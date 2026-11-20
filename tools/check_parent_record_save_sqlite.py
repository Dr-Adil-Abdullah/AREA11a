#!/usr/bin/env python3
"""Actual selected parent/catalog write SQL,Python-owned plans/Tx/audit arguments.
NOT execution of C# validation/dynamic Product.Update/Database.Tx/UI/COMMIT failures.
"""
from pathlib import Path
import re
import sqlite3
from check_ledger_posting_sqlite import quoted
ROOT = Path(__file__).resolve().parents[1]
SERVICES = ROOT / 'dotnet/Area11.Core/Services'


def method(source, name):
    m = re.search(r'(?:public|private) ServiceResult(?:<[^>]+>)? '+name+r'\(', source)
    assert m, name
    return re.split(r'\n    (?:public|private) ', source[m.end():], 1)[0]


def run():
    product = (SERVICES / 'ProductService.cs').read_text()
    partner = (SERVICES / 'PartnerService.cs').read_text()
    audit = quoted((SERVICES / 'AuditService.cs').read_text(), 'INSERT INTO audit_logs')
    plans = [
        ('product-create', [quoted(method(product, 'Create'), 'INSERT INTO products')]),
        ('customer-create', [quoted(method(partner, 'CreateCustomer'), 'INSERT INTO customers')]),
        ('supplier-create', [quoted(method(partner, 'CreateSupplier'), 'INSERT INTO suppliers')]),
        ('customer-edit', [quoted(method(partner, 'SaveCustomer'), 'UPDATE customers')])]
    for name, table in [('Category', 'categories'), ('Company', 'companies'), ('Formula', 'generic_formulas'), ('Brand', 'brands'), ('Unit', 'units')]:
        plans.append((name.lower()+'-add', [quoted(method(product, 'Add'+name), 'INSERT INTO '+table)]))
        edits = method(product, 'Update'+name)
        queries = [quoted(edits, 'UPDATE '+table)]
        if name in ('Formula', 'Brand'): queries.insert(0, quoted(edits, 'UPDATE products'))
        if name == 'Unit':
            queries = [quoted(edits, 'UPDATE products SET base_unit'), quoted(edits, 'UPDATE products SET strength_unit')] + queries
        plans.append((name.lower()+'-edit', queries))
    plans.append(('brand-link', [quoted(method(product, 'SetBrandCompany'), 'UPDATE brands')]))
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))

    def seed():
        db = sqlite3.connect(':memory:'); db.execute('PRAGMA foreign_keys=ON')
        for p in migrations: db.executescript(p.read_text())
        db.executescript("""
INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic record owner','owner',1);
INSERT INTO customers(id,name,category,credit_limit_paisa,balance_paisa) VALUES(9001,'Synthetic customer','normal',5000,0);
INSERT INTO suppliers(id,name,balance_paisa) VALUES(9001,'Synthetic supplier',0);
INSERT INTO categories(id,name) VALUES(9001,'Synthetic category');
INSERT INTO companies(id,name) VALUES(9001,'Synthetic company');
INSERT INTO generic_formulas(id,name,name_key) VALUES(9001,'Synthetic formula','synthetic formula');
INSERT INTO brands(id,name,name_key) VALUES(9001,'Synthetic brand','synthetic brand');
INSERT INTO units(id,name) VALUES(9001,'syntheticunit');
INSERT INTO products(id,name,base_unit,generic_formula_id,brand_id,cost_paisa,retail_paisa)
VALUES(9001,'Synthetic product','syntheticunit',9001,9001,20000,30000);
INSERT INTO batches(product_id,batch_no,qty_base,cost_paisa,retail_paisa) VALUES(9001,'synthetic-batch',100,20000,30000);
""")
        return db

    args = dict(id=9001, brandId=9001, name='Synthetic changed', clean='Synthetic changed', key='synthetic changed',
                category='normal', phone=None, agency=None, limit=5000, parentId=None, sortOrder=10, order=10,
                generic=None, brand=None, barcode=None, categoryId=None, companyId=9001, formulaId=None,
                preferredSupplierId=None, rackNo=None, room=None, packSizeLabel=None, baseUnit='tablet',
                strengthValue=0, strengthUnit='', boxStrips=0, stripTablets=0, costPaisa=20000, retailPaisa=30000,
                vipPaisa=0, doctorPaisa=0, reorderLevel=0, trackExpiry=1, at='2026-11-10 12:00:00', wholeOnly=0,
                old='syntheticunit', userId=1, userName='Synthetic record owner', module='Products', action='synthetic_sql_save',
                entity='records', entityId='9001', oldValue='synthetic old', newValue='synthetic new', details=None)

    def post(db, queries):
        try:
            db.execute('BEGIN IMMEDIATE')
            for sql in queries: db.execute(sql, args)
            db.execute(audit, args); db.commit(); return True
        except sqlite3.DatabaseError:
            db.rollback(); return False

    cases = 0
    for name, queries in plans:
        for when in ('BEFORE', 'AFTER'):
            db = seed(); before = tuple(db.iterdump())
            db.executescript('CREATE TRIGGER record_save_fault '+when+" INSERT ON audit_logs BEGIN SELECT RAISE(ABORT,'synthetic-save-fault'); END;")
            assert not post(db, queries), name
            db.execute('DROP TRIGGER record_save_fault'); assert tuple(db.iterdump()) == before, name
            assert post(db, queries), name
            assert db.execute('SELECT COUNT(*) FROM audit_logs').fetchone()[0] == 1
            assert db.execute('SELECT COUNT(*) FROM payments').fetchone()[0] == 0
            assert db.execute('SELECT COUNT(*) FROM sales').fetchone()[0] == 0
            assert db.execute('SELECT COUNT(*) FROM purchases').fetchone()[0] == 0
            assert db.execute('SELECT SUM(qty_base) FROM batches').fetchone()[0] == 100
            assert not db.execute('PRAGMA foreign_key_check').fetchall()
            db.close(); cases += 1
    # Existing two-step brand contract: name is committed even when the separate link fails.
    db = seed(); brand = dict(plans)['brand-add']; link = dict(plans)['brand-link']
    assert post(db, brand)
    brand_id = db.execute("SELECT id FROM brands WHERE name='Synthetic changed'").fetchone()[0]
    args['brandId'] = brand_id
    db.executescript("CREATE TRIGGER link_fault BEFORE UPDATE OF company_id ON brands BEGIN SELECT RAISE(ABORT,'synthetic-link-fault'); END;")
    assert not post(db, link)
    assert db.execute('SELECT name,company_id FROM brands WHERE id=?', (brand_id,)).fetchone() == ('Synthetic changed', None)
    assert db.execute("SELECT COUNT(*) FROM brands WHERE name='Synthetic changed'").fetchone()[0] == 1
    db.close()
    print(f'PASS {len(migrations)} migrations;{len(plans)} extracted-SQL plans/{cases} audit-trigger rollback+retry cases;1 two-phase brand name/link case;financial rows/stock unchanged.')
    print('NOT C#/validation/dynamic Product.Update/Database.Tx/WPF/provider COMMIT or durable-dedup proof;Python owns plan/Tx/audit arguments.')


if __name__ == '__main__': run()
