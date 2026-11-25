#!/usr/bin/env python3
"""Lexical recovery-editor mutations;exact bytes restored.NOT .NET/auth/WPF/provider proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(file, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / file, token))
    add('Area11.Core/Services/AuthService.cs',
        'SetRecoveryWordCore(actor, word, null, checkedEdit: false)',
        'SetRecoveryWordCore(actor, word, expected, checkedEdit: true)',
        'return checkedEdit ? _settings.SaveRecoveryHash(actor, stored, expected)', 'var clean = NormalizeWord(word);')
    add('Area11.Core/Services/SettingsService.cs',
        'SaveRecoveryHashCore(actor, hash, expected, checkedEdit: true)',
        'if (expected is null) throw new ValidationException(',
        'EditConcurrency.Require(_db, connection, "recovery_word", 0, expected);',
        'OwnerAccess.Require(_db, connection, actor);',
        '_audit.Record(connection, owner, "Auth", "recovery_word_changed",',
        'return CaptureRecoveryEdit(connection);', '}, outcome);', 'committedForm = next;',
        'if (committedForm is not null)', 'NoCommitConfirmed = outcome.NoCommitConfirmed',
        'Success(CaptureRecoveryEdit(connection))', 'RecoveryEdit = next, EditVersion = next.EditVersion')
    add('Area11.Core/Services/EditConcurrency.cs', '"recovery_word" => null,', 'if (scope == "recovery_word")',
        "SELECT key,value FROM settings WHERE key='security.recoveryWordHash';")
    add('Area11.Core/Models/RecoveryEditForm.cs', 'public bool UsesFactoryWord { get; }', '[System.Text.Json.Serialization.JsonIgnore]')
    add('Area11.Core/Utils/ServiceResult.cs', 'public Area11.Core.Models.RecoveryEditForm? RecoveryEdit { get; init; }')
    add('Area11.Desktop/ViewModels/SettingsViewModel.cs', 'private RecoveryEditForm? _recoveryEdit;',
        'LoadRecoveryWordEdit();', 'var loaded = _services.Auth.LoadRecoveryEditForm(_user);',
        'var form = _recoveryEdit;', '_services.Auth.SetRecoveryWord(_user, NewRecoveryWord, form.EditVersion)',
        'if (!result.NoCommitConfirmed)', 'acknowledged = true;', '_recoveryEdit = result.RecoveryEdit;',
        '_recoveryEdit = null; _newRecoveryWord = string.Empty;', '_newRecoveryWord = string.Empty;',
        '_recoveryEdit.UsesFactoryWord', 'finally { _savingStaff = false; }')
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_recovery_edit_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_RECOVERY_EDIT_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_recovery_edit_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} mutations rejected;exact bytes restored.NOT C#/WPF/auth/provider proof.')


if __name__ == '__main__': run()
