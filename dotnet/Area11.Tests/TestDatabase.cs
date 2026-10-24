using Area11.Core.Data;
using Area11.Core.Services;
using Area11.Core.Utils;
using System.IO;

namespace Area11.Tests;

/// <summary>
/// A throw-away database per test, built exactly like the real application.
///
/// **Why a template:** running the 19 migrations for every one of the 270 tests took five minutes
/// on the shop PC - the owner waited with no output. The schema is identical in every test, so it is
/// migrated ONCE per run, checkpointed into a single file, and copied. A copy costs milliseconds and
/// the tests still start from the real schema, not from a hand-written stub.
///
/// The copy carries the `_migrations` ledger, so <see cref="MigrationTests"/> can still prove that
/// every .sql file was applied exactly once.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private static readonly object TemplateGate = new();
    private static string? _templatePath;

    private readonly string _folder;

    public TestDatabase()
    {
        _folder = Path.Combine(Path.GetTempPath(), "area11-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);

        var file = Path.Combine(_folder, "area11.db");
        File.Copy(TemplateDatabase(), file);

        Database = new Database(file, pooling: false, durable: false);
        Log = new LogService(Path.Combine(_folder, "logs"));
        Audit = new AuditService(Database, Log);

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

    /// <summary>The folder every test migrates from - the same one the application uses.</summary>
    public static string MigrationsFolder => FindMigrationsFolder();

    public void Dispose()
    {
        // The connections of a test are not pooled, so they are closed by now and the folder can go.
        // A leftover that Windows still holds is harmless - and the next run sweeps the parent.
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Harmless: Windows cleans %TEMP% later, and the next run uses a fresh folder.
        }
    }

    /// <summary>
    /// The migrated, single-file database every test copies. Built on the first use of the run.
    /// </summary>
    private static string TemplateDatabase()
    {
        lock (TemplateGate)
        {
            var cached = _templatePath;

            if (cached is not null && File.Exists(cached))
            {
                return cached;
            }

            var root = Path.Combine(Path.GetTempPath(), "area11-tests");
            Directory.CreateDirectory(root);
            SweepOldFolders(root);

            var folder = Path.Combine(root, "_template");
            Directory.CreateDirectory(folder);

            foreach (var stale in Directory.GetFiles(folder))
            {
                File.Delete(stale);
            }

            var path = Path.Combine(folder, "template.db");

            var database = new Database(path, pooling: false, durable: false);
            var log = new LogService(Path.Combine(folder, "logs"));
            new MigrationRunner(database, log).ApplyPending(FindMigrationsFolder());

            // Copying one file must copy the whole schema: push the WAL back into the main file and
            // leave the template in DELETE mode, where no -wal companion exists.
            using (var connection = database.Open())
            {
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode=DELETE;";
                command.ExecuteNonQuery();
            }

            _templatePath = path;
            return path;
        }
    }

    /// <summary>
    /// Removes the throw-away folders of earlier runs (one per test). Anything still locked is left
    /// alone - the shop's own data folder is never touched, this is only %TEMP%\area11-tests.
    ///
    /// Only folders older than two hours: test classes run in parallel, so a fresh folder usually
    /// belongs to a test that is running right now.
    /// </summary>
    private static void SweepOldFolders(string root)
    {
        var cutoff = DateTime.UtcNow.AddHours(-2);

        foreach (var folder in Directory.GetDirectories(root))
        {
            if (string.Equals(Path.GetFileName(folder), "_template", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Directory.GetLastWriteTimeUtc(folder) > cutoff)
            {
                continue;
            }

            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Still open somewhere; it will go on a later run.
            }
        }
    }

    private static string FindMigrationsFolder()
    {
        // The SOURCE tree first: in a test run it is always the current set of files, while the
        // folder next to the test binary can lag one build behind (that is how a migration once
        // "disappeared" from a test run).
        var missed = new List<string>();
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        for (var level = 0; level < 8 && current is not null; level++)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(current.FullName, "Area11.Core", "Data", "Migrations"),
                         Path.Combine(current.FullName, "dotnet", "Area11.Core", "Data", "Migrations")
                     })
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                missed.Add(candidate);
            }

            current = current.Parent;
        }

        var besideTheBinary = Path.Combine(AppContext.BaseDirectory, "Migrations");

        if (Directory.Exists(besideTheBinary))
        {
            return besideTheBinary;
        }

        throw new DirectoryNotFoundException(
            "The SQL migrations folder could not be found. Searched:" + Environment.NewLine
            + string.Join(Environment.NewLine, missed) + Environment.NewLine + besideTheBinary);
    }
}
