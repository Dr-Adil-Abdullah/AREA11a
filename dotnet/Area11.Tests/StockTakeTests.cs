using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Stock-take (Spec 19.2). The promises that must hold:
///   • opening freezes the book - later sales do not move the sheet;
///   • counting changes **nothing** in stock until Apply;
///   • Apply sets every counted batch to the counted number, once, owner only;
///   • shortage and excess are valued at the **frozen** cost, not today's;
///   • negative book stock is corrected by the count;
///   • medicine found but never received gets a `GINTI-ST-nnnn` batch.
/// </summary>
public sealed class StockTakeTests
{
    private static StockTakeService Takes(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings),
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static double StockOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<double>(connection,
            "SELECT IFNULL(SUM(qty_base), 0) FROM batches WHERE product_id = @id AND active = 1;",
            new { id = productId });
    }

    private static StockTakeLine LineOf(StockTakeService service, int sessionId, int productId) =>
        service.Sheet(sessionId).First(line => line.ProductId == productId);

    [Fact]
    public void Opening_FreezesTheBook_AndGetsAPermanentNumber()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);

        var opened = Takes(test).Open(TestData.Owner);

        Assert.True(opened.Ok, opened.Error);
        Assert.Equal("ST-0001", opened.Value!.Code);
        Assert.Equal(1, opened.Value.TotalLines);

        // A sale after opening must not change the frozen number.
        TestData.Sales(test).Create(TestData.Cart(product, qty: 5), TestData.Owner);

        Assert.Equal(100d, LineOf(Takes(test), opened.Value.Id, product).SystemQtyBase);
    }

    [Fact]
    public void OpeningWithNothingToCount_IsRefused()
    {
        using var test = new TestDatabase();

        var opened = Takes(test).Open(TestData.Owner);

        Assert.False(opened.Ok);
        Assert.Contains("no live stock", opened.Error ?? string.Empty);
    }

    [Fact]
    public void Counting_WritesTheSheet_ButNeverTouchesStock()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        var line = LineOf(service, opened.Id, product);
        var result = service.Count(TestData.Owner, line.Id, 90, "unit", "two strips short");

        Assert.True(result.Ok, result.Error);
        Assert.Equal(100d, StockOf(test, product));      // stock untouched until Apply

        var counted = LineOf(service, opened.Id, product);
        Assert.Equal(90d, counted.CountedQtyBase);
        Assert.Equal(-10d, counted.DiffQtyBase);
        Assert.Equal(-200000L, counted.ValuePaisa);      // 10 x Rs 200 cost, minus = shortage
        Assert.True(counted.HasDifference);
        Assert.Equal(1, service.History()[0].CountedLines);
    }

    [Fact]
    public void Apply_SetsTheBatchToTheCount_AndWritesAnAdjustMovement()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        service.Count(TestData.Owner, LineOf(service, opened.Id, product).Id, 88, "unit", null);

        var applied = service.Apply(TestData.Owner, opened.Id);

        Assert.True(applied.Ok, applied.Error);
        Assert.Equal(1, applied.Value!.DiffCount);
        Assert.Equal(240000L, applied.Value.ShortPaisa);   // 12 x Rs 200
        Assert.Equal(88d, StockOf(test, product));

        using var connection = test.Database.Open();
        Assert.Equal("adjust", test.Database.QuerySingle<string>(connection,
            "SELECT type FROM stock_movements WHERE product_id = @p ORDER BY id DESC LIMIT 1;", new { p = product }));
        Assert.Equal(-12d, test.Database.QuerySingle<double>(connection,
            "SELECT qty_base FROM stock_movements WHERE product_id = @p ORDER BY id DESC LIMIT 1;", new { p = product }));
        Assert.Equal("applied", test.Database.QuerySingle<string>(connection,
            "SELECT status FROM stock_takes WHERE id = @id;", new { id = opened.Id }));
    }

    [Fact]
    public void AStockTake_CanBeAppliedOnlyOnce()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        service.Count(TestData.Owner, LineOf(service, opened.Id, product).Id, 9, "unit", null);
        Assert.True(service.Apply(TestData.Owner, opened.Id).Ok);

        var again = service.Apply(TestData.Owner, opened.Id);

        Assert.False(again.Ok);
        Assert.Contains("only once", again.Error ?? string.Empty);
        Assert.Equal(9d, StockOf(test, product));
    }

    [Fact]
    public void OnlyTheOwner_CanApply()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        service.Count(TestData.Owner, LineOf(service, opened.Id, product).Id, 8, "unit", null);

        var manager = new User { Id = 3, Name = "Kamran", Role = "manager" };
        var refused = service.Apply(manager, opened.Id);

        Assert.False(refused.Ok);
        Assert.Contains("owner", refused.Error ?? string.Empty);
        Assert.Equal(10d, StockOf(test, product));
    }

    [Fact]
    public void ACashier_CannotEvenOpenASession()
    {
        using var test = new TestDatabase();
        TestData.AddProduct(test);

        var cashier = new User { Id = 5, Name = "Bilal", Role = "cashier" };
        var refused = Takes(test).Open(cashier);

        Assert.False(refused.Ok);
        Assert.Contains("permission", refused.Error ?? string.Empty);
    }

    [Fact]
    public void TheFrozenCost_IsUsedEvenIfTheProductsCostChangedMeanwhile()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, batchQty: 100);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        // A purchase lands while the owner is still counting and raises the cost.
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE products SET cost_paisa = 30000 WHERE id = @id;", new { id = product }));

        service.Count(TestData.Owner, LineOf(service, opened.Id, product).Id, 90, "unit", null);
        var applied = service.Apply(TestData.Owner, opened.Id);

        Assert.True(applied.Ok, applied.Error);
        Assert.Equal(200000L, applied.Value!.ShortPaisa);   // 10 x Rs 200 frozen, not Rs 300
        Assert.Equal(200000L, Math.Abs(LineOf(service, opened.Id, product).ValuePaisa ?? 0L));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(7d)]
    public void NegativeBookStock_IsCorrectedByThePhysicalCount(double physicalCount)
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: -5); // direct legacy fixture, not a new sale
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;
        var minus = new StockMinusService(test.Database, test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

        Assert.Equal(-5d, LineOf(service, opened.Id, product).SystemQtyBase);
        var before = minus.Report(TestData.Owner);
        Assert.True(before.Ok, before.Error);
        Assert.Equal(-5d, Assert.Single(before.Value!.Rows).StockQty);

        var counted = service.Count(TestData.Owner, LineOf(service, opened.Id, product).Id,
            physicalCount, "unit", "the physical shelf count");
        Assert.True(counted.Ok, counted.Error);
        var applied = service.Apply(TestData.Owner, opened.Id);
        Assert.True(applied.Ok, applied.Error);

        Assert.Equal(physicalCount, StockOf(test, product));
        Assert.Empty(minus.Report(TestData.Owner).Value!.Rows);

        using var connection = test.Database.Open();
        Assert.Equal(physicalCount + 5d, test.Database.QuerySingle<double>(connection,
            "SELECT qty_base FROM stock_movements WHERE product_id = @p ORDER BY id DESC LIMIT 1;",
            new { p = product }));
        Assert.Equal("applied", test.Database.QuerySingle<string>(connection,
            "SELECT status FROM stock_takes WHERE id = @id;", new { id = opened.Id }));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFinitePhysicalCounts_AreRefusedWithoutTouchingStock(double physicalCount)
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;
        var line = LineOf(service, opened.Id, product);

        var counted = service.Count(TestData.Owner, line.Id, physicalCount, "unit", null);

        Assert.False(counted.Ok);
        Assert.Equal(10d, StockOf(test, product));
        Assert.False(service.Apply(TestData.Owner, opened.Id).Ok); // no counted row was accepted
        Assert.Equal(10d, StockOf(test, product));

        using var connection = test.Database.Open();
        Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM stock_movements WHERE product_id = @id AND type = 'adjust';",
            new { id = product }) ?? 0L));
    }

    [Fact]
    public void AMedicineFoundOnTheShelf_GetsAGintiBatch()
    {
        using var test = new TestDatabase();

        // The shop must have something real to count, otherwise a session cannot even open.
        TestData.AddProduct(test, name: "Panadol 500mg", batchQty: 10);

        // A medicine the shop sells but never received: no batch at all.
        var product = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO products(name, base_unit, box_strips, strip_tablets, cost_paisa, retail_paisa,
                     vip_paisa, doctor_paisa, reorder_level, active, created_at, updated_at)
VALUES('Disprin', 'tablet', 2, 10, 5000, 8000, 0, 0, 10, 1, @at, @at);",
                new { at = Clock.Now() });

            return (int)test.Database.NextId(connection);
        });

        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        var added = service.AddFound(TestData.Owner, opened.Id, product);
        Assert.True(added.Ok, added.Error);

        var line = LineOf(service, opened.Id, product);
        Assert.Null(line.BatchId);
        Assert.Equal(0d, line.SystemQtyBase);

        service.Count(TestData.Owner, line.Id, 3, "box", "found behind the counter");
        var applied = service.Apply(TestData.Owner, opened.Id);

        Assert.True(applied.Ok, applied.Error);
        Assert.Equal(1, applied.Value!.NewBatches);
        Assert.Equal(60d, StockOf(test, product));          // 3 boxes x 20 tablets

        using var connection = test.Database.Open();
        var batchNo = test.Database.QuerySingle<string>(connection,
            "SELECT batch_no FROM batches WHERE product_id = @id;", new { id = product });
        Assert.Equal("GINTI-ST-0001", batchNo);
    }

    [Fact]
    public void AnUncountedLine_KeepsItsBookStock_AndSaysSo()
    {
        using var test = new TestDatabase();
        var first = TestData.AddProduct(test, name: "Panadol 500mg", batchQty: 10);
        var second = TestData.AddProduct(test, name: "Brufen 400mg", batchQty: 20);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        service.Count(TestData.Owner, LineOf(service, opened.Id, first).Id, 9, "unit", null);

        var applied = service.Apply(TestData.Owner, opened.Id);

        Assert.True(applied.Ok, applied.Error);
        Assert.Equal(9d, StockOf(test, first));
        Assert.Equal(20d, StockOf(test, second));           // never counted, never zeroed
        Assert.Contains("never counted", string.Join(" ", applied.Value!.Warnings));
    }

    [Fact]
    public void ApplyingWithNothingCounted_IsRefused()
    {
        using var test = new TestDatabase();
        TestData.AddProduct(test);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        var applied = service.Apply(TestData.Owner, opened.Id);

        Assert.False(applied.Ok);
        Assert.Contains("Nothing has been counted", applied.Error ?? string.Empty);
    }

    [Fact]
    public void ACancelledSession_ChangesNoStock_AndCannotBeApplied()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        service.Count(TestData.Owner, LineOf(service, opened.Id, product).Id, 4, "unit", null);

        Assert.False(service.Cancel(TestData.Owner, opened.Id, "no").Ok);        // reason too short
        Assert.True(service.Cancel(TestData.Owner, opened.Id, "shop closed early").Ok);
        Assert.Equal(10d, StockOf(test, product));

        var applied = service.Apply(TestData.Owner, opened.Id);
        Assert.False(applied.Ok);
        Assert.Contains("cancelled", applied.Error ?? string.Empty);
    }

    [Fact]
    public void TheSheet_CanShowOnlyTheDifferences()
    {
        using var test = new TestDatabase();
        var first = TestData.AddProduct(test, name: "Panadol 500mg", batchQty: 10);
        var second = TestData.AddProduct(test, name: "Brufen 400mg", batchQty: 20);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        service.Count(TestData.Owner, LineOf(service, opened.Id, first).Id, 10, "unit", null);   // no difference
        service.Count(TestData.Owner, LineOf(service, opened.Id, second).Id, 18, "unit", null);  // difference

        var all = service.Sheet(opened.Id);
        var diffs = service.Sheet(opened.Id, differencesOnly: true);

        Assert.Equal(2, all.Count);
        Assert.Single(diffs);
        Assert.Equal(second, diffs[0].ProductId);
    }

    [Fact]
    public void CountCanBeCleared_BackToNotCounted()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;
        var line = LineOf(service, opened.Id, product);

        Assert.True(service.Count(TestData.Owner, line.Id, 9, "unit", null).Ok);
        Assert.True(service.ClearCount(TestData.Owner, line.Id).Ok);

        var cleared = LineOf(service, opened.Id, product);
        Assert.Null(cleared.CountedQtyBase);
        Assert.Null(cleared.DiffQtyBase);
        Assert.Equal(0, TakeSession(test, opened.Id).CountedLines);
    }

    [Fact]
    public void TheWholeRun_IsInTheBlackBox()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);
        var service = Takes(test);
        var opened = service.Open(TestData.Owner).Value!;

        service.Count(TestData.Owner, LineOf(service, opened.Id, product).Id, 10, "unit", null);
        Assert.True(service.Apply(TestData.Owner, opened.Id).Ok);

        using var connection = test.Database.Open();
        var actions = test.Database.Query<string>(connection,
            "SELECT action FROM audit_logs WHERE action LIKE 'stock_take_%' ORDER BY id;");

        Assert.Equal(new[] { "stock_take_open", "stock_take_apply" }, actions);
    }

    private static StockTakeSession TakeSession(TestDatabase test, int id) =>
        Takes(test).History().First(row => row.Id == id);
}
