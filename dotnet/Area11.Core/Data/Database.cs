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
    // Sync-only unit of work. A reentrant monitor rejects same-thread nested Tx and serializes
    // other callers on this Database instance; no ambient transaction is lent to another connection.
    private readonly object _transactionGate = new();

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
        // The mode is read here too: a main counter keeps the rollback journal on its OWN disk,
        // because the second counter opens that same file over the network (F-45).
        Plan = StoragePlan.For(FilePath, ShopConfig.Current.Mode);

        // A second counter must never invent an empty shop. If the shared file is not there, stop
        // with a sentence that names the path - creating a fresh database on the second PC would
        // look like "the shop lost everything" (F-45).
        if (Plan.IsNetwork && !File.Exists(FilePath))
        {
            throw new InvalidOperationException(
                "The shop's database was not found on the network at " + FilePath + Environment.NewLine +
                "Check that the main counter's PC is switched on, that its Area11 folder is shared, " +
                "and that this PC can open it in File Explorer. Nothing was created on this PC.");
        }

        if (!Plan.IsNetwork)
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }
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

    /// <summary>WAL on a local disk, rollback journal over the LAN - see <see cref="StoragePlan"/>.</summary>
    public StoragePlan Plan { get; }

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
                ? "PRAGMA busy_timeout=" + Plan.BusyTimeoutMs + ";"
                  + (Plan.SynchronousFull ? " PRAGMA synchronous=FULL;" : string.Empty)
                : "PRAGMA busy_timeout=5000; PRAGMA journal_mode=MEMORY; PRAGMA synchronous=OFF;";

            settings.ExecuteNonQuery();
        }

        // The journal mode is a property of the FILE, so it is asked for once per run, not on every
        // one of the dozens of connections a bill opens. Until it succeeds the next open tries again.
        //
        // WAL on a shared folder is not asked for at all: its `-shm` file is memory that two PCs
        // cannot share, and a dropped cable would leave a file SQLite cannot repair (F-45).
        if (_durable && Interlocked.CompareExchange(ref _journalReady, 1, 0) == 0)
        {
            using var journal = connection.CreateCommand();
            journal.CommandText = Plan.UseWal ? "PRAGMA journal_mode=WAL;" : "PRAGMA journal_mode=DELETE;";

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

    // Every command is associated with a transaction ONLY when that transaction owns this exact
    // connection. A separately opened connection must never receive a foreign transaction.
    private SqliteTransaction? TransactionFor(SqliteConnection connection) =>
        ReferenceEquals(_transaction?.Connection, connection) ? _transaction : null;

    private T Command<T>(SqliteConnection connection, Func<SqliteTransaction?, T> command)
    {
        lock (_transactionGate) return command(TransactionFor(connection));
    }

    public int Execute(SqliteConnection connection, string sql, object? parameters = null) =>
        Command(connection, transaction => connection.Execute(sql, parameters, transaction));

    public List<T> Query<T>(SqliteConnection connection, string sql, object? parameters = null) =>
        Command(connection, transaction => connection.Query<T>(sql, parameters, transaction).ToList());

    public T? QuerySingle<T>(SqliteConnection connection, string sql, object? parameters = null) =>
        Command(connection, transaction => connection.QueryFirstOrDefault<T>(sql, parameters, transaction));

    public object? Scalar(SqliteConnection connection, string sql, object? parameters = null) =>
        Command(connection, transaction => connection.ExecuteScalar(sql, parameters, transaction));

    public long NextId(SqliteConnection connection) =>
        Command(connection, transaction => connection.ExecuteScalar<long>("SELECT last_insert_rowid();", null, transaction));

    /// <summary>
    /// Synchronous read unit: reuse this caller's active Tx connection, otherwise own/dispose a new
    /// one. A borrowed connection must not be closed, returned, retained or used asynchronously.
    /// Never wait inside the callback for another thread to call this Database (the gate is held).
    /// This does not make an outside-Tx multi-query read a coherent SQLite snapshot.
    /// </summary>
    public T Read<T>(Func<SqliteConnection, T> read)
    {
        lock (_transactionGate)
        {
            if (_transaction is not null)
                return read(_transaction.Connection ?? throw new InvalidOperationException("The transaction connection is closed."));
            using var connection = Open();
            return read(connection);
        }
    }

    /// <summary>All-or-nothing synchronous write. Nested calls are a programming error.
    /// Do not return tasks or wait for another thread to call this Database while this gate is held.</summary>
    public T Tx<T>(Func<SqliteConnection, T> work)
    {
        lock (_transactionGate)
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
    }

    public void Tx(Action<SqliteConnection> work) =>
        Tx<object?>(connection =>
        {
            work(connection);
            return null;
        });
}
