using System.Diagnostics;
using Area11.Core.Utils;

namespace Area11.Desktop;

/// <summary>
/// Sends WPF's binding complaints to <see cref="BindingWatch"/> instead of the debug output.
///
/// Why a `TraceListener` and not a try/catch: a binding that names a property which does not exist
/// never throws. WPF writes it to `PresentationTraceSources.DataBindingSource` and carries on with
/// an empty box. On a shop PC nobody can see that stream, so the page simply "has no data" - the
/// hardest fault to trace back to one letter, and the one this project has hit most often.
/// </summary>
public sealed class BindingWatchListener : TraceListener
{
    public override void Write(string? message) => BindingWatch.Note(message);

    public override void WriteLine(string? message) => BindingWatch.Note(message);
}

/// <summary>Attaches the listener, once, before the first window is drawn.</summary>
public static class BindingWatchInstall
{
    /// <summary>
    /// Errors only: WPF also emits information-level chatter that would bury the one line that
    /// matters. If anything here fails the program still starts - a diagnostic may never be a
    /// reason the shop cannot open its till.
    /// </summary>
    public static void Install()
    {
        try
        {
            PresentationTraceSources.Refresh();

            var source = PresentationTraceSources.DataBindingSource;
            source.Listeners.Add(new BindingWatchListener());
            source.Switch.Level = SourceLevels.Error;
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                              or NotSupportedException
                                              or System.Security.SecurityException)
        {
            // Diagnostics are optional; the till is not.
        }
    }
}
