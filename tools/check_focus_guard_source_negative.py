#!/usr/bin/env python3
"""D-118/A lexical mutations. Exact bytes restored. NOT C#/WPF/Windows/provider proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1] / 'dotnet'


def run():
    cases = []
    def missing(path, tokens):
        for token in tokens: cases.append((ROOT / path, token, 'REMOVED_FOCUS_CONTRACT'))
    missing('Area11.Core/Services/SettingsService.cs', [
        'return raw?.Trim() switch', '"true" or "1" => ServiceResult<bool>.Success(true)',
        '"false" or "0" => ServiceResult<bool>.Success(false)',
        '_ => ServiceResult<bool>.Fail("The screen-lock setting is missing or invalid.")',
        'return ServiceResult<bool>.Fail("The screen-lock setting could not be read.");'])
    missing('Area11.Core/Services/FocusGuardService.cs', [
        'private bool? _lastVerifiedEnabled;', 'public bool Enabled => ReadStatus().Enabled;',
        'var current = _settings.ReadCurrentFocusGuard();', 'lock (_stateGate)', 'if (current.Ok)',
        '_lastVerifiedEnabled = current.Value;', '_readWarning = false;', 'if (!_readWarning)', '_readWarning = true;',
        'return new FocusGuardStatus(current.Value, true, string.Empty);',
        'var enabled = _lastVerifiedEnabled ?? true;', 'Keeping the last verified setting ',
        'No verified setting yet; using ON temporarily.', 'return new FocusGuardStatus(enabled, false,',
        'This is not a Windows security lock.',
        'public sealed record FocusGuardStatus(bool Enabled, bool Verified, string Notice);'])
    for name in ('ShellWindow', 'LoginWindow'):
        missing('Area11.Desktop/Views/' + name + '.xaml.cs', [
            'Interval = TimeSpan.FromSeconds(5)', '_focusGuardTimer.Tick += RefreshFocusGuard;',
            '_focusGuardTimer.Start();', '_focusGuardTimer.Stop();', '_focusGuardTimer.Tick -= RefreshFocusGuard;',
            'FocusGuardNotice.Text = status.Notice;',
            'FocusGuardNotice.Visibility = status.Verified ? Visibility.Collapsed : Visibility.Visible;', 'return status.Enabled;',
            'Dispatcher.BeginInvoke(new Action(Close));'])
        missing('Area11.Desktop/Views/' + name + '.xaml', [
            'x:Name="FocusGuardNotice"', 'AutomationProperties.Name="Screen-lock verification warning"'])
    missing('Area11.Desktop/Views/ShellWindow.xaml.cs', [
        '(!shell.Services.Startup.IsProtected && !ReadFocusGuard(shell))',
        'DataContext is not ShellViewModel shell || !ReadFocusGuard(shell)',
        'App.IsSystemShutdown || AllowClose || !ReadFocusGuard(shell)', 'if (ReadFocusGuard(shell))',
        'if (!App.IsSystemShutdown && !AllowClose && WindowState == WindowState.Minimized',
        '&& DataContext is ShellViewModel shell && ReadFocusGuard(shell)',
        'Area11 will try to record the away episode when you return.'])
    missing('Area11.Desktop/Views/LoginWindow.xaml.cs', [
        'if (!_viewModel.Services.Startup.IsProtected && !ReadFocusGuard())', 'Loaded += RefreshFocusGuard;'])
    missing('Area11.Desktop/Views/SettingsView.xaml', [
        'reset startup protection only; screen lock is unchanged.',
        'best-effort black-box records, not guaranteed evidence.', 'keeps the last verified choice;', 'before the first verification it uses ON.'])
    missing('Area11.Desktop/App.xaml.cs', [
        'A reset to manual startup was requested', 'Screen lock, if enabled, is unchanged.', 'a failed reset must not be treated as completed.'])
    cases += [(ROOT / 'Area11.Desktop/ViewModels/SettingsViewModel.cs',
        '                LockdownChromeChanged?.Invoke();\n                OnPropertyChanged(nameof(FocusGuardEnabled));',
        '                OnPropertyChanged(nameof(FocusGuardEnabled));\n                LockdownChromeChanged?.Invoke();')]
    original = {p: p.read_bytes() for p, _, _ in cases}
    try:
        checks.check_focus_guard_read_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token, replacement in cases:
            text = original[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, replacement))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_focus_guard_read_outcomes()
            assert checks.FAILURES, 'Survived: ' + token
            path.write_bytes(original[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in original.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in original.items())
    print(f'PASS {len(cases)} Focus Guard lexical mutations rejected; exact bytes restored. NOT C#/WPF/Windows/provider proof.')


if __name__ == '__main__': run()
