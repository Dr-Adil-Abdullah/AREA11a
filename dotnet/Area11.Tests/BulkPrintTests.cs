using System.Text;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Portion 11: one set of rows, one press, the right paper. The bulk switches
/// (`printer.defaultBulk`, `printer.bulkFormat`) and every new slip are covered here; the spooler
/// itself is Windows-only, so the byte shape is asserted through `ComposeBulk`, never `PrintBulk`.
/// </summary>
public sealed class BulkPrintTests
{
    // ============================== the bulk shape ==============================

    [Fact]
    public void SeparateFormatSpoolsEveryFullReceipt()
    {
        using var test = new TestDatabase();
        var first = Bill(test);
        var second = Bill(test);
        var job = History(test).BuildBulkBills(TestData.Owner, new[] { first.SaleId, second.SaleId }).Value!;

        var text = Encoding.UTF8.GetString(PrinterService.ComposeBulk(job, "separate_receipts"));

        Assert.Contains(first.Code, text);
        Assert.Contains(second.Code, text);
    }

    [Fact]
    public void A4ListSpoolsOnlyTheOneSummary()
    {
        using var test = new TestDatabase();
        var first = Bill(test);
        var second = Bill(test);
        var job = History(test).BuildBulkBills(TestData.Owner, new[] { first.SaleId, second.SaleId }).Value!;

        var bytes = PrinterService.ComposeBulk(job, "a4_list");

        Assert.Equal(EscPosBuilder.Build(job.Summary!), bytes);
    }

    [Fact]
    public void AnUnknownFormatWordFallsBackToSeparateReceipts()
    {
        using var test = new TestDatabase();
        var first = Bill(test);
        var second = Bill(test);
        var job = History(test).BuildBulkBills(TestData.Owner, new[] { first.SaleId, second.SaleId }).Value!;

        var unknown = PrinterService.ComposeBulk(job, "wall_poster");
        var separate = PrinterService.ComposeBulk(job, "separate_receipts");

        Assert.Equal(separate, unknown);
    }

    [Fact]
    public void AnEmptyJobIsRefusedBeforeTheSpooler()
    {
        using var test = new TestDatabase();
        var printer = new PrinterService(test.Settings, test.Log);

        var printed = printer.PrintBulk(new BulkPrintJob { Title = "nothing" });

        Assert.False(printed.Ok);
        Assert.Contains("nothing to print", printed.Error);
    }

    // ============================== the bulk builders ==============================

    [Fact]
    public void BulkBillsCarryEveryBillAndATotal()
    {
        using var test = new TestDatabase();
        var first = Bill(test);
        var second = Bill(test);

        var job = History(test).BuildBulkBills(TestData.Owner, new[] { first.SaleId, second.SaleId });

        Assert.True(job.Ok, job.Error);
        Assert.Equal(2, job.Value!.Documents.Count);
        Assert.Contains(first.Code, job.Value.Summary!.PlainText);
        Assert.Contains(second.Code, job.Value.Summary!.PlainText);
        Assert.Contains("TOTAL", job.Value.Summary!.PlainText);
    }

    [Fact]
    public void BulkBillsRefuseAnEmptyPick()
    {
        using var test = new TestDatabase();

        var job = History(test).BuildBulkBills(TestData.Owner, Array.Empty<int>());

        Assert.False(job.Ok);
    }

    [Fact]
    public void BulkBillsNameTheBillThatCouldNotBePrepared()
    {
        using var test = new TestDatabase();
        var sale = Bill(test);

        var job = History(test).BuildBulkBills(TestData.Owner, new[] { sale.SaleId, 999999 });

        Assert.False(job.Ok);
        Assert.Contains("999999", job.Error);
    }

    [Fact]
    public void ACashierCannotBulkPrintBills()
    {
        using var test = new TestDatabase();
        var sale = Bill(test);

        var job = History(test).BuildBulkBills(TestData.Cashier(test), new[] { sale.SaleId });

        Assert.False(job.Ok);
    }

    [Fact]
    public void CustomerBulkStatementsNameEveryCustomerAndWhatTheyOwe()
    {
        using var test = new TestDatabase();
        var ali = TestData.AddCustomer(test, "Ali Raza");
        var bilal = TestData.AddCustomer(test, "Bilal Ahmed");

        var job = TestData.Ledger(test).BuildBulkStatements(new[] { ali, bilal });

        Assert.True(job.Ok, job.Error);
        Assert.Equal(2, job.Value!.Documents.Count);
        Assert.Contains("Ali Raza", job.Value.Summary!.PlainText);
        Assert.Contains("Bilal Ahmed", job.Value.Summary!.PlainText);
        Assert.Contains("TOTAL OWED", job.Value.Summary!.PlainText);
    }

    [Fact]
    public void SupplierBulkStatementsNameEverySupplier()
    {
        using var test = new TestDatabase();
        var first = TestData.AddSupplier(test, "GSK Depot");
        var second = TestData.AddSupplier(test, "Sami Pharma");

        var job = Suppliers(test).BuildBulkStatements(new[] { first, second });

        Assert.True(job.Ok, job.Error);
        Assert.Equal(2, job.Value!.Documents.Count);
        Assert.Contains("GSK Depot", job.Value.Summary!.PlainText);
        Assert.Contains("Sami Pharma", job.Value.Summary!.PlainText);
    }

    // ============================== the new single slips ==============================

