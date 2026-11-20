#!/usr/bin/env python3
"""Lexical current-counter negatives, NOT executable C#/WPF mutations. Run without concurrent edits."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]
S=ROOT/'dotnet/Area11.Core/Services/SaleService.cs'
R=ROOT/'dotnet/Area11.Core/Services/ReturnService.cs'
D=ROOT/'dotnet/Area11.Core/Services/DraftService.cs'
V=ROOT/'dotnet/Area11.Desktop/ViewModels/ReturnsViewModel.cs'
W=ROOT/'dotnet/Area11.Desktop/Views/ReturnsView.xaml.cs'
T=ROOT/'dotnet/Area11.Tests/CounterCurrentAccessTests.cs'


def run():
    original={p:p.read_bytes() for p in (S,R,D,V,W,T)};cases=[]
    for p,module,signatures in ((S,'Pos',['CounterWarnings(SaleInput input, User? user)','Quote(SaleInput input, User user)']),
                                (R,'Returns',['FindSale(string? code, User? user)','Search(SaleLookupInput? input, User? user)','Lines(int saleId, User? user)','Quote(ReturnInput input, User user)'])):
        text=original[p].decode()
        for signature in signatures:
            a=text.index(signature);b=text.find('\n    public ',a+len(signature));body=text[a:b if b!=-1 else len(text)]
            old='RequirePostingReader(connection, user);';assert old in body
            cases.append((p,body,body.replace(old,'null!;',1),signature+' gate removed'))
        for old,new in [('var reader = RequirePostingReader(connection, user);','var reader = user;'),
                        ('return Save(connection, input, reader);','return Save(connection, input, user);'),
                        ('SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.'+module+');','SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Reports);')]:
            cases.append((p,old,new,p.name+': '+old))
    for p,old,new in [(D,'connection =>\n            {\n                _sales.RequirePostingReader(connection, user);','null'),
                       (D,'_sales.CheckPostingAccess(user)','ServiceResult.Success()'),
                       (D,'Finalize(connection, _sales.RequirePostingReader(connection, user), draftId, DraftType.Sale, id)','Finalize(connection, user, draftId, DraftType.Sale, id)'),
                       (V,'Pending.Clear();',''),(V,'History.Clear();',''),(V,'_saleId = 0;',''),
                       (V,'if (!RefreshPostingAccess()) return;',''),(V,'FindSale(BillCode, _user)','FindSale(BillCode, null)'),
                       (W,'_accessTimer.Stop();',''),(W,'TimeSpan.FromSeconds(3)','TimeSpan.FromMinutes(30)'),
                       (T,'[Fact]\n    public void SuccessfulPostsUseStoredCurrentNameNotCallerAuthoredName','public void SuccessfulPostsUseStoredCurrentNameNotCallerAuthoredName'),
                       (T,'Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);')]:
        assert old in original[p].decode(),old;cases.append((p,old,new,p.name+': '+old))
    try:
        checks.check_counter_current_access();assert not checks.FAILURES,checks.FAILURES
        for p,old,new,label in cases:
            p.write_text(original[p].decode().replace(old,new))
            checks.FAILURES.clear();checks.REPORT.clear();checks.check_counter_current_access()
            assert checks.FAILURES,'Survived '+label
            print('REJECT',label);p.write_bytes(original[p])
    finally:
        for p,b in original.items():p.write_bytes(b)
    assert all(p.read_bytes()==b for p,b in original.items())
    print(f'PASS {len(cases)} lexical negatives rejected; exact original bytes restored; NOT C#/WPF execution')


if __name__=='__main__':run()
