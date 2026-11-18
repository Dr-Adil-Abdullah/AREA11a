using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>Isolated real-schema regressions; written here, execution needs the .NET SDK.</summary>
public sealed class TransactionPermissionTests
{
    private sealed class Probe { public long Value { get; set; } }
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static long Number(TestDatabase t, string sql) => t.Database.Read(c => Convert.ToInt64(t.Database.Scalar(c, sql)));
    private static void PendingGrant(TestDatabase t, Microsoft.Data.Sqlite.SqliteConnection c, User u, string module, bool allow) =>
        t.Database.Execute(c, "INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(@id,@module,@allow) ON CONFLICT(user_id,module_key) DO UPDATE SET allowed=excluded.allowed;",
            new { id = u.Id, module, allow = allow ? 1 : 0 });

    [Fact]
    public void ExecuteDoesNotLendTransactionToForeignConnection()
    {
        using var t = new TestDatabase(); using var other = t.Database.Open();
        t.Database.Tx(c => { t.Database.Execute(other, "SELECT 17;"); });
    }

    [Fact]
    public void QueryDoesNotLendTransactionToForeignConnection()
    {
        using var t = new TestDatabase(); using var other = t.Database.Open();
        t.Database.Tx(c => { Assert.Equal(17L, Assert.Single(t.Database.Query<Probe>(other, "SELECT 17 AS Value;")).Value); });
    }

    [Fact]
    public void QuerySingleDoesNotLendTransactionToForeignConnection()
    {
        using var t = new TestDatabase(); using var other = t.Database.Open();
        t.Database.Tx(c => { Assert.Equal(17L, t.Database.QuerySingle<Probe>(other, "SELECT 17 AS Value;")!.Value); });
    }

    [Fact]
    public void ScalarDoesNotLendTransactionToForeignConnection()
    {
        using var t = new TestDatabase(); using var other = t.Database.Open();
        t.Database.Tx(c => { Assert.Equal(17L, Convert.ToInt64(t.Database.Scalar(other, "SELECT 17;"))); });
    }

    [Fact]
    public void NextIdDoesNotLendTransactionToForeignConnection()
    {
        using var t = new TestDatabase(); using var other = t.Database.Open();
        t.Database.Tx(c => { Assert.Equal(0L, t.Database.NextId(other)); });
    }

    [Fact]
    public void ReadBorrowsCurrentConnectionWithoutDisposingIt()
    {
        using var t = new TestDatabase();
        t.Database.Tx(c =>
        {
            Assert.Equal(17L, t.Database.Read(borrowed =>
            {
                Assert.Same(c, borrowed); return Convert.ToInt64(t.Database.Scalar(borrowed, "SELECT 17;"));
            }));
            Assert.Equal(ConnectionState.Open, c.State);
            t.Database.Execute(c, "INSERT INTO companies(name,active) VALUES('Pending read probe',1);");
            Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM companies WHERE name='Pending read probe';"));
        });
    }

    [Fact]
    public void OutsideReadOwnsAndDisposesItsConnection()
    {
        using var t = new TestDatabase(); Microsoft.Data.Sqlite.SqliteConnection? observed = null;
        // Capture only to verify disposal in this regression; production readers must not retain it.
        Assert.Equal(17L, t.Database.Read(c => { observed = c; return Convert.ToInt64(t.Database.Scalar(c, "SELECT 17;")); }));
        Assert.NotNull(observed); Assert.Equal(ConnectionState.Closed, observed!.State);
    }

    [Fact]
    public void ReadFailureDoesNotDisposeBorrowedTransaction()
    {
        using var t = new TestDatabase();
        t.Database.Tx(c =>
        {
            Assert.Throws<InvalidOperationException>(() => t.Database.Read<int>(_ => throw new InvalidOperationException("synthetic read fault")));
            Assert.Equal(17L, Convert.ToInt64(t.Database.Scalar(c, "SELECT 17;")));
        });
    }

    [Fact]
    public void SameConnectionCommandsStillRollBackTogetherAndNextTxWorks()
    {
        using var t = new TestDatabase();
        Assert.Throws<InvalidOperationException>(() => t.Database.Tx(c =>
        {
            t.Database.Execute(c, "INSERT INTO companies(name,active) VALUES('Rollback probe',1);");
            Assert.True(t.Database.NextId(c) > 0);
            Assert.Equal(1L, t.Database.QuerySingle<long>(c, "SELECT COUNT(*) FROM companies WHERE name='Rollback probe';"));
            Assert.Equal(1L, Assert.Single(t.Database.Query<Probe>(c, "SELECT COUNT(*) AS Value FROM companies WHERE name='Rollback probe';")).Value);
            Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM companies WHERE name='Rollback probe';"));
            throw new InvalidOperationException("synthetic write fault");
        }));
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM companies WHERE name='Rollback probe';"));
        Execute(t, "INSERT INTO companies(name,active) VALUES('After rollback',1);");
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM companies WHERE name='After rollback';"));
    }

