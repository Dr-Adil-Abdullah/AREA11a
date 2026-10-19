using System.Windows;
using Area11.Core.Models;
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

        try
        {
            _services = AppServices.Create();
            ShowLogin();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "Area11 could not start.\n\n" + exception.Message +
                "\n\nThe details were written to the log folder.",
                "Area11", MessageBoxButton.OK, MessageBoxImage.Error);

            Shutdown(1);
        }
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
