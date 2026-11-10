using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Portion 8: managed salts and brands. Central catalogs with one canonical spelling each,
/// explicit product links that are never guessed from typed text, rename propagation to linked
/// rows only, and put-away that unlinks but keeps every typed word.
/// </summary>
public sealed class SaltBrandTests
{
    private static ProductService Products(TestDatabase test) => new(test.Database, test.Audit, test.Log);

    private static int AddSalt(ProductService products, string name)
    {
        var added = products.AddFormula(TestData.Owner, name);
        Assert.True(added.Ok, added.Error);
        return added.Value;
    }

    private static int AddBrand(ProductService products, string name)
    {
        var added = products.AddBrand(TestData.Owner, name);
        Assert.True(added.Ok, added.Error);
        return added.Value;
    }

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
    public void AddSalt_ListsItWithZeroUsage()
    {
        using var test = new TestDatabase();
        var products = Products(test);

        var id = AddSalt(products, "Paracetamol");

        var list = products.GetFormulas().Value!;
        var row = Assert.Single(list);
        Assert.Equal(id, row.Id);
        Assert.Equal("Paracetamol", row.Name);
        Assert.Equal(0, row.ProductCount);
        Assert.Equal("Active", row.ActiveLabel);
        Assert.Single(products.GetFormulas(activeOnly: false).Value!);
    }

    [Fact]
    public void DuplicateSalt_Refused_IncludingPutAwayAndCaseVariants()
    {
        using var test = new TestDatabase();
        var products = Products(test);

        var id = AddSalt(products, "Paracetamol");

        var spaced = products.AddFormula(TestData.Owner, "  paracetamol ");
        Assert.False(spaced.Ok);
        Assert.Contains("already", spaced.Error!);

        Assert.True(products.SetFormulaActive(TestData.Owner, id, false).Ok);

        var afterPutAway = products.AddFormula(TestData.Owner, "PARACETAMOL");
        Assert.False(afterPutAway.Ok);
        Assert.Contains("already", afterPutAway.Error!);
    }

    [Fact]
    public void NonManager_CannotChangeSaltsOrBrands()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var cashier = TestData.Cashier(test);
        var salt = AddSalt(products, "Paracetamol");
        var brand = AddBrand(products, "Acme");

