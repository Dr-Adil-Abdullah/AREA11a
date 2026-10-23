using System.Collections.ObjectModel;
using System.Windows;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The tax report (F-49, Spec 6.5 / 33.4.1): one month, day by day, exactly as the bills charged it.
///
/// The page has no arithmetic of its own - `TaxService` reads `sales.tax_paisa` - and it says out
/// loud what the number cannot cover: refunds are one amount, and a held or void bill was never a
/// sale. Copying the table is the bridge to the accountant, so the shape of that text is built and
/// tested in the service, not here.
/// </summary>
public sealed class TaxViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(Rows.Count == 0, "Tax report");

    private readonly AppServices _services;
    private readonly User _user;

    private int _year;
    private int _month;

    private string _monthLabel = string.Empty;
    private string _settingLabel = string.Empty;
    private string _billsText = "0";
    private string _netText = "Rs 0";
    private string _taxText = "Rs 0";
    private string _grossText = "Rs 0";
    private string _refundText = "Rs 0";
    private bool _hasRefunds;
    private string _roundOffText = string.Empty;
    private bool _hasRoundOff;
    private string _statusText = string.Empty;
    private bool _isStatusError;

    public TaxViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        var today = DateTime.Now;
        _year = today.Year;
        _month = today.Month;

        PreviousCommand = new RelayCommand(() => Shift(-1));
        NextCommand = new RelayCommand(() => Shift(1));
        ThisMonthCommand = new RelayCommand(GoToThisMonth);
        RefreshCommand = new RelayCommand(Load);
        CopyCommand = new RelayCommand(Copy);

        Load();
    }

    public ObservableCollection<TaxDayRow> Rows { get; } = new();

    public RelayCommand PreviousCommand { get; }

    public RelayCommand NextCommand { get; }

    public RelayCommand ThisMonthCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand CopyCommand { get; }

    public string MonthLabel
    {
        get => _monthLabel;
        private set => SetProperty(ref _monthLabel, value);
    }

    public string SettingLabel
    {
        get => _settingLabel;
        private set => SetProperty(ref _settingLabel, value);
    }

    public string BillsText
    {
        get => _billsText;
        private set => SetProperty(ref _billsText, value);
    }

    public string NetText
    {
        get => _netText;
        private set => SetProperty(ref _netText, value);
    }

    public string TaxText
    {
        get => _taxText;
        private set => SetProperty(ref _taxText, value);
    }

    public string GrossText
    {
        get => _grossText;
        private set => SetProperty(ref _grossText, value);
    }

    public string RefundText
    {
        get => _refundText;
        private set => SetProperty(ref _refundText, value);
    }

    /// <summary>True when the month has a refund, so the red line is shown instead of a quiet zero.</summary>
    public bool HasRefunds
    {
        get => _hasRefunds;
        private set => SetProperty(ref _hasRefunds, value);
    }

    /// <summary>
    /// "Round-off down on the bills (Z-03): Rs -3 ..." - shown so the three numbers on the screen
    /// add up for an accountant: every bill is rounded down to the shop's step, so the total is not
    /// exactly sales-before-tax plus tax.
    /// </summary>
    public string RoundOffText
    {
        get => _roundOffText;
        private set => SetProperty(ref _roundOffText, value);
    }

    public bool HasRoundOff
    {
        get => _hasRoundOff;
        private set => SetProperty(ref _hasRoundOff, value);
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
        Rows.Clear();

        var report = _services.Tax.Report(_user, _year, _month);

        if (!report.Ok || report.Value is null)
        {
            MonthLabel = MonthName(_year, _month);
            BillsText = "0";
            NetText = "Rs 0";
            TaxText = "Rs 0";
            GrossText = "Rs 0";
            RefundText = "Rs 0";
            HasRefunds = false;
            RoundOffText = string.Empty;
            HasRoundOff = false;
            SetStatus(report.Error ?? "The tax report could not be built.", true);
            return;
        }

        var value = report.Value;

        MonthLabel = value.MonthLabel;
        SettingLabel = value.SettingLabel;
        BillsText = value.BillsLabel;
        NetText = value.NetLabel;
        TaxText = value.TaxTotalLabel;
        GrossText = value.GrossLabel;
        RefundText = value.RefundLabel;
        HasRefunds = value.RefundPaisa > 0;
        HasRoundOff = value.HasRoundOff;
        RoundOffText = value.HasRoundOff
            ? "Round-off down on the bills (Z-03): " + value.RoundOffLabel +
              ". Every bill is rounded down to the shop's step, so the total is not exactly " +
              "sales before tax plus tax - the difference is the customer's gain, not a missing rupee."
            : string.Empty;

        foreach (var row in value.Rows)
        {
            Rows.Add(row);
        }

        SetStatus(value.HasRows
            ? value.Bills + " bill(s) in " + value.MonthLabel + ". Copy the table for the accountant."
            : "No bills were made in " + value.MonthLabel + ".", false);
    }

    private void Shift(int months)
    {
        var moved = new DateTime(_year, _month, 1).AddMonths(months);
        _year = moved.Year;
        _month = moved.Month;
        Load();
    }

    private void GoToThisMonth()
    {
        var today = DateTime.Now;
        _year = today.Year;
        _month = today.Month;
        Load();
    }

    private void Copy()
    {
        var report = _services.Tax.Report(_user, _year, _month);

        if (!report.Ok || report.Value is null)
        {
            SetStatus(report.Error ?? "The tax report could not be built.", true);
            return;
        }

        try
        {
            Clipboard.SetText(TaxService.ToText(report.Value));
            SetStatus("The table is on the clipboard - paste it into the accountant's message.", false);
        }
        catch (Exception exception)
        {
            SetStatus("The clipboard did not answer: " + exception.Message, true);
        }
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }

    private static string MonthName(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMMM yyyy");
}
