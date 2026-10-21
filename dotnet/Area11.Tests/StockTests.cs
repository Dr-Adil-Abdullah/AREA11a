using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Write-off and count correction (Spec 19.1, U-30). What must stay true:
///   • the loss is valued at **purchase cost**, never at the selling price;
///   • the stock ledger and the adjustment record both get a line;
///   • writing off more than the book says is **allowed with a warning** - never blocked;
///   • a mistyped entry is corrected by a second line, never by editing the first.
/// </summary>
public sealed class StockTests
{
    private static StockService Stock(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static double BatchQty(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<double>(connection,
            "SELECT IFNULL(SUM(qty_base), 0) FROM batches WHERE product_id = @id AND active = 1;",
            new { id = productId });
    }

    [Fact]
    public void AWriteOff_TakesTheStockOut_AndValuesTheLossAtCost()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000, batchQty: 100);

        var result = Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product,
            QtyEntered = 10,
            Unit = "unit",
            Reason = StockService.ReasonExpired,
            Note = "box fell"
        }, TestData.Owner);

        Assert.True(result.Ok, result.Error);

        // 10 tablets x Rs 200 cost = Rs 2,000 - NOT the Rs 3,000 retail value.
        Assert.Equal(200000L, result.Value!.LossPaisa);
        Assert.Equal(90d, BatchQty(test, product));
    }

    [Fact]
    public void TheLoss_IsWrittenInBothTheLedgerAndTheAdjustmentRecord()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 50);

        var result = Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product,
            QtyEntered = 5,
            Unit = "unit",
            Reason = StockService.ReasonDamaged
        }, TestData.Owner);

        Assert.True(result.Ok, result.Error);

        using var connection = test.Database.Open();

        var movementType = test.Database.QuerySingle<string>(connection,
            "SELECT type FROM stock_movements WHERE product_id = @p ORDER BY id DESC LIMIT 1;", new { p = product });
        Assert.Equal("writeoff", movementType);

        var movementQty = test.Database.QuerySingle<double>(connection,
            "SELECT qty_base FROM stock_movements WHERE product_id = @p ORDER BY id DESC LIMIT 1;", new { p = product });
        Assert.Equal(-5d, movementQty);

        var stored = test.Database.QuerySingle<long>(connection,
            "SELECT value_paisa FROM stock_adjustments WHERE id = @id;", new { id = result.Value!.AdjustmentId });
        Assert.Equal(100000L, stored);
    }

    [Fact]
    public void WritingOffMoreThanTheBook_IsAllowed_WithAWarning()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 3);

        var result = Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product,
            QtyEntered = 10,
            Unit = "unit",
            Reason = StockService.ReasonExpired
        }, TestData.Owner);

        Assert.True(result.Ok, result.Error);          // U-30: never blocks
        Assert.NotEmpty(result.Value!.Warnings);
        Assert.Contains("MINUS", result.Value.Warnings[0].ToUpperInvariant());
        Assert.Equal(-7d, BatchQty(test, product));
    }

    [Fact]
    public void WhenNegativeStockIsSwitchedOff_TheWriteOffIsRefused()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 3);
        test.Settings.Set("stock.allowNegative", false, TestData.Owner);
        test.Settings.Reload();

        var result = Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product,
            QtyEntered = 10,
            Unit = "unit",
            Reason = StockService.ReasonExpired
        }, TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("Negative stock is switched off", result.Error ?? string.Empty);
        Assert.Equal(3d, BatchQty(test, product));
    }

    [Fact]
    public void ACountThatFindsMore_AddsTheStockBack()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);

        var result = Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product,
            QtyEntered = 4,
            Unit = "unit",
            Reason = StockService.ReasonCountExtra
        }, TestData.Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(14d, BatchQty(test, product));
        Assert.Contains("ADDS stock", result.Value!.Warnings[0]);

        using var connection = test.Database.Open();
        Assert.Equal("adjust", test.Database.QuerySingle<string>(connection,
            "SELECT type FROM stock_movements WHERE product_id = @p ORDER BY id DESC LIMIT 1;", new { p = product }));
    }

    [Fact]
    public void OneBox_IsTwentyTablets()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);

        var result = Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product,
            QtyEntered = 1,
            Unit = "box",
            Reason = StockService.ReasonExpired
        }, TestData.Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(80d, BatchQty(test, product));    // 1 box = 2 strips x 10 tablets
        Assert.Equal(400000L, result.Value!.LossPaisa);
    }

    [Fact]
    public void ZeroQuantity_AndAnUnknownReason_AreRefused()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        Assert.False(Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = 0, Unit = "unit", Reason = StockService.ReasonExpired
        }, TestData.Owner).Ok);

        Assert.False(Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = 1, Unit = "unit", Reason = "because"
        }, TestData.Owner).Ok);
    }

    [Fact]
    public void ACashier_CannotWriteOffStock()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var cashier = new User { Id = 5, Name = "Bilal", Role = "cashier" };

        var result = Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = 1, Unit = "unit", Reason = StockService.ReasonLost
        }, cashier);

        Assert.False(result.Ok);
        Assert.Contains("permission", result.Error ?? string.Empty);
        Assert.Equal(100d, BatchQty(test, product));
    }

    [Fact]
    public void AManager_MayWriteOff_AButNeverEditsHistory()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 20);
        var manager = new User { Id = 3, Name = "Kamran", Role = "manager" };

        Assert.True(Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = 2, Unit = "unit", Reason = StockService.ReasonLost
        }, manager).Ok);

        Assert.True(Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = 4, Unit = "unit", Reason = StockService.ReasonCountExtra,
            Note = "correcting the 2 lost"
        }, manager).Ok);

        var history = Stock(test).History();

        Assert.Equal(2, history.Count);                       // both lines stay
        Assert.Equal("out", history[1].Direction);
        Assert.Equal(2d, history[1].QtyBase);
        Assert.Equal("count_extra", history[0].Reason);
        Assert.Equal("in", history[0].Direction);
        Assert.Equal(4d, history[0].QtyBase);
        Assert.Equal("count_extra", history[0].Reason);
        Assert.Equal(22d, BatchQty(test, product));
    }

    [Fact]
    public void TheHistory_KnowsTheMedicineTheBatchAndThePerson()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, name: "Brufen 400mg", batchQty: 30);

        Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = 3, Unit = "strip", Reason = StockService.ReasonExpired
        }, TestData.Owner);

        var row = Stock(test).History()[0];

        Assert.Equal("Brufen 400mg", row.ProductName);
        Assert.Equal("B-1", row.BatchNo);
        Assert.Equal("Owner", row.UserName);
        Assert.Equal("Expired", row.ReasonLabel);
        Assert.Equal("-30", row.SignedQtyLabel);   // 3 strips x 10 tablets
        Assert.Equal(3d, row.QtyEntered ?? 0d);
    }

    [Fact]
    public void TodaysLoss_CountsTheOuts_AndSubtractsTheIns()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);

        Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = 10, Unit = "unit", Reason = StockService.ReasonExpired
        }, TestData.Owner);

        Assert.Equal(200000L, Stock(test).LossTodayPaisa());

        Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = 2, Unit = "unit", Reason = StockService.ReasonCountExtra
        }, TestData.Owner);

        Assert.Equal(160000L, Stock(test).LossTodayPaisa());
    }

    [Fact]
    public void TheWriteOff_IsInTheBlackBox()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        Stock(test).WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = 1, Unit = "unit", Reason = StockService.ReasonDamaged
        }, TestData.Owner);

        using var connection = test.Database.Open();
        var audit = test.Database.QuerySingle<string>(connection,
            "SELECT new_value FROM audit_logs WHERE action = 'writeoff' ORDER BY id DESC LIMIT 1;");

        Assert.NotNull(audit);
        Assert.Contains("Broken", audit);
    }
}
