# Phase4 portion2 — Settings / direct Core access and profit projections

**v0.80.0 Ibis, 2026-11-10. IN PROGRESS, not a comprehensive audit/security PASS.**

This extends `POS-PHASE4-EVIDENCE.md`; that register's26 routes/eight Settings contents/36 lessons remain the inventory. Source review below is method-specific, not a claim that every method in a named service was reviewed. No .NET SDK, WPF or physical hardware execution here.

## Bounded findings fixed

1. `ShellViewModel.BuildSettingsTabs` lacked index0 App name & icon although SettingsView is headerless. Add its section without renumbering Shop1…Fields7. Separately `SettingsViewModel.SelectedTabIndex` clamped to6, so ShowTab(7) selected App words instead of Fields. Clamp to7; all eight existing content controls remain. This is source repair, not Windows accessibility proof.
2. `NativeFieldPolicyService.List/SetRequired` trusted supplied owner role text. Read current active owner with `OwnerAccess.Require`; SetRequired does so inside the write transaction **before** no-op success. Critical-risk confirmation stays mandatory, final-post validation unchanged. Acknowledgement and audit use current DB name, not supplied display name. Existing fixture assertions now expect TestData.SeedOwnerName (`admin`) rather than fabricated `Owner`; this is an intentional current-identity assertion, not weaker test coverage.
3. `RepService.ListDoctors` used only Modules.Profit, bypassing global `profit.showMargins`. It now uses `ProfitPolicy.CanSee` like its customer-profit method, Dashboard/Reports/ProductHistory. Denied rows have ProfitKnown=false and ProfitPaisa0; sales/counts remain available under Customers permission. No SQL or stored monetary values changed.
4. `ProductHistoryService.Load` returned raw row ProfitPaisa and aggregate even with CanSeeProfit=false. Mask denied row values before returning; existing UI/CSV/paper gates stay. Visible users get original snapshot-derived item profit; item figures remain BEFORE bill Extra, no allocation introduced.

**Limits:** masking an explicit profit property is not mathematical secrecy. Existing cost-bearing read paths and profit-based quotes can imply costs/profit; those remaining paths are listed below, not silently certified. The in-process `User` argument is not an authenticated security token; checking current owner by supplied id does not stop an arbitrary caller supplying the real owner id.

## Source access/projection matrix

Paths under `dotnet/Area11.Core/Services/` unless stated. F = focused fixed/reviewed behavior with written regressions, T = inspected trace/boundary, OPEN = known incomplete boundary. None denotes executed C# or penetration testing.

| Surface / exact method(s) | Entry gate / data returned / inspected evidence | Status / remaining |
|---|---|---|
| Desktop Shell.BuildSettingsTabs; Settings.SelectedTabIndex / ShowTab; SettingsView top-level contents | Eight exact indices0–7, existing header command selects same VM; no added permission | F source-only; WPF clicks, focus/DPI and unchanged typed state pending |
| NativeFieldPolicy.List | Owner-role check followed by live DB active-owner read before metadata returned | F; unknown/non-owner/inactive/demoted owner regressions written |
| NativeFieldPolicy.SetRequired | Live owner inside Tx before no-op, then required/critical/risk guards, UPDATE and audit; DB owner name | F; existing audit-trigger rollback test retained, actual C# run pending |
| Rep.ListDoctors | Customers gate; DoctorSql returns sales/count/profit; MaskProfit now uses shared global+permission policy | F; off switch tested for owner and explicit profit grantee; on again preserves sales |
| Rep.ListCustomerProfit | ProfitPolicy at entry; returns lifetime aggregate; no new gate introduced | T; service wording says owner but actual gate is permission+global; caller identity/stale-session matrix still OPEN |
| ProductHistory.Load | Products gate; stored purchase/sale snapshots; CanSeeProfit global+permission; raw sale profits now zero when denied | F for explicit profit fields ONLY; purchase costs still in separate purchase rows, see OPEN below |
| ProductHistory.BuildCsv / BuildDocument | Both call Load; profit column/summary already conditional; normal sold amounts remain before Extra | T + existing regression coverage inherited; ownerless identity/cost inference not certified |
| Dashboard.GetSnapshot | ProfitPolicy decides todayProfit calculation/flag; denied default0; customer credit and supplier payable aggregates also returned | T; all-field visibility matrix is OPEN, no blanket Dashboard privacy PASS |
| Report.Build / ReadTotals | Reports gate + ProfitPolicy; explicit profit/margin/net fields masked, top-profit list denied | T; CostPaisa/RestockCostPaisa still populated in returned totals even when profit denied; coordinated projection review OPEN |
| SaleHistory.Search / CanLook | Sales permission at entry; history route is not an owner-only feature | T; Open/reprint/receipt detail/cost fields and record-id matrix still pending |
| Auth.ListUsers | Selects only id/name/role/active (no secret hash); no actor argument; used by login/staff/shift callers | T; don't add a blind owner-only gate that breaks login; caller/context split remains OPEN |
| Permission.Can / MapFor | Owner role snapshot shortcut; failed DB read falls back to documented role defaults | OPEN; deliberate policy unchanged, explain/ask before changing DB-error selling behavior |
| PricingAllowance.ForUser / Save | Existing current active account/allowance reads; owner write DB validation from phase2 | T inherited, not a new all-service-session guarantee |
| Reorder.Report | Reorder permission at entry; result includes supplier/last purchase date and LastCostPaisa | OPEN cost-bearing result, even though current grid does not show last cost; compare owner/manager versus specially granted staff before coordinated change |
| CustomerLedger.List / Page | Legacy signatures do not accept actor; List includes raw ProfitPaisa; UI decides what to display | OPEN HIGH PRIORITY: direct-call/hidden profit boundary is incomplete; must migrate internal/caller/export contracts together, not claim covered by ProductHistory fix |
| SupplierLedger.List / Page / ExpiredBatches / Statement / BuildBulkStatements | Legacy actorless read/statement APIs expose supplier/ledger/cost data; Pay/Return have separate actor paths | OPEN HIGH PRIORITY: read caller contracts and unauthorized-id/export tests next portion; no whole supplier workflow PASS |
| Product.Search / SearchSmart / Get | Legacy read APIs lack actor; full Product model has costs used by receiving/pricing/editor callers | OPEN HIGH PRIORITY: separate safe catalog DTO/context from authorized cost reads; do not destroy needed Core cost calculation |
| CustomField.List(manage/includeInactive) | Owner role text plus Settings permission for management; ordinary form values use entity module | OPEN: current-owner validation not yet uniformly applied to custom definitions/answers |
| TimeCorrection.CanFix / ApplyCorrection | Owner role text; bill-time plan/read then write/audit changes reporting dates | OPEN: current-account/stale-plan transaction review next; no financial-time mutation changed this portion |

