using System.Globalization;
using System.IO;
using System.Threading;

namespace Area11.Core.Utils;

/// <summary>
/// Where WPF's **silent** binding complaints are kept, so a blank page can be explained.
///
/// Why this exists: a WPF binding that names a property which does not exist does not throw. The
/// framework writes one line to the debug output - which a shop PC never sees - and leaves the box
/// empty. The owner then reports "the page has no data", and the fault has to be found by reading
/// the XAML by eye. On 23 Oct 2026 the opposite kind of binding fault (a TwoWay binding to a
/// read-only label) killed the window outright; the quiet ones are the other half of the same
/// problem.
///
/// The desktop program attaches a listener that sends those lines here (see
/// `Desktop/BindingWatchInstall.cs`). The file lives in `%TEMP%\Area11` and **not** in the shop
/// folder: this is diagnostics, and it must not be able to slow down or fill the folder that holds
/// the shop's database.
///
/// Nothing here may ever throw - a watcher that crashes the till is worse than a blank column.
/// </summary>
public static class BindingWatch
{
    /// <summary>When the file passes this size it is set aside as `.old` and a fresh one starts.</summary>
    public const int MaxBytes = 200 * 1024;

    /// <summary>One complaint is clipped to this many characters: the file must stay readable.</summary>
    public const int MaxMessageLength = 600;

    /// <summary>The file the desktop listener writes, and the one the owner is asked to send.</summary>
    public static string FilePath =>
        Path.Combine(Path.GetTempPath(), "Area11", "binding-errors.txt");

    /// <summary>
    /// At most this many complaints are written per run. A page that binds 10,000 rows with one
    /// wrong column would otherwise write 10,000 lines and slow the till down for a diagnostic.
    /// The first ones are the ones that matter: they repeat, the first one names the path.
    /// </summary>
    public const int MaxNotedPerRun = 200;

    private static int _noted;

    /// <summary>Note one complaint. Blank text is ignored, so the file only holds real ones.</summary>
    public static void Note(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // Counted before the write so two threads cannot both think they are the last one; past
        // the cap the line is dropped rather than the till being slowed (F-31: the shop comes first).
        if (Interlocked.Increment(ref _noted) > MaxNotedPerRun)
        {
            return;
        }

        Append(FilePath, message);
    }

    /// <summary>
    /// Appends one timestamped line and returns the file it went to - or an empty string when
    /// nothing was written. Separate from <see cref="Note"/> so a test can use its own file and
    /// never touch the one the shop PC is watching.
    /// </summary>
    public static string Append(string path, string? message)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        // One complaint is ONE line: WPF sometimes writes a value with a line break in it, and a
        // file whose entries cannot be counted is not a log.
        var text = message!.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " | ", StringComparison.Ordinal)
            .Trim();

        if (text.Length > MaxMessageLength)
        {
            text = text[..MaxMessageLength] + " ...";
        }

        try
        {
            var folder = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // Keep the file useful instead of letting it grow until the disk complains. The old
            // file is kept as .old, because the FIRST complaint is usually the one that matters
            // and it would otherwise be the first thing lost.
            if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
            {
                File.Move(path, path + ".old", overwrite: true);
            }

            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                       + "  " + text + Environment.NewLine;

            File.AppendAllText(path, line);

            return path;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or NotSupportedException or ArgumentException
                                              or System.Security.SecurityException)
        {
            // A watcher may never become a second fault: the window carries on without the file.
            return string.Empty;
        }
    }

    /// <summary>How many complaints are on file - shown in the crash report so a number is visible.</summary>
    public static int CountIn(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllLines(path).Length : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or NotSupportedException or ArgumentException
                                              or System.Security.SecurityException)
        {
            return 0;
        }
    }
}
