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

    /// <summary>Current Cash reader and current senior flag; not an authorization token.</summary>
    public ServiceResult<bool> CheckAccess(User? user)
    {
        try
        {
            using var connection = _db.Open();
            var reader = RequireCash(connection, user);
            return ServiceResult<bool>.Success(reader.CanManage);
        }
        catch (ValidationException exception) { return ServiceResult<bool>.Fail(exception.Message); }
        catch (Exception) { return ServiceResult<bool>.Fail("Current Cash access could not be confirmed."); }
    }

    /// <summary>Only open/closed state for Counter/session callers; never exposes drawer money.</summary>
    public ServiceResult<bool> DrawerState(User? user)
    {
        try
        {
            using var connection = _db.Open();
            SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Cash, Modules.Pos);
            var count = Convert.ToInt64(_db.Scalar(connection, "SELECT COUNT(1) FROM shifts WHERE status = 'open';") ?? 0L);
            return ServiceResult<bool>.Success(count > 0);
        }
        catch (ValidationException exception) { return ServiceResult<bool>.Fail(exception.Message); }
        catch (Exception exception)
        {
            _log.Error("Drawer state could not be read.", exception);
            return ServiceResult<bool>.Fail("Drawer state could not be confirmed. Refresh before making a bill.");
        }
    }

    /// <summary>Failing reads are not a closed drawer or an empty certified history.</summary>
    public ServiceResult<ShiftPage> Page(User? user, int limit = 60)
    {
        if (limit < 1 || limit > 500) return ServiceResult<ShiftPage>.Fail("Choose between 1 and 500 shifts.");
        try
        {
            using var connection = _db.Open();
            var reader = RequireCash(connection, user);
            var current = ReadOpen(connection);
            if (current is not null) FillDrawer(connection, current, current.OpenedAt, Clock.Now());
            return ServiceResult<ShiftPage>.Success(new ShiftPage
            {
                Current = current,
                Previous = ReadLastCounted(connection),
                History = ReadHistory(connection, limit),
                Discrepancies = ReadDiscrepancies(connection, limit),
                CanSeePeople = reader.CanManage
            });
        }
        catch (ValidationException exception) { return ServiceResult<ShiftPage>.Fail(exception.Message); }
        catch (Exception exception)
        {
            _log.Error("The drawer page could not be read.", exception);
            return ServiceResult<ShiftPage>.Fail("The drawer could not be read. Refresh; its open/closed state is not confirmed.");
        }
    }

    /// <summary>Opens the drawer with a float. Refused while another shift is open.</summary>
    public ServiceResult<ShiftMoney> Open(User user, long openingFloatPaisa, string? note)
    {
        if (openingFloatPaisa < 0)
        {
            return ServiceResult<ShiftMoney>.Fail("The opening float cannot be negative.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var reader = RequireCash(connection, user);
                if (ReadOpen(connection) is not null)
                {
                    return ServiceResult<ShiftMoney>.Fail(
                        "A shift is already open. Close it before counting the drawer again.");
                }

                var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note!.Trim();
                var previous = ReadLastCounted(connection);
                var openingDifference = previous?.ActualPaisa is long counted
                    ? openingFloatPaisa - counted
                    : 0L;

                if (openingDifference != 0 && !HasDifferenceReason(cleanNote))
                {
                    return ServiceResult<ShiftMoney>.Fail(
                        $"Opening cash differs from the previous counted {Money.Format(previous!.ActualPaisa!.Value)} " +
                        $"by {Money.Format(Math.Abs(openingDifference))}. Write the reason before opening the shift.");
                }

                var at = Clock.Now();

                _db.Execute(connection, @"
INSERT INTO shifts(user_id, opened_at, opening_float_paisa, status, note)
VALUES(@userId, @at, @openingFloat, 'open', @note);",
                    new
                    {
                        userId = reader.Id,
                        at,
                        openingFloat = openingFloatPaisa,
                        note = cleanNote
                    });

                var id = (int)_db.NextId(connection);

                // Portion 14: the opening difference gets its own contract row (migration 030) - the
                // reason stops hiding inside a general note. Exact floats leave no row.
                if (openingDifference != 0 && previous?.ActualPaisa is long previousCash)
                {
                    RecordDiscrepancy(connection, id, "opening", previousCash, openingFloatPaisa,
                        openingDifference, cleanNote ?? string.Empty, at);
                }

                _audit.Record(connection, reader, "Cash", "shift_open", "shifts", id.ToString(),
                    newValue: Money.Format(openingFloatPaisa),
                    details: previous?.ActualPaisa is long previousCounted
                        ? $"Previous counted {Money.Format(previousCounted)}; opening " +
                          $"{ShiftWords.Difference(openingDifference)}; reason: {cleanNote ?? "(not required)"}"
                        : cleanNote);

                var shift = ReadOpen(connection);

                if (shift is null)
                {
                    throw new ValidationException("The shift could not be opened. Nothing was changed.");
                }

                FillDrawer(connection, shift, shift.OpenedAt, Clock.Now());
                return ServiceResult<ShiftMoney>.Success(shift);
            });
        }
        catch (ValidationException exception) { return ServiceResult<ShiftMoney>.Fail(exception.Message); }
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
        if (actualPaisa < 0)
        {
            return ServiceResult<ShiftMoney>.Fail("The counted cash cannot be negative.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var reader = RequireCash(connection, user);
                var shift = ReadOpen(connection);

                if (shift is null)
                {
                    return ServiceResult<ShiftMoney>.Fail("There is no open shift to close.");
                }

                // The person who opened the drawer closes it, or somebody senior does it for him.
                if (shift.UserId != reader.Id && !reader.CanManage)
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

                if (shift.DifferencePaisa != 0 && !HasDifferenceReason(cleanNote))
                {
                    return ServiceResult<ShiftMoney>.Fail(
                        $"The drawer is {shift.DifferenceLabel}. Write the reason before closing the shift; " +
                        "even a Rs 1 difference needs one.");
                }

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

                _audit.RecordChange(connection, reader, "Cash", "shift_close", "shifts", shift.Id.ToString(),
                    new { Status = "open" },
                    new
                    {
                        Status = "closed",
                        Expected = shift.ExpectedPaisa,
                        Actual = actualPaisa,
                        Difference = shift.DifferencePaisa
                    },
                    cleanNote);

                // Portion 14: the closing difference gets its own contract row (migration 030), next to
                // the audit line. An exact count leaves no row.
                if (shift.DifferencePaisa != 0)
                {
                    RecordDiscrepancy(connection, shift.Id, "closing", shift.ExpectedPaisa, actualPaisa,
                        shift.DifferencePaisa.Value, cleanNote ?? string.Empty, closedAt);
                }

                return ServiceResult<ShiftMoney>.Success(shift);
            });
        }
        catch (ValidationException exception) { return ServiceResult<ShiftMoney>.Fail(exception.Message); }
        catch (Exception exception)
        {
            _log.Error("The shift could not be closed.", exception);
            return ServiceResult<ShiftMoney>.Fail("The shift could not be closed.");
        }
    }

    /// <summary>
    /// Hands the drawer from one person to the next (Spec 14.4 / 18.2, F-55). One transaction does
    /// three things that must never be separated: it counts and closes the first shift, it writes
    /// who gave it to whom, and it opens the next shift **with the counted money as its float**.
    ///
    /// Why the counted number and not the expected one: the next person did not cause the
    /// difference and must not be asked to explain it. The shortfall (or the extra) is booked
    /// against the shift that produced it, and the new shift starts clean with what is actually in
    /// the drawer. If this used the expected figure instead, every handover would hide a gap and
    /// the day's total would drift by it for ever.
    ///
    /// The new person must be on the staff list, active, and allowed to work the till - handing a
    /// drawer to somebody who cannot open it leaves the money in nobody's name.
    /// </summary>
    public ServiceResult<ShiftHandover> Handover(User user, User next, long countedPaisa, string? note)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(next);

        if (next.Id == user.Id)
        {
            return ServiceResult<ShiftHandover>.Fail(
                "That is the same person. Choose who is taking the drawer.");
        }

        if (next.Id <= 0 || string.IsNullOrWhiteSpace(next.Name))
        {
            return ServiceResult<ShiftHandover>.Fail("Choose the person who is taking the drawer.");
        }

        if (!next.Active)
        {
            return ServiceResult<ShiftHandover>.Fail(next.Name + " is switched off and cannot take the drawer.");
        }

        if (countedPaisa < 0)
        {
            return ServiceResult<ShiftHandover>.Fail("The counted cash cannot be negative.");
        }

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note!.Trim();

        try
        {
            return _db.Tx(connection =>
            {
                var reader = RequireCash(connection, user);
                var recipient = RequireCashTarget(connection, next);
                var shift = ReadOpen(connection);

                if (shift is null)
                {
                    return ServiceResult<ShiftHandover>.Fail("There is no open shift to hand over.");
                }

                // The same rule as closing: the person who opened the drawer, or somebody senior.
                if (shift.UserId != reader.Id && !reader.CanManage)
                {
                    return ServiceResult<ShiftHandover>.Fail(
                        "Only the person who opened this shift, or the owner or a manager, can hand it over.");
                }

                var at = Clock.Now();
                FillDrawer(connection, shift, shift.OpenedAt, at);

                shift.ActualPaisa = countedPaisa;
                shift.DifferencePaisa = countedPaisa - shift.ExpectedPaisa;
                shift.Status = "closed";
                shift.HandedToName = recipient.Name.Trim();

                if (shift.DifferencePaisa != 0 && !HasDifferenceReason(cleanNote))
                {
                    return ServiceResult<ShiftHandover>.Fail(
                        $"The drawer is {shift.DifferenceLabel}. Write the reason before handing over; " +
                        "the next shift will still start with the actual counted cash.");
                }

                _db.Execute(connection, @"
UPDATE shifts
SET closed_at = @at, expected_paisa = @expected, actual_paisa = @actual,
    difference_paisa = @difference, status = 'closed', note = IFNULL(@note, note),
    handed_to_user_id = @nextId, handed_to_name = @nextName
WHERE id = @id;",
                    new
                    {
                        at,
                        expected = shift.ExpectedPaisa,
                        actual = countedPaisa,
                        difference = shift.DifferencePaisa,
                        note = cleanNote,
                        nextId = recipient.Id,
                        nextName = recipient.Name.Trim(),
                        id = shift.Id
                    });

                _db.Execute(connection, @"
INSERT INTO shifts(user_id, opened_at, opening_float_paisa, status, note, opened_from_shift_id)
VALUES(@userId, @at, @openingFloat, 'open', @note, @fromId);",
                    new
                    {
                        userId = recipient.Id,
                        at,
                        openingFloat = countedPaisa,
                        note = "Handover from " + reader.Name.Trim(),
                        fromId = shift.Id
                    });

                var nextShiftId = (int)_db.NextId(connection);

                _audit.RecordChange(connection, reader, "Cash", "shift_handover", "shifts", shift.Id.ToString(),
                    new { Status = "open" },
                    new
                    {
                        Status = "closed",
                        Expected = shift.ExpectedPaisa,
                        Actual = countedPaisa,
                        Difference = shift.DifferencePaisa,
                        GivenTo = recipient.Name.Trim()
                    },
                    cleanNote);

                _audit.Record(connection, reader, "Cash", "shift_open", "shifts", nextShiftId.ToString(),
                    newValue: Money.Format(countedPaisa) + " from drawer " + shift.Id);

                // Portion 14: the handed-over drawer's difference gets its own contract row, inside
                // the same transaction. The new shift starts with the counted cash, so it never
                // owes an opening row of its own. An exact count leaves no row.
                if (shift.DifferencePaisa != 0)
                {
                    RecordDiscrepancy(connection, shift.Id, "closing", shift.ExpectedPaisa, countedPaisa,
                        shift.DifferencePaisa.Value, cleanNote ?? string.Empty, at);
                }

                var closedShift = ReadShift(connection, shift.Id)
                    ?? throw new ValidationException("The recorded handover could not be read. Nothing was changed.");

                // The stored row keeps the expected/actual/difference, but the six lines that
                // explain *how* that expected figure was reached are not columns - they are read
                // from the history. The slip prints them, so the closed shift is filled again with
                // exactly the same rule it was closed with. Without this the slip would hand the
                // next person a piece of paper full of zeros.
                FillDrawer(connection, closedShift, closedShift.OpenedAt, at);

                var openedShift = ReadShift(connection, nextShiftId);

                if (openedShift is null)
                {
                    throw new ValidationException("The next shift could not be opened. Nothing was changed.");
                }

                FillDrawer(connection, openedShift, openedShift.OpenedAt, at);

                return ServiceResult<ShiftHandover>.Success(new ShiftHandover
                {
                    From = closedShift,
                    To = openedShift,
                    GivenBy = reader.Name.Trim(),
                    GivenTo = recipient.Name.Trim(),
                    GivenAt = at
                });
            });
        }
        catch (ValidationException exception) { return ServiceResult<ShiftHandover>.Fail(exception.Message); }
        catch (Exception exception)
        {
            _log.Error("The shift could not be handed over.", exception);
            return ServiceResult<ShiftHandover>.Fail("The shift could not be handed over. Nothing was changed.");
        }
    }

    /// <summary>
    /// Who counted the drawer, and how far each person's counts landed from what the bills said
    /// (F-56, Spec 7.2.7). Read-only: it opens no till and changes no row.
    ///
    /// **Why per person and not per day.** A single short count is noise - a note miscounted, a
    /// rupee that rolled under the tray. The same person short in three counts out of five is a
    /// pattern, and a daily report cannot show it because every day starts from zero. Adding one
    /// person's differences over a month is the only arithmetic that reveals it.
    ///
    /// **Who may ask.** Cash permission *and* a manager or the owner: this is the page that says
    /// somebody's drawer was short, and that is a conversation for the person who runs the shop.
    /// A cashier who may open the till still may not read his colleagues' differences.
    ///
    /// **Short and over stay apart** (the same rule as the books check, F-54): one net number would
    /// let a drawer that leaks cancel a drawer that hoards, and the shop would read "no problem".
    /// </summary>
    public ServiceResult<ShiftPersonReport> ByPerson(User? user, int days = 30)
    {
        if (days < 1 || days > 3650)
        {
            return ServiceResult<ShiftPersonReport>.Fail("Choose a window between 1 and 3650 days.");
        }

        try
        {
            using var connection = _db.Open();
            var reader = RequireCash(connection, user);
            if (!reader.CanManage)
                return ServiceResult<ShiftPersonReport>.Fail("Only the owner or a manager can see who was short on the drawer.");

            // A closed shift always has both numbers: Close() writes them in the same statement that
            // marks the row closed. The filter is belt-and-braces for a file written by something
            // else - a shift with no counted cash has no difference and must not be counted as zero.
            var people = _db.Query<ShiftPersonSummary>(connection, @"
SELECT s.user_id AS userId, IFNULL(u.name, '(removed)') AS name,
       COUNT(1) AS closedShifts,
       IFNULL(SUM(s.expected_paisa), 0) AS expectedPaisa,
       IFNULL(SUM(s.actual_paisa), 0) AS countedPaisa,
       IFNULL(SUM(s.actual_paisa - s.expected_paisa), 0) AS differencePaisa,
       SUM(CASE WHEN s.actual_paisa - s.expected_paisa < 0 THEN 1 ELSE 0 END) AS shortCount,
       SUM(CASE WHEN s.actual_paisa - s.expected_paisa > 0 THEN 1 ELSE 0 END) AS overCount,
       SUM(CASE WHEN s.actual_paisa = s.expected_paisa THEN 1 ELSE 0 END) AS exactCount,
       IFNULL(MIN(CASE WHEN s.actual_paisa < s.expected_paisa
                       THEN s.expected_paisa - s.actual_paisa END), 0) AS worstShortPaisa,
       IFNULL(MAX(CASE WHEN s.actual_paisa > s.expected_paisa
                       THEN s.actual_paisa - s.expected_paisa END), 0) AS worstOverPaisa
FROM shifts s
LEFT JOIN users u ON u.id = s.user_id
WHERE s.status = 'closed'
  AND s.actual_paisa IS NOT NULL
  AND s.expected_paisa IS NOT NULL
  AND IFNULL(s.closed_at, s.opened_at) >= datetime('now', 'localtime', @cut)
GROUP BY s.user_id, u.name
ORDER BY ABS(IFNULL(SUM(s.actual_paisa - s.expected_paisa), 0)) DESC, IFNULL(u.name, '') COLLATE NOCASE;",
                new { cut = "-" + days + " days" });

            // The shop's short and over are added up from the SHIFT ROWS, not from the people above.
            // Why: a person can be short in one count and over in another, and their row shows one
            // net number - which is right for "where does this person stand" and wrong for "how much
            // did the shop lose". Adding the nets would report a shop that was short Rs 500 once and
            // over Rs 200 once as simply "Rs 300 short", and both facts would be gone. The first
            // version of this method did exactly that, and a real SQLite file showed it in one run.
            var shop = _db.QuerySingle<ShopTotals>(connection, @"
SELECT COUNT(1) AS closedShifts,
       IFNULL(SUM(s.expected_paisa), 0) AS expectedPaisa,
       IFNULL(SUM(s.actual_paisa), 0) AS countedPaisa,
       IFNULL(SUM(CASE WHEN s.actual_paisa < s.expected_paisa
                       THEN s.expected_paisa - s.actual_paisa ELSE 0 END), 0) AS missingPaisa,
       IFNULL(SUM(CASE WHEN s.actual_paisa > s.expected_paisa
                       THEN s.actual_paisa - s.expected_paisa ELSE 0 END), 0) AS tooMuchPaisa
FROM shifts s
WHERE s.status = 'closed'
  AND s.actual_paisa IS NOT NULL
  AND s.expected_paisa IS NOT NULL
  AND IFNULL(s.closed_at, s.opened_at) >= datetime('now', 'localtime', @cut);",
                new { cut = "-" + days + " days" });

            var report = new ShiftPersonReport
            {
                Days = days,
                People = people,
                ClosedShifts = shop?.ClosedShifts ?? 0,
                ExpectedPaisa = shop?.ExpectedPaisa ?? 0,
                CountedPaisa = shop?.CountedPaisa ?? 0,
                MissingPaisa = shop?.MissingPaisa ?? 0,
                TooMuchPaisa = shop?.TooMuchPaisa ?? 0
            };

            return ServiceResult<ShiftPersonReport>.Success(report);
        }
        catch (ValidationException exception) { return ServiceResult<ShiftPersonReport>.Fail(exception.Message); }
        catch (Exception exception)
        {
            _log.Error("The per-person shift report could not be read.", exception);
            return ServiceResult<ShiftPersonReport>.Fail("The shift report could not be read.");
        }
    }

    private List<ShiftDiscrepancy> ReadDiscrepancies(Microsoft.Data.Sqlite.SqliteConnection connection, int limit) =>
        _db.Query<ShiftDiscrepancy>(connection, @"
SELECT d.id AS id, d.shift_id AS shiftId, d.stage AS stage,
       d.expected_paisa AS expectedPaisa, d.counted_paisa AS countedPaisa,
       d.difference_paisa AS differencePaisa, d.reason AS reason,
       d.alerted_at AS alertedAt, d.created_at AS createdAt,
       IFNULL(u.name, '(removed)') AS openedBy, s.opened_at AS openedAt
FROM shift_discrepancies d
JOIN shifts s ON s.id = d.shift_id
LEFT JOIN users u ON u.id = s.user_id
ORDER BY d.id DESC
LIMIT @limit;", new { limit });

    private List<ShiftMoney> ReadHistory(Microsoft.Data.Sqlite.SqliteConnection connection, int limit) =>
        _db.Query<ShiftMoney>(connection, @"
SELECT s.id AS id, s.opened_at AS openedAt, IFNULL(u.name, '') AS openedBy,
       s.opening_float_paisa AS openingFloatPaisa, IFNULL(s.expected_paisa, 0) AS expectedPaisa,
       s.actual_paisa AS actualPaisa, s.difference_paisa AS differencePaisa,
       s.status AS status, s.note AS note, s.handed_to_name AS handedToName,
       s.opened_from_shift_id AS openedFromShiftId
FROM shifts s
LEFT JOIN users u ON u.id = s.user_id
ORDER BY s.id DESC
LIMIT @limit;", new { limit });

    // Same connection is essential inside Database.Tx: PermissionService.Can opens another
    // connection, while Database attaches its active transaction to commands. Do not turn that
    // cross-connection error into a role-default grant/refusal for a delegated Cash operator.
    private User RequireCash(Microsoft.Data.Sqlite.SqliteConnection connection, User? actor)
    {
        _db.Sessions.Require(actor);
        var current = RequireCashTarget(connection, actor);
        _db.Sessions.Carry(actor, current);
        return current;
    }

    private User RequireCashTarget(Microsoft.Data.Sqlite.SqliteConnection connection, User? actor)
    {
        if (actor is not { Active: true } || actor.Id <= 0)
            throw new ValidationException("Sign in with an active account to work the drawer.");
        var current = _db.QuerySingle<User>(connection,
            "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = actor.Id });
        if (current is not { Active: true } || current.Kind != actor.Kind)
            throw new ValidationException("The account has changed. Sign in again before working the drawer.");
        if (current.IsOwner) return current;
        var grant = _db.QuerySingle<int?>(connection,
            "SELECT allowed FROM user_permissions WHERE user_id = @userId AND module_key = @module;",
            new { userId = current.Id, module = Modules.Cash });
        if (!(grant is int saved ? saved != 0 : PermissionService.RoleDefault(current.Kind, Modules.Cash)))
            throw new ValidationException("You do not have permission to work the drawer.");
        return current;
    }

    private static bool HasDifferenceReason(string? note) =>
        !string.IsNullOrWhiteSpace(note) && note.Trim().Length >= 3;

    /// <summary>
    /// Portion 14: one row in the `shift_discrepancies` contract (migration 030). Called only when
    /// the difference is non-zero and a reason was already demanded, so the CHECKs cannot fail;
    /// `alerted_at` marks the moment the alert board can see it.
    /// </summary>
    private void RecordDiscrepancy(
        Microsoft.Data.Sqlite.SqliteConnection connection, int shiftId, string stage, long expectedPaisa,
        long countedPaisa, long differencePaisa, string reason, string at)
    {
        _db.Execute(connection, @"
INSERT INTO shift_discrepancies(shift_id, stage, expected_paisa, counted_paisa, difference_paisa,
                               reason, alerted_at)
VALUES(@shiftId, @stage, @expected, @counted, @difference, @reason, @at);",
            new
            {
                shiftId,
                stage,
                expected = expectedPaisa,
                counted = countedPaisa,
                difference = differencePaisa,
                reason,
                at
            });
    }

    private ShiftMoney? ReadLastCounted(Microsoft.Data.Sqlite.SqliteConnection connection) =>
        _db.QuerySingle<ShiftMoney>(connection, @"
SELECT s.id AS id, s.user_id AS userId, s.opened_at AS openedAt, IFNULL(u.name, '') AS openedBy,
       s.opening_float_paisa AS openingFloatPaisa, IFNULL(s.expected_paisa, 0) AS expectedPaisa,
       s.actual_paisa AS actualPaisa, s.difference_paisa AS differencePaisa,
       s.status AS status, s.note AS note, s.handed_to_name AS handedToName,
       s.opened_from_shift_id AS openedFromShiftId
FROM shifts s
LEFT JOIN users u ON u.id = s.user_id
WHERE s.status = 'closed' AND s.actual_paisa IS NOT NULL
ORDER BY s.id DESC
LIMIT 1;");

    private ShiftMoney? ReadOpen(Microsoft.Data.Sqlite.SqliteConnection connection) =>
        _db.QuerySingle<ShiftMoney>(connection, @"
SELECT s.id AS id, s.user_id AS userId, s.opened_at AS openedAt, IFNULL(u.name, '') AS openedBy,
       s.opening_float_paisa AS openingFloatPaisa, IFNULL(s.expected_paisa, 0) AS expectedPaisa,
       s.status AS status, s.note AS note, s.handed_to_name AS handedToName,
       s.opened_from_shift_id AS openedFromShiftId
FROM shifts s
LEFT JOIN users u ON u.id = s.user_id
WHERE s.status = 'open'
ORDER BY s.id DESC
LIMIT 1;");

    /// <summary>One shift by its number, open or closed - the handover reads both halves this way.</summary>
    private ShiftMoney? ReadShift(Microsoft.Data.Sqlite.SqliteConnection connection, int id) =>
        _db.QuerySingle<ShiftMoney>(connection, @"
SELECT s.id AS id, s.user_id AS userId, s.opened_at AS openedAt, IFNULL(u.name, '') AS openedBy,
       s.opening_float_paisa AS openingFloatPaisa, IFNULL(s.expected_paisa, 0) AS expectedPaisa,
       s.actual_paisa AS actualPaisa, s.difference_paisa AS differencePaisa,
       s.status AS status, s.note AS note, s.handed_to_name AS handedToName,
       s.opened_from_shift_id AS openedFromShiftId
FROM shifts s
LEFT JOIN users u ON u.id = s.user_id
WHERE s.id = @id;", new { id });

    /// <summary>
    /// Fills the six money lines from the history between two timestamps. Every line is its own
    /// sub-select so one empty table can never hide another line.
    ///
    /// **The boundary is decided by the row, not by the caller.** A shift that was opened by a
    /// handover does not count the second it was opened in: that second already belongs to the
    /// drawer the previous person counted. Every other shift counts its opening second, exactly as
    /// it always has. This is why the rule lives here instead of in a parameter - the same shift is
    /// read again when the page refreshes, when it is closed and when its slip is printed, and all
    /// four readings must agree.
    ///
    /// Why the closing side stays inclusive: a normal close has always counted `[opened, closed]`,
    /// and a handover must report the same number a plain close would for the same rows - otherwise
    /// the same drawer says 800 on one button and 500 on the other, and the owner trusts neither.
    /// So a bill stamped in the same second as the handover belongs to the **closing** shift. It
    /// can only belong to one of them, and giving it to the person whose counted cash was taken
    /// after it is the ordering that really happens: ring the bill, then count, then hand over.
    /// </summary>
    private void FillDrawer(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        ShiftMoney shift,
        string from,
        string to)
    {
        var sql = @"
SELECT
  IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
          WHERE p.method = 'cash' AND p.sale_id IS NOT NULL
            AND p.date >= @from AND p.date <= @to), 0) AS cashSalesPaisa,
  IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
          WHERE p.method = 'cash' AND p.sale_id IS NULL AND p.customer_id IS NOT NULL
            AND p.provisional_return_id IS NULL
            AND p.date >= @from AND p.date <= @to), 0) AS cashCollectionsPaisa,
  IFNULL((SELECT SUM(p.amount_paisa) FROM payments p
          WHERE p.method = 'cash' AND p.supplier_id IS NOT NULL
            AND p.date >= @from AND p.date <= @to), 0) AS supplierCashOutPaisa,
  IFNULL((SELECT SUM(r.cash_refund_paisa) FROM sale_returns r
          WHERE r.date >= @from AND r.date <= @to), 0)
  + IFNULL((SELECT -SUM(p.amount_paisa) FROM payments p
            WHERE p.provisional_return_id IS NOT NULL AND p.amount_paisa < 0
              AND p.date >= @from AND p.date <= @to), 0) AS cashRefundsPaisa,
  IFNULL((SELECT SUM(sr.total_paisa) FROM supplier_returns sr
          WHERE sr.settled = 1 AND sr.date >= @from AND sr.date <= @to), 0) AS supplierCashInPaisa,
  -- F-32: money the shop spent (rent, tea, a repair) and cash the owner took both left the till.
  -- A GOODS drawing is deliberately absent: the medicine left the shelf, not the drawer.
  IFNULL((SELECT SUM(e.amount_paisa) FROM expenses e
          WHERE e.date >= @from AND e.date <= @to), 0) AS expensesPaisa,
  IFNULL((SELECT SUM(d.amount_paisa) FROM owner_drawings d
          WHERE d.type = 'cash' AND d.date >= @from AND d.date <= @to), 0) AS drawingsPaisa;";

        if (shift.OpenedFromShiftId is not null)
        {
            sql = sql.Replace(">= @from", "> @from", StringComparison.Ordinal);
        }

        var drawer = _db.QuerySingle<ShiftMoney>(connection, sql, new { from, to });

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

    /// <summary>
    /// The shop's own totals over the window - one row, read straight from the shift rows.
    /// Named to match the SELECT aliases exactly: a column with no property is dropped silently.
    /// </summary>
    private sealed class ShopTotals
    {
        public int ClosedShifts { get; set; }

        public long ExpectedPaisa { get; set; }

        public long CountedPaisa { get; set; }

        public long MissingPaisa { get; set; }

        public long TooMuchPaisa { get; set; }
    }
}
