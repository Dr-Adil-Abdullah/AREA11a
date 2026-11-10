using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

public enum DashboardLink
{
    Bills,
    Sales,
    Profit,
    Credit,
    Products,
    LowStock,
    StockMinus,
    Expiry,
    Payable
}

public sealed class DashboardViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89).
    ///
    /// The dashboard is "empty" only for a shop that has not started: no medicine on the shelf and
    /// no bill today. A shop that has medicines but a quiet morning still has something to show
    /// (stock, khata, expiry), and calling that empty would be wrong.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(_isEmptyShop, "Dashboard");

    private readonly AppServices _services;
    private readonly User _user;
    private readonly Action<DashboardLink>? _navigate;
    private string _statusMessage = string.Empty;
    private bool _hasAlert;
    private bool _canSeeProfit;
    private bool _isEmptyShop;

    public DashboardViewModel(AppServices services, User user, Action<DashboardLink>? navigate = null)
    {
        _services = services;
        _user = user;
        _navigate = navigate;
        RefreshCommand = new RelayCommand(Load);
        OpenBillsCommand = new RelayCommand(() => Open(DashboardLink.Bills));
        OpenSalesCommand = new RelayCommand(() => Open(DashboardLink.Sales));
        OpenProfitCommand = new RelayCommand(() => Open(DashboardLink.Profit));
        OpenCreditCommand = new RelayCommand(() => Open(DashboardLink.Credit));
        OpenProductsCommand = new RelayCommand(() => Open(DashboardLink.Products));
        OpenLowStockCommand = new RelayCommand(() => Open(DashboardLink.LowStock));
        OpenStockMinusCommand = new RelayCommand(() => Open(DashboardLink.StockMinus));
        OpenExpiryCommand = new RelayCommand(() => Open(DashboardLink.Expiry));
        OpenPayableCommand = new RelayCommand(() => Open(DashboardLink.Payable));
        Load();
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand OpenBillsCommand { get; }
    public RelayCommand OpenSalesCommand { get; }
    public RelayCommand OpenProfitCommand { get; }
    public RelayCommand OpenCreditCommand { get; }
    public RelayCommand OpenProductsCommand { get; }
    public RelayCommand OpenLowStockCommand { get; }
    public RelayCommand OpenStockMinusCommand { get; }
    public RelayCommand OpenExpiryCommand { get; }
    public RelayCommand OpenPayableCommand { get; }

    private void Open(DashboardLink link) => _navigate?.Invoke(link);

    public string StoreName => _services.Settings.GetString("store.name");

    public string TodayText => TimeSource.Now.ToString("dddd, dd MMMM yyyy");

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

        // A brand-new shop: nothing on the shelf and nothing billed. Set here, where the numbers
        // arrive, so the card cannot claim a shop is new after a single sale (D-89).
        _isEmptyShop = snapshot.Products == 0 && snapshot.TodayBills == 0;
        OnPropertyChanged(nameof(EmptyHint));

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
            ? $"Stock is MINUS on {snapshot.NegativeStock} medicine(s). New decrements are blocked; reconcile by physical count or receive stock."
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
