using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated public permission-editor cases.NOT execution/authentication/WPF proof.</summary>
public sealed class PermissionEditConcurrencyTests
{
    private static PermissionService Permissions(TestDatabase t, Database? db = null)
    { db ??= t.Database; return new PermissionService(db, t.Log, new AuditService(db, t.Log)); }
    private static PermissionEditForm Load(PermissionService p, User target, User? owner = null)
    {
        var result = p.LoadEditForm(owner ?? TestData.Owner, target.Id);
        Assert.True(result.Ok, result.Error); Assert.NotNull(result.Value); return result.Value!;
    }
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n", new[] { "users", "user_permissions", "audit_logs", "sqlite_sequence", "sales", "payments" }.Select(table =>
    {
        var columns = t.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
        var projection = string.Join("||':'||", columns.Select(col => "quote(\"" + col + "\")"));
        return table + "=" + string.Join(";", t.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
    })));
    private static void Conflict(ServiceResult result)
    { Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Contains("changed", result.Error!); }

    [Fact]
    public void TwoEditorsCannotOverwriteTheFirstSavedGrant()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); var one = Load(p, target); var two = Load(p, target);
        Assert.True(p.Set(TestData.Owner, target.Id, Modules.Reports, true, one.EditVersion).Ok); var before = State(t);
        Conflict(p.Set(TestData.Owner, target.Id, Modules.Reports, false, two.EditVersion)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void AnotherModuleChangeAlsoInvalidatesTheWholeLoadedList()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); var old = Load(p, target);
        Assert.True(p.Set(TestData.Owner, target.Id, Modules.Products, true).Ok); var before = State(t);
        Conflict(p.Set(TestData.Owner, target.Id, Modules.Reports, true, old.EditVersion)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void StaleResetCannotDeleteANewerPermissionGrant()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); var old = Load(p, target);
        Assert.True(p.Set(TestData.Owner, target.Id, Modules.Reports, true).Ok); var before = State(t);
        Conflict(p.ResetToRoleDefaults(TestData.Owner, target.Id, old.EditVersion)); Assert.Equal(before, State(t)); Assert.True(p.Can(target, Modules.Reports));
    }

    [Fact]
    public void MissingRowAndEquivalentExplicitDefaultAreDifferentStates()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); var old = Load(p, target); Assert.False(old.Values[Modules.Reports]);
        Assert.True(p.Set(TestData.Owner, target.Id, Modules.Reports, false).Ok); Assert.False(Load(p, target).Values[Modules.Reports]);
        Conflict(p.Set(TestData.Owner, target.Id, Modules.Products, true, old.EditVersion));
    }

    [Theory]
    [InlineData("UPDATE users SET role='manager' WHERE id=@id;")]
    [InlineData("UPDATE users SET active=0 WHERE id=@id;")]
    public void TargetRoleOrActiveChangeInvalidatesSetAndReset(string sql)
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); var old = Load(p, target);
        t.Database.Tx(c => t.Database.Execute(c, sql, new { id = target.Id })); var before = State(t);
        Conflict(p.Set(TestData.Owner, target.Id, Modules.Reports, true, old.EditVersion));
        Conflict(p.ResetToRoleDefaults(TestData.Owner, target.Id, old.EditVersion)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void AnotherHandleChangeIsSeenBeforeTheFirstWrite()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); var old = Load(p, target);
        var other = Permissions(t, new Database(t.Database.FilePath, pooling: false, durable: false)); var fresh = Load(other, target);
        Assert.True(other.Set(TestData.Owner, target.Id, Modules.Reports, true, fresh.EditVersion).Ok); var before = State(t);
        Conflict(p.Set(TestData.Owner, target.Id, Modules.Products, true, old.EditVersion)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void TokenForAnotherTargetCannotChangeOrResetThisTarget()
    {
        using var t = new TestDatabase(); var first = TestData.Cashier(t); var second = TestData.AddUser(t, "manager"); var p = Permissions(t); var old = Load(p, first);
        Conflict(p.Set(TestData.Owner, second.Id, Modules.Reports, false, old.EditVersion)); Conflict(p.ResetToRoleDefaults(TestData.Owner, second.Id, old.EditVersion));
    }

    [Fact]
    public void AnotherDatabaseInstanceTokenCannotAuthorizeAnEdit()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var other = Permissions(t, new Database(t.Database.FilePath, pooling: false, durable: false)); var old = Load(other, target);
        Conflict(Permissions(t).Set(TestData.Owner, target.Id, Modules.Reports, true, old.EditVersion));
    }

    [Fact]
    public void PricingScopeTokenCannotAuthorizePermissionChanges()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var pricing = new PricingAllowanceService(t.Database, t.Audit, t.Log).ForUser(TestData.Owner, target);
        Conflict(Permissions(t).Set(TestData.Owner, target.Id, Modules.Reports, true, pricing.EditVersion));
    }

    [Fact]
    public void OwnerDeactivationCannotBeBypassedByAValidLoadedToken()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); var old = Load(p, target);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;")); var before = State(t);
        Assert.False(p.Set(TestData.Owner, target.Id, Modules.Reports, true, old.EditVersion).Ok);
        Assert.False(p.ResetToRoleDefaults(TestData.Owner, target.Id, old.EditVersion).Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void AStaffGranteeCannotUseTheOwnersLoadedToken()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); var old = Load(p, target);
        Assert.True(p.Set(TestData.Owner, target.Id, Modules.Staff, true).Ok); var before = State(t);
        Assert.False(p.Set(target, target.Id, Modules.Reports, true, old.EditVersion).Ok); Assert.False(p.LoadEditForm(target, target.Id).Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void StoredTargetRoleNotSuppliedPickerTextDeterminesTheMap()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var supplied = new User { Id = target.Id, Role = "owner", Name = "synthetic supplied owner" };
        var p = Permissions(t); var map = p.MapFor(TestData.Owner, supplied); Assert.False(map[Modules.Reports]); Assert.True(map[Modules.Pos]);
        Assert.Equal(UserRole.Cashier, Load(p, target).Role);
    }

    [Fact]
    public void FailedReadIsNotASuccessfulDefaultPermissionEditor()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t);
        t.Database.Tx(c => t.Database.Execute(c, "ALTER TABLE user_permissions RENAME TO synthetic_hidden_permissions;"));
        var result = p.LoadEditForm(TestData.Owner, target.Id); Assert.False(result.Ok); Assert.Null(result.Value);
        Assert.All(p.MapFor(TestData.Owner, target).Values, allowed => Assert.False(allowed));
        Assert.All(p.MapFor(target).Values, allowed => Assert.False(allowed));
    }

    [Fact]
    public void CheckedEnabledEditorNeedsProofAndBaselineWhileImperativeCallsRemainCompatible()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); t.Database.Sessions.Enable();
        Assert.False(p.LoadEditForm(TestData.Owner, target.Id).Ok);
        var login = new AuthService(t.Database, t.Audit, t.Settings, t.Log).Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        var owner = login.Value!; Conflict(p.Set(owner, target.Id, Modules.Reports, true, null)); Conflict(p.ResetToRoleDefaults(owner, target.Id, null));
        var loaded = Load(p, target, owner); Assert.True(p.Set(owner, target.Id, Modules.Reports, true, loaded.EditVersion).Ok);
        Assert.True(p.Set(owner, target.Id, Modules.Products, true).Ok); // Existing imperative intent remains compatible.
        Assert.True(p.ResetToRoleDefaults(owner, target.Id).Ok);
    }

    [Fact]
    public void ExactSetHandoffAllowsNextEditButDoesNotBlessALaterWriter()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); var old = Load(p, target);
        var first = p.Set(TestData.Owner, target.Id, Modules.Reports, true, old.EditVersion); Assert.True(first.Ok, first.Error); Assert.True(first.PermissionEdit!.Values[Modules.Reports]);
        var next = p.Set(TestData.Owner, target.Id, Modules.Products, true, first.EditVersion); Assert.True(next.Ok, next.Error);
        Conflict(p.Set(TestData.Owner, target.Id, Modules.Cash, true, first.EditVersion));
    }

    [Fact]
    public void ResetReturnsExactRoleDefaultsAndNextTokenWithoutFreshRead()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); Assert.True(p.Set(TestData.Owner, target.Id, Modules.Reports, true).Ok);
        var old = Load(p, target); var reset = p.ResetToRoleDefaults(TestData.Owner, target.Id, old.EditVersion); Assert.True(reset.Ok, reset.Error);
        Assert.False(reset.PermissionEdit!.Values[Modules.Reports]); Assert.True(reset.PermissionEdit.Values[Modules.Pos]);
        Assert.True(p.Set(TestData.Owner, target.Id, Modules.Products, true, reset.EditVersion).Ok);
        Assert.False(reset.PermissionEdit.Values[Modules.Products]); // Detached result was not rebased by that later Save.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuditFailureRollsBackSetOrResetAndOriginalTokenCanRetry(bool reset)
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); Assert.True(p.Set(TestData.Owner, target.Id, Modules.Reports, true).Ok); var old = Load(p, target);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_permissions BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;"));
        var before = State(t); var failed = reset ? p.ResetToRoleDefaults(TestData.Owner, target.Id, old.EditVersion) : p.Set(TestData.Owner, target.Id, Modules.Products, true, old.EditVersion);
        Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Null(failed.PermissionEdit); Assert.Equal(before, State(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_permissions;"));
        var retry = reset ? p.ResetToRoleDefaults(TestData.Owner, target.Id, old.EditVersion) : p.Set(TestData.Owner, target.Id, Modules.Products, true, old.EditVersion); Assert.True(retry.Ok, retry.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredCommitFailureCannotReturnASuccessTokenOrSafeRetryVerdict(bool reset)
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); Assert.True(p.Set(TestData.Owner, target.Id, Modules.Reports, true).Ok); var old = Load(p, target);
        t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_permission_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_permission_commit_audit AFTER INSERT ON audit_logs BEGIN INSERT INTO synthetic_permission_commit(parent_id) VALUES(-99999); END;"));
        var before = State(t); var failed = reset ? p.ResetToRoleDefaults(TestData.Owner, target.Id, old.EditVersion) : p.Set(TestData.Owner, target.Id, Modules.Products, true, old.EditVersion);
        Assert.False(failed.Ok); Assert.False(failed.NoCommitConfirmed); Assert.Null(failed.PermissionEdit); Assert.Null(failed.EditVersion); Assert.Contains("unconfirmed", failed.Error!); Assert.Equal(before, State(t));
    }

    [Fact]
    public void OwnerFinancialCheckboxesRemainIndependentButOtherOwnerGrantsAndResetStayProtected()
    {
        using var t = new TestDatabase(); var p = Permissions(t); var old = Load(p, TestData.Owner);
        var profit = p.Set(TestData.Owner, 1, Modules.Profit, false, old.EditVersion); Assert.True(profit.Ok, profit.Error);
        var cost = p.Set(TestData.Owner, 1, Modules.PurchaseCost, false, profit.EditVersion); Assert.True(cost.Ok, cost.Error);
        Assert.False(cost.PermissionEdit!.Values[Modules.Profit]); Assert.False(cost.PermissionEdit.Values[Modules.PurchaseCost]); Assert.True(cost.PermissionEdit.Values[Modules.Settings]);
        Assert.False(p.Set(TestData.Owner, 1, Modules.Settings, false, cost.EditVersion).Ok); Assert.False(p.ResetToRoleDefaults(TestData.Owner, 1, cost.EditVersion).Ok);
    }

    [Fact]
    public void InactiveTargetCanStillBeReviewedAndChangedUnderAFreshOwnerForm()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=@id;", new { id = target.Id }));
        var fresh = Load(p, target); Assert.True(p.Set(TestData.Owner, target.Id, Modules.Reports, true, fresh.EditVersion).Ok);
    }

    [Fact]
    public void ResultMapIsImmutableAndTokensAreExcludedFromJson()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var p = Permissions(t); var form = Load(p, target);
        Conflict(p.Set(TestData.Owner, target.Id, Modules.Reports, true, null));
        Conflict(p.ResetToRoleDefaults(TestData.Owner, target.Id, null));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, bool>)form.Values)[Modules.Reports] = true);
        Assert.DoesNotContain("EditVersion", JsonSerializer.Serialize(form));
        var saved = p.Set(TestData.Owner, target.Id, Modules.Reports, true, form.EditVersion); Assert.True(saved.Ok, saved.Error);
        Assert.DoesNotContain("PermissionEdit", JsonSerializer.Serialize(saved)); Assert.DoesNotContain("EditVersion", JsonSerializer.Serialize(saved));
    }
}
