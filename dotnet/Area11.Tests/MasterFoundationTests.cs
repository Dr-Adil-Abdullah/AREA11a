using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;
using System.IO;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Approved master-checklist Phase 1. These are schema/data-contract tests, not proof that the later
/// WPF, WhatsApp, printing, automatic-sync or physical owner workflows have been implemented.
/// </summary>
public sealed class MasterFoundationTests
{
    [Fact]
    public void FreshSchema_ContainsMasterContracts_AndKeepsQuantitiesReal()
    {
        using var test = new TestDatabase();
        using var connection = test.Database.Open();

        foreach (var table in new[]
                 {
                     "form_field_definitions", "document_drafts", "generic_formulas", "brands",
                     "shift_discrepancies"
                 })
        {
            Assert.Equal(1L, Convert.ToInt64(test.Database.Scalar(connection,
                "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @table;",
                new { table }) ?? 0L));
        }

        Assert.Equal(1L, Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM sqlite_master WHERE type = 'view' AND name = 'product_supplier_directory';") ?? 0L));

        foreach (var column in new[] { "generic_formula_id", "brand_id", "preferred_supplier_id" })
        {
            Assert.Equal(1L, Convert.ToInt64(test.Database.Scalar(connection,
                "SELECT COUNT(1) FROM pragma_table_info('products') WHERE name = @column;",
                new { column }) ?? 0L));
        }

        // D-93 remains unchanged: every quantity involved in these posting lines is still REAL.
        foreach (var table in new[] { "batches", "purchase_items", "sale_items" })
        {
            var declared = Convert.ToString(test.Database.Scalar(connection,
                "SELECT type FROM pragma_table_info(@table) WHERE name = 'qty_base';",
                new { table }));
            Assert.Equal("REAL", declared?.ToUpperInvariant());
        }
    }

    [Fact]
    public void NativeFields_SupportMonthCalendarAndRealQuantityContracts()
    {
        using var test = new TestDatabase();

        var expiry = InsertField(test, "purchase.expiry", NativeFieldDataType.Date,
            NativeInputMethod.MonthCalendar, critical: false, safeRequired: false, required: false,
            acknowledged: false);
        var quantity = InsertField(test, "purchase.quantity", NativeFieldDataType.Quantity,
            NativeInputMethod.Numeric, critical: true, safeRequired: true, required: true,
            acknowledged: false);

        using var connection = test.Database.Open();
        var rows = test.Database.Query<FormFieldDefinition>(connection,
            "SELECT * FROM form_field_definitions ORDER BY id;");
        Assert.Equal(2, rows.Count);
        Assert.Equal(NativeInputMethod.MonthCalendar,
            rows.Single(row => row.Id == expiry).InputMethod);
        Assert.Equal(NativeFieldDataType.Quantity,
            rows.Single(row => row.Id == quantity).DataType);
    }

    [Fact]
    public void CriticalFieldCannotBecomeOptionalWithoutNamedOwnerAcknowledgement()
    {
        using var test = new TestDatabase();

        Assert.Throws<SqliteException>(() => InsertField(test, "sale.quantity",
            NativeFieldDataType.Quantity, NativeInputMethod.Numeric,
            critical: true, safeRequired: true, required: false, acknowledged: false));

        var overridden = InsertField(test, "sale.quantity",
            NativeFieldDataType.Quantity, NativeInputMethod.Numeric,
            critical: true, safeRequired: true, required: false, acknowledged: true);
        var optional = InsertField(test, "product.rack",
            NativeFieldDataType.Text, NativeInputMethod.Text,
            critical: false, safeRequired: false, required: false, acknowledged: false);

        using var connection = test.Database.Open();
        var rows = test.Database.Query<FormFieldDefinition>(connection,
            "SELECT * FROM form_field_definitions ORDER BY id;");
        Assert.True(rows.Single(row => row.Id == overridden).RequiresOverrideAcknowledgement);
        Assert.Equal("Owner", rows.Single(row => row.Id == overridden).OverrideAcknowledgedBy);
        Assert.False(rows.Single(row => row.Id == optional).RequiresOverrideAcknowledgement);
    }

    [Fact]
    public void DraftsValidateJsonAndNeverBecomeFinancialDocumentsByThemselves()
    {
        using var test = new TestDatabase();

        Assert.Throws<SqliteException>(() => InsertDraft(test, "not-json"));
        InsertDraft(test, "{\"lines\":[],\"customerId\":null}");

        using var connection = test.Database.Open();
        var draft = Assert.Single(test.Database.Query<DocumentDraft>(connection,
            "SELECT * FROM document_drafts;"));
        Assert.Equal(DraftStatus.Draft, draft.Status);
        Assert.Null(draft.PostedDocumentId);
        Assert.Null(draft.PostedAt);

        foreach (var table in new[] { "sales", "purchases", "payments", "stock_movements" })
        {
            Assert.Equal(0L, Convert.ToInt64(test.Database.Scalar(connection,
                "SELECT COUNT(1) FROM " + table + ";") ?? 0L));
        }

        // A draft cannot even be marked posted without the resulting document id and timestamp.
        Assert.Throws<SqliteException>(() => test.Database.Tx(connection =>
            test.Database.Execute(connection,
                "UPDATE document_drafts SET status = 'posted' WHERE id = @id;", new { draft.Id })));
    }

    [Fact]
    public void FormulaAndBrandMastersRejectBlankNamesAndDuplicateNormalizedKeys()
    {
        using var test = new TestDatabase();

        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, @"
INSERT INTO generic_formulas(id, name, name_key)
VALUES(1, 'Amoxicillin', 'amoxicillin');");
            test.Database.Execute(connection, @"
INSERT INTO brands(id, name, name_key)
VALUES(1, 'Legacy brand', 'legacy brand');");
        });

        // NOCASE uniqueness is a second line behind the future service's normalized-name guard.
        Assert.Throws<SqliteException>(() => test.Database.Tx(connection =>
            test.Database.Execute(connection, @"
INSERT INTO generic_formulas(id, name, name_key)
VALUES(2, 'Different spelling', 'AMOXICILLIN');")));
        Assert.Throws<SqliteException>(() => test.Database.Tx(connection =>
            test.Database.Execute(connection,
                "INSERT INTO brands(id, name, name_key) VALUES(2, '   ', 'blank');")));
    }

    [Fact]
    public void SupplierDirectoryContainsBothPreferredAndEveryHistoricalSupplier()
    {
        using var test = new TestDatabase();

        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "INSERT INTO suppliers(id, name) VALUES(101, 'Preferred supplier'), (102, 'Historical supplier');");
            test.Database.Execute(connection,
                "INSERT INTO products(id, name, preferred_supplier_id) VALUES(101, 'Laxin 50', 101);");
            test.Database.Execute(connection, @"
INSERT INTO purchases(id, code, supplier_id, date)
VALUES(101, 'PINV-HISTORY', 102, '2026-10-01 09:00:00');");
            test.Database.Execute(connection,
                "INSERT INTO purchase_items(id, purchase_id, product_id, qty_base) VALUES(101, 101, 101, 2.5);");
        });

        using var connection = test.Database.Open();
        var rows = test.Database.Query<ProductSupplierDirectoryRow>(connection, @"
SELECT product_id AS productId, supplier_id AS supplierId,
       is_preferred AS isPreferred, last_purchased_at AS lastPurchasedAt
FROM product_supplier_directory
WHERE product_id = 101
ORDER BY supplier_id;");

        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].IsPreferred);
        Assert.Equal(101, rows[0].SupplierId);
        Assert.Null(rows[0].LastPurchasedAt);
        Assert.False(rows[1].IsPreferred);
        Assert.Equal(102, rows[1].SupplierId);
        Assert.Equal("2026-10-01 09:00:00", rows[1].LastPurchasedAt);
        Assert.Equal(2.5d, Convert.ToDouble(test.Database.Scalar(connection,
            "SELECT qty_base FROM purchase_items WHERE id = 101;")));
    }

