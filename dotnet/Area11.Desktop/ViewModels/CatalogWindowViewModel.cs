using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// E-04 catalog management. This class only moves values to ProductService; all permission,
/// two-level, uniqueness, unlink and audit rules stay in Core.
/// </summary>
public sealed class CatalogWindowViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private bool _loading;
    private Category? _selectedCategory;
    private Company? _selectedCompany;
    private GenericFormula? _selectedFormula;
    private BrandDefinition? _selectedBrand;
    private Company? _selectedBrandCompany;
    private int? _brandCompanyId;
    private Category? _selectedParent;
    private string _categoryName = string.Empty;
    private string _companyName = string.Empty;
    private string _formulaName = string.Empty;
    private string _brandName = string.Empty;
    private string _categoryOrderText = "0";
    private string _companyOrderText = "0";
    private string _formulaOrderText = "0";
    private string _brandOrderText = "0";
    private string _categoryCountText = "no categories";
    private string _companyCountText = "no companies";
    private string _formulaCountText = "no salts";
    private string _brandCountText = "no brands";
    private string _statusText = "Pick a row to edit, or choose New. Nothing is deleted.";
    private bool _isStatusError;

    public CatalogWindowViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;
        CanEdit = user.CanManage;

        NewCategoryCommand = new RelayCommand(NewCategory);
        SaveCategoryCommand = new RelayCommand(SaveCategory);
        ToggleCategoryCommand = new RelayCommand(ToggleCategory);
        NewCompanyCommand = new RelayCommand(NewCompany);
        SaveCompanyCommand = new RelayCommand(SaveCompany);
        ToggleCompanyCommand = new RelayCommand(ToggleCompany);
        NewFormulaCommand = new RelayCommand(NewFormula);
        SaveFormulaCommand = new RelayCommand(SaveFormula);
        ToggleFormulaCommand = new RelayCommand(ToggleFormula);
        NewBrandCommand = new RelayCommand(NewBrand);
        SaveBrandCommand = new RelayCommand(SaveBrand);
        ToggleBrandCommand = new RelayCommand(ToggleBrand);
        ClearBrandCompanyCommand = new RelayCommand(ClearBrandCompany);
        RefreshCommand = new RelayCommand(Refresh);

        Load();
    }

    public ObservableCollection<Category> Categories { get; } = new();

    public ObservableCollection<Category> ParentOptions { get; } = new();

    public ObservableCollection<Company> Companies { get; } = new();

    public ObservableCollection<GenericFormula> Formulas { get; } = new();

    public ObservableCollection<BrandDefinition> Brands { get; } = new();

    public ObservableCollection<Company> ActiveCompanies { get; } = new();

    public RelayCommand NewCategoryCommand { get; }

    public RelayCommand SaveCategoryCommand { get; }

    public RelayCommand ToggleCategoryCommand { get; }

    public RelayCommand NewCompanyCommand { get; }

    public RelayCommand SaveCompanyCommand { get; }

    public RelayCommand ToggleCompanyCommand { get; }

    public RelayCommand NewFormulaCommand { get; }

    public RelayCommand SaveFormulaCommand { get; }

    public RelayCommand ToggleFormulaCommand { get; }

    public RelayCommand NewBrandCommand { get; }

    public RelayCommand SaveBrandCommand { get; }

    public RelayCommand ToggleBrandCommand { get; }

    public RelayCommand ClearBrandCompanyCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public bool CanEdit { get; }

    public bool IsReadOnly => !CanEdit;

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanToggleCategory));
            OnPropertyChanged(nameof(CategoryActionLabel));
            if (_loading)
            {
                return;
            }

            FillCategoryEditor(value);
        }
    }

    public Company? SelectedCompany
    {
        get => _selectedCompany;
        set
        {
            if (!SetProperty(ref _selectedCompany, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanToggleCompany));
            OnPropertyChanged(nameof(CompanyActionLabel));
            if (_loading)
            {
                return;
            }

            FillCompanyEditor(value);
        }
    }

    public GenericFormula? SelectedFormula
    {
        get => _selectedFormula;
        set
        {
            if (!SetProperty(ref _selectedFormula, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanToggleFormula));
            OnPropertyChanged(nameof(FormulaActionLabel));
            if (_loading)
            {
                return;
            }

            FillFormulaEditor(value);
        }
    }

    public BrandDefinition? SelectedBrand
    {
        get => _selectedBrand;
        set
        {
            if (!SetProperty(ref _selectedBrand, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanToggleBrand));
            OnPropertyChanged(nameof(BrandActionLabel));
            if (_loading)
            {
                return;
            }

            FillBrandEditor(value);
        }
    }

    public Company? SelectedBrandCompany
    {
        get => _selectedBrandCompany;
        set => SetProperty(ref _selectedBrandCompany, value);
    }

    public Category? SelectedParent
    {
        get => _selectedParent;
        set => SetProperty(ref _selectedParent, value);
    }

    public string CategoryName
    {
        get => _categoryName;
        set => SetProperty(ref _categoryName, value);
    }

    public string CompanyName
    {
        get => _companyName;
        set => SetProperty(ref _companyName, value);
    }

    public string FormulaName
    {
        get => _formulaName;
        set => SetProperty(ref _formulaName, value);
    }

    public string BrandName
    {
        get => _brandName;
        set => SetProperty(ref _brandName, value);
    }

    public string CategoryOrderText
    {
        get => _categoryOrderText;
        set => SetProperty(ref _categoryOrderText, value);
    }

    public string CompanyOrderText
    {
        get => _companyOrderText;
        set => SetProperty(ref _companyOrderText, value);
    }

    public string FormulaOrderText
    {
        get => _formulaOrderText;
        set => SetProperty(ref _formulaOrderText, value);
    }

    public string BrandOrderText
    {
        get => _brandOrderText;
        set => SetProperty(ref _brandOrderText, value);
    }

    public string CategoryCountText
    {
        get => _categoryCountText;
        private set => SetProperty(ref _categoryCountText, value);
    }

    public string CompanyCountText
    {
        get => _companyCountText;
        private set => SetProperty(ref _companyCountText, value);
    }

    public string FormulaCountText
    {
        get => _formulaCountText;
        private set => SetProperty(ref _formulaCountText, value);
    }

    public string BrandCountText
    {
        get => _brandCountText;
        private set => SetProperty(ref _brandCountText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    public bool HasNoCategories => Categories.Count == 0;

    public bool HasNoCompanies => Companies.Count == 0;

    public bool HasNoFormulas => Formulas.Count == 0;

    public bool HasNoBrands => Brands.Count == 0;

    public bool CanToggleCategory => CanEdit && SelectedCategory is not null;

    public bool CanToggleCompany => CanEdit && SelectedCompany is not null;

    public bool CanToggleFormula => CanEdit && SelectedFormula is not null;

    public bool CanToggleBrand => CanEdit && SelectedBrand is not null;

    public string CategoryActionLabel => SelectedCategory is { Active: false } ? "Bring back" : "Put away";

    public string CompanyActionLabel => SelectedCompany is { Active: false } ? "Bring back" : "Put away";

    public string FormulaActionLabel => SelectedFormula is { Active: false } ? "Bring back" : "Put away";

    public string BrandActionLabel => SelectedBrand is { Active: false } ? "Bring back" : "Put away";

    private bool Load(int? selectCategory = null, int? selectCompany = null, int? selectFormula = null, int? selectBrand = null)
    {
        var categoryResult = _services.Products.GetCategories(activeOnly: false);
        if (!categoryResult.Ok || categoryResult.Value is null)
        {
            SetStatus(categoryResult.Error ?? "Categories could not be loaded.", true);
            return false;
        }

        var companyResult = _services.Products.GetCompanies(activeOnly: false);
        if (!companyResult.Ok || companyResult.Value is null)
        {
            SetStatus(companyResult.Error ?? "Companies could not be loaded.", true);
            return false;
        }

        var formulaResult = _services.Products.GetFormulas(activeOnly: false);
        if (!formulaResult.Ok || formulaResult.Value is null)
        {
            SetStatus(formulaResult.Error ?? "Salts could not be loaded.", true);
            return false;
        }

        var brandResult = _services.Products.GetBrands(activeOnly: false);
        if (!brandResult.Ok || brandResult.Value is null)
        {
            SetStatus(brandResult.Error ?? "Brands could not be loaded.", true);
            return false;
        }

        _loading = true;
        try
        {
            var categoryId = selectCategory ?? SelectedCategory?.Id;
            var companyId = selectCompany ?? SelectedCompany?.Id;
            var formulaId = selectFormula ?? SelectedFormula?.Id;
            var brandId = selectBrand ?? SelectedBrand?.Id;

            ParentOptions.Clear();
            ParentOptions.Add(new Category { Id = 0, Name = "Top level", Active = true });
            foreach (var row in categoryResult.Value.Where(row => row.ParentId is null))
            {
                ParentOptions.Add(row);
            }

            Categories.Clear();
            foreach (var row in categoryResult.Value)
            {
                Categories.Add(row);
            }

            Companies.Clear();
            foreach (var row in companyResult.Value)
            {
                Companies.Add(row);
            }

            Formulas.Clear();
            foreach (var row in formulaResult.Value)
            {
                Formulas.Add(row);
            }

            Brands.Clear();
            foreach (var row in brandResult.Value)
            {
                Brands.Add(row);
            }

            ActiveCompanies.Clear();
            foreach (var row in companyResult.Value.Where(row => row.Active))
            {
                ActiveCompanies.Add(row);
            }

            SelectedCategory = Categories.FirstOrDefault(row => row.Id == categoryId)
                               ?? Categories.FirstOrDefault();
            SelectedCompany = Companies.FirstOrDefault(row => row.Id == companyId)
                              ?? Companies.FirstOrDefault();
            SelectedFormula = Formulas.FirstOrDefault(row => row.Id == formulaId)
                              ?? Formulas.FirstOrDefault();
            SelectedBrand = Brands.FirstOrDefault(row => row.Id == brandId)
                            ?? Brands.FirstOrDefault();
        }
        finally
        {
            _loading = false;
        }

        // The selection was assigned while loading, so fill the editors once after the guard lifts.
        FillCategoryEditor(SelectedCategory);
        FillCompanyEditor(SelectedCompany);
        FillFormulaEditor(SelectedFormula);
        FillBrandEditor(SelectedBrand);
        CategoryCountText = CountLabel(Categories.Count, Categories.Count(row => !row.Active), "category");
        CompanyCountText = CountLabel(Companies.Count, Companies.Count(row => !row.Active), "company");
        FormulaCountText = CountLabel(Formulas.Count, Formulas.Count(row => !row.Active), "salt");
        BrandCountText = CountLabel(Brands.Count, Brands.Count(row => !row.Active), "brand");
        OnPropertyChanged(nameof(HasNoCategories));
        OnPropertyChanged(nameof(HasNoCompanies));
        OnPropertyChanged(nameof(HasNoFormulas));
        OnPropertyChanged(nameof(HasNoBrands));
        return true;
    }

    private void FillCategoryEditor(Category? value)
    {
        CategoryName = value?.Name ?? string.Empty;
        CategoryOrderText = value?.SortOrder.ToString() ?? "0";
        SelectedParent = ParentOptions.FirstOrDefault(row => row.Id == (value?.ParentId ?? 0))
                         ?? ParentOptions.FirstOrDefault();
    }

    private void FillCompanyEditor(Company? value)
    {
        CompanyName = value?.Name ?? string.Empty;
        CompanyOrderText = value?.SortOrder.ToString() ?? "0";
    }

    private void FillFormulaEditor(GenericFormula? value)
    {
        FormulaName = value?.Name ?? string.Empty;
        FormulaOrderText = value?.SortOrder.ToString() ?? "0";
    }

    private void FillBrandEditor(BrandDefinition? value)
    {
        BrandName = value?.Name ?? string.Empty;
        BrandOrderText = value?.SortOrder.ToString() ?? "0";
        _brandCompanyId = value?.CompanyId;
        SelectedBrandCompany = ActiveCompanies.FirstOrDefault(row => row.Id == _brandCompanyId);
    }

    private void Refresh()
    {
        if (Load())
        {
            SetStatus("The catalog lists are up to date.", false);
        }
    }

    private void NewCategory()
    {
        SelectedCategory = null;
        CategoryName = string.Empty;
        SelectedParent = ParentOptions.FirstOrDefault();
        CategoryOrderText = NextOrder(Categories.Select(row => row.SortOrder));
        SetStatus("New category: enter a name, optional parent and order, then Save.", false);
    }

    private void SaveCategory()
    {
        if (!TryOrder(CategoryOrderText, out var order))
        {
            SetStatus("Category order must be a whole number.", true);
            return;
        }

        int? parentId = SelectedParent is { Id: > 0 } ? (int?)SelectedParent.Id : null;
        int id;
        if (SelectedCategory is null)
        {
            var added = _services.Products.AddCategory(_user, CategoryName, parentId, order);
            if (!added.Ok)
            {
                SetStatus(added.Error ?? "The category could not be saved.", true);
                return;
            }

            id = added.Value;
        }
        else
        {
            id = SelectedCategory.Id;
            var updated = _services.Products.UpdateCategory(_user, id, CategoryName, parentId, order);
            if (!updated.Ok)
            {
                SetStatus(updated.Error ?? "The category could not be saved.", true);
                return;
            }
        }

        if (Load(id, null, null, null))
        {
            SetStatus("Category saved. Product and report reads use the current name.", false);
        }
    }

    private void ToggleCategory()
    {
        if (SelectedCategory is null)
        {
            SetStatus("Pick a category first.", true);
            return;
        }

        var id = SelectedCategory.Id;
        var name = SelectedCategory.Name;
        var bringBack = !SelectedCategory.Active;
        var result = _services.Products.SetCategoryActive(_user, id, bringBack);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The category could not be changed.", true);
            return;
        }

        if (Load(id, null, null, null))
        {
            SetStatus(bringBack
                ? name + " is back. Former product links were not recreated."
                : name + " is put away. Its direct product links were removed; history and children remain.", false);
        }
    }

    private void NewCompany()
    {
        SelectedCompany = null;
        CompanyName = string.Empty;
        CompanyOrderText = NextOrder(Companies.Select(row => row.SortOrder));
        SetStatus("New company: enter a name and order, then Save.", false);
    }

    private void SaveCompany()
    {
        if (!TryOrder(CompanyOrderText, out var order))
        {
            SetStatus("Company order must be a whole number.", true);
            return;
        }

        int id;
        if (SelectedCompany is null)
        {
            var added = _services.Products.AddCompany(_user, CompanyName, order);
            if (!added.Ok)
            {
                SetStatus(added.Error ?? "The company could not be saved.", true);
                return;
            }

            id = added.Value;
        }
        else
        {
            id = SelectedCompany.Id;
            var updated = _services.Products.UpdateCompany(_user, id, CompanyName, order);
            if (!updated.Ok)
            {
                SetStatus(updated.Error ?? "The company could not be saved.", true);
                return;
            }
        }

        if (Load(null, id, null, null))
        {
            SetStatus("Company saved. Phone, notes and medical-rep fields were not rewritten.", false);
        }
    }

    private void ToggleCompany()
    {
        if (SelectedCompany is null)
        {
            SetStatus("Pick a company first.", true);
            return;
        }

        var id = SelectedCompany.Id;
        var name = SelectedCompany.Name;
        var bringBack = !SelectedCompany.Active;
        var result = _services.Products.SetCompanyActive(_user, id, bringBack);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The company could not be changed.", true);
            return;
        }

        if (Load(null, id, null, null))
        {
            SetStatus(bringBack
                ? name + " is back. Former product links were not recreated."
                : name + " is put away. Product links were removed; every historical record remains.", false);
        }
    }

    private void NewFormula()
    {
        SelectedFormula = null;
        FormulaName = string.Empty;
        FormulaOrderText = NextOrder(Formulas.Select(row => row.SortOrder));
        SetStatus("New salt: enter a name and order, then Save.", false);
    }

    private void SaveFormula()
    {
        if (!TryOrder(FormulaOrderText, out var order))
        {
            SetStatus("Salt order must be a whole number.", true);
            return;
        }

        int id;
        if (SelectedFormula is null)
        {
            var added = _services.Products.AddFormula(_user, FormulaName, order);
            if (!added.Ok)
            {
                SetStatus(added.Error ?? "The salt could not be saved.", true);
                return;
            }

            id = added.Value;
        }
        else
        {
            id = SelectedFormula.Id;
            var updated = _services.Products.UpdateFormula(_user, id, FormulaName, order);
            if (!updated.Ok)
            {
                SetStatus(updated.Error ?? "The salt could not be saved.", true);
                return;
            }
        }

        if (Load(null, null, id, null))
        {
            SetStatus("Salt saved. Linked products show the current spelling; typed text elsewhere was not rewritten.", false);
        }
    }

    private void ToggleFormula()
    {
        if (SelectedFormula is null)
        {
            SetStatus("Pick a salt first.", true);
            return;
        }

        var id = SelectedFormula.Id;
        var name = SelectedFormula.Name;
        var bringBack = !SelectedFormula.Active;
        var result = _services.Products.SetFormulaActive(_user, id, bringBack);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The salt could not be changed.", true);
            return;
        }

        if (Load(null, null, id, null))
        {
            SetStatus(bringBack
                ? name + " is back. Former product links were not recreated."
                : name + " is put away. Product links were removed; typed salt text was kept.", false);
        }
    }

    private void NewBrand()
    {
        SelectedBrand = null;
        _brandCompanyId = null;
        SelectedBrandCompany = null;
        BrandName = string.Empty;
        BrandOrderText = NextOrder(Brands.Select(row => row.SortOrder));
        SetStatus("New brand: enter a name and order, then Save.", false);
    }

    private void SaveBrand()
    {
        if (!TryOrder(BrandOrderText, out var order))
        {
            SetStatus("Brand order must be a whole number.", true);
            return;
        }

        int id;
        if (SelectedBrand is null)
        {
            var added = _services.Products.AddBrand(_user, BrandName, order);
            if (!added.Ok)
            {
                SetStatus(added.Error ?? "The brand could not be saved.", true);
                return;
            }

            id = added.Value;
        }
        else
        {
            id = SelectedBrand.Id;
            var updated = _services.Products.UpdateBrand(_user, id, BrandName, order);
            if (!updated.Ok)
            {
                SetStatus(updated.Error ?? "The brand could not be saved.", true);
                return;
            }
        }

        var wantCompanyId = SelectedBrandCompany?.Id;
        if (wantCompanyId != _brandCompanyId)
        {
            var linked = _services.Products.SetBrandCompany(_user, id, wantCompanyId);
            if (!linked.Ok)
            {
                if (Load(null, null, null, id))
                {
                    SetStatus("Brand saved, but the company link failed: " + (linked.Error ?? "unknown error"), true);
                }

                return;
            }
        }

        if (Load(null, null, null, id))
        {
            SetStatus("Brand saved. Linked products show the current spelling; typed text elsewhere was not rewritten.", false);
        }
    }

    private void ClearBrandCompany()
    {
        SelectedBrandCompany = null;
        SetStatus("Company cleared from this form. Save the brand to keep it that way.", false);
    }

    private void ToggleBrand()
    {
        if (SelectedBrand is null)
        {
            SetStatus("Pick a brand first.", true);
            return;
        }

        var id = SelectedBrand.Id;
        var name = SelectedBrand.Name;
        var bringBack = !SelectedBrand.Active;
        var result = _services.Products.SetBrandActive(_user, id, bringBack);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The brand could not be changed.", true);
            return;
        }

        if (Load(null, null, null, id))
        {
            SetStatus(bringBack
                ? name + " is back. Former product links were not recreated."
                : name + " is put away. Product links were removed; typed brand text was kept.", false);
        }
    }

    private static string NextOrder(IEnumerable<int> values)
    {
        var maximum = values.DefaultIfEmpty(0).Max();
        return maximum <= 999_999_990 ? (maximum + 10).ToString() : "0";
    }

    private static bool TryOrder(string text, out int order) => int.TryParse(text, out order) && order >= 0;

    private static string CountLabel(int total, int hidden, string singular)
    {
        var rows = total + " " + (total == 1 ? singular : singular + " rows");
        return hidden > 0 ? rows + " · " + hidden + " put away" : rows;
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
