using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Catalog screen. The right-hand form creates or edits one product; the grid on the left is
/// the live list. All rules (permission, validation, audit, partial update) live in
/// ProductService — this class only moves text between the screen and that service.
/// </summary>
public sealed class ProductsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private Product? _selectedProduct;
    private int? _editingId;

    private string _searchText = string.Empty;
    private string _name = string.Empty;
    private string _generic = string.Empty;
    private string _brand = string.Empty;
    private string _barcode = string.Empty;
    private string _rackNo = string.Empty;
    private string _room = string.Empty;
    private string _baseUnit = "tablet";
    private string _boxStripsText = "0";
    private string _stripTabletsText = "0";
    private string _costText = "0";
    private string _retailText = "0";
    private string _vipText = "0";
    private string _doctorText = "0";
    private string _reorderText = "0";
    private Category? _selectedCategory;
    private Company? _selectedCompany;
    private string _newCategoryName = string.Empty;
    private string _newCompanyName = string.Empty;
    private string _statusMessage = "Select a product to edit, or start a new one.";
    private bool _isStatusError;

    public ProductsViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        SaveCommand = new RelayCommand(Save);
        NewCommand = new RelayCommand(NewProduct);
        DeleteCommand = new RelayCommand(DeactivateSelected);
        RefreshCommand = new RelayCommand(Load);
        AddCategoryCommand = new RelayCommand(AddCategory);
        AddCompanyCommand = new RelayCommand(AddCompany);

        LoadLookups();
        Load();
    }

    public ObservableCollection<Product> Items { get; } = new();

    public ObservableCollection<Category> Categories { get; } = new();

    public ObservableCollection<Company> Companies { get; } = new();

    public ObservableCollection<string> BaseUnits { get; } = new() { "tablet", "capsule", "ml", "piece", "sachet" };

    public RelayCommand SaveCommand { get; }

    public RelayCommand NewCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand AddCategoryCommand { get; }

    public RelayCommand AddCompanyCommand { get; }

    public string HeaderText => _editingId is null ? "New product" : "Editing product";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                Load();
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
                LoadForm(value);
            }
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Generic
    {
        get => _generic;
        set => SetProperty(ref _generic, value);
    }

    public string Brand
    {
        get => _brand;
        set => SetProperty(ref _brand, value);
    }

    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string RackNo
    {
        get => _rackNo;
        set => SetProperty(ref _rackNo, value);
    }

    public string Room
    {
        get => _room;
        set => SetProperty(ref _room, value);
    }

    public string BaseUnit
    {
        get => _baseUnit;
        set => SetProperty(ref _baseUnit, value);
    }

    public string BoxStripsText
    {
        get => _boxStripsText;
        set => SetProperty(ref _boxStripsText, value);
    }

    public string StripTabletsText
    {
        get => _stripTabletsText;
        set => SetProperty(ref _stripTabletsText, value);
    }

    public string CostText
    {
        get => _costText;
        set => SetProperty(ref _costText, value);
    }

    public string RetailText
    {
        get => _retailText;
        set => SetProperty(ref _retailText, value);
    }

    public string VipText
    {
        get => _vipText;
        set => SetProperty(ref _vipText, value);
    }

    public string DoctorText
    {
        get => _doctorText;
        set => SetProperty(ref _doctorText, value);
    }

    public string ReorderText
    {
        get => _reorderText;
        set => SetProperty(ref _reorderText, value);
    }

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public Company? SelectedCompany
    {
        get => _selectedCompany;
        set => SetProperty(ref _selectedCompany, value);
    }

    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetProperty(ref _newCategoryName, value);
    }

    public string NewCompanyName
    {
        get => _newCompanyName;
        set => SetProperty(ref _newCompanyName, value);
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

    private void LoadLookups()
    {
        var categories = _services.Products.GetCategories();
        Categories.Clear();
        if (categories.Ok && categories.Value is not null)
        {
            foreach (var category in categories.Value)
            {
                Categories.Add(category);
            }
        }

        var companies = _services.Products.GetCompanies();
        Companies.Clear();
        if (companies.Ok && companies.Value is not null)
        {
            foreach (var company in companies.Value)
            {
                Companies.Add(company);
            }
        }
    }

    private void Load()
    {
        var result = _services.Products.Search(SearchText);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The product list could not be loaded.", true);
            return;
        }

        Items.Clear();
        foreach (var product in result.Value)
        {
            Items.Add(product);
        }

        SetStatus($"{Items.Count} product(s).", false);
    }

    private void NewProduct()
    {
        _editingId = null;
        _selectedProduct = null;
        OnPropertyChanged(nameof(SelectedProduct));

        Name = string.Empty;
        Generic = string.Empty;
        Brand = string.Empty;
        Barcode = string.Empty;
        RackNo = string.Empty;
        Room = string.Empty;
        BaseUnit = "tablet";
        BoxStripsText = "0";
        StripTabletsText = "0";
        CostText = "0";
        RetailText = "0";
        VipText = "0";
        DoctorText = "0";
        ReorderText = "0";
        SelectedCategory = Categories.FirstOrDefault();
        SelectedCompany = Companies.FirstOrDefault();

        OnPropertyChanged(nameof(HeaderText));
        SetStatus("New product — fill the name and rates.", false);
    }

    private void LoadForm(Product product)
    {
        _editingId = product.Id;

        Name = product.Name;
        Generic = product.Generic ?? string.Empty;
        Brand = product.Brand ?? string.Empty;
        Barcode = product.Barcode ?? string.Empty;
        RackNo = product.RackNo ?? string.Empty;
        Room = product.Room ?? string.Empty;
        BaseUnit = product.BaseUnit;
        BoxStripsText = product.BoxStrips.ToString();
        StripTabletsText = product.StripTablets.ToString();
        CostText = Money.Edit(product.CostPaisa);
        RetailText = Money.Edit(product.RetailPaisa);
        VipText = Money.Edit(product.VipPaisa);
        DoctorText = Money.Edit(product.DoctorPaisa);
        ReorderText = product.ReorderLevel.ToString("0.##");

        SelectedCategory = Categories.FirstOrDefault(c => c.Id == product.CategoryId);
        SelectedCompany = Companies.FirstOrDefault(c => c.Id == product.CompanyId);

        OnPropertyChanged(nameof(HeaderText));
        SetStatus($"Editing {product.Name}.", false);
    }

    private void Save()
    {
        if (!Money.TryParse(CostText, out var cost) || !Money.TryParse(RetailText, out var retail)
            || !Money.TryParse(VipText, out var vip) || !Money.TryParse(DoctorText, out var doctor))
        {
            SetStatus("Rates must be numbers in rupees (for example 245.50).", true);
            return;
        }

        if (!int.TryParse(BoxStripsText, out var boxStrips) || !int.TryParse(StripTabletsText, out var stripTablets))
        {
            SetStatus("The pack formula must be whole numbers.", true);
            return;
        }

        if (!double.TryParse(ReorderText, out var reorder))
        {
            reorder = 0;
        }

        var patch = new ProductPatch
        {
            Name = Name,
            Generic = Generic,
            Brand = Brand,
            Barcode = Barcode,
            CategoryId = SelectedCategory?.Id,
            CompanyId = SelectedCompany?.Id,
            RackNo = RackNo,
            Room = Room,
            BaseUnit = BaseUnit,
            BoxStrips = boxStrips,
            StripTablets = stripTablets,
            CostPaisa = Money.ToPaisa(cost),
            RetailPaisa = Money.ToPaisa(retail),
            VipPaisa = Money.ToPaisa(vip),
            DoctorPaisa = Money.ToPaisa(doctor),
            ReorderLevel = reorder
        };

        var result = _editingId is null
            ? _services.Products.Create(_user, patch)
            : _services.Products.Update(_user, _editingId.Value, patch);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The product could not be saved.", true);
            return;
        }

        var message = _editingId is null ? "Product added." : "Product updated.";
        Load();
        NewProduct();
        SetStatus(message, false);
    }

    private void DeactivateSelected()
    {
        if (SelectedProduct is null)
        {
            SetStatus("Select a product in the list first.", true);
            return;
        }

        var name = SelectedProduct.Name;
        var result = _services.Products.Deactivate(_user, SelectedProduct.Id);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The product could not be removed.", true);
            return;
        }

        SetStatus($"{name} was removed from the list (history is kept).", false);
        Load();
        NewProduct();
    }

    private void AddCategory()
    {
        var result = _services.Products.AddCategory(_user, NewCategoryName);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The category could not be saved.", true);
            return;
        }

        var id = result.Value;
        NewCategoryName = string.Empty;
        LoadLookups();
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == id);
        SetStatus("Category added.", false);
    }

    private void AddCompany()
    {
        var result = _services.Products.AddCompany(_user, NewCompanyName);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The company could not be saved.", true);
            return;
        }

        var id = result.Value;
        NewCompanyName = string.Empty;
        LoadLookups();
        SelectedCompany = Companies.FirstOrDefault(c => c.Id == id);
        SetStatus("Company added.", false);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}
