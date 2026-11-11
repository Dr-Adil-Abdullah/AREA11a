using Area11.Core.Utils;

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

    /// <summary>
    /// How the "paid now" money left the shop: cash, online or card. Null or anything unknown
    /// means cash, so drafts written before this choice existed keep their old meaning - and only
    /// cash moves the drawer's expected count.
    /// </summary>
    public string? PaidMethod { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// The owner's own purchase questions (Portion 18): field id to answer. Saved atomically with
    /// the purchase; a missing required answer refuses the post. Drafts carry these along.
    /// </summary>
    public Dictionary<int, string?> CustomAnswers { get; set; } = new();
}

public sealed class PurchaseResult
{
    public int PurchaseId { get; init; }

    public string Code { get; init; } = string.Empty;

    public long TotalPaisa { get; init; }

    public double Units { get; init; }

    public List<string> Warnings { get; init; } = new();
}

/// <summary>One line of a saved purchase invoice, for the printed copy.</summary>
public sealed class PurchaseInvoiceLine
{
    public string ProductName { get; set; } = string.Empty;

    public double QtyEntered { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string? BatchNo { get; set; }

    public string? ExpiryDate { get; set; }

    public long LineTotalPaisa { get; set; }

    public bool IsSample { get; set; }

    public string QtyLabel => Qty.Format(QtyEntered) + " " + Unit;
}
