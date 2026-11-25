#!/usr/bin/env python3
"""Permission edit SQL reference.Python owns fingerprints/compare/Tx/control/default assumptions.
NOT C# PermissionService/EditConcurrency/auth/provider outcome/UI proof."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'; service = (core / 'Services/PermissionService.cs').read_text()
    upsert = quoted(service, 'INSERT INTO user_permissions'); delete = quoted(service, 'DELETE FROM user_permissions')
    capture = (core / 'Services/EditConcurrency.cs').read_text().split('if (scope == "permissions")', 1)[1].split('if (scope == Product)', 1)[0]
    target_sql = quoted(capture, 'SELECT id,role,active FROM users'); rows_sql = quoted(capture, 'SELECT module_key,allowed FROM user_permissions')
    audit_sql = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-permission-edit-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db')); one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.executescript("INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner'),(9002,'Synthetic target','cashier');")
            one.commit(); assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            one.execute('PRAGMA foreign_keys=ON'); two.execute('PRAGMA foreign_keys=ON')
            def token(db): return hashlib.sha256(json.dumps([db.execute(target_sql, dict(id=9002)).fetchall(), db.execute(rows_sql, dict(id=9002)).fetchall()]).encode()).digest()
            def grant(db, module='reports', allowed=1): db.execute(upsert, dict(userId=9002, module=module, allowed=allowed, at='2026-11-10', actor='Synthetic owner'))
            def reset(db): db.execute(delete, dict(userId=9002))
            def audit(action): one.execute(audit_sql, dict(at='2026-11-10', userId=9001, userName='Synthetic owner', action=action,
                module='Users', entity='user_permissions', entityId='9002', oldValue='synthetic', newValue='synthetic', details=None))
            def state(): return tuple(one.iterdump())
            old = token(one); one.execute('BEGIN IMMEDIATE'); assert token(one) == old
            grant(one); audit('permission_change'); own = token(one); one.commit(); assert own == token(one); plans += 1
            for action in ('set', 'reset'):
                before = state(); one.execute('BEGIN IMMEDIATE'); assert token(one) != old; one.rollback(); assert before == state(); plans += 1
            reset(one); one.commit(); missing = token(one); grant(two, 'reports', 0); two.commit()
            assert missing != token(one); plans += 1
            old = token(one); grant(two, 'products', 1); two.commit(); assert old != token(one); plans += 1
            old = token(one); grant(two, 'unknown-future-module', 1); two.commit(); assert old != token(one); plans += 1
            for sql in ("UPDATE users SET role='manager' WHERE id=9002", 'UPDATE users SET active=0 WHERE id=9002'):
                old = token(one); two.execute(sql); two.commit(); assert old != token(one); plans += 1
            two.execute("UPDATE users SET role='cashier',active=1 WHERE id=9002"); two.commit()
            for action in ('set', 'reset'):
                one.executescript("CREATE TRIGGER synthetic_permission_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;")
                old = token(one); before = state()
                try:
                    one.execute('BEGIN IMMEDIATE'); assert token(one) == old
                    if action == 'set': grant(one, 'cash', 1)
                    else: reset(one)
                    audit('permission_' + action); one.commit()
                except sqlite3.IntegrityError: one.rollback()
                else: raise AssertionError('audit fault absent')
                assert before == state(); assert old == token(one)
                one.execute('DROP TRIGGER synthetic_permission_audit'); one.commit(); plans += 1
            one.executescript('CREATE TABLE synthetic_permission_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);')
            for action in ('set', 'reset'):
                one.executescript("CREATE TRIGGER synthetic_permission_commit_audit AFTER INSERT ON audit_logs BEGIN INSERT INTO synthetic_permission_commit(parent_id) VALUES(-99999); END;")
                before = state(); attempted = False
                try:
                    one.execute('BEGIN IMMEDIATE')
                    if action == 'set': grant(one, 'cash', 1)
                    else: reset(one)
                    audit('permission_' + action); attempted = True; one.commit()
                except sqlite3.IntegrityError: one.rollback()
                else: raise AssertionError('deferred COMMIT fault absent')
                assert attempted; assert before == state()  # Python models conservative no-safe-retry,not C# proof.
                one.execute('DROP TRIGGER synthetic_permission_commit_audit'); one.commit(); plans += 1
            old = token(one); one.execute('BEGIN IMMEDIATE'); assert token(one) == old
            reset(one); audit('permission_reset'); own = token(one); one.commit()
            assert one.execute(rows_sql, dict(id=9002)).fetchall() == []; assert own == token(one); plans += 1
            one.execute('BEGIN IMMEDIATE')
            try: grant(two, 'cash', 1); two.commit()
            except sqlite3.OperationalError: two.rollback()
            else: raise AssertionError('writer reservation bypassed')
            one.rollback(); plans += 1
            one.execute('BEGIN DEFERRED'); frozen = token(one)
            try: grant(two, 'cash', 1); two.commit()
            except sqlite3.OperationalError: assert mode == 'DELETE'; two.rollback()
            else: assert mode == 'WAL'
            assert token(one) == frozen; one.commit(); plans += 1
            for table in ('sales', 'purchases', 'payments', 'stock_movements'):
                assert one.execute('SELECT COUNT(*) FROM ' + table).fetchone()[0] == 0
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations;{plans} Python-owned permission compare/presence/role/active/Set+Reset rollback/deferred-COMMIT/handoff/reservation/snapshot plans WAL+DELETE;zero financial posts.')
    print('NOT C# PermissionService/EditConcurrency/Database/defaults/auth/TransactionOutcome/UI/provider-fault/LAN proof.')


if __name__ == '__main__': run()
