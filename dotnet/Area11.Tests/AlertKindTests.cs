using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The two kinds of alert (D-50) and the light door.
///
/// The owner's rule from 16.4 has not moved: money or stock that is wrong now is ended only by the
/// owner, with a written reason. What is new is the other door - a *reminder* (an open drawer from an
/// earlier day, a bill that only needs a confirming word) may be put away by anybody, because it
/// blocks nobody and hides no money.
///
/// These tests pin the line between the two doors, because that line is the whole safety of the
/// feature: a cashier must never be able to silence "stock is minus" with one click.
/// </summary>
public sealed class AlertKindTests
{
    private static AlertService Alerts(TestDatabase test) => TestData.Alerts(test);

    /// <summary>Puts one medicine below its reorder level (TestData sets reorder_level = 10).</summary>
    private static int LowShelf(TestDatabase test, string name, double qty)
    {
        var product = TestData.AddProduct(test, batchQty: qty, name: name);
        Assert.True(product > 0);
        return product;
    }

    /// <summary>Writes a shift row with the hour the test needs - the drawer of "yesterday".</summary>
    private static int OpenDrawer(TestDatabase test, DateTime openedAt)
    {
        return test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO shifts(user_id, opened_at, opening_float_paisa, expected_paisa, actual_paisa,
                   difference_paisa, status)
VALUES(@userId, @at, 0, 0, 0, 0, 'open');",
                new { userId = TestData.Owner.Id, at = openedAt.ToString("yyyy-MM-dd HH:mm:ss") });

