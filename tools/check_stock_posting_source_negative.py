#!/usr/bin/env python3
"""Lexical negatives only; not C#/WPF mutation execution. Do not run with concurrent edits."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]
P=ROOT/'dotnet/Area11.Desktop/ViewModels/PurchasesViewModel.cs'
S=ROOT/'dotnet/Area11.Desktop/ViewModels/SuppliersViewModel.cs'
T=ROOT/'dotnet/Area11.Tests/StockPostingIntegrityTests.cs'


def run():
    original={p:p.read_bytes() for p in (P,S,T)};cases=[]
    for p,method,flag,clear,verdict in ((P,'Save','_saveInProgress','Lines.Clear();','if (!_lastLookupsSucceeded || !DocEditor.LoadedSuccessfully || !RefreshEntryAccess())'),(S,'PostReturn','_returnInProgress','ClearTray();','if (!_lastSupplierLoadSucceeded)')):
        text=original[p].decode();a=text.index('    private void '+method+'()');b=text.index('\n    private ',a+15);body=text[a:b]
        for old,new in [('if ('+flag+') return;',''),(flag+' = true;',flag+' = false;'),('finally { '+flag+' = false; }',''),
                        (clear,''),(verdict,'if (false)'),('catch (Exception exception)','catch (InvalidOperationException exception)'),
                        ('if (!result.Ok)','if (!result.Ok || result.Value is null)')]:
            assert old in body;cases.append((p,body,body.replace(old,new),method+': '+old))
    for p,old,new in [(P,'_lastLookupsSucceeded = false;',''),(P,'if (!suppliers.Ok || suppliers.Value is null)','if (false)'),
                       (P,'if (!products.Ok || products.Value is null)','if (false)'),
                       (S,'if (!LoadExpired())','if (false)'),(S,'_onChanged?.Invoke();\n                if (!UpdateAccess())','_onChanged?.Invoke();\n                if (false)'),
                       (T,'[Fact]\n    public void SampleDeliveryStillAddsStockAtZeroCostWithoutInventingPayable','public void SampleDeliveryStillAddsStockAtZeroCostWithoutInventingPayable'),
                       (T,'"sqlite_sequence"','"omitted_sequence"'),(T,'Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);')]:
        text=original[p].decode();assert old in text;cases.append((p,old,new,str(p.name)+': '+old))
    try:
        checks.check_stock_posting_integrity();assert not checks.FAILURES,checks.FAILURES
        for p,old,new,label in cases:
            p.write_text(original[p].decode().replace(old,new))
            checks.FAILURES.clear();checks.REPORT.clear();checks.check_stock_posting_integrity()
            assert checks.FAILURES,'Survived '+label
            print('REJECT',label);p.write_bytes(original[p])
    finally:
        for p,b in original.items():p.write_bytes(b)
    assert all(p.read_bytes()==b for p,b in original.items())
    print(f'PASS {len(cases)} lexical negatives rejected; exact bytes restored; NOT C#/WPF execution')


if __name__=='__main__':run()
