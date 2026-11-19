using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Current local actor/module regressions; source only, not executed here or session proof.</summary>
public sealed class CounterCurrentAccessTests
{
    private sealed class CurrentCounterFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public User Actor { get; }
        public int Product { get; }
        public SaleService Sales { get; }
        public ReturnService Returns { get; }
        public SaleResult Original { get; }
        public CurrentCounterFixture()
        {
            Actor = TestData.AddUser(T, "manager");
            Product = TestData.AddProduct(T);
            Sales = TestData.Sales(T); Returns = TestData.Returns(T);
            var saved = Sales.Create(Input(), TestData.Owner); Assert.True(saved.Ok, saved.Error); Original = saved.Value!;
        }
        public SaleInput Input() => TestData.Cart(Product, qty: 1);
        public ReturnInput Back() => TestData.Return(Original, 1);
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public string Snapshot() => T.Database.Read(c => string.Join("\n", new[]
        {
            "settings", "customers", "batches", "sales", "sale_items", "sale_returns", "stock_movements",
            "payments", "audit_logs", "custom_values", "document_drafts", "sqlite_sequence"
        }.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public bool Call(string route, User? actor) => route switch
        {
            "warning" => Verdict(Sales.CounterWarnings(Input(), actor)),
            "sale-quote" => Verdict(Sales.Quote(Input(), actor!)),
            "sale-post" => Verdict(Sales.Create(Input(), actor!)),
            "find" => Verdict(Returns.FindSale(Original.Code, actor)),
            "search" => Verdict(Returns.Search(new SaleLookupInput { Text = Original.Code }, actor)),
            "lines" => Verdict(Returns.Lines(Original.SaleId, actor)),
            "return-quote" => Verdict(Returns.Quote(Back(), actor!)),
            "return-post" => Verdict(Returns.Create(Back(), actor!)),
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
                case "inactive": Execute("UPDATE users SET active=" + (restore ? "1" : "0") + " WHERE id=" + Actor.Id); break;
                case "role": Execute($"UPDATE users SET role='{(restore ? "manager" : "cashier")}' WHERE id={Actor.Id};"); break;
                case "revoke":
                    Execute("INSERT OR REPLACE INTO user_permissions(user_id,module_key,allowed) VALUES(" + Actor.Id + ",'pos'," + (restore ? "1" : "0") + ");");
                    Execute("INSERT OR REPLACE INTO user_permissions(user_id,module_key,allowed) VALUES(" + Actor.Id + ",'returns'," + (restore ? "1" : "0") + ");"); break;
                case "grant-read": Execute(restore ? "ALTER TABLE hidden_permissions RENAME TO user_permissions;" : "ALTER TABLE user_permissions RENAME TO hidden_permissions;"); break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
        }
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData("warning", "inactive")]
    [InlineData("warning", "role")]
    [InlineData("warning", "revoke")]
    [InlineData("warning", "grant-read")]
    [InlineData("sale-quote", "inactive")]
    [InlineData("sale-quote", "role")]
    [InlineData("sale-quote", "revoke")]
    [InlineData("sale-quote", "grant-read")]
    [InlineData("sale-post", "inactive")]
    [InlineData("sale-post", "role")]
    [InlineData("sale-post", "revoke")]
    [InlineData("sale-post", "grant-read")]
    [InlineData("find", "inactive")]
    [InlineData("find", "role")]
    [InlineData("find", "revoke")]
    [InlineData("find", "grant-read")]
    [InlineData("search", "inactive")]
    [InlineData("search", "role")]
    [InlineData("search", "revoke")]
    [InlineData("search", "grant-read")]
    [InlineData("lines", "inactive")]
    [InlineData("lines", "role")]
    [InlineData("lines", "revoke")]
    [InlineData("lines", "grant-read")]
    [InlineData("return-quote", "inactive")]
    [InlineData("return-quote", "role")]
    [InlineData("return-quote", "revoke")]
    [InlineData("return-quote", "grant-read")]
    [InlineData("return-post", "inactive")]
    [InlineData("return-post", "role")]
    [InlineData("return-post", "revoke")]
    [InlineData("return-post", "grant-read")]
    public void CurrentChangeDeniesNextReadOrPostWithoutMutationAndRegrantUsesSameServices(string route, string fault)
    {
        using var f = new CurrentCounterFixture(); f.Change(fault); var before = f.Snapshot();
        Assert.False(f.Call(route, f.Actor)); Assert.Equal(before, f.Snapshot());
        Assert.False(f.Sales.CheckPostingAccess(f.Actor).Ok); Assert.False(f.Returns.CheckPostingAccess(f.Actor).Ok);
        f.Change(fault, restore: true); Assert.True(f.Call(route, f.Actor));
    }

    [Theory]
    [InlineData("warning", "null")]
    [InlineData("warning", "zero")]
    [InlineData("warning", "inactive-object")]
    [InlineData("warning", "forged-owner")]
    [InlineData("sale-quote", "null")]
    [InlineData("sale-quote", "zero")]
    [InlineData("sale-quote", "inactive-object")]
    [InlineData("sale-quote", "forged-owner")]
    [InlineData("sale-post", "null")]
    [InlineData("sale-post", "zero")]
    [InlineData("sale-post", "inactive-object")]
    [InlineData("sale-post", "forged-owner")]
    [InlineData("find", "null")]
    [InlineData("find", "zero")]
    [InlineData("find", "inactive-object")]
    [InlineData("find", "forged-owner")]
    [InlineData("search", "null")]
    [InlineData("search", "zero")]
    [InlineData("search", "inactive-object")]
    [InlineData("search", "forged-owner")]
    [InlineData("lines", "null")]
    [InlineData("lines", "zero")]
    [InlineData("lines", "inactive-object")]
    [InlineData("lines", "forged-owner")]
    [InlineData("return-quote", "null")]
    [InlineData("return-quote", "zero")]
    [InlineData("return-quote", "inactive-object")]
    [InlineData("return-quote", "forged-owner")]
    [InlineData("return-post", "null")]
    [InlineData("return-post", "zero")]
    [InlineData("return-post", "inactive-object")]
    [InlineData("return-post", "forged-owner")]
    public void InvalidSuppliedActorCannotReadOrPostEvenWithDefaultAllowedRole(string route, string invalid)
    {
        using var f = new CurrentCounterFixture();
        User? actor = invalid switch
        {
            "null" => null,
            "zero" => new User { Id = 0, Role = "owner" },
            "inactive-object" => new User { Id = f.Actor.Id, Role = "manager", Active = false },
            "forged-owner" => new User { Id = f.Actor.Id, Role = "owner" },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
        var before = f.Snapshot(); Assert.False(f.Call(route, actor)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData("inactive")] [InlineData("role")] [InlineData("revoke")]
    public void DeniedSaleDraftPostCannotPremarkOrConsumeDraft(string fault)
    {
        using var f = new CurrentCounterFixture(); var drafts = TestData.Drafts(f.T);
        var draft = drafts.SaveSaleDraft(f.Actor, f.Input()); Assert.True(draft.Ok, draft.Error);
        f.Change(fault); var before = f.Snapshot();
        Assert.False(drafts.PostSale(f.Actor, draft.Value!.Id, f.Input()).Ok); Assert.Equal(before, f.Snapshot());
        f.Change(fault, restore: true); Assert.True(drafts.PostSale(f.Actor, draft.Value!.Id, f.Input()).Ok);
    }

    [Fact]
    public void SuccessfulPostsUseStoredCurrentNameNotCallerAuthoredName()
    {
        using var f = new CurrentCounterFixture(); f.Execute("UPDATE users SET name='Current stored actor' WHERE id=" + f.Actor.Id);
        f.Actor.Name = "Caller stale alias";
        Assert.True(f.Sales.Create(f.Input(), f.Actor).Ok); Assert.True(f.Returns.Create(f.Back(), f.Actor).Ok);
        var names = f.T.Database.Read(c => f.T.Database.Query<string>(c,
            "SELECT user_name FROM audit_logs WHERE action IN ('sale_create','return_create') AND user_id=@id;", new { id = f.Actor.Id }));
        Assert.Equal(2, names.Count); Assert.All(names, name => Assert.Equal("Current stored actor", name));
    }

    [Fact]
    public void IndependentFinancialGrantsAndMainOffDoNotDisableOrdinarySaleOrRefund()
    {
        using var f = new CurrentCounterFixture();
        TestData.SeedRawSetting(f.T, MasterFoundationSettings.ProfitShowMargins, false);
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner, f.Actor.Id, Modules.PurchaseCost, false).Ok);
        Assert.True(TestData.Permissions(f.T).Set(TestData.Owner, f.Actor.Id, Modules.Profit, false).Ok);
        var quote = f.Sales.Quote(f.Input(), f.Actor); Assert.True(quote.Ok, quote.Error);
        Assert.All(quote.Value!.Lines, line => { Assert.False(line.CostKnown); Assert.False(line.ProfitKnown); Assert.Equal(0L, line.CostPaisa); Assert.Equal(0L, line.ProfitPaisa); });
        Assert.True(f.Sales.Create(f.Input(), f.Actor).Ok); Assert.True(f.Returns.Create(f.Back(), f.Actor).Ok);
    }

    [Fact]
    public void CounterAndReturnsModuleGrantsRemainIndependent()
    {
        using var f = new CurrentCounterFixture(); var permissions = TestData.Permissions(f.T);
        Assert.True(permissions.Set(TestData.Owner, f.Actor.Id, Modules.Pos, false).Ok);
        Assert.False(f.Sales.Quote(f.Input(), f.Actor).Ok); Assert.True(f.Returns.Lines(f.Original.SaleId, f.Actor).Ok);
        Assert.True(permissions.Set(TestData.Owner, f.Actor.Id, Modules.Pos, true).Ok);
        Assert.True(permissions.Set(TestData.Owner, f.Actor.Id, Modules.Returns, false).Ok);
        Assert.True(f.Sales.Quote(f.Input(), f.Actor).Ok); Assert.False(f.Returns.Lines(f.Original.SaleId, f.Actor).Ok);
    }

    [Fact]
    public void DenialBeforeSaveDoesNotConsumeRetailDiscountConfirmation()
    {
        using var f = new CurrentCounterFixture(); var input = f.Input(); input.RetailDiscountConfirmed = true;
        f.Change("inactive"); var before = f.Snapshot(); Assert.False(f.Sales.Create(input, f.Actor).Ok);
        Assert.True(input.RetailDiscountConfirmed); Assert.Equal(before, f.Snapshot());
    }
    [Theory]
    [InlineData("inactive")] [InlineData("role")]
    public void StoredOwnerAccountChangeCannotUseOrdinaryOwnerBypass(string fault)
    {
        using var f = new CurrentCounterFixture();
        f.Execute(fault == "inactive" ? "UPDATE users SET active=0 WHERE id=1;" : "UPDATE users SET role='manager' WHERE id=1;");
        var before = f.Snapshot();
        foreach (var route in new[] { "warning", "sale-quote", "sale-post", "find", "search", "lines", "return-quote", "return-post" })
            Assert.False(f.Call(route, TestData.Owner));
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void ValidCurrentOwnerKeepsOrdinaryModuleBypassWhenGrantTableIsUnreadable()
    {
        using var f = new CurrentCounterFixture(); f.Change("grant-read");
        Assert.True(f.Sales.CheckPostingAccess(TestData.Owner).Ok); Assert.True(f.Returns.CheckPostingAccess(TestData.Owner).Ok);
        Assert.False(f.Sales.CheckPostingAccess(f.Actor).Ok); Assert.False(f.Returns.CheckPostingAccess(f.Actor).Ok);
    }

}
