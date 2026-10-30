using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The alert board (Spec 16, U-31, U-34, 15.2.1, D-50). Live shop facts include:
///   • a historical medicine whose stock is MINUS - D-93 blocks new decrements, but old rows stay visible;
///   • goods sitting in quarantine, waiting for a decision;
///   • a no-bill return that is still missing its bill;
///   • a batch inside the **supplier return window**: the supplier will take it back only until a
///     cutoff (three months before expiry by default), so the shop must act now or keep it (§9.3.6);
///   • a shelf at or below its reorder level - folded into ONE line, not one line per medicine;
///   • a batch already **expired** and still on the shelf - also one folded line;
///   • batches inside the nearest coming-expiry band (green reminders);
///   • opening/closing cash differences with their reason (red financial alerts);
///   • a drawer left open from an earlier day (a reminder, not a problem).
///
/// **Two kinds (D-50).** A *problem* - money or stock that is wrong now - is ended only by the
/// owner, with a written reason (the rule this service was built around). A *reminder* - the open
/// drawer, a bill that only needs a confirming word - may be put away by anybody with one click,
/// because it blocks nobody and hides no money. The classification lives in
/// <see cref="AlertItem.IsReminderType"/> so the model, the service and the screen cannot disagree,
/// and <see cref="DismissLight"/> refuses any key that is not a reminder.
///
/// The owner's rule: **an alert ends only when the owner writes a reason.** So an alert is never
/// deleted - it is *ended*, with the reason, the name and the time kept forever, and the owner can
/// bring it back if the reason was wrong.
///
/// One addition that the rule needs to be honest: **an ended alert re-opens by itself when the
/// situation moves again** (a new sale or stock movement for that medicine). Otherwise one dismissal
/// would silence the same medicine for ever, which is exactly the hole U-31 was written to close.
/// Quarantine has no movement while the goods sit there, so it stays ended until a decision is made.
/// </summary>
public sealed class AlertService
{
    public const string TypeNegativeStock = "negstock";
    public const string TypeQuarantine = "quarantine";
    public const string TypeProvisional = "provisional";
    public const string TypeProvisionalLinked = "provisional_linked";
    public const string TypeSupplierCutoff = "supplier_cutoff";

    /// <summary>A reminder: the drawer of an earlier day was never closed (light).</summary>
    public const string TypeShiftOpen = "shift_open";

    /// <summary>A shelf at or below its reorder level, folded into ONE alert (serious).</summary>
    public const string TypeLowStock = "lowstock";

    /// <summary>Batches already past their expiry date that are still on the shelf (serious).</summary>
    public const string TypeExpired = "expired";

    /// <summary>Coming expiry inside the owner's nearest band: an ordinary green reminder.</summary>
    public const string TypeExpirySoon = "expiry_soon";

    /// <summary>Actual opening/closing cash differs from its counted/expected baseline (red).</summary>
    public const string TypeShiftOpeningDifference = "shift_opening_difference";
    public const string TypeShiftClosingDifference = "shift_closing_difference";

    private const int MinimumReasonLength = 3;

