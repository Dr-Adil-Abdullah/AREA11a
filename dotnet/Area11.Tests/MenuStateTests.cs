using Area11.Core.Models;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The left menu folds (D-51). Two rules have to hold, or the shop loses work:
///
///   1. A fold is a **preference**, not a business change - it is stored so it survives a restart,
///      but it must not write a line into the black box. A cashier who tidies the menu ten times a
///      day would otherwise bury the real changes (a price, a refund) under menu clicks.
///   2. The owner's "keep every group open" tick is a **known** setting. If the key were missing
///      from the defaults, the Settings page would refuse the save with "Unknown setting" and the
///      tick would simply never stick.
///
/// The folding itself (hiding rows, keeping the selected page) lives in the view model, which needs
/// a window; what is tested here is the storage the view model writes through.
/// </summary>
public sealed class MenuStateTests
{
    private static readonly User Owner = new() { Id = 1, Name = "Owner", Role = "owner" };

    [Fact]
    public void FoldedGroups_AreSavedAndReadBack()
    {
        using var test = new TestDatabase();

        var folded = new List<string> { "stock", "money" };
        Assert.True(test.Settings.SetUiOnly("menu.collapsedGroups", folded).Ok);

        var read = test.Settings.GetList<string>("menu.collapsedGroups");

        Assert.Equal(folded, read);
    }

    [Fact]
    public void AFold_WritesNoAuditLine()
    {
        using var test = new TestDatabase();

        var before = Rows(test);
        Assert.True(test.Settings.SetUiOnly("menu.collapsedGroups", new List<string> { "sell" }).Ok);

        // Same count, and not one "settings_change" row: the quiet door is the whole point.
        Assert.Equal(before, Rows(test));
        Assert.Equal(0L, Rows(test, "settings_change"));
    }

    [Fact]
    public void AnOwnerSetting_DoesWriteAnAuditLine()
    {
        using var test = new TestDatabase();

        Assert.True(test.Settings.Set("menu.pinnedOpen", true, Owner).Ok);

        // The contrast that makes the first test mean something: the same table, the loud door.
        Assert.Equal(1L, Rows(test, "settings_change"));
        Assert.True(test.Settings.GetBool("menu.pinnedOpen"));
    }

    [Fact]
    public void AnUnknownPreference_IsRefusedAndNothingIsWritten()
    {
        using var test = new TestDatabase();

        var result = test.Settings.SetUiOnly("menu.madeUpKey", true);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);

        // Refused means refused: the key was not created behind our back, and nothing was logged.
        Assert.DoesNotContain("menu.madeUpKey", test.Settings.KnownKeys);
        Assert.Equal(0L, Rows(test, "settings_change"));
    }

    [Fact]
    public void TheMenuKeys_AreKnownSettings()
    {
        using var test = new TestDatabase();

        // Settings → Save refuses an unknown key, so both keys must already be in the defaults -
        // otherwise the owner's tick fails on a screen that otherwise saves fine.
        Assert.Contains("menu.pinnedOpen", test.Settings.KnownKeys);
        Assert.Contains("menu.collapsedGroups", test.Settings.KnownKeys);

        var saved = test.Settings.SetMany(
            new Dictionary<string, object?>
            {
                ["menu.pinnedOpen"] = true,
                ["menu.collapsedGroups"] = new List<string> { "people" }
            },
            Owner);

        Assert.True(saved.Ok, saved.Error);
        Assert.True(test.Settings.GetBool("menu.pinnedOpen"));
    }

    [Fact]
    public void ASavedGroupThatNoLongerExists_IsReadAsPlainText()
    {
        using var test = new TestDatabase();

        // The owner may keep the database of an old build; a saved key that no longer exists must
        // read as a plain value and never as a crash.
        Assert.True(test.Settings.SetUiOnly("menu.collapsedGroups", new List<string> { "removedGroup" }).Ok);

        var read = test.Settings.GetList<string>("menu.collapsedGroups");

        Assert.Single(read);
        Assert.Equal("removedGroup", read[0]);
    }

    /// <summary>How many audit rows exist, in total or for one action.</summary>
    private static long Rows(TestDatabase test, string? action = null) =>
        test.Database.Tx(connection => Convert.ToInt64(action is null
            ? test.Database.Scalar(connection, "SELECT COUNT(1) FROM audit_logs;")
            : test.Database.Scalar(connection,
                "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action }) ?? 0L));
}
