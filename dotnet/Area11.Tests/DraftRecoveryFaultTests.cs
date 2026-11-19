using System.IO;
using System.Reflection;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Area11.Tests;

/// <summary>Real schema + service regression sources. Reflection injects only the private creator seam;
/// no production fault switch. This is not a power-cut/provider COMMIT or WPF reconstruction test.</summary>
public sealed class DraftRecoveryFaultTests
{
    private sealed class RecoveryFaultFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public DraftService D { get; }
        public HoldCartService H { get; }
        public SaleInput Sale { get; }
        public PurchaseInput Purchase { get; }
        public int DraftId { get; }
        public int HeldId { get; }
        public RecoveryFaultFixture(bool purchase = false)
        {
            var product = TestData.AddProduct(T);
            Sale = TestData.Cart(product, qty: 1);
            Purchase = new PurchaseInput { SupplierId = TestData.AddSupplier(T), Lines = new List<PurchaseLineInput>
            {
                new() { ProductId = product, Unit = "base", Quantity = 1, CostPaisa = 20000, RetailPaisa = 30000, BatchNo = "SYN-REC", Expiry = "2099-06" }
            }};
            D = TestData.Drafts(T); H = TestData.Holds(T);
            var saved = purchase ? D.SavePurchaseDraft(TestData.Owner, Purchase) : D.SaveSaleDraft(TestData.Owner, Sale);
            Assert.True(saved.Ok, saved.Error); DraftId = saved.Value!.Id;
            Assert.True(H.Hold(Sale, TestData.Owner).Ok);
            HeldId = Assert.Single(H.List(TestData.Owner).Value!).Id;
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql) ?? 0L));
        public string Text(string sql) => T.Database.Read(c => Convert.ToString(T.Database.Scalar(c, sql)) ?? "");
        public string Tables(params string[] tables) => T.Database.Read(c => string.Join("\n", tables.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public string Money() => Tables("customers", "suppliers", "batches", "sales", "sale_items", "purchases", "purchase_items", "payments", "stock_movements");
        public string Snapshot() => Money() + Tables("document_drafts", "held_carts", "held_cart_items", "audit_logs", "settings", "sqlite_sequence");
        public void Dispose() => T.Dispose();
    }

    private static ServiceResult<TResult> Inject<TInput, TResult>(RecoveryFaultFixture f, string type, TInput input,
        Func<TInput, ServiceResult<TResult>> create, Func<SqliteConnection, int, ServiceResult>? finalize = null) where TResult : class
    {
        var method = typeof(DraftService).GetMethod("Post", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(typeof(TInput), typeof(TResult));
        finalize ??= (_, _) => ServiceResult.Success();
        return (ServiceResult<TResult>)method.Invoke(f.D, new object?[] { f.DraftId, type, input, create, finalize, null })!;
    }

    [Theory]
    [InlineData(false, "known")]
    [InlineData(true, "known")]
    [InlineData(false, "unknown")]
    [InlineData(true, "unknown")]
    [InlineData(false, "throw")]
    [InlineData(true, "throw")]
    [InlineData(false, "committed-throw")]
    [InlineData(true, "committed-throw")]
    [InlineData(false, "restore-noop")]
    [InlineData(true, "restore-noop")]
    [InlineData(false, "restore-fault")]
    [InlineData(true, "restore-fault")]
    public void CreatorOutcomeControlsRestoreNotErrorWording(bool purchase, string fault)
    {
        using var f = new RecoveryFaultFixture(purchase); var money = f.Money();
        f.Sale.Lines[0].Quantity = 2; f.Purchase.Lines[0].Quantity = 2;
        if (fault == "restore-fault") f.Execute("CREATE TRIGGER synthetic_restore BEFORE UPDATE ON document_drafts WHEN NEW.status='draft' BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        if (fault == "restore-noop") f.Execute("CREATE TRIGGER synthetic_restore BEFORE UPDATE ON document_drafts WHEN NEW.status='draft' BEGIN SELECT RAISE(IGNORE); END;");
        ServiceResult<T> Answer<T>(Func<ServiceResult<T>> commit) where T : class
        {
            if (fault == "committed-throw") { var saved = commit(); Assert.True(saved.Ok, saved.Error); throw new IOException("after document commit"); }
            if (fault == "throw") throw new IOException("creator unavailable");
            // Deliberately identical wording in both known/unknown cases: never parse this text.
            return new ServiceResult<T> { Error = "synthetic creator failure", NoCommitConfirmed = fault is "known" or "restore-fault" or "restore-noop" };
        }
        ServiceResult result = purchase
            ? Inject(f, DraftType.Purchase, f.Purchase, _ => Answer(() => TestData.Purchases(f.T).Create(f.Purchase, TestData.Owner)))
            : Inject(f, DraftType.Sale, f.Sale, _ => Answer(() => TestData.Sales(f.T).Create(f.Sale, TestData.Owner)));
        Assert.False(result.Ok);
        Assert.Equal(fault == "known" ? "draft" : "posted", f.Text($"SELECT status FROM document_drafts WHERE id={f.DraftId};"));
        if (fault == "known")
        {
            Assert.Contains("restored with the latest work", result.Error!);
            Assert.Equal(2d, purchase ? f.D.OpenPurchase(TestData.Owner, f.DraftId).Value!.Lines.Single().Quantity
                : f.D.OpenSale(TestData.Owner, f.DraftId).Value!.Lines.Single().Quantity);
            Assert.Equal(0, f.Number($"SELECT COUNT(*) FROM document_drafts WHERE id={f.DraftId} AND posted_document_id IS NOT NULL;"));
        }
        else
        {
            Assert.Equal(-1, f.Number($"SELECT posted_document_id FROM document_drafts WHERE id={f.DraftId};"));
            if (fault is "restore-fault" or "restore-noop") Assert.Contains("restoration was not confirmed", result.Error!);
            var before = f.Snapshot();
            Assert.False(purchase ? f.D.PostPurchase(TestData.Owner, f.DraftId).Ok : f.D.PostSale(TestData.Owner, f.DraftId).Ok);
            Assert.False(purchase ? f.D.SavePurchaseDraft(TestData.Owner, f.Purchase, f.DraftId).Ok : f.D.SaveSaleDraft(TestData.Owner, f.Sale, f.DraftId).Ok);
            Assert.False(f.H.Hold(f.Sale, TestData.Owner, f.DraftId).Ok);
            Assert.Equal(before, f.Snapshot());
        }
        if (fault == "committed-throw") Assert.Equal(1, f.Number(purchase ? "SELECT COUNT(*) FROM purchases;" : "SELECT COUNT(*) FROM sales;"));
        else Assert.Equal(money, f.Money());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalizeAuditFaultReturnsSavedWarningAndNeverRecreatesDocument(bool purchase)
    {
        using var f = new RecoveryFaultFixture(purchase);
        f.Execute("CREATE TRIGGER synthetic_finalize BEFORE INSERT ON audit_logs WHEN NEW.action='draft_posted' BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        ServiceResult result;
        if (purchase)
        {
            var r = f.D.PostPurchase(TestData.Owner, f.DraftId); result = r;
            Assert.True(r.Ok, r.Error); Assert.Contains(r.Value!.Warnings, x => x.Contains("finalization failed"));
        }
        else
        {
            var r = f.D.PostSale(TestData.Owner, f.DraftId); result = r;
            Assert.True(r.Ok, r.Error); Assert.Contains(r.Value!.Warnings, x => x.Contains("finalization failed"));
        }
        Assert.Contains(result.Warnings, x => x.Contains("Do not post again"));
        Assert.Equal(-1, f.Number($"SELECT posted_document_id FROM document_drafts WHERE id={f.DraftId};"));
        var before = f.Snapshot();
        Assert.False(purchase ? f.D.PostPurchase(TestData.Owner, f.DraftId).Ok : f.D.PostSale(TestData.Owner, f.DraftId).Ok);
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(1, f.Number(purchase ? "SELECT COUNT(*) FROM purchases;" : "SELECT COUNT(*) FROM sales;"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PremarkFaultDoesNotCallCreatorOrChangeWork(bool purchase)
    {
        using var f = new RecoveryFaultFixture(purchase); var before = f.Snapshot(); var calls = 0;
        f.Execute("CREATE TRIGGER synthetic_premark BEFORE UPDATE ON document_drafts WHEN NEW.posted_document_id=-1 BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        var result = Inject<SaleInput, SaleResult>(f, purchase ? DraftType.Purchase : DraftType.Sale, f.Sale, _ => { calls++; throw new IOException(); });
        Assert.False(result.Ok); Assert.Equal(0, calls); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData("insert")]
    [InlineData("consume")]
    [InlineData("draft-audit")]
    [InlineData("resume-audit")]
    public void ResumeFaultRollsBackDraftConsumeAndAuditThenOneRetryRecovers(string fault)
    {
        using var f = new RecoveryFaultFixture();
        var trigger = fault switch
        {
            "insert" => "BEFORE INSERT ON document_drafts",
            "consume" => "BEFORE UPDATE ON held_carts",
            "draft-audit" => "BEFORE INSERT ON audit_logs WHEN NEW.action='draft_saved'",
            _ => "BEFORE INSERT ON audit_logs WHEN NEW.action='cart_resume'"
        };
        f.Execute("CREATE TRIGGER synthetic_resume " + trigger + " BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        var before = f.Snapshot(); Assert.False(f.H.Resume(TestData.Owner, f.HeldId).Ok); Assert.Equal(before, f.Snapshot());
        f.Execute("DROP TRIGGER synthetic_resume;");
        var resumed = f.H.Resume(TestData.Owner, f.HeldId); Assert.True(resumed.Ok, resumed.Error);
        Assert.True(resumed.Value!.RecoveryDraftId > 0);
        before = f.Snapshot(); Assert.False(f.H.Resume(TestData.Owner, f.HeldId).Ok); Assert.Equal(before, f.Snapshot());
        Assert.Equal(1, f.Number("SELECT COUNT(*) FROM held_carts WHERE recovery_draft_id IS NOT NULL;"));
    }

    [Fact]
    public void ResumedCartRecoverySurvivesNewServicesKeepsPricingAndPostsOnlyOnce()
    {
        using var f = new RecoveryFaultFixture(); var money = f.Money();
        // Edit actual held snapshots to cover all preserved fields without altering pricing rules.
        f.Execute("UPDATE held_carts SET discount_percent=2,discount_basis='profit',extra_discount_paisa=100,note='recover this';");
        f.Execute("UPDATE held_cart_items SET unit_price_paisa=31000,pricing_mode='manual',discount_basis='retail',discount_percent=3;");
        var resumed = f.H.Resume(TestData.Owner, f.HeldId); Assert.True(resumed.Ok, resumed.Error);
        var id = resumed.Value!.RecoveryDraftId; Assert.Equal("recover this", resumed.Value.Notes);
        var drafts = TestData.Drafts(f.T); var opened = drafts.OpenSale(TestData.Owner, id); Assert.True(opened.Ok, opened.Error);
        var input = opened.Value!; Assert.Equal(2m, input.DiscountPercent); Assert.Equal("profit", input.DiscountBasis);
        Assert.Equal(100, input.ExtraDiscountPaisa); Assert.Equal("recover this", input.Notes);
        var line = Assert.Single(input.Lines); Assert.Equal(31000, line.UnitPricePaisa); Assert.Equal("manual", line.PricingMode);
        Assert.Equal("retail", line.DiscountBasis); Assert.Equal(3m, line.LineDiscountPercent);
        Assert.Equal(money, f.Money());
        Assert.False(TestData.Holds(f.T).Hold(input, TestData.Owner, id).Ok);
        // The recovery payload is editable only as the SAME draft; use the settled normal fixture to post.
        Assert.True(drafts.SaveSaleDraft(TestData.Owner, f.Sale, id).Ok);
        var posted = drafts.PostSale(TestData.Owner, id); Assert.True(posted.Ok, posted.Error);
        var before = f.Snapshot(); Assert.False(drafts.PostSale(TestData.Owner, id).Ok); Assert.Equal(before, f.Snapshot());
        Assert.Equal(1, f.Number("SELECT COUNT(*) FROM sales;"));
    }

    [Fact]
    public void HistoricalResumedRowsAreNotReplayedOrBackfilled()
    {
        using var f = new RecoveryFaultFixture(); f.Execute("UPDATE held_carts SET status='resumed';"); var before = f.Snapshot();
        Assert.False(f.H.Resume(TestData.Owner, f.HeldId).Ok); Assert.Equal(before, f.Snapshot());
        Assert.Equal(0, f.Number("SELECT COUNT(*) FROM held_carts WHERE recovery_draft_id IS NOT NULL;"));
    }

    [Fact]
    public void TransactionObservationRequiresPreCommitRollbackAndIsNotACommitReceipt()
    {
        using var f = new RecoveryFaultFixture(); var observation = new TransactionOutcome();
        Assert.Throws<IOException>(() => f.T.Database.Tx<int>(_ => throw new IOException("work"), observation));
        Assert.True(observation.WorkStarted); Assert.True(observation.RollbackConfirmed); Assert.False(observation.CommitAttempted); Assert.True(observation.NoCommitConfirmed);
        Assert.Equal(7, f.T.Database.Tx(_ => 7, observation));
        Assert.True(observation.CommitAttempted); Assert.False(observation.RollbackConfirmed); Assert.False(observation.NoCommitConfirmed);
        // Conservative classification of the uncertain provider boundary; NOT an injected real COMMIT failure.
        typeof(TransactionOutcome).GetProperty(nameof(TransactionOutcome.RollbackConfirmed))!.SetValue(observation, true);
        Assert.False(observation.NoCommitConfirmed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualValidationFailureRestoresThenValidRetryPostsOnce(bool purchase)
    {
        using var f = new RecoveryFaultFixture(purchase);
        var money = f.Money();
        if (purchase) f.Purchase.Lines[0].ProductId = int.MaxValue; else f.Sale.Lines[0].ProductId = int.MaxValue;
        var result = purchase ? f.D.PostPurchase(TestData.Owner, f.DraftId, f.Purchase).Ok : f.D.PostSale(TestData.Owner, f.DraftId, f.Sale).Ok;
        Assert.False(result); Assert.Equal("draft", f.Text($"SELECT status FROM document_drafts WHERE id={f.DraftId};"));
        Assert.Equal(money, f.Money());
        var product = (int)f.Number("SELECT id FROM products ORDER BY id LIMIT 1;");
        f.Purchase.Lines[0].ProductId = product; f.Sale.Lines[0].ProductId = product;
        Assert.True(purchase ? f.D.PostPurchase(TestData.Owner, f.DraftId, f.Purchase).Ok : f.D.PostSale(TestData.Owner, f.DraftId, f.Sale).Ok);
        Assert.Equal(1, f.Number(purchase ? "SELECT COUNT(*) FROM purchases;" : "SELECT COUNT(*) FROM sales;"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARejectedFinalizationResultIsSurfacedAsSavedNotRetryable(bool purchase)
    {
        using var f = new RecoveryFaultFixture(purchase);
        ServiceResult result = purchase
            ? Inject(f, DraftType.Purchase, f.Purchase, _ => TestData.Purchases(f.T).Create(f.Purchase, TestData.Owner), (_, _) => ServiceResult.Fail("synthetic finalizer"))
            : Inject(f, DraftType.Sale, f.Sale, _ => TestData.Sales(f.T).Create(f.Sale, TestData.Owner), (_, _) => ServiceResult.Fail("synthetic finalizer"));
        Assert.True(result.Ok, result.Error); Assert.NotEmpty(result.Warnings);
        Assert.Equal(-1, f.Number($"SELECT posted_document_id FROM document_drafts WHERE id={f.DraftId};"));
        Assert.Equal(1, f.Number(purchase ? "SELECT COUNT(*) FROM purchases;" : "SELECT COUNT(*) FROM sales;"));
    }

    [Fact]
    public void OversizedRecoveryPayloadLeavesCartParkedWithNoOrphanDraft()
    {
        using var f = new RecoveryFaultFixture();
        f.T.Database.Tx(c => { f.T.Database.Execute(c, "UPDATE held_carts SET note=@note WHERE id=@id;", new { note = new string('x', DraftService.MaxPayloadChars), id = f.HeldId }); });
        var before = f.Snapshot(); Assert.False(f.H.Resume(TestData.Owner, f.HeldId).Ok); Assert.Equal(before, f.Snapshot());
        Assert.Equal(1, f.H.Count(TestData.Owner).Value);
    }

    [Fact]
    public void NestedTransactionRefusalDoesNotStartCreatorWork()
    {
        using var f = new RecoveryFaultFixture(); var observation = new TransactionOutcome(); var called = false;
        f.T.Database.Tx(_ =>
        {
            Assert.Throws<InvalidOperationException>(() => f.T.Database.Tx(c => { called = true; return 1; }, observation));
        });
        Assert.False(called); Assert.False(observation.WorkStarted); Assert.True(observation.NoCommitConfirmed);
    }

}
