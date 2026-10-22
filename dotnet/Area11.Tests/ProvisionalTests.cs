using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// No-bill (rush) returns, `PR-nnnn` (9.2.2, 15.5, U-33). The tests pin the four promises the
/// owner made for this feature:
///   • the goods go on the shelf **once**, and only once, even after the bill is attached (31.6);
///   • the entry keeps a permanent number and stays pending until the bill is attached;
///   • cancelling needs a reason and takes the same stock back out;
///   • only the owner or a manager can attach or cancel.
/// </summary>
public sealed class ProvisionalTests
{
    private static double StockOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<double>(connection,
            "SELECT IFNULL(SUM(qty_base), 0) FROM batches WHERE product_id = @id AND active = 1;",
            new { id = productId });
    }

    [Fact]
    public void ARushReturn_GetsAPermanentNumber_AndTheGoodsGoOnTheShelf()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);

        var result = TestData.Provisionals(test).Create(TestData.RushReturn(product), TestData.Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal("PR-0001", result.Value!.Code);
        Assert.Equal(60000, result.Value.RefundPaisa);

        // 1 box = 2 strips x 10 tablets = 20 tablets, added to the shelf.
        Assert.Equal(30d, StockOf(test, product));
    }

    [Fact]
    public void TheSecondRushReturn_GetsTheNextNumber()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        var first = TestData.Provisionals(test).Create(TestData.RushReturn(product, qty: 1), TestData.Owner);
        var second = TestData.Provisionals(test).Create(TestData.RushReturn(product, qty: 1), TestData.Owner);

        Assert.Equal("PR-0001", first.Value!.Code);
        Assert.Equal("PR-0002", second.Value!.Code);
    }

    [Fact]
    public void TheEntry_StaysPending_WithNoBill()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        TestData.Provisionals(test).Create(TestData.RushReturn(product), TestData.Owner);

        var pending = TestData.Provisionals(test).Pending();

        Assert.Single(pending);
        Assert.True(pending[0].IsPending);
        Assert.Equal("bill MISSING", pending[0].StatusLabel);
        Assert.Contains("Ali Raza", pending[0].CustomerLabel);
    }

    [Fact]
    public void AnEmptyReturn_IsRefused()
    {
        using var test = new TestDatabase();

        var empty = new ProvisionalInput { RefundPaisa = 5000 };

        var result = TestData.Provisionals(test).Create(empty, TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("at least one medicine", result.Error ?? string.Empty);
    }

    [Fact]
    public void ANegativeRefund_IsRefused()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        var result = TestData.Provisionals(test).Create(TestData.RushReturn(product, refundPaisa: -100), TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("minus", result.Error ?? string.Empty);
    }

    [Fact]
    public void AttachingTheBill_DoesNotAddTheStockTwice()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);

        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 2), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var rush = TestData.Provisionals(test).Create(TestData.RushReturn(product, qty: 1), TestData.Owner);
        var afterRush = StockOf(test, product);
        Assert.Equal(28d, afterRush);   // 10 - 2 sold + 20 returned

        var linked = TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value!.Code, TestData.Owner);

        Assert.True(linked.Ok, linked.Error);
        Assert.Equal(afterRush, StockOf(test, product));   // 31.6: no double addition

        var row = TestData.Provisionals(test).Get(rush.Value.Code)!;
        Assert.True(row.IsLinked);
        Assert.Equal(sale.Value.Code, row.LinkedCode);
    }

    [Fact]
    public void AnUnknownBill_IsRefused()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        var rush = TestData.Provisionals(test).Create(TestData.RushReturn(product), TestData.Owner);

        var linked = TestData.Provisionals(test).Link(rush.Value!.Id, "INV-9999", TestData.Owner);

        Assert.False(linked.Ok);
        Assert.Contains("No bill found", linked.Error ?? string.Empty);
    }

    [Fact]
    public void Cancelling_NeedsAReason_AndTakesTheStockBackOut()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 10);

        var rush = TestData.Provisionals(test).Create(TestData.RushReturn(product, qty: 1), TestData.Owner);

        Assert.False(TestData.Provisionals(test).Cancel(rush.Value!.Id, string.Empty, TestData.Owner).Ok);
        Assert.False(TestData.Provisionals(test).Cancel(rush.Value.Id, "no", TestData.Owner).Ok);
        Assert.Equal(30d, StockOf(test, product));

        var cancelled = TestData.Provisionals(test).Cancel(rush.Value.Id, "typed twice by mistake", TestData.Owner);

        Assert.True(cancelled.Ok, cancelled.Error);
        Assert.Equal(10d, StockOf(test, product));

        var row = TestData.Provisionals(test).Get(rush.Value.Code)!;
        Assert.True(row.IsCancelled);
        Assert.Contains("typed twice", row.CancelReason ?? string.Empty);
    }

    [Fact]
    public void ACancelledEntry_CannotBeCancelledAgain()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        var rush = TestData.Provisionals(test).Create(TestData.RushReturn(product), TestData.Owner);
        Assert.True(TestData.Provisionals(test).Cancel(rush.Value!.Id, "mistake", TestData.Owner).Ok);

        var again = TestData.Provisionals(test).Cancel(rush.Value.Id, "mistake again", TestData.Owner);

        Assert.False(again.Ok);
        Assert.Contains("already cancelled", again.Error ?? string.Empty);
    }

    [Fact]
    public void OnlyTheOwnerOrAManager_CanAttachOrCancel()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sale = TestData.Sales(test).Create(TestData.Cart(product), TestData.Owner);
        var rush = TestData.Provisionals(test).Create(TestData.RushReturn(product), TestData.Owner);

        var cashier = new User { Id = 5, Name = "Bilal", Role = "cashier" };

        Assert.False(TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value!.Code, cashier).Ok);
        Assert.False(TestData.Provisionals(test).Cancel(rush.Value.Id, "wrong entry", cashier).Ok);

        var manager = new User { Id = 3, Name = "Kamran", Role = "manager" };
        Assert.True(TestData.Provisionals(test).Link(rush.Value.Id, sale.Value.Code, manager).Ok);
    }

    [Fact]
    public void ARushReturn_RaisesAnAlert_ThatTurnsAmberWhenTheBillArrives()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sale = TestData.Sales(test).Create(TestData.Cart(product), TestData.Owner);
        var rush = TestData.Provisionals(test).Create(TestData.RushReturn(product), TestData.Owner);

        var alerts = TestData.Alerts(test);

        var report = alerts.Report();
        Assert.Equal(1, report.ProvisionalCount);
        Assert.Contains("missing the bill", report.SummaryText);

        Assert.True(TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value!.Code, TestData.Owner).Ok);

        var afterLink = alerts.Report();
        Assert.Equal(0, afterLink.ProvisionalCount);
        Assert.Equal(1, afterLink.LinkedCount);
        Assert.Contains("confirmation pending", afterLink.SummaryText);

        // Even after the bill is attached the alert stays until the owner writes a reason (15.5.4).
        Assert.True(afterLink.HasLive);
        var key = afterLink.Live.First(alert => alert.Type == "provisional_linked").Key;
        Assert.True(alerts.Dismiss(TestData.Owner, key, "checked against the bill").Ok);
        Assert.False(alerts.Report().HasLive);
    }

    [Fact]
    public void TheSlip_SaysTheBillIsMissing_AndPrintsTheNumber()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var rush = TestData.Provisionals(test).Create(TestData.RushReturn(product), TestData.Owner);

        var receipt = TestData.ProvisionalReceipts(test).Build(rush.Value!.Code);

        Assert.True(receipt.Ok, receipt.Error);
        var text = receipt.Value!.PlainText;

        Assert.Contains("PR-0001", text);
        Assert.Contains("NO-BILL RETURN", text);
        Assert.Contains("Paper bill missing", text);
        Assert.Contains("Panadol", text);
        Assert.Contains("600.00", text);
    }

    [Fact]
    public void SmartLookup_FindsABill_ByMedicinePhoneOrDate()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var customer = TestData.AddCustomer(test);
        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 3, method: "credit", customerId: customer), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var service = TestData.Provisionals(test);

        Assert.Equal(sale.Value!.Code, service.SearchBills("Panadol").Value![0].Code);
        Assert.Equal(sale.Value.Code, service.SearchBills("0300-1234567").Value![0].Code);
        Assert.Equal(sale.Value.Code, service.SearchBills("Ali Raza").Value![0].Code);
        Assert.Equal(sale.Value.Code, service.SearchBills(sale.Value.Code).Value![0].Code);

        Assert.False(service.SearchBills("P").Ok);      // one letter is not a search
        Assert.Empty(service.SearchBills("Nothing-matches-this").Value!);
    }

    [Fact]
    public void CreatingAndLinking_AreWrittenToTheBlackBox()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var sale = TestData.Sales(test).Create(TestData.Cart(product), TestData.Owner);
        var rush = TestData.Provisionals(test).Create(TestData.RushReturn(product), TestData.Owner);

        Assert.True(TestData.Provisionals(test).Link(rush.Value!.Id, sale.Value!.Code, TestData.Owner).Ok);

        using var connection = test.Database.Open();
        var actions = test.Database.Query<string>(connection,
            "SELECT action FROM audit_logs WHERE action LIKE 'provisional_%' ORDER BY id;");

        Assert.Equal(new[] { "provisional_create", "provisional_link" }, actions);
    }
}
