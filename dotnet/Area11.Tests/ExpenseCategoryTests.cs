using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The shop's own expense categories (owner's word, Nov 2026): the eight historical words seed
/// the list under "running cost", the owner adds his own under either main type, and an expense
/// must name a word that is on the list - a mistyped drawer no longer hides under "Other".
/// </summary>
public sealed class ExpenseCategoryTests
{
    private static ExpenseCategoryService Categories(TestDatabase test) =>
        new(test.Database, test.Audit, new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    [Fact]
    public void TheEightHistoricalWordsSeedAsRunningCosts()
    {
        using var test = new TestDatabase();

        var list = Categories(test).List();

        Assert.Equal(8, list.Count);
        Assert.All(list, row => Assert.Equal(ExpenseCategoryType.Running, row.Type));
        Assert.Equal("Rent", ExpenseCategoryService.Label("rent"));
        Assert.Equal("Tea / food", ExpenseCategoryService.Label("tea"));
    }

    [Fact]
    public void OnlyTheOwnerManagesTheList()
    {
        using var test = new TestDatabase();
        var cats = Categories(test);
        var cashier = TestData.Cashier(test);

        Assert.False(cats.Add(cashier, "Diesel", ExpenseCategoryType.Running).Ok);
        Assert.False(cats.Rename(cashier, 1, "House rent").Ok);
        Assert.False(cats.SetActive(cashier, 1, false).Ok);
        Assert.Equal(8, cats.List().Count);
    }

    [Fact]
    public void AddedWordsPrintAsTypedUnderEitherMainType()
    {
        using var test = new TestDatabase();
        var cats = Categories(test);

        var diesel = cats.Add(TestData.Owner, "Diesel for generator", ExpenseCategoryType.Running);
        var cartons = cats.Add(TestData.Owner, "Empty cartons", "stock");

        Assert.True(diesel.Ok, diesel.Error);
        Assert.True(cartons.Ok, cartons.Error);
        Assert.Equal("Diesel for generator", ExpenseCategoryService.Label("Diesel for generator"));
        Assert.Equal("Stock goods", cartons.Value!.TypeLabel);
        Assert.Equal(10, cats.List().Count);
    }

    [Fact]
    public void DuplicatesCaseBlindAndBadTypesAreRefused()
    {
        using var test = new TestDatabase();
        var cats = Categories(test);

        Assert.False(cats.Add(TestData.Owner, "RENT", ExpenseCategoryType.Running).Ok);
        Assert.False(cats.Add(TestData.Owner, "   ", ExpenseCategoryType.Running).Ok);
        Assert.False(cats.Add(TestData.Owner, "Diesel", "fun").Ok);
        Assert.False(cats.Rename(TestData.Owner, 1, "salary").Ok);
        Assert.Equal(8, cats.List().Count);
    }

    [Fact]
    public void TheOtherFallbackCanBeNeitherRenamedNorPutAway()
    {
        using var test = new TestDatabase();
        var cats = Categories(test);
        var other = cats.List().Single(row => row.Name == "other");

        Assert.False(cats.Rename(TestData.Owner, other.Id, "Misc").Ok);
        Assert.False(cats.SetActive(TestData.Owner, other.Id, false).Ok);
        Assert.Equal(8, cats.List().Count);
    }

    [Fact]
    public void PutAwayHidesFromThePickerButKeepsOldExpensesReadable()
    {
        using var test = new TestDatabase();
        var cats = Categories(test);
        var added = cats.Add(TestData.Owner, "Shop paint", ExpenseCategoryType.Running).Value!;
        var spent = TestData.CashOut(test).AddExpense(TestData.Owner,
            new ExpenseInput { Category = "Shop paint", Title = "Front shutter paint", AmountPaisa = 500000 });

        Assert.True(spent.Ok, spent.Error);
        Assert.True(cats.SetActive(TestData.Owner, added.Id, false).Ok);
        Assert.DoesNotContain(cats.List(), row => row.Name == "Shop paint");
        Assert.Contains("Shop paint", cats.List(includeInactive: true).Select(row => row.Name));

        // The old expense keeps its word and still prints it.
        var refused = TestData.CashOut(test).AddExpense(TestData.Owner,
            new ExpenseInput { Category = "Shop paint", Title = "Second coat", AmountPaisa = 100000 });

        Assert.False(refused.Ok);
        Assert.Contains("Unknown expense category", refused.Error);
        Assert.Equal("Shop paint", ExpenseCategoryService.Label("Shop paint"));
    }

    [Fact]
    public void AnExpenseMustNameAWordOnTheList()
    {
        using var test = new TestDatabase();

        var missing = TestData.CashOut(test).AddExpense(TestData.Owner,
            new ExpenseInput { Category = "spaceship", Title = "Fuel", AmountPaisa = 100000 });

        Assert.False(missing.Ok);
        Assert.Contains("Manage categories", missing.Error);

        var custom = Categories(test).Add(TestData.Owner, "Newspapers", ExpenseCategoryType.Running);
        Assert.True(custom.Ok, custom.Error);

        var spent = TestData.CashOut(test).AddExpense(TestData.Owner,
            new ExpenseInput { Category = "newspapers", Title = "Daily papers", AmountPaisa = 50000 });

        Assert.True(spent.Ok, spent.Error);
    }

    [Fact]
    public void RenamingKeepsOldExpensesOnTheirSavedSpelling()
    {
        using var test = new TestDatabase();
        var cats = Categories(test);
        var added = cats.Add(TestData.Owner, "Van hire", ExpenseCategoryType.Running).Value!;
        var spent = TestData.CashOut(test).AddExpense(TestData.Owner,
            new ExpenseInput { Category = "Van hire", Title = "Airport run", AmountPaisa = 200000 });

        Assert.True(spent.Ok, spent.Error);
        Assert.True(cats.Rename(TestData.Owner, added.Id, "Suzuki hire").Ok);

        using var connection = test.Database.Open();
        var stored = test.Database.Scalar(connection,
            "SELECT category FROM expenses WHERE id = @id;", new { id = spent.Value!.Id })?.ToString();

        Assert.Equal("Van hire", stored);
    }
}
