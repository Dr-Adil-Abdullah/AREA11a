using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The stock-minus registry and its monthly review (F-39, Spec §33.6.6). The promises:
///
///   • every medicine whose shelf is below zero is on the page, with the value at cost;
///   • the page says **since when** - the movement that took the shelf below zero - and a shelf that
///     came back and went minus again starts a new episode;
///   • a review is one permanent line in the black box, it needs a real note, and it **never** moves
///     the stock;
///   • a review from before the current episode does not mark it reviewed;
///   • the cashier can neither read the page nor review it.
/// </summary>
public sealed class StockMinusTests
{
    private const string OwnerName = "Owner";

    // ============================== the list ==============================

    [Fact]
    public void AMinusShelfAppearsWithItsValueAtCost()
    {
        using var test = new TestDatabase();

        // 5 tablets at Rs 200 cost = Rs 1,000 worth of stock that is not on the shelf.
        TestData.AddProduct(test, batchQty: -5, name: "Panadol 500mg");

        var report = Registry(test).Report(TestData.Owner);

        Assert.True(report.Ok, report.Error);

        var row = Assert.Single(report.Value!.Rows);
        Assert.Equal("Panadol 500mg", row.Name);
        Assert.Equal(-5d, row.StockQty);
        Assert.Equal(-100000L, row.ValuePaisa);
        Assert.Equal("-5 tablet", row.StockText);
        Assert.Equal("Rs -1,000", row.ValueText);
        Assert.Contains("below zero", report.Value.SummaryText);
    }

    [Fact]
    public void AShellAtZeroIsNotMinus()
    {
        using var test = new TestDatabase();

        TestData.AddProduct(test, batchQty: 0, name: "Panadol 500mg");

        var report = Registry(test).Report(TestData.Owner);

        Assert.True(report.Ok, report.Error);
        Assert.Empty(report.Value!.Rows);
        Assert.Contains("No medicine is below zero", report.Value.SummaryText);
    }

    [Fact]
    public void NothingMinusIsAQuietPage()
    {
        using var test = new TestDatabase();

        TestData.AddProduct(test, batchQty: 40, name: "Panadol 500mg");

        var report = Registry(test).Report(TestData.Owner);

        Assert.True(report.Ok, report.Error);
        Assert.Equal(0, report.Value!.Count);
        Assert.Equal("Rs 0", report.Value.TotalText);
        Assert.Equal("-", report.Value.OldestText);
    }

    // ============================== the episode ==============================

    [Fact]
    public void TheEpisodeStartsAtTheMovementThatWentBelowZero()
    {
        using var test = new TestDatabase();

        var product = TestData.AddProduct(test, batchQty: -7, name: "Panadol 500mg");

        var arrived = DaysAgo(20);
        var sold = DaysAgo(17);

        Move(test, product, 5, arrived, "import", "STOCK-1");
        Move(test, product, -12, sold, "sale", "INV-0001");

        var report = Registry(test).Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        var row = Assert.Single(report.Value!.Rows);

        Assert.StartsWith(sold[..10], row.SinceAt);
        Assert.Equal(17, row.DaysNegative);
        Assert.Equal("sold INV-0001", StockMinusCause.Describe(row.SinceType, row.SinceRefCode));
        Assert.Contains("sold INV-0001", row.CauseLabel);
        Assert.Equal("17 day(s)", row.DaysLabel);

        // "old" is the colour of a month, not a fortnight: the row is still "fresh" at 17 days.
        Assert.Equal("fresh", row.Tone);
    }

    [Fact]
    public void AMonthOfMinusIsMarkedOld()
    {
        using var test = new TestDatabase();

        var product = TestData.AddProduct(test, batchQty: -4, name: "Panadol 500mg");
        Move(test, product, -4, DaysAgo(31), "sale", "INV-0002");

        var report = Registry(test).Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        var row = Assert.Single(report.Value!.Rows);

        Assert.Equal(31, row.DaysNegative);
        Assert.Equal("old", row.Tone);
    }

    [Fact]
    public void ComingBackToZeroEndsTheOldEpisode()
    {
        using var test = new TestDatabase();

        var product = TestData.AddProduct(test, batchQty: -3, name: "Brufen 400mg");

        // First episode: -5, then a return brings it back to +5 - that episode is over.
        Move(test, product, -5, DaysAgo(50), "sale", "INV-0001");
        Move(test, product, 10, DaysAgo(40), "return_in", "RET-0001");

        // Second episode, and the one the page must talk about.
        Move(test, product, -8, DaysAgo(3), "sale", "INV-0009");

        var report = Registry(test).Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        var row = Assert.Single(report.Value!.Rows);

        Assert.Equal(3, row.DaysNegative);
        Assert.StartsWith(DaysAgo(3)[..10], row.SinceAt);
        Assert.Contains("INV-0009", row.CauseLabel);
        Assert.Equal("fresh", row.Tone);
    }

