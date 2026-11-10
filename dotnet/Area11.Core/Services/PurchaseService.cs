using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Stock-in (Spec 9.2). One transaction writes the PINV bill, the batch of every line
/// (with expiry), the stock movement, the supplier payable and the optional payment.
/// A sample / bonus line enters stock at cost 0 and never reaches the bill or the payable.
/// </summary>
public sealed class PurchaseService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly NumberingService _numbering;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public PurchaseService(
        Database db,
        AuditService audit,
        NumberingService numbering,
        SettingsService settings,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _numbering = numbering;
        _settings = settings;
        _permissions = permissions;
        _log = log;
    }

    public ServiceResult<PurchaseResult> Create(PurchaseInput input, User user)
    {
        if (!_permissions.Can(user, Modules.Purchases))
        {
            return ServiceResult<PurchaseResult>.Fail("You do not have permission to enter purchases.");
        }

        if (input.Lines.Count == 0)
        {
            return ServiceResult<PurchaseResult>.Fail("Add at least one line to the purchase.");
        }

        try
        {
            var result = _db.Tx(connection => Save(connection, input, user));
            return result;
        }
        catch (ValidationException exception)
        {
            return ServiceResult<PurchaseResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The purchase could not be saved.", exception);
            return ServiceResult<PurchaseResult>.Fail("The purchase could not be saved. Please try again.");
        }
    }

    /// <summary>
    /// Portion 11: the saved purchase as an invoice for the file. Read from the database, so a
    /// reprint never depends on the lines still sitting on the screen; read-only, changes nothing.
    /// </summary>
    public ServiceResult<ReceiptDocument> BuildInvoiceDocument(int purchaseId, User user)
    {
        if (!_permissions.Can(user, Modules.Purchases))
        {
            return ServiceResult<ReceiptDocument>.Fail("You do not have permission to see the purchases.");
        }

        try
        {
            using var connection = _db.Open();
            var header = _db.QuerySingle<PurchaseInvoiceHeader>(connection, @"
SELECT p.code AS code, p.supplier_invoice_no AS supplierInvoiceNo, p.date AS date,
       p.total_paisa AS totalPaisa, p.paid_paisa AS paidPaisa, p.due_paisa AS duePaisa,
       s.name AS supplierName
FROM purchases p
JOIN suppliers s ON s.id = p.supplier_id
WHERE p.id = @id;", new { id = purchaseId });
            if (header is null)
            {
                return ServiceResult<ReceiptDocument>.Fail("That purchase no longer exists.");
            }

            var lines = _db.Query<PurchaseInvoiceLine>(connection, @"
SELECT p.name AS productName, i.qty_entered AS qtyEntered, i.unit AS unit,
       i.batch_no AS batchNo, i.expiry_date AS expiryDate,
       i.line_total_paisa AS lineTotalPaisa,
       CASE WHEN b.is_sample = 1 THEN 1 ELSE 0 END AS isSample
FROM purchase_items i
JOIN products p ON p.id = i.product_id
LEFT JOIN batches b ON b.id = i.batch_id
WHERE i.purchase_id = @id
ORDER BY i.id;", new { id = purchaseId });

            var width = ReceiptService.ColumnsForMillimetres(_settings.GetInt("printer.width", 58));
            var document = new ReceiptDocument
            {
                Title = "Purchase " + header.Code,
                Width = width,
                AutoCut = _settings.GetBool("printer.autoCut", true),
                Copies = 1
            };

            void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) =>
                document.Lines.Add(new ReceiptLine(ReceiptService.Fit(text, width), align, bold, big));

            void Pair(string left, string right, bool bold = false) =>
                document.Lines.Add(new ReceiptLine(ReceiptService.TwoColumns(left, right, width), ReceiptAlign.Left, bold));

            var shop = _settings.GetString("store.name");
            if (!string.IsNullOrWhiteSpace(shop))
            {
                Add(shop!.Trim(), ReceiptAlign.Center, bold: true);
            }

            Add("PURCHASE INVOICE", ReceiptAlign.Center, bold: true, big: true);
            Add(header.Code, ReceiptAlign.Center, bold: true);
            Add("Supplier: " + header.SupplierName);
            if (!string.IsNullOrWhiteSpace(header.SupplierInvoiceNo))
            {
                Add("Their bill: " + header.SupplierInvoiceNo!.Trim());
            }

            if (!string.IsNullOrWhiteSpace(header.Date))
            {
                Add("Date: " + header.Date!.Trim());
            }

            document.Lines.Add(ReceiptLine.Sep(width));

            foreach (var line in lines)
            {
                var head = line.ProductName + (line.IsSample ? " (free)" : string.Empty);
                Add(head, ReceiptAlign.Left, bold: true);
                Pair("    " + line.QtyLabel, Money.Format(line.LineTotalPaisa));
                var where = (line.BatchNo ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(line.ExpiryDate))
                {
                    where += (where.Length == 0 ? string.Empty : "  ") + "exp " + line.ExpiryDate!.Trim();
                }

                if (where.Length > 0)
                {
                    Add("    " + where);
                }
            }

            document.Lines.Add(ReceiptLine.Sep(width));
            Pair("Total", Money.Format(header.TotalPaisa), bold: true);
            if (header.PaidPaisa > 0)
            {
                Pair("Paid", Money.Format(header.PaidPaisa));
                Pair("Due", Money.Format(header.DuePaisa), header.DuePaisa > 0);
            }

            return ServiceResult<ReceiptDocument>.Success(document);
        }
        catch (Exception exception)
        {
            _log.Error("The purchase invoice could not be prepared.", exception);
            return ServiceResult<ReceiptDocument>.Fail("The purchase invoice could not be prepared. " + exception.Message);
        }
    }

    public ServiceResult<List<BatchView>> GetBatches(int productId)
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<BatchView>(connection, @"
SELECT id, product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa, is_sample
FROM batches
WHERE product_id = @productId AND active = 1 AND qty_base <> 0
ORDER BY CASE WHEN expiry_date IS NULL OR expiry_date = '' THEN 1 ELSE 0 END, expiry_date, id;",
                new { productId });

            return ServiceResult<List<BatchView>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error($"Batches of product {productId} could not be loaded.", exception);
            return ServiceResult<List<BatchView>>.Fail("Batches could not be loaded.");
        }
    }

    private ServiceResult<PurchaseResult> Save(SqliteConnection connection, PurchaseInput input, User user)
    {
        var supplier = _db.QuerySingle<Supplier>(connection,
            "SELECT id, name, balance_paisa FROM suppliers WHERE id = @id AND active = 1;",
            new { id = input.SupplierId })
            ?? throw new ValidationException("Select a supplier first.");

        var stamp = Clock.Now();
        var code = _numbering.Take(connection, "purchase");
        var warnings = new List<string>();

        _db.Execute(connection, @"
INSERT INTO purchases(code, supplier_id, supplier_invoice_no, date, subtotal_paisa, discount_paisa,
                      total_paisa, paid_paisa, due_paisa, notes, user_id, created_at)
VALUES(@code, @supplierId, @invoiceNo, @date, 0, 0, 0, 0, 0, @notes, @userId, @createdAt);",
            new
            {
                code,
                supplierId = input.SupplierId,
                invoiceNo = string.IsNullOrWhiteSpace(input.SupplierInvoiceNo) ? null : input.SupplierInvoiceNo.Trim(),
                date = stamp,
                notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                userId = user.Id,
                createdAt = stamp
            });

        var purchaseId = (int)_db.NextId(connection);

        // The owner's own purchase questions (Portion 18): validated and stored in the same
        // transaction as the purchase, so a missing required answer refuses the whole post.
        var purchaseFields = new CustomFieldService(_db, _audit, _permissions, _log);
        var purchaseAnswers = purchaseFields.SaveNewRecordValuesInTransaction(
            connection, user, CustomFieldEntity.Purchase, purchaseId, input.CustomAnswers);
        if (!purchaseAnswers.Ok) throw new ValidationException(purchaseAnswers.Error ?? "The purchase's extra answers could not be saved.");

        long totalPaisa = 0;
        double totalUnits = 0;

        foreach (var line in input.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new ValidationException("Every purchase line needs a quantity greater than zero.");
            }

            var product = _db.QuerySingle<ProductRow>(connection,
                "SELECT id, name, base_unit, box_strips, strip_tablets, cost_paisa, retail_paisa FROM products WHERE id = @id AND active = 1;",
                new { id = line.ProductId })
                ?? throw new ValidationException("A selected product no longer exists.");

            var unit = (line.Unit ?? "box").Trim().ToLowerInvariant();
            var unitsPerSelected = unit switch
            {
                "box" => product.BoxStrips > 0 ? (double)(Math.Max(1, product.BoxStrips) * Math.Max(1, product.StripTablets)) : 1d,
                "strip" => (double)Math.Max(1, product.StripTablets),
                _ => 1d
            };

            var qtyBase = Calc.ToBase(line.Quantity, unitsPerSelected);
            if (!double.IsFinite(qtyBase) || qtyBase <= 0)
            {
                throw new ValidationException("Every purchase line needs a real quantity greater than zero.");
            }
            if (UnitPolicy.RequiresWhole(_db, connection, product.BaseUnit) && !Qty.IsWhole(qtyBase))
            {
                throw new ValidationException(
                    $"{product.Name}: receive whole {product.BaseUnit} units; ml is strength, not a stock division.");
            }

            var batchNo = (line.BatchNo ?? string.Empty).Trim();
            if (batchNo.Length == 0)
            {
                batchNo = "NO-BATCH";
                warnings.Add($"{product.Name}: no batch number was entered.");
            }

            if (!Expiry.TryNormalize(line.Expiry, out var expiryDate, out var expiryYm))
            {
                expiryDate = string.Empty;
                expiryYm = string.Empty;
                warnings.Add($"{product.Name}: expiry '{line.Expiry}' is not valid — the batch has no expiry.");
            }

            var costPaisa = line.IsSample ? 0 : Math.Max(0, line.CostPaisa);
            var retailPaisa = line.RetailPaisa > 0 ? line.RetailPaisa : product.RetailPaisa;
            var lineTotal = line.IsSample ? 0 : Calc.Round(costPaisa * qtyBase);

            _db.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa,
                    is_sample, supplier_id, purchase_id, active, created_at)
