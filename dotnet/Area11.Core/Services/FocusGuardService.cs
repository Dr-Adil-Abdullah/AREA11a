using System.Globalization;
using System.Text.Json;
using Area11.Core.Data;
using Microsoft.Data.Sqlite;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Best-effort shared session markers and away episodes, independent of the screen-lock policy.
/// Atomic stamps are not PC/run identity, kill proof, or exactly-once delivery. Writes can block.
/// Checked results distinguish confirmed save, confirmed no-commit, and uncertainty; no hidden retry.
/// </summary>
public sealed class FocusGuardService
{
    public const string GuardKey = "lockdown.focusGuard";

    private const string LastUserKey = "session.lastUser";
    private const string LastStartKey = "session.lastStart";
    private const string LastSeenKey = "session.lastSeen";
    private const string CleanExitKey = "session.cleanExit";

    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly LogService _log;

    public FocusGuardService(SettingsService settings, AuditService audit, LogService log)
    {
        _settings = settings;
        _audit = audit;
        _log = log;
    }

    private readonly object _stateGate = new();
    private bool? _lastVerifiedEnabled;
    private bool _readWarning;

    /// <summary>Owner decision D-118/A: retain last VERIFIED persisted choice on read failure;
    /// before the first verification use ON. Never mistakes a cached/default OFF for evidence.</summary>
    public bool Enabled => ReadStatus().Enabled;

    /// <summary>One current read and one coherent status. No writes, repair, audit, or durable state.
    /// A valid later read (including OFF) recovers normally. State belongs to this app service only.</summary>
    public FocusGuardStatus ReadStatus()
    {
        var current = _settings.ReadCurrentFocusGuard();
        lock (_stateGate)
        {
            if (current.Ok)
            {
                _lastVerifiedEnabled = current.Value;
                _readWarning = false;
                return new FocusGuardStatus(current.Value, true, string.Empty);
            }
            if (!_readWarning)
            {
                _log.Warn("Screen-lock setting is unverified; keeping the last verified choice, or ON before the first verification.");
                _readWarning = true;
            }
            var enabled = _lastVerifiedEnabled ?? true;
            var choice = _lastVerifiedEnabled.HasValue
                ? "Keeping the last verified setting " + (enabled ? "ON." : "OFF.")
                : "No verified setting yet; using ON temporarily.";
            return new FocusGuardStatus(enabled, false,
                "Screen-lock setting could not be verified. " + choice + " Ask the owner to review Settings and the database connection. This is not a Windows security lock.");
        }
    }

    private readonly object _telemetryGate = new();
    private bool _sessionStarted;
    private bool _telemetryUncertain;
    private string _telemetryNotice = string.Empty;

    /// <summary>Sticky warning for this service lifetime. A later heartbeat cannot repair a lost event.</summary>
    public string TelemetryNotice { get { lock (_telemetryGate) return _telemetryNotice; } }

    /// <summary>Compatibility text only, not a save receipt. New callers use RecordAwayChecked.</summary>
    public string RecordAway(User? user, string leftAt, string backAt)
    {
        var result = RecordAwayChecked(user, leftAt, backAt);
        return result.Value ?? AwayDetails(user, leftAt, backAt);
    }

    private static string AwayDetails(User? user, string leftAt, string backAt)
    {
        var name = string.IsNullOrWhiteSpace(user?.Name) ? "Someone" : user!.Name.Trim();
        return name + " left the screen for " + DescribeGap(leftAt, backAt) +
            " (" + leftAt + " to " + backAt + ").";
    }

    /// <summary>One attempt per episode; unlike RecordStandalone, audit errors are observable.
    /// Never replay an unknown result. No durable deduplication is provided.</summary>
    public ServiceResult<string?> RecordAwayChecked(User? user, string leftAt, string backAt)
    {
        lock (_telemetryGate)
        {
            var details = AwayDetails(user, leftAt, backAt);
            return WriteTelemetry("Away episode", false, (db, connection, values) =>
            {
                RecordTelemetryAudit(db, connection, user, "screen_left", details);
                return details;
            });
        }
    }

