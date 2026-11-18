using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Current Cash, unknown-vs-closed, transactional fault and two-connection regression facts. UNRUN.</summary>
public sealed class ShiftCurrentAccessTests
{
    private static ShiftService Shifts(TestDatabase t) => TestData.Shifts(t);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql)
    { using var c = t.Database.Open(); return Convert.ToInt64(t.Database.Scalar(c, sql) ?? 0L); }
    private static User Staff(TestDatabase t, string name = "Synthetic worker", string role = "cashier")
    {
        var id = t.Database.Tx(c => { t.Database.Execute(c, "INSERT INTO users(name,role,active) VALUES(@name,@role,1);", new { name, role }); return (int)t.Database.NextId(c); });
        var user = new User { Id = id, Name = name, Role = role };
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, id, Modules.Cash, true).Ok); return user;
    }
    private static void Cash(TestDatabase t, User u, bool yes) => Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, Modules.Cash, yes).Ok);
    private static ShiftPage Page(TestDatabase t)
    { var result = Shifts(t).Page(TestData.Owner); Assert.True(result.Ok, result.Error); return result.Value!; }
    private static void Fault(TestDatabase t, string target) => Execute(t, "CREATE TRIGGER synthetic_shift_fault BEFORE INSERT ON " + target + " BEGIN SELECT RAISE(ABORT,'synthetic-shift-fault'); END;");
    private static void DeniedReader(TestDatabase t, User? user)
    {
        var service = Shifts(t); Assert.False(service.CheckAccess(user).Ok);
        var page = service.Page(user); Assert.False(page.Ok); Assert.Null(page.Value); Assert.False(service.ByPerson(user).Ok);
    }

    [Fact]
    public void AnonymousAndMissingReadersCannotReadMoney()
    {
        using var t = new TestDatabase(); DeniedReader(t, null); DeniedReader(t, new User { Id = int.MaxValue, Role = "owner" });
    }

    [Fact]
    public void InactiveSuppliedAndStoredReadersCannotReadMoney()
    {
        using var t = new TestDatabase(); var u = Staff(t); u.Active = false; DeniedReader(t, u); u.Active = true;
        Execute(t, "UPDATE users SET active=0 WHERE id=" + u.Id); DeniedReader(t, u);
    }

    [Fact]
    public void ForgedOwnerAndChangedStoredRoleCannotReadMoney()
    {
        using var t = new TestDatabase(); var u = Staff(t); DeniedReader(t, new User { Id = u.Id, Role = "owner" });
        Execute(t, "UPDATE users SET role='manager' WHERE id=" + u.Id); DeniedReader(t, u);
    }

    [Fact]
    public void CashRevocationBlocksPageAndBothMoneyWrites()
    {
        using var t = new TestDatabase(); var u = Staff(t); var service = Shifts(t); Assert.True(service.Open(u, 10000, null).Ok);
        Cash(t, u, false); DeniedReader(t, u); Assert.False(service.Open(u, 10000, null).Ok); Assert.False(service.Close(u, 10000, null).Ok);
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM shifts WHERE status='open';"));
    }

    [Fact]
    public void CashRestorationWorksOnSameService()
    {
        using var t = new TestDatabase(); var u = Staff(t); var service = Shifts(t); Cash(t, u, false); Assert.False(service.Page(u).Ok);
        Cash(t, u, true); Assert.True(service.Open(u, 10000, null).Ok); Assert.True(service.Page(u).Ok); Assert.True(service.Close(u, 10000, null).Ok);
    }

    [Fact]
    public void CashDoesNotRequireCostProfitOrMain()
    {
        using var t = new TestDatabase(); var u = Staff(t);
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, Modules.Profit, false).Ok); Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, Modules.PurchaseCost, false).Ok);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Assert.True(Shifts(t).Open(u, 10000, null).Ok); Assert.True(Shifts(t).Page(u).Ok); Assert.True(Shifts(t).Close(u, 10000, null).Ok);
    }

    [Fact]
    public void FinancialGrantsCannotReplaceCash()
    {
        using var t = new TestDatabase(); var u = Staff(t); Cash(t, u, false);
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, Modules.Profit, true).Ok); Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, Modules.PurchaseCost, true).Ok);
        DeniedReader(t, u);
    }

    [Fact]
    public void PosOnlyGetsStateButNotDrawerMoney()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok);
        var state = service.DrawerState(u); Assert.True(state.Ok, state.Error); Assert.True(state.Value); Assert.False(service.Page(u).Ok);
    }

    [Fact]
    public void NoCashOrPosCannotEvenReadState()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, Modules.Pos, false).Ok);
        Assert.False(Shifts(t).DrawerState(u).Ok);
    }

    [Fact]
    public void SuccessfullyClosedDrawerIsKnownNotFailed()
    {
        using var t = new TestDatabase(); var page = Shifts(t).Page(TestData.Owner); Assert.True(page.Ok, page.Error); Assert.Null(page.Value!.Current);
        var state = Shifts(t).DrawerState(TestData.Owner); Assert.True(state.Ok); Assert.False(state.Value);
    }

    [Fact]
    public void DatabaseFailureIsNotSuccessfulClosedOrEmpty()
    {
        using var t = new TestDatabase(); Execute(t, "ALTER TABLE shifts RENAME TO synthetic_missing_shifts;");
        var page = Shifts(t).Page(TestData.Owner); var state = Shifts(t).DrawerState(TestData.Owner);
        Assert.False(page.Ok); Assert.Null(page.Value); Assert.False(state.Ok); Assert.DoesNotContain("no such table", page.Error!); Assert.DoesNotContain("synthetic_missing", state.Error!);
    }

    [Fact]
    public void NonSeniorCashReaderCannotReadPerPersonReport()
    {
        using var t = new TestDatabase(); var u = Staff(t); Assert.True(Shifts(t).Page(u).Ok); Assert.False(Shifts(t).Page(u).Value!.CanSeePeople); Assert.False(Shifts(t).ByPerson(u).Ok);
    }

    [Fact]
    public void CurrentManagerWithCashCanReadPerPersonReport()
    {
        using var t = new TestDatabase(); var u = Staff(t, role: "manager"); Assert.True(Shifts(t).Page(u).Value!.CanSeePeople); Assert.True(Shifts(t).ByPerson(u).Ok);
    }

    [Fact]
    public void DemotedCachedManagerCannotReadOrCloseAsSenior()
    {
        using var t = new TestDatabase(); var u = Staff(t, role: "manager"); Assert.True(Shifts(t).Open(TestData.Owner, 10000, null).Ok);
        Execute(t, "UPDATE users SET role='cashier' WHERE id=" + u.Id); Assert.False(Shifts(t).ByPerson(u).Ok); Assert.False(Shifts(t).Close(u, 10000, null).Ok);
    }

    [Fact]
    public void DeactivatedAndForgedActorsCannotOpenCloseOrHandover()
    {
        using var t = new TestDatabase(); var u = Staff(t); var next = Staff(t, "Synthetic next"); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok);
        var forged = new User { Id = u.Id, Role = "owner" }; Assert.False(service.Open(forged, 0, null).Ok); Assert.False(service.Close(forged, 10000, null).Ok); Assert.False(service.Handover(forged, next, 10000, null).Ok);
        Execute(t, "UPDATE users SET active=0 WHERE id=1;"); Assert.False(service.Close(TestData.Owner, 10000, null).Ok); Assert.False(service.Handover(TestData.Owner, next, 10000, null).Ok);
    }

    [Fact]
    public void CurrentCollectorNameIsUsedForOpenAndCloseAudit()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE users SET name='Stored counter actor' WHERE id=1;"); var service = Shifts(t);
        Assert.True(service.Open(TestData.Owner, 10000, null).Ok); Assert.True(service.Close(TestData.Owner, 10000, null).Ok);
        Assert.Equal(2L, Number(t, "SELECT COUNT(*) FROM audit_logs WHERE action IN ('shift_open','shift_close') AND user_name='Stored counter actor';"));
    }

    [Fact]
    public void HandoverUsesStoredActorAndRecipientNames()
    {
        using var t = new TestDatabase(); var next = Staff(t, "Old next"); Execute(t, "UPDATE users SET name='Stored next' WHERE id=" + next.Id); Execute(t, "UPDATE users SET name='Stored actor' WHERE id=1;");
        var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok); var result = service.Handover(TestData.Owner, next, 10000, null); Assert.True(result.Ok, result.Error);
        Assert.Equal("Stored actor", result.Value!.GivenBy); Assert.Equal("Stored next", result.Value.GivenTo); Assert.Equal("Stored next", result.Value.From.HandedToName);
    }

    [Fact]
    public void RevokedRecipientCannotTakeDrawer()
    {
        using var t = new TestDatabase(); var next = Staff(t); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok); Cash(t, next, false);
        Assert.False(service.Handover(TestData.Owner, next, 10000, null).Ok); Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM shifts;"));
    }

    [Fact]
    public void InactiveOrChangedRecipientCannotTakeDrawer()
    {
        using var t = new TestDatabase(); var next = Staff(t); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok);
        Execute(t, "UPDATE users SET active=0 WHERE id=" + next.Id); Assert.False(service.Handover(TestData.Owner, next, 10000, null).Ok);
        Execute(t, "UPDATE users SET active=1,role='manager' WHERE id=" + next.Id); Assert.False(service.Handover(TestData.Owner, next, 10000, null).Ok);
    }

    [Fact]
    public void PeerStillCannotCloseOrHandoverAnotherDrawer()
    {
        using var t = new TestDatabase(); var peer = Staff(t); var next = Staff(t, "Synthetic next"); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok);
        Assert.False(service.Close(peer, 10000, null).Ok); Assert.False(service.Handover(peer, next, 10000, null).Ok);
    }

    [Fact]
    public void OpenAuditFaultRollsBackInsertedShift()
    {
        using var t = new TestDatabase(); Fault(t, "audit_logs"); var result = Shifts(t).Open(TestData.Owner, 10000, null);
        Assert.False(result.Ok); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM shifts;")); Assert.DoesNotContain("synthetic-shift-fault", result.Error!);
    }

    [Fact]
    public void CloseAuditFaultRestoresOpenShift()
    {
        using var t = new TestDatabase(); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok); var audit = Number(t, "SELECT COUNT(*) FROM audit_logs;"); Fault(t, "audit_logs");
        Assert.False(service.Close(TestData.Owner, 10000, null).Ok); Assert.True(Page(t).Current!.IsOpen); Assert.Null(Page(t).Current!.ActualPaisa); Assert.Equal(audit, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }

    [Fact]
    public void HandoverAuditFaultRollsBackBothHalves()
    {
        using var t = new TestDatabase(); var next = Staff(t); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok); Fault(t, "audit_logs");
        Assert.False(service.Handover(TestData.Owner, next, 9000, "Synthetic short count").Ok); Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM shifts;")); Assert.Equal(1, Page(t).Current!.UserId); Assert.Empty(Page(t).Discrepancies);
    }

    [Fact]
    public void OpeningDiscrepancyFaultRollsBackShiftAndAudit()
    {
        using var t = new TestDatabase(); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok); Assert.True(service.Close(TestData.Owner, 10000, null).Ok);
        var audit = Number(t, "SELECT COUNT(*) FROM audit_logs;"); Fault(t, "shift_discrepancies"); Assert.False(service.Open(TestData.Owner, 9000, "Synthetic gap").Ok);
        Assert.Null(Page(t).Current); Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM shifts;")); Assert.Equal(audit, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }

    [Fact]
    public void ClosingDiscrepancyFaultRollsBackUpdateAndAudit()
    {
        using var t = new TestDatabase(); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok); var audit = Number(t, "SELECT COUNT(*) FROM audit_logs;"); Fault(t, "shift_discrepancies");
        Assert.False(service.Close(TestData.Owner, 9000, "Synthetic short").Ok); Assert.True(Page(t).Current!.IsOpen); Assert.Equal(audit, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }

    [Fact]
    public void HandoverDiscrepancyFaultRollsBackUpdateInsertAndAudits()
    {
        using var t = new TestDatabase(); var next = Staff(t); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok); var audit = Number(t, "SELECT COUNT(*) FROM audit_logs;"); Fault(t, "shift_discrepancies");
        Assert.False(service.Handover(TestData.Owner, next, 9000, "Synthetic short").Ok); Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM shifts;")); Assert.True(Page(t).Current!.IsOpen); Assert.Equal(audit, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }

    [Fact]
    public void OpenPostwriteRereadFailureRollsBack()
    {
        using var t = new TestDatabase(); Execute(t, "CREATE TRIGGER synthetic_hide_open AFTER INSERT ON shifts BEGIN UPDATE shifts SET status='closed' WHERE id=NEW.id; END;");
        var result = Shifts(t).Open(TestData.Owner, 10000, null); Assert.False(result.Ok); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM shifts;")); Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM audit_logs WHERE action='shift_open';"));
    }

    [Fact]
    public void OneRupeeReasonAndCountedNextFloatArePreserved()
    {
        using var t = new TestDatabase(); var next = Staff(t); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok);
        Assert.False(service.Close(TestData.Owner, 9900, null).Ok); Assert.False(service.Handover(TestData.Owner, next, 9900, null).Ok);
        var result = service.Handover(TestData.Owner, next, 9900, "Synthetic one rupee short"); Assert.True(result.Ok, result.Error);
        Assert.Equal(-100L, result.Value!.From.DifferencePaisa); Assert.Equal(9900L, result.Value.To.OpeningFloatPaisa); Assert.Single(Page(t).Discrepancies);
    }

    [Fact]
    public void InvalidPageLimitsAreNotSuccessfulEmptyLists()
    {
        using var t = new TestDatabase(); Assert.False(Shifts(t).Page(TestData.Owner, 0).Ok); Assert.False(Shifts(t).Page(TestData.Owner, 501).Ok);
    }

    [Fact]
    public void ReadPageStateAndSeniorReportDoNotWrite()
    {
        using var t = new TestDatabase(); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok); var audit = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        Assert.True(service.Page(TestData.Owner).Ok); Assert.True(service.DrawerState(TestData.Owner).Ok); Assert.True(service.ByPerson(TestData.Owner).Ok);
        Assert.Equal(audit, Number(t, "SELECT COUNT(*) FROM audit_logs;")); Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM shifts;"));
    }

    [Fact]
    public void TwoDatabaseInstancesCannotBothOpenTheDrawer()
    {
        using var t = new TestDatabase(); var first = Shifts(t);
        var secondDb = new Database(t.Database.FilePath, pooling: false, durable: false); var secondAudit = new AuditService(secondDb, t.Log);
        var second = new ShiftService(secondDb, secondAudit, new PermissionService(secondDb, t.Log, secondAudit), t.Log);
        using var gate = new System.Threading.ManualResetEventSlim(false);
        var a = Task.Run(() => { gate.Wait(); return first.Open(TestData.Owner, 10000, null); });
        var b = Task.Run(() => { gate.Wait(); return second.Open(TestData.Owner, 10000, null); }); gate.Set(); Task.WaitAll(a, b);
        Assert.Equal(1, new[] { a.Result, b.Result }.Count(r => r.Ok)); Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM shifts WHERE status='open';"));
    }

    [Fact]
    public void RepeatedCloseDoesNotWriteAnotherClosing()
    {
        using var t = new TestDatabase(); var service = Shifts(t); Assert.True(service.Open(TestData.Owner, 10000, null).Ok); Assert.True(service.Close(TestData.Owner, 10000, null).Ok);
        var audit = Number(t, "SELECT COUNT(*) FROM audit_logs;"); Assert.False(service.Close(TestData.Owner, 10000, null).Ok); Assert.Equal(audit, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }
    [Fact]
    public void HandoverPostwriteRereadFailureRollsBackBothHalves()
    {
        using var t = new TestDatabase(); var next = Staff(t); var service = Shifts(t);
        Assert.True(service.Open(TestData.Owner, 10000, null).Ok); var audit = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        Execute(t, "CREATE TRIGGER synthetic_remove_next AFTER INSERT ON shifts WHEN NEW.opened_from_shift_id IS NOT NULL BEGIN DELETE FROM shifts WHERE id=NEW.id; END;");
        Assert.False(service.Handover(TestData.Owner, next, 10000, null).Ok);
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM shifts;")); Assert.True(Page(t).Current!.IsOpen);
        Assert.Equal(audit, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
    }

    [Fact]
    public void SessionNoteRequiresCheckingWithoutCertifyingOpenOrClosed()
    {
        using var t = new TestDatabase(); SessionHandover.Mark(t.Settings, "Synthetic expiring actor");
        var note = SessionHandover.Read(t.Settings); Assert.Contains("needs checking", note);
        Assert.DoesNotContain("was left open", note); Assert.DoesNotContain("is closed", note);
        SessionHandover.Clear(t.Settings); Assert.Equal(string.Empty, SessionHandover.Read(t.Settings));
    }

    [Fact]
    public void CashGrantReadFailureDoesNotFallBackToManagerDefault()
    {
        using var t = new TestDatabase(); var manager = Staff(t, role: "manager");
        Execute(t, "ALTER TABLE user_permissions RENAME TO synthetic_missing_grants;");
        var service = Shifts(t); Assert.False(service.CheckAccess(manager).Ok); Assert.False(service.Page(manager).Ok);
        Assert.False(service.ByPerson(manager).Ok); Assert.False(service.Open(manager, 10000, null).Ok);
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM shifts;"));
    }

}
