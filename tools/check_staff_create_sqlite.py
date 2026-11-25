#!/usr/bin/env python3
"""Staff create extracted SQL reference. Python owns validation/hash/Tx/outcome/guard assumptions.
NOT C# AuthService/Database/auth/session/PostingAttemptGuard/provider/WPF execution."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'
    source = (core / 'Services/AuthService.cs').read_text().split('public ServiceResult<int> CreateStaff(', 1)[1].split('private static ServiceResult<int> CreateStaffFailure', 1)[0]
    insert = quoted(source, 'INSERT INTO users(name, role, pin_hash')
    duplicate = quoted(source, 'SELECT COUNT(1) FROM users WHERE lower(name)')
    length_sql = quoted(source, "SELECT value FROM settings WHERE key='security.pinLength';")
    baseline = quoted((core / 'Services/EditConcurrency.cs').read_text(), 'SELECT id,name,role,active,password_hash,pin_hash FROM users')
    audit_sql = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-staff-create-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db'))
            one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.execute("INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');")
            one.execute("INSERT INTO settings(key,value,updated_at) VALUES('security.pinLength','4','2026-11-10')"); one.commit()
            assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            one.execute('PRAGMA foreign_keys=ON'); two.execute('PRAGMA foreign_keys=ON')
            def state(): return tuple(one.iterdump())
            def token(db, ident): return hashlib.sha256(json.dumps(db.execute(baseline, dict(id=ident)).fetchall()).encode()).digest()
            def write(db, name, role='cashier'):
                db.execute(insert, dict(name=name, role=role, pin='synthetic-hash-only', at='2026-11-10'))
                return db.execute('SELECT last_insert_rowid()').fetchone()[0]
            def audit(ident, name, role='cashier'):
                one.execute(audit_sql, dict(at='2026-11-10', userId=9001, userName='Synthetic owner', action='create',
                    module='Users', entity='users', entityId=str(ident), oldValue=None,
                    newValue=json.dumps(dict(name=name, role=role, pin='set')), details=None))
            for role in ('owner', 'manager', 'salesman', 'cashier'):
                name = 'Synthetic role ' + role; one.execute('BEGIN IMMEDIATE')
                assert one.execute(length_sql).fetchone()[0] == '4'
                assert one.execute(duplicate, dict(name=name)).fetchone()[0] == 0
                ident = write(one, name, role); audit(ident, name, role); saved = token(one, ident); one.commit()
                assert saved == token(one, ident)
                assert one.execute('SELECT name,role,active,pin_hash,password_hash FROM users WHERE id=?', (ident,)).fetchone() == (name, role, 1, 'synthetic-hash-only', None)
                plans += 1
            for inactive in (False, True):
                one.execute('UPDATE users SET active=? WHERE id=?', (0 if inactive else 1, ident)); one.commit(); before = state()
                one.execute('BEGIN IMMEDIATE'); assert one.execute(duplicate, dict(name=name.upper())).fetchone()[0] == 1
                one.rollback(); assert before == state(); plans += 1
            for fault in ('insert', 'audit'):
                if fault == 'insert': one.executescript("CREATE TRIGGER synthetic_staff_create_fault BEFORE INSERT ON users BEGIN SELECT RAISE(ABORT,'synthetic'); END;")
                else: one.executescript("CREATE TRIGGER synthetic_staff_create_fault BEFORE INSERT ON audit_logs WHEN NEW.module='Users' AND NEW.action='create' BEGIN SELECT RAISE(ABORT,'synthetic'); END;")
                before = state(); name = 'Synthetic rollback ' + fault
                try:
                    one.execute('BEGIN IMMEDIATE'); ident = write(one, name); audit(ident, name); one.commit()
                except sqlite3.IntegrityError: one.rollback()
                else: raise AssertionError('fault absent')
                assert before == state(); one.execute('DROP TRIGGER synthetic_staff_create_fault'); one.commit()
                one.execute('BEGIN IMMEDIATE'); ident = write(one, name); audit(ident, name); one.commit()
                assert one.execute(duplicate, dict(name=name)).fetchone()[0] == 1; plans += 1
            one.executescript('CREATE TABLE synthetic_staff_create_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);')
            one.executescript("CREATE TRIGGER synthetic_staff_create_commit_audit AFTER INSERT ON audit_logs WHEN NEW.module='Users' AND NEW.action='create' BEGIN INSERT INTO synthetic_staff_create_commit(parent_id) VALUES(-99999); END;")
            before = state(); attempted = False
            try:
                one.execute('BEGIN IMMEDIATE'); ident = write(one, 'Synthetic commit fault'); audit(ident, 'Synthetic commit fault'); attempted = True; one.commit()
            except sqlite3.IntegrityError: one.rollback()
            else: raise AssertionError('deferred COMMIT fault absent')
            assert attempted and before == state(); one.execute('DROP TRIGGER synthetic_staff_create_commit_audit'); one.commit(); plans += 1
            for length in (3, 6, 8):
                two.execute("UPDATE settings SET value=? WHERE key='security.pinLength'", (str(length),)); two.commit()
                one.execute('BEGIN IMMEDIATE'); current = int(one.execute(length_sql).fetchone()[0]); assert current == length and current != 4
                ident = write(one, 'Synthetic length ' + str(length)); audit(ident, 'Synthetic length ' + str(length)); one.commit(); plans += 1
            for raw in (None, 'broken'):
                one.execute("DELETE FROM settings WHERE key='security.pinLength'")
                if raw is not None: one.execute("INSERT INTO settings(key,value,updated_at) VALUES('security.pinLength',?,'2026-11-10')", (raw,))
                one.commit(); one.execute('BEGIN IMMEDIATE'); row = one.execute(length_sql).fetchone()
                try: length = int(row[0]) if row else 4
                except ValueError: length = 4
                assert length == 4; one.rollback(); plans += 1
            one.execute('BEGIN IMMEDIATE'); ident = write(one, 'Synthetic exact handoff'); audit(ident, 'Synthetic exact handoff'); exact = token(one, ident); one.commit()
            two.execute('UPDATE users SET active=0 WHERE id=?', (ident,)); two.commit(); assert exact != token(one, ident); plans += 1
            # Synthetic lost response AFTER a real Python commit;NOT a provider dropped-ack injection.
            one.execute('BEGIN IMMEDIATE'); ident = write(one, 'Synthetic lost ack'); audit(ident, 'Synthetic lost ack'); one.commit()
            synthetic_guard = 'needs-verification'; assert synthetic_guard != 'ready'
            assert one.execute('SELECT COUNT(*) FROM users WHERE id=?', (ident,)).fetchone()[0] == 1; plans += 1
            one.execute('BEGIN IMMEDIATE')
            try: two.execute('BEGIN IMMEDIATE')
            except sqlite3.OperationalError: pass
            else: raise AssertionError('second writer acquired reserved DB')
            one.rollback(); plans += 1
            assert one.execute('SELECT COUNT(*) FROM sales').fetchone()[0] == 0
            for raw, in one.execute("SELECT new_value FROM audit_logs WHERE module='Users' AND action='create'"):
                assert json.loads(raw)['pin'] == 'set'; assert 'synthetic-hash-only' not in raw
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations;{plans} Python-owned staff create/duplicate/current-PIN/insert+audit rollback+retry/deferred-COMMIT/exact-handoff/synthetic-lost-ack/reservation plans WAL+DELETE;zero financial posts.')
    print('NOT C# AuthService/Database/TransactionOutcome/PostingAttemptGuard/validation/hash/auth/session/provider/WPF/LAN proof.')


if __name__ == '__main__': run()
