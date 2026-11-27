#!/usr/bin/env python3
"""Original-words reset lexical mutations; restore exact bytes. NOT C#/WPF/provider evidence."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1] / 'dotnet'


def run():
    cases = []
    def add(path, *tokens):
        for token in tokens: cases.append((ROOT / path, token, 'REMOVED_LABEL_RESET_CONTRACT'))
    add('Area11.Core/Services/AppLabelService.cs', 'expected is null', 'NoCommitConfirmed = true',
        '_settings.SaveEditForm(Catalog.ToDictionary(', 'definition => (object?)definition.DefaultText')
    vm = 'Area11.Desktop/ViewModels/SettingsViewModel.cs'
    add(vm, 'if (_resettingLabels || _savingSettings || _changingIcon) return;',
        'if (!_labelResetAttempt.Ready)', '_resettingLabels = true;', 'var writerStarted = false;',
        'if (!_confirmLabelReset || !ReferenceEquals(_labelResetForm, form))',
        '_labelResetForm = form;', '_confirmLabelReset = true;',
        'Unsaved word edits will be replaced; other settings are not saved by this button.',
        'if (!_labelResetAttempt.TryBegin()) return;', 'writerStarted = true;',
        '_services.AppLabels.ResetChecked(_user, form)', '_labelResetAttempt.ObserveFailure(result);',
        'serviceFailure = result.Error;', 'result.NoCommitConfirmed', '_editForm = null;',
        '_labelResetAttempt.AcknowledgeCommit();', 'var receipt = result.SettingsEdit;',
        'receipt is null || AppLabelService.Definitions.Any(definition =>',
        'receipt.GetString(definition.SettingKey) != definition.DefaultText', '_editForm = receipt;',
        '_confirmLabelReset = false; _labelResetForm = null;', 'LoadLabels();', '_onSaved?.Invoke();',
        'SetStatus("SAVED. Original application words restored.',
        '!ReferenceEquals(_editForm, receipt) || _confirmLabelReset || _labelResetForm is not null',
        'LabelRows.Count != AppLabelService.Definitions.Count',
        'AppLabelService.Definitions.Any(definition => LabelRows.Count(row => row.Key == definition.Key',
        'row.Text == definition.DefaultText && row.DefaultText == definition.DefaultText) != 1',
        '_labelResetAttempt.CompleteReset();', '_labelResetAttempt.ObserveException();',
        'if (writerStarted) _editForm = null;', 'finally { _resettingLabels = false; }',
        'if (_resettingLabels) return;',
        'if (!_labelResetAttempt.Ready) { SetStatus(_labelResetHint, true); return; }',
        'public string LabelResetHint => _labelResetHint;')
    add('Area11.Desktop/Views/SettingsView.xaml', 'Text="{Binding LabelResetHint}"',
        'Visibility="{Binding HasLabelResetHint, Converter={StaticResource BoolToVis}}"')
    for forbidden in ('Load();', '_services.AppLabels.Reset(_user, form);', '_services.Settings.LoadEditForm(_user, new[] { "store.name" });'):
        cases.append((ROOT / vm, '            var result = _services.AppLabels.ResetChecked(_user, form);',
                      '            ' + forbidden + '\n            var result = _services.AppLabels.ResetChecked(_user, form);'))
    original = {p: p.read_bytes() for p, _, _ in cases}
    try:
        checks.check_label_reset_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token, replacement in cases:
            text = original[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, replacement))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_label_reset_outcomes()
            assert checks.FAILURES, 'Survived: ' + token
            path.write_bytes(original[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in original.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in original.items())
    print(f'PASS {len(cases)} label-reset mutations rejected; exact bytes restored. NOT C#/WPF/provider proof.')


if __name__ == '__main__': run()
