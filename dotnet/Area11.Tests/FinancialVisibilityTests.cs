using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>D-110: independent visibility grants plus common master switch, not session certification.</summary>
public sealed class FinancialVisibilityTests
{
    private static ProductService Products(TestDatabase t) => new(t.Database, t.Audit, t.Log, permissions: TestData.Permissions(t), settings: t.Settings);
    private static ProductHistoryService History(TestDatabase t) => new(t.Database, t.Settings, TestData.Permissions(t), t.Log);
    private static ReorderService Pad(TestDatabase t) => new(t.Database, t.Audit, t.Settings, TestData.Permissions(t), t.Log);
    private static void Grant(TestDatabase t, User user, string module, bool allowed = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, module, allowed).Ok);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static (int Sold, int Dead) Seed(TestDatabase t)
    {
        var id = TestData.AddProduct(t, batchQty: 0, name: "Sold medicine");
        var buy = TestData.Purchases(t).Create(new PurchaseInput
        {
            SupplierId = TestData.AddSupplier(t),
            Lines = new List<PurchaseLineInput> { new() { ProductId = id, Unit = "base", Quantity = 10,
                CostPaisa = 20000, RetailPaisa = 30000, BatchNo = "V7", Expiry = "2027-06" } }
        }, TestData.Owner);
        Assert.True(buy.Ok, buy.Error);
        var sale = TestData.Sales(t).Create(TestData.Cart(id), TestData.Owner); Assert.True(sale.Ok, sale.Error);
        var dead = TestData.AddProduct(t, costPaisa: 12345, batchQty: 3, name: "Dead medicine");
        Execute(t, "UPDATE products SET reorder_level=20;"); return (id, dead);
    }
    private static User Staff(TestDatabase t)
    {
        var user = TestData.Cashier(t);
        foreach (var module in new[] { Modules.Products, Modules.Reorder, Modules.Reports }) Grant(t, user, module);
        return user;
    }
    private static ReportDeadRow Dead(TestDatabase t, User user, int id) =>
        TestData.Reports(t).Build(ReportRange.Today, user).Value!.DeadStock.Single(x => x.ProductId == id);
    private static void MaskedCost(TestDatabase t, User user, int sold, int dead)
    {
        var h = History(t).Load(user, sold).Value!;
        Assert.False(h.CanSeeCost); Assert.Equal(0L, h.BoughtPaisa); Assert.Equal("hidden", h.BoughtTotalLabel);
        Assert.All(h.Purchases, row => { Assert.False(row.CostKnown); Assert.Equal(0L, row.RatePaisa); Assert.Equal(0L, row.LineTotalPaisa); });
        var reorder = Pad(t).Report(user).Value!.Rows.Single(x => x.ProductId == sold);
        Assert.False(reorder.LastCostKnown); Assert.Equal(0L, reorder.LastCostPaisa);
        var row = Dead(t, user, dead); Assert.False(row.CostKnown); Assert.Equal(0L, row.ValuePaisa); Assert.Equal("hidden", row.ValueLabel);
        var product = Products(t).Get(user, sold).Value!; Assert.False(product.CostKnown); Assert.Equal(0L, product.CostPaisa);
    }

    [Fact]
    public void StaffHasTwoDistinctFinancialCheckboxKeysAndLabels()
    {
        Assert.NotEqual(Modules.Profit, Modules.PurchaseCost);
        Assert.Equal("View Profit", Modules.Label(Modules.Profit)); Assert.Equal("View Purchase Cost", Modules.Label(Modules.PurchaseCost));
        Assert.Contains(Modules.Profit, Modules.All); Assert.Contains(Modules.PurchaseCost, Modules.All);
    }
    [Fact]
    public void NonOwnerRolesHaveNoAutomaticCostOrProfitVisibility()
    {
        foreach (var role in new[] { UserRole.Manager, UserRole.Salesman, UserRole.Cashier })
        {
            Assert.False(PermissionService.RoleDefault(role, Modules.PurchaseCost));
            Assert.False(PermissionService.RoleDefault(role, Modules.Profit));
        }
    }
    [Fact]
    public void CostOnlyGrantWorksAcrossNamedViewsWithoutGrantingProfit()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = Staff(t); Grant(t, user, Modules.PurchaseCost);
        var h = History(t).Load(user, ids.Sold).Value!;
        Assert.True(h.CanSeeCost); Assert.Equal(200000L, h.BoughtPaisa); Assert.False(h.CanSeeProfit); Assert.Equal(0L, h.ProfitPaisa);
        Assert.Equal(20000L, Pad(t).Report(user).Value!.Rows.Single(x => x.ProductId == ids.Sold).LastCostPaisa);
        Assert.Equal(37035L, Dead(t, user, ids.Dead).ValuePaisa); Assert.True(Dead(t, user, ids.Dead).CostKnown);
        var p = Products(t).Get(user, ids.Sold).Value!; Assert.True(p.CostKnown); Assert.False(p.MarginKnown);
    }
    [Fact]
    public void ProfitOnlyGrantDoesNotReturnNamedPurchaseCostFields()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = Staff(t); Grant(t, user, Modules.Profit);
        MaskedCost(t, user, ids.Sold, ids.Dead);
        Assert.Equal(10000L, History(t).Load(user, ids.Sold).Value!.ProfitPaisa);
        var p = Products(t).Get(user, ids.Sold).Value!;
        Assert.True(p.MarginKnown); Assert.Equal("33.3%", p.MarginLabel); Assert.Equal(0L, p.CostPaisa);
    }
    [Fact]
    public void BothGrantsExposeBothWithoutRoleBasedManagementCondition()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = Staff(t);
        Grant(t, user, Modules.Profit); Grant(t, user, Modules.PurchaseCost);
        var h = History(t).Load(user, ids.Sold).Value!; Assert.True(h.CanSeeCost); Assert.True(h.CanSeeProfit);
        Assert.Equal(200000L, h.BoughtPaisa); Assert.Equal(10000L, h.ProfitPaisa);
    }
    [Fact]
    public void MainSwitchOffMasksBothEvenForOwner()
    {
        using var t = new TestDatabase(); var ids = Seed(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        MaskedCost(t, TestData.Owner, ids.Sold, ids.Dead);
        Assert.False(History(t).Load(TestData.Owner, ids.Sold).Value!.CanSeeProfit);
        Assert.False(Products(t).Get(TestData.Owner, ids.Sold, ProductReadPurpose.Purchase).Ok);
    }
    [Fact]
    public void MainSwitchOnRestoresOriginalCostAndProfitWithoutReposting()
    {
        using var t = new TestDatabase(); var ids = Seed(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        MaskedCost(t, TestData.Owner, ids.Sold, ids.Dead);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        Assert.Equal(200000L, History(t).Load(TestData.Owner, ids.Sold).Value!.BoughtPaisa);
        Assert.Equal(10000L, History(t).Load(TestData.Owner, ids.Sold).Value!.ProfitPaisa);
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM sales;")); Assert.Equal(20000L, Number(t, "SELECT cost_paisa_at_sale FROM sale_items;"));
    }
    [Fact]
    public void RevokingCostLeavesProfitGrantIndependentOnNextRead()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = Staff(t);
        Grant(t, user, Modules.Profit); Grant(t, user, Modules.PurchaseCost); Grant(t, user, Modules.PurchaseCost, false);
        MaskedCost(t, user, ids.Sold, ids.Dead); Assert.True(History(t).Load(user, ids.Sold).Value!.CanSeeProfit);
    }
    [Fact]
    public void RevokingProfitLeavesCostGrantIndependentOnNextRead()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = Staff(t);
        Grant(t, user, Modules.Profit); Grant(t, user, Modules.PurchaseCost); Grant(t, user, Modules.Profit, false);
        var h = History(t).Load(user, ids.Sold).Value!; Assert.True(h.CanSeeCost); Assert.False(h.CanSeeProfit);
        Assert.Equal(200000L, h.BoughtPaisa); Assert.Equal(0L, h.ProfitPaisa);
    }
    [Fact]
    public void ManagerWithoutCostGrantDoesNotBypassNewPolicy()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = TestData.AddUser(t, "manager");
        MaskedCost(t, user, ids.Sold, ids.Dead);
    }
    [Fact]
    public void OwnerCostCheckboxMayBeRevokedWithoutLosingSettingsAccess()
    {
        using var t = new TestDatabase(); var ids = Seed(t); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        var permissions = TestData.Permissions(t);
        Assert.False(permissions.Can(TestData.Owner, Modules.PurchaseCost)); Assert.False(permissions.MapFor(TestData.Owner)[Modules.PurchaseCost]);
        Assert.True(permissions.Can(TestData.Owner, Modules.Settings)); Assert.True(permissions.Can(TestData.Owner, Modules.Staff));
        MaskedCost(t, TestData.Owner, ids.Sold, ids.Dead); Grant(t, TestData.Owner, Modules.PurchaseCost);
        Assert.True(Dead(t, TestData.Owner, ids.Dead).CostKnown);
    }
    [Fact]
    public void OwnerProfitCheckboxMayBeRevokedWhileCostRemains()
    {
        using var t = new TestDatabase(); var ids = Seed(t); Grant(t, TestData.Owner, Modules.Profit, false);
        var h = History(t).Load(TestData.Owner, ids.Sold).Value!; Assert.True(h.CanSeeCost); Assert.False(h.CanSeeProfit);
        Assert.False(TestData.Permissions(t).MapFor(TestData.Owner)[Modules.Profit]);
        Assert.False(TestData.Permissions(t).Set(TestData.Owner, 1, Modules.Settings, false).Ok);
    }
    [Fact]
    public void ManagerCannotAdministerFinancialCheckboxesEvenWithStaffGrant()
    {
        using var t = new TestDatabase(); var manager = TestData.AddUser(t, "manager"); var target = Staff(t);
        Grant(t, manager, Modules.Staff);
        Assert.False(TestData.Permissions(t).Set(manager, target.Id, Modules.PurchaseCost, true).Ok);
        Assert.False(TestData.Permissions(t).Set(manager, target.Id, Modules.Profit, true).Ok);
    }
    [Fact]
    public void FinancialGrantReadsRejectForgedMissingAndInactiveAccounts()
    {
        using var t = new TestDatabase(); var user = Staff(t); Grant(t, user, Modules.Profit); Grant(t, user, Modules.PurchaseCost);
        var permissions = TestData.Permissions(t);
        foreach (var actor in new[] { new User { Id = user.Id, Role = "owner" }, new User { Id = int.MaxValue, Role = "owner" }, new User { Id = 1, Role = "owner", Active = false } })
        {
            Assert.False(permissions.Can(actor, Modules.Profit)); Assert.False(permissions.Can(actor, Modules.PurchaseCost));
        }
        Execute(t, "UPDATE users SET active=0 WHERE role='cashier';");
        Assert.False(permissions.Can(user, Modules.Profit)); Assert.False(permissions.Can(user, Modules.PurchaseCost));
    }
    [Fact]
    public void FinancialDatabaseReadFailureDoesNotFallBackToOwnerVisibility()
    {
        using var t = new TestDatabase(); var permissions = TestData.Permissions(t); Execute(t, "DROP TABLE user_permissions;");
        Assert.False(permissions.Can(TestData.Owner, Modules.Profit)); Assert.False(permissions.Can(TestData.Owner, Modules.PurchaseCost));
        Assert.True(permissions.Can(TestData.Owner, Modules.Settings)); // legacy non-financial owner access unchanged
    }
    [Fact]
    public void CostGrantDoesNotGrantReportsProductsOrReorderModule()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = TestData.Cashier(t); Grant(t, user, Modules.PurchaseCost);
        Assert.False(Products(t).Get(user, ids.Sold).Ok); Assert.False(History(t).Load(user, ids.Sold).Ok);
        Assert.False(Pad(t).Report(user).Ok); Assert.False(TestData.Reports(t).Build(ReportRange.Today, user).Ok);
    }
    [Fact]
    public void HistoryCsvBothPurchaseMoneyCellsFollowCostNotProfitGrant()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = Staff(t); Grant(t, user, Modules.Profit);
        Grant(t, user, Modules.Import);
        var hidden = History(t).BuildCsv(user, ids.Sold).Value!.Split('\n').Single(x => x.StartsWith("bought,"));
        Assert.Equal("\"\"", hidden.Split(',')[5]); Assert.Equal("\"\"", hidden.Split(',')[6]);
        Grant(t, user, Modules.PurchaseCost);
        var allowed = History(t).BuildCsv(user, ids.Sold).Value!.Split('\n').Single(x => x.StartsWith("bought,"));
        Assert.Equal("20000", allowed.Split(',')[5]); Assert.Equal("200000", allowed.Split(',')[6]);
    }
    [Fact]
    public void QuoteProfitOnlyReturnsActualProfitButNoAllocatedCost()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = Staff(t); Grant(t, user, Modules.Profit);
        var quote = TestData.Sales(t).Quote(TestData.Cart(ids.Sold), user); Assert.True(quote.Ok, quote.Error);
        var row = Assert.Single(quote.Value!.Lines); Assert.False(row.CostKnown); Assert.Equal(0L, row.CostPaisa);
        Assert.True(row.ProfitKnown); Assert.Equal(10000L, row.ProfitPaisa); Assert.Equal(30000L, row.NetPaisa);
    }
    [Fact]
    public void QuoteCostOnlyDoesNotReturnExplicitProfit()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = Staff(t); Grant(t, user, Modules.PurchaseCost);
        var row = Assert.Single(TestData.Sales(t).Quote(TestData.Cart(ids.Sold), user).Value!.Lines);
        Assert.True(row.CostKnown); Assert.Equal(20000L, row.CostPaisa); Assert.False(row.ProfitKnown); Assert.Equal(0L, row.ProfitPaisa);
    }
    [Fact]
    public void ReportCostInputsFollowCostGrantIndependentlyOfProfitTotals()
    {
        using var t = new TestDatabase(); Seed(t); var user = Staff(t); Grant(t, user, Modules.PurchaseCost);
        var report = TestData.Reports(t).Build(ReportRange.Today, user).Value!;
        Assert.Equal(20000L, report.Totals.CostPaisa); Assert.Equal(0L, report.Totals.ProfitPaisa);
        Assert.True(report.Totals.CostKnown); Assert.Equal(200000L, report.Totals.PurchasesPaisa);
        Grant(t, user, Modules.PurchaseCost, false); Grant(t, user, Modules.Profit);
        var profitOnly = TestData.Reports(t).Build(ReportRange.Today, user).Value!;
        Assert.Equal(0L, profitOnly.Totals.CostPaisa); Assert.Equal(10000L, profitOnly.Totals.ProfitPaisa);
        Assert.Equal(0L, profitOnly.Totals.PurchasesPaisa); Assert.Equal("hidden", profitOnly.Totals.PurchasesText);
    }
    [Fact]
    public void MainSwitchCannotBeChangedThroughManagerOrUiOnlyPath()
    {
        using var t = new TestDatabase(); var manager = TestData.AddUser(t, "manager");
        Assert.False(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, manager).Ok);
        Assert.False(t.Settings.SetUiOnly(MasterFoundationSettings.ProfitShowMargins, false).Ok);
        Assert.True(t.Settings.GetBool(MasterFoundationSettings.ProfitShowMargins, true));
    }
    [Fact]
    public void CostPreparationRequiresCostGrantRatherThanReturningFakeZeroInputs()
    {
        using var t = new TestDatabase(); var ids = Seed(t); var user = TestData.AddUser(t, "manager");
        foreach (var purpose in new[] { ProductReadPurpose.Purchase, ProductReadPurpose.WriteOff, ProductReadPurpose.Expense })
            Assert.False(Products(t).Get(user, ids.Sold, purpose).Ok);
        Grant(t, user, Modules.PurchaseCost);
        foreach (var purpose in new[] { ProductReadPurpose.Purchase, ProductReadPurpose.WriteOff, ProductReadPurpose.Expense })
            Assert.True(Products(t).Get(user, ids.Sold, purpose).Value!.CostKnown);
    }
    [Fact]
    public void MaskedQuoteCannotBypassActualBatchCostFloorOrWriteSnapshots()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t, costPaisa: 20000, retailPaisa: 10000);
        Grant(t, TestData.Owner, Modules.PurchaseCost, false); Grant(t, TestData.Owner, Modules.Profit, false);
        Assert.False(TestData.Sales(t).Create(TestData.Cart(id), TestData.Owner).Ok);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM sales;")); Assert.Equal(20000L, Number(t, "SELECT cost_paisa FROM products;"));
    }
    [Fact]
    public void GenuineZeroValuationIsKnownForAuthorizedReaderNotHidden()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t, costPaisa: 0, batchQty: 3);
        var row = Dead(t, TestData.Owner, id); Assert.True(row.CostKnown); Assert.Equal(0L, row.ValuePaisa); Assert.NotEqual("hidden", row.ValueLabel);
        Grant(t, TestData.Owner, Modules.PurchaseCost, false); Assert.Equal("hidden", Dead(t, TestData.Owner, id).ValueLabel);
    }
}
