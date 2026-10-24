using Area11.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// One failed button must not take the counter down (v0.34.0, D-90).
///
/// Why: every page's buttons run through `RelayCommand`, and a command that throws used to land on
/// the dispatcher - a crash report, and either a closed window or (from v0.33.0) the whole page
/// replaced by a fault card. Neither is right for one failed Save: the operator loses the half-typed
/// bill for a fault that had nothing to do with it.
/// </summary>
public sealed class CommandGuardTests
{
    /// <summary>
    /// The recorder the tests hand to the guard instead of the real crash file.
    ///
    /// Why: a test must never write into the shop's own crash folder. `tools\SEND-REPORT.cmd` prints
    /// the **newest** crash report, so a file written by a test run would be handed to the owner as
    /// the shop's fault, and he would go looking for a fault that never happened. The real default
    /// (`CrashLog.Write`) is pinned by gate #42; the test below proves the contract with its own file.
    /// </summary>
    private static string Quiet(string name, Exception exception) => string.Empty;

    [Fact]
    public void ACommandThatFinishes_IsOk_AndSaysNothing()
    {
        var ran = false;

        var outcome = CommandGuard.Run("Save", () => ran = true);

        Assert.True(ran);
        Assert.True(outcome.Ok);
        Assert.Equal(string.Empty, outcome.Detail);
        Assert.Equal("Save", outcome.Name);
    }

    [Fact]
    public void ACommandThatThrows_ComesBackAsAFault_NotAsAnException()
    {
        var outcome = CommandGuard.Run("Print bill", () => throw new InvalidOperationException("no printer"), Quiet);

        // The one thing this method must never do: throw. If it did, the guard would be the crash.
        Assert.False(outcome.Ok);
        Assert.Contains("InvalidOperationException", outcome.Detail);
        Assert.Contains("no printer", outcome.Detail);
    }

