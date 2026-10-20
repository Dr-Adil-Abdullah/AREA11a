using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The counter. The total on the screen is always produced by SaleService.Quote — the same
/// calculation that saves the bill — so what the cashier reads is what is stored.
/// </summary>
public sealed class PosViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly List<Product> _catalog = new();

    private string _searchText = string.Empty;
    private Product? _selectedProduct;
    private string _unit = "strip";
    private string _quantity = "1";
    private Customer? _selectedCustomer;
    private string _discountPercent = "0";
    private string _paymentMethod = "cash";
    private string _tenderedText = string.Empty;

    private string _subtotalText = "Rs 0";
    private string _discountText = "Rs 0";
    private string _totalText = "Rs 0";
    private string _changeText = "Rs 0";
    private string _statusMessage = "Ready.";
    private bool _isStatusError;
    private string _lastBill = string.Empty;
    private int _lastSaleId;

    public PosViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        AddCommand = new RelayCommand(AddToCart);
        RemoveCommand = new RelayCommand(RemoveLast);
        ClearCommand = new RelayCommand(ClearCart);
        CompleteCommand = new RelayCommand(CompleteSale);
        PrintLastCommand = new RelayCommand(PrintLast, () => _lastSaleId > 0);
        RefreshCommand = new RelayCommand(LoadLookups);

        LoadLookups();
    }

    public ObservableCollection<Product> Products { get; } = new();

    public ObservableCollection<PosCartLine> Cart { get; } = new();

    public ObservableCollection<Customer> Customers { get; } = new();

    public ObservableCollection<string> Units { get; } = new() { "base", "strip", "box" };

    public ObservableCollection<string> PaymentMethods { get; } = new() { "cash", "credit" };

    public RelayCommand AddCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand CompleteCommand { get; }

    public RelayCommand PrintLastCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public string CashierName => _user.Name;

    public int MaxDiscountPercent => _user.Kind switch
    {
        UserRole.Owner => 100,
        UserRole.Manager => _services.Settings.GetInt("discount.maxPercentManager", 20),
        _ => _services.Settings.GetInt("discount.maxPercentCashier", 5)
    };

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterProducts();
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
                OnPropertyChanged(nameof(PriceHint));
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
                OnPropertyChanged(nameof(PriceHint));
            }
        }
    }

    public string Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                OnPropertyChanged(nameof(CustomerHint));
                Recalculate();
            }
        }
    }

    public string DiscountPercent
    {
        get => _discountPercent;
        set
        {
            if (SetProperty(ref _discountPercent, value))
            {
                Recalculate();
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
                Recalculate();
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
                Recalculate();
            }
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

    public string LastBill
    {
        get => _lastBill;
        private set => SetProperty(ref _lastBill, value);
    }

    public string CustomerHint => SelectedCustomer is null || SelectedCustomer.Id == 0
        ? "Cash customer"
        : $"{SelectedCustomer.Category} · balance {Money.Format(SelectedCustomer.BalancePaisa)}";

    public string PriceHint
    {
        get
        {
            if (SelectedProduct is null)
            {
                return "Pick a medicine";
            }

            var units = Unit switch
            {
                "box" => SelectedProduct.UnitsPerBox,
                "strip" => SelectedProduct.UnitsPerStrip,
                _ => 1
            };

            var price = SelectedProduct.RetailPaisa * units;
            return $"{Unit}: {Money.Format(Calc.Round(price))}";
        }
    }

    private void LoadLookups()
    {
        var products = _services.Products.Search();
        _catalog.Clear();
        if (products.Ok && products.Value is not null)
        {
            _catalog.AddRange(products.Value);
        }

        var customers = _services.Partners.GetCustomers();
        Customers.Clear();
        Customers.Add(new Customer { Id = 0, Name = "Walk-in customer", Category = "normal" });
        if (customers.Ok && customers.Value is not null)
        {
            foreach (var customer in customers.Value)
            {
                Customers.Add(customer);
            }
        }

        SelectedCustomer = Customers[0];
        FilterProducts();
        Recalculate();
    }

    private void FilterProducts()
    {
        var term = SearchText.Trim();
        Products.Clear();

        foreach (var product in _catalog)
        {
            if (term.Length == 0
                || product.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (product.Generic ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)
                || (product.Barcode ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
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
            SetStatus("Pick a medicine first.", true);
            return;
        }

        if (!Qty.TryParse(Quantity, out var quantity) || quantity <= 0)
        {
            SetStatus("Quantity must be greater than zero.", true);
            return;
        }

        var existing = Cart.FirstOrDefault(line => line.ProductId == SelectedProduct.Id && line.Unit == Unit);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            Recalculate();
        }
        else
        {
            Cart.Add(new PosCartLine
            {
                ProductId = SelectedProduct.Id,
                Name = SelectedProduct.Name,
                Unit = Unit,
                Quantity = quantity
            });
        }

        Quantity = "1";
        SetStatus($"{SelectedProduct.Name} added.", false);
        Recalculate();
    }

    private void RemoveLast()
    {
        if (Cart.Count == 0)
        {
            return;
        }

        Cart.RemoveAt(Cart.Count - 1);
        Recalculate();
    }

    private void ClearCart()
    {
        Cart.Clear();
        TenderedText = string.Empty;
        DiscountPercent = "0";
        SetStatus("Cart cleared.", false);
        Recalculate();
    }

    private void CompleteSale()
    {
        if (Cart.Count == 0)
        {
            SetStatus("The cart is empty.", true);
            return;
        }

        if (!Money.TryParse(TenderedText, out _))
        {
            SetStatus("Cash received must be a number.", true);
            return;
        }

        var result = _services.Sales.Create(BuildInput(), _user);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The bill could not be saved.", true);
            return;
        }

        var sale = result.Value;
        LastBill = $"{sale.Code} · total {Money.Format(sale.TotalPaisa)}" +
                   (sale.ChangePaisa > 0 ? $" · change {Money.Format(sale.ChangePaisa)}" : string.Empty) +
                   (sale.DuePaisa > 0 ? $" · due {Money.Format(sale.DuePaisa)}" : string.Empty);

        var warnings = sale.Warnings.Count > 0 ? "  ⚠ " + string.Join(" ", sale.Warnings) : string.Empty;
        SetStatus($"Bill saved.{warnings}", sale.Warnings.Count > 0);

        // Owner rules D-05 / D-23: printing is instant, with no dialog. The bill is already saved,
        // so a printer problem is a warning on this screen - never a lost sale (Z-07).
        _lastSaleId = sale.SaleId;
        PrintLastCommand.RaiseCanExecuteChanged();

        if (_services.Settings.GetBool("printer.autoPrint", true))
        {
            Print(_lastSaleId);
        }

        Cart.Clear();
        TenderedText = string.Empty;
        DiscountPercent = "0";
        LoadLookups();
    }

    private SaleInput BuildInput()
    {
        decimal.TryParse(DiscountPercent, out var percent);

        var tendered = 0L;
        if (IsCash && Money.TryParse(TenderedText, out var rupees) && rupees > 0)
        {
            tendered = Money.ToPaisa(rupees);
        }

        return new SaleInput
        {
            CustomerId = SelectedCustomer is { Id: > 0 } ? SelectedCustomer.Id : null,
            DiscountPercent = percent,
            PaymentMethod = PaymentMethod,
            TenderedPaisa = tendered,
            Lines = Cart.Select(line => new SaleLineInput
            {
                ProductId = line.ProductId,
                Unit = line.Unit,
                Quantity = line.Quantity
            }).ToList()
        };
    }

    private void Recalculate()
    {
        if (Cart.Count == 0)
        {
            SubtotalText = "Rs 0";
            DiscountText = "Rs 0";
            TotalText = "Rs 0";
            ChangeText = "Rs 0";
            return;
        }

        var quote = _services.Sales.Quote(BuildInput(), _user);
        if (!quote.Ok || quote.Value is null)
        {
            SubtotalText = "—";
            DiscountText = "—";
            TotalText = "—";
            ChangeText = "—";
            SetStatus(quote.Error ?? "The total could not be calculated.", true);
            return;
        }

        SubtotalText = Money.Format(quote.Value.SubtotalPaisa);
        DiscountText = Money.Format(quote.Value.DiscountPaisa);
        TotalText = Money.Format(quote.Value.TotalPaisa);
        ChangeText = Money.Format(quote.Value.ChangePaisa);

        if (quote.Value.GoesNegative)
        {
            SetStatus("⚠ This bill takes stock into MINUS. It will still be saved (owner rule U-30).", true);
        }
    }

    private void PrintLast()
    {
        if (_lastSaleId <= 0)
        {
            SetStatus("There is no bill to print yet.", true);
            return;
        }

        Print(_lastSaleId);
    }

    private void Print(int saleId)
    {
        var document = _services.Receipts.Build(saleId);
        if (!document.Ok || document.Value is null)
        {
            SetStatus(document.Error ?? "The receipt could not be prepared.", true);
            return;
        }

        var printed = _services.Printer.Print(document.Value);
        if (!printed.Ok)
        {
            SetStatus(printed.Error ?? "The receipt could not be printed.", true);
            return;
        }

        SetStatus($"Receipt printed ({document.Value.Width} characters wide).", false);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}

/// <summary>One cart line. Quantities are double because the schema stores REAL.</summary>
public sealed class PosCartLine : ObservableObject
{
    private double _quantity;

    public int ProductId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Unit { get; init; } = "base";

    public double Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(QuantityText));
            }
        }
    }

    public string QuantityText => Qty.Format(Quantity);

    public string UnitLabel => Unit;
}
