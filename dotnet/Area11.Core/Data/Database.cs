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

    public Database(string? filePath = null)
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
            Pooling = true,
            ForeignKeys = true
        }.ToString();
    }

    public string FilePath { get; }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();

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
