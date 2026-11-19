using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Current draft/held boundaries; source regressions, not authenticated session/provider evidence.</summary>
public sealed class DraftHeldCurrentAccessTests
{
    private sealed class DraftHeldAccessFixture : IDisposable
    {
        public TestDatabase T { get; } = new();
        public User Actor { get; }
        public DraftService D { get; }
        public HoldCartService H { get; }
        public SaleInput Sale { get; }
        public PurchaseInput Purchase { get; }
        public int SaleId { get; }
        public int PurchaseId { get; }
        public int HoldId { get; }
        public DraftHeldAccessFixture()
        {
            Actor = TestData.AddUser(T, "manager");
            TestData.SeedRawSetting(T, MasterFoundationSettings.ProfitShowMargins, true);
            Grant("purchasecost", true);
            var product = TestData.AddProduct(T); Sale = TestData.Cart(product, qty: 1);
            Purchase = new PurchaseInput { SupplierId = TestData.AddSupplier(T), Lines = new List<PurchaseLineInput>
            {
                new() { ProductId = product, Unit = "base", Quantity = 1, CostPaisa = 20000, RetailPaisa = 30000, BatchNo = "SYNTHETIC", Expiry = "2099-06" }
            }};
            D = TestData.Drafts(T); H = TestData.Holds(T);
            var sale = D.SaveSaleDraft(TestData.Owner, Sale); Assert.True(sale.Ok, sale.Error); SaleId = sale.Value!.Id;
            var purchase = D.SavePurchaseDraft(TestData.Owner, Purchase); Assert.True(purchase.Ok, purchase.Error); PurchaseId = purchase.Value!.Id;
            var held = H.Hold(Sale, TestData.Owner); Assert.True(held.Ok, held.Error);
            var list = H.List(TestData.Owner); Assert.True(list.Ok, list.Error); HoldId = Assert.Single(list.Value!).Id;
        }
        public void Execute(string sql) => T.Database.Tx(c => { T.Database.Execute(c, sql); });
        public void Grant(string module, bool allow) => Execute($"INSERT OR REPLACE INTO user_permissions(user_id,module_key,allowed) VALUES({Actor.Id},'{module}',{(allow ? 1 : 0)});");
        public string Tables(params string[] tables) => T.Database.Read(c => string.Join("\n", tables.Select(table =>
        {
            var columns = T.Database.Query<string>(c, "SELECT name FROM pragma_table_info(@table);", new { table });
            var projection = string.Join("||':'||", columns.Select(column => "quote(\"" + column + "\")"));
            return table + "=" + string.Join(";", T.Database.Query<string>(c, $"SELECT {projection} FROM {table} ORDER BY 1;"));
        })));
        public string Snapshot() => Tables("settings", "customers", "suppliers", "products", "batches", "sales", "sale_items", "purchases", "purchase_items", "payments", "stock_movements", "document_drafts", "held_carts", "held_cart_items", "audit_logs", "sqlite_sequence");
        public string MoneySnapshot() => Tables("customers", "suppliers", "batches", "sales", "sale_items", "purchases", "purchase_items", "payments", "stock_movements");
        public bool Call(string route, User? actor) => route switch
        {
            "sale-save" => Verdict(D.SaveSaleDraft(actor, Sale)), "sale-update" => Verdict(D.SaveSaleDraft(actor, Sale, SaleId)),
            "sale-list" => Verdict(D.List(actor, DraftType.Sale)), "sale-open" => Verdict(D.OpenSale(actor, SaleId)), "sale-discard" => D.Discard(actor, SaleId).Ok,
            "purchase-save" => Verdict(D.SavePurchaseDraft(actor, Purchase)), "purchase-update" => Verdict(D.SavePurchaseDraft(actor, Purchase, PurchaseId)),
            "purchase-list" => Verdict(D.List(actor, DraftType.Purchase)), "purchase-open" => Verdict(D.OpenPurchase(actor, PurchaseId)), "purchase-discard" => D.Discard(actor, PurchaseId).Ok,
            "hold" => Verdict(H.Hold(Sale, actor)), "hold-list" => Verdict(H.List(actor)), "hold-count" => Verdict(H.Count(actor)),
            "hold-resume" => Verdict(H.Resume(actor, HoldId)), "hold-discard" => H.Discard(actor, HoldId).Ok,
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };
        private static bool Verdict<T>(ServiceResult<T> result)
        {
            if (!result.Ok) { Assert.Equal(default(T), result.Value); Assert.False(string.IsNullOrWhiteSpace(result.Error)); }
            return result.Ok;
        }
        public void Change(string fault, bool restore = false)
        {
            switch (fault)
            {
                case "inactive": Execute($"UPDATE users SET active={(restore ? 1 : 0)} WHERE id={Actor.Id};"); break;
                case "role": Execute($"UPDATE users SET role='{(restore ? "manager" : "cashier")}' WHERE id={Actor.Id};"); break;
                case "revoke": Grant("pos", restore); Grant("purchases", restore); break;
                case "grant-read": Execute(restore ? "ALTER TABLE hidden_permissions RENAME TO user_permissions;" : "ALTER TABLE user_permissions RENAME TO hidden_permissions;"); break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
        }
        public void Dispose() => T.Dispose();
    }

