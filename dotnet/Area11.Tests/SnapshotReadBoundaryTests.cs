using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Focused snapshot/export boundaries; not authenticated-session or Windows tests.</summary>
public sealed class SnapshotReadBoundaryTests
{
    private const long Cost = 18761;
    private static ProductHistoryService History(TestDatabase t) => new(t.Database, t.Settings, TestData.Permissions(t), t.Log);
    private static ReorderService Pad(TestDatabase t) => new(t.Database, t.Audit, t.Settings, TestData.Permissions(t), t.Log);
    private static DashboardService Dashboard(TestDatabase t) => new(t.Database, t.Settings, TestData.Permissions(t), t.Log);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Scalar(TestDatabase t, string sql)
    {
        using var c = t.Database.Open();
        return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static void Grant(TestDatabase t, User user, string module, bool allowed = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, module, allowed).Ok);
    private static int Seed(TestDatabase t, bool sell = true, long cost = Cost)
    {
        var id = TestData.AddProduct(t, batchQty: 0, name: "Med");
        var buy = TestData.Purchases(t).Create(new PurchaseInput
        {
            SupplierId = TestData.AddSupplier(t),
            Lines = new List<PurchaseLineInput>
            {
                new() { ProductId = id, Unit = "base", Quantity = 10, CostPaisa = cost,
                    RetailPaisa = 30000, BatchNo = "H5", Expiry = "2027-06", IsSample = cost == 0 }
            }
        }, TestData.Owner);
        Assert.True(buy.Ok, buy.Error);
        Execute(t, "UPDATE products SET reorder_level=20;");
        if (sell)
        {
            var sale = TestData.Sales(t).Create(TestData.Cart(id), TestData.Owner);
            Assert.True(sale.Ok, sale.Error);
        }
        return id;
    }
    private static void Denied(TestDatabase t, User? actor, int id)
    {
        Assert.False(History(t).Load(actor, id).Ok);
        Assert.False(History(t).BuildCsv(actor, id).Ok);
        Assert.False(History(t).BuildDocument(actor, id).Ok);
        Assert.False(Pad(t).Report(actor).Ok);
        Assert.False(TestData.Reports(t).Build(ReportRange.Today, actor).Ok);
        Assert.False(TestData.Reports(t).Days(ReportRange.Today, actor).Ok);
        Assert.False(TestData.Reports(t).BuildDocument(ReportRange.Today, actor).Ok);
        Assert.False(Dashboard(t).GetSnapshot(actor).Ok);
    }
    private static void Masked(ProductHistory h)
    {
        Assert.False(h.CanSeeCost); Assert.Equal(0L, h.BoughtPaisa); Assert.Equal("hidden", h.BoughtTotalLabel);
        var row = Assert.Single(h.Purchases);
        Assert.False(row.CostKnown); Assert.Equal(0L, row.RatePaisa); Assert.Equal(0L, row.LineTotalPaisa);
        Assert.Equal("hidden", row.RateLabel); Assert.Equal("hidden", row.TotalLabel);
        Assert.Equal(10d, row.QtyBase); Assert.NotEmpty(row.SupplierName); Assert.NotEmpty(row.BillCode);
    }

    [Fact]
    public void AnonymousSnapshotAndHistoryExportReadsRefuse()
    {
        using var t = new TestDatabase(); Denied(t, null, Seed(t));
    }

    [Fact]
    public void ForgedOwnerLabelOnRealCashierCannotReadSnapshots()
    {
        using var t = new TestDatabase(); var id = Seed(t); var user = TestData.Cashier(t);
        Denied(t, new User { Id = user.Id, Role = "owner" }, id);
    }

