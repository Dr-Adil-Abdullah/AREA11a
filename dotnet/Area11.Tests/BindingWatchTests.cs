using Area11.Core.Utils;
using System.IO;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The file that explains a blank column (v0.31.2, D-87).
///
/// Why: a WPF binding that names a property which does not exist does **not** throw. It writes one
/// line to the debug output - which no shop PC shows - and leaves the box empty, so the owner
/// reports "the page has no data" and the fault has to be found by reading the XAML by eye. The
/// desktop program now sends those lines here.
///
/// The promises that must hold:
///   • one complaint is one line, with a timestamp, whatever the framework passed in;
///   • blank text writes nothing, so the file only holds real complaints;
///   • the file never grows without end - it is set aside as `.old` before it becomes unreadable;
///   • a path that cannot be written returns quietly: a watcher may never become a second fault.
/// </summary>
public sealed class BindingWatchTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "area11-bindwatch", Guid.NewGuid().ToString("N"));

    private string File_ => Path.Combine(_folder, "binding-errors.txt");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Windows still holds it; the next run uses a fresh folder.
        }
    }

    [Fact]
    public void OneComplaint_IsOneLine_WithTheTimeItHappened()
    {
        var written = BindingWatch.Append(File_, "BindingExpression path error: 'QtyLabel' property not found");

        Assert.Equal(File_, written);

        var lines = File.ReadAllLines(File_);

        Assert.Single(lines);
        Assert.Contains("QtyLabel", lines[0]);

        // yyyy-MM-dd HH:mm:ss is the first 19 characters - a file without times cannot be lined up
        // against a crash report from the same minute.
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}  ", lines[0]);
    }

    [Fact]
    public void BlankText_WritesNothingAtAll()
    {
        Assert.Equal(string.Empty, BindingWatch.Append(File_, null));
        Assert.Equal(string.Empty, BindingWatch.Append(File_, "   "));
        Assert.Equal(string.Empty, BindingWatch.Append(string.Empty, "something"));

        // Not even an empty file: "the file exists" must mean "something was noted".
        Assert.False(File.Exists(File_));
    }

    [Fact]
    public void AValueWithALineBreak_StillCountsAsOneEntry()
    {
        BindingWatch.Append(File_, "first line\r\nsecond line");

        var lines = File.ReadAllLines(File_);

        Assert.Single(lines);
        Assert.Contains("first line", lines[0]);
        Assert.Contains("second line", lines[0]);
    }

    [Fact]
    public void AVeryLongComplaint_IsClippedNotSwallowed()
    {
        var message = new string('x', BindingWatch.MaxMessageLength + 500);

        BindingWatch.Append(File_, message);

        var line = Assert.Single(File.ReadAllLines(File_));

        // The time stamp and the two spaces are in front; the rest is the clip and its marker.
        Assert.Contains(" ...", line);
        Assert.True(line.Length < message.Length);
        Assert.True(line.Length <= BindingWatch.MaxMessageLength + 40);
    }

    [Fact]
    public void AFatFile_IsSetAside_SoTheFirstFaultIsNeverLost()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(File_, new string('x', BindingWatch.MaxBytes + 10));

        BindingWatch.Append(File_, "the new one");

        Assert.True(File.Exists(File_ + ".old"));
        Assert.True(new FileInfo(File_ + ".old").Length > BindingWatch.MaxBytes);

        var fresh = File.ReadAllLines(File_);

        Assert.Single(fresh);
        Assert.Contains("the new one", fresh[0]);
    }

    [Fact]
    public void APathThatCannotBeWritten_IsNotAFault()
    {
        // A FILE used as if it were a folder: Directory.CreateDirectory cannot, and the watcher
        // must return quietly instead of throwing into the layout pass.
        Directory.CreateDirectory(_folder);

        var blocker = Path.Combine(_folder, "blocker");
        File.WriteAllText(blocker, "not a folder");

        var impossible = Path.Combine(blocker, "deeper", "binding-errors.txt");

        Assert.Equal(string.Empty, BindingWatch.Append(impossible, "should not throw"));
        Assert.Equal(0, BindingWatch.CountIn(impossible));
    }

    [Fact]
    public void TheFilePathIsSomewhereTheShopFolderCannotRefuse()
    {
        Assert.StartsWith(Path.GetTempPath(), BindingWatch.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("binding-errors.txt", BindingWatch.FilePath, StringComparison.OrdinalIgnoreCase);

        // The shop folder can be the second counter's share; diagnostics may never depend on it.
        Assert.DoesNotContain("area11.db", BindingWatch.FilePath, StringComparison.OrdinalIgnoreCase);
    }
}
