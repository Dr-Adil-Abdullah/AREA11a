using Area11.WPF.Models;

namespace Area11.WPF.ViewModels;

/// <summary>A pending purchase line before it is saved.</summary>
public sealed class PurchaseLineViewModel
{
    public int ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string Unit { get; init; } = "base";

    public int Quantity { get; init; }

    public string BatchNo { get; init; } = string.Empty;

    public string Expiry { get; init; } = string.Empty;

    public long CostPaisa { get; init; }

    public long RetailPaisa { get; init; }

    public bool IsSample { get; init; }

    public string QuantityText => $"{Quantity} {Unit}";

    public string CostText => Utils.FormatHelper.Rupees(CostPaisa);

    public string RetailText => Utils.FormatHelper.Rupees(RetailPaisa);

    public string SampleText => IsSample ? "sample" : string.Empty;

    public PurchaseLineInput ToInput() => new()
    {
        ProductId = ProductId,
        Unit = Unit,
        Quantity = Quantity,
        BatchNo = BatchNo,
        Expiry = Expiry,
        CostPaisa = CostPaisa,
        RetailPaisa = RetailPaisa,
        IsSample = IsSample
    };
}
