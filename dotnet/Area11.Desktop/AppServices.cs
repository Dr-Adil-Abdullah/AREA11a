using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop;

/// <summary>
/// The composition root. It is the only place that constructs the Core services, so the views
/// only ever see interfaces of behaviour, never a database connection (Spec 4.4.3).
/// </summary>
public sealed class AppServices
{
    private AppServices()
    {
        Log = new LogService();

        // Read before the database is opened: on a second counter this file is the only thing that
        // knows where the shop's database even is (F-45).
        Config = ShopConfig.Current;
        Database = new Database();
        Audit = new AuditService(Database, Log);
        Settings = new SettingsService(Database, Audit, Log);
        Auth = new AuthService(Database, Audit, Settings, Log);
        Permissions = new PermissionService(Database, Log, Audit);
        Numbering = new NumberingService(Settings);
        Products = new ProductService(Database, Audit, Log);
        Dashboard = new DashboardService(Database, Settings, Permissions, Log);
        Partners = new PartnerService(Database, Audit, Log);
        Ledger = new CustomerLedgerService(Database, Audit, Settings, Permissions, Log);
        SupplierLedger = new SupplierLedgerService(Database, Audit, Numbering, Settings, Permissions, Log);
        Sales = new SaleService(Database, Audit, Numbering, Settings, Permissions, Log);
        Purchases = new PurchaseService(Database, Audit, Numbering, Permissions, Log);
        Holds = new HoldCartService(Database, Audit, Numbering, Permissions, Sales, Log);
        Returns = new ReturnService(Database, Audit, Numbering, Settings, Permissions, Log);
        Quarantine = new QuarantineService(Database, Audit, Permissions, Log);
        ReturnReceipts = new ReturnReceiptService(Database, Settings, Log);
        Alert = new AlertService(Database, Settings, Audit, Permissions, Log);
        Provisional = new ProvisionalReturnService(Database, Audit, Numbering, Permissions, Log);
        Stock = new StockService(Database, Audit, Settings, Permissions, Log);
        StockTake = new StockTakeService(Database, Audit, Numbering, Permissions, Log);
        Shifts = new ShiftService(Database, Audit, Permissions, Log);
        CashOut = new ExpenseService(Database, Audit, Settings, Permissions, Log);
        Recovery = new RecoveryService(Database, Audit, Settings, Permissions, Log);
        Reports = new ReportService(Database, Permissions, Log);
        Tax = new TaxService(Database, Settings, Permissions, Log);
        AuditHistory = new AuditHistoryService(Database, Permissions, Log);
        ProvisionalReceipts = new ProvisionalReceiptService(Provisional, Settings, Log);
        Receipts = new ReceiptService(Database, Settings, Log);
        Printer = new PrinterService(Settings, Log);
        Backup = new BackupService(Database, Settings, Audit, Permissions, Log);
        Import = new ImportService(Database, Audit, Permissions, Log);
        Reorder = new ReorderService(Database, Audit, Settings, Permissions, Log);
        StockMinus = new StockMinusService(Database, Audit, Permissions, Log);
        Photos = new PhotoService(Database, Audit, Permissions, Log);
        CustomFields = new CustomFieldService(Database, Audit, Permissions, Log);
        Barcodes = new BarcodeService(Database, Audit, Permissions, Log);
        Reps = new RepService(Database, Audit, Permissions, Log);
        Hardware = new HardwareService(Settings, Audit, Permissions, Log);
        // The one service that talks to the internet. Its rules are in Core; the wire is a
        // replaceable piece so every test runs without a network (F-48).
        Cloud = new SyncService(Database, Settings, Audit, Permissions, Log, Backup, new SupabaseTransport());
        // The bill history and its reprint: the same ReceiptService that printed the original (F-52).
        History = new SaleHistoryService(Database, Receipts, ReturnReceipts, Audit, Permissions, Log);
        // A new shop can be filled with believable demo data; the service refuses a shop with bills (F-53).
        SampleShop = new SampleShopService(Database, Settings, Audit, Permissions, Log);
        Lan = new LanService(Database, Audit, Log, Config);
    }

