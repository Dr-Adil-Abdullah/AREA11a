using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Stock-take, the physical count (Spec 19.2, U-30, F-26).
///
/// How the owner actually uses it: the shop closes, he opens a session `ST-0001`, and the app writes
/// down **the book stock at that moment** for the scope he chose (whole shop, one room, one
/// category). Then he walks the shelves and types what he can hold in his hand. The screen shows
/// **book / counted / difference / value** side by side, so a wrong row is visible before anything is
/// changed.
///
/// Only **Apply** touches stock, only the **owner** may press it, and only **once** (19.2.4). Apply
/// sets every batch to the counted number, writes one `adjust` movement per line with the signed
/// difference, and values the shortage and the excess **at the frozen cost** of the session - not at
/// today's cost, which a purchase might have changed while he was counting.
///
/// A medicine found on the shelf that the book has never seen is not a problem: it gets a batch
/// `GINTI-ST-0001` at Apply, so the stock exists from that day on and the count is complete.
///
/// Negative book stock is corrected like any other number: the non-negative physical count wins
/// (D-93). This absolute-count door is deliberately separate from ordinary stock decrements.
/// </summary>
public sealed class StockTakeService
{
    private const int MinimumReasonLength = 3;

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly NumberingService _numbering;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public StockTakeService(
        Database db,
        AuditService audit,
        NumberingService numbering,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _numbering = numbering;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Opens a session and freezes the book for its scope (19.2.1).</summary>
    public ServiceResult<StockTakeSession> Open(User? user, string? room = null, int? categoryId = null, string? note = null)
    {
        if (!_permissions.Can(user, Modules.StockTake))
        {
            return ServiceResult<StockTakeSession>.Fail("You do not have permission to run a stock-take.");
        }

        try
        {
            return _db.Tx(connection => OpenInside(connection, user!, room, categoryId, note));
        }
        catch (ValidationException exception)
        {
            return ServiceResult<StockTakeSession>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The stock-take session could not be opened.", exception);
            return ServiceResult<StockTakeSession>.Fail("The stock-take could not be opened.");
        }
    }

    private ServiceResult<StockTakeSession> OpenInside(
        SqliteConnection connection, User user, string? room, int? categoryId, string? note)
    {
        var cleanRoom = string.IsNullOrWhiteSpace(room) ? null : room!.Trim();
        var stamp = Clock.Now();
        var code = _numbering.Take(connection, "stockTake");

        _db.Execute(connection, @"
INSERT INTO stock_takes(code, date, status, room, category_id, note, user_id, created_at)
VALUES(@code, @date, 'open', @room, @categoryId, @note, @userId, @at);",
            new
            {
                code,
                date = stamp,
                room = cleanRoom,
                categoryId,
                note = string.IsNullOrWhiteSpace(note) ? null : note!.Trim(),
                userId = user.Id,
                at = stamp
            });

        var sessionId = (int)_db.NextId(connection);

        // The book, frozen now: every live batch in scope. Batches are the only place stock lives,
        // so the sheet is built from batches, not from products - a medicine with two batches is
        // two rows, because the owner counts two separate boxes.
        var count = _db.Execute(connection, @"
INSERT INTO stock_take_items(stock_take_id, product_id, batch_id, system_qty_base, system_cost_paisa, created_at)
SELECT @sessionId, b.product_id, b.id, b.qty_base,
       CASE WHEN b.cost_paisa > 0 THEN b.cost_paisa ELSE p.cost_paisa END,
       @at
FROM batches b
JOIN products p ON p.id = b.product_id
WHERE b.active = 1 AND p.active = 1
  AND (@room IS NULL OR IFNULL(p.room, '') = @room)
  AND (@categoryId IS NULL OR p.category_id = @categoryId)
ORDER BY p.name COLLATE NOCASE, b.expiry_date;",
            new { sessionId, room = cleanRoom, categoryId, at = stamp });

        if (count == 0)
        {
            // Nothing to count is a refusal, not an empty session: an empty ST number would sit in
            // the ledger for ever and teach the owner nothing.
            throw new ValidationException(
                cleanRoom is null && categoryId is null
                    ? "There is no live stock to count yet. Receive stock first."
                    : "No live stock in that scope. Choose another room or category.");
        }

        _db.Execute(connection, "UPDATE stock_takes SET items_count = @count WHERE id = @id;",
            new { count, id = sessionId });

        _audit.Record(connection, user, "StockTake", "stock_take_open", "stock_takes", code,
            oldValue: null, newValue: count + " batches frozen" + (cleanRoom is null ? string.Empty : " in " + cleanRoom));

        return ServiceResult<StockTakeSession>.Success(LoadSession(connection, sessionId)!);
    }

    /// <summary>
    /// Adds a medicine the book has never seen (found on the shelf, no batch at all). It is counted
    /// later like any other line and becomes a `GINTI-ST-xxxx` batch at Apply (19.2.3).
    /// </summary>
    public ServiceResult AddFound(User? user, int sessionId, int productId)
    {
        if (!_permissions.Can(user, Modules.StockTake))
        {
            return ServiceResult.Fail("You do not have permission to run a stock-take.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var session = LoadSession(connection, sessionId)
                    ?? throw new ValidationException("That stock-take was not found.");

                if (!session.IsOpen)
                {
                    throw new ValidationException("Only an open stock-take can be changed.");
                }

                var product = _db.QuerySingle<string>(connection,
                    "SELECT name FROM products WHERE id = @id AND active = 1;", new { id = productId })
                    ?? throw new ValidationException("That medicine is not in the list any more.");

                var already = _db.QuerySingle<int?>(connection, @"
SELECT id FROM stock_take_items
WHERE stock_take_id = @sessionId AND product_id = @productId AND batch_id IS NULL;",
                    new { sessionId, productId });

                if (already is not null)
                {
                    throw new ValidationException(product + " is already on this sheet.");
                }

                _db.Execute(connection, @"
INSERT INTO stock_take_items(stock_take_id, product_id, batch_id, system_qty_base, system_cost_paisa, created_at)
VALUES(@sessionId, @productId, NULL, 0, (SELECT cost_paisa FROM products WHERE id = @productId), @at);",
                    new { sessionId, productId, at = Clock.Now() });

                _db.Execute(connection, @"
UPDATE stock_takes SET items_count = (SELECT COUNT(1) FROM stock_take_items WHERE stock_take_id = @sessionId)
WHERE id = @sessionId;", new { sessionId });

                return ServiceResult.Success();
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("A found medicine could not be added to the stock-take.", exception);
            return ServiceResult.Fail("The medicine could not be added to the sheet.");
        }
    }

    /// <summary>Writes one counted number. Nothing in stock changes yet - Apply does that.</summary>
    public ServiceResult Count(User? user, int lineId, double qtyEntered, string? unit, string? note)
    {
        if (!_permissions.Can(user, Modules.StockTake))
        {
            return ServiceResult.Fail("You do not have permission to run a stock-take.");
        }

        if (!double.IsFinite(qtyEntered) || qtyEntered < 0)
        {
            return ServiceResult.Fail("A physical count must be a real number of zero or more.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var line = _db.QuerySingle<LineDbRow>(connection, @"
SELECT i.id, i.stock_take_id AS sessionId, i.product_id AS productId, i.system_qty_base AS systemQtyBase,
       i.system_cost_paisa AS costPaisa, t.status,
       p.box_strips AS boxStrips, p.strip_tablets AS stripTablets, p.base_unit AS baseUnit
FROM stock_take_items i
JOIN stock_takes t ON t.id = i.stock_take_id
JOIN products p ON p.id = i.product_id
WHERE i.id = @id;", new { id = lineId })
                    ?? throw new ValidationException("That line is not on the sheet any more.");

                if (!string.Equals(line.Status, "open", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException("This stock-take is already " + line.Status + "; the sheet is locked.");
                }

                var perUnit = PerUnit(line, unit);
                var countedBase = Calc.ToBase(qtyEntered, perUnit);
                if (!double.IsFinite(countedBase) || countedBase < 0)
                {
                    throw new ValidationException("A physical count must be a real number of zero or more.");
                }
                if (UnitPolicy.RequiresWhole(_db, connection, line.BaseUnit) && !Qty.IsWhole(countedBase))
                {
                    throw new ValidationException(
                        $"{line.BaseUnit}: count whole {line.BaseUnit} units; ml is strength, not a stock division.");
                }
                var diff = countedBase - line.SystemQtyBase;
                var value = Calc.Round(diff * line.CostPaisa);

                _db.Execute(connection, @"
UPDATE stock_take_items
SET counted_qty_base = @counted, unit_entered = @unit, qty_entered = @entered,
    diff_qty_base = @diff, value_paisa = @value, counted_by = @userId, counted_at = @at, note = @note
WHERE id = @id;",
                    new
                    {
                        counted = countedBase,
                        unit = string.IsNullOrWhiteSpace(unit) ? line.BaseUnit : unit!.Trim().ToLowerInvariant(),
                        entered = qtyEntered,
                        diff,
                        value,
                        userId = user?.Id,
                        at = Clock.Now(),
                        note = string.IsNullOrWhiteSpace(note) ? null : note!.Trim(),
                        id = lineId
                    });

                RefreshTotals(connection, line.SessionId);
                return ServiceResult.Success();
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The count of line " + lineId + " could not be saved.", exception);
            return ServiceResult.Fail("The count could not be saved.");
        }
    }

    /// <summary>Clears a count that was typed on the wrong row (the line stays, the number goes).</summary>
    public ServiceResult ClearCount(User? user, int lineId)
    {
        if (!_permissions.Can(user, Modules.StockTake))
        {
            return ServiceResult.Fail("You do not have permission to run a stock-take.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var sessionId = _db.QuerySingle<int?>(connection,
                    "SELECT i.stock_take_id FROM stock_take_items i JOIN stock_takes t ON t.id = i.stock_take_id "
                    + "WHERE i.id = @id AND t.status = 'open';", new { id = lineId })
                    ?? throw new ValidationException("That line is not on an open sheet any more.");

                _db.Execute(connection, @"
UPDATE stock_take_items
SET counted_qty_base = NULL, diff_qty_base = NULL, value_paisa = NULL,
    unit_entered = NULL, qty_entered = NULL, counted_by = NULL, counted_at = NULL, note = NULL
WHERE id = @id;", new { id = lineId });

                RefreshTotals(connection, sessionId);
                return ServiceResult.Success();
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The count of line " + lineId + " could not be cleared.", exception);
            return ServiceResult.Fail("The count could not be cleared.");
        }
    }

    /// <summary>
    /// Applies the count (19.2.3). Owner only, once only. Every counted line sets its batch to the
    /// counted number; a found medicine gets its `GINTI-ST-xxxx` batch.
    /// </summary>
    public ServiceResult<StockTakeResult> Apply(User? user, int sessionId, string? note = null)
    {
        if (user is null || !user.IsOwner)
        {
            return ServiceResult<StockTakeResult>.Fail("Only the owner can apply a stock-take.");
        }

        try
        {
            return _db.Tx(connection => ApplyInside(connection, user, sessionId, note));
        }
        catch (ValidationException exception)
        {
            return ServiceResult<StockTakeResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("Stock-take " + sessionId + " could not be applied.", exception);
            return ServiceResult<StockTakeResult>.Fail("The stock-take could not be applied. Nothing was changed.");
        }
    }

    private ServiceResult<StockTakeResult> ApplyInside(SqliteConnection connection, User user, int sessionId, string? note)
    {
        var session = LoadSession(connection, sessionId)
            ?? throw new ValidationException("That stock-take was not found.");

        if (!session.IsOpen)
        {
            throw new ValidationException("This stock-take is already " + session.Status + "; it can be applied only once.");
        }

        var lines = _db.Query<ApplyRow>(connection, @"
SELECT i.id, i.product_id AS productId, i.batch_id AS batchId, i.system_qty_base AS systemQtyBase,
       i.counted_qty_base AS countedQtyBase, i.system_cost_paisa AS costPaisa,
       i.diff_qty_base AS diffQtyBase, i.value_paisa AS valuePaisa,
       p.name AS productName, p.base_unit AS baseUnit
FROM stock_take_items i
JOIN products p ON p.id = i.product_id
WHERE i.stock_take_id = @sessionId AND i.counted_qty_base IS NOT NULL
ORDER BY i.id;", new { sessionId });

        if (lines.Count == 0)
        {
            throw new ValidationException("Nothing has been counted yet, so there is nothing to apply.");
        }

        var stamp = Clock.Now();
        var shortPaisa = 0L;
        var extraPaisa = 0L;
        var diffCount = 0;
        var newBatches = 0;
        var warnings = new List<string>();

        foreach (var line in lines)
        {
            var counted = line.CountedQtyBase ?? 0d;
            if (!double.IsFinite(counted) || counted < 0)
            {
                throw new ValidationException(
                    line.ProductName + ": the physical count must be a real number of zero or more. Nothing was applied.");
            }
            if (UnitPolicy.RequiresWhole(_db, connection, line.BaseUnit) && !Qty.IsWhole(counted))
            {
                throw new ValidationException(
                    line.ProductName + ": apply whole " + line.BaseUnit + " units; ml is strength, not a stock division. Nothing was applied.");
            }

            var diff = counted - line.SystemQtyBase;
            var batchId = line.BatchId;

            if (batchId is null)
            {
                // Found on the shelf, never received. The count is the only evidence it exists.
                if (counted <= 0)
                {
                    continue;
                }

                var batchNo = "GINTI-" + session.Code;
                _db.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa, is_sample, active, created_at)
VALUES(@productId, @batchNo, NULL, NULL, 0,
       (SELECT cost_paisa FROM products WHERE id = @productId),
       (SELECT retail_paisa FROM products WHERE id = @productId), 0, 1, @at);",
                    new { productId = line.ProductId, batchNo, at = stamp });

                batchId = (int)_db.NextId(connection);
                newBatches++;
            }

            _db.Execute(connection, "UPDATE batches SET qty_base = @qty WHERE id = @id;",
                new { qty = counted, id = batchId.Value });

            _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, ref_type, ref_id, ref_code, note, user_id, created_at)
VALUES(@productId, @batchId, 'adjust', @diff, 'stock_take', @sessionId, @code, @note, @userId, @at);",
                new
                {
                    productId = line.ProductId,
                    batchId = batchId.Value,
                    diff,
                    sessionId,
                    code = session.Code,
                    note = "Count " + Qty.Format(counted) + " " + line.BaseUnit + " (book " + Qty.Format(line.SystemQtyBase) + ")",
                    userId = user.Id,
                    at = stamp
                });

            if (Math.Abs(diff) > 0.0005)
            {
                diffCount++;

                if (diff < 0)
                {
                    shortPaisa += Math.Abs(Calc.Round(diff * line.CostPaisa));
                }
                else
                {
                    extraPaisa += Calc.Round(diff * line.CostPaisa);
                }
            }
        }

        // A line that was never counted is not silently zeroed: it keeps its book number and is
        // listed in the warning, because "not counted" and "counted zero" are different facts.
        var skipped = _db.QuerySingle<int>(connection, @"
SELECT COUNT(1) FROM stock_take_items WHERE stock_take_id = @sessionId AND counted_qty_base IS NULL;",
            new { sessionId });

        if (skipped > 0)
        {
            warnings.Add(skipped + " line(s) were never counted - their book stock was left untouched.");
        }

        if (newBatches > 0)
        {
            warnings.Add(newBatches + " medicine(s) were found on the shelf and given a GINTI batch.");
        }

        _db.Execute(connection, @"
UPDATE stock_takes
SET status = 'applied', diff_count = @diffCount, short_paisa = @shortPaisa, extra_paisa = @extraPaisa,
    applied_at = @at, applied_by = @userId,
    note = CASE WHEN @note IS NULL THEN note ELSE @note END
WHERE id = @id;",
            new
            {
                diffCount,
                shortPaisa,
                extraPaisa,
                at = stamp,
                userId = user.Id,
                note = string.IsNullOrWhiteSpace(note) ? null : note!.Trim(),
                id = sessionId
            });

        _audit.Record(connection, user, "StockTake", "stock_take_apply", "stock_takes", session.Code,
            oldValue: "open",
            newValue: "applied: " + diffCount + " difference(s), short " + Money.Format(shortPaisa)
                       + ", extra " + Money.Format(extraPaisa));

        // The stock has just moved under the alert board's feet; nothing else to do here, because
        // the board reads the batches live on every page change.

        return ServiceResult<StockTakeResult>.Success(new StockTakeResult
        {
            Code = session.Code,
            DiffCount = diffCount,
            ShortPaisa = shortPaisa,
            ExtraPaisa = extraPaisa,
            NewBatches = newBatches,
            Warnings = warnings
        }, warnings.ToArray());
    }

    /// <summary>Cancels an open session. Nothing in stock changes; a reason is compulsory.</summary>
    public ServiceResult Cancel(User? user, int sessionId, string? reason)
    {
        if (user is null || !user.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can cancel a stock-take.");
        }

        var clean = (reason ?? string.Empty).Trim();
        if (clean.Length < MinimumReasonLength)
        {
            return ServiceResult.Fail("Write a reason of at least " + MinimumReasonLength + " characters for cancelling.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var session = LoadSession(connection, sessionId)
                    ?? throw new ValidationException("That stock-take was not found.");

                if (!session.IsOpen)
                {
                    throw new ValidationException("This stock-take is already " + session.Status + ".");
                }

                _db.Execute(connection, "UPDATE stock_takes SET status = 'cancelled', note = @note WHERE id = @id;",
                    new { note = "Cancelled: " + clean, id = sessionId });

                _audit.Record(connection, user, "StockTake", "stock_take_cancel", "stock_takes", session.Code,
                    oldValue: "open", newValue: "cancelled: " + clean);

                return ServiceResult.Success();
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("Stock-take " + sessionId + " could not be cancelled.", exception);
            return ServiceResult.Fail("The stock-take could not be cancelled.");
        }
    }

    /// <summary>The open session, or the newest one - what the page opens itself with.</summary>
    public StockTakeSession? Current()
    {
        try
        {
            using var connection = _db.Open();

            var id = _db.QuerySingle<int?>(connection,
                "SELECT id FROM stock_takes WHERE status = 'open' ORDER BY id DESC LIMIT 1;");

            if (id is int openId)
            {
                return LoadSession(connection, openId);
            }

            var newest = _db.QuerySingle<int?>(connection, "SELECT id FROM stock_takes ORDER BY id DESC LIMIT 1;");
            return newest is int newestId ? LoadSession(connection, newestId) : null;
        }
        catch (Exception exception)
        {
            _log.Error("The current stock-take could not be read.", exception);
            return null;
        }
    }

    /// <summary>Every session, newest first.</summary>
    public List<StockTakeSession> History()
    {
        try
        {
            using var connection = _db.Open();

            return _db.Query<StockTakeSession>(connection, @"
SELECT t.id AS id, t.code AS code, t.date AS date, t.status AS status, t.room AS room,
       t.category_id AS categoryId, t.note AS note, t.items_count AS itemsCount, t.diff_count AS diffCount,
       t.short_paisa AS shortPaisa, t.extra_paisa AS extraPaisa, u.name AS userName,
       t.applied_at AS appliedAt, a.name AS appliedBy,
       (SELECT COUNT(1) FROM stock_take_items i
        WHERE i.stock_take_id = t.id AND i.counted_qty_base IS NOT NULL) AS countedLines,
       (SELECT COUNT(1) FROM stock_take_items i WHERE i.stock_take_id = t.id) AS totalLines
FROM stock_takes t
LEFT JOIN users u ON u.id = t.user_id
LEFT JOIN users a ON a.id = t.applied_by
ORDER BY t.id DESC;");
        }
        catch (Exception exception)
        {
            _log.Error("The stock-take history could not be read.", exception);
            return new List<StockTakeSession>();
        }
    }

    /// <summary>The sheet of one session, with book / counted / difference / value (19.2.2).</summary>
    public List<StockTakeLine> Sheet(int sessionId, bool differencesOnly = false)
    {
        try
        {
            using var connection = _db.Open();

            var lines = _db.Query<StockTakeLine>(connection, @"
SELECT i.id AS id, i.product_id AS productId, p.name AS productName, i.batch_id AS batchId,
       b.batch_no AS batchNo, b.expiry_date AS expiryDate, p.rack_no AS rackNo,
       i.system_qty_base AS systemQtyBase, i.counted_qty_base AS countedQtyBase,
       i.unit_entered AS unitEntered, i.qty_entered AS qtyEntered, i.diff_qty_base AS diffQtyBase,
       i.value_paisa AS valuePaisa, u.name AS countedBy, i.counted_at AS countedAt, i.note AS note
FROM stock_take_items i
JOIN products p ON p.id = i.product_id
LEFT JOIN batches b ON b.id = i.batch_id
LEFT JOIN users u ON u.id = i.counted_by
WHERE i.stock_take_id = @sessionId
ORDER BY p.name COLLATE NOCASE, b.expiry_date, i.id;", new { sessionId });

            return differencesOnly
                ? lines.Where(line => line.HasDifference || (!line.IsCounted && Math.Abs(line.SystemQtyBase) > 0.0005)).ToList()
                : lines;
        }
        catch (Exception exception)
        {
            _log.Error("The sheet of stock-take " + sessionId + " could not be read.", exception);
            return new List<StockTakeLine>();
        }
    }

    /// <summary>Searches medicines that are not on the sheet, for "found on the shelf".</summary>
    public List<Product> NotOnSheet(int sessionId, string? term = null)
    {
        var text = (term ?? string.Empty).Trim();

        try
        {
            using var connection = _db.Open();

            return _db.Query<Product>(connection, @"
SELECT p.id AS id, p.name AS name, p.base_unit AS baseUnit, p.box_strips AS boxStrips,
       p.strip_tablets AS stripTablets, p.cost_paisa AS costPaisa, p.retail_paisa AS retailPaisa,
       p.reorder_level AS reorderLevel, p.active AS active
FROM products p
WHERE p.active = 1
  AND (@term = '' OR p.name LIKE @like)
  AND NOT EXISTS (SELECT 1 FROM stock_take_items i WHERE i.stock_take_id = @sessionId AND i.product_id = p.id)
ORDER BY p.name COLLATE NOCASE
LIMIT 40;", new { term = text, like = "%" + text + "%", sessionId });
        }
        catch (Exception exception)
        {
            _log.Error("The medicines missing from the sheet could not be searched.", exception);
            return new List<Product>();
        }
    }

    // ------------------------------------------------------------------ helpers

    private void RefreshTotals(SqliteConnection connection, int sessionId)
    {
        _db.Execute(connection, @"
UPDATE stock_takes
SET items_count = (SELECT COUNT(1) FROM stock_take_items WHERE stock_take_id = @id),
    diff_count  = (SELECT COUNT(1) FROM stock_take_items WHERE stock_take_id = @id
                     AND counted_qty_base IS NOT NULL AND ABS(diff_qty_base) > 0.0005),
    short_paisa = IFNULL((SELECT SUM(-value_paisa) FROM stock_take_items
                          WHERE stock_take_id = @id AND value_paisa < 0), 0),
    extra_paisa = IFNULL((SELECT SUM(value_paisa) FROM stock_take_items
                          WHERE stock_take_id = @id AND value_paisa > 0), 0)
WHERE id = @id;", new { id = sessionId });
    }

    private StockTakeSession? LoadSession(SqliteConnection connection, int id) =>
        _db.QuerySingle<StockTakeSession>(connection, @"
SELECT t.id AS id, t.code AS code, t.date AS date, t.status AS status, t.room AS room,
       t.category_id AS categoryId, t.note AS note, t.items_count AS itemsCount, t.diff_count AS diffCount,
       t.short_paisa AS shortPaisa, t.extra_paisa AS extraPaisa, u.name AS userName,
       t.applied_at AS appliedAt, a.name AS appliedBy,
       (SELECT COUNT(1) FROM stock_take_items i
        WHERE i.stock_take_id = t.id AND i.counted_qty_base IS NOT NULL) AS countedLines,
       (SELECT COUNT(1) FROM stock_take_items i WHERE i.stock_take_id = t.id) AS totalLines
FROM stock_takes t
LEFT JOIN users u ON u.id = t.user_id
LEFT JOIN users a ON a.id = t.applied_by
WHERE t.id = @id;", new { id });

    private static double PerUnit(LineDbRow line, string? unit)
    {
        var word = (unit ?? string.Empty).Trim().ToLowerInvariant();
        var perStrip = Math.Max(1, line.StripTablets);
        var perBox = Math.Max(1, line.BoxStrips) * perStrip;

        return word switch
        {
            "box" => line.BoxStrips > 0 ? perBox : 1,
            "strip" => line.StripTablets > 0 ? perStrip : 1,
            _ => 1
        };
    }

    // -------------------------------------------------------------------- types

    private sealed class LineDbRow
    {
        public int Id { get; set; }

        public int SessionId { get; set; }

        public int ProductId { get; set; }

        public double SystemQtyBase { get; set; }

        public long CostPaisa { get; set; }

        public string? Status { get; set; }

        public int BoxStrips { get; set; }

        public int StripTablets { get; set; }

        public string BaseUnit { get; set; } = "tablet";
    }

    private sealed class ApplyRow
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public int? BatchId { get; set; }

        public double SystemQtyBase { get; set; }

        public double? CountedQtyBase { get; set; }

        public long CostPaisa { get; set; }

        public double? DiffQtyBase { get; set; }

        public long? ValuePaisa { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string BaseUnit { get; set; } = "tablet";
    }
}
