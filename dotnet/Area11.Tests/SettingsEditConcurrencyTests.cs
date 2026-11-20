using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Real-schema checked Settings form cases,written but not executed here.
/// No real Windows registry,shop database or credentials. Not durable posting identity.</summary>
public sealed class SettingsEditConcurrencyTests
{
    private static readonly User Owner = TestData.Owner;
    private static SettingsEditForm Load(TestDatabase t, params string[] keys)
    {
        var result = t.Settings.LoadEditForm(Owner, keys); Assert.True(result.Ok, result.Error); return result.Value!;
    }
    private static Dictionary<string, object?> Values(params (string Key, object? Value)[] pairs) => pairs.ToDictionary(p => p.Key, p => p.Value);
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs;")));
    private static void Changed(ServiceResult r) { Assert.False(r.Ok); Assert.True(r.NoCommitConfirmed); Assert.Contains("changed", r.Error!); Assert.Null(r.SettingsEdit); }
    private static SettingsService Other(TestDatabase t)
    {
        var db = new Database(t.Database.FilePath, pooling: false, durable: false); var audit = new AuditService(db, t.Log);
        return new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
    }

    [Fact]
    public void TwoOpenFormsCannotSilentlyOverwriteTheSameSetting()
    {
        using var t = new TestDatabase(); var a = Load(t, "store.name"); var b = Load(t, "store.name");
        Assert.True(t.Settings.SaveEditForm(Values(("store.name", "Synthetic A")), Owner, a).Ok);
        var audits = Audits(t); Changed(t.Settings.SaveEditForm(Values(("store.name", "Synthetic B")), Owner, b));
        Assert.Equal(audits, Audits(t)); Assert.Equal("Synthetic A", t.Settings.GetString("store.name"));
    }

