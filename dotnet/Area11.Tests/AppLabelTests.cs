using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>E-12/S-05: editable section names, persisted and guarded in Core.</summary>
public sealed class AppLabelTests
{
    private static AppLabelService Service(TestDatabase test) =>
        new(test.Settings, TestData.Permissions(test));

    [Fact]
    public void CatalogDefaults_AreUniqueAndPersistedAsKnownSettings()
    {
        using var test = new TestDatabase();
        var rows = Service(test).List();

        Assert.Equal(32, rows.Count);
        Assert.Equal(rows.Count, rows.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(rows, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.DefaultText));
            Assert.Equal(row.DefaultText, row.Text);
            Assert.Contains("labels." + row.Key, SettingsService.KnownKeys);
        });
    }

    [Fact]
    public void UnicodeRename_IsTrimmedPersistedAndAudited()
    {
        using var test = new TestDatabase();
        var result = Service(test).Save(new Dictionary<string, string?>
        {
            ["counter"] = "  ریکاؤنٹر  "
        }, TestData.Owner);

        Assert.True(result.Ok, result.Error);
        Assert.Equal("ریکاؤنٹر", Service(test).Get("counter"));

        using var secondDatabase = new Database(test.Database.FilePath, pooling: false, durable: false);
        var secondSettings = new SettingsService(secondDatabase, test.Audit, test.Log);
        Assert.Equal("ریکاؤنٹر", new AppLabelService(secondSettings, TestData.Permissions(test)).Get("counter"));

        using var connection = test.Database.Open();
        var audit = test.Database.QuerySingle<string>(connection, @"
SELECT old_value || '|' || new_value FROM audit_logs
WHERE action = 'settings_change' AND entity_id = 'labels.counter'
ORDER BY id DESC LIMIT 1;");
        Assert.Contains("Counter (POS)", audit);
        Assert.Contains("ریکاؤنٹر", audit);
    }

    [Fact]
    public void InvalidLabels_AreRefusedAtomically()
    {
        using var test = new TestDatabase();
        var invalid = new string?[] { null, "", "   ", new string('x', AppLabelService.MaximumLength + 1), "two\nlines" };

        foreach (var text in invalid)
        {
            var result = Service(test).Save(new Dictionary<string, string?>
            {
                ["dashboard"] = "A valid but unsaved change",
                ["counter"] = text
            }, TestData.Owner);

            Assert.False(result.Ok);
            Assert.Equal("Dashboard", Service(test).Get("dashboard"));
            Assert.Equal("Counter (POS)", Service(test).Get("counter"));
        }
    }

    [Fact]
    public void UnknownLabel_IsRefusedWithoutASettingsRow()
    {
        using var test = new TestDatabase();
        var result = Service(test).Save(new Dictionary<string, string?> { ["invented"] = "Anything" }, TestData.Owner);

        Assert.False(result.Ok);
        Assert.Contains("Unknown", result.Error);

        using var connection = test.Database.Open();
        Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM settings WHERE key = 'labels.invented';") ?? 0L));
    }

    [Fact]
    public void Cashier_CannotSaveOrResetThroughThePublicService()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);

        var saved = Service(test).Save(new Dictionary<string, string?> { ["reports"] = "Changed" }, cashier);
        var reset = Service(test).Reset(cashier);
        var direct = test.Settings.Set("labels.reports", "Changed directly", cashier);

        Assert.False(saved.Ok);
        Assert.False(reset.Ok);
        Assert.False(direct.Ok);
        Assert.Equal("Reports", Service(test).Get("reports"));

        using var connection = test.Database.Open();
        Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = 'settings_change' AND entity_id LIKE 'labels.%';") ?? 0L));
    }

    [Fact]
    public void Reset_RestoresEveryDefaultAndWritesTheAudit()
    {
        using var test = new TestDatabase();
        var service = Service(test);
        Assert.True(service.Save(new Dictionary<string, string?>
        {
            ["dashboard"] = "Home",
            ["settings"] = "Choices"
        }, TestData.Owner).Ok);

        var reset = service.Reset(TestData.Owner);

        Assert.True(reset.Ok, reset.Error);
        Assert.All(service.List(), row => Assert.Equal(row.DefaultText, row.Text));

        using var connection = test.Database.Open();
        Assert.Equal(32L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = 'settings_change' AND entity_id LIKE 'labels.%';") ?? 0L) - 2L);
    }

    [Fact]
    public void DirectSettingsWrite_CannotBypassTheLabelGuard()
    {
        using var test = new TestDatabase();
        var result = test.Settings.Set("labels.dashboard", " ", TestData.Owner);
        var quietWrite = test.Settings.SetUiOnly("labels.dashboard", "Unaudited");
        var withoutPermissions = new SettingsService(test.Database, test.Audit, test.Log);
        var failClosed = withoutPermissions.Set("labels.dashboard", "No permission service", TestData.Owner);

        Assert.False(result.Ok);
        Assert.False(quietWrite.Ok);
        Assert.False(failClosed.Ok);
        Assert.Equal("Dashboard", Service(test).Get("dashboard"));

        // The larger Settings dictionary is a supported Core route too: it must store the same
        // trimmed text that AppLabelService.Save writes, not merely trim when the value is read.
        var direct = test.Settings.Set("labels.dashboard", "  Direct home  ", TestData.Owner);
        Assert.True(direct.Ok, direct.Error);
        Assert.Equal("\"Direct home\"", test.Settings.GetRaw("labels.dashboard"));
        Assert.Equal("Direct home", Service(test).Get("dashboard"));

        // A damaged or hand-edited old row must not leave a blank menu either.
        using var connection = test.Database.Open();
        test.Database.Execute(connection,
            "UPDATE settings SET value = @value WHERE key = 'labels.dashboard';",
            new { value = "\"  \"" });
        test.Settings.Reload();

        Assert.Equal("Dashboard", Service(test).Get("dashboard"));
    }
}
