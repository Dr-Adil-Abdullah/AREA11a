using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Public projections must never become inputs to frozen valuation/posting.</summary>
public sealed class StockValuationVisibilityTests
{
    private sealed record Seed(int Product, int Session, int Line);
    private static StockService Stock(TestDatabase t) => new(t.Database, t.Audit, t.Settings, TestData.Permissions(t), t.Log);
    private static StockTakeService Takes(TestDatabase t) => new(t.Database, t.Audit, new NumberingService(t.Settings), t.Settings, TestData.Permissions(t), t.Log);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L);
    }
    private static void Grant(TestDatabase t, User user, string module, bool allowed = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, module, allowed).Ok);
    private static User Staff(TestDatabase t, bool cost = false)
    {
        var user = TestData.Cashier(t); Grant(t, user, Modules.Stock); Grant(t, user, Modules.StockTake);
        if (cost) Grant(t, user, Modules.PurchaseCost); return user;
    }
    private static Seed Counted(TestDatabase t, long cost = 12345)
    {
        var p = TestData.AddProduct(t, costPaisa: cost, batchQty: 10, name: "Frozen medicine");
        var service = Takes(t); var opened = service.Open(TestData.Owner); Assert.True(opened.Ok, opened.Error);
        var line = Assert.Single(service.Sheet(TestData.Owner, opened.Value!.Id));
        Assert.True(service.Count(TestData.Owner, line.Id, 7, "unit", "synthetic count").Ok);
        return new Seed(p, opened.Value.Id, line.Id);
    }
    private static WriteOffInput Input(int product) => new() { ProductId = product, QtyEntered = 2, Unit = "unit", Reason = StockService.ReasonDamaged };
    private static void HiddenSession(StockTakeSession session)
    {
        Assert.False(session.CostKnown); Assert.Equal(0L, session.ShortPaisa); Assert.Equal(0L, session.ExtraPaisa);
        Assert.Equal("hidden", session.ShortLabel); Assert.Equal("hidden", session.ExtraLabel);
        Assert.Equal(1, session.CountedLines); Assert.Equal(1, session.TotalLines);
    }
    private static void HiddenLine(StockTakeLine line)
    {
        Assert.False(line.CostKnown); Assert.Null(line.ValuePaisa); Assert.Equal("hidden", line.ValueLabel);
        Assert.Equal(10d, line.SystemQtyBase); Assert.Equal(7d, line.CountedQtyBase); Assert.Equal(-3d, line.DiffQtyBase);
        Assert.Equal("Frozen medicine", line.ProductName); Assert.True(line.HasDifference);
    }

    [Fact]
    public void AnonymousStockAndStockTakeReadsRefuse()
    {
        using var t = new TestDatabase(); var seed = Counted(t);
        Assert.Empty(Stock(t).History(null)); Assert.Null(Stock(t).LossTodayPaisa(null));
        Assert.Null(Takes(t).Current(null)); Assert.Empty(Takes(t).History(null));
        Assert.Empty(Takes(t).Sheet(null, seed.Session)); Assert.Empty(Takes(t).NotOnSheet(null, seed.Session));
        Assert.False(Takes(t).BuildSheetDocument(seed.Session, null).Ok);
    }

    [Fact]
    public void ForgedAndMissingStoredReadersCannotBuildOrReadSheets()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t);
        foreach (var actor in new[] { new User { Id = user.Id, Role = "owner" }, new User { Id = int.MaxValue, Role = "owner" } })
        {
            Assert.Empty(Stock(t).History(actor)); Assert.Null(Stock(t).LossTodayPaisa(actor));
            Assert.Null(Takes(t).Current(actor)); Assert.Empty(Takes(t).History(actor)); Assert.Empty(Takes(t).Sheet(actor, seed.Session));
            Assert.Empty(Takes(t).NotOnSheet(actor, seed.Session)); Assert.False(Takes(t).BuildSheetDocument(seed.Session, actor).Ok);
        }
    }

    [Fact]
    public void InactiveStoredOwnerLosesAllNamedReadOutputs()
    {
        using var t = new TestDatabase(); var seed = Counted(t); Execute(t, "UPDATE users SET active=0 WHERE id=1;");
        Assert.Empty(Stock(t).History(TestData.Owner)); Assert.Null(Stock(t).LossTodayPaisa(TestData.Owner));
        Assert.Null(Takes(t).Current(TestData.Owner)); Assert.Empty(Takes(t).History(TestData.Owner));
        Assert.Empty(Takes(t).Sheet(TestData.Owner, seed.Session)); Assert.Empty(Takes(t).NotOnSheet(TestData.Owner, seed.Session));
        Assert.False(Takes(t).BuildSheetDocument(seed.Session, TestData.Owner).Ok);
    }

    [Fact]
    public void InactiveCallerSnapshotAndChangedStoredRoleCannotRead()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var off = new User { Id = 1, Role = "owner", Active = false };
        Assert.Null(Takes(t).Current(off)); Assert.Empty(Stock(t).History(off));
        Execute(t, "UPDATE users SET role='manager' WHERE id=1;");
        Assert.False(Takes(t).BuildSheetDocument(seed.Session, TestData.Owner).Ok); Assert.Null(Stock(t).LossTodayPaisa(TestData.Owner));
    }

    [Fact]
    public void CostGrantCannotReplaceEitherPageModule()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t, true);
        Grant(t, user, Modules.Stock, false); Assert.Empty(Stock(t).History(user)); Assert.Null(Stock(t).LossTodayPaisa(user));
        Assert.NotEmpty(Takes(t).Sheet(user, seed.Session));
        Grant(t, user, Modules.StockTake, false); Assert.Null(Takes(t).Current(user)); Assert.Empty(Takes(t).History(user));
        Assert.Empty(Takes(t).Sheet(user, seed.Session)); Assert.Empty(Takes(t).NotOnSheet(user, seed.Session));
        Assert.False(Takes(t).BuildSheetDocument(seed.Session, user).Ok);
    }

    [Fact]
    public void ManagerHistoryKeepsQuantitiesButMasksWriteOffMoney()
    {
        using var t = new TestDatabase(); var seed = Counted(t); Assert.True(Stock(t).WriteOff(Input(seed.Product), TestData.Owner).Ok);
        var row = Assert.Single(Stock(t).History(TestData.AddUser(t, "manager")));
        Assert.False(row.CostKnown); Assert.Equal(0L, row.ValuePaisa); Assert.Equal("hidden", row.ValueLabel);
        Assert.Equal(2d, row.QtyBase); Assert.Equal("Frozen medicine", row.ProductName); Assert.Equal("damaged", row.Reason);
    }

    [Fact]
    public void ProfitOnlyCannotRevealStockHistoryOrDailyTotal()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t); Grant(t, user, Modules.Profit);
        Assert.True(Stock(t).WriteOff(Input(seed.Product), TestData.Owner).Ok);
        Assert.Equal("hidden", Assert.Single(Stock(t).History(user)).ValueLabel); Assert.Null(Stock(t).LossTodayPaisa(user));
    }

    [Fact]
    public void CostOnlySeesExactStockHistoryAndDailyTotal()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t, true);
        Assert.True(Stock(t).WriteOff(Input(seed.Product), TestData.Owner).Ok);
        var row = Assert.Single(Stock(t).History(user)); Assert.True(row.CostKnown); Assert.Equal(24690L, row.ValuePaisa);
        Assert.Equal(24690L, Stock(t).LossTodayPaisa(user)); Assert.False(TestData.Permissions(t).Can(user, Modules.Profit));
    }

    [Fact]
    public void KnownZeroTodayIsNotHiddenNull()
    {
        using var t = new TestDatabase(); var user = Staff(t, true); Assert.Equal(0L, Stock(t).LossTodayPaisa(user));
        Grant(t, user, Modules.PurchaseCost, false); Assert.Null(Stock(t).LossTodayPaisa(user));
    }

    [Fact]
    public void MaskedWriteOffResultStillPostsActualCostAndStock()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t);
        var result = Stock(t).WriteOff(Input(seed.Product), user); Assert.True(result.Ok, result.Error);
        Assert.False(result.Value!.CostKnown); Assert.Equal(0L, result.Value.LossPaisa); Assert.Equal("hidden", result.Value.LossLabel);
        Assert.Equal(-2d, result.Value.QtyBase); Assert.Equal(8d, result.Value.StockAfterBase);
        Assert.Equal(24690L, Number(t, "SELECT value_paisa FROM stock_adjustments;"));
        Assert.Equal(8L, Number(t, "SELECT qty_base FROM batches;"));
    }

    [Fact]
    public void MainOffMasksOwnerWriteOffResultAndHistory()
    {
        using var t = new TestDatabase(); var seed = Counted(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var result = Stock(t).WriteOff(Input(seed.Product), TestData.Owner); Assert.True(result.Ok, result.Error);
        Assert.Equal("hidden", result.Value!.LossLabel); Assert.Equal("hidden", Assert.Single(Stock(t).History(TestData.Owner)).ValueLabel);
        Assert.Null(Stock(t).LossTodayPaisa(TestData.Owner)); Assert.Equal(24690L, Number(t, "SELECT value_paisa FROM stock_adjustments;"));
    }

    [Fact]
    public void InactiveOrForgedWriteOffActorCannotPostBeforeResultMask()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t);
        Assert.False(Stock(t).WriteOff(Input(seed.Product), new User { Id = user.Id, Role = "owner" }).Ok);
        Execute(t, "UPDATE users SET active=0 WHERE id=1;");
        Assert.False(Stock(t).WriteOff(Input(seed.Product), TestData.Owner).Ok);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM stock_adjustments;")); Assert.Equal(10L, Number(t, "SELECT qty_base FROM batches;"));
    }

    [Fact]
    public void CostDeniedSessionAndSheetKeepFrozenOperationalNumbers()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t);
        HiddenSession(Takes(t).Current(user)!); HiddenSession(Assert.Single(Takes(t).History(user)));
        HiddenLine(Assert.Single(Takes(t).Sheet(user, seed.Session)));
    }

    [Fact]
    public void ProfitOnlyCannotRevealFrozenValuesOrSessionAggregates()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t); Grant(t, user, Modules.Profit);
        HiddenSession(Takes(t).Current(user)!); HiddenLine(Assert.Single(Takes(t).Sheet(user, seed.Session)));
    }

    [Fact]
    public void CostOnlySeesOriginalFrozenLineAndBothAggregates()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t, true); var service = Takes(t);
        var line = Assert.Single(service.Sheet(user, seed.Session)); Assert.True(line.CostKnown); Assert.Equal(-37035L, line.ValuePaisa);
        var session = service.Current(user)!; Assert.True(session.CostKnown); Assert.Equal(37035L, session.ShortPaisa); Assert.Equal(0L, session.ExtraPaisa);
    }

    [Fact]
    public void MainOffThenOnRestoresOwnerFrozenValuesWithoutRecounting()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var service = Takes(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        HiddenSession(service.Current(TestData.Owner)!); HiddenLine(Assert.Single(service.Sheet(TestData.Owner, seed.Session)));
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, true, TestData.Owner).Ok);
        Assert.Equal(-37035L, Assert.Single(service.Sheet(TestData.Owner, seed.Session)).ValuePaisa);
    }

    [Fact]
    public void ExplicitOwnerCostDenialMasksReadDocumentAndApplyResult()
    {
        using var t = new TestDatabase(); var seed = Counted(t); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        HiddenSession(Takes(t).Current(TestData.Owner)!);
        var doc = Takes(t).BuildSheetDocument(seed.Session, TestData.Owner); Assert.True(doc.Ok, doc.Error); Assert.Contains("hidden", doc.Value!.PlainText);
        var applied = Takes(t).Apply(TestData.Owner, seed.Session); Assert.True(applied.Ok, applied.Error);
        Assert.False(applied.Value!.CostKnown); Assert.Equal(0L, applied.Value.ShortPaisa); Assert.Equal("hidden", applied.Value.ShortLabel);
        Assert.Equal(37035L, Number(t, "SELECT short_paisa FROM stock_takes;")); Assert.Equal(7L, Number(t, "SELECT qty_base FROM batches;"));
    }

    [Fact]
    public void SameServiceRevocationAffectsCurrentHistorySheetAndNewDocument()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t, true); var service = Takes(t);
        Assert.True(service.Current(user)!.CostKnown); Grant(t, user, Modules.PurchaseCost, false);
        HiddenSession(service.Current(user)!); HiddenSession(Assert.Single(service.History(user)));
        HiddenLine(Assert.Single(service.Sheet(user, seed.Session)));
        Assert.Contains("hidden", service.BuildSheetDocument(seed.Session, user).Value!.PlainText);
    }

    [Fact]
    public void DifferenceFilterDoesNotDependOnMoneyVisibility()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t);
        HiddenLine(Assert.Single(Takes(t).Sheet(user, seed.Session, differencesOnly: true)));
        Assert.True(Takes(t).Count(TestData.Owner, seed.Line, 10, "unit", null).Ok);
        Assert.Empty(Takes(t).Sheet(user, seed.Session, differencesOnly: true));
    }

    [Fact]
    public void UncountedAndKnownZeroLabelsRemainDifferentFromHidden()
    {
        using var t = new TestDatabase(); TestData.AddProduct(t, costPaisa: 0, batchQty: 10); var service = Takes(t);
        var opened = service.Open(TestData.Owner).Value!; var line = Assert.Single(service.Sheet(TestData.Owner, opened.Id));
        Assert.True(line.CostKnown); Assert.Equal("—", line.ValueLabel);
        Assert.True(service.Count(TestData.Owner, line.Id, 9, "unit", null).Ok);
        var zero = Assert.Single(service.Sheet(TestData.Owner, opened.Id)); Assert.Equal(0L, zero.ValuePaisa); Assert.Equal(Money.Format(0), zero.ValueLabel);
        Grant(t, TestData.Owner, Modules.PurchaseCost, false); Assert.Equal("hidden", Assert.Single(service.Sheet(TestData.Owner, opened.Id)).ValueLabel);
    }

    [Fact]
    public void NewDocumentHidesNetValueButPreservesBookAndCounted()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t);
        var result = Takes(t).BuildSheetDocument(seed.Session, user); Assert.True(result.Ok, result.Error); var text = result.Value!.PlainText;
        Assert.Contains("Frozen medicine", text); Assert.Contains("book 10", text); Assert.Contains("counted 7", text);
        Assert.Contains(result.Value.Lines, row => row.Text.StartsWith("Value") && row.Text.EndsWith("hidden"));
        Assert.DoesNotContain(Money.Format(-37035), text);
    }

    [Fact]
    public void AuthorizedDocumentRetainsExactSignedFrozenNetValue()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t, true);
        var result = Takes(t).BuildSheetDocument(seed.Session, user); Assert.True(result.Ok, result.Error);
        Assert.Contains(Money.Format(-37035), result.Value!.PlainText); Assert.DoesNotContain("hidden", result.Value.PlainText);
    }

    [Fact]
    public void MissingDocumentAndDatabaseFailureNeverProduceSuccessfulEmptySheet()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var s = Takes(t);
        Assert.False(s.BuildSheetDocument(int.MaxValue, TestData.Owner).Ok);
        Execute(t, "ALTER TABLE stock_take_items RENAME TO missing_sheet_items;");
        var failed = s.BuildSheetDocument(seed.Session, TestData.Owner); Assert.False(failed.Ok);
        Assert.DoesNotContain("missing_sheet_items", failed.Error ?? string.Empty);
    }

    [Fact]
    public void NotOnSheetMasksRawCostWithoutLosingPackMetadata()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var p = TestData.AddProduct(t, costPaisa: 9876, name: "Found medicine"); var user = Staff(t);
        var row = Assert.Single(Takes(t).NotOnSheet(user, seed.Session, "Found medicine"));
        Assert.Equal(p, row.Id); Assert.False(row.CostKnown); Assert.Equal(0L, row.CostPaisa); Assert.Equal(2, row.BoxStrips); Assert.Equal(10, row.StripTablets);
        Grant(t, user, Modules.PurchaseCost); Assert.Equal(9876L, Assert.Single(Takes(t).NotOnSheet(user, seed.Session, "Found medicine")).CostPaisa);
    }

    [Fact]
    public void FinancialGrantDatabaseFailureMasksOwnerOutputsButDoesNotInventZeroKnown()
    {
        using var t = new TestDatabase(); var seed = Counted(t); Execute(t, "DROP TABLE user_permissions;");
        HiddenSession(Takes(t).Current(TestData.Owner)!); HiddenLine(Assert.Single(Takes(t).Sheet(TestData.Owner, seed.Session)));
        Assert.Null(Stock(t).LossTodayPaisa(TestData.Owner));
        Assert.Contains("hidden", Takes(t).BuildSheetDocument(seed.Session, TestData.Owner).Value!.PlainText);
    }

    [Fact]
    public void MaskedOpenResultDoesNotZeroFrozenCostUsedByCountAndApply()
    {
        using var t = new TestDatabase(); TestData.AddProduct(t, costPaisa: 12345, batchQty: 10);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok); var s = Takes(t);
        var opened = s.Open(TestData.Owner); Assert.True(opened.Ok, opened.Error); Assert.False(opened.Value!.CostKnown);
        var line = Assert.Single(s.Sheet(TestData.Owner, opened.Value.Id)); Assert.True(s.Count(TestData.Owner, line.Id, 7, "unit", null).Ok);
        var applied = s.Apply(TestData.Owner, opened.Value.Id); Assert.True(applied.Ok, applied.Error); Assert.Equal("hidden", applied.Value!.ShortLabel);
        Assert.Equal(12345L, Number(t, "SELECT system_cost_paisa FROM stock_take_items;"));
        Assert.Equal(-37035L, Number(t, "SELECT value_paisa FROM stock_take_items;")); Assert.Equal(37035L, Number(t, "SELECT short_paisa FROM stock_takes;"));
        Assert.False(s.Apply(TestData.Owner, opened.Value.Id).Ok);
    }

    [Fact]
    public void LaterLiveCostChangeCannotRewriteFrozenApplyOrReadValues()
    {
        using var t = new TestDatabase(); var seed = Counted(t); Execute(t, "UPDATE batches SET cost_paisa=99999;");
        Execute(t, "UPDATE products SET cost_paisa=99999;"); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        HiddenLine(Assert.Single(Takes(t).Sheet(TestData.Owner, seed.Session)));
        var applied = Takes(t).Apply(TestData.Owner, seed.Session); Assert.True(applied.Ok, applied.Error);
        Assert.Equal(37035L, Number(t, "SELECT short_paisa FROM stock_takes;"));
        Grant(t, TestData.Owner, Modules.PurchaseCost); Assert.Equal(-37035L, Assert.Single(Takes(t).Sheet(TestData.Owner, seed.Session)).ValuePaisa);
    }

    [Fact]
    public void InactiveOwnerCannotOpenOrApplyEvenWhenSuppliedSnapshotSaysOwner()
    {
        using var t = new TestDatabase(); var seed = Counted(t); Execute(t, "UPDATE users SET active=0 WHERE id=1;");
        Assert.False(Takes(t).Open(TestData.Owner).Ok); Assert.False(Takes(t).Apply(TestData.Owner, seed.Session).Ok);
        Assert.Equal("open", ReadStatus(t)); Assert.Equal(10L, Number(t, "SELECT qty_base FROM batches;"));
    }
    private static string ReadStatus(TestDatabase t)
    {
        using var c = t.Database.Open(); return t.Database.QuerySingle<string>(c, "SELECT status FROM stock_takes LIMIT 1;")!;
    }

    [Fact]
    public void MaskedReadAndDocumentPreparationDoNotChangeData()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t);
        var tables = new[] { "stock_takes", "stock_take_items", "batches", "stock_adjustments", "stock_movements", "audit_logs" };
        var counts = tables.ToDictionary(table => table, table => Number(t, "SELECT COUNT(*) FROM " + table));
        _ = Stock(t).History(user); _ = Stock(t).LossTodayPaisa(user); _ = Takes(t).Current(user); _ = Takes(t).History(user);
        _ = Takes(t).Sheet(user, seed.Session); _ = Takes(t).NotOnSheet(user, seed.Session); Assert.True(Takes(t).BuildSheetDocument(seed.Session, user).Ok);
        foreach (var table in tables) Assert.Equal(counts[table], Number(t, "SELECT COUNT(*) FROM " + table));
        Assert.Equal(12345L, Number(t, "SELECT system_cost_paisa FROM stock_take_items;")); Assert.Equal(-37035L, Number(t, "SELECT value_paisa FROM stock_take_items;"));
    }

    [Fact]
    public void WriteOffValuationFailureRollsBackRatherThanReturningMaskedSuccess()
    {
        using var t = new TestDatabase(); var seed = Counted(t); var user = Staff(t);
        Execute(t, "CREATE TRIGGER reject_adjustment BEFORE INSERT ON stock_adjustments BEGIN SELECT RAISE(ABORT, 'synthetic fault'); END;");
        var result = Stock(t).WriteOff(Input(seed.Product), user); Assert.False(result.Ok); Assert.Null(result.Value);
        Assert.Equal(10L, Number(t, "SELECT qty_base FROM batches;")); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM stock_adjustments;"));
    }
}
