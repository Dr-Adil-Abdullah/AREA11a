using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Real schema/service regression sources. C#/provider/WPF execution still required.</summary>
public sealed class ProvisionalPostingFaultTests
{
    private sealed class PrFaultFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public ProvisionalReturnService P { get; }
        public int First { get; }
        public int Second { get; }
        public SaleResult? Sale { get; }
        public ProvisionalResult? Pr { get; private set; }
        public PrFaultFixture(string route, bool noBatch = false)
        {
            Assert.True(TestData.Shifts(T).Open(TestData.Owner, 100000, "Synthetic PR drawer").Ok);
            First = TestData.AddProduct(T, costPaisa: 1000, retailPaisa: 1500, batchQty: 10, name: "Synthetic PR A");
            Second = TestData.AddProduct(T, costPaisa: 1000, retailPaisa: 1500, batchQty: 10, name: "Synthetic PR B");
            P = TestData.Provisionals(T);
            if (noBatch) Execute("DELETE FROM batches;");
            if (route != "create")
            {
                var cart = TestData.Cart(First, qty: 3);
                cart.Lines.Add(new SaleLineInput { ProductId = Second, Unit = "base", Quantity = 3 });
                var sale = TestData.Sales(T).Create(cart, TestData.Owner); Assert.True(sale.Ok, sale.Error); Sale = sale.Value!;
                Assert.True(Post("create").Ok);
            }
        }
        public ProvisionalInput Input(long refund = 3001) => new()
        {
            CustomerName = "Synthetic PR", RefundPaisa = refund, Reason = "changed mind",
            Lines = new List<ProvisionalLineInput>
            {
                new() { ProductId = First, QtyEntered = 1, Unit = "base" },
                new() { ProductId = Second, QtyEntered = 1, Unit = "base" }
            }
        };
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql) ?? 0));
        public string Snapshot(params string[] only) => T.Database.Read(c => string.Join("\n", (only.Length > 0 ? only : new[]
        {
            "settings", "customers", "suppliers", "products", "batches", "provisional_returns", "provisional_items", "provisional_return_allocations",
            "sales", "sale_items", "sale_returns", "payments", "stock_movements", "audit_logs", "sqlite_sequence"
        }).Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public ServiceResult Post(string route, User? actor = null)
        {
            actor ??= TestData.Owner;
            if (route == "create")
            {
                var created = P.Create(Input(), actor); if (created.Ok) Pr = created.Value!; return created;
            }
            return route == "link" ? P.Link(Pr!.Id, Sale!.Code, actor) : P.Cancel(Pr!.Id, "Synthetic duplicate", actor);
        }
        public void Reconcile(string route, bool noBatch = false, bool legacy = false)
        {
            Assert.Equal("PR-0001", Pr!.Code);
            Assert.Equal(2, Number("SELECT CAST(value AS INTEGER) FROM settings WHERE key='bill.nextProvisionalNo';"));
            Assert.Equal(1, Number("SELECT COUNT(*) FROM provisional_returns;"));
            Assert.Equal(2, Number("SELECT COUNT(*) FROM provisional_items;"));
            Assert.Equal(3001, Number("SELECT SUM(refund_paisa) FROM provisional_items;"));
            var refunds = T.Database.Read(c => T.Database.Query<long>(c, "SELECT refund_paisa FROM provisional_items ORDER BY id;"));
            Assert.Equal(new long[] { 1501, 1500 }, refunds);
            Assert.Equal(route == "create" ? noBatch ? 2 : 22 : route == "link" ? 16 : 14, Number("SELECT SUM(qty_base) FROM batches;"));
            Assert.Equal(1, Number("SELECT COUNT(*) FROM payments WHERE provisional_return_id IS NOT NULL;"));
            Assert.Equal(-3001, Number("SELECT SUM(amount_paisa) FROM payments WHERE provisional_return_id IS NOT NULL;"));
            var action = route == "create" ? "provisional_create" : route == "link" ? legacy ? "provisional_reconcile" : "provisional_link" : "provisional_cancel";
            Assert.Equal(1, T.Database.Read(c => T.Database.QuerySingle<long>(c, "SELECT COUNT(*) FROM audit_logs WHERE action=@action;", new { action })));
            Assert.Equal(route == "cancel" ? 4 : 2, Number("SELECT COUNT(*) FROM stock_movements WHERE ref_type='provisional_return';"));
            Assert.Equal(route == "link" ? 2 : 0, Number("SELECT COUNT(*) FROM provisional_return_allocations;"));
            if (route == "link")
            {
                Assert.Equal(3000, Number("SELECT SUM(expected_refund_paisa) FROM provisional_return_allocations;"));
                Assert.Equal(3001, Number("SELECT SUM(refund_paisa) FROM provisional_return_allocations;"));
            }
            var shift = TestData.Shifts(T).Page(TestData.Owner); Assert.True(shift.Ok, shift.Error);
            Assert.Equal(3001, shift.Value!.Current!.CashRefundsPaisa);
            Assert.Equal(100000 + (Sale?.PaidPaisa ?? 0) - 3001, shift.Value.Current.ExpectedPaisa);
            if (route != "create")
            {
                var before = Snapshot(); var repeated = Post(route); Assert.False(repeated.Ok); Assert.True(repeated.NoCommitConfirmed); Assert.Equal(before, Snapshot());
            }
        }
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData("create", "header", false)]
    [InlineData("create", "header", true)]
    [InlineData("create", "batch", false)]
    [InlineData("create", "batch", true)]
    [InlineData("create", "movement", false)]
    [InlineData("create", "movement", true)]
    [InlineData("create", "item", false)]
    [InlineData("create", "item", true)]
    [InlineData("create", "refund", false)]
    [InlineData("create", "refund", true)]
    [InlineData("create", "payment", false)]
    [InlineData("create", "payment", true)]
    [InlineData("create", "audit", false)]
    [InlineData("create", "audit", true)]
    [InlineData("create", "newbatch", false)]
    [InlineData("create", "newbatch", true)]
    [InlineData("create", "second", false)]
    [InlineData("create", "second", true)]
    [InlineData("link", "allocation", false)]
    [InlineData("link", "allocation", true)]
    [InlineData("link", "header", false)]
    [InlineData("link", "header", true)]
    [InlineData("link", "audit", false)]
    [InlineData("link", "audit", true)]
    [InlineData("link", "second", false)]
    [InlineData("link", "second", true)]
    [InlineData("cancel", "batch", false)]
    [InlineData("cancel", "batch", true)]
    [InlineData("cancel", "movement", false)]
    [InlineData("cancel", "movement", true)]
    [InlineData("cancel", "header", false)]
    [InlineData("cancel", "header", true)]
    [InlineData("cancel", "audit", false)]
    [InlineData("cancel", "audit", true)]
    [InlineData("cancel", "second", false)]
    [InlineData("cancel", "second", true)]
    public void StatementFaultRestoresSnapshotThenOneRetryReconciles(string route, string stage, bool after)
    {
        var noBatch = stage == "newbatch"; using var f = new PrFaultFixture(route, noBatch);
        var action = "provisional_" + route;
        var verb = stage is "batch" or "refund" || (stage == "header" && route != "create") ? "UP" + "DATE" : "IN" + "SERT";
        var table = stage switch
        {
            "batch" or "newbatch" => "batches", "movement" => "stock_movements", "item" or "refund" => "provisional_items",
            "payment" => "payments", "allocation" => "provisional_return_allocations", "header" => "provisional_returns",
            "audit" => "audit_logs WHEN NEW.action='" + action + "'",
            "second" => route == "link" ? "provisional_return_allocations WHEN NEW.sale_item_id=(SELECT MAX(id) FROM sale_items)"
                : "stock_movements WHEN NEW.product_id=" + f.Second,
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };
        f.Execute("CREATE TRIGGER synthetic_pr " + (after ? "AFTER" : "BEFORE") + " " + verb + " ON " + table + " BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        var before = f.Snapshot(); var failed = f.Post(route); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Equal(before, f.Snapshot());
        f.Execute("DROP TRIGGER synthetic_pr;");
        var financial = f.Snapshot("sales", "sale_items", "payments");
        var stock = f.Snapshot("batches", "stock_movements");
        var saved = f.Post(route); Assert.True(saved.Ok, saved.Error); f.Reconcile(route, noBatch);
        if (route != "create") Assert.Equal(financial, f.Snapshot("sales", "sale_items", "payments"));
        if (route == "link") Assert.Equal(stock, f.Snapshot("batches", "stock_movements"));
    }

    [Theory]
    [InlineData("create", false)]
    [InlineData("link", false)]
    [InlineData("cancel", false)]
    [InlineData("create", true)]
    [InlineData("link", true)]
    [InlineData("cancel", true)]
    public void KnownRefusalsPermitCorrectionWithoutMutation(string route, bool denied)
    {
        using var f = new PrFaultFixture(route); ServiceResult failed;
        if (denied)
        {
            var cashier = TestData.Cashier(f.T);
            Assert.True(TestData.Permissions(f.T).Set(TestData.Owner, cashier.Id, Modules.Returns, false).Ok);
            var before = f.Snapshot(); failed = f.Post(route, cashier); Assert.Equal(before, f.Snapshot());
        }
        else
        {
            var before = f.Snapshot();
            failed = route == "create" ? f.P.Create(new ProvisionalInput(), TestData.Owner)
                : route == "link" ? f.P.Link(f.Pr!.Id, "MISSING-SYNTHETIC", TestData.Owner)
                : f.P.Cancel(f.Pr!.Id, "", TestData.Owner);
            Assert.Equal(before, f.Snapshot());
        }
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed);
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); guard.ObserveFailure(failed); Assert.True(guard.TryBegin());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyUnallocatedLinkKeepsItsSameBillAndNoNewMoney(bool wrongBillFirst)
    {
        using var f = new PrFaultFixture("link");
        f.Execute($"UPDATE provisional_returns SET status='linked',linked_sale_id={f.Sale!.SaleId},linked_code='{f.Sale.Code}',linked_expected_paisa=NULL;");
        if (wrongBillFirst)
        {
            var other = TestData.Sales(f.T).Create(TestData.Cart(f.First), TestData.Owner); Assert.True(other.Ok, other.Error);
            var before = f.Snapshot(); var denied = f.P.Link(f.Pr!.Id, other.Value!.Code, TestData.Owner);
            Assert.False(denied.Ok); Assert.True(denied.NoCommitConfirmed); Assert.Equal(before, f.Snapshot());
        }
        var financial = f.Snapshot("sales", "sale_items", "payments", "batches", "stock_movements");
        var saved = f.Post("link"); Assert.True(saved.Ok, saved.Error);
        Assert.Equal(financial, f.Snapshot("sales", "sale_items", "payments", "batches", "stock_movements"));
        Assert.Equal(2, f.Number("SELECT COUNT(*) FROM provisional_return_allocations;"));
        Assert.Equal(1, f.Number("SELECT COUNT(*) FROM audit_logs WHERE action='provisional_reconcile';"));
    }

    [Fact]
    public void ZeroRefundStillAddsGoodsButNoCashPaymentOrDrawerOutflow()
    {
        using var f = new PrFaultFixture("create"); var saved = f.P.Create(f.Input(0), TestData.Owner); Assert.True(saved.Ok, saved.Error);
        Assert.NotEmpty(saved.Value!.Warnings); Assert.Equal(22, f.Number("SELECT SUM(qty_base) FROM batches;"));
        Assert.Equal(0, f.Number("SELECT COUNT(*) FROM payments;")); Assert.Equal(0, f.Number("SELECT SUM(refund_paisa) FROM provisional_items;"));
        var shift = TestData.Shifts(f.T).Page(TestData.Owner); Assert.True(shift.Ok, shift.Error); Assert.Equal(100000, shift.Value!.Current!.ExpectedPaisa);
    }
}
