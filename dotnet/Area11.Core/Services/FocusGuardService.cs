using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The screen lockdown's memory (owner's word, Nov 2026): who left the screen, when, and for how
/// long - plus whether the previous run ended without closing (Task Manager, power loss). Every
/// write here is silent telemetry: it never audits itself, never throws at the caller, and a shop
/// that never switches the guard on still gets the kill detection for free.
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

    /// <summary>True when the owner switched the guard on. Never throws: off on any doubt.</summary>
    public bool Enabled
    {
        get
        {
            try
            {
                return _settings.GetBool(GuardKey);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// One away-episode, written when the operator comes back: who, how long, and the exact span.
    /// Returns the black-box line (tests pin it). A failed write is logged, never thrown.
    /// </summary>
    public string RecordAway(User? user, string leftAt, string backAt)
    {
        var name = string.IsNullOrWhiteSpace(user?.Name) ? "Someone" : user!.Name.Trim();
        var details = name + " left the screen for " + DescribeGap(leftAt, backAt) +
                      " (" + leftAt + " to " + backAt + ").";

        try
        {
            _audit.RecordStandalone(user, Modules.Pos, "screen_left", details);
        }
        catch (Exception exception)
        {
            _log.Error("An away-episode could not be written to the black box.", exception);
        }

        return details;
    }

    /// <summary>
    /// Runs once at startup, before the sign-in window. When the previous run never said goodbye,
    /// the gap since it was last seen goes to the black box; then this run stamps itself as alive
    /// but not yet cleanly exited. Returns the line, or null when there is nothing to report.
    /// </summary>
    public string? CheckPreviousRun()
    {
        try
        {
            var lastStart = _settings.GetString(LastStartKey);
            var lastUser = _settings.GetString(LastUserKey);
            var lastSeen = _settings.GetString(LastSeenKey);
            var clean = _settings.GetBool(CleanExitKey);

            var now = Clock.Now();
            _settings.SetUiOnly(LastStartKey, now);
            _settings.SetUiOnly(LastSeenKey, now);
            _settings.SetUiOnly(CleanExitKey, false);

            // No previous run (first start, or an upgrade that just seeded the keys): nothing to mourn.
            if (lastStart.Trim().Length == 0 || clean)
            {
                return null;
            }

            var seen = lastSeen.Trim().Length > 0 ? lastSeen.Trim() : lastStart.Trim();
            var who = lastUser.Trim().Length > 0 ? lastUser.Trim() : "The previous run";
            var details = who + " ended without closing - last seen " + seen +
                          ", back now (away " + DescribeGap(seen, now) + ").";

            _audit.RecordStandalone(null, Modules.Pos, "session_killed", details);
            _log.Warn(details);

            return details;
        }
        catch (Exception exception)
        {
            _log.Error("The previous run could not be checked.", exception);
            return null;
        }
    }

    /// <summary>Called when the program shuts down normally - the opposite of a kill.</summary>
    public void MarkCleanExit()
    {
        try
        {
            _settings.SetUiOnly(CleanExitKey, true);
        }
        catch (Exception exception)
        {
            _log.Error("A clean exit could not be stamped.", exception);
        }
    }

    /// <summary>Called every 30 seconds from the shell tick: still alive, still this user.</summary>
    public void Heartbeat(string? userName)
    {
        try
        {
            _settings.SetUiOnly(LastSeenKey, Clock.Now());

            if (!string.IsNullOrWhiteSpace(userName))
            {
                _settings.SetUiOnly(LastUserKey, userName!.Trim());
            }
        }
        catch (Exception exception)
        {
            _log.Error("The session heartbeat could not be stamped.", exception);
        }
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
