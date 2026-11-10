using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Expenses and owner drawings (F-32, Spec 9.1 and 14.4). This is the last hole in the drawer:
/// before it existed, paying the tea boy out of the till made the shift look **short**.
///
/// The three rules that keep the books honest:
///   • **an expense is not a purchase.** No stock arrives and no supplier's account moves; only
///     cash leaves the drawer. Recording rent must never raise a payable.
///   • **a cash drawing leaves the drawer; a goods drawing leaves the shelf.** Goods are valued at
///     *purchase cost* - the money the shop actually spent - never at the selling price, because the
///     owner must not book a profit on medicine he took home.
///   • **nothing is edited or deleted.** A wrong entry is corrected by a second entry that says so;
///     the audit log keeps both (the same rule as write-off and stock-take).
///
/// The shift/galla report reads these two tables straight from the database, so a recorded expense
/// lowers the expected cash with no second step to forget.
/// </summary>
public sealed class ExpenseService
{
    public const string KindExpense = "expense";
    public const string KindDrawingCash = "drawing_cash";
    public const string KindDrawingGoods = "drawing_goods";

    /// <summary>The shop's usual costs; "other" is always there so nothing is ever refused for a missing word.</summary>
    public static readonly string[] Categories = ExpenseCategories.All;

    public static readonly string[] Types = { "cash", "goods" };

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public ExpenseService(Database db, AuditService audit, SettingsService settings,
        PermissionService permissions, LogService log)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    public static string CategoryLabel(string? category) => ExpenseCategories.Label(category);

    // ------------------------------------------------------------------ expenses

    /// <summary>Records money the shop spent. Cash / shifts permission: it is the drawer's money.</summary>
    public ServiceResult<ExpenseResult> AddExpense(User? user, ExpenseInput input)
    {
        if (!_permissions.Can(user, Modules.Cash))
        {
            return ServiceResult<ExpenseResult>.Fail("You do not have permission to record an expense.");
        }

        if (input is null)
        {
            return ServiceResult<ExpenseResult>.Fail("Nothing to save.");
        }

        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length < 3)
        {
            return ServiceResult<ExpenseResult>.Fail("Write what the money was spent on (at least three letters).");
        }

        if (input.AmountPaisa <= 0)
        {
            return ServiceResult<ExpenseResult>.Fail("The amount must be more than zero.");
        }

        var category = (input.Category ?? "other").Trim().ToLowerInvariant();
        if (Array.IndexOf(Categories, category) < 0)
        {
            category = "other";
        }

