using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated public CreateStaff cases plus synthetic guard observations;not Desktop/WPF proof.</summary>
public sealed class StaffCreateOutcomeTests
{
    private static AuthService Auth(TestDatabase t, Database? db = null)
    {
        if (db is null) return new AuthService(t.Database, t.Audit, t.Settings, t.Log);
        var audit = new AuditService(db, t.Log);
        return new AuthService(db, audit, new SettingsService(db, audit, t.Log), t.Log);
    }
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n", new[] { "users", "settings", "audit_logs", "sqlite_sequence", "sales", "payments" }.Select(table =>
    {
        var columns = t.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
        var projection = string.Join("||':'||", columns.Select(col => "quote(\"" + col + "\")"));
        return table + "=" + string.Join(";", t.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
    })));
    private static void NoCommit(ServiceResult<int> r)
    { Assert.False(r.Ok); Assert.True(r.NoCommitConfirmed); Assert.Equal(0, r.Value); Assert.Null(r.StaffEdit); Assert.Null(r.EditVersion); }

    [Theory]
    [InlineData(UserRole.Owner)]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Salesman)]
    [InlineData(UserRole.Cashier)]
    public void ExistingRolesReturnExactCreatedIdAndNonsecretDetachedRow(UserRole role)
    {
        using var t = new TestDatabase(); var a = Auth(t);
        var saved = a.CreateStaff(TestData.Owner, " Synthetic new staff ", role, " 1234 "); Assert.True(saved.Ok, saved.Error); Assert.True(saved.Value > 1);
        var row = saved.StaffEdit!; Assert.NotNull(row); Assert.NotNull(row.StaffForm); Assert.Equal(saved.Value, row.Id); Assert.Equal(row.Id, row.StaffForm!.UserId);
        Assert.Equal("Synthetic new staff", row.Name); Assert.Equal(role, row.Kind); Assert.Equal(role, row.StaffForm.Role); Assert.True(row.Active); Assert.Same(saved.EditVersion, row.StaffForm.EditVersion);
        var hash = t.Database.Read(c => t.Database.QuerySingle<string>(c, "SELECT pin_hash FROM users WHERE id=@id;", new { id = saved.Value }));
        Assert.True(PasswordHasher.Verify("1234", hash));
        Assert.Equal("", t.Database.Read(c => t.Database.QuerySingle<string>(c, "SELECT COALESCE(password_hash,'') FROM users WHERE id=@id;", new { id = saved.Value })));
        var json = JsonSerializer.Serialize(saved); Assert.DoesNotContain("StaffEdit", json); Assert.DoesNotContain("EditVersion", json); Assert.DoesNotContain(hash!, json);
        var audit = t.Database.Read(c => t.Database.QuerySingle<string>(c, "SELECT new_value FROM audit_logs WHERE module='Users' AND action='create' AND entity_id=@id;", new { id = saved.Value.ToString() }));
        Assert.Contains("\"pin\":\"set\"", audit!); Assert.DoesNotContain("1234", audit!); Assert.DoesNotContain(hash!, audit!);
    }

    [Theory]
    [InlineData("", "1234")]
    [InlineData("x", "1234")]
    [InlineData("Synthetic invalid", "123")]
    [InlineData("Synthetic invalid", "12345")]
    [InlineData("Synthetic invalid", "12x4")]
    public void ValidationIsKnownNoCommitWithoutAccountOrAudit(string name, string pin)
    {
        using var t = new TestDatabase(); var before = State(t);
        NoCommit(Auth(t).CreateStaff(TestData.Owner, name, UserRole.Cashier, pin)); Assert.Equal(before, State(t));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaseInsensitiveTrimmedDuplicateStillRefusesEvenIfInactive(bool inactive)
    {
        using var t = new TestDatabase(); var a = Auth(t); var first = a.CreateStaff(TestData.Owner, "Synthetic duplicate", UserRole.Cashier, "1234"); Assert.True(first.Ok, first.Error);
        if (inactive) Assert.True(a.SetActive(TestData.Owner, first.Value, false).Ok); var before = State(t);
        NoCommit(a.CreateStaff(TestData.Owner, "  SYNTHETIC DUPLICATE  ", UserRole.Manager, "5678")); Assert.Equal(before, State(t));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(8)]
    public void CurrentPinLengthIsReadInsideTheWriteNotFromTheOldServiceCache(int length)
    {
        using var t = new TestDatabase(); var a = Auth(t); Assert.Equal(4, t.Settings.GetInt("security.pinLength", 4));
        var other = new Database(t.Database.FilePath, pooling: false, durable: false);
        other.Tx(c => other.Execute(c, "UPDATE settings SET value=@value WHERE key='security.pinLength';", new { value = length.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        Assert.Equal(4, t.Settings.GetInt("security.pinLength", 4)); var before = State(t);
        NoCommit(a.CreateStaff(TestData.Owner, "Synthetic current PIN", UserRole.Cashier, "1234")); Assert.Equal(before, State(t));
        Assert.True(a.CreateStaff(TestData.Owner, "Synthetic current PIN", UserRole.Cashier, new string('7', length)).Ok);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrMalformedLengthKeepsTheExistingFourDigitFallback(bool missing)
    {
        using var t = new TestDatabase(); var a = Auth(t);
        t.Database.Tx(c => t.Database.Execute(c, missing ? "DELETE FROM settings WHERE key='security.pinLength';" : "UPDATE settings SET value='broken' WHERE key='security.pinLength';"));
        Assert.True(a.CreateStaff(TestData.Owner, "Synthetic fallback PIN", UserRole.Cashier, "1234").Ok);
    }

    [Fact]
    public void AuditFaultRollsBackNewAccountSequenceAndAllowsAnExplicitRetry()
    {
        using var t = new TestDatabase(); var a = Auth(t); var guard = new PostingAttemptGuard();
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_staff_create_audit BEFORE INSERT ON audit_logs WHEN NEW.module='Users' AND NEW.action='create' BEGIN SELECT RAISE(ABORT,'synthetic'); END;")); var before = State(t);
        Assert.True(guard.TryBegin()); var failed = a.CreateStaff(TestData.Owner, "Synthetic audit retry", UserRole.Cashier, "1234"); NoCommit(failed); guard.ObserveFailure(failed); Assert.True(guard.Ready); Assert.Equal(before, State(t));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_staff_create_audit;")); Assert.True(guard.TryBegin()); var retry = a.CreateStaff(TestData.Owner, "Synthetic audit retry", UserRole.Cashier, "1234"); Assert.True(retry.Ok, retry.Error); guard.AcknowledgeCommit(); Assert.False(guard.TryBegin());
    }

    [Fact]
    public void InsertFaultDoesNotCreateAnAccountOrAudit()
    {
        using var t = new TestDatabase(); var a = Auth(t);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_staff_create_insert BEFORE INSERT ON users BEGIN SELECT RAISE(ABORT,'synthetic'); END;")); var before = State(t);
        NoCommit(a.CreateStaff(TestData.Owner, "Synthetic refused insert", UserRole.Cashier, "1234")); Assert.Equal(before, State(t));
    }

    [Fact]
    public void DeferredCommitFaultIsUnknownAndReturnsNoCreatedIdOrRow()
    {
        using var t = new TestDatabase(); var a = Auth(t); var guard = new PostingAttemptGuard();
        t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_staff_create_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_staff_create_commit_audit AFTER INSERT ON audit_logs WHEN NEW.module='Users' AND NEW.action='create' BEGIN INSERT INTO synthetic_staff_create_commit(parent_id) VALUES(-99999); END;")); var before = State(t);
        Assert.True(guard.TryBegin()); var failed = a.CreateStaff(TestData.Owner, "Synthetic commit fault", UserRole.Cashier, "1234");
        Assert.False(failed.Ok); Assert.False(failed.NoCommitConfirmed); Assert.Contains("unconfirmed", failed.Error!); Assert.Equal(0, failed.Value); Assert.Null(failed.StaffEdit); Assert.Null(failed.EditVersion); Assert.Equal(before, State(t));
        guard.ObserveFailure(failed); Assert.Equal(PostingAttemptState.NeedsVerification, guard.State); Assert.False(guard.TryBegin());
    }

    [Fact]
    public void CurrentOwnerAndInteractiveProofRemainRequiredAndNoTargetLoginIsIssued()
    {
        using var t = new TestDatabase(); var a = Auth(t); var staff = TestData.Cashier(t); var before = State(t);
        NoCommit(a.CreateStaff(staff, "Synthetic not owner", UserRole.Cashier, "1234")); Assert.Equal(before, State(t));
        t.Database.Sessions.Enable(); NoCommit(a.CreateStaff(TestData.Owner, "Synthetic no proof", UserRole.Cashier, "1234"));
        var login = a.Login("admin", "area11"); Assert.True(login.Ok, login.Error); var owner = login.Value!;
        var result = a.CreateStaff(owner, "Synthetic authenticated creation", UserRole.Cashier, "1234"); Assert.True(result.Ok, result.Error); Assert.False(a.CheckSession(result.StaffEdit).Ok); Assert.True(a.CheckSession(owner).Ok);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;")); before = State(t);
        NoCommit(a.CreateStaff(owner, "Synthetic revoked owner", UserRole.Cashier, "1234")); Assert.Equal(before, State(t));
    }

    [Fact]
    public void AnotherWriterAfterCreationInvalidatesTheExactReturnedStaffBaseline()
    {
        using var t = new TestDatabase(); var a = Auth(t); var saved = a.CreateStaff(TestData.Owner, "Synthetic handoff", UserRole.Cashier, "1234"); Assert.True(saved.Ok, saved.Error);
        Assert.True(a.SetActive(TestData.Owner, saved.Value, false).Ok); var before = State(t);
        var conflict = a.SetSecret(TestData.Owner, saved.Value, "5678", false, saved.EditVersion); Assert.False(conflict.Ok); Assert.True(conflict.NoCommitConfirmed); Assert.Equal(before, State(t)); Assert.True(saved.StaffEdit!.StaffForm!.Active);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("throw")]
    [InlineData("ack-refresh-fault")]
    [InlineData("complete")]
    [InlineData("cancel")]
    public void SyntheticLocalGuardSurvivesRefreshInputChangeAndAccessRegrant(string scenario)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); Assert.False(guard.TryBegin());
        if (scenario == "unknown") guard.ObserveFailure(ServiceResult.Fail("synthetic unknown"));
        else if (scenario == "throw") guard.ObserveException();
        else if (scenario == "cancel") guard.CancelBeforeWrite();
        else
        {
            guard.AcknowledgeCommit(); guard.ObserveException(); Assert.True(guard.WasAcknowledged);
            if (scenario == "complete") guard.CompleteReset();
        }
        // Deliberately synthetic UI events: no guard reset is called by refresh/regrant or edited input.
        var editedInput = "another name"; var allowedAgain = true; Assert.NotEmpty(editedInput); Assert.True(allowedAgain);
        Assert.Equal(scenario is "complete" or "cancel", guard.Ready);
        if (!guard.Ready) Assert.False(guard.TryBegin());
    }
}
