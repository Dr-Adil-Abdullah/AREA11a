using System.Collections.ObjectModel;
using System.Globalization;
using Area11.Core.Models;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The reports page (F-34, Spec 21). Four buttons pick the period; everything else is read-only.
///
/// The page holds no rule of its own: `ReportService` decides what a period means, refuses a cashier
/// and hides the net profit from anyone without the `profit` permission. The view model only turns
/// the answer into text, so a wrong number here can never be caught "in the screen".
/// </summary>
public sealed class ReportsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private ReportRange _range = ReportRange.Today;

    private string _periodLabel = string.Empty;
    private string _statusText = string.Empty;
    private bool _isStatusError;

    private bool _canSeeProfit;
    private string _profitNote = string.Empty;

    private string _billsText = "0";
    private string _itemsText = "0";
    private string _salesText = "Rs 0";
    private string _discountText = "Rs 0";
    private string _averageBillText = "Rs 0";
    private string _cashText = "Rs 0";
    private string _creditText = "Rs 0";
    private string _refundText = "Rs 0";
    private string _expensesText = "Rs 0";
    private string _drawingsText = "Rs 0";
    private string _purchasesText = "Rs 0";
    private string _profitText = "hidden";
    private string _marginText = "hidden";
    private string _netText = "hidden";

    private string _deadStockTitle = "Dead stock";
    private string _deadStockNote = string.Empty;
    private string _bestDayText = "no sales yet";
    private string _worstDayText = "no sales yet";

    public ReportsViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        TodayCommand = new RelayCommand(() => Select(ReportRange.Today));
        WeekCommand = new RelayCommand(() => Select(ReportRange.Last7));
        Month30Command = new RelayCommand(() => Select(ReportRange.Last30));
        MonthCommand = new RelayCommand(() => Select(ReportRange.ThisMonth));
        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<ReportDayRow> Days { get; } = new();

    public ObservableCollection<ReportItemRow> TopByQty { get; } = new();

    public ObservableCollection<ReportItemRow> TopByProfit { get; } = new();

    public ObservableCollection<ReportCategoryRow> Categories { get; } = new();

    public ObservableCollection<ReportDeadRow> DeadStock { get; } = new();

    public RelayCommand TodayCommand { get; }

    public RelayCommand WeekCommand { get; }

    public RelayCommand Month30Command { get; }

    public RelayCommand MonthCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public string PeriodLabel
    {
        get => _periodLabel;
        private set => SetProperty(ref _periodLabel, value);
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

    /// <summary>False for a manager: the profit cards and the profit list hide themselves.</summary>
    public bool CanSeeProfit
    {
        get => _canSeeProfit;
        private set => SetProperty(ref _canSeeProfit, value);
    }

    public string ProfitNote
    {
        get => _profitNote;
        private set => SetProperty(ref _profitNote, value);
    }

    public string BillsText
    {
        get => _billsText;
        private set => SetProperty(ref _billsText, value);
    }

    public string ItemsText
    {
        get => _itemsText;
        private set => SetProperty(ref _itemsText, value);
    }

    public string SalesText
    {
        get => _salesText;
        private set => SetProperty(ref _salesText, value);
    }

    public string DiscountText
    {
        get => _discountText;
        private set => SetProperty(ref _discountText, value);
    }

    public string AverageBillText
    {
        get => _averageBillText;
        private set => SetProperty(ref _averageBillText, value);
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

    public string RefundText
    {
        get => _refundText;
        private set => SetProperty(ref _refundText, value);
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

    public string PurchasesText
    {
        get => _purchasesText;
        private set => SetProperty(ref _purchasesText, value);
    }

    public string ProfitText
    {
        get => _profitText;
        private set => SetProperty(ref _profitText, value);
    }

    public string MarginText
    {
        get => _marginText;
        private set => SetProperty(ref _marginText, value);
    }

    public string NetText
    {
        get => _netText;
        private set => SetProperty(ref _netText, value);
    }

    public string DeadStockTitle
    {
        get => _deadStockTitle;
        private set => SetProperty(ref _deadStockTitle, value);
    }

    public string DeadStockNote
    {
        get => _deadStockNote;
        private set => SetProperty(ref _deadStockNote, value);
    }

    public string BestDayText
    {
        get => _bestDayText;
        private set => SetProperty(ref _bestDayText, value);
    }

    public string WorstDayText
    {
        get => _worstDayText;
        private set => SetProperty(ref _worstDayText, value);
    }

    private void Select(ReportRange range)
    {
        _range = range;
        Load();
    }

    private void Load()
    {
        Days.Clear();
        TopByQty.Clear();
        TopByProfit.Clear();
        Categories.Clear();
        DeadStock.Clear();

        var report = _services.Reports.Build(_range, _user);

        if (!report.Ok)
        {
            IsStatusError = true;
            StatusText = report.Error ?? "The report could not be read.";
            ProfitNote = string.Empty;
            return;
        }

        var value = report.Value!;

        CanSeeProfit = value.CanSeeProfit;
        ProfitNote = value.ProfitNote;
        PeriodLabel = value.PeriodLabel;
        IsStatusError = false;

        StatusText = value.Totals.Bills == 0
            ? "No bills in this period yet."
            : value.Totals.Bills.ToString(CultureInfo.InvariantCulture) + " bill(s) - "
              + value.Totals.SalesText + " sold.";

        BillsText = value.Totals.BillsText;
        ItemsText = value.Totals.ItemsText;
        SalesText = value.Totals.SalesText;
        DiscountText = value.Totals.DiscountText;
        AverageBillText = value.Totals.AverageBillText;
        CashText = value.Totals.CashText;
        CreditText = value.Totals.CreditText;
        RefundText = value.Totals.RefundText;
        ExpensesText = value.Totals.ExpensesText;
        DrawingsText = value.Totals.DrawingsText;
        PurchasesText = value.Totals.PurchasesText;

        ProfitText = value.CanSeeProfit ? value.Totals.ProfitText : "hidden";
        MarginText = value.CanSeeProfit ? value.Totals.MarginText : "hidden";
        NetText = value.CanSeeProfit ? value.Totals.NetText : "hidden";

        foreach (var day in value.Days)
        {
            Days.Add(day);
        }

        foreach (var row in value.TopByQty)
        {
            TopByQty.Add(row);
        }

        foreach (var row in value.TopByProfit)
        {
            TopByProfit.Add(row);
        }

        foreach (var row in value.Categories)
        {
            Categories.Add(row);
        }

        foreach (var row in value.DeadStock)
        {
            DeadStock.Add(row);
        }

        DeadStockTitle = value.DeadStockTitle;
        DeadStockNote = value.DeadStock.Count == 0
            ? "Every item on the shelf has sold in the last "
              + value.DeadStockDays.ToString(CultureInfo.InvariantCulture) + " days."
            : value.DeadStock.Count.ToString(CultureInfo.InvariantCulture)
              + " item(s) with stock but no sale - the money is sleeping on the shelf.";

        BestDayText = value.BestDayLabel;
        WorstDayText = value.WorstDayLabel;
    }
}
