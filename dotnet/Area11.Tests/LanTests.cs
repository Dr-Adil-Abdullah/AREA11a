using Area11.Core.Data;
using Area11.Core.Services;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;
using Xunit;
using System.IO;

namespace Area11.Tests;

/// <summary>
/// The second counter (F-45, Spec §34.9, D-62).
///
/// The promises this file pins down:
///   1. a shop with no file is one computer - nothing changes for it;
///   2. the little `shop.ini` is written in one move and read back exactly;
///   3. the main counter keeps the file on its **own disk**, the second counter names a **share**;
///   4. a network database never asks for WAL - that journal cannot cross a share;
///   5. only the owner may move the shop's database, and the move takes effect after a restart;
///   6. the black box shows which counter joined.
/// </summary>
public sealed class LanTests
{
    // ============================== the file ==============================

    [Fact]
    public void AShopWithNoFileIsOneComputer()
    {
        var config = ShopConfig.Load(Path.Combine(TempFolder(), "missing.ini"));

        Assert.Equal(ShopMode.Single, config.Mode);
        Assert.Null(config.DataFolder);
        Assert.Null(config.Problem);
        Assert.False(config.IsLan);
        Assert.Equal("One computer", config.ModeLabel);
    }

    [Fact]
    public void TheIniFileIsWrittenAndReadBack()
    {
        var ini = Path.Combine(TempFolder(), "shop.ini");

        ShopConfig.Save(new ShopConfig { Mode = ShopMode.Second, DataFolder = @"\\MAIN-PC\Area11" }, ini);

        Assert.True(File.Exists(ini));

        var config = ShopConfig.Load(ini);

        Assert.Equal(ShopMode.Second, config.Mode);
        Assert.Equal(@"\\MAIN-PC\Area11", config.DataFolder);
        Assert.True(config.IsLan);
        Assert.Contains("mode=second", File.ReadAllText(ini));
    }

    [Fact]
    public void CommentsAndJunkLinesAreIgnored()
    {
        var ini = Path.Combine(TempFolder(), "shop.ini");

        File.WriteAllLines(ini, new[]
        {
            "; the main counter",
            "",
            "  # a comment with spaces",
            "data = \"C:\\ProgramData\\Area11\"",
            "nonsense",
            "mode=main"
        });

        var config = ShopConfig.Load(ini);

        Assert.Equal(ShopMode.Main, config.Mode);
        Assert.Equal(@"C:\ProgramData\Area11", config.DataFolder);
        Assert.Null(config.Problem);
    }

    [Fact]
    public void AnUnknownModeIsShownNotGuessed()
    {
        var ini = Path.Combine(TempFolder(), "shop.ini");
        File.WriteAllLines(ini, new[] { "mode=server" });

        var config = ShopConfig.Load(ini);

        Assert.Equal(ShopMode.Single, config.Mode);
        Assert.NotNull(config.Problem);
        Assert.Contains("server", config.Problem);
    }

    // ============================== the rules ==============================

    [Fact]
    public void AMainCounterCannotUseANetworkFolder()
    {
        var problem = ShopConfig.Validate(ShopMode.Main, @"\\MAIN-PC\Area11");

        Assert.NotNull(problem);
        Assert.Contains("own disk", problem);
        Assert.Null(ShopConfig.Validate(ShopMode.Main, @"C:\ProgramData\Area11"));
    }

    [Fact]
    public void ASecondCounterNeedsTheMainPcShare()
    {
        Assert.NotNull(ShopConfig.Validate(ShopMode.Second, @"C:\Area11"));
        Assert.NotNull(ShopConfig.Validate(ShopMode.Second, @"\\MAIN-PC"));
        Assert.NotNull(ShopConfig.Validate(ShopMode.Second, "  "));
        Assert.Null(ShopConfig.Validate(ShopMode.Second, @"\\MAIN-PC\Area11"));

        // A trailing separator is the same folder, so the file must not be written twice.
        Assert.Equal(@"\\MAIN-PC\Area11", ShopConfig.NormalizeFolder("\\\\MAIN-PC\\Area11\\"));
    }

    [Fact]
    public void TheNetworkPlanUsesTheRollbackJournal()
    {
        var plan = StoragePlan.For(@"\\MAIN-PC\Area11\area11.db");

        Assert.True(plan.IsNetwork);
        Assert.False(plan.UseWal);
        Assert.True(plan.SynchronousFull);
        Assert.Equal(15000, plan.BusyTimeoutMs);
    }

    [Fact]
    public void TheMainCounterIsTreatedAsSharedEvenOnItsOwnDisk()
    {
        // The main counter's file sits on a local disk, but a second PC opens it - so it must keep
        // the rollback journal after every restart, not only on the day it was prepared (D-62).
        var plan = StoragePlan.For(@"C:\ProgramData\Area11\area11.db", ShopMode.Main);

        Assert.False(plan.IsNetwork);
        Assert.True(plan.IsShared);
        Assert.False(plan.UseWal);
        Assert.True(plan.SynchronousFull);
        Assert.Equal(15000, plan.BusyTimeoutMs);
    }

    [Fact]
    public void TheLocalPlanKeepsWal()
    {
        var plan = StoragePlan.For(@"C:\ProgramData\Area11\area11.db");

        Assert.False(plan.IsNetwork);
        Assert.True(plan.UseWal);
        Assert.False(plan.SynchronousFull);
        Assert.Equal(5000, plan.BusyTimeoutMs);
    }

    // ============================== the service ==============================

