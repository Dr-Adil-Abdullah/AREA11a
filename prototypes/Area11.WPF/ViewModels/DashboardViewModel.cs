using Area11.WPF.Services;
using Area11.WPF.Utils;
using Area11.WPF.ViewModels.Base;

namespace Area11.WPF.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly ReportService _reports;
    private string _todaySales = "Rs 0.00";
    private string _todayBills = "0";
    private string _products = "0";
    private string _lowStock = "0";
    private string _expiring = "0";
    private string _stockValue = "Rs 0.00";
    private string _lastRefreshed = string.Empty;

    public DashboardViewModel(AppServices services)
    {
        _reports = services.Reports;
        StoreName = services.Settings.Get("store.name");
        RefreshCommand = new RelayCommand(Load);
        Load();
    }

    public string StoreName { get; }

    public string TodaySales
    {
        get => _todaySales;
        private set => SetProperty(ref _todaySales, value);
    }

    public string TodayBills
    {
        get => _todayBills;
        private set => SetProperty(ref _todayBills, value);
    }

    public string Products
    {
        get => _products;
        private set => SetProperty(ref _products, value);
    }

    public string LowStock
    {
        get => _lowStock;
        private set => SetProperty(ref _lowStock, value);
    }

    public string Expiring
    {
        get => _expiring;
        private set => SetProperty(ref _expiring, value);
    }

    public string StockValue
    {
        get => _stockValue;
        private set => SetProperty(ref _stockValue, value);
    }

    public string LastRefreshed
    {
        get => _lastRefreshed;
        private set => SetProperty(ref _lastRefreshed, value);
    }

    public RelayCommand RefreshCommand { get; }

    private void Load()
    {
        var snapshot = _reports.GetDashboard();
        TodaySales = FormatHelper.Rupees(snapshot.TodaySalesPaisa);
        TodayBills = snapshot.TodayBills.ToString();
        Products = snapshot.ActiveProducts.ToString();
        LowStock = snapshot.LowStockProducts.ToString();
        Expiring = snapshot.ExpiringBatches.ToString();
        StockValue = FormatHelper.Rupees(snapshot.StockValuePaisa);
        LastRefreshed = DateTime.Now.ToString("hh:mm tt");
    }
}
