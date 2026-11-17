using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Cost-bearing preparation refuses rather than returning an editable zero-price draft.</summary>
public sealed class PurchasePreparationTests
{
    private sealed record Fixture(PurchaseInput Input, int Draft);
    private static void Execute(TestDatabase t, string sql) => t.Database.Tx(c => { t.Database.Execute(c, sql); });
    private static string Text(TestDatabase t, string sql)
    {
        using var c = t.Database.Open(); return t.Database.Scalar(c, sql)?.ToString() ?? string.Empty;
    }
    private static long Number(TestDatabase t, string sql) => Convert.ToInt64(Text(t, sql));
    private static void Grant(TestDatabase t, User user, string module, bool allowed = true) =>
        Assert.True(TestData.Permissions(t).Set(TestData.Owner, user.Id, module, allowed).Ok);
    private static User Staff(TestDatabase t, bool cost = false)
    {
        var u = TestData.Cashier(t); Grant(t, u, Modules.Purchases);
        if (cost) Grant(t, u, Modules.PurchaseCost); return u;
    }
    private static Fixture Seed(TestDatabase t, long cost = 12345, bool sample = false)
    {
        var p = TestData.AddProduct(t, costPaisa: cost, batchQty: 0);
        var input = new PurchaseInput
        {
            SupplierId = TestData.AddSupplier(t), PaidPaisa = sample || cost == 0 ? 0 : 10000,
            PaidMethod = "online", Lines = new List<PurchaseLineInput>
            {
                new() { ProductId = p, Unit = "base", Quantity = 3, CostPaisa = cost,
                    RetailPaisa = 30000, BatchNo = "SYN-P13", Expiry = "2099-06", IsSample = sample }
            }
        };
        var saved = TestData.Drafts(t).SavePurchaseDraft(TestData.Owner, input);
        Assert.True(saved.Ok, saved.Error); return new Fixture(input, saved.Value!.Id);
    }
    private static string Snapshot(TestDatabase t) => string.Join("|", new[]
    {
        "SELECT COUNT(*) FROM purchases;", "SELECT COUNT(*) FROM purchase_items;", "SELECT COUNT(*) FROM payments;",
        "SELECT COUNT(*) FROM stock_movements;", "SELECT COUNT(*) FROM audit_logs;",
        "SELECT SUM(qty_base) FROM batches;", "SELECT SUM(cost_paisa) FROM products;",
        "SELECT SUM(balance_paisa) FROM suppliers;",
        "SELECT group_concat(payload_json || ':' || status || ':' || COALESCE(posted_document_id,'')) FROM document_drafts;"
    }.Select(sql => Text(t, sql)));
    private static void Denied(TestDatabase t, Fixture f, User? actor)
    {
        var before = Snapshot(t); var purchases = TestData.Purchases(t); var drafts = TestData.Drafts(t);
        Assert.False(purchases.CheckEntryAccess(actor).Ok);
        var create = purchases.Create(f.Input, actor); Assert.False(create.Ok); Assert.Null(create.Value);
        var open = drafts.OpenPurchase(actor, f.Draft); Assert.False(open.Ok); Assert.Null(open.Value);
        Assert.False(drafts.SavePurchaseDraft(actor, f.Input).Ok);
        Assert.False(drafts.SavePurchaseDraft(actor, f.Input, f.Draft).Ok);
        Assert.False(drafts.PostPurchase(actor, f.Draft).Ok);
        Assert.False(drafts.PostPurchase(actor, f.Draft, f.Input).Ok);
        Assert.Equal(before, Snapshot(t));
    }

    [Fact]
    public void NullActorCannotPrepareReadSaveOrPost()
    {
        using var t = new TestDatabase(); var f = Seed(t); Denied(t, f, null);
    }

