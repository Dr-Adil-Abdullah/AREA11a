using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The sales history (F-52, Spec 12.3): find a bill, open it, print it again.
///
/// No rule lives here. <see cref="Area11.Core.Services.SaleHistoryService"/> decides what may be seen,
/// builds the paper with the same <c>ReceiptService</c> that printed the original, and writes the
/// reprint into the black box; this class carries the list, the filters, the preview and the buttons.
///
/// Why the preview is on this page: paper costs money and a wrong paper width loses the last column.
/// The owner sees the exact text that will print - at 32 or 48 characters - before he spends a slip.
/// </summary>
public sealed class SaleHistoryViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private SaleHistoryRow? _selected;
    private SaleHistoryReturn? _selectedReturn;
    private string _searchText = string.Empty;
    private string _fromText = string.Empty;
    private string _toText = string.Empty;
    private string _selectedMethod = AllMethods;
    private string _countText = "0 bills";
    private string _totalText = "Rs 0";
    private string _dueText = "Rs 0";
    private string _selectedTitle = "Open a bill to see its lines.";
    private string _previewText = string.Empty;
    private bool _hasSelection;
    private bool _canPrint;
    private string _subtotalLabel = "-";
    private string _discountLabel = "-";
    private string _taxLabel = "-";
    private string _roundOffLabel = "-";
    private string _totalLabel = "-";
    private string _paidLabel = "-";
    private string _dueLabel = "-";
    private bool _hasDiscount;
    private bool _hasTax;
    private bool _hasRoundOff;
    private bool _hasReturns;
    private bool _hasPayments;
    private string _statusText = "Pick a range and press Search. Every bill in it can be printed again.";
    private bool _isStatusError;

    private const string AllMethods = "All methods";

    public SaleHistoryViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        // The range the owner almost always wants: the last month, ending today.
        _fromText = Clock.DateDaysAgo(30);
        _toText = Clock.Today();

        RefreshCommand = new RelayCommand(Load);
        TodayCommand = new RelayCommand(() => SetRange(0));
        WeekCommand = new RelayCommand(() => SetRange(6));
        MonthCommand = new RelayCommand(() => SetRange(30));
        ClearCommand = new RelayCommand(Clear);
        PrintCommand = new RelayCommand(PrintAgain);
        PrintReturnCommand = new RelayCommand(PrintSelectedReturn);

        Load();
    }

    public ObservableCollection<SaleHistoryRow> Rows { get; } = new();

    public ObservableCollection<SaleHistoryLine> Lines { get; } = new();

    public ObservableCollection<SaleHistoryPayment> Payments { get; } = new();

    public ObservableCollection<SaleHistoryReturn> Returns { get; } = new();

    /// <summary>What the method drop-down offers; the first entry means "do not filter".</summary>
    public IReadOnlyList<string> Methods { get; } = new[]
    {
        AllMethods, "Cash", "Khata", "Online", "Card", "Split"
    };

    public RelayCommand RefreshCommand { get; }

    public RelayCommand TodayCommand { get; }

    public RelayCommand WeekCommand { get; }

    public RelayCommand MonthCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand PrintCommand { get; }

    /// <summary>Prints the return receipt of the row picked in the returns list.</summary>
    public RelayCommand PrintReturnCommand { get; }

    // ------------------------------ the filters ------------------------------

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string FromText
    {
        get => _fromText;
        set => SetProperty(ref _fromText, value);
    }

    public string ToText
    {
        get => _toText;
        set => SetProperty(ref _toText, value);
    }

    public string SelectedMethod
    {
        get => _selectedMethod;
        set => SetProperty(ref _selectedMethod, value);
    }

    // ------------------------------ the list ------------------------------

    public SaleHistoryRow? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value))
            {
                return;
            }

            HasSelection = value is not null;
            CanPrint = value is not null;
            Open();
        }
    }

    public bool CanPrint
    {
        get => _canPrint;
        private set => SetProperty(ref _canPrint, value);
    }

    /// <summary>The return picked in the bill's return list; null when the bill has none.</summary>
    public SaleHistoryReturn? SelectedReturn
    {
        get => _selectedReturn;
        set
        {
            if (SetProperty(ref _selectedReturn, value))
            {
                OnPropertyChanged(nameof(CanPrintReturn));
            }
        }
    }

    public bool CanPrintReturn => SelectedReturn is { CanPrint: true };

    public string CountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
    }

    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    public string DueText
    {
        get => _dueText;
        private set => SetProperty(ref _dueText, value);
    }

    // ------------------------------ the opened bill ------------------------------

    public bool HasSelection
    {
        get => _hasSelection;
        private set => SetProperty(ref _hasSelection, value);
    }

    public string SelectedTitle
    {
        get => _selectedTitle;
        private set => SetProperty(ref _selectedTitle, value);
    }

    public string PreviewText
    {
        get => _previewText;
        private set => SetProperty(ref _previewText, value);
    }

    public string SubtotalLabel
    {
        get => _subtotalLabel;
        private set => SetProperty(ref _subtotalLabel, value);
    }

    public string DiscountLabel
    {
        get => _discountLabel;
        private set => SetProperty(ref _discountLabel, value);
    }

    public string TaxLabel
    {
        get => _taxLabel;
        private set => SetProperty(ref _taxLabel, value);
    }

    public string RoundOffLabel
    {
        get => _roundOffLabel;
        private set => SetProperty(ref _roundOffLabel, value);
    }

    public string TotalLabel
    {
        get => _totalLabel;
        private set => SetProperty(ref _totalLabel, value);
    }

    public string PaidLabel
    {
        get => _paidLabel;
        private set => SetProperty(ref _paidLabel, value);
    }

    public string DueLabel
    {
        get => _dueLabel;
        private set => SetProperty(ref _dueLabel, value);
    }

    public bool HasDiscount
    {
        get => _hasDiscount;
        private set => SetProperty(ref _hasDiscount, value);
    }

    public bool HasTax
    {
        get => _hasTax;
        private set => SetProperty(ref _hasTax, value);
    }

    public bool HasRoundOff
    {
        get => _hasRoundOff;
        private set => SetProperty(ref _hasRoundOff, value);
    }

    public bool HasReturns
    {
        get => _hasReturns;
        private set => SetProperty(ref _hasReturns, value);
    }

    public bool HasPayments
    {
        get => _hasPayments;
        private set => SetProperty(ref _hasPayments, value);
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

    // ============================== the commands ==============================

    public void Load()
    {
        var page = _services.History.Search(_user, new SaleHistoryFilter
        {
            Search = SearchText,
            From = FromText,
            To = ToText,
            Method = MethodValue()
        });

        if (!page.Ok || page.Value is null)
        {
            SetStatus(page.Error ?? "The bill history could not be read.", true);
            return;
        }

        var value = page.Value;

        Rows.Clear();

        foreach (var row in value.Rows)
        {
            Rows.Add(row);
        }

        CountText = value.CountLabel;
        TotalText = value.TotalLabel;
        DueText = value.DueLabel;

        Selected = null;
        ClearDetail();

        if (value.FilterProblem is { Length: > 0 } problem)
        {
            SetStatus(problem, true);
            return;
        }

        SetStatus(
            value.IsEmpty
                ? "No bills in this range."
                : "Pick a bill on the left. The paper is shown here before it prints, and nothing is changed by looking.",
            false);
    }

    private void Open()
    {
        if (Selected is null)
        {
            ClearDetail();
            return;
        }

        var opened = _services.History.Open(_user, Selected.Id);

        if (!opened.Ok || opened.Value is null)
        {
            ClearDetail();
            SetStatus(opened.Error ?? "That bill could not be opened.", true);
            return;
        }

        var detail = opened.Value;

        Lines.Clear();
        Payments.Clear();
        Returns.Clear();

        foreach (var line in detail.Lines)
        {
            Lines.Add(line);
        }

        foreach (var payment in detail.Payments)
        {
            Payments.Add(payment);
        }

        foreach (var item in detail.Returns)
        {
            Returns.Add(item);
        }

        SelectedReturn = Returns.FirstOrDefault(row => row.CanPrint);
        OnPropertyChanged(nameof(CanPrintReturn));

        SelectedTitle = detail.TitleLabel;
        SubtotalLabel = detail.SubtotalLabel;
        DiscountLabel = detail.DiscountLabel;
        TaxLabel = detail.TaxLabel;
        RoundOffLabel = detail.RoundOffLabel;
        TotalLabel = detail.TotalLabel;
        PaidLabel = detail.PaidLabel;
        DueLabel = detail.DueLabel;
        HasDiscount = detail.HasDiscount;
        HasTax = detail.HasTax;
        HasRoundOff = detail.HasRoundOff;
        HasReturns = detail.HasReturns;
        HasPayments = detail.HasPayments;

        PreviewText = BuildPreview();
    }

    private string BuildPreview()
    {
        if (Selected is null)
        {
            return string.Empty;
        }

        var document = _services.History.BuildReceipt(_user, Selected.Id);

        return document.Ok && document.Value is not null
            ? document.Value.PlainText
            : document.Error ?? "The paper could not be prepared.";
    }

    private void PrintAgain()
    {
        if (Selected is null)
        {
            SetStatus("Pick a bill first.", true);
            return;
        }

        var document = _services.History.BuildReceipt(_user, Selected.Id);

        if (!document.Ok || document.Value is null)
        {
            SetStatus(document.Error ?? "The bill could not be prepared.", true);
            return;
        }

        var printed = _services.Printer.Print(document.Value);

        if (!printed.Ok)
        {
            SetStatus(printed.Error ?? "The printer did not answer.", true);
            return;
        }

        // The paper is out, so the black box may say so. If that line cannot be written the owner is told,
        // because an unrecorded reprint is exactly the copy nobody can trace.
        var noted = _services.History.RecordReprint(_user, Selected.Id);

        SetStatus(
            "Printed " + Selected.Code + " again (" + Selected.TotalLabel + ")."
            + (noted.Ok ? string.Empty : " The black box did not record it: " + noted.Error),
            !noted.Ok);
    }

    private void PrintSelectedReturn()
    {
        var row = SelectedReturn;

        if (row is null || !row.CanPrint)
        {
            SetStatus("That return has no number, so there is no receipt to print.", true);
            return;
        }

        var document = _services.History.BuildReturnReceipt(_user, row.Code);

        if (!document.Ok || document.Value is null)
        {
            SetStatus(document.Error ?? "The return receipt could not be prepared.", true);
            return;
        }

        var printed = _services.Printer.Print(document.Value);

        if (!printed.Ok)
        {
            SetStatus(printed.Error ?? "The printer did not answer.", true);
            return;
        }

        var noted = _services.History.RecordReturnReprint(_user, row.Code);

        SetStatus(
            "Printed the return receipt " + row.Code + " again."
            + (noted.Ok ? string.Empty : " The black box did not record it: " + noted.Error),
            !noted.Ok);
    }

    private void Clear()
    {
        SearchText = string.Empty;
        SelectedMethod = AllMethods;
        FromText = Clock.DateDaysAgo(30);
        ToText = Clock.Today();
        Load();
    }

    private void SetRange(int daysBack)
    {
        FromText = Clock.DateDaysAgo(daysBack);
        ToText = Clock.Today();
        Load();
    }

    /// <summary>"All methods" -> "" so the SQL does not filter; the rest travels as the stored word.</summary>
    private string MethodValue() => SelectedMethod switch
    {
        "Cash" => "cash",
        "Khata" => "credit",
        "Online" => "online",
        "Card" => "card",
        "Split" => "split",
        _ => string.Empty
    };

    private void ClearDetail()
    {
        Lines.Clear();
        Payments.Clear();
        Returns.Clear();
        PreviewText = string.Empty;
        SelectedTitle = "Open a bill to see its lines.";
        SubtotalLabel = "-";
        DiscountLabel = "-";
        TaxLabel = "-";
        RoundOffLabel = "-";
        TotalLabel = "-";
        PaidLabel = "-";
        DueLabel = "-";
        HasDiscount = false;
        HasTax = false;
        HasRoundOff = false;
        HasReturns = false;
        HasPayments = false;
        CanPrint = false;
        SelectedReturn = null;
        OnPropertyChanged(nameof(CanPrintReturn));
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