    [Theory]
    [InlineData("sale-save", "inactive")]
    [InlineData("sale-save", "role")]
    [InlineData("sale-save", "revoke")]
    [InlineData("sale-save", "grant-read")]
    [InlineData("sale-update", "inactive")]
    [InlineData("sale-update", "role")]
    [InlineData("sale-update", "revoke")]
    [InlineData("sale-update", "grant-read")]
    [InlineData("sale-list", "inactive")]
    [InlineData("sale-list", "role")]
    [InlineData("sale-list", "revoke")]
    [InlineData("sale-list", "grant-read")]
    [InlineData("sale-open", "inactive")]
    [InlineData("sale-open", "role")]
    [InlineData("sale-open", "revoke")]
    [InlineData("sale-open", "grant-read")]
    [InlineData("sale-discard", "inactive")]
    [InlineData("sale-discard", "role")]
    [InlineData("sale-discard", "revoke")]
    [InlineData("sale-discard", "grant-read")]
    [InlineData("purchase-save", "inactive")]
    [InlineData("purchase-save", "role")]
    [InlineData("purchase-save", "revoke")]
    [InlineData("purchase-save", "grant-read")]
    [InlineData("purchase-update", "inactive")]
    [InlineData("purchase-update", "role")]
    [InlineData("purchase-update", "revoke")]
    [InlineData("purchase-update", "grant-read")]
    [InlineData("purchase-list", "inactive")]
    [InlineData("purchase-list", "role")]
    [InlineData("purchase-list", "revoke")]
    [InlineData("purchase-list", "grant-read")]
    [InlineData("purchase-open", "inactive")]
    [InlineData("purchase-open", "role")]
    [InlineData("purchase-open", "revoke")]
    [InlineData("purchase-open", "grant-read")]
    [InlineData("purchase-discard", "inactive")]
    [InlineData("purchase-discard", "role")]
    [InlineData("purchase-discard", "revoke")]
    [InlineData("purchase-discard", "grant-read")]
    [InlineData("hold", "inactive")]
    [InlineData("hold", "role")]
    [InlineData("hold", "revoke")]
    [InlineData("hold", "grant-read")]
    [InlineData("hold-list", "inactive")]
    [InlineData("hold-list", "role")]
    [InlineData("hold-list", "revoke")]
    [InlineData("hold-list", "grant-read")]
    [InlineData("hold-count", "inactive")]
    [InlineData("hold-count", "role")]
    [InlineData("hold-count", "revoke")]
    [InlineData("hold-count", "grant-read")]
    [InlineData("hold-resume", "inactive")]
    [InlineData("hold-resume", "role")]
    [InlineData("hold-resume", "revoke")]
    [InlineData("hold-resume", "grant-read")]
    [InlineData("hold-discard", "inactive")]
    [InlineData("hold-discard", "role")]
    [InlineData("hold-discard", "revoke")]
    [InlineData("hold-discard", "grant-read")]
    public void CurrentChangeDeniesReadOrMutationAndSameServiceRegrants(string route, string fault)
    {
        using var f = new DraftHeldAccessFixture(); f.Change(fault); var before = f.Snapshot();
        Assert.False(f.Call(route, f.Actor)); Assert.Equal(before, f.Snapshot());
        f.Change(fault, restore: true); var money = f.MoneySnapshot();
        Assert.True(f.Call(route, f.Actor)); Assert.Equal(money, f.MoneySnapshot());
    }

