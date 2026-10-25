using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Rush-time (no-bill) returns, `PR-nnnn` (9.2.2, 15.5, U-33).
///
/// What happens at the counter:
///   • the customer has no paper bill and the shop is busy;
///   • the cashier takes the medicine back, puts it on the shelf at once and pays the money back;
///   • the entry gets a permanent `PR-0001` number and stays **pending** - a red badge blinks until
///     the real bill is attached;
///   • the owner/manager can attach the bill later. Attaching does **not** add the stock a second
///     time (15.5.3) and does **not** silence the alert: the alert turns amber and asks the owner
///     for a reason (15.5.4);
///   • an entry that was typed by mistake is **cancelled with a written reason** (15.5.5), and
///     cancelling takes the same stock back out of the same batch.
///
/// The honest limit of this feature, said plainly: a refund with no bill can never be checked
/// against what was paid. That is why the number is permanent, the badge does not stop blinking,
/// and only the owner can end the alert.
/// </summary>
public sealed class ProvisionalReturnService
{
    public const string StatusPending = "pending";
    public const string StatusLinked = "linked";
    public const string StatusCancelled = "cancelled";

    private const int MinimumReasonLength = 3;

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly NumberingService _numbering;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public ProvisionalReturnService(
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

    /// <summary>Takes the medicine back, adds it to the shelf and pays the customer out.</summary>
    public ServiceResult<ProvisionalResult> Create(ProvisionalInput input, User user)
    {
        if (!_permissions.Can(user, Modules.Returns))
        {
            return ServiceResult<ProvisionalResult>.Fail("You do not have permission to take a return.");
        }

        try
        {
            return _db.Tx(connection => Save(connection, input, user));
        }
        catch (ValidationException exception)
        {
            return ServiceResult<ProvisionalResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The no-bill return could not be saved.", exception);
            return ServiceResult<ProvisionalResult>.Fail("The no-bill return could not be saved. Nothing was changed.");
        }
    }

    /// <summary>The blinking list: every no-bill return still waiting for its bill.</summary>
    public List<ProvisionalRow> Pending()
    {
        try
        {
            using var connection = _db.Open();
            return ReadRows(connection, "WHERE p.status = 'pending'");
        }
        catch (Exception exception)
        {
            _log.Error("The pending no-bill returns could not be listed.", exception);
            return new List<ProvisionalRow>();
        }
    }

    /// <summary>Everything ever refunded without a bill - the paper trail the owner asked for.</summary>
    public List<ProvisionalRow> History()
    {
        try
        {
            using var connection = _db.Open();
            return ReadRows(connection, string.Empty);
        }
        catch (Exception exception)
        {
            _log.Error("The no-bill return history could not be listed.", exception);
            return new List<ProvisionalRow>();
        }
    }

    /// <summary>One entry by its permanent code (PR-0001), for the receipt.</summary>
    public ProvisionalRow? Get(string? code)
    {
        var clean = (code ?? string.Empty).Trim();
        if (clean.Length == 0)
        {
            return null;
        }

        try
        {
            using var connection = _db.Open();
            return ReadRows(connection, "WHERE upper(p.code) = upper(@code)", new { code = clean }).FirstOrDefault();
        }
        catch (Exception exception)
        {
            _log.Error("The no-bill return " + clean + " could not be read.", exception);
            return null;
        }
    }

    /// <summary>The lines of one entry, oldest first (the receipt prints them in this order).</summary>
    public List<string> Lines(int provisionalId)
    {
        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<LineRow>(connection, @"
SELECT i.qty_entered AS qtyEntered, i.unit_entered AS unitEntered, i.qty_base AS qtyBase,
       pr.name AS productName
FROM provisional_items i
JOIN products pr ON pr.id = i.product_id
WHERE i.provisional_id = @id
ORDER BY i.id;", new { id = provisionalId });

            return rows.Select(row =>
                $"{Qty.Format(row.QtyEntered)} {row.UnitEntered ?? "unit"} - {row.ProductName}").ToList();
        }
        catch (Exception exception)
        {
            _log.Error("The lines of no-bill return " + provisionalId + " could not be read.", exception);
            return new List<string>();
        }
    }

    /// <summary>
    /// Attaches the real bill (15.5.3). Owner or manager only. **No stock change** - the goods were
    /// already added when the customer was refunded, and adding them again would invent medicine.
    /// The alert deliberately stays, in amber, until the owner writes a reason (15.5.4).
    /// </summary>
    public ServiceResult Link(int provisionalId, string? saleCode, User user)
    {
        if (!CanClose(user))
        {
            return ServiceResult.Fail("Only the owner or a manager can attach the bill.");
        }

        var clean = (saleCode ?? string.Empty).Trim();
        if (clean.Length == 0)
        {
            return ServiceResult.Fail("Type the bill number, for example INV-0001.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var row = LoadOne(connection, provisionalId);

                if (row is null)
                {
                    throw new ValidationException("That no-bill return was not found.");
                }

                if (!string.Equals(row.Status, StatusPending, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException(
                        "This entry is already " + row.Status + "; a bill can only be attached to a pending entry.");
                }

                var saleId = _db.QuerySingle<int?>(connection,
                    "SELECT id FROM sales WHERE upper(code) = upper(@code);", new { code = clean });

                if (saleId is not int linkedSaleId)
                {
                    throw new ValidationException("No bill found with the number " + clean + ".");
                }

                _db.Execute(connection, @"
UPDATE provisional_returns
SET status = 'linked', linked_sale_id = @saleId, linked_code = @code, linked_at = @at, linked_by = @userId
WHERE id = @id;",
                    new { saleId = linkedSaleId, code = clean.ToUpperInvariant(), at = Clock.Now(), userId = user.Id, id = provisionalId });

                _audit.Record(connection, user, "Returns", "provisional_link", "provisional_returns", row.Code,
                    oldValue: "pending", newValue: "linked to " + clean.ToUpperInvariant());

                return ServiceResult.Success();
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The bill could not be attached to no-bill return " + provisionalId + ".", exception);
            return ServiceResult.Fail("The bill could not be attached.");
        }
    }

    /// <summary>
    /// Cancels an entry that was typed by mistake (15.5.5). The reason is compulsory, and the stock
    /// that was added at the counter is taken back out of the **same batch**, so the shelf agrees
    /// with what physically came back.
    /// </summary>
    public ServiceResult Cancel(int provisionalId, string? reason, User user)
    {
        if (!CanClose(user))
        {
            return ServiceResult.Fail("Only the owner or a manager can cancel a no-bill return.");
        }

        var clean = (reason ?? string.Empty).Trim();
        if (clean.Length < MinimumReasonLength)
        {
            return ServiceResult.Fail(
                "Write a reason of at least " + MinimumReasonLength + " characters for cancelling this entry.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var row = LoadOne(connection, provisionalId);

                if (row is null)
                {
                    throw new ValidationException("That no-bill return was not found.");
                }

                if (!string.Equals(row.Status, StatusPending, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException(
                        "This entry is already " + row.Status + "; only a pending entry can be cancelled.");
                }

                var stamp = Clock.Now();
                var items = _db.Query<StockRow>(connection, @"
SELECT product_id AS productId, batch_id AS batchId, qty_base AS qtyBase, qty_entered AS qtyEntered,
       unit_entered AS unitEntered
FROM provisional_items WHERE provisional_id = @id ORDER BY id;", new { id = provisionalId });

                foreach (var item in items)
                {
                    if (item.BatchId is int batchId)
                    {
                        _db.Execute(connection,
                            "UPDATE batches SET qty_base = qty_base - @qty WHERE id = @id;",
                            new { qty = item.QtyBase, id = batchId });
                    }

                    _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, ref_type, ref_id, ref_code, note, user_id, created_at)
VALUES(@productId, @batchId, 'return_out', @qty, 'provisional_return', @refId, @code, @note, @userId, @at);",
                        new
                        {
                            productId = item.ProductId,
                            batchId = item.BatchId,
                            qty = -item.QtyBase,
                            refId = provisionalId,
                            code = row.Code,
                            note = "No-bill return cancelled: " + clean,
                            userId = user.Id,
                            at = stamp
                        });
                }

                _db.Execute(connection, @"
UPDATE provisional_returns
SET status = 'cancelled', cancel_reason = @reason, cancelled_at = @at, cancelled_by = @userId
WHERE id = @id;",
                    new { reason = clean, at = stamp, userId = user.Id, id = provisionalId });

                _audit.Record(connection, user, "Returns", "provisional_cancel", "provisional_returns", row.Code,
                    oldValue: "pending", newValue: "cancelled: " + clean);

                return ServiceResult.Success();
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("No-bill return " + provisionalId + " could not be cancelled.", exception);
            return ServiceResult.Fail("The no-bill return could not be cancelled.");
        }
    }

    /// <summary>
    /// Smart bill lookup (9.2.1): a medicine name, a customer phone or a rough date finds earlier
    /// bills. Only bills that can carry a return are listed (paid, partial or credit).
    /// </summary>
    public ServiceResult<List<BillSearchRow>> SearchBills(string? term, int limit = 40)
    {
        var text = (term ?? string.Empty).Trim();

        if (text.Length < 2)
        {
            return ServiceResult<List<BillSearchRow>>.Fail("Type at least two letters, a phone number or a date.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<BillSearchRow>(connection, @"
SELECT s.id AS id, s.code AS code, s.date AS date, c.name AS customerName, c.phone AS phone,
       s.total_paisa AS totalPaisa,
       (SELECT COUNT(1) FROM sale_items si WHERE si.sale_id = s.id) AS itemCount,
       COALESCE((SELECT group_concat(si2.name_snapshot, ', ')
                 FROM (SELECT name_snapshot FROM sale_items WHERE sale_id = s.id ORDER BY id) si2), '') AS itemsLabel
FROM sales s
LEFT JOIN customers c ON c.id = s.customer_id
WHERE s.status IN ('paid', 'partial', 'credit')
  AND (upper(s.code) LIKE upper(@like) OR upper(s.date) LIKE upper(@like)
       OR IFNULL(c.phone, '') LIKE @like OR IFNULL(c.name, '') LIKE @like
       OR EXISTS (SELECT 1 FROM sale_items si WHERE si.sale_id = s.id AND si.name_snapshot LIKE @like))
ORDER BY s.id DESC
LIMIT @limit;",
                new { like = "%" + text + "%", limit = Math.Clamp(limit, 1, 200) });

            return ServiceResult<List<BillSearchRow>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The bill lookup for '" + text + "' failed.", exception);
            return ServiceResult<List<BillSearchRow>>.Fail("The bills could not be searched.");
        }
    }

    // ------------------------------------------------------------------ saving

    private ServiceResult<ProvisionalResult> Save(SqliteConnection connection, ProvisionalInput input, User user)
    {
        var lines = (input.Lines ?? new List<ProvisionalLineInput>())
            .Where(line => line.ProductId > 0 && line.QtyEntered > 0)
            .ToList();

        if (lines.Count == 0)
        {
            throw new ValidationException("Add at least one medicine and its quantity.");
        }

        if (input.RefundPaisa < 0)
        {
            throw new ValidationException("The refund cannot be a minus number.");
        }

        var warnings = new List<string>();
        var stamp = Clock.Now();
        var code = _numbering.Take(connection, "provisional");
        var name = string.IsNullOrWhiteSpace(input.CustomerName) ? null : input.CustomerName!.Trim();
        var phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone!.Trim();

        if (name is null && phone is null)
        {
            warnings.Add("No name and no phone: this entry can never be matched to a bill later.");
        }

        if (input.RefundPaisa == 0)
        {
            warnings.Add("The refund is zero - the customer went home with no money.");
        }

        _db.Execute(connection, @"
INSERT INTO provisional_returns(code, date, phone, customer_name, reason, notes, refund_paisa, status, user_id, created_at)
VALUES(@code, @date, @phone, @name, @reason, @notes, @refund, 'pending', @userId, @at);",
            new
            {
                code,
                date = stamp,
                phone,
                name,
                reason = string.IsNullOrWhiteSpace(input.Reason) ? "no bill" : input.Reason!.Trim(),
                notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes!.Trim(),
                refund = input.RefundPaisa,
                userId = user.Id,
                at = stamp
            });

        var headerId = (int)_db.NextId(connection);

        var savedLines = new List<SavedLine>();

        foreach (var line in lines)
        {
            var product = _db.QuerySingle<ProductRow>(connection,
                "SELECT id, name, base_unit AS baseUnit, box_strips AS boxStrips, strip_tablets AS stripTablets, cost_paisa AS costPaisa, retail_paisa AS retailPaisa FROM products WHERE id = @id AND active = 1;",
                new { id = line.ProductId });

            if (product is null)
            {
                throw new ValidationException("One of the medicines is not in the list any more.");
            }

            var perUnit = PerUnit(product, line.Unit);
            var qtyBase = Calc.ToBase(line.QtyEntered, perUnit);

            if (!double.IsFinite(qtyBase) || qtyBase <= 0)
            {
                throw new ValidationException(product.Name + ": the quantity must be a finite number more than zero.");
            }

            var batch = FindOrOpenBatch(connection, product, stamp);
            var batchId = batch.Id;

            _db.Execute(connection,
                "UPDATE batches SET qty_base = qty_base + @qty WHERE id = @id;",
                new { qty = qtyBase, id = batchId });

            _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, ref_type, ref_id, ref_code, note, user_id, created_at)
VALUES(@productId, @batchId, 'return_in', @qty, 'provisional_return', @refId, @code, @note, @userId, @at);",
                new
                {
                    productId = product.Id,
                    batchId,
                    qty = qtyBase,
                    refId = headerId,
                    code,
                    note = "No-bill return " + code,
                    userId = user.Id,
                    at = stamp
                });

            _db.Execute(connection, @"
INSERT INTO provisional_items(provisional_id, product_id, qty_base, batch_id, qty_entered, unit_entered,
                              refund_paisa, cost_paisa_at_return, created_at)
VALUES(@provisionalId, @productId, @qtyBase, @batchId, @qtyEntered, @unit, 0, @cost, @at);",
                new
                {
                    provisionalId = headerId,
                    productId = product.Id,
                    qtyBase,
                    batchId,
                    qtyEntered = line.QtyEntered,
                    unit = string.IsNullOrWhiteSpace(line.Unit) ? product.BaseUnit : line.Unit.Trim().ToLowerInvariant(),
                    cost = batch.CostPaisa,
                    at = stamp
                });

            savedLines.Add(new SavedLine
            {
                Id = (int)_db.NextId(connection)
            });
        }

        AllocateRefund(connection, savedLines, input.RefundPaisa);

        if (input.RefundPaisa > 0)
        {
            _db.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, provisional_return_id, user_id, note)
VALUES(@date, 'cash', @amount, @provisionalId, @userId, @note);",
                new
                {
                    date = stamp,
                    amount = -input.RefundPaisa,
                    provisionalId = headerId,
                    userId = user.Id,
                    note = "No-bill cash refund " + code
                });
        }

        _audit.Record(connection, user, "Returns", "provisional_create", "provisional_returns", code,
            oldValue: null,
            newValue: "no-bill return of " + Money.Format(input.RefundPaisa) + " for " + savedLines.Count.ToString() + " item(s)");

        return ServiceResult<ProvisionalResult>.Success(new ProvisionalResult
        {
            Id = headerId,
            Code = code,
            RefundPaisa = input.RefundPaisa,
            ItemCount = savedLines.Count,
            Warnings = warnings
        }, warnings.ToArray());
    }

