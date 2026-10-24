using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The people who owe the shop and the people the shop owes (F-18, Spec 10 / 15).
///
/// Why these three rules are pinned first: a duplicate phone number splits one family's khata in two,
/// a supplier payment that overshoots leaves the account in credit, and a supplier return that takes
/// the wrong quantity changes a shelf and a payable in one stroke. Each one is a question the owner
/// asks on the phone, so each one has to answer the same way every time.
/// </summary>
public sealed class PartnerTests
{
    // ============================== one phone, one khata ==============================

    [Fact]
    public void TwoCustomersCannotShareAPhoneNumber()
    {
        using var test = new TestDatabase();
        var partners = TestData.Partners(test);

        var first = partners.CreateCustomer(TestData.Owner, "Ali Raza", "0300-1234567", "normal");
        Assert.True(first.Ok, first.Error);

        var second = partners.CreateCustomer(TestData.Owner, "Ali Raza's brother", "0300-1234567", "normal");

        Assert.False(second.Ok);
        Assert.Contains("Ali Raza", second.Error!);
        Assert.Contains("phone", second.Error!);
        Assert.Equal(1, Count(test, "customers"));
    }

    [Fact]
    public void APhoneNumberMayBeLeftEmptyAsManyTimesAsNeeded()
    {
        using var test = new TestDatabase();
        var partners = TestData.Partners(test);

        Assert.True(partners.CreateCustomer(TestData.Owner, "Walking one", null, "normal").Ok);
        Assert.True(partners.CreateCustomer(TestData.Owner, "Walking two", "   ", "normal").Ok);

        Assert.Equal(2, Count(test, "customers"));
    }

    [Fact]
    public void ACustomerNeedsATwoLetterName()
    {
        using var test = new TestDatabase();
        var partners = TestData.Partners(test);

        var refused = partners.CreateCustomer(TestData.Owner, " A ", null, "normal");

        Assert.False(refused.Ok);
        Assert.Contains("name", refused.Error!);
        Assert.Equal(0, Count(test, "customers"));
    }

    [Fact]
    public void AnUnknownCustomerCategoryQuietlyBecomesNormal()
    {
        using var test = new TestDatabase();
        var partners = TestData.Partners(test);

        var created = partners.CreateCustomer(TestData.Owner, "Hameed Clinic", "042-35566778", "clinic");

        Assert.True(created.Ok, created.Error);
        Assert.Equal("normal", CategoryOf(test, created.Value));
    }

    [Fact]
    public void ADoctorIsKeptAsADoctorBecauseTheRateDependsOnIt()
    {
        using var test = new TestDatabase();
        var partners = TestData.Partners(test);

        var created = partners.CreateCustomer(TestData.Owner, "Dr. Sana Malik", "0300-5556677", "DOCTOR ");

        Assert.True(created.Ok, created.Error);
        Assert.Equal("doctor", CategoryOf(test, created.Value));
    }

    [Fact]
    public void TheCustomerListFindsAPersonByPhone()
    {
        using var test = new TestDatabase();
        var partners = TestData.Partners(test);
        partners.CreateCustomer(TestData.Owner, "Ali Raza", "0300-1234567", "normal");
        partners.CreateCustomer(TestData.Owner, "Fatima Bibi", "0321-9876543", "normal");

        var found = partners.GetCustomers("98765");

        Assert.True(found.Ok, found.Error);
        Assert.Single(found.Value!);
        Assert.Equal("Fatima Bibi", found.Value![0].Name);
    }

    // ============================== the companies the shop buys from ==============================

    [Fact]
    public void OnlyTheOwnerOrAManagerCanAddASupplier()
    {
        using var test = new TestDatabase();
        var partners = TestData.Partners(test);

        var refused = partners.CreateSupplier(TestData.Cashier(test), "Getz Pharma", "042-1112233", "Getz");

        Assert.False(refused.Ok);
        Assert.Contains("owner or a manager", refused.Error!);
        Assert.Equal(0, Count(test, "suppliers"));
    }

    [Fact]
    public void TheOwnerAddsASupplierAndThePayableStartsAtZero()
    {
        using var test = new TestDatabase();
        var partners = TestData.Partners(test);

        var created = partners.CreateSupplier(TestData.Owner, " Getz Pharma ", "042-1112233", "Getz");

        Assert.True(created.Ok, created.Error);
        Assert.Equal(0, PayableOf(test, created.Value));
        Assert.Equal(1, Count(test, "suppliers"));
    }

    // ============================== the shop's own payables ==============================

    [Fact]
    public void ASupplierPaymentLowersThePayableAndIsNumbered()
    {
        using var test = new TestDatabase();
        var supplierId = TestData.AddSupplier(test, openingPaisa: 100000);

        var paid = Pay(test, supplierId, 40000);

        Assert.True(paid.Ok, paid.Error);
        Assert.Equal(60000, paid.Value!.NewBalancePaisa);
        Assert.StartsWith("PAY-", paid.Value.Code);
        Assert.Equal(60000, PayableOf(test, supplierId));
    }

