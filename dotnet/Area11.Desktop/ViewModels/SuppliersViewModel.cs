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
/// payable by a credit note, and refuses stock below zero under D-93.
/// D-114 keeps accounting amounts readable under the Supplier module, not direct cost previews.
/// </summary>
public sealed class SuppliersViewModel : ObservableObject, IEmptyPage
{    /// <summary>
    /// Why this tab has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it has
    /// rows** - the container page shows whichever tab is open.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(Suppliers.Count == 0, "Suppliers");


    private readonly AppServices _services;
    private readonly User _user;
    private readonly Action? _onChanged;

    private string _searchText = string.Empty;
    private string _amountText = string.Empty;
    private string _method = "cash";
    private string _payNote = string.Empty;
    private bool _payInProgress;
    private bool _lastSupplierLoadSucceeded;
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
    private List<SupplierSummary> _printSelection = new();
    private bool _canPrintSelection;
    private bool _accessAllowed = true;
    private bool? _lastCostKnown;
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

        Editor = new CustomFieldsEditorViewModel(services, user, CustomFieldEntity.Supplier);
        PayCommand = new RelayCommand(Pay);
        StatementCommand = new RelayCommand(PrintStatement);
        PrintSelectionCommand = new RelayCommand(PrintSelection);
        PreviewCommand = new RelayCommand(Preview);
        WhatsAppStatementCommand = new RelayCommand(SendStatementOnWhatsApp);
        WhatsAppProfileCommand = new RelayCommand(SendProfileOnWhatsApp);
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

    /// <summary>The owner's own questions for a supplier (F-42) - drawn only if he made any.</summary>
    public CustomFieldsEditorViewModel Editor { get; }

    public RelayCommand PayCommand { get; }

    public RelayCommand StatementCommand { get; }

    /// <summary>Portion 11: the ticked suppliers, as one bulk job (the "Print selected" button).</summary>
    public RelayCommand PrintSelectionCommand { get; }

    /// <summary>Portion 11: the exact paper text on the screen before anything prints.</summary>
    public RelayCommand PreviewCommand { get; }

    /// <summary>Portion 12: the open statement as WhatsApp text plus a PDF the owner attaches by hand.</summary>
    public RelayCommand WhatsAppStatementCommand { get; }

    /// <summary>Portion 12: the short payable note (what the shop owes) as WhatsApp text, no file.</summary>
    public RelayCommand WhatsAppProfileCommand { get; }

    public RelayCommand ProductSearchCommand { get; }

    public RelayCommand AddLineCommand { get; }

    public RelayCommand RemoveLineCommand { get; }

    public RelayCommand ClearTrayCommand { get; }

    public RelayCommand PostReturnCommand { get; }

    public RelayCommand AddExpiredCommand { get; }

    public bool CanWork => _accessAllowed;

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
        _lastSupplierLoadSucceeded = false;
        if (!UpdateAccess()) return;
        var keep = Selected?.Id;

        var result = _services.SupplierLedger.List(_user, SearchText, OnlyOwing);

        Suppliers.Clear();

        if (!result.Ok || result.Value is null)
        {
            ClearSensitiveData();
            SetStatus(result.Error ?? "The suppliers could not be loaded.", true);
            return;
        }

        foreach (var row in result.Value!)
        {
            Suppliers.Add(row);
        }

        _lastSupplierLoadSucceeded = true;
        Selected = Suppliers.FirstOrDefault(row => row.Id == keep) ?? Suppliers.FirstOrDefault();

        if (Suppliers.Count == 0)
        {
            SetStatus(OnlyOwing ? "Every supplier is settled." : "No supplier matches that search.", false);
        }