    [Fact]
    public void TheOrderPadRefusesAnEmptyPick()
    {
        using var test = new TestDatabase();

        var pad = Reorder(test).BuildPadDocument(Enumerable.Empty<ReorderLine>());

        Assert.False(pad.Ok);
    }

    [Fact]
    public void TheOrderPadListsEveryTickedLine()
    {
        using var test = new TestDatabase();

        var pad = Reorder(test).BuildPadDocument(new[]
        {
            new ReorderLine { Name = "Panadol 500mg", Quantity = 100, BaseUnit = "tablet" },
            new ReorderLine { Name = "Augmentin 625mg", Quantity = 20, BaseUnit = "tablet" }
        }, "GSK Depot");

        Assert.True(pad.Ok, pad.Error);
        Assert.Contains("Panadol 500mg", pad.Value!.PlainText);
        Assert.Contains("Augmentin 625mg", pad.Value!.PlainText);
        Assert.Contains("Total: 2 item(s).", pad.Value!.PlainText);
    }

    [Fact]
    public void ThePrintedReportHidesProfitFromAManager()
    {
        using var test = new TestDatabase();
        Bill(test);
        var manager = TestData.AddUser(test, "manager");

        var hidden = TestData.Reports(test).BuildDocument(ReportRange.Today, manager);
        var shown = TestData.Reports(test).BuildDocument(ReportRange.Today, TestData.Owner);

        Assert.True(hidden.Ok, hidden.Error);
        Assert.True(shown.Ok, shown.Error);
        Assert.DoesNotContain("Profit", hidden.Value!.PlainText);
        Assert.Contains("Profit", shown.Value!.PlainText);
    }

    [Fact]
    public void TheStockTakeSheetPrintsBookNextToCounted()
    {
        using var test = new TestDatabase();
        Stock(test);
        var session = Takes(test).Open(TestData.Owner).Value!;

        var sheet = Takes(test).BuildSheetDocument(session.Id, TestData.Owner);

        Assert.True(sheet.Ok, sheet.Error);
        Assert.Contains(session.Code, sheet.Value!.PlainText);
        Assert.Contains("book", sheet.Value!.PlainText);
    }

    [Fact]
    public void ThePurchaseInvoiceNamesTheSupplierAndTheTotal()
    {
        using var test = new TestDatabase();
        var purchase = Purchase(test);

        var invoice = TestData.Purchases(test).BuildInvoiceDocument(purchase.PurchaseId, TestData.Owner);

        Assert.True(invoice.Ok, invoice.Error);
        Assert.Contains("PURCHASE INVOICE", invoice.Value!.PlainText);
        Assert.Contains(purchase.Code, invoice.Value!.PlainText);
        Assert.Contains(Money.Format(purchase.TotalPaisa), invoice.Value!.PlainText);
    }

    [Fact]
    public void TheExpenseVoucherShowsWhatTheMoneyWasFor()
    {
        using var test = new TestDatabase();
        var saved = TestData.CashOut(test).AddExpense(TestData.Owner, new ExpenseInput
        {
            Category = "rent",
            Title = "August rent",
            AmountPaisa = 150000
        });
        Assert.True(saved.Ok, saved.Error);

        var voucher = TestData.CashOut(test).BuildVoucherDocument("expense", saved.Value!.Id, TestData.Owner);

        Assert.True(voucher.Ok, voucher.Error);
        Assert.Contains("EXPENSE VOUCHER", voucher.Value!.PlainText);
        Assert.Contains("August rent", voucher.Value!.PlainText);
    }

    // ============================== helpers ==============================

    private static SaleHistoryService History(TestDatabase test) =>
        new(test.Database,
            new ReceiptService(test.Database, test.Settings, test.Log),
            new ReturnReceiptService(test.Database, test.Settings, test.Log),
            test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit),
            test.Log);

    private static SupplierLedgerService Suppliers(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings), test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static ReorderService Reorder(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static StockTakeService Takes(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings), test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    /// <summary>One real bill through the counter, so the bulk set holds what the shop made.</summary>
    private static SaleResult Bill(TestDatabase test)
    {
        var productId = TestData.AddProduct(test);

        var created = TestData.Sales(test).Create(
            TestData.Cart(productId, 1, "cash", null), TestData.Owner);

        Assert.True(created.Ok, created.Error);

        return created.Value!;
    }

    /// <summary>Stock on the shelf, so the stock-take session freezes a real book.</summary>
    private static void Stock(TestDatabase test)
    {
        var created = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = TestData.AddSupplier(test),
            Lines = new List<PurchaseLineInput>
            {
                new()
                {
                    ProductId = TestData.AddProduct(test, name: "Panadol 500mg"),
                    Unit = "base",
                    Quantity = 10,
                    CostPaisa = 10000,
                    RetailPaisa = 15000,
                    BatchNo = "B-1",
                    Expiry = "2027-06"
                }
            }
        }, TestData.Owner);

        Assert.True(created.Ok, created.Error);
    }

    private static PurchaseResult Purchase(TestDatabase test)
    {
        var created = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = TestData.AddSupplier(test),
            PaidPaisa = 5000,
            Lines = new List<PurchaseLineInput>
            {
                new()
                {
                    ProductId = TestData.AddProduct(test, name: "Augmentin 625mg"),
                    Unit = "base",
                    Quantity = 2,
                    CostPaisa = 10000,
                    RetailPaisa = 15000,
                    BatchNo = "B-7",
                    Expiry = "2027-06"
                }
            }
        }, TestData.Owner);

        Assert.True(created.Ok, created.Error);

        return created.Value!;
    }
}