    [Fact]
    public void OnlyTheOwnerMayMoveTheShop()
    {
        using var test = new TestDatabase();
        var lan = Lan(test);
        var cashier = TestData.Cashier(test);
        var manager = TestData.AddUser(test, "manager");

        Assert.False(lan.PrepareAsMain(cashier).Ok);
        Assert.False(lan.PrepareAsMain(manager).Ok);
        Assert.False(lan.JoinAsSecond(cashier, @"\\MAIN-PC\Area11").Ok);
        Assert.False(lan.Status(manager).Ok);
        Assert.False(lan.TestConnection(cashier).Ok);
    }

    [Fact]
    public void PrepareWritesTheMainModeAndLeavesWal()
    {
        using var test = new TestDatabase();
        var ini = Path.Combine(TempFolder(), "shop.ini");
        var lan = Lan(test, ini);

        var prepared = lan.PrepareAsMain(TestData.Owner);

        Assert.True(prepared.Ok, prepared.Error);

        var config = ShopConfig.Load(ini);

        Assert.Equal(ShopMode.Main, config.Mode);
        Assert.Equal(Path.GetDirectoryName(test.Database.FilePath), config.DataFolder);
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'lan_prepare';"));

        // A plain connection of its own: the test database runs with an in-memory journal per
        // connection (durable: false), so only a fresh connection reports the FILE's journal mode.
        using var connection = new SqliteConnection("Data Source=" + test.Database.FilePath);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";

        var journal = Convert.ToString(command.ExecuteScalar());

        Assert.Equal("delete", journal?.Trim().ToLowerInvariant());
    }

    [Fact]
    public void ASecondCounterCannotPrepareTheMainPc()
    {
        using var test = new TestDatabase();
        var ini = Path.Combine(TempFolder(), "shop.ini");
        var second = new LanService(test.Database, test.Audit, test.Log,
            new ShopConfig { Mode = ShopMode.Second, DataFolder = @"\\MAIN-PC\Area11" }, ini);

        var refused = second.PrepareAsMain(TestData.Owner);

        Assert.False(refused.Ok);
        Assert.Contains("second counter", refused.Error);
        Assert.False(File.Exists(ini));
    }

    [Fact]
    public void AJoinToALocalFolderIsRefusedBeforeAnythingIsWritten()
    {
        using var test = new TestDatabase();
        var ini = Path.Combine(TempFolder(), "shop.ini");
        var lan = Lan(test, ini);

        var refused = lan.JoinAsSecond(TestData.Owner, @"C:\Area11");

        Assert.False(refused.Ok);
        Assert.Contains("two backslashes", refused.Error);
        Assert.False(File.Exists(ini));
    }

    [Fact]
    public void AJoinToAMissingFileChangesNothing()
    {
        using var test = new TestDatabase();
        var ini = Path.Combine(TempFolder(), "shop.ini");
        var lan = Lan(test, ini);

        // The folder is on this PC, so the test never waits for a network; the guard that matters
        // is the second one - no area11.db, no change.
        var refused = lan.JoinAsSecond(TestData.Owner, ToShare(Path.GetDirectoryName(test.Database.FilePath)!));

        Assert.False(refused.Ok);
        Assert.False(File.Exists(ini));
    }

    [Fact]
    public void TheStatusNamesTheModeAndTheFolder()
    {
        using var test = new TestDatabase();
        var lan = Lan(test);

        var status = lan.Status(TestData.Owner);

        Assert.True(status.Ok, status.Error);

        var value = status.Value!;

        Assert.Equal(ShopMode.Single, value.Mode);
        Assert.Equal("One computer", value.ModeLabel);
        Assert.Equal(Path.GetDirectoryName(test.Database.FilePath), value.DatabaseFolder);
        Assert.Equal(Environment.MachineName, value.ThisPc);
        Assert.True(value.Reachable, value.CheckMessage);
        Assert.False(value.RestartPending);
        Assert.False(string.IsNullOrWhiteSpace(value.JournalMode));
    }

    [Fact]
    public void TheCountersThatJoinedAreListed()
    {
        using var test = new TestDatabase();
        var lan = Lan(test);

        test.Database.Tx(connection => test.Audit.Record(connection, TestData.Owner, "system", "lan_connect",
            "lan", "COUNTER-2", details: "COUNTER-2"));

        var value = lan.Status(TestData.Owner).Value!;

        Assert.Single(value.Connections);
        Assert.Equal("COUNTER-2", value.Connections[0].PcLabel);
        Assert.Equal("Owner", value.Connections[0].UserLabel);
        Assert.Equal(16, value.Connections[0].WhenLabel.Length);
        Assert.NotEqual("-", value.Connections[0].WhenLabel);
    }

    [Fact]
    public void TheConnectionTestTakesTheWriteLockAndRecordsIt()
    {
        using var test = new TestDatabase();
        var lan = Lan(test);

        var tested = lan.TestConnection(TestData.Owner);

        Assert.True(tested.Ok, tested.Error);
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'lan_test';"));
    }

    // ============================== helpers ==============================

    private static LanService Lan(TestDatabase test, string? ini = null) =>
        new(test.Database, test.Audit, test.Log, iniPath: ini ?? Path.Combine(TempFolder(), "shop.ini"));

    private static string TempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-lan-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>`C:\x` -> `\\MACHINE\x` is not a real share; the test only needs the shape.</summary>
    private static string ToShare(string localFolder) =>
        "\\\\" + Environment.MachineName + "\\" + Path.GetFileName(localFolder);

    private static long Rows(TestDatabase test, string sql)
    {
        using var connection = test.Database.Open();
        return Convert.ToInt64(test.Database.Scalar(connection, sql) ?? 0);
    }
}
