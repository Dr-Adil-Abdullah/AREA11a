using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Explicitly ENABLED boundary;legacy fixture actors are not authentication evidence.
/// Written real-schema tests;execution/build needs .NET,UI next-action cases are separate.</summary>
public sealed class AuthenticatedSessionTests
{
    private static AuthService Auth(TestDatabase t) => new(t.Database, t.Audit, t.Settings, t.Log);
    private static User Login(AuthService auth, string name = "admin", string secret = "area11")
    {
        var result = auth.Login(name, secret);
        Assert.True(result.Ok, result.Error);
        return Assert.IsType<User>(result.Value);
    }
    private static User Enable(TestDatabase t)
    {
        t.Database.Sessions.Enable();
        return Login(Auth(t));
    }
    private static User Copy(User u) => new() { Id = u.Id, Name = u.Name, Role = u.Role, Active = u.Active };
    private static CustomFieldService Fields(TestDatabase t) =>
        new(t.Database, t.Audit, TestData.Permissions(t), t.Log);

    private static SaleService Sales(TestDatabase t) =>
        new(t.Database, t.Audit, new NumberingService(t.Settings), t.Settings, TestData.Permissions(t), t.Log);

    [Fact]
    public void FreshReadersCarryTheVerifiedLeaseForQuoteAndCustomValues()
    {
        using var t = new TestDatabase(); var owner = Enable(t);
        var product = TestData.AddProduct(t);
        var result = Sales(t).Quote(TestData.Cart(product), owner);
        Assert.True(result.Ok, result.Error);
        Assert.True(Fields(t).CheckValueAccess(owner, CustomFieldEntity.Sale).Ok);
        Assert.True(Fields(t).List(owner, CustomFieldEntity.Sale, manage: true).Ok);
    }

    [Fact]
    public void DisabledLegacyHostIsNotSessionCertification()
    {
        using var t = new TestDatabase();
        Assert.False(t.Database.Sessions.Enabled);
        Assert.False(Auth(t).CheckSession(TestData.Owner).Ok);
        Assert.True(TestData.Permissions(t).Can(TestData.Owner, Modules.Pos));
    }

    [Fact]
    public void EnableIsOneWayAndRepeatedEnableDoesNotDiscardProof()
    {
        using var t = new TestDatabase(); var owner = Enable(t);
        t.Database.Sessions.Enable();
        Assert.True(t.Database.Sessions.Enabled);
        Assert.True(Auth(t).CheckSession(owner).Ok);
    }

    [Fact]
    public void RealLoginWorksButSameIdRoleObjectIsNotAuthenticated()
    {
        using var t = new TestDatabase(); var owner = Enable(t);
        Assert.True(TestData.Permissions(t).Can(owner, Modules.Pos));
        Assert.False(TestData.Permissions(t).Can(Copy(owner), Modules.Pos));
        Assert.False(Auth(t).CheckSession(Copy(owner)).Ok);
        Assert.False(Auth(t).CheckSession(null).Ok);
    }

    [Fact]
    public void ProofCannotBeUsedOnAnotherDatabaseInstanceEvenForSameFile()
    {
        using var t = new TestDatabase(); var owner = Enable(t);
        var other = new Database(t.Database.FilePath, pooling: false, durable: false);
        other.Sessions.Enable();
        Assert.Throws<ValidationException>(() => other.Sessions.Require(owner));
        Assert.True(Auth(t).CheckSession(owner).Ok);
    }

