# AGENTS.md - how to work in this repository

This file is for **any AI coding agent** that is given only the repository link (the owner's rule
U-13). Read it fully before touching code; it is short on purpose.

Owner: Dr. Adil Abdullah (does not program). Product: **Area11**, an offline pharmacy POS +
inventory for Windows. Language: talk to the owner in **Urdu or English matching his message -
never Roman Urdu**. Code, database and UI stay in **English**.

---

## 1. Start here (10 minutes)

1. [`HANDOFF.md`](HANDOFF.md) - orientation, next tasks, traps.
2. [`docs/PROGRESS.md`](docs/PROGRESS.md) - every feature: ID, state, files, test, "done when".
3. [`docs/OWNER-DECISIONS.md`](docs/OWNER-DECISIONS.md) - the owner's decisions (Urdu, authoritative).
4. [`docs/RULES-MATRIX.md`](docs/RULES-MATRIX.md) - his 116 numbered rules and where each lives.
5. [`docs/AREA11-MASTER-SPEC.md`](docs/AREA11-MASTER-SPEC.md) - the blueprint. Sections 3 (rules),
   5 (data), 11 (pricing), 12 (billing), 14 (printing), 15 (returns), 34 (rebuild protocol).
6. [`docs/BUILD-ON-WINDOWS.md`](docs/BUILD-ON-WINDOWS.md) - how the owner builds it.

Then run the checkers (below) and pick the first ⬜ or 🟡 task from `HANDOFF.md` §4.

## 2. Hard rules

0. **Document every change, and change the colour with it.** Two standing orders from the owner:
   (a) anything a future session would need to know goes into this repository - `HANDOFF.md`,
   `docs/PROGRESS.md`, `docs/VERSIONS.md`, the doc comment above the code - so that the app can be
   dropped at any moment and any agent can continue from the link alone;
   (b) every code change ships with a **new brand colour** (`docs/VERSIONS.md`), because the owner
   tells builds apart by colour on his own screen.

These come from the spec (§3, §5) and from bugs already paid for. A change that breaks one of them is
rejected even if it "works".

1. **Money is `long` paisa**; quantities are **`double`** (the schema stores REAL). Never `decimal` -
   Microsoft.Data.Sqlite turns it into TEXT and `SUM()` breaks.
2. **Bill total = sum of line totals.** Round once (`Money.RoundDownWithCostGuard`), then spread the
   difference over the last allocation. Never re-round the total afterwards.
3. **Round down only, never below purchase cost** (Z-11).
4. **Negative stock never blocks a sale or a stock operation**; it warns, records `negative_sale`, and
   raises a persistent alert. It blocks only when `stock.allowNegative` is off (U-25/U-30).
5. **Permissions are enforced in `Area11.Core`**, keyed on the signed-in `User`. Hiding a button is not
   a permission. Owner-only actions: dismissing alerts, applying stock-take, restoring backup.
6. **Never delete business rows.** Use `active = 0`. Audit rows and sales are permanent.
7. **Migrations are append-only**, run inside `BEGIN IMMEDIATE`, and re-check `_migrations` after the
   lock is taken. Add `016_*.sql`; never edit an existing file.
8. **Views contain no SQL and no business logic.** Services own every rule and every transaction; a
   service method takes `User` when the rule depends on permissions.
9. **Never modify the original Next.js app** (`Dr-Adil-Abdullah/Area11`) - it is only a behaviour
   reference.
10. **Everything editable** (E-01): no hard-coded shop name, discount limit, printer width, wording or
    colour. Settings come from the `settings` table via `SettingsService`.

## 3. Layout you must respect

```
dotnet/Area11.Core/      no UI references - Data, Models, Services, Utils
dotnet/Area11.Desktop/   WPF + MVVM only - Views, ViewModels, Resources, AppServices
dotnet/Area11.Tests/     xUnit; TestDatabase = in-memory SQLite + all migrations + seed
docs/                    spec, decisions, progress, rules matrix, schema, build guide
tools/                   check_repo.py, build_rules_matrix.py
```

