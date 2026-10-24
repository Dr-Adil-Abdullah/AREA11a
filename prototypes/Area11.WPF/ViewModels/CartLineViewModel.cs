using Area11.WPF.Models;
using Area11.WPF.Utils;
using Area11.WPF.ViewModels.Base;

namespace Area11.WPF.ViewModels;

/// <summary>One line in the counter cart. Money is kept in paisa; the view only sees text.</summary>
public sealed class CartLineViewModel : ObservableObject
{
    private int _quantity;
    private long _unitPricePaisa;

    public CartLineViewModel(Product product, string unit, int quantity, long unitPricePaisa)
    {
        ProductId = product.Id;
        Name = product.Name;
        Unit = unit;
        UnitsPerSelected = unit switch
        {
            "box" => Math.Max(1, product.BoxStrips) * Math.Max(1, product.StripUnits),
            "strip" => Math.Max(1, product.StripUnits),
            _ => 1
        };

        _quantity = quantity;
        _unitPricePaisa = unitPricePaisa;
        AvailableBase = product.StockBase;
    }

    public int ProductId { get; }

    public string Name { get; }

    public string Unit { get; }

    public int UnitsPerSelected { get; }

    public long AvailableBase { get; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(QuantityText));
                OnPropertyChanged(nameof(LineTotalPaisa));
                OnPropertyChanged(nameof(LineTotalText));
                OnPropertyChanged(nameof(StockWarning));
            }
        }
    }

    public string QuantityText => Quantity.ToString();

    public long UnitPricePaisa
    {
        get => _unitPricePaisa;
        set
        {
            if (SetProperty(ref _unitPricePaisa, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(UnitPriceText));
                OnPropertyChanged(nameof(LineTotalPaisa));
                OnPropertyChanged(nameof(LineTotalText));
            }
        }
    }

    public string UnitPriceText => FormatHelper.Rupees(UnitPricePaisa);

    public long LineTotalPaisa => (long)Math.Round((decimal)UnitPricePaisa * Quantity, MidpointRounding.AwayFromZero);

    public string LineTotalText => FormatHelper.Rupees(LineTotalPaisa);

    /// <summary>Shown in the cart before saving, so the cashier knows the shelf will go minus.</summary>
    public string StockWarning =>
        Quantity * UnitsPerSelected > AvailableBase
            ? $"⚠ stock {AvailableBase} only — will go minus by {Quantity * UnitsPerSelected - AvailableBase}"
            : string.Empty;

    public SaleLineInput ToInput() => new()
    {
        ProductId = ProductId,
        Unit = Unit,
        Quantity = Quantity,
        UnitPricePaisa = UnitPricePaisa
    };

    public void AddQuantity(int extra)
    {
        Quantity += extra;
        OnPropertyChanged(nameof(QuantityText));
    }
}
