using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Real-schema C# regression source; compilation/execution still required on Windows.</summary>
public sealed class StockPostingIntegrityTests
{
    private sealed class StockPostingFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public int Supplier { get; }
        public int Product { get; }
        public SupplierLedgerService Ledger { get; }
        public StockPostingFixture()
        {
            Supplier = TestData.AddSupplier(T, name: "Synthetic stock-post supplier");
            Product = TestData.AddProduct(T, costPaisa: 1000, retailPaisa: 1500, batchQty: 0);
            Ledger = new SupplierLedgerService(T.Database, T.Audit, new NumberingService(T.Settings), T.Settings,
                new PermissionService(T.Database, T.Log, T.Audit), T.Log);
            Assert.True(TestData.Shifts(T).Open(TestData.Owner, 500000, "Synthetic stock-post drawer").Ok);
        }
        public PurchaseInput Delivery(string method = "cash", bool sample = false) => new()
        {
            SupplierId = Supplier, PaidPaisa = 4000, PaidMethod = method,
            Lines = new List<PurchaseLineInput>
            {
                new() { ProductId = Product, Unit = "base", Quantity = 20, CostPaisa = 1000, RetailPaisa = 1600, BatchNo = "SYN-A", IsSample = sample },
                new() { ProductId = Product, Unit = "base", Quantity = 20, CostPaisa = 1000, RetailPaisa = 1700, BatchNo = "SYN-B" }
            }
        };
        public SupplierReturnInput GoodsBack(bool cash) => new()
        {
            SupplierId = Supplier, CashReceived = cash,
            Lines = T.Database.Read(c => T.Database.Query<int>(c, "SELECT id FROM batches WHERE purchase_id IS NOT NULL ORDER BY id;"))
                .Select(id => new SupplierReturnLineInput { ProductId = Product, BatchId = id, Unit = "base", QtyEntered = 5 }).ToList()
        };
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql)));
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "settings", "suppliers", "products", "batches", "purchases", "purchase_items", "stock_movements",
            "payments", "supplier_returns", "supplier_return_items", "audit_logs", "custom_values", "sqlite_sequence"
        }.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public void Reconcile(long payable, long stock, long outCash = 4000, long inCash = 0)
        {
            var account = Ledger.Page(TestData.Owner, Supplier); Assert.True(account.Ok, account.Error);
            Assert.True(account.Value!.BooksMatch); Assert.Equal(payable, account.Value.StoredPaisa);
            Assert.Equal(stock, Number("SELECT SUM(qty_base) FROM batches;"));
            var page = TestData.Shifts(T).Page(TestData.Owner); Assert.True(page.Ok, page.Error);
            var drawer = page.Value!.Current!; Assert.NotNull(drawer);
            Assert.Equal(outCash, drawer.SupplierCashOutPaisa); Assert.Equal(inCash, drawer.SupplierCashInPaisa);
            Assert.Equal(500000 - outCash + inCash, drawer.ExpectedPaisa);
        }
        public void Fault(bool returns, string stage, bool after)
        {
            var target = stage switch
            {
                "number" => "BEFORE UPDATE OF value ON settings",
                "header" => returns ? "BEFORE INSERT ON supplier_returns" : "BEFORE INSERT ON purchases",
                "batch" => returns ? "BEFORE UPDATE OF qty_base ON batches" : "BEFORE INSERT ON batches",
                "item" => returns ? "BEFORE INSERT ON supplier_return_items" : "BEFORE INSERT ON purchase_items",
                "movement" => "BEFORE INSERT ON stock_movements",
                "retail" => "BEFORE UPDATE OF retail_paisa ON products",
                "balance" => "BEFORE UPDATE OF balance_paisa ON suppliers",
                "payment" => "BEFORE INSERT ON payments",
                "audit" => "BEFORE INSERT ON audit_logs",
                _ => throw new ArgumentOutOfRangeException(nameof(stage))
            };
            var when = stage == "number" ? " WHEN NEW.key='bill." + (returns ? "nextSupplierReturnNo" : "nextPurchaseNo") + "'"
                : stage == "audit" ? " WHEN NEW.action='" + (returns ? "supplier_return" : "purchase_create") + "'" : string.Empty;
            Execute("CREATE TRIGGER stock_post_fault " + (after ? target.Replace("BEFORE ", "AFTER ", StringComparison.Ordinal) : target) + when
                + " BEGIN SELECT RAISE(ABORT,'private-stock-post-fault'); END;");
        }
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData("number", false)] [InlineData("number", true)]
    [InlineData("header", false)] [InlineData("header", true)]
    [InlineData("batch", false)] [InlineData("batch", true)]
    [InlineData("item", false)] [InlineData("item", true)]
    [InlineData("movement", false)] [InlineData("movement", true)]
    [InlineData("retail", false)] [InlineData("retail", true)]
    [InlineData("balance", false)] [InlineData("balance", true)]
    [InlineData("payment", false)] [InlineData("payment", true)]
    [InlineData("audit", false)] [InlineData("audit", true)]
    public void DeliveryStageFaultRestoresAllRowsAndNumberThenRetryReconciles(string stage, bool after)
    {
        using var f = new StockPostingFixture(); var before = f.Snapshot(); f.Fault(false, stage, after);
        var failed = TestData.Purchases(f.T).Create(f.Delivery(), TestData.Owner);
        Assert.False(failed.Ok); Assert.DoesNotContain("private-stock-post-fault", failed.Error!);
        Assert.Equal(before, f.Snapshot()); f.Reconcile(0, 0, 0);
        f.Execute("DROP TRIGGER stock_post_fault;");
        var retry = TestData.Purchases(f.T).Create(f.Delivery(), TestData.Owner); Assert.True(retry.Ok, retry.Error);
        Assert.Equal("PINV-0001", retry.Value!.Code); f.Reconcile(36000, 40);
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM purchases;"));
        Assert.Equal(2L, f.Number("SELECT COUNT(*) FROM purchase_items;"));
        Assert.Equal(2L, f.Number("SELECT COUNT(*) FROM stock_movements WHERE type='purchase';"));
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM payments;"));
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM audit_logs WHERE action='purchase_create';"));
    }

    [Theory]
    [InlineData("number", false, false)] [InlineData("number", true, false)]
    [InlineData("header", false, false)] [InlineData("header", true, false)]
    [InlineData("batch", false, false)] [InlineData("batch", true, false)]
    [InlineData("item", false, false)] [InlineData("item", true, false)]
    [InlineData("movement", false, false)] [InlineData("movement", true, false)]
    [InlineData("audit", false, false)] [InlineData("audit", true, false)]
    [InlineData("number", false, true)] [InlineData("number", true, true)]
    [InlineData("header", false, true)] [InlineData("header", true, true)]
    [InlineData("batch", false, true)] [InlineData("batch", true, true)]
    [InlineData("item", false, true)] [InlineData("item", true, true)]
    [InlineData("movement", false, true)] [InlineData("movement", true, true)]
    [InlineData("audit", false, true)] [InlineData("audit", true, true)]
    [InlineData("balance", false, false)] [InlineData("balance", true, false)]
    public void GoodsBackStageFaultPreservesCommittedDeliveryThenRetryReconciles(string stage, bool after, bool cash)
    {
        using var f = new StockPostingFixture(); Assert.True(TestData.Purchases(f.T).Create(f.Delivery(), TestData.Owner).Ok);
        var before = f.Snapshot(); f.Fault(true, stage, after);
        var failed = f.Ledger.Return(TestData.Owner, f.GoodsBack(cash)); Assert.False(failed.Ok);
        Assert.DoesNotContain("private-stock-post-fault", failed.Error!); Assert.Equal(before, f.Snapshot());
        f.Reconcile(36000, 40); f.Execute("DROP TRIGGER stock_post_fault;");
        var retry = f.Ledger.Return(TestData.Owner, f.GoodsBack(cash)); Assert.True(retry.Ok, retry.Error);
        Assert.Equal("SR-0001", retry.Value!.Code); f.Reconcile(cash ? 36000 : 26000, 30, 4000, cash ? 10000 : 0);
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM supplier_returns;"));
        Assert.Equal(2L, f.Number("SELECT COUNT(*) FROM supplier_return_items;"));
        Assert.Equal(2L, f.Number("SELECT COUNT(*) FROM stock_movements WHERE type='supplier_return';"));
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM payments;")); // Cash return is NOT a payment row.
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM audit_logs WHERE action='supplier_return';"));
    }

    [Theory]
    [InlineData("cash")] [InlineData("card")] [InlineData("online")]
    public void DeliveryPaymentMethodAndCashGoodsBackMatchSupplierBooksAndDrawer(string method)
    {
        using var f = new StockPostingFixture(); var delivery = TestData.Purchases(f.T).Create(f.Delivery(method), TestData.Owner);
        Assert.True(delivery.Ok, delivery.Error); f.Reconcile(36000, 40, method == "cash" ? 4000 : 0);
        var returned = f.Ledger.Return(TestData.Owner, f.GoodsBack(true)); Assert.True(returned.Ok, returned.Error);
        f.Reconcile(36000, 30, method == "cash" ? 4000 : 0, 10000);
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM payments;"));
    }

    [Fact]
    public void SampleDeliveryStillAddsStockAtZeroCostWithoutInventingPayable()
    {
        using var f = new StockPostingFixture(); var result = TestData.Purchases(f.T).Create(f.Delivery(sample: true), TestData.Owner);
        Assert.True(result.Ok, result.Error); Assert.Equal(20000L, result.Value!.TotalPaisa); f.Reconcile(16000, 40);
        Assert.Equal(0L, f.Number("SELECT SUM(cost_paisa) FROM batches WHERE is_sample=1;"));
        Assert.Equal(0L, f.Number("SELECT SUM(line_total_paisa) FROM purchase_items WHERE batch_no='SYN-A';"));
    }

    [Fact]
    public void InvalidSecondDeliveryLineRollsBackAlreadyInsertedFirstLine()
    {
        using var f = new StockPostingFixture(); var before = f.Snapshot(); var input = f.Delivery(); input.Lines[1].Quantity = 0;
        Assert.False(TestData.Purchases(f.T).Create(input, TestData.Owner).Ok); Assert.Equal(before, f.Snapshot());
        Assert.True(TestData.Purchases(f.T).Create(f.Delivery(), TestData.Owner).Ok); f.Reconcile(36000, 40);
    }

    [Fact]
    public void SecondGoodsBackItemFaultRollsBackFirstBatchDecreaseAndKeepsOriginalDelivery()
    {
        using var f = new StockPostingFixture(); Assert.True(TestData.Purchases(f.T).Create(f.Delivery(), TestData.Owner).Ok);
        var input = f.GoodsBack(false); var before = f.Snapshot();
        f.Execute("CREATE TRIGGER stock_post_fault AFTER INSERT ON supplier_return_items WHEN NEW.batch_id=" + input.Lines[1].BatchId
            + " BEGIN SELECT RAISE(ABORT,'private-stock-post-fault'); END;");
        Assert.False(f.Ledger.Return(TestData.Owner, input).Ok); Assert.Equal(before, f.Snapshot()); f.Reconcile(36000, 40);
        f.Execute("DROP TRIGGER stock_post_fault;"); Assert.True(f.Ledger.Return(TestData.Owner, input).Ok); f.Reconcile(26000, 30);
    }
}
