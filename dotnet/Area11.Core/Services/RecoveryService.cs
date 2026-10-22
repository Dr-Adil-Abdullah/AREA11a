using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Old udhaar: who still owes, how old the debt is, and the slip handed over when it is paid (F-33).
///
/// The owner's question is one sentence - **"whose money is oldest?"** - so this service answers it
/// with a real ageing, not an estimate:
///
///   • every credit bill puts its unpaid part (`due_paisa`) on the customer's side;
///   • every khata collection and every return that reduced the khata takes money off;
///   • each rupee of payment is applied to the **oldest unpaid bill first**, the way a shopkeeper
///     with a notebook works through a page;
///   • what is left on a bill keeps that bill's date, so the 90+ column holds money that has really
///     been owed for three months.
///
/// The total of the four columns must equal the stored khata for every customer. When it does not,
/// the row is flagged instead of being quietly trusted - the same promise the khata page makes.
///
/// **Numbering:** the slip is `PAY-nnnn`, the permanent number a collection already receives. The
/// roadmap called this feature "SR recovery bills", but `SR-` is the supplier return's number
/// (Spec 5.1.12) and two documents may never share a prefix - corrected in D-43.
/// </summary>
public sealed class RecoveryService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public RecoveryService(Database db, AuditService audit, SettingsService settings,
        PermissionService permissions, LogService log)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>
    /// The recovery list, oldest debt first.
    ///
    /// Reading it needs the **customers** permission - the same one that opens the khata page - because
    /// the person who takes the money at the counter must be able to find the customer. A cashier has
    /// that by default; a shop that does not want it can switch it off per person (D-43).
    /// </summary>
    public ServiceResult<RecoveryReport> Report(User? user, DateTime? today = null)
    {
        if (!_permissions.Can(user, Modules.Customers))
        {
            return ServiceResult<RecoveryReport>.Fail("You do not have permission to see the udhaar list.");
        }

        try
        {
            using var connection = _db.Open();

            return ServiceResult<RecoveryReport>.Success(Build(connection, (today ?? DateTime.Now).Date));
        }
        catch (Exception exception)
        {
            _log.Error("The udhaar recovery list could not be built.", exception);
            return ServiceResult<RecoveryReport>.Fail("The udhaar list could not be built.");
        }
    }

    /// <summary>True when nobody owes anything - the screen shows a friendly line instead of an empty grid.</summary>
    public ServiceResult<bool> NothingOwed(User? user)
    {
        var report = Report(user);

        return report.Ok && report.Value is not null
            ? ServiceResult<bool>.Success(report.Value.CustomersCount == 0)
            : ServiceResult<bool>.Fail(report.Error ?? "The udhaar list could not be built.");
    }

    // ------------------------------------------------------------------ ageing

    private RecoveryReport Build(SqliteConnection connection, DateTime today)
    {
        var customers = _db.Query<CustomerRow>(connection, @"
SELECT c.id AS customerId, c.name AS name, c.phone AS phone, c.category AS category,
       c.balance_paisa AS balancePaisa
FROM customers c
WHERE c.active = 1 AND c.balance_paisa > 0
ORDER BY c.name COLLATE NOCASE;");

        // Every credit bill of every customer who still owes, oldest first. One query, not one per
        // customer: the screen opens on every visit and must not take a second per row.
        var bills = _db.Query<BillRow>(connection, @"
SELECT s.customer_id AS customerId, s.id AS id, s.code AS code, s.date AS date, s.due_paisa AS duePaisa
FROM sales s
JOIN customers c ON c.id = s.customer_id AND c.active = 1 AND c.balance_paisa > 0
WHERE s.status IN ('paid', 'partial', 'credit') AND s.due_paisa > 0
ORDER BY s.customer_id, s.date, s.id;");

        // The only two things that take money off a khata (5.1.11): a collection and a return's
        // credit part. A payment written at the till against a sale is already inside due_paisa,
        // so counting it here would subtract the same rupee twice.
        var credits = _db.Query<CreditRow>(connection, @"
SELECT customerId, id, date, amountPaisa, kind FROM (
    SELECT p.customer_id AS customerId, p.id AS id, p.date AS date, p.amount_paisa AS amountPaisa,
           'collection' AS kind
    FROM payments p
    JOIN customers c ON c.id = p.customer_id AND c.active = 1 AND c.balance_paisa > 0
    WHERE p.customer_id IS NOT NULL AND p.sale_id IS NULL AND p.purchase_id IS NULL
    UNION ALL
    SELECT s.customer_id AS customerId, r.id AS id, r.date AS date, r.credit_reduced_paisa AS amountPaisa,
           'return' AS kind
    FROM sale_returns r
    JOIN sales s ON s.id = r.sale_id
    JOIN customers c ON c.id = s.customer_id AND c.active = 1 AND c.balance_paisa > 0
    WHERE r.credit_reduced_paisa > 0
)
ORDER BY customerId, date, id;");

        var report = new RecoveryReport();

        var billsByCustomer = bills.GroupBy(row => row.CustomerId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var creditsByCustomer = credits.GroupBy(row => row.CustomerId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var customer in customers)
        {
            var open = (billsByCustomer.TryGetValue(customer.CustomerId, out var hisBills) ? hisBills : new List<BillRow>())
                .Select(bill => new OpenBill
                {
                    Code = bill.Code ?? string.Empty,
                    Date = bill.Date ?? string.Empty,
                    Left = bill.DuePaisa
                })
                .ToList();

            var hisCredits = creditsByCustomer.TryGetValue(customer.CustomerId, out var found)
                ? found
                : new List<CreditRow>();

            foreach (var credit in hisCredits)
            {
                Allocate(open, credit.AmountPaisa);
            }

            var row = new RecoveryRow
            {
                CustomerId = customer.CustomerId,
                Name = customer.Name,
                Phone = customer.Phone,
                Category = customer.Category,
                BalancePaisa = customer.BalancePaisa,
                LastPaymentDate = hisCredits
                    .Where(credit => credit.Kind == "collection")
                    .Select(credit => credit.Date)
                    .LastOrDefault()
            };

            foreach (var bill in open.Where(bill => bill.Left > 0))
            {
                var days = Ageing.AgeInDays(bill.Date, today);

                if (days > row.OldestDays)
                {
                    row.OldestDays = days;
                    row.OldestBillCode = bill.Code;
                }

                switch (Ageing.Bucket(days))
                {
                    case 0: row.Bucket0Paisa += bill.Left; break;
                    case 1: row.Bucket1Paisa += bill.Left; break;
                    case 2: row.Bucket2Paisa += bill.Left; break;
                    default: row.Bucket3Paisa += bill.Left; break;
                }
            }

            // A balance with no open bill means the history and the stored number disagree; the
            // row still appears (the owner must see his money) and BooksMatch turns red.
            report.Rows.Add(row);

            report.Bucket0Paisa += row.Bucket0Paisa;
            report.Bucket1Paisa += row.Bucket1Paisa;
            report.Bucket2Paisa += row.Bucket2Paisa;
            report.Bucket3Paisa += row.Bucket3Paisa;
        }

        // Oldest first: that is the order in which money should be chased. Newest debt last, and
        // the name breaks the tie so two customers of the same age never swap places between reads.
        report.Rows.Sort((left, right) =>
        {
            var byAge = right.OldestDays.CompareTo(left.OldestDays);
            if (byAge != 0)
            {
                return byAge;
            }

            var byMoney = right.BalancePaisa.CompareTo(left.BalancePaisa);

            return byMoney != 0
                ? byMoney
                : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        return report;
    }

    /// <summary>Applies one payment to the oldest unpaid bills, for as long as the money lasts.</summary>
    private static void Allocate(List<OpenBill> bills, long amountPaisa)
    {
        var left = amountPaisa;

        foreach (var bill in bills)
        {
            if (left <= 0)
            {
                return;
            }

            if (bill.Left <= 0)
            {
                continue;
            }

            var take = Math.Min(left, bill.Left);

            bill.Left -= take;
            left -= take;
        }

        // Money beyond the oldest debt is not invented onto a new bill; the khata cannot go below
        // zero (Collect refuses an over-payment and ReturnService clamps), so the rest is dropped.
    }

    // ------------------------------------------------------------------ the slip

    /// <summary>
    /// The paper for one collection: what was owed, what was paid, what is left, and which bills
    /// the money cleared. Rebuilt from the history, so a slip reprinted next month says the same
    /// numbers as the one the customer took home.
    /// </summary>
    public ServiceResult<RecoverySlip> Slip(int paymentId, User? user)
    {
        if (!_permissions.Can(user, Modules.Customers))
        {
            return ServiceResult<RecoverySlip>.Fail("You do not have permission to print a recovery slip.");
        }

        if (paymentId <= 0)
        {
            return ServiceResult<RecoverySlip>.Fail("Pick a collection first.");
        }

        try
        {
            using var connection = _db.Open();

            var payment = _db.QuerySingle<PaymentRow>(connection, @"
SELECT p.id AS id, p.date AS date, p.customer_id AS customerId, p.amount_paisa AS amountPaisa,
       p.method AS method, p.note AS note, IFNULL(u.name, '') AS userName
FROM payments p
LEFT JOIN users u ON u.id = p.user_id
WHERE p.id = @id AND p.customer_id IS NOT NULL AND p.sale_id IS NULL AND p.purchase_id IS NULL;",
                new { id = paymentId });

            if (payment is null)
            {
                return ServiceResult<RecoverySlip>.Fail("That collection was not found.");
            }

            var customer = _db.QuerySingle<CustomerRow>(connection,
                "SELECT id AS customerId, name AS name, phone AS phone, category AS category, balance_paisa AS balancePaisa FROM customers WHERE id = @id;",
                new { id = payment.CustomerId });

            if (customer is null)
            {
                return ServiceResult<RecoverySlip>.Fail("The customer of that collection was not found.");
            }

            // Replay this customer's history exactly as the khata page does, stopping at the target
            // payment: before = what he owed on that day, after = what he owed an instant later.
            var bills = _db.Query<BillRow>(connection, @"
SELECT s.customer_id AS customerId, s.id AS id, s.code AS code, s.date AS date, s.due_paisa AS duePaisa
FROM sales s
WHERE s.customer_id = @id AND s.status IN ('paid', 'partial', 'credit') AND s.due_paisa > 0
ORDER BY s.date, s.id;", new { id = payment.CustomerId });

            var credits = _db.Query<CreditRow>(connection, @"
SELECT customerId, id, date, amountPaisa, kind FROM (
        SELECT p.customer_id AS customerId, p.id AS id, p.date AS date, p.amount_paisa AS amountPaisa,
               'collection' AS kind
        FROM payments p
        WHERE p.customer_id = @id AND p.sale_id IS NULL AND p.purchase_id IS NULL
        UNION ALL
        SELECT s.customer_id AS customerId, r.id AS id, r.date AS date, r.credit_reduced_paisa AS amountPaisa,
               'return' AS kind
        FROM sale_returns r
        JOIN sales s ON s.id = r.sale_id
        WHERE s.customer_id = @id AND r.credit_reduced_paisa > 0
    )
    ORDER BY date, id;", new { id = payment.CustomerId });

            var open = bills.Select(bill => new OpenBill
            {
                Code = bill.Code ?? string.Empty,
                Date = bill.Date ?? string.Empty,
                Left = bill.DuePaisa
            }).ToList();

            var slip = new RecoverySlip
            {
                PaymentId = payment.Id,
                Code = "PAY-" + payment.Id.ToString("D4", CultureInfo.InvariantCulture),
                Date = payment.Date,
                CustomerName = customer.Name,
                Phone = customer.Phone,
                ReceivedPaisa = payment.AmountPaisa,
                Method = payment.Method ?? "cash",
                Note = payment.Note,
                ReceivedBy = payment.UserName
            };

            var reached = false;

            foreach (var credit in credits)
            {
                if (credit.Kind == "collection" && credit.Id == payment.Id)
                {
                    // What he owed a moment before the money was handed over.
                    slip.BalanceBeforePaisa = open.Sum(bill => bill.Left);

                    // The same oldest-first walk as Allocate, keeping a note of every bill touched:
                    // the paper must be able to show where the money went.
                    var left = payment.AmountPaisa;

                    foreach (var bill in open)
                    {
                        if (left <= 0)
                        {
                            break;
                        }

                        if (bill.Left <= 0)
                        {
                            continue;
                        }

                        var take = Math.Min(left, bill.Left);

                        bill.Left -= take;
                        left -= take;

                        slip.Hits.Add(new RecoveryHit
                        {
                            BillCode = bill.Code,
                            BillDate = bill.Date,
                            PaidPaisa = take,
                            LeftPaisa = bill.Left
                        });
                    }

                    slip.BalanceAfterPaisa = Math.Max(0, slip.BalanceBeforePaisa - payment.AmountPaisa);
                    reached = true;

                    break;
                }

                Allocate(open, credit.AmountPaisa);
            }

            if (!reached)
            {
                return ServiceResult<RecoverySlip>.Fail("That collection is not in the customer's history.");
            }

            return ServiceResult<RecoverySlip>.Success(slip);
        }
        catch (Exception exception)
        {
            _log.Error("The recovery slip of payment " + paymentId + " could not be built.", exception);
            return ServiceResult<RecoverySlip>.Fail("The recovery slip could not be built.");
        }
    }

    /// <summary>Turns the slip into the 58 / 80 mm paper the shop already prints on.</summary>
    public ServiceResult<ReceiptDocument> SlipDocument(int paymentId, User? user)
    {
        var slip = Slip(paymentId, user);

        if (!slip.Ok || slip.Value is null)
        {
            return ServiceResult<ReceiptDocument>.Fail(slip.Error ?? "The recovery slip could not be built.");
        }

        var data = slip.Value;
        var width = _settings.GetInt("printer.width", 58) >= 80 ? 48 : 32;

        var document = new ReceiptDocument
        {
            Title = "Recovery " + data.Code,
            Width = width,
            AutoCut = _settings.GetBool("printer.autoCut", true),
            Copies = Math.Max(1, _settings.GetInt("printer.copies", 1))
        };

        void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.Fit(text, width), align, bold, big));

        void Pair(string left, string right, bool bold = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.TwoColumns(left, right, width), ReceiptAlign.Left, bold));

        var shop = _settings.GetString("store.name");
        Add(string.IsNullOrWhiteSpace(shop) ? "Area11" : shop, ReceiptAlign.Center, bold: true);

        var address = _settings.GetString("store.address");
        if (!string.IsNullOrWhiteSpace(address))
        {
            Add(address, ReceiptAlign.Center);
        }

        var phone = _settings.GetString("store.phone");
        if (!string.IsNullOrWhiteSpace(phone))
        {
            Add(phone, ReceiptAlign.Center);
        }

        document.Lines.Add(ReceiptLine.Sep(width));
        Add("UDHAAR RECOVERY", ReceiptAlign.Center, bold: true, big: true);
        Add("Old balance received", ReceiptAlign.Center);
        document.Lines.Add(ReceiptLine.Sep(width));

        Pair(data.Code, data.Date is null ? "-" : data.Date.Length >= 16 ? data.Date[..16] : data.Date);
        Add(data.CustomerName, bold: true);

        if (!string.IsNullOrWhiteSpace(data.Phone))
        {
            Add("Phone: " + data.Phone);
        }

        document.Lines.Add(ReceiptLine.Sep(width));

        Pair("Udhaar before", data.BeforeText);
        Pair("RECEIVED NOW", data.ReceivedText, bold: true);
        Pair("Udhaar after", data.AfterText, bold: true);
        Pair("Method", data.MethodLabel);

        if (!string.IsNullOrWhiteSpace(data.Note) && data.Note != "Khata collection")
        {
            Add("Note: " + data.Note);
        }

        if (data.Hits.Count > 0)
        {
            document.Lines.Add(ReceiptLine.Sep(width));
            Add("Knocked off these bills:");

            foreach (var hit in data.Hits)
            {
                Add("  " + hit.Label);
            }
        }

        document.Lines.Add(ReceiptLine.Sep(width));

        if (data.Cleared)
        {
            Add("KHATA CLEAR - thank you.", ReceiptAlign.Center, bold: true);
        }

        Add("Received by: " + (string.IsNullOrWhiteSpace(data.ReceivedBy) ? "______________" : data.ReceivedBy));
        Add("Signature: ______________");
        document.Lines.Add(ReceiptLine.Sep(width));

        var footer = _settings.GetString("store.footerNote");
        if (!string.IsNullOrWhiteSpace(footer))
        {
            Add(footer, ReceiptAlign.Center);
        }

        return ServiceResult<ReceiptDocument>.Success(document);
    }

    // ------------------------------------------------------------------ types

    private sealed class OpenBill
    {
        public string Code { get; init; } = string.Empty;

        public string Date { get; init; } = string.Empty;

        public long Left { get; set; }
    }

    private sealed class CustomerRow
    {
        public int CustomerId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string? Category { get; set; }

        public long BalancePaisa { get; set; }
    }

    private sealed class BillRow
    {
        public int CustomerId { get; set; }

        public int Id { get; set; }

        public string? Code { get; set; }

        public string? Date { get; set; }

        public long DuePaisa { get; set; }
    }

    private sealed class CreditRow
    {
        public int CustomerId { get; set; }

        public int Id { get; set; }

        public string? Date { get; set; }

        public long AmountPaisa { get; set; }

        public string? Kind { get; set; }
    }

    private sealed class PaymentRow
    {
        public int Id { get; set; }

        public string? Date { get; set; }

        public int CustomerId { get; set; }

        public long AmountPaisa { get; set; }

        public string? Method { get; set; }

        public string? Note { get; set; }

        public string? UserName { get; set; }
    }
}