VALUES(@productId, @batchNo, @expiryDate, @expiryYm, @qty, @cost, @retail,
       @isSample, @supplierId, @purchaseId, 1, @at);",
                new
                {
                    productId = product.Id,
                    batchNo,
                    expiryDate = expiryDate.Length == 0 ? null : expiryDate,
                    expiryYm = expiryYm.Length == 0 ? null : expiryYm,
                    qty = qtyBase,
                    cost = costPaisa,
                    retail = retailPaisa,
                    isSample = line.IsSample ? 1 : 0,
                    supplierId = input.SupplierId,
                    purchaseId,
                    at = stamp
                });

            var batchId = (int)_db.NextId(connection);

            _db.Execute(connection, @"
INSERT INTO purchase_items(purchase_id, product_id, batch_id, qty_entered, unit, qty_base,
                           cost_paisa, retail_paisa, line_total_paisa, batch_no, expiry_date)
VALUES(@purchaseId, @productId, @batchId, @qtyEntered, @unit, @qtyBase,
       @cost, @retail, @lineTotal, @batchNo, @expiryDate);",
                new
                {
                    purchaseId,
                    productId = product.Id,
                    batchId,
                    qtyEntered = line.Quantity,
                    unit,
                    qtyBase,
                    cost = costPaisa,
                    retail = retailPaisa,
                    lineTotal,
                    batchNo,
                    expiryDate = expiryDate.Length == 0 ? null : expiryDate
                });

            _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, note, user_id, created_at)
