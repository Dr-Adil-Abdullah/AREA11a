using Area11.Core.Utils;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The heartbeat: what this run of the program actually did (v0.34.0, D-90).
///
/// Why it exists: the owner was asked to look for a diagnostics file after a run, found nothing, and
/// that empty folder was treated as evidence - but a run that found no faults and a run that never
/// reached the window leave exactly the same nothing. A file that says "started", "database ready",
/// "signed in", "page: Bills" turns an absence into a readable fact.
/// </summary>
public sealed class RunTrailTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "area11-runtrail", Guid.NewGuid().ToString("N"));
    private readonly string _file;

    public RunTrailTests()
    {
        Directory.CreateDirectory(_folder);
        _file = Path.Combine(_folder, "last-run.txt");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Windows still holds it; a fresh folder is used next time.
        }
    }

    [Fact]
    public void BeginningAFile_WritesTheHeaderAndTheFirstLine()
    {
        var written = RunTrail.Begin("v0.34.0 (Test) - unit test", _file);

        Assert.Equal(_file, written);
        Assert.True(File.Exists(_file));

        var text = File.ReadAllText(_file);

        Assert.Contains("Area11 - what this run did", text);
        Assert.Contains("v0.34.0 (Test) - unit test", text);
        Assert.Contains("started", text);
    }

    [Fact]
    public void EachStep_IsOneTimestampedLine()
    {
        RunTrail.Begin("build", _file);
        RunTrail.Step("database ready", _file);
        RunTrail.Step("signed in: Owner", _file);
        RunTrail.Step("page: Bills", _file);

        var lines = File.ReadAllLines(_file);

        Assert.Contains(lines, line => line.Contains("database ready"));
        Assert.Contains(lines, line => line.Contains("signed in: Owner"));
        Assert.Contains(lines, line => line.Contains("page: Bills"));

        // The newest line is last, because that is the one that explains where the run stopped.
        Assert.Contains("page: Bills", lines[^1]);

        // Every line carries a stamp, so two runs in one day can be told apart.
        Assert.Contains("  ", lines[^1]);
    }

    [Fact]
    public void ABreakInAMessage_StaysOneLine()
    {
        RunTrail.Begin("build", _file);
        RunTrail.Step("first line\r\nsecond line", _file);

        var lines = File.ReadAllLines(_file);

        Assert.Equal(1, lines.Count(line => line.Contains("first line")));
        Assert.Contains(lines, line => line.Contains("first line | second line"));
    }

    [Fact]
    public void AnEmptyMessage_WritesNothing()
    {
        RunTrail.Begin("build", _file);
        var before = RunTrail.CountIn(_file);

        Assert.Equal(string.Empty, RunTrail.Step("   ", _file));
        Assert.Equal(string.Empty, RunTrail.Step(null, _file));
        Assert.Equal(string.Empty, RunTrail.Append(string.Empty, "something"));

        Assert.Equal(before, RunTrail.CountIn(_file));
    }

    [Fact]
    public void TheFileNeverGrowsPastItsCap_AndKeepsTheStartAndTheEnd()
    {
        RunTrail.Begin("build", _file);
        RunTrail.Step("the very first thing", _file);

        for (var index = 0; index < RunTrail.MaxLines * 2; index++)
        {
            RunTrail.Step("step " + index, _file);
        }

        var lines = File.ReadAllLines(_file);

        // A diagnostics file that grows without limit is a diagnostics file that fills a disk on a
        // shop PC that is never switched off; the cap is the feature.
        Assert.True(lines.Length <= RunTrail.MaxLines, $"{lines.Length} lines - past the cap of {RunTrail.MaxLines}");

        // The header survives, because it carries the build and the computer.
        Assert.Contains(lines, line => line.Contains("Area11 - what this run did"));
        Assert.Contains(lines, line => line.Contains("Build   : build"));

        // The newest line survives, because it is the one that explains the run.
        Assert.Contains("step " + ((RunTrail.MaxLines * 2) - 1), lines[^1]);

        // And the file says it dropped something, so nobody reads a gap as "nothing happened".
        Assert.Contains(lines, line => line.Contains("older line(s) dropped"));
    }

    [Fact]
    public void ABadPath_ReturnsEmpty_AndNeverThrows()
    {
        // A FILE used as a FOLDER: Windows refuses to create a directory where a file already sits,
        // and it does so the same way on every machine. (A name with a control character in it was
        // the first draft, but .NET's path rules differ between versions - a test that depends on
        // the framework's mood is not a test.)
        var impossible = Path.Combine(_file, "last-run.txt");

        File.WriteAllText(_file, "this is a file, not a folder");

        Assert.Equal(string.Empty, RunTrail.Begin("build", impossible));
        Assert.Equal(string.Empty, RunTrail.Step("a step", impossible));
        Assert.Equal(0, RunTrail.CountIn(impossible));
    }

    [Fact]
    public void TheRealPath_IsUnderTheTempArea11Folder()
    {
        // The shop folder can be the second counter's share; diagnostics never go there.
        Assert.StartsWith(Path.GetTempPath(), RunTrail.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("Area11", "last-run.txt"), RunTrail.FilePath, StringComparison.OrdinalIgnoreCase);
    }
}
