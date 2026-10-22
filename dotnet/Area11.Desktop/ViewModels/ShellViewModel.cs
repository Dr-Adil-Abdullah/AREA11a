using System.Collections.ObjectModel;
using System.Windows.Input;
using Area11.Core.Models;
using Area11.Core.Services;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The window frame: menu on the left, current page on the right.
///
/// The shape the owner asked for (D-50):
///   • the menu is **short** - one line per page, grouped under small headings, and **Customers and
///     Suppliers are one entry** with three tabs because they are the two sides of the same book;
///   • the **bell at the top** is the only alert light now. It shows how many alerts are live and
///     opens them in a small panel: what needs the owner, and what anybody may put away;
///   • clicking the **shop name** opens Settings; clicking the **person's name** opens the staff
///     settings (who may do what), because that is where a person's question is answered.
///
/// The menu is built from the permissions of the signed-in person, but hiding a menu is never the
/// security - every service checks the permission again (Spec 7.2.5).
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
    private AlertsViewModel? _alertsPage;
    private bool _alertsOpen;
    private bool _hasBellAlerts;
    private string _bellBadgeText = string.Empty;
    private string _bellSummaryText = "Nothing needs attention.";
    private bool _hasSerious;
    private bool _hasLight;
    private bool _hasNoAlerts = true;

    public ShellViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        StoreName = services.Settings.GetString("store.name");
        AppName = services.Settings.GetString("brand.appName");

        LogoutCommand = new RelayCommand(Logout);
        OpenSettingsCommand = new RelayCommand(() => ShowSettings(0));
        OpenOwnerSettingsCommand = new RelayCommand(() => ShowSettings(3));
        OpenAlertsBoardCommand = new RelayCommand(OpenAlertsBoard);
        CloseAlertsCommand = new RelayCommand(() => AlertsOpen = false);
        DismissLightCommand = new RelayCommand<AlertItem>(CloseReminder);

        BuildNavigation();
        RefreshAlerts();
        SelectedNav = NavItems.FirstOrDefault(item => item.IsPage);
    }

    public event Action? LoggedOut;

    public ObservableCollection<NavItem> NavItems { get; } = new();

    /// <summary>What the bell panel shows under "Needs the owner".</summary>
    public ObservableCollection<AlertItem> SeriousAlerts { get; } = new();

    /// <summary>What the bell panel shows under "Anyone can close this".</summary>
    public ObservableCollection<AlertItem> LightAlerts { get; } = new();

    public RelayCommand LogoutCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand OpenOwnerSettingsCommand { get; }

    public RelayCommand OpenAlertsBoardCommand { get; }

    public RelayCommand CloseAlertsCommand { get; }

    /// <summary>Takes the clicked reminder as its parameter.</summary>
    public ICommand DismissLightCommand { get; }

    /// <summary>The bell panel is open. Setting it true re-reads the board first, so it is never stale.</summary>
    public bool AlertsOpen
    {
        get => _alertsOpen;
        set
        {
            if (value)
            {
                RefreshAlerts();
            }

            SetProperty(ref _alertsOpen, value);
        }
    }

    /// <summary>True = draw the red badge on the bell.</summary>
    public bool HasBellAlerts
    {
        get => _hasBellAlerts;
        private set => SetProperty(ref _hasBellAlerts, value);
    }

    /// <summary>The number in the badge, capped at "9+" so it never widens the bell.</summary>
    public string BellBadgeText
    {
        get => _bellBadgeText;
        private set => SetProperty(ref _bellBadgeText, value);
    }

    /// <summary>The bell's tooltip: "3 need the owner · 1 reminder".</summary>
    public string BellSummaryText
    {
        get => _bellSummaryText;
        private set => SetProperty(ref _bellSummaryText, value);
    }

    public bool HasSeriousAlerts
    {
        get => _hasSerious;
        private set => SetProperty(ref _hasSerious, value);
    }

    public bool HasLightAlerts
    {
        get => _hasLight;
        private set => SetProperty(ref _hasLight, value);
    }

    /// <summary>Drives the "Nothing needs attention" line, so the panel is never just empty air.</summary>
    public bool HasNoAlerts
    {
        get => _hasNoAlerts;
        private set => SetProperty(ref _hasNoAlerts, value);
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
            // A heading is not a page: clicking near it must not blank the screen.
            if (value is { IsHeader: true })
            {
                return;
            }

            if (!SetProperty(ref _selectedNav, value))
            {
                return;
            }

            CurrentViewModel = value?.Resolve();

            // Move around the app and the bell refreshes itself; no timer, no background thread.
            RefreshAlerts();
        }
    }

    public ObservableObject? CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    /// <summary>
    /// The menu, short and grouped. Only the owner sees the owner's pages, and a heading is drawn
    /// only when at least one page follows it - a cashier must never read "MONEY" over nothing.
    ///
    /// The permissions are read once, at the top, because the same answer decides both the heading
    /// and the rows under it. Reading them twice is how a menu grows a heading with no pages.
    /// </summary>
    private void BuildNavigation()
    {
        var permissions = _services.Permissions;

        var canSell = permissions.Can(_user, Modules.Pos);
        var canReturn = permissions.Can(_user, Modules.Returns);
        var canProducts = permissions.Can(_user, Modules.Products);
        var canPurchase = permissions.Can(_user, Modules.Purchases);
        var canWriteOff = permissions.Can(_user, Modules.Stock);
        var canCount = permissions.Can(_user, Modules.StockTake);
        var canCustomers = permissions.Can(_user, Modules.Customers);
        var canSuppliers = permissions.Can(_user, Modules.Suppliers);
        var canCash = permissions.Can(_user, Modules.Cash);
        var canReports = permissions.Can(_user, Modules.Reports);
        var canAudit = permissions.Can(_user, Modules.Audit);
        var canSettings = CanOpenSettings;

        NavItems.Add(new NavItem("Dashboard", "Today at a glance", () => new DashboardViewModel(_services)));

        // ---- SELL: the counter and what comes back from it ----
        if (canSell || canReturn)
        {
            AddGroup("SELL");
        }

        if (canSell)
        {
            NavItems.Add(new NavItem("Counter (POS)", "Make a bill", () => new PosViewModel(_services, _user)));
        }

        if (canReturn)
        {
            NavItems.Add(new NavItem("Returns", "Money back, quarantine", () => new ReturnsViewModel(_services, _user)));
            NavItems.Add(new NavItem("No bill (PR)", "Rush return, bill later", () => new ProvisionalViewModel(_services, _user)));
        }

        // ---- STOCK: what is on the shelf and how it got there ----
        if (canProducts || canPurchase || canWriteOff || canCount)
        {
            AddGroup("STOCK");
        }

        if (canProducts)
        {
            NavItems.Add(new NavItem("Products", "Medicines and items", () => new ProductsViewModel(_services, _user)));
        }

        if (canPurchase)
        {
            NavItems.Add(new NavItem("Purchases", "Stock-in (PINV)", () => new PurchasesViewModel(_services, _user)));
        }

        if (canWriteOff)
        {
            NavItems.Add(new NavItem("Write-off", "Expired, broken, lost", () => new WriteOffViewModel(_services, _user)));
        }

        if (canCount)
        {
            NavItems.Add(new NavItem("Stock-take", "Count the shelves", () => new StockTakeViewModel(_services, _user)));
        }

        // ---- PEOPLE: one entry, three tabs - the owner's grouping (D-50) ----
        if (canCustomers || canSuppliers)
        {
            AddGroup("CUSTOMERS & SUPPLIERS");

            // The tabs each person may open are decided by PartnersViewModel from the same two
            // permissions, so the menu and the page can never disagree.
            NavItems.Add(new NavItem("Customers & suppliers",
                "Khata, udhaar recovery, payables",
                () => new PartnersViewModel(_services, _user, RefreshAlerts)));
        }

        // ---- MONEY: the drawer and what it must explain ----
        if (canCash || canReports)
        {
            AddGroup("MONEY");
        }

        if (canCash)
        {
            NavItems.Add(new NavItem("Shift (galla)", "Open and close the drawer", () => new ShiftViewModel(_services, _user)));
            NavItems.Add(new NavItem("Expenses", "Money out, owner drawings", () => new ExpensesViewModel(_services, _user)));
        }

        if (canReports)
        {
            NavItems.Add(new NavItem("Reports", "Day, week, dead stock", () => new ReportsViewModel(_services, _user)));
        }

        // ---- SYSTEM: the shop's own settings and its memory ----
        if (canAudit || canSettings)
        {
            AddGroup("SYSTEM");
        }

        if (canAudit)
        {
            NavItems.Add(new NavItem("Black box", "Every change, who made it", () => new AuditViewModel(_services, _user)));
        }

        if (canSettings)
        {
            NavItems.Add(new NavItem("Settings", "Shop, printing, staff", CreateSettingsPage));
        }
    }

    /// <summary>Adds a heading. A heading is only worth drawing if at least one page follows it.</summary>
    private void AddGroup(string title) => NavItems.Add(NavItem.Header(title));

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
    /// True when this person may open Settings at all - the shop's page or the staff page. The two
    /// buttons in the top bar are disabled otherwise, so a cashier is never offered a click that
    /// does nothing (the shop name still tells them which shop they are in).
    /// </summary>
    public bool CanOpenSettings =>
        _services.Permissions.Can(_user, Modules.Settings)
        || _services.Permissions.Can(_user, Modules.Staff);

    /// <summary>
    /// Opens Settings on a tab: 0 = Shop (the shop name was clicked), 3 = Staff &amp; permissions
    /// (the person's name was clicked). The page is created on the way, because the menu row may not
    /// have been opened yet in this session.
    /// </summary>
    private void ShowSettings(int tab)
    {
        if (!CanOpenSettings)
        {
            return;
        }

        var page = NavItems.FirstOrDefault(item => item.IsPage && item.Title == "Settings");

        if (page is null)
        {
            return;
        }

        // SelectedNav resolves the page (creating the view model if needed), then ShowTab moves the
        // TabControl. Doing it in this order keeps the two steps independent of the click order.
        SelectedNav = page;
        _settingsPage?.ShowTab(tab);
    }

    /// <summary>The bell's "open the full board" button. The board is not in the menu any more.</summary>
    private void OpenAlertsBoard()
    {
        AlertsOpen = false;

        _alertsPage ??= new AlertsViewModel(_services, _user, RefreshAlerts);

        // The board is asked for by name, so it must be current: an owner who ended an alert on the
        // Shift page and then opens the board must not see the old list.
        _alertsPage.Load();

        // No menu row is selected while the board is open from the bell, so the list clears itself.
        SetProperty(ref _selectedNav, null, nameof(SelectedNav));
        CurrentViewModel = _alertsPage;
    }

    /// <summary>Closes one reminder from the bell. Any signed-in person may do this (D-50).</summary>
    private void CloseReminder(AlertItem? reminder)
    {
        if (reminder is null)
        {
            return;
        }

        var closed = _services.Alert.DismissLight(_user, reminder.Key);

        RefreshAlerts();

        if (!closed.Ok)
        {
            BellSummaryText = closed.Error ?? "The reminder could not be closed.";
        }
    }

    /// <summary>
    /// Reads the alert board for the bell. Called when the page changes, when the bell opens and
    /// after anything that could change a number, so the badge never shows a problem that was just
    /// dealt with.
    /// </summary>
    private void RefreshAlerts()
    {
        var report = _services.Alert.Report();

        SeriousAlerts.Clear();
        LightAlerts.Clear();

        foreach (var alert in report.Serious)
        {
            SeriousAlerts.Add(alert);
        }

        foreach (var alert in report.Light)
        {
            LightAlerts.Add(alert);
        }

        HasSeriousAlerts = SeriousAlerts.Count > 0;
        HasLightAlerts = LightAlerts.Count > 0;
        HasNoAlerts = !report.HasLive;
        HasBellAlerts = report.HasLive;
        BellBadgeText = report.LiveCount > 9 ? "9+" : report.LiveCount.ToString();

        // The bell's tooltip. CloseReminder may overwrite it with a refusal message afterwards, and
        // the next refresh puts the sentence back.
        BellSummaryText = report.BellText;
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
