using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using System.Globalization;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Reports (F-34, Spec 21). The tests that matter are the money ones: the profit formula of Spec 21.6,
/// the return rule of Spec 15.6, and the rule that a manager sees the shop but not its profit.
///
/// The rows are written straight into the database with an explicit date instead of through the sale
/// screen: a report about "70 days ago" must not depend on the machine running the test in the right
/// order, and this file is about reading, not about selling.
/// </summary>
public sealed class ReportTests
{
    private const long UnitPrice = 8000;   // Rs 80
    private const long UnitCost = 5000;    // Rs 50

    // ------------------------------------------------------------------ the period buttons

    [Fact]
    public void TheFourPeriods_CoverTheDaysTheyPromise()
    {
        using var test = new TestDatabase();
        var reports = TestData.Reports(test);

        var today = reports.Period(ReportRange.Today);
        Assert.Equal(today.FromDate, today.ToDate);
        Assert.Equal(1, today.Days);
        Assert.Equal(Clock.Today(), today.ToDate);

        var week = reports.Period(ReportRange.Last7);
        Assert.Equal(7, week.Days);                                  // today + the six before it
        Assert.Equal(Clock.Today(), week.ToDate);
        Assert.Equal(Day(-6), week.FromDate);

        var month = reports.Period(ReportRange.Last30);
        Assert.Equal(30, month.Days);
        Assert.Equal(Day(-29), month.FromDate);

        var thisMonth = reports.Period(ReportRange.ThisMonth);
        Assert.Equal(1, Date(thisMonth.FromDate).Day);
        Assert.Equal(Clock.Today(), thisMonth.ToDate);

        // The window is half-open on the far end, so the whole last day is inside it.
        Assert.Equal(Date(today.ToDate).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 00:00:00",
            today.UntilStamp);
    }

    // ------------------------------------------------------------------ profit (Spec 21.6, 15.6)

