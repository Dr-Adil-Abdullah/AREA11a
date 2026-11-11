using System.Windows;
using Area11.Core.Utils;

namespace Area11.Desktop;

/// <summary>
/// The one place that tells the owner something broke.
///
/// Why it is separate from App: a WPF program can fail before `OnStartup` ever runs - a broken
/// resource dictionary, a missing assembly - and an exception there is invisible: the window never
/// appears, the console of a WinExe shows nothing, and the batch file can only say "stopped with an
/// error". Every failure now writes a file with the full stack trace, prints the same text on the
/// console, and shows the path in a box.
/// </summary>
public static class CrashReporter
{
    public static void Report(string area, Exception? exception, string? note = null)
    {
        var file = CrashLog.Write(area, exception, note);
        var message = exception?.Message ?? "No detail was available.";

        try
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("  AREA11 CRASH in " + area + ":");
            Console.Error.WriteLine("  " + (exception?.GetType().Name ?? "Exception") + ": " + message);

            if (!string.IsNullOrWhiteSpace(file))
            {
                Console.Error.WriteLine("  Full details: " + file);
            }

            Console.Error.WriteLine();
        }
        catch (Exception)
        {
            // No console attached (double-clicked exe). The file and the box are enough.
        }

        var text = "Area11 hit a problem " + area + ".\n\n"
            + (string.IsNullOrWhiteSpace(note) ? string.Empty : note + "\n\n")
            + (exception?.GetType().Name ?? "Exception") + ": " + message
            + (string.IsNullOrWhiteSpace(file)
                ? "\n\nThe details could not be written to a file."
                : "\n\nFull details were written to:\n" + file)
            + "\n\nSend this text and that file in the chat.";
        var caption = "Area11 " + BuildInfo.Version;

        // Kiosk login (v0.66.0) is topmost fullscreen: an ownerless box would open UNDER it and the
        // shop would stare at a frozen sign-in. Parenting to the open window keeps the box on top.
        var owner = SafeMainWindow();

        try
        {
            if (owner is null)
            {
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                try
                {
                    MessageBox.Show(owner, text, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception)
                {
                    // The owner was closing under us - the plain box is still better than silence.
                    MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch (Exception)
        {
            // Even the box failed; the console line and the file are still there.
        }
    }

    private static Window? SafeMainWindow()
    {
        try
        {
            var owner = Application.Current?.MainWindow;
            return owner is not null && owner.IsLoaded && owner.IsVisible ? owner : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
