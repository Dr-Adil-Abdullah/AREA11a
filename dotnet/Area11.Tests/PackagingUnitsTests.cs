using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Portion 7: packaging, units and variants. One conversion everywhere (a "box" of a medicine
/// that has no box packing counts one single, on every path), "unit" as the loose-single word
/// on all seven quantity boxes, whole-only refusals on the two return paths that missed them,
/// and distinct 50ml/100ml variant products with a same-name warning at creation.
/// </summary>
public sealed class PackagingUnitsTests
{
    private static ProductService Products(TestDatabase test) => new(test.Database, test.Audit, test.Log);

    private static StockTakeService Takes(TestDatabase test) =>
        new(test.Database, test.Audit, new NumberingService(test.Settings),
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static int Create(
        ProductService products,
        string name,
        string baseUnit = "tablet",
        int boxStrips = 2,
        int stripTablets = 10,
        double strengthValue = 0,
        string strengthUnit = "",
        string packSizeLabel = "")
    {
        var created = products.Create(TestData.Owner, new ProductPatch
        {
            Name = name,
            BaseUnit = baseUnit,
            BoxStrips = boxStrips,
            StripTablets = stripTablets,
            StrengthValue = strengthValue,
            StrengthUnit = strengthUnit,
            PackSizeLabel = packSizeLabel,
            CostPaisa = 100,
            RetailPaisa = 200
        });
        Assert.True(created.Ok, created.Error);
        return created.Value;
    }

    private static void AddBatch(TestDatabase test, int productId, double quantity)
    {
        test.Database.Tx(connection => test.Database.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    is_sample, active, created_at)
VALUES(@productId, 'P7-1', '2027-06-30', '2027-06', @quantity, 100, 200, 0, 1, @at);",
            new { productId, quantity, at = Clock.Now() }));
    }

    private static double StockOf(TestDatabase test, int productId)
    {
        using var connection = test.Database.Open();
        return Convert.ToDouble(test.Database.Scalar(connection,
            "SELECT IFNULL(SUM(qty_base), 0) FROM batches WHERE product_id = @id AND active = 1;",
            new { id = productId }) ?? 0d);
    }

    [Fact]
    public void OneConversionEverywhere_BoxWithoutBoxPackingCountsOneSingle()
    {
        // The normal packs every screen always agreed on.
        Assert.Equal(20, Product.QuantityPerSelectedUnit("box", 2, 10));
        Assert.Equal(10, Product.QuantityPerSelectedUnit("strip", 2, 10));
        Assert.Equal(20, Packs.PerUnit(2, 10, "box"));

        // Strips but no box packing: "1 box" is one single, not a guessed strip.
        Assert.Equal(1, Product.QuantityPerSelectedUnit("box", 0, 10));
        Assert.Equal(1, Packs.PerUnit(0, 10, "box"));
        Assert.Equal(10, Product.QuantityPerSelectedUnit("strip", 0, 10));
        Assert.Equal(10, Packs.PerUnit(0, 10, "strip"));

        // No packs at all (a syrup bottle): box/strip stay a lenient single, as always.
        Assert.Equal(1, Product.QuantityPerSelectedUnit("box", 0, 0));
        Assert.Equal(1, Packs.PerUnit(0, 0, "strip"));
        Assert.Equal(1, Product.QuantityPerSelectedUnit("unit", 2, 10));
        Assert.Equal(1, Packs.PerUnit(2, 10, "unit"));
    }

    [Fact]
    public void SaleQuote_CountsBoxOfStripsOnlyProductAsOneSingle()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = Create(products, "Strips only", boxStrips: 0, stripTablets: 10);
        AddBatch(test, id, 50);

        var quote = TestData.Sales(test).Quote(new SaleInput
        {
            Lines = new List<SaleLineInput> { new() { ProductId = id, Unit = "box", Quantity = 1 } }
        }, TestData.Owner);

        Assert.True(quote.Ok, quote.Error);
        Assert.Equal(1, quote.Value!.Units);
    }

    [Fact]
    public void SaleAcceptsTheUnitWord_AndStoresTheBaseToken()
    {
        using var test = new TestDatabase();
        var id = TestData.AddProduct(test, batchQty: 100);

        var sale = TestData.Sales(test).Create(new SaleInput
        {
            PaymentMethod = "cash",
            Lines = new List<SaleLineInput> { new() { ProductId = id, Unit = "unit", Quantity = 2 } }
        }, TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(98d, StockOf(test, id));

        using var connection = test.Database.Open();
        var stored = test.Database.QuerySingle<string>(connection,
            "SELECT unit_sold FROM sale_items WHERE sale_id = @id;", new { id = sale.Value!.SaleId });
        Assert.Equal("base", stored);
    }

    [Fact]
    public void LegacyBaseWord_StillPosts_OldDraftsKeepWorking()
    {
        using var test = new TestDatabase();
        var id = TestData.AddProduct(test, batchQty: 100);

        var sale = TestData.Sales(test).Create(TestData.Cart(id, qty: 3), TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(97d, StockOf(test, id));
    }

    [Fact]
    public void PurchaseBoxOfStripsOnlyProduct_EntersOneSingle_AndUnitWordPosts()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = Create(products, "Loose strips", boxStrips: 0, stripTablets: 10);
        var supplierId = TestData.AddSupplier(test);

        var bought = TestData.Purchases(test).Create(new PurchaseInput
        {
            SupplierId = supplierId,
            SupplierInvoiceNo = "SUP-P7",
            Lines = new List<PurchaseLineInput>
            {
                new() { ProductId = id, Unit = "box", Quantity = 1, BatchNo = "P7-A", Expiry = "2027-06-30", CostPaisa = 100 },
                new() { ProductId = id, Unit = "unit", Quantity = 3, BatchNo = "P7-B", Expiry = "2027-06-30", CostPaisa = 100 }
            }
        }, TestData.Owner);

        Assert.True(bought.Ok, bought.Error);
        Assert.Equal(4d, StockOf(test, id));
    }

    [Fact]
    public void FractionalMl_ReturnsFreely_MlIsNotWholeOnly()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = Create(products, "Syrup ml", baseUnit: "ml", boxStrips: 0, stripTablets: 0);
        AddBatch(test, id, 100);

        var sale = TestData.Sales(test).Create(new SaleInput
        {
            PaymentMethod = "cash",
            Lines = new List<SaleLineInput> { new() { ProductId = id, Unit = "unit", Quantity = 2.5 } }
        }, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var returned = TestData.Returns(test).Create(TestData.Return(sale.Value!, 0.5), TestData.Owner);

        Assert.True(returned.Ok, returned.Error);
        Assert.Equal(98d, StockOf(test, id));
    }

    [Fact]
    public void FractionalInjectionReturn_IsRefused_AndWritesNothing()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = Create(products, "Ampoule", baseUnit: "injection", boxStrips: 0, stripTablets: 0);
        AddBatch(test, id, 10);

        var sale = TestData.Sales(test).Create(new SaleInput
        {
            PaymentMethod = "cash",
            Lines = new List<SaleLineInput> { new() { ProductId = id, Unit = "unit", Quantity = 2 } }
        }, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(8d, StockOf(test, id));

        var refused = TestData.Returns(test).Create(TestData.Return(sale.Value!, 0.5), TestData.Owner);

        Assert.False(refused.Ok);
        Assert.Contains("whole", refused.Error!);
        Assert.Equal(8d, StockOf(test, id));

        var whole = TestData.Returns(test).Create(TestData.Return(sale.Value!, 2), TestData.Owner);
        Assert.True(whole.Ok, whole.Error);
        Assert.Equal(10d, StockOf(test, id));
    }

    [Fact]
    public void FullReturnOfAnOldFractionalLine_StillPasses_AfterTheUnitTurnsWholeOnly()
    {
        using var test = new TestDatabase();
        var id = TestData.AddProduct(test, batchQty: 100);

        var sale = TestData.Sales(test).Create(new SaleInput
        {
            PaymentMethod = "cash",
            Lines = new List<SaleLineInput> { new() { ProductId = id, Unit = "unit", Quantity = 0.5 } }
        }, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE units SET whole_only = 1 WHERE name = 'tablet';"));

        // The whole remainder goes back exactly as it left: nothing new is being split.
        var returned = TestData.Returns(test).Create(TestData.Return(sale.Value!, 0.5), TestData.Owner);
        Assert.True(returned.Ok, returned.Error);
        Assert.Equal(100d, StockOf(test, id));
    }

    [Fact]
    public void FractionalInjectionNoBillReturn_IsRefused_AndWritesNothing()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = Create(products, "Loose ampoule", baseUnit: "injection", boxStrips: 0, stripTablets: 0);

        var refused = TestData.Provisionals(test).Create(TestData.RushReturn(id, qty: 0.5, unit: "unit"), TestData.Owner);

        Assert.False(refused.Ok);
        Assert.Contains("whole", refused.Error!);

        using var connection = test.Database.Open();
        var rows = Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM provisional_returns;", new { }) ?? 0L);
        Assert.Equal(0L, rows);
    }

    [Fact]
    public void WholeInjectionNoBillReturn_IsAccepted()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = Create(products, "Whole ampoule", baseUnit: "injection", boxStrips: 0, stripTablets: 0);

        var accepted = TestData.Provisionals(test).Create(TestData.RushReturn(id, qty: 1, unit: "unit"), TestData.Owner);

        Assert.True(accepted.Ok, accepted.Error);
        Assert.Equal(1d, StockOf(test, id));
    }

    [Fact]
    public void SecondVariantWithTheSameName_WarnsButSaves_TwoDistinctProducts()
    {
        using var test = new TestDatabase();
        var products = Products(test);

        var fifty = Create(products, "Variant syrup", baseUnit: "ml", boxStrips: 0, stripTablets: 0,
            strengthValue: 50, strengthUnit: "ml", packSizeLabel: "50 mL bottle");
        var hundred = products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Variant syrup",
            BaseUnit = "ml",
            BoxStrips = 0,
            StripTablets = 0,
            StrengthValue = 100,
            StrengthUnit = "ml",
            PackSizeLabel = "100 mL bottle",
            CostPaisa = 100,
            RetailPaisa = 200
        });

        Assert.True(hundred.Ok, hundred.Error);
        Assert.NotEqual(fifty, hundred.Value);
        var warning = Assert.Single(hundred.Warnings);
        Assert.Contains("Another medicine shares the name", warning);
        Assert.Contains("50 ml", warning);
        Assert.Contains("50 mL bottle", warning);
    }

    [Fact]
    public void ThirdVariantWithTheSameName_CountsTwoOthers()
    {
        using var test = new TestDatabase();
        var products = Products(test);

        Create(products, "Triple syrup", baseUnit: "ml", boxStrips: 0, stripTablets: 0,
            strengthValue: 50, strengthUnit: "ml", packSizeLabel: "50 mL bottle");
        Create(products, "Triple syrup", baseUnit: "ml", boxStrips: 0, stripTablets: 0,
            strengthValue: 100, strengthUnit: "ml", packSizeLabel: "100 mL bottle");
        var third = products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Triple syrup",
            BaseUnit = "ml",
            CostPaisa = 100,
            RetailPaisa = 200
        });

        Assert.True(third.Ok, third.Error);
        Assert.Contains("2 other medicines share the name", Assert.Single(third.Warnings));
    }

    [Fact]
    public void DistinctVariants_HoldSeparateStock_AndReadAsSeparateChoices()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var fifty = Create(products, "Split syrup", baseUnit: "ml", boxStrips: 0, stripTablets: 0,
            strengthValue: 50, strengthUnit: "ml", packSizeLabel: "50 mL bottle");
        var hundred = Create(products, "Split syrup", baseUnit: "ml", boxStrips: 0, stripTablets: 0,
            strengthValue: 100, strengthUnit: "ml", packSizeLabel: "100 mL bottle");
        AddBatch(test, fifty, 7);
        AddBatch(test, hundred, 11);

        var first = products.Get(fifty).Value!;
        var second = products.Get(hundred).Value!;

        Assert.Equal(7, first.StockBase);
        Assert.Equal(11, second.StockBase);
        Assert.NotEqual(first.ProductChoiceLabel, second.ProductChoiceLabel);
        Assert.Contains("Split syrup", first.ProductChoiceLabel);
        Assert.Contains("100", second.ProductChoiceLabel);
    }

    [Fact]
    public void StoredBaseToken_ReadsAsUnit_OnLabelsAndSlips()
    {
        Assert.Equal("unit", Packs.Display("base"));
        Assert.Equal("unit", Packs.Display(null));
        Assert.Equal("unit", Packs.Display("  "));
        Assert.Equal("strip", Packs.Display("strip"));

        Assert.Equal("2 unit", new QuarantineItem { QtyEntered = 2, UnitSold = "base" }.QuantityLabel);
        Assert.Equal("1 unit", new ReturnLineOption { QtyEntered = 1, UnitSold = "base" }.SoldLabel);
    }

    [Fact]
    public void StockTakeFinder_RowsCarryTheirVariantColumns()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        Create(products, "Finder syrup", baseUnit: "ml", boxStrips: 0, stripTablets: 0,
            strengthValue: 50, strengthUnit: "ml", packSizeLabel: "50 mL bottle");

        var found = Takes(test).NotOnSheet(1, "Finder");

        var row = Assert.Single(found);
        Assert.Equal(50, row.StrengthValue);
        Assert.Equal("ml", row.StrengthUnit);
        Assert.Equal("50 mL bottle", row.PackSizeLabel);
        Assert.Equal("50 ml", row.StrengthLabel);
    }
}
