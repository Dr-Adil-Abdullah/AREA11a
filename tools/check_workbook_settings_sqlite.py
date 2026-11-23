#!/usr/bin/env python3
"""Python-owned Workbook Settings dependency/Tx reference.NOT workbook parser,C# services,
auth/cache/provider outcomes/UI/Excel proof.Dynamic UPDATE below is a Python reference,not C# execution."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'; source = (core / 'Services/WorkbookService.cs').read_text()
    assert 'UPDATE {record.Table} SET {string.Join(", ", assignments)} WHERE {record.KeyColumn} = @key;' in source
    assert '_settings.RequireWorkbookEdit(connection, preview.SettingsEdit, preview.SettingValues.Keys)' in source
    audit_sql = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    pairs = [('discount.maxPercentCashier', 'discount.maxPercentManager', 5, 20), ('supplier.returnCutoffDays', 'supplier.returnAlertDays', 90, 180)]
    with tempfile.TemporaryDirectory(prefix='area11-workbook-settings-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db')); one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.executescript("INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner'); CREATE TABLE synthetic_workbook_master(id INTEGER PRIMARY KEY,value TEXT); INSERT INTO synthetic_workbook_master VALUES(1,'old');")
            for left, right, first, last in pairs:
                for key, value in [(left, first), (right, last)]: one.execute('INSERT OR REPLACE INTO settings(key,value,updated_at) VALUES(?,?,?)', (key, json.dumps(value), '2026-11-10'))
            one.commit(); assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            one.execute('PRAGMA foreign_keys=ON'); two.execute('PRAGMA foreign_keys=ON')
            def token(db, keys): return hashlib.sha256(json.dumps([db.execute('SELECT key,value FROM settings WHERE key=?', (k,)).fetchall() for k in keys]).encode()).digest()
            def update(db, key, value): db.execute('UPDATE settings SET value=?,updated_at=? WHERE key=?', (json.dumps(value), '2026-11-10', key))
            def audit(): one.execute(audit_sql, dict(at='2026-11-10', userId=9001, userName='Synthetic owner', action='workbook_update', module='Settings', entity='settings', entityId='synthetic', oldValue='5', newValue='6', details='Python reference'))
            def state(): return tuple(one.iterdump())
            for left, right, first, last in pairs:
                for dependency in (left, right):
                    old = token(one, [left, right]); update(two, dependency, 101); two.commit(); before = state()
                    one.execute('BEGIN IMMEDIATE'); assert old != token(one, [left, right]); one.rollback(); assert state() == before; plans += 1
                update(one, left, first); update(one, right, last); one.commit()
            keys = list(pairs[0][:2]); old = token(one, keys)
            one.executescript("CREATE TRIGGER synthetic_workbook_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;")
            before = state()
            try:
                one.execute('BEGIN IMMEDIATE'); assert token(one, keys) == old
                one.execute("UPDATE synthetic_workbook_master SET value='new' WHERE id=1"); update(one, keys[0], 6); audit(); one.commit()
            except sqlite3.IntegrityError: one.rollback()
            else: raise AssertionError('audit fault absent')
            assert before == state(); assert token(one, keys) == old; plans += 1
            one.execute('DROP TRIGGER synthetic_workbook_audit'); one.commit()
            one.execute('BEGIN IMMEDIATE'); assert token(one, keys) == old
            update(one, keys[0], 6); audit(); one.execute("UPDATE synthetic_workbook_master SET value='new' WHERE id=1"); one.commit()
            assert one.execute("SELECT COUNT(*) FROM audit_logs WHERE action='workbook_update'").fetchone()[0] == 1; plans += 1
            # Actual SQLite deferred COMMIT refusal;Python models conservative blocked-plan handling.
            one.executescript("CREATE TABLE synthetic_wb_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER synthetic_wb_commit_audit AFTER INSERT ON audit_logs BEGIN INSERT INTO synthetic_wb_commit(parent_id) VALUES(-99999); END;")
            before = state(); attempted = False; blocked = True
            try:
                one.execute('BEGIN IMMEDIATE'); update(one, keys[0], 7); audit(); attempted = True; one.commit()
            except sqlite3.IntegrityError: one.rollback()
            else: raise AssertionError('COMMIT fault absent')
            assert attempted and blocked; assert state() == before; plans += 1
            one.execute('DROP TRIGGER synthetic_wb_commit_audit'); one.commit()
            one.execute('BEGIN IMMEDIATE')
            try: update(two, keys[0], 8); two.commit()
            except sqlite3.OperationalError: two.rollback()
            else: raise AssertionError('reservation bypassed')
            one.rollback(); plans += 1
            one.execute('BEGIN DEFERRED'); frozen = token(one, keys)
            try: update(two, keys[1], 25); two.commit()
            except sqlite3.OperationalError: assert mode == 'DELETE'; two.rollback()
            else: assert mode == 'WAL'
            assert token(one, keys) == frozen; one.commit(); plans += 1
            for table in ('sales', 'purchases', 'payments', 'stock_movements'):
                assert one.execute('SELECT COUNT(*) FROM ' + table).fetchone()[0] == 0
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations;{plans} Python-owned Workbook Settings dependency/mixed-control rollback/retry/deferred-COMMIT/reservation/snapshot plans WAL+DELETE;zero financial posts.')
    print('NOT C# Workbook/Settings/Database/auth/cache/Excel/WPF/provider-fault/plan-latch/durable identity proof.')


if __name__ == '__main__': run()
