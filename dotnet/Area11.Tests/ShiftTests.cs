using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Shift / galla (Spec 14, F-31). The promises that must hold:
///   • one drawer at a time, and the float is the only number the person types in advance;
///   • **expected cash is rebuilt from history**: cash bills + khata money received + cash a supplier
///     handed back, minus refunds and money paid to suppliers;
///   • cards, online and credit bills never move the drawer;
///   • closing says exact / over / short and is refused twice;
///   • a shift is closed by the person who opened it, or by the owner or a manager - not by a peer.
/// </summary>
public sealed class ShiftTests
{
    private static ShiftService Shifts(TestDatabase test) =>
        new(test.Database, test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static int AddCashier(TestDatabase test, bool withCashPermission)
    {
        var id = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO users(name, role, active, created_at) VALUES('Cashier', 'cashier', 1, @at);",
                new { at = Clock.Now() });

            return (int)test.Database.NextId(connection);
        });

        if (withCashPermission)
        {
            var granted = new PermissionService(test.Database, test.Log, test.Audit)
                .Set(TestData.Owner, id, Modules.Cash, true);

            Assert.True(granted.Ok, granted.Error);
        }

        return id;
    }

    private static void SetSupplierOpening(TestDatabase test, int supplierId, long paisa)
    {
        using var connection = test.Database.Open();
        test.Database.Execute(connection,
            "UPDATE suppliers SET balance_paisa = @paisa WHERE id = @id;", new { paisa, id = supplierId });
    }

    [Fact]
    public void AnOpenShift_ShowsItsFloat()
    {
        using var test = new TestDatabase();

        var opened = Shifts(test).Open(TestData.Owner, 50000, "morning");

        Assert.True(opened.Ok, opened.Error);
        Assert.True(opened.Value!.IsOpen);
        Assert.Equal(50000L, opened.Value.OpeningFloatPaisa);
        Assert.Equal(50000L, opened.Value.ExpectedPaisa);      // nothing sold yet
        Assert.Equal("morning", opened.Value.Note);

        var current = Shifts(test).Current();
        Assert.NotNull(current);
        Assert.Equal("Owner", current!.OpenedBy);
    }

    [Fact]
    public void ASecondOpen_IsRefused()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 10000, null).Ok);

        var second = Shifts(test).Open(TestData.Owner, 20000, null);

        Assert.False(second.Ok);
        Assert.Contains("already open", second.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10000L, Shifts(test).Current()!.OpeningFloatPaisa);
    }

    [Fact]
    public void OnlyCashBills_ReachTheDrawer()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);

        var cash = TestData.Sales(test).Create(TestData.Cart(product, qty: 3, method: "cash"), TestData.Owner);
        var card = TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "card"), TestData.Owner);
        var credit = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 1, method: "credit", customerId: customer), TestData.Owner);

        Assert.True(cash.Ok && card.Ok && credit.Ok, cash.Error ?? card.Error ?? credit.Error);

        var shift = Shifts(test).Current()!;

        Assert.Equal(90000L, shift.CashSalesPaisa);            // 3 x Rs 300 cash
        Assert.Equal(140000L, shift.ExpectedPaisa);            // float 500 + 900
        Assert.Equal(0L, shift.CashCollectionsPaisa);
    }

    [Fact]
    public void KhataMoneyReceived_AddsToTheDrawer()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var customer = TestData.AddCustomer(test);

        var credit = TestData.Sales(test).Create(
            TestData.Cart(product, qty: 2, method: "credit", customerId: customer), TestData.Owner);
        Assert.True(credit.Ok, credit.Error);

        Assert.True(Shifts(test).Open(TestData.Owner, 0, null).Ok);

        var ledger = new CustomerLedgerService(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

        var collected = ledger.Collect(TestData.Owner, customer, 40000, "cash", "part payment");
        Assert.True(collected.Ok, collected.Error);

        var shift = Shifts(test).Current()!;

        Assert.Equal(40000L, shift.CashCollectionsPaisa);
        Assert.Equal(0L, shift.CashSalesPaisa);                // the credit bill itself gave no cash
        Assert.Equal(40000L, shift.ExpectedPaisa);
    }

    [Fact]
    public void ACashRefund_TakesMoneyOutOfTheDrawer()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);

        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 3, method: "cash"), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        Assert.True(Shifts(test).Open(TestData.Owner, 0, null).Ok);

        var returned = TestData.Returns(test).Create(TestData.Return(sale.Value!, 1), TestData.Owner);
        Assert.True(returned.Ok, returned.Error);
        Assert.Equal(30000L, returned.Value!.CashPaisa);

        var shift = Shifts(test).Current()!;

        Assert.Equal(90000L, shift.CashSalesPaisa);
        Assert.Equal(30000L, shift.CashRefundsPaisa);
        Assert.Equal(60000L, shift.ExpectedPaisa);
    }

    [Fact]
    public void SupplierMoney_MovesTheDrawerBothWays()
    {
        using var test = new TestDatabase();
        var supplier = TestData.AddSupplier(test);
        var product = TestData.AddProduct(test, batchQty: 0);

        SetSupplierOpening(test, supplier, 0);

        var bought = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = supplier,
            PaidPaisa = 0,
            Lines = new List<PurchaseLineInput>
            {
                new() { ProductId = product, Unit = "box", Quantity = 2, BatchNo = "B-9", Expiry = "2027-06",
                        CostPaisa = 20000, RetailPaisa = 30000 }
            }
        }, TestData.Owner);

        Assert.True(bought.Ok, bought.Error);

        Assert.True(Shifts(test).Open(TestData.Owner, 0, null).Ok);

        var ledger = new SupplierLedgerService(test.Database, test.Audit, new NumberingService(test.Settings),
            test.Settings, new PermissionService(test.Database, test.Log, test.Audit), test.Log);

        // Money paid to the supplier leaves the drawer.
        Assert.True(ledger.Pay(TestData.Owner, supplier, 200000, "cash", "part").Ok);

        // Goods returned for cash bring money back in: 1 box = 20 units x Rs 200 cost.
        var batchId = BoughtBatchId(test, product, "B-9");

        var returned = ledger.Return(TestData.Owner, new SupplierReturnInput
        {
            SupplierId = supplier,
            Reason = SupplierLedgerService.ReasonOverstock,
            CashReceived = true,
            Note = "cash back",
            Lines = new List<SupplierReturnLineInput> { new() { BatchId = batchId, QtyBase = 20 } }
        });

        Assert.True(returned.Ok, returned.Error);

        var shift = Shifts(test).Current()!;

        Assert.Equal(200000L, shift.SupplierCashOutPaisa);
        Assert.Equal(400000L, shift.SupplierCashInPaisa);
        Assert.Equal(200000L, shift.ExpectedPaisa);            // 400 in - 200 out
    }

    [Fact]
    public void Closing_SaysExactOverOrShort_AndCannotBeDoneTwice()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);

        Assert.True(Shifts(test).Open(TestData.Owner, 10000, null).Ok);
        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash"), TestData.Owner).Ok);

        var closed = Shifts(test).Close(TestData.Owner, 40500, "short by a little");

        Assert.True(closed.Ok, closed.Error);
        Assert.Equal(40000L, closed.Value!.ExpectedPaisa);
        Assert.Equal(40500L, closed.Value.ActualPaisa);
        Assert.Equal(500L, closed.Value.DifferencePaisa);
        Assert.Equal("over by Rs 5", closed.Value.DifferenceLabel);
        Assert.False(closed.Value.IsOpen);

        var again = Shifts(test).Close(TestData.Owner, 40000, null);
        Assert.False(again.Ok);
        Assert.Null(Shifts(test).Current());
    }

    [Fact]
    public void AShortDrawer_SaysShortWithTheAmount()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);

        Assert.True(Shifts(test).Open(TestData.Owner, 0, null).Ok);
        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash"), TestData.Owner).Ok);

        var closed = Shifts(test).Close(TestData.Owner, 20000, "note missing");

        Assert.True(closed.Ok, closed.Error);
        Assert.Equal(-10000L, closed.Value!.DifferencePaisa);
        Assert.Equal("short by Rs 100", closed.Value.DifferenceLabel);
        Assert.True(closed.Value.IsShort);
    }

    [Fact]
    public void APeerMayNotCloseSomebodyElsesShift_ButTheOwnerMay()
    {
        using var test = new TestDatabase();
        var cashierId = AddCashier(test, withCashPermission: true);
        var cashier = new User { Id = cashierId, Name = "Cashier", Role = "cashier" };

        Assert.True(Shifts(test).Open(TestData.Owner, 10000, null).Ok);

        var refused = Shifts(test).Close(cashier, 10000, null);
        Assert.False(refused.Ok);
        Assert.Contains("owner or a manager", refused.Error!, StringComparison.OrdinalIgnoreCase);

        var byOwner = Shifts(test).Close(TestData.Owner, 10000, null);
        Assert.True(byOwner.Ok, byOwner.Error);
        Assert.True(byOwner.Value!.IsExact);
        Assert.Equal("exact", byOwner.Value.DifferenceLabel);
    }

    [Fact]
    public void WithoutTheCashPermission_TheTillStaysClosed()
    {
        using var test = new TestDatabase();
        var cashierId = AddCashier(test, withCashPermission: false);
        var cashier = new User { Id = cashierId, Name = "Cashier", Role = "cashier" };

        var refused = Shifts(test).Open(cashier, 10000, null);

        Assert.False(refused.Ok);
        Assert.Contains("permission", refused.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(Shifts(test).Current());
    }

    [Fact]
    public void TheHistory_KeepsTheClosedShift()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);

        Assert.True(Shifts(test).Open(TestData.Owner, 25000, "morning").Ok);
        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash"), TestData.Owner).Ok);
        Assert.True(Shifts(test).Close(TestData.Owner, 55000, "counted twice").Ok);

        var row = Assert.Single(Shifts(test).History());

        Assert.Equal("Closed", row.StatusLabel);
        Assert.Equal(25000L, row.OpeningFloatPaisa);
        Assert.Equal(55000L, row.ExpectedPaisa);
        Assert.Equal(55000L, row.ActualPaisa);
        Assert.Equal(0L, row.DifferencePaisa);
        Assert.Equal("Owner", row.OpenedBy);
        Assert.Equal("counted twice", row.NoteLabel);
    }

    /// <summary>The batch the purchase created, so the return takes the goods this shift received.</summary>
    private static int BoughtBatchId(TestDatabase test, int productId, string batchNo)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<int>(connection,
            "SELECT id FROM batches WHERE product_id = @productId AND batch_no = @batchNo ORDER BY id DESC LIMIT 1;",
            new { productId, batchNo });
    }
}
