using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Additive real-schema shared-draft tests. Written,not executed here;never a live shop.
/// Local edit baselines are not a new durable retry identity or a whole-draft transaction.</summary>
public sealed class DraftEditConcurrencyTests
{
    private sealed class DraftEditFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public DraftService Drafts { get; }
        public bool Purchase { get; }
        public User Actor { get; private set; } = TestData.Owner;
        public SaleInput Sale { get; }
        public PurchaseInput Delivery { get; }
        public int ProductId { get; }
        public int Id { get; }
        public EditVersion Initial { get; }
        public string Type => Purchase ? DraftType.Purchase : DraftType.Sale;
        public DraftEditFixture(bool purchase)
        {
            Purchase = purchase; ProductId = TestData.AddProduct(T);
            Sale = TestData.Cart(ProductId, qty: 1);
            Delivery = new PurchaseInput { SupplierId = TestData.AddSupplier(T), Lines = new List<PurchaseLineInput>
            {
                new() { ProductId = ProductId, Unit = "base", Quantity = 1, CostPaisa = 20000,
                    RetailPaisa = 30000, BatchNo = "SYN-CAS", Expiry = "2099-06" }
            }};
            Drafts = TestData.Drafts(T); // Opens the synthetic drawer before a host enables Sessions.
            var saved = Save(); Assert.True(saved.Ok, saved.Error);
            Id = saved.Value!.Id; Initial = Assert.IsType<EditVersion>(saved.Value.EditVersion);
        }
        public void Enable()
        {
            T.Database.Sessions.Enable();
            var login = new AuthService(T.Database, T.Audit, T.Settings, T.Log).Login("admin", "area11");
            Assert.True(login.Ok, login.Error); Actor = login.Value!;
        }
        public ServiceResult<DocumentDraft> Save(int id = 0, EditVersion? expected = null) => Purchase
            ? Drafts.SavePurchaseDraft(Actor, Delivery, id, expected) : Drafts.SaveSaleDraft(Actor, Sale, id, expected);
        public ServiceResult Open() => Purchase ? Drafts.OpenPurchase(Actor, Id) : Drafts.OpenSale(Actor, Id);
        public ServiceResult Post(EditVersion? expected = null, bool stored = false) => Purchase
            ? Drafts.PostPurchase(Actor, Id, stored ? null : Delivery, expected)
            : Drafts.PostSale(Actor, Id, stored ? null : Sale, expected);
        public void Quantity(double qty) { Sale.Lines[0].Quantity = qty; Delivery.Lines[0].Quantity = qty; }
        public void Product(int id) { Sale.Lines[0].ProductId = id; Delivery.Lines[0].ProductId = id; }
        public void Execute(string sql) => T.Database.Tx(c => T.Database.Execute(c, sql));
        public string Text(string column) => T.Database.Read(c => Convert.ToString(T.Database.Scalar(c,
            $"SELECT {column} FROM document_drafts WHERE id=@id;", new { id = Id })) ?? "");
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "document_drafts", "audit_logs", "settings", "sqlite_sequence", "sales", "sale_items", "purchases",
            "purchase_items", "payments", "stock_movements", "batches", "customers", "suppliers"
        }.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public void Dispose() => T.Dispose();
    }

    private static void Refused(ServiceResult result)
    { Assert.False(result.Ok); Assert.Contains("changed", result.Error!); Assert.Null(result.EditVersion); }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoOpenFormsCannotSaveOverAnotherPersonsWork(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); var second = f.Open(); Assert.True(second.Ok, second.Error);
        f.Quantity(2); Assert.True(f.Save(f.Id, f.Initial).Ok);
        var before = f.Snapshot(); f.Quantity(3);
        var result = f.Save(f.Id, second.EditVersion); Refused(result); Assert.True(result.NoCommitConfirmed);
        Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaleEditedPostCannotRewritePayloadOrStartTheCreator(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); f.Quantity(2); Assert.True(f.Save(f.Id, f.Initial).Ok);
        var before = f.Snapshot(); f.Quantity(3);
        var result = f.Post(f.Initial); Refused(result); Assert.True(result.NoCommitConfirmed);
        Assert.Equal(before, f.Snapshot()); Assert.Equal("draft", f.Text("status"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrawerConfirmationIsBoundToTheDisplayedDraftNotAFreshReplacement(bool purchase)
    {
        using var f = new DraftEditFixture(purchase);
        var listed = f.Drafts.List(f.Actor, f.Type); Assert.True(listed.Ok, listed.Error);
        var summary = Assert.Single(listed.Value!); f.Quantity(2); Assert.True(f.Save(f.Id, f.Initial).Ok);
        var before = f.Snapshot(); Refused(f.Post(summary.EditVersion, stored: true)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiscardCannotConsumeADraftChangedDuringTheConfirmation(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); var summary = Assert.Single(f.Drafts.List(f.Actor, f.Type).Value!);
        f.Quantity(2); Assert.True(f.Save(f.Id, f.Initial).Ok); var before = f.Snapshot();
        Refused(f.Drafts.Discard(f.Actor, f.Id, expected: summary.EditVersion)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveReturnsTheExactNewBaselineForTheNextLinkedSaveAndPost(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); f.Quantity(2);
        var saved = f.Save(f.Id, f.Initial); Assert.True(saved.Ok, saved.Error); Assert.NotNull(saved.Value!.EditVersion);
        Assert.Equal(f.Text("payload_json"), saved.Value.PayloadJson);
        Assert.Equal(f.Text("updated_at"), saved.Value.UpdatedAt);
        f.Quantity(3); var next = f.Save(f.Id, saved.Value.EditVersion); Assert.True(next.Ok, next.Error);
        var posted = f.Post(next.Value!.EditVersion); Assert.True(posted.Ok, posted.Error);
        Assert.Equal("posted", f.Text("status")); Assert.NotEqual("-1", f.Text("posted_document_id"));
        Assert.False(f.Post(next.Value.EditVersion).Ok);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASecondDatabaseCommitIsDetectedWithoutRelyingOnUpdatedAt(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); var timestamp = f.Text("updated_at");
        var other = new Database(f.T.Database.FilePath, pooling: false, durable: false);
        other.Tx(c => other.Execute(c, "UPDATE document_drafts SET payload_json=@payload WHERE id=@id;",
            new { payload = "{\"Lines\":[]}", id = f.Id }));
        Assert.Equal(timestamp, f.Text("updated_at")); var before = f.Snapshot();
        Refused(f.Save(f.Id, f.Initial)); Refused(f.Post(f.Initial)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TokenForAnotherDraftCannotAuthorizeThisDraft(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); var other = f.Save(); Assert.True(other.Ok, other.Error);
        var before = f.Snapshot(); Refused(f.Save(f.Id, other.Value!.EditVersion));
        Refused(f.Post(other.Value.EditVersion)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TokenFromAnotherDatabaseInstanceCannotAuthorizeTheLocalEditor(bool purchase)
    {
        using var f = new DraftEditFixture(purchase);
        var db = new Database(f.T.Database.FilePath, pooling: false, durable: false);
        var audit = new AuditService(db, f.T.Log); var permissions = new PermissionService(db, f.T.Log, audit);
        var settings = new SettingsService(db, audit, f.T.Log, permissions); settings.Reload();
        var sales = new SaleService(db, audit, new NumberingService(settings), settings, permissions, f.T.Log);
        var purchases = new PurchaseService(db, audit, new NumberingService(settings), settings, permissions, f.T.Log);
        var drafts = new DraftService(db, audit, sales, purchases, permissions, f.T.Log);
        ServiceResult opened = purchase ? drafts.OpenPurchase(f.Actor, f.Id) : drafts.OpenSale(f.Actor, f.Id);
        Assert.True(opened.Ok, opened.Error); var before = f.Snapshot();
        Refused(f.Save(f.Id, opened.EditVersion)); Refused(f.Post(opened.EditVersion)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AProductEditTokenIsNotADraftToken(bool purchase)
    {
        using var f = new DraftEditFixture(purchase);
        var product = new ProductService(f.T.Database, f.T.Audit, f.T.Log).Get(f.Actor, f.ProductId);
        Assert.True(product.Ok, product.Error); Assert.NotNull(product.Value!.EditVersion);
        var before = f.Snapshot(); Refused(f.Save(f.Id, product.Value.EditVersion));
        Refused(f.Post(product.Value.EditVersion)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnabledDesktopHostRequiresLoadedBaselineForEditedWriters(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); f.Enable(); var before = f.Snapshot();
        Refused(f.Save(f.Id)); Refused(f.Post()); Refused(f.Drafts.Discard(f.Actor, f.Id));
        Assert.Equal(before, f.Snapshot());
        var opened = f.Open(); Assert.True(opened.Ok, opened.Error);
        Assert.True(f.Save(f.Id, opened.EditVersion).Ok);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmedFailedCreatorReturnsExactRestorationTokenForCorrection(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); f.Enable(); f.Product(int.MaxValue);
        var failed = f.Post(f.Initial); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed);
        Assert.Contains("restored with the latest work", failed.Error!); Assert.NotNull(failed.EditVersion);
        Assert.Equal("draft", f.Text("status")); f.Product(f.ProductId);
        var posted = f.Post(failed.EditVersion); Assert.True(posted.Ok, posted.Error);
        Assert.Equal("posted", f.Text("status"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestorationHandoffDoesNotSilentlyAcceptALaterWritersChange(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); f.Product(int.MaxValue); var failed = f.Post(f.Initial);
        Assert.NotNull(failed.EditVersion); f.Product(f.ProductId); var opened = f.Open();
        f.Quantity(2); Assert.True(f.Save(f.Id, opened.EditVersion).Ok); var before = f.Snapshot();
        Refused(f.Post(failed.EditVersion)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedPostSummaryCannotDiscardAMarkerThatChanged(bool purchase)
    {
        using var f = new DraftEditFixture(purchase);
        f.Execute($"UPDATE document_drafts SET status='posted',posted_document_id=-1,posted_at='2026-11-10 09:00:00' WHERE id={f.Id};");
        var marker = Assert.Single(f.Drafts.List(f.Actor, f.Type).Value!); Assert.True(marker.NeedsPostCheck);
        f.Execute($"UPDATE document_drafts SET discard_reason='changed after display' WHERE id={f.Id};");
        var before = f.Snapshot(); Refused(f.Drafts.Discard(f.Actor, f.Id, expected: marker.EditVersion));
        Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidBaselineDoesNotReplaceCurrentActorAuthorization(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); f.Execute("UPDATE users SET active=0 WHERE id=1;");
        var before = f.Snapshot(); Assert.False(f.Save(f.Id, f.Initial).Ok); Assert.False(f.Post(f.Initial).Ok);
        Assert.False(f.Drafts.Discard(f.Actor, f.Id, expected: f.Initial).Ok); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedRestorationCannotHandBackAFreshRetryToken(bool purchase)
    {
        using var f = new DraftEditFixture(purchase);
        f.Execute("CREATE TRIGGER synthetic_restore BEFORE UPDATE ON document_drafts WHEN NEW.status='draft' BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        f.Product(int.MaxValue); var failed = f.Post(f.Initial);
        Assert.False(failed.Ok); Assert.False(failed.NoCommitConfirmed); Assert.Null(failed.EditVersion);
        Assert.Contains("restoration was not confirmed", failed.Error!); Assert.Equal("posted", f.Text("status"));
        Assert.Equal("-1", f.Text("posted_document_id"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedDraftStillAllowsAnotherAuthorizedPersonWithAFreshBaseline(bool purchase)
    {
        using var f = new DraftEditFixture(purchase); var colleague = TestData.AddUser(f.T, "owner");
        ServiceResult opened = purchase ? f.Drafts.OpenPurchase(colleague, f.Id) : f.Drafts.OpenSale(colleague, f.Id);
        Assert.True(opened.Ok, opened.Error); f.Quantity(2);
        var saved = purchase ? f.Drafts.SavePurchaseDraft(colleague, f.Delivery, f.Id, opened.EditVersion)
            : f.Drafts.SaveSaleDraft(colleague, f.Sale, f.Id, opened.EditVersion);
        Assert.True(saved.Ok, saved.Error); Assert.Equal(TestData.Owner.Id.ToString(), f.Text("created_by_id"));
        var before = f.Snapshot(); Refused(f.Save(f.Id, f.Initial)); Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void PurchaseSummaryCanStillBeDiscardedWithBaselineButWithoutCostPermission()
    {
        using var f = new DraftEditFixture(true); f.Enable();
        Assert.True(TestData.Permissions(f.T).Set(f.Actor, f.Actor.Id, Modules.PurchaseCost, false).Ok);
        var listed = f.Drafts.List(f.Actor, DraftType.Purchase); Assert.True(listed.Ok, listed.Error);
        var row = Assert.Single(listed.Value!); Assert.NotNull(row.EditVersion);
        Assert.False(f.Open().Ok);
        var discarded = f.Drafts.Discard(f.Actor, f.Id, expected: row.EditVersion);
        Assert.True(discarded.Ok, discarded.Error); Assert.Equal("discarded", f.Text("status"));
    }

    [Fact]
    public void ResumedHeldCartCarriesItsExactRecoveryDraftBaseline()
    {
        using var f = new DraftEditFixture(false); var holds = TestData.Holds(f.T);
        Assert.True(holds.Hold(f.Sale, f.Actor).Ok); var held = Assert.Single(holds.List(f.Actor).Value!);
        var resumed = holds.Resume(f.Actor, held.Id); Assert.True(resumed.Ok, resumed.Error);
        var cart = resumed.Value!; Assert.NotNull(cart.EditVersion);
        f.Quantity(2); var saved = f.Drafts.SaveSaleDraft(f.Actor, f.Sale, cart.RecoveryDraftId, cart.EditVersion);
        Assert.True(saved.Ok, saved.Error);
        var before = f.Snapshot(); Refused(f.Drafts.PostSale(f.Actor, cart.RecoveryDraftId, f.Sale, cart.EditVersion));
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void DraftAndSummaryBaselinesAreNotSerializedWithPayloadOrExports()
    {
        using var f = new DraftEditFixture(false); var saved = f.Save(); Assert.True(saved.Ok, saved.Error);
        var summary = Assert.Single(f.Drafts.List(f.Actor, f.Type).Value!.Where(x => x.Id == saved.Value!.Id));
        var opened = f.Drafts.OpenSale(f.Actor, f.Id);
        foreach (var text in new[] { JsonSerializer.Serialize(saved.Value), JsonSerializer.Serialize(summary), JsonSerializer.Serialize(opened),
                     JsonSerializer.Serialize(new HeldCart()), f.Text("payload_json") })
        {
            Assert.DoesNotContain("EditVersion", text); Assert.DoesNotContain("Fingerprint", text);
        }
    }

    [Fact]
    public void ReviewedFreshStoredPostWorksInEnabledHostWithoutAnEditedPayload()
    {
        using var f = new DraftEditFixture(false); f.Enable();
        var posted = f.Post(stored: true); Assert.True(posted.Ok, posted.Error);
        Assert.Equal("posted", f.Text("status"));
    }
}
