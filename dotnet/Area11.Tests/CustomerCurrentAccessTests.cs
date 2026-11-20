using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Current customer/doctor/rep boundaries on isolated fixtures; Windows execution required.</summary>
public sealed class CustomerCurrentAccessTests
{
    private static RepService Reps(TestDatabase t) => new(t.Database, t.Audit, t.Settings, TestData.Permissions(t), t.Log);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static void Grant(TestDatabase t, User u, string module, bool allow = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, module, allow).Ok);
    private static User Staff(TestDatabase t)
    {
        var u = TestData.Cashier(t); Grant(t, u, Modules.Customers); return u;
    }
    private static int Customer(TestDatabase t)
    {
        var c = TestData.AddCustomer(t); Execute(t, "UPDATE customers SET opening_balance_paisa=100000,balance_paisa=100000 WHERE id=" + c); return c;
    }
    private static int Company(TestDatabase t) => t.Database.Tx(c =>
    {
        t.Database.Execute(c, "INSERT INTO companies(name,active) VALUES('Synthetic access company',1);");
        return (int)t.Database.NextId(c);
    });
    private static int Prescription(TestDatabase t)
    {
        var p = TestData.AddProduct(t); var doctor = TestData.AddCustomer(t, category: "doctor");
        var sale = TestData.Sales(t).Create(TestData.Cart(p, qty: 2, customerId: doctor, doctorId: doctor), TestData.Owner);
        Assert.True(sale.Ok, sale.Error); return doctor;
    }
    private static void Denied(TestDatabase t, User? actor)
    {
        var customer = Customer(t); var company = Company(t); var ledger = TestData.Ledger(t); var reps = Reps(t);
        Execute(t, "INSERT INTO payments(customer_id,amount_paisa,method,user_id) VALUES(" + customer + ",10000,'cash',1);");
        var payment = (int)Number(t, "SELECT MAX(id) FROM payments;");
        Execute(t, "UPDATE customers SET balance_paisa=90000 WHERE id=" + customer);
        var before = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        Assert.False(ledger.CheckAccess(actor).Ok); Assert.False(ledger.List(actor).Ok); Assert.False(ledger.Page(actor, customer).Ok);
        Assert.False(ledger.Statement(actor, customer).Ok); Assert.False(ledger.BuildBulkStatements(actor, new[] { customer }).Ok);
        Assert.False(ledger.PaymentReceipt(actor, payment).Ok); Assert.False(ledger.Collect(actor, customer, 100, "cash", null).Ok);
        Assert.False(reps.CheckAccess(actor).Ok); Assert.False(reps.ListDoctors(actor).Ok); Assert.False(reps.ListCustomerProfit(actor).Ok);
        Assert.False(reps.ListReps(actor).Ok); Assert.False(reps.SaveRep(actor, company, "Synthetic rep", "0300", 1).Ok);
        Assert.Equal(90000L, Number(t, "SELECT balance_paisa FROM customers WHERE id=" + customer));
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM payments;")); Assert.Equal(before, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM companies WHERE rep_name IS NOT NULL;"));
    }

    [Fact]
    public void AnonymousCannotCollectReadReceiptOrDoctorRepData()
    {
        using var t = new TestDatabase(); Denied(t, null);
    }

    [Fact]
    public void InactiveCallerCannotCollectReadReceiptOrDoctorRepData()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = 1, Role = "owner", Active = false });
    }

    [Fact]
    public void MissingStoredIdentityCannotCollectOrRead()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = int.MaxValue, Role = "owner" });
    }

    [Fact]
    public void ForgedOwnerCannotBorrowStoredStaffIdentity()
    {
        using var t = new TestDatabase(); var u = Staff(t); Denied(t, new User { Id = u.Id, Role = "owner" });
    }

    [Fact]
    public void ChangedStoredRoleBlocksCollectionAndAllRelatedReads()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE users SET role='manager' WHERE id=1;"); Denied(t, TestData.Owner);
    }

    [Fact]
    public void InactiveStoredOwnerBlocksCollectionAndAllRelatedReads()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE users SET active=0 WHERE id=1;"); Denied(t, TestData.Owner);
    }

    [Fact]
    public void FinancialGrantsCannotReplaceCustomerOrProductModule()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Customers, false); Grant(t, u, Modules.Profit); Grant(t, u, Modules.PurchaseCost); Denied(t, u);
    }

    [Fact]
    public void CollectionDoesNotRequireCostOrProfitAndReconciles()
    {
        using var t = new TestDatabase(); var u = Staff(t); var c = Customer(t); var ledger = TestData.Ledger(t);
        var collected = ledger.Collect(u, c, 25000, "cash", "Synthetic collection"); Assert.True(collected.Ok, collected.Error);
        Assert.Equal(75000L, collected.Value!.NewBalancePaisa); Assert.True(ledger.Page(u, c).Value!.BooksMatch);
        Assert.True(ledger.PaymentReceipt(u, collected.Value.PaymentId).Ok); Assert.False(ledger.CheckAccess(u).Value);
    }

    [Fact]
    public void MainOffDoesNotBlockCollectionOrStatements()
    {
        using var t = new TestDatabase(); var u = Staff(t); var c = Customer(t); Grant(t, u, Modules.Profit);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var ledger = TestData.Ledger(t); Assert.True(ledger.Collect(u, c, 10000, "online", null).Ok);
        Assert.True(ledger.Statement(u, c).Ok); Assert.True(ledger.BuildBulkStatements(u, new[] { c }).Ok);
        Assert.False(ledger.Page(u, c).Value!.Summary.ProfitKnown);
    }

    [Fact]
    public void EveryExistingCollectionMethodStillWorks()
    {
        using var t = new TestDatabase(); var u = Staff(t); var c = Customer(t); var ledger = TestData.Ledger(t);
        foreach (var method in CustomerLedgerService.CollectionMethods) Assert.True(ledger.Collect(u, c, 10000, method, null).Ok);
        Assert.Equal(70000L, ledger.Page(u, c).Value!.ExpectedPaisa); Assert.True(ledger.Page(u, c).Value!.BooksMatch);
    }

    [Fact]
    public void OverpaymentAndInvalidCollectionInputsWriteNothing()
    {
        using var t = new TestDatabase(); var c = Customer(t); var ledger = TestData.Ledger(t);
        foreach (var amount in new long[] { 0, -1, 100001 }) Assert.False(ledger.Collect(TestData.Owner, c, amount, "cash", null).Ok);
        Assert.False(ledger.Collect(TestData.Owner, c, 100, "credit", null).Ok);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM payments;")); Assert.Equal(100000L, ledger.Page(TestData.Owner, c).Value!.StoredPaisa);
        Assert.True(ledger.Collect(TestData.Owner, c, 100000, "card", null).Ok); Assert.True(ledger.Page(TestData.Owner, c).Value!.BooksMatch);
    }

    [Fact]
    public void AuditFailureRollsBackCollectionAndBalanceWithSafeError()
    {
        using var t = new TestDatabase(); var u = Staff(t); var c = Customer(t); var before = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        Execute(t, "CREATE TRIGGER synthetic_collection_fault BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'sensitive-collection-fault'); END;");
        var result = TestData.Ledger(t).Collect(u, c, 10000, "cash", null); Assert.False(result.Ok);
        Assert.DoesNotContain("sensitive-collection-fault", result.Error!); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM payments;"));
        Assert.Equal(before, Number(t, "SELECT COUNT(*) FROM audit_logs;")); Assert.Equal(100000L, Number(t, "SELECT balance_paisa FROM customers WHERE id=" + c));
    }

    [Fact]
    public void RevocationAfterReadBlocksCollectionAndReprint()
    {
        using var t = new TestDatabase(); var u = Staff(t); var c = Customer(t); var ledger = TestData.Ledger(t);
        var collected = ledger.Collect(u, c, 10000, "cash", null); Assert.True(collected.Ok, collected.Error); Assert.True(ledger.Page(u, c).Ok);
        Grant(t, u, Modules.Customers, false); Assert.False(ledger.Collect(u, c, 10000, "cash", null).Ok);
        Assert.False(ledger.PaymentReceipt(u, collected.Value!.PaymentId).Ok); Assert.Equal(90000L, Number(t, "SELECT balance_paisa FROM customers WHERE id=" + c));
    }

    [Fact]
    public void CollectionAuditAndReceiptUseCurrentStoredReaderName()
    {
        using var t = new TestDatabase(); var u = Staff(t); var c = Customer(t);
        Execute(t, "UPDATE users SET name='Fresh Actor' WHERE id=" + u.Id);
        var ledger = TestData.Ledger(t); var collected = ledger.Collect(u, c, 10000, "cash", null); Assert.True(collected.Ok, collected.Error);
        using var connection = t.Database.Open(); Assert.Equal("Fresh Actor", t.Database.QuerySingle<string>(connection, "SELECT user_name FROM audit_logs WHERE action='customer_collect' ORDER BY id DESC LIMIT 1;"));
        Assert.Contains("Fresh Actor", ledger.PaymentReceipt(u, collected.Value!.PaymentId).Value!.PlainText);
    }

    [Fact]
    public void SalePaymentCannotBeReprintedAsKhataCollection()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var c = TestData.AddCustomer(t);
        Assert.True(TestData.Sales(t).Create(TestData.Cart(p, customerId: c), TestData.Owner).Ok);
        var payment = (int)Number(t, "SELECT id FROM payments WHERE sale_id IS NOT NULL LIMIT 1;");
        Assert.False(TestData.Ledger(t).PaymentReceipt(TestData.Owner, payment).Ok);
    }

    [Fact]
    public void MissingInactiveAndSettledCustomerCannotReceiveCollection()
    {
        using var t = new TestDatabase(); var c = Customer(t); var ledger = TestData.Ledger(t);
        Assert.False(ledger.Collect(TestData.Owner, int.MaxValue, 100, "cash", null).Ok);
        Execute(t, "UPDATE customers SET active=0 WHERE id=" + c); Assert.False(ledger.Collect(TestData.Owner, c, 100, "cash", null).Ok);
        Execute(t, "UPDATE customers SET active=1,balance_paisa=0 WHERE id=" + c); Assert.False(ledger.Collect(TestData.Owner, c, 100, "cash", null).Ok);
    }

    [Fact]
    public void DoctorSalesRemainReadableButProfitIsExplicitlyMasked()
    {
        using var t = new TestDatabase(); var doctor = Prescription(t); var u = Staff(t); var reps = Reps(t);
        var row = reps.ListDoctors(u).Value!.Single(r => r.DoctorId == doctor); Assert.Equal(60000L, row.SalesPaisa);
        Assert.False(row.ProfitKnown); Assert.Equal(0L, row.ProfitPaisa); Assert.Equal("—", row.ProfitText);
        Assert.False(reps.ListCustomerProfit(u).Ok);
    }

    [Fact]
    public void ProfitGrantWithoutCustomersCannotReadLifetimeProfit()
    {
        using var t = new TestDatabase(); Prescription(t); var u = TestData.Cashier(t); Grant(t, u, Modules.Customers, false); Grant(t, u, Modules.Profit);
        Assert.False(Reps(t).ListCustomerProfit(u).Ok); Assert.False(Reps(t).ListDoctors(u).Ok);
    }

    [Fact]
    public void DelegatedProfitDoesNotRequireCostOrOwnerRank()
    {
        using var t = new TestDatabase(); var doctor = Prescription(t); var u = Staff(t); Grant(t, u, Modules.Profit); var reps = Reps(t);
        Assert.Equal(20000L, reps.ListDoctors(u).Value!.Single(r => r.DoctorId == doctor).ProfitPaisa);
        var row = reps.ListCustomerProfit(u).Value!.Single(r => r.CustomerId == doctor); Assert.True(row.ProfitKnown); Assert.Equal(20000L, row.ProfitPaisa);
        Assert.True(reps.CheckAccess(u).Value!.Profit);
    }

    [Fact]
    public void ProfitRevocationAndMainOffApplyOnSameService()
    {
        using var t = new TestDatabase(); var doctor = Prescription(t); var u = Staff(t); Grant(t, u, Modules.Profit); var reps = Reps(t);
        Assert.True(reps.ListCustomerProfit(u).Ok); Grant(t, u, Modules.Profit, false); Assert.False(reps.ListCustomerProfit(u).Ok);
        Assert.False(reps.ListDoctors(u).Value!.Single(r => r.DoctorId == doctor).ProfitKnown); Grant(t, u, Modules.Profit);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.False(reps.ListCustomerProfit(u).Ok); Assert.False(reps.ListDoctors(TestData.Owner).Value!.Single(r => r.DoctorId == doctor).ProfitKnown);
    }

    [Fact]
    public void ProductsOnlyCanReadAndEditRepButNotCustomerSummaries()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Customers, false); Grant(t, u, Modules.Products); var company = Company(t); var reps = Reps(t);
        Assert.True(reps.SaveRep(u, company, "Synthetic rep", "0300", 1).Ok); Assert.NotEmpty(reps.ListReps(u).Value!);
        Assert.False(reps.ListDoctors(u).Ok); Assert.False(reps.ListCustomerProfit(u).Ok);
        var access = reps.CheckAccess(u).Value!; Assert.True(access.Reps); Assert.False(access.Customers); Assert.False(access.Profit);
    }

    [Fact]
    public void RepRevocationAndAuditFailureLeaveCompanyUnchanged()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Products); var company = Company(t); var reps = Reps(t);
        Assert.True(reps.ListReps(u).Ok); Grant(t, u, Modules.Products, false); Assert.False(reps.SaveRep(u, company, "Denied", "0300", 1).Ok);
        Grant(t, u, Modules.Products); Execute(t, "CREATE TRIGGER synthetic_rep_fault BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'sensitive-rep-fault'); END;");
        var result = reps.SaveRep(u, company, "No commit", "0300", 1); Assert.False(result.Ok); Assert.DoesNotContain("sensitive-rep-fault", result.Error!);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM companies WHERE rep_name IS NOT NULL;"));
    }

    [Fact]
    public void RepAuditUsesCurrentStoredName()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Products); var company = Company(t);
        Execute(t, "UPDATE users SET name='Fresh Rep Actor' WHERE id=" + u.Id); Assert.True(Reps(t).SaveRep(u, company, "Synthetic rep", "0300", 1).Ok);
        using var connection = t.Database.Open(); Assert.Equal("Fresh Rep Actor", t.Database.QuerySingle<string>(connection, "SELECT user_name FROM audit_logs WHERE action='company_rep_update' ORDER BY id DESC LIMIT 1;"));
    }

    [Fact]
    public void FreshAccessSnapshotsReadWithoutPostingMoneyOrAudit()
    {
        using var t = new TestDatabase(); var u = Staff(t); var c = Customer(t); var ledger = TestData.Ledger(t); var reps = Reps(t);
        var before = Number(t, "SELECT COUNT(*) FROM audit_logs;"); Assert.True(ledger.CheckAccess(u).Ok); Assert.False(ledger.CheckAccess(u).Value);
        Assert.True(reps.CheckAccess(u).Value!.Customers); Assert.False(reps.CheckAccess(u).Value!.Reps);
        Assert.Equal(before, Number(t, "SELECT COUNT(*) FROM audit_logs;")); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM payments;"));
        Assert.True(ledger.Page(u, c).Value!.BooksMatch);
    }
}
