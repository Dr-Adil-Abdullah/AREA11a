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

/// <summary>One row of the month drop-down: the month number plus "06 — June".</summary>
public sealed class ExpiryMonth
{
    public ExpiryMonth(int number, string name)
    {
        Number = number;
        Label = number.ToString("00", CultureInfo.InvariantCulture) + " — " + name;
    }

    public int Number { get; }

    public string Label { get; }
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

        // What the strip literally prints: MM/YY ("06/27"). Two-digit years always mean 2000-2099 -
        // no century pivot, so "06/27" can never silently become 1927 (Portion 6). Single-digit
        // months ("6/2027") ride along. Year-first garbage ("27/06") still fails: the month comes
        // first on the strip, and guessing the other way round would invent a wrong expiry.
        if (TryMonthParts(value, out var partYear, out var partMonth))
        {
            var lastDay = DateTime.DaysInMonth(partYear, partMonth);
            var first = new DateTime(partYear, partMonth, 1);
            date = new DateTime(partYear, partMonth, lastDay).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            yearMonth = first.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Splits a two-part month form: month-first ("06/27", "6-2027") or year-first ("2027-6").
    /// Two-digit years mean 2000-2099, always. Anything with a day part, or a month past 12,
    /// is not a month form.
    /// </summary>
    private static bool TryMonthParts(string value, out int year, out int month)
    {
        year = 0;
        month = 0;

        var parts = value.Split('/', '-');
        if (parts.Length != 2 || !IsDigits(parts[0]) || !IsDigits(parts[1]))
        {
            return false;
        }

        if (parts[0].Length <= 2 && int.TryParse(parts[0], out var firstMonth)
            && firstMonth >= 1 && firstMonth <= 12
            && (parts[1].Length == 2 || parts[1].Length == 4)
            && int.TryParse(parts[1], out var secondYear))
        {
            month = firstMonth;
            year = parts[1].Length == 2 ? 2000 + secondYear : secondYear;
            return year is >= 2000 and <= 2199;
        }

        if (parts[0].Length == 4 && int.TryParse(parts[0], out var firstYear)
            && firstYear is >= 2000 and <= 2199
            && parts[1].Length <= 2 && int.TryParse(parts[1], out var secondMonth)
            && secondMonth >= 1 && secondMonth <= 12)
        {
            year = firstYear;
            month = secondMonth;
            return true;
        }

        return false;
    }

    private static bool IsDigits(string part) =>
        part.Length > 0 && part.All(char.IsDigit);

    /// <summary>
    /// The live line under the expiry box: what the typed text means and exactly what would be
    /// stored. Blank means "no expiry" (true, said plainly); unreadable text returns false so the
    /// caller can show its own guidance instead of a guessed date.
    /// </summary>
    public static bool TryDescribe(string? text, out string description)
    {
        description = string.Empty;

        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            description = "No expiry entered.";
            return true;
        }

        if (!TryNormalize(value, out var date, out _))
        {
            return false;
        }

        var parsed = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        description = TryMonthParts(value, out _, out _)
            ? parsed.ToString("MMMM yyyy", CultureInfo.InvariantCulture) + " · stored as " + date
            : parsed.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) + " · stored as " + date;
        return true;
    }

    /// <summary>Twelve rows for the month drop-down: 1..12 with the invariant month name.</summary>
    public static IReadOnlyList<ExpiryMonth> MonthOptions() =>
        Enumerable.Range(1, 12)
            .Select(number => new ExpiryMonth(number,
                CultureInfo.InvariantCulture.DateTimeFormat.MonthNames[number - 1]))
            .ToList();

    /// <summary>
    /// Year rows for the year drop-down. The start year is a parameter (the caller passes this
    /// year), so the list is a pure function of its inputs and the tests pin it exactly.
    /// </summary>
    public static IReadOnlyList<int> YearOptions(int startYear, int count) =>
        count <= 0 ? Array.Empty<int>() : Enumerable.Range(startYear, Math.Min(count, 60)).ToList();

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
