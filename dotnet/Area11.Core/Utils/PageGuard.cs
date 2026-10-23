namespace Area11.Core.Utils;

/// <summary>
/// One page that could not be opened, in words the shop can act on (v0.32.0, D-88).
/// </summary>
public sealed class PageFault
{
    /// <summary>The menu row the operator pressed, as it is written on the screen.</summary>
    public string PageTitle { get; init; } = string.Empty;

    /// <summary>The exception's own name and message - what the fault really was.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>Where the full detail was written, so it can be sent.</summary>
    public string ReportPath { get; init; } = string.Empty;

    /// <summary>The headline. It names the page, because "something went wrong" names nothing.</summary>
    public string Sentence =>
        (string.IsNullOrWhiteSpace(PageTitle) ? "This page" : "The " + PageTitle + " page")
        + " could not be opened.";

    /// <summary>
    /// What to do next - and the first half is the part that matters: **nothing else is broken**.
    /// The rest of the program keeps working, and that has to be said, or the operator closes the
    /// program and loses the till for the day.
    /// </summary>
    public string Advice =>
        "Nothing else is affected: the other pages, the counter and the shop's figures are untouched. "
        + "Press another page, and try this one again in a minute - a read that failed once often "
        + "works on the second try. If it fails again, run tools\\SEND-REPORT.cmd and send that one "
        + "file; it carries the whole detail.";

    public bool HasReport => !string.IsNullOrWhiteSpace(ReportPath);

    /// <summary>
    /// The line under the advice that says **where the full detail is**. It is on the card for one
    /// reason: the owner is asked to send a file, and a request for "the log" makes him hunt for it.
    ///
    /// Written here rather than in the window so that a test can read it - a path typed into a
    /// view-model is a path nobody ever checks, and this line's first draft printed its separators
    /// twice because the literal in the source had been escaped a second time.
    /// </summary>
    public string ReportLabel => HasReport
        ? "The full detail was written to: " + ReportPath
        : "The full detail is in the shop's log folder (" + LogsFolderForReading()
          + ") - run tools\\SEND-REPORT.cmd and it collects the report for you.";

    /// <summary>
    /// The log folder, or the usual place if this machine will not answer. A label is never worth an
    /// exception: the card exists to explain a failure, not to become one.
    /// </summary>
    private static string LogsFolderForReading()
    {
        try
        {
            return AppPaths.LogsFolder;
        }
        catch (Exception)
        {
            // Everything, for the same reason as the guard itself: this text is read while a fault
            // is on the screen, and a second fault there would leave the operator with a blank line
            // where the path should be.
            return @"C:\ProgramData\Area11\logs";
        }
    }
}

/// <summary>The answer to "open this page": either the page, or the fault that stopped it.</summary>
public sealed class PageOpen<T> where T : class
{
    private PageOpen(T? page, PageFault? fault)
    {
        Page = page;
        Fault = fault;
    }

    /// <summary>The page, ready to show. Null when it could not be built.</summary>
    public T? Page { get; }

    /// <summary>What went wrong, in the shop's words. Null when the page opened.</summary>
    public PageFault? Fault { get; }

    public bool Ok => Page is not null && Fault is null;

    public static PageOpen<T> Success(T page) => new(page, null);

    public static PageOpen<T> Failed(PageFault fault) => new(null, fault);
}

/// <summary>
/// Opens a page so that **one broken page cannot take the till down** (v0.32.0, D-88).
///
/// Why: every menu row builds its view-model with `new SomeViewModel(...)`, and those constructors
/// read the database. A single failed read - one odd row, one missing table in an old copy of the
/// file - throws out of the constructor, and that exception lands in the dispatcher while the window
/// is still being built. On 22 Oct 2026 a XAML fault did exactly that and the program closed; the
/// cheap version of the same accident would be a page that takes sign-in with it, so the operator
/// cannot even reach the counter.
///
/// **What this catches:** anything the page's own construction throws. **What it does not:** a fault
/// inside the XAML view itself, which WPF raises later when it draws the template. That one is
/// covered by the crash reporter and by the binding gates, not by this guard - and the card says so
/// by telling the operator to send the report, not by pretending nothing happened.
///
/// The exception is never swallowed: it is logged (the shop's log file) and handed to the caller's
/// recorder, which writes the crash report the owner can attach.
/// </summary>
public static class PageGuard
{
    /// <summary>
    /// Builds the page, or returns the fault. **This method never throws** - that is its whole
    /// purpose, so the one thing it must not do is fail a second time.
    /// </summary>
    public static PageOpen<T> Open<T>(
        string pageTitle,
        Func<T> build,
        LogService? log = null,
        Func<string, Exception, string>? record = null) where T : class
    {
        try
        {
            return Attempt(pageTitle, build, log, record);
        }
        catch (Exception unexpected)
        {
            // The last line of defence, and the reason this method can keep its promise. Attempt()
            // is not supposed to throw; if it ever does - a logger that fails in a way nobody
            // predicted, an empty report path that turns out to be null - the shop still gets a
            // card instead of a dead window, which is the whole point of this class.
            return PageOpen<T>.Failed(new PageFault
            {
                PageTitle = pageTitle,
                Detail = unexpected.GetType().Name + ": " + unexpected.Message
            });
        }
    }

    /// <summary>The guarded body. Only <see cref="Open{T}"/> may call it.</summary>
    private static PageOpen<T> Attempt<T>(
        string pageTitle,
        Func<T> build,
        LogService? log,
        Func<string, Exception, string>? record) where T : class
    {
        if (build is null)
        {
            return PageOpen<T>.Failed(new PageFault
            {
                PageTitle = pageTitle,
                Detail = "The program asked for this page with nothing to build it from."
            });
        }

        try
        {
            var page = build();

            if (page is null)
            {
                // A page that builds nothing is the same as a page that failed: the operator would
                // get an empty frame and no reason.
                return PageOpen<T>.Failed(new PageFault
                {
                    PageTitle = pageTitle,
                    Detail = "The page was built but came back empty."
                });
            }

            return PageOpen<T>.Success(page);
        }
        catch (Exception exception)
        {
            // Deliberately broad: the alternative is the shop losing the till because one query on
            // one page met one bad row. OutOfMemoryException is not a reason to stop guarding
            // either - after it, the only useful thing left is to keep the window open.
            var path = string.Empty;

            try
            {
                log?.Error("The " + pageTitle + " page could not be opened.", exception);
            }
            catch (Exception)
            {
                // Deliberately everything, not a list of file errors: a logger that fails in a way
                // nobody predicted - a formatter, a closed handle, a full disk reported as
                // something else - must not become the second fault on the screen. The card is
                // more important than the log line.
            }

            try
            {
                // The recorder writes the crash file and returns where it went, so the card can
                // name it. It must not show a box: the card is already on the screen, and a second
                // window on top of a failure is how an operator learns to click "OK" without reading.
                var written = record?.Invoke(pageTitle, exception);

                if (!string.IsNullOrWhiteSpace(written))
                {
                    path = written!;
                }
            }
            catch (Exception)
            {
                // Same rule, and the same word: everything. A crash reporter that throws is worse
                // than no crash reporter, because it takes the explanation with it.
            }

            return PageOpen<T>.Failed(new PageFault
            {
                PageTitle = pageTitle,
                Detail = exception.GetType().Name + ": " + exception.Message,
                ReportPath = path
            });
        }
    }
}
