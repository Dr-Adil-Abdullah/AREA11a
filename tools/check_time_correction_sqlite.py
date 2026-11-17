#!/usr/bin/env python3
"""Isolated SQLite lock/rollback proof using actual source SQL, NOT C# service execution.
Only a TemporaryDirectory synthetic database is opened; no shop paths or CLI DB arguments.
"""
from datetime import datetime, timedelta
from pathlib import Path
import re
import sqlite3
import tempfile

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / 'dotnet/Area11.Core'


def run() -> None:
    source = (CORE / 'Services/TimeCorrectionService.cs').read_text()
    sql = re.findall(r'@"((?:[^"]|"")*)"', source)
    by_id = next(x for x in sql if 'WHERE s.id = @id' in x)
    update_sale = next(x for x in sql if 'UPDATE sales SET date' in x)
    read_payments = next(x for x in sql if 'FROM payments WHERE sale_id' in x)
    update_payment = re.search(r'"(UPDATE payments SET date[^\"]+)"', source).group(1)
    audit_sql = re.search(r'@"((?:[^"]|"")*)"', (CORE / 'Services/AuditService.cs').read_text()).group(1)
    migrations = sorted((CORE / 'Data/Migrations').glob('*.sql'))
    with tempfile.TemporaryDirectory(prefix='area11-time-proof-') as folder:
        path = str(Path(folder) / 'synthetic.db')
        first = sqlite3.connect(path, timeout=0, isolation_level=None)
        second = sqlite3.connect(path, timeout=0, isolation_level=None)
        try:
            for c in (first, second):
                c.execute('PRAGMA foreign_keys=ON')
            for migration in migrations:
                first.executescript(migration.read_text())
            first.execute("INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic Owner','owner',1)")
            first.execute("""INSERT INTO sales(id,code,user_id,date,created_at,total_paisa,paid_paisa,status,time_offset_seconds)
VALUES(1,'SYNTHETIC',1,'2026-11-01 10:00:00','2026-11-01 10:00:00',30000,30000,'paid',0)""")
            first.execute("INSERT INTO payments(id,sale_id,user_id,date,method,amount_paisa) VALUES(1,1,1,'2026-11-01 10:00:00','cash',30000)")
            before_sale = first.execute('SELECT * FROM sales').fetchone()
            before_payment = first.execute('SELECT * FROM payments').fetchone()
            first.execute('BEGIN IMMEDIATE')
            row = first.execute(by_id, {'id': 1}).fetchone()
            try:
                second.execute("UPDATE sales SET date='2026-11-04 10:00:00' WHERE id=1")
            except sqlite3.OperationalError as error:
                assert 'locked' in str(error).lower(), str(error)
            else:
                raise AssertionError('second writer changed bill during immediate read-plan transaction')
            first.rollback()
            print('PASS immediate read-plan transaction blocks a second writer (independent SQLite, not C# threading)')

            def write_correction() -> None:
                # Python independently supplies the same two-day shift; the SQL is extracted above.
                original = row[2]
                corrected = (datetime.strptime(original, '%Y-%m-%d %H:%M:%S') + timedelta(days=2)).strftime('%Y-%m-%d %H:%M:%S')
                first.execute(update_sale, {'date': corrected, 'offset': 172800, 'id': 1})
                for payment_id, stamp in first.execute(read_payments, {'id': 1}).fetchall():
                    date = (datetime.strptime(stamp, '%Y-%m-%d %H:%M:%S') + timedelta(days=2)).strftime('%Y-%m-%d %H:%M:%S')
                    first.execute(update_payment, {'date': date, 'id': payment_id})
                first.execute(audit_sql, dict(at=corrected, userId=1, userName='Synthetic Owner',
                    action='sale_time_fixed', module='sales', entity='sales', entityId='1',
                    oldValue=original, newValue=corrected, details='synthetic SQL proof only'))

            first.executescript("""CREATE TRIGGER fault BEFORE INSERT ON audit_logs
WHEN NEW.action='sale_time_fixed' BEGIN SELECT RAISE(ABORT,'synthetic audit fault'); END;""")
            first.execute('BEGIN IMMEDIATE')
            try:
                write_correction()
            except sqlite3.IntegrityError:
                first.rollback()
            else:
                raise AssertionError('audit fault was not injected')
            assert first.execute('SELECT * FROM sales').fetchone() == before_sale
            assert first.execute('SELECT * FROM payments').fetchone() == before_payment
            assert first.execute('SELECT COUNT(*) FROM audit_logs').fetchone()[0] == 0
            print('PASS actual sale/payment/audit SQL rolls back together on injected audit failure')
            first.execute('DROP TRIGGER fault')
            first.execute('BEGIN IMMEDIATE')
            write_correction()
            first.commit()
            assert first.execute('SELECT date,created_at,total_paisa,paid_paisa,time_offset_seconds FROM sales').fetchone() == (
                '2026-11-03 10:00:00', '2026-11-01 10:00:00', 30000, 30000, 172800)
            assert first.execute('SELECT date,amount_paisa FROM payments').fetchone() == ('2026-11-03 10:00:00', 30000)
            assert first.execute('SELECT old_value,new_value FROM audit_logs').fetchone() == ('2026-11-01 10:00:00', '2026-11-03 10:00:00')
            assert first.execute('PRAGMA foreign_key_check').fetchall() == []
            print(f'PASS {len(migrations)} migrations; actual time SQL preserves created_at/money, moves dates and retains old/new audit; foreign keys clean')
        finally:
            second.close()
            first.close()
    print('NOT RUN: .NET TimeCorrectionService, xUnit, owner/session authentication, Windows/WPF, hardware or production data')


if __name__ == '__main__':
    run()
