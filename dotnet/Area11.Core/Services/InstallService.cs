using Area11.Core.Models;
using Area11.Core.Utils;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Area11.Core.Services;

/// <summary>
/// The installer's brain (F-47, D-19). It decides **what** has to happen on this PC and says so in
/// plain words; the two PowerShell scripts beside it are the hands that do it.
///
/// Why the split: a rule can be proved by a test, while copying files into `Program Files` can only
/// be proved on the owner's own Windows machine. Everything that can be decided without touching a
/// disk lives here:
///   * where the program goes (`C:\Program Files\Area11`) and where the shop data stays
///     (`C:\ProgramData\Area11`) - two folders that must never be the same one;
///   * which shortcuts are written (desktop, Start menu, uninstaller) - **public** ones, so the icon
///     is there for every person who signs in on the shop PC;
///   * what "ready" means: Windows 10 build 17763 or newer, about 600 MB free, a program to copy
///     (a finished `dist\Area11` or the .NET SDK to build one), and never a reason to stop for a
///     missing printer - a shop without a printer must still take a bill (Z-07).
///
/// The same program asks these questions at install time: `Area11.exe --install-check report.txt`
/// writes <see cref="InstallReport.ToText"/> to a file and returns 0 (go) or 2 (stop). That way the
/// text the owner reads and the rules the tests pin down are the same code.
/// </summary>
public static class InstallService
{
    /// <summary>The program and the shop folder share this name on purpose: one shop, one name.</summary>
    public const string AppName = "Area11";

    /// <summary>What Add/Remove Programs shows.</summary>
    public const string DisplayName = "Area11 - pharmacy POS";

    public const string Publisher = "Area11";

    /// <summary>The folder under Program Files. Program files are replaced on every update.</summary>
    public const string InstallFolderName = "Area11";

    /// <summary>The folder under ProgramData. Nothing here is ever replaced or deleted (D-19).</summary>
    public const string DataFolderName = "Area11";

    public const string ExeName = "Area11.exe";

    public const string DatabaseName = "area11.db";

    /// <summary>Lives inside the install folder, so it is removed with the program, not with the data.</summary>
    public const string UninstallerName = "UNINSTALL-AREA11.cmd";

    /// <summary>Add/Remove Programs entry (64-bit view - the installer runs elevated).</summary>
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Area11";

    /// <summary>Windows 10 version 1809. Older builds lack the APIs .NET 10 expects.</summary>
    public const int MinimumWindowsBuild = 17763;

    /// <summary>About 200 MB of program plus room for the shop to grow; checked before copying.</summary>
    public const long MinimumFreeMb = 600;

    /// <summary>
    /// `Users` by its SID, never by its name: the name is translated on a non-English Windows, the
    /// SID is not. Without this grant a cashier's account could not write the shop folder, and
    /// Area11 would quietly fall back to that person's own `%LocalAppData%` (see AppPaths) - the
    /// worst possible outcome: one shop with two databases.
    /// </summary>
    public const string EveryoneSid = "*S-1-5-32-545";

    /// <summary>Added when the PC has no printer at all, so a receipt is still possible (Z-07).</summary>
    public const string PdfPrinterName = "Microsoft Print to PDF";

    public const string DesktopShortcutName = "Area11";

    public const string StartMenuShortcutName = "Area11";

    public const string UninstallShortcutName = "Uninstall Area11";

    /// <summary>The version Add/Remove Programs shows: the same number as the sign-in screen.</summary>
    public static string Version => BuildInfo.Version;

