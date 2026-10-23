using System.Collections.ObjectModel;
using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Returns (F-21, F-23): find the bill, type how many came back, and take the return. Two tabs:
///   • Take a return - the bill, its lines, the reason and where the goods go.
///   • Quarantine    - goods waiting for a decision, with the three outcomes of U-34.
///
/// Every rule lives in Core (ReturnService / QuarantineService): this class only moves text between
/// the screen and those services, and shows the refund the service calculated.
/// </summary>
public sealed class ReturnsViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(History.Count == 0, "Returns");

    private readonly AppServices _services;
    private readonly User _user;

    private string _billCode = string.Empty;
    private string _reason = ReturnService.ReasonChangedMind;
    private string _note = string.Empty;
    private bool _restock = true;
    private string _refundText = "Rs 0";
    private string _cashText = "Rs 0";
    private string _creditText = "Rs 0";
    private string _statusText = "Type the bill number (for example INV-0001) and press Find.";
    private bool _isStatusError;
    private int _saleId;
    private string _billSummary = string.Empty;
    // ---- "Which bill was it?" (F-20): a list, never a guess ----
    private string _searchText = string.Empty;
    private string _fromDate = string.Empty;
    private string _toDate = string.Empty;
    private SaleLookupRow? _selectedMatch;
    private string _searchCaption =
        "Type a bill number, a phone, a name or a medicine - or choose a date range.";

    private string _quarantineCaption = "No goods are waiting for a decision.";
    private bool _busy;

    public ReturnsViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        FindCommand = new RelayCommand(Find);
        SearchCommand = new RelayCommand(SearchBills);
        OpenMatchCommand = new RelayCommand(OpenMatch);
        TakeCommand = new RelayCommand(TakeReturn);
        RefreshQuarantineCommand = new RelayCommand(LoadQuarantine);
        ClearCommand = new RelayCommand(Clear);

        LoadQuarantine();
    }

    public ObservableCollection<ReturnLineRow> Lines { get; } = new();

    /// <summary>Bills that match the search, newest first.</summary>
    public ObservableCollection<SaleLookupRow> Matches { get; } = new();

    public ObservableCollection<QuarantineRow> Pending { get; } = new();

    public ObservableCollection<QuarantineItem> History { get; } = new();

    public IReadOnlyList<string> Reasons { get; } = ReturnService.Reasons;

    public RelayCommand FindCommand { get; }

    /// <summary>Finds a bill by anything the customer remembers (F-20).</summary>
    public RelayCommand SearchCommand { get; }

    /// <summary>Opens the bill chosen in that list, exactly as if its number had been typed.</summary>
    public RelayCommand OpenMatchCommand { get; }

    public RelayCommand TakeCommand { get; }

    public RelayCommand RefreshQuarantineCommand { get; }

    public RelayCommand ClearCommand { get; }

    public string BillCode
    {
        get => _billCode;
        set => SetProperty(ref _billCode, value);
    }

    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    /// <summary>True = the goods go straight back on the shelf (U-32).</summary>
    public bool Restock
    {
        get => _restock;
        set
        {
            if (SetProperty(ref _restock, value))
            {
                OnPropertyChanged(nameof(ToQuarantine));
            }
        }
    }

    /// <summary>False = the goods go to quarantine with a Q-number (U-34).</summary>
    public bool ToQuarantine
    {
        get => !_restock;
        set => Restock = !value;
    }

    public string RefundText
    {
        get => _refundText;
        private set => SetProperty(ref _refundText, value);
    }

    public string CashText
    {
        get => _cashText;
        private set => SetProperty(ref _cashText, value);
    }

    public string CreditText
    {
        get => _creditText;
        private set => SetProperty(ref _creditText, value);
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

    public string BillSummary
    {
        get => _billSummary;
        private set => SetProperty(ref _billSummary, value);
    }

    public string QuarantineCaption
    {
        get => _quarantineCaption;
        private set => SetProperty(ref _quarantineCaption, value);
    }

    public bool HasBill => _saleId > 0;

    // -------------------------------------------------------------- the bill

    private void Find()
    {
        var found = _services.Returns.FindSale(BillCode);

        // `ServiceResult<int>.Value` is a plain `int`, not `int?`: the `T?` in the model is only a
        // nullable annotation, and a value type stays non-nullable. So the answer is checked with
        // `Ok` and `<= 0`, never with `is null` (that was CS0037 and stopped the first Desktop build).
        if (!found.Ok || found.Value <= 0)
        {
            ResetBill();
            SetStatus(found.Error ?? "That bill was not found.", true);
            return;
        }

        var saleId = found.Value;

        var lines = _services.Returns.Lines(saleId);
        if (!lines.Ok || lines.Value is null)
        {
            ResetBill();
            SetStatus(lines.Error ?? "The bill could not be loaded.", true);
            return;
        }

        _saleId = saleId;
        Lines.Clear();

        foreach (var line in lines.Value)
        {
            var row = new ReturnLineRow(line, Recalculate);
            Lines.Add(row);
        }

        BillSummary = $"{BillCode.Trim().ToUpperInvariant()} · {Lines.Count} line(s) · " +
                      $"refundable in cash today {Money.Format(lines.Value.Sum(line => line.RemainingPaisa))}";
        OnPropertyChanged(nameof(HasBill));

        SetStatus(Lines.Count == 0
            ? "This bill has nothing left to return."
            : "Type the quantity coming back on the lines that apply, then press Take the return.", false);

        Recalculate();
    }

    // ------------------------------------------------- "which bill was it?" (F-20)

    /// <summary>Anything the customer remembers: bill number, phone, name, medicine.</summary>
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public string ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public SaleLookupRow? SelectedMatch
    {
        get => _selectedMatch;
        set => SetProperty(ref _selectedMatch, value);
    }

    /// <summary>What the list is showing, in words - never a silent zero.</summary>
    public string SearchCaption
    {
        get => _searchCaption;
        private set => SetProperty(ref _searchCaption, value);
    }

    public bool HasMatches => Matches.Count > 0;

    private void SearchBills()
    {
        var found = _services.Returns.Search(new SaleLookupInput
        {
            Text = SearchText,
            FromDate = FromDate,
            ToDate = ToDate
        });

        if (!found.Ok || found.Value is null)
        {
            Matches.Clear();
            SelectedMatch = null;
            OnPropertyChanged(nameof(HasMatches));
            SearchCaption = found.Error ?? "The bills could not be searched.";
            return;
        }

        Matches.Clear();

        foreach (var row in found.Value)
        {
            Matches.Add(row);
        }

        SelectedMatch = Matches.FirstOrDefault();
        OnPropertyChanged(nameof(HasMatches));

        SearchCaption = Matches.Count switch
        {
            0 => "No bill matches that. Try fewer letters, or another day.",
            100 => "Showing the newest 100 bills. Narrow it down with a date if the bill is not here.",
            _ => Matches.Count == 1
                ? "One bill found."
                : Matches.Count.ToString(CultureInfo.InvariantCulture) + " bills found - pick the right one."
        };
    }

    /// <summary>
    /// Opens the chosen bill: sets the number box and runs the same <see cref="Find"/> the typed
    /// number runs, so there is one way into a bill and one set of checks behind it.
    /// </summary>
    private void OpenMatch()
    {
        var row = SelectedMatch;

        if (row is null)
        {
            SetStatus("Pick a bill in the list first.", true);
            return;
        }

        BillCode = row.Code;
        Find();
    }

    private void ResetBill()
    {
        _saleId = 0;
        Lines.Clear();
        BillSummary = string.Empty;
        OnPropertyChanged(nameof(HasBill));
        RefundText = "Rs 0";
        CashText = "Rs 0";
        CreditText = "Rs 0";
    }

    private void Clear()
    {
        BillCode = string.Empty;
        SearchText = string.Empty;
        FromDate = string.Empty;
        ToDate = string.Empty;
        Matches.Clear();
        SelectedMatch = null;
        OnPropertyChanged(nameof(HasMatches));
        SearchCaption = "Type a bill number, a phone, a name or a medicine - or choose a date range.";
        Note = string.Empty;
        Reason = ReturnService.ReasonChangedMind;
        Restock = true;
        ResetBill();
        SetStatus("Ready for the next bill.", false);
    }

    // ------------------------------------------------------------- the refund

    private ReturnInput BuildInput()
    {
        var input = new ReturnInput
        {
            SaleId = _saleId,
            Reason = Reason,
            Note = Note,
            Restock = Restock,
            Lines = new List<ReturnLineInput>()
        };

        foreach (var row in Lines)
        {
            if (Qty.TryParse(row.QtyText, out var qty) && qty > 0)
            {
                input.Lines.Add(new ReturnLineInput { SaleItemId = row.SaleItemId, QtyBase = qty });
            }
        }

        return input;
    }

    private void Recalculate()
    {
        if (_saleId <= 0)
        {
            return;
        }

        var input = BuildInput();

        if (input.Lines.Count == 0)
        {
            RefundText = "Rs 0";
            CashText = "Rs 0";
            CreditText = "Rs 0";
            return;
        }

        var quote = _services.Returns.Quote(input, _user);

        if (!quote.Ok || quote.Value is null)
        {
            RefundText = "—";
            CashText = "—";
            CreditText = "—";
            SetStatus(quote.Error ?? "The refund could not be calculated.", true);
            return;
        }

        RefundText = Money.Format(quote.Value.RefundPaisa);
        CashText = Money.Format(quote.Value.CashPaisa);
        CreditText = Money.Format(quote.Value.CreditReducedPaisa);
        SetStatus("Refund calculated from this bill. Press Take the return to save it.", false);
    }

    private void TakeReturn()
    {
        if (_saleId <= 0)
        {
            SetStatus("Find the bill first.", true);
            return;
        }

        var input = BuildInput();
        if (input.Lines.Count == 0)
        {
            SetStatus("Type how many units are coming back.", true);
            return;
        }

        var result = _services.Returns.Create(input, _user);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The return could not be saved.", true);
            return;
        }

        var saved = result.Value;
        var where = saved.QuarantineCodes.Count > 0
            ? " In quarantine: " + string.Join(", ", saved.QuarantineCodes) + "."
            : " Back in stock.";

        var message = $"{saved.Code} saved · refund {Money.Format(saved.RefundPaisa)}" +
                      (saved.CreditReducedPaisa > 0
                          ? $", of which {Money.Format(saved.CreditReducedPaisa)} off the khata"
                          : string.Empty) + "." + where;

        Print(saved.Code);
        LoadQuarantine();

        // Clear the form but keep the message: the cashier must be able to read the RET number.
        BillCode = string.Empty;
        Note = string.Empty;
        Reason = ReturnService.ReasonChangedMind;
        Restock = true;
        ResetBill();
        SetStatus(message, false);
    }

    private void Print(string returnCode)
    {
        if (!_services.Settings.GetBool("printer.autoPrint", true))
        {
            return;
        }

        var document = _services.ReturnReceipts.Build(returnCode);
        if (!document.Ok || document.Value is null)
        {
            SetStatus(document.Error ?? "The return was saved, but its receipt could not be prepared.", true);
            return;
        }

        var printed = _services.Printer.Print(document.Value);
        if (!printed.Ok)
        {
            SetStatus($"{returnCode} is saved. The printer did not answer: {printed.Error}", true);
        }
    }

    // --------------------------------------------------------- quarantine

    public void LoadQuarantine()
    {
        Pending.Clear();
        History.Clear();

        var pending = _services.Quarantine.Pending();
        if (pending.Ok && pending.Value is not null)
        {
            foreach (var item in pending.Value)
            {
                Pending.Add(new QuarantineRow(item, Decide));
            }
        }

        var history = _services.Quarantine.History();
        if (history.Ok && history.Value is not null)
        {
            foreach (var item in history.Value)
            {
                History.Add(item);
            }
        }

        QuarantineCaption = Pending.Count == 0
            ? "No goods are waiting for a decision."
            : $"{Pending.Count} item(s) waiting. Goods stay here (and the alert stays on) until you decide.";
    }

    private void Decide(QuarantineRow row, string outcome)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;

        try
        {
            var result = _services.Quarantine.Dispose(_user, row.ReturnId, outcome, row.NoteText);
            SetStatus(result.Ok
                ? $"{row.QCode}: {row.OutcomeLabel(outcome)}."
                : (result.Error ?? "The decision could not be saved."), !result.Ok);
        }
        finally
        {
            _busy = false;
        }

        LoadQuarantine();
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}

