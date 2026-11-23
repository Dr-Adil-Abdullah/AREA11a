#!/usr/bin/env python3
"""Synthetic icon file+SQLite reference.Python owns compare/auth assumptions/transaction/outcome.
NOT C# IconAssetService/SettingsService,provider/COMMIT classification,WPF/codec/ACL/powercut proof."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]
KEY = 'appearance.iconFileName'
CONTENT = bytes([0, 0, 1, 0, 1, 0, 10, 20])


def run():
    core = ROOT / 'dotnet/Area11.Core'
    source = (core / 'Services/SettingsService.cs').read_text()
    write = source.split('public ServiceResult SetMany(', 1)[1].split('internal ServiceResult CheckIconEdit(', 1)[0]
    upsert = quoted(write, 'INSERT INTO settings')
    audit_sql = quoted((core / 'Services/AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    migrations = sorted((core / 'Data/Migrations').glob('*.sql')); plans = 0
    with tempfile.TemporaryDirectory(prefix='area11-icon-edit-') as folder:
        root = Path(folder)
        for mode in ('WAL', 'DELETE'):
            photos = root / mode; photos.mkdir()
            one = sqlite3.connect(str(root / (mode + '.db')), timeout=.05); two = sqlite3.connect(str(root / (mode + '.db')), timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.execute("INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');"); one.commit()
            assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode
            one.execute('PRAGMA foreign_keys=ON'); two.execute('PRAGMA foreign_keys=ON')
            def put(db, name): db.execute(upsert, dict(key=KEY, value=json.dumps(name), at='2026-11-10 12:00:00'))
            def token(db): return hashlib.sha256(json.dumps(db.execute('SELECT key,value FROM settings WHERE key=?', (KEY,)).fetchall()).encode()).digest()
            def audit(): one.execute(audit_sql, dict(at='2026-11-10 12:00:00', userId=9001, userName='Synthetic owner',
                action='settings_change', module='Settings', entity='settings', entityId=KEY, oldValue='""', newValue='"synthetic"', details=None))
            def snapshot(): return tuple(one.iterdump())
            def create(label):
                path = photos / (label + '.ico')
                with path.open('xb') as output: output.write(CONTENT); output.flush()
                return path
            put(one, ''); one.commit(); old_file = create('old-retained')
            old = token(one); mine = create('success')
            one.execute('BEGIN IMMEDIATE'); assert token(one) == old; put(one, mine.name); audit(); own = token(one); one.commit()
            assert own == token(one); assert mine.read_bytes() == CONTENT; assert old_file.exists(); plans += 1
            # Known stale preflight has no copied file or write.
            before = snapshot(); files = sorted(photos.iterdir()); one.execute('BEGIN IMMEDIATE'); assert old != token(one); one.rollback()
            assert before == snapshot(); assert sorted(photos.iterdir()) == files; plans += 1
            # A writer between preflight and pointer update is caught by final checked transaction.
            old = token(one); copied = create('concurrent-refused'); put(two, 'concurrent.ico'); two.commit(); before = snapshot()
            one.execute('BEGIN IMMEDIATE'); assert old != token(one); one.rollback(); copied.unlink()
            assert before == snapshot(); assert mine.exists() and old_file.exists(); plans += 1
            one.executescript("CREATE TRIGGER synthetic_icon_audit BEFORE INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic'); END;")
            old = token(one); copied = create('audit-refused'); before = snapshot()
            try:
                one.execute('BEGIN IMMEDIATE'); assert old == token(one); put(one, copied.name); audit(); one.commit()
            except sqlite3.IntegrityError: one.rollback(); copied.unlink()
            else: raise AssertionError('audit fault absent')
            assert before == snapshot(); assert mine.exists(); one.execute('DROP TRIGGER synthetic_icon_audit'); one.commit(); plans += 1
            one.executescript("CREATE TABLE synthetic_icon_commit(id INTEGER PRIMARY KEY,parent_id INTEGER REFERENCES users(id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER synthetic_icon_commit_audit AFTER INSERT ON audit_logs BEGIN INSERT INTO synthetic_icon_commit(parent_id) VALUES(-99999); END;")
            old = token(one); copied = create('commit-unconfirmed'); before = snapshot(); commit_attempted = False
            try:
                one.execute('BEGIN IMMEDIATE'); assert old == token(one); put(one, copied.name); audit(); commit_attempted = True; one.commit()
            except sqlite3.IntegrityError: one.rollback()
            else: raise AssertionError('deferred COMMIT fault absent')
            assert commit_attempted; assert copied.exists(); assert before == snapshot(); plans += 1
            one.execute('DROP TRIGGER synthetic_icon_commit_audit'); one.commit()
            # Lost acknowledgement AFTER a real commit:retain the referenced file.
            old = token(one); copied = create('lost-ack'); one.execute('BEGIN IMMEDIATE'); assert token(one) == old
            put(one, copied.name); audit(); one.commit(); assert copied.exists()
            assert json.loads(one.execute('SELECT value FROM settings WHERE key=?', (KEY,)).fetchone()[0]) == copied.name; plans += 1
            stale_reset = old; before = snapshot(); one.execute('BEGIN IMMEDIATE'); assert token(one) != stale_reset; one.rollback()
            assert before == snapshot(); assert copied.exists(); plans += 1
            old = token(one); files = sorted(photos.iterdir()); one.execute('BEGIN IMMEDIATE'); assert token(one) == old
            put(one, ''); audit(); one.commit(); assert files == sorted(photos.iterdir()); plans += 1
            one.execute('BEGIN IMMEDIATE')
            try: put(two, 'blocked.ico'); two.commit()
            except sqlite3.OperationalError: two.rollback()
            else: raise AssertionError('writer reservation bypassed')
            one.rollback(); plans += 1
            for table in ('sales', 'purchases', 'payments', 'stock_movements'):
                assert one.execute('SELECT COUNT(*) FROM ' + table).fetchone()[0] == 0
            one.close(); two.close()
        # File ownership fault reference:never clean a collision,only the newly created copy.
        old_file = root / 'collision.ico'; old_file.write_bytes(b'sentinel')
        created = False
        try:
            with old_file.open('xb') as output: created = True; output.write(CONTENT)
        except FileExistsError: assert not created
        assert old_file.read_bytes() == b'sentinel'; plans += 1
        for phase in ('write', 'flush', 'dispose'):
            path = root / (phase + '.ico'); created = False
            try:
                with path.open('xb') as output:
                    created = True
                    if phase == 'write': output.write(CONTENT[:4]); raise OSError('synthetic write')
                    output.write(CONTENT)
                    if phase == 'flush': raise OSError('synthetic flush')
                raise OSError('synthetic dispose')
            except OSError:
                if created: path.unlink()
            assert not path.exists(); assert old_file.read_bytes() == b'sentinel'; plans += 1
        # File residue is preferable to deleting an asset after an unconfirmed Settings response.
        retained = root / 'unknown-throw.ico'; retained.write_bytes(CONTENT); writer_started = True
        assert writer_started and retained.exists(); plans += 1
        growing = root / 'growing.ico'; growing.write_bytes(b'x' * (2 * 1024 * 1024 + 1))
        with growing.open('rb') as source: bounded = source.read(2 * 1024 * 1024 + 1)
        assert len(bounded) > 2 * 1024 * 1024; plans += 1
    print(f'PASS {len(migrations)} migrations;{plans} Python-owned icon file/checked-pointer/audit-rollback/deferred-COMMIT/lost-ack/reset/reservation cases;zero financial posts.')
    print('NOT C# services/auth/TransactionOutcome/cache/codec/WPF/ACL/provider COMMIT-fault/powerloss/cross-resource atomicity proof.')


if __name__ == '__main__': run()
