using Area11.Core.Utils;
using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;

namespace Area11.Core.Data;

/// <summary>
/// Append-only migrations (Spec 5.1.6). Each file runs once, inside BEGIN IMMEDIATE, and the
/// ledger is checked again after the write lock is taken — this is the fix for the original
/// "duplicate column name" crash when two processes opened a new database together.
/// </summary>
public sealed class MigrationRunner
{
    private readonly Database _db;
    private readonly LogService _log;

    public MigrationRunner(Database db, LogService log)
    {
        _db = db;
        _log = log;
    }

    public int ApplyPending(string? migrationsFolder = null)
    {
        var folder = migrationsFolder ?? FindMigrationsFolder();

        if (folder is null)
        {
            // Name every place that was tried. "Migrations folder was not found: <one path>" left the
            // owner with nothing to check; this message says exactly which folders were searched.
            throw new DirectoryNotFoundException(
                "The SQL migration files were not found. Searched:" + Environment.NewLine +
                string.Join(Environment.NewLine, CandidateFolders().Select(path => "  " + path)));
        }

        var files = Directory.GetFiles(folder, "*.sql")
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            throw new InvalidOperationException($"No .sql migrations were found in {folder}.");
        }

        using var connection = _db.Open();
        connection.Execute(
            "CREATE TABLE IF NOT EXISTS _migrations (id TEXT PRIMARY KEY, applied_at TEXT NOT NULL DEFAULT (datetime('now','localtime')));");

        var applied = 0;

        foreach (var file in files)
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (IsApplied(connection, null, id))
            {
                continue;
            }

            using var transaction = connection.BeginTransaction(deferred: false);

            try
            {
                if (!IsApplied(connection, transaction, id))
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = File.ReadAllText(file);
                    command.ExecuteNonQuery();

                    connection.Execute(
                        "INSERT INTO _migrations(id) VALUES(@id);",
                        new { id },
                        transaction);

                    _log.Info($"Migration applied: {id}");
                    applied++;
                }

                transaction.Commit();
            }
            catch (Exception exception)
            {
                transaction.Rollback();
                _log.Error($"Migration failed: {id}", exception);
                throw;
            }
        }

        return applied;
    }

    /// <summary>
    /// The folder that holds the .sql files: next to the program first, then the source tree (which
    /// is where `dotnet run` lives during development). Null when neither exists.
    /// </summary>
    private static string? FindMigrationsFolder() =>
        CandidateFolders().FirstOrDefault(Directory.Exists);

    private static IEnumerable<string> CandidateFolders()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Migrations");

        // A `dotnet run` from the repository: walk up until the source folder is in reach. This
        // costs nothing on the shop PC (the loop ends after a few steps) and it means a missing
        // copy step can never stop the owner again.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        for (var step = 0; step < 8 && directory is not null; step++)
        {
            var candidate = Path.Combine(directory.FullName, "Area11.Core", "Data", "Migrations");

            if (Directory.Exists(candidate))
            {
                yield return candidate;
            }

            candidate = Path.Combine(directory.FullName, "dotnet", "Area11.Core", "Data", "Migrations");

            if (Directory.Exists(candidate))
            {
                yield return candidate;
            }

            directory = directory.Parent;
        }
    }

    private static bool IsApplied(SqliteConnection connection, SqliteTransaction? transaction, string id) =>
        connection.ExecuteScalar<long>(
            "SELECT COUNT(1) FROM _migrations WHERE id = @id;",
            new { id },
            transaction) > 0;
}
