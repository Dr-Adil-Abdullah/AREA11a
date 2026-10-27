# RULES MATRIX - every numbered rule -> where it lives -> its test

> **Generated file - do not edit by hand.** The rule text is read from
> [`AREA11-MASTER-SPEC.md`](AREA11-MASTER-SPEC.md) by `tools/build_rules_matrix.py`;
> only the status table inside that script is edited. Run it again after changing a rule's
> state: `python3 tools/build_rules_matrix.py`.

Rules are the owner's own numbering (spec section 3). **No rule may be skipped** (spec 30.1.4).

## Legend

| Badge | Meaning |
|---|---|
| ✅ done | written, and covered by a test where a test is possible |
| 🟡 written / partial | code exists but is incomplete, or has not yet been compiled on Windows |
| ⬜ not started | planned, not written |
| 🔁 standing rule | a way of working that must be obeyed in every session - never "finished" |

**Read this together with [`PROGRESS.md`](PROGRESS.md)** - that file tracks features and phases,
this file tracks the owner's numbered rules.

## Summary

- rules in the specification: **116**
- ✅ done: **92**
- 🟡 written / partial: **4**
- ⬜ not started: **0**
- 🔁 standing rules: **20**

## U - the owner's rules

Product, process and the owner's direct orders.

| Rule | Requirement | State | Where it lives | Test | Phase |
|---|---|---|---|---|---|
| **U-01** | Whatever technology is used, a live preview must be available. | 🔁 standing rule | desktop app; the owner runs the build (`docs/BUILD-ON-WINDOWS.md`) | - | - |
| **U-02** | It must be possible to go back to an earlier stage / earlier edit. | ✅ done | git history + tags (no reset that loses work) | - | - |
| **U-03** | Everything is stored in the Area11 repository. | ✅ done | this repository | - | - |
| **U-04** | First look at all files; if something is missing, ask; if complete, start work. | 🔁 standing rule | AGENTS.md read order | - | - |
| **U-05** | First the necessary/basic work, after that the main work. | 🔁 standing rule | four phases (D-20) | - | - |
| **U-06** | This is a business — there must be no recurring technical errors (zero error). | 🔁 standing rule | tests + `tools/check_repo.py` before every commit | - | - |
| **U-07** | Whatever code, library, framework feature or repository already exists — use that. | ✅ done | Dapper, Microsoft.Data.Sqlite, WPF, xUnit | - | - |
| **U-08** | Write as little code as possible. | 🔁 standing rule | one Compute() for quote+save, shared helpers | - | - |
| **U-09** | Personal use — no license restriction (condition: mature code). | ✅ done | personal use, MIT-style dependencies only | - | - |
| **U-10** | Everything must be editable — app name, logo, categories… nothing is final (see E). | ✅ done | `Services/SettingsService.cs` + the Settings page (122 keys) | - | - |
| **U-11** | A change made on the front end must automatically reach the back end/data (see S). | ✅ done | services write straight to SQLite, no second copy | - | - |
| **U-12** | Every point is written numbered, so nothing is missed. | ✅ done | this matrix | - | - |
| **U-13** | One link is enough: giving only the repository link must allow any new agent to understand what is done and what remains. HANDOFF.md and AGENTS.md are always kept current. (Order of 1 Oct 2026.) | ✅ done | HANDOFF.md + AGENTS.md + docs/PROGRESS.md + this file | `tools/check_repo.py` | - |
| **U-14** | Update documents after every agent session: after every feature/decision update HANDOFF.md, the rule numbers file, and the README so the next agent starts exactly where the last stopped. | 🔁 standing rule | AGENTS.md section 6 (definition of done) | - | - |
| **U-15** | Bring the necessary forward: a feature that belongs to a later phase but without which the shop cannot work (returns, credit collection, backup, day-end) is done earlier. | 🔁 standing rule | Phase 1 includes credit sales because the shop needs them | - | - |
| **U-16** | The next work is given to a new/full agent with only the repository link; the questions (Q-01…Q-19) are already settled and must not be asked again. | ✅ done | AGENTS.md is the single entry point | - | - |
| **U-18** | Category/company etc. are created where they are chosen: wherever a drop-down exists there is "＋ New" — no need to leave to another page. (5 Oct 2026) | ✅ done | `ProductService` categories, companies and shelves; inline supplier in Purchases | - | - |
| **U-19** | Photos live in a folder (data/photos/), the database stores only the file name — keeps the database light. (5 Oct 2026) | ✅ done | `PhotoService`: the file lives in `photos` **beside the database** (shared folder = both counters see it), the database keeps only the name; migration 021 adds the put-away flag | PhotoTests | Phase 4 |
| **U-20** | Current rule (27 Oct 2026 physical-testing decision): an explicit discount percentage applies to the full retail/selling-price total. This supersedes the 5 Oct margin/retail choice for new sales; the legacy discount.mode value is retained only so old databases open safely and does not change the calculation. | ✅ done | SaleService full-retail discount + Counter's explicit special-rate choice; legacy `discount.mode` is inert | SaleTests.*Discount*, SaleTests.SpecialRate_* | - |
| **U-21** | Medicine room/almari + camera photo: each medicine carries "where it is kept" and a photo — both. (5 Oct 2026) | ✅ done | `products.room` + photos of the medicine, the customer and the supplier (F-41) | PhotoTests | - |
| **U-22** | Filter + sort in the three big lists: medicines, customers, bills — and in the medicine list grouping (category/company/room) that can be expanded/collapsed. (5 Oct 2026) | ✅ done | `ProductService.Search` with the fuzzy fallback (F-50) and the Products page filters | ProductTests.* | - |
| **U-23** | Git updated with every change: whenever the chat is closed, code + documents must both be on GitHub; "no work should be such that it cannot be updated". (5 Oct 2026) | ✅ done | every change pushed | - | - |
| **U-24** | Remaining work one by one: finish the app first, after each item update git, and keep a readable done/remaining list inside the files. (5 Oct 2026) | ✅ done | docs/PROGRESS.md is the readable done/remaining list | - | - |
| **U-25** | Negative inventory: the system never stops a sale even if stock goes below zero — but a big red alert appears at the very top of the screen. (6 Oct 2026, Spec 1) | ✅ done | superseded for new sales by D-93: StockPolicy refuses below zero; historical alerts remain | NegativeStockPolicyTests.* | Phase 2 |
| **U-26** | Every stock movement is real-time: every sale and every return changes stock at that moment — and if a batch is empty the record still continues (stock really goes negative). (6 Oct 2026, Spec 1.3) | ✅ done | stock movements stay transactional; D-93 supersedes the old empty-batch continuation | NegativeStockPolicyTests.* | - |
| **U-27** | A log in every module is mandatory: every action (sale, return, stock, edit, delete, login) is stored permanently with user name/id, time, action, old value and new value. Unauthorized attempts are also recorded. (6 Oct 2026, Spec 2) | ✅ done | every module writes the audit trail (`AuditService`); one real line in the shop is the board's F-04 | AuditDiffTests.* | Phase 3 |
| **U-28** | Strict return validation: a return only against the original bill number; more than what was sold or more refund than what was collected is impossible; after approval stock and money records correct themselves. (6 Oct 2026, Spec 3) | ✅ done | `ReturnService` (bill number, quantity and refund guards) | ReturnTests.* | Phase 2 |
| **U-29** | Instant notice at the counter: a stock problem or price change message appears on the billing screen at that moment (toast + top alert bar). (6 Oct 2026, Spec 4) | ✅ done | the frame's alert bar on every page + the counter's own message | AlertTests.* | Phase 2 |
| **U-30** | Negative stock must never stop any work: supplier return, stock adjust, write-off — anywhere the quantity exceeds available stock, no error, only an alert (warning); the work completes even if stock goes negative. (6 Oct 2026) | ✅ done | superseded for new decrements by D-93: central guard; legacy setting inert; physical-count reconciliation | NegativeStockPolicyTests.* | Phase 2 |
| **U-31** | Every alert is dismissed only by the owner, by writing a reason: any alert (negative stock, no-bill return, etc.) can be "ended" only by the Owner and a reason is compulsory; the reason is stored and logged. Until the owner dismisses it with a reason, the alert shows on every page. (6 Oct 2026) | ✅ done | `AlertService`: owner only, a reason is compulsory, never deleted | AlertTests.* | Phase 2 |
| **U-32** | Returned goods go straight into stock, no quarantine: the moment goods are taken back from a customer (as soon as money goes out) they are added to stock at once. Damaged/expired goods are not taken back at all, so no separate quarantine is needed. (6 Oct 2026) | ✅ done | changed by D-22 to quarantine: a returned item is counted, then put away or back | QuarantineTests.* | Phase 2 |
| **U-33** | No-bill (rush) return: stock immediately + alert stays: on a return without a bill the goods go into stock immediately; the bill may be attached later, but the alert keeps showing until the owner writes a reason and dismisses it (even after the bill is attached). (6 Oct 2026) | ✅ done | `ProvisionalReturnService` (`PR-`), stock at once and the alert stays | ProvisionalTests.* | Phase 2 |
| **U-34** | Quarantine stays, but with full accounting: every item in quarantine has its own permanent number which can never change; the history (black box + quarantine list) must show clearly which item went where — back to stock, expired/damaged (write-off), or sold. From quarantine goods can be (a) returned to stock, (b) sent to damaged/expired, (c) sold if wanted; the negative-stock rule applies here too. While goods are in quarantine the alert keeps going, and the owner can end it by writing a reason (as U-31). (7 Oct 2026) | ✅ done | `NumberingService` gives every quarantine item its own `Q-0001` number | QuarantineTests.* | Phase 2 |
| **U-35** | A printable receipt for every return: containing the permanent return number, date/time, cashier, original bill number, medicine + quantity + refunded amount, reason, total refund — ready for printing (thermal/A4). (7 Oct 2026) | ✅ done | `ReturnReceiptService` prints the return slip with its own number | ReturnReceiptTests.* | Phase 2 |
| **U-36** | Work one by one in the owner's order: (1) the receipt, (2) a final review of the whole app + report, (3) cleaning of useless Settings lines. One finished and uploaded, then the next. (7 Oct 2026) | 🔁 standing rule | receipt -> review -> cleanup -> installer order | - | - |