    [Fact]
    public void SeparateDatabaseWriterInvalidatesLoadedStateEvenWhenCacheIsOld()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.name"); var cached = t.Settings.GetRaw("store.name");
        Assert.True(Other(t).Set("store.name", "Synthetic other connection", Owner).Ok);
        Assert.Equal(cached, t.Settings.GetRaw("store.name"));
        Changed(t.Settings.SaveEditForm(Values(("store.name", "stale")), Owner, old));
    }

    [Fact]
    public void LoadReadsCurrentDatabaseRatherThanTheSharedCache()
    {
        using var t = new TestDatabase(); Assert.True(Other(t).Set("store.name", "Synthetic fresh", Owner).Ok);
        Assert.Equal("Synthetic fresh", Load(t, "store.name").GetString("store.name"));
    }

    [Fact]
    public void BulkConflictDoesNotPartiallyWriteEarlierKeysOrAudit()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.phone", "store.name"); var phone = t.Settings.GetRaw("store.phone");
        Assert.True(Other(t).Set("store.name", "Synthetic new name", Owner).Ok); var audits = Audits(t);
        Changed(t.Settings.SaveEditForm(Values(("store.phone", "would be first"), ("store.name", "stale name")), Owner, old));
        Assert.Equal(phone, t.Settings.GetRaw("store.phone")); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void WrittenKeysHandBackExactStateForAnotherSave()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.name");
        var saved = t.Settings.SaveEditForm(Values(("store.name", "Synthetic first")), Owner, old); Assert.True(saved.Ok, saved.Error);
        Assert.Equal("Synthetic first", saved.SettingsEdit!.GetString("store.name"));
        var next = t.Settings.SaveEditForm(Values(("store.name", "Synthetic second")), Owner, saved.SettingsEdit); Assert.True(next.Ok, next.Error);
    }

    [Fact]
    public void HandoffDoesNotRebaseAnUntouchedKeyChangedBySomeoneElse()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.name", "store.phone");
        Assert.True(Other(t).Set("store.phone", "Synthetic different phone", Owner).Ok);
        var name = t.Settings.SaveEditForm(Values(("store.name", "Synthetic reviewed name")), Owner, old); Assert.True(name.Ok, name.Error);
        Changed(t.Settings.SaveEditForm(Values(("store.phone", "stale phone")), Owner, name.SettingsEdit));
    }

    [Fact]
    public void RuntimeHeartbeatDoesNotConflictWithUnrelatedFormFields()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.name");
        Assert.True(t.Settings.SetUiOnly("session.lastSeen", "synthetic heartbeat").Ok);
        var saved = t.Settings.SaveEditForm(Values(("store.name", "Synthetic shop")), Owner, old); Assert.True(saved.Ok, saved.Error);
    }

    [Fact]
    public void NumberingCounterMovementDoesNotInvalidateAnOrdinaryForm()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.name");
        t.Database.Tx(c => { new NumberingService(t.Settings).Take(c, "sale"); });
        var saved = t.Settings.SaveEditForm(Values(("store.name", "Synthetic shop")), Owner, old); Assert.True(saved.Ok, saved.Error);
    }

    [Fact]
    public void MissingSettingUsesDefaultButLaterInsertionStillConflicts()
    {
        using var t = new TestDatabase(); t.Database.Tx(c => t.Database.Execute(c, "DELETE FROM settings WHERE key='printer.copies';"));
        var old = Load(t, "printer.copies"); Assert.Equal(1, old.GetInt("printer.copies"));
        Assert.True(Other(t).Set("printer.copies", 1, Owner).Ok);
        Changed(t.Settings.SaveEditForm(Values(("printer.copies", 2)), Owner, old));
    }

    [Fact]
    public void TimestampOnlyChurnIsNotAValueConflict()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.name");
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET updated_at='2099-01-01' WHERE key='store.name';"));
        Assert.True(t.Settings.SaveEditForm(Values(("store.name", "Synthetic update")), Owner, old).Ok);
    }

    [Fact]
    public void SameTimestampButDifferentJsonIsAConflict()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.name");
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value='\"Synthetic direct change\"' WHERE key='store.name';"));
        Changed(t.Settings.SaveEditForm(Values(("store.name", "stale")), Owner, old));
    }

    [Fact]
    public void FormCannotWriteAKeyItDidNotLoad()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.name"); var audits = Audits(t);
        Changed(t.Settings.SaveEditForm(Values(("store.phone", "not loaded")), Owner, old)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void AnotherDatabaseContextCannotLendAFormToThisService()
    {
        using var t = new TestDatabase(); var other = Other(t); var opened = other.LoadEditForm(Owner, new[] { "store.name" }); Assert.True(opened.Ok, opened.Error);
        Changed(t.Settings.SaveEditForm(Values(("store.name", "wrong context")), Owner, opened.Value));
    }

    [Fact]
    public void CheckedPathRefusesMissingFormEvenInLegacyHost()
    {
        using var t = new TestDatabase(); var audits = Audits(t);
        Assert.False(t.Settings.SaveEditForm(Values(("store.name", "not loaded")), Owner, null).Ok); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void RecoveryHashAndUnknownKeysCannotBeLoadedIntoAnEditor()
    {
        using var t = new TestDatabase();
        Assert.False(t.Settings.LoadEditForm(Owner, new[] { AuthService.RecoveryWordKey }).Ok);
        Assert.False(t.Settings.LoadEditForm(Owner, new[] { "synthetic.unknown" }).Ok);
    }

    [Fact]
    public void CloudSecretIsNeverExposedAsAnEditorValue()
    {
        using var t = new TestDatabase(); Assert.True(t.Settings.Set(SyncService.KeyKey, "synthetic-private-value", Owner).Ok);
        var form = Load(t, SyncService.KeyKey, SyncService.EnabledKey); Assert.True(form.HasCloudKey);
        Assert.Equal("null", form.GetRaw(SyncService.KeyKey)); Assert.Equal(string.Empty, form.GetString(SyncService.KeyKey));
        Assert.DoesNotContain("synthetic-private-value", JsonSerializer.Serialize(form));
    }

    [Fact]
    public void CloudConfigurationCannotUseAStaleMaskedKeyDependency()
    {
        using var t = new TestDatabase(); var form = Load(t, SyncService.KeyKey, SyncService.EnabledKey);
        Assert.True(Other(t).Set(SyncService.KeyKey, "synthetic-rotated-key", Owner).Ok); var audits = Audits(t);
        Changed(t.Settings.SaveEditForm(Values((SyncService.EnabledKey, false)), Owner, form)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void CloudRightsAreRequiredForLoadingCloudKeys()
    {
        using var t = new TestDatabase(); var manager = TestData.AddUser(t, "manager");
        Assert.True(t.Settings.LoadEditForm(manager, new[] { "store.name" }).Ok);
        Assert.False(t.Settings.LoadEditForm(manager, new[] { "store.name", SyncService.KeyKey }).Ok);
    }

    [Fact]
    public void ValidFormDoesNotReplaceCurrentActiveAccountCheck()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.name"); t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"));
        var audits = Audits(t); Assert.False(t.Settings.SaveEditForm(Values(("store.name", "inactive writer")), Owner, old).Ok); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void ManagerCanReadButCannotUseSnapshotToWriteOwnerOnlySetting()
    {
        using var t = new TestDatabase(); var manager = TestData.AddUser(t, "manager");
        var form = t.Settings.LoadEditForm(manager, new[] { "brand.appName" }); Assert.True(form.Ok, form.Error);
        Assert.False(t.Settings.SaveEditForm(Values(("brand.appName", "Synthetic name")), manager, form.Value).Ok);
    }

    [Fact]
    public void AuditFaultRollsBackValuesAndCacheAndKeepsTheOriginalBaselineRetryable()
    {
        using var t = new TestDatabase(); var old = Load(t, "store.name", "store.phone"); var name = t.Settings.GetRaw("store.name");
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;"));
        var failed = t.Settings.SaveEditForm(Values(("store.name", "Synthetic name"), ("store.phone", "Synthetic phone")), Owner, old);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Null(failed.SettingsEdit); Assert.Equal(name, t.Settings.GetRaw("store.name"));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_audit;"));
        var retry = t.Settings.SaveEditForm(Values(("store.name", "Synthetic retry")), Owner, old); Assert.True(retry.Ok, retry.Error);
    }

    [Fact]
    public void CheckedFormWorksWithARealEnabledSessionButNotACopiedUser()
    {
        using var t = new TestDatabase(); t.Database.Sessions.Enable();
        var login = new AuthService(t.Database, t.Audit, t.Settings, t.Log).Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        var opened = t.Settings.LoadEditForm(login.Value, new[] { "store.name" }); Assert.True(opened.Ok, opened.Error);
        Assert.False(t.Settings.SaveEditForm(Values(("store.name", "copied actor")), Owner, opened.Value).Ok);
        Assert.True(t.Settings.SaveEditForm(Values(("store.name", "Synthetic authenticated")), login.Value, opened.Value).Ok);
    }

    [Fact]
    public void LabelResetCannotOverwriteAConcurrentLabelEdit()
    {
        using var t = new TestDatabase(); var keys = AppLabelService.Definitions.Select(d => d.SettingKey).ToArray(); var old = Load(t, keys);
        Assert.True(Other(t).Set(keys[0], "Synthetic changed label", Owner).Ok); var audits = Audits(t);
        var labels = new AppLabelService(t.Settings, TestData.Permissions(t)); Changed(labels.Reset(Owner, old)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void SettingsEditMetadataIsNotSerializedWithServiceResults()
    {
        using var t = new TestDatabase(); var result = t.Settings.SaveEditForm(Values(("store.name", "Synthetic saved")), Owner, Load(t, "store.name"));
        Assert.True(result.Ok, result.Error); Assert.DoesNotContain("SettingsEdit", JsonSerializer.Serialize(result));
        Assert.DoesNotContain("Baselines", JsonSerializer.Serialize(result));
    }

    private sealed class SettingsFakeStart : IAutoStartStore
    {
        public int Mutations { get; private set; }
        public bool Enabled { get; private set; }
        public void Enable(string path) { Mutations++; Enabled = true; }
        public void Disable() { Mutations++; Enabled = false; }
        public bool IsEnabled() => Enabled;
    }

    [Fact]
    public void StaleStartupFormIsRefusedBeforeTouchingFakeWindowsEntries()
    {
        using var t = new TestDatabase(); var old = Load(t, StartupService.ModeKey);
        Assert.True(Other(t).Set(StartupService.ModeKey, StartupService.Auto, Owner).Ok);
        var machine = new SettingsFakeStart(); var user = new SettingsFakeStart();
        var startup = new StartupService(t.Settings, new AuthService(t.Database, t.Audit, t.Settings, t.Log), TestData.Permissions(t), t.Audit, t.Log, machine, user);
        var result = startup.SetMode(Owner, StartupService.Manual, "synthetic.exe", old); Assert.False(result.Ok);
        Assert.Equal(0, machine.Mutations); Assert.Equal(0, user.Mutations);
    }
}
