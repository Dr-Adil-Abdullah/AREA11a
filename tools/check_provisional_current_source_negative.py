#!/usr/bin/env python3
"""P40 lexical negative controls only; run alone, exact source bytes restored."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]


def run():
    files={k:ROOT/p for k,p in {'core':'dotnet/Area11.Core/Services/ProvisionalReturnService.cs',
        'receipt':'dotnet/Area11.Core/Services/ProvisionalReceiptService.cs',
        'vm':'dotnet/Area11.Desktop/ViewModels/ProvisionalViewModel.cs',
        'view':'dotnet/Area11.Desktop/Views/ProvisionalView.xaml.cs','xaml':'dotnet/Area11.Desktop/Views/ProvisionalView.xaml',
        'tests':'dotnet/Area11.Tests/ProvisionalCurrentAccessTests.cs'}.items()}
    cases=[('core','SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Returns);','user!;'),
           ('core','user = RequireReader(connection, user);',''),('core','user = RequireDecider(connection, user);',''),
           ('core','if (!current.CanManage) throw','if (false) throw'),('core','RequireReader(connection, user);',''),
           ('core','lines.Count == 0 || lines.Count != row.ItemCount','false'),('core','}, outcome);','});'),
           ('receipt','_provisional.ReadReceipt(clean, user)','_provisional.ReadReceipt(clean, null)'),
           ('receipt','if (!read.Ok || read.Value is null)','if (false)'),
           ('vm','_services.Provisional.Page(_user)','_services.Provisional.Page(null)'),
           ('vm','_services.Provisional.CheckAccess(_user, decision: true)','_services.Provisional.CheckAccess(_user)'),
           ('vm','if (!RefreshAccess()) return;',''),('vm','if (wasDecider && !_decisionAllowed)','if (false)'),
           ('vm','_listLoaded = false;',''),('vm','ClearReadState();',''),('vm','ClearDecisionState();',''),
           ('vm','Results.Clear(); Lines.Clear();',''),('vm','Pending.Concat(History).Contains(SelectedEntry)','true'),
           ('vm','Lookup.Contains(row)','true'),('vm','_services.ProvisionalReceipts.Build(code, _user)','_services.ProvisionalReceipts.Build(code)'),
           ('vm','_services.Provisional.SearchBills(LookupText, user: _user)','_services.Provisional.SearchBills(LookupText)'),
           ('vm','if (!_listLoaded) throw new InvalidOperationException("Saved PR could not refresh its list.");',''),
           ('view','TimeSpan.FromSeconds(3)','TimeSpan.FromSeconds(30)'),('view','_accessTimer.Stop()','_accessTimer.Start()'),
           ('xaml','IsEnabled="{Binding CanWork}"',''),
           ('tests','Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);'),
           ('tests','[InlineData("page", "null")]','')]
    originals={p:p.read_bytes() for p in files.values()}
    try:
        checks.check_provisional_current_access();assert not checks.FAILURES,checks.FAILURES
        for key,old,new in cases:
            p=files[key];text=originals[p].decode();assert old in text,(key,old)
            p.write_text(text.replace(old,new));checks.FAILURES.clear();checks.REPORT.clear();checks.check_provisional_current_access()
            assert checks.FAILURES,'Survived '+key+':'+old
            p.write_bytes(originals[p]);print('REJECT',key,old)
    finally:
        for p,raw in originals.items():p.write_bytes(raw)
    assert all(p.read_bytes()==raw for p,raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact bytes restored.NOT C#/WPF/session/provider proof.')


if __name__=='__main__':run()
