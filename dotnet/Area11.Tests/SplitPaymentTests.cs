using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Split payment (Spec 8.5.2, 13.3): half cash and half online, or half cash and half credit.
///
/// The rule that matters to the shop's money: **credit is not cash**. A credit part must never be
/// written as a `payments` row, or the drawer would show money the shop never received.
/// </summary>
public sealed class SplitPaymentTests
{
    private static SaleInput Split(int productId, params (string Method, long Amount)[] parts)
    {
        var input = TestData.Cart(productId);
        input.PaymentMethod = "split";
        input.Payments = parts
            .Select(part => new SalePaymentInput { Method = part.Method, AmountPaisa = part.Amount })
            .ToList();

        return input;
    }

    [Fact]
    public void HalfCashHalfOnline_IsOneBillWithTwoPaymentRows()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sales = TestData.Sales(test);

        var input = Split(product, ("cash", 15000), ("online", 15000));
        input.TenderedPaisa = 20000;                 // customer hands over Rs 200 in cash

        var result = sales.Create(input, TestData.Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(30000L, result.Value!.TotalPaisa);
        Assert.Equal(30000L, result.Value.PaidPaisa);
        Assert.Equal(0L, result.Value.DuePaisa);
        Assert.Equal(5000L, result.Value.ChangePaisa);   // change comes off the CASH part only

        using var connection = test.Database.Open();
        var method = test.Database.QuerySingle<string>(connection,
            "SELECT payment_method FROM sales WHERE id = @id;", new { id = result.Value.SaleId });
        Assert.Equal("split", method);

        var rows = test.Database.Query<TestData.PaymentDbRow>(connection,
            "SELECT method, amount_paisa AS amountPaisa FROM payments WHERE sale_id = @id ORDER BY id;",
            new { id = result.Value.SaleId });

        Assert.Equal(2, rows.Count);
        Assert.Equal(15000L, rows.Single(row => row.Method == "cash").AmountPaisa);
        Assert.Equal(15000L, rows.Single(row => row.Method == "online").AmountPaisa);
    }

    [Fact]
    public void CashPlusCredit_PutsTheCreditOnTheKhataAndNeverInTheDrawer()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var customer = TestData.AddCustomer(test);
        var sales = TestData.Sales(test);

        var input = Split(product, ("cash", 10000), ("credit", 20000));
        input.CustomerId = customer;

        var result = sales.Create(input, TestData.Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(10000L, result.Value!.PaidPaisa);
        Assert.Equal(20000L, result.Value.DuePaisa);

        using var connection = test.Database.Open();

        var status = test.Database.QuerySingle<string>(connection,
            "SELECT status FROM sales WHERE id = @id;", new { id = result.Value.SaleId });
        Assert.Equal("partial", status);

        var paymentRows = test.Database.Query<TestData.PaymentDbRow>(connection,
            "SELECT method, amount_paisa AS amountPaisa FROM payments WHERE sale_id = @id;",
            new { id = result.Value.SaleId });

        Assert.Single(paymentRows);
        Assert.Equal("cash", paymentRows[0].Method);

        var balance = test.Database.QuerySingle<long>(connection,
            "SELECT balance_paisa FROM customers WHERE id = @id;", new { id = customer });
        Assert.Equal(20000L, balance);
    }

    [Fact]
    public void PartsThatAddUpToLess_AreFoldedIntoCash()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sales = TestData.Sales(test);

        // The cashier typed only the online part; the rest is collected in cash (Spec 13.3.2).
        var result = sales.Create(Split(product, ("online", 10000)), TestData.Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(30000L, result.Value!.PaidPaisa);

        using var connection = test.Database.Open();
        var rows = test.Database.Query<TestData.PaymentDbRow>(connection,
            "SELECT method, amount_paisa AS amountPaisa FROM payments WHERE sale_id = @id ORDER BY id;",
            new { id = result.Value.SaleId });

        Assert.Equal(2, rows.Count);
        Assert.Equal(20000L, rows.Single(row => row.Method == "cash").AmountPaisa);
    }

    [Fact]
    public void PartsThatAddUpToMore_AreRefused()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sales = TestData.Sales(test);

        var result = sales.Create(Split(product, ("cash", 20000), ("online", 20000)), TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("more than the bill", result.Error);

        using var connection = test.Database.Open();
        var count = Convert.ToInt64(test.Database.Scalar(connection, "SELECT COUNT(1) FROM sales;") ?? 0L);
        Assert.Equal(0L, count);      // nothing half-saved
    }

    [Fact]
    public void ACreditPartWithoutACustomer_IsRefused()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sales = TestData.Sales(test);

        var result = sales.Create(Split(product, ("cash", 10000), ("credit", 20000)), TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("customer", result.Error);
    }

    [Fact]
    public void TheCreditLimit_AppliesToTheCreditPartOnly()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var customer = TestData.AddCustomer(test, limitPaisa: 5000);
        var sales = TestData.Sales(test);

        var tooMuch = Split(product, ("cash", 20000), ("credit", 10000));
        tooMuch.CustomerId = customer;
        Assert.False(sales.Create(tooMuch, TestData.Owner).Ok);

        var fits = Split(product, ("cash", 25000), ("credit", 5000));
        fits.CustomerId = customer;
        var ok = sales.Create(fits, TestData.Owner);

        Assert.True(ok.Ok, ok.Error);
        Assert.Equal(5000L, ok.Value!.DuePaisa);
    }

    [Fact]
    public void CashHanded_IsCheckedAgainstTheCashPart_NotTheWholeBill()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sales = TestData.Sales(test);

        // Cash part is 10,000 paisa; the cashier types 5,000 - not enough for the cash part even
        // though the online part covers the rest.
        var input = Split(product, ("cash", 10000), ("online", 20000));
        input.TenderedPaisa = 5000;

        var result = sales.Create(input, TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("cash part", result.Error);
    }

    [Fact]
    public void TheReceipt_ShowsEveryPartAndTheChange()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sales = TestData.Sales(test);

        var input = Split(product, ("cash", 15000), ("online", 15000));
        input.TenderedPaisa = 20000;
        var sale = sales.Create(input, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var receipt = new ReceiptService(test.Database, test.Settings, test.Log).Build(sale.Value!.SaleId);
        Assert.True(receipt.Ok, receipt.Error);

        var text = receipt.Value!.PlainText;
        Assert.Contains("Cash", text);
        Assert.Contains("Online", text);
        Assert.Contains("150.00", text);
        Assert.Contains("Change", text);
        Assert.Contains("50.00", text);
    }

    [Fact]
    public void APlainSale_StillHasExactlyOnePaymentRow()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sales = TestData.Sales(test);

        var result = sales.Create(TestData.Cart(product), TestData.Owner);

        Assert.True(result.Ok, result.Error);

        using var connection = test.Database.Open();
        var rows = test.Database.Query<TestData.PaymentDbRow>(connection,
            "SELECT method, amount_paisa AS amountPaisa FROM payments WHERE sale_id = @id;",
            new { id = result.Value!.SaleId });

        Assert.Single(rows);
        Assert.Equal("cash", rows[0].Method);
        Assert.Equal(30000L, rows[0].AmountPaisa);
    }
}
