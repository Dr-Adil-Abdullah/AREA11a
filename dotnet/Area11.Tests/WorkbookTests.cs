using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>T-10: real Open XML files and the safe all-data/bulk-update workflow (Spec 24).</summary>
public sealed class WorkbookTests
{
    [Fact]
    public void XlsxRoundTrip_KeepsUnicodeLeadingZerosAndFormulaText()
    {
        var workbook = new XlsxWorkbook();
        var sheet = new XlsxSheet { Name = "Products" };
        sheet.Rows.Add(new List<string> { "Name", "Barcode", "Notes" });
        sheet.Rows.Add(new List<string> { "=1+1", "0012300", "علی\ntwo lines" });
        workbook.Sheets.Add(sheet);

        var bytes = XlsxFile.Write(workbook);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);

        var read = XlsxFile.Read(bytes);
        Assert.Equal("=1+1", read.Sheet("Products")!.Rows[1][0]);
        Assert.Equal("0012300", read.Sheet("Products")!.Rows[1][1]);
        Assert.Equal("علی\ntwo lines", read.Sheet("Products")!.Rows[1][2]);

        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        Assert.DoesNotContain("<f", reader.ReadToEnd()); // text that starts '=' never becomes a formula
    }

    [Fact]
    public void Reader_UnderstandsSharedStringsAndRealExcelDateCells()
    {
        var workbook = new XlsxWorkbook();
        workbook.Sheets.Add(new XlsxSheet
        {
            Name = "Products",
            Rows =
            {
                new List<string> { "Name", "Expiry" },
                new List<string> { "Panadol", "2027-06-30" },
                new List<string> { "ISO date", "2028-01-15" }
            }
        });
        var bytes = XlsxFile.Write(workbook);
        var main = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        using var output = new MemoryStream(bytes);
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
        {
            var sheetDocument = Load(zip.GetEntry("xl/worksheets/sheet1.xml")!);
            var dataCells = sheetDocument.Descendants(main + "c").ToList();
            dataCells[2].ReplaceWith(new XElement(main + "c",
                new XAttribute("r", "A2"), new XAttribute("t", "s"),
                new XElement(main + "v", "0")));
            dataCells[3].ReplaceWith(new XElement(main + "c",
                new XAttribute("r", "B2"), new XAttribute("t", "n"), new XAttribute("s", "1"),
                new XElement(main + "v", new DateTime(2027, 6, 30).ToOADate().ToString(CultureInfo.InvariantCulture))));
            dataCells[5].ReplaceWith(new XElement(main + "c",
                new XAttribute("r", "B3"), new XAttribute("t", "d"),
                new XElement(main + "v", "2028-01-15")));
            Replace(zip, "xl/worksheets/sheet1.xml", sheetDocument);

            var shared = XDocument.Parse("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"1\" uniqueCount=\"1\"><si><t>Panadol</t></si></sst>");
            Replace(zip, "xl/sharedStrings.xml", shared);

            var stylesDocument = Load(zip.GetEntry("xl/styles.xml")!);
            var formats = stylesDocument.Descendants(main + "cellXfs").Single().Elements(main + "xf").ToList();
            formats[1].SetAttributeValue("numFmtId", "14");
            Replace(zip, "xl/styles.xml", stylesDocument);
        }

        var read = XlsxFile.Read(output.ToArray());
        Assert.Equal("Panadol", read.Sheet("Products")!.Rows[1][0]);
        Assert.Equal("2027-06-30", read.Sheet("Products")!.Rows[1][1]);
        Assert.Equal("2028-01-15", read.Sheet("Products")!.Rows[2][1]);
    }

    [Fact]
    public void Template_HasFourSheets_AndWorkbookImportUsesTheMatchingOne()
    {
        using var test = new TestDatabase();
        var workbook = XlsxFile.Read(WorkbookService.Template());
        Assert.Equal(new[] { "Products", "Customers", "Suppliers", "Instructions" },
            workbook.Sheets.Select(sheet => sheet.Name));

        var products = workbook.Sheet("Products")!;
        products.Rows.Add(new List<string>());
        Set(products, 1, "Name", "Imported XLSX medicine");
        Set(products, 1, "Retail", "120");
        Set(products, 1, "Cost", "90");
        Set(products, 1, "Opening qty", "0");

        var preview = Imports(test).PreviewWorkbook("Products", XlsxFile.Write(workbook));
        Assert.Equal(1, preview.ReadyCount);
        Assert.True(preview.CanApply, preview.SummaryText);
        Assert.Equal(0L, Count(test, "products")); // preview still writes nothing
    }

    [Fact]
    public void NativeListExport_IsRealXlsxWithTheSameRupeeValues()
    {
        using var test = new TestDatabase();
        TestData.AddProduct(test, name: "Panadol 500mg", retailPaisa: 12000, costPaisa: 9000);

        var exported = Workbooks(test).ExportList(TestData.Owner, "Products");
        Assert.True(exported.Ok, exported.Error);
        var workbook = XlsxFile.Read(exported.Value!);
        var products = workbook.Sheet("Products")!;

        Assert.Equal("Panadol 500mg", Cell(products, 1, "Name"));
        Assert.Equal("120", Cell(products, 1, "Retail"));
        Assert.Equal("100", Cell(products, 1, "Stock on shelf"));
        Assert.NotNull(workbook.Sheet("Instructions"));
    }

    [Fact]
    public void AllDataDownload_HasEightSheetsWithoutPhotosSecretsOrCounters()
    {
        using var test = new TestDatabase();
        TestData.AddProduct(test);
        TestData.AddCustomer(test);
        TestData.AddSupplier(test);

        var exported = Workbooks(test).ExportAll(TestData.Owner);
        Assert.True(exported.Ok, exported.Error);
        var workbook = XlsxFile.Read(exported.Value!);
        Assert.Equal(new[]
        {
            "Products", "Customers", "Suppliers", "Categories", "Companies", "Batches", "Settings", "Instructions"
        }, workbook.Sheets.Select(sheet => sheet.Name));

        foreach (var sheet in workbook.Sheets.Where(sheet => sheet.Rows.Count > 0))
        {
            Assert.DoesNotContain(sheet.Rows[0], heading => heading.Contains("photo", StringComparison.OrdinalIgnoreCase));
        }

        var settings = workbook.Sheet("Settings")!;
        var keys = settings.Rows.Skip(1).Select(row => row[0]).ToList();
        Assert.Contains("store.name", keys);
        Assert.DoesNotContain("sync.key", keys);
        Assert.DoesNotContain("security.pinLength", keys);
        Assert.DoesNotContain("bill.nextSaleNo", keys);
        Assert.DoesNotContain("brand.logoDataUrl", keys);
    }

    [Fact]
    public void BulkApply_ShowsBeforeAfter_ClearsAndAuditsWithoutTouchingStock()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test, retailPaisa: 30000);
        Execute(test, "UPDATE products SET generic = 'Paracetamol' WHERE id = " + productId + ";");
        var workbook = Download(test);
        var products = workbook.Sheet("Products")!;
        var row = RowWithId(products, productId);
        Set(products, row, "Name", "Panadol Extra");
        Set(products, row, "Retail", "350");
        Set(products, row, "Generic", "__CLEAR__");
        var bytes = XlsxFile.Write(workbook);

        var previewResult = Workbooks(test).PreviewBulk(TestData.Owner, bytes, includeSettings: false);
        Assert.True(previewResult.Ok, previewResult.Error);
        var preview = previewResult.Value!;
        Assert.True(preview.CanApply, preview.SummaryText);
        var changed = preview.Rows.Single(entry => entry.IsReady);
        Assert.Contains(changed.Changes, change => change.Field == "Name" && change.Before == "Panadol 500mg" && change.After == "Panadol Extra");
        Assert.Contains(changed.Changes, change => change.Field == "Generic" && change.Before == "Paracetamol" && change.After == "");
        Assert.Equal(30000L, Convert.ToInt64(Scalar(test, "SELECT retail_paisa FROM products WHERE id = " + productId + ";")));

        var applied = Workbooks(test).ApplyBulk(TestData.Owner, preview);
        Assert.True(applied.Ok, applied.Error);
        Assert.Equal(35000L, Convert.ToInt64(Scalar(test, "SELECT retail_paisa FROM products WHERE id = " + productId + ";")));
        Assert.Equal("Panadol Extra", Scalar(test, "SELECT name FROM products WHERE id = " + productId + ";"));
        Assert.Null(Scalar(test, "SELECT generic FROM products WHERE id = " + productId + ";"));
        Assert.Equal(100d, Convert.ToDouble(Scalar(test, "SELECT qty_base FROM batches WHERE product_id = " + productId + ";")));
        Assert.Equal(1L, Count(test, "audit_logs WHERE action = 'workbook_update'"));
    }

    [Fact]
    public void BulkUpdate_UploadingTheAppliedFileAgainMeansZeroChanges()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);
        var workbook = Download(test);
        var products = workbook.Sheet("Products")!;
        Set(products, RowWithId(products, productId), "Name", "Only changed once");
        var bytes = XlsxFile.Write(workbook);
        var service = Workbooks(test);

        var first = service.PreviewBulk(TestData.Owner, bytes, false).Value!;
        Assert.True(service.ApplyBulk(TestData.Owner, first).Ok);
        var second = service.PreviewBulk(TestData.Owner, bytes, false).Value!;

        Assert.Equal(0, second.ChangeCount);
        Assert.Equal(0, second.ErrorCount);
        Assert.False(second.CanApply);
        Assert.Contains(second.Rows, row => row.Sheet == "Products" && row.IsSame);
        Assert.Equal(1L, Count(test, "audit_logs WHERE action = 'workbook_update'"));
    }

    [Fact]
    public void BulkUpdate_RefusesStockAndAccountBalanceChanges()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);
        var otherId = TestData.AddProduct(test, name: "Pack barcode owner");
        var customerId = TestData.AddCustomer(test);
        Execute(test, "INSERT INTO product_barcodes(product_id, barcode, pack, active) VALUES("
            + otherId + ", 'PACK-999', 'box', 1);");
        var workbook = Download(test);
        var products = workbook.Sheet("Products")!;
        var customers = workbook.Sheet("Customers")!;
        Set(products, RowWithId(products, productId), "Stock on shelf", "999");
        Set(products, RowWithId(products, otherId), "Barcode", "PACK-999");
        Set(customers, RowWithId(customers, customerId), "Udhaar now", "999");

        var preview = Workbooks(test).PreviewBulk(TestData.Owner, XlsxFile.Write(workbook), false).Value!;
        Assert.True(preview.ErrorCount >= 3, preview.SummaryText);
        Assert.False(preview.CanApply);
        var messages = string.Join(" ", preview.Rows.Where(row => row.IsError).Select(row => row.Message));
        Assert.Contains("read-only", messages);
        Assert.Contains("active pack barcodes", messages);
        Assert.False(Workbooks(test).ApplyBulk(TestData.Owner, preview).Ok);
        Assert.Equal(100d, Convert.ToDouble(Scalar(test, "SELECT qty_base FROM batches WHERE product_id = " + productId + ";")));
        Assert.Equal(0L, Convert.ToInt64(Scalar(test, "SELECT balance_paisa FROM customers WHERE id = " + customerId + ";")));
    }

    [Fact]
    public void BulkUpdate_SettingsNeedTheTick_AndKeepStringNumberAndArrayTypes()
    {
        using var test = new TestDatabase();
        var workbook = Download(test);
        var settings = workbook.Sheet("Settings")!;
        Set(settings, RowWithKey(settings, "store.name"), "Value", "Workbook Pharmacy");
        Set(settings, RowWithKey(settings, "tax.percent"), "Value", "12.5");
        Set(settings, RowWithKey(settings, "payment.methods"), "Value", "[\"cash\",\"credit\",\"bank\"]");
        var bytes = XlsxFile.Write(workbook);
        var service = Workbooks(test);

        var withoutTick = service.PreviewBulk(TestData.Owner, bytes, includeSettings: false).Value!;
        Assert.Equal(0, withoutTick.ChangeCount);
        Assert.True(withoutTick.SkippedCount > 0);
        Assert.Equal("Area11 Pharmacy", test.Settings.GetString("store.name"));

        var preview = service.PreviewBulk(TestData.Owner, bytes, includeSettings: true).Value!;
        Assert.True(preview.CanApply, preview.SummaryText);
        Assert.Equal(3, preview.ChangeCount);
        var applied = service.ApplyBulk(TestData.Owner, preview);
        Assert.True(applied.Ok, applied.Error);

        Assert.Equal("Workbook Pharmacy", test.Settings.GetString("store.name"));
        using (var tax = JsonDocument.Parse(test.Settings.GetRaw("tax.percent")))
        {
            Assert.Equal(JsonValueKind.Number, tax.RootElement.ValueKind);
            Assert.Equal(12.5m, tax.RootElement.GetDecimal());
        }

        using var methods = JsonDocument.Parse(test.Settings.GetRaw("payment.methods"));
        Assert.Equal(JsonValueKind.Array, methods.RootElement.ValueKind);
        Assert.Equal("bank", methods.RootElement[2].GetString());
    }

    [Fact]
    public void BulkUpdate_LockedSettingsCannotBeAddedByHand()
    {
        using var test = new TestDatabase();
        var workbook = Download(test);
        var settings = workbook.Sheet("Settings")!;
        settings.Rows.Add(new List<string> { "security.pinLength", "8", "number", "not-a-snapshot" });

        var preview = Workbooks(test).PreviewBulk(TestData.Owner, XlsxFile.Write(workbook), true).Value!;
        Assert.Contains(preview.Rows, row => row.IsError && row.Key == "security.pinLength" && row.Message.Contains("locked"));
        Assert.False(preview.CanApply);
        Assert.Equal(4, test.Settings.GetInt("security.pinLength"));
    }

    [Fact]
    public void BulkUpdate_CashierCannotDownloadPreviewOrApply()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);
        var cashier = TestData.Cashier(test);
        // Even an explicitly granted module permission must not widen Spec 24.2's role boundary.
        test.Database.Tx(connection => test.Database.Execute(connection, @"
INSERT INTO user_permissions(user_id, module_key, allowed)
VALUES(@id, 'import', 1), (@id, 'settings', 1);", new { id = cashier.Id }));
        var workbook = Download(test);
        var products = workbook.Sheet("Products")!;
        Set(products, RowWithId(products, productId), "Name", "Not for a cashier");
        var bytes = XlsxFile.Write(workbook);
        var service = Workbooks(test);

        Assert.False(service.ExportAll(cashier).Ok);
        Assert.False(service.PreviewBulk(cashier, bytes, false).Ok);
        var ownerPreview = service.PreviewBulk(TestData.Owner, bytes, false).Value!;
        Assert.False(service.ApplyBulk(cashier, ownerPreview).Ok);
        Assert.Equal("Panadol 500mg", Scalar(test, "SELECT name FROM products WHERE id = " + productId + ";"));
    }

    [Fact]
    public void BulkApply_StaleLaterRowRollsBackTheEarlierValidRow()
    {
        using var test = new TestDatabase();
        var firstId = TestData.AddProduct(test, name: "First medicine");
        var secondId = TestData.AddProduct(test, name: "Second medicine");
        var workbook = Download(test);
        var products = workbook.Sheet("Products")!;
        Set(products, RowWithId(products, firstId), "Name", "First from workbook");
        Set(products, RowWithId(products, secondId), "Name", "Second from workbook");
        var preview = Workbooks(test).PreviewBulk(TestData.Owner, XlsxFile.Write(workbook), false).Value!;
        Assert.Equal(2, preview.ChangeCount);

        Execute(test, "UPDATE products SET name = 'Second changed elsewhere' WHERE id = " + secondId + ";");
        var applied = Workbooks(test).ApplyBulk(TestData.Owner, preview);

        Assert.False(applied.Ok);
        Assert.Contains("rolled back", applied.Error);
        Assert.Equal("First medicine", Scalar(test, "SELECT name FROM products WHERE id = " + firstId + ";"));
        Assert.Equal("Second changed elsewhere", Scalar(test, "SELECT name FROM products WHERE id = " + secondId + ";"));
        Assert.Equal(0L, Count(test, "audit_logs WHERE action = 'workbook_update'"));

        // A field outside the plan can stale it too: proposed Retail 350 must not survive a
        // concurrent Cost increase to 400. The whole original row is rechecked before writing.
        var fresh = Download(test);
        var freshProducts = fresh.Sheet("Products")!;
        Set(freshProducts, RowWithId(freshProducts, firstId), "Retail", "350");
        var pricePreview = Workbooks(test).PreviewBulk(TestData.Owner, XlsxFile.Write(fresh), false).Value!;
        Assert.True(pricePreview.CanApply, pricePreview.SummaryText);
        var oldRetail = Convert.ToInt64(Scalar(test, "SELECT retail_paisa FROM products WHERE id = " + firstId + ";"));
        Execute(test, "UPDATE products SET cost_paisa = 40000 WHERE id = " + firstId + ";");

        var stalePrice = Workbooks(test).ApplyBulk(TestData.Owner, pricePreview);
        Assert.False(stalePrice.Ok);
        Assert.Equal(oldRetail, Convert.ToInt64(Scalar(test, "SELECT retail_paisa FROM products WHERE id = " + firstId + ";")));
        Assert.Equal(40000L, Convert.ToInt64(Scalar(test, "SELECT cost_paisa FROM products WHERE id = " + firstId + ";")));
        Assert.Equal(0L, Count(test, "audit_logs WHERE action = 'workbook_update'"));
    }

    [Fact]
    public void BulkUpdate_BatchExpiryAlsoUpdatesItsMonth_NotItsQuantity()
    {
        using var test = new TestDatabase();
        var productId = TestData.AddProduct(test);
        var workbook = Download(test);
        var batches = workbook.Sheet("Batches")!;
        var batchId = Convert.ToInt32(Scalar(test, "SELECT id FROM batches WHERE product_id = " + productId + ";"));
        Set(batches, RowWithId(batches, batchId), "Expiry", "2028-01-15");

        var preview = Workbooks(test).PreviewBulk(TestData.Owner, XlsxFile.Write(workbook), false).Value!;
        Assert.True(preview.CanApply, preview.SummaryText);
        var applied = Workbooks(test).ApplyBulk(TestData.Owner, preview);
        Assert.True(applied.Ok, applied.Error);
        Assert.Equal("2028-01-15", Scalar(test, "SELECT expiry_date FROM batches WHERE id = " + batchId + ";"));
        Assert.Equal("2028-01", Scalar(test, "SELECT expiry_ym FROM batches WHERE id = " + batchId + ";"));
        Assert.Equal(100d, Convert.ToDouble(Scalar(test, "SELECT qty_base FROM batches WHERE id = " + batchId + ";")));
    }

    [Fact]
    public void BulkUpdate_CategoryCycleIsRefusedBeforeAnythingIsWritten()
    {
        using var test = new TestDatabase();
        var (parentId, childId) = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection, "INSERT INTO categories(name, active, created_at) VALUES('Parent', 1, '2026-01-01');");
            var parent = (int)test.Database.NextId(connection);
            test.Database.Execute(connection, "INSERT INTO categories(name, parent_id, active, created_at) VALUES('Child', @parent, 1, '2026-01-01');", new { parent });
            return (parent, (int)test.Database.NextId(connection));
        });
        var workbook = Download(test);
        var categories = workbook.Sheet("Categories")!;
        Set(categories, RowWithId(categories, parentId), "Parent Id", childId.ToString(CultureInfo.InvariantCulture));
        Set(categories, RowWithId(categories, childId), "Parent Id", parentId.ToString(CultureInfo.InvariantCulture));

        var preview = Workbooks(test).PreviewBulk(TestData.Owner, XlsxFile.Write(workbook), false).Value!;
        Assert.Contains(preview.Rows, row => row.IsError && row.Message.Contains("cycle"));
        Assert.False(preview.CanApply);
        Assert.Equal(0L, Count(test, "audit_logs WHERE action = 'workbook_update'"));
    }

    // ================================ helpers ================================

    private static ImportService Imports(TestDatabase test) =>
        new(test.Database, test.Audit, new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static WorkbookService Workbooks(TestDatabase test) =>
        new(test.Database, test.Audit, new PermissionService(test.Database, test.Log, test.Audit),
            test.Settings, test.Log, Imports(test));

    private static XlsxWorkbook Download(TestDatabase test)
    {
        var result = Workbooks(test).ExportAll(TestData.Owner);
        Assert.True(result.Ok, result.Error);
        return XlsxFile.Read(result.Value!);
    }

    private static int RowWithId(XlsxSheet sheet, int id) => RowWithKey(sheet, id.ToString(CultureInfo.InvariantCulture));

    private static int RowWithKey(XlsxSheet sheet, string key)
    {
        var column = sheet.Rows[0].FindIndex(value => value is "Id" or "Key");
        for (var index = 1; index < sheet.Rows.Count; index++)
        {
            if (Cell(sheet, index, sheet.Rows[0][column]) == key)
            {
                return index;
            }
        }

        throw new InvalidOperationException("No workbook row for " + key + " in " + sheet.Name);
    }

    private static string Cell(XlsxSheet sheet, int row, string heading)
    {
        var column = sheet.Rows[0].IndexOf(heading);
        Assert.True(column >= 0, heading + " is not a column in " + sheet.Name);
        return row < sheet.Rows.Count && column < sheet.Rows[row].Count ? sheet.Rows[row][column] : string.Empty;
    }

    private static void Set(XlsxSheet sheet, int row, string heading, string value)
    {
        var column = sheet.Rows[0].IndexOf(heading);
        Assert.True(column >= 0, heading + " is not a column in " + sheet.Name);
        while (sheet.Rows[row].Count <= column)
        {
            sheet.Rows[row].Add(string.Empty);
        }

        sheet.Rows[row][column] = value;
    }

    private static XDocument Load(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static void Replace(ZipArchive zip, string path, XDocument document)
    {
        zip.GetEntry(path)?.Delete();
        var entry = zip.CreateEntry(path);
        using var stream = entry.Open();
        document.Save(stream);
    }

    private static long Count(TestDatabase test, string fromWhere) =>
        test.Database.Tx(connection => Convert.ToInt64(
            test.Database.Scalar(connection, "SELECT COUNT(1) FROM " + fromWhere + ";") ?? 0L));

    private static object? Scalar(TestDatabase test, string sql) =>
        test.Database.Tx(connection => test.Database.Scalar(connection, sql));

    private static void Execute(TestDatabase test, string sql) =>
        test.Database.Tx(connection => test.Database.Execute(connection, sql));
}