        LoadExpired();
    }

    private void LoadExpired()
    {
        SelectedExpired = null;
        Expired.Clear();

        var result = _services.SupplierLedger.ExpiredBatches(_user);

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
        Editor.Load(Selected?.Id ?? 0);

        ClearAccount();
        if (Selected is null) return;
        var page = _services.SupplierLedger.Page(_user, Selected.Id);
        if (!page.Ok || page.Value is null)
        {
            ClearSensitiveData();
            BooksMatch = false;
            BooksLabel = page.Error ?? "The account could not be loaded.";
            return;
        }
        var data = page.Value;
        HasSupplier = true;
        NameText = data.Summary.Name;
        AgencyText = data.Summary.AgencyLabel;
        PhoneText = data.Summary.PhoneLabel;
        PayableText = data.Summary.PayableText;
        PayableLabel = data.Summary.PayableLabel;
        PurchasesText = data.Summary.PurchasesText;
        PaidText = data.Summary.PaidText;
        CreditedText = data.Summary.CreditedText;
        ActivityText = data.Summary.ActivityText;
        LastPurchaseText = "Last purchase: " + data.Summary.LastPurchaseLabel;

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
        if (_payInProgress) return;
        _payInProgress = true;
        try
        {
            if (!UpdateAccess()) return;
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
                if (!UpdateAccess()) return;
                SetStatus(result.Error ?? "The payment could not be recorded.", true);
                return;
            }

            var payment = result.Value!;
            try
            {
                AmountText = string.Empty;
                PayNote = string.Empty;
                ReloadKeeping(supplierId);
                if (!_lastSupplierLoadSucceeded)
                {
                    SetStatus("SAVED. Supplier payment was stored, but the account could not refresh. Do not pay again; refresh and check the payment history.", true);
                    return;
                }
                _onChanged?.Invoke();
                if (!UpdateAccess())
                {
                    SetStatus("SAVED. Supplier payment was stored, but current access changed. Do not pay again; sign in or refresh to check the payment history.", true);
                    return;
                }
                SetStatus("SAVED. " + name + ": " + Money.Format(payment.AmountPaisa) + " paid (" + payment.Code + "). Payable now "
                          + Money.Format(payment.NewBalancePaisa) + ".", false);
            }
            catch (Exception exception)
            {
                _services.Log.Error("Supplier payment was committed but its page or callback could not finish.", exception);
                ClearSensitiveData();
                SetStatus("SAVED. Supplier payment was stored, but the page could not finish. Do not pay again; refresh and check the payment history.", true);
            }
        }
        finally { _payInProgress = false; }
    }

    /// <summary>True when two or more suppliers are ticked for the bulk buttons.</summary>
    public bool CanPrintSelection
    {
        get => _canPrintSelection;
        private set => SetProperty(ref _canPrintSelection, value);
    }

    /// <summary>
    /// The list's ticked rows, carried over by the code-behind (a list cannot bind its own
    /// multi-selection). Only ids are kept: the paper is rebuilt from the database at print time.
    /// </summary>
    public void SetPrintSelection(IEnumerable<SupplierSummary> rows)
    {
        _printSelection = (rows ?? Enumerable.Empty<SupplierSummary>()).Where(row => row is not null).Distinct().ToList();
        CanPrintSelection = _printSelection.Count > 1;
    }

    private void PrintStatement()
    {
        if (!UpdateAccess()) return;
        // Portion 11: with `printer.defaultBulk` on and several suppliers ticked, the main button
        // prints the whole ticked set; otherwise it prints the open supplier, as it always has.
        if (_printSelection.Count > 1 && _services.Settings.GetBool("printer.defaultBulk", false))
        {
            PrintBulk(_printSelection.Select(row => row.Id).ToList());
            return;
        }

        if (Selected is null)
        {
            SetStatus("Pick a supplier first.", true);
            return;
        }

        var statement = _services.SupplierLedger.Statement(_user, Selected.Id);

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

    /// <summary>The explicit bulk button: always the ticked suppliers, whatever the setting says.</summary>
    private void PrintSelection()
    {
        if (!UpdateAccess()) return;
        if (_printSelection.Count < 2)
        {
            SetStatus("Tick two or more suppliers first (hold Ctrl and click).", true);
            return;
        }

        PrintBulk(_printSelection.Select(row => row.Id).ToList());
    }

    private void PrintBulk(List<int> ids)
    {
        var job = _services.SupplierLedger.BuildBulkStatements(_user, ids);
        if (!job.Ok || job.Value is null)
        {
            SetStatus(job.Error ?? "The statements could not be prepared.", true);
            return;
        }

        var printed = _services.Printer.PrintBulk(job.Value);
        SetStatus(printed.Ok
            ? "Printed " + ids.Count + " statements."
            : "The printer did not answer: " + printed.Error, !printed.Ok);
    }

    /// <summary>
    /// The paper text on the screen first: the ticked set when several are ticked, else the open
    /// supplier's statement.
    /// </summary>
    private void Preview()
    {
        if (!UpdateAccess()) return;
        if (_printSelection.Count > 1)
        {
            var job = _services.SupplierLedger.BuildBulkStatements(_user, _printSelection.Select(row => row.Id).ToList());
            if (!job.Ok || job.Value is null)
            {
                SetStatus(job.Error ?? "The statements could not be prepared.", true);
                return;
            }

            PrintPreviewDoor.ShowBulk(_services, job.Value);
            return;
        }

        if (Selected is null)
        {
            SetStatus("Pick a supplier first.", true);
            return;
        }

        var statement = _services.SupplierLedger.Statement(_user, Selected.Id);
        if (!statement.Ok || statement.Value is null)
        {
            SetStatus(statement.Error ?? "The statement could not be prepared.", true);
            return;
        }

        PrintPreviewDoor.Show(_services, statement.Value);
    }

    /// <summary>
    /// The open supplier's statement to WhatsApp: the statement text in the message, the same
    /// statement as a PDF file. The supplier's own phone is dialled when the book has one.
    /// </summary>
    private void SendStatementOnWhatsApp()
    {
        if (!UpdateAccess()) return;
        if (Selected is null)
        {
            SetStatus("Pick a supplier first.", true);
            return;
        }

        var statement = _services.SupplierLedger.Statement(_user, Selected.Id);
        if (!statement.Ok || statement.Value is null)
        {
            SetStatus(statement.Error ?? "The statement could not be prepared.", true);
            return;
        }

        var sent = WhatsAppFlow.Send(_services, Selected.Phone, statement.Value.PlainText,
            statement.Value.Title, statement.Value.Lines.Select(line => line.Text),
            "statement-" + Selected.Name + ".pdf");
        SetStatus(sent.Message, !sent.Ok);
    }

    /// <summary>The short payable note to WhatsApp: what the shop owes, in one message, no file.</summary>
    private void SendProfileOnWhatsApp()
    {
        if (!UpdateAccess()) return;
        if (Selected is null)
        {
            SetStatus("Pick a supplier first.", true);
            return;
        }

        var page = _services.SupplierLedger.Page(_user, Selected.Id);
        if (!page.Ok || page.Value is null)
        {
            ClearSensitiveData();
            SetStatus(page.Error ?? "The current supplier account could not be loaded.", true);
            return;
        }
        var summary = page.Value.Summary;
        var text = _services.WhatsApp.BuildSupplierProfile(summary.Name, summary.Agency, summary.PayablePaisa);
        var sent = WhatsAppFlow.Send(_services, summary.Phone, text,
            "Profile", Enumerable.Empty<string>(), "profile.pdf", withPdf: false);
        SetStatus(sent.Message, !sent.Ok);
    }

    private void SearchProducts()
    {
        if (!UpdateAccess()) return;
        var result = _services.Products.Search(_user, ProductSearch, purpose: ProductReadPurpose.SupplierReturn);

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

        var result = _services.Stock.Batches(_user, SelectedProduct.Id, ProductReadPurpose.SupplierReturn);

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
        if (!UpdateAccess()) return;
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

        var prepared = _services.SupplierLedger.PrepareReturnLine(_user, SelectedProduct.Id, SelectedBatch.Id);
        if (!prepared.Ok || prepared.Value is null)
        {
            SetStatus(prepared.Error ?? "The current batch could not be prepared. No tray values were changed.", true);
            return;
        }
        var fresh = prepared.Value;
        var perUnit = Packs.PerUnit(fresh.BoxStrips, fresh.StripTablets, Unit);
        var qtyBase = Calc.ToBase(qty, perUnit);
        var cost = fresh.CostPaisa;

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
            ProductName = fresh.ProductName,
            BatchNo = fresh.BatchNo ?? string.Empty,
            QtyEntered = qty,
            Unit = Unit,
            QtyBase = qtyBase,
            CostPaisa = cost,
            CostKnown = fresh.CostKnown
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
        if (!UpdateAccess()) return;
        if (SelectedExpired is null)
        {
            SetStatus("Pick an expired batch in the list first.", true);
            return;
        }

        // Re-read on use: the displayed object may predate a permission/account/stock change.
        var selectedBatchId = SelectedExpired.BatchId;
        var current = _services.SupplierLedger.ExpiredBatches(_user, 500);
        var row = current.Value?.FirstOrDefault(item => item.BatchId == selectedBatchId);
        if (!current.Ok || row is null)
        {
            SetStatus(current.Error ?? "That batch is no longer in the current expired-stock selection. Refresh and try again.", true);
            return;
        }
        var prepared = _services.SupplierLedger.PrepareReturnLine(_user, row.ProductId, row.BatchId);
        if (!prepared.Ok || prepared.Value is null)
        {
            SetStatus(prepared.Error ?? "The expired return line could not be prepared.", true);
            return;
        }
        var fresh = prepared.Value;

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
            QtyEntered = fresh.QtyBase,
            Unit = "unit",
            QtyBase = fresh.QtyBase,
            CostPaisa = fresh.CostPaisa,
            CostKnown = fresh.CostKnown
        });

        RefreshTrayTotal();
        SetStatus("Added the whole expired batch: " + row.Caption + ".", false);
    }

    private void RefreshTrayTotal()
    {
        var total = Tray.Sum(row => row.ValuePaisa);
        TrayTotalText = Tray.Any(row => !row.CostKnown)
            ? "Cost hidden — quantity-only tray (" + Tray.Count + " batch(es)); settlement calculated on save"
            : Money.Format(total) + " in " + Tray.Count + " batch(es)";
    }

    private void PostReturn()
    {
        if (!UpdateAccess()) return;
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
        _lastSupplierLoadSucceeded = false;
        if (!UpdateAccess()) return;
        var result = _services.SupplierLedger.List(_user, SearchText, OnlyOwing);
        if (!result.Ok || result.Value is null)
        {
            ClearSensitiveData();
            SetStatus(result.Error ?? "The suppliers could not be loaded.", true);
            return;
        }
        Suppliers.Clear();
        foreach (var row in result.Value) Suppliers.Add(row);
        _lastSupplierLoadSucceeded = true;
        Selected = Suppliers.FirstOrDefault(row => row.Id == supplierId) ?? Suppliers.FirstOrDefault();
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

    public void RefreshAccess()
    {
        var wasAllowed = _accessAllowed;
        if (!UpdateAccess()) return;
        if (!wasAllowed) Load();
    }

    private bool UpdateAccess()
    {
        var access = _services.SupplierLedger.CheckReadAccess(_user);
        if (!access.Ok || access.Value is null)
        {
            SetAccessAllowed(false);
            _lastCostKnown = null;
            ClearSensitiveData();
            SetStatus(access.Error ?? "Current Supplier access is required.", true);
            return false;
        }
        SetAccessAllowed(true);
        if (_lastCostKnown != access.Value.CostKnown)
        {
            _lastCostKnown = access.Value.CostKnown;
            MaskDirectCosts();
            LoadExpired();
        }
        return true;
    }

    private void SetAccessAllowed(bool allowed)
    {
        if (SetProperty(ref _accessAllowed, allowed, nameof(CanWork)))
            OnPropertyChanged(nameof(IsNotAllowed));
    }

    private void MaskDirectCosts()
    {
        Found.Clear();
        SelectedProduct = null;
        Batches.Clear();
        SelectedBatch = null;
        SelectedExpired = null;
        SelectedTrayRow = null;
        BatchHint = "Choose a fresh batch. Direct costs require Cost permission and main ON.";
        var rows = Tray.ToList();
        Tray.Clear();
        foreach (var row in rows)
        {
            row.CostKnown = false;
            row.CostPaisa = 0;
            Tray.Add(row); // rebind computed labels; quantities/IDs remain intact
        }
        RefreshTrayTotal();
    }

    private void ClearAccount()
    {
        HasSupplier = false;
        Movements.Clear();
        NameText = string.Empty;
        AgencyText = PhoneText = ActivityText = LastPurchaseText = "—";
        PayableText = PayableLabel = PurchasesText = PaidText = CreditedText = "hidden";
        BooksLabel = "Select a supplier with current Supplier access.";
        BooksMatch = false;
    }

    private void ClearSensitiveData()
    {
        AmountText = PayNote = string.Empty;
        _lastSupplierLoadSucceeded = false;
        StatusText = string.Empty;
        IsStatusError = false;
        Suppliers.Clear();
        Selected = null;
        ClearAccount();
        Expired.Clear();
        MaskDirectCosts();
        ClearTray();
        _printSelection.Clear();
        CanPrintSelection = false;
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

        public bool CostKnown { get; set; }

        public long ValuePaisa => CostKnown ? Calc.Round(QtyBase * CostPaisa) : 0;

        public string QtyLabel => Qty.Format(QtyEntered) + " " + Unit;

        public string ValueLabel => CostKnown ? Money.Format(ValuePaisa) : "hidden";

        public string Caption => ProductName + " - " + BatchNo;
    }
}
