using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Phase4 portion3. Disposable test data only; these are not authenticated-session tests.</summary>
public sealed class LedgerReadBoundaryTests
{
    private static SupplierLedgerService Suppliers(TestDatabase t) => new(t.Database, t.Audit,
        new NumberingService(t.Settings), t.Settings, TestData.Permissions(t), t.Log);
    private static void CustomerDenied(TestDatabase t, User? user, int id)
    {
        var service = TestData.Ledger(t);
        Assert.False(service.List(user).Ok);
        Assert.False(service.Page(user, id).Ok);
        Assert.False(service.Statement(user, id).Ok);
        Assert.False(service.Statement(user, id, "2026-11").Ok);
        var bulk = service.BuildBulkStatements(user, new[] { id });
        Assert.False(bulk.Ok);
        Assert.Null(bulk.Value);
        Assert.False(service.PaymentReceipt(user, int.MaxValue).Ok);
    }
    private static void SupplierDenied(TestDatabase t, User? user, int id)
    {
        var service = Suppliers(t);
        Assert.False(service.List(user).Ok);
        Assert.False(service.Page(user, id).Ok);
        Assert.False(service.ExpiredBatches(user).Ok);
        Assert.False(service.Statement(user, id).Ok);
        var bulk = service.BuildBulkStatements(user, new[] { id });
        Assert.False(bulk.Ok);
        Assert.Null(bulk.Value);
    }
    private static int CreditSale(TestDatabase t)
    {
        var customer = TestData.AddCustomer(t);
        var product = TestData.AddProduct(t, costPaisa: 20000, retailPaisa: 30000);
        var sale = TestData.Sales(t).Create(TestData.Cart(product, method: "credit", customerId: customer), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        return customer;
    }
    private static long Count(TestDatabase t, string sql)
    {
        using var c = t.Database.Open();
        return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }

    [Fact]
    public void AnonymousCustomerReadsIncludingMonthlyAndBulkRefuse()
    {
        using var t = new TestDatabase(); CustomerDenied(t, null, TestData.AddCustomer(t));
    }

    [Fact]
    public void AnonymousSupplierReadsIncludingExpiredAndBulkRefuse()
    {
        using var t = new TestDatabase(); SupplierDenied(t, null, TestData.AddSupplier(t));
    }

    [Fact]
    public void CurrentCustomerDenialBlocksEveryReadWithExistingSnapshot()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var id = TestData.AddCustomer(t);
        Assert.True(TestData.Ledger(t).Page(user, id).Ok);
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, Modules.Customers, false).Ok);
        CustomerDenied(t, user, id);
    }

    [Fact]
    public void CashierCannotReadSupplierIdsStatementsOrCostsByDefault()
    {
        using var t = new TestDatabase(); SupplierDenied(t, TestData.Cashier(t), TestData.AddSupplier(t));
    }

    [Fact]
    public void AuthorizedCashierCustomerHistoryStillWorksWithoutProfit()
    {
        using var t = new TestDatabase(); var id = CreditSale(t); var user = TestData.Cashier(t);
        var service = TestData.Ledger(t);
        var page = service.Page(user, id);
        Assert.True(page.Ok, page.Error);
        Assert.True(page.Value!.BooksMatch);
        Assert.Equal(30000L, page.Value.Summary.BalancePaisa);
        Assert.False(page.Value.Summary.ProfitKnown);
        Assert.Equal(0L, page.Value.Summary.ProfitPaisa);
        Assert.Equal("hidden", page.Value.Summary.ProfitText);
        Assert.True(service.Statement(user, id).Ok);
        Assert.True(service.BuildBulkStatements(user, new[] { id }).Ok);
    }

    [Fact]
    public void ExplicitSupplierReadGrantPreservesSupportedWorkflow()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var id = TestData.AddSupplier(t);
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, Modules.Suppliers, true).Ok);
        var service = Suppliers(t);
        Assert.True(service.List(user).Ok);
        Assert.True(service.Page(user, id).Ok);
        Assert.True(service.ExpiredBatches(user).Ok);
        Assert.True(service.Statement(user, id).Ok);
        Assert.True(service.BuildBulkStatements(user, new[] { id }).Ok);
    }

    [Fact]
    public void SupplierGrantRevocationAffectsNextReadOnSameService()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var id = TestData.AddSupplier(t);
        var service = Suppliers(t); var permissions = TestData.Permissions(t);
        Assert.True(permissions.Set(TestData.Owner, user.Id, Modules.Suppliers, true).Ok);
        Assert.True(service.Page(user, id).Ok);
        Assert.True(permissions.Set(TestData.Owner, user.Id, Modules.Suppliers, false).Ok);
        Assert.False(service.Page(user, id).Ok);
        SupplierDenied(t, user, id);
    }

    [Fact]
    public void InactiveDatabaseOwnerCannotReadLedgersFromCachedSnapshot()
    {
        using var t = new TestDatabase(); var customer = TestData.AddCustomer(t); var supplier = TestData.AddSupplier(t);
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"); });
        CustomerDenied(t, TestData.Owner, customer); SupplierDenied(t, TestData.Owner, supplier);
    }

    [Fact]
    public void ForgedOwnerRoleOnCashierIdCannotReadEitherLedger()
    {
        using var t = new TestDatabase(); var cashier = TestData.Cashier(t);
        var forged = new User { Id = cashier.Id, Name = "Forged owner", Role = "owner" };
        CustomerDenied(t, forged, TestData.AddCustomer(t)); SupplierDenied(t, forged, TestData.AddSupplier(t));
    }

    [Fact]
    public void DemotedOwnerMustSignInAgainBeforeLedgerReads()
    {
        using var t = new TestDatabase(); var customer = TestData.AddCustomer(t); var supplier = TestData.AddSupplier(t);
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET role='manager' WHERE id=1;"); });
        CustomerDenied(t, TestData.Owner, customer); SupplierDenied(t, TestData.Owner, supplier);
    }

    [Fact]
    public void UnknownAccountIdsCannotUseRoleDefaultsToReadLedgers()
    {
        using var t = new TestDatabase(); var missing = new User { Id = int.MaxValue, Role = "manager" };
        CustomerDenied(t, missing, TestData.AddCustomer(t)); SupplierDenied(t, missing, TestData.AddSupplier(t));
    }

    [Fact]
    public void InactiveCallerSnapshotCannotReadEvenIfDatabaseAccountIsActive()
    {
        using var t = new TestDatabase();
        var inactive = new User { Id = 1, Name = "admin", Role = "owner", Active = false };
        CustomerDenied(t, inactive, TestData.AddCustomer(t)); SupplierDenied(t, inactive, TestData.AddSupplier(t));
    }

    [Fact]
    public void PaymentReceiptChecksCurrentAccountBeforeReturningExistingPayment()
    {
        using var t = new TestDatabase(); var id = CreditSale(t); var user = TestData.Cashier(t);
        var service = TestData.Ledger(t); var payment = service.Collect(user, id, 10000, "cash", null);
        Assert.True(payment.Ok, payment.Error);
        Assert.True(service.PaymentReceipt(user, payment.Value!.PaymentId).Ok);
        t.Database.Tx(c => { t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=@id;", new { id = user.Id }); });
        var denied = service.PaymentReceipt(user, payment.Value.PaymentId);
        Assert.False(denied.Ok); Assert.Null(denied.Value);
    }

    [Fact]
    public void ManagerCustomerListDoesNotReturnRawHiddenProfit()
    {
        using var t = new TestDatabase(); var id = CreditSale(t); var user = TestData.AddUser(t, "manager");
        var row = TestData.Ledger(t).List(user).Value!.Single(r => r.Id == id);
        Assert.False(row.ProfitKnown); Assert.Equal(0L, row.ProfitPaisa);
        Assert.Equal(30000L, row.LifetimePaisa);
        Assert.Equal(10000L, Count(t, "SELECT SUM(line_total_paisa-qty_base*cost_paisa_at_sale) FROM sale_items;"));
    }

    [Fact]
    public void CashierCustomerPageDoesNotReturnRawHiddenProfit()
    {
        using var t = new TestDatabase(); var id = CreditSale(t);
        var row = TestData.Ledger(t).Page(TestData.Cashier(t), id).Value!.Summary;
        Assert.False(row.ProfitKnown); Assert.Equal(0L, row.ProfitPaisa); Assert.Equal(30000L, row.LifetimePaisa);
    }

    [Fact]
    public void GlobalSwitchMasksOwnerListAndPageThenRestoresWithoutReposting()
    {
        using var t = new TestDatabase(); var id = CreditSale(t); var service = TestData.Ledger(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.False(service.List(TestData.Owner).Value!.Single(r => r.Id == id).ProfitKnown);
        Assert.Equal(0L, service.Page(TestData.Owner, id).Value!.Summary.ProfitPaisa);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        Assert.Equal(10000L, service.List(TestData.Owner).Value!.Single(r => r.Id == id).ProfitPaisa);
        Assert.True(service.Page(TestData.Owner, id).Value!.Summary.ProfitKnown);
        Assert.Equal(10000L, service.Page(TestData.Owner, id).Value!.Summary.ProfitPaisa);
        Assert.Equal(1L, Count(t, "SELECT COUNT(*) FROM sales;"));
    }

    [Fact]
    public void ExplicitProfitGrantWorksButCannotOverrideGlobalHide()
    {
        using var t = new TestDatabase(); var id = CreditSale(t); var user = TestData.Cashier(t);
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, Modules.Profit, true).Ok);
        Assert.Equal(10000L, TestData.Ledger(t).Page(user, id).Value!.Summary.ProfitPaisa);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.Equal(0L, TestData.Ledger(t).Page(user, id).Value!.Summary.ProfitPaisa);
        Assert.False(TestData.Ledger(t).List(user).Value!.Single(r => r.Id == id).ProfitKnown);
    }

    [Fact]
    public void CustomerStatementsAndBulkRemainProfitFreeForOwnerAndCashier()
    {
        using var t = new TestDatabase(); var id = CreditSale(t);
        foreach (var user in new[] { TestData.Owner, TestData.Cashier(t) })
        {
            var service = TestData.Ledger(t);
            var paper = service.Statement(user, id); Assert.True(paper.Ok, paper.Error);
            Assert.DoesNotContain("profit", paper.Value!.PlainText.ToLowerInvariant());
            Assert.True(service.Statement(user, id, "2026-11").Ok);
            var bulk = service.BuildBulkStatements(user, new[] { id }); Assert.True(bulk.Ok, bulk.Error);
            Assert.All(bulk.Value!.Documents, document => Assert.DoesNotContain("profit", document.PlainText.ToLowerInvariant()));
        }
    }

    [Fact]
    public void OwnerAndManagerSupplierReadsDoNotPostAnyFinancialRows()
    {
        using var t = new TestDatabase(); var id = TestData.AddSupplier(t);
        foreach (var user in new[] { TestData.Owner, TestData.AddUser(t, "manager") })
        {
            var service = Suppliers(t);
            Assert.True(service.List(user).Ok); Assert.True(service.Page(user, id).Ok);
            Assert.True(service.Statement(user, id).Ok); Assert.True(service.ExpiredBatches(user).Ok);
            Assert.True(service.BuildBulkStatements(user, new[] { id }).Ok);
        }
        Assert.Equal(0L, Count(t, "SELECT COUNT(*) FROM payments;"));
        Assert.Equal(0L, Count(t, "SELECT COUNT(*) FROM purchases;"));
        Assert.Equal(0L, Count(t, "SELECT COUNT(*) FROM audit_logs;"));
    }

    [Fact]
    public void AuthorizedMissingTargetsAndMixedBulkReturnNoPartialJob()
    {
        using var t = new TestDatabase(); var id = TestData.AddCustomer(t); var supplier = TestData.AddSupplier(t);
        var customer = TestData.Ledger(t); var suppliers = Suppliers(t);
        Assert.False(customer.Page(TestData.Owner, int.MaxValue).Ok);
        Assert.False(suppliers.Page(TestData.Owner, int.MaxValue).Ok);
        Assert.False(customer.Statement(TestData.Owner, id, "invalid month").Ok);
        var bulk = customer.BuildBulkStatements(TestData.Owner, new[] { id, int.MaxValue });
        Assert.False(bulk.Ok); Assert.Null(bulk.Value);
        var supplierBulk = suppliers.BuildBulkStatements(TestData.Owner, new[] { supplier, int.MaxValue });
        Assert.False(supplierBulk.Ok); Assert.Null(supplierBulk.Value);
    }
}
