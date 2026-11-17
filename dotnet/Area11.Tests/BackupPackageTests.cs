using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

public sealed class BackupPackageTests
{
    private const string Password = TestBackupPasswordStore.Password;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1048576)]
    [InlineData(1048577)]
    [InlineData(2097175)]
    public void RoundTrip_AtChunkBoundaries(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        var encoded = Encode(bytes);
        using var output = new MemoryStream();
        BackupPackage.Decrypt(new MemoryStream(encoded), output, Password);
        Assert.Equal(bytes, output.ToArray());
        var chunks = ((long)length + BackupPackage.ChunkBytes - 1) / BackupPackage.ChunkBytes;
        Assert.Equal(40 + length + chunks * 16 + 48, encoded.LongLength);
    }

    [Fact]
    public void RandomSaltAndNonce_ProduceDifferentPackages()
    {
        var bytes = Encoding.UTF8.GetBytes("synthetic private database and photo");
        Assert.NotEqual(Convert.ToHexString(Encode(bytes)), Convert.ToHexString(Encode(bytes)));
    }

    [Theory]
    [InlineData(0)] // magic / version
    [InlineData(8)] // salt
    [InlineData(24)] // nonce prefix
    [InlineData(32)] // declared size
    [InlineData(40)] // ciphertext
    [InlineData(57)] // first tag (17 byte payload)
    [InlineData(73)] // encrypted final digest
    [InlineData(105)] // final tag
    public void EveryFormatRegion_IsAuthenticatedOrRejected(int offset)
    {
        var encoded = Encode(new byte[17]);
        encoded[offset] ^= 1;
        AssertRejected(encoded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(39)]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(120)]
    public void Truncation_IsRejected(int length)
    {
        var encoded = Encode(new byte[17]);
        AssertRejected(encoded[..length]);
    }

    [Fact]
    public void TrailingBytes_AreRejected()
    {
        var encoded = Encode(new byte[17]).Concat(new byte[] { 0 }).ToArray();
        AssertRejected(encoded);
    }

    [Fact]
    public void WrongPassword_IsRejected()
    {
        var encoded = Encode(new byte[17]);
        using var output = new MemoryStream();
        Assert.ThrowsAny<CryptographicException>(() => BackupPackage.Decrypt(
            new MemoryStream(encoded), output, "another-synthetic-password"));
    }

    [Fact]
    public void ReorderedFullChunks_AreRejected()
    {
        var bytes = new byte[BackupPackage.ChunkBytes * 2];
        RandomNumberGenerator.Fill(bytes);
        var encoded = Encode(bytes);
        var recordLength = BackupPackage.ChunkBytes + 16;
        var first = encoded.AsSpan(40, recordLength).ToArray();
        encoded.AsSpan(40 + recordLength, recordLength).CopyTo(encoded.AsSpan(40, recordLength));
        first.CopyTo(encoded, 40 + recordLength);
        AssertRejected(encoded);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(34359738369L)]
    [InlineData(long.MaxValue)]
    public void InvalidLength_IsRejectedBeforePayloadAllocation(long length)
    {
        var encoded = Encode(Array.Empty<byte>());
        BinaryPrimitives.WriteInt64LittleEndian(encoded.AsSpan(32, 8), length);
        AssertRejected(encoded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(1025)]
    public void PasswordLength_IsBounded(int length) =>
        Assert.Throws<ArgumentException>(() => BackupPackage.ValidatePassword(new string('x', length)));

    [Fact]
    public void IndependentPythonVector_Decrypts()
    {
        // Produced with tools/check_backup_package_reference.py / cryptography AESGCM,
        // not the C# writer. Public synthetic salt, nonce and password; never a shop credential.
        var encoded = Convert.FromHexString(ReferenceVector);
        using var output = new MemoryStream();
        BackupPackage.Decrypt(new MemoryStream(encoded), output, Password);
        Assert.Equal("independent synthetic backup vector", Encoding.UTF8.GetString(output.ToArray()));
    }

    internal const string ReferenceVector = "41313142414B3031000102030405060708090A0B0C0D0E0F1011121314151617230000000000000068913F91385C74FDE7C47F09A85CAB27B394C053A08F7B5ED3D1659052657F08B94C2202648D1C064DDF916953BF20FF81B05AAD4BE27DA0110E789AA9ABAEC1A3FA43F7141BA877478684915E11E9812FB661234D8E63F15852A8AC0CFCD940CABF89";

    private static byte[] Encode(byte[] bytes)
    {
        using var output = new MemoryStream();
        BackupPackage.Encrypt(new MemoryStream(bytes), output, Password);
        return output.ToArray();
    }

    private static void AssertRejected(byte[] encoded)
    {
        using var output = new MemoryStream();
        Assert.ThrowsAny<Exception>(() => BackupPackage.Decrypt(new MemoryStream(encoded), output, Password));
        // Failed output is staging ONLY: caller must discard it even if earlier chunks authenticated.
    }
}
