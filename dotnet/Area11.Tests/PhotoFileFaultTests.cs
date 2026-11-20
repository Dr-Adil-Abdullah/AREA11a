using System.IO;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Source regressions: real file I/O and disposable schema when executed by .NET.
/// Outcome-state tests are synthetic observations, NOT injected provider COMMIT failures.</summary>
public sealed class PhotoFileFaultTests
{
    private sealed class Folder : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "area11-photo-fault", Guid.NewGuid().ToString("N"));
        public string Destination => Path.Combine(Root, "copy.jpg");
        public LogService Log { get; }
        public Folder() { Directory.CreateDirectory(Root); Log = new LogService(Path.Combine(Root, "logs")); }
        public void Dispose() => Directory.Delete(Root, true);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void CleanupRequiresPositiveNoCommitEvidence(bool started, bool attempted, bool rolledBack, bool deleted)
    {
        using var f = new Folder(); File.WriteAllText(f.Destination, "owned synthetic copy");
        var unrelated = Path.Combine(f.Root, "unrelated.jpg"); File.WriteAllText(unrelated, "keep");
        var outcome = new TransactionOutcome { WorkStarted = started, CommitAttempted = attempted, RollbackConfirmed = rolledBack };
        PhotoService.DiscardCopy(f.Destination, outcome, f.Log);
        Assert.Equal(deleted, !File.Exists(f.Destination)); Assert.Equal("keep", File.ReadAllText(unrelated));
        if (!deleted) Assert.Contains("retained", string.Join("\n", Directory.GetFiles(Path.Combine(f.Root, "logs")).Select(File.ReadAllText)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65536)]
    [InlineData(65537)]
    [InlineData(12 * 1024 * 1024)]
    public void ExclusiveCopyPreservesExactBytesAtBufferAndSizeBoundaries(int length)
    {
        using var f = new Folder(); var bytes = Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();
        using var input = new MemoryStream(bytes); PhotoService.CopyOwned(input, f.Destination, PhotoService.MaxBytes, out var owned);
        Assert.Equal(f.Destination, owned); Assert.Equal(bytes, File.ReadAllBytes(owned!)); Assert.True(input.CanRead);
    }

    [Fact]
    public void CollisionNeverClaimsOrOverwritesExistingFile()
    {
        using var f = new Folder(); File.WriteAllText(f.Destination, "other invocation");
        using var input = new MemoryStream(new byte[] { 1 }); string? owned = "must reset";
        Assert.Throws<IOException>(() => PhotoService.CopyOwned(input, f.Destination, 1, out owned));
        Assert.Null(owned); PhotoService.DiscardCopy(owned, new TransactionOutcome(), f.Log);
        Assert.Equal("other invocation", File.ReadAllText(f.Destination));
    }

    [Fact]
    public void CannotCreateOutputDoesNotClaimAFileOrCreateFolders()
    {
        using var f = new Folder(); using var input = new MemoryStream(new byte[] { 1 }); string? owned = "must reset";
        var missing = Path.Combine(f.Root, "missing", "copy.jpg");
        Assert.Throws<DirectoryNotFoundException>(() => PhotoService.CopyOwned(input, missing, 1, out owned));
        Assert.Null(owned); Assert.False(Directory.Exists(Path.GetDirectoryName(missing)));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("growth")]
    [InlineData("partial-read")]
    public void CopyFaultTracksPartialOwnedOutputForConfirmedRollbackCleanup(string fault)
    {
        using var f = new Folder(); string? owned = null;
        using Stream input = fault == "partial-read" ? new PartialReadFault() : new MemoryStream(fault == "empty" ? Array.Empty<byte>() : new byte[65537]);
        Assert.ThrowsAny<Exception>(() => PhotoService.CopyOwned(input, f.Destination, 65536, out owned));
        Assert.Equal(f.Destination, owned); Assert.True(File.Exists(owned));
        Assert.InRange(new FileInfo(owned!).Length, 0L, 65536L);
        PhotoService.DiscardCopy(owned, new TransactionOutcome { WorkStarted = true, RollbackConfirmed = true }, f.Log);
        Assert.False(File.Exists(owned));
    }

    private sealed class PartialReadFault : MemoryStream
    {
        private bool _read;
        public PartialReadFault() : base(new byte[] { 1, 2, 3 }) { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_read) throw new IOException("synthetic source vanished after first read");
            _read = true; return base.Read(buffer, offset, count);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoOwnedPathDoesNotTouchFiles(string? path)
    {
        using var f = new Folder(); File.WriteAllText(f.Destination, "keep");
        PhotoService.DiscardCopy(path, new TransactionOutcome(), f.Log);
        Assert.Equal("keep", File.ReadAllText(f.Destination));
    }

    [Fact]
    public void CleanupIoFailureIsLoggedNotThrown()
    {
        using var f = new Folder(); var blocked = Path.Combine(f.Root, "not-a-file"); Directory.CreateDirectory(blocked);
        // Synthetic invalid cleanup target forces portable I/O/authorization failure, not ACL proof.
        PhotoService.DiscardCopy(blocked, new TransactionOutcome(), f.Log);
        Assert.True(Directory.Exists(blocked));
        Assert.Contains("could not be removed", string.Join("\n", Directory.GetFiles(Path.Combine(f.Root, "logs")).Select(File.ReadAllText)));
    }

    [Theory]
    [InlineData("gallery")]
    [InlineData("product")]
    [InlineData("customer")]
    [InlineData("supplier")]
    public void AuditFaultRollsBackRowsAndOnlyNewOwnedCopy(string route)
    {
        using var t = new TestDatabase(); using var f = new Folder();
        var photos = Path.Combine(f.Root, "photos"); Directory.CreateDirectory(photos);
        var source = Path.Combine(f.Root, "source.jpg"); File.WriteAllText(source, "synthetic source; not codec validation");
        var kept = Path.Combine(photos, "kept.jpg"); File.WriteAllText(kept, "keep");
        var product = TestData.AddProduct(t);
        var before = Counts(t);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_photo_fault BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic audit failure'); END;"));
        var result = Create(t, photos, source, route, product);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, Counts(t));
        Assert.Equal(new[] { kept }, Directory.GetFiles(photos)); Assert.Equal("keep", File.ReadAllText(kept)); Assert.True(File.Exists(source));
    }

    [Theory]
    [InlineData("gallery")]
    [InlineData("product")]
    [InlineData("customer")]
    [InlineData("supplier")]
    public void SuccessfulPhotoHasStoredRowAndRetainedSource(string route)
    {
        using var t = new TestDatabase(); using var f = new Folder();
        var photos = Path.Combine(f.Root, "photos"); Directory.CreateDirectory(photos);
        var source = Path.Combine(f.Root, "source.jpg"); File.WriteAllText(source, "synthetic bytes");
        var product = TestData.AddProduct(t); Assert.True(Create(t, photos, source, route, product).Ok);
        var file = Assert.Single(Directory.GetFiles(photos)); Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(file));
        var stored = t.Database.Read(c => t.Database.QuerySingle<string>(c, "SELECT file_name FROM photos;"));
        Assert.Equal(Path.GetFileName(file), stored);
    }

    private static string Counts(TestDatabase t) => t.Database.Read(c => string.Join(":", new[] { "products", "customers", "suppliers", "photos", "audit_logs" }
        .Select(table => t.Database.Scalar(c, "SELECT COUNT(*) FROM " + table + ";"))));

    private static (bool Ok, bool NoCommitConfirmed) Create(TestDatabase t, string photos, string source, string route, int product)
    {
        if (route == "gallery")
        {
            var r = new PhotoService(t.Database, t.Audit, TestData.Permissions(t), t.Log, photos).Add(TestData.Owner, PhotoEntity.Product, product, source);
            return (r.Ok, r.NoCommitConfirmed);
        }
        ServiceResult<int> result = route switch
        {
            "product" => new ProductService(t.Database, t.Audit, t.Log, photos, TestData.Permissions(t), t.Settings).Create(TestData.Owner,
                new ProductPatch { Name = "Synthetic photo product", BaseUnit = "tablet", BoxStrips = 2, StripTablets = 10, CostPaisa = 10000, RetailPaisa = 15000 }, stagedPhotoPath: source),
            "customer" => new PartnerService(t.Database, t.Audit, t.Log, photos).CreateCustomer(TestData.Owner, "Synthetic photo customer", null, "normal", stagedPhotoPath: source),
            _ => new PartnerService(t.Database, t.Audit, t.Log, photos).CreateSupplier(TestData.Owner, "Synthetic photo supplier", null, null, stagedPhotoPath: source)
        };
        return (result.Ok, result.NoCommitConfirmed);
    }
}
