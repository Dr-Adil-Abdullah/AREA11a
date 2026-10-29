using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Phase 3: descriptive variants are editable and discoverable without becoming a stock model.</summary>
public sealed class PackagingSearchTests
{
    [Fact]
    public void PackLabelIsEditableNormalized_AndPartialUpdatePreservesOtherFields()
    {
        using var test = new TestDatabase();
        var products = Service(test);
        var id = Create(products, "Editable pack", patch =>
        {
            patch.BaseUnit = "injection";
            patch.StrengthValue = 50;
            patch.StrengthUnit = "ml";
            patch.PackSizeLabel = "  50   mL bottle  ";
            patch.BoxStrips = 2;
            patch.StripTablets = 5;
        });
        AddBatch(test, id, 17);

        var initial = products.Get(id).Value!;
        Assert.Equal("50 mL bottle", initial.PackSizeLabel);

        var unrelatedEdit = products.Update(TestData.Owner, id, new ProductPatch { RackNo = "R-9" });
        Assert.True(unrelatedEdit.Ok, unrelatedEdit.Error);
        Assert.Equal("50 mL bottle", products.Get(id).Value!.PackSizeLabel);

        var packEdit = products.Update(TestData.Owner, id,
            new ProductPatch { PackSizeLabel = "  100   mL bottle " });
        Assert.True(packEdit.Ok, packEdit.Error);
        var after = products.Get(id).Value!;
        Assert.Equal("100 mL bottle", after.PackSizeLabel);
        Assert.Equal(initial.Name, after.Name);
        Assert.Equal(initial.BaseUnit, after.BaseUnit);
        Assert.Equal(initial.BoxStrips, after.BoxStrips);
        Assert.Equal(initial.StripTablets, after.StripTablets);
        Assert.Equal(initial.CostPaisa, after.CostPaisa);
        Assert.Equal(initial.RetailPaisa, after.RetailPaisa);
        Assert.Equal(17, after.StockBase);
        Assert.Equal("R-9", after.RackNo);
    }

