using System.IO;
using System.Text.Json;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated DB/photos only;header bytes here are synthetic,NOT WPF codec/Windows proof.</summary>
public sealed class IconAssetConcurrencyTests
{
    private static readonly byte[] Header = { 0, 0, 1, 0, 1, 0, 10, 20 };
    private sealed class IconFixture : IDisposable
    {
        public TestDatabase Test { get; } = new();
        public string Folder { get; } = Path.Combine(Path.GetTempPath(), "area11-icon-tests", Guid.NewGuid().ToString("N"));
        public IconFixture() { Directory.CreateDirectory(Folder); }
        public IconAssetService Service(IconFileStore? store = null) => new(Test.Settings, Test.Log, Folder, store);
        public SettingsEditForm Form(User? owner = null, params string[] extra)
        {
            var loaded = Test.Settings.LoadEditForm(owner ?? TestData.Owner, new[] { IconAssetService.Key }.Concat(extra));
            Assert.True(loaded.Ok, loaded.Error); return loaded.Value!;
        }
        public string[] Files() => Directory.GetFiles(Folder);
        public string State() => Test.Database.Read(c => string.Join("\n", new[] { "settings", "audit_logs", "sqlite_sequence" }.Select(table =>
        {
            var columns = Test.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(col => "quote(\"" + col + "\")"));
            return table + "=" + string.Join(";", Test.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public void Dispose() { Test.Dispose(); try { Directory.Delete(Folder, true); } catch (IOException) { } }
    }
    private sealed class IconFaultStore : IconFileStore
    {
        public Action? DuringFlush { get; init; }
        public bool FailWrite { get; init; }
        public bool FailFlush { get; init; }
        public bool FailDispose { get; init; }
        public bool FailDelete { get; init; }
        public bool Collision { get; init; }
        public string? RequestedPath { get; private set; }
        public override Stream CreateNew(string path)
        {
            RequestedPath = path;
            if (Collision) File.WriteAllText(path, "existing sentinel");
            var output = base.CreateNew(path);
            return FailWrite || FailDispose ? new IconFaultStream(output, FailWrite, FailDispose) : output;
        }
        public override void Flush(Stream output)
        {
            base.Flush(output); DuringFlush?.Invoke();
            if (FailFlush) throw new IOException("synthetic flush fault");
        }
        public override void Delete(string path)
        { if (FailDelete) throw new IOException("synthetic cleanup fault"); base.Delete(path); }
    }
    private sealed class IconFaultStream : Stream
    {
        private readonly Stream _inner;
        private readonly bool _write;
        private readonly bool _dispose;
        public IconFaultStream(Stream inner, bool write, bool dispose) { _inner = inner; _write = write; _dispose = dispose; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Flush() => _inner.Flush();
        public override void Write(byte[] buffer, int offset, int count)
        { if (_write) throw new IOException("synthetic write fault"); _inner.Write(buffer, offset, count); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose(); base.Dispose(disposing);
            if (disposing && _dispose) throw new IOException("synthetic dispose fault");
        }
    }

    [Fact]
    public void SuccessfulChoiceWritesOnlyAGeneratedCopyAndHandsBackExactState()
    {
        using var f = new IconFixture(); var form = f.Form(); var result = f.Service().Choose(TestData.Owner, Header, form);
        Assert.True(result.Ok, result.Error); Assert.NotNull(result.SettingsEdit);
        var name = result.SettingsEdit!.GetString(IconAssetService.Key);
        Assert.StartsWith("area11-icon-", name); Assert.Equal(48, name.Length);
        Assert.Equal(Header, File.ReadAllBytes(Path.Combine(f.Folder, name)));
        Assert.Equal(name, f.Test.Settings.GetString(IconAssetService.Key));
        Assert.Equal(1L, f.Test.Database.Read(c => Convert.ToInt64(f.Test.Database.Scalar(c, "SELECT COUNT(*) FROM audit_logs WHERE entity_id='appearance.iconFileName';"))));
        Assert.DoesNotContain("SettingsEdit", JsonSerializer.Serialize(result));
    }

    [Fact]
    public void TwoLoadedChoicesCannotOverwriteTheFirstSavedIcon()
    {
        using var f = new IconFixture(); var one = f.Form(); var two = f.Form();
        Assert.True(f.Service().Choose(TestData.Owner, Header, one).Ok); var state = f.State(); var files = f.Files();
        var refused = f.Service().Choose(TestData.Owner, Header, two);
        Assert.False(refused.Ok); Assert.True(refused.NoCommitConfirmed); Assert.Contains("changed", refused.Error!);
        Assert.Equal(state, f.State()); Assert.Equal(files, f.Files());
    }

    [Fact]
    public void OldResetCannotClearTheNewlySelectedIcon()
    {
        using var f = new IconFixture(); var old = f.Form(); Assert.True(f.Service().Choose(TestData.Owner, Header, old).Ok);
        var state = f.State(); var result = f.Service().Reset(TestData.Owner, old);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(state, f.State()); Assert.Single(f.Files());
    }

    [Fact]
    public void ResetPreservesThePreviousFileAndAllowsTheNextReviewedChoice()
    {
        using var f = new IconFixture(); var saved = f.Service().Choose(TestData.Owner, Header, f.Form()); Assert.True(saved.Ok, saved.Error);
        var reset = f.Service().Reset(TestData.Owner, saved.SettingsEdit); Assert.True(reset.Ok, reset.Error);
        Assert.Equal(string.Empty, reset.SettingsEdit!.GetString(IconAssetService.Key)); Assert.Single(f.Files());
        var next = f.Service().Choose(TestData.Owner, Header, reset.SettingsEdit); Assert.True(next.Ok, next.Error); Assert.Equal(2, f.Files().Length);
    }

    [Fact]
    public void MissingBaselineDoesNotCreateAFileOrChangeTheDatabase()
    {
        using var f = new IconFixture(); var before = f.State();
        Assert.False(f.Service().Choose(TestData.Owner, Header, null).Ok); Assert.False(f.Service().Reset(TestData.Owner, null).Ok);
        Assert.Equal(before, f.State()); Assert.Empty(f.Files());
    }

    [Fact]
    public void UnrelatedFormTokenCannotAuthorizeAnIconChange()
    {
        using var f = new IconFixture(); var result = f.Test.Settings.LoadEditForm(TestData.Owner, new[] { "store.name" }); Assert.True(result.Ok, result.Error);
        var before = f.State(); Assert.False(f.Service().Choose(TestData.Owner, Header, result.Value).Ok);
        Assert.Equal(before, f.State()); Assert.Empty(f.Files());
    }

    [Fact]
    public void OtherDatabaseInstanceTokenCannotAuthorizeAnIconChange()
    {
        using var f = new IconFixture(); using var other = new IconFixture(); var before = f.State();
        Assert.False(f.Service().Choose(TestData.Owner, Header, other.Form()).Ok); Assert.Equal(before, f.State()); Assert.Empty(f.Files());
    }

    [Fact]
    public void NonOwnerCannotUseTheOwnersIconForm()
    {
        using var f = new IconFixture(); var form = f.Form(); var cashier = TestData.Cashier(f.Test);
        Assert.True(TestData.Permissions(f.Test).Set(TestData.Owner, cashier.Id, Modules.Settings, true).Ok); var before = f.State();
        Assert.False(f.Service().Choose(cashier, Header, form).Ok); Assert.False(f.Service().Reset(cashier, form).Ok);
        Assert.Equal(before, f.State()); Assert.Empty(f.Files());
    }

    [Fact]
    public void StoredOwnerDeactivationRefusesCopyAndReset()
    {
        using var f = new IconFixture(); var form = f.Form();
        f.Test.Database.Tx(c => f.Test.Database.Execute(c, "UPDATE users SET active=0 WHERE id=1;")); var before = f.State();
        Assert.False(f.Service().Choose(TestData.Owner, Header, form).Ok); Assert.False(f.Service().Reset(TestData.Owner, form).Ok);
        Assert.Equal(before, f.State()); Assert.Empty(f.Files());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(2097153)]
    public void OutOfBoundsBytesDoNotTouchTheDatabaseOrFiles(int count)
    {
        using var f = new IconFixture(); var before = f.State();
        var result = f.Service().Choose(TestData.Owner, new byte[count], f.Form());
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, f.State()); Assert.Empty(f.Files());
    }

    [Fact]
    public void MissingAndWrongHeaderAreRefusedBeforeCopy()
    {
        using var f = new IconFixture(); var form = f.Form(); var before = f.State();
        Assert.False(f.Service().Choose(TestData.Owner, null, form).Ok);
        Assert.False(f.Service().Choose(TestData.Owner, new byte[8], form).Ok);
        Assert.False(f.Service().Choose(TestData.Owner, new byte[] { 0, 0, 1, 0, 0, 0 }, form).Ok);
        Assert.Equal(before, f.State()); Assert.Empty(f.Files());
    }

    [Theory]
    [InlineData("write")]
    [InlineData("flush")]
    [InlineData("dispose")]
    public void CopyFaultRemovesOnlyItsOwnedFileBeforeAnySettingWrite(string kind)
    {
        using var f = new IconFixture(); var previous = Path.Combine(f.Folder, "previous.ico"); File.WriteAllText(previous, "previous sentinel");
        var before = f.State(); var store = new IconFaultStore { FailWrite = kind == "write", FailFlush = kind == "flush", FailDispose = kind == "dispose" };
        var result = f.Service(store).Choose(TestData.Owner, Header, f.Form());
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, f.State());
        Assert.Single(f.Files()); Assert.Equal("previous sentinel", File.ReadAllText(previous));
    }

    [Fact]
    public void CollisionNeverDeletesOrOverwritesThePreexistingFile()
    {
        using var f = new IconFixture(); var store = new IconFaultStore { Collision = true }; var before = f.State();
        var result = f.Service(store).Choose(TestData.Owner, Header, f.Form());
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, f.State());
        Assert.Equal("existing sentinel", File.ReadAllText(store.RequestedPath!));
    }

    [Fact]
    public void AuditFaultRollsBackPointerAndRemovesOnlyTheNewOwnedCopy()
    {
        using var f = new IconFixture(); var form = f.Form();
        f.Test.Database.Tx(c => f.Test.Database.Execute(c, "CREATE TRIGGER synthetic_icon BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;"));
        var before = f.State(); var result = f.Service().Choose(TestData.Owner, Header, form);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, f.State()); Assert.Empty(f.Files());
        f.Test.Database.Tx(c => f.Test.Database.Execute(c, "DROP TRIGGER synthetic_icon;"));
        Assert.True(f.Service().Choose(TestData.Owner, Header, form).Ok);
    }

