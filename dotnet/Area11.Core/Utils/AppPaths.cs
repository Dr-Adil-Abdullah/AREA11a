using System.Globalization;
using System.IO;

namespace Area11.Core.Utils;

/// <summary>
/// Every file the shop owns lives under one folder that survives updates:
/// %ProgramData%\Area11 (or %LocalAppData%\Area11 when ProgramData is not writable).
/// The installed application itself lives in Program Files, which is read-only.
/// </summary>
public static class AppPaths
{
    private static readonly Lazy<string> RootLazy = new(ResolveRoot, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Everything this run owns: database, photos, logs, backups and exports.
    ///
    /// There is exactly one folder and one answer. Until v0.30.2 this property could be pointed at a
    /// second folder by the `--demo` switch (D-76); the owner had the demo removed from the product on
    /// 23 Oct 2026 (D-77), so the switch, its folder and its three files are gone. A shop that cannot
    /// be redirected is a shop whose books cannot be split in two by a command-line flag.
    /// </summary>
    public static string DataRoot => RootLazy.Value;

    /// <summary>
    /// Where this counter keeps the shop's database. A second counter points at the main PC's shared
    /// folder through `shop.ini`, so the whole shop reads one file (F-45).
    /// </summary>
    public static string DatabaseFolder =>
        ShopConfig.Current.DataFolder is { Length: > 0 } folder ? folder : DataRoot;

    public static string DatabaseFile => Path.Combine(DatabaseFolder, "area11.db");

    /// <summary>
    /// Photos sit **beside the database** (U-19 stores only the file name), so both counters see the
    /// same pictures and a backup of one folder keeps them together.
    /// </summary>
    public static string PhotosFolder => EnsureUnder(DatabaseFolder, "photos");

    public static string LogsFolder => EnsureFolder("logs");

    public static string BackupsFolder => EnsureFolder("backups");

    public static string ExportsFolder => EnsureFolder("exports");

    private static string EnsureFolder(string name) => EnsureUnder(DataRoot, name);

    private static string EnsureUnder(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ResolveRoot()
    {
        var problems = new List<string>();
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Area11"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Area11")
        };

        foreach (var candidate in candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);
                var probe = Path.Combine(candidate, ".write-probe");
                File.WriteAllText(probe, "area11");
                File.Delete(probe);
                return candidate;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                problems.Add($"{candidate}: {exception.Message}");
            }
        }

        throw new InvalidOperationException(
            "Area11 could not find a writable data folder. Tried: " + string.Join(" | ", problems));
    }
}

/// <summary>
/// Shop-local time in the exact text format SQLite stores (Spec 5.1.3).
///
/// Every stamp in the database goes through here, and **every one of them now goes through
/// `TimeSource`** (v0.35.0, D-92): when a time server answered at startup, `TimeSource.Now` is the
/// server's time; when none answered, it is the computer's own clock. One line changed here means
/// one hundred call sites follow, and no service had to learn about the internet.
/// </summary>
public static class Clock
{
    private const string StampFormat = "yyyy-MM-dd HH:mm:ss";

    public static string Now() => TimeSource.Now.ToString(StampFormat, CultureInfo.InvariantCulture);

    public static string Today() => TimeSource.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string MonthKey() => TimeSource.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>
    /// The date <paramref name="days"/> days ago, as SQLite stores it - the cut for the 90 days of bills
    /// that travel to the cloud (F-48, Spec 25.5). Date only, so the first day is included whole.
    /// </summary>
    public static string DateDaysAgo(int days) =>
        TimeSource.Now.AddDays(-Math.Abs(days)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