    // -------------------------------------------------------------------- reads

    private ProvisionalRow? LoadOne(SqliteConnection connection, int id) =>
        ReadRows(connection, "WHERE p.id = @id", new { id }).FirstOrDefault();

    private List<ProvisionalRow> ReadRows(SqliteConnection connection, string where, object? parameters = null) =>
        _db.Query<ProvisionalRow>(connection, @"
SELECT p.id AS id, p.code AS code, p.date AS date, p.customer_name AS customerName, p.phone AS phone,
       p.reason AS reason, p.notes AS notes, p.refund_paisa AS refundPaisa, p.status AS status,
       p.linked_sale_id AS linkedSaleId, p.linked_code AS linkedCode, p.linked_at AS linkedAt,
       p.cancel_reason AS cancelReason, p.cancelled_at AS cancelledAt, u.name AS cashierName,
       (SELECT COUNT(1) FROM provisional_items i WHERE i.provisional_id = p.id) AS itemCount,
       COALESCE((SELECT group_concat(i3.name, ', ')
                 FROM (SELECT pr.name AS name
                       FROM provisional_items i2 JOIN products pr ON pr.id = i2.product_id
                       WHERE i2.provisional_id = p.id ORDER BY i2.id) i3), '') AS itemsLabel
FROM provisional_returns p
LEFT JOIN users u ON u.id = p.user_id
" + where + @"
ORDER BY p.id DESC;", parameters);

    // ------------------------------------------------------------------- helpers

    /// <summary>How many base units one of the cashier's words holds (5.1.3).</summary>
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

    /// <summary>
    /// Where the returned goods go. Nobody knows which batch they came from, so they join the newest
    /// open batch; if the shop never bought that medicine, one honest "open" batch is created.
    /// </summary>
    private BatchRow FindOrOpenBatch(SqliteConnection connection, ProductRow product, string stamp)
    {
        var batch = _db.QuerySingle<BatchRow>(connection, @"
SELECT id, cost_paisa AS costPaisa
FROM batches
WHERE product_id = @productId AND active = 1
ORDER BY (expiry_date IS NULL OR expiry_date = '') ASC, expiry_date DESC, id DESC
LIMIT 1;",
            new { productId = product.Id });

        if (batch is not null)
        {
            return batch;
        }

        _db.Execute(connection, @"
INSERT INTO batches(product_id, batch_no, expiry_date, expiry_ym, qty_base, cost_paisa, retail_paisa, is_sample, active, created_at)
VALUES(@productId, 'OPEN', NULL, NULL, 0, @cost, @retail, 0, 1, @at);",
            new { productId = product.Id, cost = product.CostPaisa, retail = product.RetailPaisa, at = stamp });

        return new BatchRow
        {
            Id = (int)_db.NextId(connection),
            CostPaisa = product.CostPaisa
        };
    }

    /// <summary>
    /// Stores the header refund on its lines for product and category reports. A no-bill entry has
    /// no trustworthy line prices, so every line gets an equal integer share and the first lines
    /// get the remaining paisa. Migration 024 uses the same long division/remainder rule: no
    /// floating-point boundary can make old and new rows disagree.
    /// </summary>
    private void AllocateRefund(SqliteConnection connection, List<SavedLine> lines, long refundPaisa)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var share = refundPaisa / lines.Count;
        var remainder = refundPaisa % lines.Count;

        for (var index = 0; index < lines.Count; index++)
        {
            var lineRefund = share + (index < remainder ? 1 : 0);
            _db.Execute(connection,
                "UPDATE provisional_items SET refund_paisa = @refund WHERE id = @id;",
                new { refund = lineRefund, id = lines[index].Id });
        }
    }

    private static bool CanClose(User? user) =>
        user is not null
        && (string.Equals(user.Role, "owner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role, "manager", StringComparison.OrdinalIgnoreCase));

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

    private sealed class BatchRow
    {
        public int Id { get; set; }

        public long CostPaisa { get; set; }
    }

    private sealed class SavedLine
    {
        public int Id { get; set; }
    }

    private sealed class StockRow
    {
        public int ProductId { get; set; }

        public int? BatchId { get; set; }

        public double QtyBase { get; set; }

        public double QtyEntered { get; set; }

        public string? UnitEntered { get; set; }
    }

    private sealed class LineRow
    {
        public double QtyEntered { get; set; }

        public string? UnitEntered { get; set; }

        public double QtyBase { get; set; }

        public string? ProductName { get; set; }
    }
}
