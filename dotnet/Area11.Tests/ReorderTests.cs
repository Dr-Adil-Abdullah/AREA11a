using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The reorder pad and the WhatsApp order (F-38, Spec 10.5 &amp; 20.3). The promises:
///
///   • the three bands come from **one** number per medicine - the minimum - so the screen and the
///     shop can never disagree about what "running low" means (D-55);
///   • the suggestion is enough to reach twice the minimum, and the owner can change it;
///   • the supplier is the one who sold that medicine last - never a guess;
///   • the message is plain text a supplier can act on, one supplier per message;
///   • a Pakistani mobile becomes an international `wa.me` number, and no number still opens WhatsApp;
///   • changing a minimum is audited, and a cashier can neither read the pad nor change it.
/// </summary>
public sealed class ReorderTests
{
    // ============================== the three bands ==============================

    [Fact]
    public void OutOfStockAtTheMinimumAndRunningLow_AreThreeDifferentBands()
    {
        using var test = new TestDatabase();
        var reorder = Pad(test);

        Add(test, "Amlodipine 5mg", level: 10, stock: 0);
        Add(test, "Panadol 500mg", level: 10, stock: 10);
        Add(test, "Brufen 400mg", level: 10, stock: 14);
        Add(test, "Augmentin 625mg", level: 10, stock: 16);

        var report = reorder.Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        var rows = report.Value!.Rows;
        Assert.Equal(3, rows.Count);

        Assert.Equal(1, report.Value.OutCount);
        Assert.Equal(1, report.Value.CriticalCount);
        Assert.Equal(1, report.Value.LowCount);

        // Red first: the empty shelf is the one the shop must act on today.
        Assert.Equal("Amlodipine 5mg", rows[0].Name);
        Assert.Equal(ReorderTier.Out, rows[0].Tier);
        Assert.Equal("OUT OF STOCK", rows[0].TierLabel);

        Assert.Equal(ReorderTier.Critical, rows[1].Tier);
        Assert.Equal("ORDER NOW", rows[1].TierLabel);

        Assert.Equal(ReorderTier.Low, rows[2].Tier);
        Assert.Equal("RUNNING LOW", rows[2].TierLabel);

        // 16 is comfortably above 10 - it must not clutter the page.
        Assert.DoesNotContain(rows, row => row.Name == "Augmentin 625mg");
    }

    [Fact]
    public void AMedicineWithNoMinimumIsShownOnlyWhenTheShelfIsEmpty()
    {
        using var test = new TestDatabase();
        var reorder = Pad(test);

        Add(test, "Unwatched with stock", level: 0, stock: 25);
        var empty = Add(test, "Unwatched and empty", level: 0, stock: 0);

        var report = reorder.Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        var row = Assert.Single(report.Value!.Rows);
        Assert.Equal(empty, row.ProductId);
        Assert.Equal(ReorderTier.Out, row.Tier);
        Assert.Equal("not watched", row.LevelText);

        // One unit is a starting number, not a policy: the owner types the real quantity.
        Assert.Equal(1d, row.SuggestQty);
    }

    [Fact]
    public void TheSuggestionFillsTheShelfToTwiceTheMinimum()
    {
        using var test = new TestDatabase();
        var reorder = Pad(test);

        Add(test, "Panadol 500mg", level: 20, stock: 5);
        Add(test, "Brufen 400mg", level: 10, stock: 0);

        var report = reorder.Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        Assert.Equal(35d, report.Value!.Rows.Single(row => row.Name == "Panadol 500mg").SuggestQty);
        Assert.Equal(20d, report.Value.Rows.Single(row => row.Name == "Brufen 400mg").SuggestQty);
    }

    // ============================== who to order from ==============================

