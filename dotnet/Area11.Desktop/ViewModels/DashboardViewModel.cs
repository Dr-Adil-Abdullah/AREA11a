using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _statusMessage = string.Empty;
    private bool _hasAlert;

    public DashboardViewModel(AppServices services)
    {
        _services = services;
        RefreshCommand = new RelayCommand(Load);
        Load();
    }

    public RelayCommand RefreshCommand { get; }

    public string StoreName => _services.Settings.GetString("store.name");

    public string TodayText => DateTime.Now.ToString("dddd, dd MMMM yyyy");

    public string BillsText { get; private set; } = "0";

    public string SalesText { get; private set; } = "Rs 0";

    public string ProfitText { get; private set; } = "Rs 0";

    public string CreditText { get; private set; } = "Rs 0";

    public string PayableText { get; private set; } = "Rs 0";

    public string ProductsText { get; private set; } = "0";

    public string LowStockText { get; private set; } = "0";

    public string NegativeText { get; private set; } = "0";

    public string ExpiryText { get; private set; } = "0";

    public string ShiftText { get; private set; } = "No shift open";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasAlert
    {
        get => _hasAlert;
        private set => SetProperty(ref _hasAlert, value);
    }

    private void Load()
    {
        var result = _services.Dashboard.GetSnapshot();
        if (!result.Ok || result.Value is null)
        {
            StatusMessage = result.Error ?? "The dashboard could not be loaded.";
            return;
        }

        var snapshot = result.Value;

        BillsText = snapshot.TodayBills.ToString();
        SalesText = Money.Format(snapshot.TodaySalesPaisa);
        ProfitText = Money.Format(snapshot.TodayProfitPaisa);
        CreditText = Money.Format(snapshot.CustomerCreditPaisa);
        PayableText = Money.Format(snapshot.SupplierPayablePaisa);
        ProductsText = snapshot.Products.ToString();
        LowStockText = snapshot.LowStock.ToString();
        NegativeText = snapshot.NegativeStock.ToString();
        ExpiryText = snapshot.Expired.ToString();
        ShiftText = snapshot.ShiftOpen ? "Shift is open" : "No shift open";
        StatusMessage = snapshot.NegativeStock > 0
            ? $"Stock is MINUS on {snapshot.NegativeStock} medicine(s). Sales continue; fix by count or purchase."
            : string.Empty;
        HasAlert = snapshot.NegativeStock > 0;

        OnPropertyChanged(nameof(BillsText));
        OnPropertyChanged(nameof(SalesText));
        OnPropertyChanged(nameof(ProfitText));
        OnPropertyChanged(nameof(CreditText));
        OnPropertyChanged(nameof(PayableText));
        OnPropertyChanged(nameof(ProductsText));
        OnPropertyChanged(nameof(LowStockText));
        OnPropertyChanged(nameof(NegativeText));
        OnPropertyChanged(nameof(ExpiryText));
        OnPropertyChanged(nameof(ShiftText));
    }
}
