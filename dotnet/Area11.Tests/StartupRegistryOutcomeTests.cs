using System.Security;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Public checked service + exact-value fake stores; never real Windows registry/WPF execution.</summary>
public sealed class StartupRegistryOutcomeTests
{
    private const string Path = @"C:\Synthetic\New Area11.exe";
    private static AutoStartEntry Old(string command = "\"C:\\Synthetic\\Old.exe\" --retained") =>
        AutoStartEntry.Present(AutoStartValueKind.String, command);
    private sealed class Store : ICheckedAutoStartStore
    {
        public AutoStartEntry Entry { get; set; } = AutoStartEntry.Missing;
        public int Captures { get; private set; }
        public int Attempts { get; private set; }
        public int Writes { get; private set; }
        public Action<Store>? OnCapture { get; set; }
        public Action<Store>? BeforeCompare { get; set; }
        public Action<Store>? BeforeWrite { get; set; }
        public Action<Store>? AfterWrite { get; set; }
        public AutoStartEntry Capture() { Captures++; OnCapture?.Invoke(this); return Entry; }
        public void Replace(AutoStartEntry expected, AutoStartEntry replacement)
        {
            Attempts++; BeforeCompare?.Invoke(this);
            if (!Capture().Matches(expected)) throw new InvalidOperationException("Synthetic changed entry");
            if (expected.Matches(replacement)) return;
            BeforeWrite?.Invoke(this); Entry = replacement; Writes++; AfterWrite?.Invoke(this);
            if (!Capture().Matches(replacement)) throw new InvalidOperationException("Synthetic readback failure");
        }
        public void Enable(string path) => throw new InvalidOperationException("Checked route must not use legacy Enable.");
        public void Disable() => throw new InvalidOperationException("Checked route must not use legacy Disable.");
        public bool IsEnabled() => throw new InvalidOperationException("Checked route must not collapse reads to a bool.");
    }
    private sealed class LegacyStore : IAutoStartStore
    {
        public int Mutations { get; private set; }
        public void Enable(string path) => Mutations++;
        public void Disable() => Mutations++;
        public bool IsEnabled() => false;
    }
    private static StartupService Service(TestDatabase t, IAutoStartStore machine, IAutoStartStore user) =>
        new(t.Settings, new AuthService(t.Database, t.Audit, t.Settings, t.Log), TestData.Permissions(t), t.Audit, t.Log, machine, user);
    private static SettingsEditForm Form(TestDatabase t)
    {
        var opened = t.Settings.LoadEditForm(TestData.Owner, new[] { StartupService.ModeKey });
        Assert.True(opened.Ok, opened.Error); return opened.Value!;
    }
    private static SettingsService Other(TestDatabase t)
    {
        var db = new Database(t.Database.FilePath, pooling: false, durable: false); var audit = new AuditService(db, t.Log);
        return new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
    }
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c,
        "SELECT COUNT(*) FROM audit_logs WHERE action='settings_change' AND entity_id='startup.mode';")));
    private static void AuditFault(TestDatabase t) => t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TRIGGER synthetic_startup_audit BEFORE INSERT ON audit_logs WHEN NEW.entity_id='startup.mode'
