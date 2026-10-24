using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The bill history and its reprint (F-52, Spec 12.3): find a bill, open it, and put the same paper in the
/// customer's hand again.
///
/// Why these tests matter more than they look: the reprint is the only place in the shop where paper can be
/// produced without money moving, so the numbers on it must come from the stored rows (never from today's
/// prices), and the copy must leave a line in the black box.
/// </summary>
public sealed class SaleHistoryTests
{
    // ============================== finding a bill ==============================

    [Fact]
    public void ABillIsFoundByItsNumber()
    {
        using var test = new TestDatabase();
        var sale = Bill(test);

        var page = History(test).Search(TestData.Owner, new SaleHistoryFilter { Search = sale.Code }).Value!;

        Assert.False(page.IsEmpty);
        Assert.Equal(sale.Code, page.Rows[0].Code);
        Assert.Equal(sale.TotalPaisa, page.Rows[0].TotalPaisa);
        Assert.Equal("Paid", page.Rows[0].StatusLabel);
    }

    [Fact]
    public void ABillIsFoundByTheCustomersName()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test, "Ali Raza");
        var sale = Bill(test, customerId: customer);

        var page = History(test).Search(TestData.Owner, new SaleHistoryFilter { Search = "ali raza" }).Value!;

        Assert.Single(page.Rows);
        Assert.Equal(sale.Code, page.Rows[0].Code);
        Assert.Equal("Ali Raza", page.Rows[0].CustomerLabel);
    }

    [Fact]
    public void TheDefaultRangeLeavesOutAnOldBill()
    {
        using var test = new TestDatabase();
        OldBill(test, "INV-OLD-1", daysAgo: 200);

        var page = History(test).Search(TestData.Owner, new SaleHistoryFilter()).Value!;

        Assert.True(page.IsEmpty);

        var wider = History(test).Search(TestData.Owner, new SaleHistoryFilter
        {
            From = Clock.DateDaysAgo(400),
            To = Clock.Today()
        }).Value!;

        Assert.Single(wider.Rows);
        Assert.Equal("INV-OLD-1", wider.Rows[0].Code);
    }

    [Fact]
    public void TheNewestBillComesFirst()
    {
        using var test = new TestDatabase();
        var first = Bill(test);
        var second = Bill(test);

        var page = History(test).Search(TestData.Owner, new SaleHistoryFilter()).Value!;

        Assert.Equal(2, page.Count);
        Assert.Equal(second.SaleId, page.Rows[0].Id);
        Assert.Equal(first.SaleId, page.Rows[1].Id);
    }

    [Fact]
    public void TheTotalsOfTheListAreTheSumOfItsRows()
    {
        using var test = new TestDatabase();
        var first = Bill(test, qty: 1);
        var second = Bill(test, qty: 2);

        var page = History(test).Search(TestData.Owner, new SaleHistoryFilter()).Value!;

        Assert.Equal(2, page.Count);
        Assert.Equal(first.TotalPaisa + second.TotalPaisa, page.TotalPaisa);
        Assert.Equal(first.DuePaisa + second.DuePaisa, page.DuePaisa);
    }

    [Fact]
    public void ABadDateIsASentenceNotAnEmptyShop()
    {
        using var test = new TestDatabase();

        var page = History(test).Search(TestData.Owner,
            new SaleHistoryFilter { From = "31-12-2026" }).Value!;

        Assert.NotNull(page.FilterProblem);
        Assert.Contains("2026-10-26", page.FilterProblem);
        Assert.True(page.IsEmpty);
    }

    [Fact]
    public void AReversedRangeIsRefused()
    {
        using var test = new TestDatabase();

        var page = History(test).Search(TestData.Owner, new SaleHistoryFilter
        {
            From = Clock.Today(),
            To = Clock.DateDaysAgo(30)
        }).Value!;

        Assert.NotNull(page.FilterProblem);
        Assert.Contains("after", page.FilterProblem);
    }

    [Fact]
    public void ACashierCannotOpenTheBillHistory()
    {
        using var test = new TestDatabase();
        Bill(test);

        var cashier = TestData.Cashier(test);
        var history = History(test);

        Assert.False(history.CanLook(cashier));
        Assert.False(history.Search(cashier, new SaleHistoryFilter()).Ok);
        Assert.False(history.Open(cashier, 1).Ok);
    }

    // ============================== opening a bill ==============================

    [Fact]
    public void TheOpenedBillShowsItsLinesAndItsMoney()
    {
        using var test = new TestDatabase();
        var sale = Bill(test, qty: 2);

        var detail = History(test).Open(TestData.Owner, sale.SaleId).Value!;

        Assert.Equal(sale.Code, detail.Sale.Code);
        Assert.Single(detail.Lines);
        Assert.Equal(Money.Format(sale.TotalPaisa), detail.TotalLabel);
        Assert.False(detail.HasReturns);
        Assert.True(detail.Lines[0].QtyLabel.Contains("2", StringComparison.Ordinal));
    }

    [Fact]
    public void AKhataBillShowsWhatIsStillOwed()
    {
        using var test = new TestDatabase();
        var customer = TestData.AddCustomer(test);
        var sale = Bill(test, method: "credit", customerId: customer);

        var row = History(test).Search(TestData.Owner, new SaleHistoryFilter()).Value!.Rows[0];

        Assert.True(row.HasDue);
        Assert.Equal(sale.DuePaisa, row.DuePaisa);
        Assert.NotEqual("-", row.DueLabel);
        Assert.Equal("Khata", row.MethodLabel);
    }

    // ============================== printing it again ==============================

    [Fact]
    public void TheReprintIsBuiltByTheSameBuilderAsTheOriginal()
    {
        using var test = new TestDatabase();
        var sale = Bill(test);
        var history = History(test);

        var again = history.BuildReceipt(TestData.Owner, sale.SaleId).Value!;
        var original = new ReceiptService(test.Database, test.Settings, test.Log).Build(sale.SaleId).Value!;

        // Same paper, because a reprint is not a new document - it is the stored bill, rendered again.
        Assert.Equal(original.PlainText, again.PlainText);
        Assert.Contains(sale.Code, again.PlainText);
    }

    [Fact]
    public void EveryPrintedLineFitsThePaper()
    {
        using var test = new TestDatabase();
        var sale = Bill(test);

        var document = History(test).BuildReceipt(TestData.Owner, sale.SaleId).Value!;

        Assert.All(document.Lines, line => Assert.True(line.Text.Length <= document.Width,
            $"'{line.Text}' is {line.Text.Length} characters on {document.Width}"));
    }

    [Fact]
    public void TheReprintIsWrittenIntoTheBlackBox()
    {
        using var test = new TestDatabase();
        var sale = Bill(test);
        var history = History(test);

        Assert.True(history.RecordReprint(TestData.Owner, sale.SaleId).Ok);

        Assert.Equal(1, Audits(test, "sale_reprint"));
        Assert.Contains(sale.Code, AuditDetails(test, "sale_reprint"));
        Assert.Contains(Money.Format(sale.TotalPaisa), AuditDetails(test, "sale_reprint"));
    }

    [Fact]
    public void AReturnShowsOnItsBillAndItsReceiptCanBePrintedAgain()
    {
        using var test = new TestDatabase();
        var sale = Bill(test, qty: 3);

        var returned = TestData.Returns(test).Create(TestData.Return(sale, 1), TestData.Owner);
        Assert.True(returned.Ok, returned.Error);

        var detail = History(test).Open(TestData.Owner, sale.SaleId).Value!;

        Assert.True(detail.HasReturns);
        Assert.Single(detail.Returns);
        Assert.Equal(returned.Value!.Code, detail.Returns[0].Code);
        Assert.True(detail.Returns[0].CanPrint);

        var paper = History(test).BuildReturnReceipt(TestData.Owner, detail.Returns[0].Code);
        Assert.True(paper.Ok, paper.Error);
    }

    // ============================== helpers ==============================

    private static SaleHistoryService History(TestDatabase test) =>
        new(test.Database,
            new ReceiptService(test.Database, test.Settings, test.Log),
            new ReturnReceiptService(test.Database, test.Settings, test.Log),
            test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit),
            test.Log);

    /// <summary>One real bill through the counter, so the history sees exactly what the shop made.</summary>
    private static SaleResult Bill(
        TestDatabase test,
        int qty = 1,
        string method = "cash",
        int? customerId = null)
    {
        var productId = TestData.AddProduct(test);

        var created = TestData.Sales(test).Create(
            TestData.Cart(productId, qty, method, customerId), TestData.Owner);

        Assert.True(created.Ok, created.Error);

        return created.Value!;
    }

    /// <summary>A bill from the past, written straight into the table - the counter cannot back-date.</summary>
    private static void OldBill(TestDatabase test, string code, int daysAgo)
    {
        using var connection = test.Database.Open();

        test.Database.Execute(connection, @"
INSERT INTO sales(code, date, subtotal_paisa, discount_paisa, total_paisa, paid_paisa, status, payment_method, created_at)
VALUES(@code, @date, 5000, 0, 5000, 5000, 'paid', 'cash', @date);",
            new { code, date = Clock.DateDaysAgo(daysAgo) + " 10:00:00" });
    }

    private static long Audits(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action }) ?? 0L);
    }

    private static string AuditDetails(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return test.Database.Scalar(connection,
            "SELECT details FROM audit_logs WHERE action = @action ORDER BY id DESC LIMIT 1;",
            new { action }) as string ?? string.Empty;
    }
}
