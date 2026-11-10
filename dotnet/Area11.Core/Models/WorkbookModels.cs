namespace Area11.Core.Models;

/// <summary>What one downloaded workbook row will do when the owner presses Apply.</summary>
public enum BulkUpdateState
{
    Ready,
    Same,
    Skipped,
    Error
}

public sealed class BulkFieldChange
{
    public string Field { get; set; } = string.Empty;

    public string Before { get; set; } = string.Empty;

    public string After { get; set; } = string.Empty;

    public string Caption => Field + ": " + Display(Before) + " → " + Display(After);

    private static string Display(string value) => value.Length == 0 ? "(empty)" : value;
}

public sealed class BulkUpdateRow
{
    public string Sheet { get; set; } = string.Empty;

    public int Line { get; set; }

    public string Key { get; set; } = string.Empty;

    public BulkUpdateState State { get; set; }

    public string Message { get; set; } = string.Empty;

    public List<BulkFieldChange> Changes { get; init; } = new();

    public bool IsReady => State == BulkUpdateState.Ready;

    public bool IsSame => State == BulkUpdateState.Same;

    public bool IsSkipped => State == BulkUpdateState.Skipped;

    public bool IsError => State == BulkUpdateState.Error;

    public string StateLabel => State switch
    {
        BulkUpdateState.Ready => "Change",
        BulkUpdateState.Same => "Same",
        BulkUpdateState.Skipped => "Skipped",
        _ => "Error"
    };

    /// <summary>The complete before → after list for this record, not merely a count.</summary>
    public string Caption
    {
        get
        {
            var prefix = Sheet + " line " + Line + " · " + Key;
            if (IsError || IsSkipped)
            {
                return prefix + " · " + Message;
            }

            return Changes.Count == 0
                ? prefix + " · no change"
                : prefix + " · " + string.Join("; ", Changes.Select(change => change.Caption));
        }
    }
}

public sealed class BulkUpdatePreview
{
    public List<BulkUpdateRow> Rows { get; init; } = new();

    public bool IncludeSettings { get; set; }

    public int ChangeCount => Rows.Count(row => row.IsReady);

    public int SameCount => Rows.Count(row => row.IsSame);

    public int SkippedCount => Rows.Count(row => row.IsSkipped);

    public int ErrorCount => Rows.Count(row => row.IsError);

    public int ChangedFieldCount => Rows.Where(row => row.IsReady).Sum(row => row.Changes.Count);

    /// <summary>Any error refuses the whole workbook; no approved rows are applied by themselves.</summary>
    public bool CanApply => ErrorCount == 0 && ChangeCount > 0;

    public string SummaryText
    {
        get
        {
            var text = $"{ChangeCount} record(s) to change ({ChangedFieldCount} fields), {SameCount} unchanged";
            if (SkippedCount > 0)
            {
                text += $", {SkippedCount} skipped";
            }

            if (ErrorCount > 0)
            {
                text += $", {ErrorCount} with a problem - nothing can be applied until they are fixed";
            }

            return text + ".";
        }
    }

    internal List<PendingBulkRecord> Pending { get; } = new();

    internal Dictionary<string, object?> SettingValues { get; } = new(StringComparer.Ordinal);
}

public sealed class BulkUpdateOutcome
{
    public int ChangedRecords { get; set; }

    public int ChangedFields { get; set; }

    public string SummaryText =>
        $"{ChangedRecords} record(s) updated ({ChangedFields} fields), in one transaction. Every change is in the black box.";
}

/// <summary>Internal apply plan. SQL identifiers come only from the static sheet schemas.</summary>
internal sealed class PendingBulkRecord
{
    public string Sheet { get; set; } = string.Empty;

    public string Table { get; set; } = string.Empty;

    public string KeyColumn { get; set; } = string.Empty;

    public object KeyValue { get; set; } = 0;

    public string Module { get; set; } = "Import";

    public bool HasUpdatedAt { get; set; }

    /// <summary>Fingerprint of every exported value before preview, not only the edited fields.</summary>
    public string Snapshot { get; set; } = string.Empty;

    public List<PendingBulkField> Fields { get; init; } = new();

    public Dictionary<string, object?> Before { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<string, object?> After { get; init; } = new(StringComparer.Ordinal);

    /// <summary>The complete proposed row, used for cross-record catalog validation.</summary>
    public Dictionary<string, object?> RowAfter { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class PendingBulkField
{
    public string SqlName { get; set; } = string.Empty;

    public object? OldValue { get; set; }

    public object? NewValue { get; set; }
}
