using Area11.Core.Models;
using Xunit;

namespace Area11.Tests;

/// <summary>The success popup must use committed money, not a quote or the next empty cart.</summary>
public sealed class SaleCompletionTests
{
    private static SaleInput One(int product, string method = "cash", long tendered = 0) => new()
    {
        PaymentMethod = method,
        TenderedPaisa = tendered,
        Lines = new List<SaleLineInput>
        {
            new() { ProductId = product, Unit = "base", Quantity = 1 }
        }
    };

    [Fact]
    public void DiscountedCashSale_ReportsBill270Received500AndChange230()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 15000, retailPaisa: 30000);
        var input = One(product, tendered: 50000);
        input.DiscountPercent = 10;
        input.DiscountBasis = "retail"; // This fixture deliberately keeps the Rs270 completion example.
        input.RetailDiscountConfirmed = true;
        var saved = TestData.Sales(test).Create(input, TestData.Owner);
        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(27000L, saved.Value!.TotalPaisa);
        Assert.Equal(50000L, saved.Value.CashReceivedPaisa);
        Assert.Equal(23000L, saved.Value.ChangePaisa);
        Assert.Equal(27000L, saved.Value.PaidPaisa);
        Assert.False(saved.Value.CashReceivedAssumed);
        Assert.Equal(0L, saved.Value.DuePaisa);
        using var connection = test.Database.Open();
        Assert.Equal(1L, test.Database.QuerySingle<long>(connection, "SELECT COUNT(*) FROM sales;"));
        Assert.Equal(27000L, test.Database.QuerySingle<long>(connection,
            "SELECT SUM(amount_paisa) FROM payments WHERE sale_id = @id;", new { id = saved.Value.SaleId }));
    }

    [Fact]
    public void EmptyTender_RetainsExactCashRuleAndLabelsItsAssumption()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var saved = TestData.Sales(test).Create(One(product), TestData.Owner);
        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(30000L, saved.Value!.CashReceivedPaisa);
        Assert.True(saved.Value.CashReceivedAssumed);
        Assert.Equal(0L, saved.Value.ChangePaisa);
    }

    [Fact]
    public void CardAndOnlineNeverReportCashOrCashChange()
    {
        foreach (var method in new[] { "card", "online" })
        {
            using var test = new TestDatabase();
            var product = TestData.AddProduct(test);
            var saved = TestData.Sales(test).Create(One(product, method, 50000), TestData.Owner);
            Assert.True(saved.Ok, saved.Error);
            Assert.Equal(30000L, saved.Value!.PaidPaisa);
            Assert.Equal(0L, saved.Value.CashReceivedPaisa);
            Assert.Equal(0L, saved.Value.ChangePaisa);
            Assert.False(saved.Value.CashReceivedAssumed);
        }
    }

    [Fact]
    public void CreditSaleReportsDueWithoutPretendingCashWasReceived()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var input = One(product, "credit", 50000);
        input.CustomerId = TestData.AddCustomer(test);
        var saved = TestData.Sales(test).Create(input, TestData.Owner);
        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(30000L, saved.Value!.DuePaisa);
        Assert.Equal(0L, saved.Value.PaidPaisa);
        Assert.Equal(0L, saved.Value.CashReceivedPaisa);
        Assert.Equal(0L, saved.Value.ChangePaisa);
        Assert.False(saved.Value.CashReceivedAssumed);
    }

    [Fact]
    public void SplitSaleSeparatesCardCreditAndCashTender()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var input = One(product, "split", 20000);
        input.CustomerId = TestData.AddCustomer(test);
        input.Payments = new List<SalePaymentInput>
        {
            new() { Method = "card", AmountPaisa = 10000 },
            new() { Method = "credit", AmountPaisa = 5000 }
        }; // Rs 150 remaining is cash. Rs 200 handed over means Rs 50 change.
        var saved = TestData.Sales(test).Create(input, TestData.Owner);
        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(30000L, saved.Value!.TotalPaisa);
        Assert.Equal(25000L, saved.Value.PaidPaisa);
        Assert.Equal(5000L, saved.Value.DuePaisa);
        Assert.Equal(20000L, saved.Value.CashReceivedPaisa);
        Assert.Equal(5000L, saved.Value.ChangePaisa);
        Assert.False(saved.Value.CashReceivedAssumed);
    }

    [Fact]
    public void InsufficientTenderStillRefusesBeforeAnySuccessResult()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var saved = TestData.Sales(test).Create(One(product, tendered: 10000), TestData.Owner);
        Assert.False(saved.Ok);
        Assert.Null(saved.Value);
        using var connection = test.Database.Open();
        Assert.Equal(0L, test.Database.QuerySingle<long>(connection, "SELECT COUNT(*) FROM sales;"));
        Assert.Equal(0L, test.Database.QuerySingle<long>(connection, "SELECT COUNT(*) FROM payments;"));
    }
}
