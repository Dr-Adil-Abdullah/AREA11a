using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Counter-only warnings; the owner's final answer retains D-93 for every role.</summary>
public sealed class CounterAvailabilityTests
{
    private static SaleInput Cart(int product, long rate = 0) => new()
    {
        PaymentMethod = "cash",
        Lines = new List<SaleLineInput>
        {
            new() { ProductId = product, Unit = "base", Quantity = 1, UnitPricePaisa = rate }
        }
    };

    [Fact]
    public void BothCausesAreReportedWithoutSavingAnything()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 0, batchQty: 0);
        var result = TestData.Sales(test).CounterWarnings(Cart(product), TestData.Owner);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value, text => text.Contains("not in stock"));
        Assert.Contains(result.Value, text => text.Contains("no retail price"));
        using var connection = test.Database.Open();
        Assert.Equal(0L, test.Database.QuerySingle<long>(connection, "SELECT COUNT(*) FROM sales;"));
    }

    [Fact]
    public void ExpiredStockIsNotAvailable()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE batches SET expiry_date = '2000-01-01' WHERE product_id = @id;", new { id = product }));
        var result = TestData.Sales(test).CounterWarnings(Cart(product), TestData.Owner);
        Assert.True(result.Ok, result.Error);
        Assert.Single(result.Value!);
        Assert.Contains("not in stock", result.Value![0]);
    }

    [Fact]
    public void StockCheckCombinesRepeatedProductsAndPackUnits()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);
        var input = Cart(product);
        input.Lines.Add(new SaleLineInput { ProductId = product, Unit = "strip", Quantity = 1 });
        var sales = TestData.Sales(test);
        var shortStock = sales.CounterWarnings(input, TestData.Owner);
        Assert.True(shortStock.Ok, shortStock.Error);
        Assert.Single(shortStock.Value!); // 1 tablet + 10 tablets > 10
        input.Lines.RemoveAt(0);
        var exact = sales.CounterWarnings(input, TestData.Owner);
        Assert.True(exact.Ok, exact.Error);
        Assert.Empty(exact.Value!); // exactly one strip is allowed
    }

    [Fact]
    public void OwnerAndManagerCanEnterMissingRateWhenStockExists()
    {
        foreach (var role in new[] { "owner", "manager" })
        {
            using var test = new TestDatabase();
            var product = TestData.AddProduct(test, retailPaisa: 0);
            var user = role == "owner" ? TestData.Owner : TestData.AddUser(test, role);
            if (role == "manager")
                Assert.True(new PricingAllowanceService(test.Database, test.Audit, test.Log)
                    .Save(TestData.Owner, new PricingAllowance { UserId = user.Id, EditRate = true }).Ok);
            var sales = TestData.Sales(test);
            var input = Cart(product, 30000);
            var quote = sales.Quote(input, user);
            Assert.True(quote.Ok, quote.Error);
            var saved = sales.Create(input, user);
            Assert.True(saved.Ok, saved.Error);
        }
    }

    [Fact]
    public void CashierCannotSupplyMissingRateEvenThroughCore()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 0);
        var cashier = TestData.Cashier(test);
        var sales = TestData.Sales(test);
        var quote = sales.Quote(Cart(product, 30000), cashier);
        Assert.False(quote.Ok);
        Assert.Contains("not allowed to edit selling rates", quote.Error ?? string.Empty);
        Assert.False(sales.Create(Cart(product, 30000), cashier).Ok);
    }

    [Fact]
    public void ZeroStockIsBlockedForEveryRoleEvenWithCustomRate()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 0, batchQty: 0);
        var sales = TestData.Sales(test);
        foreach (var role in new[] { "owner", "manager", "cashier" })
        {
            var user = role == "owner" ? TestData.Owner : TestData.AddUser(test, role);
            // Exercise stock denial after a genuine rate allowance, not an earlier pricing refusal.
            Assert.True(new PricingAllowanceService(test.Database, test.Audit, test.Log)
                .Save(TestData.Owner, new PricingAllowance { UserId = user.Id, EditRate = true }).Ok);
            Assert.False(sales.Quote(Cart(product, 30000), user).Ok);
            Assert.False(sales.Create(Cart(product, 30000), user).Ok);
        }
        using var connection = test.Database.Open();
        Assert.Equal(0L, test.Database.QuerySingle<long>(connection, "SELECT COUNT(*) FROM sales;"));
        Assert.Equal(0L, test.Database.QuerySingle<long>(connection, "SELECT COUNT(*) FROM payments;"));
        Assert.Equal(0d, test.Database.QuerySingle<double>(connection,
            "SELECT SUM(qty_base) FROM batches WHERE product_id = @id;", new { id = product }));
    }

    [Fact]
    public void MissingRateStillNeedsPositivePriceAndBelowCostLockStillApplies()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 0);
        var sales = TestData.Sales(test);
        var missing = sales.Quote(Cart(product), TestData.Owner);
        Assert.False(missing.Ok);
        Assert.Contains("no retail rate", missing.Error ?? string.Empty);
        var belowCost = sales.Quote(Cart(product, 10000), TestData.Owner);
        Assert.False(belowCost.Ok);
        Assert.Contains("below purchase cost", belowCost.Error ?? string.Empty);
    }
}
