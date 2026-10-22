using Area11.Core.Utils;
using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;

namespace Area11.Core.Data;

/// <summary>
/// The only place that knows how to open SQLite. Services receive an open connection and use
/// Execute/Query/Scalar, so no SQL connection is ever created outside the Core layer
/// (Spec 4.4.2). One active transaction per call keeps every business action atomic.
/// </summary>
public sealed class Database
{
    private readonly string _connectionString;
    private SqliteTransaction? _transaction;

    static Database()
    {
        // snake_case columns map to PascalCase properties without aliasing every column.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    /// <param name="filePath">The database file; the shop's own file when null.</param>
    /// <param name="pooling">
    /// True for the application (a bill must not wait for a connection). False for the tests: a
    /// pooled handle keeps the file open on Windows, so a throw-away database could never be deleted.
    /// </param>
    public Database(string? filePath = null, bool pooling = true)
    {
        FilePath = string.IsNullOrWhiteSpace(filePath) ? AppPaths.DatabaseFile : filePath;

        var folder = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = pooling,
            ForeignKeys = true
        }.ToString();
    }

    public string FilePath { get; }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // busy_timeout FIRST: if the file is momentarily busy (another copy of the shop program,
        // antivirus scanning the folder) the next statement waits instead of failing. Then WAL is
        // best effort - after the first start the database already IS in WAL, and a checkpoint that
        // cannot run right now must not stop a bill from being saved.
        using (var settings = connection.CreateCommand())
        {
            settings.CommandText = "PRAGMA busy_timeout=5000;";
            settings.ExecuteNonQuery();
        }

        using (var journal = connection.CreateCommand())
        {
            journal.CommandText = "PRAGMA journal_mode=WAL;";

            try
            {
                journal.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Busy or read-only for a moment. The mode is remembered in the file; the next open
                // sets it again, and every write below is still safe in the current mode.
            }
        }

        return connection;
    }

    public int Execute(SqliteConnection connection, string sql, object? parameters = null) =>
        connection.Execute(sql, parameters, _transaction);

    public List<T> Query<T>(SqliteConnection connection, string sql, object? parameters = null) =>
        connection.Query<T>(sql, parameters, _transaction).ToList();

    public T? QuerySingle<T>(SqliteConnection connection, string sql, object? parameters = null) =>
        connection.QueryFirstOrDefault<T>(sql, parameters, _transaction);

    public object? Scalar(SqliteConnection connection, string sql, object? parameters = null) =>
        connection.ExecuteScalar(sql, parameters, _transaction);

    public long NextId(SqliteConnection connection) =>
        connection.ExecuteScalar<long>("SELECT last_insert_rowid();", null, _transaction);

    /// <summary>All-or-nothing write. Nested calls are a programming error, not a silent bug.</summary>
    public T Tx<T>(Func<SqliteConnection, T> work)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A database transaction is already open; do not nest Tx calls.");
        }

        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        _transaction = transaction;

        try
        {
            var result = work(connection);
            transaction.Commit();
            return result;
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
                // The connection already rolled back (for example a failing statement);
                // the original exception is the useful one, so it is rethrown below.
            }

            throw;
        }
        finally
        {
            _transaction = null;
        }
    }

    public void Tx(Action<SqliteConnection> work) =>
        Tx<object?>(connection =>
        {
            work(connection);
            return null;
        });
}