    [Fact]
    public void DrawerDiscrepanciesRequireOpeningClosingArithmeticAndARealReason()
    {
        using var test = new TestDatabase();

        test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "INSERT INTO shifts(id, opening_float_paisa) VALUES(1, 9000), (2, 0);");
            InsertDiscrepancy(test.Database, connection, 1, DrawerStage.Opening,
                expected: 10000, counted: 9000, difference: -1000, reason: "Morning short");
            InsertDiscrepancy(test.Database, connection, 1, DrawerStage.Closing,
                expected: 20000, counted: 21000, difference: 1000, reason: "Counted extra");
        });

        using (var connection = test.Database.Open())
        {
            var rows = test.Database.Query<ShiftDiscrepancy>(connection,
                "SELECT * FROM shift_discrepancies WHERE shift_id = 1 ORDER BY stage;");
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, row => row.Stage == DrawerStage.Opening && row.DifferencePaisa == -1000);
            Assert.Contains(rows, row => row.Stage == DrawerStage.Closing && row.DifferencePaisa == 1000);
        }

        Assert.Throws<SqliteException>(() => test.Database.Tx(connection =>
            InsertDiscrepancy(test.Database, connection, 2, DrawerStage.Opening,
                expected: 10000, counted: 9000, difference: -5, reason: "Wrong arithmetic")));
        Assert.Throws<SqliteException>(() => test.Database.Tx(connection =>
            InsertDiscrepancy(test.Database, connection, 2, DrawerStage.Closing,
                expected: 10000, counted: 9000, difference: -1000, reason: "x")));
        Assert.Throws<SqliteException>(() => test.Database.Tx(connection =>
            InsertDiscrepancy(test.Database, connection, 2, DrawerStage.Closing,
                expected: 10000, counted: 10000, difference: 0, reason: "No difference")));
    }

    [Fact]
    public void MasterSettings_DefaultToSinglePrintManualSyncAndVisibleProfit()
    {
        using var test = new TestDatabase();

        Assert.False(test.Settings.GetBool(MasterFoundationSettings.PrinterDefaultBulk));
        Assert.Equal(MasterFoundationSettings.SeparateReceipts,
            test.Settings.GetString(MasterFoundationSettings.PrinterBulkFormat));
        Assert.True(test.Settings.GetBool(MasterFoundationSettings.WhatsAppPreparePdf));
        Assert.Equal(string.Empty,
            test.Settings.GetString(MasterFoundationSettings.WhatsAppExportDirectory));
        Assert.True(test.Settings.GetBool(MasterFoundationSettings.ProfitShowMargins));
        Assert.False(test.Settings.GetBool(MasterFoundationSettings.SyncAutomaticEnabled));
        Assert.Equal(15, test.Settings.GetInt(MasterFoundationSettings.SyncAutomaticMinutes));
    }

    [Fact]
    public void MasterSettings_RefuseInvalidBulkFormatAndAutomaticInterval()
    {
        using var test = new TestDatabase();

        Assert.False(test.Settings.Set(MasterFoundationSettings.PrinterBulkFormat,
            "unknown", TestData.Owner).Ok);
        Assert.Equal(MasterFoundationSettings.SeparateReceipts,
            test.Settings.GetString(MasterFoundationSettings.PrinterBulkFormat));
        Assert.True(test.Settings.Set(MasterFoundationSettings.PrinterBulkFormat,
            MasterFoundationSettings.A4List, TestData.Owner).Ok);

        Assert.False(test.Settings.Set(MasterFoundationSettings.SyncAutomaticMinutes,
            4, TestData.Owner).Ok);
        Assert.False(test.Settings.Set(MasterFoundationSettings.SyncAutomaticMinutes,
            15.5, TestData.Owner).Ok);
        Assert.True(test.Settings.Set(MasterFoundationSettings.SyncAutomaticMinutes,
            30, TestData.Owner).Ok);
        Assert.Equal(30, test.Settings.GetInt(MasterFoundationSettings.SyncAutomaticMinutes));
        Assert.False(test.Settings.Set(MasterFoundationSettings.WhatsAppExportDirectory,
            "bad\npath", TestData.Owner).Ok);

        // The new contracts use the existing atomic old -> new audit path; refused values above
        // must not add another row for the same key.
        using var connection = test.Database.Open();
        var audit = Assert.Single(test.Database.Query<AuditRow>(connection,
            "SELECT * FROM audit_logs WHERE action = 'settings_change' AND entity_id = @key;",
            new { key = MasterFoundationSettings.SyncAutomaticMinutes }));
        Assert.Equal("Owner", audit.UserName);
        Assert.Equal("15", audit.OldValue);
        Assert.Equal("30", audit.NewValue);
    }

    [Fact]
    public void Migration030_PreservesLegacyProductTextAndAddsHistoryWithoutGuessingLinks()
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-master-foundation", Guid.NewGuid().ToString("N"));
        var legacyFolder = Path.Combine(folder, "legacy");
        Directory.CreateDirectory(legacyFolder);

        foreach (var source in Directory.GetFiles(TestDatabase.MigrationsFolder, "*.sql"))
        {
            if (!Path.GetFileName(source).StartsWith("030_", StringComparison.Ordinal))
            {
                File.Copy(source, Path.Combine(legacyFolder, Path.GetFileName(source)));
            }
        }

        try
        {
            var database = new Database(Path.Combine(folder, "legacy.db"), pooling: false, durable: false);
            var log = new LogService(Path.Combine(folder, "logs"));
            var runner = new MigrationRunner(database, log);
            runner.ApplyPending(legacyFolder);

            database.Tx(connection =>
            {
                database.Execute(connection, "INSERT INTO suppliers(id, name) VALUES(11, 'Old supplier');");
                database.Execute(connection,
                    "INSERT INTO products(id, name, generic, brand) VALUES(12, 'Old product', 'Legacy salt', 'Legacy brand');");
                database.Execute(connection, @"
INSERT INTO purchases(id, code, supplier_id, date)
VALUES(13, 'PINV-OLD', 11, '2026-01-01 08:00:00');");
                database.Execute(connection,
                    "INSERT INTO purchase_items(id, purchase_id, product_id, qty_base) VALUES(14, 13, 12, 1.25);");
            });

            Assert.Equal(1, runner.ApplyPending(TestDatabase.MigrationsFolder));

            using var connection = database.Open();
            var product = database.QuerySingle<LegacyProductRow>(connection, @"
SELECT generic, brand, generic_formula_id AS genericFormulaId,
       brand_id AS brandId, preferred_supplier_id AS preferredSupplierId
FROM products WHERE id = 12;");
            Assert.NotNull(product);
            Assert.Equal("Legacy salt", product!.Generic);
            Assert.Equal("Legacy brand", product.Brand);
            Assert.Null(product.GenericFormulaId);
            Assert.Null(product.BrandId);
            Assert.Null(product.PreferredSupplierId);

            var supplier = Assert.Single(database.Query<ProductSupplierDirectoryRow>(connection, @"
SELECT product_id AS productId, supplier_id AS supplierId,
       is_preferred AS isPreferred, last_purchased_at AS lastPurchasedAt
FROM product_supplier_directory WHERE product_id = 12;"));
            Assert.Equal(11, supplier.SupplierId);
            Assert.False(supplier.IsPreferred);
            Assert.Equal(1.25d, Convert.ToDouble(database.Scalar(connection,
                "SELECT qty_base FROM purchase_items WHERE id = 14;")));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void IsolatedTestRebuild_AlwaysBacksUpBeforeReplacingAndStartsWithoutBusinessRows()
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-isolated-rebuild", Guid.NewGuid().ToString("N"));

        try
        {
            var service = new IsolatedTestCopyService(folder, TestDatabase.MigrationsFolder);
            var first = service.Rebuild(IsolatedTestCopyService.ConfirmationPhrase);

            Assert.True(first.Ok, first.Error);
            Assert.NotNull(first.Value);
            Assert.False(first.Value!.HadPreviousCopy);
            Assert.Equal(string.Empty, first.Value.TechnicalBackupPath);
            Assert.Equal(Directory.GetFiles(TestDatabase.MigrationsFolder, "*.sql").Length,
                first.Value.MigrationsApplied);
            Assert.True(first.Value.TableCount >= 42);
            Assert.True(File.Exists(Path.Combine(folder, IsolatedTestCopyService.MarkerFileName)));

            var database = new Database(first.Value.DatabasePath, pooling: false, durable: false);
            using (var freshConnection = database.Open())
            {
                foreach (var table in new[] { "products", "sales", "purchases", "payments", "stock_movements" })
                {
                    Assert.Equal(0L, Convert.ToInt64(database.Scalar(freshConnection,
                        "SELECT COUNT(1) FROM " + table + ";") ?? 0L));
                }
            }

            database.Tx(connection => database.Execute(connection,
                "INSERT INTO products(id, name) VALUES(77, 'Disposable test row');"));

            var second = service.Rebuild(IsolatedTestCopyService.ConfirmationPhrase);

            Assert.True(second.Ok, second.Error);
            Assert.NotNull(second.Value);
            Assert.True(second.Value!.HadPreviousCopy);
            Assert.True(File.Exists(second.Value.TechnicalBackupPath));
            Assert.EndsWith(".db", second.Value.TechnicalBackupPath, StringComparison.OrdinalIgnoreCase);

            using (var currentConnection = database.Open())
            {
                Assert.Equal(0L, Convert.ToInt64(database.Scalar(currentConnection,
                    "SELECT COUNT(1) FROM products;") ?? 0L));
                var audit = Assert.Single(database.Query<AuditRow>(currentConnection,
                    "SELECT * FROM audit_logs WHERE action = 'test_schema_rebuild';"));
                Assert.Equal("Test copy", audit.Module);
                Assert.Contains(Path.GetFileName(second.Value.TechnicalBackupPath), audit.Details,
                    StringComparison.Ordinal);
            }

            var backup = new Database(second.Value.TechnicalBackupPath, pooling: false, durable: false);
            using (var backupConnection = backup.Open())
            {
                Assert.Equal("Disposable test row", Convert.ToString(backup.Scalar(backupConnection,
                    "SELECT name FROM products WHERE id = 77;")));
            }
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void IsolatedTestRebuild_RefusesWrongConfirmationAndUnmarkedExistingFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "area11-isolated-refusals", Guid.NewGuid().ToString("N"));
        var newCopy = Path.Combine(root, "new");
        var unmarked = Path.Combine(root, "unmarked");

        try
        {
            var wrong = new IsolatedTestCopyService(newCopy, TestDatabase.MigrationsFolder)
                .Rebuild("yes");
            Assert.False(wrong.Ok);
            Assert.False(Directory.Exists(newCopy));

            Directory.CreateDirectory(unmarked);
            var unrelated = Path.Combine(unmarked, "keep.txt");
            File.WriteAllText(unrelated, "not a test marker");

            var refused = new IsolatedTestCopyService(unmarked, TestDatabase.MigrationsFolder)
                .Rebuild(IsolatedTestCopyService.ConfirmationPhrase);

            Assert.False(refused.Ok);
            Assert.Contains("not empty", refused.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("not a test marker", File.ReadAllText(unrelated));
            Assert.False(File.Exists(Path.Combine(unmarked, IsolatedTestCopyService.DatabaseFileName)));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void IsolatedTestRebuild_BackupFaultLeavesTheOriginalIsolatedCopyUntouched()
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-isolated-backup-fault", Guid.NewGuid().ToString("N"));

        try
        {
            var service = new IsolatedTestCopyService(folder, TestDatabase.MigrationsFolder);
            var first = service.Rebuild(IsolatedTestCopyService.ConfirmationPhrase);
            Assert.True(first.Ok, first.Error);

            var database = new Database(first.Value!.DatabasePath, pooling: false, durable: false);
            database.Tx(connection => database.Execute(connection,
                "INSERT INTO products(id, name) VALUES(88, 'Must survive failed rebuild');"));

            // Fault injection happens only in this throw-away folder: a file blocks the technical
            // backup directory. The rebuild must stop BEFORE staging or replacing the old database.
            File.WriteAllText(Path.Combine(folder, "backups"), "blocks backup directory");
            var failed = service.Rebuild(IsolatedTestCopyService.ConfirmationPhrase);

            Assert.False(failed.Ok);
            Assert.Contains("not rebuilt", failed.Error, StringComparison.OrdinalIgnoreCase);
            using var connection = database.Open();
            Assert.Equal("Must survive failed rebuild", Convert.ToString(database.Scalar(connection,
                "SELECT name FROM products WHERE id = 88;")));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }

    private static int InsertField(
        TestDatabase test,
        string key,
        string dataType,
        string inputMethod,
        bool critical,
        bool safeRequired,
        bool required,
        bool acknowledged) => test.Database.Tx(connection =>
    {
        test.Database.Execute(connection, @"
INSERT INTO form_field_definitions
    (field_key, section_key, label, data_type, input_method, safe_required, required, critical,
     override_acknowledged_by, override_acknowledged_at)
VALUES(@key, 'purchase', @key, @dataType, @inputMethod, @safeRequired, @required, @critical,
       @by, @at);",
            new
            {
                key,
                dataType,
                inputMethod,
                safeRequired = safeRequired ? 1 : 0,
                required = required ? 1 : 0,
                critical = critical ? 1 : 0,
                by = acknowledged ? "Owner" : null,
                at = acknowledged ? "2026-11-01 09:00:00" : null
            });
        return (int)test.Database.NextId(connection);
    });

    private static void InsertDraft(TestDatabase test, string payload) => test.Database.Tx(connection =>
        test.Database.Execute(connection, @"
INSERT INTO document_drafts(draft_key, draft_type, payload_json, created_by_name)
VALUES(@key, 'sale', @payload, 'Owner');",
            new { key = Guid.NewGuid().ToString("N"), payload }));

    private static void InsertDiscrepancy(
        Database database,
        SqliteConnection connection,
        int shift,
        string stage,
        long expected,
        long counted,
        long difference,
        string reason) => database.Execute(connection, @"
INSERT INTO shift_discrepancies
    (shift_id, stage, expected_paisa, counted_paisa, difference_paisa, reason)
VALUES(@shift, @stage, @expected, @counted, @difference, @reason);",
        new { shift, stage, expected, counted, difference, reason });

    private sealed class LegacyProductRow
    {
        public string? Generic { get; set; }

        public string? Brand { get; set; }

        public int? GenericFormulaId { get; set; }

        public int? BrandId { get; set; }

        public int? PreferredSupplierId { get; set; }
    }
}
