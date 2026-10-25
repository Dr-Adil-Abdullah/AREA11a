using Area11.Core.Data;
using Area11.Core.Services;
using Area11.Core.Utils;
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

    [Fact]
    public void ProvisionalMoneyMigration_BackfillsOldLinesAndPayments()
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-migration-test", Guid.NewGuid().ToString("N"));
        var legacyFolder = Path.Combine(folder, "legacy");
        Directory.CreateDirectory(legacyFolder);

        foreach (var source in Directory.GetFiles(TestDatabase.MigrationsFolder, "*.sql"))
        {
            if (!Path.GetFileName(source).StartsWith("024_", StringComparison.Ordinal))
            {
                File.Copy(source, Path.Combine(legacyFolder, Path.GetFileName(source)));
            }
        }

        var path = Path.Combine(folder, "legacy.db");

        try
        {
            var database = new Database(path, pooling: false, durable: false);
            var log = new LogService(Path.Combine(folder, "logs"));
            var runner = new MigrationRunner(database, log);
            runner.ApplyPending(legacyFolder);

            database.Tx(connection =>
            {
                database.Execute(connection, @"
INSERT INTO products(id, name, base_unit, cost_paisa, retail_paisa, created_at, updated_at)
VALUES(1, 'First', 'tablet', 500, 800, '2026-01-01', '2026-01-01'),
      (2, 'Second', 'tablet', 700, 1600, '2026-01-01', '2026-01-01');");
                database.Execute(connection, @"
INSERT INTO batches(id, product_id, batch_no, qty_base, cost_paisa, retail_paisa, created_at)
VALUES(1, 1, 'A', 0, 500, 800, '2026-01-01'),
      (2, 2, 'B', 0, 700, 1600, '2026-01-01');");
                database.Execute(connection, @"
INSERT INTO provisional_returns(id, code, date, refund_paisa, status, cancel_reason, cancelled_at)
VALUES(1, 'PR-0001', '2026-10-20 10:00:00', 10001, 'cancelled', 'typed twice', '2026-10-21 11:00:00');");
                database.Execute(connection, @"
INSERT INTO provisional_items(id, provisional_id, product_id, batch_id, qty_base, qty_entered, unit_entered, created_at)
VALUES(1, 1, 1, 1, 1, 1, 'tablet', '2026-10-20 10:00:00'),
      (2, 1, 2, 2, 2, 2, 'tablet', '2026-10-20 10:00:00');");
            });

            Assert.Equal(1, runner.ApplyPending(TestDatabase.MigrationsFolder));

            using var connection = database.Open();
            var lines = database.Query<MigrationMoneyRow>(connection,
                "SELECT refund_paisa AS refundPaisa, cost_paisa_at_return AS costPaisa FROM provisional_items ORDER BY id;");
            Assert.Equal(10001L, lines.Sum(row => row.RefundPaisa));
            Assert.Equal(new long[] { 5001, 5000 }, lines.Select(row => row.RefundPaisa));
            Assert.All(lines, row => Assert.True(row.RefundPaisa >= 0));
            Assert.Equal(new long[] { 500, 700 }, lines.Select(row => row.CostPaisa));

            var payment = Assert.Single(database.Query<long>(connection,
                "SELECT amount_paisa FROM payments WHERE provisional_return_id = 1;"));
            Assert.Equal(-10001L, payment); // cancellation is not proof of cash coming back

            var reportRows = Convert.ToInt64(database.Scalar(connection,
                "SELECT COUNT(1) FROM provisional_report_lines;") ?? 0L);
            Assert.Equal(0L, reportRows); // a cancelled document is void in reports, not a negative refund
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    private sealed class MigrationMoneyRow
    {
        public long RefundPaisa { get; set; }

        public long CostPaisa { get; set; }
    }

    private sealed class AuditRow
    {
        public string? OldValue { get; set; }

        public string? NewValue { get; set; }
    }
}
