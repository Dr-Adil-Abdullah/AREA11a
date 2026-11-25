#!/usr/bin/env python3
"""Lexical permission-editor mutations;exact bytes restored.NOT .NET/auth/WPF/provider proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(file, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / file, token))
    add('Area11.Core/Services/PermissionService.cs', 'return _db.Snapshot(connection =>',
        'OwnerAccess.Require(_db, connection, actor);', 'Success(CaptureEdit(connection, target))',
        'SetCore(actor, userId, module, allowed, expected, checkedEdit: true)', 'ResetCore(actor, userId, expected, checkedEdit: true)',
        'if (checkedEdit) RequireEditor(connection, userId, expected);',
        'EditConcurrency.Require(_db, connection, "permissions", userId, expected);', 'if (expected is null) throw new ValidationException(',
        'var form = CaptureEdit(connection, target);', 'PermissionEdit = form', '}, outcome);', 'NoCommitConfirmed = outcome.NoCommitConfirmed',
        'RoleDefault(target.Kind, module)')
    add('Area11.Core/Services/EditConcurrency.cs', '"permissions" => null,', 'if (scope == "permissions")',
        'SELECT module_key,allowed FROM user_permissions WHERE user_id=@id ORDER BY module_key;')
    add('Area11.Core/Models/PermissionEditForm.cs', 'new ReadOnlyDictionary<string, bool>', '[System.Text.Json.Serialization.JsonIgnore]')
    add('Area11.Core/Utils/ServiceResult.cs', 'public Area11.Core.Models.PermissionEditForm? PermissionEdit { get; init; }')
    add('Area11.Desktop/ViewModels/SettingsViewModel.cs', 'private PermissionEditForm? _permissionEdit;', 'private bool _savingPermission;',
        '_services.Permissions.LoadEditForm(_user, _selectedUser.Id)', '_services.Permissions.ResetToRoleDefaults(_user, target.Id, form.EditVersion)',
        '_services.Permissions.Set(_user, target.Id, toggle.Module, toggle.Allowed, form.EditVersion)',
        'acknowledged = true;', '_permissionEdit = result.PermissionEdit;',
        'if (!result.NoCommitConfirmed) { _permissionEdit = null; ClearPermissionRows(); }',
        'finally { _savingPermission = false; }', 'try { toggle.Allowed = _permissionEdit.Values[toggle.Module]; }')
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_permission_edit_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_PERMISSION_EDIT_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_permission_edit_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} mutations rejected;exact bytes restored.NOT C#/WPF/auth/provider proof.')


if __name__ == '__main__': run()
