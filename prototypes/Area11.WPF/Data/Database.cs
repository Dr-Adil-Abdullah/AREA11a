using Microsoft.Data.Sqlite;
using System.IO;

namespace Area11.WPF.Data;

/// <summary>
/// Owns the single SQLite file (the shop's source of truth) and every read/write transaction.
/// A local file database is what makes the POS work with no internet connection.
/// </summary>
public sealed class Database
{
    private readonly string _connectionString;
    private readonly string _migrationsFolder;

    public Database(string databasePath, string? migrationsFolder = null)
    {
        var folder = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();

        _migrationsFolder = migrationsFolder ?? Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();

        return connection;
    }

    /// <summary>Applies every not-yet-applied .sql file once, in name order. Migrations are never edited after release.</summary>
    public void Migrate()
    {
        if (!Directory.Exists(_migrationsFolder))
        {
            throw new DirectoryNotFoundException($"Migrations folder not found: {_migrationsFolder}");
        }

        using var connection = Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE IF NOT EXISTS _migrations (name TEXT PRIMARY KEY, applied_at TEXT NOT NULL);";
            create.ExecuteNonQuery();
        }

        foreach (var file in Directory.GetFiles(_migrationsFolder, "*.sql").OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);

            using (var check = connection.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(1) FROM _migrations WHERE name = $name;";
                check.Parameters.AddWithValue("$name", name);
                if (Convert.ToInt64(check.ExecuteScalar() ?? 0L) > 0)
                {
                    continue;
                }
            }

            using var transaction = connection.BeginTransaction();

            using (var apply = connection.CreateCommand())
            {
                apply.Transaction = transaction;
                apply.CommandText = File.ReadAllText(file);
                apply.ExecuteNonQuery();
            }

            using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText = "INSERT INTO _migrations(name, applied_at) VALUES($name, $at);";
                record.Parameters.AddWithValue("$name", name);
                record.Parameters.AddWithValue("$at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                record.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    /// <summary>Runs work inside one transaction so a half-written bill can never reach the database.</summary>
    public T Tx<T>(Func<SqliteConnection, T> work)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = work(connection);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void Tx(Action<SqliteConnection> work)
    {
        Tx<object?>(connection =>
        {
            work(connection);
            return null;
        });
    }

    public List<T> Query<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] args)
    {
        using var command = CreateCommand(connection, sql, args);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    public T? Scalar<T>(SqliteConnection connection, string sql, params (string Name, object? Value)[] args)
    {
        using var command = CreateCommand(connection, sql, args);
        var raw = command.ExecuteScalar();
        if (raw is null || raw is DBNull)
        {
            return default;
        }

        if (raw is T typed)
        {
            return typed;
        }

        return (T)Convert.ChangeType(raw, typeof(T));
    }

    public int Exec(SqliteConnection connection, string sql, params (string Name, object? Value)[] args)
    {
        using var command = CreateCommand(connection, sql, args);
        return command.ExecuteNonQuery();
    }

    public long LastInsertId(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(command.ExecuteScalar() ?? 0L);
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, string sql, (string Name, object? Value)[] args)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in args)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }
}
