using Area11.WPF.Data;
using Area11.WPF.Models;
using Area11.WPF.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.WPF.Services;

/// <summary>
/// Stock-in. A purchase writes the bill (PINV), the batches and their expiry, the stock movement,
/// the supplier payable and the optional payment — all inside one transaction.
/// Sample / bonus lines still enter stock but cost nothing and never touch the supplier balance.
/// </summary>
public sealed class PurchaseService
{
    private readonly Database _db;
    private readonly LoggerService _log;
    private readonly AuditService _audit;
    private readonly NumberingService _numbering;

    public PurchaseService(Database db, LoggerService log, AuditService audit, NumberingService numbering)
    {
        _db = db;
        _log = log;
        _audit = audit;
        _numbering = numbering;
    }

    public OperationResult<string> Create(PurchaseInput input, string userName)
    {
        var warnings = new List<string>();
        try
        {
            if (input.Lines.Count == 0)
            {
                throw new ValidationException("Add at least one line to the purchase.");
            }

            var code = _db.Tx(connection =>
            {
                var supplier = _db.Query(connection,
                    "SELECT name FROM suppliers WHERE id = $id AND active = 1;",
                    reader => reader.Str("name"),
                    ("$id", input.SupplierId)).FirstOrDefault()
                    ?? throw new ValidationException("Select a supplier first.");

                var documentCode = _numbering.Take(connection, "purchase");
                var stamp = FormatHelper.Stamp();

                _db.Exec(connection, @"
                    INSERT INTO purchases(code, supplier_id, total_paisa, paid_paisa, note, created_at, created_by)
                    VALUES($code, $supplier, 0, 0, $note, $at, $user);",
                    ("$code", documentCode),
                    ("$supplier", input.SupplierId),
                    ("$note", string.IsNullOrWhiteSpace(input.Note) ? DBNull.Value : input.Note.Trim()),
                    ("$at", stamp),
                    ("$user", userName));

                var purchaseId = (int)_db.LastInsertId(connection);
                long totalPaisa = 0;

                foreach (var line in input.Lines)
                {
                    if (line.Quantity <= 0)
                    {
                        throw new ValidationException("Every purchase line needs a quantity of at least 1.");
                    }

                    var product = _db.Query(connection,
                        "SELECT name, box_strips, strip_units, cost_paisa, retail_paisa FROM products WHERE id = $id AND active = 1;",
                        reader => new
                        {
                            name = reader.Str("name"),
                            boxStrips = Math.Max(1, reader.Int("box_strips")),
                            stripUnits = Math.Max(1, reader.Int("strip_units")),
                            cost = reader.Long("cost_paisa"),
                            retail = reader.Long("retail_paisa")
                        },
                        ("$id", line.ProductId)).FirstOrDefault()
                        ?? throw new ValidationException("A selected product no longer exists.");

                    var qtyBase = ConvertToBase(line.Unit, line.Quantity, product.boxStrips, product.stripUnits);
                    var costPaisa = line.CostPaisa > 0 ? line.CostPaisa : product.cost;
                    var retailPaisa = line.RetailPaisa > 0 ? line.RetailPaisa : product.retail;

                    if (!DateHelper.TryNormalizeExpiry(line.Expiry, out var expiry))
                    {
                        warnings.Add($"{product.name}: expiry '{line.Expiry}' is not valid — batch saved without a usable expiry.");
                        expiry = string.Empty;
                    }

                    var batchNo = (line.BatchNo ?? string.Empty).Trim();
                    if (batchNo.Length == 0)
                    {
                        batchNo = "NO-BATCH";
                        warnings.Add($"{product.name}: no batch number given.");
                    }

                    _db.Exec(connection, @"
                        INSERT INTO batches(product_id, batch_no, expiry, qty_base, cost_paisa, retail_paisa, created_at)
                        VALUES($product, $batch, $expiry, $qty, $cost, $retail, $at);",
                        ("$product", line.ProductId),
                        ("$batch", batchNo),
                        ("$expiry", expiry),
                        ("$qty", qtyBase),
                        ("$cost", costPaisa),
                        ("$retail", retailPaisa),
                        ("$at", stamp));

                    var batchId = (int)_db.LastInsertId(connection);

                    var lineTotal = line.IsSample ? 0L : qtyBase * costPaisa;
                    totalPaisa += lineTotal;

                    _db.Exec(connection, @"
                        INSERT INTO purchase_items
                            (purchase_id, product_id, batch_id, unit, qty_base, cost_paisa, retail_paisa, line_total_paisa, is_sample)
                        VALUES($purchase, $product, $batch, $unit, $qty, $cost, $retail, $total, $sample);",
                        ("$purchase", purchaseId),
                        ("$product", line.ProductId),
                        ("$batch", batchId),
                        ("$unit", line.Unit),
                        ("$qty", qtyBase),
                        ("$cost", costPaisa),
                        ("$retail", retailPaisa),
                        ("$total", lineTotal),
                        ("$sample", line.IsSample ? 1 : 0));

                    _db.Exec(connection, @"
                        UPDATE products
                        SET stock_base = stock_base + $qty,
                            cost_paisa = $cost,
                            retail_paisa = $retail,
                            updated_at = $at
                        WHERE id = $id;",
                        ("$qty", qtyBase),
                        ("$cost", costPaisa),
                        ("$retail", retailPaisa),
                        ("$at", stamp),
                        ("$id", line.ProductId));

                    _db.Exec(connection, @"
                        INSERT INTO stock_movements(product_id, batch_id, type, qty_base, note, created_at, created_by)
                        VALUES($product, $batch, $type, $qty, $note, $at, $user);",
                        ("$product", line.ProductId),
                        ("$batch", batchId),
                        ("$type", line.IsSample ? "sample" : "purchase"),
                        ("$qty", qtyBase),
                        ("$note", line.IsSample ? "Sample / bonus (0 cost)" : $"Purchase {documentCode}"),
                        ("$at", stamp),
                        ("$user", userName));
                }

                _db.Exec(connection,
                    "UPDATE purchases SET total_paisa = $total WHERE id = $id;",
                    ("$total", totalPaisa),
                    ("$id", purchaseId));

                // A supplier bill is money we owe; paying part of it now reduces the payable.
                _db.Exec(connection,
                    "UPDATE suppliers SET balance_paisa = balance_paisa + $total WHERE id = $id;",
                    ("$total", totalPaisa),
                    ("$id", input.SupplierId));

                var paid = Math.Clamp(input.PaidPaisa, 0, totalPaisa);
                if (paid > 0)
                {
                    _db.Exec(connection, @"
                        INSERT INTO payments(purchase_id, supplier_id, amount_paisa, method, note, created_at, created_by)
                        VALUES($purchase, $supplier, $amount, 'cash', $note, $at, $user);",
                        ("$purchase", purchaseId),
                        ("$supplier", input.SupplierId),
                        ("$amount", paid),
                        ("$note", $"Paid with {documentCode}"),
                        ("$at", stamp),
                        ("$user", userName));

                    _db.Exec(connection,
                        "UPDATE suppliers SET balance_paisa = balance_paisa - $paid WHERE id = $id;",
                        ("$paid", paid),
                        ("$id", input.SupplierId));

                    _db.Exec(connection, "UPDATE purchases SET paid_paisa = $paid WHERE id = $id;",
                        ("$paid", paid),
                        ("$id", purchaseId));
                }

                _audit.Record(connection, userName, "Purchases", "create", "purchases", purchaseId, null,
                    new { code = documentCode, supplier, totalPaisa, paidPaisa = paid, lines = input.Lines.Count });

                return documentCode;
            });

            return OperationResult<string>.Success(code, warnings);
        }
        catch (ValidationException exception)
        {
            return OperationResult<string>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("Purchase could not be saved.", exception);
            return OperationResult<string>.Fail("The purchase could not be saved. Please try again.");
        }
    }