    [Fact]
    public void ConcurrentChangeAfterCopyIsComparedAgainAndNewCopyIsDiscarded()
    {
        using var f = new IconFixture(); var form = f.Form();
        const string concurrent = "area11-icon-11111111111111111111111111111111.ico";
        var store = new IconFaultStore { DuringFlush = () =>
            Assert.True(f.Test.Settings.Set(IconAssetService.Key, concurrent, TestData.Owner).Ok) };
        var result = f.Service(store).Choose(TestData.Owner, Header, form);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Empty(f.Files());
        Assert.Equal(concurrent, f.Test.Settings.GetString(IconAssetService.Key));
    }

    [Fact]
    public void DeferredCommitFailureRetainsTheOwnedFileWithoutClaimingSafeRetry()
    {
        using var f = new IconFixture(); var form = f.Form();
        f.Test.Database.Tx(c => f.Test.Database.Execute(c, @"
CREATE TABLE synthetic_icon_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER synthetic_icon_commit_audit AFTER INSERT ON audit_logs BEGIN
INSERT INTO synthetic_icon_commit(parent_id) VALUES(-99999); END;"));
        var before = f.State(); var result = f.Service().Choose(TestData.Owner, Header, form);
        Assert.False(result.Ok); Assert.False(result.NoCommitConfirmed); Assert.Contains("unconfirmed", result.Error!);
        Assert.Equal(before, f.State()); Assert.Single(f.Files());
    }

    [Fact]
    public void FailedCleanupDoesNotTurnConfirmedRollbackIntoACommitClaim()
    {
        using var f = new IconFixture(); var form = f.Form();
        var store = new IconFaultStore { FailFlush = true, FailDelete = true }; var before = f.State();
        var result = f.Service(store).Choose(TestData.Owner, Header, form);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, f.State()); Assert.Single(f.Files());
    }

    [Fact]
    public void IconHandoffDoesNotBlessAStaleUntouchedMainSetting()
    {
        using var f = new IconFixture(); var form = f.Form(null, "store.name");
        Assert.True(f.Test.Settings.Set("store.name", "Concurrent shop", TestData.Owner).Ok);
        var saved = f.Service().Choose(TestData.Owner, Header, form); Assert.True(saved.Ok, saved.Error); var before = f.State();
        var result = f.Test.Settings.SaveEditForm(new Dictionary<string, object?> { ["store.name"] = "Old shop" }, TestData.Owner, saved.SettingsEdit);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, f.State());
    }

    [Fact]
    public void EnabledHostRequiresAnActualOwnerSession()
    {
        using var f = new IconFixture(); var old = f.Form(); f.Test.Database.Sessions.Enable();
        Assert.False(f.Service().Choose(TestData.Owner, Header, old).Ok); Assert.Empty(f.Files());
        var login = new AuthService(f.Test.Database, f.Test.Audit, f.Test.Settings, f.Test.Log).Login("admin", "area11"); Assert.True(login.Ok, login.Error);
        var form = f.Form(login.Value!); var result = f.Service().Choose(login.Value!, Header, form); Assert.True(result.Ok, result.Error);
    }
}
