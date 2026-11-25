#!/usr/bin/env python3
"""Part5.1 synthetic schedule/pricing SQL. Python owns fingerprints/compare/Tx/control flow;
NOT C# services/auth/cache/default calculations,real backup/registry,WPF or COMMIT-fault proof."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    core = ROOT / 'dotnet/Area11.Core'
    settings = (core / 'Services/SettingsService.cs').read_text().split('public ServiceResult SetMany(', 1)[1].split('internal bool HardwareBaselineRequired', 1)[0]
    pricing = (core / 'Services/PricingAllowanceService.cs').read_text()
    capture = (core / 'Services/EditConcurrency.cs').read_text().split('if (scope == "pricing")', 1)[1].split('if (scope == "permissions")', 1)[0]
    setting_sql = quoted(settings, 'INSERT INTO settings')
    pricing_sql = quoted(pricing, 'INSERT INTO user_pricing_allowances')
    person_sql = quoted(capture, 'SELECT id,role,active FROM users')
    allowance_sql = quoted(capture, 'SELECT * FROM user_pricing_allowances WHERE user_id')
    audit_sql = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql'))
    plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-schedule-pricing-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db'))
            one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.executescript("INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner'),(9002,'Synthetic target','cashier');")
            one.commit(); assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode

            def digest(parts): return hashlib.sha256(json.dumps(parts, separators=(',', ':')).encode()).digest()
            def put(db, key, value): db.execute(setting_sql, dict(key=key, value=json.dumps(value), at='2026-11-10 10:00:00'))
            def schedule_token(db):
                return digest([db.execute('SELECT key,value FROM settings WHERE key=?', (key,)).fetchall()
                               for key in ('backup.autoEnabled', 'backup.everyHours', 'backup.keep')])
            def price_token(db): return digest([db.execute(person_sql, dict(id=9002)).fetchall(), db.execute(allowance_sql, dict(id=9002)).fetchall()])
            def grant(db, limit=10): db.execute(pricing_sql, dict(userId=9002, editRate=1, manual=limit, profitDiscount=1,
                profit=limit, retailDiscount=1, retail=limit, overrideBasis=0, extra=1, extraLimit=100))
            def audit(): one.execute(audit_sql, dict(at='2026-11-10 10:00:00', userId=9001, userName='Synthetic owner',
                action='pricing_permissions', module='Users', entity='user_pricing_allowances', entityId='9002',
                oldValue='{}', newValue='{}', details=None))
            def snap(): return tuple(one.iterdump())

            put(one, 'backup.autoEnabled', True); put(one, 'backup.everyHours', 24); put(one, 'backup.keep', 7); one.commit()
            for key, value in [('backup.autoEnabled', False), ('backup.everyHours', 48), ('backup.keep', 14)]:
                old = schedule_token(one); put(two, key, value); two.commit(); before = snap()
                one.execute('BEGIN IMMEDIATE'); assert old != schedule_token(one); one.rollback(); assert snap() == before; plans += 1
            old = schedule_token(one); one.execute('BEGIN IMMEDIATE'); assert schedule_token(one) == old
            put(one, 'backup.keep', 20); own = schedule_token(one); one.commit(); assert own == schedule_token(one); plans += 1
            put(two, 'backup.lastRunAt', 'synthetic telemetry'); put(two, 'backup.lastFile', 'synthetic.a11bak'); two.commit()
            assert own == schedule_token(one); plans += 1
            # Missing row and an explicit all-deny row have different persisted state,even if defaults match.
            missing = price_token(one)
            two.execute(pricing_sql, dict(userId=9002, editRate=0, manual=0, profitDiscount=0, profit=0,
                retailDiscount=0, retail=0, overrideBasis=0, extra=0, extraLimit=0)); two.commit()
            assert missing != price_token(one); plans += 1
            old = price_token(one); grant(two, 15); two.commit(); before = snap()
            one.execute('BEGIN IMMEDIATE'); assert old != price_token(one); one.rollback(); assert snap() == before; plans += 1
            old = price_token(one); two.execute("UPDATE users SET role='manager' WHERE id=9002"); two.commit()
            assert old != price_token(one); plans += 1
            old = price_token(one); two.execute('UPDATE users SET active=0 WHERE id=9002'); two.commit()
            assert old != price_token(one); plans += 1
            two.execute('UPDATE users SET active=1 WHERE id=9002'); two.commit()
            old = price_token(one)
            one.execute("CREATE TRIGGER synthetic_price_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;"); one.commit(); before = snap()
            try:
                one.execute('BEGIN IMMEDIATE'); assert old == price_token(one); grant(one, 25); audit(); one.commit()
            except sqlite3.IntegrityError: one.rollback()
            else: raise AssertionError('audit fault absent')
            assert snap() == before; assert price_token(one) == old; plans += 1
            one.execute('DROP TRIGGER synthetic_price_audit;'); one.commit()
            one.execute('BEGIN IMMEDIATE'); assert old == price_token(one); grant(one, 25); audit(); own = price_token(one); one.commit()
            assert own == price_token(one); assert one.execute('SELECT COUNT(*) FROM audit_logs').fetchone()[0] == 1; plans += 1
            one.execute('BEGIN IMMEDIATE')
            try: grant(two, 30)
            except sqlite3.OperationalError: two.rollback()
            else: raise AssertionError('second writer bypassed reservation')
            one.rollback(); plans += 1
            one.execute('BEGIN DEFERRED'); frozen = price_token(one)
            try: grant(two, 30); two.commit()
            except sqlite3.OperationalError: assert mode == 'DELETE'; two.rollback()
            else: assert mode == 'WAL'
            assert price_token(one) == frozen; one.commit(); plans += 1
            for table in ('sales', 'purchases', 'payments', 'stock_movements'):
                assert one.execute(f'SELECT COUNT(*) FROM {table};').fetchone()[0] == 0
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations;{plans} Python-owned schedule/pricing compare/snapshot/reservation/audit-rollback/retry plans across WAL+DELETE;zero financial posts.')
    print('NOT C#/auth/cache/pricing defaults/real backup/registry/COMMIT faults/WPF/LAN/process-crash proof.')


if __name__ == '__main__': run()
