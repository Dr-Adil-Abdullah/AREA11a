using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The medical rep and the doctor's prescription (F-44, Spec §8.3.3, §10.3.2, §10.4.1, 17.5, 17.7).
///
/// Two questions live here because they share one page and one idea - **who brought this sale**:
///   * which company's rep visits on which day, so the owner can plan his day;
///   * on whose prescription a bill was sold, and what that prescription earned.
///
/// Profit is never invented: it is the same formula the report uses (Spec 21.6) -
///   sales - cost of goods sold - refunds + cost of the goods that came back to the shelf.
/// A person without the profit permission gets a dash, not a zero, because a zero would look like
/// a fact.
/// </summary>
public sealed class RepService
{
    private const int MaxNameLength = 60;
    private const int MaxPhoneLength = 30;

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public RepService(Database db, AuditService audit, PermissionService permissions, LogService log)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    // ============================== the reps ==============================

    /// <summary>Every company with its rep, ordered the way the week runs.</summary>
    public ServiceResult<List<RepCompanyRow>> ListReps(User? user)
    {
        if (!_permissions.Can(user, Modules.Products))
        {
            return ServiceResult<List<RepCompanyRow>>.Fail(
                "You do not have permission to see the companies and their reps.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<RepCompanyRow>(connection, @"
SELECT id AS id, name AS name, rep_name AS repName, rep_phone AS repPhone, visit_day AS visitDay
FROM companies
WHERE active = 1
ORDER BY CASE WHEN visit_day IS NULL THEN 1 ELSE 0 END, visit_day, name COLLATE NOCASE;");

            return ServiceResult<List<RepCompanyRow>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The reps could not be loaded.", exception);
            return ServiceResult<List<RepCompanyRow>>.Fail("The reps could not be loaded.");
        }
    }

    /// <summary>
    /// Writes the rep of one company: name, phone and visit day. An empty name clears the rep -
    /// the shop changed agency, and the black box keeps the old line (D-00b).
    /// </summary>
    public ServiceResult SaveRep(User? user, int companyId, string? repName, string? repPhone, int? visitDay)
    {
        if (!_permissions.Can(user, Modules.Products))
        {
            return ServiceResult.Fail("You do not have permission to change a company's rep.");
        }

        var name = (repName ?? string.Empty).Trim();
        var phone = (repPhone ?? string.Empty).Trim();
        var day = visitDay is null or VisitDays.None ? (int?)null : visitDay;

        if (name.Length > MaxNameLength)
        {
            return ServiceResult.Fail("The rep's name is too long - keep it under "
                                     + MaxNameLength + " letters.");
        }

        if (phone.Length > MaxPhoneLength)
        {
            return ServiceResult.Fail("The phone number is too long - keep it under "
                                     + MaxPhoneLength + " characters.");
        }

        if (phone.Any(char.IsLetter))
        {
            return ServiceResult.Fail("A phone number holds digits, spaces, + and - only.");
        }

        if (day is not null && !VisitDays.IsKnown(day))
        {
            return ServiceResult.Fail("Pick a visit day from the list, or leave it empty.");
        }

        if (name.Length == 0 && (phone.Length > 0 || day is not null))
        {
            return ServiceResult.Fail("Write the rep's name first - a phone or a day needs a person.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var current = _db.QuerySingle<RepCompanyRow>(connection, @"
SELECT id AS id, name AS name, rep_name AS repName, rep_phone AS repPhone, visit_day AS visitDay
FROM companies WHERE id = @companyId AND active = 1;",
                    new { companyId });

                if (current is null)
                {
                    return ServiceResult.Fail("That company is not on the list any more.");
                }

                _db.Execute(connection, @"
UPDATE companies
SET rep_name = @name, rep_phone = @phone, visit_day = @day
WHERE id = @companyId;",
                    new
                    {
                        name = name.Length == 0 ? null : name,
                        phone = phone.Length == 0 ? null : phone,
                        day,
                        companyId
                    });

                _audit.Record(connection, user, Modules.Products, "company_rep_update",
                    "companies", companyId.ToString(),
                    oldValue: DescribeRep(current), newValue: DescribeRep(name, phone, day),
                    details: current.Name);

                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"The rep of company {companyId} could not be saved.", exception);
            return ServiceResult.Fail("The rep could not be saved. " + exception.Message);
        }
    }

