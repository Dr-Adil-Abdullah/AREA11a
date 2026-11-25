#!/usr/bin/env python3
"""Lexical staff-editor mutations;exact bytes restored.NOT .NET/auth/WPF/provider proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(file, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / file, token))
    add('Area11.Core/Services/AuthService.cs', 'return _db.Snapshot(connection =>',
        'person.StaffForm = new StaffEditForm(person, EditConcurrency.Capture(_db, connection, "staff", person.Id));',
        'SetSecretCore(actor, userId, newSecret, isPassword, expected, checkedEdit: true)',
        'SetActiveCore(actor, userId, active, expected, checkedEdit: true)',
        'OwnerAccess.Require(_db, connection, actor);', 'RequireStaffEdit(connection, userId, expected);',
        'if (expected is null) throw new ValidationException(', 'if (isPassword != target.IsOwner)',
        'if (target.IsOwner && !active)', 'EditConcurrency.Require(_db, connection, "staff", userId, expected);',
        'var saved = CaptureStaffEdit(connection, userId);', 'EditVersion = saved.StaffForm!.EditVersion, StaffEdit = saved',
        '}, outcome);', 'NoCommitConfirmed = outcome.NoCommitConfirmed')
    add('Area11.Core/Services/EditConcurrency.cs', '"staff" => null,', 'if (scope == "staff")',
        'SELECT id,name,role,active,password_hash,pin_hash FROM users WHERE id=@id;')
    add('Area11.Core/Models/StaffEditForm.cs', 'public bool Active { get; }', '[System.Text.Json.Serialization.JsonIgnore]')
    add('Area11.Core/Models/User.cs', 'public StaffEditForm? StaffForm { get; internal set; }')
    add('Area11.Core/Utils/ServiceResult.cs', 'public Area11.Core.Models.User? StaffEdit { get; init; }')
    add('Area11.Desktop/ViewModels/SettingsViewModel.cs', 'private bool _savingStaff;',
        'private void ToggleActive() => SaveStaffChange(secret: false);', 'private void ResetSecret() => SaveStaffChange(secret: true);',
        'var form = person.StaffForm;', 'person.Id != form.UserId || !Users.Contains(person)',
        '_services.Auth.SetSecret(_user, form.UserId, ResetStaffSecret, form.Role == UserRole.Owner, form.EditVersion)',
        '_services.Auth.SetActive(_user, form.UserId, !form.Active, form.EditVersion)',
        'if (!result.NoCommitConfirmed) ClearStaffData();', 'acknowledged = true;', '_resetStaffSecret = string.Empty;',
        'var saved = result.StaffEdit;', '_selectedUser = saved;', '_pricingVersion = null;',
        'finally { _savingStaff = false; }')
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_staff_edit_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_STAFF_EDIT_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_staff_edit_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} mutations rejected;exact bytes restored.NOT C#/WPF/auth/provider proof.')


if __name__ == '__main__': run()
