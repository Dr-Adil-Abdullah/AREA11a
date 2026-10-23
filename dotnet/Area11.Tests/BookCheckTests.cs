using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The "do the books add up?" check (F-54).
///
/// Why every rule is tested twice - once on a clean shop and once with a fault written straight
/// into the database: a check that never fails is not a check, it is a green light with no bulb.
/// The clean half proves the rule does not invent faults (a false alarm on a money screen is worse
/// than no screen at all, because it teaches the owner to ignore the next one); the broken half
/// proves the rule can see the fault at all, and that it names the document rather than a row id.
///
/// The faults are written with a plain UPDATE/INSERT on purpose: they stand for the two things that
/// really happen in a shop - an old import that was not written by this program, and a file that
/// was edited by hand. Nothing in the program can produce them, which is exactly why the check has
/// to look for them from the outside.
/// </summary>
public sealed class BookCheckTests
{
    private static BookCheckService Check(TestDatabase test) =>
        new(test.Database, TestData.Permissions(test), test.Log);

    private static BookCheckReport Run(TestDatabase test, User? user = null)
    {
        var result = Check(test).Run(user ?? TestData.Owner);

        Assert.True(result.Ok, result.Error);

        return result.Value!;
    }

    /// <summary>The rule with this title, or a failure that says which titles do exist.</summary>
    private static BookCheckRule Rule(BookCheckReport report, string title)
    {
        var rule = report.Rules.FirstOrDefault(item => item.Title == title);

        Assert.True(rule is not null,
            "No rule called '" + title + "'. The rules are: "
            + string.Join(" | ", report.Rules.Select(item => item.Title)));

        return rule!;
    }

