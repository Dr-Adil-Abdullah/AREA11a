using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Real-schema source regressions plus synthetic guard observations. Not executed here;
/// synthetic unknown outcomes are NOT actual provider COMMIT/rollback fault injection.</summary>
public sealed class LedgerOutcomeFaultTests
{
    private sealed class LedgerOutcomeFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public int Customer { get; }
        public int Supplier { get; }
        public CustomerLedgerService Collections { get; }
        public SupplierLedgerService Payments { get; }
        public LedgerOutcomeFixture()
        {
            Customer = TestData.AddCustomer(T);
            Supplier = TestData.AddSupplier(T, openingPaisa: 100000);
            Execute("UPDATE customers SET balance_paisa=100000,opening_balance_paisa=100000 WHERE id=" + Customer);
            var permissions = TestData.Permissions(T);
            Collections = new CustomerLedgerService(T.Database, T.Audit, T.Settings, permissions, T.Log);
            Payments = new SupplierLedgerService(T.Database, T.Audit, new NumberingService(T.Settings), T.Settings, permissions, T.Log);
            Assert.True(TestData.Shifts(T).Open(TestData.Owner, 500000, "Synthetic outcome drawer").Ok);
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public ServiceResult Post(bool supplier, long amount = 30000, string method = "cash", User? actor = null, int? id = null) => supplier
            ? Payments.Pay(actor ?? TestData.Owner, id ?? Supplier, amount, method, "Synthetic outcome")
            : Collections.Collect(actor ?? TestData.Owner, id ?? Customer, amount, method, "Synthetic outcome");
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "SELECT quote(id)||':'||quote(balance_paisa) FROM customers ORDER BY id;",
            "SELECT quote(id)||':'||quote(balance_paisa) FROM suppliers ORDER BY id;",
            "SELECT quote(id)||':'||quote(amount_paisa)||':'||quote(method) FROM payments ORDER BY id;",
            "SELECT quote(id)||':'||quote(action)||':'||quote(old_value)||':'||quote(new_value) FROM audit_logs ORDER BY id;",
            "SELECT quote(id)||':'||quote(qty_base) FROM stock_movements ORDER BY id;"
        }.Select(sql => string.Join(";", T.Database.Query<string>(c, sql)))));
        public void Reconcile(bool supplier, long amount, string method)
        {
            var customer = Collections.Page(TestData.Owner, Customer); Assert.True(customer.Ok); Assert.True(customer.Value!.BooksMatch);
            var vendor = Payments.Page(TestData.Owner, Supplier); Assert.True(vendor.Ok); Assert.True(vendor.Value!.BooksMatch);
            Assert.Equal(supplier ? 100000L : 100000L - amount, customer.Value.StoredPaisa);
            Assert.Equal(supplier ? 100000L - amount : 100000L, vendor.Value.StoredPaisa);
            var page = TestData.Shifts(T).Page(TestData.Owner); Assert.True(page.Ok); var drawer = page.Value!.Current!;
            Assert.Equal(!supplier && method == "cash" ? amount : 0L, drawer.CashCollectionsPaisa);
            Assert.Equal(supplier && method == "cash" ? amount : 0L, drawer.SupplierCashOutPaisa);
            Assert.Equal(500000L + (!supplier && method == "cash" ? amount : 0L) - (supplier && method == "cash" ? amount : 0L), drawer.ExpectedPaisa);
        }
        public void Fault(bool supplier, string stage)
        {
            var table = supplier ? "suppliers" : "customers";
            var clause = stage switch
            {
                "payment-before" => "BEFORE INSERT ON payments",
                "payment-after" => "AFTER INSERT ON payments",
                "balance-before" => "BEFORE UPDATE OF balance_paisa ON " + table,
                "balance-after" => "AFTER UPDATE OF balance_paisa ON " + table,
                "audit-before" => "BEFORE INSERT ON audit_logs",
                _ => "AFTER INSERT ON audit_logs"
            };
            Execute("CREATE TRIGGER outcome_fault " + clause + " BEGIN SELECT RAISE(ABORT,'synthetic-private-outcome-fault'); END;");
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
    public void ConfirmedStageRollbackExplicitlyAllowsOneLocalRetry(bool supplier, string stage)
    {
        using var f = new LedgerOutcomeFixture(); var before = f.Snapshot(); f.Fault(supplier, stage);
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); var result = f.Post(supplier);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.DoesNotContain("synthetic-private", result.Error!);
        guard.ObserveFailure(result); Assert.True(guard.Ready); Assert.Equal(before, f.Snapshot()); f.Reconcile(supplier, 0, "cash");
        f.Execute("DROP TRIGGER outcome_fault;"); Assert.True(guard.TryBegin()); Assert.True(f.Post(supplier).Ok);
        guard.AcknowledgeCommit(); Assert.False(guard.TryBegin()); f.Reconcile(supplier, 30000, "cash"); guard.CompleteReset();
        Assert.True(guard.Ready);
    }

    [Theory]
    [InlineData(false, "zero")]
    [InlineData(false, "negative")]
    [InlineData(false, "method")]
    [InlineData(false, "missing")]
    [InlineData(false, "over")]
    [InlineData(false, "inactive")]
    [InlineData(false, "grant")]
    [InlineData(true, "zero")]
    [InlineData(true, "negative")]
    [InlineData(true, "method")]
    [InlineData(true, "missing")]
    [InlineData(true, "over")]
    [InlineData(true, "inactive")]
    [InlineData(true, "grant")]
    public void DefiniteRefusalsAreNotAmbiguousAndLeaveBooksUnchanged(bool supplier, string reason)
    {
        using var f = new LedgerOutcomeFixture(); var actor = TestData.AddUser(f.T, "manager");
        if (reason == "inactive") f.Execute("UPDATE users SET active=0 WHERE id=" + actor.Id);
        if (reason == "grant") Assert.True(TestData.Permissions(f.T).Set(TestData.Owner, actor.Id, supplier ? Modules.Suppliers : Modules.Customers, false).Ok);
        var before = f.Snapshot(); var amount = reason switch { "zero" => 0L, "negative" => -1L, "over" => 100001L, _ => 30000L };
        var result = f.Post(supplier, amount, reason == "method" ? "credit" : "cash", actor, reason == "missing" ? int.MaxValue : null);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, f.Snapshot()); f.Reconcile(supplier, 0, "cash");
    }

    [Theory]
    [InlineData(false, "cash")]
    [InlineData(false, "online")]
    [InlineData(false, "card")]
    [InlineData(true, "cash")]
    [InlineData(true, "online")]
    [InlineData(true, "card")]
    public void DistinctLegitimatePartialPaymentsRemainAllowed(bool supplier, string method)
    {
        using var f = new LedgerOutcomeFixture(); Assert.True(f.Post(supplier, 10000, method).Ok); Assert.True(f.Post(supplier, 10000, method).Ok);
        f.Reconcile(supplier, 20000, method);
        Assert.Equal(2L, f.T.Database.Read(c => Convert.ToInt64(f.T.Database.Scalar(c, "SELECT COUNT(*) FROM payments;"))));
    }

    [Theory]
    [InlineData("customer", "unknown")]
    [InlineData("customer", "confirmed")]
    [InlineData("customer", "throw")]
    [InlineData("customer", "ack-fault")]
    [InlineData("customer", "complete")]
    [InlineData("recovery", "unknown")]
    [InlineData("recovery", "confirmed")]
    [InlineData("recovery", "throw")]
    [InlineData("recovery", "ack-fault")]
    [InlineData("recovery", "complete")]
    [InlineData("supplier", "unknown")]
    [InlineData("supplier", "confirmed")]
    [InlineData("supplier", "throw")]
    [InlineData("supplier", "ack-fault")]
    [InlineData("supplier", "complete")]
    public void SyntheticGuardObservationsDoNotTreatUnknownAsNoCommit(string page, string scenario)
    {
        // Page names document three consumers, not WPF execution or three distinct guard implementations.
        Assert.Contains(page, new[] { "customer", "recovery", "supplier" });
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); Assert.False(guard.TryBegin());
        if (scenario is "unknown" or "confirmed") guard.ObserveFailure(new ServiceResult { NoCommitConfirmed = scenario == "confirmed" });
        else if (scenario == "throw") guard.ObserveException();
        else { guard.AcknowledgeCommit(); guard.ObserveException(); if (scenario == "complete") guard.CompleteReset(); }
        Assert.Equal(scenario is "confirmed" or "complete", guard.Ready);
        Assert.Equal(scenario is "ack-fault" or "complete", guard.WasAcknowledged);
        if (!guard.Ready) Assert.False(guard.TryBegin());
    }
}
