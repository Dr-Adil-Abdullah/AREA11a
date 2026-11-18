using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>D-113 editable files refuse, audit can return metadata. C# execution still required.</summary>
public sealed class BulkAndAuditAccessTests
{
    private static ImportService Imports(TestDatabase t) => new(t.Database, t.Audit, TestData.Permissions(t), t.Log, t.Settings);
    private static WorkbookService Books(TestDatabase t) => new(t.Database, t.Audit, TestData.Permissions(t), t.Settings, t.Log, Imports(t));
    private static AuditHistoryService Audit(TestDatabase t) => TestData.AuditHistory(t);
    private static void Grant(TestDatabase t, User u, string module, bool allowed = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, module, allowed).Ok);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static User Delegate(TestDatabase t, bool cost = false, bool profit = false)
    {
        var u = TestData.Cashier(t); Grant(t, u, Modules.Import);
        if (cost) Grant(t, u, Modules.PurchaseCost);
        if (profit) Grant(t, u, Modules.Profit); return u;
    }
    private static AuditFilter Filter(string search = "") => new() { Module = "Synthetic14", Search = search };
    private static int SeedAudit(TestDatabase t)
    {
        return t.Database.Tx(c =>
        {
            t.Audit.Record(c, TestData.Owner, "Synthetic14", "changed", "products", "14",
                "old-secret-14", "new-secret-14", "valuation-secret-14");
            return (int)t.Database.NextId(c);
        });
    }
    private static void Hidden(AuditPage page)
    {
        var row = Assert.Single(page.Rows); Assert.False(row.DetailKnown);
        Assert.Equal("", row.OldValue); Assert.Equal("", row.NewValue);
        Assert.Equal("", row.Entity); Assert.Equal("", row.EntityId); Assert.Equal("hidden", row.Details);
        Assert.Equal("changed", row.Action); Assert.Equal("Synthetic14", row.Module); Assert.Equal("Owner", row.UserName);
        Assert.DoesNotContain("secret-14", page.ToCsv());
        var snapshot = AuditSnapshot.For(row); Assert.Null(snapshot.EarlierRaw); Assert.Null(snapshot.RecordedRaw);
        Assert.Equal("hidden", snapshot.EarlierText); Assert.Equal("hidden", snapshot.RecordedText);
    }

    [Fact]
    public void BulkRoleDefaultsAreOwnerOnlyIncludingManager()
    {
        Assert.True(PermissionService.RoleDefault(UserRole.Owner, Modules.Import));
        foreach (var role in new[] { UserRole.Manager, UserRole.Salesman, UserRole.Cashier })
            Assert.False(PermissionService.RoleDefault(role, Modules.Import));
        Assert.Equal("Allow Excel/CSV Bulk Export & Import", Modules.Label(Modules.Import));
    }

    [Fact]
    public void ManagerWithoutExplicitBulkGrantCannotExportAnyList()
    {
        using var t = new TestDatabase(); var u = TestData.AddUser(t, "manager"); var s = Imports(t);
        Assert.False(s.ExportProducts(u).Ok); Assert.False(s.ExportCustomers(u).Ok); Assert.False(s.ExportSuppliers(u).Ok);
        Assert.False(Books(t).ExportAll(u).Ok);
    }

    [Fact]
    public void ProductsPageGrantIsNotABulkFileGrant()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Products); Grant(t, u, Modules.PurchaseCost);
        Assert.False(Imports(t).ExportProducts(u).Ok); Assert.False(Imports(t).CanUseBulk(u));
    }

    [Fact]
    public void NullAndForgedActorsCannotExportOrParseUploads()
    {
        using var t = new TestDatabase(); var u = Delegate(t, true, true); var s = Imports(t);
        foreach (var actor in new User?[] { null, new User { Id = u.Id, Role = "owner", Name = "Forged" },
                     new User { Id = int.MaxValue, Role = "owner", Name = "Missing" } })
        {
            Assert.False(s.ExportProducts(actor).Ok); Assert.False(Books(t).ExportAll(actor).Ok);
            Assert.Throws<ValidationException>(() => s.PreviewProducts(actor, "Name,Cost,Retail\r\nFake,10,20"));
        }
    }

    [Fact]
    public void StoredDeactivationInvalidatesBulkActorOnExistingService()
    {
        using var t = new TestDatabase(); var u = Delegate(t, true); var s = Imports(t); Assert.True(s.CanUseBulk(u));
        Execute(t, $"UPDATE users SET active=0 WHERE id={u.Id};"); Assert.False(s.CanUseBulk(u)); Assert.False(s.ExportProducts(u).Ok);
    }

    [Fact]
    public void ExplicitStaffBulkAndCostGrantCanExportActualNonzeroCost()
    {
        using var t = new TestDatabase(); TestData.AddProduct(t, costPaisa: 12345); var u = Delegate(t, true);
        var result = Imports(t).ExportProducts(u); Assert.True(result.Ok, result.Error);
        var rows = CsvReader.Parse(result.Value!); Assert.Equal("123.45", rows[1][10]);
        var workbook = Books(t).ExportList(u, "Products"); Assert.True(workbook.Ok, workbook.Error);
        Assert.Equal("123.45", XlsxFile.Read(workbook.Value!).Sheet("Products")!.Rows[1][10]);
    }

    [Fact]
    public void NoCostRefusesWholeEditableProductFileRatherThanBlankingItsPrice()
    {
        using var t = new TestDatabase(); TestData.AddProduct(t); var u = Delegate(t); var result = Imports(t).ExportProducts(u);
        Assert.False(result.Ok); Assert.Null(result.Value); Assert.False(Books(t).ExportList(u, "Products").Ok);
        Assert.Throws<ValidationException>(() => Imports(t).PreviewProducts(u, "Name,Cost,Retail\r\nFake,10,20"));
    }

    [Fact]
    public void MainOffRefusesOwnerProductFileAndMixedWorkbook()
    {
        using var t = new TestDatabase(); Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.False(Imports(t).ExportProducts(TestData.Owner).Ok); Assert.False(Books(t).ExportAll(TestData.Owner).Ok);
    }

    [Fact]
    public void CustomerListKeepsItsSeparateFinancialMeaningWithBulkGrant()
    {
        using var t = new TestDatabase(); TestData.AddCustomer(t); var u = Delegate(t);
        Assert.True(Imports(t).ExportCustomers(u).Ok); Assert.True(Books(t).ExportList(u, "Customers").Ok);
        Assert.False(Imports(t).ExportCustomers(TestData.Cashier(t)).Ok);
    }

    [Fact]
    public void MixedWorkbookRequiresBothFinancialGrantsNotJustCost()
    {
        using var t = new TestDatabase(); var u = Delegate(t, true); Assert.False(Books(t).ExportAll(u).Ok);
        Grant(t, u, Modules.Profit); Assert.True(Books(t).ExportAll(u).Ok);
    }

    [Fact]
    public void ExplicitStaffCanPreviewAndApplyAuthorizedProductImport()
    {
        using var t = new TestDatabase(); var u = Delegate(t, true); var s = Imports(t);
        var preview = s.PreviewProducts(u, "Name,Cost,Retail\r\nSynthetic 14,100,200\r\n");
        Assert.True(preview.CanApply); var saved = s.ApplyProducts(u, preview); Assert.True(saved.Ok, saved.Error);
        Assert.Equal(10000L, Number(t, "SELECT cost_paisa FROM products WHERE name='Synthetic 14';"));
    }

    [Fact]
    public void RevokedBulkCannotApplyPreviouslyPreparedProductImport()
    {
        using var t = new TestDatabase(); var u = Delegate(t, true); var s = Imports(t);
        var preview = s.PreviewProducts(u, "Name,Cost,Retail\r\nSynthetic 14,100,200\r\n");
        Grant(t, u, Modules.Import, false); Assert.False(s.ApplyProducts(u, preview).Ok);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM products;"));
    }

    [Fact]
    public void RevokedCostCannotApplyPreviouslyPreparedProductImport()
    {
        using var t = new TestDatabase(); var u = Delegate(t, true); var s = Imports(t);
        var preview = s.PreviewProducts(u, "Name,Cost,Retail\r\nSynthetic 14,100,200\r\n");
        Grant(t, u, Modules.PurchaseCost, false); Assert.False(s.ApplyProducts(u, preview).Ok);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM products;"));
    }

    [Fact]
    public void UnauthorizedWorkbookUploadRefusesBeforeDecodingInvalidBytes()
    {
        using var t = new TestDatabase(); var u = TestData.AddUser(t, "manager");
        Assert.Throws<ValidationException>(() => Imports(t).PreviewWorkbook(u, "Products", new byte[] { 1, 2 }));
        Assert.False(Books(t).PreviewBulk(u, new byte[] { 1, 2 }, false).Ok);
    }

    [Fact]
    public void FinancialPermissionStorageFailureCannotCreateAnEditableCostFile()
    {
        using var t = new TestDatabase(); Execute(t, "DROP TABLE user_permissions;");
        Assert.False(Imports(t).ExportProducts(TestData.Owner).Ok); Assert.False(Books(t).ExportAll(TestData.Owner).Ok);
    }

    [Fact]
    public void DelegatedUserCannotAdministerTheBulkPermission()
    {
        using var t = new TestDatabase(); var u = Delegate(t, true, true); var other = TestData.Cashier(t);
        Assert.False(TestData.Permissions(t).Set(u, other.Id, Modules.Import, true).Ok);
    }

    [Fact]
    public void AuditOnlyReaderGetsTimePersonActionButNoPayload()
    {
        using var t = new TestDatabase(); SeedAudit(t); var u = TestData.Cashier(t); Grant(t, u, Modules.Audit);
        var page = Audit(t).Read(Filter(), u); Assert.True(page.Ok, page.Error); Hidden(page.Value!);
    }

    [Fact]
    public void AuditCostOnlyStillCannotReadMixedRawDetail()
    {
        using var t = new TestDatabase(); SeedAudit(t); var u = Delegate(t, true); Grant(t, u, Modules.Audit);
        Hidden(Audit(t).Read(Filter(), u).Value!);
    }

    [Fact]
    public void AuditProfitOnlyStillCannotReadMixedRawDetail()
    {
        using var t = new TestDatabase(); SeedAudit(t); var u = Delegate(t, profit: true); Grant(t, u, Modules.Audit);
        Hidden(Audit(t).Read(Filter(), u).Value!);
    }

    [Fact]
    public void AuditBothGrantsReadOriginalValuesWithoutRewritingHistory()
    {
        using var t = new TestDatabase(); var id = SeedAudit(t); var u = Delegate(t, true, true); Grant(t, u, Modules.Audit);
        var s = Audit(t); var row = Assert.Single(s.Read(Filter(), u).Value!.Rows); Assert.True(row.DetailKnown);
        Assert.Equal("old-secret-14", row.OldValue); Assert.Equal("new-secret-14", row.NewValue);
        Assert.Equal("old-secret-14", s.ReadSnapshot(id, u).Value!.EarlierRaw);
    }

    [Fact]
    public void MainOffMasksOwnerAuditAndRawSnapshot()
    {
        using var t = new TestDatabase(); var id = SeedAudit(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Hidden(Audit(t).Read(Filter(), TestData.Owner).Value!);
        Assert.Null(Audit(t).ReadSnapshot(id, TestData.Owner).Value!.EarlierRaw);
    }

    [Fact]
    public void MetadataSearchCannotMatchHiddenValueDetailsOrEntity()
    {
        using var t = new TestDatabase(); SeedAudit(t); var u = Delegate(t); Grant(t, u, Modules.Audit);
        foreach (var term in new[] { "old-secret-14", "new-secret-14", "valuation-secret-14", "products" })
        {
            var result = Audit(t).Read(Filter(term), u); Assert.True(result.Ok, result.Error);
            Assert.Equal(0, result.Value!.Total); Assert.Empty(result.Value.Rows);
        }
        Assert.Single(Audit(t).Read(Filter("changed"), u).Value!.Rows);
    }

    [Fact]
    public void AuditCsvRequiresBulkPermissionEvenForMetadata()
    {
        using var t = new TestDatabase(); SeedAudit(t); var u = TestData.Cashier(t); Grant(t, u, Modules.Audit);
        Assert.True(Audit(t).Read(Filter(), u).Ok); Assert.False(Audit(t).BuildCsv(Filter(), u).Ok);
        Grant(t, u, Modules.Import); var csv = Audit(t).BuildCsv(Filter(), u); Assert.True(csv.Ok, csv.Error);
        Assert.Contains("changed", csv.Value!); Assert.DoesNotContain("secret-14", csv.Value!);
    }

    [Fact]
    public void AuditExportRereadsAfterRevocationInsteadOfUsingEarlierRawPage()
    {
        using var t = new TestDatabase(); SeedAudit(t); var u = Delegate(t, true, true); Grant(t, u, Modules.Audit); var s = Audit(t);
        var old = s.Read(Filter(), u).Value!; Assert.Contains("old-secret-14", old.ToCsv());
        Grant(t, u, Modules.PurchaseCost, false); var csv = s.BuildCsv(Filter(), u);
        Assert.True(csv.Ok, csv.Error); Assert.DoesNotContain("secret-14", csv.Value!);
        Assert.Contains("old-secret-14", old.ToCsv()); // Old returned objects cannot be recalled.
    }

    [Fact]
    public void CurrentAuditReaderCannotBeReplacedByFinancialGrants()
    {
        using var t = new TestDatabase(); var id = SeedAudit(t); var u = Delegate(t, true, true);
        Assert.False(Audit(t).Read(Filter(), u).Ok); Assert.False(Audit(t).ReadSnapshot(id, u).Ok);
        Assert.False(Audit(t).BuildCsv(Filter(), u).Ok);
    }

    [Fact]
    public void StoredRoleChangeRefusesOldAuditActor()
    {
        using var t = new TestDatabase(); var id = SeedAudit(t); var u = Delegate(t, true, true); Grant(t, u, Modules.Audit);
        Execute(t, $"UPDATE users SET role='manager' WHERE id={u.Id};");
        Assert.False(Audit(t).Read(Filter(), u).Ok); Assert.False(Audit(t).ReadSnapshot(id, u).Ok);
    }

    [Fact]
    public void AuditFinancialGrantFailureMasksRatherThanAssumingOwnerDetail()
    {
        using var t = new TestDatabase(); SeedAudit(t); Execute(t, "DROP TABLE user_permissions;");
        Hidden(Audit(t).Read(Filter(), TestData.Owner).Value!);
    }

    [Fact]
    public void MetadataReadsNeverRewriteStoredAuditValues()
    {
        using var t = new TestDatabase(); SeedAudit(t); var u = Delegate(t); Grant(t, u, Modules.Audit);
        var before = Number(t, "SELECT COUNT(*) FROM audit_logs;"); Hidden(Audit(t).Read(Filter(), u).Value!);
        Audit(t).BuildCsv(Filter(), u);
        Assert.Equal(before, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM audit_logs WHERE old_value='old-secret-14' AND new_value='new-secret-14';"));
    }
    [Fact]
    public void HistoryReportingCsvAlsoNeedsDedicatedBulkPermission()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var u = TestData.Cashier(t); Grant(t, u, Modules.Products);
        var history = new ProductHistoryService(t.Database, t.Settings, TestData.Permissions(t), t.Log);
        Assert.True(history.Load(u, p).Ok); Assert.False(history.BuildCsv(u, p).Ok);
        Grant(t, u, Modules.Import); Assert.True(history.BuildCsv(u, p).Ok);
    }

    [Fact]
    public void BooksCsvRequiresBulkBothFinancialAndRelevantPageGrants()
    {
        using var t = new TestDatabase(); var u = Delegate(t, true, true);
        var books = new BookCheckService(t.Database, TestData.Permissions(t), t.Log, t.Settings);
        Assert.False(books.BuildCsv(u).Ok); Grant(t, u, Modules.Reports); Assert.True(books.BuildCsv(u).Ok);
        Grant(t, u, Modules.Import, false); Assert.False(books.BuildCsv(u).Ok);
    }

    [Fact]
    public void BooksCsvRefusesMissingProfitEvenWhenReportsAndCostAreAllowed()
    {
        using var t = new TestDatabase(); var u = Delegate(t, true); Grant(t, u, Modules.Reports);
        var books = new BookCheckService(t.Database, TestData.Permissions(t), t.Log, t.Settings);
        Assert.False(books.BuildCsv(u).Ok);
    }

    [Fact]
    public void OldMixedWorkbookPreviewCannotBeAppliedAfterBulkRevocation()
    {
        using var t = new TestDatabase(); TestData.AddProduct(t); var u = Delegate(t, true, true); var service = Books(t);
        var bytes = service.ExportAll(u); Assert.True(bytes.Ok, bytes.Error);
        var workbook = XlsxFile.Read(bytes.Value!);
        var products = workbook.Sheet("Products")!;
        products.Rows[1][products.Rows[0].IndexOf("Name")] = "Edited synthetic product";
        var preview = service.PreviewBulk(u, XlsxFile.Write(workbook), false); Assert.True(preview.Ok, preview.Error);
        Assert.True(preview.Value!.CanApply);
        Grant(t, u, Modules.Import, false); Assert.False(service.ApplyBulk(u, preview.Value).Ok);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM products WHERE name='Edited synthetic product';"));
    }

    [Fact]
    public void BlackBoxMenuEligibilityRequiresCurrentAuditBothFinancialAndMain()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var audit = Audit(t);
        foreach (var auditGrant in new[] { false, true })
        foreach (var costGrant in new[] { false, true })
        foreach (var profitGrant in new[] { false, true })
        {
            Grant(t, user, Modules.Audit, auditGrant);
            Grant(t, user, Modules.PurchaseCost, costGrant);
            Grant(t, user, Modules.Profit, profitGrant);
            var access = audit.CheckAccess(user);
            Assert.Equal(auditGrant && costGrant && profitGrant, access is { Ok: true, Value: true });
        }
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.False(audit.CheckAccess(user) is { Ok: true, Value: true });
        Assert.False(audit.CheckAccess(TestData.Owner) is { Ok: true, Value: true });
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        Assert.True(audit.CheckAccess(user) is { Ok: true, Value: true });
        Execute(t, "UPDATE users SET active=0 WHERE id=" + user.Id);
        Assert.False(audit.CheckAccess(user) is { Ok: true, Value: true });
    }

}