        try
        {
            var result = _db.Tx(connection =>
            {
                var stamp = Clock.Now();

                _db.Execute(connection, @"
INSERT INTO expenses(date, category, title, amount_paisa, note, user_id, created_at)
VALUES(@at, @category, @title, @amount, @note, @userId, @at);",
                    new
                    {
                        at = stamp,
                        category,
                        title,
                        amount = input.AmountPaisa,
                        note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note!.Trim(),
                        userId = user?.Id
                    });

                var id = (int)_db.NextId(connection);

                _audit.Record(connection, user, "Cash", "expense", "expenses", id.ToString(),
                    newValue: Money.Format(input.AmountPaisa) + " (" + CategoryLabel(category) + ": " + title + ")");

                var saved = new ExpenseResult
                {
                    Id = id,
                    Kind = KindExpense,
                    AmountPaisa = input.AmountPaisa
                };

                if (OpenShift(connection) is null)
                {
                    saved.Warnings.Add("No shift is open, so this money is recorded as shop cost "
                        + "but no drawer count will show it. Open the shift first if it came out of the till.");
                }

                return saved;
            });

            return ServiceResult<ExpenseResult>.Success(result);
        }
        catch (Exception exception)
        {
            _log.Error("An expense could not be saved.", exception);
            return ServiceResult<ExpenseResult>.Fail("The expense could not be saved. Nothing was written.");
        }
    }

    // ------------------------------------------------------------------ drawings

    /// <summary>
    /// The owner takes money or goods out of his own shop. **Owner only**: this line reduces what the
    /// business owns, and no cashier may decide that.
    /// </summary>
    public ServiceResult<DrawingResult> AddDrawing(User? user, DrawingInput input)
    {
        if (user is null || !user.IsOwner)
        {
            return ServiceResult<DrawingResult>.Fail("Only the owner can take money or goods out of the shop.");
        }

        if (input is null)
        {
            return ServiceResult<DrawingResult>.Fail("Nothing to save.");
        }

        var type = (input.Type ?? "cash").Trim().ToLowerInvariant();

        return type == "goods"
            ? AddGoodsDrawing(user, input)
            : AddCashDrawing(user, input);
    }

    private ServiceResult<DrawingResult> AddCashDrawing(User user, DrawingInput input)
    {
        if (input.AmountPaisa <= 0)
        {
            return ServiceResult<DrawingResult>.Fail("The amount must be more than zero.");
        }

        try
        {
            var result = _db.Tx(connection =>
            {
                var stamp = Clock.Now();

                _db.Execute(connection, @"
INSERT INTO owner_drawings(date, type, amount_paisa, note, user_id, created_at)
VALUES(@at, 'cash', @amount, @note, @userId, @at);",
                    new
                    {
                        at = stamp,
                        amount = input.AmountPaisa,
                        note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note!.Trim(),
                        userId = user.Id
                    });

                var id = (int)_db.NextId(connection);

                _audit.Record(connection, user, "Cash", "owner_drawing", "owner_drawings", id.ToString(),
                    newValue: Money.Format(input.AmountPaisa) + " (cash)");

                var saved = new DrawingResult
                {
                    Id = id,
                    Type = "cash",
                    AmountPaisa = input.AmountPaisa
                };

                if (OpenShift(connection) is null)
                {
                    saved.Warnings.Add("No shift is open, so this drawing will not appear in any drawer count.");
                }

                return saved;
            });

            return ServiceResult<DrawingResult>.Success(result);
        }
        catch (Exception exception)
        {
            _log.Error("An owner drawing could not be saved.", exception);
            return ServiceResult<DrawingResult>.Fail("The drawing could not be saved. Nothing was written.");
        }
    }

    /// <summary>
    /// Medicine taken by the owner: stock leaves the chosen batch, valued at **cost**, and no money
    /// moves. D-93 refuses a quantity that would take the batch or product below zero.
    /// </summary>
    private ServiceResult<DrawingResult> AddGoodsDrawing(User user, DrawingInput input)
    {
        if (input.ProductId <= 0)
        {
            return ServiceResult<DrawingResult>.Fail("Choose the medicine the owner took.");
        }

        if (input.QtyEntered <= 0)
        {
            return ServiceResult<DrawingResult>.Fail("The quantity must be more than zero.");
        }

        try
        {
            var result = _db.Tx(connection => SaveGoods(connection, user, input));

            if (!result.Ok || result.Value is null)
            {
                return result;
            }

            return ServiceResult<DrawingResult>.Success(result.Value, result.Value.Warnings.ToArray());
        }
        catch (ValidationException exception)
        {
            return ServiceResult<DrawingResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("A goods drawing could not be saved.", exception);
            return ServiceResult<DrawingResult>.Fail("The drawing could not be saved. Nothing was written.");
        }
    }

    private ServiceResult<DrawingResult> SaveGoods(SqliteConnection connection, User user, DrawingInput input)
    {
        var product = _db.QuerySingle<ProductRow>(connection, @"
SELECT id, name, base_unit AS baseUnit, box_strips AS boxStrips, strip_tablets AS stripTablets,
       cost_paisa AS costPaisa
FROM products WHERE id = @id AND active = 1;",
            new { id = input.ProductId })
            ?? throw new ValidationException("That medicine is not in the list any more.");

        var batch = input.BatchId is int wanted
            ? _db.QuerySingle<BatchDbRow>(connection, @"
SELECT id, batch_no AS batchNo, qty_base AS qtyBase, cost_paisa AS costPaisa
FROM batches WHERE id = @id AND product_id = @productId AND active = 1;",
                new { id = wanted, productId = product.Id })
            : _db.QuerySingle<BatchDbRow>(connection, @"
SELECT id, batch_no AS batchNo, qty_base AS qtyBase, cost_paisa AS costPaisa
FROM batches WHERE product_id = @productId AND active = 1
ORDER BY (expiry_date IS NULL OR expiry_date = '') ASC, expiry_date ASC, id ASC LIMIT 1;",
                new { productId = product.Id });

        if (batch is null)
        {
            throw new ValidationException(product.Name + " has no live batch, so there is nothing to take.");
        }

        var perUnit = Packs.PerUnit(product.BoxStrips, product.StripTablets, input.Unit);
        var qtyBase = Calc.ToBase(input.QtyEntered, perUnit);

        if (!double.IsFinite(qtyBase) || qtyBase <= 0)
        {
            throw new ValidationException("The quantity must be a real number more than zero.");
        }

        var costPaisa = batch.CostPaisa > 0 ? batch.CostPaisa : product.CostPaisa;
        var valuePaisa = Calc.Round(qtyBase * costPaisa);
        var stamp = Clock.Now();
        var note = Qty.Format(input.QtyEntered) + " " + (input.Unit ?? product.BaseUnit) + " "
                   + product.Name + " (" + batch.BatchNo + ")"
                   + (string.IsNullOrWhiteSpace(input.Note) ? string.Empty : " - " + input.Note!.Trim());

        var stockAfter = StockPolicy.Decrease(_db, connection, product.Id, batch.Id, qtyBase,
            product.Name, product.BaseUnit, "take as a goods drawing");

        _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, ref_type, ref_code, note, user_id, created_at)
