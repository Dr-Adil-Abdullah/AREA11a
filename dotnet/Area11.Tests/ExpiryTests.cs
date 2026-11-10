using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Expiry levels from Settings (Spec 6.1, F-29). The promises that must hold:
///   • the three bands are the owner's, not three numbers written into the code;
///   • Level 3 is the **nearest** band and a past date is always EXPIRED;
///   • a stored list that is broken, unordered or has two equal bands is repaired, never trusted;
///   • saving two equal bands is refused, because one of them could never be seen;
///   • the stock screen and the dashboard follow the saved numbers on the next refresh.
/// </summary>
public sealed class ExpiryTests
{
    private static StockService Stock(TestDatabase test) =>
        new(test.Database, test.Audit, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static DashboardService Dashboard(TestDatabase test) =>
        new(test.Database, test.Settings,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    /// <summary>A batch that expires exactly <paramref name="days"/> days from today.</summary>
    private static string InDays(int days) =>
        DateTime.Today.AddDays(days).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Moves the demo batch to a chosen expiry date, the way the purchase screen would.</summary>
    private static void SetExpiry(TestDatabase test, int productId, int days)
    {
        using var connection = test.Database.Open();

        test.Database.Execute(connection, @"
UPDATE batches
SET expiry_date = @date, expiry_ym = @ym
WHERE product_id = @productId;",
            new
            {
                date = InDays(days),
                ym = InDays(days).Substring(0, 7),
                productId
            });
    }

    private static List<ExpiryBand> Bands(int level1, int level2, int level3) => new()
    {
        new() { Level = 1, Days = level1, Color = "blue", Label = "Level 1" },
        new() { Level = 2, Days = level2, Color = "yellow", Label = "Level 2" },
        new() { Level = 3, Days = level3, Color = "red", Label = "Level 3" }
    };

    [Fact]
    public void TheLabel_UsesTheBandsItIsGiven_NotTheNumbersInTheCode()
    {
        // 45 days away. Shipped scale (365/180/90) calls it Level 3; a 365/180/30 scale calls it
        // Level 2. The old hard-coded version always said Level 3, which is exactly the bug F-29 fixes.
        Assert.Equal("Level 3 · 45 days", Expiry.StatusLabel(InDays(45), 365, 180, 90));
        Assert.Equal("Level 2 · 45 days", Expiry.StatusLabel(InDays(45), 365, 180, 30));
        Assert.Equal("Level 1 · 45 days", Expiry.StatusLabel(InDays(45), 400, 30, 10));
    }

    [Fact]
    public void TheNearestBandWins_AndAPastDateIsAlwaysExpired()
    {
        // The band edges are inclusive: 90 days is already Level 3 on the shipped scale.
        Assert.Equal(ExpiryKind.Level3, Expiry.KindOf(InDays(90), 365, 180, 90));
        Assert.Equal(ExpiryKind.Level2, Expiry.KindOf(InDays(91), 365, 180, 90));
        Assert.Equal(ExpiryKind.Level1, Expiry.KindOf(InDays(181), 365, 180, 90));
        Assert.Equal(ExpiryKind.Ok, Expiry.KindOf(InDays(366), 365, 180, 90));

        // Yesterday is expired whatever the owner typed - a warning band cannot rescue old stock.
        Assert.Equal(ExpiryKind.Expired, Expiry.KindOf(InDays(-1), 5000, 4000, 3000));
        Assert.Equal("EXPIRED", Expiry.StatusLabel(InDays(-1), 365, 180, 90));

        // No date at all is not "expired"; it is unknown.
        Assert.Equal(ExpiryKind.None, Expiry.KindOf(string.Empty, 365, 180, 90));
        Assert.Equal("—", Expiry.StatusLabel(null, 365, 180, 90));
    }

    [Fact]
    public void TheStoredList_IsSorted_SoTheOwnerMayTypeTheBandsInAnyOrder()
    {
        var levels = ExpiryLevels.Build(Bands(90, 365, 180));

        Assert.Equal(365, levels.Level1Days);
        Assert.Equal(180, levels.Level2Days);
        Assert.Equal(90, levels.Level3Days);
        Assert.Equal(new[] { 1, 2, 3 }, levels.Bands.Select(band => band.Level));
    }

    [Fact]
    public void AnImpossibleStoredList_FallsBackToTheShippedScale()
    {
        // Two bands with the same number of days would hide one colour for ever.
        Assert.Equal(365, ExpiryLevels.Read("not json at all").Level1Days);
        Assert.Equal(365, ExpiryLevels.Read("[]").Level1Days);

        const string duplicates = "[{\"level\":1,\"days\":180,\"color\":\"blue\",\"label\":\"A\"}," +
                                  "{\"level\":2,\"days\":180,\"color\":\"yellow\",\"label\":\"B\"}," +
                                  "{\"level\":3,\"days\":90,\"color\":\"red\",\"label\":\"C\"}]";

        Assert.Equal(365, ExpiryLevels.Read(duplicates).Level1Days);

        // Negative and zero days are pushed back to a usable number instead of crashing a screen.
        var repaired = ExpiryLevels.Build(Bands(365, 180, 0));
        Assert.Equal(1, repaired.Level3Days);
    }

    [Fact]
    public void TheSavedJson_KeepsTheShapeTheSchemaShipsWith()
    {
        // The reference settings table stores lowercase level/days/color/label; writing PascalCase
        // here would make the same database look different to the two versions of the app.
        var json = ExpiryLevels.Default.ToJson();

        Assert.Contains("\"level\":1", json);
        Assert.Contains("\"days\":365", json);
        Assert.Contains("\"color\":\"blue\"", json);
        Assert.Contains("\"label\":", json);
        Assert.DoesNotContain("\"Level\"", json);
    }

    [Fact]
    public void SavingTwoEqualBands_IsRefused_ButAGoodSetIsStored()
    {
        using var test = new TestDatabase();

        var refused = test.Settings.SetMany(
            new Dictionary<string, object?> { ["expiry.levels"] = Bands(365, 180, 180) },
            TestData.Owner);

        Assert.False(refused.Ok);
        Assert.Contains("different", refused.Error!, StringComparison.OrdinalIgnoreCase);

        var saved = test.Settings.SetMany(
            new Dictionary<string, object?> { ["expiry.levels"] = Bands(200, 120, 45) },
            TestData.Owner);

        Assert.True(saved.Ok, saved.Error);

        var levels = test.Settings.ExpiryBands;
        Assert.Equal(200, levels.Level1Days);
        Assert.Equal(120, levels.Level2Days);
        Assert.Equal(45, levels.Level3Days);
    }

    [Fact]
    public void BandsHandedOverAsJsonText_AreStoredAsAnArrayNotAsAQuotedString()
    {
        using var test = new TestDatabase();

        // Two doorways into the same setting: the Settings page sends three objects, an import or an
        // old test sends the JSON text. Both must end up in the row as an ARRAY. Serialising the text
        // turned it into "[\{...\}]" (a string, not a list), ExpiryLevels.Read could not parse it and
        // the shop fell back to 365 / 180 / 90 without saying a word - the owner's PC found it as
        // "expected 0, got 1" on the home screen.
        var text = "[{\"days\":400,\"color\":\"blue\",\"label\":\"\"},"
                   + "{\"days\":120,\"color\":\"amber\",\"label\":\"\"},"
                   + "{\"days\":1,\"color\":\"red\",\"label\":\"\"}]";

        var saved = test.Settings.SetMany(
            new Dictionary<string, object?> { ["expiry.levels"] = text },
            TestData.Owner);

        Assert.True(saved.Ok, saved.Error);

        using (var connection = test.Database.Open())
        {
            var stored = test.Database.Scalar(connection,
                "SELECT value FROM settings WHERE key = 'expiry.levels';")?.ToString() ?? string.Empty;

            Assert.StartsWith("[", stored);
            Assert.DoesNotContain("\\\"days\\\"", stored);
        }

        var levels = test.Settings.ExpiryBands;

        Assert.Equal(400, levels.Level1Days);
        Assert.Equal(120, levels.Level2Days);
        Assert.Equal(1, levels.Level3Days);
    }

    [Fact]
    public void TheBatchLabel_FollowsTheSavedBands()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 50);
        SetExpiry(test, product, 75);

        var shipped = Stock(test).Batches(product).Value!.Single();
        Assert.Equal("Level 3 · 75 days", shipped.ExpiryLabel);

        test.Settings.SetMany(
            new Dictionary<string, object?> { ["expiry.levels"] = Bands(400, 120, 45) },
            TestData.Owner);

        var changed = Stock(test).Batches(product).Value!.Single();
        Assert.Equal("Level 2 · 75 days", changed.ExpiryLabel);

        // The colour trigger the shelf list reads moves with the band as well.
        Assert.Equal("level2", changed.ExpiryTone);
    }

