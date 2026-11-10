using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;
using System.IO;

namespace Area11.Core.Services;

/// <summary>
/// Rebuilds an explicitly marked, isolated test database from the real migrations.
///
/// This is deliberately **not** a demo-shop generator and is not connected to the desktop shell.
/// D-77 still stands: no invented bills, purchases, payments or stock rows are written. The normal
/// first-start owner/categories/settings are seeded only so the empty copy has the same baseline as
/// a fresh installation.
///
/// Three accident barriers are required before an existing file can be replaced: the exact
/// confirmation phrase, a marker owned by this service, and a path that is neither the real data
/// root nor a network share. An existing unmarked folder is never adopted. A SQLite consistent
/// technical snapshot is made before every replacement and is never automatically trimmed.
/// </summary>
public sealed class IsolatedTestCopyService
{
    public const string ConfirmationPhrase = "REBUILD ISOLATED TEST COPY";
    public const string DatabaseFileName = "area11-test.db";
    public const string MarkerFileName = ".area11-isolated-test-copy";

    public const string MarkerText =
        "AREA11 ISOLATED TEST COPY - NO GENUINE BOOKS\n" +
        "This marker permits schema-only test rebuilds in this folder.\n";

    private readonly string _workspaceFolder;
    private readonly string _migrationsFolder;

    public IsolatedTestCopyService(string workspaceFolder, string migrationsFolder)
    {
        _workspaceFolder = workspaceFolder;
        _migrationsFolder = migrationsFolder;
    }

