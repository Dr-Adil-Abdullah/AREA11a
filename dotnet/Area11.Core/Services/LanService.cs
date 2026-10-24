using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;

namespace Area11.Core.Services;

/// <summary>
/// The second counter (F-45, Spec §34.9, D-12).
///
/// The shop's rule since the first day: **one computer now, a second counter later, and the screen
/// must not change when it comes.** This service is the "later": it writes the one line that tells a
/// PC where the shop's database is, and it refuses to pretend.
///
/// What it deliberately does **not** do:
///   • it never moves files. The main counter keeps its database where it is and shares the folder;
///     the second counter opens the same file over the network;
///   • it never switches the running program to another database. A change takes effect after a
///     restart, because open bills, cached settings and the open pages all belong to the old file;
///   • it never lets a second counter create an empty shop. If the shared file is missing, the
///     program stops with the path in the message (<see cref="Database"/>).
///
/// The honest warning lives in `docs/OWNER-DECISIONS.md` (D-62): SQLite is safe on a share only for
/// this shape - two counters, a wired network, one file, short transactions - and every bill is
/// still one transaction, so a lost cable refuses the bill instead of half writing it.
/// </summary>
public sealed class LanService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly LogService _log;
    private readonly string _iniPath;

    private ShopConfig _config;

    public LanService(
        Database db,
        AuditService audit,
        LogService log,
        ShopConfig? config = null,
        string? iniPath = null)
    {
        _db = db;
        _audit = audit;
        _log = log;
        _iniPath = string.IsNullOrWhiteSpace(iniPath) ? ShopConfig.DefaultIniPath : iniPath;
        _config = config ?? ShopConfig.Load(_iniPath);
    }

    /// <summary>The configuration this program started with (re-read after a Prepare/Join).</summary>
    public ShopConfig Config => _config;

    public string IniPath => _iniPath;

    public string DatabaseFolder => Path.GetDirectoryName(_db.FilePath) ?? string.Empty;

    // ============================== status ==============================

    /// <summary>Mode, path, journal, reachability and the last counters that connected.</summary>
    public ServiceResult<LanStatus> Status(User? user)
    {
        if (!IsOwner(user))
        {
            return ServiceResult<LanStatus>.Fail(
                "Only the owner can look at the second counter's connection.");
        }

        var status = new LanStatus
        {
            Mode = _config.Mode,
            ModeLabel = _config.ModeLabel,
            DatabaseFolder = DatabaseFolder,
            IniFile = _iniPath,
            IsNetwork = _db.Plan.IsNetwork,
            ThisPc = Environment.MachineName,
            RestartPending = IsRestartPending()
        };

        try
        {
            using var connection = _db.Open();

            status.JournalMode = (Convert.ToString(_db.Scalar(connection, "PRAGMA journal_mode;")) ?? "?")
                .Trim().ToUpperInvariant();

            var shopName = Convert.ToString(
                _db.Scalar(connection, "SELECT IFNULL(value, '') FROM settings WHERE key = 'store.name';"))
                ?? string.Empty;

            // Taking the write lock proves two things at once: the file is writable, and no other
            // counter is stuck holding it. The lock is dropped again at once - nothing is written.
            _db.Execute(connection, "BEGIN IMMEDIATE;");
            _db.Execute(connection, "ROLLBACK;");

            status.Reachable = true;
            status.CheckMessage = shopName.Trim().Length == 0
                ? "The file is readable and writable, but it has no shop name - check that this is the shop's own area11.db."
                : "The file is readable and writable. Shop: " + shopName.Trim() + ".";

            status.Connections = _db.Query<LanConnectionRow>(connection, @"
SELECT at AS at, IFNULL(user_name, '-') AS userName, IFNULL(details, '-') AS details
FROM audit_logs
WHERE action = 'lan_connect'
ORDER BY id DESC
LIMIT 10;");
        }
        catch (Exception exception)
        {
            status.Reachable = false;
            status.CheckMessage = "The file could not be reached: " + exception.Message;
            _log.Error("The shop's database could not be reached for the LAN status.", exception);
        }

        return ServiceResult<LanStatus>.Success(status);
    }

    // ============================== the main counter ==============================

    /// <summary>
    /// Turns **this** PC into the main counter: the database stays where it is, the journal leaves
    /// WAL (a share cannot carry WAL) and `shop.ini` records the folder to share.
    /// </summary>
    public ServiceResult PrepareAsMain(User? user)
    {
        if (!IsOwner(user))
        {
            return ServiceResult.Fail("Only the owner can change where the shop's database lives.");
        }

        if (_config.Mode == ShopMode.Second)
        {
            return ServiceResult.Fail(
                "This PC is set as the second counter. Run this on the main PC - the one that holds the database.");
        }

        try
        {
            using var connection = _db.Open();

            _db.Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE);");

            var mode = Convert.ToString(_db.Scalar(connection, "PRAGMA journal_mode=DELETE;"))
                ?.Trim().ToLowerInvariant();

            if (mode != "delete")
            {
                return ServiceResult.Fail(
                    "The database is still using the WAL journal, which cannot be shared over a network. " +
                    "Close every other window of Area11 on this PC (and the backup page), then press again. " +
                    "SQLite reported: " + (mode ?? "no answer"));
            }
        }
        catch (Exception exception)
        {
            _log.Error("The shop's database could not be prepared for sharing.", exception);
            return ServiceResult.Fail(
                "The database could not be prepared for sharing. Close the other windows of Area11 on this PC " +
                "and press again. " + exception.Message);
        }

        SaveConfig(new ShopConfig { Mode = ShopMode.Main, DataFolder = DatabaseFolder });
        _audit.RecordStandalone(user, "system", "lan_prepare", DatabaseFolder);
        _log.Info("This PC was prepared as the main counter; the shop's folder is " + DatabaseFolder + ".");

        return ServiceResult.Success();
    }

    // ============================== the second counter ==============================

    /// <summary>
    /// Points this PC at the main counter's folder. The file is opened read-write and a line is
    /// written into the main counter's black box, so the owner can see the second PC arrive.
    /// </summary>
    public ServiceResult JoinAsSecond(User? user, string? sharePath)
    {
        if (!IsOwner(user))
        {
            return ServiceResult.Fail("Only the owner can change where the shop's database lives.");
        }

        var problem = ShopConfig.Validate(ShopMode.Second, sharePath);
        if (problem is not null)
        {
            return ServiceResult.Fail(problem);
        }

        var folder = ShopConfig.NormalizeFolder(sharePath);
        var file = Path.Combine(folder, "area11.db");

        if (!File.Exists(file))
        {
            return ServiceResult.Fail(
                "No area11.db was found in " + folder + "." + Environment.NewLine +
                "Check the main counter: press \\\"Prepare this PC as the main counter\\\" there, share its " +
                "Area11 folder, and make sure this PC can open that folder in File Explorer.");
        }

        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = file,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                DefaultTimeout = 15
            }.ToString());

            connection.Open();

            // The cheapest honest identity check: an Area11 database always has the `settings` table.
            var tables = Convert.ToInt64(connection.ExecuteScalar<long>(
                "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'settings';"));

            if (tables == 0)
            {
                return ServiceResult.Fail(
                    "That file is not an Area11 database (it has no settings table). Nothing was changed.");
            }

            // A real write, so "read-only" or "no permission on the share" is caught here and not at
            // the first bill. The line is also the owner's proof that the second PC arrived.
            connection.Execute(@"
INSERT INTO audit_logs(at, user_id, user_name, action, module, entity, entity_id, details)
VALUES(@at, @userId, @userName, 'lan_connect', 'system', 'lan', @pc, @details);",
                new
                {
                    at = Clock.Now(),
                    userId = user!.Id,
                    userName = user.Name,
                    pc = Environment.MachineName,
                    details = Environment.MachineName
                });
        }
        catch (Exception exception)
        {
            _log.Error("The second counter could not open the main counter's database: " + file, exception);
            return ServiceResult.Fail(
                "The main counter's file was found but could not be opened for writing. " +
                "On the main PC, share the folder with \\\"Change\\\" permission for this PC's Windows user. " +
                exception.Message);
        }

        SaveConfig(new ShopConfig { Mode = ShopMode.Second, DataFolder = folder });
        _log.Info("This PC joined the main counter at " + folder + "; restart the program to use it.");

        return ServiceResult.Success();
    }

    // ============================== a quick test ==============================

    /// <summary>Takes the write lock and drops it again, and records the test in the black box.</summary>
    public ServiceResult TestConnection(User? user)
    {
        if (!IsOwner(user))
        {
            return ServiceResult.Fail("Only the owner can test the shop's database connection.");
        }

        try
        {
            using var connection = _db.Open();

            _db.Execute(connection, "BEGIN IMMEDIATE;");
            _db.Execute(connection, "ROLLBACK;");

            _audit.Record(connection, user, "system", "lan_test", "system",
                Environment.MachineName, details: _db.FilePath);

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error("The database connection test failed.", exception);
            return ServiceResult.Fail("The database could not be locked for writing. " + exception.Message);
        }
    }

    // ============================== helpers ==============================

    private static bool IsOwner(User? user) => user is { IsOwner: true };

    private bool IsRestartPending()
    {
        var configured = ShopConfig.NormalizeFolder(_config.DataFolder);

        return configured.Length > 0
               && !string.Equals(configured, ShopConfig.NormalizeFolder(DatabaseFolder),
                   StringComparison.OrdinalIgnoreCase);
    }

    private void SaveConfig(ShopConfig config)
    {
        ShopConfig.Save(config, _iniPath);
        _config = config;
    }
}
