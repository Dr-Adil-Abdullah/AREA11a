using System.Windows;
using System.Windows.Input;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    /// <summary>
    /// Portion 16: one-shot pass. The app sets it before a close it ordered itself (a successful
    /// sign-in) - only a person's × is ever asked for the password.
    /// </summary>
    public bool AllowClose { get; set; }

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => NameBox.Focus();
        Closing += ConfirmLoginClosing;
    }

    private void ConfirmLoginClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.IsSystemShutdown || AllowClose)
        {
            return;
        }

        if (!_viewModel.Services.Startup.IsProtected)
        {
            return;
        }

        e.Cancel = true;

        var dialog = new CloseUnlockWindow(_viewModel.Services) { Owner = this };

        if (dialog.ShowDialog() == true && dialog.Unlocked)
        {
            AllowClose = true;
            Close();
        }
    }

    private void OnSignInClick(object sender, RoutedEventArgs e) => TrySignIn();

    private void OnSecretKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TrySignIn();
        }
    }

    private void TrySignIn() => _viewModel.SignIn(SecretBox.Password);
}
