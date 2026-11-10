using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Old udhaar and its recovery (F-33). The four promises:
///   • a bill's age is the bill's own date - a payment never makes old money young;
///   • money knocks the **oldest** bill off first, the way a shopkeeper's notebook works;
///   • the four age columns always add up to the stored khata (or the row is flagged, never hidden);
///   • the slip is rebuilt from the history, so a reprint says exactly what the customer took home.
/// </summary>
public sealed class RecoveryTests
{
    private static readonly DateTime Today = new(2026, 10, 21);

    private static void AgeBill(TestDatabase test, int saleId, int days)
    {
        var stamp = Today.AddDays(-days).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        using var connection = test.Database.Open();
        test.Database.Execute(connection, "UPDATE sales SET date = @at WHERE id = @id;",
            new { at = stamp, id = saleId });
    }

    private static void AgePayment(TestDatabase test, int paymentId, int days)
    {
        var stamp = Today.AddDays(-days).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        using var connection = test.Database.Open();
        test.Database.Execute(connection, "UPDATE payments SET date = @at WHERE id = @id;",
            new { at = stamp, id = paymentId });
    }

    /// <summary>One credit bill of the given rupee amount, aged by `days`.</summary>
    private static int CreditBill(TestDatabase test, int productId, int customerId, int units, int days)
    {
        var sale = TestData.Sales(test).Create(TestData.Cart(productId, qty: units, method: "credit", customerId: customerId),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);

        AgeBill(test, sale.Value!.SaleId, days);

        return sale.Value.SaleId;
    }

    private static RecoveryReport Report(TestDatabase test) =>
        TestData.Recovery(test).Report(TestData.Owner, Today).Value!;

    // ------------------------------------------------------------------ ageing