    [Fact]
    public void Profit_FollowsTheSpecExample_WhenGoodsComeBackToTheShelf()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        // Spec 15.6.1: sold 6 for Rs 480 (cost Rs 300); 3 came back to the shelf, Rs 240 refunded.
        var sale = SeedSale(test, product, Clock.Today() + " 10:00:00", qty: 6);
        SeedReturn(test, sale, product, Clock.Today() + " 11:00:00", qty: 3, refundPaisa: 24000, restock: true);

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner);

        Assert.True(report.Ok, report.Error);

        var totals = report.Value!.Totals;

        Assert.Equal(48000L, totals.SalesPaisa);
        Assert.Equal(24000L, totals.RefundPaisa);
        Assert.Equal(30000L, totals.CostPaisa);              // 6 x Rs 50
        Assert.Equal(15000L, totals.RestockCostPaisa);       // 3 x Rs 50 came back
        Assert.Equal(9000L, totals.ProfitPaisa);             // Rs 90, not a loss of Rs 60
        Assert.Equal(37.5, totals.MarginPercent, 3);         // 90 of 240
    }

    [Fact]
    public void AQuarantineReturn_IsALoss_AndItsCostStays()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        var sale = SeedSale(test, product, Clock.Today() + " 10:00:00", qty: 6);
        SeedReturn(test, sale, product, Clock.Today() + " 11:00:00", qty: 3, refundPaisa: 24000, restock: false);

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner);
        var totals = report.Value!.Totals;

        // The money went back (24,000) but the goods did not come back to the shelf, so the full
        // cost of six strips stays: (48,000 - 24,000) - 30,000 = -6,000.
        Assert.Equal(0L, totals.RestockCostPaisa);
        Assert.Equal(-6000L, totals.ProfitPaisa);
    }

    [Fact]
    public void TopByQuantity_AndTopByProfit_AreNetOfRestockedReturns()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        var sale = SeedSale(test, product, Clock.Today() + " 10:00:00", qty: 6);
        SeedReturn(test, sale, product, Clock.Today() + " 11:00:00", qty: 3, refundPaisa: 24000, restock: true);

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner).Value!;

        var row = Assert.Single(report.TopByQty);

        Assert.Equal(3d, row.QtyBase);            // 6 sold, 3 back on the shelf
        Assert.Equal(24000L, row.SalesPaisa);
        Assert.Equal(9000L, row.ProfitPaisa);

        // The two lists agree on the money, and the item profit adds up to the total profit.
        // (This is the assertion that caught the column named `profit` while the property was
        // `ProfitPaisa`: Dapper mapped nothing, the row said 0, and the page showed no profit.)
        Assert.Equal(row.ProfitPaisa, report.TopByProfit[0].ProfitPaisa);
        Assert.Equal(row.SalesPaisa, report.TopByProfit[0].SalesPaisa);
        Assert.Equal(row.QtyBase, report.TopByProfit[0].QtyBase);
        Assert.Equal(report.Totals.ProfitPaisa, report.TopByQty.Sum(item => item.ProfitPaisa));
        Assert.Equal(report.Totals.ProfitPaisa, report.TopByProfit.Sum(item => item.ProfitPaisa));
    }

    [Fact]
    public void TopProductQuantity_KeepsFractionalBaseUnits()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var cart = TestData.Cart(product);
        cart.Lines[0].Quantity = 1.5;
        var sale = TestData.Sales(test).Create(cart, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        using var connection = test.Database.Open();
        var storedQuantity = test.Database.QuerySingle<double>(connection,
            "SELECT SUM(qty_base) FROM sale_items WHERE sale_id = @id;", new { id = sale.Value!.Id });

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner);

        Assert.Equal(1.5d, storedQuantity);
        Assert.True(report.Ok, report.Error);
        Assert.Equal(1.5d, Assert.Single(report.Value!.TopByQty).QtyBase);
    }

    [Fact]
    public void ProvisionalRefund_AndRestockedCost_AgreeAcrossEveryReport()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: UnitCost, retailPaisa: UnitPrice);
        SeedSale(test, product, Clock.Today() + " 10:00:00", qty: 6);

        var rush = TestData.Provisionals(test).Create(
            TestData.RushReturn(product, qty: 3, unit: "base", refundPaisa: 24000), TestData.Owner);
        Assert.True(rush.Ok, rush.Error);

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner).Value!;
        var totals = report.Totals;

        Assert.Equal(48000L, totals.SalesPaisa);
        Assert.Equal(24000L, totals.RefundPaisa);
        Assert.Equal(30000L, totals.CostPaisa);
        Assert.Equal(15000L, totals.RestockCostPaisa);
        Assert.Equal(9000L, totals.ProfitPaisa);
        Assert.Equal(totals.ProfitPaisa, report.Days.Sum(day => day.ProfitPaisa));

        var item = Assert.Single(report.TopByQty);
        Assert.Equal(3d, item.QtyBase);
        Assert.Equal(24000L, item.SalesPaisa);
        Assert.Equal(9000L, item.ProfitPaisa);
        Assert.Equal(totals.ProfitPaisa, report.TopByProfit.Sum(row => row.ProfitPaisa));

        var category = Assert.Single(report.Categories);
        Assert.Equal("Uncategorised", category.Name);
        Assert.Equal(24000L, category.SalesPaisa);
    }

    [Fact]
    public void RefundOnlyRows_DoNotReviveAnInactiveCategory()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "INSERT INTO categories(name, active, created_at) VALUES('Retired category', 0, @at);",
                new { at = Clock.Now() });
            var categoryId = test.Database.NextId(connection);
            test.Database.Execute(connection,
                "UPDATE products SET category_id = @categoryId WHERE id = @productId;",
                new { categoryId, productId = product });
        });
        var rush = TestData.Provisionals(test).Create(
            TestData.RushReturn(product, qty: 1, unit: "base", refundPaisa: 1000), TestData.Owner);
        Assert.True(rush.Ok, rush.Error);

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner).Value!;

        var category = Assert.Single(report.Categories);
        Assert.Equal("Uncategorised", category.Name);
        Assert.Equal(-1000L, category.SalesPaisa);
    }

    [Fact]
    public void CancelledProvisional_DisappearsFromReports_WithoutCashIn()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: UnitCost, retailPaisa: UnitPrice);
        var rush = TestData.Provisionals(test).Create(
            TestData.RushReturn(product, qty: 3, unit: "base", refundPaisa: 24000), TestData.Owner);
        Assert.True(rush.Ok, rush.Error);

        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "UPDATE provisional_returns SET date = @old WHERE id = @id;",
                new { old = Day(-1) + " 10:00:00", id = rush.Value!.Id });
            test.Database.Execute(connection,
                "UPDATE payments SET date = @old WHERE provisional_return_id = @id AND amount_paisa < 0;",
                new { old = Day(-1) + " 10:00:00", id = rush.Value.Id });
        });

        Assert.True(TestData.Provisionals(test).Cancel(rush.Value!.Id, "typed twice", TestData.Owner).Ok);

        var today = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner).Value!;
        Assert.Equal(0L, today.Totals.RefundPaisa);
        Assert.Equal(0L, today.Totals.RestockCostPaisa);
        Assert.Equal(0L, today.Totals.ProfitPaisa);
        Assert.Empty(today.TopByQty);
        Assert.Empty(today.Categories);

        var week = TestData.Reports(test).Build(ReportRange.Last7, TestData.Owner).Value!;
        Assert.Equal(0L, week.Totals.RefundPaisa);
        Assert.Equal(0L, week.Totals.RestockCostPaisa);
        Assert.Equal(0L, week.Totals.ProfitPaisa);
        Assert.Equal(0L, week.Days.Sum(day => day.ProfitPaisa));
        Assert.Empty(week.TopByQty);
        Assert.Empty(week.Categories);
    }

    // ------------------------------------------------------------------ the per-day sheet

    [Fact]
    public void PerDay_HasEveryDayOfThePeriod_EvenTheQuietOnes()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        SeedSale(test, product, Clock.Today() + " 10:00:00", qty: 2);

        var report = TestData.Reports(test).Build(ReportRange.Last7, TestData.Owner).Value!;

        Assert.Equal(7, report.Days.Count);
        Assert.Equal(Day(-6), report.Days[0].Date);
        Assert.Equal(Clock.Today(), report.Days[^1].Date);

        var today = report.Days[^1];
        Assert.Equal(16000L, today.SalesPaisa);
        Assert.True(today.HasSales);
        Assert.Equal(1.0, today.BarShare, 3);          // the only day with money is the full bar

        Assert.False(report.Days[0].HasSales);
        Assert.Equal(0, report.Days[0].BarShare, 3);
    }

    [Fact]
    public void PerDayProfit_AddsUpToTheTotalProfit()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        var sale = SeedSale(test, product, Clock.Today() + " 10:00:00", qty: 6);
        SeedReturn(test, sale, product, Clock.Today() + " 11:00:00", qty: 3, refundPaisa: 24000, restock: true);

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner).Value!;

        Assert.Equal(report.Totals.ProfitPaisa, report.Days.Sum(day => day.ProfitPaisa));
    }

    // ------------------------------------------------------------------ dead stock (Spec 21.3)

    [Fact]
    public void DeadStock_ListsTheShelfMoneyThatHasNotMovedForSixtyDays()
    {
        using var test = new TestDatabase();

        var old = TestData.AddProduct(test, name: "Old Syrup", batchQty: 10);
        var fresh = TestData.AddProduct(test, name: "Fresh Syrup", batchQty: 10);

        SeedSale(test, old, Day(-70) + " 10:00:00", qty: 1, code: "INV-7001");
        SeedSale(test, fresh, Clock.Today() + " 10:00:00", qty: 1, code: "INV-7002");

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner).Value!;

        var row = Assert.Single(report.DeadStock);

        Assert.Equal("Old Syrup", row.Name);
        Assert.Equal(Day(-70), row.LastSoldLabel);
        Assert.True(row.ValuePaisa > 0);          // money, at cost, still sitting on the shelf
    }

    [Fact]
    public void DeadStock_DoesNotListAMedicineWithNoStock()
    {
        using var test = new TestDatabase();

        TestData.AddProduct(test, name: "Empty Shelf", batchQty: 0);

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner).Value!;

        Assert.Empty(report.DeadStock);
    }

    // ------------------------------------------------------------------ who may see what

    [Fact]
    public void ACashier_IsRefused_AndAManagerSeesTheShopButNotTheProfit()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        SeedSale(test, product, Clock.Today() + " 10:00:00", qty: 6);

        var cashier = TestData.Cashier(test);
        var refused = TestData.Reports(test).Build(ReportRange.Today, cashier);

        Assert.False(refused.Ok);
        Assert.Contains("permission", refused.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var manager = TestData.AddUser(test, "manager");
        var forManager = TestData.Reports(test).Build(ReportRange.Today, manager);

        Assert.True(forManager.Ok, forManager.Error);
        Assert.False(forManager.Value!.CanSeeProfit);
        Assert.Equal(0L, forManager.Value!.Totals.ProfitPaisa);
        Assert.Empty(forManager.Value!.TopByProfit);

        // The rest of the shop's numbers are still there: the manager runs the day.
        Assert.Equal(48000L, forManager.Value!.Totals.SalesPaisa);
        Assert.Single(forManager.Value!.TopByQty);

        var forOwner = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner);
        Assert.True(forOwner.Value!.CanSeeProfit);
        Assert.NotEqual(0L, forOwner.Value!.Totals.ProfitPaisa);
    }

    // ------------------------------------------------------------------ the rest of the page

    [Fact]
    public void CategorySplit_AddsUpToTheNetSales()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        var sale = SeedSale(test, product, Clock.Today() + " 10:00:00", qty: 6);
        SeedReturn(test, sale, product, Clock.Today() + " 11:00:00", qty: 3, refundPaisa: 24000, restock: true);

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner).Value!;

        Assert.Equal(report.Totals.NetSalesPaisa, report.Categories.Sum(row => row.SalesPaisa));
        Assert.Equal(100.0, report.Categories[0].SharePercent, 3);
    }

    [Fact]
    public void BestAndWorstDay_AreTheOnesTheOwnerRemembers()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        SeedSale(test, product, Day(-3) + " 10:00:00", qty: 2, code: "INV-8001");    // Rs 160
        SeedSale(test, product, Day(-2) + " 10:00:00", qty: 6, code: "INV-8002");    // Rs 480

        var report = TestData.Reports(test).Build(ReportRange.Last7, TestData.Owner).Value!;

        Assert.Contains(Money.Format(48000), report.BestDayLabel);
        Assert.Contains(Money.Format(16000), report.WorstDayLabel);
    }

    [Fact]
    public void CashAndAverageBill_ComeFromTheSameBills()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        SeedSale(test, product, Clock.Today() + " 10:00:00", qty: 2, code: "INV-8101");   // Rs 160
        SeedSale(test, product, Clock.Today() + " 12:00:00", qty: 4, code: "INV-8102");   // Rs 320

        var totals = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner).Value!.Totals;

        Assert.Equal(2, totals.Bills);
        Assert.Equal(48000L, totals.SalesPaisa);
        Assert.Equal(48000L, totals.CashPaisa);          // both bills were paid in cash
        Assert.Equal(24000L, totals.AverageBillPaisa);   // 48,000 / 2
        Assert.Equal(6L, totals.ItemsBase);              // 2 + 4 units sold
    }

    // ------------------------------------------------------------------ helpers

    private static DateTime Date(string text) =>
        DateTime.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Day(int offset) =>
        Date(Clock.Today()).AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>One paid cash bill on an explicit date, with the item and the payment row behind it.</summary>
    private static int SeedSale(
        TestDatabase test,
        int productId,
        string date,
        long qty,
        long unitPrice = UnitPrice,
        long unitCost = UnitCost,
        string code = "INV-9001")
    {
        return test.Database.Tx(connection =>
        {
            var total = qty * unitPrice;

            test.Database.Execute(connection, @"
INSERT INTO sales(code, date, user_id, subtotal_paisa, discount_paisa, tax_paisa, round_off_paisa,
                  total_paisa, paid_paisa, due_paisa, status, payment_method, created_at)
VALUES(@code, @date, 1, @total, 0, 0, 0, @total, @total, 0, 'paid', 'cash', @date);",
                new { code, date, total });

            var saleId = (int)test.Database.NextId(connection);

            test.Database.Execute(connection, @"
INSERT INTO sale_items(sale_id, product_id, name_snapshot, qty_base, unit_sold, qty_entered,
                       unit_price_paisa, discount_paisa, line_total_paisa, cost_paisa_at_sale)
VALUES(@saleId, @productId, 'Item', @qty, 'base', @qty, @unitPrice, 0, @total, @unitCost);",
                new { saleId, productId, qty, unitPrice, total, unitCost });

            test.Database.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, sale_id, user_id, note)
VALUES(@date, 'cash', @total, @saleId, 1, 'Sale test');",
                new { date, total, saleId });

            return saleId;
        });
    }

    /// <summary>A return against that bill. `restock` is the one flag the profit formula turns on.</summary>
    private static void SeedReturn(
        TestDatabase test,
        int saleId,
        int productId,
        string date,
        long qty,
        long refundPaisa,
        bool restock)
    {
        test.Database.Tx(connection =>
        {
            var itemId = Convert.ToInt32(test.Database.Scalar(connection,
                "SELECT id FROM sale_items WHERE sale_id = @saleId LIMIT 1;", new { saleId }));

            test.Database.Execute(connection, @"
INSERT INTO sale_returns(date, sale_id, sale_item_id, product_id, qty_base, refund_paisa, restock,
                         cash_refund_paisa, credit_reduced_paisa, disposition, created_at)
VALUES(@date, @saleId, @itemId, @productId, @qty, @refund, @restock, @refund, 0, @disposition, @date);",
                new
                {
                    date,
                    saleId,
                    itemId,
                    productId,
                    qty,
                    refund = refundPaisa,
                    restock = restock ? 1 : 0,
                    disposition = restock ? "restock" : "quarantine"
                });
        });
    }
}
