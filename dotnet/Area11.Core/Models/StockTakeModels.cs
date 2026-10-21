using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>One counted medicine inside one stock-take session.</summary>
public sealed class StockTakeLine
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int? BatchId { get; set; }

    public string? BatchNo { get; set; }

    public string? ExpiryDate { get; set; }

    /// <summary>Which shelf it was counted on, so the owner can walk the shop in order.</summary>
    public string? RackNo { get; set; }

    /// <summary>The book number, frozen when the session opened (19.2.1).</summary>
    public double SystemQtyBase { get; set; }

    /// <summary>What the owner actually counted; null = not counted yet.</summary>
    public double? CountedQtyBase { get; set; }

    public string? UnitEntered { get; set; }

    public double? QtyEntered { get; set; }

    /// <summary>counted - book. Minus means the book was too high (shortage).</summary>
    public double? DiffQtyBase { get; set; }

    public long? ValuePaisa { get; set; }

    public string? CountedBy { get; set; }

    public string? CountedAt { get; set; }

    public string? Note { get; set; }

    public bool IsCounted => CountedQtyBase is not null;

    public bool HasDifference => DiffQtyBase is not null && Math.Abs(DiffQtyBase.Value) > 0.0005;

    public string BookLabel => Qty.Format(SystemQtyBase);

    public string CountedLabel => CountedQtyBase is null ? "—" : Qty.Format(CountedQtyBase.Value);

    public string DiffLabel => DiffQtyBase is null
        ? "—"
        : (DiffQtyBase.Value > 0 ? "+" : string.Empty) + Qty.Format(DiffQtyBase.Value);

    public string ValueLabel => ValuePaisa is null ? "—" : Money.Format(ValuePaisa.Value);

    public string WhereLabel => string.IsNullOrWhiteSpace(BatchNo)
        ? "(new batch)"
        : string.IsNullOrWhiteSpace(RackNo) ? BatchNo! : BatchNo + " · " + RackNo;

    public string CountedByLabel => string.IsNullOrWhiteSpace(CountedBy)
        ? "—"
        : string.IsNullOrWhiteSpace(CountedAt) ? CountedBy! : CountedBy + " " + CountedAt;
}

/// <summary>One stock-take session: the frozen book, the counts and the outcome.</summary>
public sealed class StockTakeSession
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? Date { get; set; }

    /// <summary>open | applied | cancelled</summary>
    public string Status { get; set; } = "open";

    public string? Room { get; set; }

    public int? CategoryId { get; set; }

    public string? Note { get; set; }

    public int ItemsCount { get; set; }

    public int DiffCount { get; set; }

    public long ShortPaisa { get; set; }

    public long ExtraPaisa { get; set; }

    public string? UserName { get; set; }

    public string? AppliedAt { get; set; }

    public string? AppliedBy { get; set; }

    public int CountedLines { get; set; }

    public int TotalLines { get; set; }

    /// <summary>How far the sheet has got - the number the owner watches while counting.</summary>
    public string ProgressLabel => CountedLines + " of " + TotalLines + " counted";

    public string StatusLabel => Status switch
    {
        "applied" => "Applied",
        "cancelled" => "Cancelled",
        _ => "Open"
    };

    public string ShortLabel => Money.Format(ShortPaisa);

    public string ExtraLabel => Money.Format(ExtraPaisa);

    public string DateLabel => string.IsNullOrWhiteSpace(Date) || Date!.Length < 16 ? Date ?? string.Empty : Date![..16];

    public bool IsOpen => string.Equals(Status, "open", StringComparison.OrdinalIgnoreCase);

    public bool IsApplied => string.Equals(Status, "applied", StringComparison.OrdinalIgnoreCase);
}

/// <summary>What the owner must read after Apply.</summary>
public sealed class StockTakeResult
{
    public string Code { get; set; } = string.Empty;

    public int DiffCount { get; set; }

    public long ShortPaisa { get; set; }

    public long ExtraPaisa { get; set; }

    public int NewBatches { get; set; }

    public List<string> Warnings { get; init; } = new();
}

/// <summary>What the screen needs for one line at a time: book, counted, difference, value.</summary>
public sealed class StockTakeSummary
{
    public int TotalLines { get; set; }

    public int CountedLines { get; set; }

    public int DiffCount { get; set; }

    public long ShortPaisa { get; set; }

    public long ExtraPaisa { get; set; }

    public string ShortLabel => Money.Format(ShortPaisa);

    public string ExtraLabel => Money.Format(ExtraPaisa);

    public string ProgressLabel => CountedLines + " of " + TotalLines + " counted · " + DiffCount + " with a difference";
}
