using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// The counter (Spec 12.2). One transaction:
///   validate → FEFO batches → tier price → discount guard → tax → round down with cost guard →
///   reconcile the lines → deduct stock (negative allowed, warning only) → sale + payments →
///   customer khata → black box.
/// The same calculation produces both the live quote and the saved bill, so the cashier can
/// never see a total different from the one stored.
/// </summary>
public sealed class SaleService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly NumberingService _numbering;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public SaleService(
        Database db,
        AuditService audit,
        NumberingService numbering,
        SettingsService settings,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _numbering = numbering;
        _settings = settings;
        _permissions = permissions;
        _log = log;
    }

    public ServiceResult<SaleQuote> Quote(SaleInput input, User user)
    {
        try
        {
            using var connection = _db.Open();
            var computed = Compute(connection, input, user);

            var change = 0L;
            if (IsCash(input) && input.TenderedPaisa > 0)
            {
                change = Math.Max(0, input.TenderedPaisa - computed.TotalPaisa);
            }

            return ServiceResult<SaleQuote>.Success(new SaleQuote
            {
                SubtotalPaisa = computed.SubtotalPaisa,
                DiscountPaisa = computed.DiscountPaisa,
                TaxPaisa = computed.TaxPaisa,
                RoundPaisa = computed.RoundPaisa,
                TotalPaisa = computed.TotalPaisa,
                ChangePaisa = change,
                Units = computed.Lines.Sum(line => line.QtyBase),
                GoesNegative = computed.WentNegative,
                Warnings = computed.Warnings
            }, computed.Warnings.ToArray());
        }
        catch (ValidationException exception)
        {
            return ServiceResult<SaleQuote>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The bill could not be calculated.", exception);
            return ServiceResult<SaleQuote>.Fail("The bill could not be calculated.");
        }
    }

    public ServiceResult<SaleResult> Create(SaleInput input, User user)
    {
        if (!_permissions.Can(user, Modules.Pos))
        {
            return ServiceResult<SaleResult>.Fail("You do not have permission to make bills.");
        }

        try
        {
            var result = _db.Tx(connection => Save(connection, input, user));
            return result;
        }
        catch (ValidationException exception)
        {
            return ServiceResult<SaleResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The bill could not be saved.", exception);
            return ServiceResult<SaleResult>.Fail("The bill could not be saved. Please try again.");
        }
    }

    private ServiceResult<SaleResult> Save(SqliteConnection connection, SaleInput input, User user)
    {
        var computed = Compute(connection, input, user);
        var stamp = Clock.Now();
        var method = NormalizeMethod(input.PaymentMethod);

        var customer = ResolveCustomer(connection, input.CustomerId);

        long paid;
        long due;
        long tendered = 0;
        long change = 0;
        string status;

        if (method == "credit")
        {
            if (customer is null)
            {
                throw new ValidationException("Credit needs a selected customer.");
            }

            paid = 0;
            due = computed.TotalPaisa;
            status = "credit";
        }
        else
        {
            tendered = input.TenderedPaisa > 0 ? input.TenderedPaisa : computed.TotalPaisa;
            if (tendered < computed.TotalPaisa)
            {
                throw new ValidationException(
                    $"Cash received ({Money.Format(tendered)}) is less than the bill ({Money.Format(computed.TotalPaisa)}).");
            }

            change = tendered - computed.TotalPaisa;
            paid = computed.TotalPaisa;
            due = 0;
            status = "paid";
        }

        if (due > 0 && customer is not null)
        {
            EnforceCreditLimit(customer, due, user);
        }

        var code = _numbering.Take(connection, "sale");

        _db.Execute(connection, @"
INSERT INTO sales(code, date, customer_id, user_id, subtotal_paisa, discount_paisa, tax_paisa,
                  round_off_paisa, total_paisa, paid_paisa, due_paisa, status, payment_method,
                  notes, created_at, change_paisa)
VALUES(@code, @date, @customerId, @userId, @subtotal, @discount, @tax,
       @roundOff, @total, @paid, @due, @status, @method,
       @notes, @createdAt, @change);",
            new
            {
                code,
                date = stamp,
                customerId = input.CustomerId is > 0 ? input.CustomerId.Value : (int?)null,
                userId = user.Id,
                subtotal = computed.SubtotalPaisa,
                discount = computed.DiscountPaisa,
                tax = computed.TaxPaisa,
                roundOff = computed.RoundPaisa,
                total = computed.TotalPaisa,
                paid,
                due,
                status,
                method,
                notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                createdAt = stamp,
                change
            });

        var saleId = (int)_db.NextId(connection);

        foreach (var line in computed.Lines)
        {
            _db.Execute(connection, @"
INSERT INTO sale_items(sale_id, product_id, batch_id, name_snapshot, qty_base, unit_sold, qty_entered,
                       unit_price_paisa, discount_paisa, line_total_paisa, cost_paisa_at_sale)
VALUES(@saleId, @productId, @batchId, @name, @qtyBase, @unitSold, @qtyEntered,
       @unitPrice, @discount, @lineTotal, @costUnit);",
                new
                {
                    saleId,
                    productId = line.ProductId,
                    batchId = line.BatchId,
                    name = line.Name,
                    qtyBase = line.QtyBase,
                    unitSold = line.UnitSold,
                    qtyEntered = line.QtyEntered,
                    unitPrice = line.UnitPricePaisa,
                    discount = line.DiscountPaisa,
                    lineTotal = line.LineTotalPaisa,
                    costUnit = line.CostUnitPaisa
                });

            if (line.BatchId is not null)
            {
                _db.Execute(connection,
                    "UPDATE batches SET qty_base = qty_base - @qty WHERE id = @id;",
                    new { qty = line.QtyBase, id = line.BatchId.Value });
            }

            _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, note, user_id, created_at)
VALUES(@productId, @batchId, 'sale', @qty, @note, @userId, @at);",
                new
                {
                    productId = line.ProductId,
                    batchId = line.BatchId,
                    qty = -line.QtyBase,
                    note = $"Sale {code}",
                    userId = user.Id,
                    at = stamp
                });
        }

        if (paid > 0)
        {
            _db.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, sale_id, customer_id, user_id, note)
