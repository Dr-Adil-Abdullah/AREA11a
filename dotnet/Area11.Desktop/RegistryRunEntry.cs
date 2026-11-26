using Area11.Core.Services;
using Microsoft.Win32;

namespace Area11.Desktop;

/// <summary>Only Run\Area11's value. No key creation, ACL changes or environment expansion.
/// The lock serializes this process only; another process can race between compare/write/readback.</summary>
internal static class RegistryRunEntry
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Area11";
    private static readonly object Gate = new();

    public static AutoStartEntry Capture(RegistryKey root)
    {
        lock (Gate)
        {
            using var key = root.OpenSubKey(RunKey, writable: false);
            if (key is null || !key.GetValueNames().Contains(ValueName, StringComparer.OrdinalIgnoreCase))
                return AutoStartEntry.Missing;
            var kind = key.GetValueKind(ValueName);
            var value = key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                ?? throw new InvalidOperationException("The Windows start-up value changed while being read.");
            return AutoStartEntry.Present((AutoStartValueKind)(int)kind, value);
        }
    }

    public static void Replace(RegistryKey root, AutoStartEntry expected, AutoStartEntry replacement)
    {
        lock (Gate)
        {
            if (!Capture(root).Matches(expected))
                throw new InvalidOperationException("The Windows start-up entry changed. Verify it before retrying.");
            if (expected.Matches(replacement)) return;
            using (var key = root.OpenSubKey(RunKey, writable: true)
                ?? throw new InvalidOperationException("The Windows start-up list could not be opened for writing."))
            {
                if (replacement.IsPresent)
                    key.SetValue(ValueName, replacement.Value!, (RegistryValueKind)(int)replacement.Kind);
                else key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            if (!Capture(root).Matches(replacement))
                throw new InvalidOperationException("The Windows start-up entry could not be verified after writing.");
        }
    }
}
