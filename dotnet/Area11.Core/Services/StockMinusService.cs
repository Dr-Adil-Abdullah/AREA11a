using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The stock-minus registry (F-39, Spec §33.6.6): every medicine whose shelf is below zero, why it
/// went there, how long it has been there, and the owner's **monthly review** of it.
///
/// **Why a page and not an alert.** The alert board says *now*; this page says *since when*, because
/// a shelf that has been minus for two months is a different problem from one that went minus this
/// morning. A medicine that stays minus is theft, breakage or a keying mistake (Spec §33.6.6), and
/// the only honest fix is a stock-take - not a quiet correction.
///
/// **What is "the episode".** The stock book is replayed in movement order; the episode begins at the
/// movement that took the running balance below zero and ends when stock comes back to zero or above.
/// The current stock is always read from the **batches**, never from that replay - the batches are the
/// truth, and a shop whose batches were created without movements would otherwise show a wrong count.
///
/// **What a review is.** One line in the black box: who looked, when, and what he wrote. No row is
/// edited and none is deleted (Spec 22), and a review does not change the stock - the stock changes
/// only through a stock-take, a return or a write-off.
/// </summary>
public sealed class StockMinusService
{
    /// <summary>A review note shorter than this is refused - a blank "seen" explains nothing.</summary>
    public const int MinNoteLength = 3;

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public StockMinusService(
        Database db,
        AuditService audit,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Every minus shelf, oldest first, with its review state.</summary>
    public ServiceResult<StockMinusReport> Report(User? user)
    {
        if (!_permissions.Can(user, Modules.Stock))
        {
            return ServiceResult<StockMinusReport>.Fail(
                "You do not have permission to see the stock-minus registry.");
        }

        var report = new StockMinusReport();

        try
        {
            using var connection = _db.Open();

            var products = _db.Query<ProductRow>(connection, @"
SELECT p.id AS productId, p.name AS name, p.generic AS generic, p.base_unit AS baseUnit,
       IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) AS stockQty,
       IFNULL((SELECT SUM(b.qty_base * b.cost_paisa) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) AS valuePaisa
FROM products p
WHERE p.active = 1
  AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) < 0
ORDER BY p.name COLLATE NOCASE;");

            if (products.Count == 0)
            {
                return ServiceResult<StockMinusReport>.Success(report);
            }

            // The stock book of the minus products only: enough to find the episode, never the whole
            // history of the shop (the page opens on every visit).
            var movements = _db.Query<MovementRow>(connection, @"
SELECT m.product_id AS productId, m.id AS id, m.created_at AS at, m.type AS type,
       m.ref_code AS refCode, m.qty_base AS qty
FROM stock_movements m
WHERE m.product_id IN (
    SELECT p.id FROM products p
    WHERE p.active = 1
      AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) < 0
)
ORDER BY m.product_id, m.id;");

            // Every review ever written, newest first; the first row per medicine is the last review.
            var reviews = _db.Query<ReviewRow>(connection, @"
SELECT entity_id AS entityId, at AS at, user_name AS userName, details AS details
FROM audit_logs
WHERE module = 'stock' AND action = 'stock_minus_review' AND entity = 'products'
ORDER BY at DESC, id DESC;")
                .GroupBy(row => row.EntityId ?? string.Empty)
                .ToDictionary(group => group.Key, group => group.First());

            var episodes = FindEpisodes(movements);
            var today = DateTime.Today;

            foreach (var product in products)
            {
                var row = new StockMinusRow
                {
                    ProductId = product.ProductId,
                    Name = product.Name,
                    Generic = product.Generic,
                    BaseUnit = string.IsNullOrWhiteSpace(product.BaseUnit) ? "unit" : product.BaseUnit,
                    StockQty = product.StockQty,
                    ValuePaisa = product.ValuePaisa
                };

                if (episodes.TryGetValue(product.ProductId, out var start))
                {
                    row.SinceAt = start.At;
                    row.SinceType = start.Type;
                    row.SinceRefCode = start.RefCode;
                    row.DaysNegative = Ageing.AgeInDays(start.At, today);
                }

                if (reviews.TryGetValue(product.ProductId.ToString(), out var review))
                {
                    row.ReviewAt = review.At;
                    row.ReviewBy = review.UserName;
                    row.ReviewNote = review.Details;

                    // A review only counts for THIS episode: the note that explained last month's
                    // minus does not explain a new one.
                    row.IsReviewed = string.IsNullOrWhiteSpace(row.SinceAt)
                        || string.CompareOrdinal(review.At, row.SinceAt) >= 0;
                }

                report.Rows.Add(row);
            }

            report.Rows.Sort((left, right) =>
            {
                var byDays = right.DaysNegative.CompareTo(left.DaysNegative);

                return byDays != 0
                    ? byDays
                    : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            return ServiceResult<StockMinusReport>.Success(report);
        }
        catch (Exception exception)
        {
            _log.Error("The stock-minus registry could not be built.", exception);
            return ServiceResult<StockMinusReport>.Fail(
                "The stock-minus registry could not be built. " + exception.Message);
        }
    }

    /// <summary>
    /// Records the owner's look at one medicine: who, when, what he saw and what he wrote. The stock
    /// itself is not touched - only a stock-take, a return or a write-off may move it (U-30, D-56).
    /// </summary>
    public ServiceResult Review(User? user, int productId, string? note)
    {
        if (!_permissions.Can(user, Modules.Stock))
        {
            return ServiceResult.Fail("You do not have permission to review the stock-minus registry.");
        }

        var clean = (note ?? string.Empty).Trim();

        if (clean.Length < MinNoteLength)
        {
            return ServiceResult.Fail(
                "Write what happened (at least " + MinNoteLength + " letters) - \"seen\" explains nothing later.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var product = _db.QuerySingle<ProductRow>(connection, @"
SELECT p.id AS productId, p.name AS name, p.base_unit AS baseUnit,
       IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) AS stockQty,
       0 AS valuePaisa
FROM products p WHERE p.id = @id;", new { id = productId });

                if (product is null)
                {
                    return ServiceResult.Fail("That medicine is not in the product list any more.");
                }

                if (product.StockQty >= -0.0005)
                {
                    return ServiceResult.Fail(
                        "That medicine is not below zero any more - there is nothing to review.");
                }

                _audit.Record(connection, user, Modules.Stock, "stock_minus_review", "products",
                    productId.ToString(),
                    oldValue: null,
                    newValue: Qty.Format(product.StockQty) + " " + product.BaseUnit,
                    details: clean);

                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error("A stock-minus review could not be written.", exception);
            return ServiceResult.Fail("The review could not be saved. " + exception.Message);
        }
    }

    /// <summary>
    /// Replays each medicine's stock book and returns the movement that began the current minus
    /// episode. A shelf that went minus, came back and went minus again starts at the **second**
    /// crossing - the earlier note must not make the new problem look reviewed.
    /// </summary>
    private static Dictionary<int, MovementRow> FindEpisodes(List<MovementRow> movements)
    {
        var episodes = new Dictionary<int, MovementRow>();

        foreach (var group in movements.GroupBy(row => row.ProductId))
        {
            double running = 0;
            MovementRow? beganAt = null;

            foreach (var movement in group.OrderBy(row => row.Id))
            {
                var wasNegative = running < -0.0005;

                running += movement.Qty;

                var isNegative = running < -0.0005;

                if (!wasNegative && isNegative)
                {
                    beganAt = movement;
                }
                else if (wasNegative && !isNegative)
                {
                    beganAt = null;
                }
            }

            if (beganAt is not null)
            {
                episodes[group.Key] = beganAt;
            }
        }

        return episodes;
    }

    // ------------------------------------------------------------------ rows

    private sealed class ProductRow
    {
        public int ProductId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Generic { get; set; }

        public string BaseUnit { get; set; } = "unit";

        public double StockQty { get; set; }

        public long ValuePaisa { get; set; }
    }

    private sealed class MovementRow
    {
        public int ProductId { get; set; }

        public long Id { get; set; }

        public string? At { get; set; }

        public string? Type { get; set; }

        public string? RefCode { get; set; }

        public double Qty { get; set; }
    }

    private sealed class ReviewRow
    {
        public string? EntityId { get; set; }

        public string? At { get; set; }

        public string? UserName { get; set; }

        public string? Details { get; set; }
    }
}
