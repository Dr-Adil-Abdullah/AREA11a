#!/usr/bin/env python3
"""Restorable lexical mutations of the parent Save contracts;not .NET/WPF tests."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core/Services'
    ui = ROOT / 'dotnet/Area11.Desktop/ViewModels'
    cases = []
    for filename in ('ProductService.cs', 'PartnerService.cs'):
        cases.extend([
            (core / filename, '}, outcome);', '});'),
            (core / filename, 'NoCommitConfirmed = outcome.NoCommitConfirmed', 'NoCommitConfirmed = true'),
            (core / filename, 'ServiceResult<int> Refuse(string error) => new() { Error = error, NoCommitConfirmed = true };',
             'ServiceResult<int> Refuse(string error) => new() { Error = error };')])
    for filename, guard in [('Products', '_recordSave'), ('Products', '_quickCatalogSave'),
                            ('Customers', '_newRecordSave'), ('Customers', '_editRecordSave'),
                            ('Pos', '_newCustomerSave'), ('Purchases', '_newSupplierSave'),
                            ('CatalogWindow', '_recordSave'), ('UnitWindow', '_recordSave')]:
        for old, new in [(guard + '.TryBegin()', 'true'),
                         (guard + '.AcknowledgeCommit();', ''),
                         (guard + '.ObserveException();', ''),
                         (guard + '.CompleteReset();', '')]:
            cases.append((ui / (filename + 'ViewModel.cs'), old, new))
    cases.extend([
        (ui / 'ProductsViewModel.cs', '_editingId = created.Value;', ''),
        (ui / 'ProductsViewModel.cs', 'if (!_quickCatalogSave.Ready)', 'if (false)'),
        (ui / 'CatalogWindowViewModel.cs', 'SetStatus(linked.NoCommitConfirmed', 'SetStatus(true'),
        (ui / 'CatalogWindowViewModel.cs', 'if (!_recordSave.Ready)', 'if (false)'),
        (ui / 'UnitWindowViewModel.cs', 'if (!_recordSave.Ready)', 'if (false)'),
        (ui / 'PosViewModel.cs', '            _newCustomerSave.AcknowledgeCommit();', '            Cart.Clear();\n            _newCustomerSave.AcknowledgeCommit();'),
        (ui / 'PurchasesViewModel.cs', '            _newSupplierSave.AcknowledgeCommit();', '            Lines.Clear();\n            _newSupplierSave.AcknowledgeCommit();')])
    test = ROOT / 'dotnet/Area11.Tests/RecordSaveOutcomeTests.cs'
    synchronous_fault = 'Assert.Throws<InvalidOperationException>((Action)(() => throw new InvalidOperationException("synthetic UI refresh")));'
    cases.extend([
        (test, synchronous_fault, 'Assert.Throws<InvalidOperationException>(() => throw new InvalidOperationException("synthetic UI refresh"));'),
        (test, synchronous_fault, ''),
        (test, synchronous_fault, 'Assert.Throws<InvalidOperationException>(async () => { throw new InvalidOperationException("synthetic UI refresh"); });')])
    originals = {p: p.read_bytes() for p, _, _ in cases}
    try:
        checks.check_parent_record_save_outcomes(); assert not checks.FAILURES, checks.FAILURES
        for p, old, new in cases:
            text = originals[p].decode(); assert old in text, (p.name, old)
            p.write_text(text.replace(old, new)); checks.FAILURES.clear(); checks.REPORT.clear()
            checks.check_parent_record_save_outcomes()
            assert checks.FAILURES, 'Survived ' + p.name + ': ' + old
            p.write_bytes(originals[p]); print('REJECT', p.name, old)
    finally:
        for p, raw in originals.items(): p.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact original bytes restored.NOT C#/WPF/runtime proof.')


if __name__ == '__main__': run()