            return (int)test.Database.NextId(connection);
        });
    }

    private static void ExpireEverything(TestDatabase test, int productId)
    {
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "UPDATE batches SET expiry_date = '2020-01-31' WHERE product_id = @productId;",
                new { productId });

            return 0;
        });
    }

    [Fact]
    public void ShelvesBelowTheReorderLevel_BecomeOneFoldedLine()
    {
        using var test = new TestDatabase();
        LowShelf(test, "Panadol 500mg", 2);
        LowShelf(test, "Augmentin 625mg", 1);

        var report = Alerts(test).Report();

        // Two medicines, one line: the owner asked for a count, not a wall of names.
        Assert.Equal(1, report.LowStockCount);

        var low = report.Live.Single(alert => alert.Type == "lowstock");
        Assert.Equal(2, low.Count);
        Assert.Equal("lowstock:2", low.Key);
        Assert.Contains("2 medicines", low.Title);
        Assert.Contains("reorder level", low.Title);
        Assert.Contains("buy them", low.Detail);

        // Serious: a shelf that is nearly empty is a decision about money, not housekeeping.
        Assert.True(low.IsSerious);
        Assert.False(low.IsLight);
    }

    [Fact]
    public void AFullShelf_RaisesNoLowStockLine()
    {
        using var test = new TestDatabase();
        TestData.AddProduct(test, batchQty: 100);

        var report = Alerts(test).Report();

        Assert.Equal(0, report.LowStockCount);
        Assert.Empty(report.Live);
    }

    [Fact]
    public void AnExpiredBatch_IsSeriousAndShowsTheMoneyAtCost()
    {
        using var test = new TestDatabase();

        // 4 tablets at cost 20000 paisa (Rs 200) = Rs 800 written off if nobody acts.
        var product = TestData.AddProduct(test, costPaisa: 20000, batchQty: 4, name: "Calpol syrup");
        ExpireEverything(test, product);

        var report = Alerts(test).Report();

        Assert.Equal(1, report.ExpiredCount);

        var expired = report.Live.Single(alert => alert.Type == "expired");
        Assert.True(expired.IsSerious);
        Assert.Contains("1 batch has expired", expired.Title);
        Assert.Contains("800", expired.Title);
        Assert.Contains("write them off", expired.Detail);
    }

    [Fact]
    public void ADrawerLeftOpenFromYesterday_IsAReminder()
    {
        using var test = new TestDatabase();
        var shiftId = OpenDrawer(test, DateTime.Today.AddDays(-1).AddHours(21));

        var report = Alerts(test).Report();

        Assert.Equal(1, report.ShiftOpenCount);

        var drawer = report.Live.Single();
        Assert.Equal("shift_open:" + shiftId, drawer.Key);
        Assert.Equal("Drawer left open", drawer.TypeLabel);
        Assert.True(drawer.IsLight);
        Assert.False(drawer.IsSerious);
        Assert.Contains("Owner", drawer.Detail);
    }

    [Fact]
    public void TodaysOpenDrawer_IsNotAReminder()
    {
        using var test = new TestDatabase();

        // Opening the drawer this morning is normal - the shift page is looking after it.
        OpenDrawer(test, DateTime.Now);

        Assert.Equal(0, Alerts(test).Report().ShiftOpenCount);
    }

    [Fact]
    public void TheBell_SeparatesTheOwnerSWorkFromTheHousekeeping()
    {
        using var test = new TestDatabase();

        var product = TestData.AddProduct(test, batchQty: 2);
        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 5), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        OpenDrawer(test, DateTime.Today.AddDays(-2).AddHours(20));

        var report = Alerts(test).Report();

        Assert.Equal(1, report.SeriousCount);
        Assert.Equal(1, report.LightCount);
        Assert.Equal(2, report.LiveCount);

        Assert.Contains("1 needs the owner", report.BellText);
        Assert.Contains("1 reminder", report.BellText);
    }

    [Fact]
    public void AnEmptyBoard_SaysSoInTheBell()
    {
        using var test = new TestDatabase();

        var report = Alerts(test).Report();

        Assert.False(report.HasLive);
        Assert.Equal("Nothing needs attention.", report.BellText);
        Assert.Equal(string.Empty, report.SummaryText);
    }

    [Fact]
    public void ACashierCanPutAReminderAway()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        var shiftId = OpenDrawer(test, DateTime.Today.AddDays(-1).AddHours(21));

        var result = Alerts(test).DismissLight(cashier, "shift_open:" + shiftId);

        Assert.True(result.Ok, result.Error);

        var after = Alerts(test).Report();
        Assert.Equal(0, after.LiveCount);

        // Nothing was hidden from the record: the row says who put it away and, in plain words, that
        // nobody wrote a reason.
        var ended = after.Ended.Single();
        Assert.Equal("Cashier", ended.EndedBy);
        Assert.Equal("seen by Cashier", ended.Reason);
    }

    [Fact]
    public void ACashierCannotPutAProblemAway()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);

        var product = TestData.AddProduct(test, batchQty: 2);
        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 5), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var negative = Alerts(test).Report().Live.Single(alert => alert.Type == "negstock");

        var result = Alerts(test).DismissLight(cashier, negative.Key);

        Assert.False(result.Ok);
        Assert.Contains("owner", result.Error!);

        // And it is still on the board: a refused click must change nothing.
        Assert.Equal(1, Alerts(test).Report().LiveCount);
    }

    [Fact]
    public void PuttingAReminderAway_IsWrittenInTheBlackBox()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        var shiftId = OpenDrawer(test, DateTime.Today.AddDays(-1).AddHours(21));

        Alerts(test).DismissLight(cashier, "shift_open:" + shiftId);

        using var connection = test.Database.Open();
        var action = test.Database.QuerySingle<string>(connection, @"
SELECT action FROM audit_logs
WHERE module = 'Alerts' AND entity_id = @id
ORDER BY id DESC LIMIT 1;",
            new { id = "shift_open:" + shiftId });

        Assert.Equal("alert_seen", action);
    }

    [Fact]
    public void AnUnknownKey_IsRefused()
    {
        using var test = new TestDatabase();

        Assert.False(Alerts(test).DismissLight(TestData.Owner, "nonsense:12").Ok);
        Assert.False(Alerts(test).DismissLight(TestData.Owner, string.Empty).Ok);
        Assert.False(Alerts(test).DismissLight(null, "shift_open:1").Ok);
    }

    [Fact]
    public void TheOwner_CanStillEndAProblemWithAReason()
    {
        using var test = new TestDatabase();

        var product = TestData.AddProduct(test, batchQty: 2);
        var sale = TestData.Sales(test).Create(TestData.Cart(product, qty: 5), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var negative = Alerts(test).Report().Live.Single(alert => alert.Type == "negstock");

        Assert.True(Alerts(test).Dismiss(TestData.Owner, negative.Key, "counted the shelf").Ok);
        Assert.Equal(0, Alerts(test).Report().LiveCount);
    }

    [Theory]
    [InlineData("shift_open", true)]
    [InlineData("provisional_linked", true)]
    [InlineData("negstock", false)]
    [InlineData("lowstock", false)]
    [InlineData("expired", false)]
    [InlineData("quarantine", false)]
    [InlineData("supplier_cutoff", false)]
    public void TheKindOfAnAlert_IsDecidedInOnePlace(string type, bool isReminder)
    {
        Assert.Equal(isReminder, AlertItem.IsReminderType(type));

        var alert = new AlertItem { Type = type };
        Assert.Equal(isReminder, alert.IsLight);
        Assert.Equal(!isReminder, alert.IsSerious);
    }

    [Fact]
    public void EveryKind_HasAWordForTheScreen()
    {
        var labels = new[]
        {
            "negstock", "quarantine", "provisional", "provisional_linked",
            "supplier_cutoff", "lowstock", "expired", "shift_open"
        };

        foreach (var type in labels)
        {
            var label = new AlertItem { Type = type }.TypeLabel;

            // A raw key on the screen ("lowstock") is the thing that makes a shop app look unfinished.
            Assert.NotEqual(type, label);
            Assert.False(string.IsNullOrWhiteSpace(label));
        }
    }
}
