using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The "forgot password" door (Phase 2, owner's word): the recovery word signs the owner straight
/// in. Until the owner sets their own word, the factory word (area11, case and spaces forgiven)
/// opens it; every recovery - and every failed guess - reaches the black box.
///
/// Throttle note: the "(recovery)" gate is shared across this run, so every test below ends with
/// a successful recovery, which clears the gate for the next test. The lock itself shares the
/// sign-in machinery pinned by `GuessingAtThePrompt_IsSlowed`.
/// </summary>
public sealed class AuthRecoveryTests
{
    private static readonly User Owner = new() { Id = 1, Name = "Owner", Role = "owner" };

    private static AuthService Auth(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings, test.Log);

    private static int CreateOwner(AuthService auth)
    {
        var created = auth.CreateStaff(Owner, "Owner", UserRole.Owner, "1234");
        Assert.True(created.Ok, created.Error);

        return created.Value;
    }

    [Fact]
    public void FactoryWordSignsOwnerIn()
    {
        using var test = new TestDatabase();
        var auth = Auth(test);
        CreateOwner(auth);

        var recovered = auth.RecoverOwner("area11");

        Assert.True(recovered.Ok, recovered.Error);
        Assert.True(recovered.Value!.IsOwner);
        Assert.Equal(1, Audits(test, "owner_recovered"));
        Assert.Equal("factory word", Detail(test, "owner_recovered"));
    }

    [Fact]
    public void FactoryWordForgivesCaseAndSpaces()
    {
        using var test = new TestDatabase();
        var auth = Auth(test);
        CreateOwner(auth);

        Assert.True(auth.RecoverOwner("  Area 11 ").Ok);
    }

    [Fact]
    public void WrongWordsAreRefusedAndLeaveThePasswordAlone()
    {
        using var test = new TestDatabase();
        var auth = Auth(test);
        CreateOwner(auth);

        // Four wrong guesses: each refused, each recorded, no premature lock.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var refused = auth.RecoverOwner("wrong-word");
            Assert.False(refused.Ok);
            Assert.Contains("not correct", refused.Error!, StringComparison.Ordinal);
        }

        Assert.Equal(4, Audits(test, "owner_recover_failed"));

        // The ordinary password never noticed: PIN sign-in and the true word both still work,
        // and the success clears the shared gate for the next test.
        Assert.True(auth.Login("Owner", "1234").Ok);
        Assert.True(auth.RecoverOwner("area11").Ok);
    }

    [Fact]
    public void OwnerSetsOwnWord_FactoryStopsAndOthersAreRefused()
    {
        using var test = new TestDatabase();
        var auth = Auth(test);
        CreateOwner(auth);
        var created = auth.CreateStaff(Owner, "Bilal", UserRole.Cashier, "1234");
        Assert.True(created.Ok, created.Error);
        var staff = new User { Id = created.Value, Name = "Bilal", Role = "cashier" };

        Assert.False(auth.SetRecoveryWord(staff, "my-own-word").Ok);
        Assert.False(auth.SetRecoveryWord(Owner, "abc").Ok);
        Assert.True(auth.SetRecoveryWord(Owner, "my-own-word").Ok, "own word saves");

        Assert.False(auth.RecoverOwner("area11").Ok);
        Assert.True(auth.RecoverOwner("my-own-word").Ok);
        Assert.Equal("own word", Detail(test, "owner_recovered"));
        Assert.Equal(1, Audits(test, "recovery_word_changed"));
        Assert.Equal("changed", Detail(test, "recovery_word_changed"));

        // The black box carries neither the word nor its hash.
        using var connection = test.Database.Open();
        var text = Convert.ToString(test.Database.Scalar(connection,
            "SELECT GROUP_CONCAT(COALESCE(details,''), ' ') FROM audit_logs WHERE action = 'recovery_word_changed';"));
        Assert.DoesNotContain("my-own-word", text ?? string.Empty);
    }

    [Fact]
    public void EmptyWordHandsTheDoorBackToFactory()
    {
        using var test = new TestDatabase();
        var auth = Auth(test);
        CreateOwner(auth);

        Assert.True(auth.SetRecoveryWord(Owner, "my-own-word").Ok);
        Assert.True(auth.SetRecoveryWord(Owner, "   ").Ok);
        Assert.True(auth.RecoverOwner("area11").Ok);
        Assert.Equal("back to the factory word", Detail(test, "recovery_word_changed"));
    }

    private static long Audits(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action }) ?? 0L);
    }

    private static string Detail(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return Convert.ToString(test.Database.Scalar(connection,
            "SELECT details FROM audit_logs WHERE action = @action ORDER BY id DESC LIMIT 1;",
            new { action })) ?? string.Empty;
    }
}