    [Fact]
    public void NestedTxStillRejectedWithoutLosingOuterTransaction()
    {
        using var t = new TestDatabase();
        t.Database.Tx(c =>
        {
            Assert.Throws<InvalidOperationException>(() => t.Database.Tx(inner => { t.Database.Execute(inner, "SELECT 1;"); }));
            t.Database.Execute(c, "INSERT INTO companies(name,active) VALUES('Outer survives',1);");
        });
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM companies WHERE name='Outer survives';"));
    }

    [Fact]
    public void OtherThreadReadWaitsUntilCommitAndDoesNotBorrowTheWriterConnection()
    {
        using var t = new TestDatabase(); using var started = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim(); Task? worker = null;
        try
        {
            t.Database.Tx(c =>
            {
                t.Database.Execute(c, "INSERT INTO companies(name,active) VALUES('Serialized read probe',1);");
                worker = Task.Factory.StartNew(() =>
                {
                    started.Set();
                    t.Database.Read(read =>
                    {
                        entered.Set(); Assert.NotSame(c, read);
                        Assert.Equal(1L, Convert.ToInt64(t.Database.Scalar(read, "SELECT COUNT(*) FROM companies WHERE name='Serialized read probe';")));
                        return 0;
                    });
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
                Assert.False(entered.Wait(TimeSpan.FromMilliseconds(100)));
            });
        }
        finally { if (worker is not null) Assert.True(worker.Wait(TimeSpan.FromSeconds(15))); }
        Assert.True(entered.IsSet);
    }

    [Fact]
    public void OtherThreadTxWaitsInsteadOfReceivingNestedTxError()
    {
        using var t = new TestDatabase(); using var started = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim(); Task? worker = null;
        try
        {
            t.Database.Tx(c =>
            {
                worker = Task.Factory.StartNew(() =>
                {
                    started.Set();
                    t.Database.Tx(other =>
                    {
                        entered.Set(); Assert.NotSame(c, other);
                        t.Database.Execute(other, "INSERT INTO companies(name,active) VALUES('Serialized Tx probe',1);");
                    });
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
                Assert.False(entered.Wait(TimeSpan.FromMilliseconds(100)));
            });
        }
        finally { if (worker is not null) Assert.True(worker.Wait(TimeSpan.FromSeconds(15))); }
        Assert.Equal(1L, Number(t, "SELECT COUNT(*) FROM companies WHERE name='Serialized Tx probe';"));
    }

    [Fact]
    public void CommittedDelegatedCashIsAllowedInsideTx()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        Assert.True(permissions.Set(TestData.Owner, u.Id, Modules.Cash, true).Ok);
        t.Database.Tx(c => { Assert.True(permissions.Can(u, Modules.Cash)); });
    }

    [Fact]
    public void CommittedRevokedCustomersCannotRegainCashierDefaultInsideTx()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        Assert.True(permissions.Set(TestData.Owner, u.Id, Modules.Customers, false).Ok);
        t.Database.Tx(c => { Assert.False(permissions.Can(u, Modules.Customers)); });
    }

    [Fact]
    public void PendingDelegatedGrantIsVisibleInsideTx()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        Assert.False(permissions.Can(u, Modules.Cash));
        t.Database.Tx(c => { PendingGrant(t, c, u, Modules.Cash, true); Assert.True(permissions.Can(u, Modules.Cash)); });
        Assert.True(permissions.Can(u, Modules.Cash));
    }

    [Fact]
    public void PendingRevocationIsVisibleAndRollbackRestoresRoleDefault()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        Assert.True(permissions.Can(u, Modules.Customers));
        Assert.Throws<InvalidOperationException>(() => t.Database.Tx(c =>
        {
            PendingGrant(t, c, u, Modules.Customers, false); Assert.False(permissions.Can(u, Modules.Customers));
            throw new InvalidOperationException("synthetic revoke rollback");
        }));
        Assert.True(permissions.Can(u, Modules.Customers));
    }

