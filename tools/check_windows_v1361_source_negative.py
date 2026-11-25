#!/usr/bin/env python3
"""Restorable lexical checks for the owner's two failures;not C#/scheduler execution."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    tests = ROOT / 'dotnet/Area11.Tests'
    cases = [(tests / 'TimeCollection.cs', 'DisableParallelization = true', 'DisableParallelization = false')]
    for name in ('TimeSourceTests', 'TimeCorrectionTests'):
        cases.extend([(tests / (name + '.cs'), '[Collection("Time")]', ''),
                      (tests / (name + '.cs'), 'public void Dispose() => TimeSource.Reset();', 'public void Dispose() { }')])
    native = tests / 'NativeFieldOutcomeTests.cs'
    for old, new in [
        ('p.SetRequired(TestData.Owner, f.Id, false, true, f.EditVersion)', 'p.SetRequired(TestData.Owner, f.Id, false, false, f.EditVersion)'),
        ('Assert.False(Field(p).Required);', ''),
        ('Assert.Equal(TestData.SeedOwnerName, Field(p).OverrideAcknowledgedBy);', ''),
        ('Assert.NotNull(Field(p).OverrideAcknowledgedAt);', ''),
        ('Assert.False(p.PrepareRequiredEdit(TestData.Owner, f.Id, f.EditVersion).Ok);', ''),
        ('NoCommit(p.SetRequiredChecked(TestData.Owner, f.Id, false, true, f.EditVersion)); Assert.Equal(before, Audits(t));', ''),
    ]: cases.append((native, old, new))
    cases.extend([(tests / 'StockPostingIntegrityTests.cs', 'long outCash = 4000', 'long outCash = 0'),
                  (tests / 'StockPostingIntegrityTests.cs', 'Assert.Equal(outCash, drawer.SupplierCashOutPaisa);', '')])
    originals = {p: p.read_bytes() for p, _, _ in cases}
    try:
        checks.check_windows_v1361_regressions(); assert not checks.FAILURES, checks.FAILURES
        for path, old, new in cases:
            text = originals[path].decode(); assert old in text, (path.name, old)
            path.write_text(text.replace(old, new))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_windows_v1361_regressions()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + old
            path.write_bytes(originals[path]); print('REJECT', path.name, old)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations;exact bytes restored.NOT C#/scheduler/runtime proof.')


if __name__ == '__main__': run()
