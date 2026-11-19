#!/usr/bin/env python3
"""Lexical P36 mutation probes. Run alone; always restore exact original bytes."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]


def run():
    paths={key:ROOT/value for key,value in {
        'db':'dotnet/Area11.Core/Data/Database.cs','outcome':'dotnet/Area11.Core/Data/TransactionOutcome.cs',
        'draft':'dotnet/Area11.Core/Services/DraftService.cs','hold':'dotnet/Area11.Core/Services/HoldCartService.cs',
        'pos':'dotnet/Area11.Desktop/ViewModels/PosViewModel.cs','vm':'dotnet/Area11.Desktop/ViewModels/DraftsViewModel.cs',
        'sale':'dotnet/Area11.Core/Services/SaleService.cs','purchase':'dotnet/Area11.Core/Services/PurchaseService.cs',
        'tests':'dotnet/Area11.Tests/DraftRecoveryFaultTests.cs'}.items()}
    cases=[('outcome','!WorkStarted || (!CommitAttempted && RollbackConfirmed)','!WorkStarted || RollbackConfirmed'),
           ('db','outcome.CommitAttempted = true;',''),('db','outcome.RollbackConfirmed = true;',''),
           ('draft','if (!created.NoCommitConfirmed)','if (false)'),
           ('draft','var restored = RestoreAfterFailure(draftId, type);','var restored = true;'),
           ('draft','if (!marked.Ok) throw new ValidationException','if (false) throw new ValidationException'),
           ('draft','sale.Warnings.Add(warning);',''),('draft','purchase.Warnings.Add(warning);',''),
           ('draft','created.Warnings.Concat(new[] { warning })','created.Warnings'),
           ('draft','return changed == 1 && row is','return row is'),
           ('hold','recovery_draft_id = @draft','recovery_draft_id = NULL'),
           ('hold','INSERT INTO document_drafts(','INSERT INTO fake_drafts('),
           ('hold','RecoveryDraftId = recoveryId,','RecoveryDraftId = 0,'),
           ('hold','if (consumed != 1)','if (false)'),
           ('hold','if (sourceDraftId > 0)','if (false)'),
           ('sale','}, outcome);','});'),('purchase','}, outcome);','});'),
           ('pos','_openDraftId = cart.RecoveryDraftId;','_openDraftId = 0;'),
           ('pos','if (_draftReconstructionFailed)','if (false)'),
           ('pos','_draftReconstructionFailed = skipped > 0;','_draftReconstructionFailed = false;'),
           ('pos','_services.Holds.Hold(BuildInput(), _user, _openDraftId)','_services.Holds.Hold(BuildInput(), _user)'),
           ('pos','Unsaved edits will be lost.',''),
           ('vm','posted.Warning','string.Empty'),
           ('tests','Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);')]
    originals={p:p.read_bytes() for p in paths.values()}
    try:
        checks.check_draft_recovery_fault_boundaries();assert not checks.FAILURES,checks.FAILURES
        for key,old,new in cases:
            p=paths[key];text=originals[p].decode();assert old in text,(key,old)
            p.write_text(text.replace(old,new));checks.FAILURES.clear();checks.REPORT.clear();checks.check_draft_recovery_fault_boundaries()
            assert checks.FAILURES,'Survived '+key+':'+old
            p.write_bytes(originals[p]);print('REJECT',key,old)
    finally:
        for p,data in originals.items():p.write_bytes(data)
    assert all(p.read_bytes()==data for p,data in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected; exact original bytes restored. Not C#/WPF/provider proof.')


if __name__=='__main__':run()
