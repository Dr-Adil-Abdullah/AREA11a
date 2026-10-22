using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Excel / CSV import, export and bulk price change (F-37, Spec 24). The promises:
///
///   • a file is **read before it is written**: the preview lists every line and writes nothing;
///   • **one bad line means nothing is written** - a half-imported stock list cannot be explained;
///   • importing the same file twice adds nothing - a duplicate is skipped, never overwritten;
///   • opening stock arrives as a **real batch with an expiry** and a movement, so the shelf, the
///     expiry screen and the ledger agree;
///   • opening **udhaar** is part of the khata's maths, so "Books match" is true on day one;
///   • a price change never writes a retail price below cost.
///
/// The reader is tested on the four things Excel actually does: a BOM, CRLF, quotes around a value
/// with a comma or a line break, and a last line with no ending.
/// </summary>
public sealed class ImportTests
{
    // ============================== the reader ==============================

    [Fact]
    public void TheReader_SplitsQuotedCommasQuotesAndLineBreaks()
    {
        var rows = CsvReader.Parse("a,b\r\n\"with, comma\",\"say \"\"hi\"\"\"\r\n\"two\r\nlines\",end\r\n");

        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "a", "b" }, rows[0]);
        Assert.Equal(new[] { "with, comma", "say \"hi\"" }, rows[1]);
        Assert.Equal("two\r\nlines", rows[2][0]);
        Assert.Equal("end", rows[2][1]);
    }

    [Fact]
    public void TheReader_DropsTheBomAndBlankLines()
    {
        // Excel writes a byte-order mark; left in place the first heading is "\uFEFFName" and every
        // row is then skipped as "no name".
        var rows = CsvReader.Parse("\uFEFFName,Retail\r\n\r\nPanadol,120\r\n\r\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal("Name", rows[0][0]);
        Assert.Equal("Panadol", rows[1][0]);
    }

    [Fact]
    public void TheHeader_IsFoundByNameNotByPosition()
    {
        var map = CsvReader.HeaderMap(new[] { "  Retail (Rs) ", "NAME", "Opening Qty" });

        Assert.Equal(1, map["name"]);
        Assert.Equal(0, map["retail"]);
        Assert.Equal(2, map["openingqty"]);
    }

    [Fact]
    public void TheTemplate_HasTheHeadingsTheImporterLooksFor()
    {
        var template = ImportService.Template("Products");
        var rows = CsvReader.Parse(template);

        Assert.Single(rows);
        Assert.Equal(ImportService.ProductColumns.Length, rows[0].Count);
        Assert.Contains("Name", rows[0]);
        Assert.Contains("Retail", rows[0]);
        Assert.Contains("Opening qty", rows[0]);
        Assert.Contains("Expiry", rows[0]);
    }

    // ============================ product import ============================

    [Fact]
    public void ThePreview_WritesNothingAndSaysWhatWouldHappen()
    {
        using var test = new TestDatabase();

        var preview = Imports(test).PreviewProducts(
            "Name,Retail,Cost,Opening qty,Expiry\r\n" +
            "Panadol 500mg,120,90,100,2027-06-30\r\n" +
            "Panadol 500mg,120,90,0,\r\n" +          // the same medicine twice in one file
            "Brufen 400mg,not a number,90,0,\r\n");  // a broken amount

        Assert.Equal(1, preview.ReadyCount);
        Assert.Equal(1, preview.SkippedCount);
        Assert.Equal(1, preview.ErrorCount);
        Assert.False(preview.CanApply);              // one error stops the whole file

        // Nothing reached the shop.
        Assert.Equal(0L, Count(test, "products"));
        Assert.Equal(0L, Count(test, "batches"));
    }

    [Fact]
    public void AGoodFile_BecomesMedicinesWithRealOpeningStock()
    {
        using var test = new TestDatabase();

        var preview = Imports(test).PreviewProducts(
            "Name,Generic,Retail,Cost,Reorder level,Rack,Opening qty,Batch no,Expiry\r\n" +
            "Panadol 500mg,Paracetamol,120,90,10,A-1,100,B-77,2027-06-30\r\n" +
            "Brufen 400mg,Ibuprofen,150,110,5,A-2,0,,\r\n");

        Assert.True(preview.CanApply, preview.SummaryText);

        var imported = Imports(test).ApplyProducts(TestData.Owner, preview);

        Assert.True(imported.Ok, imported.Error);
        Assert.NotNull(imported.Value);
        Assert.Equal(2, imported.Value!.Added);
        Assert.Equal(1, imported.Value.Batches);
        Assert.Equal(100d, imported.Value.StockBase);

        Assert.Equal(2L, Count(test, "products"));
        Assert.Equal(1L, Count(test, "batches"));

        // The opening stock is a batch the expiry screen can see, at the cost from the file.
        Assert.Equal("B-77", Scalar(test, "SELECT batch_no FROM batches;")?.ToString());
        Assert.Equal("2027-06-30", Scalar(test, "SELECT expiry_date FROM batches;")?.ToString());
        Assert.Equal(100d, Convert.ToDouble(Scalar(test, "SELECT qty_base FROM batches;")));
        Assert.Equal(9000L, Convert.ToInt64(Scalar(test, "SELECT cost_paisa FROM batches;")));

        // And a movement, so the stock ledger can say where the goods came from.
        Assert.Equal(1L, Count(test, "stock_movements WHERE type = 'import'"));

        // Money was typed in rupees and stored in paisa, never as a floating number.
        Assert.Equal(12000L, Convert.ToInt64(Scalar(test,
            "SELECT retail_paisa FROM products WHERE name = 'Panadol 500mg';")));
    }

    [Fact]
    public void ASecondImportOfTheSameFile_AddsNothing()
    {
        using var test = new TestDatabase();

        var csv = "Name,Retail,Cost\r\nPanadol 500mg,120,90\r\n";
        var imports = Imports(test);

        var first = imports.ApplyProducts(TestData.Owner, imports.PreviewProducts(csv));
        Assert.True(first.Ok, first.Error);

        var second = imports.PreviewProducts(csv);

        Assert.Equal(0, second.ReadyCount);
        Assert.Equal(1, second.SkippedCount);
        Assert.False(second.CanApply);           // nothing to do
        Assert.Equal(1L, Count(test, "products"));
    }

    [Fact]
    public void OpeningStockWithoutAnExpiry_IsAnError()
    {
        using var test = new TestDatabase();

        var preview = Imports(test).PreviewProducts("Name,Retail,Cost,Opening qty\r\nPanadol 500mg,120,90,50\r\n");

        Assert.Equal(1, preview.ErrorCount);
        Assert.Contains("expiry", preview.Rows[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RetailBelowCost_IsRefusedAtThePreview()
    {
        using var test = new TestDatabase();

        var preview = Imports(test).PreviewProducts("Name,Retail,Cost\r\nPanadol 500mg,80,90\r\n");

        Assert.Equal(1, preview.ErrorCount);
        Assert.Contains("below cost", preview.Rows[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ACashierCannotImport()
    {
        using var test = new TestDatabase();

        var imports = Imports(test);
        var preview = imports.PreviewProducts("Name,Retail,Cost\r\nPanadol 500mg,120,90\r\n");

        Assert.True(preview.CanApply);

        var imported = imports.ApplyProducts(TestData.Cashier(test), preview);

        Assert.False(imported.Ok);
        Assert.Equal(0L, Count(test, "products"));
    }

    // =========================== customers & udhaar ===========================

    [Fact]
    public void ImportedUdhaar_IsPartOfTheKhataSoTheBooksStillMatch()
    {
        using var test = new TestDatabase();

        var imports = Imports(test);

        var preview = imports.PreviewCustomers(
            "Name,Phone,Category,Opening udhaar,Credit limit\r\n" +
            "Ali Raza,0300-1234567,VIP,5000,10000\r\n" +
            "Sara Khan,,normal,0,\r\n");

        Assert.True(preview.CanApply, preview.SummaryText);

        var imported = imports.ApplyCustomers(TestData.Owner, preview);
        Assert.True(imported.Ok, imported.Error);
        Assert.Equal(2, imported.Value!.Added);

        var page = TestData.Ledger(test).Page(CustomerId(test, "Ali Raza"));

        Assert.True(page.Ok, page.Error);

        // The opening is the first line of the khata, and it is counted - not a note beside it.
        Assert.Equal(500000L, page.Value!.StoredPaisa);
        Assert.Equal(500000L, page.Value.ExpectedPaisa);
        Assert.True(page.Value.BooksMatch, page.Value.BooksLabel);

        var opening = page.Value.Lines[0];
        Assert.Equal("opening", opening.Kind);
        Assert.Equal("Opening balance", opening.KindLabel);
        Assert.Equal(500000L, opening.DebitPaisa);

        // The udhaar page must not turn red for carried-in money: it has no bill date, so it is
        // counted beside the age columns instead of being aged as if it were 90 days old.
        var report = TestData.Recovery(test).Report(TestData.Owner);
        Assert.True(report.Ok, report.Error);

        var row = report.Value!.Rows.Single(entry => entry.Name == "Ali Raza");
        Assert.True(row.BooksMatch);
        Assert.Equal(500000L, row.OpeningPaisa);
        Assert.True(row.HasOpening);

        // The four age columns stay empty on purpose - carried-in money has no bill date to age -
        // and the row's own maths (aged + carried in = stored khata) is what keeps the page honest.
        Assert.Equal(0L, row.BucketsTotalPaisa);
        Assert.Equal(0L, row.OverduePaisa);
        Assert.Equal(500000L, row.BalancePaisa);
        Assert.Equal(500000L, report.Value.OpeningTotalPaisa);
        Assert.True(report.Value.BooksMatch);
    }

    [Fact]
    public void APhoneThatAlreadyHasAKhata_IsSkipped()
    {
        using var test = new TestDatabase();

        TestData.AddCustomer(test, name: "Ali Raza", phone: "0300-1234567");

        var preview = Imports(test).PreviewCustomers(
            "Name,Phone\r\nAli Raza,0300-1234567\r\nSara Khan,0321-9999999\r\n");

        Assert.Equal(1, preview.ReadyCount);
        Assert.Equal(1, preview.SkippedCount);
        Assert.Contains("0300-1234567", preview.Rows[0].Message);
    }

    [Fact]
    public void ASupplier_KeepsHisOpeningPayable()
    {
        using var test = new TestDatabase();

        var imports = Imports(test);

        var preview = imports.PreviewSuppliers("Name,Phone,Agency,Opening payable\r\nGSK,042-111,GSK Pakistan,25000\r\n");

        Assert.True(preview.CanApply, preview.SummaryText);

        var imported = imports.ApplySuppliers(TestData.Owner, preview);
        Assert.True(imported.Ok, imported.Error);

        Assert.Equal(2500000L, Convert.ToInt64(Scalar(test,
            "SELECT opening_balance_paisa FROM suppliers WHERE name = 'GSK';")));
        Assert.Equal(2500000L, Convert.ToInt64(Scalar(test,
            "SELECT balance_paisa FROM suppliers WHERE name = 'GSK';")));
    }

    // ============================== price change ==============================

    [Fact]
    public void APercentChange_IsRoundedToWholePaisaAndAudited()
    {
        using var test = new TestDatabase();

        var productId = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var imports = Imports(test);

        var preview = imports.PreviewPrices(TestData.Owner,
            new PriceChangeRequest { Kind = PriceChangeKind.Percent, Amount = 10m });

        Assert.Single(preview.Rows);
        Assert.Equal(33000L, preview.Rows[0].NewRetailPaisa);   // 300 × 1.10
        Assert.True(preview.CanApply, preview.SummaryText);

        var applied = imports.ApplyPrices(TestData.Owner, preview);
        Assert.True(applied.Ok, applied.Error);
        Assert.Equal(1, applied.Value!.Changed);

        Assert.Equal(33000L, Convert.ToInt64(Scalar(test,
            $"SELECT retail_paisa FROM products WHERE id = {productId};")));

        // Old value and new value both in the black box: a price is money.
        Assert.Equal(1L, Count(test, "audit_logs WHERE action = 'price_change'"));
    }

    [Fact]
    public void AChangeThatWouldSellBelowCost_IsRefusedAndNothingIsWritten()
    {
        using var test = new TestDatabase();

        var productId = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 21000);
        var imports = Imports(test);

        var preview = imports.PreviewPrices(TestData.Owner,
            new PriceChangeRequest { Kind = PriceChangeKind.Percent, Amount = -10m });

        // 210 - 10% = 189, which is below the cost of 200.
        Assert.Equal(1, preview.BelowCostCount);
        Assert.False(preview.CanApply);

        var applied = imports.ApplyPrices(TestData.Owner, preview);
        Assert.False(applied.Ok);

        Assert.Equal(21000L, Convert.ToInt64(Scalar(test,
            $"SELECT retail_paisa FROM products WHERE id = {productId};")));
    }

    [Fact]
    public void LoweringTheCostToo_MakesTheSamePercentSafe()
    {
        using var test = new TestDatabase();

        var productId = TestData.AddProduct(test, costPaisa: 20000, retailPaisa: 30000);
        var imports = Imports(test);

        var preview = imports.PreviewPrices(TestData.Owner,
            new PriceChangeRequest { Kind = PriceChangeKind.Percent, Amount = -10m, IncludeCost = true });

        Assert.Equal(0, preview.BelowCostCount);
        Assert.True(preview.CanApply, preview.SummaryText);

        Assert.True(imports.ApplyPrices(TestData.Owner, preview).Ok);

        Assert.Equal(27000L, Convert.ToInt64(Scalar(test,
            $"SELECT retail_paisa FROM products WHERE id = {productId};")));
        Assert.Equal(18000L, Convert.ToInt64(Scalar(test,
            $"SELECT cost_paisa FROM products WHERE id = {productId};")));
    }

    // ================================ export ================================

    [Fact]
    public void TheExport_IsAFileExcelCanOpen()
    {
        using var test = new TestDatabase();

        TestData.AddProduct(test, name: "Panadol 500mg", retailPaisa: 12000, costPaisa: 9000);

        var exported = Imports(test).ExportProducts(TestData.Owner);

        Assert.True(exported.Ok, exported.Error);

        var rows = CsvReader.Parse(exported.Value!);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Id", rows[0][0]);
        Assert.Equal("Panadol 500mg", rows[1][1]);
        Assert.Contains("120", rows[1][11]);         // the retail rate in rupees, as typed in Excel
        Assert.Contains("100", rows[1][14]);         // the stock on the shelf
    }

    [Fact]
    public void ACashierCannotExportTheShopBooks()
    {
        using var test = new TestDatabase();

        var exported = Imports(test).ExportCustomers(TestData.Cashier(test));

        Assert.False(exported.Ok);
        Assert.NotNull(exported.Error);
    }

    // ================================ helpers ================================

    private static ImportService Imports(TestDatabase test) =>
        new(test.Database, test.Audit,
            new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static long Count(TestDatabase test, string fromWhere) =>
        test.Database.Tx(connection => Convert.ToInt64(
            test.Database.Scalar(connection, "SELECT COUNT(1) FROM " + fromWhere + ";") ?? 0L));

    private static int CustomerId(TestDatabase test, string name) =>
        test.Database.Tx(connection => Convert.ToInt32(
            test.Database.Scalar(connection, "SELECT id FROM customers WHERE name = @name;", new { name }) ?? 0));

    /// <summary>One value from one query - the smallest possible way to inspect what was written.</summary>
    private static object? Scalar(TestDatabase test, string sql) =>
        test.Database.Tx(connection => test.Database.Scalar(connection, sql));
}
