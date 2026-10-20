using System.Collections.ObjectModel;
using Area11.WPF.Models;
using Area11.WPF.Services;
using Area11.WPF.Utils;
using Area11.WPF.ViewModels.Base;

namespace Area11.WPF.ViewModels;

public sealed class PurchasesViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly string _userName;
    private readonly List<Product> _catalog = new();

    private Supplier? _selectedSupplier;
    private string _newSupplierName = string.Empty;
    private string _newSupplierPhone = string.Empty;
    private string _productSearch = string.Empty;
    private Product? _selectedProduct;
    private string _unit = "box";
    private string _quantity = "1";
    private string _batchNo = string.Empty;
    private string _expiry = string.Empty;
    private string _cost = string.Empty;
    private string _retail = string.Empty;
    private bool _isSample;
    private string _paidAmount = string.Empty;
    private string _note = string.Empty;
    private string _statusMessage = "Fill the delivery details and add lines.";
    private bool _isStatusError;

    public PurchasesViewModel(AppServices services, string userName)
    {
        _services = services;
        _userName = userName;

        AddLineCommand = new RelayCommand(AddLine);
        RemoveLineCommand = new RelayCommand(RemoveLine);
        SaveCommand = new RelayCommand(Save);
        AddSupplierCommand = new RelayCommand(AddSupplier);
        RefreshCommand = new RelayCommand(LoadLookups);

        LoadLookups();
    }

    public ObservableCollection<Supplier> Suppliers { get; } = new();

    public ObservableCollection<Product> Products { get; } = new();

    public ObservableCollection<Product> FilteredProducts { get; } = new();

    public ObservableCollection<PurchaseLineViewModel> Lines { get; } = new();

    public ObservableCollection<string> Units { get; } = new() { "box", "strip", "base" };

    public RelayCommand AddLineCommand { get; }

    public RelayCommand RemoveLineCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand AddSupplierCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public Supplier? SelectedSupplier
    {
        get => _selectedSupplier;
        set => SetProperty(ref _selectedSupplier, value);
    }

    public string NewSupplierName
    {
        get => _newSupplierName;
        set => SetProperty(ref _newSupplierName, value);
    }

    public string NewSupplierPhone
    {
        get => _newSupplierPhone;
        set => SetProperty(ref _newSupplierPhone, value);
    }

    public string ProductSearch
    {
        get => _productSearch;
        set
        {
            if (SetProperty(ref _productSearch, value))
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
            if (SetProperty(ref _selectedProduct, value) && value is not null)
            {
                Cost = MoneyHelper.ToRupees(value.CostPaisa).ToString("0.##");
                Retail = MoneyHelper.ToRupees(value.RetailPaisa).ToString("0.##");
            }
        }
    }

    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    public string Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public string BatchNo
    {
        get => _batchNo;
        set => SetProperty(ref _batchNo, value);
    }

    public string Expiry
    {
        get => _expiry;
        set => SetProperty(ref _expiry, value);
    }

    public string Cost
    {
        get => _cost;
        set => SetProperty(ref _cost, value);
    }

    public string Retail
    {
        get => _retail;
        set => SetProperty(ref _retail, value);
    }

    public bool IsSample
    {
        get => _isSample;
        set => SetProperty(ref _isSample, value);
    }

    public string PaidAmount
    {
        get => _paidAmount;
        set => SetProperty(ref _paidAmount, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
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

    public string TotalText => FormatHelper.Rupees(TotalPaisa);

    private long TotalPaisa => Lines.Where(l => !l.IsSample).Sum(l => l.CostPaisa * BaseQuantity(l));

    private void LoadLookups()
    {
        var suppliers = _services.Partners.GetSuppliers();
        Suppliers.Clear();
        if (suppliers.Ok && suppliers.Value is not null)
        {
            foreach (var supplier in suppliers.Value)
            {
                Suppliers.Add(supplier);
            }
        }

        SelectedSupplier ??= Suppliers.FirstOrDefault();

        var products = _services.Products.Search(null);
        _catalog.Clear();
        if (products.Ok && products.Value is not null)
        {
            _catalog.AddRange(products.Value);
        }

        FilterProducts();
    }

    private void FilterProducts()
    {
        var term = ProductSearch.Trim();
        FilteredProducts.Clear();

        foreach (var product in _catalog)
        {
            if (term.Length == 0
                || product.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (product.Barcode ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                FilteredProducts.Add(product);
            }
        }

        if (SelectedProduct is null || !FilteredProducts.Contains(SelectedProduct))
        {
            SelectedProduct = FilteredProducts.FirstOrDefault();
        }
    }

    private void AddSupplier()
    {
        var result = _services.Partners.CreateSupplier(NewSupplierName, NewSupplierPhone, null, _userName);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The supplier could not be saved.", true);
            return;
        }

        NewSupplierName = string.Empty;
        NewSupplierPhone = string.Empty;
        LoadLookups();
        SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == result.Value);
        SetStatus("Supplier added.", false);
    }

    private void AddLine()
    {
        if (SelectedProduct is null)
        {
            SetStatus("Select a product first.", true);
            return;
        }

        if (!int.TryParse(Quantity, out var quantity) || quantity <= 0)
        {
            SetStatus("Quantity must be a whole number of 1 or more.", true);
            return;
        }

        var cost = MoneyHelper.ParseRupeesOrZero(Cost);
        var retail = MoneyHelper.ParseRupeesOrZero(Retail);
        var expiry = Expiry.Trim();

        if (expiry.Length > 0 && !DateHelper.TryNormalizeExpiry(expiry, out _))
        {
            SetStatus("Expiry must look like 2027-06 or 2027-06-30.", true);
            return;
        }

        Lines.Add(new PurchaseLineViewModel
        {
            ProductId = SelectedProduct.Id,
            ProductName = SelectedProduct.Name,
            Unit = Unit,
            Quantity = quantity,
            BatchNo = BatchNo.Trim(),
            Expiry = expiry,
            CostPaisa = cost,
            RetailPaisa = retail,
            IsSample = IsSample
        });

        BatchNo = string.Empty;
        Expiry = string.Empty;
        Quantity = "1";
        IsSample = false;
        OnPropertyChanged(nameof(TotalText));
        SetStatus("Line added.", false);
    }

    private void RemoveLine()
    {
        if (Lines.Count == 0)
        {
            return;
        }

        Lines.Remove(Lines[^1]);
        OnPropertyChanged(nameof(TotalText));
    }

    private void Save()
    {
        if (SelectedSupplier is null)
        {
            SetStatus("Select or add a supplier first.", true);
            return;
        }

        if (Lines.Count == 0)
        {
            SetStatus("Add at least one line.", true);
            return;
        }

        var input = new PurchaseInput
        {
            SupplierId = SelectedSupplier.Id,
            Note = Note,
            PaidPaisa = MoneyHelper.ParseRupeesOrZero(PaidAmount),
            Lines = Lines.Select(line => line.ToInput()).ToList()
        };

        var result = _services.Purchases.Create(input, _userName);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The purchase could not be saved.", true);
            return;
        }

        var warnings = result.Warnings.Count > 0 ? "  ⚠ " + string.Join(" ", result.Warnings) : string.Empty;
        SetStatus($"{result.Value} saved — total {TotalText}.{warnings}", result.Warnings.Count > 0);

        Lines.Clear();
        PaidAmount = string.Empty;
        Note = string.Empty;
        OnPropertyChanged(nameof(TotalText));
        LoadLookups();
    }

    private int BaseQuantity(PurchaseLineViewModel line)
    {
        var product = _catalog.FirstOrDefault(p => p.Id == line.ProductId);
        if (product is null)
        {
            return line.Quantity;
        }

        return PurchaseService.ConvertToBase(line.Unit, line.Quantity, product.BoxStrips, product.StripUnits);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}
