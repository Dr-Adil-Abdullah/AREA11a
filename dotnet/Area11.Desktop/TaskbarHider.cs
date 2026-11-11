using System;
using System.Runtime.InteropServices;

namespace Area11.Desktop;

/// <summary>
/// Really hides the Windows taskbar while the kiosk login is on the screen (owner's word, Nov 2026:
/// covering the taskbar was offered, hiding was chosen).
///
/// Safety, because a hidden taskbar that never comes back is a broken PC:
///   * Hide/Show are reference-counted, so two stacked logins cannot unhide each other early;
///   * the login restores on Closed for ANY reason (sign-in, x, crash-box survival);
///   * App startup heals a taskbar orphaned by a kill during a previous login (first copy only);
///   * App exit forces it back (first copy only);
///   * every method swallows: the till must never fall over hiding chrome.
/// The main bar (Shell_TrayWnd) and every extra monitor's bar (Shell_SecondaryTrayWnd) go together.
/// </summary>
internal static class TaskbarHider
{
    private const int SwHide = 0;
    private const int SwShow = 5;

    private static int _hidden;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(
        IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static void Hide()
    {
        try
        {
            if (_hidden++ == 0)
            {
                SetVisible(false);
            }
        }
        catch (Exception)
        {
        }
    }

    public static void Show()
    {
        try
        {
            if (_hidden > 0 && --_hidden == 0)
            {
                SetVisible(true);
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Startup heal and exit force: the count is meaningless across a kill - just show it.</summary>
    public static void EnsureVisible()
    {
        try
        {
            _hidden = 0;
            SetVisible(true);
        }
        catch (Exception)
        {
        }
    }

    private static void SetVisible(bool visible)
    {
        var command = visible ? SwShow : SwHide;

        var tray = FindWindow("Shell_TrayWnd", null);

        if (tray != IntPtr.Zero)
        {
            ShowWindow(tray, command);
        }

        var child = IntPtr.Zero;

        for (var guard = 0; guard < 16; guard++)
        {
            child = FindWindowEx(IntPtr.Zero, child, "Shell_SecondaryTrayWnd", null);

            if (child == IntPtr.Zero)
            {
                break;
            }

            ShowWindow(child, command);
        }
    }
}
