using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated public workbook+Settings cases;not .NET execution/Excel/WPF/LAN evidence.</summary>
public sealed class WorkbookSettingsConcurrencyTests
{
    private static WorkbookService Service(TestDatabase t, Database? db = null, SettingsService? settings = null)
    {
        db ??= t.Database; settings ??= t.Settings; var audit = new AuditService(db, t.Log); var permissions = new PermissionService(db, t.Log, audit);
        return new WorkbookService(db, audit, permissions, settings, t.Log, new ImportService(db, audit, permissions, t.Log, settings));
    }
    private static XlsxWorkbook Book(WorkbookService service)
    {
        var result = service.ExportAll(TestData.Owner); Assert.True(result.Ok, result.Error); return XlsxFile.Read(result.Value!);
    }
    private static void Edit(XlsxWorkbook book, string key, string value)
    {
        var sheet = book.Sheet("Settings")!; var keyColumn = sheet.Rows[0].IndexOf("Key"); var valueColumn = sheet.Rows[0].IndexOf("Value");
        var row = sheet.Rows.Skip(1).Single(r => r[keyColumn] == key); row[valueColumn] = value;
    }
    private static BulkUpdatePreview Preview(WorkbookService service, XlsxWorkbook book, bool settings = true)
    {
        var result = service.PreviewBulk(TestData.Owner, XlsxFile.Write(book), settings);
        Assert.True(result.Ok, result.Error); return result.Value!;
    }
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n", new[] { "settings", "products", "audit_logs", "sqlite_sequence" }.Select(table =>
    {
        var columns = t.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
        var projection = string.Join("||':'||", columns.Select(col => "quote(\"" + col + "\")"));
        return table + "=" + string.Join(";", t.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
    })));
    private static void Conflict(ServiceResult result)
    { Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Contains("changed", result.Error!); }

    [Theory]
    [InlineData("discount.maxPercentCashier", "4", "discount.maxPercentManager", 25)]
    [InlineData("discount.maxPercentManager", "30", "discount.maxPercentCashier", 6)]
    [InlineData("supplier.returnCutoffDays", "80", "supplier.returnAlertDays", 200)]
    [InlineData("supplier.returnAlertDays", "200", "supplier.returnCutoffDays", 80)]
    public void UntouchedValidationPartnerChangesInvalidateTheReviewedSettings(string edited, string proposed, string partner, int concurrent)
    {
        using var t = new TestDatabase(); var service = Service(t); var book = Book(service); Edit(book, edited, proposed);
        var preview = Preview(service, book); Assert.True(preview.CanApply, preview.SummaryText);
        Assert.True(t.Settings.Set(partner, concurrent, TestData.Owner).Ok); var before = State(t);
        Conflict(service.ApplyBulk(TestData.Owner, preview)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void ConcurrentEditedSettingRefusesTheWholeMixedWorkbook()
    {
        using var t = new TestDatabase(); var id = TestData.AddProduct(t); var service = Service(t); var book = Book(service);
        Edit(book, "store.name", "Workbook shop"); var sheet = book.Sheet("Products")!;
        var row = sheet.Rows.Skip(1).Single(r => r[sheet.Rows[0].IndexOf("Id")] == id.ToString());
        row[sheet.Rows[0].IndexOf("Name")] = "Workbook renamed";
        var preview = Preview(service, book); Assert.True(preview.CanApply, preview.SummaryText);
        Assert.True(t.Settings.Set("store.name", "Concurrent shop", TestData.Owner).Ok); var before = State(t);
        Conflict(service.ApplyBulk(TestData.Owner, preview)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void PreviewValidatesAgainstItsDatabaseSnapshotNotAnOldSharedCache()
    {
        using var t = new TestDatabase(); var service = Service(t); var book = Book(service); Edit(book, "discount.maxPercentCashier", "4");
        var settingsSheet = book.Sheet("Settings")!; var keyColumn = settingsSheet.Rows[0].IndexOf("Key");
        settingsSheet.Rows.RemoveAll(row => row != settingsSheet.Rows[0] && row[keyColumn] != "discount.maxPercentCashier");
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value='2' WHERE key='discount.maxPercentManager';"));
        Assert.Equal(20, t.Settings.GetInt("discount.maxPercentManager"));
        var preview = Preview(service, book); Assert.False(preview.CanApply);
        Assert.Contains(preview.Rows, row => row.IsError && row.Message.Contains("cashier"));
    }

    [Fact]
    public void ValidPairIsPreparedTogetherAndTheSamePlanCannotApplyTwice()
    {
        using var t = new TestDatabase(); var service = Service(t); var book = Book(service);
        Edit(book, "discount.maxPercentCashier", "15"); Edit(book, "discount.maxPercentManager", "30");
        var preview = Preview(service, book); Assert.True(preview.CanApply, preview.SummaryText);
        var result = service.ApplyBulk(TestData.Owner, preview); Assert.True(result.Ok, result.Error);
        Assert.Equal(15, t.Settings.GetInt("discount.maxPercentCashier")); Assert.Equal(30, t.Settings.GetInt("discount.maxPercentManager"));
        var before = State(t); Assert.False(service.ApplyBulk(TestData.Owner, preview).Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void AnotherDatabaseContextCannotApplyThisSettingsPlan()
    {
        using var t = new TestDatabase(); var service = Service(t); var book = Book(service); Edit(book, "store.name", "New shop");
        var preview = Preview(service, book); var db = new Database(t.Database.FilePath, pooling: false, durable: false);
        var audit = new AuditService(db, t.Log); var settings = new SettingsService(db, audit, t.Log, new PermissionService(db, t.Log, audit));
        var before = State(t); var result = Service(t, db, settings).ApplyBulk(TestData.Owner, preview);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, State(t));
    }

    [Fact]
    public void UnrelatedTelemetryDoesNotInvalidateSettingsApply()
    {
        using var t = new TestDatabase(); var service = Service(t); var book = Book(service); Edit(book, "store.name", "New shop");
        var preview = Preview(service, book); Assert.True(t.Settings.SetUiOnly("backup.lastRunAt", "synthetic").Ok);
        Assert.True(service.ApplyBulk(TestData.Owner, preview).Ok);
    }

    [Fact]
    public void AuditFailureRollsBackAndAllowsTheSameReviewedPlanToRetry()
    {
        using var t = new TestDatabase(); var service = Service(t); var book = Book(service); Edit(book, "store.name", "New shop");
        var preview = Preview(service, book);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_workbook BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;"));
        var before = State(t); var failed = service.ApplyBulk(TestData.Owner, preview);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Equal(before, State(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_workbook;"));
        Assert.True(service.ApplyBulk(TestData.Owner, preview).Ok);
    }

    [Fact]
    public void DeferredCommitFailureDoesNotPromiseRollbackOrPermitPlanReuse()
    {
        using var t = new TestDatabase(); var service = Service(t); var book = Book(service); Edit(book, "store.name", "New shop");
        var preview = Preview(service, book);
        t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_workbook_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_workbook_commit_audit AFTER INSERT ON audit_logs BEGIN
INSERT INTO synthetic_workbook_commit(parent_id) VALUES(-99999); END;"));
        var before = State(t); var failed = service.ApplyBulk(TestData.Owner, preview);
        Assert.False(failed.Ok); Assert.False(failed.NoCommitConfirmed); Assert.Contains("unconfirmed", failed.Error!); Assert.Equal(before, State(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_workbook_commit_audit;"));
        Assert.False(service.ApplyBulk(TestData.Owner, preview).Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void MissingSettingsTickSkipsSettingsAndCannotApplyThem()
    {
        using var t = new TestDatabase(); var service = Service(t); var book = Book(service); Edit(book, "store.name", "New shop");
        var preview = Preview(service, book, settings: false); Assert.False(preview.CanApply); var before = State(t);
        Assert.False(service.ApplyBulk(TestData.Owner, preview).Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void CurrentOwnerDeactivationRefusesAnOldSettingsPreview()
    {
        using var t = new TestDatabase(); var service = Service(t); var book = Book(service); Edit(book, "store.name", "New shop");
        var preview = Preview(service, book); t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;"));
        var before = State(t); var result = service.ApplyBulk(TestData.Owner, preview); Assert.False(result.Ok); Assert.Equal(before, State(t));
    }
}
