using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated public-service cases. Written only until .NET execution;not WPF proof.</summary>
public sealed class RecoveryEditConcurrencyTests
{
    private static AuthService Auth(TestDatabase t, Database? db = null)
    {
        if (db is null) return new AuthService(t.Database, t.Audit, t.Settings, t.Log);
        var audit = new AuditService(db, t.Log);
        return new AuthService(db, audit, new SettingsService(db, audit, t.Log), t.Log);
    }
    private static RecoveryEditForm Load(AuthService auth, User? owner = null)
    {
        var result = auth.LoadRecoveryEditForm(owner ?? TestData.Owner);
        Assert.True(result.Ok, result.Error); Assert.NotNull(result.Value); return result.Value!;
    }
    private static string State(TestDatabase t) => t.Database.Read(c => string.Join("\n", new[] { "settings", "users", "audit_logs", "sqlite_sequence", "sales", "payments" }.Select(table =>
    {
        var columns = t.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
        var projection = string.Join("||':'||", columns.Select(col => "quote(\"" + col + "\")"));
        return table + "=" + string.Join(";", t.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
    })));
    private static void Conflict(ServiceResult result)
    {
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Contains("changed", result.Error!);
        Assert.Null(result.RecoveryEdit); Assert.Null(result.EditVersion);
    }
    private static string Hash(TestDatabase t) => t.Database.Read(c => JsonSerializer.Deserialize<string>(t.Database.QuerySingle<string>(c,
        "SELECT value FROM settings WHERE key=@key;", new { key = AuthService.RecoveryWordKey })!)!);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FirstWordChangeOrFactoryResetInvalidatesEitherOldAction(bool firstFactory, bool secondFactory)
    {
        using var t = new TestDatabase(); var a = Auth(t); Assert.True(a.SetRecoveryWord(TestData.Owner, "initial-word").Ok);
        var one = Load(a); var two = Load(a);
        var first = a.SetRecoveryWord(TestData.Owner, firstFactory ? "" : "first-new-word", one.EditVersion); Assert.True(first.Ok, first.Error); var before = State(t);
        Conflict(a.SetRecoveryWord(TestData.Owner, secondFactory ? "" : "second-new-word", two.EditVersion)); Assert.Equal(before, State(t));
        Assert.Equal(firstFactory, first.RecoveryEdit!.UsesFactoryWord);
    }

    [Fact]
    public void MissingRowAndExplicitFactoryValueHaveDifferentBaselines()
    {
        using var t = new TestDatabase(); var a = Auth(t);
        t.Database.Tx(c => t.Database.Execute(c, "DELETE FROM settings WHERE key=@key;", new { key = AuthService.RecoveryWordKey })); var missing = Load(a); Assert.True(missing.UsesFactoryWord);
        t.Database.Tx(c => t.Database.Execute(c, "INSERT INTO settings(key,value,updated_at) VALUES(@key,'\"\"','2026-11-10');", new { key = AuthService.RecoveryWordKey }));
        Assert.True(Load(a).UsesFactoryWord); var before = State(t);
        Conflict(a.SetRecoveryWord(TestData.Owner, "my-new-word", missing.EditVersion)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void UncachedExactStoredJsonNotTheAppCacheControlsTheComparison()
    {
        using var t = new TestDatabase(); var a = Auth(t); var old = Load(a);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value='\"synthetic-other-hash\"' WHERE key=@key;", new { key = AuthService.RecoveryWordKey }));
        Assert.Equal("", t.Settings.GetString(AuthService.RecoveryWordKey)); Assert.False(Load(a).UsesFactoryWord); var before = State(t);
        Conflict(a.SetRecoveryWord(TestData.Owner, "my-new-word", old.EditVersion)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void TimestampOnlyAndUnrelatedSettingsDoNotStaleTheWordEditor()
    {
        using var t = new TestDatabase(); var a = Auth(t); var old = Load(a);
        t.Database.Tx(c =>
        {
            t.Database.Execute(c, "UPDATE settings SET updated_at='2099-01-01';");
            t.Database.Execute(c, "UPDATE settings SET value='\"Synthetic shop\"' WHERE key='store.name';");
        });
        Assert.True(a.SetRecoveryWord(TestData.Owner, "my-new-word", old.EditVersion).Ok);
    }

    [Fact]
    public void AnotherConnectionIsSeenButItsTokenCannotBeUsedOnThisInstance()
    {
        using var t = new TestDatabase(); var a = Auth(t); var old = Load(a);
        var other = Auth(t, new Database(t.Database.FilePath, pooling: false, durable: false)); var fresh = Load(other);
        Conflict(a.SetRecoveryWord(TestData.Owner, "my-new-word", fresh.EditVersion));
        Assert.True(other.SetRecoveryWord(TestData.Owner, "other-word", fresh.EditVersion).Ok);
        var before = State(t); Conflict(a.SetRecoveryWord(TestData.Owner, "", old.EditVersion)); Assert.Equal(before, State(t));
    }

    [Fact]
    public void StaffAndPermissionScopeTokensCannotWriteRecovery()
    {
        using var t = new TestDatabase(); var a = Auth(t); var staff = a.ListUsers(TestData.Owner); Assert.True(staff.Ok, staff.Error);
        var permission = new PermissionService(t.Database, t.Log, t.Audit).LoadEditForm(TestData.Owner, 1); Assert.True(permission.Ok, permission.Error); var before = State(t);
        Conflict(a.SetRecoveryWord(TestData.Owner, "my-new-word", staff.Value![0].StaffForm!.EditVersion));
        Conflict(a.SetRecoveryWord(TestData.Owner, "my-new-word", permission.Value!.EditVersion)); Assert.Equal(before, State(t));
    }

    [Theory]
    [InlineData("")]
    [InlineData("new-word")]
    public void CheckedNullIsRejectedEvenWithoutSessions(string word)
    {
        using var t = new TestDatabase(); var before = State(t); Conflict(Auth(t).SetRecoveryWord(TestData.Owner, word, null)); Assert.Equal(before, State(t));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmno")]
    public void InvalidLengthIsKnownNoCommitAndTheLoadedFormCanStillBeCorrected(string word)
    {
        using var t = new TestDatabase(); var a = Auth(t); var old = Load(a); var before = State(t);
        var bad = a.SetRecoveryWord(TestData.Owner, word, old.EditVersion); Assert.False(bad.Ok); Assert.True(bad.NoCommitConfirmed); Assert.Equal(before, State(t));
        Assert.True(a.SetRecoveryWord(TestData.Owner, "corrected-word", old.EditVersion).Ok);
    }

    [Fact]
    public void ExactSuccessHandoffAllowsNextChangeButDoesNotRebaseAnotherWriter()
    {
        using var t = new TestDatabase(); var a = Auth(t); var old = Load(a);
        var saved = a.SetRecoveryWord(TestData.Owner, "my-new-word", old.EditVersion); Assert.True(saved.Ok, saved.Error); Assert.False(saved.RecoveryEdit!.UsesFactoryWord);
        Assert.Same(saved.EditVersion, saved.RecoveryEdit.EditVersion); Assert.True(old.UsesFactoryWord);
        var reset = a.SetRecoveryWord(TestData.Owner, "", saved.EditVersion); Assert.True(reset.Ok, reset.Error); Assert.True(reset.RecoveryEdit!.UsesFactoryWord);
        Conflict(a.SetRecoveryWord(TestData.Owner, "later-word", saved.EditVersion));
    }

    [Fact]
    public void NormalizationFactoryResetAndNonsecretAuditKeepTheirExistingPolicy()
    {
        using var t = new TestDatabase(); var a = Auth(t); var old = Load(a);
        var saved = a.SetRecoveryWord(TestData.Owner, " My Own Word ", old.EditVersion); Assert.True(saved.Ok, saved.Error);
        Assert.True(PasswordHasher.Verify("myownword", Hash(t))); Assert.Equal(Hash(t), t.Settings.GetString(AuthService.RecoveryWordKey));
        var audit = t.Database.Read(c => t.Database.QuerySingle<string>(c, "SELECT details FROM audit_logs WHERE action='recovery_word_changed' ORDER BY id DESC LIMIT 1;")); Assert.Equal("changed", audit);
        var reset = a.SetRecoveryWord(TestData.Owner, "   ", saved.EditVersion); Assert.True(reset.Ok, reset.Error); Assert.True(reset.RecoveryEdit!.UsesFactoryWord); Assert.Equal("", Hash(t));
        audit = t.Database.Read(c => t.Database.QuerySingle<string>(c, "SELECT details FROM audit_logs WHERE action='recovery_word_changed' ORDER BY id DESC LIMIT 1;")); Assert.Equal("back to the factory word", audit);
    }

    [Fact]
    public void ReadFailureIsNotASuccessfulFactoryEditor()
    {
        using var t = new TestDatabase(); var a = Auth(t);
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE settings SET value='broken-json' WHERE key=@key;", new { key = AuthService.RecoveryWordKey })); var before = State(t);
        var result = a.LoadRecoveryEditForm(TestData.Owner); Assert.False(result.Ok); Assert.Null(result.Value); Assert.Equal(before, State(t));
    }

    [Fact]
    public void EditorAndResultAreImmutableAndNeverSerializeCredentialMaterial()
    {
        using var t = new TestDatabase(); var a = Auth(t); var old = Load(a);
        Assert.All(typeof(RecoveryEditForm).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.DoesNotContain("EditVersion", JsonSerializer.Serialize(old));
        var saved = a.SetRecoveryWord(TestData.Owner, "secret-new-word", old.EditVersion); Assert.True(saved.Ok, saved.Error);
        var json = JsonSerializer.Serialize(saved); Assert.DoesNotContain("RecoveryEdit", json); Assert.DoesNotContain("EditVersion", json); Assert.DoesNotContain(Hash(t), json);
        json = JsonSerializer.Serialize(saved.RecoveryEdit); Assert.DoesNotContain("secret-new-word", json); Assert.DoesNotContain(Hash(t), json); Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuditFailureRollsBackWordAndCacheAndOriginalTokenCanRetry(bool factory)
    {
        using var t = new TestDatabase(); var a = Auth(t); Assert.True(a.SetRecoveryWord(TestData.Owner, "initial-word").Ok); var old = Load(a); var cached = t.Settings.GetString(AuthService.RecoveryWordKey);
        t.Database.Tx(c => t.Database.Execute(c, "CREATE TRIGGER synthetic_recovery_audit BEFORE INSERT ON audit_logs WHEN NEW.action='recovery_word_changed' BEGIN SELECT RAISE(ABORT,'synthetic'); END;")); var before = State(t);
        var bad = a.SetRecoveryWord(TestData.Owner, factory ? "" : "new-word", old.EditVersion); Assert.False(bad.Ok); Assert.True(bad.NoCommitConfirmed); Assert.Null(bad.RecoveryEdit); Assert.Equal(before, State(t)); Assert.Equal(cached, t.Settings.GetString(AuthService.RecoveryWordKey));
        t.Database.Tx(c => t.Database.Execute(c, "DROP TRIGGER synthetic_recovery_audit;")); Assert.True(a.SetRecoveryWord(TestData.Owner, factory ? "" : "new-word", old.EditVersion).Ok);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredCommitFailureReturnsNeitherHandoffNorSafeRetry(bool factory)
    {
        using var t = new TestDatabase(); var a = Auth(t); Assert.True(a.SetRecoveryWord(TestData.Owner, "initial-word").Ok); var old = Load(a); var cached = t.Settings.GetString(AuthService.RecoveryWordKey);
        t.Database.Tx(c => t.Database.Execute(c, @"
CREATE TABLE synthetic_recovery_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_recovery_commit_audit AFTER INSERT ON audit_logs WHEN NEW.action='recovery_word_changed' BEGIN INSERT INTO synthetic_recovery_commit(parent_id) VALUES(-99999); END;")); var before = State(t);
        var bad = a.SetRecoveryWord(TestData.Owner, factory ? "" : "new-word", old.EditVersion); Assert.False(bad.Ok); Assert.False(bad.NoCommitConfirmed); Assert.Contains("unconfirmed", bad.Error!); Assert.Null(bad.RecoveryEdit); Assert.Null(bad.EditVersion); Assert.Equal(before, State(t)); Assert.Equal(cached, t.Settings.GetString(AuthService.RecoveryWordKey));
    }

    [Fact]
    public void CurrentOwnerAndInteractiveProofCannotBeReplacedByALoadedToken()
    {
        using var t = new TestDatabase(); var a = Auth(t); var old = Load(a); var staff = TestData.Cashier(t);
        Assert.False(a.LoadRecoveryEditForm(null).Ok); Assert.False(a.LoadRecoveryEditForm(staff).Ok); Assert.False(a.SetRecoveryWord(staff, "new-word", old.EditVersion).Ok);
        t.Database.Sessions.Enable(); Assert.False(a.LoadRecoveryEditForm(TestData.Owner).Ok); Assert.False(a.SetRecoveryWord(TestData.Owner, "new-word", old.EditVersion).Ok);
        var login = a.Login("admin", "area11"); Assert.True(login.Ok, login.Error); var owner = login.Value!; var fresh = Load(a, owner);
        Assert.True(a.SetRecoveryWord(owner, "new-word", fresh.EditVersion).Ok); Assert.True(a.CheckSession(owner).Ok); // Recovery-word change is not a password/PIN reset.
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;")); var before = State(t);
        Assert.False(a.LoadRecoveryEditForm(owner).Ok); Assert.False(a.SetRecoveryWord(owner, "later-word", fresh.EditVersion).Ok); Assert.Equal(before, State(t));
    }

    [Fact]
    public void SavingTheSameNormalizedWordStillChangesItsSaltAndInvalidatesTheOldEditor()
    {
        using var t = new TestDatabase(); var a = Auth(t); Assert.True(a.SetRecoveryWord(TestData.Owner, "same-word").Ok); var old = Load(a); var hash = Hash(t);
        Assert.True(a.SetRecoveryWord(TestData.Owner, " SAME-WORD ", old.EditVersion).Ok); Assert.NotEqual(hash, Hash(t));
        Conflict(a.SetRecoveryWord(TestData.Owner, "", old.EditVersion));
    }

    [Fact]
    public void RepeatedFactorySaveIsContentEquivalentNotDurableDeduplication()
    {
        using var t = new TestDatabase(); var a = Auth(t); var old = Load(a); Assert.True(old.UsesFactoryWord);
        Assert.True(a.SetRecoveryWord(TestData.Owner, "", old.EditVersion).Ok);
        Assert.True(a.SetRecoveryWord(TestData.Owner, "", old.EditVersion).Ok); // No content change;this is not an attempt ledger.
        Assert.Equal(2L, t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs WHERE action='recovery_word_changed';"))));
    }

    [Fact]
    public void DemotedStoredOwnerCannotUseAnOldValidForm()
    {
        using var t = new TestDatabase(); var a = Auth(t); var beforeRead = State(t); var old = Load(a); Assert.Equal(beforeRead, State(t));
        t.Database.Tx(c => t.Database.Execute(c, "UPDATE users SET role='manager' WHERE id=1;")); var before = State(t);
        Assert.False(a.LoadRecoveryEditForm(TestData.Owner).Ok);
        var denied = a.SetRecoveryWord(TestData.Owner, "unwanted-word", old.EditVersion); Assert.False(denied.Ok); Assert.True(denied.NoCommitConfirmed); Assert.Equal(before, State(t));
    }

    [Fact]
    public void ImperativeCompatibilityRemainsSeparateAndGenericSettingsStayClosed()
    {
        using var t = new TestDatabase(); var a = Auth(t);
        Assert.True(a.SetRecoveryWord(TestData.Owner, "new-word").Ok); Assert.True(a.SetRecoveryWord(TestData.Owner, "").Ok);
        t.Database.Sessions.Enable(); var login = a.Login("admin", "area11"); Assert.True(login.Ok, login.Error); Assert.True(a.SetRecoveryWord(login.Value!, "enabled-word").Ok);
        Assert.False(t.Settings.LoadEditForm(login.Value!, new[] { AuthService.RecoveryWordKey }).Ok);
        Assert.False(t.Settings.SetMany(new Dictionary<string, object?> { [AuthService.RecoveryWordKey] = "synthetic-unwanted" }, login.Value!).Ok);
    }
}
