#!/usr/bin/env python3
"""Part5.1 extracted Settings/audit SQL on synthetic WAL/DELETE databases. Python owns
fingerprints,compare/Tx/control flow;NOT C# SettingsService/auth/cache/registry/WPF proof."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'
    source = (core / 'Services/SettingsService.cs').read_text()
    write = source.split('public ServiceResult SetMany(', 1)[1].split('internal bool HardwareBaselineRequired', 1)[0]
    upsert = quoted(write, 'INSERT INTO settings')
    fingerprint_sql = quoted(source, 'SELECT key,value FROM settings WHERE key=@entity;')
    audit = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql'))
    plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-settings-edit-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db'))
            one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.execute("INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');"); one.commit()
            assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode

            def put(db, key, value): db.execute(upsert, dict(key=key, value=json.dumps(value), at='2026-11-10 10:00:00'))
            def token(db, key):
                return hashlib.sha256(json.dumps(db.execute(fingerprint_sql, dict(entity=key)).fetchall()).encode()).hexdigest()
            def load(*keys): return {k: token(one, k) for k in keys}
            def checked(values, expected):
                one.execute('BEGIN IMMEDIATE')
                keys = list(values) + (['sync.key'] if any(k.startswith('sync.') for k in values) else [])
                if any(expected.get(k) != token(one, k) for k in keys): one.rollback(); return None
                for key, value in values.items():
                    old = one.execute('SELECT value FROM settings WHERE key=?', (key,)).fetchone()
                    put(one, key, value)
                    one.execute(audit, dict(at='2026-11-10 10:00:00', userId=9001, userName='Synthetic owner',
                        module='Settings', action='settings_change', entity='settings', entityId=key,
                        oldValue='redacted' if key == 'sync.key' else old[0] if old else None,
                        newValue='redacted' if key == 'sync.key' else json.dumps(value), details=None))
                updated = dict(expected)
                for key in values: updated[key] = token(one, key)
                one.commit(); return updated

            put(one, 'store.name', 'Synthetic old'); put(one, 'store.phone', 'Synthetic phone'); one.commit()
            old = load('store.phone', 'store.name'); put(two, 'store.name', 'Synthetic other'); two.commit()
            before = tuple(one.iterdump()); assert checked({'store.phone': 'stale first', 'store.name': 'stale'}, old) is None
            assert tuple(one.iterdump()) == before; plans += 1
            old = load('store.name', 'store.phone'); put(two, 'store.phone', 'Concurrent phone'); two.commit()
            own = checked({'store.name': 'Reviewed name'}, old); assert own is not None
            assert checked({'store.phone': 'stale untouched'}, own) is None; plans += 1
            own = checked({'store.name': 'Next own name'}, own); assert own is not None; plans += 1
            put(two, 'store.name', 'After our commit'); two.commit(); assert checked({'store.name': 'stale handoff'}, own) is None; plans += 1
            old = load('printer.copies'); put(two, 'printer.copies', 1); two.commit()
            assert checked({'printer.copies': 2}, old) is None; plans += 1
            old = load('store.name'); two.execute("UPDATE settings SET updated_at='2099-01-01' WHERE key='store.name'"); two.commit()
            assert checked({'store.name': 'Timestamp only allowed'}, old) is not None; plans += 1
            old = load('store.name'); put(two, 'session.lastSeen', 'Synthetic heartbeat'); put(two, 'bill.nextSaleNo', 5); two.commit()
            assert checked({'store.name': 'Counter movement allowed'}, old) is not None; plans += 1
            old = load('sync.enabled', 'sync.key'); put(two, 'sync.key', 'Synthetic rotated secret'); two.commit()
            assert checked({'sync.enabled': False}, old) is None; plans += 1
            old = load('store.name', 'store.phone'); before = tuple(one.iterdump())
            one.execute("CREATE TRIGGER synthetic_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;"); one.commit()
            before_fault = tuple(one.iterdump())
            try: checked({'store.name': 'must rollback', 'store.phone': 'must rollback'}, old)
            except sqlite3.IntegrityError: one.rollback()
            else: raise AssertionError('audit fault did not fire')
            assert tuple(one.iterdump()) == before_fault; plans += 1
            one.execute('DROP TRIGGER synthetic_audit;'); one.commit(); assert tuple(one.iterdump()) == before
            assert checked({'store.name': 'One reviewed retry'}, old) is not None; plans += 1
            one.execute('BEGIN IMMEDIATE')
            try: put(two, 'store.name', 'Must wait')
            except sqlite3.OperationalError: two.rollback()
            else: raise AssertionError('reservation bypassed')
            one.rollback(); plans += 1
            one.execute('BEGIN DEFERRED'); frozen = token(one, 'store.name')
            try: put(two, 'store.name', 'Between reads'); two.commit()
            except sqlite3.OperationalError: assert mode == 'DELETE'; two.rollback()
            else: assert mode == 'WAL'
            assert token(one, 'store.name') == frozen; one.commit(); plans += 1
            for table in ('sales', 'purchases', 'payments', 'stock_movements'):
                assert one.execute(f'SELECT COUNT(*) FROM {table};').fetchone()[0] == 0
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations;{plans} Python-owned Settings compare/handoff/audit-rollback/retry/reservation/snapshot plans in WAL+DELETE;zero financial posts.')
    print('NOT C# SettingsService/Database/auth/cache/registry,provider COMMIT-fault,LAN or WPF proof.')


if __name__ == '__main__': run()
