using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// "Do the books add up?" (F-54) - the check that reads the shop's own rows and compares every
/// number a document *claims* with the rows that were written underneath it.
///
/// Why this exists: the owner's standing order is that **no error may remain, because it is money**.
/// A pharmacy's books can go wrong in exactly one way that a screen cannot show - a total that no
/// longer matches its lines - and every one of those faults is silent: WPF draws the number, the
/// report sums the number, and the receipt was printed from the number. Nobody notices until the
/// drawer and the report disagree at the end of the month.
///
/// **What this service is not:** it is not a repair. It never writes a row, never recalculates, and
/// never "fixes" a total - a program that silently corrects money is worse than one that says
/// nothing. It reads, it compares, and it names the document. The rules are the ones this program
/// itself guarantees by construction (`SaleService.Compute` spreads the tax, the round-off and the
/// points over the lines so their sum IS the total; `PurchaseService` writes the header from the
/// sum of its lines), so a finding here means one of two things: a row was written by something
/// other than this program (an old import, a hand-edited file), or a genuine bug reached the
/// database - which is exactly what the owner wants to hear about the same day, not at month end.
///
/// **Why each rule is exact and not an estimate:** a check that cries wolf is worse than no check,
/// so no rule compares two definitions that merely usually agree. Each one is an identity the
/// program guarantees: lines → total, header → total, paid + due → total, money rows → paid,
/// khata ≥ 0. Anything the program deliberately leaves open (a supplier's credit note may stand in
/// the shop's favour, a shelf may go minus and warn) is **not** a rule here.
/// </summary>
public sealed class BookCheckService
{
    /// <summary>How many findings one rule shows before it says "and more".</summary>
    private const int MaxFindings = 200;

    private readonly Database _db;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public BookCheckService(Database db, PermissionService permissions, LogService log)
    {
        _db = db;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>
    /// May this person open the page? The menu asks this, and <see cref="Run"/> asks the same
    /// question - one rule, so a menu row can never point at a page that refuses to open.
    ///
    /// It is the reports/black-box permission, not the till: a cashier has no business reading the
    /// arithmetic of the whole shop.
    /// </summary>
    public bool CanRun(User? user) =>
        _permissions.Can(user, Modules.Reports) || _permissions.Can(user, Modules.Audit);

    /// <summary>
    /// Runs every rule once, in the order a shopkeeper would ask them: bills first (that is where
    /// the money comes in), then purchases (where it goes out), then the khata (who owes), then the
    /// money rows themselves.
    /// </summary>
    public ServiceResult<BookCheckReport> Run(User? user)
    {
        if (!CanRun(user))
        {
            return ServiceResult<BookCheckReport>.Fail(
                "You do not have permission to check the books (Reports or Black box).");
        }

        try
        {
            using var connection = _db.Open();

            var rules = new List<BookCheckRule>
            {
                BillLines(connection),
                BillHeader(connection),
                BillPaidAndDue(connection),
                BillMoneyRows(connection),
                BillImpossibleMoney(connection),
                PurchaseLines(connection),
                PurchaseHeader(connection),
                PurchasePaidAndDue(connection),
                PurchaseMoneyRows(connection),
                KhataNotNegative(connection),
                MoneyRowsBelong(connection)
            };

            var report = new BookCheckReport
            {
                RanAt = TimeSource.Now,
                DatabaseFile = AppPaths.DatabaseFile,
                Rules = rules
            };

            _log.Info("Books check: " + rules.Count + " rule(s), " + report.Problems + " fault(s).");

            return ServiceResult<BookCheckReport>.Success(report);
        }
        catch (Exception exception)
        {
            _log.Error("Books check failed: " + exception.Message);

            return ServiceResult<BookCheckReport>.Fail(
                "The check could not read the database: " + exception.Message);
        }
    }

    // -----------------------------------------------------------------------
    // The rules. One method per rule, each with the sentence that explains it.
    // -----------------------------------------------------------------------

    /// <summary>Rule 1: the lines of a bill add up to the bill's total.</summary>
    private BookCheckRule BillLines(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM sales WHERE status IN ('paid','partial','credit');") ?? 0);

        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT s.code AS Code, s.total_paisa AS ExpectedPaisa,
       IFNULL(SUM(si.line_total_paisa), 0) AS FoundPaisa
FROM sales s
LEFT JOIN sale_items si ON si.sale_id = s.id
WHERE s.status IN ('paid','partial','credit')
GROUP BY s.id, s.code, s.total_paisa
HAVING IFNULL(SUM(si.line_total_paisa), 0) <> s.total_paisa
ORDER BY s.id
LIMIT 201;"));

        return Rule("A bill's lines add up to the bill",
            "Every bill is the sum of its lines; the receipt, the report and the drawer all read this total.",
            checkedRows, findings);
    }

    /// <summary>Rule 2: the bill's own header adds up (subtotal, discount, tax, round-off, points).</summary>
    private BookCheckRule BillHeader(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM sales WHERE status IN ('paid','partial','credit');") ?? 0);

        // Loyalty points spent at the counter are a NEGATIVE payment row (`loyalty`), and the total
        // was reduced by them before the lines were spread. So they belong on the left of the sum.
        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT s.code AS Code, s.total_paisa AS ExpectedPaisa,
       (s.subtotal_paisa - s.discount_paisa + s.tax_paisa + s.round_off_paisa
        + IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
                  WHERE p.sale_id = s.id AND p.method = 'loyalty'), 0)) AS FoundPaisa
FROM sales s
WHERE s.status IN ('paid','partial','credit')
  AND s.total_paisa <> (s.subtotal_paisa - s.discount_paisa + s.tax_paisa + s.round_off_paisa
        + IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
                  WHERE p.sale_id = s.id AND p.method = 'loyalty'), 0))
