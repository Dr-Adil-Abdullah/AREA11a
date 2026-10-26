#!/usr/bin/env python3
"""Generate docs/RULES-MATRIX.md from the master specification + the status map below.

Why this file exists
--------------------
The owner's rule U-12 says every requirement is numbered, and spec §34.4 demands a
"rules matrix" (rule -> module -> test) where no numbered rule is skipped.  Keeping that
matrix by hand drifts within a week.  So the *text* of every rule is always read fresh from
`docs/AREA11-MASTER-SPEC.md` (the authority), while only the *status* lives here.

Usage
-----
    python3 tools/build_rules_matrix.py          # rewrite docs/RULES-MATRIX.md
    python3 tools/build_rules_matrix.py --check  # fail if the file is out of date

An agent that finishes a rule changes its entry in STATUS below, then runs this script.
If a rule id in the spec has no entry here the script fails loudly - nothing is skipped.
"""

from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = ROOT / "docs" / "AREA11-MASTER-SPEC.md"
OUT = ROOT / "docs" / "RULES-MATRIX.md"

RULE_LINE = re.compile(r"^\s*([0-9.]+)\s+\*\*\[([URESTPBZMQ]-\d+)\]\*\*\s*(.*)$", re.M)

# ---------------------------------------------------------------------------
# Status map.  id -> (state, where, test)
#   state: DONE  = written and covered (compiled on Windows once BUILD says so)
#          WIP   = written, not yet compiled / not yet complete
#          TODO  = not started
#          DUTY  = a standing rule that is obeyed forever, never "finished"
# ---------------------------------------------------------------------------

DUTY = ("DUTY", "process - see AGENTS.md", "-")

