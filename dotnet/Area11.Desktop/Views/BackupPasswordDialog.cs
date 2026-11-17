using System.Windows;
using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>Password controls only; no secret binding, settings property or telemetry.</summary>
public sealed class BackupPasswordDialog : Window
{
    private readonly PasswordBox _password = new() { MaxLength = 1024, Margin = new Thickness(0, 8, 0, 8) };
    private readonly PasswordBox _confirmation = new() { MaxLength = 1024, Margin = new Thickness(0, 8, 0, 8) };
    private readonly bool _setup;

    private BackupPasswordDialog(bool setup)
    {
        _setup = setup;
        Title = CrashReporter.AppName + " — " + (setup ? "Set / change backup password" : "Original password for this backup");
        SetResourceReference(IconProperty, "Area11WindowIcon");
        ShowInTaskbar = false;
        Width = 500;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = setup
                ? "Owner only. Use a separate password (12–1024 characters). Automatic backups use Windows-protected storage for this Windows account and shop path. Keep the password somewhere safe: losing it can make backups unrecoverable. Changing it does NOT change older backups."
                : "Enter the ORIGINAL password used when this file was made, even on a new PC. First set up a local backup password so an encrypted safety copy of the current shop can be made. Cancel changes nothing.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock { Text = "Backup password" });
        panel.Children.Add(_password);
        if (setup)
        {
            panel.Children.Add(new TextBlock { Text = "Confirm new backup password" });
            panel.Children.Add(_confirmation);
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(12, 6, 12, 6) };
        var accept = new Button { Content = setup ? "Store password" : "Verify and restore", IsDefault = true,
            Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
        accept.Click += (_, _) =>
        {
            if (_password.Password.Length < 12 || (_setup && _password.Password != _confirmation.Password))
            {
                MessageBox.Show(this, "Use at least 12 characters; confirmation must match.", "Backup password");
                return;
            }
            DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(accept);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _password.Focus();
    }

    public static (string Password, string Confirmation)? Prompt(bool setup)
    {
        var dialog = new BackupPasswordDialog(setup);
        try
        {
            return dialog.ShowDialog() == true
                ? (dialog._password.Password, dialog._confirmation.Password) : null;
        }
        finally
        {
            dialog._password.Clear();
            dialog._confirmation.Clear();
        }
    }
}
