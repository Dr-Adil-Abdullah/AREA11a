using Area11.Core.Data;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Additive real-schema public runtime-policy cases; not Windows/WPF/taskbar proof.</summary>
public sealed class FocusGuardReadOutcomeTests
{
    private static FocusGuardService Guard(TestDatabase t) => new(t.Settings, t.Audit, t.Log);
    private static void Raw(TestDatabase t, string value) => t.Database.Tx(c => t.Database.Execute(c,
        "UPDATE settings SET value=@value WHERE key=@key;", new { key = FocusGuardService.GuardKey, value }));
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs;")));
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n", t.Database.Query<string>(c,
        "SELECT key || '=' || value || '@' || updated_at FROM settings ORDER BY key;")));
    private static SettingsService Other(TestDatabase t)
    {
        var db = new Database(t.Database.FilePath, pooling: false, durable: false); var audit = new AuditService(db, t.Log);
        return new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
    }
    private static void Status(FocusGuardStatus status, bool enabled, bool verified)
    {
        Assert.Equal(enabled, status.Enabled); Assert.Equal(verified, status.Verified);
        if (verified) Assert.Empty(status.Notice);
        else { Assert.Contains("could not be verified", status.Notice); Assert.Contains("not a Windows security lock", status.Notice); }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData(" \ntrue\t", true)]
    [InlineData(" false ", false)]
    public void FreshStrictReadAcceptsBooleansAndLegacyZeroOneWithoutCacheRefresh(string raw, bool enabled)
    {
        using var t = new TestDatabase(); Raw(t, raw); var before = State(t); var audit = Audits(t);
        var result = t.Settings.ReadCurrentFocusGuard(); Assert.True(result.Ok, result.Error); Assert.Equal(enabled, result.Value);
        Status(Guard(t).ReadStatus(), enabled, true); Assert.Equal(before, State(t)); Assert.Equal(audit, Audits(t));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"false\"")]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("0.0")]
    [InlineData("{}")]
    [InlineData("")]
    [InlineData("TRUE")]
    public void InvalidFirstReadIsUnknownAndTemporarilyOnNeverInventedOff(string raw)
    {
        using var t = new TestDatabase(); Raw(t, raw); var before = State(t); var audit = Audits(t);
        Assert.False(t.Settings.ReadCurrentFocusGuard().Ok); var guard = Guard(t);
        Status(guard.ReadStatus(), true, false); Assert.Contains("No verified setting yet", guard.ReadStatus().Notice);
        Assert.True(guard.Enabled); Assert.Equal(before, State(t)); Assert.Equal(audit, Audits(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadFailureRetainsLastVerifiedChoiceIncludingOff(bool enabled)
    {
        using var t = new TestDatabase(); Raw(t, enabled ? "true" : "false"); var guard = Guard(t); Status(guard.ReadStatus(), enabled, true);
        Raw(t, "synthetic-invalid"); var before = State(t); var audit = Audits(t);
        Status(guard.ReadStatus(), enabled, false); Assert.Contains("Keeping the last verified setting " + (enabled ? "ON." : "OFF."), guard.ReadStatus().Notice);
        Assert.Equal(enabled, guard.Enabled); Assert.Equal(before, State(t)); Assert.Equal(audit, Audits(t));
    }

    [Fact]
    public void MissingFirstRowDoesNotTreatShippedOffDefaultAsVerification()
    {
        using var t = new TestDatabase(); t.Database.Tx(c => t.Database.Execute(c, "DELETE FROM settings WHERE key='lockdown.focusGuard';"));
        Assert.False(t.Settings.GetBool(FocusGuardService.GuardKey)); Assert.False(t.Settings.ReadCurrentFocusGuard().Ok);
        Status(Guard(t).ReadStatus(), true, false);
        Assert.Equal(0L, t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM settings WHERE key='lockdown.focusGuard';"))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingLaterRowRetainsEvidenceAndDoesNotRepairTheRow(bool enabled)
    {
        using var t = new TestDatabase(); Raw(t, enabled ? "true" : "false"); var guard = Guard(t); Status(guard.ReadStatus(), enabled, true);
        t.Database.Tx(c => t.Database.Execute(c, "DELETE FROM settings WHERE key='lockdown.focusGuard';"));
        var state = State(t); Status(guard.ReadStatus(), enabled, false); Assert.Equal(state, State(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidReadRecoversFromInitialUnknownAndClearsWarning(bool enabled)
    {
        using var t = new TestDatabase(); Raw(t, "null"); var guard = Guard(t); Status(guard.ReadStatus(), true, false);
        Raw(t, enabled ? "true" : "false"); Status(guard.ReadStatus(), enabled, true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecoveryAdoptsNewOppositeChoiceRatherThanStickingToOldEvidence(bool enabled)
    {
        using var t = new TestDatabase(); Raw(t, enabled ? "true" : "false"); var guard = Guard(t); Status(guard.ReadStatus(), enabled, true);
        Raw(t, "{}"); Status(guard.ReadStatus(), enabled, false);
        Raw(t, enabled ? "false" : "true"); Status(guard.ReadStatus(), !enabled, true);
        Raw(t, "{}"); Status(guard.ReadStatus(), !enabled, false);
    }

    [Fact]
    public void CurrentWriterOnAnotherSettingsInstanceIsObservedWithoutReloadOrAuditFromReader()
    {
        using var t = new TestDatabase(); var guard = Guard(t); Status(guard.ReadStatus(), false, true); var other = Other(t);
        Assert.True(other.Set(FocusGuardService.GuardKey, true, TestData.Owner).Ok);
        Assert.False(t.Settings.GetBool(FocusGuardService.GuardKey)); var audits = Audits(t);
        Status(guard.ReadStatus(), true, true); Assert.False(t.Settings.GetBool(FocusGuardService.GuardKey)); Assert.Equal(audits, Audits(t));
        Assert.True(other.Set(FocusGuardService.GuardKey, false, TestData.Owner).Ok); Status(guard.ReadStatus(), false, true);
    }

    [Fact]
    public void RuntimeReadDoesNotRebaseOpenSettingsForm()
    {
        using var t = new TestDatabase(); var form = t.Settings.LoadEditForm(TestData.Owner, new[] { FocusGuardService.GuardKey }); Assert.True(form.Ok, form.Error);
        Assert.True(Other(t).Set(FocusGuardService.GuardKey, true, TestData.Owner).Ok); Status(Guard(t).ReadStatus(), true, true);
        Assert.False(form.Value!.GetBool(FocusGuardService.GuardKey));
        var conflict = t.Settings.SaveEditForm(new Dictionary<string, object?> { [FocusGuardService.GuardKey] = false }, TestData.Owner, form.Value);
        Assert.False(conflict.Ok); Assert.True(conflict.NoCommitConfirmed); Assert.Contains("changed", conflict.Error!);
    }

    [Fact]
    public void NewRuntimeServiceCannotBorrowAnotherInstancesLastVerifiedOff()
    {
        using var t = new TestDatabase(); var first = Guard(t); Status(first.ReadStatus(), false, true); Raw(t, "null");
        Status(first.ReadStatus(), false, false); Status(Guard(t).ReadStatus(), true, false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealSqlReadFailureRetainsChoiceThenRecovers(bool enabled)
    {
        using var t = new TestDatabase(); Raw(t, enabled ? "true" : "false"); var guard = Guard(t); Status(guard.ReadStatus(), enabled, true);
        t.Database.Tx(c => t.Database.Execute(c, "ALTER TABLE settings RENAME TO synthetic_focus_unavailable;"));
        try { Assert.False(t.Settings.ReadCurrentFocusGuard().Ok); Status(guard.ReadStatus(), enabled, false); }
        finally { t.Database.Tx(c => t.Database.Execute(c, "ALTER TABLE synthetic_focus_unavailable RENAME TO settings;")); }
        Status(guard.ReadStatus(), enabled, true);
    }

    [Fact]
    public void RealSqlReadFailureBeforeFirstVerificationIsTemporaryOn()
    {
        using var t = new TestDatabase(); var guard = Guard(t);
        t.Database.Tx(c => t.Database.Execute(c, "ALTER TABLE settings RENAME TO synthetic_focus_unavailable;"));
        try { Status(guard.ReadStatus(), true, false); }
        finally { t.Database.Tx(c => t.Database.Execute(c, "ALTER TABLE synthetic_focus_unavailable RENAME TO settings;")); }
        Status(guard.ReadStatus(), false, true);
    }

    [Fact]
    public void UnrelatedBookkeepingCannotChangeGuardEvidenceOrCreateAudit()
    {
        using var t = new TestDatabase(); var guard = Guard(t); Status(guard.ReadStatus(), false, true); var before = Audits(t);
        Assert.True(t.Settings.SetUiOnly("session.lastSeen", "Synthetic heartbeat").Ok); Status(guard.ReadStatus(), false, true);
        Assert.False(t.Settings.SetUiOnly(FocusGuardService.GuardKey, true).Ok); Status(guard.ReadStatus(), false, true); Assert.Equal(before, Audits(t));
    }

    [Fact]
    public void WriterStillRequiresCurrentOwnerRegardlessOfRuntimeFallback()
    {
        using var t = new TestDatabase(); Raw(t, "null"); Status(Guard(t).ReadStatus(), true, false); var before = State(t); var audit = Audits(t);
        Assert.False(t.Settings.Set(FocusGuardService.GuardKey, false, TestData.AddUser(t, "manager")).Ok);
        Assert.Equal(before, State(t)); Assert.Equal(audit, Audits(t));
    }
}
