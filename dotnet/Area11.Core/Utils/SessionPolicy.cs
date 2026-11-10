namespace Area11.Core.Utils;

/// <summary>Why a session ended (v0.35.0, D-92).</summary>
public enum SessionEnd
{
    /// <summary>Still signed in.</summary>
    None,

    /// <summary>Nobody touched the counter for the number of hours the owner chose.</summary>
    Idle,

    /// <summary>The 12-hour limit - it applies whatever the owner chose.</summary>
    HardCap
}

/// <summary>
/// What the frame should do about the person who is signed in (v0.35.0, D-92).
///
/// The owner's rule, in his words: *"a person must not forget to log out and let the next person use
/// his account - after two, four or six hours, or a day, sign him out; but **after 12 hours, in every
/// case** everything closes once, so the next person signs in again."*
///
/// So there are **two** clocks on one session:
///   * **idle** - how long since anybody touched the program. A cashier who bills all day never
///     reaches it; one who walks away with the drawer open does. Chosen by the owner; `0` = off.
///   * **hard cap** - 12 hours from the sign-in, whatever the owner chose. It is not a setting,
///     because the case it protects against is the owner forgetting that he has a setting.
/// </summary>
public readonly struct SessionCheck
{
    /// <summary>Why the session ended, or `None` while it is still running.</summary>
    public SessionEnd End { get; init; }

    /// <summary>How long is left before the session ends.</summary>
    public TimeSpan Left { get; init; }

    /// <summary>True when the person must be signed out now.</summary>
    public bool Expired => End != SessionEnd.None;

    /// <summary>True when the frame should show "signed out in N minutes" - not on a fresh session.</summary>
    public bool Warning => !Expired && Left <= TimeSpan.FromMinutes(15);

    /// <summary>The sentence the login screen shows after an automatic sign-out (empty otherwise).</summary>
    public string Sentence => End switch
    {
        SessionEnd.Idle =>
            "Nobody touched the counter for " + Hours(IdleLimitHours) +
            ", so Area11 signed the last person out. Sign in again to carry on.",
        SessionEnd.HardCap =>
            "This session reached its 12-hour limit, so Area11 signed the last person out. " +
            "Sign in again - the drawer is still counted on the Shift (galla) page.",
        _ => string.Empty
    };

    /// <summary>The idle limit that produced this answer, in hours (0 = off).</summary>
    public int IdleLimitHours { get; init; }

    /// <summary>
    /// Which clock will end the session - also while it is still running, so the warning line can
    /// tell the truth: touching the counter buys time against <see cref="SessionEnd.Idle"/> and buys
    /// nothing against <see cref="SessionEnd.HardCap"/>.
    /// </summary>
    public SessionEnd Next { get; init; }

    /// <summary>"2 hours" / "1 hour" - one place, so two screens cannot say it differently.</summary>
    public static string Hours(int hours) => hours == 1 ? "1 hour" : hours + " hours";
}

/// <summary>The two clocks, answered from the same place so no screen can invent its own rule.</summary>
public static class SessionPolicy
{
    /// <summary>The limit that applies whatever the owner chose.</summary>
    public const int HardCapHours = 12;

    /// <summary>The idle limits the owner may choose from (0 = never, except the 12-hour limit).</summary>
    public static readonly int[] AllowedIdleHours = { 0, 1, 2, 4, 6, 12 };

    /// <summary>
    /// The state of one session. **Never throws**: a clock that moved backwards (a laptop carried
    /// between time zones, or a correction from a time server) counts as "no time has passed", so a
    /// wrong clock cannot lock the shop out - the 12-hour limit would otherwise fire immediately.
    /// </summary>
    public static SessionCheck Check(DateTime signedInAt, DateTime lastActivityAt, DateTime now,
        int idleLimitHours)
    {
        var limit = AllowedIdleHours.Contains(idleLimitHours) ? idleLimitHours : 0;

        var signedIn = Elapsed(now, signedInAt);
        var idle = Elapsed(now, lastActivityAt);

        var toCap = TimeSpan.FromHours(HardCapHours) - signedIn;

        if (toCap <= TimeSpan.Zero)
        {
            return new SessionCheck
            {
                End = SessionEnd.HardCap, Left = TimeSpan.Zero, IdleLimitHours = limit, Next = SessionEnd.HardCap
            };
        }

        // While the sign-in itself is ahead of the PC, the timeline is not trustworthy. Both
        // elapsed values above are frozen at zero; do not let the shorter idle choice become the
        // countdown (or its warning) until the clock catches up. The hard-cap answer is the safe
        // full twelve hours in this state, and neither clock can expire the session.
        if (limit > 0 && now >= signedInAt)
        {
            var toIdle = TimeSpan.FromHours(limit) - idle;

            if (toIdle <= TimeSpan.Zero)
            {
                return new SessionCheck
                {
                    End = SessionEnd.Idle, Left = TimeSpan.Zero, IdleLimitHours = limit, Next = SessionEnd.Idle
                };
            }

            if (toIdle < toCap)
            {
                return new SessionCheck
                {
                    End = SessionEnd.None, Left = toIdle, IdleLimitHours = limit, Next = SessionEnd.Idle
                };
            }
        }

        return new SessionCheck
        {
            End = SessionEnd.None, Left = toCap, IdleLimitHours = limit, Next = SessionEnd.HardCap
        };
    }

    /// <summary>The minutes left, rounded down - what the warning line shows.</summary>
    public static int MinutesLeft(SessionCheck check) =>
        check.Expired ? 0 : (int)Math.Floor(check.Left.TotalMinutes);

    private static TimeSpan Elapsed(DateTime now, DateTime since)
    {
        var elapsed = now - since;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }
}
