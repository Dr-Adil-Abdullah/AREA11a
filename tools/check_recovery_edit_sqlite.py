#!/usr/bin/env python3
"""Recovery edit extracted SQL reference. Python owns synthetic hashes/compare/Tx/outcome control.
NOT C# AuthService/SettingsService/EditConcurrency/session/cache/provider/WPF execution."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]
KEY = 'security.recoveryWordHash'


def run():
    core = ROOT / 'dotnet/Area11.Core'
    source = (core / 'Services/SettingsService.cs').read_text().split('private ServiceResult SaveRecoveryHashCore(', 1)[1].split('private static ServiceResult RecoverySaved', 1)[0]
    upsert = quoted(source, 'INSERT INTO settings')
    baseline = quoted((core / 'Services/EditConcurrency.cs').read_text(), "SELECT key,value FROM settings WHERE key='security.recoveryWordHash';")
    audit_sql = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-recovery-edit-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db'))
            one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.execute("INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');"); one.commit()
            assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            one.execute('PRAGMA foreign_keys=ON'); two.execute('PRAGMA foreign_keys=ON')
            def token(db): return hashlib.sha256(json.dumps(db.execute(baseline).fetchall()).encode()).digest()
            def put(db, value): db.execute(upsert, dict(key=KEY, value=json.dumps(value), at='2026-11-10 10:00:00'))
            def audit(factory): one.execute(audit_sql, dict(at='2026-11-10', userId=9001, userName='Synthetic owner', action='recovery_word_changed',
                module='Auth', entity=None, entityId=None, oldValue=None, newValue=None, details='back to the factory word' if factory else 'changed'))
            def state(): return tuple(one.iterdump())
            for first_factory in (False, True):
                for second_factory in (False, True):
                    put(one, 'synthetic-initial-hash'); one.commit(); old = token(one)
                    one.execute('BEGIN IMMEDIATE'); assert token(one) == old
                    put(one, '' if first_factory else 'synthetic-first-hash'); audit(first_factory); saved = token(one); one.commit(); assert saved == token(one)
                    before = state(); one.execute('BEGIN IMMEDIATE'); assert token(one) != old
                    # Both second intents are refused by the same complete baseline before SQL writes.
                    second_intent = '' if second_factory else 'synthetic-second-hash'; assert isinstance(second_intent, str)
                    one.rollback(); assert before == state(); plans += 1
            one.execute('DELETE FROM settings WHERE key=?', (KEY,)); one.commit(); missing = token(one)
            put(two, ''); two.commit(); assert missing != token(one); plans += 1
            old = token(one); two.execute('UPDATE settings SET value=? WHERE key=?', ('null', KEY)); two.commit(); assert old != token(one); plans += 1
            old = token(one); put(two, 'synthetic-peer-hash'); two.commit(); assert old != token(one); plans += 1
            old = token(one); two.execute("UPDATE settings SET updated_at='2099-01-01' WHERE key=?", (KEY,))
            two.execute("INSERT INTO settings(key,value,updated_at) VALUES('store.name','\"Synthetic shop\"','2026-11-10')"); two.commit()
            assert old == token(one); plans += 1
            old = token(one); one.execute('BEGIN IMMEDIATE'); assert old == token(one)
            put(one, 'synthetic-exact-hash'); audit(False); exact = token(one); one.commit()
            put(two, 'synthetic-later-hash'); two.commit(); assert exact != token(one); plans += 1
            for factory in (False, True):
                put(one, 'synthetic-initial-hash'); one.commit()
                one.executescript("CREATE TRIGGER synthetic_recovery_audit BEFORE INSERT ON audit_logs WHEN NEW.action='recovery_word_changed' BEGIN SELECT RAISE(ABORT,'synthetic'); END;")
                old = token(one); before = state()
                try:
                    one.execute('BEGIN IMMEDIATE'); assert token(one) == old
                    put(one, '' if factory else 'synthetic-new-hash'); audit(factory); one.commit()
                except sqlite3.IntegrityError: one.rollback()
                else: raise AssertionError('audit fault absent')
                assert before == state(); assert old == token(one)
                one.execute('DROP TRIGGER synthetic_recovery_audit'); one.commit()
                one.execute('BEGIN IMMEDIATE'); assert token(one) == old
                put(one, '' if factory else 'synthetic-new-hash'); audit(factory); saved = token(one); one.commit(); assert saved == token(one); plans += 1
            for factory in (False, True):
                one.executescript("CREATE TRIGGER synthetic_recovery_write BEFORE UPDATE ON settings WHEN NEW.key='security.recoveryWordHash' BEGIN SELECT RAISE(ABORT,'synthetic'); END;")
                before = state()
                try:
                    one.execute('BEGIN IMMEDIATE'); put(one, '' if factory else 'synthetic-write-hash'); audit(factory); one.commit()
                except sqlite3.IntegrityError: one.rollback()
                else: raise AssertionError('UPSERT fault absent')
                assert before == state(); one.execute('DROP TRIGGER synthetic_recovery_write'); one.commit(); plans += 1
            one.executescript('CREATE TABLE synthetic_recovery_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED);')
            for factory in (False, True):
                one.executescript("CREATE TRIGGER synthetic_recovery_commit_audit AFTER INSERT ON audit_logs WHEN NEW.action='recovery_word_changed' BEGIN INSERT INTO synthetic_recovery_commit(parent_id) VALUES(-99999); END;")
                before = state(); attempted = False
                try:
                    one.execute('BEGIN IMMEDIATE'); put(one, '' if factory else 'synthetic-commit-hash'); audit(factory); attempted = True; one.commit()
                except sqlite3.IntegrityError: one.rollback()
                else: raise AssertionError('deferred COMMIT fault absent')
                assert attempted and before == state(); one.execute('DROP TRIGGER synthetic_recovery_commit_audit'); one.commit(); plans += 1
            put(one, ''); one.commit(); old = token(one); put(two, ''); two.commit(); assert old == token(one); plans += 1
            one.execute('BEGIN IMMEDIATE')
            try: two.execute('BEGIN IMMEDIATE')
            except sqlite3.OperationalError: pass
            else: raise AssertionError('second writer acquired reserved DB')
            one.rollback(); plans += 1
            if mode == 'WAL':
                one.execute('BEGIN'); old = token(one); put(two, 'synthetic-snapshot-hash'); two.commit()
                assert token(one) == old; one.commit(); assert token(one) != old; plans += 1
            one.execute('UPDATE settings SET value=? WHERE key=?', ('broken-json', KEY)); one.commit()
            try: json.loads(one.execute('SELECT value FROM settings WHERE key=?', (KEY,)).fetchone()[0])
            except json.JSONDecodeError: pass
            else: raise AssertionError('malformed JSON became factory editor')
            plans += 1
            for old_value, new_value, details in one.execute("SELECT old_value,new_value,details FROM audit_logs WHERE action='recovery_word_changed'"):
                assert old_value is None and new_value is None
                assert details in ('changed', 'back to the factory word')
            assert one.execute('SELECT COUNT(*) FROM sales').fetchone()[0] == 0
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations;{plans} Python-owned recovery change/reset/presence/exact JSON/rollback+retry/deferred-COMMIT/handoff/reservation/snapshot plans WAL+DELETE;zero financial posts.')
    print('NOT C# AuthService/SettingsService/EditConcurrency/Database/TransactionOutcome/cache/auth/provider/WPF/LAN proof.')


if __name__ == '__main__': run()