    [Fact]
    public void ACreditBill_IsTodayMoney_UntilTheDayMoves()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 1, days: 0);

        var row = Assert.Single(Report(test).Rows);

        Assert.Equal(30000L, row.BalancePaisa);
        Assert.Equal(0, row.OldestDays);
        Assert.Equal(30000L, row.Bucket0Paisa);
        Assert.Equal(0L, row.Bucket1Paisa);
        Assert.True(row.BooksMatch);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(30, 0)]
    [InlineData(31, 1)]
    [InlineData(60, 1)]
    [InlineData(61, 2)]
    [InlineData(90, 2)]
    [InlineData(91, 3)]
    [InlineData(365, 3)]
    public void AnOldBill_LandsInTheRightColumn(int daysOld, int expectedBucket)
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 1, days: daysOld);

        var row = Assert.Single(Report(test).Rows);
        var column = expectedBucket switch
        {
            0 => row.Bucket0Paisa,
            1 => row.Bucket1Paisa,
            2 => row.Bucket2Paisa,
            _ => row.Bucket3Paisa
        };

        Assert.Equal(30000L, column);
        Assert.Equal(daysOld, row.OldestDays);
        Assert.Equal(30000L, row.BucketsTotalPaisa);
        Assert.True(row.BooksMatch);
    }

    [Fact]
    public void APayment_KnocksTheOldestBillOffFirst()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 10, days: 100);   // Rs 3,000 - very old
        CreditBill(test, product, customer, units: 4, days: 10);     // Rs 1,200 - fresh

        var paid = TestData.Ledger(test).Collect(TestData.Owner, customer, 300000, "cash", "clearing the old one");
        Assert.True(paid.Ok, paid.Error);

        var row = Assert.Single(Report(test).Rows);

        // The old money is gone and the fresh bill is untouched: Rs 1,200 still owed, all of it 10 days old.
        Assert.Equal(120000L, row.BalancePaisa);
        Assert.Equal(0L, row.Bucket3Paisa);
        Assert.Equal(120000L, row.Bucket0Paisa);
        Assert.Equal(10, row.OldestDays);
        Assert.True(row.BooksMatch);
    }

    [Fact]
    public void APartPayment_LeavesTheRestInItsOldColumn()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 10, days: 120);   // Rs 3,000

        var paid = TestData.Ledger(test).Collect(TestData.Owner, customer, 100000, "cash", "part");
        Assert.True(paid.Ok, paid.Error);

        var row = Assert.Single(Report(test).Rows);

        Assert.Equal(200000L, row.BalancePaisa);
        Assert.Equal(200000L, row.Bucket3Paisa);                     // still the same bill, same age
        Assert.Equal(120, row.OldestDays);
        Assert.True(row.BooksMatch);
    }

    [Fact]
    public void AReturnThatReducedTheKhata_CountsAsMoneyReceived()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        var saleId = CreditBill(test, product, customer, units: 4, days: 100);   // Rs 1,200

        var returned = TestData.Returns(test).Create(new ReturnInput
        {
            SaleId = saleId,
            Reason = "changed mind",
            Restock = true,
            Lines = new List<ReturnLineInput>
            {
                new() { SaleItemId = FirstItemId(test, saleId), QtyBase = 1 }
            }
        }, TestData.Owner);

        Assert.True(returned.Ok, returned.Error);

        var row = Assert.Single(Report(test).Rows);

        // One tablet returned at Rs 300: the khata fell by exactly that, and the rest is still old.
        Assert.Equal(90000L, row.BalancePaisa);
        Assert.Equal(90000L, row.Bucket3Paisa);
        Assert.True(row.BooksMatch);
    }

    private static int FirstItemId(TestDatabase test, int saleId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<int>(connection,
            "SELECT id FROM sale_items WHERE sale_id = @id ORDER BY id LIMIT 1;", new { id = saleId });
    }

    [Fact]
    public void TheOldestCustomer_IsListedFirst()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);

        var fresh = TestData.AddCustomer(test, name: "Fresh", phone: "0300-1");
        var old = TestData.AddCustomer(test, name: "Old", phone: "0300-2");

        CreditBill(test, product, fresh, units: 10, days: 5);      // more money, newer
        CreditBill(test, product, old, units: 2, days: 200);       // less money, much older

        var rows = Report(test).Rows;

        Assert.Equal(2, rows.Count);
        Assert.Equal("Old", rows[0].Name);
        Assert.Equal("Fresh", rows[1].Name);
    }

    [Fact]
    public void ACustomerWhoOwesNothing_IsNotOnTheList()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        var cash = TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash", customerId: customer),
            TestData.Owner);

        Assert.True(cash.Ok, cash.Error);

        var report = Report(test);

        Assert.Empty(report.Rows);
        Assert.Equal("0 customers owe money", report.CountText);
        Assert.True(report.BooksMatch);
    }

    [Fact]
    public void TheColumnsAndTheWholeShop_AlwaysAddUpToTheKhata()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 500);

        var first = TestData.AddCustomer(test, name: "First", phone: "0300-1");
        var second = TestData.AddCustomer(test, name: "Second", phone: "0300-2");

        CreditBill(test, product, first, units: 3, days: 95);
        CreditBill(test, product, first, units: 2, days: 40);
        CreditBill(test, product, second, units: 7, days: 12);

        Assert.True(TestData.Ledger(test).Collect(TestData.Owner, first, 50000, "cash", "part").Ok);
        Assert.True(TestData.Ledger(test).Collect(TestData.Owner, second, 10000, "online", "part").Ok);

        var report = Report(test);

        Assert.True(report.BooksMatch);
        Assert.Equal(report.TotalPaisa, report.BucketsTotalPaisa);
        Assert.Equal(report.Rows.Sum(row => row.BalancePaisa), report.Bucket0Paisa + report.Bucket1Paisa
            + report.Bucket2Paisa + report.Bucket3Paisa);
    }

    [Fact]
    public void OverdueMeansOlderThanTwoMonths()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 1, days: 29);    // Rs 300, fresh
        CreditBill(test, product, customer, units: 2, days: 75);    // Rs 600, overdue

        var row = Assert.Single(Report(test).Rows);

        Assert.Equal(60000L, row.OverduePaisa);
        Assert.Equal(90000L, row.BalancePaisa);
        Assert.Equal("late", row.AgeTone);
    }

    // ------------------------------------------------------------------ the slip

    [Fact]
    public void TheSlip_ShowsBeforePaidAndLeft_AndWhichBillsWereCleared()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 5, days: 120);   // Rs 1,500, old
        CreditBill(test, product, customer, units: 4, days: 20);    // Rs 1,200, fresh

        var paid = TestData.Ledger(test).Collect(TestData.Owner, customer, 150000, "cash", "old bill");
        Assert.True(paid.Ok, paid.Error);

        var slip = TestData.Recovery(test).Slip(paid.Value!.PaymentId, TestData.Owner);

        Assert.True(slip.Ok, slip.Error);

        var data = slip.Value!;

        Assert.Equal("PAY-" + paid.Value.PaymentId.ToString("D4", CultureInfo.InvariantCulture), data.Code);
        Assert.Equal(270000L, data.BalanceBeforePaisa);             // 1,500 + 1,200
        Assert.Equal(150000L, data.ReceivedPaisa);
        Assert.Equal(120000L, data.BalanceAfterPaisa);
        Assert.False(data.Cleared);

        // The money went to the OLD bill and nowhere else, and the paper says so.
        var hit = Assert.Single(data.Hits);
        Assert.Equal(150000L, hit.PaidPaisa);
        Assert.Equal(0L, hit.LeftPaisa);
        Assert.Contains("0 left", hit.Label);
    }

    [Fact]
    public void TheSlip_IsRebuiltFromHistory_SoAReprintNeverChanges()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 10, days: 100);  // Rs 3,000

        var first = TestData.Ledger(test).Collect(TestData.Owner, customer, 100000, "cash", "part one");
        Assert.True(first.Ok, first.Error);

        var before = TestData.Recovery(test).Slip(first.Value!.PaymentId, TestData.Owner).Value!;

        // A second payment happens later; the first slip must still describe the first moment.
        var second = TestData.Ledger(test).Collect(TestData.Owner, customer, 50000, "cash", "part two");
        Assert.True(second.Ok, second.Error);

        var again = TestData.Recovery(test).Slip(first.Value.PaymentId, TestData.Owner).Value!;

        Assert.Equal(before.BalanceBeforePaisa, again.BalanceBeforePaisa);
        Assert.Equal(before.BalanceAfterPaisa, again.BalanceAfterPaisa);
        Assert.Equal(200000L, again.BalanceAfterPaisa);
        Assert.Equal(before.Hits.Count, again.Hits.Count);
        Assert.Equal(before.Hits[0].LeftPaisa, again.Hits[0].LeftPaisa);
    }

    [Fact]
    public void TheSlip_IsPaperForTheShopPrinter_FiftyEightOrEighty()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 2, days: 10);

        var paid = TestData.Ledger(test).Collect(TestData.Owner, customer, 60000, "cash", "all of it");
        Assert.True(paid.Ok, paid.Error);

        var narrow = TestData.Recovery(test).SlipDocument(paid.Value!.PaymentId, TestData.Owner);
        Assert.True(narrow.Ok, narrow.Error);
        Assert.Equal(32, narrow.Value!.Width);

        var text = narrow.Value.PlainText;

        Assert.Contains("UDHAAR RECOVERY", text);
        Assert.Contains("PAY-" + paid.Value.PaymentId.ToString("D4", CultureInfo.InvariantCulture),
            narrow.Value.Title);
        Assert.Contains("Udhaar before", text);
        Assert.Contains("Udhaar after", text);
        Assert.Contains("KHATA CLEAR", text);          // he paid the whole 600

        // 80 mm paper uses 48 characters, and every line must obey it.
        Assert.True(test.Settings.Set("printer.width", 80, TestData.Owner).Ok);

        var wide = TestData.Recovery(test).SlipDocument(paid.Value.PaymentId, TestData.Owner);

        Assert.True(wide.Ok, wide.Error);
        Assert.Equal(48, wide.Value!.Width);
        Assert.All(wide.Value.Lines, line => Assert.True(line.Text.Length <= 48, line.Text));
    }

    [Fact]
    public void TheListNeedsTheCustomersPermission_AndAStoredNoWins()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 1, days: 5);

        var cashierId = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "INSERT INTO users(name, role, active, created_at) VALUES('Cashier', 'cashier', 1, @at);",
                new { at = Clock.Now() });

            return (int)test.Database.NextId(connection);
        });

        var cashier = new User { Id = cashierId, Name = "Cashier", Role = "cashier" };

        // The counter must be able to collect: a cashier has Customers by role default.
        Assert.True(TestData.Recovery(test).Report(cashier, Today).Ok);

        // A stored "no" always beats the role default - that is the whole promise of the screen.
        var permissions = new PermissionService(test.Database, test.Log, test.Audit);
        Assert.True(permissions.Set(TestData.Owner, cashierId, Modules.Customers, false).Ok);

        var refused = TestData.Recovery(test).Report(cashier, Today);

        Assert.False(refused.Ok);
        Assert.Contains("permission", refused.Error!, StringComparison.OrdinalIgnoreCase);

        // The owner can never lock himself out, and the manager has Customers by default.
        Assert.True(TestData.Recovery(test).Report(TestData.Owner, Today).Ok);
        Assert.True(TestData.Recovery(test).Report(new User { Id = 9, Name = "Manager", Role = "manager" }, Today).Ok);
    }

    [Fact]
    public void APaymentForASale_IsNotCountedTwice()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        // A cash sale leaves a payment row against the sale - it is already inside the bill, so the
        // ageing must never subtract it a second time.
        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash", customerId: customer),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);

        CreditBill(test, product, customer, units: 2, days: 30);

        var row = Assert.Single(Report(test).Rows);

        Assert.Equal(60000L, row.BalancePaisa);
        Assert.True(row.BooksMatch);
    }

    [Fact]
    public void TheSlipOfAMissingPayment_IsAnError_NotAnEmptyPaper()
    {
        using var test = new TestDatabase();

        var slip = TestData.Recovery(test).Slip(9999, TestData.Owner);

        Assert.False(slip.Ok);
        Assert.False(string.IsNullOrWhiteSpace(slip.Error));
    }

    [Fact]
    public void AReturnReducesItsOwnBill_NotWhateverBillHappensToBeOldest()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 1, days: 100);  // old bill stays owed
        var newer = CreditBill(test, product, customer, units: 1, days: 10);

        var returned = TestData.Returns(test).Create(new ReturnInput
        {
            SaleId = newer,
            Restock = true,
            Lines = new List<ReturnLineInput>
            {
                new() { SaleItemId = FirstItemId(test, newer), QtyBase = 1 }
            }
        }, TestData.Owner);
        Assert.True(returned.Ok, returned.Error);
        Assert.Equal(30000L, returned.Value!.CreditReducedPaisa);

        var row = Assert.Single(Report(test).Rows);
        Assert.Equal(30000L, row.BalancePaisa);
        Assert.Equal(30000L, row.Bucket3Paisa);
        Assert.Equal(0L, row.Bucket0Paisa);
        Assert.Equal(100, row.OldestDays);                        // the new return did not clear old debt
        Assert.True(row.BooksMatch);
    }

    [Fact]
    public void ARecoverySlip_DoesNotAbsorbABillCreatedAfterThatPayment()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, units: 3, days: 100); // Rs 900
        var paid = TestData.Ledger(test).Collect(TestData.Owner, customer, 30000, "cash", "part");
        Assert.True(paid.Ok, paid.Error);
        AgePayment(test, paid.Value!.PaymentId, 50);

        var before = TestData.Recovery(test).Slip(paid.Value.PaymentId, TestData.Owner).Value!;
        Assert.Equal(90000L, before.BalanceBeforePaisa);
        Assert.Equal(60000L, before.BalanceAfterPaisa);

        CreditBill(test, product, customer, units: 2, days: 0);   // a later Rs 600 bill

        var reprint = TestData.Recovery(test).Slip(paid.Value.PaymentId, TestData.Owner).Value!;
        Assert.Equal(before.BalanceBeforePaisa, reprint.BalanceBeforePaisa);
        Assert.Equal(before.BalanceAfterPaisa, reprint.BalanceAfterPaisa);
        var hit = Assert.Single(reprint.Hits);
        Assert.Equal(30000L, hit.PaidPaisa);
        Assert.Equal(60000L, hit.LeftPaisa);
    }

    [Fact]
    public void ACollectionBeyondTheBills_ReducesCarriedInKhataAndItsSlipExplainsWhereItWent()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test);

        using (var connection = test.Database.Open())
        {
            test.Database.Execute(connection,
                "UPDATE customers SET opening_balance_paisa = 20000, balance_paisa = 20000 WHERE id = @id;",
                new { id = customer });
        }

        CreditBill(test, product, customer, units: 1, days: 20); // Rs 300 + Rs 200 carried in
        var paid = TestData.Ledger(test).Collect(TestData.Owner, customer, 40000, "cash", "bills and opening");
        Assert.True(paid.Ok, paid.Error);

        var row = Assert.Single(Report(test).Rows);
        Assert.Equal(10000L, row.BalancePaisa);
        Assert.Equal(10000L, row.OpeningPaisa);
        Assert.Equal(0L, row.BucketsTotalPaisa);
        Assert.True(row.BooksMatch);

        var slip = TestData.Recovery(test).Slip(paid.Value!.PaymentId, TestData.Owner);
        Assert.True(slip.Ok, slip.Error);
        Assert.Equal(50000L, slip.Value!.BalanceBeforePaisa);
        Assert.Equal(10000L, slip.Value.BalanceAfterPaisa);
        Assert.Equal(2, slip.Value.Hits.Count);
        Assert.Equal(10000L, slip.Value.Hits[1].PaidPaisa);
        Assert.True(slip.Value.Hits[1].IsOpening);
        Assert.Contains("Carried-in udhaar", slip.Value.Hits[1].Label);
    }
}
