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

    private readonly object _modeGate = new();
    private bool _modeWarning;

    /// <summary>D-119/C: current verified mode, otherwise TEMPORARY manual with a warning.
    /// Never uses a stale cache, retains an old protected mode, or repairs the saved switch.</summary>
    public StartupModeStatus ReadStatus()
    {
        var current = _settings.ReadCurrentStartupMode();
        lock (_modeGate)
        {
            if (current.Ok)
            {
                _modeWarning = false;
                return new StartupModeStatus(current.Value!, true, string.Empty);
            }
            if (!_modeWarning)
            {
                _log.Warn("Startup mode is unverified; using manual temporarily under owner decision D-119/C.");
                _modeWarning = true;
            }
            return new StartupModeStatus(Manual, false,
                "Startup mode could not be verified. Using manual temporarily: startup-mode protected close is OFF. The saved mode was not changed; Windows entries may still start Area11. Separate screen lock still applies. Ask the owner to review Settings and the database connection.");
        }
    }

    public string Mode => ReadStatus().Mode;
    public bool IsProtected => ReadStatus().IsProtected;
    public bool WantsAutoStart => ReadStatus().WantsAutoStart;

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
            try
            {
                var machinePresent = _machine.IsEnabled();
                var userPresent = _userStore.IsEnabled();
                if (machinePresent && userPresent) return "Legacy host reports both Windows start-up entries. Verify both before changing startup.";
                if (machinePresent) return "Applies to every Windows account on this PC. Legacy host reports presence only; command and kind are not verified.";
                if (userPresent) return "Applies to this Windows account only. Legacy host reports presence only; command and kind are not verified.";
                return "Legacy host reports no start-up entry; exact Windows values are not verified.";
            }
            catch (Exception) { return "Windows start-up entries could not be read. Verify access and both entries."; }
        }
    }

    /// <summary>Current runtime mode is not a saved receipt or verified Windows command.</summary>
    public string ModeLabel
    {
        get
        {
            var status = ReadStatus();
            if (!status.Verified) return "Startup mode is unverified; using manual temporarily. Separate screen lock still applies.";
            return status.Mode switch
            {
                Auto => "Starts with Windows; closes normally.",
                Protected => "Starts with Windows; closing needs the password.",
                _ => "Starts and closes by hand."
            };
        }
    }

    private readonly PostingAttemptGuard _compatibilityAttempt = new();
    private string _compatibilityHint = string.Empty;

    /// <summary>Compatibility entry point: checked hosts use the exact-value checked service.
    /// Boolean-only hosts retain their presence-only contract, NEVER attempt guessed restoration,
    /// and block replay on any partial/unknown outcome. No durable identity or installer change.</summary>
    public ServiceResult SetMode(User actor, string mode, string exePath, SettingsEditForm? expected = null)
    {
        static ServiceResult Refuse(string message) => new() { Error = message, NoCommitConfirmed = true };
        if (!_compatibilityAttempt.Ready) return ServiceResult.Fail(_compatibilityHint);
        if (actor is null || !actor.IsOwner) return Refuse("Only the owner can change how Area11 starts and closes.");
        var access = _settings.CheckAccess(actor);
        if (!access.Ok) return Refuse(access.Error ?? "Current Owner access is required.");
        var clean = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (clean is not (Manual or Auto or Protected)) return Refuse("The startup mode must be manual, auto or protected.");
        try { if (clean != Manual) _ = AutoStartEntry.Command(exePath); }
        catch (Exception) { return Refuse("The program's own unquoted path is needed for the start-up entry."); }
        var form = expected;
        if (form is null)
        {
            var loaded = _settings.LoadEditForm(actor, new[] { ModeKey });
            if (!loaded.Ok || loaded.Value is null) return Refuse(loaded.Error ?? "Current Owner and startup mode are required.");
            form = loaded.Value;
        }
        var baseline = _settings.CheckStartupEdit(actor, form);
        if (!baseline.Ok) return Refuse(baseline.Error ?? "Review the startup mode before changing it.");
        if (!_compatibilityAttempt.TryBegin()) return ServiceResult.Fail(_compatibilityHint);
        _compatibilityHint = "Startup change is already running. Wait for its result; do not repeat it.";
        try
        {
            var saved = _machine is ICheckedAutoStartStore && _userStore is ICheckedAutoStartStore
                ? SetModeChecked(actor, clean, exePath, form)
                : SetLegacyMode(actor, clean, exePath, form);
            if (!saved.Ok)
            {
                _compatibilityAttempt.ObserveFailure(saved);
                _compatibilityHint = saved.Error ?? "Startup change is unconfirmed. Verify before retrying.";
                return saved;
            }
            _compatibilityAttempt.AcknowledgeCommit();
            if (saved.SettingsEdit is not { } receipt || ReferenceEquals(receipt, form) || receipt.GetString(ModeKey) != clean)
                throw new InvalidOperationException("The compatibility startup receipt is missing or mismatched.");
            _compatibilityAttempt.CompleteReset();
            return saved;
        }
        catch (Exception exception)
        {
            _compatibilityAttempt.ObserveException();
            _log.Error("Compatibility startup change did not finish. Exception type: " + exception.GetType().Name);
            _compatibilityHint = _compatibilityAttempt.WasAcknowledged
                ? "SAVED. Startup mode was stored, but handoff did not finish. Verify Settings and both Windows entries before reopening; do not repeat the change."
                : "Startup change is unconfirmed. Verify Settings and both Windows entries before reopening or retrying.";
            return ServiceResult.Fail(_compatibilityHint);
        }
    }

    private ServiceResult SetLegacyMode(User actor, string clean, string exePath, SettingsEditForm form)
    {
        var attempted = false;
        var settingsStarted = false;
        var settingsSaved = false;
        try
        {
            // Reads may throw. Never reinterpret an unreadable legacy host as an absent entry.
            var machineBefore = _machine.IsEnabled();
            _ = _userStore.IsEnabled();
            var userScope = false;
            if (clean == Manual)
            {
                attempted = true;
                _machine.Disable();
                _userStore.Disable();
            }
            else
            {
                try
                {
                    attempted = true;
                    _machine.Enable(exePath);
                }
                catch (Exception exception) when (IsAccessRefusal(exception))
                {
                    if (machineBefore || _machine.IsEnabled()) throw;
                    _userStore.Enable(exePath);
                    userScope = true;
                }
                // Keep cleanup outside the enable-refusal catch: its refusal is NOT fallback.
                if (!userScope) _userStore.Disable();
            }
            bool PresenceMatches() => clean == Manual ? !_machine.IsEnabled() && !_userStore.IsEnabled()
                : userScope ? !_machine.IsEnabled() && _userStore.IsEnabled()
                : _machine.IsEnabled() && !_userStore.IsEnabled();
            if (!PresenceMatches()) throw new InvalidOperationException("Legacy startup presence did not match.");
            settingsStarted = true;
            var saved = _settings.SaveEditForm(new Dictionary<string, object?> { [ModeKey] = clean }, actor, form);
            if (!saved.Ok)
                return new ServiceResult { Error = (saved.Error ?? "Startup setting saving is unconfirmed.")
                    + " Windows entries were not restored: this legacy host cannot restore exact old values. Verify both entries before reopening; do not repeat the change." };
            settingsSaved = true;
            if (!PresenceMatches()) throw new InvalidOperationException("Legacy startup presence changed after saving.");
            return new ServiceResult { Ok = true, SettingsEdit = saved.SettingsEdit, Warnings = userScope
                ? new[] { "No administrator was available, so this applies to this Windows account only. Legacy host verifies presence only, not the exact command or kind." }
                : Array.Empty<string>() };
        }
        catch (Exception exception)
        {
            _log.Error("Legacy startup change did not finish. Exception type: " + exception.GetType().Name);
            return new ServiceResult { NoCommitConfirmed = !attempted,
                Error = settingsStarted
                    ? (settingsSaved ? "SAVED. Startup mode was stored; " : "Startup setting saving is unconfirmed; ")
                        + "Windows entries were not restored. Verify Settings and both entries before reopening or retrying."
                    : "Nothing was changed in startup settings. Windows entries may have changed; verify both before reopening or retrying. Check Windows administrator access." };
        }
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

    private readonly PostingAttemptGuard _recoveryAttempt = new();
    private string _recoveryHint = string.Empty;

    /// <summary>Shift recovery prioritizes manual mode even when Windows entries are stuck.
    /// Manual + protection_reset audit commit FIRST; registry cleanup is separate best-effort.
    /// Checked stores compare captured values, never restore or overwrite a changed entry.
    /// Unknown/partial/SAVED-handoff state blocks this service instance, not a durable identity.</summary>
    public ServiceResult ResetToManual()
    {
        if (!_recoveryAttempt.TryBegin()) return ServiceResult.Fail(_recoveryHint);
        _recoveryHint = "Startup recovery is already running. Wait for its result; do not repeat it.";
        try
        {
            var machineBefore = CaptureRecoveryEntry(_machine);
            var userBefore = CaptureRecoveryEntry(_userStore);
            var saved = _settings.ResetStartupForRecovery();
            if (!saved.Ok)
            {
                _recoveryAttempt.ObserveFailure(saved);
                _recoveryHint = saved.Error ?? "Startup recovery is unconfirmed. Verify before retrying.";
                // No Windows mutation happened, even if the DB result is unknown.
                return new ServiceResult { Error = _recoveryHint, NoCommitConfirmed = saved.NoCommitConfirmed };
            }
            _recoveryAttempt.AcknowledgeCommit();
            var receipt = saved.SettingsEdit;
            if (receipt is null || receipt.GetRaw(ModeKey) != "\"manual\"")
                throw new InvalidOperationException("The recovery receipt is missing or mismatched.");

            var warnings = new List<string>();
            CleanupRecoveryEntry(_machine, machineBefore, receipt, "machine-wide", warnings);
            CleanupRecoveryEntry(_userStore, userBefore, receipt, "this-account", warnings);
            var machineAbsent = VerifyRecoveryEntry(_machine);
            var userAbsent = VerifyRecoveryEntry(_userStore);
            if (!machineAbsent || !userAbsent)
                warnings.Add("One or both Windows entries remain or cannot be verified. Area11 may still start at Windows sign-in.");
            if (!_settings.MatchesStartupRecovery(receipt))
                warnings.Add("Startup settings changed after the reset or cannot be verified. The saved receipt is not the current state; review Settings before retrying.");
            if (warnings.Count > 0)
                warnings.Add("Verify settings and both Windows entries before reopening; do not repeat recovery blindly.");
            if (warnings.Count == 0) _recoveryAttempt.CompleteReset();
            else _recoveryHint = "SAVED. Manual startup and its audit were stored. " + string.Join(" ", warnings);
            return new ServiceResult { Ok = true, SettingsEdit = receipt, Warnings = warnings.ToArray() };
        }
        catch (Exception exception)
        {
            _recoveryAttempt.ObserveException();
            _log.Error("Startup recovery/handoff did not finish. Exception type: " + exception.GetType().Name);
            _recoveryHint = _recoveryAttempt.WasAcknowledged
                ? "SAVED. Manual startup and its audit were stored; Windows cleanup or handoff did not finish. Verify settings and both Windows entries before reopening; do not repeat recovery."
                : "Startup recovery is unconfirmed. Verify settings, the black box and both Windows entries before reopening or retrying.";
            return ServiceResult.Fail(_recoveryHint);
        }
    }

    private static AutoStartEntry? CaptureRecoveryEntry(IAutoStartStore store)
    {
        try { return store is ICheckedAutoStartStore checkedStore ? checkedStore.Capture() : null; }
        catch (Exception) { return null; }
    }

    private void CleanupRecoveryEntry(IAutoStartStore store, AutoStartEntry? before,
        SettingsEditForm receipt, string scope, List<string> warnings)
    {
        if (!_settings.MatchesStartupRecovery(receipt))
        {
            warnings.Add("The " + scope + " Windows cleanup was skipped: startup settings changed or could not be verified.");
            return;
        }
        try
        {
            if (store is ICheckedAutoStartStore checkedStore)
            {
                if (before is null)
                {
                    warnings.Add("The " + scope + " Windows entry could not be captured; it was not changed.");
                    return;
                }
                checkedStore.Replace(before, AutoStartEntry.Missing);
                if (!checkedStore.Capture().Matches(AutoStartEntry.Missing))
                    throw new InvalidOperationException("Windows cleanup readback did not match.");
            }
            else
            {
                // Compatibility only. A boolean-only store cannot prove exact presence/readback.
                store.Disable();
                warnings.Add("The " + scope + " Windows cleanup was attempted through a legacy host and cannot be fully verified.");
            }
        }
        catch (Exception exception)
        {
            _log.Error("Startup recovery Windows cleanup did not finish. Exception type: " + exception.GetType().Name);
            warnings.Add("The " + scope + " Windows entry was not verified as removed; it may remain or have changed. No restoration was attempted.");
        }
    }

    private static bool VerifyRecoveryEntry(IAutoStartStore store)
    {
        try { return store is ICheckedAutoStartStore checkedStore && checkedStore.Capture().Matches(AutoStartEntry.Missing); }
        catch (Exception) { return false; }
    }
}

/// <summary>One current read and D-119/C effective policy; not a saved-settings or Windows receipt.</summary>
public sealed record StartupModeStatus(string Mode, bool Verified, string Notice)
{
    public bool IsProtected => Mode == StartupService.Protected;
    public bool WantsAutoStart => Mode is StartupService.Auto or StartupService.Protected;
}
