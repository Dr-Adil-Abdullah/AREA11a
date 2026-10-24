using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Area11.Desktop;

/// <summary>
/// Starts Area11 again and closes this copy.
///
/// **Why a restart and not a refresh:** a restore replaces the database FILE. Every page that is
/// open holds numbers, names and totals that were read from the file that no longer exists, and a
/// page that keeps showing pre-restore numbers after a restore is worse than no page at all. Starting
/// the program again takes about a second, and it is the only way to be certain that what is on the
/// screen is what is in the database.
///
/// The restore itself has already finished and been written to the black box before this runs. If
/// the new copy cannot be started, this method only closes the window - the owner then double-clicks
/// START-AREA11.bat, which is the same thing.
/// </summary>
public static class AppRestart
{
    public static void Restart()
    {
        var executable = Environment.ProcessPath;

        if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
        {
            try
            {
                Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // No new copy: fall through and close. The owner starts it the usual way.
            }
        }

        Application.Current?.Shutdown();
    }
}
