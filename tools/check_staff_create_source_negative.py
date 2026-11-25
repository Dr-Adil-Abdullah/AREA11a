#!/usr/bin/env python3
"""Lexical staff-create mutations;exact bytes restored.NOT .NET/auth/WPF/provider proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(file, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / file, token))
    add('Area11.Core/Services/AuthService.cs',
        'var outcome = new TransactionOutcome();', 'OwnerAccess.Require(_db, connection, actor);',
        "SELECT value FROM settings WHERE key='security.pinLength';",
        'cleanPin.Length != pinLength || !cleanPin.All(char.IsDigit)',
        'SELECT COUNT(1) FROM users WHERE lower(name) = lower(@name);', 'if (duplicate > 0)',
        'pin = PasswordHasher.Hash(cleanPin)',
        'return CaptureStaffEdit(connection, newId);', '}, outcome);',
        'Value = saved.Id, StaffEdit = saved, EditVersion = saved.StaffForm!.EditVersion',
        'CreateStaffFailure(outcome, exception.Message)', 'NoCommitConfirmed = outcome.NoCommitConfirmed')
    add('Area11.Desktop/ViewModels/SettingsViewModel.cs',
        'private readonly PostingAttemptGuard _staffCreate = new();', 'if (!_staffCreate.TryBegin())',
        'var writeStarted = false;', '_services.Auth.CreateStaff(_user, name, role, pin)',
        '_staffCreate.ObserveFailure(result);', '_staffCreate.AcknowledgeCommit();',
        '_createdStaffId = result.Value;', '_newStaffName = _newStaffPin = string.Empty;',
        'saved?.StaffForm is null', 'saved.StaffForm.UserId != _createdStaffId',
        'Users.Any(person => person.Id == _createdStaffId)', 'Users.Add(saved);',
        'if (_newStaffName.Length != 0 || _newStaffPin.Length != 0 || !Users.Contains(saved) || saved.Id != _createdStaffId)',
        '_staffCreate.CompleteReset();', '_staffCreate.ObserveException();',
        'if (!writeStarted && _staffCreate.InFlight) _staffCreate.CancelBeforeWrite();')
    add('Area11.Desktop/Views/SettingsView.xaml', 'Text="PIN (configured length)"')
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_staff_create_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_STAFF_CREATE_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_staff_create_outcomes()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} mutations rejected;exact bytes restored.NOT C#/WPF/auth/provider proof.')


if __name__ == '__main__': run()
