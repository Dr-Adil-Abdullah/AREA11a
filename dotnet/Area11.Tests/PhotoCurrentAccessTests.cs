using System.IO;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Synthetic files in isolated real-schema fixtures; no image decoding/Windows claim.</summary>
public sealed class PhotoCurrentAccessTests
{
    private sealed class PhotoAccessFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public PhotoService Photos { get; }
        public string Source { get; }
        public int Customer { get; }
        public PhotoItem Item { get; }
        public PhotoAccessFixture()
        {
            var root = Path.GetDirectoryName(T.Database.FilePath)!;
            Source = Path.Combine(root, "synthetic-source.jpg"); File.WriteAllText(Source, "synthetic photo bytes, not an image decode fixture");
            Photos = new PhotoService(T.Database, T.Audit, TestData.Permissions(T), T.Log, Path.Combine(root, "synthetic-photos"));
            Customer = TestData.AddCustomer(T); var added = Photos.Add(TestData.Owner, PhotoEntity.Customer, Customer, Source);
            Assert.True(added.Ok, added.Error); Item = added.Value!;
        }
        public void Dispose() => T.Dispose();
    }
    private static void Execute(PhotoAccessFixture f, string sql) => f.T.Database.Tx(c => { f.T.Database.Execute(c, sql); });
    private static long Number(PhotoAccessFixture f, string sql) => f.T.Database.Read(c => Convert.ToInt64(f.T.Database.Scalar(c, sql)));
    private static void Grant(PhotoAccessFixture f, User u, string module, bool allow = true) => Assert.True(TestData.Permissions(f.T).Set(TestData.Owner, u.Id, module, allow).Ok);
    private static void Denied(PhotoAccessFixture f, User? u)
    {
        var audits = Number(f, "SELECT COUNT(*) FROM audit_logs;"); var rows = Number(f, "SELECT COUNT(*) FROM photos;");
        var files = Directory.GetFiles(f.Photos.PhotosFolder).OrderBy(p => p).ToArray();
        Assert.False(f.Photos.CheckAccess(u, PhotoEntity.Customer, f.Customer).Ok);
        Assert.False(f.Photos.List(u, PhotoEntity.Customer, f.Customer, true).Ok);
        Assert.False(f.Photos.PathOf(u, f.Item.Id).Ok); Assert.False(f.Photos.Hide(u, f.Item.Id).Ok); Assert.False(f.Photos.Restore(u, f.Item.Id).Ok);
        Assert.False(f.Photos.Add(u, PhotoEntity.Customer, f.Customer, f.Source).Ok);
        Assert.Empty(f.Photos.CoversFor(u, PhotoEntity.Customer, new[] { f.Customer }));
        Assert.Equal(audits, Number(f, "SELECT COUNT(*) FROM audit_logs;")); Assert.Equal(rows, Number(f, "SELECT COUNT(*) FROM photos;"));
        Assert.Equal(files, Directory.GetFiles(f.Photos.PhotosFolder).OrderBy(p => p).ToArray()); Assert.True(File.Exists(f.Item.FullPath));
    }
    [Fact] public void AnonymousCannotReadWriteOrResolveCustomerPhotos() { using var f = new PhotoAccessFixture(); Denied(f, null); }
    [Fact] public void InactiveSuppliedOwnerCannotUsePhotos() { using var f = new PhotoAccessFixture(); Denied(f, new User { Id = 1, Role = "owner", Active = false }); }
    [Fact] public void MissingStoredOwnerCannotUsePhotos() { using var f = new PhotoAccessFixture(); Denied(f, new User { Id = int.MaxValue, Role = "owner" }); }
    [Fact] public void ForgedOwnerCannotBorrowStaffPhotoAccess()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.Cashier(f.T); Denied(f, new User { Id = u.Id, Role = "owner" });
    }
    [Fact] public void InactiveStoredOwnerCannotUsePhotos() { using var f = new PhotoAccessFixture(); Execute(f, "UPDATE users SET active=0 WHERE id=1;"); Denied(f, TestData.Owner); }
    [Fact] public void ChangedStoredOwnerRoleCannotUsePhotos() { using var f = new PhotoAccessFixture(); Execute(f, "UPDATE users SET role='manager' WHERE id=1;"); Denied(f, TestData.Owner); }
    [Fact] public void RevokedCustomerGrantDeniesCachedGalleryActions()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.Cashier(f.T); Assert.True(f.Photos.List(u, PhotoEntity.Customer, f.Customer).Ok);
        Grant(f, u, Modules.Customers, false); Denied(f, u);
    }
    [Fact] public void RegrantRestoresFreshGalleryReadsAndWrites()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.Cashier(f.T); Grant(f, u, Modules.Customers, false); Denied(f, u); Grant(f, u, Modules.Customers);
        Assert.True(f.Photos.CheckAccess(u, PhotoEntity.Customer, f.Customer).Ok); Assert.Single(f.Photos.List(u, PhotoEntity.Customer, f.Customer).Value!);
        Assert.True(f.Photos.Hide(u, f.Item.Id).Ok); Assert.True(f.Photos.Restore(u, f.Item.Id).Ok); Assert.True(f.Photos.PathOf(u, f.Item.Id).Ok);
    }
    [Fact] public void StaffRoleChangeBlocksPhotosEvenIfNewRoleHasModuleDefault()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.Cashier(f.T); Execute(f, "UPDATE users SET role='manager' WHERE id=" + u.Id); Denied(f, u);
    }
    [Fact] public void FailedGrantReadCannotUseManagerDefaultForPhotos()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.AddUser(f.T, "manager"); Execute(f, "DROP TABLE user_permissions;"); Denied(f, u);
    }
    [Fact] public void CustomerPhotoAccessDoesNotRequireFinancialGrantsOrMain()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.Cashier(f.T);
        Assert.True(f.T.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.True(f.Photos.CheckAccess(u, PhotoEntity.Customer, f.Customer).Ok); Assert.True(f.Photos.PathOf(u, f.Item.Id).Ok);
        Assert.True(f.Photos.Hide(u, f.Item.Id).Ok);
    }
    [Fact] public void CurrentWriterStoredNameIsUsedForPhotoAudit()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.Cashier(f.T); Execute(f, "UPDATE users SET name='Current synthetic photo writer' WHERE id=" + u.Id);
        Assert.True(f.Photos.Hide(u, f.Item.Id).Ok);
        var name = f.T.Database.Read(c => f.T.Database.QuerySingle<string>(c, "SELECT user_name FROM audit_logs WHERE action='photo_hide' ORDER BY id DESC LIMIT 1;"));
        Assert.Equal("Current synthetic photo writer", name);
    }
    [Fact] public void MissingParentIsNotASuccessfulEmptyGallery()
    {
        using var f = new PhotoAccessFixture(); Assert.False(f.Photos.List(TestData.Owner, PhotoEntity.Customer, int.MaxValue).Ok);
        Assert.False(f.Photos.CheckAccess(TestData.Owner, PhotoEntity.Customer, int.MaxValue).Ok);
    }
    [Fact] public void InvalidEntityAndNonpositiveParentFailClosed()
    {
        using var f = new PhotoAccessFixture(); Assert.False(f.Photos.CheckAccess(TestData.Owner, "unknown", f.Customer).Ok);
        Assert.False(f.Photos.CheckAccess(TestData.Owner, PhotoEntity.Customer, 0).Ok); Assert.False(f.Photos.List(TestData.Owner, PhotoEntity.Customer, -1).Ok);
    }
    [Fact] public void InactiveParentRetainsHistoricalReadAndPutAwayButCannotAdd()
    {
        using var f = new PhotoAccessFixture(); Execute(f, "UPDATE customers SET active=0 WHERE id=" + f.Customer);
        Assert.True(f.Photos.List(TestData.Owner, PhotoEntity.Customer, f.Customer).Ok); Assert.True(f.Photos.PathOf(TestData.Owner, f.Item.Id).Ok);
        Assert.True(f.Photos.Hide(TestData.Owner, f.Item.Id).Ok); Assert.True(f.Photos.Restore(TestData.Owner, f.Item.Id).Ok);
        Assert.False(f.Photos.Add(TestData.Owner, PhotoEntity.Customer, f.Customer, f.Source).Ok);
    }
    [Fact] public void OrphanPhotoCannotBeReadOrChangedByItsId()
    {
        using var f = new PhotoAccessFixture(); Execute(f, "UPDATE photos SET entity_id=2147483647 WHERE id=" + f.Item.Id);
        Assert.False(f.Photos.PathOf(TestData.Owner, f.Item.Id).Ok); Assert.False(f.Photos.Hide(TestData.Owner, f.Item.Id).Ok);
    }
    [Fact] public void UnknownStoredEntityCannotFallBackToCustomers()
    {
        using var f = new PhotoAccessFixture(); Execute(f, "UPDATE photos SET entity='unknown' WHERE id=" + f.Item.Id);
        Assert.False(f.Photos.PathOf(TestData.Owner, f.Item.Id).Ok); Assert.False(f.Photos.Restore(TestData.Owner, f.Item.Id).Ok);
    }
    [Fact] public void ProductCoversRemainAvailableToCurrentPosOnlyReader()
    {
        using var f = new PhotoAccessFixture(); var product = TestData.AddProduct(f.T); var u = TestData.Cashier(f.T);
        Assert.True(f.Photos.Add(TestData.Owner, PhotoEntity.Product, product, f.Source).Ok);
        Assert.Single(f.Photos.CoversFor(u, PhotoEntity.Product, new[] { product }));
        Assert.False(f.Photos.List(u, PhotoEntity.Product, product).Ok);
    }
    [Fact] public void ProductCoversRejectAnonymousAndStaleAccounts()
    {
        using var f = new PhotoAccessFixture(); var product = TestData.AddProduct(f.T); var u = TestData.Cashier(f.T);
        Assert.True(f.Photos.Add(TestData.Owner, PhotoEntity.Product, product, f.Source).Ok);
        Assert.Empty(f.Photos.CoversFor(null, PhotoEntity.Product, new[] { product }));
        Execute(f, "UPDATE users SET active=0 WHERE id=" + u.Id); Assert.Empty(f.Photos.CoversFor(u, PhotoEntity.Product, new[] { product }));
    }
    [Fact] public void ProductCoverChangeRequiresCurrentProductsWriter()
    {
        using var f = new PhotoAccessFixture(); var product = TestData.AddProduct(f.T); var u = TestData.Cashier(f.T); Grant(f, u, Modules.Products);
        var item = f.Photos.Add(TestData.Owner, PhotoEntity.Product, product, f.Source).Value!;
        Grant(f, u, Modules.Products, false); Assert.False(f.Photos.SetProductCover(u, item.Id).Ok);
        Execute(f, "UPDATE users SET active=0 WHERE id=1;"); Assert.False(f.Photos.SetProductCover(TestData.Owner, item.Id).Ok);
    }
    [Fact] public void StaffGalleryDelegationIsPreservedWithoutOwnerPromotion()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.Cashier(f.T); var target = TestData.Cashier(f.T); Grant(f, u, Modules.Staff);
        Assert.True(f.Photos.Add(u, PhotoEntity.User, target.Id, f.Source).Ok); Assert.True(f.Photos.List(u, PhotoEntity.User, target.Id).Ok);
        Grant(f, u, Modules.Staff, false); Assert.False(f.Photos.List(u, PhotoEntity.User, target.Id).Ok);
    }
    [Fact] public void OwnAvatarStillNeedsNoStaffGrantButRejectsChangedRole()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.Cashier(f.T); Assert.True(f.Photos.Add(TestData.Owner, PhotoEntity.User, u.Id, f.Source).Ok);
        Assert.True(f.Photos.OwnAvatar(u).Ok); Execute(f, "UPDATE users SET role='manager' WHERE id=" + u.Id); Assert.False(f.Photos.OwnAvatar(u).Ok);
    }
    [Fact] public void OwnAvatarRejectsInactiveSuppliedActor()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.Cashier(f.T); Assert.True(f.Photos.Add(TestData.Owner, PhotoEntity.User, u.Id, f.Source).Ok);
        Assert.False(f.Photos.OwnAvatar(new User { Id = u.Id, Role = "cashier", Active = false }).Ok);
    }
    [Fact] public void PhotoReadDatabaseFailureIsGenericAndNotSuccessfulZero()
    {
        using var f = new PhotoAccessFixture(); Execute(f, "DROP TABLE photos;");
        var list = f.Photos.List(TestData.Owner, PhotoEntity.Customer, f.Customer); Assert.False(list.Ok); Assert.Null(list.Value);
        Assert.DoesNotContain("no such table", list.Error!); Assert.False(f.Photos.PathOf(TestData.Owner, f.Item.Id).Ok);
    }
    [Fact] public void AddAuditFaultRemovesOnlyTheNewCopiedFileAndRollsBackRow()
    {
        using var f = new PhotoAccessFixture(); var rows = Number(f, "SELECT COUNT(*) FROM photos;"); var files = Directory.GetFiles(f.Photos.PhotosFolder).OrderBy(p => p).ToArray();
        Execute(f, "CREATE TRIGGER fail_photo_add BEFORE INSERT ON audit_logs WHEN NEW.action='photo_add' BEGIN SELECT RAISE(ABORT,'synthetic photo fault'); END;");
        Assert.False(f.Photos.Add(TestData.Owner, PhotoEntity.Customer, f.Customer, f.Source).Ok);
        Assert.Equal(rows, Number(f, "SELECT COUNT(*) FROM photos;")); Assert.Equal(files, Directory.GetFiles(f.Photos.PhotosFolder).OrderBy(p => p).ToArray());
        Assert.True(File.Exists(f.Source)); Assert.True(File.Exists(f.Item.FullPath));
    }
    [Fact] public void PutAwayAuditFaultRollsBackVisibilityAndKeepsFile()
    {
        using var f = new PhotoAccessFixture(); Execute(f, "CREATE TRIGGER fail_photo_hide BEFORE INSERT ON audit_logs WHEN NEW.action='photo_hide' BEGIN SELECT RAISE(ABORT,'synthetic hide fault'); END;");
        Assert.False(f.Photos.Hide(TestData.Owner, f.Item.Id).Ok); Assert.Equal(1L, Number(f, "SELECT active FROM photos WHERE id=" + f.Item.Id)); Assert.True(File.Exists(f.Item.FullPath));
    }
    [Fact] public void RestoreAuditFaultRollsBackVisibilityAndKeepsFile()
    {
        using var f = new PhotoAccessFixture(); Assert.True(f.Photos.Hide(TestData.Owner, f.Item.Id).Ok);
        Execute(f, "CREATE TRIGGER fail_photo_restore BEFORE INSERT ON audit_logs WHEN NEW.action='photo_restore' BEGIN SELECT RAISE(ABORT,'synthetic restore fault'); END;");
        Assert.False(f.Photos.Restore(TestData.Owner, f.Item.Id).Ok); Assert.Equal(0L, Number(f, "SELECT active FROM photos WHERE id=" + f.Item.Id)); Assert.True(File.Exists(f.Item.FullPath));
    }
    [Fact] public void AccessAndReadsDoNotWriteAuditOrPhotoRows()
    {
        using var f = new PhotoAccessFixture(); var audits = Number(f, "SELECT COUNT(*) FROM audit_logs;"); var rows = Number(f, "SELECT COUNT(*) FROM photos;");
        Assert.True(f.Photos.CheckAccess(TestData.Owner, PhotoEntity.Customer, f.Customer).Ok); Assert.True(f.Photos.List(TestData.Owner, PhotoEntity.Customer, f.Customer).Ok);
        Assert.True(f.Photos.PathOf(TestData.Owner, f.Item.Id).Ok); Assert.Single(f.Photos.CoversFor(TestData.Owner, PhotoEntity.Customer, new[] { f.Customer }));
        Assert.Equal(audits, Number(f, "SELECT COUNT(*) FROM audit_logs;")); Assert.Equal(rows, Number(f, "SELECT COUNT(*) FROM photos;"));
    }
    [Fact] public void SupplierGalleryUsesItsOwnCurrentGrant()
    {
        using var f = new PhotoAccessFixture(); var u = TestData.Cashier(f.T); var supplier = TestData.AddSupplier(f.T); Grant(f, u, Modules.Suppliers);
        Assert.True(f.Photos.Add(u, PhotoEntity.Supplier, supplier, f.Source).Ok);
        Assert.True(f.Photos.List(u, PhotoEntity.Supplier, supplier).Ok);
        Grant(f, u, Modules.Suppliers, false); Assert.False(f.Photos.List(u, PhotoEntity.Supplier, supplier).Ok);
        Assert.Empty(f.Photos.CoversFor(u, PhotoEntity.Supplier, new[] { supplier }));
    }
    [Fact] public void StagedPhotoCurrentActorFailureRollsBackParentCreation()
    {
        using var f = new PhotoAccessFixture(); var before = Number(f, "SELECT COUNT(*) FROM customers;"); var audits = Number(f, "SELECT COUNT(*) FROM audit_logs;");
        var files = Directory.GetFiles(f.Photos.PhotosFolder).OrderBy(p => p).ToArray(); Execute(f, "UPDATE users SET active=0 WHERE id=1;");
        var partners = new PartnerService(f.T.Database, f.T.Audit, f.T.Log, f.Photos.PhotosFolder);
        Assert.False(partners.CreateCustomer(TestData.Owner, "Synthetic denied staged customer", "0300000001", "normal", stagedPhotoPath: f.Source).Ok);
        Assert.Equal(before, Number(f, "SELECT COUNT(*) FROM customers;")); Assert.Equal(audits, Number(f, "SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal(files, Directory.GetFiles(f.Photos.PhotosFolder).OrderBy(p => p).ToArray());
    }

}