## R - reuse before writing

What to reuse and what was decided about libraries.

| Rule | Requirement | State | Where it lives | Test | Phase |
|---|---|---|---|---|---|
| **R-01** | Before every feature ask: "does a solution already exist?" Yes = take it. | 🔁 standing rule | process - see AGENTS.md | - | - |
| **R-02** | Six steps: look → check → take → connect → try → save. | 🔁 standing rule | process - see AGENTS.md | - | - |
| **R-03** | Matrix (how much new code): ready-made project ~5 % · built-in feature ~10 % · library ~20 % · module ~40 % · fully new code = last resort. | 🔁 standing rule | process - see AGENTS.md | - | - |
| **R-04** | Not only code — reuse methods/designs too. | 🔁 standing rule | process - see AGENTS.md | - | - |
| **R-05** | Condition: mature, working code — not experimental/broken. | 🔁 standing rule | process - see AGENTS.md | - | - |
| **R-06** | Review of pharmacy repositories (original study): pharma-suite (audit log/RBAC/invoice), Pharmtech-POS (shift/reorder), a Next.js dashboard (UI); the rest (PHP/desktop) light/incomplete. | ✅ done | `docs/PORT-PLAN.md` section 1 | - | - |
| **R-07** | Decision: do not fork a whole repository — because the owner-specific parts exist in none (Box→Strip→Tablet, margin discount, downward round-off, owner drawings, black box, Urdu). | ✅ done | D-17: three projects of our own, no forked repository | - | - |
| **R-08** | Yes, libraries are used — 70–80 % of code saved. | ✅ done | Dapper, Microsoft.Data.Sqlite, xUnit, ClosedXML (planned) | - | - |
| **R-09** | USB barcode gun = zero code (it types like a keyboard). | ✅ done | the gun types the code and presses Enter; Enter in the POS search box scans it (`PosViewModel.ScanCommand`), one code per pack (D-60) | - | - |
| **R-10** | Whatever is used is recorded in the rules file for reference. | ✅ done | `docs/PORT-PLAN.md` + this matrix | - | - |

