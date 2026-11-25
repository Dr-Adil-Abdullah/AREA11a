using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Core visibility cases on isolated DBs;synthetic shared-guard cases are not VM proof.</summary>
public sealed class CustomDefinitionActiveOutcomeTests
{
    private sealed class CustomActiveFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public CustomFieldService S { get; }
        public int Id { get; }
        public int Other { get; }
        public CustomActiveFixture()
        {
            S = new CustomFieldService(T.Database, T.Audit, TestData.Permissions(T), T.Log);
            var a = S.Add(TestData.Owner, "customer", "Synthetic visible question", "text", null, false);
            var b = S.Add(TestData.Owner, "customer", "Synthetic other question", "text", null, false);
            Assert.True(a.Ok, a.Error); Assert.True(b.Ok, b.Error); Id = a.Value!.Id; Other = b.Value!.Id;
        }
        public List<CustomField> Rows() => S.List(TestData.Owner, "customer", true, true).Value!;
        public CustomField Loaded() => Rows().Single(f => f.Id == Id);
        public ServiceResult Set(bool active, EditVersion? token) => S.SetActiveChecked(TestData.Owner, Id, active, token);
        public void Sql(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Audits() => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs WHERE action IN ('custom_field_on','custom_field_off');")));
        public void Dispose() => T.Dispose();
    }
    private static void NoCommit(ServiceResult r)
    { Assert.False(r.Ok); Assert.True(r.NoCommitConfirmed); Assert.Null(r.CustomDefinitionEdit); Assert.Null(r.EditVersion); }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothDirectionsReturnExactWholeEntityWithHiddenRowsAndRowSpecificTokens(bool active)
    {
        using var f = new CustomActiveFixture(); if (active) Assert.True(f.S.SetActive(TestData.Owner, f.Id, false).Ok);
        Assert.True(f.S.SetActive(TestData.Owner, f.Other, false).Ok); var loaded = f.Loaded(); var audits = f.Audits();
        var r = f.Set(active, loaded.EditVersion); Assert.True(r.Ok, r.Error); var form = r.CustomDefinitionEdit!;
        Assert.Equal(f.Id, form.FieldId); Assert.Equal("customer", form.Entity); Assert.Equal(2, form.Fields.Count);
        Assert.Equal(active, form.Fields.Single(x => x.Id == f.Id).Active); Assert.False(form.Fields.Single(x => x.Id == f.Other).Active);
        Assert.All(form.Fields, x => Assert.NotNull(x.EditVersion)); Assert.Same(r.EditVersion, form.EditVersion); Assert.Equal(audits + 1, f.Audits());
        var next = f.S.UpdateChecked(TestData.Owner, f.Other, "Synthetic next edit", "text", null, false, "", form.Fields.Single(x => x.Id == f.Other).EditVersion);
        Assert.True(next.Ok, next.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoOpReturnsReceiptButDoesNotInventAnAudit(bool active)
    {
        using var f = new CustomActiveFixture(); if (!active) Assert.True(f.S.SetActive(TestData.Owner, f.Id, false).Ok);
        var loaded = f.Loaded(); var audits = f.Audits(); var r = f.Set(active, loaded.EditVersion);
        Assert.True(r.Ok, r.Error); Assert.Equal(active, r.CustomDefinitionEdit!.Fields.Single(x => x.Id == f.Id).Active); Assert.Equal(audits, f.Audits());
    }

    [Theory]
    [InlineData("label")]
    [InlineData("sort")]
    [InlineData("active")]
    [InlineData("required")]
    [InlineData("help_text")]
    public void ASecondConnectionChangingAnyPeerInvalidatesTheOriginalEntityToken(string column)
    {
        using var f = new CustomActiveFixture(); var loaded = f.Loaded();
        var db = new Database(f.T.Database.FilePath, pooling: false, durable: false);
        var value = column is "label" or "help_text" ? "'Synthetic external'" : column == "active" ? "0" : column == "required" ? "1" : "99";
        db.Tx(c => { db.Execute(c, $"UPDATE custom_fields SET {column}={value} WHERE id=@id;", new { id = f.Other }); });
        var audits = f.Audits(); NoCommit(f.Set(false, loaded.EditVersion)); Assert.True(f.Loaded().Active); Assert.Equal(audits, f.Audits());
    }

    [Fact]
    public void AWriterAlreadyAchievingTheDesiredValueDoesNotBypassTheStaleToken()
    {
        using var f = new CustomActiveFixture(); var loaded = f.Loaded(); Assert.True(f.S.SetActive(TestData.Owner, f.Id, false).Ok);
        var audits = f.Audits(); NoCommit(f.Set(false, loaded.EditVersion)); Assert.Equal(audits, f.Audits());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckedDoorRejectsNullBaselineInLegacyAndInteractiveHosts(bool interactive)
    {
        using var f = new CustomActiveFixture(); if (interactive) f.T.Database.Sessions.Enable(); NoCommit(f.Set(false, null));
    }

    [Fact]
    public void ForeignTargetOrDatabaseBaselineCannotAuthorizeVisibility()
    {
        using var f = new CustomActiveFixture(); NoCommit(f.Set(false, f.Rows().Single(x => x.Id == f.Other).EditVersion));
        var db = new Database(f.T.Database.FilePath, pooling: false, durable: false); var audit = new AuditService(db, f.T.Log);
        var s = new CustomFieldService(db, audit, new PermissionService(db, f.T.Log, audit), f.T.Log);
        NoCommit(s.SetActiveChecked(TestData.Owner, f.Id, false, f.Loaded().EditVersion)); Assert.True(f.Loaded().Active);
    }

    [Fact]
    public void CurrentOwnerSessionIsRequiredForNoOpTooAndAuditUsesStoredName()
    {
        using var f = new CustomActiveFixture(); var loaded = f.Loaded(); f.T.Database.Sessions.Enable(); NoCommit(f.Set(true, loaded.EditVersion));
        var auth = new AuthService(f.T.Database, f.T.Audit, f.T.Settings, f.T.Log); var login = auth.Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        f.Sql("UPDATE users SET name='Synthetic current owner' WHERE id=1;");
        var r = f.S.SetActiveChecked(login.Value!, f.Id, false, loaded.EditVersion); Assert.True(r.Ok, r.Error);
        Assert.Equal("Synthetic current owner", f.T.Database.Read(c => f.T.Database.QuerySingle<string>(c, "SELECT user_name FROM audit_logs WHERE action='custom_field_off' ORDER BY id DESC LIMIT 1;")));
        f.Sql("UPDATE users SET role='manager' WHERE id=1;");
        NoCommit(f.S.SetActiveChecked(login.Value!, f.Id, false, r.EditVersion));
    }

    [Fact]
    public void OnOffPreservesOldAnswersStableKeyAndOtherDefinitionValues()
    {
        using var f = new CustomActiveFixture(); var customer = TestData.AddCustomer(f.T);
        Assert.True(f.S.SaveValues(TestData.Owner, "customer", customer, new Dictionary<int, string?> { [f.Id] = "Synthetic historical answer" }).Ok);
        var loaded = f.Loaded(); var peer = f.Rows().Single(x => x.Id == f.Other); var off = f.Set(false, loaded.EditVersion); Assert.True(off.Ok, off.Error);
        var on = f.Set(true, off.EditVersion); Assert.True(on.Ok, on.Error); var after = on.CustomDefinitionEdit!.Fields.Single(x => x.Id == f.Id);
        Assert.Equal(loaded.Key, after.Key); Assert.Equal(loaded.Label, after.Label); Assert.Equal(loaded.Type, after.Type); Assert.Equal(loaded.HelpText, after.HelpText); Assert.Equal(loaded.Sort, after.Sort);
        Assert.True(CustomFieldDefinitionState.Matches(peer, on.CustomDefinitionEdit.Fields.Single(x => x.Id == f.Other)));
        Assert.Equal("Synthetic historical answer", f.T.Database.Read(c => f.T.Database.QuerySingle<string>(c, "SELECT value FROM custom_values WHERE field_id=@id;", new { id = f.Id })));
    }

    [Theory]
    [InlineData("update")]
    [InlineData("audit")]
    public void StatementFaultIsKnownRollbackAndAllowsExplicitRetry(string stage)
    {
        using var f = new CustomActiveFixture(); var loaded = f.Loaded(); var audits = f.Audits();
        f.Sql(stage == "update" ? "CREATE TRIGGER synthetic_active_fault BEFORE UPDATE ON custom_fields BEGIN SELECT RAISE(ABORT,'synthetic'); END;"
            : "CREATE TRIGGER synthetic_active_fault BEFORE INSERT ON audit_logs WHEN NEW.action='custom_field_off' BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        NoCommit(f.Set(false, loaded.EditVersion)); Assert.True(f.Loaded().Active); Assert.Equal(audits, f.Audits());
        f.Sql("DROP TRIGGER synthetic_active_fault;"); Assert.True(f.Set(false, loaded.EditVersion).Ok);
    }

    [Fact]
    public void DeferredCommitFaultIsUnknownAndDoesNotReturnAReceipt()
    {
        using var f = new CustomActiveFixture(); var loaded = f.Loaded(); var audits = f.Audits();
        f.Sql(@"CREATE TABLE synthetic_active_commit(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_active_commit_audit AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_off' BEGIN INSERT INTO synthetic_active_commit VALUES(-99999); END;");
        var r = f.Set(false, loaded.EditVersion); Assert.False(r.Ok); Assert.False(r.NoCommitConfirmed); Assert.Null(r.CustomDefinitionEdit); Assert.Null(r.EditVersion);
        Assert.Contains("unconfirmed", r.Error!); Assert.True(f.Loaded().Active); Assert.Equal(audits, f.Audits());
    }

    [Fact]
    public void ReceiptIsDefensiveNotSerializedAndDoesNotAdoptLaterWriter()
    {
        using var f = new CustomActiveFixture(); var r = f.Set(false, f.Loaded().EditVersion); Assert.True(r.Ok, r.Error); var form = r.CustomDefinitionEdit!;
        form.Fields.Single(x => x.Id == f.Id).Active = true; Assert.False(form.Fields.Single(x => x.Id == f.Id).Active);
        Assert.DoesNotContain("CustomDefinitionEdit", JsonSerializer.Serialize(r));
        Assert.True(f.S.Update(TestData.Owner, f.Other, "Synthetic later peer", "text", null, false).Ok);
        NoCommit(f.Set(true, r.EditVersion)); Assert.Equal("Synthetic other question", form.Fields.Single(x => x.Id == f.Other).Label);
    }

    [Fact]
    public void LegacySetActiveStillAllowsNullBaselineInSessionsDisabledHost()
    {
        using var f = new CustomActiveFixture(); var r = f.S.SetActive(TestData.Owner, f.Id, false); Assert.True(r.Ok, r.Error); Assert.NotNull(r.CustomDefinitionEdit);
    }

    [Fact]
    public void DeletedTargetIsNotSuccessfulVisibilityOrNoOp()
    {
        using var f = new CustomActiveFixture(); var loaded = f.Loaded(); var audits = f.Audits(); f.Sql($"DELETE FROM custom_fields WHERE id={f.Id};");
        NoCommit(f.Set(false, loaded.EditVersion)); Assert.Equal(audits, f.Audits());
    }

    [Theory]
    [InlineData("active-unknown")]
    [InlineData("active-ack-callback")]
    [InlineData("update-unknown")]
    public void SyntheticSharedGuardBlocksEitherCommandAfterEitherUncertainWrite(string mode)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        if (mode == "active-ack-callback") guard.AcknowledgeCommit(); else guard.ObserveFailure(ServiceResult.Fail("synthetic unknown"));
        guard.ObserveException(); Assert.False(guard.Ready); Assert.False(guard.TryBegin()); Assert.Equal(mode == "active-ack-callback", guard.WasAcknowledged);
        // Shared existing guard only;actual Desktop commands/timer/regrant are NOT executed here.
    }
}
