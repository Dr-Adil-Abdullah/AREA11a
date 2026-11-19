#!/usr/bin/env python3
"""Lexical quarantine guard negatives, not executed C#/WPF. Run alone; exact restore required."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]


def run():
    files={k:ROOT/p for k,p in {
        'core':'dotnet/Area11.Core/Services/QuarantineService.cs',
        'vm':'dotnet/Area11.Desktop/ViewModels/ReturnsViewModel.cs',
        'view':'dotnet/Area11.Desktop/Views/ReturnsView.xaml',
        'tests':'dotnet/Area11.Tests/QuarantineCurrentAccessTests.cs'}.items()}
    original={p:p.read_bytes() for p in files.values()};cases=[]
    for key,old,new in [
        ('core','Pending(User? user)','Pending()'),('core','History(User? user)','History()'),('core','PendingCount(User? user)','PendingCount()'),
        ('core','RequireReader(connection, user);','null!;'),
        ('core','ServiceResult<int>.Fail("The quarantine count could not be read.")','ServiceResult<int>.Success(0)'),
        ('core','SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Returns);','SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Pos);'),
        ('core','var reader = RequireDecider(connection, user);','var reader = user;'),
        ('core','if (!reader.CanManage)','if (false)'),
        ('core','userId = reader.Id','userId = user.Id'),
        ('core','AddBack(connection, row, clean, stamp, reader);','AddBack(connection, row, clean, stamp, user);'),
        ('core','_audit.Record(connection, reader,','_audit.Record(connection, user,'),
        ('core','That item already has a decision;','Already decided;'),
        ('core','if (clean == Expired && cleanNote.Length < 3)','if (false)'),
        ('core','if (clean is Restocked or Sold)','if (true)'),
        ('vm','_services.Quarantine.Pending(_user)','_services.Quarantine.Pending(null)'),
        ('vm','_services.Quarantine.History(_user)','_services.Quarantine.History(null)'),
        ('vm','access.Ok && _services.Quarantine.CheckDecisionAccess(_user).Ok','access.Ok'),
        ('vm','if (!CanDecideQuarantine || !Pending.Contains(row))','if (!CanDecideQuarantine)'),
        ('vm','Pending.Remove(row);',''),('vm','var saved = false;','var saved = true;'),
        ('vm','Do not decide these goods again.','Try deciding again.'),
        ('view','IsEnabled="{Binding DataContext.CanDecideQuarantine, RelativeSource={RelativeSource AncestorType=UserControl}}"',''),
        ('tests','Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);'),
        ('tests','Assert.Equal(money, f.MoneySnapshot());','Assert.NotNull(money);'),
        ('tests','[Fact]\n    public void FailedCountIsNotCertifiedZeroAndUnreadableRowsHaveNoPayload','public void FailedCountIsNotCertifiedZeroAndUnreadableRowsHaveNoPayload')]:
        cases.append((files[key],old,new))
    # Scope shared UI latch/exception mutations to Decide, not Return posting.
    p=files['vm'];text=original[p].decode();a=text.index('    private void Decide(');b=text.index('    private void SetStatus(',a);body=text[a:b]
    for old,new in [('if (_busy) return;',''),('finally { _busy = false; }',''),('catch (Exception exception)','catch (InvalidOperationException exception)'),('row.NoteText = string.Empty;','')]:
        assert old in body;cases.append((p,body,body.replace(old,new)))
    try:
        checks.check_quarantine_current_access();assert not checks.FAILURES,checks.FAILURES
        for p,old,new in cases:
            text=original[p].decode();assert old in text,(p,old)
            p.write_text(text.replace(old,new));checks.FAILURES.clear();checks.REPORT.clear();checks.check_quarantine_current_access()
            assert checks.FAILURES,'Survived '+old
            print('REJECT',p.name,old[:90]);p.write_bytes(original[p])
    finally:
        for p,data in original.items():p.write_bytes(data)
    assert all(p.read_bytes()==data for p,data in original.items())
    print(f'PASS {len(cases)} lexical negatives rejected; exact bytes restored. NOT C#/WPF/session execution.')


if __name__=='__main__':run()
