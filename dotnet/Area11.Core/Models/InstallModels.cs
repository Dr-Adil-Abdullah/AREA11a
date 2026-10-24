using Area11.Core.Utils;
using System.Globalization;
using System.IO;

namespace Area11.Core.Models;

/// <summary>
/// How one line of the installer's report reads (F-47, D-19).
///
/// Four states only, because the owner has to be able to tell "nothing to do" from "the installer
/// will do it for you" from "stop here":
///   * <see cref="Ready"/> - this PC already has what is needed;
///   * <see cref="Note"/> - worth knowing, never a reason to stop;
///   * <see cref="Fixes"/> - not right yet, and **the installer repairs it by itself**;
///   * <see cref="Stop"/> - the install cannot go on until a person does something.
/// </summary>
public enum InstallState
{
    Ready,
    Note,
    Fixes,
    Stop
}

/// <summary>One thing the installer looked at, and what it found (F-47).</summary>
public sealed class InstallCheck
{
    public InstallCheck(string key, string title, InstallState state, string detail, string action)
    {
        Key = key;
        Title = title;
        State = state;
        Detail = detail;
        Action = action;
    }

    /// <summary>A short stable name (`windows`, `printers`, `data`), used by the tests.</summary>
    public string Key { get; }

    /// <summary>What was looked at, in two or three words: "Windows version".</summary>
    public string Title { get; }

    public InstallState State { get; }

    /// <summary>What was found, in plain words and with the real numbers.</summary>
    public string Detail { get; }

    /// <summary>What happens next. Empty when there is nothing to do.</summary>
    public string Action { get; }

    /// <summary>True when this line alone stops the install.</summary>
    public bool IsStop => State == InstallState.Stop;

    /// <summary>The word printed in the report's first column. ASCII, so a cmd window shows it.</summary>
    public string StateLabel => State switch
    {
        InstallState.Ready => "ready",
        InstallState.Note => "note",
        InstallState.Fixes => "does",
        _ => "STOP"
    };

    /// <summary>One line of the report: `ready  Windows version: ...`.</summary>
    public string Line => StateLabel + "  " + Title + ": " + Detail;

    /// <summary>One line with the action after it, for the lines that need one.</summary>
    public string LineWithAction => string.IsNullOrWhiteSpace(Action)
        ? Line
        : Line + " -> " + Action;
}

/// <summary>
/// One shortcut the installer writes: the desktop icon, the Start-menu entry and the uninstaller
/// (D-19). A shortcut is a **.lnk file**; nothing here is created by this class - it only says what
/// the scripts have to create, so the names can be tested on any machine.
/// </summary>
public sealed class InstallShortcut
{
    public InstallShortcut(string name, string folder, string target, string arguments, string description)
    {
        Name = name;
        Folder = folder;
        Target = target;
        Arguments = arguments;
        Description = description;
    }

    /// <summary>Name without `.lnk`: "Area11" or "Uninstall Area11".</summary>
    public string Name { get; }

    /// <summary>The folder the .lnk sits in (the desktop, or the Start menu's Programs folder).</summary>
    public string Folder { get; }

    public string Target { get; }

    public string Arguments { get; }

    public string Description { get; }

    /// <summary>Full path of the .lnk. Named `ShortcutPath` on purpose: a member called `Path`
    /// would hide the `System.IO.Path` type inside this class.</summary>
    public string ShortcutPath => Path.Combine(Folder, Name + ".lnk");
}

/// <summary>
/// Everything the installer is going to do, worked out before it touches the disk (F-47, D-19).
///
/// The two folders are the heart of it:
///   * the **program** goes to `C:\Program Files\Area11` - read-only for the shop, replaced on an
///     update, and gone on an uninstall;
///   * the **shop data** stays in `C:\ProgramData\Area11` - written by the cashier's own account,
///     never overwritten by an update, never deleted by the uninstaller.
/// Keeping them apart is what makes "install again to update" safe.
/// </summary>
public sealed class InstallPlan
{
    public string InstallFolder { get; set; } = string.Empty;

    public string ExePath { get; set; } = string.Empty;

    public string DataFolder { get; set; } = string.Empty;

    public string DatabaseFile { get; set; } = string.Empty;

    public string UninstallerPath { get; set; } = string.Empty;

    public string UninstallKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DisplayVersion { get; set; } = string.Empty;

    public string Publisher { get; set; } = string.Empty;

    /// <summary>The folder the program files are copied **from**: `dist\Area11` when it exists.</summary>
    public string SourceFolder { get; set; } = string.Empty;

    /// <summary>True when the script has to build the program first (`dotnet publish`).</summary>
    public bool NeedsPublish { get; set; }

    /// <summary>`icacls` argument that lets every signed-in person write the shop folder.</summary>
    public string DataFolderGrant { get; set; } = string.Empty;

    public List<InstallShortcut> Shortcuts { get; set; } = new();
}