    [Theory]
    [InlineData("sale-save", "null")]
    [InlineData("sale-save", "zero")]
    [InlineData("sale-save", "inactive")]
    [InlineData("sale-save", "forged-owner")]
    [InlineData("sale-update", "null")]
    [InlineData("sale-update", "zero")]
    [InlineData("sale-update", "inactive")]
    [InlineData("sale-update", "forged-owner")]
    [InlineData("sale-list", "null")]
    [InlineData("sale-list", "zero")]
    [InlineData("sale-list", "inactive")]
    [InlineData("sale-list", "forged-owner")]
    [InlineData("sale-open", "null")]
    [InlineData("sale-open", "zero")]
    [InlineData("sale-open", "inactive")]
    [InlineData("sale-open", "forged-owner")]
    [InlineData("sale-discard", "null")]
    [InlineData("sale-discard", "zero")]
    [InlineData("sale-discard", "inactive")]
    [InlineData("sale-discard", "forged-owner")]
    [InlineData("purchase-save", "null")]
    [InlineData("purchase-save", "zero")]
    [InlineData("purchase-save", "inactive")]
    [InlineData("purchase-save", "forged-owner")]
    [InlineData("purchase-update", "null")]
    [InlineData("purchase-update", "zero")]
    [InlineData("purchase-update", "inactive")]
    [InlineData("purchase-update", "forged-owner")]
    [InlineData("purchase-list", "null")]
    [InlineData("purchase-list", "zero")]
    [InlineData("purchase-list", "inactive")]
    [InlineData("purchase-list", "forged-owner")]
    [InlineData("purchase-open", "null")]
    [InlineData("purchase-open", "zero")]
    [InlineData("purchase-open", "inactive")]
    [InlineData("purchase-open", "forged-owner")]
    [InlineData("purchase-discard", "null")]
    [InlineData("purchase-discard", "zero")]
    [InlineData("purchase-discard", "inactive")]
    [InlineData("purchase-discard", "forged-owner")]
    [InlineData("hold", "null")]
    [InlineData("hold", "zero")]
    [InlineData("hold", "inactive")]
    [InlineData("hold", "forged-owner")]
    [InlineData("hold-list", "null")]
    [InlineData("hold-list", "zero")]
    [InlineData("hold-list", "inactive")]
    [InlineData("hold-list", "forged-owner")]
    [InlineData("hold-count", "null")]
    [InlineData("hold-count", "zero")]
    [InlineData("hold-count", "inactive")]
    [InlineData("hold-count", "forged-owner")]
    [InlineData("hold-resume", "null")]
    [InlineData("hold-resume", "zero")]
    [InlineData("hold-resume", "inactive")]
    [InlineData("hold-resume", "forged-owner")]
    [InlineData("hold-discard", "null")]
    [InlineData("hold-discard", "zero")]
    [InlineData("hold-discard", "inactive")]
    [InlineData("hold-discard", "forged-owner")]
    public void InvalidActorCannotUseKnownDraftOrHeldIds(string route, string kind)
    {
        using var f = new DraftHeldAccessFixture();
        User? actor = kind switch
        {
            "null" => null, "zero" => new User { Id = 0, Name = "Unknown", Role = "owner" },
            "inactive" => new User { Id = f.Actor.Id, Name = "Old", Role = "manager", Active = false },
            _ => new User { Id = f.Actor.Id, Name = "Forged", Role = "owner" }
        };
        var before = f.Snapshot(); Assert.False(f.Call(route, actor)); Assert.Equal(before, f.Snapshot());
    }

