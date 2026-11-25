#!/usr/bin/env python3
"""Visibility lexical regressions,not C#/provider/WPF execution;exact bytes restored."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def contracts():
    return {
        'Area11.Core/Services/CustomFieldService.cs': [
            'var outcome = new TransactionOutcome();', 'Error = outcome.NoCommitConfirmed ? error',
            'NoCommitConfirmed = outcome.NoCommitConfirmed', 'return _db.Tx<ServiceResult>(connection =>',
            'var owner = OwnerAccess.Require(_db, connection, user);',
            'EditConcurrency.Require(_db, connection, EditConcurrency.CustomField, fieldId, expected);',
            'if (field.Active == active)', 'return SavedActiveEdit(connection, field.Entity, fieldId);',
            'UPDATE custom_fields SET active = @active WHERE id = @id;',
            'active ? "custom_field_on" : "custom_field_off"', '}, outcome);',
            'public ServiceResult SetActiveChecked(', 'if (expected is null) return new ServiceResult',
            'return SetActive(user, fieldId, active, expected);',
            'var saved = CaptureDefinitionEdit(connection, entity, fieldId);',
            'return new ServiceResult { Ok = true, CustomDefinitionEdit = saved, EditVersion = saved.EditVersion };'],
        'Area11.Desktop/ViewModels/CustomFieldsViewModel.cs': [
            'if (_savingDefinitionUpdate || !_definitionUpdate.Ready) return;',
            'selected.Field.Id != _editingId', 'selected.Field.Entity != SelectedTab.Entity',
            'var active = !selected.Field.Active;', 'if (!_definitionUpdate.TryBegin()) return;',
            '_services.CustomFields.SetActiveChecked(_user, fieldId, active, selected.Field.EditVersion)',
            '_definitionUpdate.ObserveFailure(result);', '_definitionUpdate.AcknowledgeCommit();',
            '_labelText = _optionsText = _helpText = string.Empty;',
            'saved.FieldId != fieldId', 'saved.Entity != entity', 'SelectedTab.Entity != entity',
            'target.Active != active', 'ReplaceRows(rows, 0);', 'NotifyConsumedEditor();',
            '!CustomFieldDefinitionState.Matches(row.Field, rows[index])',
            '!ReferenceEquals(row.Field.EditVersion, rows[index].EditVersion)',
            '_definitionUpdate.CompleteReset();', '_definitionUpdate.ObserveException();',
            'InvalidateUpdate(_definitionUpdate.WasAcknowledged', 'finally { _savingDefinitionUpdate = false; }']
    }


def run():
    cases = [(ROOT / 'dotnet' / file, token) for file, tokens in contracts().items() for token in tokens]
    original = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_custom_definition_active_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            raw = original[path].decode(); assert token in raw, (path,token)
            path.write_text(raw.replace(token, 'REMOVED_CUSTOM_VISIBILITY'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_custom_definition_active_outcomes()
            assert checks.FAILURES, 'Survived: ' + token
            path.write_bytes(original[path]); print('REJECT', path.name, token)
    finally:
        for p, raw in original.items(): p.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in original.items())
    print(f'PASS {len(cases)} visibility mutations rejected;exact bytes restored.NOT C#/WPF/provider proof.')


if __name__ == '__main__': run()
