# Phase4 portion27 — stable5/6/7: current customer/product writers and stale editors

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**v0.105.0 Harbor · `#466B65` · 2026-11-10 · source checkpoint; Windows OPEN.**
Baseline `596222adce9161bd6326004438c488f993949f1a` (portion26).
[PT-47 Urdu](POS-PHASE4-PART27-URDU.md) · [Stable1–36 status](NUMBERED-WORK-STATUS-URDU.md).

## 1. Findings and bounded authorization

1. **5/6:** Customer Update only reached the current custom-answer writer when `customValues` was non-null. A null map therefore bypassed its current-account checks; the parent SaveCustomer trusted supplied CanManage for limits and supplied name for audit. Current Customers/same-role/active actor now resolves inside Tx **before** SaveCustomer, independent of optional answers. Cashier phone/name edits and Owner/Manager-only limit changes remain.
2. **5/6:** Product Update/Deactivate trusted supplied CanManage; Create's later custom writer did not express the parent Products workflow. All three reviewed writes now resolve current stored Products writer/manager before mutation/no-op and use that actor for audit. Missing/nonpositive update/hide targets fail. Authorized already-hidden hide is an idempotent no-audit success, not proof of general monetary idempotency.
3. **6/13:** Existing full editor already required CostKnown (Cost+main), but the direct Core stored-cost patch had no matching gate. A non-null CostPaisa patch now reads the main key on the current write connection and requires PurchaseCost. Missing key preserves shipped true default; numeric compatibility remains; malformed/error does not authorize cost writes. Profit alone is insufficient; no Profit grant required for cost-only. New-product authored-cost workflow and non-cost Core patches do **not** gain a new Cost requirement.
4. **7:** Customer Save used moving Selected.Id and retained authored identity/custom state after some resets. It now pins intended ID, rejects selection drift and clears the form/custom editor on cancel/selection/access loss; failed custom-field load aborts opening.
5. **7:** Product modal retained cost/form/lookups after current access changed. New preflight checks current writer/active target/existing Cost+main before opening/actions, after prompts and nominal3s while open. Denial clears form/custom state/rates/lookups/list, disables Save and latches invalidation until explicit reopen. Failed full Get and custom load also discard old state. Stable allowed ticks do not reload/overwrite authored text. Closed stops the timer.
6. **7/25:** Customer/Product committed save/hide with later reload exception/failure now says SAVED/do not repeat. Product list failure clears rows and is not a successful-empty overlay. This is UI result clarity, not a command token, external race fix or universal retry safety.

This is the previously announced bounded Next under items5/6/7, not a new business-policy decision. POS quick-customer creation, independent Cost/Profit rules, supplier exceptionB, accounting/pricing/Extra/return rules, backup passwordA and approved Blackbox navigation/BookCheck scope remain settled.

## 2. Implementation map

| File | Bounded change | Remaining boundary |
|---|---|---|
| `PartnerService.cs` | One stored current Customers actor resolution before SaveCustomer in Tx, including null custom map | Other methods/internal quick-create policies not changed |
| `ProductService.cs` | Create/Update/Deactivate supplied/current Products manager; stored audit; explicit hide target/no-op; fresh DB main+Cost stored-cost patch; CheckEditorAccess | Preflight read is not CAS; direct Update's pre-existing inactive-row behavior unchanged; full UI edit requires active |
| `CustomersViewModel.cs` | Intended ID pin; cleanup authored/edit/new custom state; postcommit reload verdict | Other customer commands and complete authenticated sessions remain separate |
| `ProductsViewModel.cs` | Current modal preflight, invalidation latch, clear form/lookups/cost/list; failed Get/extra-fields abort; SAVED reload/exception paths | Already-open independent history/barcode/photo windows not recalled; auxiliary catalog managers outside patch |
| `ProductEditorWindow.xaml(.cs)` | Save enabled binding, nominal3s Loaded timer/Closed stop | Actual WPF lifecycle/focus/dispatcher behavior UNRUN |
| `CustomerEditTests.cs` | Two role-only fixtures become persisted accounts, original assertions retained | No C# execution here |
| `RecordWriteCurrentAccessTests.cs` |36 real-schema facts | All UNRUN; photo bytes deliberately synthetic, not codec proof |
| `tools/check_repo.py` | New lexical boundary check; old product-editor check follows common postcommit Editor.Load(pinnedID) instead of old create-only refresh | Lexical expectations not compiler/control-flow proof |
| New Python probes | Actual migrated SQL exercise and30 scoped negative token checks with exact restoration | Not C# guard/Database.Tx/WPF/session execution |

## 3. Written C# regression inventory —36 facts, UNRUN

