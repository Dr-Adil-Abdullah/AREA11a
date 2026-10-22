using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using System.IO;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The self-installing installer (F-47, D-19).
///
/// The promises this file pins down, in the order the owner meets them:
///   1. the **program** goes to `C:\Program Files\Area11` and the **shop data** stays in
///      `C:\ProgramData\Area11` - two folders, never one;
///   2. the desktop icon and the Start-menu entry are created for **everyone** who signs in, and the
///      uninstaller sits with the program, not with the data;
///   3. nothing has to be installed besides Area11: the .NET runtime travels inside it and there is
///      no Python anywhere;
///   4. only three things can stop the install - an old Windows, too little disk, and no program to
///      copy; a missing printer never stops a shop from taking a bill (Z-07);
///   5. updating means running it again: the program files are replaced, the database is not;
///   6. the text the owner reads and the exit code the script reads come from this same code.
/// </summary>
public sealed class InstallTests
{
    // ============================== the two folders ==============================

    [Fact]
    public void TheProgramGoesToProgramFilesAndTheDataStaysInProgramData()
    {
        var plan = InstallService.Plan(ReadyPc());

        Assert.Equal(@"C:\Program Files\Area11", plan.InstallFolder);
        Assert.Equal(@"C:\Program Files\Area11\Area11.exe", plan.ExePath);
        Assert.Equal(@"C:\ProgramData\Area11", plan.DataFolder);
        Assert.Equal(@"C:\ProgramData\Area11\area11.db", plan.DatabaseFile);

        // The whole point of two folders: an update replaces one and must never touch the other.
        Assert.NotEqual(plan.InstallFolder, plan.DataFolder);
    }