/// <summary>One returnable line of the bill. The quantity box is the only input.</summary>
public sealed class ReturnLineRow : ObservableObject
{
    private readonly Action _changed;
    private string _qtyText = string.Empty;

    public ReturnLineRow(ReturnLineOption line, Action changed)
    {
        _changed = changed;

        SaleItemId = line.SaleItemId;
        Name = line.Name;
        UnitLabel = line.UnitSold;
        SoldLabel = line.SoldLabel;
        ReturnedLabel = line.ReturnedLabel;
        RemainingLabel = line.RemainingLabel;
        MoneyLabel = line.RemainingMoneyLabel;
        MaxUnits = line.RemainingUnits;
        CanReturn = line.CanReturn;
    }

    public int SaleItemId { get; }

    public string Name { get; }

    public string UnitLabel { get; }

    public string SoldLabel { get; }

    public string ReturnedLabel { get; }

    public string RemainingLabel { get; }

    public string MoneyLabel { get; }

    public double MaxUnits { get; }

    public bool CanReturn { get; }

    /// <summary>Quantity coming back, in base units (tablets). Empty = this line is not returned.</summary>
    public string QtyText
    {
        get => _qtyText;
        set
        {
            if (SetProperty(ref _qtyText, value))
            {
                _changed();
            }
        }
    }
}

