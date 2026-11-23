#!/usr/bin/env python3
"""Python-owned Hardware compare/transaction reference,not C#/auth/cache/UI/driver proof."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]
KEYS = ['hardware.testedAt', 'hardware.testedBy', 'hardware.note', 'hardware.testedWidth', 'hardware.passed',
        'printer.name', 'printer.width', 'printer.copies', 'printer.autoCut']


def run():
    core = ROOT / 'dotnet/Area11.Core'
    source = (core / 'Services/SettingsService.cs').read_text().split('internal ServiceResult SaveHardwareTest(', 1)[1].split('internal ServiceResult SaveRecoveryHash', 1)[0]
    sql = quoted(source, 'INSERT INTO settings')
    audit_sql = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-hardware-edit-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db'))
            one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.executescript("INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');")
            def put(db, key, value): db.execute(sql, dict(key=key, value=json.dumps(value), at='2026-11-10 12:00:00'))
            def fingerprint(db, keys=KEYS):
                return hashlib.sha256(json.dumps([db.execute('SELECT key,value FROM settings WHERE key=?', (k,)).fetchall()
                                                  for k in keys], separators=(',', ':')).encode()).digest()
            def state(): return tuple(one.iterdump())
            def write_record():
                for key, value in zip(KEYS[:5], ['2026-11-10 12:00:00', 'Synthetic owner', 'synthetic note', 58, 'bill,cut,fits,found,printed,scanner']): put(one, key, value)
                one.execute(audit_sql, dict(at='2026-11-10 12:00:00', userId=9001, userName='Synthetic owner',
                    action='hardware_test', module='Settings', entity=None, entityId=None, oldValue=None, newValue=None, details='Synthetic result'))
            for key, value in zip(KEYS, ['', '', '', 0, '', '', 58, 1, True]): put(one, key, value)
            one.commit(); assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            # Every record and exact printer dependency matters;refusal performs no writes.
            for key in KEYS:
                old = fingerprint(one); put(two, key, 'synthetic concurrent change'); two.commit(); before = state()
                one.execute('BEGIN IMMEDIATE'); assert old != fingerprint(one); one.rollback(); assert state() == before; plans += 1
            for key in KEYS[:5]:
                one.execute("CREATE TRIGGER synthetic_hardware_key BEFORE UPDATE ON settings WHEN NEW.key='" + key + "' BEGIN SELECT RAISE(ABORT,'synthetic'); END;"); one.commit()
                old = fingerprint(one); before = state()
                try:
                    one.execute('BEGIN IMMEDIATE'); assert old == fingerprint(one); write_record(); one.commit()
                except sqlite3.IntegrityError: one.rollback()
                else: raise AssertionError('key fault missing')
                assert state() == before; one.execute('DROP TRIGGER synthetic_hardware_key'); one.commit(); plans += 1
            one.execute("CREATE TRIGGER synthetic_hardware_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;"); one.commit()
            old = fingerprint(one); before = state()
            try:
                one.execute('BEGIN IMMEDIATE'); assert old == fingerprint(one); write_record(); one.commit()
            except sqlite3.IntegrityError: one.rollback()
            else: raise AssertionError('audit fault missing')
            assert state() == before; assert old == fingerprint(one); one.execute('DROP TRIGGER synthetic_hardware_audit'); one.commit(); plans += 1
            one.execute('BEGIN IMMEDIATE'); assert old == fingerprint(one); write_record(); own = fingerprint(one); one.commit()
            assert fingerprint(one) == own; assert one.execute("SELECT COUNT(*) FROM audit_logs WHERE action='hardware_test'").fetchone()[0] == 1; plans += 1
            # A single printer-name handoff must not bless somebody else's untouched record changes.
            old_record = fingerprint(one, KEYS[:5]); put(two, 'hardware.note', 'somebody else'); two.commit()
            one.execute('BEGIN IMMEDIATE'); put(one, 'printer.name', 'my selection'); one.commit()
            assert old_record != fingerprint(one, KEYS[:5]); plans += 1
            one.execute('BEGIN IMMEDIATE')
            try: put(two, 'hardware.note', 'blocked writer'); two.commit()
            except sqlite3.OperationalError: two.rollback()
            else: raise AssertionError('reservation bypassed')
            one.rollback(); plans += 1
            one.execute('BEGIN DEFERRED'); frozen = fingerprint(one)
            try: put(two, 'hardware.note', 'concurrent snapshot writer'); two.commit()
            except sqlite3.OperationalError: assert mode == 'DELETE'; two.rollback()
            else: assert mode == 'WAL'
            assert fingerprint(one) == frozen; one.commit(); plans += 1
            for table in ('sales', 'purchases', 'payments', 'stock_movements'):
                assert one.execute('SELECT COUNT(*) FROM ' + table).fetchone()[0] == 0
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations;{plans} Python-owned Hardware compare/rollback/retry/handoff/reservation/snapshot plans WAL+DELETE;zero financial posts.')
    print('NOT C# services/TransactionOutcome/auth/cache/COMMIT faults/WPF/Windows printer/default-driver/LAN proof.')


if __name__ == '__main__': run()