    /// <summary>Makes one real bill for one strip: Rs 300, paid in cash.</summary>
    private static string MakeBill(TestDatabase test, out int productId)
    {
        productId = TestData.AddProduct(test);

        var input = new SaleInput
        {
            PaymentMethod = "cash",
            Lines = new List<SaleLineInput>
            {
                new() { ProductId = productId, Unit = "base", Quantity = 1 }
            }
        };

        var sale = TestData.Sales(test).Create(input, TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(30000L, sale.Value!.TotalPaisa);

        return sale.Value.Code;
    }

    [Fact]
    public void AFreshShopHasNothingToComplainAbout()
    {
        using var test = new TestDatabase();

        var report = Run(test);

        // Eleven rules, every one of them with a sentence: a rule the owner cannot read is a number
        // he cannot act on, and the first question he asks is "what does zero mean here?".
        Assert.Equal(11, report.Rules.Count);
        Assert.All(report.Rules, rule =>
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Title));
            Assert.False(string.IsNullOrWhiteSpace(rule.Why));
        });

        Assert.Equal(0, report.Problems);
        Assert.True(report.Clean);
        Assert.Contains("Everything adds up", report.Headline);
    }

    [Fact]
    public void EveryRuleHoldsOnARealBillAndARealPurchase()
    {
        using var test = new TestDatabase();

        var code = MakeBill(test, out _);

        var supplierId = TestData.AddSupplier(test);
        var productId = TestData.AddProduct(test, name: "Augmentin 625mg");

        var purchase = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = supplierId,
            PaidPaisa = 5000,
            Lines = new List<PurchaseLineInput>
            {
                new()
                {
                    ProductId = productId,
                    Unit = "base",
                    Quantity = 2,
                    CostPaisa = 10000,
                    RetailPaisa = 15000,
                    BatchNo = "B-7",
                    Expiry = "2027-06"
                }
            }
        }, TestData.Owner);

        Assert.True(purchase.Ok, purchase.Error);

        var report = Run(test);

        // The bill is really there and the rules really read it - a clean report on an empty shop
        // would pass even if every query returned nothing.
        Assert.Equal(1, Rule(report, "A bill's lines add up to the bill").Checked);
        Assert.Equal(1, Rule(report, "A purchase's lines add up to its subtotal").Checked);
        Assert.Equal("INV-0001", code);
        Assert.Equal(0, report.Problems);
    }

    [Fact]
    public void ABillWhoseTotalIsNotItsLinesIsNamedWithTheGap()
    {
        using var test = new TestDatabase();
        var code = MakeBill(test, out _);

        // Rs 5 was added to the bill's total and to nothing else - the shape of a hand-edited file.
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE sales SET total_paisa = total_paisa + 500 WHERE code = @code;", new { code }));

        var report = Run(test);

        var finding = Assert.Single(Rule(report, "A bill's lines add up to the bill").Findings);

        Assert.Equal(code, finding.Code);
        Assert.Equal(500L, finding.DifferencePaisa);
        Assert.Contains("missing", finding.GapText);

        // The same rupee is caught by the header rule too: a total that does not come out of its own
        // header is the same fault seen from the other side, and both are reported rather than one.
        Assert.Single(Rule(report, "A bill's header adds up").Findings);

        // The same rupee is seen from three sides - the lines, the header and "paid or owed" - and
        // all three are reported. One root cause is one fault for a reader, but three rules really
        // did fail, and hiding two of them would hide which rows disagree.
        Assert.Equal(3, report.Problems);
    }

    [Fact]
    public void MoneyRowsThatDoNotMatchTheBillAreNamed()
    {
        using var test = new TestDatabase();
        var code = MakeBill(test, out _);

        // The cash row is gone, but the bill still says it was paid. That is a drawer that cannot
        // balance no matter how carefully it is counted.
        test.Database.Tx(connection => test.Database.Execute(connection,
            "DELETE FROM payments WHERE sale_id = (SELECT id FROM sales WHERE code = @code);",
            new { code }));

        var report = Run(test);

        var finding = Assert.Single(Rule(report, "A bill's money rows match what it says was paid").Findings);

        Assert.Equal(code, finding.Code);
        Assert.Equal(30000L, finding.ExpectedPaisa);
        Assert.Equal(0L, finding.FoundPaisa);
    }

    [Fact]
    public void APurchaseWhoseLinesDoNotAddUpIsNamed()
    {
        using var test = new TestDatabase();

        var supplierId = TestData.AddSupplier(test);
        var productId = TestData.AddProduct(test);

        var purchase = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = supplierId,
            Lines = new List<PurchaseLineInput>
            {
                new()
                {
                    ProductId = productId,
                    Unit = "base",
                    Quantity = 3,
                    CostPaisa = 10000,
                    RetailPaisa = 15000,
                    BatchNo = "B-3",
                    Expiry = "2027-06"
                }
            }
        }, TestData.Owner);

        Assert.True(purchase.Ok, purchase.Error);

        // The header keeps its own arithmetic (subtotal, total and due all move together); only the
        // rows underneath no longer reach the subtotal - the shape of a bill whose lines were edited.
        test.Database.Tx(connection => test.Database.Execute(connection, @"
UPDATE purchases
SET subtotal_paisa = subtotal_paisa - 100, total_paisa = total_paisa - 100, due_paisa = due_paisa - 100
WHERE code = @code;", new { code = purchase.Value!.Code }));

        var report = Run(test);

        var finding = Assert.Single(Rule(report, "A purchase's lines add up to its subtotal").Findings);

        Assert.Equal(purchase.Value!.Code, finding.Code);
        Assert.Equal(19900L, finding.ExpectedPaisa);
        Assert.Equal(20000L, finding.FoundPaisa);
        Assert.Equal(-100L, finding.DifferencePaisa);

        // The header rule is a DIFFERENT identity and must stay clean here: three rules were not
        // written to say the same thing three ways.
        Assert.True(Rule(report, "A purchase's header adds up").Clean);
    }

    [Fact]
    public void APurchaseHeaderThatDoesNotAddUpIsNamed()
    {
        using var test = new TestDatabase();

        var supplierId = TestData.AddSupplier(test);
        var productId = TestData.AddProduct(test);

        var purchase = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = supplierId,
            Lines = new List<PurchaseLineInput>
            {
                new()
                {
                    ProductId = productId,
                    Unit = "base",
                    Quantity = 2,
                    CostPaisa = 10000,
                    RetailPaisa = 15000,
                    BatchNo = "B-4",
                    Expiry = "2027-06"
                }
            }
        }, TestData.Owner);

        Assert.True(purchase.Ok, purchase.Error);

        // Rs 5 added to what the shop owes, with nothing to explain it - a supplier discount that was
        // typed in the wrong column, or a file written by another program.
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE purchases SET total_paisa = total_paisa + 500, due_paisa = due_paisa + 500 WHERE code = @code;",
            new { code = purchase.Value!.Code }));

        var report = Run(test);

        var header = Assert.Single(Rule(report, "A purchase's header adds up").Findings);

        Assert.Equal(purchase.Value!.Code, header.Code);
        Assert.Equal(20000L, header.ExpectedPaisa);
        Assert.Equal(20500L, header.FoundPaisa);

        // The lines still reach the subtotal - only the header moved.
        Assert.True(Rule(report, "A purchase's lines add up to its subtotal").Clean);
    }

    [Fact]
    public void AKhataBelowZeroIsNamed()
    {
        using var test = new TestDatabase();

        var customerId = TestData.AddCustomer(test, name: "Bilal");

        // No command in the program can do this: `Collect` refuses an amount bigger than the balance
        // and a return stops at zero. A row is the only way in.
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE customers SET balance_paisa = -2500 WHERE id = @id;", new { id = customerId }));

        var report = Run(test);

        var finding = Assert.Single(Rule(report, "No customer is owed money by themselves").Findings);

        Assert.Equal("Bilal", finding.Code);
        Assert.Equal(-2500L, finding.FoundPaisa);
    }

    [Fact]
    public void AMoneyRowThatBelongsToNobodyIsNamed()
    {
        using var test = new TestDatabase();

        test.Database.Tx(connection => test.Database.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, note)
VALUES(@at, 'cash', 7500, 'no owner');", new { at = Clock.Now() }));

        var report = Run(test);

        var finding = Assert.Single(Rule(report, "Every rupee received belongs to someone").Findings);

        // The page names it by the code the shop would give it, because a raw id means nothing on a
        // screen the owner reads.
        Assert.Equal("PAY-1", finding.Code);
        Assert.Equal(7500L, finding.FoundPaisa);
    }

    [Fact]
    public void TheCheckDoesNotRepairAnything()
    {
        using var test = new TestDatabase();
        var code = MakeBill(test, out _);

        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE sales SET total_paisa = total_paisa + 500 WHERE code = @code;", new { code }));

        Run(test);
        Run(test);

        var total = test.Database.Tx(connection =>
            Convert.ToInt64(test.Database.Scalar(connection,
                "SELECT total_paisa FROM sales WHERE code = @code;", new { code })));

        // A program that silently corrects money is worse than one that says nothing: the owner has
        // to decide. Two runs, same numbers.
        Assert.Equal(30500L, total);
    }

    [Fact]
    public void ACashierCannotRunTheCheck()
    {
        using var test = new TestDatabase();

        var cashier = TestData.Cashier(test);
        var result = Check(test).Run(cashier);

        // The till is a cashier's business; the arithmetic of the whole shop is not. The same two
        // permissions that guard the Reports page and the black box guard this one.
        Assert.False(result.Ok);
        Assert.Contains("permission", result.Error ?? string.Empty);
    }
}