    /// <summary>Compatibility report only. Null means no report OR failure; use checked API for outcome.</summary>
    public string? CheckPreviousRun() => CheckPreviousRunChecked().Value;

    /// <summary>Current strict reads, previous-marker audit and all new stamps in ONE transaction.
    /// Shared markers may belong to another active counter; the report explicitly says so.
    /// Calls represent startup attempts, not a timer/retry loop.</summary>
    public ServiceResult<string?> CheckPreviousRunChecked()
    {
        lock (_telemetryGate)
        {
            _sessionStarted = false;
            var result = WriteTelemetry("Startup telemetry", true, (db, connection, values) =>
            {
                var lastStart = ReadString(db, connection, LastStartKey);
                var lastUser = ReadString(db, connection, LastUserKey);
                var lastSeen = ReadString(db, connection, LastSeenKey);
                var rawClean = ReadRaw(db, connection, CleanExitKey);
                var clean = JsonSerializer.Deserialize<bool>(rawClean);
                var now = Clock.Now();
                string? details = null;
                if (lastStart.Trim().Length > 0 && !clean)
                {
                    var seen = lastSeen.Trim().Length > 0 ? lastSeen.Trim() : lastStart.Trim();
                    var who = lastUser.Trim().Length > 0 ? lastUser.Trim() : "The previous run";
                    details = "Unverified shared marker: " + who + " ended without closing - last seen " + seen +
                        ", back now (away " + DescribeGap(seen, now) + "). " +
                        "This is not proof of a forced close or of which PC/user ended a run; another counter may still be active.";
                    RecordTelemetryAudit(db, connection, null, "session_killed", details);
                }
                Put(values, LastStartKey, now);
                Put(values, LastSeenKey, now);
                Put(values, LastUserKey, string.Empty); // Never inherit the preceding operator into a new start.
                Put(values, CleanExitKey, false);
                return details;
            });
            if (result.Ok) _sessionStarted = true;
            return result;
        }
    }

    /// <summary>Compatibility entry; failures still set the visible sticky warning.</summary>
    public void MarkCleanExit() => MarkCleanExitChecked();

    /// <summary>Normal-exit marker, not proof about any other process sharing this database.</summary>
    public ServiceResult<string?> MarkCleanExitChecked()
    {
        lock (_telemetryGate)
        {
            var result = WriteTelemetry("Clean exit", true, (db, connection, values) =>
            {
                Put(values, LastSeenKey, Clock.Now());
                Put(values, CleanExitKey, true);
                return null;
            }, requireStarted: true);
            // No late timer may mark this service alive again, even when exit stamping failed.
            _sessionStarted = false;
            return result;
        }
    }

    /// <summary>Compatibility entry; synchronous DB work can wait for SQLite.</summary>
    public void Heartbeat(string? userName) => HeartbeatChecked(userName);

    /// <summary>Atomic seen/user pair. Empty names clear stale attribution. No startup => no stamps.</summary>
    public ServiceResult<string?> HeartbeatChecked(string? userName)
    {
        lock (_telemetryGate)
        {
            return WriteTelemetry("Heartbeat", true, (db, connection, values) =>
            {
                Put(values, LastSeenKey, Clock.Now());
                Put(values, LastUserKey, userName?.Trim() ?? string.Empty);
                return null;
            }, requireStarted: true);
        }
    }

    private static void Put(Dictionary<string, string> values, string key, object value) =>
        values[key] = JsonSerializer.Serialize(value);

    private static string ReadRaw(Database db, SqliteConnection connection, string key) =>
        db.QuerySingle<string>(connection, "SELECT value FROM settings WHERE key=@key;", new { key })
            ?? throw new InvalidOperationException("Missing telemetry value.");

    private static string ReadString(Database db, SqliteConnection connection, string key) =>
        JsonSerializer.Deserialize<string>(ReadRaw(db, connection, key))
            ?? throw new InvalidOperationException("Invalid telemetry string.");

