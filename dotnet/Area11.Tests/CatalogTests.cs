using System.IO;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>E-04: category/company management is a safe, audited catalog operation.</summary>
public sealed class CatalogTests
{
    [Fact]
    public void CategoryRenameOrderAndTwoLevelsWork_AndParentSearchIncludesChildren()
    {
        using var test = new TestDatabase();
        var service = Service(test);

        var parent = service.AddCategory(TestData.Owner, "Catalog parent", sortOrder: 20);
        Assert.True(parent.Ok, parent.Error);
        var child = service.AddCategory(TestData.Owner, "Catalog child", parent.Value, 10);
        Assert.True(child.Ok, child.Error);
        var other = service.AddCategory(TestData.Owner, "Catalog other", sortOrder: 30);
        Assert.True(other.Ok, other.Error);

        var childProduct = AddProduct(service, "Child medicine", child.Value, null);
        var otherProduct = AddProduct(service, "Other medicine", other.Value, null);

        var found = service.Search(categoryId: parent.Value);
        Assert.True(found.Ok, found.Error);
        Assert.Equal(childProduct, Assert.Single(found.Value!).Id);
        Assert.DoesNotContain(found.Value, row => row.Id == otherProduct);

        // A child cannot become a parent, a row cannot parent itself, and a parent with children
        // cannot be moved beneath another row. The category count itself remains unlimited.
        Assert.False(service.AddCategory(TestData.Owner, "Third level", child.Value).Ok);
        Assert.False(service.UpdateCategory(TestData.Owner, parent.Value, "Catalog parent", parent.Value, 20).Ok);
        Assert.False(service.UpdateCategory(TestData.Owner, parent.Value, "Catalog parent", other.Value, 20).Ok);

        var renamed = service.UpdateCategory(TestData.Owner, child.Value, "Renamed child", parent.Value, 5);
        Assert.True(renamed.Ok, renamed.Error);

        var rows = service.GetCategories(activeOnly: false);
        Assert.True(rows.Ok, rows.Error);
        var saved = rows.Value!.Single(row => row.Id == child.Value);
        Assert.Equal("Renamed child", saved.Name);
        Assert.Equal(parent.Value, saved.ParentId);
        Assert.Equal("Catalog parent", saved.ParentName);
        Assert.Equal(5, saved.SortOrder);
        Assert.Equal(1, saved.ProductCount);
        Assert.Equal("Renamed child", service.Get(childProduct).Value!.CategoryName);
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'category_update';"));
    }

    [Fact]
    public void CategoryPutAwayUnlinksOnlyDirectProducts_AndRestoreDoesNotInventLinks()
    {
        using var test = new TestDatabase();
        var service = Service(test);
        var parent = service.AddCategory(TestData.Owner, "Put-away parent").Value;
        var child = service.AddCategory(TestData.Owner, "Still-active child", parent).Value;
        var direct = AddProduct(service, "Direct product", parent, null);
        var nested = AddProduct(service, "Nested product", child, null);

        var putAway = service.SetCategoryActive(TestData.Owner, parent, false);
        Assert.True(putAway.Ok, putAway.Error);
        Assert.Null(service.Get(direct).Value!.CategoryId);
        Assert.Equal(child, service.Get(nested).Value!.CategoryId);
        Assert.Equal(2L, Rows(test, "SELECT COUNT(1) FROM products WHERE id IN (@direct, @nested);", new { direct, nested }));
        Assert.DoesNotContain(service.GetCategories().Value!, row => row.Id == parent);
        Assert.Contains(service.GetCategories().Value!, row => row.Id == child && row.ParentId == parent);

        var inactiveLink = service.Create(TestData.Owner, new ProductPatch
        {
            Name = "Inactive link",
            BaseUnit = "tablet",
            CostPaisa = 100,
            RetailPaisa = 200,
            CategoryId = parent
        });
        Assert.False(inactiveLink.Ok);
        Assert.Contains("active", inactiveLink.Error);

        Assert.True(service.SetCategoryActive(TestData.Owner, parent, true).Ok);
        Assert.True(service.GetCategories(activeOnly: false).Value!.Single(row => row.Id == parent).Active);
        Assert.Null(service.Get(direct).Value!.CategoryId);
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'category_put_away';"));
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'category_restore';"));
        Assert.Contains(direct.ToString(), Text(test,
            "SELECT details FROM audit_logs WHERE action = 'category_put_away' AND entity_id = @id;",
            new { id = parent }));
    }