    [Fact]
    public void TheSentence_NamesTheButton_AndNeverPromisesNothingWasSaved()
    {
        var outcome = CommandGuard.Run("Save", () => throw new ApplicationException("deadlock"), Quiet);

        Assert.Contains("\"Save\" button", outcome.Sentence);
        Assert.Contains("page is still open", outcome.Sentence);
        Assert.Contains("SEND-REPORT", outcome.Sentence);

        // A command can fail AFTER its transaction committed. Telling the shop "nothing was saved"
        // would be a lie, and a lie about money is the worst kind this program can tell.
        Assert.DoesNotContain("nothing was saved", outcome.Sentence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AButtonWithNoReadableName_SaysSoInsteadOfNamingTheConstructor()
    {
        var outcome = CommandGuard.Run("button", () => throw new InvalidOperationException("no printer"), Quiet);

        // The compiler names a lambda after the method that made it; a command made in a page's
        // constructor would otherwise read `The ".ctor" button could not finish`.
        Assert.False(outcome.HasName);
        Assert.StartsWith("A button on this page", outcome.Sentence);
    }

    [Fact]
    public void EveryFailingCommand_LeavesACrashReport()
    {
        // The guard's contract is "the file the recorder wrote comes back, so the banner can name it".
        // The recorder here writes into the test's own temp folder and not the shop's crash folder -
        // see `Quiet` above for why that matters.
        var folder = Path.Combine(Path.GetTempPath(), "area11-guard", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "crash-guard.txt");
        Directory.CreateDirectory(folder);

        var outcome = CommandGuard.Run("Save",
            () => throw new InvalidOperationException("disk is full"),
            (name, exception) =>
            {
                File.WriteAllText(file, name + Environment.NewLine + exception.GetType().Name +
                                        ": " + exception.Message);
                return file;
            });

        // A failed button with no file behind it is a dead end: the owner is asked to send a report.
        Assert.True(outcome.HasReport);
        Assert.Equal(file, outcome.ReportPath);
        Assert.True(File.Exists(file));

        var written = File.ReadAllText(file);

        Assert.Contains("Save", written);
        Assert.Contains("disk is full", written);

        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Windows may still hold the handle; the temp folder is not the shop's and is swept later.
        }
    }

    [Fact]
    public void TheLogSink_HearsAboutItOnce()
    {
        var lines = new List<string>();
        var previous = CommandGuard.Sink;
        CommandGuard.Sink = (message, exception) => lines.Add(message + " :: " + exception?.Message);

        try
        {
            CommandGuard.Run("Save", () => throw new InvalidOperationException("locked"), Quiet);

            var single = Assert.Single(lines);

            Assert.Contains("Save", single);
            Assert.Contains("locked", single);
        }
        finally
        {
            CommandGuard.Sink = previous;
        }
    }

    [Fact]
    public void ALogSinkThatItselfThrows_DoesNotStopTheFaultComingBack()
    {
        var previous = CommandGuard.Sink;
        CommandGuard.Sink = (_, _) => throw new IOException("the log file went away");

        try
        {
            var outcome = CommandGuard.Run("Save", () => throw new InvalidOperationException("locked"), Quiet);

            Assert.False(outcome.Ok);
            Assert.Contains("locked", outcome.Detail);
        }
        finally
        {
            CommandGuard.Sink = previous;
        }
    }

    [Fact]
    public void TheWindow_IsToldOnce_AndOnlyOnFailure()
    {
        var heard = new List<string>();
        void Listener(CommandOutcome outcome) => heard.Add(outcome.Name);

        CommandGuard.Failed += Listener;

        try
        {
            CommandGuard.Run("Save", () => { });
            CommandGuard.Run("Print", () => throw new InvalidOperationException("no printer"), Quiet);

            var single = Assert.Single(heard);

            Assert.Equal("Print", single);
        }
        finally
        {
            CommandGuard.Failed -= Listener;
        }
    }

    [Fact]
    public void ASubscriberThatThrows_DoesNotStopTheFaultComingBack()
    {
        void Listener(CommandOutcome outcome) => throw new ApplicationException("the window failed too");

        CommandGuard.Failed += Listener;

        try
        {
            var outcome = CommandGuard.Run("Save", () => throw new InvalidOperationException("locked"), Quiet);

            Assert.False(outcome.Ok);
            Assert.Contains("locked", outcome.Detail);
        }
        finally
        {
            CommandGuard.Failed -= Listener;
        }
    }

    [Fact]
    public void AButtonWithNothingBehindIt_IsAFaultToo()
    {
        var outcome = CommandGuard.Run("Save", null);

        // A button wired to nothing used to do nothing at all, which reads as "the program ignored
        // my click". It now says so, in the same words as any other fault.
        Assert.False(outcome.Ok);
        Assert.Contains("not connected", outcome.Detail);
    }

    [Fact]
    public void ALambdaIsNamedAfterItsEnclosingMethod()
    {
        // Zero call-site churn: a hundred buttons already say `new RelayCommand(SaveSomething)`, and
        // the sentence names the method without anybody editing them.
        Assert.Equal("SaveSomething", CommandGuard.NameOf((Action)SaveSomething));

        // A lambda is named `<TheMethodThatMadeIt>b__0_1` by the compiler, so the readable half is
        // the method around it - not the method it calls. The name is honest either way; the
        // alternative (a blank) would leave the banner unable to say which button failed.
        Assert.Equal("ALambdaIsNamedAfterItsEnclosingMethod",
            CommandGuard.NameOf((Action)(() => Load())));
    }

    [Fact]
    public void ACommandMadeInAConstructor_HasNoNameToSay()
    {
        // Every page builds its commands in its constructor, so the compiler names those lambdas
        // `<.ctor>b__12_0`. Printing that to a shopkeeper would be worse than saying "a button".
        Assert.Equal("button", CommandGuard.NameOf(new ButtonHolder().Command));
    }

    [Fact]
    public void NoNameAtAll_IsStillAUsableSentence()
    {
        Assert.Equal("button", CommandGuard.NameOf(null));

        var outcome = CommandGuard.Run(string.Empty, () => throw new InvalidOperationException("x"), Quiet);

        Assert.False(outcome.HasName);
        Assert.Contains("could not finish", outcome.Sentence);
    }

    /// <summary>A stand-in for a page: its command is made inside the constructor, like every page.</summary>
    private sealed class ButtonHolder
    {
        public ButtonHolder()
        {
            Command = () => Load();
        }

        public Action Command { get; }
    }

    private static void SaveSomething()
    {
    }

    private static int Load() => 0;
}
