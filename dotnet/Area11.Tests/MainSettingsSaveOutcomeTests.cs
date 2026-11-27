using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Real-schema public Settings + local guard cases. Not the Desktop SaveCore or WPF execution.</summary>
public sealed class MainSettingsSaveOutcomeTests
{
    private static Dictionary<string, object?> Values()
    {
        var values = AppLabelService.Definitions.ToDictionary(d => d.SettingKey, d => (object?)d.DefaultText);
        values["store.name"] = "Synthetic reviewed shop"; values["store.phone"] = "Synthetic phone";
        values["printer.width"] = 80; values["printer.copies"] = 2; values["printer.autoPrint"] = false;
        values["loyalty.enabled"] = true; values["loyalty.rupeesPerPoint"] = 150; values["loyalty.vipThreshold"] = 600;
        values["session.idleHours"] = 2; return values;
    }
    private static SettingsEditForm Form(TestDatabase t, User? actor = null, params string[] extra)
    {
        var opened = t.Settings.LoadEditForm(actor ?? TestData.Owner, Values().Keys.Concat(extra));
        Assert.True(opened.Ok, opened.Error); return opened.Value!;
    }
    private static SettingsService Other(TestDatabase t)
    {
        var db = new Database(t.Database.FilePath, pooling: false, durable: false); var audit = new AuditService(db, t.Log);
        return new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
    }
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n", t.Database.Query<string>(c,
        "SELECT key || '=' || value FROM settings ORDER BY key;")));
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs;")));
    private static void Conflict(ServiceResult result)
    { Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Null(result.SettingsEdit); Assert.Contains("changed", result.Error!); }
    private static Dictionary<string, object?> Cloud() => new()
    {
        [SyncService.EnabledKey] = true, [SyncService.UrlKey] = "https://synthetic.example",
        [SyncService.TableKey] = "shop_snapshots", [SyncService.ShopIdKey] = "synthetic-shop", [SyncService.KeyKey] = "synthetic-only-key"
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedMainDictionaryIsAtomicAndReturnsNewExactReusableReceipt(bool manager)
    {
        using var t = new TestDatabase(); var actor = manager ? TestData.AddUser(t, "manager") : TestData.Owner;
        var form = Form(t, actor); var oldName = form.GetString("store.name"); var values = Values(); var audits = Audits(t);
        var saved = t.Settings.SaveEditForm(values, actor, form); Assert.True(saved.Ok, saved.Error);
        Assert.NotSame(form, saved.SettingsEdit); Assert.Equal(oldName, form.GetString("store.name"));
        Assert.Equal("Synthetic reviewed shop", saved.SettingsEdit!.GetString("store.name"));
        Assert.Equal(80, saved.SettingsEdit.GetInt("printer.width")); Assert.False(saved.SettingsEdit.GetBool("printer.autoPrint"));
        Assert.Equal(audits + values.Count, Audits(t));
        Assert.All(AppLabelService.Definitions, d => Assert.Equal(d.DefaultText, saved.SettingsEdit.GetString(d.SettingKey)));
        Assert.True(t.Settings.SaveEditForm(values, actor, saved.SettingsEdit).Ok);
    }

    [Fact]
    public void CounterPrintWriterCannotBeOverwrittenByAnOldMainForm()
    {
        using var t = new TestDatabase(); var form = Form(t);
        Assert.True(Other(t).Set("printer.autoPrint", !form.GetBool("printer.autoPrint"), TestData.Cashier(t)).Ok);
        var state = State(t); var audits = Audits(t); Conflict(t.Settings.SaveEditForm(Values(), TestData.Owner, form));
        Assert.Equal(state, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void ConcurrentLabelRenameRefusesWholeMainDictionary()
    {
        using var t = new TestDatabase(); var form = Form(t);
        Assert.True(Other(t).Set("labels.counter", "Synthetic concurrent counter", TestData.Owner).Ok);
        var state = State(t); var audits = Audits(t); Conflict(t.Settings.SaveEditForm(Values(), TestData.Owner, form));
        Assert.Equal(state, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Theory]
    [InlineData(StartupService.ModeKey)]
    [InlineData(FocusGuardService.GuardKey)]
    public void PrimaryReceiptDoesNotBlessConcurrentUntouchedFollowupKey(string key)
    {
        using var t = new TestDatabase(); var form = Form(t, TestData.Owner, key);
        object wanted = key == StartupService.ModeKey ? "protected" : true;
        Assert.True(Other(t).Set(key, wanted, TestData.Owner).Ok);
        var primary = t.Settings.SaveEditForm(Values(), TestData.Owner, form); Assert.True(primary.Ok, primary.Error);
        Assert.Equal(form.GetRaw(key), primary.SettingsEdit!.GetRaw(key)); var audits = Audits(t);
        Conflict(t.Settings.SaveEditForm(new Dictionary<string, object?> { [key] = wanted }, TestData.Owner, primary.SettingsEdit));
        Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void RuntimeHeartbeatDoesNotConflictWithMainSnapshot()
    {
        using var t = new TestDatabase(); var form = Form(t);
        Assert.True(t.Settings.SetUiOnly("session.lastSeen", "Synthetic heartbeat").Ok);
        Assert.True(t.Settings.SaveEditForm(Values(), TestData.Owner, form).Ok);
    }

    [Fact]
    public void MissingFormCannotEnterImperativeMainWriter()
    {
        using var t = new TestDatabase(); var state = State(t); var audits = Audits(t);
        var failed = t.Settings.SaveEditForm(Values(), TestData.Owner, null); Assert.False(failed.Ok); Assert.Null(failed.SettingsEdit);
        Assert.Equal(state, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void OtherContextCannotLendMainFormEvenForSameFile()
    {
        using var t = new TestDatabase(); var loaded = Other(t).LoadEditForm(TestData.Owner, Values().Keys); Assert.True(loaded.Ok, loaded.Error);
        var state = State(t); Conflict(t.Settings.SaveEditForm(Values(), TestData.Owner, loaded.Value)); Assert.Equal(state, State(t));
    }

    [Theory]
    [InlineData("labels.sell")]
    [InlineData("session.idleHours")]
    public void EarlyOrLateAuditFaultRollsBackAllPrimaryValuesAndAudits(string key)
    {
        using var t = new TestDatabase(); var form = Form(t); var state = State(t); var audits = Audits(t);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_main_audit BEFORE INSERT ON audit_logs WHEN NEW.entity_id='" + key
            + "' BEGIN SELECT RAISE(ABORT,'synthetic main audit fault'); END;"));
        var failed = t.Settings.SaveEditForm(Values(), TestData.Owner, form); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed);
        Assert.Null(failed.SettingsEdit); Assert.Equal(state, State(t)); Assert.Equal(audits, Audits(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_main_audit;"));
        Assert.True(t.Settings.SaveEditForm(Values(), TestData.Owner, form).Ok);
    }

    [Fact]
    public void DeferredCommitFailureDoesNotOfferRetrySafeEvidence()
    {
        using var t = new TestDatabase(); var form = Form(t); var state = State(t); var audits = Audits(t);
        t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_main_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_main_commit_audit AFTER INSERT ON audit_logs
BEGIN INSERT INTO synthetic_main_commit(parent_id) VALUES(-99999); END;"));
        var failed = t.Settings.SaveEditForm(Values(), TestData.Owner, form); Assert.False(failed.Ok); Assert.False(failed.NoCommitConfirmed);
        Assert.Null(failed.SettingsEdit); Assert.Equal(state, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("inactive")]
    [InlineData("role")]
    public void PreviouslyLoadedMainFormStillRequiresCurrentWriter(string mode)
    {
        using var t = new TestDatabase(); var actor = TestData.Cashier(t);
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, actor.Id, Modules.Settings, true).Ok); var form = Form(t, actor);
        if (mode == "revoked") Assert.True(TestData.Permissions(t).Set(TestData.Owner, actor.Id, Modules.Settings, false).Ok);
        else t.Database.Tx(c => t.Database.Execute(c, mode == "inactive"
            ? "UPDATE users SET active=0 WHERE id=@id;" : "UPDATE users SET role='manager' WHERE id=@id;", new { id = actor.Id }));
        var state = State(t); var audits = Audits(t); var failed = t.Settings.SaveEditForm(Values(), actor, form);
        Assert.False(failed.Ok); Assert.Null(failed.SettingsEdit); Assert.Equal(state, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void CloudReceiptMasksCredentialAndCannotRebaseRotatedKey()
    {
        using var t = new TestDatabase(); var values = Values(); foreach (var pair in Cloud()) values[pair.Key] = pair.Value;
        var form = Form(t, TestData.Owner, Cloud().Keys.ToArray()); var saved = t.Settings.SaveEditForm(values, TestData.Owner, form);
        Assert.True(saved.Ok, saved.Error); Assert.True(saved.SettingsEdit!.HasCloudKey); Assert.Equal("null", saved.SettingsEdit.GetRaw(SyncService.KeyKey));
        Assert.DoesNotContain("synthetic-only-key", JsonSerializer.Serialize(saved));
        Assert.True(Other(t).Set(SyncService.KeyKey, "synthetic-rotated-key", TestData.Owner).Ok);
        Conflict(t.Settings.SaveEditForm(new Dictionary<string, object?> { [SyncService.EnabledKey] = false }, TestData.Owner, saved.SettingsEdit));
    }

    [Fact]
    public void SettingsWithoutCloudGrantCannotSmuggleCloudIntoMainSave()
    {
        using var t = new TestDatabase(); var actor = TestData.AddUser(t, "manager"); var form = Form(t, actor);
        var values = Values(); values[SyncService.EnabledKey] = false; var state = State(t); var audits = Audits(t);
        Assert.False(t.Settings.SaveEditForm(values, actor, form).Ok); Assert.Equal(state, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void ReceiptStaysExactAfterAnotherWriterChangesPersistedMainValue()
    {
        using var t = new TestDatabase(); var saved = t.Settings.SaveEditForm(Values(), TestData.Owner, Form(t)); Assert.True(saved.Ok, saved.Error);
        Assert.True(Other(t).Set("store.name", "Synthetic later shop", TestData.Owner).Ok);
        Assert.Equal("Synthetic reviewed shop", saved.SettingsEdit!.GetString("store.name"));
        Conflict(t.Settings.SaveEditForm(Values(), TestData.Owner, saved.SettingsEdit));
    }

    [Fact]
    public void PrimaryCommitSurvivesKnownFollowupRefusalAndClientGuardRemainsAcknowledged()
    {
        using var t = new TestDatabase(); var form = Form(t, TestData.Owner, FocusGuardService.GuardKey);
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        var saved = t.Settings.SaveEditForm(Values(), TestData.Owner, form); Assert.True(saved.Ok, saved.Error); guard.AcknowledgeCommit();
        Assert.True(Other(t).Set(FocusGuardService.GuardKey, true, TestData.Owner).Ok); var audits = Audits(t);
        Conflict(t.Settings.SaveEditForm(new Dictionary<string, object?> { [FocusGuardService.GuardKey] = false }, TestData.Owner, saved.SettingsEdit));
        Assert.True(guard.WasAcknowledged); Assert.False(guard.Ready); Assert.Equal(audits, Audits(t));
        Assert.Equal("Synthetic reviewed shop", t.Settings.GetString("store.name"));
    }

    [Fact]
    public void PrimaryCommitAndReceiptAreNotLostWhenClientCallbackThrows()
    {
        using var t = new TestDatabase(); var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        var saved = t.Settings.SaveEditForm(Values(), TestData.Owner, Form(t)); Assert.True(saved.Ok, saved.Error); guard.AcknowledgeCommit();
        Action callback = () => throw new InvalidOperationException("Synthetic client callback");
        Assert.Throws<InvalidOperationException>(callback); guard.ObserveException();
        Assert.True(guard.WasAcknowledged); Assert.False(guard.TryBegin());
        Assert.Equal("Synthetic reviewed shop", t.Settings.GetString("store.name")); Assert.Equal("Synthetic reviewed shop", saved.SettingsEdit!.GetString("store.name"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardRequiresExplicitNoCommitEvidenceNotErrorText(bool known)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        guard.ObserveFailure(new ServiceResult { Error = "SAVED. Synthetic local-cache failure", NoCommitConfirmed = known });
        Assert.Equal(known, guard.Ready); Assert.Equal(known, guard.TryBegin());
    }

    [Fact]
    public void GuardOnlyReleasesAfterExplicitCompleteHandoff()
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); guard.AcknowledgeCommit();
        Assert.False(guard.TryBegin()); guard.CompleteReset(); Assert.True(guard.TryBegin()); Assert.False(guard.WasAcknowledged);
    }

    [Fact]
    public void EnabledSessionMainWriteRejectsCopiedActor()
    {
        using var t = new TestDatabase(); t.Database.Sessions.Enable();
        var login = new AuthService(t.Database, t.Audit, t.Settings, t.Log).Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        var form = Form(t, login.Value); Assert.False(t.Settings.SaveEditForm(Values(), TestData.Owner, form).Ok);
        Assert.True(t.Settings.SaveEditForm(Values(), login.Value, form).Ok);
    }

    [Fact]
    public void MainAuditUsesCurrentStoredNameAndNeverSerializesReceipt()
    {
        using var t = new TestDatabase(); var form = Form(t);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET name='Synthetic current owner' WHERE id=1;"));
        var saved = t.Settings.SaveEditForm(Values(), TestData.Owner, form); Assert.True(saved.Ok, saved.Error);
        var names = t.Database.Read(c => t.Database.Query<string>(c, "SELECT DISTINCT user_name FROM audit_logs WHERE action='settings_change';"));
        Assert.Equal("Synthetic current owner", Assert.Single(names)); Assert.DoesNotContain("SettingsEdit", JsonSerializer.Serialize(saved));
    }
}
