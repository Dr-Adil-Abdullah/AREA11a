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
        Database = new Database();
        Audit = new AuditService(Database, Log);
        Settings = new SettingsService(Database, Audit, Log);
        Auth = new AuthService(Database, Audit, Settings, Log);
        Permissions = new PermissionService(Database, Log, Audit);
        Numbering = new NumberingService(Settings);
        Products = new ProductService(Database, Audit, Log);
        Dashboard = new DashboardService(Database, Log);
        Partners = new PartnerService(Database, Audit, Log);
        Sales = new SaleService(Database, Audit, Numbering, Settings, Permissions, Log);
        Purchases = new PurchaseService(Database, Audit, Numbering, Permissions, Log);
        Holds = new HoldCartService(Database, Audit, Numbering, Permissions, Sales, Log);
        Returns = new ReturnService(Database, Audit, Numbering, Settings, Permissions, Log);
        Quarantine = new QuarantineService(Database, Audit, Permissions, Log);
        ReturnReceipts = new ReturnReceiptService(Database, Settings, Log);
        Alert = new AlertService(Database, Audit, Permissions, Log);
        Provisional = new ProvisionalReturnService(Database, Audit, Numbering, Permissions, Log);
        ProvisionalReceipts = new ProvisionalReceiptService(Provisional, Settings, Log);
        Receipts = new ReceiptService(Database, Settings, Log);
        Printer = new PrinterService(Settings, Log);
    }

    public Database Database { get; }

    public LogService Log { get; }

    public AuditService Audit { get; }

    public SettingsService Settings { get; }

    public AuthService Auth { get; }

    public PermissionService Permissions { get; }

    public NumberingService Numbering { get; }

    public ProductService Products { get; }

    public DashboardService Dashboard { get; }

    public PartnerService Partners { get; }

    public SaleService Sales { get; }

    public PurchaseService Purchases { get; }

    public HoldCartService Holds { get; }

    public ReturnService Returns { get; }

    public QuarantineService Quarantine { get; }

    public ReturnReceiptService ReturnReceipts { get; }

    public AlertService Alert { get; }

    public ProvisionalReturnService Provisional { get; }

    public ProvisionalReceiptService ProvisionalReceipts { get; }

    public ReceiptService Receipts { get; }

    public PrinterService Printer { get; }

    public User? CurrentUser { get; private set; }

    public bool UsesDefaultOwnerPassword { get; private set; }

    /// <summary>Migrate, seed, load settings — in that order, and fail loudly if any step fails.</summary>
    public static AppServices Create()
    {
        var services = new AppServices();

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