    [Fact]
    public void ForgedOwnerRoleDoesNotUpgradeAnExistingStaffIdentity()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t, true);
        Denied(t, f, new User { Id = u.Id, Name = u.Name, Role = "owner" });
    }

    [Fact]
    public void MissingStoredIdentityCannotPrepare()
    {
        using var t = new TestDatabase(); var f = Seed(t);
        Denied(t, f, new User { Id = int.MaxValue, Name = "Missing", Role = "owner" });
    }

    [Fact]
    public void InactiveSuppliedOwnerCannotPrepare()
    {
        using var t = new TestDatabase(); var f = Seed(t);
        Denied(t, f, new User { Id = 1, Name = "Owner", Role = "owner", Active = false });
    }

    [Fact]
    public void StoredOwnerDeactivationRefusesWithoutMutatingDraftOrStock()
    {
        using var t = new TestDatabase(); var f = Seed(t); Execute(t, "UPDATE users SET active=0 WHERE id=1;");
        Denied(t, f, TestData.Owner);
    }

    [Fact]
    public void StoredRoleChangeInvalidatesOldActor()
    {
        using var t = new TestDatabase(); var f = Seed(t); Execute(t, "UPDATE users SET role='manager' WHERE id=1;");
        Denied(t, f, TestData.Owner);
    }

    [Fact]
    public void PurchasesModuleCannotBeReplacedByBothFinancialGrants()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t, true);
        Grant(t, u, Modules.Profit); Grant(t, u, Modules.Purchases, false); Denied(t, f, u);
        Assert.False(TestData.Drafts(t).List(u, DraftType.Purchase).Ok);
        Assert.False(TestData.Drafts(t).Discard(u, f.Draft).Ok);
    }

    [Fact]
    public void ProfitOnlyDoesNotAuthorizeCostBearingPreparation()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t); Grant(t, u, Modules.Profit);
        Denied(t, f, u);
    }

    [Fact]
    public void ManagerRankAloneDoesNotAuthorizeCostBearingPreparation()
    {
        using var t = new TestDatabase(); var f = Seed(t); Denied(t, f, TestData.AddUser(t, "manager"));
    }

    [Fact]
    public void OwnerCostRevocationIsNotBypassedByRank()
    {
        using var t = new TestDatabase(); var f = Seed(t); Grant(t, TestData.Owner, Modules.PurchaseCost, false);
        Denied(t, f, TestData.Owner);
    }

    [Fact]
    public void MainOffBlocksEvenOwnerAndKeepsRawDraftIntact()
    {
        using var t = new TestDatabase(); var f = Seed(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        Denied(t, f, TestData.Owner);
    }

    [Fact]
    public void MainOffBlocksCostGranteeToo()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t, true);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok); Denied(t, f, u);
    }

    [Fact]
    public void ExistingServiceRechecksCostAtNextInteraction()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t, true); var service = TestData.Purchases(t);
        Assert.True(service.CheckEntryAccess(u).Ok); Grant(t, u, Modules.PurchaseCost, false);
        Assert.False(service.CheckEntryAccess(u).Ok); Assert.False(service.Create(f.Input, u).Ok); Denied(t, f, u);
    }

    [Fact]
    public void ExistingDraftServiceCannotPostPreviouslyOpenedPayloadAfterRevocation()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t, true); var service = TestData.Drafts(t);
        var opened = service.OpenPurchase(u, f.Draft); Assert.True(opened.Ok, opened.Error);
        Grant(t, u, Modules.PurchaseCost, false); var before = Snapshot(t);
        Assert.False(service.PostPurchase(u, f.Draft, opened.Value).Ok); Assert.Equal(before, Snapshot(t));
        Assert.Equal(12345L, opened.Value!.Lines[0].CostPaisa); // Previously returned objects cannot be recalled.
    }

    [Fact]
    public void CostOnlyStaffCanPostActualAmountsWithoutProfitGrant()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t, true); Grant(t, u, Modules.Profit, false);
        var result = TestData.Purchases(t).Create(f.Input, u); Assert.True(result.Ok, result.Error);
        Assert.True(result.Value!.CostKnown); Assert.Equal(37035L, result.Value.TotalPaisa);
        Assert.Equal(Money.Format(37035), result.Value.TotalLabel);
        Assert.Equal(37035L, Number(t, "SELECT total_paisa FROM purchases;"));
        Assert.Equal(27035L, Number(t, "SELECT balance_paisa FROM suppliers;"));
        Assert.Equal(10000L, Number(t, "SELECT amount_paisa FROM payments;"));
        Assert.Equal(3L, Number(t, "SELECT SUM(qty_base) FROM batches;"));
    }

    [Fact]
    public void AuthorizedRealZeroIsKnownNotHidden()
    {
        using var t = new TestDatabase(); var f = Seed(t, 0); var result = TestData.Purchases(t).Create(f.Input, TestData.Owner);
        Assert.True(result.Ok, result.Error); Assert.True(result.Value!.CostKnown);
        Assert.Equal(0L, result.Value.TotalPaisa); Assert.Equal(Money.Format(0), result.Value.TotalLabel);
    }

    [Fact]
    public void SamplesStillRequirePreparationGrantAndRemainZeroWhenAllowed()
    {
        using var t = new TestDatabase(); var f = Seed(t, sample: true); var u = Staff(t); Denied(t, f, u);
        Grant(t, u, Modules.PurchaseCost); var result = TestData.Purchases(t).Create(f.Input, u);
        Assert.True(result.Ok, result.Error); Assert.True(result.Value!.CostKnown); Assert.Equal(0L, result.Value.TotalPaisa);
        Assert.Equal(0L, Number(t, "SELECT cost_paisa FROM batches WHERE batch_no='SYN-P13';"));
    }

    [Fact]
    public void DefaultResultDoesNotLabelUnknownTotalAsKnownZero()
    {
        var result = new PurchaseResult(); Assert.False(result.CostKnown); Assert.Equal("hidden", result.TotalLabel);
    }

    [Fact]
    public void SummaryListRemainsAvailableWithoutCostOrMainButReturnsNoPayload()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t);
        Assert.True(t.Settings.Set(MasterFoundationSettings.ProfitShowMargins, false, TestData.Owner).Ok);
        var listed = TestData.Drafts(t).List(u, DraftType.Purchase); Assert.True(listed.Ok, listed.Error);
        Assert.Equal(f.Draft, Assert.Single(listed.Value!).Id);
        Assert.Null(typeof(DraftSummary).GetProperty("PayloadJson")); Assert.Null(typeof(DraftSummary).GetProperty("TotalPaisa"));
        Assert.False(TestData.Drafts(t).OpenPurchase(u, f.Draft).Ok);
    }

    [Fact]
    public void SummaryListAndDiscardRequireCurrentStoredAccount()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t); var service = TestData.Drafts(t);
        Assert.True(service.List(u, DraftType.Purchase).Ok); Execute(t, $"UPDATE users SET active=0 WHERE id={u.Id};");
        Assert.False(service.List(u, DraftType.Purchase).Ok); Assert.False(service.Discard(u, f.Draft).Ok);
        Assert.Equal("draft", Text(t, "SELECT status FROM document_drafts;"));
    }

    [Fact]
    public void ModuleOnlyCanDiscardWithoutOpeningMonetaryPayload()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t);
        Assert.True(TestData.Drafts(t).Discard(u, f.Draft).Ok);
        Assert.Equal("discarded", Text(t, "SELECT status FROM document_drafts;"));
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM purchases;"));
    }

    [Fact]
    public void RestoringGrantOpensOriginalNotMaskedZeroDraft()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t, true); var service = TestData.Drafts(t);
        Grant(t, u, Modules.PurchaseCost, false); Denied(t, f, u); Grant(t, u, Modules.PurchaseCost);
        var opened = service.OpenPurchase(u, f.Draft); Assert.True(opened.Ok, opened.Error);
        Assert.Equal(12345L, opened.Value!.Lines[0].CostPaisa); Assert.Equal(10000L, opened.Value.PaidPaisa);
        Assert.Equal(3d, opened.Value.Lines[0].Quantity);
    }

    [Fact]
    public void AuthorizedDraftUpdateAndPostKeepActualValues()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t, true); var service = TestData.Drafts(t);
        f.Input.Lines[0].Quantity = 4; Assert.True(service.SavePurchaseDraft(u, f.Input, f.Draft).Ok);
        var posted = service.PostPurchase(u, f.Draft); Assert.True(posted.Ok, posted.Error);
        Assert.True(posted.Value!.CostKnown); Assert.Equal(49380L, posted.Value.TotalPaisa);
        Assert.Equal("posted", Text(t, "SELECT status FROM document_drafts;"));
        Assert.Equal(posted.Value.PurchaseId, Number(t, "SELECT posted_document_id FROM document_drafts;"));
    }

    [Fact]
    public void ExplicitCurrentPayloadCannotBypassCostGateOrReplaceSavedDraft()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t); var before = Snapshot(t);
        f.Input.Lines[0].CostPaisa = 0; f.Input.PaidPaisa = 0;
        Assert.False(TestData.Drafts(t).PostPurchase(u, f.Draft, f.Input).Ok); Assert.Equal(before, Snapshot(t));
    }

    [Fact]
    public void RepeatedAuthorizedPostDoesNotCreateSecondPurchase()
    {
        using var t = new TestDatabase(); var f = Seed(t); var service = TestData.Drafts(t);
        Assert.True(service.PostPurchase(TestData.Owner, f.Draft).Ok); var before = Snapshot(t);
        Assert.False(service.PostPurchase(TestData.Owner, f.Draft).Ok); Assert.Equal(before, Snapshot(t));
    }

    [Fact]
    public void PurchaseValidationFailureKeepsDraftRetryableWithoutPartialStock()
    {
        using var t = new TestDatabase(); var f = Seed(t); var service = TestData.Drafts(t);
        f.Input.Lines.Add(new PurchaseLineInput { ProductId = int.MaxValue, Quantity = 1, Unit = "base", CostPaisa = 1 });
        Assert.False(service.PostPurchase(TestData.Owner, f.Draft, f.Input).Ok);
        Assert.Equal("draft", Text(t, "SELECT status FROM document_drafts;"));
        Assert.Equal(0L, Number(t, "SELECT COUNT(*) FROM purchases;"));
        Assert.Equal(0L, Number(t, "SELECT SUM(qty_base) FROM batches;"));
        f.Input.Lines.RemoveAt(1); Assert.True(service.PostPurchase(TestData.Owner, f.Draft, f.Input).Ok);
    }

    [Fact]
    public void DeniedOpenDoesNotDeserializeCorruptPayloadOrReturnEditableProjection()
    {
        using var t = new TestDatabase(); var f = Seed(t); var u = Staff(t);
        Execute(t, @"UPDATE document_drafts SET payload_json='{""Lines"":42}';");
        var opened = TestData.Drafts(t).OpenPurchase(u, f.Draft); Assert.False(opened.Ok); Assert.Null(opened.Value);
        Assert.Contains("View Purchase Cost", opened.Error!); Assert.Equal("{\"Lines\":42}", Text(t, "SELECT payload_json FROM document_drafts;"));
    }

    [Fact]
    public void PermissionStorageFailureRefusesCostPreparationInsteadOfAssumingOwner()
    {
        using var t = new TestDatabase(); var f = Seed(t); Execute(t, "DROP TABLE user_permissions;"); Denied(t, f, TestData.Owner);
    }

    [Fact]
    public void MissingUserTableFailsSafelyWithoutExposingDatabaseDetails()
    {
        using var t = new TestDatabase(); var service = TestData.Purchases(t); Execute(t, "ALTER TABLE users RENAME TO saved_users;");
        var result = service.CheckEntryAccess(TestData.Owner); Assert.False(result.Ok);
        Assert.DoesNotContain("no such table", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Purchase preparation access could not be checked. Refresh and try again.", result.Error);
    }

    [Fact]
    public void RepeatedPreparationChecksDoNotWriteAccountingOrDrafts()
    {
        using var t = new TestDatabase(); var f = Seed(t); var before = Snapshot(t); var service = TestData.Purchases(t);
        for (var i = 0; i < 4; i++) Assert.True(service.CheckEntryAccess(TestData.Owner).Ok);
        Assert.Equal(before, Snapshot(t)); Assert.Equal(12345L, f.Input.Lines[0].CostPaisa);
    }
}