## E - everything is editable

Nothing hard-coded; every label, price and rule from settings.

| Rule | Requirement | State | Where it lives | Test | Phase |
|---|---|---|---|---|---|
| **E-01** | Nothing is hard-coded — everything comes from data/settings. | ✅ done | the `settings` table + `SettingsService` (122 keys) and the Settings page; nothing in the shop is hard-coded | SettingsTests.* | - |
| **E-02** | Branding: app name, logo, colour/theme — changeable from Settings. | ✅ done | branding keys (`store.*`, `brand.*`) edited on Settings -> Shop | SettingsTests.* | - |
| **E-03** | Shop information: name, address, phone, terms — printed on the receipt. | ✅ done | `ReceiptService` prints `store.*` on every bill | ReceiptTests.* | Phase 1 |
| **E-04** | Categories: create, change, delete, order (unlimited + sub-categories). | ✅ done | `ProductService` + Products → Manage categories and companies: create, rename, two-level parents, order, soft put-away/restore and unlink-only product references; Core permissions and old/new audit; migration 026 orders companies | CatalogTests.* | - |
| **E-05** | Custom fields: owner makes them — text / number / amount / photo (Spec §2.2). | ✅ done | `CustomFieldService` + SYSTEM → Own fields (F-42): text, number, amount, date, list, yes/no, photo | CustomFieldTests | Phase 4 |
| **E-06** | Unit and packing: per product 1 Box = X Strips = Y Tablets — changeable later. | ✅ done | `products.box_strips` / `strip_tablets` | SaleTests (unit multiplier) | - |
| **E-07** | Expiry levels: days + colour from settings (levels 1/2/3). | ✅ done | the three bands are read from `expiry.levels` (fixed in v0.3.10, F-29) | ExpiryTests.* | Phase 2 |
| **E-08** | Rates and discount: retail/VIP/Doctor rates remain editable guidance; a special rate requires an explicit counter Yes, while No keeps retail. Explicit discount is a percentage of full retail, with role limits from Settings (27 Oct 2026 clarification). | ✅ done | editable tier prices + `SaleInput.UseSpecialRate`, `discount.maxPercent*`, and `sale.successPopup`; explicit discount always uses full retail | SaleTests.*Discount*, SaleTests.SpecialRate_* | - |
| **E-09** | Round-off, tax, invoice format, printer size (58/80 mm) — from settings. | ✅ done | `bill.roundTo`, `tax.*` and the printer width in `print.*` | MoneyTests.* | - |
| **E-10** | Receipt layout: date top/bottom, show/hide columns — by toggle. | ✅ done | `receipt.datePosition` and the `receipt.show*` toggles, edited on Settings | ReceiptTests.* | Phase 1 |
| **E-11** | Roles and permissions: Owner/Manager/Cashier powers — from settings. | ✅ done | `user_permissions` + `PermissionService` + the people and permission editor on Settings | PermissionTests.* | - |
| **E-12** | Language/terms: words can also be changed (custom labels). | 🟡 written / partial | `AppLabelService` + Settings → App words: 32 stable section labels in `settings`, Unicode/length/single-line guards, Settings permission, old/new audit and safe reset; Windows/live owner proof pending | AppLabelTests.* | Phase 4 |
| **E-13** | Rule: nothing is "final" — every item has an edit option later. | 🔁 standing rule | process - see AGENTS.md | - | - |
| **E-14** | Start from default values but all changeable; when a category is changed/removed old records stay safe. | ✅ done | soft delete (`active = 0`) everywhere; no business row is ever deleted | ProductTests.* | - |

