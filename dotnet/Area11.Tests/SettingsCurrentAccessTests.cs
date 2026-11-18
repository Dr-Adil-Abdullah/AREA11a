using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Real isolated schema fixtures. Written regressions, not a claim of .NET/Windows execution.</summary>
public sealed class SettingsCurrentAccessTests
{
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, sql)));
    private static string Snapshot(TestDatabase t) => t.Database.Read(c => string.Join("\n", t.Database.Query<string>(c, "SELECT key || '=' || value FROM settings ORDER BY key;")));
    private static void Grant(TestDatabase t, User u, string module, bool allow = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, module, allow).Ok);
    private static void Denied(TestDatabase t, User? u)
    {
        var before = Snapshot(t); var cached = t.Settings.GetRaw("store.name"); var audits = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        Assert.False(t.Settings.CheckAccess(u).Ok);
        Assert.False(t.Settings.SetMany(new Dictionary<string, object?> { ["store.name"] = "Synthetic denied shop", ["printer.width"] = 80 }, u).Ok);
        Assert.Equal(before, Snapshot(t)); Assert.Equal(cached, t.Settings.GetRaw("store.name"));
        Assert.Equal(audits, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }

    [Fact] public void AnonymousCannotSaveOrReadSettingsAccess()
    {
        using var t = new TestDatabase(); Denied(t, null);
    }
    [Fact] public void InactiveSuppliedOwnerCannotSave()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = 1, Role = "owner", Active = false });
    }
    [Fact] public void MissingStoredOwnerCannotSave()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = int.MaxValue, Role = "owner" });
    }
    [Fact] public void ForgedOwnerCannotBorrowStaffIdentity()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Denied(t, new User { Id = u.Id, Role = "owner" });
    }
    [Fact] public void ChangedStoredRoleCannotSaveFromOldSettingsPage()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE users SET role='manager' WHERE id=1;"); Denied(t, TestData.Owner);
    }
    [Fact] public void InactiveStoredOwnerCannotSave()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE users SET active=0 WHERE id=1;"); Denied(t, TestData.Owner);
    }
    [Fact] public void RevokedSettingsBlocksAndRegrantRestoresOrdinarySave()
    {
        using var t = new TestDatabase(); var u = TestData.AddUser(t, "manager"); Grant(t, u, Modules.Settings, false); Denied(t, u);
        Grant(t, u, Modules.Settings); Assert.True(t.Settings.CheckAccess(u).Ok);
        Assert.True(t.Settings.Set("store.name", "Synthetic restored shop", u).Ok);
        Assert.Equal("Synthetic restored shop", t.Settings.GetString("store.name"));
    }
    [Fact] public void DelegatedSettingsWriterUsesStoredAuditName()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Settings);
        Execute(t, "UPDATE users SET name='Current synthetic name' WHERE id=" + u.Id);
        Assert.True(t.Settings.Set("printer.width", 80, u).Ok);
        var name = t.Database.Read(c => t.Database.QuerySingle<string>(c, "SELECT user_name FROM audit_logs WHERE action='settings_change' AND entity_id='printer.width' ORDER BY id DESC LIMIT 1;"));
        Assert.Equal("Current synthetic name", name);
    }
    [Fact] public void ServiceWithoutPermissionsCannotSaveEvenOrdinarySettings()
    {
        using var t = new TestDatabase(); var service = new SettingsService(t.Database, t.Audit, t.Log); var before = Snapshot(t);
        Assert.False(service.Set("printer.width", 80, TestData.Owner).Ok); Assert.Equal(before, Snapshot(t));
    }
    [Fact] public void FailedGrantReadDoesNotInventManagerSettingsAccess()
    {
        using var t = new TestDatabase(); var u = TestData.AddUser(t, "manager"); Execute(t, "DROP TABLE user_permissions;"); Denied(t, u);
    }
    [Fact] public void PosOnlyMaySaveOnlyTheCounterAutoPrintSwitch()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t);
        Assert.False(t.Settings.CheckAccess(u).Ok); Assert.True(t.Settings.Set("printer.autoPrint", false, u).Ok);
        Assert.False(t.Settings.GetBool("printer.autoPrint", true));
    }
    [Fact] public void PosOnlyCannotSmuggleShopChangeInAutoPrintDictionary()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var before = Snapshot(t);
        Assert.False(t.Settings.SetMany(new Dictionary<string, object?> { ["printer.autoPrint"] = false, ["store.name"] = "Synthetic smuggled shop" }, u).Ok);
        Assert.Equal(before, Snapshot(t));
    }
    [Fact] public void RevokedPosCannotUseAutoPrintException()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Pos, false);
        Assert.False(t.Settings.Set("printer.autoPrint", false, u).Ok);
    }
    [Fact] public void DelegatedBackupMaySaveTheThreeScheduleKeys()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Backup);
        Assert.True(t.Settings.SetMany(new Dictionary<string, object?> { ["backup.autoEnabled"] = false, ["backup.everyHours"] = 12, ["backup.keep"] = 9 }, u).Ok);
        Assert.Equal(12, t.Settings.GetInt("backup.everyHours")); Assert.Equal(9, t.Settings.GetInt("backup.keep"));
    }
    [Fact] public void BackupGrantCannotSmuggleOtherSettings()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Backup); var before = Snapshot(t);
        Assert.False(t.Settings.SetMany(new Dictionary<string, object?> { ["backup.keep"] = 9, ["printer.width"] = 80 }, u).Ok);
        Assert.Equal(before, Snapshot(t));
    }
    [Fact] public void DelegatedSyncMaySaveCloudOnlyConfiguration()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Sync);
        Assert.True(t.Settings.CheckCloudAccess(u).Ok); Assert.False(t.Settings.CheckAccess(u).Ok);
        Assert.True(t.Settings.SetMany(new Dictionary<string, object?> { ["sync.enabled"] = true, ["sync.url"] = "https://synthetic.example", ["sync.key"] = "synthetic-cloud-key" }, u).Ok);
        Assert.Equal("synthetic-cloud-key", t.Settings.GetString("sync.key"));
    }
    [Fact] public void SettingsGrantAloneCannotChangeCloudConfiguration()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Settings); var before = Snapshot(t);
        Assert.False(t.Settings.CheckCloudAccess(u).Ok); Assert.False(t.Settings.Set("sync.key", "synthetic-key", u).Ok);
        Assert.Equal(before, Snapshot(t));
    }
    [Fact] public void MixedCloudAndShopDictionaryRequiresBothGrants()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Sync); var before = Snapshot(t);
        var values = new Dictionary<string, object?> { ["store.name"] = "Synthetic mixed shop", ["sync.enabled"] = true };
        Assert.False(t.Settings.SetMany(values, u).Ok); Assert.Equal(before, Snapshot(t));
        Grant(t, u, Modules.Settings); Assert.True(t.Settings.SetMany(values, u).Ok);
    }
    [Fact] public void RevokedCloudIsNotBorrowedFromOldRoleOrSettingsGrant()
    {
        using var t = new TestDatabase(); var u = TestData.AddUser(t, "manager"); Grant(t, u, Modules.Sync); Assert.True(t.Settings.CheckCloudAccess(u).Ok);
        Grant(t, u, Modules.Sync, false); Assert.False(t.Settings.CheckCloudAccess(u).Ok);
        Assert.False(t.Settings.Set("sync.automaticEnabled", true, u).Ok);
    }
    [Fact] public void QuietDoorRejectsEveryNonRuntimeKnownKeyWithoutWrites()
    {
        using var t = new TestDatabase(); var runtime = RuntimeValues(); var before = Snapshot(t); var audits = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        foreach (var key in SettingsService.KnownKeys.Where(k => !runtime.ContainsKey(k)))
            Assert.False(t.Settings.SetUiOnly(key, "synthetic-denied").Ok, key);
        Assert.Equal(before, Snapshot(t)); Assert.Equal(audits, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }
    private static Dictionary<string, object?> RuntimeValues() => new()
    {
        ["session.lastUser"] = "Synthetic", ["session.lastStart"] = "2026-11-10 09:00:00", ["session.lastSeen"] = "2026-11-10 09:00:01", ["session.cleanExit"] = false,
        ["session.handoverAt"] = "2026-11-10 09:00:00", ["session.handoverBy"] = "Synthetic",
        ["backup.lastRunAt"] = "2026-11-10 09:00:00", ["backup.lastFile"] = "synthetic.a11bak", ["backup.lastRestoreSummary"] = "Synthetic fixture",
        ["sync.lastSyncAt"] = "2026-11-10 09:00:00", ["sync.status"] = "synthetic", ["sync.lastError"] = "synthetic",
        ["hardware.testedAt"] = "2026-11-10 09:00:00", ["hardware.testedBy"] = "Synthetic", ["hardware.note"] = "Synthetic fixture", ["hardware.testedWidth"] = 58, ["hardware.passed"] = "synthetic"
    };
    [Fact] public void AllSeventeenRuntimeKeysRemainQuietAndKnown()
    {
        using var t = new TestDatabase(); var audits = Number(t, "SELECT COUNT(*) FROM audit_logs;"); var values = RuntimeValues(); Assert.Equal(17, values.Count);
        foreach (var p in values) { Assert.Contains(p.Key, SettingsService.KnownKeys); Assert.True(t.Settings.SetUiOnly(p.Key, p.Value).Ok, p.Key); }
        Assert.Equal(audits, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }
    [Fact] public void SettingsManagerCannotChangeSecurityOrStartupPolicy()
    {
        using var t = new TestDatabase(); var u = TestData.AddUser(t, "manager"); var before = Snapshot(t);
        Assert.False(t.Settings.Set("security.pinLength", 6, u).Ok); Assert.False(t.Settings.Set("security.sessionHours", 4, u).Ok);
        Assert.False(t.Settings.Set("startup.mode", StartupService.Auto, u).Ok); Assert.Equal(before, Snapshot(t));
    }
    [Fact] public void CurrentOwnerCanStillChangeValidSecuritySettings()
    {
        using var t = new TestDatabase(); Assert.True(t.Settings.Set("security.pinLength", 6, TestData.Owner).Ok);
        Assert.Equal(6, t.Settings.GetInt("security.pinLength"));
    }
    [Fact] public void LegacyDiscountCapsRemainOwnerOnly()
    {
        using var t = new TestDatabase(); var u = TestData.AddUser(t, "manager");
        Assert.False(t.Settings.Set("discount.maxPercentManager", 25, u).Ok);
        Assert.True(t.Settings.Set("discount.maxPercentManager", 25, TestData.Owner).Ok);
    }
    [Fact] public void NewCloudCredentialAuditNeverCopiesOldOrNewSecret()
    {
        using var t = new TestDatabase(); TestData.SeedRawSetting(t, "sync.key", "synthetic-OLD-secret");
        Assert.True(t.Settings.Set("sync.key", "synthetic-NEW-secret", TestData.Owner).Ok);
        var payload = t.Database.Read(c => t.Database.QuerySingle<string>(c, "SELECT COALESCE(old_value,'') || COALESCE(new_value,'') || COALESCE(details,'') FROM audit_logs WHERE action='settings_change' AND entity_id='sync.key' ORDER BY id DESC LIMIT 1;"));
        Assert.NotNull(payload); Assert.DoesNotContain("synthetic-OLD-secret", payload); Assert.DoesNotContain("synthetic-NEW-secret", payload);
        Assert.Contains("changed", payload); Assert.Equal("synthetic-NEW-secret", t.Settings.GetString("sync.key"));
    }
    [Fact] public void AuditFaultRollsBackWholeDictionaryAndDoesNotPublishCache()
    {
        using var t = new TestDatabase(); var before = Snapshot(t); var store = t.Settings.GetRaw("store.name"); var cloud = t.Settings.GetRaw("sync.key");
        Execute(t, "CREATE TRIGGER fail_settings_audit BEFORE INSERT ON audit_logs WHEN NEW.action='settings_change' BEGIN SELECT RAISE(ABORT,'synthetic settings audit fault'); END;");
        Assert.False(t.Settings.SetMany(new Dictionary<string, object?> { ["store.name"] = "Synthetic rollback shop", ["sync.key"] = "synthetic-rollback-secret" }, TestData.Owner).Ok);
        Assert.Equal(before, Snapshot(t)); Assert.Equal(store, t.Settings.GetRaw("store.name")); Assert.Equal(cloud, t.Settings.GetRaw("sync.key"));
        t.Settings.Reload(); Assert.Equal(store, t.Settings.GetRaw("store.name")); Assert.Equal(cloud, t.Settings.GetRaw("sync.key"));
    }
    [Fact] public void ProtectedKeyInOrdinaryDictionaryDeniesWholeSave()
    {
        using var t = new TestDatabase(); var u = TestData.AddUser(t, "manager"); var before = Snapshot(t);
        Assert.False(t.Settings.SetMany(new Dictionary<string, object?> { ["store.name"] = "Synthetic denied shop", ["security.pinLength"] = 6 }, u).Ok);
        Assert.Equal(before, Snapshot(t));
    }
    [Fact] public void ExistingValueValidationStillRejectsWholeSave()
    {
        using var t = new TestDatabase(); var before = Snapshot(t);
        Assert.False(t.Settings.SetMany(new Dictionary<string, object?> { ["store.name"] = "Synthetic invalid shop", ["printer.copies"] = 9 }, TestData.Owner).Ok);
        Assert.Equal(before, Snapshot(t));
    }
    [Fact] public void AccessChecksDoNotWriteSettingsAuditsOrMoney()
    {
        using var t = new TestDatabase(); var before = Snapshot(t); var audits = Number(t, "SELECT COUNT(*) FROM audit_logs;"); var money = Number(t, "SELECT COUNT(*) FROM payments;");
        Assert.True(t.Settings.CheckAccess(TestData.Owner).Ok); Assert.True(t.Settings.CheckCloudAccess(TestData.Owner).Ok);
        Assert.Equal(before, Snapshot(t)); Assert.Equal(audits, Number(t, "SELECT COUNT(*) FROM audit_logs;")); Assert.Equal(money, Number(t, "SELECT COUNT(*) FROM payments;"));
    }
    [Fact] public void GenericAndQuietDoorsStillRefuseRecoveryHash()
    {
        using var t = new TestDatabase(); var before = Snapshot(t);
        Assert.False(t.Settings.Set(AuthService.RecoveryWordKey, "synthetic-hash", TestData.Owner).Ok);
        Assert.False(t.Settings.SetUiOnly(AuthService.RecoveryWordKey, "synthetic-hash").Ok); Assert.Equal(before, Snapshot(t));
    }
    [Fact] public void CloudAccessFailureIsNotSuccessfulFalseConfiguration()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Sync);
        Execute(t, "DROP TABLE user_permissions;"); Assert.False(t.Settings.CheckCloudAccess(u).Ok);
        Assert.False(t.Settings.Set("sync.enabled", true, u).Ok);
    }
    [Fact] public void OrdinarySettingsSaveDoesNotRequireFinancialGrantsOrMain()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Settings);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.True(t.Settings.Set("store.footerNote", "Synthetic ordinary footer", u).Ok);
        Assert.False(TestData.Permissions(t).Can(u, Modules.Profit)); Assert.False(TestData.Permissions(t).Can(u, Modules.PurchaseCost));
    }
}
