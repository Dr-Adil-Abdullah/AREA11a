using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Portion 10: one tokenized keyword-plus-unit search everywhere. Every word must land in some
/// field (AND), in any order — "panadol 100ml" narrows to the 100ml variant on Products, the
/// Counter and Purchases alike, because all three share the same tokenizer and matcher.
/// </summary>
public sealed class TokenSearchTests
{
    private static ProductService Products(TestDatabase test) => new(test.Database, test.Audit, test.Log);

    private static int CreateProduct(ProductService products, string name, ProductPatch? patch = null)
    {
        patch ??= new ProductPatch();
        patch.Name = name;
        patch.CostPaisa ??= 100;
        patch.RetailPaisa ??= 200;
        var created = products.Create(TestData.Owner, patch);
        Assert.True(created.Ok, created.Error);
        return created.Value;
    }

    [Fact]
    public void TwoTokensAcrossFields_Match()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = CreateProduct(products, "Panadol", new ProductPatch { PackSizeLabel = "100ml bottle" });
        CreateProduct(products, "Disprin");

        var found = products.Search("panadol 100ml").Value!;

        Assert.Equal(id, Assert.Single(found).Id);
    }

    [Fact]
    public void TokenOrderDoesNotMatter()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = CreateProduct(products, "Panadol", new ProductPatch { PackSizeLabel = "100ml bottle" });

        var found = products.Search("100ml panadol").Value!;

        Assert.Equal(id, Assert.Single(found).Id);
    }

    [Fact]
    public void SplitUnitTokens_MatchStrengthOnly()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var fifty = CreateProduct(products, "Iron injection",
            new ProductPatch { StrengthValue = 50, StrengthUnit = "ml" });
        CreateProduct(products, "Iron syrup",
            new ProductPatch { StrengthValue = 100, StrengthUnit = "ml" });

        var found = products.Search("50 ml").Value!;

        Assert.Equal(fifty, Assert.Single(found).Id);
    }

    [Fact]
    public void SingleToken_StaysNormalized()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = CreateProduct(products, "Iron injection",
            new ProductPatch { StrengthValue = 50, StrengthUnit = "ml" });

        var found = products.Search("50ml").Value!;

        Assert.Equal(id, Assert.Single(found).Id);
    }

    [Fact]
    public void VariantIdentification_PicksOneOfTwo()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        CreateProduct(products, "Para 50", new ProductPatch
        {
            Generic = "Paracetamol",
            StrengthValue = 50,
            StrengthUnit = "ml"
        });
        var hundred = CreateProduct(products, "Para 100", new ProductPatch
        {
            Generic = "Paracetamol",
            StrengthValue = 100,
            StrengthUnit = "ml"
        });

        var found = products.Search("paracetamol 100ml").Value!;

        Assert.Equal(hundred, Assert.Single(found).Id);
    }

    [Fact]
    public void SecondToken_Excludes()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        CreateProduct(products, "Panadol", new ProductPatch { PackSizeLabel = "100ml bottle" });

        Assert.Empty(products.Search("panadol 50ml").Value!);
    }

    [Fact]
    public void Tokens_CombineWithCategoryFilter()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var syrups = products.AddCategory(TestData.Owner, "Syrups").Value;
        var tablets = products.AddCategory(TestData.Owner, "Tablets").Value;
        var id = CreateProduct(products, "Panadol", new ProductPatch
        {
            PackSizeLabel = "100ml bottle",
            CategoryId = syrups
        });
        CreateProduct(products, "Panadol Extra", new ProductPatch
        {
            PackSizeLabel = "100ml bottle",
            CategoryId = tablets
        });

        Assert.Equal(id, Assert.Single(products.Search("panadol 100ml", syrups).Value!).Id);
        Assert.Empty(products.Search("panadol 50ml", syrups).Value!);
    }

    [Fact]
    public void Whitespace_Collapses()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = CreateProduct(products, "Panadol", new ProductPatch { PackSizeLabel = "100ml bottle" });

        var found = products.Search("  panadol   100ml  ").Value!;

        Assert.Equal(id, Assert.Single(found).Id);
    }

    [Fact]
    public void EmptyTerm_ReturnsAll()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        CreateProduct(products, "Panadol");
        CreateProduct(products, "Disprin");

        Assert.Equal(2, products.Search("   ").Value!.Count);
    }

    [Fact]
    public void Matches_AllTokensAcrossFields()
    {
        var product = new Product { Name = "Panadol", PackSizeLabel = "100ml bottle" };

        Assert.True(PackagingPolicy.Matches(product, "panadol 100ml"));
        Assert.True(PackagingPolicy.Matches(product, "100ml panadol"));
        Assert.False(PackagingPolicy.Matches(product, "panadol 50ml"));
        Assert.False(PackagingPolicy.Matches(product, "disprin 100ml"));
    }

    [Fact]
    public void Matches_SplitUnitAndSingleWord()
    {
        var product = new Product { Name = "Iron injection", StrengthValue = 50, StrengthUnit = "ml" };

        Assert.True(PackagingPolicy.Matches(product, "50 ml"));
        Assert.True(PackagingPolicy.Matches(product, "50ml"));
        Assert.True(PackagingPolicy.Matches(product, "iron"));
        Assert.False(PackagingPolicy.Matches(product, "100 ml"));
    }

    [Fact]
    public void Matches_EmptyTrue_BarcodeTokenCounts()
    {
        var product = new Product { Name = "Panadol" };

        Assert.True(PackagingPolicy.Matches(product, null));
        Assert.True(PackagingPolicy.Matches(product, "   "));
        Assert.False(PackagingPolicy.Matches(product, "panadol 789"));
        Assert.True(PackagingPolicy.Matches(product, "panadol 789", new[] { "600-789-001" }));
    }

    [Fact]
    public void Tokens_SplitCollapseAndCap()
    {
        Assert.Equal(new[] { "panadol", "100ml" }, PackagingPolicy.Tokens("  panadol   100ml  "));
        Assert.Empty(PackagingPolicy.Tokens(null));
        Assert.Empty(PackagingPolicy.Tokens("   "));

        var many = PackagingPolicy.Tokens("w1 w2 w3 w4 w5 w6 w7 w8 w9 w10 w11 w12");
        Assert.Equal(PackagingPolicy.MaxSearchTokens, many.Length);
        Assert.Equal("w10", many[^1]);
    }

    [Fact]
    public void Tokens_MatchGenericAndBrand()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = CreateProduct(products, "Pain relief", new ProductPatch
        {
            Generic = "Paracetamol",
            Brand = "Acme"
        });
        CreateProduct(products, "Other relief", new ProductPatch { Generic = "Paracetamol", Brand = "Globus" });

        var found = products.Search("paracetamol acme").Value!;

        Assert.Equal(id, Assert.Single(found).Id);
    }

    [Fact]
    public void SearchSmart_MultiWord_ExactHitWithoutGuessing()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var id = CreateProduct(products, "Panadol", new ProductPatch { PackSizeLabel = "100ml bottle" });
        CreateProduct(products, "Disprin");

        var answer = products.SearchSmart("panadol 100ml").Value!;

        Assert.False(answer.IsFuzzy);
        Assert.Equal(id, Assert.Single(answer.Items).Id);
    }
}
