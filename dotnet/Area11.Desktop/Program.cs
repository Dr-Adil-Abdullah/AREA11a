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

        // D-76: `Area11.exe --demo` runs the DEMO shop, in its own folder, so the owner can look at
        // two and a half months of invented bills without one invented rupee ever reaching his own
        // database. It must be decided BEFORE the first window, because every path below (database,
        // photos, logs, backups) reads AppPaths.DataRoot - and that is now the demo's folder.
        if (DemoShop.WantsDemo(args))
        {
            DemoShop.TurnOn(AppPaths.RealDataRoot);
        }

        try
        {
            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
        catch (Exception exception)
        {
            CrashReporter.Report("starting Area11 (before the first window)", exception,
                "The program closed while Windows was drawing its first window.");

            return 1;
        }
    }
}