    [Fact]
    public void ForgedOwnerCannotWriteOrInspectPermissions()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var fake = Copy(owner);
        Assert.False(Auth(t).CreateStaff(fake, "Forged staff", UserRole.Cashier, "1234").Ok);
        Assert.False(TestData.Permissions(t).Set(fake, owner.Id, Modules.Profit, false).Ok);
        Assert.False(new PermissionInspectionService(t.Database, t.Log).Inspect(fake, Modules.Pos).Ok);
    }

    [Fact]
    public void ProtectedCloseProofCannotBePromotedToInteractiveLogin()
    {
        using var t = new TestDatabase(); Enable(t);
        var close = Auth(t).VerifySecret("admin", "area11"); Assert.True(close.Ok, close.Error);
        var user = Assert.IsType<User>(close.Value);
        Assert.True(TestData.Permissions(t).Can(user, Modules.CloseApp));
        Assert.False(TestData.Permissions(t).Can(user, Modules.Pos));
        Assert.False(Auth(t).CheckSession(user).Ok);
        Assert.False(Auth(t).CreateStaff(user, "No close promotion", UserRole.Cashier, "1234").Ok);
    }

    [Fact]
    public void LogoutRevokesOnlyThatLoginAndFreshLoginIsRequired()
    {
        using var t = new TestDatabase(); var first = Enable(t); var auth = Auth(t);
        var second = Login(auth); auth.Logout(first);
        Assert.False(auth.CheckSession(first).Ok);
        Assert.True(auth.CheckSession(second).Ok);
        Assert.True(auth.CheckSession(Login(auth)).Ok);
    }

    [Fact]
    public void SelfPasswordResetRevokesEveryExistingLoginBeforeNextServiceAction()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var auth = Auth(t);
        var second = Login(auth);
        Assert.True(auth.SetSecret(owner, owner.Id, "replacement", isPassword: true).Ok);
        Assert.False(auth.CheckSession(owner).Ok); Assert.False(auth.CheckSession(second).Ok);
        Assert.False(Sales(t).CheckPostingAccess(owner).Ok);
        Assert.False(auth.Login("admin", "area11").Ok);
        Assert.True(auth.CheckSession(Login(auth, "admin", "replacement")).Ok);
    }

    [Fact]
    public void ResetThroughAnotherConnectionInvalidatesExistingLocalLogin()
    {
        using var t = new TestDatabase(); var owner = Enable(t);
        var other = new Database(t.Database.FilePath, pooling: false, durable: false);
        other.Sessions.Enable();
        var audit = new AuditService(other, t.Log);
        var permissions = new PermissionService(other, t.Log, audit);
        var settings = new SettingsService(other, audit, t.Log, permissions);
        var auth = new AuthService(other, audit, settings, t.Log);
        var otherOwner = Login(auth);
        Assert.True(auth.SetSecret(otherOwner, otherOwner.Id, "remote-change", isPassword: true).Ok);
        Assert.False(Auth(t).CheckSession(owner).Ok);
        Assert.True(Auth(t).CheckSession(Login(Auth(t), "admin", "remote-change")).Ok);
    }

    [Fact]
    public void PinResetRejectsOldStaffSessionButNotOwner()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var auth = Auth(t);
        var created = auth.CreateStaff(owner, "Session cashier", UserRole.Cashier, "1234");
        Assert.True(created.Ok, created.Error); var staff = Login(auth, "Session cashier", "1234");
        Assert.True(auth.SetSecret(owner, created.Value, "5678", isPassword: false).Ok);
        Assert.False(Sales(t).CheckPostingAccess(staff).Ok);
        Assert.True(auth.CheckSession(owner).Ok);
        Assert.True(auth.CheckSession(Login(auth, "Session cashier", "5678")).Ok);
    }

    [Fact]
    public void PasswordResetAlsoRejectsAlreadyVerifiedCloseProof()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var auth = Auth(t);
        var close = auth.VerifySecret("admin", "area11"); Assert.True(close.Ok, close.Error);
        Assert.True(auth.SetSecret(owner, owner.Id, "replacement", isPassword: true).Ok);
        Assert.False(TestData.Permissions(t).Can(close.Value, Modules.CloseApp));
    }

    [Fact]
    public void DeactivationAndRoleChangeRejectTheAuthenticatedSnapshot()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var auth = Auth(t);
        var created = auth.CreateStaff(owner, "Changing staff", UserRole.Cashier, "1234");
        Assert.True(created.Ok); var staff = Login(auth, "Changing staff", "1234");
        Assert.True(auth.SetActive(owner, created.Value, false).Ok);
        Assert.False(auth.CheckSession(staff).Ok);
        Assert.True(auth.SetActive(owner, created.Value, true).Ok);
        Assert.False(auth.CheckSession(staff).Ok); // observed revocation is not undone by reactivation
        var current = Login(auth, "Changing staff", "1234");
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET role='manager' WHERE id=@id;", new { id = created.Value }));
        Assert.False(auth.CheckSession(current).Ok);
    }

    [Fact]
    public void PermissionRevokeAndRegrantDoNotRequireFakeLoginOrCachedGrant()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var auth = Auth(t);
        var created = auth.CreateStaff(owner, "Permission staff", UserRole.Cashier, "1234");
        Assert.True(created.Ok); var staff = Login(auth, "Permission staff", "1234");
        var permissions = TestData.Permissions(t);
        Assert.True(Sales(t).CheckPostingAccess(staff).Ok);
        Assert.True(permissions.Set(owner, staff.Id, Modules.Pos, false).Ok);
        Assert.False(Sales(t).CheckPostingAccess(staff).Ok);
        Assert.True(auth.CheckSession(staff).Ok);
        Assert.True(permissions.Set(owner, staff.Id, Modules.Pos, true).Ok);
        Assert.True(Sales(t).CheckPostingAccess(staff).Ok);
    }

    [Fact]
    public void MutatingAuthenticatedUserIdOrRoleDoesNotTransferTheProof()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var auth = Auth(t);
        owner.Id += 100; Assert.False(auth.CheckSession(owner).Ok);
        owner.Id -= 100; owner.Role = "cashier"; Assert.False(auth.CheckSession(owner).Ok);
    }

    [Fact]
    public void ReadFailureDoesNotBecomeOwnerPermission()
    {
        using var t = new TestDatabase(); var owner = Enable(t);
        t.Database.Tx(c => t.Database.Execute(c, "ALTER TABLE users RENAME TO unavailable_users;"));
        Assert.False(Auth(t).CheckSession(owner).Ok);
        Assert.False(TestData.Permissions(t).Can(owner, Modules.Pos));
    }

    [Fact]
    public void RecoveryIssuesAnInteractiveProofWithoutAuthenticatingACopiedOwner()
    {
        using var t = new TestDatabase(); Enable(t);
        var recovered = Auth(t).RecoverOwner("area11"); Assert.True(recovered.Ok, recovered.Error);
        var user = Assert.IsType<User>(recovered.Value);
        Assert.True(Auth(t).CheckSession(user).Ok);
        Assert.False(Auth(t).CheckSession(Copy(user)).Ok);
    }

    [Fact]
    public void OwnerMayInspectTargetsWithoutMintingTheirSession()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var auth = Auth(t);
        var created = auth.CreateStaff(owner, "Target", UserRole.Cashier, "1234"); Assert.True(created.Ok);
        var target = new User { Id = created.Value, Name = "Target", Role = "cashier" };
        Assert.True(TestData.Permissions(t).MapFor(owner, target)[Modules.Pos]);
        Assert.All(TestData.Permissions(t).MapFor(target), pair => Assert.False(pair.Value));
        var pricing = new PricingAllowanceService(t.Database, t.Audit, t.Log);
        Assert.Equal(target.Id, pricing.ForUser(owner, target).UserId);
        Assert.False(auth.CheckSession(target).Ok);
        Assert.Throws<ValidationException>(() => pricing.ForUser(target));
    }

    [Fact]
    public void PickerListsRequireProofButPreserveCounterParentAccess()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var auth = Auth(t);
        var created = auth.CreateStaff(owner, "Counter only", UserRole.Cashier, "1234"); Assert.True(created.Ok);
        var staff = Login(auth, "Counter only", "1234");
        Assert.True(TestData.Permissions(t).Set(owner, staff.Id, Modules.Customers, false).Ok);
        var partners = TestData.Partners(t);
        Assert.True(partners.GetCustomers(actor: staff).Ok);
        Assert.False(partners.GetCustomers().Ok);
        Assert.False(partners.GetCustomers(actor: Copy(staff)).Ok);
        Assert.True(TestData.Permissions(t).Set(owner, staff.Id, Modules.Pos, false).Ok);
        Assert.False(partners.GetCustomers(actor: staff).Ok);
    }

    [Fact]
    public void SupplierPickerPreservesProductAndPurchaseWorkflowAccess()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var auth = Auth(t);
        var created = auth.CreateStaff(owner, "Product staff", UserRole.Manager, "1234"); Assert.True(created.Ok);
        var staff = Login(auth, "Product staff", "1234"); var permissions = TestData.Permissions(t);
        Assert.True(permissions.Set(owner, staff.Id, Modules.Suppliers, false).Ok);
        Assert.True(permissions.Set(owner, staff.Id, Modules.Products, true).Ok);
        Assert.True(permissions.Set(owner, staff.Id, Modules.Purchases, false).Ok);
        Assert.True(TestData.Partners(t).GetSuppliers(staff).Ok);
        Assert.True(permissions.Set(owner, staff.Id, Modules.Products, false).Ok);
        Assert.False(TestData.Partners(t).GetSuppliers(staff).Ok);
        Assert.True(permissions.Set(owner, staff.Id, Modules.Purchases, true).Ok);
        Assert.True(TestData.Partners(t).GetSuppliers(staff).Ok);
        Assert.False(TestData.Partners(t).GetSuppliers().Ok);
    }

    [Fact]
    public void CustomFieldsAndCounterRefuseAStaleSessionAfterPasswordReset()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var auth = Auth(t);
        Assert.True(Fields(t).List(owner, CustomFieldEntity.Sale).Ok);
        Assert.True(auth.SetSecret(owner, owner.Id, "replacement", isPassword: true).Ok);
        Assert.False(Fields(t).List(owner, CustomFieldEntity.Sale).Ok);
        Assert.False(Fields(t).CheckValueAccess(owner, CustomFieldEntity.Sale).Ok);
        Assert.False(Sales(t).CheckPostingAccess(owner).Ok);
        Assert.True(Fields(t).List(Login(auth, "admin", "replacement"), CustomFieldEntity.Sale).Ok);
    }

    [Fact]
    public void ExistingSavedCustomerSurvivesSessionRevocation()
    {
        using var t = new TestDatabase(); var owner = Enable(t); var id = TestData.AddCustomer(t);
        Auth(t).Logout(owner);
        Assert.False(TestData.Partners(t).GetCustomers(actor: owner).Ok);
        var current = Login(Auth(t)); var rows = TestData.Partners(t).GetCustomers(actor: current);
        Assert.True(rows.Ok, rows.Error); Assert.Contains(rows.Value!, row => row.Id == id);
    }
}
