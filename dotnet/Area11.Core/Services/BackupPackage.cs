using System.IO;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Area11.Core.Services;

/// <summary>
/// Version1 bounded chunked AES-256-GCM container. Header+record index are AAD; final encrypted
/// SHA-256 authenticates completion. Decrypt ONLY to private staging: no consumer may use output
/// until this method succeeds. No compression/cipher implementation of our own; uses platform AES.
/// </summary>
public static class BackupPackage
{
    public const string Extension = ".a11bak";
    public const int ChunkBytes = 1024 * 1024;
    public const int HeaderBytes = 40;
    public const int Iterations = 600000;
    public const long MaxPayloadBytes = 32L * 1024 * 1024 * 1024;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("A11BAK01");

    public static void ValidatePassword(string password)
    {
        if (password is null || password.Length < 12 || password.Length > 1024)
            throw new ArgumentException("Use a backup password of 12 to 1024 characters.");
    }

    public static void Encrypt(Stream source, Stream destination, string password)
    {
        ValidatePassword(password);
        var length = source.Length - source.Position;
        if (length < 0 || length > MaxPayloadBytes) throw new InvalidDataException("Backup payload exceeds the supported limit.");
        var header = new byte[HeaderBytes];
        Magic.CopyTo(header, 0);
        RandomNumberGenerator.Fill(header.AsSpan(8, 24)); // independent salt16 + nonce prefix8
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(32, 8), length);
        destination.Write(header);
        Transform(source, destination, password, header, encrypt: true);
    }

    public static void Decrypt(Stream source, Stream destination, string password)
    {
        ValidatePassword(password);
        var header = new byte[HeaderBytes];
        source.ReadExactly(header);
        if (!header.AsSpan(0, 8).SequenceEqual(Magic)) throw new InvalidDataException("Unsupported encrypted backup format.");
        var length = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(32, 8));
        if (length < 0 || length > MaxPayloadBytes) throw new InvalidDataException("Invalid backup length.");
        var chunks = (length + ChunkBytes - 1) / ChunkBytes;
        var encodedLength = HeaderBytes + length + chunks * 16 + 32 + 16;
        if (!source.CanSeek || source.Length - source.Position != encodedLength - HeaderBytes)
            throw new InvalidDataException("Truncated or extended encrypted backup.");
        Transform(source, destination, password, header, encrypt: false);
        if (source.ReadByte() != -1) throw new InvalidDataException("Unexpected backup trailing data.");
    }

    private static void Transform(Stream source, Stream destination, string password, byte[] header, bool encrypt)
    {
        var key = Rfc2898DeriveBytes.Pbkdf2(password, header.AsSpan(8, 16), Iterations, HashAlgorithmName.SHA256, 32);
        var plain = new byte[ChunkBytes];
        var cipher = new byte[ChunkBytes];
        var tag = new byte[16];
        var nonce = new byte[12];
        header.AsSpan(24, 8).CopyTo(nonce);
        var aad = new byte[HeaderBytes + 4];
        header.CopyTo(aad, 0);
        var remaining = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(32, 8));
        uint index = 0;
        try
        {
            using var aes = new AesGcm(key, 16);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (remaining > 0)
            {
                var take = (int)Math.Min(remaining, ChunkBytes);
                BinaryPrimitives.WriteUInt32LittleEndian(nonce.AsSpan(8), index);
                BinaryPrimitives.WriteUInt32LittleEndian(aad.AsSpan(HeaderBytes), index);
                if (encrypt)
                {
                    source.ReadExactly(plain.AsSpan(0, take));
                    hash.AppendData(plain.AsSpan(0, take));
                    aes.Encrypt(nonce, plain.AsSpan(0, take), cipher.AsSpan(0, take), tag, aad);
                    destination.Write(cipher.AsSpan(0, take));
                    destination.Write(tag);
                }
                else
                {
                    source.ReadExactly(cipher.AsSpan(0, take));
                    source.ReadExactly(tag);
                    aes.Decrypt(nonce, cipher.AsSpan(0, take), tag, plain.AsSpan(0, take), aad);
                    hash.AppendData(plain.AsSpan(0, take));
                    destination.Write(plain.AsSpan(0, take));
                }
                remaining -= take;
                index++;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(nonce.AsSpan(8), index);
            BinaryPrimitives.WriteUInt32LittleEndian(aad.AsSpan(HeaderBytes), index);
            var digest = hash.GetHashAndReset();
            try
            {
                if (encrypt)
                {
                    aes.Encrypt(nonce, digest, cipher.AsSpan(0, 32), tag, aad);
                    destination.Write(cipher.AsSpan(0, 32));
                    destination.Write(tag);
                    if (source.ReadByte() != -1) throw new InvalidDataException("Backup source changed during packaging.");
                }
                else
                {
                    source.ReadExactly(cipher.AsSpan(0, 32));
                    source.ReadExactly(tag);
                    aes.Decrypt(nonce, cipher.AsSpan(0, 32), tag, plain.AsSpan(0, 32), aad);
                    if (!CryptographicOperations.FixedTimeEquals(digest, plain.AsSpan(0, 32)))
                        throw new CryptographicException("Backup payload checksum mismatch.");
                }
            }
            finally { CryptographicOperations.ZeroMemory(digest); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(cipher);
        }
    }
}
