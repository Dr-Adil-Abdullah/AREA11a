using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Silent values (the session heartbeat, a last-seen stamp) are stored like any other setting but
/// must never write into the black box: a stamp written every 30 seconds would otherwise bury the
/// real changes (a price, a refund) under heartbeats. Three rules pin the two doors:
///
///   1. The quiet door (`SetUiOnly`) stores and reads back, writing no audit line.
///   2. The loud door (`Set`/`SetMany`) writes exactly one `settings_change` line.
///   3. An unknown key is refused on write; a read of one answers empty, never a crash.
/// </summary>
public sealed class QuietSettingsTests
{
    private static readonly User Owner = new() { Id = 1, Name = "Owner", Role = "owner" };

    [Fact]
    public void SilentValues_AreSavedAndReadBack()
    {
        using var test = new TestDatabase();

        Assert.True(test.Settings.SetUiOnly("session.lastSeen", "2026-11-10 14:00:00").Ok);
        Assert.Equal("2026-11-10 14:00:00", test.Settings.GetString("session.lastSeen"));
    }

    [Fact]
    public void ASilentWrite_WritesNoAuditLine()
    {
        using var test = new TestDatabase();

        var before = Rows(test);
        Assert.True(test.Settings.SetUiOnly("session.lastSeen", "2026-11-10 14:01:00").Ok);

        // Same count, and not one "settings_change" row: the quiet door is the whole point.
        Assert.Equal(before, Rows(test));
        Assert.Equal(0L, Rows(test, "settings_change"));
    }

    [Fact]
    public void AnOwnerSetting_DoesWriteAnAuditLine()
    {
        using var test = new TestDatabase();

        Assert.True(test.Settings.Set("lockdown.focusGuard", true, Owner).Ok);

        // The contrast that makes the second test mean something: the same table, the loud door.
        Assert.Equal(1L, Rows(test, "settings_change"));
        Assert.True(test.Settings.GetBool("lockdown.focusGuard"));
    }

    [Fact]
    public void AnUnknownPreference_IsRefusedAndNothingIsWritten()
    {
        using var test = new TestDatabase();

        var result = test.Settings.SetUiOnly("session.madeUpKey", true);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);

        // Refused means refused: the key was not created behind our back, and nothing was logged.
        Assert.DoesNotContain("session.madeUpKey", SettingsService.KnownKeys);
        Assert.Equal(0L, Rows(test, "settings_change"));
    }

    [Fact]
    public void TheSilentKeys_AreKnownSettings()
    {
        using var test = new TestDatabase();

        // Settings → Save refuses an unknown key, so the silent keys must already be in the
        // defaults - otherwise a screen that writes them fails on an otherwise fine save.
        Assert.Contains("session.lastSeen", SettingsService.KnownKeys);
        Assert.Contains("session.cleanExit", SettingsService.KnownKeys);
        Assert.Contains("lockdown.focusGuard", SettingsService.KnownKeys);

        var saved = test.Settings.SetMany(
            new Dictionary<string, object?>
            {
                ["lockdown.focusGuard"] = true,
            },
            Owner);

        Assert.True(saved.Ok, saved.Error);
        Assert.True(test.Settings.GetBool("lockdown.focusGuard"));
    }

    [Fact]
    public void AnUnknownKey_ReadsEmptyAndNeverCrashes()
    {
        using var test = new TestDatabase();

        // The owner may keep the database of an old build; a read of a key that no longer exists
        // answers empty and never crashes the screen.
        Assert.Equal(string.Empty, test.Settings.GetString("session.madeUpKey"));
        Assert.False(test.Settings.GetBool("session.madeUpKey"));
    }

    /// <summary>How many audit rows exist, in total or for one action.</summary>
    private static long Rows(TestDatabase test, string? action = null) =>
        test.Database.Tx(connection => Convert.ToInt64(action is null
            ? test.Database.Scalar(connection, "SELECT COUNT(1) FROM audit_logs;")
            : test.Database.Scalar(connection,
                "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action }) ?? 0L));
}
