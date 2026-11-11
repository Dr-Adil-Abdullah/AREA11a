using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// How Area11 starts and how it may be closed (Portion 16, roadmap #16). Three modes, owner's word:
///   • manual    - start from the icon, close with × like any program;
///   • auto      - Windows starts it at every sign-in on this PC, × still closes it;
///   • protected - auto-start, and × asks for the owner's password/PIN (or a staff member the
///                 owner explicitly ticked for closing) every single time.
///
/// The honest limit, stated wherever the mode is shown: this stops casual closing, not a Windows
/// administrator, Task Manager, OS shutdown or power loss - and the shop's data survives all of
/// those (SQLite + drafts), so protection is never sold as something it is not.
/// </summary>
public sealed class StartupService
{
    public const string ModeKey = "startup.mode";

    public const string Manual = "manual";

    public const string Auto = "auto";

    public const string Protected = "protected";

    private readonly SettingsService _settings;
    private readonly AuthService _auth;
    private readonly PermissionService _permissions;
    private readonly AuditService _audit;
    private readonly LogService _log;
    private readonly IAutoStartStore _machine;
    private readonly IAutoStartStore _userStore;

    public StartupService(
        SettingsService settings,
        AuthService auth,
        PermissionService permissions,
        AuditService audit,
        LogService log,
        IAutoStartStore machine,
        IAutoStartStore userStore)
    {
        _settings = settings;
        _auth = auth;
        _permissions = permissions;
        _audit = audit;
        _log = log;
        _machine = machine;
        _userStore = userStore;
    }

    /// <summary>The saved mode, or manual when the word is missing or unreadable. Never throws.</summary>
    public string Mode
    {
        get
        {
            try
            {
                return _settings.GetString(ModeKey).Trim().ToLowerInvariant() switch
                {
                    Auto => Auto,
                    Protected => Protected,
                    _ => Manual
                };
            }
            catch (Exception exception)
            {
                _log.Error("The startup mode could not be read; manual is assumed.", exception);
                return Manual;
            }
        }
    }

    /// <summary>True in protected mode: every × must prove a secret.</summary>
    public bool IsProtected => Mode == Protected;

    /// <summary>True in auto and protected: Windows should start Area11 at sign-in.</summary>
    public bool WantsAutoStart => Mode is Auto or Protected;

    /// <summary>Where the entry actually lives, read live from Windows (never a stored claim).</summary>
    public string ScopeText
    {
        get
        {
            if (EnabledOf(_machine))
            {
                return "Applies to every Windows account on this PC.";
            }

            if (EnabledOf(_userStore))
            {
                return "Applies to this Windows account only - save again as administrator for every account.";
            }

            return "No start-up entry is set.";
        }
    }

    /// <summary>One line for Settings and the sign-in screen.</summary>
    public string ModeLabel => Mode switch
    {
        Auto => "Starts with Windows; closes normally.",
        Protected => "Starts with Windows; closing needs the password.",
        _ => "Starts and closes by hand."
    };

