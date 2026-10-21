using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The alert board (Spec 16, U-31, U-34). Two sources today:
///   • a medicine whose stock is MINUS - somebody sold what the shelf did not have;
///   • goods sitting in quarantine, waiting for a decision.
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

    private const int MinimumReasonLength = 3;

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public AlertService(Database db, AuditService audit, PermissionService permissions, LogService log)
    {
        _db = db;
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

            var dismissals = _db.Query<DismissalRow>(connection, @"
SELECT alert_key AS alertKey, alert_type AS alertType, entity_id AS entityId,
       reason, user_name AS userName, created_at AS createdAt
FROM alert_dismissals;")
                .ToDictionary(row => row.AlertKey, StringComparer.Ordinal);

            var report = new AlertReport();

            foreach (var problem in problems)
            {
                var isQuarantine = !string.IsNullOrWhiteSpace(problem.QCode);
                var key = $"{(isQuarantine ? TypeQuarantine : TypeNegativeStock)}:{problem.EntityId}";

                var alert = new AlertItem
                {
                    Key = key,
                    Type = isQuarantine ? TypeQuarantine : TypeNegativeStock,
                    EntityId = problem.EntityId,
                    Title = string.IsNullOrWhiteSpace(problem.Name) ? "(unknown medicine)" : problem.Name!,
                    Detail = isQuarantine
                        ? $"{Qty.Format(problem.Qty)} in quarantine since {ShortDate(problem.LastChange)} - decide: back in stock, damaged, or sold"
                        : $"stock {Qty.Format(problem.Qty)} - fix by a stock count or a purchase"
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
            _ => (null, 0)
        };
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
