using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Owner's order (19 Oct 2026): the owner decides per person what is visible.</summary>
public sealed class PermissionTests
{
    [Fact]
    public void Owner_HasEverything()
    {
        foreach (var module in Modules.All)
        {
            Assert.True(PermissionService.RoleDefault(UserRole.Owner, module), module);
        }
    }

    [Fact]
    public void Manager_DoesNotSeeProfitAuditBackupOrStaffByDefault()
    {
        Assert.True(PermissionService.RoleDefault(UserRole.Manager, Modules.Pos));
        Assert.True(PermissionService.RoleDefault(UserRole.Manager, Modules.Reports));
        Assert.False(PermissionService.RoleDefault(UserRole.Manager, Modules.Profit));
        Assert.False(PermissionService.RoleDefault(UserRole.Manager, Modules.Audit));
        Assert.False(PermissionService.RoleDefault(UserRole.Manager, Modules.Backup));
        Assert.False(PermissionService.RoleDefault(UserRole.Manager, Modules.Staff));
    }

    [Fact]
    public void Cashier_IsLimitedToTheCounter()
    {
        Assert.True(PermissionService.RoleDefault(UserRole.Cashier, Modules.Pos));
        Assert.False(PermissionService.RoleDefault(UserRole.Cashier, Modules.Reports));
        Assert.False(PermissionService.RoleDefault(UserRole.Cashier, Modules.Products));
    }

    [Fact]
    public void StoredPermission_OverridesTheRoleDefault()
    {
        using var test = new TestDatabase();
        var permissions = new PermissionService(test.Database, test.Log, test.Audit);

        var owner = new User { Id = 1, Name = "Owner", Role = "owner" };

        var auth = new AuthService(test.Database, test.Audit, test.Settings, test.Log);
        var created = auth.CreateStaff(owner, "Bilal", UserRole.Cashier, "1234");
        Assert.True(created.Ok, created.Error);

        var staff = new User { Id = created.Value, Name = "Bilal", Role = "cashier" };

        Assert.False(permissions.Can(staff, Modules.Reports));

        var set = permissions.Set(owner, staff.Id, Modules.Reports, true);
        Assert.True(set.Ok, set.Error);

        Assert.True(permissions.Can(staff, Modules.Reports));

        var reset = permissions.ResetToRoleDefaults(owner, staff.Id);
        Assert.True(reset.Ok, reset.Error);
        Assert.False(permissions.Can(staff, Modules.Reports));
    }

    [Fact]
    public void OnlyTheOwnerCanChangePermissions()
    {
        using var test = new TestDatabase();

        var manager = new User { Id = 3, Name = "Manager", Role = "manager" };
        var permissions = new PermissionService(test.Database, test.Log, test.Audit);

        var result = permissions.Set(manager, 2, Modules.Profit, true);

        Assert.False(result.Ok);
    }
}
