using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The home screen (F-12, D-75).
///
/// Why the numbers are checked one at a time and against a hand-built expectation: the owner's
/// acceptance for this page is "it matches a manual count". A dashboard that is off by one is worse
/// than no dashboard - it teaches him to stop trusting the page and then to stop trusting the
/// reports that share its rules. So every card here is compared with rows this test made itself,
/// not with another query that could carry the same mistake.
/// </summary>
public sealed class DashboardTests
{
    private static DashboardService Dashboard(TestDatabase test) =>
        new(test.Database, test.Settings, TestData.Permissions(test), test.Log);

    private static DashboardSnapshot Snapshot(TestDatabase test, User? user = null)
    {
        var result = Dashboard(test).GetSnapshot(user ?? TestData.Owner);

        Assert.True(result.Ok, result.Error);

        return result.Value!;
    }

    /// <summary>Changes one medicine's minimum, the way the Products page does.</summary>
    private static void SetLevel(TestDatabase test, int productId, double level)
    {
        using var connection = test.Database.Open();

        test.Database.Execute(connection,
            "UPDATE products SET reorder_level = @level WHERE id = @id;",
            new { level, id = productId });
    }

    /// <summary>Puts stock on a shelf without a purchase: enough for the stock cards.</summary>
    private static void SetStock(TestDatabase test, int productId, double qty)
    {
        using var connection = test.Database.Open();

        test.Database.Execute(connection,
            "UPDATE batches SET qty_base = @qty WHERE product_id = @id;",
            new { qty, id = productId });
    }

    /// <summary>Moves a batch's expiry date, the way a real delivery would carry it.</summary>
    private static void SetExpiry(TestDatabase test, int productId, string date)
    {
        using var connection = test.Database.Open();

        test.Database.Execute(connection,
            "UPDATE batches SET expiry_date = @date, expiry_ym = SUBSTR(@date, 1, 7) WHERE product_id = @id;",
            new { date, id = productId });
    }

    private static string InDays(int days) =>
        DateTime.Today.AddDays(days).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // ============================== one bill, counted by hand ==============================

