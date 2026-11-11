using System.Windows;
using System.Windows.Input;
using Area11.Core.Models;

namespace Area11.Desktop.Views;

/// <summary>
/// Phase 2, owner's word: the "forgot password" door. Code-behind on purpose, like the closing
/// prompt: a PasswordBox must never be data-bound, so this window reads it and hands it straight
/// to the service. No rule lives here either - the service decides, throttles and records.
/// </summary>
public partial class RecoveryWindow : Window
{
    private readonly AppServices _services;

    public RecoveryWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;
        Loaded += (_, _) => SecretBox.Focus();
    }

    /// <summary>The signed-in owner. Read after ShowDialog, only when it returned true.</summary>
    public User? RecoveredUser { get; private set; }

    private void OnRecover(object sender, RoutedEventArgs e) => TryRecover();

    private void OnSecretKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryRecover();
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TryRecover()
    {
        var result = _services.Auth.RecoverOwner(SecretBox.Password);

        if (!result.Ok || result.Value is null)
        {
            ErrorText.Text = result.Error ?? "The recovery word is not correct.";
            ErrorText.Visibility = Visibility.Visible;
            SecretBox.Clear();
            SecretBox.Focus();
            return;
        }

        RecoveredUser = result.Value;
        DialogResult = true;
        Close();
    }
}