VALUES(@at, @method, @amount, @saleId, @customerId, @userId, @note);",
                new
                {
                    at = stamp,
                    method,
                    amount = paid,
                    saleId,
                    customerId = input.CustomerId is > 0 ? input.CustomerId.Value : (int?)null,
                    userId = user.Id,
                    note = $"Sale {code}"
                });
        }

        if (due > 0 && customer is not null)
        {
            _db.Execute(connection,
                "UPDATE customers SET balance_paisa = balance_paisa + @amount WHERE id = @id;",
                new { amount = due, id = customer.Id });
        }

        _audit.Record(connection, user, "POS", "sale_create", "sales", saleId.ToString(),
            newValue: $"{{\"code\":\"{code}\",\"total\":{computed.TotalPaisa},\"discount\":{computed.DiscountPaisa},\"round\":{computed.RoundPaisa},\"method\":\"{method}\"}}");

        if (computed.WentNegative)
        {
            _audit.Record(connection, user, "POS", "negative_sale", "sales", saleId.ToString(),
                details: string.Join(" | ", computed.Warnings));
        }

        return ServiceResult<SaleResult>.Success(new SaleResult
        {
            SaleId = saleId,
            Code = code,
            SubtotalPaisa = computed.SubtotalPaisa,
            DiscountPaisa = computed.DiscountPaisa,
            TaxPaisa = computed.TaxPaisa,
            RoundPaisa = computed.RoundPaisa,
            TotalPaisa = computed.TotalPaisa,
            PaidPaisa = paid,
            DuePaisa = due,
            ChangePaisa = change,
            Warnings = computed.Warnings
        }, computed.Warnings.ToArray());
    }

    /// <summary>The single calculation used by quote and save (Spec 11.4, 12.2).</summary>
    private ComputedSale Compute(SqliteConnection connection, SaleInput input, User user)
    {
        if (input.Lines.Count == 0)
        {
            throw new ValidationException("The cart is empty.");
        }

        if (!_settings.GetBool("discount.enabled") && input.DiscountPercent > 0)
        {
            throw new ValidationException("Discounts are switched off in Settings.");
        }

        var allowedPercent = user.Kind switch
        {
            UserRole.Owner => 100m,
            UserRole.Manager => _settings.GetInt("discount.maxPercentManager", 20),
            _ => _settings.GetInt("discount.maxPercentCashier", 5)
        };

        if (input.DiscountPercent < 0 || input.DiscountPercent > allowedPercent)
        {
            throw new ValidationException(
                $"{user.RoleLabel} can give at most {allowedPercent:0.##}% discount.");
        }

        var customer = ResolveCustomer(connection, input.CustomerId);
        var warnings = new List<string>();
        var lines = new List<SaleDraftLine>();
        var wentNegative = false;

        foreach (var lineInput in input.Lines)
        {
            if (lineInput.Quantity <= 0)
            {
                throw new ValidationException("Every line needs a quantity greater than zero.");
            }

            var product = _db.QuerySingle<ProductRow>(connection, @"
SELECT id, name, base_unit, box_strips, strip_tablets, retail_paisa, vip_paisa, doctor_paisa
FROM products WHERE id = @id AND active = 1;",
                new { id = lineInput.ProductId })
                ?? throw new ValidationException("A product in the cart no longer exists.");

            var unit = (lineInput.Unit ?? "base").Trim().ToLowerInvariant();
            // double, never int: the schema stores quantities as REAL and an int here made
            // Math.Round(long) ambiguous (CS0121) on the first Windows build.
            var unitsPerSelected = unit switch
            {
                "box" => (double)(Math.Max(1, product.BoxStrips) * Math.Max(1, product.StripTablets)),
                "strip" => (double)Math.Max(1, product.StripTablets),
                _ => 1d
            };

            var qtyBase = Calc.ToBase(lineInput.Quantity, unitsPerSelected);

            var tierPrice = customer?.Category?.ToLowerInvariant() switch
            {
                "vip" => product.VipPaisa > 0 ? product.VipPaisa : product.RetailPaisa,
                "doctor" => product.DoctorPaisa > 0 ? product.DoctorPaisa : product.RetailPaisa,
                _ => product.RetailPaisa
            };

            if (tierPrice <= 0)
            {
                throw new ValidationException($"{product.Name}: no retail rate is set.");
            }

            var pricePerSelected = lineInput.UnitPricePaisa > 0
                ? lineInput.UnitPricePaisa
                : Calc.Round(tierPrice * unitsPerSelected);

            var gross = Calc.Round(pricePerSelected * lineInput.Quantity);

            var batches = _db.Query<BatchRow>(connection, @"
SELECT id, batch_no, qty_base, cost_paisa
FROM batches
WHERE product_id = @productId AND active = 1 AND qty_base > 0
  AND (expiry_date IS NULL OR expiry_date = '' OR date(expiry_date) >= date('now','localtime'))
ORDER BY CASE WHEN expiry_date IS NULL OR expiry_date = '' THEN 1 ELSE 0 END, expiry_date, id;",
                new { productId = product.Id });

            var available = batches.Sum(batch => batch.QtyBase);
            var allowNegative = _settings.GetBool("stock.allowNegative");
            var remaining = qtyBase;
            var allocated = 0.0;
            var lineDrafts = new List<SaleDraftLine>();

            foreach (var batch in batches)
            {
                if (remaining <= 0.000001)
                {
                    break;
                }

                var take = Math.Min(batch.QtyBase, remaining);

                lineDrafts.Add(new SaleDraftLine
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    BatchId = batch.Id,
                    BatchNo = batch.BatchNo,
                    UnitSold = unit,
                    QtyEntered = lineInput.Quantity * (take / qtyBase),
                    QtyBase = take,
                    UnitPricePaisa = Calc.Round((double)pricePerSelected / unitsPerSelected),
                    CostUnitPaisa = batch.CostPaisa,
                    GrossPaisa = 0
                });

                remaining -= take;
                allocated += take;
            }

            if (remaining > 0.000001)
            {
                if (!allowNegative)
                {
                    throw new ValidationException(
                        $"{product.Name}: only {Qty.Format(available)} in stock, {Qty.Format(qtyBase)} requested " +
                        "(negative stock is switched off in Settings).");
                }

                var fallback = lineDrafts.LastOrDefault();

                lineDrafts.Add(new SaleDraftLine
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    BatchId = fallback?.BatchId,
                    BatchNo = fallback?.BatchNo ?? string.Empty,
                    UnitSold = unit,
                    QtyEntered = lineInput.Quantity * (remaining / qtyBase),
                    QtyBase = remaining,
                    UnitPricePaisa = Calc.Round((double)pricePerSelected / unitsPerSelected),
                    CostUnitPaisa = fallback?.CostUnitPaisa ?? 0,
                    GrossPaisa = 0
                });

                wentNegative = true;
                warnings.Add(
                    $"{product.Name}: stock goes MINUS by {Qty.Format(remaining)} {product.BaseUnit} " +
                    $"(available {Qty.Format(available)}, sold {Qty.Format(qtyBase)}).");
            }

            // Split the line's money over its batch allocations; the last one absorbs rounding.
            var given = 0L;
            for (var index = 0; index < lineDrafts.Count; index++)
            {
                var draft = lineDrafts[index];
                var share = index == lineDrafts.Count - 1
                    ? gross - given
                    : Calc.Round(gross * (draft.QtyBase / allocated));

                draft.GrossPaisa = share;
                given += share;
            }

            lines.AddRange(lineDrafts);
        }

        // Discount (margin or retail), never below cost when the lock is on.
        var marginMode = !string.Equals(_settings.GetString("discount.mode"), "retail", StringComparison.OrdinalIgnoreCase);
        var blockBelowCost = _settings.GetBool("discount.blockBelowCost");

        foreach (var line in lines)
        {
            var costs = line.LineCostPaisa;
            var baseForDiscount = marginMode ? Math.Max(0, line.GrossPaisa - costs) : line.GrossPaisa;
            var discount = Calc.RoundMoney(baseForDiscount * input.DiscountPercent / 100m);

            if (blockBelowCost && line.GrossPaisa - discount < costs)
            {
                discount = Math.Max(0, line.GrossPaisa - costs);
                warnings.Add($"{line.Name}: discount limited so the price never falls below cost.");
            }

            line.DiscountPaisa = Math.Max(0, discount);
            line.LineTotalPaisa = line.GrossPaisa - line.DiscountPaisa;
        }

        var subtotal = lines.Sum(line => line.GrossPaisa);
        var discountTotal = lines.Sum(line => line.DiscountPaisa);
        var costTotal = lines.Sum(line => line.LineCostPaisa);
        var netBeforeExtras = subtotal - discountTotal;

        var taxPaisa = _settings.GetBool("tax.enabled")
            ? Calc.RoundMoney(netBeforeExtras * _settings.GetInt("tax.percent", 0) / 100m)
            : 0L;

        var roundTo = Math.Max(0, _settings.GetInt("bill.roundTo", 10));
        var roundMode = _settings.GetString("bill.roundMode");
        var preRound = netBeforeExtras + taxPaisa;

        var totalPaisa = string.Equals(roundMode, "none", StringComparison.OrdinalIgnoreCase) || roundTo == 0
            ? preRound
            : Money.RoundDownWithCostGuard(preRound, costTotal, roundTo);

        var roundPaisa = totalPaisa - preRound;

        // Spread tax and round-off over the lines so their sum is exactly the printed total.
        Spread(lines, taxPaisa + roundPaisa, totalPaisa);

        return new ComputedSale
        {
            Lines = lines,
            SubtotalPaisa = subtotal,
            DiscountPaisa = discountTotal,
            TaxPaisa = taxPaisa,
            RoundPaisa = roundPaisa,
            TotalPaisa = totalPaisa,
            Warnings = warnings,
            WentNegative = wentNegative
        };
    }

    private static void Spread(List<SaleDraftLine> lines, long adjustment, long expectedTotal)
    {
        if (lines.Count == 0)
        {
            return;
        }

        if (adjustment != 0)
        {
            var baseSum = lines.Sum(line => line.LineTotalPaisa);
            var given = 0L;

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var share = index == lines.Count - 1
                    ? adjustment - given
                    : Calc.Round(adjustment * (baseSum == 0 ? 1.0 / lines.Count : (double)line.LineTotalPaisa / baseSum));

                line.LineTotalPaisa += share;
                given += share;
            }
        }

        // The bill total must equal the sum of its lines, always (Spec 11.4).
        var difference = expectedTotal - lines.Sum(line => line.LineTotalPaisa);
        if (difference != 0)
        {
            lines[^1].LineTotalPaisa += difference;
        }
    }

    private Customer? ResolveCustomer(SqliteConnection connection, int? customerId)
    {
        if (customerId is not > 0)
        {
            return null;
        }

        return _db.QuerySingle<Customer>(connection, @"
SELECT id, name, phone, category, credit_limit_paisa, balance_paisa, loyalty_points, active
FROM customers WHERE id = @id AND active = 1;",
            new { id = customerId.Value })
            ?? throw new ValidationException("The selected customer no longer exists.");
    }

    private void EnforceCreditLimit(Customer customer, long newCredit, User user)
    {
        if (!_settings.GetBool("credit.blockOverLimit") || user.IsOwner)
        {
            return;
        }

        if (user.Kind == UserRole.Manager && _settings.GetBool("credit.managerCanOverride"))
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
                $"{customer.Name}: credit limit {Money.Format(customer.CreditLimitPaisa)} would be crossed " +
                $"(old {Money.Format(customer.BalancePaisa)} + this bill {Money.Format(newCredit)} = {Money.Format(after)}).");
        }
    }

    private static bool IsCash(SaleInput input) =>
        string.Equals(NormalizeMethod(input.PaymentMethod), "cash", StringComparison.Ordinal);

    private static string NormalizeMethod(string? method)
    {
        var clean = (method ?? "cash").Trim().ToLowerInvariant();
        return clean is "credit" or "cash" or "online" or "card" ? clean : "cash";
    }

    private sealed class ProductRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string BaseUnit { get; set; } = "tablet";

        public int BoxStrips { get; set; }

        public int StripTablets { get; set; }

        public long RetailPaisa { get; set; }

        public long VipPaisa { get; set; }

        public long DoctorPaisa { get; set; }
    }

    private sealed class BatchRow
    {
        public int Id { get; set; }

        public string BatchNo { get; set; } = string.Empty;

        public double QtyBase { get; set; }

        public long CostPaisa { get; set; }
    }

    private sealed class ComputedSale
    {
        public List<SaleDraftLine> Lines { get; init; } = new();

        public long SubtotalPaisa { get; init; }

        public long DiscountPaisa { get; init; }

        public long TaxPaisa { get; init; }

        public long RoundPaisa { get; init; }

        public long TotalPaisa { get; init; }

        public List<string> Warnings { get; init; } = new();

        public bool WentNegative { get; init; }
    }
}
