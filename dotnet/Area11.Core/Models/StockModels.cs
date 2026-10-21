namespace Area11.Core.Models;

/// <summary>One batch of a medicine, as the write-off screen shows it.</summary>
public sealed class BatchRow
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string BatchNo { get; set; } = string.Empty;

    public string? ExpiryDate { get; set; }

    public double QtyBase { get; set; }

    public long CostPaisa { get; set; }

    public string? SupplierName { get; set; }

    /// <summary>How much money is lying in this batch at purchase cost.</summary>
    public long ValuePaisa => Area11.Core.Utils.Calc.Round(QtyBase * CostPaisa);

    public string QtyLabel => Area11.Core.Utils.Qty.Format(QtyBase);

    public string ValueLabel => Area11.Core.Utils.Money.Format(ValuePaisa);

    /// <summary>EXPIRED / Level 3 / OK - the same words the Products screen uses.</summary>
    public string ExpiryLabel => Area11.Core.Utils.Expiry.StatusLabel(ExpiryDate, 365, 180, 90);

    public bool IsExpired => ExpiryLabel == "EXPIRED";
}

/// <summary>
/// What the owner is writing off, and why. `direction` is derived: a shortage is `out`, a count
/// correction that found extra is `in`.
/// </summary>
public sealed class WriteOffInput
{
    public int ProductId { get; set; }

    public int? BatchId { get; set; }

    public double QtyEntered { get; set; }

    /// <summary>box | strip | unit - the words the owner counts in.</summary>
    public string Unit { get; set; } = "unit";

    /// <summary>expired | damaged | lost | count_short | count_extra | other</summary>
    public string Reason { get; set; } = "expired";

    public string? Note { get; set; }
}

/// <summary>What the owner must read after a write-off saved.</summary>
public sealed class WriteOffResult
{
    public int AdjustmentId { get; set; }

    public long LossPaisa { get; set; }

    public double QtyBase { get; set; }

    /// <summary>The stock left in that batch after the write-off (may be minus).</summary>
    public double StockAfterBase { get; set; }

    public List<string> Warnings { get; init; } = new();
}

/// <summary>One line of write-off history: what went, how much, at what cost, and who did it.</summary>
public sealed class WriteOffRow
{
    public int Id { get; set; }

    public string? Date { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? BatchNo { get; set; }

    public string? ExpiryDate { get; set; }

    /// <summary>out (shortage / destroyed) or in (count found extra).</summary>
    public string? Direction { get; set; }

    public double QtyBase { get; set; }

    public double? QtyEntered { get; set; }

    public string? UnitEntered { get; set; }

    public long ValuePaisa { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? UserName { get; set; }

    public string QtyLabel => Area11.Core.Utils.Qty.Format(QtyBase);

    public string ValueLabel => Area11.Core.Utils.Money.Format(ValuePaisa);

    public string SignedQtyLabel => (Direction == "in" ? "+" : "-") + QtyLabel;

    public string ReasonLabel => Reason switch
    {
        "expired" => "Expired",
        "damaged" => "Broken / dropped",
        "lost" => "Lost",
        "count_short" => "Count was less",
        "count_extra" => "Count was more",
        _ => "Other"
    };

    public string DateLabel => string.IsNullOrWhiteSpace(Date) || Date!.Length < 16 ? Date ?? string.Empty : Date![..16];
}
