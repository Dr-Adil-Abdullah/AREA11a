using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Portion28 real-schema catalog writer regressions. All require actual .NET execution.</summary>
public sealed class CatalogCurrentAccessTests
{
    private sealed class CatalogWriteFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public ProductService Products { get; }
        public int Category { get; }
        public int Company { get; }
        public int Formula { get; }
        public int Brand { get; }
        public int Unit { get; }
        public int Product { get; }
        public CatalogWriteFixture()
        {
            Products = new ProductService(T.Database, T.Audit, T.Log, permissions: TestData.Permissions(T), settings: T.Settings);
            Category = Added(Products.AddCategory(TestData.Owner, "Synthetic category", sortOrder: 10));
            Company = Added(Products.AddCompany(TestData.Owner, "Synthetic company", 10));
            Formula = Added(Products.AddFormula(TestData.Owner, "Synthetic formula", 10));
            Brand = Added(Products.AddBrand(TestData.Owner, "Synthetic brand", 10));
            Unit = Added(Products.AddUnit(TestData.Owner, "syntheticunit", 10));
            Assert.True(Products.SetBrandCompany(TestData.Owner, Brand, Company).Ok);
            Product = TestData.AddProduct(T);
            Assert.True(Products.Update(TestData.Owner, Product, new ProductPatch { CategoryId = Category, CompanyId = Company, GenericFormulaId = Formula, BrandId = Brand }).Ok);
        }
        private static int Added(ServiceResult<int> result) { Assert.True(result.Ok, result.Error); Assert.True(result.Value > 0); return result.Value; }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql)));
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "SELECT quote(id)||':'||quote(name)||':'||quote(parent_id)||':'||quote(sort_order)||':'||quote(active) FROM categories ORDER BY id;",
            "SELECT quote(id)||':'||quote(name)||':'||quote(sort_order)||':'||quote(active) FROM companies ORDER BY id;",
            "SELECT quote(id)||':'||quote(name)||':'||quote(sort_order)||':'||quote(active) FROM generic_formulas ORDER BY id;",
            "SELECT quote(id)||':'||quote(name)||':'||quote(company_id)||':'||quote(sort_order)||':'||quote(active) FROM brands ORDER BY id;",
            "SELECT quote(id)||':'||quote(name)||':'||quote(whole_only)||':'||quote(sort_order)||':'||quote(active) FROM units ORDER BY id;",
            "SELECT quote(id)||':'||quote(name)||':'||quote(category_id)||':'||quote(company_id)||':'||quote(generic_formula_id)||':'||quote(brand_id)||':'||quote(base_unit)||':'||quote(strength_unit)||':'||quote(cost_paisa)||':'||quote(retail_paisa) FROM products ORDER BY id;",
            "SELECT quote(id)||':'||quote(product_id)||':'||quote(qty_base)||':'||quote(cost_paisa) FROM batches ORDER BY id;",
            "SELECT quote(product_id)||':'||quote(category_id) FROM product_category_tags ORDER BY product_id,category_id;",
            "SELECT quote(id)||':'||quote(user_name)||':'||quote(action)||':'||quote(old_value)||':'||quote(new_value) FROM audit_logs ORDER BY id;"
        }.Select(sql => string.Join(";", T.Database.Query<string>(c, sql)))));
        public void Grant(User user, bool allowed) => Assert.True(TestData.Permissions(T).Set(TestData.Owner, user.Id, Modules.Products, allowed).Ok);
        public void Dispose() => T.Dispose();
    }

    private static ServiceResult Apply(CatalogWriteFixture f, int operation, User? user) => operation switch
    {
        0 => f.Products.AddCategory(user!, "Synthetic new category", sortOrder: 20),
        1 => f.Products.UpdateCategory(user!, f.Category, "Synthetic renamed category", null, 20),
        2 => f.Products.SetCategoryActive(user!, f.Category, false),
        3 => f.Products.AddCompany(user!, "Synthetic new company", 20),
        4 => f.Products.UpdateCompany(user!, f.Company, "Synthetic renamed company", 20),
        5 => f.Products.SetCompanyActive(user!, f.Company, false),
        6 => f.Products.AddFormula(user!, "Synthetic new formula", 20),
        7 => f.Products.UpdateFormula(user!, f.Formula, "Synthetic renamed formula", 20),
        8 => f.Products.SetFormulaActive(user!, f.Formula, false),
        9 => f.Products.AddBrand(user!, "Synthetic new brand", 20),
        10 => f.Products.UpdateBrand(user!, f.Brand, "Synthetic renamed brand", 20),
        11 => f.Products.SetBrandActive(user!, f.Brand, false),
        12 => f.Products.SetBrandCompany(user!, f.Brand, null),
        13 => f.Products.AddUnit(user!, "syntheticnewunit", 20),
        14 => f.Products.UpdateUnit(user!, f.Unit, "syntheticrenamedunit", 20, true),
        15 => f.Products.SetUnitActive(user!, f.Unit, false),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    [Theory]
    [InlineData("anonymous")]
    [InlineData("supplied-inactive")]
    [InlineData("missing")]
    [InlineData("forged")]
    [InlineData("stored-inactive")]
    [InlineData("role-changed")]
    [InlineData("revoked")]
    [InlineData("cashier-granted")]
    public void AllSixteenWritersRejectInvalidCurrentAuthorityWithoutMutation(string state)
    {
        using var f = new CatalogWriteFixture(); User? actor = TestData.Owner;
        switch (state)
        {
            case "anonymous": actor = null; break;
            case "supplied-inactive": actor = new User { Id = 1, Role = "owner", Active = false }; break;
            case "missing": actor = new User { Id = int.MaxValue, Role = "owner" }; break;
            case "forged": var cashier = TestData.Cashier(f.T); actor = new User { Id = cashier.Id, Role = "owner" }; break;
            case "stored-inactive": f.Execute("UPDATE users SET active=0 WHERE id=1;"); break;
            case "role-changed": f.Execute("UPDATE users SET role='manager' WHERE id=1;"); break;
            case "revoked": actor = TestData.AddUser(f.T, "manager"); f.Grant(actor, false); break;
            case "cashier-granted": actor = TestData.Cashier(f.T); f.Grant(actor, true); break;
        }
        var before = f.Snapshot();
        for (var operation = 0; operation < 16; operation++)
        {
            Assert.False(Apply(f, operation, actor).Ok); Assert.Equal(before, f.Snapshot());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void EachWriterAuditsCurrentStoredManagerNameWithoutFinancialGrants(int operation)
    {
        using var f = new CatalogWriteFixture(); var manager = TestData.AddUser(f.T, "manager");
        f.Execute("UPDATE users SET name='Current catalog manager' WHERE id=" + manager.Id + "; UPDATE settings SET value='false' WHERE key='profit.showMargins';");
        var count = f.Number("SELECT COUNT(*) FROM audit_logs;");
        var result = Apply(f, operation, manager); Assert.True(result.Ok, result.Error);
        Assert.Equal(count + 1, f.Number("SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal("Current catalog manager", f.T.Database.Read(c => f.T.Database.Scalar(c, "SELECT user_name FROM audit_logs ORDER BY id DESC LIMIT 1;"))?.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void EachWriterRollsBackOnAuditFaultWithoutProviderDisclosure(int operation)
    {
        using var f = new CatalogWriteFixture(); var before = f.Snapshot();
        f.Execute("CREATE TRIGGER catalog_audit_fault BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'private-synthetic-provider-marker'); END;");
        var result = Apply(f, operation, TestData.Owner); Assert.False(result.Ok);
        Assert.DoesNotContain("private-synthetic-provider-marker", result.Error!); Assert.Equal(before, f.Snapshot());
    }

    [Fact] public void CatalogReaderKeepsReadOnlyProductsAccessWithoutCostOrMain()
    {
        using var f = new CatalogWriteFixture(); var user = TestData.AddUser(f.T, "salesman"); f.Execute("UPDATE settings SET value='false' WHERE key='profit.showMargins';");
        var access = f.Products.CheckCatalogAccess(user); Assert.True(access.Ok, access.Error); Assert.False(access.Value);
    }
    [Fact] public void ManagerCatalogCapabilityRequiresProductsNotCostProfitMain()
    {
        using var f = new CatalogWriteFixture(); var user = TestData.AddUser(f.T, "manager"); f.Execute("UPDATE settings SET value='false' WHERE key='profit.showMargins';");
        Assert.True(f.Products.CheckCatalogAccess(user).Value); f.Grant(user, false); Assert.False(f.Products.CheckCatalogAccess(user).Ok);
        f.Grant(user, true); Assert.True(f.Products.CheckCatalogAccess(user).Value);
    }
    [Fact] public void CatalogCapabilityRejectsMissingInactiveAndRoleChangedReaders()
    {
        using var f = new CatalogWriteFixture(); Assert.False(f.Products.CheckCatalogAccess(null).Ok);
        Assert.False(f.Products.CheckCatalogAccess(new User { Id = int.MaxValue, Role = "owner" }).Ok);
        f.Execute("UPDATE users SET active=0 WHERE id=1;"); Assert.False(f.Products.CheckCatalogAccess(TestData.Owner).Ok);
        f.Execute("UPDATE users SET active=1,role='manager' WHERE id=1;"); Assert.False(f.Products.CheckCatalogAccess(TestData.Owner).Ok);
    }
    [Fact] public void NoOpsStillRequireCurrentWriterAndProduceNoInventedAudit()
    {
        using var f = new CatalogWriteFixture(); var before = f.Snapshot();
        Assert.True(f.Products.UpdateCategory(TestData.Owner, f.Category, "Synthetic category", null, 10).Ok);
        Assert.True(f.Products.UpdateCompany(TestData.Owner, f.Company, "Synthetic company", 10).Ok);
        Assert.True(f.Products.SetCategoryActive(TestData.Owner, f.Category, true).Ok);
        Assert.True(f.Products.SetCompanyActive(TestData.Owner, f.Company, true).Ok);
        Assert.True(f.Products.SetFormulaActive(TestData.Owner, f.Formula, true).Ok);
        Assert.True(f.Products.SetBrandActive(TestData.Owner, f.Brand, true).Ok);
        Assert.True(f.Products.SetUnitActive(TestData.Owner, f.Unit, true).Ok);
        Assert.True(f.Products.SetBrandCompany(TestData.Owner, f.Brand, f.Company).Ok); Assert.Equal(before, f.Snapshot());
        f.Execute("UPDATE users SET active=0 WHERE id=1;");
        Assert.False(f.Products.SetCategoryActive(TestData.Owner, f.Category, true).Ok);
        Assert.False(f.Products.SetCompanyActive(TestData.Owner, f.Company, true).Ok);
        Assert.False(f.Products.SetFormulaActive(TestData.Owner, f.Formula, true).Ok);
        Assert.False(f.Products.SetBrandActive(TestData.Owner, f.Brand, true).Ok);
        Assert.False(f.Products.SetUnitActive(TestData.Owner, f.Unit, true).Ok);
        Assert.False(f.Products.SetBrandCompany(TestData.Owner, f.Brand, f.Company).Ok); Assert.Equal(before, f.Snapshot());
    }
    [Fact] public void RegrantAllowsFreshWriteAfterDeniedUnlink()
    {
        using var f = new CatalogWriteFixture(); var user = TestData.AddUser(f.T, "manager"); f.Grant(user, false);
        Assert.False(f.Products.SetCategoryActive(user, f.Category, false).Ok); f.Grant(user, true);
        Assert.True(f.Products.SetCategoryActive(user, f.Category, false).Ok);
        Assert.Equal(0L, f.Number("SELECT COUNT(*) FROM products WHERE category_id=" + f.Category));
    }
    [Fact] public void UnitRenameAuditFaultPreservesLinkedBaseAndStrengthLabelsAndBatchQuantity()
    {
        using var f = new CatalogWriteFixture(); f.Execute("UPDATE products SET base_unit='syntheticunit',strength_value=2,strength_unit='syntheticunit' WHERE id=" + f.Product);
        var before = f.Snapshot(); f.Execute("CREATE TRIGGER unit_audit_fault BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'unit fault'); END;");
        Assert.False(f.Products.UpdateUnit(TestData.Owner, f.Unit, "changedunit", 30, true).Ok); Assert.Equal(before, f.Snapshot());
    }
    [Fact] public void UsedUnitPutAwayRuleIsNotBypassedByCurrentOwner()
    {
        using var f = new CatalogWriteFixture(); f.Execute("UPDATE products SET base_unit='syntheticunit' WHERE id=" + f.Product); var before = f.Snapshot();
        Assert.False(f.Products.SetUnitActive(TestData.Owner, f.Unit, false).Ok); Assert.Equal(before, f.Snapshot());
    }
    [Fact] public void CatalogPermissionReadFailureDeniesManagerWithoutRoleDefaultFallback()
    {
        using var f = new CatalogWriteFixture(); var user = TestData.AddUser(f.T, "manager"); f.Execute("DROP TABLE user_permissions;");
        Assert.False(f.Products.CheckCatalogAccess(user).Ok); var before = f.Snapshot();
        for (var operation = 0; operation < 16; operation++) Assert.False(Apply(f, operation, user).Ok);
        Assert.Equal(before, f.Snapshot());
    }
    [Fact] public void BrandNameCommitAndCompanyLinkRemainSeparateTransactions()
    {
        using var f = new CatalogWriteFixture(); var user = TestData.AddUser(f.T, "manager");
        Assert.True(f.Products.UpdateBrand(user, f.Brand, "Committed brand name", 20).Ok); f.Grant(user, false);
        Assert.False(f.Products.SetBrandCompany(user, f.Brand, null).Ok);
        var brand = f.Products.GetBrands(false).Value!.Single(row => row.Id == f.Brand);
        Assert.Equal("Committed brand name", brand.Name); Assert.Equal(f.Company, brand.CompanyId);
    }
}
