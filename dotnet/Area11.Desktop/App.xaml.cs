using System.Windows;
using System.Windows.Input;
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
    private SingleInstanceGuard? _instance;

    /// <summary>
    /// Portion 16: true once Windows announced it is going down. The protected-close prompt must
    /// never block an OS shutdown - the honest limit is that it cannot, so it does not try.
    /// </summary>
    internal static bool IsSystemShutdown { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One shop, one copy (owner's word, Nov 2026): asked FIRST, before the database or any
        // window exists, so the refused copy leaves nothing behind - no window, no log line it
        // would have to explain, and no second writer on the shop's file.
        _instance = SingleInstanceGuard.TryAcquire(SingleInstanceGuard.ApplicationName);

        if (!_instance.IsFirst)
        {
            _instance.Dispose();
            _instance = null;

            MessageBox.Show(
                "Area11 is already running on this Windows account. Use the open window - " +
                "a second copy is not allowed, because two copies would sell from the same shelf.",
                "Area11", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // A WinExe has no console, so an exception that escapes would vanish. Both hooks write a
        // crash file and print the same text on the console the batch file is watching.
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        SessionEnding += (_, _) => IsSystemShutdown = true;

        try
        {
            _services = AppServices.Create();
            LabelResources.Apply(_services.AppLabels);
            IconAssets.Apply(_services);

            // The Shift-at-startup recovery (Portion 16, owner's word): protection off, back to
            // manual. Said out loud in a box, because a silent reset would read as a bug the next
            // time × closes without asking.
            if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                && _services.Startup.IsProtected)
            {
                _services.Startup.ResetToManual();
                RunTrail.Step("startup protection reset by Shift-at-startup");
                MessageBox.Show(
                    "Closing protection was switched off and Area11 is back to manual start. " +
                    "This was written to the black box. Pick a mode again in Settings, Shop " +
                    "when you are ready.",
                    "Area11", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // Every button that fails writes one line to the same log the rest of the program uses
            // (v0.34.0, D-90). Set once, here, because this is the first moment the log exists.
            CommandGuard.Sink = (message, exception) => _services!.Log.Error(message, exception);
            RunTrail.Step("database ready");

            // A run that never said goodbye (Task Manager, power loss) is reported now, once, to
            // the black box - before the sign-in window, so there is no one on the screen to blame
            // and no way to start without the line being written.
            var previousRun = _services.Focus.CheckPreviousRun();

            if (!string.IsNullOrWhiteSpace(previousRun))
            {
                RunTrail.Step("previous run: " + previousRun);
            }

            // The shop's time (v0.35.0, D-92): asked from the internet in the background, from this
            // moment and every 20 minutes after it. Nothing waits for the answer - if the line is
            // down the sign-in window opens at once on the computer's own clock.
            // The trail keeps one line per change of answer, not one every 20 minutes: the heartbeat
            // is read by a person and has 140 lines for the whole day (D-90), so thirty identical
            // "no time server answered" lines would bury the one page that failed.
            var lastClockNote = string.Empty;

            TimeSource.StartBackgroundSync(note =>
            {
                if (!string.Equals(note, lastClockNote, StringComparison.Ordinal))
                {
                    lastClockNote = note;
                    RunTrail.Step("clock: " + note);
                }

                _services!.Log.Info("Clock: " + note);
            });

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

    protected override void OnExit(ExitEventArgs e)
    {
        // The opposite of a kill: a Task Manager end never reaches here, so the stamp below is
        // exactly what the next startup checks for. Never throws - an exit that crashes is the one
        // thing this override must not become.
        try
        {
            _services?.Focus.MarkCleanExit();
        }
        catch (Exception)
        {
        }

        // Letting go of the single-instance lock. Dispose never throws, and even a kill that skips
        // this override is safe: the mutex dies with the process and the next start takes it over.
        _instance?.Dispose();
        _instance = null;

        base.OnExit(e);
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        RunTrail.Step("FAULT on the UI thread: " + e.Exception.GetType().Name + ": " + e.Exception.Message);
        CrashReporter.Report("working in the application", e.Exception, "The window that was open has a problem.");

        // A file is not an explanation (v0.33.0, D-89). When the shell is on the screen, the page
        // that failed is replaced by the same card a page that could not be opened gets - so the
        // operator reads what happened instead of guessing at a half-drawn page. The frame decides
        // which page it was and cannot throw back at us; if it somehow did, the file above is
        // already written and `e.Handled` below keeps the program alive.
        try
        {
            if (MainWindow?.DataContext is ShellViewModel shell)
            {
                shell.ShowDrawingFault(e.Exception);
            }
        }
        catch (Exception)
        {
            // Never let the explanation become the fault.
        }

        // The app keeps running: the shops must never lose the till because the dashboard failed.
        e.Handled = true;
    }

    private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        RunTrail.Step("UNRECOVERABLE: " + (e.ExceptionObject as Exception)?.Message);
        CrashReporter.Report("an unrecoverable error", e.ExceptionObject as Exception);
    }

    private void ShowLogin()
    {
        RunTrail.Step("login window shown");

        var viewModel = new LoginViewModel(_services!);
        viewModel.SignedIn += OpenShell;

        _loginWindow = new LoginWindow(viewModel);
        MainWindow = _loginWindow;
        _loginWindow.Show();
    }

    private void OpenShell(User user)
    {
        RunTrail.Step("signed in: " + user.Name);

        var viewModel = new ShellViewModel(_services!, user);
        var shell = new ShellWindow { DataContext = viewModel };

        shell.Closed += (_, _) =>
        {
            // The frame listens to the command guard; it must stop when the frame goes, or the
            // next shell would share one banner with a window that no longer exists.
            viewModel.Detach();

            if (!_loggingOut)
            {
                Shutdown();
            }
        };

        viewModel.LoggedOut += () =>
        {
            _loggingOut = true;
            ShowLogin();
            shell.AllowClose = true;
            shell.Close();
            _loggingOut = false;
        };

        // The idle limit or the 12-hour limit ended the session (v0.35.0, D-92). Same road as a
        // sign-out: the frame closes and the sign-in window is the only thing on the screen.
        viewModel.SessionExpired += () =>
        {
            _loggingOut = true;
            ShowLogin();
            shell.AllowClose = true;
            shell.Close();
            _loggingOut = false;
        };

        if (_loginWindow is not null)
        {
            _loginWindow.AllowClose = true;
            _loginWindow.Close();
        }

        _loginWindow = null;

        MainWindow = shell;
        shell.Show();
    }
}
