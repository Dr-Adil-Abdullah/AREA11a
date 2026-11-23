#!/usr/bin/env python3
"""Lexical Workbook Settings mutations;exact restore.NOT .NET/Excel/provider/WPF proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(file, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / file, token))
    add('Area11.Core/Services/WorkbookService.cs', 'return _db.Snapshot(connection =>', 'SourceDatabase = _db',
        '_settings.LoadEditForm(user, SettingsService.WorkbookEditKeys(preview.SettingValues.Keys))', 'preview.SettingsEdit = loaded.Value;',
        'lock (preview) return ApplyBulkOnce(user, preview);', 'if (preview.ApplyBlocked)',
        'if (hasSettings && !ReferenceEquals(preview.SourceDatabase, _db))', 'preview.ApplyBlocked = true;',
        '_settings.RequireWorkbookEdit(connection, preview.SettingsEdit, preview.SettingValues.Keys)',
        '_settings.PrepareBulk(preview.SettingValues, connection)', '_settings.PrepareBulk(values, connection);',
        '}, observation);', 'committed = true;', 'if (observation.NoCommitConfirmed) preview.ApplyBlocked = false;',
        'NoCommitConfirmed = observation.NoCommitConfirmed', 'if (committed && applied is not null)')
    add('Area11.Core/Services/SettingsService.cs', 'selected.UnionWith(new[] { "discount.maxPercentCashier", "discount.maxPercentManager" });',
        'selected.UnionWith(new[] { "supplier.returnCutoffDays", "supplier.returnAlertDays" });',
        'RequireEdit(connection, expected, WorkbookEditKeys(keys));', 'Guard(pair.Key, pair.Value, values, connection)',
        '_db.QuerySingle<string>(connection, "SELECT value FROM settings WHERE key=@key;", new { key = otherKey })')
    add('Area11.Core/Models/WorkbookModels.cs', 'internal SettingsEditForm? SettingsEdit { get; set; }', 'internal bool ApplyBlocked { get; set; }')
    add('Area11.Desktop/ViewModels/ImportExportViewModel.cs', 'if (!_bulkApplyAttempt.Ready)', 'if (!_bulkApplyAttempt.TryBegin())',
        '_bulkApplyAttempt.ObserveFailure(result);', '_bulkApplyAttempt.AcknowledgeCommit();', '_bulkApplyAttempt.CompleteReset();', '_bulkApplyAttempt.ObserveException();')
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_workbook_settings_edit_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_WORKBOOK_SETTINGS_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_workbook_settings_edit_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} mutations rejected;exact bytes restored.NOT runtime/provider/Excel/WPF proof.')


if __name__ == '__main__': run()
