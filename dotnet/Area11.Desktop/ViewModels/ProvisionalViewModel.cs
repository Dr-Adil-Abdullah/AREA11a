using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// No-bill (rush) returns, `PR-nnnn` (F-25, 15.5, U-33).
///
/// Left: the counter. Search a medicine, add what came back, type the money that was handed over
/// and save - the goods go on the shelf and the customer leaves with a slip.
/// Right: the paper trail. Every entry stays pending, red, until the real bill is attached; after
/// that it turns amber and waits for the owner. Attaching a bill never touches stock a second time.
///
/// The whole rule set (stock, numbering, who may attach/cancel, compulsory reason) is in
/// ProvisionalReturnService - this class only moves text and commands.
/// </summary>
public sealed class ProvisionalViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private string _searchText = string.Empty;
    private string _qtyText = "1";
    private string _unit = "box";
    private string _refundText = string.Empty;
    private string _customerName = string.Empty;
    private string _phone = string.Empty;
    private string _reason = ReturnService.ReasonChangedMind;
    private string _notes = string.Empty;
    private string _statusText = "Type a medicine name, add the quantity, type the money you paid back, then Save.";
    private bool _isStatusError;
    private string _totalText = "Rs 0";
    private string _billCode = string.Empty;
    private string _cancelReason = string.Empty;
    private string _lookupText = string.Empty;
    private string _pendingCaption = "Nothing is waiting for a bill.";
    private Product? _selectedProduct;
    private PrLine? _selectedLine;
    private ProvisionalRow? _selectedEntry;
    private BillSearchRow? _selectedLookup;

    public ProvisionalViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        SearchCommand = new RelayCommand(Search);
        AddCommand = new RelayCommand(AddLine);
        RemoveCommand = new RelayCommand(RemoveLine);
        SaveCommand = new RelayCommand(Save);
        RefreshCommand = new RelayCommand(Load);
        LinkCommand = new RelayCommand(LinkBill);
        ReprintCommand = new RelayCommand(Reprint);
        LookupCommand = new RelayCommand(SearchBills);
        PickBillCommand = new RelayCommand(PickBill);
        CancelCommand = new RelayCommand(CancelEntry);

        Load();
    }

    public ObservableCollection<Product> Results { get; } = new();

    public ObservableCollection<PrLine> Lines { get; } = new();

    public ObservableCollection<ProvisionalRow> Pending { get; } = new();

    public ObservableCollection<ProvisionalRow> History { get; } = new();

    public ObservableCollection<BillSearchRow> Lookup { get; } = new();

    public IReadOnlyList<string> Units { get; } = new[] { "box", "strip", "unit" };

    public IReadOnlyList<string> Reasons { get; } = ReturnService.Reasons;

    public RelayCommand SearchCommand { get; }

    public RelayCommand AddCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand LinkCommand { get; }

    public RelayCommand ReprintCommand { get; }

    public RelayCommand LookupCommand { get; }

    public RelayCommand PickBillCommand { get; }

    public RelayCommand CancelCommand { get; }

    public bool IsOwnerOrManager =>
        string.Equals(_user.Role, "owner", StringComparison.OrdinalIgnoreCase)
        || string.Equals(_user.Role, "manager", StringComparison.OrdinalIgnoreCase);

    public bool IsNotOwnerOrManager => !IsOwnerOrManager;

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
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

    public string RefundText
    {
        get => _refundText;
        set => SetProperty(ref _refundText, value);
    }

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string BillCode
    {
        get => _billCode;
        set => SetProperty(ref _billCode, value);
    }

    public string CancelReason
    {
        get => _cancelReason;
        set => SetProperty(ref _cancelReason, value);
    }

    public string LookupText
    {
        get => _lookupText;
        set => SetProperty(ref _lookupText, value);
    }

    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    public PrLine? SelectedLine
    {
        get => _selectedLine;
        set => SetProperty(ref _selectedLine, value);
    }

    public BillSearchRow? SelectedLookup
    {
        get => _selectedLookup;
        set => SetProperty(ref _selectedLookup, value);
    }

    public ProvisionalRow? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                OnPropertyChanged(nameof(SelectedCaption));
            }
        }
    }

    public string SelectedCaption => _selectedEntry is null
        ? "Pick an entry from the list first."
        : _selectedEntry.Code + " · " + _selectedEntry.RefundText + " · " + _selectedEntry.StatusLabel;

    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    public string PendingCaption
    {
        get => _pendingCaption;
        private set => SetProperty(ref _pendingCaption, value);
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

    // ------------------------------------------------------------------ commands

    private void Search()
    {
        var result = _services.Products.Search(SearchText);

        Results.Clear();

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The medicines could not be searched.", true);
            return;
        }

        foreach (var product in result.Value!.Take(40))
        {
            Results.Add(product);
        }

        SetStatus(Results.Count == 0
            ? "No medicine found with that name."
            : Results.Count + " medicine(s) found - pick one and press Add.", false);
    }

    private void AddLine()
    {
        if (SelectedProduct is null)
        {
            SetStatus("Pick a medicine from the list first.", true);
            return;
        }

        if (!Qty.TryParse(QtyText, out var qty) || qty <= 0)
        {
            SetStatus("The quantity must be more than zero.", true);
            return;
        }

        var existing = Lines.FirstOrDefault(line =>
            line.ProductId == SelectedProduct.Id
            && string.Equals(line.Unit, Unit, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Quantity += qty;
            RefreshTotal();
            SetStatus(existing.Name + " is now " + Qty.Format(existing.Quantity) + " " + existing.Unit + ".", false);
            return;
        }

        Lines.Add(new PrLine
        {
            ProductId = SelectedProduct.Id,
            Name = SelectedProduct.Name,
            PackLabel = SelectedProduct.PackLabel,
            Quantity = qty,
            Unit = Unit
        });

        RefreshTotal();
        SetStatus(SelectedProduct.Name + " added.", false);
    }

    private void RemoveLine()
    {
        if (SelectedLine is null)
        {
            SetStatus("Pick a line in the list to remove it.", true);
            return;
        }

        Lines.Remove(SelectedLine);
        SelectedLine = null;
        RefreshTotal();
        SetStatus("Line removed.", false);
    }

    private void Save()
    {
        if (Lines.Count == 0)
        {
            SetStatus("Add at least one medicine before saving.", true);
            return;
        }

        if (!Money.TryParse(RefundText, out var rupees) || rupees < 0)
        {
            SetStatus("Type the money you paid back, for example 250.", true);
            return;
        }

        var input = new ProvisionalInput
        {
            CustomerName = CustomerName,
            Phone = Phone,
            RefundPaisa = Money.ToPaisa(rupees),
            Reason = Reason,
            Notes = Notes,
            Lines = Lines.Select(line => new ProvisionalLineInput
            {
                ProductId = line.ProductId,
                QtyEntered = line.Quantity,
                Unit = line.Unit
            }).ToList()
        };

        var result = _services.Provisional.Create(input, _user);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The no-bill return could not be saved.", true);
            return;
        }

        var saved = result.Value!;
        var code = saved.Code;

        // Fields are cleared by hand; Load() must not wipe the message (v0.3.0 lesson).
        Lines.Clear();
        RefundText = string.Empty;
        Notes = string.Empty;
        CustomerName = string.Empty;
        Phone = string.Empty;
        SearchText = string.Empty;
        Results.Clear();
        SelectedProduct = null;
        SelectedLine = null;
        RefreshTotal();
        Load();

        var printerProblem = Print(code);
        var extra = saved.Warnings.Count > 0 ? " " + saved.Warnings[0] : string.Empty;

        if (printerProblem is null)
        {
            SetStatus(code + " saved - " + Money.Format(saved.RefundPaisa) + " paid back, goods on the shelf." + extra, false);
        }
        else
        {
            SetStatus(code + " saved - " + Money.Format(saved.RefundPaisa) + " paid back. " + printerProblem + extra, true);
        }
    }

    private void LinkBill()
    {
        if (SelectedEntry is null)
        {
            SetStatus("Pick a pending entry first.", true);
            return;
        }

        var result = _services.Provisional.Link(SelectedEntry.Id, BillCode, _user);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The bill could not be attached.", true);
            return;
        }

        var caption = SelectedEntry.Code;
        BillCode = string.Empty;
        Lookup.Clear();
        LookupText = string.Empty;
        Load();
        SetStatus(caption + ": bill attached. Stock was NOT added again; the alert waits for the owner.", false);
    }

    private void CancelEntry()
    {
        if (SelectedEntry is null)
        {
            SetStatus("Pick a pending entry first.", true);
            return;
        }

        var result = _services.Provisional.Cancel(SelectedEntry.Id, CancelReason, _user);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The entry could not be cancelled.", true);
            return;
        }

        var caption = SelectedEntry.Code;
        CancelReason = string.Empty;
        Load();
        SetStatus(caption + " cancelled - the stock that was added has been taken back out.", false);
    }

    // -------------------------------------------------------------------- loading

    public void Load()
    {
        Pending.Clear();
        var pending = _services.Provisional.Pending();
        foreach (var row in pending)
        {
            Pending.Add(row);
        }

        History.Clear();
        foreach (var row in _services.Provisional.History().Where(row => !row.IsPending))
        {
            History.Add(row);
        }

        var keep = SelectedEntry?.Id;
        SelectedEntry = Pending.FirstOrDefault(row => row.Id == keep) ?? Pending.FirstOrDefault();

        PendingCaption = pending.Count == 0
            ? "Nothing is waiting for a bill."
            : pending.Count + " entr(y/ies) waiting for a bill - "
              + Money.Format(pending.Sum(row => row.RefundPaisa)) + " paid out with no bill.";
    }

    /// <summary>Prints the slip of the selected entry - the manual-print path for PR.</summary>
    private void Reprint()
    {
        if (SelectedEntry is null)
        {
            SetStatus("Pick an entry to print.", true);
            return;
        }

        var receipt = _services.ProvisionalReceipts.Build(SelectedEntry.Code);

        if (!receipt.Ok)
        {
            SetStatus(receipt.Error ?? "The slip could not be prepared.", true);
            return;
        }

        var printed = _services.Printer.Print(receipt.Value!);

        SetStatus(printed.Ok
            ? SelectedEntry.Code + " sent to the printer."
            : "The printer did not answer: " + printed.Error, !printed.Ok);
    }

    /// <summary>Smart bill lookup for the selected entry (9.2.1): phone, name, medicine or date.</summary>
    public void SearchBills()
    {
        var result = _services.Provisional.SearchBills(LookupText);

        Lookup.Clear();

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The bills could not be searched.", true);
            return;
        }

        foreach (var row in result.Value!)
        {
            Lookup.Add(row);
        }

        SetStatus(Lookup.Count == 0 ? "No bill matches that." : Lookup.Count + " bill(s) found - double-click the right one.", false);
    }

    /// <summary>Fills the bill box from the lookup list (no database work, no side effects).</summary>
    private void PickBill()
    {
        var row = SelectedLookup;

        if (row is not null)
        {
            BillCode = row.Code;
            SetStatus(row.Code + " picked - press Attach this bill.", false);
        }
    }

    /// <summary>
    /// Prints the slip when the shop prints automatically. Manual mode leaves it to the Reprint
    /// button. Returns the printer's complaint, or null when there is nothing to report.
    /// </summary>
    private string? Print(string code)
    {
        if (!_services.Settings.GetBool("printer.autoPrint", true))
        {
            return null;
        }

        var receipt = _services.ProvisionalReceipts.Build(code);

        if (!receipt.Ok)
        {
            return receipt.Error ?? "The slip could not be prepared.";
        }

        var printed = _services.Printer.Print(receipt.Value!);

        return printed.Ok ? null : "The printer did not answer: " + printed.Error;
    }

    private void RefreshTotal()
    {
        TotalText = Lines.Count == 0
            ? "No items yet"
            : Lines.Count + " item(s): " + string.Join(", ", Lines.Select(line => line.Label));
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}

/// <summary>One line of the no-bill return being typed.</summary>
public sealed class PrLine : ObservableObject
{
    private double _quantity;

    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PackLabel { get; set; } = string.Empty;

    public string Unit { get; set; } = "box";

    public double Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    public string Label => Qty.Format(Quantity) + " " + Unit + " - " + Name;
}
