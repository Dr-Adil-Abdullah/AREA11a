using System.Globalization;
using System.IO;
using System.Text.Json;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Cloud sync (F-48, Spec 25): push, look, bring - and the one rule that matters most, that a pull can
/// never write itself. Every test here runs against a fake wire, so the suite needs no internet, no
/// Supabase account and no network card.
/// </summary>
public sealed class SyncTests
{
    // ============================== the document ==============================

    [Fact]
    public void TheSixListsTravelWholeAndTheMoneyTravelsNinetyDays()
    {
        using var test = new TestDatabase();
        Configure(test);

        var oldSale = Sale(test, "INV-OLD", 200);
        var newSale = Sale(test, "INV-NEW", 2);

        var envelope = Cloud(test, new FakeCloud()).Build(TestData.Owner).Value!;

        Assert.True(envelope.RowCount > 0);
        Assert.Equal(1, envelope.CountOf("sales"));
        Assert.Equal(newSale, Convert.ToInt64(envelope.Tables["sales"][0]["id"]));
        Assert.DoesNotContain(envelope.Tables["sales"],
            row => Convert.ToInt64(row["id"]) == oldSale);
    }

    [Fact]
    public void PhotosNeverTravel()
    {
        // Spec 25.5: photos go in the zip backup, not through the cloud - a shop's customers' faces
        // must not end up in a free database by accident.
        Assert.DoesNotContain("photos", SyncTables.Order);
        Assert.DoesNotContain("photos", SyncTables.Recent);
    }

    [Fact]
    public void TheDocumentKnowsWhichShopAndWhichBuildMadeIt()
    {
        using var test = new TestDatabase();
        Configure(test, "shop-main");

        var envelope = Cloud(test, new FakeCloud()).Build(TestData.Owner).Value!;

        Assert.Equal("shop-main", envelope.ShopId);
        Assert.Equal("Area11 Pharmacy", envelope.ShopName);
        Assert.Equal(SyncEnvelope.CurrentFormat, envelope.Format);
        Assert.Equal(BuildInfo.Version, envelope.Version);
        Assert.True(envelope.SizeBytes > 0);
    }

    // ============================== dependencies around the 90-day cut ==============================

    [Fact]
    public void ARecentCollectionBringsItsOlderSaleAndAllTheSaleLines()
    {
        using var source = new TestDatabase();
        Configure(source);
        var product = TestData.AddProduct(source, name: "Cloud sale medicine");
        var batch = BatchOf(source, product);
        var sale = Sale(source, "INV-OLD-COLLECTION", 200);
        SaleLine(source, sale, product, batch, "Cloud sale medicine", 1);
        SaleLine(source, sale, product, batch, "Cloud sale medicine", 2);
        Payment(source, saleId: sale);

        var envelope = Cloud(source, new FakeCloud()).Build(TestData.Owner).Value!;

        Assert.Equal(1, envelope.CountOf("sales"));
        Assert.Equal(2, envelope.CountOf("sale_items"));
        Assert.Equal(1, envelope.CountOf("payments"));

        using var target = new TestDatabase();
        Configure(target);
        var applied = Cloud(target, new FakeCloud()).Apply(TestData.Owner, envelope, confirmed: true);

        Assert.True(applied.Ok, applied.Error);
        Assert.Equal(1, Count(target, "sales"));
        Assert.Equal(2, Count(target, "sale_items"));
        Assert.Equal(1, Count(target, "payments"));
    }

    [Fact]
    public void ARecentSupplierPaymentBringsItsOlderPurchaseAndLines()
    {
        using var source = new TestDatabase();
        Configure(source);
        var product = TestData.AddProduct(source, name: "Cloud purchase medicine");
        var batch = BatchOf(source, product);
        var purchase = Purchase(source, "PINV-OLD-PAYMENT", product, batch, daysAgo: 200);
        Payment(source, purchaseId: purchase);

        var envelope = Cloud(source, new FakeCloud()).Build(TestData.Owner).Value!;

        Assert.Equal(1, envelope.CountOf("purchases"));
        Assert.Equal(1, envelope.CountOf("purchase_items"));
        Assert.Equal(1, envelope.CountOf("payments"));

        using var target = new TestDatabase();
        Configure(target);
        var applied = Cloud(target, new FakeCloud()).Apply(TestData.Owner, envelope, confirmed: true);

        Assert.True(applied.Ok, applied.Error);
        Assert.Equal(1, Count(target, "purchases"));
        Assert.Equal(1, Count(target, "purchase_items"));
        Assert.Equal(1, Count(target, "payments"));
    }

