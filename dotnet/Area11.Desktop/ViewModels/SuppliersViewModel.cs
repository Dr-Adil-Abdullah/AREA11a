using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Suppliers (F-28, Spec 9.3, 17.4): what the shop owes, and the goods that go back.
///
/// Left: every supplier with the payable. Middle: one supplier - the payable, the three totals and
/// every purchase, payment and return with a running balance, plus the line that matters most:
/// **"Books match"**. Right: the two things a person actually does here - pay money, and send goods
/// back under `SR-nnnn`.
///
/// No rule lives here. SupplierLedgerService refuses a payment bigger than the debt, lowers the
/// payable by a credit note, and lets the stock go negative (U-30) with a warning instead of
/// stopping the shop.
/// </summary>
public sealed class SuppliersViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly Action? _onChanged;

    private string _searchText = string.Empty;
    private string _amountText = string.Empty;
    private string _method = "cash";
    private string _payNote = string.Empty;
    private string _productSearch = string.Empty;
    private string _qtyText = string.Empty;
    private string _unit = "unit";
    private string _reason = SupplierLedgerService.ReasonExpired;
    private string _lineNote = string.Empty;
    private string _srNote = string.Empty;
    private bool _onlyOwing;
    private bool _cashReceived;
    private bool _hasSupplier;
    private bool _booksMatch = true;
    private string _booksLabel = "Pick a supplier to see the account.";
    private string _statusText = "Pick a supplier, or search by name, agency or phone.";
    private bool _isStatusError;
    private string _nameText = string.Empty;
    private string _agencyText = "-";
    private string _phoneText = "-";
    private string _payableText = "Rs 0";
    private string _payableLabel = "settled";
    private string _purchasesText = "Rs 0";
    private string _paidText = "Rs 0";
    private string _creditedText = "Rs 0";
    private string _activityText = "-";
    private string _lastPurchaseText = "-";
    private string _trayTotalText = "Rs 0";
    private string _batchHint = "Search a medicine, then pick a batch.";
    private SupplierSummary? _selected;
    private Product? _selectedProduct;
    private BatchRow? _selectedBatch;
    private ExpiredBatchRow? _selectedExpired;
    private ReturnTrayRow? _selectedTrayRow;

    public SuppliersViewModel(AppServices services, User user, Action? onChanged = null)
    {
        _services = services;
        _user = user;
        _onChanged = onChanged;

        RefreshCommand = new RelayCommand(Load);
        PhotosCommand = new RelayCommand(OpenPhotos);
        PayCommand = new RelayCommand(Pay);
        StatementCommand = new RelayCommand(PrintStatement);
        ProductSearchCommand = new RelayCommand(SearchProducts);
        AddLineCommand = new RelayCommand(AddLine);
        RemoveLineCommand = new RelayCommand(RemoveLine);
        ClearTrayCommand = new RelayCommand(ClearTray);
        PostReturnCommand = new RelayCommand(PostReturn);
        AddExpiredCommand = new RelayCommand(AddExpired);

        Load();
    }

    public ObservableCollection<SupplierSummary> Suppliers { get; } = new();

    public ObservableCollection<SupplierMovement> Movements { get; } = new();

    public ObservableCollection<Product> Found { get; } = new();

    public ObservableCollection<BatchRow> Batches { get; } = new();

    public ObservableCollection<ReturnTrayRow> Tray { get; } = new();

    public ObservableCollection<ExpiredBatchRow> Expired { get; } = new();

    public IReadOnlyList<string> Units { get; } = new[] { "unit", "strip", "box" };

    public IReadOnlyList<string> Reasons { get; } = SupplierLedgerService.ReturnReasons;

    public IReadOnlyList<string> Methods { get; } = SupplierLedgerService.PaymentMethods;

    public RelayCommand RefreshCommand { get; }

    /// <summary>The photo gallery of the supplier on the page (F-41, D-21) - the licence, the card.</summary>
    public RelayCommand PhotosCommand { get; }

    public RelayCommand PayCommand { get; }

    public RelayCommand StatementCommand { get; }

    public RelayCommand ProductSearchCommand { get; }

    public RelayCommand AddLineCommand { get; }

    public RelayCommand RemoveLineCommand { get; }

    public RelayCommand ClearTrayCommand { get; }

    public RelayCommand PostReturnCommand { get; }

    public RelayCommand AddExpiredCommand { get; }

    public bool CanWork => _services.Permissions.Can(_user, Modules.Suppliers);

    public bool IsNotAllowed => !CanWork;

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public bool OnlyOwing
    {
        get => _onlyOwing;
        set
        {
            if (SetProperty(ref _onlyOwing, value))
            {
                Load();
            }
        }
    }

    public string AmountText
    {
        get => _amountText;
        set => SetProperty(ref _amountText, value);
    }

    public string Method
    {
        get => _method;
        set => SetProperty(ref _method, value);
    }

    public string PayNote
    {
        get => _payNote;
        set => SetProperty(ref _payNote, value);
    }

    public string ProductSearch
    {
        get => _productSearch;
        set => SetProperty(ref _productSearch, value);
    }

    public string QtyText
    {
        get => _qtyText;
        set => SetProperty(ref _qtyText, value);
    }

    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public string LineNote
    {
        get => _lineNote;
        set => SetProperty(ref _lineNote, value);
    }

    public string SrNote
    {
        get => _srNote;
        set => SetProperty(ref _srNote, value);
    }

    public bool CashReceived
    {
        get => _cashReceived;
        set => SetProperty(ref _cashReceived, value);
    }

    public SupplierSummary? Selected
    {
        get => _selected;
        set
        {
            // A tray belongs to one supplier: moving to another person clears it, a refresh of the
            // same person keeps it (otherwise paying a bill would silently throw away a return).
            var differentSupplier = _selected?.Id != value?.Id;

            if (SetProperty(ref _selected, value))
            {
                if (differentSupplier)
                {
                    ClearTray();
                }

                ShowSupplier();
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
                LoadBatches();
            }
        }
    }

    public BatchRow? SelectedBatch
    {
        get => _selectedBatch;
        set
        {
            if (SetProperty(ref _selectedBatch, value))
            {
                BatchHint = value is null
                    ? "Pick a batch."
                    : "Batch " + value.BatchNo + ": " + value.QtyLabel + " on the shelf, "
                      + value.ValueLabel + " at cost, " + value.ExpiryLabel;
            }
        }
    }

    public ExpiredBatchRow? SelectedExpired
    {
        get => _selectedExpired;
        set => SetProperty(ref _selectedExpired, value);
    }

    public ReturnTrayRow? SelectedTrayRow
    {
        get => _selectedTrayRow;
        set => SetProperty(ref _selectedTrayRow, value);
    }

    public bool HasSupplier
    {
        get => _hasSupplier;
        private set => SetProperty(ref _hasSupplier, value);
    }

    public bool BooksMatch
    {
        get => _booksMatch;
        private set
        {
            if (SetProperty(ref _booksMatch, value))
            {
                OnPropertyChanged(nameof(BooksMismatch));
            }
        }
    }

    public bool BooksMismatch => !_booksMatch;

    public string BooksLabel
    {
        get => _booksLabel;
        private set => SetProperty(ref _booksLabel, value);
    }

    public string NameText
    {
        get => _nameText;
        private set => SetProperty(ref _nameText, value);
    }

    public string AgencyText
    {
        get => _agencyText;
        private set => SetProperty(ref _agencyText, value);
    }

    public string PhoneText
    {
        get => _phoneText;
        private set => SetProperty(ref _phoneText, value);
    }

    public string PayableText
    {
        get => _payableText;
        private set => SetProperty(ref _payableText, value);
    }

    public string PayableLabel
    {
        get => _payableLabel;
        private set => SetProperty(ref _payableLabel, value);
    }

    public string PurchasesText
    {
        get => _purchasesText;
        private set => SetProperty(ref _purchasesText, value);
    }

    public string PaidText
    {
        get => _paidText;
        private set => SetProperty(ref _paidText, value);
    }

    public string CreditedText
    {
        get => _creditedText;
        private set => SetProperty(ref _creditedText, value);
    }

    public string ActivityText
    {
        get => _activityText;
        private set => SetProperty(ref _activityText, value);
    }

    public string LastPurchaseText
    {
        get => _lastPurchaseText;
        private set => SetProperty(ref _lastPurchaseText, value);
    }

    public string TrayTotalText
    {
        get => _trayTotalText;
        private set => SetProperty(ref _trayTotalText, value);
    }

    public string BatchHint
    {
        get => _batchHint;
        private set => SetProperty(ref _batchHint, value);
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

    // ------------------------------------------------------------------- loading

    public void Load()
    {
        var keep = Selected?.Id;

        var result = _services.SupplierLedger.List(SearchText, OnlyOwing);

        Suppliers.Clear();

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The suppliers could not be loaded.", true);
            return;
        }

        foreach (var row in result.Value!)
        {
            Suppliers.Add(row);
        }

        Selected = Suppliers.FirstOrDefault(row => row.Id == keep) ?? Suppliers.FirstOrDefault();

        if (Suppliers.Count == 0)
        {
            SetStatus(OnlyOwing ? "Every supplier is settled." : "No supplier matches that search.", false);
        }

        LoadExpired();
    }

    private void LoadExpired()
    {
        Expired.Clear();

        var result = _services.SupplierLedger.ExpiredBatches();

        if (result.Ok)
        {
            foreach (var row in result.Value!)
            {
                Expired.Add(row);
            }
        }
    }

    private void ShowSupplier()
    {
        Movements.Clear();

        if (Selected is null)
        {
            HasSupplier = false;
            BooksLabel = "Pick a supplier to see the account.";
            BooksMatch = true;
            return;
        }

        HasSupplier = true;
        NameText = Selected.Name;
        AgencyText = Selected.AgencyLabel;
        PhoneText = Selected.PhoneLabel;
        PayableText = Selected.PayableText;
        PayableLabel = Selected.PayableLabel;
        PurchasesText = Selected.PurchasesText;
        PaidText = Selected.PaidText;
        CreditedText = Selected.CreditedText;
        ActivityText = Selected.ActivityText;
        LastPurchaseText = "Last purchase: " + Selected.LastPurchaseLabel;

        var page = _services.SupplierLedger.Page(Selected.Id);

        if (!page.Ok || page.Value is null)
        {
            BooksMatch = false;
            BooksLabel = page.Error ?? "The account could not be loaded.";
            return;
        }

        var data = page.Value;

        foreach (var line in data.Lines)
        {
            Movements.Add(line);
        }

        PayableText = Money.Format(data.StoredPaisa);
        BooksMatch = data.BooksMatch;
        BooksLabel = data.BooksLabel;
    }

    // ------------------------------------------------------------------- actions

    private void Pay()
    {
        if (Selected is null)
        {
            SetStatus("Pick a supplier first.", true);
            return;
        }

        if (!Money.TryParse(AmountText, out var rupees) || rupees <= 0)
        {
            SetStatus("Type the money paid, for example 5000.", true);
            return;
        }

        var supplierId = Selected.Id;
        var name = Selected.Name;

        var result = _services.SupplierLedger.Pay(_user, supplierId, Money.ToPaisa(rupees), Method, PayNote);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The payment could not be recorded.", true);
            return;
        }

        var payment = result.Value!;
        AmountText = string.Empty;
        PayNote = string.Empty;

        ReloadKeeping(supplierId);
        _onChanged?.Invoke();

        SetStatus(name + ": " + Money.Format(payment.AmountPaisa) + " paid (" + payment.Code + "). Payable now "
                  + Money.Format(payment.NewBalancePaisa) + ".", false);
    }

    private void PrintStatement()
    {
        if (Selected is null)
        {
            SetStatus("Pick a supplier first.", true);
            return;
        }

        var statement = _services.SupplierLedger.Statement(Selected.Id);

        if (!statement.Ok || statement.Value is null)
        {
            SetStatus(statement.Error ?? "The statement could not be prepared.", true);
            return;
        }

        var document = statement.Value;
        var printed = _services.Printer.Print(document);

        SetStatus(printed.Ok
            ? "Statement for " + Selected.Name + " sent to the printer."
            : "The printer did not answer: " + printed.Error, !printed.Ok);
    }

    private void SearchProducts()
    {
        var result = _services.Products.Search(ProductSearch);

        Found.Clear();
        Batches.Clear();

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The medicines could not be searched.", true);
            return;
        }

        foreach (var product in result.Value!.Take(30))
        {
            Found.Add(product);
        }

        if (Found.Count == 0)
        {
            SetStatus("No medicine matches that search.", false);
        }
    }

    private void LoadBatches()
    {
        Batches.Clear();
        SelectedBatch = null;

        if (SelectedProduct is null)
        {
            BatchHint = "Search a medicine, then pick a batch.";
            return;
        }

        var result = _services.Stock.Batches(SelectedProduct.Id);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The batches could not be loaded.", true);
            return;
        }

        foreach (var batch in result.Value!)
        {
            Batches.Add(batch);
        }

        BatchHint = Batches.Count == 0
            ? SelectedProduct.Name + " has no live batch on the shelf."
            : "Pick the batch that is going back.";
    }

    private void AddLine()
    {
        if (SelectedBatch is null || SelectedProduct is null)
        {
            SetStatus("Pick a medicine and a batch first.", true);
            return;
        }

        if (!Qty.TryParse(QtyText, out var qty) || qty <= 0)
        {
            SetStatus("Type how much is going back, for example 2.", true);
            return;
        }

        var perUnit = Packs.PerUnit(SelectedProduct.BoxStrips, SelectedProduct.StripTablets, Unit);
        var qtyBase = Calc.ToBase(qty, perUnit);
        var cost = SelectedBatch.CostPaisa > 0 ? SelectedBatch.CostPaisa : SelectedProduct.CostPaisa;

        // The same batch twice would make the tray lie about the stock, so the second entry replaces
        // the first - one line per batch, like the service expects.
        var existing = Tray.FirstOrDefault(row => row.BatchId == SelectedBatch.Id);
        if (existing is not null)
        {
            Tray.Remove(existing);
        }

        Tray.Add(new ReturnTrayRow
        {
            ProductId = SelectedProduct.Id,
            BatchId = SelectedBatch.Id,
            ProductName = SelectedProduct.Name,
            BatchNo = SelectedBatch.BatchNo,
            QtyEntered = qty,
            Unit = Unit,
            QtyBase = qtyBase,
            CostPaisa = cost
        });

        QtyText = string.Empty;
        RefreshTrayTotal();
        SetStatus("Added: " + SelectedProduct.Name + " (" + SelectedBatch.BatchNo + ").", false);
    }

    private void RemoveLine()
    {
        if (SelectedTrayRow is null)
        {
            SetStatus("Pick a line in the tray first.", true);
            return;
        }

        Tray.Remove(SelectedTrayRow);
        RefreshTrayTotal();
    }

    private void ClearTray()
    {
        Tray.Clear();
        RefreshTrayTotal();
    }

    private void AddExpired()
    {
        if (SelectedExpired is null)
        {
            SetStatus("Pick an expired batch in the list first.", true);
            return;
        }

        var row = SelectedExpired;

        var existing = Tray.FirstOrDefault(line => line.BatchId == row.BatchId);
        if (existing is not null)
        {
            Tray.Remove(existing);
        }

        // The whole batch goes back at purchase cost - the one-click path of Spec 9.3.3.
        Tray.Add(new ReturnTrayRow
        {
            ProductId = row.ProductId,
            BatchId = row.BatchId,
            ProductName = row.ProductName,
            BatchNo = row.BatchNo,
            QtyEntered = row.QtyBase,
            Unit = "unit",
            QtyBase = row.QtyBase,
            CostPaisa = row.CostPaisa
        });

        RefreshTrayTotal();
        SetStatus("Added the whole expired batch: " + row.Caption + ".", false);
    }

    private void RefreshTrayTotal()
    {
        var total = Tray.Sum(row => row.ValuePaisa);
        TrayTotalText = Money.Format(total) + " in " + Tray.Count + " batch(es)";
    }

    private void PostReturn()
    {
        if (Selected is null)
        {
            SetStatus("Pick a supplier first.", true);
            return;
        }

        if (Tray.Count == 0)
        {
            SetStatus("Add at least one batch to the tray.", true);
            return;
        }

        var supplierId = Selected.Id;
        var name = Selected.Name;

        var input = new SupplierReturnInput
        {
            SupplierId = supplierId,
            Reason = Reason,
            Note = string.IsNullOrWhiteSpace(SrNote) ? LineNote : SrNote,
            CashReceived = CashReceived
        };

        foreach (var row in Tray)
        {
            input.Lines.Add(new SupplierReturnLineInput
            {
                ProductId = row.ProductId,
                BatchId = row.BatchId,
                QtyEntered = row.QtyEntered,
                Unit = row.Unit
            });
        }

        var result = _services.SupplierLedger.Return(_user, input);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The return could not be saved.", true);
            return;
        }

        var saved = result.Value!;
        ClearTray();
        SrNote = string.Empty;
        CashReceived = false;

        ReloadKeeping(supplierId);
        LoadExpired();
        _onChanged?.Invoke();

        var message = saved.Code + ": " + name + " took back " + saved.TotalText + " ("
                      + saved.SettlementLabel.ToLowerInvariant() + "). Payable now "
                      + Money.Format(saved.NewPayablePaisa) + ".";

        foreach (var warning in saved.Warnings)
        {
            message += " " + warning;
        }

        SetStatus(message, false);
    }

    /// <summary>Reloads the list but keeps the same supplier open on the right.</summary>
    private void ReloadKeeping(int supplierId)
    {
        var result = _services.SupplierLedger.List(SearchText, OnlyOwing);

        Suppliers.Clear();

        if (result.Ok)
        {
            foreach (var row in result.Value!)
            {
                Suppliers.Add(row);
            }
        }

        Selected = Suppliers.FirstOrDefault(row => row.Id == supplierId) ?? Suppliers.FirstOrDefault();

        // The list hands back fresh objects, so Selected may not raise a change event; the page is
        // refreshed directly to be certain the new payable is on screen.
        ShowSupplier();
    }

    /// <summary>Opens the photo window for the supplier whose account is on the screen.</summary>
    private void OpenPhotos()
    {
        if (Selected is null)
        {
            SetStatus("Choose a supplier first.", true);
            return;
        }

        PhotoWindow.Show(_services, _user, PhotoEntity.Supplier, Selected.Id, Selected.Name);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }

    /// <summary>One batch waiting to go back: what the tray shows and what `SR-nnnn` will save.</summary>
    public sealed class ReturnTrayRow
    {
        public int ProductId { get; set; }

        public int BatchId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string BatchNo { get; set; } = string.Empty;

        public double QtyEntered { get; set; }

        public string Unit { get; set; } = "unit";

        public double QtyBase { get; set; }

        public long CostPaisa { get; set; }

        public long ValuePaisa => Calc.Round(QtyBase * CostPaisa);

        public string QtyLabel => Qty.Format(QtyEntered) + " " + Unit;

        public string ValueLabel => Money.Format(ValuePaisa);

        public string Caption => ProductName + " - " + BatchNo;
    }
}
