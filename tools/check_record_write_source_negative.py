#!/usr/bin/env python3
"""Lexical negative checks for portion27. NOT executable C#/WPF mutation tests.
Temporarily change one source token at a time; always restore original bytes.
Run alone, not concurrently with builds or source edits.
"""
from pathlib import Path
import check_repo as checks

ROOT = Path(__file__).resolve().parents[1]
P = ROOT / 'dotnet/Area11.Core/Services/ProductService.cs'
C = ROOT / 'dotnet/Area11.Core/Services/PartnerService.cs'
V = ROOT / 'dotnet/Area11.Desktop/ViewModels/ProductsViewModel.cs'
U = ROOT / 'dotnet/Area11.Desktop/ViewModels/CustomersViewModel.cs'
W = ROOT / 'dotnet/Area11.Desktop/Views/ProductEditorWindow.xaml.cs'
X = ROOT / 'dotnet/Area11.Desktop/Views/ProductEditorWindow.xaml'
T = ROOT / 'dotnet/Area11.Tests/RecordWriteCurrentAccessTests.cs'


def run():
    cases = [
        (C, 'actor = SnapshotReadAccess.Require(_db, connection, new PermissionService(_db, _log, _audit), actor, Modules.Customers);', '', 'customer null-map stored writer removed'),
        (P, 'actor is not { Active: true } || actor.Id <= 0 || !actor.CanManage', '!actor.CanManage', 'supplied product actor inactive accepted'),
        (P, 'actor = RequireProductWriter(connection, actor);', '', 'product create stored writer/audit removed'),
        (P, 'Modules.Products);\n        if (!current.CanManage)', 'Modules.Pos);\n        if (!current.CanManage)', 'wrong product writer module'),
        (P, 'if (!current.CanManage)', 'if (false)', 'stored product role not checked'),
        (P, 'if (patch.CostPaisa is not null) RequireStoredCostEdit(connection, actor);', '', 'stored cost patch grant bypass'),
        (P, 'if (active is null) throw new ValidationException', 'if (false) throw new ValidationException', 'missing hide target accepted'),
        (P, 'if (Convert.ToInt64(active) == 0) return;', '', 'inactive hide invents audit'),
        (P, 'SELECT value FROM settings WHERE key = @key;', 'SELECT value FROM settings LIMIT 1;', 'fresh cost main key unbound'),
        (P, 'raw is null || (bool.TryParse(raw, out var flag) ? flag', 'raw is not null || (bool.TryParse(raw, out var flag) ? flag', 'malformed main authorizes cost'),
        (P, '!enabled || !_permissions.Can(writer, Modules.PurchaseCost)', '!enabled', 'cost grant omitted'),
        (P, 'RequireStoredCostEdit(connection, writer);', '', 'existing editor lacks cost preflight'),
        (P, 'if (active is null || Convert.ToInt64(active) == 0)', 'if (false)', 'inactive editor parent accepted'),
        (U, '_editingCustomerId = Selected.Id;', '_editingCustomerId = 0;', 'customer intended target not pinned'),
        (U, 'Id = _editingCustomerId,', 'Id = Selected!.Id,', 'customer save uses moving selection'),
        (U, 'if (!IsEditing || _editingCustomerId <= 0 || Selected?.Id != _editingCustomerId)', 'if (Selected is null)', 'customer selection drift not refused'),
        (U, 'EditName = EditPhone = EditLimitText = string.Empty;', 'EditLimitText = string.Empty;', 'customer stale authored identity retained'),
        (U, 'NewEditor.Load(0);', '', 'customer new answers retained on denial'),
        (V, 'if (_editorAccessLost) return false;', '', 'regrant silently reuses invalidated editor'),
        (V, 'public bool CanSaveEditor => !_editorAccessLost;', 'public bool CanSaveEditor => true;', 'save remains enabled after denial'),
        (V, 'private void Save()\n    {\n        if (!RefreshEditorAccess()) return;', 'private void Save()\n    {', 'save current preflight omitted'),
        (V, 'private void OpenHistory()\n    {\n        if (!RefreshEditorAccess()) return;', 'private void OpenHistory()\n    {', 'history current preflight omitted'),
        (V, 'CostText = RetailText = VipText = DoctorText = string.Empty;', 'RetailText = string.Empty;', 'stale cost retained'),
        (V, '        Brands.Clear();\n        Suppliers.Clear();\n        SupplierFilterOptions.Clear();', '        Brands.Clear();\n        SupplierFilterOptions.Clear();', 'stale supplier lookups retained'),
        (V, '_lastProductLoadSucceeded && Items.Count == 0,', 'Items.Count == 0,', 'failed product read certified empty'),
        (V, 'SAVED. Product and extra fields were stored, but could not refresh.', 'Refresh failed.', 'product saved result ambiguous'),
        (V, 'InvalidateEditor(full.Error ??', 'SetStatus(full.Error ??', 'failed full product read retains old form'),
        (W, 'accessTimer.Stop();', '', 'closed modal timer not stopped'),
        (X, 'IsEnabled="{Binding CanSaveEditor}"', '', 'save button not disabled'),
        (T, '[Fact] public void AnonymousCannotUseReviewedRecordWrites()', 'public void AnonymousCannotUseReviewedRecordWrites()', 'regression unregistered'),
    ]
    original = {p: p.read_bytes() for p in {case[0] for case in cases}}
    try:
        checks.check_record_write_current_boundaries()
        assert not checks.FAILURES, checks.FAILURES
        for path, old, new, label in cases:
            source = original[path].decode()
            assert old in source, label
            path.write_text(source.replace(old, new, 1))
            checks.FAILURES.clear()
            checks.REPORT.clear()
            checks.check_record_write_current_boundaries()
            assert checks.FAILURES, 'Survived: ' + label
            print('REJECT', label)
            path.write_bytes(original[path])
    finally:
        for path, content in original.items():
            path.write_bytes(content)
    assert all(p.read_bytes() == content for p, content in original.items())
    print(f'PASS {len(cases)} lexical source-negative checks; exact bytes restored; NOT C#/WPF execution')


if __name__ == '__main__':
    run()
