using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Additive checked-reset public service cases. No claim of .NET, WPF callback or Windows execution.</summary>
public sealed class AppLabelResetOutcomeTests
{
    private static AppLabelService Service(TestDatabase t) => new(t.Settings, TestData.Permissions(t));
    private static string[] Keys => AppLabelService.Definitions.Select(d => d.SettingKey).ToArray();
    private static SettingsEditForm Form(TestDatabase t, User? actor = null, params string[] extra)
    {
        var loaded = t.Settings.LoadEditForm(actor ?? TestData.Owner, Keys.Concat(extra));
        Assert.True(loaded.Ok, loaded.Error); return loaded.Value!;
    }
    private static SettingsService Other(TestDatabase t)
    {
        var db = new Database(t.Database.FilePath, pooling: false, durable: false); var audit = new AuditService(db, t.Log);
        return new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
    }
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c,
        "SELECT COUNT(*) FROM audit_logs WHERE action='settings_change' AND entity_id LIKE 'labels.%';")));
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n", t.Database.Query<string>(c,
        "SELECT key || '=' || value FROM settings ORDER BY key;")));
    private static void Conflict(ServiceResult result)
    { Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Null(result.SettingsEdit); Assert.Contains("changed", result.Error!); }
    private static void Defaults(SettingsEditForm form) => Assert.All(AppLabelService.Definitions,
        definition => Assert.Equal(definition.DefaultText, form.GetString(definition.SettingKey)));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentOwnerOrDelegatedSettingsWriterRestoresAllAndGetsExactReceipt(bool delegated)
    {
        using var t = new TestDatabase(); var actor = delegated ? TestData.Cashier(t) : TestData.Owner;
        if (delegated) Assert.True(TestData.Permissions(t).Set(TestData.Owner, actor.Id, Modules.Settings, true).Ok);
        Assert.True(Service(t).Save(new Dictionary<string, string?> { ["counter"] = "Synthetic renamed counter" }, actor).Ok);
        var before = Audits(t); var original = Form(t, actor);
        var saved = Service(t).ResetChecked(actor, original); Assert.True(saved.Ok, saved.Error);
        Defaults(saved.SettingsEdit!); Assert.Equal(before + Keys.Length, Audits(t));
        Assert.Equal("Synthetic renamed counter", original.GetString("labels.counter"));
        Assert.True(Service(t).ResetChecked(actor, saved.SettingsEdit).Ok);
    }

    [Fact]
    public void NullFormNeverDegradesToBlindCompatibilityReset()
    {
        using var t = new TestDatabase(); var before = State(t); var audits = Audits(t);
        var refused = Service(t).ResetChecked(TestData.Owner, null);
        Assert.False(refused.Ok); Assert.True(refused.NoCommitConfirmed); Assert.Null(refused.SettingsEdit);
        Assert.Equal(before, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void PartialLabelFormCannotResetTheEntireCatalog()
    {
        using var t = new TestDatabase(); var loaded = t.Settings.LoadEditForm(TestData.Owner, Keys.Take(1)); Assert.True(loaded.Ok, loaded.Error);
        var before = State(t); var audits = Audits(t); Conflict(Service(t).ResetChecked(TestData.Owner, loaded.Value));
        Assert.Equal(before, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void FormFromAnotherDatabaseContextCannotLendItsBaseline()
    {
        using var t = new TestDatabase(); var loaded = Other(t).LoadEditForm(TestData.Owner, Keys); Assert.True(loaded.Ok, loaded.Error);
        var before = State(t); Conflict(Service(t).ResetChecked(TestData.Owner, loaded.Value)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void OneConcurrentLabelRefusesAllThirtyTwoAndAllAuditRows()
    {
        using var t = new TestDatabase(); var form = Form(t);
        Assert.True(Other(t).Set(Keys[^1], "Synthetic concurrent word", TestData.Owner).Ok);
        var before = State(t); var audits = Audits(t); Conflict(Service(t).ResetChecked(TestData.Owner, form));
        Assert.Equal(before, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void FreshFormReadsDatabaseEvenWhenSharedCacheStillHasOldWords()
    {
        using var t = new TestDatabase(); var original = t.Settings.GetString("labels.counter");
        Assert.True(Other(t).Set("labels.counter", "Synthetic fresh word", TestData.Owner).Ok);
        Assert.Equal(original, t.Settings.GetString("labels.counter")); Assert.Equal("Synthetic fresh word", Form(t).GetString("labels.counter"));
        var saved = Service(t).ResetChecked(TestData.Owner, Form(t)); Assert.True(saved.Ok, saved.Error); Defaults(saved.SettingsEdit!);
    }

    [Fact]
    public void UnrelatedSettingsAreNotWrittenByReset()
    {
        using var t = new TestDatabase(); var form = Form(t); Assert.True(Other(t).Set("store.name", "Synthetic changed shop", TestData.Owner).Ok);
        var saved = Service(t).ResetChecked(TestData.Owner, form); Assert.True(saved.Ok, saved.Error);
        Assert.Equal("Synthetic changed shop", t.Settings.LoadEditForm(TestData.Owner, new[] { "store.name" }).Value!.GetString("store.name"));
    }

    [Fact]
    public void PartialReceiptDoesNotBlessAStaleUntouchedMainSetting()
    {
        using var t = new TestDatabase(); var form = Form(t, TestData.Owner, "store.name"); var old = form.GetString("store.name");
        Assert.True(Other(t).Set("store.name", "Synthetic other shop", TestData.Owner).Ok);
        var saved = Service(t).ResetChecked(TestData.Owner, form); Assert.True(saved.Ok, saved.Error);
        Assert.Equal(old, saved.SettingsEdit!.GetString("store.name"));
        Conflict(t.Settings.SaveEditForm(new Dictionary<string, object?> { ["store.name"] = "Synthetic stale overwrite" }, TestData.Owner, saved.SettingsEdit));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingDefaultPresenceIsPartOfLoadedResetBaseline(bool insert)
    {
        using var t = new TestDatabase(); var first = AppLabelService.Definitions[0];
        t.Database.Tx(c => t.Database.Execute(c, "DELETE FROM settings WHERE key=@key;", new { key = first.SettingKey }));
        var form = Form(t); Assert.Equal(first.DefaultText, form.GetString(first.SettingKey));
        if (insert) Assert.True(Other(t).Set(first.SettingKey, first.DefaultText, TestData.Owner).Ok);
        var saved = Service(t).ResetChecked(TestData.Owner, form);
        if (insert) Conflict(saved); else { Assert.True(saved.Ok, saved.Error); Defaults(saved.SettingsEdit!); }
    }

    [Fact]
    public void TimestampOnlyChurnDoesNotPreventValueCheckedReset()
    {
        using var t = new TestDatabase(); var form = Form(t);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET updated_at='2099-01-01' WHERE key LIKE 'labels.%';"));
        Assert.True(Service(t).ResetChecked(TestData.Owner, form).Ok);
    }

    [Fact]
    public void RawWhitespaceChangeCannotHideBehindSameDisplayedWord()
    {
        using var t = new TestDatabase(); var form = Form(t);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value=@value WHERE key='labels.counter';", new { value = "\" Counter (POS) \"" }));
        var before = State(t); Conflict(Service(t).ResetChecked(TestData.Owner, form)); Assert.Equal(before, State(t));
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("cashier")]
    [InlineData("revoked")]
    [InlineData("inactive")]
    [InlineData("role")]
    public void CurrentAuthorizationIsRequiredAtCheckedWrite(string mode)
    {
        using var t = new TestDatabase(); User? actor = mode == "anonymous" ? null : TestData.Cashier(t);
        if (actor is not null && mode != "cashier") Assert.True(TestData.Permissions(t).Set(TestData.Owner, actor.Id, Modules.Settings, true).Ok);
        var form = Form(t);
        if (mode == "revoked") Assert.True(TestData.Permissions(t).Set(TestData.Owner, actor!.Id, Modules.Settings, false).Ok);
        if (mode is "inactive" or "role") t.Database.Tx(c => t.Database.Execute(c, mode == "inactive"
            ? "UPDATE users SET active=0 WHERE id=@id;" : "UPDATE users SET role='manager' WHERE id=@id;", new { id = actor!.Id }));
        var before = State(t); var audits = Audits(t); var failed = Service(t).ResetChecked(actor, form);
        Assert.False(failed.Ok); Assert.Equal(mode is "inactive" or "role", failed.NoCommitConfirmed); Assert.Null(failed.SettingsEdit);
        Assert.Equal(before, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void FailedGrantReadDoesNotInventSettingsPermissionOrNoCommitEvidence()
    {
        using var t = new TestDatabase(); var actor = TestData.Cashier(t);
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, actor.Id, Modules.Settings, true).Ok);
        var form = Form(t, actor); t.Database.Tx(c => t.Database.Execute(c, "DROP TABLE user_permissions;"));
        var before = State(t); var audits = Audits(t); var failed = Service(t).ResetChecked(actor, form);
        Assert.False(failed.Ok); Assert.False(failed.NoCommitConfirmed); Assert.Null(failed.SettingsEdit);
        Assert.Equal(before, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Theory]
    [InlineData("labels.sell")]
    [InlineData("labels.alerts")]
    public void FirstOrLastAuditFaultRollsBackEntireResetAndOriginalBaselineCanRetry(string faultKey)
    {
        using var t = new TestDatabase(); Assert.True(Service(t).Save(new Dictionary<string, string?> { ["counter"] = "Synthetic old" }, TestData.Owner).Ok);
        var form = Form(t); var before = State(t); var audits = Audits(t);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_word_audit BEFORE INSERT ON audit_logs WHEN NEW.entity_id='" + faultKey
            + "' BEGIN SELECT RAISE(ABORT,'synthetic word audit fault'); END;"));
        var failed = Service(t).ResetChecked(TestData.Owner, form); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Null(failed.SettingsEdit);
        Assert.Equal(before, State(t)); Assert.Equal(audits, Audits(t)); Assert.Equal("Synthetic old", t.Settings.GetString("labels.counter"));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_word_audit;"));
        Assert.True(Service(t).ResetChecked(TestData.Owner, form).Ok);
    }

    [Fact]
    public void SettingFaultAlsoRollsBackAllValuesAndAudits()
    {
        using var t = new TestDatabase(); var form = Form(t); var before = State(t); var audits = Audits(t);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_word_setting BEFORE INSERT ON settings WHEN NEW.key='labels.counter' BEGIN SELECT RAISE(ABORT,'synthetic word setting fault'); END;"));
        var failed = Service(t).ResetChecked(TestData.Owner, form); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed);
        Assert.Equal(before, State(t)); Assert.Equal(audits, Audits(t)); Assert.Null(failed.SettingsEdit);
    }

    [Fact]
    public void DeferredCommitFaultIsNotRetrySafeEvenWhenConnectionLaterRolledBack()
    {
        using var t = new TestDatabase(); var form = Form(t); var before = State(t); var audits = Audits(t);
        t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_word_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_word_commit_audit AFTER INSERT ON audit_logs WHEN NEW.entity_id LIKE 'labels.%'
BEGIN INSERT INTO synthetic_word_commit(parent_id) VALUES(-99999); END;"));
        var failed = Service(t).ResetChecked(TestData.Owner, form); Assert.False(failed.Ok); Assert.False(failed.NoCommitConfirmed);
        Assert.Null(failed.SettingsEdit); Assert.Contains("unconfirmed", failed.Error!); Assert.Equal(before, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void EnabledSessionAcceptsAuthenticatedWriterNotCopiedOwner()
    {
        using var t = new TestDatabase(); t.Database.Sessions.Enable();
        var login = new AuthService(t.Database, t.Audit, t.Settings, t.Log).Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        var form = Form(t, login.Value); Assert.False(Service(t).ResetChecked(TestData.Owner, form).Ok);
        var saved = Service(t).ResetChecked(login.Value, form); Assert.True(saved.Ok, saved.Error); Defaults(saved.SettingsEdit!);
    }

    [Fact]
    public void ReceiptRemainsExactAfterConcurrentRenameAndCannotReplayItAway()
    {
        using var t = new TestDatabase(); var saved = Service(t).ResetChecked(TestData.Owner, Form(t)); Assert.True(saved.Ok, saved.Error);
        Assert.True(Other(t).Set("labels.counter", "Synthetic next name", TestData.Owner).Ok); Defaults(saved.SettingsEdit!);
        var audits = Audits(t); Conflict(Service(t).ResetChecked(TestData.Owner, saved.SettingsEdit)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void StoredCurrentWriterNameIsUsedInResetAudit()
    {
        using var t = new TestDatabase(); var form = Form(t);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET name='Synthetic current owner' WHERE id=1;"));
        Assert.True(Service(t).ResetChecked(TestData.Owner, form).Ok);
        var names = t.Database.Read(c => t.Database.Query<string>(c, "SELECT DISTINCT user_name FROM audit_logs WHERE action='settings_change' AND entity_id LIKE 'labels.%';"));
        Assert.Equal("Synthetic current owner", Assert.Single(names));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalResetGuardUsesExplicitOutcomeEvidenceNotWords(bool noCommit)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        guard.ObserveFailure(new ServiceResult { Error = "SAVED. Synthetic refresh fault", NoCommitConfirmed = noCommit });
        Assert.Equal(noCommit, guard.Ready); Assert.Equal(noCommit, guard.TryBegin());
    }

    [Fact]
    public void AcknowledgedCallbackFailureRetainsResetGuardUntilCompleteHandoff()
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); guard.AcknowledgeCommit(); guard.ObserveException();
        Assert.True(guard.WasAcknowledged); Assert.False(guard.TryBegin()); Assert.False(guard.Ready);
    }

    [Fact]
    public void LabelResetReceiptIsNotSerializedInServiceResult()
    {
        using var t = new TestDatabase(); var saved = Service(t).ResetChecked(TestData.Owner, Form(t)); Assert.True(saved.Ok, saved.Error);
        Assert.DoesNotContain("SettingsEdit", JsonSerializer.Serialize(saved)); Assert.DoesNotContain("Baselines", JsonSerializer.Serialize(saved));
    }
}
