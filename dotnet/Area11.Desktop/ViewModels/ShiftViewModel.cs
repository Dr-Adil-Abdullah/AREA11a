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
public sealed class ShiftViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private bool _hasShift;
    private string _headerText = "The drawer is closed.";
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
    private string _statusText = "Open the drawer with the cash you start with.";
    private bool _isStatusError;

    public ShiftViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        OpenCommand = new RelayCommand(OpenShift);
        CloseCommand = new RelayCommand(CloseShift);
        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<ShiftMoney> History { get; } = new();

    public RelayCommand OpenCommand { get; }

    public RelayCommand CloseCommand { get; }

    public RelayCommand RefreshCommand { get; }

    /// <summary>The till is money: the page needs the Cash / shifts permission (owner always has it).</summary>
    public bool CanWork => _services.Permissions.Can(_user, Modules.Cash);

    public bool IsBlocked => !CanWork;

    public bool HasShift
    {
        get => _hasShift;
        private set
        {
            if (SetProperty(ref _hasShift, value))
            {
                OnPropertyChanged(nameof(NoShift));
            }
        }
    }

    public bool NoShift => !_hasShift;

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
        }
        else
        {
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

        History.Clear();

        foreach (var row in _services.Shifts.History())
        {
            History.Add(row);
        }
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
        Load();

        SetStatus("Shift closed. Expected " + closed.ExpectedLabel + ", counted " + closed.ActualLabel
                  + " - " + closed.DifferenceLabel + ".", closed.IsShort);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}