    private readonly Database _db;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public AlertService(Database db, SettingsService settings, AuditService audit,
        PermissionService permissions, LogService log)
    {
        _db = db;
        _settings = settings;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Everything on the board, live and ended. No permission check: everybody may look.</summary>
    public AlertReport Report()
    {
        try
        {
            using var connection = _db.Open();

            var problems = new List<ProblemRow>();

            problems.AddRange(_db.Query<ProblemRow>(connection, @"
SELECT p.id AS entityId, p.name AS name,
       SUM(b.qty_base) AS qty,
       COALESCE((SELECT MAX(m.created_at) FROM stock_movements m WHERE m.product_id = p.id), '') AS lastChange
FROM products p
JOIN batches b ON b.product_id = p.id AND b.active = 1
WHERE p.active = 1
GROUP BY p.id, p.name
HAVING SUM(b.qty_base) < 0
ORDER BY SUM(b.qty_base) ASC;" ));

            problems.AddRange(_db.Query<ProblemRow>(connection, @"
SELECT r.id AS entityId, s.name_snapshot AS name, r.qty_base AS qty, r.created_at AS lastChange,
       r.qcode AS qcode, r.date AS date
FROM sale_returns r
LEFT JOIN sale_items s ON s.id = r.sale_item_id
WHERE r.disposition = 'quarantine'
ORDER BY r.created_at DESC;"));

            problems.AddRange(_db.Query<ProblemRow>(connection, @"
SELECT q.id AS entityId, 'No-bill return ' || q.code AS name, 0 AS qty,
       COALESCE(q.linked_at, q.created_at) AS lastChange,
       q.linked_code AS qcode, q.date AS date, q.status AS status, q.refund_paisa AS refundPaisa,
       q.linked_expected_paisa AS linkedExpectedPaisa,
       q.linked_unallocated_paisa AS linkedUnallocatedPaisa
FROM provisional_returns q
WHERE q.status IN ('pending', 'linked')
ORDER BY q.id DESC;"));

            // The supplier-return window (15.2.1, §9.3.6): goods may go back only until the cutoff,
            // so a batch that is inside the window is a reminder with a date on it. The two numbers
            // are settings because every supplier's policy is different.
            var cutoffDays = _settings.GetInt("supplier.returnCutoffDays", 90);
            var alertDays = _settings.GetInt("supplier.returnAlertDays", 180);

            if (cutoffDays > 0 && alertDays > cutoffDays)
            {
                problems.AddRange(_db.Query<ProblemRow>(connection, @"
SELECT b.id AS entityId, p.name || ' - ' || b.batch_no AS name, b.qty_base AS qty,
       b.expiry_date AS date, 'supplier_cutoff' AS type,
       CAST(julianday(b.expiry_date) - julianday(date('now','localtime')) AS INTEGER) AS daysLeft
FROM batches b
JOIN products p ON p.id = b.product_id
WHERE p.active = 1 AND b.active = 1 AND b.qty_base > 0
  AND IFNULL(b.expiry_date, '') <> ''
  AND julianday(b.expiry_date) - julianday(date('now','localtime')) BETWEEN @cutoff AND @alert
ORDER BY b.expiry_date ASC, p.name COLLATE NOCASE;",
                    new { cutoff = cutoffDays, alert = alertDays }));
            }

            // ---- a shelf running out: ONE line for all of them (serious) ----
            // The folded line uses only the names and the number of medicines. Do not map an
            // aggregate quantity that is never read: SQLite SUM may expose either Int64 or Double,
            // while the quantity itself remains REAL and must never be truncated for this screen.
            var lowStock = _db.Query<ProblemRow>(connection, @"
SELECT p.id AS entityId, p.name AS name
FROM products p
WHERE p.active = 1
  AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) >= 0
  AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) <= p.reorder_level
ORDER BY IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) ASC,
         p.name COLLATE NOCASE;");

            if (lowStock.Count > 0)
            {
                // A review of 200 medicines below their level would be useless on a phone screen, so
                // the line names the first three and counts the rest.
                var names = string.Join(", ", lowStock.Take(3).Select(row => row.Name).Where(name => !string.IsNullOrWhiteSpace(name)));

                problems.Add(new ProblemRow
                {
                    EntityId = lowStock.Count,                 // the count IS the key, see below
                    Count = lowStock.Count,
                    Type = TypeLowStock,
                    Title = lowStock.Count + (lowStock.Count == 1 ? " medicine is" : " medicines are")
                            + " below the reorder level",
                    Detail = (names.Length > 0 ? "lowest first: " + names : string.Empty)
                             + (lowStock.Count > 3 ? " and " + (lowStock.Count - 3) + " more" : string.Empty)
                             + " - buy them before the shelf is empty"
                });
            }

            // ---- goods already past their date and still on the shelf (serious) ----
            var expired = _db.Query<ProblemRow>(connection, @"
SELECT b.id AS entityId, p.name || ' - ' || b.batch_no AS name, b.qty_base AS qty,
       b.expiry_date AS date, CAST(b.qty_base * b.cost_paisa AS INTEGER) AS refundPaisa
FROM batches b
JOIN products p ON p.id = b.product_id
WHERE p.active = 1 AND b.active = 1 AND b.qty_base > 0
  AND IFNULL(b.expiry_date, '') <> ''
  AND b.expiry_date < date('now','localtime')
ORDER BY b.expiry_date ASC, p.name COLLATE NOCASE;");

            if (expired.Count > 0)
            {
                var money = expired.Sum(row => row.RefundPaisa);
                var names = string.Join(", ", expired.Take(3).Select(row => row.Name).Where(name => !string.IsNullOrWhiteSpace(name)));

                problems.Add(new ProblemRow
                {
                    EntityId = expired.Count,
                    Count = expired.Count,
                    Type = TypeExpired,
                    Title = expired.Count + (expired.Count == 1 ? " batch has" : " batches have")
                            + " expired - " + Money.Format(money) + " at cost",
                    Detail = (names.Length > 0 ? names : string.Empty)
                             + (expired.Count > 3 ? " and " + (expired.Count - 3) + " more" : string.Empty)
                             + " - write them off or send them back"
                });
            }

            // ---- batches nearing expiry: an ordinary green reminder, not a financial adjustment ----
            var expiryDays = _settings.ExpiryBands.Level3Days;
            var nearingExpiry = _db.Query<ProblemRow>(connection, @"
SELECT b.id AS entityId, p.name || ' - ' || b.batch_no AS name, b.qty_base AS qty,
       b.expiry_date AS date, 'expiry_soon' AS type,
       CAST(julianday(b.expiry_date) - julianday(date('now','localtime')) AS INTEGER) AS daysLeft
FROM batches b
JOIN products p ON p.id = b.product_id
WHERE p.active = 1 AND b.active = 1 AND b.qty_base > 0
  AND IFNULL(b.expiry_date, '') <> ''
  AND julianday(b.expiry_date) - julianday(date('now','localtime')) BETWEEN 0 AND @days
ORDER BY b.expiry_date ASC, p.name COLLATE NOCASE;", new { days = expiryDays });

            if (nearingExpiry.Count > 0)
            {
                var names = string.Join(", ", nearingExpiry.Take(3).Select(row => row.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name)));
                problems.Add(new ProblemRow
                {
                    EntityId = nearingExpiry.Count,
                    Count = nearingExpiry.Count,
                    Type = TypeExpirySoon,
                    Title = nearingExpiry.Count + (nearingExpiry.Count == 1 ? " batch is" : " batches are") +
                            " nearing expiry",
                    Detail = names + (nearingExpiry.Count > 3 ? " and more" : string.Empty) +
                             " - check the nearest dates and sell in FEFO order"
                });
            }

            // ---- opening and closing cash differences: red financial alerts, never money movement ----
            problems.AddRange(_db.Query<ProblemRow>(connection, @"
SELECT s.id AS entityId,
       'Opening shift #' || s.id || ' by ' || IFNULL(u.name, 'someone') AS name,
       s.opening_float_paisa AS amountPaisa, p.actual_paisa AS baselinePaisa,
       s.opened_at AS lastChange, s.opened_at AS date,
       'shift_opening_difference' AS type, s.note AS note
FROM shifts s
JOIN shifts p ON p.id = (
    SELECT previous.id FROM shifts previous
    WHERE previous.id < s.id AND previous.status = 'closed' AND previous.actual_paisa IS NOT NULL
    ORDER BY previous.id DESC LIMIT 1)
LEFT JOIN users u ON u.id = s.user_id
WHERE s.opening_float_paisa <> p.actual_paisa
ORDER BY s.id DESC;"));

            problems.AddRange(_db.Query<ProblemRow>(connection, @"
SELECT s.id AS entityId,
       'Closing shift #' || s.id || ' by ' || IFNULL((
           SELECT a.user_name FROM audit_logs a
           WHERE a.entity = 'shifts' AND a.entity_id = CAST(s.id AS TEXT)
             AND a.action IN ('shift_close', 'shift_handover')
           ORDER BY a.id DESC LIMIT 1), IFNULL(u.name, 'someone')) AS name,
       s.actual_paisa AS amountPaisa, IFNULL(s.expected_paisa, 0) AS baselinePaisa,
       IFNULL(s.closed_at, s.opened_at) AS lastChange, IFNULL(s.closed_at, s.opened_at) AS date,
       'shift_closing_difference' AS type, s.note AS note
FROM shifts s
LEFT JOIN users u ON u.id = s.user_id
WHERE s.status = 'closed' AND s.actual_paisa IS NOT NULL
  AND s.actual_paisa <> IFNULL(s.expected_paisa, 0)
ORDER BY s.id DESC;"));

            // ---- a drawer left open from an earlier day (a reminder) ----
            problems.AddRange(_db.Query<ProblemRow>(connection, @"
SELECT s.id AS entityId, IFNULL(u.name, 'someone') AS name, s.opened_at AS lastChange,
       s.opened_at AS date, 'shift_open' AS type
FROM shifts s
LEFT JOIN users u ON u.id = s.user_id
WHERE s.status = 'open'
  AND substr(s.opened_at, 1, 10) < date('now','localtime')
ORDER BY s.opened_at ASC;"));

            var dismissals = _db.Query<DismissalRow>(connection, @"
SELECT alert_key AS alertKey, alert_type AS alertType, entity_id AS entityId,
       reason, user_name AS userName, created_at AS createdAt
FROM alert_dismissals;")
                .ToDictionary(row => row.AlertKey, StringComparer.Ordinal);

            var report = new AlertReport();

            foreach (var problem in problems)
            {
                var kind = KindOf(problem);
                var key = kind + ":" + problem.EntityId.ToString();

                var alert = new AlertItem
                {
                    Key = key,
                    Type = kind,
                    EntityId = problem.EntityId,
                    Title = !string.IsNullOrWhiteSpace(problem.Title)
                        ? problem.Title!
                        : string.IsNullOrWhiteSpace(problem.Name) ? "(unknown medicine)" : problem.Name!,
                    Detail = !string.IsNullOrWhiteSpace(problem.Detail)
                        ? problem.Detail!
                        : DetailOf(problem, kind, cutoffDays),

                    // A folded line must keep its own number: the title says "2 medicines", and a row
                    // that says Count = 1 while the title says 2 is the kind of small lie that makes
                    // the operator stop trusting the screen.
                    Count = problem.Count
                };

                if (dismissals.TryGetValue(key, out var dismissal) && !MovedSince(problem.LastChange, dismissal.CreatedAt))
                {
                    alert.Ended = true;
                    alert.Reason = dismissal.Reason;
                    alert.EndedBy = dismissal.UserName;
                    alert.EndedAt = ShortDate(dismissal.CreatedAt);
                    report.Ended.Add(alert);
                }
                else
                {
                    report.Live.Add(alert);
                }
            }

            return report;
        }
        catch (Exception exception)
        {
            _log.Error("The alert board could not be built.", exception);
            return new AlertReport();
        }
    }

    /// <summary>Ends one alert for the owner's reason. Owner only, reason compulsory (16.4.1-16.4.3).</summary>
    public ServiceResult Dismiss(User? user, string? key, string? reason)
    {
        if (user is null || !user.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can end an alert by writing a reason.");
        }

        var cleanKey = (key ?? string.Empty).Trim();
        var cleanReason = (reason ?? string.Empty).Trim();

        if (cleanKey.Length == 0)
        {
            return ServiceResult.Fail("There is no alert selected.");
        }

        if (cleanReason.Length < MinimumReasonLength)
        {
            return ServiceResult.Fail(
                $"Write a reason of at least {MinimumReasonLength} characters - the alert does not end without one.");
        }

        var (type, entityId) = SplitKey(cleanKey);
        if (type is null)
        {
            return ServiceResult.Fail("That alert key is not recognised.");
        }

        try
        {
            _db.Tx(connection =>
            {
                _db.Execute(connection, @"
INSERT INTO alert_dismissals(alert_key, alert_type, entity_id, reason, user_id, user_name, created_at)
VALUES(@key, @type, @entityId, @reason, @userId, @userName, @at)
ON CONFLICT(alert_key) DO UPDATE SET
    reason = excluded.reason,
    user_id = excluded.user_id,
    user_name = excluded.user_name,
    created_at = excluded.created_at;",
                    new
                    {
                        key = cleanKey,
                        type,
                        entityId,
                        reason = cleanReason,
                        userId = user.Id,
                        userName = user.Name,
                        at = Clock.Now()
                    });

                _audit.Record(connection, user, "Alerts", "alert_dismiss", "alert_dismissals", cleanKey,
                    oldValue: "active", newValue: $"dismissed: {cleanReason}");
            });

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"Alert {cleanKey} could not be ended.", exception);
            return ServiceResult.Fail("The alert could not be ended.");
        }
    }

    /// <summary>
    /// Puts a **reminder** away - the light door (D-50). Any signed-in person may do it, with no
    /// reason typed, because a reminder blocks nobody: the drawer is still open on the Shift page and
    /// the bill is still attached; only the sentence in the bell disappears.
    ///
    /// It refuses any key that is **not** a reminder. That guard is the whole safety of this method:
    /// without it, the light door would became a way for a cashier to silence "stock is minus".
    /// The row goes into the same `alert_dismissals` table, so the history shows who put it away.
    /// </summary>
    public ServiceResult DismissLight(User? user, string? key)
    {
        if (user is null)
        {
            return ServiceResult.Fail("Sign in first.");
        }

        var cleanKey = (key ?? string.Empty).Trim();
        if (cleanKey.Length == 0)
        {
            return ServiceResult.Fail("There is no reminder selected.");
        }

        var (type, entityId) = SplitKey(cleanKey);
        if (type is null)
        {
            return ServiceResult.Fail("That alert key is not recognised.");
        }

        if (!AlertItem.IsReminderType(type))
        {
            return ServiceResult.Fail(
                "That is not a reminder - only the owner can end it, and only with a written reason.");
        }

        try
        {
            _db.Tx(connection =>
            {
                _db.Execute(connection, @"
INSERT INTO alert_dismissals(alert_key, alert_type, entity_id, reason, user_id, user_name, created_at)
VALUES(@key, @type, @entityId, @reason, @userId, @userName, @at)
ON CONFLICT(alert_key) DO UPDATE SET
    reason = excluded.reason,
    user_id = excluded.user_id,
    user_name = excluded.user_name,
    created_at = excluded.created_at;",
                    new
                    {
                        key = cleanKey,
                        type,
                        entityId,

                        // The reason column is NOT NULL, and it should say plainly that nobody wrote
                        // one: this is a "seen it" tick, not a decision about money.
                        reason = "seen by " + user.Name,
                        userId = user.Id,
                        userName = user.Name,
                        at = Clock.Now()
                    });

                _audit.Record(connection, user, "Alerts", "alert_seen", "alert_dismissals", cleanKey,
                    oldValue: "active", newValue: "reminder closed");
            });

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"Reminder {cleanKey} could not be closed.", exception);
            return ServiceResult.Fail("The reminder could not be closed.");
        }
    }

    /// <summary>Brings an ended alert back to the board (16.4.4). Owner only.</summary>
    public ServiceResult Restore(User? user, string? key)
    {
        if (user is null || !user.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can bring an alert back.");
        }

        var cleanKey = (key ?? string.Empty).Trim();
        if (cleanKey.Length == 0)
        {
            return ServiceResult.Fail("There is no alert selected.");
        }

        try
        {
            _db.Tx(connection =>
            {
                var reason = _db.QuerySingle<string>(connection,
                    "SELECT reason FROM alert_dismissals WHERE alert_key = @key;", new { key = cleanKey });

                if (reason is null)
                {
                    throw new ValidationException("That alert is not ended, so there is nothing to bring back.");
                }

                _db.Execute(connection, "DELETE FROM alert_dismissals WHERE alert_key = @key;", new { key = cleanKey });

                _audit.Record(connection, user, "Alerts", "alert_restore", "alert_dismissals", cleanKey,
                    oldValue: $"dismissed: {reason}", newValue: "active");
            });

            return ServiceResult.Success();
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error($"Alert {cleanKey} could not be brought back.", exception);
            return ServiceResult.Fail("The alert could not be brought back.");
        }
    }

    /// <summary>
    /// Every reason the owner ever gave, newest first - the "alerts the owner ended" card.
    /// This reads the dismissal table itself, not the live problems: a reason must stay on record
    /// even after the stock is corrected or the quarantine item is decided (16.4.3).
    /// </summary>
    public List<AlertItem> Ended()
    {
        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<DismissalRow>(connection, @"
SELECT alert_key AS alertKey, alert_type AS alertType, entity_id AS entityId,
       reason, user_name AS userName, created_at AS createdAt
FROM alert_dismissals
ORDER BY created_at DESC, id DESC;");

            var items = new List<AlertItem>(rows.Count);

            foreach (var row in rows)
            {
                items.Add(new AlertItem
                {
                    Key = row.AlertKey,
                    Type = row.AlertType,
                    EntityId = row.EntityId ?? 0,
                    Title = NameOf(connection, row.AlertType, row.EntityId),
                    Detail = "ended by the owner",
                    Ended = true,
                    Reason = row.Reason,
                    EndedBy = row.UserName,
                    EndedAt = ShortDate(row.CreatedAt)
                });
            }

            return items;
        }
        catch (Exception exception)
        {
            _log.Error("The ended alerts could not be listed.", exception);
            return new List<AlertItem>();
        }
    }

    private string NameOf(Microsoft.Data.Sqlite.SqliteConnection connection, string? type, int? entityId)
    {
        if (entityId is not int id)
        {
            return string.Empty;
        }

        return type switch
        {
            TypeNegativeStock => _db.QuerySingle<string>(connection,
                "SELECT name FROM products WHERE id = @id;", new { id }) ?? string.Empty,
            TypeQuarantine => _db.QuerySingle<string>(connection, @"
SELECT s.name_snapshot FROM sale_returns r
LEFT JOIN sale_items s ON s.id = r.sale_item_id
WHERE r.id = @id;", new { id }) ?? string.Empty,
            TypeSupplierCutoff => _db.QuerySingle<string>(connection, @"
SELECT p.name || ' - ' || b.batch_no FROM batches b
JOIN products p ON p.id = b.product_id WHERE b.id = @id;", new { id }) ?? string.Empty,
            TypeProvisional or TypeProvisionalLinked => _db.QuerySingle<string>(connection,
                "SELECT 'No-bill return ' || code FROM provisional_returns WHERE id = @id;", new { id }) ?? string.Empty,
            TypeShiftOpeningDifference or TypeShiftClosingDifference => "Shift #" + id,
            _ => string.Empty
        };
    }

    private static (string? Type, int EntityId) SplitKey(string key)
    {
        var parts = key.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var entityId))
        {
            return (null, 0);
        }

        return parts[0] switch
        {
            TypeNegativeStock => (TypeNegativeStock, entityId),
            TypeQuarantine => (TypeQuarantine, entityId),
            TypeProvisional => (TypeProvisional, entityId),
            TypeProvisionalLinked => (TypeProvisionalLinked, entityId),
            TypeSupplierCutoff => (TypeSupplierCutoff, entityId),
            TypeShiftOpen => (TypeShiftOpen, entityId),
            TypeLowStock => (TypeLowStock, entityId),
            TypeExpired => (TypeExpired, entityId),
            TypeExpirySoon => (TypeExpirySoon, entityId),
            TypeShiftOpeningDifference => (TypeShiftOpeningDifference, entityId),
            TypeShiftClosingDifference => (TypeShiftClosingDifference, entityId),
            _ => (null, 0)
        };
    }

    /// <summary>
    /// Which of the three lists a row belongs to. A medicine with minus stock is red. Goods in
    /// quarantine need a decision. A no-bill return is red until the bill is attached and becomes
    /// a green confirmation reminder afterwards, because the money question is then waiting for review (15.5.4).
    /// </summary>
    private static string KindOf(ProblemRow problem)
    {
        // A query that knows its own kind says so; the older lists are still read from their fields
        // because a no-bill return's colour depends on its status.
        if (!string.IsNullOrWhiteSpace(problem.Type))
        {
            return problem.Type!;
        }

        if (!string.IsNullOrWhiteSpace(problem.Status))
        {
            return string.Equals(problem.Status, "linked", StringComparison.OrdinalIgnoreCase)
                ? TypeProvisionalLinked
                : TypeProvisional;
        }

        return string.IsNullOrWhiteSpace(problem.QCode) ? TypeNegativeStock : TypeQuarantine;
    }

    private static string DetailOf(ProblemRow problem, string kind, int cutoffDays)
    {
        if (kind == TypeProvisionalLinked)
        {
            var bill = string.IsNullOrWhiteSpace(problem.QCode) ? "(unknown)" : problem.QCode;

            if (problem.LinkedExpectedPaisa is null)
            {
                return "bill " + bill + " attached before line reconciliation - attach the same bill " +
                       "again on the No bill (PR) page before another return";
            }

            var variance = problem.RefundPaisa - problem.LinkedExpectedPaisa.Value;
            if (variance != 0 || (problem.LinkedUnallocatedPaisa ?? 0) > 0)
            {
                return "bill " + bill + " attached - PR refund differs from the returned quantities' " +
                       "bill value by " + Money.Format(Math.Abs(variance)) +
                       "; check the reconciliation, then end this alert";
            }

            return "bill " + bill + " attached - confirm it is the right bill, then end this alert";
        }

        if (kind is TypeShiftOpeningDifference or TypeShiftClosingDifference)
        {
            var opening = kind == TypeShiftOpeningDifference;
            var difference = problem.AmountPaisa - problem.BaselinePaisa;
            var baseline = opening ? "previous counted" : "expected";
            var reason = string.IsNullOrWhiteSpace(problem.Note) ? "(not recorded)" : problem.Note;

            return (opening ? "opening " : "counted ") + Money.Format(problem.AmountPaisa) +
                   "; " + baseline + " " + Money.Format(problem.BaselinePaisa) +
                   "; " + ShiftWords.Difference(difference) + "; reason: " + reason +
                   ". Review shift #" + problem.EntityId + " - no automatic payment or drawer adjustment was made";
        }

        return kind switch
        {
            TypeSupplierCutoff =>
                Qty.Format(problem.Qty) + " expiring " + ShortDate(problem.Date) + " (" + problem.DaysLeft +
                " days) - send it back by " + LastReturnDay(problem.Date, cutoffDays) +
                ", after that the supplier may refuse it",
            TypeQuarantine =>
                Qty.Format(problem.Qty) + " in quarantine since " + ShortDate(problem.LastChange) +
                " - decide: back in stock, damaged, or sold",
            TypeProvisional =>
                Money.Format(problem.RefundPaisa) + " refunded on " + ShortDate(problem.Date) +
                " with no bill - attach the bill",
            TypeShiftOpen =>
                "opened " + ShortDate(problem.Date) + " by " + (problem.Name ?? "someone") +
                " - count the drawer and close it on the Shift (galla) page",
            _ => "stock " + Qty.Format(problem.Qty) + " - fix by a stock count or a purchase"
        };
    }

    /// <summary>The last day the supplier still takes this batch back: expiry minus the cutoff.</summary>
    private static string LastReturnDay(string? expiryDate, int cutoffDays)
    {
        if (string.IsNullOrWhiteSpace(expiryDate) ||
            !DateTime.TryParse(expiryDate, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
        {
            return "the cutoff";
        }

        return date.AddDays(-cutoffDays).ToString("yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Did the situation move after the owner ended it? Then the episode is a new one.</summary>
    private static bool MovedSince(string? lastChange, string? dismissedAt) =>
        !string.IsNullOrWhiteSpace(lastChange)
        && !string.IsNullOrWhiteSpace(dismissedAt)
        && string.CompareOrdinal(lastChange, dismissedAt) > 0;

    private static string ShortDate(string? raw) =>
        raw is { Length: >= 16 } ? raw[..16] : raw ?? string.Empty;

    private sealed class ProblemRow
    {
        public int EntityId { get; set; }

        public string? Name { get; set; }

        public double Qty { get; set; }

        public string? LastChange { get; set; }

        public string? QCode { get; set; }

        public string? Date { get; set; }

        public string? Status { get; set; }

        public long RefundPaisa { get; set; }

        public long? LinkedExpectedPaisa { get; set; }

        public long? LinkedUnallocatedPaisa { get; set; }

        /// <summary>Shift actual/opening cash and the previous-counted/expected baseline.</summary>
        public long AmountPaisa { get; set; }

        public long BaselinePaisa { get; set; }

        public string? Note { get; set; }

        /// <summary>Set only by a query that already knows its kind (the supplier cutoff).</summary>
        public string? Type { get; set; }

        /// <summary>Days until expiry - only the supplier-cutoff query fills it.</summary>
        public int DaysLeft { get; set; }

        /// <summary>Set by a folded alert (a shelf running out, batches expired): its own headline.</summary>
        public string? Title { get; set; }

        /// <summary>Set by a folded alert: the sentence under the headline.</summary>
        public string? Detail { get; set; }

        /// <summary>How many rows were folded into this one line (1 for a single row).</summary>
        public int Count { get; set; } = 1;
    }

    private sealed class DismissalRow
    {
        public string AlertKey { get; set; } = string.Empty;

        public string AlertType { get; set; } = string.Empty;

        public int? EntityId { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string? UserName { get; set; }

        public string? CreatedAt { get; set; }
    }
}
