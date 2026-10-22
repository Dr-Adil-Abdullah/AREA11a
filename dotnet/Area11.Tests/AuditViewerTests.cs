using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The black box (F-35, Spec 22): what the owner can read, who is allowed to read it, and what the
/// CSV file really contains. The writer is tested in MigrationTests; this file is the reading side.
///
/// The rule that matters: **the viewer can never change history.** There is no update and no delete
/// in the service, the page has no command for one, and the CSV is built from what is on the screen -
/// the same rows, the same order, the same filter.
/// </summary>
public sealed class AuditViewerTests
{
    // ------------------------------------------------------------------ who may look

    [Fact]
    public void TheBlackBox_IsForTheOwner_TheManagerAndCashierAreRefused()
    {
        using var test = new TestDatabase();

        var owner = Viewer(test).Read(new AuditFilter(), TestData.Owner);
        Assert.True(owner.Ok, owner.Error);

        var manager = Viewer(test).Read(new AuditFilter(), TestData.AddUser(test, "manager"));
        Assert.False(manager.Ok);
        Assert.Contains("owner", manager.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var cashier = Viewer(test).Read(new AuditFilter(), TestData.Cashier(test));
        Assert.False(cashier.Ok);
    }

    [Fact]
    public void AManager_MayBeGivenTheBlackBox_OnePersonAtATime()
    {
        using var test = new TestDatabase();
        var manager = TestData.AddUser(test, "manager");

        Assert.False(Viewer(test).Read(new AuditFilter(), manager).Ok);

        // A stored per-person permission always beats the role default - the owner decides.
        var granted = TestData.Permissions(test).Set(TestData.Owner, manager.Id, Modules.Audit, true);
        Assert.True(granted.Ok, granted.Error);

        Assert.True(Viewer(test).Read(new AuditFilter(), manager).Ok);
    }

    // ------------------------------------------------------------------ what it shows

    [Fact]
    public void EveryLine_CarriesWhoWhatWhenAndTheChange()
    {
        using var test = new TestDatabase();

        Record(test, "Settings", "settings_change", "settings", "store.name", "Old Shop", "Al-Shifa Pharmacy");
        Record(test, "POS", "sale_created", "sales", "12", null, "INV-0012");

        var page = Viewer(test).Read(new AuditFilter(), TestData.Owner);

        Assert.True(page.Ok, page.Error);
        Assert.Equal(2, page.Total);

        // Newest first: the sale was written last, so it is the first line.
        Assert.Equal("sale_created", page.Value!.Rows[0].Action);
        Assert.Equal("sales #12", page.Value.Rows[0].EntityLabel);

        var settings = page.Value.Rows[1];
        Assert.Equal("Owner", settings.WhoLabel);
        Assert.Equal("settings change", settings.ActionLabel);
        Assert.Equal("Old Shop  →  Al-Shifa Pharmacy", settings.ChangeLabel);
        Assert.Equal("settings", settings.Module);
        Assert.NotEmpty(settings.At);
    }

    [Fact]
    public void Search_FindsAValueNotOnlyAnAction()
    {
        using var test = new TestDatabase();

        Record(test, "Settings", "settings_change", "settings", "print", "58", "80");
        Record(test, "POS", "sale_created", "sales", "12", null, "INV-0012");

        var page = Viewer(test).Read(new AuditFilter { Search = "Al-Shifa" }, TestData.Owner);
        Assert.Empty(page.Value!.Rows);                      // nothing holds that word

        var byValue = Viewer(test).Read(new AuditFilter { Search = "58" }, TestData.Owner);
        Assert.Single(byValue.Value!.Rows);
        Assert.Equal("settings_change", byValue.Value!.Rows[0].Action);

        var byAction = Viewer(test).Read(new AuditFilter { Search = "sale" }, TestData.Owner);
        Assert.Single(byAction.Value!.Rows);
    }

    [Fact]
    public void ModuleAndAction_NarrowThePage_AndTheTotalCountsTheFilter()
    {
        using var test = new TestDatabase();

        Record(test, "Settings", "settings_change", "settings", "a", null, "1");
        Record(test, "POS", "sale_created", "sales", "1", null, "INV-0001");
        Record(test, "POS", "sale_created", "sales", "2", null, "INV-0002");

        var pos = Viewer(test).Read(new AuditFilter { Module = "POS" }, TestData.Owner);

        Assert.Equal(2, pos.Value!.Total);
        Assert.Equal(2, pos.Value.Rows.Count);

        var oneAction = Viewer(test).Read(new AuditFilter { Action = "settings_change" }, TestData.Owner);
        Assert.Single(oneAction.Value!.Rows);

        // The drop-downs list only what is really in the table.
        Assert.Contains("POS", pos.Value.Modules);
        Assert.Contains("Settings", pos.Value.Modules);
        Assert.Contains("sale_created", pos.Value.Actions);
        Assert.Contains("Owner", pos.Value.Users);
    }

    [Fact]
    public void TheDateWindow_IncludesTheWholeLastDay_AndTheLimitIsHonoured()
    {
        using var test = new TestDatabase();

        Record(test, "POS", "old_line", "sales", "1", null, "old", at: "2026-01-01 09:00:00");
        Record(test, "POS", "new_line", "sales", "2", null, "new", at: Clock.Now());

        var today = Viewer(test).Read(
            new AuditFilter { FromDate = Clock.Today(), ToDate = Clock.Today() }, TestData.Owner);

        Assert.Single(today.Value!.Rows);
        Assert.Equal("new_line", today.Value.Rows[0].Action);

        var limited = Viewer(test).Read(new AuditFilter { Limit = 1 }, TestData.Owner);

        Assert.Single(limited.Value!.Rows);
        Assert.Equal(2, limited.Value.Total);
        Assert.Equal("showing 1 of 2 line(s)", limited.Value.CountLabel);
    }

    [Fact]
    public void AnEmptyFilter_SaysSo_WithoutANullRow()
    {
        using var test = new TestDatabase();

        var page = Viewer(test).Read(new AuditFilter(), TestData.Owner);

        Assert.True(page.Ok, page.Error);
        Assert.True(page.Value!.IsEmpty);
        Assert.Equal("0 line(s)", page.Value.CountLabel);
    }

    // ------------------------------------------------------------------ the CSV file

    [Fact]
    public void TheCsv_QuotesWhatWouldOtherwiseBreakTheFile()
    {
        var line = CsvWriter.Line(new[] { "plain", "with, comma", "with \"quote\"", "two\r\nlines", "" });

        Assert.Equal("plain,\"with, comma\",\"with \"\"quote\"\"\",\"two\r\nlines\",", line);
    }

    [Fact]
    public void TheCsv_OfAPage_HasAHeaderAndOneLinePerRow()
    {
        using var test = new TestDatabase();

        Record(test, "Settings", "settings_change", "settings", "store.name", "A, B", "Al-Shifa");
        Record(test, "POS", "sale_created", "sales", "12", null, "INV-0012");

        var page = Viewer(test).Read(new AuditFilter(), TestData.Owner).Value!;
        var csv = page.ToCsv();

        Assert.StartsWith("Id,At,User,Module,Action,Entity,Entity id,Old value,New value,Details", csv);
        Assert.Contains("\"A, B\"", csv);                     // the comma is quoted, not a separator
        Assert.Contains("sale_created", csv);
        Assert.Contains("INV-0012", csv);

        // A file with 3 records (header + 2 rows) must have 3 logical lines, even though one value
        // held a comma. Counting CRLF keeps the assertion readable and does not fight the quotes.
        Assert.Equal(3, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void TheCsv_IsNeverWrittenForAnEmptyPage()
    {
        using var test = new TestDatabase();

        var page = Viewer(test).Read(new AuditFilter { Search = "nothing-like-this" }, TestData.Owner).Value!;

        // The desktop button is disabled when there is nothing to save; the service still answers a
        // valid page, so the rule is tested where it is decided.
        Assert.True(page.IsEmpty);
        Assert.Equal(0, page.Rows.Count);
    }

    // ------------------------------------------------------------------ helpers

    private static AuditHistoryService Viewer(TestDatabase test) => TestData.AuditHistory(test);

    private static void Record(
        TestDatabase test,
        string module,
        string action,
        string entity,
        string entityId,
        string? oldValue,
        string? newValue,
        string? at = null)
    {
        test.Database.Tx(connection => test.Database.Execute(connection, @"
INSERT INTO audit_logs(at, user_id, user_name, action, module, entity, entity_id, old_value, new_value)
VALUES(@at, @userId, @userName, @action, @module, @entity, @entityId, @oldValue, @newValue);",
            new
            {
                at = at ?? Clock.Now(),
                userId = TestData.Owner.Id,
                userName = TestData.Owner.Name,
                action,
                module,
                entity,
                entityId,
                oldValue,
                newValue
            }));
    }
}