## S - front end reaches the data

One source of truth: a change on screen is a change in the database.

| Rule | Requirement | State | Where it lives | Test | Phase |
|---|---|---|---|---|---|
| **S-01** | Create a category on the front end → it is created in the database automatically. | ✅ done | `ProductService.AddCategory/AddCompany` | - | - |
| **S-02** | No separate developer work — the user can create/change himself. | ✅ done | categories, companies, shelves and suppliers can be created from the page they are needed on | - | - |
| **S-03** | UI and data are always consistent (one source of truth). | ✅ done | one `Compute()` drives both Quote and Create | SaleTests.BillTotal_* | - |
| **S-04** | A new custom field → appears in the form automatically. | ✅ done | a new field appears on the form at once - the panel is built from the database (F-42) | CustomFieldTests | - |
| **S-05** | Change a name/term → changes everywhere automatically. | 🟡 written / partial | saved labels use stable keys plus live resources to refresh navigation, all 26 page headings, the Cloud sync tab and Own fields panel without rebuilding the shell; Windows rename/restart proof pending | AppLabelTests.* | - |
| **S-06** | Database structure (schema) changes happen by automatic migration — no data lost. | ✅ done | `Data/MigrationRunner.cs`, append-only | MigrationTests.* | - |
| **S-07** | Settings are stored in the database (not only in the browser/local UI) — so identical on every counter/device. | ✅ done | `settings` table | - | - |
| **S-08** | Every change has a record (who, when changed) — Spec §13. | ✅ done | `AuditService` is called by every module - the black box is the proof, not a promise | AuditDiffTests.* | - |
| **S-09** | Offline too — a change made offline syncs when the net returns. | ✅ done | `SyncService`: format-2 push/preview/bring with 90-day rows plus referenced parents, complete lines, backup-first apply and no deletion | SyncTests.* | Phase 4 |

