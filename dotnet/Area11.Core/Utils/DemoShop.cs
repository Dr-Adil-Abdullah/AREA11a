using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Area11.Core.Utils;

/// <summary>
/// The demo shop (D-76): a **separate** Area11 that is never the shop's own database.
///
/// Why it exists: the owner asked for one button that loads demo data, and for the demo - button
/// and data - to be removed at the end of the project. Both promises are only honest if the demo
/// lives somewhere the real books cannot reach. So a run started with `--demo` keeps *everything*
/// under `C:\ProgramData\Area11\demo` - database, photos, logs, backups - and the real
/// `C:\ProgramData\Area11\area11.db` is never opened, never written, never migrated.
///
/// Why a distinct folder and not a flag inside the same database: on the day the demo is deleted,
/// deleting a folder cannot delete a bill. A row marked "demo" in the real table would have to be
/// untangled by hand, and one mistake would take real money out of the books.
///
/// The switch is read once, by the desktop program, before the first window; every path in the
/// application follows <see cref="DataFolder"/> from then on.
/// </summary>
public static class DemoShop
{
    /// <summary>The switch that starts the demo: `Area11.exe --demo`.</summary>
    public const string Flag = "--demo";

    /// <summary>The folder inside the shop's data folder that holds the whole demo.</summary>
    public const string FolderName = "demo";

    /// <summary>The demo's data folder, or null when this is the real shop.</summary>
    public static string? DataFolder { get; private set; }

    /// <summary>True while the program is running as the demo shop.</summary>
    public static bool IsOn => !string.IsNullOrWhiteSpace(DataFolder);

    /// <summary>
    /// Did the caller ask for the demo? Kept separate from <see cref="TurnOn"/> so the rule can be
    /// tested without touching the paths of a running program.
    /// </summary>
    public static bool WantsDemo(IEnumerable<string>? args) =>
        args is not null && args.Any(arg =>
            string.Equals(arg?.Trim(), Flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>Where the demo lives, given the shop's real data folder.</summary>
    public static string FolderUnder(string dataRoot) => Path.Combine(dataRoot, FolderName);

    /// <summary>
    /// Switches this run to the demo shop. Called once, before any window opens, because every path
    /// below it (database, photos, logs) reads <see cref="DataFolder"/>.
    /// </summary>
    public static string TurnOn(string dataRoot)
    {
        var folder = FolderUnder(dataRoot);

        Directory.CreateDirectory(folder);

        DataFolder = folder;

        return folder;
    }

    /// <summary>
    /// Back to the real shop. Only the tests use this: a long-running program never switches back,
    /// because half the shop would then be reading one database and half the other.
    /// </summary>
    public static void TurnOff() => DataFolder = null;
}
