using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The hardware test (F-46, Spec 33.3.4): the printer's own slip, the six statements the owner ticks,
/// and the record that keeps his answer.
///
/// Why the slip is tested so carefully: it is the only proof the program can offer about a machine it
/// cannot see. If the ruler is one character too long for 32-column paper, every bill loses its last
/// digit and the test would still pass - so the width rule itself is pinned here.
/// </summary>
public sealed class HardwareTests
{
    // ============================== the slip ==============================

    [Fact]
    public void TheSlipFillsA58MmLineExactly()
    {
        using var test = new TestDatabase();
        var slip = Hardware(test).BuildSlip(TestData.Owner);

        Assert.True(slip.Ok, slip.Error);

        var document = slip.Value!;

        Assert.Equal(32, document.Width);
        Assert.All(document.Lines, line => Assert.True(line.Text.Length <= 32,
            $"'{line.Text}' is {line.Text.Length} characters - it would be cut off"));
        Assert.Contains(document.Lines, line => line.Text == "12345678901234567890123456789012");
    }

    [Fact]
    public void TheSlipFillsAn80MmLineExactly()
    {
        using var test = new TestDatabase();
        Assert.True(test.Settings.Set("printer.width", 80, TestData.Owner).Ok);
        test.Settings.Reload();

        var document = Hardware(test).BuildSlip(TestData.Owner).Value!;

        Assert.Equal(48, document.Width);
        Assert.All(document.Lines, line => Assert.True(line.Text.Length <= 48));
        Assert.Contains(document.Lines, line => line.Text == "123456789012345678901234567890123456789012345678");
        Assert.Contains(document.Lines, line => line.Text.Contains("48 characters"));
    }

    [Fact]
    public void TheSlipSaysItIsNotABill()
    {
        using var test = new TestDatabase();
        var document = Hardware(test).BuildSlip(TestData.Owner).Value!;

        var text = document.PlainText;

        Assert.Contains("TEST SLIP - NOT A BILL", text);
        Assert.Contains("Area11 Pharmacy", text);     // the shop's own name, from Settings
        Assert.Contains("Keep this slip", text);
    }

    [Fact]
    public void TheSlipCarriesThePrinterAndThePaper()
    {
        using var test = new TestDatabase();
        var document = Hardware(test).BuildSlip(TestData.Owner).Value!;

        // The printer line names what Windows resolved, so it differs from PC to PC: on a machine
        // with a real default printer it reads "Windows default - EPSON", on one without it says
        // "Windows default printer". Asserting the exact sentence made the test fail on the shop PC
        // the moment a printer was installed (v0.28.0, the owner's run) - the slip was right.
        var printer = Assert.Single(document.Lines, line => line.Text.StartsWith("Printer: "));

        Assert.StartsWith("Printer: ", printer.Text);
        Assert.Contains("Windows default", printer.Text);

        Assert.Contains(document.Lines, line => line.Text == "Paper: 58 mm / 32 characters");
    }

    [Fact]
    public void TheSlipStartsWithAResetAndEndsWithACut()
    {
        using var test = new TestDatabase();
        var document = Hardware(test).BuildSlip(TestData.Owner).Value!;
        var bytes = EscPosBuilder.Build(document);

        Assert.Equal(EscPosBuilder.Esc, bytes[0]);
        Assert.Equal((byte)'@', bytes[1]);
        Assert.True(bytes.TakeLast(EscPosBuilder.Cut.Length).SequenceEqual(EscPosBuilder.Cut),
            "the slip must end with the cut command when auto-cut is on");
    }

    [Fact]
    public void WithoutTheCutterThePaperIsLeftWhole()
    {
        using var test = new TestDatabase();
        Assert.True(test.Settings.Set("printer.autoCut", false, TestData.Owner).Ok);
        test.Settings.Reload();

        var document = Hardware(test).BuildSlip(TestData.Owner).Value!;
        var bytes = EscPosBuilder.Build(document);

        Assert.False(document.AutoCut);
        Assert.False(bytes.TakeLast(EscPosBuilder.Cut.Length).SequenceEqual(EscPosBuilder.Cut));
        Assert.Contains(document.Lines, line => line.Text.Contains("no cut"));
    }

