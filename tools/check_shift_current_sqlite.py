#!/usr/bin/env python3
"""Actual Shift/Audit SQL on synthetic memory; Python orchestrates rollback, NOT C# execution."""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run():
    c = sqlite3.connect(':memory:')
    c.row_factory = sqlite3.Row
    c.execute('PRAGMA foreign_keys=ON')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for p in migrations:
        c.executescript(p.read_text())
    source = (ROOT / 'dotnet/Area11.Core/Services/ShiftService.cs').read_text()
    blocks = re.findall(r'@"(.*?)"', source, re.S)
    assert len(blocks) == 13
    open_sql = next(b for b in blocks if b.strip().startswith('INSERT INTO shifts(user_id, opened_at, opening_float_paisa, status, note)'))
    close_sql = next(b for b in blocks if b.strip().startswith('UPDATE shifts') and '@closedAt' in b)
    hand_close = next(b for b in blocks if b.strip().startswith('UPDATE shifts') and 'handed_to_user_id' in b)
    hand_open = next(b for b in blocks if b.strip().startswith('INSERT INTO shifts') and 'opened_from_shift_id' in b)
    discrepancy = next(b for b in blocks if b.strip().startswith('INSERT INTO shift_discrepancies'))
    read_open = next(b for b in blocks if "WHERE s.status = 'open'" in b)
    history = next(b for b in blocks if 'ORDER BY s.id DESC' in b and 'LIMIT @limit' in b)
    fill = next(b for b in blocks if 'AS cashSalesPaisa' in b)
    audit = next(b for b in re.findall(r'@"(.*?)"', (ROOT / 'dotnet/Area11.Core/Services/AuditService.cs').read_text(), re.S)
                 if b.strip().startswith('INSERT INTO audit_logs'))
    c.executescript("""
INSERT INTO users(id,name,role) VALUES(1,'Synthetic actor','owner'),(2,'Synthetic recipient','cashier');
INSERT INTO customers(id,name) VALUES(1,'Synthetic customer');
INSERT INTO suppliers(id,name) VALUES(1,'Synthetic supplier');
INSERT INTO sales(id,code,date,status,total_paisa,paid_paisa) VALUES(1,'SYN-SALE','2026-11-10 12:00:00','paid',30000,30000);
INSERT INTO payments(date,method,amount_paisa,sale_id,user_id) VALUES('2026-11-10 12:00:00','cash',30000,1,1);
INSERT INTO payments(date,method,amount_paisa,customer_id,user_id) VALUES('2026-11-10 12:00:00','cash',2000,1,1);
INSERT INTO payments(date,method,amount_paisa,supplier_id,user_id) VALUES('2026-11-10 12:00:00','cash',3000,1,1);
INSERT INTO supplier_returns(code,date,supplier_id,total_paisa,settled) VALUES('SYN-SR','2026-11-10 12:00:00',1,4000,1);
INSERT INTO expenses(date,title,amount_paisa) VALUES('2026-11-10 12:00:00','Synthetic expense',500);
INSERT INTO owner_drawings(date,type,amount_paisa) VALUES('2026-11-10 12:00:00','cash',600),('2026-11-10 12:00:00','goods',999999);
""")
    stamp = '2026-11-10 12:00:00'
    p = dict(userId=1, at=stamp, openingFloat=10000, note='Synthetic count')
    c.execute(open_sql, p)
    shift_id = c.execute('SELECT last_insert_rowid()').fetchone()[0]
    c.commit()
    before = list(c.iterdump())
    assert c.execute(read_open).fetchone()['id'] == shift_id
    assert len(list(c.execute(history, {'limit': 1}))) == 1
    money = dict(c.execute(fill, {'from': stamp, 'to': stamp}).fetchone())
    assert (money['cashSalesPaisa'], money['cashCollectionsPaisa'], money['supplierCashOutPaisa'], money['supplierCashInPaisa'], money['expensesPaisa'], money['drawingsPaisa']) == (30000, 2000, 3000, 4000, 500, 600)
    assert money['cashRefundsPaisa'] == 0
    after_boundary = dict(c.execute(fill.replace('>= @from', '> @from'), {'from': stamp, 'to': stamp}).fetchone())
    assert all(value == 0 for value in after_boundary.values()), 'handover boundary does not recount same-second payments'
    assert list(c.iterdump()) == before
    expected = 41900  # 10000+30000+2000+4000-3000-500-600; Python expectation, not C# FillDrawer execution.
    aud = dict(at=stamp, userId=1, userName='Synthetic actor', action='synthetic_shift', module='Cash', entity='shifts', entityId=str(shift_id), oldValue=None, newValue='synthetic', details='synthetic')
    dp = dict(shiftId=shift_id, stage='closing', expected=expected, counted=expected-100, difference=-100, reason='Synthetic one rupee', at=stamp)
    for fault_table in ('audit_logs', 'shift_discrepancies'):
        c.execute(f"CREATE TRIGGER synthetic_shift_fault BEFORE INSERT ON {fault_table} BEGIN SELECT RAISE(ABORT,'synthetic-shift-fault'); END;")
        c.commit()
        for operation in ('open', 'close', 'handover'):
            before_fault = list(c.iterdump())
            c.execute('BEGIN IMMEDIATE')
            try:
                if operation == 'open':
                    c.execute(open_sql, p)
                elif operation == 'close':
                    c.execute(close_sql, dict(closedAt=stamp, expected=expected, actual=expected-100, difference=-100, note='Synthetic', id=shift_id))
                else:
                    c.execute(hand_close, dict(at=stamp, expected=expected, actual=expected-100, difference=-100, note='Synthetic', nextId=2, nextName='Synthetic recipient', id=shift_id))
                    c.execute(hand_open, dict(userId=2, at=stamp, openingFloat=expected-100, note='Synthetic handover', fromId=shift_id))
                if fault_table == 'shift_discrepancies':
                    c.execute(audit, aud)
                    c.execute(discrepancy, dp)
                else:
                    c.execute(discrepancy, dp)
                    c.execute(audit, aud)
            except sqlite3.IntegrityError:
                c.rollback()
            else:
                raise AssertionError('injected fault was not raised')
            assert list(c.iterdump()) == before_fault, (operation, fault_table)
        c.execute('DROP TRIGGER synthetic_shift_fault')
        c.commit()
    c.execute('BEGIN IMMEDIATE')
    c.execute(hand_close, dict(at=stamp, expected=expected, actual=expected-100, difference=-100, note='Synthetic one rupee', nextId=2, nextName='Synthetic recipient', id=shift_id))
    c.execute(hand_open, dict(userId=2, at=stamp, openingFloat=expected-100, note='Synthetic handover', fromId=shift_id))
    c.execute(discrepancy, dp)
    c.execute(audit, aud)
    c.commit()
    current = c.execute(read_open).fetchone()
    assert current['userId'] == 2 and current['openingFloatPaisa'] == expected-100 and current['openedFromShiftId'] == shift_id
    assert c.execute('SELECT difference_paisa FROM shift_discrepancies').fetchone()[0] == -100
    read_dump = list(c.iterdump())
    for sql in blocks:
        if sql.strip().startswith('SELECT'):
            list(c.execute(sql, dict(id=shift_id, limit=60, cut='-3650 days', **{'from': stamp, 'to': stamp})))
    assert list(c.iterdump()) == read_dump
    assert not c.execute('PRAGMA foreign_key_check').fetchall()
    print(f'PASS {len(migrations)} migrations/13 actual Shift SQL blocks: drawer source fields/goods exclusion, inclusive vs strict handover boundary, history read-only, six audit/discrepancy-fault SQL rollback paths, counted-next-float/one-rupee difference/FK')
    print('NOT executed: C# current Cash/recipient/role authorization, Database.Tx, xUnit parallelism/retry, WPF/timer/cached slip/printer/session/Windows. Python orchestrates synthetic SQL only.')


if __name__ == '__main__':
    run()
