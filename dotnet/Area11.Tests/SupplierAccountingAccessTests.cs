using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>D-114 Supplier accounting exception; synthetic isolated data, not Windows execution.</summary>
public sealed class SupplierAccountingAccessTests
{
    private static SupplierLedgerService Ledger(TestDatabase t) =>
        new(t.Database, t.Audit, new NumberingService(t.Settings), t.Settings, TestData.Permissions(t), t.Log);
    private static StockService Stock(TestDatabase t) => new(t.Database, t.Audit, t.Settings, TestData.Permissions(t), t.Log);
    private static ProductService Products(TestDatabase t) => new(t.Database, t.Audit, t.Log, permissions: TestData.Permissions(t), settings: t.Settings);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static void Grant(TestDatabase t, User user, string module, bool allow = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, module, allow).Ok);
    private static User Staff(TestDatabase t)
    {
        var user = TestData.Cashier(t); Grant(t, user, Modules.Suppliers); return user;
    }
    private static int Batch(TestDatabase t, int product) => (int)Number(t, "SELECT id FROM batches WHERE product_id=" + product + " LIMIT 1;");
    private static SupplierReturnInput Input(int supplier, int product, int batch, double qty = 1, bool cash = false) => new()
    {
        SupplierId = supplier, CashReceived = cash, Reason = SupplierLedgerService.ReasonExpired,
        Lines = new List<SupplierReturnLineInput> { new() { ProductId = product, BatchId = batch, QtyEntered = qty, Unit = "base" } }
    };
    private static void Hidden(SupplierReturnPreparation row)
    {
        Assert.False(row.CostKnown); Assert.Equal(0L, row.CostPaisa);
        Assert.Equal(3d, row.QtyBase); Assert.Equal(2, row.BoxStrips); Assert.Equal(10, row.StripTablets);
    }
    private static void Denied(TestDatabase t, User? user)
    {
        var p = TestData.AddProduct(t); var b = Batch(t, p); var supplier = TestData.AddSupplier(t, openingPaisa: 100000);
        var service = Ledger(t); var before = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        Assert.False(service.CheckReadAccess(user).Ok); Assert.False(service.List(user).Ok);
        Assert.False(service.Page(user, supplier).Ok); Assert.False(service.Statement(user, supplier).Ok);
        Assert.False(service.ExpiredBatches(user).Ok); Assert.False(service.BuildBulkStatements(user, new[] { supplier }).Ok);
        Assert.False(service.PrepareReturnLine(user, p, b).Ok);
        Assert.False(service.Pay(user!, supplier, 100, "cash", null).Ok); Assert.False(service.Return(user!, Input(supplier, p, b)).Ok);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM supplier_returns;"));
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM payments;"));
        Assert.Equal(100L, Number(t, "SELECT qty_base FROM batches WHERE id=" + b));
        Assert.Equal(100000L, Number(t, "SELECT balance_paisa FROM suppliers WHERE id=" + supplier));
        Assert.Equal(before, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }

    [Fact]
    public void AnonymousCannotReadPreparePayOrReturn()
    {
        using var t = new TestDatabase(); Denied(t, null);
    }

    [Fact]
    public void InactiveCallerCannotReadPreparePayOrReturn()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = 1, Role = "owner", Active = false });
    }

    [Fact]
    public void MissingAccountCannotReadPreparePayOrReturn()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = int.MaxValue, Role = "owner" });
    }

    [Fact]
    public void ForgedOwnerRoleCannotReadPreparePayOrReturn()
    {
        using var t = new TestDatabase(); var u = Staff(t); Denied(t, new User { Id = u.Id, Role = "owner" });
    }

    [Fact]
    public void InactiveStoredOwnerCannotReadPreparePayOrReturn()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE users SET active=0 WHERE id=1;"); Denied(t, TestData.Owner);
    }

    [Fact]
    public void ChangedStoredRoleCannotReadPreparePayOrReturn()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE users SET role='manager' WHERE id=1;"); Denied(t, TestData.Owner);
    }

    [Fact]
    public void RevokedSupplierModuleCannotBeReplacedByCostGrant()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.PurchaseCost); Grant(t, u, Modules.Suppliers, false); Denied(t, u);
    }

    [Fact]
    public void SupplierOnlyCanReadAccountingAndBothStatementRoutes()
    {
        using var t = new TestDatabase(); var u = Staff(t); var supplier = TestData.AddSupplier(t); var s = Ledger(t);
        var p = TestData.AddProduct(t, batchQty: 0);
        var buy = TestData.Purchases(t).Create(new PurchaseInput
        {
            SupplierId = supplier, SupplierInvoiceNo = "SYN-ACCOUNTING-EXCEPTION", PaidPaisa = 50000,
            Lines = new List<PurchaseLineInput> { new() { ProductId = p, Unit = "base", Quantity = 10,
                BatchNo = "SYN-PURCHASE", Expiry = "2027-06", CostPaisa = 20000, RetailPaisa = 30000 } }
        }, TestData.Owner);
        Assert.True(buy.Ok, buy.Error);
        var account = s.Page(u, supplier).Value!;
        Assert.Equal(200000L, account.Summary.PurchasesPaisa); Assert.Equal(50000L, account.Summary.PaidPaisa);
        Assert.Equal(150000L, account.Summary.PayablePaisa); // accepted purchase inference: 500 + 1500 = 2000
        Assert.Equal(150000L, Assert.Single(s.List(u).Value!).PayablePaisa);
        Assert.True(s.Page(u, supplier).Value!.BooksMatch);
        Assert.Equal(150000L, s.Page(u, supplier).Value!.ExpectedPaisa);
        Assert.True(s.Statement(u, supplier).Ok); Assert.True(s.BuildBulkStatements(u, new[] { supplier }).Ok);
        Assert.False(s.CheckReadAccess(u).Value!.CostKnown);
    }

    [Fact]
    public void MainOffOwnerCanReadAccountingAndPay()
    {
        using var t = new TestDatabase(); var supplier = TestData.AddSupplier(t, openingPaisa: 150000);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var s = Ledger(t); Assert.True(s.Statement(TestData.Owner, supplier).Ok);
        Assert.True(s.Pay(TestData.Owner, supplier, 50000, "cash", null).Ok);
        Assert.Equal(100000L, s.Page(TestData.Owner, supplier).Value!.ExpectedPaisa);
    }

    [Fact]
    public void SupplierOnlyCanPrepareQuantityWithoutKnownZero()
    {
        using var t = new TestDatabase(); var u = Staff(t); var p = TestData.AddProduct(t, batchQty: 3);
        Hidden(Ledger(t).PrepareReturnLine(u, p, Batch(t, p)).Value!);
        Assert.False(Stock(t).PrepareBatchCost(u, p, Batch(t, p), ProductReadPurpose.SupplierReturn).Ok);
    }

    [Fact]
    public void CostGrantShowsFreshBatchPreviewAndRevocationMasks()
    {
        using var t = new TestDatabase(); var u = Staff(t); var p = TestData.AddProduct(t, batchQty: 3); var b = Batch(t, p); var s = Ledger(t);
        Grant(t, u, Modules.PurchaseCost); Execute(t, "UPDATE batches SET cost_paisa=12345 WHERE id=" + b);
        var known = s.PrepareReturnLine(u, p, b).Value!; Assert.True(known.CostKnown); Assert.Equal(12345L, known.CostPaisa);
        Grant(t, u, Modules.PurchaseCost, false); Hidden(s.PrepareReturnLine(u, p, b).Value!);
    }

    [Fact]
    public void ProfitOnlyCannotUnmaskDirectCostPreparation()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Profit); var p = TestData.AddProduct(t, batchQty: 3);
        Hidden(Ledger(t).PrepareReturnLine(u, p, Batch(t, p)).Value!);
    }

    [Fact]
    public void MainOffMasksCostEvenForOwnerButReturnStillSettles()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 3); var supplier = TestData.AddSupplier(t, openingPaisa: 100000); var b = Batch(t, p);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var s = Ledger(t); Hidden(s.PrepareReturnLine(TestData.Owner, p, b).Value!);
        var result = s.Return(TestData.Owner, Input(supplier, p, b)); Assert.True(result.Ok, result.Error);
        Assert.Equal(20000L, result.Value!.TotalPaisa); Assert.Equal(80000L, result.Value.NewPayablePaisa);
    }

    [Fact]
    public void SupplierProductSearchAndGetReturnMaskedRowsWithoutCostGrant()
    {
        using var t = new TestDatabase(); var u = Staff(t); var p = TestData.AddProduct(t); var products = Products(t);
        var row = products.Get(u, p, ProductReadPurpose.SupplierReturn); Assert.True(row.Ok, row.Error);
        Assert.False(row.Value!.CostKnown); Assert.Equal(0L, row.Value.CostPaisa);
        var found = products.Search(u, purpose: ProductReadPurpose.SupplierReturn); Assert.True(found.Ok, found.Error);
        Assert.All(found.Value!, item => { Assert.False(item.CostKnown); Assert.Equal(0L, item.CostPaisa); });
        Assert.False(products.Get(u, p, ProductReadPurpose.Purchase).Ok);
    }

    [Fact]
    public void SupplierOnlyCreditReturnUsesServerCostAndReconciles()
    {
        using var t = new TestDatabase(); var u = Staff(t); var p = TestData.AddProduct(t, batchQty: 3); var b = Batch(t, p); var supplier = TestData.AddSupplier(t, openingPaisa: 100000); var s = Ledger(t);
        Hidden(s.PrepareReturnLine(u, p, b).Value!); Execute(t, "UPDATE batches SET cost_paisa=12345 WHERE id=" + b);
        var result = s.Return(u, Input(supplier, p, b)); Assert.True(result.Ok, result.Error);
        Assert.Equal(12345L, result.Value!.TotalPaisa); Assert.Equal(87655L, result.Value.NewPayablePaisa);
        Assert.Equal(2L, Number(t, "SELECT qty_base FROM batches WHERE id=" + b)); Assert.True(s.Page(u, supplier).Value!.BooksMatch);
    }

    [Fact]
    public void SupplierOnlyCashReturnLeavesPayableAndStandalonePaymentsUnchanged()
    {
        using var t = new TestDatabase(); var u = Staff(t); var p = TestData.AddProduct(t); var supplier = TestData.AddSupplier(t, openingPaisa: 100000); var s = Ledger(t);
        var result = s.Return(u, Input(supplier, p, Batch(t, p), cash: true)); Assert.True(result.Ok, result.Error);
        Assert.Equal(100000L, result.Value!.NewPayablePaisa); Assert.NotEmpty(result.Value.Warnings);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM payments;")); Assert.True(s.Page(u, supplier).Value!.BooksMatch);
    }

    [Fact]
    public void KnownGenuineZeroIsDifferentFromHiddenZero()
    {
        using var t = new TestDatabase(); var u = Staff(t); var p = TestData.AddProduct(t, costPaisa: 0, batchQty: 3); var b = Batch(t, p);
        Hidden(Ledger(t).PrepareReturnLine(u, p, b).Value!); Grant(t, u, Modules.PurchaseCost);
        var row = Ledger(t).PrepareReturnLine(u, p, b).Value!; Assert.True(row.CostKnown); Assert.Equal(0L, row.CostPaisa);
    }

    [Fact]
    public void BatchZeroFallbackUsesCurrentProductCost()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, costPaisa: 7654); var b = Batch(t, p); Execute(t, "UPDATE batches SET cost_paisa=0 WHERE id=" + b);
        var s = Ledger(t); Assert.Equal(7654L, s.PrepareReturnLine(TestData.Owner, p, b).Value!.CostPaisa);
        var supplier = TestData.AddSupplier(t); Assert.Equal(7654L, s.Return(TestData.Owner, Input(supplier, p, b)).Value!.TotalPaisa);
    }

    [Fact]
    public void MissingWrongInactiveOrEmptyBatchCannotPrepare()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var other = TestData.AddProduct(t); var b = Batch(t, p); var s = Ledger(t);
        Assert.False(s.PrepareReturnLine(TestData.Owner, p, 0).Ok); Assert.False(s.PrepareReturnLine(TestData.Owner, other, b).Ok);
        Execute(t, "UPDATE batches SET active=0 WHERE id=" + b); Assert.False(s.PrepareReturnLine(TestData.Owner, p, b).Ok);
        Execute(t, "UPDATE batches SET active=1,qty_base=0 WHERE id=" + b); Assert.False(s.PrepareReturnLine(TestData.Owner, p, b).Ok);
    }

    [Fact]
    public void InactiveProductCannotPrepareEvenWhenBatchExists()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var b = Batch(t, p); Execute(t, "UPDATE products SET active=0 WHERE id=" + p);
        Assert.False(Ledger(t).PrepareReturnLine(TestData.Owner, p, b).Ok);
    }

    [Fact]
    public void OverpaymentAndOversizedReturnRemainAtomic()
    {
        using var t = new TestDatabase(); var u = Staff(t); var p = TestData.AddProduct(t, batchQty: 3); var b = Batch(t, p); var supplier = TestData.AddSupplier(t, openingPaisa: 1000); var s = Ledger(t);
        Assert.False(s.Pay(u, supplier, 1001, "cash", null).Ok); Assert.False(s.Return(u, Input(supplier, p, b, 4)).Ok);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM supplier_returns;")); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM payments;"));
        Assert.Equal(3L, Number(t, "SELECT qty_base FROM batches WHERE id=" + b)); Assert.True(s.Page(u, supplier).Value!.BooksMatch);
        Execute(t, "CREATE TRIGGER synthetic_supplier_audit_fault BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT, 'sensitive-synthetic-audit-fault'); END;");
        var pay = s.Pay(u, supplier, 100, "cash", null); var returned = s.Return(u, Input(supplier, p, b));
        Assert.False(pay.Ok); Assert.False(returned.Ok);
        Assert.DoesNotContain("sensitive-synthetic", pay.Error!); Assert.DoesNotContain("sensitive-synthetic", returned.Error!);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM payments;")); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM supplier_returns;"));
        Assert.Equal(3L, Number(t, "SELECT qty_base FROM batches WHERE id=" + b));
        Assert.Equal(1000L, Number(t, "SELECT balance_paisa FROM suppliers WHERE id=" + supplier));
        Execute(t, "DROP TRIGGER synthetic_supplier_audit_fault;");
        Assert.True(s.Return(u, Input(supplier, p, b)).Ok);
    }

    [Fact]
    public void RevocationAfterPreparationBlocksBothWrites()
    {
        using var t = new TestDatabase(); var u = Staff(t); var p = TestData.AddProduct(t); var b = Batch(t, p); var supplier = TestData.AddSupplier(t, openingPaisa: 100000); var s = Ledger(t);
        Assert.True(s.PrepareReturnLine(u, p, b).Ok); Grant(t, u, Modules.Suppliers, false);
        Assert.False(s.Pay(u, supplier, 100, "cash", null).Ok); Assert.False(s.Return(u, Input(supplier, p, b)).Ok);
        Assert.Equal(100L, Number(t, "SELECT qty_base FROM batches WHERE id=" + b));
    }

    [Fact]
    public void CurrentStoredNameIsUsedForPaymentAudit()
    {
        using var t = new TestDatabase(); var u = Staff(t); var supplier = TestData.AddSupplier(t, openingPaisa: 100000);
        Execute(t, "UPDATE users SET name='Current Supplier Actor' WHERE id=" + u.Id); Assert.True(Ledger(t).Pay(u, supplier, 100, "cash", null).Ok);
        using var c = t.Database.Open(); Assert.Equal("Current Supplier Actor", t.Database.QuerySingle<string>(c, "SELECT user_name FROM audit_logs WHERE action='supplier_pay' ORDER BY id DESC LIMIT 1;"));
    }

    [Fact]
    public void CurrentStoredNameIsUsedForReturnAudit()
    {
        using var t = new TestDatabase(); var u = Staff(t); var p = TestData.AddProduct(t); var supplier = TestData.AddSupplier(t);
        Execute(t, "UPDATE users SET name='Current SR Actor' WHERE id=" + u.Id); Assert.True(Ledger(t).Return(u, Input(supplier, p, Batch(t, p))).Ok);
        using var c = t.Database.Open(); Assert.Equal("Current SR Actor", t.Database.QuerySingle<string>(c, "SELECT user_name FROM audit_logs WHERE action='supplier_return' ORDER BY id DESC LIMIT 1;"));
    }
}
