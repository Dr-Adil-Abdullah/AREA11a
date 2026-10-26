using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

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

/// <summary>
/// The complete before/after text behind one selected audit row (Z-05).
///
/// This is a reader, never a rollback engine. <see cref="EarlierRaw"/> and
/// <see cref="RecordedRaw"/> keep the stored strings exactly as the history service
/// returned them; the Text properties only make JSON easier to read on screen. "Recorded" means
/// immediately after THIS line, not the live state now - a later audit line may have changed it.
/// Corrections still go through the module's normal audited workflow.
/// </summary>
public sealed class AuditSnapshot
{
    public const string NoEarlierState = "(No earlier state was recorded before this line.)";

    public const string NoRecordedState = "(No new state was recorded after this line.)";

    private static readonly JsonSerializerOptions DisplayJson = new()
    {
        WriteIndented = true,
        // This is display-only JSON in a read-only TextBox, never HTML or a value written back to
        // SQLite. Keeping Unicode visible is more useful than showing an Urdu name as \u escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string? EarlierRaw { get; init; }

    public string? RecordedRaw { get; init; }

    public string EarlierText { get; init; } = NoEarlierState;

    public string RecordedText { get; init; } = NoRecordedState;

    public string DetailsText { get; init; } = string.Empty;

    public string ContextText { get; init; } = string.Empty;

    public string CorrectionText { get; init; } = string.Empty;

    public bool HasEarlierState { get; init; }

    public bool HasRecordedState { get; init; }

    public static AuditSnapshot For(AuditRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new AuditSnapshot
        {
            EarlierRaw = row.OldValue,
            RecordedRaw = row.NewValue,
            HasEarlierState = !string.IsNullOrEmpty(row.OldValue),
            HasRecordedState = !string.IsNullOrEmpty(row.NewValue),
            EarlierText = Display(row.OldValue, NoEarlierState),
            RecordedText = Display(row.NewValue, NoRecordedState),
            DetailsText = Display(row.Details, "(No extra details were recorded.)"),
            ContextText = row.ActionLabel + "  ·  " + row.EntityLabel + "  ·  "
                          + row.WhoLabel + "  ·  " + row.AtLabel,
            CorrectionText = CorrectionGuidance(row)
        };
    }

    private static string Display(string? raw, string emptyText)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return emptyText;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(document.RootElement, DisplayJson);
        }
        catch (JsonException)
        {
            // Old plain-text values and partial/broken JSON are still evidence. Show them exactly
            // as stored instead of hiding the line or throwing while the owner is investigating.
            return raw;
        }
    }

    private static string CorrectionGuidance(AuditRow row)
    {
        var module = row.Module.Trim().ToUpperInvariant();
        var action = row.Action.Trim().ToLowerInvariant();

        if (action.StartsWith("provisional_", StringComparison.Ordinal))
        {
            return "Use Returns → No bill (PR) and review the linked bill and batch. PR cancellation "
                   + "removes stock from the same batch only; it never creates a payment and "
                   + "does not reverse the original financial effect.";
        }

        if (module == "CUSTOMERS" && (action is "customer_collect" or "khata_reduced"))
        {
            return "Use the customer ledger to review a new audited collection or return correction. "
                   + "Never type over the udhaar balance or create a payment from this viewer.";
        }

        return module switch
        {
            "POS" or "SALES" =>
                "Use Sales history to find the original bill and Returns for an audited correction. "
                + "Check goods, khata and drawer separately; never delete the bill or invent a payment.",
            "RETURNS" =>
                "Use Returns and the original bill/return history to review a new audited correction. "
                + "Never delete a return or assume that a stock movement also reversed cash.",
            "STOCK" or "STOCKTAKE" or "QUARANTINE" =>
                "Use Write-off / count correction or Stock-take after checking the physical batch. "
                + "Apply a new audited stock movement; never rewrite the earlier ledger line.",
            "INVENTORY" or "PRODUCTS" or "REORDER" =>
                "Use Products / Catalog (or Reorder pad for a level) to save the intended value as a "
                + "new audited change. Quantity corrections belong in Stock-take, not in this viewer.",
            "SETTINGS" =>
                "Open Settings, compare this earlier text with the current value, and save the intended "
                + "value again. The save creates a new audit line; it does not erase this one.",
            "USERS" =>
                "Open Staff & permissions in Settings and save the intended access as a new audited "
                + "change. Never restore a password or PIN from this panel.",
            "CASH" or "PURCHASES" or "SUPPLIERS" =>
                "Use the named cash, purchase or supplier screen for separate, reviewed and audited "
                + "money/stock corrections. Never edit a ledger balance or create a payment here.",
            "CUSTOMERS" =>
                "Use Customers to save a master-data correction as a new audited change. Udhaar changes "
                + "only through the normal bill, collection or return workflow, never by editing history.",
            "BACKUP" or "SYNC" or "IMPORT" =>
                "Do not replay this line as a restore. Inspect the affected records and use the relevant "
                + "module for a new audited correction; take a fresh backup before any restore decision.",
            _ =>
                "Use the named module's normal screen for a new, reviewed and audited correction. "
                + "This viewer never edits, deletes or replays history."
        };
    }
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
