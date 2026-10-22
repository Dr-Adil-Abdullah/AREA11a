using System.IO;
using System.IO.Compression;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Backup and restore (F-36, Spec 23). The promises a shop keeps on these:
///
///   • one zip holds **the database and the photos** - an old `.db`-only backup lost every picture;
///   • the copy is made by SQLite itself, so it is consistent while bills are being written;
///   • **restore is not possible without a safety copy** - a wrong click costs one afternoon;
///   • a file that is not an Area11 database is refused **before** anything is replaced;
///   • the program only ever deletes **its own automatic** files, never the owner's.
///
/// These tests run against a throw-away database and throw-away folders, but the real SQL,
/// `VACUUM INTO`, `PRAGMA quick_check` and the zip are the same ones the shop will use.
/// </summary>
public sealed class BackupTests
{
    [Fact]
    public void Create_PutsTheDatabaseAndThePhotosInOneZip()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        TestData.AddProduct(test, name: "Panadol 500mg");
        folders.WritePhoto("panadol-1.jpg", "front of the box");
        folders.WritePhoto("panadol-2.jpg", "back of the box");

        var created = folders.Backups(test).Create(TestData.Owner);

        Assert.True(created.Ok, created.Error);
        Assert.NotNull(created.Value);
        Assert.True(File.Exists(created.Value!.FullPath));
        Assert.Contains("KB", created.Value.SizeLabel);

        using var zip = ZipFile.OpenRead(created.Value.FullPath);
        var names = zip.Entries.Select(entry => entry.FullName).ToList();

        Assert.Contains("area11.db", names);
        Assert.Contains("photos/panadol-1.jpg", names);
        Assert.Contains("photos/panadol-2.jpg", names);
        Assert.Contains("area11-backup.txt", names);

        // The manifest is what tells a human (and the restore screen) when this file was made.
        var manifest = zip.Entries.Single(entry => entry.FullName == "area11-backup.txt");
        using var reader = new StreamReader(manifest.Open());
        var text = reader.ReadToEnd();

