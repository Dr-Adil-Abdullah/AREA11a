#!/usr/bin/env python3
"""Lexical mutations of the first part5 checkpoint;not C#/provider/Windows execution."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1]


def run():
    cases = []
    def add(path, *tokens):
        for token in tokens: cases.append((ROOT / 'dotnet' / path, token))
    add('Area11.Core/Data/Database.cs', 'PRAGMA query_only=ON;', 'PRAGMA query_only=OFF;',
        'connection.BeginTransaction(deferred: true)', 'connection.BeginTransaction(deferred: false)',
        '_readSnapshot || _transaction is null || !ReferenceEquals(_transaction.Connection, connection)',
        'finally { _transaction = null; _readSnapshot = false; }')
    add('Area11.Core/Services/EditConcurrency.cs', 'db.RequireWriteTransaction(connection);',
        'if (expected is null && !db.Sessions.Enabled) return;', '!ReferenceEquals(expected.Database, db)',
        'expected.Scope != scope || expected.Id != id',
        '!string.Equals(expected.Fingerprint, Capture(db, connection, scope, id).Fingerprint, StringComparison.Ordinal)',
        'id,name,phone,category,credit_limit_paisa,photo,active',
        'SELECT category_id FROM product_category_tags WHERE product_id=@id ORDER BY category_id;',
        'SELECT field_id,value,updated_at FROM custom_values WHERE entity=@entity AND entity_id=@id ORDER BY field_id;')
    for name, scope, key in [('Update','product','id'), ('UpdateCategory','category','id'), ('UpdateCompany','company','id'),
                              ('UpdateFormula','formula','id'), ('UpdateBrand','brand','id'), ('UpdateUnit','unit','id'),
                              ('SetBrandCompany','brand','brandId')]:
        add('Area11.Core/Services/ProductService.cs', f'EditConcurrency.Require(_db, connection, "{scope}", {key}, expected);')
    add('Area11.Core/Services/PartnerService.cs', 'EditConcurrency.Require(_db, connection, EditConcurrency.Customer, input.Id, expected);',
        "SELECT name FROM customers WHERE active=1 AND IFNULL(phone,'')=@phone LIMIT 1;")
    add('Area11.Core/Services/CustomFieldService.cs',
        'EditConcurrency.Require(_db, connection, EditConcurrency.CustomField, fieldId, expected);',
        'EditConcurrency.Require(_db, connection, "answers:" + key, entityId, expected);')
    add('Area11.Core/Services/NativeFieldPolicyService.cs', 'EditConcurrency.Require(_db, connection, EditConcurrency.NativeField, fieldId, expected);')
    for file in ['ProductService','PartnerService','NativeFieldPolicyService','CustomFieldService','CustomerLedgerService','SupplierLedgerService','SaleService']:
        add('Area11.Core/Services/' + file + '.cs', 'return _db.Snapshot(connection =>')
    for file in ['CostPolicy','ProfitPolicy']: add('Area11.Core/Services/' + file + '.cs', 'settings.CurrentFinancialVisibility()')
    add('Area11.Desktop/ViewModels/ProductsViewModel.cs', 'patch, answers, _editVersion)', 'Editor.LoadPrepared(full.Value.Record.Id, full.Value.Extra)')
    add('Area11.Desktop/ViewModels/CustomersViewModel.cs', 'EditEditor.AnswerValues(), _editVersion)')
    add('Area11.Desktop/ViewModels/CustomFieldsEditorViewModel.cs', 'values, _editVersion)')
    add('Area11.Desktop/ViewModels/NativeFieldsViewModel.cs', 'required, confirmed, field.EditVersion)')
    add('Area11.Desktop/ViewModels/UnitWindowViewModel.cs', 'WholeOnly, SelectedUnit.EditVersion)')
    add('Area11.Desktop/ViewModels/CatalogWindowViewModel.cs', 'SelectedCategory.EditVersion)', 'SelectedCompany.EditVersion)',
        'SelectedFormula.EditVersion)', 'SelectedBrand.EditVersion)', 'brandVersion = added.EditVersion;', 'brandVersion = updated.EditVersion;')
    originals = {path: path.read_bytes() for path, _ in cases}
    try:
        checks.check_edit_concurrency_boundaries(); assert not checks.FAILURES, checks.FAILURES
        for path, token in cases:
            text = originals[path].decode(); assert token in text, (path, token)
            path.write_text(text.replace(token, 'REMOVED_CONCURRENCY_CONTRACT'))
            checks.FAILURES.clear(); checks.REPORT.clear(); checks.check_edit_concurrency_boundaries()
            assert checks.FAILURES, 'Survived: ' + path.name + ': ' + token
            path.write_bytes(originals[path]); print('REJECT', path.name, token)
    finally:
        for path, raw in originals.items(): path.write_bytes(raw)
    assert all(path.read_bytes() == raw for path, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact bytes restored.NOT runtime concurrency proof.')


if __name__ == '__main__': run()
