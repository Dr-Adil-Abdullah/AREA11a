using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Loyalty at the counter (F-40, Spec 13.6 &amp; 17.6) - rupees → points → stars → a title.
///
/// The promises this file pins down:
///   1. points are **off by default**; with the switch on, a bill earns one point per the owner's
///      rupee rate, on the money the bill really costs;
///   2. a customer can **spend** points: 1 point = Rs 1, written as a negative `loyalty` payment row,
///      and the printed lines still add up to the printed total (Spec 11.4);
///   3. a redemption is **capped**, never refused: by the balance, by the bill, and - when the
///      below-cost lock is on - by the cost of the goods;
///   4. the points a customer spends come off his khata, so he cannot spend the same point twice;
///   5. the stars and the title are one rule shared by every screen.
/// </summary>
public sealed class LoyaltyTests
{
    // ============================== earning ==============================

    [Fact]
    public void PointsAreOffByDefault()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 2, method: "cash", customerId: customer), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(0, Points(test, customer));
        Assert.Equal(0L, Rows(test, "SELECT COUNT(1) FROM payments WHERE method = 'loyalty';"));
    }

    [Fact]
    public void AnEarnedPointIsOnePerTheOwnersRate()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        Assert.True(test.Settings.Set("loyalty.enabled", true, TestData.Owner).Ok);
        Assert.True(test.Settings.Set("loyalty.rupeesPerPoint", 100, TestData.Owner).Ok);
        test.Settings.Reload();

        // Rs 600 spent at Rs 100 per point = 6 points.
        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 2, method: "cash", customerId: customer), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(6, Points(test, customer));
    }

    // ============================== spending ==============================

    [Fact]
    public void SpentPointsCutTheBill_AndTheLinesStillAddUp()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        EnableLoyalty(test, perRupeePoint: 1);
        Give(test, customer, points: 500);

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "cash", customerId: customer, redeemPoints: 100),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);

        // Rs 300 - Rs 100 = Rs 200; 100 points are gone, and the bill earned 200 back.
        Assert.Equal(20000L, sale.Value!.TotalPaisa);
        Assert.Equal(600, Points(test, customer));      // 500 - 100 + 200

        Assert.Equal(20000L, LineSum(test, sale.Value.SaleId));
        Assert.Equal(20000L, SaleTotal(test, sale.Value.SaleId));
    }

    [Fact]
    public void TheSpentPointsAreANegativeLoyaltyPaymentRow()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        EnableLoyalty(test, perRupeePoint: 100);
        Give(test, customer, points: 50);

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "cash", customerId: customer, redeemPoints: 5),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);

        // Spec 13.6.1 gives the example in paisa: 5 points = -500 paisa.
        Assert.Equal(-500L, Scalar(test, "SELECT amount_paisa FROM payments WHERE method = 'loyalty';"));
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM payments WHERE method = 'loyalty';"));

        // The drawer only ever holds the money that was really handed over.
        Assert.Equal(29500L, Scalar(test, "SELECT amount_paisa FROM payments WHERE method = 'cash';"));
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'points_redeemed';"));
    }

    [Fact]
    public void TheReceiptSaysPointsUsedWithoutAMinusSign()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        EnableLoyalty(test, perRupeePoint: 100);
        Give(test, customer, points: 50);

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "cash", customerId: customer, redeemPoints: 5),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);

        var receipt = new ReceiptService(test.Database, test.Settings, test.Log).Build(sale.Value!.SaleId);

        Assert.True(receipt.Ok, receipt.Error);
        Assert.Contains("Points used", receipt.Value!.PlainText);
        Assert.DoesNotContain("-5.00", receipt.Value.PlainText);
    }

    [Fact]
    public void ACreditBillUsesThePointsToo_AndOnlyTheRestGoesOnTheKhata()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        EnableLoyalty(test, perRupeePoint: 100);
        Give(test, customer, points: 50);

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "credit", customerId: customer, redeemPoints: 20),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);

        Assert.Equal(28000L, sale.Value!.TotalPaisa);
        Assert.Equal(28000L, sale.Value.DuePaisa);
        Assert.Equal(28000L, Scalar(test, "SELECT balance_paisa FROM customers WHERE id = " + customer + ";"));
    }

    // ============================== the caps ==============================

    [Fact]
    public void APointTheCustomerDoesNotHaveIsNotSpent()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        EnableLoyalty(test, perRupeePoint: 100);
        Give(test, customer, points: 10);

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "cash", customerId: customer, redeemPoints: 99),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);

        // Only ten points exist: Rs 10 off, and the warning says why the rest was not used.
        Assert.Equal(29000L, sale.Value!.TotalPaisa);
        Assert.Contains(sale.Value.Warnings, warning => warning.Contains("Only 10 point(s)"));
    }

    [Fact]
    public void TheBillNeverGoesBelowZero()
    {
        using var test = new TestDatabase();
        // Rs 45.50, and the below-cost lock is OFF: the only cap left is the bill itself, which
        // gives whole rupees away (Spec 13.6.1) - at most 45 points on a bill of 45.50.
        var product = TestData.AddProduct(test, costPaisa: 1000, retailPaisa: 4550, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        EnableLoyalty(test, perRupeePoint: 100);
        Give(test, customer, points: 900);

        Assert.True(test.Settings.Set("discount.blockBelowCost", false, TestData.Owner).Ok);

        // The bill is rounded down to Rs 10 by default, which would hide the case this test is
        // about: switch the rounding off so the bill keeps its 50 paisa.
        Assert.True(test.Settings.Set("bill.roundMode", "none", TestData.Owner).Ok);
        test.Settings.Reload();

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "cash", customerId: customer, redeemPoints: 900),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);

        // 45 points = Rs 45 off, and 50 paisa is left on the bill - never a minus bill.
        Assert.Equal(50L, sale.Value!.TotalPaisa);
        Assert.True(sale.Value.TotalPaisa >= 0);
        Assert.Equal(855, Points(test, customer));      // 900 - 45, and 50 paisa earns nothing
    }

    [Fact]
    public void TheBelowCostLockAlsoCapsThePoints()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        EnableLoyalty(test, perRupeePoint: 100);
        Give(test, customer, points: 900);

        Assert.True(test.Settings.Set("discount.blockBelowCost", true, TestData.Owner).Ok);
        test.Settings.Reload();

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "cash", customerId: customer, redeemPoints: 900),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);

        // Cost is Rs 200, so at most Rs 100 may be given away: 100 points, never 300.
        Assert.Equal(20000L, sale.Value!.TotalPaisa);
        Assert.Equal(20000L, LineSum(test, sale.Value.SaleId));
    }

    [Fact]
    public void WithTheLockOffTheOwnerMayGiveAwayEvenTheCost()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        EnableLoyalty(test, perRupeePoint: 100);
        Give(test, customer, points: 900);

        Assert.True(test.Settings.Set("discount.blockBelowCost", false, TestData.Owner).Ok);
        test.Settings.Reload();

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "cash", customerId: customer, redeemPoints: 900),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(0L, sale.Value!.TotalPaisa);
    }

    [Fact]
    public void PointsAreNotSpentWhenTheFeatureIsOff()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        Give(test, customer, points: 300);

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "cash", customerId: customer, redeemPoints: 100),
            TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(30000L, sale.Value!.TotalPaisa);
        Assert.Equal(300, Points(test, customer));
    }

    [Fact]
    public void AWalkInCustomerHasNoPointsToSpend()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);

        EnableLoyalty(test, perRupeePoint: 100);

        var sale = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "cash", redeemPoints: 100), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(30000L, sale.Value!.TotalPaisa);
    }

    [Fact]
    public void TheQuoteShowsTheRedemptionBeforeTheBillIsSaved()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        EnableLoyalty(test, perRupeePoint: 100);
        Give(test, customer, points: 500);

        var quote = TestData.Sales(test).Quote(
            TestData.Cart(product, qty: 1, method: "cash", customerId: customer, redeemPoints: 40),
            TestData.Owner);

        Assert.True(quote.Ok, quote.Error);
        Assert.Equal(40, quote.Value!.RedeemPoints);
        Assert.Equal(4000L, quote.Value.RedeemPaisa);
        Assert.Equal(26000L, quote.Value.TotalPaisa);
    }

    // ============================== stars and titles ==============================

    [Fact]
    public void TheStarsAndTitlesAreOneRule()
    {
        Assert.Equal(1, Loyalty.StarsFor(0));
        Assert.Equal(2, Loyalty.StarsFor(100));
        Assert.Equal(3, Loyalty.StarsFor(200));
        Assert.Equal(4, Loyalty.StarsFor(300));
        Assert.Equal(5, Loyalty.StarsFor(500));

        Assert.Equal("New", Loyalty.Title(1));
        Assert.Equal("Regular", Loyalty.Title(2));
        Assert.Equal("Very good", Loyalty.Title(4));
        Assert.Equal("VIP", Loyalty.Title(5));

        // The khata page and the counter read the same helpers, so the words never differ.
        Assert.Equal("3 point(s) - Rs 3 to spend", new CustomerSummary { LoyaltyPoints = 3 }.PointsLabel);
        Assert.Equal("no points yet", new CustomerSummary().PointsLabel);
    }

    // ============================== helpers ==============================

    private static void EnableLoyalty(TestDatabase test, int perRupeePoint)
    {
        Assert.True(test.Settings.Set("loyalty.enabled", true, TestData.Owner).Ok);
        Assert.True(test.Settings.Set("loyalty.rupeesPerPoint", perRupeePoint, TestData.Owner).Ok);
        test.Settings.Reload();
    }

    private static void Give(TestDatabase test, int customerId, int points)
    {
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "UPDATE customers SET loyalty_points = @points WHERE id = @id;",
                new { points, id = customerId });
        });
    }

    private static int Points(TestDatabase test, int customerId)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt32(test.Database.Scalar(connection,
            "SELECT loyalty_points FROM customers WHERE id = @id;", new { id = customerId }) ?? 0);
    }

    private static long LineSum(TestDatabase test, int saleId)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT SUM(line_total_paisa) FROM sale_items WHERE sale_id = @id;", new { id = saleId }) ?? 0);
    }

    private static long SaleTotal(TestDatabase test, int saleId)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT total_paisa FROM sales WHERE id = @id;", new { id = saleId }) ?? 0);
    }

    private static long Scalar(TestDatabase test, string sql)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection, sql) ?? 0);
    }

    private static long Rows(TestDatabase test, string sql) => Scalar(test, sql);
}