        foreach (var result in new ServiceResult[]
                 {
                     products.AddFormula(cashier, "Ibuprofen"),
                     products.UpdateFormula(cashier, salt, "Acetaminophen", 0),
                     products.SetFormulaActive(cashier, salt, false),
                     products.AddBrand(cashier, "Globus"),
                     products.UpdateBrand(cashier, brand, "Globus", 0),
                     products.SetBrandActive(cashier, brand, false)
                 })
        {
            Assert.False(result.Ok);
            Assert.Contains("owner or a manager", result.Error!);
        }
    }

    [Fact]
    public void CreateProduct_LinkedToSaltAndBrand()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var salt = AddSalt(products, "Paracetamol");
        var brand = AddBrand(products, "Acme");

        var id = CreateProduct(products, "Linked tablet", new ProductPatch
        {
            Generic = "Paracetamol",
            Brand = "Acme",
            GenericFormulaId = salt,
            BrandId = brand
        });

        var stored = products.Get(id).Value!;
        Assert.Equal(salt, stored.GenericFormulaId);
        Assert.Equal(brand, stored.BrandId);
        Assert.Equal("Paracetamol", stored.Generic);
        Assert.Equal("Acme", stored.Brand);
        Assert.Equal(1, products.GetFormulas().Value![0].ProductCount);
        Assert.Equal(1, products.GetBrands().Value![0].ProductCount);
    }

    [Fact]
    public void LinkFillsEmptyText_FromTheCatalog()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var salt = AddSalt(products, "Paracetamol");
        var brand = AddBrand(products, "Acme");

        var id = CreateProduct(products, "Filled tablet", new ProductPatch
        {
            GenericFormulaId = salt,
            BrandId = brand
        });

        var stored = products.Get(id).Value!;
        Assert.Equal("Paracetamol", stored.Generic);
        Assert.Equal("Acme", stored.Brand);
    }

    [Fact]
    public void LinkWithDifferentText_IsRefused_NothingCreated()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var salt = AddSalt(products, "Paracetamol");

        var refused = products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Confused tablet",
            Generic = "Ibuprofen",
            GenericFormulaId = salt,
            CostPaisa = 100,
            RetailPaisa = 200
        });

        Assert.False(refused.Ok);
        Assert.Contains("does not match", refused.Error!);
        Assert.Empty(products.Search("Confused").Value!);
    }

    [Fact]
    public void UnknownOrPutAwayLink_IsRefused()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var salt = AddSalt(products, "Paracetamol");
        Assert.True(products.SetFormulaActive(TestData.Owner, salt, false).Ok);

        var unknown = products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Ghost link",
            GenericFormulaId = 999,
            CostPaisa = 100,
            RetailPaisa = 200
        });
        Assert.False(unknown.Ok);

        var putAway = products.Create(TestData.Owner, new ProductPatch
        {
            Name = "Put-away link",
            GenericFormulaId = salt,
            CostPaisa = 100,
            RetailPaisa = 200
        });
        Assert.False(putAway.Ok);
        Assert.Contains("active list", putAway.Error!);
    }

    [Fact]
    public void LegacyTextMatchingSalt_StaysUnlinkedAndSearchable()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        AddSalt(products, "Paracetamol");

        var id = CreateProduct(products, "Legacy tablet", new ProductPatch { Generic = "Paracetamol" });

        var stored = products.Get(id).Value!;
        Assert.Null(stored.GenericFormulaId);
        Assert.Equal("Paracetamol", stored.Generic);
        Assert.Equal(id, Assert.Single(products.Search("paracetamol").Value!).Id);
    }

    [Fact]
    public void Rename_RelabelsLinkedOnly_LegacyTextUntouched()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var salt = AddSalt(products, "Paracetamol");
        var linked = CreateProduct(products, "Linked relabel", new ProductPatch
        {
            GenericFormulaId = salt
        });
        var legacy = CreateProduct(products, "Legacy relabel", new ProductPatch { Generic = "Paracetamol" });

        var renamed = products.UpdateFormula(TestData.Owner, salt, "Acetaminophen", 0);
        Assert.True(renamed.Ok, renamed.Error);

        var linkedRow = products.Get(linked).Value!;
        Assert.Equal(salt, linkedRow.GenericFormulaId);
        Assert.Equal("Acetaminophen", linkedRow.Generic);

        var legacyRow = products.Get(legacy).Value!;
        Assert.Null(legacyRow.GenericFormulaId);
        Assert.Equal("Paracetamol", legacyRow.Generic);
    }

    [Fact]
    public void RenameToExisting_IsRefused()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        AddSalt(products, "Paracetamol");
        var other = AddSalt(products, "Ibuprofen");

        var refused = products.UpdateFormula(TestData.Owner, other, "paracetamol", 0);

        Assert.False(refused.Ok);
        Assert.Contains("already", refused.Error!);
    }

    [Fact]
    public void PutAway_UnlinksButKeepsText()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var salt = AddSalt(products, "Paracetamol");
        var id = CreateProduct(products, "Unlinked tablet", new ProductPatch { GenericFormulaId = salt });

        var putAway = products.SetFormulaActive(TestData.Owner, salt, false);
        Assert.True(putAway.Ok, putAway.Error);

        var stored = products.Get(id).Value!;
        Assert.Null(stored.GenericFormulaId);
        Assert.Equal("Paracetamol", stored.Generic);
        Assert.Empty(products.GetFormulas().Value!);
        Assert.Equal("Put away", products.GetFormulas(activeOnly: false).Value![0].ActiveLabel);
    }

    [Fact]
    public void BringBack_DoesNotRelink()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var salt = AddSalt(products, "Paracetamol");
        var id = CreateProduct(products, "Relink tablet", new ProductPatch { GenericFormulaId = salt });
        Assert.True(products.SetFormulaActive(TestData.Owner, salt, false).Ok);

        var back = products.SetFormulaActive(TestData.Owner, salt, true);
        Assert.True(back.Ok, back.Error);

        var stored = products.Get(id).Value!;
        Assert.Null(stored.GenericFormulaId);
        Assert.Equal("Paracetamol", stored.Generic);
    }

    [Fact]
    public void UnlinkViaClear_KeepsText()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var salt = AddSalt(products, "Paracetamol");
        var brand = AddBrand(products, "Acme");
        var id = CreateProduct(products, "Cleared tablet", new ProductPatch
        {
            GenericFormulaId = salt,
            BrandId = brand
        });

        var cleared = products.Update(TestData.Owner, id, new ProductPatch
        {
            ClearGenericFormula = true,
            ClearBrand = true
        });
        Assert.True(cleared.Ok, cleared.Error);

        var stored = products.Get(id).Value!;
        Assert.Null(stored.GenericFormulaId);
        Assert.Null(stored.BrandId);
        Assert.Equal("Paracetamol", stored.Generic);
        Assert.Equal("Acme", stored.Brand);
    }

    [Fact]
    public void BrandFullCycle_AddDupLinkRenamePutAway()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var brand = AddBrand(products, "Acme");

        var dup = products.AddBrand(TestData.Owner, "acme");
        Assert.False(dup.Ok);
        Assert.Contains("already", dup.Error!);

        var id = CreateProduct(products, "Branded tablet", new ProductPatch { BrandId = brand });
        Assert.Equal("Acme", products.Get(id).Value!.Brand);

        Assert.True(products.UpdateBrand(TestData.Owner, brand, "Acme Labs", 0).Ok);
        var relabelled = products.Get(id).Value!;
        Assert.Equal(brand, relabelled.BrandId);
        Assert.Equal("Acme Labs", relabelled.Brand);

        Assert.True(products.SetBrandActive(TestData.Owner, brand, false).Ok);
        var unlinked = products.Get(id).Value!;
        Assert.Null(unlinked.BrandId);
        Assert.Equal("Acme Labs", unlinked.Brand);
    }

    [Fact]
    public void SearchFindsLinkedProduct_BySaltAndBrandText()
    {
        using var test = new TestDatabase();
        var products = Products(test);
        var salt = AddSalt(products, "Paracetamol");
        var brand = AddBrand(products, "Acme");
        var id = CreateProduct(products, "Searchable tablet", new ProductPatch
        {
            GenericFormulaId = salt,
            BrandId = brand
        });

        Assert.Equal(id, Assert.Single(products.Search("paracetamol").Value!).Id);
        Assert.Equal(id, Assert.Single(products.Search("acme").Value!).Id);
    }
}