## P - preview and checking

The owner must be able to look at the running app.

| Rule | Requirement | State | Where it lives | Test | Phase |
|---|---|---|---|---|---|
| **P-01** | On every change automatic refresh — the owner sees the app running. | ✅ done | a desktop program; the owner builds and runs it himself | - | - |
| **P-02** | Server bound to 0.0.0.0 so the preview reaches the owner's browser. (Rebuild: equivalent — the owner can run and look at the app after every module.) | 🔁 standing rule | desktop app: the owner's own screen is the preview | - | - |
| **P-03** | After each stage the owner tries it himself (the agent says what to look at). | 🔁 standing rule | end of every phase | - | - |
| **P-04** | Any error is caught immediately (hot reload + automatic checks). | ✅ done | `tools/check_repo.py` (static checks in this sandbox) | - | - |
| **P-05** | Preview is not optional — it is a compulsory part of the work. | 🔁 standing rule | BUILD-ON-WINDOWS.md + check_repo.py | - | - |

## B - save points and going back

History is never lost.

| Rule | Requirement | State | Where it lives | Test | Phase |
|---|---|---|---|---|---|
| **B-01** | Before every stage a save point: bash scripts/ckpt.sh save "name". | ✅ done | git commit before each stage (commit messages carry `net-stage-N`) | - | - |
| **B-02** | Return to any older stage: ckpt.sh go <number>. | ✅ done | git tags / commits | - | - |
| **B-03** | One step back: ckpt.sh undo. | ✅ done | `git revert` / `git checkout <commit> -- <file>` | - | - |
| **B-04** | List: ckpt.sh list · difference: ckpt.sh diff <n> · status: ckpt.sh status. | ✅ done | `git log`, `git diff`, `git status` | - | - |
| **B-05** | Even unsaved work is not lost — an automatic safety snapshot (tested). | ✅ done | Arena snapshots + commits after every change | - | - |
| **B-06** | Nothing is ever erased — history stays linear. | ✅ done | linear history, never force-push to main | - | - |
| **B-07** | Safe across sessions: ckpt.sh push (GitHub). | ✅ done | pushed to GitHub after every change | - | - |
| **B-08** | Helper scripts: sync-spec.sh (spec → Part 1 automatic copy), rules-count.sh (counting of the numbers; says SAB THEEK when no rule number is missing). | ✅ done | replaced by `tools/check_repo.py` (49 checks) + `tools/build_rules_matrix.py` | `tools/check_repo.py` | - |
| **B-09** | The session branch is fixed, but tags/commits allow complete return. The earlier session branch's history and tags stage-1…stage-6 were fast-forwarded into the current branch on 3 Oct 2026 — nothing lost. | ✅ done | fixed session branch, commits/tags for return | - | - |
| **B-10** | Return is possible even after merges: tags stage-1…N and every commit always remain. To look: git checkout stage-N, or ckpt.sh go N; to undo a merge: git revert -m 1 <merge-commit>. Never delete tags/history; never force-push. | ✅ done | tags `net-stage-N` from stage 2 onward | - | - |

