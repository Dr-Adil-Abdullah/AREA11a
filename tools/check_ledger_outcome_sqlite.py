#!/usr/bin/env python3
"""Actual unchanged ledger SQL, Python-owned plans/Tx/guards and simulated lost ack.
NOT C# service/Database.Tx/permission/WPF/provider COMMIT-failure/durable identity proof.
"""
from pathlib import Path
import itertools
import sqlite3
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]
SERVICES = ROOT / 'dotnet/Area11.Core/Services'


class GuardReference:
    def __init__(self): self.state = 'ready'
    def begin(self):
        if self.state != 'ready': return False
        self.state = 'inflight'; return True
    def fail(self, no_commit): self.state = 'ready' if no_commit else 'verify'
    def ack(self): self.state = 'saved-needs-reset'
    def reset(self):
        assert self.state == 'saved-needs-reset'; self.state = 'ready'


def run():
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    blocks = [(SERVICES / file).read_text().split(start, 1)[1].split(end, 1)[0] for file, start, end in (
        ('CustomerLedgerService.cs', 'public ServiceResult<CollectionResult> Collect(', 'public ServiceResult<ReceiptDocument> Statement('),
        ('SupplierLedgerService.cs', 'public ServiceResult<SupplierPayment> Pay(', 'public ServiceResult<SupplierReturnResult> Return('))]
    audit = quoted((SERVICES / 'AuditService.cs').read_text().split('public void Record(', 1)[1].split('public void RecordChange(', 1)[0], 'INSERT INTO audit_logs')
    def seed():
        c = sqlite3.connect(':memory:'); c.execute('PRAGMA foreign_keys=ON')
        for p in migrations: c.executescript(p.read_text())
        c.executescript("INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic','owner',1); INSERT INTO customers(id,name,opening_balance_paisa,balance_paisa) VALUES(1,'Synthetic',100000,100000); INSERT INTO suppliers(id,name,opening_balance_paisa,balance_paisa) VALUES(1,'Synthetic',100000,100000);")
        return c
    def execute(c, side, amount=30000, method='cash'):
        table = 'suppliers' if side else 'customers'; before = c.execute('SELECT balance_paisa FROM ' + table + ' WHERE id=1').fetchone()[0]
        values = dict(at='2026-11-10', method=method, amount=amount, customerId=1, supplierId=1, userId=1, note='Synthetic reference', balance=before-amount, id=1,
                      userName='Synthetic', action='supplier_pay' if side else 'customer_collect', module='Suppliers' if side else 'Customers', entity=table, entityId='1', oldValue=str(before), newValue=str(before-amount), details='Synthetic reference')
        c.execute(quoted(blocks[side], 'INSERT INTO payments'), values)
        c.execute(quoted(blocks[side], 'UPDATE ' + table), values)
        c.execute(audit, values)
    faults = 0
    for side in (0, 1):
        table = 'suppliers' if side else 'customers'
        for timing, event in itertools.product(('BEFORE', 'AFTER'), ('INSERT ON payments', 'UPDATE OF balance_paisa ON ' + table, 'INSERT ON audit_logs')):
            c = seed(); before = tuple(c.iterdump()); guard = GuardReference(); assert guard.begin()
            c.execute('CREATE TRIGGER synthetic_outcome_fault ' + timing + ' ' + event + " BEGIN SELECT RAISE(ABORT,'synthetic-fault'); END;")
            c.execute('BEGIN IMMEDIATE')
            try:
                execute(c, side); raise AssertionError('fault not reached')
            except sqlite3.IntegrityError:
                c.rollback(); guard.fail(True)  # Python observed rollback; NOT C# NoCommitConfirmed.
            c.execute('DROP TRIGGER synthetic_outcome_fault'); assert tuple(c.iterdump()) == before
            assert guard.begin(); c.execute('BEGIN IMMEDIATE'); execute(c, side); c.commit(); guard.ack()
            assert not guard.begin(); guard.reset(); assert c.execute('SELECT COUNT(*) FROM payments').fetchone()[0] == 1
            assert c.execute('SELECT balance_paisa FROM ' + table).fetchone()[0] == 70000
            c.close(); faults += 1
    modeled = 0
    for side in (0, 1):
        for scenario in ('lost-ack', 'refresh-fault', 'normal'):
            c = seed(); guard = GuardReference(); assert guard.begin(); c.execute('BEGIN IMMEDIATE'); execute(c, side); c.commit()
            if scenario == 'lost-ack': guard.fail(False)  # Commit succeeded; acknowledgement notionally lost.
            else:
                guard.ack()
                if scenario == 'normal': guard.reset()
            assert guard.begin() == (scenario == 'normal')
            assert c.execute('SELECT COUNT(*) FROM payments').fetchone()[0] == 1
            # New page has no durable identity. Same valid partial amount remains a new payment.
            fresh = GuardReference(); assert fresh.begin(); c.execute('BEGIN IMMEDIATE'); execute(c, side); c.commit()
            assert c.execute('SELECT COUNT(*) FROM payments').fetchone()[0] == 2
            c.close(); modeled += 1
    print(f'PASS {len(migrations)} migrations;{faults} actual ledger-SQL fault rollbacks/retry;{modeled} synthetic ack/refresh/normal scenarios including explicit reopen non-dedup boundary. NOT C#/provider/WPF proof.')


if __name__ == '__main__': run()