    [Fact]
    public void ARecentProvisionalPaymentBringsTheWholeReconciliationChain()
    {
        using var source = new TestDatabase();
        Configure(source);
        var product = TestData.AddProduct(source, name: "Cloud PR medicine");
        var batch = BatchOf(source, product);
        var sale = Sale(source, "INV-OLD-PR", 200);
        var saleItem = SaleLine(source, sale, product, batch, "Cloud PR medicine", 1);
        var provisional = Provisional(source, sale, saleItem, product, batch, daysAgo: 180);
        Payment(source, provisionalId: provisional);

        var envelope = Cloud(source, new FakeCloud()).Build(TestData.Owner).Value!;

        Assert.Equal(1, envelope.CountOf("sales"));
        Assert.Equal(1, envelope.CountOf("sale_items"));
        Assert.Equal(1, envelope.CountOf("provisional_returns"));
        Assert.Equal(1, envelope.CountOf("provisional_items"));
        Assert.Equal(1, envelope.CountOf("provisional_return_allocations"));
        Assert.Equal(1, envelope.CountOf("payments"));

        using var target = new TestDatabase();
        Configure(target);
        var applied = Cloud(target, new FakeCloud()).Apply(TestData.Owner, envelope, confirmed: true);

        Assert.True(applied.Ok, applied.Error);
        foreach (var table in new[]
                 {
                     "sales", "sale_items", "provisional_returns", "provisional_items",
                     "provisional_return_allocations", "payments"
                 })
        {
            Assert.Equal(1, Count(target, table));
        }
    }

    // ============================== push ==============================

    [Fact]
    public void AShopWithNoAddressIsAskedForTheSettingsFirst()
    {
        using var test = new TestDatabase();
        var wire = new FakeCloud();

        var pushed = Cloud(test, wire).Push(TestData.Owner);

        Assert.False(pushed.Ok);
        Assert.Contains("Settings", pushed.Error);
        Assert.Equal(0, wire.Sends);
    }

    [Fact]
    public void PushSendsOneDocumentAndLeavesOneLineInTheBlackBox()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var wire = new FakeCloud();
        var pushed = Cloud(test, wire).Push(TestData.Owner);

