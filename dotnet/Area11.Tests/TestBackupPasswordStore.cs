using System.IO;
using System.IO.Compression;
using Area11.Core.Services;

namespace Area11.Tests;

/// <summary>Synthetic fixture ONLY. Production must use Windows DPAPI, never this class.</summary>
internal sealed class TestBackupPasswordStore : IBackupPasswordStore
{
    public const string Password = "synthetic-test-backup-only-2026";
    public string? Value { get; set; } = Password;
    public string? Load() => Value;
    public void Save(string password) => Value = password;

    public static ZipArchive OpenArchive(string path)
    {
        var plain = new MemoryStream();
        using (var source = File.OpenRead(path)) BackupPackage.Decrypt(source, plain, Password);
        plain.Position = 0;
        return new ZipArchive(plain, ZipArchiveMode.Read); // disposing archive also disposes the stream
    }

    public static void RewriteArchive(string path, Action<ZipArchive> mutation)
    {
        using var plain = new MemoryStream();
        using (var source = File.OpenRead(path)) BackupPackage.Decrypt(source, plain, Password);
        plain.Position = 0;
        using (var archive = new ZipArchive(plain, ZipArchiveMode.Update, leaveOpen: true)) mutation(archive);
        plain.Position = 0;
        using var output = File.Create(path);
        BackupPackage.Encrypt(plain, output, Password);
    }
}
