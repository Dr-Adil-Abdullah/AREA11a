using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated real-schema source regressions. Not executed in the Linux agent workspace.</summary>
public sealed class CounterPostingIntegrityTests
{
    private sealed class CounterPostingFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public int Customer { get; }
        public int First { get; }
        public int Second { get; }
        public CounterPostingFixture()
        {
            Customer = TestData.AddCustomer(T, name: "Synthetic counter posting");
            First = TestData.AddProduct(T, costPaisa: 1000, retailPaisa: 2000, name: "Synthetic A");
            Second = TestData.AddProduct(T, costPaisa: 1000, retailPaisa: 2000, name: "Synthetic B");
            Execute("UPDATE customers SET loyalty_points=40;");
            Assert.True(T.Settings.Set("bill.roundMode", "none", TestData.Owner).Ok);
            Assert.True(T.Settings.Set("loyalty.enabled", true, TestData.Owner).Ok);
            Assert.True(T.Settings.Set("loyalty.rupeesPerPoint", 100, TestData.Owner).Ok);
            Assert.True(TestData.Shifts(T).Open(TestData.Owner, 500000, "Synthetic counter drawer").Ok);
        }
        public SaleInput Bill(string method = "split") => new()
        {
            CustomerId = Customer, PaymentMethod = method,
            Payments = method == "split" ? new List<SalePaymentInput> { new() { Method = "credit", AmountPaisa = 30000 } } : new(),
            Lines = new List<SaleLineInput>
            {
                new() { ProductId = First, Unit = "base", Quantity = 10 },
                new() { ProductId = Second, Unit = "base", Quantity = 10 }
            }
        };
        public ReturnInput Back(SaleResult sale, bool restock = true) => new()
        {
            SaleId = sale.SaleId, Restock = restock,
            Lines = sale.SaleItemIds.Select(id => new ReturnLineInput { SaleItemId = id, QtyBase = 10 }).ToList()
        };
        public SaleResult Post(string method = "split")
        {
            var result = TestData.Sales(T).Create(Bill(method), TestData.Owner); Assert.True(result.Ok, result.Error); return result.Value!;
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql)));
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "settings", "customers", "products", "batches", "sales", "sale_items", "sale_returns",
            "stock_movements", "payments", "audit_logs", "custom_values", "sqlite_sequence"
        }.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public void Reconcile(long balance, long stock, long points, long cashSales = 10000, long cashRefund = 0)
        {
            var page = TestData.Ledger(T).Page(TestData.Owner, Customer); Assert.True(page.Ok, page.Error);
            Assert.True(page.Value!.BooksMatch, page.Value.BooksLabel); Assert.Equal(balance, page.Value.StoredPaisa);
            Assert.Equal(stock, Number("SELECT SUM(qty_base) FROM batches;"));
            Assert.Equal(points, Number("SELECT loyalty_points FROM customers;"));
            var shift = TestData.Shifts(T).Page(TestData.Owner); Assert.True(shift.Ok, shift.Error);
            var money = shift.Value!.Current!; Assert.NotNull(money);
            Assert.Equal(cashSales, money.CashSalesPaisa); Assert.Equal(cashRefund, money.CashRefundsPaisa);
            Assert.Equal(500000 + cashSales - cashRefund, money.ExpectedPaisa);
        }
        public void Fault(bool returns, string stage, bool after)
        {
            var target = stage switch
            {
                "number" or "qnumber" => "BEFORE UPDATE OF value ON settings",
                "header" => returns ? "BEFORE INSERT ON sale_returns" : "BEFORE INSERT ON sales",
                "item" => "BEFORE INSERT ON sale_items",
                "batch" => "BEFORE UPDATE OF qty_base ON batches",
                "movement" => "BEFORE INSERT ON stock_movements",
                "payment" => "BEFORE INSERT ON payments",
                "balance" => "BEFORE UPDATE OF balance_paisa ON customers",
                "points" => "BEFORE UPDATE OF loyalty_points ON customers",
                "audit" => "BEFORE INSERT ON audit_logs",
                _ => throw new ArgumentOutOfRangeException(nameof(stage))
            };
            var when = stage == "number" ? " WHEN NEW.key='bill." + (returns ? "nextReturnNo" : "nextSaleNo") + "'"
                : stage == "qnumber" ? " WHEN NEW.key='bill.nextQuarantineNo'"
                : stage == "audit" ? " WHEN NEW.action='" + (returns ? "return_create" : "sale_create") + "'" : string.Empty;
            Execute("CREATE TRIGGER counter_post_fault " + (after ? target.Replace("BEFORE ", "AFTER ", StringComparison.Ordinal) : target)
                + when + " BEGIN SELECT RAISE(ABORT,'private-counter-post-fault'); END;");
        }
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData("number", false)] [InlineData("number", true)]
    [InlineData("header", false)] [InlineData("header", true)]
    [InlineData("item", false)] [InlineData("item", true)]
    [InlineData("batch", false)] [InlineData("batch", true)]
    [InlineData("movement", false)] [InlineData("movement", true)]
    [InlineData("payment", false)] [InlineData("payment", true)]
    [InlineData("balance", false)] [InlineData("balance", true)]
    [InlineData("points", false)] [InlineData("points", true)]
    [InlineData("audit", false)] [InlineData("audit", true)]
    public void SaleFaultRestoresStockMoneyPointsAuditAndNumberThenRetryReconciles(string stage, bool after)
    {
        using var f = new CounterPostingFixture(); var before = f.Snapshot(); f.Fault(false, stage, after);
        var failed = TestData.Sales(f.T).Create(f.Bill(), TestData.Owner); Assert.False(failed.Ok);
        Assert.DoesNotContain("private-counter-post-fault", failed.Error!); Assert.Equal(before, f.Snapshot()); f.Reconcile(0, 200, 40, 0);
        f.Execute("DROP TRIGGER counter_post_fault;"); var sale = f.Post(); Assert.Equal("INV-0001", sale.Code);
        f.Reconcile(30000, 180, 44); Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM sales;"));
        Assert.Equal(2L, f.Number("SELECT COUNT(*) FROM sale_items;")); Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM payments;"));
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM audit_logs WHERE action='sale_create';"));
    }

    [Theory]
    [InlineData("number", false, true)] [InlineData("number", true, true)]
    [InlineData("header", false, true)] [InlineData("header", true, true)]
    [InlineData("batch", false, true)] [InlineData("batch", true, true)]
    [InlineData("movement", false, true)] [InlineData("movement", true, true)]
    [InlineData("balance", false, true)] [InlineData("balance", true, true)]
    [InlineData("points", false, true)] [InlineData("points", true, true)]
    [InlineData("audit", false, true)] [InlineData("audit", true, true)]
    [InlineData("number", false, false)] [InlineData("number", true, false)]
    [InlineData("qnumber", false, false)] [InlineData("qnumber", true, false)]
    [InlineData("header", false, false)] [InlineData("header", true, false)]
    [InlineData("balance", false, false)] [InlineData("balance", true, false)]
    [InlineData("points", false, false)] [InlineData("points", true, false)]
    [InlineData("audit", false, false)] [InlineData("audit", true, false)]
    public void CustomerReturnFaultPreservesOriginalBillThenRetryReconciles(string stage, bool after, bool restock)
    {
        using var f = new CounterPostingFixture(); var sale = f.Post(); var before = f.Snapshot(); f.Fault(true, stage, after);
        var failed = TestData.Returns(f.T).Create(f.Back(sale, restock), TestData.Owner); Assert.False(failed.Ok);
        Assert.DoesNotContain("private-counter-post-fault", failed.Error!); Assert.Equal(before, f.Snapshot()); f.Reconcile(30000, 180, 44);
        f.Execute("DROP TRIGGER counter_post_fault;"); var result = TestData.Returns(f.T).Create(f.Back(sale, restock), TestData.Owner);
        Assert.True(result.Ok, result.Error); Assert.Equal("RET-0001", result.Value!.Code);
        Assert.Equal(30000L, result.Value.CreditReducedPaisa); Assert.Equal(10000L, result.Value.CashPaisa);
        Assert.Equal(restock ? 0 : 2, result.Value.QuarantineCodes.Count); f.Reconcile(0, restock ? 200 : 180, 40, 10000, 10000);
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM sales;")); Assert.Equal(2L, f.Number("SELECT COUNT(*) FROM sale_returns;"));
        Assert.Equal(1L, f.Number("SELECT COUNT(*) FROM payments;")); // Refund belongs to sale_returns, not another payment.
        var committed = f.Snapshot(); Assert.False(TestData.Returns(f.T).Create(f.Back(sale, restock), TestData.Owner).Ok);
        Assert.Equal(committed, f.Snapshot()); // Quantity/bill cap, not global request identity.
    }

    [Theory]
    [InlineData("cash")] [InlineData("card")] [InlineData("online")] [InlineData("credit")] [InlineData("split")]
    public void FullReturnUsesOriginalBillCashCreditAndDrawerAcrossMethods(string method)
    {
        using var f = new CounterPostingFixture(); var sale = f.Post(method);
        var cash = method == "cash" ? 40000L : method == "split" ? 10000L : 0L;
        var due = method == "credit" ? 40000L : method == "split" ? 30000L : 0L;
        f.Reconcile(due, 180, 44, cash);
        var returned = TestData.Returns(f.T).Create(f.Back(sale), TestData.Owner); Assert.True(returned.Ok, returned.Error);
        f.Reconcile(0, 200, 40, cash, 40000 - due); // Card/online refunds retain existing cash-refund policy.
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClosedOrUnreadableDrawerCannotPostAnyRows(bool unreadable)
    {
        using var f = new CounterPostingFixture();
        f.Execute(unreadable ? "ALTER TABLE shifts RENAME TO hidden_shifts;" : "UPDATE shifts SET status='closed';");
        var before = f.Snapshot(); Assert.False(TestData.Sales(f.T).Create(f.Bill(), TestData.Owner).Ok); Assert.Equal(before, f.Snapshot());
        if (unreadable) f.Execute("ALTER TABLE hidden_shifts RENAME TO shifts;");
        else f.Execute("UPDATE shifts SET status='open';");
        f.Post(); f.Reconcile(30000, 180, 44);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SecondItemAbortRestoresFirstItemAndKeepsEarlierCommittedBill(bool returns)
    {
        using var f = new CounterPostingFixture(); var sale = returns ? f.Post() : null; var before = f.Snapshot();
        f.Execute("CREATE TRIGGER counter_post_fault AFTER INSERT ON " + (returns ? "sale_returns" : "sale_items")
            + " WHEN NEW.product_id=" + f.Second + " BEGIN SELECT RAISE(ABORT,'private-counter-post-fault'); END;");
        if (returns) Assert.False(TestData.Returns(f.T).Create(f.Back(sale!), TestData.Owner).Ok);
        else Assert.False(TestData.Sales(f.T).Create(f.Bill(), TestData.Owner).Ok);
        Assert.Equal(before, f.Snapshot()); f.Execute("DROP TRIGGER counter_post_fault;");
        if (returns) { Assert.True(TestData.Returns(f.T).Create(f.Back(sale!), TestData.Owner).Ok); f.Reconcile(0, 200, 40, 10000, 10000); }
        else { f.Post(); f.Reconcile(30000, 180, 44); }
    }

    [Fact]
    public void RedeemedPointsAndNegativeLoyaltyPaymentRollBackWithLateAuditFault()
    {
        using var f = new CounterPostingFixture(); var input = f.Bill("cash"); input.RedeemPoints = 5; var before = f.Snapshot();
        f.Fault(false, "audit", true); Assert.False(TestData.Sales(f.T).Create(input, TestData.Owner).Ok); Assert.Equal(before, f.Snapshot());
        f.Execute("DROP TRIGGER counter_post_fault;"); var saved = TestData.Sales(f.T).Create(input, TestData.Owner); Assert.True(saved.Ok, saved.Error);
        Assert.Equal(-500L, f.Number("SELECT SUM(amount_paisa) FROM payments WHERE method='loyalty';"));
        Assert.Equal(38L, f.Number("SELECT loyalty_points FROM customers;")); // 40-5+floor(395/100).
    }

    [Fact]
    public void ExtraReturnBStillRefunds560Then540AfterLateFaultAndRetry()
    {
        using var f = new CounterPostingFixture(); Assert.True(f.T.Settings.Set("loyalty.enabled", false, TestData.Owner).Ok);
        f.Execute("UPDATE products SET cost_paisa=40000,retail_paisa=56000;");
        f.Execute("UPDATE batches SET cost_paisa=40000,retail_paisa=56000;");
        var policy = new PricingAllowanceService(f.T.Database, f.T.Audit, f.T.Log); var allowance = policy.ForUser(TestData.Owner);
        allowance.ExtraDiscount = true; allowance.ExtraLimitPaisa = 5000; Assert.True(policy.Save(TestData.Owner, allowance).Ok);
        var input = f.Bill("cash"); foreach (var line in input.Lines) line.Quantity = 1; input.ExtraDiscountPaisa = 2000;
        var posted = TestData.Sales(f.T).Create(input, TestData.Owner); Assert.True(posted.Ok, posted.Error); var sale = posted.Value!;
        ReturnInput BackOne(int index) => new() { SaleId = sale.SaleId, Restock = true,
            Lines = new() { new ReturnLineInput { SaleItemId = sale.SaleItemIds[index], QtyBase = 1 } } };
        var first = TestData.Returns(f.T).Create(BackOne(0), TestData.Owner); Assert.True(first.Ok, first.Error); Assert.Equal(56000L, first.Value!.RefundPaisa);
        var before = f.Snapshot(); f.Fault(true, "audit", true); Assert.False(TestData.Returns(f.T).Create(BackOne(1), TestData.Owner).Ok);
        Assert.Equal(before, f.Snapshot()); f.Execute("DROP TRIGGER counter_post_fault;");
        var last = TestData.Returns(f.T).Create(BackOne(1), TestData.Owner); Assert.True(last.Ok, last.Error); Assert.Equal(54000L, last.Value!.RefundPaisa);
        Assert.False(TestData.Returns(f.T).Create(BackOne(1), TestData.Owner).Ok); Assert.Equal(110000L, f.Number("SELECT SUM(cash_refund_paisa) FROM sale_returns;"));
    }
}