## What was NOT changed / policy decisions deferred

- No migration, no sale/return/purchase/payment/ledger posting calculation, no cost-floor exception, no reallocation of sale-level Extra. Approved B560 then540 remains.
- No new manager authority. Owner grants per-person permissions/limits; fields remain owner-only. No recovery-factory/login policy change.
- No generic authenticated-session or revocation framework introduced. All services are local in-process APIs, not a newly exposed web API.
- No deletion of duplicate/obsolete/compatibility features. No HasOpenDrawer error-policy or Permission read-default change.
- Actorless read APIs and cost projections are actual source gaps, not resolved by hiding a WPF column. They require coordinated next-portion changes and regression mapping; they are NOT treated as accepted permanent policy. Where a new business choice is required, explain an everyday example first, then ask the owner; don't silently deny a supported workflow.

## Fifteen-dimension delta / remaining checklist

- Dimensions7/8: native-field owner and raw profit boundaries partly fixed; above OPEN rows block overall completion.
- Dimension9: current-owner names for metadata audit; no session revocation proof.
- Dimension14: eight-section reachability/index fixes in source; actual Windows small-screen/keyboard/modal behavior pending.
- Dimension15: guide lessons18/28/31/32 and summary reconciled for these bounded changes, offline MD/HTML parity required; all36 action-by-action review still pending.
- Dimensions1–6 and12–13: prior financial protections retained, no new C# finance/concurrency/double-execution proof this portion.
- Dimensions10–11: import/files/SQL/HTML/archive/resource bounds and backup/cloud isolation/RLS drills remain pending. Prior photo filename and SQLite proofs do not certify these.

**Next portion:** prioritize legacy actorless customer/supplier/catalog reads and projection call sites, current-owner custom fields/time correction, then finance/stock/duplicate fault matrix, inputs/backup/cloud, full tutorial reconciliation and real Windows evidence. No installer authorized.

## Verification boundary

12 new facts in `PhaseFourAccessProjectionTests.cs`: seven native-field identity/no-op/acknowledgement cases, three Doctors switch/grant/restore cases, two raw ProductHistory masking/restore cases. Total1337 cases written, **C# NOT RUN**. UI navigation has a structural regression gate matching actual top-level XAML contents, shell indices and VM clamp; that is not an executed WPF test. Static and SQLite results are recorded after running, separately from C#.

### Executed results — this portion

- `/home/user/.venv/bin/python tools/check_repo.py`: **68 checks PASS**;369 C# files grammar-parsed,3686 calls/6583 member references resolved,0 reported problems. Source analysis, not compilation.
- Six negative source probes each rejected by the portion2 gate: omitted identity0 section, clamp6, omitted native read check, omitted native write check, Doctors permission-only masking, retained raw ProductHistory profit. All files restored; restored gate passed (**6 negative +1 restored**). Not executed C# security tests.
- Main guide's generated HTML main equals Markdown rendering exactly;36 unique lesson anchors, no remote script/style/image assets.
- `tools/check_extra_discount_sqlite.py`: **PASS** all36 actual migration scripts, historical columns unchanged, actual BookCheck reconciliation/corruption detection, B560/540 arithmetic and foreign keys. Synthetic memory-only data, not .NET posting or production data.
- `tools/build_rules_matrix.py --check`: up to date; `git diff --check`: clean.
- Core/xUnit1337, Desktop build/launch, WPF navigation/DPI/session/device tests **NOT RUN**; no dotnet executable available. Previous Windows results do not prove this version.
