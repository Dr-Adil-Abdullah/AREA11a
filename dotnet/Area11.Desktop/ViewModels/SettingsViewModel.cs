using System.Collections.ObjectModel;
using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Services;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Settings (F-17) — the screen the owner asked for when he said printing must be a choice
/// ("in setting there is an option for auto or manu[a]l printing", D-26).
///
/// Four tabs, one save button:
///   • Shop      — name, address, phone, the note at the bottom of the receipt.
///   • Printing  — printer, 58 / 80 mm, automatic or manual, cut, copies.
///   • Sales     — discount limits, tax, negative stock.
///   • Staff     — people, their role and one checkbox per module (D-03).
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

        PaperOptions = new ObservableCollection<PaperOption>
        {
            new(58, "58 mm  —  32 characters per line (small counter printer)"),
            new(80, "80 mm  —  48 characters per line (wide printer)")
        };

        Load();
        LoadUsers();
    }

    public ObservableCollection<PaperOption> PaperOptions { get; }

    public ObservableCollection<User> Users { get; } = new();

    public ObservableCollection<PermissionToggle> Permissions { get; } = new();

    public IReadOnlyList<string> StaffRoles { get; } = new[] { "Manager", "Salesman", "Cashier" };

    public RelayCommand SaveCommand { get; }

    public RelayCommand AddStaffCommand { get; }

    public RelayCommand ResetPermissionsCommand { get; }

    public RelayCommand ToggleActiveCommand { get; }

    public RelayCommand RefreshStaffCommand { get; }

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

    /// <summary>The saved bands in one sentence, so the owner can check the meaning of the numbers.</summary>
    public string ExpirySummary
    {
        get => _expirySummary;
        private set => SetProperty(ref _expirySummary, value);
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
    }

    /// <summary>
    /// Called every time the page is opened, so a value changed elsewhere (the printing switch on
    /// the counter screen writes the same key) is shown here too.
    /// </summary>
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

        if (StoreName.Trim().Length == 0)
        {
            SetStatus("The shop name cannot be empty — it is printed on every receipt.", true);
            return;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["store.name"] = StoreName.Trim(),
            ["store.address"] = StoreAddress.Trim(),
            ["store.phone"] = StorePhone.Trim(),
            ["store.footerNote"] = FooterNote.Trim(),
            ["brand.appName"] = AppName.Trim().Length == 0 ? "Area11" : AppName.Trim(),

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
                .ToList()
        };

        var result = _services.Settings.SetMany(values, _user);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The settings could not be saved.", true);
            return;
        }

        // The summary line is rebuilt from what was just stored, so the owner sees the meaning of
        // the three numbers he typed without leaving the page.
        ExpirySummary = _services.Settings.ExpiryBands.Summary;

        _onSaved?.Invoke();
        SetStatus($"Saved. Paper {paper.Width} mm, printing " +
                  (AutoPrint ? "automatic." : "manual (use the button on the counter screen)."), false);
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
