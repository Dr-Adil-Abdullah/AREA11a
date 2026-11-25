#!/usr/bin/env python3
"""Native field lexical mutations;restores exact bytes.NOT C#/WPF/provider proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def contracts():
    return {
        'Area11.Core/Services/NativeFieldPolicyService.cs': [
            'public ServiceResult<FormFieldDefinition> PrepareRequiredEdit(',
            'if (expected is null) throw new ValidationException(',
            'EditConcurrency.Require(_db, connection, EditConcurrency.NativeField, fieldId, expected);',
            'ServiceResult<FormFieldDefinition>.Success(CaptureEdit(connection, fieldId))',
            'public ServiceResult SetRequiredChecked(', 'if (expected is null) return new ServiceResult',
            'return SetRequired(user, fieldId, required, confirmedRisk, expected);',
            'var outcome = new TransactionOutcome();', 'var owner = OwnerAccess.Require(_db, connection, user);',
            'if (field.Required == required)', 'return SavedEdit(connection, fieldId);',
            'if (overriding && !confirmedRisk)', 'by = overriding ? owner.Name.Trim() : null',
            '_audit.Record(connection, owner, Modules.Settings, "native_field_required_changed",',
            '}, outcome);', 'Error = !outcome.NoCommitConfirmed', 'NoCommitConfirmed = outcome.NoCommitConfirmed',
            'field.EditVersion = EditConcurrency.Capture(_db, connection, EditConcurrency.NativeField, fieldId);',
            'Ok = true, NativeFieldEdit = field, EditVersion = field.EditVersion'],
        'Area11.Desktop/ViewModels/NativeFieldsViewModel.cs': [
            'private bool _saving;', 'private bool _needsReload;\n    private string? _reviewStatus;',
            'if (_saving || (value is not null && !Fields.Contains(value))) return;',
            'if (_needsReload) return false;', 'if (_saving) return;',
            '_selected = null;', '_lastLoadSucceeded = false;', 'Fields.Clear();',
            '_services.NativeFields.PrepareRequiredEdit(_user, selected.Id, selected.EditVersion)',
            'if (!prepared.Ok || prepared.Value is not { } field)',
            'if (!required && field.Required && field.Critical && field.SafeRequired)',
            'MessageBoxResult.No', 'if (answer != MessageBoxResult.Yes) return;',
            '_services.NativeFields.SetRequiredChecked(_user, field.Id, required, confirmed, field.EditVersion)',
            'if (!result.NoCommitConfirmed) RequireReview(result.Error ??',
            'acknowledged = true;', 'saved?.EditVersion is null', 'saved.Id != field.Id',
            '_selected = saved;', 'Fields[index] = saved;',
            'if (!ReferenceEquals(_selected, saved) || !Fields.Contains(saved) || saved.Required != required)',
            'RequireReview(acknowledged ?', 'finally { _saving = false; }'],
        'Area11.Core/Utils/ServiceResult.cs': [
            '[System.Text.Json.Serialization.JsonIgnore]\n    public Area11.Core.Models.FormFieldDefinition? NativeFieldEdit { get; init; }'],
        'Area11.Desktop/Views/NativeFieldsView.xaml': ['Content="Refresh Fields" Command="{Binding RefreshCommand}"'],
        'Area11.Desktop/Views/NativeFieldsView.xaml.cs': [
            'Loaded += (_, _) => { (DataContext as NativeFieldsViewModel)?.RefreshAccess(); accessTimer.Start(); };']
    }


def run():
    cases = [(ROOT / 'dotnet' / name, token) for name, tokens in contracts().items() for token in tokens]
    original = {p: p.read_bytes() for p, _ in cases}
    try:
        checks.check_native_field_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = original[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_NATIVE_FIELD_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_native_field_outcomes()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(original[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in original.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in original.items())
    print(f'PASS {len(cases)} mutations rejected;exact bytes restored.NOT C#/WPF/provider proof.')


if __name__ == '__main__': run()
