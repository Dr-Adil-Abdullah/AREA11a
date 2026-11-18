#!/usr/bin/env python3
"""Portion28 lexical negative checks only; not executable C#/WPF mutation tests.
Run alone: temporarily mutate a single token and always restore exact original bytes.
"""
from pathlib import Path
import re
import check_repo as checks

ROOT = Path(__file__).resolve().parents[1]
P = ROOT / 'dotnet/Area11.Core/Services/ProductService.cs'
C = ROOT / 'dotnet/Area11.Desktop/ViewModels/CatalogWindowViewModel.cs'
U = ROOT / 'dotnet/Area11.Desktop/ViewModels/UnitWindowViewModel.cs'
W = ROOT / 'dotnet/Area11.Desktop/Views/CatalogWindow.xaml.cs'
Z = ROOT / 'dotnet/Area11.Desktop/Views/UnitWindow.xaml.cs'
T = ROOT / 'dotnet/Area11.Tests/CatalogCurrentAccessTests.cs'


def run():
    original = {p: p.read_bytes() for p in (P,C,U,W,Z,T)}
    cases = []
    source = original[P].decode()
    for method in ('AddCategory','UpdateCategory','SetCategoryActive','AddCompany','UpdateCompany','SetCompanyActive',
                   'AddFormula','AddBrand','UpdateFormula','UpdateBrand','SetFormulaActive','SetBrandActive',
                   'SetBrandCompany','AddUnit','UpdateUnit','SetUnitActive'):
        match = re.search(r'    public ServiceResult(?:<int>)? ' + method + r'\(', source)
        body = source[match.start():].split('\n    public ',1)[0]
        for old,new,label in (
            ('actor = RequireProductWriter(connection, actor);','','stored writer'),
            ('actor is not { Active: true } || actor.Id <= 0 || !actor.CanManage','!actor.CanManage','active actor')):
            assert old in body
            cases.append((P,body,body.replace(old,new,1),method+' '+label+' removed'))
    cases.extend([
        (P,'ServiceResult<bool>.Success(reader.CanManage)','ServiceResult<bool>.Success(true)','reader silently promoted to manager'),
        (C,'void SaveCategory()\n    {\n        if (!RequireEdit()) return;','void SaveCategory()\n    {','category UI action preflight removed'),
        (C,'void ClearBrandCompany()\n    {\n        if (!RequireEdit()) return;','void ClearBrandCompany()\n    {','brand company clear preflight removed'),
        (U,'void Save()\n    {\n        if (!RequireEdit()) return;','void Save()\n    {','unit UI action preflight removed'),
        (C,'CanEdit = _lastLoadSucceeded && access.Value;','CanEdit = access.Value;','catalog regrant restores stale editor'),
        (U,'CanEdit = _lastLoadSucceeded && access.Value;','CanEdit = access.Value;','unit regrant restores stale editor'),
        (C,'CategoryName = CompanyName = FormulaName = BrandName = string.Empty;','','catalog authored names retained'),
        (C,'SelectedBrandCompany = null;\n            _brandCompanyId = null;','SelectedBrandCompany = null;','brand company target retained'),
        (U,'Name = OrderText = string.Empty;','','unit authored inputs retained'),
        (U,'            WholeOnly = false;\n            OnPropertyChanged(nameof(HasNoUnits));','            OnPropertyChanged(nameof(HasNoUnits));','unit quantity tick retained'),
        (C,'_lastLoadSucceeded && Categories.Count == 0;','Categories.Count == 0;','failed catalog read certified empty'),
        (C,'SAVED. The catalog change was stored, but could not reload.','Reload failed.','catalog committed result ambiguous'),
        (U,'SAVED. The unit change was stored, but could not reload.','Reload failed.','unit committed result ambiguous'),
        (C,'brandId is null || SelectedBrand?.Id == brandId','true','catalog refresh accepted wrong saved target'),
        (W,'Closed += (_, _) => accessTimer.Stop();','','catalog timer not stopped'),
        (Z,'Closed += (_, _) => accessTimer.Stop();','','unit timer not stopped'),
        (T,'[Fact] public void CatalogReaderKeepsReadOnlyProductsAccessWithoutCostOrMain()','public void CatalogReaderKeepsReadOnlyProductsAccessWithoutCostOrMain()','reader regression unregistered'),
    ])
    try:
        checks.check_catalog_current_boundaries()
        assert not checks.FAILURES, checks.FAILURES
        for path,old,new,label in cases:
            text = original[path].decode()
            assert old in text,label
            path.write_text(text.replace(old,new,1))
            checks.FAILURES.clear(); checks.REPORT.clear()
            checks.check_catalog_current_boundaries()
            assert checks.FAILURES,'Survived: '+label
            print('REJECT',label)
            path.write_bytes(original[path])
    finally:
        for path,content in original.items(): path.write_bytes(content)
    assert all(p.read_bytes()==content for p,content in original.items())
    print(f'PASS {len(cases)} lexical source-negative checks rejected; exact bytes restored; NOT C#/WPF execution')


if __name__ == '__main__':
    run()
