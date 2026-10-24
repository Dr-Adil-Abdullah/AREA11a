using System.IO;
using System.Windows;
using Area11.WPF.Services;
using Area11.WPF.Utils;
using Area11.WPF.ViewModels;
using Area11.WPF.Views;

namespace Area11.WPF;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var config = AppConfig.Load(configPath);
            Services = new AppServices(config);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "Area11 POS could not start.\n\n" + exception.Message,
                "Area11 POS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
            return;
        }

        var loginView = new LoginView(new LoginViewModel(Services));
        if (loginView.ShowDialog() != true || loginView.CurrentUser is null)
        {
            Shutdown();
            return;
        }

        var shell = new ShellView(new ShellViewModel(Services, loginView.CurrentUser));
        MainWindow = shell;
        shell.Closed += (_, _) => Shutdown();
        shell.Show();
    }
}
