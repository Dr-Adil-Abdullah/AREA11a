using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The alert board (U-31). The owner's rule is hard: nobody but the owner ends an alert, and no
/// alert ends without a written reason. These tests also pin the rule that keeps that honest - an
/// ended alert comes back by itself when the medicine moves again.
/// </summary>
public sealed class AlertTests
{
    private static AlertService Alerts(TestDatabase test) => TestData.Alerts(test);

    /// <summary>Seeds a legacy negative row; D-93 tests must not create one through a new sale.</summary>
    private static int GoNegative(TestDatabase test, string name = "Panadol 500mg") =>
        TestData.AddProduct(test, batchQty: -3, name: name);

    [Fact]
    public void AMinusStockMedicine_RaisesAnAlert()
    {
        using var test = new TestDatabase();
        GoNegative(test);

        var report = Alerts(test).Report();

        Assert.True(report.HasLive);
        Assert.Equal(1, report.NegativeCount);
        Assert.Contains("Stock is MINUS", report.SummaryText);

        // The old red bar ended with "See now"; the bar is gone (D-50) and the sentence must stand
        // alone - a headline, not a button.
        Assert.DoesNotContain("See now", report.SummaryText);
        Assert.Equal("negstock", report.Live[0].Type);
    }

    [Fact]
    public void GoodsInQuarantine_RaiseAnAlertToo()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);

        var sale = TestData.Sales(test).Create(TestData.Cart(product), TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var returned = TestData.Returns(test).Create(
            TestData.Return(sale.Value!, 1, restock: false, reason: "defective"), TestData.Owner);
        Assert.True(returned.Ok, returned.Error);

        var report = Alerts(test).Report();

        Assert.Equal(1, report.QuarantineCount);
        Assert.Contains("quarantine", report.SummaryText);
    }

    [Fact]
    public void CleanStock_RaisesNothing()
    {
        using var test = new TestDatabase();
        TestData.AddProduct(test);

        var report = Alerts(test).Report();

        Assert.False(report.HasLive);
        Assert.Equal(string.Empty, report.SummaryText);
    }

    [Fact]
    public void OnlyTheOwner_CanEndAnAlert()
    {
        using var test = new TestDatabase();
        GoNegative(test);

        var cashier = new User { Id = 5, Name = "Bilal", Role = "cashier" };
        var manager = new User { Id = 3, Name = "Kamran", Role = "manager" };
        var key = Alerts(test).Report().Live[0].Key;

        foreach (var person in new[] { cashier, manager })
        {
            var refused = Alerts(test).Dismiss(person, key, "counted the shelf, it is fine");
            Assert.False(refused.Ok);
            Assert.Contains("owner", refused.Error ?? string.Empty);
        }

        Assert.True(Alerts(test).Report().HasLive);
    }

    [Fact]
    public void AReasonIsCompulsory()
    {
        using var test = new TestDatabase();
        GoNegative(test);
        var key = Alerts(test).Report().Live[0].Key;

        Assert.False(Alerts(test).Dismiss(TestData.Owner, key, string.Empty).Ok);
        Assert.False(Alerts(test).Dismiss(TestData.Owner, key, "ok").Ok);       // too short
        Assert.True(Alerts(test).Report().HasLive);
    }

    [Fact]
    public void TheOwnerEndsIt_AndTheReasonStaysOnRecord()
    {
        using var test = new TestDatabase();
        GoNegative(test);
        var key = Alerts(test).Report().Live[0].Key;

        var result = Alerts(test).Dismiss(TestData.Owner, key, "counted the shelf, the tablet is there");
        Assert.True(result.Ok, result.Error);

        var report = Alerts(test).Report();
        Assert.False(report.HasLive);
        Assert.Single(report.Ended);
        Assert.Contains("counted the shelf", report.Ended[0].Reason);

        // It is on record even after the board is clean.
        Assert.Single(Alerts(test).Ended());
        Assert.Equal("counted the shelf, the tablet is there", Alerts(test).Ended()[0].Reason);
    }

    [Fact]
    public void ANewMovement_ReopensTheEndedAlert()
    {
        using var test = new TestDatabase();
        var product = GoNegative(test);
        var key = Alerts(test).Report().Live[0].Key;

        Assert.True(Alerts(test).Dismiss(TestData.Owner, key, "counted, fine").Ok);
        Assert.False(Alerts(test).Report().HasLive);

        // A permitted count-extra moves the same legacy-minus medicine towards zero. The old reason
        // cannot cover the new movement even though no new transaction may make the minus worse.
        System.Threading.Thread.Sleep(1100);      // the stamp has one-second resolution
        var stock = new StockService(test.Database, test.Audit, test.Settings,
            TestData.Permissions(test), test.Log);
        var again = stock.WriteOff(new WriteOffInput
        {
            ProductId = product, QtyEntered = 1, Unit = "unit", Reason = StockService.ReasonCountExtra
        }, TestData.Owner);
        Assert.True(again.Ok, again.Error);

        Assert.True(Alerts(test).Report().HasLive);
    }

    [Fact]
    public void TheOwnerCanBringAnAlertBack()
    {
        using var test = new TestDatabase();
        GoNegative(test);
        var key = Alerts(test).Report().Live[0].Key;

        Assert.True(Alerts(test).Dismiss(TestData.Owner, key, "checked").Ok);

        var restored = Alerts(test).Restore(TestData.Owner, key);

        Assert.True(restored.Ok, restored.Error);
        Assert.True(Alerts(test).Report().HasLive);
        Assert.Empty(Alerts(test).Ended());
    }

    [Fact]
    public void EndingAndRestoring_AreWrittenToTheBlackBox()
    {
        using var test = new TestDatabase();
        GoNegative(test);
        var key = Alerts(test).Report().Live[0].Key;

        Assert.True(Alerts(test).Dismiss(TestData.Owner, key, "checked twice").Ok);
        Assert.True(Alerts(test).Restore(TestData.Owner, key).Ok);

        using var connection = test.Database.Open();
        var actions = test.Database.Query<string>(connection,
            "SELECT action FROM audit_logs WHERE action LIKE 'alert_%' ORDER BY id;");

        Assert.Equal(new[] { "alert_dismiss", "alert_restore" }, actions);
    }
}
