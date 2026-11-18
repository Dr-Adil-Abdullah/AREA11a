using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Read projections must not rewrite the historical negative stock or audit record.</summary>
public sealed class StockMinusVisibilityTests
{
    private static StockMinusService Registry(TestDatabase t) =>
        new(t.Database, t.Audit, TestData.Permissions(t), t.Log, t.Settings);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql) => t.Database.Tx(c => Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L));
    private static void Grant(TestDatabase t, User user, string module, bool allowed = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, module, allowed).Ok);
    private static User Staff(TestDatabase t, bool cost = false, bool profit = false)
    {
        var user = TestData.Cashier(t); Grant(t, user, Modules.Stock);
        if (cost) Grant(t, user, Modules.PurchaseCost);
        if (profit) Grant(t, user, Modules.Profit);
        return user;
    }
    private static int Seed(TestDatabase t, long cost = 12345) => TestData.AddProduct(t, costPaisa: cost, batchQty: -3, name: "Historical minus");
    private static StockMinusReport Read(TestDatabase t, User user)
    {
        var result = Registry(t).Report(user); Assert.True(result.Ok, result.Error); return result.Value!;
    }
    private static void Hidden(StockMinusReport report)
    {
        Assert.False(report.CostKnown); Assert.Equal(0L, report.TotalValuePaisa); Assert.Equal("hidden", report.TotalText);
        foreach (var row in report.Rows) { Assert.False(row.CostKnown); Assert.Equal(0L, row.ValuePaisa); Assert.Equal("hidden", row.ValueText); }
    }
    private static void WriteNote(TestDatabase t, int product) => Assert.True(Registry(t).Review(TestData.Owner, product, "Synthetic cost 37035 / profit 4567 note").Ok);

    [Fact]
    public void StockOnlyKeepsQuantityButMasksRawRowAggregateAndSummary()
    {
        using var t = new TestDatabase(); Seed(t); var user = Staff(t); var report = Read(t, user); Hidden(report);
        var row = Assert.Single(report.Rows); Assert.Equal(-3d, row.StockQty); Assert.Equal("Historical minus", row.Name);
        Assert.Contains("hidden", report.SummaryText); Assert.DoesNotContain("37035", report.SummaryText);
    }

    [Fact]
    public void ManagerRankDoesNotGrantPurchaseCost()
    {
        using var t = new TestDatabase(); Seed(t); var user = TestData.AddUser(t, "manager"); Grant(t, user, Modules.Stock);
        Hidden(Read(t, user));
    }

    [Fact]
    public void ProfitOnlyCannotRevealCostOrRawAuditNote()
    {
        using var t = new TestDatabase(); var p = Seed(t); WriteNote(t, p); var report = Read(t, Staff(t, profit: true));
        Hidden(report); var row = Assert.Single(report.Rows); Assert.Null(row.ReviewNote); Assert.Equal("hidden", row.ReviewNoteLabel);
    }

    [Fact]
    public void CostOnlySeesExactValuationButNotRawAuditDetails()
    {
        using var t = new TestDatabase(); var p = Seed(t); WriteNote(t, p); var user = Staff(t, cost: true);
        var report = Read(t, user); var row = Assert.Single(report.Rows);
        Assert.True(report.CostKnown); Assert.True(row.CostKnown); Assert.Equal(-37035L, row.ValuePaisa); Assert.Equal(-37035L, report.TotalValuePaisa);
        Assert.False(row.ReviewNoteKnown); Assert.Null(row.ReviewNote); Assert.Equal("hidden", row.ReviewNoteLabel);
    }

    [Fact]
    public void BothFinancialGrantsRevealStoredReviewDetails()
    {
        using var t = new TestDatabase(); var p = Seed(t); WriteNote(t, p); var report = Read(t, Staff(t, cost: true, profit: true));
        Assert.True(report.ReviewNotesKnown); var row = Assert.Single(report.Rows); Assert.True(row.ReviewNoteKnown);
        Assert.Contains("profit 4567", row.ReviewNote); Assert.Equal(TestData.SeedOwnerName, row.ReviewBy);
    }

    [Fact]
    public void MainSwitchOffMasksOwnerAndStaffCostAndAuditNote()
    {
        using var t = new TestDatabase(); var p = Seed(t); WriteNote(t, p); var user = Staff(t, cost: true, profit: true);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        foreach (var actor in new[] { TestData.Owner, user })
        { var report = Read(t, actor); Hidden(report); Assert.False(report.ReviewNotesKnown); Assert.Null(Assert.Single(report.Rows).ReviewNote); }
    }

    [Fact]
    public void OwnerCostCheckboxCanBeRevokedWithoutLosingRegistryMetadata()
    {
        using var t = new TestDatabase(); Seed(t); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        var report = Read(t, TestData.Owner); Hidden(report); Assert.Single(report.Rows);
    }

    [Fact]
    public void CostGrantDoesNotReplaceStockPagePermission()
    {
        using var t = new TestDatabase(); Seed(t); var user = Staff(t, cost: true); Grant(t, user, Modules.Stock, false);
        Assert.False(Registry(t).Report(user).Ok); Assert.False(Registry(t).CheckReadAccess(user).Ok);
    }

    [Fact]
    public void NullOrUnknownReaderIsRejected()
    {
        using var t = new TestDatabase(); Seed(t);
        Assert.False(Registry(t).Report(null).Ok); Assert.False(Registry(t).CheckReadAccess(null).Ok);
        Assert.False(Registry(t).Report(new User { Id = 99999, Role = "owner", Active = true }).Ok);
    }

    [Fact]
    public void SuppliedOwnerRoleCannotImpersonateCashierAccount()
    {
        using var t = new TestDatabase(); var p = Seed(t); var user = Staff(t, cost: true);
        var forged = new User { Id = user.Id, Role = "owner", Active = true };
        Assert.False(Registry(t).Report(forged).Ok); Assert.False(Registry(t).Review(forged, p, "Forged note").Ok);
    }

    [Fact]
    public void DeactivatedAccountCannotReuseOldReadOrReviewSnapshot()
    {
        using var t = new TestDatabase(); var p = Seed(t); var user = Staff(t, cost: true); Assert.True(Read(t, user).CostKnown);
        Execute(t, "UPDATE users SET active=0 WHERE id=" + user.Id + ";");
        Assert.False(Registry(t).Report(user).Ok); Assert.False(Registry(t).Review(user, p, "Stale active flag").Ok);
        Assert.False(Registry(t).CheckReadAccess(user).Ok);
    }

    [Fact]
    public void ChangedRoleRejectsOldCallerEvenIfNewRoleHasStockByDefault()
    {
        using var t = new TestDatabase(); var p = Seed(t); var user = Staff(t, cost: true);
        Execute(t, "UPDATE users SET role='manager' WHERE id=" + user.Id + ";");
        Assert.False(Registry(t).Report(user).Ok); Assert.False(Registry(t).Review(user, p, "Old role").Ok);
    }

    [Fact]
    public void RevocationIsObservedOnNextReportAndAccessSnapshot()
    {
        using var t = new TestDatabase(); Seed(t); var user = Staff(t, cost: true, profit: true); var service = Registry(t);
        Assert.True(service.CheckReadAccess(user).Value!.CostKnown); Assert.True(service.Report(user).Value!.CostKnown);
        Grant(t, user, Modules.PurchaseCost, false); Hidden(service.Report(user).Value!);
        Assert.False(service.CheckReadAccess(user).Value!.CostKnown); Assert.False(service.CheckReadAccess(user).Value!.ReviewNotesKnown);
    }

    [Fact]
    public void ProfitRevocationKeepsAuthorizedCostButRemovesRawNote()
    {
        using var t = new TestDatabase(); var p = Seed(t); WriteNote(t, p); var user = Staff(t, cost: true, profit: true);
        var service = Registry(t); Assert.True(service.CheckReadAccess(user).Value!.ReviewNotesKnown);
        Grant(t, user, Modules.Profit, false); var report = service.Report(user).Value!;
        Assert.True(report.CostKnown); Assert.False(report.ReviewNotesKnown); Assert.Null(Assert.Single(report.Rows).ReviewNote);
    }

    [Fact]
    public void RealZeroValuationRemainsKnownForCostReader()
    {
        using var t = new TestDatabase(); Seed(t, cost: 0); var user = Staff(t, cost: true); var report = Read(t, user);
        Assert.True(report.CostKnown); Assert.True(Assert.Single(report.Rows).CostKnown); Assert.Equal(0L, report.TotalValuePaisa); Assert.Equal("Rs 0", report.TotalText);
        Grant(t, user, Modules.PurchaseCost, false); Hidden(Read(t, user));
    }

    [Fact]
    public void EmptyAuthorizedReportDistinguishesKnownZeroFromHidden()
    {
        using var t = new TestDatabase(); var user = Staff(t, cost: true); var report = Read(t, user);
        Assert.Empty(report.Rows); Assert.True(report.CostKnown); Assert.Equal("Rs 0", report.TotalText);
        Grant(t, user, Modules.PurchaseCost, false); Hidden(Read(t, user));
    }

    [Fact]
    public void DefaultAndMixedKnownModelsCannotLabelUnknownTotalAsZero()
    {
        var report = new StockMinusReport(); Assert.Equal("hidden", report.TotalText);
        Assert.Equal("hidden", new StockMinusRow().ValueText);
        report.CostKnown = true; report.Rows.Add(new StockMinusRow { ValuePaisa = -37035 });
        Assert.Equal(0L, report.TotalValuePaisa); Assert.Equal("hidden", report.TotalText);
        Assert.Equal("hidden", new StockMinusRow { IsReviewed = true, ReviewNote = "private" }.ReviewNoteLabel);
    }

    [Fact]
    public void FinancialPermissionDatabaseFailureDoesNotFallBackToOwnerCost()
    {
        using var t = new TestDatabase(); Seed(t); Execute(t, "DROP TABLE user_permissions;");
        var report = Read(t, TestData.Owner); Hidden(report); Assert.False(report.ReviewNotesKnown);
    }

    [Fact]
    public void ReportErrorDoesNotEchoDatabaseSchemaOrInventSuccessfulEmptyReport()
    {
        using var t = new TestDatabase(); Seed(t); Execute(t, "ALTER TABLE batches RENAME TO synthetic_private_batches;");
        var result = Registry(t).Report(TestData.Owner); Assert.False(result.Ok); Assert.Null(result.Value);
        Assert.DoesNotContain("synthetic_private", result.Error); Assert.DoesNotContain("no such table", result.Error);
    }

    [Fact]
    public void MetadataReadNeverRewritesStoredNegativeValuationOrReview()
    {
        using var t = new TestDatabase(); var p = Seed(t); WriteNote(t, p); var user = Staff(t);
        var before = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        _ = Read(t, user); _ = Registry(t).CheckReadAccess(user);
        Assert.Equal(-37035L, Number(t, "SELECT SUM(qty_base*cost_paisa) FROM batches;"));
        Assert.Equal(before, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM audit_logs WHERE details='Synthetic cost 37035 / profit 4567 note';"));
    }

    [Fact]
    public void StockAuthorizedReviewDoesNotRequireCostOrProfit()
    {
        using var t = new TestDatabase(); var p = Seed(t); var user = Staff(t);
        Assert.True(Registry(t).Review(user, p, "Counted the shelf, no stock change").Ok);
        var report = Read(t, user); Hidden(report); Assert.True(Assert.Single(report.Rows).IsReviewed);
        Assert.Equal(-3L, Number(t, "SELECT qty_base FROM batches;")); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM stock_movements;"));
    }

    [Fact]
    public void ReviewRechecksRevokedStockBeforeAuditWrite()
    {
        using var t = new TestDatabase(); var p = Seed(t); var user = Staff(t); var service = Registry(t);
        Assert.True(service.CheckReadAccess(user).Ok); Grant(t, user, Modules.Stock, false);
        var before = Number(t, "SELECT COUNT(*) FROM audit_logs WHERE action='stock_minus_review';");
        Assert.False(service.Review(user, p, "Permission was revoked").Ok);
        Assert.Equal(before, Number(t, "SELECT COUNT(*) FROM audit_logs WHERE action='stock_minus_review';"));
    }

    [Fact]
    public void ReviewUsesCurrentStoredNameRatherThanSuppliedDisplayName()
    {
        using var t = new TestDatabase(); var p = Seed(t); var user = Staff(t);
        Execute(t, "UPDATE users SET name='Current reviewer' WHERE id=" + user.Id + ";");
        Assert.True(Registry(t).Review(user, p, "Current identity checked").Ok);
        Assert.Equal("Current reviewer", Assert.Single(Read(t, TestData.Owner).Rows).ReviewBy);
    }

    [Fact]
    public void AuditFailureRollsBackReviewWithoutChangingStockOrMovement()
    {
        using var t = new TestDatabase(); var p = Seed(t);
        Execute(t, "CREATE TRIGGER reject_minus_note BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic private fault'); END;");
        var result = Registry(t).Review(TestData.Owner, p, "Will fail audit"); Assert.False(result.Ok);
        Assert.DoesNotContain("synthetic private fault", result.Error);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM audit_logs WHERE action='stock_minus_review';"));
        Assert.Equal(-3L, Number(t, "SELECT qty_base FROM batches;")); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM stock_movements;"));
    }
}
