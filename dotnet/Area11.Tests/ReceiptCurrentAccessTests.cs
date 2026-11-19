using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Stored-current-account receipt/history boundaries. Source regressions; execution required.</summary>
public sealed class ReceiptCurrentAccessTests
{
    private sealed class CurrentReceiptFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public User Actor { get; }
        public SaleResult Sale { get; }
        public ReturnResult Return { get; }
        public ReceiptService Receipts { get; }
        public ReturnReceiptService ReturnReceipts { get; }
        public SaleHistoryService History { get; }
        public CurrentReceiptFixture()
        {
            Actor = TestData.AddUser(T, "manager");
            var product = TestData.AddProduct(T);
            var sale = TestData.Sales(T).Create(TestData.Cart(product, qty: 2), TestData.Owner);
            Assert.True(sale.Ok, sale.Error); Sale = sale.Value!;
            var back = TestData.Returns(T).Create(TestData.Return(Sale, 1), TestData.Owner);
            Assert.True(back.Ok, back.Error); Return = back.Value!;
            var permissions = TestData.Permissions(T);
            Receipts = new ReceiptService(T.Database, T.Settings, T.Log, permissions);
            ReturnReceipts = new ReturnReceiptService(T.Database, T.Settings, T.Log, permissions);
            History = new SaleHistoryService(T.Database, Receipts, ReturnReceipts, T.Audit, permissions, T.Log);
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public void Grant(string module, bool allowed) => Execute(
            $"INSERT OR REPLACE INTO user_permissions(user_id,module_key,allowed) VALUES({Actor.Id},'{module}',{(allowed ? 1 : 0)});");
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "customers", "batches", "sales", "sale_items", "sale_returns", "payments", "stock_movements", "audit_logs", "sqlite_sequence"
        }.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public bool Call(string route, User? actor) => route switch
        {
            "sale-counter" => Verdict(Receipts.Build(Sale.SaleId, actor)),
            "return-counter" => Verdict(ReturnReceipts.Build(Return.Code, actor)),
            "sale-history-direct" => Verdict(Receipts.Build(Sale.SaleId, actor, ReceiptReadPurpose.History)),
            "return-history-direct" => Verdict(ReturnReceipts.Build(Return.Code, actor, ReceiptReadPurpose.History)),
            "search" => Verdict(History.Search(actor, new SaleHistoryFilter { Search = Sale.Code })),
            "open" => Verdict(History.Open(actor, Sale.SaleId)),
            "history-sale" => Verdict(History.BuildReceipt(actor, Sale.SaleId)),
            "history-return" => Verdict(History.BuildReturnReceipt(actor, Return.Code)),
            "bulk" => Verdict(History.BuildBulkBills(actor, new[] { Sale.SaleId })),
            "audit-sale" => History.RecordReprint(actor, Sale.SaleId).Ok,
            "audit-return" => History.RecordReturnReprint(actor, Return.Code).Ok,
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };
        private static bool Verdict<T>(ServiceResult<T> result)
        {
            if (!result.Ok)
            {
                Assert.Equal(default(T), result.Value);
                Assert.False(string.IsNullOrWhiteSpace(result.Error));
            }
            return result.Ok;
        }
        public void Change(string fault, bool restore = false)
        {
            switch (fault)
            {
                case "inactive": Execute($"UPDATE users SET active={(restore ? 1 : 0)} WHERE id={Actor.Id};"); break;
                case "role": Execute($"UPDATE users SET role='{(restore ? "manager" : "cashier")}' WHERE id={Actor.Id};"); break;
                case "revoke": foreach (var module in new[] { "pos", "returns", "sales" }) Grant(module, restore); break;
                case "grant-read": Execute(restore ? "ALTER TABLE hidden_permissions RENAME TO user_permissions;" : "ALTER TABLE user_permissions RENAME TO hidden_permissions;"); break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
        }
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData("sale-counter", "inactive")]
    [InlineData("sale-counter", "role")]
    [InlineData("sale-counter", "revoke")]
    [InlineData("sale-counter", "grant-read")]
    [InlineData("return-counter", "inactive")]
    [InlineData("return-counter", "role")]
    [InlineData("return-counter", "revoke")]
    [InlineData("return-counter", "grant-read")]
    [InlineData("sale-history-direct", "inactive")]
    [InlineData("sale-history-direct", "role")]
    [InlineData("sale-history-direct", "revoke")]
    [InlineData("sale-history-direct", "grant-read")]
    [InlineData("return-history-direct", "inactive")]
    [InlineData("return-history-direct", "role")]
    [InlineData("return-history-direct", "revoke")]
    [InlineData("return-history-direct", "grant-read")]
    [InlineData("search", "inactive")]
    [InlineData("search", "role")]
    [InlineData("search", "revoke")]
    [InlineData("search", "grant-read")]
    [InlineData("open", "inactive")]
    [InlineData("open", "role")]
    [InlineData("open", "revoke")]
    [InlineData("open", "grant-read")]
    [InlineData("history-sale", "inactive")]
    [InlineData("history-sale", "role")]
    [InlineData("history-sale", "revoke")]
    [InlineData("history-sale", "grant-read")]
    [InlineData("history-return", "inactive")]
    [InlineData("history-return", "role")]
    [InlineData("history-return", "revoke")]
    [InlineData("history-return", "grant-read")]
    [InlineData("bulk", "inactive")]
    [InlineData("bulk", "role")]
    [InlineData("bulk", "revoke")]
    [InlineData("bulk", "grant-read")]
    [InlineData("audit-sale", "inactive")]
    [InlineData("audit-sale", "role")]
    [InlineData("audit-sale", "revoke")]
    [InlineData("audit-sale", "grant-read")]
    [InlineData("audit-return", "inactive")]
    [InlineData("audit-return", "role")]
    [InlineData("audit-return", "revoke")]
    [InlineData("audit-return", "grant-read")]
    public void ChangedCurrentAccountDeniesPayloadOrAuditAndSameServiceRegrants(string route, string fault)
    {
        using var f = new CurrentReceiptFixture();
        Assert.True(f.Call(route, f.Actor));
        f.Change(fault);
        var before = f.Snapshot();
        Assert.False(f.Call(route, f.Actor));
        Assert.Equal(before, f.Snapshot());
        f.Change(fault, restore: true);
        Assert.True(f.Call(route, f.Actor));
    }

