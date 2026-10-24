using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The tax report, the money-received slip and the monthly statement (F-49, Spec 6.5, 13.5, 14.2,
/// 33.4.1).
///
/// Three promises, each with its own tests:
///   1. **the tax report is a witness, not a calculator** - every rupee comes from the bill, a rate
///      changed later never rewrites an old month, and a cashier cannot open it;
///   2. **a month's statement starts where the last one ended** - the opening balance is replayed
///      from the history, so no stored snapshot can drift;
///   3. **a receipt keeps its own numbers** - reprinted next month, the PAY slip still shows the
///      balance as it stood on the day the money was received.
/// </summary>
public sealed class TaxStatementTests
{
    // ============================== the tax report ==============================

    [Fact]
    public void TheTaxReportAddsUpTheMonth()
    {
        using var test = new TestDatabase();
        EnableTax(test, "GST", 17);

        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        var cash = MakeBill(test, product, qty: 1);
        var credit = MakeBill(test, product, qty: 2, customer: customer);

        var report = Tax(test).Report(TestData.Owner, DateTime.Now.Year, DateTime.Now.Month);

        Assert.True(report.Ok, report.Error);

        var value = report.Value!;

        Assert.Equal(2, value.Bills);
        Assert.Equal(90000L, value.NetPaisa);
        Assert.Equal(15300L, value.TaxPaisa);

        // Every bill is rounded DOWN to the shop's step, Rs 10 (Spec 11.3.2 / Z-03): Rs 351 and
        // Rs 702 become Rs 350 and Rs 700, so the month really took Rs 1,050 - not 1,053.
        Assert.Equal(105000L, value.GrossPaisa);
        Assert.Equal(-300L, value.RoundOffPaisa);
        Assert.True(value.HasRoundOff);

        Assert.Equal(cash.TaxPaisa + credit.TaxPaisa, value.TaxPaisa);
        Assert.Single(value.Rows);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), value.Rows[0].Day);
    }

    [Fact]
    public void AnotherMonthHasNoBills()
    {
        using var test = new TestDatabase();
        EnableTax(test, "GST", 17);

        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        MakeBill(test, product, qty: 1);

        var lastMonth = DateTime.Now.AddMonths(-1);
        var report = Tax(test).Report(TestData.Owner, lastMonth.Year, lastMonth.Month);

        Assert.True(report.Ok, report.Error);
        Assert.Equal(0, report.Value!.Bills);
        Assert.Equal(0L, report.Value.TaxPaisa);
        Assert.Empty(report.Value.Rows);
        Assert.False(report.Value.HasRows);
    }

    [Fact]
    public void TheTaxReportIsForTheReportsPeople()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);

        var refused = Tax(test).Report(cashier, DateTime.Now.Year, DateTime.Now.Month);

        Assert.False(refused.Ok);
        Assert.Contains("permission", refused.Error);
    }

    [Fact]
    public void TheTaxSettingsAreShownHonestly()
    {
        using var test = new TestDatabase();
        EnableTax(test, "GST", 17);

        var enabled = Tax(test).Report(TestData.Owner, 2026, 10).Value!;

        Assert.Contains("GST 17%", enabled.SettingLabel);
        Assert.Contains("switched on", enabled.SettingLabel);

        Assert.True(test.Settings.Set("tax.enabled", false, TestData.Owner).Ok);
        test.Settings.Reload();

        var disabled = Tax(test).Report(TestData.Owner, 2026, 10).Value!;

        Assert.Contains("switched off", disabled.SettingLabel);
    }

    [Fact]
    public void TheTaxTextIsReadyToPaste()
    {
        using var test = new TestDatabase();
        EnableTax(test, "GST", 17);

        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        MakeBill(test, product, qty: 2);

        var report = Tax(test).Report(TestData.Owner, DateTime.Now.Year, DateTime.Now.Month).Value!;
        var text = TaxService.ToText(report);

        Assert.Contains("Tax report - " + report.MonthLabel, text);
        Assert.Contains("Date\tBills\tSales before tax\tTax\tTotal", text);
        Assert.Contains("TOTAL", text);
        Assert.Contains(Money.Format(report.TaxPaisa), text);
    }

    [Fact]
    public void TheRoundOffIsWrittenDownSoTheMonthAddsUp()
    {
        using var test = new TestDatabase();
        EnableTax(test, "GST", 17);

        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        MakeBill(test, product, qty: 2);

        var report = Tax(test).Report(TestData.Owner, DateTime.Now.Year, DateTime.Now.Month).Value!;
        var text = TaxService.ToText(report);

        // Rs 702 on the bill, Rs 700 taken: the Rs 2 is the round-off, and it is on the paper.
        Assert.Equal(-200L, report.RoundOffPaisa);
        Assert.Contains("Round-off down on the bills (Z-03): " + report.RoundOffLabel, text);
        Assert.Equal(report.GrossPaisa, report.NetPaisa + report.TaxPaisa + report.RoundOffPaisa);
    }

    // ============================== the monthly statement ==============================

    [Fact]
    public void AMonthlyStatementOnlyShowsThatMonth()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        var older = CreditBill(test, product, customer, qty: 1);
        MoveSaleTo(test, older.SaleId, DateTime.Now.AddMonths(-1).ToString("yyyy-MM") + "-03 10:00:00");

        var newer = CreditBill(test, product, customer, qty: 2);

        var lastMonth = DateTime.Now.AddMonths(-1).ToString("yyyy-MM");
        var thisMonth = DateTime.Now.ToString("yyyy-MM");

        var oldPaper = Ledger(test).Statement(customer, lastMonth);

        Assert.True(oldPaper.Ok, oldPaper.Error);
        Assert.Contains(older.Code, oldPaper.Value!.PlainText);
        Assert.DoesNotContain(newer.Code, oldPaper.Value!.PlainText);
        Assert.Contains("OPENING BALANCE", oldPaper.Value!.PlainText);
        Assert.Contains("CLOSING BALANCE", oldPaper.Value!.PlainText);

        var newPaper = Ledger(test).Statement(customer, thisMonth);

        Assert.True(newPaper.Ok, newPaper.Error);
        Assert.Contains(newer.Code, newPaper.Value!.PlainText);
        Assert.DoesNotContain(older.Code, newPaper.Value!.PlainText);

        // The month starts at 300 and ends at 900: 300 carried in + 600 sold.
        Assert.Contains(Money.Fixed(30000), newPaper.Value!.PlainText);
        Assert.Contains(Money.Fixed(90000), newPaper.Value!.PlainText);
        Assert.All(newPaper.Value!.Lines, line => Assert.True(line.Text.Length <= 32,
            "the line does not fit the paper: " + line.Text));
    }

    [Fact]
    public void AMalformedMonthIsRefused()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test);

        foreach (var month in new[] { "2026-13", "28-10-2026", "Oct 2026", "2026-1", " 2026-10-01 " })
        {
            Assert.False(Ledger(test).Statement(customer, month).Ok, month + " should be refused");
        }

        Assert.True(Ledger(test).Statement(customer, "2026-10").Ok);
        Assert.True(Ledger(test).Statement(customer, " ").Ok);   // empty = every movement, as before
    }

    [Fact]
    public void AQuietMonthStillPrintsItsOpeningAndClosing()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, qty: 2);

        var paper = Ledger(test).Statement(customer, "2025-01");

        Assert.True(paper.Ok, paper.Error);
        Assert.Contains("MONTHLY STATEMENT", paper.Value!.PlainText);
        Assert.Contains("OPENING BALANCE", paper.Value!.PlainText);
        Assert.Contains("CLOSING BALANCE", paper.Value!.PlainText);
        Assert.DoesNotContain("INV-", paper.Value!.PlainText);
    }

    [Fact]
    public void TheStatementNeverPrintsProfit()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, costPaisa: 20000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, qty: 2);

        var paper = Ledger(test).Statement(customer);

        Assert.True(paper.Ok, paper.Error);

        // The customer's copy is not the owner's report: profit stays behind the owner's permission.
        Assert.DoesNotContain("Lifetime profit", paper.Value!.PlainText);
        Assert.Contains("Lifetime purchases", paper.Value!.PlainText);
    }

    // ============================== the money-received slip ==============================

    [Fact]
    public void ThePaymentReceiptShowsTheBalanceBeforeAndAfter()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, qty: 2);

        var collected = Ledger(test).Collect(TestData.Owner, customer, 20000, "cash", "part payment");

        Assert.True(collected.Ok, collected.Error);

        var receipt = Ledger(test).PaymentReceipt(TestData.Owner, collected.Value!.PaymentId);

        Assert.True(receipt.Ok, receipt.Error);

        var paper = receipt.Value!.PlainText;

        Assert.Contains("MONEY RECEIVED", paper);
        Assert.Contains(collected.Value.Code, paper);
        Assert.Contains("Udhaar before", paper);
        Assert.Contains(Money.Fixed(60000), paper);
        Assert.Contains(Money.Fixed(40000), paper);
        Assert.Contains("cash", paper);
        Assert.Contains("part payment", paper);
    }

    [Fact]
    public void AnOldReceiptKeepsItsOwnBalance()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, qty: 2);

        var first = Ledger(test).Collect(TestData.Owner, customer, 20000, "cash", null);
        var second = Ledger(test).Collect(TestData.Owner, customer, 10000, "card", null);

        Assert.True(first.Ok, first.Error);
        Assert.True(second.Ok, second.Error);

        // Today the khata is 300. The first slip must still say 400 - what it said on its day.
        var firstPaper = Ledger(test).PaymentReceipt(TestData.Owner, first.Value!.PaymentId).Value!.PlainText;

        Assert.Contains(Money.Fixed(40000), firstPaper);
        Assert.DoesNotContain(Money.Fixed(30000), firstPaper);

        var secondPaper = Ledger(test).PaymentReceipt(TestData.Owner, second.Value!.PaymentId).Value!.PlainText;

        Assert.Contains(Money.Fixed(40000), secondPaper);
        Assert.Contains(Money.Fixed(30000), secondPaper);
    }

    [Fact]
    public void ASalePaymentIsNotAReceipt()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);

        // A cash bill writes its money into `payments` with a sale_id; that is not a khata receipt.
        MakeBill(test, product, qty: 1);

        var paymentId = FirstSalePaymentId(test);

        Assert.True(paymentId > 0, "the cash bill should have written a payment row");

        var refused = Ledger(test).PaymentReceipt(TestData.Owner, paymentId);

        Assert.False(refused.Ok);
        Assert.Contains("not found", refused.Error);

        Assert.False(Ledger(test).PaymentReceipt(null, paymentId).Ok);
    }

    // ============================== helpers ==============================

    private static void EnableTax(TestDatabase test, string label, int percent)
    {
        Assert.True(test.Settings.Set("tax.enabled", true, TestData.Owner).Ok);
        Assert.True(test.Settings.Set("tax.percent", percent, TestData.Owner).Ok);
        Assert.True(test.Settings.Set("tax.label", label, TestData.Owner).Ok);
        test.Settings.Reload();
    }

    private static SaleResult MakeBill(TestDatabase test, int productId, int qty, int? customer = null)
    {
        var method = customer is null ? "cash" : "credit";

        var sale = TestData.Sales(test).Create(
            TestData.Cart(productId, qty: qty, method: method, customerId: customer), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        return sale.Value!;
    }

    private static SaleResult CreditBill(TestDatabase test, int productId, int customerId, int qty)
    {
        var sale = TestData.Sales(test).Create(
            TestData.Cart(productId, qty: qty, method: "credit", customerId: customerId), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        return sale.Value!;
    }

    /// <summary>Back-dates a bill, so a month boundary can be tested without waiting for a month.</summary>
    private static void MoveSaleTo(TestDatabase test, int saleId, string when) =>
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE sales SET date = @when WHERE id = @id;", new { when, id = saleId }));

    private static int FirstSalePaymentId(TestDatabase test)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<int>(connection,
            "SELECT IFNULL(MIN(id), 0) FROM payments WHERE sale_id IS NOT NULL;");
    }

    private static CustomerLedgerService Ledger(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static TaxService Tax(TestDatabase test) =>
        new(test.Database, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);
}
