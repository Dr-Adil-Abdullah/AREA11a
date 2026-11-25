using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated Core cases only;Desktop modal/callback/provider injection still require Windows.</summary>
public sealed class NativeFieldOutcomeTests
{
    private static NativeFieldPolicyService Policy(TestDatabase t, Database? db = null)
    {
        db ??= t.Database;
        var p = new NativeFieldPolicyService(db, new AuditService(db, t.Log), t.Log);
        Assert.True(p.EnsureDefaults().Ok); return p;
    }
    private static FormFieldDefinition Field(NativeFieldPolicyService p, string key = "sale.quantity") =>
        p.List(TestData.Owner).Value!.Single(f => f.FieldKey == key);
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c,
        "SELECT COUNT(*) FROM audit_logs WHERE action='native_field_required_changed';")));
    private static void NoCommit(ServiceResult r)
    { Assert.False(r.Ok); Assert.True(r.NoCommitConfirmed); Assert.Null(r.NativeFieldEdit); Assert.Null(r.EditVersion); }

    [Fact]
    public void PreparedFieldHasNoWriteOrAuditAndRequiresLoadedBaseline()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p); var before = Audits(t);
        Assert.False(p.PrepareRequiredEdit(TestData.Owner, f.Id, null).Ok);
        var prepared = p.PrepareRequiredEdit(TestData.Owner, f.Id, f.EditVersion);
        Assert.True(prepared.Ok, prepared.Error); Assert.True(prepared.Value!.Required); Assert.NotNull(prepared.Value.EditVersion);
        Assert.Equal(before, Audits(t)); Assert.True(Field(p).Required);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckedDoorRequiresBaselineInLegacyAndInteractiveHost(bool interactive)
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p);
        if (interactive) t.Database.Sessions.Enable();
        NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, false, true, null));
        Assert.True(t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT required FROM form_field_definitions WHERE id=@id;", new { id = f.Id }))) == 1);
    }

    [Theory]
    [InlineData("required")]
    [InlineData("label")]
    [InlineData("critical")]
    [InlineData("safe_required")]
    [InlineData("help_text")]
    public void AnyPersistedMetadataChangeConflictsBeforePromptAndAtWrite(string column)
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p);
        // A critical required field can become optional only with the real owner acknowledgement.
        // Keep the original loaded baseline so both stale-write doors still have to refuse it.
        if (column == "required")
        {
            var changed = p.SetRequired(TestData.Owner, f.Id, false, true, f.EditVersion);
            Assert.True(changed.Ok, changed.Error);
            Assert.False(Field(p).Required);
            Assert.Equal(TestData.SeedOwnerName, Field(p).OverrideAcknowledgedBy);
            Assert.NotNull(Field(p).OverrideAcknowledgedAt);
        }
        else
        {
            t.Database.Tx(c => t.Database.Execute(c, $"UPDATE form_field_definitions SET {column}=" +
                (column is "label" or "help_text" ? "'Synthetic changed'" : "0") + " WHERE id=@id;", new { id = f.Id }));
        }
        var before = Audits(t);
        Assert.False(p.PrepareRequiredEdit(TestData.Owner, f.Id, f.EditVersion).Ok);
        NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion)); Assert.Equal(before, Audits(t));
    }

    [Fact]
    public void WriterDuringModalIsComparedAgainEvenForRequestedNoOp()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p);
        var prepared = p.PrepareRequiredEdit(TestData.Owner, f.Id, f.EditVersion).Value!;
        Assert.True(p.SetRequired(TestData.Owner, f.Id, false, true, f.EditVersion).Ok); var before = Audits(t);
        NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, false, true, prepared.EditVersion)); Assert.Equal(before, Audits(t));
    }

    [Fact]
    public void RiskRefusalIsKnownRollbackAndFreshConfirmationStillRequired()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p); var before = Audits(t);
        NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, false, false, f.EditVersion)); Assert.Equal(before, Audits(t));
        Assert.True(Field(p).Required);
        var saved = p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion);
        Assert.True(saved.Ok, saved.Error); Assert.False(saved.NativeFieldEdit!.Required);
        Assert.Equal(TestData.SeedOwnerName, saved.NativeFieldEdit.OverrideAcknowledgedBy);
    }

    [Fact]
    public void NoOpChecksAccessAndReturnsExactBaselineWithoutInventingAudit()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p); var before = Audits(t);
        var result = p.SetRequiredChecked(TestData.Owner, f.Id, true, false, f.EditVersion);
        Assert.True(result.Ok, result.Error); Assert.True(result.NativeFieldEdit!.Required);
        Assert.Same(result.EditVersion, result.NativeFieldEdit.EditVersion); Assert.Equal(before, Audits(t));
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"));
        NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, true, false, result.EditVersion));
    }

    [Fact]
    public void ExactSavedRowDoesNotAdoptALaterWriterAndReceiptIsNotJson()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p);
        var result = p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion); Assert.True(result.Ok, result.Error);
        Assert.Same(result.NativeFieldEdit!.EditVersion, result.EditVersion);
        Assert.DoesNotContain("NativeFieldEdit", JsonSerializer.Serialize(result));
        Assert.True(p.SetRequiredChecked(TestData.Owner, f.Id, true, false, result.EditVersion).Ok);
        Assert.False(result.NativeFieldEdit.Required);
        NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, true, false, result.EditVersion));
    }

    [Fact]
    public void AnotherDatabaseInstanceCannotSupplyTheBaseline()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p);
        var other = new Database(t.Database.FilePath, pooling: false, durable: false); var q = Policy(t, other);
        Assert.False(q.PrepareRequiredEdit(TestData.Owner, f.Id, f.EditVersion).Ok);
        NoCommit(q.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion)); Assert.True(Field(p).Required);
    }

    [Fact]
    public void DifferentFieldBaselineCannotAuthorizeTheSelectedTarget()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p); var other = Field(p, "customer.phone");
        Assert.False(p.PrepareRequiredEdit(TestData.Owner, f.Id, other.EditVersion).Ok);
        NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, false, true, other.EditVersion)); Assert.True(Field(p).Required);
    }

    [Fact]
    public void UnrelatedFieldWriterDoesNotConflictOrReplaceTheSavedTarget()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p); var other = Field(p, "customer.phone");
        Assert.True(p.SetRequiredChecked(TestData.Owner, other.Id, true, false, other.EditVersion).Ok);
        var result = p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion);
        Assert.True(result.Ok, result.Error); Assert.Equal(f.Id, result.NativeFieldEdit!.Id); Assert.False(other.Required);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("audit")]
    public void StatementFaultRollsBackPreferenceAndAuditThenAllowsExplicitRetry(string stage)
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p); var before = Audits(t);
        t.Database.Tx(c => t.Database.Execute(c, stage == "update"
            ? "CREATE TRIGGER synthetic_native_fault BEFORE UPDATE ON form_field_definitions BEGIN SELECT RAISE(ABORT,'synthetic'); END;"
            : "CREATE TRIGGER synthetic_native_fault BEFORE INSERT ON audit_logs WHEN NEW.action='native_field_required_changed' BEGIN SELECT RAISE(ABORT,'synthetic'); END;"));
        NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion));
        Assert.True(Field(p).Required); Assert.Equal(before, Audits(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_native_fault;"));
        Assert.True(p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion).Ok);
    }

    [Fact]
    public void DeferredCommitFailureIsUnknownAndReturnsNoHandoff()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p); var before = Audits(t);
        t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_native_commit(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_native_commit_audit AFTER INSERT ON audit_logs WHEN NEW.action='native_field_required_changed'
BEGIN INSERT INTO synthetic_native_commit VALUES(-99999); END;"));
        var result = p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion);
        Assert.False(result.Ok); Assert.False(result.NoCommitConfirmed); Assert.Null(result.NativeFieldEdit); Assert.Null(result.EditVersion);
        Assert.Contains("unconfirmed", result.Error!); Assert.True(Field(p).Required); Assert.Equal(before, Audits(t));
    }

    [Fact]
    public void InteractiveProofAndRevokedOwnerAreCheckedAgainAfterPreparation()
    {
        using var t = new TestDatabase(); var p = Policy(t); t.Database.Sessions.Enable();
        var auth = new AuthService(t.Database, t.Audit, t.Settings, t.Log);
        var login = auth.Login("admin", "area11"); Assert.True(login.Ok, login.Error); var owner = login.Value!;
        var f = p.List(owner).Value!.Single(x => x.FieldKey == "sale.quantity");
        Assert.True(p.PrepareRequiredEdit(owner, f.Id, f.EditVersion).Ok);
        NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion));
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET role='manager' WHERE id=1;"));
        Assert.False(p.PrepareRequiredEdit(owner, f.Id, f.EditVersion).Ok);
        NoCommit(p.SetRequiredChecked(owner, f.Id, false, true, f.EditVersion));
    }

    [Fact]
    public void DeletedFieldCannotSucceedOrInventAnAudit()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p); var before = Audits(t);
        t.Database.Tx(c => t.Database.Execute(c, "DELETE FROM form_field_definitions WHERE id=@id;", new { id = f.Id }));
        Assert.False(p.PrepareRequiredEdit(TestData.Owner, f.Id, f.EditVersion).Ok);
        NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion)); Assert.Equal(before, Audits(t));
    }

    [Fact]
    public void CurrentStoredOwnerNameIsUsedRatherThanTheSuppliedStaleName()
    {
        using var t = new TestDatabase(); var p = Policy(t); var f = Field(p);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET name='Synthetic current owner' WHERE id=1;"));
        var result = p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion); Assert.True(result.Ok, result.Error);
        Assert.Equal("Synthetic current owner", result.NativeFieldEdit!.OverrideAcknowledgedBy);
    }
}
