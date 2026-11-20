#!/usr/bin/env python3
"""Part5.1 lexical Settings mutations;exact byte restoration,not C#/provider/WPF proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(file, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / file, token))
    add('Area11.Core/Services/SettingsService.cs', 'k == AuthService.RecoveryWordKey', 'return _db.Snapshot(connection =>',
        'RequireWriter(connection, actor, selected);', 'baselines[key] = SettingFingerprint(connection, key);',
        'if (key == SyncService.KeyKey)', 'else values[key] = raw ?? Defaults[key];',
        '_db.RequireWriteTransaction(connection);', '!ReferenceEquals(expected.Database, _db)',
        '!expected.Baselines.TryGetValue(key, out var old) || old != SettingFingerprint(connection, key)',
        'if (expected is not null) RequireEdit(connection, expected, checkedKeys);',
        'values.Keys.Concat(values.Keys.Any(IsCloudConfiguration)', 'prepared.Where(p => p.Key != SyncService.KeyKey)',
        'savedForm = expected.WithSaved(visible, baselines, hasKey);', '}, outcome);', 'committed = true;',
        'NoCommitConfirmed = !committed && outcome.NoCommitConfirmed')
    add('Area11.Core/Models/SettingsEditForm.cs', 'var nextBaselines = Baselines.ToDictionary',
        'foreach (var pair in baselines) nextBaselines[pair.Key] = pair.Value;')
    add('Area11.Desktop/ViewModels/SettingsViewModel.cs', '_services.Settings.LoadEditForm(_user, keys)',
        'var settings = loaded.Value;', '_services.Settings.SaveEditForm(values, _user, form)',
        'form.HasCloudKey', 'form = result.SettingsEdit;', '_services.AppLabels.Reset(_user, _editForm)',
        'if (cloudChanged) _editForm = null;')
    add('Area11.Core/Services/StartupService.cs', '_settings.CheckStartupEdit(actor, expected)', '_settings.Set(ModeKey, clean, actor, expected)')
    add('Area11.Core/Utils/ServiceResult.cs', '[System.Text.Json.Serialization.JsonIgnore]')
    add('Area11.Tests/SettingsEditConcurrencyTests.cs', '.Take(c, "sale")')
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_settings_edit_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_SETTINGS_EDIT_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_settings_edit_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact bytes restored.NOT C#/provider/WPF proof.')


if __name__ == '__main__': run()
