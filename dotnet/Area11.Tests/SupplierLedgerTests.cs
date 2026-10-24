using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The supplier account and the goods that go back (`SR-nnnn`), Spec 9.3 and 17.4 (F-28).
///
/// The one sentence this file protects: **payable = opening balance + purchases - payments -
/// credit notes**, proven every time the page opens. Each test breaks it from one side:
///
///   • a purchase paid on the spot must not leave a phantom payable (the money is already out);
///   • a payment bigger than the debt is refused, so a payment can never make the account negative;
///   • a credit note lowers the payable and takes the stock out of the chosen batch;
///   • cash from the supplier leaves the payable alone - goods went back, money came in instead;
///   • returning more than the batch holds still completes with a warning (U-30);
///   • a cashier cannot reach either button.
/// </summary>
public sealed class SupplierLedgerTests
{
    private static SupplierLedgerService Ledger(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings), test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    /// <summary>Buys <paramref name="boxes"/> boxes (1 box = 20 tablets) at Rs 200 a tablet.</summary>
    private static PurchaseResult Buy(
        TestDatabase test, int supplierId, int productId, double boxes = 2, long paidPaisa = 0)
    {
        var purchase = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = supplierId,
            SupplierInvoiceNo = "INV-77",
            PaidPaisa = paidPaisa,
            Lines = new List<PurchaseLineInput>
            {
                new()
                {
                    ProductId = productId,
                    Unit = "box",
                    Quantity = boxes,
                    BatchNo = "B-9",
                    Expiry = "2027-06",
                    CostPaisa = 20000,
                    RetailPaisa = 30000
                }
            }
        }, TestData.Owner);

        Assert.True(purchase.Ok, purchase.Error);
        return purchase.Value!;
    }

    private static int NewestBatch(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<int>(connection,
            "SELECT id FROM batches WHERE product_id = @id ORDER BY id DESC LIMIT 1;", new { id = productId });
    }

    private static double BatchQty(TestDatabase test, int batchId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<double>(connection,
            "SELECT qty_base FROM batches WHERE id = @id;", new { id = batchId });
    }

    private static long StoredPayable(TestDatabase test, int supplierId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<long>(connection,
            "SELECT balance_paisa FROM suppliers WHERE id = @id;", new { id = supplierId });
    }

    [Fact]
    public void APurchase_RaisesThePayable_AndTheBooksMatch()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000, batchQty: 0);
        var supplier = TestData.AddSupplier(test);

        Buy(test, supplier, product, boxes: 2);      // 2 x 20 tablets x Rs 200 = Rs 8,000

        Assert.Equal(800000L, StoredPayable(test, supplier));

        var page = Ledger(test).Page(supplier);

        Assert.True(page.Ok, page.Error);
        Assert.Equal(800000L, page.Value!.StoredPaisa);
        Assert.Equal(800000L, page.Value!.ExpectedPaisa);
        Assert.True(page.Value!.BooksMatch, page.Value!.BooksLabel);
        Assert.Single(page.Value!.Lines);
        Assert.Equal("purchase", page.Value!.Lines[0].Kind);
        Assert.Equal(800000L, page.Value!.Lines[0].RunningPaisa);
    }

    [Fact]
    public void APurchasePaidOnTheSpot_DoesNotLeaveAPhantomPayable()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test);

        Buy(test, supplier, product, boxes: 2, paidPaisa: 300000);   // Rs 3,000 paid of Rs 8,000

        Assert.Equal(500000L, StoredPayable(test, supplier));

        var page = Ledger(test).Page(supplier);

        Assert.True(page.Ok, page.Error);

        // Two lines - the purchase and the money already handed over - or the rebuilt balance would
        // be Rs 3,000 bigger than the stored one.
        Assert.Equal(2, page.Value!.Lines.Count);
        Assert.Equal("payment", page.Value!.Lines[1].Kind);
        Assert.Equal(500000L, page.Value!.Lines[1].RunningPaisa);
        Assert.True(page.Value!.BooksMatch, page.Value!.BooksLabel);
    }

    [Fact]
    public void PayingMoney_LowersThePayable_AndWritesAPaymentWithNoPurchase()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test);

        Buy(test, supplier, product);

        var paid = Ledger(test).Pay(TestData.Owner, supplier, 300000, "cash", "part payment");

        Assert.True(paid.Ok, paid.Error);
        Assert.Equal(500000L, paid.Value!.NewBalancePaisa);
        Assert.StartsWith("PAY-", paid.Value!.Code);

        using var connection = test.Database.Open();
        var standalone = test.Database.QuerySingle<int>(connection, @"
SELECT COUNT(1) FROM payments
WHERE supplier_id = @id AND purchase_id IS NULL AND amount_paisa = 300000;", new { id = supplier });
        Assert.Equal(1, standalone);

        var page = Ledger(test).Page(supplier);
        Assert.True(page.Value!.BooksMatch, page.Value!.BooksLabel);
        Assert.Equal(500000L, page.Value!.ExpectedPaisa);
    }

    [Fact]
    public void PayingMoreThanIsOwed_IsRefused_AndNothingChanges()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test);

        Buy(test, supplier, product);   // Rs 8,000 owed

        var paid = Ledger(test).Pay(TestData.Owner, supplier, 900000, "cash", null);

        Assert.False(paid.Ok);
        Assert.Contains("only", paid.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(800000L, StoredPayable(test, supplier));
    }

    [Fact]
    public void PayingASettledSupplier_IsRefused()
    {
        using var test = new TestDatabase();
        var supplier = TestData.AddSupplier(test);

        var paid = Ledger(test).Pay(TestData.Owner, supplier, 10000, "cash", null);

        Assert.False(paid.Ok);
        Assert.Contains("settled", paid.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ASupplierReturn_WithACreditNote_TakesTheStockAndLowersThePayable()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test);

        Buy(test, supplier, product);                       // 40 tablets, Rs 8,000 owed
        var batch = NewestBatch(test, product);

        var result = Ledger(test).Return(TestData.Owner, new SupplierReturnInput
        {
            SupplierId = supplier,
            Reason = SupplierLedgerService.ReasonExpired,
            Lines = new List<SupplierReturnLineInput>
            {
                new() { ProductId = product, BatchId = batch, QtyEntered = 10, Unit = "unit" }
            }
        });

        Assert.True(result.Ok, result.Error);
        Assert.Equal("SR-0001", result.Value!.Code);
        Assert.Equal(200000L, result.Value!.TotalPaisa);     // 10 x Rs 200 at cost
        Assert.Equal(600000L, result.Value!.NewPayablePaisa);
        Assert.False(result.Value!.CashReceived);
        Assert.Empty(result.Value!.Warnings);

        Assert.Equal(30d, BatchQty(test, batch));
        Assert.Equal(600000L, StoredPayable(test, supplier));

        using var connection = test.Database.Open();
        var movement = test.Database.QuerySingle<string>(connection, @"
SELECT type FROM stock_movements WHERE ref_code = 'SR-0001' ORDER BY id DESC LIMIT 1;");
        Assert.Equal("supplier_return", movement);

        var movedQty = test.Database.QuerySingle<double>(connection, @"
SELECT qty_base FROM stock_movements WHERE ref_code = 'SR-0001' ORDER BY id DESC LIMIT 1;");
        Assert.Equal(-10d, movedQty);

        var page = Ledger(test).Page(supplier);
        Assert.True(page.Value!.BooksMatch, page.Value!.BooksLabel);
        Assert.Equal(2, page.Value!.Lines.Count);           // the purchase, then the credit note
    }

    [Fact]
    public void ReturningMoreThanTheBatchHolds_StillCompletes_WithAWarning()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test);

        Buy(test, supplier, product);
        var batch = NewestBatch(test, product);

        var result = Ledger(test).Return(TestData.Owner, new SupplierReturnInput
        {
            SupplierId = supplier,
            Reason = SupplierLedgerService.ReasonDamaged,
            Lines = new List<SupplierReturnLineInput>
            {
                new() { ProductId = product, BatchId = batch, QtyEntered = 100, Unit = "unit" }
            }
        });

        Assert.True(result.Ok, result.Error);                    // U-30: never blocked
        Assert.Equal(-60d, BatchQty(test, batch));
        Assert.Contains(result.Value!.Warnings, w => w.Contains("MINUS", StringComparison.Ordinal));

        // 100 x Rs 200 = Rs 20,000 returned against Rs 8,000 owed: the shop is Rs 12,000 in credit.
        Assert.Equal(-1200000L, result.Value!.NewPayablePaisa);
        Assert.Equal(-1200000L, StoredPayable(test, supplier));
        Assert.Contains(result.Value!.Warnings, w => w.Contains("favour", StringComparison.OrdinalIgnoreCase));

        var page = Ledger(test).Page(supplier);
        Assert.True(page.Value!.BooksMatch, page.Value!.BooksLabel);
    }

    [Fact]
    public void ABatchFromAnotherSupplier_IsReturned_ButTheOwnerIsWarned()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var seller = TestData.AddSupplier(test, "GSK Depot");
        var other = TestData.AddSupplier(test, "Highnoon Labs");

        Buy(test, seller, product);
        var batch = NewestBatch(test, product);   // received from GSK

        var result = Ledger(test).Return(TestData.Owner, new SupplierReturnInput
        {
            SupplierId = other,                   // ... but sent back to Highnoon
            Reason = SupplierLedgerService.ReasonOverstock,
            Lines = new List<SupplierReturnLineInput>
            {
                new() { ProductId = product, BatchId = batch, QtyEntered = 5, Unit = "unit" }
            }
        });

        Assert.True(result.Ok, result.Error);
        Assert.Contains(result.Value!.Warnings, w => w.Contains("another supplier", StringComparison.OrdinalIgnoreCase));

        // The shop's choice is obeyed: the credit goes on Highnoon, and GSK still owes nothing less.
        Assert.Equal(-100000L, StoredPayable(test, other));      // 5 x Rs 200, in the shop's favour
        Assert.Equal(800000L, StoredPayable(test, seller));
        Assert.Equal(35d, BatchQty(test, batch));
    }

    [Fact]
    public void CashFromTheSupplier_LeavesThePayableAlone()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test);

        Buy(test, supplier, product);
        var batch = NewestBatch(test, product);

        var result = Ledger(test).Return(TestData.Owner, new SupplierReturnInput
        {
            SupplierId = supplier,
            Reason = SupplierLedgerService.ReasonWrongItem,
            CashReceived = true,
            Lines = new List<SupplierReturnLineInput>
            {
                new() { ProductId = product, BatchId = batch, QtyEntered = 10, Unit = "unit" }
            }
        });

        Assert.True(result.Ok, result.Error);
        Assert.True(result.Value!.CashReceived);
        Assert.Equal(800000L, result.Value!.NewPayablePaisa);     // goods out, cash in: the account is untouched
        Assert.Equal(800000L, StoredPayable(test, supplier));
        Assert.Equal(30d, BatchQty(test, batch));

        using var connection = test.Database.Open();
        var settled = test.Database.QuerySingle<int>(connection,
            "SELECT settled FROM supplier_returns WHERE code = 'SR-0001';");
        Assert.Equal(1, settled);

        var page = Ledger(test).Page(supplier);
        Assert.True(page.Value!.BooksMatch, page.Value!.BooksLabel);
        Assert.Equal("cash_return", page.Value!.Lines[1].Kind);
        Assert.Equal(800000L, page.Value!.Lines[1].RunningPaisa);
    }

    [Fact]
    public void AnOpeningBalance_IsPartOfThePayable_AndOfTheHistory()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test, openingPaisa: 500000);

        Buy(test, supplier, product);

        var page = Ledger(test).Page(supplier);

        Assert.True(page.Ok, page.Error);
        Assert.Equal(1300000L, page.Value!.StoredPaisa);
        Assert.Equal(1300000L, page.Value!.ExpectedPaisa);
        Assert.True(page.Value!.BooksMatch, page.Value!.BooksLabel);
        Assert.Equal("opening", page.Value!.Lines[0].Kind);
        Assert.Equal(500000L, page.Value!.Lines[0].RunningPaisa);
    }

    [Fact]
    public void ACashierCannotPayOrSendGoodsBack()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test);

        Buy(test, supplier, product);
        var batch = NewestBatch(test, product);

        var cashier = new User { Id = 99, Name = "Cashier", Role = "cashier" };

        var paid = Ledger(test).Pay(cashier, supplier, 10000, "cash", null);
        Assert.False(paid.Ok);
        Assert.Contains("permission", paid.Error, StringComparison.OrdinalIgnoreCase);

        var returned = Ledger(test).Return(cashier, new SupplierReturnInput
        {
            SupplierId = supplier,
            Lines = new List<SupplierReturnLineInput>
            {
                new() { ProductId = product, BatchId = batch, QtyEntered = 1, Unit = "unit" }
            }
        });

        Assert.False(returned.Ok);
        Assert.Contains("owner or a manager", returned.Error);
        Assert.Equal(800000L, StoredPayable(test, supplier));
        Assert.Equal(40d, BatchQty(test, batch));
    }

    [Fact]
    public void TheExpiredList_FindsOnlyStockThatIsAlreadyExpired()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test);

        Buy(test, supplier, product);   // expires 2027-06 - not expired

        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    is_sample, supplier_id, active, created_at)
