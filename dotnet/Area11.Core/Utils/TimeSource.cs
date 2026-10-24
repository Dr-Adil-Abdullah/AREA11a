using System.Globalization;
using System.Threading;

namespace Area11.Core.Utils;

/// <summary>Where the time on the bill came from (v0.35.0, D-92).</summary>
public enum TimeOrigin
{
    /// <summary>The PC's own clock - used when the internet is not reachable.</summary>
    Computer,

    /// <summary>A time server answered, and the difference was applied to the PC's clock.</summary>
    Online
}

/// <summary>
/// The time the shop's bills are stamped with - **asked from the internet when it is there, and from
/// the computer when it is not** (v0.35.0, D-92).
///
/// Why: a shop PC's clock drifts - a flat CMOS battery, a laptop carried between places, a Windows
/// time service that never runs because there is no internet. A bill dated ten minutes in the
/// future sorts wrongly in every report; one dated a day early hides a day's sales. The owner asked
/// for the time to be taken **online when possible** and from the computer otherwise, so this class
/// keeps one correction: `Offset = online time - computer time`. `Now` returns the corrected local
/// time, so the whole program (one hundred `Clock.Now()` call sites) moves together without any of
/// them being edited.
///
/// The rules, in the order they matter:
///   * **Nothing here may throw or block a bill.** Every call is wrapped; a failed probe keeps the
///     last good offset and never stops the counter.
///   * **A wild answer is refused.** A server that says the year is 1980, or a clock that is a
///     month off, must not move every bill of the shop - a difference larger than two days is
///     treated as an error and the computer clock is kept.
///   * **The shop must be able to see which time was used.** `Origin`, `Offset` and `Note` are
///     written into the log and shown on Settings → Shift &amp; session.
/// </summary>
public static class TimeSource
{
    /// <summary>Anything larger than this is not drift, it is a wrong answer.</summary>
    public static readonly TimeSpan LargestAcceptedOffset = TimeSpan.FromDays(2);

    private static readonly object Gate = new();

    private static TimeSpan _offset = TimeSpan.Zero;
    private static TimeOrigin _origin = TimeOrigin.Computer;
    private static DateTime? _checkedAt;
    private static string _note = "the time has not been checked yet";
    private static Timer? _timer;

    /// <summary>
    /// The probe the shop uses, replaced by a test (and by nothing else). It returns the true time,
    /// or null when there is no answer. A probe that throws is treated like no answer.
    /// </summary>
    public static Func<DateTimeOffset?>? Probe { get; set; }

    /// <summary>True when a time server has answered at least once in this run.</summary>
    public static bool IsOnline
    {
        get
        {
            lock (Gate)
            {
                return _origin == TimeOrigin.Online;
            }
        }
    }

    public static TimeOrigin Origin
    {
        get
        {
            lock (Gate)
            {
                return _origin;
            }
        }
    }

    /// <summary>True time minus this computer's clock. Zero when the computer clock is used.</summary>
    public static TimeSpan Offset
    {
        get
        {
            lock (Gate)
            {
                return _offset;
            }
        }
    }

    public static DateTime? CheckedAt
    {
        get
        {
            lock (Gate)
            {
                return _checkedAt;
            }
        }
    }

    /// <summary>One line for the log and the Settings page - in words, not numbers only.</summary>
    public static string Note
    {
        get
        {
            lock (Gate)
            {
                return _note;
            }
        }
    }

    /// <summary>
    /// The local time the shop should use. **This is what `Clock` returns**, so every stamp in the
    /// database is corrected when a time server has answered.
    /// </summary>
    public static DateTime Now
    {
        get
        {
            try
            {
                return DateTime.Now + Offset;
            }
            catch (Exception)
            {
                // A clock that cannot be read is not a reason to stop the till.
                return DateTime.Now;
            }
        }
    }