Three projects only (owner's D-17). Do not add a fourth.

Wiring a new screen: service → property on `AppServices` → entry in `ShellViewModel.NavItems` guarded
by `_services.Permissions.Can(user, Modules.X)` → `DataTemplate` in `ShellWindow.xaml` → view + view-model.

## 4. Verify before you claim anything works

```bash
python3 tools/check_repo.py              # C# syntax, XAML/XML, bindings+resources, migrations, tests, docs links
python3 tools/build_rules_matrix.py --check
```

`check_repo.py` uses `tree-sitter` when available and falls back to brace balance when not. The agent
sandbox has **no .NET SDK**, so a WPF build can only be done by the owner; never say "it builds" unless
you actually ran `dotnet build` on a machine with the SDK.

## 5. Asking the owner

- Only when a decision truly blocks progress, and never twice about the same thing.
- Ask as **multiple choice**: `سوال / Question:` then `ممکنہ حل / Possible Options:` A/B/C/D, all
  questions in one message, each option a workable solution (not "whatever you prefer").
- Explain the concept in plain Urdu **before** asking; warn about money/accounting risk explicitly.
- Record every answer in `docs/OWNER-DECISIONS.md` as the next `D-xx`, then follow it.

## 6. Definition of done (spec §30.4.1, owner's U-14)

1. `python3 tools/check_repo.py` is clean (or its only complaints are pre-existing and explained).
2. A test covers the rule you touched, or `PROGRESS.md` says why it cannot.
3. `docs/PROGRESS.md` is updated (state, files, test).
4. `tools/build_rules_matrix.py` status map is updated and the matrix regenerated.
5. `HANDOFF.md` §4 still names the real next task.
6. Commit message names the feature: `F-13: receipt + instant ESC/POS print`.
7. Push. The owner's rule U-23: **every change reaches GitHub**, code and documents both.

Never commit `data/`, `*.db`, `*.log`, `bin/`, `obj/` (`.gitignore` already covers them) and never
commit a real shop database.

## 7. House style

- C# naming: `PascalCase` public members, `_camelCase` private fields, no abbreviations.
- Comments explain **what and why**, never how the code works line by line.
- One `try/catch` policy: services catch, log through `LogService`, and return
  `ServiceResult`/`ServiceResult<T>` with a message the cashier can act on. No empty catch, no
  exception text shown raw to the user.
- Prefer a method that already exists. Reuse beats elegance (owner's R-01…R-10).
- Keep UI text in English; keep it short enough for a 58 mm receipt.

## 9. Releasing a version (the colour rule)

The owner tests each build himself and keeps several copies of the folder, so every release gets a
new colour and a version number. Nothing may be shipped without it:

1. next colour from the palette in `docs/VERSIONS.md` (never repeat the previous one);
2. set `Version`, `CodeName`, `BrandHex`, `BrandDarkHex`, `BrandSoftHex`, `ReleasedOn` in
   `dotnet/Area11.Core/Utils/BuildInfo.cs`;
3. paint the same three colours on `BrandBrush` / `BrandDarkBrush` / `BrandSoftBrush` in
   `dotnet/Area11.Desktop/Resources/Theme.xaml`;
4. add the row to `docs/VERSIONS.md`;
5. `python3 tools/check_repo.py` - it fails if the three disagree;
6. commit with the version in the message.

**Tests may only use public code.** `check_repo.py` fails if a test names an `internal` type: the
sandbox cannot compile, so a CS0122 would otherwise reach the owner's PC (it did, in v0.2.1). Write
tests through the public service methods and a real bill in `TestDatabase`.

## 8. Current state (update this line if you finish a phase)

> **v0.28.0 (Pine `#1E4D3B`)** - all four phases are written: Phase 1-3 in full (billing, returns,
> purchase, khata, supplier ledger, shift/galla, expenses, reports, black box, backup & restore,
> Excel / CSV, reorder + WhatsApp, stock-minus registry), and Phase 4 has **all 12 of its features**:
> loyalty from the screen, photos for medicines / customers / suppliers (copied into the shop's
> folder, **put away instead of deleted**), the owner's **own fields**, **several barcodes per
> medicine** (migration 022), **medical reps + doctors + lifetime profit** (migration 023), the
> **second counter** over the shop's own share (D-62), the **tax report + `PAY-` slip + monthly
> statement** (D-63), the **installer** (F-47, D-64), the **printer & scanner test** (F-46, D-65),
> **cloud sync** (F-48, D-66) and the **bills page** (F-52, D-67) - 12 of 12, with the printer
> repair of v0.22.1 keeping the hardware test honest.
>
> The last confirmed Windows build is v0.4.8; everything from v0.6.2 on is proved only by
> `tools/check_repo.py` until the owner runs `START-AREA11.bat` and `tools\\RUN-TESTS.cmd`
> (**666 cases**).
>
> **v0.28.0 turns the sample shop into a running shop (F-53, D-74).** SYSTEM -> *Sample shop* now
> writes **72 days** - 44 medicines over 17 categories, 20 customers, ~700 bills, deliveries six
> mornings a week, 16 returns, khata collections, rent/salary/chai/repairs, and 73 counted drawers
> with today's still open. The generator is deterministic (`Random(20261026)`), every closed drawer
> is rebuilt from its own payments exactly as `ShiftService` does it, and FEFO never sells a batch
> inside its last month. 12 new cases, none compiled or run here; `tools\RUN-TESTS.cmd` must end
> `Failed: 0` with **666** cases.
>
> **v0.27.0 adds the bill lookup for a return (F-20).** The Returns page takes the bill number, a phone with or
> without dashes, a customer name or a medicine, plus an optional date range, and answers with a **list** of
> bills - never a guess. *Open this bill* runs the same lookup a typed number runs, so there is one doorway.
> Searching writes nothing. 13 new cases, none of them compiled or run here; `tools\RUN-TESTS.cmd` must end
> `Failed: 0` with **654** cases.
>
> **v0.26.1 is a build fix (F-53).** The owner's first real build of the cloud round stopped in Core with **CS0200**:
> `new SaleSample { Lines = lines }` set a getter-only property; it is `init;` now. The lesson is a **24th checker
> rule**, `check_readonly_members`, which fails any object initializer that assigns a getter-only property - proved by
> reintroducing the line and watching the check fail. No behaviour changed, no test changed (**641 cases**).
>
> **v0.26.0 repairs the cloud push (F-48).** The cloud table's row is built from its own snake_case columns
> (`shop_id`, `shop_name`, `sent_at`, `version` as an int, `payload`), `sent_at` carries the shop's offset,
> `version` holds the document format while the program version stays in `payload`, and Settings has its
> fifth tab - **Cloud sync** - with the `sync.enabled` switch that was declared in Spec 6.12 and never read.
> The key is never shown again, never audited, and the tab is closed to anyone without cloud-sync permission.
> These 12 new tests have **not** been compiled or run here; `tools\RUN-TESTS.cmd` must end `Failed: 0`
> with **641** cases, and the first real Supabase push is still the owner's to make.
>
> **v0.25.0 adds the purchase, partner and numbering tests (F-18)** - the last untested corner of
> the code, proved through the public services only: a 2 x 10 box is 20 tablets, a bonus line never
> reaches the bill, a supplier payment may not overshoot, and every paper keeps its own counter.
> The 36 new cases have **not been compiled or run here** (this sandbox has no .NET SDK);
> `tools\RUN-TESTS.cmd` must end `Failed: 0` with **629** cases.
>
> **v0.24.0 adds the misspelt search (F-50).** A typo at the counter or on the Products page still
> finds the medicine - and the page says the list is a guess, never an answer.
>
> **v0.23.0 adds the sample shop (F-53).** SYSTEM -> *Sample shop* fills a new database with 20 medicines,
> 12 companies, 4 suppliers, 8 customers, 4 purchases, 12 bills, 2 returns, khata, expenses, a shift, a
> stock-take and a held cart in ONE transaction - refusing any shop that already has a bill.
>
> **v0.22.1 fixes the printer (code 6) and the two red tests.** SYSTEM -> *Printer & scanner* lists
> every printer Windows knows and saves the choice at once; an empty name now resolves the default
> printer by name instead of opening the print server, and each Windows code is explained.
>
> **v0.21.0 added cloud sync (F-48).** SYSTEM -> *Cloud sync* pushes the shop's own data to a Supabase
> mirror (lists whole, money for the last 90 days, no photos). *See what the cloud has* reads the
> copy and writes **nothing**; *Bring the cloud data here* needs a tick, takes a **backup**, writes
> every row in **one transaction**, never deletes, and ignores tables or columns this database does
> not have.
>
> **Next:** the misspelt-medicine search (F-50), the sample shop (F-53), and the owner's own run of
> the printer slip on the shop's paper (F-46).
> Details: `HANDOFF.md` §4.