    /// <summary>
    /// The owner picks a mode. The Windows entry is written FIRST and the setting only after it
    /// sticks, so the two can never disagree with the setting claiming what Windows never got.
    /// All accounts first; when no administrator is available the current account still gets its
    /// own entry (owner's word, Nov 2026) and the scope sentence says so - a refused save is now
    /// only for a Windows that refuses even that. One entry only: two Run values would open
    /// Area11 twice, and nothing here guards a second copy.
    /// </summary>
    public ServiceResult SetMode(User actor, string mode, string exePath)
    {
        if (actor is null || !actor.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can change how Area11 starts and closes.");
        }

        var clean = (mode ?? string.Empty).Trim().ToLowerInvariant();

        if (clean is not (Manual or Auto or Protected))
        {
            return ServiceResult.Fail("The startup mode must be manual, auto or protected.");
        }

        var machineWas = EnabledOf(_machine);
        var userWas = EnabledOf(_userStore);
        var scope = "machine";

        try
        {
            if (clean == Manual)
            {
                DisableQuietly(_machine);
                DisableQuietly(_userStore);
            }
            else
            {
                try
                {
                    _machine.Enable(exePath);
                    DisableQuietly(_userStore);
                }
                catch (UnauthorizedAccessException)
                {
                    _userStore.Enable(exePath);
                    scope = "user";
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return ServiceResult.Fail(
                "Windows refused even this account's own start-up entry. Right-click START-AREA11, " +
                "choose Run as administrator, then save again. Nothing was changed.");
        }
        catch (Exception exception)
        {
            _log.Error("The Windows start-up entry could not be written.", exception);

            return ServiceResult.Fail(
                "The Windows start-up entry could not be written (" + exception.Message + "). Nothing was changed.");
        }

        var saved = _settings.Set(ModeKey, clean, actor);

        if (!saved.Ok)
        {
            // The setting refused after Windows accepted: put back exactly what was there before
            // this call, so the next reader never finds an entry the setting does not know about.
            RestoreQuietly(_machine, machineWas, exePath);
            RestoreQuietly(_userStore, userWas, exePath);

            return ServiceResult.Fail(saved.Error ?? "The startup mode could not be saved.");
        }

        if (scope == "user")
        {
            return ServiceResult.Success(
                "No administrator was available, so this applies to this Windows account only. " +
                "Save again as administrator for every account on this PC.");
        }

        return ServiceResult.Success();
    }

    private static bool EnabledOf(IAutoStartStore store)
    {
        try
        {
            return store.IsEnabled();
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void DisableQuietly(IAutoStartStore store)
    {
        try
        {
            store.Disable();
        }
        catch (Exception exception)
        {
            _log.Warn("A start-up entry could not be removed: " + exception.Message);
        }
    }

    private void RestoreQuietly(IAutoStartStore store, bool wasEnabled, string exePath)
    {
        try
        {
            if (wasEnabled)
            {
                store.Enable(exePath);
            }
            else
            {
                store.Disable();
            }
        }
        catch (Exception exception)
        {
            _log.Error("The Windows start-up entry could not be rolled back.", exception);
        }
    }

    /// <summary>
    /// Proves a name + password/PIN and the closing permission, for the × prompt. Throttled inside
    /// <see cref="AuthService.VerifySecret"/> (guessing is slowed like at sign-in); the success is
    /// written to the black box with the name, so the shop always knows who closed the program.
    /// </summary>
    public ServiceResult<User> VerifyClose(string? name, string secret)
    {
        var verified = _auth.VerifySecret(name, secret);

        if (!verified.Ok || verified.Value is null)
        {
            return ServiceResult<User>.Fail(verified.Error ?? "The password or PIN is not correct.");
        }

        var user = verified.Value;

        if (!_permissions.Can(user, Modules.CloseApp))
        {
            _audit.RecordStandalone(user, "Auth", "close_refused",
                details: user.Name + " proved their secret but may not close Area11.");

            return ServiceResult<User>.Fail(
                "This account may not close Area11. Ask the owner to tick Close the program in " +
                "Settings, Staff & permissions.");
        }

        _audit.RecordStandalone(user, "Auth", "close_unlocked",
            details: user.Name + " unlocked closing.");

        return ServiceResult<User>.Success(user);
    }

    /// <summary>
    /// The Shift-at-startup recovery (owner's word): protection off, back to manual. The setting is
    /// the part that matters; removing the Windows entry is best-effort (without elevation it stays,
    /// and a manual mode with a leftover entry simply opens the program at sign-in - harmless).
    /// </summary>
    public ServiceResult ResetToManual()
    {
        try
        {
            DisableQuietly(_machine);
            DisableQuietly(_userStore);

            _settings.SetUiOnly(ModeKey, Manual);
            _audit.RecordStandalone(null, "Auth", "protection_reset",
                details: "Startup mode reset to manual by Shift-at-startup recovery.");

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error("The startup mode could not be reset.", exception);
            return ServiceResult.Fail("The startup mode could not be reset.");
        }
    }
}
