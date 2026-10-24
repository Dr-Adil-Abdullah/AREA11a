using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

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
    private string _currentPageTitle = string.Empty;
    private bool _showingDrawingFault;
    private bool _userMenuOpen;
    private bool _menuPinned;
    private bool _hasBellAlerts;
    private string _bellBadgeText = string.Empty;
    private string _bellSummaryText = "Nothing needs attention.";
    private string _faultBanner = string.Empty;
    private bool _hasFaultBanner;
    private readonly DispatcherTimer _sessionTimer;
    private string _sessionNotice = string.Empty;

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
        DismissFaultCommand = new RelayCommand(DismissFault);

        // A button that fails reports here, once, and the page it was on stays exactly as it was
        // (v0.34.0, D-90). Subscribed in the constructor and released in Detach(), because a window
        // that closes must not keep answering for a shell nobody is looking at.
        CommandGuard.Failed += OnCommandFailed;

        BuildNavigation();
        RefreshAlerts();

        // The first sign-in of the day makes the daily copy if it is switched on. It never throws
        // and never blocks the till: a failed backup is a line in the log and a note on the page.
        _services.Backup.RunAutomaticIfDue();

        // The home screen is the first page of the menu (F-12). There is no special start page any
        // more: the demo shop was removed from the product on the owner's word (D-77), so every start
        // is the same start, in the shop's own books.
        SelectedNav = NavItems.FirstOrDefault(item => item.IsPage);

        // The two clocks of the session (v0.35.0, D-92). Started here, at the sign-in, and stopped
        // when this window goes - a timer that outlives its window is a timer that signs somebody
        // out twice.
        SessionWatch.Start();
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _sessionTimer.Tick += (_, _) => CheckSession();
        _sessionTimer.Start();
    }

    public event Action? LoggedOut;

    /// <summary>
    /// Raised when the idle limit or the 12-hour limit ended the session. App shows the sign-in
    /// window; the drawer is **not** closed here, because closing it means counting the cash and a
    /// program must never invent that number (D-92).
    /// </summary>
    public event Action? SessionExpired;

    /// <summary>Any click or key press on the frame counts as work - the idle clock starts again.</summary>
    public void TouchSession() => SessionWatch.Touch();

    /// <summary>The warning line in the top bar, or empty when there is no hurry.</summary>
    public string SessionNotice
    {
        get => _sessionNotice;
        private set
        {
            if (SetProperty(ref _sessionNotice, value))
            {
                OnPropertyChanged(nameof(HasSessionNotice));
            }
        }
    }

    public bool HasSessionNotice => _sessionNotice.Length > 0;

    /// <summary>
    /// Reads both clocks once every 30 seconds (v0.35.0, D-92). The rule is in `SessionPolicy`, so
    /// the tests can prove it without a window; this method only carries out the verdict.
    /// </summary>
    private void CheckSession()
    {
        if (!SessionWatch.Running)
        {
            return;
        }

        var hours = _services.Settings.GetInt("session.idleHours", 2);
        var check = SessionPolicy.Check(
            SessionWatch.SignedInAt, SessionWatch.LastActivityAt, TimeSource.Now, hours);

        if (check.Expired)
        {
            ExpireSession(check);
            return;
        }

        SessionNotice = !check.Warning
            ? string.Empty
            : check.Next == SessionEnd.HardCap
                ? "Signed out in " + SessionPolicy.MinutesLeft(check) +
                  " minutes: the 12-hour limit cannot be extended by touching the screen."
                : "Signed out in " + SessionPolicy.MinutesLeft(check) +
                  " minutes because the counter is idle - touch the screen to stay signed in.";
    }

    /// <summary>Carries out an expired session: a note if the drawer is open, then the sign-in window.</summary>
    private void ExpireSession(SessionCheck check)
    {
        if (!SessionWatch.Running)
        {
            return;
        }

        _sessionTimer.Stop();
        SessionWatch.Stop();
        SessionNotice = string.Empty;

        var reason = check.End == SessionEnd.HardCap ? "the 12-hour limit" : "the idle limit";
        RunTrail.Step("session ended by " + reason + " for " + _user.Name);
        _services.Log.Info("Signed out automatically by " + reason + " for " + _user.Name + ".");

        // A drawer that is still open must be counted by a person, so it stays open and a note is
        // left where the next sign-in will find it (SessionHandover).
        if (_services.Shifts.Current() is not null)
        {
            SessionHandover.Mark(_services.Settings, _user.Name);
        }

        SessionExpired?.Invoke();
    }

    /// <summary>Closes the user's own menu and stops listening to the command guard.</summary>
    public void Detach()
    {
        CommandGuard.Failed -= OnCommandFailed;
        _sessionTimer.Stop();
        SessionWatch.Stop();
        UserMenuOpen = false;
    }

    /// <summary>
    /// The line above the page after a button failed, or empty when nothing has failed
    /// (v0.34.0, D-90). It names the button, says the page is still open, and names the report file
    /// when one was written - the owner is asked to send a file, so the file has to be findable.
    /// </summary>
    public string FaultBanner
    {
        get => _faultBanner;
        private set => SetProperty(ref _faultBanner, value);
    }

    public bool HasFaultBanner
    {
        get => _hasFaultBanner;
        private set => SetProperty(ref _hasFaultBanner, value);
    }

    public RelayCommand DismissFaultCommand { get; }

    private void OnCommandFailed(CommandOutcome outcome)
    {
        // The trail is what makes "no file" readable later (D-90): the button, the time and the
        // exact fault, in the same small file the report tool prints.
        RunTrail.Step("FAILED button \"" + outcome.Name + "\": " + outcome.Detail);

        FaultBanner = outcome.Sentence
                      + (outcome.HasReport ? " The whole detail was written to: " + outcome.ReportPath : string.Empty);
        HasFaultBanner = true;
    }

    private void DismissFault()
    {
        FaultBanner = string.Empty;
        HasFaultBanner = false;
    }

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
            CurrentViewModel = value is null ? null : OpenPage(value);

            // Move around the app and the bell refreshes itself; no timer, no background thread.
            RefreshAlerts();
        }
    }

    public ObservableObject? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            var previous = _currentViewModel;

            if (!SetProperty(ref _currentViewModel, value))
            {
                return;
            }

            // The "why is this page empty?" card belongs to the page on the screen, so the frame
            // listens to that page and asks it again whenever it says something changed (D-89).
            // Unsubscribing the old page matters: a page that is gone must not keep this window
            // alive, and two pages must never both refresh the same card.
            if (previous is not null)
            {
                previous.PropertyChanged -= OnPageChanged;

                if (previous is PosViewModel oldCounter)
                {
                    oldCounter.OpenShiftRequested -= GoToShift;
                }
            }

            if (value is not null)
            {
                value.PropertyChanged += OnPageChanged;
            }

            OnPropertyChanged(nameof(EmptyHint));
        }
    }

    /// <summary>
    /// Why the page on the screen has nothing on it, or **null** when it has rows (v0.33.0, D-89).
    ///
    /// The question is asked of the page itself, not kept here: a page knows what fills it, and a
    /// copy of that answer in the frame is a second truth that drifts the first time a page is
    /// changed. A page that does not implement <see cref="IEmptyPage"/> simply has no card.
    /// </summary>
    public EmptyHint? EmptyHint => (CurrentViewModel as IEmptyPage)?.EmptyHint;

    /// <summary>Walks to the Shift (galla) page - the one place a drawer may be opened (D-92).</summary>
    private void GoToShift()
    {
        var shift = NavItems.FirstOrDefault(item => item.IsPage && item.Title == "Shift (galla)");

        if (shift is not null)
        {
            SelectedNav = shift;
        }
    }

    /// <summary>A page changed something - a search, a save, a list reloaded - so ask it again.</summary>
    private void OnPageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(EmptyHint));

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
        var canBills = permissions.Can(_user, Modules.Sales);
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

        // The books check reads the whole shop's arithmetic: the same permission as its page (F-54).
        var canBooks = _services.Books.CanRun(_user);

        // Moving the shop's database changes every counter at once, so only the owner sees it (F-45).
        var canLan = _user.IsOwner;

        // Cloud sync copies the whole shop out of the building: it follows the Sync permission, which
        // the owner grants one person at a time (F-48, D-66).
        var canSync = _services.Cloud.CanSync(_user);

        NavItems.Add(new NavItem("Dashboard", "Today at a glance", () => new DashboardViewModel(_services, _user)));

        // ---- SELL: the counter and what comes back from it ----
        if (canSell || canReturn || canBills)
        {
            AddGroup("sell", "SELL");

            if (canSell)
            {
                AddPage("sell", "Counter (POS)", "Make a bill", () => new PosViewModel(_services, _user));
            }

            if (canBills)
            {
                // Beside the counter: the same bills, seen after the customer has gone home - and the
                // only place a copy can be printed (F-52).
                AddPage("sell", "Bills", "Find one, print it again", () => new SaleHistoryViewModel(_services, _user));
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
                AddPage("money", "Tax report", "The month for the accountant", () => new TaxViewModel(_services, _user));
            }
        }

        // ---- SYSTEM: the shop's own settings, its memory and its safety net ----
        if (canAudit || canBackup || canImport || canSettings || canLan || canSync || canBooks)
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

            if (canLan)
            {
                AddPage("system", "Second counter (LAN)", "Two PCs, one database",
                    () => new LanViewModel(_services, _user));
            }

            if (canSync)
            {
                // Beside the second counter: both are about the shop's data leaving this PC, and both
                // are the owner's decision alone (F-48).
                AddPage("system", "Cloud sync", "A copy at home", () => new CloudSyncViewModel(_services, _user));
            }

            if (canSettings)
            {
                // The owner's own questions sit beside Settings: both are his, both are about how the
                // shop's forms look, and neither is a day-to-day page (F-42).
                AddPage("system", "Own fields", "Your own questions", () => new CustomFieldsViewModel(_services, _user));

                // The printer and the scanner are tested on the shop's own paper, once, by the owner
                // (F-46) - so the page that proves it sits next to the page that configures it.
                AddPage("system", "Printer & scanner", "Test it on this PC",
                    () => new HardwareViewModel(_services, _user));

                AddPage("system", "Settings", "Shop, printing, staff", CreateSettingsPage);
            }

            if (canBooks)
            {
                // Read-only, so it sits beside the black box: both answer "is the shop's record
                // itself sound?" rather than "what shall we do next?" (F-54).
                AddPage("system", "Check the books", "Do the totals add up?", CreateBookCheckPage);
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
    /// Builds the page for a menu row, and **never lets its failure reach the dispatcher** (D-88).
    ///
    /// Why: every page's constructor reads the shop's database. One failed read used to throw out
    /// of `Resolve()` - inside a property setter, on the UI thread - and on 22 Oct 2026 that is how
    /// the window died. A page is not the program: if it cannot be built, the operator gets a card
    /// that names it, and the counter stays one click away.
    /// </summary>
    private ObservableObject OpenPage(NavItem page)
    {
        // Its name, so a fault raised later - while WPF is drawing this page - can still say which
        // page it was (v0.33.0, D-89). The window frame is the only place that knows.
        // The banner belongs to the page it was raised on (v0.34.0, D-90): once another page is on
        // the screen, its button is no longer the one the operator is looking at.
        DismissFault();
        _currentPageTitle = page.Title;
        RunTrail.Step("page: " + page.Title);

        var opened = PageGuard.Open<ObservableObject>(
            page.Title, () => page.Resolve(), _services.Log, RecordPageFault);

        if (opened.Ok && opened.Page is PosViewModel counter)
        {
            // The counter cannot open a drawer itself (it is a page, and opening a drawer is a money
            // action with a counted float), so its card asks the frame to walk to the shift page
            // (v0.35.0, D-92). Detached in CurrentViewModel when another page arrives.
            counter.OpenShiftRequested += GoToShift;
        }

        return opened.Ok ? opened.Page! : new PageFaultViewModel(opened.Fault!);
    }

    /// <summary>
    /// A fault that arrived **after** the page was built - WPF fell over while drawing it, and the
    /// exception reached the dispatcher (v0.33.0, D-89).
    ///
    /// Why the frame takes it: the dispatcher's handler in `App` already writes the crash file, but
    /// writing a file is not an explanation. Without this the operator is left looking at a
    /// half-drawn page with no words on it, and the honest-looking thing to do is to close the
    /// program. The page is replaced by the same card a construction fault gets, saying that the
    /// screen stopped being drawn - which is a different sentence from "could not be opened",
    /// because the data was read and the drawing is what failed.
    ///
    /// It never throws and it cannot loop: a fault raised while showing the card is ignored here.
    /// </summary>
    public void ShowDrawingFault(Exception? exception)
    {
        if (_showingDrawingFault)
        {
            return;
        }

        _showingDrawingFault = true;

        try
        {
            var title = string.IsNullOrWhiteSpace(_currentPageTitle) ? "page" : _currentPageTitle;
            var fault = PageGuard.Drawing(title, exception, _services.Log, RecordPageFault);

            CurrentViewModel = new PageFaultViewModel(fault);
        }
        catch (Exception)
        {
            // The crash file is already written by the caller; a card that fails must not become a
            // second fault on the dispatcher.
        }
        finally
        {
            _showingDrawingFault = false;
        }
    }

    /// <summary>
    /// Writes the page's fault where the owner can send it from: the crash report (which the batch
    /// files already know how to print and collect). Silently does nothing if it cannot be written -
    /// the card on the screen is the message that matters.
    /// </summary>
    private static string RecordPageFault(string pageTitle, Exception exception) =>
        CrashLog.Write("opening the " + pageTitle + " page", exception,
            "The page could not be opened. The rest of the program kept working.");

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
        // Settings reads the whole settings table twice (once to build the page, once to refresh
        // it), so it is the page most able to fail on a half-written file - and it is opened by
        // clicking the shop's own name, which the owner does without thinking (D-88).
        DismissFault();
        _currentPageTitle = "Settings";
        RunTrail.Step("page: Settings");

        var opened = PageGuard.Open<SettingsViewModel>("Settings", () =>
        {
            // Written out rather than `??=` inside a lambda: the compiler's own type of `??=` on a
            // nullable field is `SettingsViewModel?`, and a nullable type argument does not satisfy
            // this method's `where T : class`. The explicit T removes the guess entirely.
            if (_settingsPage is null)
            {
                _settingsPage = new SettingsViewModel(_services, _user, RefreshHeader);
            }

            var page = _settingsPage;
            page.Refresh();
            return page;
        }, _services.Log, RecordPageFault);

        return opened.Ok ? opened.Page! : new PageFaultViewModel(opened.Fault!);
    }

    /// <summary>The read-only check of the shop's own arithmetic (F-54).</summary>
    private ObservableObject CreateBookCheckPage() => new BookCheckViewModel(_services, _user);

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

        DismissFault();
        _currentPageTitle = "Alerts";
        RunTrail.Step("page: Alerts");

        var opened = PageGuard.Open<AlertsViewModel>("Alerts", () =>
        {
            if (_alertsPage is null)
            {
                _alertsPage = new AlertsViewModel(_services, _user, RefreshAlerts);
            }

            var page = _alertsPage;
            page.Load();
            return page;
        }, _services.Log, RecordPageFault);

        // No menu row belongs to this page, so the list clears itself (the bell stays lit).
        SetProperty(ref _selectedNav, null, nameof(SelectedNav));
        CurrentViewModel = opened.Ok ? opened.Page! : new PageFaultViewModel(opened.Fault!);
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
        _sessionTimer.Stop();
        SessionWatch.Stop();
        SessionNotice = string.Empty;
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
