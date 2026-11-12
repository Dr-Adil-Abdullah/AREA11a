using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Fixes bills that were stamped with the wrong clock (v0.68.0, owner's word Nov 2026).
///
/// Why this exists: the shop's PC clock drifts - or the internet is down for days, so no time
/// server corrects it - and every bill stamped meanwhile carries the wrong day, sometimes the
/// wrong month. The moment a time server answers, `TimeSource` accepts even a days-large drift
/// (the two-day refusal is gone), so NEW bills are right - but the old ones stay wrong in the
/// books. The owner fixes them here: the wrongly-stamped bills are listed TOGETHER, each with the
/// time it would get, and ONE press moves them all.
///
/// The rules, in the order they matter:
///   * **Only the owner.** Moving a bill across midnight moves the day's حساب - no cashier.
///   * **The reporting time is the corrected one.** The fix writes `sales.date`, which is what
///     every report, khata and recovery reads; `sales.created_at` keeps the wrong stamp for ever,
///     so the paper and the black box can always show both.
///   * **One action, one transaction.** Either every picked bill moves with its money and its black
///     box lines, or nothing moves - a half-fixed range would be worse than none.
///   * **Seconds are left alone.** A shift under a minute never moves a day's حساب, so bills that
///     are seconds out are reported as "already right" and never rewritten.
/// </summary>
public sealed class TimeCorrectionService
{
    /// <summary>
    /// A drift this large (or larger) means bills may carry the wrong time - the Bills page shows
    /// its "fix the time" hint from here. Five minutes: seconds of NTP wobble must not nag.
    /// </summary>
    public static readonly TimeSpan SuspectThreshold = TimeSpan.FromMinutes(5);

    /// <summary>Shifts smaller than this are not written: a second never moves a day's حساب.</summary>
    public static readonly TimeSpan SmallestShift = TimeSpan.FromMinutes(1);

    /// <summary>The list stops here and says so; a shop with more bills in range narrows the dates.</summary>
    public const int MaxRows = 500;

    /// <summary>No dates typed means "the last 30 days" - the range the owner almost always wants.</summary>
    public const int DefaultDays = 30;

    /// <summary>The same shape `Clock.Now()` writes ("2026-11-09 14:05:00").</summary>
    private const string StampFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly LogService _log;

    public TimeCorrectionService(Database db, AuditService audit, LogService log)
    {
        _db = db;
        _audit = audit;
        _log = log;
    }

    /// <summary>Moving a bill across midnight moves the day's books - the owner only.</summary>
    public bool CanFix(User? user) => user is not null && user.IsOwner;

    /// <summary>
    /// The bills in the range that the fix would touch, oldest first - each with the wrong stamp
    /// and the time it would get. Bills that are already right (under a minute out) are not listed:
    /// the fix would skip them anyway, and a list of five hundred "already right" rows hides the
    /// three that matter.
    /// </summary>
    public ServiceResult<List<TimeCorrectionBill>> ListBills(User? user, string? from, string? to) =>
        ListBills(user, from, to, TimeSource.Offset);

    /// <summary>
    /// The same list with the drift passed in. The tests use this door, so they can ask about a
    /// three-day drift without moving every stamp in the program for a moment.
    /// </summary>
    public ServiceResult<List<TimeCorrectionBill>> ListBills(
        User? user, string? from, string? to, TimeSpan currentOffset)
    {
        if (!CanFix(user))
        {
            return ServiceResult<List<TimeCorrectionBill>>.Fail("Only the owner can fix the time on bills.");
        }

        var toDate = ParseDate(to) ?? DateTime.Today;
        var fromDate = ParseDate(from) ?? toDate.AddDays(-DefaultDays);

        if (ParseDate(to) is null && (to ?? string.Empty).Trim().Length > 0)
        {
            return ServiceResult<List<TimeCorrectionBill>>.Fail(
                "The second date is not a date (write it as 2026-11-09). Nothing was listed.");
        }

        if (ParseDate(from) is null && (from ?? string.Empty).Trim().Length > 0)
        {
            return ServiceResult<List<TimeCorrectionBill>>.Fail(
                "The first date is not a date (write it as 2026-11-09). Nothing was listed.");
        }

        if (fromDate > toDate)
        {
            return ServiceResult<List<TimeCorrectionBill>>.Fail(
                "The first date is after the second - swap them. Nothing was listed.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<BillRow>(connection, @"
SELECT s.id AS Id, s.code AS Code, s.date AS Date, s.created_at AS CreatedAt,
       s.time_offset_seconds AS OffsetSeconds, s.total_paisa AS TotalPaisa
FROM sales s
WHERE date(s.date) >= date(@from) AND date(s.date) <= date(@to)
ORDER BY s.date, s.id
LIMIT 501;",
                new
                {
                    from = fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    to = toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                });

            if (rows.Count > MaxRows)
            {
                return ServiceResult<List<TimeCorrectionBill>>.Fail(
                    "More than 500 bills in this range - narrow the dates. Nothing was listed.");
            }

            var bills = new List<TimeCorrectionBill>();

            foreach (var row in rows)
            {
                if (!TryReadStamp(row.Date, out var wrong))
                {
                    // An unreadable stamp cannot be proposed a correction; the fix names it loudly
                    // instead of guessing, so it is left out of the list, never silently dropped.
                    continue;
                }

                var shift = ShiftFor(row.OffsetSeconds, currentOffset);

                if (shift.Duration() < SmallestShift)
                {
                    continue;
                }

                var proposed = wrong + shift;

                bills.Add(new TimeCorrectionBill
                {
                    Id = row.Id,
                    Code = row.Code,
                    WrongDate = row.Date,
                    ProposedDate = proposed.ToString(StampFormat, CultureInfo.InvariantCulture),
                    ShiftLabel = TimeSource.Describe(shift),
                    TotalPaisa = row.TotalPaisa,
                    TotalLabel = Money.Format(row.TotalPaisa),
                    KnowsOffset = row.OffsetSeconds is not null
                });
            }

            return ServiceResult<List<TimeCorrectionBill>>.Success(bills);
        }
        catch (Exception exception)
        {
            _log.Error("The wrongly-stamped bills could not be listed.", exception);

            return ServiceResult<List<TimeCorrectionBill>>.Fail(
                "The bills could not be listed. " + exception.Message);
        }
    }

    /// <summary>
    /// Moves every picked bill to its corrected time: `sales.date` (the reporting time) and the
    /// money rows that belong to it, with both times in the black box. One transaction - either
    /// every bill moves or none does.
    /// </summary>
    public ServiceResult<TimeCorrectionResult> ApplyCorrection(User? user, IReadOnlyList<int>? ids) =>
        ApplyCorrection(user, ids, TimeSource.Offset);

    /// <summary>
    /// The same fix with the drift passed in. The tests use this door, so they can move bills by
    /// days without moving every stamp in the program for a moment.
    /// </summary>
    public ServiceResult<TimeCorrectionResult> ApplyCorrection(
        User? user, IReadOnlyList<int>? ids, TimeSpan currentOffset)
    {
        if (!CanFix(user))
        {
            return ServiceResult<TimeCorrectionResult>.Fail("Only the owner can fix the time on bills.");
        }

        var clean = (ids ?? Array.Empty<int>()).Where(id => id > 0).Distinct().OrderBy(id => id).ToList();

        if (clean.Count == 0)
        {
            return ServiceResult<TimeCorrectionResult>.Fail("Pick at least one bill first. Nothing was changed.");
        }

        try
        {
            var rows = new List<BillRow>();

            // The reading connection is closed before the writing transaction starts: two open
            // connections around one write is how a shop PC earns a "database is locked" box.
            using (var connection = _db.Open())
            {
                foreach (var id in clean)
                {
                    var row = _db.QuerySingle<BillRow>(connection, @"
SELECT s.id AS Id, s.code AS Code, s.date AS Date, s.created_at AS CreatedAt,
       s.time_offset_seconds AS OffsetSeconds, s.total_paisa AS TotalPaisa
FROM sales s
WHERE s.id = @id;",
                        new { id });

                    if (row is null)
                    {
                        return ServiceResult<TimeCorrectionResult>.Fail(
                            "Bill " + id + " is not in this shop's database. Nothing was changed.");
                    }

                    rows.Add(row);
                }
            }
            // Every stamp is read BEFORE anything is written: one unreadable bill refuses the whole
            // batch by name, so the range is never half-fixed.
            var planned = new List<(BillRow Row, DateTime Wrong, DateTime Correct, TimeSpan Shift)>();
            var skipped = 0;

            foreach (var row in rows)
            {
                if (!TryReadStamp(row.Date, out var wrong))
                {
                    return ServiceResult<TimeCorrectionResult>.Fail(
                        "Bill " + row.Code + " has a date the shop cannot read (" +
                        (row.Date ?? "empty") + "). Nothing was changed - narrow the range to skip it.");
                }

                var shift = ShiftFor(row.OffsetSeconds, currentOffset);

                if (shift.Duration() < SmallestShift)
                {
                    skipped++;
                    continue;
                }

                planned.Add((row, wrong, wrong + shift, shift));
            }

            if (planned.Count == 0)
            {
                return ServiceResult<TimeCorrectionResult>.Success(new TimeCorrectionResult
                {
                    Fixed = 0,
                    Skipped = skipped
                });
            }

            var currentSeconds = Calc.Round(currentOffset.TotalSeconds);
            var driftWords = TimeSource.Describe(currentOffset);

            _db.Tx(work =>
            {
                foreach (var (row, _, correct, shift) in planned)
                {
                    var correctText = correct.ToString(StampFormat, CultureInfo.InvariantCulture);

                    _db.Execute(work, @"
UPDATE sales SET date = @date, time_offset_seconds = @offset WHERE id = @id;",
                        new { date = correctText, offset = currentSeconds, id = row.Id });

                    // The money moves with its bill: a payment left on the wrong day would put the
                    // day's collection and the day's sales in different days.
                    var payments = _db.Query<PaymentRow>(work, @"
SELECT id AS Id, date AS Date FROM payments WHERE sale_id = @id ORDER BY id;",
                        new { id = row.Id });

                    foreach (var payment in payments)
                    {
                        if (!TryReadStamp(payment.Date, out var paidAt))
                        {
                            throw new ValidationException(
                                "Bill " + row.Code + " has a payment with a date the shop cannot read (" +
                                (payment.Date ?? "empty") + "). Nothing was changed.");
                        }

                        _db.Execute(work, "UPDATE payments SET date = @date WHERE id = @id;",
                            new
                            {
                                date = (paidAt + shift).ToString(StampFormat, CultureInfo.InvariantCulture),
                                id = payment.Id
                            });
                    }

                    // Both times, for ever: the wrong stamp is the old value, the corrected reporting
                    // time the new one, and the shift in words so the owner need not subtract.
                    _audit.Record(work, user, Modules.Sales, "sale_time_fixed", "sales",
                        row.Id.ToString(CultureInfo.InvariantCulture),
                        oldValue: row.Date,
                        newValue: correctText,
                        details: "Moved " + TimeSource.Describe(shift) + " (true time is " +
                                 driftWords + " from this computer's clock).");
                }
            });

            return ServiceResult<TimeCorrectionResult>.Success(new TimeCorrectionResult
            {
                Fixed = planned.Count,
                Skipped = skipped
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult<TimeCorrectionResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The bill times could not be fixed.", exception);

            return ServiceResult<TimeCorrectionResult>.Fail(
                "The bill times could not be fixed. " + exception.Message);
        }
    }

    /// <summary>
    /// How far one bill must move. A bill that knows its own offset moves by the difference; an older
    /// bill (v0.68.0 knows nothing about its clock) is assumed stamped with the computer clock.
    /// </summary>
    private static TimeSpan ShiftFor(long? offsetSeconds, TimeSpan currentOffset)
    {
        if (offsetSeconds is null)
        {
            return currentOffset;
        }

        return currentOffset - TimeSpan.FromSeconds(offsetSeconds.Value);
    }

    private static bool TryReadStamp(string? text, out DateTime stamp) =>
        DateTime.TryParseExact((text ?? string.Empty).Trim(), StampFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out stamp);

    private static DateTime? ParseDate(string? text)
    {
        var clean = (text ?? string.Empty).Trim();

        if (clean.Length == 0)
        {
            return null;
        }

        return DateTime.TryParseExact(clean, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private sealed class BillRow
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string? Date { get; set; }

        public string? CreatedAt { get; set; }

        public long? OffsetSeconds { get; set; }

        public long TotalPaisa { get; set; }
    }

    private sealed class PaymentRow
    {
        public int Id { get; set; }

        public string? Date { get; set; }
    }
}
