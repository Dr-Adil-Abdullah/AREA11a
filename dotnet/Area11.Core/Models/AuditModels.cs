using System.Globalization;
using System.Text;

namespace Area11.Core.Models;

/// <summary>
/// One line of the black box (Spec 22), as the owner reads it.
///
/// The row is deliberately read-only: the table has no update and no delete anywhere in the program,
/// so a mistake is corrected by a second line, never by editing this one.
/// </summary>
public sealed class AuditRow
{
    public int Id { get; set; }

    public string At { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string OldValue { get; set; } = string.Empty;

    public string NewValue { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    /// <summary>`21 Oct 2026 14:32` - the seconds are noise on a screen, they stay in the CSV.</summary>
    public string AtLabel => At.Length >= 16 ? At[..16] : At;

    /// <summary>The change as one arrow, the way the owner reads a correction.</summary>
    public string ChangeLabel
    {
        get
        {
            if (string.IsNullOrEmpty(OldValue) && string.IsNullOrEmpty(NewValue))
            {
                return Details;
            }

            if (string.IsNullOrEmpty(OldValue))
            {
                return NewValue;
            }

            if (string.IsNullOrEmpty(NewValue))
            {
                return OldValue;
            }

            return OldValue + "  →  " + NewValue;
        }
    }

    public string WhoLabel => string.IsNullOrWhiteSpace(UserName) ? "system" : UserName;

    public string EntityLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Entity))
            {
                return "-";
            }

            return string.IsNullOrWhiteSpace(EntityId) ? Entity : Entity + " #" + EntityId;
        }
    }

    /// <summary>`settings_change` reads as `settings change` on a printed line.</summary>
    public string ActionLabel => Action.Replace('_', ' ');
}

/// <summary>What the owner narrowed the black box down to. Empty fields mean "no restriction".</summary>
public sealed class AuditFilter
{
    /// <summary>Free text: action, person, entity, the old or the new value.</summary>
    public string Search { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    /// <summary>`yyyy-MM-dd` or empty. Inclusive day.</summary>
    public string FromDate { get; set; } = string.Empty;

    /// <summary>`yyyy-MM-dd` or empty. Inclusive day: the query uses the next midnight.</summary>
    public string ToDate { get; set; } = string.Empty;

    public int Limit { get; set; } = 500;

    public string FromStamp => string.IsNullOrWhiteSpace(FromDate) ? string.Empty : FromDate.Trim() + " 00:00:00";

    public string UntilStamp
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ToDate))
            {
                return string.Empty;
            }

            return DateTime.ParseExact(ToDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture)
                .AddDays(1)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 00:00:00";
        }
    }

    public bool IsNarrowed =>
        !string.IsNullOrWhiteSpace(Search)
        || !string.IsNullOrWhiteSpace(Module)
        || !string.IsNullOrWhiteSpace(Action)
        || !string.IsNullOrWhiteSpace(FromDate)
        || !string.IsNullOrWhiteSpace(ToDate);

    public AuditFilter Copy() => new()
    {
        Search = Search,
        Module = Module,
        Action = Action,
        FromDate = FromDate,
        ToDate = ToDate,
        Limit = Limit
    };
}

/// <summary>What the page shows after a search: the rows, how many there are, and the filter names.</summary>
public sealed class AuditPage
{
    public List<AuditRow> Rows { get; init; } = new();

    /// <summary>How many rows match the filter in the whole table (not only the page).</summary>
    public int Total { get; init; }

    public AuditFilter Filter { get; init; } = new();

    public List<string> Modules { get; init; } = new();

    public List<string> Actions { get; init; } = new();

    public List<string> Users { get; init; } = new();

    public bool IsEmpty => Rows.Count == 0;

    public string CountLabel => Total == Rows.Count
        ? Total.ToString(CultureInfo.InvariantCulture) + " line(s)"
        : "showing " + Rows.Count.ToString(CultureInfo.InvariantCulture) + " of "
          + Total.ToString(CultureInfo.InvariantCulture) + " line(s)";

    /// <summary>The CSV file the Save button writes: one header line, then every row of the page.</summary>
    public string ToCsv() => CsvWriter.Document(
        new[] { "Id", "At", "User", "Module", "Action", "Entity", "Entity id", "Old value", "New value", "Details" },
        Rows.Select(row => new[]
        {
            row.Id.ToString(CultureInfo.InvariantCulture),
            row.At,
            row.WhoLabel,
            row.Module,
            row.Action,
            row.Entity,
            row.EntityId,
            row.OldValue,
            row.NewValue,
            row.Details
        }));
}

/// <summary>
/// The small, boring, load-bearing part of an export: commas and quotes inside a value must not
/// break the file, because a break shows up as a shifted column in Excel - months later.
///
/// RFC 4180: wrap the value in double quotes when it holds a comma, a quote or a line break, and
/// double every quote inside it. The file uses CRLF because that is what Excel and Notepad expect.
/// </summary>
public static class CsvWriter
{
    public static string Document(IEnumerable<string> header, IEnumerable<IEnumerable<string?>> rows)
    {
        var text = new StringBuilder();

        text.Append(Line(header));
        text.Append("\r\n");

        foreach (var row in rows)
        {
            text.Append(Line(row));
            text.Append("\r\n");
        }

        return text.ToString();
    }

    public static string Line(IEnumerable<string?> fields) =>
        string.Join(",", fields.Select(Field));

    public static string Field(string? value)
    {
        var text = value ?? string.Empty;

        if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
