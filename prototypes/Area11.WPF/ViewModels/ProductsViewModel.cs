using System.Collections.ObjectModel;
using Area11.WPF.Models;
using Area11.WPF.Services;
using Area11.WPF.Utils;
using Area11.WPF.ViewModels.Base;

namespace Area11.WPF.ViewModels;

public sealed class ProductsViewModel : ObservableObject
{
    private readonly ProductService _products;
    private readonly string _userName;

    private string _searchText = string.Empty;
    private Category? _selectedCategory;
    private bool _lowStockOnly;
    private bool _isFormOpen;
    private int? _editingId;
    private string _formTitle = "New product";
    private string _statusMessage = string.Empty;
    private bool _isStatusError;
    private ProductRow? _selectedProduct;

    private string _name = string.Empty;
    private string _barcode = string.Empty;
    private string _generic = string.Empty;
    private string _newCategoryName = string.Empty;
    private Category? _formCategory;
    private Company? _formCompany;
    private string _newCompanyName = string.Empty;
    private string _boxStrips = "1";
    private string _stripUnits = "1";
    private string _cost = "0";
    private string _retail = "0";
    private string _vip = "0";
    private string _doctor = "0";
    private string _rack = string.Empty;
    private string _room = string.Empty;
    private string _reorder = "0";

    public ProductsViewModel(AppServices services, string userName)
    {
        _products = services.Products;
        _userName = userName;

        RefreshCommand = new RelayCommand(LoadProducts);
        ToggleFormCommand = new RelayCommand(ToggleForm);
        SaveCommand = new RelayCommand(Save);
        CancelFormCommand = new RelayCommand(CloseForm);
        EditCommand = new RelayCommand(StartEdit);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        LoadLookups();
        LoadProducts();
    }

    public ObservableCollection<ProductRow> Products { get; } = new();

    public ObservableCollection<Category> Categories { get; } = new();

    public ObservableCollection<Company> Companies { get; } = new();

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ToggleFormCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand CancelFormCommand { get; }

    public RelayCommand EditCommand { get; }

