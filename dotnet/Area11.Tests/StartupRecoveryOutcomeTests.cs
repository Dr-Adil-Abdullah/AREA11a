using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Public recovery + real isolated schema and synthetic stores. No Windows/WPF execution.</summary>
public sealed class StartupRecoveryOutcomeTests
{
    private static AutoStartEntry Old(string value = "synthetic old command") => AutoStartEntry.Present(AutoStartValueKind.String, value);
    private sealed class Store : ICheckedAutoStartStore
    {
        public AutoStartEntry Entry { get; set; } = Old();
        public int Captures { get; private set; }
        public int Attempts { get; private set; }
        public int Writes { get; private set; }
        public Action<Store>? OnCapture { get; set; }
        public Action<Store>? BeforeCompare { get; set; }
        public Action<Store>? AfterWrite { get; set; }
        public AutoStartEntry? Expected { get; private set; }
        public AutoStartEntry Capture() { Captures++; OnCapture?.Invoke(this); return Entry; }
        public void Replace(AutoStartEntry expected, AutoStartEntry next)
        {
            Attempts++; Expected = expected; BeforeCompare?.Invoke(this);
            if (!Entry.Matches(expected)) throw new InvalidOperationException("Synthetic entry changed");
            if (Entry.Matches(next)) return;
            Entry = next; Writes++; AfterWrite?.Invoke(this);
            if (!Capture().Matches(next)) throw new InvalidOperationException("Synthetic cleanup readback failed");
        }
        public void Enable(string path) => throw new InvalidOperationException("No legacy Enable in checked recovery");
        public void Disable() => throw new InvalidOperationException("No legacy Disable in checked recovery");
        public bool IsEnabled() => throw new InvalidOperationException("No bool snapshot in checked recovery");
    }
    private sealed class Legacy : IAutoStartStore
    {
        public int Attempts { get; private set; }
        public bool Throw { get; init; }
        public void Enable(string path) => throw new InvalidOperationException("Recovery cannot enable");
        public void Disable() { Attempts++; if (Throw) throw new InvalidOperationException("Synthetic legacy cleanup"); }
        public bool IsEnabled() => throw new InvalidOperationException("A bool cannot prove readback");
    }
    private static StartupService Service(TestDatabase t, IAutoStartStore m, IAutoStartStore u) => new(t.Settings,
        new AuthService(t.Database, t.Audit, t.Settings, t.Log), TestData.Permissions(t), t.Audit, t.Log, m, u);
    private static void Protected(TestDatabase t) => TestData.SeedRawSetting(t, StartupService.ModeKey, StartupService.Protected);
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c,
        "SELECT COUNT(*) FROM audit_logs WHERE action='protection_reset';")));
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n", t.Database.Query<string>(c,
        "SELECT key || '=' || value || '@' || updated_at FROM settings ORDER BY key;")));
    private static void Fault(TestDatabase t, string mode) => t.Database.Tx(c => t.Database.Execute(c, mode == "setting" ? @"
CREATE TRIGGER synthetic_recovery_fault BEFORE UPDATE ON settings WHEN NEW.key='startup.mode'
BEGIN SELECT RAISE(ABORT,'synthetic recovery setting fault'); END;" : @"
CREATE TRIGGER synthetic_recovery_fault BEFORE INSERT ON audit_logs WHEN NEW.action='protection_reset'
BEGIN SELECT RAISE(ABORT,'synthetic recovery audit fault'); END;"));
    private static SettingsService Other(TestDatabase t)
    {
        var db = new Database(t.Database.FilePath, pooling: false, durable: false); var audit = new AuditService(db, t.Log);
        return new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
    }
    private static void Saved(ServiceResult result)
    { Assert.True(result.Ok, result.Error); Assert.False(result.NoCommitConfirmed); Assert.Equal("manual", result.SettingsEdit!.GetString(StartupService.ModeKey)); }

    [Fact]
    public void ManualAndSystemAuditCommitBeforeAnyCheckedWindowsMutation()
    {
        using var t = new TestDatabase(); Protected(t); var m = new Store(); var u = new Store();
        m.BeforeCompare = _ => { Assert.Equal(1, Audits(t)); Assert.Equal("manual", t.Settings.GetString(StartupService.ModeKey)); };
        u.BeforeCompare = _ => Assert.Equal(1, Audits(t)); var result = Service(t, m, u).ResetToManual();
        Saved(result); Assert.Empty(result.Warnings); Assert.False(m.Entry.IsPresent); Assert.False(u.Entry.IsPresent);
        var row = t.Database.Read(c => t.Database.QuerySingle<AuditRow>(c,
            "SELECT user_name,old_value,new_value,details FROM audit_logs WHERE action='protection_reset';"));
        Assert.Equal("system", row!.UserName); Assert.Equal("\"protected\"", row.OldValue); Assert.Equal("\"manual\"", row.NewValue);
        Assert.Contains("separate best-effort", row.Details); Assert.Equal("null", result.SettingsEdit!.GetRaw(FocusGuardService.GuardKey));
    }
    private sealed class AuditRow
    { public string UserName { get; set; } = ""; public string OldValue { get; set; } = ""; public string NewValue { get; set; } = ""; public string Details { get; set; } = ""; }

    [Theory]
    [InlineData("string")]
    [InlineData("expand")]
    [InlineData("binary")]
    [InlineData("none")]
    [InlineData("multi")]
    [InlineData("dword")]
    [InlineData("qword")]
    public void CleanupUsesExactCapturedKindAndValueNotBooleanSnapshot(string kind)
    {
        using var t = new TestDatabase(); var old = kind switch
        {
            "expand" => AutoStartEntry.Present(AutoStartValueKind.ExpandString, "%SYNTHETIC%\\Old.exe"),
            "binary" => AutoStartEntry.Present(AutoStartValueKind.Binary, new byte[] { 1, 2 }),
            "none" => AutoStartEntry.Present(AutoStartValueKind.None, new byte[] { 3 }),
            "multi" => AutoStartEntry.Present(AutoStartValueKind.MultiString, new[] { "synthetic", "words" }),
            "dword" => AutoStartEntry.Present(AutoStartValueKind.DWord, -1),
            "qword" => AutoStartEntry.Present(AutoStartValueKind.QWord, long.MinValue),
            _ => Old()
        };
        var m = new Store { Entry = old }; var u = new Store { Entry = AutoStartEntry.Missing };
        var result = Service(t, m, u).ResetToManual(); Saved(result); Assert.Empty(result.Warnings);
        Assert.True(m.Expected!.Matches(old)); Assert.Equal(1, m.Writes); Assert.Equal(0, u.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StuckEntryStillAllowsManualButWarnsAndPreventsBlindSameServiceReplay(bool machine)
    {
        using var t = new TestDatabase(); Protected(t); var m = new Store(); var u = new Store(); var stuck = machine ? m : u;
        stuck.BeforeCompare = _ => throw new UnauthorizedAccessException("Synthetic stuck entry"); var service = Service(t, m, u);
        var result = service.ResetToManual(); Saved(result); Assert.NotEmpty(result.Warnings); Assert.True(stuck.Entry.IsPresent);
        Assert.False((machine ? u : m).Entry.IsPresent); var attempts = m.Attempts + u.Attempts;
        var blocked = service.ResetToManual(); Assert.False(blocked.Ok); Assert.False(blocked.NoCommitConfirmed); Assert.Contains("SAVED", blocked.Error!);
        Assert.Equal(attempts, m.Attempts + u.Attempts); Assert.Equal(1, Audits(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnreadableInitialSnapshotSkipsThatStoreButNotManualCommitOrOtherCleanup(bool machine)
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store(); var unreadable = machine ? m : u;
        unreadable.OnCapture = _ => throw new InvalidOperationException("Synthetic unavailable store");
        var result = Service(t, m, u).ResetToManual(); Saved(result); Assert.NotEmpty(result.Warnings);
        Assert.Equal(0, unreadable.Attempts); Assert.True(unreadable.Entry.IsPresent); Assert.False((machine ? u : m).Entry.IsPresent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcurrentDifferentEntryIsNotDeletedOrRestored(bool machine)
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store(); var changed = machine ? m : u; var concurrent = Old("synthetic newer command");
        changed.BeforeCompare = s => s.Entry = concurrent; var result = Service(t, m, u).ResetToManual(); Saved(result);
        Assert.NotEmpty(result.Warnings); Assert.True(changed.Entry.Matches(concurrent)); Assert.Equal(0, changed.Writes); Assert.Equal(1, changed.Attempts);
    }

    [Theory]
    [InlineData("setting")]
    [InlineData("audit")]
    public void KnownRollbackChangesNeitherManualModeAuditNorWindowsAndAllowsReviewedRetry(string fault)
    {
        using var t = new TestDatabase(); Protected(t); var before = State(t); var m = new Store(); var u = new Store(); var service = Service(t, m, u); Fault(t, fault);
        var failed = service.ResetToManual(); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Null(failed.SettingsEdit);
        Assert.Equal(before, State(t)); Assert.Equal("protected", t.Settings.GetString(StartupService.ModeKey)); Assert.Equal(0, Audits(t));
        Assert.Equal(0, m.Attempts + u.Attempts); t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_recovery_fault;"));
        Saved(service.ResetToManual()); Assert.Equal(1, Audits(t));
    }

    [Fact]
    public void DeferredCommitFailureIsUnknownAndCannotStartWindowsCleanupOrBlindRetry()
    {
        using var t = new TestDatabase(); Protected(t); var before = State(t); var m = new Store(); var u = new Store(); var service = Service(t, m, u);
        t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_recovery_commit(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_recovery_commit_audit AFTER INSERT ON audit_logs WHEN NEW.action='protection_reset'
BEGIN INSERT INTO synthetic_recovery_commit VALUES(-99999); END;"));
        var failed = service.ResetToManual(); Assert.False(failed.Ok); Assert.False(failed.NoCommitConfirmed); Assert.Null(failed.SettingsEdit);
        Assert.Contains("unconfirmed", failed.Error!); Assert.Equal(before, State(t)); Assert.Equal(0, Audits(t)); Assert.Equal(0, m.Attempts + u.Attempts);
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_recovery_commit_audit;"));
        Assert.False(service.ResetToManual().Ok); Assert.Equal(0, m.Attempts + u.Attempts); Assert.Equal(0, Audits(t));
    }

    [Fact]
    public void EntryReappearingDuringOtherCleanupIsReportedNotQuietlyDeletedAgain()
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store(); var newer = Old("synthetic reappeared command");
        u.AfterWrite = _ => m.Entry = newer; var result = Service(t, m, u).ResetToManual(); Saved(result);
        Assert.NotEmpty(result.Warnings); Assert.True(m.Entry.Matches(newer)); Assert.Equal(1, m.Writes); Assert.Equal(1, m.Attempts);
    }

    [Fact]
    public void LaterModeWriterStopsRemainingCleanupAndReceiptDoesNotRebase()
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store(); var other = Other(t);
        m.BeforeCompare = _ => Assert.True(other.Set(StartupService.ModeKey, StartupService.Auto, TestData.Owner).Ok);
        var service = Service(t, m, u); var result = service.ResetToManual(); Saved(result); Assert.NotEmpty(result.Warnings);
        Assert.Equal(0, u.Attempts); Assert.True(u.Entry.IsPresent); Assert.Equal("manual", result.SettingsEdit!.GetString(StartupService.ModeKey));
        Assert.Equal("auto", other.GetString(StartupService.ModeKey)); Assert.False(service.ResetToManual().Ok); Assert.Equal(1, Audits(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyBestEffortCleanupNeverClaimsVerifiedAbsenceOrPermitsBlindReplay(bool throws)
    {
        using var t = new TestDatabase(); var m = new Legacy { Throw = throws }; var u = new Legacy(); var service = Service(t, m, u);
        var result = service.ResetToManual(); Saved(result); Assert.NotEmpty(result.Warnings); Assert.Equal(1, m.Attempts); Assert.Equal(1, u.Attempts);
        Assert.False(service.ResetToManual().Ok); Assert.Equal(1, m.Attempts); Assert.Equal(1, Audits(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusGuardSavedSwitchAndRuntimeChoiceAreUnchangedByStartupRecovery(bool enabled)
    {
        using var t = new TestDatabase(); TestData.SeedRawSetting(t, FocusGuardService.GuardKey, enabled); var guard = new FocusGuardService(t.Settings, t.Audit, t.Log);
        Assert.Equal(enabled, guard.Enabled); var before = t.Settings.GetRaw(FocusGuardService.GuardKey);
        Saved(Service(t, new Store(), new Store()).ResetToManual()); Assert.Equal(before, t.Settings.GetRaw(FocusGuardService.GuardKey)); Assert.Equal(enabled, guard.Enabled);
    }

    [Fact]
    public void ShiftRecoveryRemainsAnExplicitSystemOperationBeforeInteractiveLogin()
    {
        using var t = new TestDatabase(); Protected(t); t.Database.Sessions.Enable();
        var result = Service(t, new Store(), new Store()).ResetToManual(); Saved(result); Assert.Equal(1, Audits(t));
    }

    [Fact]
    public void MissingStartupRowIsUpsertedWithAtomicAuditNotASettingOnlyRepair()
    {
        using var t = new TestDatabase(); t.Database.Tx(c => t.Database.Execute(c, "DELETE FROM settings WHERE key='startup.mode';"));
        var result = Service(t, new Store(), new Store()).ResetToManual(); Saved(result); Assert.Equal(1, Audits(t));
        Assert.Equal("manual", t.Settings.GetString(StartupService.ModeKey));
    }

    [Fact]
    public void FullyVerifiedCompletedRecoveryMayAcceptANewExplicitRequest()
    {
        using var t = new TestDatabase(); var service = Service(t, new Store(), new Store());
        var first = service.ResetToManual(); Saved(first); Assert.Empty(first.Warnings);
        var second = service.ResetToManual(); Saved(second); Assert.Empty(second.Warnings); Assert.NotSame(first.SettingsEdit, second.SettingsEdit); Assert.Equal(2, Audits(t));
    }

    [Fact]
    public void RecoveryReceiptCannotLendAnUntouchedMainSettingsBaseline()
    {
        using var t = new TestDatabase(); var result = Service(t, new Store(), new Store()).ResetToManual(); Saved(result);
        var failed = t.Settings.SaveEditForm(new Dictionary<string, object?> { ["store.name"] = "Synthetic renamed shop" }, TestData.Owner, result.SettingsEdit);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Equal(1, Audits(t));
    }
}