1. Anonymous denied.
2. Supplied inactive Owner denied.
3. Missing stored Owner denied.
4. Forged Owner over Cashier denied.
5. Stored inactive Owner denied.
6. Changed stored role denied.
7. Reactivated Owner fresh writes.
8. Revoked Customers denies null-map edit without mutation/audit.
9. Regranted Customers fresh edit.
10. Ordinary customer correction independent of financial grants/main.
11. Cashier phone allowed, limit change rejected without partial mutation.
12. Revoked Products denies Create/Update/Hide/no-op.
13. Regranted Products non-cost patch.
14. Products grant does not promote Cashier.
15. Missing customer edit no write/audit.
16. Missing/nonpositive product update/hide no audit.
17. Already-hidden authorized idempotent hide; current actor still required.
18. Authorized empty product patch no audit.
19. All reviewed audit actions use stored current name (including price change).
20. Cost-denied patch does not partially rename.
21. Non-cost Core patch preserved without Cost.
22. Profit-only cannot edit cost.
23. Cost without Profit can edit cost.
24. MainOFF blocks stored cost even Owner.
25. Fresh main read ignores deliberately stale injected settings cache.
26. Malformed main denies stored cost.
27. Missing main row retains shipped default.
28. Numeric0/1 compatibility.
29. Authored new-product cost with no stored-Cost grant/mainOFF preserved.
30. Existing editor requires active positive target andCost.
31. New editor still requires current Products manager.
32. Customer audit failure rolls back null-map edit.
33. Product edit audit failure rolls back tags/name/cost.
34. Product create audit failure rolls back parent/photo and only its new copy, preserving pre-existing file/source.
35. Put-away audit failure keeps active product.
36. Permission-table read error denies manager writes/preflight.

Total suite source expectation: **2045 cases** (previous2009+36). Core/Tests/Desktop compile and all C# tests **UNRUN**: no .NET SDK/Windows/Inno here. No timing, provider, DPAPI, image codec, actual session, compiler or production-security PASS claim.

## 4. Executed evidence and preservation

- `.venv/bin/python tools/check_repo.py`: **94 checks PASS**, **416 C# grammar files**, **4592 calls /9329 member references**, **0 issues**. Real grammar/member source analysis, **not compilation**.
- All **18** `tools/check_*_sqlite.py` probes PASS on synthetic databases. Python backup reference: pinned vector, **6 chunk-boundary round trips /24 negative cases PASS**; not C# codec/service/DPAPI/new-PC execution.
- `tools/build_rules_matrix.py --check` PASS; checklist regenerated (51done/6in-progress featureboard is not a Phase4 safety percentage). MD/HTML parity/36 lesson anchors, stable1–36 tracker and Windows CRLF checks PASS; `git diff --check` PASS. Final same-branch publication hash is reported only after actual push/remote verification.
- `tools/check_record_write_source_negative.py`: **30 lexical token mutations rejected, exact original bytes restored**; run alone, not alongside edits/builds. Not executable C#/WPF mutation testing.
- Existing portion26 lexical suite: **32 rejected/exact restoration** after this patch.
- `tools/check_record_write_sqlite.py`: all36 migrations, actual bound customer SELECT/UPDATE, actor/parent/main/hide queries, missing/inactive targets, preserved customer balance, fresh raw main values, Python SQLite-trigger rollback. Does **not** execute C# permissions/parsing/Database.Tx/audit/file cleanup.
- Preservation versus baseline: **726 tracked paths retained**, **166 other Core service/data/model files byte-identical**. All old8 Partner and34 Product multiline literals exact. These are literal/source comparisons, not behavior proofs.
- Database/Permission/Auth/Audit/Custom/Native/Photo services and all migrations unchanged; no account/pricing/batch/sales/purchase/backup/Blackbox/BookCheck calculations rewritten. No real shop data or synthetic posting to a live shop.

## 5. Same-number leftovers and next gates

1. **5:** supplied ID/current same-role stored account is not authenticated login/session binding; other callers still need reconciliation.
2. **6:** remaining parent routes/workflow combinations and catalog managers; no expected-version/CAS or cross-editor conflict protocol. Direct Update retains old inactive/history behavior; UI full editor is active-only. Read/query/use can race, externally changed content is not merged.
3. **7:** real WPF timer/bindings/focus tests; other cached objects/dialogs/views. Nominal3s is not instant recall and may be delayed by UI work; output files/paper/clipboard cannot be recalled. Arbitrary user free text not classified as financial secrets.
4. **2/4/23–25:** all2045 C# cases and fresh builds/retest/fixes, actual audit/file faults, parallel/busy/commit/retry/restart/power-loss matrix. SAVED messages are not universal idempotency.
5. **26–29:** encrypted backup/restore/newPC/files/cloud and isolated integration.
6. **30–35:** actual Windows UI/hardware/startup/isolation/logs/screens, no fabricated evidence.
7. **36:** authorized single double-click test installer remains NOT BUILT, depends on the Windows/build/install safety gates. Full Phase4 OPEN.

Every portion requires docs plus same-current-branch commit/push and actual remote verification. Latest publication hash/clean state belongs in the final report, not an unverified promise in this document. The36 stable categories are not36 Next turns; any3–6 integrated-round estimate remains conditional, not an upper bound or deadline.
