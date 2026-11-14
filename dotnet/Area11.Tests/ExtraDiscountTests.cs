using System.Text.Json;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Phase 3, owner-selected return policy B. Isolated database only; never shop data.</summary>
public sealed class ExtraDiscountTests
{
    private static PricingAllowanceService Policy(TestDatabase t) => new(t.Database, t.Audit, t.Log);
    private static void Grant(TestDatabase t, User? user = null, long cap = 5000, bool enabled = true)
    {
        var value = Policy(t).ForUser(user ?? TestData.Owner);
        value.ExtraDiscount = enabled;
        value.ExtraLimitPaisa = cap;
        Assert.True(Policy(t).Save(TestData.Owner, value).Ok);
    }
    private static SaleInput Cart(TestDatabase t, long extra = 2000)
    {
        Assert.True(t.Settings.Set("bill.roundMode", "none", TestData.Owner).Ok);
        var product = TestData.AddProduct(t, costPaisa: 40000, retailPaisa: 56000);
        var input = TestData.Cart(product, qty: 2);
        // Deliberately separate rows: price and prior discount of BOTH stay untouched.
        input.Lines[0].Quantity = 1;
        input.Lines.Add(new SaleLineInput { ProductId = product, Quantity = 1, Unit = "base" });
        input.ExtraDiscountPaisa = extra;
        return input;
    }
    private static SaleResult Post(TestDatabase t, SaleInput input)
    {
        var saved = TestData.Sales(t).Create(input, TestData.Owner);
        Assert.True(saved.Ok, saved.Error);
        return saved.Value!;
    }
    private static long Scalar(TestDatabase t, string sql)
    {
        using var c = t.Database.Open();
        return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static ReturnInput Back(SaleResult sale, int index = 0, double qty = 1) => new()
    {
        SaleId = sale.SaleId, Reason = ReturnService.ReasonChangedMind, Restock = true,
        Lines = new() { new ReturnLineInput { SaleItemId = sale.SaleItemIds[index], QtyBase = qty } }
    };

    [Fact]
    public void NoAutomaticExtraAllowanceEvenForOwner()
    {
        using var t = new TestDatabase();
        Assert.False(Policy(t).ForUser(TestData.Owner).ExtraDiscount);
        Assert.False(TestData.Sales(t).Create(Cart(t), TestData.Owner).Ok);
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM sales;"));
    }

    [Fact]
    public void OwnerConfiguresIndependentExtraAllowancePerPerson()
    {
        using var t = new TestDatabase();
        var first = TestData.Cashier(t); var second = TestData.Cashier(t);
        Grant(t, first, 3000);
        Assert.Equal(3000L, Policy(t).ForUser(first).ExtraLimitPaisa);
        Assert.False(Policy(t).ForUser(second).ExtraDiscount);
        Assert.True(TestData.Sales(t).Quote(Cart(t), first).Ok);
        Assert.False(TestData.Sales(t).Quote(Cart(t), second).Ok);
    }

    [Fact]
    public void ManagerCannotGrantExtraAllowance()
    {
        using var t = new TestDatabase();
        var manager = TestData.AddUser(t, "manager");
        Assert.False(Policy(t).Save(manager, new PricingAllowance
            { UserId = manager.Id, ExtraDiscount = true, ExtraLimitPaisa = 5000 }).Ok);
    }

    [Fact]
    public void NegativeExtraAllowanceCapIsRefused()
    {
        using var t = new TestDatabase();
        Assert.False(Policy(t).Save(TestData.Owner, new PricingAllowance
            { UserId = 1, ExtraDiscount = true, ExtraLimitPaisa = -1 }).Ok);
    }

    [Fact]
    public void DisabledAllowanceDoesNotUseItsStoredCap()
    {
        using var t = new TestDatabase(); Grant(t, enabled: false);
        Assert.False(TestData.Sales(t).Quote(Cart(t), TestData.Owner).Ok);
    }

    [Fact]
    public void OnePaisaOverCapIsRefusedNotClamped()
    {
        using var t = new TestDatabase(); Grant(t, cap: 2000);
        var input = Cart(t, 2001);
        Assert.False(TestData.Sales(t).Quote(input, TestData.Owner).Ok);
        Assert.False(TestData.Sales(t).Create(input, TestData.Owner).Ok);
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM sales;"));
    }

    [Fact]
    public void NegativeExtraCannotIncreaseOrRoundUpABill()
    {
        using var t = new TestDatabase(); Grant(t);
        Assert.False(TestData.Sales(t).Quote(Cart(t, -1), TestData.Owner).Ok);
    }

    [Fact]
    public void OwnerExample1120Minus20Is1100WithNoDue()
    {
        using var t = new TestDatabase(); Grant(t);
        var input = Cart(t); input.TenderedPaisa = 110000;
        var sale = Post(t, input);
        Assert.Equal(112000L, sale.PreExtraTotalPaisa);
        Assert.Equal(2000L, sale.ExtraDiscountPaisa);
        Assert.Equal(110000L, sale.TotalPaisa);
        Assert.Equal(110000L, sale.PaidPaisa);
        Assert.Equal(0L, sale.DuePaisa);
        Assert.Equal(0L, sale.ChangePaisa);
    }

    [Fact]
    public void OriginalExtraAndFinalAreStoredSeparately()
    {
        using var t = new TestDatabase(); Grant(t); Post(t, Cart(t));
        Assert.Equal(112000L, Scalar(t, "SELECT pre_extra_total_paisa FROM sales;"));
        Assert.Equal(2000L, Scalar(t, "SELECT extra_discount_paisa FROM sales;"));
        Assert.Equal(110000L, Scalar(t, "SELECT total_paisa FROM sales;"));
        Assert.Equal(0L, Scalar(t, "SELECT discount_paisa FROM sales;"));
    }

    [Fact]
    public void SaleItemRowsAreNotReducedByExtra()
    {
        using var t = new TestDatabase(); Grant(t); Post(t, Cart(t));
        Assert.Equal(112000L, Scalar(t, "SELECT SUM(line_total_paisa) FROM sale_items;"));
        Assert.Equal(112000L, Scalar(t, "SELECT SUM(unit_price_paisa) FROM sale_items;"));
        Assert.Equal(0L, Scalar(t, "SELECT SUM(discount_paisa) FROM sale_items;"));
        Assert.Equal(2L, Scalar(t, "SELECT SUM(qty_base) FROM sale_items;"));
    }

    [Fact]
    public void ManualFinalLinesStillAllowSeparateExtra()
    {
        using var t = new TestDatabase(); Grant(t); var input = Cart(t);
        foreach (var line in input.Lines) { line.PricingMode = "manual"; line.UnitPricePaisa = 55000; }
        var sale = Post(t, input);
        Assert.Equal(108000L, sale.TotalPaisa);
        Assert.Equal(110000L, Scalar(t, "SELECT SUM(line_total_paisa) FROM sale_items;"));
    }

    [Fact]
    public void ItemPercentAndBillPercentAreUnchangedByExtra()
    {
        using var t = new TestDatabase(); Grant(t); var input = Cart(t);
        input.DiscountPercent = 10;
        input.Lines[0].PricingMode = "discount";
        input.Lines[0].DiscountBasis = "retail";
        input.Lines[0].LineDiscountPercent = 5;
        var sales = TestData.Sales(t);
        input.ExtraDiscountPaisa = 0; var before = sales.Quote(input, TestData.Owner).Value!;
        input.ExtraDiscountPaisa = 2000; var after = sales.Quote(input, TestData.Owner).Value!;
        Assert.Equal(before.DiscountPaisa, after.DiscountPaisa);
        Assert.Equal(before.Lines.Select(x => x.NetPaisa), after.Lines.Select(x => x.NetPaisa));
        Assert.Equal(before.TotalPaisa - 2000, after.TotalPaisa);
        Assert.Equal(10m, input.DiscountPercent); Assert.Equal(5m, input.Lines[0].LineDiscountPercent);
    }

    [Fact]
    public void SeparateAllowanceWorksWithoutAnyOrdinaryPercentGrant()
    {
        using var t = new TestDatabase(); var cashier = TestData.Cashier(t); Grant(t, cashier);
        var saved = TestData.Sales(t).Create(Cart(t), cashier);
        Assert.True(saved.Ok, saved.Error); Assert.Equal(110000L, saved.Value!.TotalPaisa);
    }

    [Fact]
    public void RequotingAbsoluteAmountNeverAccumulatesOrResetsTheCap()
    {
        using var t = new TestDatabase(); Grant(t, cap: 2000); var input = Cart(t);
        var sales = TestData.Sales(t);
        Assert.Equal(110000L, sales.Quote(input, TestData.Owner).Value!.TotalPaisa);
        Assert.Equal(110000L, sales.Quote(input, TestData.Owner).Value!.TotalPaisa);
        input.ExtraDiscountPaisa = 2001; Assert.False(sales.Quote(input, TestData.Owner).Ok);
        input.ExtraDiscountPaisa = 0; Assert.Equal(112000L, sales.Quote(input, TestData.Owner).Value!.TotalPaisa);
    }

    [Fact]
    public void RevokedPermissionBetweenQuoteAndPostIsRechecked()
    {
        using var t = new TestDatabase(); Grant(t); var input = Cart(t); var sales = TestData.Sales(t);
        Assert.True(sales.Quote(input, TestData.Owner).Ok);
        Grant(t, enabled: false); Assert.False(sales.Create(input, TestData.Owner).Ok);
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM sales;"));
    }

    [Fact]
    public void ExtraCannotTakeFinalBillBelowActualCostEvenForOwner()
    {
        using var t = new TestDatabase(); Grant(t, cap: 100000); var input = Cart(t, 32001);
        Assert.False(TestData.Sales(t).Create(input, TestData.Owner).Ok);
        input.ExtraDiscountPaisa = 32000;
        Assert.Equal(80000L, Post(t, input).TotalPaisa);

        // A later FEFO batch is more expensive than the catalog cost; use its actual cost too.
        using var fefo = new TestDatabase(); Grant(fefo, cap: 100000); var mixed = Cart(fefo, 17001);
        fefo.Database.Tx(c =>
        {
            fefo.Database.Execute(c, "UPDATE batches SET qty_base=1 WHERE product_id=@id;",
                new { id = mixed.Lines[0].ProductId });
            fefo.Database.Execute(c, @"
INSERT INTO batches(product_id,batch_no,expiry_date,expiry_ym,qty_base,cost_paisa,retail_paisa,active)
VALUES(@id,'EXPENSIVE','2028-06-30','2028-06',5,55000,56000,1);",
                new { id = mixed.Lines[0].ProductId });
        });
        Assert.False(TestData.Sales(fefo).Create(mixed, TestData.Owner).Ok);
        mixed.ExtraDiscountPaisa = 17000;
        Assert.Equal(95000L, Post(fefo, mixed).TotalPaisa);
    }

    [Fact]
    public void TooLittleCashIsNotAutomaticallyForgiven()
    {
        using var t = new TestDatabase(); var input = Cart(t, 0); input.TenderedPaisa = 110000;
        Assert.False(TestData.Sales(t).Create(input, TestData.Owner).Ok);
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM sales;"));
    }

    [Fact]
    public void CashChangeAndPaymentRowsUseFinalPayable()
    {
        using var t = new TestDatabase(); Grant(t); var input = Cart(t); input.TenderedPaisa = 120000;
        var sale = Post(t, input); Assert.Equal(10000L, sale.ChangePaisa);
        Assert.Equal(110000L, Scalar(t, "SELECT SUM(amount_paisa) FROM payments WHERE method='cash';"));
    }

    [Fact]
    public void SplitCashAndCreditUseFinalPayable()
    {
        using var t = new TestDatabase(); Grant(t); var input = Cart(t);
        input.CustomerId = TestData.AddCustomer(t); input.PaymentMethod = "split";
        input.Payments.Add(new SalePaymentInput { Method = "credit", AmountPaisa = 10000 });
        input.TenderedPaisa = 100000;
        var sale = Post(t, input); Assert.Equal(10000L, sale.DuePaisa); Assert.Equal(100000L, sale.PaidPaisa);
        Assert.Equal(10000L, Scalar(t, "SELECT balance_paisa FROM customers;"));
    }

    [Fact]
    public void CreditReturnReducesOnlyFinalDebtNotPreExtraTotal()
    {
        using var t = new TestDatabase(); Grant(t); var input = Cart(t);
        input.CustomerId = TestData.AddCustomer(t); input.PaymentMethod = "credit";
        var sale = Post(t, input); var back = Back(sale); back.Lines.Add(Back(sale, 1).Lines[0]);
        var returned = TestData.Returns(t).Create(back, TestData.Owner);
        Assert.True(returned.Ok, returned.Error);
        Assert.Equal(110000L, returned.Value!.CreditReducedPaisa); Assert.Equal(0L, returned.Value.CashPaisa);
        Assert.Equal(0L, Scalar(t, "SELECT balance_paisa FROM customers;"));
    }

    [Fact]
    public void ReturnPolicyBFirst560Then540AndNoThirdRefund()
    {
        using var t = new TestDatabase(); Grant(t); var sale = Post(t, Cart(t)); var returns = TestData.Returns(t);
        var first = returns.Create(Back(sale), TestData.Owner); Assert.True(first.Ok, first.Error);
        Assert.Equal(56000L, first.Value!.RefundPaisa);
        var preview = returns.Quote(Back(sale, 1), TestData.Owner); Assert.True(preview.Ok, preview.Error);
        Assert.Equal(54000L, preview.Value!.RefundPaisa);
        Assert.Contains(preview.Value.Warnings, x => x.Contains("Extra Discount"));
        var last = returns.Create(Back(sale, 1), TestData.Owner); Assert.True(last.Ok, last.Error);
        Assert.Equal(54000L, last.Value!.RefundPaisa);
        Assert.False(returns.Create(Back(sale, 1), TestData.Owner).Ok);
        Assert.Equal(110000L, Scalar(t, "SELECT SUM(cash_refund_paisa) FROM sale_returns;"));
        Assert.Equal(112000L, Scalar(t, "SELECT SUM(line_total_paisa) FROM sale_items;"));
    }

    [Fact]
    public void FullReturnInOneRequestIsCappedAt1100()
    {
        using var t = new TestDatabase(); Grant(t); var sale = Post(t, Cart(t));
        var input = Back(sale); input.Lines.Add(Back(sale, 1).Lines[0]);
        var result = TestData.Returns(t).Create(input, TestData.Owner); Assert.True(result.Ok, result.Error);
        Assert.Equal(110000L, result.Value!.RefundPaisa);
    }

    [Fact]
    public void RepeatedQuantityReturnUsesBThenLastPaisaCap()
    {
        using var t = new TestDatabase(); Grant(t); var input = Cart(t);
        input.Lines.RemoveAt(1); input.Lines[0].Quantity = 2;
        var sale = Post(t, input); var returns = TestData.Returns(t);
        Assert.Equal(56000L, returns.Create(Back(sale), TestData.Owner).Value!.RefundPaisa);
        Assert.Equal(54000L, returns.Create(Back(sale), TestData.Owner).Value!.RefundPaisa);
    }

    [Fact]
    public void ReceiptAlwaysShowsExtraEvenWhenOrdinaryDiscountHidden()
    {
        using var t = new TestDatabase(); Grant(t); var sale = Post(t, Cart(t));
        t.Settings.Set("receipt.showDiscount", false, TestData.Owner);
        var receipts = new ReceiptService(t.Database, t.Settings, t.Log);
        var first = receipts.Build(sale.SaleId); Assert.True(first.Ok, first.Error);
        Assert.Contains("Before Extra Discount", first.Value!.PlainText);
        Assert.Contains("Extra Discount", first.Value.PlainText);
        Assert.Contains("1,100.00", first.Value.PlainText);
        Assert.Equal(first.Value.PlainText, receipts.Build(sale.SaleId).Value!.PlainText);
    }

    [Fact]
    public void ReportsProfitAndCategoriesReconcileWithoutProductAllocation()
    {
        using var t = new TestDatabase(); Grant(t); var input = Cart(t);
        input.CustomerId = TestData.AddCustomer(t);
        Post(t, input);
        var customer = TestData.Ledger(t).Page(input.CustomerId.Value);
        Assert.True(customer.Ok, customer.Error);
        Assert.Equal(30000L, customer.Value!.Summary.ProfitPaisa);
        var histories = new ProductHistoryService(t.Database, t.Settings, TestData.Permissions(t), t.Log);
        Assert.Contains("before_bill_extra_discount", histories.BuildCsv(TestData.Owner, input.Lines[0].ProductId).Value!);
        Assert.Contains("Amounts before bill Extra.", histories.BuildDocument(TestData.Owner, input.Lines[0].ProductId).Value!.PlainText);
        var result = TestData.Reports(t).Build(ReportRange.Today, TestData.Owner); Assert.True(result.Ok, result.Error);
        var report = result.Value!;
        Assert.Equal(110000L, report.Totals.SalesPaisa);
        Assert.Equal(30000L, report.Totals.ProfitPaisa);
        Assert.Equal(2000L, report.Totals.ExtraDiscountPaisa);
        Assert.Equal(110000L, report.Categories.Sum(x => x.SalesPaisa));
        Assert.Contains(report.Categories, x => x.Name.Contains("Extra Discount") && x.SalesPaisa == -2000);
    }

    [Fact]
    public void BookCheckAcceptsUnchangedLinesMinusSeparateExtra()
    {
        using var t = new TestDatabase(); Grant(t); Post(t, Cart(t));
        var result = new BookCheckService(t.Database, TestData.Permissions(t), t.Log).Run(TestData.Owner);
        Assert.True(result.Ok, result.Error);
        Assert.All(result.Value!.Rules.Where(x => x.Title.StartsWith("A bill")), x => Assert.True(x.Clean, x.Title));
    }

    [Fact]
    public void HoldResumePreservesExtraButCannotBypassNewAllowance()
    {
        using var t = new TestDatabase(); Grant(t); var input = Cart(t); var holds = TestData.Holds(t);
        Assert.True(holds.Hold(input, TestData.Owner).Ok);
        var row = holds.List(TestData.Owner).Value!.Single();
        var resumed = holds.Resume(TestData.Owner, row.Id); Assert.True(resumed.Ok, resumed.Error);
        Assert.Equal(2000L, resumed.Value!.ExtraDiscountPaisa);
        Grant(t, cap: 1000);
        Assert.False(TestData.Sales(t).Create(input, TestData.Owner).Ok);
    }

    [Fact]
    public void DraftJsonKeepsAbsoluteExtraNotConfirmation()
    {
        var original = new SaleInput { ExtraDiscountPaisa = 2000, RetailDiscountConfirmed = true };
        var clone = JsonSerializer.Deserialize<SaleInput>(JsonSerializer.Serialize(original));
        Assert.Equal(2000L, clone!.ExtraDiscountPaisa); Assert.False(clone.RetailDiscountConfirmed);
        using var t = new TestDatabase(); Grant(t); var input = Cart(t);
        var drafts = TestData.Drafts(t); var saved = drafts.SaveSaleDraft(TestData.Owner, input);
        Assert.True(saved.Ok, saved.Error);
        var reopened = drafts.OpenSale(TestData.Owner, saved.Value!.Id); Assert.True(reopened.Ok, reopened.Error);
        Assert.Equal(2000L, reopened.Value!.ExtraDiscountPaisa);
        Grant(t, cap: 1000);
        Assert.False(TestData.Sales(t).Create(reopened.Value, TestData.Owner).Ok);
    }

    [Fact]
    public void DedicatedExtraAuditIsRecordedWithTheSale()
    {
        using var t = new TestDatabase(); Grant(t); Post(t, Cart(t));
        Assert.Equal(1L, Scalar(t, "SELECT COUNT(*) FROM audit_logs WHERE action='sale_extra_discount';"));
    }

    [Fact]
    public void FailedCostCheckRollsBackAllSaleSideEffects()
    {
        using var t = new TestDatabase(); Grant(t, cap: 100000);
        Assert.False(TestData.Sales(t).Create(Cart(t, 99999), TestData.Owner).Ok);
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM sales;"));
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM sale_items;"));
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM payments;"));
        Assert.Equal(100L, Scalar(t, "SELECT SUM(qty_base) FROM batches;"));
        Assert.Equal(0L, Scalar(t, "SELECT COUNT(*) FROM audit_logs WHERE action='sale_extra_discount';"));
    }
}