    private void RecordTelemetryAudit(Database db, SqliteConnection connection, User? actor, string action, string details)
    {
        _audit.Record(connection, actor, Modules.Pos, action, details: details);
        if (Convert.ToInt64(db.Scalar(connection, "SELECT changes();") ?? 0L) != 1L)
            throw new InvalidOperationException("Telemetry audit was not inserted.");
    }

    // Called under _telemetryGate. No generic caller-controlled keys or security-setting writes.
    private ServiceResult<string?> WriteTelemetry(string operation, bool stamps,
        Func<Database, SqliteConnection, Dictionary<string, string>, string?> work, bool requireStarted = false)
    {
        if (_telemetryUncertain || (requireStarted && !_sessionStarted))
        {
            var message = "Session telemetry is paused: a startup was not confirmed, the run ended, or an earlier write is unverified. No retry was made.";
            WarnTelemetry(message);
            return new ServiceResult<string?> { Error = message, NoCommitConfirmed = true };
        }
        var outcome = new TransactionOutcome();
        var committed = false;
        string? details = null;
        try
        {
            var db = _settings.SessionTelemetryDatabase;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            details = db.Tx(connection =>
            {
                var line = work(db, connection, values);
                foreach (var pair in values)
                {
                    var changed = db.Execute(connection, @"
INSERT INTO settings(key, value, updated_at) VALUES(@key, @value, @at)
ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;",
                        new { key = pair.Key, value = pair.Value, at = Clock.Now() });
                    if (changed != 1) throw new InvalidOperationException("Telemetry stamp was not written.");
                }
                // Verify the final set, not only the last statement (including trigger interference).
                foreach (var pair in values)
                    if (ReadRaw(db, connection, pair.Key) != pair.Value)
                        throw new InvalidOperationException("Telemetry stamp readback did not match.");
                return line;
            }, outcome);
            committed = true; // DB receipt is established BEFORE local cache publication.
            _settings.PublishSessionTelemetry(values);
            return ServiceResult<string?>.Success(details);
        }
        catch (Exception exception)
        {
            _log.Error(operation + " did not finish.", exception);
            var message = committed ? "SAVED. " + operation + " was stored, but local cache refresh failed."
                : outcome.NoCommitConfirmed ? operation + " was not saved; no commit was made."
                : operation + " save is unverified. Do not retry the same event; session telemetry is paused.";
            if (!committed && !outcome.NoCommitConfirmed) _telemetryUncertain = true;
            // A known failed start cannot be followed by a falsely successful heartbeat/exit.
            if (stamps && !committed) _sessionStarted = false;
            WarnTelemetry(message);
            return committed
                ? ServiceResult<string?>.Success(details, message)
                : new ServiceResult<string?> { Error = message, NoCommitConfirmed = outcome.NoCommitConfirmed };
        }
    }

    private void WarnTelemetry(string message)
    {
        var notice = "Session history is incomplete or unverified. " + message + " Ask the owner to check the app log. Screen lock and startup settings are unchanged.";
        if (_telemetryNotice == notice) return;
        _telemetryNotice = notice;
        _log.Warn(message);
    }

    /// <summary>"3 sec", "2 min", "1 hr 5 min" - the owner's words for a gap. Never throws.</summary>
    public static string DescribeGap(string from, string to)
    {
        try
        {
            if (!DateTime.TryParseExact(from.Trim(), "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                || !DateTime.TryParseExact(to.Trim(), "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            {
                return "an unknown time";
            }

            var seconds = (long)(end - start).TotalSeconds;

            if (seconds < 1)
            {
                return "a moment";
            }

            if (seconds < 60)
            {
                return seconds + " sec";
            }

            if (seconds < 3600)
            {
                return (seconds / 60) + " min";
            }

            var hours = seconds / 3600;
            var minutes = (seconds % 3600) / 60;

            return minutes == 0 ? hours + " hr" : hours + " hr " + minutes + " min";
        }
        catch (Exception)
        {
            return "an unknown time";
        }
    }
}

/// <summary>Runtime decision and its warning from the same read; not a saved-settings receipt.</summary>
public sealed record FocusGuardStatus(bool Enabled, bool Verified, string Notice);
