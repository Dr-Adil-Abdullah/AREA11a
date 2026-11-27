#!/usr/bin/env python3
"""Extracted recovery upsert/audit SQL on synthetic files; Python owns Tx/control flow.
NOT C# outcome/guard/cache/auth/registry/provider COMMIT-fault/Windows proof.
"""
from pathlib import Path
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'
    method = (core / 'Services/SettingsService.cs').read_text().split('internal ServiceResult ResetStartupForRecovery()', 1)[1].split('internal bool MatchesStartupRecovery', 1)[0]
    upsert = quoted(method, 'INSERT INTO settings')
    audit = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-startup-recovery-') as folder:
        for journal in ('WAL', 'DELETE'):
            db = sqlite3.connect(str(Path(folder) / (journal + '.db')))
            for migration in migrations: db.executescript(migration.read_text())
            assert db.execute('PRAGMA journal_mode=' + journal).fetchone()[0].upper() == journal
            at = '2026-11-10 10:00:00'
            def put(key, value): db.execute(upsert, dict(key=key, value=json.dumps(value), at=at))
            def state(): return (tuple(db.execute('SELECT * FROM settings ORDER BY key')), tuple(db.execute('SELECT * FROM audit_logs ORDER BY id')))
            def recover():
                db.execute('BEGIN IMMEDIATE')
                try:
                    old = db.execute("SELECT value FROM settings WHERE key='startup.mode'").fetchone()
                    put('startup.mode', 'manual')
                    db.execute(audit, dict(at=at, userId=None, userName='system', module='Auth', action='protection_reset',
                        entity='settings', entityId='startup.mode', oldValue=old[0] if old else None,
                        newValue=json.dumps('manual'), details='Synthetic recovery; Windows cleanup separate.'))
                    db.commit()
                except BaseException: db.rollback(); raise
            put('startup.mode', 'protected'); put('lockdown.focusGuard', True); db.commit()
            recover()
            assert db.execute("SELECT value FROM settings WHERE key='startup.mode'").fetchone()[0] == '"manual"'
            assert db.execute("SELECT old_value,new_value,user_name FROM audit_logs WHERE action='protection_reset'").fetchone() == ('"protected"', '"manual"', 'system')
            assert db.execute("SELECT value FROM settings WHERE key='lockdown.focusGuard'").fetchone()[0] == 'true'; plans += 1
            for trigger in ("BEFORE UPDATE ON settings WHEN NEW.key='startup.mode'", "BEFORE INSERT ON audit_logs WHEN NEW.action='protection_reset'"):
                put('startup.mode', 'protected'); db.commit()
                db.execute("CREATE TRIGGER synthetic_recovery_fault " + trigger + " BEGIN SELECT RAISE(ABORT,'synthetic recovery fault'); END;"); db.commit()
                before = state()
                try: recover()
                except sqlite3.IntegrityError: pass
                else: raise AssertionError('Synthetic recovery fault did not fire')
                assert state() == before; plans += 1
                db.execute('DROP TRIGGER synthetic_recovery_fault'); db.commit()
            before = db.execute("SELECT COUNT(*) FROM audit_logs WHERE action='protection_reset'").fetchone()[0]
            recover(); assert db.execute("SELECT COUNT(*) FROM audit_logs WHERE action='protection_reset'").fetchone()[0] == before + 1; plans += 1
            for table in ('sales', 'purchases', 'payments', 'stock_movements'):
                assert db.execute(f'SELECT COUNT(*) FROM {table}').fetchone()[0] == 0
            db.close()
    print(f'PASS {len(migrations)} migrations;{plans} Python-owned extracted recovery SQL plans in WAL+DELETE:atomic manual/audit/focus preservation,setting/audit rollback,reviewed retry;zero financial posts.')
    print('NOT C# outcome/guard/cache/auth/registry/provider COMMIT-fault/Windows proof.')


if __name__ == '__main__': run()
