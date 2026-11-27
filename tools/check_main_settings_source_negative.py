#!/usr/bin/env python3
"""Main Settings lexical mutations only; exact restoration. NOT WPF/provider/C# execution."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1] / 'dotnet'


def run():
    vm = ROOT / 'Area11.Desktop/ViewModels/SettingsViewModel.cs'
    view = ROOT / 'Area11.Desktop/Views/SettingsView.xaml'
    tokens = [
        'if (!_mainSettingsAttempt.Ready) { SetStatus(_mainSettingsHint, true); return; }',
        'var startupChoice = SelectedStartupMode;', 'var focusChoice = FocusGuardEnabled;',
        'var canChangeStartup = CanChangeStartup;', 'if (!_mainSettingsAttempt.TryBegin()) return;',
        'var loadedForm = form;', 'string? primaryFailure = null;', 'string? laterFailure = null;',
        '_services.Settings.SaveEditForm(values, _user, form)', 'primaryFailure = result.Error;',
        '_mainSettingsAttempt.ObserveFailure(result);', 'if (!result.NoCommitConfirmed) _editForm = null;',
        '_mainSettingsAttempt.AcknowledgeCommit();', 'savedStages = "Main settings SAVED.";',
        'form is null || ReferenceEquals(form, loadedForm)',
        'values.Keys.Any(key => key != SyncService.KeyKey && primaryReceipt.GetRaw(key) == "null")',
        'var primaryReceipt = form;', '_syncKey = string.Empty;', '_syncKeyCleared = false;',
        '_confirmLabelReset = false; _labelResetForm = null;', 'OnPropertyChanged(nameof(SyncKey));',
        'ExpirySummary = form.ExpiryBands.Summary;', 'LoadLabels();', '_onSaved?.Invoke();',
        'RequireMainSettingsHandoff(primaryReceipt);', '!values.ContainsKey(SyncService.EnabledKey)',
        'primaryReceipt.GetBool(SyncService.EnabledKey)', 'if (canChangeStartup && startupChoice is not null',
        'laterFailure = _startupSaveHint;', 'savedStages += " Startup SAVED.";',
        'if (canChangeStartup && focusChoice != form.GetBool(FocusGuardService.GuardKey))',
        '[FocusGuardService.GuardKey] = focusChoice', 'laterFailure = "Screen-lock result: " + focus.Error;',
        'savedStages += " Screen lock SAVED.";',
        'focus.SettingsEdit is not { } focusReceipt || focusReceipt.GetBool(FocusGuardService.GuardKey) != focusChoice',
        '_focusGuardEnabled = focusChoice;', 'primaryReceipt.GetInt("printer.width")',
        'primaryReceipt.GetBool("printer.autoPrint", true)', 'LockdownChromeChanged?.Invoke();',
        'RequireMainSettingsHandoff(form);', 'FocusGuardEnabled != focusChoice', '_mainSettingsAttempt.CompleteReset();',
        '_mainSettingsAttempt.ObserveException();', 'if (!_mainSettingsAttempt.Ready) _editForm = null;',
        'primaryFailure is not null', 'laterFailure is not null',
        '!ReferenceEquals(_editForm, receipt) || _syncKey.Length != 0 || _syncKeyCleared',
        'row.Text == receipt.GetString(definition.SettingKey) && row.DefaultText == definition.DefaultText) != 1'
    ]
    cases = [(vm, token, 'REMOVED_MAIN_SETTINGS_CONTRACT') for token in tokens]
    cases += [(view, token, 'REMOVED_MAIN_SETTINGS_NOTICE') for token in (
        'Text="{Binding MainSettingsHint}"', 'Visibility="{Binding HasMainSettingsHint, Converter={StaticResource BoolToVis}}"')]
    for forbidden in ('Load();', '_services.Settings.LoadEditForm(_user, values.Keys);', 'SyncKey = string.Empty;'):
        cases.append((vm, '            var result = _services.Settings.SaveEditForm(values, _user, form);',
                      '            ' + forbidden + '\n            var result = _services.Settings.SaveEditForm(values, _user, form);'))
    original = {p: p.read_bytes() for p, _, _ in cases}
    try:
        checks.check_main_settings_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token, replacement in cases:
            text = original[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, replacement))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_main_settings_outcomes()
            assert checks.FAILURES, 'Survived: ' + token
            path.write_bytes(original[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in original.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in original.items())
    print(f'PASS {len(cases)} main Settings lexical mutations rejected; exact bytes restored. NOT WPF/provider/C# proof.')


if __name__ == '__main__': run()