BEGIN SELECT RAISE(ABORT,'synthetic startup audit failure'); END;"));
    private static void Refused(Area11.Core.Utils.ServiceResult result, bool known)
    { Assert.False(result.Ok); Assert.Equal(known, result.NoCommitConfirmed); Assert.Null(result.SettingsEdit); }

    [Theory]
    [InlineData("manual")]
    [InlineData("auto")]
    [InlineData("protected")]
    public void CheckedModeVerifiesBothEntriesAndReturnsExactReusableSettingsReceipt(string mode)
    {
        using var t = new TestDatabase(); var m = new Store { Entry = Old() }; var u = new Store { Entry = Old("old user") };
        var before = Audits(t); var saved = Service(t, m, u).SetModeChecked(TestData.Owner, mode, Path, Form(t));
        Assert.True(saved.Ok, saved.Error); Assert.Equal(mode, saved.SettingsEdit!.GetString(StartupService.ModeKey));
        Assert.Equal(mode, t.Settings.GetString(StartupService.ModeKey)); Assert.Equal(before + 1, Audits(t));
        Assert.True(m.Entry.Matches(mode == "manual" ? AutoStartEntry.Missing : AutoStartEntry.Command(Path)));
        Assert.False(u.Entry.IsPresent); Assert.Empty(saved.Warnings);
        var next = t.Settings.SaveEditForm(new Dictionary<string, object?> { [StartupService.ModeKey] = "manual" }, TestData.Owner, saved.SettingsEdit);
        Assert.True(next.Ok, next.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AccessRefusalFallsBackOnlyBesideVerifiedAbsentMachineEntry(bool security)
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store();
        m.BeforeWrite = _ => { if (security) throw new SecurityException("synthetic"); throw new UnauthorizedAccessException("synthetic"); };
        var saved = Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t));
        Assert.True(saved.Ok, saved.Error); Assert.False(m.Entry.IsPresent); Assert.True(u.Entry.Matches(AutoStartEntry.Command(Path)));
        Assert.Contains("this Windows account only", Assert.Single(saved.Warnings));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingMachineEntryPreventsDuplicateFallbackEvenOnAccessRefusal(bool security)
    {
        using var t = new TestDatabase(); var old = Old(); var m = new Store { Entry = old }; var u = new Store();
        m.BeforeWrite = _ => { if (security) throw new SecurityException("synthetic"); throw new UnauthorizedAccessException("synthetic"); };
        var failed = Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t));
        Refused(failed, true); Assert.True(m.Entry.Matches(old)); Assert.Equal(0, u.Attempts); Assert.Equal(0, Audits(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnreadableSnapshotNeverInventsAbsenceOrStartsMutation(bool machine)
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store();
        (machine ? m : u).OnCapture = _ => throw new UnauthorizedAccessException("synthetic read");
        var failed = Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t));
        Refused(failed, true); Assert.Equal(0, m.Attempts + u.Attempts); Assert.Equal(0, Audits(t));
        Assert.Contains("could not be read", Service(t, m, u).ScopeText);
    }

    [Fact]
    public void NullFormCannotBecomeAnImperativeStartupWrite()
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store();
        Refused(Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, null), true);
        Assert.Equal(0, m.Captures + u.Captures); Assert.Equal(0, Audits(t));
    }

    [Fact]
    public void BooleanOnlyHostIsRefusedBeforeMutation()
    {
        using var t = new TestDatabase(); var m = new LegacyStore(); var u = new LegacyStore();
        Refused(Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t)), true);
        Assert.Equal(0, m.Mutations + u.Mutations); Assert.Equal(0, Audits(t));
    }

    [Fact]
    public void StaleFormIsRefusedBeforeEvenCapturingWindows()
    {
        using var t = new TestDatabase(); var form = Form(t); var m = new Store(); var u = new Store();
        Assert.True(Other(t).Set(StartupService.ModeKey, "protected", TestData.Owner).Ok); var audits = Audits(t);
        Refused(Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, form), true);
        Assert.Equal(0, m.Captures + u.Captures); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void NonOwnerCannotBorrowOwnersLoadedForm()
    {
        using var t = new TestDatabase(); var form = Form(t); var m = new Store(); var u = new Store();
        Refused(Service(t, m, u).SetModeChecked(TestData.Cashier(t), "auto", Path, form), true);
        Assert.Equal(0, m.Captures + u.Captures);
    }

    [Theory]
    [InlineData("invalid", "synthetic.exe")]
    [InlineData("auto", "")]
    [InlineData("auto", "\"quoted.exe\"")]
    public void InvalidModeOrPathDoesNotTouchWindows(string mode, string path)
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store();
        Refused(Service(t, m, u).SetModeChecked(TestData.Owner, mode, path, Form(t)), true);
        Assert.Equal(0, m.Attempts + u.Attempts); Assert.Equal(0, Audits(t));
    }

    private static AutoStartEntry TypedOld(string kind) => kind switch
    {
        "expand" => AutoStartEntry.Present(AutoStartValueKind.ExpandString, "%SYNTHETIC_HOME%\\Old.exe --args"),
        "binary" => AutoStartEntry.Present(AutoStartValueKind.Binary, new byte[] { 0, 1, 255 }),
        "none" => AutoStartEntry.Present(AutoStartValueKind.None, new byte[] { 1, 2 }),
        "multi" => AutoStartEntry.Present(AutoStartValueKind.MultiString, new[] { "Synthetic old", "--args" }),
        "dword" => AutoStartEntry.Present(AutoStartValueKind.DWord, -1),
        "qword" => AutoStartEntry.Present(AutoStartValueKind.QWord, long.MinValue),
        _ => Old()
    };

    [Theory]
    [InlineData("string")]
    [InlineData("expand")]
    [InlineData("binary")]
    [InlineData("none")]
    [InlineData("multi")]
    [InlineData("dword")]
    [InlineData("qword")]
    public void KnownDatabaseRollbackRestoresOriginalRawValueKindAndUserCommand(string kind)
    {
        using var t = new TestDatabase(); var old = TypedOld(kind); var userOld = TypedOld("expand");
        var m = new Store { Entry = old }; var u = new Store { Entry = userOld }; AuditFault(t);
        var failed = Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t));
        Refused(failed, true); Assert.True(m.Entry.Matches(old)); Assert.True(u.Entry.Matches(userOld));
        Assert.Equal("manual", t.Settings.GetString(StartupService.ModeKey)); Assert.Equal(0, Audits(t));
        Assert.Equal(2, m.Writes); Assert.Equal(2, u.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualDisableRefusalCannotBeSwallowedAsSuccess(bool machine)
    {
        using var t = new TestDatabase(); var old = Old(); var m = new Store { Entry = old }; var u = new Store { Entry = old };
        (machine ? m : u).BeforeWrite = _ => throw new UnauthorizedAccessException("synthetic disable");
        Refused(Service(t, m, u).SetModeChecked(TestData.Owner, "manual", "", Form(t)), true);
        Assert.True(m.Entry.Matches(old)); Assert.True(u.Entry.Matches(old)); Assert.Equal(0, Audits(t));
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("manual")]
    public void UnknownCommitOutcomeNeverCompensatesWindows(string mode)
    {
        using var t = new TestDatabase(); Assert.True(t.Settings.Set(StartupService.ModeKey, "protected", TestData.Owner).Ok);
        var m = new Store { Entry = Old() }; var u = new Store { Entry = Old("old user") }; var audits = Audits(t);
        t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_startup_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_startup_commit_audit AFTER INSERT ON audit_logs WHEN NEW.entity_id='startup.mode'
BEGIN INSERT INTO synthetic_startup_commit(parent_id) VALUES(-99999); END;"));
        var failed = Service(t, m, u).SetModeChecked(TestData.Owner, mode, Path, Form(t));
        Refused(failed, false); Assert.Contains("not rolled back", failed.Error!);
        Assert.True(m.Entry.Matches(mode == "manual" ? AutoStartEntry.Missing : AutoStartEntry.Command(Path)));
        Assert.False(u.Entry.IsPresent); Assert.Equal(1, m.Writes); Assert.Equal(1, u.Writes);
        Assert.Equal("protected", t.Settings.GetString(StartupService.ModeKey)); Assert.Equal(audits, Audits(t));
    }

    [Fact]
    public void PostCommitReadbackFailureSaysSavedAndDoesNotRestoreOldWindowsValues()
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store();
        m.OnCapture = _ => { if (t.Settings.GetString(StartupService.ModeKey) == "auto") throw new InvalidOperationException("synthetic post-commit read"); };
        var failed = Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t));
        Refused(failed, false); Assert.StartsWith("SAVED.", failed.Error!);
        Assert.True(m.Entry.Matches(AutoStartEntry.Command(Path))); Assert.Equal(1, m.Writes); Assert.Equal(1, Audits(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalDatabaseCheckStillRejectsChangedModeOrRevokedActor(bool revoke)
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store();
        m.AfterWrite = store =>
        {
            if (store.Writes != 1) return;
            if (revoke) t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"));
            else Assert.True(Other(t).Set(StartupService.ModeKey, "protected", TestData.Owner).Ok);
        };
        var failed = Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t));
        // Owner prevalidation may refuse without an observed Tx; conservatively unknown, never infer from wording.
        Assert.False(failed.Ok); Assert.Null(failed.SettingsEdit);
        if (!revoke) { Assert.True(failed.NoCommitConfirmed); Assert.False(m.Entry.IsPresent); Assert.Equal("protected", t.Settings.LoadEditForm(TestData.Owner, new[] { StartupService.ModeKey }).Value!.GetString(StartupService.ModeKey)); }
        else { Assert.False(failed.NoCommitConfirmed); Assert.True(m.Entry.IsPresent); }
    }

    [Fact]
    public void ConcurrentWindowsValueIsNotOverwrittenByCompensation()
    {
        using var t = new TestDatabase(); var concurrent = Old("synthetic concurrent"); var m = new Store(); var u = new Store();
        m.BeforeCompare = store => { if (store.Attempts == 1) store.Entry = concurrent; };
        Refused(Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t)), false);
        Assert.True(m.Entry.Matches(concurrent)); Assert.Equal(0, m.Writes + u.Writes); Assert.Equal(0, Audits(t));
    }

    [Fact]
    public void FailedRestorationIsUnknownAndNeverClaimsNothingChanged()
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store(); AuditFault(t);
        m.BeforeWrite = store => { if (store.Writes == 1) throw new UnauthorizedAccessException("synthetic restore refusal"); };
        var failed = Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t));
        Refused(failed, false); Assert.Contains("restoration is unconfirmed", failed.Error!);
        Assert.DoesNotContain("Nothing was changed", failed.Error!); Assert.True(m.Entry.IsPresent); Assert.Equal(0, Audits(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MutationThenExceptionIsConditionallyRestoredNotAssumedUntouched(bool machine)
    {
        using var t = new TestDatabase(); var m = new Store(); var old = Old(); var u = new Store { Entry = old };
        (machine ? m : u).AfterWrite = store => { if (store.Writes == 1) throw new InvalidOperationException("synthetic partial mutation"); };
        Refused(Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t)), true);
        Assert.False(m.Entry.IsPresent); Assert.True(u.Entry.Matches(old)); Assert.Equal(0, Audits(t));
    }

    [Fact]
    public void UserCleanupFailurePreservesConcurrentMachineValueInsteadOfBlindRestore()
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store { Entry = Old() }; var concurrent = Old("synthetic other app path");
        u.AfterWrite = store => { if (store.Writes == 1) m.Entry = concurrent; };
        Refused(Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t)), false);
        Assert.True(m.Entry.Matches(concurrent)); Assert.True(u.Entry.IsPresent); Assert.Equal(0, Audits(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotsDefensivelyCopyMutableArraysOnInputAndOutput(bool strings)
    {
        if (strings)
        {
            var input = new[] { "original" }; var entry = AutoStartEntry.Present(AutoStartValueKind.MultiString, input);
            input[0] = "changed"; ((string[])entry.Value!)[0] = "changed again";
            Assert.Equal("original", ((string[])entry.Value!)[0]);
        }
        else
        {
            var input = new byte[] { 1, 2 }; var entry = AutoStartEntry.Present(AutoStartValueKind.Binary, input);
            input[0] = 99; ((byte[])entry.Value!)[0] = 88; Assert.Equal((byte)1, ((byte[])entry.Value!)[0]);
        }
    }

    [Fact]
    public void ValueKindAndPresenceAreNotCollapsedToCommandOrBoolean()
    {
        Assert.False(AutoStartEntry.Present(AutoStartValueKind.String, "x").Matches(AutoStartEntry.Present(AutoStartValueKind.ExpandString, "x")));
        Assert.False(AutoStartEntry.Missing.Matches(AutoStartEntry.Present(AutoStartValueKind.String, "")));
        Assert.True(TypedOld("multi").Matches(TypedOld("multi"))); Assert.True(TypedOld("binary").Matches(TypedOld("binary")));
    }

    [Fact]
    public void AccessExceptionAfterMutationDoesNotJustifyFallback()
    {
        using var t = new TestDatabase(); var m = new Store(); var u = new Store();
        m.AfterWrite = store => { if (store.Writes == 1) throw new UnauthorizedAccessException("synthetic partial denied write"); };
        Refused(Service(t, m, u).SetModeChecked(TestData.Owner, "auto", Path, Form(t)), true);
        Assert.False(m.Entry.IsPresent); Assert.Equal(0, u.Attempts); Assert.Equal(2, m.Writes); Assert.Equal(0, Audits(t));
    }

    [Fact]
    public void ScopeReadDoesNotAdvertiseMalformedValueAsWorkingCommand()
    {
        using var t = new TestDatabase(); var m = new Store { Entry = TypedOld("binary") }; var u = new Store();
        Assert.Contains("not a usable command", Service(t, m, u).ScopeText);
        m.Entry = Old(); u.Entry = Old(); Assert.Contains("Both Windows", Service(t, m, u).ScopeText);
    }

    [Fact]
    public void UnsupportedRegistryKindOrMismatchedValueFailsClosed()
    {
        Assert.Throws<ArgumentException>(() => AutoStartEntry.Present((AutoStartValueKind)999, "synthetic"));
        Assert.Throws<ArgumentException>(() => AutoStartEntry.Present(AutoStartValueKind.DWord, "synthetic"));
    }
}
