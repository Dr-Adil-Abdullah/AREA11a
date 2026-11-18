using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

public sealed class NativeFieldCurrentAccessTests
{
    private static NativeFieldPolicyService Policy(TestDatabase t) => new(t.Database, t.Audit, t.Log);
    private static void Denied(TestDatabase t, User actor)
    {
        var service = Policy(t); Assert.True(service.EnsureDefaults().Ok);
        var field = t.Database.Read(c => Convert.ToInt32(t.Database.Scalar(c, "SELECT id FROM form_field_definitions WHERE field_key='sale.quantity';")));
        var audit = t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs;")));
        Assert.False(service.List(actor).Ok); Assert.False(service.SetRequired(actor, field, false, true).Ok);
        Assert.Equal(1L, t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT required FROM form_field_definitions WHERE id=@id;", new { id = field }))));
        Assert.Equal(audit, t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs;"))));
    }
    [Fact] public void InactiveSuppliedOwnerCannotReadOrChangeNativePolicy()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = 1, Name = "Old", Role = "owner", Active = false });
    }
    [Fact] public void InactiveStoredOwnerCannotReadOrChangeNativePolicy()
    {
        using var t = new TestDatabase(); t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"); }); Denied(t, TestData.Owner);
    }
    [Fact] public void ChangedOwnerRoleCannotReadOrChangeNativePolicy()
    {
        using var t = new TestDatabase(); t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET role='manager' WHERE id=1;"); }); Denied(t, TestData.Owner);
    }
    [Fact] public void ForgedOwnerCannotReadOrChangeNativePolicy()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Denied(t, new User { Id = u.Id, Name = "Forged", Role = "owner" });
    }
    [Fact] public void RestoredOwnerGetsFreshPolicyWithoutLosingRiskConfirmation()
    {
        using var t = new TestDatabase(); var service = Policy(t); Assert.True(service.EnsureDefaults().Ok);
        var field = service.List(TestData.Owner).Value!.Single(f => f.FieldKey == "sale.quantity");
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"); }); Assert.False(service.List(TestData.Owner).Ok);
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET active=1 WHERE id=1;"); }); Assert.True(service.List(TestData.Owner).Ok);
        Assert.False(service.SetRequired(TestData.Owner, field.Id, false, false).Ok);
        Assert.True(service.SetRequired(TestData.Owner, field.Id, false, true).Ok);
        Assert.False(service.List(TestData.Owner).Value!.Single(f => f.Id == field.Id).Required);
    }
    [Fact] public void NativePolicyQueryFaultIsFailureNotCertifiedEmpty()
    {
        using var t = new TestDatabase(); var service = Policy(t); Assert.True(service.EnsureDefaults().Ok);
        t.Database.Tx(c => { t.Database.Execute(c, "DROP TABLE form_field_definitions;"); });
        var result = service.List(TestData.Owner); Assert.False(result.Ok); Assert.Null(result.Value);
        Assert.DoesNotContain("SQLite", result.Error ?? "");
    }
}
