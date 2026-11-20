#!/usr/bin/env python3
"""Real SQLite two-connection probes over migrated synthetic files. Python owns fingerprint,
comparison/Tx/control flow:NOT C# EditConcurrency/Database/WPF/provider execution."""
from pathlib import Path
import hashlib
import json
import re
import sqlite3
import tempfile
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]


def run():
    source = (ROOT / 'dotnet/Area11.Core/Services/EditConcurrency.cs').read_text()
    constants = dict(re.findall(r'internal const string (\w+) = "([a-z_]+)";', source))
    tables = {constants.get(a, a.strip('"')): b for a, b in re.findall(r'(\w+|"[a-z]+") => "([a-z_]+)"', source)}
    customer_columns = re.search(r'scope == Customer \? "([^"]+)"', source)[1]
    tag_sql = quoted(source, 'SELECT category_id FROM product_category_tags')
    defs_sql = quoted(source.split('private static string Digest', 1)[0], 'SELECT * FROM custom_fields WHERE entity=@entity')
    values_sql = quoted(source, 'SELECT field_id,value,updated_at FROM custom_values')
    manager_sql = quoted(source, 'SELECT * FROM custom_fields WHERE entity=(SELECT entity')
    phone_sql = quoted((ROOT / 'dotnet/Area11.Core/Services/PartnerService.cs').read_text(), 'SELECT name FROM customers WHERE active=1')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    cases = 0
    with tempfile.TemporaryDirectory(prefix='area11-concurrency-') as folder:
        path = str(Path(folder) / 'synthetic.db')
        one = sqlite3.connect(path, timeout=.05); two = sqlite3.connect(path, timeout=.05)
        for migration in migrations: one.executescript(migration.read_text())
        one.executescript("""
INSERT INTO users(id,name,role) VALUES(9001,'Synthetic owner','owner');
INSERT INTO products(id,name,base_unit,cost_paisa,retail_paisa) VALUES(9001,'Synthetic item','tablet',20000,30000);
INSERT INTO customers(id,name,category,phone,credit_limit_paisa,balance_paisa) VALUES(9001,'Synthetic customer','normal','oldphone',0,0);
INSERT INTO custom_fields(id,entity,label,type,required,active,sort) VALUES(9001,'product','Synthetic question','text',0,1,10);
INSERT INTO custom_values(field_id,entity,entity_id,value) VALUES(9001,'product',9001,'oldanswer');
INSERT INTO form_field_definitions(id,field_key,section_key,label,data_type,input_method,safe_required,required,critical) VALUES(9001,'synthetic.input','Sale','Synthetic native','text','text',1,1,1);
INSERT INTO categories(id,name) VALUES(9001,'Synthetic category');
INSERT INTO companies(id,name) VALUES(9001,'Synthetic company');
INSERT INTO generic_formulas(id,name,name_key) VALUES(9001,'Synthetic formula','synthetic formula');
INSERT INTO brands(id,name,name_key) VALUES(9001,'Synthetic brand','synthetic brand');
INSERT INTO units(id,name) VALUES(9001,'syntheticunit');
""")
        one.execute('PRAGMA journal_mode=WAL'); one.commit()

        def fingerprint(db, scope):
            args = dict(id=9001, entity=scope.split(':', 1)[-1]); parts = []
            if scope in tables and scope != 'custom_field':
                cols = customer_columns if scope == 'customer' else '*'
                parts.append(db.execute(f'SELECT {cols} FROM {tables[scope]} WHERE id=@id;', args).fetchall())
            if scope == 'product': parts.append(db.execute(tag_sql, args).fetchall())
            if scope in ('product', 'customer') or scope.startswith('answers:'):
                parts.append(db.execute(defs_sql, args).fetchall()); parts.append(db.execute(values_sql, args).fetchall())
            if scope == 'custom_field': parts.append(db.execute(manager_sql, args).fetchall())
            return hashlib.sha256(json.dumps(parts, separators=(',', ':')).encode()).digest()

        mutations = {
            'product': "UPDATE products SET rack_no=IFNULL(rack_no,'')||'new' WHERE id=9001;",
            'customer': "UPDATE customers SET phone=phone||'new' WHERE id=9001;",
            'custom_field': "UPDATE custom_fields SET label=label||'new' WHERE id=9001;",
            'native_field': "UPDATE form_field_definitions SET label=label||'new' WHERE id=9001;",
            'category': "UPDATE categories SET name=name||'new' WHERE id=9001;",
            'company': "UPDATE companies SET name=name||'new' WHERE id=9001;",
            'formula': "UPDATE generic_formulas SET name=name||'new' WHERE id=9001;",
            'brand': "UPDATE brands SET name=name||'new' WHERE id=9001;",
            'unit': "UPDATE units SET name=name||'new' WHERE id=9001;",
            'answers:product': "UPDATE custom_values SET value=value||'new' WHERE entity='product' AND entity_id=9001;"}
        for scope, mutation in mutations.items():
            old = fingerprint(one, scope)
            two.execute('BEGIN IMMEDIATE'); two.execute(mutation); two.commit()
            after_other = tuple(one.iterdump())
            one.execute('BEGIN IMMEDIATE'); assert fingerprint(one, scope) != old, scope
            one.rollback(); assert tuple(one.iterdump()) == after_other, scope
            # A reviewed fresh baseline matches under the following reservation.
            fresh = fingerprint(one, scope); one.execute('BEGIN IMMEDIATE'); assert fingerprint(one, scope) == fresh; one.rollback()
            cases += 1
        # Non-edit ledger movement intentionally does not invalidate a customer phone form.
        old = fingerprint(one, 'customer'); two.execute('UPDATE customers SET balance_paisa=12345,loyalty_points=7 WHERE id=9001;'); two.commit()
        assert fingerprint(one, 'customer') == old; cases += 1
        # Category tags,definitions and answers must invalidate a product form even if updated_at stays unchanged.
        for mutation in ["INSERT INTO product_category_tags(product_id,category_id) VALUES(9001,9001);",
                         "UPDATE custom_fields SET help_text='different semantics' WHERE id=9001;",
                         "UPDATE custom_values SET value='different answer' WHERE field_id=9001;"]:
            old = fingerprint(one, 'product'); two.execute(mutation); two.commit(); assert fingerprint(one, 'product') != old; cases += 1
        # One snapshot sees old/old,not old/new,while another connection commits.
        one.execute('BEGIN DEFERRED'); old = one.execute('SELECT name FROM products WHERE id=9001').fetchone()
        two.execute("UPDATE products SET name='changed between reads' WHERE id=9001"); two.commit()
        assert one.execute('SELECT name FROM products WHERE id=9001').fetchone() == old
        one.commit(); assert one.execute('SELECT name FROM products WHERE id=9001').fetchone() != old; cases += 1
        one.execute('PRAGMA query_only=ON'); one.execute('BEGIN DEFERRED')
        try: one.execute("UPDATE products SET name='must not write'")
        except sqlite3.OperationalError: one.rollback()
        else: raise AssertionError('query_only write survived')
        one.execute('PRAGMA query_only=OFF'); cases += 1
        # BEGIN IMMEDIATE excludes another writer throughout compare/write,not just around UPDATE.
        one.execute('BEGIN IMMEDIATE')
        try: two.execute("UPDATE products SET rack_no='must wait' WHERE id=9001")
        except sqlite3.OperationalError: two.rollback()
        else: raise AssertionError('second writer bypassed reservation')
        one.rollback(); cases += 1
        # Both early phone preflights can be empty;the repeated in-Tx lookup prevents second insertion.
        assert one.execute(phone_sql, dict(phone='samephone')).fetchone() is None
        assert two.execute(phone_sql, dict(phone='samephone')).fetchone() is None
        one.execute('BEGIN IMMEDIATE'); one.execute("INSERT INTO customers(name,phone) VALUES('Synthetic first','samephone')"); one.commit()
        two.execute('BEGIN IMMEDIATE'); assert two.execute(phone_sql, dict(phone='samephone')).fetchone() is not None; two.rollback(); cases += 1
        assert one.execute("SELECT COUNT(*) FROM customers WHERE phone='samephone'").fetchone()[0] == 1
        for table in ['sales','purchases','payments','stock_movements']:
            assert one.execute(f'SELECT COUNT(*) FROM {table}').fetchone()[0] == 0
        one.close(); two.close()
    print(f'PASS {len(migrations)} migrations;{cases} Python-owned two-connection SQLite compare/snapshot/reservation plans;financial posts remain zero.')
    print('NOT C# EditConcurrency/Database,provider failures,WPF,runtime security or durable identity proof.')


if __name__ == '__main__': run()
