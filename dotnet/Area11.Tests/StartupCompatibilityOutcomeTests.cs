using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Public SetMode + real schema + synthetic stores. Not registry/WPF/provider execution.</summary>
public sealed class StartupCompatibilityOutcomeTests
{
    private const string Path = @"C:\Synthetic\New.exe";
    private sealed class Legacy : IAutoStartStore
    {
        public bool Present { get; set; }
        public int Enables { get; private set; }
        public int Disables { get; private set; }
        public int Reads { get; private set; }
        public Action<Legacy>? BeforeEnable { get; set; }
        public Action<Legacy>? AfterEnable { get; set; }
        public Action<Legacy>? BeforeDisable { get; set; }
        public Action<Legacy>? OnRead { get; set; }
        public void Enable(string path) { Enables++; BeforeEnable?.Invoke(this); Present = true; AfterEnable?.Invoke(this); }
        public void Disable() { Disables++; BeforeDisable?.Invoke(this); Present = false; }
        public bool IsEnabled() { Reads++; OnRead?.Invoke(this); return Present; }
    }
    private sealed class Checked : ICheckedAutoStartStore
    {
        public AutoStartEntry Entry { get; set; } = AutoStartEntry.Present(AutoStartValueKind.ExpandString, "%SYNTHETIC%\\Old.exe --retained");
        public int Captures { get; private set; }
        public int Writes { get; private set; }
        public AutoStartEntry Capture() { Captures++; return Entry; }
        public void Replace(AutoStartEntry before, AutoStartEntry next)
        {
            if (!Capture().Matches(before)) throw new InvalidOperationException("Synthetic changed entry");
            if (Entry.Matches(next)) return; Entry = next; Writes++;
        }
        public void Enable(string path) => throw new InvalidOperationException("No legacy Enable in checked dispatch");
        public void Disable() => throw new InvalidOperationException("No legacy Disable in checked dispatch");
        public bool IsEnabled() => throw new InvalidOperationException("No bool snapshot in checked dispatch");
    }
    private static StartupService Service(TestDatabase t, IAutoStartStore m, IAutoStartStore u) => new(t.Settings,
        new AuthService(t.Database, t.Audit, t.Settings, t.Log), TestData.Permissions(t), t.Audit, t.Log, m, u);
    private static SettingsEditForm Form(TestDatabase t)
    {
        var loaded = t.Settings.LoadEditForm(TestData.Owner, new[] { StartupService.ModeKey }); Assert.True(loaded.Ok, loaded.Error); return loaded.Value!;
    }
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c,
        "SELECT COUNT(*) FROM audit_logs WHERE action='settings_change' AND entity_id='startup.mode';")));
    private static void AuditFault(TestDatabase t) => t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TRIGGER synthetic_compat_audit BEFORE INSERT ON audit_logs WHEN NEW.entity_id='startup.mode'
