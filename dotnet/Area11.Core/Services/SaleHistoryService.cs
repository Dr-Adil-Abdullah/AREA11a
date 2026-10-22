using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The sales history (F-52, Spec 12.3): find any bill, open it, and print it again.
///
/// Why a shop needs this and the counter does not: the counter only knows the cart in front of it. A
/// customer comes back for a copy, the accountant asks for last month, and a return has to be checked
/// against the bill it came from - and every one of those ends in paper. So the reprint is built by the
/// same <see cref="ReceiptService"/> that printed the original, from the stored rows, which is why a
/// reprint next month still says what the customer took home that day.
///
/// Why a reprint is written into the black box: a second copy of a bill is the easiest paper in the shop
/// to misuse. Recording who printed it, when, and how much it was for turns a quiet copy into a line the
/// owner can find (Spec 12.1.2).
/// </summary>
public sealed class SaleHistoryService
{
    /// <summary>The list stops here and says so; a shop that has more bills in range must narrow the dates.</summary>
    public const int MaxRows = 500;

    /// <summary>No dates typed means "the last 30 days" - the range the owner almost always wants.</summary>
    public const int DefaultDays = 30;

    private readonly Database _db;
    private readonly ReceiptService _receipts;
    private readonly ReturnReceiptService _returnReceipts;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public SaleHistoryService(
        Database db,
        ReceiptService receipts,
        ReturnReceiptService returnReceipts,
        AuditService audit,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _receipts = receipts;
        _returnReceipts = returnReceipts;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Looking at the shop's bills is a sales-counter right, not an owner-only one (Spec 12.3.3).</summary>
    public bool CanLook(User? user) => _permissions.Can(user, Modules.Sales);

    /// <summary>
    /// The bills in the range, newest first. A bad date is a refusal with a reason, never a silent "no
    /// bills" - the owner would think the shop had no sales that day.
    /// </summary>
    public ServiceResult<SaleHistoryPage> Search(User? user, SaleHistoryFilter? filter)
    {
        if (!CanLook(user))
        {
            return ServiceResult<SaleHistoryPage>.Fail(
                "Only a person who may look at the sales can open the bill history.");
        }

        var want = filter ?? new SaleHistoryFilter();
        var page = new SaleHistoryPage();

        var to = ParseDate(want.To) ?? DateTime.Today;
        var from = ParseDate(want.From) ?? to.AddDays(-DefaultDays);

        if (ParseDate(want.To) is null && (want.To ?? string.Empty).Trim().Length > 0)
        {
            page.FilterProblem = "The second date is not a date (write it as 2026-10-26).";
            return ServiceResult<SaleHistoryPage>.Success(page);
        }

        if (ParseDate(want.From) is null && (want.From ?? string.Empty).Trim().Length > 0)
        {
            page.FilterProblem = "The first date is not a date (write it as 2026-10-26).";
            return ServiceResult<SaleHistoryPage>.Success(page);
        }

        if (from > to)
        {
            page.FilterProblem = "The first date is after the second - swap them.";
            return ServiceResult<SaleHistoryPage>.Success(page);
        }

        try
        {
            var search = (want.Search ?? string.Empty).Trim();
            var query = search.Length == 0 ? string.Empty : "%" + search + "%";

            using var connection = _db.Open();

            var rows = _db.Query<SaleHistoryRow>(connection, @"
SELECT s.id AS id, s.code AS code, s.date AS date, s.status AS status,
       s.payment_method AS method, IFNULL(c.name, '') AS customer,
       s.subtotal_paisa AS subtotalPaisa, s.discount_paisa AS discountPaisa,
       s.tax_paisa AS taxPaisa, s.round_off_paisa AS roundOffPaisa,
       s.total_paisa AS totalPaisa, s.paid_paisa AS paidPaisa, s.due_paisa AS duePaisa,
       (SELECT COUNT(1) FROM sale_items si WHERE si.sale_id = s.id) AS items,
       (SELECT COUNT(1) FROM sale_returns r WHERE r.sale_id = s.id) AS returns
FROM sales s
LEFT JOIN customers c ON c.id = s.customer_id
WHERE date(s.date) >= date(@from) AND date(s.date) <= date(@to)
  AND (@method = '' OR s.payment_method = @method)
  AND (@status = '' OR s.status = @status)
  AND (@query = '' OR upper(s.code) LIKE upper(@query)
       OR upper(IFNULL(c.name, '')) LIKE upper(@query)
       OR IFNULL(c.phone, '') LIKE @query)
ORDER BY s.date DESC, s.id DESC
LIMIT 501;",
                new
                {
                    from = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    to = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    method = (want.Method ?? string.Empty).Trim(),
                    status = (want.Status ?? string.Empty).Trim(),
                    query
                });

            page.IsTruncated = rows.Count > MaxRows;

            foreach (var row in rows.Take(MaxRows))
            {
                page.Rows.Add(row);
            }

            return ServiceResult<SaleHistoryPage>.Success(page);
        }
        catch (Exception exception)
        {
            _log.Error("The bill history could not be read.", exception);

            return ServiceResult<SaleHistoryPage>.Fail(
                "The bill history could not be read. " + exception.Message);
        }
    }

    /// <summary>One bill with its lines, the money that came in, and the returns that hang from it.</summary>
    public ServiceResult<SaleHistoryDetail> Open(User? user, int saleId)
    {
        if (!CanLook(user))
        {
            return ServiceResult<SaleHistoryDetail>.Fail(
                "Only a person who may look at the sales can open a bill.");
        }

        try
        {
            using var connection = _db.Open();

            var header = _db.QuerySingle<SaleHistoryRow>(connection, @"
SELECT s.id AS id, s.code AS code, s.date AS date, s.status AS status,
       s.payment_method AS method, IFNULL(c.name, '') AS customer,
       s.subtotal_paisa AS subtotalPaisa, s.discount_paisa AS discountPaisa,
       s.tax_paisa AS taxPaisa, s.round_off_paisa AS roundOffPaisa,
       s.total_paisa AS totalPaisa, s.paid_paisa AS paidPaisa, s.due_paisa AS duePaisa,
       (SELECT COUNT(1) FROM sale_items si WHERE si.sale_id = s.id) AS items,
       (SELECT COUNT(1) FROM sale_returns r WHERE r.sale_id = s.id) AS returns
FROM sales s
LEFT JOIN customers c ON c.id = s.customer_id
WHERE s.id = @id;", new { id = saleId });

            if (header is null)
            {
                return ServiceResult<SaleHistoryDetail>.Fail("That bill is not in this shop's database.");
            }

            var detail = new SaleHistoryDetail { Sale = header };

            detail.Lines.AddRange(_db.Query<SaleHistoryLine>(connection, @"
SELECT si.id AS id, IFNULL(si.name_snapshot, '') AS name,
       si.qty_entered AS qtyEntered, IFNULL(si.unit_sold, 'base') AS unitSold,
       si.qty_base AS qtyBase, si.unit_price_paisa AS unitPricePaisa,
       si.discount_paisa AS discountPaisa, si.line_total_paisa AS lineTotalPaisa
FROM sale_items si
WHERE si.sale_id = @id
ORDER BY si.id;", new { id = saleId }));

            detail.Payments.AddRange(_db.Query<SaleHistoryPayment>(connection, @"
SELECT p.date AS date, p.method AS method, p.amount_paisa AS amountPaisa,
       IFNULL(p.note, '') AS note
FROM payments p
WHERE p.sale_id = @id
ORDER BY p.id;", new { id = saleId }));

            detail.Returns.AddRange(_db.Query<SaleHistoryReturn>(connection, @"
SELECT IFNULL(r.code, '') AS code, r.date AS date, r.refund_paisa AS refundPaisa,
       r.cash_refund_paisa AS cashPaisa, r.credit_reduced_paisa AS creditPaisa,
       r.qty_entered AS qtyEntered, IFNULL(r.unit_sold, 'base') AS unitSold,
       r.restock AS restock
FROM sale_returns r
WHERE r.sale_id = @id
ORDER BY r.id;", new { id = saleId }));

            return ServiceResult<SaleHistoryDetail>.Success(detail);
        }
        catch (Exception exception)
        {
            _log.Error($"The bill {saleId} could not be opened.", exception);

            return ServiceResult<SaleHistoryDetail>.Fail(
                "That bill could not be opened. " + exception.Message);
        }
    }

    /// <summary>The paper for a bill that was printed once already: the same builder, the same numbers.</summary>
    public ServiceResult<ReceiptDocument> BuildReceipt(User? user, int saleId)
    {
        if (!CanLook(user))
        {
            return ServiceResult<ReceiptDocument>.Fail(
                "Only a person who may look at the sales can print a bill again.");
        }

        return _receipts.Build(saleId);
    }

    /// <summary>The paper for a return, found from its number on the bill page (Spec 14.3.2).</summary>
    public ServiceResult<ReceiptDocument> BuildReturnReceipt(User? user, string? code)
    {
        if (!CanLook(user))
        {
            return ServiceResult<ReceiptDocument>.Fail(
                "Only a person who may look at the sales can print a return receipt again.");
        }

        return _returnReceipts.Build(code);
    }

    /// <summary>
    /// Called after the print really happened. The bill is named in the black box with its total, so a
    /// copy that appears in a customer's hand can always be traced to the minute it was printed.
    /// </summary>
    public ServiceResult RecordReprint(User? user, int saleId)
    {
        if (!CanLook(user))
        {
            return ServiceResult.Fail("Only a person who may look at the sales can print a bill again.");
        }

        try
        {
            using var connection = _db.Open();

            var row = _db.QuerySingle<SaleHistoryRow>(connection,
                "SELECT code AS code, total_paisa AS totalPaisa FROM sales WHERE id = @id;",
                new { id = saleId });

            if (row is null)
            {
                return ServiceResult.Fail("That bill is not in this shop's database.");
            }

            _audit.RecordStandalone(user, Modules.Sales, "sale_reprint",
                details: "Printed the bill " + row.Code + " again (" + Money.Format(row.TotalPaisa) + ").");

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"The reprint of the bill {saleId} could not be recorded.", exception);

            return ServiceResult.Fail("The reprint could not be recorded in the black box.");
        }
    }

    /// <summary>The same line for a return receipt, named by its own number.</summary>
    public ServiceResult RecordReturnReprint(User? user, string? code)
    {
        if (!CanLook(user))
        {
            return ServiceResult.Fail(
                "Only a person who may look at the sales can print a return receipt again.");
        }

        var clean = (code ?? string.Empty).Trim();

        if (clean.Length == 0)
        {
            return ServiceResult.Fail("There is no return number to record.");
        }

        try
        {
            _audit.RecordStandalone(user, Modules.Sales, "return_reprint",
                details: "Printed the return receipt " + clean + " again.");

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"The reprint of the return {clean} could not be recorded.", exception);

            return ServiceResult.Fail("The reprint could not be recorded in the black box.");
        }
    }

    /// <summary>yyyy-MM-dd exactly; anything else is null, and the caller turns that into a sentence.</summary>
    private static DateTime? ParseDate(string? text)
    {
        var clean = (text ?? string.Empty).Trim();

        if (clean.Length == 0)
        {
            return null;
        }

        return DateTime.TryParseExact(clean, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}
