using System.Text.Json;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Public-service/provider cases; additive to FocusGuardTests. UNRUN without .NET.</summary>
public sealed class SessionTelemetryOutcomeTests
{
    private static FocusGuardService Guard(TestDatabase t) => new(t.Settings, t.Audit, t.Log);
    private static void Sql(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static string Raw(TestDatabase t, string key) => t.Database.Read(c =>
        t.Database.QuerySingle<string>(c, "SELECT value FROM settings WHERE key=@key;", new { key }));
    private static void SetRaw(TestDatabase t, string key, string value) => t.Database.Tx(c =>
    { t.Database.Execute(c, "UPDATE settings SET value=@value WHERE key=@key;", new { key, value }); });
    private static string State(TestDatabase t) => string.Join("|", new[] { "session.lastStart", "session.lastSeen", "session.lastUser", "session.cleanExit" }.Select(k => Raw(t, k)));
    private static long Audits(TestDatabase t, string action) => t.Database.Read(c => Convert.ToInt64(
        t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs WHERE action=@action;", new { action }) ?? 0L));
    private static void Incomplete(TestDatabase t)
    {
        SetRaw(t, "session.lastStart", JsonSerializer.Serialize("2026-11-10 10:00:00"));
        SetRaw(t, "session.lastSeen", JsonSerializer.Serialize("2026-11-10 10:30:00"));
        SetRaw(t, "session.lastUser", JsonSerializer.Serialize("Ali"));
        SetRaw(t, "session.cleanExit", "false");
    }
    private static void RejectSetting(TestDatabase t, string key) => Sql(t,
        "CREATE TRIGGER reject_telemetry BEFORE UPDATE ON settings WHEN NEW.key='" + key + "' BEGIN SELECT RAISE(ABORT,'telemetry fault'); END;");
    private static void CommitFault(TestDatabase t) => Sql(t, @"
CREATE TABLE telemetry_probe_parent(id INTEGER PRIMARY KEY);
CREATE TABLE telemetry_probe_child(id INTEGER REFERENCES telemetry_probe_parent(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER fail_telemetry_commit AFTER UPDATE ON settings WHEN NEW.key='session.lastSeen'
BEGIN INSERT INTO telemetry_probe_child(id) VALUES(1); END;");

    [Fact]
    public void FirstStartupWritesCoherentMarkersWithoutAuditAndClearsPreviousUser()
    {
        using var t = new TestDatabase(); SetRaw(t, "session.lastUser", "\"Old operator\""); var g = Guard(t);
        var result = g.CheckPreviousRunChecked(); Assert.True(result.Ok, result.Error); Assert.Null(result.Value);
        Assert.Equal(Raw(t, "session.lastStart"), Raw(t, "session.lastSeen"));
        Assert.Equal("\"\"", Raw(t, "session.lastUser")); Assert.Equal("false", Raw(t, "session.cleanExit"));
        Assert.Equal(0, Audits(t, "session_killed")); Assert.Empty(g.TelemetryNotice);
        Assert.Equal(string.Empty, t.Settings.GetString("session.lastUser")); Assert.False(t.Settings.GetBool("session.cleanExit"));
    }

    [Fact]
    public void StartupReadsCurrentRowsNotCachedDefaultsAndQualifiesSharedEvidence()
    {
        using var t = new TestDatabase(); var g = Guard(t); Incomplete(t);
        Assert.True(t.Settings.GetBool("session.cleanExit"));
        var result = g.CheckPreviousRunChecked(); Assert.True(result.Ok, result.Error);
        Assert.Contains("Ali ended without closing", result.Value!); Assert.Contains("Unverified shared marker", result.Value!);
        Assert.Contains("another counter may still be active", result.Value!); Assert.Contains("not proof", result.Value!);
        Assert.Equal(1, Audits(t, "session_killed")); Assert.Equal("\"\"", Raw(t, "session.lastUser"));
    }

    [Theory]
    [InlineData("session.lastStart")]
    [InlineData("session.lastSeen")]
    [InlineData("session.lastUser")]
    [InlineData("session.cleanExit")]
    public void AnyStartupStampFailureRollsBackAllStampsAndPreviousAudit(string key)
    {
        using var t = new TestDatabase(); Incomplete(t); var before = State(t); RejectSetting(t, key); var g = Guard(t);
        var result = g.CheckPreviousRunChecked(); Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed);
        Assert.Null(result.Value); Assert.Equal(before, State(t)); Assert.Equal(0, Audits(t, "session_killed"));
        Assert.Contains("not saved", result.Error!); Assert.NotEmpty(g.TelemetryNotice);
        Assert.False(g.HeartbeatChecked("Ali").Ok); Assert.False(g.MarkCleanExitChecked().Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void StartupAuditFailureDoesNotConsumeThePreviousMarker()
    {
        using var t = new TestDatabase(); Incomplete(t); var before = State(t); var g = Guard(t);
        Sql(t, "CREATE TRIGGER reject_audit BEFORE INSERT ON audit_logs WHEN NEW.action='session_killed' BEGIN SELECT RAISE(ABORT,'audit fault'); END;");
        var failed = g.CheckPreviousRunChecked(); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed);
        Assert.Equal(before, State(t)); Assert.Equal(0, Audits(t, "session_killed"));
        Sql(t, "DROP TRIGGER reject_audit;"); var explicitAttempt = g.CheckPreviousRunChecked();
        Assert.True(explicitAttempt.Ok, explicitAttempt.Error); Assert.Contains("Ali", explicitAttempt.Value!);
        Assert.Equal(1, Audits(t, "session_killed")); Assert.NotEmpty(g.TelemetryNotice);
    }

    [Theory]
    [InlineData("session.cleanExit", "null")]
    [InlineData("session.cleanExit", "\"false\"")]
    [InlineData("session.cleanExit", "1")]
    [InlineData("session.lastStart", "null")]
    [InlineData("session.lastSeen", "{}")]
    [InlineData("session.lastUser", "false")]
    public void MalformedMarkersFailWithoutRepairOrInventedDefaults(string key, string raw)
    {
        using var t = new TestDatabase(); SetRaw(t, key, raw); var before = State(t); var g = Guard(t);
        var result = g.CheckPreviousRunChecked(); Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed);
        Assert.Equal(before, State(t)); Assert.Equal(0, Audits(t, "session_killed")); Assert.NotEmpty(g.TelemetryNotice);
    }

    [Fact]
    public void MissingMarkerFailsWithoutUpsertingAReplacement()
    {
        using var t = new TestDatabase(); Sql(t, "DELETE FROM settings WHERE key='session.cleanExit';"); var g = Guard(t);
        var result = g.CheckPreviousRunChecked(); Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed);
        Assert.Null(t.Database.Read(c => t.Database.Scalar(c, "SELECT value FROM settings WHERE key='session.cleanExit';")));
        Assert.Equal(0, Audits(t, "session_killed"));
    }

    [Theory]
    [InlineData(" Ali ", "Ali")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void HeartbeatAtomicallyPublishesSeenAndTrimmedOrClearedUser(string? name, string expected)
    {
        using var t = new TestDatabase(); var g = Guard(t); Assert.True(g.CheckPreviousRunChecked().Ok);
        Assert.True(g.HeartbeatChecked("Old").Ok); var result = g.HeartbeatChecked(name); Assert.True(result.Ok, result.Error);
        Assert.Equal(JsonSerializer.Serialize(expected), Raw(t, "session.lastUser"));
        Assert.Equal(expected, t.Settings.GetString("session.lastUser")); Assert.False(t.Settings.GetBool("session.cleanExit"));
        Assert.Equal(0, Audits(t, "session_killed"));
    }

    [Fact]
    public void HeartbeatPairFailureRollsBackAndDoesNotPublishCacheOrAllowCleanStamp()
    {
        using var t = new TestDatabase(); var g = Guard(t); Assert.True(g.CheckPreviousRunChecked().Ok);
        SetRaw(t, "session.lastSeen", "\"old\""); var before = State(t); RejectSetting(t, "session.lastUser");
        var failed = g.HeartbeatChecked("Ali"); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed);
        Assert.Equal(before, State(t)); Assert.Equal(string.Empty, t.Settings.GetString("session.lastUser"));
        Assert.False(g.MarkCleanExitChecked().Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void ConfirmedCleanExitUpdatesSeenAndStopsLateHeartbeat()
    {
        using var t = new TestDatabase(); var g = Guard(t); Assert.True(g.CheckPreviousRunChecked().Ok);
        Assert.True(g.HeartbeatChecked("Ali").Ok); Assert.True(g.MarkCleanExitChecked().Ok);
        Assert.Equal("true", Raw(t, "session.cleanExit")); var before = State(t);
        Assert.False(g.HeartbeatChecked("Late").Ok); Assert.Equal(before, State(t));
        var next = g.CheckPreviousRunChecked(); Assert.True(next.Ok); Assert.Null(next.Value); Assert.Equal(0, Audits(t, "session_killed"));
    }

    [Fact]
    public void CleanExitFailureDoesNotClaimCleanOrPartiallyUpdateSeen()
    {
        using var t = new TestDatabase(); var g = Guard(t); Assert.True(g.CheckPreviousRunChecked().Ok);
        SetRaw(t, "session.lastSeen", "\"old\""); var before = State(t); RejectSetting(t, "session.cleanExit");
        var result = g.MarkCleanExitChecked(); Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed);
        Assert.Equal(before, State(t)); Assert.False(t.Settings.GetBool("session.cleanExit")); Assert.NotEmpty(g.TelemetryNotice);
    }

    [Fact]
    public void UnstartedHeartbeatAndExitDoNotInventACleanRun()
    {
        using var t = new TestDatabase(); var g = Guard(t); var before = State(t);
        Assert.False(g.HeartbeatChecked("Ali").Ok); Assert.False(g.MarkCleanExitChecked().Ok);
        Assert.Equal(before, State(t)); Assert.NotEmpty(g.TelemetryNotice);
    }

    [Fact]
    public void AwayFailureIsObservableInsteadOfStandaloneSwallowingAndDoesNotReplay()
    {
        using var t = new TestDatabase(); var g = Guard(t);
        Sql(t, "CREATE TRIGGER reject_away BEFORE INSERT ON audit_logs WHEN NEW.action='screen_left' BEGIN SELECT RAISE(ABORT,'away fault'); END;");
        var result = g.RecordAwayChecked(null, "2026-11-10 10:00:00", "2026-11-10 10:01:00");
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Null(result.Value); Assert.NotEmpty(g.TelemetryNotice);
        Assert.Equal(0, Audits(t, "screen_left")); Sql(t, "DROP TRIGGER reject_away;"); Assert.Equal(0, Audits(t, "screen_left"));
        Assert.True(g.RecordAwayChecked(null, "2026-11-10 11:00:00", "2026-11-10 11:01:00").Ok);
        Assert.Equal(1, Audits(t, "screen_left")); Assert.NotEmpty(g.TelemetryNotice);
    }

    [Fact]
    public void SuccessfulAwayReturnsConfirmedLineAndDoesNotChangeStamps()
    {
        using var t = new TestDatabase(); var g = Guard(t); var before = State(t);
        var result = g.RecordAwayChecked(null, "2026-11-10 10:00:00", "2026-11-10 10:00:03");
        Assert.True(result.Ok, result.Error); Assert.Contains("Someone left the screen for 3 sec", result.Value!);
        Assert.Equal(1, Audits(t, "screen_left")); Assert.Equal(before, State(t));
    }

    [Fact]
    public void CommitFailureIsConservativelyUnknownAndCannotBeReplayedByLaterCalls()
    {
        using var t = new TestDatabase(); var g = Guard(t); Incomplete(t); var before = State(t); CommitFault(t);
        var result = g.CheckPreviousRunChecked(); Assert.False(result.Ok); Assert.False(result.NoCommitConfirmed);
        Assert.Contains("unverified", result.Error!); Assert.Equal(before, State(t)); Assert.Equal(0, Audits(t, "session_killed"));
        Sql(t, "DROP TRIGGER fail_telemetry_commit;");
        var blocked = g.CheckPreviousRunChecked(); Assert.False(blocked.Ok); Assert.True(blocked.NoCommitConfirmed);
        Assert.False(g.HeartbeatChecked("Ali").Ok); Assert.False(g.MarkCleanExitChecked().Ok);
        Assert.False(g.RecordAwayChecked(null, "a", "b").Ok); Assert.Equal(before, State(t));
        Assert.Equal(0, Audits(t, "session_killed")); Assert.NotEmpty(g.TelemetryNotice);
    }

    [Fact]
    public void UnknownHeartbeatStopsFollowingStampsEvenAfterTheProviderFaultIsRemoved()
    {
        using var t = new TestDatabase(); var g = Guard(t); Assert.True(g.CheckPreviousRunChecked().Ok);
        var before = State(t); CommitFault(t); var result = g.HeartbeatChecked("Ali");
        Assert.False(result.Ok); Assert.False(result.NoCommitConfirmed); Assert.Equal(before, State(t));
        Sql(t, "DROP TRIGGER fail_telemetry_commit;"); Assert.False(g.HeartbeatChecked("Ali").Ok);
        Assert.False(g.MarkCleanExitChecked().Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void UnknownAwayCommitCannotBeReplayedAndDoesNotPretendToHaveAReceipt()
    {
        using var t = new TestDatabase(); var g = Guard(t); CommitFault(t);
        Sql(t, @"CREATE TRIGGER fail_away_commit AFTER INSERT ON audit_logs WHEN NEW.action='screen_left'
BEGIN INSERT INTO telemetry_probe_child(id) VALUES(1); END;");
        var result = g.RecordAwayChecked(null, "a", "b"); Assert.False(result.Ok); Assert.False(result.NoCommitConfirmed);
        Assert.Null(result.Value); Assert.Equal(0, Audits(t, "screen_left"));
        Sql(t, "DROP TRIGGER fail_away_commit;"); Assert.False(g.RecordAwayChecked(null, "a", "b").Ok);
        Assert.Equal(0, Audits(t, "screen_left")); Assert.NotEmpty(g.TelemetryNotice);
    }

    [Theory]
    [InlineData("startup")]
    [InlineData("away")]
    public void SilentlyIgnoredAuditIsNotReportedAsSaved(string operation)
    {
        using var t = new TestDatabase(); Incomplete(t); var before = State(t); var g = Guard(t);
        Sql(t, "CREATE TRIGGER ignore_telemetry_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(IGNORE); END;");
        var result = operation == "startup" ? g.CheckPreviousRunChecked() : g.RecordAwayChecked(null, "a", "b");
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Null(result.Value);
        Assert.Equal(before, State(t)); Assert.Equal(0, Audits(t, "session_killed")); Assert.Equal(0, Audits(t, "screen_left"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IgnoredOrRewrittenStampDoesNotProduceAFalseReceipt(bool rewrite)
    {
        using var t = new TestDatabase(); Incomplete(t); var before = State(t); var g = Guard(t);
        Sql(t, rewrite
            ? @"CREATE TRIGGER rewrite_telemetry AFTER UPDATE ON settings WHEN NEW.key='session.cleanExit'
BEGIN UPDATE settings SET value='""changed""' WHERE key='session.lastUser'; END;"
            : "CREATE TRIGGER ignore_telemetry BEFORE UPDATE ON settings WHEN NEW.key='session.lastSeen' BEGIN SELECT RAISE(IGNORE); END;");
        var result = g.CheckPreviousRunChecked(); Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed);
        Assert.Equal(before, State(t)); Assert.Equal(0, Audits(t, "session_killed"));
    }

    [Fact]
    public void TelemetryDoesNotAlterSeparateStartupOrFocusPolicies()
    {
        using var t = new TestDatabase(); var g = Guard(t); var focus = Raw(t, FocusGuardService.GuardKey);
        var startup = Raw(t, StartupService.ModeKey); RejectSetting(t, "session.cleanExit");
        Assert.False(g.CheckPreviousRunChecked().Ok); Assert.Equal(focus, Raw(t, FocusGuardService.GuardKey));
        Assert.Equal(startup, Raw(t, StartupService.ModeKey)); Assert.True(g.ReadStatus().Verified);
    }
}
