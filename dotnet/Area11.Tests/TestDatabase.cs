using Area11.Core.Data;
using Area11.Core.Services;
using Area11.Core.Utils;
using System.IO;

namespace Area11.Tests;

/// <summary>A throw-away database per test, migrated exactly like the real application.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _folder;

    public TestDatabase()
    {
        _folder = Path.Combine(Path.GetTempPath(), "area11-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);

        Database = new Database(Path.Combine(_folder, "area11.db"));
        Log = new LogService(Path.Combine(_folder, "logs"));
        Audit = new AuditService(Database, Log);

        var migrations = new MigrationRunner(Database, Log);
        migrations.ApplyPending(FindMigrationsFolder());

        Settings = new SettingsService(Database, Audit, Log);
        Settings.EnsureDefaults();
        Settings.Reload();

        Seed = new SeedService(Database, Log);
        Seed.Seed();
    }

    public Database Database { get; }

    public LogService Log { get; }

    public AuditService Audit { get; }

    public SettingsService Settings { get; }

    public SeedService Seed { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A pooled SQLite handle may still hold the file on Windows; the temp folder is
            // harmless and Windows cleans it later.
        }
    }

    private static string FindMigrationsFolder()
    {
        var direct = Path.Combine(AppContext.BaseDirectory, "Migrations");
        if (Directory.Exists(direct))
        {
            return direct;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var level = 0; level < 8 && current is not null; level++)
        {
            var candidate = Path.Combine(current.FullName, "Area11.Core", "Data", "Migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var solutionCandidate = Path.Combine(current.FullName, "dotnet", "Area11.Core", "Data", "Migrations");
            if (Directory.Exists(solutionCandidate))
            {
                return solutionCandidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("The SQL migrations folder could not be found from the test output.");
    }
}
