using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Read-only Recovery/monthly-report access; synthetic fixtures, Windows execution required.</summary>
public sealed class RecoveryReportAccessTests
{
    private static readonly DateTime Today = new(2026, 11, 10);
    private static TaxService Tax(TestDatabase t) => new(t.Database, t.Settings, TestData.Permissions(t), t.Log);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static void Grant(TestDatabase t, User u, string module, bool allow = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, module, allow).Ok);
    private static User Reader(TestDatabase t, string module)
    {
        var u = TestData.Cashier(t); Grant(t, u, module); return u;
    }
    private static int Customer(TestDatabase t)
    {
        var id = TestData.AddCustomer(t); Execute(t, "UPDATE customers SET opening_balance_paisa=100000,balance_paisa=100000 WHERE id=" + id); return id;
    }
    private static void StoredMonth(TestDatabase t) => Execute(t, @"
INSERT INTO sales(code,date,status,subtotal_paisa,discount_paisa,tax_paisa,total_paisa,paid_paisa,user_id)
VALUES('SYN-MONTH','2026-11-04 12:00:00','paid',30000,0,1234,31000,31000,1);");
    private static void Denied(TestDatabase t, User? actor)
    {
        var customer = Customer(t); StoredMonth(t);
        Execute(t, "INSERT INTO payments(customer_id,amount_paisa,method,user_id) VALUES(" + customer + ",25000,'cash',1);");
        var id = (int)Number(t, "SELECT MAX(id) FROM payments;");
        Execute(t, "UPDATE customers SET balance_paisa=75000 WHERE id=" + customer);
        var audit = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        var recovery = TestData.Recovery(t); var tax = Tax(t);
        Assert.False(recovery.CheckAccess(actor).Ok); Assert.False(recovery.Report(actor, Today).Ok);
        Assert.False(recovery.Slip(id, actor).Ok); Assert.False(recovery.SlipDocument(id, actor).Ok);
        Assert.False(tax.CheckAccess(actor).Ok); Assert.False(tax.Report(actor, 2026, 11).Ok);
        Assert.Equal(audit, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal(75000L, Number(t, "SELECT balance_paisa FROM customers WHERE id=" + customer));
    }

    [Fact]
    public void AnonymousCannotReadRecoverySlipOrMonth()
    {
        using var t = new TestDatabase(); Denied(t, null);
    }

    [Fact]
    public void InactiveCallerCannotReadRecoverySlipOrMonth()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = 1, Role = "owner", Active = false });
    }

    [Fact]
    public void MissingStoredIdentityCannotReadRecoveryOrMonth()
    {
        using var t = new TestDatabase(); Denied(t, new User { Id = int.MaxValue, Role = "owner" });
    }

    [Fact]
    public void ForgedOwnerRoleCannotBorrowStoredStaffIdentity()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Denied(t, new User { Id = u.Id, Role = "owner" });
    }

    [Fact]
    public void ChangedStoredRoleBlocksRecoveryAndMonthlyReads()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE users SET role='manager' WHERE id=1;"); Denied(t, TestData.Owner);
    }

    [Fact]
    public void InactiveStoredOwnerCannotReadRecoveryOrMonth()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE users SET active=0 WHERE id=1;"); Denied(t, TestData.Owner);
    }

    [Fact]
    public void FinancialGrantsDoNotReplaceOwnReadModules()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Grant(t, u, Modules.Customers, false); Grant(t, u, Modules.Profit); Grant(t, u, Modules.PurchaseCost); Denied(t, u);
    }

    [Fact]
    public void CustomersOnlyCanReadRecoveryAndSlipButNotReports()
    {
        using var t = new TestDatabase(); var u = Reader(t, Modules.Customers); var c = Customer(t);
        var payment = TestData.Ledger(t).Collect(u, c, 25000, "cash", null); Assert.True(payment.Ok, payment.Error);
        var service = TestData.Recovery(t); Assert.True(service.CheckAccess(u).Ok); Assert.True(service.Report(u, Today).Ok);
        Assert.True(service.SlipDocument(payment.Value!.PaymentId, u).Ok); Assert.False(Tax(t).Report(u, 2026, 11).Ok);
    }

    [Fact]
    public void ReportsOnlyCanReadMonthButNotRecovery()
    {
        using var t = new TestDatabase(); var u = Reader(t, Modules.Reports); Grant(t, u, Modules.Customers, false); StoredMonth(t);
        var report = Tax(t).Report(u, 2026, 11); Assert.True(report.Ok, report.Error); Assert.Equal(31000L, report.Value!.GrossPaisa);
        Assert.False(TestData.Recovery(t).Report(u, Today).Ok);
    }

    [Fact]
    public void MainOffAndFinancialDenialsDoNotBlockOrdinaryModuleReports()
    {
        using var t = new TestDatabase(); var u = Reader(t, Modules.Customers); Grant(t, u, Modules.Reports); Customer(t); StoredMonth(t);
        Grant(t, u, Modules.Profit, false); Grant(t, u, Modules.PurchaseCost, false);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.True(TestData.Recovery(t).Report(u, Today).Ok); Assert.True(Tax(t).Report(u, 2026, 11).Ok);
    }

    [Fact]
    public void RecoveryRevocationBlocksFreshReportAndPreparedSlipId()
    {
        using var t = new TestDatabase(); var u = Reader(t, Modules.Customers); var c = Customer(t); var service = TestData.Recovery(t);
        var paid = TestData.Ledger(t).Collect(u, c, 25000, "cash", null); Assert.True(paid.Ok, paid.Error);
        Assert.True(service.Slip(paid.Value!.PaymentId, u).Ok); Grant(t, u, Modules.Customers, false);
        Assert.False(service.Report(u, Today).Ok); Assert.False(service.Slip(paid.Value.PaymentId, u).Ok);
        Assert.False(service.SlipDocument(paid.Value.PaymentId, u).Ok);
    }

    [Fact]
    public void ReportRevocationBlocksFreshTextPreparationSource()
    {
        using var t = new TestDatabase(); var u = Reader(t, Modules.Reports); StoredMonth(t); var service = Tax(t);
        Assert.Contains("TOTAL", TaxService.ToText(service.Report(u, 2026, 11).Value!)); Grant(t, u, Modules.Reports, false);
        var denied = service.Report(u, 2026, 11); Assert.False(denied.Ok); Assert.Null(denied.Value);
    }

    [Fact]
    public void RestoredReadGrantsWorkOnSameServiceInstances()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var recovery = TestData.Recovery(t); var tax = Tax(t); Grant(t, u, Modules.Customers, false);
        Assert.False(recovery.CheckAccess(u).Ok); Assert.False(tax.CheckAccess(u).Ok);
        Grant(t, u, Modules.Customers); Grant(t, u, Modules.Reports);
        Assert.True(recovery.Report(u, Today).Ok); Assert.True(tax.Report(u, 2026, 11).Ok);
    }

    [Fact]
    public void SalePaymentCannotBecomeRecoverySlip()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var c = TestData.AddCustomer(t);
        Assert.True(TestData.Sales(t).Create(TestData.Cart(p, customerId: c), TestData.Owner).Ok);
        var payment = (int)Number(t, "SELECT id FROM payments WHERE sale_id IS NOT NULL LIMIT 1;");
        Assert.False(TestData.Recovery(t).Slip(payment, TestData.Owner).Ok); Assert.False(TestData.Recovery(t).Slip(0, TestData.Owner).Ok);
    }

    [Fact]
    public void FullSettlementRemovesOwingRowButKeepsHistoricalSlip()
    {
        using var t = new TestDatabase(); var c = Customer(t); var paid = TestData.Ledger(t).Collect(TestData.Owner, c, 100000, "cash", null);
        Assert.True(paid.Ok, paid.Error); var recovery = TestData.Recovery(t); Assert.Empty(recovery.Report(TestData.Owner, Today).Value!.Rows);
        Assert.Equal(0L, recovery.Slip(paid.Value!.PaymentId, TestData.Owner).Value!.BalanceAfterPaisa);
    }

    [Fact]
    public void LaterCollectionDoesNotRewriteOldSlipBalance()
    {
        using var t = new TestDatabase(); var c = Customer(t); var ledger = TestData.Ledger(t);
        var first = ledger.Collect(TestData.Owner, c, 25000, "cash", null); Assert.True(first.Ok, first.Error);
        Assert.True(ledger.Collect(TestData.Owner, c, 10000, "online", null).Ok);
        var slip = TestData.Recovery(t).Slip(first.Value!.PaymentId, TestData.Owner).Value!;
        Assert.Equal(100000L, slip.BalanceBeforePaisa); Assert.Equal(75000L, slip.BalanceAfterPaisa); Assert.Equal(25000L, slip.ReceivedPaisa);
    }

    [Fact]
    public void RecoverySlipKeepsCollectorRatherThanPrintingReaderName()
    {
        using var t = new TestDatabase(); var u = Reader(t, Modules.Customers); var c = Customer(t);
        Execute(t, "UPDATE users SET name='Collector A' WHERE id=" + u.Id);
        var paid = TestData.Ledger(t).Collect(u, c, 25000, "cash", null); Assert.True(paid.Ok, paid.Error);
        Execute(t, "UPDATE users SET name='Printing Reader B' WHERE id=1;");
        Assert.Equal("Collector A", TestData.Recovery(t).Slip(paid.Value!.PaymentId, TestData.Owner).Value!.ReceivedBy);
    }

    [Fact]
    public void OpeningDebtStaysSeparateFromInventedAgeColumns()
    {
        using var t = new TestDatabase(); Customer(t); var report = TestData.Recovery(t).Report(TestData.Owner, Today).Value!;
        Assert.True(report.BooksMatch); Assert.Equal(100000L, report.OpeningTotalPaisa); Assert.Equal(0L, report.BucketsTotalPaisa);
    }

    [Fact]
    public void CurrentReaderDoesNotChangeSixtyDayAgeBoundary()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t); var c = TestData.AddCustomer(t);
        var sale = TestData.Sales(t).Create(TestData.Cart(p, method: "credit", customerId: c), TestData.Owner); Assert.True(sale.Ok, sale.Error);
        using (var connection = t.Database.Open())
            t.Database.Execute(connection, "UPDATE sales SET date=@at WHERE id=@id;", new { at = Today.AddDays(-60).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), id = sale.Value!.SaleId });
        var report = TestData.Recovery(t).Report(TestData.Owner, Today).Value!;
        Assert.Equal(30000L, report.Bucket1Paisa); Assert.Equal(0L, report.Bucket2Paisa); Assert.True(report.BooksMatch);
    }

    [Fact]
    public void MonthReadsStoredAmountsWithoutEnablingOrRecalculatingTax()
    {
        using var t = new TestDatabase(); StoredMonth(t); var before = Number(t, "SELECT tax_paisa FROM sales WHERE code='SYN-MONTH';");
        var report = Tax(t).Report(TestData.Owner, 2026, 11).Value!;
        Assert.False(report.TaxEnabled); Assert.Equal(1234L, report.TaxPaisa); Assert.Equal(30000L, report.NetPaisa);
        Assert.Equal(-234L, report.RoundOffPaisa); Assert.Equal(before, Number(t, "SELECT tax_paisa FROM sales WHERE code='SYN-MONTH';"));
    }

    [Fact]
    public void InvalidMonthDoesNotProduceSuccessfulEmptyZero()
    {
        using var t = new TestDatabase(); var service = Tax(t);
        foreach (var pair in new[] { (1999, 11), (2101, 11), (2026, 0), (2026, 13) })
        { var report = service.Report(TestData.Owner, pair.Item1, pair.Item2); Assert.False(report.Ok); Assert.Null(report.Value); }
    }

    [Fact]
    public void HoldVoidAndOtherMonthRemainExcluded()
    {
        using var t = new TestDatabase(); StoredMonth(t);
        Execute(t, "INSERT INTO sales(code,date,status,total_paisa,tax_paisa) VALUES('SYN-HOLD','2026-11-05','hold',999999,99999),('SYN-VOID','2026-11-05','void',888888,88888),('SYN-OTHER','2026-10-05','paid',777777,77777);");
        var report = Tax(t).Report(TestData.Owner, 2026, 11).Value!; Assert.Equal(1, report.Bills); Assert.Equal(1234L, report.TaxPaisa);
    }

    [Fact]
    public void FreshReportAndSlipReadsDoNotWriteFinancialOrAuditRows()
    {
        using var t = new TestDatabase(); var c = Customer(t); StoredMonth(t); var paid = TestData.Ledger(t).Collect(TestData.Owner, c, 25000, "cash", null); Assert.True(paid.Ok, paid.Error);
        var audit = Number(t, "SELECT COUNT(*) FROM audit_logs;"); var payments = Number(t, "SELECT COUNT(*) FROM payments;");
        var recovery = TestData.Recovery(t); Assert.True(recovery.CheckAccess(TestData.Owner).Ok); Assert.True(recovery.Report(TestData.Owner, Today).Ok);
        Assert.True(recovery.SlipDocument(paid.Value!.PaymentId, TestData.Owner).Ok); Assert.Contains("TOTAL", TaxService.ToText(Tax(t).Report(TestData.Owner, 2026, 11).Value!));
        Assert.Equal(audit, Number(t, "SELECT COUNT(*) FROM audit_logs;")); Assert.Equal(payments, Number(t, "SELECT COUNT(*) FROM payments;"));
        Assert.Equal(75000L, Number(t, "SELECT balance_paisa FROM customers WHERE id=" + c));
    }

    [Fact]
    public void DatabaseFailuresReturnNoRawSchemaDetailsOrReport()
    {
        using var t = new TestDatabase(); StoredMonth(t); Execute(t, "DROP TABLE sales;");
        var tax = Tax(t).Report(TestData.Owner, 2026, 11); Assert.False(tax.Ok); Assert.Null(tax.Value); Assert.DoesNotContain("no such table", tax.Error!);
        var recovery = TestData.Recovery(t).Report(TestData.Owner, Today); Assert.False(recovery.Ok); Assert.Null(recovery.Value); Assert.DoesNotContain("no such table", recovery.Error!);
    }
}
