using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Returns (Spec §9, U-28, U-32, U-34, U-35). A customer brings medicine back; the shop must refund
/// exactly what it was paid for those units - never more - and the shelf must match what is standing
/// behind the counter.
///
/// The rules, all enforced inside one transaction:
///   • the return hangs on the original bill; an item that is not on that bill is refused;
///   • never more than was sold, counting every earlier return of the same bill;
///   • never more money than was collected for that bill - cash refunds are capped by `paid_paisa`;
///   • if the bill was on credit, the khata is reduced first and only the rest leaves the drawer;
///   • the original bill rows are never edited (15.1.6) - a return is a new record;
///   • goods go back to the SAME batch instantly (U-32), or to quarantine with a permanent
///     Q-number when the cashier deliberately chooses "not back to stock" (U-34).
///
/// The return gets ONE permanent number for all its lines (RET-0001), so the customer leaves with
/// one receipt (U-35).
/// </summary>
public sealed class ReturnService
{
    public const string ReasonDefective = "defective";
    public const string ReasonExpired = "expired";
    public const string ReasonChangedMind = "changed mind";
    public const string ReasonWrongItem = "wrong medicine";
    public const string ReasonOther = "other";

    public static readonly string[] Reasons =
    {
        ReasonDefective, ReasonExpired, ReasonChangedMind, ReasonWrongItem, ReasonOther
    };

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly NumberingService _numbering;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public ReturnService(
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

    /// <summary>Finds a bill by its printed number (INV-0001) - what the cashier types.</summary>
    public ServiceResult<int> FindSale(string? code)
    {
        var clean = (code ?? string.Empty).Trim();
        if (clean.Length == 0)
        {
            return ServiceResult<int>.Fail("Type the bill number first, for example INV-0001.");
        }

        try
        {
            using var connection = _db.Open();

            var id = _db.QuerySingle<int?>(connection,
                "SELECT id FROM sales WHERE upper(code) = upper(@code);", new { code = clean });

            return id is null
                ? ServiceResult<int>.Fail($"No bill found with the number {clean}.")
                : ServiceResult<int>.Success(id.Value);
        }
        catch (Exception exception)
        {
            _log.Error($"The bill {clean} could not be looked up.", exception);
            return ServiceResult<int>.Fail("The bill could not be looked up.");
        }
    }

    /// <summary>
    /// "Which bill was it?" (F-20, Spec 9.1): the customer has no paper, so the shop looks the bill up
    /// by anything the person can remember - the number, a phone with or without dashes, a name, a
    /// medicine - and can narrow it by date.
    ///
    /// The result is a list, never a guess: two bills of Panadol on the same day are told apart by the
    /// date, the customer and the total, and a person chooses. Nothing is written here, so a wrong
    /// search costs nothing.
    /// </summary>
    public ServiceResult<List<SaleLookupRow>> Search(SaleLookupInput? input)
    {
        var term = (input?.Text ?? string.Empty).Trim();

        if (!TryReadDate(input?.FromDate, "From", out var fromDate, out var dateError)
            || !TryReadDate(input?.ToDate, "To", out var toDate, out dateError))
        {
            return ServiceResult<List<SaleLookupRow>>.Fail(dateError!);
        }

        if (fromDate.Length > 0 && toDate.Length > 0
            && string.CompareOrdinal(fromDate, toDate) > 0)
        {
            return ServiceResult<List<SaleLookupRow>>.Fail(
                "The 'From' date is after the 'To' date. Swap them, or leave one empty.");
        }

        if (term.Length == 0 && fromDate.Length == 0 && toDate.Length == 0)
        {
            return ServiceResult<List<SaleLookupRow>>.Fail(
                "Type a bill number, a phone number, a name or a medicine - or choose a date. "
                + "An empty search would bring every bill the shop has ever made.");
        }

        try
        {
            // A phone is typed with dashes, spaces or a leading +, but it is stored however the
            // customer was added. Comparing the digits only makes 0300-1234567 and 03001234567 one
            // number - the one thing a person at the counter will not do is retype it properly.
            var digits = new string(term.Where(char.IsDigit).ToArray());

            using var connection = _db.Open();

            var rows = _db.Query<SaleLookupRow>(connection, @"
SELECT s.id AS saleId, s.code AS code, s.date AS date,
       IFNULL(c.name, '') AS customerName, IFNULL(c.phone, '') AS customerPhone,
       s.total_paisa AS totalPaisa, s.due_paisa AS duePaisa, s.status AS status,
       (SELECT COUNT(1) FROM sale_items i WHERE i.sale_id = s.id) AS lineCount
FROM sales s
LEFT JOIN customers c ON c.id = s.customer_id
WHERE (@term = ''
       OR s.code LIKE @like COLLATE NOCASE
       OR IFNULL(c.name, '') LIKE @like COLLATE NOCASE
       OR IFNULL(c.phone, '') LIKE @like COLLATE NOCASE
       OR (@digits <> '' AND REPLACE(REPLACE(REPLACE(IFNULL(c.phone, ''), '-', ''), ' ', ''), '+', '') LIKE @digits)
       OR EXISTS (SELECT 1 FROM sale_items i
                  WHERE i.sale_id = s.id AND IFNULL(i.name_snapshot, '') LIKE @like COLLATE NOCASE))
  AND (@fromDate = '' OR SUBSTR(s.date, 1, 10) >= @fromDate)
  AND (@toDate = '' OR SUBSTR(s.date, 1, 10) <= @toDate)
ORDER BY s.date DESC, s.id DESC
LIMIT 100;",
                new
                {
                    term,
                    like = "%" + term + "%",
                    digits = digits.Length >= 3 ? "%" + digits + "%" : string.Empty,
                    fromDate,
                    toDate
                });

            return ServiceResult<List<SaleLookupRow>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The bills could not be searched.", exception);
            return ServiceResult<List<SaleLookupRow>>.Fail("The bills could not be searched.");
        }
    }

    /// <summary>
    /// A date typed by a person, in any of the three shapes the shop already uses, turned into the
    /// yyyy-MM-dd the database compares. Empty is allowed (no limit); nonsense is refused by name,
    /// because "From date is not a date" is a sentence the owner can act on.
    /// </summary>
    private static bool TryReadDate(string? text, string which, out string date, out string? error)
    {
        date = string.Empty;
        error = null;

        var clean = (text ?? string.Empty).Trim();
        if (clean.Length == 0)
        {
            return true;
        }

        var formats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy" };

        if (!DateTime.TryParseExact(clean, formats, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var parsed))
        {
            error = "The " + which + " date '" + clean + "' is not a date. Write it as 2026-10-20.";
            return false;
        }

        date = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Every line of a bill, with what already came back and what a full return would refund.</summary>
    public ServiceResult<List<ReturnLineOption>> Lines(int saleId)
    {
        try
        {
            using var connection = _db.Open();
            var sale = LoadSale(connection, saleId);

            var items = ReadItemRows(connection, sale).Cast<ReturnLineOption>().ToList();
            return ServiceResult<List<ReturnLineOption>>.Success(items);
        }
        catch (ValidationException exception)
        {
            return ServiceResult<List<ReturnLineOption>>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error($"The lines of bill {saleId} could not be loaded for a return.", exception);
            return ServiceResult<List<ReturnLineOption>>.Fail("The bill could not be loaded.");
        }
    }

    /// <summary>What the refund would be, without writing anything (the number the cashier reads out).</summary>
    public ServiceResult<ReturnQuote> Quote(ReturnInput input, User user)
    {
        if (!_permissions.Can(user, Modules.Returns))
        {
            return ServiceResult<ReturnQuote>.Fail("You do not have permission to take a return.");
        }

        try
        {
            using var connection = _db.Open();
            var plan = Plan(connection, input, user);

            return ServiceResult<ReturnQuote>.Success(new ReturnQuote
            {
                RefundPaisa = plan.RefundPaisa,
                CashPaisa = plan.CashPaisa,
                CreditReducedPaisa = plan.CreditPaisa,
                RefundablePaisa = plan.RefundablePaisa,
                Warnings = plan.Warnings
            }, plan.Warnings.ToArray());
        }
        catch (ValidationException exception)
        {
            return ServiceResult<ReturnQuote>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The refund could not be calculated.", exception);
            return ServiceResult<ReturnQuote>.Fail("The refund could not be calculated.");
        }
    }

    public ServiceResult<ReturnResult> Create(ReturnInput input, User user)
    {
        if (!_permissions.Can(user, Modules.Returns))
        {
            return ServiceResult<ReturnResult>.Fail("You do not have permission to take a return.");
        }

        try
        {
            var result = _db.Tx(connection => Save(connection, input, user));
            return result;
        }
        catch (ValidationException exception)
        {
            return ServiceResult<ReturnResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The return could not be saved.", exception);
            return ServiceResult<ReturnResult>.Fail("The return could not be saved. Nothing was changed.");
        }
    }

    // ------------------------------------------------------------------ saving

    private ServiceResult<ReturnResult> Save(SqliteConnection connection, ReturnInput input, User user)
    {
        var plan = Plan(connection, input, user);
        var stamp = Clock.Now();
        var code = _numbering.Take(connection, "return");

        var quarantineCodes = new List<string>();
        var cashGiven = 0L;
        var creditGiven = 0L;
        int returnId = 0;

        for (var index = 0; index < plan.Lines.Count; index++)
        {
            var line = plan.Lines[index];

            // Money follows the lines: the last line absorbs the rounding so the parts always add
            // up to the refund the customer was told.
            var cashShare = index == plan.Lines.Count - 1
                ? plan.CashPaisa - cashGiven
                : Calc.Round((double)plan.CashPaisa * line.RefundPaisa / Math.Max(1, plan.RefundPaisa));

            var creditShare = index == plan.Lines.Count - 1
                ? plan.CreditPaisa - creditGiven
                : Calc.Round((double)plan.CreditPaisa * line.RefundPaisa / Math.Max(1, plan.RefundPaisa));

            cashGiven += cashShare;
            creditGiven += creditShare;

            string? qcode = null;
            if (!input.Restock)
            {
                qcode = _numbering.Take(connection, "quarantine");
                quarantineCodes.Add(qcode);
            }

            _db.Execute(connection, @"
INSERT INTO sale_returns(date, sale_id, sale_item_id, product_id, batch_id, qty_base, refund_paisa,
                         restock, reason, user_id, created_at, qcode, disposition,
                         qty_entered, unit_sold, note, cash_refund_paisa, credit_reduced_paisa, code)
VALUES(@date, @saleId, @itemId, @productId, @batchId, @qtyBase, @refund,
       @restock, @reason, @userId, @at, @qcode, @disposition,
       @qtyEntered, @unitSold, @note, @cash, @credit, @code);",
                new
                {
                    date = stamp,
                    saleId = plan.Sale.Id,
                    itemId = line.Item.SaleItemId,
                    productId = line.Item.ProductId,
                    batchId = line.Item.BatchId,
                    qtyBase = line.QtyBase,
                    refund = line.RefundPaisa,
                    restock = input.Restock ? 1 : 0,
                    reason = string.IsNullOrWhiteSpace(input.Reason) ? ReasonChangedMind : input.Reason.Trim(),
                    userId = user.Id,
                    at = stamp,
                    qcode,
                    disposition = input.Restock ? "restocked" : "quarantine",
                    qtyEntered = line.QtyEntered,
                    unitSold = line.Item.UnitSold,
                    note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
                    cash = cashShare,
                    credit = creditShare,
                    code
                });

            var id = (int)_db.NextId(connection);
            if (returnId == 0)
            {
                returnId = id;
            }

            if (input.Restock)
            {
                Restock(connection, line, code, user, stamp);
            }
        }

        // The khata shrinks first (15.2.1). The bill rows themselves are never edited.
        if (plan.CreditPaisa > 0 && plan.Sale.CustomerId is int customerId)
        {
            _db.Execute(connection,
                "UPDATE customers SET balance_paisa = MAX(0, balance_paisa - @amount) WHERE id = @id;",
                new { amount = plan.CreditPaisa, id = customerId });

            _audit.Record(connection, user, "Returns", "khata_reduced", "customers", customerId.ToString(),
                oldValue: plan.CreditPaisa.ToString(),
                details: $"{code}: {Money.Format(plan.CreditPaisa)} taken off the khata.");
        }

        // A refund must not buy free points (13.6): take back what this money earned. Done for any
        // bill of a known customer, whether the refund went on the khata or into the hand.
        if (plan.Sale.CustomerId is int loyaltyCustomer && _settings.GetBool("loyalty.enabled", false))
        {
            CustomerLedgerService.ReversePoints(_db, connection, _settings, loyaltyCustomer,
                plan.RefundPaisa, _settings.GetInt("loyalty.rupeesPerPoint", 100));
        }

        var qList = quarantineCodes.Count == 0 ? "none" : string.Join(" ", quarantineCodes);
        var mode = input.Restock ? "restock" : "quarantine";

        _audit.Record(connection, user, "Returns", "return_create", "sale_returns", returnId.ToString(),
            newValue: $"{code} against {plan.Sale.Code}: refund {Money.Format(plan.RefundPaisa)} " +
                      $"(cash {Money.Format(plan.CashPaisa)}, khata {Money.Format(plan.CreditPaisa)}), " +
                      $"{mode}, qcodes: {qList}");

        return ServiceResult<ReturnResult>.Success(new ReturnResult
        {
            ReturnId = returnId,
            Code = code,
            RefundPaisa = plan.RefundPaisa,
            CashPaisa = plan.CashPaisa,
            CreditReducedPaisa = plan.CreditPaisa,
            QuarantineCodes = quarantineCodes,
            Warnings = plan.Warnings
        }, plan.Warnings.ToArray());
    }

    private void Restock(SqliteConnection connection, PlannedLine line, string code, User user, string stamp)
    {
        if (line.Item.BatchId is int batchId)
        {
            _db.Execute(connection,
                "UPDATE batches SET qty_base = qty_base + @qty WHERE id = @id;",
                new { qty = line.QtyBase, id = batchId });

        }
        else
        {
            // An old bill may have no batch (sold before batches were tracked). The stock still has
            // to come back somewhere, so it goes to the newest open batch of that product if one
            // exists; otherwise the movement alone records the truth and the count stays honest.
            var fallback = _db.QuerySingle<int?>(connection, @"
SELECT id FROM batches WHERE product_id = @productId AND active = 1
ORDER BY (expiry_date IS NULL OR expiry_date = '') ASC, expiry_date DESC, id DESC LIMIT 1;",
                new { productId = line.Item.ProductId });

            if (fallback is int fallbackId)
            {
                _db.Execute(connection,
                    "UPDATE batches SET qty_base = qty_base + @qty WHERE id = @id;",
                    new { qty = line.QtyBase, id = fallbackId });
            }
        }

        _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, ref_type, ref_id, ref_code, note, user_id, created_at)
VALUES(@productId, @batchId, 'return_in', @qty, 'sale_return', @returnOf, @code, @note, @userId, @at);",
            new
            {
                productId = line.Item.ProductId,
                batchId = line.Item.BatchId,
                qty = line.QtyBase,
                returnOf = line.Item.SaleItemId,
                code,
                note = $"Return {code}",
                userId = user.Id,
                at = stamp
            });
    }