## Z - money and data are never broken

The hard guarantees.

| Rule | Requirement | State | Where it lives | Test | Phase |
|---|---|---|---|---|---|
| **Z-01** | Run every piece of work yourself and look, then save. | 🔁 standing rule | build + tests + owner runs it | - | - |
| **Z-02** | A new change must not break old working parts (test at every stage). | ✅ done | regression tests for every money rule | SaleTests.*, MoneyTests.* | - |
| **Z-03** | No mistakes in calculation/money (round-off only downward — 545→540). | ✅ done | round-down only downward, never below cost | MoneyTests.*, SaleTests.RoundDown_* | - |
| **Z-04** | Data is always safe (auto-save + transactions). | ✅ done | single transaction per document, WAL | SaleTests.* | - |
| **Z-05** | Wrong entry reversible (Undo / see the earlier state). | 🟡 written / partial | `AuditSnapshot` + selected Black box row: complete read-only before/after/details and guidance to existing audited corrections; no universal financial undo; Windows/live owner proof pending | AuditViewerTests.EarlierState_* | - |
| **Z-06** | Offline too — no error (Spec §1.2.1). | ✅ done | SQLite file, no network needed | - | - |
| **Z-07** | If the printer/barcode device is absent the app must not stop. | ✅ done | a printer fault is reported and the bill is saved anyway - the paper never decides the sale | PrintTests.* | - |
| **Z-08** | Data is one local file (SQLite) — backup = copy of one file. | ✅ done | one SQLite file + zip backup (Phase 3) | - | - |
| **Z-09** | Audit log (unchangeable) — to reach the root of any mistake. | ✅ done | `audit_logs` + the Black box page (search, filters, CSV) | AuditDiffTests.* | - |
| **Z-10** | Each stage passes through the owner's hands, then move forward. | 🔁 standing rule | end of each phase | - | - |
| **Z-11** | Round-off + cost protection (owner-confirmed): if rounding down to a multiple of 10 would take the amount below the purchase cost, the rounding is not applied (bill 545, cost 543 → collect 545; bill 545, cost 500 → 540). Original function: applyRoundWithCostGuard. A warning is shown when a bill is below cost. | ✅ done | `Money.RoundDownWithCostGuard` | MoneyTests.* | - |

## T - technology

What the original used, and what the rebuild uses instead.