VALUES(@productId, @batchId, @type, @qty, @note, @userId, @at);",
                new
                {
                    productId = product.Id,
                    batchId,
                    type = line.IsSample ? "sample" : "purchase",
                    qty = qtyBase,
                    note = line.IsSample ? $"Sample {code}" : $"Purchase {code}",
                    userId = user.Id,
                    at = stamp
                });

            totalPaisa += lineTotal;
            totalUnits += qtyBase;

            if (retailPaisa > 0)
            {
                _db.Execute(connection,
                    "UPDATE products SET retail_paisa = @retail, updated_at = @at WHERE id = @id;",
                    new { retail = retailPaisa, at = stamp, id = product.Id });
            }
        }

        var paid = Math.Clamp(input.PaidPaisa, 0, totalPaisa);

        _db.Execute(connection, @"
UPDATE purchases SET subtotal_paisa = @total, total_paisa = @total, paid_paisa = @paid,
                     due_paisa = @due WHERE id = @id;",
            new { total = totalPaisa, paid, due = totalPaisa - paid, id = purchaseId });

        // A supplier bill is money we owe; paying part of it now reduces the payable.
        _db.Execute(connection,
            "UPDATE suppliers SET balance_paisa = balance_paisa + @total WHERE id = @id;",
            new { total = totalPaisa, id = input.SupplierId });

        if (paid > 0)
        {
            _db.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, purchase_id, supplier_id, user_id, note)
VALUES(@at, 'cash', @amount, @purchaseId, @supplierId, @userId, @note);",
                new
                {
                    at = stamp,
                    amount = paid,
                    purchaseId,
                    supplierId = input.SupplierId,
                    userId = user.Id,
                    note = $"Purchase {code}"
                });

            _db.Execute(connection,
                "UPDATE suppliers SET balance_paisa = balance_paisa - @paid WHERE id = @id;",
                new { paid, id = input.SupplierId });
        }

        _audit.Record(connection, user, "Purchases", "purchase_create", "purchases", purchaseId.ToString(),
            newValue: $"{{\"code\":\"{code}\",\"supplier\":\"{supplier.Name}\",\"total\":{totalPaisa},\"paid\":{paid},\"lines\":{input.Lines.Count}}}");

        return ServiceResult<PurchaseResult>.Success(new PurchaseResult
        {
            PurchaseId = purchaseId,
            Code = code,
            TotalPaisa = totalPaisa,
            Units = totalUnits,
            Warnings = warnings
        }, warnings.ToArray());
    }

    private sealed class PurchaseInvoiceHeader
    {
        public string Code { get; set; } = string.Empty;

        public string? SupplierInvoiceNo { get; set; }

        public string? Date { get; set; }

        public long TotalPaisa { get; set; }

        public long PaidPaisa { get; set; }

        public long DuePaisa { get; set; }

        public string SupplierName { get; set; } = string.Empty;
    }

    private sealed class ProductRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string BaseUnit { get; set; } = "tablet";

        public int BoxStrips { get; set; }

        public int StripTablets { get; set; }

        public long CostPaisa { get; set; }

        public long RetailPaisa { get; set; }
    }
}
