using System.Globalization;

namespace Area11.WPF.Utils;

public static class DateHelper
{
    /// <summary>
    /// Expiry is stored as a full ISO date. A month-only entry ("2027-06") is expanded to the
    /// last day of that month, because medicine stays usable until the end of the printed month.
    /// </summary>
    public static bool TryNormalizeExpiry(string? text, out string expiry)
    {
        expiry = string.Empty;
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return false;
        }

        if (DateTime.TryParseExact(value, new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var full))
        {
            expiry = full.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        if (DateTime.TryParseExact(value, new[] { "yyyy-MM", "yyyy/MM", "MM-yyyy", "MM/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            var lastDay = DateTime.DaysInMonth(month.Year, month.Month);
            expiry = new DateTime(month.Year, month.Month, lastDay).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    public static string Today() => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string StartOfMonth() => new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)
        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
