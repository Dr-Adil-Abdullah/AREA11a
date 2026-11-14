namespace Area11.Core.Models;

/// <summary>Owner-managed allowances, per person, not per role. Missing non-owner rows deny edits.</summary>
public sealed class PricingAllowance
{
    public int UserId { get; set; }
    public bool EditRate { get; set; }
    public decimal ManualReductionPercent { get; set; }
    public bool ProfitDiscount { get; set; }
    public decimal ProfitPercent { get; set; }
    public bool RetailDiscount { get; set; }
    public decimal RetailPercent { get; set; }
    public bool OverrideBasis { get; set; }
}

public sealed class SaleLinePricing
{
    public long CostPaisa { get; init; }
    public long GrossPaisa { get; init; }
    public long NetPaisa { get; init; }
}
