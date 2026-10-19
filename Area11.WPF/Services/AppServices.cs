using Area11.WPF.Data;
using Area11.WPF.Utils;

namespace Area11.WPF.Services;

/// <summary>
/// Composition root: one place where every service is created and wired.
/// Views and ViewModels receive this object instead of building their own dependencies.
/// </summary>
public sealed class AppServices
{
    public AppServices(AppConfig config)
    {
        Config = config;
        Logger = new LoggerService(config.ResolveLogPath());
        Logger.Info("Area11 POS starting.");

        Database = new Database(config.ResolveDatabasePath());
        Database.Migrate();
        DatabaseSeed.Apply(Database, config.Security.DefaultOwnerPassword);

        Audit = new AuditService();
        Settings = new SettingsService(Database, Audit, Logger);
        Auth = new AuthService(
            Database,
            Logger,
            Audit,
            config.Security.MaxLoginAttempts,
            config.Security.LockoutSeconds);
        Products = new ProductService(Database, Logger, Audit);
        Reports = new ReportService(Database, Logger);

        Logger.Info("Startup complete.");
    }

    public AppConfig Config { get; }

    public LoggerService Logger { get; }

    public Database Database { get; }

    public AuditService Audit { get; }

    public SettingsService Settings { get; }

    public AuthService Auth { get; }

    public ProductService Products { get; }

    public ReportService Reports { get; }
}
