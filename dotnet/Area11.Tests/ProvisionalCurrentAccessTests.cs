using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Current PR boundary regression sources; C#/provider/WPF execution still required.</summary>
public sealed class ProvisionalCurrentAccessTests
{
    private sealed class CurrentPrFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public ProvisionalReturnService P { get; }
        public User Manager { get; }
        public User Cashier { get; }
        public int Product { get; }
        public SaleResult Sale { get; }
        public ProvisionalResult Pr { get; }
        public CurrentPrFixture()
        {
            Manager = TestData.AddUser(T, "manager"); Cashier = TestData.Cashier(T);
            Product = TestData.AddProduct(T, batchQty: 100);
            var sale = TestData.Sales(T).Create(TestData.Cart(Product, qty: 3), TestData.Owner); Assert.True(sale.Ok, sale.Error); Sale = sale.Value!;
            P = TestData.Provisionals(T);
            var pr = P.Create(TestData.RushReturn(Product, qty: 1, unit: "base", refundPaisa: 30000), TestData.Owner); Assert.True(pr.Ok, pr.Error); Pr = pr.Value!;
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "settings", "users", "user_permissions", "products", "batches", "sales", "sale_items", "provisional_returns", "provisional_items",
            "provisional_return_allocations", "payments", "stock_movements", "audit_logs", "sqlite_sequence"
        }.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public User? Denied(string fault)
        {
            if (fault == "null") return null;
            if (fault == "missing") return new User { Id = int.MaxValue, Name = "Synthetic", Role = "manager" };
            if (fault == "inactive-input") return new User { Id = Manager.Id, Role = Manager.Role, Active = false };
            if (fault == "owner-inactive") { Execute("UPDATE users SET active=0 WHERE id=1;"); return TestData.Owner; }
            if (fault == "inactive-stored") Execute($"UPDATE users SET active=0 WHERE id={Manager.Id};");
            else if (fault == "role-changed") Execute($"UPDATE users SET role='cashier' WHERE id={Manager.Id};");
            else if (fault == "revoked") Assert.True(TestData.Permissions(T).Set(TestData.Owner, Manager.Id, Modules.Returns, false).Ok);
            else throw new ArgumentOutOfRangeException(nameof(fault));
            return Manager;
        }
        public bool Read(string route, User? user) => route switch
        {
            "page" => P.Page(user).Ok,
            "pending" => P.Pending(user).Count > 0,
            "history" => P.History(user).Count > 0,
            "get" => P.Get(Pr.Code, user) is not null,
            "lines" => P.Lines(Pr.Id, user).Count > 0,
            "search" => P.SearchBills(Sale.Code, user: user).Ok,
            "receipt" => TestData.ProvisionalReceipts(T).Build(Pr.Code, user).Ok,
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };
        public Area11.Core.Utils.ServiceResult Write(string route, User? user) => route switch
        {
            "create" => P.Create(TestData.RushReturn(Product, qty: 1, unit: "base"), user!),
            "link" => P.Link(Pr.Id, Sale.Code, user!),
            "cancel" => P.Cancel(Pr.Id, "Synthetic duplicate", user!),
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData("page", "null")]
    [InlineData("page", "missing")]
    [InlineData("page", "inactive-input")]
    [InlineData("page", "inactive-stored")]
    [InlineData("page", "role-changed")]
    [InlineData("page", "revoked")]
    [InlineData("page", "owner-inactive")]
    [InlineData("pending", "null")]
    [InlineData("pending", "missing")]
    [InlineData("pending", "inactive-input")]
    [InlineData("pending", "inactive-stored")]
    [InlineData("pending", "role-changed")]
    [InlineData("pending", "revoked")]
    [InlineData("pending", "owner-inactive")]
    [InlineData("history", "null")]
    [InlineData("history", "missing")]
    [InlineData("history", "inactive-input")]
    [InlineData("history", "inactive-stored")]
    [InlineData("history", "role-changed")]
    [InlineData("history", "revoked")]
    [InlineData("history", "owner-inactive")]
    [InlineData("get", "null")]
    [InlineData("get", "missing")]
    [InlineData("get", "inactive-input")]
    [InlineData("get", "inactive-stored")]
    [InlineData("get", "role-changed")]
    [InlineData("get", "revoked")]
    [InlineData("get", "owner-inactive")]
    [InlineData("lines", "null")]
    [InlineData("lines", "missing")]
    [InlineData("lines", "inactive-input")]
    [InlineData("lines", "inactive-stored")]
    [InlineData("lines", "role-changed")]
    [InlineData("lines", "revoked")]
    [InlineData("lines", "owner-inactive")]
    [InlineData("search", "null")]
    [InlineData("search", "missing")]
    [InlineData("search", "inactive-input")]
    [InlineData("search", "inactive-stored")]
    [InlineData("search", "role-changed")]
    [InlineData("search", "revoked")]
    [InlineData("search", "owner-inactive")]
    [InlineData("receipt", "null")]
    [InlineData("receipt", "missing")]
    [InlineData("receipt", "inactive-input")]
    [InlineData("receipt", "inactive-stored")]
    [InlineData("receipt", "role-changed")]
    [InlineData("receipt", "revoked")]
    [InlineData("receipt", "owner-inactive")]
    public void DeniedReaderCannotReuseOldIdsOrCodes(string route, string fault)
    {
        using var f = new CurrentPrFixture(); var actor = f.Denied(fault); var before = f.Snapshot();
        Assert.False(f.Read(route, actor)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData("create", "null")]
    [InlineData("create", "missing")]
    [InlineData("create", "inactive-input")]
    [InlineData("create", "inactive-stored")]
    [InlineData("create", "role-changed")]
    [InlineData("create", "revoked")]
    [InlineData("create", "owner-inactive")]
    [InlineData("link", "null")]
    [InlineData("link", "missing")]
    [InlineData("link", "inactive-input")]
    [InlineData("link", "inactive-stored")]
    [InlineData("link", "role-changed")]
    [InlineData("link", "revoked")]
    [InlineData("link", "owner-inactive")]
    [InlineData("cancel", "null")]
    [InlineData("cancel", "missing")]
    [InlineData("cancel", "inactive-input")]
    [InlineData("cancel", "inactive-stored")]
    [InlineData("cancel", "role-changed")]
    [InlineData("cancel", "revoked")]
    [InlineData("cancel", "owner-inactive")]
    public void DeniedWriterCannotMutateOrClaimUnknownCommit(string route, string fault)
    {
        using var f = new CurrentPrFixture(); var actor = f.Denied(fault); var before = f.Snapshot();
        var result = f.Write(route, actor); Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData("page", "owner")]
    [InlineData("page", "manager")]
    [InlineData("page", "cashier")]
    [InlineData("pending", "owner")]
    [InlineData("pending", "manager")]
    [InlineData("pending", "cashier")]
    [InlineData("history", "owner")]
    [InlineData("history", "manager")]
    [InlineData("history", "cashier")]
    [InlineData("get", "owner")]
    [InlineData("get", "manager")]
    [InlineData("get", "cashier")]
    [InlineData("lines", "owner")]
    [InlineData("lines", "manager")]
    [InlineData("lines", "cashier")]
    [InlineData("search", "owner")]
    [InlineData("search", "manager")]
    [InlineData("search", "cashier")]
    [InlineData("receipt", "owner")]
    [InlineData("receipt", "manager")]
    [InlineData("receipt", "cashier")]
    public void CurrentReturnsReadersKeepTheirExistingReadAccess(string route, string role)
    {
        using var f = new CurrentPrFixture(); var actor = role == "owner" ? TestData.Owner : role == "manager" ? f.Manager : f.Cashier;
        var before = f.Snapshot(); Assert.True(f.Read(route, actor)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData("create")]
    [InlineData("link")]
    [InlineData("cancel")]
    public void WriterAuditUsesStoredActorNotForgedDisplayName(string route)
    {
        using var f = new CurrentPrFixture(); f.Execute($"UPDATE users SET name='Current synthetic manager' WHERE id={f.Manager.Id};");
        var supplied = new User { Id = f.Manager.Id, Role = "manager", Name = "Forged old name" };
        var result = f.Write(route, supplied); Assert.True(result.Ok, result.Error);
        var action = "provisional_" + route;
        var name = f.T.Database.Read(c => f.T.Database.QuerySingle<string>(c, "SELECT user_name FROM audit_logs WHERE action=@action ORDER BY id DESC LIMIT 1;", new { action }));
        Assert.Equal("Current synthetic manager", name);
    }

    [Theory]
    [InlineData("link")]
    [InlineData("cancel")]
    public void OrdinaryReturnsReaderCannotMakeOwnerManagerDecision(string route)
    {
        using var f = new CurrentPrFixture(); var before = f.Snapshot(); var result = f.Write(route, f.Cashier);
        Assert.False(result.Ok); Assert.True(result.NoCommitConfirmed); Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void FailedPageIsNotSuccessfulEmptyAndRegrantRequiresFreshRead()
    {
        using var f = new CurrentPrFixture();
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner, f.Manager.Id, Modules.Returns, false).Ok);
        var denied = f.P.Page(f.Manager); Assert.False(denied.Ok); Assert.Null(denied.Value);
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner, f.Manager.Id, Modules.Returns, true).Ok);
        var fresh = f.P.Page(f.Manager); Assert.True(fresh.Ok, fresh.Error); Assert.Single(fresh.Value!);
        f.Execute("ALTER TABLE provisional_returns RENAME TO synthetic_unreadable_pr;");
        var failed = f.P.Page(f.Manager); Assert.False(failed.Ok); Assert.Null(failed.Value);
    }

    [Fact]
    public void ReceiptLineReadFailureCannotProduceAHeaderOnlySuccessfulSlip()
    {
        using var f = new CurrentPrFixture();
        f.Execute("ALTER TABLE provisional_items RENAME COLUMN unit_entered TO synthetic_unreadable_unit;");
        Assert.NotNull(f.P.Get(f.Pr.Code, f.Manager));
        var read = f.P.ReadReceipt(f.Pr.Code, f.Manager); Assert.False(read.Ok); Assert.Null(read.Value);
        var receipt = TestData.ProvisionalReceipts(f.T).Build(f.Pr.Code, f.Manager); Assert.False(receipt.Ok); Assert.Null(receipt.Value);
    }

    [Fact]
    public void ActorlessCompatibilityCallsAndMissingTargetsFailClosed()
    {
        using var f = new CurrentPrFixture();
        Assert.Empty(f.P.Pending()); Assert.Empty(f.P.History()); Assert.Null(f.P.Get(f.Pr.Code)); Assert.Empty(f.P.Lines(f.Pr.Id));
        Assert.False(f.P.SearchBills(f.Sale.Code).Ok); Assert.False(TestData.ProvisionalReceipts(f.T).Build(f.Pr.Code).Ok);
        Assert.False(f.P.ReadReceipt("MISSING-SYNTHETIC", f.Manager).Ok);
        Assert.Empty(f.P.Lines(int.MaxValue, f.Manager));
    }
}
