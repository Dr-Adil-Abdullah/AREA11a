#!/usr/bin/env python3
"""Lexical regressions only; restores exact source bytes. NOT C#/WPF/provider proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    vm = ROOT / 'dotnet/Area11.Desktop/ViewModels/PosViewModel.cs'
    xaml = ROOT / 'dotnet/Area11.Desktop/Views/PosView.xaml'
    original = {p: p.read_bytes() for p in (vm, xaml)}
    tokens = [
        'set => SaveAutoPrintChoice(value);',
        'public bool CanEditAutoPrint => _autoPrintEdit is not null && !_autoPrintNeedsReview;',
        'public bool CanReadAutoPrint => _autoPrintAttempt.Ready || (_autoPrintEdit is not null && !_autoPrintNeedsReview);',
        'if (_savingAutoPrint || !_autoPrintAttempt.Ready) return;',
        '_services.Settings.LoadEditForm(_user, new[] { "printer.autoPrint" })',
        '_autoPrintEdit = loaded.Ok ? loaded.Value : null;',
        '_autoPrint = _autoPrintEdit?.GetBool("printer.autoPrint", true) ?? false;',
        'if (!CanEditAutoPrint || _autoPrintEdit is not { } form)',
        'if (wanted == _autoPrint) return;', 'if (!_autoPrintAttempt.TryBegin()) return;',
        'var saved = access.Ok ? _services.Settings.SaveEditForm(',
        'new Dictionary<string, object?> { ["printer.autoPrint"] = wanted }, _user, form)',
        'new ServiceResult { Error = access.Error, NoCommitConfirmed = true }',
        '_autoPrintAttempt.ObserveFailure(saved);', 'if (!_autoPrintAttempt.Ready)',
        '_autoPrintAttempt.AcknowledgeCommit();', 'var receipt = saved.SettingsEdit;',
        'receipt is null || receipt.GetRaw("printer.autoPrint") != (wanted ? "true" : "false")',
        '_autoPrintEdit = receipt;', '_autoPrint = wanted;', '_autoPrintNeedsReview = false;',
        '!ReferenceEquals(_autoPrintEdit, receipt) || _autoPrint != wanted || _autoPrintNeedsReview || _autoPrintHint.Length != 0',
        '_autoPrintAttempt.CompleteReset();', '_autoPrintAttempt.ObserveException();',
        'InvalidateAutoPrintChoice(_autoPrintAttempt.WasAcknowledged',
        'private ServiceResult CheckAutoPrintAccess() => _services.CheckInteractiveSession()',
        '? _services.Sales.CheckPostingAccess(_user)',
        'OnPropertyChanged(nameof(AutoPrint));', 'RefreshAutoPrintCommand.RaiseCanExecuteChanged();',
        '_autoPrint = false;', '_autoPrintEdit = null;', '_autoPrintHint = message;'
    ]
    cases = [(vm, token, 'REMOVED_PRINT_CONTRACT') for token in tokens]
    cases += [(xaml, token, 'REMOVED_PRINT_BINDING') for token in (
        'IsEnabled="{Binding CanEditAutoPrint}"', 'Command="{Binding RefreshAutoPrintCommand}"',
        'Text="{Binding AutoPrintHint}"', 'ToolTipService.ShowOnDisabled="True"')]
    for forbidden in ('LoadAutoPrintChoice();', 'LoadLookups();', 'RefreshCounter();', 'Cart.Clear();',
                      '_services.Settings.Set("printer.autoPrint", wanted, _user);'):
        cases.append((vm, '            var access = CheckAutoPrintAccess();\n            var saved',
                      '            ' + forbidden + '\n            var access = CheckAutoPrintAccess();\n            var saved'))
    count = 0
    try:
        checks.check_counter_autoprint_edit(); assert not checks.FAILURES, checks.FAILURES
        for path, token, replacement in cases:
            source = original[path].decode()
            assert token in source, token
            path.write_text(source.replace(token, replacement, 1))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_counter_autoprint_edit()
            assert checks.FAILURES, 'Survived print mutation: ' + token
            path.write_bytes(original[path]); count += 1
            print('REJECT', path.name, token)
    finally:
        for path, raw in original.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in original.items())
    print(f'PASS {count} print-choice mutations rejected; exact bytes restored. NOT C#/WPF/provider proof.')


if __name__ == '__main__': run()
