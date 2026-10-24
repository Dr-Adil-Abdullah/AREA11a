using Area11.Core.Utils;
using System;
using System.IO;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// One broken page must not take the till down (v0.32.0, D-88).
///
/// Why: every menu row builds its view-model with `new SomeViewModel(...)`, and those constructors
/// read the shop's database. One failed read used to throw out of that constructor and into the
/// dispatcher while the shell was still being built - which is how the window died on 22 Oct 2026,
/// and the same accident at sign-in would leave the shop with no counter at all.
///
/// The promises that must hold:
///   • a page that builds is handed back untouched;
///   • a page that throws comes back as a **fault**, never as an exception (the guard must not
///     become the second fault);
///   • the fault names the page, keeps the exception's own words, and tells the operator that the
///     rest of the program still works;
///   • the exception is logged, and handed to the recorder the caller gave - evidence, not silence;
///   • a page that comes back empty is a fault too, because an empty frame reads as a broken screen.
/// </summary>
public sealed class PageGuardTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "area11-pageguard", Guid.NewGuid().ToString("N"));

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

    private sealed class FakePage
    {
        public string Title { get; init; } = "a page";
    }

    private LogService NewLog() => new(_folder);

    [Fact]
    public void APageThatBuilds_IsHandedBackUntouched()
    {
        var page = new FakePage { Title = "the counter" };

        var opened = PageGuard.Open("Counter", () => page, NewLog());

        Assert.True(opened.Ok);
        Assert.Same(page, opened.Page);
        Assert.Null(opened.Fault);
    }

    [Fact]
    public void APageThatThrows_ComesBackAsAFault_NotAsAnException()
    {
        var opened = PageGuard.Open<FakePage>(
            "Bills",
            () => throw new InvalidOperationException("no such table: sales"),
            NewLog());

        // The one thing this method must never do: throw. If it did, the guard would be the crash.
        Assert.False(opened.Ok);
        Assert.Null(opened.Page);
        Assert.NotNull(opened.Fault);
    }

    [Fact]
    public void TheFault_NamesThePage_AndKeepsTheExceptionsOwnWords()
    {
        var fault = PageGuard.Open<FakePage>(
            "Bills",
            () => throw new InvalidOperationException("no such table: sales"),
            NewLog()).Fault!;

        Assert.Contains("Bills", fault.Sentence);
        Assert.Contains("could not be opened", fault.Sentence);

        // The exception's own words, not a polite rewrite: the owner has to be able to send this.
        Assert.Contains("InvalidOperationException", fault.Detail);
        Assert.Contains("no such table: sales", fault.Detail);
    }

    [Fact]
    public void TheFault_SaysTheRestOfTheShopStillWorks()
    {
        var fault = PageGuard.Open<FakePage>(
            "Purchases",
            () => throw new ApplicationException("disk is full"),
            NewLog()).Fault!;

        // The first thing the operator needs to know is that he has not lost the day's billing.
        Assert.Contains("Nothing else is affected", fault.Advice);
        Assert.Contains("SEND-REPORT", fault.Advice);
    }

    [Fact]
    public void TheException_IsWrittenToTheLog()
    {
        var log = NewLog();

        PageGuard.Open<FakePage>(
            "Stock take",
            () => throw new InvalidOperationException("locked database"),
            log);

        var files = Directory.GetFiles(_folder, "app-*.log");

        Assert.NotEmpty(files);

        var text = string.Concat(files.Select(File.ReadAllText));

        Assert.Contains("Stock take", text);
        Assert.Contains("locked database", text);
        Assert.Contains("[ERROR]", text);
    }

    [Fact]
    public void TheCaller_IsToldOnceWithThePageAndTheException()
    {
        var told = new List<(string Page, string Message)>();

        PageGuard.Open<FakePage>(
            "Reports",
            () => throw new InvalidOperationException("bad column"),
            NewLog(),
            (page, exception) =>
            {
                told.Add((page, exception.Message));
                return string.Empty;
            });

        var single = Assert.Single(told);

        Assert.Equal("Reports", single.Page);
        Assert.Equal("bad column", single.Message);
    }

    [Fact]
    public void APageThatComesBackEmpty_IsAFaultToo()
    {
        var opened = PageGuard.Open<FakePage>("Settings", () => null!, NewLog());

        // An empty frame with no message is the fault the owner reports as "the page is blank".
        Assert.False(opened.Ok);
        Assert.Contains("Settings", opened.Fault!.Sentence);
    }

    [Fact]
    public void TheReportPath_TheRecorderWrote_IsCarriedOnTheCard()
    {
        var fault = PageGuard.Open<FakePage>(
            "Bills",
            () => throw new InvalidOperationException("bad column"),
            NewLog(),
            (_, _) => @"C:\ProgramData\Area11\logs\crash-2026-10-23_101010.txt").Fault!;

        // The card can then name the file, so the owner sends the right one.
        Assert.True(fault.HasReport);
        Assert.Contains("crash-2026-10-23", fault.ReportPath);
    }

    [Fact]
    public void TheCard_NamesTheReportFile_WhenThereIsOne()
    {
        var fault = PageGuard.Open<FakePage>(
            "Bills",
            () => throw new InvalidOperationException("bad column"),
            NewLog(),
            (_, _) => @"C:\ProgramData\Area11\logs\crash-2026-10-23_101010.txt").Fault!;

        Assert.Contains(@"C:\ProgramData\Area11\logs\crash-2026-10-23_101010.txt", fault.ReportLabel);
    }

    [Fact]
    public void TheCard_PointsAtTheLogFolderAndTheTool_WhenTheReportCouldNotBeWritten()
    {
        var fault = PageGuard.Open<FakePage>(
            "Bills",
            () => throw new InvalidOperationException("bad column"),
            NewLog(),
            (_, _) => string.Empty).Fault!;

        // A recorder that returns nothing is a real case: CrashLog gives up silently when every
        // folder refuses the write. The card must still say where to look and what to run.
        Assert.False(fault.HasReport);
        Assert.Contains("log folder", fault.ReportLabel);
        Assert.Contains("SEND-REPORT", fault.ReportLabel);
    }

    [Fact]
    public void TheCard_NeverPrintsADoubledBackslash()
    {
        var fault = PageGuard.Open<FakePage>(
            "Bills",
            () => throw new InvalidOperationException("bad column"),
            NewLog(),
            (_, _) => @"C:\ProgramData\Area11\logs\crash-2026-10-23_101010.txt").Fault!;
        var noReport = PageGuard.Open<FakePage>(
            "Bills",
            () => throw new InvalidOperationException("bad column"),
            NewLog()).Fault!;

        // The card's first draft printed a doubled separator before ProgramData: the literal in the
        // source was escaped a second time, so the operator was shown a path that does not exist -
        // and a path he cannot copy is a path he cannot use. Every line of the card is checked,
        // because the two are written in different places and only one of them would have been read.
        Assert.DoesNotContain(@"\\", fault.ReportLabel);
        Assert.DoesNotContain(@"\\", noReport.ReportLabel);
        Assert.DoesNotContain(@"\\", noReport.Advice);
    }

    [Fact]
    public void ARecorderThatThrowsSomethingUnexpected_StillShowsTheCard_WithTheRealFault()
    {
        var opened = PageGuard.Open<FakePage>(
            "Bills",
            () => throw new InvalidOperationException("bad column"),
            NewLog(),
            (_, _) => throw new Exception("a fault nobody predicted"));

        // The list of file errors a crash recorder is expected to raise is not the whole world. The
        // guard's promise is absolute, so the catch is too - and the card still carries the fault
        // that actually happened, not the one the recorder raised while trying to describe it.
        Assert.False(opened.Ok);
        Assert.Contains("bad column", opened.Fault!.Detail);
    }

    [Fact]
    public void APageThatFailsWhileDrawing_SaysThat_InsteadOfCouldNotBeOpened()
    {
        var fault = PageGuard.Drawing(
            "Counter (POS)",
            new InvalidOperationException("the template could not be created"),
            NewLog());

        // Two different accidents, two different sentences: the page was built (so its data was
        // read) and the drawing is what failed. Telling the operator it "could not be opened" would
        // send him looking for a data fault that is not there.
        Assert.True(fault.WhileDrawing);
        Assert.Contains("stopped while its screen was being drawn", fault.Sentence);
        Assert.DoesNotContain("could not be opened", fault.Sentence);
        Assert.Contains("Counter (POS)", fault.Sentence);
    }

    [Fact]
    public void TheDrawingFault_KeepsTheExceptionsOwnWords_AndIsLogged()
    {
        var log = NewLog();

        var fault = PageGuard.Drawing("Bills", new InvalidOperationException("converter failed"), log);

        Assert.Contains("InvalidOperationException", fault.Detail);
        Assert.Contains("converter failed", fault.Detail);

        var text = string.Concat(Directory.GetFiles(_folder, "app-*.log").Select(File.ReadAllText));

        Assert.Contains("[ERROR]", text);
        Assert.Contains("Bills", text);
        Assert.Contains("converter failed", text);
    }

    [Fact]
    public void TheDrawingFault_NamesTheReportFile()
    {
        var fault = PageGuard.Drawing(
            "Reports",
            new InvalidOperationException("bad template"),
            NewLog(),
            (_, _) => @"C:\ProgramData\Area11\logs\crash-2026-10-23_111111.txt");

        Assert.True(fault.HasReport);
        Assert.Contains("crash-2026-10-23", fault.ReportLabel);
    }

    [Fact]
    public void ADrawingFaultWithNoException_StillExplainsItself()
    {
        // WPF can report a frame fault with nothing attached; a card with an empty detail line
        // would look like a bug in the card.
        var fault = PageGuard.Drawing("Alerts", null, NewLog());

        Assert.True(fault.WhileDrawing);
        Assert.Contains("without telling the program why", fault.Detail);
    }

    [Fact]
    public void ARecorderThatThrows_WhileDrawing_DoesNotThrow()
    {
        var fault = PageGuard.Drawing(
            "Share",
            new InvalidOperationException("gone"),
            NewLog(),
            (_, _) => throw new Exception("the recorder failed too"));

        // Drawing runs while the program is already dealing with a fault; it must not add one.
        Assert.True(fault.WhileDrawing);
        Assert.Contains("gone", fault.Detail);
    }

    [Fact]
    public void ARecorderThatItselfFails_DoesNotStopTheCard()
    {
        var opened = PageGuard.Open<FakePage>(
            "Audit",
            () => throw new InvalidOperationException("gone"),
            NewLog(),
            (_, _) => throw new IOException("the disk went away"));

        // A broken recorder must not turn a page fault into a crash.
        Assert.False(opened.Ok);
        Assert.Contains("Audit", opened.Fault!.Sentence);
    }
}
