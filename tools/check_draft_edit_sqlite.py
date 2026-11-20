#!/usr/bin/env python3
"""Synthetic two-connection SQLite draft plans using extracted SQL. Python owns the baseline,
compare/authorization assumptions/Tx/control flow,NOT C# Database/DraftService/provider/WPF."""
from pathlib import Path
import hashlib
import json
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    source = (ROOT / 'dotnet/Area11.Core/Services/DraftService.cs').read_text()
    concurrency = (ROOT / 'dotnet/Area11.Core/Services/EditConcurrency.cs').read_text()
    assert '"draft" => "document_drafts"' in concurrency
    insert = quoted(source, 'INSERT INTO document_drafts')
    save = quoted(source, 'UPDATE document_drafts SET payload_json')
    mark = quoted(source, 'UPDATE document_drafts\nSET payload_json = @payload, status')
    restore = quoted(source, 'UPDATE document_drafts\nSET status = \'draft\'')
    discard = quoted(source, 'UPDATE document_drafts\nSET status = \'discarded\'')
    # Use the literal ReadRow query,not the similar List query.
    read = quoted(source.split('private DocumentDraft? ReadRow(', 1)[1], 'SELECT id, draft_key AS draftKey')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    cases = 0
    with tempfile.TemporaryDirectory(prefix='area11-draft-edit-') as folder:
        for mode in ('WAL', 'DELETE'):
            path = str(Path(folder) / (mode + '.db'))
            one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
            for migration in migrations: one.executescript(migration.read_text())
            one.execute("INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');"); one.commit()
            assert one.execute('PRAGMA journal_mode=' + mode).fetchone()[0].upper() == mode

            def baseline(db, id):
                # Mirrors Capture's full-row intent,not its provider-specific serialized bytes.
                rows = db.execute('SELECT * FROM document_drafts WHERE id=@id;', dict(id=id)).fetchall()
                return hashlib.sha256(json.dumps(rows, separators=(',', ':')).encode()).digest()

            def dump(): return tuple(one.iterdump())

            for type in ('sale', 'purchase'):
                args = dict(key='synthetic-' + type, type=type, payload='{"Lines":[{"Quantity":1}]}',
                            byId=9001, by='Synthetic owner', at='2026-11-10 09:00:00')
                one.execute(insert, args); id = one.execute('SELECT last_insert_rowid();').fetchone()[0]; one.commit()
                old = baseline(one, id)
                # Both pages opened the same payload. Writer2 changes it within the SAME timestamp.
                two.execute('BEGIN IMMEDIATE')
                two.execute(save, dict(payload='{"Lines":[{"Quantity":2}]}', at=args['at'], id=id)); two.commit()
                assert one.execute(read, dict(id=id)).fetchone() is not None
                after = dump()
                # Python,not production C#,decides not to execute each extracted write when stale.
                for statement, params in [(save, dict(payload='{"Synthetic":"stale"}', at=args['at'], id=id)),
                                          (mark, dict(payload='{"Synthetic":"stale"}', marker=-1, at=args['at'], id=id)),
                                          (discard, dict(reason='stale', at=args['at'], id=id))]:
                    one.execute('BEGIN IMMEDIATE'); matches = baseline(one, id) == old
                    if matches: one.execute(statement, params)
                    one.rollback(); assert not matches; assert dump() == after; cases += 1
                # Fresh reviewed save returns exactly its own state,without a post-commit re-read.
                fresh = baseline(one, id); one.execute('BEGIN IMMEDIATE'); assert baseline(one, id) == fresh
                one.execute(save, dict(payload='{"Lines":[{"Quantity":3}]}', at=args['at'], id=id))
                own = baseline(one, id); one.commit(); assert own == baseline(one, id); cases += 1
                # Another writer changes after our COMMIT but before UI cleanup. Own token stays old.
                two.execute(save, dict(payload='{"Lines":[{"Quantity":4}]}', at=args['at'], id=id)); two.commit()
                assert baseline(one, id) != own; cases += 1
                # Pre-mark is still terminal. A *synthetic known no-document-commit* allows restore;
                # capture its baseline in that restoration Tx,then a later writer must invalidate it.
                fresh = baseline(one, id); one.execute('BEGIN IMMEDIATE'); assert baseline(one, id) == fresh
                one.execute(mark, dict(payload='{"Lines":[{"Quantity":5}]}', marker=-1, at=args['at'], id=id)); one.commit()
                one.execute('BEGIN IMMEDIATE'); changed = one.execute(restore, dict(id=id, type=type, marker=-1, at=args['at'])).rowcount
                assert changed == 1; restored = baseline(one, id); one.commit(); assert baseline(one, id) == restored; cases += 1
                two.execute(save, dict(payload='{"Lines":[{"Quantity":6}]}', at=args['at'], id=id)); two.commit()
                assert baseline(one, id) != restored; cases += 1
                # An old verification-marker summary cannot consume a changed marker.
                one.execute(mark, dict(payload='{}', marker=-1, at=args['at'], id=id)); one.commit(); marker = baseline(one, id)
                two.execute("UPDATE document_drafts SET discard_reason='changed after modal' WHERE id=?", (id,)); two.commit()
                assert marker != baseline(one, id); cases += 1
                # BEGIN IMMEDIATE protects the comparison/write window from another connection.
                one.execute('BEGIN IMMEDIATE')
                try: two.execute(save, dict(payload='{"Synthetic":"must wait"}', at=args['at'], id=id))
                except sqlite3.OperationalError: two.rollback()
                else: raise AssertionError('second writer bypassed reservation')
                one.rollback(); cases += 1
                # Frozen list/read snapshot:WAL permits another commit;DELETE blocks that COMMIT.
                one.execute('PRAGMA query_only=ON'); one.execute('BEGIN DEFERRED'); frozen = baseline(one, id)
                try:
                    two.execute(save, dict(payload='{"Synthetic":"after snapshot"}', at=args['at'], id=id)); two.commit()
                except sqlite3.OperationalError:
                    assert mode == 'DELETE'; two.rollback()
                else: assert mode == 'WAL'
                assert baseline(one, id) == frozen
                one.commit(); one.execute('PRAGMA query_only=OFF'); cases += 1
            for table in ('sales', 'purchases', 'payments', 'stock_movements', 'audit_logs'):
                assert one.execute(f'SELECT COUNT(*) FROM {table};').fetchone()[0] == 0
            one.close(); two.close()
    print(f'PASS {len(migrations)} migrations;{cases} Python-owned draft compare/handoff/reservation/snapshot plans across WAL+DELETE;zero financial/audit posts.')
    print('NOT C# DraftService/Database,auth,creator failures,provider COMMIT/restore faults,WPF or LAN/process-crash proof.')


if __name__ == '__main__': run()
