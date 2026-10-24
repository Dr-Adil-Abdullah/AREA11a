using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Stock-in (F-18, Spec 9.2): a purchase bill writes the batch, the shelf, the supplier's payable and
/// the optional payment in ONE transaction.
///
/// Why the arithmetic is checked so closely: the purchase is where money leaves the shop before any
/// medicine is sold - a wrong total here is a wrong payable for the month, and a wrong quantity is a
/// shelf that lies to the counter for ever.
/// </summary>
public sealed class PurchaseTests
{
    // ============================== one box, twenty tablets ==============================

    [Fact]
    public void OneBoxEntersTheWholePackOnTheShelf()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test); // 2 strips of 10 = 20 tablets in a box
        var supplierId = TestData.AddSupplier(test);

        var purchase = Buy(test, supplierId, productId, unit: "box", qty: 1, cost: 1000);

        Assert.True(purchase.Ok, purchase.Error);
        Assert.Equal("PINV-0001", purchase.Value!.Code);
        Assert.Equal(20000, purchase.Value.TotalPaisa); // 20 tablets at Rs 10
        Assert.Equal(20, purchase.Value.Units);
        Assert.Equal(20, StockOf(test, productId));
    }

    [Fact]
    public void AStripEntersTenTabletsAndABaseUnitEntersOne()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test);
        var supplierId = TestData.AddSupplier(test);

        var strip = Buy(test, supplierId, productId, unit: "strip", qty: 2, cost: 1000);
        Assert.True(strip.Ok, strip.Error);
        Assert.Equal(20, StockOf(test, productId)); // 2 strips of 10

        var single = Buy(test, supplierId, productId, unit: "base", qty: 5, cost: 1000);
        Assert.True(single.Ok, single.Error);
        Assert.Equal(25, StockOf(test, productId));
    }

    // ============================== the payable ==============================

    [Fact]
    public void TheSupplierIsOwedWhatWasNotPaid()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test);
        var supplierId = TestData.AddSupplier(test);

        var purchase = Buy(test, supplierId, productId, unit: "base", qty: 10, cost: 1000, paid: 4000);

        Assert.True(purchase.Ok, purchase.Error);
        Assert.Equal(10000, purchase.Value!.TotalPaisa);
        Assert.Equal(6000, PayableOf(test, supplierId));
    }

    [Fact]
    public void PayingMoreThanTheBillIsClampedToTheBill()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test);
        var supplierId = TestData.AddSupplier(test);

        var purchase = Buy(test, supplierId, productId, unit: "base", qty: 10, cost: 1000, paid: 999999);

        Assert.True(purchase.Ok, purchase.Error);
        Assert.Equal(0, PayableOf(test, supplierId));
    }

    // ============================== the bonus line ==============================

    [Fact]
    public void ASampleLineIsFreeStockAndNeverReachesTheBill()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test);
        var supplierId = TestData.AddSupplier(test);

        var purchase = Buy(test, supplierId, productId, unit: "base", qty: 10, cost: 1000,
            sampleCost: 5000, sampleQty: 4);

        Assert.True(purchase.Ok, purchase.Error);
        Assert.Equal(10000, purchase.Value!.TotalPaisa); // only the paid line
        Assert.Equal(14, StockOf(test, productId));      // the bonus is on the shelf
        Assert.Equal(10000, PayableOf(test, supplierId));
    }

    // ============================== a warning, not a refusal ==============================

    [Fact]
    public void AMissingBatchNumberIsAWarningAndTheBatchIsNamed()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test);
        var supplierId = TestData.AddSupplier(test);

        var purchase = Buy(test, supplierId, productId, unit: "base", qty: 5, cost: 1000, batchNo: null);

        Assert.True(purchase.Ok, purchase.Error);
        Assert.Contains(purchase.Value!.Warnings, warning => warning.Contains("no batch number"));
        Assert.Equal("NO-BATCH", BatchNumberOf(test, productId));
    }

    [Fact]
    public void AnImpossibleExpiryWarnsAndLeavesTheBatchWithoutOne()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test);
        var supplierId = TestData.AddSupplier(test);

        var purchase = Buy(test, supplierId, productId, unit: "base", qty: 5, cost: 1000, expiry: "not-a-date");

        Assert.True(purchase.Ok, purchase.Error);
        Assert.Contains(purchase.Value!.Warnings, warning => warning.Contains("not valid"));
        Assert.Equal(string.Empty, ExpiryOf(test, productId));
    }

    [Fact]
    public void AnExpiryMonthIsStoredAsTheFirstAndLastDayItMeans()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test);
        var supplierId = TestData.AddSupplier(test);

        var purchase = Buy(test, supplierId, productId, unit: "base", qty: 5, cost: 1000, expiry: "2027-06");

        Assert.True(purchase.Ok, purchase.Error);
        Assert.Equal("2027-06", ExpiryMonthOf(test, productId));
        Assert.Equal("2027-06-30", ExpiryOf(test, productId)); // "June" means "until the last day of June"
    }

    // ============================== what is refused ==============================

    [Fact]
    public void APurchaseWithNoLinesIsRefused()
    {
        using var test = new TestDatabase();
        var supplierId = TestData.AddSupplier(test);

        var refused = Purchases(test).Create(
            new PurchaseInput { SupplierId = supplierId }, TestData.Owner);

        Assert.False(refused.Ok);
        Assert.Contains("at least one line", refused.Error!);
        Assert.Equal(0, Count(test, "purchases"));
    }

    [Fact]
    public void AQuantityOfZeroIsRefusedAndNothingIsWritten()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test);
        var supplierId = TestData.AddSupplier(test);

        var refused = Buy(test, supplierId, productId, unit: "base", qty: 0, cost: 1000);

        Assert.False(refused.Ok);
        Assert.Equal(0, Count(test, "purchases"));
        Assert.Equal(0, StockOf(test, productId)); // the shelf did not move
    }

    [Fact]
    public void ACashierCannotEnterAPurchase()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test);
        var supplierId = TestData.AddSupplier(test);
        var cashier = TestData.Cashier(test);

        var refused = Purchases(test).Create(
            new PurchaseInput
            {
                SupplierId = supplierId,
                Lines = new List<PurchaseLineInput>
                {
                    new() { ProductId = productId, Unit = "base", Quantity = 1, CostPaisa = 1000 }
                }
            },
            cashier);

        Assert.False(refused.Ok);
        Assert.Contains("permission", refused.Error!);
        Assert.Equal(0, Count(test, "purchases"));
    }

    // ============================== the shop notices ==============================

    [Fact]
    public void EveryPurchaseGetsTheNextNumberAndOneBlackBoxLine()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test);
        var supplierId = TestData.AddSupplier(test);

        var first = Buy(test, supplierId, productId, unit: "base", qty: 1, cost: 1000);
        var second = Buy(test, supplierId, productId, unit: "base", qty: 1, cost: 1000);

        Assert.Equal("PINV-0001", first.Value!.Code);
        Assert.Equal("PINV-0002", second.Value!.Code);
        Assert.Equal(2, Audits(test, "purchase_create"));
    }

    [Fact]
    public void ANewRetailPriceOnTheBillBecomesTheShopPrice()
    {
        using var test = new TestDatabase();
        var productId = NewProduct(test, retailPaisa: 30000);
        var supplierId = TestData.AddSupplier(test);

        var purchase = Buy(test, supplierId, productId, unit: "base", qty: 1, cost: 1000, retail: 45000);

        Assert.True(purchase.Ok, purchase.Error);

        using var connection = test.Database.Open();
        var retail = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT retail_paisa FROM products WHERE id = @id;", new { id = productId }) ?? 0L);

        Assert.Equal(45000, retail);
    }

    // ============================== helpers ==============================

    private static PurchaseService Purchases(TestDatabase test) => TestData.Purchases(test);

    /// <summary>
    /// A medicine with an EMPTY shelf: the shared helper ships one batch of 100, which would make every
    /// stock assertion here read 100 + the purchase and hide a real mistake.
    /// </summary>
    private static int NewProduct(TestDatabase test, long retailPaisa = 30000) =>
        TestData.AddProduct(test, retailPaisa: retailPaisa, batchQty: 0);

    private static ServiceResult<PurchaseResult> Buy(
        TestDatabase test,
        int supplierId,
        int productId,
        string unit,
        double qty,
        long cost,
        long paid = 0,
        string? batchNo = "B-9",
        string expiry = "2027-06-30",
        long retail = 0,
        long sampleCost = 0,
        double sampleQty = 0)
    {
        var input = new PurchaseInput
        {
            SupplierId = supplierId,
            SupplierInvoiceNo = "SUP-77",
            PaidPaisa = paid,
            Lines = new List<PurchaseLineInput>
            {
                new()
                {
                    ProductId = productId,
                    Unit = unit,
                    Quantity = qty,
                    BatchNo = batchNo,
                    Expiry = expiry,
                    CostPaisa = cost,
                    RetailPaisa = retail
                }
            }
        };

        if (sampleQty > 0)
        {
            input.Lines.Add(new PurchaseLineInput
            {
                ProductId = productId,
                Unit = "base",
                Quantity = sampleQty,
                BatchNo = "BONUS-1",
                Expiry = expiry,
                CostPaisa = sampleCost,
                RetailPaisa = retail,
                IsSample = true
            });
        }

        return Purchases(test).Create(input, TestData.Owner);
    }

    private static double StockOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();

        return Convert.ToDouble(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(qty_base), 0) FROM batches WHERE product_id = @id AND active = 1;",
            new { id = productId }) ?? 0d);
    }

    private static long PayableOf(TestDatabase test, int supplierId)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT balance_paisa FROM suppliers WHERE id = @id;", new { id = supplierId }) ?? 0L);
    }

    private static string BatchNumberOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();

        return test.Database.Scalar(connection,
            "SELECT batch_no FROM batches WHERE product_id = @id ORDER BY id DESC LIMIT 1;",
            new { id = productId })?.ToString() ?? string.Empty;
    }

    private static string ExpiryOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();

        return test.Database.Scalar(connection,
            "SELECT IFNULL(expiry_date, '') FROM batches WHERE product_id = @id ORDER BY id DESC LIMIT 1;",
            new { id = productId })?.ToString() ?? string.Empty;
    }

    private static string ExpiryMonthOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();

        return test.Database.Scalar(connection,
            "SELECT IFNULL(expiry_ym, '') FROM batches WHERE product_id = @id ORDER BY id DESC LIMIT 1;",
            new { id = productId })?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Rows in a table after a step. The table name is a literal at every call site, never a value
    /// typed by a user, so building the small query here is safe and keeps the assertions readable.
    /// </summary>
    private static long Count(TestDatabase test, string fromWhere) =>
        test.Database.Tx(connection => Convert.ToInt64(
            test.Database.Scalar(connection, "SELECT COUNT(1) FROM " + fromWhere + ";") ?? 0L));

    private static long Audits(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action }) ?? 0L);
    }
}