        Assert.True(pushed.Ok, pushed.Error);
        Assert.Equal(1, wire.Sends);
        Assert.Contains("\"shopId\":\"shop-1\"", wire.SentJson!);
        Assert.Contains("\"products\"", wire.SentJson!);
        Assert.Equal(1, Audits(test, "sync_push"));
        Assert.Equal("pushed", test.Settings.GetString("sync.status"));
        Assert.False(string.IsNullOrWhiteSpace(test.Settings.GetString("sync.lastSyncAt")));
    }

    [Fact]
    public void ARotatedKeyIsNamedAsAKey()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var wire = new FakeCloud { SendAnswer = SyncTransportResult.Fail(401, "{\"message\":\"Invalid API key\"}") };
        var pushed = Cloud(test, wire).Push(TestData.Owner);

        Assert.False(pushed.Ok);
        Assert.Contains("401", pushed.Error);
        Assert.Contains("key", pushed.Error);
        Assert.Equal("error", test.Settings.GetString("sync.status"));
    }

    [Fact]
    public void AWrongTableIsNamedAsA404()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var wire = new FakeCloud { SendAnswer = SyncTransportResult.Fail(404, string.Empty) };
        var pushed = Cloud(test, wire).Push(TestData.Owner);

        Assert.False(pushed.Ok);
        Assert.Contains("404", pushed.Error);
    }

    [Fact]
    public void NoInternetIsSaidInTheOwnersWords()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var wire = new FakeCloud
        {
            SendAnswer = SyncTransportResult.Fail(0, string.Empty, "No such host is known.")
        };

        var pushed = Cloud(test, wire).Push(TestData.Owner);

        Assert.False(pushed.Ok);
        Assert.Contains("internet", pushed.Error);
    }

    // ============================== look ==============================

    [Fact]
    public void PreviewWritesNothingAtAll()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var wire = new FakeCloud
        {
            FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", envelope =>
                envelope.Add("products", Row(99, "Panadol Extra"))))
        };

        var before = Count(test, "products");
        var preview = Cloud(test, wire).Preview(TestData.Owner);

        Assert.True(preview.Ok, preview.Error);
        Assert.Equal(1, wire.Fetches);
        Assert.Equal(before, Count(test, "products"));
        Assert.Equal(0, Audits(test, "sync_apply"));
        Assert.Null(PreviewRow(test));
    }

    [Fact]
    public void AnEmptyCloudAsksForAPushFirst()
    {
        using var test = new TestDatabase();
        Configure(test);

        var wire = new FakeCloud { FetchAnswer = SyncTransportResult.Success(200, "[]") };
        var preview = Cloud(test, wire).Preview(TestData.Owner);

        Assert.False(preview.Ok);
        Assert.Contains("push", preview.Error);
    }

    [Fact]
    public void ThePreviewPutsTheCloudRowsBesideTheRowsHere()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var wire = new FakeCloud
        {
            FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", envelope =>
            {
                envelope.Add("products", Row(97, "A"));
                envelope.Add("products", Row(98, "B"));
                envelope.Add("products", Row(99, "C"));
            }))
        };

        var preview = Cloud(test, wire).Preview(TestData.Owner).Value!;
        var products = preview.Tables.Single(table => table.Table == "products");

        Assert.Equal(3, products.Cloud);
        Assert.Equal(1, products.Here);
        Assert.Equal("+2", products.DifferenceLabel);
        Assert.True(preview.HasDifferences);
        Assert.True(preview.HereRows >= 1);
        Assert.Equal(3, preview.Rows);
    }

    // ============================== bring ==============================

    [Fact]
    public void ABringWithoutTheTickIsRefused()
    {
        using var test = new TestDatabase();
        Configure(test);

        var wire = new FakeCloud
        {
            FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", envelope =>
                envelope.Add("products", Row(99, "Panadol Cloud"))))
        };

        var cloud = Cloud(test, wire);
        var preview = cloud.Preview(TestData.Owner).Value!;
        var refused = cloud.Apply(TestData.Owner, preview.Envelope, confirmed: false);

        Assert.False(refused.Ok);
        Assert.Contains("Tick", refused.Error);
        Assert.Equal(0, Count(test, "products"));
    }

    [Fact]
    public void SwitchingCloudOffAfterPreviewAlsoClosesTheCoreWriteDoor()
    {
        using var test = new TestDatabase();
        Configure(test);
        var backups = NewFolder();
        var wire = new FakeCloud
        {
            FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", envelope =>
                envelope.Add("products", Row(99, "Panadol Cloud"))))
        };
        var cloud = Cloud(test, wire, backups);
        var preview = cloud.Preview(TestData.Owner).Value!;
        Assert.True(test.Settings.SetUiOnly(SyncService.EnabledKey, false).Ok);

        var refused = cloud.Apply(TestData.Owner, preview.Envelope, confirmed: true);

        Assert.False(refused.Ok);
        Assert.Contains("switched off", refused.Error);
        Assert.Equal(0, Count(test, "products"));
        Assert.Empty(Directory.GetFiles(backups));
    }

    [Fact]
    public void AFutureFormatCannotBypassPreviewAndReachTheWriter()
    {
        using var test = new TestDatabase();
        Configure(test);
        var backups = NewFolder();
        var envelope = new SyncEnvelope
        {
            Format = SyncEnvelope.CurrentFormat + 1,
            ShopId = "shop-1"
        };
        envelope.Add("products", Row(99, "Future Panadol"));

        var refused = Cloud(test, new FakeCloud(), backups)
            .Apply(TestData.Owner, envelope, confirmed: true);

        Assert.False(refused.Ok);
        Assert.Contains("format", refused.Error);
        Assert.Equal(0, Count(test, "products"));
        Assert.Empty(Directory.GetFiles(backups));
    }

    [Fact]
    public void ANullCloudIdIsSkippedInsteadOfBecomingANewLocalRow()
    {
        using var test = new TestDatabase();
        Configure(test);
        var envelope = new SyncEnvelope { ShopId = "shop-1" };
        envelope.Add("products", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = null,
            ["name"] = "Not a real keyed row"
        });

        var brought = Cloud(test, new FakeCloud()).Apply(TestData.Owner, envelope, confirmed: true);

        Assert.True(brought.Ok, brought.Error);
        Assert.Equal(0, brought.Value!.Rows);
        Assert.Equal(1, brought.Value.Skipped);
        Assert.Equal(0, Count(test, "products"));
    }

    [Fact]
    public void ABringTakesABackupAndWritesTheRows()
    {
        using var test = new TestDatabase();
        Configure(test);

        var backups = NewFolder();

        var wire = new FakeCloud
        {
            FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", envelope =>
                envelope.Add("products", Row(99, "Panadol Cloud"))))
        };

        var cloud = Cloud(test, wire, backups);
        var preview = cloud.Preview(TestData.Owner).Value!;
        var brought = cloud.Apply(TestData.Owner, preview.Envelope, confirmed: true);

        Assert.True(brought.Ok, brought.Error);
        Assert.Equal(1, Count(test, "products"));
        Assert.Equal("Panadol Cloud", NameOf(test, 99));
        Assert.True(File.Exists(Path.Combine(backups, brought.Value!.BackupName)));
        Assert.Equal(1, Audits(test, "sync_apply"));
        Assert.Equal("applied", test.Settings.GetString("sync.status"));
    }

    [Fact]
    public void ABringNeverDeletesARowTheCloudDidNotSend()
    {
        using var test = new TestDatabase();
        Configure(test);

        var today = Sale(test, "INV-TODAY", 0);

        var wire = new FakeCloud
        {
            FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", envelope =>
                envelope.Add("products", Row(99, "Panadol Cloud"))))
        };

        var cloud = Cloud(test, wire);
        var preview = cloud.Preview(TestData.Owner).Value!;
        Assert.True(cloud.Apply(TestData.Owner, preview.Envelope, confirmed: true).Ok);

        // The cloud copy is older than the till. The bill sold ten minutes ago must still be here.
        Assert.Equal(1, Count(test, "sales"));
        Assert.Equal(today, Convert.ToInt64(Scalar(test, "SELECT id FROM sales LIMIT 1;")));
    }

    [Fact]
    public void ACopyFromAnotherShopIsRefused()
    {
        using var test = new TestDatabase();
        Configure(test, "shop-main");

        var wire = new FakeCloud
        {
            FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-other", envelope =>
                envelope.Add("products", Row(99, "Panadol Cloud"))))
        };

        var cloud = Cloud(test, wire);
        var preview = cloud.Preview(TestData.Owner).Value!;
        var refused = cloud.Apply(TestData.Owner, preview.Envelope, confirmed: true);

        Assert.False(refused.Ok);
        Assert.Contains("belongs", refused.Error);
        Assert.Equal(0, Count(test, "products"));
    }

    [Fact]
    public void ABrokenRowLeavesTheWholeShopAsItWas()
    {
        using var test = new TestDatabase();
        Configure(test);

        var backups = NewFolder();

        var wire = new FakeCloud
        {
            FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", envelope =>
            {
                envelope.Add("products", Row(98, "Good Row"));

                // `name` is NOT NULL: this row cannot be written, and the good row before it must not
                // stay behind on its own (one transaction, Spec 25.4).
                var broken = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = 97L,
                    ["name"] = null
                };

                envelope.Add("products", broken);
            }))
        };

        var cloud = Cloud(test, wire, backups);
        var preview = cloud.Preview(TestData.Owner).Value!;
        var brought = cloud.Apply(TestData.Owner, preview.Envelope, confirmed: true);

        Assert.False(brought.Ok);
        Assert.Equal(0, Count(test, "products"));
        Assert.Equal(0, Audits(test, "sync_apply"));
    }

    [Fact]
    public void AColumnThisDatabaseDoesNotHaveIsIgnored()
    {
        using var test = new TestDatabase();
        Configure(test);

        var wire = new FakeCloud
        {
            FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", envelope =>
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = 96L,
                    ["name"] = "Panadol Safe",
                    ["x\"); DROP TABLE products;--"] = "boom"
                };

                envelope.Add("products", row);
            }))
        };

        var cloud = Cloud(test, wire);
        var preview = cloud.Preview(TestData.Owner).Value!;
        var brought = cloud.Apply(TestData.Owner, preview.Envelope, confirmed: true);

        Assert.True(brought.Ok, brought.Error);
        Assert.Equal(1, Count(test, "products"));
        Assert.Equal("Panadol Safe", NameOf(test, 96));
    }

    // ============================== who may do it ==============================

    [Fact]
    public void ACashierCannotPushLookOrBring()
    {
        using var test = new TestDatabase();
        Configure(test);

        var wire = new FakeCloud();
        var cloud = Cloud(test, wire);
        var cashier = TestData.Cashier(test);

        Assert.False(cloud.CanSync(cashier));
        Assert.False(cloud.Push(cashier).Ok);
        Assert.False(cloud.Preview(cashier).Ok);
        Assert.False(cloud.Apply(cashier, null, true).Ok);
        Assert.Equal(0, wire.Sends);
        Assert.Equal(0, wire.Fetches);
    }

    [Fact]
    public void AManagerDoesNotGetCloudSyncByDefault()
    {
        // D-66: the shop's whole database leaving the building is not a default, not even for a manager.
        Assert.False(PermissionService.RoleDefault(UserRole.Manager, Modules.Sync));
        Assert.True(PermissionService.RoleDefault(UserRole.Owner, Modules.Sync));
        Assert.Equal("Cloud sync", Modules.Label(Modules.Sync));
    }

    [Fact]
    public void TheKeyNeverReachesTheBlackBox()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var cloud = Cloud(test, new FakeCloud());
        Assert.True(cloud.Push(TestData.Owner).Ok);

        Assert.DoesNotContain("secret-key-1234-5678", AuditDetails(test, "sync_push"));
        Assert.Contains("****", cloud.KeyLabel);
        Assert.DoesNotContain("secret-key-1234-5678", cloud.KeyLabel);
    }

    // ============================== the row on the wire ==============================

    [Fact]
    public void APushSendsTheTablesOwnRowAndNotTheDocument()
    {
        // The defect this pins (found after v0.21.0): the push posted the document itself, so PostgREST
        // was asked for a `format` column the table does not have and refused the whole push.
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var wire = new FakeCloud();
        var pushed = Cloud(test, wire).Push(TestData.Owner);

        Assert.True(pushed.Ok, pushed.Error);
        Assert.NotNull(wire.SentJson);

        using var document = JsonDocument.Parse(wire.SentJson!);
        var row = document.RootElement;

        Assert.Equal("shop-1", row.GetProperty("shop_id").GetString());
        Assert.Equal("Area11 Pharmacy", row.GetProperty("shop_name").GetString());
        Assert.Equal(JsonValueKind.Object, row.GetProperty("payload").ValueKind);

        Assert.False(row.TryGetProperty("shopId", out _));   // the camelCase document is INSIDE payload
        Assert.False(row.TryGetProperty("format", out _));   // there is no such column in the table
    }

    [Fact]
    public void TheRowsVersionIsTheFormatAndTheProgramVersionStaysInTheDocument()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var wire = new FakeCloud();
        Assert.True(Cloud(test, wire).Push(TestData.Owner).Ok);

        using var document = JsonDocument.Parse(wire.SentJson!);
        var row = document.RootElement;

        Assert.True(row.GetProperty("version").TryGetInt32(out var format));
        Assert.Equal(SyncEnvelope.CurrentFormat, format);       // the format, as an int column

        Assert.Equal(BuildInfo.Version,
            row.GetProperty("payload").GetProperty("version").GetString());
    }

    [Fact]
    public void TheSentStampCarriesTheShopsClockAndItsOffset()
    {
        using var test = new TestDatabase();
        Configure(test);
        TestData.AddProduct(test);

        var wire = new FakeCloud();
        Assert.True(Cloud(test, wire).Push(TestData.Owner).Ok);

        using var document = JsonDocument.Parse(wire.SentJson!);
        var stamp = document.RootElement.GetProperty("sent_at").GetString();

        Assert.False(string.IsNullOrWhiteSpace(stamp));
        Assert.True(DateTimeOffset.TryParseExact(stamp, "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _), stamp);
    }

    // ============================== the switch ==============================

    [Fact]
    public void ASwitchedOffShopIsToldTheSwitchIsOffRatherThanAskedToFillTheForm()
    {
        using var test = new TestDatabase();
        Configure(test);
        Assert.True(test.Settings.SetUiOnly(SyncService.EnabledKey, false).Ok);

        var refused = Cloud(test, new FakeCloud()).Push(TestData.Owner);

        Assert.False(refused.Ok);
        Assert.Contains("switched off", refused.Error!);
        Assert.Contains("Settings -> Cloud sync", refused.Error!);
    }

    [Fact]
    public void ThePageSaysTheCloudIsSwitchedOffRatherThanNotSetUp()
    {
        using var test = new TestDatabase();
        Configure(test);
        Assert.True(test.Settings.SetUiOnly(SyncService.EnabledKey, false).Ok);

        var status = Cloud(test, new FakeCloud()).Status(TestData.Owner).Value!;

        Assert.False(status.Enabled);
        Assert.False(status.Configured);
        Assert.Equal("Cloud sync is switched off", status.ConfigLabel);
    }

    // ============================== reading the cloud back ==============================

    [Fact]
    public void AnOlderFormatOneDocumentCanStillBeReadAndApplied()
    {
        using var test = new TestDatabase();
        Configure(test);
        var wire = new FakeCloud
        {
            FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", envelope =>
                envelope.Add("products", Row(99, "Format One Panadol")), format: 1))
        };
        var cloud = Cloud(test, wire);

        var preview = cloud.Preview(TestData.Owner);
        Assert.True(preview.Ok, preview.Error);
        Assert.Equal(1, preview.Value!.Envelope!.Format);
        var brought = cloud.Apply(TestData.Owner, preview.Value.Envelope, confirmed: true);

        Assert.True(brought.Ok, brought.Error);
        Assert.Equal("Format One Panadol", NameOf(test, 99));
    }

    [Fact]
    public void ThePreviewShowsTheProgramVersionAndNeverTheFormatNumber()
    {
        using var test = new TestDatabase();
        Configure(test);

        var wire = new FakeCloud { FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", _ => { })) };
        var preview = Cloud(test, wire).Preview(TestData.Owner).Value!;

        Assert.Equal("0.21.0", preview.Version);   // the document's text, not the row's integer format
    }

    [Fact]
    public void ACloudStampIsShownInTheShopsOwnTime()
    {
        using var test = new TestDatabase();
        Configure(test);

        var wire = new FakeCloud { FetchAnswer = SyncTransportResult.Success(200, CloudBody("shop-1", _ => { })) };
        var preview = Cloud(test, wire).Preview(TestData.Owner).Value!;

        var expected = DateTimeOffset.Parse("2026-10-20T10:00:00+05:00", CultureInfo.InvariantCulture)
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        Assert.Equal(expected, preview.SentAt);
    }

    // ============================== helpers ==============================

    /// <summary>One fake wire, so a push or a pull can be exercised without an internet card.</summary>
    private sealed class FakeCloud : ICloudTransport
    {
        public string? SentJson { get; private set; }

        public int Sends { get; private set; }

        public int Fetches { get; private set; }

        public SyncTransportResult SendAnswer { get; init; } = SyncTransportResult.Success(201, string.Empty);

        public SyncTransportResult FetchAnswer { get; init; } =
            SyncTransportResult.Success(200, "[]");

        public SyncTransportResult Send(string url, string key, string table, string json)
        {
            Sends++;
            SentJson = json;
            return SendAnswer;
        }

        public SyncTransportResult Fetch(string url, string key, string table, string shopId)
        {
            Fetches++;
            return FetchAnswer;
        }
    }

    private static SyncService Cloud(TestDatabase test, ICloudTransport wire, string? backupsFolder = null)
    {
        var permissions = new PermissionService(test.Database, test.Log, test.Audit);

        var backup = new BackupService(test.Database, test.Settings, test.Audit, permissions, test.Log,
            NewFolder(), backupsFolder ?? NewFolder());

        return new SyncService(test.Database, test.Settings, test.Audit, permissions, test.Log, backup, wire);
    }

    private static void Configure(TestDatabase test, string shopId = "shop-1")
    {
        // SetUiOnly: the point of these tests is not to audit a settings screen, it is the sync itself.
        // The switch is ticked here because it is the same door the owner uses (v0.26.0): a shop whose
        // `sync.enabled` is off is told so, and never sends anything.
        Assert.True(test.Settings.SetUiOnly(SyncService.EnabledKey, true).Ok);
        Assert.True(test.Settings.SetUiOnly("sync.url", "https://shop.supabase.co").Ok);
        Assert.True(test.Settings.SetUiOnly("sync.key", "secret-key-1234-5678").Ok);
        Assert.True(test.Settings.SetUiOnly("sync.table", "area11_sync").Ok);
        Assert.True(test.Settings.SetUiOnly("sync.shopId", shopId).Ok);
    }

    private static string CloudBody(
        string shopId,
        Action<SyncEnvelope> fill,
        int format = SyncEnvelope.CurrentFormat)
    {
        var envelope = new SyncEnvelope
        {
            Format = format,
            ShopId = shopId,
            ShopName = "Area11 Pharmacy",
            MadeAt = Clock.Now(),
            Version = "0.21.0"
        };

        fill(envelope);

        // The shape PostgREST returns: an array of rows, with the document inside `payload`.
        return "[{\"shop_name\":\"Area11 Pharmacy\",\"sent_at\":\"2026-10-20T10:00:00+05:00\","
               + "\"version\":" + format.ToString(CultureInfo.InvariantCulture)
               + ",\"payload\":" + JsonSerializer.Serialize(envelope) + "}]";
    }

    private static Dictionary<string, object?> Row(long id, string name) =>
        new(StringComparer.Ordinal) { ["id"] = id, ["name"] = name };

    private static long BatchOf(TestDatabase test, int product)
    {
        using var connection = test.Database.Open();
        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT id FROM batches WHERE product_id = @product;", new { product }) ?? 0L);
    }

    private static long Payment(
        TestDatabase test,
        long? saleId = null,
        long? purchaseId = null,
        long? provisionalId = null) =>
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, sale_id, purchase_id, provisional_return_id)
VALUES(@date, 'cash', 100, @saleId, @purchaseId, @provisionalId);",
                new { date = Clock.Now(), saleId, purchaseId, provisionalId });

            return test.Database.NextId(connection);
        });

    private static long SaleLine(
        TestDatabase test,
        long sale,
        int product,
        long batch,
        string name,
        double qty) =>
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO sale_items(sale_id, product_id, batch_id, name_snapshot, qty_base, unit_sold,
                       qty_entered, unit_price_paisa, line_total_paisa, cost_paisa_at_sale)