    /// <summary>
    /// Every file the installer will write, worked out from the facts - no disk touched.
    /// </summary>
    public static InstallPlan Plan(MachineFacts facts)
    {
        var installFolder = Path.Combine(facts.ProgramFilesFolder, InstallFolderName);
        var dataFolder = Path.Combine(facts.CommonDataFolder, DataFolderName);
        var exe = Path.Combine(installFolder, ExeName);
        var uninstaller = Path.Combine(installFolder, UninstallerName);

        return new InstallPlan
        {
            InstallFolder = installFolder,
            ExePath = exe,
            DataFolder = dataFolder,
            DatabaseFile = Path.Combine(dataFolder, DatabaseName),
            UninstallerPath = uninstaller,
            UninstallKey = InstallService.UninstallKey,
            DisplayName = InstallService.DisplayName,
            DisplayVersion = Version,
            Publisher = InstallService.Publisher,
            SourceFolder = facts.SourceFolder,
            NeedsPublish = !facts.HasPublishedBundle,
            DataFolderGrant = EveryoneSid + ":(OI)(CI)M",
            Shortcuts = new List<InstallShortcut>
            {
                new(DesktopShortcutName, facts.DesktopFolder, exe, string.Empty,
                    "Area11 - pharmacy POS"),
                new(StartMenuShortcutName, facts.StartMenuFolder, exe, string.Empty,
                    "Area11 - pharmacy POS"),
                new(UninstallShortcutName, facts.StartMenuFolder, uninstaller, string.Empty,
                    "Remove Area11 from this PC. The shop data in " + dataFolder + " stays.")
            }
        };
    }

    /// <summary>
    /// All the questions in one place, in the order the owner reads them: the machine, then where
    /// the program comes from, then the shop's own folder.
    ///
    /// Only three things can stop an install - an old Windows, too little disk, and no way to get
    /// the program files. Everything else is either a note or something the installer repairs.
    /// </summary>
    public static InstallReport Inspect(MachineFacts facts)
    {
        var plan = Plan(facts);

        var checks = new List<InstallCheck>
        {
            WindowsCheck(facts),
            ArchitectureCheck(facts),
            RuntimeCheck(),
            SourceCheck(facts),
            DiskCheck(facts),
            SpoolerCheck(facts),
            PrinterCheck(facts),
            DataCheck(facts, plan),
            ExistingInstallCheck(facts, plan)
        };

        return new InstallReport { Plan = plan, Checks = checks };
    }

    // ------------------------------------------------------------------ the checks

    private static InstallCheck WindowsCheck(MachineFacts facts)
    {
        var name = string.IsNullOrWhiteSpace(facts.WindowsName) ? "Windows" : facts.WindowsName;
        var build = facts.BuildNumber.ToString(CultureInfo.InvariantCulture);

        if (facts.BuildNumber < MinimumWindowsBuild)
        {
            return new InstallCheck("windows", "Windows version", InstallState.Stop,
                "Area11 needs Windows 10 build 17763 (1809) or newer. This PC reports build " + build + " (" + name + ").",
                "Install the free Windows updates first, then run INSTALL-AREA11.cmd again.");
        }

        return new InstallCheck("windows", "Windows version", InstallState.Ready,
            name + ", build " + build + " - supported.",
            string.Empty);
    }

    private static InstallCheck ArchitectureCheck(MachineFacts facts) => facts.IsArm64
        ? new InstallCheck("architecture", "Processor", InstallState.Note,
            "This is an ARM PC. Area11 installs its x64 build and Windows 10 runs it under emulation - slower, but it works.",
            "Nothing to do.")
        : new InstallCheck("architecture", "Processor", InstallState.Ready,
            "64-bit Intel/AMD PC - the build Area11 is made for.",
            string.Empty);

    private static InstallCheck RuntimeCheck() => new(
        "runtime", "What has to be installed", InstallState.Ready,
        "Nothing else: the .NET runtime travels inside the program (D-19), the database engine ships with it, and there is no Python anywhere.",
        string.Empty);

    private static InstallCheck SourceCheck(MachineFacts facts)
    {
        if (facts.HasPublishedBundle)
        {
            return new InstallCheck("source", "Program files", InstallState.Ready,
                "Using the finished copy in " + facts.SourceFolder + " - no build needed.",
                string.Empty);
        }

        if (facts.HasDotnetSdk)
        {
            return new InstallCheck("source", "Program files", InstallState.Fixes,
                "No finished copy in dist\\Area11, but the .NET SDK is on this PC.",
                "The installer builds a self-contained Area11 (about two minutes, once).");
        }

        return new InstallCheck("source", "Program files", InstallState.Stop,
            "There is no finished copy in dist\\Area11 and no .NET SDK on this PC, so there is nothing to install.",
            "On the build PC run tools\\MAKE-INSTALLER.cmd, then copy the dist folder next to this installer and run it again.");
    }