    [Theory]
    [InlineData("sale-save", "draft_saved")]
    [InlineData("sale-update", "draft_updated")]
    [InlineData("sale-discard", "draft_discarded")]
    [InlineData("purchase-save", "draft_saved")]
    [InlineData("purchase-update", "draft_updated")]
    [InlineData("purchase-discard", "draft_discarded")]
    [InlineData("hold", "cart_hold")]
    [InlineData("hold-resume", "cart_resume")]
    [InlineData("hold-discard", "cart_discard")]
    public void WritersUseStoredActorNameNotCallerText(string route, string action)
    {
        using var f = new DraftHeldAccessFixture(); f.Execute($"UPDATE users SET name='Current author' WHERE id={f.Actor.Id};");
        Assert.True(f.Call(route, f.Actor));
        var names = f.T.Database.Read(c => f.T.Database.Query<string>(c,
            "SELECT user_name FROM audit_logs WHERE user_id=@id AND action=@action;", new { id = f.Actor.Id, action }));
        Assert.Equal("Current author", Assert.Single(names));
        if (route.EndsWith("-save", StringComparison.Ordinal))
        {
            var creators = f.T.Database.Read(c => f.T.Database.Query<string>(c,
                "SELECT created_by_name FROM document_drafts WHERE created_by_id=@id;", new { id = f.Actor.Id }));
            Assert.Equal("Current author", Assert.Single(creators));
        }
    }

    [Fact]
    public void PurchaseMetadataAndDiscardRemainAllowedWithoutCostButPayloadDoesNot()
    {
        using var f = new DraftHeldAccessFixture(); f.Grant("purchasecost", false);
        Assert.True(f.Call("purchase-list", f.Actor)); Assert.False(f.Call("purchase-open", f.Actor));
        Assert.False(f.Call("purchase-save", f.Actor)); Assert.False(f.Call("purchase-update", f.Actor));
        Assert.True(f.D.CheckReadAccess(f.Actor, " PURCHASE ").Ok);
        Assert.False(f.D.CheckReadAccess(f.Actor, " PURCHASE ", includePayload: true).Ok);
        Assert.True(f.Call("purchase-discard", f.Actor));
        TestData.SeedRawSetting(f.T, MasterFoundationSettings.ProfitShowMargins, false); f.Grant("purchasecost", true);
        Assert.True(f.Call("purchase-list", f.Actor)); Assert.False(f.Call("purchase-open", f.Actor));
        Assert.True(f.Call("sale-open", f.Actor));
    }

    [Fact]
    public void PosAndPurchasesRemainIndependent()
    {
        using var f = new DraftHeldAccessFixture(); f.Grant("pos", false); f.Grant("purchases", true);
        Assert.False(f.Call("sale-list", f.Actor)); Assert.False(f.Call("hold-list", f.Actor)); Assert.True(f.Call("purchase-open", f.Actor));
        f.Grant("pos", true); f.Grant("purchases", false);
        Assert.True(f.Call("sale-open", f.Actor)); Assert.True(f.Call("hold-count", f.Actor)); Assert.False(f.Call("purchase-list", f.Actor));
    }

