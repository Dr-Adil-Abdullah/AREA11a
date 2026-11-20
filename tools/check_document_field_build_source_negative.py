#!/usr/bin/env python3
"""Narrow lexical regression checks; NOT .NET compile/runtime proof."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    test = ROOT / 'dotnet/Area11.Tests/DocumentFieldTests.cs'
    service = ROOT / 'dotnet/Area11.Core/Services/ExpenseService.cs'
    cases = [
        (test, 'cashOut.List(TestData.Owner)', 'cashOut.List()'),
        (test, 'cashOut.List(TestData.Owner)', 'cashOut.List(null)'),
        (test, 'Assert.Single(rows);', ''),
        (test, 'Assert.Contains("Approved by: Owner", rows[0].ExtraLabel);', ''),
        (service, 'List(User? user, int limit = 200)', 'List(User? user = null, int limit = 200)')]
    originals = {p: p.read_bytes() for p in (test, service)}
    try:
        checks.check_document_field_expense_reader(); assert not checks.FAILURES, checks.FAILURES
        for p, old, new in cases:
            text = originals[p].decode(); assert old in text, old
            p.write_text(text.replace(old, new)); checks.FAILURES.clear(); checks.REPORT.clear()
            checks.check_document_field_expense_reader(); assert checks.FAILURES, 'Survived: ' + old
            p.write_bytes(originals[p]); print('REJECT', old)
    finally:
        for p, raw in originals.items(): p.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected; exact original bytes restored. NOT .NET compilation.')


if __name__ == '__main__': run()