    public RelayCommand ClearSearchCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                LoadProducts();
            }
        }
    }

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                LoadProducts();
            }
        }
    }

    public bool LowStockOnly
    {
        get => _lowStockOnly;
        set
        {
            if (SetProperty(ref _lowStockOnly, value))
            {
                LoadProducts();
            }
        }
    }

    public Category? FormCategory
    {
        get => _formCategory;
        set => SetProperty(ref _formCategory, value);
    }

    public bool IsFormOpen
    {
        get => _isFormOpen;
        private set => SetProperty(ref _isFormOpen, value);
    }

    public string FormTitle
    {
        get => _formTitle;
        private set => SetProperty(ref _formTitle, value);
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

    public ProductRow? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string Generic
    {
        get => _generic;
        set => SetProperty(ref _generic, value);
    }

    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetProperty(ref _newCategoryName, value);
    }

    public Company? FormCompany
    {
        get => _formCompany;
        set => SetProperty(ref _formCompany, value);
    }

    public string NewCompanyName
    {
        get => _newCompanyName;
        set => SetProperty(ref _newCompanyName, value);
    }

    public string BoxStrips
    {
        get => _boxStrips;
        set => SetProperty(ref _boxStrips, value);
    }

    public string StripUnits
    {
        get => _stripUnits;
        set => SetProperty(ref _stripUnits, value);
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

    public string Vip
    {
        get => _vip;
        set => SetProperty(ref _vip, value);
    }

    public string Doctor
    {
        get => _doctor;
        set => SetProperty(ref _doctor, value);
    }

    public string Rack
    {
        get => _rack;
        set => SetProperty(ref _rack, value);
    }

    public string Room
    {
        get => _room;
        set => SetProperty(ref _room, value);
    }

    public string Reorder
    {
        get => _reorder;
        set => SetProperty(ref _reorder, value);
    }

    private void LoadLookups()
    {
        var filterCategoryId = SelectedCategory?.Id ?? 0;
        var formCategoryId = FormCategory?.Id ?? 0;
        var formCompanyId = FormCompany?.Id;

        Categories.Clear();
        Categories.Add(new Category { Id = 0, Name = "All categories" });
        foreach (var category in _products.GetCategories())
        {
            Categories.Add(category);
        }

        SelectedCategory = Categories.FirstOrDefault(c => c.Id == filterCategoryId) ?? Categories[0];
        FormCategory = Categories.FirstOrDefault(c => c.Id == formCategoryId) ?? Categories[0];

        Companies.Clear();
        Companies.Add(new Company { Id = 0, Name = "(none)" });
        foreach (var company in _products.GetCompanies())
        {
            Companies.Add(company);
        }

        FormCompany = Companies.FirstOrDefault(c => c.Id == formCompanyId) ?? Companies[0];
    }

    private void LoadProducts()
    {
        var result = _products.Search(
            SearchText,
            SelectedCategory is { Id: > 0 } ? SelectedCategory.Id : null,
            LowStockOnly);

        Products.Clear();

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "Products could not be loaded.", true);
            return;
        }

        foreach (var product in result.Value)
        {
            Products.Add(new ProductRow
            {
                Id = product.Id,
                Name = product.Name,
                Barcode = product.Barcode ?? string.Empty,
                Category = product.CategoryName ?? string.Empty,
                Pack = $"{product.BoxStrips} x {product.StripUnits}",
                Retail = FormatHelper.Rupees(product.RetailPaisa),
                Stock = product.IsLowStock
                    ? $"{FormatHelper.Quantity(product.StockBase)}  ⚠"
                    : FormatHelper.Quantity(product.StockBase)
            });
        }
    }

    private void ToggleForm()
    {
        if (IsFormOpen)
        {
            CloseForm();
            return;
        }

        ResetForm();
        IsFormOpen = true;
    }

    private void CloseForm()
    {
        IsFormOpen = false;
        ResetForm();
    }

    private void ResetForm()
    {
        _editingId = null;
        FormTitle = "New product";
        Name = string.Empty;
        Barcode = string.Empty;
        Generic = string.Empty;
        NewCategoryName = string.Empty;
        NewCompanyName = string.Empty;
        BoxStrips = "1";
        StripUnits = "1";
        Cost = "0";
        Retail = "0";
        Vip = "0";
        Doctor = "0";
        Rack = string.Empty;
        Room = string.Empty;
        Reorder = "0";
        FormCategory = Categories.FirstOrDefault();
        FormCompany = Companies.FirstOrDefault();
    }

    private void StartEdit()
    {
        if (SelectedProduct is null)
        {
            SetStatus("Select a product row first.", true);
            return;
        }

        var result = _products.GetById(SelectedProduct.Id);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The product could not be loaded.", true);
            return;
        }

        var product = result.Value;
        _editingId = product.Id;
        FormTitle = $"Edit product #{product.Id}";
        Name = product.Name;
        Barcode = product.Barcode ?? string.Empty;
        Generic = product.Generic ?? string.Empty;
        NewCategoryName = string.Empty;
        NewCompanyName = string.Empty;
        BoxStrips = product.BoxStrips.ToString();
        StripUnits = product.StripUnits.ToString();
        Cost = MoneyHelper.ToRupees(product.CostPaisa).ToString("0.##");
        Retail = MoneyHelper.ToRupees(product.RetailPaisa).ToString("0.##");
        Vip = MoneyHelper.ToRupees(product.VipPaisa).ToString("0.##");
        Doctor = MoneyHelper.ToRupees(product.DoctorPaisa).ToString("0.##");
        Rack = product.Rack ?? string.Empty;
        Room = product.Room ?? string.Empty;
        Reorder = product.ReorderLevel.ToString();

        FormCategory = Categories.FirstOrDefault(c => c.Id == product.CategoryId) ?? Categories[0];
        FormCompany = Companies.FirstOrDefault(c => c.Id == product.CompanyId) ?? Companies[0];
        IsFormOpen = true;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            SetStatus("Product name is required.", true);
            return;
        }

        if (!int.TryParse(BoxStrips, out var boxStrips) || boxStrips < 1)
        {
            SetStatus("Box → strips must be a whole number of 1 or more.", true);
            return;
        }

        if (!int.TryParse(StripUnits, out var stripUnits) || stripUnits < 1)
        {
            SetStatus("Strips → units must be a whole number of 1 or more.", true);
            return;
        }

        if (!TryAmount(Cost, out var cost))
        {
            SetStatus("Cost must be a valid amount.", true);
            return;
        }

        if (!TryAmount(Retail, out var retail))
        {
            SetStatus("Retail price must be a valid amount.", true);
            return;
        }

        TryAmount(Vip, out var vip);
        TryAmount(Doctor, out var doctor);
        var reorder = int.TryParse(Reorder, out var parsedReorder) ? Math.Max(0, parsedReorder) : 0;

        var input = new ProductInput
        {
            Name = Name,
            Barcode = Barcode,
            Generic = Generic,
            CategoryId = FormCategory is { Id: > 0 } ? FormCategory.Id : null,
            NewCategoryName = NewCategoryName,
            CompanyId = FormCompany is { Id: > 0 } ? FormCompany.Id : null,
            NewCompanyName = NewCompanyName,
            BoxStrips = boxStrips,
            StripUnits = stripUnits,
            CostPaisa = cost,
            RetailPaisa = retail,
            VipPaisa = vip,
            DoctorPaisa = doctor,
            Rack = Rack,
            Room = Room,
            ReorderLevel = reorder
        };

        if (_editingId is not null)
        {
            var update = _products.Update(_editingId.Value, input, _userName);
            if (!update.Ok)
            {
                SetStatus(update.Error ?? "The product could not be updated.", true);
                return;
            }

            LoadLookups();
            LoadProducts();
            IsFormOpen = false;
            ResetForm();
            SetStatus(Combine("Product updated.", update.Warnings), false);
            return;
        }

        var create = _products.Create(input, _userName);
        if (!create.Ok)
        {
            SetStatus(create.Error ?? "The product could not be saved.", true);
            return;
        }

        LoadLookups();
        LoadProducts();
        IsFormOpen = false;
        ResetForm();
        SetStatus(Combine("Product saved.", create.Warnings), false);
    }

    private static bool TryAmount(string? text, out long paisa)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            paisa = 0;
            return true;
        }

        if (!MoneyHelper.TryParseRupees(text, out var rupees) || rupees < 0)
        {
            paisa = 0;
            return false;
        }

        paisa = MoneyHelper.ToPaisa(rupees);
        return true;
    }

    private static string Combine(string message, IReadOnlyList<string> warnings) =>
        warnings.Count == 0 ? message : $"{message}  ⚠ {string.Join(" ", warnings)}";

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}
