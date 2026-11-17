using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Phase4 portion4: scoped catalog projections. No authenticated-session/full secrecy claim.</summary>
public sealed class ProductReadBoundaryTests
{
    private static ProductService Products(TestDatabase t) => new(t.Database, t.Audit, t.Log,
        permissions: TestData.Permissions(t), settings: t.Settings);
    private static void Denied(TestDatabase t, User? actor, int id)
    {
        var service = Products(t);
        foreach (var purpose in Enum.GetValues<ProductReadPurpose>())
        {
            Assert.False(service.Search(actor, purpose: purpose).Ok);
            Assert.False(service.SearchSmart(actor, "panadl", purpose: purpose).Ok);
            Assert.False(service.Get(actor, id, purpose).Ok);
        }
    }
    private static void Masked(Product p)
    {
        Assert.False(p.CostKnown); Assert.False(p.MarginKnown);
        Assert.Equal(0L, p.CostPaisa); Assert.Equal("hidden", p.CostText);
        Assert.Equal("—", p.MarginLabel);
        Assert.Equal(30000L, p.RetailPaisa);
    }
    private static void Sell(TestDatabase t, int id)
    {
        var result = TestData.Sales(t).Create(TestData.Cart(id), TestData.Owner);
        Assert.True(result.Ok, result.Error);
    }
    private static long Scalar(TestDatabase t, string sql)
    {
        using var c = t.Database.Open();
        return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }

    [Fact]
    public void AnonymousCatalogAndWorkflowReadsRefuse()
    {
        using var t = new TestDatabase(); Denied(t, null, TestData.AddProduct(t));
    }

    [Fact]
    public void ForgedOwnerRoleCannotReadProductsThroughAnotherAccount()
    {
        using var t = new TestDatabase(); var cashier = TestData.Cashier(t);
        Denied(t, new User { Id = cashier.Id, Role = "owner" }, TestData.AddProduct(t));
    }

