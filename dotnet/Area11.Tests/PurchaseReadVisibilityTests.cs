using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>D-110 bounded invoice/expired-stock read projections; no supplier payment-policy change.</summary>
public sealed class PurchaseReadVisibilityTests
{
    private sealed record Seeded(int Purchase, int Product, int Batch, long Cost, long Total, long Paid);
    private static SupplierLedgerService Suppliers(TestDatabase t) => new(t.Database, t.Audit,
        new NumberingService(t.Settings), t.Settings, TestData.Permissions(t), t.Log);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static void Grant(TestDatabase t, User user, string module, bool allowed = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, module, allowed).Ok);
    private static User Staff(TestDatabase t)
    {
        var user = TestData.Cashier(t); Grant(t, user, Modules.Purchases); Grant(t, user, Modules.Suppliers); return user;
    }
    private static Seeded Seed(TestDatabase t, long cost = 12345)
    {
        var product = TestData.AddProduct(t, costPaisa: cost, batchQty: 0, name: "Boundary medicine");
        var paid = cost == 0 ? 0 : Math.Min(10000L, cost * 3);
        var purchase = TestData.Purchases(t).Create(new PurchaseInput
        {
            SupplierId = TestData.AddSupplier(t, name: "Boundary depot"), SupplierInvoiceNo = "PAPER-9",
            PaidPaisa = paid, PaidMethod = "online",
            Lines = new List<PurchaseLineInput> { new() { ProductId = product, Unit = "base", Quantity = 3,
                CostPaisa = cost, RetailPaisa = 30000, BatchNo = "P9", Expiry = "2027-06" } }
        }, TestData.Owner);
        Assert.True(purchase.Ok, purchase.Error);
        var batch = (int)Number(t, "SELECT id FROM batches WHERE batch_no='P9';");
        Execute(t, "UPDATE batches SET expiry_date='2000-01-31',expiry_ym='2000-01' WHERE batch_no='P9';");
        return new Seeded(purchase.Value!.PurchaseId, product, batch, cost, cost * 3, paid);
    }
    private static ReceiptDocument Invoice(TestDatabase t, User user, Seeded seed)
    {
        var result = TestData.Purchases(t).BuildInvoiceDocument(seed.Purchase, user);
        Assert.True(result.Ok, result.Error); return result.Value!;
    }
    private static ExpiredBatchRow Expired(TestDatabase t, User user, Seeded seed)
    {
        var result = Suppliers(t).ExpiredBatches(user); Assert.True(result.Ok, result.Error);
        return result.Value!.Single(row => row.BatchId == seed.Batch);
    }
    private static void HiddenInvoice(ReceiptDocument doc, Seeded seed)
    {
        Assert.Contains("Boundary medicine", doc.PlainText); Assert.Contains("Boundary depot", doc.PlainText);
        Assert.Contains("P9", doc.PlainText); Assert.Contains("PAPER-9", doc.PlainText);
        Assert.Contains(doc.Lines, row => row.Text.StartsWith("Total") && row.Text.EndsWith("hidden"));
        Assert.Contains(doc.Lines, row => row.Text.StartsWith("Paid") && row.Text.EndsWith("hidden"));
        Assert.Contains(doc.Lines, row => row.Text.StartsWith("Due") && row.Text.EndsWith("hidden"));
        Assert.True(doc.Lines.Count(row => row.Text.Contains("hidden")) >= 4);
        if (seed.Total > 0) Assert.DoesNotContain(Money.Format(seed.Total), doc.PlainText);
        if (seed.Paid > 0) Assert.DoesNotContain(Money.Format(seed.Paid), doc.PlainText);
        if (seed.Total > seed.Paid) Assert.DoesNotContain(Money.Format(seed.Total - seed.Paid), doc.PlainText);
    }
    private static void HiddenExpired(ExpiredBatchRow row, Seeded seed)
    {
        Assert.False(row.CostKnown); Assert.Equal(0L, row.CostPaisa); Assert.Equal(0L, row.ValuePaisa);
        Assert.Equal("hidden", row.ValueLabel); Assert.Equal(3d, row.QtyBase);
        Assert.Equal(seed.Product, row.ProductId); Assert.Equal("P9", row.BatchNo);
        Assert.Equal("2000-01-31", row.ExpiryDate); Assert.Contains("Boundary medicine", row.Caption);
    }

    [Fact]
    public void AnonymousAndForgedInvoiceReadersRefuseBeforeDocumentPreparation()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t); var service = TestData.Purchases(t);
        Assert.False(service.BuildInvoiceDocument(seed.Purchase, null).Ok);
        Assert.False(service.BuildInvoiceDocument(seed.Purchase, new User { Id = user.Id, Role = "owner" }).Ok);
        Assert.False(service.BuildInvoiceDocument(seed.Purchase, new User { Id = int.MaxValue, Role = "owner" }).Ok);
    }

    [Fact]
    public void InvoiceRequiresPurchasesModuleNotJustCostOrSupplierAccess()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t);
        Grant(t, user, Modules.PurchaseCost); Grant(t, user, Modules.Purchases, false);
        Assert.False(TestData.Purchases(t).BuildInvoiceDocument(seed.Purchase, user).Ok);
        Assert.True(Expired(t, user, seed).CostKnown);
    }

    [Fact]
    public void ManagerHasNoAutomaticInvoiceMoneyVisibility()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var manager = TestData.AddUser(t, "manager");
        HiddenInvoice(Invoice(t, manager, seed), seed);
    }

    [Fact]
    public void ProfitOnlyCannotRevealInvoiceLineTotalPaidOrDue()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t); Grant(t, user, Modules.Profit);
        HiddenInvoice(Invoice(t, user, seed), seed);
    }

    [Fact]
    public void CostOnlyInvoiceShowsOriginalTotalsAndPaymentMethodWithoutProfitGrant()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t); Grant(t, user, Modules.PurchaseCost);
        var text = Invoice(t, user, seed).PlainText;
        Assert.Contains(Money.Format(seed.Total), text); Assert.Contains(Money.Format(seed.Paid), text);
        Assert.Contains(Money.Format(seed.Total - seed.Paid), text); Assert.Contains("Paid (online)", text);
        Assert.DoesNotContain("hidden", text); Assert.False(TestData.Permissions(t).Can(user, Modules.Profit));
    }

    [Fact]
    public void MainOffMasksOwnerInvoiceAndOnRestoresWithoutReposting()
    {
        using var t = new TestDatabase(); var seed = Seed(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        HiddenInvoice(Invoice(t, TestData.Owner, seed), seed);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        Assert.Contains(Money.Format(seed.Total), Invoice(t, TestData.Owner, seed).PlainText);
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM purchases;"));
    }

    [Fact]
    public void SameInvoiceServiceUsesNextReadCostRevocation()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t); Grant(t, user, Modules.PurchaseCost);
        var service = TestData.Purchases(t);
        Assert.DoesNotContain("hidden", service.BuildInvoiceDocument(seed.Purchase, user).Value!.PlainText);
        Grant(t, user, Modules.PurchaseCost, false);
        HiddenInvoice(service.BuildInvoiceDocument(seed.Purchase, user).Value!, seed);
    }

    [Fact]
    public void InactiveStoredAccountCannotPrepareExistingInvoice()
    {
        using var t = new TestDatabase(); var seed = Seed(t);
        Execute(t, "UPDATE users SET active=0 WHERE id=1;");
        Assert.False(TestData.Purchases(t).BuildInvoiceDocument(seed.Purchase, TestData.Owner).Ok);
    }

    [Fact]
    public void ChangedRoleAndInactiveCallerSnapshotsCannotPrepareInvoice()
    {
        using var t = new TestDatabase(); var seed = Seed(t);
        Assert.False(TestData.Purchases(t).BuildInvoiceDocument(seed.Purchase, new User { Id = 1, Role = "owner", Active = false }).Ok);
        Execute(t, "UPDATE users SET role='manager' WHERE id=1;");
        Assert.False(TestData.Purchases(t).BuildInvoiceDocument(seed.Purchase, TestData.Owner).Ok);
    }

    [Fact]
    public void MissingInvoiceStillNeedsCurrentAuthorizedReader()
    {
        using var t = new TestDatabase(); var service = TestData.Purchases(t);
        Assert.False(service.BuildInvoiceDocument(int.MaxValue, null).Ok);
        var missing = service.BuildInvoiceDocument(int.MaxValue, TestData.Owner);
        Assert.False(missing.Ok); Assert.Contains("no longer exists", missing.Error ?? string.Empty);
    }

    [Fact]
    public void ZeroCostInvoiceIsNotReplacedByHiddenWhenAuthorized()
    {
        using var t = new TestDatabase(); var seed = Seed(t, cost: 0);
        var visible = Invoice(t, TestData.Owner, seed);
        Assert.Contains(Money.Format(0), visible.PlainText); Assert.DoesNotContain("hidden", visible.PlainText);
        Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        HiddenInvoice(Invoice(t, TestData.Owner, seed), seed);
    }

    [Fact]
    public void UnpaidInvoiceStillHidesBothPaidAndDueRatherThanLeakingZeroShape()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t);
        Execute(t, "UPDATE purchases SET paid_paisa=0,due_paisa=total_paisa;");
        HiddenInvoice(Invoice(t, user, seed), seed with { Paid = 0 });
    }

    [Fact]
    public void ExpiredListStillRequiresSupplierModuleEvenWithCostGrant()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t);
        Grant(t, user, Modules.PurchaseCost); Grant(t, user, Modules.Suppliers, false);
        Assert.False(Suppliers(t).ExpiredBatches(user).Ok);
    }

    [Fact]
    public void ManagerExpiredListRetainsQuantityButMasksRateAndValuation()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var manager = TestData.AddUser(t, "manager");
        HiddenExpired(Expired(t, manager, seed), seed);
    }

    [Fact]
    public void ProfitGrantDoesNotRevealExpiredBatchCost()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t); Grant(t, user, Modules.Profit);
        HiddenExpired(Expired(t, user, seed), seed);
    }

    [Fact]
    public void CostGrantRevealsExactExpiredRateAndAggregate()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t); Grant(t, user, Modules.PurchaseCost);
        var row = Expired(t, user, seed);
        Assert.True(row.CostKnown); Assert.Equal(seed.Cost, row.CostPaisa); Assert.Equal(seed.Total, row.ValuePaisa);
        Assert.Equal(Money.Format(seed.Total), row.ValueLabel);
    }

    [Fact]
    public void MainOffMasksExpiredOwnerRawCostAndLabel()
    {
        using var t = new TestDatabase(); var seed = Seed(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        HiddenExpired(Expired(t, TestData.Owner, seed), seed);
    }

    [Fact]
    public void SameExpiredServiceUsesCurrentGrantRevocation()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t); Grant(t, user, Modules.PurchaseCost);
        var service = Suppliers(t); Assert.True(service.ExpiredBatches(user).Value!.Single().CostKnown);
        Grant(t, user, Modules.PurchaseCost, false);
        HiddenExpired(service.ExpiredBatches(user).Value!.Single(), seed);
    }

    [Fact]
    public void ClosedAccountsCannotReadExpiredMetadataEither()
    {
        using var t = new TestDatabase(); Seed(t); var user = Staff(t);
        Execute(t, "UPDATE users SET active=0 WHERE role='cashier';");
        Assert.False(Suppliers(t).ExpiredBatches(user).Ok);
        Assert.False(Suppliers(t).ExpiredBatches(new User { Id = 1, Role = "cashier" }).Ok);
    }

    [Fact]
    public void AuthorizedActualZeroExpiredCostIsKnownNotHidden()
    {
        using var t = new TestDatabase(); var seed = Seed(t, cost: 0); var row = Expired(t, TestData.Owner, seed);
        Assert.True(row.CostKnown); Assert.Equal(0L, row.CostPaisa); Assert.Equal(0L, row.ValuePaisa);
        Assert.Equal(Money.Format(0), row.ValueLabel);
        Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        HiddenExpired(Expired(t, TestData.Owner, seed), seed);
    }

    [Fact]
    public void ExpiredFilterKeepsItsExistingInactiveAndUnexpiredExclusions()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t);
        TestData.AddProduct(t, batchQty: 9, name: "Unexpired medicine");
        Assert.Single(Suppliers(t).ExpiredBatches(user).Value!);
        Execute(t, "UPDATE batches SET active=0 WHERE batch_no='P9';");
        Assert.Empty(Suppliers(t).ExpiredBatches(user).Value!);
    }

    [Fact]
    public void OwnerExplicitCostDenialAppliesToInvoiceAndExpiredReads()
    {
        using var t = new TestDatabase(); var seed = Seed(t); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        HiddenInvoice(Invoice(t, TestData.Owner, seed), seed); HiddenExpired(Expired(t, TestData.Owner, seed), seed);
    }

    [Fact]
    public void FinancialGrantDatabaseFailureDoesNotFallBackToOwnerCost()
    {
        using var t = new TestDatabase(); var seed = Seed(t); Execute(t, "DROP TABLE user_permissions;");
        HiddenInvoice(Invoice(t, TestData.Owner, seed), seed); HiddenExpired(Expired(t, TestData.Owner, seed), seed);
    }

    [Fact]
    public void MaskedReadsDoNotRewriteMoneyStockPaymentsOrAudits()
    {
        using var t = new TestDatabase(); var seed = Seed(t); var user = Staff(t);
        var tables = new[] { "purchases", "purchase_items", "batches", "payments", "suppliers", "stock_movements", "audit_logs" };
        var before = tables.ToDictionary(table => table, table => Number(t, "SELECT COUNT(*) FROM " + table + ";"));
        HiddenInvoice(Invoice(t, user, seed), seed); HiddenExpired(Expired(t, user, seed), seed);
        foreach (var table in tables) Assert.Equal(before[table], Number(t, "SELECT COUNT(*) FROM " + table + ";"));
        Assert.Equal(seed.Total, Number(t, "SELECT total_paisa FROM purchases;"));
        Assert.Equal(seed.Cost, Number(t, "SELECT cost_paisa FROM purchase_items;"));
        Assert.Equal(seed.Total - seed.Paid, Number(t, "SELECT balance_paisa FROM suppliers;"));
        Assert.Equal(3L, Number(t, "SELECT SUM(qty_base) FROM batches;"));
    }
}
