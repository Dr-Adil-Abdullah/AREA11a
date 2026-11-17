using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Goods-cost projections are independent from cash; not production/runtime certification.</summary>
public sealed class ExpenseVisibilityTests
{
    private sealed record Seed(int Product, int Expense, int Cash, int Goods);
    private static ExpenseService Cash(TestDatabase t) => TestData.CashOut(t);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static void Grant(TestDatabase t, User user, string module, bool allowed = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, module, allowed).Ok);
    private static User Staff(TestDatabase t, bool cost = false)
    {
        var u = TestData.Cashier(t); Grant(t, u, Modules.Cash); if (cost) Grant(t, u, Modules.PurchaseCost); return u;
    }
    private static ExpenseInput Rent() => new() { Category = "rent", Title = "Synthetic rent", AmountPaisa = 25000 };
    private static DrawingInput Goods(int product) => new() { Type = "goods", ProductId = product, QtyEntered = 3, Unit = "unit" };
    private static DrawingInput CashInput() => new() { Type = "cash", AmountPaisa = 7000, Note = "Synthetic cash" };
    private static Seed SeedRows(TestDatabase t, long cost = 12345)
    {
        var p = TestData.AddProduct(t, costPaisa: cost, batchQty: 10, name: "Drawing medicine"); var s = Cash(t);
        var expense = s.AddExpense(TestData.Owner, Rent()); Assert.True(expense.Ok, expense.Error);
        var cash = s.AddDrawing(TestData.Owner, CashInput()); Assert.True(cash.Ok, cash.Error);
        var goods = s.AddDrawing(TestData.Owner, Goods(p)); Assert.True(goods.Ok, goods.Error);
        return new Seed(p, expense.Value!.Id, cash.Value!.Id, goods.Value!.Id);
    }
    private static void HiddenGoods(TestDatabase t, User u)
    {
        var rows = Cash(t).List(u); Assert.Equal(3, rows.Count);
        var goods = rows.Single(row => row.Kind == ExpenseService.KindDrawingGoods);
        Assert.False(goods.AmountKnown); Assert.Equal(0L, goods.AmountPaisa); Assert.Equal("hidden", goods.AmountLabel);
        Assert.False(goods.TookCash); Assert.Equal("stock only", goods.CashLabel); Assert.Contains("Drawing medicine", goods.Note!);
        Assert.Equal(25000L, rows.Single(row => row.Kind == ExpenseService.KindExpense).AmountPaisa);
        Assert.Equal(7000L, rows.Single(row => row.Kind == ExpenseService.KindDrawingCash).AmountPaisa);
        var today = Cash(t).Today(u); Assert.True(today.CashKnown); Assert.False(today.GoodsCostKnown);
        Assert.Equal(0L, today.DrawingGoodsCostPaisa); Assert.Equal("hidden", today.GoodsLabel);
        Assert.Equal(32000L, today.CashOutPaisa); Assert.Equal(3, today.Entries);
    }

    [Fact]
    public void AnonymousListTodayAndVouchersRefuseWithoutKnownZeroFallback()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t);
        Assert.Empty(Cash(t).List(null)); var today = Cash(t).Today(null); Assert.False(today.CashKnown);
        Assert.Equal("hidden", today.CashOutLabel); Assert.Equal("hidden", today.GoodsLabel);
        Assert.False(Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingGoods, seed.Goods, null).Ok);
        Assert.False(Cash(t).BuildVoucherDocument(ExpenseService.KindExpense, seed.Expense, null).Ok);
    }

    [Fact]
    public void ForgedAndMissingStoredIdentitiesCannotReadAnyCashOutput()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); var u = Staff(t);
        foreach (var actor in new[] { new User { Id = u.Id, Role = "owner" }, new User { Id = int.MaxValue, Role = "owner" } })
        {
            Assert.Empty(Cash(t).List(actor)); Assert.False(Cash(t).Today(actor).CashKnown);
            Assert.False(Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingGoods, seed.Goods, actor).Ok);
        }
    }

    [Fact]
    public void InactiveCallerAndChangedStoredRoleRefuse()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); var off = new User { Id = 1, Role = "owner", Active = false };
        Assert.Empty(Cash(t).List(off)); Assert.False(Cash(t).Today(off).CashKnown);
        Execute(t, "UPDATE users SET role='manager' WHERE id=1;");
        Assert.Empty(Cash(t).List(TestData.Owner)); Assert.False(Cash(t).BuildVoucherDocument(ExpenseService.KindExpense, seed.Expense, TestData.Owner).Ok);
    }

    [Fact]
    public void InactiveStoredOwnerCannotReadOrPrepareVoucher()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); Execute(t, "UPDATE users SET active=0 WHERE id=1;");
        Assert.Empty(Cash(t).List(TestData.Owner)); Assert.False(Cash(t).Today(TestData.Owner).CashKnown);
        Assert.False(Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingCash, seed.Cash, TestData.Owner).Ok);
    }

    [Fact]
    public void CashModuleCannotBeReplacedByProfitOrCostGrant()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); var u = Staff(t, true); Grant(t, u, Modules.Profit); Grant(t, u, Modules.Cash, false);
        Assert.Empty(Cash(t).List(u)); Assert.False(Cash(t).Today(u).CashKnown);
        Assert.False(Cash(t).BuildVoucherDocument(ExpenseService.KindExpense, seed.Expense, u).Ok);
    }

    [Fact]
    public void MainOffMasksGoodsButKeepsActualCashExpenseAndDrawingAmounts()
    {
        using var t = new TestDatabase(); SeedRows(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok); HiddenGoods(t, TestData.Owner);
    }

    [Fact]
    public void ProfitOnlyAndManagerRankAreNotGoodsCostGrants()
    {
        using var t = new TestDatabase(); SeedRows(t); var u = Staff(t); Grant(t, u, Modules.Profit);
        HiddenGoods(t, u); HiddenGoods(t, TestData.AddUser(t, "manager"));
    }

    [Fact]
    public void CostOnlyRevealsExactGoodsCostWithoutProfit()
    {
        using var t = new TestDatabase(); SeedRows(t); var u = Staff(t, true);
        var row = Cash(t).List(u).Single(r => r.Kind == ExpenseService.KindDrawingGoods);
        Assert.True(row.AmountKnown); Assert.Equal(37035L, row.AmountPaisa); Assert.Equal(Money.Format(37035), row.AmountLabel);
        var today = Cash(t).Today(u); Assert.True(today.GoodsCostKnown); Assert.Equal(37035L, today.DrawingGoodsCostPaisa);
        Assert.False(TestData.Permissions(t).Can(u, Modules.Profit));
    }

    [Fact]
    public void ExplicitOwnerCostDenialAppliesToListTodayAndGoodsVoucher()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); Grant(t, TestData.Owner, Modules.PurchaseCost, false); HiddenGoods(t, TestData.Owner);
        var doc = Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingGoods, seed.Goods, TestData.Owner);
        Assert.True(doc.Ok, doc.Error); Assert.Contains("hidden", doc.Value!.PlainText);
    }

    [Fact]
    public void SameServiceUsesNextReadCostAndPageRevocation()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); var u = Staff(t, true); var s = Cash(t);
        Assert.True(s.Today(u).GoodsCostKnown); Grant(t, u, Modules.PurchaseCost, false);
        Assert.False(s.Today(u).GoodsCostKnown); Assert.Equal("hidden", s.List(u).Single(r => r.Kind == ExpenseService.KindDrawingGoods).AmountLabel);
        Assert.Contains("hidden", s.BuildVoucherDocument(ExpenseService.KindDrawingGoods, seed.Goods, u).Value!.PlainText);
        Grant(t, u, Modules.Cash, false); Assert.Empty(s.List(u)); Assert.False(s.Today(u).CashKnown);
    }

    [Fact]
    public void AuthorizedZeroGoodsCostIsKnownRatherThanHidden()
    {
        using var t = new TestDatabase(); SeedRows(t, 0); var s = Cash(t);
        Assert.True(s.Today(TestData.Owner).GoodsCostKnown); Assert.Equal(Money.Format(0), s.Today(TestData.Owner).GoodsLabel);
        Assert.True(s.List(TestData.Owner).Single(r => r.Kind == ExpenseService.KindDrawingGoods).AmountKnown);
        Grant(t, TestData.Owner, Modules.PurchaseCost, false); Assert.Equal("hidden", s.Today(TestData.Owner).GoodsLabel);
    }

    [Fact]
    public void NewGoodsVoucherMasksAmountAndPreservesOperationalNote()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); var u = Staff(t);
        var result = Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingGoods, seed.Goods, u); Assert.True(result.Ok, result.Error);
        Assert.Contains("Drawing medicine", result.Value!.PlainText); Assert.Contains("goods", result.Value.PlainText);
        Assert.Contains(result.Value.Lines, line => line.Text.StartsWith("Amount") && line.Text.EndsWith("hidden"));
        Assert.DoesNotContain(Money.Format(37035), result.Value.PlainText);
    }

    [Fact]
    public void CashVouchersStayReadableWithMainOffAndCostDenied()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var rent = Cash(t).BuildVoucherDocument(ExpenseService.KindExpense, seed.Expense, TestData.Owner);
        var cash = Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingCash, seed.Cash, TestData.Owner);
        Assert.True(rent.Ok, rent.Error); Assert.True(cash.Ok, cash.Error);
        Assert.Contains(Money.Format(25000), rent.Value!.PlainText); Assert.Contains(Money.Format(7000), cash.Value!.PlainText);
        Assert.DoesNotContain("hidden", rent.Value.PlainText); Assert.DoesNotContain("hidden", cash.Value.PlainText);
    }

    [Fact]
    public void GoodsRowCannotBeReadAsCashKindToBypassCostMask()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); var u = Staff(t);
        Assert.False(Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingCash, seed.Goods, u).Ok);
        Assert.False(Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingGoods, seed.Cash, u).Ok);
    }

    [Fact]
    public void VoucherRejectsUnknownKindAndMissingIdentifier()
    {
        using var t = new TestDatabase(); SeedRows(t);
        Assert.False(Cash(t).BuildVoucherDocument("owner_drawings OR 1=1", 1, TestData.Owner).Ok);
        Assert.False(Cash(t).BuildVoucherDocument(ExpenseService.KindExpense, int.MaxValue, TestData.Owner).Ok);
    }

    [Fact]
    public void FailedReadHasHiddenUnknownCashAndVoucherDoesNotExposeDatabaseDetails()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); Execute(t, "ALTER TABLE owner_drawings RENAME TO synthetic_missing_drawings;");
        Assert.Empty(Cash(t).List(TestData.Owner)); Assert.Equal("hidden", Cash(t).Today(TestData.Owner).CashOutLabel);
        var failed = Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingGoods, seed.Goods, TestData.Owner); Assert.False(failed.Ok);
        Assert.DoesNotContain("owner_drawings", failed.Error ?? string.Empty); Assert.Null(failed.Value);
    }

    [Fact]
    public void CashTotalNeverIncludesGoodsValuationAndEntriesRemainThree()
    {
        using var t = new TestDatabase(); SeedRows(t); var s = Cash(t); var visible = s.Today(TestData.Owner);
        Assert.Equal(32000L, visible.CashOutPaisa); Assert.Equal(37035L, visible.DrawingGoodsCostPaisa); Assert.Equal(3, visible.Entries);
        Grant(t, TestData.Owner, Modules.PurchaseCost, false); var hidden = s.Today(TestData.Owner);
        Assert.Equal(visible.CashOutPaisa, hidden.CashOutPaisa); Assert.Equal(visible.Entries, hidden.Entries);
    }

    [Fact]
    public void ReadAndVoucherPreparationDoNotChangeStoredData()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); var u = Staff(t);
        var tables = new[] { "expenses", "owner_drawings", "batches", "stock_movements", "audit_logs", "suppliers", "purchases" };
        var before = tables.ToDictionary(table => table, table => Number(t, "SELECT COUNT(*) FROM " + table));
        HiddenGoods(t, u); Assert.True(Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingGoods, seed.Goods, u).Ok);
        foreach (var table in tables) Assert.Equal(before[table], Number(t, "SELECT COUNT(*) FROM " + table));
        Assert.Equal(37035L, Number(t, "SELECT amount_paisa FROM owner_drawings WHERE type='goods';"));
        Assert.Equal(7L, Number(t, "SELECT qty_base FROM batches;"));
    }

    [Fact]
    public void MainOffGoodsDrawingMasksReturnedAmountButPostsActualStockAndCost()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, costPaisa: 12345, batchQty: 10);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var result = Cash(t).AddDrawing(TestData.Owner, Goods(p)); Assert.True(result.Ok, result.Error);
        Assert.False(result.Value!.AmountKnown); Assert.Equal(0L, result.Value.AmountPaisa); Assert.Equal("hidden", result.Value.AmountLabel);
        Assert.Equal(3d, result.Value.QtyBase); Assert.Equal(7d, result.Value.StockAfterBase);
        Assert.Equal(37035L, Number(t, "SELECT amount_paisa FROM owner_drawings;")); Assert.Equal(7L, Number(t, "SELECT qty_base FROM batches;"));
    }

    [Fact]
    public void OwnerCostDenialDoesNotHideOrBlockCashDrawingResult()
    {
        using var t = new TestDatabase(); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        var result = Cash(t).AddDrawing(TestData.Owner, CashInput()); Assert.True(result.Ok, result.Error);
        Assert.True(result.Value!.AmountKnown); Assert.Equal(7000L, result.Value.AmountPaisa); Assert.Equal(Money.Format(7000), result.Value.AmountLabel);
    }

    [Fact]
    public void CurrentCashGranteeCanRecordExpenseWithoutCostOrProfit()
    {
        using var t = new TestDatabase(); var u = Staff(t);
        var result = Cash(t).AddExpense(u, Rent()); Assert.True(result.Ok, result.Error); Assert.Equal(25000L, result.Value!.AmountPaisa);
        Assert.Equal(25000L, Cash(t).List(u).Single().AmountPaisa);
    }

    [Fact]
    public void ForgedOwnerCannotPostCashOrGoodsDrawing()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 10); var u = Staff(t); var forged = new User { Id = u.Id, Role = "owner" };
        Assert.False(Cash(t).AddDrawing(forged, CashInput()).Ok); Assert.False(Cash(t).AddDrawing(forged, Goods(p)).Ok);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM owner_drawings;")); Assert.Equal(10L, Number(t, "SELECT qty_base FROM batches;"));
    }

    [Fact]
    public void InactiveStoredOwnerCannotPostAnyNamedExpenseOrDrawing()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 10); Execute(t, "UPDATE users SET active=0 WHERE id=1;");
        Assert.False(Cash(t).AddExpense(TestData.Owner, Rent()).Ok); Assert.False(Cash(t).AddDrawing(TestData.Owner, CashInput()).Ok);
        Assert.False(Cash(t).AddDrawing(TestData.Owner, Goods(p)).Ok); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM expenses;"));
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM owner_drawings;"));
    }

    [Fact]
    public void RevokedCashModuleRefusesExpenseBeforePosting()
    {
        using var t = new TestDatabase(); var u = Staff(t); Grant(t, u, Modules.Cash, false);
        Assert.False(Cash(t).AddExpense(u, Rent()).Ok); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM expenses;"));
    }

    [Fact]
    public void GoodsFailureRollsBackStockInsteadOfReturningMaskedSuccess()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, batchQty: 10); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        Execute(t, "CREATE TRIGGER refuse_goods BEFORE INSERT ON owner_drawings WHEN NEW.type='goods' BEGIN SELECT RAISE(ABORT,'synthetic fault'); END;");
        var result = Cash(t).AddDrawing(TestData.Owner, Goods(p)); Assert.False(result.Ok); Assert.Null(result.Value);
        Assert.Equal(10L, Number(t, "SELECT qty_base FROM batches;")); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM stock_movements;"));
    }

    [Fact]
    public void GoodsDrawingStillDoesNotMoveDrawerCashWhenMainOff()
    {
        using var t = new TestDatabase(); var p = TestData.AddProduct(t, costPaisa: 12345, batchQty: 10);
        var shifts = new ShiftService(t.Database, t.Audit, TestData.Permissions(t), t.Log);
        Assert.True(shifts.Open(TestData.Owner, 100000, null).Ok);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.True(Cash(t).AddDrawing(TestData.Owner, Goods(p)).Ok); Assert.Equal(100000L, shifts.Current()!.ExpectedPaisa);
        Assert.Equal(0L, Cash(t).Today(TestData.Owner).CashOutPaisa);
    }

    [Fact]
    public void StockCategoryIsOnlyACashExpenseLabelNotReceivingOrGoodsValuation()
    {
        using var t = new TestDatabase(); TestData.AddProduct(t, batchQty: 10); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        Execute(t, "UPDATE expense_categories SET type='stock' WHERE name='rent';");
        var result = Cash(t).AddExpense(TestData.Owner, Rent()); Assert.True(result.Ok, result.Error);
        Assert.Equal(25000L, Cash(t).List(TestData.Owner).Single().AmountPaisa);
        Assert.Equal(10L, Number(t, "SELECT qty_base FROM batches;")); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM purchases;"));
    }

    [Fact]
    public void FinancialGrantDatabaseFailureMasksGoodsButNotOrdinaryCash()
    {
        using var t = new TestDatabase(); var seed = SeedRows(t); Execute(t, "DROP TABLE user_permissions;"); HiddenGoods(t, TestData.Owner);
        Assert.Contains("hidden", Cash(t).BuildVoucherDocument(ExpenseService.KindDrawingGoods, seed.Goods, TestData.Owner).Value!.PlainText);
        Assert.Contains(Money.Format(25000), Cash(t).BuildVoucherDocument(ExpenseService.KindExpense, seed.Expense, TestData.Owner).Value!.PlainText);
    }
}
