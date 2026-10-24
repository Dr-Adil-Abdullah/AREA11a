namespace Area11.Core.Utils;

/// <summary>
/// The one session the shop is running right now (v0.35.0, D-92): when the person signed in, and
/// when anybody last touched the program.
///
/// Why a static: the two facts are needed by the window frame (which owns the timer), by the button
/// guard (every click is activity) and by the tests, and none of them should have to pass a clock
/// through five layers. It holds no state that belongs in the database - a restart is a new session,
/// which is exactly what the owner asked for.
/// </summary>
public static class SessionWatch
{
    private static readonly object Gate = new();

    private static bool _running;
    private static DateTime _signedInAt;
    private static DateTime _lastActivityAt;

    /// <summary>True between a sign-in and a sign-out.</summary>
    public static bool Running
    {
        get
        {
            lock (Gate)
            {
                return _running;
            }
        }
    }

    public static DateTime SignedInAt
    {
        get
        {
            lock (Gate)
            {
                return _signedInAt;
            }
        }
    }

    public static DateTime LastActivityAt
    {
        get
        {
            lock (Gate)
            {
                return _lastActivityAt;
            }
        }
    }

    /// <summary>Starts the clock at a sign-in. Activity counts as "now" at that moment.</summary>
    public static void Start(DateTime? at = null)
    {
        var stamp = at ?? TimeSource.Now;

        lock (Gate)
        {
            _signedInAt = stamp;
            _lastActivityAt = stamp;
            _running = true;
        }
    }

    /// <summary>Records that the counter was touched. Cheap enough to call on every click.</summary>
    public static void Touch(DateTime? at = null)
    {
        var stamp = at ?? TimeSource.Now;

        lock (Gate)
        {
            if (!_running)
            {
                return;
            }

            // Only forward: a clock that went backwards must not make the idle timer longer.
            if (stamp > _lastActivityAt)
            {
                _lastActivityAt = stamp;
            }
        }
    }

    /// <summary>Ends the session (sign-out, or the window closing).</summary>
    public static void Stop()
    {
        lock (Gate)
        {
            _running = false;
        }
    }

    /// <summary>Forgets the session completely - a test calls this, nothing in the program does.</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            _running = false;
            _signedInAt = default;
            _lastActivityAt = default;
        }
    }
}