    // ============================== the doctors ==============================

    /// <summary>
    /// Every doctor (a customer whose category is 'doctor') with the bills sold on his prescription
    /// and what those bills earned. A doctor with no bills is still listed - he is on the panel.
    /// </summary>
    public ServiceResult<List<DoctorRow>> ListDoctors(User? user)
    {
        if (!_permissions.Can(user, Modules.Customers))
        {
            return ServiceResult<List<DoctorRow>>.Fail(
                "You do not have permission to see the doctors.");
        }

        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<DoctorRow>(connection, DoctorSql);

            MaskProfit(rows, _permissions.Can(user, Modules.Profit));

            return ServiceResult<List<DoctorRow>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The doctors could not be loaded.", exception);
            return ServiceResult<List<DoctorRow>>.Fail("The doctors could not be loaded.");
        }
    }

    /// <summary>
    /// Lifetime profit per customer (Spec 17.5). Owner-only: it is the shop's margin spread over
    /// the people who bought it, and a manager has no business seeing another customer's value.
    /// </summary>
    public ServiceResult<List<CustomerProfitRow>> ListCustomerProfit(User? user)
    {
        if (!_permissions.Can(user, Modules.Profit))
        {
            return ServiceResult<List<CustomerProfitRow>>.Fail(
                "Only the owner can see what a customer has earned the shop.");
        }

        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<CustomerProfitRow>(connection, CustomerProfitSql);

            foreach (var row in rows)
            {
                row.ProfitKnown = true;
            }

            return ServiceResult<List<CustomerProfitRow>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The lifetime profit per customer could not be loaded.", exception);
            return ServiceResult<List<CustomerProfitRow>>.Fail(
                "The lifetime profit could not be loaded. " + exception.Message);
        }
    }

    // ============================== the SQL ==============================

    /// <summary>
    /// Bills by doctor, with three separate aggregates: SUM over a join of bills and items would
    /// multiply every line by every other line. `paid|partial|credit` is the same filter the report
    /// uses - a held or voided bill is not a sale.
    /// </summary>
    private const string DoctorSql = @"
WITH prescribed AS (
    SELECT s.doctor_id AS doctorId,
           COUNT(1) AS bills,
           IFNULL(SUM(s.total_paisa), 0) AS sales,
           MAX(s.date) AS lastBill
    FROM sales s
    WHERE s.status IN ('paid','partial','credit') AND s.doctor_id IS NOT NULL
    GROUP BY s.doctor_id
),
cost AS (
    SELECT s.doctor_id AS doctorId,
           CAST(IFNULL(SUM(si.qty_base * si.cost_paisa_at_sale), 0) AS INTEGER) AS cost
    FROM sales s
    JOIN sale_items si ON si.sale_id = s.id
    WHERE s.status IN ('paid','partial','credit') AND s.doctor_id IS NOT NULL
    GROUP BY s.doctor_id
),
came AS (
    SELECT s.doctor_id AS doctorId,
           IFNULL(SUM(r.refund_paisa), 0) AS refunds,
           CAST(IFNULL(SUM(CASE WHEN r.restock = 1 THEN r.qty_base * si.cost_paisa_at_sale ELSE 0 END), 0) AS INTEGER) AS restock
    FROM sale_returns r
    JOIN sale_items si ON si.id = r.sale_item_id
    JOIN sales s ON s.id = si.sale_id
    WHERE s.doctor_id IS NOT NULL
    GROUP BY s.doctor_id
)
SELECT c.id AS doctorId, c.name AS name, c.phone AS phone,
       IFNULL(p.bills, 0) AS bills,
       CAST(IFNULL(p.sales, 0) AS INTEGER) AS salesPaisa,
       CAST(IFNULL(p.sales, 0) - IFNULL(k.cost, 0) - IFNULL(m.refunds, 0) + IFNULL(m.restock, 0) AS INTEGER) AS profitPaisa,
       p.lastBill AS lastBill
