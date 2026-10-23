using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The sample shop (F-53, D-69, D-74): one button that fills a new Area11 with two and a half months
/// of one small pharmacy.
///
/// Why the tests are this strict about arithmetic: demo money is still money on a screen. If the
/// sample bills did not add up, if the shelves disagreed with their own movement history, or if a
/// drawer said it was short when the payments say it was exact, the owner would spend his first week
/// hunting a fault that the demo put there - and he would stop trusting the reports before he ever
/// used them. Every page he is going to judge is fed by these rows, so every sum is checked here.
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

        // What stands on the shelves and behind the counter.
        Assert.Equal(44, Count(test, "products"));
        Assert.Equal(17, Count(test, "categories"));
        Assert.Equal(16, Count(test, "companies"));
        Assert.Equal(6, Count(test, "suppliers"));
        Assert.Equal(20, Count(test, "customers"));

        // Two and a half months of work: the deliveries, the bills, the papers that came back.
        Assert.True(Count(test, "purchases") >= 30);
        Assert.True(Count(test, "sales") >= 500);
        Assert.True(Count(test, "sale_returns") >= 8);
        Assert.True(Count(test, "expenses") >= 40);
        Assert.True(Count(test, "owner_drawings") >= 6);
        Assert.Equal(2, Count(test, "stock_takes"));
        Assert.Equal(2, Count(test, "supplier_returns"));
        Assert.Equal(2, Count(test, "provisional_returns"));

        // The drawer: one per day of the story, the last one still open.
        Assert.Equal(SampleShopService.HistoryDays + 1, Count(test, "shifts"));

        Assert.Equal(2, Count(test, "custom_fields"));
        Assert.Equal(4, Count(test, "custom_values"));
        Assert.Equal(2, Count(test, "held_carts"));
        Assert.Equal(4, Count(test, "product_barcodes"));

        Assert.True(Count(test, "payments") >= 400);
        Assert.True(Count(test, "stock_movements") >= 1500);
    }

    [Fact]
    public void TheStoryRunsForTwoAndAHalfMonthsAndEndsToday()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var first = test.Database.Scalar(connection,
            "SELECT MIN(SUBSTR(date, 1, 10)) FROM sales;")?.ToString() ?? string.Empty;

        var last = test.Database.Scalar(connection,
            "SELECT MAX(SUBSTR(date, 1, 10)) FROM sales;")?.ToString() ?? string.Empty;

        // The oldest bill is at least seventy days old, and the shop sold something today.
        Assert.True(string.CompareOrdinal(first, Clock.DateDaysAgo(SampleShopService.HistoryDays - 2)) <= 0,
            "the first bill is " + first);

        Assert.Equal(Clock.Today(), last);
    }

    [Fact]
    public void TheSampleRunsInOneGoOrNotAtAll()
    {
        using var test = new TestDatabase();
        var report = Sample(test).Seed(TestData.Owner).Value!;

        // Every promise the page made is a row: no step may be quietly dropped.
        Assert.True(report.CanSeed);
        Assert.Equal(13, report.Steps.Count);
        Assert.Contains("44 medicines", string.Join(" ", report.Steps));
        Assert.Contains("17 categories", string.Join(" ", report.Steps));
    }

    // ============================== the money adds up ==============================

    [Fact]
    public void EverySampleBillTotalEqualsItsLines()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        var bills = Bills(test);

        Assert.True(bills.Count >= 500);

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

        var wrongBills = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM sales s
WHERE s.paid_paisa <> IFNULL((SELECT SUM(p.amount_paisa) FROM payments p WHERE p.sale_id = s.id), 0);") ?? 0L);

        Assert.Equal(0L, wrongBills);

        // A delivery is paid by the same rule: the bill says what was paid, and the money rows agree.
        var wrongPurchases = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM purchases b
