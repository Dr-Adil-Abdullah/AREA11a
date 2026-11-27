#!/usr/bin/env python3
"""Extracted telemetry SQL on synthetic files. Python owns all control flow/transactions.
NOT C# guard/cache/outcome, Microsoft.Data.Sqlite COMMIT, WPF, or delivery proof.
"""
from pathlib import Path
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'
    service = (core / 'Services/FocusGuardService.cs').read_text()
    upsert = quoted(service, 'INSERT INTO settings')
    read = quoted(service, 'SELECT value FROM settings WHERE key=@key;')
    audit = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-telemetry-') as folder:
        for journal in ('WAL', 'DELETE'):
            db = sqlite3.connect(str(Path(folder) / (journal + '.db')))
            for migration in migrations: db.executescript(migration.read_text())
            db.execute('PRAGMA foreign_keys=ON')
            assert db.execute('PRAGMA journal_mode=' + journal).fetchone()[0].upper() == journal
            at = '2026-11-10 10:00:00'
            pending = {}
            def put(key, value):
                raw = json.dumps(value); changed = db.execute(upsert, dict(key=key, value=raw, at=at)).rowcount
                assert changed == 1, 'stamp ignored'
                pending[key] = raw
            def get(key): return json.loads(db.execute(read, dict(key=key)).fetchone()[0])
            def row(action):
                changed = db.execute(audit, dict(at=at, userId=None, userName='system', action=action,
                    module='POS', entity=None, entityId=None, oldValue=None, newValue=None, details='Synthetic shared marker; not kill/PC proof.')).rowcount
                assert changed == 1, 'audit ignored'
            def state(): return (tuple(db.execute('SELECT * FROM settings ORDER BY key')), tuple(db.execute('SELECT * FROM audit_logs ORDER BY id')))
            def tx(work):
                pending.clear(); db.execute('BEGIN IMMEDIATE')
                try:
                    work()
                    for key, raw in pending.items(): assert db.execute(read, dict(key=key)).fetchone()[0] == raw, 'stamp rewritten'
                    db.commit()
                except BaseException: db.rollback(); raise
            def startup():
                start, seen, user, clean = [get(k) for k in ('session.lastStart', 'session.lastSeen', 'session.lastUser', 'session.cleanExit')]
                assert all(isinstance(v, str) for v in (start, seen, user)) and type(clean) is bool
                if start.strip() and not clean: row('session_killed')
                for k, v in [('session.lastStart', at), ('session.lastSeen', at), ('session.lastUser', ''), ('session.cleanExit', False)]: put(k, v)
            def seed():
                for k, v in [('session.lastStart', 'previous'), ('session.lastSeen', 'seen'), ('session.lastUser', 'Ali'), ('session.cleanExit', False)]: put(k, v)
                db.commit()
            def fault(sql, work):
                nonlocal plans
                db.execute('CREATE TRIGGER telemetry_fault ' + sql + " BEGIN SELECT RAISE(ABORT,'synthetic fault'); END;"); db.commit()
                before = state()
                try: tx(work)
                except sqlite3.IntegrityError: pass
                else: raise AssertionError('Expected synthetic fault')
                assert state() == before; plans += 1
                db.execute('DROP TRIGGER telemetry_fault'); db.commit()
            seed(); tx(startup); assert get('session.lastUser') == '' and get('session.cleanExit') is False
            assert get('session.lastStart') == get('session.lastSeen'); plans += 1
            for key in ('session.lastStart', 'session.lastSeen', 'session.lastUser', 'session.cleanExit'):
                seed(); fault("BEFORE UPDATE ON settings WHEN NEW.key='" + key + "'", startup)
            seed(); fault("BEFORE INSERT ON audit_logs WHEN NEW.action='session_killed'", startup)
            def heartbeat(): put('session.lastSeen', at); put('session.lastUser', 'New operator')
            tx(heartbeat); assert get('session.lastUser') == 'New operator'; plans += 1
            seed(); fault("BEFORE UPDATE ON settings WHEN NEW.key='session.lastUser'", heartbeat)
            def clean(): put('session.lastSeen', at); put('session.cleanExit', True)
            tx(clean); assert get('session.cleanExit') is True; plans += 1
            seed(); fault("BEFORE UPDATE ON settings WHEN NEW.key='session.cleanExit'", clean)
            before = tuple(db.execute('SELECT * FROM settings ORDER BY key')); tx(lambda: row('screen_left'))
            assert before == tuple(db.execute('SELECT * FROM settings ORDER BY key')); plans += 1
            fault("BEFORE INSERT ON audit_logs WHEN NEW.action='screen_left'", lambda: row('screen_left'))
            for trigger, work in [
                ("BEFORE INSERT ON audit_logs WHEN NEW.action='session_killed' BEGIN SELECT RAISE(IGNORE); END;", startup),
                ("BEFORE INSERT ON audit_logs WHEN NEW.action='screen_left' BEGIN SELECT RAISE(IGNORE); END;", lambda: row('screen_left')),
                ("BEFORE UPDATE ON settings WHEN NEW.key='session.lastSeen' BEGIN SELECT RAISE(IGNORE); END;", startup),
                ("AFTER UPDATE ON settings WHEN NEW.key='session.cleanExit' BEGIN UPDATE settings SET value='null' WHERE key='session.lastUser'; END;", startup)]:
                seed(); db.execute('CREATE TRIGGER silent_fault ' + trigger); db.commit(); before = state()
                try: tx(work)
                except AssertionError: pass
                else: raise AssertionError('Ignored/rewritten SQL must not be success')
                assert state() == before; plans += 1; db.execute('DROP TRIGGER silent_fault'); db.commit()
            # A COMMIT error may be known rolled back here; C# deliberately labels any attempted
            # COMMIT failure unknown. This Python test cannot establish that provider classification.
            db.executescript('''CREATE TABLE telemetry_probe_parent(id INTEGER PRIMARY KEY);
CREATE TABLE telemetry_probe_child(id INTEGER REFERENCES telemetry_probe_parent(id) DEFERRABLE INITIALLY DEFERRED);
CREATE TRIGGER telemetry_commit_fault AFTER UPDATE ON settings WHEN NEW.key='session.lastSeen'
BEGIN INSERT INTO telemetry_probe_child(id) VALUES(1); END;''')
            before = state()
            try: tx(startup)
            except sqlite3.IntegrityError: pass
            else: raise AssertionError('Expected deferred COMMIT fault')
            assert state() == before; plans += 1
            for table in ('sales', 'purchases', 'payments', 'stock_movements'):
                assert db.execute(f'SELECT COUNT(*) FROM {table}').fetchone()[0] == 0
            db.close()
    print(f'PASS {len(migrations)} migrations; {plans} synthetic telemetry plans across WAL/DELETE. NOT C#/provider/WPF proof.')


if __name__ == '__main__': run()
