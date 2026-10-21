# Area11 — pharmacy POS + inventory for Windows

A native **Windows desktop** rebuild of the Area11 Smart Pharmacy & Retail POS + Inventory system,
written in **C# / .NET 10 / WPF / SQLite**. It works with no internet, keeps the shop's whole
database in one file, and prints bills instantly on a 58 mm or 80 mm thermal printer.

Ported from the original Next.js + SQLite web app (`Dr-Adil-Abdullah/Area11`) using the
[master specification](docs/AREA11-MASTER-SPEC.md) as the blueprint.

> **Taking over this repository? Start with [`HANDOFF.md`](HANDOFF.md).**
> It says what is finished, what is next, and the traps. [`AGENTS.md`](AGENTS.md) explains how to
> work here. The owner's own decisions are in [`docs/OWNER-DECISIONS.md`](docs/OWNER-DECISIONS.md).

---

## Where it stands

| Phase | Contents | State |
|---|---|---|
| **1** | Foundation, database, sign-in, products, purchases `PINV`, counter `INV`, receipt & instant print, settings screen, **hold cart, split payment** | ✅ written and compiled; needs one real walk-through by the owner |
| **2** | Returns `RET` / provisional `PR` / quarantine `Q`, alert centre, write-off, stock-take `ST`, ledgers | ⬜ not started — **next** |
| **3** | Shift & galla, expenses, `SR` recovery, reports, audit viewer, backup, Excel, reorder/WhatsApp | ⬜ not started |
| **4** | Loyalty, photos & camera, custom fields, second counter, hardware test, **self-installing installer** | ⬜ not started |

Live counts are in [`docs/PROGRESS.md`](docs/PROGRESS.md) (features) and
[`docs/RULES-MATRIX.md`](docs/RULES-MATRIX.md) (the owner's 116 numbered rules).

**Honest note:** the build machine used so far is Linux with no .NET SDK, so nothing in `dotnet/`
has ever been compiled. It is checked statically (C# parses, XAML/bindings/resources valid,
migrations execute against SQLite, 173 test cases written). The Windows build succeeded on
20 Oct 2026; see [`docs/VERSIONS.md`](docs/VERSIONS.md) for which build the owner has.

---

## Build and run (Windows 10/11)

**Easiest:** double-click **`START-AREA11.bat`** — it checks the folder, updates, builds and starts
the app. [Urdu steps](docs/HOW-TO-RUN-URDU.md).

> **`Area11.sln` missing in your folder?** That copy is old or incomplete (the download was
> interrupted, or an older extract was merged into it). Take a fresh copy instead of repairing it:
> [download the ZIP](https://github.com/Dr-Adil-Abdullah/AREA11a/archive/refs/heads/arena/a8a3b2e0-area11a.zip),
> extract to `D:\`, then run `START-AREA11.bat` inside the new folder. Shop data lives in
> `C:\ProgramData\Area11` and is never touched.

Or manually:

```bat
git clone https://github.com/Dr-Adil-Abdullah/AREA11a.git
cd AREA11a
dotnet build
dotnet test
dotnet run --project dotnet\Area11.Desktop
```

Sign in as **Owner** with password **`area11`**, then change it. Data lives in
`%ProgramData%\Area11\` (`area11.db`, `photos\`, `logs\`, `backups\`) and is never committed.
Step-by-step help, including what to do when the build fails: [`docs/BUILD-ON-WINDOWS.md`](docs/BUILD-ON-WINDOWS.md).

> The blue "app is running" window some earlier builds show is `prototypes/WPFSample`, the very first
> smoke test — it is **not** Area11. The real app is `dotnet/Area11.Desktop` (assembly name `Area11`).

---

## Repository map

| Path | What it is |
|---|---|
| `Area11.sln` · `dotnet/` | **The product**: the solution is in the root; `Area11.Core` (rules, SQLite) · `Area11.Desktop` (WPF) · `Area11.Tests` (xUnit) |
| `docs/` | specification, owner decisions, progress board, rules matrix, schema reference, build guide, port plan |
| `tools/` | `check_repo.py` (static checks) · `build_rules_matrix.py` (keeps the rules matrix in sync with the spec) · `ci/build.yml` |
| `HANDOFF.md` · `AGENTS.md` | the takeover manual and the working protocol |
| `prototypes/` | earlier prototypes (`Area11.WPF`, `WPFSample`), reference only — they are not the app |

## Documentation

| File | Language | Contents |
|---|---|---|
| [`HANDOFF.md`](HANDOFF.md) | English | orientation, next tasks, non-negotiables, traps, decisions in English |
| [`AGENTS.md`](AGENTS.md) | English | read order, hard rules, verification, definition of done |
| [`docs/PROGRESS.md`](docs/PROGRESS.md) | English | every feature: ID, state, files, test, "done when" |
| [`docs/RULES-MATRIX.md`](docs/RULES-MATRIX.md) | English | 116 numbered owner rules → where each is implemented |
| [`docs/AREA11-MASTER-SPEC.md`](docs/AREA11-MASTER-SPEC.md) | English | the blueprint (sections 3, 5, 11, 12, 14, 15, 34) |
| [`docs/OWNER-DECISIONS.md`](docs/OWNER-DECISIONS.md) | **Urdu** | the owner's decisions D-01…D-23, phase plan, open questions |
| [`docs/SCHEMA-REFERENCE.md`](docs/SCHEMA-REFERENCE.md) | English | column-by-column reference of every table |
| [`docs/PORT-PLAN.md`](docs/PORT-PLAN.md) | English | the technical study: conflicts, module map, test map, port order |
| [`docs/BUILD-ON-WINDOWS.md`](docs/BUILD-ON-WINDOWS.md) | English | build, run, test, and what to send back when something fails |
| [`docs/GIT-AND-VS-URDU.md`](docs/GIT-AND-VS-URDU.md) | **Urdu** | bring the code from GitHub, open it in Visual Studio / VS Code, change the colours |

## Database

`dotnet/Area11.Core/Data/Migrations/` holds the **original 13 migrations verbatim**, plus
`014_user_permissions.sql` (per-person permissions) and `015_photos.sql` (several photos per product,
supplier and customer), then Phase 2's `016`-`019` (held carts, return detail, no-bill detail, stock-take
detail). They create **33 business tables** (plus the `_migrations` ledger) and run
append-only inside `BEGIN IMMEDIATE`, so an existing `area11.db` opens unchanged and no data is lost
when the app is updated.

## The rules that shape the code

- Money is **integer paisa**; quantities are `double` (the schema stores REAL). Never `decimal`.
- A bill's total always equals the sum of its line totals.
- Rounding is **downward only**, and never below purchase cost.
- Negative stock never stops a sale — it warns and raises an alert (owner's rule U-25).
- Discounts are capped per role and can never sell below cost.
- Permissions are enforced in the business layer, not by hiding buttons.
- Nothing is hard-coded: shop name, wording, limits, printer width all come from `settings`.
- Every action is written to an audit trail with old → new values; nothing is ever deleted.

## Checking your work

```bash
python3 tools/check_repo.py             # C#, usings, XAML, bindings, migrations, SQL, tests, docs links
python3 tools/build_rules_matrix.py --check
```

Both must be clean before a commit; `dotnet build` + `dotnet test` are the Windows gates.