    // ----------------------------------------------------------------- planning

    /// <summary>
    /// The single calculation used by both the preview and the save, so the refund on the screen is
    /// the refund in the database.
    /// </summary>
    private ReturnPlan Plan(SqliteConnection connection, ReturnInput input, User user)
    {
        var sale = LoadSale(connection, input.SaleId);
        var warnings = new List<string>();

        if (!string.Equals(sale.Status, "paid", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sale.Status, "partial", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sale.Status, "credit", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("This bill cannot be returned against (it is void or still held).");
        }

        var maxDays = _settings.GetInt("returns.maxDays", 0);
        if (maxDays > 0 && DateTime.TryParse(sale.Date, out var soldOn)
            && (TimeSource.Now - soldOn).TotalDays > maxDays)
        {
            throw new ValidationException(
                $"This bill is {Math.Floor((TimeSource.Now - soldOn).TotalDays):0} days old; " +
                $"returns are allowed for {maxDays} days.");
        }

        var items = ReadItemRows(connection, sale);
        var byId = items.ToDictionary(item => item.SaleItemId);

        var lines = new List<PlannedLine>();
        var seen = new HashSet<int>();

        foreach (var wanted in input.Lines ?? new List<ReturnLineInput>())
        {
            if (wanted.QtyBase <= 0)
            {
                continue;
            }

            if (!byId.TryGetValue(wanted.SaleItemId, out var item))
            {
                throw new ValidationException("An item in this return is not on this bill at all.");
            }

            if (!seen.Add(item.SaleItemId))
            {
                throw new ValidationException($"{item.Name} is listed twice in the same return.");
            }

            if (wanted.QtyBase > item.RemainingBase + 0.0005)
            {
                throw new ValidationException(
                    $"{item.Name}: this bill had {Qty.Format(item.QtyEntered)} {item.UnitSold}, " +
                    $"{Qty.Format(item.AlreadyReturnedBase)} already returned - " +
                    $"now only {Qty.Format(item.RemainingUnits)} {item.UnitSold} can be returned.");
            }

            var wholeRemainder = Math.Abs(wanted.QtyBase - item.RemainingBase) < 0.0005;

            var refund = wholeRemainder
                ? item.RemainingPaisa
                : Calc.Round((double)item.RemainingPaisa * wanted.QtyBase / Math.Max(item.RemainingBase, 0.000001));

            refund = Math.Clamp(refund, 0L, item.RemainingPaisa);

            lines.Add(new PlannedLine
            {
                Item = item,
                QtyBase = wholeRemainder ? item.RemainingBase : wanted.QtyBase,
                QtyEntered = wholeRemainder
                    ? item.QtyEntered
                    : item.QtyBase <= 0 ? 0 : item.QtyEntered * wanted.QtyBase / item.QtyBase,
                RefundPaisa = refund
            });
        }

        if (lines.Count == 0)
        {
            throw new ValidationException("Choose at least one item and a quantity to return.");
        }

        var refundTotal = lines.Sum(line => line.RefundPaisa);

        // Never more money than the shop took for this bill, and the khata comes first.
        var creditCap = Math.Max(0, sale.DuePaisa - sale.CreditRefundedPaisa);
        var cashCap = Math.Max(0, sale.PaidPaisa - sale.CashRefundedPaisa);

        // A khata can only shrink if there is a customer to shrink it on. A bill with no customer
        // (walk-in) has nothing to write the debt off against, so the whole refund must be cash.
        var credit = sale.CustomerId is int ? Math.Min(creditCap, refundTotal) : 0L;
        var cash = refundTotal - credit;

        if (cash > cashCap)
        {
            throw new ValidationException(
                $"Only {Money.Format(cashCap)} of this bill was collected in cash and not yet refunded, " +
                $"so {Money.Format(refundTotal)} cannot be paid back.");
        }

        if (refundTotal == 0)
        {
            warnings.Add("The refund for these items is zero - they were free or fully discounted.");
        }

        return new ReturnPlan
        {
            Sale = sale,
            Lines = lines,
            RefundPaisa = refundTotal,
            CashPaisa = cash,
            CreditPaisa = credit,
            RefundablePaisa = cashCap,
            Warnings = warnings
        };
    }

    // -------------------------------------------------------------------- reads

    private SaleRow LoadSale(SqliteConnection connection, int saleId)
    {
        var sale = _db.QuerySingle<SaleRow>(connection, @"
SELECT s.id, s.code, s.date, s.customer_id AS customerId, s.total_paisa AS totalPaisa,
       s.paid_paisa AS paidPaisa, s.due_paisa AS duePaisa, s.status,
       COALESCE((SELECT SUM(r.cash_refund_paisa)   FROM sale_returns r WHERE r.sale_id = s.id), 0) AS cashRefundedPaisa,
       COALESCE((SELECT SUM(r.credit_reduced_paisa) FROM sale_returns r WHERE r.sale_id = s.id), 0) AS creditRefundedPaisa
FROM sales s WHERE s.id = @id;", new { id = saleId });

        if (sale is null)
        {
            throw new ValidationException("That bill was not found.");
        }

        return sale;
    }

    private List<ItemRow> ReadItemRows(SqliteConnection connection, SaleRow sale)
    {
        var returned = _db.Query<ReturnedRow>(connection, @"
SELECT sale_item_id AS saleItemId, SUM(qty_base) AS qty, SUM(refund_paisa) AS refund
FROM sale_returns WHERE sale_id = @id GROUP BY sale_item_id;", new { id = sale.Id })
            .ToDictionary(row => row.SaleItemId);

        var items = _db.Query<ItemRow>(connection, @"
SELECT id AS saleItemId, product_id AS productId, batch_id AS batchId, name_snapshot AS name,
       qty_base AS qtyBase, qty_entered AS qtyEntered, unit_sold AS unitSold, line_total_paisa AS lineTotalPaisa
FROM sale_items WHERE sale_id = @id ORDER BY id;", new { id = sale.Id });

        foreach (var item in items)
        {
            if (returned.TryGetValue(item.SaleItemId, out var row))
            {
                item.AlreadyReturnedBase = row.Qty;
                item.AlreadyRefundedPaisa = row.Refund;
            }
        }

        return items;
    }

    // -------------------------------------------------------------------- types

    private sealed class SaleRow
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string? Date { get; set; }

        public int? CustomerId { get; set; }

        public long TotalPaisa { get; set; }

        public long PaidPaisa { get; set; }

        public long DuePaisa { get; set; }

        public string? Status { get; set; }

        public long CashRefundedPaisa { get; set; }

        public long CreditRefundedPaisa { get; set; }
    }

    private sealed class ItemRow : ReturnLineOption
    {
        public int? BatchId { get; set; }
    }

    private sealed class ReturnedRow
    {
        public int SaleItemId { get; set; }

        public double Qty { get; set; }

        public long Refund { get; set; }
    }

    private sealed class PlannedLine
    {
        public ItemRow Item { get; init; } = new();

        public double QtyBase { get; init; }

        public double QtyEntered { get; init; }

        public long RefundPaisa { get; init; }
    }

    private sealed class ReturnPlan
    {
        public SaleRow Sale { get; init; } = new();

        public List<PlannedLine> Lines { get; init; } = new();

        public long RefundPaisa { get; init; }

        public long CashPaisa { get; init; }

        public long CreditPaisa { get; init; }

        public long RefundablePaisa { get; init; }

        public List<string> Warnings { get; init; } = new();
    }
}
