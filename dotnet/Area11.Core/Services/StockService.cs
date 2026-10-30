using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Stock write-off and count correction (Spec 19.1, §15.2.2, D-93).
///
/// The owner stands in front of the shelf with a box of expired strips in his hand. One press must:
///   • take the quantity **out of the stock** (or put it in, when the count found more);
///   • value the loss **at purchase cost**, never at selling price - that is the real money lost;
///   • write the stock ledger (`stock_movements`) and its own record (`stock_adjustments`);
///   • go into the black box;
///   • and **refuse** a removal bigger than the book says (D-93). The entered form stays intact;
///     an existing minus is reconciled by the separate physical stock-take, never by another removal.
///
/// What it will never do: edit or delete an old adjustment. A mistake is corrected by a second,
/// opposite entry with its own reason, so the loss and the correction both stay readable.
///
/// One honest warning for the owner: a write-off at cost lowers the profit report by that much. If
/// the goods were already counted as expired, writing them off twice would count the loss twice -
/// the history list is the place to check before pressing.
/// </summary>
public sealed class StockService
{
    public const string ReasonExpired = "expired";
    public const string ReasonDamaged = "damaged";
    public const string ReasonLost = "lost";
    public const string ReasonCountShort = "count_short";
    public const string ReasonCountExtra = "count_extra";
    public const string ReasonOther = "other";

    public static readonly string[] Reasons =
    {
        ReasonExpired, ReasonDamaged, ReasonLost, ReasonCountShort, ReasonCountExtra, ReasonOther
    };

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public StockService(
        Database db,
        AuditService audit,
        SettingsService settings,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>The live batches of one medicine, oldest expiry first - where the medicine actually is.</summary>
    public ServiceResult<List<BatchRow>> Batches(int productId)
    {
        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<BatchRow>(connection, @"
SELECT b.id AS id, b.product_id AS productId, b.batch_no AS batchNo, b.expiry_date AS expiryDate,
       b.qty_base AS qtyBase, b.cost_paisa AS costPaisa, s.name AS supplierName
FROM batches b
LEFT JOIN suppliers s ON s.id = b.supplier_id
WHERE b.product_id = @productId AND b.active = 1
ORDER BY (b.expiry_date IS NULL OR b.expiry_date = '') ASC, b.expiry_date ASC, b.id ASC;",
                new { productId });

            // The bands come from Settings, not from the code (F-29): change the days and the label
            // on this screen changes with the next refresh.
            var levels = _settings.ExpiryBands;

            foreach (var row in rows)
            {
                row.Levels = levels;
            }

            return ServiceResult<List<BatchRow>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The batches of medicine " + productId + " could not be read.", exception);
            return ServiceResult<List<BatchRow>>.Fail("The batches could not be loaded.");
        }
    }

    /// <summary>Writes the goods off (or corrects the count). Owner or manager only (19.1).</summary>
    public ServiceResult<WriteOffResult> WriteOff(WriteOffInput input, User user)
    {
        if (!_permissions.Can(user, Modules.Stock))
        {
            return ServiceResult<WriteOffResult>.Fail("You do not have permission to change stock.");
        }

        try
        {
            return _db.Tx(connection => Save(connection, input, user));
        }
        catch (ValidationException exception)
        {
            return ServiceResult<WriteOffResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The write-off could not be saved.", exception);
            return ServiceResult<WriteOffResult>.Fail("The write-off could not be saved. Nothing was changed.");
        }
    }

