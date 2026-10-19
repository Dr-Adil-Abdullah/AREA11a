using Area11.Core.Utils;
using Dapper;
using Microsoft.Data.Sqlite;

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
        var folder = migrationsFolder ?? Path.Combine(AppContext.BaseDirectory, "Migrations");
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"Migrations folder was not found: {folder}");
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

    private static bool IsApplied(SqliteConnection connection, SqliteTransaction? transaction, string id) =>
        connection.ExecuteScalar<long>(
            "SELECT COUNT(1) FROM _migrations WHERE id = @id;",
            new { id },
            transaction) > 0;
}