    [Fact]
    public void OneBill_ShowsAsOneBillAndItsOwnTotal()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);

        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 3, method: "cash"), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var snapshot = Snapshot(test);

        Assert.Equal(1, snapshot.TodayBills);
        Assert.Equal(sale.Value!.TotalPaisa, snapshot.TodaySalesPaisa);

        // The shop's own round-down rule: three tablets at Rs 300 is Rs 900, and that is what the
        // drawer holds - the dashboard quotes the bill, not a second arithmetic.
        Assert.Equal(90000L, snapshot.TodaySalesPaisa);
    }

    [Fact]
    public void ThreeBillsOfMixedMethods_AreAllCounted()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);

        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash"), TestData.Owner).Ok);
        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 2, method: "card"), TestData.Owner).Ok);
        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "online"), TestData.Owner).Ok);

        var snapshot = Snapshot(test);

        // A card bill is a bill. A dashboard that only counted the drawer would report one.
        Assert.Equal(3, snapshot.TodayBills);
        Assert.Equal(4 * 30000L, snapshot.TodaySalesPaisa);
    }

    [Fact]
    public void AHeldCartAndAVoidBillAreNotSales()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1), TestData.Owner).Ok);

        // A void bill is a row the shop keeps for the record but never charged; a held cart is a
        // shopping list. Neither may inflate today's takings.
        using (var connection = test.Database.Open())
        {
            test.Database.Execute(connection, @"
INSERT INTO sales(code, date, user_id, subtotal_paisa, discount_paisa, tax_paisa, round_off_paisa,
                  total_paisa, paid_paisa, due_paisa, status, payment_method, created_at)
VALUES('INV-9001', @at, @userId, 500000, 0, 0, 0, 500000, 0, 0, 'void', 'cash', @at);",
                new { at = Clock.Now(), userId = TestData.Owner.Id });

            test.Database.Execute(connection, @"
INSERT INTO sales(code, date, user_id, subtotal_paisa, discount_paisa, tax_paisa, round_off_paisa,
                  total_paisa, paid_paisa, due_paisa, status, payment_method, created_at)
VALUES('INV-9002', @at, @userId, 400000, 0, 0, 0, 400000, 0, 0, 'hold', 'cash', @at);",
                new { at = Clock.Now(), userId = TestData.Owner.Id });
        }

        var snapshot = Snapshot(test);

        Assert.Equal(1, snapshot.TodayBills);
        Assert.Equal(30000L, snapshot.TodaySalesPaisa);
    }

    [Fact]
    public void AbillFromYesterday_DoesNotCountToday()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1), TestData.Owner).Ok);

        using (var connection = test.Database.Open())
        {
            test.Database.Execute(connection,
                "UPDATE sales SET date = @date, created_at = @date WHERE code = (SELECT code FROM sales LIMIT 1);",
                new { date = Clock.DateDaysAgo(1) + " 11:00:00" });
        }

        var snapshot = Snapshot(test);

        Assert.Equal(0, snapshot.TodayBills);
        Assert.Equal(0L, snapshot.TodaySalesPaisa);
    }

    // ============================== profit, by the shop's own rule ==============================

    [Fact]
    public void TodaysProfit_IsSalesMinusTheCostOfWhatLeftTheShelf()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);

        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 2), TestData.Owner).Ok);

        var snapshot = Snapshot(test);

        // Two tablets at Rs 300 = Rs 600 in; they cost the shop Rs 200 each = Rs 400 out.
        Assert.Equal(60000L, snapshot.TodaySalesPaisa);
        Assert.Equal(20000L, snapshot.TodayProfitPaisa);
        Assert.True(snapshot.CanSeeProfit);
    }

    [Fact]
    public void ADiscountAndARoundDown_TakeTheirShareOfTheProfit()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);

        var cart = TestData.Cart(product, qty: 2);
        cart.DiscountPercent = 10m;

        var sale = TestData.Sales(test).Create(cart, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var snapshot = Snapshot(test);

        // Rs 600 less 10 % = Rs 540; the shop paid Rs 400 for the goods, so the day earned Rs 140 -
        // not Rs 200. A dashboard that ignored the discount would overstate the owner's profit.
        Assert.Equal(54000L, sale.Value!.TotalPaisa);
        Assert.Equal(54000L, snapshot.TodaySalesPaisa);
        Assert.Equal(14000L, snapshot.TodayProfitPaisa);
    }

    [Fact]
    public void AReturnToday_TakesBackBothTheMoneyAndTheProfit()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var sales = TestData.Sales(test);
        var returns = TestData.Returns(test);

        var first = sales.Create(TestData.Cart(product, qty: 3), TestData.Owner);
        Assert.True(first.Ok, first.Error);

        var before = Snapshot(test);
        Assert.Equal(30000L, before.TodayProfitPaisa);

        var returned = returns.Create(TestData.Return(first.Value!, 1, restock: true), TestData.Owner);
        Assert.True(returned.Ok, returned.Error);

        var after = Snapshot(test);

        // One tablet came back: Rs 300 refunded and Rs 200 of cost back on the shelf, so the day's
        // profit drops from Rs 300 to Rs 200 - and the bill itself is never edited.
        Assert.Equal(1, after.TodayBills);
        Assert.Equal(60000L, after.TodaySalesPaisa);
        Assert.Equal(20000L, after.TodayProfitPaisa);
    }

    [Fact]
    public void AStaffMemberWithoutProfitPermission_SeesZeroAndIsToldSo()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1), TestData.Owner).Ok);

        // A manager keeps the report pages but the profit module is the owner's to grant, so the
        // cash value is not even read for them.
        var manager = TestData.AddUser(test, "manager");
        var snapshot = Snapshot(test, manager);

        Assert.False(snapshot.CanSeeProfit);
        Assert.Equal(0L, snapshot.TodayProfitPaisa);

        // The rest of the page still works: a manager may run the shop without seeing its margin.
        Assert.Equal(1, snapshot.TodayBills);
        Assert.Equal(30000L, snapshot.TodaySalesPaisa);
    }

    [Fact]
    public void ACashierSeesTheShopButNeverTheProfit()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var cashier = TestData.Cashier(test);

        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1), cashier).Ok);

        var snapshot = Snapshot(test, cashier);

        // The counter's own person needs the page - today's bills, the shelf, the khata - but the
        // margin is not theirs to read, so the number is not even fetched (D-75).
        Assert.Equal(1, snapshot.TodayBills);
        Assert.Equal(30000L, snapshot.TodaySalesPaisa);
        Assert.False(snapshot.CanSeeProfit);
        Assert.Equal(0L, snapshot.TodayProfitPaisa);
    }

    [Fact]
    public void APersonWithTheCounterAndTheReportsSwitchedOff_IsRefused()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);

        using (var connection = test.Database.Open())
        {
            test.Database.Execute(connection, @"
INSERT INTO user_permissions(user_id, module_key, allowed) VALUES(@id, 'pos', 0);",
                new { id = cashier.Id });
        }

        var refused = Dashboard(test).GetSnapshot(cashier);

        // Nothing on this page is theirs, so the page is not shown at all rather than shown empty.
        Assert.False(refused.Ok);
        Assert.Contains("permission", refused.Error!);
    }

    [Fact]
    public void TheDashboardAndTodaysReport_TellTheSameStory()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var sales = TestData.Sales(test);

        Assert.True(sales.Create(TestData.Cart(product, qty: 2), TestData.Owner).Ok);
        Assert.True(sales.Create(TestData.Cart(product, qty: 1, method: "card"), TestData.Owner).Ok);

        var snapshot = Snapshot(test);

        // Two pages, one set of rules: the home card and the report's "today" row describe the same
        // day, so the owner cannot find one number on one page and another on the next (D-75).
        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner);
        Assert.True(report.Ok, report.Error);

        var totals = report.Value!.Totals;

        Assert.Equal(totals.Bills, snapshot.TodayBills);
        Assert.Equal(totals.SalesPaisa, snapshot.TodaySalesPaisa);
        Assert.Equal(totals.ProfitPaisa, snapshot.TodayProfitPaisa);
    }

    // ============================== the shelves ==============================

    [Fact]
    public void LowStock_IsTheRedHalfOfTheReorderPad()
    {
        using var test = new TestDatabase();

        var watched = TestData.AddProduct(test, name: "Below its minimum", batchQty: 5);
        SetLevel(test, watched, 10);

        // An empty shelf is "out" on the pad even when nobody set a minimum - a customer asking for
        // it gets nothing. The card must not hide that just because the level is 0.
        var empty = TestData.AddProduct(test, name: "Empty, no minimum", batchQty: 0);
        SetLevel(test, empty, 0);

        // Nobody asked to watch this one, and it is full: not on the card.
        TestData.AddProduct(test, name: "Full shelf", batchQty: 500);

        var snapshot = Snapshot(test);

        Assert.Equal(3, snapshot.Products);
        Assert.Equal(2, snapshot.LowStock);
        Assert.Equal(0, snapshot.NegativeStock);
    }

    [Fact]
    public void TheLowStockCard_AndTheReorderPad_NameTheSameMedicines()
    {
        using var test = new TestDatabase();

        var critical = TestData.AddProduct(test, name: "At the minimum", batchQty: 10);
        SetLevel(test, critical, 10);

        var outOfStock = TestData.AddProduct(test, name: "Finished", batchQty: 0);
        SetLevel(test, outOfStock, 5);

        var amber = TestData.AddProduct(test, name: "Getting low", batchQty: 14);
        SetLevel(test, amber, 10);

        TestData.AddProduct(test, name: "Comfortable", batchQty: 200);

        var snapshot = Snapshot(test);

        var pad = new ReorderService(test.Database, test.Audit, test.Settings,
            TestData.Permissions(test), test.Log).Report(TestData.Owner);
        Assert.True(pad.Ok, pad.Error);

        // The pad's red rows (Out + Critical) are exactly the home screen's number. The amber row
        // ("Getting low", 14 against a minimum of 10) is a warning to the owner on the pad but not
        // yet a "running out" card - and this assertion is what keeps the two pages honest (D-75).
        var red = pad.Value!.OutCount + pad.Value!.CriticalCount;

        Assert.Equal(2, red);
        Assert.Equal(2, snapshot.LowStock);
    }

    [Fact]
    public void AShelfBelowZero_IsCountedSeparatelyFromRunningOut()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: -4);
        SetLevel(test, product, 0);

        var snapshot = Snapshot(test);

        // Minus stock is a fault to fix, not a reorder: the cards are different on purpose. Even with
        // no minimum set, a minus shelf is still a minus shelf.
        Assert.Equal(1, snapshot.NegativeStock);
        Assert.Equal(0, snapshot.LowStock);
    }

    [Fact]
    public void ExpiringSoonAndExpired_AreTwoDifferentNumbers()
    {
        using var test = new TestDatabase();

        var soon = TestData.AddProduct(test, name: "Soon");
        SetExpiry(test, soon, InDays(20));

        var expired = TestData.AddProduct(test, name: "Expired");
        SetExpiry(test, expired, InDays(-5));

        var fine = TestData.AddProduct(test, name: "Fine");
        SetExpiry(test, fine, InDays(400));

        var snapshot = Snapshot(test);

        // 20 days is inside the default nearest band (90); 400 days is a healthy shelf.
        Assert.Equal(1, snapshot.ExpiringSoon);
        Assert.Equal(1, snapshot.Expired);
    }

    [Fact]
    public void AnEmptyBatch_IsNotCountedAsExpiring()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        SetExpiry(test, product, InDays(10));

        var snapshot = Snapshot(test);

        // Nothing on the shelf means nothing to lose - the page must not cry over an empty box.
        Assert.Equal(0, snapshot.ExpiringSoon);
        Assert.Equal(0, snapshot.Expired);
    }

    [Fact]
    public void ChangingTheNearestBandInSettings_MovesTheCard()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 20);
        SetExpiry(test, product, InDays(75));

        Assert.Equal(1, Snapshot(test).ExpiringSoon);

        test.Settings.SetMany(
            new Dictionary<string, object?> { ["expiry.levels"] = Bands(400, 120, 1) },
            TestData.Owner);

        // The owner widened the band: the same batch is no longer "soon" for him.
        Assert.Equal(0, Snapshot(test).ExpiringSoon);
    }

    /// <summary>Three bands, nearest last - the shape the Settings page stores.</summary>
    private static string Bands(int level1, int level2, int level3) =>
        "[{\"days\":" + level1 + ",\"color\":\"blue\",\"label\":\"\"},"
        + "{\"days\":" + level2 + ",\"color\":\"amber\",\"label\":\"\"},"
        + "{\"days\":" + level3 + ",\"color\":\"red\",\"label\":\"\"}]";

    // ============================== the books ==============================

    [Fact]
    public void CreditAndPayable_AreTheTwoSidesOfTheKhata()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var customer = TestData.AddCustomer(test, limitPaisa: 500000);
        TestData.AddSupplier(test, openingPaisa: 250000);

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 2, method: "credit", customerId: customer), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);

        var snapshot = Snapshot(test);

        // A credit bill is money the shop is owed; a supplier opening is money the shop owes.
        Assert.Equal(60000L, snapshot.CustomerCreditPaisa);
        Assert.Equal(250000L, snapshot.SupplierPayablePaisa);

        // The bill still counts as today's sale - the goods left the shop and were charged. Whether
        // the money arrived is the khata card's job, not the takings card's.
        Assert.Equal(1, snapshot.TodayBills);
        Assert.Equal(60000L, snapshot.TodaySalesPaisa);
    }

    [Fact]
    public void TheDrawerCard_FollowsTheOpenShift()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        Assert.False(Snapshot(test).ShiftOpen);

        var shifts = new ShiftService(test.Database, test.Audit, TestData.Permissions(test), test.Log);
        var opened = shifts.Open(TestData.Owner, 100000, "morning");
        Assert.True(opened.Ok, opened.Error);

        Assert.True(Snapshot(test).ShiftOpen);

        // Selling on a card leaves the drawer open - only the shift decides the card.
        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "card"), TestData.Owner).Ok);
        Assert.True(Snapshot(test).ShiftOpen);
    }

    [Fact]
    public void AClosedShift_ClosesTheDrawerCard()
    {
        using var test = new TestDatabase();

        var shifts = new ShiftService(test.Database, test.Audit, TestData.Permissions(test), test.Log);
        Assert.True(shifts.Open(TestData.Owner, 100000, "morning").Ok);
        Assert.True(Snapshot(test).ShiftOpen);

        var closed = shifts.Close(TestData.Owner, 100000, "evening");
        Assert.True(closed.Ok, closed.Error);

        // The card must follow the row, not the memory of the page: once the drawer is counted and
        // put away, the home screen stops saying "shift is open".
        Assert.False(Snapshot(test).ShiftOpen);
    }
}
