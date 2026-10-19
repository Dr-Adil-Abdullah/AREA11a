# PORT-PLAN — Area11 WPF rebuild (Step 0 deliverable)

> **Purpose.** This is the `PORT-PLAN.md` required by the Master Spec §34.2 (Step 0 — read first, write no
> code, produce the plan, wait for the owner's approval). It records what was studied, what already exists
> in this repository, every conflict found between the existing prototype and the Specification, the exact
> port order, and the questions that need the owner's answer.
>
> **Status:** study complete · **database ported and verified** · architecture question pending (Q-01).
> **Date:** 2026-10-19 · **Repo:** `Dr-Adil-Abdullah/AREA11a` (branch `arena/a8a3b2e0-area11a`)
> **Owner's decisions live in [`OWNER-DECISIONS.md`](OWNER-DECISIONS.md)** — this file is the technical plan.

---

## 1. What was studied

| Source | Size | Used for |
|---|---|---|
| `AREA11-MASTER-SPEC.md` (owner-supplied) | 2,108 lines · 37 sections + Appendix A | The rebuild blueprint (authority) |
| `Dr-Adil-Abdullah/Area11` → `HANDOFF.md` | 370 lines | Live status, gotchas, decisions |
| `…/PHASE-TWO-INFORMATION.md` | 240 lines | 136 numbered rules (U/R/E/S/P/B/Z/T/M/Q) |
| `…/docs/APP-TAFSEEL.md` | 663 lines | Full feature + Settings catalogue |
| `…/INPUT-INFORMATION.md` | 477 lines | A-01…A-49, Q-01…Q-19 |
| `…/src/lib/schema.ts` | 596 lines | **Exact schema of migrations 001–013** (readable; code is parked in comments) |
| `…/src/lib/settings.ts` | — | **Exact 70 setting keys** |
| `…/src/lib/*.test.ts` | 5 files | **55 tests**: auth 7 · backup 5 · reports 10 · shift-math 4 · spec-1-2-3 29 |

Because `schema.ts` and `settings.ts` were readable in the reference repository, the two biggest unknowns of
the Specification (Section 37.2: “exact column list” and “the remaining setting keys”) are **no longer gaps**.
The rebuild can use the original schema verbatim, which is what §5.2.1 requires.

---

## 2. Target architecture (Spec §4.3 / §4.4) — the standard the rebuild must meet

```
Area11.sln
├── src/
│   ├── Area11.Core/            class library, NO UI references
│   │   ├── Models/             plain DTOs (serializable, no DB rows)
│   │   ├── Data/               Microsoft.Data.Sqlite + Dapper, migrations 001–013 verbatim
│   │   ├── Services/           one interface per area (IProductService, ISaleService, …)
│   │   ├── Services/Impl/      SQLite implementations (today) — HTTP implementation later
│   │   └── Utils/              money, dates, hashing (PBKDF2), numbering, zip
│   ├── Area11.Desktop/         WPF (.NET 10, MVVM, CommunityToolkit.Mvvm, WPF-UI)
│   │   ├── Views/  ViewModels/  Controls/  Resources/
│   │   └── must NOT reference Microsoft.Data.Sqlite  ← architecture test
│   └── Area11.Tests/           xUnit — 55 ported tests + acceptance scenarios
└── docs/PORT-PLAN.md           this file
```

Non-negotiables taken from the Specification:

1. Business rules live in **Core services**, never in ViewModels or XAML code-behind (§4.4.2, §4.4.8).
2. Every service method takes/returns **DTOs** and the current user (§4.4.4).
3. Sale, stock deduction, return, shift close = **one transactional call** (§4.4.5) so a second counter
   can never double-sell.
4. Schema = **exact original** `001_core … 013_quarantine_tracking` (§4.3.3, §5.2.1).
5. Money = integer paisa; quantities = base units; times = local; never delete (§5.1).
6. Role checks in Core, not only in the UI (§4.4.8).
7. Zero build warnings; tests green; tag `net-stage-N`; update handoff (§4.5, §34.4).

---

## 3. What exists today in THIS repository (and how it measures up)

The earlier turns produced a working prototype in `Area11.WPF/` (single project, .NET 8, no Dapper, no MVVM
toolkit, custom simplified schema). It proved the flow end-to-end but it **does not meet §4.3/§4.4**:

| Requirement (Spec) | Prototype today | Verdict |
|---|---|---|
| Core / Desktop / Tests split | one project | ✘ must be restructured |
| .NET 10 LTS | .NET 8 | ✘ |
| Dapper | raw ADO | ✘ |
| CommunityToolkit.Mvvm | hand-written base classes | ✘ |
| Exact schema 001–013 (old `area11.db` opens) | own simplified schema | ✘ highest risk |
| 70 setting keys, JSON-typed values | 45 keys, plain strings | ✘ |
| 55 tests ported first | none | ✘ |
| Silent ESC/POS printing | not built | ✘ |
| Everything else (POS, purchases, audit) | working prototype | ✔ logic reusable |

**Conclusion:** the prototype is a **reference/behaviour proof**, not the product. The rebuild replaces it with
the target solution; the logic already written (FEFO, discount, round-off with cost guard, numbering, audit
diff) is carried over into `Area11.Core`.

> Nothing in `WPFSample/` or `Area11.WPF/` is deleted until the owner answers **Q-03**.

---

## 4. Conflicts and gaps — status after 19 Oct 2026

Resolved by the owner (see `OWNER-DECISIONS.md`):

| # | Decision |
|---|---|
| C-02 | **Schema = original 001–013 verbatim** → done: 14 migration files in `dotnet/Area11.Core/Data/Migrations/`, all executed in order, 31 tables generated |
| C-04 | **Cashier return → quarantine**, persistent alert until the owner writes a reason; owner/manager returns restock immediately (setting `returns.cashierRestockToShelf`, default OFF) |
| C-05 | **Per-person permissions** edited by the owner in Settings → Staff (migration 014) |
| C-06 | Unfinished features → **Phase 5**, documented, nothing dropped |
| C-09 | Start clean by default |
| C-10 | Receipt text fully editable; Urdu lines rendered as graphics |
| C-15 | Loyalty at the very end (Phase 6) |

Still open: **C-01** (architecture), **C-03** (location), **C-07/C-11** (printing + installer), **C-13** (.NET version).

### Original conflict list (kept for the record)

These are the issues I cannot resolve from the documents alone. Each has a recommended default in §8.

| # | Issue | Why it matters |
|---|---|---|
| C-01 | Spec says .NET 10 + Dapper + CommunityToolkit + WPF-UI; prototype is .NET 8, raw ADO, hand-rolled MVVM | Whole solution layout |
| C-02 | Spec §5.2.1: the rebuilt schema must open an existing `area11.db` unchanged | Owner's real data; must port schema verbatim |
| C-03 | Where the new solution lives (`dotnet/` per §34.2 vs `Area11.WPF/`) and whether the prototype is replaced | Git history / folder hygiene |
| C-04 | §35.1 cashier return → restock: U-32 says “straight to stock”, stage-16 said “quarantine until owner” | Money + stock behaviour |
| C-05 | §35.4 audit log / net profit: Spec = owner only; original = owner+manager | Privacy of profit figures |
| C-06 | §33.2 items marked ❌ (lifetime profit, doctor tracking, reps, multi-barcode, pack-size search, 3-tier reorder, all-time best/worst day, vendor-return cutoff, generic alternative, customer statement, collection receipt, tax report, auto-send backup, archival, multi-branch) — first release or later? | Scope / delivery time |
| C-07 | Printer: silent ESC/POS needs raw printing; which route? | Hardware |
| C-08 | Camera capture in WPF (the web version used `getUserMedia`) | Product/customer photos |
| C-09 | Old data migration: does the owner already have a live `area11.db` to import, or start clean? | First-run experience |
| C-10 | Urdu on receipts as graphics (§4.3.5) — is it needed from day one? | Printing complexity |
| C-11 | Installer format (MSI vs portable self-contained folder) | Shop deployment |
| C-12 | Second counter / server: Spec says later — confirm nothing is built now | Scope |
| C-13 | `.NET 10` runtime availability on the shop PC | Deployment risk |
| C-14 | Prototype disposal: delete, archive in a branch, or keep beside the new solution | Repo clarity |
| C-15 | Loyalty defaults (§35.5): 1 point per Rs 100, VIP at 5-star threshold, feature OFF | Config defaults |

---

## 5. Exact schema source — **PORTED ✅**

Thirteen migrations, confirmed present in `src/lib/schema.ts`:

```
001_core · 002_returns · 003_change_and_stock · 004_provisional_returns
005_custom_fields_photos · 006_product_room · 007_supplier_returns
008_stock_takes · 009_shift_handover · 010_audit_old_new
011_alert_dismissals · 012_return_number · 013_quarantine_tracking
```

Tables (26 known): `settings`, `users`, `categories`, `companies`, `suppliers`, `customers`, `products`,
`batches`, `purchases`, `purchase_items`, `sales`, `sale_items`, `payments`, `stock_movements`, `shifts`,
`expenses`, `owner_drawings`, `audit_logs`, `sale_returns`, `stock_adjustments`, `provisional_returns`,
`provisional_items`, `custom_fields`, `custom_values`, `supplier_returns`, `supplier_return_items`,
`stock_takes`, `stock_take_items`, `alert_dismissals`, `_migrations`.

Notable original conventions that must be preserved (from `schema.ts`):

- `products`: `rack_no`, `pack_size_label`, `base_unit`, `box_strips`, `strip_tablets`, `track_expiry`,
  `reorder_level REAL`.
- `batches`: `expiry_date` **and** `expiry_ym` (fast filter), `qty_base REAL`, `is_sample`, `supplier_id`.
- `sales`: `date` (not `created_at` for the business date), `round_off_paisa`, `status`
  `paid|partial|credit|hold|void`, `payment_method` incl. `split`.
- `sale_items`: `name_snapshot`, `qty_entered`, `unit_sold`, `cost_paisa_at_sale`.
- `settings`: `key`, `value`, `updated_at` — values are **JSON**, type preserved.
- `payments`: `ref`, `user_id`, signed rows (purchase/supplier = out, refunds negative).

**Done:** each `sql` string was extracted verbatim into `dotnet/Area11.Core/Data/Migrations/<id>.sql`
(514 lines of original SQL), plus `014_user_permissions.sql` for the owner's per-person permissions.
All 14 were executed in order against SQLite: **31 tables, no error**. The column list of every table is
published in [`SCHEMA-REFERENCE.md`](SCHEMA-REFERENCE.md).

Two facts found while porting that the C# code must respect:

1. `qty_base` is **REAL** in the original (not integer) — the Core must use `decimal`, not `int`.
2. Names differ from the prototype: `sales.date` + `sales.user_id` + `sales.notes`,
   `sale_items.cost_paisa_at_sale`, `batches.expiry_date` + `expiry_ym`, `shifts.status`.
   The prototype's SQL must not be copied blindly.

---

## 6. Module plan (Core services → screens → tests)

| # | Module | Core service(s) | Tables | Rules / Spec | Tests to port |
|---|---|---|---|---|---|
| 1 | Schema + settings + audit | `IDatabase`, `ISettingsService`, `IAuditService` | settings, _migrations, audit_logs | §5, §6, §22, E-01, S-07 | audit diff (4), settings |
| 2 | Auth + roles | `IAuthService`, `ISession` | users, audit_logs | §7, Q-13, U-27 | auth (7) |
| 3 | Catalog | `IProductService`, `ICategoryService`, `ICustomFieldService` | products, categories, companies, custom_* | §8, U-17/18/21/22 | product merge |
| 4 | Suppliers + purchases | `ISupplierService`, `IPurchaseService` | suppliers, purchases, purchase_items, batches, stock_movements | §9, §10 | — |
| 5 | POS / sales | `ISaleService`, `INumberingService` | sales, sale_items, payments, batches, stock_movements | §11, §12, §13 | money + reconcile (10) |
| 6 | Receipts + printing | `IReceiptService`, `IPrintService` | (read) | §14 | — |
| 7 | Returns / provisional / quarantine / alerts | `IReturnService`, `IQuarantineService`, `IAlertService` | sale_returns, return items, provisional_*, alert_dismissals | §15, §16, U-28…U-35 | spec-1-2-3 (29) |
| 8 | Customers / credit / loyalty | `ICustomerService` | customers, payments | §17 | — |
| 9 | Cash / shifts / handover | `ICashService` | shifts, expenses, owner_drawings, payments | §18 | shift-math (4) |
| 10 | Stock write-off / stock-take | `IStockService` | stock_adjustments, stock_takes, stock_take_items | §19 | — |
| 11 | Reports | `IReportService` | (read) | §21, report-math | reports (10) |
| 12 | Audit viewer / backup / restore | `IAuditQueryService`, `IBackupService` | audit_logs, zip | §22, §23 | backup (5) |
| 13 | Excel import / export / bulk update | `IImportExportService` | all | §24 | — |
| 14 | Items marked ❌ in §33.2 | per feature | — | §33.2 | — |

Total ported tests: **55**, then new tests per module + the 22 acceptance scenarios of §31 as integration tests.

---

## 7. Port order and milestones (Spec §34.5)

| Stage | Deliverable | Gate |
|---|---|---|
| `net-stage-0` | This plan approved | owner OK |
| `net-stage-1` | Solution skeleton, Core/Desktop/Tests, migrations 001–013, settings, audit | 0 warnings, audit tests green |
| `net-stage-2` | Auth, roles, lock screen, staff management | 7 auth tests green |
| `net-stage-3` | Products, categories, companies, custom fields, photos | merge test green |
| `net-stage-4` | Suppliers, purchases (`PINV`), batches, supplier ledger | acceptance 31.20 |
| `net-stage-5` | POS (`INV`), FEFO, discount guard, round-off + cost guard | 31.1, 31.18 |
| `net-stage-6` | Receipts + silent printing (58/80), handover slip | 31.22 |
| `net-stage-7` | Returns `RET`, provisional `PR`, quarantine `Q`, alerts | 31.3–31.8 (29 tests) |
| `net-stage-8` | Customers, credit limit, split payment, loyalty | 31.10 |
| `net-stage-9` | Cash, shifts, handover, expenses, drawings, day-end | 31.11 |
| `net-stage-10` | Write-off, stock-take `ST` | 31.12 |
| `net-stage-11` | Reports, WhatsApp text | 31.9, 31.14 |
| `net-stage-12` | Audit viewer, backup/restore (zip + photos) | 31.16 |
| `net-stage-13` | Excel import / bulk update | 31.13 |
| `net-stage-14` | §33.2 gaps (as approved) + hardware test | owner sign-off |

The 14 stages are grouped into the **6 owner-facing phases** of `OWNER-DECISIONS.md` §5 so a working,
tested build can be shown after each phase (D-15).

Each stage: **0 warnings → all tests green → run the app → commit → tag → update `dotnet/HANDOFF.md`**.

---

## 8. Recommended defaults (used if the owner does not override)

| # | Question | Default |
|---|---|---|
| Q-01 | Architecture | Follow Spec §4.3/§4.4 exactly (Core/Desktop/Tests, .NET 10, Dapper, CommunityToolkit.Mvvm) |
| Q-02 | Schema | Port migrations 001–013 **verbatim**; existing `area11.db` opens unchanged |
| Q-03 | Location | New `dotnet/` folder; prototype archived, not deleted, until the new build runs |
| Q-04 | Cashier restock | Setting `returns.cashierRestockToShelf` = **ON** (follows U-32) |
| Q-05 | Audit / net profit | Permission toggle; **owner-only** by default |
| Q-06 | §33.2 gaps | Build after the ported core is accepted (stage 14), not before |
| Q-07 | Printing | Own minimal ESC/POS writer over `winspool.drv` + A4 `PrintDialog` fallback |
| Q-08 | Camera | File picker first; camera capture at the photo stage |
| Q-09 | Old data | Start clean by default; import available on request |
| Q-10 | Urdu receipts | English first; Urdu lines as graphics later |
| Q-11 | Installer | Portable self-contained folder first; MSI later |
| Q-12 | Second counter | Not now (per §4.3.8 / §34.9) |
| Q-13 | .NET version | .NET 10 LTS (fall back to .NET 8 only if the shop PC cannot install 10) |
| Q-14 | Prototype | Keep in history, remove from the working tree once `net-stage-1` builds |
| Q-15 | Loyalty | OFF; defaults 100 rupees/point, VIP at 5-star threshold |

---

## 9. Risk register

| Risk | Mitigation |
|---|---|
| Wrong schema → owner's existing data unusable | Port `schema.ts` verbatim; open a copy of a real `area11.db` in a test |
| Two counters double-selling (later) | Single transactional Core calls; WAL; stable ids |
| Thermal printer unavailable | Printing is a service; absence never blocks a sale (§Z-07) |
| Money rounding errors | Integer paisa + the 6 reconciliation tests + the 5 report tests |
| Scope creep from §33.2 gaps | Stage 14 is explicitly owner-approved work only |
| Build cannot be verified in this Linux sandbox | Windows CI or shop PC build; static checks here (XAML/JSON/SQL/C# parse) |
| Secret exposure (`sync.key`) | DPAPI-protected storage option; never logged |

---

## 10. What will NOT be done

- The Next.js reference app is **not** modified or deleted.
- No second counter server, no Android app, no cloud sync in the first release (§4.3.7, §34.9).
- No production role switcher (§7.3.1).
- No new setting key that no code reads (§6.1) — the 70-key catalogue stays clean.

---

## 11. Approval

The owner's answer to Q-01…Q-15 (chat) unlocks `net-stage-1`. Until then this document is the only change.
