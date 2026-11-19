using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Real schema/service and pure local-state regression sources; C#/WPF/provider execution required.</summary>
public sealed class ExpenseStockPostingFaultTests
{
    private sealed class ExpenseStockFaultFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public ExpenseService E { get; }
        public StockService S { get; }
        public StockTakeService ST { get; }
        public int Product { get; }
        public int Session { get; }
        public int Found { get; }
        public ExpenseStockFaultFixture(bool found = false)
        {
            TestData.SeedRawSetting(T, MasterFoundationSettings.ProfitShowMargins, true);
            Product = TestData.AddProduct(T, costPaisa: 1000, retailPaisa: 1500, batchQty: 10);
            Assert.True(TestData.Shifts(T).Open(TestData.Owner, 100000, "Synthetic expense-stock drawer").Ok);
            var permissions = new PermissionService(T.Database, T.Log, T.Audit);
            E = TestData.CashOut(T); S = new StockService(T.Database, T.Audit, T.Settings, permissions, T.Log);
            ST = new StockTakeService(T.Database, T.Audit, new NumberingService(T.Settings), T.Settings, permissions, T.Log);
            var opened = ST.Open(TestData.Owner); Assert.True(opened.Ok, opened.Error); Session = opened.Value!.Id;
            if (found)
            {
                Found = TestData.AddProduct(T, costPaisa: 1000, retailPaisa: 1500, batchQty: 0, name: "Synthetic found");
                Execute($"DELETE FROM batches WHERE product_id={Found};");
                Assert.True(ST.AddFound(TestData.Owner, Session, Found).Ok);
                var line = ST.Sheet(TestData.Owner, Session).Single(x => x.ProductId == Found);
                Assert.True(ST.Count(TestData.Owner, line.Id, 2, "base", null).Ok);
            }
            else
            {
                var line = Assert.Single(ST.Sheet(TestData.Owner, Session));
                Assert.True(ST.Count(TestData.Owner, line.Id, 8, "base", null).Ok);
            }
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql) ?? 0));
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "settings", "customers", "suppliers", "products", "batches", "expenses", "owner_drawings", "stock_adjustments",
            "stock_takes", "stock_take_items", "stock_movements", "sales", "purchases", "payments", "custom_values", "audit_logs", "sqlite_sequence"
        }.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public ServiceResult Post(string route, bool invalid = false) => route switch
        {
            "expense" => E.AddExpense(TestData.Owner, new ExpenseInput { Category = "rent", Title = "Synthetic rent", AmountPaisa = invalid ? 0 : 3000 }),
            "cash" => E.AddDrawing(TestData.Owner, new DrawingInput { Type = "cash", AmountPaisa = invalid ? 0 : 4000 }),
            "goods" => E.AddDrawing(TestData.Owner, new DrawingInput { Type = "goods", ProductId = Product, Unit = "base", QtyEntered = invalid ? 0 : 2 }),
            "writeoff" => S.WriteOff(new WriteOffInput { ProductId = Product, Unit = "base", QtyEntered = invalid ? 0 : 2, Reason = StockService.ReasonExpired }, TestData.Owner),
            "take" or "found" => ST.Apply(TestData.Owner, invalid ? int.MaxValue : Session),
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };
        public void Reconcile(string route)
        {
            Assert.Equal(route == "found" ? 12 : route is "goods" or "writeoff" or "take" ? 8 : 10, Number("SELECT SUM(qty_base) FROM batches;"));
            Assert.Equal(route == "expense" ? 1 : 0, Number("SELECT COUNT(*) FROM expenses;"));
            Assert.Equal(route is "goods" or "cash" ? 1 : 0, Number("SELECT COUNT(*) FROM owner_drawings;"));
            Assert.Equal(route == "writeoff" ? 1 : 0, Number("SELECT COUNT(*) FROM stock_adjustments;"));
            Assert.Equal(route is "goods" or "writeoff" or "take" or "found" ? 1 : 0, Number("SELECT COUNT(*) FROM stock_movements;"));
            Assert.Equal(0, Number("SELECT COUNT(*) FROM purchases;")); Assert.Equal(0, Number("SELECT COUNT(*) FROM payments;"));
            var action = route switch { "expense" => "expense", "cash" => "owner_drawing", "goods" => "owner_drawing_goods", "writeoff" => "writeoff", _ => "stock_take_apply" };
            Assert.Equal(1, T.Database.Read(c => T.Database.QuerySingle<long>(c, "SELECT COUNT(*) FROM audit_logs WHERE action=@action;", new { action })));
            Assert.Equal(route == "expense" ? 3000 : 0, Number("SELECT COALESCE(SUM(amount_paisa),0) FROM expenses;"));
            Assert.Equal(route == "cash" ? 4000 : route == "goods" ? 2000 : 0, Number("SELECT COALESCE(SUM(amount_paisa),0) FROM owner_drawings;"));
            Assert.Equal(route == "writeoff" ? 2000 : 0, Number("SELECT COALESCE(SUM(value_paisa),0) FROM stock_adjustments;"));
            if (route is "take" or "found")
            {
                Assert.Equal(route == "take" ? 2000 : 0, Number("SELECT short_paisa FROM stock_takes;"));
                Assert.Equal(route == "found" ? 2000 : 0, Number("SELECT extra_paisa FROM stock_takes;"));
            }
            var shift = TestData.Shifts(T).Page(TestData.Owner); Assert.True(shift.Ok, shift.Error);
            Assert.Equal(route == "expense" ? 3000 : 0, shift.Value!.Current!.ExpensesPaisa);
            Assert.Equal(route == "cash" ? 4000 : 0, shift.Value.Current.DrawingsPaisa);
            Assert.Equal(route == "expense" ? 97000 : route == "cash" ? 96000 : 100000, shift.Value.Current.ExpectedPaisa);
            if (route is "take" or "found")
            {
                Assert.Equal(1, Number("SELECT COUNT(*) FROM stock_takes WHERE status='applied';"));
                var before = Snapshot(); var repeat = Post(route); Assert.False(repeat.Ok); Assert.True(repeat.NoCommitConfirmed); Assert.Equal(before, Snapshot());
            }
        }
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData("expense", "row", false)]
    [InlineData("expense", "row", true)]
    [InlineData("expense", "audit", false)]
    [InlineData("expense", "audit", true)]
    [InlineData("cash", "row", false)]
    [InlineData("cash", "row", true)]
    [InlineData("cash", "audit", false)]
    [InlineData("cash", "audit", true)]
    [InlineData("goods", "batch", false)]
    [InlineData("goods", "batch", true)]
    [InlineData("goods", "movement", false)]
    [InlineData("goods", "movement", true)]
    [InlineData("goods", "row", false)]
    [InlineData("goods", "row", true)]
    [InlineData("goods", "audit", false)]
    [InlineData("goods", "audit", true)]
    [InlineData("writeoff", "batch", false)]
    [InlineData("writeoff", "batch", true)]
    [InlineData("writeoff", "movement", false)]
    [InlineData("writeoff", "movement", true)]
    [InlineData("writeoff", "row", false)]
    [InlineData("writeoff", "row", true)]
    [InlineData("writeoff", "audit", false)]
    [InlineData("writeoff", "audit", true)]
    [InlineData("take", "batch", false)]
    [InlineData("take", "batch", true)]
    [InlineData("take", "movement", false)]
    [InlineData("take", "movement", true)]
    [InlineData("take", "session", false)]
    [InlineData("take", "session", true)]
    [InlineData("take", "audit", false)]
    [InlineData("take", "audit", true)]
    [InlineData("found", "newbatch", false)]
    [InlineData("found", "newbatch", true)]
    public void StatementFaultRestoresWholeSnapshotThenOneRetryReconciles(string route, string stage, bool after)
    {
        using var f = new ExpenseStockFaultFixture(route == "found");
        var action = route switch { "expense" => "expense", "cash" => "owner_drawing", "goods" => "owner_drawing_goods", "writeoff" => "writeoff", _ => "stock_take_apply" };
        var verb = stage is "batch" or "session" ? "UP" + "DATE" : "IN" + "SERT";
        var table = stage switch
        {
            "batch" or "newbatch" => "batches", "movement" => "stock_movements", "session" => "stock_takes",
            "audit" => "audit_logs WHEN NEW.action='" + action + "'",
            _ => route == "expense" ? "expenses" : route == "writeoff" ? "stock_adjustments" : "owner_drawings"
        };
        f.Execute("CREATE TRIGGER synthetic_post " + (after ? "AFTER" : "BEFORE") + " " + verb + " ON " + table + " BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        var before = f.Snapshot(); var failure = f.Post(route);
        Assert.False(failure.Ok); Assert.True(failure.NoCommitConfirmed); Assert.Equal(before, f.Snapshot());
        f.Execute("DROP TRIGGER synthetic_post;"); var retry = f.Post(route); Assert.True(retry.Ok, retry.Error); f.Reconcile(route);
    }

    [Theory]
    [InlineData("expense", "invalid")]
    [InlineData("expense", "inactive")]
    [InlineData("cash", "invalid")]
    [InlineData("cash", "inactive")]
    [InlineData("goods", "invalid")]
    [InlineData("goods", "inactive")]
    [InlineData("writeoff", "invalid")]
    [InlineData("writeoff", "inactive")]
    [InlineData("take", "invalid")]
    [InlineData("take", "inactive")]
    public void DefiniteRefusalIsSafeAndDoesNotMutate(string route, string fault)
    {
        using var f = new ExpenseStockFaultFixture();
        if (fault == "inactive") f.Execute("UPDATE users SET active=0 WHERE id=1;");
        var before = f.Snapshot(); var failed = f.Post(route, fault == "invalid");
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SecondStockTakeLineFaultRollsBackFirstThenApplyRemainsTerminal(bool after)
    {
        using var f = new ExpenseStockFaultFixture(); var second = TestData.AddProduct(f.T, costPaisa: 1000, retailPaisa: 1500, batchQty: 0, name: "Synthetic later line");
        f.Execute($"DELETE FROM batches WHERE product_id={second};");
        Assert.True(f.ST.AddFound(TestData.Owner, f.Session, second).Ok);
        var line = f.ST.Sheet(TestData.Owner, f.Session).Single(x => x.ProductId == second);
        Assert.True(f.ST.Count(TestData.Owner, line.Id, 2, "base", null).Ok);
        f.Execute("CREATE TRIGGER synthetic_second " + (after ? "AFTER" : "BEFORE") + " " + "IN" + $"SERT ON stock_movements WHEN NEW.product_id={second} BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        var before = f.Snapshot(); var failure = f.Post("take"); Assert.False(failure.Ok); Assert.True(failure.NoCommitConfirmed); Assert.Equal(before, f.Snapshot());
        f.Execute("DROP TRIGGER synthetic_second;"); Assert.True(f.Post("take").Ok);
        Assert.Equal(2, f.Number("SELECT COUNT(*) FROM stock_movements;"));
        before = f.Snapshot(); Assert.False(f.Post("take").Ok); Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void ApplyStillUsesFrozenCostAndLeavesUncountedStockAlone()
    {
        using var f = new ExpenseStockFaultFixture(true);
        f.Execute("UPDATE products SET cost_paisa=9000;");
        f.Execute("UPDATE batches SET cost_paisa=9000;");
        var saved = f.ST.Apply(TestData.Owner, f.Session); Assert.True(saved.Ok, saved.Error);
        Assert.Equal(2000, saved.Value!.ExtraPaisa); Assert.Equal(0, saved.Value.ShortPaisa);
        Assert.Equal(1, saved.Value.NewBatches); Assert.NotEmpty(saved.Value.Warnings); f.Reconcile("found");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitFailureOutcomeNotWordingControlsLocalRetry(bool known)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        guard.ObserveFailure(new ServiceResult { Error = "same wording", NoCommitConfirmed = known });
        Assert.Equal(known, guard.TryBegin());
        if (!known) Assert.Equal(PostingAttemptState.NeedsVerification, guard.State);
    }

    [Theory]
    [InlineData("inflight")]
    [InlineData("committed")]
    [InlineData("reset")]
    public void ExceptionDoesNotForgetAcknowledgedCommit(string stage)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        if (stage != "inflight") guard.AcknowledgeCommit();
        if (stage == "reset") guard.CompleteReset();
        guard.ObserveException(); Assert.Equal(stage != "inflight", guard.WasAcknowledged);
        Assert.Equal(stage == "reset", guard.Ready);
        if (stage == "inflight") Assert.Equal(PostingAttemptState.NeedsVerification, guard.State);
    }

    [Theory]
    [InlineData("inflight")]
    [InlineData("committed")]
    [InlineData("unknown")]
    public void UnconsumedOrUnconfirmedAttemptCannotBeginAgain(string stage)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin());
        if (stage == "committed") guard.AcknowledgeCommit();
        if (stage == "unknown") guard.ObserveFailure(ServiceResult.Fail("synthetic"));
        Assert.False(guard.TryBegin());
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("inflight")]
    [InlineData("unknown")]
    public void ResetCannotReleaseUnknownOrUnacknowledgedWork(string stage)
    {
        var guard = new PostingAttemptGuard();
        if (stage != "ready") Assert.True(guard.TryBegin());
        if (stage == "unknown") guard.ObserveFailure(ServiceResult.Fail("synthetic"));
        Assert.Throws<InvalidOperationException>(guard.CompleteReset);
    }

    [Fact]
    public void PrewriteValidationCanReleaseButSuccessfulCleanupMustAcknowledgeFirst()
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); guard.CancelBeforeWrite();
        Assert.True(guard.TryBegin()); Assert.Throws<InvalidOperationException>(() => guard.ObserveFailure(ServiceResult.Success()));
        guard.AcknowledgeCommit(); Assert.False(guard.TryBegin()); guard.CompleteReset();
        Assert.True(guard.WasAcknowledged); Assert.True(guard.TryBegin()); Assert.False(guard.WasAcknowledged);
    }
}
