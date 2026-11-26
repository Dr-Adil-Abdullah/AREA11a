using System.Security;
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
            if (_machine is ICheckedAutoStartStore machine && _userStore is ICheckedAutoStartStore user)
            {
                try
                {
                    var machineEntry = machine.Capture(); var userEntry = user.Capture();
                    if ((machineEntry.IsPresent && !HasCommand(machineEntry)) || (userEntry.IsPresent && !HasCommand(userEntry)))
                        return "A Windows start-up value is not a usable command. Verify both entries.";
                    if (machineEntry.IsPresent && userEntry.IsPresent) return "Both Windows start-up entries exist. Verify them before changing startup.";
                    if (machineEntry.IsPresent) return "Applies to every Windows account on this PC.";
                    if (userEntry.IsPresent) return "Applies to this Windows account only - save again as administrator for every account.";
                    return "No start-up entry is set.";
                }
                catch (Exception) { return "Windows start-up entries could not be read. Verify access and both entries."; }
            }
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
    /// sticks. Windows and SQLite are separate resources: restoration after a refusal is best-effort,
    /// not crash atomicity. Loaded forms check before registry work and again in the setting Tx;
    /// an intervening writer can still require verification of the Windows entries.
    /// All accounts first; when no administrator is available the current account still gets its
    /// own entry (owner's word, Nov 2026) and the scope sentence says so - a refused save is now
    /// only for a Windows that refuses even that. One entry only: two Run values would open
    /// Area11 twice, and nothing here guards a second copy.
    ///
    /// Both refusal shapes are caught: Windows answers a locked HKLM Run list with
    /// <see cref="SecurityException"/> ("Requested registry access is not allowed.") when the key
    /// is opened for writing, and with <see cref="UnauthorizedAccessException"/> when the value
    /// itself cannot be written. Catching only the second one let the first skip the fallback and
    /// fail the whole save (owner's screenshot, 11 Nov 2026) - that is the bug this catches.
    /// </summary>
    public ServiceResult SetMode(User actor, string mode, string exePath, SettingsEditForm? expected = null)
    {
        if (actor is null || !actor.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can change how Area11 starts and closes.");
        }

        var access = _settings.CheckAccess(actor);
        if (!access.Ok) return ServiceResult.Fail(access.Error ?? "Current Owner access is required.");

        var clean = (mode ?? string.Empty).Trim().ToLowerInvariant();

        if (clean is not (Manual or Auto or Protected))
        {
            return ServiceResult.Fail("The startup mode must be manual, auto or protected.");
        }

        if (expected is not null)
        {
            var baseline = _settings.CheckStartupEdit(actor, expected);
            if (!baseline.Ok) return baseline;
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
                catch (Exception refused) when (IsAccessRefusal(refused))
                {
                    _userStore.Enable(exePath);
                    scope = "user";
                }
            }
        }
        catch (Exception refused) when (IsAccessRefusal(refused))
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

        var saved = _settings.Set(ModeKey, clean, actor, expected);

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
            return new ServiceResult { Ok = true, SettingsEdit = saved.SettingsEdit, Warnings = new[] {
                "No administrator was available, so this applies to this Windows account only. Save again as administrator for every account on this PC." } };
        }

        return new ServiceResult { Ok = true, SettingsEdit = saved.SettingsEdit };
    }

    /// <summary>Settings-only checked path. Value snapshots preserve old raw command/kind/presence.
    /// Known DB rollback permits conditional compensation; unknown/committed DB outcomes NEVER do.
    /// No cross-resource/process atomicity, durable retry identity or installer/recovery change.</summary>
    public ServiceResult SetModeChecked(User actor, string mode, string exePath, SettingsEditForm? expected)
    {
        static ServiceResult Refuse(string message) => new() { Error = message, NoCommitConfirmed = true };
        if (actor is null || !actor.IsOwner) return Refuse("Only the owner can change how Area11 starts and closes.");
        if (expected is null) return Refuse("Load Settings before changing the startup mode.");
        var clean = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (clean is not (Manual or Auto or Protected)) return Refuse("Choose manual, auto or protected startup.");
        var baseline = _settings.CheckStartupEdit(actor, expected);
        if (!baseline.Ok) return Refuse(baseline.Error ?? "Current Owner and loaded startup state are required.");
        if (_machine is not ICheckedAutoStartStore machine || _userStore is not ICheckedAutoStartStore user)
            return Refuse("This host cannot safely capture Windows start-up entries. No startup change was attempted.");

        AutoStartEntry machineBefore;
        AutoStartEntry userBefore;
        AutoStartEntry desired;
        try
        {
            desired = clean == Manual ? AutoStartEntry.Missing : AutoStartEntry.Command(exePath);
            machineBefore = machine.Capture();
            userBefore = user.Capture();
        }
        catch (Exception exception)
        {
            _log.Error("Windows start-up entries could not be captured before editing.", exception);
            return Refuse("Windows start-up entries could not be read. No startup change was attempted; check access and refresh.");
        }

        var machineTarget = desired;
        var userTarget = AutoStartEntry.Missing;
        var machineAttempted = false;
        var userAttempted = false;
        var settingsStarted = false;
        var settingsSaved = false;
        var userScope = false;
        try
        {
            try
            {
                machineAttempted = true;
                machine.Replace(machineBefore, machineTarget);
            }
            catch (Exception exception) when (clean != Manual && IsAccessRefusal(exception))
            {
                // A denied write is not proof of no mutation. Never fallback beside an old HKLM entry.
                if (machineBefore.IsPresent || !machine.Capture().Matches(machineBefore)) throw;
                machineTarget = machineBefore;
                userTarget = desired;
                userScope = true;
            }
            userAttempted = true;
            user.Replace(userBefore, userTarget);
            if (!machine.Capture().Matches(machineTarget) || !user.Capture().Matches(userTarget))
                throw new InvalidOperationException("Windows start-up entries changed before the Settings save.");

            settingsStarted = true;
            var saved = _settings.SaveEditForm(new Dictionary<string, object?> { [ModeKey] = clean }, actor, expected);
            if (!saved.Ok)
            {
                if (!saved.NoCommitConfirmed)
                    return new ServiceResult { Error = (saved.Error ?? "Startup settings save is unconfirmed.")
                        + " Windows entries were not rolled back. Verify Settings and both Windows entries before reopening; do not repeat the change." };
                var restored = RestoreChecked(machine, machineBefore, machineTarget, machineAttempted)
                    & RestoreChecked(user, userBefore, userTarget, userAttempted);
                return new ServiceResult { NoCommitConfirmed = restored,
                    Error = "Startup setting was not committed. " + (restored
                        ? "The previous Windows values were restored and read back. Refresh and review before retrying. "
                        : "Windows restoration is unconfirmed. Verify both entries before reopening or retrying. ") + saved.Error };
            }
            settingsSaved = true;
            if (saved.SettingsEdit is null || saved.SettingsEdit.GetString(ModeKey) != clean)
                throw new InvalidOperationException("The saved startup receipt is missing or mismatched.");
            if (!machine.Capture().Matches(machineTarget) || !user.Capture().Matches(userTarget))
                throw new InvalidOperationException("Windows start-up entries changed after the Settings save.");
            return new ServiceResult { Ok = true, SettingsEdit = saved.SettingsEdit, Warnings = userScope
                ? new[] { "No administrator was available, so this applies to this Windows account only. Save again as administrator for every account on this PC." }
                : Array.Empty<string>() };
        }
        catch (Exception exception)
        {
            _log.Error("Checked startup change did not finish.", exception);
            if (settingsStarted)
                return new ServiceResult { Error = (settingsSaved ? "SAVED. Startup mode was stored; " : "Startup setting save is unconfirmed; ")
                    + "Windows entries were not rolled back. Verify Settings and both Windows entries before reopening or retrying." };
            var restored = RestoreChecked(machine, machineBefore, machineTarget, machineAttempted)
                & RestoreChecked(user, userBefore, userTarget, userAttempted);
            return new ServiceResult { NoCommitConfirmed = restored,
                Error = "Startup setting was not written. " + (restored
                    ? "Previous Windows values were verified. Check Windows administrator access, refresh and review before retrying."
                    : "Windows restoration is unconfirmed. Verify both entries before reopening or retrying.") };
        }
    }

    private static bool HasCommand(AutoStartEntry entry) =>
        entry.Kind is AutoStartValueKind.String or AutoStartValueKind.ExpandString
        && entry.Value is string command && !string.IsNullOrWhiteSpace(command);

    private bool RestoreChecked(ICheckedAutoStartStore store, AutoStartEntry before, AutoStartEntry intended, bool attempted)
    {
        try
        {
            var current = store.Capture();
            if (current.Matches(before)) return true;
            // Do not overwrite an unrelated/concurrent Windows value, or touch an unattempted store.
            if (!attempted || !current.Matches(intended)) return false;
            store.Replace(intended, before);
            return store.Capture().Matches(before);
        }
        catch (Exception exception)
        {
            _log.Error("Previous Windows start-up value could not be verified/restored.", exception);
            return false;
        }
    }

    /// <summary>
    /// The installer's path (Phase 5, owner's word): machine-wide start-with-Windows plus
    /// `startup.mode = auto`, run once by `Area11.exe --enable-autostart` at the end of setup.
    /// Machine ONLY, deliberately no this-account fallback: setup runs elevated, often as a
    /// different administrator account, so a fallback would register the wrong person's sign-in.
    /// When Windows refuses, the install itself still stands - the sentence says where to switch
    /// auto-start on by hand (Settings, Shop), and setup shows it without failing.
    /// </summary>
    public ServiceResult SetMachineAutoStart(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return ServiceResult.Fail("The installed program's path is needed for the start-up entry.");
        }

        var machineWas = EnabledOf(_machine);

        try
        {
            _machine.Enable(exePath);
        }
        catch (Exception refused) when (IsAccessRefusal(refused))
        {
            return ServiceResult.Fail(
                "Windows refused the machine-wide start-up entry. Area11 is installed and works - " +
                "switch auto-start on in Settings, Shop (run as administrator for every account).");
        }
        catch (Exception exception)
        {
            _log.Error("The machine-wide start-up entry could not be written by setup.", exception);

            return ServiceResult.Fail(
                "The machine-wide start-up entry could not be written (" + exception.Message + "). " +
                "Area11 is installed and works - switch auto-start on in Settings, Shop.");
        }

        var saved = _settings.SaveSystemStartupMode(automatic: true);

        if (!saved.Ok)
        {
            RestoreQuietly(_machine, machineWas, exePath);

            return ServiceResult.Fail(saved.Error ?? "The startup mode could not be saved.");
        }

        return ServiceResult.Success();
    }

    /// <summary>
    /// The two shapes of "Windows said no": a value that cannot be written
    /// (<see cref="UnauthorizedAccessException"/>) and a key that cannot even be opened for
    /// writing (<see cref="SecurityException"/> - the HKLM answer without an administrator).
    /// Everything else (a broken path, a missing list) is a real fault and must keep failing loud.
    /// </summary>
    private static bool IsAccessRefusal(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException;

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

            var saved = _settings.SaveSystemStartupMode(automatic: false);
            if (!saved.Ok) return saved;
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