    [Fact]
    public void CountReadFailureCannotClaimSuccessfulZero()
    {
        using var f = new DraftHeldAccessFixture(); f.Execute("ALTER TABLE held_carts RENAME TO hidden_carts;");
        var count = f.H.Count(f.Actor); Assert.False(count.Ok); Assert.False(string.IsNullOrWhiteSpace(count.Error));
        Assert.False(f.H.List(f.Actor).Ok);
        f.Execute("ALTER TABLE hidden_carts RENAME TO held_carts;"); count = f.H.Count(f.Actor);
        Assert.True(count.Ok, count.Error); Assert.Equal(1, count.Value);
    }

    [Fact]
    public void WrongKindOrUnknownKindCannotReadPayloadOrOverwriteDraft()
    {
        using var f = new DraftHeldAccessFixture(); var before = f.Snapshot();
        Assert.False(f.D.OpenSale(f.Actor, f.PurchaseId).Ok); Assert.False(f.D.OpenPurchase(f.Actor, f.SaleId).Ok);
        Assert.False(f.D.SaveSaleDraft(f.Actor, f.Sale, f.PurchaseId).Ok);
        Assert.False(f.D.CheckReadAccess(f.Actor, "unknown", true).Ok); Assert.Equal(before, f.Snapshot());
        f.Execute($"UPDATE document_drafts SET draft_type='unknown' WHERE id={f.SaleId};"); before = f.Snapshot();
        Assert.False(f.D.Discard(f.Actor, f.SaleId).Ok); Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void StoredOwnerMustRemainActiveAndSameRoleBeforeOrdinaryBypass()
    {
        using var f = new DraftHeldAccessFixture(); f.Execute("ALTER TABLE user_permissions RENAME TO hidden_permissions;");
        Assert.True(f.Call("sale-list", TestData.Owner)); Assert.True(f.Call("hold-count", TestData.Owner));
        f.Execute("UPDATE users SET active=0 WHERE id=1;"); var before = f.Snapshot();
        Assert.False(f.Call("sale-open", TestData.Owner)); Assert.False(f.Call("hold-resume", TestData.Owner)); Assert.Equal(before, f.Snapshot());
        f.Execute("UPDATE users SET active=1,role='manager' WHERE id=1;"); before = f.Snapshot();
        Assert.False(f.Call("purchase-list", TestData.Owner)); Assert.False(f.Call("sale-discard", TestData.Owner)); Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void TerminalHeldAndDiscardedDraftCannotBeReusedOrInventExtraAudit()
    {
        using var f = new DraftHeldAccessFixture(); var money = f.MoneySnapshot(); Assert.True(f.Call("hold-resume", f.Actor));
        var before = f.Snapshot(); Assert.False(f.Call("hold-resume", f.Actor)); Assert.False(f.Call("hold-discard", f.Actor)); Assert.Equal(before, f.Snapshot());
        Assert.True(f.Call("sale-discard", f.Actor)); before = f.Snapshot();
        Assert.True(f.Call("sale-discard", f.Actor)); Assert.False(f.Call("sale-open", f.Actor)); Assert.False(f.Call("sale-update", f.Actor));
        Assert.Equal(before, f.Snapshot()); Assert.Equal(money, f.MoneySnapshot());
    }

    [Fact]
    public void DraftsStaySharedAcrossAllowedPeopleAndCreatorRemainsHistorical()
    {
        using var f = new DraftHeldAccessFixture(); var money = f.MoneySnapshot();
        Assert.True(f.Call("sale-open", f.Actor)); Assert.True(f.Call("purchase-open", f.Actor));
        Assert.True(f.Call("sale-update", f.Actor)); Assert.True(f.Call("purchase-update", f.Actor));
        var authors = f.T.Database.Read(c => f.T.Database.Query<string>(c, "SELECT created_by_name FROM document_drafts;"));
        Assert.All(authors, name => Assert.Equal(TestData.SeedOwnerName, name)); Assert.Equal(money, f.MoneySnapshot());
    }
}