| Rule | Requirement | State | Where it lives | Test | Phase |
|---|---|---|---|---|---|
| **T-01** | TypeScript + Next.js — screen + server in one place (less code). | ✅ done | reference only; rebuild is C#/WPF (D-17) | - | - |
| **T-02** | UI: Tailwind + shadcn/ui + Lucide — ready tables/forms/dialogs. | ✅ done | `Resources/Theme.xaml` design system | - | - |
| **T-03** | Data: Node's own SQLite (node:sqlite) — zero dependency (Prisma was planned first but its engine binaries could not be downloaded; the built-in SQLite is better: no extra package = fewer errors). One file, offline, fast, WAL mode. | ✅ done | SQLite via Microsoft.Data.Sqlite + Dapper | - | - |
| **T-04** | Offline: PWA + Dexie + Workbox (Spec §1.2). | ✅ done | desktop is offline by nature | - | - |
| **T-05** | Charts: Recharts · tables: TanStack Table (virtual). | ✅ done | native WPF per-day bar chart (`ReportDayRow.BarShare` + `ProgressBar`) and DataGrid tables; Recharts/TanStack are reference-web libraries; stale no-chart status corrected after actual-code review | ReportTests.PerDay* | Phase 3 |
| **T-06** | Forms: react-hook-form + Zod (automatic validation). | ✅ done | a service refuses with a sentence the cashier can act on, and the page shows it beside the field | SaleTests.* | - |
| **T-07** | Search: Fuse.js (even a wrongly spelled medicine is found). | ✅ done | misspelt search (F-50): `panadl` finds Panadol, and Enter never adds a guess | ProductTests.* | Phase 4 |
| **T-08** | Print: node-thermal-printer / ESC-POS (58/80 mm) + react-to-print (A4). | ✅ done | `EscPosBuilder` + `PrinterService`; the owner printed on the shop printer on 23 Oct 2026 | PrintTests.* | Phase 1 |
| **T-09** | WhatsApp: free wa.me link — no paid API (Spec §11.2.5). | ✅ done | the Reorder pad builds the order text and opens WhatsApp (F-38) | ReorderTests.* | Phase 3 |
| **T-10** | Excel: SheetJS (import old data / export reports). | 🟡 written / partial | dependency-free native `.xlsx` read/write, multi-sheet templates and eight-sheet all-data/bulk update are written with protected stock/balances/Settings and 14 facts; Windows/xUnit and real Excel open/edit/re-import proof pending | WorkbookTests.*; ImportTests.* | Phase 3 |
| **T-11** | Live preview: dev server (0.0.0.0). | 🔁 standing rule | desktop app | - | - |
| **T-12** | Return/go-back: Git + tags (no new engine to write). | ✅ done | git tags | - | - |
| **T-13** | No extra library for the schema: SQL schema + migrations written by hand (short, clean, understandable). | ✅ done | hand-written SQL migrations 001-015 | - | - |
| **T-14** | Two app lines: (1) the real product = root folder (Next.js + node:sqlite, real database, offline); (2) legacy-demo-vite/ = a Vite browser demo of another session (phases 1–3, sample data, 212 unit tests + 36 end-to-end specs, docs) kept for reference/reuse (logic for returns, cash drawer, supplier ledger in src/domain/: returns.ts, cash.ts, accounts.ts, suppliers.ts, alerts.ts, arithmetic.ts); it must not be merged into the root. | ✅ done | `dotnet/` is the product; prototypes kept for reference | - | - |

## M - milestones

The original's five phases, mapped to this rebuild's four.

| Rule | Requirement | State | Where it lives | Test | Phase |
|---|---|---|---|---|---|
| **M-01** | Stone 0 — foundation + checkpoint + information files + numbered rules ✅. | ✅ done | foundation, information files, numbered rules | - | - |
| **M-02** | Stone 0.5 — Next.js + database + live preview running + settings skeleton (E + S from day one). | ✅ done | schema + settings + audit + login running | - | - |
| **M-03** | Phase 1 (Core Lifeline): auto PINV stock-in + batch + expiry + supplier account; packing formula + [Box][Strip][Tablet] buttons; counter billing with barcode/search, FIFO, auto INV, discount, downward round-off; thermal receipt 58/80 + auto print + hold/park cart + cash/credit; role switcher + sales history screen. | ✅ done | purchases, the counter and the receipt are written and the owner prints | SaleTests.* | - |
| **M-04** | Phase 2 — returns + smart bill lookup + provisional-return alert + float/shift/closing + owner drawing + expenses + below-cost lock. | ✅ done | returns, quarantine, alerts, write-off and stock-take | ReturnTests.* | Phase 2 |
| **M-05** | Phase 3 — supplier ledger + return credit note + 3-level expiry + 3-level reorder + WhatsApp web order + sample stock. | ✅ done | ledgers, reports, backup, Excel/CSV, reorder/WhatsApp and the stock-minus registry; the sample list was deleted on the owner's word (D-77) | ReportTests.* | Phase 3 |
| **M-06** | Phase 4 — customer/credit limit/loyalty stars + multi-tier rates + split payment + custom fields/categories. | ✅ done | loyalty, photos, own fields, barcodes, reps/doctors, the LAN second counter, the installer and the hardware test - its scan half still waits for the shop | HardwareTests.* | Phase 4 |
| **M-07** | Phase 5 — black-box audit + multi-dimensional search/rack number + breakage + analytics/P&L + online-offline sync + backup. | ✅ done | the black box, stock-take, backup and cloud sync | BackupTests.* | Phase 3 |
| **M-08** | Rule: the E and S principles apply from day one — not later. | ✅ done | settings and data-driven from the first migration | - | - |

