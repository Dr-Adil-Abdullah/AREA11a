#!/usr/bin/env python3
"""Lexical receipt/history negative probes; run alone. Not C#/WPF execution."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]


def run():
    files={key:ROOT/path for key,path in {
        'sale':'dotnet/Area11.Core/Services/ReceiptService.cs',
        'return':'dotnet/Area11.Core/Services/ReturnReceiptService.cs',
        'history':'dotnet/Area11.Core/Services/SaleHistoryService.cs',
        'vm':'dotnet/Area11.Desktop/ViewModels/SaleHistoryViewModel.cs',
        'view':'dotnet/Area11.Desktop/Views/SaleHistoryView.xaml.cs',
        'tests':'dotnet/Area11.Tests/ReceiptCurrentAccessTests.cs'}.items()}
    originals={p:p.read_bytes() for p in files.values()}
    cases=[]
    for key,module in (('sale','Pos'),('return','Returns')):
        for old,new in [('SnapshotReadAccess.Require(_db, connection, _permissions, user, module);',''),
                        ('ReceiptReadPurpose.Counter => Modules.'+module,'ReceiptReadPurpose.Counter => Modules.Sales'),
                        ('ReceiptReadPurpose.History => Modules.Sales','ReceiptReadPurpose.History => Modules.Pos'),
                        ('_ => throw new ValidationException','_ => new ValidationException')]:
            cases.append((files[key],old,new))
    for key,old,new in [
        ('history','CanLook(User? user) => CheckReadAccess(user).Ok;','CanLook(User? user) => true;'),
        ('history','RequireReader(connection, user);',''),
        ('history','var reader = RequireReader(connection, user);','var reader = user;'),
        ('history','_audit.Record(connection, reader, Modules.Sales','_audit.Record(connection, user, Modules.Sales'),
        ('history','_receipts.Build(saleId, user, ReceiptReadPurpose.History)','_receipts.Build(saleId, user)'),
        ('history','_returnReceipts.Build(code, user, ReceiptReadPurpose.History)','_returnReceipts.Build(code, user)'),
        ('history','_receipts.Build(id, user, ReceiptReadPurpose.History)','_receipts.Build(id, user)'),
        ('history','RequireReader(receiptConnection, user);',''),
        ('history','A selected bill changed.','Selected bill disappeared.'),
        ('history','if (exists == 0) throw new ValidationException','if (false) throw new ValidationException'),
        ('vm','if (_checkingReadAccess || _clearingHistoryPayload) return false;','if (_checkingReadAccess) return false;'),
        ('vm','RecordReprint(_user, selected.Id)','RecordReprint(_user, Selected.Id)'),
        ('vm','if (!RefreshReadAccess()) return;',''),
        ('vm','_printSelection = new List<SaleHistoryRow>();',''),
        ('vm','CountText = "-";',''),('vm','TotalText = "-";',''),('vm','DueText = "-";',''),
        ('view','_accessTimer.Stop();',''),('view','TimeSpan.FromSeconds(3)','TimeSpan.FromMinutes(30)'),
        ('tests','Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);'),
        ('tests','[Fact]\n    public void AuditInsertFaultIsFailureAndDoesNotSilentlyClaimRecorded','public void AuditInsertFaultIsFailureAndDoesNotSilentlyClaimRecorded')]:
        cases.append((files[key],old,new))
    try:
        checks.check_receipt_current_access();assert not checks.FAILURES,checks.FAILURES
        for p,old,new in cases:
            text=originals[p].decode();assert old in text,(p,old)
            p.write_text(text.replace(old,new));checks.FAILURES.clear();checks.REPORT.clear();checks.check_receipt_current_access()
            assert checks.FAILURES,'Survived '+old
            print('REJECT',p.name,old);p.write_bytes(originals[p])
    finally:
        for p,data in originals.items():p.write_bytes(data)
    assert all(p.read_bytes()==data for p,data in originals.items())
    print(f'PASS {len(cases)} lexical negatives rejected; original bytes restored. NOT C#/session/WPF.')


if __name__=='__main__':run()
