using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Returns (U-28, U-32). The owner's line is: "more than what was sold, or more refund than what was
/// collected, is impossible". These tests try to break that line from every side, and also check the
/// two things the shelf depends on: goods go back to the same batch, and the original bill is never
/// edited.
/// </summary>
public sealed class ReturnTests
{
    private static (SaleService Sales, ReturnService Returns, int ProductId) Build(TestDatabase test)
    {
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000, batchQty: 100);
        return (TestData.Sales(test), TestData.Returns(test), product);
    }

    private static double BatchQty(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<double>(connection,
            "SELECT COALESCE(SUM(qty_base), 0) FROM batches WHERE product_id = @id;", new { id = productId });
    }

    [Fact]
    public void AFullReturn_PutsTheGoodsBackAndRefundsTheLine()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var sale = sales.Create(TestData.Cart(product, qty: 3), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(97d, BatchQty(test, product));

        var result = returns.Create(TestData.Return(sale.Value!, 3), TestData.Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(sale.Value!.TotalPaisa, result.Value!.RefundPaisa);
        Assert.Equal("RET-0001", result.Value.Code);
        Assert.Equal(100d, BatchQty(test, product));       // the same batch got the tablets back
        Assert.Empty(result.Value.QuarantineCodes);
    }

    [Fact]
    public void APartialReturn_RefundsItsShare_AndLeavesTheRestReturnable()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var sale = sales.Create(TestData.Cart(product, qty: 4), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var first = returns.Create(TestData.Return(sale.Value!, 1), TestData.Owner);
        Assert.True(first.Ok, first.Error);
        Assert.Equal(30000L, first.Value!.RefundPaisa);

        var lines = returns.Lines(sale.Value!.SaleId);
        Assert.True(lines.Ok, lines.Error);
        var line = lines.Value!.Single();
        Assert.Equal(3d, line.RemainingBase);
        Assert.Equal(90000L, line.RemainingPaisa);

        var second = returns.Create(TestData.Return(sale.Value, 3), TestData.Owner);
        Assert.True(second.Ok, second.Error);
        Assert.Equal(90000L, second.Value!.RefundPaisa);
    }

    [Fact]
    public void ReturningMoreThanWasSold_IsRefused_WithTheNumbersInTheMessage()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var sale = sales.Create(TestData.Cart(product, qty: 2), TestData.Owner);
        Assert.True(returns.Create(TestData.Return(sale.Value!, 1), TestData.Owner).Ok);

        var tooMany = returns.Create(TestData.Return(sale.Value!, 2), TestData.Owner);

        Assert.False(tooMany.Ok);
        Assert.Contains("already returned", tooMany.Error);
    }

    [Fact]
    public void AnItemThatIsNotOnTheBill_IsRefused()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);
        var other = TestData.AddProduct(test, name: "Brufen 400mg", retailPaisa: 15000);

        var sale = sales.Create(TestData.Cart(product), TestData.Owner);
        var otherSale = sales.Create(TestData.Cart(other), TestData.Owner);

        var input = TestData.Return(sale.Value!, 1);
        input.SaleId = sale.Value!.SaleId;
        input.Lines[0].SaleItemId = otherSale.Value!.SaleItemIds[0];   // a line of a different bill

        var result = returns.Create(input, TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("not on this bill", result.Error);
    }

    [Fact]
    public void ACreditBill_ReducesTheKhataFirst_NotTheDrawer()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);
        var customer = TestData.AddCustomer(test);

        var cart = TestData.Cart(product, qty: 2, method: "credit", customerId: customer);
        var sale = sales.Create(cart, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(60000L, sale.Value!.DuePaisa);

        var result = returns.Create(TestData.Return(sale.Value, 2), TestData.Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(0L, result.Value!.CashPaisa);            // nothing leaves the drawer
        Assert.Equal(60000L, result.Value.CreditReducedPaisa);

        using var connection = test.Database.Open();
        var balance = test.Database.QuerySingle<long>(connection,
            "SELECT balance_paisa FROM customers WHERE id = @id;", new { id = customer });
        Assert.Equal(0L, balance);
    }

    [Fact]
    public void TheOriginalBill_IsNeverEdited()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var sale = sales.Create(TestData.Cart(product, qty: 2), TestData.Owner);
        var before = Snapshot(test, sale.Value!.SaleId);

        Assert.True(returns.Create(TestData.Return(sale.Value, 2), TestData.Owner).Ok);

        Assert.Equal(before, Snapshot(test, sale.Value.SaleId));
    }

    private static string Snapshot(TestDatabase test, int saleId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<string>(connection, @"
SELECT code || '|' || total_paisa || '|' || paid_paisa || '|' || due_paisa || '|' || status || '|' ||
       (SELECT COUNT(1) FROM sale_items WHERE sale_id = sales.id)
FROM sales WHERE id = @id;", new { id = saleId }) ?? string.Empty;
    }

    [Fact]
    public void AGoodsToQuarantineReturn_GetsItsOwnNumber_AndDoesNotTouchTheShelf()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var sale = sales.Create(TestData.Cart(product, qty: 2), TestData.Owner);
        var before = BatchQty(test, product);

        var result = returns.Create(TestData.Return(sale.Value!, 2, restock: false, reason: "defective"), TestData.Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(60000L, result.Value!.RefundPaisa);
        Assert.Single(result.Value.QuarantineCodes);
        Assert.Equal("Q-0001", result.Value.QuarantineCodes[0]);
        Assert.Equal(before, BatchQty(test, product));        // the shelf did NOT gain stock

        var pending = TestData.Quarantine(test).Pending();
        Assert.True(pending.Ok, pending.Error);
        Assert.Single(pending.Value!);
        Assert.Equal("Q-0001", pending.Value![0].QCode);
        Assert.True(pending.Value[0].IsPending);
    }

    [Fact]
    public void EveryReturn_GetsTheNextNumber()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var first = sales.Create(TestData.Cart(product, qty: 2), TestData.Owner);
        var second = sales.Create(TestData.Cart(product, qty: 2), TestData.Owner);

        Assert.Equal("RET-0001", returns.Create(TestData.Return(first.Value!, 1), TestData.Owner).Value!.Code);
        Assert.Equal("RET-0002", returns.Create(TestData.Return(second.Value!, 1), TestData.Owner).Value!.Code);
    }

    [Fact]
    public void AReturnInsideTheWindow_IsAllowed_OutsideItIsRefused()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var sale = sales.Create(TestData.Cart(product), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        test.Settings.Set("returns.maxDays", 7, TestData.Owner);

        using var connection = test.Database.Open();
        test.Database.Execute(connection,
            "UPDATE sales SET date = datetime('now','localtime','-30 days') WHERE id = @id;",
            new { id = sale.Value!.SaleId });

        var result = returns.Create(TestData.Return(sale.Value, 1), TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("days old", result.Error);
    }

    [Fact]
    public void ARefundBeyondWhatWasCollected_IsRefused()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var sale = sales.Create(TestData.Cart(product), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        // Simulate an old, badly collected bill: only 100 rupees were ever received and the rest is
        // an unassigned debt (no customer). The shop cannot hand back 300 in cash.
        using var connection = test.Database.Open();
        test.Database.Execute(connection,
            "UPDATE sales SET paid_paisa = 10000, due_paisa = 20000, status = 'partial' WHERE id = @id;",
            new { id = sale.Value!.SaleId });

        var result = returns.Create(TestData.Return(sale.Value, 1), TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("cannot be paid back", result.Error);
    }

    [Fact]
    public void AVoidBill_CannotBeReturned()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var sale = sales.Create(TestData.Cart(product), TestData.Owner);

        using var connection = test.Database.Open();
        test.Database.Execute(connection, "UPDATE sales SET status = 'void' WHERE id = @id;",
            new { id = sale.Value!.SaleId });

        var result = returns.Create(TestData.Return(sale.Value, 1), TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("void", result.Error);
    }

    [Fact]
    public void TheReturnReceipt_ShowsTheNumberTheBillTheQCodeAndTheReason()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var sale = sales.Create(TestData.Cart(product, qty: 2), TestData.Owner);
        var result = returns.Create(TestData.Return(sale.Value!, 2, restock: false, reason: "defective"), TestData.Owner);
        Assert.True(result.Ok, result.Error);

        var receipt = TestData.ReturnReceipts(test).Build(result.Value!.Code);

        Assert.True(receipt.Ok, receipt.Error);
        var text = receipt.Value!.PlainText;

        Assert.Contains("RET-0001", text);
        Assert.Contains(sale.Value!.Code, text);
        Assert.Contains("Q-0001", text);
        Assert.Contains("defective", text);
        Assert.Contains("TOTAL REFUND", text);
        Assert.Contains("600.00", text);
        Assert.Contains("Received by", text);

        Assert.All(receipt.Value.Lines, line => Assert.True(line.Text.Length <= receipt.Value.Width));
    }

    [Fact]
    public void TheReturn_IsWrittenToTheBlackBox()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);

        var sale = sales.Create(TestData.Cart(product), TestData.Owner);
        Assert.True(returns.Create(TestData.Return(sale.Value!, 1), TestData.Owner).Ok);

        using var connection = test.Database.Open();
        var rows = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = 'return_create';") ?? 0L);

        Assert.Equal(1L, rows);
    }

    [Fact]
    public void AFullReturnAfterLaterCollection_SplitsCashAndKhataByWhatIsActuallyLeft()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);
        var customer = TestData.AddCustomer(test);

        var sale = sales.Create(TestData.Cart(product, qty: 2, method: "credit", customerId: customer),
            TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        Assert.True(TestData.Ledger(test).Collect(TestData.Owner, customer, 50000, "cash", null).Ok);

        var input = TestData.Return(sale.Value!, 2);
        var quote = returns.Quote(input, TestData.Owner);
        Assert.True(quote.Ok, quote.Error);
        Assert.Equal(60000L, quote.Value!.RefundPaisa);
        Assert.Equal(50000L, quote.Value.CashPaisa);
        Assert.Equal(10000L, quote.Value.CreditReducedPaisa);
        Assert.Equal(60000L, quote.Value.RefundablePaisa);       // cash + khata, never cash alone

        var result = returns.Create(input, TestData.Owner);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(50000L, result.Value!.CashPaisa);
        Assert.Equal(10000L, result.Value.CreditReducedPaisa);

        var receipt = TestData.ReturnReceipts(test).Build(result.Value.Code);
        Assert.True(receipt.Ok, receipt.Error);
        Assert.Contains("Cash refunded", receipt.Value!.PlainText);
        Assert.Contains("Khata reduced", receipt.Value.PlainText);
    }

    [Fact]
    public void AReturnBeforeLaterCollection_LeavesOnlyTheRemainingValueToRefundInCash()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);
        var customer = TestData.AddCustomer(test);

        var sale = sales.Create(TestData.Cart(product, qty: 2, method: "credit", customerId: customer),
            TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var first = returns.Create(TestData.Return(sale.Value!, 1), TestData.Owner);
        Assert.True(first.Ok, first.Error);
        Assert.Equal(30000L, first.Value!.CreditReducedPaisa);
        Assert.Equal(0L, first.Value.CashPaisa);

        // The customer now pays the other Rs 300. Only the one remaining tablet can still be
        // refunded; the earlier khata reduction cannot become refundable a second time.
        Assert.True(TestData.Ledger(test).Collect(TestData.Owner, customer, 30000, "cash", null).Ok);

        var second = returns.Create(TestData.Return(sale.Value!, 1), TestData.Owner);
        Assert.True(second.Ok, second.Error);
        Assert.Equal(30000L, second.Value!.RefundPaisa);
        Assert.Equal(30000L, second.Value.CashPaisa);
        Assert.Equal(0L, second.Value.CreditReducedPaisa);
    }

    [Fact]
    public void ACollectionOnTheOlderBill_DoesNotTurnTheNewerBillsKhataIntoCash()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);
        var customer = TestData.AddCustomer(test);

        var older = sales.Create(TestData.Cart(product, method: "credit", customerId: customer),
            TestData.Owner);
        var newer = sales.Create(TestData.Cart(product, method: "credit", customerId: customer),
            TestData.Owner);
        Assert.True(older.Ok, older.Error);
        Assert.True(newer.Ok, newer.Error);

        // Oldest-first recovery collected the older bill, not both bills and not the newer one.
        Assert.True(TestData.Ledger(test).Collect(TestData.Owner, customer, 30000, "cash", null).Ok);

        var oldReturn = returns.Create(TestData.Return(older.Value!, 1), TestData.Owner);
        Assert.True(oldReturn.Ok, oldReturn.Error);
        Assert.Equal(30000L, oldReturn.Value!.CashPaisa);
        Assert.Equal(0L, oldReturn.Value.CreditReducedPaisa);

        var newReturn = returns.Create(TestData.Return(newer.Value!, 1), TestData.Owner);
        Assert.True(newReturn.Ok, newReturn.Error);
        Assert.Equal(0L, newReturn.Value!.CashPaisa);
        Assert.Equal(30000L, newReturn.Value.CreditReducedPaisa);
    }

    [Fact]
    public void TheFinalRemainder_KeepsItsOwnEnteredQuantityAndExactRemainingPaisa()
    {
        using var test = new TestDatabase();
        var (sales, returns, product) = Build(test);
        var cart = TestData.Cart(product);
        cart.Lines[0].Unit = "strip";
        cart.Lines[0].Quantity = 2;                              // 2 strips = 20 tablets

        var sale = sales.Create(cart, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(600000L, sale.Value!.TotalPaisa);

        var first = returns.Create(TestData.Return(sale.Value!, 10), TestData.Owner);
        Assert.True(first.Ok, first.Error);
        Assert.Equal(300000L, first.Value!.RefundPaisa);

        var remaining = returns.Lines(sale.Value.SaleId).Value!.Single();
        Assert.Equal(1d, remaining.RemainingUnits);

        var second = returns.Create(TestData.Return(sale.Value!, 10), TestData.Owner);
        Assert.True(second.Ok, second.Error);
        Assert.Equal(300000L, second.Value!.RefundPaisa);

        using var connection = test.Database.Open();
        var entered = test.Database.QuerySingle<double>(connection,
            "SELECT qty_entered FROM sale_returns WHERE sale_id = @id ORDER BY id DESC LIMIT 1;",
            new { id = sale.Value.SaleId });
        Assert.Equal(1d, entered);                               // not the original two strips
    }
}
