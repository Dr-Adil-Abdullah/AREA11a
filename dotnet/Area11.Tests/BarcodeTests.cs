using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Several barcodes per medicine (F-43, Spec 8.1.5, 35.6, R-09).
///
/// The promises this file pins down:
///   1. one medicine can carry a code for the strip and another for the box, and the **scan says
///      which pack it was** - a box scan must add a box;
///   2. the same digits can never point at two medicines - a wrong pick at the counter is a wrong
///      bill, and the customer is standing there;
///   3. the old single `products.barcode` column still scans, because existing databases carry it;
///   4. **nothing is deleted** (D-00b): a code is put away, stays in the list, and can be given to a
///      new box later - but not while it is live elsewhere;
///   5. the cashier may scan and may not edit the codes.
/// </summary>
public sealed class BarcodeTests
{
    private const string BoxCode = "8901234567890";

    private const string StripCode = "8901234567891";

    // ============================== the list ==============================

    [Fact]
    public void ACodeBelongsToAPackAndTheScanSaysWhich()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);
        var product = TestData.AddProduct(test, name: "Panadol 500mg");

        var added = barcodes.Add(TestData.Owner, product, BoxCode, BarcodePack.Box, "the big carton");

        Assert.True(added.Ok, added.Error);
        Assert.Equal("box", added.Value!.PackLabel);

        var found = barcodes.Find(TestData.Owner, BoxCode);

        Assert.True(found.Ok, found.Error);
        Assert.Equal(product, found.Value!.ProductId);
        Assert.Equal("Panadol 500mg · box", found.Value.Caption);

        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'barcode_add';"));
    }

    [Fact]
    public void OneMedicineCanCarryThreeCodes()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);
        var product = TestData.AddProduct(test);

        Assert.True(barcodes.Add(TestData.Owner, product, "1111111111111", BarcodePack.Base, null).Ok);
        Assert.True(barcodes.Add(TestData.Owner, product, StripCode, BarcodePack.Strip, null).Ok);
        Assert.True(barcodes.Add(TestData.Owner, product, BoxCode, BarcodePack.Box, null).Ok);

        var list = barcodes.List(TestData.Owner, product);

        Assert.True(list.Ok, list.Error);
        Assert.Equal(3, list.Value!.Count);
        Assert.Equal(3, barcodes.All(TestData.Owner).Value!.Count);
    }

    [Fact]
    public void ABadCodeOrPackIsRefusedBeforeAnythingIsWritten()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);
        var product = TestData.AddProduct(test);

        Assert.False(barcodes.Add(TestData.Owner, product, "12", BarcodePack.Base, null).Ok);
        Assert.False(barcodes.Add(TestData.Owner, product, "1234 5678", BarcodePack.Base, null).Ok);
        Assert.False(barcodes.Add(TestData.Owner, product, BoxCode, "carton", null).Ok);

        Assert.Equal(0L, Rows(test, "SELECT COUNT(1) FROM product_barcodes;"));
    }

    [Fact]
    public void ACodeCannotBelongToTwoMedicines()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);

        var first = TestData.AddProduct(test, name: "Panadol 500mg");
        var second = TestData.AddProduct(test, name: "Disprin 300mg");

        Assert.True(barcodes.Add(TestData.Owner, first, BoxCode, BarcodePack.Box, null).Ok);

        var twice = barcodes.Add(TestData.Owner, second, BoxCode, BarcodePack.Box, null);

        Assert.False(twice.Ok);
        Assert.Contains("already", twice.Error);
        Assert.Equal(first, barcodes.Find(TestData.Owner, BoxCode).Value!.ProductId);
    }

    [Fact]
    public void AMedicineThatIsGoneCannotTakeACode()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);

        Assert.False(barcodes.Add(TestData.Owner, 9999, BoxCode, BarcodePack.Box, null).Ok);
        Assert.Equal(0L, Rows(test, "SELECT COUNT(1) FROM product_barcodes;"));
    }

    // ============================== the old single column ==============================

    [Fact]
    public void TheOldSingleBarcodeStillScans()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);
        var product = TestData.AddProduct(test, name: "Brufen 400mg");

        SetLegacyBarcode(test, product, "5012345678900");

        var found = barcodes.Find(TestData.Owner, "5012345678900");

        Assert.True(found.Ok, found.Error);
        Assert.Equal("base", found.Value!.Pack);
        Assert.Equal(product, found.Value.ProductId);

        // It is one more live code, so the counter's filter can see it too.
        Assert.Contains(barcodes.All(TestData.Owner).Value!, match => match.ProductId == product);

        // And it cannot be typed again into the new list: one code, one meaning.
        Assert.False(barcodes.Add(TestData.Owner, product, "5012345678900", BarcodePack.Base, null).Ok);
    }

    // ============================== nothing is deleted ==============================

    [Fact]
    public void APutAwayCodeStopsScanningAndStaysInTheList()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);
        var product = TestData.AddProduct(test);

        var added = barcodes.Add(TestData.Owner, product, BoxCode, BarcodePack.Box, null);
        var id = added.Value!.Id;

        Assert.True(barcodes.SetActive(TestData.Owner, id, false).Ok);

        Assert.False(barcodes.Find(TestData.Owner, BoxCode).Ok);
        Assert.Empty(barcodes.List(TestData.Owner, product).Value!);
        Assert.Single(barcodes.List(TestData.Owner, product, includeInactive: true).Value!);
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM product_barcodes;"));
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'barcode_off';"));

        Assert.True(barcodes.SetActive(TestData.Owner, id, true).Ok);
        Assert.True(barcodes.Find(TestData.Owner, BoxCode).Ok);
    }

    [Fact]
    public void APutAwayCodeMayBeGivenToANewBoxButNotToTwoAtOnce()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);

        var oldProduct = TestData.AddProduct(test, name: "Old box");
        var newProduct = TestData.AddProduct(test, name: "New box");

        var first = barcodes.Add(TestData.Owner, oldProduct, BoxCode, BarcodePack.Box, null);
        Assert.True(barcodes.SetActive(TestData.Owner, first.Value!.Id, false).Ok);

        // The box is gone from the shelf; the same digits may now belong to the new box.
        var moved = barcodes.Add(TestData.Owner, newProduct, BoxCode, BarcodePack.Box, null);
        Assert.True(moved.Ok, moved.Error);

        // The old row can stay in the history, but it cannot be switched back on beside the new one.
        var restore = barcodes.SetActive(TestData.Owner, first.Value.Id, true);

        Assert.False(restore.Ok);
        Assert.Contains("already in use", restore.Error);
        Assert.Equal(newProduct, barcodes.Find(TestData.Owner, BoxCode).Value!.ProductId);
    }

    [Fact]
    public void ACodeCopiedFromTheOldColumnCanGoAwayAndComeBack()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);
        var product = TestData.AddProduct(test, name: "Brufen 400mg");

        SetLegacyBarcode(test, product, "5012345678900");

        // Migration 022 copies the old column into the table; an existing shop therefore has the same
        // digits in both places, and switching the copied row back on must not see its own twin.
        test.Database.Tx(connection => test.Database.Execute(connection, @"
INSERT INTO product_barcodes(product_id, barcode, pack, note, active, created_at)
VALUES(@product, '5012345678900', 'base', 'copied from the old single barcode', 1, '2026-10-22 10:00:00');",
            new { product }));

        var id = (int)Rows(test, "SELECT id FROM product_barcodes;");

        Assert.True(barcodes.SetActive(TestData.Owner, id, false).Ok);
        Assert.True(barcodes.SetActive(TestData.Owner, id, true).Ok);
        Assert.True(barcodes.Find(TestData.Owner, "5012345678900").Ok);
        Assert.Equal(1L, Rows(test, "SELECT COUNT(1) FROM audit_logs WHERE action = 'barcode_on';"));
    }

    // ============================== who may do what ==============================

    [Fact]
    public void TheCashierCanScanButNotEditCodes()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);
        var cashier = TestData.Cashier(test);
        var product = TestData.AddProduct(test);

        Assert.True(barcodes.Add(TestData.Owner, product, BoxCode, BarcodePack.Box, null).Ok);

        Assert.True(barcodes.Find(cashier, BoxCode).Ok);
        Assert.True(barcodes.All(cashier).Ok);

        Assert.False(barcodes.Add(cashier, product, StripCode, BarcodePack.Strip, null).Ok);
        Assert.False(barcodes.List(cashier, product).Ok);
    }

    // ============================== the product search ==============================

    [Fact]
    public void TheProductsPageFindsAMedicineByItsSecondCode()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);
        var products = new ProductService(test.Database, test.Audit, test.Log);

        var product = TestData.AddProduct(test, name: "Augmentin 625mg");
        TestData.AddProduct(test, name: "Calpol 120mg");

        Assert.True(barcodes.Add(TestData.Owner, product, StripCode, BarcodePack.Strip, null).Ok);

        var found = products.Search(StripCode);

        Assert.True(found.Ok, found.Error);
        Assert.Single(found.Value!);
        Assert.Equal(product, found.Value![0].Id);
    }

    [Fact]
    public void AnInactiveCodeIsNotShownByTheProductsPageSearch()
    {
        using var test = new TestDatabase();
        var barcodes = Barcodes(test);
        var products = new ProductService(test.Database, test.Audit, test.Log);

        var product = TestData.AddProduct(test, name: "Augmentin 625mg");
        var added = barcodes.Add(TestData.Owner, product, StripCode, BarcodePack.Strip, null);

        Assert.True(barcodes.SetActive(TestData.Owner, added.Value!.Id, false).Ok);
        Assert.Empty(products.Search(StripCode).Value!);
    }

    // ============================== helpers ==============================

    private static BarcodeService Barcodes(TestDatabase test) =>
        new(test.Database, test.Audit, TestData.Permissions(test), test.Log);

    /// <summary>The one column migration 001 wrote - an existing shop still carries it.</summary>
    private static void SetLegacyBarcode(TestDatabase test, int productId, string code)
    {
        test.Database.Tx(connection => test.Database.Execute(connection,
            "UPDATE products SET barcode = @code WHERE id = @id;", new { code, id = productId }));
    }

    private static long Rows(TestDatabase test, string sql)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection, sql) ?? 0);
    }
}
