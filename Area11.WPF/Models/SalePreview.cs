namespace Area11.WPF.Models;

/// <summary>What the counter shows before saving: the same calculation the save will use.</summary>
public sealed class SalePreview
{
    public long SubtotalPaisa { get; init; }

    public long DiscountPaisa { get; init; }

    public long TaxPaisa { get; init; }

    public long RoundPaisa { get; init; }

    public long TotalPaisa { get; init; }

    public long ChangePaisa { get; init; }

    public int Lines { get; init; }
}
