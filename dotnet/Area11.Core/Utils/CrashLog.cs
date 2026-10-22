using System.Globalization;
using System.IO;

namespace Area11.Core.Utils;

/// <summary>
/// Writes the full detail of a crash to a file the owner can send, even when the normal log is
/// unusable.
///
/// Why it exists: the first successful build of the desktop app started and stopped before the
/// login window, and the console showed only "Area11 stopped with an error" - because a WinExe has
/// nowhere to print. A failure the owner cannot quote is a failure nobody can fix, so every crash
/// now leaves a `crash-YYYY-MM-DD_HHmmss.txt` in the log folder with the exception, its inner
/// exceptions and the message.
///
/// It never throws: if the shop data folder is the thing that broke, it falls back to %TEMP% and,
/// if even that fails, gives up quietly - a crash reporter may never become a second crash.
/// </summary>
public static class CrashLog
{
    /// <summary>Writes the crash and returns the full path of the file (or an empty string).</summary>
    public static string Write(string area, Exception? exception, string? note = null)
    {
        var text = Build(area, exception, note);

        foreach (var folder in CandidateFolders())
        {
            try
            {
                Directory.CreateDirectory(folder);

                var file = Path.Combine(folder,
                    "crash-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".txt");

                File.WriteAllText(file, text);

                return file;
            }
            catch (Exception writeFailure) when (writeFailure is IOException or UnauthorizedAccessException
                                                     or NotSupportedException or ArgumentException)
            {
                // Try the next folder. A crash reporter that throws is worse than no reporter.
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> CandidateFolders()
    {
        string? shop = null;

        try
        {
            shop = AppPaths.LogsFolder;
        }
        catch (Exception pathFailure) when (pathFailure is IOException or UnauthorizedAccessException
                                              or InvalidOperationException)
        {
            // The data folder itself is the problem; fall through to %TEMP%.
        }

        if (!string.IsNullOrWhiteSpace(shop))
        {
            yield return shop;
        }

        yield return Path.Combine(Path.GetTempPath(), "Area11");
    }

    private static string Build(string area, Exception? exception, string? note)
    {
        var lines = new List<string>
        {
            "Area11 crash report",
            "===================",
            "When     : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            "Version  : " + BuildInfo.Version + " (" + BuildInfo.CodeName + ")",
            "Area     : " + area,
            "Computer : " + Environment.MachineName,
            "Windows  : " + Environment.OSVersion.VersionString,
            ".NET     : " + Environment.Version,
            "Folder   : " + SafeBaseDirectory()
        };

        if (!string.IsNullOrWhiteSpace(note))
        {
            lines.Add("Note     : " + note);
        }

        lines.Add(string.Empty);

        if (exception is null)
        {
            lines.Add("No exception object was available.");
            return string.Join(Environment.NewLine, lines);
        }

        var depth = 0;

        for (var current = exception; current is not null; current = current.InnerException)
        {
            depth++;

            lines.Add(depth == 1 ? "EXCEPTION" : "INNER EXCEPTION " + depth);
            lines.Add("---------");
            lines.Add("Type    : " + current.GetType().FullName);
            lines.Add("Message : " + current.Message);
            lines.Add("Source  : " + current.Source);
            lines.Add(string.Empty);
            lines.Add(current.ToString());
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string SafeBaseDirectory()
    {
        try
        {
            return AppContext.BaseDirectory;
        }
        catch (Exception)
        {
            return "(unknown)";
        }
    }
}
