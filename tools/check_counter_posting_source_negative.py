#!/usr/bin/env python3
"""Lexical source negatives only, not executable C#/WPF mutations. Run without concurrent edits."""
from pathlib import Path
import check_repo as checks
ROOT=Path(__file__).resolve().parents[1]
P=ROOT/'dotnet/Area11.Desktop/ViewModels/PosViewModel.cs'
R=ROOT/'dotnet/Area11.Desktop/ViewModels/ReturnsViewModel.cs'
S=ROOT/'dotnet/Area11.Core/Services/SaleService.cs'
T=ROOT/'dotnet/Area11.Tests/CounterPostingIntegrityTests.cs'


def run():
    original={p:p.read_bytes() for p in (P,R,S,T)};cases=[]
    for p,method,mutations in (
        (P,'CompleteSaleOnce',[('if (!result.Ok)','if (!result.Ok || result.Value is null)'),('_openDraftId = 0;',''),('Cart.Clear();',''),
                              ('TenderedText = string.Empty;',''),('SecondAmountText = string.Empty;',''),('_appliedExtraDiscount = 0;',''),
                              ('ResetBillBasis();',''),('if (!_lastLookupsSucceeded || !DocEditor.LoadedSuccessfully)','if (false)'),
                              ('catch (Exception exception)','catch (InvalidOperationException exception)'),('Do not bill or collect this sale again;','Try billing again;')]),
        (R,'TakeReturn',[('if (_returnInProgress) return;',''),('_returnInProgress = true;',''),('finally { _returnInProgress = false; }',''),
                        ('if (!result.Ok)','if (!result.Ok || result.Value is null)'),('_saleId = 0;',''),('ResetBill();',''),
                        ('if (!_lastQuarantineLoadSucceeded)','if (false)'),('catch (Exception exception)','catch (InvalidOperationException exception)'),
                        ('Do not return or refund these goods again;','Try refunding again;')])):
        text=original[p].decode();a=text.index('    private void '+method+'()');b=text.index('\n    private ',a+15);body=text[a:b]
        for old,new in mutations:
            assert old in body;cases.append((p,body,body.replace(old,new),method+': '+old))
    for p,old,new in [(S,'if (!HasOpenDrawer(connection))','if (false)'),(P,'_committedCartNeedsReset = true;',''),(P,'if (RefuseCommittedCart()) return;',''),(S,'return false;','return true;'),
                       (P,'_lastBarcodeLoadSucceeded = false;',''),(P,'_lastLookupsSucceeded = false;',''),
                       (R,'_lastQuarantineLoadSucceeded = false;',''),
                       (R,'if (!pending.Ok || pending.Value is null || !history.Ok || history.Value is null)','if (false)'),
                       (T,'[Fact]\n    public void ExtraReturnBStillRefunds560Then540AfterLateFaultAndRetry','public void ExtraReturnBStillRefunds560Then540AfterLateFaultAndRetry'),
                       (T,'Assert.Equal(before, f.Snapshot());','Assert.NotNull(before);')]:
        assert old in original[p].decode();cases.append((p,old,new,p.name+': '+old))
    try:
        checks.check_counter_posting_integrity();assert not checks.FAILURES,checks.FAILURES
        for p,old,new,label in cases:
            p.write_text(original[p].decode().replace(old,new))
            checks.FAILURES.clear();checks.REPORT.clear();checks.check_counter_posting_integrity()
            assert checks.FAILURES,'Survived '+label
            print('REJECT',label);p.write_bytes(original[p])
    finally:
        for p,b in original.items():p.write_bytes(b)
    assert all(p.read_bytes()==b for p,b in original.items())
    print(f'PASS {len(cases)} lexical negatives rejected; exact bytes restored; NOT C#/WPF execution')


if __name__=='__main__':run()
