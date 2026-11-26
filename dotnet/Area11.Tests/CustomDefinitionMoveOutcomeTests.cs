using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Public Move outcome/receipt tests on disposable DBs; guard cases are synthetic, not WPF proof.</summary>
public sealed class CustomDefinitionMoveOutcomeTests
{
    private sealed class CustomMoveFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public CustomFieldService S { get; }
        public int First { get; }
        public int Middle { get; }
        public int Last { get; }
        public CustomMoveFixture()
        {
            S = new CustomFieldService(T.Database, T.Audit, TestData.Permissions(T), T.Log);
            First = Add("Synthetic first"); Middle = Add("Synthetic middle"); Last = Add("Synthetic last");
        }
        private int Add(string label)
        {
            var result = S.Add(TestData.Owner, "customer", label, "text", null, false, "Synthetic help");
            Assert.True(result.Ok, result.Error); return result.Value!.Id;
        }
        public List<CustomField> Rows() => S.List(TestData.Owner, "customer", true, true).Value!;
        public CustomField Loaded(int id) => Rows().Single(row => row.Id == id);
        public ServiceResult Move(EditVersion? token, int direction = 1) => S.MoveChecked(TestData.Owner, Middle, direction, token);
        public void Sql(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Audits() => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs WHERE action='custom_field_order';")));
        public string State() => T.Database.Read(c => string.Join("\n", new[] { "custom_fields", "custom_values", "audit_logs", "sqlite_sequence" }.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(col => "quote(\"" + col + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public void Dispose() => T.Dispose();
    }

    private static void NoCommit(ServiceResult result)
    {
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed);
        Assert.Null(result.CustomDefinitionEdit); Assert.Null(result.EditVersion);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(1, false)]
    [InlineData(-1, true)]
    [InlineData(1, true)]
    public void BothDirectionsRenumberWholeEntityEvenWithDuplicateSortsAndHiddenRows(int direction, bool tied)
    {
        using var f = new CustomMoveFixture(); Assert.True(f.S.SetActive(TestData.Owner, f.First, false).Ok);
        if (tied) f.Sql("UPDATE custom_fields SET sort=90 WHERE entity='customer';");
        var before = f.Rows(); var audits = f.Audits();
        var result = f.Move(before.Single(row => row.Id == f.Middle).EditVersion, direction); Assert.True(result.Ok, result.Error);
        var saved = result.CustomDefinitionEdit!;
        var expected = direction < 0 ? new[] { f.Middle, f.First, f.Last } : new[] { f.First, f.Last, f.Middle };
        Assert.Equal(expected, saved.Fields.Select(row => row.Id).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, saved.Fields.Select(row => row.Sort).ToArray());
        Assert.Equal(f.Middle, saved.FieldId); Assert.Equal("customer", saved.Entity); Assert.Same(result.EditVersion, saved.EditVersion);
        Assert.All(saved.Fields, row => Assert.NotNull(row.EditVersion)); Assert.Equal(audits + 1, f.Audits());
        foreach (var row in before)
        {
            var stored = saved.Fields.Single(item => item.Id == row.Id); row.Sort = stored.Sort;
            Assert.True(CustomFieldDefinitionState.Matches(row, stored));
            Assert.True(CustomFieldDefinitionState.Matches(stored, f.Loaded(row.Id)));
        }
        Assert.False(saved.Fields.Single(row => row.Id == f.First).Active);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void BoundaryRefusalIsKnownNoCommitWithoutAnyAudit(int direction)
    {
        using var f = new CustomMoveFixture(); var id = direction < 0 ? f.First : f.Last; var loaded = f.Loaded(id); var before = f.State();
        var result = f.S.MoveChecked(TestData.Owner, id, direction, loaded.EditVersion);
        NoCommit(result); Assert.Contains(direction < 0 ? "first" : "last", result.Error!); Assert.Equal(before, f.State());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckedDoorRefusesNullBaselineEvenInLegacyHost(bool interactive)
    {
        using var f = new CustomMoveFixture(); if (interactive) f.T.Database.Sessions.Enable();
        var before = f.State(); NoCommit(f.Move(null)); Assert.Equal(before, f.State());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-2)]
    public void InvalidDirectionNeverStartsAReorder(int direction)
    {
        using var f = new CustomMoveFixture(); var loaded = f.Loaded(f.Middle); var before = f.State();
        NoCommit(f.Move(loaded.EditVersion, direction)); Assert.Equal(before, f.State());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("manager")]
    [InlineData("inactive")]
    public void ActorPreflightNeverCreatesAReceiptOrAudit(string mode)
    {
        using var f = new CustomMoveFixture(); var token = f.Loaded(f.Middle).EditVersion; var before = f.State();
        var actor = mode == "missing" ? null : new User { Id = 1, Name = "Synthetic actor", Role = mode == "manager" ? "manager" : "owner", Active = mode != "inactive" };
        NoCommit(f.S.MoveChecked(actor, f.Middle, 1, token)); Assert.Equal(before, f.State());
    }

    [Theory]
    [InlineData("label", "'Synthetic external label'")]
    [InlineData("sort", "90")]
    [InlineData("active", "0")]
    [InlineData("required", "1")]
    [InlineData("help_text", "'Synthetic external help'")]
    [InlineData("created_at", "'2001-01-02'")]
    public void SecondConnectionPeerChangeRefusesOriginalEntityBaseline(string column, string value)
    {
        using var f = new CustomMoveFixture(); var loaded = f.Loaded(f.Middle);
        var db = new Database(f.T.Database.FilePath, pooling: false, durable: false);
        db.Tx(c => { db.Execute(c, $"UPDATE custom_fields SET {column}={value} WHERE id=@id;", new { id = f.Last }); });
        var before = f.State(); var result = f.Move(loaded.EditVersion);
        NoCommit(result); Assert.Contains("changed", result.Error!); Assert.Equal(before, f.State());
    }

    [Fact]
    public void EvenBoundaryCheckRequiresTheOriginalCurrentEntityToken()
    {
        using var f = new CustomMoveFixture(); var loaded = f.Loaded(f.First);
        Assert.True(f.S.Update(TestData.Owner, f.Last, "Synthetic changed peer", "text", null, false).Ok);
        var before = f.State(); var result = f.S.MoveChecked(TestData.Owner, f.First, -1, loaded.EditVersion);
        NoCommit(result); Assert.Contains("changed", result.Error!); Assert.Equal(before, f.State());
    }

    [Fact]
    public void WrongTargetAndForeignDatabaseTokensCannotAuthorizeMove()
    {
        using var f = new CustomMoveFixture(); var before = f.State(); NoCommit(f.Move(f.Loaded(f.First).EditVersion));
        var db = new Database(f.T.Database.FilePath, pooling: false, durable: false); var audit = new AuditService(db, f.T.Log);
        var service = new CustomFieldService(db, audit, new PermissionService(db, f.T.Log, audit), f.T.Log);
        NoCommit(service.MoveChecked(TestData.Owner, f.Middle, 1, f.Loaded(f.Middle).EditVersion)); Assert.Equal(before, f.State());
    }

    [Fact]
    public void UnrelatedEntityWriterDoesNotInvalidateTheCustomerToken()
    {
        using var f = new CustomMoveFixture(); var loaded = f.Loaded(f.Middle);
        var other = f.S.Add(TestData.Owner, "supplier", "Synthetic supplier question", "text", null, false); Assert.True(other.Ok, other.Error);
        var result = f.Move(loaded.EditVersion); Assert.True(result.Ok, result.Error);
        Assert.True(CustomFieldDefinitionState.Matches(other.Value, f.S.List(TestData.Owner, "supplier", true, true).Value!.Single()));
    }

    [Fact]
    public void DeletedTargetRefusesWithoutInventingBoundarySuccessOrReceipt()
    {
        using var f = new CustomMoveFixture(); var token = f.Loaded(f.Middle).EditVersion; f.Sql($"DELETE FROM custom_fields WHERE id={f.Middle};");
        var before = f.State(); NoCommit(f.Move(token)); NoCommit(f.S.Move(TestData.Owner, f.Middle, 1)); Assert.Equal(before, f.State());
    }

    [Fact]
    public void CurrentSessionOwnerAndStoredAuditNameAreCheckedBeforeBoundaryDecision()
    {
        using var f = new CustomMoveFixture(); var middle = f.Loaded(f.Middle); var first = f.Loaded(f.First);
        f.T.Database.Sessions.Enable(); var before = f.State();
        NoCommit(f.S.MoveChecked(TestData.Owner, f.First, -1, first.EditVersion)); Assert.Equal(before, f.State());
        var auth = new AuthService(f.T.Database, f.T.Audit, f.T.Settings, f.T.Log); var login = auth.Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        f.Sql("UPDATE users SET name='Synthetic current owner' WHERE id=1;");
        var result = f.S.MoveChecked(login.Value!, f.Middle, 1, middle.EditVersion); Assert.True(result.Ok, result.Error);
        Assert.Equal("Synthetic current owner", f.T.Database.Read(c => f.T.Database.QuerySingle<string>(c, "SELECT user_name FROM audit_logs WHERE action='custom_field_order' ORDER BY id DESC LIMIT 1;")));
        var firstToken = result.CustomDefinitionEdit!.Fields.Single(row => row.Id == f.First).EditVersion;
        f.Sql("UPDATE users SET role='manager' WHERE id=1;"); var audits = f.Audits();
        var denied = f.S.MoveChecked(login.Value!, f.First, -1, firstToken); NoCommit(denied); Assert.Contains("owner", denied.Error!); Assert.Equal(audits, f.Audits());
    }

    [Theory]
    [InlineData("first")]
    [InlineData("middle")]
    [InlineData("last")]
    [InlineData("audit")]
    [InlineData("receipt")]
    public void PartialRenumberOrAuditOrReceiptFaultRollsBackEverythingThenExplicitRetryWorks(string stage)
    {
        using var f = new CustomMoveFixture(); var loaded = f.Loaded(f.Middle);
        var id = stage == "first" ? f.First : stage == "middle" ? f.Last : f.Middle;
        f.Sql(stage == "audit" ? "CREATE TRIGGER synthetic_move_fault BEFORE INSERT ON audit_logs WHEN NEW.action='custom_field_order' BEGIN SELECT RAISE(ABORT,'synthetic'); END;"
            : stage == "receipt" ? "CREATE TRIGGER synthetic_move_fault AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_order' BEGIN DELETE FROM custom_fields WHERE id=CAST(NEW.entity_id AS INTEGER); END;"
            : $"CREATE TRIGGER synthetic_move_fault BEFORE UPDATE OF sort ON custom_fields WHEN OLD.id={id} BEGIN SELECT RAISE(ABORT,'synthetic'); END;");
        var before = f.State(); NoCommit(f.Move(loaded.EditVersion)); Assert.Equal(before, f.State());
        f.Sql("DROP TRIGGER synthetic_move_fault;"); var saved = f.Move(loaded.EditVersion); Assert.True(saved.Ok, saved.Error); Assert.Equal(1, f.Audits());
    }

    [Fact]
    public void DeferredCommitFailureReturnsUnknownWithoutReceiptAndWithoutSafeRetryPromise()
    {
        using var f = new CustomMoveFixture(); var loaded = f.Loaded(f.Middle);
        f.Sql(@"CREATE TABLE synthetic_move_commit(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_move_commit_audit AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_order' BEGIN INSERT INTO synthetic_move_commit VALUES(-99999); END;");
        var before = f.State(); var result = f.Move(loaded.EditVersion);
        Assert.False(result.Ok); Assert.False(result.NoCommitConfirmed); Assert.Null(result.CustomDefinitionEdit); Assert.Null(result.EditVersion);
        Assert.Contains("unconfirmed", result.Error!); Assert.Equal(before, f.State());
    }

    [Fact]
    public void DefensiveReceiptDoesNotSerializeOrAdoptLaterWriter()
    {
        using var f = new CustomMoveFixture(); var result = f.Move(f.Loaded(f.Middle).EditVersion); Assert.True(result.Ok, result.Error);
        var saved = result.CustomDefinitionEdit!; saved.Fields.Single(row => row.Id == f.Middle).Sort = 999;
        Assert.Equal(3, saved.Fields.Single(row => row.Id == f.Middle).Sort);
        Assert.DoesNotContain("CustomDefinitionEdit", JsonSerializer.Serialize(result));
        Assert.True(f.S.Update(TestData.Owner, f.Last, "Synthetic later peer", "text", null, false).Ok);
        NoCommit(f.Move(result.EditVersion, -1)); Assert.Equal("Synthetic last", saved.Fields.Single(row => row.Id == f.Last).Label);
    }

    [Fact]
    public void RowSpecificReceiptTokenCanAuthorizeADeliberateNextMove()
    {
        using var f = new CustomMoveFixture(); var moved = f.Move(f.Loaded(f.Middle).EditVersion); Assert.True(moved.Ok, moved.Error);
        var other = moved.CustomDefinitionEdit!.Fields.Single(row => row.Id == f.Last);
        var next = f.S.MoveChecked(TestData.Owner, f.Last, -1, other.EditVersion); Assert.True(next.Ok, next.Error);
        Assert.Equal(new[] { f.Last, f.First, f.Middle }, next.CustomDefinitionEdit!.Fields.Select(row => row.Id).ToArray());
    }

    [Fact]
    public void LegacySessionsDisabledNullTokenStillWorksWithoutWeakeningCheckedDoor()
    {
        using var f = new CustomMoveFixture(); NoCommit(f.Move(null));
        var result = f.S.Move(TestData.Owner, f.Middle, 1); Assert.True(result.Ok, result.Error); Assert.NotNull(result.CustomDefinitionEdit);
    }

    [Fact]
    public void OldAnswersStableKeysAndNonSortDefinitionValuesStayIntact()
    {
        using var f = new CustomMoveFixture(); var customer = TestData.AddCustomer(f.T);
        Assert.True(f.S.SaveValues(TestData.Owner, "customer", customer, new Dictionary<int, string?> { [f.Middle] = "Synthetic old answer" }).Ok);
        var before = f.Loaded(f.Middle); var result = f.Move(before.EditVersion); Assert.True(result.Ok, result.Error);
        var after = result.CustomDefinitionEdit!.Fields.Single(row => row.Id == f.Middle); before.Sort = after.Sort;
        Assert.True(CustomFieldDefinitionState.Matches(before, after));
        Assert.Equal("Synthetic old answer", f.T.Database.Read(c => f.T.Database.QuerySingle<string>(c, "SELECT value FROM custom_values WHERE field_id=@id;", new { id = f.Middle })));
    }

    [Theory]
    [InlineData("move-unknown")]
    [InlineData("move-ack-callback")]
    [InlineData("add-unknown")]
    [InlineData("update-unknown")]
    public void SyntheticSharedGuardBlocksMoveAndOtherCommandsAfterUncertainty(string mode)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); Assert.False(guard.TryBegin());
        if (mode == "move-ack-callback") guard.AcknowledgeCommit(); else guard.ObserveFailure(ServiceResult.Fail("Synthetic unknown"));
        guard.ObserveException(); Assert.False(guard.Ready); Assert.False(guard.TryBegin()); Assert.Equal(mode == "move-ack-callback", guard.WasAcknowledged);
        // Synthetic public guard transitions only: no Desktop command/timer/focus is executed here.
    }
}
