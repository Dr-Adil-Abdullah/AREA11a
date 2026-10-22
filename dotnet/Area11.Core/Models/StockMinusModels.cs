using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// One medicine whose shelf count is **below zero**, and the monthly review of it (F-39, Spec §33.6.6).
///
/// A minus shelf is never a number to fix silently: it means a bill, a return or an entry that the
/// stock book cannot explain, and the owner is the one who has to say what happened. The registry
/// keeps the question open until he has written a note against the current episode.
/// </summary>
public sealed class StockMinusRow
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Generic { get; set; }

    public string BaseUnit { get; set; } = "unit";

    /// <summary>Base units on the shelf: always negative on this page.</summary>
    public double StockQty { get; set; }

    /// <summary>What the missing goods are worth at purchase cost; negative, like the shelf.</summary>
    public long ValuePaisa { get; set; }

    // ---- the episode: the movement that took the shelf below zero ----

    public string? SinceAt { get; set; }

    public string? SinceType { get; set; }

    public string? SinceRefCode { get; set; }

    public int DaysNegative { get; set; }

    // ---- the monthly review ----

    public string? ReviewAt { get; set; }

    public string? ReviewBy { get; set; }

    public string? ReviewNote { get; set; }

    /// <summary>True when the last review happened **after** this episode began.</summary>
    public bool IsReviewed { get; set; }

    public string NameLabel => string.IsNullOrWhiteSpace(Generic) ? Name : Name + " (" + Generic + ")";

    public string StockText => Qty.Format(StockQty) + " " + BaseUnit;

    public string ValueText => Money.Format(ValuePaisa);

    public string SinceLabel => string.IsNullOrWhiteSpace(SinceAt)
        ? "no stock movement on record"
        : "since " + SinceAt![..Math.Min(10, SinceAt!.Length)];

    public string CauseLabel => string.IsNullOrWhiteSpace(SinceAt)
        ? SinceLabel
        : SinceLabel + " - " + StockMinusCause.Describe(SinceType, SinceRefCode);

    public string DaysLabel => string.IsNullOrWhiteSpace(SinceAt) ? "unknown" : DaysNegative + " day(s)";

    public string ReviewLabel => IsReviewed && !string.IsNullOrWhiteSpace(ReviewAt)
        ? "reviewed " + ReviewAt![..Math.Min(10, ReviewAt!.Length)] + " by " + ReviewBy
        : "not reviewed yet";

    public string ReviewNoteLabel => IsReviewed && !string.IsNullOrWhiteSpace(ReviewNote)
        ? ReviewNote!
        : string.Empty;

    /// <summary>"old" once the shelf has been minus for a month - the colour trigger on the row.</summary>
    public string Tone => DaysNegative >= 30 ? "old" : "fresh";
}

/// <summary>The whole registry: every minus shelf, and how much of it has been looked at.</summary>
public sealed class StockMinusReport
{
    public List<StockMinusRow> Rows { get; } = new();

    public int Count => Rows.Count;

    public long TotalValuePaisa => Rows.Sum(row => row.ValuePaisa);

    public int ReviewedCount => Rows.Count(row => row.IsReviewed);

    public int NotReviewedCount => Rows.Count(row => !row.IsReviewed);

    public int OldestDays => Rows.Count == 0 ? 0 : Rows.Max(row => row.DaysNegative);

    public string TotalText => Money.Format(TotalValuePaisa);

    public string OldestText => OldestDays == 0 ? "-" : OldestDays + " day(s)";

    public string SummaryText => Rows.Count == 0
        ? "No medicine is below zero - the shelf and the stock book agree."
        : Rows.Count + " medicine(s) are below zero, worth " + TotalText + " at cost. "
          + NotReviewedCount + " still need the owner's review this month.";
}

/// <summary>The words used for a stock movement on the registry - the why, not the raw type code.</summary>
public static class StockMinusCause
{
    public static string Describe(string? type, string? refCode)
    {
        var verb = (type ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "sale" => "sold",
            "return_in" => "came back on a return",
            "return_out" => "left again on a return",
            "sale_return" => "came back on a return",
            "provisional_return" => "no-bill return",
            "supplier_return" => "sent back to the supplier",
            "quarantine" => "moved to quarantine",
            "import" => "imported",
            "adjust" or "stock_adjustment" => "adjusted",
            "stock_take" => "stock-take correction",
            "writeoff" or "write_off" => "written off",
            "in" => "stock-in",
            "out" => "stock-out",
            "" => "a stock movement",
            _ => type!.Trim().Replace('_', ' ')
        };

        return string.IsNullOrWhiteSpace(refCode) ? verb : verb + " " + refCode!.Trim();
    }
}
