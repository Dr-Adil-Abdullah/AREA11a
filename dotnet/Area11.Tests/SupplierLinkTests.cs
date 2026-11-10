using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Portion 9: brand/company selection, one explicit preferred supplier per product, and the
/// supplier-to-products filter over preferred + genuine purchase history. History is never
/// erased and never guesses a preferred supplier.
/// </summary>
public sealed class SupplierLinkTests
{
    private static ProductService Products(TestDatabase test) => new(test.Database, test.Audit, test.Log);

    private static int AddBrand(ProductService products, string name)
    {
        var added = products.AddBrand(TestData.Owner, name);
        Assert.True(added.Ok, added.Error);
        return added.Value;
    }

    private static int AddCompany(ProductService products, string name)
    {
        var added = products.AddCompany(TestData.Owner, name);
        Assert.True(added.Ok, added.Error);
        return added.Value;
    }

    private static int CreateProduct(ProductService products, string name, ProductPatch? patch = null)
    {
        patch ??= new ProductPatch();
        patch.Name = name;
        patch.CostPaisa ??= 100;
        patch.RetailPaisa ??= 200;
        var created = products.Create(TestData.Owner, patch);
        Assert.True(created.Ok, created.Error);
        return created.Value;
    }

    private static void AddHistory(TestDatabase test, int productId, int supplierId, string code)
    {
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "INSERT INTO purchases(code, supplier_id, date) VALUES(@code, @supplierId, '2026-02-01 10:00:00');",
                new { code, supplierId });
            var purchaseId = (int)test.Database.NextId(connection);
            test.Database.Execute(connection,
                "INSERT INTO purchase_items(purchase_id, product_id) VALUES(@purchaseId, @productId);",
                new { purchaseId, productId });
            return 0;
        });
    }

    [Fact]
    public void SetBrandCompany_LinksAndShowsCompanyName()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var company = AddCompany(products, "Acme Labs");
        var brand = AddBrand(products, "Acme");

        var linked = products.SetBrandCompany(TestData.Owner, brand, company);

        Assert.True(linked.Ok, linked.Error);
        var row = Assert.Single(products.GetBrands().Value!);
        Assert.Equal(company, row.CompanyId);
        Assert.Equal("Acme Labs", row.CompanyName);
    }

    [Fact]
    public void SetBrandCompany_UnknownOrPutAwayCompany_Refused()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var company = AddCompany(products, "Acme Labs");
        var brand = AddBrand(products, "Acme");

        var unknown = products.SetBrandCompany(TestData.Owner, brand, 999);
        Assert.False(unknown.Ok);

        Assert.True(products.SetCompanyActive(TestData.Owner, company, false).Ok);
        var putAway = products.SetBrandCompany(TestData.Owner, brand, company);
        Assert.False(putAway.Ok);
        Assert.Contains("active list", putAway.Error!);
        Assert.Null(products.GetBrands(activeOnly: false).Value![0].CompanyId);
    }

    [Fact]
    public void SetBrandCompany_NonManager_Refused()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var company = AddCompany(products, "Acme Labs");
        var brand = AddBrand(products, "Acme");

        var refused = products.SetBrandCompany(TestData.Cashier(test), brand, company);

        Assert.False(refused.Ok);
        Assert.Contains("owner or a manager", refused.Error!);
    }

    [Fact]
    public void SetBrandCompany_Clear_KeepsNameAndProducts()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var company = AddCompany(products, "Acme Labs");
        var brand = AddBrand(products, "Acme");
        Assert.True(products.SetBrandCompany(TestData.Owner, brand, company).Ok);

        var cleared = products.SetBrandCompany(TestData.Owner, brand, null);

        Assert.True(cleared.Ok, cleared.Error);
        var row = Assert.Single(products.GetBrands().Value!);
        Assert.Null(row.CompanyId);
        Assert.Null(row.CompanyName);
        Assert.Equal("Acme", row.Name);
    }

    [Fact]
    public void CompanyPutAway_UnlinksBrands_BringBackDoesNotRelink()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var company = AddCompany(products, "Acme Labs");
        var brand = AddBrand(products, "Acme");
        Assert.True(products.SetBrandCompany(TestData.Owner, brand, company).Ok);

        Assert.True(products.SetCompanyActive(TestData.Owner, company, false).Ok);
        Assert.Null(products.GetBrands(activeOnly: false).Value![0].CompanyId);

        Assert.True(products.SetCompanyActive(TestData.Owner, company, true).Ok);
        Assert.Null(products.GetBrands().Value![0].CompanyId);
    }

    [Fact]
    public void CreateProduct_WithPreferredSupplier()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var supplier = TestData.AddSupplier(test, "Alpha Depot");

        var id = CreateProduct(products, "Preferred tablet", new ProductPatch { PreferredSupplierId = supplier });

        Assert.Equal(supplier, products.Get(id).Value!.PreferredSupplierId);
        var directory = products.GetProductSuppliers(id).Value!;
        var row = Assert.Single(directory);
        Assert.Equal(supplier, row.SupplierId);
        Assert.Equal("Alpha Depot", row.SupplierName);
        Assert.True(row.IsPreferred);
    }

    [Fact]
    public void CreateProduct_UnknownOrInactivePreferred_Refused()
    {
        using var test = new TestDatabase();
        var products = Products(test);

        var unknown = products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Ghost preferred",
            PreferredSupplierId = 999,
            CostPaisa = 100,
            RetailPaisa = 200
        });
        Assert.False(unknown.Ok);

        var supplier = TestData.AddSupplier(test, "Quiet Depot");
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, "UPDATE suppliers SET active = 0 WHERE id = @id;", new { id = supplier });
            return 0;
        });
        var inactive = products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Quiet preferred",
            PreferredSupplierId = supplier,
            CostPaisa = 100,
            RetailPaisa = 200
        });
        Assert.False(inactive.Ok);
        Assert.Contains("active list", inactive.Error!);
        Assert.Empty(products.Search("preferred").Value!);
    }

    [Fact]
    public void PurchaseHistory_NeverSetsPreferred()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var supplier = TestData.AddSupplier(test, "History Depot");
        var id = CreateProduct(products, "History tablet");

        AddHistory(test, id, supplier, "PINV-0001");

        Assert.Null(products.Get(id).Value!.PreferredSupplierId);
        var directory = products.GetProductSuppliers(id).Value!;
        var row = Assert.Single(directory);
        Assert.Equal(supplier, row.SupplierId);
        Assert.Equal("History Depot", row.SupplierName);
        Assert.False(row.IsPreferred);
        Assert.Equal("2026-02-01 10:00:00", row.LastPurchasedAt);
    }

    [Fact]
    public void UpdatePreferred_SetAndClear()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var first = TestData.AddSupplier(test, "First Depot");
        var second = TestData.AddSupplier(test, "Second Depot");
        var id = CreateProduct(products, "Switched tablet", new ProductPatch { PreferredSupplierId = first });

        var set = products.Update(TestData.Owner, id, new ProductPatch { PreferredSupplierId = second });
        Assert.True(set.Ok, set.Error);
        Assert.Equal(second, products.Get(id).Value!.PreferredSupplierId);

        var cleared = products.Update(TestData.Owner, id, new ProductPatch { ClearPreferredSupplier = true });
        Assert.True(cleared.Ok, cleared.Error);
        var stored = products.Get(id).Value!;
        Assert.Null(stored.PreferredSupplierId);
        Assert.Equal("Switched tablet", stored.Name);
    }

    [Fact]
    public void UpdatePreferred_Unknown_Refused_NothingChanged()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var supplier = TestData.AddSupplier(test, "Kept Depot");
        var id = CreateProduct(products, "Kept tablet", new ProductPatch { PreferredSupplierId = supplier });

        var refused = products.Update(TestData.Owner, id, new ProductPatch { PreferredSupplierId = 999 });

        Assert.False(refused.Ok);
        Assert.Equal(supplier, products.Get(id).Value!.PreferredSupplierId);
    }

    [Fact]
    public void SupplierFilter_MatchesPreferredOnly()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var supplier = TestData.AddSupplier(test, "Preferred Depot");
        var id = CreateProduct(products, "Preferred match", new ProductPatch { PreferredSupplierId = supplier });
        CreateProduct(products, "Unrelated match");

        var found = products.Search(null, supplierId: supplier).Value!;

        Assert.Equal(id, Assert.Single(found).Id);
    }

    [Fact]
    public void SupplierFilter_MatchesHistoryOnly()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var supplier = TestData.AddSupplier(test, "History Filter Depot");
        var id = CreateProduct(products, "History match");
        AddHistory(test, id, supplier, "PINV-0002");

        var found = products.Search(null, supplierId: supplier).Value!;

        Assert.Equal(id, Assert.Single(found).Id);
    }

    [Fact]
    public void SupplierFilter_CombinesWithTerm_AndExcludesOthers()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var supplier = TestData.AddSupplier(test, "Combo Depot");
        var other = TestData.AddSupplier(test, "Other Depot");
        var id = CreateProduct(products, "Combo syrup", new ProductPatch { PreferredSupplierId = supplier });
        CreateProduct(products, "Combo drops", new ProductPatch { PreferredSupplierId = other });

        var found = products.Search("syrup", supplierId: supplier).Value!;

        Assert.Equal(id, Assert.Single(found).Id);
        Assert.Empty(products.Search("drops", supplierId: supplier).Value!);
    }

    [Fact]
    public void SupplierOptions_CountActiveProducts_IncludesInactiveHistorySupplier()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var live = TestData.AddSupplier(test, "Live Depot");
        var quiet = TestData.AddSupplier(test, "Quiet History Depot");
        var first = CreateProduct(products, "Counted one", new ProductPatch { PreferredSupplierId = live });
        CreateProduct(products, "Counted two", new ProductPatch { PreferredSupplierId = live });
        var history = CreateProduct(products, "Counted history");
        AddHistory(test, history, quiet, "PINV-0003");
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, "UPDATE suppliers SET active = 0 WHERE id = @id;", new { id = quiet });
            return 0;
        });
        Assert.True(products.Update(TestData.Owner, first, new ProductPatch { Active = false }).Ok);

        var options = products.GetProductSupplierOptions().Value!;

        Assert.Equal(2, options.Count);
        Assert.Equal("Live Depot", options[0].Name);
        Assert.Equal(1, options[0].ProductCount);
        Assert.Equal("Quiet History Depot", options[1].Name);
        Assert.Equal(1, options[1].ProductCount);
    }

    [Fact]
    public void GetProductSuppliers_PreferredFirstThenName()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var zebra = TestData.AddSupplier(test, "Zebra Depot");
        var alpha = TestData.AddSupplier(test, "Alpha History Depot");
        var middle = TestData.AddSupplier(test, "Middle History Depot");
        var id = CreateProduct(products, "Ordered tablet", new ProductPatch { PreferredSupplierId = zebra });
        AddHistory(test, id, middle, "PINV-0004");
        AddHistory(test, id, alpha, "PINV-0005");

        var directory = products.GetProductSuppliers(id).Value!;

        Assert.Equal(3, directory.Count);
        Assert.Equal("Zebra Depot", directory[0].SupplierName);
        Assert.True(directory[0].IsPreferred);
        Assert.Equal("Alpha History Depot", directory[1].SupplierName);
        Assert.Equal("Middle History Depot", directory[2].SupplierName);
    }
}
