#!/usr/bin/env python3
"""P39 lexical mutations only. Run alone; exact source bytes restored in finally."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]


def run():
    files={k:ROOT/p for k,p in {'core':'dotnet/Area11.Core/Services/ProvisionalReturnService.cs',
                               'vm':'dotnet/Area11.Desktop/ViewModels/ProvisionalViewModel.cs',
                               'tests':'dotnet/Area11.Tests/ProvisionalPostingFaultTests.cs'}.items()}
    cases=[('core','return Save(connection, input, user);','return ServiceResult<ProvisionalResult>.Fail("synthetic");'),
           ('core','}, outcome);','});'),('core','NoCommitConfirmed = outcome.NoCommitConfirmed','NoCommitConfirmed = true'),
           ('core','outcome.NoCommitConfirmed ? exception.Message','true ? exception.Message'),
           ('core','NoCommitConfirmed = true','NoCommitConfirmed = false'),
           ('core','The PR action was not saved; no commit was made.','The PR action failed.'),
           ('core','The PR outcome could not be confirmed. Verify','The PR outcome failed. Verify')]
    cases.extend(('vm',token,'') for token in ('_posting.ObserveFailure(result);','_posting.AcknowledgeCommit();','_posting.CompleteReset();',
                  'BillCode = string.Empty;','CancelReason = string.Empty;','Lookup.Clear();','ForgetPostingTarget();',
                  'var entryId = SelectedEntry.Id;','var caption = SelectedEntry.Code;', 'if (RefusePendingPosting()) return;',
                  'if (!_posting.TryBegin()) return;', '_postingInProgress = true;', '_posting.ObserveException();',
                  'if (_posting.InFlight) _posting.CancelBeforeWrite();','_selectedEntry = null;','_selectedLookup = null;'))
    cases.extend([('vm','private void Save() => RunPosting(SaveOnce);','private void Save() => SaveOnce();'),
                  ('vm','private void LinkBill() => RunPosting(LinkBillOnce);','private void LinkBill() => LinkBillOnce();'),
                  ('vm','private void CancelEntry() => RunPosting(CancelEntryOnce);','private void CancelEntry() => CancelEntryOnce();'),
                  ('tests','Assert.True(failed.NoCommitConfirmed);','Assert.False(failed.Ok);'),
                  ('tests','Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);'),
                  ('tests','[InlineData("create", "header", false)]','')])
    original={p:p.read_bytes() for p in files.values()}
    try:
        checks.check_provisional_posting_fault_boundaries();assert not checks.FAILURES,checks.FAILURES
        for key,old,new in cases:
            p=files[key];text=original[p].decode();assert old in text,(key,old)
            p.write_text(text.replace(old,new));checks.FAILURES.clear();checks.REPORT.clear();checks.check_provisional_posting_fault_boundaries()
            assert checks.FAILURES,'Survived '+key+':'+old
            p.write_bytes(original[p]);print('REJECT',key,old)
    finally:
        for p,raw in original.items():p.write_bytes(raw)
    assert all(p.read_bytes()==raw for p,raw in original.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact bytes restored.NOT C#/WPF/provider proof.')


if __name__=='__main__':run()