WHERE b.paid_paisa <> IFNULL((SELECT SUM(p.amount_paisa) FROM payments p WHERE p.purchase_id = b.id), 0);") ?? 0L);

        Assert.Equal(0L, wrongPurchases);
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

        // And no shelf may be below zero: a demo never sells medicine it never bought.
        var negative = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM batches WHERE qty_base < -0.0001;") ?? 0L);

        Assert.Equal(0L, negative);
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

        // So does the money the customer brings back weeks later.
        var collected = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT IFNULL(SUM(amount_paisa), 0) FROM payments
WHERE customer_id IS NOT NULL AND sale_id IS NULL AND purchase_id IS NULL;") ?? 0L);

        var opening = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT IFNULL(SUM(opening_balance_paisa), 0) FROM customers;") ?? 0L);

        var balance = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT IFNULL(SUM(balance_paisa), 0) FROM customers;") ?? 0L);

        Assert.True(owed > 0);
        Assert.True(collected > 0);
        Assert.Equal(opening + owed - takenOff - collected, balance);
    }

    [Fact]
    public void EveryCustomerWhoOwesMoneyCarriesItFromABillOrFromBeforeArea11()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        // A balance with nothing behind it would be a phantom debt on the khata page.
        var phantom = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM customers c
WHERE c.balance_paisa > 0
  AND c.opening_balance_paisa = 0
  AND IFNULL((SELECT SUM(s.due_paisa) FROM sales s WHERE s.customer_id = c.id AND s.due_paisa > 0), 0) = 0;") ?? 0L);

        Assert.Equal(0L, phantom);
    }

    [Fact]
    public void TheSupplierPageHasOldBillsAndAPayable()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var unpaid = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM purchases WHERE due_paisa > 0;") ?? 0L);

        var payable = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(balance_paisa), 0) FROM suppliers;") ?? 0L);

        // The payable must be exactly the unpaid part of the deliveries - no invented debt.
        var due = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(due_paisa), 0) FROM purchases;") ?? 0L);

        Assert.True(unpaid >= 1);
        Assert.True(payable > 0);
        Assert.Equal(due, payable);
    }

    // ============================== the shelves tell a story ==============================

    [Fact]
    public void TheSampleHasAHealthyShelfAnEmptyOneSomethingExpiredAndSomethingNearly()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var empty = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM products p
WHERE IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) = 0;") ?? 0L);

        var expired = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM batches WHERE active = 1 AND qty_base > 0
  AND date(expiry_date) < date('now', 'localtime');") ?? 0L);

        var nearExpiry = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM batches WHERE active = 1 AND qty_base > 0
  AND date(expiry_date) >= date('now', 'localtime')
  AND date(expiry_date) <= date('now', 'localtime', '+90 day');") ?? 0L);

        var healthy = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM batches WHERE active = 1 AND qty_base > 0
  AND date(expiry_date) > date('now', 'localtime', '+180 day');") ?? 0L);

        Assert.True(empty >= 1);
        Assert.True(expired >= 1);
        Assert.True(nearExpiry >= 1);
        Assert.True(healthy >= 10);
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
    public void TheSampleUsesEveryCategoryItPromised()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var used = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(DISTINCT category_id) FROM products;") ?? 0L);

        // Fourteen shelves under three headings - tablets to equipment.
        Assert.Equal(14L, used);

        var parents = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM categories WHERE parent_id IS NULL;") ?? 0L);

        Assert.Equal(3L, parents);
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
    public void SomeBillsWereWrittenOnADoctorsPrescription()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var prescribed = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM sales WHERE doctor_id IS NOT NULL;") ?? 0L);

        var doctors = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM customers WHERE category = 'doctor';") ?? 0L);

        Assert.Equal(3L, doctors);
        Assert.True(prescribed > 10);
    }

    [Fact]
    public void TheReturnsAndPapersHaveTheirOwnNumbers()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var codes = test.Database.Query<string>(connection, @"
SELECT code FROM sale_returns ORDER BY id;");

        Assert.True(codes.Count >= 8);

        for (var index = 0; index < codes.Count; index++)
        {
            Assert.Equal("RET-" + (index + 1).ToString("D4"), codes[index]);
        }

        // Quarantine goods are set aside with a Q number of their own; restocked goods are not.
        var storedQcode = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM sale_returns WHERE IFNULL(qcode, '') <> '';") ?? 0L);

        var quarantined = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM sale_returns WHERE restock = 0;") ?? 0L);

        Assert.True(quarantined >= 1);
        Assert.Equal(quarantined, storedQcode);
    }

    [Fact]
    public void TheReturnsComeBackOverTwoMonthsNotOnOneDay()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var first = test.Database.Scalar(connection,
            "SELECT MIN(SUBSTR(date, 1, 10)) FROM sale_returns;")?.ToString() ?? string.Empty;

        Assert.True(string.CompareOrdinal(first, Clock.DateDaysAgo(30)) < 0,
            "the oldest return is " + first);
    }

    // ============================== the drawer ==============================

    [Fact]
    public void EveryClosedDrawerSaysExactlyWhatItsOwnPaymentsSay()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        // The same seven lines ShiftService uses: float + cash bills + khata money + supplier money in,
        // minus refunds, supplier money out, expenses and drawings. If the sample ever typed a number
        // instead of adding them up, this is where it would show.
        var wrong = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM shifts s
