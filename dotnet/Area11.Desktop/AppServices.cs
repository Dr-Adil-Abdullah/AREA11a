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
