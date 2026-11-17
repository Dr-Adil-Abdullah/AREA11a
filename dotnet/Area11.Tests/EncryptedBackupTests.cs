using System.IO;
using System.IO.Compression;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Synthetic isolated DB only. DPAPI/Windows ACL execution remains a separate Windows gate.</summary>
public sealed class EncryptedBackupTests
{
    [Fact]
    public void MissingCredential_ManualAutomaticAndSafetyFailWithoutPlaintext()
    {
        using var fixture = new EncryptedBackupFixture();
        fixture.Store.Value = null;
        Assert.False(fixture.Service.Create(TestData.Owner).Ok);
        Assert.False(fixture.Service.SafetyCopy("test", TestData.Owner).Ok);
        Assert.Null(fixture.Service.RunAutomaticIfDue());
        Assert.NotEmpty(fixture.Service.LastFailure);
        Assert.Empty(Directory.EnumerateFiles(fixture.Backups));
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Stage));
    }

    [Fact]
    public void CredentialFailure_NeverLogsExceptionOrFallsBack()
    {
        using var fixture = new EncryptedBackupFixture();
        var service = fixture.Build(new ThrowingStore());
        var result = service.Create(TestData.Owner);
        Assert.False(result.Ok);
        Assert.DoesNotContain("SECRET", result.Error ?? string.Empty);
        var logs = Path.Combine(Path.GetDirectoryName(fixture.Test.Database.FilePath)!, "logs");
        foreach (var log in Directory.EnumerateFiles(logs)) Assert.DoesNotContain("SECRET", File.ReadAllText(log));
        Assert.Empty(Directory.EnumerateFiles(fixture.Backups));
    }

    [Fact]
    public void PasswordSetup_RequiresMatchingConfirmationAndCurrentOwner()
    {
        using var fixture = new EncryptedBackupFixture();
        fixture.Store.Value = null;
        Assert.False(fixture.Service.ConfigurePassword(TestData.Cashier(fixture.Test),
            TestBackupPasswordStore.Password, TestBackupPasswordStore.Password).Ok);
        Assert.False(fixture.Service.ConfigurePassword(TestData.Owner, TestBackupPasswordStore.Password, "different").Ok);
        Assert.False(fixture.Service.ConfigurePassword(TestData.Owner, "short", "short").Ok);
        Assert.Null(fixture.Store.Value);
        Assert.True(fixture.Service.ConfigurePassword(TestData.Owner,
            TestBackupPasswordStore.Password, TestBackupPasswordStore.Password).Ok);
        Assert.True(fixture.Service.PasswordConfigured);
        fixture.Test.Database.Tx(connection => fixture.Test.Database.Execute(connection, "UPDATE users SET active = 0 WHERE id = 1;"));
        Assert.False(fixture.Service.ConfigurePassword(TestData.Owner,
            "replacement-test-password", "replacement-test-password").Ok);
        Assert.Equal(TestBackupPasswordStore.Password, fixture.Store.Value);
    }

    [Fact]
    public void AutomaticFlag_DoesNotBypassPublicActorCheck()
    {
        using var fixture = new EncryptedBackupFixture();
        Assert.False(fixture.Service.Create(null, automatic: true).Ok);
        Assert.False(fixture.Service.Create(TestData.Cashier(fixture.Test), automatic: true).Ok);
        Assert.Empty(Directory.EnumerateFiles(fixture.Backups));
    }

    [Fact]
    public void Rotation_OldFileNeedsOriginalPassword_NewSafetyUsesLocalPassword()
    {
        using var fixture = new EncryptedBackupFixture();
        TestData.AddProduct(fixture.Test, name: "In old package");
        var made = fixture.Service.Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);
        const string replacement = "replacement-synthetic-local-password";
        Assert.True(fixture.Service.ConfigurePassword(TestData.Owner, replacement, replacement).Ok);
        TestData.AddProduct(fixture.Test, name: "Current shop");
        Assert.False(fixture.Service.Restore(made.Value!.FullPath, TestData.Owner).Ok);
        Assert.Equal(2L, fixture.CountProducts());
        var restored = fixture.Service.Restore(made.Value.FullPath, TestData.Owner, TestBackupPasswordStore.Password);
        Assert.True(restored.Ok, restored.Error);
        Assert.Equal(1L, fixture.CountProducts());
        var safety = Path.Combine(fixture.Backups, restored.Value!.SafetyCopy);
        using var output = new MemoryStream();
        using var input = File.OpenRead(safety);
        BackupPackage.Decrypt(input, output, replacement);
        Assert.NotEqual(0, output.Length);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Stage));
    }

    [Fact]
    public void OriginalPasswordOnNewCredentialStore_RestoresWithoutOriginalWindowsProfile()
    {
        using var fixture = new EncryptedBackupFixture();
        TestData.AddProduct(fixture.Test, name: "Original package");
        var made = fixture.Service.Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);
        TestData.AddProduct(fixture.Test, name: "New machine state");
        var otherStore = new TestBackupPasswordStore { Value = "new-machine-synthetic-password" };
        var otherService = fixture.Build(otherStore);
        var restored = otherService.Restore(made.Value!.FullPath, TestData.Owner, TestBackupPasswordStore.Password);
        Assert.True(restored.Ok, restored.Error);
        Assert.Equal(1L, fixture.CountProducts());
        // This simulates credential independence, NOT a genuine new-PC/DPAPI test.
    }

    [Fact]
    public void Legacy_RequiresExplicitAcceptanceAndCurrentOwner()
    {
        using var fixture = new EncryptedBackupFixture();
        TestData.AddProduct(fixture.Test);
        var made = fixture.Service.Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);
        var legacy = Path.Combine(fixture.Root, "old.db");
        using (var zip = TestBackupPasswordStore.OpenArchive(made.Value!.FullPath))
            zip.GetEntry("area11.db")!.ExtractToFile(legacy);
        Assert.False(fixture.Service.Restore(legacy, TestData.Owner).Ok);
        Assert.False(fixture.Service.RestoreLegacy(legacy, TestData.Owner, false).Ok);
        Assert.False(fixture.Service.RestoreLegacy(legacy, TestData.Cashier(fixture.Test), true).Ok);
        fixture.Test.Database.Tx(connection => fixture.Test.Database.Execute(connection, "UPDATE users SET active = 0 WHERE id = 1;"));
        Assert.False(fixture.Service.RestoreLegacy(legacy, TestData.Owner, true).Ok);
        Assert.Equal(1L, fixture.CountProducts());
    }

    [Theory]
    [InlineData("photos/../escape.jpg")]
    [InlineData("photos/CON.jpg")]
    [InlineData("photos/C:/escape.jpg")]
    [InlineData("photos/a\\..\\escape.jpg")]
    [InlineData("photos/trailing. /a.jpg")]
    [InlineData("area11.db")]
    [InlineData("unexpected.bin")]
    public void AuthenticatedButUnsafeArchive_IsRejectedBeforeSafetyOrLiveChanges(string name)
    {
        using var fixture = new EncryptedBackupFixture();
        TestData.AddProduct(fixture.Test, name: "First");
        var made = fixture.Service.Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);
        TestData.AddProduct(fixture.Test, name: "Still here");
        TestBackupPasswordStore.RewriteArchive(made.Value!.FullPath, zip =>
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write("bad entry");
        });
        Assert.False(fixture.Service.Restore(made.Value.FullPath, TestData.Owner).Ok);
        Assert.Equal(2L, fixture.CountProducts());
        Assert.DoesNotContain(fixture.Service.List(), file => file.IsSafetyCopy);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Stage));
    }

    [Fact]
    public void FailedDestination_DoesNotDeleteExistingFile_AndCleansStaging()
    {
        using var fixture = new EncryptedBackupFixture();
        var path = Path.Combine(fixture.Backups, "existing.a11bak");
        File.WriteAllText(path, "keep older backup");
        fixture.Store.Value = null;
        Assert.False(fixture.Service.Create(TestData.Owner, destinationFile: path).Ok);
        Assert.Equal("keep older backup", File.ReadAllText(path));
        Assert.Single(Directory.EnumerateFiles(fixture.Backups));
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Stage));
    }

    [Fact]
    public void Retention_PreservesLegacyAutomaticAndForeignFiles()
    {
        using var fixture = new EncryptedBackupFixture();
        var legacy = Path.Combine(fixture.Backups, "area11-backup-20200101-000000-000-auto.zip");
        var foreign = Path.Combine(fixture.Backups, "other.a11bak");
        File.WriteAllText(legacy, "legacy evidence");
        File.WriteAllText(foreign, "foreign evidence");
        Assert.True(fixture.Test.Settings.Set("backup.keep", 1, TestData.Owner).Ok);
        Assert.True(fixture.Service.Create(TestData.Owner, automatic: true).Ok);
        var newest = fixture.Service.Create(TestData.Owner, automatic: true);
        Assert.True(newest.Ok, newest.Error);
        Assert.True(File.Exists(newest.Value!.FullPath));
        Assert.Equal("legacy evidence", File.ReadAllText(legacy));
        Assert.Equal("foreign evidence", File.ReadAllText(foreign));
        Assert.Single(fixture.Service.List(), file => file.IsAutomatic && file.IsEncrypted);
        Assert.Contains(fixture.Service.List(), file => !file.IsEncrypted && file.ProtectionLabel.Contains("unverified"));
    }

    [Fact]
    public void Password_IsAbsentFromSettingsAuditAndPackagePlaintext()
    {
        using var fixture = new EncryptedBackupFixture();
        Assert.True(fixture.Service.ConfigurePassword(TestData.Owner,
            TestBackupPasswordStore.Password, TestBackupPasswordStore.Password).Ok);
        var made = fixture.Service.Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);
        var persisted = fixture.Test.Database.Tx(connection =>
            string.Join(" ", fixture.Test.Database.Query<string>(connection, "SELECT value FROM settings;"))
            + string.Join(" ", fixture.Test.Database.Query<string>(connection, "SELECT COALESCE(details,'') FROM audit_logs;")));
        Assert.DoesNotContain(TestBackupPasswordStore.Password, persisted);
        Assert.DoesNotContain(TestBackupPasswordStore.Password, System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(made.Value!.FullPath)));
    }

    [Fact]
    public void ValidPackage_StillRequiresLocalSafetyPasswordBeforeRestore()
    {
        using var fixture = new EncryptedBackupFixture();
        TestData.AddProduct(fixture.Test);
        var made = fixture.Service.Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);
        TestData.AddProduct(fixture.Test, name: "Still live");
        fixture.Store.Value = null;
        Assert.False(fixture.Service.Restore(made.Value!.FullPath, TestData.Owner, TestBackupPasswordStore.Password).Ok);
        Assert.Equal(2L, fixture.CountProducts());
        Assert.DoesNotContain(fixture.Service.List(), file => file.IsSafetyCopy);
    }

    [Fact]
    public void ExplicitOwnerLegacyZip_StillValidatesDatabaseAndMakesEncryptedSafety()
    {
        using var fixture = new EncryptedBackupFixture();
        TestData.AddProduct(fixture.Test);
        var made = fixture.Service.Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);
        var legacy = Path.Combine(fixture.Root, "legacy.zip");
        using (var input = File.OpenRead(made.Value!.FullPath))
        using (var output = File.Create(legacy))
            BackupPackage.Decrypt(input, output, TestBackupPasswordStore.Password);
        TestData.AddProduct(fixture.Test, name: "Current data");
        var restored = fixture.Service.RestoreLegacy(legacy, TestData.Owner, true);
        Assert.True(restored.Ok, restored.Error);
        Assert.Equal(1L, fixture.CountProducts());
        var safety = Assert.Single(fixture.Service.List(), file => file.IsSafetyCopy);
        Assert.True(safety.IsEncrypted);
        Assert.True(File.Exists(legacy));
    }

    [Fact]
    public void OversizedManifest_IsRejectedBeforeLiveReplacement()
    {
        using var fixture = new EncryptedBackupFixture();
        var made = fixture.Service.Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);
        TestBackupPasswordStore.RewriteArchive(made.Value!.FullPath, zip =>
        {
            zip.GetEntry("area11-backup.txt")!.Delete();
            using var output = zip.CreateEntry("area11-backup.txt").Open();
            output.Write(new byte[65537]);
        });
        Assert.False(fixture.Service.Restore(made.Value.FullPath, TestData.Owner).Ok);
        Assert.DoesNotContain(fixture.Service.List(), file => file.IsSafetyCopy);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Stage));
    }

    [Fact]
    public void ExcessiveDirectoryCount_IsRejectedBeforeZipEntryAllocation()
    {
        using var fixture = new EncryptedBackupFixture();
        var made = fixture.Service.Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);
        using var plain = new MemoryStream();
        using (var input = File.OpenRead(made.Value!.FullPath))
            BackupPackage.Decrypt(input, plain, TestBackupPasswordStore.Password);
        var payload = plain.ToArray();
        // Our writer emits a comment-free EOCD. Forge both count fields; keep valid encryption.
        payload[^14] = 0xff; payload[^13] = 0xff;
        payload[^12] = 0xff; payload[^11] = 0xff;
        using (var output = File.Create(made.Value.FullPath))
            BackupPackage.Encrypt(new MemoryStream(payload), output, TestBackupPasswordStore.Password);
        Assert.False(fixture.Service.Restore(made.Value.FullPath, TestData.Owner).Ok);
        Assert.DoesNotContain(fixture.Service.List(), file => file.IsSafetyCopy);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Stage));
    }

    private sealed class ThrowingStore : IBackupPasswordStore
    {
        public string? Load() => throw new InvalidOperationException("SECRET must never reach UI/log");
        public void Save(string password) => throw new InvalidOperationException("SECRET");
    }

    private sealed class EncryptedBackupFixture : IDisposable
    {
        public TestDatabase Test { get; } = new();
        public TestBackupPasswordStore Store { get; } = new();
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "area11-encrypted-backup-tests", Guid.NewGuid().ToString("N"));
        public string Backups => Path.Combine(Root, "backups");
        public string Stage => Path.Combine(Root, "stage");
        public BackupService Service { get; }
        public EncryptedBackupFixture()
        {
            Directory.CreateDirectory(Stage);
            Service = Build(Store);
        }
        public BackupService Build(IBackupPasswordStore store) => new(Test.Database, Test.Settings, Test.Audit,
            new PermissionService(Test.Database, Test.Log, Test.Audit), Test.Log,
            Path.Combine(Root, "photos"), Backups, store, Stage);
        public long CountProducts() => Test.Database.Tx(connection => Convert.ToInt64(
            Test.Database.Scalar(connection, "SELECT COUNT(*) FROM products;") ?? 0L));
        public void Dispose()
        {
            Test.Dispose();
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
