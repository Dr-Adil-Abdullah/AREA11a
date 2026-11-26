#!/usr/bin/env python3
"""Add lexical regressions,not C#/provider/WPF execution;exact bytes restored."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def contracts():
    return {'Area11.Core/Services/CustomFieldService.cs': ['var outcome = new TransactionOutcome();', 'Error = outcome.NoCommitConfirmed ? error', 'NoCommitConfirmed = outcome.NoCommitConfirmed', 'FieldGovernance.DefinitionError(', 'return _db.Tx<ServiceResult<CustomField>>(connection =>', 'var owner = OwnerAccess.Require(_db, connection, user);', 'throw new ValidationException(', 'LabelExists(connection, key, cleanLabel, exceptId: 0)', 'SELECT IFNULL(MAX(sort), 0) + 1', 'INSERT INTO custom_fields(', 'at = Clock.Now()', 'FieldGovernance.KeyFor(key, id)', 'UPDATE custom_fields SET field_key = @fieldKey WHERE id = @id;', '_audit.Record(connection, owner, Modules.Settings, "custom_field_add"', 'var saved = CaptureDefinitionEdit(connection, key, id);', 'Ok = true, Value = saved.Fields.Single(field => field.Id == id)', 'CustomDefinitionEdit = saved, EditVersion = saved.EditVersion', '}, outcome);'], 'Area11.Desktop/ViewModels/CustomFieldsViewModel.cs': ['if (_savingDefinitionUpdate || !_definitionUpdate.Ready) return;', 'if (!_lastLoadSucceeded || !CanManage || _editingId != 0 || _selectedField is not null || IsEditing)', 'var entity = SelectedTab.Entity;', 'if (!_definitionUpdate.TryBegin()) return;', '_savingDefinitionUpdate = true;', '_services.CustomFields.Add(_user, entity, LabelText,', '_definitionUpdate.ObserveFailure(result);', '_definitionUpdate.AcknowledgeCommit();', '_labelText = _optionsText = _helpText = string.Empty;', 'saved.FieldId != created.Id', 'saved.Entity != entity', 'SelectedTab.Entity != entity', '!CustomFieldDefinitionState.Matches(created, target)', '!ReferenceEquals(created.EditVersion, target.EditVersion)', 'ReplaceRows(rows, 0);', 'NotifyConsumedEditor();', '!CustomFieldDefinitionState.Matches(row.Field, rows[index])', '!ReferenceEquals(row.Field.EditVersion, rows[index].EditVersion)', '_definitionUpdate.CompleteReset();', '_definitionUpdate.ObserveException();', 'InvalidateUpdate(_definitionUpdate.WasAcknowledged', 'finally { _savingDefinitionUpdate = false; }']}


def run():
    cases = [(ROOT / 'dotnet' / file, token) for file, tokens in contracts().items() for token in tokens]
    original = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_custom_definition_add_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            raw = original[path].decode(); assert token in raw, (path,token)
            path.write_text(raw.replace(token, 'REMOVED_CUSTOM_ADD'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_custom_definition_add_outcomes()
            assert checks.FAILURES, 'Survived: ' + token
            path.write_bytes(original[path]); print('REJECT', path.name, token)
    finally:
        for p, raw in original.items(): p.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in original.items())
    print(f'PASS {len(cases)} Add mutations rejected;exact bytes restored.NOT C#/WPF/provider proof.')


if __name__ == '__main__': run()
