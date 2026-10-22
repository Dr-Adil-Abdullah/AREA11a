using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using System.Globalization;
using System.Text;

namespace Area11.Core.Services;

/// <summary>
/// The month's tax (F-49, Spec 6.5, 21.8, 33.4.1).
///
/// The spec applies tax to every bill but says plainly that no separate tax report exists yet
/// (§33.4.1). This service is that report - and it is a **read-only witness**, not a calculator:
/// every rupee comes from `sales.tax_paisa`, exactly what the receipt printed on that day. A rate
/// changed in the middle of the month therefore shows the tax each bill really took, instead of
/// today's rate multiplied over old bills.
///
/// Two limits are stated on the page and in D-63 rather than hidden:
///   • a **void** bill and a **held** bill were never sold, so neither is counted;
///   • a refund is one amount in the schema, so its tax part is **not** split out; refunds are
///     listed beside the report for the accountant to adjust by hand.
/// </summary>
public sealed class TaxService
{
    private readonly Database _db;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public TaxService(Database db, SettingsService settings, PermissionService permissions, LogService log)
    {
        _db = db;
        _settings = settings;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>One month for the accountant: bills, sales before tax, tax and gross, day by day.</summary>
    public ServiceResult<TaxReport> Report(User? user, int year, int month)
    {
        if (!_permissions.Can(user, Modules.Reports))
        {
            return ServiceResult<TaxReport>.Fail("You do not have permission to see the reports.");
        }

        if (year < 2000 || year > 2100 || month < 1 || month > 12)
        {
            return ServiceResult<TaxReport>.Fail("That is not a real month. Choose a month between 2000 and 2100.");
        }

        var prefix = year.ToString("0000", CultureInfo.InvariantCulture) + "-"
                     + month.ToString("00", CultureInfo.InvariantCulture);

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<TaxDayRow>(connection, @"
SELECT substr(s.date, 1, 10) AS day,
       COUNT(1) AS bills,
       CAST(IFNULL(SUM(s.subtotal_paisa - s.discount_paisa), 0) AS INTEGER) AS netPaisa,
       CAST(IFNULL(SUM(s.tax_paisa), 0) AS INTEGER) AS taxPaisa,
       CAST(IFNULL(SUM(s.total_paisa), 0) AS INTEGER) AS grossPaisa
FROM sales s
WHERE s.status IN ('paid', 'partial', 'credit') AND s.date LIKE @like
GROUP BY day
ORDER BY day;", new { like = prefix + "%" });

            var refunds = Convert.ToInt64(_db.Scalar(connection, @"
SELECT CAST(IFNULL(SUM(refund_paisa), 0) AS INTEGER) FROM sale_returns WHERE date LIKE @like;",
                new { like = prefix + "%" }) ?? 0L);

            var report = new TaxReport
            {
                Year = year,
                Month = month,
                MonthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                TaxLabel = FirstNonEmpty(_settings.GetString("tax.label"), "Sales Tax"),
                TaxEnabled = _settings.GetBool("tax.enabled"),
                PercentLabel = Math.Max(0, _settings.GetInt("tax.percent", 0)).ToString(CultureInfo.InvariantCulture) + "%",
                Rows = rows,
                Bills = rows.Sum(row => row.Bills),
                NetPaisa = rows.Sum(row => row.NetPaisa),
                TaxPaisa = rows.Sum(row => row.TaxPaisa),
                GrossPaisa = rows.Sum(row => row.GrossPaisa),
                RefundPaisa = refunds
            };

            return ServiceResult<TaxReport>.Success(report);
        }
        catch (Exception exception)
        {
            _log.Error("The tax report for " + prefix + " could not be built.", exception);
            return ServiceResult<TaxReport>.Fail("The tax report could not be built. " + exception.Message);
        }
    }

    /// <summary>
    /// The same table as tab-separated text, for the accountant's email - not a CSV file, so the
    /// owner can paste it straight into a message. The shape is fixed and tested.
    /// </summary>
    public static string ToText(TaxReport report)
    {
        var text = new StringBuilder();

        text.AppendLine("Tax report - " + report.MonthLabel);
        text.AppendLine(report.SettingLabel);
        text.AppendLine();
        text.AppendLine("Date\tBills\tSales before tax\tTax\tTotal");

        foreach (var row in report.Rows)
        {
            text.AppendLine(string.Join('\t',
                row.DayLabel,
                row.BillsLabel,
                row.NetLabel,
                row.TaxLabel,
                row.GrossLabel));
        }

        text.AppendLine(string.Join('\t',
            "TOTAL",
            report.BillsLabel,
            report.NetLabel,
            report.TaxTotalLabel,
            report.GrossLabel));

        if (report.RefundPaisa > 0)
        {
            text.AppendLine();
            text.AppendLine("Refunds paid in this month (tax part not recorded): " + report.RefundLabel);
        }

        return text.ToString().TrimEnd();
    }

    private static string FirstNonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value!.Trim();
}
