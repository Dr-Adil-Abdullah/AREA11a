#!/usr/bin/env python3
"""Lexical part5.1 regression mutations;always restore bytes. NOT C#/WPF/provider proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(file, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / file, token))
    add('Area11.Desktop/ViewModels/BackupViewModel.cs', '_services.Settings.LoadEditForm(_user, ScheduleKeys)',
        'ShowSchedule(_scheduleForm);', 'EveryHoursText = KeepText = string.Empty;', 'if (_scheduleForm is null)',
        'if (_savingSchedule) return;', 'finally { _savingSchedule = false; }',
        '_services.Settings.SaveEditForm(values, _user, _scheduleForm)', 'if (!result.NoCommitConfirmed) _scheduleForm = null;',
        'saved = true;', '_scheduleForm = result.SettingsEdit;', '_services.Backup.RunAutomaticIfDue();')
    add('Area11.Core/Services/EditConcurrency.cs', '"pricing" => null,', 'if (scope == "pricing")',
        'SELECT id,role,active FROM users WHERE id=@id;', 'SELECT * FROM user_pricing_allowances WHERE user_id=@id;')
    add('Area11.Core/Services/PricingAllowanceService.cs', 'return _db.Snapshot(connection =>',
        'OwnerAccess.Require(_db, connection, actor);', 'allowance.EditVersion = EditConcurrency.Capture',
        '!actor.Active || !actor.IsOwner || owner is not { IsOwner: true, Active: true }',
        'EditConcurrency.Require(_db, connection, "pricing", value.UserId, expected);',
        '_audit.Record(connection, owner, "Users",', 'EditVersion = EditConcurrency.Capture(_db, connection, "pricing", value.UserId)',
        '}, outcome);', 'NoCommitConfirmed = outcome.NoCommitConfirmed')
    add('Area11.Core/Models/PricingModels.cs', '[System.Text.Json.Serialization.JsonIgnore]')
    add('Area11.Desktop/ViewModels/SettingsViewModel.cs', '_pricingVersion = allowance.EditVersion;', 'if (_pricingVersion is null)',
        '}, _pricingVersion);', 'if (result.Ok) _pricingVersion = result.EditVersion;',
        'else if (!result.NoCommitConfirmed) _pricingVersion = null;', '_pricingVersion = null;')
    add('Area11.Tests/SettingsSchedulePricingConcurrencyTests.cs', 'Assert.Equal(before, Snapshot(t));')
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_schedule_pricing_edit_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_SCHEDULE_PRICING_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_schedule_pricing_edit_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact bytes restored.NOT C#/provider/WPF proof.')


if __name__ == '__main__': run()