    private static InstallCheck DiskCheck(MachineFacts facts)
    {
        if (facts.FreeDiskMb < 0)
        {
            return new InstallCheck("disk", "Free space", InstallState.Note,
                "Windows did not say how much space is free. Area11 needs about " + MinimumFreeMb + " MB.",
                "If the copy stops with a full-disk message, free some space and run the installer again.");
        }

        var free = facts.FreeDiskMb.ToString("N0", CultureInfo.InvariantCulture);
        var needed = MinimumFreeMb.ToString("N0", CultureInfo.InvariantCulture);

        if (facts.FreeDiskMb < MinimumFreeMb)
        {
            return new InstallCheck("disk", "Free space", InstallState.Stop,
                "Only " + free + " MB is free and Area11 needs about " + needed + " MB.",
                "Free some space (clear the Recycle Bin, remove old downloads), then run the installer again.");
        }

        return new InstallCheck("disk", "Free space", InstallState.Ready,
            free + " MB free; about " + needed + " MB is needed.",
            string.Empty);
    }

    private static InstallCheck SpoolerCheck(MachineFacts facts)
    {
        if (facts.SpoolerStopped)
        {
            return new InstallCheck("spooler", "Print spooler", InstallState.Fixes,
                "The Windows print spooler is stopped, so nothing can print.",
                "The installer sets it to start with Windows and starts it now.");
        }

        if (string.Equals(facts.Spooler, "running", StringComparison.OrdinalIgnoreCase))
        {
            return new InstallCheck("spooler", "Print spooler", InstallState.Ready,
                "Running - receipts can reach a printer.",
                string.Empty);
        }

        return new InstallCheck("spooler", "Print spooler", InstallState.Note,
            "Windows did not say whether the print spooler is running.",
            "The installer starts it anyway; a printer problem never stops Area11 (Z-07).");
    }

    private static InstallCheck PrinterCheck(MachineFacts facts)
    {
        if (!facts.PrintersKnown)
        {
            return new InstallCheck("printers", "Printer", InstallState.Note,
                "Windows did not say which printers are installed.",
                "Add the receipt printer in Windows when it arrives; Area11 refuses nothing without one (Z-07).");
        }

        if (facts.Printers.Count > 0)
        {
            return new InstallCheck("printers", "Printer", InstallState.Ready,
                "Found " + facts.PrinterCountLabel + ": " + ShortPrinterList(facts.Printers) + ".",
                string.Empty);
        }

        return new InstallCheck("printers", "Printer", InstallState.Fixes,
            "No printer is installed on this PC.",
            "The installer adds " + PdfPrinterName + ", so a receipt can still be printed or saved as a PDF (Z-07).");
    }

    private static InstallCheck DataCheck(MachineFacts facts, InstallPlan plan)
    {
        if (facts.ExistingDatabaseBytes > 0)
        {
            var text = (facts.ExistingDatabaseBytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture);

            return new InstallCheck("data", "Shop data", InstallState.Ready,
                "The shop database is already here (" + text + " MB) and is kept exactly as it is: " + plan.DatabaseFile,
                "Updated only by Area11 itself. The installer never overwrites or deletes this folder.");
        }

        return new InstallCheck("data", "Shop data", InstallState.Ready,
            "A new shop folder is created here: " + plan.DataFolder,
            "Nothing is deleted, ever - not by an update and not by the uninstaller.");
    }

    private static InstallCheck ExistingInstallCheck(MachineFacts facts, InstallPlan plan)
    {
        if (facts.ExistingInstall)
        {
            return new InstallCheck("install", "This PC", InstallState.Fixes,
                "Area11 is already installed in " + plan.InstallFolder + ".",
                "The installer replaces the program files only; the shop data above is not touched. Close Area11 first.");
        }

        return new InstallCheck("install", "This PC", InstallState.Ready,
            "Area11 is not installed on this PC yet; it goes to " + plan.InstallFolder + ".",
            string.Empty);
    }

