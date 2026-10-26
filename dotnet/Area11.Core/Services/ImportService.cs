using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Excel import, export and price change (F-37, Spec 24) - the bridge between the shop's old
/// spreadsheet and this program.
///
/// Both real .xlsx workbooks and the original CSV workflow are accepted. The dependency-free Open
/// XML codec and all-data/bulk-update rules live in <see cref="WorkbookService"/>; this service keeps
/// the same new-record preview, duplicate, opening-stock and price-change rules for either format.
///
/// The shape of every import is the same, because it is the shape that protects the shop:
///
///   1. **Preview** reads and checks the whole file and writes **nothing**. Every line is OK,
///      skipped ("already exists") or an error with the reason and the Excel line number.
///   2. **Any error means nothing is written.** A half-imported stock list leaves the shop unable to
///      say which half went in.
///   3. **Apply** writes exactly the rows that were shown, in ONE transaction, and every row writes
///      its own line in the black box.
///
/// The rules that keep the books honest:
///   • a duplicate is skipped, never overwritten - importing the same file twice adds nothing;
///   • opening stock becomes a **real batch** with an expiry and an `import` movement, so the shelf
///     count, the expiry screen and the stock ledger all agree on where the goods came from;
///   • opening **udhaar** goes into the customer's opening balance - part of the khata's maths, not a
///     note beside it (migration 020), exactly like a supplier's opening payable;
///   • a price change never writes a retail price below the cost - those rows are refused.
/// </summary>
public sealed class ImportService
{
    /// <summary>What the file must have, and what it may have, in the order the template writes them.</summary>
    public static readonly string[] ProductColumns =
    {
        "Name", "Generic", "Brand", "Barcode", "Category", "Pack", "Base unit",
        "Box strips", "Strip tablets", "Cost", "Retail", "Reorder level", "Rack",
        "Opening qty", "Batch no", "Expiry"
    };

    public static readonly string[] CustomerColumns =
    {
        "Name", "Phone", "Category", "Opening udhaar", "Credit limit"
    };

    public static readonly string[] SupplierColumns =
    {
        "Name", "Phone", "Agency", "Opening payable"
    };

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public ImportService(Database db, AuditService audit, PermissionService permissions, LogService log)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    // ======================= Templates and exports =======================

    /// <summary>An empty file with the headings, so the owner has somewhere to type.</summary>
    public static string Template(string kind)
    {
        var header = kind switch
        {
            "Customers" => CustomerColumns,
            "Suppliers" => SupplierColumns,
            _ => ProductColumns
        };

        return CsvWriter.Document(header, Array.Empty<IEnumerable<string?>>());
    }

