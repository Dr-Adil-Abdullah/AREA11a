using System.Windows;
using System.Windows.Input;

namespace Area11.Desktop.Views;

/// <summary>
/// Portion 16: the "who is closing?" prompt. Code-behind on purpose: a PasswordBox must never be
/// data-bound (its content would sit in memory as plain text), so this window reads it and hands it
/// straight to the service, exactly like the sign-in window does. No rule lives here either - the
/// service decides who may close, and the black box records who did.
/// </summary>
public partial class CloseUnlockWindow : Window
{
    private readonly AppServices _services;

    public CloseUnlockWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;

        // De-workshop (v0.69.0): this prompt carries the shop's app name, not the product name.
        var app = AppDisplayName;
        Title = app + " — Closing is protected";
        WhoText.Text = "Who is closing " + app + "?";
        CloseButton.Content = "Close " + app;

        Loaded += (_, _) => NameBox.Focus();
    }

    private string AppDisplayName
    {
        get
        {
            var name = _services.Settings.GetString("brand.appName").Trim();
            return name.Length == 0 ? "Area11" : name;
        }
    }

    /// <summary>True when the service accepted the secret and the permission. Read after ShowDialog.</summary>
    public bool Unlocked { get; private set; }

    private void OnUnlock(object sender, RoutedEventArgs e) => TryUnlock();

    private void OnSecretKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryUnlock();
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        // Setting DialogResult already closes a modal dialog - Close() after it crashes the
        // window while it is closing (owner's 2026-11-11 InvalidOperationException). Never add it.
        DialogResult = false;
    }

    private void TryUnlock()
    {
        var result = _services.Startup.VerifyClose(NameBox.Text, SecretBox.Password);

        if (!result.Ok || result.Value is null)
        {
            ErrorText.Text = result.Error ?? "The password or PIN is not correct.";
            ErrorText.Visibility = Visibility.Visible;
            SecretBox.Clear();
            SecretBox.Focus();
            return;
        }

        Unlocked = true;
        // No Close() after this: DialogResult already closes the dialog (see OnCancel).
        DialogResult = true;
    }
}
