using System.Globalization;

namespace Area11.Core.Utils;

/// <summary>
/// Expiry is stored twice in the reference schema: expiry_date (YYYY-MM-DD, used for FEFO) and
/// expiry_ym (YYYY-MM, used for the fast month filters and the three alert levels).
/// A month typed by the owner means "end of that month".
/// </summary>
public static class Expiry
{
    public static bool TryNormalize(string? text, out string date, out string yearMonth)
    {
        date = string.Empty;
        yearMonth = string.Empty;

        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return false;
        }

        if (DateTime.TryParseExact(value, new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var full))
        {
            date = full.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            yearMonth = full.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return true;
        }

        if (DateTime.TryParseExact(value, new[] { "yyyy-MM", "yyyy/MM", "MM-yyyy", "MM/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            var lastDay = DateTime.DaysInMonth(month.Year, month.Month);
            date = new DateTime(month.Year, month.Month, lastDay).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            yearMonth = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    public static string StatusLabel(string? expiryDate, int level1Days, int level2Days, int level3Days)
    {
        if (string.IsNullOrWhiteSpace(expiryDate) ||
            !DateTime.TryParse(expiryDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return "—";
        }

        var days = (date.Date - DateTime.Today).Days;

        return days switch
        {
            < 0 => "EXPIRED",
            <= 90 => $"Level 3 · {days} days",
            <= 180 => $"Level 2 · {days} days",
            <= 365 => $"Level 1 · {days} days",
            _ => "OK"
        };
    }
}