FROM customers c
LEFT JOIN prescribed p ON p.doctorId = c.id
LEFT JOIN cost k ON k.doctorId = c.id
LEFT JOIN came m ON m.doctorId = c.id
WHERE c.active = 1 AND LOWER(IFNULL(c.category, '')) = 'doctor'
ORDER BY IFNULL(p.sales, 0) DESC, c.name COLLATE NOCASE;";

    /// <summary>Lifetime profit per customer - the same three aggregates, keyed on the buyer.</summary>
    private const string CustomerProfitSql = @"
WITH bought AS (
    SELECT s.customer_id AS customerId,
           COUNT(1) AS bills,
           IFNULL(SUM(s.total_paisa), 0) AS sales,
           MAX(s.date) AS lastBill
    FROM sales s
    WHERE s.status IN ('paid','partial','credit') AND s.customer_id IS NOT NULL
    GROUP BY s.customer_id
),
cost AS (
    SELECT s.customer_id AS customerId,
           CAST(IFNULL(SUM(si.qty_base * si.cost_paisa_at_sale), 0) AS INTEGER) AS cost
    FROM sales s
    JOIN sale_items si ON si.sale_id = s.id
    WHERE s.status IN ('paid','partial','credit') AND s.customer_id IS NOT NULL
    GROUP BY s.customer_id
),
came AS (
    SELECT s.customer_id AS customerId,
           IFNULL(SUM(r.refund_paisa), 0) AS refunds,
           CAST(IFNULL(SUM(CASE WHEN r.restock = 1 THEN r.qty_base * si.cost_paisa_at_sale ELSE 0 END), 0) AS INTEGER) AS restock
    FROM sale_returns r
    JOIN sale_items si ON si.id = r.sale_item_id
    JOIN sales s ON s.id = si.sale_id
    WHERE s.customer_id IS NOT NULL
    GROUP BY s.customer_id
)
SELECT c.id AS customerId, c.name AS name, c.phone AS phone, c.category AS category,
       b.bills AS bills,
       CAST(IFNULL(b.sales, 0) AS INTEGER) AS salesPaisa,
       CAST(IFNULL(b.sales, 0) - IFNULL(k.cost, 0) - IFNULL(m.refunds, 0) + IFNULL(m.restock, 0) AS INTEGER) AS profitPaisa,
       b.lastBill AS lastBill
FROM customers c
JOIN bought b ON b.customerId = c.id
LEFT JOIN cost k ON k.customerId = c.id
LEFT JOIN came m ON m.customerId = c.id
WHERE c.active = 1
ORDER BY profitPaisa DESC, c.name COLLATE NOCASE;";

    // ============================== helpers ==============================

    /// <summary>
    /// "Imran · 0300-1234567 · Monday". The audit keeps a readable line, not a JSON blob, so the
    /// owner can see in the black box what the old rep was after the agency changed.
    /// </summary>
    private static string DescribeRep(RepCompanyRow row) =>
        DescribeRep(row.RepName ?? string.Empty, row.RepPhone ?? string.Empty, row.VisitDay);

    private static string DescribeRep(string name, string? phone, int? day) =>
        name.Length == 0
            ? "(no rep)"
            : name + " · " + (string.IsNullOrWhiteSpace(phone) ? "-" : phone!.Trim())
              + " · " + VisitDays.Label(day);

    private static void MaskProfit(List<DoctorRow> rows, bool canSeeProfit)
    {
        if (canSeeProfit)
        {
            return;
        }

        foreach (var row in rows)
        {
            row.ProfitPaisa = 0;
            row.ProfitKnown = false;
        }
    }
}
