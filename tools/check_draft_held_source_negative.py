#!/usr/bin/env python3
"""Lexical draft/held guard negatives only. Run alone; exact byte restoration required."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]


def run():
    files={k:ROOT/p for k,p in {
        'draft':'dotnet/Area11.Core/Services/DraftService.cs','hold':'dotnet/Area11.Core/Services/HoldCartService.cs',
        'vm':'dotnet/Area11.Desktop/ViewModels/DraftsViewModel.cs','pos':'dotnet/Area11.Desktop/ViewModels/PosViewModel.cs',
        'dv':'dotnet/Area11.Desktop/Views/DraftsView.xaml.cs','pv':'dotnet/Area11.Desktop/Views/PosView.xaml.cs',
        'tests':'dotnet/Area11.Tests/DraftHeldCurrentAccessTests.cs'}.items()}
    original={p:p.read_bytes() for p in files.values()};cases=[]
    for key,old,new in [
        ('draft','type = DraftType.Clean(type);',''),('draft','if (!DraftType.IsKnown(type))','if (false)'),
        ('draft','if (includePayload && type == DraftType.Purchase)','if (false)'),
        ('draft','return _purchases.RequireEntryReader(connection, user);','return user!;'),
        ('draft','var reader = RequireReader(connection, user, type, includePayload: true);','var reader = user;'),
        ('draft','var reader = RequireReader(connection, user, type, includePayload: false);','var reader = user;'),
        ('draft','by = reader.Name.Trim(),','by = user.Name,'),('draft','byId = reader.Id,','byId = user.Id,'),
        ('draft','_audit.Record(connection, reader,','_audit.Record(connection, user,'),
        ('draft','SELECT draft_type FROM document_drafts WHERE id = @id;','SELECT payload_json FROM document_drafts WHERE id = @id;'),
        ('hold','RequireReader(connection, user);','null!;'),('hold','userId = reader.Id,','userId = user.Id,'),
        ('hold','_audit.Record(connection, reader, "POS",','_audit.Record(connection, user, "POS",'),
        ('hold','ServiceResult<int>.Fail("The held carts could not be counted.")','ServiceResult<int>.Success(0)'),
        ('hold','Count(User? user)','Count()'),
        ('vm','if (!_listLoaded || !Drafts.Contains(draft))','if (!_listLoaded)'),
        ('vm','if (answer != MessageBoxResult.Yes || !CurrentSelection(draft, includePayload: true)) return;','if (answer != MessageBoxResult.Yes) return;'),
        ('vm','if (answer != MessageBoxResult.Yes || !CurrentSelection(draft, includePayload: false)) return;','if (answer != MessageBoxResult.Yes) return;'),
        ('vm','public bool HasNoDraft => _listLoaded && Drafts.Count == 0;','public bool HasNoDraft => Drafts.Count == 0;'),
        ('vm','finally { _busy = false; }',''),('vm','Do not post this draft again.','Try posting again.'),
        ('pos','if (!RefreshParkedAccess()) return;',''),
        ('pos','if (!_heldLoadSucceeded || !HeldCarts.Contains(row))','if (!_heldLoadSucceeded)'),
        ('dv','_accessTimer.Stop();',''),('pv','_parkedAccessTimer.Stop();',''),
        ('tests','Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);'),
        ('tests','[Fact]\n    public void CountReadFailureCannotClaimSuccessfulZero','public void CountReadFailureCannotClaimSuccessfulZero')]:
        cases.append((files[key],old,new))
    try:
        checks.check_draft_held_current_access();assert not checks.FAILURES,checks.FAILURES
        for p,old,new in cases:
            text=original[p].decode();assert old in text,(p,old)
            p.write_text(text.replace(old,new));checks.FAILURES.clear();checks.REPORT.clear();checks.check_draft_held_current_access()
            assert checks.FAILURES,'Survived '+old
            print('REJECT',p.name,old[:85]);p.write_bytes(original[p])
    finally:
        for p,data in original.items():p.write_bytes(data)
    assert all(p.read_bytes()==data for p,data in original.items())
    print(f'PASS {len(cases)} lexical negatives rejected; exact original bytes restored; NOT C#/WPF/session execution.')


if __name__=='__main__':run()
