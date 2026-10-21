using System.Globalization;

namespace Area11.Core.Utils;

/// <summary>Which part of the owner's expiry scale a batch sits on (Spec 6.1, F-29).</summary>
public enum ExpiryKind
{
    None,
    Ok,
    Level1,
    Level2,
    Level3,
    Expired
}

/// <summary>
/// Expiry is stored twice in the reference schema: expiry_date (YYYY-MM-DD, used for FEFO) and
/// expiry_ym (YYYY-MM, used for the fast month filters and the three alert levels).
/// A month typed by the owner means "end of that month".
///
/// The three bands are NOT hard-coded here. Level 1 is the *farthest* band, Level 3 the nearest
/// (defaults: 365 / 180 / 90 days), and every caller passes the numbers it read from settings, so
/// changing them in Settings changes the label - that is exactly what F-29 asks for.
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

    /// <summary>Whole days from today to the expiry date; null when there is no readable date.</summary>
    public static int? DaysLeft(string? expiryDate)
    {
        if (string.IsNullOrWhiteSpace(expiryDate) ||
            !DateTime.TryParse(expiryDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        return (date.Date - DateTime.Today).Days;
    }

    /// <summary>
    /// The band a date sits in. Level 3 is checked first because it is the nearest band (the
    /// smallest number of days); a date already past is EXPIRED whatever the settings say.
    /// </summary>
    public static ExpiryKind KindOf(string? expiryDate, int level1Days, int level2Days, int level3Days)
    {
        var days = DaysLeft(expiryDate);

        if (days is null)
        {
            return ExpiryKind.None;
        }

        if (days.Value < 0)
        {
            return ExpiryKind.Expired;
        }

        if (days.Value <= Math.Max(1, level3Days))
        {
            return ExpiryKind.Level3;
        }

        if (days.Value <= Math.Max(1, level2Days))
        {
            return ExpiryKind.Level2;
        }

        return days.Value <= Math.Max(1, level1Days) ? ExpiryKind.Level1 : ExpiryKind.Ok;
    }

    /// <summary>"EXPIRED", "Level 3 · 41 days", "OK", or "—" when there is no date.</summary>
    public static string StatusLabel(string? expiryDate, int level1Days, int level2Days, int level3Days)
    {
        var days = DaysLeft(expiryDate);

        return KindOf(expiryDate, level1Days, level2Days, level3Days) switch
        {
            ExpiryKind.None => "—",
            ExpiryKind.Expired => "EXPIRED",
            ExpiryKind.Ok => "OK",
            ExpiryKind.Level3 => "Level 3 · " + days + " days",
            ExpiryKind.Level2 => "Level 2 · " + days + " days",
            _ => "Level 1 · " + days + " days"
        };
    }

    /// <summary>A short word the screens use for a colour trigger: expired / level3 / level2 / level1 / ok.</summary>
    public static string Tone(ExpiryKind kind) => kind switch
    {
        ExpiryKind.Expired => "expired",
        ExpiryKind.Level3 => "level3",
        ExpiryKind.Level2 => "level2",
        ExpiryKind.Level1 => "level1",
        ExpiryKind.Ok => "ok",
        _ => "none"
    };
}
