using Area11.WPF.Data;
using Area11.WPF.Models;
using Area11.WPF.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.WPF.Services;

/// <summary>
/// Counter billing.
/// Rules taken from the reference: nearest-expiry-first (FEFO), the pack formula converts
/// strips/boxes to base units, discount follows the configured base (margin or retail) with a
/// per-role ceiling, the bill rounds down but never below cost, and a negative stock only
/// raises a warning (the sale is never blocked — owner rule U-30).
/// Preview and save share one calculation, so the cashier always sees the amount that is stored.
/// </summary>
public sealed class SaleService
{
    private readonly Database _db;
    private readonly LoggerService _log;
    private readonly AuditService _audit;
    private readonly NumberingService _numbering;
    private readonly SettingsService _settings;
    private readonly PartnerService _partners;

    public SaleService(
        Database db,
        LoggerService log,
        AuditService audit,
        NumberingService numbering,
        SettingsService settings,
        PartnerService partners)
    {
        _db = db;
        _log = log;
        _audit = audit;
        _numbering = numbering;
        _settings = settings;
        _partners = partners;
    }

    public OperationResult<SalePreview> Preview(SaleInput input, User user)
    {
        try
        {
            using var connection = _db.Open();
            var warnings = new List<string>();
            var computation = Compute(connection, input, user, warnings);

            var change = 0L;
            if (string.Equals(input.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase)
                && input.TenderedPaisa > 0)
            {
                change = Math.Max(0, input.TenderedPaisa - computation.TotalPaisa);
            }

            return OperationResult<SalePreview>.Success(new SalePreview
            {
                SubtotalPaisa = computation.SubtotalPaisa,
                DiscountPaisa = computation.DiscountPaisa,
                TaxPaisa = computation.TaxPaisa,
                RoundPaisa = computation.RoundPaisa,
                TotalPaisa = computation.TotalPaisa,
                ChangePaisa = change,
                Lines = computation.Drafts.Count
            }, warnings);
        }
        catch (ValidationException exception)
        {
            return OperationResult<SalePreview>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("Sale preview failed.", exception);
            return OperationResult<SalePreview>.Fail("The total could not be calculated.");
        }
    }

    public OperationResult<SaleResult> Create(SaleInput input, User user)
    {
        try
        {
            var result = _db.Tx(connection => Save(connection, input, user));
            return OperationResult<SaleResult>.Success(result.Value!, result.Warnings);
        }
        catch (ValidationException exception)
        {
            return OperationResult<SaleResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("Sale could not be saved.", exception);
            return OperationResult<SaleResult>.Fail("The bill could not be saved. Please try again.");
        }
    }