BEGIN SELECT RAISE(ABORT,'synthetic compatibility audit'); END;"));
    private static void CommitFault(TestDatabase t) => t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_compat_commit(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_compat_commit_audit AFTER INSERT ON audit_logs WHEN NEW.entity_id='startup.mode'
BEGIN INSERT INTO synthetic_compat_commit VALUES(-99999); END;"));
    private static void Unknown(ServiceResult result)
    { Assert.False(result.Ok); Assert.False(result.NoCommitConfirmed); Assert.Null(result.SettingsEdit); }

    [Theory]
    [InlineData("manual")]
    [InlineData("auto")]
    [InlineData("protected")]
    public void LegacySuccessHasExactDatabaseReceiptButOnlyPresencePostconditions(string mode)
    {
        using var t = new TestDatabase(); var m = new Legacy { Present = true }; var u = new Legacy { Present = true }; var service = Service(t, m, u);
        var saved = service.SetMode(TestData.Owner, mode, Path); Assert.True(saved.Ok, saved.Error);
        Assert.Equal(mode, saved.SettingsEdit!.GetString(StartupService.ModeKey)); Assert.Equal(mode != "manual", m.Present); Assert.False(u.Present);
        Assert.Equal(1, Audits(t)); Assert.Empty(saved.Warnings);
        Assert.True(service.SetMode(TestData.Owner, mode, Path, saved.SettingsEdit).Ok); Assert.Equal(2, Audits(t));
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("auto")]
    [InlineData("protected")]
    public void CheckedHostCompatibilityDispatchNeverUsesBooleanOrImperativeStoreMethods(string mode)
    {
        using var t = new TestDatabase(); var m = new Checked(); var u = new Checked(); var saved = Service(t, m, u).SetMode(TestData.Owner, mode, Path);
        Assert.True(saved.Ok, saved.Error); Assert.Equal(mode, saved.SettingsEdit!.GetString(StartupService.ModeKey));
        Assert.True(m.Entry.Matches(mode == "manual" ? AutoStartEntry.Missing : AutoStartEntry.Command(Path))); Assert.False(u.Entry.IsPresent);
        Assert.Equal(1, Audits(t));
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("path")]
    [InlineData("actor")]
    public void InvalidCompatibilityRequestDoesNotReadOrMutateStores(string refusal)
    {
        using var t = new TestDatabase(); var m = new Legacy(); var u = new Legacy();
        var failed = Service(t, m, u).SetMode(refusal == "actor" ? TestData.Cashier(t) : TestData.Owner,
            refusal == "mode" ? "sometimes" : "auto", refusal == "path" ? "" : Path);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Equal(0, m.Reads + u.Reads + m.Enables + u.Enables); Assert.Equal(0, Audits(t));
    }

    [Fact]
    public void BothAccessRefusalsScopeNoChangeToDatabaseNotPossiblyMutatedWindows()
    {
        using var t = new TestDatabase(); var m = new Legacy(); var u = new Legacy();
        m.BeforeEnable = _ => throw new UnauthorizedAccessException("synthetic"); u.AfterEnable = _ => throw new UnauthorizedAccessException("synthetic post-write");
        var service = Service(t, m, u); var failed = service.SetMode(TestData.Owner, "auto", Path); Unknown(failed);
        Assert.Contains("Nothing was changed in startup settings.", failed.Error!); Assert.Contains("Windows entries may have changed", failed.Error!);
        Assert.True(u.Present); Assert.Equal("manual", t.Settings.GetString(StartupService.ModeKey)); Assert.Equal(0, Audits(t));
        var operations = m.Reads + u.Reads + m.Enables + u.Enables; Unknown(service.SetMode(TestData.Owner, "auto", Path));
        Assert.Equal(operations, m.Reads + u.Reads + m.Enables + u.Enables);
    }

    [Fact]
    public void PartialMachineAccessFailureCannotFallBackBesideItsNewEntry()
    {
        using var t = new TestDatabase(); var m = new Legacy(); var u = new Legacy();
        m.AfterEnable = _ => throw new System.Security.SecurityException("synthetic after write");
        Unknown(Service(t, m, u).SetMode(TestData.Owner, "auto", Path)); Assert.True(m.Present); Assert.Equal(0, u.Enables); Assert.Equal(0, Audits(t));
    }

    [Fact]
    public void ExistingMachineEntryBlocksDuplicateFallbackOnAccessRefusal()
    {
        using var t = new TestDatabase(); var m = new Legacy { Present = true }; var u = new Legacy();
        m.BeforeEnable = _ => throw new UnauthorizedAccessException("synthetic");
        Unknown(Service(t, m, u).SetMode(TestData.Owner, "auto", Path)); Assert.True(m.Present); Assert.Equal(0, u.Enables);
    }

    [Fact]
    public void UserCleanupRefusalIsNotMisclassifiedAsMachineEnableFallback()
    {
        using var t = new TestDatabase(); var m = new Legacy(); var u = new Legacy { Present = true };
        u.BeforeDisable = _ => throw new UnauthorizedAccessException("synthetic cleanup");
        var service = Service(t, m, u); Unknown(service.SetMode(TestData.Owner, "auto", Path));
        Assert.Equal(0, u.Enables); Assert.True(m.Present); Assert.True(u.Present); Assert.Equal(0, Audits(t)); Assert.Contains("both Windows", service.ScopeText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualDisableExceptionIsNotSwallowedAsSuccessfulSave(bool machine)
    {
        using var t = new TestDatabase(); var m = new Legacy { Present = true }; var u = new Legacy { Present = true };
        (machine ? m : u).BeforeDisable = _ => throw new InvalidOperationException("synthetic stuck");
        Unknown(Service(t, m, u).SetMode(TestData.Owner, "manual", "")); Assert.Equal(0, Audits(t)); Assert.True(u.Present);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialPresenceReadFailureHasNoMutationAndAllowsReviewedRetry(bool machine)
    {
        using var t = new TestDatabase(); var m = new Legacy(); var u = new Legacy(); var broken = machine ? m : u;
        broken.OnRead = _ => throw new InvalidOperationException("synthetic unreadable"); var service = Service(t, m, u);
        var failed = service.SetMode(TestData.Owner, "auto", Path); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed);
        Assert.Equal(0, m.Enables + u.Enables + m.Disables + u.Disables); broken.OnRead = null;
        Assert.True(service.SetMode(TestData.Owner, "auto", Path).Ok);
    }

    [Fact]
    public void LegacyKnownDatabaseRollbackDoesNotGuessOldWindowsCommandAndBlocksReplay()
    {
        using var t = new TestDatabase(); var m = new Legacy(); var u = new Legacy { Present = true }; var service = Service(t, m, u); AuditFault(t);
        Unknown(service.SetMode(TestData.Owner, "auto", Path)); Assert.True(m.Present); Assert.False(u.Present); Assert.Equal(1, m.Enables);
        Assert.Equal("manual", t.Settings.GetString(StartupService.ModeKey)); Assert.Equal(0, Audits(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_compat_audit;"));
        Unknown(service.SetMode(TestData.Owner, "auto", Path)); Assert.Equal(1, m.Enables); Assert.Equal(0, Audits(t));
    }

    [Fact]
    public void LegacyCommitUnknownDoesNotCompensateWindowsOrPermitBlindRetry()
    {
        using var t = new TestDatabase(); var m = new Legacy(); var u = new Legacy(); var service = Service(t, m, u); CommitFault(t);
        var failed = service.SetMode(TestData.Owner, "auto", Path); Unknown(failed); Assert.Contains("unconfirmed", failed.Error!);
        Assert.True(m.Present); Assert.Equal(0, m.Disables); Assert.Equal(0, Audits(t)); Unknown(service.SetMode(TestData.Owner, "auto", Path)); Assert.Equal(1, m.Enables);
    }

    [Fact]
    public void LoadedStaleCompatibilityFormNeverRebasesOrTouchesWindows()
    {
        using var t = new TestDatabase(); var form = Form(t); Assert.True(t.Settings.Set(StartupService.ModeKey, "protected", TestData.Owner).Ok);
        var m = new Legacy(); var u = new Legacy(); var failed = Service(t, m, u).SetMode(TestData.Owner, "auto", Path, form);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Equal(0, m.Reads + u.Reads + m.Enables + u.Enables);
    }

    [Fact]
    public void OwnerRevokedDuringWindowsWorkCannotCommitModeAndCannotTriggerGuessedRestore()
    {
        using var t = new TestDatabase(); var m = new Legacy(); var u = new Legacy();
        m.AfterEnable = _ => t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"));
        Unknown(Service(t, m, u).SetMode(TestData.Owner, "auto", Path)); Assert.True(m.Present); Assert.Equal(0, m.Disables); Assert.Equal(0, Audits(t));
    }

    [Fact]
    public void PresenceFailureAfterDatabaseCommitRetainsSavedTruthAndGuard()
    {
        using var t = new TestDatabase(); var m = new Legacy(); var u = new Legacy();
        m.OnRead = s => { if (s.Reads == 3) s.Present = false; }; var service = Service(t, m, u);
        var failed = service.SetMode(TestData.Owner, "auto", Path); Unknown(failed); Assert.Contains("SAVED.", failed.Error!);
        Assert.Equal("auto", t.Settings.GetString(StartupService.ModeKey)); Assert.Equal(1, Audits(t)); Unknown(service.SetMode(TestData.Owner, "auto", Path));
    }

    [Fact]
    public void CheckedDispatchKnownRollbackRestoresExactOldValuesAndAllowsReviewedRetry()
    {
        using var t = new TestDatabase(); var m = new Checked(); var u = new Checked(); var oldM = m.Entry; var oldU = u.Entry; var service = Service(t, m, u); AuditFault(t);
        var failed = service.SetMode(TestData.Owner, "auto", Path); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed);
        Assert.True(m.Entry.Matches(oldM)); Assert.True(u.Entry.Matches(oldU)); Assert.Equal(0, Audits(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_compat_audit;")); Assert.True(service.SetMode(TestData.Owner, "auto", Path).Ok);
    }

    [Fact]
    public void CheckedDispatchUnknownCommitRetainsTargetsAndBlocksSameServiceReplay()
    {
        using var t = new TestDatabase(); var m = new Checked(); var u = new Checked(); var service = Service(t, m, u); CommitFault(t);
        Unknown(service.SetMode(TestData.Owner, "auto", Path)); Assert.True(m.Entry.Matches(AutoStartEntry.Command(Path))); Assert.False(u.Entry.IsPresent);
        var writes = m.Writes + u.Writes; Unknown(service.SetMode(TestData.Owner, "auto", Path)); Assert.Equal(writes, m.Writes + u.Writes); Assert.Equal(0, Audits(t));
    }
}
