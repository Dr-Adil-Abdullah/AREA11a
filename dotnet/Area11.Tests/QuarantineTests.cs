using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Quarantine (U-34). Goods that came back but must not go straight to the shelf. Only one of the
/// three outcomes puts stock back, the decision can be made once, and only by the owner or a manager.
/// </summary>
public sealed class QuarantineTests
{
    private static (SaleResult Sale, ReturnResult Returned, int ProductId) ParkInQuarantine(TestDatabase test)
    {
        var product = TestData.AddProduct(test, batchQty: 100);
        var sales = TestData.Sales(test);

        var sale = sales.Create(TestData.Cart(product, qty: 3), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var returned = TestData.Returns(test).Create(
            TestData.Return(sale.Value!, 3, restock: false, reason: "defective"), TestData.Owner);
        Assert.True(returned.Ok, returned.Error);

        return (sale.Value!, returned.Value!, product);
    }

    private static double Stock(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<double>(connection,
            "SELECT COALESCE(SUM(qty_base), 0) FROM batches WHERE product_id = @id;", new { id = productId });
    }

    [Fact]
    public void GoodsInQuarantine_HaveAPermanentNumber_AndWaitForADecision()
    {
        using var test = new TestDatabase();
        var (_, returned, _) = ParkInQuarantine(test);

        Assert.Equal("Q-0001", returned.QuarantineCodes.Single());

        var quarantine = TestData.Quarantine(test);
        Assert.Equal(1, quarantine.PendingCount());
        Assert.Single(quarantine.Pending().Value!);
    }

    [Fact]
    public void BackInStock_PutsTheGoodsOnTheShelf_AndClearsTheList()
    {
        using var test = new TestDatabase();
        var (_, returned, product) = ParkInQuarantine(test);
        var quarantine = TestData.Quarantine(test);

        Assert.Equal(97d, Stock(test, product));       // still sold, still not on the shelf

        var result = quarantine.Dispose(TestData.Owner, returned.ReturnId, QuarantineService.Restocked, "checked, fine");

        Assert.True(result.Ok, result.Error);
        Assert.Equal(100d, Stock(test, product));
        Assert.Equal(0, quarantine.PendingCount());
        Assert.Single(quarantine.History().Value!);
        Assert.Equal("restocked", quarantine.History().Value![0].Disposition);
    }

    [Fact]
    public void DamagedOrExpired_DestroysTheGoods_WithoutTouchingTheStock()
    {
        using var test = new TestDatabase();
        var (_, returned, product) = ParkInQuarantine(test);
        var quarantine = TestData.Quarantine(test);

        var result = quarantine.Dispose(TestData.Owner, returned.ReturnId, QuarantineService.Expired, "wet box");

        Assert.True(result.Ok, result.Error);
        Assert.Equal(97d, Stock(test, product));       // they were never in stock; nothing is added
        Assert.Equal("expired", quarantine.History().Value![0].Disposition);
    }

    [Fact]
    public void DamagedOrExpired_NeedsANote()
    {
        using var test = new TestDatabase();
        var (_, returned, product) = ParkInQuarantine(test);

        var result = TestData.Quarantine(test).Dispose(TestData.Owner, returned.ReturnId, QuarantineService.Expired, "  ");

        Assert.False(result.Ok);
        Assert.Contains("note", result.Error);
        Assert.Equal(97d, Stock(test, product));
    }

    [Fact]
    public void SoldOn_ReentersStockSoTheNormalBillTakesItOut()
    {
        using var test = new TestDatabase();
        var (_, returned, product) = ParkInQuarantine(test);

        var result = TestData.Quarantine(test).Dispose(TestData.Owner, returned.ReturnId, QuarantineService.Sold, "sold to a walk-in");

        Assert.True(result.Ok, result.Error);
        Assert.Equal(100d, Stock(test, product));
    }

    [Fact]
    public void ADecisionCannotBeChanged()
    {
        using var test = new TestDatabase();
        var (_, returned, _) = ParkInQuarantine(test);
        var quarantine = TestData.Quarantine(test);

        Assert.True(quarantine.Dispose(TestData.Owner, returned.ReturnId, QuarantineService.Expired, "broken").Ok);

        var again = quarantine.Dispose(TestData.Owner, returned.ReturnId, QuarantineService.Restocked, "changed my mind");

        Assert.False(again.Ok);
        Assert.Contains("already has a decision", again.Error);

        Assert.Equal("expired", quarantine.History().Value![0].Disposition);
    }

    [Fact]
    public void OnlyTheOwnerOrAManager_Decides()
    {
        using var test = new TestDatabase();
        var (_, returned, product) = ParkInQuarantine(test);

        var cashier = new User { Id = 5, Name = "Bilal", Role = "cashier" };
        var result = TestData.Quarantine(test).Dispose(cashier, returned.ReturnId, QuarantineService.Restocked, "ok");

        Assert.False(result.Ok);
        Assert.Contains("owner or a manager", result.Error);
        Assert.Equal(1, TestData.Quarantine(test).PendingCount());
        Assert.Equal(97d, Stock(test, product));
    }

    [Fact]
    public void AnUnknownOutcome_IsRefused()
    {
        using var test = new TestDatabase();
        var (_, returned, _) = ParkInQuarantine(test);

        var result = TestData.Quarantine(test).Dispose(TestData.Owner, returned.ReturnId, "burn it", "note");

        Assert.False(result.Ok);
        Assert.Contains("back in stock", result.Error);
    }

    [Fact]
    public void EveryDecision_IsWrittenToTheBlackBox()
    {
        using var test = new TestDatabase();
        var (_, returned, _) = ParkInQuarantine(test);
        var quarantine = TestData.Quarantine(test);

        Assert.True(quarantine.Dispose(TestData.Owner, returned.ReturnId, QuarantineService.Expired, "expired batch").Ok);

        using var connection = test.Database.Open();
        var rows = test.Database.Query<string>(connection,
            "SELECT action FROM audit_logs WHERE action LIKE 'quarantine_%';");

        Assert.Contains("quarantine_expired", rows);
    }
}
