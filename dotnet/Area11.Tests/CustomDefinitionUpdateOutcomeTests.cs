using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated Core tests and separately labelled synthetic guard cases;not WPF proof.</summary>
public sealed class CustomDefinitionUpdateOutcomeTests
{
    private sealed class CustomUpdateFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public CustomFieldService S { get; }
        public int Id { get; }
        public int Other { get; }
        public CustomUpdateFixture()
        {
            S = new CustomFieldService(T.Database, T.Audit, TestData.Permissions(T), T.Log);
            var a = S.Add(TestData.Owner, "customer", "Synthetic first", "text", null, false);
            var b = S.Add(TestData.Owner, "customer", "Synthetic second", "text", null, false);
            Assert.True(a.Ok, a.Error); Assert.True(b.Ok, b.Error); Id = a.Value!.Id; Other = b.Value!.Id;
        }
        public List<CustomField> Rows() => S.List(TestData.Owner, "customer", true, true).Value!;
        public CustomField Loaded() => Rows().Single(f => f.Id == Id);
        public ServiceResult Save(EditVersion? token, string label = "Synthetic renamed", string type = "text", string? help = "Synthetic help") =>
            S.UpdateChecked(TestData.Owner, Id, label, type, null, false, help, token);
        public void Sql(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Audits() => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs WHERE action='custom_field_update';")));
        public string? Answers() => T.Database.Read(c => T.Database.Scalar(c, "SELECT group_concat(field_id || ':' || value,';') FROM custom_values;"))?.ToString();
        public void Dispose() => T.Dispose();
    }
    private static void NoCommit(ServiceResult result)
    { Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Null(result.CustomDefinitionEdit); Assert.Null(result.EditVersion); }

    [Fact]
    public void ExactEntityHandoffIncludesHiddenRowsAndRowSpecificTokens()
    {
        using var f = new CustomUpdateFixture(); Assert.True(f.S.SetActive(TestData.Owner, f.Other, false).Ok); var loaded = f.Loaded();
        var saved = f.Save(loaded.EditVersion); Assert.True(saved.Ok, saved.Error); var form = saved.CustomDefinitionEdit!;
        Assert.Equal("customer", form.Entity); Assert.Equal(f.Id, form.FieldId); Assert.Equal(2, form.Fields.Count);
        Assert.False(form.Fields.Single(x => x.Id == f.Other).Active); Assert.All(form.Fields, x => Assert.NotNull(x.EditVersion));
        Assert.Same(saved.EditVersion, form.EditVersion); Assert.Equal("Synthetic renamed", form.Fields.Single(x => x.Id == f.Id).Label);
        var next = f.S.UpdateChecked(TestData.Owner, f.Other, "Synthetic other edit", "text", null, false, "", form.Fields.Single(x => x.Id == f.Other).EditVersion);
        Assert.True(next.Ok, next.Error);
    }

    [Fact]
    public void ReceiptRowsAreDefensiveCopiesAndNotSerializedWithServiceResult()
    {
        using var f = new CustomUpdateFixture(); var saved = f.Save(f.Loaded().EditVersion); Assert.True(saved.Ok, saved.Error);
        var form = saved.CustomDefinitionEdit!; var row = form.Fields.Single(x => x.Id == f.Id); row.Label = "Mutated caller copy"; row.Sort = 999;
        Assert.Equal("Synthetic renamed", form.Fields.Single(x => x.Id == f.Id).Label); Assert.NotEqual(999, form.Fields.Single(x => x.Id == f.Id).Sort);
        Assert.DoesNotContain("CustomDefinitionEdit", JsonSerializer.Serialize(saved)); Assert.DoesNotContain("EditVersion", JsonSerializer.Serialize(saved));
    }

    [Theory]
    [InlineData("label")]
    [InlineData("sort")]
    [InlineData("active")]
    [InlineData("required")]
    [InlineData("help_text")]
    public void OtherDefinitionChangesInvalidateTheOpenedEntityBaseline(string column)
    {
        using var f = new CustomUpdateFixture(); var loaded = f.Loaded();
        var other = new Database(f.T.Database.FilePath, pooling: false, durable: false);
        var value = column is "label" or "help_text" ? "'Synthetic changed'" : column == "active" ? "0" : "99";
        if (column == "required") value = "1";
        other.Tx(c => { other.Execute(c, $"UPDATE custom_fields SET {column}={value} WHERE id=@id;", new { id = f.Other }); });
        var audits = f.Audits(); NoCommit(f.Save(loaded.EditVersion)); Assert.Equal("Synthetic first", f.Loaded().Label); Assert.Equal(audits, f.Audits());
    }

