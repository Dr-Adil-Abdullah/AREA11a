using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Shift / galla (F-31, Spec 14): open the drawer with a float, work the day, count the cash and
/// close it.
///
/// The screen never computes the expected cash - ShiftService rebuilds it from the history and
/// hands it over. All this page does is show the six lines, take the counted number and repeat the
/// service's verdict: exact, over, or short.
/// </summary>
public sealed class ShiftViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(History.Count == 0, "Shift (galla)");

    private readonly AppServices _services;
    private readonly User _user;

    private bool _hasShift;
    private string _headerText = "The drawer is closed.";
    private string _previousClosingText = string.Empty;
    private string _floatText = "Rs 0";
    private string _cashSalesText = "Rs 0";
    private string _collectionsText = "Rs 0";
    private string _supplierInText = "Rs 0";
    private string _supplierOutText = "Rs 0";
    private string _refundsText = "Rs 0";
    private string _expensesText = "Rs 0";
    private string _drawingsText = "Rs 0";
    private string _expectedText = "Rs 0";
    private string _openFloatText = string.Empty;
    private string _actualText = string.Empty;
    private string _noteText = string.Empty;
    private string _handoverNote = string.Empty;
    private string _statusText = "Open the drawer with the cash you start with.";
    private bool _isStatusError;
    private User? _selectedPerson;
    private string _peopleSummaryText = string.Empty;
    private string _handoverCashText = string.Empty;
    private ShiftHandover? _lastHandover;

    public ShiftViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        OpenCommand = new RelayCommand(OpenShift);
        CloseCommand = new RelayCommand(CloseShift);
        HandoverCommand = new RelayCommand(HandOver);
        PrintSlipCommand = new RelayCommand(PrintSlip);
        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<ShiftMoney> History { get; } = new();

    /// <summary>Everybody who could take the drawer - the signed-in person is not in this list.</summary>
    public ObservableCollection<User> NextPeople { get; } = new();

    /// <summary>Who counted a drawer in the last 30 days and how far each one landed (F-56).</summary>
    public ObservableCollection<ShiftPersonSummary> PeopleSummary { get; } = new();

    public RelayCommand OpenCommand { get; }

    public RelayCommand CloseCommand { get; }

    /// <summary>Count the drawer, choose the next person, and print the slip (F-55).</summary>
    public RelayCommand HandoverCommand { get; }

    /// <summary>Print the last handover slip again - the paper gets lost, the record does not.</summary>
    public RelayCommand PrintSlipCommand { get; }

    public RelayCommand RefreshCommand { get; }

    /// <summary>The till is money: the page needs the Cash / shifts permission (owner always has it).</summary>
    public bool CanWork => _services.Permissions.Can(_user, Modules.Cash);

    public bool IsBlocked => !CanWork;

    /// <summary>
    /// The note a forgotten session left behind (v0.35.0, D-92), or empty. Shown above the drawer
    /// numbers so the person who must count it cannot miss it - and cleared the moment a drawer is
    /// opened, closed or handed over, because then a person has counted the cash.
    /// </summary>
    public string HandoverNote
    {
        get => _handoverNote;
        private set => SetProperty(ref _handoverNote, value);
    }

    public bool HasHandoverNote => _handoverNote.Length > 0;

    public bool HasShift
    {
        get => _hasShift;
        private set
        {
            if (SetProperty(ref _hasShift, value))
            {
                OnPropertyChanged(nameof(NoShift));
                OnPropertyChanged(nameof(CanHandover));
            }
        }
    }

    public bool NoShift => !_hasShift;

    /// <summary>The handover card is only useful while a drawer is open.</summary>
    public bool CanHandover => _hasShift && CanWork;

    public User? SelectedPerson
    {
        get => _selectedPerson;
        set => SetProperty(ref _selectedPerson, value);
    }

    /// <summary>What the first person counted - the same number the next shift starts with.</summary>
    public string HandoverCashText
    {
        get => _handoverCashText;
        set => SetProperty(ref _handoverCashText, value);
    }

    public bool HasLastHandover => _lastHandover is not null;

    /// <summary>
    /// "Who was short" is a management question: the owner and a manager see it, a cashier does not,
    /// even when the cashier is allowed to open the till. The service enforces the same rule, so a
    /// hidden card and a refused call can never disagree.
    /// </summary>
    public bool CanSeePeople => _user.CanManage;

    /// <summary>The sentence under the table: how many shifts, how many people, and what it found.</summary>
    public string PeopleSummaryText
    {
        get => _peopleSummaryText;
        private set => SetProperty(ref _peopleSummaryText, value);
    }

    public string PreviousClosingText
    {
        get => _previousClosingText;
        private set => SetProperty(ref _previousClosingText, value);
    }

    public string HeaderText
    {
        get => _headerText;
        private set => SetProperty(ref _headerText, value);
    }

    public string FloatText
    {
        get => _floatText;
        private set => SetProperty(ref _floatText, value);
    }

    public string CashSalesText
    {
        get => _cashSalesText;
        private set => SetProperty(ref _cashSalesText, value);
    }

    public string CollectionsText
    {
        get => _collectionsText;
        private set => SetProperty(ref _collectionsText, value);
    }

    public string SupplierInText
    {
        get => _supplierInText;
        private set => SetProperty(ref _supplierInText, value);
    }

    public string SupplierOutText
    {
        get => _supplierOutText;
        private set => SetProperty(ref _supplierOutText, value);
    }

    public string RefundsText
    {
        get => _refundsText;
        private set => SetProperty(ref _refundsText, value);
    }

    public string ExpensesText
    {
        get => _expensesText;
        private set => SetProperty(ref _expensesText, value);
    }

    public string DrawingsText
    {
        get => _drawingsText;
        private set => SetProperty(ref _drawingsText, value);
    }

    public string ExpectedText
    {
        get => _expectedText;
        private set => SetProperty(ref _expectedText, value);
    }

    public string OpenFloatText
    {
        get => _openFloatText;
        set => SetProperty(ref _openFloatText, value);
    }

    public string ActualText
    {
        get => _actualText;
        set => SetProperty(ref _actualText, value);
    }

    public string NoteText
    {
        get => _noteText;
        set => SetProperty(ref _noteText, value);
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

    public void Load()
    {
        HandoverNote = SessionHandover.Read(_services.Settings);
        OnPropertyChanged(nameof(HasHandoverNote));

        var current = _services.Shifts.Current();

        HasShift = current is not null;

        if (current is null)
        {
            HeaderText = "The drawer is closed. Count the cash you are starting with and open it.";
            FloatText = "Rs 0";
            CashSalesText = "Rs 0";
            CollectionsText = "Rs 0";
            SupplierInText = "Rs 0";
            SupplierOutText = "Rs 0";
            RefundsText = "Rs 0";
            ExpensesText = "Rs 0";
            DrawingsText = "Rs 0";
            ExpectedText = "Rs 0";

            var previous = _services.Shifts.LastCounted();

            if (previous?.ActualPaisa is long counted)
            {
                OpenFloatText = Money.Edit(counted);
                PreviousClosingText = $"Previous counted closing: {Money.Format(counted)}. " +
                                      "It is filled in below; changing it needs a reason.";
            }
            else
            {
                PreviousClosingText = "No previous counted closing yet. Type the actual opening cash.";
            }
        }
        else
        {
            PreviousClosingText = string.Empty;
            HeaderText = current.HeaderLabel;
            FloatText = current.FloatLabel;
            CashSalesText = current.CashSalesLabel;
            CollectionsText = current.CollectionsLabel;
            SupplierInText = current.SupplierInLabel;
            SupplierOutText = current.SupplierOutLabel;
            RefundsText = current.RefundsLabel;
            ExpensesText = current.ExpensesLabel;
            DrawingsText = current.DrawingsLabel;
            ExpectedText = current.ExpectedLabel;
        }

        LoadPeople();
        LoadPeopleSummary();

        History.Clear();

        foreach (var row in _services.Shifts.History())
        {
            History.Add(row);
        }
    }

    /// <summary>
    /// The next person must be on the staff list, switched on, and somebody else. The service
    /// refuses an inactive account or one without the cash permission; this list only shows the
    /// people who could actually take the drawer, so the page cannot offer a choice that fails.
    /// </summary>
    private void LoadPeople()
    {
        var keeper = SelectedPerson?.Id;
        var people = _services.Auth.ListUsers();

        NextPeople.Clear();

        if (people.Ok && people.Value is not null)
        {
            foreach (var person in people.Value)
            {
                if (person.Active && person.Id != _user.Id)
                {
                    NextPeople.Add(person);
                }
            }
        }

        SelectedPerson = NextPeople.FirstOrDefault(person => person.Id == keeper) ?? NextPeople.FirstOrDefault();
    }

    /// <summary>
    /// Reads the per-person report (F-56). The page stays usable when the answer is a refusal: the
    /// list is emptied and the sentence says who may look, because an empty table with no reason
    /// reads like "nobody was ever short".
    /// </summary>
    private void LoadPeopleSummary()
    {
        PeopleSummary.Clear();

        if (!CanSeePeople)
        {
            PeopleSummaryText = "Only the owner or a manager can see who was short on the drawer.";
            return;
        }

        var result = _services.Shifts.ByPerson(_user, 30);

        if (!result.Ok || result.Value is null)
        {
            PeopleSummaryText = result.Error ?? "The shift report could not be read.";
            return;
        }

        var report = result.Value;

        foreach (var person in report.People)
        {
            PeopleSummary.Add(person);
        }

        PeopleSummaryText = report.Sentence;
    }

    private void HandOver()
    {
        if (SelectedPerson is null)
        {
            SetStatus("Choose who is taking the drawer.", true);
            return;
        }

        if (!Money.TryParse(HandoverCashText, out var rupees) || rupees < 0)
        {
            SetStatus("Count the drawer and type the total cash, for example 12450.", true);
            return;
        }

        var result = _services.Shifts.Handover(_user, SelectedPerson, Money.ToPaisa(rupees), NoteText);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The shift could not be handed over.", true);
            return;
        }

        var handover = result.Value;

        _lastHandover = handover;
        OnPropertyChanged(nameof(HasLastHandover));
        HandoverCashText = string.Empty;
        NoteText = string.Empty;
        SessionHandover.Clear(_services.Settings);
        Load();

        // The money has already moved, so a printer that is off must not look like a failed
        // handover: the sentence says both things separately.
        var printed = _services.Printer.Print(_services.HandoverSlips.Build(handover));

        SetStatus(printed.Ok
                ? handover.Sentence + " The slip has gone to the printer - both signatures go on it."
                : handover.Sentence + " The slip could not be printed (" + printed.Error
                  + "). The handover is recorded; use Print the slip again.",
            !printed.Ok || !handover.IsExact);
    }

    private void PrintSlip()
    {
        if (_lastHandover is null)
        {
            SetStatus("Hand the drawer over first - there is no slip to print yet.", true);
            return;
        }

        var printed = _services.Printer.Print(_services.HandoverSlips.Build(_lastHandover));

        SetStatus(printed.Ok
                ? "The handover slip has gone to the printer again."
                : "The slip could not be printed: " + printed.Error,
            !printed.Ok);
    }

    private void OpenShift()
    {
        if (!Money.TryParse(OpenFloatText, out var rupees) || rupees < 0)
        {
            SetStatus("Type the cash you are starting with, for example 5000.", true);
            return;
        }

        var result = _services.Shifts.Open(_user, Money.ToPaisa(rupees), NoteText);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The shift could not be opened.", true);
            return;
        }

        OpenFloatText = string.Empty;
        NoteText = string.Empty;
        SessionHandover.Clear(_services.Settings);
        Load();

        SetStatus("Shift opened with " + result.Value.FloatLabel + ".", false);
    }

    private void CloseShift()
    {
        if (!Money.TryParse(ActualText, out var rupees) || rupees < 0)
        {
            SetStatus("Count the drawer and type the total cash, for example 12450.", true);
            return;
        }

        var result = _services.Shifts.Close(_user, Money.ToPaisa(rupees), NoteText);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The shift could not be closed.", true);
            return;
        }

        var closed = result.Value;
        ActualText = string.Empty;
        NoteText = string.Empty;
        SessionHandover.Clear(_services.Settings);
        Load();

        SetStatus("Shift closed. Expected " + closed.ExpectedLabel + ", counted " + closed.ActualLabel
                  + " - " + closed.DifferenceLabel + ".", !closed.IsExact);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}
