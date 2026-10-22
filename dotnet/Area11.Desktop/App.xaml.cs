using System.Windows;
using System.Windows.Threading;
using Area11.Core.Models;
using Area11.Core.Utils;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

public partial class App : Application
{
    private AppServices? _services;
    private LoginWindow? _loginWindow;
    private bool _loggingOut;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A WinExe has no console, so an exception that escapes would vanish. Both hooks write a
        // crash file and print the same text on the console the batch file is watching.
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;

        try
        {
            _services = AppServices.Create();
            ShowLogin();
        }
        catch (Exception exception)
        {
            // A second counter that cannot reach the main PC must say so in the shop's own words
            // instead of a bare stack-trace box (F-45): switch the main PC on first.
            var note = ShopConfig.Current.Mode == ShopMode.Second
                ? "This PC is the second counter and could not open the main counter's database. "
                  + "Switch the main PC on, make sure its Area11 folder is shared, then start Area11 here again. "
                  + "Nothing on this PC was changed."
                : "The login window could not be opened.";

            CrashReporter.Report("starting the application", exception, note);
            Shutdown(1);
        }
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CrashReporter.Report("working in the application", e.Exception, "The window that was open has a problem.");

        // The app keeps running: the shops must never lose the till because the dashboard failed.
        e.Handled = true;
    }

    private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        CrashReporter.Report("an unrecoverable error", e.ExceptionObject as Exception);
    }

    private void ShowLogin()
    {
        var viewModel = new LoginViewModel(_services!);
        viewModel.SignedIn += OpenShell;

        _loginWindow = new LoginWindow(viewModel);
        MainWindow = _loginWindow;
        _loginWindow.Show();
    }

    private void OpenShell(User user)
    {
        var viewModel = new ShellViewModel(_services!, user);
        var shell = new ShellWindow { DataContext = viewModel };

        shell.Closed += (_, _) =>
        {
            if (!_loggingOut)
            {
                Shutdown();
            }
        };

        viewModel.LoggedOut += () =>
        {
            _loggingOut = true;
            ShowLogin();
            shell.Close();
            _loggingOut = false;
        };

        _loginWindow?.Close();
        _loginWindow = null;

        MainWindow = shell;
        shell.Show();
    }
}
