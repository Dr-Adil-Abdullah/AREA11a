using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Quarantine current access and real-schema statement-fault regressions; execution required.</summary>
public sealed class QuarantineCurrentAccessTests
{
    private sealed class QuarantineAccessFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public User Actor { get; }
        public ReturnResult Return { get; }
        public QuarantineService Q { get; }
        public QuarantineAccessFixture()
        {
            Actor = TestData.AddUser(T, "manager");
            var product = TestData.AddProduct(T, batchQty: 100);
            var sale = TestData.Sales(T).Create(TestData.Cart(product, qty: 3), TestData.Owner);
            Assert.True(sale.Ok, sale.Error);
            var back = TestData.Returns(T).Create(TestData.Return(sale.Value!, 3, restock: false), TestData.Owner);
            Assert.True(back.Ok, back.Error); Return = back.Value!; Q = TestData.Quarantine(T);
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public long Number(string sql) => T.Database.Read(c => Convert.ToInt64(T.Database.Scalar(c, sql)));
        private string Tables(params string[] tables) => T.Database.Read(c => string.Join("\n", tables.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public string Snapshot() => Tables("settings", "customers", "products", "batches", "sales", "sale_items", "sale_returns", "payments", "stock_movements", "audit_logs", "sqlite_sequence");
        public string MoneySnapshot() => Tables("settings", "customers", "sales", "sale_items", "payments");
        public bool Call(string route, User? actor) => route switch
        {
            "pending" => Verdict(Q.Pending(actor)), "history" => Verdict(Q.History(actor)), "count" => Verdict(Q.PendingCount(actor)),
            _ => Q.Dispose(actor, Return.ReturnId, route, "checked goods").Ok
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
                case "revoke": Execute($"INSERT OR REPLACE INTO user_permissions(user_id,module_key,allowed) VALUES({Actor.Id},'returns',{(restore ? 1 : 0)});"); break;
                case "grant-read": Execute(restore ? "ALTER TABLE hidden_permissions RENAME TO user_permissions;" : "ALTER TABLE user_permissions RENAME TO hidden_permissions;"); break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
        }
        public void Reconcile(string outcome)
        {
            var count = Q.PendingCount(Actor); Assert.True(count.Ok, count.Error); Assert.Equal(0, count.Value);
            var history = Q.History(Actor); Assert.True(history.Ok, history.Error);
            Assert.Equal(outcome, Assert.Single(history.Value!).Disposition);
            Assert.Equal(outcome == QuarantineService.Expired ? 97L : 100L, Number("SELECT SUM(qty_base) FROM batches;"));
            Assert.Equal(outcome == QuarantineService.Expired ? 0L : 1L, Number("SELECT COUNT(*) FROM stock_movements WHERE ref_type='quarantine';"));
            Assert.Equal(1L, Number("SELECT COUNT(*) FROM audit_logs WHERE action LIKE 'quarantine_%';"));
        }
        public void Fault(string stage, bool after)
        {
            var target = stage switch
            {
                "disposition" => "BEFORE UPDATE OF disposition ON sale_returns",
                "batch" => "BEFORE UPDATE OF qty_base ON batches",
                "movement" => "BEFORE INSERT ON stock_movements",
                "audit" => "BEFORE INSERT ON audit_logs",
                _ => throw new ArgumentOutOfRangeException(nameof(stage))
            };
            Execute("CREATE TRIGGER quarantine_fault " + (after ? target.Replace("BEFORE ", "AFTER ", StringComparison.Ordinal) : target)
                + " BEGIN SELECT RAISE(ABORT,'private-quarantine-fault'); END;");
        }
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData("pending", "inactive")]
    [InlineData("pending", "role")]
    [InlineData("pending", "revoke")]
    [InlineData("pending", "grant-read")]
    [InlineData("history", "inactive")]
    [InlineData("history", "role")]
    [InlineData("history", "revoke")]
    [InlineData("history", "grant-read")]
    [InlineData("count", "inactive")]
    [InlineData("count", "role")]
    [InlineData("count", "revoke")]
    [InlineData("count", "grant-read")]
    [InlineData("restocked", "inactive")]
    [InlineData("restocked", "role")]
    [InlineData("restocked", "revoke")]
    [InlineData("restocked", "grant-read")]
    [InlineData("expired", "inactive")]
    [InlineData("expired", "role")]
    [InlineData("expired", "revoke")]
    [InlineData("expired", "grant-read")]
    [InlineData("sold", "inactive")]
    [InlineData("sold", "role")]
    [InlineData("sold", "revoke")]
    [InlineData("sold", "grant-read")]
    public void CurrentChangeDeniesWithoutMutationThenSameServiceRegrants(string route, string fault)
    {
        using var f = new QuarantineAccessFixture();
        Assert.True(f.Q.CheckDecisionAccess(f.Actor).Ok);
        f.Change(fault); var before = f.Snapshot();
        Assert.False(f.Q.CheckDecisionAccess(f.Actor).Ok);
        Assert.False(f.Call(route, f.Actor)); Assert.Equal(before, f.Snapshot());
        f.Change(fault, restore: true); Assert.True(f.Call(route, f.Actor));
        if (QuarantineService.Outcomes.Contains(route)) f.Reconcile(route);
    }

    [Theory]
    [InlineData("pending", "null")]
    [InlineData("pending", "zero")]
    [InlineData("pending", "inactive")]
    [InlineData("pending", "forged-owner")]
    [InlineData("history", "null")]
    [InlineData("history", "zero")]
    [InlineData("history", "inactive")]
    [InlineData("history", "forged-owner")]
    [InlineData("count", "null")]
    [InlineData("count", "zero")]
    [InlineData("count", "inactive")]
    [InlineData("count", "forged-owner")]
    [InlineData("restocked", "null")]
    [InlineData("restocked", "zero")]
    [InlineData("restocked", "inactive")]
    [InlineData("restocked", "forged-owner")]
    [InlineData("expired", "null")]
    [InlineData("expired", "zero")]
    [InlineData("expired", "inactive")]
    [InlineData("expired", "forged-owner")]
    [InlineData("sold", "null")]
    [InlineData("sold", "zero")]
    [InlineData("sold", "inactive")]
    [InlineData("sold", "forged-owner")]
    public void InvalidActorCannotReadCountOrDecideKnownReturn(string route, string kind)
    {
        using var f = new QuarantineAccessFixture();
        User? actor = kind switch
        {
            "null" => null,
            "zero" => new User { Id = 0, Name = "Unknown", Role = "owner" },
            "inactive" => new User { Id = f.Actor.Id, Name = "Old", Role = "manager", Active = false },
            _ => new User { Id = f.Actor.Id, Name = "Forged", Role = "owner" }
        };
        var before = f.Snapshot(); Assert.False(f.Q.CheckDecisionAccess(actor).Ok);
        Assert.False(f.Call(route, actor)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData("restocked", "disposition", false)]
    [InlineData("restocked", "disposition", true)]
    [InlineData("restocked", "batch", false)]
    [InlineData("restocked", "batch", true)]
    [InlineData("restocked", "movement", false)]
    [InlineData("restocked", "movement", true)]
    [InlineData("restocked", "audit", false)]
    [InlineData("restocked", "audit", true)]
    [InlineData("expired", "disposition", false)]
    [InlineData("expired", "disposition", true)]
    [InlineData("expired", "audit", false)]
    [InlineData("expired", "audit", true)]
    [InlineData("sold", "disposition", false)]
    [InlineData("sold", "disposition", true)]
    [InlineData("sold", "batch", false)]
    [InlineData("sold", "batch", true)]
    [InlineData("sold", "movement", false)]
    [InlineData("sold", "movement", true)]
    [InlineData("sold", "audit", false)]
    [InlineData("sold", "audit", true)]
    public void StatementFaultRollsBackThenOneRetryReconciles(string outcome, string stage, bool after)
    {
        using var f = new QuarantineAccessFixture(); var before = f.Snapshot(); var money = f.MoneySnapshot();
        f.Fault(stage, after);
        var failed = f.Q.Dispose(f.Actor, f.Return.ReturnId, outcome, "checked goods");
        Assert.False(failed.Ok); Assert.DoesNotContain("private-quarantine-fault", failed.Error!);
        Assert.Equal(before, f.Snapshot());
        f.Execute("DROP TRIGGER quarantine_fault;");
        Assert.True(f.Call(outcome, f.Actor)); f.Reconcile(outcome); Assert.Equal(money, f.MoneySnapshot());
        var committed = f.Snapshot(); Assert.False(f.Call(outcome, f.Actor)); Assert.Equal(committed, f.Snapshot());
    }

    [Theory]
    [InlineData("restocked")]
    [InlineData("expired")]
    [InlineData("sold")]
    public void DecisionsUseStoredNameAndIdForAuditAndStock(string outcome)
    {
        using var f = new QuarantineAccessFixture();
        f.Execute($"UPDATE users SET name='Current decider' WHERE id={f.Actor.Id};");
        var money = f.MoneySnapshot(); Assert.True(f.Call(outcome, f.Actor)); f.Reconcile(outcome);
        var names = f.T.Database.Read(c => f.T.Database.Query<string>(c,
            "SELECT user_name FROM audit_logs WHERE action LIKE 'quarantine_%';"));
        Assert.Equal("Current decider", Assert.Single(names));
        Assert.Equal(f.Actor.Id, f.Number("SELECT disposed_by FROM sale_returns WHERE disposition <> 'quarantine' LIMIT 1;"));
        if (outcome != QuarantineService.Expired)
            Assert.Equal(f.Actor.Id, f.Number("SELECT user_id FROM stock_movements WHERE ref_type='quarantine' LIMIT 1;"));
        Assert.Equal(money, f.MoneySnapshot());
    }

    [Theory]
    [InlineData("restocked")]
    [InlineData("expired")]
    [InlineData("sold")]
    public void ACommittedDecisionCannotBeReplacedByAnotherOutcome(string outcome)
    {
        using var f = new QuarantineAccessFixture(); Assert.True(f.Call(outcome, f.Actor)); var before = f.Snapshot();
        foreach (var next in QuarantineService.Outcomes) Assert.False(f.Call(next, f.Actor));
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void LegitimateReturnsReaderCanReadButCannotDecide()
    {
        using var f = new QuarantineAccessFixture(); var cashier = TestData.Cashier(f.T); var before = f.Snapshot();
        Assert.True(f.Call("pending", cashier)); Assert.True(f.Call("history", cashier)); Assert.True(f.Call("count", cashier));
        Assert.False(f.Q.CheckDecisionAccess(cashier).Ok);
        foreach (var outcome in QuarantineService.Outcomes) Assert.False(f.Call(outcome, cashier));
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void FailedCountIsNotCertifiedZeroAndUnreadableRowsHaveNoPayload()
    {
        using var f = new QuarantineAccessFixture();
        f.Execute("ALTER TABLE sale_returns RENAME TO hidden_returns;");
        var count = f.Q.PendingCount(f.Actor); Assert.False(count.Ok); Assert.False(string.IsNullOrWhiteSpace(count.Error));
        var pending = f.Q.Pending(f.Actor); var history = f.Q.History(f.Actor);
        Assert.False(pending.Ok); Assert.Null(pending.Value); Assert.False(history.Ok); Assert.Null(history.Value);
        f.Execute("ALTER TABLE hidden_returns RENAME TO sale_returns;");
        count = f.Q.PendingCount(f.Actor); Assert.True(count.Ok, count.Error); Assert.Equal(1, count.Value);
    }

    [Fact]
    public void MainAndFinancialGrantsDoNotReplaceReturnsPermission()
    {
        using var f = new QuarantineAccessFixture();
        f.Execute($"INSERT OR REPLACE INTO user_permissions(user_id,module_key,allowed) VALUES({f.Actor.Id},'profit',0),({f.Actor.Id},'purchasecost',0),({f.Actor.Id},'returns',1);");
        TestData.SeedRawSetting(f.T, MasterFoundationSettings.ProfitShowMargins, false);
        Assert.True(f.Call("pending", f.Actor)); Assert.True(f.Call("restocked", f.Actor)); f.Reconcile("restocked");
    }

    [Fact]
    public void ValidOwnerBypassStillRequiresStoredActiveSameRoleAccount()
    {
        using var f = new QuarantineAccessFixture(); f.Execute("ALTER TABLE user_permissions RENAME TO hidden_permissions;");
        Assert.True(f.Call("pending", TestData.Owner)); Assert.True(f.Q.CheckDecisionAccess(TestData.Owner).Ok);
        f.Execute("UPDATE users SET active=0 WHERE id=1;"); var before = f.Snapshot();
        Assert.False(f.Call("pending", TestData.Owner)); Assert.False(f.Call("restocked", TestData.Owner)); Assert.Equal(before, f.Snapshot());
        f.Execute("UPDATE users SET active=1, role='manager' WHERE id=1;"); before = f.Snapshot();
        Assert.False(f.Call("history", TestData.Owner)); Assert.False(f.Call("expired", TestData.Owner)); Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void MissingTargetInvalidOutcomeAndMissingDestroyNoteCannotMutate()
    {
        using var f = new QuarantineAccessFixture(); var before = f.Snapshot();
        Assert.False(f.Q.Dispose(f.Actor, int.MaxValue, "restocked", "checked").Ok);
        Assert.False(f.Q.Dispose(f.Actor, f.Return.ReturnId, "unknown", "checked").Ok);
        Assert.False(f.Q.Dispose(f.Actor, f.Return.ReturnId, "expired", " ").Ok);
        Assert.Equal(before, f.Snapshot());
    }
}
