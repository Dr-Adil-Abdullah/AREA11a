using System.Text.Json;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>The owner's phase-2 examples, actual FEFO costs, per-person caps and persisted state.</summary>
public sealed class PricingPhaseTwoTests
{
    private static PricingAllowanceService Policy(TestDatabase test) => new(test.Database, test.Audit, test.Log);
    private static void NoRound(TestDatabase test) => Assert.True(test.Settings.Set("bill.roundMode", "none", TestData.Owner).Ok);
    private static PricingAllowance Grant(User person) => new()
    {
        UserId = person.Id, EditRate = true, ManualReductionPercent = 20,
        ProfitDiscount = true, ProfitPercent = 30, RetailDiscount = true, RetailPercent = 10, OverrideBasis = true
    };
    private static SaleInput Discount(int product, decimal percent = 10, string? basis = null)
    {
        var cart = TestData.Cart(product);
        cart.DiscountPercent = percent;
        cart.DiscountBasis = basis;
        return cart;
    }

    [Fact]
    public void ExistingShopFutureBillsUseTheNewProfitDefault()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        Assert.Equal("profit", test.Settings.GetString("discount.billBasis"));
        var result = TestData.Sales(test).Create(Discount(product), TestData.Owner);
        Assert.True(result.Ok, result.Error);
        Assert.Equal(29000L, result.Value!.TotalPaisa);
    }

    [Fact]
    public void RetailModeNeedsConfirmationOnEverySavedBill()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var cart = Discount(product, basis: "retail");
        var sales = TestData.Sales(test);
        Assert.True(sales.Quote(cart, TestData.Owner).Ok);
        Assert.False(sales.Create(cart, TestData.Owner).Ok);
        cart.RetailDiscountConfirmed = true;
        var saved = sales.Create(cart, TestData.Owner);
        Assert.True(saved.Ok, saved.Error);
        Assert.Equal(27000L, saved.Value!.TotalPaisa);
        Assert.False(sales.Create(cart, TestData.Owner).Ok); // Reusing the same object cannot reuse consent.
        Assert.False(sales.Create(Discount(product, basis: "retail"), TestData.Owner).Ok);
    }

    [Fact]
    public void AConfirmationIsNeverSerializedIntoADraft()
    {
        var cart = new SaleInput { RetailDiscountConfirmed = true, DiscountBasis = "retail" };
        var clone = JsonSerializer.Deserialize<SaleInput>(JsonSerializer.Serialize(cart));
        Assert.False(clone!.RetailDiscountConfirmed);
        Assert.Equal("retail", clone.DiscountBasis);
    }

    [Fact]
    public void OnlyTheOwnerMayChangeDefaultBasis()
    {
        using var test = new TestDatabase();
        var manager = TestData.AddUser(test, "manager");
        Assert.False(test.Settings.Set("discount.billBasis", "retail", manager).Ok);
        var fake = new User { Id = manager.Id, Name = manager.Name, Role = "owner" };
        Assert.False(test.Settings.Set("discount.billBasis", "retail", fake).Ok);
        Assert.True(test.Settings.Set("discount.billBasis", "retail", TestData.Owner).Ok);
        Assert.False(test.Settings.Set("discount.billBasis", "typo", TestData.Owner).Ok);
    }

    [Fact]
    public void NewManagerDoesNotInheritPriceEditingRights()
    {
        using var test = new TestDatabase();
        var manager = TestData.AddUser(test, "manager");
        var allowance = Policy(test).ForUser(manager);
        Assert.False(allowance.EditRate);
        Assert.False(allowance.ProfitDiscount);
        var product = TestData.AddProduct(test);
        var cart = TestData.Cart(product);
        cart.Lines[0].UnitPricePaisa = 29000;
        Assert.False(TestData.Sales(test).Quote(cart, manager).Ok);
        var permissions = new PermissionService(test.Database, test.Log, test.Audit);
        Assert.True(permissions.Set(TestData.Owner, manager.Id, Modules.Pos, false).Ok);
        Assert.False(TestData.Sales(test).Quote(TestData.Cart(product), manager).Ok);
    }

    [Fact]
    public void ManagerCannotGrantHimselfOrSomeoneElsePricingRights()
    {
        using var test = new TestDatabase();
        var manager = TestData.AddUser(test, "manager");
        var cashier = TestData.Cashier(test);
        Assert.False(Policy(test).Save(manager, Grant(manager)).Ok);
        Assert.False(Policy(test).Save(manager, Grant(cashier)).Ok);
        Assert.False(Policy(test).ForUser(cashier).EditRate);
    }

    [Fact]
    public void OwnerSavesIndependentLimitsForEachPerson()
    {
        using var test = new TestDatabase();
        var first = TestData.Cashier(test);
        var second = TestData.Cashier(test);
        Assert.True(Policy(test).Save(TestData.Owner, Grant(first)).Ok);
        Assert.Equal(20m, Policy(test).ForUser(first).ManualReductionPercent);
        Assert.Equal(30m, Policy(test).ForUser(first).ProfitPercent);
        Assert.Equal(10m, Policy(test).ForUser(first).RetailPercent);
        Assert.False(Policy(test).ForUser(second).EditRate);
    }

    [Fact]
    public void ForgedOwnerRoleCannotGrantPricingRights()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        var fake = new User { Id = cashier.Id, Name = cashier.Name, Role = "owner" };
        Assert.False(Policy(test).Save(fake, Grant(cashier)).Ok);
        Assert.False(Policy(test).ForUser(fake).EditRate);
    }

    [Fact]
    public void ManualRateCannotBypassTheTwentyPercentReductionCap()
    {
        using var test = new TestDatabase();
        NoRound(test);
        var cashier = TestData.Cashier(test);
        Assert.True(Policy(test).Save(TestData.Owner, Grant(cashier)).Ok);
        var product = TestData.AddProduct(test, costPaisa: 50000, retailPaisa: 120000);
        var cart = TestData.Cart(product);
        cart.Lines[0].UnitPricePaisa = 80000;
        var rejected = TestData.Sales(test).Quote(cart, cashier);
        Assert.False(rejected.Ok);
        Assert.Contains("960", rejected.Error ?? string.Empty);
        Assert.Contains("240", rejected.Error ?? string.Empty);
        cart.Lines[0].UnitPricePaisa = 96000;
        Assert.True(TestData.Sales(test).Quote(cart, cashier).Ok);
        // A missing reference is price entry, not a measurable percentage reduction.
        var missing = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 0, name: "No reference");
        var priced = TestData.Cart(missing);
        priced.Lines[0].UnitPricePaisa = 25000;
        Assert.True(TestData.Sales(test).Quote(priced, cashier).Ok);
        priced.Lines[0].UnitPricePaisa = 19000;
        Assert.False(TestData.Sales(test).Quote(priced, cashier).Ok);
    }

    [Fact]
    public void CostFloorIsStricterThanTheManualPercentageFloor()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        Assert.True(Policy(test).Save(TestData.Owner, Grant(cashier)).Ok);
        var product = TestData.AddProduct(test, costPaisa: 100000, retailPaisa: 120000);
        var cart = TestData.Cart(product);
        cart.Lines[0].UnitPricePaisa = 96000;
        Assert.False(TestData.Sales(test).Quote(cart, cashier).Ok);
        cart.Lines[0].UnitPricePaisa = 100000;
        Assert.True(TestData.Sales(test).Quote(cart, cashier).Ok);
    }

    [Fact]
    public void OwnerCannotSwitchOffCostProtectionEvenWithLegacyFalseStored()
    {
        using var test = new TestDatabase();
        Assert.False(test.Settings.Set("discount.blockBelowCost", false, TestData.Owner).Ok);
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE settings SET value = 'false' WHERE key = 'discount.blockBelowCost';"));
        test.Settings.Reload();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var cart = TestData.Cart(product);
        cart.Lines[0].UnitPricePaisa = 15000;
        Assert.False(TestData.Sales(test).Create(cart, TestData.Owner).Ok);
    }

    [Fact]
    public void ProfitAndRetailCapsAreSeparateAndRevocationIsImmediate()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        var grant = Grant(cashier);
        Assert.True(Policy(test).Save(TestData.Owner, grant).Ok);
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var sales = TestData.Sales(test);
        Assert.True(sales.Quote(Discount(product, 20, "profit"), cashier).Ok);
        Assert.False(sales.Quote(Discount(product, 20, "retail"), cashier).Ok);
        grant.ProfitPercent = 5;
        Assert.True(Policy(test).Save(TestData.Owner, grant).Ok);
        Assert.False(sales.Quote(Discount(product, 20, "profit"), cashier).Ok);
    }

    [Fact]
    public void OneBillOverrideRequiresItsOwnPermissionAndDoesNotChangeSettings()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        var grant = Grant(cashier);
        grant.OverrideBasis = false;
        Assert.True(Policy(test).Save(TestData.Owner, grant).Ok);
        var product = TestData.AddProduct(test);
        Assert.False(TestData.Sales(test).Quote(Discount(product, 5, "retail"), cashier).Ok);
        grant.OverrideBasis = true;
        Assert.True(Policy(test).Save(TestData.Owner, grant).Ok);
        Assert.True(TestData.Sales(test).Quote(Discount(product, 5, "retail"), cashier).Ok);
        Assert.Equal("profit", test.Settings.GetString("discount.billBasis"));
    }

    [Fact]
    public void ManualAndItemDiscountLinesAreExcludedFromBillDiscount()
    {
        using var test = new TestDatabase();
        NoRound(test);
        var product = TestData.AddProduct(test, costPaisa: 5000, retailPaisa: 10000);
        var cart = Discount(product, basis: "retail");
        cart.Lines.Clear();
        cart.Lines.Add(new SaleLineInput { ProductId = product, Quantity = 1, PricingMode = "manual", UnitPricePaisa = 9500 });
        cart.Lines.Add(new SaleLineInput { ProductId = product, Quantity = 1, PricingMode = "discount", DiscountBasis = "retail", LineDiscountPercent = 20 });
        cart.Lines.Add(new SaleLineInput { ProductId = product, Quantity = 1 });
        var quote = TestData.Sales(test).Quote(cart, TestData.Owner);
        Assert.True(quote.Ok, quote.Error);
        Assert.Equal(26500L, quote.Value!.TotalPaisa);
        Assert.Equal(new long[] { 9500, 8000, 9000 }, quote.Value.Lines.Select(line => line.NetPaisa));
        cart.Lines[0].UnitPricePaisa = 11000;
        var increased = TestData.Sales(test).Quote(cart, TestData.Owner);
        Assert.True(increased.Ok, increased.Error);
        Assert.Equal(28000L, increased.Value!.TotalPaisa);
    }

    [Fact]
    public void ResetNormalAndNewLinesUseTheCurrentBillDiscount()
    {
        using var test = new TestDatabase();
        NoRound(test);
        var product = TestData.AddProduct(test, costPaisa: 5000, retailPaisa: 10000);
        var cart = Discount(product, basis: "retail");
        cart.Lines.Add(new SaleLineInput { ProductId = product, Quantity = 1 });
        var quote = TestData.Sales(test).Quote(cart, TestData.Owner);
        Assert.True(quote.Ok, quote.Error);
        Assert.Equal(18000L, quote.Value!.TotalPaisa);
    }

    [Fact]
    public void EachItemMayChooseADifferentDiscountBasis()
    {
        using var test = new TestDatabase();
        NoRound(test);
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var cart = TestData.Cart(product);
        cart.Lines[0].PricingMode = "discount";
        cart.Lines[0].DiscountBasis = "profit";
        cart.Lines[0].LineDiscountPercent = 10;
        cart.Lines.Add(new SaleLineInput { ProductId = product, Quantity = 1, PricingMode = "discount", DiscountBasis = "retail", LineDiscountPercent = 10 });
        var quote = TestData.Sales(test).Quote(cart, TestData.Owner);
        Assert.True(quote.Ok, quote.Error);
        Assert.Equal(56000L, quote.Value!.TotalPaisa);
    }

    [Fact]
    public void ManualRateAndItemDiscountCannotBeStackedByADirectCaller()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var cart = TestData.Cart(product);
        cart.Lines[0].PricingMode = "discount";
        cart.Lines[0].UnitPricePaisa = 25000;
        cart.Lines[0].LineDiscountPercent = 10;
        Assert.False(TestData.Sales(test).Quote(cart, TestData.Owner).Ok);
    }

    [Fact]
    public void ActualBatchCostNotTheProductCardDeterminesProfit()
    {
        using var test = new TestDatabase();
        NoRound(test);
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE products SET cost_paisa = 100 WHERE id = @id;", new { id = product }));
        var quote = TestData.Sales(test).Quote(Discount(product), TestData.Owner);
        Assert.True(quote.Ok, quote.Error);
        Assert.Equal(29000L, quote.Value!.TotalPaisa);
        Assert.Equal(20000L, quote.Value.Lines[0].CostPaisa);
    }

    [Fact]
    public void SavedPricingSnapshotsDoNotChangeWhenDefaultChanges()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var saved = TestData.Sales(test).Create(Discount(product), TestData.Owner);
        Assert.True(saved.Ok, saved.Error);
        Assert.True(test.Settings.Set("discount.billBasis", "retail", TestData.Owner).Ok);
        using var connection = test.Database.Open();
        Assert.Equal(29000L, test.Database.QuerySingle<long>(connection,
            "SELECT total_paisa FROM sales WHERE id = @id;", new { id = saved.Value!.SaleId }));
        Assert.Equal("profit", test.Database.QuerySingle<string>(connection,
            "SELECT discount_basis FROM sales WHERE id = @id;", new { id = saved.Value.SaleId }));
        Assert.Equal("profit", test.Database.QuerySingle<string>(connection,
            "SELECT discount_basis FROM sale_items WHERE sale_id = @id;", new { id = saved.Value.SaleId }));
    }

    [Fact]
    public void HeldCartPreservesLineModesAndBillBasis()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var cart = TestData.Cart(product);
        cart.DiscountBasis = "retail";
        cart.Lines[0].PricingMode = "discount";
        cart.Lines[0].DiscountBasis = "profit";
        cart.Lines[0].LineDiscountPercent = 10;
        var holds = TestData.Holds(test);
        Assert.True(holds.Hold(cart, TestData.Owner).Ok);
        var listed = holds.List(TestData.Owner);
        Assert.True(listed.Ok, listed.Error);
        var resumed = holds.Resume(TestData.Owner, listed.Value![0].Id);
        Assert.True(resumed.Ok, resumed.Error);
        Assert.Equal("retail", resumed.Value!.DiscountBasis);
        Assert.Equal("discount", resumed.Value.Lines[0].PricingMode);
        Assert.Equal("profit", resumed.Value.Lines[0].DiscountBasis);
        Assert.Equal(10m, resumed.Value.Lines[0].LineDiscountPercent);
    }

    [Fact]
    public void DraftPreservesAdjustmentsButRechecksCurrentPermissions()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        var product = TestData.AddProduct(test);
        var cart = TestData.Cart(product);
        cart.Lines[0].PricingMode = "manual";
        cart.Lines[0].UnitPricePaisa = 29000;
        var drafts = TestData.Drafts(test);
        var saved = drafts.SaveSaleDraft(TestData.Owner, cart);
        Assert.True(saved.Ok, saved.Error);
        var reopened = drafts.OpenSale(cashier, saved.Value!.Id);
        Assert.True(reopened.Ok, reopened.Error);
        Assert.Equal("manual", reopened.Value!.Lines[0].PricingMode);
        Assert.False(drafts.PostSale(cashier, saved.Value.Id, reopened.Value).Ok);
    }

    [Fact]
    public void DisabledProfitVisibilityDoesNotLeakCostThroughQuote()
    {
        using var test = new TestDatabase();
        Assert.True(test.Settings.Set("profit.showMargins", false, TestData.Owner).Ok);
        var product = TestData.AddProduct(test);
        var quote = TestData.Sales(test).Quote(TestData.Cart(product), TestData.Owner);
        Assert.True(quote.Ok, quote.Error);
        Assert.Equal(0L, quote.Value!.Lines[0].CostPaisa);
    }

    [Fact]
    public void RejectedCostDiscountLeavesStockAndSaleTablesUntouched()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var cart = Discount(product, 50, "retail");
        cart.RetailDiscountConfirmed = true;
        var before = TestData.Sales(test).Create(cart, TestData.Owner);
        Assert.False(before.Ok);
        using var connection = test.Database.Open();
        Assert.Equal(0L, test.Database.QuerySingle<long>(connection, "SELECT COUNT(*) FROM sales;"));
        Assert.Equal(0L, test.Database.QuerySingle<long>(connection, "SELECT COUNT(*) FROM stock_movements;"));
    }

    [Fact]
    public void AnExplicitZeroPercentItemExclusionStillRequiresPermission()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        var product = TestData.AddProduct(test);
        var cart = TestData.Cart(product);
        cart.Lines[0].PricingMode = "discount";
        Assert.False(TestData.Sales(test).Quote(cart, cashier).Ok);
    }

    [Fact]
    public void UiOnlySettingsPathCannotBypassPricingPolicy()
    {
        using var test = new TestDatabase();
        Assert.False(test.Settings.SetUiOnly("discount.billBasis", "retail").Ok);
        Assert.False(test.Settings.SetUiOnly("discount.blockBelowCost", false).Ok);
        Assert.Equal("profit", test.Settings.GetString("discount.billBasis"));
    }

    [Fact]
    public void StripManualCapUsesTheStripReferenceNotOneTablet()
    {
        using var test = new TestDatabase();
        NoRound(test);
        var cashier = TestData.Cashier(test);
        Assert.True(Policy(test).Save(TestData.Owner, Grant(cashier)).Ok);
        var product = TestData.AddProduct(test, costPaisa: 5000, retailPaisa: 12000);
        var cart = TestData.Cart(product);
        cart.Lines[0].Unit = "strip"; // Ten tablets = Rs1200 retail.
        cart.Lines[0].UnitPricePaisa = 95000;
        Assert.False(TestData.Sales(test).Quote(cart, cashier).Ok);
        cart.Lines[0].UnitPricePaisa = 96000;
        Assert.True(TestData.Sales(test).Quote(cart, cashier).Ok);
    }

    [Fact]
    public void ProfitDiscountUsesBothFefoBatchCosts()
    {
        using var test = new TestDatabase();
        NoRound(test);
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000, batchQty: 1);
        test.Database.Tx(connection => test.Database.Execute(connection, @"
INSERT INTO batches(product_id,batch_no,expiry_date,qty_base,cost_paisa,retail_paisa,active,created_at)
VALUES(@product,'B-2','2027-07-31',1,25000,30000,1,@at);", new { product, at = Clock.Now() }));
        var cart = Discount(product);
        cart.Lines[0].Quantity = 2;
        var quote = TestData.Sales(test).Quote(cart, TestData.Owner);
        Assert.True(quote.Ok, quote.Error);
        Assert.Equal(58500L, quote.Value!.TotalPaisa); // 600 - 10% of (600 - 450)
        Assert.Equal(45000L, quote.Value.Lines[0].CostPaisa);
    }

    [Fact]
    public void ADiscountedPartialReturnUsesSavedNetNotTodaysBasis()
    {
        using var test = new TestDatabase();
        NoRound(test);
        var product = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var cart = Discount(product);
        cart.Lines[0].Quantity = 2;
        var saved = TestData.Sales(test).Create(cart, TestData.Owner);
        Assert.True(saved.Ok, saved.Error);
        Assert.True(test.Settings.Set("discount.billBasis", "retail", TestData.Owner).Ok);
        var returns = TestData.Returns(test);
        var first = returns.Create(TestData.Return(saved.Value!, 1), TestData.Owner);
        Assert.True(first.Ok, first.Error);
        Assert.Equal(29000L, first.Value!.RefundPaisa);
        var second = returns.Create(TestData.Return(saved.Value!, 1), TestData.Owner);
        Assert.True(second.Ok, second.Error);
        Assert.Equal(58000L, first.Value.RefundPaisa + second.Value!.RefundPaisa);
    }

    [Fact]
    public void InvalidPricingLimitsDoNotOverwriteThePreviousAllowance()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        var grant = Grant(cashier);
        Assert.True(Policy(test).Save(TestData.Owner, grant).Ok);
        grant.ProfitPercent = 101;
        Assert.False(Policy(test).Save(TestData.Owner, grant).Ok);
        Assert.Equal(30m, Policy(test).ForUser(cashier).ProfitPercent);
    }

    [Fact]
    public void ReopeningARetailDraftRequiresFreshSaveConfirmation()
    {
        using var test = new TestDatabase();
        var product = TestData.AddProduct(test);
        var cart = Discount(product, basis: "retail");
        cart.RetailDiscountConfirmed = true;
        var drafts = TestData.Drafts(test);
        var saved = drafts.SaveSaleDraft(TestData.Owner, cart);
        Assert.True(saved.Ok, saved.Error);
        var opened = drafts.OpenSale(TestData.Owner, saved.Value!.Id);
        Assert.True(opened.Ok, opened.Error);
        Assert.False(opened.Value!.RetailDiscountConfirmed);
        Assert.False(drafts.PostSale(TestData.Owner, saved.Value.Id, opened.Value).Ok);
        opened.Value.RetailDiscountConfirmed = true;
        Assert.True(drafts.PostSale(TestData.Owner, saved.Value.Id, opened.Value).Ok);
    }
}