WHERE s.status = 'closed' AND s.expected_paisa <> (
    s.opening_float_paisa
  + IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
            WHERE p.method = 'cash' AND p.sale_id IS NOT NULL
              AND p.date >= s.opened_at AND p.date <= s.closed_at), 0)
  + IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
            WHERE p.method = 'cash' AND p.sale_id IS NULL AND p.customer_id IS NOT NULL
              AND p.date >= s.opened_at AND p.date <= s.closed_at), 0)
  + IFNULL((SELECT SUM(sr.total_paisa) FROM supplier_returns sr
            WHERE sr.settled = 1 AND sr.date >= s.opened_at AND sr.date <= s.closed_at), 0)
  - IFNULL((SELECT SUM(r.cash_refund_paisa) FROM sale_returns r
            WHERE r.date >= s.opened_at AND r.date <= s.closed_at), 0)
  - IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
            WHERE p.method = 'cash' AND p.supplier_id IS NOT NULL
              AND p.date >= s.opened_at AND p.date <= s.closed_at), 0)
  - IFNULL((SELECT SUM(e.amount_paisa) FROM expenses e
            WHERE e.date >= s.opened_at AND e.date <= s.closed_at), 0)
  - IFNULL((SELECT SUM(d.amount_paisa) FROM owner_drawings d
            WHERE d.type = 'cash' AND d.date >= s.opened_at AND d.date <= s.closed_at), 0));") ?? 0L);

        Assert.Equal(0L, wrong);
    }

    [Fact]
    public void TheDrawerIsNeverNegativeAndHasTwoSmallMistakes()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var negative = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM shifts WHERE status = 'closed' AND (expected_paisa < 0 OR actual_paisa < 0);") ?? 0L);

        var exact = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM shifts WHERE status = 'closed' AND difference_paisa = 0;") ?? 0L);

        var short_ = Convert.ToInt64(test.Database.Scalar(connection, @"
SELECT COUNT(1) FROM shifts WHERE status = 'closed' AND difference_paisa <> 0;") ?? 0L);

        var open = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM shifts WHERE status = 'open';") ?? 0L);

        Assert.Equal(0L, negative);
        Assert.True(exact > SampleShopService.HistoryDays - 10);
        Assert.Equal(2L, short_);
        Assert.Equal(1L, open);
    }

    // ============================== a real shop's month ==============================

    [Fact]
    public void TheMonthHasTheBillsARealshopWouldHave()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var categories = test.Database.Query<string>(connection, @"
SELECT DISTINCT category FROM expenses ORDER BY category;");

        var joined = string.Join(",", categories);

        foreach (var expected in new[] { "rent", "salary", "electricity", "tea", "repair", "transport", "packing" })
        {
            Assert.Contains(expected, joined);
        }

        // The owner's own examples: the staff's chai, and the fan that was repaired.
        var tea = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM expenses WHERE title LIKE 'Chai%' OR title LIKE 'Tea%';") ?? 0L);

        var fan = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM expenses WHERE title LIKE '%fan%';") ?? 0L);

        Assert.True(tea >= 10);
        Assert.Equal(1L, fan);
    }

    [Fact]
    public void TheSampleHasGoodsThatWereLostAndGoodsThatWentBackToTheCompany()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        var reasons = test.Database.Query<string>(connection, @"
SELECT DISTINCT reason FROM stock_adjustments ORDER BY reason;");

        var joined = string.Join(",", reasons);

        Assert.Contains("damaged", joined);
        Assert.Contains("lost", joined);

        var waiting = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM supplier_returns WHERE settled = 0;") ?? 0L);

        var settled = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM supplier_returns WHERE settled = 1;") ?? 0L);

        Assert.Equal(1L, waiting);
        Assert.Equal(1L, settled);
    }

    // ============================== the counters carry on ==============================

    [Fact]
    public void TheFirstRealBillAfterTheSampleIsTheNextNumber()
    {
        using var test = new TestDatabase();

        var before = Count(test, "sales");
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);
        Assert.True(before == 0);

        var seeded = Count(test, "sales");

        var productId = TestData.AddProduct(test);
        var sale = TestData.Sales(test).Create(TestData.Cart(productId), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal("INV-" + (seeded + 1).ToString("D4"), sale.Value!.Code);
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
    public void AShopWithThirtyRealMedicinesIsNotADemo()
    {
        using var test = new TestDatabase();

        for (var index = 0; index < 30; index++)
        {
            TestData.AddProduct(test);
        }

        var refused = Sample(test).Seed(TestData.Owner);

        Assert.False(refused.Ok);
        Assert.Contains("medicines", refused.Error!);
        Assert.Equal(30, Count(test, "products"));
        Assert.Equal(0, Count(test, "sales"));
    }

    [Fact]
    public void TheSampleIsNeverAddedTwice()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        var again = Sample(test).Seed(TestData.Owner);

        Assert.False(again.Ok);
        Assert.Equal(44, Count(test, "products"));
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
    public void TheBillsPageFindsThreeMonthsOfBills()
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
        Assert.True(page.Value!.Count >= 200);
        Assert.True(page.Value.TotalPaisa > 0);

        var first = page.Value.Rows[0];
        var detail = history.Open(TestData.Owner, first.Id);

        Assert.True(detail.Ok, detail.Error);
        Assert.NotEmpty(detail.Value!.Lines);
    }

    [Fact]
    public void TheSalesLookupFindsABillFromMonthsAgo()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        var returns = TestData.Returns(test);

        // A phone number is enough - with dashes, with spaces or as bare digits.
        var found = returns.Search(new SaleLookupInput { Text = "0300-1234567" });

        Assert.True(found.Ok, found.Error);
        Assert.NotEmpty(found.Value!);
        Assert.Contains(found.Value!, row => row.CustomerName == "Ali Raza");

        var bare = returns.Search(new SaleLookupInput { Text = "03001234567" });

        Assert.True(bare.Ok, bare.Error);
        Assert.Equal(found.Value!.Count, bare.Value!.Count);
    }

    [Fact]
    public void TheReportsHaveSomethingToAddUp()
    {
        using var test = new TestDatabase();
        Assert.True(Sample(test).Seed(TestData.Owner).Ok);

        using var connection = test.Database.Open();

        // Every day of the last two months has either a bill or a written reason for silence - the
        // report page draws a bar per day, and empty weeks would look like a broken page.
        var days = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(DISTINCT SUBSTR(date, 1, 10)) FROM sales;") ?? 0L);

        Assert.True(days >= 60);

        var revenue = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(total_paisa), 0) FROM sales;") ?? 0L);

        var cost = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(si.cost_paisa_at_sale * si.qty_base), 0) FROM sale_items si;") ?? 0L);

        Assert.True(revenue > 0);
        Assert.True(cost > 0);
        Assert.True(revenue > cost);
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
        Assert.Contains("bills", details);
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
