using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The owner's panel for a forgotten secret (Phase 2, owner's word): only the owner changes a
/// PIN or password, the new secret signs in at once, the old one stops, and the black box records
/// that it changed - never the secret itself.
/// </summary>
public sealed class AuthSecretTests
{
    private static readonly User Owner = new() { Id = 1, Name = "Owner", Role = "owner" };

    private static AuthService Auth(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings, test.Log);

    [Fact]
    public void OwnerResetsStaffPin_NewPinSignsInAndOldStops()
    {
        using var test = new TestDatabase();
        var auth = Auth(test);
        var created = auth.CreateStaff(Owner, "Bilal", UserRole.Cashier, "1234");
        Assert.True(created.Ok, created.Error);

        var reset = auth.SetSecret(Owner, created.Value, "9999", isPassword: false);
        Assert.True(reset.Ok, reset.Error);

        Assert.True(auth.Login("Bilal", "9999").Ok);
        Assert.False(auth.Login("Bilal", "1234").Ok);
        Assert.Equal(1, Audits(test, "secret_change"));
    }

    [Fact]
    public void OwnerResetsOwnPassword_NewPasswordSignsIn()
    {
        using var test = new TestDatabase();
        var auth = Auth(test);
        var created = auth.CreateStaff(Owner, "Owner", UserRole.Owner, "1234");
        Assert.True(created.Ok, created.Error);

        var reset = auth.SetSecret(Owner, created.Value, "new-owner-word", isPassword: true);
        Assert.True(reset.Ok, reset.Error);

        var login = auth.Login("Owner", "new-owner-word");
        Assert.True(login.Ok, login.Error);
        Assert.True(login.Value!.IsOwner);
    }

    [Fact]
    public void NonOwnerCannotResetAndShortSecretsAreRefused()
    {
        using var test = new TestDatabase();
        var auth = Auth(test);
        var created = auth.CreateStaff(Owner, "Bilal", UserRole.Cashier, "1234");
        Assert.True(created.Ok, created.Error);
        var staff = new User { Id = created.Value, Name = "Bilal", Role = "cashier" };

        Assert.False(auth.SetSecret(staff, created.Value, "9999", isPassword: false).Ok);
        Assert.False(auth.SetSecret(Owner, created.Value, "99", isPassword: false).Ok);
        Assert.False(auth.SetSecret(Owner, 1, "abc", isPassword: true).Ok);

        // All three refusals: the old PIN still signs in, and nothing was written.
        Assert.True(auth.Login("Bilal", "1234").Ok);
        Assert.Equal(0, Audits(test, "secret_change"));
    }

    [Fact]
    public void TheBlackBoxNeverCarriesTheSecretItself()
    {
        using var test = new TestDatabase();
        var auth = Auth(test);
        var created = auth.CreateStaff(Owner, "Bilal", UserRole.Cashier, "1234");
        Assert.True(created.Ok, created.Error);

        Assert.True(auth.SetSecret(Owner, created.Value, "s3cr3t-pin", isPassword: false).Ok);

        using var connection = test.Database.Open();
        var text = Convert.ToString(test.Database.Scalar(connection,
            "SELECT GROUP_CONCAT(COALESCE(user_name,'') || COALESCE(entity_id,'') || COALESCE(details,''), ' ') FROM audit_logs WHERE action = 'secret_change';"));

        Assert.DoesNotContain("s3cr3t-pin", text ?? string.Empty);
    }

    private static long Audits(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action }) ?? 0L);
    }
}
