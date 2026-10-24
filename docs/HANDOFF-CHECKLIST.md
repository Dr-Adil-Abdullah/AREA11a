# HANDOFF CHECKLIST — what was actually checked before this repository was handed over

**Round:** v0.35.2 · Tarragon `#5A6B3B` · code commit `60a6683`, plus the documentation commit that added this page · 24 October 2026
**Purpose:** the owner is about to give this repository to a **new agent**. This page is the receipt:
every line was run on this date, in this tree, and the result is written beside it — including the
things that **could not** be checked here.

> A green tick means *it was run and it passed*. A hollow box `☐` means **not checked in this
> environment** — read the reason; do not assume it works.

---

## 1. The tree is the pushed tree

| ☐ | Check | Command | Result |
|---|---|---|---|
| ✅ | the branch on disk is the one on GitHub | `git fetch origin` + `git status -sb` | `60a6683` = `origin/arena/a8a3b2e0-area11a` — **identical**, nothing uncommitted |
| ✅ | no work was lost when this session's checkout was found on a stale commit (`f0af796`) | `git reset --mixed origin/arena/a8a3b2e0-area11a` | files restored from the remote commit; `git diff` empty |
| ✅ | open pull request | `gh pr list --head …` | **#1 open** — phase 1, foundation + documentation |

## 2. The static gate (44 checks)

| ☐ | Check | Result |
|---|---|---|
| ✅ | `python3 tools/check_repo.py` | **`OK - 44 checks, no problems`** |
| ✅ | every C# file parses with the real grammar (`tree_sitter` + `tree_sitter_c_sharp`) | **265 files, 0 error nodes** |
| ✅ | static member resolution (names, arity, `StaticResource` keys, constructor arity) | **2,265 calls + 3,753 member references, 0 problems** |
| ✅ | explicit `using`s for every type each file names | clean |
| ✅ | XAML / csproj / props are valid XML | clean |
| ✅ | every binding matches its view-model; 377 two-way-by-default bindings checked | clean |
| ✅ | every `StaticResource` / `DynamicResource` key exists | **1,434 keys** |
| ✅ | every SQL statement prepares against the real schema | clean |
| ✅ | every mapped column exists on its model | clean |
| ✅ | migrations executed on SQLite | **23 files → 34 tables** |
| ✅ | version / colour / `docs/VERSIONS.md` agree | **v0.35.2 (Tarragon, `#5A6B3B`)** |
| ✅ | the three script banners agree with the version | 3 files print `v0.35.2 (Tarragon)` |
| ✅ | tests inventory | **717 facts + 12 theories (52 inline rows) = 769 cases in 62 files** |
| ✅ | `HANDOFF.md` §3 and `docs/PROGRESS.md` no longer carry dead "not started" lists | corrected on this date; the board is the only source of feature state |
| ✅ | board = printable tick-list = `PRINT-CHECKLIST.cmd` | **57 features: 52 done, 5 in progress, 0 not started** |
| ✅ | `tools/build_rules_matrix.py --check` | up to date - **116 rules: 90 done, 5 partial, 1 not started (E-12), 20 standing** |
| ✅ | the rules matrix was read back against the code | **43 entries had been stale since Phase 1** and were corrected on this date (returns, quarantine, alerts, return receipt, receipt layout, permission editor, audit viewer, fuzzy search, ESC/POS print, sync …); the six that are genuinely open are named in `TAKEOVER.md` §4 |
| ✅ | documentation links | **22 files, 0 broken links** (re-run after this page and `TAKEOVER.md` were added) |
| ✅ | `.bat` / `.cmd` blocks, labels, `goto` targets | **13 files, 0 problems** |
| ✅ | installer names agree with `InstallService.cs`, icon in the version colour, `dist` ignored | clean |
| ✅ | the launcher refuses a stale folder and explains `MSB3027` / `MSB3021` | clean |
| ✅ | run verdict: `ALL GREEN` only on a full pass, counted against `EXPECTED=769` | clean |
| ✅ | `RunTrail` writes one step per line (`AppendAllLines`, never `AppendAllText`) | new v0.35.2 rule, proved by putting the fault back |
| ✅ | every `Safe(` in `RunTrail` is handed a lambda | v0.35.1 rule, still green |

