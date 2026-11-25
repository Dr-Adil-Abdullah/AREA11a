#!/usr/bin/env python3
"""Extracted staff SQL reference only. Python owns hash/compare/Tx/outcome control.
NOT AuthService/EditConcurrency/Database/session/provider/WPF execution or proof."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'
    auth = (core / 'Services/AuthService.cs').read_text()
    toggle = quoted(auth, 'UPDATE users SET active = @active')
    reset = quoted(auth, 'UPDATE users SET {column} = @hash').replace('{column}', 'pin_hash').replace('{otherColumn}', 'password_hash')
    baseline = quoted((core / 'Services/EditConcurrency.cs').read_text(), 'SELECT id,name,role,active,password_hash,pin_hash')
    audit_sql = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-staff-edit-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db'))
            one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.executescript("INSERT INTO users(id,name,role,pin_hash) VALUES(9001,'Synthetic owner','owner',NULL),(9002,'Synthetic staff','cashier','synthetic-old');")
            one.commit(); assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            one.execute('PRAGMA foreign_keys=ON'); two.execute('PRAGMA foreign_keys=ON')
            def token(db): return hashlib.sha256(json.dumps(db.execute(baseline, dict(id=9002)).fetchall()).encode()).digest()
            def write(db, secret): db.execute(reset if secret else toggle, dict(id=9002, hash='synthetic-new', active=0))
            def audit(): one.execute(audit_sql, dict(at='2026-11-10', userId=9001, userName='Synthetic owner', action='synthetic_staff_change',
                module='Users', entity='users', entityId='9002', oldValue='synthetic', newValue='synthetic', details=None))
            def state(): return tuple(one.iterdump())
            for first in (False, True):
                one.execute("UPDATE users SET active=1,pin_hash='synthetic-old',password_hash=NULL WHERE id=9002"); one.commit()
                old = token(one); one.execute('BEGIN IMMEDIATE'); assert token(one) == old
                write(one, first); audit(); saved = token(one); one.commit(); assert saved == token(one); plans += 1
                before = state()
                for second in (False, True):
                    one.execute('BEGIN IMMEDIATE'); assert token(one) != old; one.rollback(); assert before == state(); plans += 1
            for assignment in ("name='Synthetic renamed'", "role='manager'", 'active=0', "password_hash='synthetic-other-password'", "pin_hash='synthetic-other-pin'"):
                old = token(one); two.execute('UPDATE users SET ' + assignment + ' WHERE id=9002'); two.commit()
                assert old != token(one); plans += 1
            for secret in (False, True):
                one.execute("UPDATE users SET active=1,pin_hash='synthetic-old' WHERE id=9002"); one.commit()
                one.executescript("CREATE TRIGGER synthetic_staff_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;")
                old = token(one); before = state()
                try:
                    one.execute('BEGIN IMMEDIATE'); assert token(one) == old
                    write(one, secret); audit(); one.commit()
                except sqlite3.IntegrityError: one.rollback()
                else: raise AssertionError('audit fault absent')
                assert before == state(); assert old == token(one)
                one.execute('DROP TRIGGER synthetic_staff_audit'); one.commit()
                one.execute('BEGIN IMMEDIATE'); assert token(one) == old
                write(one, secret); audit(); saved = token(one); one.commit(); assert token(one) == saved; plans += 1
            one.executescript('CREATE TABLE synthetic_staff_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);')
            for secret in (False, True):
                one.executescript("CREATE TRIGGER synthetic_staff_commit_audit AFTER INSERT ON audit_logs BEGIN INSERT INTO synthetic_staff_commit(parent_id) VALUES(-99999); END;")
                before = state(); attempted = False
                try:
                    one.execute('BEGIN IMMEDIATE'); write(one, secret); audit(); attempted = True; one.commit()
                except sqlite3.IntegrityError: one.rollback()
                else: raise AssertionError('deferred COMMIT fault absent')
                assert attempted and before == state(); one.execute('DROP TRIGGER synthetic_staff_commit_audit'); one.commit(); plans += 1
            one.execute('BEGIN IMMEDIATE')
            try:
                two.execute('BEGIN IMMEDIATE')
            except sqlite3.OperationalError: pass
            else: raise AssertionError('second writer acquired reserved DB')
            one.rollback(); plans += 1
            if mode == 'WAL':
                one.execute('BEGIN'); old = token(one)
                two.execute("UPDATE users SET name='Synthetic snapshot peer' WHERE id=9002"); two.commit()
                assert token(one) == old; one.commit(); assert token(one) != old; plans += 1
            assert one.execute('SELECT COUNT(*) FROM sales').fetchone()[0] == 0
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations;{plans} Python-owned staff dependency/CAS/toggle+reset audit rollback+retry/deferred-COMMIT/exact-handoff/reservation/snapshot plans WAL+DELETE;zero financial posts.')
    print('NOT C# AuthService/EditConcurrency/Database/TransactionOutcome/session/provider/WPF/LAN proof.')


if __name__ == '__main__': run()
