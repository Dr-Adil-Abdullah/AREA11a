using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The supplier-return cutoff window (Spec §15.2.1, §9.3.6). The promises that must hold:
///   • a batch inside the window is **on the alert board**, with the last day the supplier takes it;
///   • the window edges are inclusive and the numbers come from Settings, not from the code;
///   • a batch with no stock, or a switched-off batch, is not a reminder;
///   • the owner ends it with a written reason and it stays ended; a cashier cannot;
///   • a reminder that starts after the cutoff is refused, because that alert could never appear.
/// </summary>
public sealed class SupplierCutoffTests
{
    private static AlertService Alerts(TestDatabase test) => TestData.Alerts(test);

    private static string InDays(int days) =>
        DateTime.Today.AddDays(days).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Moves a product's only batch to a chosen expiry date and returns the batch id.</summary>
    private static int BatchExpiringIn(TestDatabase test, int productId, int days)
    {
        var date = InDays(days);

        using var connection = test.Database.Open();

        test.Database.Execute(connection, @"
UPDATE batches SET expiry_date = @date, expiry_ym = @ym WHERE product_id = @productId;",
            new { date, ym = date.Substring(0, 7), productId });

        return test.Database.QuerySingle<int>(connection,
            "SELECT id FROM batches WHERE product_id = @productId;", new { productId });
    }

    private static List<AlertItem> Cutoffs(AlertReport report) =>
        report.Live.Where(alert => alert.Type == AlertService.TypeSupplierCutoff).ToList();

    [Fact]
    public void ABatchInTheWindow_IsOnTheBoard_WithItsLastReturnDay()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 40);
        var batch = BatchExpiringIn(test, product, 120);

        var report = Alerts(test).Report();

        var alert = Assert.Single(Cutoffs(report));
        Assert.Equal(batch, alert.EntityId);
        Assert.Equal("Return to supplier", alert.TypeLabel);
        Assert.True(alert.IsAmber);
        Assert.False(alert.IsCritical);

        // 120 days away, cutoff 90: the supplier must receive it within 30 days.
        Assert.Contains(InDays(120), alert.Detail);
        Assert.Contains(InDays(30), alert.Detail);
        Assert.Contains("120 days", alert.Detail);
    }

    [Fact]
    public void TheWindowEdges_AreInclusive()
    {
        using var test = new TestDatabase();

        var inside120 = BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 10), 120);
        var inside180 = BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 10), 180);
        var inside90 = BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 10), 90);
        var outside181 = BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 10), 181);
        var outside89 = BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 10), 89);

        var reported = Cutoffs(Alerts(test).Report()).Select(alert => alert.EntityId).ToList();

        Assert.Contains(inside120, reported);
        Assert.Contains(inside180, reported);          // the reminder starts here
        Assert.Contains(inside90, reported);           // the supplier's last day is still today
        Assert.DoesNotContain(outside181, reported);
        Assert.DoesNotContain(outside89, reported);    // past the cutoff: no longer returnable
    }

    [Fact]
    public void AShortOrSwitchedOffBatch_IsNotAReminder()
    {
        using var test = new TestDatabase();

        var soldOut = BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 0), 120);
        var switchedOff = BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 20), 120);

        using var connection = test.Database.Open();
        test.Database.Execute(connection, "UPDATE batches SET active = 0 WHERE id = @id;", new { id = switchedOff });

        Assert.DoesNotContain(soldOut, Cutoffs(Alerts(test).Report()).Select(alert => alert.EntityId));
        Assert.DoesNotContain(switchedOff, Cutoffs(Alerts(test).Report()).Select(alert => alert.EntityId));
    }

    [Fact]
    public void TheOwnerEndsItWithAReason_AndItStaysEnded()
    {
        using var test = new TestDatabase();
        var batch = BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 30), 120);
        var key = AlertService.TypeSupplierCutoff + ":" + batch;

        var cashier = new User { Id = 2, Name = "Cashier", Role = "cashier" };

        Assert.False(Alerts(test).Dismiss(cashier, key, "we will sell it").Ok);
        Assert.False(Alerts(test).Dismiss(TestData.Owner, key, "no").Ok);   // reason too short

        Assert.True(Alerts(test).Dismiss(TestData.Owner, key, "we will sell it before the cutoff").Ok);

        var after = Alerts(test).Report();

        Assert.DoesNotContain(after.Live, alert => alert.Key == key);
        var ended = Assert.Single(after.Ended, alert => alert.Key == key);
        Assert.Contains("Panadol", ended.Title);
        Assert.Equal("we will sell it before the cutoff", ended.Reason);
    }

    [Fact]
    public void ChangingTheSettings_MovesTheWindow()
    {
        using var test = new TestDatabase();
        var batch = BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 30), 200);

        Assert.DoesNotContain(batch, Cutoffs(Alerts(test).Report()).Select(alert => alert.EntityId));

        var saved = test.Settings.SetMany(new Dictionary<string, object?>
        {
            ["supplier.returnAlertDays"] = 270,
            ["supplier.returnCutoffDays"] = 120
        }, TestData.Owner);

        Assert.True(saved.Ok, saved.Error);
        Assert.Contains(batch, Cutoffs(Alerts(test).Report()).Select(alert => alert.EntityId));
    }

    [Fact]
    public void AReminderThatStartsTooLate_IsRefused()
    {
        using var test = new TestDatabase();

        // The cutoff is 90 days; a reminder that starts at 60 could never appear before it.
        var refused = test.Settings.SetMany(
            new Dictionary<string, object?> { ["supplier.returnAlertDays"] = 60 }, TestData.Owner);

        Assert.False(refused.Ok);
        Assert.Contains("earlier", refused.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(180, test.Settings.GetInt("supplier.returnAlertDays", 0));

        var zero = test.Settings.SetMany(
            new Dictionary<string, object?> { ["supplier.returnCutoffDays"] = 0 }, TestData.Owner);

        Assert.False(zero.Ok);
    }

    [Fact]
    public void TheTopBar_SaysHowManyBatchesAreNearTheCutoff()
    {
        using var test = new TestDatabase();
        BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 12), 150);
        BatchExpiringIn(test, TestData.AddProduct(test, batchQty: 8), 120);

        var report = Alerts(test).Report();

        Assert.Equal(2, report.CutoffCount);
        Assert.Contains("supplier return cutoff", report.BarText);
    }
}
