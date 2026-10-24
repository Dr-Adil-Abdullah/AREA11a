using System.Globalization;
using System.IO;

namespace Area11.Core.Utils;

/// <summary>
/// A heartbeat: **what this run of the program actually did**, newest line last (v0.34.0, D-90).
///
/// Why it exists: on 23 Oct 2026 the owner was told to look for `%TEMP%\Area11\binding-errors.txt`
/// after a run, found nothing, and asked "no file?" - and the honest answer was *that proves
/// nothing*: a run that found no binding faults and a run that never reached the window leave the
/// same empty folder. An absence is only evidence when its cause is knowable, so the program now
/// writes one small file on every start and adds a line whenever it does something worth naming:
///
///     13:52:46  started v0.33.0 (Mauveine)
///     13:52:46  database ready
///     13:53:31  signed in: Owner
///     13:53:35  page: Dashboard
///     13:56:10  closed normally
///
/// Then the reading is exact: **no file = the program never started** (a build or launcher fault),
/// **file stopped after "database ready" = it died before the window**, **file with pages and no
/// crash line = the pages it lists were drawn without a silent binding fault**.
///
/// It lives in `%TEMP%\Area11`, never in the shop folder: the shop folder can be the second
/// counter's share, and this is diagnostics. Nothing here may ever throw - a heartbeat that stops
/// the till is worse than no heartbeat.
/// </summary>
public static class RunTrail
{
    /// <summary>The file the report tool prints and the owner may be asked to send.</summary>
    public static string FilePath =>
        Path.Combine(Path.GetTempPath(), "Area11", "last-run.txt");

    /// <summary>
    /// How many lines the file may hold. Past this the oldest middle lines go, but the header and
    /// the newest lines stay - the start and the end are the two halves that explain a run.
    /// </summary>
    public const int MaxLines = 140;

    /// <summary>Lines kept from the top when the file is trimmed (the header).</summary>
    private const int HeaderLines = 8;

    /// <summary>
    /// Lines kept from the bottom when the file is trimmed (what just happened).
    ///
    /// The dropped-lines note occupies one line of its own, so the tail is one shorter than the space
    /// left - otherwise a trimmed file would sit at MaxLines + 1 and never actually obey the cap.
    /// </summary>
    private const int TailLines = MaxLines - HeaderLines - 1;

    private static readonly object Gate = new();

    /// <summary>
    /// Starts a new file: the header, then the first line. Called once, by `Program.Main`, **before
    /// the first window**, so a start that dies in the theme still leaves a line.
    /// </summary>
    /// <returns>The file written, or an empty string when nothing could be written.</returns>
    public static string Begin(string appLine, string? path = null)
    {
        var target = string.IsNullOrWhiteSpace(path) ? FilePath : path!;

        try
        {
            var folder = Path.GetDirectoryName(target);

            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder!);
            }

            var header = new List<string>
            {
                "Area11 - what this run did (newest line last)",
                "Started : " + Stamp(),
                "Build   : " + (appLine ?? string.Empty),
                "Computer: " + Safe(() => Environment.MachineName),
                "Windows : " + Safe(() => Environment.UserName),
                "Folder  : " + Safe(() => AppContext.BaseDirectory),
                "Process : " + Safe(() => Environment.ProcessId.ToString(CultureInfo.InvariantCulture)),
                "------------------------------------------------------------"
            };

            header.Add(Line("started"));

            lock (Gate)
            {
                File.WriteAllLines(target, header);
            }

            return target;
        }
        catch (Exception)
        {
            // Everything: this is diagnostics, and it runs before the window exists. A heartbeat
            // that throws would turn a healthy start into a crash report about a heartbeat.
            return string.Empty;
        }
    }

    /// <summary>
    /// Adds one line. Never throws. A blank line is ignored, so a caller does not have to check.
    /// </summary>
    /// <returns>The file written, or an empty string when nothing was written.</returns>
    public static string Step(string? what, string? path = null) =>
        Append(path is null ? FilePath : path, what);

    /// <summary>
    /// Adds one line to a named file. Separate from <see cref="Step"/> so a test writes its own
    /// file and never touches the one the shop PC is watching.
    /// </summary>
    public static string Append(string? path, string? what)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(what))
        {
            return string.Empty;
        }

        try
        {
            var folder = Path.GetDirectoryName(path!);

            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder!);
            }

            lock (Gate)
            {
                // One line is one line: a message with a break in it must not become two entries
                // that read like two events.
                // Windows line ends first, so "\r\n" becomes one separator instead of a space
                // followed by a separator - the point is one entry per line, not one per break.
                var text = (what ?? string.Empty)
                    .Replace("\r\n", " | ", StringComparison.Ordinal)
                    .Replace("\n", " | ", StringComparison.Ordinal)
                    .Replace("\r", " ", StringComparison.Ordinal)
                    .Trim();

                // One step is one line (v0.35.2). `AppendAllText` writes exactly the characters it is
                // given - no line end - so v0.34.0 and v0.35.x glued every step onto the previous one
                // and the whole run read as one long line. `AppendAllLines` writes the line and its
                // terminator, which is what "one line per step" means on Windows and in the tests.
                File.AppendAllLines(path!, new[] { Line(text) });

                var lines = File.ReadAllLines(path!);

                if (lines.Length > MaxLines)
                {
                    var kept = new List<string>(MaxLines);
                    kept.AddRange(lines[..HeaderLines]);
                    kept.Add("... " + (lines.Length - HeaderLines - TailLines) + " older line(s) dropped");
                    kept.AddRange(lines[^TailLines..]);

                    File.WriteAllLines(path!, kept);
                }
            }

            return path!;
        }
        catch (Exception)
        {
            // The same rule as Begin: never a second fault.
            return string.Empty;
        }
    }

    /// <summary>
    /// How many lines the file holds - the report tool prints this, and the tests read it.
    /// </summary>
    public static int CountIn(string? path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? 0 : File.ReadAllLines(path!).Length;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string Line(string text) => Stamp() + "  " + text;

    private static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Safe(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return "unknown";
        }
    }
}
