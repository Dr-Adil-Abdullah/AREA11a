using Area11.Core.Services;
using System.IO;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The schema port is the highest-risk piece (an existing area11.db must open unchanged), so it
/// gets its own tests: every migration, the ledger, the settings seed and the first login.
/// </summary>
public sealed class MigrationTests
{
    [Fact]
    public void AllMigrations_AreRecordedOnce()
    {
        using var test = new TestDatabase();

        // The expected number is the number of .sql files, so adding a migration never breaks this
        // test and never needs a number typed by hand (15 was hard-coded here and went stale).
        var files = Directory.GetFiles(TestDatabase.MigrationsFolder, "*.sql")
            // GetFileNameWithoutExtension is annotated `string?`; a path that exists always has a
            // name, so the empty string keeps the list non-nullable and the build warning-free.
            .Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        using var connection = test.Database.Open();
        var recorded = test.Database.Query<string>(connection, "SELECT id FROM _migrations;")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(files, recorded);
        Assert.Equal(files.Count, recorded.Distinct().Count());
    }

    [Fact]
    public void CoreTables_Exist()
    {
        using var test = new TestDatabase();
        using var connection = test.Database.Open();

        foreach (var table in new[]
                 {
                     "settings", "users", "products", "batches", "sales", "sale_items", "purchases",
                     "suppliers", "customers", "payments", "stock_movements", "shifts", "sale_returns",
                     "provisional_returns", "supplier_returns", "stock_takes", "audit_logs",
                     "alert_dismissals", "user_permissions", "photos", "_migrations"
                 })
        {
            var exists = Convert.ToInt64(test.Database.Scalar(connection,
                "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @name;",
                new { name = table }) ?? 0L);

            Assert.True(exists > 0, $"Table '{table}' is missing.");
        }
    }

    [Fact]
    public void SettingsSeed_ContainsEveryKnownKey()
    {
        using var test = new TestDatabase();
        using var connection = test.Database.Open();

        var rows = Convert.ToInt64(
            test.Database.Scalar(connection, "SELECT COUNT(1) FROM settings;") ?? 0L);

        Assert.Equal((long)SettingsService.KnownKeys.Count, rows);
        Assert.True(SettingsService.KnownKeys.Count >= 70, "The reference catalogue has 70 keys.");
    }

    [Fact]
    public void FirstStart_CreatesOwnerAndCategories_AndDefaultLoginWorks()
    {
        using var test = new TestDatabase();

        var auth = new AuthService(test.Database, test.Audit, test.Settings, test.Log);
        var login = auth.Login(string.Empty, "area11");

        Assert.True(login.Ok, login.Error);
        Assert.NotNull(login.Value);
        Assert.True(login.Value!.IsOwner);

        using var connection = test.Database.Open();
        var categories = Convert.ToInt64(
            test.Database.Scalar(connection, "SELECT COUNT(1) FROM categories;") ?? 0L);

        Assert.Equal(5L, categories);
    }

    [Fact]
    public void WrongPassword_IsRejectedAndAudited()
    {
        using var test = new TestDatabase();
        var auth = new AuthService(test.Database, test.Audit, test.Settings, test.Log);

        var login = auth.Login(string.Empty, "definitely-wrong");

        Assert.False(login.Ok);

        using var connection = test.Database.Open();
        var failed = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = 'login_failed';") ?? 0L);

        Assert.Equal(1L, failed);
    }

    [Fact]
    public void Numbering_IssuesSequentialPermanentCodes()
    {
        using var test = new TestDatabase();
        var numbering = new NumberingService(test.Settings);

        var codes = test.Database.Tx(connection => new[]
        {
            numbering.Take(connection, "sale"),
            numbering.Take(connection, "sale")
        });

        Assert.Equal("INV-0001", codes[0]);
        Assert.Equal("INV-0002", codes[1]);
    }

    [Fact]
    public void Audit_OldNew_IsStored()
    {
        using var test = new TestDatabase();

        test.Database.Tx(connection =>
            test.Audit.Record(connection, null, "POS", "test_event", "sales", "1", "5", "-7"));

        using var connection = test.Database.Open();
        var row = test.Database.QuerySingle<AuditRow>(connection,
            "SELECT old_value, new_value FROM audit_logs WHERE action = 'test_event';");

        Assert.NotNull(row);
        Assert.Equal("5", row!.OldValue);
        Assert.Equal("-7", row.NewValue);
    }

    private sealed class AuditRow
    {
        public string? OldValue { get; set; }

        public string? NewValue { get; set; }
    }
}