    // ============================== the six boxes ==============================

    [Fact]
    public void AFreshShopHasSixUntickedBoxes()
    {
        using var test = new TestDatabase();
        var report = Hardware(test).Load(TestData.Owner).Value!;

        Assert.Equal(6, report.CheckCount);
        Assert.Equal(0, report.PassedCount);
        Assert.False(report.HasRecord);
        Assert.False(report.IsComplete);
        Assert.Equal("0 of 6 passed", report.ProgressLabel);
        Assert.Contains("never checked", report.LastRunLabel);
        Assert.Equal(new[] { "printed", "fits", "cut", "scanner", "found", "bill" },
            report.Checks.Select(check => check.Key).ToArray());
    }

    [Fact]
    public void AFullPassIsRecordedInOneBlackBoxLine()
    {
        using var test = new TestDatabase();
        var hardware = Hardware(test);
        var keys = hardware.Load(TestData.Owner).Value!.Checks.Select(check => check.Key).ToList();

        var saved = hardware.Save(TestData.Owner, keys, "Everything printed on the first try.");

        Assert.True(saved.Ok, saved.Error);
        Assert.True(saved.Value!.IsComplete);
        Assert.Equal("Owner", saved.Value.CheckedBy);
        Assert.Equal(6, saved.Value.PassedCount);
        Assert.Equal(1L, Audits(test));

        var reloaded = hardware.Load(TestData.Owner).Value!;

        Assert.True(reloaded.HasRecord);
        Assert.True(reloaded.IsComplete);
        Assert.True(reloaded.RecordMatchesWidth);
        Assert.Contains("All done", reloaded.SummaryText);
    }

    [Fact]
    public void APartialPassNeedsAReason()
    {
        using var test = new TestDatabase();
        var hardware = Hardware(test);
        var keys = hardware.Load(TestData.Owner).Value!.Checks.Select(check => check.Key).Take(5).ToList();

        var refused = hardware.Save(TestData.Owner, keys, "   ");

        Assert.False(refused.Ok);
        Assert.Contains("reason", refused.Error ?? string.Empty);
        Assert.Equal(0L, Audits(test));
    }

    [Fact]
    public void APartialPassWithANoteIsKeptAndSaysWhatIsMissing()
    {
        using var test = new TestDatabase();
        var hardware = Hardware(test);
        var report = hardware.Load(TestData.Owner).Value!;
        var keys = report.Checks.Select(check => check.Key).Take(5).ToList();
        var missing = report.Checks[5].Text;

        var saved = hardware.Save(TestData.Owner, keys, "The bill printed, but the cutter jammed.");

        Assert.True(saved.Ok, saved.Error);
        Assert.False(saved.Value!.IsComplete);
        Assert.Contains(missing, saved.Value.MissingLabel);

        var reloaded = hardware.Load(TestData.Owner).Value!;

        Assert.Equal(5, reloaded.PassedCount);
        Assert.Contains("the cutter jammed", reloaded.Note ?? string.Empty);
        Assert.Contains("Still missing", reloaded.SummaryText);
        Assert.Equal(1L, Audits(test));
    }

    [Fact]
    public void ATickThatIsNotPartOfTheTestIsRefused()
    {
        using var test = new TestDatabase();
        var refused = Hardware(test).Save(TestData.Owner, new[] { "printed", "invented" }, "test");

        Assert.False(refused.Ok);
        Assert.Contains("invented", refused.Error ?? string.Empty);
    }

    [Fact]
    public void ACashierCannotRecordTheTest()
    {
        using var test = new TestDatabase();
        var cashier = TestData.Cashier(test);
        var hardware = Hardware(test);

        Assert.False(hardware.CanTest(cashier));
        Assert.False(hardware.Load(cashier).Ok);
        Assert.False(hardware.BuildSlip(cashier).Ok);
        Assert.False(hardware.Save(cashier, new[] { "printed" }, "let me").Ok);
        Assert.Equal(0L, Audits(test));
    }

