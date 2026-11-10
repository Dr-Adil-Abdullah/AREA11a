using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Handing the drawer over (Spec 14.4 / 18.2, F-55). The promises that must hold:
///   • one transaction closes the first shift and opens the next - never one without the other;
///   • **the counted cash becomes the next float**, and the difference stays on the shift that
///     produced it, so the next person is never asked to explain somebody else's money;
///   • both shifts point at each other (`handed_to_name`, `opened_from_shift_id`);
///   • the drawer cannot be handed to yourself, to a switched-off account, to somebody who cannot
///     work the till, or by a peer - and it cannot be handed over when no shift is open;
///   • the slip carries the six money lines, the difference in words and both signatures, at 32
///     characters on 58 mm and 48 on 80 mm.
/// </summary>
public sealed class ShiftHandoverTests
{
    private static ShiftService Shifts(TestDatabase test) =>
        new(test.Database, test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static PermissionService Permissions(TestDatabase test) =>
        new(test.Database, test.Log, test.Audit);

    private static User AddPerson(TestDatabase test, string name, string role, bool withCash, bool active = true)
    {
        var id = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO users(name, role, active, created_at) VALUES(@name, @role, @active, @at);",
                new { name, role, active = active ? 1 : 0, at = Clock.Now() });

            return (int)test.Database.NextId(connection);
        });

        if (withCash && !string.Equals(role, "owner", StringComparison.Ordinal))
        {
            var granted = Permissions(test).Set(TestData.Owner, id, Modules.Cash, true);
            Assert.True(granted.Ok, granted.Error);
        }

        return new User { Id = id, Name = name, Role = role, Active = active };
    }

    private static HandoverSlipService Slips(TestDatabase test) => new(test.Settings);

    [Fact]
    public void TheCountedMoney_BecomesTheNextFloat()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var bilal = AddPerson(test, "Bilal", "cashier", withCash: true);

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, "morning").Ok);
        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash"), TestData.Owner).Ok);

        // Rs 500 float + one Rs 300 bill = Rs 800 expected; the owner counts Rs 800.
        var handed = Shifts(test).Handover(TestData.Owner, bilal, 80000, "going home");

        Assert.True(handed.Ok, handed.Error);

        var handover = handed.Value!;

        Assert.Equal(80000L, handover.CountedPaisa);
        Assert.Equal(80000L, handover.To.OpeningFloatPaisa);
        Assert.Equal(80000L, handover.To.ExpectedPaisa);      // nothing sold on the new shift yet
        Assert.Equal(80000L, handover.From.ActualPaisa);
        Assert.Equal(0L, handover.From.DifferencePaisa);
        Assert.True(handover.IsExact);
        Assert.Equal("Bilal", handover.To.OpenedBy);

        // The open shift is now the second one, and the first one is closed.
        var current = Shifts(test).Current()!;
        Assert.Equal(handover.To.Id, current.Id);
        Assert.Equal(bilal.Id, current.UserId);
    }

    [Fact]
    public void TheDifference_StaysOnTheShiftThatWasCounted()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var bilal = AddPerson(test, "Bilal", "cashier", withCash: true);

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);
        Assert.True(TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash"), TestData.Owner).Ok);

        // Rs 50 is missing from the drawer.
        var handover = Shifts(test).Handover(TestData.Owner, bilal, 75000, "short").Value!;

        Assert.Equal(80000L, handover.From.ExpectedPaisa);
        Assert.Equal(-5000L, handover.From.DifferencePaisa);
        Assert.True(handover.IsShort);
        Assert.Equal("short by Rs 50", handover.From.DifferenceLabel);

        // The next shift starts with what is really there, not with the expected figure.
        Assert.Equal(75000L, handover.To.OpeningFloatPaisa);
        Assert.Equal(75000L, handover.To.ExpectedPaisa);
        Assert.Null(handover.To.DifferencePaisa);
        Assert.False(handover.To.IsShort);
    }

    [Fact]
    public void BothShiftsPointAtEachOther()
    {
        using var test = new TestDatabase();
        var bilal = AddPerson(test, "Bilal", "cashier", withCash: true);

        Assert.True(Shifts(test).Open(TestData.Owner, 10000, null).Ok);

        var handover = Shifts(test).Handover(TestData.Owner, bilal, 10000, null).Value!;

        using var connection = test.Database.Open();

        var rows = test.Database.Query<Row>(connection, @"
SELECT id, handed_to_user_id AS handedToId, handed_to_name AS handedTo, opened_from_shift_id AS sourceShiftId
FROM shifts ORDER BY id;");

        Assert.Equal(2, rows.Count);
        Assert.Equal(bilal.Id, rows[0].HandedToId);
        Assert.Equal("Bilal", rows[0].HandedTo);
        Assert.Null(rows[0].SourceShiftId);
        Assert.Equal(rows[0].Id, rows[1].SourceShiftId);
        Assert.Null(rows[1].HandedTo);

        Assert.Equal(handover.From.Id, handover.To.OpenedFromShiftId);
        Assert.True(handover.From.WasHandedOver);
        Assert.Contains("handed to Bilal", handover.From.HandoverLabel);
        Assert.Contains("came from drawer", handover.To.HandoverLabel);
    }

    [Fact]
    public void HandingTheDrawerToYourself_IsRefused()
    {
        using var test = new TestDatabase();

        Assert.True(Shifts(test).Open(TestData.Owner, 10000, null).Ok);

        var same = new User { Id = TestData.Owner.Id, Name = TestData.Owner.Name, Role = "owner" };
        var refused = Shifts(test).Handover(TestData.Owner, same, 10000, null);

        Assert.False(refused.Ok);
        Assert.Contains("same person", refused.Error!, StringComparison.OrdinalIgnoreCase);

        // The drawer stayed exactly as it was.
        Assert.True(Shifts(test).Current()!.IsOpen);
        Assert.Equal(TestData.Owner.Id, Shifts(test).Current()!.UserId);
    }

    [Fact]
    public void ASwitchedOffAccount_CannotTakeTheDrawer()
    {
        using var test = new TestDatabase();
        var gone = AddPerson(test, "Gone", "cashier", withCash: true, active: false);

        Assert.True(Shifts(test).Open(TestData.Owner, 10000, null).Ok);

        var refused = Shifts(test).Handover(TestData.Owner, gone, 10000, null);

        Assert.False(refused.Ok);
        Assert.Contains("switched off", refused.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.True(Shifts(test).Current()!.IsOpen);
    }

    [Fact]
    public void SomebodyWhoCannotWorkTheTill_CannotTakeTheDrawer()
    {
        using var test = new TestDatabase();
        var helper = AddPerson(test, "Helper", "cashier", withCash: false);

        // The role default decides the answer; the test asks the same question the service asks.
        if (Permissions(test).Can(helper, Modules.Cash))
        {
            Assert.True(Permissions(test).Set(TestData.Owner, helper.Id, Modules.Cash, false).Ok);
        }

        Assert.True(Shifts(test).Open(TestData.Owner, 10000, null).Ok);

        var refused = Shifts(test).Handover(TestData.Owner, helper, 10000, null);

        Assert.False(refused.Ok);
        Assert.Contains("cannot work the till", refused.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.True(Shifts(test).Current()!.IsOpen);
    }

    [Fact]
    public void APeer_CannotHandOverSomebodyElsesDrawer()
    {
        using var test = new TestDatabase();
        var first = AddPerson(test, "First", "cashier", withCash: true);
        var second = AddPerson(test, "Second", "cashier", withCash: true);
        var third = AddPerson(test, "Third", "cashier", withCash: true);

        Assert.True(Shifts(test).Open(first, 20000, null).Ok);

        // Second did not open this drawer and is not senior: refused.
        var refused = Shifts(test).Handover(second, third, 20000, null);

        Assert.False(refused.Ok);
        Assert.Contains("Only the person who opened", refused.Error!);
        Assert.Equal(first.Id, Shifts(test).Current()!.UserId);

        // The owner may hand over on behalf of the person who went home.
        var byOwner = Shifts(test).Handover(TestData.Owner, third, 20000, "no one at the counter");

        Assert.True(byOwner.Ok, byOwner.Error);
        Assert.Equal(third.Id, Shifts(test).Current()!.UserId);
    }

    [Fact]
    public void AHandoverWithNoOpenShift_IsRefused()
    {
        using var test = new TestDatabase();
        var bilal = AddPerson(test, "Bilal", "cashier", withCash: true);

        var refused = Shifts(test).Handover(TestData.Owner, bilal, 0, null);

        Assert.False(refused.Ok);
        Assert.Contains("no open shift", refused.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheHandover_IsInTheBlackBox_AndInTheHistory()
    {
        using var test = new TestDatabase();
        var bilal = AddPerson(test, "Bilal", "cashier", withCash: true);

        Assert.True(Shifts(test).Open(TestData.Owner, 10000, null).Ok);

        var handover = Shifts(test).Handover(TestData.Owner, bilal, 9500, "Rs 50 missing").Value!;

        using var connection = test.Database.Open();

        var audit = test.Database.Query<AuditRow>(connection, @"
SELECT action, entity, entity_id AS entityId, details AS note
FROM audit_logs WHERE action IN ('shift_handover', 'shift_open') ORDER BY id;");

        var handoverLine = audit.Single(row => row.Action == "shift_handover");
        Assert.Equal("shifts", handoverLine.Entity);
        Assert.Equal(handover.From.Id.ToString(), handoverLine.EntityId);
        Assert.Equal("Rs 50 missing", handoverLine.Note);

        // Two openings by now - the morning one and the one this handover created - so the test
        // asks for the last rather than the only one. `Single` here was the first failure this
        // file ever produced on Windows (v0.30.7).
        var openLines = audit.Where(row => row.Action == "shift_open").ToList();
        Assert.Equal(2, openLines.Count);
        Assert.Equal(handover.To.Id.ToString(), openLines[^1].EntityId);

        var history = Shifts(test).History();
        Assert.Equal(2, history.Count);
        Assert.Equal(handover.To.Id, history[0].Id);              // newest first
        Assert.True(history[1].WasHandedOver);
    }

    [Fact]
    public void TheDaysTakings_AreNotCountedTwice()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var bilal = AddPerson(test, "Bilal", "cashier", withCash: true);

        Assert.True(Shifts(test).Open(TestData.Owner, 0, null).Ok);

        // Two boxes before the change of hands, one after it.
        var before = TestData.Sales(test).Create(TestData.Cart(product, qty: 2, method: "cash"), TestData.Owner);
        Assert.True(before.Ok, before.Error);

        var handover = Shifts(test).Handover(TestData.Owner, bilal, 60000, null).Value!;

        Assert.Equal(60000L, handover.From.CashSalesPaisa);
        Assert.Equal(60000L, handover.From.ExpectedPaisa);
        Assert.Equal(60000L, handover.To.OpeningFloatPaisa);

        var after = TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash"), bilal);
        Assert.True(after.Ok, after.Error);

        // A test does everything in a few milliseconds, so both bills and the handover carry the
        // same second - and a rule about the second cannot be proved by the clock. The four stamps
        // are pinned instead: first bill at -15 s, handover at -10 s, second bill at -5 s.
        PinAroundTheHandover(test, handover, before.Value!.Code, after.Value!.Code);

        var second = Shifts(test).Current()!;

        Assert.Equal(30000L, second.CashSalesPaisa);        // only the bill after the handover
        Assert.Equal(90000L, second.ExpectedPaisa);         // the counted float + that bill

        var history = Shifts(test).History();
        var first = history.Single(row => row.Id == handover.From.Id);

        // The two shifts together say exactly what the shop sold: 6 + 3 = 9, never 15. The first
        // shift's own number was fixed when the drawer was counted, so it cannot be rewritten by
        // the bill that came afterwards.
        Assert.Equal(60000L, first.ExpectedPaisa);
        Assert.Equal(90000L, handover.From.CashSalesPaisa + second.CashSalesPaisa);
    }

    [Fact]
    public void ABillInTheHandoverSecond_BelongsToTheShiftThatWasCounted()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, retailPaisa: 30000, batchQty: 100);
        var bilal = AddPerson(test, "Bilal", "cashier", withCash: true);

        Assert.True(Shifts(test).Open(TestData.Owner, 0, null).Ok);

        var bill = TestData.Sales(test).Create(TestData.Cart(product, qty: 1, method: "cash"), TestData.Owner);
        Assert.True(bill.Ok, bill.Error);

        var handover = Shifts(test).Handover(TestData.Owner, bilal, 30000, null).Value!;
        var opened = Shifts(test).Current()!;

        // No pinning needed here: whichever seconds the two land in, the bill is never after the
        // count, because it was written before the handover ran.
        Assert.Equal(30000L, handover.From.CashSalesPaisa);
        Assert.Equal(30000L, handover.From.ExpectedPaisa);
        Assert.Equal(0L, handover.From.DifferencePaisa);

        // And the new shift must not count it a second time: its window starts strictly after the
        // second the drawer changed hands. Counting it twice would invent Rs 300.
        Assert.Equal(0L, opened.CashSalesPaisa);
        Assert.Equal(30000L, opened.OpeningFloatPaisa);
        Assert.Equal(30000L, opened.ExpectedPaisa);
        Assert.Equal(30000L, handover.From.CashSalesPaisa + opened.CashSalesPaisa);
    }

    /// <summary>
    /// Pins the two shifts and the two bills to fixed seconds in the past: the first bill at -15 s,
    /// the handover at -10 s and the second bill at -5 s. Without this the wall clock decides which
    /// shift sees which bill and the test proves nothing on a slow machine or the wrong thing on a
    /// fast one. Only the four stamps change; no money moves.
    /// </summary>
    private static void PinAroundTheHandover(TestDatabase test, ShiftHandover handover, string beforeCode, string afterCode)
    {
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
UPDATE shifts SET opened_at = datetime('now','localtime','-20 seconds'),
                  closed_at = datetime('now','localtime','-10 seconds')
WHERE id = @id;", new { id = handover.From.Id });

            test.Database.Execute(connection,
                "UPDATE shifts SET opened_at = datetime('now','localtime','-10 seconds') WHERE id = @id;",
                new { id = handover.To.Id });

            test.Database.Execute(connection,
                "UPDATE sales SET date = datetime('now','localtime','-15 seconds') WHERE code = @code;",
                new { code = beforeCode });

            test.Database.Execute(connection, @"
UPDATE payments SET date = datetime('now','localtime','-15 seconds')
WHERE sale_id = (SELECT id FROM sales WHERE code = @code);", new { code = beforeCode });

            test.Database.Execute(connection,
                "UPDATE sales SET date = datetime('now','localtime','-5 seconds') WHERE code = @code;",
                new { code = afterCode });

            test.Database.Execute(connection, @"
UPDATE payments SET date = datetime('now','localtime','-5 seconds')
WHERE sale_id = (SELECT id FROM sales WHERE code = @code);", new { code = afterCode });
        });
    }

    [Fact]
    public void TheSlip_CarriesBothSignaturesAndFitsThirtyTwoColumns()
    {
        using var test = new TestDatabase();
        var bilal = AddPerson(test, "Bilal", "cashier", withCash: true);

        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);

        var handover = Shifts(test).Handover(TestData.Owner, bilal, 49950, "fifty paisa short").Value!;
        var slip = Slips(test).Build(handover);

        Assert.Equal(32, slip.Width);
        Assert.All(slip.Lines, line => Assert.True(line.Text.Length <= 32, $"too long: '{line.Text}'"));

        var text = slip.PlainText;

        Assert.Contains("HANDOVER", text);
        Assert.Contains("From: Owner", text);
        Assert.Contains("To:   Bilal", text);
        Assert.Contains("Given by:", text);
        Assert.Contains("Taken by:", text);
        Assert.Contains("HANDED OVER", text);

        // The six lines are all there, and the money is printed the receipt way.
        Assert.Contains("Opening float", text);
        Assert.Contains("Khata received", text);
        Assert.Contains("Cash from supplier", text);
        Assert.Contains("Refunds", text);
        Assert.Contains("Paid to suppliers", text);
        Assert.Contains("Shop expenses", text);
        Assert.Contains("Owner took (cash)", text);
        Assert.Contains("500.00", text);
    }

    [Fact]
    public void TheSlip_SaysShortInWords_AndGrowsToFortyEightColumns()
    {
        using var test = new TestDatabase();
        var bilal = AddPerson(test, "Bilal", "cashier", withCash: true);

        Assert.True(test.Settings.Set("printer.width", 80, TestData.Owner).Ok);
        Assert.True(Shifts(test).Open(TestData.Owner, 50000, null).Ok);

        var handover = Shifts(test).Handover(TestData.Owner, bilal, 49950, "fifty paisa short").Value!;
        var slip = Slips(test).Build(handover);

        Assert.Equal(48, slip.Width);
        Assert.All(slip.Lines, line => Assert.True(line.Text.Length <= 48, $"too long: '{line.Text}'"));

        var difference = slip.Lines.Single(line => line.Text.StartsWith("Difference", StringComparison.Ordinal));

        Assert.Contains("0.50 short", difference.Text);
        Assert.True(difference.Bold);
    }

    /// <summary>Two flat rows for the assertions that look at the table itself.</summary>
    private sealed class Row
    {
        public int Id { get; set; }
        public int? HandedToId { get; set; }
        public string? HandedTo { get; set; }
        public int? SourceShiftId { get; set; }
    }

    private sealed class AuditRow
    {
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string? Note { get; set; }
    }
}
