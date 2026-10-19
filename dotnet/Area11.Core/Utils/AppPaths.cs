using System.Globalization;

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

    public static string DatabaseFile => Path.Combine(DataRoot, "area11.db");

    public static string PhotosFolder => EnsureFolder("photos");

    public static string LogsFolder => EnsureFolder("logs");

    public static string BackupsFolder => EnsureFolder("backups");

    public static string ExportsFolder => EnsureFolder("exports");

    private static string EnsureFolder(string name)
    {
        var path = Path.Combine(DataRoot, name);
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