ORDER BY s.id
LIMIT 201;"));

        return Rule("A bill's header adds up",
            "Subtotal minus discount plus tax plus round-off (and less the points used) is the printed total.",
            checkedRows, findings);
    }

    /// <summary>Rule 3: what was paid plus what is owed equals the bill.</summary>
    private BookCheckRule BillPaidAndDue(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM sales WHERE status IN ('paid','partial','credit');") ?? 0);

        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT s.code AS Code, s.total_paisa AS ExpectedPaisa,
       (s.paid_paisa + s.due_paisa) AS FoundPaisa
FROM sales s
WHERE s.status IN ('paid','partial','credit')
  AND s.paid_paisa + s.due_paisa <> s.total_paisa
ORDER BY s.id
LIMIT 201;"));

        return Rule("Every bill is either paid or owed",
            "Paid plus khata is the total; a rupee that is neither was paid nor added to a customer's debt is lost.",
            checkedRows, findings);
    }

    /// <summary>Rule 4: the money rows of a bill add up to the bill's "paid" figure.</summary>
    private BookCheckRule BillMoneyRows(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM sales WHERE status IN ('paid','partial','credit');") ?? 0);

        // The loyalty row is the only money row that is not cash in the drawer - points are a
        // discount, not a payment - so it is excluded here and counted by rule 2 instead.
        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT s.code AS Code, s.paid_paisa AS ExpectedPaisa,
       IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
               WHERE p.sale_id = s.id AND p.method <> 'loyalty'), 0) AS FoundPaisa
FROM sales s
WHERE s.status IN ('paid','partial','credit')
  AND s.paid_paisa <> IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
                              WHERE p.sale_id = s.id AND p.method <> 'loyalty'), 0)
