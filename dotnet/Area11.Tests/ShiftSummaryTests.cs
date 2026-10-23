using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Who counted the drawer, and how it landed (F-56, Spec 7.2.7 - the per-user shift report the
/// original spec listed as *not built*).
///
/// The promises that must hold:
///   • a shift is counted against **the person who counted it**, and against nobody else;
///   • an open drawer is not in the report - it has no counted number yet;
///   • a shift older than the window is left out, and the window is the only thing that decides;
///   • **short and over are never netted**: one count short of Rs 500 and another over Rs 200 is
///     "500 short, 200 over", not "300 short". The first version of the service summed the people's
///     nets and a real SQLite file showed the difference in one run;
///   • a cashier cannot read his colleagues' differences, even with the till permission.
/// </summary>
public sealed class ShiftSummaryTests
{
    private static ShiftService Shifts(TestDatabase test) =>
        new(test.Database, test.Audit, TestData.Permissions(test), test.Log);

    private static User AddPerson(TestDatabase test, string name, string role)
    {
        var id = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO users(name, role, active, created_at) VALUES(@name, @role, 1, @at);",
                new { name, role, at = Clock.Now() });

            return (int)test.Database.NextId(connection);
        });

        if (role == "cashier")
        {
            Assert.True(TestData.Permissions(test).Set(TestData.Owner, id, Modules.Cash, true).Ok);
        }

        return new User { Id = id, Name = name, Role = role, Active = true };
    }

    /// <summary>
    /// Writes a closed shift straight into the table. The window tests need a date in the past, and
    /// only a direct INSERT can put one there - opening and closing a real drawer always stamps now.
    /// A closed shift has both money columns, exactly as `ShiftService.Close` leaves them.
    /// </summary>
    private static void ClosedShift(TestDatabase test, int userId, long expected, long counted, int daysAgo)
    {
        test.Database.Tx(connection => test.Database.Execute(connection, @"
INSERT INTO shifts(user_id, opened_at, closed_at, opening_float_paisa, expected_paisa, actual_paisa,
                   difference_paisa, status)
VALUES(@userId, datetime('now','localtime',@from), datetime('now','localtime',@at),
       0, @expected, @counted, @difference, 'closed');",
            new
            {
                userId,
                from = "-" + (daysAgo + 1) + " days",
                at = "-" + daysAgo + " days",
                expected,
                counted,
                difference = counted - expected
            }));
    }

    private static ShiftPersonReport Report(TestDatabase test, int days = 30)
    {
        var result = Shifts(test).ByPerson(TestData.Owner, days);

        Assert.True(result.Ok, result.Error);

        return result.Value!;
    }

    [Fact]
    public void AnExactClose_LeavesNoDifference()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);
        Assert.True(Shifts(test).Close(TestData.Owner, 50000, null).Ok);

        var report = Report(test);

        Assert.Equal(1, report.ClosedShifts);
        Assert.Equal(1, report.PeopleCount);
        Assert.Equal(0L, report.DifferencePaisa);
        Assert.Equal(0L, report.MissingPaisa);
        Assert.Equal(0L, report.TooMuchPaisa);

        var person = Assert.Single(report.People);

        Assert.Equal("Owner", person.Name);
        Assert.Equal("1 shift", person.ShiftsLabel);
        Assert.Equal("exact", person.DifferenceLabel);
        Assert.Equal("1 exact", person.PatternLabel);
        Assert.Contains("every count was exact", report.Sentence);
    }

    [Fact]
    public void AShortClose_IsWrittenAgainstThePersonWhoCountedIt()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 100000, null).Ok);

        // Rs 10,000 expected, Rs 9,500 counted: 500 paisa short.
        var closed = Shifts(test).Close(TestData.Owner, 99500, "counted twice");
        Assert.True(closed.Ok, closed.Error);

        var report = Report(test);
        var person = Assert.Single(report.People);

        Assert.Equal(-500L, person.DifferencePaisa);
        Assert.Equal("short by Rs 5", person.DifferenceLabel);
        Assert.Equal(1, person.ShortCount);
        Assert.Equal(500L, person.WorstShortPaisa);
        Assert.Equal("Rs 5", person.WorstShortLabel);
        Assert.Equal("1 short", person.PatternLabel);

        Assert.Equal(500L, report.MissingPaisa);
        Assert.Equal(0L, report.TooMuchPaisa);
        Assert.Contains("Rs 5 short", report.Sentence);
        Assert.Contains("Rs 0 over", report.Sentence);
    }

    [Fact]
    public void TwoPeople_DoNotShareAnEquation()
    {
        using var test = new TestDatabase();
        var bilal = AddPerson(test, "Bilal", "cashier");

        // The owner is short 100; Bilal is over 200.
        Assert.True(Shifts(test).Open(TestData.Owner, 10000, null).Ok);
        Assert.True(Shifts(test).Close(TestData.Owner, 9900, null).Ok);

        Assert.True(Shifts(test).Open(bilal, 50000, null).Ok);
        Assert.True(Shifts(test).Close(bilal, 50200, null).Ok);

        var report = Report(test);

        Assert.Equal(2, report.PeopleCount);
        Assert.Equal(2, report.ClosedShifts);

        var owner = report.People.Single(person => person.Name == "Owner");
        var cashier = report.People.Single(person => person.Name == "Bilal");

        Assert.Equal(-100L, owner.DifferencePaisa);
        Assert.Equal(200L, cashier.DifferencePaisa);
        Assert.Equal("over by Rs 2", cashier.DifferenceLabel);

        // The shop is 100 short and 200 over - the two figures replace nothing.
        Assert.Equal(100L, report.MissingPaisa);
        Assert.Equal(200L, report.TooMuchPaisa);
    }

    [Fact]
    public void AnOpenShift_IsNotInTheReport()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);

        var report = Report(test);

        // An open drawer has no counted number; calling it "exact" would be a lie in the shop's
        // favour, which is the most expensive kind.
        Assert.Equal(0, report.ClosedShifts);
        Assert.Equal(0, report.PeopleCount);
        Assert.False(report.AnyShifts);
        Assert.Contains("nothing to compare", report.Sentence);
    }

    [Fact]
    public void AShiftOlderThanTheWindow_IsLeftOut()
    {
        using var test = new TestDatabase();

        ClosedShift(test, TestData.Owner.Id, 10000, 10000, daysAgo: 2);
        ClosedShift(test, TestData.Owner.Id, 10000, 9000, daysAgo: 40);

        var month = Report(test, 30);

        Assert.Equal(1, month.ClosedShifts);
        Assert.Equal(0L, month.MissingPaisa);           // the short one is the old one

        var twoMonths = Report(test, 60);

        Assert.Equal(2, twoMonths.ClosedShifts);
        Assert.Equal(1000L, twoMonths.MissingPaisa);
    }

    [Fact]
    public void ShortAndOver_AreNeverNetted()
    {
        using var test = new TestDatabase();

        // The same person: one count short of Rs 5, one over Rs 2. Added together those are
        // "Rs 3 short" - and both facts would be gone. This is the fault a real SQLite file found
        // in the first version of the service.
        ClosedShift(test, TestData.Owner.Id, 10000, 9500, daysAgo: 1);
        ClosedShift(test, TestData.Owner.Id, 10000, 10200, daysAgo: 2);

        var report = Report(test);

        Assert.Equal(500L, report.MissingPaisa);
        Assert.Equal(200L, report.TooMuchPaisa);
        Assert.NotEqual(300L, report.MissingPaisa);

        var person = Assert.Single(report.People);

        // The row still shows where the person stands overall - that is a different question from
        // how much the shop lost, and both answers are on the screen.
        Assert.Equal(-300L, person.DifferencePaisa);
        Assert.Equal("1 short, 1 over", person.PatternLabel);
        Assert.Equal("Rs 5", person.WorstShortLabel);
        Assert.Equal("Rs 2", person.WorstOverLabel);
    }

    [Fact]
    public void AHandover_IsCountedForThePersonWhoCountedTheDrawer()
    {
        using var test = new TestDatabase();
        var bilal = AddPerson(test, "Bilal", "cashier");

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);

        var handover = Shifts(test).Handover(TestData.Owner, bilal, 49500, null);
        Assert.True(handover.Ok, handover.Error);

        var report = Report(test);

        // Only the owner's counted drawer is in the report; Bilal's is open and has no count yet.
        var person = Assert.Single(report.People);

        Assert.Equal("Owner", person.Name);
        Assert.Equal(-500L, person.DifferencePaisa);
        Assert.Equal(1, report.ClosedShifts);

        // And the handover really is attached to the owner, not to the person who took over.
        Assert.Equal("Bilal", handover.Value!.From.HandedToName);
    }

    [Fact]
    public void ACashier_CannotReadWhoWasShort()
    {
        using var test = new TestDatabase();
        var bilal = AddPerson(test, "Bilal", "cashier");

        // Bilal may open and close the till - the cash permission was granted above - but reading
        // whether a colleague's drawer was short is a different question.
        Assert.True(TestData.Permissions(test).Can(bilal, Modules.Cash));

        var refused = Shifts(test).ByPerson(bilal, 30);

        Assert.False(refused.Ok);
        Assert.Contains("owner or a manager", refused.Error ?? string.Empty);

        // A manager may look, without being the owner.
        var manager = AddPerson(test, "Sana", "manager");
        var allowed = Shifts(test).ByPerson(manager, 30);

        Assert.True(allowed.Ok, allowed.Error);
    }

    [Fact]
    public void AWindowOutsideTheAllowedRange_IsRefused()
    {
        using var test = new TestDatabase();

        Assert.False(Shifts(test).ByPerson(TestData.Owner, 0).Ok);
        Assert.False(Shifts(test).ByPerson(TestData.Owner, -7).Ok);
        Assert.False(Shifts(test).ByPerson(TestData.Owner, 5000).Ok);

        // The edges themselves are allowed.
        Assert.True(Shifts(test).ByPerson(TestData.Owner, 1).Ok);
        Assert.True(Shifts(test).ByPerson(TestData.Owner, 3650).Ok);
    }
}
