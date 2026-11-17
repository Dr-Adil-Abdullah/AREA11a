using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Drawer differences with their reasons (Portion 14, roadmap #8). The promises that must hold:
///   • an opening or closing difference gets its own `shift_discrepancies` row (migration 030) -
///     stage, expected, counted, difference, the reason, and the moment the alert board saw it;
///   • exact drawers leave no row, and a refused close/open writes nothing either;
///   • a handover's difference stays on the old shift; the new shift starts clean, so it never
///     owes an opening row of its own;
///   • the read side is newest first, and says who counted and why.
/// </summary>
public sealed class ShiftDiscrepancyTests
{
    private static ShiftService Shifts(TestDatabase test) =>
        new(test.Database, test.Audit, TestData.Permissions(test), test.Log);

    private static User AddCashier(TestDatabase test, string name)
    {
        var id = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO users(name, role, active, created_at) VALUES(@name, 'cashier', 1, @at);",
                new { name, at = Clock.Now() });

            return (int)test.Database.NextId(connection);
        });

        var granted = TestData.Permissions(test).Set(TestData.Owner, id, Modules.Cash, true);
        Assert.True(granted.Ok, granted.Error);

        return new User { Id = id, Name = name, Role = "cashier", Active = true };
    }

    [Fact]
    public void AnExactOpening_LeavesNoRow()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, "morning").Ok);

        Assert.Empty(Shifts(test).Discrepancies());
    }

    [Fact]
    public void AnExactClosing_LeavesNoRow()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);

        var closed = Shifts(test).Close(TestData.Owner, 50000, null);

        Assert.True(closed.Ok, closed.Error);
        Assert.Empty(Shifts(test).Discrepancies());
    }

    [Fact]
    public void AnOpeningOver_WritesAnOpeningRow()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);
        Assert.True(Shifts(test).Close(TestData.Owner, 50000, null).Ok);

        var opened = Shifts(test).Open(TestData.Owner, 60000, "added change from the safe");

        Assert.True(opened.Ok, opened.Error);

        var rows = Shifts(test).Discrepancies();

        var row = Assert.Single(rows);
        Assert.Equal("opening", row.Stage);
        Assert.Equal(50000L, row.ExpectedPaisa);     // the previous counted drawer
        Assert.Equal(60000L, row.CountedPaisa);
        Assert.Equal(10000L, row.DifferencePaisa);
        Assert.Equal("added change from the safe", row.Reason);
        Assert.Equal(opened.Value!.Id, row.ShiftId);
        Assert.False(string.IsNullOrWhiteSpace(row.AlertedAt));
    }

    [Fact]
    public void AnOpeningShort_WritesAnOpeningRow()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);
        Assert.True(Shifts(test).Close(TestData.Owner, 50000, null).Ok);

        var opened = Shifts(test).Open(TestData.Owner, 45000, "took coins for the bus");

        Assert.True(opened.Ok, opened.Error);

        var row = Assert.Single(Shifts(test).Discrepancies());
        Assert.Equal("opening", row.Stage);
        Assert.Equal(50000L, row.ExpectedPaisa);
        Assert.Equal(45000L, row.CountedPaisa);
        Assert.Equal(-5000L, row.DifferencePaisa);
        Assert.True(row.IsShort);
        Assert.Equal("took coins for the bus", row.Reason);
        Assert.Equal(opened.Value!.Id, row.ShiftId);
    }

    [Fact]
    public void AClosingShort_WritesAClosingRow()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);
        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash"), TestData.Owner).Ok);

        // Rs 500 float + one Rs 300 bill = Rs 800 expected; the drawer holds Rs 750.
        var closed = Shifts(test).Close(TestData.Owner, 75000, "gave the change twice");

        Assert.True(closed.Ok, closed.Error);

        var row = Assert.Single(Shifts(test).Discrepancies());
        Assert.Equal("closing", row.Stage);
        Assert.Equal(80000L, row.ExpectedPaisa);
        Assert.Equal(75000L, row.CountedPaisa);
        Assert.Equal(-5000L, row.DifferencePaisa);
        Assert.Equal("gave the change twice", row.Reason);
        Assert.Equal(closed.Value!.Id, row.ShiftId);
    }

    [Fact]
    public void AClosingOver_WritesAClosingRow()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);

        var closed = Shifts(test).Close(TestData.Owner, 52000, "found a note under the tray");

        Assert.True(closed.Ok, closed.Error);

        var row = Assert.Single(Shifts(test).Discrepancies());
        Assert.Equal("closing", row.Stage);
        Assert.Equal(50000L, row.ExpectedPaisa);
        Assert.Equal(52000L, row.CountedPaisa);
        Assert.Equal(2000L, row.DifferencePaisa);
        Assert.False(row.IsShort);
        Assert.Equal("found a note under the tray", row.Reason);
    }

    [Fact]
    public void AnExactHandover_LeavesNoRow()
    {
        using var test = new TestDatabase();
        var bilal = AddCashier(test, "Bilal");

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, "morning").Ok);

        var handed = Shifts(test).Handover(TestData.Owner, bilal, 50000, "going home");

        Assert.True(handed.Ok, handed.Error);
        Assert.Empty(Shifts(test).Discrepancies());
    }

    [Fact]
    public void AShortHandover_WritesAClosingRowOnTheOldShiftOnly()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var bilal = AddCashier(test, "Bilal");

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);
        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash"), TestData.Owner).Ok);

        // Rs 800 expected; the owner hands over Rs 780.
        var handed = Shifts(test).Handover(TestData.Owner, bilal, 78000, "short at handover");

        Assert.True(handed.Ok, handed.Error);

        var row = Assert.Single(Shifts(test).Discrepancies());
        Assert.Equal("closing", row.Stage);
        Assert.Equal(80000L, row.ExpectedPaisa);
        Assert.Equal(78000L, row.CountedPaisa);
        Assert.Equal(-2000L, row.DifferencePaisa);
        Assert.Equal("short at handover", row.Reason);

        // The difference stays on the shift that was counted; the new shift starts with the
        // counted cash, so it owes no opening row of its own.
        Assert.Equal(handed.Value!.From.Id, row.ShiftId);
        Assert.NotEqual(handed.Value.To.Id, row.ShiftId);
    }

    [Fact]
    public void AnOverHandover_WritesAClosingRow()
    {
        using var test = new TestDatabase();
        var bilal = AddCashier(test, "Bilal");

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);

        var handed = Shifts(test).Handover(TestData.Owner, bilal, 50500, "extra coin in the drawer");

        Assert.True(handed.Ok, handed.Error);

        var row = Assert.Single(Shifts(test).Discrepancies());
        Assert.Equal("closing", row.Stage);
        Assert.Equal(500L, row.DifferencePaisa);
        Assert.Equal(handed.Value!.From.Id, row.ShiftId);
    }

    [Fact]
    public void ARefusedClosing_WritesNothing()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);

        // Short with no reason is refused - and the refusal must not leave half a row behind.
        var refused = Shifts(test).Close(TestData.Owner, 49000, null);

        Assert.False(refused.Ok);
        Assert.Empty(Shifts(test).Discrepancies());
    }

    [Fact]
    public void ARefusedOpening_WritesNothing()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);
        Assert.True(Shifts(test).Close(TestData.Owner, 50000, null).Ok);

        var refused = Shifts(test).Open(TestData.Owner, 60000, null);

        Assert.False(refused.Ok);
        Assert.Empty(Shifts(test).Discrepancies());
    }

    [Fact]
    public void Discrepancies_AreNewestFirst()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);
        Assert.True(Shifts(test).Close(TestData.Owner, 49000, "first short").Ok);
        Assert.True(Shifts(test).Open(TestData.Owner, 49000, "carried over").Ok);
        Assert.True(Shifts(test).Close(TestData.Owner, 50000, "second over").Ok);

        var rows = Shifts(test).Discrepancies();

        Assert.Equal(2, rows.Count);
        Assert.Equal("second over", rows[0].Reason);
        Assert.Equal("first short", rows[1].Reason);
        Assert.True(rows[0].Id > rows[1].Id);
    }

    [Fact]
    public void Discrepancies_SayWhoCountedAndWhen()
    {
        using var test = new TestDatabase();
        var bilal = AddCashier(test, "Bilal");

        Assert.True(Shifts(test).Open(bilal, 50000, null).Ok);
        Assert.True(Shifts(test).Close(bilal, 48000, "bilal was short").Ok);

        var row = Assert.Single(Shifts(test).Discrepancies());

        Assert.Equal("Bilal", row.OpenedBy);
        Assert.False(string.IsNullOrWhiteSpace(row.OpenedAt));
        Assert.False(string.IsNullOrWhiteSpace(row.CreatedAt));
        Assert.Equal("Closing", row.StageLabel);
    }

    [Fact]
    public void DifferenceLabels_SayShortAndOverInWords()
    {
        Assert.Equal("exact", new ShiftDiscrepancy { DifferencePaisa = 0 }.DifferenceLabel);
        Assert.Equal("short by " + Money.Format(5000), new ShiftDiscrepancy { DifferencePaisa = -5000 }.DifferenceLabel);
        Assert.Equal("over by " + Money.Format(2000), new ShiftDiscrepancy { DifferencePaisa = 2000 }.DifferenceLabel);
        Assert.Equal("Opening", new ShiftDiscrepancy { Stage = "opening" }.StageLabel);
    }
}
