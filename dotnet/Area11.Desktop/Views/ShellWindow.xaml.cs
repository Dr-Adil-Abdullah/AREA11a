using System.Windows;
using System.Windows.Input;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// The frame. Its only job in code is to tell the session clock that somebody is still here: every
/// click and every key press is work, so the idle limit starts again (v0.35.0, D-92).
///
/// Why the window and not the pages: a person can read a page without touching a button - and a
/// person who is reading is not the person the rule is for. The rule is for the counter that was
/// left alone with the drawer open.
/// </summary>
public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
    }

    private void OnActivity(object sender, InputEventArgs e) =>
        (DataContext as ShellViewModel)?.TouchSession();
}
