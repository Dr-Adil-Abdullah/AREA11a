using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Bounded batch projection/fresh preparation, not posting/session certification.</summary>
public sealed class BatchReadVisibilityTests
{
    private static readonly ProductReadPurpose[] Purposes =
        { ProductReadPurpose.WriteOff, ProductReadPurpose.Expense, ProductReadPurpose.SupplierReturn };
    private static StockService Stock(TestDatabase t) => new(t.Database, t.Audit, t.Settings, TestData.Permissions(t), t.Log);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static void Grant(TestDatabase t, User u, string module, bool allowed = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, module, allowed).Ok);
    private static string Module(ProductReadPurpose purpose) => purpose switch
    {
        ProductReadPurpose.WriteOff => Modules.Stock,
        ProductReadPurpose.Expense => Modules.Cash,
        _ => Modules.Suppliers
    };
    private static User Staff(TestDatabase t, bool cost = false)
    {
        var user = TestData.Cashier(t);
        foreach (var purpose in Purposes) Grant(t, user, Module(purpose));
        if (cost) Grant(t, user, Modules.PurchaseCost);
        return user;
    }
    private static int Batch(TestDatabase t, int product) => (int)Number(t, "SELECT id FROM batches WHERE product_id=" + product + " LIMIT 1;");
    private static BatchRow Row(StockService service, User user, int product, ProductReadPurpose purpose = ProductReadPurpose.WriteOff)
    {
        var result = service.Batches(user, product, purpose); Assert.True(result.Ok, result.Error); return Assert.Single(result.Value!);
    }
    private static void Hidden(BatchRow row)
    {
        Assert.False(row.CostKnown); Assert.Equal(0L, row.CostPaisa); Assert.Equal(0L, row.ValuePaisa);
        Assert.Equal("hidden", row.ValueLabel); Assert.Contains("hidden", row.PickLabel);
        Assert.Equal("B-1", row.BatchNo); Assert.Equal(3d, row.QtyBase); Assert.Equal("2027-06-30", row.ExpiryDate);
    }

    [Fact]
    public void AnonymousReadersAndPreparersCannotUseAnyBatchWorkflow()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var s = Stock(t);
        foreach (var purpose in Purposes)
        {
            Assert.False(s.Batches(null, p, purpose).Ok);
            Assert.False(s.PrepareBatchCost(null, p, Batch(t, p), purpose).Ok);
        }
    }

    [Fact]
    public void ForgedAndMissingStoredIdentitiesRefuse()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var user = Staff(t); var s = Stock(t);
        foreach (var actor in new[] { new User { Id = user.Id, Role = "owner" }, new User { Id = int.MaxValue, Role = "owner" } })
        {
            Assert.False(s.Batches(actor, p).Ok);
            Assert.False(s.PrepareBatchCost(actor, p, Batch(t, p), ProductReadPurpose.WriteOff).Ok);
        }
    }

    [Fact]
    public void InactiveCallerSnapshotCannotReadOrPrepare()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t);
        var actor = new User { Id = 1, Role = "owner", Active = false }; var s = Stock(t);
        Assert.False(s.Batches(actor, p).Ok);
        Assert.False(s.PrepareBatchCost(actor, p, Batch(t, p), ProductReadPurpose.WriteOff).Ok);
    }

    [Fact]
    public void InactiveStoredOwnerCannotReadOrPrepare()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); Execute(t, "UPDATE users SET active=0 WHERE id=1;"); var s = Stock(t);
        Assert.False(s.Batches(TestData.Owner, p).Ok);
        Assert.False(s.PrepareBatchCost(TestData.Owner, p, Batch(t, p), ProductReadPurpose.WriteOff).Ok);
    }

    [Fact]
    public void ChangedStoredRoleRefusesNextRead()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var s = Stock(t);
        Assert.True(s.Batches(TestData.Owner, p).Ok); Execute(t, "UPDATE users SET role='manager' WHERE id=1;");
        Assert.False(s.Batches(TestData.Owner, p).Ok);
        Assert.False(s.PrepareBatchCost(TestData.Owner, p, Batch(t, p), ProductReadPurpose.WriteOff).Ok);
    }

    [Fact]
    public void UnsupportedWorkflowsCannotBorrowOtherPageGrants()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var s = Stock(t);
        foreach (var purpose in new[] { ProductReadPurpose.Catalog, ProductReadPurpose.Counter, ProductReadPurpose.Purchase,
            ProductReadPurpose.ProvisionalReturn, (ProductReadPurpose)999 })
        {
            Assert.False(s.Batches(TestData.Owner, p, purpose).Ok);
            Assert.False(s.PrepareBatchCost(TestData.Owner, p, Batch(t, p), purpose).Ok);
        }
    }

    [Fact]
    public void EverySupportedWorkflowRequiresItsOwnModuleEvenWithCost()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var user = Staff(t, true); var s = Stock(t);
        foreach (var purpose in Purposes)
        {
            Grant(t, user, Module(purpose), false);
            Assert.False(s.Batches(user, p, purpose).Ok);
            Assert.False(s.PrepareBatchCost(user, p, Batch(t, p), purpose).Ok);
            Grant(t, user, Module(purpose));
        }
    }

    [Fact]
    public void ProductsGrantDoesNotReplaceWorkflowGrant()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var user = Staff(t, true); Grant(t, user, Modules.Products);
        foreach (var purpose in Purposes) Grant(t, user, Module(purpose), false);
        foreach (var purpose in Purposes) Assert.False(Stock(t).Batches(user, p, purpose).Ok);
    }

    [Fact]
    public void ManagerBatchMetadataRemainsButRawCostAndValuationAreHidden()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 3); var manager = TestData.AddUser(t, "manager");
        Hidden(Row(Stock(t), manager, p));
    }

    [Fact]
    public void ProfitOnlyDoesNotRevealBatchCostInAnyWorkflow()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 3); var user = Staff(t); Grant(t, user, Modules.Profit);
        foreach (var purpose in Purposes) Hidden(Row(Stock(t), user, p, purpose));
    }

    [Fact]
    public void CostOnlyRevealsExactBatchRateAndAggregateWithoutProfit()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, costPaisa: 12345, batchQty: 3); var user = Staff(t, true);
        foreach (var purpose in Purposes)
        {
            var row = Row(Stock(t), user, p, purpose);
            Assert.True(row.CostKnown); Assert.Equal(12345L, row.CostPaisa); Assert.Equal(37035L, row.ValuePaisa);
            Assert.Equal(Money.Format(37035), row.ValueLabel);
        }
        Assert.False(TestData.Permissions(t).Can(user, Modules.Profit));
    }

    [Fact]
    public void OwnerExplicitCostDenialMasksAndRefusesPreparation()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 3); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        Hidden(Row(Stock(t), TestData.Owner, p));
        Assert.False(Stock(t).PrepareBatchCost(TestData.Owner, p, Batch(t, p), ProductReadPurpose.WriteOff).Ok);
    }

    [Fact]
    public void MainSwitchNextReadMasksOwnerAndOnRestores()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 3); var s = Stock(t);
        Assert.True(Row(s, TestData.Owner, p).CostKnown);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Hidden(Row(s, TestData.Owner, p));
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        Assert.True(Row(s, TestData.Owner, p).CostKnown);
    }

    [Fact]
    public void SameServiceCostRevocationMasksNextBatchRead()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 3); var user = Staff(t, true); var s = Stock(t);
        Assert.True(Row(s, user, p).CostKnown); Grant(t, user, Modules.PurchaseCost, false); Hidden(Row(s, user, p));
    }

    [Fact]
    public void SameServiceModuleRevocationRefusesMetadataToo()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var user = Staff(t, true); var s = Stock(t);
        Assert.True(s.Batches(user, p).Ok); Grant(t, user, Modules.Stock, false); Assert.False(s.Batches(user, p).Ok);
    }

    [Fact]
    public void HiddenBatchCostCannotBecomeZeroPreparation()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 3); var user = Staff(t); var s = Stock(t);
        foreach (var purpose in Purposes)
        {
            Hidden(Row(s, user, p, purpose));
            var result = s.PrepareBatchCost(user, p, Batch(t, p), purpose);
            Assert.False(result.Ok); Assert.Null(result.Value);
        }
    }

    [Fact]
    public void CostGranteeCanPrepareAllThreeExistingWorkflows()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, costPaisa: 12345, batchQty: 3); var user = Staff(t, true);
        foreach (var purpose in Purposes)
        {
            var result = Stock(t).PrepareBatchCost(user, p, Batch(t, p), purpose); Assert.True(result.Ok, result.Error);
            Assert.Equal(12345L, result.Value!.CostPaisa); Assert.Equal(3d, result.Value.QtyBase);
            Assert.Equal(p, result.Value.ProductId); Assert.Equal(Batch(t, p), result.Value.BatchId);
        }
    }

    [Fact]
    public void MainOffRefusesOwnerPreparationRatherThanReturningZero()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        foreach (var purpose in Purposes) Assert.False(Stock(t).PrepareBatchCost(TestData.Owner, p, Batch(t, p), purpose).Ok);
    }

    [Fact]
    public void PreparationReReadsCurrentCostQuantityAndPackInsteadOfDisplayedRow()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, costPaisa: 12345, batchQty: 3); var s = Stock(t);
        var old = Row(s, TestData.Owner, p);
        Execute(t, "UPDATE batches SET cost_paisa=777,qty_base=8;");
        Execute(t, "UPDATE products SET box_strips=3,strip_tablets=12;");
        var result = s.PrepareBatchCost(TestData.Owner, p, old.Id, ProductReadPurpose.WriteOff); Assert.True(result.Ok, result.Error);
        Assert.Equal(777L, result.Value!.CostPaisa); Assert.Equal(8d, result.Value.QtyBase);
        Assert.Equal(3, result.Value.BoxStrips); Assert.Equal(12, result.Value.StripTablets);
        Assert.Equal(12345L, old.CostPaisa);
    }

    [Fact]
    public void BatchFromDifferentProductCannotBeUsedForPreparation()
    {
        using var t = new TestDatabase(); var a = TestData.AddProduct(t); var b = TestData.AddProduct(t);
        Assert.False(Stock(t).PrepareBatchCost(TestData.Owner, a, Batch(t, b), ProductReadPurpose.SupplierReturn).Ok);
    }

    [Fact]
    public void MissingOrInactiveSelectedBatchNeverFallsBackToAnother()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var id = Batch(t, p); var s = Stock(t);
        Assert.False(s.PrepareBatchCost(TestData.Owner, p, int.MaxValue, ProductReadPurpose.WriteOff).Ok);
        Execute(t, "UPDATE batches SET active=0;");
        Assert.False(s.PrepareBatchCost(TestData.Owner, p, id, ProductReadPurpose.WriteOff).Ok);
    }

    [Fact]
    public void InactiveProductCannotSupplyPreparationFallback()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); Execute(t, "UPDATE products SET active=0;");
        Assert.False(Stock(t).PrepareBatchCost(TestData.Owner, p, Batch(t, p), ProductReadPurpose.WriteOff).Ok);
    }

    [Fact]
    public void NoBatchAllowsExistingProductFallbackButNeverSupplierReturnPreparation()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, costPaisa: 12345); Execute(t, "UPDATE batches SET active=0;");
        foreach (var purpose in new[] { ProductReadPurpose.WriteOff, ProductReadPurpose.Expense })
        {
            var result = Stock(t).PrepareBatchCost(TestData.Owner, p, null, purpose); Assert.True(result.Ok, result.Error);
            Assert.Null(result.Value!.BatchId); Assert.Equal(12345L, result.Value.CostPaisa);
        }
        Assert.False(Stock(t).PrepareBatchCost(TestData.Owner, p, null, ProductReadPurpose.SupplierReturn).Ok);
    }

    [Fact]
    public void NullSelectionWithLiveBatchesMustNotSilentlyPickFirstBatch()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t);
        foreach (var purpose in Purposes) Assert.False(Stock(t).PrepareBatchCost(TestData.Owner, p, null, purpose).Ok);
    }

    [Fact]
    public void KnownZeroIsNotHiddenAndPreparationRetainsExistingPositiveBatchFallbackRule()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, costPaisa: 0); var s = Stock(t);
        var row = Row(s, TestData.Owner, p); Assert.True(row.CostKnown); Assert.Equal(Money.Format(0), row.ValueLabel);
        Assert.Equal(0L, s.PrepareBatchCost(TestData.Owner, p, row.Id, ProductReadPurpose.WriteOff).Value!.CostPaisa);
        Execute(t, "UPDATE products SET cost_paisa=456;");
        Assert.Equal(456L, s.PrepareBatchCost(TestData.Owner, p, row.Id, ProductReadPurpose.WriteOff).Value!.CostPaisa);
    }

    [Fact]
    public void FinancialPermissionDatabaseFailureCannotFallBackToOwnerMoneyVisibility()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 3); Execute(t, "DROP TABLE user_permissions;");
        Hidden(Row(Stock(t), TestData.Owner, p));
        Assert.False(Stock(t).PrepareBatchCost(TestData.Owner, p, Batch(t, p), ProductReadPurpose.WriteOff).Ok);
    }

    [Fact]
    public void ReadAndPreparationDoNotPostMoneyStockOrAudit()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, costPaisa: 12345, batchQty: 3); var user = Staff(t, true);
        var tables = new[] { "batches", "products", "stock_adjustments", "stock_movements", "audit_logs", "expenses", "owner_drawings" };
        var counts = tables.ToDictionary(table => table, table => Number(t, "SELECT COUNT(*) FROM " + table));
        foreach (var purpose in Purposes)
        {
            Assert.True(Stock(t).Batches(user, p, purpose).Ok);
            Assert.True(Stock(t).PrepareBatchCost(user, p, Batch(t, p), purpose).Ok);
        }
        foreach (var table in tables) Assert.Equal(counts[table], Number(t, "SELECT COUNT(*) FROM " + table));
        Assert.Equal(3L, Number(t, "SELECT qty_base FROM batches;")); Assert.Equal(12345L, Number(t, "SELECT cost_paisa FROM batches;"));
    }

    [Fact]
    public void InvalidIdentifiersDoNotTurnIntoUnboundedPreparation()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var s = Stock(t);
        Assert.False(s.PrepareBatchCost(TestData.Owner, -1, Batch(t, p), ProductReadPurpose.WriteOff).Ok);
        Assert.False(s.PrepareBatchCost(TestData.Owner, p, -1, ProductReadPurpose.WriteOff).Ok);
        Assert.Empty(s.Batches(TestData.Owner, -1).Value!);
    }
}