    [Fact]
    public void RolledBackDelegationDoesNotLeakThroughPermissionCache()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        Assert.Throws<InvalidOperationException>(() => t.Database.Tx(c =>
        {
            PendingGrant(t, c, u, Modules.Cash, true); Assert.True(permissions.Can(u, Modules.Cash));
            throw new InvalidOperationException("synthetic grant rollback");
        }));
        Assert.False(permissions.Can(u, Modules.Cash));
    }

    [Fact]
    public void MissingGrantKeepsRoleDefaultWhenReadSucceeds()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        t.Database.Tx(c =>
        {
            t.Database.Execute(c, "DELETE FROM user_permissions WHERE user_id=@id;", new { id = u.Id });
            Assert.True(permissions.Can(u, Modules.Customers)); Assert.False(permissions.Can(u, Modules.Cash));
        });
    }

    [Fact]
    public void OrdinaryPermissionReadFailureIsDeniedOutsideTx()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        Execute(t, "DROP TABLE user_permissions;");
        Assert.False(permissions.Can(u, Modules.Customers));
    }

    [Fact]
    public void OrdinaryPermissionReadFailureIsDeniedInsideTx()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        t.Database.Tx(c => { t.Database.Execute(c, "DROP TABLE user_permissions;"); Assert.False(permissions.Can(u, Modules.Customers)); });
    }

    [Fact]
    public void OwnerOrdinaryBypassAndExplicitFinancialRevocationRemainSeparate()
    {
        using var t = new TestDatabase(); var permissions = TestData.Permissions(t);
        t.Database.Tx(c =>
        {
            PendingGrant(t, c, TestData.Owner, Modules.Cash, false);
            PendingGrant(t, c, TestData.Owner, Modules.Profit, false);
            Assert.True(permissions.Can(TestData.Owner, Modules.Cash)); Assert.False(permissions.Can(TestData.Owner, Modules.Profit));
        });
    }

    [Fact]
    public void BothFinancialGrantsReadPendingChangesIndependently()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        t.Database.Tx(c =>
        {
            PendingGrant(t, c, u, Modules.Profit, true); PendingGrant(t, c, u, Modules.PurchaseCost, false);
            Assert.True(permissions.Can(u, Modules.Profit)); Assert.False(permissions.Can(u, Modules.PurchaseCost));
            PendingGrant(t, c, u, Modules.Profit, false); PendingGrant(t, c, u, Modules.PurchaseCost, true);
            Assert.False(permissions.Can(u, Modules.Profit)); Assert.True(permissions.Can(u, Modules.PurchaseCost));
        });
    }

    [Fact]
    public void FinancialGrantReadsCurrentPendingAccountState()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        t.Database.Tx(c =>
        {
            PendingGrant(t, c, u, Modules.Profit, true); Assert.True(permissions.Can(u, Modules.Profit));
            t.Database.Execute(c, "UPDATE users SET active=0 WHERE id=@id;", new { id = u.Id });
            Assert.False(permissions.Can(u, Modules.Profit));
        });
    }

    [Fact]
    public void FinancialReadFailureStillFailsClosedForOwner()
    {
        using var t = new TestDatabase(); var permissions = TestData.Permissions(t);
        t.Database.Tx(c =>
        {
            t.Database.Execute(c, "DROP TABLE user_permissions;");
            Assert.False(permissions.Can(TestData.Owner, Modules.Profit)); Assert.False(permissions.Can(TestData.Owner, Modules.PurchaseCost));
        });
    }

    [Fact]
    public void AssignmentMapReadsPendingAssignmentsWithoutBecomingAuthorizationApi()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var permissions = TestData.Permissions(t);
        t.Database.Tx(c =>
        {
            PendingGrant(t, c, u, Modules.Cash, true); PendingGrant(t, c, u, Modules.Customers, false);
            var map = permissions.MapFor(u); Assert.True(map[Modules.Cash]); Assert.False(map[Modules.Customers]);
        });
    }

    [Fact]
    public void DelegatedCustomerCollectionStillPostsExactlyOnePayment()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var customer = TestData.AddCustomer(t);
        Execute(t, "UPDATE customers SET opening_balance_paisa=10000,balance_paisa=10000 WHERE id=" + customer);
        var before = Number(t, "SELECT COUNT(*) FROM payments;");
        var result = TestData.Ledger(t).Collect(u, customer, 1000, "cash", "Synthetic transaction boundary");
        Assert.True(result.Ok, result.Error);
        Assert.Equal(before + 1, Number(t, "SELECT COUNT(*) FROM payments;"));
        Assert.Equal(9000L, Number(t, "SELECT balance_paisa FROM customers WHERE id=" + customer));
    }

    [Fact]
    public void RevokedCustomerCollectionDoesNotPostOrAudit()
    {
        using var t = new TestDatabase(); var u = TestData.Cashier(t); var customer = TestData.AddCustomer(t);
        Execute(t, "UPDATE customers SET opening_balance_paisa=10000,balance_paisa=10000 WHERE id=" + customer);
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, u.Id, Modules.Customers, false).Ok);
        var payments = Number(t, "SELECT COUNT(*) FROM payments;"); var audits = Number(t, "SELECT COUNT(*) FROM audit_logs;");
        Assert.False(TestData.Ledger(t).Collect(u, customer, 1000, "cash", "Synthetic denied collection").Ok);
        Assert.Equal(payments, Number(t, "SELECT COUNT(*) FROM payments;")); Assert.Equal(audits, Number(t, "SELECT COUNT(*) FROM audit_logs;"));
        Assert.Equal(10000L, Number(t, "SELECT balance_paisa FROM customers WHERE id=" + customer));
    }
}