    /// <summary>The write-off history, newest first - which goods went where, and at what cost.</summary>
    public List<WriteOffRow> History(int limit = 200)
    {
        try
        {
            using var connection = _db.Open();

            return _db.Query<WriteOffRow>(connection, @"
SELECT a.id AS id, a.date AS date, p.name AS productName, b.batch_no AS batchNo,
       b.expiry_date AS expiryDate, a.direction AS direction, a.qty_base AS qtyBase,
       a.qty_entered AS qtyEntered, a.unit_entered AS unitEntered, a.value_paisa AS valuePaisa,
       a.reason AS reason, a.note AS note, u.name AS userName
FROM stock_adjustments a
JOIN products p ON p.id = a.product_id
LEFT JOIN batches b ON b.id = a.batch_id
LEFT JOIN users u ON u.id = a.user_id
ORDER BY a.id DESC
LIMIT @limit;", new { limit = Math.Clamp(limit, 1, 1000) });
        }
        catch (Exception exception)
        {
            _log.Error("The write-off history could not be read.", exception);
            return new List<WriteOffRow>();
        }
    }

    /// <summary>Money written off since midnight - the number the owner checks before closing.</summary>
    public long LossTodayPaisa()
    {
        try
        {
            using var connection = _db.Open();

            return Convert.ToInt64(_db.Scalar(connection, @"
SELECT IFNULL(SUM(CASE WHEN direction = 'out' THEN value_paisa ELSE -value_paisa END), 0)
FROM stock_adjustments
WHERE date >= date('now', 'localtime') || ' 00:00:00';") ?? 0L);
        }
        catch (Exception exception)
        {
            _log.Error("Today's write-off value could not be read.", exception);
            return 0L;
        }
    }

    // ------------------------------------------------------------------- saving

    private ServiceResult<WriteOffResult> Save(SqliteConnection connection, WriteOffInput input, User user)
    {
        if (input.QtyEntered <= 0)
        {
            throw new ValidationException("The quantity must be more than zero.");
        }

        var reason = (input.Reason ?? string.Empty).Trim().ToLowerInvariant();
        if (Array.IndexOf(Reasons, reason) < 0)
        {
            throw new ValidationException("Choose a reason for the write-off.");
        }

        var product = _db.QuerySingle<ProductRow>(connection, @"
SELECT id, name, base_unit AS baseUnit, box_strips AS boxStrips, strip_tablets AS stripTablets,
       cost_paisa AS costPaisa, retail_paisa AS retailPaisa
FROM products WHERE id = @id AND active = 1;", new { id = input.ProductId });

        if (product is null)
        {
            throw new ValidationException("That medicine is not in the list any more.");
        }

        var perUnit = PerUnit(product, input.Unit);
        var qtyBase = Calc.ToBase(input.QtyEntered, perUnit);

        if (!double.IsFinite(qtyBase) || qtyBase <= 0)
        {
            throw new ValidationException("The quantity must be a real number more than zero.");
        }
        if (UnitPolicy.RequiresWhole(_db, connection, product.BaseUnit) && !Qty.IsWhole(qtyBase))
        {
            throw new ValidationException(
                $"{product.Name}: stock corrections use whole {product.BaseUnit} units; ml is strength, not a stock division.");
        }

        // Which batch? The owner can pick one; otherwise the oldest expiry (the one to destroy first).
        var batch = input.BatchId is int wanted
            ? _db.QuerySingle<BatchDbRow>(connection,
                "SELECT id, batch_no AS batchNo, qty_base AS qtyBase, cost_paisa AS costPaisa, expiry_date AS expiryDate FROM batches WHERE id = @id AND product_id = @productId AND active = 1;",
                new { id = wanted, productId = product.Id })
            : _db.QuerySingle<BatchDbRow>(connection, @"
SELECT id, batch_no AS batchNo, qty_base AS qtyBase, cost_paisa AS costPaisa, expiry_date AS expiryDate
FROM batches WHERE product_id = @productId AND active = 1
ORDER BY (expiry_date IS NULL OR expiry_date = '') ASC, expiry_date ASC, id ASC LIMIT 1;",
                new { productId = product.Id });

        // Only "the count found more" puts stock back. Everything else takes it out.
        var direction = reason == ReasonCountExtra ? "in" : "out";

        if (batch is null && direction == "out")
        {
            throw new ValidationException(
                product.Name + " has no live batch, so there is nothing on the shelf to take out. "
                + "Use a count correction instead, or receive the stock first.");
        }

        var costPaisa = batch is not null && batch.CostPaisa > 0 ? batch.CostPaisa : product.CostPaisa;
        var valuePaisa = Calc.Round(qtyBase * costPaisa);
        var stockBefore = batch?.QtyBase ?? 0d;
        var warnings = new List<string>();

        if (direction == "in")
        {
            warnings.Add("This entry ADDS stock (" + Qty.Format(qtyBase) + " " + product.BaseUnit + "). Use it only when the count found more than the book.");
        }

        var stamp = Clock.Now();
        var batchId = batch?.Id;

        double stockAfter;
        if (batchId is int realBatchId)
        {
            if (direction == "out")
            {
                stockAfter = StockPolicy.Decrease(_db, connection, product.Id, realBatchId, qtyBase,
                    product.Name, product.BaseUnit, "write off");
            }
            else
            {
                _db.Execute(connection,
                    "UPDATE batches SET qty_base = qty_base + @qty WHERE id = @id;",
                    new { qty = qtyBase, id = realBatchId });
                stockAfter = stockBefore + qtyBase;
            }
        }
        else
        {
            // Only an inbound legacy count can reach this branch; outbound with no batch was
            // refused above. The physical stock-take is the path that creates a missing batch.
            stockAfter = stockBefore + qtyBase;
        }

        var movementType = reason is ReasonCountShort or ReasonCountExtra ? "adjust" : "writeoff";

        _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, ref_type, ref_code, note, user_id, created_at)
VALUES(@productId, @batchId, @type, @qty, 'stock_adjustment', NULL, @note, @userId, @at);",
            new
            {
                productId = product.Id,
                batchId,
                type = movementType,
                qty = direction == "out" ? -qtyBase : qtyBase,
                note = ReasonLabel(reason) + (string.IsNullOrWhiteSpace(input.Note) ? string.Empty : ": " + input.Note!.Trim()),
                userId = user.Id,
                at = stamp
            });

        var movementId = (int)_db.NextId(connection);

        _db.Execute(connection, @"
INSERT INTO stock_adjustments(date, product_id, batch_id, direction, qty_base, unit_entered, qty_entered,
                             value_paisa, reason, note, user_id, movement_id, created_at)
VALUES(@date, @productId, @batchId, @direction, @qtyBase, @unit, @qtyEntered,
       @value, @reason, @note, @userId, @movementId, @at);",
            new
            {
                date = stamp,
                productId = product.Id,
                batchId,
                direction,
                qtyBase,
                unit = string.IsNullOrWhiteSpace(input.Unit) ? product.BaseUnit : input.Unit.Trim().ToLowerInvariant(),
                qtyEntered = input.QtyEntered,
                value = valuePaisa,
                reason,
                note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note!.Trim(),
                userId = user.Id,
                movementId,
                at = stamp
            });

        var adjustmentId = (int)_db.NextId(connection);

        _audit.Record(connection, user, "Stock", direction == "out" ? "writeoff" : "count_extra", "stock_adjustments",
            adjustmentId.ToString(),
            oldValue: direction == "out" ? Qty.Format(stockBefore) : null,
            newValue: (direction == "out" ? "-" : "+") + Qty.Format(qtyBase) + " " + product.BaseUnit +
                      " (" + ReasonLabel(reason) + ", " + Money.Format(valuePaisa) + ")");

        return ServiceResult<WriteOffResult>.Success(new WriteOffResult
        {
            AdjustmentId = adjustmentId,
            LossPaisa = valuePaisa,
            QtyBase = direction == "out" ? -qtyBase : qtyBase,
            StockAfterBase = stockAfter,
            Warnings = warnings
        }, warnings.ToArray());
    }

    // ------------------------------------------------------------------ helpers

    private static double PerUnit(ProductRow product, string? unit)
    {
        var word = (unit ?? string.Empty).Trim().ToLowerInvariant();
        var perStrip = Math.Max(1, product.StripTablets);
        var perBox = Math.Max(1, product.BoxStrips) * perStrip;

        return word switch
        {
            "box" => product.BoxStrips > 0 ? perBox : 1,
            "strip" => product.StripTablets > 0 ? perStrip : 1,
            _ => 1
        };
    }

    private static string ReasonLabel(string reason) => reason switch
    {
        ReasonExpired => "Expired",
        ReasonDamaged => "Broken / dropped",
        ReasonLost => "Lost",
        ReasonCountShort => "Count was less",
        ReasonCountExtra => "Count was more",
        _ => "Other"
    };

    // -------------------------------------------------------------------- types

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

    private sealed class BatchDbRow
    {
        public int Id { get; set; }

        public string? BatchNo { get; set; }

        public double QtyBase { get; set; }

        public long CostPaisa { get; set; }

        public string? ExpiryDate { get; set; }
    }
}