    private OperationResult<SaleResult> Save(SqliteConnection connection, SaleInput input, User user)
    {
        var warnings = new List<string>();
        var computation = Compute(connection, input, user, warnings);
        var stamp = FormatHelper.Stamp();
        var shiftId = GetOpenShiftId(connection);

        var customer = ResolveCustomer(connection, input.CustomerId);
        var method = (input.PaymentMethod ?? "cash").Trim().ToLowerInvariant();

        var paidPaisa = 0L;
        var duePaisa = 0L;
        var creditUsed = 0L;
        var tendered = 0L;
        var change = 0L;
        var cashParts = new List<PaymentSplitInput>();

        if (method == "split")
        {
            if (input.Splits.Count == 0)
            {
                throw new ValidationException("Split payment needs at least one part.");
            }

            if (input.Splits.Sum(s => s.AmountPaisa) != computation.TotalPaisa)
            {
                throw new ValidationException(
                    $"Split parts must add up to {FormatHelper.Rupees(computation.TotalPaisa)}.");
            }

            foreach (var split in input.Splits)
            {
                var splitMethod = (split.Method ?? "cash").Trim().ToLowerInvariant();
                if (splitMethod == "credit")
                {
                    if (customer is null)
                    {
                        throw new ValidationException("Credit needs a selected customer.");
                    }

                    creditUsed += split.AmountPaisa;
                    duePaisa += split.AmountPaisa;
                }
                else
                {
                    paidPaisa += split.AmountPaisa;
                    cashParts.Add(split);
                }
            }
        }
        else if (method == "credit")
        {
            if (customer is null)
            {
                throw new ValidationException("Credit needs a selected customer.");
            }

            creditUsed = computation.TotalPaisa;
            duePaisa = computation.TotalPaisa;
        }
        else
        {
            tendered = input.TenderedPaisa > 0 ? input.TenderedPaisa : computation.TotalPaisa;
            if (tendered < computation.TotalPaisa)
            {
                throw new ValidationException(
                    $"Cash received ({FormatHelper.Rupees(tendered)}) is less than the bill ({FormatHelper.Rupees(computation.TotalPaisa)}).");
            }

            change = tendered - computation.TotalPaisa;
            paidPaisa = computation.TotalPaisa;
            cashParts.Add(new PaymentSplitInput { Method = "cash", AmountPaisa = paidPaisa });
        }

        if (creditUsed > 0 && customer is not null)
        {
            EnforceCreditLimit(customer, creditUsed, user);
        }

        var code = _numbering.Take(connection, "sale");

        _db.Exec(connection, @"
            INSERT INTO sales
                (code, customer_id, shift_id, subtotal_paisa, discount_paisa, tax_paisa, round_paisa,
                 total_paisa, paid_paisa, due_paisa, tendered_paisa, change_paisa, payment_method,
                 status, note, created_at, created_by)
            VALUES
                ($code, $customer, $shift, $subtotal, $discount, $tax, $round,
                 $total, $paid, $due, $tendered, $change, $method,
                 $status, $note, $at, $user);",
            ("$code", code),
            ("$customer", input.CustomerId is > 0 ? input.CustomerId.Value : DBNull.Value),
            ("$shift", shiftId ?? (object)DBNull.Value),
            ("$subtotal", computation.SubtotalPaisa),
            ("$discount", computation.DiscountPaisa),
            ("$tax", computation.TaxPaisa),
            ("$round", computation.RoundPaisa),
            ("$total", computation.TotalPaisa),
            ("$paid", paidPaisa),
            ("$due", duePaisa),
            ("$tendered", tendered),
            ("$change", change),
            ("$method", method),
            ("$status", duePaisa == 0 ? "paid" : "partial"),
            ("$note", string.IsNullOrWhiteSpace(input.Note) ? DBNull.Value : input.Note.Trim()),
            ("$at", stamp),
            ("$user", user.Name));

        var saleId = (int)_db.LastInsertId(connection);

        foreach (var draft in computation.Drafts)
        {
            _db.Exec(connection, @"
                INSERT INTO sale_items
                    (sale_id, product_id, batch_id, unit, qty_base, unit_price_paisa, discount_paisa, line_total_paisa, cost_paisa)
                VALUES($sale, $product, $batch, $unit, $qty, $price, $discount, $total, $cost);",
                ("$sale", saleId),
                ("$product", draft.ProductId),
                ("$batch", draft.BatchId ?? (object)DBNull.Value),
                ("$unit", draft.Unit),
                ("$qty", draft.QtyBase),
                ("$price", draft.UnitPricePaisa),
                ("$discount", draft.DiscountPaisa),
                ("$total", draft.LineTotalPaisa),
                ("$cost", draft.CostPaisa));

            if (draft.BatchId is not null)
            {
                _db.Exec(connection,
                    "UPDATE batches SET qty_base = qty_base - $qty WHERE id = $id;",
                    ("$qty", draft.QtyBase),
                    ("$id", draft.BatchId.Value));
            }

            _db.Exec(connection, @"
                INSERT INTO stock_movements(product_id, batch_id, type, qty_base, note, created_at, created_by)
                VALUES($product, $batch, 'sale', $qty, $note, $at, $user);",
                ("$product", draft.ProductId),
                ("$batch", draft.BatchId ?? (object)DBNull.Value),
                ("$qty", -draft.QtyBase),
                ("$note", $"Sale {code}"),
                ("$at", stamp),
                ("$user", user.Name));

            _db.Exec(connection,
                "UPDATE products SET stock_base = stock_base - $qty, updated_at = $at WHERE id = $id;",
                ("$qty", draft.QtyBase),
                ("$at", stamp),
                ("$id", draft.ProductId));
        }

        foreach (var part in cashParts)
        {
            _db.Exec(connection, @"
                INSERT INTO payments(sale_id, customer_id, amount_paisa, method, note, shift_id, created_at, created_by)
                VALUES($sale, $customer, $amount, $method, $note, $shift, $at, $user);",
                ("$sale", saleId),
                ("$customer", input.CustomerId is > 0 ? input.CustomerId.Value : DBNull.Value),
                ("$amount", part.AmountPaisa),
                ("$method", (part.Method ?? "cash").Trim().ToLowerInvariant()),
                ("$note", $"Sale {code}"),
                ("$shift", shiftId ?? (object)DBNull.Value),
                ("$at", stamp),
                ("$user", user.Name));
        }

        if (creditUsed > 0 && customer is not null)
        {
            _partners.AddCustomerCredit(connection, customer.Id, creditUsed);
        }

        _audit.Record(connection, user.Name, "POS", "create", "sales", saleId, null,
            new
            {
                code,
                total = computation.TotalPaisa,
                subtotal = computation.SubtotalPaisa,
                discount = computation.DiscountPaisa,
                round = computation.RoundPaisa,
                method,
                due = duePaisa
            },
            warnings.Count == 0 ? null : string.Join(" ", warnings));

        return OperationResult<SaleResult>.Success(new SaleResult
        {
            Code = code,
            TotalPaisa = computation.TotalPaisa,
            ChangePaisa = change,
            DuePaisa = duePaisa,
            ItemLines = computation.Drafts.Count
        }, warnings);
    }

