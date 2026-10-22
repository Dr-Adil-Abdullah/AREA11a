using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Tests;

/// <summary>
/// Shared test data: one medicine with one batch, a customer, and the services wired exactly as
/// AppServices wires them. Keeping it here means a change in a service constructor breaks in one
/// place instead of in every test file.
/// </summary>
internal static class TestData
{
    public static readonly User Owner = new() { Id = 1, Name = "Owner", Role = "owner" };

    /// <summary>Adds a medicine that has stock sitting on one batch (expiry far away).</summary>
    public static int AddProduct(
        TestDatabase test,
        long costPaisa = 20000,
        long retailPaisa = 30000,
        double batchQty = 100,
        string name = "Panadol 500mg")
    {
        return test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO products(name, base_unit, box_strips, strip_tablets, cost_paisa, retail_paisa,
                     vip_paisa, doctor_paisa, reorder_level, active, created_at, updated_at)
VALUES(@name, 'tablet', 2, 10, @cost, @retail, 0, 0, 10, 1, @at, @at);",
                new { name, cost = costPaisa, retail = retailPaisa, at = Clock.Now() });

            var productId = (int)test.Database.NextId(connection);

            test.Database.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    is_sample, active, created_at)
VALUES(@productId, 'B-1', '2027-06-30', '2027-06', @qty, @cost, @retail, 0, 1, @at);",
                new { productId, qty = batchQty, cost = costPaisa, retail = retailPaisa, at = Clock.Now() });

            return productId;
        });
    }

    /// <summary>
    /// Adds a customer. A limit of 0 means "no limit" (see SaleService.EnforceCreditLimit).
    /// The phone is a parameter because the khata screen searches by it, and F-11 tests a shared one.
    /// </summary>
    public static int AddCustomer(
        TestDatabase test,
        string name = "Ali Raza",
        long limitPaisa = 0,
        string? phone = "0300-1234567")
    {
        return test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO customers(name, phone, category, credit_limit_paisa, balance_paisa, loyalty_points, active, created_at)
VALUES(@name, @phone, 'normal', @limit, 0, 0, 1, @at);",
                new { name, phone, limit = limitPaisa, at = Clock.Now() });

            return (int)test.Database.NextId(connection);
        });
    }

    /// <summary>A supplier with an optional opening payable - the same row the purchase screen creates.</summary>
    public static int AddSupplier(TestDatabase test, string name = "GSK Depot", long openingPaisa = 0)
    {
        return test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO suppliers(name, agency, phone, opening_balance_paisa, balance_paisa, active, created_at)
VALUES(@name, 'GSK', '042-1112233', @opening, @opening, 1, '2026-01-01 09:00:00');",
                new { name, opening = openingPaisa });

            return (int)test.Database.NextId(connection);
        });
    }

    /// <summary>
    /// Adds a real person (the row, not just an object) and returns the signed-in user. The row is
    /// needed because `sales.user_id` is a foreign key: a made-up id would fail the insert.
    /// </summary>
    public static User AddUser(TestDatabase test, string role)
    {
        var id = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO users(name, role, active, created_at) VALUES(@name, @role, 1, @at);",
                new { name = char.ToUpperInvariant(role[0]) + role[1..], role, at = Clock.Now() });

            return (int)test.Database.NextId(connection);
        });

        return new User { Id = id, Name = char.ToUpperInvariant(role[0]) + role[1..], Role = role };
    }

    /// <summary>A cashier: makes bills, cannot take money out of the shop.</summary>
    public static User Cashier(TestDatabase test) => AddUser(test, "cashier");

    public static PurchaseService Purchases(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings),
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    /// <summary>
    /// The alert board. One factory, so a service constructor that gains a dependency is fixed here
    /// and not in three test files (CS7036 at v0.4.3 came from exactly that copy-paste).
    /// </summary>
    public static AlertService Alerts(TestDatabase test) =>
        new(test.Database, test.Settings, test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    /// <summary>Expenses and owner drawings (F-32). One factory: a constructor change is fixed here once.</summary>
    public static ExpenseService CashOut(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    /// <summary>The khata book (F-27). One factory: Collect is used by several test files.</summary>
    public static CustomerLedgerService Ledger(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    /// <summary>Reports (F-34). One factory, so a constructor change breaks in one place.</summary>
    public static ReportService Reports(TestDatabase test) =>
        new(test.Database, new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    /// <summary>The reading side of the black box (F-35). One factory, one constructor change.</summary>
    public static AuditHistoryService AuditHistory(TestDatabase test) =>
        new(test.Database, new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    /// <summary>Permissions, for the tests that grant one person an extra module.</summary>
    public static PermissionService Permissions(TestDatabase test) =>
        new(test.Database, test.Log, test.Audit);

    /// <summary>Old udhaar ageing and the recovery slip (F-33). One factory, one constructor change.</summary>
    public static RecoveryService Recovery(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    public static SaleService Sales(TestDatabase test)
    {
        var permissions = new PermissionService(test.Database, test.Log, test.Audit);
        return new SaleService(test.Database, test.Audit, new NumberingService(test.Settings),
            test.Settings, permissions, test.Log);
    }

    public static HoldCartService Holds(TestDatabase test, SaleService? sales = null)
    {
        var permissions = new PermissionService(test.Database, test.Log, test.Audit);
        return new HoldCartService(test.Database, test.Audit, new NumberingService(test.Settings),
            permissions, sales ?? Sales(test), test.Log);
    }

    public static ReturnService Returns(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings), test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    public static QuarantineService Quarantine(TestDatabase test) =>
        new(test.Database, test.Audit, new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    public static ReturnReceiptService ReturnReceipts(TestDatabase test) =>
        new(test.Database, test.Settings, test.Log);

    public static ProvisionalReturnService Provisionals(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings),
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    public static ProvisionalReceiptService ProvisionalReceipts(TestDatabase test) =>
        new(Provisionals(test), test.Settings, test.Log);

    /// <summary>One-medicine no-bill return, quantity in the unit the customer used.</summary>
    public static ProvisionalInput RushReturn(
        int productId,
        double qty = 1,
        string unit = "box",
        long refundPaisa = 60000,
        string? name = "Ali Raza",
        string? phone = "0300-1234567") => new()
    {
        CustomerName = name,
        Phone = phone,
        RefundPaisa = refundPaisa,
        Reason = "changed mind",
        Lines = new List<ProvisionalLineInput>
        {
            new() { ProductId = productId, QtyEntered = qty, Unit = unit }
        }
    };

    /// <summary>The only line of a one-line bill, ready to be returned in full.</summary>
    public static ReturnInput Return(SaleResult sale, double qtyBase, bool restock = true, string reason = "changed mind")
    {
        var itemId = SaleItemIdOf(sale);

        return new ReturnInput
        {
            SaleId = sale.SaleId,
            Reason = reason,
            Restock = restock,
            Lines = new List<ReturnLineInput> { new() { SaleItemId = itemId, QtyBase = qtyBase } }
        };
    }

    /// <summary>Reads the single sale-item id of a one-line bill (the tests never guess ids).</summary>
    public static int SaleItemIdOf(SaleResult sale) => sale.SaleItemIds.Count > 0
        ? sale.SaleItemIds[0]
        : throw new InvalidOperationException("The sale has no lines.");

    /// <summary>One row of the `payments` table, for the split-payment assertions.</summary>
    public sealed class PaymentDbRow
    {
        public string Method { get; set; } = string.Empty;

        public long AmountPaisa { get; set; }
    }

    /// <summary>One line, one unit, no discount - the plainest possible bill.</summary>
    public static SaleInput Cart(int productId, int qty = 1, string method = "cash", int? customerId = null) => new()
    {
        CustomerId = customerId,
        PaymentMethod = method,
        Lines = new List<SaleLineInput>
        {
            new() { ProductId = productId, Unit = "base", Quantity = qty }
        }
    };
}
