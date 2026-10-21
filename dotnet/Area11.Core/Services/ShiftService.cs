using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The galla / shift (F-31, Spec 14). One drawer at a time - this is a single-counter shop.
///
/// The rule that makes this service worth having: **expected cash is rebuilt from history**, never
/// from a number the cashier typed. Opening float + cash bills + khata money received + cash a
/// supplier handed back, minus cash refunds, money paid to suppliers, shop expenses and cash the
/// owner took. The owner counts the drawer, types the number, and the app says "exact", "over by
/// Rs 50" or "short by Rs 120".
///
/// What is deliberately NOT counted: card and online payments (they never entered the drawer), and
/// credit bills (that money was never received). The supplier side counts **all** cash paid to
/// suppliers, including the part paid at the moment of a purchase, because that money left the till.
///
/// A shift is money, so the page needs the `cash` permission; the owner always has it.
/// </summary>
public sealed class ShiftService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public ShiftService(Database db, AuditService audit, PermissionService permissions, LogService log)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>The open shift with its money so far, or null when the drawer is closed.</summary>
    public ShiftMoney? Current()
    {
        try
        {
            using var connection = _db.Open();
            var shift = ReadOpen(connection);

            if (shift is not null)
            {
                FillDrawer(connection, shift, shift.OpenedAt, Clock.Now());
            }

            return shift;
        }
        catch (Exception exception)
        {
            _log.Error("The open shift could not be read.", exception);
            return null;
        }
    }

    /// <summary>Opens the drawer with a float. Refused while another shift is open.</summary>
    public ServiceResult<ShiftMoney> Open(User user, long openingFloatPaisa, string? note)
    {
        if (!_permissions.Can(user, Modules.Cash))
        {
            return ServiceResult<ShiftMoney>.Fail("You do not have permission to open the till.");
        }

        if (openingFloatPaisa < 0)
        {
            return ServiceResult<ShiftMoney>.Fail("The opening float cannot be negative.");
        }

        try
        {
            using var connection = _db.Open();

            if (ReadOpen(connection) is not null)
            {
                return ServiceResult<ShiftMoney>.Fail(
                    "A shift is already open. Close it before counting the drawer again.");
            }

            _db.Execute(connection, @"
INSERT INTO shifts(user_id, opened_at, opening_float_paisa, status, note)
VALUES(@userId, @at, @openingFloat, 'open', @note);",
                new
                {
                    userId = user.Id,
                    at = Clock.Now(),
                    openingFloat = openingFloatPaisa,
                    note = string.IsNullOrWhiteSpace(note) ? null : note!.Trim()
                });

            var id = (int)_db.NextId(connection);

            _audit.Record(connection, user, "Cash", "shift_open", "shifts", id.ToString(),
                newValue: Money.Format(openingFloatPaisa));

            var shift = ReadOpen(connection);

            if (shift is null)
            {
                return ServiceResult<ShiftMoney>.Fail("The shift could not be opened.");
            }

            FillDrawer(connection, shift, shift.OpenedAt, Clock.Now());
            return ServiceResult<ShiftMoney>.Success(shift);
        }
        catch (Exception exception)
        {
            _log.Error("The shift could not be opened.", exception);
            return ServiceResult<ShiftMoney>.Fail("The shift could not be opened.");
        }
    }

    /// <summary>
    /// Counts the drawer and closes the shift. The expected cash is computed here, not taken from
    /// the screen, so a wrong number on the page cannot rewrite the day.
    /// </summary>
    public ServiceResult<ShiftMoney> Close(User user, long actualPaisa, string? note)
    {
        if (!_permissions.Can(user, Modules.Cash))
        {
            return ServiceResult<ShiftMoney>.Fail("You do not have permission to close the till.");
        }

        if (actualPaisa < 0)
        {
            return ServiceResult<ShiftMoney>.Fail("The counted cash cannot be negative.");
        }

        try
        {
            using var connection = _db.Open();
            var shift = ReadOpen(connection);

            if (shift is null)
            {
                return ServiceResult<ShiftMoney>.Fail("There is no open shift to close.");
            }

            // The person who opened the drawer closes it, or somebody senior does it for him.
            if (shift.UserId != user.Id && !user.CanManage)
            {
                return ServiceResult<ShiftMoney>.Fail(
                    "Only the person who opened this shift, or the owner or a manager, can close it.");
            }

            var closedAt = Clock.Now();
            FillDrawer(connection, shift, shift.OpenedAt, closedAt);

            shift.ActualPaisa = actualPaisa;
            shift.DifferencePaisa = actualPaisa - shift.ExpectedPaisa;
            shift.Status = "closed";

            var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note!.Trim();

            _db.Execute(connection, @"
UPDATE shifts
SET closed_at = @closedAt, expected_paisa = @expected, actual_paisa = @actual,
    difference_paisa = @difference, status = 'closed', note = IFNULL(@note, note)
WHERE id = @id;",
                new
                {
                    closedAt,
                    expected = shift.ExpectedPaisa,
                    actual = actualPaisa,
                    difference = shift.DifferencePaisa,
                    note = cleanNote,
                    id = shift.Id
                });

            _audit.RecordChange(connection, user, "Cash", "shift_close", "shifts", shift.Id.ToString(),
                new { Status = "open" },
                new
                {
                    Status = "closed",
                    Expected = shift.ExpectedPaisa,
                    Actual = actualPaisa,
                    Difference = shift.DifferencePaisa
                },
                cleanNote);

            return ServiceResult<ShiftMoney>.Success(shift);
        }
        catch (Exception exception)
        {
            _log.Error("The shift could not be closed.", exception);
            return ServiceResult<ShiftMoney>.Fail("The shift could not be closed.");
        }
    }

    /// <summary>The last shifts, newest first - the day-by-day record of the drawer.</summary>
    public List<ShiftMoney> History(int limit = 60)
    {
        try
        {
            using var connection = _db.Open();

            return _db.Query<ShiftMoney>(connection, @"
SELECT s.id AS id, s.opened_at AS openedAt, IFNULL(u.name, '') AS openedBy,
       s.opening_float_paisa AS openingFloatPaisa, IFNULL(s.expected_paisa, 0) AS expectedPaisa,
       s.actual_paisa AS actualPaisa, s.difference_paisa AS differencePaisa,
       s.status AS status, s.note AS note
FROM shifts s
LEFT JOIN users u ON u.id = s.user_id
ORDER BY s.id DESC
LIMIT @limit;",
                new { limit });
        }
        catch (Exception exception)
        {
            _log.Error("The shift history could not be read.", exception);
            return new List<ShiftMoney>();
        }
    }

    private ShiftMoney? ReadOpen(Microsoft.Data.Sqlite.SqliteConnection connection) =>
        _db.QuerySingle<ShiftMoney>(connection, @"
SELECT s.id AS id, s.user_id AS userId, s.opened_at AS openedAt, IFNULL(u.name, '') AS openedBy,
       s.opening_float_paisa AS openingFloatPaisa, IFNULL(s.expected_paisa, 0) AS expectedPaisa,
       s.status AS status, s.note AS note
FROM shifts s
LEFT JOIN users u ON u.id = s.user_id
WHERE s.status = 'open'
ORDER BY s.id DESC
LIMIT 1;");

    /// <summary>
    /// Fills the six money lines from the history between two timestamps. Every line is its own
    /// sub-select so one empty table can never hide another line.
    /// </summary>
    private void FillDrawer(Microsoft.Data.Sqlite.SqliteConnection connection, ShiftMoney shift, string from, string to)
    {
        var drawer = _db.QuerySingle<ShiftMoney>(connection, @"
SELECT
  IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
          WHERE p.method = 'cash' AND p.sale_id IS NOT NULL
            AND p.date >= @from AND p.date <= @to), 0) AS cashSalesPaisa,
  IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
          WHERE p.method = 'cash' AND p.sale_id IS NULL AND p.customer_id IS NOT NULL
            AND p.date >= @from AND p.date <= @to), 0) AS cashCollectionsPaisa,
  IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
          WHERE p.method = 'cash' AND p.supplier_id IS NOT NULL
            AND p.date >= @from AND p.date <= @to), 0) AS supplierCashOutPaisa,
  IFNULL((SELECT SUM(r.cash_refund_paisa) FROM sale_returns r
          WHERE r.date >= @from AND r.date <= @to), 0) AS cashRefundsPaisa,
  IFNULL((SELECT SUM(sr.total_paisa) FROM supplier_returns sr
          WHERE sr.settled = 1 AND sr.date >= @from AND sr.date <= @to), 0) AS supplierCashInPaisa,
  -- F-32: money the shop spent (rent, tea, a repair) and cash the owner took both left the till.
  -- A GOODS drawing is deliberately absent: the medicine left the shelf, not the drawer.
  IFNULL((SELECT SUM(e.amount_paisa) FROM expenses e
          WHERE e.date >= @from AND e.date <= @to), 0) AS expensesPaisa,
  IFNULL((SELECT SUM(d.amount_paisa) FROM owner_drawings d
          WHERE d.type = 'cash' AND d.date >= @from AND d.date <= @to), 0) AS drawingsPaisa;",
            new { from, to });

        shift.CashSalesPaisa = drawer?.CashSalesPaisa ?? 0;
        shift.CashCollectionsPaisa = drawer?.CashCollectionsPaisa ?? 0;
        shift.SupplierCashOutPaisa = drawer?.SupplierCashOutPaisa ?? 0;
        shift.CashRefundsPaisa = drawer?.CashRefundsPaisa ?? 0;
        shift.SupplierCashInPaisa = drawer?.SupplierCashInPaisa ?? 0;
        shift.ExpensesPaisa = drawer?.ExpensesPaisa ?? 0;
        shift.DrawingsPaisa = drawer?.DrawingsPaisa ?? 0;

        shift.ExpectedPaisa = shift.OpeningFloatPaisa + shift.CashSalesPaisa + shift.CashCollectionsPaisa
                              + shift.SupplierCashInPaisa - shift.CashRefundsPaisa - shift.SupplierCashOutPaisa
                              - shift.ExpensesPaisa - shift.DrawingsPaisa;
    }
}
