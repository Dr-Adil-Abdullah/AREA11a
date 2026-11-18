using System.Threading;
using System.Threading.Tasks;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated integrated accounting regressions. Written, not executed in this Linux workspace.</summary>
public sealed class LedgerPostingIntegrityTests
{
    private sealed class PostingServices
    {
        public CustomerLedgerService Customers { get; }
        public SupplierLedgerService Suppliers { get; }
        public PostingServices(Database db, LogService log)
        {
            var audit = new AuditService(db, log); var permissions = new PermissionService(db, log, audit);
            var settings = new SettingsService(db, audit, log, permissions);
            Customers = new CustomerLedgerService(db, audit, settings, permissions, log);
            Suppliers = new SupplierLedgerService(db, audit, new NumberingService(settings), settings, permissions, log);
        }
    }
    private sealed class PostingIntegrityFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public int Customer { get; }
        public int Supplier { get; }
        public PostingServices Services { get; }
        public PostingIntegrityFixture()
        {
            Customer = TestData.AddCustomer(T, name: "Synthetic posting customer");
            Supplier = TestData.AddSupplier(T, name: "Synthetic posting supplier", openingPaisa: 100000);
            Execute("UPDATE customers SET opening_balance_paisa=100000,balance_paisa=100000 WHERE id=" + Customer);
            TestData.AddProduct(T); // Snapshot proves these ledger operations leave stock alone.
            Assert.True(TestData.Shifts(T).Open(TestData.Owner, 500000, "Synthetic integrity drawer").Ok);
            Services = new PostingServices(T.Database, T.Log);
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql)));
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "SELECT quote(id)||':'||quote(balance_paisa)||':'||quote(opening_balance_paisa) FROM customers ORDER BY id;",
            "SELECT quote(id)||':'||quote(balance_paisa)||':'||quote(opening_balance_paisa) FROM suppliers ORDER BY id;",
            "SELECT quote(id)||':'||quote(customer_id)||':'||quote(supplier_id)||':'||quote(amount_paisa)||':'||quote(method)||':'||quote(note) FROM payments ORDER BY id;",
            "SELECT quote(id)||':'||quote(user_name)||':'||quote(action)||':'||quote(old_value)||':'||quote(new_value) FROM audit_logs ORDER BY id;",
            "SELECT quote(id)||':'||quote(product_id)||':'||quote(qty_base)||':'||quote(cost_paisa) FROM batches ORDER BY id;"
        }.Select(sql => string.Join(";", T.Database.Query<string>(c, sql)))));
        public (bool Ok, int Id, string? Error) Post(bool supplier, long amount = 30000, string method = "cash", PostingServices? services = null, User? user = null)
        {
            var s = services ?? Services; var actor = user ?? TestData.Owner;
            if (supplier)
            {
                var result = s.Suppliers.Pay(actor, Supplier, amount, method, "Synthetic posting");
                return (result.Ok, result.Value?.PaymentId ?? 0, result.Error);
            }
            var collection = s.Customers.Collect(actor, Customer, amount, method, "Synthetic posting");
            return (collection.Ok, collection.Value?.PaymentId ?? 0, collection.Error);
        }
        public void Reconcile(long customerBalance, long supplierBalance, long cashIn, long cashOut)
        {
            var customer = Services.Customers.Page(TestData.Owner, Customer); Assert.True(customer.Ok, customer.Error);
            Assert.True(customer.Value!.BooksMatch); Assert.Equal(customerBalance, customer.Value.StoredPaisa);
            var supplier = Services.Suppliers.Page(TestData.Owner, Supplier); Assert.True(supplier.Ok, supplier.Error);
            Assert.True(supplier.Value!.BooksMatch); Assert.Equal(supplierBalance, supplier.Value.StoredPaisa);
            var page = TestData.Shifts(T).Page(TestData.Owner); Assert.True(page.Ok, page.Error); Assert.NotNull(page.Value!.Current);
            var drawer = page.Value.Current!;
            Assert.Equal(cashIn, drawer.CashCollectionsPaisa); Assert.Equal(cashOut, drawer.SupplierCashOutPaisa);
            Assert.Equal(500000 + cashIn - cashOut, drawer.ExpectedPaisa);
        }
        public void Fault(bool supplier, string stage)
        {
            var eventSql = stage switch
            {
                "payment-before" => "BEFORE INSERT ON payments",
                "payment-after" => "AFTER INSERT ON payments",
                "balance-before" => supplier ? "BEFORE UPDATE OF balance_paisa ON suppliers" : "BEFORE UPDATE OF balance_paisa ON customers",
                "balance-after" => supplier ? "AFTER UPDATE OF balance_paisa ON suppliers" : "AFTER UPDATE OF balance_paisa ON customers",
                "audit-before" => "BEFORE INSERT ON audit_logs",
                "audit-after" => "AFTER INSERT ON audit_logs",
                _ => throw new ArgumentOutOfRangeException(nameof(stage))
            };
            var condition = stage.StartsWith("audit",StringComparison.Ordinal) ? " WHEN NEW.action='" + (supplier ? "supplier_pay" : "customer_collect") + "'" : string.Empty;
            Execute("CREATE TRIGGER posting_fault " + eventSql + condition + " BEGIN SELECT RAISE(ABORT,'private-posting-fault'); END;");
        }
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData(false, "payment-before")]
    [InlineData(false, "payment-after")]
    [InlineData(false, "balance-before")]
    [InlineData(false, "balance-after")]
    [InlineData(false, "audit-before")]
    [InlineData(false, "audit-after")]
    [InlineData(true, "payment-before")]
    [InlineData(true, "payment-after")]
    [InlineData(true, "balance-before")]
    [InlineData(true, "balance-after")]
    [InlineData(true, "audit-before")]
    [InlineData(true, "audit-after")]
    public void StageFaultRollsBackPaymentBalanceAuditAndDrawerThenOneRetrySucceeds(bool supplier, string stage)
    {
        using var f = new PostingIntegrityFixture(); var before = f.Snapshot(); f.Fault(supplier, stage);
        var denied = f.Post(supplier); Assert.False(denied.Ok); Assert.DoesNotContain("private-posting-fault", denied.Error!);
        Assert.Equal(before, f.Snapshot()); f.Reconcile(100000,100000,0,0);
        f.Execute("DROP TRIGGER posting_fault;"); var retry = f.Post(supplier); Assert.True(retry.Ok, retry.Error);
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM payments;"));
        f.Reconcile(supplier ? 100000 : 70000, supplier ? 70000 : 100000, supplier ? 0 : 30000, supplier ? 30000 : 0);
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("card")]
    [InlineData("online")]
    public void BothAccountBooksPaymentsCustomerReceiptAndDrawerAgreeByMethod(string method)
    {
        using var f = new PostingIntegrityFixture(); var incoming = f.Post(false,30000,method); var outgoing = f.Post(true,20000,method);
        Assert.True(incoming.Ok,incoming.Error); Assert.True(outgoing.Ok,outgoing.Error);
        Assert.NotEqual(incoming.Id,outgoing.Id); Assert.True(f.Services.Customers.PaymentReceipt(TestData.Owner,incoming.Id).Ok);
        Assert.Equal(2L,f.Number("SELECT COUNT(*) FROM payments WHERE sale_id IS NULL AND purchase_id IS NULL;"));
        f.Reconcile(70000,80000,method == "cash" ? 30000 : 0,method == "cash" ? 20000 : 0);
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void CompetingOverSettlementCannotCommitBothOnSameOrSeparateDatabaseInstances(bool supplier, bool separate)
    {
        using var f = new PostingIntegrityFixture();
        var other = separate ? new Database(f.T.Database.FilePath, pooling:false, durable:false) : f.T.Database;
        var aServices = new PostingServices(f.T.Database,f.T.Log); var bServices = new PostingServices(other,f.T.Log);
        using var start = new Barrier(2);
        var a = Task.Factory.StartNew(() => { Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(10))); return f.Post(supplier,60000,services:aServices); }, CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
        var b = Task.Factory.StartNew(() => { Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(10))); return f.Post(supplier,60000,services:bServices); }, CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
        Assert.True(Task.WaitAll(new Task[] {a,b},TimeSpan.FromSeconds(30)));
        Assert.Equal(1,new[] {a.Result.Ok,b.Result.Ok}.Count(ok => ok));
        Assert.Equal(1L,f.Number("SELECT COUNT(*) FROM payments;"));
        f.Reconcile(supplier ? 100000 : 40000,supplier ? 40000 : 100000,supplier ? 0 : 60000,supplier ? 60000 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshServiceAfterFullSettlementRejectsSameAmountWithoutSecondPayment(bool supplier)
    {
        using var f = new PostingIntegrityFixture(); Assert.True(f.Post(supplier,100000).Ok); var before = f.Snapshot();
        var restarted = new PostingServices(new Database(f.T.Database.FilePath,pooling:false,durable:false),f.T.Log);
        Assert.False(f.Post(supplier,100000,services:restarted).Ok); Assert.Equal(before,f.Snapshot());
        f.Reconcile(supplier ? 100000 : 0,supplier ? 0 : 100000,supplier ? 0 : 100000,supplier ? 100000 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RevokedReadAfterCommitDoesNotUndoOrRepeatStoredPayment(bool supplier)
    {
        using var f = new PostingIntegrityFixture(); var staff = TestData.AddUser(f.T,"manager"); var module = supplier ? Modules.Suppliers : Modules.Customers;
        Assert.True(f.Post(supplier,user:staff).Ok);
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner,staff.Id,module,false).Ok);
        if (supplier) Assert.False(f.Services.Suppliers.Statement(staff,f.Supplier).Ok);
        else Assert.False(f.Services.Customers.PaymentReceipt(staff,(int)f.Number("SELECT MAX(id) FROM payments;")).Ok);
        Assert.Equal(1L,f.Number("SELECT COUNT(*) FROM payments;"));
        Assert.Equal(70000L,f.Number("SELECT balance_paisa FROM " + (supplier ? "suppliers" : "customers") + " WHERE id=" + (supplier ? f.Supplier : f.Customer)));
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner,staff.Id,module,true).Ok);
        f.Reconcile(supplier ? 100000 : 70000,supplier ? 70000 : 100000,supplier ? 0 : 30000,supplier ? 30000 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedGrantReadCannotPostFromRoleDefault(bool supplier)
    {
        using var f = new PostingIntegrityFixture(); var user = TestData.AddUser(f.T,"manager"); f.Execute("DROP TABLE user_permissions;"); var before = f.Snapshot();
        Assert.False(f.Post(supplier,user:user).Ok); Assert.Equal(before,f.Snapshot()); f.Reconcile(100000,100000,0,0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InactiveParentCannotLeavePaymentOrDrawerMovement(bool supplier)
    {
        using var f = new PostingIntegrityFixture(); f.Execute(supplier ? "UPDATE suppliers SET active=0 WHERE id=" + f.Supplier : "UPDATE customers SET active=0 WHERE id=" + f.Customer);
        var before = f.Snapshot(); Assert.False(f.Post(supplier).Ok); Assert.Equal(before,f.Snapshot());
        Assert.Equal(0L,f.Number("SELECT COUNT(*) FROM payments;"));
    }

    [Fact] public void PartialRepeatsAreDistinctNewPaymentsNotGlobalIdempotency()
    {
        using var f = new PostingIntegrityFixture();
        foreach (var supplier in new[] {false,true})
        {
            var first = f.Post(supplier,10000); var second = f.Post(supplier,10000);
            Assert.True(first.Ok); Assert.True(second.Ok); Assert.NotEqual(first.Id,second.Id);
        }
        Assert.Equal(4L,f.Number("SELECT COUNT(*) FROM payments;")); f.Reconcile(80000,80000,20000,20000);
    }
    [Fact] public void BothPostsAuditCurrentStoredActorName()
    {
        using var f = new PostingIntegrityFixture(); f.Execute("UPDATE users SET name='Stored posting actor' WHERE id=1;");
        Assert.True(f.Post(false).Ok); Assert.True(f.Post(true).Ok);
        Assert.Equal(2L,f.Number("SELECT COUNT(*) FROM audit_logs WHERE action IN ('customer_collect','supplier_pay') AND user_name='Stored posting actor';"));
    }
    [Fact] public void FailedSecondActionDoesNotRollBackFirstCommittedAction()
    {
        using var f = new PostingIntegrityFixture(); Assert.True(f.Post(false).Ok); var before = f.Snapshot(); f.Fault(true,"audit-after");
        Assert.False(f.Post(true).Ok); Assert.Equal(before,f.Snapshot()); f.Reconcile(70000,100000,30000,0);
    }
    [Fact] public void IndependentCustomerAndSupplierIdsKeepMovementsSeparate()
    {
        using var f = new PostingIntegrityFixture(); var otherCustomer = TestData.AddCustomer(f.T,name:"Other synthetic customer");
        var otherSupplier = TestData.AddSupplier(f.T,name:"Other synthetic supplier",openingPaisa:50000);
        Assert.True(f.Post(false).Ok); Assert.True(f.Post(true).Ok);
        Assert.Equal(0L,f.Number("SELECT balance_paisa FROM customers WHERE id="+otherCustomer));
        Assert.Equal(50000L,f.Number("SELECT balance_paisa FROM suppliers WHERE id="+otherSupplier));
        Assert.Equal(0L,f.Number("SELECT COUNT(*) FROM payments WHERE customer_id="+otherCustomer+" OR supplier_id="+otherSupplier));
    }
    [Fact] public void AccountingPostsKeepIndependentFinancialGrantsAndSupplierExceptionB()
    {
        using var f = new PostingIntegrityFixture(); var user = TestData.AddUser(f.T,"manager");
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner,user.Id,Modules.Profit,false).Ok);
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner,user.Id,Modules.PurchaseCost,false).Ok);
        TestData.SeedRawSetting(f.T,MasterFoundationSettings.ProfitShowMargins,false);
        Assert.True(f.Post(false,user:user).Ok); Assert.True(f.Post(true,user:user).Ok); f.Reconcile(70000,70000,30000,30000);
    }
}
