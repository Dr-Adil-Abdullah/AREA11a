#!/usr/bin/env python3
"""Portion28 actual catalog unlink/unit SQL on synthetic migrated SQLite.
Not C# access/Database.Tx/audit or WPF execution. Python owns these transactions.
"""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run():
    db = sqlite3.connect(':memory:')
    db.row_factory = sqlite3.Row
    db.execute('PRAGMA foreign_keys=ON;')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for path in migrations:
        db.executescript(path.read_text())
    source = (ROOT / 'dotnet/Area11.Core/Services/ProductService.cs').read_text()
    def block(method):
        match = re.search(r'    public ServiceResult(?:<int>)? ' + method + r'\(', source)
        return source[match.start():].split('\n    public ',1)[0]
    def sql(method, prefix):
        literals = re.findall(r'@"([^"]*)"|"([^"\n]*)"', block(method))
        matches = [a or b for a,b in literals if (a or b).lstrip().startswith(prefix)]
        assert len(matches) == 1,(method,prefix)
        return matches[0]
    category_unlink = sql('SetCategoryActive', 'UPDATE products SET category_id')
    category_tags = sql('SetCategoryActive', 'DELETE FROM product_category_tags')
    company_unlink = sql('SetCompanyActive', 'UPDATE products SET company_id')
    formula_unlink = sql('SetFormulaActive', 'UPDATE products SET generic_formula_id')
    brand_unlink = sql('SetBrandActive', 'UPDATE products SET brand_id')
    base_rename = sql('UpdateUnit', 'UPDATE products SET base_unit')
    strength_rename = sql('UpdateUnit', 'UPDATE products SET strength_unit')
    unit_rename = sql('UpdateUnit', 'UPDATE units SET name')
    db.executescript("""
INSERT INTO categories(id,name) VALUES(1,'Synthetic category');
INSERT INTO companies(id,name) VALUES(1,'Synthetic company');
INSERT INTO generic_formulas(id,name,name_key) VALUES(1,'Synthetic salt','synthetic salt');
INSERT INTO brands(id,name,name_key) VALUES(1,'Synthetic brand','synthetic brand');
INSERT INTO units(id,name,whole_only) VALUES(999,'syntheticunit',0);
INSERT INTO products(id,name,category_id,company_id,generic_formula_id,brand_id,generic,brand,base_unit,strength_value,strength_unit,cost_paisa,retail_paisa,created_at,updated_at)
VALUES(1,'Synthetic product',1,1,1,1,'Preserved salt text','Preserved brand text','syntheticunit',2,'syntheticunit',20000,30000,'2026-11-10','2026-11-10');
INSERT INTO product_category_tags(product_id,category_id) VALUES(1,1);
INSERT INTO batches(product_id,batch_no,qty_base,cost_paisa,retail_paisa,created_at) VALUES(1,'Synthetic batch',20,20000,30000,'2026-11-10');
""")
    before = list(db.iterdump())
    params = {'id':1,'at':'2026-11-10'}
    for query in (category_unlink,category_tags,company_unlink,formula_unlink,brand_unlink):
        db.execute(query,params)
    row = db.execute('SELECT * FROM products WHERE id=1').fetchone()
    assert all(row[col] is None for col in ('category_id','company_id','generic_formula_id','brand_id'))
    assert row['generic'] == 'Preserved salt text' and row['brand'] == 'Preserved brand text'
    assert not db.execute('SELECT * FROM product_category_tags').fetchall()
    db.rollback()
    assert list(db.iterdump()) == before
    args = {'id':999,'at':'2026-11-10','old':'syntheticunit','clean':'newsyntheticunit','order':20,'wholeOnly':1}
    db.executescript("CREATE TRIGGER synthetic_unit_fault BEFORE UPDATE OF name ON units BEGIN SELECT RAISE(ABORT,'synthetic unit fault'); END;")
    try:
        with db:
            db.execute(base_rename,args); db.execute(strength_rename,args); db.execute(unit_rename,args)
        raise AssertionError('Expected SQL trigger fault')
    except sqlite3.IntegrityError:
        pass
    db.execute('DROP TRIGGER synthetic_unit_fault;')
    assert list(db.iterdump()) == before
    with db:
        db.execute(base_rename,args); db.execute(strength_rename,args); db.execute(unit_rename,args)
    row = db.execute('SELECT * FROM products WHERE id=1').fetchone()
    assert row['base_unit'] == row['strength_unit'] == 'newsyntheticunit'
    assert row['cost_paisa'] == 20000 and row['retail_paisa'] == 30000
    assert db.execute('SELECT qty_base FROM batches WHERE product_id=1').fetchone()[0] == 20
    assert not db.execute('PRAGMA foreign_key_check').fetchall()
    print(f'PASS {len(migrations)} migrations; actual category/tag/company/salt/brand unlink SQL, typed text preservation, unit base/strength rename rollback and success, unchanged batch quantity/prices; NOT C# permissions/Database.Tx/audit/WPF proof')


if __name__ == '__main__':
    run()