    /// <summary>
    /// Reads the matching worksheet from a real .xlsx file, then uses the exact same preview and
    /// duplicate/opening-stock rules as CSV. A one-sheet workbook may have any sheet name.
    /// </summary>
    public ImportPreview PreviewWorkbook(string kind, byte[] bytes)
    {
        var workbook = XlsxFile.Read(bytes);
        var sheet = workbook.Sheet(kind);
        if (sheet is null)
        {
            var dataSheets = workbook.Sheets
                .Where(candidate => !string.Equals(candidate.Name, "Instructions", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (dataSheets.Count == 1)
            {
                sheet = dataSheets[0];
            }
        }

        if (sheet is null)
        {
            throw new InvalidDataException("The workbook has no '" + kind + "' worksheet.");
        }

        var text = XlsxFile.SheetAsCsv(sheet);
        return kind switch
        {
            "Customers" => PreviewCustomers(text),
            "Suppliers" => PreviewSuppliers(text),
            _ => PreviewProducts(text)
        };
    }

    /// <summary>
    /// Every medicine with its stock on the shelf. This is the file the owner keeps as a paper copy
    /// of the shop, and the one he edits for a bulk price change.
    /// </summary>
    public ServiceResult<string> ExportProducts(User user)
    {
        if (!Allowed(user))
        {
            return ServiceResult<string>.Fail("You do not have permission to export data.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<ExportProductRow>(connection, @"
SELECT p.id AS id, p.name AS name, IFNULL(p.generic, '') AS generic, IFNULL(p.brand, '') AS brand,
       IFNULL(p.barcode, '') AS barcode, IFNULL(c.name, '') AS category, IFNULL(p.pack_size_label, '') AS pack,
       p.base_unit AS baseUnit, IFNULL(p.box_strips, 0) AS boxStrips, IFNULL(p.strip_tablets, 0) AS stripTablets,
       p.cost_paisa AS costPaisa, p.retail_paisa AS retailPaisa, IFNULL(p.reorder_level, 0) AS reorderLevel,
       IFNULL(p.rack_no, '') AS rackNo,
       CAST(IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) AS REAL) AS stockBase,
       (SELECT MIN(b.expiry_date) FROM batches b WHERE b.product_id = p.id AND b.active = 1 AND b.qty_base > 0) AS nearestExpiry
FROM products p
LEFT JOIN categories c ON c.id = p.category_id
WHERE p.active = 1
ORDER BY p.name COLLATE NOCASE;");

            var csv = CsvWriter.Document(
                new[]
                {
                    "Id", "Name", "Generic", "Brand", "Barcode", "Category", "Pack", "Base unit",
                    "Box strips", "Strip tablets", "Cost", "Retail", "Reorder level", "Rack",
                    "Stock on shelf", "Nearest expiry"
                },
                rows.Select(row => new[]
                {
                    row.Id.ToString(CultureInfo.InvariantCulture),
                    row.Name,
                    row.Generic ?? string.Empty,
                    row.Brand ?? string.Empty,
                    row.Barcode ?? string.Empty,
                    row.Category ?? string.Empty,
                    row.Pack ?? string.Empty,
                    row.BaseUnit ?? string.Empty,
                    row.BoxStrips.ToString(CultureInfo.InvariantCulture),
                    row.StripTablets.ToString(CultureInfo.InvariantCulture),
                    Money.Edit(row.CostPaisa),
                    Money.Edit(row.RetailPaisa),
                    row.ReorderLevel.ToString(CultureInfo.InvariantCulture),
                    row.RackNo ?? string.Empty,
                    Qty.Edit(row.StockBase),
                    row.NearestExpiry ?? string.Empty
                }));

            _log.Info($"Products exported: {rows.Count} row(s).");
            return ServiceResult<string>.Success(csv);
        }
        catch (Exception exception)
        {
            _log.Error("The product list could not be exported.", exception);
            return ServiceResult<string>.Fail("The product list could not be exported.");
        }
    }

    /// <summary>Every customer with the udhaar on the book - the khata in one file.</summary>
    public ServiceResult<string> ExportCustomers(User user)
    {
        if (!Allowed(user))
        {
            return ServiceResult<string>.Fail("You do not have permission to export data.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<ExportCustomerRow>(connection, @"
SELECT c.id AS id, c.name AS name, IFNULL(c.phone, '') AS phone, c.category AS category,
       IFNULL(c.opening_balance_paisa, 0) AS openingPaisa, c.balance_paisa AS balancePaisa,
       c.credit_limit_paisa AS creditLimitPaisa
FROM customers c
WHERE c.active = 1
ORDER BY c.name COLLATE NOCASE;");

            var csv = CsvWriter.Document(
                new[] { "Id", "Name", "Phone", "Category", "Opening udhaar", "Udhaar now", "Credit limit" },
                rows.Select(row => new[]
                {
                    row.Id.ToString(CultureInfo.InvariantCulture),
                    row.Name,
                    row.Phone ?? string.Empty,
                    row.Category,
                    Money.Edit(row.OpeningPaisa),
                    Money.Edit(row.BalancePaisa),
                    Money.Edit(row.CreditLimitPaisa)
                }));

            return ServiceResult<string>.Success(csv);
        }
        catch (Exception exception)
        {
            _log.Error("The customer list could not be exported.", exception);
            return ServiceResult<string>.Fail("The customer list could not be exported.");
        }
    }

    /// <summary>Every supplier with what the shop owes.</summary>
    public ServiceResult<string> ExportSuppliers(User user)
    {
        if (!Allowed(user))
        {
            return ServiceResult<string>.Fail("You do not have permission to export data.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<ExportSupplierRow>(connection, @"
SELECT s.id AS id, s.name AS name, IFNULL(s.phone, '') AS phone, IFNULL(s.agency, '') AS agency,
       IFNULL(s.opening_balance_paisa, 0) AS openingPaisa, s.balance_paisa AS balancePaisa
FROM suppliers s
WHERE s.active = 1
ORDER BY s.name COLLATE NOCASE;");

            var csv = CsvWriter.Document(
                new[] { "Id", "Name", "Phone", "Agency", "Opening payable", "Payable now" },
                rows.Select(row => new[]
                {
                    row.Id.ToString(CultureInfo.InvariantCulture),
                    row.Name,
                    row.Phone ?? string.Empty,
                    row.Agency ?? string.Empty,
                    Money.Edit(row.OpeningPaisa),
                    Money.Edit(row.BalancePaisa)
                }));

            return ServiceResult<string>.Success(csv);
        }
        catch (Exception exception)
        {
            _log.Error("The supplier list could not be exported.", exception);
            return ServiceResult<string>.Fail("The supplier list could not be exported.");
        }
    }

    // ============================ Product import ============================

    /// <summary>Reads and checks a products file. Writes nothing - see the class comment.</summary>
    public ImportPreview PreviewProducts(string csvText)
    {
        var preview = new ImportPreview { Kind = "Products" };
        var table = CsvReader.Parse(csvText ?? string.Empty);

        if (table.Count == 0)
        {
            return preview;
        }

        var header = table[0];
        var map = CsvReader.HeaderMap(header);
        preview.HeaderLine = 1;
        preview.Headings.AddRange(map.Keys.OrderBy(key => key, StringComparer.Ordinal));

        if (!map.ContainsKey("name"))
        {
            preview.Rows.Add(new ImportRow
            {
                Line = 1,
                Key = "headings",
                State = ImportState.Error,
                Message = "The first line must have a \"Name\" column. Use the template."
            });

            return preview;
        }

        var existingNames = ReadExistingProductNames();
        var existingBarcodes = ReadExistingBarcodes();

        for (var index = 1; index < table.Count; index++)
        {
            var row = table[index];
            var line = index + 1;

            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string Field(string column) =>
                map.TryGetValue(column, out var position) ? CsvReader.At(row, position) : string.Empty;

            var name = Field("name");

            if (name.Length < 2)
            {
                preview.Rows.Add(Error(line, name, "The medicine name is empty."));
                continue;
            }

            if (existingNames.Contains(name))
            {
                preview.Rows.Add(Skipped(line, name, "already in the shop"));
                continue;
            }

            var barcode = Field("barcode");
            if (barcode.Length > 0 && existingBarcodes.Contains(barcode))
            {
                preview.Rows.Add(Skipped(line, name, "barcode " + barcode + " already belongs to another medicine"));
                continue;
            }

            if (!Money.TryParse(Field("retail"), out var retailRupees) || retailRupees < 0)
            {
                preview.Rows.Add(Error(line, name, "Retail is not an amount of money."));
                continue;
            }

            if (!Money.TryParse(Field("cost"), out var costRupees) || costRupees < 0)
            {
                preview.Rows.Add(Error(line, name, "Cost is not an amount of money."));
                continue;
            }

            if (retailRupees < costRupees)
            {
                preview.Rows.Add(Error(line, name, "Retail is below cost - check the two amounts."));
                continue;
            }

            if (!Qty.TryParse(Field("openingqty"), out var openingQty) || openingQty < 0)
            {
                preview.Rows.Add(Error(line, name, "Opening quantity is not a number."));
                continue;
            }

            var expiry = Field("expiry");

            if (openingQty > 0 && expiry.Length == 0)
            {
                preview.Rows.Add(Error(line, name, "There is opening stock but no expiry date."));
                continue;
            }

            var normalisedExpiry = string.Empty;

            if (expiry.Length > 0 && !Expiry.TryNormalize(expiry, out normalisedExpiry, out _))
            {
                preview.Rows.Add(Error(line, name, "The expiry date \"" + expiry + "\" is not a date (use 2027-06-30)."));
                continue;
            }

            var item = new ProductImport
            {
                Name = name,
                Generic = Empty(Field("generic")),
                Brand = Empty(Field("brand")),
                Barcode = Empty(barcode),
                Category = Empty(Field("category")),
                RackNo = Empty(Field("rack")),
                PackLabel = Empty(Field("pack")),
                BaseUnit = Field("baseunit").Length == 0 ? "tablet" : Field("baseunit").ToLowerInvariant(),
                BoxStrips = Whole(Field("boxstrips")),
                StripTablets = Whole(Field("striptablets")),
                CostPaisa = Money.ToPaisa(costRupees),
                RetailPaisa = Money.ToPaisa(retailRupees),
                ReorderLevel = Whole(Field("reorderlevel")),
                OpeningQty = openingQty,
                BatchNo = Empty(Field("batchno")),
                Expiry = normalisedExpiry.Length == 0 ? null : normalisedExpiry
            };

            preview.Products.Add(item);

            preview.Rows.Add(new ImportRow
            {
                Line = line,
                Key = name,
                State = ImportState.Ready,
                Detail = Money.Format(item.RetailPaisa)
                         + (openingQty > 0 ? ", opening " + Qty.Format(openingQty) + " units" : ", card only")
            });

            existingNames.Add(name);

            if (barcode.Length > 0)
            {
                existingBarcodes.Add(barcode);
            }
        }

        return preview;
    }

    /// <summary>
    /// Writes the previewed products. One transaction: either the whole file is in the shop or none
    /// of it is.
    /// </summary>
    public ServiceResult<ImportOutcome> ApplyProducts(User user, ImportPreview preview)
    {
        if (!CanImport(user))
        {
            return ServiceResult<ImportOutcome>.Fail("Only the owner or a manager can import medicines.");
        }

        if (!preview.CanApply)
        {
            return ServiceResult<ImportOutcome>.Fail(
                preview.ErrorCount > 0
                    ? "The file still has lines with a problem. Nothing was imported."
                    : "There is nothing to import in this file.");
        }

        try
        {
            var outcome = _db.Tx(connection =>
            {
                var result = new ImportOutcome { Kind = "Products", Skipped = preview.SkippedCount };
                var categoryIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in preview.Products)
                {
                    var categoryId = CategoryId(connection, item.Category, categoryIds);

                    _db.Execute(connection, @"
INSERT INTO products
    (name, generic, brand, barcode, category_id, rack_no, pack_size_label, base_unit,
     box_strips, strip_tablets, cost_paisa, retail_paisa, vip_paisa, doctor_paisa,
     reorder_level, track_expiry, active, created_at, updated_at)
VALUES
    (@name, @generic, @brand, @barcode, @categoryId, @rackNo, @packLabel, @baseUnit,
     @boxStrips, @stripTablets, @costPaisa, @retailPaisa, 0, 0,
     @reorderLevel, 1, 1, @at, @at);",
                        new
                        {
                            name = item.Name,
                            generic = item.Generic,
                            brand = item.Brand,
                            barcode = item.Barcode,
                            categoryId,
                            rackNo = item.RackNo,
                            packLabel = item.PackLabel,
                            baseUnit = item.BaseUnit,
                            boxStrips = item.BoxStrips,
                            stripTablets = item.StripTablets,
                            costPaisa = item.CostPaisa,
                            retailPaisa = item.RetailPaisa,
                            reorderLevel = item.ReorderLevel,
                            at = Clock.Now()
                        });

                    var productId = (int)_db.NextId(connection);
                    result.Added++;

                    _audit.Record(connection, user, "Import", "product_import", "products",
                        productId.ToString(CultureInfo.InvariantCulture),
                        newValue: $"{{\"name\":\"{item.Name}\",\"retailPaisa\":{item.RetailPaisa}}}");

                    if (item.OpeningQty <= 0)
                    {
                        continue;
                    }

                    // Opening stock is a real batch, not a number on the card: the expiry screen, the
                    // shelf count and every report read batches, and a product with stock but no batch
                    // would look empty everywhere.
                    var expiry = item.Expiry ?? string.Empty;
                    var expiryYm = expiry.Length >= 7 ? expiry[..7] : null;

                    _db.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    is_sample, active, created_at)
VALUES(@productId, @batchNo, @expiryDate, @expiryYm, @qty, @cost, @retail, 0, 1, @at);",
                        new
                        {
                            productId,
                            batchNo = item.BatchNo ?? "OPENING",
                            expiryDate = expiry.Length == 0 ? null : expiry,
                            expiryYm,
                            qty = item.OpeningQty,
                            cost = item.CostPaisa,
                            retail = item.RetailPaisa,
                            at = Clock.Now()
                        });

                    var batchId = (int)_db.NextId(connection);
                    result.Batches++;
                    result.StockBase += item.OpeningQty;

                    _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, ref_type, note, user_id, created_at)
VALUES(@productId, @batchId, 'import', @qty, 'import', 'Opening stock (import)', @userId, @at);",
                        new
                        {
                            productId,
                            batchId,
                            qty = item.OpeningQty,
                            userId = user.Id,
                            at = Clock.Now()
                        });
                }

                return result;
            });

            _log.Info($"Products imported: {outcome.Added} added, {outcome.Skipped} skipped.");
            return ServiceResult<ImportOutcome>.Success(outcome);
        }
        catch (Exception exception)
        {
            _log.Error("The product import failed.", exception);
            return ServiceResult<ImportOutcome>.Fail(
                "The import stopped and nothing was saved (the whole file is one transaction). " +
                exception.Message);
        }
    }

    // =========================== Customer import ===========================

    public ImportPreview PreviewCustomers(string csvText)
    {
        var preview = new ImportPreview { Kind = "Customers" };
        var table = CsvReader.Parse(csvText ?? string.Empty);

        if (table.Count == 0)
        {
            return preview;
        }

        var map = CsvReader.HeaderMap(table[0]);
        preview.HeaderLine = 1;
        preview.Headings.AddRange(map.Keys.OrderBy(key => key, StringComparer.Ordinal));

        if (!map.ContainsKey("name"))
        {
            preview.Rows.Add(Error(1, "headings", "The first line must have a \"Name\" column. Use the template."));
            return preview;
        }

        var phones = ReadExistingPhones();
        var names = ReadExistingCustomerNames();

        for (var index = 1; index < table.Count; index++)
        {
            var row = table[index];
            var line = index + 1;

            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string Field(string column) =>
                map.TryGetValue(column, out var position) ? CsvReader.At(row, position) : string.Empty;

            var name = Field("name");

            if (name.Length < 2)
            {
                preview.Rows.Add(Error(line, name, "The customer name is empty."));
                continue;
            }

            var phone = Field("phone");

            if (phone.Length > 0 && phones.Contains(phone))
            {
                preview.Rows.Add(Skipped(line, name, "phone " + phone + " already belongs to a khata"));
                continue;
            }

            if (phone.Length == 0 && names.Contains(name))
            {
                preview.Rows.Add(Skipped(line, name, "already in the shop (same name, no phone to tell them apart)"));
                continue;
            }

            if (!Money.TryParse(Field("openingudhaar"), out var opening) || opening < 0)
            {
                preview.Rows.Add(Error(line, name, "Opening udhaar is not an amount of money."));
                continue;
            }

            if (!Money.TryParse(Field("creditlimit"), out var limit) || limit < 0)
            {
                preview.Rows.Add(Error(line, name, "Credit limit is not an amount of money."));
                continue;
            }

            var category = Field("category").ToLowerInvariant() switch
            {
                "vip" => "vip",
                "doctor" => "doctor",
                _ => "normal"
            };

            var item = new CustomerImport
            {
                Name = name,
                Phone = Empty(phone),
                Category = category,
                OpeningPaisa = Money.ToPaisa(opening),
                CreditLimitPaisa = Money.ToPaisa(limit)
            };

            preview.Customers.Add(item);
            names.Add(name);

            if (phone.Length > 0)
            {
                phones.Add(phone);
            }

            preview.Rows.Add(new ImportRow
            {
                Line = line,
                Key = name,
                State = ImportState.Ready,
                Detail = (phone.Length == 0 ? "no phone" : phone)
                         + (item.OpeningPaisa > 0 ? ", owes " + Money.Format(item.OpeningPaisa) : string.Empty)
            });
        }

        return preview;
    }

    /// <summary>
    /// Writes the previewed customers. The opening udhaar goes into `opening_balance_paisa` **and**
    /// into `balance_paisa`: the opening is part of the khata, and the page starts its running total
    /// there, so "Books match" is true on the day the shop moves in.
    /// </summary>
    public ServiceResult<ImportOutcome> ApplyCustomers(User user, ImportPreview preview)
    {
        if (!CanImport(user))
        {
            return ServiceResult<ImportOutcome>.Fail("Only the owner or a manager can import customers.");
        }

        if (!preview.CanApply)
        {
            return ServiceResult<ImportOutcome>.Fail(
                preview.ErrorCount > 0
                    ? "The file still has lines with a problem. Nothing was imported."
                    : "There is nothing to import in this file.");
        }

        try
        {
            var outcome = _db.Tx(connection =>
            {
                var result = new ImportOutcome { Kind = "Customers", Skipped = preview.SkippedCount };

                foreach (var item in preview.Customers)
                {
                    _db.Execute(connection, @"
INSERT INTO customers(name, phone, category, credit_limit_paisa, opening_balance_paisa,
                     balance_paisa, loyalty_points, stars, active, created_at)
VALUES(@name, @phone, @category, @limit, @opening, @opening, 0, 1, 1, @at);",
                        new
                        {
                            name = item.Name,
                            phone = item.Phone,
                            category = item.Category,
                            limit = item.CreditLimitPaisa,
                            opening = item.OpeningPaisa,
                            at = Clock.Now()
                        });

                    var id = (int)_db.NextId(connection);
                    result.Added++;

                    _audit.Record(connection, user, "Import", "customer_import", "customers",
                        id.ToString(CultureInfo.InvariantCulture),
                        newValue: $"{{\"name\":\"{item.Name}\",\"openingPaisa\":{item.OpeningPaisa}}}");
                }

                return result;
            });

            _log.Info($"Customers imported: {outcome.Added} added, {outcome.Skipped} skipped.");
            return ServiceResult<ImportOutcome>.Success(outcome);
        }
        catch (Exception exception)
        {
            _log.Error("The customer import failed.", exception);
            return ServiceResult<ImportOutcome>.Fail(
                "The import stopped and nothing was saved. " + exception.Message);
        }
    }

    // =========================== Supplier import ===========================

    public ImportPreview PreviewSuppliers(string csvText)
    {
        var preview = new ImportPreview { Kind = "Suppliers" };
        var table = CsvReader.Parse(csvText ?? string.Empty);

        if (table.Count == 0)
        {
            return preview;
        }

        var map = CsvReader.HeaderMap(table[0]);
        preview.HeaderLine = 1;
        preview.Headings.AddRange(map.Keys.OrderBy(key => key, StringComparer.Ordinal));

        if (!map.ContainsKey("name"))
        {
            preview.Rows.Add(Error(1, "headings", "The first line must have a \"Name\" column. Use the template."));
            return preview;
        }

        var names = ReadExistingSupplierNames();

        for (var index = 1; index < table.Count; index++)
        {
            var row = table[index];
            var line = index + 1;

            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string Field(string column) =>
                map.TryGetValue(column, out var position) ? CsvReader.At(row, position) : string.Empty;

            var name = Field("name");

            if (name.Length < 2)
            {
                preview.Rows.Add(Error(line, name, "The supplier name is empty."));
                continue;
            }

            if (names.Contains(name))
            {
                preview.Rows.Add(Skipped(line, name, "already in the shop"));
                continue;
            }

            if (!Money.TryParse(Field("openingpayable"), out var opening) || opening < 0)
            {
                preview.Rows.Add(Error(line, name, "Opening payable is not an amount of money."));
                continue;
            }

            var item = new SupplierImport
            {
                Name = name,
                Phone = Empty(Field("phone")),
                Agency = Empty(Field("agency")),
                OpeningPaisa = Money.ToPaisa(opening)
            };

            preview.Suppliers.Add(item);
            names.Add(name);

            preview.Rows.Add(new ImportRow
            {
                Line = line,
                Key = name,
                State = ImportState.Ready,
                Detail = item.OpeningPaisa > 0 ? "we owe " + Money.Format(item.OpeningPaisa) : "no opening payable"
            });
        }

        return preview;
    }

    public ServiceResult<ImportOutcome> ApplySuppliers(User user, ImportPreview preview)
    {
        if (!CanImport(user))
        {
            return ServiceResult<ImportOutcome>.Fail("Only the owner or a manager can import suppliers.");
        }

        if (!preview.CanApply)
        {
            return ServiceResult<ImportOutcome>.Fail(
                preview.ErrorCount > 0
                    ? "The file still has lines with a problem. Nothing was imported."
                    : "There is nothing to import in this file.");
        }

        try
        {
            var outcome = _db.Tx(connection =>
            {
                var result = new ImportOutcome { Kind = "Suppliers", Skipped = preview.SkippedCount };

                foreach (var item in preview.Suppliers)
                {
                    _db.Execute(connection, @"
INSERT INTO suppliers(name, phone, agency, opening_balance_paisa, balance_paisa, active, created_at)
VALUES(@name, @phone, @agency, @opening, @opening, 1, @at);",
                        new
                        {
                            name = item.Name,
                            phone = item.Phone,
                            agency = item.Agency,
                            opening = item.OpeningPaisa,
                            at = Clock.Now()
                        });

                    var id = (int)_db.NextId(connection);
                    result.Added++;

                    _audit.Record(connection, user, "Import", "supplier_import", "suppliers",
                        id.ToString(CultureInfo.InvariantCulture),
                        newValue: $"{{\"name\":\"{item.Name}\",\"openingPaisa\":{item.OpeningPaisa}}}");
                }

                return result;
            });

            _log.Info($"Suppliers imported: {outcome.Added} added, {outcome.Skipped} skipped.");
            return ServiceResult<ImportOutcome>.Success(outcome);
        }
        catch (Exception exception)
        {
            _log.Error("The supplier import failed.", exception);
            return ServiceResult<ImportOutcome>.Fail(
                "The import stopped and nothing was saved. " + exception.Message);
        }
    }

    // ========================== Bulk price change ==========================

    /// <summary>
    /// Works out every new price without writing anything. The owner reads the before → after column
    /// and presses Apply; this is the same shape as an import, for the same reason.
    /// </summary>
    public PricePreview PreviewPrices(User user, PriceChangeRequest request)
    {
        var preview = new PricePreview();

        if (!CanImport(user))
        {
            return preview;
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<PriceRow>(connection, @"
SELECT p.id AS id, p.name AS name, p.cost_paisa AS costPaisa, p.retail_paisa AS retailPaisa
FROM products p
WHERE p.active = 1 AND (@categoryId = 0 OR p.category_id = @categoryId)
ORDER BY p.name COLLATE NOCASE;",
                new { categoryId = request.CategoryId });

            foreach (var row in rows)
            {
                var newRetail = Change(row.RetailPaisa, request);
                var newCost = request.IncludeCost ? Change(row.CostPaisa, request) : row.CostPaisa;

                preview.Rows.Add(new PriceChangeRow
                {
                    ProductId = row.Id,
                    Name = row.Name,
                    OldRetailPaisa = row.RetailPaisa,
                    NewRetailPaisa = newRetail,
                    OldCostPaisa = row.CostPaisa,
                    NewCostPaisa = newCost,
                    CostChanges = request.IncludeCost
                });
            }

            return preview;
        }
        catch (Exception exception)
        {
            _log.Error("The price change could not be prepared.", exception);
            return preview;
        }
    }

    /// <summary>
    /// Applies the previewed change. A row whose new retail is below cost is **not written**: the shop
    /// would be selling at a loss on every bill, and that is the one mistake this feature could cause.
    /// </summary>
    public ServiceResult<PriceOutcome> ApplyPrices(User user, PricePreview preview)
    {
        if (!_permissions.Can(user, Modules.Products))
        {
            return ServiceResult<PriceOutcome>.Fail("You do not have permission to change prices.");
        }

        if (!preview.CanApply)
        {
            return ServiceResult<PriceOutcome>.Fail(
                preview.BelowCostCount > 0
                    ? "Some prices would fall below cost. Nothing was changed - lower the percentage."
                    : "There is nothing to change.");
        }

        try
        {
            var outcome = _db.Tx(connection =>
            {
                var result = new PriceOutcome { BelowCost = preview.BelowCostCount };

                foreach (var row in preview.Rows.Where(row => row.Changes && !row.BelowCost))
                {
                    _db.Execute(connection, @"
UPDATE products
SET retail_paisa = @retail, cost_paisa = @cost, updated_at = @at
WHERE id = @id;",
                        new { retail = row.NewRetailPaisa, cost = row.NewCostPaisa, id = row.ProductId, at = Clock.Now() });

                    result.Changed++;

                    _audit.Record(connection, user, "Inventory", "price_change", "products",
                        row.ProductId.ToString(CultureInfo.InvariantCulture),
                        oldValue: $"{{\"retailPaisa\":{row.OldRetailPaisa},\"costPaisa\":{row.OldCostPaisa}}}",
                        newValue: $"{{\"retailPaisa\":{row.NewRetailPaisa},\"costPaisa\":{row.NewCostPaisa}}}");
                }

                return result;
            });

            _log.Info($"Bulk price change: {outcome.Changed} product(s).");
            return ServiceResult<PriceOutcome>.Success(outcome);
        }
        catch (Exception exception)
        {
            _log.Error("The price change failed.", exception);
            return ServiceResult<PriceOutcome>.Fail("The prices could not be changed. Nothing was saved.");
        }
    }

    // ============================== Helpers ==============================

    /// <summary>Export and preview: reading data is enough; writing needs <see cref="CanImport"/>.</summary>
    private bool Allowed(User? user) =>
        user is not null && (_permissions.Can(user, Modules.Import) || _permissions.Can(user, Modules.Products));

    private bool CanImport(User? user) => user is not null && _permissions.Can(user, Modules.Import);

    /// <summary>
    /// One price after the owner's instruction. Percent uses <see cref="Calc.RoundMoney"/> so a price
    /// never drifts by a floating point paisa; the result is floored at zero, because a negative price
    /// is not a price.
    /// </summary>
    private static long Change(long oldPaisa, PriceChangeRequest request)
    {
        var value = request.Kind == PriceChangeKind.Percent
            ? oldPaisa * (1m + request.Amount / 100m)
            : oldPaisa + Money.ToPaisa(request.Amount);

        var rounded = Calc.RoundMoney(value);
        return rounded < 0 ? 0 : rounded;
    }

    private int? CategoryId(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string? name,
        Dictionary<string, int> cache)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var key = name.Trim();

        if (cache.TryGetValue(key, out var known))
        {
            return known;
        }

        var existing = _db.QuerySingle<int?>(connection,
            "SELECT id FROM categories WHERE name = @name COLLATE NOCASE;", new { name = key });

        if (existing is not null)
        {
            cache[key] = existing.Value;
            return existing;
        }

        // A category typed in the file is created once for the whole file, so a stock list with
        // fourteen "Tablet" lines does not make fourteen categories.
        _db.Execute(connection, "INSERT INTO categories(name, created_at) VALUES(@name, @at);",
            new { name = key, at = Clock.Now() });

        var created = (int)_db.NextId(connection);
        cache[key] = created;
        return created;
    }

    private HashSet<string> ReadExistingProductNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var connection = _db.Open();

            foreach (var name in _db.Query<string>(connection, "SELECT name FROM products;"))
            {
                names.Add(name.Trim());
            }
        }
        catch (Exception exception)
        {
            _log.Error("The existing product names could not be read.", exception);
        }

        return names;
    }

    private HashSet<string> ReadExistingBarcodes()
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var connection = _db.Open();

            foreach (var code in _db.Query<string>(connection,
                "SELECT barcode FROM products WHERE barcode IS NOT NULL AND barcode <> '';"))
            {
                codes.Add(code.Trim());
            }
        }
        catch (Exception exception)
        {
            _log.Error("The existing barcodes could not be read.", exception);
        }

        return codes;
    }

    private HashSet<string> ReadExistingPhones()
    {
        var phones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var connection = _db.Open();

            foreach (var phone in _db.Query<string>(connection,
                "SELECT phone FROM customers WHERE phone IS NOT NULL AND phone <> '';"))
            {
                phones.Add(phone.Trim());
            }
        }
        catch (Exception exception)
        {
            _log.Error("The existing phone numbers could not be read.", exception);
        }

        return phones;
    }

    private HashSet<string> ReadExistingCustomerNames() => ReadNames("customers");

    private HashSet<string> ReadExistingSupplierNames() => ReadNames("suppliers");

    private HashSet<string> ReadNames(string table)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var connection = _db.Open();

            // The table name comes from this file, never from a user, so it cannot be injected into.
            foreach (var name in _db.Query<string>(connection, "SELECT name FROM " + table + ";"))
            {
                names.Add(name.Trim());
            }
        }
        catch (Exception exception)
        {
            _log.Error($"The existing names in {table} could not be read.", exception);
        }

        return names;
    }

    private static ImportRow Error(int line, string key, string message) => new()
    {
        Line = line,
        Key = key,
        State = ImportState.Error,
        Message = message
    };

    private static ImportRow Skipped(int line, string key, string message) => new()
    {
        Line = line,
        Key = key,
        State = ImportState.Skipped,
        Message = message
    };

    private static string? Empty(string value) => value.Trim().Length == 0 ? null : value.Trim();

    /// <summary>A whole number typed as text; anything that is not one is treated as zero.</summary>
    private static int Whole(string text) =>
        int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? Math.Max(0, value)
            : 0;

    // ---- the flat rows the export queries read ----

    private sealed class ExportProductRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Generic { get; set; }

        public string? Brand { get; set; }

        public string? Barcode { get; set; }

        public string? Category { get; set; }

        public string? Pack { get; set; }

        public string? BaseUnit { get; set; }

        public int BoxStrips { get; set; }

        public int StripTablets { get; set; }

        public long CostPaisa { get; set; }

        public long RetailPaisa { get; set; }

        public int ReorderLevel { get; set; }

        public string? RackNo { get; set; }

        public double StockBase { get; set; }

        public string? NearestExpiry { get; set; }
    }

    private sealed class ExportCustomerRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string Category { get; set; } = "normal";

        public long OpeningPaisa { get; set; }

        public long BalancePaisa { get; set; }

        public long CreditLimitPaisa { get; set; }
    }

    private sealed class ExportSupplierRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string? Agency { get; set; }

        public long OpeningPaisa { get; set; }

        public long BalancePaisa { get; set; }
    }

    private sealed class PriceRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public long CostPaisa { get; set; }

        public long RetailPaisa { get; set; }
    }
}
