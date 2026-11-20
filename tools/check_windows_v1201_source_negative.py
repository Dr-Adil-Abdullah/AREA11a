#!/usr/bin/env python3
"""Lexical fixture regression mutations;not Windows/.NET test execution."""
from pathlib import Path
import check_repo as checks
ROOT = Path(__file__).resolve().parents[1] / 'dotnet/Area11.Tests'


def run():
    cases = [
        ('CustomerCurrentAccessTests.cs', 'Grant(t, u, Modules.Customers, false);', ''),
        ('ReceiptTests.cs', 'Assert.True(test.Settings.Set("printer.width", 80, TestData.Owner).Ok);', 'test.Settings.Set("printer.width", 80, null);'),
        ('ReceiptTests.cs', 'Assert.True(test.Settings.Set("store.name", "Al-Shifa Pharmacy", TestData.Owner).Ok);', 'test.Settings.Set("store.name", "Al-Shifa Pharmacy", null);'),
        ('ReceiptTests.cs', 'Assert.True(test.Settings.Set("printer.copies", 2, TestData.Owner).Ok);', 'test.Settings.Set("printer.copies", 2, null);'),
        ('ReceiptTests.cs', 'Assert.True(test.Settings.Set("receipt.showDiscount", false, TestData.Owner).Ok);', 'test.Settings.Set("receipt.showDiscount", false, null);'),
        ('SplitPaymentTests.cs', 'Assert.True(test.Settings.Set("credit.blockOverLimit", true, TestData.Owner).Ok);', 'test.Settings.Set("credit.blockOverLimit", true, null);'),
        ('SplitPaymentTests.cs', 'Assert.False(sales.Create(again, manager).Ok);', ''),
        ('SplitPaymentTests.cs', 'Assert.True(byManager.Ok, byManager.Error);', ''),
        ('RecoveryTests.cs', 'Report(TestData.AddUser(test, "manager"), Today)', 'Report(new User { Id = 9, Role = "manager" }, Today)'),
        ('MasterFoundationTests.cs', 'Assert.Equal(TestData.SeedOwnerName, audit.UserName);', 'Assert.Equal("Owner", audit.UserName);'),
        ('PartnerTests.cs', 'Assert.Equal(before, StockOf(test, productId));', ''),
        ('SupplierLedgerTests.cs', 'var cashier = TestData.Cashier(test);', 'var cashier = new User { Id = 99, Role = "cashier" };'),
        ('SupplierLedgerTests.cs', 'Assert.Equal(paymentCount,', 'Assert.NotEqual(paymentCount,'),
        ('CounterPostingIntegrityTests.cs', 'var sales = TestData.Sales(f.T);', ''),
        ('CounterPostingIntegrityTests.cs', 'Assert.False(sales.Create(f.Bill(), TestData.Owner).Ok);', 'Assert.False(TestData.Sales(f.T).Create(f.Bill(), TestData.Owner).Ok);'),
        ('AutomaticDraftPostingTests.cs', ",posted_at='2026-11-10 12:00:00'", ''),
        ('StockTests.cs', 'var manager = TestData.AddUser(test, "manager");', 'var manager = new User { Id = 3, Role = "manager" };'),
        ('ProvisionalTests.cs', 'var manager = TestData.AddUser(test, "manager");', 'var manager = new User { Id = 3, Role = "manager" };'),
        ('PurchasePreparationTests.cs', 'var drafts = f.Drafts;', 'var drafts = TestData.Drafts(t);'),
        ('ShiftHandoverTests.cs', 'Assert.Contains("permission", refused.Error!, StringComparison.OrdinalIgnoreCase);', 'Assert.Contains("cannot work the till", refused.Error!, StringComparison.OrdinalIgnoreCase);'),
        ('ShiftHandoverTests.cs', 'Assert.Contains("From: " + TestData.SeedOwnerName, text);', 'Assert.Contains("From: Owner", text);'),
        ('PricingPhaseTwoTests.cs', 'Assert.Contains("account has changed", quote.Error ?? string.Empty);', 'Assert.Contains("role has changed", quote.Error ?? string.Empty);')]
    originals = {ROOT / name: (ROOT / name).read_bytes() for name, _, _ in cases}
    try:
        checks.check_windows_v1201_regressions(); assert not checks.FAILURES, checks.FAILURES
        for name, old, new in cases:
            p = ROOT / name; text = originals[p].decode(); assert old in text, (name, old)
            p.write_text(text.replace(old, new)); checks.FAILURES.clear(); checks.REPORT.clear()
            checks.check_windows_v1201_regressions(); assert checks.FAILURES, 'Survived ' + name + ':' + old
            p.write_bytes(originals[p]); print('REJECT', name, old)
    finally:
        for p, raw in originals.items(): p.write_bytes(raw)
    assert all(p.read_bytes() == raw for p, raw in originals.items())
    print(f'PASS {len(cases)} lexical mutations rejected;exact bytes restored.NOT C#/Windows execution.')


if __name__ == '__main__': run()
