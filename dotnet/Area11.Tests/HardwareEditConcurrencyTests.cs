using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated DB only;no actual Print calls or hardware/Windows certification.</summary>
public sealed class HardwareEditConcurrencyTests
{
    private static HardwareService Hardware(TestDatabase t, SettingsService? settings = null) =>
        new(settings ?? t.Settings, t.Audit, new PermissionService(t.Database, t.Log, t.Audit), t.Log);
    private static HardwareReport Load(HardwareService service, User? actor = null)
    {
        var result = service.Load(actor ?? TestData.Owner);
        Assert.True(result.Ok, result.Error); Assert.NotNull(result.Value!.SettingsEdit); return result.Value;
    }
    private static string[] All(HardwareReport report) => report.Checks.Select(c => c.Key).ToArray();
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n",
        new[] { "settings", "audit_logs", "sqlite_sequence" }.Select(table =>
        {
            var columns = t.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(col => "quote(\"" + col + "\")"));
            return table + "=" + string.Join(";", t.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));

    [Fact]
    public void OldHardwareFormCannotOverwriteTheOtherRecordedTest()
    {
        using var t = new TestDatabase(); var hardware = Hardware(t); var one = Load(hardware); var two = Load(hardware);
        Assert.True(hardware.Save(TestData.Owner, All(one), "first", one.SettingsEdit).Ok); var before = State(t);
        var conflict = hardware.Save(TestData.Owner, All(two), "second", two.SettingsEdit);
        Assert.False(conflict.Ok); Assert.True(conflict.NoCommitConfirmed); Assert.Contains("changed", conflict.Error!);
        Assert.Equal(before, State(t)); Assert.Equal("first", Load(hardware).Note);
    }