    /// <summary>
    /// One calculation used by both preview and save: FEFO allocation, discount, tax, rounding,
    /// then the adjustment is spread over the lines so their sum equals the printed total.
    /// </summary>
    private Computation Compute(SqliteConnection connection, SaleInput input, User user, List<string> warnings)
    {
        if (input.Lines.Count == 0)
        {
            throw new ValidationException("The cart is empty.");
        }

        if (!_settings.GetBool("discount.enabled") && input.DiscountPercent > 0)
        {
            throw new ValidationException("Discounts are switched off in Settings.");
        }

        var allowedPercent = user.Role switch
        {
            UserRole.Owner => 100m,
            UserRole.Manager => _settings.GetInt("discount.maxPercentManager", 20),
            _ => _settings.GetInt("discount.maxPercentCashier", 5)
        };

        if (input.DiscountPercent < 0 || input.DiscountPercent > allowedPercent)
        {
            throw new ValidationException(
                $"{user.Role} can give at most {allowedPercent:0.##}% discount (Settings -> Discount).");
        }

        var customer = ResolveCustomer(connection, input.CustomerId);
        var drafts = new List<SaleLineDraft>();

        foreach (var line in input.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new ValidationException("Every cart line needs a quantity of at least 1.");
            }

            drafts.AddRange(BuildDrafts(connection, line, customer, warnings));
        }

        var marginMode = !string.Equals(_settings.Get("discount.mode"), "retail", StringComparison.OrdinalIgnoreCase);
        var blockBelowCost = _settings.GetBool("discount.blockBelowCost");

        foreach (var draft in drafts)
        {
            var gross = draft.LineTotalPaisa;
            var baseForDiscount = marginMode ? gross - draft.CostPaisa : gross;
            var discount = (long)Math.Round(baseForDiscount * input.DiscountPercent / 100m, MidpointRounding.AwayFromZero);

            if (blockBelowCost && gross - discount < draft.CostPaisa)
            {
                discount = Math.Max(0, gross - draft.CostPaisa);
                warnings.Add($"{draft.ProductName}: discount limited so the price never falls below cost.");
            }

            draft.DiscountPaisa = Math.Max(0, discount);
        }

        var subtotal = drafts.Sum(d => d.LineTotalPaisa);
        var discountTotal = drafts.Sum(d => d.DiscountPaisa);
        var costTotal = drafts.Sum(d => d.CostPaisa);

        foreach (var draft in drafts)
        {
            draft.LineTotalPaisa -= draft.DiscountPaisa;
        }

        var netBeforeExtras = subtotal - discountTotal;
        var taxPaisa = _settings.GetBool("tax.enabled")
            ? (long)Math.Round(netBeforeExtras * _settings.GetInt("tax.percent", 0) / 100m, MidpointRounding.AwayFromZero)
            : 0L;

        var roundTo = Math.Max(0, _settings.GetInt("bill.roundTo", 10));
        var billingBase = netBeforeExtras + taxPaisa;
        var total = string.Equals(_settings.Get("bill.roundMode"), "none", StringComparison.OrdinalIgnoreCase) || roundTo == 0
            ? billingBase
            : MoneyHelper.ApplyRoundDownWithCostGuard(billingBase, costTotal, roundTo);

