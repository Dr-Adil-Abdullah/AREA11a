using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Hold / park a cart (Spec 8.5.1). The original app kept parked carts in the browser, so they died
/// with the profile; the rebuild keeps them in the database. These tests pin three things:
///   • a parked cart survives a restart (a new service on the same database),
///   • it comes back with every line and quantity exactly as it was,
///   • parking a cart is NOT a sale - no money, no stock movement, no INV number.
/// </summary>
public sealed class HoldCartTests
{
    private static SaleInput TwoLines(int panadol, int brufen) => new()
    {
        PaymentMethod = "cash",
        Lines = new List<SaleLineInput>
        {
            new() { ProductId = panadol, Unit = "strip", Quantity = 2 },
            new() { ProductId = brufen, Unit = "base", Quantity = 7 }
        }
    };

    [Fact]
    public void AParkedCart_AppearsInTheListWithItsTotalAndLineCount()
    {
        using var test = new TestDatabase();
        var panadol = TestData.AddProduct(test);
        var brufen = TestData.AddProduct(test, name: "Brufen 400mg", retailPaisa: 15000);
        var holds = TestData.Holds(test);

        var held = holds.Hold(TwoLines(panadol, brufen), TestData.Owner);
        Assert.True(held.Ok, held.Error);
        Assert.Equal("H-0001", held.Value);

        var list = holds.List(TestData.Owner);
        Assert.True(list.Ok, list.Error);
        Assert.Single(list.Value!);

        // The parked total must be the number the cashier was looking at, so it is compared with
        // the quote rather than with a number typed into the test.
        var quote = TestData.Sales(test).Quote(TwoLines(panadol, brufen), TestData.Owner);
        Assert.True(quote.Ok, quote.Error);

        var summary = list.Value![0];
        Assert.Equal("H-0001", summary.Code);
        Assert.Equal(2, summary.ItemsCount);
        Assert.Equal(quote.Value!.TotalPaisa, summary.TotalPaisa);

        Assert.Equal(1, holds.Count());
    }

    [Fact]
    public void AParkedCart_IsNotASale()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var holds = TestData.Holds(test);

        Assert.True(holds.Hold(TestData.Cart(product), TestData.Owner).Ok);

        using var connection = test.Database.Open();
        Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection, "SELECT COUNT(1) FROM sales;") ?? 0L));
        Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection, "SELECT COUNT(1) FROM payments;") ?? 0L));
        Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection, "SELECT COUNT(1) FROM stock_movements;") ?? 0L));
    }

    [Fact]
    public void TheCartComesBack_WithEveryLineAndQuantity()
    {
        using var test = new TestDatabase();
        var panadol = TestData.AddProduct(test);
        var brufen = TestData.AddProduct(test, name: "Brufen 400mg", retailPaisa: 15000);
        var holds = TestData.Holds(test);
        var cart = TwoLines(panadol, brufen);
        cart.Lines[0].UnitPricePaisa = 25000;  // an owner/manager rate, not a new default

        var held = holds.Hold(cart, TestData.Owner);
        Assert.True(held.Ok, held.Error);

        var summary = holds.List(TestData.Owner).Value!.Single();
        var resumed = holds.Resume(TestData.Owner, summary.Id);

        Assert.True(resumed.Ok, resumed.Error);
        Assert.Equal("H-0001", resumed.Value!.Code);
        Assert.Equal(2, resumed.Value.Lines.Count);

        var strip = resumed.Value.Lines.Single(line => line.ProductId == panadol);
        Assert.Equal("strip", strip.Unit);
        Assert.Equal(2, strip.Quantity);
        Assert.Equal(25000, strip.UnitPricePaisa);

        var tablets = resumed.Value.Lines.Single(line => line.ProductId == brufen);
        Assert.Equal("base", tablets.Unit);
        Assert.Equal(7, tablets.Quantity);
    }

    [Fact]
    public void AResumedCart_LeavesTheList_AndCannotBeTakenTwice()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var holds = TestData.Holds(test);

        var first = holds.Hold(TestData.Cart(product), TestData.Owner);
        Assert.True(first.Ok, first.Error);

        var id = holds.List(TestData.Owner).Value!.Single().Id;

        Assert.True(holds.Resume(TestData.Owner, id).Ok);
        Assert.Empty(holds.List(TestData.Owner).Value!);

        var again = holds.Resume(TestData.Owner, id);
        Assert.False(again.Ok);
        Assert.Contains("already", again.Error ?? string.Empty);
    }

    [Fact]
    public void ADiscardedCart_LeavesTheList()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var holds = TestData.Holds(test);

        Assert.True(holds.Hold(TestData.Cart(product), TestData.Owner).Ok);
        var id = holds.List(TestData.Owner).Value!.Single().Id;

        var discarded = holds.Discard(TestData.Owner, id);

        Assert.True(discarded.Ok, discarded.Error);
        Assert.Empty(holds.List(TestData.Owner).Value!);
        Assert.Equal(0, holds.Count());
    }

    [Fact]
    public void AnEmptyCart_IsNotHeld()
    {
        using var test = new TestDatabase();
        var holds = TestData.Holds(test);

        var result = holds.Hold(new SaleInput(), TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("empty", result.Error ?? string.Empty);
    }

    [Fact]
    public void ACartThatCannotBePriced_IsNotHeld()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var holds = TestData.Holds(test);

        var cart = TestData.Cart(product);
        cart.DiscountPercent = 90;             // a cashier may give 5% - parking it would hide the error
        var cashier = new User { Id = 2, Name = "Bilal", Role = "cashier" };

        var result = holds.Hold(cart, cashier);

        Assert.False(result.Ok);
        Assert.Equal(0, holds.Count());
    }

    [Fact]
    public void AParkedCart_SurvivesARestart()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        var beforeRestart = TestData.Holds(test);
        Assert.True(beforeRestart.Hold(TestData.Cart(product), TestData.Owner).Ok);

        // A brand-new service on the same database is what a restart looks like.
        var afterRestart = TestData.Holds(test);

        Assert.Equal(1, afterRestart.Count());
        Assert.Equal("H-0001", afterRestart.List(TestData.Owner).Value!.Single().Code);

        var resumed = afterRestart.Resume(TestData.Owner, afterRestart.List(TestData.Owner).Value![0].Id);
        Assert.True(resumed.Ok, resumed.Error);
        Assert.Single(resumed.Value!.Lines);
    }

    [Fact]
    public void EveryParkedCart_GetsItsOwnNumber()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var holds = TestData.Holds(test);

        Assert.Equal("H-0001", holds.Hold(TestData.Cart(product), TestData.Owner).Value);
        Assert.Equal("H-0002", holds.Hold(TestData.Cart(product), TestData.Owner).Value);
        Assert.Equal(2, holds.Count());
    }
}
