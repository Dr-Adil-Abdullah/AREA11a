#!/usr/bin/env python3
"""Independent SQLite migration/money proof. NOT a .NET/WPF or service-runtime test.
Uses only in-memory synthetic data; never opens the shop database.
"""
from pathlib import Path
import sqlite3

ROOT = Path(__file__).resolve().parents[1]
MIGRATIONS = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))


def run() -> None:
    c = sqlite3.connect(':memory:')
    c.execute('PRAGMA foreign_keys=ON')
    for path in MIGRATIONS:
        if path.name.startswith('036_'):
            break
        c.executescript(path.read_text())
    c.execute("INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic Owner','owner',1)")
    c.execute("INSERT INTO products(id,name,base_unit,cost_paisa,retail_paisa) VALUES(1,'Synthetic medicine','tablet',40000,56000)")
    c.execute("INSERT INTO batches(id,product_id,batch_no,qty_base,cost_paisa,retail_paisa) VALUES(1,1,'SYNTHETIC',100,40000,56000)")
    c.execute("INSERT INTO sales(id,code,user_id,subtotal_paisa,total_paisa,paid_paisa,due_paisa,status) VALUES(1,'SYN-OLD',1,112000,112000,112000,0,'paid')")
    for index in (1, 2):
        c.execute("""INSERT INTO sale_items(id,sale_id,product_id,batch_id,name_snapshot,qty_base,qty_entered,unit_sold,
                     unit_price_paisa,line_total_paisa,cost_paisa_at_sale,discount_paisa)
                     VALUES(?,1,1,1,'Synthetic medicine',1,1,'base',56000,56000,40000,0)""", (index,))
    c.execute("INSERT INTO user_pricing_allowances(user_id,edit_rate,manual_reduction_percent) VALUES(1,1,20)")
    # Compare EVERY old column, not only the few sample amounts named in the output.
    old_sale = c.execute('SELECT * FROM sales').fetchall()
    old_items = c.execute('SELECT * FROM sale_items ORDER BY id').fetchall()
    old_grants = c.execute('SELECT * FROM user_pricing_allowances').fetchall()
    sale_cols = [x[1] for x in c.execute('PRAGMA table_info(sales)')]
    item_cols = [x[1] for x in c.execute('PRAGMA table_info(sale_items)')]
    grant_cols = [x[1] for x in c.execute('PRAGMA table_info(user_pricing_allowances)')]
    c.executescript(next(x for x in MIGRATIONS if x.name.startswith('036_')).read_text())
    assert c.execute('SELECT ' + ','.join(sale_cols) + ' FROM sales').fetchall() == old_sale
    assert c.execute('SELECT ' + ','.join(item_cols) + ' FROM sale_items ORDER BY id').fetchall() == old_items
    assert c.execute('SELECT ' + ','.join(grant_cols) + ' FROM user_pricing_allowances').fetchall() == old_grants
    assert c.execute('SELECT pre_extra_total_paisa,extra_discount_paisa FROM sales').fetchone() == (None, 0)
    assert c.execute('SELECT extra_discount,extra_limit_paisa FROM user_pricing_allowances').fetchone() == (0, 0)
    print('PASS migration036: every historical sale/item/grant column unchanged; old original snapshot NULL, extra0; no automatic grant')
    # Synthetic new bill, as the service stores it: unchanged items, sale-only allowance.
    c.execute('UPDATE sales SET pre_extra_total_paisa=112000,extra_discount_paisa=2000,total_paisa=110000,paid_paisa=110000 WHERE id=1')
    assert c.execute('SELECT SUM(line_total_paisa) FROM sale_items').fetchone()[0] == 112000
    assert c.execute('SELECT SUM(unit_price_paisa),SUM(discount_paisa),SUM(qty_base) FROM sale_items').fetchone() == (112000, 0, 2.0)
    assert c.execute('SELECT SUM(line_total_paisa)-(SELECT extra_discount_paisa FROM sales) FROM sale_items').fetchone()[0] == 110000
    assert c.execute('SELECT total_paisa-(SELECT SUM(qty_base*cost_paisa_at_sale) FROM sale_items) FROM sales').fetchone()[0] == 30000
    # Execute the actual updated BookCheck SQL, rather than just retyping its equation.
    import re
    book = (ROOT / 'dotnet/Area11.Core/Services/BookCheckService.cs').read_text()
    for method in ('BillLines', 'BillHeader', 'BillImpossibleMoney'):
        body = book.split('private BookCheckRule ' + method + '(')[1].split('private BookCheckRule ', 1)[0]
        sql = re.search(r'_db.Query<CheckRow>\(connection, @"(.*?)"\)', body, re.S).group(1)
        assert c.execute(sql).fetchall() == [], method
    c.execute('UPDATE sales SET pre_extra_total_paisa=112001 WHERE id=1')
    assert c.execute(sql).fetchall(), 'tampered original/extra/final snapshot must be detected'
    c.execute('UPDATE sales SET pre_extra_total_paisa=112000 WHERE id=1')
    print('PASS actual BookCheck SQL: item sum minus extra, header, original/extra/final; deliberately corrupted snapshot detected')
    # Approved policy B, in integer paisa: same cap ReturnService uses. Actual C# still needs tests.
    total = 110000
    refunds = []
    for item_value in (56000, 56000):
        refunds.append(min(item_value, max(0, total - sum(refunds))))
    assert refunds == [56000, 54000] and sum(refunds) == total
    assert c.execute('PRAGMA foreign_key_check').fetchall() == []
    c.close()
    # Blank DB also receives every real script with foreign keys enabled.
    blank = sqlite3.connect(':memory:')
    blank.execute('PRAGMA foreign_keys=ON')
    for path in MIGRATIONS:
        blank.executescript(path.read_text())
    assert blank.execute('PRAGMA foreign_key_check').fetchall() == []
    blank.close()
    print(f'PASS {len(MIGRATIONS)} actual scripts on blank SQLite; policy-B arithmetic 560/540; foreign keys clean')
    print('NOT RUN: .NET SaleService/ReturnService tests, Windows build, WPF, printer or shop data')


if __name__ == '__main__':
    run()
