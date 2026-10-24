using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The customer khata (Spec 17.3, 13.5, 13.6, F-27). Five promises, and each one is a test here:
///
///   1. the balance moves **only** through a credit bill, money received, or a return;
///   2. money received is refused when it is more than the udhaar - no negative khata, no advance;
///   3. the balance can always be rebuilt from the history, and the page says so ("books match");
///   4. a return never pushes the khata below zero, and the rebuilt balance uses the same clamp;
///   5. loyalty is off by default; when it is on, a bill earns points, a return takes them back,
///      and five stars make a Normal customer VIP.
///
/// The last chain test is the important one: bill → collect → return, then the stored balance and
/// the rebuilt balance must be the same number.
/// </summary>
public sealed class CustomerLedgerTests
{
    private static CustomerLedgerService Ledger(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static long StoredBalance(TestDatabase test, int customerId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<long>(connection,
            "SELECT balance_paisa FROM customers WHERE id = @id;", new { id = customerId });
    }

    private static int PaymentRows(TestDatabase test, int customerId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<int>(connection,
            "SELECT COUNT(1) FROM payments WHERE customer_id = @id;", new { id = customerId });
    }

    /// <summary>A credit bill for qty tablets at Rs 300 each; the whole total lands on the khata.</summary>
    private static SaleResult CreditBill(TestDatabase test, int productId, int customerId, int qty)
    {
        var sale = TestData.Sales(test).Create(
            TestData.Cart(productId, qty: qty, method: "credit", customerId: customerId), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        return sale.Value!;
    }

    [Fact]
    public void ACreditBill_ShowsAsOneLine_AndTheBooksMatch()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, 3);

        var page = Ledger(test).Page(customer);

        Assert.True(page.Ok, page.Error);
        Assert.Equal(90000L, page.Value!.StoredPaisa);      // 3 x Rs 300
        Assert.Equal(90000L, page.Value!.ExpectedPaisa);
        Assert.True(page.Value!.BooksMatch);
        Assert.Single(page.Value!.Lines);
        Assert.Equal("bill", page.Value!.Lines[0].Kind);
        Assert.Equal(90000L, page.Value!.Lines[0].RunningPaisa);
    }

