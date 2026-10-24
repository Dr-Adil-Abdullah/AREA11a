using System.Collections.ObjectModel;
using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

// empty-page: none - the page is a set of forms (shop, printing, staff, permissions), not a list;
// a "nothing here yet" card over a form would be telling the operator the wrong thing.
/// <summary>
/// Settings (F-17) — the screen the owner asked for when he said printing must be a choice
/// ("in setting there is an option for auto or manu[a]l printing", D-26).
///
/// Five tabs, one save button:
///   • Shop        — name, address, phone, the note at the bottom of the receipt.
///   • Printing    — printer, 58 / 80 mm, automatic or manual, cut, copies.
///   • Sales       — discount limits, tax, negative stock.
///   • Cloud sync  — the Supabase address, key, table and shop id (F-48, Spec 25.3).
///   • Staff       — people, their role and one checkbox per module (D-03).
///
/// All rules live in Core: <see cref="SettingsService"/> refuses a bad width, a bad copy count or a
/// cashier limit above the manager's, and <see cref="PermissionService"/> refuses a permission change
/// from anyone but the owner. This class only moves text between the screen and those services.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly Action? _onSaved;

    private string _storeName = string.Empty;
    private string _storeAddress = string.Empty;
    private string _storePhone = string.Empty;
    private string _footerNote = string.Empty;
    private string _appName = string.Empty;
    private bool _menuPinnedOpen;

    private string _printerName = string.Empty;
    private PaperOption? _selectedPaper;
    private bool _autoPrint;
    private bool _autoCut;
    private string _copiesText = "1";
    private bool _showDiscount;

    private string _cashierLimitText = "5";
    private string _managerLimitText = "20";
    private bool _blockBelowCost;
    private bool _taxEnabled;
    private string _taxPercentText = "0";
    private bool _allowNegativeStock;

    private string _expiry1Text = "365";
    private string _expiry2Text = "180";
    private string _expiry3Text = "90";
    private string _expirySummary = string.Empty;

    private string _supplierCutoffText = "90";
    private string _supplierAlertText = "180";

    private bool _loyaltyEnabled;
    private string _loyaltyPerPointText = "100";
    private string _loyaltyVipText = "500";
    private string _loyaltySummary = string.Empty;

    // ---- Cloud sync (F-48): the key is typed here but never read back onto the screen ----
    private bool _syncEnabled;
    private string _syncUrl = string.Empty;
    private string _syncTable = "area11_sync";
    private string _syncShopId = string.Empty;
    private string _syncKey = string.Empty;
    private string _syncKeyLabel = string.Empty;
    private string _syncSummary = string.Empty;
    private bool _syncKeyCleared;

    private int _selectedTabIndex;
    private IdleHourOption? _selectedIdleHour;
    private string _clockText = string.Empty;
    private User? _selectedUser;
    private string _newStaffName = string.Empty;
    private string _newStaffPin = string.Empty;
    private string _selectedRole = "Cashier";

    private string _statusText = "Changes are saved when you press Save.";
    private bool _isStatusError;
    private bool _suppressPermissionSave;

    public SettingsViewModel(AppServices services, User user, Action? onSaved = null)
    {
        _services = services;
        _user = user;
        _onSaved = onSaved;

        SaveCommand = new RelayCommand(Save);
        AddStaffCommand = new RelayCommand(AddStaff);
        ResetPermissionsCommand = new RelayCommand(ResetPermissions);
        ToggleActiveCommand = new RelayCommand(ToggleActive);
        RefreshStaffCommand = new RelayCommand(LoadUsers);
        ClearSyncKeyCommand = new RelayCommand(ClearSyncKey);

        PaperOptions = new ObservableCollection<PaperOption>
        {
            new(58, "58 mm  —  32 characters per line (small counter printer)"),
            new(80, "80 mm  —  48 characters per line (wide printer)")
        };

        IdleHourOptions = new ObservableCollection<IdleHourOption>
        {
            new(0, "Never - only the 12-hour limit"),
            new(1, "After 1 hour without work"),
            new(2, "After 2 hours without work"),
            new(4, "After 4 hours without work"),
            new(6, "After 6 hours without work"),
            new(12, "After 12 hours without work")
        };

        Load();
        LoadUsers();
    }

    public ObservableCollection<PaperOption> PaperOptions { get; }

    /// <summary>The sign-out choices (v0.35.0, D-92). 12 hours is also the ceiling for every choice.</summary>
    public ObservableCollection<IdleHourOption> IdleHourOptions { get; }

    public IdleHourOption? SelectedIdleHour
    {
        get => _selectedIdleHour;
        set => SetProperty(ref _selectedIdleHour, value);
    }

    /// <summary>Where the shop's time comes from, in the owner's words - set by Load().</summary>
    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

    public ObservableCollection<User> Users { get; } = new();

    public ObservableCollection<PermissionToggle> Permissions { get; } = new();

    public IReadOnlyList<string> StaffRoles { get; } = new[] { "Manager", "Salesman", "Cashier" };

    public RelayCommand SaveCommand { get; }

    public RelayCommand AddStaffCommand { get; }

    public RelayCommand ResetPermissionsCommand { get; }

    public RelayCommand ToggleActiveCommand { get; }

    public RelayCommand RefreshStaffCommand { get; }

    /// <summary>Takes the saved key off this computer (it takes effect when Save is pressed).</summary>
    public RelayCommand ClearSyncKeyCommand { get; }

    public bool CanManageStaff => _services.Permissions.Can(_user, Modules.Staff);

    public bool CannotManageStaff => !CanManageStaff;

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

    // ------------------------------- Shop -------------------------------

    public string StoreName
    {
        get => _storeName;
        set => SetProperty(ref _storeName, value);
    }

    public string StoreAddress
    {
        get => _storeAddress;
        set => SetProperty(ref _storeAddress, value);
    }

    public string StorePhone
    {
        get => _storePhone;
        set => SetProperty(ref _storePhone, value);
    }

    public string FooterNote
    {
        get => _footerNote;
        set => SetProperty(ref _footerNote, value);
    }

    /// <summary>
    /// The owner's tick (D-51): every menu group stays open and the headings stop folding. A cashier
    /// who keeps the whole menu in use sets this once and never thinks about it again.
    /// </summary>
    public bool MenuPinnedOpen
    {
        get => _menuPinnedOpen;
        set => SetProperty(ref _menuPinnedOpen, value);
    }

    public string AppName
    {
        get => _appName;
        set => SetProperty(ref _appName, value);
    }

    // ----------------------------- Printing -----------------------------

    public string PrinterName
    {
        get => _printerName;
        set => SetProperty(ref _printerName, value);
    }

    public PaperOption? SelectedPaper
    {
        get => _selectedPaper;
        set => SetProperty(ref _selectedPaper, value);
    }

    /// <summary>The owner's choice: printed automatically after the sale, or only when asked.</summary>
    public bool AutoPrint
    {
        get => _autoPrint;
        set => SetProperty(ref _autoPrint, value);
    }

    public bool AutoCut
    {
        get => _autoCut;
        set => SetProperty(ref _autoCut, value);
    }

    public string CopiesText
    {
        get => _copiesText;
        set => SetProperty(ref _copiesText, value);
    }

    public bool ShowDiscount
    {
        get => _showDiscount;
        set => SetProperty(ref _showDiscount, value);
    }

    // ------------------------------ Sales -------------------------------

    public string CashierLimitText
    {
        get => _cashierLimitText;
        set => SetProperty(ref _cashierLimitText, value);
    }

    public string ManagerLimitText
    {
        get => _managerLimitText;
        set => SetProperty(ref _managerLimitText, value);
    }

    public bool BlockBelowCost
    {
        get => _blockBelowCost;
        set => SetProperty(ref _blockBelowCost, value);
    }

    public bool TaxEnabled
    {
        get => _taxEnabled;
        set => SetProperty(ref _taxEnabled, value);
    }

    public string TaxPercentText
    {
        get => _taxPercentText;
        set => SetProperty(ref _taxPercentText, value);
    }

    public bool AllowNegativeStock
    {
        get => _allowNegativeStock;
        set => SetProperty(ref _allowNegativeStock, value);
    }

    /// <summary>Level 1 is the farthest band (default a year), Level 3 the nearest (three months).</summary>
    public string Expiry1Text
    {
        get => _expiry1Text;
        set => SetProperty(ref _expiry1Text, value);
    }

    public string Expiry2Text
    {
        get => _expiry2Text;
        set => SetProperty(ref _expiry2Text, value);
    }

    public string Expiry3Text
    {
        get => _expiry3Text;
        set => SetProperty(ref _expiry3Text, value);
    }

    /// <summary>The last day the supplier takes goods back, counted backwards from the expiry date.</summary>
    public string SupplierCutoffText
    {
        get => _supplierCutoffText;
        set => SetProperty(ref _supplierCutoffText, value);
    }

    /// <summary>How many days before expiry the alert board starts reminding the owner.</summary>
    public string SupplierAlertText
    {
        get => _supplierAlertText;
        set => SetProperty(ref _supplierAlertText, value);
    }

    // ------------------------------ Loyalty -------------------------------

    /// <summary>Off by default (Q-06): the owner decides whether the shop gives points at all.</summary>
    public bool LoyaltyEnabled
    {
        get => _loyaltyEnabled;
        set
        {
            if (SetProperty(ref _loyaltyEnabled, value))
            {
                RaiseLoyaltySummary();
            }
        }
    }

    /// <summary>Rupees the customer must spend to earn one point.</summary>
    public string LoyaltyPerPointText
    {
        get => _loyaltyPerPointText;
        set
        {
            if (SetProperty(ref _loyaltyPerPointText, value))
            {
                RaiseLoyaltySummary();
            }
        }
    }

    /// <summary>Points at which a Normal customer becomes VIP (the fifth star).</summary>
    public string LoyaltyVipText
    {
        get => _loyaltyVipText;
        set
        {
            if (SetProperty(ref _loyaltyVipText, value))
            {
                RaiseLoyaltySummary();
            }
        }
    }

    /// <summary>"1 point per Rs 100 · 5 stars at 500 points" - the rule in one line.</summary>
    public string LoyaltySummary
    {
        get => _loyaltySummary;
        private set => SetProperty(ref _loyaltySummary, value);
    }

    /// <summary>The saved bands in one sentence, so the owner can check the meaning of the numbers.</summary>
    public string ExpirySummary
    {
        get => _expirySummary;
        private set => SetProperty(ref _expirySummary, value);
    }

    /// <summary>Rebuilds the one-line rule without touching the database, so a typo never blocks typing.</summary>
    private void RaiseLoyaltySummary()
    {
        var per = int.TryParse(LoyaltyPerPointText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : 100;

        var vip = int.TryParse(LoyaltyVipText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var points) && points > 0
            ? points
            : 500;

        LoyaltySummary = (LoyaltyEnabled ? "Points are ON. " : "Points are OFF. ")
            + "1 point per Rs " + per + " spent; " + vip + " points = 5 stars = VIP. "
            + "One point is worth Rs 1 when the customer spends it.";
    }

    // ------------------------------ Staff -------------------------------

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (!SetProperty(ref _selectedUser, value))
            {
                return;
            }

            LoadPermissions();
            OnPropertyChanged(nameof(SelectedUserCaption));
            OnPropertyChanged(nameof(CanChangeSelectedUser));
        }
    }

    public string SelectedUserCaption
    {
        get
        {
            var person = _selectedUser;
            return person is null
                ? "Select a person to see their permissions."
                : $"{person.Name} — {person.RoleLabel}";
        }
    }

    /// <summary>The owner account always has everything, so its boxes are shown ticked and locked.</summary>
    public bool CanChangeSelectedUser => _selectedUser is { IsOwner: false } && CanManageStaff;

    public string NewStaffName
    {
        get => _newStaffName;
        set => SetProperty(ref _newStaffName, value);
    }

    public string NewStaffPin
    {
        get => _newStaffPin;
        set => SetProperty(ref _newStaffPin, value);
    }

    public string SelectedRole
    {
        get => _selectedRole;
        set => SetProperty(ref _selectedRole, value);
    }

    // ----------------------------- Loading ------------------------------

    private void Load()
    {
        var settings = _services.Settings;

        StoreName = settings.GetString("store.name");
        StoreAddress = settings.GetString("store.address");
        StorePhone = settings.GetString("store.phone");
        FooterNote = settings.GetString("store.footerNote");
        AppName = settings.GetString("brand.appName");
        MenuPinnedOpen = settings.GetBool("menu.pinnedOpen");

        var idleHours = settings.GetInt("session.idleHours", 2);
        SelectedIdleHour = IdleHourOptions.FirstOrDefault(option => option.Hours == idleHours)
                           ?? IdleHourOptions[2];

        // The clock line (v0.35.0, D-92). It says where the time comes from and how far the two
        // clocks were apart when Area11 last looked - a shop whose PC is a day out must see it here.
        ClockText = TimeSource.DescribeForOwner();

        PrinterName = settings.GetString("printer.name");
        AutoPrint = settings.GetBool("printer.autoPrint", true);
        AutoCut = settings.GetBool("printer.autoCut", true);
        CopiesText = settings.GetLong("printer.copies", 1).ToString(CultureInfo.InvariantCulture);
        ShowDiscount = settings.GetBool("receipt.showDiscount", true);

        var width = settings.GetInt("printer.width", 58);
        SelectedPaper = PaperOptions.FirstOrDefault(option => option.Width == width) ?? PaperOptions[0];

        CashierLimitText = Text(settings.GetDouble("discount.maxPercentCashier", 5));
        ManagerLimitText = Text(settings.GetDouble("discount.maxPercentManager", 20));
        BlockBelowCost = settings.GetBool("discount.blockBelowCost", true);
        TaxEnabled = settings.GetBool("tax.enabled");
        TaxPercentText = Text(settings.GetDouble("tax.percent"));
        AllowNegativeStock = settings.GetBool("stock.allowNegative", true);

        var bands = settings.ExpiryBands;
        Expiry1Text = bands.Level1Days.ToString(CultureInfo.InvariantCulture);
        Expiry2Text = bands.Level2Days.ToString(CultureInfo.InvariantCulture);
        Expiry3Text = bands.Level3Days.ToString(CultureInfo.InvariantCulture);
        ExpirySummary = bands.Summary;

        SupplierCutoffText = settings.GetLong("supplier.returnCutoffDays", 90).ToString(CultureInfo.InvariantCulture);
        SupplierAlertText = settings.GetLong("supplier.returnAlertDays", 180).ToString(CultureInfo.InvariantCulture);

        LoyaltyEnabled = settings.GetBool("loyalty.enabled", false);
        LoyaltyPerPointText = settings.GetLong("loyalty.rupeesPerPoint", 100).ToString(CultureInfo.InvariantCulture);
        LoyaltyVipText = settings.GetLong("loyalty.vipThreshold", 500).ToString(CultureInfo.InvariantCulture);
        RaiseLoyaltySummary();

        // Cloud sync (F-48): the key is the one value that is shown as a masked label, never as text.
        SyncEnabled = settings.GetBool(SyncService.EnabledKey);
        SyncUrl = settings.GetString(SyncService.UrlKey);
        SyncTable = settings.GetString(SyncService.TableKey);
        SyncShopId = settings.GetString(SyncService.ShopIdKey);
        SyncKey = string.Empty;
        _syncKeyCleared = false;
        RaiseSyncLabels();
    }

    /// <summary>
    /// Called every time the page is opened, so a value changed elsewhere (the printing switch on
    /// the counter screen writes the same key) is shown here too.
    /// </summary>
    /// <summary>
    /// Which tab is open: 0 Shop, 1 Printing, 2 Sales rules, 3 Cloud sync, 4 Staff &amp; permissions.
    /// Bound two-way, so the shop name in the sidebar can open Shop and a person's name can open the
    /// staff tab (D-50) - the two questions the owner actually clicks.
    /// </summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, Math.Clamp(value, 0, 4));
    }

    /// <summary>
    /// Opens a tab by number (the frame's top-bar buttons call this).
    ///
    /// It does **not** reload the page: the menu already refreshes Settings whenever it is opened, and
    /// reloading here would throw away half-typed values when somebody clicks their own name while
    /// already on this page. Moving a TabControl must never cost the operator their work.
    /// </summary>
    public void ShowTab(int index) => SelectedTabIndex = index;

    public void Refresh()
    {
        Load();
        LoadUsers();
    }

    public void LoadUsers()
    {
        var currentId = _selectedUser?.Id;
        var result = _services.Auth.ListUsers();

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The staff list could not be loaded.", true);
            return;
        }

        Users.Clear();
        foreach (var person in result.Value)
        {
            Users.Add(person);
        }

        _selectedUser = Users.FirstOrDefault(person => person.Id == currentId) ?? Users.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedUser));
        OnPropertyChanged(nameof(SelectedUserCaption));
        OnPropertyChanged(nameof(CanChangeSelectedUser));

        LoadPermissions();
    }

    private void LoadPermissions()
    {
        Permissions.Clear();

        if (_selectedUser is null)
        {
            return;
        }

        var map = _services.Permissions.MapFor(_selectedUser);
        var editable = CanChangeSelectedUser;

        _suppressPermissionSave = true;

        foreach (var module in Modules.All)
        {
            var toggle = new PermissionToggle(module, Modules.Label(module), map[module], editable)
            {
                Allowed = map[module]
            };

            toggle.Changed += OnPermissionChanged;
            Permissions.Add(toggle);
        }

        _suppressPermissionSave = false;
    }

    // ----------------------------- Saving -------------------------------

    private void Save()
    {
        if (!_services.Permissions.Can(_user, Modules.Settings))
        {
            SetStatus("You are not allowed to change the settings.", true);
            return;
        }

        var paper = SelectedPaper;
        if (paper is null)
        {
            SetStatus("Choose the paper width.", true);
            return;
        }

        if (!int.TryParse(CopiesText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var copies))
        {
            SetStatus("Copies must be a whole number, for example 1.", true);
            return;
        }

        if (!TryNumber(CashierLimitText, out var cashierLimit) || !TryNumber(ManagerLimitText, out var managerLimit))
        {
            SetStatus("Discount limits must be numbers, for example 5 or 7.5.", true);
            return;
        }

        if (!TryNumber(TaxPercentText, out var taxPercent))
        {
            SetStatus("The tax rate must be a number, for example 17.", true);
            return;
        }

        if (!int.TryParse(Expiry1Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiry1)
            || !int.TryParse(Expiry2Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiry2)
            || !int.TryParse(Expiry3Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiry3))
        {
            SetStatus("The expiry levels must be whole days, for example 365, 180, 90.", true);
            return;
        }

        if (!ExpiryLevels.IsOrdered(expiry1, expiry2, expiry3))
        {
            SetStatus("Level 3 must be the smallest number of days, then Level 2, then Level 1 - all different.",
                true);
            return;
        }

        if (!int.TryParse(SupplierCutoffText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cutoffDays)
            || !int.TryParse(SupplierAlertText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var alertDays))
        {
            SetStatus("The supplier return window must be whole days, for example 180 and 90.", true);
            return;
        }

        if (cutoffDays < 1 || alertDays <= cutoffDays)
        {
            SetStatus("The reminder must start earlier than the supplier's cutoff - for example 180 and 90 days.", true);
            return;
        }

        if (!int.TryParse(LoyaltyPerPointText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var perPoint)
            || perPoint < 1)
        {
            SetStatus("The loyalty rate must be whole rupees, at least 1.", true);
            return;
        }

        if (!int.TryParse(LoyaltyVipText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var vipPoints)
            || vipPoints < 5)
        {
            SetStatus("The VIP number must be at least 5 points (one per star).", true);
            return;
        }

        if (StoreName.Trim().Length == 0)
        {
            SetStatus("The shop name cannot be empty — it is printed on every receipt.", true);
            return;
        }

        // A switch that is ON with half a form behind it would fail on the Cloud sync page with a
        // sentence the owner cannot act on, so the page refuses the combination here (D-66, v0.26.0).
        var keyWillExist = !_syncKeyCleared && (_services.Cloud.HasKey || SyncKey.Trim().Length > 0);

        if (CanChangeCloud && SyncEnabled
            && (SyncUrl.Trim().Length == 0 || SyncTable.Trim().Length == 0
                || SyncShopId.Trim().Length == 0 || !keyWillExist))
        {
            SetStatus("Cloud sync is switched on, but the address, table, shop id and key must all be filled. " +
                      "Fill them, or switch cloud sync off.", true);
            return;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["store.name"] = StoreName.Trim(),
            ["store.address"] = StoreAddress.Trim(),
            ["store.phone"] = StorePhone.Trim(),
            ["store.footerNote"] = FooterNote.Trim(),
            ["brand.appName"] = AppName.Trim().Length == 0 ? "Area11" : AppName.Trim(),
            ["menu.pinnedOpen"] = MenuPinnedOpen,

            ["printer.name"] = PrinterName.Trim(),
            ["printer.width"] = paper.Width,
            ["printer.autoPrint"] = AutoPrint,
            ["printer.autoCut"] = AutoCut,
            ["printer.copies"] = copies,
            ["receipt.showDiscount"] = ShowDiscount,

            ["discount.maxPercentCashier"] = cashierLimit,
            ["discount.maxPercentManager"] = managerLimit,
            ["discount.blockBelowCost"] = BlockBelowCost,
            ["tax.enabled"] = TaxEnabled,
            ["tax.percent"] = taxPercent,
            ["stock.allowNegative"] = AllowNegativeStock,
            ["expiry.levels"] = _services.Settings.ExpiryBands
                .WithDays(expiry1, expiry2, expiry3)
                .Bands
                .ToList(),
            ["supplier.returnCutoffDays"] = cutoffDays,
            ["supplier.returnAlertDays"] = alertDays,
            ["loyalty.enabled"] = LoyaltyEnabled,
            ["loyalty.rupeesPerPoint"] = perPoint,
            ["loyalty.vipThreshold"] = vipPoints,
            ["session.idleHours"] = SelectedIdleHour?.Hours ?? 2
        };

        // Only a person the owner ticked for cloud sync writes these four keys: the key is a password
        // to the whole shop's mirror, and a manager's Settings tab must not be able to replace it.
        if (CanChangeCloud)
        {
            values[SyncService.EnabledKey] = SyncEnabled;
            values[SyncService.UrlKey] = SyncUrl.Trim();
            values[SyncService.TableKey] = SyncTable.Trim();
            values[SyncService.ShopIdKey] = SyncShopId.Trim();

            if (_syncKeyCleared)
            {
                values[SyncService.KeyKey] = string.Empty;
            }
            else if (SyncKey.Trim().Length > 0)
            {
                values[SyncService.KeyKey] = SyncKey.Trim();
            }
        }

        var result = _services.Settings.SetMany(values, _user);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The settings could not be saved.", true);
            return;
        }

        // The summary line is rebuilt from what was just stored, so the owner sees the meaning of
        // the three numbers he typed without leaving the page.
        ExpirySummary = _services.Settings.ExpiryBands.Summary;
        RaiseLoyaltySummary();

        // The key box is emptied at once: a key left on the screen is one a person walking past can read.
        SyncKey = string.Empty;
        _syncKeyCleared = false;
        RaiseSyncLabels();

        _onSaved?.Invoke();

        // The cloud sentence is added only when the owner could have changed it, so a cashier's save
        // still says exactly what it saved.
        var cloudNote = !CanChangeCloud
            ? string.Empty
            : !SyncEnabled
                ? " Cloud sync is switched off."
                : _services.Cloud.IsConfigured
                    ? " Cloud sync is ready."
                    : " Cloud sync is switched on but still incomplete.";

        SetStatus($"Saved. Paper {paper.Width} mm, printing " +
                  (AutoPrint ? "automatic." : "manual (use the button on the counter screen).") +
                  cloudNote, false);
    }

    // ---------------------------- Cloud sync ----------------------------

    /// <summary>Only someone the owner ticked for cloud sync may see or change these four boxes.</summary>
    public bool CanChangeCloud => _services.Cloud.CanSync(_user);

    public bool SyncEnabled
    {
        get => _syncEnabled;
        set
        {
            if (SetProperty(ref _syncEnabled, value))
            {
                RaiseSyncSummary();
            }
        }
    }

    public string SyncUrl
    {
        get => _syncUrl;
        set => SetProperty(ref _syncUrl, value);
    }

    public string SyncTable
    {
        get => _syncTable;
        set => SetProperty(ref _syncTable, value);
    }

    public string SyncShopId
    {
        get => _syncShopId;
        set => SetProperty(ref _syncShopId, value);
    }

    /// <summary>
    /// What the owner types when he wants to replace the key. It is deliberately **not** loaded from the
    /// database: a password that appears on a screen is a password that ends up in a photograph. An
    /// empty box means "keep the saved key".
    /// </summary>
    public string SyncKey
    {
        get => _syncKey;
        set => SetProperty(ref _syncKey, value);
    }

    /// <summary>"A key is saved (eyJh********9f2c)." - proof there is one, without showing it.</summary>
    public string SyncKeyLabel
    {
        get => _syncKeyLabel;
        private set => SetProperty(ref _syncKeyLabel, value);
    }

    /// <summary>One sentence: is the cloud set up, and what does the owner do next?</summary>
    public string SyncSummary
    {
        get => _syncSummary;
        private set => SetProperty(ref _syncSummary, value);
    }

    private void ClearSyncKey()
    {
        if (!CanChangeCloud)
        {
            SetStatus("Only a person the owner ticked for cloud sync can change the key.", true);
            return;
        }

        _syncKeyCleared = true;
        SyncKey = string.Empty;
        SyncKeyLabel = "The saved key will be removed when you press Save settings.";
    }

    private void RaiseSyncLabels()
    {
        SyncKeyLabel = _services.Cloud.HasKey
            ? "A key is saved (" + _services.Cloud.KeyLabel + "). Leave the box empty to keep it."
            : "No key is saved yet.";

        RaiseSyncSummary();
    }

    private void RaiseSyncSummary()
    {
        if (!CanChangeCloud)
        {
            SyncSummary = "Only a person the owner ticked for cloud sync can change these.";
            return;
        }

        if (!SyncEnabled)
        {
            SyncSummary = "Cloud sync is off - nothing is ever sent. The rest of Area11 works without it.";
            return;
        }

        SyncSummary = _services.Cloud.IsConfigured
            ? "Ready. Open SYSTEM -> Cloud sync to push the shop's data, or to read the cloud copy."
            : "Switched on but not complete - fill the address, the key, the table and the shop id, then press Save settings.";
    }

    // ------------------------------ Staff -------------------------------

    private void AddStaff()
    {
        if (!CanManageStaff)
        {
            SetStatus("Only the owner can add staff.", true);
            return;
        }

        var result = _services.Auth.CreateStaff(_user, NewStaffName, UserRoles.Parse(SelectedRole), NewStaffPin);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The staff account could not be saved.", true);
            return;
        }

        var name = NewStaffName.Trim();
        NewStaffName = string.Empty;
        NewStaffPin = string.Empty;
        LoadUsers();

        SetStatus($"{name} can now sign in with that PIN. The role decides what they see.", false);
    }

    private void ToggleActive()
    {
        if (_selectedUser is null)
        {
            SetStatus("Select a person first.", true);
            return;
        }

        if (!CanManageStaff)
        {
            SetStatus("Only the owner can switch an account on or off.", true);
            return;
        }

        var person = _selectedUser;
        var result = _services.Auth.SetActive(_user, person.Id, !person.Active);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The account could not be changed.", true);
            return;
        }

        LoadUsers();
        SetStatus(person.Active
            ? $"{person.Name} has been switched off and can no longer sign in."
            : $"{person.Name} can sign in again.", false);
    }

    private void ResetPermissions()
    {
        if (_selectedUser is null)
        {
            SetStatus("Select a person first.", true);
            return;
        }

        var result = _services.Permissions.ResetToRoleDefaults(_user, _selectedUser.Id);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The permissions could not be reset.", true);
            return;
        }

        LoadPermissions();
        SetStatus($"{_selectedUser.Name} is back to the normal {_selectedUser.RoleLabel} permissions.", false);
    }

    private void OnPermissionChanged(object? sender, EventArgs e)
    {
        if (_suppressPermissionSave || sender is not PermissionToggle toggle || _selectedUser is null)
        {
            return;
        }

        var result = _services.Permissions.Set(_user, _selectedUser.Id, toggle.Module, toggle.Allowed);

        if (result.Ok)
        {
            SetStatus($"{_selectedUser.Name}: '{toggle.Label}' is now " +
                      (toggle.Allowed ? "allowed." : "not allowed."), false);
            return;
        }

        // Put the box back where it was, otherwise the screen would tell a lie.
        _suppressPermissionSave = true;
        toggle.Allowed = !toggle.Allowed;
        _suppressPermissionSave = false;

        SetStatus(result.Error ?? "The permission could not be saved.", true);
    }

    // ----------------------------- Helpers ------------------------------

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }

    private static string Text(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static bool TryNumber(string? raw, out double value) =>
        double.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}

