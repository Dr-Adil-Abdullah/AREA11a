using Area11.Core.Services;
using Microsoft.Win32;

namespace Area11.Desktop;

/// <summary>
/// The fallback beside <see cref="HklmRunStore"/> (owner's word, Nov 2026): Area11 in the
/// CURRENT Windows account's Run list. Writing here needs no administrator, so a save refused
/// for all accounts still starts the program for the account that saved it - and says so.
/// Same seam, same value name, lives in Desktop for the same reason (registry is Windows-only).
/// </summary>
public sealed class HkcuRunStore : IAutoStartStore
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public const string ValueName = "Area11";

    public void Enable(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException("The program's own path is needed for the start-up entry.", nameof(exePath));
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("The Windows start-up list could not be opened.");

        key.SetValue(ValueName, "\"" + exePath.Trim() + "\"", RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);

        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);

            return key?.GetValue(ValueName) is string value && value.Trim().Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
