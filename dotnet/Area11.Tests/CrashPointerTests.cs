using Area11.Core.Utils;
using System.IO;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The one line that says where the newest crash report is (v0.31.1, D-86).
///
/// Why this exists: on 23 Oct 2026 the launcher stopped and printed a report written the day
/// before by v0.29.0 - a fault that had already been repaired. The report had gone to a place the
/// launcher does not look (the shop folder is the shared folder for a second counter, and %TEMP%
/// when that folder cannot be written). The program now writes the path of its own newest report
/// into an always-writable file, and the launcher reads that instead of guessing folders.
///
/// The promises that must hold:
///   • the pointer is written in %TEMP%\Area11 - never in the shop folder, which can be a share;
///   • it holds the full path of the report that was just written, and nothing else;
///   • an empty path changes nothing and throws nothing - a crash reporter may never become a
///     second crash.
/// </summary>
public sealed class CrashPointerTests
{
    [Fact]
    public void ThePointerFileIsSomewhereAlwaysWritable()
    {
        Assert.StartsWith(Path.GetTempPath(), CrashLog.PointerPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("latest-crash.txt", CrashLog.PointerPath, StringComparison.OrdinalIgnoreCase);

        // The shop folder may be a share on another PC (D-62): a pointer there would be the very
        // guess that failed.
        Assert.DoesNotContain("area11.db", CrashLog.PointerPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThePointerNamesTheReportThatWasJustWritten()
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-pointer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var report = Path.Combine(folder, "crash-2026-10-23_174207.txt");
        File.WriteAllText(report, "Area11 crash report");

        // The pointer is a real file on this PC. A test may not leave it pointing at a report it
        // is about to delete - the launcher would name a file that no longer exists - so whatever
        // was there is put back afterwards.
        var pointer = CrashLog.PointerPath;
        var before = File.Exists(pointer) ? File.ReadAllText(pointer) : null;

        try
        {
            CrashLog.RecordNewest(report);

            Assert.Equal(report, File.ReadAllText(pointer).Trim());

            // An empty path must be ignored, not written over the pointer and not thrown.
            CrashLog.RecordNewest("   ");

            Assert.Equal(report, File.ReadAllText(pointer).Trim());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);

            if (before is null)
            {
                File.Delete(pointer);
            }
            else
            {
                File.WriteAllText(pointer, before);
            }
        }
    }
}