STATUS: dict[str, tuple[str, str, str]] = {
    # ---- U: owner's rules -------------------------------------------------
    "U-01": ("DUTY", "desktop app; the owner runs the build (`docs/BUILD-ON-WINDOWS.md`)", "-"),
    "U-02": ("DONE", "git history + tags (no reset that loses work)", "-"),
    "U-03": ("DONE", "this repository", "-"),
    "U-04": ("DUTY", "AGENTS.md read order", "-"),
    "U-05": ("DUTY", "four phases (D-20)", "-"),
    "U-06": ("DUTY", "tests + `tools/check_repo.py` before every commit", "-"),
    "U-07": ("DONE", "Dapper, Microsoft.Data.Sqlite, WPF, xUnit", "-"),
    "U-08": ("DUTY", "one Compute() for quote+save, shared helpers", "-"),
    "U-09": ("DONE", "personal use, MIT-style dependencies only", "-"),
    "U-10": ("DONE", "`Services/SettingsService.cs` + the Settings page (121 keys)", "-"),
    "U-11": ("DONE", "services write straight to SQLite, no second copy", "-"),
    "U-12": ("DONE", "this matrix", "-"),
    "U-13": ("DONE", "HANDOFF.md + AGENTS.md + docs/PROGRESS.md + this file", "`tools/check_repo.py`"),
    "U-14": ("DUTY", "AGENTS.md section 6 (definition of done)", "-"),
    "U-15": ("DUTY", "Phase 1 includes credit sales because the shop needs them", "-"),
    "U-16": ("DONE", "AGENTS.md is the single entry point", "-"),
    "U-18": ("DONE", "`ProductService` categories, companies and shelves; inline supplier in Purchases", "-"),
    "U-19": ("DONE", "`PhotoService`: the file lives in `photos` **beside the database** (shared folder = both counters see it), the database keeps only the name; migration 021 adds the put-away flag", "PhotoTests"),
    "U-20": ("DONE", "SaleService + `discount.mode`", "SaleTests.RetailDiscount_*"),
    "U-21": ("DONE", "`products.room` + photos of the medicine, the customer and the supplier (F-41)", "PhotoTests"),
    "U-22": ("DONE", "`ProductService.Search` with the fuzzy fallback (F-50) and the Products page filters", "ProductTests.*"),
    "U-23": ("DONE", "every change pushed", "-"),
    "U-24": ("DONE", "docs/PROGRESS.md is the readable done/remaining list", "-"),
    "U-25": ("DONE", "superseded for new sales by D-93: StockPolicy refuses below zero; historical alerts remain", "NegativeStockPolicyTests.*"),
    "U-26": ("DONE", "stock movements stay transactional; D-93 supersedes the old empty-batch continuation", "NegativeStockPolicyTests.*"),
    "U-27": ("DONE", "every module writes the audit trail (`AuditService`); one real line in the shop is the board's F-04", "AuditDiffTests.*"),
    "U-28": ("DONE", "`ReturnService` (bill number, quantity and refund guards)", "ReturnTests.*"),
    "U-29": ("DONE", "the frame's alert bar on every page + the counter's own message", "AlertTests.*"),
    "U-30": ("DONE", "superseded for new decrements by D-93: central guard; legacy setting inert; physical-count reconciliation", "NegativeStockPolicyTests.*"),
    "U-31": ("DONE", "`AlertService`: owner only, a reason is compulsory, never deleted", "AlertTests.*"),
    "U-32": ("DONE", "changed by D-22 to quarantine: a returned item is counted, then put away or back", "QuarantineTests.*"),
    "U-33": ("DONE", "`ProvisionalReturnService` (`PR-`), stock at once and the alert stays", "ProvisionalTests.*"),
    "U-34": ("DONE", "`NumberingService` gives every quarantine item its own `Q-0001` number", "QuarantineTests.*"),
    "U-35": ("DONE", "`ReturnReceiptService` prints the return slip with its own number", "ReturnReceiptTests.*"),
    "U-36": ("DUTY", "receipt -> review -> cleanup -> installer order", "-"),

    # ---- R: reuse ---------------------------------------------------------
    "R-01": DUTY,
    "R-02": DUTY,
    "R-03": DUTY,
    "R-04": DUTY,
    "R-05": DUTY,
    "R-06": ("DONE", "`docs/PORT-PLAN.md` section 1", "-"),
    "R-07": ("DONE", "D-17: three projects of our own, no forked repository", "-"),
    "R-08": ("DONE", "Dapper, Microsoft.Data.Sqlite, xUnit, ClosedXML (planned)", "-"),
    "R-09": ("DONE", "the gun types the code and presses Enter; Enter in the POS search box scans it (`PosViewModel.ScanCommand`), one code per pack (D-60)", "-"),
    "R-10": ("DONE", "`docs/PORT-PLAN.md` + this matrix", "-"),

    # ---- E: everything editable ------------------------------------------
    "E-01": ("DONE", "the `settings` table + `SettingsService` (121 keys) and the Settings page; nothing in the shop is hard-coded", "SettingsTests.*"),
    "E-02": ("DONE", "branding keys (`store.*`, `brand.*`) edited on Settings -> Shop", "SettingsTests.*"),
    "E-03": ("DONE", "`ReceiptService` prints `store.*` on every bill", "ReceiptTests.*"),
    "E-04": ("DONE", "`ProductService` + Products → Manage categories and companies: create, rename, two-level parents, order, soft put-away/restore and unlink-only product references; Core permissions and old/new audit; migration 026 orders companies", "CatalogTests.*"),
    "E-05": ("DONE", "`CustomFieldService` + SYSTEM → Own fields (F-42): text, number, amount, date, list, yes/no, photo", "CustomFieldTests"),
    "E-06": ("DONE", "`products.box_strips` / `strip_tablets`", "SaleTests (unit multiplier)"),
    "E-07": ("DONE", "the three bands are read from `expiry.levels` (fixed in v0.3.10, F-29)", "ExpiryTests.*"),
    "E-08": ("DONE", "`discount.mode`, `discount.maxPercent*`, tier prices", "SaleTests.*Discount*"),
    "E-09": ("DONE", "`bill.roundTo`, `tax.*` and the printer width in `print.*`", "MoneyTests.*"),
    "E-10": ("DONE", "`receipt.datePosition` and the `receipt.show*` toggles, edited on Settings", "ReceiptTests.*"),
    "E-11": ("DONE", "`user_permissions` + `PermissionService` + the people and permission editor on Settings", "PermissionTests.*"),
    "E-12": ("WIP", "`AppLabelService` + Settings → App words: 32 stable section labels in `settings`, Unicode/length/single-line guards, Settings permission, old/new audit and safe reset; Windows/live owner proof pending", "AppLabelTests.*"),
    "E-13": DUTY,
    "E-14": ("DONE", "soft delete (`active = 0`) everywhere; no business row is ever deleted", "ProductTests.*"),

    # ---- S: settings live in the database ---------------------------------
    "S-01": ("DONE", "`ProductService.AddCategory/AddCompany`", "-"),
    "S-02": ("DONE", "categories, companies, shelves and suppliers can be created from the page they are needed on", "-"),
    "S-03": ("DONE", "one `Compute()` drives both Quote and Create", "SaleTests.BillTotal_*"),
    "S-04": ("DONE", "a new field appears on the form at once - the panel is built from the database (F-42)", "CustomFieldTests"),
    "S-05": ("WIP", "saved labels use stable keys plus live resources to refresh navigation, all 26 page headings, the Cloud sync tab and Own fields panel without rebuilding the shell; Windows rename/restart proof pending", "AppLabelTests.*"),
    "S-06": ("DONE", "`Data/MigrationRunner.cs`, append-only", "MigrationTests.*"),
    "S-07": ("DONE", "`settings` table", "-"),
    "S-08": ("DONE", "`AuditService` is called by every module - the black box is the proof, not a promise", "AuditDiffTests.*"),
    "S-09": ("DONE", "`SyncService`: push, preview and bring back, with a backup before any write and no deletion", "SyncTests.*"),

    # ---- P: preview and checking -----------------------------------------
    "P-01": ("DONE", "a desktop program; the owner builds and runs it himself", "-"),
    "P-02": ("DUTY", "desktop app: the owner's own screen is the preview", "-"),
    "P-03": ("DUTY", "end of every phase", "-"),
    "P-04": ("DONE", "`tools/check_repo.py` (static checks in this sandbox)", "-"),
    "P-05": ("DUTY", "BUILD-ON-WINDOWS.md + check_repo.py", "-"),

    # ---- B: save points ---------------------------------------------------
    "B-01": ("DONE", "git commit before each stage (commit messages carry `net-stage-N`)", "-"),
    "B-02": ("DONE", "git tags / commits", "-"),
    "B-03": ("DONE", "`git revert` / `git checkout <commit> -- <file>`", "-"),
    "B-04": ("DONE", "`git log`, `git diff`, `git status`", "-"),
    "B-05": ("DONE", "Arena snapshots + commits after every change", "-"),
    "B-06": ("DONE", "linear history, never force-push to main", "-"),
    "B-07": ("DONE", "pushed to GitHub after every change", "-"),
    "B-08": ("DONE", "replaced by `tools/check_repo.py` (49 checks) + `tools/build_rules_matrix.py`", "`tools/check_repo.py`"),
    "B-09": ("DONE", "fixed session branch, commits/tags for return", "-"),
    "B-10": ("DONE", "tags `net-stage-N` from stage 2 onward", "-"),

    # ---- Z: never break money or data ------------------------------------
    "Z-01": ("DUTY", "build + tests + owner runs it", "-"),
    "Z-02": ("DONE", "regression tests for every money rule", "SaleTests.*, MoneyTests.*"),
    "Z-03": ("DONE", "round-down only downward, never below cost", "MoneyTests.*, SaleTests.RoundDown_*"),
    "Z-04": ("DONE", "single transaction per document, WAL", "SaleTests.*"),
    "Z-05": ("WIP", "`AuditSnapshot` + selected Black box row: complete read-only before/after/details and guidance to existing audited corrections; no universal financial undo; Windows/live owner proof pending", "AuditViewerTests.EarlierState_*"),
    "Z-06": ("DONE", "SQLite file, no network needed", "-"),
    "Z-07": ("DONE", "a printer fault is reported and the bill is saved anyway - the paper never decides the sale", "PrintTests.*"),
    "Z-08": ("DONE", "one SQLite file + zip backup (Phase 3)", "-"),
    "Z-09": ("DONE", "`audit_logs` + the Black box page (search, filters, CSV)", "AuditDiffTests.*"),
    "Z-10": ("DUTY", "end of each phase", "-"),
    "Z-11": ("DONE", "`Money.RoundDownWithCostGuard`", "MoneyTests.*"),

    # ---- T: reference technology (web app) -> rebuild choice ---------------
    "T-01": ("DONE", "reference only; rebuild is C#/WPF (D-17)", "-"),
    "T-02": ("DONE", "`Resources/Theme.xaml` design system", "-"),
    "T-03": ("DONE", "SQLite via Microsoft.Data.Sqlite + Dapper", "-"),
    "T-04": ("DONE", "desktop is offline by nature", "-"),
    "T-05": ("DONE", "native WPF per-day bar chart (`ReportDayRow.BarShare` + `ProgressBar`) and DataGrid tables; Recharts/TanStack are reference-web libraries; stale no-chart status corrected after actual-code review", "ReportTests.PerDay*"),
    "T-06": ("DONE", "a service refuses with a sentence the cashier can act on, and the page shows it beside the field", "SaleTests.*"),
    "T-07": ("DONE", "misspelt search (F-50): `panadl` finds Panadol, and Enter never adds a guess", "ProductTests.*"),
    "T-08": ("DONE", "`EscPosBuilder` + `PrinterService`; the owner printed on the shop printer on 23 Oct 2026", "PrintTests.*"),
    "T-09": ("DONE", "the Reorder pad builds the order text and opens WhatsApp (F-38)", "ReorderTests.*"),
    "T-10": ("WIP", "dependency-free native `.xlsx` read/write, multi-sheet templates and eight-sheet all-data/bulk update are written with protected stock/balances/Settings and 14 facts; Windows/xUnit and real Excel open/edit/re-import proof pending", "WorkbookTests.*; ImportTests.*"),
    "T-11": ("DUTY", "desktop app", "-"),
    "T-12": ("DONE", "git tags", "-"),
    "T-13": ("DONE", "hand-written SQL migrations 001-015", "-"),
    "T-14": ("DONE", "`dotnet/` is the product; prototypes kept for reference", "-"),

    # ---- M: milestones of the original (now the four phases) ---------------
    "M-01": ("DONE", "foundation, information files, numbered rules", "-"),
    "M-02": ("DONE", "schema + settings + audit + login running", "-"),
    "M-03": ("DONE", "purchases, the counter and the receipt are written and the owner prints", "SaleTests.*"),
    "M-04": ("DONE", "returns, quarantine, alerts, write-off and stock-take", "ReturnTests.*"),
    "M-05": ("DONE", "ledgers, reports, backup, Excel/CSV, reorder/WhatsApp and the stock-minus registry; the sample list was deleted on the owner's word (D-77)", "ReportTests.*"),
    "M-06": ("DONE", "loyalty, photos, own fields, barcodes, reps/doctors, the LAN second counter, the installer and the hardware test - its scan half still waits for the shop", "HardwareTests.*"),
    "M-07": ("DONE", "the black box, stock-take, backup and cloud sync", "BackupTests.*"),
    "M-08": ("DONE", "settings and data-driven from the first migration", "-"),
}

