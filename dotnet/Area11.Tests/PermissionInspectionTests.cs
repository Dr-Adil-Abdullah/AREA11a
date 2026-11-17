using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Owner-only read inspector. Gesture/timer behavior still needs actual Windows evidence.</summary>
public sealed class PermissionInspectionTests
{
    private static PermissionInspectionService Inspector(TestDatabase t) => new(t.Database, t.Log);
    private static PermissionInspection Read(TestDatabase t, string module)
    {
        var result = Inspector(t).Inspect(TestData.Owner, module);
        Assert.True(result.Ok, result.Error); return result.Value!;
    }
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static void Grant(TestDatabase t, User user, string module, bool allowed) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, module, allowed).Ok);

    [Fact]
    public void EveryExistingCheckboxHasSummaryScopeAndPrerequisites()
    {
        Assert.All(Modules.All, module =>
        {
            var help = PermissionDocumentation.For(module);
            Assert.Equal(module, help.Module); Assert.False(string.IsNullOrWhiteSpace(help.Title));
            Assert.False(string.IsNullOrWhiteSpace(help.Summary)); Assert.False(string.IsNullOrWhiteSpace(help.Scope));
            Assert.False(string.IsNullOrWhiteSpace(help.Prerequisites)); Assert.False(string.IsNullOrWhiteSpace(help.RolloutNote));
        });
    }

    [Fact]
    public void SensitiveSectionKeepsTwoFinancialKeysAndBothExistingLedgerKeys()
    {
        Assert.True(PermissionDocumentation.IsSensitive(Modules.Profit));
        Assert.True(PermissionDocumentation.IsSensitive(Modules.PurchaseCost));
        Assert.True(PermissionDocumentation.IsSensitive(Modules.Suppliers));
        Assert.True(PermissionDocumentation.IsSensitive(Modules.Customers));
        Assert.False(PermissionDocumentation.IsSensitive(Modules.Pos));
        Assert.NotEqual(PermissionDocumentation.For(Modules.Profit).Title, PermissionDocumentation.For(Modules.PurchaseCost).Title);
    }

    [Fact]
    public void AnonymousAndInactiveCallerCannotInspectAssignments()
    {
        using var t = new TestDatabase(); var service = Inspector(t);
        Assert.False(service.Inspect(null, Modules.Profit).Ok);
        Assert.False(service.Inspect(new User { Id = 1, Role = "owner", Active = false }, Modules.Profit).Ok);
    }

    [Fact]
    public void ManagerWithStaffAndSettingsGrantsStillCannotInspect()
    {
        using var t = new TestDatabase(); var manager = TestData.AddUser(t, "manager");
        Grant(t, manager, Modules.Staff, true); Grant(t, manager, Modules.Settings, true);
        Assert.False(Inspector(t).Inspect(manager, Modules.PurchaseCost).Ok);
    }

    [Fact]
    public void ForgedAndMissingOwnerIdsCannotReadStaffList()
    {
        using var t = new TestDatabase(); var cashier = TestData.Cashier(t); var service = Inspector(t);
        Assert.False(service.Inspect(new User { Id = cashier.Id, Role = "owner" }, Modules.Suppliers).Ok);
        Assert.False(service.Inspect(new User { Id = int.MaxValue, Role = "owner" }, Modules.Suppliers).Ok);
    }

    [Fact]
    public void DeactivatedOwnerCannotRefreshPreviouslySuccessfulInspector()
    {
        using var t = new TestDatabase(); var service = Inspector(t);
        Assert.True(service.Inspect(TestData.Owner, Modules.Profit).Ok);
        Execute(t, "UPDATE users SET active=0 WHERE id=1;");
        var denied = service.Inspect(TestData.Owner, Modules.Profit);
        Assert.False(denied.Ok); Assert.Null(denied.Value);
    }

    [Fact]
    public void DemotedOwnerCannotRefreshPreviouslySuccessfulInspector()
    {
        using var t = new TestDatabase(); var service = Inspector(t);
        Assert.True(service.Inspect(TestData.Owner, Modules.Profit).Ok);
        Execute(t, "UPDATE users SET role='manager' WHERE id=1;");
        Assert.False(service.Inspect(TestData.Owner, Modules.Profit).Ok);
    }

    [Fact]
    public void UnknownPermissionDoesNotBecomeAnUnfilteredQuery()
    {
        using var t = new TestDatabase(); var service = Inspector(t);
        Assert.False(service.Inspect(TestData.Owner, "purchasecost' OR 1=1 --").Ok);
        Assert.False(service.Inspect(TestData.Owner, "").Ok);
        Assert.True(service.Inspect(TestData.Owner, Modules.PurchaseCost).Ok);
    }

    [Fact]
    public void ManagerFinancialDefaultsAreShownDisabled()
    {
        using var t = new TestDatabase(); var manager = TestData.AddUser(t, "manager");
        foreach (var module in new[] { Modules.Profit, Modules.PurchaseCost })
        {
            var row = Read(t, module).People.Single(p => p.UserId == manager.Id);
            Assert.False(row.Assigned); Assert.Equal("Role default", row.Source); Assert.Equal("Disabled", row.AssignedLabel);
        }
    }

    [Fact]
    public void SameInspectorShowsCostGrantAndRevocationOnNextRead()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); var service = Inspector(t);
        Grant(t, user, Modules.PurchaseCost, true);
        var allowed = service.Inspect(TestData.Owner, Modules.PurchaseCost).Value!.People.Single(p => p.UserId == user.Id);
        Assert.True(allowed.Assigned); Assert.Equal("Explicit override", allowed.Source);
        Grant(t, user, Modules.PurchaseCost, false);
        var denied = service.Inspect(TestData.Owner, Modules.PurchaseCost).Value!.People.Single(p => p.UserId == user.Id);
        Assert.False(denied.Assigned); Assert.Equal("Explicit override", denied.Source);
        Assert.False(Read(t, Modules.Profit).People.Single(p => p.UserId == user.Id).Assigned);
    }

    [Fact]
    public void MainOffShowsAssignedGrantSeparatelyFromBlockedAccess()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); Grant(t, user, Modules.PurchaseCost, true);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var data = Read(t, Modules.PurchaseCost); Assert.False(data.MainSwitchOn);
        var row = data.People.Single(p => p.UserId == user.Id);
        Assert.True(row.Assigned); Assert.Equal("Enabled", row.AssignedLabel); Assert.Equal("Blocked: main switch OFF", row.Access);
    }

    [Fact]
    public void InspectorReadsCurrentSavedMainSwitchWithoutSettingsCacheReload()
    {
        using var t = new TestDatabase(); Assert.True(t.Settings.GetBool(MasterFoundationSettings.ProfitShowMargins, true));
        Execute(t, "UPDATE settings SET value='false' WHERE key='profit.showMargins';");
        Assert.False(Read(t, Modules.Profit).MainSwitchOn);
        Execute(t, "UPDATE settings SET value='true' WHERE key='profit.showMargins';");
        Assert.True(Read(t, Modules.Profit).MainSwitchOn);
    }

    [Fact]
    public void OwnerFinancialOverrideIsShownWithoutBlockingInspector()
    {
        using var t = new TestDatabase(); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        var row = Read(t, Modules.PurchaseCost).People.Single(p => p.UserId == 1);
        Assert.False(row.Assigned); Assert.Equal("Explicit override", row.Source);
        Assert.True(Read(t, Modules.Profit).People.Single(p => p.UserId == 1).Assigned);
    }

    [Fact]
    public void NonFinancialOwnerModuleRetainsAutomaticAssignment()
    {
        using var t = new TestDatabase();
        Execute(t, "INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(1,'suppliers',0);");
        var row = Read(t, Modules.Suppliers).People.Single(p => p.UserId == 1);
        Assert.True(row.Assigned); Assert.Equal("Owner automatic", row.Source);
    }

    [Fact]
    public void InactiveStaffRemainListedButBlockedDespiteSavedGrant()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t); Grant(t, user, Modules.Profit, true);
        Execute(t, "UPDATE users SET active=0 WHERE role='cashier';");
        var row = Read(t, Modules.Profit).People.Single(p => p.UserId == user.Id);
        Assert.True(row.Assigned); Assert.False(row.Active); Assert.Equal("Inactive", row.AccountLabel);
        Assert.Equal("Blocked: inactive account", row.Access);
    }

    [Fact]
    public void CurrentStoredNamesAndRolesAppearOnNextRead()
    {
        using var t = new TestDatabase(); var user = TestData.Cashier(t);
        Execute(t, "UPDATE users SET name='Renamed member',role='manager' WHERE role='cashier';");
        var row = Read(t, Modules.Profit).People.Single(p => p.UserId == user.Id);
        Assert.Equal("Renamed member", row.Name); Assert.Equal("Manager", row.RoleLabel);
    }

    [Fact]
    public void DatabaseFailureDoesNotReturnAnOwnerDefaultOrCachedPeople()
    {
        using var t = new TestDatabase(); var service = Inspector(t);
        Assert.NotEmpty(service.Inspect(TestData.Owner, Modules.Profit).Value!.People);
        Execute(t, "DROP TABLE user_permissions;");
        var failed = service.Inspect(TestData.Owner, Modules.Profit);
        Assert.False(failed.Ok); Assert.Null(failed.Value);
    }

    [Fact]
    public void MalformedSavedMainSwitchDoesNotPretendTheInspectorIsEligible()
    {
        using var t = new TestDatabase(); Execute(t, "UPDATE settings SET value='not-json' WHERE key='profit.showMargins';");
        var failed = Inspector(t).Inspect(TestData.Owner, Modules.Profit);
        Assert.False(failed.Ok); Assert.Null(failed.Value);
    }

    [Fact]
    public void MainSwitchDoesNotPretendToDisableTheSupplierModuleItself()
    {
        using var t = new TestDatabase(); var user = TestData.AddUser(t, "manager");
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var data = Read(t, Modules.Suppliers); var row = data.People.Single(p => p.UserId == user.Id);
        Assert.True(row.Assigned); Assert.Contains("Module allowed", row.Access);
        Assert.Contains("not the entire ledger module", data.MainSwitchLabel);
        Assert.Contains("GLOBAL ROLLOUT OPEN", data.Definition.RolloutNote);
    }

    [Fact]
    public void InspectionDoesNotWriteGrantsSettingsAuditsOrFinancialRows()
    {
        using var t = new TestDatabase(); TestData.Cashier(t);
        long Count(string table)
        {
            using var c = t.Database.Open(); return t.Database.QuerySingle<long>(c, "SELECT COUNT(*) FROM " + table + ";");
        }
        var tables = new[] { "user_permissions", "settings", "audit_logs", "sales", "purchases", "payments", "stock_movements" };
        var before = tables.ToDictionary(table => table, Count);
        foreach (var module in Modules.All) Assert.NotEmpty(Read(t, module).People);
        foreach (var table in tables) Assert.Equal(before[table], Count(table));
    }
}
