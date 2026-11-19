#!/usr/bin/env python3
"""P38 lexical negative controls only. Run alone; restores exact source bytes."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]


def run():
    files={key:ROOT/path for key,path in {
        'expense':'dotnet/Area11.Core/Services/ExpenseService.cs','stock':'dotnet/Area11.Core/Services/StockService.cs',
        'take':'dotnet/Area11.Core/Services/StockTakeService.cs','guard':'dotnet/Area11.Core/Utils/PostingAttemptGuard.cs',
        'evm':'dotnet/Area11.Desktop/ViewModels/ExpensesViewModel.cs','wvm':'dotnet/Area11.Desktop/ViewModels/WriteOffViewModel.cs',
        'tvm':'dotnet/Area11.Desktop/ViewModels/StockTakeViewModel.cs','tests':'dotnet/Area11.Tests/ExpenseStockPostingFaultTests.cs'}.items()}
    cases=[]
    for key in ('expense','stock','take'):
        cases.extend([(key,'}, outcome);','});'),(key,'NoCommitConfirmed = outcome.NoCommitConfirmed','NoCommitConfirmed = true')])
    cases.extend([('guard','if (!Ready) return false;',''),('guard','WasAcknowledged = false;',''),
                  ('guard','failure.NoCommitConfirmed ? PostingAttemptState.Ready : PostingAttemptState.NeedsVerification','PostingAttemptState.Ready'),
                  ('guard','WasAcknowledged = true;',''),('guard','Require(PostingAttemptState.CommittedNeedsReset);',''),
                  ('guard','if (InFlight) State = PostingAttemptState.NeedsVerification;','')])
    for key in ('evm','wvm','tvm'):
        for old in ('if (_postingInProgress) return;','_posting.ObserveFailure(result);','_posting.AcknowledgeCommit();','_posting.CompleteReset();','_posting.ObserveException();'):
            cases.append((key,old,''))
    cases.extend([('tvm','_session = null;',''),('tvm','if (_postingInProgress || !_posting.Ready)','if (false)'),
                  ('evm','if (!DocEditor.LoadedSuccessfully) throw','if (false) throw'),
                  ('tests','Assert.True(failure.NoCommitConfirmed);','Assert.False(failure.Ok);'),
                  ('tests','Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);'),
                  ('tests','[InlineData("expense", "row", false)]','')])
    original={p:p.read_bytes() for p in files.values()}
    try:
        checks.check_expense_stock_fault_boundaries();assert not checks.FAILURES,checks.FAILURES
        for key,old,new in cases:
            p=files[key];text=original[p].decode();assert old in text,(key,old)
            p.write_text(text.replace(old,new));checks.FAILURES.clear();checks.REPORT.clear();checks.check_expense_stock_fault_boundaries()
            assert checks.FAILURES,'Survived '+key+':'+old
            p.write_bytes(original[p]);print('REJECT',key,old)
    finally:
        for p,raw in original.items():p.write_bytes(raw)
    assert all(p.read_bytes()==raw for p,raw in original.items())
    print(f'PASS {len(cases)} lexical mutations rejected; exact bytes restored. Not service/WPF/provider proof.')


if __name__=='__main__':run()