/// <summary>One item in quarantine, with its note box and the three decisions.</summary>
public sealed class QuarantineRow : ObservableObject
{
    private string _noteText = string.Empty;

    public QuarantineRow(QuarantineItem item, Action<QuarantineRow, string> decide)
    {
        ReturnId = item.ReturnId;
        QCode = string.IsNullOrWhiteSpace(item.QCode) ? "Q-?" : item.QCode!;
        ProductName = string.IsNullOrWhiteSpace(item.ProductName) ? "(unknown item)" : item.ProductName!;
        QuantityLabel = item.QuantityLabel;
        ReasonLabel = string.IsNullOrWhiteSpace(item.Reason) ? "—" : item.Reason!;
        DateLabel = string.IsNullOrWhiteSpace(item.Date) ? string.Empty : item.Date![..Math.Min(16, item.Date.Length)];

        RestockCommand = new RelayCommand(() => decide(this, QuarantineService.Restocked));
        ExpiredCommand = new RelayCommand(() => decide(this, QuarantineService.Expired));
        SoldCommand = new RelayCommand(() => decide(this, QuarantineService.Sold));
    }

    public int ReturnId { get; }

    public string QCode { get; }

    public string ProductName { get; }

    public string QuantityLabel { get; }

    public string ReasonLabel { get; }

    public string DateLabel { get; }

    public RelayCommand RestockCommand { get; }

    public RelayCommand ExpiredCommand { get; }

    public RelayCommand SoldCommand { get; }

    public string NoteText
    {
        get => _noteText;
        set => SetProperty(ref _noteText, value);
    }

    public string OutcomeLabel(string outcome) => outcome switch
    {
        QuarantineService.Restocked => "back in stock",
        QuarantineService.Expired => "destroyed as damaged/expired",
        QuarantineService.Sold => "sold on",
        _ => outcome
    };
}
