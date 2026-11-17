using System;
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
        Closed += (_, _) => TaskbarHider.Show();
        ApplyKioskIfAutoMode();
    }

    /// <summary>
    /// Kiosk login (owner's word, Nov 2026): on a PC whose mode auto-starts Area11, the sign-in
    /// fills the whole screen edge to edge and the taskbar is really hidden - staff see nothing
    /// but the shop. Manual PCs keep the normal window. The taskbar is restored when this window
    /// closes for ANY reason (see Closed above), and App startup heals a taskbar orphaned by a
    /// kill during a previous login.
    /// </summary>
    private void ApplyKioskIfAutoMode()
    {
        if (!_viewModel.Services.Startup.WantsAutoStart)
        {
            return;
        }

        WindowStyle = WindowStyle.None;
        WindowState = WindowState.Maximized;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        TaskbarHider.Hide();
    }

    private void ConfirmLoginClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.IsSystemShutdown || AllowClose)
        {
            return;
        }

        // The screen lock implies a protected close (owner's word, Nov 2026): with the lock on,
        // × asks for the password even when the startup mode itself is not protected.
        if (!_viewModel.Services.Startup.IsProtected && !_viewModel.Services.Focus.Enabled)
        {
            return;
        }

        e.Cancel = true;

        var dialog = new CloseUnlockWindow(_viewModel.Services) { Owner = this };

        if (dialog.ShowDialog() == true && dialog.Unlocked)
        {
            // Same repair as ShellWindow (owner's 2026-11-11 VerifyNotClosing trace): never a
            // direct Close() inside Closing - the real close is deferred past the cancelled event.
            AllowClose = true;
            Dispatcher.BeginInvoke(new Action(Close));
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

    private void OnForgotPassword(object sender, RoutedEventArgs e)
    {
        var dialog = new RecoveryWindow(_viewModel.Services) { Owner = this };

        if (dialog.ShowDialog() == true && dialog.RecoveredUser is not null)
        {
            _viewModel.SignInAs(dialog.RecoveredUser);
        }
    }
}
