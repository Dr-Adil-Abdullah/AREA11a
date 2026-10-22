using System.Collections.ObjectModel;
using System.Windows.Input;
using Area11.Core.Models;
using Area11.Core.Services;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The window frame (D-51): a **46-pixel bar across the top** and a 176-pixel menu on the left.
///
/// The bar carries exactly three things, because the owner counted the cost of every extra pixel:
///   • **Area11, small**, top left - a mark, not a title; a click goes back to the Dashboard;
///   • the **shop name in the centre** - a click opens Settings, Shop tab;
///   • the **bell beside it** - the live count and the only door to the alerts page.
///
/// Everything else moved down into the left bar: the **signed-in person** (photo, name, role) at the
/// bottom with their own menu (settings, sign out), and the menu itself, grouped under five headings.
/// Each heading is a fold: one click puts its pages away and the choice is remembered next morning.
///
/// The menu is built from the permissions of the signed-in person, but hiding a menu is never the
/// security - every service checks the permission again (Spec 7.2.5).
///
/// Folding hides rows; it never removes a page, a permission or the page already on the screen.
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly List<NavGroup> _groups = new();

    private NavItem? _selectedNav;
    private ObservableObject? _currentViewModel;
    private string _storeName = string.Empty;
    private string _appName = string.Empty;
    private SettingsViewModel? _settingsPage;
    private AlertsViewModel? _alertsPage;
    private bool _isShowingAlerts;
    private bool _userMenuOpen;
    private bool _menuPinned;
    private bool _hasBellAlerts;
    private string _bellBadgeText = string.Empty;
    private string _bellSummaryText = "Nothing needs attention.";

    public ShellViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        StoreName = services.Settings.GetString("store.name");
        AppName = services.Settings.GetString("brand.appName");

        LogoutCommand = new RelayCommand(Logout);
        OpenSettingsCommand = new RelayCommand(() => ShowSettings(0));
        OpenOwnerSettingsCommand = new RelayCommand(() => ShowSettings(3));
        OpenAlertsCommand = new RelayCommand(ShowAlerts);
        GoHomeCommand = new RelayCommand(GoHome);
        ToggleGroupCommand = new RelayCommand<NavItem>(ToggleGroup);
        OpenUserMenuCommand = new RelayCommand(() => UserMenuOpen = true);
        CloseUserMenuCommand = new RelayCommand(() => UserMenuOpen = false);

        BuildNavigation();
        RefreshAlerts();

        // The first sign-in of the day makes the daily copy if it is switched on. It never throws
        // and never blocks the till: a failed backup is a line in the log and a note on the page.
        _services.Backup.RunAutomaticIfDue();
        SelectedNav = NavItems.FirstOrDefault(item => item.IsPage);
    }

    public event Action? LoggedOut;

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public RelayCommand LogoutCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand OpenOwnerSettingsCommand { get; }

    /// <summary>The bell: it opens the alerts PAGE, not a side panel (D-51).</summary>
    public RelayCommand OpenAlertsCommand { get; }

    /// <summary>The small logo: back to the dashboard from anywhere.</summary>
    public RelayCommand GoHomeCommand { get; }

    /// <summary>Takes the clicked heading as its parameter and puts its pages away or back.</summary>
    public ICommand ToggleGroupCommand { get; }

    public RelayCommand OpenUserMenuCommand { get; }

    public RelayCommand CloseUserMenuCommand { get; }

    /// <summary>True while the alerts page is on the screen; it lights the bell up.</summary>
    public bool IsShowingAlerts
    {
        get => _isShowingAlerts;
        private set => SetProperty(ref _isShowingAlerts, value);
    }

    /// <summary>True while the small "me" menu (settings / sign out) is open.</summary>
    public bool UserMenuOpen
    {
        get => _userMenuOpen;
        set => SetProperty(ref _userMenuOpen, value);
    }

    /// <summary>True = draw the count badge on the bell.</summary>
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

    /// <summary>
    /// True when this person may open Settings at all - the shop's page or the staff page. The two
    /// entries that lead there are disabled otherwise, so nobody is offered a click that does
    /// nothing.
    /// </summary>
    public bool CanOpenSettings =>
        _services.Permissions.Can(_user, Modules.Settings)
        || _services.Permissions.Can(_user, Modules.Staff);

    /// <summary>False while the owner has ticked "keep every group open" in Settings (D-51).</summary>
    public bool CanFoldMenu => !_menuPinned;

    public NavItem? SelectedNav
    {
        get => _selectedNav;
        set
        {
            // A heading is not a page: its row toggles the fold instead of opening anything.
            if (value is { IsHeader: true })
            {
                return;
            }

            if (!SetProperty(ref _selectedNav, value))
            {
                return;
            }

            IsShowingAlerts = false;
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
        var canReorder = permissions.Can(_user, Modules.Reorder);
        var canCustomers = permissions.Can(_user, Modules.Customers);
        var canSuppliers = permissions.Can(_user, Modules.Suppliers);
        var canCash = permissions.Can(_user, Modules.Cash);
        var canReports = permissions.Can(_user, Modules.Reports);
        var canAudit = permissions.Can(_user, Modules.Audit);
        var canBackup = permissions.Can(_user, Modules.Backup);
        var canImport = permissions.Can(_user, Modules.Import);
        var canSettings = CanOpenSettings;

        NavItems.Add(new NavItem("Dashboard", "Today at a glance", () => new DashboardViewModel(_services)));

        // ---- SELL: the counter and what comes back from it ----
        if (canSell || canReturn)
        {
            AddGroup("sell", "SELL");

            if (canSell)
            {
                AddPage("sell", "Counter (POS)", "Make a bill", () => new PosViewModel(_services, _user));
            }

            if (canReturn)
            {
                AddPage("sell", "Returns", "Money back, quarantine", () => new ReturnsViewModel(_services, _user));
                AddPage("sell", "No bill (PR)", "Rush return, bill later", () => new ProvisionalViewModel(_services, _user));
            }
        }

        // ---- STOCK: what is on the shelf and how it got there ----
        if (canProducts || canPurchase || canWriteOff || canCount || canReorder)
        {
            AddGroup("stock", "STOCK");

            if (canProducts)
            {
                AddPage("stock", "Products", "Medicines and items", () => new ProductsViewModel(_services, _user));
            }

            if (canReorder)
            {
                // Right under Products: the owner opens this page when he notices a shelf is thin,
                // and the answer he wants is the message to the supplier.
                AddPage("stock", "Reorder pad", "What is running out, WhatsApp text",
                    () => new ReorderViewModel(_services, _user));
            }

            if (canPurchase)
            {
                AddPage("stock", "Purchases", "Stock-in (PINV)", () => new PurchasesViewModel(_services, _user));
            }

            if (canWriteOff)
            {
                AddPage("stock", "Write-off", "Expired, broken, lost", () => new WriteOffViewModel(_services, _user));

                // The other side of the same coin: the shelves that are below zero and the owner's
                // monthly look at them (F-39).
                AddPage("stock", "Stock minus", "Below zero, monthly review",
                    () => new StockMinusViewModel(_services, _user));
            }

            if (canCount)
            {
                AddPage("stock", "Stock-take", "Count the shelves", () => new StockTakeViewModel(_services, _user));
            }
        }

        // ---- PEOPLE: one entry, three tabs - the owner's grouping (D-50) ----
        if (canCustomers || canSuppliers || canProducts)
        {
            AddGroup("people", "CUSTOMERS & SUPPLIERS");

            if (canCustomers || canSuppliers)
            {
                // The tabs each person may open are decided by PartnersViewModel from the same two
                // permissions, so the menu and the page can never disagree.
                AddPage("people", "Customers & suppliers", "Khata, udhaar recovery, payables",
                    () => new PartnersViewModel(_services, _user, RefreshAlerts));
            }

            // Reps need the products permission, the doctors need customers; each block of the page
            // hides itself when its own permission is missing (F-44).
            if (canProducts || canCustomers)
            {
                AddPage("people", "Doctors & reps", "Prescriptions, reps and lifetime profit",
                    () => new DoctorsViewModel(_services, _user));
            }
        }

        // ---- MONEY: the drawer and what it must explain ----
        if (canCash || canReports)
        {
            AddGroup("money", "MONEY");

            if (canCash)
            {
                AddPage("money", "Shift (galla)", "Open and close the drawer", () => new ShiftViewModel(_services, _user));
                AddPage("money", "Expenses", "Money out, owner drawings", () => new ExpensesViewModel(_services, _user));
            }

            if (canReports)
            {
                AddPage("money", "Reports", "Day, week, dead stock", () => new ReportsViewModel(_services, _user));
            }
        }

        // ---- SYSTEM: the shop's own settings, its memory and its safety net ----
        if (canAudit || canBackup || canImport || canSettings)
        {
            AddGroup("system", "SYSTEM");

            if (canAudit)
            {
                AddPage("system", "Black box", "Every change, who made it", () => new AuditViewModel(_services, _user));
            }

            if (canImport)
            {
                // Import sits above backup: a new shop needs it on day one, before it needs a copy.
                AddPage("system", "Excel / CSV", "Import, export, prices",
                    () => new ImportExportViewModel(_services, _user));
            }

            if (canBackup)
            {
                // The one page that can bring a dead shop back. It sits above Settings on purpose:
                // it is the first thing to open after an update and the first thing to open after
                // a bad day, and both happen more often than a change of printer paper.
                AddPage("system", "Backup & restore", "ZIP of database + photos", CreateBackupPage);
            }

            if (canSettings)
            {
                // The owner's own questions sit beside Settings: both are his, both are about how the
                // shop's forms look, and neither is a day-to-day page (F-42).
                AddPage("system", "Own fields", "Your own questions", () => new CustomFieldsViewModel(_services, _user));
                AddPage("system", "Settings", "Shop, printing, staff", CreateSettingsPage);
            }
        }

        ApplyMenuState();
    }

    /// <summary>
    /// Reads what the menu should look like: either the owner's tick says "always open", or each
    /// group comes back the way it was left. The state lives in the settings table, so it survives a
    /// restart - and a cashier's fold on the till never travels to the owner's laptop.
    /// </summary>
    private void ApplyMenuState()
    {
        _menuPinned = _services.Settings.GetBool("menu.pinnedOpen");

        var folded = _menuPinned
            ? new List<string>()
            : _services.Settings.GetList<string>("menu.collapsedGroups");

        foreach (var group in _groups)
        {
            SetCollapsed(group, folded.Contains(group.Key, StringComparer.Ordinal));
        }
    }

    /// <summary>Adds a foldable heading. It is only created when a page follows it (see BuildNavigation).</summary>
    private void AddGroup(string key, string title)
    {
        var header = NavItem.Header(title);
        header.GroupKey = key;

        _groups.Add(new NavGroup(key, header));
        NavItems.Add(header);
    }

    /// <summary>Adds one page under an existing heading.</summary>
    private void AddPage(string key, string title, string subtitle, Func<ObservableObject> resolve)
    {
        var page = new NavItem(title, subtitle, resolve) { GroupKey = key };
        var group = _groups.First(item => item.Key == key);

        group.Pages.Add(page);
        NavItems.Add(page);
    }

    /// <summary>
    /// Opens or closes a heading. The rows are hidden, not removed: rebuilding the list would drop
    /// the selected row (and with it the page the operator is reading).
    ///
    /// A page that disappears into a fold stays where it is - the operator asked to tidy the menu,
    /// not to be thrown off the screen.
    /// </summary>
    private void ToggleGroup(NavItem? header)
    {
        if (header is null || !header.IsHeader)
        {
            return;
        }

        var group = _groups.FirstOrDefault(item => item.Key == header.GroupKey);

        // The owner's tick wins over a stray click: with "keep every group open" the headings are
        // furniture, not buttons.
        if (group is null || _menuPinned)
        {
            return;
        }

        SetCollapsed(group, !header.IsCollapsed);
        SaveMenuState();
    }

    /// <summary>
    /// Remembers the folds. It goes in by the quiet door (<c>SetUiOnly</c>): folding the menu is not a
    /// business change, and it must not fill the black box.
    /// </summary>
    private void SaveMenuState()
    {
        var folded = _groups.Where(group => group.Header.IsCollapsed)
            .Select(group => group.Key)
            .ToList();

        _services.Settings.SetUiOnly("menu.collapsedGroups", folded);
    }

    private static void SetCollapsed(NavGroup group, bool collapsed)
    {
        group.Header.IsCollapsed = collapsed;

        foreach (var page in group.Pages)
        {
            page.IsVisible = !collapsed;
        }
    }

    private void GoHome()
    {
        SelectedNav = NavItems.FirstOrDefault(item => item.IsPage && item.GroupKey.Length == 0);
    }

    /// <summary>
    /// One Settings page for the whole session: it keeps the staff selection and raises Saved, so a
    /// new shop name appears in the menu the moment the owner presses Save.
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
    /// The backup page. A restore replaces the database file, so the whole program is started again
    /// afterwards - one of these pages would otherwise keep showing numbers from a file that is gone.
    /// </summary>
    private ObservableObject CreateBackupPage() =>
        new BackupViewModel(_services, _user, AppRestart.Restart);

    /// <summary>
    /// Opens Settings on a tab: 0 = Shop (the shop name was clicked), 3 = Staff &amp; permissions
    /// (the person's name was clicked). The page is created on the way, because the menu row may not
    /// have been opened yet in this session.
    /// </summary>
    private void ShowSettings(int tab)
    {
        UserMenuOpen = false;

        if (!CanOpenSettings)
        {
            return;
        }

        var page = NavItems.FirstOrDefault(item => item.IsPage && item.Title == "Settings");

        if (page is null)
        {
            return;
        }

        // If the SYSTEM group was folded away, the Settings row is hidden - open the group first, or
        // the selection would point at a row the operator cannot see.
        var group = _groups.FirstOrDefault(item => item.Key == page.GroupKey);

        if (group is not null && group.Header.IsCollapsed)
        {
            SetCollapsed(group, false);
        }

        SelectedNav = page;
        _settingsPage?.ShowTab(tab);
    }

    /// <summary>
    /// The bell. Alerts are an ordinary page now (D-51): nothing slides in over the work, and the
    /// owner can read the reason, the history and the two kinds side by side.
    /// </summary>
    private void ShowAlerts()
    {
        UserMenuOpen = false;
        RefreshAlerts();

        _alertsPage ??= new AlertsViewModel(_services, _user, RefreshAlerts);
        _alertsPage.Load();

        // No menu row belongs to this page, so the list clears itself (the bell stays lit).
        SetProperty(ref _selectedNav, null, nameof(SelectedNav));
        CurrentViewModel = _alertsPage;
        IsShowingAlerts = true;
    }

    /// <summary>
    /// Reads the alert board for the bell. Called when the page changes, when the bell opens and
    /// after anything that could change a number, so the badge never shows a problem that was just
    /// dealt with.
    /// </summary>
    private void RefreshAlerts()
    {
        var report = _services.Alert.Report();

        HasBellAlerts = report.HasLive;
        BellBadgeText = report.LiveCount > 9 ? "9+" : report.LiveCount.ToString();
        BellSummaryText = report.BellText;
    }

    private void RefreshHeader()
    {
        StoreName = _services.Settings.GetString("store.name");
        AppName = _services.Settings.GetString("brand.appName");

        // The owner may have just ticked (or unticked) "keep every group open" on the Settings page
        // he was taken to by clicking the shop name. Apply it here, or the fold would keep working
        // until the next restart.
        ApplyMenuState();
        OnPropertyChanged(nameof(CanFoldMenu));
    }

    private void Logout()
    {
        UserMenuOpen = false;
        _services.Auth.Logout(_user);
        _services.SignOut();
        LoggedOut?.Invoke();
    }

    /// <summary>One foldable group of menu rows: the heading that opens and closes it, and its pages.</summary>
    private sealed class NavGroup
    {
        public NavGroup(string key, NavItem header)
        {
            Key = key;
            Header = header;
        }

        public string Key { get; }

        public NavItem Header { get; }

        public List<NavItem> Pages { get; } = new();
    }
}