    [Fact]
    public void TheSavedTicksSurviveANewServiceInstance()
    {
        using var test = new TestDatabase();
        var first = Hardware(test);
        var keys = first.Load(TestData.Owner).Value!.Checks.Select(check => check.Key).ToList();
        Assert.True(first.Save(TestData.Owner, keys, string.Empty).Ok);

        // A new service is what the next start of Area11 builds: the answer must come from the
        // database, not from a field in memory.
        var second = Hardware(test);
        var reloaded = second.Load(TestData.Owner).Value!;

        Assert.True(reloaded.IsComplete);
        Assert.Equal(keys.Count, reloaded.PassedCount);
    }

    [Fact]
    public void ChangingThePaperMakesTheOldRecordStale()
    {
        using var test = new TestDatabase();
        var hardware = Hardware(test);
        var keys = hardware.Load(TestData.Owner).Value!.Checks.Select(check => check.Key).ToList();
        Assert.True(hardware.Save(TestData.Owner, keys, string.Empty).Ok);

        Assert.True(test.Settings.Set("printer.width", 80, TestData.Owner).Ok);
        test.Settings.Reload();

        var reloaded = hardware.Load(TestData.Owner).Value!;

        Assert.True(reloaded.HasRecord);
        Assert.False(reloaded.RecordMatchesWidth);
        Assert.Equal(58, reloaded.CheckedWidthMm);
        Assert.Equal(80, reloaded.WidthMm);
    }

    // ============================== the printer that would not print ==============================

    [Fact]
    public void CodeSixSaysThatNoPrinterWasChosenAndWhereToChooseOne()
    {
        // The owner's first printer error was "refused by Windows (code 6)". The cause was in Area11:
        // OpenPrinter(NULL) opens the print SERVER, and the job then fails on that handle.
        var sentence = PrinterService.Explain(6);

        Assert.Contains("code 6", sentence);
        Assert.Contains("Printer & scanner", sentence);
        Assert.Contains("default printer", sentence);
    }

    [Fact]
    public void APrinterNameThatDoesNotExistIsNamedInTheAnswer()
    {
        Assert.Contains("1801", PrinterService.Explain(1801));
        Assert.Contains("name", PrinterService.Explain(1801));
    }

    [Fact]
    public void AnUnknownPrinterCodeIsStillReportedWithItsNumber()
    {
        Assert.Contains("4242", PrinterService.Explain(4242));
    }

    [Fact]
    public void ThePrinterListIsAlwaysAListAndNeverNull()
    {
        // On a PC without a driver this is empty; the page then says the driver is missing. The point
        // of the test is that asking Windows never throws and never returns null.
        Assert.NotNull(PrinterService.InstalledPrinters());
        Assert.NotNull(PrinterService.DefaultPrinterName());
    }

    [Fact]
    public void OnlyTheSettingsPersonChoosesThePrinter()
    {
        using var test = new TestDatabase();

        var refused = Hardware(test).ChoosePrinter(TestData.Cashier(test), string.Empty);

        Assert.False(refused.Ok);
        Assert.Contains("settings", refused.Error!);
    }

    [Fact]
    public void ChoosingTheWindowsDefaultStoresAnEmptyNameAndSaysWhatWindowsWillUse()
    {
        using var test = new TestDatabase();

        var chosen = Hardware(test).ChoosePrinter(TestData.Owner, "   ");

        Assert.True(chosen.Ok, chosen.Error);
        Assert.Equal(string.Empty, test.Settings.GetString("printer.name"));
        Assert.False(string.IsNullOrWhiteSpace(chosen.Value));
    }

    // ============================== helpers ==============================

    /// <summary>One factory, so a constructor that gains a dependency is fixed here and not in every test.</summary>
    private static HardwareService Hardware(TestDatabase test) =>
        new(test.Settings, test.Audit, new PermissionService(test.Database, test.Log, test.Audit), test.Log);

    private static long Audits(TestDatabase test)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = 'hardware_test';") ?? 0L);
    }
}