    [Fact]
    public void LaterWriterDoesNotChangeTheExactReceiptAndItsNextWriteConflicts()
    {
        using var f = new CustomUpdateFixture(); var saved = f.Save(f.Loaded().EditVersion); Assert.True(saved.Ok, saved.Error);
        var target = saved.CustomDefinitionEdit!.Fields.Single(x => x.Id == f.Id);
        Assert.True(f.S.Update(TestData.Owner, f.Other, "Synthetic later writer", "text", null, false).Ok);
        NoCommit(f.Save(target.EditVersion, "Synthetic third edit"));
        Assert.Equal("Synthetic second", saved.CustomDefinitionEdit.Fields.Single(x => x.Id == f.Other).Label);
    }

    [Fact]
    public void NoOpReturnsExactReceiptWithoutAnInventedAuditButStillChecksTheToken()
    {
        using var f = new CustomUpdateFixture(); var loaded = f.Loaded(); var audits = f.Audits();
        var saved = f.Save(loaded.EditVersion, loaded.Label, loaded.Type, loaded.HelpText); Assert.True(saved.Ok, saved.Error);
        Assert.NotNull(saved.CustomDefinitionEdit); Assert.Equal(audits, f.Audits());
        f.Sql($"UPDATE custom_fields SET help_text='Synthetic external' WHERE id={f.Other};");
        NoCommit(f.Save(saved.EditVersion, loaded.Label, loaded.Type, loaded.HelpText));
    }

    [Theory]
    [InlineData("x", "text")]
    [InlineData("Synthetic invalid", "select")]
    [InlineData("", "text")]
    public void InvalidDefinitionIsKnownNoCommit(string label, string type)
    {
        using var f = new CustomUpdateFixture(); var audits = f.Audits(); NoCommit(f.Save(f.Loaded().EditVersion, label, type)); Assert.Equal(audits, f.Audits());
    }

