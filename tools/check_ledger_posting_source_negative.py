#!/usr/bin/env python3
"""Lexical negative checks only, not C#/WPF mutation testing. Run without concurrent edits/builds."""
from pathlib import Path
import check_repo as checks

ROOT=Path(__file__).resolve().parents[1]
C=ROOT/'dotnet/Area11.Desktop/ViewModels/CustomersViewModel.cs'
S=ROOT/'dotnet/Area11.Desktop/ViewModels/SuppliersViewModel.cs'
T=ROOT/'dotnet/Area11.Tests/LedgerPostingIntegrityTests.cs'


def run():
    original={p:p.read_bytes() for p in (C,S,T)};cases=[]
    for p,method,flag,note,loaded in ((C,'Collect','_collectInProgress','Note','_lastCustomerLoadSucceeded'),(S,'Pay','_payInProgress','PayNote','_lastSupplierLoadSucceeded')):
        text=original[p].decode();a=text.index('    private void '+method+'()');b=text.index('\n    private ',a+15)
        body=text[a:b]
        for old,new,label in (
            ('if ('+flag+') return;','','reentrant post accepted'),
            (flag+' = true;',flag+' = false;','posting latch not acquired'),
            ('finally { '+flag+' = false; }','','posting latch never released'),
            ('AmountText = string.Empty;','','committed authored amount retained'),
            (note+' = string.Empty;','','committed note retained'),
            ('if (!'+loaded+')','if (false)','failed reload certified success'),
            ('_onChanged?.Invoke();\n                if (!UpdateAccess())','_onChanged?.Invoke();\n                if (false)','callback revocation not checked'),
            ('ClearSensitiveData();','','postcommit exception keeps stale page'),
            ('Do not '+('receive' if p==C else 'pay')+' again;','Try the action again;','ambiguous retry instruction')):
            assert old in body,label;cases.append((p,body,body.replace(old,new),method+': '+label))
    cases.extend([
        (S,'_lastSupplierLoadSucceeded = false;\n        if (!UpdateAccess()) return;\n        var result = _services.SupplierLedger.List(_user, SearchText, OnlyOwing);','var result = _services.SupplierLedger.List(_user, SearchText, OnlyOwing);','supplier reload current-access/verdict reset removed'),
        (C,'AmountText = Note = string.Empty;','','customer payment inputs retained after access loss'),
        (S,'AmountText = PayNote = string.Empty;','','supplier payment inputs retained after access loss'),
        (T,'[Fact] public void PartialRepeatsAreDistinctNewPaymentsNotGlobalIdempotency()','public void PartialRepeatsAreDistinctNewPaymentsNotGlobalIdempotency()','partial-repeat boundary unregistered'),
        (T,'[InlineData(false,false)]','','same-instance competition unregistered'),
    ])
    try:
        checks.check_ledger_posting_integrity();assert not checks.FAILURES,checks.FAILURES
        for p,old,new,label in cases:
            text=original[p].decode();assert old in text,label;p.write_text(text.replace(old,new,1))
            checks.FAILURES.clear();checks.REPORT.clear();checks.check_ledger_posting_integrity()
            assert checks.FAILURES,'Survived: '+label
            print('REJECT',label);p.write_bytes(original[p])
    finally:
        for p,b in original.items():p.write_bytes(b)
    assert all(p.read_bytes()==b for p,b in original.items())
    print(f'PASS {len(cases)} lexical negatives rejected; exact original bytes restored; NOT C#/WPF execution')


if __name__=='__main__':run()