/// <summary>One paper size in the drop-down. ToString() is what the ComboBox shows.</summary>
/// <summary>One sign-out choice on the Settings page (v0.35.0, D-92).</summary>
public sealed class IdleHourOption
{
    public IdleHourOption(int hours, string label)
    {
        Hours = hours;
        Label = label;
    }

    public int Hours { get; }

    public string Label { get; }

    public override string ToString() => Label;
}

public sealed class PaperOption
{
    public PaperOption(int width, string label)
    {
        Width = width;
        Label = label;
    }

    public int Width { get; }

    public string Label { get; }

    public override string ToString() => Label;
}

/// <summary>
/// One permission checkbox. Ticking it saves immediately (PermissionService audits the change),
/// so the owner does not have to hunt for a second Save button.
/// </summary>
public sealed class PermissionToggle : ObservableObject
{
    private bool _allowed;

    public PermissionToggle(string module, string label, bool allowed, bool canEdit)
    {
        Module = module;
        Label = label;
        _allowed = allowed;
        CanEdit = canEdit;
    }

    public event EventHandler? Changed;

    public string Module { get; }

    public string Label { get; }

    public bool CanEdit { get; }

    public bool Allowed
    {
        get => _allowed;
        set
        {
            if (SetProperty(ref _allowed, value))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
