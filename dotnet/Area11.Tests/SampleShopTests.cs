using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The sample shop (F-53, D-69): one button that fills every page of a new Area11 with a believable
/// day's work.
///
/// Why the tests are this strict about arithmetic: demo money is still money on a screen. If the
/// sample bills did not add up, or the shelves disagreed with their own movement history, the owner
/// would spend his first week hunting a fault that the demo put there - and he would stop trusting
/// the reports before he ever used them.
/// </summary>
public sealed class SampleShopTests
{
    // ============================== the whole shop ==============================

    [Fact]
    public void TheSampleShopFillsEveryPageOfANewShop()
    {
        using var test = new TestDatabase();
        var seeded = Sample(test).Seed(TestData.Owner);

        Assert.True(seeded.Ok, seeded.Error);

        Assert.Equal(20, Count(test, "products"));
        Assert.Equal(7, Count(test, "categories"));
        Assert.Equal(12, Count(test, "companies"));
        Assert.Equal(4, Count(test, "suppliers"));
        Assert.Equal(8, Count(test, "customers"));
        Assert.Equal(4, Count(test, "purchases"));
        Assert.Equal(12, Count(test, "sales"));
        Assert.Equal(2, Count(test, "sale_returns"));
        Assert.Equal(3, Count(test, "expenses"));
        Assert.Equal(2, Count(test, "owner_drawings"));
        Assert.Equal(1, Count(test, "shifts"));
        Assert.Equal(1, Count(test, "stock_takes"));
        Assert.Equal(1, Count(test, "supplier_returns"));
        Assert.Equal(1, Count(test, "provisional_returns"));
        Assert.Equal(2, Count(test, "custom_fields"));
        Assert.Equal(2, Count(test, "custom_values"));
        Assert.Equal(1, Count(test, "held_carts"));
        Assert.Equal(2, Count(test, "product_barcodes"));
        Assert.True(Count(test, "payments") >= 10);
        Assert.True(Count(test, "stock_movements") > 40);
    }

    [Fact]
    public void TheSampleRunsInOneGoOrNotAtAll()
    {
        using var test = new TestDatabase();
        var report = Sample(test).Seed(TestData.Owner).Value!;

        // Every promise the page made is a row: no step may be quietly dropped.
        Assert.True(report.CanSeed);
        Assert.Equal(12, report.Steps.Count);
        Assert.Contains("20 medicines", string.Join(" ", report.Steps));
    }

    // ============================== the money adds up ==============================

    [Fact]
    public void EverySampleBillTotalEqualsItsLines()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        var bills = Bills(test);

        Assert.Equal(12, bills.Count);

        foreach (var bill in bills)
        {
            Assert.Equal(bill.Subtotal, bill.LineTotal);
            Assert.Equal(bill.Subtotal - bill.RoundOff, bill.Total);
            Assert.Equal(bill.Total, bill.Paid + bill.Due);
            Assert.True(bill.LineTotal > 0);
        }
    }

    [Fact]
    public void EveryPaymentBelongsToABillAndNeverExceedsIt()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var wrong = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM sales s
WHERE s.paid_paisa <> IFNULL((SELECT SUM(p.amount_paisa) FROM payments p WHERE p.sale_id = s.id), 0);") ?? 0L);

        Assert.Equal(0L, wrong);
    }

    [Fact]
    public void TheShelvesAreTheSumOfTheirOwnMovements()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var wrong = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM batches b
WHERE ABS(b.qty_base - IFNULL((SELECT SUM(m.qty_base) FROM stock_movements m WHERE m.batch_id = b.id), 0)) > 0.0001;") ?? 0L);

        Assert.Equal(0L, wrong);
    }

    [Fact]
    public void TheKhataOfTheSampleAddsUp()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var owed = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT IFNULL(SUM(due_paisa), 0) FROM sales WHERE customer_id IS NOT NULL;") ?? 0L);

        // A return that went against the khata takes money off it without editing the bill.
        var takenOff = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT IFNULL(SUM(credit_reduced_paisa), 0) FROM sale_returns;") ?? 0L);

        var opening = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT IFNULL(SUM(opening_balance_paisa), 0) FROM customers;") ?? 0L);

        var balance = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT IFNULL(SUM(balance_paisa), 0) FROM customers;") ?? 0L);

        Assert.True(owed > 0);
        Assert.Equal(opening + owed - takenOff, balance);
    }

    [Fact]
    public void OnePurchaseIsUnpaidSoTheSupplierPageOwesMoney()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var unpaid = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM purchases WHERE due_paisa > 0;") ?? 0L);

        var payable = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(balance_paisa), 0) FROM suppliers;") ?? 0L);

        Assert.Equal(1L, unpaid);
        Assert.True(payable > 0);
    }

    // ============================== the shelves tell a story ==============================

    [Fact]
    public void TheSampleHasAHealthyShelfAnEmptyOneAndSomethingExpired()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var empty = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM products p
WHERE IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) = 0;") ?? 0L);

        var expired = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM batches WHERE active = 1 AND date(expiry_date) < date('now', 'localtime');") ?? 0L);

        var nearExpiry = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM batches WHERE active = 1 AND date(expiry_date) >= date('now', 'localtime')
  AND date(expiry_date) <= date('now', 'localtime', '+90 day');") ?? 0L);

        Assert.True(empty >= 1);
        Assert.True(expired >= 1);
        Assert.True(nearExpiry >= 1);
    }

    [Fact]
    public void EveryMedicineHasACategoryACompanyAndABarcode()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var bare = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM products