    [Theory]
    [InlineData("printer.width", "80")]
    [InlineData("printer.name", "\"synthetic printer\"")]
    [InlineData("printer.copies", "2")]
    [InlineData("printer.autoCut", "false")]
    [InlineData("hardware.note", "\"concurrent note\"")]
    [InlineData("hardware.passed", "\"printed\"")]
    [InlineData("hardware.testedAt", "\"2026-11-10 14:00:00\"")]
    [InlineData("hardware.testedBy", "\"concurrent staff\"")]
    [InlineData("hardware.testedWidth", "80")]
    public void EveryRecordAndPrinterDependencyIsCompared(string key, string value)
    {
        using var t = new TestDatabase(); var hardware = Hardware(t); var old = Load(hardware);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value=@value WHERE key=@key;", new { key, value }));
        var before = State(t); var result = hardware.Save(TestData.Owner, All(old), "old ticks", old.SettingsEdit);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, State(t));
    }

    [Fact]
    public void AuditFaultRollsBackAllFiveValuesAndAllowsReviewedRetry()
    {
        using var t = new TestDatabase(); var hardware = Hardware(t); var old = Load(hardware);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_hardware BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;"));
        var before = State(t); var result = hardware.Save(TestData.Owner, All(old), "retry", old.SettingsEdit);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, State(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_hardware;"));
        Assert.True(hardware.Save(TestData.Owner, All(old), "retry", old.SettingsEdit).Ok);
    }

    [Fact]
    public void ASettingsWriteFaultDoesNotLeaveTheOtherFourValues()
    {
        using var t = new TestDatabase(); var hardware = Hardware(t); var old = Load(hardware);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_hardware_key BEFORE UPDATE ON settings WHEN NEW.key='hardware.note' BEGIN SELECT RAISE(ABORT,'synthetic'); END;"));
        var before = State(t); var result = hardware.Save(TestData.Owner, All(old), "rollback", old.SettingsEdit);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, State(t));
    }

    [Fact]
    public void SuccessfulTestHasOneAuditAndAnExactReusableBaseline()
    {
        using var t = new TestDatabase(); var hardware = Hardware(t); var old = Load(hardware);
        var result = hardware.Save(TestData.Owner, All(old), "one", old.SettingsEdit); Assert.True(result.Ok, result.Error);
        Assert.NotNull(result.Value!.SettingsEdit);
        Assert.True(hardware.Save(TestData.Owner, All(old), "two", result.Value.SettingsEdit).Ok);
        Assert.Equal(2L, t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs WHERE action='hardware_test';"))));
        Assert.DoesNotContain("SettingsEdit", JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public void CurrentDatabaseReadDoesNotUseTheOldSettingsCache()
    {
        using var t = new TestDatabase(); var hardware = Hardware(t);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value='80' WHERE key='printer.width';"));
        Assert.Equal(58, t.Settings.GetInt("printer.width")); Assert.Equal(80, Load(hardware).WidthMm);
    }

    [Fact]
    public void SettingsPermissionRevocationRefusesTheLoadedTest()
    {
        using var t = new TestDatabase(); var cashier = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        Assert.True(permissions.Set(TestData.Owner, cashier.Id, Modules.Settings, true).Ok);
        var hardware = Hardware(t); var old = Load(hardware, cashier);
        Assert.True(permissions.Set(TestData.Owner, cashier.Id, Modules.Settings, false).Ok);
        var before = State(t); Assert.False(hardware.Save(cashier, All(old), "revoked", old.SettingsEdit).Ok);
        Assert.Equal(before, State(t));
    }

    [Fact]
    public void TokenFromAnotherDatabaseInstanceCannotSaveHardware()
    {
        using var t = new TestDatabase(); var db = new Database(t.Database.FilePath, pooling: false, durable: false);
        var audit = new AuditService(db, t.Log); var otherSettings = new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
        var old = Load(Hardware(t, otherSettings)); var before = State(t);
        Assert.False(Hardware(t).Save(TestData.Owner, All(old), "foreign context", old.SettingsEdit).Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void UnrelatedTelemetryDoesNotConflictWithHardware()
    {
        using var t = new TestDatabase(); var hardware = Hardware(t); var old = Load(hardware);
        Assert.True(t.Settings.SetUiOnly("backup.lastRunAt", "synthetic telemetry").Ok);
        Assert.True(hardware.Save(TestData.Owner, All(old), "valid", old.SettingsEdit).Ok);
    }

    [Fact]
    public void EnabledHostRequiresLoadedBaselineForRecordAndPrinterChoice()
    {
        using var t = new TestDatabase(); t.Database.Sessions.Enable();
        var login = new AuthService(t.Database, t.Audit, t.Settings, t.Log).Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        var owner = login.Value!; var hardware = Hardware(t); var old = Load(hardware, owner);
        Assert.False(hardware.Save(owner, All(old), "missing").Ok);
        Assert.False(hardware.ChoosePrinter(owner, string.Empty).Ok);
        Assert.True(hardware.Save(owner, All(old), "loaded", old.SettingsEdit).Ok);
    }

    [Fact]
    public void PrinterChoiceDoesNotRebaseUntouchedStaleHardwareFields()
    {
        using var t = new TestDatabase(); var hardware = Hardware(t); var old = Load(hardware);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value='\"new note\"' WHERE key='hardware.note';"));
        var chosen = hardware.ChoosePrinter(TestData.Owner, string.Empty, old.SettingsEdit); Assert.True(chosen.Ok, chosen.Error);
        var before = State(t); var stale = hardware.Save(TestData.Owner, All(old), "old note", chosen.SettingsEdit);
        Assert.False(stale.Ok); Assert.True(stale.NoCommitConfirmed); Assert.Equal(before, State(t));
    }

    [Fact]
    public void OldPrinterChoiceCannotOverwriteAConcurrentSelection()
    {
        using var t = new TestDatabase(); var hardware = Hardware(t); var old = Load(hardware);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value='\"other printer\"' WHERE key='printer.name';"));
        var before = State(t); var chosen = hardware.ChoosePrinter(TestData.Owner, string.Empty, old.SettingsEdit);
        Assert.False(chosen.Ok); Assert.True(chosen.NoCommitConfirmed); Assert.Equal(before, State(t));
    }
}