        var roundPaisa = total - billingBase;

        SpreadAcrossLines(drafts, taxPaisa + roundPaisa);

        return new Computation
        {
            Drafts = drafts,
            SubtotalPaisa = subtotal,
            DiscountPaisa = discountTotal,
            TaxPaisa = taxPaisa,
            RoundPaisa = roundPaisa,
            TotalPaisa = total
        };
    }

    /// <summary>Price for one selected unit, adjusted to the customer type (VIP / doctor / retail).</summary>
    public long ResolveUnitPrice(Product product, string unit, Customer? customer)
    {
        var price = customer?.Type.ToLowerInvariant() switch
        {
            "vip" => product.VipPaisa > 0 ? product.VipPaisa : product.RetailPaisa,
            "doctor" => product.DoctorPaisa > 0 ? product.DoctorPaisa : product.RetailPaisa,
            _ => product.RetailPaisa
        };

        var units = unit switch
        {
            "box" => Math.Max(1, product.BoxStrips) * Math.Max(1, product.StripUnits),
            "strip" => Math.Max(1, product.StripUnits),
            _ => 1
        };

        return (long)Math.Round(price * units, MidpointRounding.AwayFromZero);
    }

    private List<SaleLineDraft> BuildDrafts(
        SqliteConnection connection,
        SaleLineInput line,
        Customer? customer,
        List<string> warnings)
    {
        var product = _db.Query(connection, @"
            SELECT id, name, box_strips, strip_units, retail_paisa, vip_paisa, doctor_paisa
            FROM products WHERE id = $id AND active = 1;",
            reader => new Product
            {
                Id = reader.Int("id"),
                Name = reader.Str("name"),
                BoxStrips = reader.Int("box_strips"),
                StripUnits = reader.Int("strip_units"),
                RetailPaisa = reader.Long("retail_paisa"),
                VipPaisa = reader.Long("vip_paisa"),
                DoctorPaisa = reader.Long("doctor_paisa")
            },
            ("$id", line.ProductId)).FirstOrDefault()
            ?? throw new ValidationException("A product in the cart no longer exists.");

        var unit = (line.Unit ?? "base").Trim().ToLowerInvariant();
        var unitsPerSelected = unit switch
        {
            "box" => Math.Max(1, product.BoxStrips) * Math.Max(1, product.StripUnits),
            "strip" => Math.Max(1, product.StripUnits),
            _ => 1
        };

        var qtyBase = line.Quantity * unitsPerSelected;
        var unitPrice = line.UnitPricePaisa > 0 ? line.UnitPricePaisa : ResolveUnitPrice(product, unit, customer);
        var lineTotal = (long)Math.Round((decimal)unitPrice * line.Quantity, MidpointRounding.AwayFromZero);

        var batches = _db.Query(connection, @"
            SELECT id, batch_no, qty_base, cost_paisa
            FROM batches
            WHERE product_id = $product AND qty_base > 0
              AND (expiry = '' OR expiry >= date('now','localtime'))
            ORDER BY CASE WHEN expiry = '' THEN 1 ELSE 0 END, expiry, id;",
            reader => new
            {
                id = reader.Int("id"),
                batchNo = reader.Str("batch_no"),
                qty = reader.Long("qty_base"),
                cost = reader.Long("cost_paisa")
            },
            ("$product", product.Id));

        var available = batches.Sum(b => b.qty);
        var allowNegative = _settings.GetBool("stock.allowNegative");
        var remaining = (long)qtyBase;
        var drafts = new List<SaleLineDraft>();
        var allocated = 0L;

        foreach (var batch in batches)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(batch.qty, remaining);
            if (take <= 0)
            {
                continue;
            }

            drafts.Add(new SaleLineDraft
            {
                ProductId = product.Id,
                ProductName = product.Name,
                BatchId = batch.id,
                BatchNo = batch.batchNo,
                Unit = unit,
                QtyBase = (int)take,
                UnitPricePaisa = (long)Math.Round((decimal)lineTotal / qtyBase, MidpointRounding.AwayFromZero),
                CostPaisa = batch.cost * take
            });

            remaining -= take;
            allocated += take;
        }

        if (remaining > 0)
        {
            if (!allowNegative)
            {
                throw new ValidationException(
                    $"{product.Name}: only {available} in stock, {qtyBase} requested (negative stock is switched off in Settings).");
            }

            // The owner's rule: never stop a sale. The shortfall is written against the nearest
            // batch so the shelf count goes truly negative and the alert shows the real number.
            var fallback = drafts.LastOrDefault();
            var fallbackCost = fallback is not null ? fallback.CostPaisa / Math.Max(1, fallback.QtyBase) : 0L;

            drafts.Add(new SaleLineDraft
            {
                ProductId = product.Id,
                ProductName = product.Name,
                BatchId = fallback?.BatchId,
                BatchNo = fallback?.BatchNo ?? string.Empty,
                Unit = unit,
                QtyBase = (int)remaining,
                UnitPricePaisa = (long)Math.Round((decimal)lineTotal / qtyBase, MidpointRounding.AwayFromZero),
                CostPaisa = fallbackCost * remaining
            });

            warnings.Add(
                $"{product.Name}: stock went MINUS by {remaining} base unit(s) (available {available}, sold {qtyBase}).");
        }

        // Distribute the line's money over its batch allocations; the last one absorbs rounding.
        var allocatedMoney = 0L;
        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            var share = index == drafts.Count - 1
                ? lineTotal - allocatedMoney
                : (long)Math.Round((decimal)lineTotal * draft.QtyBase / Math.Max(1, allocated), MidpointRounding.AwayFromZero);

            draft.LineTotalPaisa = share;
            allocatedMoney += share;
        }

        return drafts;
    }

    /// <summary>Adds tax and rounding to the item lines so their sum equals the printed bill total.</summary>
    private static void SpreadAcrossLines(List<SaleLineDraft> drafts, long adjustment)
    {
        if (adjustment == 0 || drafts.Count == 0)
        {
            return;
        }

        var baseSum = drafts.Sum(d => d.LineTotalPaisa);
        var given = 0L;

        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            var share = index == drafts.Count - 1
                ? adjustment - given
                : (long)Math.Round((decimal)adjustment * draft.LineTotalPaisa / Math.Max(1, baseSum), MidpointRounding.AwayFromZero);

            draft.LineTotalPaisa += share;
            given += share;
        }
    }

    private Customer? ResolveCustomer(SqliteConnection connection, int? customerId)
    {
        if (customerId is not > 0)
        {
            return null;
        }

        return _db.Query(connection,
            "SELECT id, name, type, credit_limit_paisa, balance_paisa FROM customers WHERE id = $id AND active = 1;",
            reader => new Customer
            {
                Id = reader.Int("id"),
                Name = reader.Str("name"),
                Type = reader.Str("type"),
                CreditLimitPaisa = reader.Long("credit_limit_paisa"),
                BalancePaisa = reader.Long("balance_paisa")
            },
            ("$id", customerId.Value)).FirstOrDefault()
            ?? throw new ValidationException("The selected customer no longer exists.");
    }

    private void EnforceCreditLimit(Customer customer, long newCredit, User user)
    {
        if (!_settings.GetBool("credit.blockOverLimit") || user.IsOwner)
        {
            return;
        }

        if (user.Role == UserRole.Manager && _settings.GetBool("credit.managerCanOverride"))
        {
            return;
        }

        if (customer.CreditLimitPaisa <= 0)
        {
            return;
        }

        var after = customer.BalancePaisa + newCredit;
        if (after > customer.CreditLimitPaisa)
        {
            throw new ValidationException(
                $"{customer.Name}: credit limit {FormatHelper.Rupees(customer.CreditLimitPaisa)} would be crossed " +
                $"(old {FormatHelper.Rupees(customer.BalancePaisa)} + this bill {FormatHelper.Rupees(newCredit)} = {FormatHelper.Rupees(after)}). " +
                "Credit over the limit is blocked in Settings.");
        }
    }

    private int? GetOpenShiftId(SqliteConnection connection)
    {
        var row = _db.Query(connection,
            "SELECT id FROM shifts WHERE closed_at IS NULL ORDER BY id DESC LIMIT 1;",
            reader => reader.Int("id"));
        return row.Count == 0 ? null : row[0];
    }

    private sealed class Computation
    {
        public List<SaleLineDraft> Drafts { get; init; } = new();

        public long SubtotalPaisa { get; init; }

        public long DiscountPaisa { get; init; }

        public long TaxPaisa { get; init; }

        public long RoundPaisa { get; init; }

        public long TotalPaisa { get; init; }
    }
}
