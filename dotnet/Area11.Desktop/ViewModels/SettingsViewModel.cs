using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Win32;
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
/// Six shell-selected sections, one save button:
///   • Shop        — name, address, phone, the note at the bottom of the receipt.
///   • Printing    — printer, 58 / 80 mm, automatic or manual, cut, copies.
///   • Sales       — discount limits, tax, and the D-93 non-negative stock rule.
///   • Cloud sync  — the Supabase address, key, table and shop id (F-48, Spec 25.3).
///   • Staff       — people, their role and one checkbox per module (D-03).
///   • App words   — the shop's names for application sections (E-12/S-05).
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
    private string _appDisplayName = "Area11";
    private string _productPhotoSize = "medium";
    private string _storeAddress = string.Empty;
    private string _storePhone = string.Empty;
    private string _footerNote = string.Empty;

    private string _printerName = string.Empty;
    private PaperOption? _selectedPaper;
    private bool _autoPrint;
    private bool _autoCut;
    private bool _defaultBulk;
    private BulkFormatOption? _selectedBulkFormat;
    private bool _whatsAppPreparePdf;
    private string _whatsAppExportDirectory = string.Empty;
    private string _copiesText = "1";
    private bool _showDiscount;
    private bool _saleSuccessPopup;

    private string _cashierLimitText = "5";
    private string _managerLimitText = "20";
    private bool _blockBelowCost;
    private bool _blockOverLimit;
    private bool _showMargins;
    private bool _taxEnabled;
    private string _taxPercentText = "0";

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
    private StartupModeOption? _selectedStartupMode;
    private bool _focusGuardEnabled;
    private bool _savingSettings;
    private readonly PostingAttemptGuard _startupAttempt = new();
    private string _startupSaveHint = string.Empty;

    private bool _syncEnabled;
    private string _syncUrl = string.Empty;
    private string _syncTable = "area11_sync";
    private string _syncShopId = string.Empty;
    private bool _syncAutomatic;
    private string _syncAutomaticMinutesText = "15";
    private string _syncKey = string.Empty;
    private string _syncKeyLabel = string.Empty;
    private string _syncSummary = string.Empty;
    private bool _syncKeyCleared;

    private int _selectedTabIndex;
    private IdleHourOption? _selectedIdleHour;
    private string _clockText = string.Empty;
    private User? _selectedUser;
    private string _billDiscountBasis = "profit";
    private bool _pricingExtra;
    private string _pricingExtraLimit = "0";
    private bool _pricingEditRate, _pricingProfit, _pricingRetail, _pricingOverride;
    private string _pricingManualLimit = "0", _pricingProfitLimit = "0", _pricingRetailLimit = "0";

    private bool _staffAccessAllowed;
    private string _newStaffName = string.Empty;
    private string _newStaffPin = string.Empty;
    private string _resetStaffSecret = string.Empty;
    private string _newRecoveryWord = string.Empty;
    private string _selectedRole = "Cashier";

    private string _statusText = "Changes are saved when you press Save.";
    private bool _isStatusError;
    private bool _suppressPermissionSave;
    private bool _confirmLabelReset;
    private SettingsEditForm? _editForm;
    private bool _changingIcon;
    private EditVersion? _pricingVersion;
    private PermissionEditForm? _permissionEdit;
    private bool _savingPermission;
    private bool _savingStaff;
    private readonly PostingAttemptGuard _staffCreate = new();
    private int _createdStaffId;
    private RecoveryEditForm? _recoveryEdit;
    private static readonly string[] FormKeys =
    {
        "appearance.productPhotoSize",
        IconAssetService.Key,
        "store.name",
        "brand.appName",
        "store.address",
        "store.phone",
        "store.footerNote",
        "session.idleHours",
        "startup.mode",
        "lockdown.focusGuard",
        "printer.name",
        "printer.autoPrint",
        "printer.autoCut",
        "printer.defaultBulk",
        "whatsapp.preparePdf",
        "whatsapp.exportDirectory",
        "printer.bulkFormat",
        "printer.copies",
        "receipt.showDiscount",
        "sale.successPopup",
        "printer.width",
        "discount.maxPercentCashier",
        "discount.maxPercentManager",
        "discount.blockBelowCost",
        "discount.billBasis",
        "credit.blockOverLimit",
        "profit.showMargins",
        "tax.enabled",
        "tax.percent",
        "expiry.levels",
        "supplier.returnCutoffDays",
        "supplier.returnAlertDays",
        "loyalty.enabled",
        "loyalty.rupeesPerPoint",
        "loyalty.vipThreshold",
    };
    private static readonly string[] CloudFormKeys = { "sync.enabled", "sync.url", "sync.table", "sync.shopId", "sync.automaticEnabled", "sync.automaticMinutes", "sync.key" };

    private bool _settingsAccessAllowed;
    private bool _cloudAccessAllowed;

    public SettingsViewModel(AppServices services, User user, Action? onSaved = null)
    {
        _services = services;
        _user = user;
        _onSaved = onSaved;
        NativeFields = new NativeFieldsViewModel(services, user);
        ExtraFields = new CustomFieldsViewModel(services, user);

        SaveCommand = new RelayCommand(Save);
        SavePricingCommand = new RelayCommand(SavePricing);
        AddStaffCommand = new RelayCommand(AddStaff);
        ResetPermissionsCommand = new RelayCommand(ResetPermissions);
        ResetLabelsCommand = new RelayCommand(ResetLabels);
        ToggleActiveCommand = new RelayCommand(ToggleActive);
        ResetSecretCommand = new RelayCommand(ResetSecret);
        SaveRecoveryWordCommand = new RelayCommand(SaveRecoveryWord);
        RefreshStaffCommand = new RelayCommand(LoadUsers);
        StaffPhotosCommand = new RelayCommand(OpenStaffPhotos);
        ChooseAppIconCommand = new RelayCommand(ChooseAppIcon);
        ResetAppIconCommand = new RelayCommand(ResetAppIcon);
        ClearSyncKeyCommand = new RelayCommand(ClearSyncKey);

        PaperOptions = new ObservableCollection<PaperOption>
        {
            new(58, "58 mm  —  32 characters per line (small counter printer)"),
            new(80, "80 mm  —  48 characters per line (wide printer)")
        };

        BulkFormatOptions = new ObservableCollection<BulkFormatOption>
        {
            new("separate_receipts", "Separate receipts — one slip per bill, each cut on its own"),
            new("a4_list", "One A4 list — the summary page only, for the file")
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

        StartupModeOptions = new ObservableCollection<StartupModeOption>
        {
            new(StartupService.Manual, "By hand - start from the icon, close with ×"),
            new(StartupService.Auto, "Start with Windows - × still closes it"),
            new(StartupService.Protected, "Start with Windows - × asks for the password every time")
        };

        Load();
        LoadUsers();
    }

    public NativeFieldsViewModel NativeFields { get; }

    public CustomFieldsViewModel ExtraFields { get; }

    public ObservableCollection<PaperOption> PaperOptions { get; }

    /// <summary>Portion 11: the two shapes a bulk print can take.</summary>
    public ObservableCollection<BulkFormatOption> BulkFormatOptions { get; }

    /// <summary>The sign-out choices (v0.35.0, D-92). 12 hours is also the ceiling for every choice.</summary>
    public ObservableCollection<IdleHourOption> IdleHourOptions { get; }

    /// <summary>Portion 16: the three ways Area11 may start and close. Owner only.</summary>
    public ObservableCollection<StartupModeOption> StartupModeOptions { get; }

    /// <summary>
    /// Lockdown chrome (v0.67.0): raised from Save() when the guard tick was actually written, so
    /// the frame can drop the minimize box and the taskbar (or bring them back) at once. The shell
    /// subscribes through ShellViewModel - a page never touches the window.
    /// </summary>
    public event Action? LockdownChromeChanged;

    /// <summary>Screen lockdown (owner, Nov 2026): warn-and-pull-back plus black-box episodes.</summary>
    public bool FocusGuardEnabled
    {
        get => _focusGuardEnabled;
        set => SetProperty(ref _focusGuardEnabled, value);
    }

    public StartupModeOption? SelectedStartupMode
    {
        get => _selectedStartupMode;
        set => SetProperty(ref _selectedStartupMode, value);
    }

    /// <summary>Portion 16: only the owner picks how the program starts and closes.</summary>
    public bool CanChangeStartup => CanUseSettings && _staffAccessAllowed;

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

    public ObservableCollection<AppLabelEditRow> LabelRows { get; } = new();

    public ObservableCollection<PermissionToggle> Permissions { get; } = new();
    public ObservableCollection<PermissionToggle> PagePermissions { get; } = new();
    public ObservableCollection<PermissionToggle> FinancialPermissions { get; } = new();

    public IReadOnlyList<string> StaffRoles { get; } = new[] { "Manager", "Salesman", "Cashier" };

    public RelayCommand SaveCommand { get; }

    public RelayCommand AddStaffCommand { get; }

    public RelayCommand ResetPermissionsCommand { get; }

    public RelayCommand ResetLabelsCommand { get; }

    public string ResetLabelsText => _confirmLabelReset ? "Click again to restore all" : "Restore original words";

    public RelayCommand ToggleActiveCommand { get; }

    public RelayCommand ResetSecretCommand { get; }

    public RelayCommand SaveRecoveryWordCommand { get; }

    public RelayCommand RefreshStaffCommand { get; }

    public RelayCommand StaffPhotosCommand { get; }

    public RelayCommand ChooseAppIconCommand { get; }

    public RelayCommand ResetAppIconCommand { get; }

    public bool CanChangeAppIcon => CanUseSettings && _staffAccessAllowed;

    private void ChooseAppIcon() => ChangeAppIcon(reset: false);

    private void ResetAppIcon() => ChangeAppIcon(reset: true);

    private void ChangeAppIcon(bool reset)
    {
        if (_changingIcon) return;
        if (!UpdateSettingsAccess() || !UpdateStaffAccess()) return;
        if (!CanChangeAppIcon) { SetStatus("Only the owner can change the icon.", true); return; }
        var form = _editForm;
        if (form is null) { SetStatus("Refresh Settings before changing the icon.", true); return; }
        _changingIcon = true;
        var acknowledged = false;
        try
        {
            string? source = null;
            if (!reset)
            {
                var dialog = new OpenFileDialog { Title = "Choose application icon", Filter = "Windows icon (*.ico)|*.ico", CheckFileExists = true };
                if (dialog.ShowDialog() != true) return;
                source = dialog.FileName;
            }
            // Keep the PRE-dialog form. A modal must not bless an intervening icon change.
            var result = reset ? IconAssets.Reset(_services, _user, form)
                : IconAssets.Choose(_services, _user, source, form);
            if (!result.Ok)
            {
                if (!result.NoCommitConfirmed) _editForm = null;
                SetStatus(result.Error ?? "Icon change failed.", true);
                return;
            }
            acknowledged = true;
            _editForm = result.SettingsEdit;
            var message = reset ? "The default window icon is restored."
                : "Window icon changed. Existing Windows shortcuts keep their own icon.";
            SetStatus(result.Warnings.Count > 0 ? string.Join(" ", result.Warnings) : message, result.Warnings.Count > 0);
        }
        catch (Exception exception)
        {
            _editForm = null;
            _services.Log.Error("Icon save/display did not finish.", exception);
            SetStatus(acknowledged ? "SAVED. The icon was stored but the page could not refresh. Reopen Settings."
                : "Icon saving is unconfirmed. Refresh and verify before retrying.", true);
        }
        finally { _changingIcon = false; }
    }

    /// <summary>Takes the saved key off this computer (it takes effect when Save is pressed).</summary>
    public RelayCommand ClearSyncKeyCommand { get; }

    public RelayCommand SavePricingCommand { get; }
    public bool IsOwner => _staffAccessAllowed;
    public IReadOnlyList<string> DiscountBases { get; } = new[] { "profit", "retail" };
    public string BillDiscountBasis { get => _billDiscountBasis; set => SetProperty(ref _billDiscountBasis, value); }
    public bool PricingExtra { get => _pricingExtra; set => SetProperty(ref _pricingExtra, value); }
    public string PricingExtraLimit { get => _pricingExtraLimit; set => SetProperty(ref _pricingExtraLimit, value); }
    public bool PricingEditRate { get => _pricingEditRate; set => SetProperty(ref _pricingEditRate, value); }
    public bool PricingProfit { get => _pricingProfit; set => SetProperty(ref _pricingProfit, value); }
    public bool PricingRetail { get => _pricingRetail; set => SetProperty(ref _pricingRetail, value); }
    public bool PricingOverride { get => _pricingOverride; set => SetProperty(ref _pricingOverride, value); }
    public string PricingManualLimit { get => _pricingManualLimit; set => SetProperty(ref _pricingManualLimit, value); }
    public string PricingProfitLimit { get => _pricingProfitLimit; set => SetProperty(ref _pricingProfitLimit, value); }
    public string PricingRetailLimit { get => _pricingRetailLimit; set => SetProperty(ref _pricingRetailLimit, value); }

    private void LoadPricing()
    {
        ClearPricing();
        if (_selectedUser is null || !UpdateStaffAccess()) return;
        try
        {
            var allowance = _services.Pricing.ForUser(_user, _selectedUser);
            _pricingVersion = allowance.EditVersion;
            PricingEditRate = allowance.EditRate;
            PricingManualLimit = allowance.ManualReductionPercent.ToString("0.##", CultureInfo.InvariantCulture);
            PricingProfit = allowance.ProfitDiscount;
            PricingProfitLimit = allowance.ProfitPercent.ToString("0.##", CultureInfo.InvariantCulture);
            PricingRetail = allowance.RetailDiscount;
            PricingRetailLimit = allowance.RetailPercent.ToString("0.##", CultureInfo.InvariantCulture);
            PricingOverride = allowance.OverrideBasis;
            PricingExtra = allowance.ExtraDiscount;
            PricingExtraLimit = Money.Edit(allowance.ExtraLimitPaisa);
        }
        catch (ValidationException exception) { SetStatus(exception.Message, true); }
    }

    private void SavePricing()
    {
        if (_savingStaff || _savingPermission) return;
        if (!UpdateStaffAccess() || _selectedUser is null) { SetStatus("Only the owner can set a person's pricing limits.", true); return; }
        if (_pricingVersion is null) { SetStatus("Reload the selected person's pricing limits before saving.", true); return; }
        if (!decimal.TryParse(PricingManualLimit, NumberStyles.Number, CultureInfo.InvariantCulture, out var manual)
            || !decimal.TryParse(PricingProfitLimit, NumberStyles.Number, CultureInfo.InvariantCulture, out var profit)
            || !decimal.TryParse(PricingRetailLimit, NumberStyles.Number, CultureInfo.InvariantCulture, out var retail))
        { SetStatus("Enter numeric percentage limits between 0 and 100.", true); return; }
        if (!Money.TryParse(PricingExtraLimit, out var extra) || extra < 0 || extra > long.MaxValue / 100m
            || decimal.Round(extra, 2) != extra)
        { SetStatus("Extra Discount limit must be a positive rupee amount (up to two decimals), or zero.", true); return; }
        var result = _services.Pricing.Save(_user, new PricingAllowance
        {
            UserId = _selectedUser.Id, EditRate = PricingEditRate, ManualReductionPercent = manual,
            ProfitDiscount = PricingProfit, ProfitPercent = profit, RetailDiscount = PricingRetail,
            RetailPercent = retail, OverrideBasis = PricingOverride,
            ExtraDiscount = PricingExtra, ExtraLimitPaisa = Money.ToPaisa(extra)
        }, _pricingVersion);
        if (result.Ok) _pricingVersion = result.EditVersion;
        else if (!result.NoCommitConfirmed) _pricingVersion = null;
        SetStatus(result.Ok ? "Pricing limits saved for " + _selectedUser.Name + "." : result.Error ?? "Could not save pricing limits.", !result.Ok);
    }

    public bool CanManageStaff => _staffAccessAllowed;

    public bool CanManageFinancialVisibility => _staffAccessAllowed;

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

    public IReadOnlyList<string> ProductPhotoSizes { get; } = new[] { "medium", "large" };

    public string ProductPhotoSize
    {
        get => _productPhotoSize;
        set => SetProperty(ref _productPhotoSize, value);
    }

    public string StoreName
    {
        get => _storeName;
        set => SetProperty(ref _storeName, value);
    }

    /// <summary>Owner's word (Nov 2026): his own name on every window, the sign-in screen and the taskbar.</summary>
    public string AppDisplayName
    {
        get => _appDisplayName;
        set => SetProperty(ref _appDisplayName, value);
    }

    public bool CanChangeAppName => CanUseSettings && _staffAccessAllowed;

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

    /// <summary>
    /// Portion 11: on, the Print button prints every ticked row; off, it prints the one open row
    /// and the bulk set stays behind its own "Print selected" button.
    /// </summary>
    public bool DefaultBulk
    {
        get => _defaultBulk;
        set => SetProperty(ref _defaultBulk, value);
    }

    public BulkFormatOption? SelectedBulkFormat
    {
        get => _selectedBulkFormat;
        set => SetProperty(ref _selectedBulkFormat, value);
    }

    /// <summary>Portion 12: on, every WhatsApp send also prepares a PDF file for attaching by hand.</summary>
    public bool WhatsAppPreparePdf
    {
        get => _whatsAppPreparePdf;
        set => SetProperty(ref _whatsAppPreparePdf, value);
    }

    /// <summary>Portion 12: where the prepared PDFs land; empty means the shop's exports folder.</summary>
    public string WhatsAppExportDirectory
    {
        get => _whatsAppExportDirectory;
        set => SetProperty(ref _whatsAppExportDirectory, value);
    }

    public string CopiesText
    {
        get => _copiesText;
        set => SetProperty(ref _copiesText, value);
    }

    /// <summary>
    /// Portion 13: the global profit switch. Off hides every margin - dashboard, reports, customer
    /// pages, doctors, history - even from the owner, until it is switched back on.
    /// </summary>
    public bool ShowMargins
    {
        get => _showMargins;
        set => SetProperty(ref _showMargins, value);
    }

    public bool ShowDiscount
    {
        get => _showDiscount;
        set => SetProperty(ref _showDiscount, value);
    }

    // ------------------------------ Sales -------------------------------

    /// <summary>Optional post-commit checkmark/invoice/change card on the counter.</summary>
    public bool SaleSuccessPopup
    {
        get => _saleSuccessPopup;
        set => SetProperty(ref _saleSuccessPopup, value);
    }

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

    public bool BlockOverLimit
    {
        get => _blockOverLimit;
        set => SetProperty(ref _blockOverLimit, value);
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

    private void OpenStaffPhotos()
    {
        if (!UpdateStaffAccess()) return;
        if (!CanManageStaff || _selectedUser is not { Active: true } person)
        {
            SetStatus("Choose an active staff member first.", true);
            return;
        }
        PhotoWindow.Show(_services, _user, PhotoEntity.User, person.Id, person.Name);
        _onSaved?.Invoke(); // refresh the signed-in person's header after a photo is changed
    }

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (_savingPermission || _savingStaff) return;
            if (value is not null && (!UpdateStaffAccess() || !Users.Contains(value))) return;
            if (!ReferenceEquals(_selectedUser, value)) ResetStaffSecret = string.Empty;
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

    /// <summary>The new PIN or password the owner types for the selected person (cleared after save).</summary>
    public string ResetStaffSecret
    {
        get => _resetStaffSecret;
        set => SetProperty(ref _resetStaffSecret, value);
    }

    /// <summary>The owner's own "forgot password" word (cleared after save; empty means factory).</summary>
    public string NewRecoveryWord
    {
        get => _newRecoveryWord;
        set => SetProperty(ref _newRecoveryWord, value);
    }

    // ----------------------------- Loading ------------------------------

    private void LoadLabels()
    {
        _confirmLabelReset = false;
        OnPropertyChanged(nameof(ResetLabelsText));
        LabelRows.Clear();

        if (_editForm is null) return;
        foreach (var definition in AppLabelService.Definitions)
        {
            var text = _editForm.GetString(definition.SettingKey).Trim();
            if (AppLabelService.GuardSetting(definition.SettingKey, text) is not null) text = definition.DefaultText;
            LabelRows.Add(new AppLabelEditRow(definition.Key, definition.DefaultText, text));
        }
    }

    private void Load()
    {
        if (_changingIcon) return;
        if (_savingSettings) return;
        if (!UpdateSettingsAccess()) return;
        _editForm = null;
        var keys = FormKeys.Concat(AppLabelService.Definitions.Select(d => d.SettingKey))
            .Concat(CanChangeCloud ? CloudFormKeys : Array.Empty<string>());
        var loaded = _services.Settings.LoadEditForm(_user, keys);
        if (!loaded.Ok || loaded.Value is null)
        {
            LabelRows.Clear();
            SetStatus(loaded.Error ?? "Settings could not be loaded. Refresh before saving.", true);
            return;
        }
        var settings = loaded.Value;
        _editForm = settings;

        var photoSize = settings.GetString("appearance.productPhotoSize");
        ProductPhotoSize = photoSize is "large" ? "large" : "medium";
        StoreName = settings.GetString("store.name");
        AppDisplayName = settings.GetString("brand.appName");
        StoreAddress = settings.GetString("store.address");
        StorePhone = settings.GetString("store.phone");
        FooterNote = settings.GetString("store.footerNote");
        LoadLabels();

        var idleHours = settings.GetInt("session.idleHours", 2);
        SelectedIdleHour = IdleHourOptions.FirstOrDefault(option => option.Hours == idleHours)
                           ?? IdleHourOptions[2];

        var startupMode = settings.GetString(StartupService.ModeKey);
        SelectedStartupMode = StartupModeOptions.FirstOrDefault(option => option.Key == startupMode)
                              ?? StartupModeOptions[0];
        FocusGuardEnabled = settings.GetBool(FocusGuardService.GuardKey);

        // The clock line (v0.35.0, D-92). It says where the time comes from and how far the two
        // clocks were apart when Area11 last looked - a shop whose PC is a day out must see it here.
        ClockText = TimeSource.DescribeForOwner();

        PrinterName = settings.GetString("printer.name");
        AutoPrint = settings.GetBool("printer.autoPrint", true);
        AutoCut = settings.GetBool("printer.autoCut", true);
        DefaultBulk = settings.GetBool("printer.defaultBulk", false);
        WhatsAppPreparePdf = settings.GetBool("whatsapp.preparePdf", true);
        WhatsAppExportDirectory = settings.GetString("whatsapp.exportDirectory") ?? string.Empty;
        SelectedBulkFormat = BulkFormatOptions.FirstOrDefault(option =>
                string.Equals(option.Key, settings.GetString("printer.bulkFormat"), StringComparison.OrdinalIgnoreCase))
            ?? BulkFormatOptions[0];
        CopiesText = settings.GetLong("printer.copies", 1).ToString(CultureInfo.InvariantCulture);
        ShowDiscount = settings.GetBool("receipt.showDiscount", true);
        SaleSuccessPopup = settings.GetBool("sale.successPopup", true);

        var width = settings.GetInt("printer.width", 58);
        SelectedPaper = PaperOptions.FirstOrDefault(option => option.Width == width) ?? PaperOptions[0];

        CashierLimitText = Text(settings.GetDouble("discount.maxPercentCashier", 5));
        ManagerLimitText = Text(settings.GetDouble("discount.maxPercentManager", 20));
        BlockBelowCost = true;
        BillDiscountBasis = settings.GetString("discount.billBasis");
        BlockOverLimit = settings.GetBool("credit.blockOverLimit", false);
        ShowMargins = settings.GetBool("profit.showMargins", true);
        TaxEnabled = settings.GetBool("tax.enabled");
        TaxPercentText = Text(settings.GetDouble("tax.percent"));

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

        LoadCloud();
    }

    private void LoadCloud()
    {
        if (!CanChangeCloud) { ClearCloudData(); return; }
        var settings = _editForm;
        if (settings is null) { ClearCloudData(); return; }
        // Cloud sync (F-48): the key is the one value that is shown as a masked label, never as text.
        SyncEnabled = settings.GetBool(SyncService.EnabledKey);
        SyncUrl = settings.GetString(SyncService.UrlKey);
        SyncTable = settings.GetString(SyncService.TableKey);
        SyncShopId = settings.GetString(SyncService.ShopIdKey);
        SyncAutomatic = settings.GetBool(SyncService.AutomaticEnabledKey);
        SyncAutomaticMinutesText = settings.GetInt(SyncService.AutomaticMinutesKey, 15)
            .ToString(CultureInfo.InvariantCulture);
        SyncKey = string.Empty;
        _syncKeyCleared = false;
        RaiseSyncLabels();
    }

    /// <summary>
    /// Which section is open: 0 App name &amp; icon, 1 Shop, 2 Printing, 3 Sales rules, 4 Cloud, 5 Staff, 6 App words, 7 Fields.
    /// Bound two-way so the shell's dark-header section buttons and the person's staff shortcut can
    /// select the same existing content without rebuilding it.
    /// </summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, Math.Clamp(value, 0, 7));
    }

    /// <summary>
    /// Opens a tab by number (the frame's top-bar buttons call this).
    ///
    /// It does **not** reload the page: the menu already refreshes Settings whenever it is opened, and
    /// reloading here would throw away half-typed values when somebody clicks their own name while
    /// already on this page. Moving a TabControl must never cost the operator their work.
    /// </summary>
    public void ShowTab(int index) => SelectedTabIndex = index;

    /// <summary>
    /// Called every time the page is opened, so a value changed elsewhere (the printing switch on
    /// the counter screen writes the same key) is shown here too. Section changes do not call this.
    /// </summary>
    public void Refresh()
    {
        Load();
        LoadUsers();
        NativeFields.Refresh();
    }

    public void LoadUsers()
    {
        if (_savingStaff || _savingPermission) return;
        _recoveryEdit = null;
        _newRecoveryWord = string.Empty;
        OnPropertyChanged(nameof(NewRecoveryWord));
        ResetStaffSecret = string.Empty;
        var currentId = _selectedUser?.Id;
        if (!UpdateStaffAccess()) return;
        var result = _services.Auth.ListUsers(_user);

        if (!result.Ok || result.Value is null)
        {
            ClearStaffData();
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
        LoadRecoveryWordEdit();
    }

    private void LoadRecoveryWordEdit()
    {
        _recoveryEdit = null;
        var loaded = _services.Auth.LoadRecoveryEditForm(_user);
        if (!loaded.Ok || loaded.Value is null)
        { SetStatus(loaded.Error ?? "The recovery word editor could not be loaded. Refresh before editing.", true); return; }
        _recoveryEdit = loaded.Value;
    }

    public void RefreshStaffAccess()
    {
        if (_savingStaff || _savingPermission) return;
        var hadSettings = CanUseSettings;
        if (!UpdateSettingsAccess()) return;
        if (!hadSettings) Load();
        var wasAllowed = _staffAccessAllowed;
        if (UpdateStaffAccess() && !wasAllowed) LoadUsers();
    }

    public bool CanUseSettings => _settingsAccessAllowed;

    private bool UpdateSettingsAccess()
    {
        var access = _services.Settings.CheckAccess(_user);
        var changed = SetProperty(ref _settingsAccessAllowed, access.Ok, nameof(CanUseSettings));
        if (!access.Ok)
        {
            ClearCloudData(); ClearStaffData(); LabelRows.Clear();
            _editForm = null;
            _cloudAccessAllowed = false;
            _staffAccessAllowed = false;
            OnPropertyChanged(nameof(CanManageStaff));
            OnPropertyChanged(nameof(CannotManageStaff));
            OnPropertyChanged(nameof(IsOwner));
            OnPropertyChanged(nameof(CanManageFinancialVisibility));
            OnPropertyChanged(nameof(CanChangeSelectedUser));
            OnPropertyChanged(nameof(CanChangeAppName));
            OnPropertyChanged(nameof(CanChangeAppIcon));
            OnPropertyChanged(nameof(CanChangeStartup));
            OnPropertyChanged(nameof(CanChangeCloud));
            SetStatus(access.Error ?? "Settings access is not available. Sign in again or refresh.", true);
            return false;
        }
        var cloud = _services.Settings.CheckCloudAccess(_user).Ok;
        var cloudChanged = SetProperty(ref _cloudAccessAllowed, cloud, nameof(CanChangeCloud));
        if (!cloud) ClearCloudData();
        else if (cloudChanged) { _editForm = null; LoadCloud(); }
        if (cloudChanged) _editForm = null; // Refresh must explicitly reload the whole form after access changes.
        if (changed)
        {
            OnPropertyChanged(nameof(CanChangeAppName));
            OnPropertyChanged(nameof(CanChangeAppIcon));
            OnPropertyChanged(nameof(CanChangeStartup));
        }
        return true;
    }

    private void ClearCloudData()
    {
        SyncKey = SyncUrl = SyncTable = SyncShopId = SyncAutomaticMinutesText = string.Empty;
        SyncEnabled = SyncAutomatic = false;
        _syncKeyCleared = false;
        SyncKeyLabel = "Cloud settings access is required.";
        SyncSummary = "Cloud settings are hidden until access is verified.";
    }

    private bool UpdateStaffAccess()
    {
        var access = _services.Auth.CheckStaffAccess(_user);
        var changed = SetProperty(ref _staffAccessAllowed, access.Ok, nameof(CanManageStaff));
        if (changed)
        {
            OnPropertyChanged(nameof(CannotManageStaff));
            OnPropertyChanged(nameof(CanManageFinancialVisibility));
            OnPropertyChanged(nameof(CanChangeSelectedUser));
            OnPropertyChanged(nameof(IsOwner));
            OnPropertyChanged(nameof(CanChangeAppName));
            OnPropertyChanged(nameof(CanChangeAppIcon));
            OnPropertyChanged(nameof(CanChangeStartup));
        }
        if (!access.Ok)
        {
            // Avoid replacing unrelated Settings-page status on every non-owner timer tick.
            if (Users.Count > 0 || _selectedUser is not null || changed)
            {
                ClearStaffData();
                SetStatus(access.Error ?? "Current Owner access is required for staff administration.", true);
            }
            return false;
        }
        return true;
    }

    private void ClearPricing()
    {
        _pricingVersion = null;
        PricingEditRate = PricingProfit = PricingRetail = PricingOverride = PricingExtra = false;
        PricingManualLimit = PricingProfitLimit = PricingRetailLimit = PricingExtraLimit = string.Empty;
    }

    private void ClearStaffData()
    {
        // Clear selection directly: the normal setter reads pricing/permissions.
        _selectedUser = null;
        _recoveryEdit = null;
        _newRecoveryWord = string.Empty;
        _permissionEdit = null;
        _pricingVersion = null;
        _resetStaffSecret = string.Empty;
        Users.Clear();
        Permissions.Clear();
        PagePermissions.Clear();
        FinancialPermissions.Clear();
        ClearPricing();
        NewStaffName = NewStaffPin = ResetStaffSecret = NewRecoveryWord = string.Empty;
        OnPropertyChanged(nameof(ResetStaffSecret));
        OnPropertyChanged(nameof(NewRecoveryWord));
        OnPropertyChanged(nameof(SelectedUser));
        OnPropertyChanged(nameof(SelectedUserCaption));
        OnPropertyChanged(nameof(CanChangeSelectedUser));
        StatusText = string.Empty;
    }

    private void LoadPermissions()
    {
        if (_savingPermission || _savingStaff) return;
        _permissionEdit = null;
        if (!UpdateStaffAccess()) return;
        LoadPricing();
        ClearPermissionRows();
        if (_selectedUser is null) return;
        var loaded = _services.Permissions.LoadEditForm(_user, _selectedUser.Id);
        if (!loaded.Ok || loaded.Value is null)
        {
            SetStatus(loaded.Error ?? "Permissions could not be read. Reload the selected person.", true);
            return;
        }
        _permissionEdit = loaded.Value;
        ShowPermissions(loaded.Value);
    }

    private void ClearPermissionRows()
    {
        Permissions.Clear(); PagePermissions.Clear(); FinancialPermissions.Clear();
    }

    private void ShowPermissions(PermissionEditForm form)
    {
        ClearPermissionRows();
        if (_selectedUser?.Id != form.UserId) { _permissionEdit = null; return; }
        var editable = form.Role != UserRole.Owner && CanManageStaff;
        _suppressPermissionSave = true;
        try
        {
            foreach (var module in Modules.All)
            {
                var toggle = new PermissionToggle(module, PermissionDocumentation.For(module).Title, form.Values[module],
                    editable || (CanManageFinancialVisibility && PermissionService.IsFinancialVisibility(module)));
                toggle.Changed += OnPermissionChanged;
                Permissions.Add(toggle);
                if (PermissionDocumentation.IsSensitive(module)) FinancialPermissions.Add(toggle);
                else PagePermissions.Add(toggle);
            }
        }
        finally { _suppressPermissionSave = false; }
    }

    // ----------------------------- Saving -------------------------------

    private void Save()
    {
        if (_changingIcon) return;
        if (_savingSettings) return;
        if (!_startupAttempt.Ready) { SetStatus(_startupSaveHint, true); return; }
        _savingSettings = true;
        try { SaveCore(); }
        finally { _savingSettings = false; }
    }

    private void SaveCore()
    {
        if (_changingIcon) return;
        if (!UpdateSettingsAccess()) return;
        var form = _editForm;
        if (form is null) { SetStatus("Refresh Settings and review the form before saving.", true); return; }
        var startupChoice = SelectedStartupMode;

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

        var automaticMinutes = 15;
        if (CanChangeCloud && (!int.TryParse(SyncAutomaticMinutesText.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out automaticMinutes)
            || automaticMinutes is < 5 or > 1440))
        {
            SetStatus("Automatic sync must be a whole number of minutes from 5 to 1440.", true);
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

        if (CanChangeAppName && (AppDisplayName.Trim().Length == 0 || AppDisplayName.Trim().Length > 40))
        {
            SetStatus("The application name must be 1 to 40 letters.", true);
            return;
        }

        // A switch that is ON with half a form behind it would fail on the Cloud sync page with a
        // sentence the owner cannot act on, so the page refuses the combination here (D-66, v0.26.0).
        var keyWillExist = !_syncKeyCleared && (form.HasCloudKey || SyncKey.Trim().Length > 0);

        if (CanChangeCloud && SyncEnabled
            && (SyncUrl.Trim().Length == 0 || SyncTable.Trim().Length == 0
                || SyncShopId.Trim().Length == 0 || !keyWillExist))
        {
            SetStatus("Cloud sync is switched on, but the address, table, shop id and key must all be filled. " +
                      "Fill them, or switch cloud sync off.", true);
            return;
        }

        var labelValues = LabelRows.ToDictionary(
            row => row.Key,
            row => (string?)row.Text,
            StringComparer.Ordinal);
        var labelValidation = _services.AppLabels.Validate(labelValues);

        if (!labelValidation.Ok)
        {
            SetStatus(labelValidation.Error ?? "The application labels could not be saved.", true);
            return;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["appearance.productPhotoSize"] = ProductPhotoSize,
            ["store.name"] = StoreName.Trim(),
            ["store.address"] = StoreAddress.Trim(),
            ["store.phone"] = StorePhone.Trim(),
            ["store.footerNote"] = FooterNote.Trim(),

            ["printer.name"] = PrinterName.Trim(),
            ["printer.width"] = paper.Width,
            ["printer.autoPrint"] = AutoPrint,
            ["printer.autoCut"] = AutoCut,
            ["printer.defaultBulk"] = DefaultBulk,
            ["printer.bulkFormat"] = SelectedBulkFormat?.Key ?? "separate_receipts",
            ["whatsapp.preparePdf"] = WhatsAppPreparePdf,
            ["whatsapp.exportDirectory"] = WhatsAppExportDirectory.Trim(),
            ["printer.copies"] = copies,
            ["receipt.showDiscount"] = ShowDiscount,
            ["sale.successPopup"] = SaleSuccessPopup,

            ["discount.blockBelowCost"] = true,
            ["credit.blockOverLimit"] = BlockOverLimit,
            ["tax.enabled"] = TaxEnabled,
            ["tax.percent"] = taxPercent,
            ["expiry.levels"] = form.ExpiryBands
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

        foreach (var pair in labelValues)
        {
            values[AppLabelService.SettingKey(pair.Key)] = pair.Value!.Trim();
        }

        // Only a person the owner ticked for cloud sync writes these four keys: the key is a password
        // to the whole shop's mirror, and a manager's Settings tab must not be able to replace it.
        if (CanChangeCloud)
        {
            values[SyncService.EnabledKey] = SyncEnabled;
            values[SyncService.UrlKey] = SyncUrl.Trim();
            values[SyncService.TableKey] = SyncTable.Trim();
            values[SyncService.ShopIdKey] = SyncShopId.Trim();
            values[SyncService.AutomaticEnabledKey] = SyncAutomatic;
            values[SyncService.AutomaticMinutesKey] = automaticMinutes;

            if (_syncKeyCleared)
            {
                values[SyncService.KeyKey] = string.Empty;
            }
            else if (SyncKey.Trim().Length > 0)
            {
                values[SyncService.KeyKey] = SyncKey.Trim();
            }
        }

        // The app name is an owner-only key: a manager's ordinary save must not trip the whole
        // atomic dictionary on one key, so it travels only when the owner changed it.
        if (CanChangeAppName && !string.Equals(AppDisplayName.Trim(),
                form.GetString("brand.appName"), StringComparison.Ordinal))
        {
            values["brand.appName"] = AppDisplayName.Trim();
        }

        // A manager saving ordinary settings must not submit this owner-only switch.
        if (_user.IsOwner) values[MasterFoundationSettings.ProfitShowMargins] = ShowMargins;
        if (_user.IsOwner) values["discount.billBasis"] = BillDiscountBasis;
        var result = _services.Settings.SaveEditForm(values, _user, form);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The settings could not be saved.", true);
            return;
        }

        form = result.SettingsEdit;
        _editForm = form;
        if (form is null) { SetStatus("SAVED. Reopen Settings before making another change.", true); return; }
        // The summary line is rebuilt from what was just stored, so the owner sees the meaning of
        // the three numbers he typed without leaving the page.
        ExpirySummary = form.ExpiryBands.Summary;
        RaiseLoyaltySummary();

        // The key box is emptied at once: a key left on the screen is one a person walking past can read.
        SyncKey = string.Empty;
        _syncKeyCleared = false;
        RaiseSyncLabels();
        LoadLabels();

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

        // Startup and close (Portion 16): saved through the service, not the values above - the
        // Windows entry must stick before the setting claims it. Unchanged means untouched: an
        // ordinary save must not rewrite the registry or audit a choice nobody made.
        var startupNote = string.Empty;

        if (CanChangeStartup && startupChoice is not null
            && startupChoice.Key != form.GetString(StartupService.ModeKey))
        {
            if (!TrySaveStartupChoice(form, startupChoice.Key, out var startupForm, out startupNote)) return;
            form = startupForm;
        }

        // Screen lockdown (owner's word, Nov 2026): a plain setting, owner-only at the service
        // guard. Unchanged means untouched, like the startup mode above.
        var focusNote = string.Empty;

        if (CanChangeStartup && FocusGuardEnabled != form.GetBool(FocusGuardService.GuardKey))
        {
            var focus = _services.Settings.SaveEditForm(new Dictionary<string, object?> { [FocusGuardService.GuardKey] = FocusGuardEnabled }, _user, form);

            if (!focus.Ok)
            {
                SetStatus("Settings SAVED; screen lock was not saved. Refresh before retrying. " + focus.Error, true);
                return;
            }

            form = focus.SettingsEdit ?? form;
            _editForm = form;
            focusNote = " Screen lock: " + (FocusGuardEnabled ? "on." : "off.");
        }

        SetStatus($"Saved. Paper {paper.Width} mm, printing " +
                  (AutoPrint ? "automatic." : "manual (use the button on the counter screen).") +
                  cloudNote + startupNote + focusNote, false);

        if (focusNote.Length > 0)
        {
            LockdownChromeChanged?.Invoke();
        }
    }

    public string StartupSaveHint => _startupSaveHint;
    public bool HasStartupSaveHint => _startupSaveHint.Length > 0;

    private bool TrySaveStartupChoice(SettingsEditForm form, string wanted, out SettingsEditForm savedForm, out string note)
    {
        savedForm = form; note = string.Empty;
        if (!_startupAttempt.TryBegin()) return false;
        string? serviceFailure = null;
        try
        {
            var saved = _services.CheckInteractiveSession()
                ? _services.Startup.SetModeChecked(_user, wanted, Environment.ProcessPath ?? string.Empty, form)
                : new ServiceResult { Error = "Sign in again before changing startup.", NoCommitConfirmed = true };
            if (!saved.Ok)
            {
                serviceFailure = saved.Error;
                _startupAttempt.ObserveFailure(saved);
                _editForm = null;
                SetStartupSaveHint("Settings SAVED; " + saved.Error);
                SetStatus(_startupSaveHint, true);
                return false;
            }
            _startupAttempt.AcknowledgeCommit();
            var receipt = saved.SettingsEdit;
            if (receipt is null || receipt.GetString(StartupService.ModeKey) != wanted)
                throw new InvalidOperationException("The startup save receipt is missing or mismatched.");
            _editForm = receipt;
            SelectedStartupMode = StartupModeOptions.First(option => option.Key == wanted);
            note = " Startup SAVED: " + _services.Startup.ModeLabel + " " + _services.Startup.ScopeText
                + " " + string.Join(" ", saved.Warnings);
            SetStartupSaveHint(string.Empty);
            SetStatus("Settings SAVED." + note, false);
            if (!ReferenceEquals(_editForm, receipt) || SelectedStartupMode?.Key != wanted)
                throw new InvalidOperationException("The saved startup choice was not retained.");
            savedForm = receipt;
            _startupAttempt.CompleteReset();
            return true;
        }
        catch (Exception exception)
        {
            _startupAttempt.ObserveException();
            _editForm = null;
            _services.Log.Error("Startup choice save/handoff did not finish.", exception);
            var message = serviceFailure is not null
                ? "Settings SAVED; " + serviceFailure + " Page notification failed; review the startup result before retrying."
                : _startupAttempt.WasAcknowledged
                ? "Settings SAVED; startup SAVED, but the page could not refresh. Verify Settings and both Windows entries before reopening; do not repeat the change."
                : "Settings SAVED; startup change is unconfirmed. Verify Settings and both Windows entries before reopening or retrying.";
            try { SetStartupSaveHint(message); SetStatus(message, true); }
            catch (Exception notification) { _services.Log.Error("Startup failure notification did not finish.", notification); }
            return false;
        }
    }

    private void SetStartupSaveHint(string message)
    {
        _startupSaveHint = message;
        OnPropertyChanged(nameof(StartupSaveHint)); OnPropertyChanged(nameof(HasStartupSaveHint));
    }

    // ---------------------------- Cloud sync ----------------------------

    /// <summary>Only someone the owner ticked for cloud sync may see or change these four boxes.</summary>
    public bool CanChangeCloud => CanUseSettings && _cloudAccessAllowed;

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

    /// <summary>Portion 15: the automatic tick - on means the shop pushes itself every few minutes.</summary>
    public bool SyncAutomatic
    {
        get => _syncAutomatic;
        set
        {
            if (SetProperty(ref _syncAutomatic, value))
            {
                RaiseSyncSummary();
            }
        }
    }

    /// <summary>Portion 15: minutes between automatic pushes, whole, 5 to 1440 (checked on save).</summary>
    public string SyncAutomaticMinutesText
    {
        get => _syncAutomaticMinutesText;
        set => SetProperty(ref _syncAutomaticMinutesText, value);
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
        if (!UpdateSettingsAccess()) return;
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
        if (!CanChangeCloud) { SyncKeyLabel = "Cloud settings access is required."; return; }
        SyncKeyLabel = _editForm?.HasCloudKey == true
            ? "A key is saved. Leave the box empty to keep it."
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

        SyncSummary = !_services.Cloud.IsConfigured
            ? "Switched on but not complete - fill the address, the key, the table and the shop id, then press Save settings."
            : SyncAutomatic
                ? "Ready. Open SYSTEM -> Cloud sync to push the shop's data, or to read the cloud copy. Automatic push is on."
                : "Ready. Open SYSTEM -> Cloud sync to push the shop's data, or to read the cloud copy.";
    }

    // ------------------------------ Staff -------------------------------

    private void AddStaff()
    {
        if (_savingStaff || _savingPermission) return;
        if (!_staffCreate.TryBegin())
        {
            SetStatus(_staffCreate.WasAcknowledged
                ? "SAVED. The previous staff account was created. Verify before reopening this page; do not add it again."
                : "Staff account creation is unconfirmed. Verify before reopening this page; do not add it again.", true);
            return;
        }
        _savingStaff = true;
        _createdStaffId = 0;
        var writeStarted = false;
        try
        {
            if (!UpdateStaffAccess()) return;
            var name = NewStaffName;
            var role = UserRoles.Parse(SelectedRole);
            var pin = NewStaffPin;
            writeStarted = true;
            var result = _services.Auth.CreateStaff(_user, name, role, pin);
            if (!result.Ok)
            {
                _staffCreate.ObserveFailure(result);
                SetStatus(result.Error ?? "Staff account creation is unconfirmed. Verify before retrying.", true);
                return;
            }
            _staffCreate.AcknowledgeCommit();
            _createdStaffId = result.Value;
            _newStaffName = _newStaffPin = string.Empty; // Consume original inputs before observable callbacks.
            var saved = result.StaffEdit;
            if (_createdStaffId <= 0 || saved?.StaffForm is null || saved.Id != _createdStaffId
                || saved.StaffForm.UserId != _createdStaffId || Users.Any(person => person.Id == _createdStaffId))
            { SetStatus("SAVED. The account was created but could not be shown. Verify before reopening this page; do not add it again.", true); return; }
            OnPropertyChanged(nameof(NewStaffName));
            OnPropertyChanged(nameof(NewStaffPin));
            Users.Add(saved); // Exact created row only;do not fresh-read/rebase existing account/permission/recovery forms.
            SetStatus("SAVED. Staff #" + _createdStaffId + " — " + saved.Name + " was created. The role decides what they see.", false);
            if (_newStaffName.Length != 0 || _newStaffPin.Length != 0 || !Users.Contains(saved) || saved.Id != _createdStaffId)
            { SetStatus("SAVED. The account was created but input/list cleanup changed. Verify before reopening this page; do not add it again.", true); return; }
            _staffCreate.CompleteReset(); // Only after callbacks and the consumed-input/retained-row verdict.
        }
        catch (Exception exception)
        {
            _staffCreate.ObserveException();
            _services.Log.Error("Staff creation save/refresh did not finish.", exception);
            SetStatus(_staffCreate.WasAcknowledged
                ? "SAVED. Staff #" + _createdStaffId + " was created but the page could not refresh. Verify before reopening this page; do not add it again."
                : "Staff account creation is unconfirmed. Verify before reopening this page; do not add it again.", true);
        }
        finally
        {
            if (!writeStarted && _staffCreate.InFlight) _staffCreate.CancelBeforeWrite();
            _savingStaff = false;
        }
    }

    private void ToggleActive() => SaveStaffChange(secret: false);

    private void ResetSecret() => SaveStaffChange(secret: true);

    private void SaveStaffChange(bool secret)
    {
        if (_savingStaff || _savingPermission) return;
        _savingStaff = true;
        var acknowledged = false;
        try
        {
            if (!UpdateStaffAccess()) return;
            if (_selectedUser is not { } person) return;
            var form = person.StaffForm;
            if (form is null || person.Id != form.UserId || !Users.Contains(person))
            { SetStatus("Refresh Staff and review the selected account before saving.", true); return; }
            var result = secret
                ? _services.Auth.SetSecret(_user, form.UserId, ResetStaffSecret, form.Role == UserRole.Owner, form.EditVersion)
                : _services.Auth.SetActive(_user, form.UserId, !form.Active, form.EditVersion);
            if (!result.Ok)
            {
                if (!result.NoCommitConfirmed) ClearStaffData();
                SetStatus(result.Error ?? "The account could not be changed.", true);
                return;
            }
            acknowledged = true;
            _resetStaffSecret = string.Empty; // Consume plaintext before any observable binding callback.
            var saved = result.StaffEdit;
            if (saved?.StaffForm is null || saved.StaffForm.UserId != form.UserId
                || !ReferenceEquals(_selectedUser, person) || !Users.Contains(person))
            { ClearStaffData(); SetStatus("SAVED. Reopen Staff and verify the account before another change.", true); return; }
            var index = Users.IndexOf(person);
            _selectedUser = saved; // Exact successful handoff;never re-read/rebase other writers here.
            _permissionEdit = null;
            _pricingVersion = null; // Preserve input but require explicit reload/review before its Save.
            Users[index] = saved;
            ClearPermissionRows();
            OnPropertyChanged(nameof(ResetStaffSecret));
            OnPropertyChanged(nameof(SelectedUser));
            OnPropertyChanged(nameof(SelectedUserCaption));
            OnPropertyChanged(nameof(CanChangeSelectedUser));
            SetStatus(secret ? (saved.IsOwner ? "SAVED. Owner password changed. Sign in with the new one."
                : "SAVED. " + saved.Name + " can sign in with the new PIN.")
                : saved.Active ? saved.Name + " can sign in again." : saved.Name + " has been switched off and can no longer sign in.", false);
        }
        catch (Exception exception)
        {
            _services.Log.Error("Staff save/refresh did not finish.", exception);
            ClearStaffData();
            SetStatus(acknowledged ? "SAVED. The account was changed but the page could not refresh. Reopen Staff and verify."
                : "Staff account saving is unconfirmed. Refresh and verify before retrying.", true);
        }
        finally { _savingStaff = false; }
    }

    /// <summary>
    /// The owner's own "forgot password" word (Phase 2, owner's word). Until this is set, the
    /// factory word signs the owner in; an empty save hands the door back to the factory word.
    /// </summary>
    private void SaveRecoveryWord()
    {
        if (_savingStaff || _savingPermission) return;
        _savingStaff = true;
        var acknowledged = false;
        try
        {
            if (!UpdateStaffAccess()) return;
            var form = _recoveryEdit;
            if (form is null)
            { SetStatus("Refresh Staff and review the recovery word editor before saving.", true); return; }
            var result = _services.Auth.SetRecoveryWord(_user, NewRecoveryWord, form.EditVersion);
            if (!result.Ok)
            {
                if (!result.NoCommitConfirmed)
                {
                    _recoveryEdit = null; _newRecoveryWord = string.Empty;
                    OnPropertyChanged(nameof(NewRecoveryWord));
                }
                SetStatus(result.Error ?? "Recovery word saving is unconfirmed. Refresh and verify.", true);
                return;
            }
            acknowledged = true;
            _recoveryEdit = result.RecoveryEdit; // Exact handoff,not a fresh read of a later writer.
            _newRecoveryWord = string.Empty;
            OnPropertyChanged(nameof(NewRecoveryWord));
            if (_recoveryEdit is null)
            { SetStatus("SAVED. Refresh Staff and verify the recovery word before another change.", true); return; }
            SetStatus(result.Warnings.Count > 0 ? string.Join(" ", result.Warnings)
                : _recoveryEdit.UsesFactoryWord ? "SAVED. The recovery word is back to the factory word (area11)."
                : "SAVED. The recovery word changed. The factory word no longer works.", result.Warnings.Count > 0);
        }
        catch (Exception exception)
        {
            _recoveryEdit = null; _newRecoveryWord = string.Empty;
            _services.Log.Error("Recovery word save/refresh did not finish.", exception);
            OnPropertyChanged(nameof(NewRecoveryWord));
            SetStatus(acknowledged ? "SAVED. The recovery word was stored but the page could not refresh. Reopen Staff and verify."
                : "Recovery word saving is unconfirmed. Refresh and verify before retrying.", true);
        }
        finally { _savingStaff = false; }
    }

    private void ResetLabels()
    {
        if (!_services.Permissions.Can(_user, Modules.Settings))
        {
            SetStatus("Only a person who may change settings can restore the original application words.", true);
            return;
        }

        if (!_confirmLabelReset)
        {
            _confirmLabelReset = true;
            OnPropertyChanged(nameof(ResetLabelsText));
            SetStatus("Press the restore button once more to put every application word back to its original. Nothing has changed yet.", false);
            return;
        }

        if (_editForm is null) { SetStatus("Refresh Settings before restoring application words.", true); return; }
        var result = _services.AppLabels.Reset(_user, _editForm);

        if (!result.Ok)
        {
            _confirmLabelReset = false;
            OnPropertyChanged(nameof(ResetLabelsText));
            SetStatus(result.Error ?? "The original application words could not be restored.", true);
            return;
        }

        _editForm = result.SettingsEdit;
        LoadLabels();
        _onSaved?.Invoke();
        SetStatus("Original application words restored. The shell and open pages have been updated.", false);
    }

    private void ResetPermissions() => SavePermissionChange(null);

    // Pointer clicks are delayed by the Windows double-click interval. Keyboard Space still uses
    // the native checkbox. A selection/reload replaces objects, so a pending old row cannot save.
    public void TogglePermissionFromPointer(PermissionToggle toggle)
    {
        if (!UpdateStaffAccess()) return;
        if (!CanManageStaff || !toggle.CanEdit || !Permissions.Contains(toggle)) return;
        toggle.Allowed = !toggle.Allowed;
    }

    public void OpenPermissionDetails(PermissionToggle toggle)
    {
        if (!UpdateStaffAccess()) return;
        if (!Permissions.Contains(toggle)) return;
        var result = _services.PermissionInspector.Inspect(_user, toggle.Module);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "Only the active owner can inspect assignments.", true);
            return;
        }
        PermissionDetailsDoor.Show(_services, _user, toggle.Module);
    }

    private void OnPermissionChanged(object? sender, EventArgs e)
    {
        if (_suppressPermissionSave || sender is not PermissionToggle toggle) return;
        if (_savingPermission || _savingStaff) { RestorePermissionToggle(toggle); return; }
        SavePermissionChange(toggle);
    }

    private void RestorePermissionToggle(PermissionToggle toggle)
    {
        if (_permissionEdit is null || !Permissions.Contains(toggle)) return;
        _suppressPermissionSave = true;
        try { toggle.Allowed = _permissionEdit.Values[toggle.Module]; }
        finally { _suppressPermissionSave = false; }
    }

    private void SavePermissionChange(PermissionToggle? toggle)
    {
        if (_savingPermission || _savingStaff) return;
        if (!UpdateStaffAccess() || _selectedUser is not { } target) return;
        if (toggle is not null && (!toggle.CanEdit || !Permissions.Contains(toggle))) return;
        var form = _permissionEdit;
        if (form is null || form.UserId != target.Id)
        { _permissionEdit = null; ClearPermissionRows(); SetStatus("Reload the selected person's permissions before editing.", true); return; }
        _savingPermission = true;
        var acknowledged = false;
        try
        {
            var result = toggle is null
                ? _services.Permissions.ResetToRoleDefaults(_user, target.Id, form.EditVersion)
                : _services.Permissions.Set(_user, target.Id, toggle.Module, toggle.Allowed, form.EditVersion);
            if (!result.Ok)
            {
                if (!result.NoCommitConfirmed) { _permissionEdit = null; ClearPermissionRows(); }
                else if (toggle is not null) RestorePermissionToggle(toggle);
                SetStatus(result.Error ?? "Permissions could not be saved.", true);
                return;
            }
            acknowledged = true;
            _permissionEdit = result.PermissionEdit;
            if (_permissionEdit is null || !ReferenceEquals(_selectedUser, target))
            { _permissionEdit = null; ClearPermissionRows(); SetStatus("SAVED. Reopen Staff before changing permissions again.", true); return; }
            if (toggle is null) ShowPermissions(_permissionEdit); // Exact reset result,never a new read/rebase.
            SetStatus(toggle is null ? target.Name + " is back to the normal role permissions."
                : target.Name + ": '" + toggle.Label + "' is now " + (toggle.Allowed ? "allowed." : "not allowed."), false);
        }
        catch (Exception exception)
        {
            _permissionEdit = null;
            _services.Log.Error("Staff permission save/refresh did not finish.", exception);
            ClearPermissionRows();
            SetStatus(acknowledged ? "SAVED. Permissions were stored but the page could not refresh. Reopen Staff."
                : "Permission saving is unconfirmed. Reload and verify before retrying.", true);
        }
        finally { _savingPermission = false; }
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
/// <summary>Portion 16: one start-and-close choice on the Settings page. ToString is the label.</summary>
public sealed class StartupModeOption
{
    public StartupModeOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    public string Label { get; }

    public override string ToString() => Label;
}

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

/// <summary>One editable application word. Key stays stable while Text is typed in the box.</summary>
public sealed class AppLabelEditRow
{
    public AppLabelEditRow(string key, string defaultText, string text)
    {
        Key = key;
        DefaultText = defaultText;
        Text = text;
    }

    public string Key { get; }

    public string DefaultText { get; }

    public string Text { get; set; }
}

/// <summary>Portion 11: one shape a bulk print can take - the key is what `printer.bulkFormat` stores.</summary>
public sealed class BulkFormatOption
{
    public BulkFormatOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

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

    public string Summary => PermissionDocumentation.For(Module).Summary;

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