    [Fact]
    public void TheDashboardWindow_IsTheNearestBand()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, batchQty: 20);
        SetExpiry(test, product, 75);

        // Shipped scale: 75 days is inside Level 3 (90), so the card warns.
        var before = Dashboard(test).GetSnapshot(TestData.Owner);
        Assert.True(before.Ok, before.Error);
        Assert.Equal(1, before.Value!.ExpiringSoon);

        // A one-day nearest band leaves the same batch outside the window. The dashboard compares
        // calendar MONTHS (it is a reminder, not a count), so the test uses the extreme that is true
        // on every day of the year: a batch 75 days away is always at least two months ahead.
        test.Settings.SetMany(
            new Dictionary<string, object?> { ["expiry.levels"] = Bands(400, 120, 1) },
            TestData.Owner);

        var after = Dashboard(test).GetSnapshot(TestData.Owner);
        Assert.True(after.Ok, after.Error);
        Assert.Equal(0, after.Value!.ExpiringSoon);
    }

    // ------------------------------------------------------------------ Portion 6: the picker

    [Fact]
    public void WhatTheStripPrints_MeansThisCentury()
    {
        // A strip says "06/27", not "2027-06": the picker era reads it as typed.
        Assert.True(Expiry.TryNormalize("06/27", out var date, out var ym));
        Assert.Equal("2027-06-30", date);
        Assert.Equal("2027-06", ym);

        Assert.True(Expiry.TryNormalize("6/27", out date, out ym));
        Assert.Equal("2027-06-30", date);

        Assert.True(Expiry.TryNormalize("06-27", out date, out ym));
        Assert.Equal("2027-06-30", date);

        // No century pivot: two digits always mean 2000-2099, never the 1900s.
        Assert.True(Expiry.TryNormalize("12/99", out date, out ym));
        Assert.Equal("2099-12-31", date);
        Assert.Equal("2099-12", ym);
    }

    [Fact]
    public void SingleDigitMonths_AreAcceptedBothWaysRound()
    {
        Assert.True(Expiry.TryNormalize("6/2027", out var date, out var ym));
        Assert.Equal("2027-06-30", date);
        Assert.Equal("2027-06", ym);

        Assert.True(Expiry.TryNormalize("2027-6", out date, out _));
        Assert.Equal("2027-06-30", date);
    }

    [Fact]
    public void TheOldForms_MeanExactlyWhatTheyAlwaysMeant()
    {
        Assert.True(Expiry.TryNormalize("2027-06", out var date, out var ym));
        Assert.Equal("2027-06-30", date);
        Assert.Equal("2027-06", ym);

        Assert.True(Expiry.TryNormalize("2027/06", out date, out _));
        Assert.Equal("2027-06-30", date);

        Assert.True(Expiry.TryNormalize("06/2027", out date, out _));
        Assert.Equal("2027-06-30", date);

        Assert.True(Expiry.TryNormalize("2027-06-30", out date, out ym));
        Assert.Equal("2027-06-30", date);
        Assert.Equal("2027-06", ym);

        Assert.True(Expiry.TryNormalize("30/06/2027", out date, out _));
        Assert.Equal("2027-06-30", date);

        // February still ends on the 28th (2027 is not a leap year).
        Assert.True(Expiry.TryNormalize("02/27", out date, out _));
        Assert.Equal("2027-02-28", date);
    }

    [Fact]
    public void Nonsense_StillFails_Loudly()
    {
        string[] bad =
        {
            "13/27", "00/27", "06/2", "06/277", "27/06", "2027-13", "2027-00",
            "2027-06-31", "30-02-2027", "abcd", "2027/06/30/1", "06//27"
        };
        foreach (var text in bad)
        {
            Assert.False(Expiry.TryNormalize(text, out _, out _), text);
            Assert.False(Expiry.TryDescribe(text, out _), text);
        }

        // Blank is not nonsense: it means "no expiry", and the services treat it as none.
        Assert.False(Expiry.TryNormalize(string.Empty, out _, out _));
        Assert.False(Expiry.TryNormalize("   ", out _, out _));
    }

    [Fact]
    public void TheLiveLine_SaysWhatWouldBeStored()
    {
        Assert.True(Expiry.TryDescribe(string.Empty, out var blank));
        Assert.Equal("No expiry entered.", blank);

        Assert.True(Expiry.TryDescribe("06/27", out var month));
        Assert.Equal("June 2027 · stored as 2027-06-30", month);

        Assert.True(Expiry.TryDescribe("2027-06", out var monthFirst));
        Assert.Equal("June 2027 · stored as 2027-06-30", monthFirst);

        Assert.True(Expiry.TryDescribe("2027-06-15", out var day));
        Assert.Equal("15 June 2027 · stored as 2027-06-15", day);

        Assert.False(Expiry.TryDescribe("junk", out _));
    }

    [Fact]
    public void TheMonthList_HasTwelveNamedMonths()
    {
        var months = Expiry.MonthOptions();

        Assert.Equal(12, months.Count);
        Assert.Equal(6, months[5].Number);
        Assert.Equal("06 — June", months[5].Label);
        Assert.Equal("12 — December", months[11].Label);
        Assert.Equal(Enumerable.Range(1, 12), months.Select(month => month.Number));
    }

    [Fact]
    public void TheYearList_CountsFromTheGivenYear()
    {
        Assert.Equal(new[] { 2026, 2027, 2028 }, Expiry.YearOptions(2026, 3));
        Assert.Equal(13, Expiry.YearOptions(2026, 13).Count);
        Assert.Empty(Expiry.YearOptions(2026, 0));
    }

    [Fact]
    public void APurchaseMayCarry_TheStripForm_EndToEnd()
    {
        using var test = new TestDatabase();
        var supplier = TestData.AddSupplier(test);
        var product = TestData.AddProduct(test, name: "Augmentin 625mg");

        var purchase = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = supplier,
            Lines = new List<PurchaseLineInput>
            {
                new()
                {
                    ProductId = product,
                    Unit = "base",
                    Quantity = 2,
                    CostPaisa = 10000,
                    RetailPaisa = 15000,
                    BatchNo = "B-7",
                    Expiry = "06/27"
                }
            }
        }, TestData.Owner);

        Assert.True(purchase.Ok, purchase.Error);

        // The batch carries the normalized pair, exactly like a "2027-06" line would.
        using var connection = test.Database.Open();
        var stored = test.Database.QuerySingle<string>(connection,
            "SELECT expiry_date || '|' || expiry_ym FROM batches WHERE product_id = @id AND batch_no = 'B-7';",
            new { id = product });
        Assert.Equal("2027-06-30|2027-06", stored);
    }
}
