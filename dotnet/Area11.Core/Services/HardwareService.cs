using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The shop's hardware test (F-46, Spec 33.3.4): the thermal printer on 58 mm and 80 mm paper, and
/// the USB barcode scanner that behaves like a keyboard.
///
/// Why this page exists at all: the program cannot see the paper. A wrong width setting loses the
/// last characters of every bill, a printer without a cutter turns a busy counter into a queue, and
/// a scanner that needs a click first silently adds a step to every sale. The only person who can
/// confirm these things is the one standing at the counter - so the program hands him a printed
/// slip, a box to scan into, and a list of statements only he can tick. The ticks are kept, with the
/// date, the person and the paper width they belong to (Spec 12.1.2 - the black box remembers).
///
/// Nothing here prints by itself. <see cref="BuildSlip"/> builds a document and
/// <see cref="PrinterService"/> sends the bytes; a printer that is off never blocks this page (Z-07).
/// </summary>
public sealed class HardwareService
{
    // The saved answer, as five plain settings. Why not one JSON blob: a shop that upgrades and a
    // shop that starts fresh must read the same five words, and a value a person can read in the
    // settings table is a value that can be explained on the phone (D-25).
    public const string SettingAt = "hardware.testedAt";
    public const string SettingBy = "hardware.testedBy";
    public const string SettingNote = "hardware.note";
    public const string SettingWidth = "hardware.testedWidth";
    public const string SettingPassed = "hardware.passed";

    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public HardwareService(
        SettingsService settings,
        AuditService audit,
        PermissionService permissions,
        LogService log)
    {
        _settings = settings;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>The page, the slip and the record are for the person who may change the shop's settings.</summary>
    public bool CanTest(User? user) => _permissions.Can(user, Modules.Settings);

    /// <summary>58 or 80 - the two roll widths the owner's printers use (Q-10, D-27).</summary>
    public int WidthMm => _settings.GetInt("printer.width", 58) >= 80 ? 80 : 58;

    public int Columns => ReceiptService.ColumnsForMillimetres(WidthMm);

    /// <summary>Everything the page needs: the six statements, the printer facts and the saved answer.</summary>
    public ServiceResult<HardwareReport> Load(User? user)
    {
        if (!_permissions.Can(user, Modules.Settings))
        {
            return ServiceResult<HardwareReport>.Fail(
                "Only a person who may change the settings can test the printer.");
        }

        var report = Build();
        var at = _settings.GetString(SettingAt).Trim();

        if (at.Length > 0)
        {
            var passed = _settings.GetString(SettingPassed)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            report.CheckedAt = at;
            report.CheckedBy = _settings.GetString(SettingBy).Trim();
            report.CheckedWidthMm = _settings.GetInt(SettingWidth, 0);
            report.Note = _settings.GetString(SettingNote);

            foreach (var check in report.Checks)
            {
                check.Passed = passed.Contains(check.Key);
            }
        }

        return ServiceResult<HardwareReport>.Success(report);
    }

    /// <summary>
    /// The slip the owner holds in his hand: the shop's name, the paper width, a ruler that fills the
    /// line exactly, a sample bill line and one sentence that says what to do when a digit is
    /// missing. Plain Latin text on purpose - the cheap printers corrupt anything else (Spec 14.5.3).
    /// </summary>
    public ServiceResult<ReceiptDocument> BuildSlip(User? user)
    {
        if (!_permissions.Can(user, Modules.Settings))
        {
            return ServiceResult<ReceiptDocument>.Fail(
                "Only a person who may change the settings can print a test slip.");
        }

        var report = Build();
        var width = report.Columns;

        var document = new ReceiptDocument
        {
            Title = "Area11 test slip",
            Width = width,
            AutoCut = report.AutoCut,
            Copies = report.Copies
        };

        void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) =>
            document.Lines.Add(new ReceiptLine(Fit(text, width), align, bold, big));

        var shop = FirstNonEmpty(_settings.GetString("store.name"), "Area11");

        Add(shop, ReceiptAlign.Center, bold: true, big: true);
        Add("TEST SLIP - NOT A BILL", ReceiptAlign.Center, bold: true);
        document.Lines.Add(ReceiptLine.Sep(width));

        Add("Printer: " + report.PrinterLabel);
        Add("Paper: " + report.WidthMm.ToString(CultureInfo.InvariantCulture) + " mm / "
            + width.ToString(CultureInfo.InvariantCulture) + " characters");
        Add("Copies: " + report.Copies.ToString(CultureInfo.InvariantCulture)
            + (report.AutoCut ? ", cut on" : ", no cut"));
        Add("Printed: " + Clock.Now());
        document.Lines.Add(ReceiptLine.Sep(width));

        // The ruler is the width test: it fills the line exactly, so a lost last digit is visible
        // without measuring anything.
        Add(Ruler(width));
        Add("Every character above must be");
        Add("on the paper.");
        document.Lines.Add(ReceiptLine.Sep(width));

        Add("Panadol 500mg x 2");
        Add("2 x Rs 150.00 = Rs 300.00", bold: true);
        Add("TOTAL Rs 300.00", ReceiptAlign.Right, bold: true);
        document.Lines.Add(ReceiptLine.Sep(width));

        Add("If a digit is missing, set the");
        Add("paper width in Settings.");
        Add(width.ToString(CultureInfo.InvariantCulture) + " mm prints "
            + width.ToString(CultureInfo.InvariantCulture) + " characters.");
        Add("Keep this slip: this PC was tested.");

        return ServiceResult<ReceiptDocument>.Success(document);
    }

