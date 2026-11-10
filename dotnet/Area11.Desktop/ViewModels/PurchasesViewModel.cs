using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

// empty-page: none - the page is a form for the delivery being entered, with suppliers beside it;
// its only list is the draft lines of a purchase that has not been saved yet.
/// <summary>
/// Stock-in screen. Lines are built on the right and saved in one transaction by PurchaseService,
/// which also creates the batches and the supplier payable.
/// </summary>
public sealed class PurchasesViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly List<Product> _catalog = new();

    private Supplier? _selectedSupplier;
    private string _newSupplierName = string.Empty;
    private string _newSupplierPhone = string.Empty;
    private string _newSupplierAgency = string.Empty;
    private string _productSearch = string.Empty;
    private Product? _selectedProduct;
    private string _unit = "box";
    private string _quantity = "1";
    private string _batchNo = string.Empty;
    private string _expiry = string.Empty;
    private string _costText = string.Empty;
    private string _retailText = string.Empty;
    private bool _isSample;
    private string _paidText = string.Empty;
    private string _statusMessage = "Enter a delivery and add lines.";
    private bool _isStatusError;
    private int _openDraftId;

    public PurchasesViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;
        NewSupplierEditor = new CustomFieldsEditorViewModel(services, user, CustomFieldEntity.Supplier, saveWithParent: true);
        NewSupplierEditor.LoadNew();

        AddLineCommand = new RelayCommand(AddLine);
        RemoveLineCommand = new RelayCommand(RemoveLast);
        SaveCommand = new RelayCommand(Save);
        SaveDraftCommand = new RelayCommand(SavePurchaseDraft);
        DraftsCommand = new RelayCommand(OpenDrafts);
        AddSupplierCommand = new RelayCommand(AddSupplier);
        RefreshCommand = new RelayCommand(LoadLookups);

        LoadLookups();
    }

    public CustomFieldsEditorViewModel NewSupplierEditor { get; }

    public ObservableCollection<Supplier> Suppliers { get; } = new();

    public ObservableCollection<Product> FilteredProducts { get; } = new();

    public ObservableCollection<PurchaseLineRow> Lines { get; } = new();

    public ObservableCollection<string> Units { get; } = new() { "box", "strip", "base" };

    public RelayCommand AddLineCommand { get; }

    public RelayCommand RemoveLineCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand SaveDraftCommand { get; }

    public RelayCommand DraftsCommand { get; }

    /// <summary>True while the page shows an open draft; saving it posts the draft.</summary>
    public bool HasOpenDraft => _openDraftId > 0;

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

    public string NewSupplierAgency
    {
        get => _newSupplierAgency;
        set => SetProperty(ref _newSupplierAgency, value);
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
                CostText = Money.Edit(value.CostPaisa);
                RetailText = Money.Edit(value.RetailPaisa);
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

    public bool IsSample
    {
        get => _isSample;
        set => SetProperty(ref _isSample, value);
    }

    public string PaidText
    {
        get => _paidText;
        set => SetProperty(ref _paidText, value);
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

    public string TotalText => Money.Format(Lines.Where(line => !line.IsSample).Sum(line => line.LineTotalPaisa));

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

        var products = _services.Products.Search();
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
            if (PackagingPolicy.Matches(product, term))
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
        if (!NewSupplierEditor.LoadedSuccessfully)
        {
            SetStatus("Supplier extra fields could not be loaded. Reopen the purchase page.", true);
            return;
        }
        var result = _services.Partners.CreateSupplier(_user, NewSupplierName, NewSupplierPhone, NewSupplierAgency,
            NewSupplierEditor.AnswerValues(), NewSupplierEditor.StagedPhotoForSave());
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The supplier could not be saved.", true);
            return;
        }

        NewSupplierName = string.Empty;
        NewSupplierPhone = string.Empty;
        NewSupplierAgency = string.Empty;
        NewSupplierEditor.LoadNew();
        LoadLookups();
        SelectedSupplier = Suppliers.FirstOrDefault(supplier => supplier.Id == result.Value);
        SetStatus("Supplier added.", false);
    }

    private void AddLine()
    {
        if (SelectedProduct is null)
        {
            SetStatus("Select a product first.", true);
            return;
        }

        if (!Qty.TryParse(Quantity, out var quantity) || quantity <= 0)
        {
            SetStatus("Quantity must be greater than zero.", true);
            return;
        }

        if (!Money.TryParse(CostText, out var cost) || !Money.TryParse(RetailText, out var retail))
        {
            SetStatus("Cost and retail must be numbers in rupees.", true);
            return;
        }

        if (Expiry.Trim().Length > 0 && !Area11.Core.Utils.Expiry.TryNormalize(Expiry, out _, out _))
        {
            SetStatus("Expiry must look like 2027-06 or 2027-06-30.", true);
            return;
        }

        var unitsPerSelected = Unit switch
        {
            "box" => SelectedProduct.UnitsPerBox,
            "strip" => SelectedProduct.UnitsPerStrip,
            _ => 1
        };

        Lines.Add(new PurchaseLineRow
        {
            ProductId = SelectedProduct.Id,
            ProductName = SelectedProduct.Name,
            Unit = Unit,
            Quantity = quantity,
            QtyBase = quantity * unitsPerSelected,
            BatchNo = BatchNo.Trim(),
            Expiry = Expiry.Trim(),
            CostPaisa = Money.ToPaisa(cost),
            RetailPaisa = Money.ToPaisa(retail),
            IsSample = IsSample
        });

        BatchNo = string.Empty;
        Expiry = string.Empty;
        Quantity = "1";
        IsSample = false;
        OnPropertyChanged(nameof(TotalText));
        SetStatus("Line added.", false);
    }

    private void RemoveLast()
    {
        if (Lines.Count == 0)
        {
            return;
        }

        Lines.RemoveAt(Lines.Count - 1);
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

        if (!Money.TryParse(PaidText, out var paid))
        {
            SetStatus("Paid amount must be a number.", true);
            return;
        }

        var input = new PurchaseInput
        {
            SupplierId = SelectedSupplier.Id,
            PaidPaisa = Money.ToPaisa(paid),
            Lines = Lines.Select(line => new PurchaseLineInput
            {
                ProductId = line.ProductId,
                Unit = line.Unit,
                Quantity = line.Quantity,
                BatchNo = line.BatchNo,
                Expiry = line.Expiry,
                CostPaisa = line.CostPaisa,
                RetailPaisa = line.RetailPaisa,
                IsSample = line.IsSample
            }).ToList()
        };

        var wasDraft = _openDraftId > 0;
        var result = wasDraft
            ? _services.Drafts.PostPurchase(_user, _openDraftId, input)
            : _services.Purchases.Create(input, _user);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The purchase could not be saved.", true);
            return;
        }

        var purchase = result.Value;
        var warnings = purchase.Warnings.Count > 0 ? "  ⚠ " + string.Join(" ", purchase.Warnings) : string.Empty;
        SetStatus($"{purchase.Code} saved{(wasDraft ? " - its draft is posted" : string.Empty)} — {Money.Format(purchase.TotalPaisa)}.{warnings}",
            purchase.Warnings.Count > 0);

        Lines.Clear();
        PaidText = string.Empty;
        _openDraftId = 0;
        OnPropertyChanged(nameof(HasOpenDraft));
        OnPropertyChanged(nameof(TotalText));
        LoadLookups();
    }

    /// <summary>
    /// Parks the delivery as a shared draft. The supplier may be unchosen and quantities rough:
    /// finishing is the next person's job, not saving's.
    /// </summary>
    private void SavePurchaseDraft()
    {
        if (Lines.Count == 0)
        {
            SetStatus("Add at least one line.", true);
            return;
        }

        if (!Money.TryParse(PaidText, out var paid))
        {
            SetStatus("Paid amount must be a number.", true);
            return;
        }

        var input = new PurchaseInput
        {
            SupplierId = SelectedSupplier?.Id ?? 0,
            PaidPaisa = Money.ToPaisa(paid),
            Lines = Lines.Select(line => new PurchaseLineInput
            {
                ProductId = line.ProductId,
                Unit = line.Unit,
                Quantity = line.Quantity,
                BatchNo = line.BatchNo,
                Expiry = line.Expiry,
                CostPaisa = line.CostPaisa,
                RetailPaisa = line.RetailPaisa,
                IsSample = line.IsSample
            }).ToList()
        };

        var saved = _openDraftId > 0
            ? _services.Drafts.SavePurchaseDraft(_user, input, _openDraftId)
            : _services.Drafts.SavePurchaseDraft(_user, input);
        if (!saved.Ok || saved.Value is null)
        {
            SetStatus(saved.Error ?? "The draft could not be saved.", true);
            return;
        }

        _openDraftId = saved.Value.Id;
        OnPropertyChanged(nameof(HasOpenDraft));
        SetStatus("Draft saved as " + saved.Value.DraftKey + ".", false);
    }

    private void OpenDrafts() =>
        DraftsWindowDoor.Show(_services, _user, DraftType.Purchase, OpenPurchaseDraft);

    /// <summary>
    /// Loads a draft onto the page for review. Choosing the supplier and correcting lines happen
    /// here; saving posts the draft through the same guarded Create.
    /// </summary>
    public void OpenPurchaseDraft(int draftId)
    {
        if (Lines.Count > 0)
        {
            SetStatus("Save or remove the current lines before opening a draft.", true);
            return;
        }

        var opened = _services.Drafts.OpenPurchase(_user, draftId);
        if (!opened.Ok || opened.Value is null)
        {
            SetStatus(opened.Error ?? "The draft could not be opened.", true);
            return;
        }

        var input = opened.Value;
        SelectedSupplier = input.SupplierId > 0
            ? Suppliers.FirstOrDefault(supplier => supplier.Id == input.SupplierId)
            : null;
        PaidText = input.PaidPaisa > 0 ? Money.Edit(input.PaidPaisa) : string.Empty;

        var skipped = 0;
        Lines.Clear();
        foreach (var line in input.Lines)
        {
            var product = _catalog.FirstOrDefault(item => item.Id == line.ProductId);
            var name = product?.Name ?? "Unknown product";
            var unitsPerSelected = product is null ? 1 : line.Unit switch
            {
                "box" => product.UnitsPerBox,
                "strip" => product.UnitsPerStrip,
                _ => 1
            };
            if (product is null)
            {
                skipped++;
            }
            Lines.Add(new PurchaseLineRow
            {
                ProductId = line.ProductId,
                ProductName = name,
                Unit = line.Unit,
                Quantity = line.Quantity,
                QtyBase = line.Quantity * unitsPerSelected,
                BatchNo = line.BatchNo,
                Expiry = line.Expiry,
                CostPaisa = line.CostPaisa,
                RetailPaisa = line.RetailPaisa,
                IsSample = line.IsSample
            });
        }

        _openDraftId = draftId;
        OnPropertyChanged(nameof(HasOpenDraft));
        OnPropertyChanged(nameof(TotalText));
        SetStatus(SelectedSupplier is null
            ? "Draft opened - choose a supplier, review the lines, then save."
            : skipped == 0
                ? "Draft opened. Review the lines, then save."
                : "Draft opened, but " + skipped + " line(s) carry an unknown product - check before saving.",
            skipped > 0);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}

public sealed class PurchaseLineRow
{
    public int ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string Unit { get; init; } = "box";

    public double Quantity { get; init; }

    public double QtyBase { get; init; }

    public string BatchNo { get; init; } = string.Empty;

    public string Expiry { get; init; } = string.Empty;

    public long CostPaisa { get; init; }

    public long RetailPaisa { get; init; }

    public bool IsSample { get; init; }

    public long LineTotalPaisa => IsSample ? 0 : Calc.Round(CostPaisa * QtyBase);

    public string QuantityText => $"{Qty.Format(Quantity)} {Unit}";

    public string CostText => IsSample ? "sample" : Money.Format(LineTotalPaisa);
}
