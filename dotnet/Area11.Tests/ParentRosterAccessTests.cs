using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Current parent navigation and administrative vs operational roster reads. Windows UNRUN.</summary>
public sealed class ParentRosterAccessTests
{
    private static PartnerAccessService Partners(TestDatabase t) => new(t.Database, TestData.Permissions(t), t.Log);
    private static AuthService Auth(TestDatabase t) => new(t.Database, t.Audit, t.Settings, t.Log);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    { using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L); }
    private static void Grant(TestDatabase t, User u, string module, bool yes = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, module, yes).Ok);
    private static User Staff(TestDatabase t)
    { var u = TestData.Cashier(t); Grant(t, u, Modules.Customers, false); return u; }
    private static void OwnerDenied(TestDatabase t, User? actor)
    {
        var auth = Auth(t); var before = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        Assert.False(auth.CheckStaffAccess(actor).Ok);
        var result = auth.ListUsers(actor); Assert.False(result.Ok); Assert.Null(result.Value);
        Assert.Equal(before, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }

    [Fact]
    public void NoPartnerGrantSelectsNoTab()
    {
        var access = new PartnerAccess(); Assert.False(access.HasAny);
        foreach (var n in new[] { -1, 0, 1, 2, 3, int.MaxValue }) Assert.Equal(-1, access.SelectTab(n));
    }

    [Fact]
    public void CustomersKeepsBothCustomerChaptersAndRejectsSupplierIndex()
    {
        var access = new PartnerAccess { Customers = true };
        Assert.True(access.HasAny); Assert.Equal(0, access.SelectTab(0)); Assert.Equal(1, access.SelectTab(1)); Assert.Equal(0, access.SelectTab(2)); Assert.Equal(0, access.SelectTab(-1));
    }

    [Fact]
    public void SupplierOnlyAlwaysSelectsItsPhysicalIndexTwo()
    {
        var access = new PartnerAccess { Suppliers = true };
        foreach (var n in new[] { -1, 0, 1, 2, 20 }) Assert.Equal(2, access.SelectTab(n));
    }

    [Fact]
    public void BothPartnerGrantsPreserveAllowedSelection()
    {
        var access = new PartnerAccess { Customers = true, Suppliers = true };
        foreach (var n in new[] { 0, 1, 2 }) Assert.Equal(n, access.SelectTab(n)); Assert.Equal(0, access.SelectTab(42));
    }

    [Fact]
    public void AnonymousParentAccessIsRefused()
    {
        using var t = new TestDatabase(); var result = Partners(t).Read(null); Assert.False(result.Ok); Assert.Null(result.Value);
    }

    [Fact]
    public void CustomersOnlyParentDoesNotAcquireSuppliers()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Customers);
        var result = Partners(t).Read(u); Assert.True(result.Ok, result.Error); Assert.True(result.Value!.Customers); Assert.False(result.Value.Suppliers);
    }

    [Fact]
    public void SuppliersOnlyParentDoesNotAcquireCustomers()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Suppliers);
        var result = Partners(t).Read(u); Assert.True(result.Ok, result.Error); Assert.False(result.Value!.Customers); Assert.True(result.Value.Suppliers); Assert.Equal(2, result.Value.SelectTab(0));
    }

    [Fact]
    public void FinancialAndStaffGrantsCannotReplacePartnerModules()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Profit); Grant(t, u, Modules.PurchaseCost); Grant(t, u, Modules.Staff);
        Assert.False(Partners(t).Read(u).Ok);
    }

    [Fact]
    public void ParentVisibilityDoesNotDependOnFinancialMain()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Customers); Grant(t, u, Modules.Suppliers);
        Grant(t, u, Modules.Profit, false); Grant(t, u, Modules.PurchaseCost, false);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var access = Partners(t).Read(u); Assert.True(access.Ok, access.Error); Assert.True(access.Value!.Customers); Assert.True(access.Value.Suppliers);
    }

    [Fact]
    public void ParentRechecksRevokeAndRestoreOnSameService()
    {
        using var t = new TestDatabase(); var u = Staff(t); var service = Partners(t); Grant(t, u, Modules.Customers);
        Assert.True(service.Read(u).Ok); Grant(t, u, Modules.Customers, false); Assert.False(service.Read(u).Ok);
        Grant(t, u, Modules.Suppliers); var restored = service.Read(u); Assert.True(restored.Ok, restored.Error); Assert.Equal(2, restored.Value!.SelectTab(1));
    }

    [Fact]
    public void ParentRejectsDeactivatedStoredAndInactiveSuppliedUsers()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Customers); var service = Partners(t);
        u.Active = false; Assert.False(service.Read(u).Ok); u.Active = true;
        Execute(t, "UPDATE users SET active=0 WHERE id=" + u.Id); Assert.False(service.Read(u).Ok);
    }

    [Fact]
    public void ParentRejectsForgedOrChangedStoredRoleAndMissingId()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Customers); var service = Partners(t);
        Assert.False(service.Read(new User { Id = u.Id, Role = "owner" }).Ok);
        Assert.False(service.Read(new User { Id = int.MaxValue, Role = "owner" }).Ok);
        Execute(t, "UPDATE users SET role='manager' WHERE id=" + u.Id); Assert.False(service.Read(u).Ok);
    }

    [Fact]
    public void OwnerRosterIncludesInactiveAccountsWithoutSecretFields()
    {
        using var t = new TestDatabase(); var u = Staff(t); Execute(t, "UPDATE users SET active=0 WHERE id=" + u.Id);
        var result = Auth(t).ListUsers(TestData.Owner); Assert.True(result.Ok, result.Error); Assert.Equal(1, result.Value![0].Id);
        Assert.Contains(result.Value, person => person.Id == u.Id && !person.Active);
        Assert.DoesNotContain(typeof(User).GetProperties(), property => property.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnonymousCannotReadAdministrativeRoster()
    {
        using var t = new TestDatabase(); OwnerDenied(t, null);
    }

    [Fact]
    public void StaffGrantDoesNotTurnStaffIntoOwnerRosterReader()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Staff); Grant(t, u, Modules.Settings); OwnerDenied(t, u);
    }

    [Fact]
    public void ManagerCannotReadAdministrativeRoster()
    {
        using var t = new TestDatabase(); var u = Staff(t); Execute(t, "UPDATE users SET role='manager' WHERE id=" + u.Id); u.Role = "manager"; OwnerDenied(t, u);
    }

    [Fact]
    public void ForgedOwnerCannotReadAdministrativeRoster()
    {
        using var t = new TestDatabase(); var u = Staff(t); OwnerDenied(t, new User { Id = u.Id, Role = "owner" });
    }

    [Fact]
    public void InactiveSuppliedOwnerCannotReadAdministrativeRoster()
    {
        using var t = new TestDatabase(); OwnerDenied(t, new User { Id = 1, Role = "owner", Active = false });
    }

    [Fact]
    public void DeactivatedOrDemotedOwnerCannotReadAdministrativeRoster()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE users SET active=0 WHERE id=1;"); OwnerDenied(t, TestData.Owner);
        Execute(t, "UPDATE users SET active=1,role='manager' WHERE id=1;"); OwnerDenied(t, TestData.Owner);
    }

    [Fact]
    public void OwnerRosterWorksAfterRestoreWithoutFinancialGrants()
    {
        using var t = new TestDatabase(); var auth = Auth(t); Grant(t, TestData.Owner, Modules.Profit, false); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Execute(t, "UPDATE users SET active=0 WHERE id=1;"); Assert.False(auth.ListUsers(TestData.Owner).Ok);
        Execute(t, "UPDATE users SET active=1 WHERE id=1;"); Assert.True(auth.CheckStaffAccess(TestData.Owner).Ok); Assert.True(auth.ListUsers(TestData.Owner).Ok);
    }

    [Fact]
    public void CashDelegationAllowsOperationalPickerButNotAdministration()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Cash);
        Assert.True(Auth(t).ListHandoverPeople(u).Ok); Assert.False(Auth(t).ListUsers(u).Ok);
    }

    [Fact]
    public void CustomersOrStaffGrantDoesNotReplaceCashPickerGrant()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Customers); Grant(t, u, Modules.Staff);
        Assert.False(Auth(t).ListHandoverPeople(u).Ok);
    }

    [Fact]
    public void PickerExcludesSelfAndInactiveAndKeepsOtherActivePeople()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Cash);
        Execute(t, "INSERT INTO users(name,role,active) VALUES('Synthetic off','manager',0),('Synthetic other','cashier',1);");
        var result = Auth(t).ListHandoverPeople(u); Assert.True(result.Ok, result.Error);
        Assert.DoesNotContain(result.Value!, person => person.Id == u.Id || !person.Active);
        Assert.Contains(result.Value!, person => person.Name == "Synthetic other"); Assert.Contains(result.Value!, person => person.IsOwner);
    }

    [Fact]
    public void PickerRechecksCashRevocationAndRestore()
    {
        using var t = new TestDatabase(); var u = Staff(t); var auth = Auth(t); Grant(t, u, Modules.Cash); Assert.True(auth.ListHandoverPeople(u).Ok);
        Grant(t, u, Modules.Cash, false); var denied = auth.ListHandoverPeople(u); Assert.False(denied.Ok); Assert.Null(denied.Value);
        Grant(t, u, Modules.Cash); Assert.True(auth.ListHandoverPeople(u).Ok);
    }

    [Fact]
    public void PickerRejectsInactiveForgedChangedAndMissingReaders()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Cash); var auth = Auth(t);
        Assert.False(auth.ListHandoverPeople(null).Ok); Assert.False(auth.ListHandoverPeople(new User { Id = u.Id, Role = "owner" }).Ok);
        Assert.False(auth.ListHandoverPeople(new User { Id = int.MaxValue, Role = "owner" }).Ok);
        u.Active = false; Assert.False(auth.ListHandoverPeople(u).Ok); u.Active = true;
        Execute(t, "UPDATE users SET active=0 WHERE id=" + u.Id); Assert.False(auth.ListHandoverPeople(u).Ok);
        Execute(t, "UPDATE users SET active=1,role='manager' WHERE id=" + u.Id); Assert.False(auth.ListHandoverPeople(u).Ok);
    }

    [Fact]
    public void CashPickerNeedsNoFinancialOrStaffPermission()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Cash); Grant(t, u, Modules.Staff, false); Grant(t, u, Modules.Profit, false); Grant(t, u, Modules.PurchaseCost, false);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.True(Auth(t).ListHandoverPeople(u).Ok);
    }

    [Fact]
    public void ParentAndBothRosterReadsDoNotWriteAuditOrAccountData()
    {
        using var t = new TestDatabase(); Staff(t); var audit = Number(t, "SELECT COUNT(*) FROM audit_logs;"); var count = Number(t, "SELECT COUNT(*) FROM users;");
        Assert.True(Partners(t).Read(TestData.Owner).Ok); Assert.True(Auth(t).ListUsers(TestData.Owner).Ok); Assert.True(Auth(t).ListHandoverPeople(TestData.Owner).Ok);
        Assert.Equal(audit, Number(t, "SELECT COUNT(*) FROM audit_logs;")); Assert.Equal(count, Number(t, "SELECT COUNT(*) FROM users;"));
    }

    [Fact]
    public void DatabaseErrorsProduceSafeFailureNotAnEmptySuccess()
    {
        using var t = new TestDatabase(); Execute(t, "ALTER TABLE users RENAME TO synthetic_missing_users;");
        var roster = Auth(t).ListUsers(TestData.Owner); var picker = Auth(t).ListHandoverPeople(TestData.Owner); var parent = Partners(t).Read(TestData.Owner);
        Assert.False(roster.Ok); Assert.Null(roster.Value); Assert.False(picker.Ok); Assert.Null(picker.Value); Assert.False(parent.Ok); Assert.Null(parent.Value);
        Assert.DoesNotContain("no such table", roster.Error!); Assert.DoesNotContain("synthetic_missing", picker.Error!); Assert.DoesNotContain("no such table", parent.Error!);
    }
}