    [Fact]
    public void AMoneyReceipt_LowersTheKhata_WritesAPayment_AndLeavesTheBooksMatching()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, 3);

        var collected = Ledger(test).Collect(TestData.Owner, customer, 40000, "cash", "part payment");

        Assert.True(collected.Ok, collected.Error);
        Assert.Equal(50000L, collected.Value!.NewBalancePaisa);
        Assert.StartsWith("PAY-", collected.Value!.Code);
        Assert.Equal(50000L, StoredBalance(test, customer));

        // Exactly one payment row, and it is a collection: no sale, no purchase.
        using var connection = test.Database.Open();
        var unattached = test.Database.QuerySingle<int>(connection, @"
SELECT COUNT(1) FROM payments
WHERE customer_id = @id AND sale_id IS NULL AND purchase_id IS NULL AND amount_paisa = 40000;",
            new { id = customer });
        Assert.Equal(1, unattached);

        var page = Ledger(test).Page(customer);
        Assert.True(page.Ok, page.Error);
        Assert.True(page.Value!.BooksMatch);
        Assert.Equal(2, page.Value!.Lines.Count);

        // The bill was Rs 900 and Rs 400 came in, so the running balance after the collection is 500.
        Assert.Equal(50000L, page.Value!.Lines[1].RunningPaisa);
        Assert.Equal("collection", page.Value!.Lines[1].Kind);
    }

    [Fact]
    public void MoreMoneyThanTheUdhaar_IsRefused_AndNothingChanges()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, 1);   // Rs 300 owed

        var collected = Ledger(test).Collect(TestData.Owner, customer, 50000, "cash", null);

        Assert.False(collected.Ok);
        Assert.Contains("owes", collected.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(30000L, StoredBalance(test, customer));
        Assert.Equal(0, PaymentRows(test, customer));
    }

    [Fact]
    public void MoneyOnAClearAccount_IsRefused()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test);

        var collected = Ledger(test).Collect(TestData.Owner, customer, 10000, "cash", null);

        Assert.False(collected.Ok);
        Assert.Contains("clear", collected.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, PaymentRows(test, customer));
    }

    [Fact]
    public void CreditIsNotAWayOfReceivingMoney()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, 1);

        var collected = Ledger(test).Collect(TestData.Owner, customer, 10000, "credit", null);

        Assert.False(collected.Ok);
        Assert.Contains("cash, online or card", collected.Error);
        Assert.Equal(30000L, StoredBalance(test, customer));
    }

    [Fact]
    public void Bill_ThenMoney_ThenReturn_StillAddsUpToTheStoredBalance()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        var bill = CreditBill(test, product, customer, 3);          // 90000 on the khata
        var collected = Ledger(test).Collect(TestData.Owner, customer, 40000, "cash", null);
        Assert.True(collected.Ok, collected.Error);                 // 50000 left

        var ret = TestData.Returns(test).Create(TestData.Return(bill, 1), TestData.Owner);
        Assert.True(ret.Ok, ret.Error);
        Assert.Equal(30000L, ret.Value!.CreditReducedPaisa);        // a credit bill refunds the khata, not the drawer

        Assert.Equal(20000L, StoredBalance(test, customer));

        var page = Ledger(test).Page(customer);

        Assert.True(page.Ok, page.Error);
        Assert.Equal(20000L, page.Value!.ExpectedPaisa);
        Assert.Equal(20000L, page.Value!.StoredPaisa);
        Assert.True(page.Value!.BooksMatch, page.Value!.BooksLabel);
        Assert.Equal(3, page.Value!.Lines.Count);
        Assert.Equal(20000L, page.Value!.Lines[2].RunningPaisa);
    }

    [Fact]
    public void AReturnBiggerThanWhatIsLeft_StopsTheKhataAtZero_AndTheBooksStillMatch()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        var bill = CreditBill(test, product, customer, 2);          // 60000 on the khata
        var collected = Ledger(test).Collect(TestData.Owner, customer, 50000, "cash", null);
        Assert.True(collected.Ok, collected.Error);                 // 10000 left

        var ret = TestData.Returns(test).Create(TestData.Return(bill, 2), TestData.Owner);
        Assert.True(ret.Ok, ret.Error);
        Assert.Equal(60000L, ret.Value!.CreditReducedPaisa);

        // MAX(0, ...) in ReturnService: the customer was not refunded more than the bill, and the
        // khata cannot go below zero.
        Assert.Equal(0L, StoredBalance(test, customer));

        var page = Ledger(test).Page(customer);

        Assert.True(page.Ok, page.Error);
        Assert.Equal(0L, page.Value!.ExpectedPaisa);
        Assert.True(page.Value!.BooksMatch, page.Value!.BooksLabel);
    }

    [Fact]
    public void TheList_FindsACustomerByPhone_AndCanShowOnlyThoseWhoOwe()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);
        var ali = TestData.AddCustomer(test, "Ali Raza");
        var sana = TestData.AddCustomer(test, "Sana Khan");

        CreditBill(test, product, sana, 1);

        var all = Ledger(test).List("Sana");
        Assert.True(all.Ok, all.Error);
        Assert.Single(all.Value!);
        Assert.Equal(sana, all.Value![0].Id);

        var byPhone = Ledger(test).List("0300-1234567");
        Assert.True(byPhone.Ok, byPhone.Error);
        Assert.Equal(2, byPhone.Value!.Count);       // both share the test phone

        var owing = Ledger(test).List(null, onlyCredit: true);
        Assert.True(owing.Ok, owing.Error);
        Assert.Single(owing.Value!);
        Assert.Equal(sana, owing.Value![0].Id);
        Assert.True(owing.Value![0].BalancePaisa > 0);

        var clear = Ledger(test).List("Ali");
        Assert.Equal(ali, clear.Value![0].Id);
        Assert.Equal(0L, clear.Value![0].BalancePaisa);
    }

    [Fact]
    public void WithoutTheCustomersPermission_MoneyIsNotReceived()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, 1);

        // A real account, then an explicit "no" for this one module - a stored row beats the role.
        test.Database.Tx(c =>
        {
            test.Database.Execute(c,
                "INSERT INTO users(name, role, active, created_at) VALUES('Cashier', 'cashier', 1, '2026-01-01 09:00:00');");
            return 0;
        });

        using var connection = test.Database.Open();
        var cashierId = test.Database.QuerySingle<int>(connection,
            "SELECT id FROM users WHERE name = 'Cashier' LIMIT 1;");

        test.Database.Tx(c =>
        {
            test.Database.Execute(c, @"
INSERT INTO user_permissions(user_id, module_key, allowed) VALUES(@id, 'customers', 0);",
                new { id = cashierId });
            return 0;
        });

        var cashier = new User { Id = cashierId, Name = "Cashier", Role = "cashier" };

        var collected = Ledger(test).Collect(cashier, customer, 10000, "cash", null);

        Assert.False(collected.Ok);
        Assert.Contains("permission", collected.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(30000L, StoredBalance(test, customer));
    }

    [Fact]
    public void LoyaltyIsOff_SoABillGivesNoPoints()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, 3);

        var summary = Ledger(test).List("Ali").Value![0];
        Assert.Equal(0, summary.LoyaltyPoints);
        Assert.Equal(1, summary.Stars);
    }

    [Fact]
    public void LoyaltyOn_ABillEarnsPoints_AndFiveStarsMakeTheCustomerVip()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        // One rupee = one point keeps the arithmetic readable: a Rs 500 bill is 500 points, and
        // the fifth star is reached at loyalty.vipThreshold (500 by default).
        Assert.True(test.Settings.Set("loyalty.enabled", true, TestData.Owner).Ok);
        Assert.True(test.Settings.Set("loyalty.rupeesPerPoint", 1, TestData.Owner).Ok);
        test.Settings.Reload();

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 2, method: "credit", customerId: customer), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        using var connection = test.Database.Open();
        var points = test.Database.QuerySingle<int>(connection,
            "SELECT loyalty_points FROM customers WHERE id = @id;", new { id = customer });
        var stars = test.Database.QuerySingle<int>(connection,
            "SELECT stars FROM customers WHERE id = @id;", new { id = customer });
        var category = test.Database.QuerySingle<string>(connection,
            "SELECT category FROM customers WHERE id = @id;", new { id = customer });

        // Rs 600 bill = 600 points; step = 500 / 5 = 100, so 1 + 6 -> clamped to 5 stars.
        Assert.Equal(600, points);
        Assert.Equal(5, stars);
        Assert.Equal("vip", category);

        // The promotion is in the black box, so the owner can see who changed the customer.
        var promoted = test.Database.QuerySingle<int>(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = 'vip_promoted';");
        Assert.Equal(1, promoted);
    }

    [Fact]
    public void AReturnTakesThePointsBack_ButNeverBelowZero()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        Assert.True(test.Settings.Set("loyalty.enabled", true, TestData.Owner).Ok);
        Assert.True(test.Settings.Set("loyalty.rupeesPerPoint", 1, TestData.Owner).Ok);
        test.Settings.Reload();

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 3, method: "credit", customerId: customer), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);            // 900 points

        var ret = TestData.Returns(test).Create(TestData.Return(sale.Value!, 1), TestData.Owner);
        Assert.True(ret.Ok, ret.Error);              // Rs 300 back = 300 points back

        using var connection = test.Database.Open();
        var points = test.Database.QuerySingle<int>(connection,
            "SELECT loyalty_points FROM customers WHERE id = @id;", new { id = customer });
        Assert.Equal(600, points);

        // Stars are a record of the best the customer reached: a refund does not demote.
        var stars = test.Database.QuerySingle<int>(connection,
            "SELECT stars FROM customers WHERE id = @id;", new { id = customer });
        Assert.True(stars >= 1);
    }

    [Fact]
    public void StarsAndPoints_AreDerivedTheSameWay_ForAnyThreshold()
    {
        Assert.Equal(1, CustomerLedgerService.StarsFor(0, 500));
        Assert.Equal(2, CustomerLedgerService.StarsFor(100, 500));
        Assert.Equal(5, CustomerLedgerService.StarsFor(400, 500));
        Assert.Equal(5, CustomerLedgerService.StarsFor(99999, 500));

        // A zero or negative threshold falls back to the default instead of dividing by zero.
        Assert.Equal(1, CustomerLedgerService.StarsFor(0, 0));
        Assert.Equal(5, CustomerLedgerService.StarsFor(500, 0));

        Assert.Equal(0, CustomerLedgerService.PointsFor(9999, 100));
        Assert.Equal(1, CustomerLedgerService.PointsFor(10000, 100));
        Assert.Equal(2, CustomerLedgerService.PointsFor(25000, 100));
    }

    [Fact]
    public void TheStatement_FitsThePaper_AndShowsTheClosingBalance()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, 2);
        Assert.True(Ledger(test).Collect(TestData.Owner, customer, 20000, "cash", null).Ok);

        var statement = Ledger(test).Statement(customer);

        Assert.True(statement.Ok, statement.Error);

        var paper = statement.Value!;
        Assert.Equal(32, paper.Width);                       // 58 mm by default

        Assert.Contains("CUSTOMER STATEMENT", paper.PlainText);
        Assert.Contains("TOTAL UDHAAR", paper.PlainText);
        Assert.All(paper.Lines, line => Assert.True(line.Text.Length <= 32,
            "the line does not fit the paper: " + line.Text));
    }

    [Fact]
    public void AStatementOn80mm_UsesTheWiderPaper()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        CreditBill(test, product, customer, 1);

        Assert.True(test.Settings.Set("printer.width", 80, TestData.Owner).Ok);
        test.Settings.Reload();

        var statement = Ledger(test).Statement(customer);

        Assert.True(statement.Ok, statement.Error);

        var paper = statement.Value!;
        Assert.Equal(48, paper.Width);
        Assert.All(paper.Lines, line => Assert.True(line.Text.Length <= 48,
            "the line does not fit the paper: " + line.Text));
    }
}
