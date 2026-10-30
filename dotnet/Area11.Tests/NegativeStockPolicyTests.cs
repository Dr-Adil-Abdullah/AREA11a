using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// D-93: complete negative-stock retirement for NEW work. Historical minus rows are seeded directly
/// as legacy fixtures; no test is allowed to create them by overselling through a public service.
/// The suite proves cumulative carts, shared batches, exact-zero success, atomic rollback, document
/// numbering, stock-only PR cancellation, and the continued readability of old sales and returns.
/// </summary>
public sealed class NegativeStockPolicyTests
{
    private static SaleInput Cart(int productId, params double[] quantities) => new()
    {
        PaymentMethod = "cash",
        TenderedPaisa = 0,
        Lines = quantities.Select(quantity => new SaleLineInput
        {
            ProductId = productId,
            Unit = "base",
            Quantity = quantity
        }).ToList()
    };

    private static StockService Stock(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static SupplierLedgerService Suppliers(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings), test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static StockTakeService Takes(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings),
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static StockMinusService Minus(TestDatabase test) =>
        new(test.Database, test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static long ScalarLong(TestDatabase test, string sql, object? parameters = null)
    {
        using var connection = test.Database.Open();
        return Convert.ToInt64(test.Database.Scalar(connection, sql, parameters) ?? 0L);
    }

    private static string ScalarText(TestDatabase test, string sql, object? parameters = null)
    {
        using var connection = test.Database.Open();
        return Convert.ToString(test.Database.Scalar(connection, sql, parameters)) ?? string.Empty;
    }

    private static double BatchQty(TestDatabase test, int batchId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<double>(connection,
            "SELECT qty_base FROM batches WHERE id = @id;", new { id = batchId });
    }

    private static double ProductQty(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<double>(connection,
            "SELECT IFNULL(SUM(qty_base), 0) FROM batches WHERE product_id = @id AND active = 1;",
            new { id = productId });
    }

    private static int NewestBatch(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<int>(connection,
            "SELECT id FROM batches WHERE product_id = @id ORDER BY id DESC LIMIT 1;", new { id = productId });
    }

    private static int AddBatch(
        TestDatabase test,
        int productId,
        string batchNo,
        double quantity,
        string expiry = "2028-12-31") =>
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    is_sample, active, created_at)
VALUES(@productId, @batchNo, @expiry, @expiryYm, @quantity, 20000, 30000, 0, 1, @at);",
                new
                {
                    productId,
                    batchNo,
                    expiry,
                    expiryYm = expiry[..7],
                    quantity,
                    at = Clock.Now()
                });

