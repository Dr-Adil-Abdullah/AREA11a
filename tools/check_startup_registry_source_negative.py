#!/usr/bin/env python3
"""Startup lexical mutations only. Exact source bytes restored; NOT Windows/provider/WPF proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1] / 'dotnet'


def run():
    cases = []
    def add(path, *tokens):
        for token in tokens: cases.append((ROOT / path, token, 'REMOVED_STARTUP_CONTRACT'))
    service = 'Area11.Core/Services/StartupService.cs'
    add(service, 'if (expected is null)', '_settings.CheckStartupEdit(actor, expected)',
        '_machine is not ICheckedAutoStartStore machine || _userStore is not ICheckedAutoStartStore user',
        'desired = clean == Manual ? AutoStartEntry.Missing : AutoStartEntry.Command(exePath);',
        'machineBefore = machine.Capture();', 'userBefore = user.Capture();',
        'machineAttempted = true;', 'machine.Replace(machineBefore, machineTarget);',
        'clean != Manual && IsAccessRefusal(exception)',
        'if (machineBefore.IsPresent || !machine.Capture().Matches(machineBefore)) throw;',
        'machineTarget = machineBefore;', 'userTarget = desired;', 'userAttempted = true;',
        'user.Replace(userBefore, userTarget);',
        '!machine.Capture().Matches(machineTarget) || !user.Capture().Matches(userTarget)',
        'settingsStarted = true;',
        '_settings.SaveEditForm(new Dictionary<string, object?> { [ModeKey] = clean }, actor, expected)',
        'if (!saved.NoCommitConfirmed)', '& RestoreChecked(user, userBefore, userTarget, userAttempted);',
        'NoCommitConfirmed = restored', 'settingsSaved = true;',
        'saved.SettingsEdit is null || saved.SettingsEdit.GetString(ModeKey) != clean',
        'if (settingsStarted)', 'if (current.Matches(before)) return true;',
        'if (!attempted || !current.Matches(intended)) return false;', 'store.Replace(intended, before);',
        'return store.Capture().Matches(before);')
    add('Area11.Core/Services/CheckedAutoStartStore.cs', 'public object? Value => Copy(_value);',
        '_value = Copy(value);', 'if (!valid) throw', 'Kind == other.Kind', 'IsPresent == other.IsPresent',
        'bytes.SequenceEqual(next)', 'words.SequenceEqual(next, StringComparer.Ordinal)',
        'byte[] bytes => bytes.ToArray()', 'string[] words => words.ToArray()')
    add('Area11.Desktop/RegistryRunEntry.cs', 'lock (Gate)',
        'GetValueNames().Contains(ValueName, StringComparer.OrdinalIgnoreCase)', 'GetValueKind(ValueName)',
        'RegistryValueOptions.DoNotExpandEnvironmentNames', 'if (!Capture(root).Matches(expected))',
        'if (expected.Matches(replacement)) return;',
        'key.SetValue(ValueName, replacement.Value!, (RegistryValueKind)(int)replacement.Kind)',
        'key.DeleteValue(ValueName, throwOnMissingValue: false)', 'if (!Capture(root).Matches(replacement))')
    vm = 'Area11.Desktop/ViewModels/SettingsViewModel.cs'
    add(vm, 'if (_savingSettings) return;', 'if (!_startupAttempt.Ready)', 'try { SaveCore(); }',
        'finally { _savingSettings = false; }', 'var startupChoice = SelectedStartupMode;',
        'if (!_startupAttempt.TryBegin()) return false;',
        '_services.Startup.SetModeChecked(_user, wanted, Environment.ProcessPath ?? string.Empty, form)',
        'serviceFailure = saved.Error;', '_startupAttempt.ObserveFailure(saved);',
        '_startupAttempt.AcknowledgeCommit();', 'var receipt = saved.SettingsEdit;',
        'receipt is null || receipt.GetString(StartupService.ModeKey) != wanted',
        'SelectedStartupMode = StartupModeOptions.First(option => option.Key == wanted);',
        '!ReferenceEquals(_editForm, receipt) || SelectedStartupMode?.Key != wanted',
        'savedForm = receipt;', '_startupAttempt.CompleteReset();', '_startupAttempt.ObserveException();')
    add('Area11.Desktop/HklmRunStore.cs', 'RegistryRunEntry.Capture(Registry.LocalMachine)',
        'RegistryRunEntry.Replace(Registry.LocalMachine, expected, replacement)')
    add('Area11.Desktop/HkcuRunStore.cs', 'RegistryRunEntry.Capture(Registry.CurrentUser)',
        'RegistryRunEntry.Replace(Registry.CurrentUser, expected, replacement)')
    add('Area11.Desktop/Views/SettingsView.xaml', 'Text="{Binding StartupSaveHint}"')
    cases.append((ROOT / service, 'if (!saved.NoCommitConfirmed)\n                    return',
                  'if (!saved.NoCommitConfirmed)\n                    RestoreChecked(machine, machineBefore, machineTarget, machineAttempted);\n                    return'))
    for blind in ('DisableQuietly(_machine);', 'RestoreQuietly(_machine, true, exePath);', 'EnabledOf(_machine);'):
        cases.append((ROOT / service, '        var machineTarget = desired;',
                      '        ' + blind + '\n        var machineTarget = desired;'))
    original = {p: p.read_bytes() for p, _, _ in cases}
    try:
        checks.check_startup_registry_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token, replacement in cases:
            text = original[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, replacement))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_startup_registry_outcomes()
            assert checks.FAILURES, 'Survived: ' + token
            path.write_bytes(original[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in original.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in original.items())
    print(f'PASS {len(cases)} startup lexical mutations rejected; exact bytes restored. NOT Windows/provider/WPF proof.')


if __name__ == '__main__': run()
