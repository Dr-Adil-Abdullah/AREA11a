#!/usr/bin/env python3
"""Fixture-only SQL evidence for the owner's v0.120.1 failures.
Actual migrations/fixture marker/identity/grant SQL;Python owns plans/assertions.
NOT .NET tests,settings authorization,drawer-factory execution or Windows proof.
"""
from pathlib import Path
import re
import sqlite3
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    c = sqlite3.connect(':memory:'); c.execute('PRAGMA foreign_keys=ON')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for p in migrations: c.executescript(p.read_text())
    services = ROOT / 'dotnet/Area11.Core/Services'
    seed = quoted((services / 'SeedService.cs').read_text(), 'INSERT INTO users')
    c.execute(seed, dict(hash='synthetic-not-a-login-hash', at='2026-11-10'))
    actor = quoted((services / 'LedgerReadAccess.cs').read_text(), 'SELECT id, name, role, active')
    assert c.execute(actor, dict(id=1)).fetchone() == (1, 'admin', 'owner', 1)
    assert c.execute(actor, dict(id=99)).fetchone() is None
    factory = (ROOT / 'dotnet/Area11.Tests/TestData.cs').read_text().split('public static User AddUser(', 1)[1].split('public static void SeedRawSetting(', 1)[0]
    create_user = quoted(factory, 'INSERT INTO users')
    c.execute(create_user, dict(name='Manager', role='manager', at='2026-11-10'))
    manager = c.execute(actor, dict(id=2)).fetchone(); assert manager == (2, 'Manager', 'manager', 1)
    # Actual stored module row/query;no execution of PermissionService.Can or its role defaults.
    permissions = (services / 'PermissionService.cs').read_text().split('public bool Can(', 1)[1].split('public static bool IsFinancialVisibility', 1)[0]
    read_grant = quoted(permissions, 'SELECT allowed FROM user_permissions')
    assert c.execute(read_grant, dict(userId=2, module='customers')).fetchone() is None
    c.execute("INSERT INTO user_permissions(user_id,module_key,allowed) VALUES(2,'customers',0)")
    assert c.execute(read_grant, dict(userId=2, module='customers')).fetchone() == (0,)
    c.execute("INSERT INTO document_drafts(draft_key,draft_type,payload_json,created_by_id,created_by_name) VALUES('fixture','sale','{}',1,'admin')")
    c.commit(); before = c.execute('SELECT status,posted_document_id,posted_at FROM document_drafts').fetchone()
    try:
        c.execute("UPDATE document_drafts SET status='posted',posted_document_id=-1;")
        raise AssertionError('Original invalid marker unexpectedly accepted')
    except sqlite3.IntegrityError:
        c.rollback()
    assert c.execute('SELECT status,posted_document_id,posted_at FROM document_drafts').fetchone() == before
    tests = (ROOT / 'dotnet/Area11.Tests/AutomaticDraftPostingTests.cs').read_text()
    marker = re.search(r'if \(state == "marker"\) f.Execute\("([^"]+)"\);', tests)[1]
    c.execute(marker)
    status, document, at = c.execute('SELECT status,posted_document_id,posted_at FROM document_drafts').fetchone()
    assert status == 'posted' and document == -1 and at is not None
    assert c.execute('SELECT COUNT(*) FROM sales').fetchone()[0] == 0
    c.close()
    print(f'PASS {len(migrations)} migrations;stored owner/missing/real manager,explicit deny lookup,old marker CHECK refusal/unchanged row,new actual fixture marker accepted without sale.NOT C#/Windows execution.')


if __name__ == '__main__': run()