    /// <summary>
    /// Records the owner's answer. A missing tick is allowed - a printer that does not work is a fact,
    /// not an error - but it needs one line of explanation, because the next person to read the record
    /// must know what to fix.
    /// </summary>
    public ServiceResult<HardwareReport> Save(User? user, IEnumerable<string>? passedKeys, string? note)
    {
        if (!_permissions.Can(user, Modules.Settings))
        {
            return ServiceResult<HardwareReport>.Fail(
                "Only a person who may change the settings can record the test.");
        }

        var keys = passedKeys?.ToList() ?? new List<string>();
        var report = Build();

        var unknown = keys.Where(key => report.Checks.All(check => check.Key != key)).ToList();

        if (unknown.Count > 0)
        {
            return ServiceResult<HardwareReport>.Fail(
                "That tick-box is not part of the test: " + string.Join(", ", unknown));
        }

        foreach (var check in report.Checks)
        {
            check.Passed = keys.Contains(check.Key);
        }

        var text = (note ?? string.Empty).Trim();

        if (!report.IsComplete && text.Length == 0)
        {
            return ServiceResult<HardwareReport>.Fail(
                "Write one line about what did not pass - a missing tick needs a reason.");
        }

        report.CheckedAt = Clock.Now();
        report.CheckedBy = string.IsNullOrWhiteSpace(user?.Name) ? "someone" : user!.Name.Trim();
        report.CheckedWidthMm = report.WidthMm;
        report.Note = text.Length == 0 ? null : text;

        var ordered = keys.Distinct().OrderBy(key => key, StringComparer.Ordinal).ToList();

        try
        {
            // The quiet door: these are tick-boxes, not shop settings, and five of them must not write
            // five lines into the black box. The one line that matters is recorded below, in words.
            //
            // The date is written LAST on purpose: the page treats a non-empty date as "this PC was
            // tested", so a write that fails halfway leaves "never checked" instead of a record that
            // claims six ticks it does not have.
            var written = _settings.SetUiOnly(SettingPassed, string.Join(",", ordered)).Ok
                          && _settings.SetUiOnly(SettingNote, report.Note ?? string.Empty).Ok
                          && _settings.SetUiOnly(SettingWidth, report.WidthMm).Ok
                          && _settings.SetUiOnly(SettingBy, report.CheckedBy).Ok
                          && _settings.SetUiOnly(SettingAt, report.CheckedAt).Ok;

            if (!written)
            {
                return ServiceResult<HardwareReport>.Fail(
                    "The test result could not be saved. Try again, or check the disk is not full.");
            }

            _audit.RecordStandalone(user, Modules.Settings, "hardware_test", details: report.SummaryText);

            return ServiceResult<HardwareReport>.Success(report);
        }
        catch (Exception exception)
        {
            _log.Error("The hardware test result could not be saved.", exception);
            return ServiceResult<HardwareReport>.Fail(
                "The test result could not be saved. " + exception.Message);
        }
    }

    // ============================== the rules of the page ==============================

    /// <summary>
    /// The six statements. They are asked in the order the owner works: the slip, the paper, the
    /// cutter, the scanner, the medicine, and finally a real bill.
    /// </summary>
    private static List<HardwareCheck> ChecksFor(HardwareReport report)
    {
        var paper = report.WidthMm.ToString(CultureInfo.InvariantCulture) + " mm";

        return new List<HardwareCheck>
        {
            new("printed", "A test slip printed on " + paper + " paper.",
                "Press Print test slip - it goes to the printer chosen in Settings (empty means the Windows default)."),
            new("fits", "Every character of the ruler is on the paper - nothing is cut off.",
                "A missing digit means the paper width in Settings is wrong for this roll."),
            new("cut", "The paper was cut (or torn) cleanly after the slip.",
                "A printer without a cutter is fine - it only needs the tear bar, not a repair."),
            new("scanner", "One barcode scan typed its code into the box without a click first.",
                "A USB scanner behaves like a keyboard; if it needs a click, its manual is open on the wrong setting."),
            new("found", "The scanned code found the right medicine.",
                "Scan a box you know, or type its code by hand - the answer must name the medicine and the pack."),
            new("bill", "One real bill printed from the counter and looked right.",
                "The test slip is not a bill: the totals, the khata line and the footer have to fit too.")
        };
    }

    private HardwareReport Build()
    {
        var widthMm = WidthMm;

        var report = new HardwareReport
        {
            WidthMm = widthMm,
            Columns = ReceiptService.ColumnsForMillimetres(widthMm),
            PrinterName = _settings.GetString("printer.name"),
            Copies = Math.Clamp(_settings.GetInt("printer.copies", 1), 1, 3),
            AutoCut = _settings.GetBool("printer.autoCut", true)
        };

        report.Checks.AddRange(ChecksFor(report));

        return report;
    }

    /// <summary>"1234567890" repeated to the last column - the width the paper must hold.</summary>
    private static string Ruler(int columns)
    {
        var digits = new char[columns];

        for (var index = 0; index < columns; index++)
        {
            digits[index] = (char)('0' + ((index + 1) % 10));
        }

        return new string(digits);
    }

    /// <summary>One line, never wider than the paper (the same rule the receipt follows).</summary>
    private static string Fit(string text, int width)
    {
        var clean = (text ?? string.Empty).Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

        return clean.Length <= width ? clean : clean[..width];
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
