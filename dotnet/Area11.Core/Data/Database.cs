using Area11.Core.Utils;
using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Threading;

namespace Area11.Core.Data;

/// <summary>
/// The only place that knows how to open SQLite. Services receive an open connection and use
/// Execute/Query/Scalar, so no SQL connection is ever created outside the Core layer
/// (Spec 4.4.2). One active transaction per call keeps every business action atomic.
/// </summary>
public sealed class Database
{
    private readonly string _connectionString;
    private readonly bool _durable;
    private int _journalReady;
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
    /// <param name="durable">
    /// True for the shop: WAL plus a sync on every commit, so a power cut cannot lose a bill. False
    /// for the tests, where the file is deleted at the end anyway: journal in memory and no sync.
    /// The schema, the SQL and the transactions are identical - only the safety against a power cut
    /// is dropped, and that is what made a 271-test run take five minutes on the shop PC.
    /// </param>
    public Database(string? filePath = null, bool pooling = true, bool durable = true)
    {
        _durable = durable;

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
        // antivirus scanning the folder) the next statement waits instead of failing.
        //
        // A throw-away test file gets MEMORY + OFF on every connection: those two are per-connection
        // settings, and they stop SQLite creating and deleting a -wal / -shm / -journal file around
        // every single query - which is exactly what made a run of 271 tests sit for five minutes.
        using (var settings = connection.CreateCommand())
        {
            settings.CommandText = _durable
                ? "PRAGMA busy_timeout=5000;"
                : "PRAGMA busy_timeout=5000; PRAGMA journal_mode=MEMORY; PRAGMA synchronous=OFF;";

            settings.ExecuteNonQuery();
        }

        // WAL is a property of the FILE, so it is asked for once per run, not on every one of the
        // dozens of connections a bill opens. Until it succeeds the next open tries again.
        if (_durable && Interlocked.CompareExchange(ref _journalReady, 1, 0) == 0)
        {
            using var journal = connection.CreateCommand();
            journal.CommandText = "PRAGMA journal_mode=WAL;";

            try
            {
                journal.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Busy or read-only for a moment; the file keeps its current mode, every write is
                // still safe, and the next open asks again.
                Interlocked.Exchange(ref _journalReady, 0);
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
