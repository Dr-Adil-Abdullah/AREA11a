using System.IO;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Real-schema isolated fixtures; no WPF/session/runtime certification.</summary>
public sealed class CustomValueCurrentAccessTests
{
    private sealed class ValueAccessFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public CustomFieldService Fields { get; }
        public PartnerService Partners { get; }
        public int Customer { get; }
        public int Field { get; }
        public ValueAccessFixture()
        {
            Fields = new CustomFieldService(T.Database, T.Audit, TestData.Permissions(T), T.Log);
            Partners = new PartnerService(T.Database, T.Audit, T.Log,
                Path.Combine(Path.GetDirectoryName(T.Database.FilePath)!, "synthetic-photos"));
            Customer = TestData.AddCustomer(T);
            var added = Fields.Add(TestData.Owner, CustomFieldEntity.Customer, "Synthetic reference", CustomFieldType.Text, null, false);
            Assert.True(added.Ok, added.Error); Field = added.Value!.Id;
            Assert.True(Fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, Customer, Answers("old synthetic answer")).Ok);
        }
        public Dictionary<int, string?> Answers(string value) => new() { [Field] = value };
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql)));
        public void Dispose() => T.Dispose();
    }
    private static void Denied(ValueAccessFixture f, User? actor)
    {
        var audit = f.Number("SELECT COUNT(*) FROM audit_logs;");
        Assert.False(f.Fields.CheckValueAccess(actor, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.False(f.Fields.Values(actor, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.False(f.Fields.DescribeValues(actor, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.False(f.Fields.SaveValues(actor, CustomFieldEntity.Customer, f.Customer, f.Answers("denied change")).Ok);
        Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal("old synthetic answer", f.T.Database.Read(c => f.T.Database.Scalar(c, "SELECT value FROM custom_values WHERE field_id=@id;", new { id = f.Field }))?.ToString());
    }
    private static void CreationDenied(ValueAccessFixture f, User actor)
    {
        var count = f.Number("SELECT COUNT(*) FROM customers;"); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;");
        var result = f.Partners.CreateCustomer(actor, "Synthetic denied parent", null, "normal", f.Answers("must roll back"));
        Assert.False(result.Ok);
        Assert.Equal(count, f.Number("SELECT COUNT(*) FROM customers;")); Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM custom_values;"));
    }
    [Fact] public void AnonymousCannotReadOrSaveCustomAnswers() { using var f = new ValueAccessFixture(); Denied(f, null); }
    [Fact] public void InactiveSuppliedOwnerCannotReadOrSaveCustomAnswers() { using var f = new ValueAccessFixture(); Denied(f, new User { Id = 1, Role = "owner", Name = "Old", Active = false }); }
    [Fact] public void MissingStoredOwnerCannotUseCustomAnswers() { using var f = new ValueAccessFixture(); Denied(f, new User { Id = int.MaxValue, Role = "owner", Name = "Missing" }); }
    [Fact] public void ForgedOwnerCannotUseCustomAnswers() { using var f = new ValueAccessFixture(); var u = TestData.Cashier(f.T); Denied(f, new User { Id = u.Id, Role = "owner", Name = "Forged" }); }
    [Fact] public void InactiveStoredOwnerCannotUseCustomAnswers() { using var f = new ValueAccessFixture(); f.Execute("UPDATE users SET active=0 WHERE id=1;"); Denied(f, TestData.Owner); }
    [Fact] public void ChangedStoredRoleCannotUseCustomAnswers() { using var f = new ValueAccessFixture(); f.Execute("UPDATE users SET role='manager' WHERE id=1;"); Denied(f, TestData.Owner); }
    [Fact] public void RevokeBlocksCurrentAnswerReadsAndPublicSave()
    {
        using var f = new ValueAccessFixture(); var u = TestData.Cashier(f.T);
        Assert.True(f.Fields.Values(u, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner, u.Id, Modules.Customers, false).Ok); Denied(f, u);
    }
    [Fact] public void RegrantAllowsFreshAnswers()
    {
        using var f = new ValueAccessFixture(); var u = TestData.Cashier(f.T); var p = TestData.Permissions(f.T);
        Assert.True(p.Set(TestData.Owner, u.Id, Modules.Customers, false).Ok); Denied(f, u);
        Assert.True(p.Set(TestData.Owner, u.Id, Modules.Customers, true).Ok);
        Assert.True(f.Fields.SaveValues(u, CustomFieldEntity.Customer, f.Customer, f.Answers("fresh")).Ok);
        Assert.Equal("fresh", f.Fields.Values(u, CustomFieldEntity.Customer, f.Customer).Value![f.Field]);
    }
    [Fact] public void NonpositiveRecordIsNotCertifiedEmpty()
    {
        using var f = new ValueAccessFixture();
        foreach (var id in new[] { 0, -1 })
        {
            Assert.False(f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, id).Ok);
            Assert.False(f.Fields.DescribeValues(TestData.Owner, CustomFieldEntity.Customer, id).Ok);
            Assert.False(f.Fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, id, f.Answers("bad")).Ok);
        }
    }
    [Fact] public void MissingParentIsNotCertifiedEmptyOrOrphanAnswerOutput()
    {
        using var f = new ValueAccessFixture(); f.Execute("DELETE FROM customers WHERE id=" + f.Customer);
        Assert.False(f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.False(f.Fields.DescribeValues(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.False(f.Fields.CheckValueAccess(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.False(f.Fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, f.Customer, f.Answers("orphan")).Ok);
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM custom_values;"));
    }
    [Fact] public void ExistingRecordWithoutAnswersStillHasSuccessfulEmptyRead()
    {
        using var f = new ValueAccessFixture(); var other = TestData.AddCustomer(f.T, name: "Synthetic other");
        Assert.Empty(f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, other).Value!);
        Assert.True(f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, other).Ok);
        Assert.True(f.Fields.DescribeValues(TestData.Owner, CustomFieldEntity.Customer, other).Ok);
    }
    [Fact] public void InactiveParentRetainsHistoricalAnswersButNotWrites()
    {
        using var f = new ValueAccessFixture(); f.Execute("UPDATE customers SET active=0 WHERE id=" + f.Customer);
        Assert.True(f.Fields.CheckValueAccess(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.Equal("old synthetic answer", f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Value![f.Field]);
        Assert.Single(f.Fields.DescribeValues(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Value!);
        Assert.False(f.Fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, f.Customer, f.Answers("bad")).Ok);
    }
    [Fact] public void UnknownEntityIsRejectedBeforeTableSelection()
    {
        using var f = new ValueAccessFixture();
        Assert.False(f.Fields.CheckValueAccess(TestData.Owner, "customers; DROP TABLE users", f.Customer).Ok);
        Assert.False(f.Fields.Values(TestData.Owner, "unknown", f.Customer).Ok);
        Assert.False(f.Fields.DescribeValues(TestData.Owner, "unknown", f.Customer).Ok);
        Assert.False(f.Fields.SaveValues(TestData.Owner, "unknown", f.Customer, f.Answers("bad")).Ok);
    }
    [Fact] public void NewFormCheckRequiresCurrentActorButNotExistingRecord()
    {
        using var f = new ValueAccessFixture(); Assert.True(f.Fields.CheckValueAccess(TestData.Owner, CustomFieldEntity.Customer).Ok);
        Assert.False(f.Fields.CheckValueAccess(TestData.Owner, CustomFieldEntity.Customer, -1).Ok);
        f.Execute("UPDATE users SET active=0 WHERE id=1;"); Assert.False(f.Fields.CheckValueAccess(TestData.Owner, CustomFieldEntity.Customer).Ok);
    }
    [Fact] public void InternalCreationRejectsInactiveStoredActorAndRollsBackParent()
    {
        using var f = new ValueAccessFixture(); f.Execute("UPDATE users SET active=0 WHERE id=1;"); CreationDenied(f, TestData.Owner);
    }
    [Fact] public void InternalCreationRejectsChangedRoleAndRollsBackParent()
    {
        using var f = new ValueAccessFixture(); f.Execute("UPDATE users SET role='manager' WHERE id=1;"); CreationDenied(f, TestData.Owner);
    }
    [Fact] public void InternalCreationRejectsInactiveSuppliedActor()
    {
        using var f = new ValueAccessFixture(); CreationDenied(f, new User { Id = 1, Role = "owner", Name = "Old", Active = false });
    }
    [Fact] public void InternalCreationRejectsForgedOwner()
    {
        using var f = new ValueAccessFixture(); var u = TestData.Cashier(f.T); CreationDenied(f, new User { Id = u.Id, Role = "owner", Name = "Forged" });
    }
    [Fact] public void InternalCreationChecksActorEvenWhenNoAnswersWereSupplied()
    {
        using var f = new ValueAccessFixture(); f.Execute("UPDATE users SET active=0 WHERE id=1;"); var before = f.Number("SELECT COUNT(*) FROM customers;");
        Assert.False(f.Partners.CreateCustomer(TestData.Owner, "Synthetic no answers", null, "normal").Ok);
        Assert.Equal(before, f.Number("SELECT COUNT(*) FROM customers;"));
    }
    [Fact] public void InternalQuickCustomerCreationDoesNotAddCustomersGrantRequirement()
    {
        using var f = new ValueAccessFixture(); var u = TestData.Cashier(f.T);
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner, u.Id, Modules.Customers, false).Ok);
        Assert.False(f.Fields.CheckValueAccess(u, CustomFieldEntity.Customer).Ok);
        var result = f.Partners.CreateCustomer(u, "Synthetic POS quick create", null, "normal");
        Assert.True(result.Ok, result.Error); // Existing workflow-owned policy, NOT a general authorization endorsement.
    }
    [Fact] public void InternalCustomValueAuditUsesStoredCurrentActorName()
    {
        using var f = new ValueAccessFixture(); f.Execute("UPDATE users SET name='Current synthetic owner' WHERE id=1;");
        var result = f.Partners.CreateCustomer(TestData.Owner, "Synthetic audit parent", null, "normal", f.Answers("new answer"));
        Assert.True(result.Ok, result.Error);
        var name = f.T.Database.Read(c => f.T.Database.Scalar(c, "SELECT user_name FROM audit_logs WHERE action='custom_value_set' ORDER BY id DESC LIMIT 1;"))?.ToString();
        Assert.Equal("Current synthetic owner", name);
    }
    [Fact] public void AuditFaultRollsBackAllAnswersAndParentCreation()
    {
        using var f = new ValueAccessFixture(); var count = f.Number("SELECT COUNT(*) FROM customers;"); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;");
        f.Execute("CREATE TRIGGER fail_custom_value BEFORE INSERT ON audit_logs WHEN NEW.action='custom_value_set' BEGIN SELECT RAISE(ABORT,'synthetic value fault'); END;");
        Assert.False(f.Fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, f.Customer, f.Answers("bad")).Ok);
        Assert.False(f.Partners.CreateCustomer(TestData.Owner, "Synthetic fault parent", null, "normal", f.Answers("bad")).Ok);
        Assert.Equal(count, f.Number("SELECT COUNT(*) FROM customers;")); Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal("old synthetic answer", f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Value![f.Field]);
    }
    [Fact] public void ReadInfrastructureFailureHasGenericFailureNotProviderText()
    {
        using var f = new ValueAccessFixture(); f.Execute("DROP TABLE custom_values;");
        var result = f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, f.Customer);
        Assert.False(result.Ok); Assert.DoesNotContain("custom_values", result.Error ?? ""); Assert.DoesNotContain("SQLite", result.Error ?? "");
    }
    [Fact] public void ExistingDocumentAnswerReadChecksParentAndPreservesPosModule()
    {
        using var f = new ValueAccessFixture(); f.Execute("INSERT INTO sales(code) VALUES('SYNTHETIC-ONLY');");
        var id = (int)f.Number("SELECT id FROM sales WHERE code='SYNTHETIC-ONLY';"); var u = TestData.Cashier(f.T);
        Assert.True(f.Fields.Values(u, CustomFieldEntity.Sale, id).Ok);
        Assert.True(f.Fields.DescribeValues(u, CustomFieldEntity.Sale, id).Ok);
        Assert.False(f.Fields.Values(u, CustomFieldEntity.Sale, int.MaxValue).Ok);
    }
    [Fact] public void ReadAndPreflightDoNotWriteAuditOrAnswers()
    {
        using var f = new ValueAccessFixture(); var count = f.Number("SELECT COUNT(*) FROM audit_logs;");
        Assert.True(f.Fields.CheckValueAccess(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.True(f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.True(f.Fields.DescribeValues(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Ok);
        Assert.Equal(count, f.Number("SELECT COUNT(*) FROM audit_logs;")); Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM custom_values;"));
    }
    [Fact] public void HiddenDefinitionAnswersRemainInHistory()
    {
        using var f = new ValueAccessFixture(); Assert.True(f.Fields.SetActive(TestData.Owner, f.Field, false).Ok);
        Assert.Equal("old synthetic answer", f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Value![f.Field]);
        Assert.Single(f.Fields.DescribeValues(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Value!);
    }
}