VALUES(@productId, @batchId, 'drawing', @qty, 'owner_drawing', NULL, @note, @userId, @at);",
            new
            {
                productId = product.Id,
                batchId = batch.Id,
                qty = -qtyBase,
                note,
                userId = user.Id,
                at = stamp
            });

        _db.Execute(connection, @"
INSERT INTO owner_drawings(date, type, amount_paisa, note, user_id, created_at)
VALUES(@at, 'goods', @value, @note, @userId, @at);",
            new
            {
                at = stamp,
                value = valuePaisa,
                note,
                userId = user.Id
            });

        var id = (int)_db.NextId(connection);

        _audit.Record(connection, user, "Cash", "owner_drawing_goods", "owner_drawings", id.ToString(),
            newValue: note + " = " + Money.Format(valuePaisa) + " at cost");

        var saved = new DrawingResult
        {
            Id = id,
            Type = "goods",
            AmountPaisa = valuePaisa,
            QtyBase = qtyBase,
            StockAfterBase = stockAfter
        };

        return ServiceResult<DrawingResult>.Success(saved);
    }

    // ------------------------------------------------------------------ reading

    /// <summary>Expenses and drawings on one list, newest first - what the screen shows.</summary>
    public List<CashOutRow> List(int limit = 200)
    {
        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<CashOutRow>(connection, @"
SELECT e.id AS id, 'expense' AS kind, e.date AS date, e.category AS category,
       e.title AS title, e.amount_paisa AS amountPaisa, e.note AS note, IFNULL(u.name, '') AS userName,
       0 AS sortKind
FROM expenses e
LEFT JOIN users u ON u.id = e.user_id
UNION ALL
SELECT d.id AS id, CASE WHEN d.type = 'goods' THEN 'drawing_goods' ELSE 'drawing_cash' END AS kind,
       d.date AS date, d.type AS category, 'Owner drawing' AS title, d.amount_paisa AS amountPaisa,
       d.note AS note, IFNULL(u.name, '') AS userName,
       1 AS sortKind
FROM owner_drawings d
LEFT JOIN users u ON u.id = d.user_id
-- The two tables have their own ids, so `id` alone cannot order two entries saved in the same
-- second: they could swap places between two reads. sortKind (0 = expense, 1 = drawing) makes the
-- order unique and therefore repeatable; Dapper drops a column that no property claims. Within one
-- second the page therefore shows expenses first, then drawings, newest id first in each group.
ORDER BY date DESC, sortKind, id DESC
LIMIT @limit;",
                new { limit });

            return rows;
        }
        catch (Exception exception)
        {
            _log.Error("The cash-out list could not be read.", exception);
            return new List<CashOutRow>();
        }
    }

    /// <summary>Today's totals for the top of the screen.</summary>
    public CashOutToday Today()
    {
        try
        {
            using var connection = _db.Open();
            var from = Clock.Today();
            var to = from + " 23:59:59";

            return _db.QuerySingle<CashOutToday>(connection, @"
SELECT
  IFNULL((SELECT SUM(e.amount_paisa) FROM expenses e
          WHERE e.date >= @from AND e.date <= @to), 0) AS expensePaisa,
  IFNULL((SELECT SUM(d.amount_paisa) FROM owner_drawings d
          WHERE d.type = 'cash' AND d.date >= @from AND d.date <= @to), 0) AS drawingCashPaisa,
  IFNULL((SELECT SUM(d.amount_paisa) FROM owner_drawings d
          WHERE d.type = 'goods' AND d.date >= @from AND d.date <= @to), 0) AS drawingGoodsCostPaisa,
  (SELECT COUNT(1) FROM expenses e WHERE e.date >= @from AND e.date <= @to)
  + (SELECT COUNT(1) FROM owner_drawings d WHERE d.date >= @from AND d.date <= @to) AS entries;",
                new { from, to }) ?? new CashOutToday();
        }
        catch (Exception exception)
        {
            _log.Error("Today's cash-out could not be read.", exception);
            return new CashOutToday();
        }
    }

    /// <summary>True while a drawer is open - the form warns when the money cannot reach a count.</summary>
    public bool ShiftOpen()
    {
        using var connection = _db.Open();
        return OpenShift(connection) is not null;
    }

    private object? OpenShift(SqliteConnection connection) =>
        _db.Scalar(connection, "SELECT id FROM shifts WHERE status = 'open' ORDER BY id DESC LIMIT 1;");

    /// <summary>The few columns this service reads from `products`.</summary>
    private sealed class ProductRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string BaseUnit { get; set; } = "tablet";

        public int BoxStrips { get; set; }

        public int StripTablets { get; set; }

        public long CostPaisa { get; set; }
    }

    private sealed class BatchDbRow
    {
        public int Id { get; set; }

        public string BatchNo { get; set; } = string.Empty;

        public double QtyBase { get; set; }

        public long CostPaisa { get; set; }
    }
}