    [Fact]
    public void InactiveAndMissingAccountsCannotUseProductPurposeDefaults()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t);
        Denied(t, new User { Id = int.MaxValue, Role = "owner" }, id);
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"); });
        Denied(t, TestData.Owner, id);
    }

    [Fact]
    public void StaleRoleSnapshotsMustSignInAgainForCatalogReads()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t);
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET role='manager' WHERE id=1;"); });
        Denied(t, TestData.Owner, id);
    }

    [Fact]
    public void CounterPermissionDoesNotGrantCatalogOrReceivingReads()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var user = TestData.Cashier(t);
        var service = Products(t);
        Assert.False(service.Search(user).Ok);
        Assert.False(service.Get(user, id).Ok);
        Assert.False(service.Search(user, purpose: ProductReadPurpose.Purchase).Ok);
        Assert.False(service.Search(user, purpose: ProductReadPurpose.SupplierReturn).Ok);
    }

    [Fact]
    public void CounterReadsKeepRetailAndStockButNeverRawCatalogCost()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var user = TestData.Cashier(t);
        var service = Products(t);
        var row = service.Search(user, purpose: ProductReadPurpose.Counter).Value!.Single();
        Assert.Equal(id, row.Id); Assert.Equal(100d, row.StockBase); Masked(row);
        Masked(service.Get(user, id, ProductReadPurpose.Counter).Value!);
    }

    [Fact]
    public void ProductModuleAndProfitGrantAloneDoNotRevealCashierCatalogCost()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var user = TestData.Cashier(t);
        var permissions = TestData.Permissions(t);
        Assert.True(permissions.Set(TestData.Owner, user.Id, Modules.Products, true).Ok);
        Assert.True(permissions.Set(TestData.Owner, user.Id, Modules.Profit, true).Ok);
        var row = Products(t).Get(user, id).Value!;
        Assert.False(row.CostKnown); Assert.Equal(0L, row.CostPaisa);
        Assert.True(row.MarginKnown); Assert.Equal("33.3%", row.MarginLabel);
    }

    [Fact]
    public void OwnerCounterAndProvisionalReadsAreStillCostFree()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var service = Products(t);
        foreach (var purpose in new[] { ProductReadPurpose.Counter, ProductReadPurpose.ProvisionalReturn })
        {
            Masked(service.Search(TestData.Owner, purpose: purpose).Value!.Single());
            Masked(service.Get(TestData.Owner, id, purpose).Value!);
        }
    }

    [Fact]
    public void OwnerCatalogCostAndSellingMarginRemainAvailable()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t);
        var row = Products(t).Get(TestData.Owner, id).Value!;
        Assert.True(row.CostKnown); Assert.Equal(20000L, row.CostPaisa);
        Assert.True(row.MarginKnown); Assert.Equal("33.3%", row.MarginLabel);
    }

    [Fact]
    public void ManagerMayPrepareCatalogCostWithoutAnAutomaticProfitGrant()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var user = TestData.AddUser(t, "manager");
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, Modules.PurchaseCost, true).Ok);
        var row = Products(t).Get(user, id).Value!;
        Assert.True(row.CostKnown); Assert.Equal(20000L, row.CostPaisa);
        Assert.False(row.MarginKnown); Assert.Equal("—", row.MarginLabel);
    }

    [Fact]
    public void GlobalFinancialSwitchMasksCostAndMarginWithoutRewritingStoredCost()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var service = Products(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var hidden = service.Get(TestData.Owner, id).Value!;
        Assert.False(hidden.CostKnown); Assert.Equal(0L, hidden.CostPaisa); Assert.False(hidden.MarginKnown);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        Assert.True(service.Get(TestData.Owner, id).Value!.MarginKnown);
    }

    [Fact]
    public void ExactTokenAndFuzzyCounterRowsShareTheSameProjection()
    {
        using var t = new TestDatabase(); TestData.AddProduct(t, name: "Panadol 500mg");
        var user = TestData.Cashier(t); var service = Products(t);
        Masked(service.Search(user, "panadol 500mg", purpose: ProductReadPurpose.Counter).Value!.Single());
        var close = service.SearchSmart(user, "panadl", purpose: ProductReadPurpose.Counter);
        Assert.True(close.Ok, close.Error); Assert.True(close.Value!.IsFuzzy); Assert.NotEmpty(close.Value.Items);
        Assert.All(close.Value.Items, Masked);
    }

    [Fact]
    public void OperationalCostPreparationNeedsModuleAndSeparateCostGrant()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var user = TestData.Cashier(t);
        var purposes = new[] { ProductReadPurpose.Purchase, ProductReadPurpose.SupplierReturn, ProductReadPurpose.WriteOff, ProductReadPurpose.Expense };
        var modules = new[] { Modules.Purchases, Modules.Suppliers, Modules.Stock, Modules.Cash };
        for (var n = 0; n < purposes.Length; n++)
        {
            Assert.False(Products(t).Get(user, id, purposes[n]).Ok);
            Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, modules[n], true).Ok);
            if (n == 0)
            {
                Assert.False(Products(t).Get(user, id, purposes[n]).Ok);
                Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, Modules.PurchaseCost, true).Ok);
            }
            var row = Products(t).Get(user, id, purposes[n]).Value!;
            Assert.True(row.CostKnown); Assert.Equal(20000L, row.CostPaisa);
            Assert.False(row.MarginKnown);
        }
        Assert.False(Products(t).Get(user, id).Ok); // no implicit Products grant
    }

    [Fact]
    public void PurposeGrantRevocationAffectsNextReadOnSameService()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var user = TestData.Cashier(t);
        var service = Products(t); var permissions = TestData.Permissions(t);
        Assert.True(permissions.Set(TestData.Owner, user.Id, Modules.Purchases, true).Ok);
        Assert.True(permissions.Set(TestData.Owner, user.Id, Modules.PurchaseCost, true).Ok);
        Assert.True(service.Get(user, id, ProductReadPurpose.Purchase).Ok);
        Assert.True(permissions.Set(TestData.Owner, user.Id, Modules.Purchases, false).Ok);
        Assert.False(service.Search(user, purpose: ProductReadPurpose.Purchase).Ok);
        Assert.False(service.Get(user, id, ProductReadPurpose.Purchase).Ok);
    }

    [Fact]
    public void UnknownPurposeNeverFallsBackToOwnerOrUnrestrictedQuery()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var purpose = (ProductReadPurpose)999;
        Assert.False(Products(t).Search(TestData.Owner, purpose: purpose).Ok);
        Assert.False(Products(t).SearchSmart(TestData.Owner, "panadl", purpose: purpose).Ok);
        Assert.False(Products(t).Get(TestData.Owner, id, purpose).Ok);
    }

    [Fact]
    public void LegacyConstructorAlsoReadsCurrentMarginSettingOnEachRequest()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t);
        var service = new ProductService(t.Database, t.Audit, t.Log);
        Assert.True(service.Get(TestData.Owner, id).Value!.MarginKnown);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.False(service.Get(TestData.Owner, id).Value!.MarginKnown);
    }

    [Fact]
    public void CostFreeCounterResultDoesNotZeroActualPostedBatchCost()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t);
        var row = Products(t).Get(TestData.Owner, id, ProductReadPurpose.Counter).Value!; Masked(row);
        Sell(t, row.Id);
        Assert.Equal(20000L, Scalar(t, "SELECT cost_paisa_at_sale FROM sale_items;"));
        Assert.Equal(30000L, Scalar(t, "SELECT total_paisa FROM sales;"));
    }

    [Fact]
    public void CostFreeCatalogObjectCannotBypassCoreActualCostFloor()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t, costPaisa: 20000, retailPaisa: 10000);
        var row = Products(t).Get(TestData.Owner, id, ProductReadPurpose.Counter).Value!;
        Assert.False(row.CostKnown); Assert.Equal(0L, row.CostPaisa);
        Assert.False(TestData.Sales(t).Create(TestData.Cart(row.Id), TestData.Owner).Ok);
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM sales;"));
    }

    [Fact]
    public void ManagerReportDoesNotReturnRawHiddenCostInputs()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t);
        var sale = TestData.Sales(t).Create(TestData.Cart(id, qty: 2), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        var returned = TestData.Returns(t).Create(TestData.Return(sale.Value!, 1), TestData.Owner);
        Assert.True(returned.Ok, returned.Error);
        var owner = TestData.Reports(t).Build(ReportRange.Last30, TestData.Owner).Value!;
        Assert.Equal(40000L, owner.Totals.CostPaisa); Assert.Equal(20000L, owner.Totals.RestockCostPaisa);
        var report = TestData.Reports(t).Build(ReportRange.Last30, TestData.AddUser(t, "manager"));
        Assert.True(report.Ok, report.Error); Assert.False(report.Value!.CanSeeProfit);
        Assert.Equal(0L, report.Value.Totals.CostPaisa); Assert.Equal(0L, report.Value.Totals.RestockCostPaisa);
        Assert.Equal(0L, report.Value.Totals.ProfitPaisa); Assert.Equal(60000L, report.Value.Totals.SalesPaisa);
        Assert.Equal(30000L, report.Value.Totals.RefundPaisa);
    }

    [Fact]
    public void OwnerReportGlobalOffMasksCostsWithoutChangingProfitComputationWhenOn()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); Sell(t, id); var service = TestData.Reports(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var hidden = service.Build(ReportRange.Last30, TestData.Owner).Value!;
        Assert.False(hidden.CanSeeProfit); Assert.Equal(0L, hidden.Totals.CostPaisa); Assert.Equal(0L, hidden.Totals.ProfitPaisa);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        var visible = service.Build(ReportRange.Last30, TestData.Owner).Value!;
        Assert.Equal(20000L, visible.Totals.CostPaisa); Assert.Equal(10000L, visible.Totals.ProfitPaisa);
        Assert.Equal(30000L, visible.Totals.SalesPaisa); Assert.Equal(1L, Scalar(t, "SELECT COUNT(*) FROM sales;"));
    }
}
