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
    public static int Main()
    {
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
