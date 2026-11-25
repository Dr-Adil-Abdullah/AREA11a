#!/usr/bin/env python3
"""Custom definition Update lexical mutations;exact bytes restored.NOT C#/WPF/provider proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def contracts():
    return {
        'Area11.Core/Services/CustomFieldService.cs': [
            'return _db.Tx<ServiceResult>(connection =>', 'var outcome = new TransactionOutcome();',
            'Error = outcome.NoCommitConfirmed ? error', 'NoCommitConfirmed = outcome.NoCommitConfirmed',
            'EditConcurrency.Require(_db, connection, EditConcurrency.CustomField, fieldId, expected);',
            'var owner = OwnerAccess.Require(_db, connection, user);',
            'throw new ValidationException(entityProblem);',
            'var saved = CaptureDefinitionEdit(connection, field.Entity, fieldId);',
            'Ok = true, CustomDefinitionEdit = saved, EditVersion = saved.EditVersion',
            'Warnings = typeChanged && anyValues', '}, outcome);',
            'public ServiceResult UpdateChecked(', 'if (expected is null) return new ServiceResult',
            'return Update(user, fieldId, label, type, optionsText, required, helpText, expected);',
            'FROM custom_fields WHERE entity = @entity ORDER BY sort, id;',
            'EditConcurrency.AttachDefinitions(_db, connection, entity, fields);'],
        'Area11.Desktop/ViewModels/CustomFieldsViewModel.cs': [
            'private readonly PostingAttemptGuard _definitionUpdate = new();',
            'RefreshCommand = new RelayCommand(Refresh);',
            'if (_savingDefinitionUpdate || !_definitionUpdate.Ready) return;',
            'if (_savingDefinitionUpdate || !_definitionUpdate.Ready) return false;',
            'if (_editingId > 0) { SaveExisting(); return; }',
            'The form\'s fields or order changed. Refresh and review before saving this edit.',
            'if (!_definitionUpdate.TryBegin()) return;',
            '_services.CustomFields.UpdateChecked(_user, _editingId, LabelText,',
            'SelectedType.Key, OptionsText, Required, HelpText, selected.Field.EditVersion)',
            '_definitionUpdate.ObserveFailure(result);', '_definitionUpdate.AcknowledgeCommit();',
            '_labelText = _optionsText = _helpText = string.Empty;',
            'saved.FieldId != fieldId', 'saved.Entity != SelectedTab.Entity',
            'ReplaceRows(rows, 0);', 'NotifyConsumedEditor();',
            '!CustomFieldDefinitionState.Matches(row.Field, rows[index])',
            '!ReferenceEquals(row.Field.EditVersion, rows[index].EditVersion)',
            '_definitionUpdate.CompleteReset();', '_definitionUpdate.ObserveException();',
            'InvalidateUpdate(_definitionUpdate.WasAcknowledged', 'finally { _savingDefinitionUpdate = false; }',
            '_canManage = _lastLoadSucceeded = false;', '_statusText = message;'],
        'Area11.Desktop/Views/CustomFieldsView.xaml': ['Content="Refresh fields" Command="{Binding RefreshCommand}"'],
        'Area11.Core/Models/CustomDefinitionEditForm.cs': [
            'private readonly List<CustomField> _fields;',
            '_fields = fields.Select(Copy).ToList();',
            'public IReadOnlyList<CustomField> Fields => _fields.Select(Copy).ToList().AsReadOnly();',
            'CreatedAt = field.CreatedAt, EditVersion = field.EditVersion'],
        'Area11.Core/Utils/ServiceResult.cs': [
            '[System.Text.Json.Serialization.JsonIgnore]\n    public Area11.Core.Models.CustomDefinitionEditForm? CustomDefinitionEdit { get; init; }']
    }


def run():
    cases = [(ROOT / 'dotnet' / p, t) for p, tokens in contracts().items() for t in tokens]
    originals = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_custom_definition_update_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            raw = originals[path].decode(); assert token in raw, (path, token)
            path.write_text(raw.replace(token, 'REMOVED_CUSTOM_DEFINITION_UPDATE'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_custom_definition_update_outcomes()
            assert checks.FAILURES, 'Survived: ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected/restored.NOT C#/WPF/provider proof.')


if __name__ == '__main__': run()
