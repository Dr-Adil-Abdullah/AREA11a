using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// One place issues every permanent number (F-18, Spec 5.1.12): INV, PINV, RET, PR, SR, ST, Q, H.
///
/// Why this earns its own tests: a document number is the only thing in the shop that can never be
/// repeated and never be re-used. Two bills numbered INV-0007 make the accountant's month impossible
/// to rebuild, and a number that jumps tells the owner something was deleted that was not.
/// </summary>
public sealed class NumberingTests
{
    [Fact]
    public void ASaleStartsAtOneAndPadsToFourDigits()
    {
        using var test = new TestDatabase();
        var numbering = new NumberingService(test.Settings);

        Assert.Equal("INV-0001", Take(test, numbering, "sale"));
        Assert.Equal("INV-0002", Take(test, numbering, "sale"));
        Assert.Equal("INV-0003", Take(test, numbering, "sale"));
    }

    [Fact]
    public void APurchaseHasItsOwnCounter()
    {
        using var test = new TestDatabase();
        var numbering = new NumberingService(test.Settings);

        Assert.Equal("INV-0001", Take(test, numbering, "sale"));
        Assert.Equal("PINV-0001", Take(test, numbering, "purchase"));
        Assert.Equal("INV-0002", Take(test, numbering, "sale"));
    }

    [Fact]
    public void EveryKindOfPaperHasItsOwnPrefix()
    {
        using var test = new TestDatabase();
        var numbering = new NumberingService(test.Settings);

        Assert.Equal("INV-0001", Take(test, numbering, "sale"));
        Assert.Equal("PINV-0001", Take(test, numbering, "purchase"));
        Assert.Equal("RET-0001", Take(test, numbering, "return"));
        Assert.Equal("PR-0001", Take(test, numbering, "provisional"));
        Assert.Equal("SR-0001", Take(test, numbering, "supplierReturn"));
        Assert.Equal("ST-0001", Take(test, numbering, "stockTake"));
        Assert.Equal("Q-0001", Take(test, numbering, "quarantine"));
        Assert.Equal("H-0001", Take(test, numbering, "hold"));
    }

    [Fact]
    public void ThePrefixAndThePaddingComeFromTheShopSettings()
    {
        using var test = new TestDatabase();

        Assert.True(test.Settings.Set("bill.salePrefix", "BILL-", TestData.Owner).Ok);
        Assert.True(test.Settings.Set("bill.numberPadding", 6, TestData.Owner).Ok);
        test.Settings.Reload();

        Assert.Equal("BILL-000001", Take(test, new NumberingService(test.Settings), "sale"));
    }

    [Fact]
    public void ANegativeOrZeroCounterIsTreatedAsOne()
    {
        using var test = new TestDatabase();

        Assert.True(test.Settings.Set("bill.nextSaleNo", 0, TestData.Owner).Ok);
        test.Settings.Reload();

        Assert.Equal("INV-0001", Take(test, new NumberingService(test.Settings), "sale"));
    }

    [Fact]
    public void AnUnknownKindIsRefusedRatherThanInventingANumber()
    {
        using var test = new TestDatabase();
        var numbering = new NumberingService(test.Settings);

        using var connection = test.Database.Open();

        Assert.Throws<ArgumentOutOfRangeException>(() => numbering.Take(connection, "lottery"));
    }

    [Fact]
    public void ARealSaleCarriesTheNumberTheCounterIssued()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);

        var first = TestData.Sales(test).Create(TestData.Cart(productId), TestData.Owner);
        var second = TestData.Sales(test).Create(TestData.Cart(productId), TestData.Owner);

        Assert.True(first.Ok, first.Error);
        Assert.Equal("INV-0001", first.Value!.Code);
        Assert.Equal("INV-0002", second.Value!.Code);
        Assert.Equal(3L, NextSaleNumber(test));
    }

    [Fact]
    public void TheNumberIsNeverHandedOutTwiceEvenAfterARestart()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);

        Assert.True(TestData.Sales(test).Create(TestData.Cart(productId), TestData.Owner).Ok);

        // A restart reads the counter back from the settings table, not from memory.
        test.Settings.Reload();

        var second = TestData.Sales(test).Create(TestData.Cart(productId), TestData.Owner);

        Assert.Equal("INV-0002", second.Value!.Code);
    }

    // ============================== helpers ==============================

    private static string Take(TestDatabase test, NumberingService numbering, string kind) =>
        test.Database.Tx(connection => numbering.Take(connection, kind));

    private static long NextSaleNumber(TestDatabase test)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT value FROM settings WHERE key = 'bill.nextSaleNo';") ?? 0L);
    }
}