VALUES(@sale, @product, @batch, @name, @qty, 'base', @qty, 30000, 30000, 20000);",
                new { sale, product, batch, name, qty });

            return test.Database.NextId(connection);
        });

    private static long Purchase(
        TestDatabase test,
        string code,
        int product,
        long batch,
        int daysAgo) =>
        test.Database.Tx(connection =>
        {
            var date = Clock.DateDaysAgo(daysAgo) + " 10:00:00";
            test.Database.Execute(connection, @"
INSERT INTO purchases(code, date, subtotal_paisa, total_paisa, paid_paisa, due_paisa, created_at)
VALUES(@code, @date, 20000, 20000, 0, 20000, @date);",
                new { code, date });
            var purchase = test.Database.NextId(connection);

            test.Database.Execute(connection, @"
INSERT INTO purchase_items(purchase_id, product_id, batch_id, qty_entered, unit, qty_base,
                           cost_paisa, retail_paisa, line_total_paisa, batch_no)
VALUES(@purchase, @product, @batch, 1, 'base', 1, 20000, 30000, 20000, 'B-1');",
                new { purchase, product, batch });

            return purchase;
        });

    private static long Provisional(
        TestDatabase test,
        long sale,
        long saleItem,
        int product,
        long batch,
        int daysAgo) =>
        test.Database.Tx(connection =>
        {
            var date = Clock.DateDaysAgo(daysAgo) + " 10:00:00";
            test.Database.Execute(connection, @"
INSERT INTO provisional_returns(code, date, refund_paisa, status, linked_sale_id, linked_at, created_at)
VALUES(@code, @date, 30000, 'linked', @sale, @date, @date);",
                new { code = "PR-OLD-" + sale.ToString(CultureInfo.InvariantCulture), date, sale });
            var provisional = test.Database.NextId(connection);

            test.Database.Execute(connection, @"
INSERT INTO provisional_items(provisional_id, product_id, batch_id, qty_base, created_at,
                              qty_entered, unit_entered, refund_paisa, cost_paisa_at_return)
VALUES(@provisional, @product, @batch, 1, @date, 1, 'base', 30000, 20000);",
                new { provisional, product, batch, date });
            var provisionalItem = test.Database.NextId(connection);

            test.Database.Execute(connection, @"
INSERT INTO provisional_return_allocations(provisional_item_id, sale_item_id, qty_base,
                                           expected_refund_paisa, refund_paisa, created_at)
VALUES(@provisionalItem, @saleItem, 1, 30000, 30000, @date);",
                new { provisionalItem, saleItem, date });

            return provisional;
        });

    private static long Sale(TestDatabase test, string code, int daysAgo) =>
        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO sales(code, date, subtotal_paisa, total_paisa, paid_paisa, status, payment_method, created_at)
VALUES(@code, @date, 100, 100, 100, 'paid', 'cash', @date);",
                new { code, date = Clock.DateDaysAgo(daysAgo) + " 10:00:00" });

            return test.Database.NextId(connection);
        });

    private static long Count(TestDatabase test, string table) =>
        Convert.ToInt64(Scalar(test, "SELECT COUNT(1) FROM " + table + ";"));

    private static object? Scalar(TestDatabase test, string sql)
    {
        using var connection = test.Database.Open();
        return test.Database.Scalar(connection, sql);
    }

    private static string? NameOf(TestDatabase test, long id)
    {
        using var connection = test.Database.Open();

        return test.Database.Scalar(connection, "SELECT name FROM products WHERE id = @id;", new { id })
            as string;
    }

    private static long Audits(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action }) ?? 0L);
    }

    private static string AuditDetails(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return test.Database.Scalar(connection,
            "SELECT details FROM audit_logs WHERE action = @action ORDER BY id DESC LIMIT 1;",
            new { action }) as string ?? string.Empty;
    }

    /// <summary>Null when the shop has no product row 99 - proof that a preview wrote nothing.</summary>
    private static string? PreviewRow(TestDatabase test) => NameOf(test, 99);

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-tests", "sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
