#!/usr/bin/env python3
"""Isolated BookCheck SQL regression for the owner's v0.80.0 Windows report.
Actual source-extracted SQL on synthetic in-memory data, NOT .NET/xUnit execution.
No arguments or shop-data path; never opens a production database.
"""
from pathlib import Path
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[1]


def run() -> None:
    c = sqlite3.connect(':memory:')
    c.execute('PRAGMA foreign_keys=ON')
    migrations = sorted((ROOT / 'dotnet/Area11.Core/Data/Migrations').glob('*.sql'))
    for migration in migrations:
        c.executescript(migration.read_text())
    c.execute("INSERT INTO users(id,name,role,active) VALUES(1,'Synthetic owner','owner',1)")
    c.execute("INSERT INTO products(id,name,base_unit) VALUES(1,'Synthetic medicine','tablet')")
    c.execute("INSERT INTO batches(id,product_id,batch_no,qty_base,cost_paisa,retail_paisa) VALUES(1,1,'SYNTHETIC',100,20000,30000)")
    for sale_id in (1, 2):
        c.execute("""INSERT INTO sales(id,code,user_id,subtotal_paisa,total_paisa,paid_paisa,due_paisa,status,
                                      pre_extra_total_paisa,extra_discount_paisa)
                     VALUES(?,?,1,30000,30000,30000,0,'paid',30000,0)""", (sale_id, 'SYN-' + str(sale_id)))
        c.execute("""INSERT INTO sale_items(sale_id,product_id,batch_id,name_snapshot,qty_base,qty_entered,
                         unit_sold,unit_price_paisa,line_total_paisa,cost_paisa_at_sale,discount_paisa)
                     VALUES(?,1,1,'Synthetic medicine',1,1,'base',30000,30000,20000,0)""", (sale_id,))
        c.execute("INSERT INTO payments(sale_id,method,amount_paisa) VALUES(?,'cash',30000)", (sale_id,))

    source = (ROOT / 'dotnet/Area11.Core/Services/BookCheckService.cs').read_text()
    queries = {}
    for name in ('BillLines', 'BillHeader', 'BillPaidAndDue', 'BillMoneyRows', 'BillImpossibleMoney'):
        body = source.split('private BookCheckRule ' + name + '(', 1)[1].split('private BookCheckRule ', 1)[0]
        queries[name] = re.search(r'_db.Query<CheckRow>\(connection, @"(.*?)"\)', body, re.S)[1]

    def findings():
        return {name: c.execute(query).fetchall() for name, query in queries.items()}

    def worst_money(rows):
        # Independent model of BookCheckReport.WorstPerDocument; not C# execution.
        worst = {}
        for group in rows.values():
            for code, expected, found in group:
                gap = expected - found
                if code not in worst or abs(gap) > abs(worst[code]):
                    worst[code] = gap
        return (sum(max(gap, 0) for gap in worst.values()),
                sum(max(-gap, 0) for gap in worst.values()))

    assert not any(findings().values())
    c.execute('UPDATE sales SET total_paisa=30500 WHERE id=1')
    rows = findings()
    assert [name for name, group in rows.items() if group] == [
        'BillLines', 'BillHeader', 'BillPaidAndDue', 'BillImpossibleMoney']
    assert rows['BillImpossibleMoney'] == [('SYN-1', 30500, 30000)]
    assert sum(map(len, rows.values())) == 4  # CSV: header + four findings
    assert worst_money(rows) == (500, 0)
    c.execute('UPDATE sales SET total_paisa=29700 WHERE id=2')
    assert worst_money(findings()) == (500, 300)
    print('PASS actual SQL: new snapshot bills have four findings/five CSV rows; Rs5 missing and Rs3 excess stay separate, not whole-bill faults')

    c.execute('UPDATE sales SET total_paisa=30000')
    c.execute('UPDATE sales SET pre_extra_total_paisa=30100 WHERE id=1')
    rows = findings()
    assert sum(map(len, rows.values())) == 1
    assert rows['BillImpossibleMoney'] == [('SYN-1', 30000, 30100)]
    assert worst_money(rows) == (0, 100)
    print('PASS actual SQL: snapshot-only Rs1 mismatch reports Rs1, not the full Rs300 bill')

    c.execute('UPDATE sales SET pre_extra_total_paisa=NULL,total_paisa=30500 WHERE id=1')
    rows = findings()
    assert sum(map(len, rows.values())) == 3
    assert rows['BillImpossibleMoney'] == []
    assert worst_money(rows) == (500, 0)
    print('PASS actual SQL: legacy NULL snapshot retains three findings, no invented snapshot fault')

    c.execute('UPDATE sales SET pre_extra_total_paisa=30000,extra_discount_paisa=2000,total_paisa=28000,paid_paisa=28000 WHERE id=1')
    c.execute('UPDATE payments SET amount_paisa=28000 WHERE sale_id=1')
    assert not any(findings().values())
    assert c.execute('SELECT line_total_paisa FROM sale_items WHERE sale_id=1').fetchone() == (30000,)
    assert not c.execute('PRAGMA foreign_key_check').fetchall()
    print(f'PASS {len(migrations)} actual migrations: valid sale-level Extra reconciles without changing item values; FK clean')
    print('NOT RUN: C# BookCheckReport CSV/money grouping, PricingAllowanceService, xUnit, Windows/WPF or shop data')
    c.close()


if __name__ == '__main__':
    run()
