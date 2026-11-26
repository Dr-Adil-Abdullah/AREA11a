#!/usr/bin/env python3
"""Move lexical regressions,not C#/provider/WPF execution;exact bytes restored."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def contracts():
    return {'Area11.Core/Services/CustomFieldService.cs': ['var outcome = new TransactionOutcome();',
                                                'Error = outcome.NoCommitConfirmed ? error',
                                                'NoCommitConfirmed = outcome.NoCommitConfirmed',
                                                'direction is not (-1 or 1)',
                                                'return _db.Tx<ServiceResult>(connection =>',
                                                'var owner = OwnerAccess.Require(_db, connection, user);',
                                                'EditConcurrency.Require(_db, connection, '
                                                'EditConcurrency.CustomField, fieldId, expected);',
                                                'throw new ValidationException("That field is not in the list '
                                                'any more.");',
                                                'ORDER BY sort, id;',
                                                'var target = index + direction;',
                                                'throw new ValidationException(direction < 0',
                                                '(ordered[index], ordered[target]) = (ordered[target], '
                                                'ordered[index]);',
                                                'for (var position = 0; position < ordered.Count; position++)',
                                                'UPDATE custom_fields SET sort = @sort WHERE id = @id;',
                                                'sort = position + 1, id = ordered[position].Id',
                                                '_audit.Record(connection, owner, Modules.Settings, '
                                                '"custom_field_order"',
                                                'var saved = CaptureDefinitionEdit(connection, field.Entity, '
                                                'fieldId);',
                                                'return new ServiceResult { Ok = true, CustomDefinitionEdit = '
                                                'saved, EditVersion = saved.EditVersion };',
                                                '}, outcome);',
                                                'public ServiceResult MoveChecked(',
                                                'if (expected is null) return new ServiceResult',
                                                'return Move(user, fieldId, direction, expected);'],
 'Area11.Desktop/ViewModels/CustomFieldsViewModel.cs': ['if (_savingDefinitionUpdate || '
                                                        '!_definitionUpdate.Ready) return;',
                                                        'selected.Field.Id != _editingId',
                                                        'selected.Field.Entity != SelectedTab.Entity',
                                                        'var fieldId = selected.Field.Id;',
                                                        'var entity = selected.Field.Entity;',
                                                        'var expectedSort = Fields.IndexOf(selected) + '
                                                        'direction + 1;',
                                                        'if (!_definitionUpdate.TryBegin()) return;',
                                                        '_savingDefinitionUpdate = true;',
                                                        '_services.CustomFields.MoveChecked(_user, fieldId, '
                                                        'direction, selected.Field.EditVersion)',
                                                        '_definitionUpdate.ObserveFailure(result);',
                                                        '_definitionUpdate.AcknowledgeCommit();',
                                                        '_labelText = _optionsText = _helpText = string.Empty;',
                                                        'saved.FieldId != fieldId',
                                                        'saved.Entity != entity',
                                                        'SelectedTab.Entity != entity',
                                                        'target.Sort != expectedSort',
                                                        'field.Entity != entity || field.EditVersion is null',
                                                        'ReplaceRows(rows, 0);',
                                                        'NotifyConsumedEditor();',
                                                        '!CustomFieldDefinitionState.Matches(row.Field, '
                                                        'rows[index])',
                                                        '!ReferenceEquals(row.Field.EditVersion, '
                                                        'rows[index].EditVersion)',
                                                        '_definitionUpdate.CompleteReset();',
                                                        '_definitionUpdate.ObserveException();',
                                                        'InvalidateUpdate(_definitionUpdate.WasAcknowledged',
                                                        'finally { _savingDefinitionUpdate = false; }']}


def run():
    cases = [(ROOT / 'dotnet' / file, token) for file, tokens in contracts().items() for token in tokens]
    original = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_custom_definition_move_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            raw = original[path].decode(); assert token in raw, (path,token)
            path.write_text(raw.replace(token, 'REMOVED_CUSTOM_MOVE'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_custom_definition_move_outcomes()
            assert checks.FAILURES, 'Survived: ' + token
            path.write_bytes(original[path]); print('REJECT', path.name, token)
    finally:
        for p, raw in original.items(): p.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in original.items())
    print(f'PASS {len(cases)} Move mutations rejected;exact bytes restored.NOT C#/WPF/provider proof.')


if __name__ == '__main__': run()