    [Fact]
    public void PayingMoreThanIsOwedIsRefusedRatherThanLeavingACredit()
    {
        using var test = new TestDatabase();
        var supplierId = TestData.AddSupplier(test, openingPaisa: 100000);

        var refused = Pay(test, supplierId, 100001);

        Assert.False(refused.Ok);
        Assert.Contains("refused", refused.Error!);
        Assert.Equal(100000, PayableOf(test, supplierId));
    }

    [Fact]
    public void ASettledSupplierHasNothingToPay()
    {
        using var test = new TestDatabase();
        var supplierId = TestData.AddSupplier(test, openingPaisa: 0);

        var refused = Pay(test, supplierId, 1000);

        Assert.False(refused.Ok);
        Assert.Contains("settled", refused.Error!);
    }

    // ============================== goods back to the company ==============================

    [Fact]
    public void ASupplierReturnTakesTheGoodsOffTheShelfAndOffThePayable()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);
        var supplierId = TestData.AddSupplier(test, openingPaisa: 500000);
        var batchId = BatchOf(test, productId);

        var result = Ledger(test).Return(TestData.Owner, new SupplierReturnInput
        {
            SupplierId = supplierId,
            Reason = "expired",
            CashReceived = false,
            Lines = new List<SupplierReturnLineInput>
            {
                new() { ProductId = productId, BatchId = batchId, QtyEntered = 10, Unit = "base" }
            }
        });

        Assert.True(result.Ok, result.Error);
        Assert.StartsWith("SR-", result.Value!.Code);
        Assert.Equal(90, StockOf(test, productId));           // 100 on the shelf, 10 went back
        Assert.Equal(300000, PayableOf(test, supplierId));    // the Rs 2,000 credit note at cost
    }

    [Fact]
    public void ASupplierReturnWithCashBackLeavesThePayableAlone()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);
        var supplierId = TestData.AddSupplier(test, openingPaisa: 500000);
        var batchId = BatchOf(test, productId);

        var result = Ledger(test).Return(TestData.Owner, new SupplierReturnInput
        {
            SupplierId = supplierId,
            Reason = "damaged",
            CashReceived = true,
            Lines = new List<SupplierReturnLineInput>
            {
                new() { ProductId = productId, BatchId = batchId, QtyEntered = 10, Unit = "base" }
            }
        });

        Assert.True(result.Ok, result.Error);
        Assert.Equal(500000, PayableOf(test, supplierId)); // the money came back in the hand
        Assert.Equal(90, StockOf(test, productId));
    }

    [Fact]
    public void ASupplierReturnWithoutAReasonIsRefused()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);
        var supplierId = TestData.AddSupplier(test, openingPaisa: 500000);
        var batchId = BatchOf(test, productId);

        var refused = Ledger(test).Return(TestData.Owner, new SupplierReturnInput
        {
            SupplierId = supplierId,
            Reason = "because",
            Lines = new List<SupplierReturnLineInput>
            {
                new() { ProductId = productId, BatchId = batchId, QtyEntered = 1, Unit = "base" }
            }
        });

        Assert.False(refused.Ok);
        Assert.Contains("reason", refused.Error!);
        Assert.Equal(100, StockOf(test, productId));
    }

    [Fact]
    public void ACashierCannotSendGoodsBackToAClaimingCompany()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);
        var supplierId = TestData.AddSupplier(test, openingPaisa: 500000);
        var batchId = BatchOf(test, productId);

        var refused = Ledger(test).Return(TestData.Cashier(test), new SupplierReturnInput
        {
            SupplierId = supplierId,
            Reason = "expired",
            Lines = new List<SupplierReturnLineInput>
            {
                new() { ProductId = productId, BatchId = batchId, QtyEntered = 1, Unit = "base" }
            }
        });

        Assert.False(refused.Ok);
        Assert.Contains("Only the owner", refused.Error!);
    }

    // ============================== helpers ==============================

    private static SupplierLedgerService Ledger(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings), test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static ServiceResult<SupplierPayment> Pay(TestDatabase test, int supplierId, long amount) =>
        Ledger(test).Pay(TestData.Owner, supplierId, amount, "cash", "Sample payment");

    private static int BatchOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt32(test.Database.Scalar(connection,
            "SELECT id FROM batches WHERE product_id = @id ORDER BY id LIMIT 1;", new { id = productId }) ?? 0);
    }

    private static double StockOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();

        return Convert.ToDouble(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(qty_base), 0) FROM batches WHERE product_id = @id AND active = 1;",
            new { id = productId }) ?? 0d);
    }

    private static long PayableOf(TestDatabase test, int supplierId)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT balance_paisa FROM suppliers WHERE id = @id;", new { id = supplierId }) ?? 0L);
    }

    private static string CategoryOf(TestDatabase test, int customerId)
    {
        using var connection = test.Database.Open();

        return test.Database.Scalar(connection,
            "SELECT category FROM customers WHERE id = @id;", new { id = customerId })?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Rows in a table after a step. The table name is a literal at every call site, never a value
    /// typed by a user, so building the small query here is safe and keeps the assertions readable.
    /// </summary>
    private static long Count(TestDatabase test, string fromWhere) =>
        test.Database.Tx(connection => Convert.ToInt64(
            test.Database.Scalar(connection, "SELECT COUNT(1) FROM " + fromWhere + ";") ?? 0L));
}
