namespace Area11.Core.Utils;

/// <summary>
/// What one button did: finished, or failed in a way the shop can read (v0.34.0, D-90).
///
/// The sentence is deliberately careful about money: it does **not** say "nothing was saved",
/// because a command can fail after its transaction committed. It says what is certainly true - the
/// page is still open, the rest of the shop is untouched, and what to do next.
/// </summary>
public sealed class CommandOutcome
{
    /// <summary>The button, named by the method behind it ("Save", "PrintBill").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The exception's own name and message, or empty when the command finished.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>Where the crash report went, or empty when none was needed or none could be written.</summary>
    public string ReportPath { get; init; } = string.Empty;

    public bool Ok => string.IsNullOrEmpty(Detail);

    /// <summary>
    /// What the window shows above the page. One line, in the shop's words.
    ///
    /// A button whose method name cannot be read is not called "the .ctor button": the compiler
    /// names a lambda after the method that made it, so some of them would read as the constructor.
    /// Those say "a button on this page" - still true, and the frame adds which page it was.
    /// </summary>
    public string Sentence =>
        (HasName ? "The \"" + Name + "\" button" : "A button on this page")
        + " could not finish. The page is still open and the rest of the shop is untouched - try it "
        + "again; if it fails twice, run tools\\SEND-REPORT.cmd and send that one file.";

    /// <summary>True when the button's method gave it a readable name.</summary>
    public bool HasName => Name.Length > 0 && !string.Equals(Name, "button", StringComparison.Ordinal);

    public bool HasReport => !string.IsNullOrWhiteSpace(ReportPath);
}

/// <summary>
/// One click must not take the counter down (v0.34.0, D-90).
///
/// Why: every page's buttons run through `RelayCommand`, and a `RelayCommand.Execute` that throws
/// lands on the dispatcher. Until v0.33.0 that meant a crash report and a window that may close; in
/// v0.33.0 the dispatcher replaces the whole page with a fault card - which is right for a page that
/// cannot be built, but far too heavy for one failed Save. So a command now fails **inside its own
/// button**: the fault is logged, the crash report is written, the page stays exactly as it was, and
/// one line above it says what happened and what to do.
///
/// The exception is never swallowed: it always reaches the log, the crash file and the caller. What
/// it does not do is reach the dispatcher.
/// </summary>
public static class CommandGuard
{
    /// <summary>
    /// Where the log line goes. Set once at startup by `App` (the desktop owns the log file); left
    /// null in a test, which is why every call below tolerates it.
    /// </summary>
    public static Action<string, Exception?>? Sink { get; set; }

    /// <summary>
    /// Raised after a failed command, so the window can show the line. The frame subscribes; a
    /// program without a frame (the tests) simply never hears it.
    /// </summary>
    public static event Action<CommandOutcome>? Failed;

    /// <summary>
    /// Runs one command. **Never throws** - that is its whole purpose, so the one thing it must not
    /// do is fail a second time.
    /// </summary>
    public static CommandOutcome Run(
        string name,
        Action? action,
        Func<string, Exception, string>? record = null)
    {
        var button = string.IsNullOrWhiteSpace(name) ? "button" : name;

        if (action is null)
        {
            return new CommandOutcome
            {
                Name = button,
                Detail = "This button is not connected to anything in the program yet."
            };
        }

        try
        {
            action();
            return new CommandOutcome { Name = button };
        }
        catch (Exception exception)
        {
            var path = string.Empty;

            try
            {
                Sink?.Invoke("The \"" + button + "\" button could not finish.", exception);
            }
            catch (Exception)
            {
                // A log that fails must not become the second fault - the same rule as PageGuard.
            }

            try
            {
                // Without a recorder the fault would have no file at all, so the default is the
                // crash report the owner already knows how to send.
                var written = record is null
                    ? CrashLog.Write("the \"" + button + "\" button", exception,
                        "The page stayed open; the rest of the program kept working.")
                    : record(button, exception);

                if (!string.IsNullOrWhiteSpace(written))
                {
                    path = written!;
                }
            }
            catch (Exception)
            {
                // The card/line matters more than the file.
            }

            var outcome = new CommandOutcome
            {
                Name = button,
                Detail = exception.GetType().Name + ": " + exception.Message,
                ReportPath = path
            };

            try
            {
                Failed?.Invoke(outcome);
            }
            catch (Exception)
            {
                // A subscriber that throws must not turn one failed button into two.
            }

            return outcome;
        }
    }

    /// <summary>
    /// A name for a button taken from the method behind it, cleaned of the compiler's generated
    /// lambda names (`&lt;Save&gt;b__12_0` becomes `Save`) - so the sentence, the log line and the
    /// crash file all name the same thing without a hundred call sites being edited.
    /// </summary>
    public static string NameOf(Delegate? action)
    {
        var raw = action?.Method.Name ?? string.Empty;

        if (raw.Length == 0)
        {
            return "button";
        }

        if (raw[0] == '<')
        {
            var end = raw.IndexOf('>');

            if (end > 1)
            {
                var inside = raw[1..end];

                // `<Load>b__5_0` is a lambda inside Load - a real name. `<.ctor>b__12_0` is a lambda
                // inside a constructor, which is every command made in a page's constructor: those
                // have no honest name, so they say "a button on this page" instead of ".ctor".
                return inside.Length > 0 && char.IsLetter(inside[0]) ? inside : "button";
            }
        }

        return raw;
    }
}
