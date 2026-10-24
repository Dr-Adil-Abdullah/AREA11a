using System.IO;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Photos of medicines, customers and suppliers (F-41, Spec 17.1 &amp; D-21). The promises:
///
///   1. the picture is **copied** into `data/photos` and only its **name** is stored - never a path,
///      or a backup restored on another computer would point at a folder that does not exist;
///   2. **nothing is deleted**: "put away" hides the photo, keeps the row and keeps the file, and the
///      black box says who did it;
///   3. a wrong kind of file, an empty file or one bigger than the limit is refused **before**
///      anything is copied, so the folder cannot fill up with mistakes;
///   4. a record keeps at most six photos - the owner asked for two or three, and a short list is
///      the point;
///   5. permission follows the record: the cashier may see a customer's picture, not the stock's.
/// </summary>
public sealed class PhotoTests
{
    // ============================== adding ==============================

    [Fact]
    public void APhotoIsCopiedInAndOnlyItsNameIsStored()
    {
        using var test = new TestDatabase();
        using var folders = new PhotoFolders();

        var product = TestData.AddProduct(test, name: "Panadol 500mg");

        var added = folders.Photos(test).Add(TestData.Owner, PhotoEntity.Product, product, folders.SourceFile("box.jpg"));

        Assert.True(added.Ok, added.Error);

        // The original is still where the owner had it; the shop has its own copy.
        Assert.True(File.Exists(folders.SourceFile("box.jpg")));
        Assert.True(File.Exists(added.Value!.FullPath));
        Assert.StartsWith(folders.PhotoFolder, added.Value.FullPath);

        // A path is never stored: the row carries a bare file name (rule U-19).
        Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), added.Value.FileName);
        Assert.Contains("product-" + product, added.Value.FileName);

        Assert.Equal(1L, folders.Rows(test, "SELECT COUNT(1) FROM photos;"));
        Assert.Equal(1L, folders.Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'photo_add';"));
    }

    [Fact]
    public void ABadFileIsRefusedBeforeAnythingIsCopied()
    {
        using var test = new TestDatabase();
        using var folders = new PhotoFolders();

        var product = TestData.AddProduct(test);

        var text = folders.Photos(test).Add(TestData.Owner, PhotoEntity.Product, product,
            folders.SourceFile("notes.txt", "this is not a picture"));

        Assert.False(text.Ok);
        Assert.Contains("picture", text.Error);

        var empty = folders.Photos(test).Add(TestData.Owner, PhotoEntity.Product, product,
            folders.SourceFile("empty.jpg", string.Empty));

        Assert.False(empty.Ok);
        Assert.Contains("empty", empty.Error);

        Assert.Equal(0L, folders.Rows(test, "SELECT COUNT(1) FROM photos;"));
        Assert.Empty(Directory.GetFiles(folders.PhotoFolder));
    }

    [Fact]
    public void AHugeFileIsRefusedByItsSize()
    {
        using var test = new TestDatabase();
        using var folders = new PhotoFolders();

        var product = TestData.AddProduct(test);
        var big = folders.SourceFile("huge.jpg");

        // 13 MB of zeros - longer than the 12 MB limit, and cheap to write.
        using (var stream = new FileStream(big, FileMode.Create, FileAccess.Write))
        {
            stream.SetLength(PhotoService.MaxBytes + 1024 * 1024);
        }

        var added = folders.Photos(test).Add(TestData.Owner, PhotoEntity.Product, product, big);

        Assert.False(added.Ok);
        Assert.Contains("limit", added.Error);
        Assert.Empty(Directory.GetFiles(folders.PhotoFolder));
    }

    [Fact]
    public void APhotoNeedsARealRecord()
    {
        using var test = new TestDatabase();
        using var folders = new PhotoFolders();

        var added = folders.Photos(test).Add(TestData.Owner, PhotoEntity.Product, 9999, folders.SourceFile("box.jpg"));

        Assert.False(added.Ok);
        Assert.Empty(Directory.GetFiles(folders.PhotoFolder));
    }

    [Fact]
    public void ARecordKeepsAtMostSixPhotos()
    {
        using var test = new TestDatabase();
        using var folders = new PhotoFolders();

        var product = TestData.AddProduct(test);
        var photos = folders.Photos(test);

        for (var index = 1; index <= PhotoService.MaxPerRecord; index++)
        {
            var added = photos.Add(TestData.Owner, PhotoEntity.Product, product,
                folders.SourceFile("photo-" + index + ".jpg"));

            Assert.True(added.Ok, added.Error);
        }

        var seventh = photos.Add(TestData.Owner, PhotoEntity.Product, product, folders.SourceFile("photo-7.jpg"));

        Assert.False(seventh.Ok);
        Assert.Contains("Put one away", seventh.Error);
        Assert.Equal(PhotoService.MaxPerRecord, Directory.GetFiles(folders.PhotoFolder).Length);
    }

    // ============================== putting away ==============================

    [Fact]
    public void PutAwayHidesThePhotoButDeletesNothing()
    {
        using var test = new TestDatabase();
        using var folders = new PhotoFolders();

        var product = TestData.AddProduct(test);
        var photos = folders.Photos(test);

        var added = photos.Add(TestData.Owner, PhotoEntity.Product, product, folders.SourceFile("box.jpg"));
        Assert.True(added.Ok, added.Error);

        var hidden = photos.Hide(TestData.Owner, added.Value!.Id);
        Assert.True(hidden.Ok, hidden.Error);

        // Gone from the list, still on the disk, still in the table.
        Assert.Empty(photos.List(TestData.Owner, PhotoEntity.Product, product).Value!);
        Assert.True(File.Exists(added.Value.FullPath));
        Assert.Equal(1L, folders.Rows(test, "SELECT COUNT(1) FROM photos;"));
        Assert.Equal(0L, folders.Rows(test, "SELECT active FROM photos;"));
        Assert.Equal(1L, folders.Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'photo_hide';"));

        // And it comes back with the same file, not a copy.
        Assert.True(photos.Restore(TestData.Owner, added.Value.Id).Ok);

        var back = photos.List(TestData.Owner, PhotoEntity.Product, product).Value!;
        Assert.Single(back);
        Assert.Equal(added.Value.FileName, back[0].FileName);
        Assert.Equal(1L, folders.Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'photo_restore';"));
    }

    [Fact]
    public void TheShowHiddenSwitchListsPutAwayPhotosToo()
    {
        using var test = new TestDatabase();
        using var folders = new PhotoFolders();

        var customer = TestData.AddCustomer(test);
        var photos = folders.Photos(test);

        var added = photos.Add(TestData.Owner, PhotoEntity.Customer, customer, folders.SourceFile("face.jpg"));
        Assert.True(added.Ok, added.Error);
        Assert.True(photos.Hide(TestData.Owner, added.Value!.Id).Ok);

        var all = photos.List(TestData.Owner, PhotoEntity.Customer, customer, includeHidden: true);

        Assert.True(all.Ok, all.Error);
        Assert.Single(all.Value!);
        Assert.False(all.Value![0].IsActive);
        Assert.Contains("put away", all.Value[0].StateLabel);
    }

    [Fact]
    public void TheGalleryReadsInTheOrderThePhotosWereAdded()
    {
        using var test = new TestDatabase();
        using var folders = new PhotoFolders();

        var supplier = TestData.AddSupplier(test);
        var photos = folders.Photos(test);

        foreach (var name in new[] { "front.jpg", "back.jpg", "strip.jpg" })
        {
            Assert.True(photos.Add(TestData.Owner, PhotoEntity.Supplier, supplier, folders.SourceFile(name)).Ok);
        }

        var list = photos.List(TestData.Owner, PhotoEntity.Supplier, supplier);

        Assert.True(list.Ok, list.Error);
        Assert.Equal(new[] { 1, 2, 3 }, list.Value!.Select(photo => photo.SortOrder).ToArray());
    }

    // ============================== permissions ==============================

    [Fact]
    public void TheCashierSeesACustomersPhotoButNotTheStock()
    {
        using var test = new TestDatabase();
        using var folders = new PhotoFolders();

        var cashier = TestData.Cashier(test);
        var customer = TestData.AddCustomer(test);
        var product = TestData.AddProduct(test);

        var photos = folders.Photos(test);

        Assert.False(photos.Add(cashier, PhotoEntity.Product, product, folders.SourceFile("box.jpg")).Ok);

        var customerPhoto = photos.Add(cashier, PhotoEntity.Customer, customer, folders.SourceFile("face.jpg"));
        Assert.True(customerPhoto.Ok, customerPhoto.Error);

        // A cashier may look at the khata, so the customer's picture is his business as well.
        Assert.Single(photos.List(cashier, PhotoEntity.Customer, customer).Value!);

        // The stock's pictures are the owner's and the salesman's.
        Assert.False(photos.List(cashier, PhotoEntity.Product, product).Ok);
    }

    [Fact]
    public void AnUnknownKindOfRecordIsRefused()
    {
        using var test = new TestDatabase();
        using var folders = new PhotoFolders();

        var added = folders.Photos(test).Add(TestData.Owner, "spaceship", 1, folders.SourceFile("box.jpg"));

        Assert.False(added.Ok);
        Assert.Equal(0L, folders.Rows(test, "SELECT COUNT(1) FROM photos;"));
    }

    // ============================== helpers ==============================

    /// <summary>One throw-away photo folder and one source folder per test.</summary>
    private sealed class PhotoFolders : IDisposable
    {
        private readonly string _root;

        public PhotoFolders()
        {
            _root = Path.Combine(Path.GetTempPath(), "area11-photo-tests", Guid.NewGuid().ToString("N"));
            PhotoFolder = Path.Combine(_root, "photos");
            SourceFolder = Path.Combine(_root, "source");

            Directory.CreateDirectory(PhotoFolder);
            Directory.CreateDirectory(SourceFolder);
        }

        public string PhotoFolder { get; }

        public string SourceFolder { get; }

        public PhotoService Photos(TestDatabase test) =>
            new(test.Database, test.Audit,
                new PermissionService(test.Database, test.Log, test.Audit), test.Log, PhotoFolder);

        /// <summary>A file in the "owner's phone" folder. The bytes do not matter - the name does.</summary>
        public string SourceFile(string name, string text = "pretend this is a picture")
        {
            var path = Path.Combine(SourceFolder, name);
            File.WriteAllText(path, text);

            return path;
        }

        public long Rows(TestDatabase test, string sql)
        {
            using var connection = test.Database.Open();

            return Convert.ToInt64(test.Database.Scalar(connection, sql) ?? 0);
        }

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
