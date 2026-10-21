using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The window frame: menu on the left, current page on the right. The menu is built from the
/// permissions of the signed-in person, but hiding a menu is never the security — every service
/// checks the permission again (Spec 7.2.5).
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private NavItem? _selectedNav;
    private ObservableObject? _currentViewModel;
    private string _storeName = string.Empty;
    private string _appName = string.Empty;
    private SettingsViewModel? _settingsPage;
    private string _alertText = string.Empty;
    private bool _alertHiddenForSession;
    private bool _hasAlertBar;

    public ShellViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        StoreName = services.Settings.GetString("store.name");
        AppName = services.Settings.GetString("brand.appName");

        LogoutCommand = new RelayCommand(Logout);
        HideAlertBarCommand = new RelayCommand(HideAlertBar);
        OpenAlertsCommand = new RelayCommand(OpenAlerts);

        BuildNavigation();
        RefreshAlerts();
        SelectedNav = NavItems.FirstOrDefault();
    }

    public event Action? LoggedOut;

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public RelayCommand LogoutCommand { get; }

    public RelayCommand HideAlertBarCommand { get; }

    public RelayCommand OpenAlertsCommand { get; }

    /// <summary>The sentence in the red bar, or empty when nothing needs attention.</summary>
    public string AlertText
    {
        get => _alertText;
        private set => SetProperty(ref _alertText, value);
    }

    public bool HasAlertBar
    {
        get => _hasAlertBar;
        private set => SetProperty(ref _hasAlertBar, value);
    }

    public string StoreName
    {
        get => _storeName;
        private set => SetProperty(ref _storeName, value);
    }

    public string AppName
    {
        get => _appName;
        private set => SetProperty(ref _appName, value);
    }

    public string UserName => _user.Name;

    public string RoleText => _user.RoleLabel;

    public string Initials => string.IsNullOrWhiteSpace(_user.Name)
        ? "A"
        : _user.Name.Trim()[..1].ToUpperInvariant();

    public NavItem? SelectedNav
    {
        get => _selectedNav;
        set
        {
            if (!SetProperty(ref _selectedNav, value))
            {
                return;
            }

            CurrentViewModel = value?.Resolve();

            // Move around the app and the bar refreshes itself; no timer, no background thread.
            RefreshAlerts();
        }
    }

    public ObservableObject? CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    private void BuildNavigation()
    {
        NavItems.Add(new NavItem("Dashboard", "Today at a glance", () => new DashboardViewModel(_services)));
        NavItems.Add(new NavItem("Alerts", "Minus stock, quarantine",
            () => new AlertsViewModel(_services, _user, RefreshAlerts)));

        if (_services.Permissions.Can(_user, Modules.Pos))
        {
            NavItems.Add(new NavItem("Counter (POS)", "Make a bill", () => new PosViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.Purchases))
        {
            NavItems.Add(new NavItem("Purchases", "Stock-in (PINV)", () => new PurchasesViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.Products))
        {
            NavItems.Add(new NavItem("Products", "Medicines and items", () => new ProductsViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.Returns))
        {
            NavItems.Add(new NavItem("Returns", "Money back, quarantine", () => new ReturnsViewModel(_services, _user)));
            NavItems.Add(new NavItem("No bill (PR)", "Rush return, bill later", () => new ProvisionalViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.Stock))
        {
            NavItems.Add(new NavItem("Write-off", "Expired, broken, lost", () => new WriteOffViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.StockTake))
        {
            NavItems.Add(new NavItem("Stock-take", "Count the shelves", () => new StockTakeViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.Cash))
        {
            NavItems.Add(new NavItem("Shift (galla)", "Open and close the drawer", () => new ShiftViewModel(_services, _user)));
            NavItems.Add(new NavItem("Expenses", "Money out, owner drawings", () => new ExpensesViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.Customers))
        {
            NavItems.Add(new NavItem("Customers", "Khata, money received", () => new CustomersViewModel(_services, _user, RefreshAlerts)));
        }

        if (_services.Permissions.Can(_user, Modules.Suppliers))
        {
            NavItems.Add(new NavItem("Suppliers", "Payable, goods sent back", () => new SuppliersViewModel(_services, _user, RefreshAlerts)));
        }

        if (_services.Permissions.Can(_user, Modules.Settings))
        {
            NavItems.Add(new NavItem("Settings", "Shop, printing, staff", CreateSettingsPage));
        }
    }

    /// <summary>
    /// One Settings page for the whole session: it keeps the staff selection and raises Saved, so a
    /// new shop name appears in the sidebar the moment the owner presses Save.
    /// </summary>
    private ObservableObject CreateSettingsPage()
    {
        if (_settingsPage is null)
        {
            _settingsPage = new SettingsViewModel(_services, _user, RefreshHeader);
        }

        _settingsPage.Refresh();
        return _settingsPage;
    }

    /// <summary>
    /// Reads the alert board for the top bar. Called when the page changes and after the owner ends
    /// an alert, so the bar never shows a problem that was just dealt with.
    /// </summary>
    private void RefreshAlerts()
    {
        var report = _services.Alert.Report();

        AlertText = report.BarText;
        HasAlertBar = report.HasLive && !_alertHiddenForSession;
    }

    private void HideAlertBar()
    {
        _alertHiddenForSession = true;
        HasAlertBar = false;
    }

    private void OpenAlerts()
    {
        var page = NavItems.FirstOrDefault(item => item.Title == "Alerts");
        if (page is not null)
        {
            SelectedNav = page;
        }
    }

    private void RefreshHeader()
    {
        StoreName = _services.Settings.GetString("store.name");
        AppName = _services.Settings.GetString("brand.appName");
    }

    private void Logout()
    {
        _services.Auth.Logout(_user);
        _services.SignOut();
        LoggedOut?.Invoke();
    }
}
