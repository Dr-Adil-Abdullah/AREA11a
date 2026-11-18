using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Real-schema isolated Core source regressions, not Desktop/session execution.</summary>
public sealed class CustomDefinitionCurrentAccessTests
{
    private static CustomField Definition() => new()
    {
        Id = 42, Entity = CustomFieldEntity.Customer, FieldKey = "customer.custom.42",
        Label = "Synthetic question", Type = CustomFieldType.Select, OptionsJson = "[\"A\",\"B\"]",
        HelpText = "Synthetic guidance", Required = true, Active = true, Sort = 2, CreatedAt = "2026-11-10 12:00:00"
    };
    [Fact] public void EqualPersistedDefinitionsMatch() => Assert.True(CustomFieldDefinitionState.Matches(Definition(), Definition()));
    [Fact] public void MissingExpectedDefinitionNeverMatches() => Assert.False(CustomFieldDefinitionState.Matches(null, Definition()));
    [Fact] public void MissingCurrentDefinitionNeverMatches() => Assert.False(CustomFieldDefinitionState.Matches(Definition(), null));
    [Fact] public void ChangedDefinitionIdDoesNotMatch() { var f = Definition(); f.Id++; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }
    [Fact] public void ChangedDefinitionEntityDoesNotMatch() { var f = Definition(); f.Entity = CustomFieldEntity.Supplier; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }
    [Fact] public void ChangedStableKeyDoesNotMatch() { var f = Definition(); f.FieldKey = "customer.custom.43"; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }
    [Fact] public void ChangedDefinitionLabelDoesNotMatch() { var f = Definition(); f.Label = "Other question"; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }
    [Fact] public void ChangedDefinitionTypeDoesNotMatch() { var f = Definition(); f.Type = CustomFieldType.Text; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }
    [Fact] public void ChangedDefinitionOptionsDoNotMatch() { var f = Definition(); f.OptionsJson = "[\"A\",\"C\"]"; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }
    [Fact] public void ChangedDefinitionHelpDoesNotMatch() { var f = Definition(); f.HelpText = "New guidance"; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }
    [Fact] public void ChangedRequiredFlagDoesNotMatch() { var f = Definition(); f.Required = false; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }
    [Fact] public void ChangedActiveFlagDoesNotMatch() { var f = Definition(); f.Active = false; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }
    [Fact] public void ChangedOrderDoesNotMatch() { var f = Definition(); f.Sort++; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }
    [Fact] public void ChangedCreationStampDoesNotMatch() { var f = Definition(); f.CreatedAt = "2026-11-10 12:00:01"; Assert.False(CustomFieldDefinitionState.Matches(Definition(), f)); }