/// <summary>
/// What the installer found on this PC (F-47). Every field is filled from the outside - read from
/// Windows by `InstallService.Read`, or written by a test - so the rules can be
/// proved without a Windows machine.
/// </summary>
public sealed class MachineFacts
{
    /// <summary>`Microsoft Windows 10.0.19045`, as Windows describes itself.</summary>
    public string WindowsName { get; set; } = string.Empty;

    /// <summary>The build number: 19045 on a current Windows 10, 17763 is the oldest supported.</summary>
    public int BuildNumber { get; set; }

    public bool IsArm64 { get; set; }

    public string ProgramFilesFolder { get; set; } = string.Empty;

    public string CommonDataFolder { get; set; } = string.Empty;

    /// <summary>The **public** desktop, so the icon is there for every person who signs in.</summary>
    public string DesktopFolder { get; set; } = string.Empty;

    /// <summary>The **all users** Start menu, for the same reason.</summary>
    public string StartMenuFolder { get; set; } = string.Empty;

    /// <summary>Free megabytes on the disk that holds Program Files; `-1` means "could not ask".</summary>
    public long FreeDiskMb { get; set; } = -1;

    public bool HasDotnetSdk { get; set; }

    /// <summary>True when `dist\Area11\Area11.exe` is already built, so no SDK is needed.</summary>
    public bool HasPublishedBundle { get; set; }

    public string SourceFolder { get; set; } = string.Empty;

    /// <summary>True when the script asked Windows and got an answer.</summary>
    public bool PrintersKnown { get; set; }

    public List<string> Printers { get; set; } = new();

    /// <summary>`running`, `stopped` or `unknown` - the script asks the service, not this class.</summary>
    public string Spooler { get; set; } = "unknown";

    public bool ExistingInstall { get; set; }

    /// <summary>Size of `C:\ProgramData\Area11\area11.db`; 0 when the shop is new.</summary>
    public long ExistingDatabaseBytes { get; set; }

    /// <summary>The queue the installer adds when the PC has no printer at all (Z-07).</summary>
    public string PrinterCountLabel => Printers.Count.ToString(CultureInfo.InvariantCulture);

    public bool SpoolerStopped => string.Equals(Spooler, "stopped", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The whole answer: the plan, every check, and one text the owner can read (F-47).
///
/// `CanInstall` is the gate: **not one line may say STOP**. A note or a line the installer repairs
/// never blocks, because a cashier PC without a printer must still be able to take a bill (Z-07).
/// </summary>
public sealed class InstallReport
{
    public InstallPlan Plan { get; set; } = new();

    public List<InstallCheck> Checks { get; set; } = new();

    public int StopCount => Checks.Count(check => check.State == InstallState.Stop);

    public int FixCount => Checks.Count(check => check.State == InstallState.Fixes);

    public int NoteCount => Checks.Count(check => check.State == InstallState.Note);

    public int ReadyCount => Checks.Count(check => check.State == InstallState.Ready);

    /// <summary>True when nothing says STOP.</summary>
    public bool CanInstall => StopCount == 0;

    /// <summary>`4 ready, 1 note, 2 the installer fixes, 0 stop`.</summary>
    public string Summary => string.Join(", ", new[]
    {
        ReadyCount.ToString(CultureInfo.InvariantCulture) + " ready",
        NoteCount.ToString(CultureInfo.InvariantCulture) + (NoteCount == 1 ? " note" : " notes"),
        FixCount.ToString(CultureInfo.InvariantCulture) + " the installer fixes",
        StopCount.ToString(CultureInfo.InvariantCulture) + " stop"
    });

    /// <summary>One line per check, STOP lines last so the reason is the last thing on the screen.</summary>
    public IEnumerable<InstallCheck> InReportOrder =>
        Checks.Where(check => check.State != InstallState.Stop)
              .Concat(Checks.Where(check => check.State == InstallState.Stop));

    /// <summary>
    /// The report as plain text - written to `logs\install-*.txt` and printed by the installer.
    /// ASCII only on purpose: a cmd window with the old codepage must show it correctly.
    /// </summary>
    public string ToText()
    {
        var lines = new List<string>
        {
            "AREA11 INSTALLER - this PC",
            "==========================",
            string.Empty
        };

        foreach (var check in InReportOrder)
        {
            lines.Add(check.LineWithAction);
        }

        lines.Add(string.Empty);
        lines.Add("Program : " + Plan.DisplayName + " " + Plan.DisplayVersion + " (v" + BuildInfo.Version + ")");
        lines.Add("Files   : " + Plan.InstallFolder);
        lines.Add("Exe     : " + Plan.ExePath);
        lines.Add("Data    : " + Plan.DataFolder);
        lines.Add("Database: " + Plan.DatabaseFile);
        lines.Add("Uninstall: " + Plan.UninstallerPath);

        foreach (var shortcut in Plan.Shortcuts)
        {
            lines.Add("Shortcut: " + shortcut.ShortcutPath);
        }

        lines.Add(string.Empty);
        lines.Add("Summary : " + Summary);
        lines.Add("Result  : " + (CanInstall
            ? "READY TO INSTALL"
            : "STOPPED - fix the line marked STOP and run the installer again"));

        return string.Join(Environment.NewLine, lines);
    }
}
