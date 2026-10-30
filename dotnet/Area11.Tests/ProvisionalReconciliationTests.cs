using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// A PR creates the stock/payment event once. Linking only allocates that event to the original
/// sale lines; these tests prove that RET then sees the same quantities and paisa, including old
/// links, split lines and imperfect refunds.
/// </summary>
public sealed class ProvisionalReconciliationTests
{
    [Fact]
    public void ExactLink_ConsumesQuantityAndValue_WithoutAnotherStockOrPaymentEvent()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 100);
        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 3), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var rush = TestData.Provisionals(test).Create(
            TestData.RushReturn(product, qty: 1, unit: "base", refundPaisa: 30000), TestData.Owner);
        Assert.True(rush.Ok, rush.Error);

        var stockBefore = StockOf(test, product);
        var saleBefore = SaleSnapshot(test, sale.Value!.SaleId);
        var paymentsBefore = ScalarLong(test, "SELECT COUNT(1) FROM payments;");
        var movementsBefore = ScalarLong(test, "SELECT COUNT(1) FROM stock_movements;");

        var linked = TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value.Code, TestData.Owner);

        Assert.True(linked.Ok, linked.Error);
        Assert.Empty(linked.Warnings);
        Assert.Equal(stockBefore, StockOf(test, product));
        Assert.Equal(saleBefore, SaleSnapshot(test, sale.Value.SaleId));
        Assert.Equal(paymentsBefore, ScalarLong(test, "SELECT COUNT(1) FROM payments;"));
        Assert.Equal(movementsBefore, ScalarLong(test, "SELECT COUNT(1) FROM stock_movements;"));

        var row = TestData.Provisionals(test).Get(rush.Value.Code)!;
        Assert.False(row.NeedsReconciliation);
        Assert.False(row.HasLinkVariance);
        Assert.Equal(30000L, row.LinkedExpectedPaisa.GetValueOrDefault());
        Assert.Equal(0L, row.LinkedUnallocatedPaisa.GetValueOrDefault());
        Assert.False(TestData.Provisionals(test).Link(rush.Value.Id, sale.Value.Code, TestData.Owner).Ok);
        Assert.Equal(1L, ScalarLong(test, "SELECT COUNT(1) FROM provisional_return_allocations;"));

        using (var connection = test.Database.Open())
        {
            var allocation = test.Database.QuerySingle<AllocationRow>(connection, @"
SELECT sale_item_id AS saleItemId, qty_base AS qtyBase,
       expected_refund_paisa AS expectedPaisa, refund_paisa AS refundPaisa
FROM provisional_return_allocations;");
            Assert.NotNull(allocation);
            Assert.Equal(TestData.SaleItemIdOf(sale.Value), allocation!.SaleItemId);
            Assert.Equal(1d, allocation.QtyBase);
            Assert.Equal(30000L, allocation.ExpectedPaisa);
            Assert.Equal(30000L, allocation.RefundPaisa);
        }

        var remaining = TestData.Returns(test).Lines(sale.Value.SaleId);
        Assert.True(remaining.Ok, remaining.Error);
        var line = Assert.Single(remaining.Value!);
        Assert.Equal(1d, line.AlreadyReturnedBase);
        Assert.Equal(30000L, line.AlreadyRefundedPaisa);
        Assert.Equal(2d, line.RemainingBase);
        Assert.Equal(60000L, line.RemainingPaisa);

        // Only the other two tablets and their Rs 600 remain. The linked PR is not refunded again.
        var normal = TestData.Returns(test).Create(TestData.Return(sale.Value, 2), TestData.Owner);
        Assert.True(normal.Ok, normal.Error);
        Assert.Equal(60000L, normal.Value!.RefundPaisa);
        Assert.Equal(100d, StockOf(test, product));
        Assert.Equal(saleBefore, SaleSnapshot(test, sale.Value.SaleId));
    }

    [Fact]
    public void WrongMedicine_IsRefusedWithoutChangingThePendingEntry()
    {
        using var test = new TestDatabase();
        var sold = TestData.AddProduct(test, name: "Sold medicine");
        var returned = TestData.AddProduct(test, name: "Different medicine");
        var sale = TestData.Sales(test).Create(TestData.Cart(sold), TestData.Owner);
        var rush = TestData.Provisionals(test).Create(
            TestData.RushReturn(returned, qty: 1, unit: "base", refundPaisa: 30000), TestData.Owner);

        var linked = TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value!.Code, TestData.Owner);

        Assert.False(linked.Ok);
        Assert.Contains("not on this bill", linked.Error);
        Assert.True(TestData.Provisionals(test).Get(rush.Value.Code)!.IsPending);
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM provisional_return_allocations;"));
    }

    [Fact]
    public void QuantityAboveTheRemainingBill_IsRefusedAtomically()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sale = TestData.Sales(test).Create(TestData.Cart(product), TestData.Owner);
        var rush = TestData.Provisionals(test).Create(
            TestData.RushReturn(product, qty: 2, unit: "base", refundPaisa: 60000), TestData.Owner);

        var linked = TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value!.Code, TestData.Owner);

        Assert.False(linked.Ok);
        Assert.Contains("unreconciled", linked.Error);
        Assert.True(TestData.Provisionals(test).Get(rush.Value.Code)!.IsPending);
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM provisional_return_allocations;"));
    }

    [Fact]
    public void RepeatedProduct_CanSpanSplitSaleLines_AndStillCapsTheFinalReturn()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var cart = TestData.Cart(product, qty: 2);
        cart.Lines.Add(new SaleLineInput { ProductId = product, Unit = "base", Quantity = 3 });
        var sale = TestData.Sales(test).Create(cart, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(2, sale.Value!.SaleItemIds.Count);

        var rush = TestData.Provisionals(test).Create(
            TestData.RushReturn(product, qty: 4, unit: "base", refundPaisa: 120000), TestData.Owner);
        var linked = TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value.Code, TestData.Owner);
        Assert.True(linked.Ok, linked.Error);

        using (var connection = test.Database.Open())
        {
            var allocations = test.Database.Query<AllocationRow>(connection, @"
SELECT sale_item_id AS saleItemId, qty_base AS qtyBase,
       expected_refund_paisa AS expectedPaisa, refund_paisa AS refundPaisa
FROM provisional_return_allocations ORDER BY sale_item_id;");
            Assert.Equal(2, allocations.Count);
            Assert.Equal(new[] { 2d, 2d }, allocations.Select(row => row.QtyBase));
            Assert.Equal(new[] { 60000L, 60000L }, allocations.Select(row => row.ExpectedPaisa));
            Assert.Equal(new[] { 60000L, 60000L }, allocations.Select(row => row.RefundPaisa));
        }

        var lines = TestData.Returns(test).Lines(sale.Value.SaleId).Value!;
        Assert.Equal(0d, lines[0].RemainingBase);
        Assert.Equal(1d, lines[1].RemainingBase);
        Assert.Equal(30000L, lines[1].RemainingPaisa);

        // The first original line is already wholly returned by the PR.
        Assert.False(TestData.Returns(test).Create(TestData.Return(sale.Value, 1), TestData.Owner).Ok);

        var last = TestData.Return(sale.Value, 1);
        last.Lines[0].SaleItemId = sale.Value.SaleItemIds[1];
        var result = TestData.Returns(test).Create(last, TestData.Owner);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(30000L, result.Value!.RefundPaisa);
    }

    [Fact]
    public void TwoLinkedPrs_AccumulateAgainstTheSameOriginalLine()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 2), TestData.Owner);

        foreach (var number in new[] { 1, 2 })
        {
            var rush = TestData.Provisionals(test).Create(
                TestData.RushReturn(product, qty: 1, unit: "base", refundPaisa: 30000), TestData.Owner);
            Assert.True(rush.Ok, rush.Error);
            var linked = TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value!.Code, TestData.Owner);
            Assert.True(linked.Ok, $"PR {number}: {linked.Error}");
        }

        Assert.Equal(2L, ScalarLong(test, "SELECT COUNT(1) FROM provisional_return_allocations;"));
        var lines = TestData.Returns(test).Lines(sale.Value!.SaleId);
        Assert.True(lines.Ok, lines.Error);
        var line = Assert.Single(lines.Value!);
        Assert.Equal(2d, line.AlreadyReturnedBase);
        Assert.Equal(60000L, line.AlreadyRefundedPaisa);
        Assert.Equal(0d, line.RemainingBase);
        Assert.Equal(0L, line.RemainingPaisa);

        var again = TestData.Returns(test).Create(TestData.Return(sale.Value, 1), TestData.Owner);
        Assert.False(again.Ok);
        Assert.Contains("already returned", again.Error);
    }

    [Theory]
    [InlineData(50000L, 10000L, 20000L)]
    [InlineData(10000L, 50000L, -20000L)]
    public void OverOrUnderRefund_IsVisible_AndOnlyTheUnrefundedBillValueRemains(
        long provisionalRefund, long laterRefund, long expectedVariance)
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 2), TestData.Owner);
        var rush = TestData.Provisionals(test).Create(
            TestData.RushReturn(product, qty: 1, unit: "base", refundPaisa: provisionalRefund), TestData.Owner);

        var linked = TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value!.Code, TestData.Owner);

        Assert.True(linked.Ok, linked.Error);
        var warning = Assert.Single(linked.Warnings);
        Assert.Contains(expectedVariance > 0 ? "over" : "under", warning);

        var row = TestData.Provisionals(test).Get(rush.Value.Code)!;
        Assert.True(row.HasLinkVariance);
        Assert.Equal(30000L, row.LinkedExpectedPaisa.GetValueOrDefault());
        Assert.Equal(expectedVariance, row.LinkVariancePaisa.GetValueOrDefault());
        Assert.Equal(0L, row.LinkedUnallocatedPaisa.GetValueOrDefault());
        var alert = TestData.Alerts(test).Report().Live.Single(item => item.Type == "provisional_linked");
        Assert.Contains("differs", alert.Detail);
        Assert.Contains(sale.Value.Code, alert.Detail);

        var normal = TestData.Returns(test).Create(TestData.Return(sale.Value, 1), TestData.Owner);
        Assert.True(normal.Ok, normal.Error);
        Assert.Equal(laterRefund, normal.Value!.RefundPaisa);
        Assert.Equal(60000L, provisionalRefund + laterRefund);

        using var connection = test.Database.Open();
        var audit = test.Database.QuerySingle<string>(connection,
            "SELECT new_value FROM audit_logs WHERE action = 'provisional_link';");
        Assert.Contains("expected 30000 paisa", audit);
        Assert.Contains($"actual {provisionalRefund} paisa", audit);
    }

    [Fact]
    public void ExcessRefundOnOneMedicine_AlsoCapsTheOtherLinesOfTheBill()
    {
        using var test = new TestDatabase();
        var first = TestData.AddProduct(test, name: "First");
        var second = TestData.AddProduct(test, name: "Second");
        var cart = TestData.Cart(first);
        cart.Lines.Add(new SaleLineInput { ProductId = second, Unit = "base", Quantity = 1 });
        var sale = TestData.Sales(test).Create(cart, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var rush = TestData.Provisionals(test).Create(
            TestData.RushReturn(first, qty: 1, unit: "base", refundPaisa: 90000), TestData.Owner);
        var linked = TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value!.Code, TestData.Owner);
        Assert.True(linked.Ok, linked.Error);
        Assert.Equal(2, linked.Warnings.Count);

        var row = TestData.Provisionals(test).Get(rush.Value!.Code)!;
        Assert.Equal(30000L, row.LinkedExpectedPaisa.GetValueOrDefault());
        Assert.Equal(60000L, row.LinkedUnallocatedPaisa.GetValueOrDefault());

        // Second still has its physical quantity and line value, but the actual PR payment already
        // exceeded the whole bill. Accepting it back must not release another Rs 300.
        var input = TestData.Return(sale.Value!, 1);
        input.Lines[0].SaleItemId = sale.Value!.SaleItemIds[1];
        var normal = TestData.Returns(test).Create(input, TestData.Owner);

        Assert.True(normal.Ok, normal.Error);
        Assert.Equal(0L, normal.Value!.RefundPaisa);
        Assert.Contains(normal.Value.Warnings, text => text.Contains("capped"));
        Assert.Contains(normal.Value.Warnings, text => text.Contains("zero refund"));
    }

    [Fact]
    public void OldStatusOnlyLink_BlocksAnotherReturn_UntilTheSameBillIsReconciled()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 2), TestData.Owner);
        var other = TestData.Sales(test).Create(TestData.Cart(product), TestData.Owner);
        var rush = TestData.Provisionals(test).Create(
            TestData.RushReturn(product, qty: 1, unit: "base", refundPaisa: 30000), TestData.Owner);
        Assert.True(rush.Ok, rush.Error);

        // This is the exact Cedar/Cypress shape: linked header, no allocations, NULL expected value.
        test.Database.Tx(connection => test.Database.Execute(connection, @"
UPDATE provisional_returns
SET status = 'linked', linked_sale_id = @saleId, linked_code = @code,
    linked_at = datetime('now','localtime')
WHERE id = @id;", new { saleId = sale.Value!.SaleId, code = sale.Value!.Code, id = rush.Value!.Id }));

        var oldAlert = TestData.Alerts(test).Report().Live.Single(item => item.Type == "provisional_linked");
        Assert.Contains("before line reconciliation", oldAlert.Detail);
        Assert.Contains(sale.Value!.Code, oldAlert.Detail);

        var blocked = TestData.Returns(test).Lines(sale.Value!.SaleId);
        Assert.False(blocked.Ok);
        Assert.Contains(rush.Value!.Code, blocked.Error);

        var wrongBill = TestData.Provisionals(test).Link(rush.Value!.Id, other.Value!.Code, TestData.Owner);
        Assert.False(wrongBill.Ok);
        Assert.Contains("same bill", wrongBill.Error);
        Assert.Equal(0L, ScalarLong(test, "SELECT COUNT(1) FROM provisional_return_allocations;"));

        var repaired = TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value!.Code, TestData.Owner);
        Assert.True(repaired.Ok, repaired.Error);
        Assert.False(TestData.Provisionals(test).Get(rush.Value!.Code)!.NeedsReconciliation);

        var lines = TestData.Returns(test).Lines(sale.Value!.SaleId);
        Assert.True(lines.Ok, lines.Error);
        Assert.Equal(1d, Assert.Single(lines.Value!).RemainingBase);

        using var connection = test.Database.Open();
        Assert.Equal(1L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = 'provisional_reconcile';") ?? 0L));
    }

    private static double StockOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<double>(connection,
            "SELECT COALESCE(SUM(qty_base), 0) FROM batches WHERE product_id = @id AND active = 1;",
            new { id = productId });
    }

    private static long ScalarLong(TestDatabase test, string sql)
    {
        using var connection = test.Database.Open();
        return Convert.ToInt64(test.Database.Scalar(connection, sql) ?? 0L);
    }

    private static string SaleSnapshot(TestDatabase test, int saleId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<string>(connection, @"
SELECT s.code || '|' || s.total_paisa || '|' || s.paid_paisa || '|' || s.due_paisa || '|' ||
       s.status || '|' || COALESCE((SELECT SUM(si.qty_base) FROM sale_items si WHERE si.sale_id = s.id), 0) ||
       '|' || COALESCE((SELECT SUM(si.line_total_paisa) FROM sale_items si WHERE si.sale_id = s.id), 0)
FROM sales s WHERE s.id = @id;", new { id = saleId }) ?? string.Empty;
    }

    private sealed class AllocationRow
    {
        public int SaleItemId { get; set; }

        public double QtyBase { get; set; }

        public long ExpectedPaisa { get; set; }

        public long RefundPaisa { get; set; }
    }
}