    [Fact]
    public void TheUninstallerTravelsWithTheProgramNotWithTheShopData()
    {
        var plan = InstallService.Plan(ReadyPc());

        Assert.StartsWith(plan.InstallFolder, plan.UninstallerPath);
        Assert.False(plan.UninstallerPath.StartsWith(plan.DataFolder, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryUserOnTheShopPcMayWriteTheShopFolder()
    {
        var plan = InstallService.Plan(ReadyPc());

        // `Users` by SID - a non-English Windows translates the name, never the SID.
        Assert.Contains("S-1-5-32-545", plan.DataFolderGrant);
        Assert.Contains("(OI)(CI)M", plan.DataFolderGrant);
    }

    [Fact]
    public void TheShortcutsArePublicSoEverybodyWhoSignsInSeesThem()
    {
        var facts = ReadyPc();
        var plan = InstallService.Plan(facts);

        // Two shortcuts are both called "Area11" - the one on the public desktop and the one in the
        // Start menu - so the folder is what tells them apart. (v0.22.1: this test asked for Single on
        // the name alone and failed the moment the Start menu shortcut joined it in v0.19.0.)
        var desktop = plan.Shortcuts.Single(shortcut =>
            shortcut.Name == InstallService.DesktopShortcutName && shortcut.Folder == facts.DesktopFolder);
        var start = plan.Shortcuts.Single(shortcut =>
            shortcut.Name == InstallService.StartMenuShortcutName && shortcut.Folder == facts.StartMenuFolder);
        var uninstall = plan.Shortcuts.Single(shortcut => shortcut.Name == InstallService.UninstallShortcutName);

        Assert.Equal(@"C:\Users\Public\Desktop", desktop.Folder);
        Assert.Equal(plan.ExePath, desktop.Target);
        Assert.EndsWith("Area11.lnk", desktop.ShortcutPath);
        Assert.StartsWith(facts.DesktopFolder, desktop.ShortcutPath);

        Assert.Equal(@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs", start.Folder);
        Assert.Equal(plan.ExePath, start.Target);

        Assert.Equal(plan.UninstallerPath, uninstall.Target);
        Assert.Contains(plan.DataFolder, uninstall.Description);
    }

    [Fact]
    public void AddRemoveProgramsShowsTheSameVersionAsTheSignInScreen()
    {
        var plan = InstallService.Plan(ReadyPc());

        Assert.Equal(BuildInfo.Version, plan.DisplayVersion);
        Assert.Contains("Uninstall", plan.UninstallKey);
        Assert.EndsWith("Area11", plan.UninstallKey);
        Assert.Equal("Area11 - pharmacy POS", plan.DisplayName);
    }

    // ============================== what can stop it ==============================

    [Fact]
    public void AReadyPcHasNoStopAndNoNumberOfThingsToDo()
    {
        var report = InstallService.Inspect(ReadyPc());

        Assert.True(report.CanInstall);
        Assert.Equal(0, report.StopCount);
        Assert.Equal(0, report.FixCount);
        Assert.Equal(report.Checks.Count, report.ReadyCount);
        Assert.Contains("READY TO INSTALL", report.ToText());
    }

    [Fact]
    public void AnOldWindowsStopsAndNamesTheBuildItFound()
    {
        var facts = ReadyPc();
        facts.WindowsName = "Microsoft Windows 7";
        facts.BuildNumber = 7601;

        var report = InstallService.Inspect(facts);
        var check = Check(report, "windows");

        Assert.False(report.CanInstall);
        Assert.Equal(InstallState.Stop, check.State);
        Assert.Contains("17763", check.Detail);
        Assert.Contains("7601", check.Detail);
        Assert.Contains("INSTALL-AREA11.cmd", check.Action);
        Assert.Contains("STOPPED", report.ToText());
    }

    [Fact]
    public void TooLittleDiskStopsBeforeHalfACopyIsMade()
    {
        var facts = ReadyPc();
        facts.FreeDiskMb = 120;

        var check = Check(InstallService.Inspect(facts), "disk");

        Assert.Equal(InstallState.Stop, check.State);
        Assert.Contains("120", check.Detail);
        Assert.Contains("600", check.Detail);
    }

    [Fact]
    public void WithoutSdkAndWithoutAFinishedCopyItSaysWhatToRunOnTheBuildPc()
    {
        var facts = ReadyPc();
        facts.HasPublishedBundle = false;
        facts.HasDotnetSdk = false;

        var check = Check(InstallService.Inspect(facts), "source");

        Assert.Equal(InstallState.Stop, check.State);
        Assert.Contains("MAKE-INSTALLER.cmd", check.Action);
        Assert.Contains("dist", check.Detail);
    }

    [Fact]
    public void AFinishedCopyIsEnoughAndNoSdkIsNeeded()
    {
        var facts = ReadyPc();
        facts.HasDotnetSdk = false;

        var report = InstallService.Inspect(facts);

        Assert.Equal(InstallState.Ready, Check(report, "source").State);
        Assert.Contains(facts.SourceFolder, Check(report, "source").Detail);
        Assert.False(report.Plan.NeedsPublish);
    }

    [Fact]
    public void WithTheSdkTheInstallerBuildsTheProgramItself()
    {
        var facts = ReadyPc();
        facts.HasPublishedBundle = false;
        facts.HasDotnetSdk = true;

        var report = InstallService.Inspect(facts);

        Assert.Equal(InstallState.Fixes, Check(report, "source").State);
        Assert.True(report.Plan.NeedsPublish);
        Assert.True(report.CanInstall);
    }

    // ============================== never a stop for hardware ==============================

    [Fact]
    public void NoPrinterIsRepairedWithPrintToPdfAndNeverStopsTheShop()
    {
        var facts = ReadyPc();
        facts.Printers = new List<string>();

        var report = InstallService.Inspect(facts);

        Assert.Equal(InstallState.Fixes, Check(report, "printers").State);
        Assert.Contains(InstallService.PdfPrinterName, Check(report, "printers").Action);
        Assert.True(report.CanInstall, "a shop without a printer must still be able to take a bill (Z-07)");
    }

    [Fact]
    public void APrinterThatIsThereIsNamedOnTheReport()
    {
        var facts = ReadyPc();
        facts.Printers = new List<string> { "HP LaserJet 1020", "Microsoft Print to PDF" };

        var check = Check(InstallService.Inspect(facts), "printers");

        Assert.Equal(InstallState.Ready, check.State);
        Assert.Contains("HP LaserJet 1020", check.Detail);
        Assert.Contains("2", check.Detail);
    }

    [Fact]
    public void AStoppedSpoolerIsStartedByTheInstaller()
    {
        var facts = ReadyPc();
        facts.Spooler = "stopped";

        var report = InstallService.Inspect(facts);

        Assert.Equal(InstallState.Fixes, Check(report, "spooler").State);
        Assert.Contains("starts it", Check(report, "spooler").Action);
        Assert.True(report.CanInstall);
    }

    [Fact]
    public void AnArmPcIsANoteAndNeverAStop()
    {
        var facts = ReadyPc();
        facts.IsArm64 = true;

        var report = InstallService.Inspect(facts);

        Assert.Equal(InstallState.Note, Check(report, "architecture").State);
        Assert.True(report.CanInstall);
    }

    [Fact]
    public void NothingElseHasToBeInstalledNoDotNetNoPython()
    {
        var check = Check(InstallService.Inspect(ReadyPc()), "runtime");

        Assert.Equal(InstallState.Ready, check.State);
        Assert.Contains(".NET", check.Detail);
        Assert.Contains("inside the program", check.Detail);
        Assert.Contains("Python", check.Detail);
    }

    // ============================== update is install again ==============================

    [Fact]
    public void AnUpdateReplacesTheProgramFilesAndLeavesTheDatabaseAlone()
    {
        var facts = ReadyPc();
        facts.ExistingInstall = true;
        facts.ExistingDatabaseBytes = 5 * 1024 * 1024;

        var report = InstallService.Inspect(facts);

        Assert.Equal(InstallState.Fixes, Check(report, "install").State);
        Assert.Contains("program files only", Check(report, "install").Action);
        Assert.Contains("5.0 MB", Check(report, "data").Detail);
        Assert.Contains(facts.CommonDataFolder + @"\Area11", Check(report, "data").Detail);
        Assert.True(report.CanInstall);
    }

    [Fact]
    public void ANewShopFolderIsCreatedAndNothingIsEverDeleted()
    {
        var check = Check(InstallService.Inspect(ReadyPc()), "data");

        Assert.Equal(InstallState.Ready, check.State);

        // The promise in the owner's own words: nothing is deleted. The sentence says "ever" rather
        // than "never", and v0.22.1 pins the words that are actually shown.
        Assert.Contains("Nothing is deleted", check.Action);
        Assert.Contains("uninstaller", check.Action);
    }

    // ============================== the switch the script uses ==============================

    [Fact]
    public void ReadingTheMachineTakesTheAnswersTheScriptSends()
    {
        var facts = InstallService.Read(new[]
        {
            "--install-check", "report.txt",
            "--printers", "HP LaserJet 1020|Microsoft Print to PDF",
            "--spooler", "running",
            "--sdk", "yes"
        });

        Assert.True(facts.PrintersKnown);
        Assert.Equal(2, facts.Printers.Count);
        Assert.Equal("running", facts.Spooler);
        Assert.True(facts.HasDotnetSdk);
        Assert.False(facts.HasPublishedBundle);
        Assert.True(facts.BuildNumber > 0);
        Assert.False(string.IsNullOrWhiteSpace(facts.ProgramFilesFolder));
    }

    [Fact]
    public void APublishedFolderNextToTheInstallerIsRecognised()
    {
        var folder = TempFolder();
        File.WriteAllText(Path.Combine(folder, InstallService.ExeName), "not really a program");

        var facts = InstallService.Read(new[] { "--install-check", "r.txt", "--source", folder });

        Assert.True(facts.HasPublishedBundle);
        Assert.Equal(folder, facts.SourceFolder);
    }

    [Fact]
    public void TheCheckWritesItsReportAndAnswersWithAnExitCode()
    {
        var folder = TempFolder();
        var report = Path.Combine(folder, "install-report.txt");

        // On the owner's Windows 10 this answers 0; the assertion is written so it also holds on a
        // machine that is not Windows (where the version check itself is the stop).
        var code = InstallService.RunCheck(new[]
        {
            "--install-check", report, "--sdk", "yes", "--printers", "HP LaserJet 1020", "--spooler", "running"
        });

        Assert.True(File.Exists(report));

        var text = File.ReadAllText(report);
        Assert.Contains("AREA11 INSTALLER", text);
        Assert.Contains("Program :", text);
        Assert.Contains("Data    :", text);
        Assert.Equal(text.Contains("READY TO INSTALL", StringComparison.Ordinal) ? 0 : 2, code);

        // No SDK and no finished copy: a stop, whatever machine this is.
        Assert.Equal(2, InstallService.RunCheck(new[] { "--install-check", report }));
        Assert.Contains("STOP", File.ReadAllText(report));
    }

    [Fact]
    public void AnUnusableReportPathSaysSoInsteadOfPretendingItWorked()
    {
        Assert.Equal(3, InstallService.RunCheck(new[] { "--install-check" }));
        Assert.Equal(3, InstallService.RunCheck(new[] { "--install-check", Path.Combine(TempFolder(), "no-such-folder", "r.txt") }));
    }

    // ============================== the text itself ==============================

    [Fact]
    public void EveryStopLineIsPrintedLastSoItIsWhatTheOwnerSeesOnTop()
    {
        var facts = ReadyPc();
        facts.FreeDiskMb = 10;

        var lines = InstallService.Inspect(facts).ToText()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var stopLine = lines.FindIndex(line => line.StartsWith("STOP", StringComparison.Ordinal));

        Assert.True(stopLine > 0);
        Assert.DoesNotContain(lines.Skip(stopLine + 1), line => line.StartsWith("ready", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSummaryCountsEveryKindOfLine()
    {
        var facts = ReadyPc();
        facts.Printers = new List<string>();          // one line the installer fixes
        facts.BuildNumber = 10240;                    // one line that stops

        var report = InstallService.Inspect(facts);

        Assert.Contains("1 the installer fixes", report.Summary);
        Assert.Contains("1 stop", report.Summary);
        Assert.False(report.CanInstall);
    }

    // ============================== helpers ==============================

    private static InstallCheck Check(InstallReport report, string key) =>
        report.Checks.Single(check => check.Key == key);

    /// <summary>A shop PC that needs nothing: Windows 10 updated, the program copied in, a printer.</summary>
    private static MachineFacts ReadyPc() => new()
    {
        WindowsName = "Microsoft Windows 10.0.19045",
        BuildNumber = 19045,
        ProgramFilesFolder = @"C:\Program Files",
        CommonDataFolder = @"C:\ProgramData",
        DesktopFolder = @"C:\Users\Public\Desktop",
        StartMenuFolder = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs",
        FreeDiskMb = 40000,
        HasPublishedBundle = true,
        SourceFolder = @"D:\dist\Area11",
        PrintersKnown = true,
        Printers = new List<string> { "HP LaserJet 1020" },
        Spooler = "running"
    };

    private static string TempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