    public Database Database { get; }

    /// <summary>The one line that decides where this PC keeps the shop's database (F-45).</summary>
    public ShopConfig Config { get; }

    /// <summary>The second counter: prepare the main PC, join from the other one.</summary>
    public LanService Lan { get; }

    public LogService Log { get; }

    public AuditService Audit { get; }

    public SettingsService Settings { get; }

    public AuthService Auth { get; }

    public PermissionService Permissions { get; }

    public NumberingService Numbering { get; }

    public ProductService Products { get; }

    public DashboardService Dashboard { get; }

    public PartnerService Partners { get; }

    public CustomerLedgerService Ledger { get; }

    public SupplierLedgerService SupplierLedger { get; }

    public SaleService Sales { get; }

    public PurchaseService Purchases { get; }

    public HoldCartService Holds { get; }

    public ReturnService Returns { get; }

    public QuarantineService Quarantine { get; }

    public ReturnReceiptService ReturnReceipts { get; }

    public AlertService Alert { get; }

    public ProvisionalReturnService Provisional { get; }

    public StockService Stock { get; }

    public StockTakeService StockTake { get; }

    public ShiftService Shifts { get; }

    public ExpenseService CashOut { get; }

    public RecoveryService Recovery { get; }

    public ReportService Reports { get; }

    /// <summary>The month's tax for the accountant (F-49).</summary>
    public TaxService Tax { get; }

    /// <summary>The printer and scanner test the owner runs on his own PC (F-46).</summary>
    public HardwareService Hardware { get; }

    /// <summary>The Supabase mirror: push, look, bring (F-48).</summary>
    public SyncService Cloud { get; }

    /// <summary>Find any bill and print it again (F-52).</summary>
    public SaleHistoryService History { get; }

    /// <summary>The sample shop: demo data for a new database (F-53).</summary>
    public SampleShopService SampleShop { get; }

    public AuditHistoryService AuditHistory { get; }

    public ProvisionalReceiptService ProvisionalReceipts { get; }

    public ReceiptService Receipts { get; }

    public PrinterService Printer { get; }

    public BackupService Backup { get; }

    public ReorderService Reorder { get; }

    public StockMinusService StockMinus { get; }

    public PhotoService Photos { get; }

    public CustomFieldService CustomFields { get; }

    public BarcodeService Barcodes { get; }

    /// <summary>Medical reps, doctors and lifetime profit (F-44).</summary>
    public RepService Reps { get; }

    public ImportService Import { get; }

    public User? CurrentUser { get; private set; }

    public bool UsesDefaultOwnerPassword { get; private set; }

    /// <summary>Migrate, seed, load settings — in that order, and fail loudly if any step fails.</summary>
    public static AppServices Create()
    {
        var services = new AppServices();

        // First line in the log: if the next start dies, the file shows how far it got.
        services.Log.Info($"Starting Area11 v{BuildInfo.Version} ({BuildInfo.CodeName}) as {Environment.UserName}.");

        var migrations = new MigrationRunner(services.Database, services.Log);
        var applied = migrations.ApplyPending();
        services.Log.Info($"Database ready at {services.Database.FilePath} ({applied} migration(s) applied).");

        services.Settings.EnsureDefaults();
        new SeedService(services.Database, services.Log).Seed();
        services.Settings.Reload();

        services.UsesDefaultOwnerPassword =
            new SeedService(services.Database, services.Log).UsesDefaultOwnerPassword();

        return services;
    }

    public void SignIn(User user)
    {
        CurrentUser = user;
        Log.Info($"Signed in: {user.Name} ({user.RoleLabel}).");
    }

    public void SignOut() => CurrentUser = null;
}
