using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>CAT-01 and UNIT-01: catalog convenience must not change the financial or stock rules.</summary>
public sealed class CatalogUnitTests
{
    [Fact]
    public void InjectionStrengthIsDescriptive_AndSaleUsesWholeBaseUnits()
    {
        using var test = new TestDatabase();
        var products = Service(test);
        var created = products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Injection 50 ml",
            BaseUnit = "injection",
            StrengthValue = 50,
            StrengthUnit = "ml",
            BoxStrips = 1,
            StripTablets = 10,
            CostPaisa = 100,
            RetailPaisa = 200
        });
        Assert.True(created.Ok, created.Error);
        var id = created.Value;
        AddBatch(test, id, 10, 100, 200);

        var product = products.Get(id).Value!;
        Assert.Equal("injection", product.BaseUnit);
        Assert.Equal(50, product.StrengthValue);
        Assert.Equal("ml", product.StrengthUnit);
        Assert.Equal("50 ml", product.StrengthLabel);
        Assert.Equal(10, product.QuantityPerSelectedUnit("box"));
        Assert.Equal(10, product.StockBase);

        var sale = TestData.Sales(test).Create(new SaleInput
        {
            PaymentMethod = "cash",
            TenderedPaisa = 200,
            Lines = new List<SaleLineInput>
            {
                new() { ProductId = id, Unit = "base", Quantity = 1 }
            }
        }, TestData.Owner);

        Assert.True(sale.Ok, sale.Error);
        Assert.Equal(200, sale.Value!.TotalPaisa);
        Assert.Equal(9, products.Get(id).Value!.StockBase);
        Assert.Equal(1, test.Database.Tx(connection => Convert.ToInt32(test.Database.Scalar(connection,
            "SELECT qty_base FROM sale_items WHERE product_id = @id;", new { id }) ?? 0)));

        // ml is a label on the variant, not an alternate selling unit that can divide an injection.
        var invalidUnit = TestData.Sales(test).Quote(new SaleInput
        {
            Lines = new List<SaleLineInput>
            {
                new() { ProductId = id, Unit = "ml", Quantity = 1 }
            }
        }, TestData.Owner);
        Assert.False(invalidUnit.Ok);

        var fractional = TestData.Sales(test).Quote(new SaleInput
        {
            Lines = new List<SaleLineInput>
            {
                new() { ProductId = id, Unit = "base", Quantity = 0.5 }
            }
        }, TestData.Owner);
        Assert.False(fractional.Ok);
        Assert.Contains("whole injection", fractional.Error ?? string.Empty);

        // The rule belongs to the managed unit row, not to the editable spelling "injection".
        var injectionUnit = products.GetUnits().Value!.Single(row => row.Name == "injection");
        Assert.True(injectionUnit.WholeOnly);
        Assert.True(products.UpdateUnit(TestData.Owner, injectionUnit.Id, "ampoule", injectionUnit.SortOrder).Ok);
        var renamedFractional = TestData.Sales(test).Quote(new SaleInput
        {
            Lines = new List<SaleLineInput>
            {
                new() { ProductId = id, Unit = "base", Quantity = 0.5 }
            }
        }, TestData.Owner);
        Assert.False(renamedFractional.Ok);
        Assert.Contains("whole ampoule", renamedFractional.Error ?? string.Empty);
    }

    [Fact]
    public void AdditionalCategoryTagsFilterOnce_ButFinancialTotalsUsePrimaryOnly()
    {
        using var test = new TestDatabase();
        var products = Service(test);
        var primary = products.AddCategory(TestData.Owner, "Primary financial").Value;
        var tag = products.AddCategory(TestData.Owner, "Filter tag").Value;
        var first = Create(products, "Tagged medicine", primary, tag);
        var second = Create(products, "Primary tag medicine", tag, configure: patch => patch.RetailPaisa = 300);
        AddBatch(test, first, 10, 100, 200);
        AddBatch(test, second, 10, 100, 300);

        var filtered = products.Search(categoryId: tag);
        Assert.True(filtered.Ok, filtered.Error);
        Assert.Equal(new[] { first, second }, filtered.Value!.Select(row => row.Id).OrderBy(id => id));
        Assert.Equal(1, filtered.Value.Count(row => row.Id == first));
        Assert.Equal(2, products.GetCategories().Value!.Single(row => row.Id == tag).ProductCount);
        Assert.False(products.Update(TestData.Owner, first, new ProductPatch { CategoryId = tag }).Ok);
        Assert.Equal(primary, products.Get(first).Value!.CategoryId);

        var sale = TestData.Sales(test).Create(new SaleInput
        {
            PaymentMethod = "cash",
            TenderedPaisa = 1000,
            Lines = new List<SaleLineInput>
            {
                new() { ProductId = first, Unit = "base", Quantity = 1 },
                new() { ProductId = second, Unit = "base", Quantity = 1 }
            }
        }, TestData.Owner);
        Assert.True(sale.Ok, sale.Error);

        var report = TestData.Reports(test).Build(ReportRange.Today, TestData.Owner);
        Assert.True(report.Ok, report.Error);
        Assert.Equal(sale.Value!.TotalPaisa, report.Value!.Categories.Sum(row => row.SalesPaisa));
        Assert.Equal(200, report.Value.Categories.Single(row => row.Name == "Primary financial").SalesPaisa);
        Assert.Equal(300, report.Value.Categories.Single(row => row.Name == "Filter tag").SalesPaisa);
    }

    [Fact]
    public void UnitRenamePropagatesCurrentLabels_AndPutAwayRefusesUsage()
    {
        using var test = new TestDatabase();
        var products = Service(test);
        var unit = products.AddUnit(TestData.Owner, "vial", 15, wholeOnly: true).Value;
        var id = Create(products, "Vial medicine", null, null, patch =>
        {
            patch.BaseUnit = "vial";
            patch.StrengthValue = 50;
            patch.StrengthUnit = "ml";
        });
        AddBatch(test, id, 7, 100, 200);

        var renamed = products.UpdateUnit(TestData.Owner, unit, "  AMPoule  ", 25);
        Assert.True(renamed.Ok, renamed.Error);
        var after = products.Get(id).Value!;
        Assert.Equal("ampoule", after.BaseUnit);
        Assert.Equal("ml", after.StrengthUnit);
        Assert.Equal(7, after.StockBase);
        var renamedUnit = products.GetUnits(activeOnly: false).Value!.Single(row => row.Id == unit);
        Assert.Equal(25, renamedUnit.SortOrder);
        Assert.True(renamedUnit.WholeOnly);

        Assert.False(products.SetUnitActive(TestData.Owner, unit, false).Ok);
        Assert.False(products.AddUnit(TestData.Owner, "AMPOULE").Ok);
        Assert.False(products.AddUnit(TestData.Cashier(test), "not allowed").Ok);

        var cleared = products.Update(TestData.Owner, id, new ProductPatch
        {
            BaseUnit = "tablet",
            StrengthValue = 0,
            StrengthUnit = string.Empty
        });
        Assert.True(cleared.Ok, cleared.Error);
        Assert.Equal(0, products.Get(id).Value!.StrengthValue);
        Assert.True(products.SetUnitActive(TestData.Owner, unit, false).Ok);
        Assert.False(products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Inactive unit product",
            BaseUnit = "ampoule",
            CostPaisa = 100,
            RetailPaisa = 200
        }).Ok);
    }

    [Fact]
    public void StrengthVariantsStaySeparate_AndStrengthFilterIsExact()
    {
        using var test = new TestDatabase();
        var products = Service(test);
        var ten = Create(products, "Same injection 10", null, null, patch =>
        {
            patch.BaseUnit = "injection";
            patch.StrengthValue = 10;
            patch.StrengthUnit = "ml";
        });
        var fifty = Create(products, "Same injection 50", null, null, patch =>
        {
            patch.BaseUnit = "injection";
            patch.StrengthValue = 50;
            patch.StrengthUnit = "ml";
        });
        var hundred = Create(products, "Same injection 100", null, null, patch =>
        {
            patch.BaseUnit = "injection";
            patch.StrengthValue = 100;
            patch.StrengthUnit = "ml";
        });

        var exact = products.Search(strength: " 50 ML ");
        Assert.True(exact.Ok, exact.Error);
        Assert.Equal(fifty, Assert.Single(exact.Value!).Id);
        Assert.Equal(3, products.Search(strength: "ml").Value!.Count);
        Assert.Equal(3, new[] { ten, fifty, hundred }.Distinct().Count());
        Assert.Equal(0, products.Get(ten).Value!.StockBase);
        Assert.Equal(0, products.Get(fifty).Value!.StockBase);
        Assert.Equal(0, products.Get(hundred).Value!.StockBase);
    }

    private static ProductService Service(TestDatabase test) => new(test.Database, test.Audit, test.Log);

    private static int Create(
        ProductService service,
        string name,
        int? categoryId,
        int? tagId = null,
        Action<ProductPatch>? configure = null)
    {
        var patch = new ProductPatch
        {
            Name = name,
            BaseUnit = "tablet",
            CategoryId = categoryId,
            CategoryTagIds = tagId is null ? null : new List<int> { tagId.Value },
            CostPaisa = 100,
            RetailPaisa = 200
        };
        configure?.Invoke(patch);
        var result = service.Create(TestData.Owner, patch);
        Assert.True(result.Ok, result.Error);
        return result.Value;
    }

    private static void AddBatch(TestDatabase test, int productId, double quantity, long cost, long retail)
    {
        test.Database.Tx(connection => test.Database.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    is_sample, active, created_at)
VALUES(@productId, 'CAT-UNIT', '2027-06-30', '2027-06', @quantity, @cost, @retail, 0, 1, @at);",
            new { productId, quantity, cost, retail, at = Clock.Now() }));
    }
}