    [Fact]
    public void TheOldestMinusIsListedFirst()
    {
        using var test = new TestDatabase();

        var older = TestData.AddProduct(test, batchQty: -1, name: "Zinc 20mg");
        var newer = TestData.AddProduct(test, batchQty: -1, name: "Amlodipine 5mg");

        Move(test, older, -1, DaysAgo(60), "sale", "INV-0001");
        Move(test, newer, -1, DaysAgo(2), "sale", "INV-0002");

        var report = Registry(test).Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        Assert.Equal("Zinc 20mg", report.Value!.Rows[0].Name);
        Assert.Equal("Amlodipine 5mg", report.Value.Rows[1].Name);
        Assert.Equal(60, report.Value.OldestDays);
        Assert.Equal("60 day(s)", report.Value.OldestText);
    }

    // ============================== the review ==============================

    [Fact]
    public void AReviewIsRecordedInTheBlackBoxAndChangesNoStock()
    {
        using var test = new TestDatabase();

        var product = TestData.AddProduct(test, batchQty: -3, name: "Panadol 500mg");
        Move(test, product, -3, DaysAgo(10), "sale", "INV-0001");

        var saved = Registry(test).Review(TestData.Owner, product, "One strip was broken and thrown away.");
        Assert.True(saved.Ok, saved.Error);

        var report = Registry(test).Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        var row = Assert.Single(report.Value!.Rows);
        Assert.True(row.IsReviewed);
        Assert.Equal(OwnerName, row.ReviewBy);
        Assert.Contains("broken", row.ReviewNoteLabel);
        Assert.Equal(1, report.Value.ReviewedCount);
        Assert.Equal(0, report.Value.NotReviewedCount);

        // The review is a note, not a correction: the shelf is still minus.
        Assert.Equal(-3d, row.StockQty);
        Assert.Equal(1L, Scalar(test, "SELECT COUNT(1) FROM audit_logs WHERE module = 'stock' AND action = 'stock_minus_review';"));
        Assert.Equal(0L, Scalar(test, "SELECT COUNT(1) FROM stock_movements WHERE type = 'adjust';"));
    }

    [Fact]
    public void AReviewNeedsARealNote()
    {
        using var test = new TestDatabase();

        var product = TestData.AddProduct(test, batchQty: -3, name: "Panadol 500mg");

        Assert.False(Registry(test).Review(TestData.Owner, product, "ok").Ok);
        Assert.False(Registry(test).Review(TestData.Owner, product, "   ").Ok);
        Assert.Equal(0L, Scalar(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'stock_minus_review';"));

        Assert.True(Registry(test).Review(TestData.Owner, product, "Counted twice.").Ok);
    }

    [Fact]
    public void AReviewFromBeforeTheEpisodeDoesNotCount()
    {
        using var test = new TestDatabase();

        var product = TestData.AddProduct(test, batchQty: -3, name: "Panadol 500mg");

        Move(test, product, -3, DaysAgo(10), "sale", "INV-0001");

        // Last month's note: it explained the shelf then, not what happened ten days ago.
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO audit_logs(at, user_name, action, module, entity, entity_id, details)
VALUES(@at, 'Owner', 'stock_minus_review', 'stock', 'products', @id, 'Seen last month.');",
                new { at = DaysAgo(30), id = product.ToString(CultureInfo.InvariantCulture) });
        });

        var before = Registry(test).Report(TestData.Owner);
        Assert.True(before.Ok, before.Error);
        Assert.False(Assert.Single(before.Value!.Rows).IsReviewed);

        Assert.True(Registry(test).Review(TestData.Owner, product, "Counted again today.").Ok);

        var after = Registry(test).Report(TestData.Owner);
        Assert.True(after.Ok, after.Error);
        Assert.True(Assert.Single(after.Value!.Rows).IsReviewed);
    }

    [Fact]
    public void AShelfThatIsNotMinusCannotBeReviewed()
    {
        using var test = new TestDatabase();

        var product = TestData.AddProduct(test, batchQty: 4, name: "Panadol 500mg");

        var saved = Registry(test).Review(TestData.Owner, product, "Nothing wrong here.");

        Assert.False(saved.Ok);
        Assert.Equal(0L, Scalar(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'stock_minus_review';"));
    }

    [Fact]
    public void ACashierCannotReadOrReviewTheRegistry()
    {
        using var test = new TestDatabase();

        var product = TestData.AddProduct(test, batchQty: -3, name: "Panadol 500mg");
        var cashier = TestData.Cashier(test);

        Assert.False(Registry(test).Report(cashier).Ok);
        Assert.False(Registry(test).Review(cashier, product, "I saw it.").Ok);
        Assert.Equal(0L, Scalar(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'stock_minus_review';"));
    }

    // ============================== helpers ==============================

    private static StockMinusService Registry(TestDatabase test) =>
        new(test.Database, test.Audit, TestData.Permissions(test), test.Log);

    /// <summary>A whole-day stamp in the past, so "days since" never depends on the day of the run.</summary>
    private static string DaysAgo(int days) =>
        DateTime.Today.AddDays(-days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 10:00:00";

    private static void Move(
        TestDatabase test,
        int productId,
        double qty,
        string at,
        string type,
        string? refCode)
    {
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO stock_movements(product_id, type, qty_base, ref_code, created_at)
VALUES(@productId, @type, @qty, @refCode, @at);",
                new { productId, type, qty, refCode, at });
        });
    }

    private static long Scalar(TestDatabase test, string sql)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection, sql) ?? 0);
    }
}
