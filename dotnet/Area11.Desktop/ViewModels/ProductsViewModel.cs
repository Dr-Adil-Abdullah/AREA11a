using System.Collections.ObjectModel;
using System.IO;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Catalog screen. The right-hand form creates or edits one product; the grid on the left is
/// the live list. All rules (permission, validation, audit, partial update) live in
/// ProductService — this class only moves text between the screen and that service.
/// </summary>
public sealed class ProductsViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(
        Items.Count == 0,
        "Products",
        searched: SearchText.Trim().Length > 0 || HasFilter);

    private readonly AppServices _services;
    private readonly User _user;

    private Product? _selectedProduct;
    private int? _editingId;

    private string _searchText = string.Empty;
    private string _searchNote = string.Empty;
    private bool _isSearchGuessing;
    private string _name = string.Empty;
    private string _generic = string.Empty;
    private string _brand = string.Empty;
    private string _barcode = string.Empty;
    private string _rackNo = string.Empty;
    private string _room = string.Empty;
    private string _baseUnit = "tablet";
    private string _strengthText = string.Empty;
    private string _strengthUnit = "ml";
    private string _strengthFilter = string.Empty;
    private int? _categoryFilter;
    private int? _companyFilter;
    private bool _lowStockOnly;
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
        ManageCatalogCommand = new RelayCommand(ManageCatalog);
        ManageUnitsCommand = new RelayCommand(ManageUnits);
        ClearFilterCommand = new RelayCommand(ClearFilter);
        OpenCategoryFilterCommand = new RelayCommand<int>(id => ApplyFilter(categoryId: id));
        OpenCompanyFilterCommand = new RelayCommand<int>(id => ApplyFilter(companyId: id));
        OpenProductPhotoCommand = new RelayCommand<int>(OpenProductPhoto);
        PhotosCommand = new RelayCommand(OpenPhotos);
        BarcodesCommand = new RelayCommand(OpenBarcodes);

        Editor = new CustomFieldsEditorViewModel(services, user, CustomFieldEntity.Product);

        LoadLookups();
        Load();
    }

    public ObservableCollection<Product> Items { get; } = new();

    public ObservableCollection<Category> Categories { get; } = new();

    public ObservableCollection<Company> Companies { get; } = new();

    /// <summary>Active managed units; the service validates the same names again on save.</summary>
    public ObservableCollection<string> BaseUnits { get; } = new();

    public ObservableCollection<CategoryTagOption> CategoryTagOptions { get; } = new();

    public RelayCommand SaveCommand { get; }

    public RelayCommand NewCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand AddCategoryCommand { get; }

    /// <summary>The photo gallery of the selected medicine (F-41).</summary>
    public RelayCommand PhotosCommand { get; }

    /// <summary>The several printed codes of this medicine (F-43). Needs a saved product.</summary>
    public RelayCommand BarcodesCommand { get; }

    /// <summary>The owner's own questions for a medicine (F-42) - drawn only if he made any.</summary>
    public CustomFieldsEditorViewModel Editor { get; }

    public RelayCommand AddCompanyCommand { get; }

    /// <summary>E-04: rename, order, put away and restore catalog rows in one small window.</summary>
    public RelayCommand ManageCatalogCommand { get; }

    public RelayCommand ManageUnitsCommand { get; }

    public RelayCommand ClearFilterCommand { get; }

    public RelayCommand<int> OpenCategoryFilterCommand { get; }

    public RelayCommand<int> OpenCompanyFilterCommand { get; }

    public RelayCommand<int> OpenProductPhotoCommand { get; }

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
            if (!SetProperty(ref _selectedProduct, value))
            {
                return;
            }

            if (value is not null)
            {
                LoadForm(value);
                Editor.Load(value.Id);
            }
            else
            {
                Editor.Load(0);
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

    public string StrengthText
    {
        get => _strengthText;
        set => SetProperty(ref _strengthText, value);
    }

    public string StrengthUnit
    {
        get => _strengthUnit;
        set => SetProperty(ref _strengthUnit, value);
    }

    /// <summary>Optional exact strength filter, e.g. 50 ml; it does not change stock quantities.</summary>
    public string StrengthFilter
    {
        get => _strengthFilter;
        set
        {
            if (SetProperty(ref _strengthFilter, value))
            {
                Load();
            }
        }
    }

    public string FilterText
    {
        get
        {
            var parts = new List<string>();
            if (_categoryFilter is int categoryId)
            {
                parts.Add("category: " + (Categories.FirstOrDefault(row => row.Id == categoryId)?.Name ?? categoryId.ToString()));
            }
            if (_companyFilter is int companyId)
            {
                parts.Add("company: " + (Companies.FirstOrDefault(row => row.Id == companyId)?.Name ?? companyId.ToString()));
            }
            if (_lowStockOnly)
            {
                parts.Add("low stock");
            }
            if (!string.IsNullOrWhiteSpace(_strengthFilter))
            {
                parts.Add("strength: " + _strengthFilter);
            }
            return parts.Count == 0 ? "All active medicines" : "Filtered — " + string.Join(" · ", parts);
        }
    }

    public bool HasFilter => _categoryFilter.HasValue || _companyFilter.HasValue || _lowStockOnly || !string.IsNullOrWhiteSpace(_strengthFilter);

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

    /// <summary>The "did you mean" sentence under the search box (F-50); empty for an exact search.</summary>
    public string SearchNote
    {
        get => _searchNote;
        private set => SetProperty(ref _searchNote, value);
    }

    /// <summary>True while the grid below is a typo suggestion rather than a result.</summary>
    public bool IsSearchGuessing
    {
        get => _isSearchGuessing;
        private set => SetProperty(ref _isSearchGuessing, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    private void LoadLookups()
    {
        var selectedTags = CategoryTagOptions.Where(row => row.IsSelected).Select(row => row.Id).ToHashSet();
        var categories = _services.Products.GetCategories();
        Categories.Clear();
        if (categories.Ok && categories.Value is not null)
        {
            foreach (var category in categories.Value)
            {
                Categories.Add(category);
            }
        }

        CategoryTagOptions.Clear();
        foreach (var category in Categories)
        {
            CategoryTagOptions.Add(new CategoryTagOption(category.Id, category.Name, selectedTags.Contains(category.Id)));
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

        var units = _services.Products.GetUnits();
        BaseUnits.Clear();
        if (units.Ok && units.Value is not null)
        {
            foreach (var unit in units.Value)
            {
                BaseUnits.Add(unit.Name);
            }
        }
        if (BaseUnits.Count > 0 && !BaseUnits.Contains(BaseUnit))
        {
            BaseUnit = BaseUnits[0];
        }
        if (BaseUnits.Count > 0 && !BaseUnits.Contains(StrengthUnit))
        {
            StrengthUnit = BaseUnits.Contains("ml") ? "ml" : BaseUnits[0];
        }
        OnPropertyChanged(nameof(FilterText));
    }

    private void Load()
    {
        // F-50: exact first; when nothing matches, the closest medicines with a sentence saying so.
        var result = _services.Products.SearchSmart(SearchText, _categoryFilter, _companyFilter, _lowStockOnly, _strengthFilter);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The product list could not be loaded.", true);
            return;
        }

        Items.Clear();

        foreach (var product in result.Value.Items)
        {
            product.PhotoPath = ResolvePhotoPath(product);
            Items.Add(product);
        }

        OnPropertyChanged(nameof(FilterText));
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(EmptyHint));
        SearchNote = result.Value.IsFuzzy ? result.Value.NoteLabel : string.Empty;
        IsSearchGuessing = result.Value.IsFuzzy;

        SetStatus(result.Value.SummaryLabel, result.Value.IsEmpty && !result.Value.IsFuzzy);
    }

    private void NewProduct()
    {
        _editingId = null;
        _selectedProduct = null;
        OnPropertyChanged(nameof(SelectedProduct));
        Editor.Load(0);

        Name = string.Empty;
        Generic = string.Empty;
        Brand = string.Empty;
        Barcode = string.Empty;
        RackNo = string.Empty;
        Room = string.Empty;
        BaseUnit = BaseUnits.Contains("tablet") ? "tablet" : BaseUnits.FirstOrDefault() ?? "tablet";
        StrengthText = string.Empty;
        StrengthUnit = BaseUnits.Contains("ml") ? "ml" : BaseUnits.FirstOrDefault() ?? "ml";
        foreach (var option in CategoryTagOptions)
        {
            option.IsSelected = false;
        }
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
        StrengthText = product.StrengthValue > 0 ? Qty.Edit(product.StrengthValue) : string.Empty;
        StrengthUnit = string.IsNullOrWhiteSpace(product.StrengthUnit)
            ? (BaseUnits.Contains("ml") ? "ml" : BaseUnits.FirstOrDefault() ?? "ml")
            : product.StrengthUnit;
        foreach (var option in CategoryTagOptions)
        {
            option.IsSelected = product.CategoryTagIds.Contains(option.Id);
        }
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

        if (!Qty.TryParse(StrengthText, out var strength) || !double.IsFinite(strength) || strength < 0)
        {
            SetStatus("Strength must be a number of zero or more, for example 50 with unit ml.", true);
            return;
        }
        if (strength > 0 && string.IsNullOrWhiteSpace(StrengthUnit))
        {
            SetStatus("Choose a managed unit for the strength.", true);
            return;
        }

        var patch = new ProductPatch
        {
            Name = Name,
            Generic = Generic,
            Brand = Brand,
            Barcode = Barcode,
            CategoryId = SelectedCategory?.Id,
            CategoryTagIds = CategoryTagOptions.Where(option => option.IsSelected && option.Id != SelectedCategory?.Id)
                .Select(option => option.Id).Distinct().ToList(),
            CompanyId = SelectedCompany?.Id,
            RackNo = RackNo,
            Room = Room,
            BaseUnit = BaseUnit,
            StrengthValue = strength,
            StrengthUnit = strength > 0 ? StrengthUnit : string.Empty,
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

    /// <summary>One predictable doorway for category, company, dashboard and strength links.</summary>
    public void ApplyFilter(
        int? categoryId = null,
        int? companyId = null,
        bool lowStockOnly = false,
        string? strength = null)
    {
        _categoryFilter = categoryId is > 0 ? categoryId : null;
        _companyFilter = companyId is > 0 ? companyId : null;
        _lowStockOnly = lowStockOnly;
        _strengthFilter = (strength ?? string.Empty).Trim();
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(FilterText));
        OnPropertyChanged(nameof(HasFilter));
        Load();
    }

    private void ClearFilter() => ApplyFilter();

    private void ManageUnits()
    {
        var selected = BaseUnit;
        UnitWindowDoor.Show(_services, _user);
        LoadLookups();
        BaseUnit = BaseUnits.Contains(selected) ? selected : BaseUnits.FirstOrDefault() ?? selected;
        Load();
        SetStatus("Managed units refreshed; stock quantities were not changed.", false);
    }

    private void OpenProductPhoto(int productId)
    {
        var product = Items.FirstOrDefault(row => row.Id == productId);
        if (product is null)
        {
            SetStatus("That medicine is no longer in the filtered list. Refresh and try again.", true);
            return;
        }
        PhotoWindow.Show(_services, _user, PhotoEntity.Product, product.Id, product.Name);
        Load();
    }

    private void ManageCatalog()
    {
        var categoryId = SelectedCategory?.Id;
        var companyId = SelectedCompany?.Id;

        CatalogWindowDoor.Show(_services, _user);

        // Keep every other unsaved form field exactly as the person left it. A renamed id is
        // selected again; a row just put away can no longer be linked by this form.
        LoadLookups();
        SelectedCategory = Categories.FirstOrDefault(row => row.Id == categoryId);
        SelectedCompany = Companies.FirstOrDefault(row => row.Id == companyId);
        Load();
        SetStatus("Category and company lists refreshed; the product form was kept.", false);
    }

    /// <summary>The barcode list of the medicine on the form. A new, unsaved medicine has no id yet.</summary>
    private void OpenBarcodes()
    {
        if (SelectedProduct is null || SelectedProduct.Id <= 0)
        {
            SetStatus("Save the medicine first, then add its barcodes.", true);
            return;
        }

        BarcodeWindowDoor.Show(_services, _user, SelectedProduct.Id, SelectedProduct.Name);
    }

    /// <summary>Photo gallery for the medicine on the form. A new, unsaved medicine has no id yet.</summary>
    private void OpenPhotos()
    {
        if (SelectedProduct is null || SelectedProduct.Id <= 0)
        {
            SetStatus("Save the medicine first, then add its photos.", true);
            return;
        }

        PhotoWindow.Show(_services, _user, PhotoEntity.Product, SelectedProduct.Id, SelectedProduct.Name);
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

    private static string? ResolvePhotoPath(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.PhotoFileName))
        {
            return null;
        }
        var path = Path.Combine(AppPaths.PhotosFolder, product.PhotoFileName);
        return File.Exists(path) ? path : null;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}

/// <summary>A checkbox row for additional filter-only category tags.</summary>
public sealed class CategoryTagOption : ObservableObject
{
    private bool _isSelected;

    public CategoryTagOption(int id, string name, bool selected = false)
    {
        Id = id;
        Name = name;
        _isSelected = selected;
    }

    public int Id { get; }

    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