    [Fact]
    public void MissingOwnerIdIsNotAReaderCredential()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = int.MaxValue, Role = "owner" }, Seed(t));
    }

    [Fact]
    public void InactiveDatabaseOwnerCannotUseSnapshotOrExportDefaults()
    {
        using var t = new TestDatabase(); var id = Seed(t);
        Execute(t, "UPDATE users SET active=0 WHERE id=1;"); Denied(t, TestData.Owner, id);
    }

    [Fact]
    public void StaleOwnerRoleMustSignInAgainBeforeSnapshots()
    {
        using var t = new TestDatabase(); var id = Seed(t);
        Execute(t, "UPDATE users SET role='manager' WHERE id=1;"); Denied(t, TestData.Owner, id);
    }

    [Fact]
    public void InactiveActorSnapshotDoesNotUseActiveDatabaseAccount()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = 1, Role = "owner", Active = false }, Seed(t));
    }

    [Fact]
    public void ExistingModuleDenialsStillApplyIncludingBothDashboardAlternatives()
    {
        using var t = new TestDatabase(); var id = Seed(t); var user = TestData.Cashier(t);
        Grant(t, user, Modules.Pos, false); Grant(t, user, Modules.Reports, false);
        Denied(t, user, id);
    }

    [Fact]
    public void DashboardKeepsPosOrReportsRatherThanRequiringBoth()
    {
        using var t = new TestDatabase(); Seed(t); var user = TestData.Cashier(t);
        Assert.True(Dashboard(t).GetSnapshot(user).Ok);
        Grant(t, user, Modules.Pos, false); Grant(t, user, Modules.Reports);
        var result = Dashboard(t).GetSnapshot(user);
        Assert.True(result.Ok, result.Error); Assert.Equal(30000L, result.Value!.TodaySalesPaisa);
        Assert.False(result.Value.CanSeeProfit); Assert.Equal(0L, result.Value.TodayProfitPaisa);
    }

    [Fact]
    public void SameSnapshotServicesRespectNextReadModuleRevocation()
    {
        using var t = new TestDatabase(); var id = Seed(t); var user = TestData.AddUser(t, "manager");
        var history = History(t); var pad = Pad(t); var reports = TestData.Reports(t); var dashboard = Dashboard(t);
        Assert.True(history.Load(user, id).Ok); Assert.True(pad.Report(user).Ok);
        Assert.True(reports.Days(ReportRange.Today, user).Ok); Assert.True(dashboard.GetSnapshot(user).Ok);
        foreach (var module in new[] { Modules.Products, Modules.Reorder, Modules.Reports, Modules.Pos }) Grant(t, user, module, false);
        Assert.False(history.Load(user, id).Ok); Assert.False(pad.Report(user).Ok);
        Assert.False(reports.Build(ReportRange.Today, user).Ok); Assert.False(reports.Days(ReportRange.Today, user).Ok);
        Assert.False(dashboard.GetSnapshot(user).Ok);
    }

    [Fact]
    public void StaffHistoryKeepsPurchaseQuantityButMasksBothCostInputsAndAggregate()
    {
        using var t = new TestDatabase(); var id = Seed(t); var user = TestData.AddUser(t, "salesman");
        var h = History(t).Load(user, id).Value!; Masked(h);
        Assert.Equal(30000L, h.SoldPaisa); Assert.Equal(1d, h.SoldQty); Assert.Equal(0L, h.ProfitPaisa);
    }

    [Fact]
    public void ProfitGrantDoesNotOverrideIndependentHistoricalCostGrant()
    {
        using var t = new TestDatabase(); var id = Seed(t); var user = TestData.Cashier(t);
        foreach (var module in new[] { Modules.Products, Modules.Purchases, Modules.Suppliers, Modules.Profit, Modules.Reorder }) Grant(t, user, module);
        Masked(History(t).Load(user, id).Value!);
        var row = Assert.Single(Pad(t).Report(user).Value!.Rows);
        Assert.False(row.LastCostKnown); Assert.Equal(0L, row.LastCostPaisa);
    }

    [Fact]
    public void ManagerNeedsSeparateCostGrantAndMainSwitchForHistoricalCost()
    {
        using var t = new TestDatabase(); var id = Seed(t); var user = TestData.AddUser(t, "manager");
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var h = History(t).Load(user, id).Value!;
        Masked(h); Assert.False(h.CanSeeProfit);
        Grant(t, user, Modules.PurchaseCost);
        Masked(History(t).Load(user, id).Value!); // grant does not override main OFF
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        var allowed = History(t).Load(user, id).Value!;
        Assert.True(allowed.CanSeeCost); Assert.False(allowed.CanSeeProfit); Assert.Equal(Cost * 10, allowed.BoughtPaisa);
    }

    [Fact]
    public void OwnerHistoryGlobalSwitchMasksMoneyThenRestoresSnapshots()
    {
        using var t = new TestDatabase(); var id = Seed(t); var service = History(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var hidden = service.Load(TestData.Owner, id).Value!;
        Masked(hidden); Assert.Equal(0L, hidden.ProfitPaisa);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        Assert.Equal(30000L - Cost, service.Load(TestData.Owner, id).Value!.ProfitPaisa);
    }

    [Fact]
    public void HistoryCsvLeavesBothDeniedPurchaseMoneyCellsEmptyButKeepsSales()
    {
        using var t = new TestDatabase(); var id = Seed(t); var user = TestData.AddUser(t, "salesman");
        var csv = History(t).BuildCsv(user, id); Assert.True(csv.Ok, csv.Error);
        var lines = csv.Value!.Split('\n'); var bought = lines.Single(x => x.StartsWith("bought,"));
        var cells = bought.Trim().Split(','); Assert.Equal("10", cells[4]); Assert.Equal("\"\"", cells[5]); Assert.Equal("\"\"", cells[6]);
        Assert.DoesNotContain(Cost.ToString(), bought); Assert.Contains("sold,", csv.Value);
        Assert.Contains(",30000,30000,", csv.Value);
    }

    [Fact]
    public void HistoryDocumentMasksPurchaseRowsAndSummaryNotOnlyScreenColumns()
    {
        using var t = new TestDatabase(); var id = Seed(t); var user = TestData.AddUser(t, "salesman");
        var doc = History(t).BuildDocument(user, id); Assert.True(doc.Ok, doc.Error);
        Assert.Contains("hidden", doc.Value!.PlainText); Assert.Contains("BOUGHT", doc.Value.PlainText);
        Assert.DoesNotContain(Money.Format(Cost), doc.Value.PlainText);
        Assert.DoesNotContain(Money.Format(Cost * 10), doc.Value.PlainText);
        Assert.Contains("SOLD", doc.Value.PlainText);
    }

    [Fact]
    public void HistoryDocumentAndCsvRevalidateBeforeEachNewPreparation()
    {
        using var t = new TestDatabase(); var id = Seed(t); var user = TestData.AddUser(t, "manager"); var service = History(t);
        Assert.True(service.BuildCsv(user, id).Ok); Assert.True(service.BuildDocument(user, id).Ok);
        Grant(t, user, Modules.Products, false);
        Assert.False(service.BuildCsv(user, id).Ok); Assert.False(service.BuildDocument(user, id).Ok);
    }

    [Fact]
    public void ReorderOnlyGrantDoesNotRevealLastPurchaseCost()
    {
        using var t = new TestDatabase(); Seed(t); var user = TestData.Cashier(t); Grant(t, user, Modules.Reorder);
        var row = Assert.Single(Pad(t).Report(user).Value!.Rows);
        Assert.False(row.LastCostKnown); Assert.Equal(0L, row.LastCostPaisa);
        Assert.True(row.HasSupplier); Assert.Equal(9d, row.StockQty); Assert.Equal(31d, row.SuggestQty);
    }

    [Fact]
    public void OwnerAndManagerReorderRetainKnownLastCost()
    {
        using var t = new TestDatabase(); Seed(t); var manager = TestData.AddUser(t, "manager");
        Grant(t, manager, Modules.PurchaseCost);
        foreach (var user in new[] { TestData.Owner, manager })
        {
            var row = Assert.Single(Pad(t).Report(user).Value!.Rows);
            Assert.True(row.LastCostKnown); Assert.Equal(Cost, row.LastCostPaisa);
        }
    }

    [Fact]
    public void AbsentPurchaseIsUnknownButAuthorizedActualZeroIsKnown()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t, batchQty: 0);
        var missing = Assert.Single(Pad(t).Report(TestData.Owner).Value!.Rows);
        Assert.False(missing.LastCostKnown); Assert.Equal(0L, missing.LastCostPaisa);
        var zeroId = Seed(t, sell: false, cost: 0);
        var row = Pad(t).Report(TestData.Owner).Value!.Rows.Single(x => x.ProductId == zeroId);
        Assert.True(row.LastCostKnown); Assert.Equal(0L, row.LastCostPaisa);
        var h = History(t).Load(TestData.Owner, zeroId).Value!;
        Assert.True(h.Purchases[0].CostKnown); Assert.Equal(Money.Format(0), h.Purchases[0].TotalLabel);
    }

    [Fact]
    public void DisabledReorderStillChecksCurrentReaderBeforeEarlySuccess()
    {
        using var t = new TestDatabase(); Seed(t); Assert.True(t.Settings.Set("reorder.enabled", false, TestData.Owner).Ok);
        Assert.True(Pad(t).Report(TestData.Owner).Value!.IsSwitchedOff);
        Execute(t, "UPDATE users SET active=0 WHERE id=1;"); Assert.False(Pad(t).Report(TestData.Owner).Ok);
        Assert.False(Pad(t).Report(null).Ok);
    }

    [Fact]
    public void PublicDaysRequiresReportModuleAndRespectsGlobalProfitToggle()
    {
        using var t = new TestDatabase(); Seed(t); var service = TestData.Reports(t); var user = TestData.Cashier(t);
        Assert.False(service.Days(ReportRange.Today, user).Ok); Grant(t, user, Modules.Reports);
        var staff = service.Days(ReportRange.Today, user).Value!;
        Assert.Equal(30000L, staff.Sum(x => x.SalesPaisa)); Assert.All(staff, row => Assert.Equal(0L, row.ProfitPaisa));
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.All(service.Days(ReportRange.Today, TestData.Owner).Value!, row => Assert.Equal(0L, row.ProfitPaisa));
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        Assert.Equal(30000L - Cost, service.Days(ReportRange.Today, TestData.Owner).Value!.Sum(x => x.ProfitPaisa));
        Assert.Equal(service.Build(ReportRange.Today, TestData.Owner).Value!.Totals.ProfitPaisa,
            service.Days(ReportRange.Today, TestData.Owner).Value!.Sum(x => x.ProfitPaisa));
    }

    [Fact]
    public void DashboardProfitGrantAndGlobalSwitchControlActualSnapshot()
    {
        using var t = new TestDatabase(); Seed(t); var user = TestData.AddUser(t, "manager"); var service = Dashboard(t);
        Assert.False(service.GetSnapshot(user).Value!.CanSeeProfit); Grant(t, user, Modules.Profit);
        Assert.Equal(30000L - Cost, service.GetSnapshot(user).Value!.TodayProfitPaisa);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var hidden = service.GetSnapshot(user).Value!;
        Assert.False(hidden.CanSeeProfit); Assert.Equal(0L, hidden.TodayProfitPaisa); Assert.Equal(30000L, hidden.TodaySalesPaisa);
    }

    [Fact]
    public void ReportAndNewDocumentKeepDeniedProfitOutOfAllFocusedOutputs()
    {
        using var t = new TestDatabase(); Seed(t); var user = TestData.AddUser(t, "manager"); var service = TestData.Reports(t);
        var report = service.Build(ReportRange.Today, user).Value!;
        Assert.False(report.CanSeeProfit); Assert.Equal(0L, report.Totals.CostPaisa); Assert.Equal(0L, report.Totals.ProfitPaisa);
        Assert.All(report.Days, row => Assert.Equal(0L, row.ProfitPaisa));
        Assert.All(report.TopByQty, row => Assert.Equal(0L, row.ProfitPaisa)); Assert.Empty(report.TopByProfit);
        var doc = service.BuildDocument(ReportRange.Today, user); Assert.True(doc.Ok, doc.Error);
        Assert.DoesNotContain("Profit", doc.Value!.PlainText);
    }

    [Fact]
    public void MaskedSnapshotAndExportReadsNeverRewriteFinancialSnapshots()
    {
        using var t = new TestDatabase(); var id = Seed(t); var user = TestData.AddUser(t, "salesman");
        Grant(t, user, Modules.Reorder); Grant(t, user, Modules.Reports);
        Masked(History(t).Load(user, id).Value!); Assert.True(History(t).BuildCsv(user, id).Ok);
        Assert.True(History(t).BuildDocument(user, id).Ok); Assert.True(Pad(t).Report(user).Ok);
        Assert.True(Dashboard(t).GetSnapshot(user).Ok); Assert.True(TestData.Reports(t).Days(ReportRange.Today, user).Ok);
        Assert.Equal(Cost, Scalar(t, "SELECT cost_paisa_at_sale FROM sale_items;"));
        Assert.Equal(Cost * 10, Scalar(t, "SELECT total_paisa FROM purchases;"));
        Assert.Equal(30000L, Scalar(t, "SELECT total_paisa FROM sales;"));
        Assert.Equal(1L, Scalar(t, "SELECT COUNT(*) FROM sales;"));
    }
}
