#!/usr/bin/env python3
"""P42 lexical negatives only; original bytes restored. Not behavioral .NET mutation tests."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    files = {k: ROOT / p for k, p in {
        'collect': 'dotnet/Area11.Core/Services/CustomerLedgerService.cs',
        'pay': 'dotnet/Area11.Core/Services/SupplierLedgerService.cs',
        'customer': 'dotnet/Area11.Desktop/ViewModels/CustomersViewModel.cs',
        'supplier': 'dotnet/Area11.Desktop/ViewModels/SuppliersViewModel.cs',
        'recovery': 'dotnet/Area11.Desktop/ViewModels/RecoveryViewModel.cs',
        'tests': 'dotnet/Area11.Tests/LedgerOutcomeFaultTests.cs'}.items()}
    cases = []
    for key in ('collect', 'pay'):
        cases += [(key, '}, outcome);', '});'),
                  (key, 'NoCommitConfirmed = true', 'NoCommitConfirmed = false'),
                  (key, 'NoCommitConfirmed = outcome.NoCommitConfirmed', 'NoCommitConfirmed = true'),
                  (key, 'Error = outcome.NoCommitConfirmed ? exception.Message', 'Error = exception.Message ??'),
                  (key, 'Verify payment history, account and drawer before retrying.', 'Nothing was changed.')]
    for key, guard, result in (('customer', '_collectionPosting', 'result'), ('supplier', '_paymentPosting', 'result'), ('recovery', '_receivePosting', 'collected')):
        for old, new in [('if (!' + guard + '.Ready)', 'if (false)'),
                         ('if (!' + guard + '.TryBegin()) return;', ''),
                         (guard + '.ObserveFailure(' + result + ');', ''),
                         (guard + '.AcknowledgeCommit();', ''),
                         (guard + '.ObserveException();', ''),
                         (guard + '.CompleteReset();', '')]:
            cases.append((key, old, new))
    cases += [('tests', 'Assert.True(result.NoCommitConfirmed);', ''),
              ('tests', 'Assert.Equal(before, f.Snapshot());', ''),
              ('tests', '[InlineData("recovery", "ack-fault")]', '')]
    originals = {p: p.read_bytes() for p in files.values()}
    try:
        checks.check_ledger_outcome_fault_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for key, old, new in cases:
            p = files[key]; text = originals[p].decode(); assert old in text, (key, old)
            p.write_text(text.replace(old, new)); checks.FAILURES.clear(); checks.REPORT.clear()
            checks.check_ledger_outcome_fault_boundaries(); assert checks.FAILURES, 'Survived ' + key + ':' + old
            p.write_bytes(originals[p]); print('REJECT', key, old)
    finally:
        for p, raw in originals.items(): p.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected; exact bytes restored. NOT C#/WPF/provider execution.')


if __name__ == '__main__': run()
