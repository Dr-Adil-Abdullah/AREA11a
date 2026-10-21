using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Quarantine (U-34). Goods that came back but must NOT go straight onto the shelf - a broken strip,
/// a returned medicine that has to be checked - stand aside with their own permanent number
/// (Q-0001). Nothing enters the shelf until a person decides, and that decision is never erased.
///
/// Three outcomes, and only one of them puts stock back:
///   • <c>restocked</c> - the goods are fit to sell: stock goes UP by the quantity, back on the same
///     batch, with a stock movement that names the Q-number.
///   • <c>expired</c>   - damaged / expired: the goods are destroyed. Stock is NOT touched, because
///     these units never came back into stock in the first place - adding and removing them again
///     would only create two false movements.
///   • <c>sold</c>      - the owner sold them on. They re-enter stock and the normal counter bill
///     takes them out, so the count and the money both stay in one place.
///
/// Every outcome writes the time, the person and the note, and the row stays in the history.
/// </summary>
public sealed class QuarantineService
{
    public const string Restocked = "restocked";
    public const string Expired = "expired";
    public const string Sold = "sold";

    public static readonly string[] Outcomes = { Restocked, Expired, Sold };

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public QuarantineService(Database db, AuditService audit, PermissionService permissions, LogService log)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Goods still waiting for a decision - the list that keeps the alert alive (15.4.4).</summary>
    public ServiceResult<List<QuarantineItem>> Pending()
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<QuarantineItem>(connection, SelectSql + @"
WHERE r.disposition = 'quarantine'
ORDER BY r.created_at DESC, r.id DESC;");

            return ServiceResult<List<QuarantineItem>>.Success(rows.ToList(), $"{rows.Count} item(s) in quarantine.");
        }
        catch (Exception exception)
        {
            _log.Error("The quarantine list could not be loaded.", exception);
            return ServiceResult<List<QuarantineItem>>.Fail("The quarantine list could not be loaded.");
        }
    }

    /// <summary>Which goods went where - every decision ever made (U-34).</summary>
    public ServiceResult<List<QuarantineItem>> History()
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<QuarantineItem>(connection, SelectSql + @"
WHERE r.qcode IS NOT NULL AND r.disposition <> 'quarantine'
ORDER BY r.created_at DESC, r.id DESC;");

            return ServiceResult<List<QuarantineItem>>.Success(rows.ToList());
        }
        catch (Exception exception)
        {
            _log.Error("The quarantine history could not be loaded.", exception);
            return ServiceResult<List<QuarantineItem>>.Fail("The quarantine history could not be loaded.");
        }
    }

    public int PendingCount()
    {
        try
        {
            using var connection = _db.Open();
            return (int)Convert.ToInt64(_db.Scalar(connection,
                "SELECT COUNT(1) FROM sale_returns WHERE disposition = 'quarantine';") ?? 0L);
        }
        catch (Exception exception)
        {
            _log.Error("The quarantine count could not be read.", exception);
            return 0;
        }
    }

    /// <summary>
    /// Decides one item's fate. Owner or manager only (15.4.3), and the note is compulsory for the
    /// two outcomes that lose money, so the reason is on record for the audit.
    /// </summary>
    public ServiceResult Dispose(User user, int returnId, string outcome, string? note)
    {
        if (!user.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can decide what happens to quarantine goods.");
        }

        if (!_permissions.Can(user, Modules.Returns))
        {
            return ServiceResult.Fail("You do not have permission to handle returns.");
        }

        var clean = (outcome ?? string.Empty).Trim().ToLowerInvariant();
        if (!Outcomes.Contains(clean))
        {
            return ServiceResult.Fail("Choose one of: back in stock, damaged/expired, sold.");
        }

        var cleanNote = (note ?? string.Empty).Trim();
        if (clean == Expired && cleanNote.Length < 3)
        {
            return ServiceResult.Fail("Write a short note for goods that are being destroyed (for example: 'wet box').");
        }

        try
        {
            _db.Tx(connection =>
            {
                var row = _db.QuerySingle<PendingRow>(connection, @"
SELECT id, qcode, product_id AS productId, batch_id AS batchId, qty_base AS qtyBase, disposition
FROM sale_returns WHERE id = @id;", new { id = returnId });

                if (row is null)
                {
                    throw new ValidationException("That quarantine item no longer exists.");
                }

                if (!string.Equals(row.Disposition, "quarantine", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException("That item already has a decision; it cannot be changed.");
                }

                var stamp = Clock.Now();

                _db.Execute(connection, @"
UPDATE sale_returns
SET disposition = @disposition, disposed_at = @at, disposed_by = @userId, disposed_note = @note,
    restock = @restock
WHERE id = @id;",
                    new
                    {
                        disposition = clean,
                        at = stamp,
                        userId = user.Id,
                        note = cleanNote.Length == 0 ? null : cleanNote,
                        restock = clean == Expired ? 0 : 1,
                        id = returnId
                    });

                if (clean is Restocked or Sold)
                {
                    AddBack(connection, row, clean, stamp, user);
                }

                var where = cleanNote.Length == 0 ? string.Empty : $", note: {cleanNote}";
                _audit.Record(connection, user, "Quarantine", "quarantine_" + clean, "sale_returns",
                    returnId.ToString(),
                    oldValue: "quarantine",
                    newValue: $"{row.QCode} -> {clean} ({Qty.Format(row.QtyBase)} base){where}");
            });

            return ServiceResult.Success($"{returnId} marked as {clean}.");
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error($"Quarantine item {returnId} could not be decided.", exception);
            return ServiceResult.Fail("The decision could not be saved.");
        }
    }

    private void AddBack(SqliteConnection connection, PendingRow row, string outcome, string stamp, User user)
    {
        if (row.BatchId is int batchId)
        {
            _db.Execute(connection,
                "UPDATE batches SET qty_base = qty_base + @qty WHERE id = @id;",
                new { qty = row.QtyBase, id = batchId });
        }
        else
        {
            var fallback = _db.QuerySingle<int?>(connection, @"
SELECT id FROM batches WHERE product_id = @productId AND active = 1
ORDER BY (expiry_date IS NULL OR expiry_date = '') ASC, expiry_date DESC, id DESC LIMIT 1;",
                new { productId = row.ProductId });

            if (fallback is int fallbackId)
            {
                _db.Execute(connection,
                    "UPDATE batches SET qty_base = qty_base + @qty WHERE id = @id;",
                    new { qty = row.QtyBase, id = fallbackId });
            }
        }

        _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, ref_type, ref_id, ref_code, note, user_id, created_at)
VALUES(@productId, @batchId, @type, @qty, 'quarantine', @returnId, @qcode, @note, @userId, @at);",
            new
            {
                productId = row.ProductId,
                batchId = row.BatchId,
                type = outcome == Sold ? "quarantine_sold" : "quarantine_restock",
                qty = row.QtyBase,
                returnId = row.Id,
                qcode = row.QCode,
                note = outcome == Sold
                    ? $"{row.QCode} sold on - ring it up at the counter"
                    : $"{row.QCode} back on the shelf",
                userId = user.Id,
                at = stamp
            });
    }

    private const string SelectSql = @"
SELECT r.id AS returnId, r.qcode AS qCode, r.code AS returnCode, s.name_snapshot AS productName,
       r.qty_base AS qtyBase, r.unit_sold AS unitSold, r.qty_entered AS qtyEntered,
       r.date, r.reason, r.disposition, r.disposed_at AS disposedAt,
       COALESCE(u.name, '') AS disposedBy, r.disposed_note AS disposedNote
FROM sale_returns r
LEFT JOIN sale_items s ON s.id = r.sale_item_id
LEFT JOIN users u ON u.id = r.disposed_by
";

    private sealed class PendingRow
    {
        public int Id { get; set; }

        public string? QCode { get; set; }

        public int ProductId { get; set; }

        public int? BatchId { get; set; }

        public double QtyBase { get; set; }

        public string? Disposition { get; set; }
    }
}