    /// <summary>
    /// Asks the time servers once. Returns true when an online answer was accepted.
    ///
    /// Called on a background thread at startup with a short budget: a shop with no internet must
    /// not wait for it before the sign-in window appears.
    /// </summary>
    public static bool TrySync(int timeoutMilliseconds = 1500)
    {
        DateTimeOffset? answer;

        try
        {
            var probe = Probe;
            answer = probe is not null ? probe() : NtpTime.Query(timeoutMilliseconds);
        }
        catch (Exception exception)
        {
            lock (Gate)
            {
                _note = "the time servers could not be asked (" + exception.GetType().Name +
                        ") - the computer clock is used";
            }

            return false;
        }

        if (answer is null)
        {
            lock (Gate)
            {
                _origin = TimeOrigin.Computer;
                _offset = TimeSpan.Zero;
                _checkedAt = DateTime.Now;
                _note = "no time server answered - the computer clock is used";
            }

            return false;
        }

        TimeSpan difference;

        try
        {
            difference = answer.Value - DateTimeOffset.Now;
        }
        catch (Exception)
        {
            difference = TimeSpan.Zero;
        }

        if (difference.Duration() > LargestAcceptedOffset)
        {
            lock (Gate)
            {
                _origin = TimeOrigin.Computer;
                _offset = TimeSpan.Zero;
                _checkedAt = DateTime.Now;
                _note = "a time server answered with a difference of " + Describe(difference) +
                        ", which is too large to be drift - the computer clock is used";
            }

            return false;
        }

        lock (Gate)
        {
            _offset = difference;
            _origin = TimeOrigin.Online;
            _checkedAt = DateTime.Now;
            _note = "online time, " + Describe(difference) + " from the computer clock";
        }

        return true;
    }

    /// <summary>
    /// Asks again every few minutes, for ever, on a background thread (v0.35.0, D-92).
    ///
    /// Why a timer and not one probe at startup: the owner's words are "sync the moment the internet
    /// comes back". A shop whose line is down in the morning and up at noon must stop adding the
    /// morning's drift to every bill at noon, and nobody should have to restart the program for it.
    /// One small UDP packet every 20 minutes costs nothing; a `TrySync` that throws is caught inside
    /// and one that fails simply leaves the computer clock in place.
    /// </summary>
    public static void StartBackgroundSync(Action<string> note, int everyMinutes = 20)
    {
        if (_timer is not null)
        {
            return;
        }

        var period = TimeSpan.FromMinutes(Math.Clamp(everyMinutes, 5, 720));

        _timer = new Timer(
            _ =>
            {
                try
                {
                    TrySync();
                    note(Note);
                }
                catch (Exception)
                {
                    // A note that cannot be written must not stop the clock.
                }
            },
            null,
            TimeSpan.Zero,
            period);
    }

    /// <summary>The owner's line on the Settings page: where the time comes from, and how far apart.</summary>
    public static string DescribeForOwner()
    {
        lock (Gate)
        {
            if (_checkedAt is null)
            {
                return "The time comes from this computer's clock. Area11 has not reached a time server yet.";
            }

            var when = _checkedAt.Value.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

            return _origin == TimeOrigin.Online
                ? "The time comes from a time server (checked " + when + "). This computer's clock was " +
                  Describe(_offset) + " away from it."
                : "No time server answered at " + when +
                  ", so the time comes from this computer's clock. Area11 asks again every 20 minutes.";
        }
    }

    /// <summary>Forgets the correction and stops the background sync - a test calls this, nothing else does.</summary>
    public static void Reset()
    {
        Probe = null;
        _timer?.Dispose();
        _timer = null;

        lock (Gate)
        {
            _offset = TimeSpan.Zero;
            _origin = TimeOrigin.Computer;
            _checkedAt = null;
            _note = "the time has not been checked yet";
        }
    }

    /// <summary>"+0.4 s" / "-2 min" - a difference a shopkeeper can read.</summary>
    public static string Describe(TimeSpan difference)
    {
        var sign = difference < TimeSpan.Zero ? "-" : "+";
        var size = difference.Duration();

        if (size.TotalSeconds < 1)
        {
            return "less than a second";
        }

        if (size.TotalMinutes < 1)
        {
            return sign + size.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        if (size.TotalHours < 1)
        {
            return sign + size.TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture) + " min";
        }

        return sign + size.TotalHours.ToString("0.0", CultureInfo.InvariantCulture) + " h";
    }
}