    public static int ConvertToBase(string? unit, int quantity, int boxStrips, int stripUnits)
    {
        var unitsPerBox = Math.Max(1, boxStrips) * Math.Max(1, stripUnits);
        var unitsPerStrip = Math.Max(1, stripUnits);

        return (unit ?? "base").Trim().ToLowerInvariant() switch
        {
            "box" => quantity * unitsPerBox,
            "strip" => quantity * unitsPerStrip,
            _ => quantity
        };
    }

    public OperationResult<List<BatchView>> GetBatches(int productId)
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query(connection, @"
                SELECT id, batch_no, expiry, qty_base, cost_paisa
                FROM batches
                WHERE product_id = $id AND qty_base <> 0
                ORDER BY CASE WHEN expiry = '' THEN 1 ELSE 0 END, expiry;",
                reader => new BatchView
                {
                    Id = reader.Int("id"),
                    BatchNo = reader.Str("batch_no"),
                    Expiry = reader.Str("expiry"),
                    QtyBase = reader.Long("qty_base"),
                    CostPaisa = reader.Long("cost_paisa")
                },
                ("$id", productId));

            return OperationResult<List<BatchView>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error($"Batches for product {productId} could not be loaded.", exception);
            return OperationResult<List<BatchView>>.Fail("Batches could not be loaded.");
        }
    }

    public sealed class BatchView
    {
        public int Id { get; set; }

        public string BatchNo { get; set; } = string.Empty;

        public string Expiry { get; set; } = string.Empty;

        public long QtyBase { get; set; }

        public long CostPaisa { get; set; }
    }
}