VALUES(@productId, 'OLD-1', '2025-01-31', '2025-01', 12, 20000, 30000, 0, @supplier, 1, '2025-01-01 09:00:00');",
                new { productId = product, supplier });
            return 0;
        });

        var expired = Ledger(test).ExpiredBatches();

        Assert.True(expired.Ok, expired.Error);
        Assert.Single(expired.Value!);
        Assert.Equal("OLD-1", expired.Value![0].BatchNo);
        Assert.Equal(12d, expired.Value![0].QtyBase);
        Assert.Contains("OLD-1", expired.Value![0].Caption);
    }

    [Fact]
    public void TheStatement_FitsThePaper_AndShowsThePayable()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 0);
        var supplier = TestData.AddSupplier(test);

        Buy(test, supplier, product, paidPaisa: 100000);

        var statement = Ledger(test).Statement(supplier);

        Assert.True(statement.Ok, statement.Error);

        var paper = statement.Value!;
        Assert.Equal(32, paper.Width);
        Assert.Contains("SUPPLIER STATEMENT", paper.PlainText);
        Assert.Contains("TOTAL PAYABLE", paper.PlainText);
        Assert.All(paper.Lines, line => Assert.True(line.Text.Length <= 32,
            "the line does not fit the paper: " + line.Text));
    }

    [Fact]
    public void TheBoxStripAndUnitCopyTheSameNumber()
    {
        // 1 box = 2 strips = 20 tablets
        Assert.Equal(20d, Packs.PerUnit(2, 10, "box"));
        Assert.Equal(10d, Packs.PerUnit(2, 10, "strip"));
        Assert.Equal(1d, Packs.PerUnit(2, 10, "unit"));

        // A medicine with no pack formula is sold in base units only - never a division by zero.
        Assert.Equal(1d, Packs.PerUnit(0, 0, "box"));
        Assert.Equal(1d, Packs.PerUnit(0, 0, "strip"));
        Assert.Contains("1 box = 2 strip = 20 tablet", Packs.Label(2, 10, "tablet"));
    }
}
