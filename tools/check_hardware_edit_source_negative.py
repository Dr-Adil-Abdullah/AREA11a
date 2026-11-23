#!/usr/bin/env python3
"""Hardware lexical mutations;restores exact bytes even on failure.NOT runtime proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(file, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / file, token))
    add('Area11.Core/Services/HardwareService.cs', '_settings.LoadEditForm(user, EditKeys)',
        'report.SettingsEdit = form;', 'var report = Build(form);', 'var report = Build(expected);',
        'if (_settings.HardwareBaselineRequired)', '_settings.SaveHardwareTest(user, report, expected)',
        'report.SettingsEdit = saved.SettingsEdit;', '_settings.Set("printer.name", wanted, user, expected)',
        'form.GetInt("printer.width", 58)', 'form.GetString("printer.name")', 'form.GetInt("printer.copies", 1)',
        'form.GetBool("printer.autoCut", true)', '_settings.LoadEditForm(user, EditKeys.Concat(new[] { "store.name" }))')
    add('Area11.Core/Services/SettingsService.cs', 'internal bool HardwareBaselineRequired => _db.Sessions.Enabled;',
        'var writer = RequireWriter(connection, actor, HardwareService.EditKeys);',
        'RequireEdit(connection, expected, HardwareService.EditKeys);',
        '_audit.Record(connection, writer, Modules.Settings, "hardware_test", details: report.SummaryText);',
        'return expected.WithSaved(prepared, baselines, null);', 'return new ServiceResult { Ok = true, SettingsEdit = next };',
        'NoCommitConfirmed = !committed && outcome.NoCommitConfirmed')
    add('Area11.Core/Models/HardwareModels.cs', '[System.Text.Json.Serialization.JsonIgnore]')
    add('Area11.Desktop/ViewModels/HardwareViewModel.cs', 'private SettingsEditForm? _edit;',
        '_services.Hardware.ChoosePrinter(_user, wanted, _edit)', '_services.Hardware.Save(_user, passed, NoteText, _edit)',
        'if (!saved.NoCommitConfirmed) _edit = null;', 'finally { _saving = false; }',
        'acknowledged = true;', '_edit = saved.Value.SettingsEdit;', 'NoteText = string.Empty;',
        'SAVED. The test was stored but the page could not refresh.',
        'SAVED. Printer selection stored but the page could not refresh.')
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_hardware_edit_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_HARDWARE_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_hardware_edit_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact bytes restored.NOT C#/provider/WPF/physical proof.')


if __name__ == '__main__': run()
