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
    "U-10": ("WIP", "`Services/SettingsService.cs`; branding screen still TODO", "-"),
    "U-11": ("DONE", "services write straight to SQLite, no second copy", "-"),
    "U-12": ("DONE", "this matrix", "-"),
    "U-13": ("DONE", "HANDOFF.md + AGENTS.md + docs/PROGRESS.md + this file", "`tools/check_repo.py`"),
    "U-14": ("DUTY", "AGENTS.md section 6 (definition of done)", "-"),
    "U-15": ("DUTY", "Phase 1 includes credit sales because the shop needs them", "-"),
    "U-16": ("DONE", "AGENTS.md is the single entry point", "-"),
    "U-18": ("WIP", "`ProductService.AddCategory/AddCompany`, inline supplier in Purchases", "-"),
    "U-19": ("TODO", "migration 015 + `photos` table ready; PhotoService missing", "-"),
    "U-20": ("DONE", "SaleService + `discount.mode`", "SaleTests.RetailDiscount_*"),
    "U-21": ("WIP", "`products.room` exists; photo capture missing", "-"),
    "U-22": ("WIP", "`ProductService.Search` only; filters/groups TODO", "-"),
    "U-23": ("DONE", "every change pushed", "-"),
    "U-24": ("DONE", "docs/PROGRESS.md is the readable done/remaining list", "-"),
    "U-25": ("WIP", "sale warning yes; alert centre TODO (Phase 2)", "SaleTests.NegativeStock_*"),
    "U-26": ("DONE", "`stock_movements` written inside the sale transaction", "SaleTests.Fefo_*"),
    "U-27": ("WIP", "auth/products/sales/purchases audited; other modules pending", "-"),
    "U-28": ("TODO", "Phase 2 - ReturnService", "-"),
    "U-29": ("TODO", "Phase 2 - alert banner on the counter", "-"),
    "U-30": ("WIP", "sale honours `stock.allowNegative`; other operations pending", "SaleTests.NegativeStock_*"),
    "U-31": ("TODO", "Phase 2 - alert dismissals (owner + reason)", "-"),
    "U-32": ("TODO", "Phase 2 - changed by D-22 to quarantine, not straight to stock", "-"),
    "U-33": ("TODO", "Phase 2 - no-bill return", "-"),
    "U-34": ("TODO", "Phase 2 - `Q-0001` numbering", "-"),
    "U-35": ("TODO", "Phase 2 - return receipt", "-"),
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
    "R-09": ("WIP", "barcode gun works as a keyboard; focus handling TODO", "-"),
    "R-10": ("DONE", "`docs/PORT-PLAN.md` + this matrix", "-"),

    # ---- E: everything editable ------------------------------------------
    "E-01": ("WIP", "`settings` table + `SettingsService` (71 keys)", "SettingsTests (planned)"),
    "E-02": ("WIP", "settings keys `brand.name` etc.; branding UI TODO", "-"),
    "E-03": ("TODO", "Phase 1 receipt (`store.*` keys already present)", "-"),
    "E-04": ("WIP", "categories list/create; edit/delete/order TODO", "-"),
    "E-05": ("TODO", "Phase 4 - custom fields", "-"),
    "E-06": ("DONE", "`products.box_strips` / `strip_tablets`", "SaleTests (unit multiplier)"),
    "E-07": ("WIP", "`Utils/Expiry.cs`; threshold parameters still hard-coded (known bug)", "-"),
    "E-08": ("DONE", "`discount.mode`, `discount.maxPercent*`, tier prices", "SaleTests.*Discount*"),
    "E-09": ("WIP", "`bill.roundTo`, `tax.*`; printer size TODO", "MoneyTests.*"),
    "E-10": ("TODO", "Phase 1 receipt layout toggles", "-"),
    "E-11": ("WIP", "`user_permissions` table + `PermissionService`; role editor UI TODO", "PermissionTests.*"),
    "E-12": ("TODO", "Phase 4 - custom labels", "-"),
    "E-13": DUTY,
    "E-14": ("WIP", "soft delete (`active = 0`), old rows untouched", "-"),

    # ---- S: settings live in the database ---------------------------------
    "S-01": ("DONE", "`ProductService.AddCategory/AddCompany`", "-"),
    "S-02": ("WIP", "products/suppliers inline create; categories/companies pending", "-"),
    "S-03": ("DONE", "one `Compute()` drives both Quote and Create", "SaleTests.BillTotal_*"),
    "S-04": ("TODO", "Phase 4", "-"),
    "S-05": ("TODO", "Phase 4", "-"),
    "S-06": ("DONE", "`Data/MigrationRunner.cs`, append-only", "MigrationTests.*"),
    "S-07": ("DONE", "`settings` table", "-"),
    "S-08": ("WIP", "`AuditService`; a few modules still silent", "-"),
    "S-09": ("TODO", "Phase 4 - sync", "-"),

    # ---- P: preview and checking -----------------------------------------
    "P-01": ("WIP", "desktop app - owner runs it after each phase", "-"),
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
    "B-08": ("WIP", "replaced by `tools/check_repo.py` + `tools/build_rules_matrix.py`", "-"),
    "B-09": ("DONE", "fixed session branch, commits/tags for return", "-"),
    "B-10": ("DONE", "tags `net-stage-N` from stage 2 onward", "-"),

    # ---- Z: never break money or data ------------------------------------
    "Z-01": ("DUTY", "build + tests + owner runs it", "-"),
    "Z-02": ("DONE", "regression tests for every money rule", "SaleTests.*, MoneyTests.*"),
    "Z-03": ("DONE", "round-down only downward, never below cost", "MoneyTests.*, SaleTests.RoundDown_*"),
    "Z-04": ("DONE", "single transaction per document, WAL", "SaleTests.*"),
    "Z-05": ("WIP", "audit old->new values; UI undo pending", "-"),
    "Z-06": ("DONE", "SQLite file, no network needed", "-"),
    "Z-07": ("WIP", "printer never blocks the bill (Phase 1 print task)", "-"),
    "Z-08": ("DONE", "one SQLite file + zip backup (Phase 3)", "-"),
    "Z-09": ("WIP", "`audit_log`; viewer TODO (Phase 3)", "AuditDiffTests.*"),
    "Z-10": ("DUTY", "end of each phase", "-"),
    "Z-11": ("DONE", "`Money.RoundDownWithCostGuard`", "MoneyTests.*"),

    # ---- T: reference technology (web app) -> rebuild choice ---------------
    "T-01": ("DONE", "reference only; rebuild is C#/WPF (D-17)", "-"),
    "T-02": ("DONE", "`Resources/Theme.xaml` design system", "-"),
    "T-03": ("DONE", "SQLite via Microsoft.Data.Sqlite + Dapper", "-"),
    "T-04": ("DONE", "desktop is offline by nature", "-"),
    "T-05": ("WIP", "tables done; charts TODO (Phase 3 reports)", "-"),
    "T-06": ("WIP", "validation inside services; form-level messages TODO", "-"),
    "T-07": ("TODO", "fuzzy search (Phase 4)", "-"),
    "T-08": ("TODO", "Phase 1 - ESC/POS instant print", "-"),
    "T-09": ("TODO", "Phase 3 - wa.me link", "-"),
    "T-10": ("TODO", "Phase 3 - ClosedXML", "-"),
    "T-11": ("DUTY", "desktop app", "-"),
    "T-12": ("DONE", "git tags", "-"),
    "T-13": ("DONE", "hand-written SQL migrations 001-015", "-"),
    "T-14": ("DONE", "`dotnet/` is the product; prototypes kept for reference", "-"),

    # ---- M: milestones of the original (now the four phases) ---------------
    "M-01": ("DONE", "foundation, information files, numbered rules", "-"),
    "M-02": ("DONE", "schema + settings + audit + login running", "-"),
    "M-03": ("WIP", "Phase 1: purchases + POS written; receipt/print next", "-"),
    "M-04": ("TODO", "Phase 2: returns, quarantine, alerts, write-off, stock-take", "-"),
    "M-05": ("TODO", "Phase 3: ledgers, reports, backup, reorder/WhatsApp", "-"),
    "M-06": ("TODO", "Phase 4: loyalty, custom fields, split payment", "-"),
    "M-07": ("TODO", "Phase 3/4: audit viewer, stock-take, backup, sync", "-"),
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
