using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// The customer's khata book (Spec 17.3, 13.5, 5.1.11, F-27).
///
/// The owner's rule for the whole account is one sentence: **the balance changes only through bills,
/// payments and returns - never by hand.** This service therefore does two jobs:
///
///   1. it writes the one movement a person makes by hand - money received at the counter - and
///   2. it re-derives the balance from the history every time the page opens, so a balance that no
///      longer matches its own history is shown in red instead of being quietly trusted.
///
/// The running balance is rebuilt with the *same* clamps the app used when it wrote each row: a
/// credit bill adds, a collection subtracts (and an over-payment is refused, never clamped), and a
/// return subtracts but never below zero (`MAX(0, ...)` in ReturnService). Without that last clamp a
/// return against a small balance would make the audit say "mismatch" about a correct database.
///
/// Loyalty: points are earned on the bill total when `loyalty.enabled` is on, stars are 1-5, and the
/// fifth star makes a Normal customer VIP (`loyalty.vipThreshold`). Stars are only ever raised -
/// nobody's status is taken away by a return.
/// </summary>
public sealed class CustomerLedgerService
{
    public const int MaxStars = 5;

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public CustomerLedgerService(
        Database db,
        AuditService audit,
        SettingsService settings,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Collection methods the counter may use. "credit" is never one of them.</summary>
    public static readonly string[] CollectionMethods = { "cash", "online", "card" };

    /// <summary>The customer list: search by name or phone, optional "only with udhaar".</summary>
    public ServiceResult<List<CustomerSummary>> List(string? search = null, bool onlyCredit = false)
    {
        try
        {
            var term = (search ?? string.Empty).Trim();

            using var connection = _db.Open();

            // Every sum is CAST to INTEGER: `qty_base` is REAL (a strip can be half a box), so SUM
            // returns a floating point number, and a floating paisa must never reach a rupee field.
            // The search pattern is a parameter named `pattern`, not one named `like` - a keyword is
            // a poor name for a value, and one reader once mistook it for the operator.
            var rows = _db.Query<CustomerSummary>(connection, @"
SELECT c.id AS id, c.name AS name, c.phone AS phone, c.category AS category,
       c.credit_limit_paisa AS creditLimitPaisa, c.balance_paisa AS balancePaisa,
       IFNULL(c.opening_balance_paisa, 0) AS openingPaisa,
       c.loyalty_points AS loyaltyPoints, IFNULL(c.stars, 1) AS stars,
       (SELECT COUNT(1) FROM sales s WHERE s.customer_id = c.id AND s.status IN ('paid','partial','credit')) AS billsCount,
       CAST(IFNULL((SELECT SUM(s.total_paisa) FROM sales s
               WHERE s.customer_id = c.id AND s.status IN ('paid','partial','credit')), 0) AS INTEGER) AS lifetimePaisa,
       CAST(IFNULL((SELECT SUM(si.line_total_paisa - si.qty_base * si.cost_paisa_at_sale)
               FROM sale_items si JOIN sales s ON s.id = si.sale_id
               WHERE s.customer_id = c.id AND s.status IN ('paid','partial','credit')), 0) AS INTEGER) AS profitPaisa,
       CAST(IFNULL((SELECT SUM(r.refund_paisa) FROM sale_returns r
               JOIN sales rs ON rs.id = r.sale_id WHERE rs.customer_id = c.id), 0) AS INTEGER) AS returnsPaisa,
       (SELECT MAX(s.date) FROM sales s WHERE s.customer_id = c.id) AS lastBillDate
FROM customers c
WHERE c.active = 1
  AND (@term = '' OR c.name LIKE @pattern OR IFNULL(c.phone, '') LIKE @pattern)
  AND (@onlyCredit = 0 OR c.balance_paisa > 0)
ORDER BY c.balance_paisa DESC, c.name COLLATE NOCASE;",
                new { term, pattern = "%" + term + "%", onlyCredit = onlyCredit ? 1 : 0 });

            foreach (var row in rows)
            {
                row.Stars = StarsFor(row.LoyaltyPoints);
            }

            return ServiceResult<List<CustomerSummary>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The customer list could not be loaded.", exception);

            // The technical reason is shown, not hidden: "database is locked" tells the owner to close
            // the second copy of Area11, while a bare "could not be loaded" tells him nothing.
            return ServiceResult<List<CustomerSummary>>.Fail(
                "The customers could not be loaded. (" + exception.GetType().Name + ": " + exception.Message + ")");
        }
    }

    /// <summary>The whole page: the numbers, every movement, and the balance check.</summary>
    public ServiceResult<CustomerPage> Page(int customerId)
    {
        try
        {
            using var connection = _db.Open();

            var summary = _db.QuerySingle<CustomerSummary>(connection, @"
SELECT c.id AS id, c.name AS name, c.phone AS phone, c.category AS category,
       c.credit_limit_paisa AS creditLimitPaisa, c.balance_paisa AS balancePaisa,
       IFNULL(c.opening_balance_paisa, 0) AS openingPaisa,
       c.loyalty_points AS loyaltyPoints, IFNULL(c.stars, 1) AS stars,
       (SELECT COUNT(1) FROM sales s WHERE s.customer_id = c.id AND s.status IN ('paid','partial','credit')) AS billsCount,
       CAST(IFNULL((SELECT SUM(s.total_paisa) FROM sales s
               WHERE s.customer_id = c.id AND s.status IN ('paid','partial','credit')), 0) AS INTEGER) AS lifetimePaisa,
       CAST(IFNULL((SELECT SUM(si.line_total_paisa - si.qty_base * si.cost_paisa_at_sale)
               FROM sale_items si JOIN sales s ON s.id = si.sale_id
               WHERE s.customer_id = c.id AND s.status IN ('paid','partial','credit')), 0) AS INTEGER) AS profitPaisa,
       CAST(IFNULL((SELECT SUM(r.refund_paisa) FROM sale_returns r
               JOIN sales rs ON rs.id = r.sale_id WHERE rs.customer_id = c.id), 0) AS INTEGER) AS returnsPaisa,
       (SELECT MAX(s.date) FROM sales s WHERE s.customer_id = c.id) AS lastBillDate
FROM customers c WHERE c.id = @id;", new { id = customerId });

            if (summary is null)
            {
                return ServiceResult<CustomerPage>.Fail("That customer was not found.");
            }

            summary.Stars = StarsFor(summary.LoyaltyPoints);

            var movements = ReadMovements(connection, customerId);

            var page = new CustomerPage
            {
                Summary = summary,
                StoredPaisa = summary.BalancePaisa
            };

            // The khata does not begin at the first bill: a shop that moved to Area11 brought its
            // udhaar with it (F-37, migration 020). This line is that carried balance, and it is
            // counted - the same way the supplier page counts its opening - so the running total
            // and "Books match" stay true from the first day.
            var running = summary.OpeningPaisa;

            if (summary.OpeningPaisa != 0)
            {
                page.Lines.Add(new LedgerLine
                {
                    Date = string.Empty,
                    Kind = "opening",
                    Code = "OPEN",
                    Description = "Udhaar carried into Area11",
                    DebitPaisa = summary.OpeningPaisa > 0 ? summary.OpeningPaisa : 0,
                    CreditPaisa = summary.OpeningPaisa < 0 ? -summary.OpeningPaisa : 0,
                    RunningPaisa = running
                });
            }

            foreach (var movement in movements)
            {
                running += movement.DebitPaisa;
                running -= movement.CreditPaisa;

                // The same clamp ReturnService applies, and the same refusal Collect applies.
                if (running < 0 && movement.Kind == "return")
                {
                    running = 0;
                }

                movement.RunningPaisa = running;
                page.Lines.Add(movement);
            }

            page.ExpectedPaisa = running;

            return ServiceResult<CustomerPage>.Success(page);
        }
        catch (Exception exception)
        {
            _log.Error("The page of customer " + customerId + " could not be loaded.", exception);
            return ServiceResult<CustomerPage>.Fail(
                "The customer's account could not be loaded. (" + exception.GetType().Name + ": "
                + exception.Message + ")");
        }
    }

    /// <summary>
    /// Money received from a customer. Lowers the khata, puts cash in the drawer (13.5.1) and is
    /// written into `payments` with the customer and **no sale** - that is what makes it a collection
    /// rather than a sale payment.
    ///
    /// Paying more than is owed is refused: an advance would need its own rule (and its own line in
    /// the drawer), and a negative khata would break the one promise this page makes - that the
    /// balance is the history.
    /// </summary>
    public ServiceResult<CollectionResult> Collect(User? user, int customerId, long amountPaisa, string? method, string? note)
    {
        if (!_permissions.Can(user, Modules.Customers))
        {
            return ServiceResult<CollectionResult>.Fail("You do not have permission to receive customer money.");
        }

        if (amountPaisa <= 0)
        {
            return ServiceResult<CollectionResult>.Fail("The amount must be more than zero.");
        }

        var cleanMethod = (method ?? "cash").Trim().ToLowerInvariant();
        if (Array.IndexOf(CollectionMethods, cleanMethod) < 0)
        {
            return ServiceResult<CollectionResult>.Fail("Choose cash, online or card. Credit is not a way of receiving money.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var customer = _db.QuerySingle<BalanceRow>(connection,
                    "SELECT id, name, balance_paisa AS balancePaisa FROM customers WHERE id = @id AND active = 1;",
                    new { id = customerId })
                    ?? throw new ValidationException("That customer was not found.");

                if (customer.BalancePaisa <= 0)
                {
                    throw new ValidationException(customer.Name + " has nothing to pay - the khata is clear.");
                }

                if (amountPaisa > customer.BalancePaisa)
                {
                    throw new ValidationException(
                        customer.Name + " owes " + Money.Format(customer.BalancePaisa) + " only. "
                        + "Receiving " + Money.Format(amountPaisa) + " would turn the khata negative, so it is refused.");
                }

                var stamp = Clock.Now();
                var cleanNote = string.IsNullOrWhiteSpace(note) ? "Khata collection" : note!.Trim();

                _db.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, customer_id, user_id, note)
VALUES(@at, @method, @amount, @customerId, @userId, @note);",
                    new
                    {
                        at = stamp,
                        method = cleanMethod,
                        amount = amountPaisa,
                        customerId,
                        userId = user?.Id,
                        note = cleanNote
                    });

                var paymentId = (int)_db.NextId(connection);
                var newBalance = customer.BalancePaisa - amountPaisa;

                _db.Execute(connection, "UPDATE customers SET balance_paisa = @balance WHERE id = @id;",
                    new { balance = newBalance, id = customerId });

                _audit.Record(connection, user, "Customers", "customer_collect", "customers", customerId.ToString(),
                    oldValue: customer.BalancePaisa.ToString(CultureInfo.InvariantCulture),
                    newValue: newBalance.ToString(CultureInfo.InvariantCulture),
                    details: cleanMethod + " " + Money.Format(amountPaisa) + " - " + cleanNote);

                return ServiceResult<CollectionResult>.Success(new CollectionResult
                {
                    PaymentId = paymentId,
                    AmountPaisa = amountPaisa,
                    NewBalancePaisa = newBalance,
                    Code = "PAY-" + paymentId.ToString("D4", CultureInfo.InvariantCulture)
                });
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult<CollectionResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("A customer collection for " + customerId + " could not be saved.", exception);
            return ServiceResult<CollectionResult>.Fail("The money could not be recorded. Nothing was changed.");
        }
    }