    private static string ShortPrinterList(List<string> printers)
    {
        const int shown = 3;
        var names = printers.Where(name => !string.IsNullOrWhiteSpace(name)).Take(shown).ToList();
        var text = string.Join(", ", names);

        return printers.Count > shown
            ? text + " and " + (printers.Count - shown).ToString(CultureInfo.InvariantCulture) + " more"
            : text;
    }

    // ------------------------------------------------------------------ reading the machine

    /// <summary>
    /// Reads what Windows can be asked without a driver: the version, the four folders, the free
    /// space, and whether an older Area11 and its database are already here.
    ///
    /// Printers, the spooler and the .NET SDK are **passed in by the installer script** (they need
    /// Windows' own services); see <see cref="RunCheck"/> for the switches.
    /// </summary>
    public static MachineFacts Read(IReadOnlyList<string> args)
    {
        var facts = new MachineFacts
        {
            WindowsName = RuntimeInformation.OSDescription,
            BuildNumber = Environment.OSVersion.Version.Build,
            IsArm64 = RuntimeInformation.OSArchitecture == Architecture.Arm64,
            ProgramFilesFolder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            CommonDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            DesktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            StartMenuFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
        };

        if (string.IsNullOrWhiteSpace(facts.ProgramFilesFolder))
        {
            facts.ProgramFilesFolder = @"C:\Program Files";
        }

        if (string.IsNullOrWhiteSpace(facts.CommonDataFolder))
        {
            facts.CommonDataFolder = @"C:\ProgramData";
        }

        var printers = Option(args, "--printers");
        if (printers is not null)
        {
            facts.PrintersKnown = true;
            facts.Printers = printers
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .ToList();
        }

        var spooler = Option(args, "--spooler");
        if (!string.IsNullOrWhiteSpace(spooler))
        {
            facts.Spooler = spooler.Trim().ToLowerInvariant();
        }

        facts.HasDotnetSdk = string.Equals(Option(args, "--sdk"), "yes", StringComparison.OrdinalIgnoreCase);

        var source = Option(args, "--source");
        if (!string.IsNullOrWhiteSpace(source))
        {
            facts.SourceFolder = source.Trim();
            facts.HasPublishedBundle = File.Exists(Path.Combine(facts.SourceFolder, ExeName));
        }

        facts.FreeDiskMb = FreeMegabytes(facts.ProgramFilesFolder);

        var plan = Plan(facts);
        try
        {
            facts.ExistingInstall = File.Exists(plan.ExePath);
            facts.ExistingDatabaseBytes = File.Exists(plan.DatabaseFile)
                ? new FileInfo(plan.DatabaseFile).Length
                : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A folder we cannot read is the same answer as "not installed yet"; the copy that
            // follows reports its own permission problem in Windows' own words.
            facts.ExistingInstall = false;
            facts.ExistingDatabaseBytes = 0;
        }

        return facts;
    }

    private static long FreeMegabytes(string folder)
    {
        try
        {
            var root = Path.GetPathRoot(folder);
            if (string.IsNullOrEmpty(root))
            {
                return -1;
            }

            return new DriveInfo(root).AvailableFreeSpace / (1024L * 1024L);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                             or ArgumentException)
        {
            return -1;
        }
    }

    /// <summary>The value of `--name value`, or null when the switch was not given.</summary>
    private static string? Option(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ the entry point

    /// <summary>
    /// `Area11.exe --install-check &lt;report.txt&gt; [--source &lt;folder&gt;] [--printers a|b]
    /// [--spooler running|stopped|unknown] [--sdk yes|no]`
    ///
    /// Writes the report and answers with an exit code the installer script can read:
    ///   0 - ready to install;
    ///   2 - at least one line says STOP (the report says which);
    ///   3 - the report file could not be written, so nothing was decided.
    /// </summary>
    public static int RunCheck(IReadOnlyList<string> args)
    {
        if (args.Count < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            return 3;
        }

        try
        {
            var report = Inspect(Read(args));
            File.WriteAllText(args[1], report.ToText() + Environment.NewLine);
            return report.CanInstall ? 0 : 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                             or ArgumentException or NotSupportedException)
        {
            return 3;
        }
    }
}