    [Fact]
    public void DuplicateLabelIsKnownRollbackAndPreservesBothDefinitions()
    {
        using var f = new CustomUpdateFixture(); var audits = f.Audits(); NoCommit(f.Save(f.Loaded().EditVersion, "Synthetic second"));
        Assert.Equal("Synthetic first", f.Loaded().Label); Assert.Equal(audits, f.Audits());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckedDoorRejectsNullTokenEvenWhenLegacySessionsAreDisabled(bool interactive)
    {
        using var f = new CustomUpdateFixture(); if (interactive) f.T.Database.Sessions.Enable(); NoCommit(f.Save(null));
    }

    [Fact]
    public void WrongTargetAndDatabaseTokensCannotAuthorizeTheEdit()
    {
        using var f = new CustomUpdateFixture(); NoCommit(f.Save(f.Rows().Single(x => x.Id == f.Other).EditVersion));
        var db = new Database(f.T.Database.FilePath, pooling: false, durable: false);
        var s = new CustomFieldService(db, new AuditService(db, f.T.Log), new PermissionService(db, f.T.Log, new AuditService(db, f.T.Log)), f.T.Log);
        NoCommit(s.UpdateChecked(TestData.Owner, f.Id, "Synthetic foreign baseline", "text", null, false, "", f.Loaded().EditVersion));
    }

    [Fact]
    public void LegacyUpdateWithoutTokenStillWorksInSessionsDisabledHost()
    {
        using var f = new CustomUpdateFixture(); var r = f.S.Update(TestData.Owner, f.Id, "Synthetic legacy", "text", null, false);
        Assert.True(r.Ok, r.Error); Assert.NotNull(r.CustomDefinitionEdit);
    }

    [Fact]
    public void ChangedKindPreservesHistoricalAnswerAndWarningAndStableKey()
    {
        using var f = new CustomUpdateFixture(); var customer = TestData.AddCustomer(f.T);
        Assert.True(f.S.SaveValues(TestData.Owner, "customer", customer, new Dictionary<int, string?> { [f.Id] = "Synthetic old text" }).Ok);
        var loaded = f.Loaded(); var answers = f.Answers(); var r = f.Save(loaded.EditVersion, "Synthetic number", "number");
        Assert.True(r.Ok, r.Error); Assert.Single(r.Warnings); Assert.Equal(answers, f.Answers());
        Assert.Equal(loaded.Key, r.CustomDefinitionEdit!.Fields.Single(x => x.Id == f.Id).Key);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("audit")]
    public void StatementFaultRollsBackAndPermitsExplicitCorrectedRetry(string stage)
    {
        using var f = new CustomUpdateFixture(); var loaded = f.Loaded(); var audits = f.Audits();
        f.Sql(stage == "update" ? "CREATE TRIGGER synthetic_custom_update BEFORE UPDATE ON custom_fields BEGIN SELECT RAISE(ABORT,'synthetic'); END;"
            : "CREATE TRIGGER synthetic_custom_update BEFORE INSERT ON audit_logs WHEN NEW.action='custom_field_update' BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        NoCommit(f.Save(loaded.EditVersion)); Assert.Equal("Synthetic first", f.Loaded().Label); Assert.Equal(audits, f.Audits());
        f.Sql("DROP TRIGGER synthetic_custom_update;"); Assert.True(f.Save(loaded.EditVersion).Ok);
    }

    [Fact]
    public void DeferredCommitFaultIsUnknownAndReturnsNoFormOrToken()
    {
        using var f = new CustomUpdateFixture(); var loaded = f.Loaded(); var audits = f.Audits();
        f.Sql(@"CREATE TABLE synthetic_custom_commit(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_custom_commit_audit AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_update' BEGIN INSERT INTO synthetic_custom_commit VALUES(-99999); END;");
        var r = f.Save(loaded.EditVersion); Assert.False(r.Ok); Assert.False(r.NoCommitConfirmed); Assert.Null(r.CustomDefinitionEdit); Assert.Null(r.EditVersion);
        Assert.Contains("unconfirmed", r.Error!); Assert.Equal(audits, f.Audits()); Assert.Equal("Synthetic first", f.Loaded().Label);
    }

    [Fact]
    public void CurrentOwnerAndInteractiveProofAreRequiredAndStoredAuditNameIsUsed()
    {
        using var f = new CustomUpdateFixture(); var token = f.Loaded().EditVersion; f.T.Database.Sessions.Enable(); NoCommit(f.Save(token));
        var auth = new AuthService(f.T.Database, f.T.Audit, f.T.Settings, f.T.Log); var login = auth.Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        f.Sql("UPDATE users SET name='Synthetic current owner' WHERE id=1;");
        var result = f.S.UpdateChecked(login.Value!, f.Id, "Synthetic authenticated", "text", null, false, "", token); Assert.True(result.Ok, result.Error);
        Assert.Equal("Synthetic current owner", f.T.Database.Read(c => f.T.Database.QuerySingle<string>(c, "SELECT user_name FROM audit_logs WHERE action='custom_field_update' ORDER BY id DESC LIMIT 1;")));
        f.Sql("UPDATE users SET active=0 WHERE id=1;");
        NoCommit(f.S.UpdateChecked(login.Value!, f.Id, "Synthetic revoked", "text", null, false, "", result.EditVersion));
    }

    [Fact]
    public void DeletedTargetCannotBeCertifiedAsASuccessfulNoOp()
    {
        using var f = new CustomUpdateFixture(); var loaded = f.Loaded(); var audits = f.Audits();
        f.Sql($"DELETE FROM custom_fields WHERE id={f.Id};");
        NoCommit(f.Save(loaded.EditVersion, loaded.Label, loaded.Type, loaded.HelpText)); Assert.Equal(audits, f.Audits());
    }

    [Fact]
    public void DocumentPhotoRestrictionIsStillEnforcedInsideTheUpdateTransaction()
    {
        using var f = new CustomUpdateFixture(); var added = f.S.Add(TestData.Owner, "sale", "Synthetic document", "text", null, false); Assert.True(added.Ok, added.Error);
        var loaded = f.S.List(TestData.Owner, "sale", true, true).Value!.Single();
        NoCommit(f.S.UpdateChecked(TestData.Owner, loaded.Id, loaded.Label, "photo", null, false, "", loaded.EditVersion));
        Assert.Equal("text", f.S.List(TestData.Owner, "sale", true, true).Value!.Single().Type);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SyntheticGuardIsNotReleasedByRefreshRegrantOrAnAcknowledgedCallbackFailure(bool acknowledged)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        if (acknowledged) guard.AcknowledgeCommit(); else guard.ObserveFailure(ServiceResult.Fail("synthetic unknown"));
        guard.ObserveException(); Assert.False(guard.Ready); Assert.False(guard.TryBegin()); Assert.Equal(acknowledged, guard.WasAcknowledged);
        // No actual VM/timer executed. These are the existing guard's state transitions only.
    }
}