WHERE category_id IS NULL OR company_id IS NULL OR IFNULL(barcode, '') = '' OR IFNULL(rack_no, '') = '';") ?? 0L);

        Assert.Equal(0L, bare);
    }

    [Fact]
    public void EveryBillSaysHowItWasPaidAndTheThreeStatesAllAppear()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var methods = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(DISTINCT payment_method) FROM sales;") ?? 0L);

        var statuses = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(DISTINCT status) FROM sales;") ?? 0L);

        Assert.Equal(5L, methods);
        Assert.Equal(3L, statuses);
    }

    [Fact]
    public void TheReturnsAndPapersHaveTheirOwnNumbers()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var codes = test.Database.Query<string>(connection, @"
SELECT code FROM sale_returns ORDER BY code;");

        Assert.Equal(2, codes.Count);
        Assert.Equal("RET-0001", codes[0]);
        Assert.Equal("RET-0002", codes[1]);

        var storedQcode = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM sale_returns WHERE IFNULL(qcode, '') <> '';") ?? 0L);

        Assert.Equal(1L, storedQcode); // one back to the shelf, one to quarantine
    }

    // ============================== the counters carry on ==============================

    [Fact]
    public void TheFirstRealBillAfterTheSampleIsNumberThirteen()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        var productId = TestData.AddProduct(test);
        var sale = TestData.Sales(test).Create(TestData.Cart(productId), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal("INV-0013", sale.Value!.Code);
    }

    // ============================== it refuses rather than risk real money ==============================

    [Fact]
    public void AShopWithOneRealBillIsNeverFilledWithDemoMoney()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);
        Assert.True(TestData.Sales(test).Create(TestData.Cart(productId), TestData.Owner).Ok);

        var refused = Sample(test).Seed(TestData.Owner);

        Assert.False(refused.Ok);
        Assert.Contains("bills", refused.Error!);
        Assert.Equal(1, Count(test, "products")); // the real shelf was not touched
        Assert.Equal(1, Count(test, "sales"));
    }

    [Fact]
    public void TheSampleIsNeverAddedTwice()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        var again = Sample(test).Seed(TestData.Owner);

        Assert.False(again.Ok);
        Assert.Equal(20, Count(test, "products"));
    }

    [Fact]
    public void ACashierCannotFillTheShop()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        var sample = Sample(test);

        Assert.False(sample.CanSeed(cashier));
        Assert.False(sample.Seed(cashier).Ok);
        Assert.False(sample.Preview(cashier).Ok);
        Assert.Equal(0, Count(test, "products"));
    }

    [Fact]
    public void ThePreviewWritesNothing()
    {
        using var test = new TestDatabase();

        var preview = Sample(test).Preview(TestData.Owner);

        Assert.True(preview.Ok, preview.Error);
        Assert.True(preview.Value!.CanSeed);
        Assert.Equal(0, Count(test, "products"));
        Assert.Equal(0, Count(test, "sales"));
    }

    // ============================== the pages it feeds ==============================

    [Fact]
    public void TheBillsPageFindsTheTwelveSampleBills()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        var history = new SaleHistoryService(test.Database,
            new ReceiptService(test.Database, test.Settings, test.Log),
            new ReturnReceiptService(test.Database, test.Settings, test.Log),
            test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit),
            test.Log);

        var page = history.Search(TestData.Owner, new SaleHistoryFilter
        {
            From = Clock.DateDaysAgo(30),
            To = Clock.Today()
        });

        Assert.True(page.Ok, page.Error);
        Assert.Equal(12, page.Value!.Count);
        Assert.True(page.Value.TotalPaisa > 0);

        var first = page.Value.Rows[0];
        var detail = history.Open(TestData.Owner, first.Id);

        Assert.True(detail.Ok, detail.Error);
        Assert.NotEmpty(detail.Value!.Lines);
    }

    [Fact]
    public void TheSampleLeavesOneLineInTheBlackBox()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var lines = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = @action;",
            new { action = SampleShopService.AuditAction }) ?? 0L);

        var details = test.Database.Scalar(connection,
            "SELECT details FROM audit_logs WHERE action = @action LIMIT 1;",
            new { action = SampleShopService.AuditAction })?.ToString() ?? string.Empty;

        Assert.Equal(1L, lines);
        Assert.Contains("sample data", details);
    }

    // ============================== helpers ==============================

    private static SampleShopService Sample(TestDatabase test) =>
        new(test.Database, test.Settings, test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static long Count(TestDatabase test, string table)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection, $"SELECT COUNT(1) FROM {table};") ?? 0L);
    }

    private sealed class BillRow
    {
        public long Subtotal { get; set; }

        public long RoundOff { get; set; }

        public long Total { get; set; }

        public long Paid { get; set; }

        public long Due { get; set; }

        public long LineTotal { get; set; }
    }

    private static List<BillRow> Bills(TestDatabase test)
    {
        using var connection = test.Database.Open();

        return test.Database.Query<BillRow>(connection, @"
SELECT s.subtotal_paisa AS subtotal, s.round_off_paisa AS roundOff, s.total_paisa AS total,
       s.paid_paisa AS paid, s.due_paisa AS due,
       IFNULL((SELECT SUM(si.line_total_paisa) FROM sale_items si WHERE si.sale_id = s.id), 0) AS lineTotal
FROM sales s
ORDER BY s.id;");
    }
}
