#!/usr/bin/env python3
"""P37 lexical regressions only. Run alone; exact byte restoration is mandatory."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]


def run():
    files={k:ROOT/v for k,v in {
        'draft':'dotnet/Area11.Core/Services/DraftService.cs','hold':'dotnet/Area11.Core/Services/HoldCartService.cs',
        'pos':'dotnet/Area11.Desktop/ViewModels/PosViewModel.cs','purchase':'dotnet/Area11.Desktop/ViewModels/PurchasesViewModel.cs',
        'tests':'dotnet/Area11.Tests/AutomaticDraftPostingTests.cs'}.items()}
    cases=[('draft','IsUniqueViolation(exception) && outcome.NoCommitConfirmed && attempt < 4','IsUniqueViolation(exception) && attempt < 4'),
           ('draft','}, outcome);','});'),('draft','new() { Error = error, NoCommitConfirmed = true };','new() { Error = error };'),
           ('hold','if (sourceDraftId > 0)','if (false)'),('hold','An open draft cannot be parked as another copy.',''),
           ('hold','}, outcome);','});'),('hold','NoCommitConfirmed = outcome.NoCommitConfirmed','NoCommitConfirmed = true'),
           ('pos','if (_openDraftId <= 0 && !StoreSaleDraft(input)) return;',''),
           ('pos','var result = _services.Drafts.PostSale(_user, _openDraftId, input);','var result = _services.Sales.Create(input, _user);'),
           ('purchase','if (_openDraftId <= 0 && !StorePurchaseDraft(input)) return;',''),
           ('purchase','var result = _services.Drafts.PostPurchase(_user, _openDraftId, input);','var result = _services.Purchases.Create(input, _user);'),
           ('pos','_draftSaveNeedsVerification = true;',''),('purchase','_draftSaveNeedsVerification = true;',''),
           ('pos','_draftSaveNeedsVerification = !saved.NoCommitConfirmed;','_draftSaveNeedsVerification = false;'),
           ('purchase','_draftSaveNeedsVerification = !saved.NoCommitConfirmed;','_draftSaveNeedsVerification = false;'),
           ('pos','_openDraftId = saved.Value.Id;','_openDraftId = 0;'),('purchase','_openDraftId = saved.Value.Id;','_openDraftId = 0;'),
           ('pos','if (_draftSaveNeedsVerification)','if (false)'),('pos','if (_parkedCartNeedsReset)','if (false)'),
           ('pos','_parkedCartNeedsReset = !result.NoCommitConfirmed;','_parkedCartNeedsReset = false;'),
           ('pos','_parkedCartNeedsReset = true;',''),('pos','_holdOutcomeUnknown = _parkedCartNeedsReset;',''),
           ('purchase','if (RefuseUnsafeEntry()) return;',''),
           ('purchase','if (_saveInProgress || RefuseUnsafeEntry()) return;',''),
           ('purchase','_committedEntryNeedsReset = true;',''),('purchase','_openDraftId = draftId;','_openDraftId = 0;'),
           ('purchase','_draftReconstructionFailed = skipped > 0;','_draftReconstructionFailed = false;'),
           ('tests','Assert.True(failed.NoCommitConfirmed);','Assert.False(failed.Ok);'),
           ('tests','Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);'),
           ('tests','[Fact]\n    public void SecondHeldLineFaultCannotKeepTheFirstLineOrConsumeNumber','public void SecondHeldLineFaultCannotKeepTheFirstLineOrConsumeNumber')]
    original={p:p.read_bytes() for p in files.values()}
    try:
        checks.check_automatic_shared_draft_posting();assert not checks.FAILURES,checks.FAILURES
        for key,old,new in cases:
            p=files[key];text=original[p].decode();assert old in text,(key,old)
            p.write_text(text.replace(old,new));checks.FAILURES.clear();checks.REPORT.clear();checks.check_automatic_shared_draft_posting()
            assert checks.FAILURES,'Survived '+key+':'+old
            p.write_bytes(original[p]);print('REJECT',key,old[:100])
    finally:
        for p,raw in original.items():p.write_bytes(raw)
    assert all(p.read_bytes()==raw for p,raw in original.items())
    print(f'PASS {len(cases)} lexical mutations rejected; exact bytes restored. Not C#/WPF/provider proof.')


if __name__=='__main__':run()
