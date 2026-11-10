using System.IO;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The automatic cloud push (Portion 15, roadmap #7). The promises that must hold:
///   • nothing is sent unless the owner ticked automatic AND the cloud is set up;
///   • pushes go one at a time - a manual press during a run is refused honestly, an automatic
///     tick during a run skips quietly, and neither records a failure for the other;
///   • a failed attempt waits the full interval (never hammered) and retries when due, with the
///     reason kept in the owner's words - never the key;
///   • an empty shop stamps "nothing to send" instead of rebuilding every tick;
///   • the scheduled push carries the owner's standing permission (his tick), and a success clears
///     the last error whether the push was manual or automatic.
/// Every test runs against a fake wire: no internet, no Supabase, no network card.
/// </summary>
public sealed class SyncAutomaticTests
{
    [Fact]
    public void AutomaticOff_SendsNothing()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var wire = new FakeCloud();
        var cloud = Cloud(test, wire);

        Assert.False(cloud.AutomaticEnabled);
        Assert.False(cloud.IsAutomaticDue());
        Assert.Null(cloud.RunAutomaticIfDue());
        Assert.Equal(0, wire.Sends);
    }

    [Fact]
    public void CloudOff_AutomaticStaysQuiet()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        // Automatic on, but the main switch off: "do not talk to the cloud" wins.
        Assert.True(test.Settings.SetUiOnly(SyncService.EnabledKey, false).Ok);
        EnableAutomatic(test);

        var wire = new FakeCloud();

        Assert.Null(Cloud(test, wire).RunAutomaticIfDue());
        Assert.Equal(0, wire.Sends);
    }

    [Fact]
    public void UnconfiguredShop_SendsNothing()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);
        EnableAutomatic(test);

        // The address goes missing after setup: the tick must not send half a configuration.
        Assert.True(test.Settings.SetUiOnly("sync.url", string.Empty).Ok);

        var wire = new FakeCloud();
        var cloud = Cloud(test, wire);

        Assert.False(cloud.IsAutomaticDue());
        Assert.Null(cloud.RunAutomaticIfDue());
        Assert.Equal(0, wire.Sends);
    }

    [Fact]
    public void DueShop_PushesOnceAndLeavesOneLine()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);
        EnableAutomatic(test);

        var wire = new FakeCloud();
        var cloud = Cloud(test, wire);

        Assert.True(cloud.IsAutomaticDue());

        var run = cloud.RunAutomaticIfDue();

        Assert.NotNull(run);
        Assert.Equal("shop-1", run!.ShopId);
        Assert.Equal(1, wire.Sends);
        Assert.Equal(1, Audits(test, "sync_push"));
    }

    [Fact]
    public void JustPushed_NotDueAgain()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);
        EnableAutomatic(test);

        var wire = new FakeCloud();
        var cloud = Cloud(test, wire);

        Assert.NotNull(cloud.RunAutomaticIfDue());
        Assert.False(cloud.IsAutomaticDue());
        Assert.Null(cloud.RunAutomaticIfDue());
        Assert.Equal(1, wire.Sends);
    }

    [Fact]
    public void AManualPush_ResetsTheAutomaticClock()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);
        EnableAutomatic(test);

        var wire = new FakeCloud();
        var cloud = Cloud(test, wire);

        Assert.True(cloud.Push(TestData.Owner).Ok);

        // The owner's own press counts as an attempt: the tick waits the full interval.
        Assert.False(cloud.IsAutomaticDue());
        Assert.Null(cloud.RunAutomaticIfDue());
        Assert.Equal(1, wire.Sends);
    }

    [Fact]
    public void AFailedPush_WaitsForTheNextInterval()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);
        EnableAutomatic(test);

        var wire = new FakeCloud
        {
            SendAnswer = SyncTransportResult.Fail(0, string.Empty, "no internet")
        };
        var cloud = Cloud(test, wire);

        Assert.Null(cloud.RunAutomaticIfDue());
        Assert.Equal(1, wire.Sends);
        Assert.Equal(0, Audits(test, "sync_push"));

        // No hammering: the failure reschedules like a success.
        Assert.False(cloud.IsAutomaticDue());
        Assert.Null(cloud.RunAutomaticIfDue());
        Assert.Equal(1, wire.Sends);

        var status = cloud.Status(TestData.Owner).Value!;

        Assert.Equal("the last attempt failed - see the message below", status.StatusLabel);
        Assert.Contains("could not be reached", status.LastError, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-key-1234-5678", status.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public void AFailedPush_RetriesWhenDue()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);
        EnableAutomatic(test);

        var wire = new FakeCloud
        {
            SendAnswer = SyncTransportResult.Fail(0, string.Empty, "no internet")
        };
        var cloud = Cloud(test, wire);

        Assert.Null(cloud.RunAutomaticIfDue());
        Assert.Equal(1, wire.Sends);

        // The interval passes: the next tick tries again instead of giving up.
        Backdate(test);

        Assert.True(cloud.IsAutomaticDue());
        Assert.Null(cloud.RunAutomaticIfDue());
        Assert.Equal(2, wire.Sends);
    }

    [Fact]
    public void AnEmptyShop_StampsNothingToSend()
    {
        using var test = new TestDatabase();
        Configure(test);
        EnableAutomatic(test);

        var wire = new FakeCloud();
        var cloud = Cloud(test, wire);

        Assert.Null(cloud.RunAutomaticIfDue());
        Assert.Equal(0, wire.Sends);
        Assert.Equal("nothing to send yet", cloud.Status(TestData.Owner).Value!.StatusLabel);

        // Stamped like an attempt: the tick waits instead of rebuilding every 30 seconds.
        Assert.False(cloud.IsAutomaticDue());
        Assert.Null(cloud.RunAutomaticIfDue());
        Assert.Equal(0, wire.Sends);
    }

    [Fact]
    public void AManualPushDuringAPush_IsRefusedHonestly()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        SyncService? cloud = null;
        ServiceResult<SyncRun>? inner = null;

        var wire = new FakeCloud
        {
            // The wire calls back while the outer push still owns it.
            OnSend = () => inner = cloud!.Push(TestData.Owner)
        };
        cloud = Cloud(test, wire);

        var outer = cloud.Push(TestData.Owner);

        Assert.True(outer.Ok, outer.Error);
        Assert.NotNull(inner);
        Assert.False(inner!.Ok);
        Assert.Contains("already running", inner.Error!, StringComparison.Ordinal);
        Assert.Contains("press Push again", inner.Error!, StringComparison.Ordinal);
        Assert.Equal(1, wire.Sends);
    }

    [Fact]
    public void AnAutomaticTickDuringAPush_SkipsQuietly()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);
        EnableAutomatic(test);

        SyncService? cloud = null;
        var innerCalled = false;
        SyncRun? inner = new();

        var wire = new FakeCloud
        {
            OnSend = () =>
            {
                innerCalled = true;
                inner = cloud!.RunAutomaticIfDue();
            }
        };
        cloud = Cloud(test, wire);

        var outer = cloud.Push(TestData.Owner);

        Assert.True(outer.Ok, outer.Error);
        Assert.True(innerCalled);
        Assert.Null(inner);
        Assert.Equal(1, wire.Sends);

        // Skipped, not failed: no error is recorded for stepping aside.
        var status = cloud.Status(TestData.Owner).Value!;
        Assert.Equal("the last push succeeded", status.StatusLabel);
        Assert.Equal(string.Empty, status.LastError);
    }

    [Fact]
    public void Minutes_AreClamped()
    {
        using var test = new TestDatabase();

        Assert.True(test.Settings.SetUiOnly(SyncService.AutomaticMinutesKey, 0).Ok);
        Assert.Equal(5, Cloud(test, new FakeCloud()).AutomaticMinutes);

        Assert.True(test.Settings.SetUiOnly(SyncService.AutomaticMinutesKey, 9999).Ok);
        Assert.Equal(1440, Cloud(test, new FakeCloud()).AutomaticMinutes);

        Assert.True(test.Settings.SetUiOnly(SyncService.AutomaticMinutesKey, 30).Ok);
        Assert.Equal(30, Cloud(test, new FakeCloud()).AutomaticMinutes);
    }

    [Fact]
    public void MinutesValidation_RefusesSillyValues()
    {
        using var test = new TestDatabase();

        Assert.False(test.Settings.Set(SyncService.AutomaticMinutesKey, 4, TestData.Owner).Ok);
        Assert.False(test.Settings.Set(SyncService.AutomaticMinutesKey, 1441, TestData.Owner).Ok);
        Assert.False(test.Settings.Set(SyncService.AutomaticMinutesKey, "every often", TestData.Owner).Ok);
        Assert.True(test.Settings.Set(SyncService.AutomaticMinutesKey, 30, TestData.Owner).Ok);
    }

    [Fact]
    public void Status_CarriesTheAutomaticLineAndClearsItOnSuccess()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var cloud = Cloud(test, new FakeCloud());

        Assert.Equal("Automatic push is off - use the Push button.",
            cloud.Status(TestData.Owner).Value!.AutomaticLabel);

        EnableAutomatic(test);

        Assert.Equal("Automatic push is on: every 15 minutes.",
            cloud.Status(TestData.Owner).Value!.AutomaticLabel);

        // One failure writes the line; the next success - manual or automatic - clears it.
        var failing = Cloud(test, new FakeCloud
        {
            SendAnswer = SyncTransportResult.Fail(0, string.Empty, "no internet")
        });

        Assert.Null(failing.RunAutomaticIfDue());
        Assert.Contains("could not be reached",
            failing.Status(TestData.Owner).Value!.LastError, StringComparison.Ordinal);

        Backdate(test);
        Assert.True(cloud.Push(TestData.Owner).Ok);
        Assert.Equal(string.Empty, cloud.Status(TestData.Owner).Value!.LastError);
    }

    // ============================== the rig ==============================

    private sealed class FakeCloud : ICloudTransport
    {
        public int Sends { get; private set; }

        public SyncTransportResult SendAnswer { get; init; } = SyncTransportResult.Success(201, string.Empty);

        public Action? OnSend { get; init; }

        public SyncTransportResult Send(string url, string key, string table, string json)
        {
            Sends++;
            OnSend?.Invoke();
            return SendAnswer;
        }

        public SyncTransportResult Fetch(string url, string key, string table, string shopId) =>
            SyncTransportResult.Success(200, "[]");
    }

    private static SyncService Cloud(TestDatabase test, ICloudTransport wire)
    {
        var permissions = new PermissionService(test.Database, test.Log, test.Audit);

        var backup = new BackupService(test.Database, test.Settings, test.Audit, permissions, test.Log,
            NewFolder(), NewFolder());

        return new SyncService(test.Database, test.Settings, test.Audit, permissions, test.Log, backup, wire);
    }

    private static void Configure(TestDatabase test, string shopId = "shop-1")
    {
        Assert.True(test.Settings.SetUiOnly(SyncService.EnabledKey, true).Ok);
        Assert.True(test.Settings.SetUiOnly("sync.url", "https://shop.supabase.co").Ok);
        Assert.True(test.Settings.SetUiOnly("sync.key", "secret-key-1234-5678").Ok);
        Assert.True(test.Settings.SetUiOnly("sync.table", "area11_sync").Ok);
        Assert.True(test.Settings.SetUiOnly("sync.shopId", shopId).Ok);
    }

    private static void EnableAutomatic(TestDatabase test)
    {
        Assert.True(test.Settings.SetUiOnly(SyncService.AutomaticEnabledKey, true).Ok);
    }

    private static void Backdate(TestDatabase test)
    {
        Assert.True(test.Settings.SetUiOnly(SyncService.LastSyncKey, "2020-01-01 00:00:00").Ok);
    }

    private static long Audits(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action }) ?? 0L);
    }

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-sync-auto-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        return folder;
    }
}
