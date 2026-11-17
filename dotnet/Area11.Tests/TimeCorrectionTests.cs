using System;
using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Fixing bills that were stamped with the wrong clock (v0.68.0, owner's word Nov 2026).
///
/// Why these tests pass the drift in by hand: a three-day correction applied to the real
/// `TimeSource`, even for a millisecond, would move every stamp another test is reading at the
/// same moment - so the service has a door that takes the drift as a number, and these tests use
/// it. The one test that goes through the live clock uses ninety seconds, like the time tests do.
/// </summary>
[Collection("Time")]
public sealed class TimeCorrectionTests : IDisposable
{
    public TimeCorrectionTests() => TimeSource.Reset();

    public void Dispose() => TimeSource.Reset();

    private static readonly User Cashier = new() { Id = 2, Name = "Cashier", Role = "cashier" };

    private static TimeCorrectionService Fix(TestDatabase test) =>
        new(test.Database, test.Audit, test.Log);

    /// <summary>Writes a bill stamped at `date`, under `offsetSeconds` of drift (null = before v0.68.0).</summary>
    private static int SeedBill(
        TestDatabase test, string code, string date, long? offsetSeconds, long totalPaisa = 30000)
    {
        return test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO sales(code, date, subtotal_paisa, total_paisa, paid_paisa, status, payment_method,
                  created_at, time_offset_seconds)
VALUES(@code, @date, @total, @total, @total, 'paid', 'cash', @date, @offset);",
                new { code, date, total = totalPaisa, offset = offsetSeconds });

            return (int)test.Database.NextId(connection);
        });
    }

    private static void SeedPayment(TestDatabase test, int saleId, string date, long amountPaisa = 30000)
    {
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, sale_id, user_id)
VALUES(@date, 'cash', @amount, @saleId, 1);",
                new { date, amount = amountPaisa, saleId });
        });
    }

    private static string SaleDate(TestDatabase test, int saleId)
    {
        using var connection = test.Database.Open();
        return test.Database.QuerySingle<string>(connection,
            "SELECT date FROM sales WHERE id = @id;", new { id = saleId }) ?? string.Empty;
    }

    [Fact]
    public void OnlyTheOwnerMayListWronglyStampedBills()
    {
        using var test = new TestDatabase();

        var listed = Fix(test).ListBills(Cashier, "2026-11-01", "2026-11-12", TimeSpan.FromDays(2));

        Assert.False(listed.Ok);
        Assert.Contains("Only the owner", listed.Error);
    }

    [Fact]
    public void OnlyTheOwnerMayApplyTheFix()
    {
        using var test = new TestDatabase();
        var bill = SeedBill(test, "INV-0001", "2026-11-09 14:05:00", 0);

        var fixed_ = Fix(test).ApplyCorrection(Cashier, new[] { bill }, TimeSpan.FromDays(2));

        Assert.False(fixed_.Ok);
        Assert.Contains("Only the owner", fixed_.Error);
        Assert.Equal("2026-11-09 14:05:00", SaleDate(test, bill));
    }

    [Fact]
    public void BillsInRangeAreListedWithTheTimeTheyWouldGet()
    {
        using var test = new TestDatabase();
        SeedBill(test, "INV-0001", "2026-11-09 14:05:00", 0);
        SeedBill(test, "INV-0002", "2026-11-10 09:30:00", 0);

        var listed = Fix(test).ListBills(TestData.Owner, "2026-11-01", "2026-11-12", TimeSpan.FromDays(2));

        Assert.True(listed.Ok, listed.Error);
        Assert.Equal(2, listed.Value!.Count);
        Assert.Equal("INV-0001", listed.Value[0].Code);
        Assert.Equal("2026-11-09 14:05:00", listed.Value[0].WrongDate);
        Assert.Equal("2026-11-11 14:05:00", listed.Value[0].ProposedDate);
        Assert.Equal("+2.0 days", listed.Value[0].ShiftLabel);
        Assert.True(listed.Value[0].KnowsOffset);
        Assert.Equal("2026-11-12 09:30:00", listed.Value[1].ProposedDate);
    }

    [Fact]
    public void BillsAlreadyOnTrueTimeAreNotListed()
    {
        using var test = new TestDatabase();
        var drift = TimeSpan.FromDays(2);
        SeedBill(test, "INV-0001", "2026-11-09 14:05:00", (long)drift.TotalSeconds);

        var listed = Fix(test).ListBills(TestData.Owner, "2026-11-01", "2026-11-12", drift);

        Assert.True(listed.Ok, listed.Error);
        Assert.Empty(listed.Value!);
    }

    [Fact]
    public void BillsFromBeforeTheOffsetWasRecordedAssumeTheComputerClock()
    {
        using var test = new TestDatabase();
        SeedBill(test, "INV-0001", "2026-11-09 14:05:00", null);

        var listed = Fix(test).ListBills(TestData.Owner, "2026-11-01", "2026-11-12", TimeSpan.FromMinutes(90));

        Assert.True(listed.Ok, listed.Error);
        Assert.Single(listed.Value!);
        Assert.False(listed.Value[0].KnowsOffset);
        Assert.Equal("2026-11-09 15:35:00", listed.Value[0].ProposedDate);
    }

    [Fact]
    public void SecondsOutIsAlreadyRightAndNeverRewritten()
    {
        using var test = new TestDatabase();
        var bill = SeedBill(test, "INV-0001", "2026-11-09 14:05:00", 0);

        var listed = Fix(test).ListBills(TestData.Owner, "2026-11-01", "2026-11-12", TimeSpan.FromSeconds(30));
        Assert.True(listed.Ok, listed.Error);
        Assert.Empty(listed.Value!);

        var fixed_ = Fix(test).ApplyCorrection(TestData.Owner, new[] { bill }, TimeSpan.FromSeconds(30));
        Assert.True(fixed_.Ok, fixed_.Error);
        Assert.Equal(0, fixed_.Value!.Fixed);
        Assert.Equal(1, fixed_.Value.Skipped);
        Assert.Equal("2026-11-09 14:05:00", SaleDate(test, bill));
    }

    [Fact]
    public void TheFixMovesTheBillAndItsMoneyTogether()
    {
        using var test = new TestDatabase();
        var bill = SeedBill(test, "INV-0001", "2026-11-09 14:05:00", 0);
        SeedPayment(test, bill, "2026-11-09 14:05:00");

        var fixed_ = Fix(test).ApplyCorrection(TestData.Owner, new[] { bill }, TimeSpan.FromDays(2));

        Assert.True(fixed_.Ok, fixed_.Error);
        Assert.Equal(1, fixed_.Value!.Fixed);
        Assert.Equal("2026-11-11 14:05:00", SaleDate(test, bill));

        using var connection = test.Database.Open();
        var paidAt = test.Database.QuerySingle<string>(connection,
            "SELECT date FROM payments WHERE sale_id = @id;", new { id = bill });
        Assert.Equal("2026-11-11 14:05:00", paidAt);

        var offset = test.Database.QuerySingle<long?>(connection,
            "SELECT time_offset_seconds FROM sales WHERE id = @id;", new { id = bill });
        Assert.Equal((long)TimeSpan.FromDays(2).TotalSeconds, offset);
    }

    [Fact]
    public void TheWrongStampStaysInCreatedAt()
    {
        using var test = new TestDatabase();
        var bill = SeedBill(test, "INV-0001", "2026-11-09 14:05:00", 0);

        var fixed_ = Fix(test).ApplyCorrection(TestData.Owner, new[] { bill }, TimeSpan.FromDays(2));
        Assert.True(fixed_.Ok, fixed_.Error);

        using var connection = test.Database.Open();
        var born = test.Database.QuerySingle<string>(connection,
            "SELECT created_at FROM sales WHERE id = @id;", new { id = bill });
        Assert.Equal("2026-11-09 14:05:00", born);
    }

    [Fact]
    public void BothTimesAreWrittenIntoTheBlackBox()
    {
        using var test = new TestDatabase();
        var bill = SeedBill(test, "INV-0001", "2026-11-09 14:05:00", 0);

        var fixed_ = Fix(test).ApplyCorrection(TestData.Owner, new[] { bill }, TimeSpan.FromDays(2));
        Assert.True(fixed_.Ok, fixed_.Error);

        using var connection = test.Database.Open();
        var oldValue = test.Database.QuerySingle<string>(connection,
            "SELECT old_value FROM audit_logs WHERE action = 'sale_time_fixed';");
        var newValue = test.Database.QuerySingle<string>(connection,
            "SELECT new_value FROM audit_logs WHERE action = 'sale_time_fixed';");
        var details = test.Database.QuerySingle<string>(connection,
            "SELECT details FROM audit_logs WHERE action = 'sale_time_fixed';");
        var entityId = test.Database.QuerySingle<string>(connection,
            "SELECT entity_id FROM audit_logs WHERE action = 'sale_time_fixed';");

        Assert.Equal("2026-11-09 14:05:00", oldValue);
        Assert.Equal("2026-11-11 14:05:00", newValue);
        Assert.Contains("+2.0 days", details);
        Assert.Equal(bill.ToString(CultureInfo.InvariantCulture), entityId);
    }

    [Fact]
    public void OneUnreadableBillRefusesTheWholeBatch()
    {
        using var test = new TestDatabase();
        var good = SeedBill(test, "INV-0001", "2026-11-09 14:05:00", 0);
        var bad = SeedBill(test, "INV-0002", "not a date", 0);

        var fixed_ = Fix(test).ApplyCorrection(TestData.Owner, new[] { good, bad }, TimeSpan.FromDays(2));

        Assert.False(fixed_.Ok);
        Assert.Contains("INV-0002", fixed_.Error);
        Assert.Equal("2026-11-09 14:05:00", SaleDate(test, good));
    }

    [Fact]
    public void ABadRangeIsRefusedWithAReason()
    {
        using var test = new TestDatabase();

        var badFirst = Fix(test).ListBills(TestData.Owner, "not a date", "2026-11-12", TimeSpan.FromDays(2));
        Assert.False(badFirst.Ok);
        Assert.Contains("first date", badFirst.Error);

        var swapped = Fix(test).ListBills(TestData.Owner, "2026-11-12", "2026-11-01", TimeSpan.FromDays(2));
        Assert.False(swapped.Ok);
        Assert.Contains("swap them", swapped.Error);
    }

    [Fact]
    public void TheCounterRecordsTheLiveDriftOnEveryBill()
    {
        // The linchpin of the per-bill fix: the counter writes the drift in force at stamping time.
        // Serial with the time tests through the "Time" collection, so the 90 seconds stay exact.
        using var test = new TestDatabase();

        TimeSource.Probe = () => DateTimeOffset.Now.AddSeconds(90);
        Assert.True(TimeSource.TrySync(200));

        var productId = TestData.AddProduct(test);
        var created = TestData.Sales(test).Create(
            TestData.Cart(productId, 1, "cash", null), TestData.Owner);
        Assert.True(created.Ok, created.Error);

        using var connection = test.Database.Open();
        var stored = test.Database.QuerySingle<long?>(connection,
            "SELECT time_offset_seconds FROM sales WHERE id = @id;", new { id = created.Value!.SaleId });

        Assert.NotNull(stored);
        Assert.InRange(stored!.Value, 85, 95);
    }

    [Fact]
    public void ThePublicDoorReadsTheLiveDrift()
    {
        using var test = new TestDatabase();
        SeedBill(test, "INV-0001", "2026-11-09 14:05:00", 0);

        TimeSource.Probe = () => DateTimeOffset.Now.AddSeconds(90);
        Assert.True(TimeSource.TrySync(200));

        var listed = Fix(test).ListBills(TestData.Owner, "2026-11-01", "2026-11-12");

        Assert.True(listed.Ok, listed.Error);
        Assert.Single(listed.Value!);

        var proposed = DateTime.ParseExact(listed.Value[0].ProposedDate, "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture);
        Assert.InRange((proposed - new DateTime(2026, 11, 9, 14, 5, 0)).TotalSeconds, 85, 95);
    }
}