    [Theory]
    [InlineData("sale-counter", "null")]
    [InlineData("sale-counter", "zero")]
    [InlineData("sale-counter", "inactive")]
    [InlineData("sale-counter", "forged-owner")]
    [InlineData("return-counter", "null")]
    [InlineData("return-counter", "zero")]
    [InlineData("return-counter", "inactive")]
    [InlineData("return-counter", "forged-owner")]
    [InlineData("sale-history-direct", "null")]
    [InlineData("sale-history-direct", "zero")]
    [InlineData("sale-history-direct", "inactive")]
    [InlineData("sale-history-direct", "forged-owner")]
    [InlineData("return-history-direct", "null")]
    [InlineData("return-history-direct", "zero")]
    [InlineData("return-history-direct", "inactive")]
    [InlineData("return-history-direct", "forged-owner")]
    [InlineData("search", "null")]
    [InlineData("search", "zero")]
    [InlineData("search", "inactive")]
    [InlineData("search", "forged-owner")]
    [InlineData("open", "null")]
    [InlineData("open", "zero")]
    [InlineData("open", "inactive")]
    [InlineData("open", "forged-owner")]
    [InlineData("history-sale", "null")]
    [InlineData("history-sale", "zero")]
    [InlineData("history-sale", "inactive")]
    [InlineData("history-sale", "forged-owner")]
    [InlineData("history-return", "null")]
    [InlineData("history-return", "zero")]
    [InlineData("history-return", "inactive")]
    [InlineData("history-return", "forged-owner")]
    [InlineData("bulk", "null")]
    [InlineData("bulk", "zero")]
    [InlineData("bulk", "inactive")]
    [InlineData("bulk", "forged-owner")]
    [InlineData("audit-sale", "null")]
    [InlineData("audit-sale", "zero")]
    [InlineData("audit-sale", "inactive")]
    [InlineData("audit-sale", "forged-owner")]
    [InlineData("audit-return", "null")]
    [InlineData("audit-return", "zero")]
    [InlineData("audit-return", "inactive")]
    [InlineData("audit-return", "forged-owner")]
    public void InvalidActorCannotUseKnownStoredBillOrReturnIds(string route, string actorKind)
    {
        using var f = new CurrentReceiptFixture();
        User? actor = actorKind switch
        {
            "null" => null,
            "zero" => new User { Id = 0, Name = "Unknown", Role = "owner" },
            "inactive" => new User { Id = f.Actor.Id, Name = "Old", Role = "manager", Active = false },
            _ => new User { Id = f.Actor.Id, Name = "Forged", Role = "owner" }
        };
        var before = f.Snapshot();
        Assert.False(f.Call(route, actor));
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void IndependentSalesPosAndReturnsGrantsCannotSubstituteForEachOther()
    {
        using var f = new CurrentReceiptFixture();
        f.Grant("pos", false); f.Grant("returns", false); f.Grant("sales", true);
        Assert.False(f.Call("sale-counter", f.Actor)); Assert.False(f.Call("return-counter", f.Actor));
        foreach (var route in new[] { "sale-history-direct", "return-history-direct", "history-sale", "history-return", "search", "open", "bulk", "audit-sale", "audit-return" })
            Assert.True(f.Call(route, f.Actor));
        f.Grant("sales", false); f.Grant("pos", true); f.Grant("returns", true);
        Assert.True(f.Call("sale-counter", f.Actor)); Assert.True(f.Call("return-counter", f.Actor));
        Assert.False(f.History.CanLook(f.Actor));
        foreach (var route in new[] { "sale-history-direct", "return-history-direct", "history-sale", "history-return", "search", "open", "bulk", "audit-sale", "audit-return" })
            Assert.False(f.Call(route, f.Actor));
    }

    [Fact]
    public void BothReprintAuditsUseStoredCurrentNameNotCallerAuthoredName()
    {
        using var f = new CurrentReceiptFixture();
        f.Execute($"UPDATE users SET name='Current printer' WHERE id={f.Actor.Id};");
        Assert.True(f.Call("audit-sale", f.Actor)); Assert.True(f.Call("audit-return", f.Actor));
        var names = f.T.Database.Read(c => f.T.Database.Query<string>(c,
            "SELECT user_name FROM audit_logs WHERE action IN ('sale_reprint','return_reprint') ORDER BY id;"));
        Assert.Equal(2, names.Count); Assert.All(names, name => Assert.Equal("Current printer", name));
    }

    [Fact]
    public void AuditInsertFaultIsFailureAndDoesNotSilentlyClaimRecorded()
    {
        using var f = new CurrentReceiptFixture();
        f.Execute("CREATE TRIGGER stop_receipt_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic audit fault'); END;");
        var before = f.Snapshot();
        Assert.False(f.Call("audit-sale", f.Actor)); Assert.False(f.Call("audit-return", f.Actor));
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void MissingReturnCannotInventAReprintAudit()
    {
        using var f = new CurrentReceiptFixture(); var before = f.Snapshot();
        Assert.False(f.History.RecordReturnReprint(f.Actor, "RET-NOT-HERE").Ok);
        Assert.False(f.History.RecordReturnReprint(f.Actor, " ").Ok);
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void UnknownReceiptPurposeFailsClosedWithoutPayloadOrMutation()
    {
        using var f = new CurrentReceiptFixture(); var before = f.Snapshot();
        var sale = f.Receipts.Build(f.Sale.SaleId, f.Actor, (ReceiptReadPurpose)999);
        var back = f.ReturnReceipts.Build(f.Return.Code, f.Actor, (ReceiptReadPurpose)999);
        Assert.False(sale.Ok); Assert.Null(sale.Value); Assert.False(back.Ok); Assert.Null(back.Value);
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void StoredOwnerMustStillBeActiveAndSameRoleBeforeOrdinaryBypass()
    {
        using var f = new CurrentReceiptFixture();
        f.Execute("ALTER TABLE user_permissions RENAME TO hidden_permissions;");
        Assert.True(f.Call("history-sale", TestData.Owner)); Assert.True(f.Call("return-counter", TestData.Owner));
        f.Execute("UPDATE users SET active=0 WHERE id=1;"); var before = f.Snapshot();
        Assert.False(f.Call("history-sale", TestData.Owner)); Assert.False(f.Call("audit-return", TestData.Owner));
        Assert.Equal(before, f.Snapshot());
        f.Execute("UPDATE users SET active=1, role='manager' WHERE id=1;"); before = f.Snapshot();
        Assert.False(f.Call("history-return", TestData.Owner)); Assert.False(f.Call("audit-sale", TestData.Owner));
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void BulkMissingBillReturnsNoPartialJobAndDoesNotWrite()
    {
        using var f = new CurrentReceiptFixture(); var before = f.Snapshot();
        var result = f.History.BuildBulkBills(f.Actor, new[] { f.Sale.SaleId, int.MaxValue });
        Assert.False(result.Ok); Assert.Null(result.Value); Assert.Equal(before, f.Snapshot());
    }
}