            return (int)test.Database.NextId(connection);
        });

    private static void BuyForty(TestDatabase test, int supplierId, int productId)
    {
        var purchase = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = supplierId,
            SupplierInvoiceNo = "D93-SUPPLIER",
            PaidPaisa = 0,
            Lines = new List<PurchaseLineInput>
            {
                new()
                {
                    ProductId = productId,
                    Unit = "box",
                    Quantity = 2, // 2 boxes x 20 tablets = 40 base units
                    BatchNo = "D93-B1",
                    Expiry = "2027-06",
                    CostPaisa = 20000,
                    RetailPaisa = 30000
                }
            }
        }, TestData.Owner);

        Assert.True(purchase.Ok, purchase.Error);
    }

    [Fact]
    public void RepeatedCartLines_FailCumulativelyBeforeNumbering_ThenExactZeroSucceeds()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);
        var sales = TestData.Sales(test);
        var invalid = Cart(product, 6, 5); // each line fits alone; together they need 11

        var quote = sales.Quote(invalid, TestData.Owner);
        Assert.False(quote.Ok);
        Assert.Contains("11", quote.Error ?? string.Empty);
        Assert.Contains("book total 10", quote.Error ?? string.Empty);
        Assert.DoesNotContain("physical stock-take", quote.Error ?? string.Empty);

        var failed = sales.Create(invalid, TestData.Owner);
        Assert.False(failed.Ok);
        Assert.Contains("across the cart", failed.Error ?? string.Empty);
        Assert.Equal(10d, ProductQty(test, product));
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM sales;"));
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM payments;"));
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM stock_movements WHERE type = 'sale';"));

        var valid = sales.Create(Cart(product, 10), TestData.Owner);
        Assert.True(valid.Ok, valid.Error);
        Assert.Equal("INV-0001", valid.Value!.Code); // the failed attempt consumed no number
        Assert.Equal(0d, ProductQty(test, product));
    }

    [Fact]
    public void ExactZeroAcrossTwoBatches_UsesFefo_AndTheNextUnitIsRefused()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 4);
        var first = NewestBatch(test, product);
        var second = AddBatch(test, product, "D93-SECOND", 3);
        var sales = TestData.Sales(test);

        var saved = sales.Create(Cart(product, 7), TestData.Owner);

        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(2, saved.Value!.SaleItemIds.Count);
        Assert.Equal(0d, BatchQty(test, first));
        Assert.Equal(0d, BatchQty(test, second));
        Assert.Equal(0d, ProductQty(test, product));

        var next = sales.Create(Cart(product, 1), TestData.Owner);
        Assert.False(next.Ok);
        Assert.Contains("below zero", next.Error ?? string.Empty);
        Assert.Equal(1L, ScalarLong(test, "SELECT COUNT(1) FROM sales;"));
        Assert.Equal(0d, ProductQty(test, product));

        // REAL quantities must not turn binary residue into a false oversell. The strict guarded
        // writes still leave exactly zero; no negative fallback or silent clamping is involved.
        var fractional = TestData.AddProduct(test, name: "Fractional medicine", batchQty: 0.3);
        var exactFraction = sales.Create(Cart(fractional, 0.1, 0.2), TestData.Owner);
        Assert.True(exactFraction.Ok, exactFraction.Error);
        Assert.Equal(0d, ProductQty(test, fractional));

        // A tiny real shortage is not floating-point residue: never accept a partially allocated
        // line and charge for quantity that was not actually taken from the batch.
        var tiny = TestData.AddProduct(test, name: "Tiny fractional medicine", batchQty: 0.00000005);
        var tinyShortage = sales.Create(Cart(tiny, 0.0000001), TestData.Owner);
        Assert.False(tinyShortage.Ok);
        Assert.Equal(0.00000005d, ProductQty(test, tiny));
    }

    [Fact]
    public void PositiveBatchBesideLegacyMinus_CannotHideTheRealProductShortage()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 5);
        var positive = NewestBatch(test, product);
        var negative = AddBatch(test, product, "LEGACY-MINUS", -3);
        var sales = TestData.Sales(test);
        var invalid = Cart(product, 3); // positive batch has 5, but the product's real total is only 2

        var quote = sales.Quote(invalid, TestData.Owner);
        Assert.False(quote.Ok);
        Assert.Contains("book total 2", quote.Error ?? string.Empty);
        Assert.Contains("physical stock-take", quote.Error ?? string.Empty);

        var failed = sales.Create(invalid, TestData.Owner);
        Assert.False(failed.Ok);
        Assert.Contains("physical stock-take", failed.Error ?? string.Empty);
        Assert.Equal(5d, BatchQty(test, positive));
        Assert.Equal(-3d, BatchQty(test, negative)); // history was neither used nor silently zeroed
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM sales;"));

        var valid = sales.Create(Cart(product, 2), TestData.Owner);
        Assert.True(valid.Ok, valid.Error);
        Assert.Equal("INV-0001", valid.Value!.Code);
        Assert.Equal(3d, BatchQty(test, positive));
        Assert.Equal(-3d, BatchQty(test, negative));
        Assert.Equal(0d, ProductQty(test, product));
    }

    [Fact]
    public void Fefo_DoesNotReuseSellableStockReservedByAnEarlierCartLine()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 5);
        var sellable = NewestBatch(test, product);
        var expired = AddBatch(test, product, "EXPIRED-OLD", 5, "2020-01-31");
        var sales = TestData.Sales(test);
        var invalid = Cart(product, 4, 4); // book total 10, but only 5 is sellable

        var failed = sales.Create(invalid, TestData.Owner);

        Assert.False(failed.Ok);
        Assert.Contains("earlier cart lines", failed.Error ?? string.Empty);
        Assert.Equal(5d, BatchQty(test, sellable));
        Assert.Equal(5d, BatchQty(test, expired));
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM sales;"));

        var valid = sales.Create(Cart(product, 4, 1), TestData.Owner);
        Assert.True(valid.Ok, valid.Error);
        Assert.Equal("INV-0001", valid.Value!.Code);
        Assert.Equal(0d, BatchQty(test, sellable));
        Assert.Equal(5d, BatchQty(test, expired));
    }

    [Fact]
    public void GoodsDrawing_CanReachExactZero_ButCannotTakeAnotherUnit()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 2);
        var cash = TestData.CashOut(test);

        var saved = cash.AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "goods", ProductId = product, QtyEntered = 2, Unit = "unit"
        });

        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(0d, saved.Value!.StockAfterBase);
        Assert.Equal(0d, ProductQty(test, product));

        var refused = cash.AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "goods", ProductId = product, QtyEntered = 1, Unit = "unit"
        });

        Assert.False(refused.Ok);
        Assert.Contains("below zero", refused.Error ?? string.Empty);
        Assert.Equal(1L, ScalarLong(test, "SELECT COUNT(1) FROM owner_drawings;"));
        Assert.Equal(1L, ScalarLong(test, "SELECT COUNT(1) FROM stock_movements WHERE type = 'drawing';"));
        Assert.Equal(0d, ProductQty(test, product));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteQuantities_AreRefusedBeforeMoneyOrStockRows(double quantity)
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);

        var sales = TestData.Sales(test);
        var sale = sales.Create(Cart(product, quantity), TestData.Owner);
        Assert.False(sale.Ok);

        var overflow = sales.Create(new SaleInput
        {
            PaymentMethod = "cash",
            Lines = new List<SaleLineInput>
            {
                new() { ProductId = product, Unit = "box", Quantity = double.MaxValue }
            }
        }, TestData.Owner);
        Assert.False(overflow.Ok);

        var writeOff = Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = quantity, Unit = "unit", Reason = StockService.ReasonDamaged
        }, TestData.Owner);
        Assert.False(writeOff.Ok);

        var drawing = TestData.CashOut(test).AddDrawing(TestData.Owner, new DrawingInput
        {
            Type = "goods", ProductId = product, QtyEntered = quantity, Unit = "unit"
        });
        Assert.False(drawing.Ok);

        Assert.Equal(10d, ProductQty(test, product));
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM sales;"));
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM stock_adjustments;"));
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM owner_drawings;"));
    }

    [Fact]
    public void SupplierReturn_RepeatedLinesRollBackEverything_AndKeepSrNumber()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test);
        BuyForty(test, supplier, product);
        var batch = NewestBatch(test, product);
        var ledger = Suppliers(test);

        SupplierReturnInput Input(double first, double second) => new()
        {
            SupplierId = supplier,
            Reason = SupplierLedgerService.ReasonDamaged,
            Lines = new List<SupplierReturnLineInput>
            {
                new() { ProductId = product, BatchId = batch, QtyEntered = first, Unit = "unit" },
                new() { ProductId = product, BatchId = batch, QtyEntered = second, Unit = "unit" }
            }
        };

        var failed = ledger.Return(TestData.Owner, Input(30, 20));

        Assert.False(failed.Ok);
        Assert.Contains("below zero", failed.Error ?? string.Empty);
        Assert.Equal(40d, BatchQty(test, batch)); // the first 30 were rolled back too
        Assert.Equal(800000L, ScalarLong(test,
            "SELECT balance_paisa FROM suppliers WHERE id = @id;", new { id = supplier }));
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM supplier_returns;"));
        Assert.Equal(0L, ScalarLong(test,
            "SELECT COUNT(1) FROM stock_movements WHERE type = 'supplier_return';"));

        var valid = ledger.Return(TestData.Owner, Input(20, 20));
        Assert.True(valid.Ok, valid.Error);
        Assert.Equal("SR-0001", valid.Value!.Code);
        Assert.Equal(0d, BatchQty(test, batch));
        Assert.Equal(2L, ScalarLong(test, "SELECT COUNT(1) FROM supplier_return_items;"));
    }

    [Fact]
    public void PrCancellation_RollsBackUntilTheSameBatchIsCounted_ThenAddsNoPayment()
    {
        using var test = new TestDatabase();
        TestData.EnsureDrawerOpen(test);
        var product = TestData.AddProduct(test, batchQty: 5);
        var provisionals = TestData.Provisionals(test);
        var created = provisionals.Create(
            TestData.RushReturn(product, qty: 5, unit: "base", refundPaisa: 50000), TestData.Owner);
        Assert.True(created.Ok, created.Error);

        var provisionalId = created.Value!.Id;
        var provisionalBatch = (int)ScalarLong(test,
            "SELECT batch_id FROM provisional_items WHERE provisional_id = @id;", new { id = provisionalId });
        Assert.Equal(10d, ProductQty(test, product));

        var sale = TestData.Sales(test).Create(Cart(product, 10), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(0d, ProductQty(test, product));
        Assert.Equal(0d, BatchQty(test, provisionalBatch));

        // Product-total stock alone must not authorize removal from a different batch.
        var unrelated = AddBatch(test, product, "UNRELATED-RECEIPT", 5);
        Assert.Equal(5d, ProductQty(test, product));
        var paymentsBefore = ScalarLong(test, "SELECT COUNT(1) FROM payments;");

        var failed = provisionals.Cancel(provisionalId, "typed twice", TestData.Owner);
        Assert.False(failed.Ok);
        Assert.Contains("batch", failed.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("pending", ScalarText(test,
            "SELECT status FROM provisional_returns WHERE id = @id;", new { id = provisionalId }));
        Assert.Equal(0L, ScalarLong(test,
            "SELECT COUNT(1) FROM stock_movements WHERE type = 'return_out' AND ref_type = 'provisional_return' AND ref_id = @id;",
            new { id = provisionalId }));
        Assert.Equal(1L, ScalarLong(test,
            "SELECT COUNT(1) FROM provisional_report_lines WHERE provisional_id = @id;",
            new { id = provisionalId }));
        Assert.Equal(paymentsBefore, ScalarLong(test, "SELECT COUNT(1) FROM payments;"));

        // Receiving an unrelated batch would not be enough. Count the original PR batch back to 5.
        var takes = Takes(test);
        var opened = takes.Open(TestData.Owner);
        Assert.True(opened.Ok, opened.Error);
        var line = takes.Sheet(opened.Value!.Id).Single(row => row.BatchId == provisionalBatch);
        Assert.True(takes.Count(TestData.Owner, line.Id, 5, "unit", "found the original PR goods").Ok);
        var counted = takes.Apply(TestData.Owner, opened.Value.Id);
        Assert.True(counted.Ok, counted.Error);
        Assert.Equal(5d, BatchQty(test, provisionalBatch));

        var cancelled = provisionals.Cancel(provisionalId, "typed twice", TestData.Owner);
        Assert.True(cancelled.Ok, cancelled.Error);
        Assert.Equal(0d, BatchQty(test, provisionalBatch));
        Assert.Equal(5d, BatchQty(test, unrelated));
        Assert.Equal(5d, ProductQty(test, product));
        Assert.Equal("cancelled", ScalarText(test,
            "SELECT status FROM provisional_returns WHERE id = @id;", new { id = provisionalId }));
        Assert.Equal(1L, ScalarLong(test,
            "SELECT COUNT(1) FROM stock_movements WHERE type = 'return_out' AND ref_type = 'provisional_return' AND ref_id = @id;",
            new { id = provisionalId }));
        Assert.Equal(0L, ScalarLong(test,
            "SELECT COUNT(1) FROM provisional_report_lines WHERE provisional_id = @id;",
            new { id = provisionalId }));
        Assert.Equal(paymentsBefore, ScalarLong(test, "SELECT COUNT(1) FROM payments;"));

        // An incomplete legacy item must not bypass the same-batch guard by having a NULL batch id.
        var legacyProduct = TestData.AddProduct(test, name: "Legacy PR without batch", batchQty: 2);
        var legacyCreated = provisionals.Create(
            TestData.RushReturn(legacyProduct, qty: 1, unit: "base", refundPaisa: 0), TestData.Owner);
        Assert.True(legacyCreated.Ok, legacyCreated.Error);
        var legacyId = legacyCreated.Value!.Id;
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE provisional_items SET batch_id = NULL WHERE provisional_id = @id;", new { id = legacyId }));

        var missingBatch = provisionals.Cancel(legacyId, "legacy incomplete row", TestData.Owner);
        Assert.False(missingBatch.Ok);
        Assert.Contains("original batch is missing", missingBatch.Error ?? string.Empty);
        Assert.Equal("pending", ScalarText(test,
            "SELECT status FROM provisional_returns WHERE id = @id;", new { id = legacyId }));
        Assert.Equal(0L, ScalarLong(test,
            "SELECT COUNT(1) FROM stock_movements WHERE type = 'return_out' AND ref_type = 'provisional_return' AND ref_id = @id;",
            new { id = legacyId }));
        Assert.Equal(3d, ProductQty(test, legacyProduct));
    }

    [Fact]
    public void LegacyNegativeStock_DoesNotBreakHistoricalReturnsCatalogOrReports()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 5);
        var sale = TestData.Sales(test).Create(Cart(product, 3), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        // Simulate a database written by an older version: the sale stays immutable, only its batch
        // carries the legacy minus. D-93 has no migration that rewrites this quantity to zero.
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE batches SET qty_base = -3 WHERE product_id = @id;", new { id = product }));

        var returned = TestData.Returns(test).Create(
            TestData.Return(sale.Value!, 1, restock: true), TestData.Owner);
        Assert.True(returned.Ok, returned.Error);
        Assert.Equal(-2d, ProductQty(test, product)); // a historical return is still an increment

        var catalog = new ProductService(test.Database, test.Audit, test.Log).Get(product);
        Assert.True(catalog.Ok, catalog.Error);
        Assert.Equal(-2d, catalog.Value!.StockBase);

        var minus = Minus(test).Report(TestData.Owner);
        Assert.True(minus.Ok, minus.Error);
        var row = Assert.Single(minus.Value!.Rows);
        Assert.Equal(product, row.ProductId);
        Assert.Equal(-2d, row.StockQty);
        Assert.Equal(-40000L, row.ValuePaisa);

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner);
        Assert.True(report.Ok, report.Error);
        Assert.Equal(90000L, report.Value!.Totals.SalesPaisa);
        Assert.Equal(30000L, report.Value.Totals.RefundPaisa);
        Assert.Equal(60000L, report.Value.Totals.NetSalesPaisa);
        var top = Assert.Single(report.Value.TopByQty);
        Assert.Equal(2d, top.QtyBase); // REAL quantity: 3 sold - 1 returned, never integer-cast
        Assert.Equal(1L, ScalarLong(test, "SELECT COUNT(1) FROM sale_returns;"));
    }
}