    /// <summary>
    /// The paper the customer takes home: every movement and the closing balance. Printed by the same
    /// 58 / 80 mm pipeline as every other slip, so the shop needs no second printer.
    /// </summary>
    public ServiceResult<ReceiptDocument> Statement(int customerId)
    {
        var page = Page(customerId);

        if (!page.Ok || page.Value is null)
        {
            return ServiceResult<ReceiptDocument>.Fail(page.Error ?? "The statement could not be prepared.");
        }

        var data = page.Value;
        var width = _settings.GetInt("printer.width", 58) >= 80 ? 48 : 32;

        var document = new ReceiptDocument
        {
            Title = "Statement " + data.Summary.Name,
            Width = width,
            AutoCut = _settings.GetBool("printer.autoCut", true),
            Copies = 1
        };

        void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.Fit(text, width), align, bold, big));

        void Pair(string left, string right, bool bold = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.TwoColumns(left, right, width), ReceiptAlign.Left, bold));

        var shop = _settings.GetString("store.name");
        Add(string.IsNullOrWhiteSpace(shop) ? "Area11" : shop, ReceiptAlign.Center, bold: true);
        document.Lines.Add(ReceiptLine.Sep(width));
        Add("CUSTOMER STATEMENT", ReceiptAlign.Center, bold: true, big: true);

        Add(data.Summary.Name, bold: true);
        Pair(data.Summary.PhoneLabel, data.Summary.CategoryLabel);
        Pair("Bills: " + data.Summary.BillsCount, "Stars: " + data.Summary.StarsLabel);
        document.Lines.Add(ReceiptLine.Sep(width));

        foreach (var line in data.Lines)
        {
            var amount = line.DebitPaisa > 0
                ? "+" + Money.Fixed(line.DebitPaisa)
                : line.CreditPaisa > 0 ? "-" + Money.Fixed(line.CreditPaisa) : "-";

            Pair(line.SlipDate, amount + "  = " + Money.Fixed(line.RunningPaisa));
            Add("  " + ReceiptService.Fit(line.Code + " " + line.Description, width - 2));
        }

        document.Lines.Add(ReceiptLine.Sep(width));
        Pair("TOTAL UDHAAR", Money.Fixed(data.StoredPaisa), bold: true);
        Pair("Lifetime purchases", Money.Fixed(data.Summary.LifetimePaisa));
        Pair("Lifetime profit", Money.Fixed(data.Summary.ProfitPaisa));

        if (!data.BooksMatch)
        {
            Add("NOTE: the khata does not match its history.", ReceiptAlign.Center, bold: true);
        }

        document.Lines.Add(ReceiptLine.Sep(width));
        Add("Received by: ______________");
        Add("Signature");
        document.Lines.Add(ReceiptLine.Sep(width));

        var footer = _settings.GetString("store.footerNote");
        if (!string.IsNullOrWhiteSpace(footer))
        {
            Add(footer, ReceiptAlign.Center);
        }

        return ServiceResult<ReceiptDocument>.Success(document);
    }

    // ------------------------------------------------------------------ loyalty

    /// <summary>
    /// Stars 1-5 from points: the fifth step is reached exactly at `loyalty.vipThreshold`, so the
    /// number in Settings is the only place the owner has to change.
    /// </summary>
    public static int StarsFor(int points, int vipThreshold = 500)
    {
        var threshold = vipThreshold <= 0 ? 500 : vipThreshold;
        var step = Math.Max(1, threshold / MaxStars);
        var stars = 1 + Math.Max(0, points) / step;

        return Math.Clamp(stars, 1, MaxStars);
    }

    /// <summary>
    /// Points earned by a bill. Integer paisa divided by the rupee value of one point, so no money
    /// rounding is involved and nothing can drift.
    /// </summary>
    public static int PointsFor(long totalPaisa, int rupeesPerPoint = 100)
    {
        var perPoint = Math.Max(1, rupeesPerPoint) * 100L;

        return (int)Math.Min(int.MaxValue, Math.Max(0, totalPaisa) / perPoint);
    }

    /// <summary>
    /// Hands the earned points to the customer and raises the stars; the fifth star makes a Normal
    /// customer VIP. Called inside the sale's own transaction, so a rolled-back bill grants nothing.
    /// </summary>
    internal static void AwardPoints(
        Database db, SqliteConnection connection, SettingsService settings, User? user,
        int customerId, int points, AuditService? audit)
    {
        if (points <= 0)
        {
            return;
        }

        db.Execute(connection,
            "UPDATE customers SET loyalty_points = MAX(0, loyalty_points + @points) WHERE id = @id;",
            new { points, id = customerId });

        PromoteStars(db, connection, settings, user, customerId, audit);
    }

    /// <summary>Takes points back when money is returned, so a refund cannot buy free points.</summary>
    internal static void ReversePoints(
        Database db, SqliteConnection connection, SettingsService settings,
        int customerId, long refundPaisa, int rupeesPerPoint)
    {
        var points = PointsFor(refundPaisa, rupeesPerPoint);

        if (points <= 0)
        {
            return;
        }

        db.Execute(connection,
            "UPDATE customers SET loyalty_points = MAX(0, loyalty_points - @points) WHERE id = @id;",
            new { points, id = customerId });

        PromoteStars(db, connection, settings, null, customerId, null);
    }

    private static void PromoteStars(
        Database db, SqliteConnection connection, SettingsService settings, User? user,
        int customerId, AuditService? audit)
    {
        var row = db.QuerySingle<PointsRow>(connection, @"
SELECT loyalty_points AS points, category AS category, IFNULL(stars, 1) AS stars
FROM customers WHERE id = @id;", new { id = customerId });

        if (row is null)
        {
            return;
        }

        var stars = StarsFor(row.Points, settings.GetInt("loyalty.vipThreshold", 500));

        if (stars > row.Stars)
        {
            db.Execute(connection, "UPDATE customers SET stars = @stars WHERE id = @id;",
                new { stars, id = customerId });
        }

        var loyaltyOn = settings.GetBool("loyalty.enabled", false);

        // Only an upgrade, and only from Normal: a VIP the owner set by hand is never demoted.
        if (loyaltyOn && stars >= MaxStars && !string.Equals(row.Category, "vip", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(row.Category, "doctor", StringComparison.OrdinalIgnoreCase))
        {
            db.Execute(connection, "UPDATE customers SET category = 'vip' WHERE id = @id;", new { id = customerId });

            audit?.Record(connection, user, "Customers", "vip_promoted", "customers", customerId.ToString(),
                oldValue: row.Category, newValue: "vip");
        }
    }

    // -------------------------------------------------------------------- reads

    private List<LedgerLine> ReadMovements(SqliteConnection connection, int customerId)
    {
        // Three sources, one order. A credit bill adds to the khata; a collection and a return's
        // credit part take money off it. Cash bills are listed for the history but move nothing.
        var output = new List<LedgerLine>();

        // The aliases are the property names: Dapper matches a column to a property by name (case
        // and underscores ignored), so "debit" would never reach DebitPaisa and the page would
        // quietly show a balance of zero.
        var bills = _db.Query<MovementRow>(connection, @"
SELECT s.date AS date,
       CASE WHEN s.due_paisa > 0 THEN 'bill' ELSE 'cash_bill' END AS kind,
       s.code AS code, s.due_paisa AS debitPaisa, 0 AS creditPaisa,
       s.total_paisa AS totalPaisa
FROM sales s
WHERE s.customer_id = @id AND s.status IN ('paid', 'partial', 'credit')
ORDER BY s.date, s.id;", new { id = customerId });

        foreach (var bill in bills)
        {
            output.Add(new LedgerLine
            {
                Date = bill.Date ?? string.Empty,
                Kind = bill.Kind,
                Code = bill.Code,
                Description = bill.DebitPaisa > 0
                    ? "Bill " + Money.Format(bill.TotalPaisa) + ", udhaar " + Money.Format(bill.DebitPaisa)
                    : "Bill " + Money.Format(bill.TotalPaisa) + " paid",
                DebitPaisa = bill.DebitPaisa
            });
        }

        var collections = _db.Query<MovementRow>(connection, @"
SELECT p.date AS date, 'collection' AS kind,
       'PAY-' || printf('%04d', p.id) AS code,
       0 AS debitPaisa, p.amount_paisa AS creditPaisa,
       'Money received' AS note, p.method AS method
FROM payments p
WHERE p.customer_id = @id AND p.sale_id IS NULL AND p.purchase_id IS NULL
ORDER BY p.date, p.id;", new { id = customerId });

        foreach (var collection in collections)
        {
            output.Add(new LedgerLine
            {
                Date = collection.Date ?? string.Empty,
                Kind = "collection",
                Code = collection.Code,
                Description = "Money received (" + (collection.Method ?? "cash") + ")",
                CreditPaisa = collection.CreditPaisa
            });
        }

        var returns = _db.Query<MovementRow>(connection, @"
SELECT r.date AS date, 'return' AS kind, r.code AS code, 0 AS debitPaisa, r.credit_reduced_paisa AS creditPaisa,
       r.refund_paisa AS totalPaisa, r.qcode AS qcode
FROM sale_returns r
JOIN sales s ON s.id = r.sale_id
WHERE s.customer_id = @id AND r.credit_reduced_paisa > 0
ORDER BY r.date, r.id;", new { id = customerId });

        foreach (var ret in returns)
        {
            output.Add(new LedgerLine
            {
                Date = ret.Date ?? string.Empty,
                Kind = "return",
                Code = ret.Code,
                Description = "Return " + Money.Format(ret.TotalPaisa) + ", khata reduced",
                CreditPaisa = ret.CreditPaisa
            });
        }

        // Oldest first; a stable order matters because the running balance is rebuilt line by line.
        return output
            .OrderBy(line => line.Date, StringComparer.Ordinal)
            .ThenBy(line => line.Kind == "bill" || line.Kind == "cash_bill" ? 0 : 1)
            .ToList();
    }

    // -------------------------------------------------------------------- types

    private sealed class BalanceRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public long BalancePaisa { get; set; }
    }

    private sealed class PointsRow
    {
        public int Points { get; set; }

        public string? Category { get; set; }

        public int Stars { get; set; } = 1;
    }

    private sealed class MovementRow
    {
        public string? Date { get; set; }

        public string Kind { get; set; } = "bill";

        public string Code { get; set; } = string.Empty;

        public long DebitPaisa { get; set; }

        public long CreditPaisa { get; set; }

        public long TotalPaisa { get; set; }

        public string? Note { get; set; }

        public string? Method { get; set; }

        public string? QCode { get; set; }
    }
}
