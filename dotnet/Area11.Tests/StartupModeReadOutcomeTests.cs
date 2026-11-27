using Area11.Core.Data;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>D-119/C real-schema runtime read cases; no Windows/WPF/native timing proof.</summary>
public sealed class StartupModeReadOutcomeTests
{
    private sealed class Store : IAutoStartStore
    { public void Enable(string path) => throw new InvalidOperationException(); public void Disable() => throw new InvalidOperationException(); public bool IsEnabled() => false; }
    private static StartupService Service(TestDatabase t) => new(t.Settings, new AuthService(t.Database, t.Audit, t.Settings, t.Log),
        TestData.Permissions(t), t.Audit, t.Log, new Store(), new Store());
    private static void Raw(TestDatabase t, string value) => t.Database.Tx(c => t.Database.Execute(c,
        "UPDATE settings SET value=@value WHERE key='startup.mode';", new { value }));
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n", t.Database.Query<string>(c,
        "SELECT key || '=' || value || '@' || updated_at FROM settings ORDER BY key;")));
    private static long Audits(TestDatabase t) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs;")));
    private static SettingsService Other(TestDatabase t)
    {
        var db = new Database(t.Database.FilePath, pooling: false, durable: false); var audit = new AuditService(db, t.Log);
        return new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
    }
    private static void Unknown(StartupModeStatus status)
    {
        Assert.False(status.Verified); Assert.Equal("manual", status.Mode); Assert.False(status.IsProtected); Assert.False(status.WantsAutoStart);
        Assert.Contains("protected close is OFF", status.Notice); Assert.Contains("saved mode was not changed", status.Notice);
        Assert.Contains("Separate screen lock still applies", status.Notice);
    }

    [Theory]
    [InlineData("\"manual\"", "manual", false, false)]
    [InlineData("\"auto\"", "auto", false, true)]
    [InlineData(" \n\"protected\"\t", "protected", true, true)]
    public void CurrentStrictReadReturnsCoherentPolicyWithoutRefreshingCache(string raw, string expected, bool close, bool kiosk)
    {
        using var t = new TestDatabase(); Raw(t, raw); var before = State(t); var audits = Audits(t);
        var current = t.Settings.ReadCurrentStartupMode(); Assert.True(current.Ok, current.Error); Assert.Equal(expected, current.Value);
        var service = Service(t); var status = service.ReadStatus(); Assert.True(status.Verified); Assert.Empty(status.Notice);
        Assert.Equal(expected, status.Mode); Assert.Equal(close, status.IsProtected); Assert.Equal(kiosk, status.WantsAutoStart);
        Assert.Equal(expected, service.Mode); Assert.Equal(close, service.IsProtected); Assert.Equal(kiosk, service.WantsAutoStart);
        Assert.Equal("manual", t.Settings.GetString(StartupService.ModeKey)); Assert.Equal(before, State(t)); Assert.Equal(audits, Audits(t));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"MANUAL\"")]
    [InlineData("\" auto \"")]
    [InlineData("\"sometimes\"")]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("{}")]
    [InlineData("")]
    public void InvalidFirstReadUsesManualWithWarningNotAnInventedVerifiedDefault(string raw)
    {
        using var t = new TestDatabase(); Raw(t, raw); var state = State(t); var audit = Audits(t);
        Assert.False(t.Settings.ReadCurrentStartupMode().Ok); var service = Service(t); Unknown(service.ReadStatus());
        Assert.Contains("unverified", service.ModeLabel); Assert.Equal(state, State(t)); Assert.Equal(audit, Audits(t));
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("auto")]
    [InlineData("protected")]
    public void ChoiceCDoesNotRetainAnyPreviousVerifiedModeOnFailure(string mode)
    {
        using var t = new TestDatabase(); TestData.SeedRawSetting(t, StartupService.ModeKey, mode); var service = Service(t);
        Assert.Equal(mode, service.ReadStatus().Mode); Raw(t, "null"); Unknown(service.ReadStatus()); Assert.False(service.IsProtected);
        Assert.False(service.WantsAutoStart); Assert.Equal("manual", service.Mode);
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("auto")]
    [InlineData("protected")]
    public void ValidLaterReadAdoptsRealModeAndClearsWarning(string mode)
    {
        using var t = new TestDatabase(); Raw(t, "null"); var service = Service(t); Unknown(service.ReadStatus());
        Assert.True(Other(t).Set(StartupService.ModeKey, mode, TestData.Owner).Ok);
        var status = service.ReadStatus(); Assert.True(status.Verified); Assert.Equal(mode, status.Mode); Assert.Empty(status.Notice);
    }

    [Fact]
    public void AnotherContextWriterIsObservedWithoutReloadAndOpenFormDoesNotRebase()
    {
        using var t = new TestDatabase(); var service = Service(t);
        var form = t.Settings.LoadEditForm(TestData.Owner, new[] { StartupService.ModeKey }); Assert.True(form.Ok, form.Error);
        Assert.True(Other(t).Set(StartupService.ModeKey, "protected", TestData.Owner).Ok);
        Assert.Equal("manual", t.Settings.GetString(StartupService.ModeKey)); Assert.Equal("protected", service.Mode);
        var result = t.Settings.SaveEditForm(new Dictionary<string, object?> { [StartupService.ModeKey] = "auto" }, TestData.Owner, form.Value);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Contains("changed", result.Error!);
    }

    [Fact]
    public void MissingRowIsUnknownWithoutAutomaticDatabaseRepair()
    {
        using var t = new TestDatabase(); t.Database.Tx(c => t.Database.Execute(c, "DELETE FROM settings WHERE key='startup.mode';"));
        var before = State(t); Unknown(Service(t).ReadStatus()); Assert.Equal(before, State(t)); Assert.False(t.Settings.ReadCurrentStartupMode().Ok);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealSqlFailureUsesManualEvenAfterProtectedAndRecovers(bool previous)
    {
        using var t = new TestDatabase(); var service = Service(t);
        if (previous) { TestData.SeedRawSetting(t, StartupService.ModeKey, "protected"); Assert.True(service.IsProtected); }
        t.Database.Tx(c => t.Database.Execute(c, "ALTER TABLE settings RENAME TO synthetic_startup_unavailable;"));
        try { Assert.False(t.Settings.ReadCurrentStartupMode().Ok); Unknown(service.ReadStatus()); }
        finally { t.Database.Tx(c => t.Database.Execute(c, "ALTER TABLE synthetic_startup_unavailable RENAME TO settings;")); }
        Assert.True(service.ReadStatus().Verified); Assert.Equal(previous, service.IsProtected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartupFallbackDoesNotChangeSeparateFocusPolicy(bool enabled)
    {
        using var t = new TestDatabase(); TestData.SeedRawSetting(t, FocusGuardService.GuardKey, enabled);
        var focus = new FocusGuardService(t.Settings, t.Audit, t.Log); Assert.Equal(enabled, focus.Enabled);
        Raw(t, "null"); Unknown(Service(t).ReadStatus()); Assert.Equal(enabled, focus.Enabled);
    }
}
