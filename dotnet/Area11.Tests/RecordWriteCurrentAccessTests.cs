using System.IO;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated real-schema record write regressions. No session/WPF execution claim.</summary>
public sealed class RecordWriteCurrentAccessTests
{
    private sealed class RecordWriteFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public ProductService Products { get; }
        public PartnerService Partners { get; }
        public int ProductId { get; }
        public int CustomerId { get; }
        public string Photos { get; }
        public RecordWriteFixture()
        {
            Photos = Path.Combine(Path.GetDirectoryName(T.Database.FilePath)!, "synthetic-photos");
            Directory.CreateDirectory(Photos);
            Products = new ProductService(T.Database, T.Audit, T.Log, Photos, TestData.Permissions(T), T.Settings);
            Partners = new PartnerService(T.Database, T.Audit, T.Log, Photos);
            ProductId = TestData.AddProduct(T); CustomerId = TestData.AddCustomer(T);
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql)));
        public CustomerEditInput Edit() => new() { Id = CustomerId, Name = "Synthetic corrected customer", Phone = "0300000001", Category = "normal" };
        public string? ProductRow() => T.Database.Read(c => T.Database.Scalar(c, "SELECT name || ':' || cost_paisa || ':' || retail_paisa || ':' || active FROM products WHERE id=@id;", new { id = ProductId }))?.ToString();
        public string? CustomerRow() => T.Database.Read(c => T.Database.Scalar(c, "SELECT name || ':' || IFNULL(phone,'') || ':' || credit_limit_paisa || ':' || balance_paisa FROM customers WHERE id=@id;", new { id = CustomerId }))?.ToString();
        public void Grant(User u, string module, bool allowed = true) => Assert.True(TestData.Permissions(T).Set(TestData.Owner, u.Id, module, allowed).Ok);
        public void Fault(string action) => Execute("CREATE TRIGGER fail_record_audit BEFORE INSERT ON audit_logs WHEN NEW.action='" + action + "' BEGIN SELECT RAISE(ABORT,'synthetic record fault'); END;");
        public void Dispose() => T.Dispose();
    }
    private static ProductPatch NewInput() => new() { Name = "Synthetic new product", BaseUnit = "tablet", BoxStrips = 2, StripTablets = 10, CostPaisa = 10000, RetailPaisa = 15000 };
    private static void Denied(RecordWriteFixture f, User? actor)
    {
        var product = f.ProductRow(); var customer = f.CustomerRow(); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;"); var count = f.Number("SELECT COUNT(*) FROM products;");
        Assert.False(f.Partners.UpdateCustomer(actor!, f.Edit()).Ok); // No custom map: this used to skip current identity.
        Assert.False(f.Products.Update(actor!, f.ProductId, new ProductPatch { Name = "Denied name" }).Ok);
        Assert.False(f.Products.Update(actor!, f.ProductId, new ProductPatch()).Ok); // No-op is still authorized.
        Assert.False(f.Products.Deactivate(actor!, f.ProductId).Ok);
        Assert.False(f.Products.Create(actor!, NewInput()).Ok);
        Assert.False(f.Products.CheckEditorAccess(actor, f.ProductId).Ok);
        Assert.Equal(product, f.ProductRow()); Assert.Equal(customer, f.CustomerRow());
        Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;")); Assert.Equal(count, f.Number("SELECT COUNT(*) FROM products;"));
    }
    [Fact] public void AnonymousCannotUseReviewedRecordWrites() { using var f = new RecordWriteFixture(); Denied(f, null); }
    [Fact] public void InactiveSuppliedOwnerCannotUseReviewedRecordWrites() { using var f = new RecordWriteFixture(); Denied(f, new User { Id = 1, Name = "Old", Role = "owner", Active = false }); }
    [Fact] public void MissingStoredOwnerCannotUseReviewedRecordWrites() { using var f = new RecordWriteFixture(); Denied(f, new User { Id = int.MaxValue, Name = "Missing", Role = "owner" }); }
    [Fact] public void ForgedOwnerCannotUseReviewedRecordWrites() { using var f = new RecordWriteFixture(); var u = TestData.Cashier(f.T); Denied(f, new User { Id = u.Id, Name = "Forged", Role = "owner" }); }
    [Fact] public void InactiveStoredOwnerCannotUseReviewedRecordWrites() { using var f = new RecordWriteFixture(); f.Execute("UPDATE users SET active=0 WHERE id=1;"); Denied(f, TestData.Owner); }
    [Fact] public void ChangedStoredRoleCannotUseReviewedRecordWrites() { using var f = new RecordWriteFixture(); f.Execute("UPDATE users SET role='manager' WHERE id=1;"); Denied(f, TestData.Owner); }
    [Fact] public void ReactivatedOwnerCanUseFreshRecordWrites()
    {
        using var f = new RecordWriteFixture(); f.Execute("UPDATE users SET active=0 WHERE id=1;"); Denied(f, TestData.Owner);
        f.Execute("UPDATE users SET active=1 WHERE id=1;"); Assert.True(f.Partners.UpdateCustomer(TestData.Owner, f.Edit()).Ok);
        Assert.True(f.Products.Update(TestData.Owner, f.ProductId, new ProductPatch { Name = "Fresh name" }).Ok);
    }
    [Fact] public void RevokedCustomersGrantBlocksNullMapCustomerEdit()
    {
        using var f = new RecordWriteFixture(); var u = TestData.Cashier(f.T); f.Grant(u, Modules.Customers, false); var row = f.CustomerRow(); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;");
        Assert.False(f.Partners.UpdateCustomer(u, f.Edit()).Ok); Assert.Equal(row, f.CustomerRow()); Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
    }
    [Fact] public void RegrantedCustomersGrantAllowsNullMapEdit()
    {
        using var f = new RecordWriteFixture(); var u = TestData.Cashier(f.T); f.Grant(u, Modules.Customers, false); Assert.False(f.Partners.UpdateCustomer(u, f.Edit()).Ok);
        f.Grant(u, Modules.Customers); Assert.True(f.Partners.UpdateCustomer(u, f.Edit()).Ok);
    }
    [Fact] public void CustomerEditDoesNotRequireFinancialGrantsOrMainSwitch()
    {
        using var f = new RecordWriteFixture(); var u = TestData.Cashier(f.T); f.Grant(u, Modules.Customers); f.Grant(u, Modules.PurchaseCost, false); f.Grant(u, Modules.Profit, false);
        f.Execute("UPDATE settings SET value='false' WHERE key='profit.showMargins';"); Assert.True(f.Partners.UpdateCustomer(u, f.Edit()).Ok);
        Assert.Equal(0L, f.Number("SELECT balance_paisa FROM customers WHERE id=" + f.CustomerId));
    }
    [Fact] public void CurrentCashierPhoneEditPreservesLimitRule()
    {
        using var f = new RecordWriteFixture(); var u = TestData.Cashier(f.T); Assert.True(f.Partners.UpdateCustomer(u, f.Edit()).Ok);
        var row = f.CustomerRow(); var edit = f.Edit(); edit.CreditLimitPaisa = 10000;
        Assert.False(f.Partners.UpdateCustomer(u, edit).Ok); Assert.Equal(row, f.CustomerRow());
    }
    [Fact] public void RevokedProductsGrantBlocksCreateUpdateHideAndNoOp()
    {
        using var f = new RecordWriteFixture(); var u = TestData.AddUser(f.T, "manager"); f.Grant(u, Modules.Products, false); var before = f.ProductRow(); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;");
        Assert.False(f.Products.Create(u, NewInput()).Ok); Assert.False(f.Products.Update(u, f.ProductId, new ProductPatch()).Ok);
        Assert.False(f.Products.Update(u, f.ProductId, new ProductPatch { Name = "Denied" }).Ok); Assert.False(f.Products.Deactivate(u, f.ProductId).Ok);
        Assert.Equal(before, f.ProductRow()); Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
    }
    [Fact] public void RegrantedProductsGrantAllowsNonCostPatch()
    {
        using var f = new RecordWriteFixture(); var u = TestData.AddUser(f.T, "manager"); f.Grant(u, Modules.Products, false);
        Assert.False(f.Products.Update(u, f.ProductId, new ProductPatch { Name = "Denied" }).Ok); f.Grant(u, Modules.Products);
        Assert.True(f.Products.Update(u, f.ProductId, new ProductPatch { Name = "Allowed" }).Ok);
    }
    [Fact] public void ProductsGrantDoesNotPromoteCashierToManager()
    {
        using var f = new RecordWriteFixture(); var u = TestData.Cashier(f.T); f.Grant(u, Modules.Products);
        Assert.False(f.Products.Create(u, NewInput()).Ok); Assert.False(f.Products.Update(u, f.ProductId, new ProductPatch()).Ok); Assert.False(f.Products.Deactivate(u, f.ProductId).Ok);
    }
    [Fact] public void MissingCustomerEditDoesNotWriteOrAudit()
    {
        using var f = new RecordWriteFixture(); var edit = f.Edit(); edit.Id = int.MaxValue; var audit = f.Number("SELECT COUNT(*) FROM audit_logs;");
        Assert.False(f.Partners.UpdateCustomer(TestData.Owner, edit).Ok); Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
    }
    [Fact] public void MissingAndNonpositiveProductsCannotBeUpdatedOrHidden()
    {
        using var f = new RecordWriteFixture(); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;");
        foreach (var id in new[] { 0, -1, int.MaxValue })
        {
            Assert.False(f.Products.Update(TestData.Owner, id, new ProductPatch()).Ok); Assert.False(f.Products.Deactivate(TestData.Owner, id).Ok);
        }
        Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
    }
    [Fact] public void AlreadyHiddenProductIsAuthorizedIdempotentWithoutNewAudit()
    {
        using var f = new RecordWriteFixture(); Assert.True(f.Products.Deactivate(TestData.Owner, f.ProductId).Ok); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;");
        Assert.True(f.Products.Deactivate(TestData.Owner, f.ProductId).Ok); Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
        f.Execute("UPDATE users SET active=0 WHERE id=1;"); Assert.False(f.Products.Deactivate(TestData.Owner, f.ProductId).Ok);
    }
    [Fact] public void AuthorizedEmptyProductPatchDoesNotWriteOrAudit()
    {
        using var f = new RecordWriteFixture(); var before = f.ProductRow(); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;");
        Assert.True(f.Products.Update(TestData.Owner, f.ProductId, new ProductPatch()).Ok); Assert.Equal(before, f.ProductRow()); Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
    }
    [Fact] public void ReviewedWritesAuditStoredCurrentActorName()
    {
        using var f = new RecordWriteFixture(); f.Execute("UPDATE users SET name='Current stored writer' WHERE id=1;");
        Assert.True(f.Partners.UpdateCustomer(TestData.Owner, f.Edit()).Ok);
        Assert.True(f.Products.Create(TestData.Owner, NewInput()).Ok);
        Assert.True(f.Products.Update(TestData.Owner, f.ProductId, new ProductPatch { Name = "Renamed", CostPaisa = 21000 }).Ok);
        Assert.True(f.Products.Deactivate(TestData.Owner, f.ProductId).Ok);
        Assert.Equal(5L, f.Number("SELECT COUNT(*) FROM audit_logs WHERE user_name='Current stored writer' AND action IN ('update','product_create','product_edit','price_change','product_deactivate');"));
    }
    [Fact] public void StoredCostPatchWithoutCostGrantDoesNotPartiallyRename()
    {
        using var f = new RecordWriteFixture(); var u = TestData.AddUser(f.T, "manager"); f.Grant(u, Modules.PurchaseCost, false); var before = f.ProductRow();
        Assert.False(f.Products.Update(u, f.ProductId, new ProductPatch { Name = "Must not change", CostPaisa = 0 }).Ok); Assert.Equal(before, f.ProductRow());
    }
    [Fact] public void NonCostCorePatchRemainsAllowedWithoutCostGrant()
    {
        using var f = new RecordWriteFixture(); var u = TestData.AddUser(f.T, "manager"); f.Grant(u, Modules.PurchaseCost, false);
        Assert.True(f.Products.Update(u, f.ProductId, new ProductPatch { Name = "Noncost change" }).Ok); Assert.Equal(20000L, f.Number("SELECT cost_paisa FROM products WHERE id=" + f.ProductId));
    }
    [Fact] public void ProfitGrantDoesNotAuthorizeStoredCostEdit()
    {
        using var f = new RecordWriteFixture(); var u = TestData.AddUser(f.T, "manager"); f.Grant(u, Modules.Profit); f.Grant(u, Modules.PurchaseCost, false);
        Assert.False(f.Products.Update(u, f.ProductId, new ProductPatch { CostPaisa = 22000 }).Ok);
    }
    [Fact] public void CostEditDoesNotRequireProfitGrant()
    {
        using var f = new RecordWriteFixture(); var u = TestData.AddUser(f.T, "manager"); f.Grant(u, Modules.PurchaseCost); f.Grant(u, Modules.Profit, false);
        Assert.True(f.Products.Update(u, f.ProductId, new ProductPatch { CostPaisa = 22000 }).Ok);
    }
    [Fact] public void MainOffBlocksStoredCostEvenForOwner()
    {
        using var f = new RecordWriteFixture(); f.Execute("UPDATE settings SET value='false' WHERE key='profit.showMargins';"); var before = f.ProductRow();
        Assert.False(f.Products.Update(TestData.Owner, f.ProductId, new ProductPatch { CostPaisa = 0 }).Ok); Assert.Equal(before, f.ProductRow());
    }
    [Fact] public void FreshMainReadDoesNotTrustInjectedSettingsCache()
    {
        using var f = new RecordWriteFixture(); Assert.True(f.T.Settings.GetBool(MasterFoundationSettings.ProfitShowMargins, true));
        f.Execute("UPDATE settings SET value='false' WHERE key='profit.showMargins';");
        Assert.True(f.T.Settings.GetBool(MasterFoundationSettings.ProfitShowMargins, true)); // Deliberately stale cache.
        Assert.False(f.Products.CheckEditorAccess(TestData.Owner, f.ProductId).Ok); Assert.False(f.Products.Update(TestData.Owner, f.ProductId, new ProductPatch { CostPaisa = 0 }).Ok);
    }
    [Fact] public void MalformedMainSettingDoesNotAuthorizeStoredCost()
    {
        using var f = new RecordWriteFixture(); f.Execute("UPDATE settings SET value='not-json' WHERE key='profit.showMargins';");
        Assert.False(f.Products.Update(TestData.Owner, f.ProductId, new ProductPatch { CostPaisa = 0 }).Ok);
    }
    [Fact] public void MissingMainRowRetainsShippedTrueDefault()
    {
        using var f = new RecordWriteFixture(); f.Execute("DELETE FROM settings WHERE key='profit.showMargins';");
        Assert.True(f.Products.Update(TestData.Owner, f.ProductId, new ProductPatch { CostPaisa = 22000 }).Ok);
    }
    [Fact] public void NumericMainCompatibilityIsRetained()
    {
        using var f = new RecordWriteFixture(); f.Execute("UPDATE settings SET value='0' WHERE key='profit.showMargins';");
        Assert.False(f.Products.Update(TestData.Owner, f.ProductId, new ProductPatch { CostPaisa = 22000 }).Ok);
        f.Execute("UPDATE settings SET value='1' WHERE key='profit.showMargins';"); Assert.True(f.Products.Update(TestData.Owner, f.ProductId, new ProductPatch { CostPaisa = 22000 }).Ok);
    }
    [Fact] public void NewProductTypedCostWorkflowDoesNotRequireStoredCostGrant()
    {
        using var f = new RecordWriteFixture(); var u = TestData.AddUser(f.T, "manager"); f.Grant(u, Modules.PurchaseCost, false);
        f.Execute("UPDATE settings SET value='false' WHERE key='profit.showMargins';");
        Assert.True(f.Products.CheckEditorAccess(u).Ok); var result = f.Products.Create(u, NewInput()); Assert.True(result.Ok, result.Error);
    }
    [Fact] public void ExistingEditorPreflightRequiresActivePositiveParentAndStoredCost()
    {
        using var f = new RecordWriteFixture(); var u = TestData.AddUser(f.T, "manager"); Assert.False(f.Products.CheckEditorAccess(u, f.ProductId).Ok);
        f.Grant(u, Modules.PurchaseCost); Assert.True(f.Products.CheckEditorAccess(u, f.ProductId).Ok);
        foreach (var id in new[] { 0, -1, int.MaxValue }) Assert.False(f.Products.CheckEditorAccess(u, id).Ok);
        Assert.True(f.Products.Deactivate(u, f.ProductId).Ok); Assert.False(f.Products.CheckEditorAccess(u, f.ProductId).Ok);
    }
    [Fact] public void NewEditorPreflightStillRequiresCurrentManagerAndProducts()
    {
        using var f = new RecordWriteFixture(); var u = TestData.AddUser(f.T, "manager"); Assert.True(f.Products.CheckEditorAccess(u).Ok);
        f.Grant(u, Modules.Products, false); Assert.False(f.Products.CheckEditorAccess(u).Ok); Assert.False(f.Products.CheckEditorAccess(null).Ok);
    }
    [Fact] public void CustomerAuditFailureRollsBackNullMapEdit()
    {
        using var f = new RecordWriteFixture(); var before = f.CustomerRow(); f.Fault("update");
        Assert.False(f.Partners.UpdateCustomer(TestData.Owner, f.Edit()).Ok); Assert.Equal(before, f.CustomerRow());
    }
    [Fact] public void ProductEditAuditFailureRollsBackTagsPriceAndName()
    {
        using var f = new RecordWriteFixture(); f.Execute("INSERT INTO categories(name) VALUES('Synthetic tag');"); var tag = (int)f.Number("SELECT id FROM categories WHERE name='Synthetic tag';");
        var before = f.ProductRow(); f.Fault("product_edit");
        Assert.False(f.Products.Update(TestData.Owner, f.ProductId, new ProductPatch { Name = "Fault change", CostPaisa = 22000, CategoryTagIds = new List<int> { tag } }).Ok);
        Assert.Equal(before, f.ProductRow()); Assert.Equal(0L, f.Number("SELECT COUNT(*) FROM product_category_tags;"));
    }
    [Fact] public void ProductCreateAuditFailureRollsBackPhotoAndOnlyNewFile()
    {
        using var f = new RecordWriteFixture(); var source = Path.Combine(Path.GetDirectoryName(f.T.Database.FilePath)!, "source.jpg"); File.WriteAllText(source, "synthetic bytes, not image codec evidence");
        var kept = Path.Combine(f.Photos, "kept.jpg"); File.WriteAllText(kept, "keep"); var count = f.Number("SELECT COUNT(*) FROM products;"); var audit = f.Number("SELECT COUNT(*) FROM audit_logs;"); f.Fault("product_create");
        Assert.False(f.Products.Create(TestData.Owner, NewInput(), stagedPhotoPath: source).Ok);
        Assert.Equal(count, f.Number("SELECT COUNT(*) FROM products;")); Assert.Equal(0L, f.Number("SELECT COUNT(*) FROM photos;")); Assert.Equal(audit, f.Number("SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal(new[] { kept }, Directory.GetFiles(f.Photos)); Assert.True(File.Exists(source));
    }
    [Fact] public void ProductHideAuditFailureKeepsProductActive()
    {
        using var f = new RecordWriteFixture(); var before = f.ProductRow(); f.Fault("product_deactivate"); Assert.False(f.Products.Deactivate(TestData.Owner, f.ProductId).Ok); Assert.Equal(before, f.ProductRow());
    }
    [Fact] public void PermissionReadFailureDeniesManagerRecordWrites()
    {
        using var f = new RecordWriteFixture(); var u = TestData.AddUser(f.T, "manager"); f.Execute("DROP TABLE user_permissions;"); var before = f.ProductRow();
        Assert.False(f.Products.CheckEditorAccess(u).Ok); Assert.False(f.Products.Update(u, f.ProductId, new ProductPatch { Name = "Denied" }).Ok); Assert.False(f.Partners.UpdateCustomer(u, f.Edit()).Ok); Assert.Equal(before, f.ProductRow());
    }
}
