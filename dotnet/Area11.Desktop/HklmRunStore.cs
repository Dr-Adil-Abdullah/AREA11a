using Area11.Core.Services;
using Microsoft.Win32;

namespace Area11.Desktop;

/// <summary>
/// Portion 16: Area11 in the machine-wide Run list, so every Windows account on this PC opens it at
/// sign-in (owner's word). Writing here needs a Windows administrator - without one the save is
/// refused with a sentence, never a stack trace. Lives in Desktop because the registry API only
/// exists on Windows and Core builds for plain .NET.
/// </summary>
public sealed class HklmRunStore : IAutoStartStore
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public const string ValueName = "Area11";

    public void Enable(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException("The program's own path is needed for the start-up entry.", nameof(exePath));
        }

        using var key = Registry.LocalMachine.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("The Windows start-up list could not be opened.");

        key.SetValue(ValueName, "\"" + exePath.Trim() + "\"", RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RunKey, writable: true);

        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RunKey, writable: false);

            return key?.GetValue(ValueName) is string value && value.Trim().Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
