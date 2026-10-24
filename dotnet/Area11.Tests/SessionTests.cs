using System;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The two clocks of a session (v0.35.0, D-92): the idle limit the owner chooses in Settings, and
/// the 12-hour ceiling that applies whatever he chose.
///
/// The tests are on <see cref="SessionPolicy"/> and not on the window, because the rule is what can
/// cost the shop money - a session that ends too early closes the counter in the middle of a queue,
/// and one that never ends hands the drawer to the next person.
/// </summary>
public sealed class SessionTests
{
    private static DateTime At(int hour, int minute = 0) =>
        new(2026, 10, 24, hour, minute, 0, DateTimeKind.Local);

    [Fact]
    public void A_quiet_counter_is_signed_out_after_the_hours_the_owner_chose()
    {
        var check = SessionPolicy.Check(At(9), At(10), At(12, 30), idleLimitHours: 2);

        Assert.True(check.Expired);
        Assert.Equal(SessionEnd.Idle, check.End);
        Assert.Contains("2 hours", check.Sentence);
    }

    [Fact]
    public void Work_postpones_the_idle_limit_and_the_session_keeps_running()
    {
        var check = SessionPolicy.Check(At(9), At(11), At(12), idleLimitHours: 2);

        Assert.False(check.Expired);
        Assert.Equal(TimeSpan.FromHours(1), check.Left);
        Assert.Equal(SessionEnd.Idle, check.Next);
    }

    [Fact]
    public void Twelve_hours_after_the_sign_in_everything_closes_even_while_the_counter_is_busy()
    {
        // Signed in at midnight, working one minute before the limit: the idle clock is happy and
        // the ceiling is not. This is the case the owner asked for - "forgot to log out".
        var check = SessionPolicy.Check(At(0), At(11, 59), At(12), idleLimitHours: 6);

        Assert.True(check.Expired);
        Assert.Equal(SessionEnd.HardCap, check.End);
    }

    [Fact]
    public void The_ceiling_is_twelve_hours_and_a_longer_idle_choice_cannot_beat_it()
    {
        var check = SessionPolicy.Check(At(0), At(0), At(13), idleLimitHours: 12);

        Assert.True(check.Expired);
        Assert.Equal(SessionEnd.HardCap, check.End);
    }

    [Fact]
    public void Off_means_only_the_ceiling_and_nothing_else()
    {
        var idle = SessionPolicy.Check(At(9), At(9), At(11), idleLimitHours: 0);
        var ceiling = SessionPolicy.Check(At(0), At(11, 50), At(12), idleLimitHours: 0);

        Assert.False(idle.Expired);
        Assert.Equal(SessionEnd.HardCap, idle.Next);
        Assert.True(ceiling.Expired);
        Assert.Equal(SessionEnd.HardCap, ceiling.End);
    }

    [Fact]
    public void A_clock_that_jumps_backwards_does_not_lock_the_shop_out()
    {
        // A laptop carried between places, or a correction from a time server: the sign-in looks
        // like it happened in the future. Nothing has "passed", so nothing expires.
        var check = SessionPolicy.Check(At(12), At(12), At(10), idleLimitHours: 1);

        Assert.False(check.Expired);
        Assert.Equal(TimeSpan.FromHours(12), check.Left);
    }

    [Fact]
    public void A_number_that_is_not_one_of_the_choices_falls_back_to_the_ceiling_only()
    {
        var check = SessionPolicy.Check(At(9), At(9), At(11), idleLimitHours: 7);

        Assert.False(check.Expired);
        Assert.Equal(SessionEnd.HardCap, check.Next);
    }

    [Fact]
    public void The_warning_line_appears_in_the_last_quarter_of_an_hour()
    {
        var early = SessionPolicy.Check(At(9), At(9), At(9, 40), idleLimitHours: 1);
        var late = SessionPolicy.Check(At(9), At(9), At(9, 50), idleLimitHours: 1);

        Assert.False(early.Warning);
        Assert.True(late.Warning);
        Assert.Equal(10, SessionPolicy.MinutesLeft(late));
    }

    [Fact]
    public void The_warning_knows_which_clock_is_going_to_fire()
    {
        // Idle 1 hour, signed in 11 hours ago: the idle clock fires first, and touching the counter
        // postpones it - so the line may invite the person to touch the screen.
        var idleSooner = SessionPolicy.Check(At(1), At(11, 50), At(11, 55), idleLimitHours: 1);

        // Idle 6 hours, signed in 11 h 50 min ago: the ceiling fires first and nothing postpones it.
        var ceilingSooner = SessionPolicy.Check(At(0), At(11, 55), At(11, 55), idleLimitHours: 6);

        Assert.Equal(SessionEnd.Idle, idleSooner.Next);
        Assert.Equal(SessionEnd.HardCap, ceilingSooner.Next);
    }

    [Fact]
    public void An_expired_session_has_nothing_left_and_says_nothing_while_it_is_running()
    {
        var running = SessionPolicy.Check(At(9), At(9), At(9, 30), idleLimitHours: 2);
        var expired = SessionPolicy.Check(At(9), At(9), At(11), idleLimitHours: 2);

        Assert.False(running.Expired);
        Assert.Equal(string.Empty, running.Sentence);
        Assert.Equal(0, SessionPolicy.MinutesLeft(expired));
        Assert.Contains("signed the last person out", expired.Sentence);
    }

    [Fact]
    public void The_session_clock_starts_at_the_sign_in_and_work_moves_only_forward()
    {
        SessionWatch.Reset();

        try
        {
            SessionWatch.Start(At(9));
            Assert.True(SessionWatch.Running);
            Assert.Equal(At(9), SessionWatch.LastActivityAt);

            SessionWatch.Touch(At(10));
            Assert.Equal(At(10), SessionWatch.LastActivityAt);

            // A clock that went backwards must not make the idle timer longer.
            SessionWatch.Touch(At(9, 30));
            Assert.Equal(At(10), SessionWatch.LastActivityAt);

            SessionWatch.Stop();
            Assert.False(SessionWatch.Running);
        }
        finally
        {
            SessionWatch.Reset();
        }
    }
}
