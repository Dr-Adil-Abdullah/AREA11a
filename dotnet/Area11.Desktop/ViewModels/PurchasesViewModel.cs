using System.Collections.ObjectModel;
using System.Globalization;
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
    private ExpiryMonth? _selectedExpiryMonth;
    private int? _selectedExpiryYear;
    private string _costText = string.Empty;
    private string _retailText = string.Empty;
    private bool _isSample;
    private string _paidText = string.Empty;
    private string _paidMethod = "cash";
    private string _statusMessage = "Enter a delivery and add lines.";
    private bool _isStatusError;
    private int _openDraftId;
    private int _lastPurchaseId;
    private string _lastPurchaseCode = string.Empty;

    public PurchasesViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;
        NewSupplierEditor = new CustomFieldsEditorViewModel(services, user, CustomFieldEntity.Supplier, saveWithParent: true);
        NewSupplierEditor.LoadNew();
        DocEditor = new CustomFieldsEditorViewModel(services, user, CustomFieldEntity.Purchase, saveWithParent: true);
        DocEditor.LoadNew();

        AddLineCommand = new RelayCommand(AddLine);
        RemoveLineCommand = new RelayCommand(RemoveLast);
        SaveCommand = new RelayCommand(Save);
        SaveDraftCommand = new RelayCommand(SavePurchaseDraft);
        DraftsCommand = new RelayCommand(OpenDrafts);
        WhatsAppCommand = new RelayCommand(SendLastOnWhatsApp);
        AddSupplierCommand = new RelayCommand(AddSupplier);
        RefreshCommand = new RelayCommand(LoadLookups);

        foreach (var month in Area11.Core.Utils.Expiry.MonthOptions()) ExpiryMonths.Add(month);
        foreach (var year in Area11.Core.Utils.Expiry.YearOptions(TimeSource.Now.Year, 13)) ExpiryYears.Add(year);

        LoadLookups();
    }

    public CustomFieldsEditorViewModel NewSupplierEditor { get; }

    /// <summary>The owner's own purchase questions (Portion 18). Saved with the purchase, cleared after it.</summary>
    public CustomFieldsEditorViewModel DocEditor { get; }

    public ObservableCollection<Supplier> Suppliers { get; } = new();

    public ObservableCollection<Product> FilteredProducts { get; } = new();

    public ObservableCollection<PurchaseLineRow> Lines { get; } = new();

    public ObservableCollection<string> Units { get; } = new() { "box", "strip", "unit" };

    public RelayCommand AddLineCommand { get; }

    public RelayCommand RemoveLineCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand SaveDraftCommand { get; }

    public RelayCommand DraftsCommand { get; }

    /// <summary>Portion 12: the just-saved invoice as WhatsApp text plus a PDF the owner attaches by hand.</summary>
    public RelayCommand WhatsAppCommand { get; }

    /// <summary>True while the page shows an open draft; saving it posts the draft.</summary>
    public bool HasOpenDraft => _openDraftId > 0;

    /// <summary>Portion 12: the just-saved purchase can go to WhatsApp until the next save.</summary>
    public bool CanSendLastOnWhatsApp => _lastPurchaseId > 0;

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
        set
        {
            if (SetProperty(ref _expiry, value))
            {
                OnPropertyChanged(nameof(ExpiryHint));
                OnPropertyChanged(nameof(IsExpiryHintError));
                OnPropertyChanged(nameof(PickedExpiryDate));
            }
        }
    }

    /// <summary>
    /// The month and year drop-downs (Portion 6): medicine strips print a month, not a day.
    /// Choosing both writes "yyyy-MM" into the box; typing stays free and blank stays allowed.
    /// </summary>
    public ObservableCollection<ExpiryMonth> ExpiryMonths { get; } = new();

    public ObservableCollection<int> ExpiryYears { get; } = new();

    public ExpiryMonth? SelectedExpiryMonth
    {
        get => _selectedExpiryMonth;
        set
        {
            if (SetProperty(ref _selectedExpiryMonth, value))
            {
                ApplyMonthYear();
            }
        }
    }

    public int? SelectedExpiryYear
    {
        get => _selectedExpiryYear;
        set
        {
            if (SetProperty(ref _selectedExpiryYear, value))
            {
                ApplyMonthYear();
            }
        }
    }

    /// <summary>
    /// The calendar aid for an exact printed day. Month forms and legacy text read back as null,
    /// so the calendar never fights what was typed - picking a day writes "yyyy-MM-dd".
    /// </summary>
    public DateTime? PickedExpiryDate
    {
        get
        {
            var formats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy" };
            return DateTime.TryParseExact(Expiry.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) ? date : null;
        }
        set
        {
            if (value is null) return;
            SelectedExpiryMonth = null;
            SelectedExpiryYear = null;
            Expiry = value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Live line under the box: what the text means and exactly what would be stored.</summary>
    public string ExpiryHint =>
        Area11.Core.Utils.Expiry.TryDescribe(Expiry, out var description)
            ? description
            : "Use 2027-06, 06/27 or 2027-06-30.";

    public bool IsExpiryHintError => !Area11.Core.Utils.Expiry.TryDescribe(Expiry, out _);

    private void ApplyMonthYear()
    {
        if (SelectedExpiryMonth is null || SelectedExpiryYear is null) return;
        Expiry = SelectedExpiryYear.Value.ToString(CultureInfo.InvariantCulture) + "-"
            + SelectedExpiryMonth.Number.ToString("00", CultureInfo.InvariantCulture);
    }

    private void ClearExpiryPicks()
    {
        // Either pick on its own never writes the box (ApplyMonthYear needs both), so clearing
        // through the properties is safe and keeps the drop-downs in sync.
        SelectedExpiryMonth = null;
        SelectedExpiryYear = null;
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

    /// <summary>How the "paid now" money leaves the shop - the same three ways the Suppliers page pays with.</summary>
    public IReadOnlyList<string> Methods { get; } = SupplierLedgerService.PaymentMethods;

    public string PaidMethod
    {
        get => _paidMethod;
        set => SetProperty(ref _paidMethod, value);
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

            // Portion 17: the picker and the lines show the same covers as the Counter.
            foreach (var product in _catalog)
            {
                product.PhotoPath = _services.Photos.CoverPath(product.PhotoFileName);
            }
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
            SetStatus("Expiry must look like 2027-06, 06/27 or 2027-06-30.", true);
            return;
        }

        var unitsPerSelected = SelectedProduct.QuantityPerSelectedUnit(Unit);

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
            IsSample = IsSample,
            PhotoPath = SelectedProduct.PhotoPath
        });

        BatchNo = string.Empty;
        Expiry = string.Empty;
        ClearExpiryPicks();
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
            PaidMethod = PaidMethod,
            CustomAnswers = DocEditor.AnswerValues().ToDictionary(pair => pair.Key, pair => pair.Value),
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

        // Portion 11: the saved purchase prints its own invoice, like a sale prints its bill (Z-07).
        // The paper is a copy for the file; a silent printer must never unsave the purchase.
        PrintInvoice(purchase.PurchaseId, purchase.Code);

        // Portion 12: the same invoice can go to WhatsApp while it is still "the last save".
        _lastPurchaseId = purchase.PurchaseId;
        _lastPurchaseCode = purchase.Code;
        OnPropertyChanged(nameof(CanSendLastOnWhatsApp));

        Lines.Clear();
        PaidText = string.Empty;
        // Back to cash after every save: a forgotten "online" would silently spare the drawer.
        PaidMethod = "cash";
        _openDraftId = 0;
        OnPropertyChanged(nameof(HasOpenDraft));
        DocEditor.LoadNew();
        OnPropertyChanged(nameof(TotalText));
        LoadLookups();
    }

    /// <summary>
    /// The just-saved invoice to WhatsApp: the invoice text in the message, the same invoice as a
    /// PDF file. The invoice is read back from the database, so it matches the black box.
    /// </summary>
    private void SendLastOnWhatsApp()
    {
        if (_lastPurchaseId <= 0)
        {
            SetStatus("Save a purchase first.", true);
            return;
        }

        var document = _services.Purchases.BuildInvoiceDocument(_lastPurchaseId, _user);
        if (!document.Ok || document.Value is null)
        {
            SetStatus(document.Error ?? "The invoice could not be prepared.", true);
            return;
        }

        var sent = WhatsAppFlow.Send(_services, null, document.Value.PlainText,
            document.Value.Title, document.Value.Lines.Select(line => line.Text),
            "purchase-" + _lastPurchaseCode + ".pdf");
        SetStatus(sent.Message, !sent.Ok);
    }

    private void PrintInvoice(int purchaseId, string code)
    {
        if (!_services.Settings.GetBool("printer.autoPrint", true))
        {
            return;
        }

        var document = _services.Purchases.BuildInvoiceDocument(purchaseId, _user);
        if (!document.Ok || document.Value is null)
        {
            SetStatus(code + " is saved, but its invoice could not be prepared: " + (document.Error ?? "unknown reason"), true);
            return;
        }

        var printed = _services.Printer.Print(document.Value);
        if (!printed.Ok)
        {
            SetStatus($"{code} is saved. The printer did not answer: {printed.Error}", true);
        }
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
            PaidMethod = PaidMethod,
            CustomAnswers = DocEditor.AnswerValues().ToDictionary(pair => pair.Key, pair => pair.Value),
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
        // An old draft has no method stored; it was written when everything was cash.
        var draftMethod = (input.PaidMethod ?? "cash").Trim().ToLowerInvariant();
        PaidMethod = draftMethod is "cash" or "online" or "card" ? draftMethod : "cash";

        var skipped = 0;
        Lines.Clear();
        foreach (var line in input.Lines)
        {
            var product = _catalog.FirstOrDefault(item => item.Id == line.ProductId);
            var name = product?.Name ?? "Unknown product";
            var unitsPerSelected = product is null ? 1 : product.QuantityPerSelectedUnit(line.Unit);
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
                IsSample = line.IsSample,
                PhotoPath = product?.PhotoPath
            });
        }

        _openDraftId = draftId;
        OnPropertyChanged(nameof(HasOpenDraft));
        DocEditor.SetAnswers(input.CustomAnswers);
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

    /// <summary>Portion 17: the cover copied from the picked catalog product - display only.</summary>
    public string? PhotoPath { get; init; }

    public bool HasNoPhoto => string.IsNullOrWhiteSpace(PhotoPath);

    public long LineTotalPaisa => IsSample ? 0 : Calc.Round(CostPaisa * QtyBase);

    public string QuantityText => $"{Qty.Format(Quantity)} {Packs.Display(Unit)}";

    public string CostText => IsSample ? "sample" : Money.Format(LineTotalPaisa);
}
