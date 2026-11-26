using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated public Add outcomes/receipts; guard probes are synthetic, not Desktop/WPF execution.</summary>
public sealed class CustomDefinitionAddOutcomeTests
{
    private sealed class CustomAddFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public CustomFieldService S { get; }
        public CustomAddFixture() => S = new CustomFieldService(T.Database, T.Audit, TestData.Permissions(T), T.Log);
        public ServiceResult<CustomField> Add(string label = "Synthetic new question", string entity = "customer") =>
            S.Add(TestData.Owner, entity, label, "text", null, false, "Synthetic help");
        public void Sql(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public string State() => T.Database.Read(c => string.Join("\n", new[] { "custom_fields", "custom_values", "audit_logs", "sqlite_sequence" }.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(col => "quote(\"" + col + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public List<CustomField> Rows(string entity = "customer") => S.List(TestData.Owner, entity, true, true).Value!;
        public long Audits() => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs WHERE action='custom_field_add';")));
        public void Dispose() => T.Dispose();
    }

    private static void NoCommit(ServiceResult<CustomField> result)
    {
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed);
        Assert.Null(result.Value); Assert.Null(result.CustomDefinitionEdit); Assert.Null(result.EditVersion);
    }

    [Theory]
    [InlineData("product")]
    [InlineData("customer")]
    [InlineData("supplier")]
    [InlineData("sale")]
    [InlineData("purchase")]
    [InlineData("expense")]
    public void EveryExistingEntityReturnsExactCreatedRowAndWholeEntityTokens(string entity)
    {
        using var f = new CustomAddFixture(); var peer = f.Add("Synthetic existing question", entity); Assert.True(peer.Ok, peer.Error);
        Assert.True(f.S.SetActive(TestData.Owner, peer.Value!.Id, false).Ok);
        var result = f.Add(entity: entity); Assert.True(result.Ok, result.Error);
        var row = result.Value!; var form = result.CustomDefinitionEdit!;
        Assert.Equal(entity, row.Entity); Assert.Equal(row.Id, form.FieldId); Assert.Equal(entity, form.Entity);
        Assert.Equal(FieldGovernance.KeyFor(entity, row.Id), row.FieldKey); Assert.True(row.Active); Assert.False(row.Required);
        Assert.Equal("Synthetic help", row.HelpText); Assert.Equal(2, row.Sort); Assert.NotNull(row.CreatedAt);
        Assert.Equal(2, form.Fields.Count); Assert.All(form.Fields, item => Assert.NotNull(item.EditVersion));
        Assert.False(form.Fields.Single(item => item.Id == peer.Value.Id).Active);
        Assert.True(CustomFieldDefinitionState.Matches(row, f.Rows(entity).Single(item => item.Id == row.Id)));
        Assert.Same(result.EditVersion, row.EditVersion); Assert.Same(result.EditVersion, form.EditVersion);
        Assert.Equal(2, f.Audits());
    }

    [Theory]
    [InlineData("customer", "", "text", null, "")]
    [InlineData("customer", "x", "text", null, "")]
    [InlineData("unknown", "Synthetic invalid", "text", null, "")]
    [InlineData("customer", "Synthetic question with a deliberately long label", "text", null, "")]
    [InlineData("customer", "Synthetic invalid", "not-a-kind", null, "")]
    [InlineData("sale", "Synthetic invalid", "photo", null, "")]
    [InlineData("customer", "Synthetic invalid", "select", null, "")]
    [InlineData("customer", "Synthetic invalid", "text", null, "Synthetic\ncontrol")]
    public void ValidationRefusesWithKnownNoCommitAndNoCreatedIdentity(string entity, string label, string type, string? options, string help)
    {
        using var f = new CustomAddFixture(); var before = f.State();
        NoCommit(f.S.Add(TestData.Owner, entity, label, type, options, false, help)); Assert.Equal(before, f.State());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrimmedCaseInsensitiveDuplicateRefusesEvenIfPutAway(bool hidden)
    {
        using var f = new CustomAddFixture(); var first = f.Add(); Assert.True(first.Ok, first.Error);
        if (hidden) Assert.True(f.S.SetActive(TestData.Owner, first.Value!.Id, false).Ok);
        var before = f.State(); NoCommit(f.Add("  SYNTHETIC NEW QUESTION  ")); Assert.Equal(before, f.State());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("manager")]
    public void ActorPreflightRefusalDoesNotStartAnyWrite(string mode)
    {
        using var f = new CustomAddFixture(); var before = f.State();
        var actor = mode == "missing" ? null : new User { Id = 1, Name = "Synthetic actor", Role = mode == "manager" ? "manager" : "owner", Active = mode != "inactive" };
        NoCommit(f.S.Add(actor, "customer", "Synthetic denied", "text", null, false)); Assert.Equal(before, f.State());
    }

    [Theory]
    [InlineData("active=0")]
    [InlineData("role='manager'")]
    public void CurrentStoredOwnerIsCheckedInsideTransaction(string change)
    {
        using var f = new CustomAddFixture(); f.Sql("UPDATE users SET " + change + " WHERE id=1;");
        var before = f.State(); NoCommit(f.Add()); Assert.Equal(before, f.State());
    }

    [Theory]
    [InlineData("insert")]
    [InlineData("stable-key")]
    [InlineData("audit")]
    [InlineData("receipt")]
    public void StatementFaultRollsBackInsertKeySequenceAndAuditAndPermitsExplicitRetry(string stage)
    {
        using var f = new CustomAddFixture();
        f.Sql(stage == "insert" ? "CREATE TRIGGER synthetic_add_fault BEFORE INSERT ON custom_fields BEGIN SELECT RAISE(ABORT,'synthetic'); END;"
            : stage == "stable-key" ? "CREATE TRIGGER synthetic_add_fault BEFORE UPDATE OF field_key ON custom_fields BEGIN SELECT RAISE(ABORT,'synthetic'); END;"
            : stage == "audit" ? "CREATE TRIGGER synthetic_add_fault BEFORE INSERT ON audit_logs WHEN NEW.action='custom_field_add' BEGIN SELECT RAISE(ABORT,'synthetic'); END;"
            : "CREATE TRIGGER synthetic_add_fault AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_add' BEGIN DELETE FROM custom_fields WHERE id=CAST(NEW.entity_id AS INTEGER); END;");
        var before = f.State(); NoCommit(f.Add()); Assert.Equal(before, f.State());
        f.Sql("DROP TRIGGER synthetic_add_fault;"); var saved = f.Add(); Assert.True(saved.Ok, saved.Error); Assert.Single(f.Rows()); Assert.Equal(1, f.Audits());
    }

    [Fact]
    public void CommitAttemptFailureIsUnknownWithoutValueReceiptOrSafeRetryPromise()
    {
        using var f = new CustomAddFixture();
        f.Sql(@"CREATE TABLE synthetic_add_commit(parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_add_commit_audit AFTER INSERT ON audit_logs WHEN NEW.action='custom_field_add' BEGIN INSERT INTO synthetic_add_commit VALUES(-99999); END;");
        var before = f.State(); var result = f.Add();
        Assert.False(result.Ok); Assert.False(result.NoCommitConfirmed); Assert.Null(result.Value); Assert.Null(result.CustomDefinitionEdit); Assert.Null(result.EditVersion);
        Assert.Contains("unconfirmed", result.Error!); Assert.Equal(before, f.State());
    }

    [Fact]
    public void ReceiptUsesStoredTimestampRatherThanASecondClockRead()
    {
        using var f = new CustomAddFixture();
        f.Sql("CREATE TRIGGER synthetic_stored_timestamp AFTER INSERT ON custom_fields BEGIN UPDATE custom_fields SET created_at='2001-01-02T03:04:05+00:00' WHERE id=NEW.id; END;");
        var saved = f.Add(); Assert.True(saved.Ok, saved.Error);
        Assert.Equal("2001-01-02T03:04:05+00:00", saved.Value!.CreatedAt);
        Assert.Equal(saved.Value.CreatedAt, saved.CustomDefinitionEdit!.Fields.Single().CreatedAt);
    }

    [Fact]
    public void InteractiveSessionRequiredAndAuditUsesStoredCurrentOwnerName()
    {
        using var f = new CustomAddFixture(); f.T.Database.Sessions.Enable(); var before = f.State(); NoCommit(f.Add()); Assert.Equal(before, f.State());
        var auth = new AuthService(f.T.Database, f.T.Audit, f.T.Settings, f.T.Log); var login = auth.Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        f.Sql("UPDATE users SET name='Synthetic current owner' WHERE id=1;");
        var saved = f.S.Add(login.Value!, "customer", "Synthetic authenticated question", "text", null, false); Assert.True(saved.Ok, saved.Error);
        Assert.Equal("Synthetic current owner", f.T.Database.Read(c => f.T.Database.QuerySingle<string>(c, "SELECT user_name FROM audit_logs WHERE action='custom_field_add' ORDER BY id DESC LIMIT 1;")));
    }

    [Fact]
    public void ReceiptCopiesAreDefensiveAndHandoffMetadataIsNotSerialized()
    {
        using var f = new CustomAddFixture(); var saved = f.Add(); Assert.True(saved.Ok, saved.Error); var form = saved.CustomDefinitionEdit!;
        saved.Value!.Label = "Synthetic mutated value"; form.Fields.Single().Label = "Synthetic mutated copy";
        Assert.Equal("Synthetic new question", form.Fields.Single().Label); Assert.Equal("Synthetic new question", f.Rows().Single().Label);
        var json = JsonSerializer.Serialize(saved); Assert.DoesNotContain("CustomDefinitionEdit", json); Assert.DoesNotContain("EditVersion", json);
    }

    [Fact]
    public void ExactReceiptDoesNotAdoptLaterPeerAndOldTokenRefusesNextEdit()
    {
        using var f = new CustomAddFixture(); var first = f.Add(); Assert.True(first.Ok, first.Error); var form = first.CustomDefinitionEdit!;
        Assert.True(f.Add("Synthetic later peer").Ok); Assert.Single(form.Fields);
        var edit = f.S.UpdateChecked(TestData.Owner, first.Value!.Id, "Synthetic stale edit", "text", null, false, "", first.EditVersion);
        Assert.False(edit.Ok); Assert.True(edit.NoCommitConfirmed); Assert.Null(edit.CustomDefinitionEdit);
        Assert.Equal("Synthetic new question", f.Rows().Single(row => row.Id == first.Value.Id).Label);
    }

    [Fact]
    public void ReceiptTokensCanImmediatelyAuthorizeADeliberateNewEdit()
    {
        using var f = new CustomAddFixture(); var saved = f.Add(); Assert.True(saved.Ok, saved.Error);
        var updated = f.S.UpdateChecked(TestData.Owner, saved.Value!.Id, "Synthetic reviewed edit", "text", null, false, "", saved.EditVersion);
        Assert.True(updated.Ok, updated.Error); Assert.Equal(saved.Value.FieldKey, updated.CustomDefinitionEdit!.Fields.Single().FieldKey);
    }

    [Fact]
    public void AppendUsesLiveLockedSortAndDoesNotRewriteExistingAnswersOrOtherEntities()
    {
        using var f = new CustomAddFixture(); var peer = f.Add("Synthetic peer"); Assert.True(peer.Ok, peer.Error);
        var customer = TestData.AddCustomer(f.T);
        Assert.True(f.S.SaveValues(TestData.Owner, "customer", customer, new Dictionary<int, string?> { [peer.Value!.Id] = "Synthetic old answer" }).Ok);
        Assert.True(f.Add("Synthetic other entity", "supplier").Ok);
        var db = new Database(f.T.Database.FilePath, pooling: false, durable: false);
        db.Tx(c => { db.Execute(c, "UPDATE custom_fields SET sort=90,label='Synthetic concurrent peer' WHERE id=@id;", new { id = peer.Value.Id }); });
        var saved = f.Add(); Assert.True(saved.Ok, saved.Error); Assert.Equal(91, saved.Value!.Sort);
        Assert.Equal("Synthetic concurrent peer", saved.CustomDefinitionEdit!.Fields.Single(row => row.Id == peer.Value.Id).Label);
        Assert.Equal("Synthetic old answer", f.T.Database.Read(c => f.T.Database.QuerySingle<string>(c, "SELECT value FROM custom_values WHERE field_id=@id;", new { id = peer.Value.Id })));
        Assert.Single(f.Rows("supplier"));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("ack-callback")]
    [InlineData("known-rollback")]
    public void SyntheticSharedGuardAllowsOnlyKnownRollbackRetryOrCompleteConsumedReset(string mode)
    {
        var guard = new PostingAttemptGuard(); Assert.True(guard.TryBegin()); Assert.False(guard.TryBegin());
        if (mode == "ack-callback") guard.AcknowledgeCommit();
        else guard.ObserveFailure(new ServiceResult { Error = "Synthetic failure", NoCommitConfirmed = mode == "known-rollback" });
        guard.ObserveException(); Assert.Equal(mode == "known-rollback", guard.Ready);
        Assert.Equal(mode == "ack-callback", guard.WasAcknowledged);
        // Not VM/timer/regrant execution. These are synthetic observations of the shared public guard.
    }
}