    private sealed class DefinitionAccessFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public CustomFieldService Fields { get; }
        public int First { get; }
        public int Second { get; }
        public int Customer { get; }
        public DefinitionAccessFixture()
        {
            Fields = new CustomFieldService(T.Database, T.Audit, TestData.Permissions(T), T.Log);
            var first = Fields.Add(TestData.Owner, CustomFieldEntity.Customer, "Synthetic first", CustomFieldType.Text, null, false);
            var second = Fields.Add(TestData.Owner, CustomFieldEntity.Customer, "Synthetic second", CustomFieldType.Text, null, false);
            Assert.True(first.Ok, first.Error); Assert.True(second.Ok, second.Error);
            First = first.Value!.Id; Second = second.Value!.Id; Customer = TestData.AddCustomer(T);
            Assert.True(Fields.SaveValues(TestData.Owner, CustomFieldEntity.Customer, Customer, new Dictionary<int, string?> { [First] = "Synthetic saved answer" }).Ok);
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql)));
        public string? Snapshot() => T.Database.Read(c => T.Database.Scalar(c,
            "SELECT group_concat(id || ':' || label || ':' || active || ':' || sort, ';') FROM (SELECT id,label,active,sort FROM custom_fields ORDER BY id);"))?.ToString();
        public void Fault(string action) => Execute("CREATE TRIGGER fail_definition_audit BEFORE INSERT ON audit_logs WHEN NEW.action='" + action + "' BEGIN SELECT RAISE(ABORT,'synthetic definition fault'); END;");
        public void Dispose() => T.Dispose();
    }
    private static void Denied(DefinitionAccessFixture f, User? actor)
    {
        var before = f.Snapshot(); var audits = f.Number("SELECT COUNT(*) FROM audit_logs;");
        Assert.False(f.Fields.List(actor, CustomFieldEntity.Customer, includeInactive: true, manage: true).Ok);
        Assert.False(f.Fields.List(actor, CustomFieldEntity.Customer, includeInactive: true).Ok);
        Assert.False(f.Fields.Add(actor, CustomFieldEntity.Customer, "Denied new", CustomFieldType.Text, null, false).Ok);
        Assert.False(f.Fields.Update(actor, f.First, "Denied change", CustomFieldType.Text, null, false).Ok);
        Assert.False(f.Fields.SetActive(actor, f.First, false).Ok);
        Assert.False(f.Fields.SetActive(actor, f.First, true).Ok); // A no-op must not bypass the current owner.
        Assert.False(f.Fields.Move(actor, f.First, 1).Ok);
        Assert.False(f.Fields.Move(actor, f.First, -1).Ok); // Boundary failure must not precede authorization.
        Assert.Equal(before, f.Snapshot()); Assert.Equal(audits, f.Number("SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM custom_values;"));
    }
    [Fact] public void AnonymousCannotManageDefinitions() { using var f = new DefinitionAccessFixture(); Denied(f, null); }
    [Fact] public void InactiveSuppliedOwnerCannotManageDefinitions() { using var f = new DefinitionAccessFixture(); Denied(f, new User { Id = 1, Role = "owner", Name = "Old", Active = false }); }
    [Fact] public void MissingStoredOwnerCannotManageDefinitions() { using var f = new DefinitionAccessFixture(); Denied(f, new User { Id = int.MaxValue, Role = "owner", Name = "Missing" }); }
    [Fact] public void ForgedOwnerCannotManageDefinitions() { using var f = new DefinitionAccessFixture(); var u = TestData.Cashier(f.T); Denied(f, new User { Id = u.Id, Role = "owner", Name = "Forged" }); }
    [Fact] public void InactiveStoredOwnerCannotManageDefinitions() { using var f = new DefinitionAccessFixture(); f.Execute("UPDATE users SET active=0 WHERE id=1;"); Denied(f, TestData.Owner); }
    [Fact] public void ChangedStoredOwnerRoleCannotManageDefinitions() { using var f = new DefinitionAccessFixture(); f.Execute("UPDATE users SET role='manager' WHERE id=1;"); Denied(f, TestData.Owner); }
    [Fact] public void OwnerReactivationAllowsFreshManagementRead()
    {
        using var f = new DefinitionAccessFixture(); f.Execute("UPDATE users SET active=0 WHERE id=1;"); Denied(f, TestData.Owner);
        f.Execute("UPDATE users SET active=1 WHERE id=1;"); Assert.Equal(2, f.Fields.List(TestData.Owner, CustomFieldEntity.Customer, true, true).Value!.Count);
        Assert.True(f.Fields.SetActive(TestData.Owner, f.First, false).Ok);
    }
    [Fact] public void ManagerWithSettingsGrantStillCannotManageDefinitions()
    {
        using var f = new DefinitionAccessFixture(); var u = TestData.AddUser(f.T, "manager");
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner, u.Id, Modules.Settings, true).Ok); Denied(f, u);
    }
    [Fact] public void AllDefinitionWritesAuditStoredCurrentOwnerName()
    {
        using var f = new DefinitionAccessFixture(); f.Execute("UPDATE users SET name='Current synthetic owner' WHERE id=1;");
        Assert.True(f.Fields.Add(TestData.Owner, CustomFieldEntity.Customer, "Synthetic third", CustomFieldType.Text, null, false).Ok);
        Assert.True(f.Fields.Update(TestData.Owner, f.First, "Renamed synthetic", CustomFieldType.Text, null, false).Ok);
        Assert.True(f.Fields.SetActive(TestData.Owner, f.First, false).Ok); Assert.True(f.Fields.Move(TestData.Owner, f.First, 1).Ok);
        Assert.Equal(4L, f.Number("SELECT COUNT(*) FROM audit_logs WHERE action IN ('custom_field_add','custom_field_update','custom_field_off','custom_field_order') AND user_name='Current synthetic owner';"));
    }
    [Fact] public void AddAuditFailureRollsBackDefinitionAndStableKey()
    {
        using var f = new DefinitionAccessFixture(); var before = f.Snapshot(); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;"); f.Fault("custom_field_add");
        var result = f.Fields.Add(TestData.Owner, CustomFieldEntity.Customer, "Synthetic fault add", CustomFieldType.Text, null, false);
        Assert.False(result.Ok); Assert.DoesNotContain("synthetic definition fault", result.Error ?? "");
        Assert.Equal(before, f.Snapshot()); Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
    }
    [Fact] public void UpdateAuditFailureRollsBackDefinition()
    {
        using var f = new DefinitionAccessFixture(); var before = f.Snapshot(); f.Fault("custom_field_update");
        Assert.False(f.Fields.Update(TestData.Owner, f.First, "Synthetic fault update", CustomFieldType.Text, null, false).Ok); Assert.Equal(before, f.Snapshot());
    }
    [Fact] public void HideAuditFailureKeepsDefinitionAndAnswers()
    {
        using var f = new DefinitionAccessFixture(); var before = f.Snapshot(); f.Fault("custom_field_off");
        Assert.False(f.Fields.SetActive(TestData.Owner, f.First, false).Ok); Assert.Equal(before, f.Snapshot());
        Assert.Equal("Synthetic saved answer", f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Value![f.First]);
    }
    [Fact] public void MoveAuditFailureRollsBackWholeOrdering()
    {
        using var f = new DefinitionAccessFixture(); var before = f.Snapshot(); f.Fault("custom_field_order");
        Assert.False(f.Fields.Move(TestData.Owner, f.First, 1).Ok); Assert.Equal(before, f.Snapshot());
    }
    [Fact] public void HideRestoreAndRenamePreserveStableKeyAndHistoricalAnswer()
    {
        using var f = new DefinitionAccessFixture(); var old = f.Fields.List(TestData.Owner, CustomFieldEntity.Customer, true, true).Value!.Single(x => x.Id == f.First);
        Assert.True(f.Fields.SetActive(TestData.Owner, f.First, false).Ok);
        Assert.Single(f.Fields.List(TestData.Owner, CustomFieldEntity.Customer).Value!);
        Assert.Equal("Synthetic saved answer", f.Fields.Values(TestData.Owner, CustomFieldEntity.Customer, f.Customer).Value![f.First]);
        Assert.True(f.Fields.SetActive(TestData.Owner, f.First, true).Ok);
        Assert.True(f.Fields.Update(TestData.Owner, f.First, "Synthetic renamed", CustomFieldType.Text, null, false).Ok);
        var now = f.Fields.List(TestData.Owner, CustomFieldEntity.Customer, true, true).Value!.Single(x => x.Id == f.First);
        Assert.Equal(old.FieldKey, now.FieldKey); Assert.False(CustomFieldDefinitionState.Matches(old, now));
    }
    [Fact] public void ManagementListInfrastructureFailureDoesNotExposeProviderDetails()
    {
        using var f = new DefinitionAccessFixture(); f.Execute("DROP TABLE custom_values; DROP TABLE custom_fields;");
        var result = f.Fields.List(TestData.Owner, CustomFieldEntity.Customer, true, true);
        Assert.False(result.Ok); Assert.Null(result.Value); Assert.DoesNotContain("custom_fields", result.Error ?? ""); Assert.DoesNotContain("SQLite", result.Error ?? "");
    }
    [Fact] public void DocumentFormsStillRejectPhotoDefinition()
    {
        using var f = new DefinitionAccessFixture();
        foreach (var entity in new[] { CustomFieldEntity.Sale, CustomFieldEntity.Purchase, CustomFieldEntity.Expense })
            Assert.False(f.Fields.Add(TestData.Owner, entity, "Synthetic document photo", CustomFieldType.Photo, null, false).Ok);
        Assert.Equal(2L, f.Number("SELECT COUNT(*) FROM custom_fields;"));
    }
    [Fact] public void ManagementListIncludesHiddenOnlyOnItsOwnForm()
    {
        using var f = new DefinitionAccessFixture(); Assert.True(f.Fields.SetActive(TestData.Owner, f.First, false).Ok);
        Assert.Equal(2, f.Fields.List(TestData.Owner, CustomFieldEntity.Customer, true, true).Value!.Count);
        Assert.Empty(f.Fields.List(TestData.Owner, CustomFieldEntity.Supplier, true, true).Value!);
    }
    [Fact] public void IdempotentActiveAndUnchangedUpdateDoNotAddAudit()
    {
        using var f = new DefinitionAccessFixture(); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;"); var before = f.Snapshot();
        Assert.True(f.Fields.SetActive(TestData.Owner, f.First, true).Ok);
        Assert.True(f.Fields.Update(TestData.Owner, f.First, "Synthetic first", CustomFieldType.Text, null, false).Ok);
        Assert.False(f.Fields.Move(TestData.Owner, f.First, -1).Ok);
        Assert.Equal(before, f.Snapshot()); Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
    }
}
