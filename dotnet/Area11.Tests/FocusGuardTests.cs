using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The screen lockdown's memory (owner's word, Nov 2026): an away-episode names who, when and for
/// how long, and a run that never said goodbye is caught at the next startup with the gap since
/// it was last seen. Telemetry never throws and never audits itself.
/// </summary>
public sealed class FocusGuardTests
{
    private static FocusGuardService Guard(TestDatabase test) =>
        new(test.Settings, test.Audit, test.Log);

    [Fact]
    public void AwayEpisodesSpeakInSecondsMinutesAndHours()
    {
        using var test = new TestDatabase();
        var guard = Guard(test);
        var user = new User { Id = 2, Name = "Ali", Role = "cashier" };

        var seconds = guard.RecordAway(user, "2026-11-10 14:00:00", "2026-11-10 14:00:03");
        var minutes = guard.RecordAway(user, "2026-11-10 14:00:00", "2026-11-10 14:02:00");
        var hours = guard.RecordAway(user, "2026-11-10 14:00:00", "2026-11-10 15:05:00");

        Assert.Contains("Ali left the screen for 3 sec", seconds);
        Assert.Contains("Ali left the screen for 2 min", minutes);
        Assert.Contains("Ali left the screen for 1 hr 5 min", hours);
        Assert.Contains("14:00:00 to 14:00:03", seconds);
        Assert.Equal(3, Audits(test, "screen_left"));
    }

    [Fact]
    public void AwayEpisodesSurviveMissingNamesAndBrokenClocks()
    {
        using var test = new TestDatabase();
        var guard = Guard(test);

        var nobody = guard.RecordAway(null, "2026-11-10 14:00:00", "2026-11-10 14:01:00");
        var broken = guard.RecordAway(TestData.Owner, "not a time", "also not");
        var backwards = guard.RecordAway(TestData.Owner, "2026-11-10 14:01:00", "2026-11-10 14:00:00");

        Assert.StartsWith("Someone left the screen for 1 min", nobody);
        Assert.Contains("an unknown time", broken);
        Assert.Contains("a moment", backwards);
        Assert.Equal(3, Audits(test, "screen_left"));
    }

    [Fact]
    public void FirstStartAndCleanExitsReportNothing()
    {
        using var test = new TestDatabase();
        var guard = Guard(test);

        // A fresh database has no previous run to mourn.
        Assert.Null(guard.CheckPreviousRun());
        Assert.Equal(0, Audits(test, "session_killed"));

        // A run that said goodbye is equally quiet.
        guard.MarkCleanExit();
        Assert.Null(guard.CheckPreviousRun());
        Assert.Equal(0, Audits(test, "session_killed"));
    }

    [Fact]
    public void AKilledRunIsCaughtWithItsGapAtTheNextStartup()
    {
        using var test = new TestDatabase();
        var guard = Guard(test);

        Assert.Null(guard.CheckPreviousRun());
        guard.Heartbeat("Ali");

        // Two hours pass with no goodbye: power cut, Task Manager, the same silence.
        var seen = test.Settings.GetString("session.lastSeen");
        Assert.False(string.IsNullOrWhiteSpace(seen));
        var backdated = DateTime.ParseExact(seen, "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture)
            .AddHours(-2).ToString("yyyy-MM-dd HH:mm:ss");
        Assert.True(test.Settings.SetUiOnly("session.lastSeen", backdated).Ok);

        var report = guard.CheckPreviousRun();

        Assert.Contains("Ali", report!);
        Assert.Contains("ended without closing", report);
        Assert.Contains("2 hr", report);
        Assert.Equal(1, Audits(test, "session_killed"));
    }

    [Fact]
    public void TheGuardSwitchIsOwnerOnlyAndOffByDefault()
    {
        using var test = new TestDatabase();
        var guard = Guard(test);

        Assert.False(guard.Enabled);
        Assert.False(test.Settings.Set("lockdown.focusGuard", true, TestData.Cashier(test)).Ok);
        Assert.True(test.Settings.Set("lockdown.focusGuard", true, TestData.Owner).Ok);
        test.Settings.Reload();
        Assert.True(guard.Enabled);
    }

    private static long Audits(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action }) ?? 0L);
    }
}