    [Fact]
    public void CompanyManagementPreservesRepFields_AndUsesUnlinkOnlyPutAway()
    {
        using var test = new TestDatabase();
        var service = Service(test);
        var company = service.AddCompany(TestData.Owner, "Catalog company", 20).Value;
        var product = AddProduct(service, "Company medicine", null, company);

        test.Database.Tx(connection => test.Database.Execute(connection, @"
UPDATE companies
SET phone = '042-100', notes = 'Keep these notes', rep_name = 'Representative',
    rep_phone = '0300-100', visit_day = 2
WHERE id = @company;", new { company }));

        var renamed = service.UpdateCompany(TestData.Owner, company, "Renamed company", 5);
        Assert.True(renamed.Ok, renamed.Error);
        var row = service.GetCompanies(activeOnly: false).Value!.Single(item => item.Id == company);
        Assert.Equal("Renamed company", row.Name);
        Assert.Equal(5, row.SortOrder);
        Assert.Equal("042-100", row.Phone);
        Assert.Equal("Keep these notes", row.Notes);
        Assert.Equal("Representative", row.RepName);
        Assert.Equal("0300-100", row.RepPhone);
        Assert.Equal(2, row.VisitDay);
        Assert.Equal(1, row.ProductCount);
        Assert.Equal("Renamed company", service.Get(product).Value!.CompanyName);

        Assert.True(service.SetCompanyActive(TestData.Owner, company, false).Ok);
        Assert.Null(service.Get(product).Value!.CompanyId);
        Assert.False(service.GetCompanies(activeOnly: false).Value!.Single(item => item.Id == company).Active);
        Assert.DoesNotContain(service.GetCompanies().Value!, item => item.Id == company);
        Assert.False(AddProductResult(service, "Inactive company link", null, company).Ok);

        Assert.True(service.SetCompanyActive(TestData.Owner, company, true).Ok);
        Assert.Null(service.Get(product).Value!.CompanyId);
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'company_update';"));
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'company_put_away';"));
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'company_restore';"));
        Assert.Contains(product.ToString(), Text(test,
            "SELECT details FROM audit_logs WHERE action = 'company_put_away' AND entity_id = @id;",
            new { id = company }));
    }

    [Fact]
    public void CatalogNamesAreCaseInsensitivelyUnique_AndOrdersAreValidated()
    {
        using var test = new TestDatabase();
        var service = Service(test);
        var category = service.AddCategory(TestData.Owner, "Unique category", sortOrder: 10).Value;
        var company = service.AddCompany(TestData.Owner, "Unique company", sortOrder: 10).Value;

        Assert.False(service.AddCategory(TestData.Owner, "unique CATEGORY").Ok);
        Assert.False(service.AddCompany(TestData.Owner, "UNIQUE company").Ok);
        Assert.False(service.UpdateCategory(TestData.Owner, category, "Unique category", null, -1).Ok);
        Assert.False(service.UpdateCompany(TestData.Owner, company, "Unique company", -1).Ok);
        Assert.False(service.AddCompany(TestData.Owner, "Bad order", -1).Ok);

        // Old SQLite uniqueness was case-sensitive. If such a legacy duplicate exists, restoring
        // the second spelling must not make two active rows with the same apparent name.
        var legacyCategory = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO categories(name, sort_order, active, created_at) VALUES('UNIQUE category', 0, 0, '2026-01-01');");
            return (int)test.Database.NextId(connection);
        });
        var legacyCompany = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "INSERT INTO companies(name, sort_order, active) VALUES('UNIQUE company', 0, 0);");
            return (int)test.Database.NextId(connection);
        });
        Assert.False(service.SetCategoryActive(TestData.Owner, legacyCategory, true).Ok);
        Assert.False(service.SetCompanyActive(TestData.Owner, legacyCompany, true).Ok);

        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM categories WHERE name = 'Unique category';"));
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM companies WHERE name = 'Unique company';"));
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'category_create' AND entity_id = @id;", new { id = category }));
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'company_create' AND entity_id = @id;", new { id = company }));
    }

    [Fact]
    public void CatalogManagementRequiresAManager_InCore()
    {
        using var test = new TestDatabase();
        var service = Service(test);
        var cashier = TestData.Cashier(test);
        var category = service.AddCategory(TestData.Owner, "Permission category").Value;
        var company = service.AddCompany(TestData.Owner, "Permission company").Value;

        Assert.False(service.AddCategory(cashier, "Not allowed").Ok);
        Assert.False(service.AddCompany(cashier, "Not allowed").Ok);
        Assert.False(service.UpdateCategory(cashier, category, "Changed", null, 0).Ok);
        Assert.False(service.UpdateCompany(cashier, company, "Changed", 0).Ok);
        Assert.False(service.SetCategoryActive(cashier, category, false).Ok);
        Assert.False(service.SetCompanyActive(cashier, company, false).Ok);
        Assert.Equal("Permission category",
            service.GetCategories(activeOnly: false).Value!.Single(row => row.Id == category).Name);
        Assert.Equal("Permission company",
            service.GetCompanies(activeOnly: false).Value!.Single(row => row.Id == company).Name);
    }

    [Fact]
    public void CatalogOrderMigration_PreservesAnExistingCompanyAndProductLink()
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-catalog-migration", Guid.NewGuid().ToString("N"));
        var legacyFolder = Path.Combine(folder, "legacy");
        Directory.CreateDirectory(legacyFolder);

        foreach (var source in Directory.GetFiles(TestDatabase.MigrationsFolder, "*.sql"))
        {
            if (!Path.GetFileName(source).StartsWith("026_", StringComparison.Ordinal))
            {
                File.Copy(source, Path.Combine(legacyFolder, Path.GetFileName(source)));
            }
        }

        try
        {
            var database = new Database(Path.Combine(folder, "legacy.db"), pooling: false, durable: false);
            var log = new LogService(Path.Combine(folder, "logs"));
            var runner = new MigrationRunner(database, log);
            runner.ApplyPending(legacyFolder);
            database.Tx(connection =>
            {
                database.Execute(connection,
                    "INSERT INTO companies(id, name, phone, notes, active) VALUES(1, 'Old company', '123', 'Old note', 1);");
                database.Execute(connection, @"
INSERT INTO products(id, name, company_id, base_unit, cost_paisa, retail_paisa, created_at, updated_at)
VALUES(1, 'Old product', 1, 'tablet', 100, 200, '2026-01-01', '2026-01-01');");
            });

            Assert.Equal(1, runner.ApplyPending(TestDatabase.MigrationsFolder));
            using var connection = database.Open();
            var row = database.QuerySingle<CatalogMigrationRow>(connection, @"
SELECT c.name, c.phone, c.notes, c.sort_order AS sortOrder, p.company_id AS companyId
FROM companies c JOIN products p ON p.company_id = c.id WHERE c.id = 1;");
            Assert.NotNull(row);
            Assert.Equal("Old company", row!.Name);
            Assert.Equal("123", row.Phone);
            Assert.Equal("Old note", row.Notes);
            Assert.Equal(0, row.SortOrder);
            Assert.Equal(1, row.CompanyId);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    private static ProductService Service(TestDatabase test) => new(test.Database, test.Audit, test.Log);

    private static int AddProduct(ProductService service, string name, int? categoryId, int? companyId)
    {
        var result = AddProductResult(service, name, categoryId, companyId);
        Assert.True(result.Ok, result.Error);
        return result.Value;
    }

    private static ServiceResult<int> AddProductResult(
        ProductService service,
        string name,
        int? categoryId,
        int? companyId) => service.Create(TestData.Owner, new ProductPatch
    {
        Name = name,
        BaseUnit = "tablet",
        CostPaisa = 100,
        RetailPaisa = 200,
        CategoryId = categoryId,
        CompanyId = companyId
    });

    private static long Rows(TestDatabase test, string sql, object? args = null)
    {
        using var connection = test.Database.Open();
        return Convert.ToInt64(test.Database.Scalar(connection, sql, args) ?? 0);
    }

    private static string Text(TestDatabase test, string sql, object? args = null)
    {
        using var connection = test.Database.Open();
        return Convert.ToString(test.Database.Scalar(connection, sql, args)) ?? string.Empty;
    }

    private sealed class CatalogMigrationRow
    {
        public string Name { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string? Notes { get; set; }

        public int SortOrder { get; set; }

        public int CompanyId { get; set; }
    }
}
