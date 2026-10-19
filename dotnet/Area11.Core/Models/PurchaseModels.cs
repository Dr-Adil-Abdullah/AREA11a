namespace Area11.Core.Models;

public sealed class PurchaseLineInput
{
    public int ProductId { get; set; }

    public string Unit { get; set; } = "box";

    public double Quantity { get; set; }

    public string? BatchNo { get; set; }

    /// <summary>"2027-06" or "2027-06-30".</summary>
    public string? Expiry { get; set; }

    public long CostPaisa { get; set; }

    public long RetailPaisa { get; set; }

    /// <summary>Free / bonus line: enters stock at cost 0, never added to the bill.</summary>
    public bool IsSample { get; set; }
}

public sealed class PurchaseInput
{
    public int SupplierId { get; set; }

    public string? SupplierInvoiceNo { get; set; }

    public List<PurchaseLineInput> Lines { get; set; } = new();

    public long PaidPaisa { get; set; }

    public string? Notes { get; set; }
}

public sealed class PurchaseResult
{
    public int PurchaseId { get; init; }

    public string Code { get; init; } = string.Empty;

    public long TotalPaisa { get; init; }

    public double Units { get; init; }

    public List<string> Warnings { get; init; } = new();
}

/// <summary>A batch as the counter sees it (nearest expiry first).</summary>
public sealed class BatchView
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string BatchNo { get; set; } = string.Empty;

    public string? ExpiryDate { get; set; }

    public string? ExpiryYm { get; set; }

    public double QtyBase { get; set; }

    public long CostPaisa { get; set; }

    public long RetailPaisa { get; set; }

    public bool IsSample { get; set; }

    public string QtyLabel => $"{QtyBase:0.###}";

    public string ExpiryLabel => string.IsNullOrWhiteSpace(ExpiryDate)
        ? "—"
        : ExpiryDate;
}
