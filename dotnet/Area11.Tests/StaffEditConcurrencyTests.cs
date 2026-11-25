using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated public-service staff editor cases. UNRUN without the .NET SDK;not WPF proof.</summary>
public sealed class StaffEditConcurrencyTests
{
    private static AuthService Auth(TestDatabase t, Database? db = null)
    {
        db ??= t.Database;
        var audit = new AuditService(db, t.Log);
        return new AuthService(db, audit, new SettingsService(db, audit, t.Log), t.Log);
    }
    private static User Load(AuthService auth, int id, User? owner = null)
    {
        var result = auth.ListUsers(owner ?? TestData.Owner);
        Assert.True(result.Ok, result.Error);
        var user = Assert.Single(result.Value!, x => x.Id == id);
        Assert.NotNull(user.StaffForm); return user;
    }
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n", new[] { "users", "audit_logs", "sqlite_sequence", "sales", "payments" }.Select(table =>
    {
        var columns = t.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
        var projection = string.Join("||':'||", columns.Select(col => "quote(\"" + col + "\")"));
        return table + "=" + string.Join(";", t.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
    })));
    private static void Conflict(ServiceResult r)
    { Assert.False(r.Ok); Assert.True(r.NoCommitConfirmed); Assert.Contains("changed", r.Error!); Assert.Null(r.StaffEdit); Assert.Null(r.EditVersion); }
    private static ServiceResult Save(AuthService a, User target, bool secret, EditVersion? version, User? owner = null) => secret
        ? a.SetSecret(owner ?? TestData.Owner, target.Id, "9876", target.IsOwner, version)
        : a.SetActive(owner ?? TestData.Owner, target.Id, !target.Active, version);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FirstToggleOrResetInvalidatesBothOldActions(bool firstSecret, bool secondSecret)
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var a = Auth(t);
        var one = Load(a, target.Id); var two = Load(a, target.Id);
        Assert.True(Save(a, one, firstSecret, one.StaffForm!.EditVersion).Ok); var before = State(t);
        Conflict(Save(a, two, secondSecret, two.StaffForm!.EditVersion)); Assert.Equal(before, State(t));
    }

    [Theory]
    [InlineData("name='Renamed'")]
    [InlineData("role='manager'")]
    [InlineData("active=0")]
    [InlineData("password_hash='synthetic-other-password'")]
    [InlineData("pin_hash='synthetic-other-pin'")]
    public void EveryLoadedStaffDependencyInvalidatesBothActions(string assignment)
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var a = Auth(t); var old = Load(a, target.Id);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET " + assignment + " WHERE id=@id;", new { id = target.Id }));
        var before = State(t);
        Conflict(Save(a, old, false, old.StaffForm!.EditVersion)); Conflict(Save(a, old, true, old.StaffForm.EditVersion)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void DeletedTargetDoesNotReappearFromAnOldForm()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var a = Auth(t); var old = Load(a, target.Id);
        t.Database.Tx(c => t.Database.Execute(c, "DELETE FROM users WHERE id=@id;", new { id = target.Id })); var before = State(t);
        var result = Save(a, old, true, old.StaffForm!.EditVersion); Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, State(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitNullIsRejectedEvenInTheLegacyHost(bool secret)
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var a = Auth(t); var before = State(t);
        Conflict(Save(a, target, secret, null)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void ContextTargetAndScopeAreBoundToTheToken()
    {
        using var t = new TestDatabase(); var target = TestData.Cashier(t); var other = TestData.AddUser(t, "manager"); var a = Auth(t); var old = Load(a, target.Id);
        Conflict(Save(a, other, true, old.StaffForm!.EditVersion));
        var db = new Database(t.Database.FilePath, pooling: false, durable: false); var otherHandle = Auth(t, db);
        Conflict(Save(otherHandle, target, false, old.StaffForm.EditVersion));
        var permissions = new PermissionService(t.Database, t.Log, t.Audit).LoadEditForm(TestData.Owner, target.Id);
        Assert.True(permissions.Ok, permissions.Error); Conflict(Save(a, target, true, permissions.Value!.EditVersion));
        var fresh = Load(otherHandle, target.Id); Assert.True(Save(otherHandle, target, true, fresh.StaffForm!.EditVersion).Ok);
        Conflict(Save(a, target, false, old.StaffForm.EditVersion));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckedCredentialColumnMustMatchTheStoredRole(bool ownerTarget)
    {
        using var t = new TestDatabase(); var target = ownerTarget ? TestData.Owner : TestData.Cashier(t); var a = Auth(t); var old = Load(a, target.Id); var before = State(t);
        var wrong = a.SetSecret(TestData.Owner, target.Id, "9876", !ownerTarget, old.StaffForm!.EditVersion);
        Assert.False(wrong.Ok); Assert.True(wrong.NoCommitConfirmed); Assert.Equal(before, State(t));
        Assert.True(a.SetSecret(TestData.Owner, target.Id, "9876", ownerTarget, old.StaffForm.EditVersion).Ok);
    }

    [Fact]
    public void OwnerCannotBeDeactivatedAndFreshInactiveStaffCanBeAdministered()
    {
        using var t = new TestDatabase(); var a = Auth(t); var owner = Load(a, 1); var before = State(t);
        var denied = a.SetActive(TestData.Owner, 1, false, owner.StaffForm!.EditVersion); Assert.False(denied.Ok); Assert.True(denied.NoCommitConfirmed); Assert.Equal(before, State(t));
        var target = TestData.Cashier(t); Assert.True(a.SetActive(TestData.Owner, target.Id, false).Ok); var inactive = Load(a, target.Id);
        var reset = a.SetSecret(TestData.Owner, target.Id, "9876", false, inactive.StaffForm!.EditVersion); Assert.True(reset.Ok, reset.Error);
        Assert.True(a.SetActive(TestData.Owner, target.Id, true, reset.EditVersion).Ok);
    }

    [Fact]
    public void ExactHandoffAdvancesButDoesNotBlessASubsequentWriter()
    {
        using var t = new TestDatabase(); var a = Auth(t); var target = TestData.Cashier(t); var old = Load(a, target.Id);
        var saved = Save(a, target, false, old.StaffForm!.EditVersion); Assert.True(saved.Ok, saved.Error); Assert.False(saved.StaffEdit!.StaffForm!.Active);
        Assert.True(a.SetSecret(TestData.Owner, target.Id, "9876", false, saved.EditVersion).Ok);
        Assert.False(saved.StaffEdit.StaffForm.Active); Assert.True(old.StaffForm.Active);
        Conflict(a.SetActive(TestData.Owner, target.Id, true, saved.EditVersion));
    }

    [Fact]
    public void RosterFormIsImmutableAndDetachedFromMutablePickerFields()
    {
        using var t = new TestDatabase(); var a = Auth(t); var target = TestData.Cashier(t); var row = Load(a, target.Id); var form = row.StaffForm!;
        row.Id = 999999; row.Name = "fake"; row.Role = "owner"; row.Active = false;
        Assert.Equal(target.Id, form.UserId); Assert.Equal(UserRole.Cashier, form.Role); Assert.True(form.Active); Assert.NotEqual(row.Name, form.Name);
        Assert.All(typeof(StaffEditForm).GetProperties(), p => Assert.Null(p.SetMethod));
        var saved = a.SetActive(TestData.Owner, form.UserId, !form.Active, form.EditVersion); Assert.True(saved.Ok, saved.Error);
        Assert.Equal(target.Id, saved.StaffEdit!.Id); Assert.False(saved.StaffEdit.Active);
    }

    [Fact]
    public void RosterAndResultDoNotSerializeTokensOrCredentialMaterial()
    {
        using var t = new TestDatabase(); var a = Auth(t); var target = TestData.Cashier(t); var row = Load(a, target.Id);
        Assert.DoesNotContain("StaffForm", JsonSerializer.Serialize(row)); Assert.DoesNotContain("EditVersion", JsonSerializer.Serialize(row.StaffForm));
        var saved = Save(a, target, true, row.StaffForm!.EditVersion); Assert.True(saved.Ok, saved.Error);
        var json = JsonSerializer.Serialize(saved); Assert.DoesNotContain("StaffEdit", json); Assert.DoesNotContain("EditVersion", json); Assert.DoesNotContain("9876", json);
        Assert.DoesNotContain("hash", JsonSerializer.Serialize(saved.StaffEdit), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFailedRosterReadCannotInventAWorkingEditor()
    {
        using var t = new TestDatabase(); var a = Auth(t);
        t.Database.Tx(c => t.Database.Execute(c, "ALTER TABLE users RENAME COLUMN pin_hash TO synthetic_hidden_pin;"));
        var result = a.ListUsers(TestData.Owner); Assert.False(result.Ok); Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuditFailureRollsBackAndOriginalFormCanRetry(bool secret)
    {
        using var t = new TestDatabase(); var a = Auth(t); var target = TestData.Cashier(t); var row = Load(a, target.Id);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_staff_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;")); var before = State(t);
        var failed = Save(a, target, secret, row.StaffForm!.EditVersion); Assert.False(failed.Ok); Assert.True(failed.NoCommitConfirmed); Assert.Null(failed.StaffEdit); Assert.Equal(before, State(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_staff_audit;")); Assert.True(Save(a, target, secret, row.StaffForm.EditVersion).Ok);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredCommitFailureIsUnknownAndReturnsNoHandoff(bool secret)
    {
        using var t = new TestDatabase(); var a = Auth(t); var target = TestData.Cashier(t); var row = Load(a, target.Id);
        t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_staff_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_staff_commit_audit AFTER INSERT ON audit_logs BEGIN INSERT INTO synthetic_staff_commit(parent_id) VALUES(-99999); END;")); var before = State(t);
        var failed = Save(a, target, secret, row.StaffForm!.EditVersion); Assert.False(failed.Ok); Assert.False(failed.NoCommitConfirmed); Assert.Null(failed.StaffEdit); Assert.Null(failed.EditVersion); Assert.Contains("unconfirmed", failed.Error!); Assert.Equal(before, State(t));
    }

    [Fact]
    public void CurrentOwnerAndInteractiveProofStillRequired()
    {
        using var t = new TestDatabase(); var a = Auth(t); var target = TestData.Cashier(t); var row = Load(a, target.Id);
        Assert.False(a.ListUsers(target).Ok); Assert.False(Save(a, target, true, row.StaffForm!.EditVersion, target).Ok);
        t.Database.Sessions.Enable(); Assert.False(a.ListUsers(TestData.Owner).Ok); Assert.False(Save(a, target, false, row.StaffForm.EditVersion).Ok);
        var login = a.Login("admin", "area11"); Assert.True(login.Ok, login.Error); var owner = login.Value!; var fresh = Load(a, target.Id, owner);
        Assert.True(Save(a, target, true, fresh.StaffForm!.EditVersion, owner).Ok);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;")); var before = State(t);
        Assert.False(Save(a, target, false, fresh.StaffForm!.EditVersion, owner).Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void ExistingImperativeCallsRetainTheirIntentInEnabledAndDisabledHosts()
    {
        using var t = new TestDatabase(); var a = Auth(t); var target = TestData.Cashier(t);
        Assert.True(a.SetActive(TestData.Owner, target.Id, false).Ok); Assert.True(a.SetSecret(TestData.Owner, target.Id, "9876", false).Ok);
        t.Database.Sessions.Enable(); var login = a.Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        Assert.True(a.SetActive(login.Value!, target.Id, true).Ok); Assert.True(a.SetSecret(login.Value!, target.Id, "1234", false).Ok);
    }
}
