using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hash = PasswordHasher.Hash("area11");

        Assert.True(PasswordHasher.Verify("area11", hash));
    }

    [Fact]
    public void Verify_RejectsWrongSecret()
    {
        var hash = PasswordHasher.Hash("area11");

        Assert.False(PasswordHasher.Verify("area12", hash));
    }

    [Fact]
    public void Hash_IsSalted_SoTwoHashesDiffer()
    {
        var first = PasswordHasher.Hash("1234");
        var second = PasswordHasher.Hash("1234");

        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify("1234", first));
        Assert.True(PasswordHasher.Verify("1234", second));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("pbkdf2-sha256$1000$bad$bad")]
    public void Verify_RejectsMissingOrBrokenHashes(string? stored)
    {
        Assert.False(PasswordHasher.Verify("area11", stored));
    }
}
