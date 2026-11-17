using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Phase4 portion1: targeted owner-write boundaries. Not a whole-program security test.</summary>
public sealed class OwnerBoundaryReviewTests
{
    private static AuthService Auth(TestDatabase t) => new(t.Database, t.Audit, t.Settings, t.Log);
    private static PermissionService Permissions(TestDatabase t) => TestData.Permissions(t);
    private static User Forged(TestDatabase t)
    {
        var person = TestData.Cashier(t);
        return new User { Id = person.Id, Name = "not-the-owner", Role = "owner" };
    }
    private static long Count(TestDatabase t, string sql)
    {
        using var c = t.Database.Open();
        return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static void DisableOwner(TestDatabase t) => t.Database.Tx(c =>
    {
        t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;");
    });

    [Fact]
    public void ForgedOwnerCannotGrantAnyModule()
    {
        using var t = new TestDatabase(); var actor = Forged(t); var target = TestData.Cashier(t);
        Assert.False(Permissions(t).Set(actor, target.Id, Modules.Reports, true).Ok);
        Assert.Equal(0L, Count(t, "SELECT COUNT(*) FROM user_permissions;"));
        Assert.Equal(0L, Count(t, "SELECT COUNT(*) FROM audit_logs WHERE action='permission_change';"));
    }

    [Fact]
    public void ForgedOwnerCannotResetSomeoneElsesDenial()
    {
        using var t = new TestDatabase(); var actor = Forged(t); var target = TestData.AddUser(t, "manager");
        Assert.True(Permissions(t).Set(TestData.Owner, target.Id, Modules.Pos, false).Ok);
        Assert.False(Permissions(t).ResetToRoleDefaults(actor, target.Id).Ok);
        Assert.False(Permissions(t).Can(target, Modules.Pos));
    }

    [Fact]
    public void InactiveOwnerCannotChangePermissionsOrStaff()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); DisableOwner(t);
        Assert.False(Permissions(t).Set(TestData.Owner, target.Id, Modules.Reports, true).Ok);
        Assert.False(Permissions(t).ResetToRoleDefaults(TestData.Owner, target.Id).Ok);
        Assert.False(Auth(t).CreateStaff(TestData.Owner, "Blocked person", UserRole.Cashier, "1234").Ok);
        Assert.False(Auth(t).SetSecret(TestData.Owner, target.Id, "9876", false).Ok);
        Assert.False(Auth(t).SetActive(TestData.Owner, target.Id, false).Ok);
    }

    [Fact]
    public void DemotedOwnerSnapshotCannotMakeOwnerWrites()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t);
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET role='manager' WHERE id=1;"); });
        Assert.False(Permissions(t).Set(TestData.Owner, target.Id, Modules.Profit, true).Ok);
        Assert.False(Auth(t).SetActive(TestData.Owner, target.Id, false).Ok);
    }

    [Fact]
    public void RealOwnerCanStillGrantAndResetStaffModules()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var permissions = Permissions(t);
        Assert.True(permissions.Set(TestData.Owner, target.Id, Modules.Reports, true).Ok);
        Assert.True(permissions.Can(target, Modules.Reports));
        Assert.True(permissions.ResetToRoleDefaults(TestData.Owner, target.Id).Ok);
        Assert.False(permissions.Can(target, Modules.Reports));
    }

    [Fact]
    public void UnknownTargetsAreNotReportedAsSuccessfulChanges()
    {
        using var t = new TestDatabase();
        Assert.False(Auth(t).SetSecret(TestData.Owner, int.MaxValue, "9876", false).Ok);
        Assert.False(Auth(t).SetActive(TestData.Owner, int.MaxValue, false).Ok);
        Assert.False(Permissions(t).ResetToRoleDefaults(TestData.Owner, int.MaxValue).Ok);
        Assert.Equal(0L, Count(t, "SELECT COUNT(*) FROM audit_logs WHERE action IN ('secret_change','activate','deactivate','permission_reset');"));
    }

    [Fact]
    public void OwnerCannotBeDisabledIncludingAnotherOwnerAccount()
    {
        using var t = new TestDatabase(); var other = TestData.AddUser(t, "owner");
        Assert.False(Auth(t).SetActive(TestData.Owner, 1, false).Ok);
        Assert.False(Auth(t).SetActive(TestData.Owner, other.Id, false).Ok);
        Assert.Equal(2L, Count(t, "SELECT COUNT(*) FROM users WHERE role='owner' AND active=1;"));
    }

    [Fact]
    public void RealOwnerCanDisableAndReenableStaff()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t);
        Assert.True(Auth(t).SetActive(TestData.Owner, target.Id, false).Ok);
        Assert.True(Auth(t).SetActive(TestData.Owner, target.Id, true).Ok);
    }

    [Fact]
    public void ForgedOwnerCannotResetOrDisableStaff()
    {
        using var t = new TestDatabase(); var auth = Auth(t); var actor = Forged(t);
        var person = auth.CreateStaff(TestData.Owner, "Boundary clerk", UserRole.Cashier, "1234");
        Assert.True(person.Ok, person.Error);
        Assert.False(auth.SetSecret(actor, person.Value, "9876", false).Ok);
        Assert.False(auth.SetActive(actor, person.Value, false).Ok);
        Assert.True(auth.Login("Boundary clerk", "1234").Ok);
    }

    [Fact]
    public void PasswordResetInvalidatesAnOldPinToo()
    {
        using var t = new TestDatabase(); var auth = Auth(t);
        var owner = auth.CreateStaff(TestData.Owner, "Reset owner", UserRole.Owner, "3210");
        Assert.True(owner.Ok, owner.Error);
        Assert.True(auth.SetSecret(TestData.Owner, owner.Value, "new-safe-password", true).Ok);
        Assert.False(auth.Login("Reset owner", "3210").Ok);
        Assert.True(auth.Login("Reset owner", "new-safe-password").Ok);
    }

    [Fact]
    public void PinResetInvalidatesAnOldPasswordToo()
    {
        using var t = new TestDatabase(); var auth = Auth(t);
        var clerk = auth.CreateStaff(TestData.Owner, "Reset clerk", UserRole.Cashier, "3210");
        Assert.True(auth.SetSecret(TestData.Owner, clerk.Value, "old-password", true).Ok);
        Assert.True(auth.SetSecret(TestData.Owner, clerk.Value, "9876", false).Ok);
        Assert.False(auth.Login("Reset clerk", "old-password").Ok);
        Assert.True(auth.Login("Reset clerk", "9876").Ok);
    }

    [Fact]
    public void ForgedOwnerCannotCreateAnotherOwner()
    {
        using var t = new TestDatabase();
        Assert.False(Auth(t).CreateStaff(Forged(t), "Forged new owner", UserRole.Owner, "1234").Ok);
        Assert.Equal(1L, Count(t, "SELECT COUNT(*) FROM users WHERE role='owner';"));
    }

    [Fact]
    public void RecoveryChangesRequireCurrentOwnerNotRoleText()
    {
        using var t = new TestDatabase(); var auth = Auth(t);
        Assert.False(auth.SetRecoveryWord(Forged(t), "unwanted-word").Ok);
        Assert.Equal(string.Empty, t.Settings.GetString(AuthService.RecoveryWordKey));
        Assert.Equal(0L, Count(t, "SELECT COUNT(*) FROM audit_logs WHERE action='recovery_word_changed';"));
    }

    [Fact]
    public void InactiveOwnerCannotChangeRecoveryWord()
    {
        using var t = new TestDatabase(); DisableOwner(t);
        Assert.False(Auth(t).SetRecoveryWord(TestData.Owner, "unwanted-word").Ok);
        Assert.Equal(string.Empty, t.Settings.GetString(AuthService.RecoveryWordKey));
    }

    [Fact]
    public void GenericSettingsCannotWriteRecoveryHashesOrAuditThem()
    {
        using var t = new TestDatabase();
        Assert.False(t.Settings.SetUiOnly(AuthService.RecoveryWordKey, "private-value").Ok);
        Assert.False(t.Settings.Set(AuthService.RecoveryWordKey, "private-value", TestData.Owner).Ok);
        Assert.Equal(0L, Count(t, "SELECT COUNT(*) FROM audit_logs WHERE new_value LIKE '%private-value%';"));
    }

    [Fact]
    public void MixedBulkSettingsWithRecoveryKeyAreAtomicRefusal()
    {
        using var t = new TestDatabase(); var before = t.Settings.GetString("store.name");
        Assert.False(t.Settings.SetMany(new Dictionary<string, object?>
            { ["store.name"] = "Do not write this", [AuthService.RecoveryWordKey] = "private-value" }, TestData.Owner).Ok);
        Assert.Equal(before, t.Settings.GetString("store.name"));
    }

    [Fact]
    public void RecoveryAuditFailureRollsBackHashAndCache()
    {
        using var t = new TestDatabase();
        t.Database.Tx(c =>
        {
            t.Database.Execute(c, @"
CREATE TRIGGER refuse_recovery_audit BEFORE INSERT ON audit_logs
WHEN NEW.action='recovery_word_changed'
BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END;");
        });
        Assert.False(Auth(t).SetRecoveryWord(TestData.Owner, "private-new-word").Ok);
        Assert.Equal(string.Empty, t.Settings.GetString(AuthService.RecoveryWordKey));
        t.Settings.Reload();
        Assert.Equal(string.Empty, t.Settings.GetString(AuthService.RecoveryWordKey));
        Assert.Equal(0L, Count(t, "SELECT COUNT(*) FROM audit_logs WHERE action='recovery_word_changed';"));
    }

    [Fact]
    public void ResetDoesNotEraseOwnerPermissionRows()
    {
        using var t = new TestDatabase();
        Assert.False(Permissions(t).ResetToRoleDefaults(TestData.Owner, 1).Ok);
        Assert.Equal(0L, Count(t, "SELECT COUNT(*) FROM audit_logs WHERE action='permission_reset';"));
    }

    [Fact]
    public void ProtectedOwnerSettingsRejectForgedOwnerAtomically()
    {
        using var t = new TestDatabase(); var actor = Forged(t);
        foreach (var pair in new Dictionary<string, object?>
        {
            ["brand.appName"] = "Unexpected name", ["appearance.iconFileName"] = "unexpected.ico",
            ["lockdown.focusGuard"] = true, ["discount.billBasis"] = "retail"
        })
        {
            Assert.False(t.Settings.Set(pair.Key, pair.Value, actor).Ok);
            Assert.False(t.Settings.SetUiOnly(pair.Key, pair.Value).Ok);
        }
    }

    [Fact]
    public void SecretResetAuditFailureRestoresOldCredential()
    {
        using var t = new TestDatabase(); var auth = Auth(t);
        var clerk = auth.CreateStaff(TestData.Owner, "Atomic reset clerk", UserRole.Cashier, "3210");
        t.Database.Tx(c => { t.Database.Execute(c, @"
CREATE TRIGGER refuse_secret_audit BEFORE INSERT ON audit_logs
WHEN NEW.action='secret_change'
BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END;"); });
        Assert.False(auth.SetSecret(TestData.Owner, clerk.Value, "9876", false).Ok);
        Assert.True(auth.Login("Atomic reset clerk", "3210").Ok);
    }
}
