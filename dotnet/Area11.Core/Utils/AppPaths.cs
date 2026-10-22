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

/// <summary>Shop-local time in the exact text format SQLite stores (Spec 5.1.3).</summary>
public static class Clock
{
    private const string StampFormat = "yyyy-MM-dd HH:mm:ss";

    public static string Now() => DateTime.Now.ToString(StampFormat, CultureInfo.InvariantCulture);

    public static string Today() => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string MonthKey() => DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
