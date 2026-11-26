using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Additive public-service/guard regressions, not Desktop binding or Windows execution proof.</summary>
public sealed class CounterAutoPrintEditTests
{
    private const string Key = "printer.autoPrint";
    private static Dictionary<string, object?> Values(bool value) => new() { [Key] = value };
    private static SettingsEditForm Load(TestDatabase t, User user)
    {
        var result = t.Settings.LoadEditForm(user, new[] { Key });
        Assert.True(result.Ok, result.Error); return result.Value!;
    }
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c,
        "SELECT COUNT(*) FROM audit_logs WHERE action='settings_change' AND entity_id='printer.autoPrint';")));
    private static SettingsService Other(TestDatabase t)
    {
        var db = new Database(t.Database.FilePath, pooling: false, durable: false);
        var audit = new AuditService(db, t.Log);
        return new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
    }
    private static void Refused(ServiceResult result)
    { Assert.False(result.Ok); Assert.Null(result.SettingsEdit); }
    private static void Conflict(ServiceResult result)
    { Refused(result); Assert.True(result.NoCommitConfirmed); Assert.Contains("changed", result.Error!); }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PosOnlyCheckedChoiceReturnsReusableExactReceipt(bool wanted)
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t);
        Assert.False(t.Settings.CheckAccess(user).Ok); var form = Load(t, user); var audits = Audits(t);
        var saved = t.Settings.SaveEditForm(Values(wanted), user, form);
        Assert.True(saved.Ok, saved.Error); Assert.NotNull(saved.SettingsEdit);
        Assert.Equal(wanted, saved.SettingsEdit!.GetBool(Key)); Assert.Equal(wanted, t.Settings.GetBool(Key));
        Assert.Equal(audits + 1, Audits(t));
        var next = t.Settings.SaveEditForm(Values(!wanted), user, saved.SettingsEdit);
        Assert.True(next.Ok, next.Error); Assert.Equal(!wanted, next.SettingsEdit!.GetBool(Key));
    }

    [Fact]
    public void SeparateWriterInvalidatesDisplayedBaselineWithoutUpdatingItsCache()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var form = Load(t, user);
        var old = form.GetBool(Key); Assert.True(Other(t).Set(Key, !old, TestData.Owner).Ok);
        var audits = Audits(t); Assert.Equal(old, t.Settings.GetBool(Key));
        Conflict(t.Settings.SaveEditForm(Values(old), user, form)); Assert.Equal(audits, Audits(t));
        Assert.Equal(!old, Load(t, user).GetBool(Key)); Assert.Equal(old, form.GetBool(Key));
    }

    [Fact]
    public void SameDesiredValueDoesNotMakeStaleFormAValidNoOp()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var form = Load(t, user);
        var wanted = !form.GetBool(Key); Assert.True(Other(t).Set(Key, wanted, TestData.Owner).Ok);
        var audits = Audits(t); Conflict(t.Settings.SaveEditForm(Values(wanted), user, form)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void FreshReadUsesDatabaseNotStaleSettingsCache()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var old = t.Settings.GetBool(Key);
        Assert.True(Other(t).Set(Key, !old, TestData.Owner).Ok);
        Assert.Equal(!old, Load(t, user).GetBool(Key)); Assert.Equal(old, t.Settings.GetBool(Key));
    }

    [Fact]
    public void MissingLoadedFormCannotFallBackToImperativeWrite()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var audits = Audits(t); var raw = t.Settings.GetRaw(Key);
        Refused(t.Settings.SaveEditForm(Values(false), user, null));
        Assert.Equal(raw, t.Settings.GetRaw(Key)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void AnotherDatabaseContextCannotLendItsPrintForm()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t);
        var form = Other(t).LoadEditForm(user, new[] { Key }); Assert.True(form.Ok, form.Error);
        var audits = Audits(t); Conflict(t.Settings.SaveEditForm(Values(false), user, form.Value)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void PosOnlyCannotAddShopSettingToCheckedPrintSave()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var form = Load(t, user);
        var before = t.Settings.GetRaw(Key); var name = t.Settings.GetRaw("store.name"); var audits = Audits(t);
        var values = Values(false); values["store.name"] = "Synthetic forbidden shop";
        Refused(t.Settings.SaveEditForm(values, user, form));
        Assert.Equal(before, t.Settings.GetRaw(Key)); Assert.Equal(name, t.Settings.GetRaw("store.name")); Assert.Equal(audits, Audits(t));
    }

    [Theory]
    [InlineData("revoke")]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("changed-role")]
    public void CurrentStoredAccessIsRequiredAfterPrintFormWasLoaded(string change)
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var form = Load(t, user);
        if (change == "revoke") Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, Modules.Pos, false).Ok);
        else t.Database.Tx(c => t.Database.Execute(c, change switch
        {
            "inactive" => "UPDATE users SET active=0 WHERE id=@id;",
            "deleted" => "DELETE FROM users WHERE id=@id;",
            _ => "UPDATE users SET role='manager' WHERE id=@id;"
        }, new { id = user.Id }));
        var raw = t.Settings.GetRaw(Key); var audits = Audits(t);
        Refused(t.Settings.SaveEditForm(Values(false), user, form));
        Assert.False(t.Settings.LoadEditForm(user, new[] { Key }).Ok);
        Assert.Equal(raw, t.Settings.GetRaw(Key)); Assert.Equal(audits, Audits(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingDefaultAndInsertedSameDefaultAreDifferentBaselines(bool insert)
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t);
        t.Database.Tx(c => t.Database.Execute(c, "DELETE FROM settings WHERE key='printer.autoPrint';"));
        var form = Load(t, user); Assert.True(form.GetBool(Key));
        if (insert) Assert.True(Other(t).Set(Key, true, TestData.Owner).Ok);
        var audits = Audits(t); var saved = t.Settings.SaveEditForm(Values(false), user, form);
        if (insert) { Conflict(saved); Assert.Equal(audits, Audits(t)); }
        else { Assert.True(saved.Ok, saved.Error); Assert.False(saved.SettingsEdit!.GetBool(Key)); }
    }

    [Fact]
    public void TimestampOnlyMovementDoesNotInvalidatePrintChoice()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var form = Load(t, user);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET updated_at='2099-01-01' WHERE key='printer.autoPrint';"));
        Assert.True(t.Settings.SaveEditForm(Values(false), user, form).Ok);
    }

    [Theory]
    [InlineData("audit")]
    [InlineData("setting")]
    public void WriteFaultRollsBackChoiceAndAuditAndDoesNotReturnReceipt(string target)
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var form = Load(t, user);
        var raw = t.Settings.GetRaw(Key); var audits = Audits(t);
        t.Database.Tx(c => t.Database.Execute(c, target == "audit"
            ? "CREATE TRIGGER synthetic_print_fault BEFORE INSERT ON audit_logs WHEN NEW.action='settings_change' BEGIN SELECT RAISE(ABORT,'synthetic print audit fault'); END;"
            : "CREATE TRIGGER synthetic_print_fault BEFORE INSERT ON settings WHEN NEW.key='printer.autoPrint' BEGIN SELECT RAISE(ABORT,'synthetic print setting fault'); END;"));
        var failed = t.Settings.SaveEditForm(Values(!form.GetBool(Key)), user, form);
        Refused(failed); Assert.True(failed.NoCommitConfirmed); Assert.Equal(raw, t.Settings.GetRaw(Key));
        Assert.Equal(form.GetBool(Key), Load(t, user).GetBool(Key)); Assert.Equal(audits, Audits(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_print_fault;"));
        Assert.True(t.Settings.SaveEditForm(Values(!form.GetBool(Key)), user, form).Ok);
    }

    [Fact]
    public void ReceiptStaysExactWhenAnotherWriterImmediatelyChangesChoice()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var form = Load(t, user);
        var saved = t.Settings.SaveEditForm(Values(false), user, form); Assert.True(saved.Ok, saved.Error);
        Assert.True(Other(t).Set(Key, true, TestData.Owner).Ok);
        Assert.False(saved.SettingsEdit!.GetBool(Key)); var audits = Audits(t);
        Conflict(t.Settings.SaveEditForm(Values(true), user, saved.SettingsEdit)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void UnrelatedSettingsChangeDoesNotRebaseOrInvalidateSinglePrintKey()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var form = Load(t, user);
        Assert.True(Other(t).Set("store.name", "Synthetic unrelated shop", TestData.Owner).Ok);
        Assert.True(t.Settings.SaveEditForm(Values(false), user, form).Ok);
    }

    [Fact]
    public void FailedPermissionReadDoesNotInventPosAccess()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var form = Load(t, user);
        t.Database.Tx(c => t.Database.Execute(c, "DROP TABLE user_permissions;")); var audits = Audits(t);
        Refused(t.Settings.SaveEditForm(Values(false), user, form)); Assert.Equal(audits, Audits(t));
        Assert.False(t.Settings.LoadEditForm(user, new[] { Key }).Ok);
    }

    [Fact]
    public void EnabledSessionRejectsCopiedActorButAcceptsAuthenticatedPrintWriter()
    {
        using var t = new TestDatabase(); t.Database.Sessions.Enable();
        var login = new AuthService(t.Database, t.Audit, t.Settings, t.Log).Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        var opened = t.Settings.LoadEditForm(login.Value, new[] { Key }); Assert.True(opened.Ok, opened.Error);
        Refused(t.Settings.SaveEditForm(Values(false), TestData.Owner, opened.Value));
        Assert.True(t.Settings.SaveEditForm(Values(false), login.Value, opened.Value).Ok);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardUsesOutcomeEvidenceNotFailureWording(bool noCommit)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        guard.ObserveFailure(new ServiceResult { Error = "SAVED. synthetic cache issue", NoCommitConfirmed = noCommit });
        Assert.Equal(noCommit, guard.Ready); Assert.Equal(noCommit, guard.TryBegin());
    }

    [Fact]
    public void AcknowledgedHandoffExceptionCannotBeRetriedOrTurnedIntoNoCommit()
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); guard.AcknowledgeCommit(); guard.ObserveException();
        Assert.True(guard.WasAcknowledged); Assert.False(guard.Ready); Assert.False(guard.TryBegin());
        Assert.Equal(PostingAttemptState.CommittedNeedsReset, guard.State);
    }
}