ORDER BY s.id
LIMIT 201;"));

        return Rule("A bill's money rows match what it says was paid",
            "Cash, online and card rows are the drawer's evidence; if they differ from the bill, one of the two is wrong.",
            checkedRows, findings);
    }

    /// <summary>Rule 5: no bill carries impossible money.</summary>
    private BookCheckRule BillImpossibleMoney(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM sales WHERE status IN ('paid','partial','credit');") ?? 0);

        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT s.code AS Code, 0 AS ExpectedPaisa, s.total_paisa AS FoundPaisa
FROM sales s
WHERE s.status IN ('paid','partial','credit')
  AND (s.total_paisa < 0 OR s.paid_paisa < 0 OR s.due_paisa < 0
       OR s.subtotal_paisa < 0 OR s.discount_paisa < 0
       OR s.discount_paisa > s.subtotal_paisa)
ORDER BY s.id
LIMIT 201;"));

        return Rule("No bill carries impossible money",
            "A negative total, a negative payment, or a discount bigger than the goods is never something this program writes.",
            checkedRows, findings);
    }

    /// <summary>Rule 6: the lines of a purchase add up to its subtotal.</summary>
    private BookCheckRule PurchaseLines(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM purchases;") ?? 0);

        // Compared with the SUBTOTAL, not the total: an old database may carry a supplier discount
        // between the two, and a rule that calls a legitimate discount a fault would cry wolf on the
        // first shop that opens a file written by the original program.
        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT p.code AS Code, p.subtotal_paisa AS ExpectedPaisa,
       IFNULL(SUM(pi.line_total_paisa), 0) AS FoundPaisa
FROM purchases p
LEFT JOIN purchase_items pi ON pi.purchase_id = p.id
GROUP BY p.id, p.code, p.subtotal_paisa
HAVING IFNULL(SUM(pi.line_total_paisa), 0) <> p.subtotal_paisa
ORDER BY p.id
LIMIT 201;"));

        return Rule("A purchase's lines add up to its subtotal",
            "A supplier bill is the cost of the stock that entered; the payable and every cost report come from it.",
            checkedRows, findings);
    }

    /// <summary>Rule 7: a purchase's total is its subtotal less the supplier's discount.</summary>
    private BookCheckRule PurchaseHeader(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM purchases;") ?? 0);

        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT p.code AS Code, (p.subtotal_paisa - p.discount_paisa) AS ExpectedPaisa,
       p.total_paisa AS FoundPaisa
FROM purchases p
WHERE p.total_paisa <> p.subtotal_paisa - p.discount_paisa
   OR p.subtotal_paisa < 0 OR p.discount_paisa < 0
ORDER BY p.id
LIMIT 201;"));

        return Rule("A purchase's header adds up",
            "Subtotal minus the supplier's discount is what the shop owes for that delivery.",
            checkedRows, findings);
    }

    /// <summary>Rule 8: a purchase's paid plus due equals its total.</summary>
    private BookCheckRule PurchasePaidAndDue(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM purchases;") ?? 0);

        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT p.code AS Code, p.total_paisa AS ExpectedPaisa,
       (p.paid_paisa + p.due_paisa) AS FoundPaisa
FROM purchases p
WHERE p.paid_paisa + p.due_paisa <> p.total_paisa
   OR p.paid_paisa < 0 OR p.due_paisa < 0
ORDER BY p.id
LIMIT 201;"));

        return Rule("A purchase is either paid or owed to the supplier",
            "Paid plus due is the supplier bill; the difference is the payable shown on the Suppliers page.",
            checkedRows, findings);
    }

    /// <summary>Rule 9: the money rows of a purchase add up to what the purchase says was paid.</summary>
    private BookCheckRule PurchaseMoneyRows(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM purchases;") ?? 0);

        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT p.code AS Code, p.paid_paisa AS ExpectedPaisa,
       IFNULL((SELECT SUM(pm.amount_paisa) FROM payments pm
               WHERE pm.purchase_id = p.id), 0) AS FoundPaisa
FROM purchases p
WHERE p.paid_paisa <> IFNULL((SELECT SUM(pm.amount_paisa) FROM payments pm
                              WHERE pm.purchase_id = p.id), 0)
ORDER BY p.id
LIMIT 201;"));

        return Rule("A purchase's money rows match what it says was paid",
            "The cash paid to a supplier at delivery is one row; the supplier's ledger and the drawer both read it.",
            checkedRows, findings);
    }

    /// <summary>Rule 10: no customer's khata is below zero.</summary>
    private BookCheckRule KhataNotNegative(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM customers WHERE active = 1;") ?? 0);

        // A negative balance is only impossible from a standing start at zero: an imported customer
        // whose opening khata was already a credit (a negative opening) legitimately stays in the
        // shop's favour, and calling that a fault would blame the shop for its own import.
        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT c.name AS Code, 0 AS ExpectedPaisa, c.balance_paisa AS FoundPaisa
FROM customers c
WHERE c.active = 1 AND c.balance_paisa < 0 AND IFNULL(c.opening_balance_paisa, 0) >= 0
ORDER BY c.id
LIMIT 201;"));

        return Rule("No customer is owed money by themselves",
            "A khata that started at zero can never go below it: every collection refuses to take more than is owed, so a minus is a row written from outside.",
            checkedRows, findings);
    }

    /// <summary>Rule 11: every money row belongs to a bill, purchase, customer, supplier or no-bill return.</summary>
    private BookCheckRule MoneyRowsBelong(SqliteConnection connection)
    {
        var checkedRows = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM payments;") ?? 0);

        var findings = ToFindings(_db.Query<CheckRow>(connection, @"
SELECT ('PAY-' || p.id) AS Code, 0 AS ExpectedPaisa, p.amount_paisa AS FoundPaisa
FROM payments p
WHERE IFNULL(p.sale_id, 0) = 0
  AND IFNULL(p.purchase_id, 0) = 0
  AND IFNULL(p.customer_id, 0) = 0
  AND IFNULL(p.supplier_id, 0) = 0
  AND IFNULL(p.provisional_return_id, 0) = 0
ORDER BY p.id
LIMIT 201;"));

        return Rule("Every rupee received belongs to someone",
            "A money row with no bill, purchase, customer, supplier or no-bill return is money that no page can explain.",
            checkedRows, findings);
    }

    // -----------------------------------------------------------------------
    // Plumbing
    // -----------------------------------------------------------------------

    private static BookCheckRule Rule(string title, string why, int checkedRows, List<BookCheckFinding> findings)
    {
        var trimmed = findings.Count > MaxFindings ? findings.Take(MaxFindings).ToList() : findings;

        return new BookCheckRule
        {
            Title = title,
            Why = why,
            Checked = checkedRows,
            Findings = trimmed,
            Truncated = findings.Count > MaxFindings
        };
    }

    /// <summary>
    /// Turns the raw numbers into what the page prints. The mapping is written here, once, so a
    /// column that Dapper could not match shows up as an empty name rather than a silent zero.
    /// </summary>
    private static List<BookCheckFinding> ToFindings(List<CheckRow> rows) =>
        rows.Select(row => new BookCheckFinding
        {
            Code = row.Code ?? string.Empty,
            ExpectedPaisa = row.ExpectedPaisa,
            FoundPaisa = row.FoundPaisa
        }).ToList();

    /// <summary>One finding's raw numbers. Named to match the SELECT aliases exactly, or Dapper writes 0.</summary>
    private sealed class CheckRow
    {
        public string? Code { get; set; }

        public long ExpectedPaisa { get; set; }

        public long FoundPaisa { get; set; }
    }
}