    [Fact]
    public void PackLabelValidationRefusesMultilineAndOverlongTextWithoutWriting()
    {
        using var test = new TestDatabase();
        var products = Service(test);
        var id = Create(products, "Validated pack", patch => patch.PackSizeLabel = "50 mL");

        var multiline = products.Update(TestData.Owner, id,
            new ProductPatch { PackSizeLabel = "50 mL\nsecond line" });
        Assert.False(multiline.Ok);
        Assert.Contains("one line", multiline.Error ?? string.Empty);
        Assert.Equal("50 mL", products.Get(id).Value!.PackSizeLabel);

        var overlong = products.Update(TestData.Owner, id,
            new ProductPatch { PackSizeLabel = new string('x', PackagingPolicy.MaxPackSizeLength + 1) });
        Assert.False(overlong.Ok);
        Assert.Contains("100 characters", overlong.Error ?? string.Empty);
        Assert.Equal("50 mL", products.Get(id).Value!.PackSizeLabel);

        var invalidCreate = products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Invalid new pack",
            BaseUnit = "tablet",
            PackSizeLabel = "box\nbottle",
            RetailPaisa = 200
        });
        Assert.False(invalidCreate.Ok);
        Assert.Empty(products.Search("Invalid new pack").Value!);
    }

    [Fact]
    public void PackSearchWithFlexibleSpacingFindsOnlyTheIntendedSeparateVariant()
    {
        using var test = new TestDatabase();
        var products = Service(test);
        var fifty = Create(products, "Variant injection", patch =>
        {
            patch.BaseUnit = "injection";
            patch.StrengthValue = 5;
            patch.StrengthUnit = "ml";
            patch.PackSizeLabel = "50 mL bottle";
        });
        var hundred = Create(products, "Variant injection", patch =>
        {
            patch.BaseUnit = "injection";
            patch.StrengthValue = 10;
            patch.StrengthUnit = "ml";
            patch.PackSizeLabel = "100 mL bottle";
        });
        AddBatch(test, fifty, 7);
        AddBatch(test, hundred, 11);

        var fiftyHits = products.Search("50ml").Value!;
        Assert.Equal(fifty, Assert.Single(fiftyHits).Id);
        Assert.Equal(7, fiftyHits[0].StockBase);

        var hundredHits = products.Search("100 ML").Value!;
        Assert.Equal(hundred, Assert.Single(hundredHits).Id);
        Assert.Equal(11, hundredHits[0].StockBase);
        Assert.NotEqual(fifty, hundred);
    }

    [Fact]
    public void ProductSearchIncludesPackBrandRoomCategoryAndCompanyMetadata()
    {
        using var test = new TestDatabase();
        var products = Service(test);
        var category = products.AddCategory(TestData.Owner, "Search syrups");
        var tag = products.AddCategory(TestData.Owner, "Search tag");
        var company = products.AddCompany(TestData.Owner, "Search ACME");
        Assert.True(category.Ok, category.Error);
        Assert.True(tag.Ok, tag.Error);
        Assert.True(company.Ok, company.Error);
        var id = Create(products, "Metadata finder", patch =>
        {
            patch.Brand = "Northstar";
            patch.Room = "Back almari";
            patch.PackSizeLabel = "Family bottle";
            patch.CategoryId = category.Value;
            patch.CategoryTagIds = new List<int> { tag.Value };
            patch.CompanyId = company.Value;
        });

        foreach (var term in new[]
                 { "northstar", "family bottle", "back almari", "search syrups", "search tag", "search acme" })
        {
            var result = products.Search(term);
            Assert.True(result.Ok, result.Error);
            Assert.Equal(id, Assert.Single(result.Value!).Id);
        }
    }

    [Fact]
    public void SharedDesktopSearchMatchesPackagingAndEveryExistingBarcode()
    {
        var product = new Product
        {
            Name = "Shared search",
            Generic = "Search salt",
            Brand = "Brand lane",
            Barcode = "BASE-1",
            RackNo = "Rack 7",
            Room = "Front room",
            CategoryName = "Syrups",
            CategoryTags = "Children, Sugar free",
            CompanyName = "Search company",
            BaseUnit = "injection",
            StrengthValue = 500,
            StrengthUnit = "mg",
            PackSizeLabel = "50 mL bottle"
        };

        foreach (var term in new[]
                 {
                     "50ml", "500mg", "brand lane", "rack 7", "front room", "sugar free",
                     "search company", "injection", "PACK-99"
                 })
        {
            Assert.True(PackagingPolicy.Matches(product, term, new[] { "PACK-99" }), term);
        }

        Assert.False(PackagingPolicy.Matches(product, "different variant", new[] { "PACK-99" }));
        Assert.True(PackagingPolicy.Matches(product, "  "));
    }

    [Fact]
    public void ProductChoiceLabelDistinguishesVariantsWithoutCreatingAnotherIdentity()
    {
        var fifty = new Product
        {
            Id = 41,
            Name = "Same medicine",
            StrengthValue = 50,
            StrengthUnit = "ml",
            PackSizeLabel = "1 x 10"
        };
        var hundred = new Product
        {
            Id = 42,
            Name = "Same medicine",
            StrengthValue = 100,
            StrengthUnit = "ml",
            PackSizeLabel = "1 x 20"
        };

        Assert.Equal("50 ml · 1 x 10", fifty.VariantLabel);
        Assert.Equal("Same medicine — 50 ml · 1 x 10", fifty.ProductChoiceLabel);
        Assert.NotEqual(fifty.ProductChoiceLabel, hundred.ProductChoiceLabel);
        Assert.NotEqual(fifty.Id, hundred.Id);
        Assert.Equal("Same medicine", fifty.ToString());
        Assert.Equal("Plain medicine", new Product { Name = "Plain medicine" }.ProductChoiceLabel);
    }

    [Fact]
    public void PackLabelEditDoesNotChangeSaleConversionPriceOrStock()
    {
        using var test = new TestDatabase();
        var products = Service(test);
        var id = Create(products, "Conversion guard", patch =>
        {
            patch.BaseUnit = "injection";
            patch.PackSizeLabel = "Before label";
            patch.BoxStrips = 2;
            patch.StripTablets = 5;
        });
        AddBatch(test, id, 20);
        var input = new SaleInput
        {
            Lines = new List<SaleLineInput>
            {
                new() { ProductId = id, Unit = "box", Quantity = 1 }
            }
        };

        var before = TestData.Sales(test).Quote(input, TestData.Owner);
        Assert.True(before.Ok, before.Error);
        Assert.Equal(10, before.Value!.Units);
        Assert.Equal(2000, before.Value.TotalPaisa);

        var edit = products.Update(TestData.Owner, id, new ProductPatch { PackSizeLabel = "After label" });
        Assert.True(edit.Ok, edit.Error);
        var after = TestData.Sales(test).Quote(input, TestData.Owner);
        Assert.True(after.Ok, after.Error);
        Assert.Equal(before.Value.Units, after.Value!.Units);
        Assert.Equal(before.Value.TotalPaisa, after.Value.TotalPaisa);
        Assert.Equal(20, products.Get(id).Value!.StockBase);
        Assert.Equal(10, products.Get(id).Value!.QuantityPerSelectedUnit("box"));
    }

    [Fact]
    public void SmartSearchUsesExactPackHitBeforeFuzzy_AndBlankSearchRemainsSafe()
    {
        using var test = new TestDatabase();
        var products = Service(test);
        var id = Create(products, "Smart pack", patch => patch.PackSizeLabel = "75 mL");
        Create(products, "Other medicine", patch => patch.PackSizeLabel = null);

        var exact = products.SearchSmart("75ml");
        Assert.True(exact.Ok, exact.Error);
        Assert.False(exact.Value!.IsFuzzy);
        Assert.Equal(id, Assert.Single(exact.Value.Items).Id);

        var blank = products.SearchSmart(null);
        Assert.True(blank.Ok, blank.Error);
        Assert.False(blank.Value!.IsFuzzy);
        Assert.Equal(2, blank.Value.Items.Count);
        Assert.True(PackagingPolicy.Matches(new Product { Name = "No pack" }, null));
    }

    private static ProductService Service(TestDatabase test) => new(test.Database, test.Audit, test.Log);

    private static int Create(
        ProductService products,
        string name,
        Action<ProductPatch>? configure = null)
    {
        var patch = new ProductPatch
        {
            Name = name,
            BaseUnit = "tablet",
            CostPaisa = 100,
            RetailPaisa = 200
        };
        configure?.Invoke(patch);
        var result = products.Create(TestData.Owner, patch);
        Assert.True(result.Ok, result.Error);
        return result.Value;
    }

    private static void AddBatch(TestDatabase test, int productId, double quantity)
    {
        test.Database.Tx(connection => test.Database.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    is_sample, active, created_at)
VALUES(@productId, 'PACK-SEARCH', '2027-06-30', '2027-06', @quantity, 100, 200, 0, 1, @at);",
            new { productId, quantity, at = Clock.Now() }));
    }
}
