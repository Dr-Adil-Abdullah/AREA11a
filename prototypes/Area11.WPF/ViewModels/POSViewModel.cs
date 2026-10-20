using System.Collections.ObjectModel;
using Area11.WPF.Models;
using Area11.WPF.Services;
using Area11.WPF.Utils;
using Area11.WPF.ViewModels.Base;

namespace Area11.WPF.ViewModels;

public sealed class POSViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly List<Product> _catalog = new();

    private string _searchText = string.Empty;
    private Product? _selectedProduct;
    private string _unit = "strip";
    private string _quantityText = "1";
    private Customer? _selectedCustomer;
    private string _discountPercent = "0";
    private string _paymentMethod = "cash";
    private string _tenderedText = string.Empty;

    private string _subtotalText = "Rs 0.00";
    private string _discountText = "Rs 0.00";
    private string _totalText = "Rs 0.00";
    private string _changeText = "Rs 0.00";

    private string _statusMessage = "Ready.";
    private bool _isStatusError;
    private string _lastBillCode = string.Empty;

    public POSViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        MaxDiscountPercent = user.Role switch
        {
            UserRole.Owner => 100,
            UserRole.Manager => services.Settings.GetInt("discount.maxPercentManager", 20),
            _ => services.Settings.GetInt("discount.maxPercentCashier", 5)
        };

        AddToCartCommand = new RelayCommand(AddToCart);
        RemoveLineCommand = new RelayCommand(RemoveLine);
        ClearCartCommand = new RelayCommand(ClearCart);
        CompleteSaleCommand = new RelayCommand(CompleteSale);
        RefreshCommand = new RelayCommand(LoadLookups);

        LoadLookups();
    }

    public ObservableCollection<Product> Products { get; } = new();

    public ObservableCollection<CartLineViewModel> Cart { get; } = new();

    public ObservableCollection<Customer> Customers { get; } = new();

    public ObservableCollection<string> Units { get; } = new() { "base", "strip", "box" };

    public ObservableCollection<string> PaymentMethods { get; } = new() { "cash", "credit" };

    public RelayCommand AddToCartCommand { get; }

    public RelayCommand RemoveLineCommand { get; }

    public RelayCommand ClearCartCommand { get; }

    public RelayCommand CompleteSaleCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public int MaxDiscountPercent { get; }

    public string CashierName => _user.Name;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyProductFilter();
            }
        }
    }

    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                OnPropertyChanged(nameof(UnitPriceText));
            }
        }
    }

    public string Unit
    {
        get => _unit;
        set
        {
            if (SetProperty(ref _unit, value))
            {
                OnPropertyChanged(nameof(UnitPriceText));
            }
        }
    }

    public string QuantityText
    {
        get => _quantityText;
        set => SetProperty(ref _quantityText, value);
    }

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                OnPropertyChanged(nameof(UnitPriceText));
                OnPropertyChanged(nameof(CustomerBalanceText));
                RecalculateTotals();
            }
        }
    }

    public string CustomerBalanceText => SelectedCustomer is null || SelectedCustomer.Id == 0
        ? "Cash customer — no account"
        : $"{SelectedCustomer.Type} · balance {FormatHelper.Rupees(SelectedCustomer.BalancePaisa)}";

    public string DiscountPercent
    {
        get => _discountPercent;
        set
        {
            if (SetProperty(ref _discountPercent, value))
            {
                RecalculateTotals();
            }
        }
    }

    public string PaymentMethod
    {
        get => _paymentMethod;
        set
        {
            if (SetProperty(ref _paymentMethod, value))
            {
                OnPropertyChanged(nameof(IsCash));
                RecalculateTotals();
            }
        }
    }

    public bool IsCash => string.Equals(PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase);

    public string TenderedText
    {
        get => _tenderedText;
        set
        {
            if (SetProperty(ref _tenderedText, value))
            {
                RecalculateTotals();
            }
        }
    }

    public string UnitPriceText
    {
        get
        {
            if (SelectedProduct is null)
            {
                return "—";
            }

            var price = _services.Sales.ResolveUnitPrice(SelectedProduct, Unit, ActiveCustomer());
            return $"Price per {Unit}: {FormatHelper.Rupees(price)}";
        }
    }

    public string SubtotalText
    {
        get => _subtotalText;
        private set => SetProperty(ref _subtotalText, value);
    }

    public string DiscountText
    {
        get => _discountText;
        private set => SetProperty(ref _discountText, value);
    }

    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    public string ChangeText
    {
        get => _changeText;
        private set => SetProperty(ref _changeText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    public string LastBillCode
    {
        get => _lastBillCode;
        private set => SetProperty(ref _lastBillCode, value);
    }

    private void LoadLookups()
    {
        var products = _services.Products.Search(null);
        _catalog.Clear();
        if (products.Ok && products.Value is not null)
        {
            _catalog.AddRange(products.Value);
        }

        var customers = _services.Partners.GetCustomers();
        Customers.Clear();
        Customers.Add(new Customer { Id = 0, Name = "Walk-in customer" });
        if (customers.Ok && customers.Value is not null)
        {
            foreach (var customer in customers.Value)
            {
                Customers.Add(customer);
            }
        }

        SelectedCustomer = Customers[0];
        ApplyProductFilter();
        RecalculateTotals();
    }

    private void ApplyProductFilter()
    {
        var term = SearchText.Trim();
        Products.Clear();

        foreach (var product in _catalog)
        {
            if (term.Length == 0
                || product.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (product.Barcode ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)
                || (product.Generic ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                Products.Add(product);
            }
        }

        if (SelectedProduct is null || !Products.Contains(SelectedProduct))
        {
            SelectedProduct = Products.FirstOrDefault();
        }
    }

    private void AddToCart()
    {
        if (SelectedProduct is null)
        {
            SetStatus("Search and pick a product first.", true);
            return;
        }

        if (!int.TryParse(QuantityText, out var quantity) || quantity <= 0)
        {
            SetStatus("Quantity must be a whole number of 1 or more.", true);
            return;
        }

        var unitPrice = _services.Sales.ResolveUnitPrice(SelectedProduct, Unit, ActiveCustomer());
        var existing = Cart.FirstOrDefault(line => line.ProductId == SelectedProduct.Id && line.Unit == Unit);

        if (existing is not null)
        {
            existing.AddQuantity(quantity);
        }
        else
        {
            var line = new CartLineViewModel(SelectedProduct, Unit, quantity, unitPrice);
            line.PropertyChanged += (_, _) => RecalculateTotals();
            Cart.Add(line);
        }

        QuantityText = "1";
        SetStatus($"{SelectedProduct.Name} added.", false);
        RecalculateTotals();
    }

    private void RemoveLine()
    {
        if (Cart.Count == 0)
        {
            return;
        }

        Cart.Remove(Cart[^1]);
        RecalculateTotals();
    }

    private void ClearCart()
    {
        Cart.Clear();
        TenderedText = string.Empty;
        DiscountPercent = "0";
        SetStatus("Cart cleared.", false);
        RecalculateTotals();
    }

    private void CompleteSale()
    {
        if (Cart.Count == 0)
        {
            SetStatus("The cart is empty.", true);
            return;
        }

        if (!decimal.TryParse(DiscountPercent, out var percent))
        {
            SetStatus("Discount must be a number.", true);
            return;
        }

        if (percent > MaxDiscountPercent)
        {
            SetStatus($"{_user.Role} can give at most {MaxDiscountPercent}% discount.", true);
            return;
        }

        if (PaymentMethod == "credit" && (SelectedCustomer is null || SelectedCustomer.Id == 0))
        {
            SetStatus("Credit needs a selected customer.", true);
            return;
        }

        var tendered = 0L;
        if (IsCash && MoneyHelper.TryParseRupees(TenderedText, out var typed))
        {
            tendered = MoneyHelper.ToPaisa(typed);
        }

        var result = _services.Sales.Create(BuildInput(tendered), _user);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The bill could not be saved.", true);
            return;
        }

        LastBillCode = $"{result.Value.Code} — total {FormatHelper.Rupees(result.Value.TotalPaisa)}, " +
                       $"change {FormatHelper.Rupees(result.Value.ChangePaisa)}";

        var warnings = result.Warnings.Count > 0 ? "  ⚠ " + string.Join(" ", result.Warnings) : string.Empty;
        SetStatus($"Bill saved.{warnings}", result.Warnings.Count > 0);

        Cart.Clear();
        TenderedText = string.Empty;
        DiscountPercent = "0";
        LoadLookups();
    }

    private SaleInput BuildInput(long tenderedPaisa)
    {
        decimal.TryParse(DiscountPercent, out var percent);

        return new SaleInput
        {
            CustomerId = ActiveCustomer()?.Id,
            DiscountPercent = percent,
            PaymentMethod = PaymentMethod,
            TenderedPaisa = tenderedPaisa,
            Lines = Cart.Select(line => line.ToInput()).ToList()
        };
    }

    private Customer? ActiveCustomer() =>
        SelectedCustomer is { Id: > 0 } ? SelectedCustomer : null;

    /// <summary>The counter asks the service for the same calculation the save will run.</summary>
    private void RecalculateTotals()
    {
        if (Cart.Count == 0)
        {
            SubtotalText = "Rs 0.00";
            DiscountText = "Rs 0.00";
            TotalText = "Rs 0.00";
            ChangeText = "Rs 0.00";
            return;
        }

        var tendered = 0L;
        if (IsCash && MoneyHelper.TryParseRupees(TenderedText, out var typed))
        {
            tendered = MoneyHelper.ToPaisa(typed);
        }

        var preview = _services.Sales.Preview(BuildInput(tendered), _user);
        if (!preview.Ok || preview.Value is null)
        {
            SubtotalText = FormatHelper.Rupees(Cart.Sum(line => line.LineTotalPaisa));
            DiscountText = "—";
            TotalText = "—";
            ChangeText = "—";
            SetStatus(preview.Error ?? "The total could not be calculated.", true);
            return;
        }

        SubtotalText = FormatHelper.Rupees(preview.Value.SubtotalPaisa);
        DiscountText = FormatHelper.Rupees(preview.Value.DiscountPaisa);
        TotalText = FormatHelper.Rupees(preview.Value.TotalPaisa);
        ChangeText = FormatHelper.Rupees(preview.Value.ChangePaisa);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}