    [Fact]
    public void TheSupplierIsTheOneWhoSoldTheMedicineLast()
    {
        using var test = new TestDatabase();

        var first = TestData.AddSupplier(test, "GSK Depot");
        var second = TestData.AddSupplier(test, "Highnoon Labs");
        var product = Add(test, "Panadol 500mg", level: 10, stock: 2);

        // Different mobiles, so the phone on the pad proves which supplier the row really followed.
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "UPDATE suppliers SET phone = '0300-7654321' WHERE id = @id;", new { id = second });
        });

        AddPurchase(test, "PINV-0001", first, product, "2026-09-01 10:00:00");
        AddPurchase(test, "PINV-0002", second, product, "2026-10-05 10:00:00");

        var report = Pad(test).Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        var row = Assert.Single(report.Value!.Rows);
        Assert.True(row.HasSupplier);
        Assert.Equal("Highnoon Labs", row.SupplierName);
        Assert.Equal("0300-7654321", row.SupplierPhone);
        Assert.Equal("last bought 2026-10-05", row.LastBoughtLabel);
    }

    [Fact]
    public void AMedicineNeverBoughtHasNoSupplier()
    {
        using var test = new TestDatabase();

        Add(test, "Panadol 500mg", level: 10, stock: 0);

        var report = Pad(test).Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        var row = Assert.Single(report.Value!.Rows);
        Assert.False(row.HasSupplier);
        Assert.Equal("no supplier yet", row.SupplierLabel);
        Assert.Equal("never bought", row.LastBoughtLabel);
    }

    // ============================== the message ==============================

    [Fact]
    public void TheOrderTextIsWhatTheSupplierReads()
    {
        var text = ReorderService.BuildText(
            new[]
            {
                new ReorderLine
                {
                    Name = "Panadol 500mg", Generic = "Paracetamol", Company = "GSK",
                    Quantity = 35, BaseUnit = "tablet", BoxStrips = 2, StripTablets = 10
                },
                new ReorderLine { Name = "Brufen 400mg", Quantity = 20, BaseUnit = "tablet" }
            },
            "GSK Depot", "Please send before Friday.", "Area11 Pharmacy");

        var lines = text.Split('\n');

        Assert.Equal("Salam, GSK Depot", lines[0]);
        Assert.Equal("Area11 Pharmacy order:", lines[1]);
        Assert.Equal(string.Empty, lines[2]);

        // 35 tablets = 1 box (20) + 1 strip (10) + 5 tablets - the words, not a raw number.
        Assert.Equal("1. Panadol 500mg (GSK - Paracetamol) - 1 box + 1 strip + 5 tablet", lines[3]);
        Assert.Equal("2. Brufen 400mg - 20 tablet", lines[4]);

        Assert.Contains("Total: 2 item(s).", text);
        Assert.Contains("Please send before Friday.", text);
        Assert.EndsWith("Please confirm the items and the rate. Thank you.", text.TrimEnd());
    }

    [Fact]
    public void AnEmptyLineIsNeverPutOnTheOrder()
    {
        var text = ReorderService.BuildText(
            new[]
            {
                new ReorderLine { Name = "Panadol 500mg", Quantity = 0, BaseUnit = "tablet" },
                new ReorderLine { Name = "Brufen 400mg", Quantity = 0, BaseUnit = "tablet" }
            },
            storeName: "Area11");

        Assert.Contains("Total: 0 item(s).", text);
        Assert.DoesNotContain("1. Panadol", text);
        Assert.DoesNotContain("2. Brufen", text);

        // No supplier chosen: the greeting stays open, so the text can go to anyone.
        Assert.StartsWith("Salam,", text);
    }

    [Fact]
    public void APakistaniMobileBecomesAnInternationalNumber()
    {
        var local = ReorderService.BuildLink("0300-1234567", "Area11 Pharmacy order:");
        Assert.StartsWith("https://wa.me/923001234567?text=", local);
        Assert.Contains("Area11%20Pharmacy", local);

        Assert.StartsWith("https://wa.me/923001234567?text=",
            ReorderService.BuildLink("+92 300 1234567", "x"));

        Assert.StartsWith("https://wa.me/923001234567?text=",
            ReorderService.BuildLink("0092 300 1234567", "x"));
    }

    [Fact]
    public void NoNumberStillOpensWhatsApp()
    {
        var link = ReorderService.BuildLink(null, "line one\nline two & more");

        Assert.StartsWith("https://wa.me/?text=", link);
        Assert.Contains("%0A", link);
        Assert.Contains("%26", link);
    }

    // ============================== the minimum ==============================

    [Fact]
    public void TheMinimumIsSaved_AndTheChangeIsInTheBlackBox()
    {
        using var test = new TestDatabase();
        var reorder = Pad(test);
        var product = Add(test, "Panadol 500mg", level: 20, stock: 5);

        var saved = reorder.SetLevel(TestData.Owner, product, 25);
        Assert.True(saved.Ok, saved.Error);

        Assert.Equal(25d, Level(test, product));
        Assert.Equal(1L, Scalar(test, "SELECT COUNT(1) FROM audit_logs WHERE module = 'reorder' AND action = 'level_change';"));

        var report = reorder.Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);
        Assert.Equal(25d, report.Value!.Rows.Single(row => row.ProductId == product).LevelQty);
    }

    [Fact]
    public void ANegativeMinimumIsRefusedAndNothingChanges()
    {
        using var test = new TestDatabase();
        var reorder = Pad(test);
        var product = Add(test, "Panadol 500mg", level: 20, stock: 5);

        var saved = reorder.SetLevel(TestData.Owner, product, -1);

        Assert.False(saved.Ok);
        Assert.Equal(20d, Level(test, product));
        Assert.Equal(0L, Scalar(test, "SELECT COUNT(1) FROM audit_logs WHERE module = 'reorder';"));
    }

    [Fact]
    public void SavingTheSameMinimumTwiceWritesOneBlackBoxLine()
    {
        using var test = new TestDatabase();
        var reorder = Pad(test);
        var product = Add(test, "Panadol 500mg", level: 20, stock: 5);

        Assert.True(reorder.SetLevel(TestData.Owner, product, 20).Ok);
        Assert.True(reorder.SetLevel(TestData.Owner, product, 30).Ok);

        // No change means no row (Spec 22.4): the first call changes nothing, the second changes once.
        Assert.Equal(1L, Scalar(test, "SELECT COUNT(1) FROM audit_logs WHERE module = 'reorder' AND action = 'level_change';"));
    }

    // ============================== permissions and switches ==============================

    [Fact]
    public void ACashierCannotSeeThePadOrChangeAMinimum()
    {
        using var test = new TestDatabase();
        var reorder = Pad(test);
        var product = Add(test, "Panadol 500mg", level: 20, stock: 5);

        var cashier = TestData.Cashier(test);

        var report = reorder.Report(cashier);
        Assert.False(report.Ok);

        var saved = reorder.SetLevel(cashier, product, 50);
        Assert.False(saved.Ok);
        Assert.Equal(20d, Level(test, product));
    }

    [Fact]
    public void TheSettingsSwitchEmptiesThePad()
    {
        using var test = new TestDatabase();
        var reorder = Pad(test);

        Add(test, "Panadol 500mg", level: 20, stock: 0);

        Assert.True(test.Settings.Set("reorder.enabled", false, TestData.Owner).Ok);

        var report = reorder.Report(TestData.Owner);

        Assert.True(report.Ok, report.Error);
        Assert.True(report.Value!.IsSwitchedOff);
        Assert.Empty(report.Value.Rows);
        Assert.Contains("switched off", report.Value.SummaryText);
    }

    [Fact]
    public void SendingAnOrderLeavesOneLineInTheBlackBox()
    {
        using var test = new TestDatabase();
        var reorder = Pad(test);

        reorder.RecordOrder(TestData.Owner, "GSK Depot", 3);

        Assert.Equal(1L, Scalar(test, "SELECT COUNT(1) FROM audit_logs WHERE module = 'reorder' AND action = 'order_text';"));
        Assert.Equal(1L, Scalar(test, "SELECT COUNT(1) FROM audit_logs WHERE module = 'reorder' AND details LIKE '%GSK Depot%';"));
        Assert.Equal(1L, Scalar(test, "SELECT COUNT(1) FROM audit_logs WHERE module = 'reorder' AND details LIKE '%3 medicine%';"));
    }

    // ============================== helpers ==============================

    private static ReorderService Pad(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings, TestData.Permissions(test), test.Log);

    /// <summary>
    /// A medicine with exactly the stock and the minimum the fact is about. The shared factory adds
    /// the batch; the level and the pack formula are set here so each row is one line to read.
    /// </summary>
    private static int Add(
        TestDatabase test,
        string name,
        double level,
        double stock,
        int boxStrips = 2,
        int stripTablets = 10,
        string unit = "tablet")
    {
        var id = TestData.AddProduct(test, batchQty: stock, name: name);

        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
UPDATE products SET reorder_level = @level, base_unit = @unit,
                    box_strips = @box, strip_tablets = @strip
WHERE id = @id;",
                new { level, unit, box = boxStrips, strip = stripTablets, id });
        });

        return id;
    }

    private static void AddPurchase(TestDatabase test, string code, int supplierId, int productId, string date)
    {
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO purchases(code, supplier_id, date, subtotal_paisa, total_paisa, created_at)
VALUES(@code, @supplierId, @date, 10000, 10000, @date);", new { code, supplierId, date });

            var purchaseId = test.Database.NextId(connection);

            test.Database.Execute(connection, @"
INSERT INTO purchase_items(purchase_id, product_id, qty_entered, unit, qty_base, cost_paisa,
                           retail_paisa, line_total_paisa, batch_no, expiry_date)
VALUES(@purchaseId, @productId, 10, 'box', 200, 10000, 15000, 100000, 'B-1', '2027-06-30');",
                new { purchaseId, productId });
        });
    }

    private static double Level(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();

        return Convert.ToDouble(test.Database.Scalar(connection,
            "SELECT reorder_level FROM products WHERE id = @id;", new { id = productId }) ?? 0);
    }

    private static long Scalar(TestDatabase test, string sql)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection, sql) ?? 0);
    }
}