        Assert.Contains("madeAt=", text);
        Assert.Contains("version=", text);
        Assert.Contains("photos=2", text);
    }

    [Fact]
    public void Create_DatabaseOnly_LeavesThePhotosOut()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        folders.WritePhoto("one.jpg", "a picture");

        var created = folders.Backups(test).Create(TestData.Owner, includePhotos: false);

        Assert.True(created.Ok, created.Error);

        using var zip = ZipFile.OpenRead(created.Value!.FullPath);
        Assert.DoesNotContain(zip.Entries, entry => entry.FullName.StartsWith("photos/", StringComparison.Ordinal));
        Assert.Contains(zip.Entries, entry => entry.FullName == "area11.db");
    }

    [Fact]
    public void Create_IsRefusedForSomebodyWithoutTheBackupPermission()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        var cashier = TestData.Cashier(test);

        var created = folders.Backups(test).Create(cashier);

        Assert.False(created.Ok);
        Assert.NotNull(created.Error);
        Assert.Empty(folders.Backups(test).List());
    }

    [Fact]
    public void TheDailyBackup_OnlyDeletesItsOwnOldFiles()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        Assert.True(test.Settings.Set("backup.keep", 2, TestData.Owner).Ok);

        var service = folders.Backups(test);

        // The owner's own backup - the one he copies to a USB stick. Nothing may remove it.
        var manual = service.Create(TestData.Owner);
        Assert.True(manual.Ok, manual.Error);

        for (var round = 0; round < 3; round++)
        {
            var automatic = service.Create(null, includePhotos: true, automatic: true);
            Assert.True(automatic.Ok, automatic.Error);
        }

        var left = service.List();

        Assert.Contains(left, file => file.FileName == manual.Value!.FileName);
        Assert.Equal(2, left.Count(file => file.IsAutomatic));
    }

    [Fact]
    public void Restore_BringsTheOldDataBack()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        var productId = TestData.AddProduct(test, name: "Panadol 500mg");
        var made = folders.Backups(test).Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);

        // A second medicine is added AFTER the backup - the restore must make it disappear.
        TestData.AddProduct(test, name: "Brufen 400mg");
        Assert.Equal(2L, CountProducts(test));

        var restored = folders.Backups(test).Restore(made.Value!.FullPath, TestData.Owner);

        Assert.True(restored.Ok, restored.Error);
        Assert.NotNull(restored.Value);
        Assert.Equal(1, restored.Value!.Products);

        // The first medicine survived, the later one is gone: the file really was replaced.
        Assert.Equal(1L, CountProducts(test));
        Assert.Equal(1L, CountProducts(test, productId));
        Assert.Equal("Panadol 500mg", NameOf(test, productId));
    }

    [Fact]
    public void Restore_AlwaysKeepsASafetyCopyOfTheLatestData()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        TestData.AddProduct(test, name: "Panadol 500mg");
        var made = folders.Backups(test).Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);

        TestData.AddProduct(test, name: "Brufen 400mg");

        var restored = folders.Backups(test).Restore(made.Value!.FullPath, TestData.Owner);
        Assert.True(restored.Ok, restored.Error);

        var safety = Path.Combine(folders.BackupFolder, restored.Value!.SafetyCopy);
        Assert.True(File.Exists(safety), "the safety copy must exist before anything is replaced");

        // The safety copy is the shop as it was a moment before the restore: both medicines.
        Assert.Equal(2L, CountProductsInFile(safety));
    }

    [Fact]
    public void Restore_RefusesAFileThatIsNotADatabase()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        var productId = TestData.AddProduct(test, name: "Panadol 500mg");
        var fake = Path.Combine(folders.BackupFolder, "not-a-backup.db");
        File.WriteAllText(fake, "this is a shopping list, not a database");

        var restored = folders.Backups(test).Restore(fake, TestData.Owner);

        Assert.False(restored.Ok);
        Assert.NotNull(restored.Error);

        // The shop is untouched: the medicine is still there and the file still opens normally.
        Assert.Equal(1L, CountProducts(test));
        Assert.Equal(1L, CountProducts(test, productId));
    }

    [Fact]
    public void Restore_RefusesAZipThatHoldsNoDatabase()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        TestData.AddProduct(test, name: "Panadol 500mg");

        var zipPath = Path.Combine(folders.BackupFolder, "holiday-photos.zip");

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("photos/beach.jpg");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("not a database");
        }

        var restored = folders.Backups(test).Restore(zipPath, TestData.Owner);

        Assert.False(restored.Ok);
        Assert.Equal(1L, CountProducts(test));
    }

    [Fact]
    public void Restore_IsRefusedForASignedInCashier()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        TestData.AddProduct(test, name: "Panadol 500mg");
        var made = folders.Backups(test).Create(TestData.Owner);
        Assert.True(made.Ok, made.Error);

        TestData.AddProduct(test, name: "Brufen 400mg");

        var restored = folders.Backups(test).Restore(made.Value!.FullPath, TestData.Cashier(test));

        Assert.False(restored.Ok);
        Assert.Equal(2L, CountProducts(test));   // nothing was replaced
    }

    [Fact]
    public void Restore_FromAPlainDatabaseFile_WorksToo()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        TestData.AddProduct(test, name: "Panadol 500mg");

        var copy = folders.Backups(test).SafetyCopy("test");
        Assert.True(copy.Ok, copy.Error);

        TestData.AddProduct(test, name: "Brufen 400mg");

        var restored = folders.Backups(test).Restore(copy.Value!, TestData.Owner);

        Assert.True(restored.Ok, restored.Error);
        Assert.Equal(1L, CountProducts(test));
    }

    [Fact]
    public void TheDailyBackup_RunsOncePerIntervalAndDoesNothingWhenSwitchedOff()
    {
        using var test = new TestDatabase();
        using var folders = new TempFolders();

        var service = folders.Backups(test);

        Assert.Null(service.RunAutomaticIfDue());
        Assert.Empty(service.List());

        Assert.True(test.Settings.Set("backup.autoEnabled", true, TestData.Owner).Ok);
        Assert.True(test.Settings.Set("backup.everyHours", 24, TestData.Owner).Ok);

        var first = service.RunAutomaticIfDue();
        Assert.NotNull(first);
        Assert.True(first!.IsAutomatic);

        // The interval has not passed: a second sign-in the same morning makes nothing.
        Assert.Null(service.RunAutomaticIfDue());
        Assert.Single(service.List());
        Assert.False(string.IsNullOrWhiteSpace(test.Settings.GetString("backup.lastRunAt")));
    }

    [Theory]
    [InlineData("backup.everyHours", 0)]
    [InlineData("backup.everyHours", 721)]
    [InlineData("backup.keep", 0)]
    [InlineData("backup.keep", 501)]
    public void TheSchedule_RefusesANonsenseNumber(string key, int value)
    {
        using var test = new TestDatabase();

        // A zero-hour interval would zip on every sign-in; keeping zero would delete today's backup.
        var saved = test.Settings.Set(key, value, TestData.Owner);

        Assert.False(saved.Ok);
        Assert.NotNull(saved.Error);
    }

    // ------------------------------ helpers ------------------------------

    private static long CountProducts(TestDatabase test) =>
        test.Database.Tx(connection => Convert.ToInt64(
            test.Database.Scalar(connection, "SELECT COUNT(1) FROM products;") ?? 0L));

    private static string? NameOf(TestDatabase test, int productId) =>
        test.Database.Tx(connection => test.Database.Scalar(connection,
            "SELECT name FROM products WHERE id = @id;", new { id = productId })?.ToString());

    /// <summary>How many medicines a database FILE holds - used to inspect a safety copy.</summary>
    private static long CountProductsInFile(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };

        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM products;";

        return Convert.ToInt64(command.ExecuteScalar() ?? 0L);
    }

    /// <summary>Two folders per test: the shop's photos and its backups. Deleting them is the test's job.</summary>
    private sealed class TempFolders : IDisposable
    {
        private readonly string _root;

        public TempFolders()
        {
            _root = Path.Combine(Path.GetTempPath(), "area11-backup-tests", Guid.NewGuid().ToString("N"));
            PhotoFolder = Path.Combine(_root, "photos");
            BackupFolder = Path.Combine(_root, "backups");

            Directory.CreateDirectory(PhotoFolder);
            Directory.CreateDirectory(BackupFolder);
        }

        public string PhotoFolder { get; }

        public string BackupFolder { get; }

        public BackupService Backups(TestDatabase test) =>
            new(test.Database, test.Settings, test.Audit,
                new PermissionService(test.Database, test.Log, test.Audit), test.Log,
                PhotoFolder, BackupFolder);

        public void WritePhoto(string name, string text) =>
            File.WriteAllText(Path.Combine(PhotoFolder, name), text);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Windows may still hold a file; %TEMP% is swept by the next run.
            }
        }
    }
}