    /// <summary>
    /// Creates the first empty test copy or replaces an existing marked one. Any failure before the
    /// same-volume swap leaves the old copy alone; a post-swap failure is rolled back from the named
    /// technical snapshot whenever an old copy existed.
    /// </summary>
    public ServiceResult<IsolatedTestRebuildResult> Rebuild(string? confirmation)
    {
        string? staged = null;
        string? databasePath = null;
        string? technicalBackupPath = null;
        var databaseReplaced = false;
        var hadPreviousCopy = false;
        LogService? log = null;

        try
        {
            if (!string.Equals(confirmation, ConfirmationPhrase, StringComparison.Ordinal))
            {
                return ServiceResult<IsolatedTestRebuildResult>.Fail(
                    "Nothing was changed. Type the exact isolated-test rebuild confirmation.");
            }

            var workspace = Path.GetFullPath(_workspaceFolder);
            var migrations = Path.GetFullPath(_migrationsFolder);

            if (IsProtectedLiveFolder(workspace))
            {
                return ServiceResult<IsolatedTestRebuildResult>.Fail(
                    "The genuine Area11 data folder is never a test copy. Nothing was changed.");
            }

            if (ShopConfig.IsUnc(workspace))
            {
                return ServiceResult<IsolatedTestRebuildResult>.Fail(
                    "An isolated test copy must be on a local disk, not a network share.");
            }

            if (IsFileSystemRoot(workspace))
            {
                return ServiceResult<IsolatedTestRebuildResult>.Fail(
                    "Choose a dedicated test folder, not a disk root. Nothing was changed.");
            }

            PrepareWorkspace(workspace);

            databasePath = Path.Combine(workspace, DatabaseFileName);
            if (string.Equals(Path.GetFullPath(databasePath), Path.GetFullPath(AppPaths.DatabaseFile),
                    StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<IsolatedTestRebuildResult>.Fail(
                    "The genuine Area11 database is never rebuilt as test data.");
            }

            log = new LogService(Path.Combine(workspace, "logs"));
            hadPreviousCopy = File.Exists(databasePath);

            if (hadPreviousCopy)
            {
                var previous = new Database(databasePath, pooling: false, durable: false);
                var previousAudit = new AuditService(previous, log);
                var permissions = new PermissionService(previous, log, previousAudit);
                var settings = new SettingsService(previous, previousAudit, log, permissions);
                var backups = new BackupService(previous, settings, previousAudit, permissions, log,
                    photosFolder: Path.Combine(workspace, "photos"),
                    backupsFolder: Path.Combine(workspace, "backups"));

                // SafetyCopy uses SQLite VACUUM INTO, not a possibly torn file copy. Unlike an
                // automatic daily backup it has no retention trimming: every pre-rebuild copy stays.
                var snapshot = backups.SafetyCopy("test-rebuild");
                if (!snapshot.Ok || string.IsNullOrWhiteSpace(snapshot.Value) || !File.Exists(snapshot.Value))
                {
                    return ServiceResult<IsolatedTestRebuildResult>.Fail(
                        "The technical backup failed, so the isolated test copy was not rebuilt. " +
                        (snapshot.Error ?? "See the test-copy log."));
                }

                technicalBackupPath = snapshot.Value;
            }

            staged = Path.Combine(workspace, $".area11-test-{Guid.NewGuid():N}.db");
            var fresh = new Database(staged, pooling: false, durable: false);
            var applied = new MigrationRunner(fresh, log).ApplyPending(migrations);
            var freshAudit = new AuditService(fresh, log);
            var freshPermissions = new PermissionService(fresh, log, freshAudit);
            var freshSettings = new SettingsService(fresh, freshAudit, log, freshPermissions);
            freshSettings.EnsureDefaults();
            new SeedService(fresh, log).Seed();
            freshSettings.Reload();

            var backupDescription = string.IsNullOrEmpty(technicalBackupPath)
                ? "first empty test copy; no previous database"
                : "technical backup before replacement: " + Path.GetFileName(technicalBackupPath);
            fresh.Tx(connection => freshAudit.Record(connection, null, "Test copy", "test_schema_rebuild",
                details: "Isolated schema-only rebuild; no invented business rows. " + backupDescription));

            ValidateFreshDatabase(fresh, applied, migrations);
            var tableCount = Convert.ToInt32(fresh.Tx(connection => fresh.Scalar(connection,
                "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table';")) ?? 0);

            File.Move(staged, databasePath, overwrite: true);
            databaseReplaced = true;
            staged = null;

            // A failed copy/move leaves the current file and its sidecars alone. Only after the
            // same-volume swap succeeds are the old companions stale; the outer catch restores the
            // snapshot if this cleanup fails, and reports an uncertain state if recovery also fails.
            RemoveSidecars(databasePath);

            log.Info($"Isolated test schema rebuilt: {databasePath}; {backupDescription}.");
            return ServiceResult<IsolatedTestRebuildResult>.Success(new IsolatedTestRebuildResult
            {
                DatabasePath = databasePath,
                TechnicalBackupPath = technicalBackupPath ?? string.Empty,
                MigrationsApplied = applied,
                TableCount = tableCount,
                HadPreviousCopy = hadPreviousCopy
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                         or SqliteException or InvalidOperationException
                                         or ArgumentException or NotSupportedException)
        {
            var recovered = !databaseReplaced ||
                (databasePath is not null && TryRecoverPrevious(databasePath, technicalBackupPath,
                    hadPreviousCopy, log));
            log?.Error("The isolated test schema could not be rebuilt safely.", exception);

            var state = !databaseReplaced
                ? "The previous isolated copy was not replaced."
                : !recovered
                    ? "The database state is uncertain; do not use it until the technical backup is restored manually."
                    : hadPreviousCopy
                        ? "The previous isolated copy was restored from its technical backup."
                        : "The incomplete new copy was removed; no previous database existed.";
            var backup = string.IsNullOrEmpty(technicalBackupPath)
                ? "No previous-copy technical backup was created."
                : "Technical backup: " + technicalBackupPath + ".";

            return ServiceResult<IsolatedTestRebuildResult>.Fail(
                $"The isolated test schema was not rebuilt safely. {state} {backup} {exception.Message}");
        }
        finally
        {
            TryDelete(staged);
        }
    }

    private static bool IsProtectedLiveFolder(string workspace)
    {
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace));
        foreach (var protectedFolder in new[] { AppPaths.DataRoot, AppPaths.DatabaseFolder })
        {
            var protectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedFolder));
            if (string.Equals(candidate, protectedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFileSystemRoot(string folder)
    {
        var root = Path.GetPathRoot(folder);
        return !string.IsNullOrWhiteSpace(root) && string.Equals(
            Path.TrimEndingDirectorySeparator(folder), Path.TrimEndingDirectorySeparator(root),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void PrepareWorkspace(string workspace)
    {
        if (Directory.Exists(workspace))
        {
            var attributes = File.GetAttributes(workspace);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "A linked/reparse-point folder cannot be used as an isolated test copy.");
            }
        }
        else
        {
            Directory.CreateDirectory(workspace);
        }

        var marker = Path.Combine(workspace, MarkerFileName);
        if (File.Exists(marker))
        {
            if (!string.Equals(File.ReadAllText(marker), MarkerText, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The test-copy marker is not valid; nothing was replaced.");
            }

            return;
        }

        // Never adopt a folder that already contains an unmarked database or anybody else's files.
        if (Directory.EnumerateFileSystemEntries(workspace).Any())
        {
            throw new InvalidOperationException(
                "The folder is not empty and has no isolated-test marker; nothing was changed.");
        }

        File.WriteAllText(marker, MarkerText);
    }

    private static void ValidateFreshDatabase(Database database, int applied, string migrationsFolder)
    {
        var migrationFiles = Directory.GetFiles(migrationsFolder, "*.sql").Length;
        if (migrationFiles == 0 || applied != migrationFiles)
        {
            throw new InvalidOperationException(
                $"A fresh rebuild must apply every migration exactly once ({applied}/{migrationFiles}).");
        }

        using var connection = database.Open();
        using (var quickCheck = connection.CreateCommand())
        {
            quickCheck.CommandText = "PRAGMA quick_check;";
            var answer = quickCheck.ExecuteScalar()?.ToString();
            if (!string.Equals(answer, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SQLite quick_check failed for the staged test copy.");
            }
        }

        using (var foreignKeys = connection.CreateCommand())
        {
            foreignKeys.CommandText = "PRAGMA foreign_key_check;";
            if (foreignKeys.ExecuteScalar() is not null)
            {
                throw new InvalidOperationException("The staged test copy has a foreign-key violation.");
            }
        }
    }

    private static bool TryRecoverPrevious(string databasePath, string? technicalBackupPath,
        bool hadPreviousCopy, LogService? log)
    {
        string? recovery = null;

        try
        {
            if (!hadPreviousCopy)
            {
                File.Delete(databasePath);
                RemoveSidecars(databasePath);
                return true;
            }

            if (string.IsNullOrWhiteSpace(technicalBackupPath) || !File.Exists(technicalBackupPath))
            {
                return false;
            }

            var workspace = Path.GetDirectoryName(databasePath)
                ?? throw new InvalidOperationException("The isolated database has no containing folder.");
            recovery = Path.Combine(workspace, $".area11-recovery-{Guid.NewGuid():N}.db");
            File.Copy(technicalBackupPath, recovery, overwrite: true);
            File.Move(recovery, databasePath, overwrite: true);
            recovery = null;
            RemoveSidecars(databasePath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                         or InvalidOperationException or ArgumentException
                                         or NotSupportedException)
        {
            log?.Error("The isolated test-copy recovery also failed; the database state is uncertain.", exception);
            return false;
        }
        finally
        {
            TryDelete(recovery);
        }
    }

    private static void RemoveSidecars(string databasePath)
    {
        File.Delete(databasePath + "-wal");
        File.Delete(databasePath + "-shm");
        File.Delete(databasePath + "-journal");
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A temporary staged file is harmless. The operation's actual error is more useful.
        }
    }
}
