#!/usr/bin/env python3
"""D-119/C + SetMode lexical mutations; exact restoration. NOT C#/Windows/provider/WPF proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1] / 'dotnet'


def run():
    cases = []
    def missing(path, tokens):
        for token in tokens: cases.append((ROOT / path, token, 'REMOVED_STARTUP_RUNTIME_CONTRACT'))
    missing('Area11.Core/Services/SettingsService.cs', [
        'JsonSerializer.Deserialize<string>(raw ?? "null")',
        'mode is StartupService.Manual or StartupService.Auto or StartupService.Protected',
        '? ServiceResult<string>.Success(mode)',
        'return ServiceResult<string>.Fail("The startup mode could not be read.");'])
    missing('Area11.Core/Services/StartupService.cs', [
        'var current = _settings.ReadCurrentStartupMode();', 'lock (_modeGate)',
        'return new StartupModeStatus(current.Value!, true, string.Empty);', 'if (!_modeWarning)',
        '_modeWarning = false;', '_modeWarning = true;', 'return new StartupModeStatus(Manual, false,',
        'protected close is OFF.', 'saved mode was not changed;', 'Separate screen lock still applies.',
        'public string Mode => ReadStatus().Mode;', 'public bool IsProtected => ReadStatus().IsProtected;',
        'public bool WantsAutoStart => ReadStatus().WantsAutoStart;',
        'public sealed record StartupModeStatus(string Mode, bool Verified, string Notice)',
        'if (!_compatibilityAttempt.Ready) return ServiceResult.Fail(_compatibilityHint);',
        'AutoStartEntry.Command(exePath)', 'var form = expected;', '_settings.LoadEditForm(actor, new[] { ModeKey })',
        '_settings.CheckStartupEdit(actor, form)', 'if (!_compatibilityAttempt.TryBegin())',
        '_machine is ICheckedAutoStartStore && _userStore is ICheckedAutoStartStore',
        '? SetModeChecked(actor, clean, exePath, form)', ': SetLegacyMode(actor, clean, exePath, form)',
        '_compatibilityAttempt.ObserveFailure(saved);', '_compatibilityAttempt.AcknowledgeCommit();',
        'saved.SettingsEdit is not { } receipt || ReferenceEquals(receipt, form) || receipt.GetString(ModeKey) != clean',
        '_compatibilityAttempt.CompleteReset();', '_compatibilityAttempt.ObserveException();', '_compatibilityAttempt.WasAcknowledged',
        'var machineBefore = _machine.IsEnabled();', '_ = _userStore.IsEnabled();',
        'if (machineBefore || _machine.IsEnabled()) throw;', 'if (!userScope) _userStore.Disable();',
        'if (!PresenceMatches()) throw',
        '_settings.SaveEditForm(new Dictionary<string, object?> { [ModeKey] = clean }, actor, form)',
        'Windows entries were not restored: this legacy host cannot restore exact old values.',
        'NoCommitConfirmed = !attempted', 'settingsSaved ? "SAVED. Startup mode was stored;',
        'Nothing was changed in startup settings. Windows entries may have changed;'])
    for name in ('LoginWindow', 'ShellWindow'):
        missing('Area11.Desktop/Views/' + name + '.xaml.cs', [
            'StartupModeNotice.Text = status.Notice;',
            'StartupModeNotice.Visibility = status.Verified ? Visibility.Collapsed : Visibility.Visible;'])
        missing('Area11.Desktop/Views/' + name + '.xaml', ['x:Name="StartupModeNotice"'])
    missing('Area11.Desktop/Views/LoginWindow.xaml.cs', [
        'if (!App.IsSystemShutdown && !AllowClose) ApplyKioskIfAutoMode();',
        'if (!status.WantsAutoStart)', 'if (_startupKioskHidden)', '_startupKioskHidden = false;',
        'Topmost = false;', 'WindowStyle = WindowStyle.SingleBorderWindow;', 'WindowState = WindowState.Normal;',
        'if (!_startupKioskHidden)', '_startupKioskHidden = true;',
        'if (!ReadStartupStatus().IsProtected && !ReadFocusGuard())'])
    missing('Area11.Desktop/Views/ShellWindow.xaml.cs', [
        '(!ReadStartupStatus(shell).IsProtected && !ReadFocusGuard(shell))',
        'ReadStartupStatus(shell); // Same window timer/load/activation'])
    missing('Area11.Desktop/App.xaml.cs', ['&& startupStatus.Verified && startupStatus.IsProtected)'])
    for forbidden in ('RestoreQuietly(_machine, true, exePath);', 'DisableQuietly(_machine);',
                      'var syntheticPresence = EnabledOf(_machine);'):
        cases.append((ROOT / 'Area11.Core/Services/StartupService.cs',
            '            var machineBefore = _machine.IsEnabled();',
            '            ' + forbidden + '\n            var machineBefore = _machine.IsEnabled();'))
    original = {p: p.read_bytes() for p, _, _ in cases}
    try:
        checks.check_startup_runtime_compatibility(); assert not checks.FAILURES, checks.FAILURES
        for path, token, replacement in cases:
            text = original[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, replacement))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_startup_runtime_compatibility()
            assert checks.FAILURES, 'Survived: ' + token
            path.write_bytes(original[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in original.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in original.items())
    print(f'PASS {len(cases)} startup runtime/compat lexical mutations rejected; exact bytes restored. NOT C#/Windows/provider/WPF proof.')


if __name__ == '__main__': run()
