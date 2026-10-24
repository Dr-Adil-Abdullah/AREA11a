using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop;

/// <summary>
/// The real entry point, in front of the generated WPF one.
///
/// Why a `Main` of our own: WPF's generated `Main` calls `InitializeComponent()` (which loads
/// Theme.xaml) and `Run()` with no try/catch. If either throws - a misspelt resource key, a missing
/// SQL folder, a broken printer driver - the process closes and the console of a WinExe stays
/// empty. Wrapping both here means every possible startup failure leaves a crash file and a
/// readable box, which is the difference between "it does not start" and a fix.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // F-47: the installer asks this same program what this PC needs, before any window opens.
        // The answer is a plain-text report and an exit code: 0 = go, 2 = a line says STOP,
        // 3 = the report could not be written. See tools\install\install.ps1.
        if (args.Length > 0 && string.Equals(args[0], "--install-check", StringComparison.OrdinalIgnoreCase))
        {
            return InstallService.RunCheck(args);
        }

        // Before the first window: WPF writes a binding fault to the debug output and carries on
        // with an empty box. Nothing on a shop PC can see that stream, so the page just "has no
        // data" - this sends every one of those lines to %TEMP%\Area11\binding-errors.txt, where
        // the owner can send it. Installed before InitializeComponent so Theme.xaml is covered too.
        BindingWatchInstall.Install();

        // A heartbeat, so "no binding-errors file" can be read (v0.34.0, D-90): on 23 Oct the owner
        // was asked to look for that file, found nothing, and that empty folder proved nothing -
        // a clean run and a run that never started look exactly the same. This file is written
        // BEFORE the first window, so its absence now means "the program never started", and its
        // last line means "this is the last thing it reached".
        RunTrail.Begin(BuildInfo.Label + " - started by " + SafeUserName());

        try
        {
            var app = new App();
            app.InitializeComponent();

            var code = app.Run();

            RunTrail.Step("closed - exit code " + code.ToString(System.Globalization.CultureInfo.InvariantCulture));

            return code;
        }
        catch (Exception exception)
        {
            RunTrail.Step("STOPPED before the first window: " + exception.GetType().Name + ": " + exception.Message);

            CrashReporter.Report("starting Area11 (before the first window)", exception,
                "The program closed while Windows was drawing its first window.");

            return 1;
        }
    }

    /// <summary>The Windows account, for the heartbeat - never allowed to throw.</summary>
    private static string SafeUserName()
    {
        try
        {
            return Environment.UserName;
        }
        catch (Exception)
        {
            return "unknown";
        }
    }
}
