using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private string _statusMessage = string.Empty;
    private bool _hasAlert;
    private bool _canSeeProfit;

    public DashboardViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;
        RefreshCommand = new RelayCommand(Load);
        Load();
    }

    public RelayCommand RefreshCommand { get; }

    public string StoreName => _services.Settings.GetString("store.name");

    public string TodayText => DateTime.Now.ToString("dddd, dd MMMM yyyy");

    public string BillsText { get; private set; } = "0";

    public string SalesText { get; private set; } = "Rs 0";

    public string ProfitText { get; private set; } = "Rs 0";

    /// <summary>
    /// The profit card is shown only to a person who may see profit. A cashier standing at the
    /// counter can read the drawer; what the shop earns on the day is the owner's business (D-75).
    /// </summary>
    public bool CanSeeProfit
    {
        get => _canSeeProfit;
        private set => SetProperty(ref _canSeeProfit, value);
    }

    public string CreditText { get; private set; } = "Rs 0";

    public string PayableText { get; private set; } = "Rs 0";

    public string ProductsText { get; private set; } = "0";

    public string LowStockText { get; private set; } = "0";

    public string NegativeText { get; private set; } = "0";

    /// <summary>Batches inside the owner's nearest expiry band - the same band Settings changes.</summary>
    public string ExpiringText { get; private set; } = "0";

    /// <summary>Batches already past their date, with stock still on the shelf.</summary>
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
        var result = _services.Dashboard.GetSnapshot(_user);
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
        ExpiringText = snapshot.ExpiringSoon.ToString();
        ExpiryText = snapshot.Expired.ToString();
        CanSeeProfit = snapshot.CanSeeProfit;
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
        OnPropertyChanged(nameof(ExpiringText));
        OnPropertyChanged(nameof(ExpiryText));
        OnPropertyChanged(nameof(ShiftText));
    }
}
