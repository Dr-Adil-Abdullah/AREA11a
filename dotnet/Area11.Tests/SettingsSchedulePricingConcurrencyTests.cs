using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Written public-service cases for part5.1. No real backups,registry or shop DB.
/// These require actual C#/provider execution;UI post-ack callbacks remain a Windows gate.</summary>
public sealed class SettingsSchedulePricingConcurrencyTests
{
    private static readonly User Owner = TestData.Owner;
    private static readonly string[] ScheduleKeys = { "backup.autoEnabled", "backup.everyHours", "backup.keep" };
    private static SettingsEditForm Schedule(TestDatabase t, User? actor = null)
    {
        var loaded = t.Settings.LoadEditForm(actor ?? Owner, ScheduleKeys); Assert.True(loaded.Ok, loaded.Error); return loaded.Value!;
    }
    private static Dictionary<string, object?> ScheduleValues(SettingsEditForm form) => new()
    {
        ["backup.autoEnabled"] = form.GetBool("backup.autoEnabled"),
        ["backup.everyHours"] = form.GetInt("backup.everyHours", 24), ["backup.keep"] = form.GetInt("backup.keep", 7)
    };
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs;")));
    private static string Snapshot(TestDatabase t) => t.Database.Read(c => string.Join("\n", new[]
    { "settings", "user_pricing_allowances", "audit_logs", "sqlite_sequence", "sales", "purchases", "payments", "stock_movements" }.Select(table =>
    {
        var cols = t.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
        var projection = string.Join("||':'||", cols.Select(col => "quote(\"" + col + "\")"));
        return table + "=" + string.Join(";", t.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
    })));
    private static void Conflict(ServiceResult result)
    { Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Contains("changed", result.Error!); }
    private static PricingAllowanceService Policy(TestDatabase t, Database? db = null)
    { db ??= t.Database; return new PricingAllowanceService(db, new AuditService(db, t.Log), t.Log); }
    private static PricingAllowance Grant(User target, decimal limit = 10) => new()
    {
        UserId = target.Id, EditRate = true, ManualReductionPercent = limit, ProfitDiscount = true,
        ProfitPercent = limit, RetailDiscount = true, RetailPercent = limit, ExtraDiscount = true, ExtraLimitPaisa = 100
    };

    [Theory]
    [InlineData("backup.autoEnabled", false)]
    [InlineData("backup.everyHours", 48)]
    [InlineData("backup.keep", 14)]
    public void AnotherScheduleEditRefusesTheEntireOldSchedule(string key, object value)
    {
        using var t = new TestDatabase(); var old = Schedule(t); var values = ScheduleValues(old);
        Assert.True(t.Settings.Set(key, value, Owner).Ok); var before = Snapshot(t);
        Conflict(t.Settings.SaveEditForm(values, Owner, old)); Assert.Equal(before, Snapshot(t));
    }

    [Fact]
    public void ScheduleSuccessHandsBackExactStateForTheNextSave()
    {
        using var t = new TestDatabase(); var old = Schedule(t); var values = ScheduleValues(old); values["backup.keep"] = 10;
        var first = t.Settings.SaveEditForm(values, Owner, old); Assert.True(first.Ok, first.Error);
        Assert.Equal(10, first.SettingsEdit!.GetInt("backup.keep")); values["backup.keep"] = 12;
        var next = t.Settings.SaveEditForm(values, Owner, first.SettingsEdit); Assert.True(next.Ok, next.Error);
    }

    [Fact]
    public void AutomaticBackupTelemetryDoesNotInvalidateTheSchedule()
    {
        using var t = new TestDatabase(); var old = Schedule(t);
        Assert.True(t.Settings.SetUiOnly("backup.lastRunAt", "synthetic-telemetry").Ok);
        Assert.True(t.Settings.SetUiOnly("backup.lastFile", "synthetic.a11bak").Ok);
        var result = t.Settings.SaveEditForm(ScheduleValues(old), Owner, old); Assert.True(result.Ok, result.Error);
    }

    [Fact]
    public void BackupOnlyGranteeCanLoadAndSaveWithoutSettingsModule()
    {
        using var t = new TestDatabase(); var cashier = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        Assert.True(permissions.Set(Owner, cashier.Id, Modules.Backup, true).Ok);
        Assert.True(permissions.Set(Owner, cashier.Id, Modules.Settings, false).Ok);
        var form = Schedule(t, cashier); var result = t.Settings.SaveEditForm(ScheduleValues(form), cashier, form);
        Assert.True(result.Ok, result.Error);
    }

    [Fact]
    public void RevokedBackupAndSettingsRightsCannotUseAnOldSchedule()
    {
        using var t = new TestDatabase(); var cashier = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        Assert.True(permissions.Set(Owner, cashier.Id, Modules.Backup, true).Ok); var old = Schedule(t, cashier);
        Assert.True(permissions.Set(Owner, cashier.Id, Modules.Backup, false).Ok);
        Assert.True(permissions.Set(Owner, cashier.Id, Modules.Settings, false).Ok); var before = Snapshot(t);
        Assert.False(t.Settings.SaveEditForm(ScheduleValues(old), cashier, old).Ok); Assert.Equal(before, Snapshot(t));
    }

    [Fact]
    public void ScheduleAuditFaultRollsBackAllKeysThenAllowsReviewedRetry()
    {
        using var t = new TestDatabase(); var old = Schedule(t); var values = ScheduleValues(old); values["backup.keep"] = 13;
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;"));
        var before = Snapshot(t); var failed = t.Settings.SaveEditForm(values, Owner, old);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Equal(before, Snapshot(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_audit;"));
        Assert.True(t.Settings.SaveEditForm(values, Owner, old).Ok);
    }

    [Fact]
    public void ScheduleFromAnotherDatabaseContextIsNotAnAuthorizationToOverwrite()
    {
        using var t = new TestDatabase(); var db = new Database(t.Database.FilePath, pooling: false, durable: false);
        var audit = new AuditService(db, t.Log); var other = new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
        var loaded = other.LoadEditForm(Owner, ScheduleKeys); Assert.True(loaded.Ok, loaded.Error);
        Conflict(t.Settings.SaveEditForm(ScheduleValues(loaded.Value!), Owner, loaded.Value));
    }

    [Fact]
    public void ScheduleLoadFailureAndMissingBaselineCannotInventASuccessfulSave()
    {
        using var t = new TestDatabase(); var old = Schedule(t); var audits = Audits(t);
        Assert.False(t.Settings.SaveEditForm(ScheduleValues(old), Owner, null).Ok); Assert.Equal(audits, Audits(t));
        t.Database.Tx(c => t.Database.Execute(c, "ALTER TABLE settings RENAME TO synthetic_hidden_settings;"));
        var failed = t.Settings.LoadEditForm(Owner, ScheduleKeys); Assert.False(failed.Ok); Assert.Null(failed.Value);
    }

    [Fact]
    public void TwoPricingEditorsCannotOverwriteTheFirstSavedGrant()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t);
        var one = p.ForUser(Owner, target); var two = p.ForUser(Owner, target);
        Assert.True(p.Save(Owner, Grant(target, 15), one.EditVersion).Ok); var before = Snapshot(t);
        Conflict(p.Save(Owner, Grant(target, 5), two.EditVersion)); Assert.Equal(before, Snapshot(t));
        Assert.Equal(15m, p.ForUser(Owner, target).ManualReductionPercent);
    }

    [Fact]
    public void MissingPricingRowAndEquivalentExplicitDefaultAreDifferentBaselines()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t); var old = p.ForUser(Owner, target);
        Assert.True(p.Save(Owner, new PricingAllowance { UserId = target.Id }, old.EditVersion).Ok);
        Conflict(p.Save(Owner, Grant(target), old.EditVersion));
    }

    [Fact]
    public void PricingChangeOnAnotherDatabaseHandleIsDetected()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t); var old = p.ForUser(Owner, target);
        var other = Policy(t, new Database(t.Database.FilePath, pooling: false, durable: false));
        var fresh = other.ForUser(Owner, target); Assert.True(other.Save(Owner, Grant(target, 22), fresh.EditVersion).Ok);
        var before = Snapshot(t); Conflict(p.Save(Owner, Grant(target, 8), old.EditVersion)); Assert.Equal(before, Snapshot(t));
    }

    [Fact]
    public void PricingTokenForAnotherTargetCannotBeUsed()
    {
        using var t = new TestDatabase(); var first = TestData.Cashier(t); var second = TestData.AddUser(t, "manager"); var p = Policy(t);
        Conflict(p.Save(Owner, Grant(second), p.ForUser(Owner, first).EditVersion));
    }

    [Fact]
    public void PricingTokenIsDatabaseInstanceBound()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t);
        var other = Policy(t, new Database(t.Database.FilePath, pooling: false, durable: false));
        Conflict(Policy(t).Save(Owner, Grant(target), other.ForUser(Owner, target).EditVersion));
    }

    [Fact]
    public void ProductTokenCannotAuthorizePricingGrant()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var product = TestData.AddProduct(t);
        var loaded = new ProductService(t.Database, t.Audit, t.Log).Get(Owner, product); Assert.True(loaded.Ok, loaded.Error);
        Conflict(Policy(t).Save(Owner, Grant(target), loaded.Value!.EditVersion));
    }

    [Fact]
    public void TargetRoleChangeInvalidatesTheDefaultsAndAllowanceBaseline()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t); var old = p.ForUser(Owner, target);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET role='manager' WHERE id=@id;", new { id = target.Id }));
        var before = Snapshot(t); Conflict(p.Save(Owner, Grant(target), old.EditVersion)); Assert.Equal(before, Snapshot(t));
    }

    [Fact]
    public void TargetDeactivationRefusesAnOldPricingEditor()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t); var old = p.ForUser(Owner, target);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=@id;", new { id = target.Id }));
        var before = Snapshot(t); Assert.False(p.Save(Owner, Grant(target), old.EditVersion).Ok); Assert.Equal(before, Snapshot(t));
    }

    [Fact]
    public void OwnerDeactivationIsNotBypassedByAValidPricingToken()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t); var old = p.ForUser(Owner, target);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"));
        var before = Snapshot(t); Assert.False(p.Save(Owner, Grant(target), old.EditVersion).Ok); Assert.Equal(before, Snapshot(t));
    }

    [Fact]
    public void EnabledPricingHostRequiresBaselineAndAllowsFreshReviewedSave()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t); t.Database.Sessions.Enable();
        var login = new AuthService(t.Database, t.Audit, t.Settings, t.Log).Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        var owner = login.Value!; Conflict(p.Save(owner, Grant(target)));
        var old = p.ForUser(owner, target); Assert.True(p.Save(owner, Grant(target), old.EditVersion).Ok);
    }

    [Fact]
    public void PricingSuccessReturnsExactNextTokenAndDoesNotSerializeIt()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t); var old = p.ForUser(Owner, target);
        var saved = p.Save(Owner, Grant(target), old.EditVersion); Assert.True(saved.Ok, saved.Error); Assert.NotNull(saved.EditVersion);
        Assert.True(p.Save(Owner, Grant(target, 20), saved.EditVersion).Ok);
        Conflict(p.Save(Owner, Grant(target), old.EditVersion));
        Assert.DoesNotContain("EditVersion", JsonSerializer.Serialize(p.ForUser(Owner, target)));
        var payload = t.Database.Read(c => Convert.ToString(t.Database.Scalar(c, "SELECT new_value FROM audit_logs WHERE action='pricing_permissions' ORDER BY id DESC LIMIT 1;")) ?? "");
        Assert.DoesNotContain("EditVersion", payload);
    }

    [Fact]
    public void PricingAuditFailureRollsBackGrantAndOriginalTokenStillRetries()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t); var old = p.ForUser(Owner, target);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_pricing BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;"));
        var before = Snapshot(t); var failed = p.Save(Owner, Grant(target), old.EditVersion);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Null(failed.EditVersion); Assert.Equal(before, Snapshot(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_pricing;"));
        Assert.True(p.Save(Owner, Grant(target), old.EditVersion).Ok);
    }

    [Fact]
    public void PricingAuditUsesStoredOwnerNameNotSuppliedText()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t); var old = p.ForUser(Owner, target);
        var supplied = new User { Id = Owner.Id, Name = "SYNTHETIC SPOOF", Role = "owner" };
        Assert.True(p.Save(supplied, Grant(target), old.EditVersion).Ok);
        var name = t.Database.Read(c => Convert.ToString(t.Database.Scalar(c, "SELECT user_name FROM audit_logs WHERE action='pricing_permissions' ORDER BY id DESC LIMIT 1;")));
        Assert.Equal(TestData.SeedOwnerName, name);
    }

    [Fact]
    public void NonOwnerCannotUseTheOwnersLoadedPricingToken()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Policy(t); var old = p.ForUser(Owner, target);
        var before = Snapshot(t); Assert.False(p.Save(target, Grant(target), old.EditVersion).Ok); Assert.Equal(before, Snapshot(t));
    }

    [Fact]
    public void StaffTargetNeedsNoOwnLoginWhenOwnerOpensItsPricingForm()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); t.Database.Sessions.Enable();
        var login = new AuthService(t.Database, t.Audit, t.Settings, t.Log).Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        var p = Policy(t); var read = p.ForUser(login.Value!, target); Assert.NotNull(read.EditVersion);
        Assert.Equal(target.Id, read.UserId); Assert.True(p.Save(login.Value!, Grant(target), read.EditVersion).Ok);
    }
}
