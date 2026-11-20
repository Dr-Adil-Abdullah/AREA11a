using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Real schema/public services for the save-link-post sequence. Not WPF execution or
/// a provider-ambiguous COMMIT injection; UI routing/latches have separate source checks.</summary>
public sealed class AutomaticDraftPostingTests
{
    private sealed class AutoPostingFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public DraftService D { get; }
        public HoldCartService H { get; }
        public SaleInput Sale { get; }
        public PurchaseInput Purchase { get; }
        public AutoPostingFixture()
        {
            var product = TestData.AddProduct(T); Sale = TestData.Cart(product);
            Purchase = new PurchaseInput { SupplierId = TestData.AddSupplier(T), PaidPaisa = 10000, Lines = new List<PurchaseLineInput>
            {
                new() { ProductId = product, Unit = "base", Quantity = 1, CostPaisa = 20000, RetailPaisa = 30000, BatchNo = "SYN-AUTO", Expiry = "2099-06" }
            }};
            D = TestData.Drafts(T); H = TestData.Holds(T);
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql) ?? 0L));
        public string Tables(params string[] tables) => T.Database.Read(c => string.Join("\n", tables.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public string Money() => Tables("customers", "suppliers", "batches", "sales", "sale_items", "purchases", "purchase_items", "payments", "stock_movements");
        public string Snapshot() => Money() + Tables("document_drafts", "held_carts", "held_cart_items", "audit_logs", "settings", "sqlite_sequence");
        public ServiceResult<DocumentDraft> Save(bool purchase, int id = 0) => purchase
            ? D.SavePurchaseDraft(TestData.Owner, Purchase, id) : D.SaveSaleDraft(TestData.Owner, Sale, id);
        public bool Post(bool purchase, int id) => purchase ? D.PostPurchase(TestData.Owner, id, Purchase).Ok : D.PostSale(TestData.Owner, id, Sale).Ok;
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData(false, false, "before")]
    [InlineData(false, false, "after")]
    [InlineData(false, false, "audit-before")]
    [InlineData(false, false, "audit-after")]
    [InlineData(false, true, "before")]
    [InlineData(false, true, "after")]
    [InlineData(false, true, "audit-before")]
    [InlineData(false, true, "audit-after")]
    [InlineData(true, false, "before")]
    [InlineData(true, false, "after")]
    [InlineData(true, false, "audit-before")]
    [InlineData(true, false, "audit-after")]
    [InlineData(true, true, "before")]
    [InlineData(true, true, "after")]
    [InlineData(true, true, "audit-before")]
    [InlineData(true, true, "audit-after")]
    public void DraftSaveStatementFaultIsKnownNoCommitAndRetryUsesOneDraft(bool purchase, bool update, string stage)
    {
        using var f = new AutoPostingFixture(); var id = 0;
        if (update) { var saved = f.Save(purchase); Assert.True(saved.Ok, saved.Error); id = saved.Value!.Id; }
        f.Sale.Lines[0].Quantity = 2; f.Purchase.Lines[0].Quantity = 2;
        var timing = stage.EndsWith("before", StringComparison.Ordinal) ? "BEFORE" : "AFTER";
        var target = stage.StartsWith("audit", StringComparison.Ordinal)
            ? "IN" + "SERT ON audit_logs WHEN NEW.action='" + (update ? "draft_updated" : "draft_saved") + "'"
            : (update ? "UP" + "DATE" : "IN" + "SERT") + " ON document_drafts";
        f.Execute("CREATE TRIGGER synthetic_save " + timing + " " + target + " BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        var before = f.Snapshot(); var failed = f.Save(purchase, id);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Null(failed.Value); Assert.Equal(before, f.Snapshot());
        f.Execute("DROP TRIGGER synthetic_save;"); var retry = f.Save(purchase, id); Assert.True(retry.Ok, retry.Error);
        if (update) Assert.Equal(id, retry.Value!.Id);
        Assert.Equal(1, f.Number("SELECT COUNT(*) FROM document_drafts;"));
        Assert.True(f.Post(purchase, retry.Value!.Id));
        Assert.Equal(1, f.Number(purchase ? "SELECT COUNT(*) FROM purchases;" : "SELECT COUNT(*) FROM sales;"));
    }

    [Theory]
    [InlineData("before", "header")]
    [InlineData("after", "header")]
    [InlineData("before", "item")]
    [InlineData("after", "item")]
    [InlineData("before", "audit")]
    [InlineData("after", "audit")]
    public void HoldStatementFaultRollsBackNumberRowsAndAuditThenOneRetryParks(string timing, string stage)
    {
        using var f = new AutoPostingFixture();
        var target = stage switch { "header" => "held_carts", "item" => "held_cart_items", _ => "audit_logs WHEN NEW.action='cart_hold'" };
        f.Execute("CREATE TRIGGER synthetic_hold " + timing.ToUpperInvariant() + " " + "IN" + "SERT ON " + target + " BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        var before = f.Snapshot(); var failed = f.H.Hold(f.Sale, TestData.Owner);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Equal(before, f.Snapshot());
        f.Execute("DROP TRIGGER synthetic_hold;"); var retry = f.H.Hold(f.Sale, TestData.Owner); Assert.True(retry.Ok, retry.Error);
        Assert.Equal("H-0001", retry.Value); Assert.Equal(1, f.H.Count(TestData.Owner).Value);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("posted")]
    [InlineData("marker")]
    [InlineData("discarded")]
    [InlineData("missing")]
    [InlineData("purchase")]
    public void AnySuppliedDraftIntentCannotBecomeAnotherParkedCopy(string state)
    {
        using var f = new AutoPostingFixture(); var saved = f.Save(state == "purchase"); Assert.True(saved.Ok, saved.Error); var id = saved.Value!.Id;
        if (state == "posted") Assert.True(f.Post(false, id));
        if (state == "marker") f.Execute("UPDATE document_drafts SET status='posted',posted_document_id=-1,posted_at='2026-11-10 12:00:00';");
        if (state == "discarded") Assert.True(f.D.Discard(TestData.Owner, id).Ok);
        if (state == "missing") id = int.MaxValue;
        var before = f.Snapshot(); var held = f.H.Hold(f.Sale, TestData.Owner, id);
        Assert.False(held.Ok); Assert.True(held.NoCommitConfirmed); Assert.Contains("draft cannot be parked", held.Error!);
        Assert.Equal(before, f.Snapshot()); Assert.Equal(0, f.H.Count(TestData.Owner).Value);
    }

    [Theory]
    [InlineData(false, "cash")]
    [InlineData(false, "online")]
    [InlineData(false, "card")]
    [InlineData(true, "cash")]
    [InlineData(true, "online")]
    [InlineData(true, "card")]
    public void OrdinarySaveSequenceIsSharedRecoverableAndPostsTheSameDraftOnlyOnce(bool purchase, string method)
    {
        using var f = new AutoPostingFixture(); f.Sale.PaymentMethod = method; f.Purchase.PaidMethod = method;
        var money = f.Money(); var saved = f.Save(purchase); Assert.True(saved.Ok, saved.Error); var id = saved.Value!.Id;
        Assert.Equal(money, f.Money());
        var manager = TestData.AddUser(f.T, "manager");
        Assert.Contains(TestData.Drafts(f.T).List(manager, purchase ? DraftType.Purchase : DraftType.Sale).Value!, row => row.Id == id);
        var restarted = TestData.Drafts(f.T);
        Assert.True(purchase ? restarted.OpenPurchase(TestData.Owner, id).Ok : restarted.OpenSale(TestData.Owner, id).Ok);
        Assert.True(f.Post(purchase, id));
        Assert.Equal(1, f.Number("SELECT COUNT(*) FROM document_drafts;"));
        var before = f.Snapshot(); Assert.False(f.Post(purchase, id)); Assert.Equal(before, f.Snapshot());
        Assert.Equal(1, f.Number(purchase ? "SELECT COUNT(*) FROM purchases;" : "SELECT COUNT(*) FROM sales;"));
    }

    [Theory]
    [InlineData(false, "empty")]
    [InlineData(true, "empty")]
    [InlineData(false, "size")]
    [InlineData(true, "size")]
    [InlineData(false, "inactive")]
    [InlineData(true, "inactive")]
    public void PreflightFailureIsExplicitlySafeAndCreatesNoRows(bool purchase, string fault)
    {
        using var f = new AutoPostingFixture();
        if (fault == "empty") { f.Sale.Lines.Clear(); f.Purchase.Lines.Clear(); }
        if (fault == "size") { f.Sale.Notes = new string('x', DraftService.MaxPayloadChars); f.Purchase.Notes = f.Sale.Notes; }
        if (fault == "inactive") f.Execute("UPDATE users SET active=0 WHERE id=1;");
        var before = f.Snapshot(); var result = f.Save(purchase);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void SecondHeldLineFaultCannotKeepTheFirstLineOrConsumeNumber()
    {
        using var f = new AutoPostingFixture(); var second = TestData.AddProduct(f.T, name: "Synthetic second");
        f.Sale.Lines.Add(new SaleLineInput { ProductId = second, Unit = "base", Quantity = 1 });
        f.Execute("CREATE TRIGGER synthetic_second BEFORE INSERT ON held_cart_items WHEN NEW.position=1 BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        var before = f.Snapshot(); var failed = f.H.Hold(f.Sale, TestData.Owner);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Equal(before, f.Snapshot());
        f.Execute("DROP TRIGGER synthetic_second;"); var held = f.H.Hold(f.Sale, TestData.Owner); Assert.True(held.Ok, held.Error);
        Assert.Equal("H-0001", held.Value); Assert.Equal(2, f.Number("SELECT COUNT(*) FROM held_cart_items;"));
    }
}