PHASE_OF = {
    "U-25": "Phase 2", "U-27": "Phase 3", "U-28": "Phase 2", "U-29": "Phase 2",
    "U-30": "Phase 2", "U-31": "Phase 2", "U-32": "Phase 2", "U-33": "Phase 2",
    "U-34": "Phase 2", "U-35": "Phase 2", "E-03": "Phase 1", "E-05": "Phase 4",
    "E-07": "Phase 2", "E-10": "Phase 1", "E-12": "Phase 4", "S-09": "Phase 4",
    "T-05": "Phase 3", "T-07": "Phase 4", "T-08": "Phase 1", "T-09": "Phase 3",
    "T-10": "Phase 3", "U-19": "Phase 4", "M-04": "Phase 2", "M-05": "Phase 3",
    "M-06": "Phase 4", "M-07": "Phase 3",
}

BADGE = {
    "DONE": "✅ done",
    "WIP": "🟡 written / partial",
    "TODO": "⬜ not started",
    "DUTY": "🔁 standing rule",
}


def clean(text: str) -> str:
    text = re.sub(r"\*\*|`", "", text)
    text = re.sub(r"\s+", " ", text).strip()
    # keep the table on one line and escape pipes
    return text.replace("|", "\\|")


def main() -> int:
    spec = SPEC.read_text(encoding="utf-8")
    rules = [(m.group(2), clean(m.group(3))) for m in RULE_LINE.finditer(spec)]

    missing = [rid for rid, _ in rules if rid not in STATUS]
    unknown = [rid for rid in STATUS if rid not in {r for r, _ in rules}]
    if missing:
        print("ERROR: rules in the spec with no status entry:", ", ".join(missing))
        return 1
    if unknown:
        print("ERROR: status entries for rules that no longer exist:", ", ".join(sorted(unknown)))
        return 1

    counts = {k: 0 for k in BADGE}
    for rid, _ in rules:
        counts[STATUS[rid][0]] += 1

    lines = [
        "# RULES MATRIX - every numbered rule -> where it lives -> its test",
        "",
        "> **Generated file - do not edit by hand.** The rule text is read from",
        "> [`AREA11-MASTER-SPEC.md`](AREA11-MASTER-SPEC.md) by `tools/build_rules_matrix.py`;",
        "> only the status table inside that script is edited. Run it again after changing a rule's",
        "> state: `python3 tools/build_rules_matrix.py`.",
        "",
        "Rules are the owner's own numbering (spec section 3). **No rule may be skipped** (spec 30.1.4).",
        "",
        "## Legend",
        "",
        "| Badge | Meaning |",
        "|---|---|",
        "| ✅ done | written, and covered by a test where a test is possible |",
        "| 🟡 written / partial | code exists but is incomplete, or has not yet been compiled on Windows |",
        "| ⬜ not started | planned, not written |",
        "| 🔁 standing rule | a way of working that must be obeyed in every session - never \"finished\" |",
        "",
        "**Read this together with [`PROGRESS.md`](PROGRESS.md)** - that file tracks features and phases,",
        "this file tracks the owner's numbered rules.",
        "",
        "## Summary",
        "",
        f"- rules in the specification: **{len(rules)}**",
        f"- ✅ done: **{counts['DONE']}**",
        f"- 🟡 written / partial: **{counts['WIP']}**",
        f"- ⬜ not started: **{counts['TODO']}**",
        f"- 🔁 standing rules: **{counts['DUTY']}**",
        "",
    ]

    groups = {
        "U": ("U - the owner's rules", "Product, process and the owner's direct orders."),
        "R": ("R - reuse before writing", "What to reuse and what was decided about libraries."),
        "E": ("E - everything is editable", "Nothing hard-coded; every label, price and rule from settings."),
        "S": ("S - front end reaches the data", "One source of truth: a change on screen is a change in the database."),
        "P": ("P - preview and checking", "The owner must be able to look at the running app."),
        "B": ("B - save points and going back", "History is never lost."),
        "Z": ("Z - money and data are never broken", "The hard guarantees."),
        "T": ("T - technology", "What the original used, and what the rebuild uses instead."),
        "M": ("M - milestones", "The original's five phases, mapped to this rebuild's four."),
    }

    for prefix, (title, blurb) in groups.items():
        subset = [(rid, text) for rid, text in rules if rid.startswith(prefix + "-")]
        if not subset:
            continue
        lines += [
            f"## {title}",
            "",
            blurb,
            "",
            "| Rule | Requirement | State | Where it lives | Test | Phase |",
            "|---|---|---|---|---|---|",
        ]
        for rid, text in subset:
            state, where, test = STATUS[rid]
            phase = PHASE_OF.get(rid, "-")
            lines.append(
                f"| **{rid}** | {text} | {BADGE[state]} | {where} | {test} | {phase} |"
            )
        lines.append("")

    content = "\n".join(lines) + "\n"

    if "--check" in sys.argv:
        current = OUT.read_text(encoding="utf-8") if OUT.exists() else ""
        if current != content:
            print("docs/RULES-MATRIX.md is out of date - run tools/build_rules_matrix.py")
            return 1
        print("rules matrix is up to date")
        return 0

    OUT.write_text(content, encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)} ({len(rules)} rules: "
          f"{counts['DONE']} done, {counts['WIP']} partial, {counts['TODO']} todo, {counts['DUTY']} standing)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