## 3. What could NOT be checked here — read this before believing anything

| ☐ | Check | Why not | Who proves it |
|---|---|---|---|
| ☐ | `dotnet build` / `dotnet test` | this sandbox is Linux with **no .NET SDK** | **the owner's PC** — his last two builds printed `BUILD OK` (v0.35.1) and `0 Warning(s), 0 Error(s)` (v0.33.0) |
| ☐ | the **769** cases of v0.34.0 … v0.35.2 | same reason; his last green suite was **730/730 on v0.33.0**, and v0.35.1 ran 765 of 769 | his next `tools\RUN-TESTS.cmd` — must print `ALL GREEN - 769 of 769` |
| ☐ | `tools/install/install.ps1` runs to the end | **no PowerShell** in this sandbox; v0.35.1 died at *"Parameter set cannot be resolved…"* | his next `tools\INSTALL-AREA11.cmd` — must list Area11 in *Add or remove programs* and write the desktop icon |
| ☐ | the pages themselves (empty-page cards, fault card, drawer card, session warning) | needs a window and a hand on the keyboard | `HANDOFF.md` §4 walk-throughs |
| ☐ | the five 🟡 features (F-04, F-08, F-09, F-10, F-46) | each needs **one real action in the shop** | the table in `TAKEOVER.md` §4 |

**The honest sentence:** everything that can be proved without Windows is proved; everything that
needs Windows is **written and waiting for his next run**. A green static checker resolves *names*,
not *types* — v0.34.0 shipped a `CS1503` that 44 green checks could not see.

## 4. The handover is complete when these exist

| ☐ | File | What a new agent gets from it |
|---|---|---|
| ✅ | **`TAKEOVER.md`** | the whole brief: state, what is left, non-negotiables, release steps, traps, the first message to the owner |
| ✅ | `docs/HANDOVER-FOR-OWNER-URDU.md` | the same handover in the owner's language, three lines to copy and send |
| ✅ | `docs/PROGRESS.md` | every feature: ID, state, files, test, "done when" |
| ✅ | `docs/OWNER-DECISIONS.md` | D-00 … D-92, his words, authoritative |
| ✅ | `docs/RULES-MATRIX.md` | his 116 numbered rules → where each lives |
| ✅ | `docs/AREA11-MASTER-SPEC.md` | the blueprint |
| ✅ | `docs/SCHEMA-REFERENCE.md` | every table and column |
| ✅ | `docs/VERSIONS.md` | 102 released versions, each colour and why it changed |
| ✅ | `AGENTS.md` / `HANDOFF.md` | how to work here / the long manual |
| ✅ | `docs/BUILD-ON-WINDOWS.md` · `docs/INSTALL.md` | build, test, install, what to send when it fails |
| ✅ | `docs/CHECKLIST-URDU.md` / `.html` | the printable tick-list, generated from the board |
| ✅ | `README.md` | entry point, linking all of the above |

## 5. The five things a new agent must do first (in this order)

1. **Pull, then ask for the owner's console.** `tools\RUN-TESTS.cmd` must say `769 of 769`,
   `tools\INSTALL-AREA11.cmd` must finish. Nothing else matters until those two are seen.
2. **Close the five 🟡 rows** with the owner — one action each, in his shop.
3. **Walk the pages** from `HANDOFF.md` §4 (2 → 14), one at a time, and record what he sees.
4. **Only then** start anything new. No feature is ⬜, so new work is a decision for the owner, not an
   assumption.
5. **Keep the discipline**: one version per round, a new colour and a **fresh** codename, the eight
   bump files, `docs/VERSIONS.md` row, docs updated, `check_repo.py` clean, push, verify the pushed
   zip, then answer him once — in Urdu or English, never Roman Urdu.

---

## 6. Owner's sign-off

| | |
|---|---|
| I have received the repository link and `TAKEOVER.md` | ☐ |
| I have given the link to the new agent | ☐ |
| The new agent's first message answered my console paste | ☐ |
| `ALL GREEN - 769 of 769` seen on my PC | ☐ |
| Area11 listed in *Add or remove programs* with its version | ☐ |
