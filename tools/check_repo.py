#!/usr/bin/env python3
"""Static checks for the Area11 repository.

Why: the area this project is built in has no .NET SDK, so a compile is impossible here.
This script catches the class of mistakes that used to reach the owner's Windows build:
a C# syntax slip, a XAML typo, a binding to a property that does not exist, a missing
resource, a broken migration, a documentation link to a file that was deleted.
It is not a compiler and does not replace `dotnet build`.

Usage:
    python3 tools/check_repo.py            # human readable report, non-zero exit on failure
    python3 tools/check_repo.py --quiet    # only failures
"""

from __future__ import annotations

import pathlib
import re
import sys
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]

# The double-click scripts that tell the owner WHICH build is about to run. v0.30.1 taught this
# lesson: a banner that parsed C# printed "vunknown" on his PC. Each script holds one literal, and
# the version rule below compares every one of them with BuildInfo.cs - so a new script only has
# to be added to this tuple to be kept honest.
SCRIPTS_THAT_NAME_THE_BUILD = (
    "tools/RUN-TESTS.cmd",
    "START-AREA11.bat",
    "tools/SEND-REPORT.cmd",
)
DOTNET = ROOT / "dotnet"

FAILURES: list[str] = []
WARNINGS: list[str] = []
REPORT: list[str] = []

# XAML files bind both to their page view-model and to row items inside DataGrid/ItemsControl.
# These names belong to the item type, not to the page view-model, so they are not checked.
ITEM_PROPERTIES = {
    "SectionKey", "DataType", "InputMethod", "Required", "Critical", "PickedDate",
    "Title", "Subtitle", "Name", "Generic", "RackNo", "StockLabel", "RetailText", "CostText",
    "MarginLabel", "UnitLabel", "QuantityText", "BatchNo", "Expiry", "ProductName", "Category",
    "Company", "Photo", "Phone", "BalanceText", "Stars", "CategoryLabel", "ActiveLabel",
    "Label", "Allowed", "CanEdit", "RoleLabel",
    "SoldLabel", "ReturnedLabel", "RemainingLabel", "MoneyLabel", "QtyText", "CanReturn",
    "QCode", "ProductName", "QuantityLabel", "ReasonLabel", "DateLabel", "DispositionLabel",
    "DisposedAt", "DisposedBy",
    "TypeLabel", "Detail", "Reason", "EndedAt",
    "StockLabel", "PackLabel", "Name", "Quantity", "Unit", "CustomerLabel", "RefundText",
    "Code", "DateLabel", "TotalText", "ItemsLabel", "StatusLabel",
    "BatchNo", "ExpiryDate", "ExpiryLabel", "ValueLabel", "QtyLabel", "SignedQtyLabel",
    "ReasonLabel", "ProductName", "DateLabel", "UserName",
    "BookLabel", "CountedLabel", "DiffLabel", "WhereLabel", "CountedByLabel",
    "ShortLabel", "ExtraLabel", "ProgressLabel", "StatusLabel", "SessionCaption",
}

VIEW_MODEL_OF = {
    "NativeFieldsView.xaml": "NativeFieldsViewModel",
    "LoginWindow.xaml": "LoginViewModel",
    "ShellWindow.xaml": "ShellViewModel",
    "DashboardView.xaml": "DashboardViewModel",
    "ProductsView.xaml": "ProductsViewModel",
    "ProductEditorWindow.xaml": "ProductsViewModel",
    "PosView.xaml": "PosViewModel",
    "LinePricingWindow.xaml": "LinePricingViewModel",
    "PermissionDetailsWindow.xaml": "PermissionDetailsViewModel",
    "PageFaultView.xaml": "PageFaultViewModel",
    "PurchasesView.xaml": "PurchasesViewModel",
    "SettingsView.xaml": "SettingsViewModel",
    "ReturnsView.xaml": "ReturnsViewModel",
    "AlertsView.xaml": "AlertsViewModel",
    "ProvisionalView.xaml": "ProvisionalViewModel",
    "WriteOffView.xaml": "WriteOffViewModel",
    "StockTakeView.xaml": "StockTakeViewModel",
    "CustomersView.xaml": "CustomersViewModel",
    "SuppliersView.xaml": "SuppliersViewModel",
    "ShiftView.xaml": "ShiftViewModel",
    "ExpensesView.xaml": "ExpensesViewModel",
    "RecoveryView.xaml": "RecoveryViewModel",
    "PartnersView.xaml": "PartnersViewModel",
    "ReportsView.xaml": "ReportsViewModel",
    "AuditView.xaml": "AuditViewModel",
    "BackupView.xaml": "BackupViewModel",
    "ImportExportView.xaml": "ImportExportViewModel",
    "ReorderView.xaml": "ReorderViewModel",
    "StockMinusView.xaml": "StockMinusViewModel",
    "PhotoGalleryWindow.xaml": "PhotoGalleryViewModel",
    "CustomFieldsView.xaml": "CustomFieldsViewModel",
    "CustomFieldsEditorView.xaml": "CustomFieldsEditorViewModel",
    "BarcodeWindow.xaml": "BarcodeListViewModel",
    "CatalogWindow.xaml": "CatalogWindowViewModel",
    "DoctorsView.xaml": "DoctorsViewModel",
    "LanView.xaml": "LanViewModel",
    "TaxView.xaml": "TaxViewModel",
    "HardwareView.xaml": "HardwareViewModel",
    "CloudSyncView.xaml": "CloudSyncViewModel",
    "SaleHistoryView.xaml": "SaleHistoryViewModel",
    "BookCheckView.xaml": "BookCheckViewModel",
    "TimeCorrectionWindow.xaml": "TimeCorrectionViewModel",
}


def fail(message: str) -> None:
    FAILURES.append(message)


def warn(message: str) -> None:
    WARNINGS.append(message)


# ---------------------------------------------------------------------------
# 7b. The launcher must know which program it is about to replace
# ---------------------------------------------------------------------------
def check_running_app() -> None:
    """The build cannot replace a DLL that a running Area11 has loaded.

    Why this exists: on 23 Oct 2026 the owner's build stopped with **MSB3027 - "Failed. The file
    is locked by: Area11 (8460)"**. Nothing was wrong with the code: the program from the previous
    run was still open behind the window, and Windows does not let a build overwrite a file in
    use. He sent four error lines that all said the same thing, and no compiler message can be
    fixed because there was no compiler fault.

    So START-AREA11.bat closes the old copy first (after asking), and the build failure path
    recognises MSB3027/MSB3021 and says one plain sentence instead of a wall. This gate keeps
    that guard honest, in the two ways it can silently stop working:

      * the process name in the script must equal `<AssemblyName>` in the desktop project - a
        rename would leave a guard looking for a program that no longer exists, which is the same
        as no guard;
      * the script must still carry the guard at all, and still recognise the two error codes.

    A gate that only looks at the C# cannot see any of this: the failure lives in a batch file and
    in the Windows file system, between the compiler and the shop.
    """
    project = DOTNET / "Area11.Desktop" / "Area11.Desktop.csproj"
    launcher = ROOT / "START-AREA11.bat"

    if not project.exists() or not launcher.exists():
        fail("START-AREA11.bat or Area11.Desktop.csproj is missing - the launcher guard cannot be checked")
        return

    csproj = project.read_text(encoding="utf-8-sig")
    script = launcher.read_text(encoding="utf-8", errors="replace")

    match = re.search(r"<AssemblyName>\s*([^<\s]+)\s*</AssemblyName>", csproj)
    if not match:
        fail("Area11.Desktop.csproj has no <AssemblyName> - the launcher cannot name the program it closes")
        return

    assembly = match.group(1).strip()

    named = re.search(r'set\s+"APPPROCESS=([^"]+)"', script, re.I)
    if not named:
        fail("START-AREA11.bat does not set APPPROCESS - a running Area11 would fail the build with MSB3027")
        return

    process = named.group(1).strip()

    if process.lower() != (assembly + ".exe").lower():
        fail(f"START-AREA11.bat closes '{process}' but the program is built as '{assembly}.exe' - "
             f"the guard would look for the wrong process and the build would lock again")

    if "tasklist" not in script.lower():
        fail("START-AREA11.bat names the program but never asks Windows whether it is running (tasklist)")

    if "taskkill" not in script.lower():
        fail("START-AREA11.bat can see a running Area11 but never closes it (taskkill)")

    for code in ("MSB3027", "MSB3021"):
        if code not in script:
            fail(f"START-AREA11.bat does not recognise {code} - the lock error would be shown "
                 f"as a compiler fault, which it is not")

    REPORT.append(f"Launcher: closes '{process}' (matches the project's AssemblyName) "
                  f"and explains MSB3027 / MSB3021 itself")


# ---------------------------------------------------------------------------
# 1. C#
# ---------------------------------------------------------------------------
def check_csharp() -> None:
    files = sorted(DOTNET.rglob("*.cs"))
    if not files:
        fail("no C# files found under dotnet/")
        return

    try:
        from tree_sitter import Language, Parser  # type: ignore
        import tree_sitter_c_sharp  # type: ignore

        parser = Parser(Language(tree_sitter_c_sharp.language()))
        mode = "tree-sitter"
    except Exception:  # noqa: BLE001 - any import problem means "fall back"
        parser = None
        mode = "brace-balance"

    broken = 0
    for path in files:
        source = path.read_bytes()
        text = source.decode("utf-8", errors="replace")

        # Database owns short-lived connections but is not IDisposable itself. A using declaration
        # here is CS1674, which a grammar-only parser quite correctly does not report as bad syntax.
        lifetime = re.search(
            r"\busing\s*\(?\s*(?:var|Database)\s+[A-Za-z_]\w*\s*=\s*new\s+Database\s*\(",
            mask_csharp_noise(text))
        if lifetime is not None:
            broken += 1
            line = text.count("\n", 0, lifetime.start()) + 1
            fail(f"{rel(path)}:{line}: Database is not IDisposable; use a plain variable and dispose "
                 "the connections returned by Database.Open() instead")
            continue

        if parser is not None:
            tree = parser.parse(source)
            if tree.root_node.has_error:
                broken += 1
                line = first_error_line(tree.root_node, source)
                fail(f"C# syntax error: {rel(path)}:{line}")
            continue

        text = strip_csharp_noise(text)
        if text.count("{") != text.count("}") or text.count("(") != text.count(")"):
            broken += 1
            fail(f"C# unbalanced braces/parentheses: {rel(path)}")

    REPORT.append(f"C#: {len(files)} files parsed with {mode}, {broken} with errors")


def strip_csharp_noise(text: str) -> str:
    """Remove comments and string/char literals so brace counting is meaningful."""
    out: list[str] = []
    i = 0
    length = len(text)
    while i < length:
        ch = text[i]

        if ch == "/" and i + 1 < length and text[i + 1] == "/":
            i = text.find("\n", i)
            if i == -1:
                break
            continue

        if ch == "/" and i + 1 < length and text[i + 1] == "*":
            start = i
            i = text.find("*/", i + 2)
            i = length if i == -1 else i + 2
            # Keep the line count identical, so reported line numbers match the real file.
            out.append("\n" * text.count("\n", start, i))
            continue

        if ch == '"':
            # raw string literal """..."""
            if text.startswith('"""', i):
                end = text.find('"""', i + 3)
                i = length if end == -1 else end + 3
                out.append('""')
                continue
            i += 1
            while i < length:
                if text[i] == "\\":
                    i += 2
                    continue
                if text[i] == '"':
                    i += 1
                    break
                i += 1
            out.append('""')
            continue

        if ch == "'":
            i += 1
            while i < length:
                if text[i] == "\\":
                    i += 2
                    continue
                if text[i] == "'":
                    i += 1
                    break
                i += 1
            out.append("''")
            continue

        out.append(ch)
        i += 1

    return "".join(out)


def mask_csharp_noise(text: str) -> str:
    """Blank out comments and string literals but keep every offset and newline in place.

    Why a second masker: `strip_csharp_noise` shortens the text, so a reported offset no longer maps
    to the line the owner sees in the build output. This version replaces the removed characters
    with spaces (newlines kept), so `text.count("\n", 0, offset)` is the true line number.
    """
    chars = list(text)
    length = len(text)
    i = 0

    def blank(start: int, end: int) -> None:
        for position in range(start, min(end, length)):
            if chars[position] != "\n":
                chars[position] = " "

    while i < length:
        ch = text[i]

        if ch == "/" and i + 1 < length and text[i + 1] == "/":
            end = text.find("\n", i)
            end = length if end == -1 else end
            blank(i, end)
            i = end
            continue

        if ch == "/" and i + 1 < length and text[i + 1] == "*":
            end = text.find("*/", i + 2)
            end = length if end == -1 else end + 2
            blank(i, end)
            i = end
            continue

        if ch == '"':
            end = i + 1
            while end < length:
                if text[end] == "\\":
                    end += 2
                    continue
                if text[end] == '"':
                    end += 1
                    break
                end += 1
            blank(i, end)
            i = end
            continue

        if ch == "'":
            end = i + 1
            while end < length:
                if text[end] == "\\":
                    end += 2
                    continue
                if text[end] == "'":
                    end += 1
                    break
                end += 1
            blank(i, end)
            i = end
            continue

        i += 1

    return "".join(chars)


def first_error_line(node, source: bytes) -> int:
    stack = [node]
    while stack:
        current = stack.pop()
        if current.type == "ERROR" or current.is_missing:
            return current.start_point[0] + 1
        stack.extend(current.children)
    return 0


# ---------------------------------------------------------------------------
# 1b. Explicit usings: the CS0103 class of error
# ---------------------------------------------------------------------------
# Reserved words that can never name a type. Kept short: only the ones a `class|record|enum|...`
# scan can actually mistake for a declaration (contextual keywords used as plain identifiers).
CSHARP_KEYWORDS = {
    "in", "is", "as", "of", "on", "into", "when", "where", "select", "from", "let", "order",
    "group", "by", "ascending", "descending", "equals", "and", "or", "not", "with", "init",
    "get", "set", "value", "var", "new", "return", "if", "else", "for", "foreach", "while",
    "do", "switch", "case", "default", "break", "continue", "try", "catch", "finally", "throw",
    "using", "namespace", "class", "record", "struct", "enum", "interface", "public", "private",
    "protected", "internal", "static", "readonly", "const", "void", "true", "false", "null"
}

NEEDED_NAMESPACES = {
    "System.IO": ("Path", "File", "Directory", "FileInfo", "DirectoryInfo", "Stream", "FileStream",
                  "StreamReader", "StreamWriter", "MemoryStream", "FileMode", "FileAccess",
                  "SearchOption", "FileShare", "IOException"),
    "System.Text": ("StringBuilder", "Encoding"),
    "System.Text.Json": ("JsonSerializer", "JsonDocument", "JsonElement"),
    "System.Globalization": ("CultureInfo",),
    "System.Security.Cryptography": ("RandomNumberGenerator", "SHA256", "HMACSHA256"),
    "Microsoft.Data.Sqlite": ("SqliteConnection", "SqliteTransaction", "SqliteCommand",
                              "SqliteParameter", "SqliteConnectionStringBuilder"),
    "System.Data": ("IDbConnection", "CommandType", "DbType"),
}


def check_usings() -> None:
    """A file may not rely on ImplicitUsings for the namespaces above.

    Why: the owner's first build failed with CS0103 ('Path'/'Directory' does not exist) because the
    files used System.IO without importing it. Explicit usings compile on every SDK and every
    project setting, which is what a shop computer needs.
    """
    files = sorted(DOTNET.rglob("*.cs")) + sorted((ROOT / "prototypes").rglob("*.cs"))
    offenders = 0
    for path in files:
        if "obj" in path.parts or "bin" in path.parts:
            continue
        text = path.read_text(encoding="utf-8-sig")
        body = re.sub(r"^\s*using [^\n]+$", "", text, flags=re.M)
        # a fully qualified use (Microsoft.Data.Sqlite.SqliteConnection) already names its namespace
        for namespace, types in NEEDED_NAMESPACES.items():
            if f"using {namespace};" in text:
                continue
            if any(re.search(rf"(?<![\w.]){re.escape(name)}\s*[.(<]", body)
                   or re.search(rf"(?<![\w.]){re.escape(name)}\s+\w", body) for name in types):
                offenders += 1
                fail(f"{rel(path)} uses {namespace} types without 'using {namespace};'")

    # ---- second pass: the project's own namespaces ----
    # CS0103 does not care that a type is "ours". `AuditViewModel` used `AuditHistoryService` (in
    # Area11.Core.Services) with no `using Area11.Core.Services;` and the Desktop project stopped at
    # v0.6.0 - the checker had only ever known about System.IO and friends.
    #
    # Visibility rules used here, so there are no false alarms:
    #   • a type in the file's OWN namespace needs no using;
    #   • a type in an ENCLOSING namespace is visible too (Area11.Desktop.AppServices from
    #     Area11.Desktop.ViewModels), so a namespace that is a prefix of the file's namespace is fine;
    #   • a type declared in the same PROJECT is skipped - a same-named class in our own assembly
    #     wins over the Core one, so mentioning it is not an error;
    #   • comments and strings are masked first, so "produced by SaleService.Quote" in a doc comment
    #     is prose, not a missing using.
    declared_ns: dict[str, str] = {}       # type -> namespace
    project_of: dict[str, str] = {}        # type -> project folder

    for path in sorted(DOTNET.rglob("*.cs")):
        if "obj" in path.parts or "bin" in path.parts:
            continue

        masked_text = mask_csharp_noise(path.read_text(encoding="utf-8-sig"))
        namespace = re.search(r"namespace\s+([\w.]+)\s*[;{]", masked_text)

        if namespace is None:
            continue

        project = path.relative_to(DOTNET).parts[0]

        # `record` is a CONTEXTUAL keyword: `foreach (var record in _rows)` reads exactly like a
        # declaration to a regex. That minted a type called `in` in Area11.Core.Services, and then
        # every `foreach (... in ...)` in the Desktop project "used" it - six false failures the day
        # the mention patterns below were widened (v0.29.0). A reserved word is never a type name, so
        # the keywords are skipped here once, for every rule that reads this table.
        #
        # Only NAMESPACE-level types are registered. A private nested `Counts` inside a service is
        # invisible outside that service, so treating it as `Area11.Core.Services.Counts` made a
        # property called `Counts` in another file look like a missing using (the second false alarm
        # of the same day). Depth tells the two apart: with a file-scoped namespace (`namespace X;`)
        # a top-level type sits at depth 0, with a block namespace (`namespace X {`) at depth 1.
        file_scoped = re.search(r"namespace\s+[\w.]+\s*;", masked_text) is not None
        top_level_depth = 0 if file_scoped else 1

        # One pass of brace depth for the whole file, then O(1) per declaration. Counting the
        # braces before every match instead made this check three times slower (12 s -> 34 s),
        # and it runs on every push.
        depth_now = 0
        depth_at = [0] * (len(masked_text) + 1)
        positions = masked_text

        for position, character in enumerate(positions):
            depth_at[position] = depth_now

            if character == "{":
                depth_now += 1
            elif character == "}":
                depth_now -= 1

        depth_at[len(positions)] = depth_now

        for match in re.finditer(r"\b(?:class|record|enum|interface|struct)\s+(\w+)", masked_text):
            name = match.group(1)

            if name in CSHARP_KEYWORDS:
                continue

            if depth_at[match.start()] != top_level_depth:
                continue

            declared_ns.setdefault(name, namespace.group(1))
            project_of.setdefault(name, project)

    for path in files:
        if "obj" in path.parts or "bin" in path.parts or DOTNET not in path.parents:
            continue

        text = path.read_text(encoding="utf-8-sig")
        masked_text = mask_csharp_noise(text)
        namespace_match = re.search(r"namespace\s+([\w.]+)\s*[;{]", masked_text)

        if namespace_match is None:
            continue

        own_ns = namespace_match.group(1)
        project = path.relative_to(DOTNET).parts[0]

        # Types declared IN THIS FILE (a nested row class, a private helper) are always visible.
        own_types = set(re.findall(r"\b(?:class|record|enum|interface|struct)\s+(\w+)", masked_text))

        # Every identifier in the file, once. Testing 450 declared types against the whole text, three
        # patterns each, for 292 files is ~400k regex runs (34 s per check); this set drops the types
        # the file never names, and the precise patterns below only run on the few that remain.
        words = set(re.findall(r"[A-Za-z_]\w*", masked_text))

        for name, type_ns in declared_ns.items():
            if name not in words:
                continue

            # A same-PROJECT type used to be skipped here, on the theory that "our own assembly wins".
            # That is true for ambiguity and false for visibility: `DashboardService` (in
            # Area11.Core.Services) used `User` (in Area11.Core.Models) with no `using`, and the
            # checker stayed silent while the compiler would have said CS0246 - caught by hand at
            # v0.29.0. A namespace is what makes a type visible, so only the namespace rules apply.
            if name in own_types:
                continue

            if own_ns == type_ns or own_ns.startswith(type_ns + "."):
                continue

            if f"using {type_ns};" in text:
                continue

            # Three ways a type is named in code. The third one is why this rule once went blind:
            # `GetSnapshot(User? user)` puts a `?` where the old pattern wanted `.`, `(` or `<`, so a
            # whole service could use a Core type with no `using` and the checker stayed quiet
            # (found by hand at v0.29.0, then reproduced as a failing check before the fix).
            mention = (re.search(rf"(?<![\w.]){re.escape(name)}\s*[.(<]", masked_text)
                       or re.search(rf"(?<![\w.]){re.escape(name)}\??\s+[A-Za-z_]", masked_text)
                       or re.search(rf"(?<![\w.]){re.escape(name)}\?", masked_text))

            if mention:
                offenders += 1
                line = masked_text[:mention.start()].count("\n") + 1
                fail(f"{rel(path)}:{line}: uses '{name}' from {type_ns} without "
                     f"'using {type_ns};' (CS0103)")

    REPORT.append(f"Usings: {len(files)} files, {offenders} missing imports")


# ---------------------------------------------------------------------------
# 1c. One rounding helper only (CS0121)
# ---------------------------------------------------------------------------
ROUNDING_ALLOWED = {"Calc.cs", "Money.cs"}


def check_rounding() -> None:
    """Raw Math.Round is forbidden outside the two money helpers.

    Why: `Math.Round(value, MidpointRounding.AwayFromZero)` is overloaded for double and decimal,
    so an integral argument (int/long) is a compile error CS0121 - which is exactly what the
    owner's build hit in SaleService.cs line 337. Everything now calls Calc.Round / Calc.RoundMoney.
    """
    offenders = 0
    for path in sorted(DOTNET.rglob("*.cs")):
        if "obj" in path.parts or "bin" in path.parts:
            continue
        if path.name in ROUNDING_ALLOWED:
            continue
        text = path.read_text(encoding="utf-8-sig")
        code = re.sub(r"//[^\n]*", "", text)
        code = re.sub(r"/\*.*?\*/", "", code, flags=re.S)
        if "Math.Round" in code:
            offenders += 1
            fail(f"{rel(path)} calls Math.Round directly - use Calc.Round (double) or "
                 f"Calc.RoundMoney (decimal) to avoid the CS0121 ambiguity")

    REPORT.append(f"Rounding: raw Math.Round allowed only in {sorted(ROUNDING_ALLOWED)}, "
                  f"{offenders} other file(s) use it")


# ---------------------------------------------------------------------------
# 1d. Every solution entry points at a file that exists
# ---------------------------------------------------------------------------
def check_solutions() -> None:
    """A .sln that names a missing .csproj is what Visual Studio reports as
    "The project file could not be loaded" - the owner lost a build to this once, because an old
    Area11.WPF.sln was still lying next to the new solution.
    """
    solutions = sorted(ROOT.glob("*.sln")) + sorted((ROOT / "prototypes").glob("*.sln"))
    total = 0
    for solution in solutions:
        text = solution.read_text(encoding="utf-8-sig", errors="replace")
        pattern = r'Project\("\{[^}]+\}"\)\s*=\s*"[^"]*",\s*"([^"]+)"'
        for relative in re.findall(pattern, text):
            project = (solution.parent / relative.replace("\\", "/")).resolve()
            total += 1
            if not project.exists():
                fail(f"{rel(solution)} points at a missing project: {relative}")

    REPORT.append(f"Solutions: {len(solutions)} files, {total} project references, all present")


# ---------------------------------------------------------------------------
# 1e. The repository skeleton is complete
# ---------------------------------------------------------------------------
def check_skeleton() -> None:
    """The files that make the project startable must all be present.

    Why: one download arrived with docs, dotnet and tools but without Area11.sln, so nothing could
    build and the symptoms looked like compiler errors. A missing skeleton file is now reported as
    what it is.
    """
    required = [
        "Area11.sln", "START-AREA11.bat", "README.md", "HANDOFF.md", "AGENTS.md",
        "dotnet/Area11.Core/Area11.Core.csproj",
        "dotnet/Area11.Desktop/Area11.Desktop.csproj",
        "dotnet/Area11.Tests/Area11.Tests.csproj",
        "tools/check_repo.py", "tools/build_rules_matrix.py", "tools/doctor.ps1",
        "tools/CHECK-AREA11.cmd", "tools/diagnose.bat",
        # The installer (F-47): the two double-click files, the hands behind them, the published
        # folder's guide and the icon the owner sees on the desktop.
        "tools/INSTALL-AREA11.cmd", "tools/UNINSTALL-AREA11.cmd", "tools/MAKE-INSTALLER.cmd",
        "tools/install/install.ps1", "tools/install/uninstall.ps1", "tools/make_icon.py",
        "docs/INSTALL.md", "dotnet/Area11.Desktop/Resources/area11.ico",
        "docs/PROGRESS.md", "docs/RULES-MATRIX.md", "docs/OWNER-DECISIONS.md",
    ]
    missing = [name for name in required if not (ROOT / name).exists()]
    for name in missing:
        fail(f"the repository is incomplete - missing {name}")

    REPORT.append(f"Skeleton: {len(required)} required files, {len(missing)} missing")


# ---------------------------------------------------------------------------
# 1f. Tests may only touch public code
# ---------------------------------------------------------------------------
def check_internal_access() -> None:
    """A test that names an internal type is a compile error waiting for the owner (CS0122).

    Why: the receipt tests reached for `ReceiptService.SaleRow`, which is internal, and the build
    broke on the shop PC. The fix was to test through the public doorway; this check keeps the next
    test from making the same mistake, because the sandbox cannot compile.
    """
    internal_types: set[str] = set()
    for path in (DOTNET / "Area11.Core").rglob("*.cs"):
        text = path.read_text(encoding="utf-8-sig")
        internal_types.update(re.findall(
            r"\binternal\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*(?:class|record|struct|enum)\s+(\w+)",
            text))

    if not internal_types:
        REPORT.append("Accessibility: no internal types in Core")
        return

    offenders = 0
    for path in sorted((DOTNET / "Area11.Tests").glob("*.cs")):
        text = path.read_text(encoding="utf-8-sig")
        code = re.sub(r"//[^\n]*", "", text)
        for name in sorted(internal_types):
            if re.search(rf"\b{re.escape(name)}\b", code):
                offenders += 1
                fail(f"{rel(path)} uses the internal type '{name}' - a test may only use public "
                     f"members (CS0122), or Core must expose it deliberately")

    REPORT.append(f"Accessibility: {len(internal_types)} internal types in Core, {offenders} used by tests")


def check_sealed_bases() -> None:
    """A class that derives from a sealed type is a compile error (CS0509).

    Why: `ReturnLineOption` was sealed while `ReturnService.ItemRow` derived from it to add the
    batch id. Not one of the other checks can see it, and it stopped the first real build of Phase 2
    on the shop PC. The rule here is simple: if a base class is declared `sealed`, nothing may derive
    from it. A base whose name is declared more than once (two namespaces) is skipped rather than
    guessed.
    """
    declarations: dict[str, list[tuple[bool, pathlib.Path]]] = {}
    sources: dict[pathlib.Path, str] = {}

    for path in sorted(DOTNET.rglob("*.cs")):
        if any(part in {"obj", "bin"} for part in path.parts):
            continue

        text = path.read_text(encoding="utf-8-sig")
        sources[path] = text

        for match in re.finditer(
                r"(?m)^\s*(?:public|internal|private|protected|file)?\s*"
                r"(?:(?:sealed|abstract|static|partial|unsafe|new)\s+)*"
                r"(?:class|record)\s+(\w+)\s*(?:<[^>{}()]*>)?\s*(?::\s*([^\n{]+?))?\s*(?:where\b[^\n{]*)?\s*\{",
                text):
            name = match.group(1)
            sealed = bool(re.search(r"\bsealed\b", match.group(0).split(name)[0]))
            declarations.setdefault(name, []).append((sealed, path))

    if not declarations:
        REPORT.append("Sealed bases: no classes found")
        return

    sealed_types = {name for name, rows in declarations.items()
                    if len(rows) == 1 and rows[0][0]}
    ambiguous = {name for name, rows in declarations.items() if len(rows) > 1}

    checked = 0
    for path, text in sources.items():
        for match in re.finditer(
                r"(?m)^\s*(?:public|internal|private|protected|file)?\s*"
                r"(?:(?:sealed|abstract|static|partial|unsafe|new)\s+)*"
                r"(?:class|record)\s+(\w+)\s*(?:<[^>{}()]*>)?\s*:\s*([^\n{]+?)\s*(?:where\b[^\n{]*)?\s*\{",
                text):
            name = match.group(1)
            base = match.group(2).split(",")[0].strip()
            base = re.sub(r"<.*", "", base).strip()

            if not base or base == name or base in ambiguous:
                continue

            checked += 1
            if base in sealed_types:
                line = text[:match.start()].count("\n") + 1
                fail(f"{rel(path)}:{line}: '{name}' derives from the sealed type '{base}' (CS0509)")

    REPORT.append(f"Sealed bases: {checked} derivations checked, "
                  f"{len(sealed_types)} sealed types")


_MEMBER_RE = re.compile(
    r"(?m)^[ \t]*(?:public|private|protected|internal|file)[^\n;{}]*\([^;{}]*\)[ \t\r\n]*"
    r"(?:where[^\n{]*)?\{")

_LOCAL_TYPES = (
    r"(?:var|string|double|long|int|bool|decimal|char|byte|float|short|object"
    r"|List<[^<>;]+>|Dictionary<[^<>;]+>|IReadOnlyList<[^<>;]+>"
    r"|ServiceResult(?:<[^<>;]*>)?"
    r"|ReturnInput|SaleInput|WriteOffInput|ProvisionalInput|ReturnQuote)"
)
_LOCAL_RE = re.compile(_LOCAL_TYPES + r"\s+(\w+)\s*(?:=[^=]|;|,)")


def check_shadowing() -> None:
    """A nested local may not reuse a name declared by an enclosing scope (CS0136), and two locals
    in the same scope may not share a name (CS0128).

    Why: `SaleService.Plan` declared `var method` inside the payment loop and `string method` later
    in the same method. C# refuses it whichever comes first, and it stopped the owner's build at
    `SaleService.cs(609)` - the second compile error in a row that no other check could see.

    How: each member body is walked once. Every local declaration records the chain of `{` blocks
    that enclose it; two declarations clash when their chains are equal or one is a prefix of the
    other (siblings are legal). Declarations inside a parameter list are ignored - two local
    functions may each have a parameter called `bold` - except `foreach (var x in ...)`, whose
    variable really does belong to the block.
    """
    conflicts = 0
    members = 0

    for path in sorted(DOTNET.rglob("*.cs")):
        if any(part in {"obj", "bin", "Migrations"} for part in path.parts):
            continue

        masked = mask_csharp_noise(path.read_text(encoding="utf-8-sig"))

        brace_match: dict[int, int] = {}
        open_braces: list[int] = []
        for index, char in enumerate(masked):
            if char == "{":
                open_braces.append(index)
            elif char == "}" and open_braces:
                brace_match[open_braces.pop()] = index

        for member in _MEMBER_RE.finditer(masked):
            body_start = member.end() - 1
            body_end = brace_match.get(body_start)
            if body_end is None:
                continue

            members += 1
            body = masked[body_start:body_end]

            declarations: dict[str, list[tuple[tuple[int, ...], int]]] = {}

            def enclosing_blocks(offset: int) -> tuple[int, ...]:
                return tuple(sorted(
                    brace for brace, close in brace_match.items()
                    if body_start < brace < body_start + offset < close))

            for decl in _LOCAL_RE.finditer(body):
                if _inside_parameter_list(body, decl.start()):
                    continue

                name = decl.group(1)
                declarations.setdefault(name, []).append(
                    (enclosing_blocks(decl.start()), body_start + decl.start()))

            for name, rows in declarations.items():
                for first in range(len(rows)):
                    for second in range(first + 1, len(rows)):
                        chain_one, offset_one = rows[first]
                        chain_two, offset_two = rows[second]

                        nested = (chain_one == chain_two
                                  or chain_one == chain_two[:len(chain_one)]
                                  or chain_two == chain_one[:len(chain_two)])

                        if not nested:
                            continue

                        conflicts += 1
                        line_one = masked[:offset_one].count("\n") + 1
                        line_two = masked[:offset_two].count("\n") + 1
                        fail(f"{rel(path)}: '{name}' is declared twice in overlapping scopes "
                             f"(lines {line_one} and {line_two}) - CS0136/CS0128")

    REPORT.append(f"Shadowing: {members} members checked, {conflicts} name clash(es)")


def _inside_parameter_list(body: str, offset: int) -> bool:
    """True when the position sits inside `(...)` that is not a `foreach`/`using` header."""
    depth = 0
    index = offset - 1
    while index >= 0:
        char = body[index]
        if char == ")":
            depth += 1
        elif char == "(":
            if depth == 0:
                head = body[max(0, index - 40):index].rstrip().split()
                word = head[-1] if head else ""
                return word not in {"foreach", "using"}
            depth -= 1
        index -= 1
    return False


_VALUE_RESULTS = {"int", "long", "double", "bool", "decimal", "float", "short", "byte"}


def check_result_values() -> None:
    """`ServiceResult<int>.Value` is a plain `int` - `T?` on an unconstrained parameter is only a
    nullable annotation, not `Nullable<T>`.

    Why: `ReturnsViewModel.Find` wrote `found.Value is null` and `found.Value.Value`. The Desktop
    build stopped with CS0037 and two CS1061 - the only project not compiled until then, because
    Core and Tests had always failed first.

    How: the checker learns which service each `_services.<Property>` is (from `AppServices`), which
    methods return `ServiceResult<value type>`, follows `var x = _services.Svc.Method(...)`, and then
    refuses `x.Value is null`, `x.Value.Value` and the comparisons on that result. A method name that
    is ambiguous inside one service (several overloads) is skipped rather than guessed.
    """
    # 1. _services.<Prop> -> service type
    app = DOTNET / "Area11.Desktop" / "AppServices.cs"
    if not app.exists():
        REPORT.append("Result values: AppServices.cs not found")
        return

    app_text = mask_csharp_noise(app.read_text(encoding="utf-8-sig"))
    properties = {name: service for service, name in
                  re.findall(r"public\s+(\w+Service)\s+(\w+)\s*\{\s*get;", app_text)}

    # 2. service type -> method -> set of ServiceResult<T> answers
    service_methods: dict[str, dict[str, set[str]]] = {}

    for path in (DOTNET / "Area11.Core").rglob("*.cs"):
        masked = mask_csharp_noise(path.read_text(encoding="utf-8-sig"))
        class_name = re.search(r"\bclass\s+(\w+)", masked)
        if not class_name:
            continue

        methods = service_methods.setdefault(class_name.group(1), {})

        for match in re.finditer(
                r"public\s+(?:static\s+|async\s+)?ServiceResult<([\w<>, ?]+)>\s+(\w+)\s*\(",
                masked):
            methods.setdefault(match.group(2), set()).add(match.group(1).strip())

        for match in re.finditer(
                r"public\s+(?:static\s+|async\s+)?ServiceResult(?!<)\s+(\w+)\s*\(", masked):
            methods.setdefault(match.group(1), set()).add("void")

    # 3. the members of every model class. A `ServiceResult<AuditPage>` is NOT an `AuditPage`:
    #    `page.Total` compiles to CS1061, which is exactly how the first build of v0.6.0 stopped.
    model_members = _model_folder_properties()

    # 4. follow `var x = _services.<Prop>.<Method>(` and `var x = TestData.<Helper>(test).<Method>(`
    testdata = DOTNET / "Area11.Tests" / "TestData.cs"
    helpers: dict[str, str] = {}
    if testdata.exists():
        helpers_text = mask_csharp_noise(testdata.read_text(encoding="utf-8-sig"))
        for match in re.finditer(r"public\s+static\s+(\w+Service)\s+(\w+)\s*\(", helpers_text):
            helpers[match.group(2)] = match.group(1)

    checked = 0

    for folder in ("Area11.Desktop", "Area11.Tests"):
        for path in sorted((DOTNET / folder).rglob("*.cs")):
            masked = mask_csharp_noise(path.read_text(encoding="utf-8-sig"))

            brace_match: dict[int, int] = {}
            open_braces: list[int] = []
            for index, char in enumerate(masked):
                if char == "{":
                    open_braces.append(index)
                elif char == "}" and open_braces:
                    brace_match[open_braces.pop()] = index

            # A test file has its own one-line helpers ("Viewer(test) => TestData.AuditHistory(test)").
            # They answer a service just like TestData does, so the same follow-the-call applies.
            local_helpers = dict(helpers)
            local_helpers.update({name: service for service, name in re.findall(
                r"static\s+(\w+Service)\s+(\w+)\s*\(", masked)})

            # One member at a time: `result` is a different type in every method of a view-model, so
            # the variable name alone is not enough to know what `.Value` means.
            for member in _MEMBER_RE.finditer(masked):
                body_start = member.end() - 1
                body_end = brace_match.get(body_start)
                if body_end is None:
                    continue

                body = masked[body_start:body_end]
                kinds: dict[str, str] = {}
                shapes: dict[str, str] = {}

                for match in re.finditer(
                        r"var\s+(\w+)\s*=\s*(_services\.(\w+)\.(\w+)\s*\(|"
                        r"(?:TestData\.)?(\w+)\([^()]*\)\.(\w+)\s*\()",
                        body):
                    variable = match.group(1)

                    if match.group(3):
                        service = properties.get(match.group(3))
                        method = match.group(4)
                    else:
                        service = local_helpers.get(match.group(5))
                        method = match.group(6)

                    if service is None:
                        continue

                    # `var page = Svc.Read(...).Value!` names the PAYLOAD, not the envelope - the
                    # member access below is then perfectly legal and must not be reported. The call
                    # may carry arguments (`Build(ReportRange.Today, owner).Value!`), so skip to the
                    # bracket that closes the call before looking for `.Value`.
                    depth = 0
                    cursor = match.end() - 1
                    while cursor < len(body):
                        if body[cursor] == "(":
                            depth += 1
                        elif body[cursor] == ")":
                            depth -= 1
                            if depth == 0:
                                break
                        cursor += 1

                    if re.match(r"\s*\.Value\b", body[cursor + 1:cursor + 30]):
                        continue

                    answers = service_methods.get(service, {}).get(method)
                    if not answers or len(answers) != 1:
                        continue

                    answer = next(iter(answers))
                    kinds[variable] = "value" if answer in _VALUE_RESULTS else "reference"
                    shapes[variable] = answer

                for variable, kind in kinds.items():
                    pattern = r"\b" + re.escape(variable) + r"\.Value\b(?:\.Value|\s+(?:is|==|!=))"

                    for match in re.finditer(pattern, body):
                        line = masked[:body_start + match.start()].count("\n") + 1
                        tail = body[match.start():match.start() + 80].replace(" ", "")

                        if ".Value.Value" in tail:
                            checked += 1
                            fail(f"{rel(path)}:{line}: '{variable}.Value.Value' - "
                                 f"ServiceResult.Value is the value itself, never another result "
                                 f"(use '{variable}.Value')")
                            continue

                        if kind != "value":
                            continue

                        checked += 1
                        fail(f"{rel(path)}:{line}: '{variable}.Value' is a value type and can never "
                             f"be null (CS0037) - check '{variable}.Ok' and the number instead")

                # The other half of the same mistake: `ServiceResult<AuditPage>` is not an
                # `AuditPage`. Reading `page.Total` or `page.Rows` off the envelope is CS1061, and it
                # is easy to write because the two names sit one keystroke apart.
                for variable, shape in shapes.items():
                    members = model_members.get(shape)
                    if not members:
                        continue

                    for match in re.finditer(r"\b" + re.escape(variable) + r"\.(\w+)", body):
                        if match.group(1) not in members:
                            continue

                        line = masked[:body_start + match.start()].count("\n") + 1
                        checked += 1
                        fail(f"{rel(path)}:{line}: '{variable}.{match.group(1)}' reads a member of "
                             f"'{shape}' off the ServiceResult itself (CS1061) - use "
                             f"'{variable}.Value!.{match.group(1)}' after checking Ok")

    REPORT.append(f"Result values: {checked} misuse(s) of a value-type ServiceResult")


# ---------------------------------------------------------------------------
# 1f. Constructor arity (CS7036 / CS1501)
# ---------------------------------------------------------------------------
def _split_arguments(text: str) -> list[str]:
    """Split "a, b(c, d), e" at the top-level commas only.

    Brackets of every kind are tracked, including generic angle brackets, so
    `IReadOnlyDictionary<string, object?>` stays ONE argument. Strings and chars are skipped.
    """
    parts: list[str] = []
    depth = 0
    current: list[str] = []
    quote = ""

    for index, ch in enumerate(text):
        previous = text[index - 1] if index else ""

        if quote:
            current.append(ch)
            if ch == quote and previous != "\\":
                quote = ""
            continue

        if ch in "\"'":
            quote = ch
            current.append(ch)
            continue

        if ch in "([{":
            depth += 1
        elif ch == "<":
            # Angle brackets are tracked only where they can be a generic: after a name and before a
            # type. Without this guard the `<` of `x < 5` and the `>` of a lambda's `=>` both moved
            # the counter, and `new NavItem(a, b, () => new View(...))` counted FOUR arguments -
            # which is how a correct call was reported as a CS7036 in the first run of this rule.
            following = text[index + 1:index + 2]
            if (previous.isalnum() or previous in ">]_?") and (following.isalpha() or following in "_?"):
                depth += 1
        elif ch == ">":
            if previous != "=" and depth > 0:
                depth -= 1
        elif ch in ")]}":
            depth -= 1
        elif ch == "," and depth == 0:
            parts.append("".join(current))
            current = []
            continue

        current.append(ch)

    tail = "".join(current)
    if tail.strip():
        parts.append(tail)

    return parts


def _class_bodies(masked: str) -> dict[str, tuple[int, str]]:
    """Class name -> (offset of the body's first character, the text inside the braces)."""
    bodies: dict[str, str] = {}
    open_to_close: dict[int, int] = {}
    stack: list[int] = []

    for index, ch in enumerate(masked):
        if ch == "{":
            stack.append(index)
        elif ch == "}" and stack:
            open_to_close[stack.pop()] = index

    for match in re.finditer(r"\bclass\s+(\w+)", masked):
        brace = masked.find("{", match.end())

        # Skip the base list / where-clause: the body is the first brace after the header.
        if brace < 0 or brace not in open_to_close:
            continue

        bodies[match.group(1)] = (brace + 1, masked[brace + 1:open_to_close[brace]])

    return bodies


def check_constructor_arity() -> None:
    """Every `new XService(...)` must pass as many arguments as XService takes.

    Why: a service constructor gained SettingsService in v0.3.10 and a test file that built its own
    instance was missed; the owner's build then stopped with CS7036. The sandbox cannot compile, so
    this walks the two things the compiler would check - the declaration and every call.
    """
    required: dict[str, tuple[int, int]] = {}
    calls_checked = 0

    # Every project, not only Core. Until v0.29.8 only Core declared the classes, so a Desktop row
    # class (`new QuarantineRow(item, Decide)`) was never compared with its own constructor - the
    # call was scanned, found no declaration to compare against, and passed silently. The window
    # program is the project that has never compiled on the shop PC; it is the last place to leave
    # a blind spot.
    for path in DOTNET.rglob("*.cs"):
        if {"obj", "bin"} & set(path.parts):
            continue

        original = path.read_text(encoding="utf-8-sig")
        masked = mask_csharp_noise(original)

        for name, (class_start, body) in _class_bodies(masked).items():
            match = re.search(r"public\s+" + re.escape(name) + r"\s*\(", body)
            if not match:
                continue

            depth = 0
            end = len(body)
            for index in range(match.end() - 1, len(body)):
                if body[index] == "(":
                    depth += 1
                elif body[index] == ")":
                    depth -= 1
                    if depth == 0:
                        end = index
                        break

            params = _split_arguments(original[class_start + match.end():class_start + end])
            optional = sum(1 for param in params if "=" in param)
            variadic = any(param.strip().startswith("params ") for param in params)
            required[name] = (len(params) - optional, len(params) if not variadic else 10_000)

    if not required:
        return

    for path in DOTNET.rglob("*.cs"):
        original = path.read_text(encoding="utf-8-sig")
        masked = mask_csharp_noise(original)

        for match in re.finditer(r"\bnew\s+(\w+)\s*\(", masked):
            name = match.group(1)
            if name not in required:
                continue

            depth = 0
            end = len(masked)
            for index in range(match.end() - 1, len(masked)):
                if masked[index] == "(":
                    depth += 1
                elif masked[index] == ")":
                    depth -= 1
                    if depth == 0:
                        end = index
                        break

            args = _split_arguments(original[match.end():end])
            given = len([arg for arg in args if arg.strip()])
            least, most = required[name]
            calls_checked += 1

            if given < least or given > most:
                line = masked[:match.start()].count("\n") + 1
                fail(f"{rel(path)}:{line}: `new {name}(...)` passes {given} argument(s) but "
                     f"`{name}` takes {least}" + ("" if least == most else f"-{most}") + " (CS7036/CS1501)")

    # Target-typed new: `private static AlertService Alerts(...) => new(...)` names no type at the
    # call, so the return type of the helper is the type being built. This is exactly how the test
    # helpers are written, and it is the shape that broke the v0.4.3 build.
    for path in DOTNET.rglob("*.cs"):
        original = path.read_text(encoding="utf-8-sig")

        for match in re.finditer(
                r"(?:public|private|internal)\s+static\s+(\w+Service)\s+\w+\s*\([^()]*\)\s*=>\s*new\s*\(",
                original):
            name = match.group(1)
            if name not in required:
                continue

            depth = 0
            end = len(original)
            for index in range(match.end() - 1, len(original)):
                if original[index] == "(":
                    depth += 1
                elif original[index] == ")":
                    depth -= 1
                    if depth == 0:
                        end = index
                        break

            given = len([arg for arg in _split_arguments(original[match.end():end]) if arg.strip()])
            least, most = required[name]
            calls_checked += 1

            if given < least or given > most:
                line = original[:match.start()].count("\n") + 1
                fail(f"{rel(path)}:{line}: a `=> new(...)` helper returning `{name}` passes {given} "
                     f"argument(s) but `{name}` takes {least}" + ("" if least == most else f"-{most}")
                     + " (CS7036/CS1501)")

    REPORT.append(f"Constructors: {calls_checked} call(s) match their declaration")


# ---------------------------------------------------------------------------
# 1g. Resource keys (the XamlParseException class of error)
# ---------------------------------------------------------------------------
def check_resources() -> None:
    """Every {StaticResource X} / {DynamicResource X} must be declared as x:Key="X".

    This replaces the earlier per-view resource check: that one only looked at the views listed in
    VIEW_MODEL_OF, so LoginWindow and ShellWindow were never examined, and it reported no line.

    Why: a missing key does not fail the build. It throws a XamlParseException the moment the window
    is created - on the shop PC, after the build looked clean - and the app closes before the login
    screen. Every key lives in Theme.xaml, so comparing the two lists settles it here.
    """
    theme = DOTNET / "Area11.Desktop" / "Resources" / "Theme.xaml"

    if not theme.exists():
        return

    declared = set(re.findall(r'x:Key="([^"]+)"', theme.read_text(encoding="utf-8-sig")))
    checked = 0

    for path in (DOTNET / "Area11.Desktop").rglob("*.xaml"):
        text = path.read_text(encoding="utf-8-sig")
        local = set(re.findall(r'x:Key="([^"]+)"', text))

        for match in re.finditer(r"\{(?:StaticResource|DynamicResource)\s+([\w.]+)\s*\}", text):
            key = match.group(1)

            # System keys (for example {x:Static ...} forms) never reach this pattern; anything here
            # must be declared by us.
            checked += 1

            if key not in declared and key not in local:
                line = text[:match.start()].count("\n") + 1
                fail(f"{rel(path)}:{line}: {match.group(0)} is not declared in Theme.xaml "
                     f"(XamlParseException when the window opens)")

    REPORT.append(f"Resources: {checked} StaticResource/DynamicResource key(s) all declared")


# ---------------------------------------------------------------------------
def check_app_labels() -> None:
    """The editable words are one catalog, not three lists that can drift (E-12/S-05).

    Resource existence alone cannot catch a forgotten page title, a theme default that differs from
    Core, or navigation that starts using the editable text as its identity. This gate compares the
    complete route: Core definitions -> settings/theme -> live page resources -> stable shell keys.
    """
    service = DOTNET / "Area11.Core/Services/AppLabelService.cs"
    settings_service = DOTNET / "Area11.Core/Services/SettingsService.cs"
    label_tests = DOTNET / "Area11.Tests/AppLabelTests.cs"
    theme = DOTNET / "Area11.Desktop/Resources/Theme.xaml"
    shell = DOTNET / "Area11.Desktop/ViewModels/ShellViewModel.cs"
    nav_item = DOTNET / "Area11.Desktop/ViewModels/NavItem.cs"
    settings_vm = DOTNET / "Area11.Desktop/ViewModels/SettingsViewModel.cs"
    settings_view = DOTNET / "Area11.Desktop/Views/SettingsView.xaml"
    partners_view = DOTNET / "Area11.Desktop/Views/PartnersView.xaml"
    custom_fields_editor = DOTNET / "Area11.Desktop/Views/CustomFieldsEditorView.xaml"
    shell_window = DOTNET / "Area11.Desktop/Views/ShellWindow.xaml"
    app = DOTNET / "Area11.Desktop/App.xaml.cs"
    needed = (service, settings_service, label_tests, theme, shell, nav_item,
              settings_vm, settings_view, partners_view, custom_fields_editor, shell_window, app)

    for path in needed:
        if not path.exists():
            fail(f"the application-label route is half-built: {rel(path)} is missing")
    if not all(path.exists() for path in needed):
        return

    service_text = service.read_text(encoding="utf-8-sig")
    matches = list(re.finditer(
        r'new\s+AppLabelDefinition\("([^"]+)",\s*"([^"]*)"\)', service_text))
    definitions = [(match.group(1), match.group(2)) for match in matches]
    keys = [key for key, _ in definitions]
    defaults = [text for _, text in definitions]

    if not definitions:
        fail("AppLabelService has no readable catalog definitions")
        return

    for values, what in ((keys, "keys"), (defaults, "default texts")):
        duplicates = sorted({value for value in values if values.count(value) > 1})
        if duplicates:
            fail("AppLabelService has duplicate " + what + ": " + ", ".join(duplicates))

    for key, text in definitions:
        if not key or not text.strip() or len(text) > 60 or any(ord(char) < 32 for char in text):
            fail(f"AppLabelService definition '{key}' has an invalid key/default")

    for needle, reason in (
        ("_permissions.Can(actor, Modules.Settings)", "label writes lost their Core Settings-permission check"),
        ("return _settings.SetMany", "label writes no longer use the atomic audited settings route"),
        ("text.Any(char.IsControl)", "label validation no longer rejects line/control characters"),
    ):
        if needle not in service_text:
            fail("AppLabelService: " + reason)

    settings_text = settings_service.read_text(encoding="utf-8-sig")
    for needle, reason in (
        ("foreach (var label in AppLabelService.Definitions)", "existing databases no longer receive catalog defaults"),
        ("_permissions?.Can(actor, Modules.Settings) != true", "direct label writes no longer fail closed without Settings permission"),
        ("return AppLabelService.GuardSetting(key, value);", "direct settings writes bypass the label validator"),
        ("Application labels must be saved through the audited settings path.", "quiet UI writes can bypass label audit"),
        ("return JsonSerializer.Serialize(TextValue(value), Json.WriterOptions);",
         "direct label writes must trim and preserve readable Unicode before storage and audit"),
    ):
        if needle not in settings_text:
            fail("SettingsService: " + reason)

    test_text = label_tests.read_text(encoding="utf-8-sig")
    for fact in ("CatalogDefaults_AreUniqueAndPersistedAsKnownSettings",
                 "UnicodeRename_IsTrimmedPersistedAndAudited", "InvalidLabels_AreRefusedAtomically",
                 "UnknownLabel_IsRefusedWithoutASettingsRow", "Cashier_CannotSaveOrResetThroughThePublicService",
                 "Reset_RestoresEveryDefaultAndWritesTheAudit", "DirectSettingsWrite_CannotBypassTheLabelGuard"):
        if fact not in test_text:
            fail(f"AppLabelTests: required regression fact '{fact}' is missing")

    catalog = dict(definitions)
    theme_root = ET.parse(theme).getroot()
    x_key = "{http://schemas.microsoft.com/winfx/2006/xaml}Key"
    resources = {
        element.attrib[x_key][len("AppLabel."):]: element.text or ""
        for element in theme_root.iter()
        if element.attrib.get(x_key, "").startswith("AppLabel.")
    }

    if set(resources) != set(catalog):
        fail("AppLabel resources and Core catalog differ; missing resources: " +
             (", ".join(sorted(set(catalog) - set(resources))) or "none") +
             "; extra resources: " + (", ".join(sorted(set(resources) - set(catalog))) or "none"))

    for key in sorted(set(resources) & set(catalog)):
        if resources[key] != catalog[key]:
            fail(f"AppLabel.{key}: Theme says '{resources[key]}' but Core ships '{catalog[key]}'")

    used: set[str] = set()
    headings: set[str] = set()
    for path in (DOTNET / "Area11.Desktop").rglob("*.xaml"):
        text = path.read_text(encoding="utf-8-sig")
        for match in re.finditer(r"\{DynamicResource AppLabel\.([\w.]+)\}", text):
            key = match.group(1)
            used.add(key)
            if key not in catalog:
                fail(f"{rel(path)}: dynamic label '{key}' is not in the Core catalog")

        # Count a heading only on an actual PageTitle TextBlock. Dashboard also has a tooltip, and
        # counting that as its heading would hide a page title that had accidentally gone static.
        for element in ET.parse(path).getroot().iter():
            match = re.fullmatch(r"\{DynamicResource AppLabel\.([\w.]+)\}",
                                 element.attrib.get("Text", ""))
            if (match is not None
                    and element.attrib.get("Style") == "{StaticResource PageTitle}"):
                headings.add(match.group(1))

    shell_text = shell.read_text(encoding="utf-8-sig")
    header_keys = set(re.findall(r'AddGroup\("([^"]+)"', shell_text))
    unknown_headers = sorted(header_keys - set(catalog))
    if unknown_headers:
        fail("navigation groups have no application-label definition: " + ", ".join(unknown_headers))

    # Partners is deliberately a three-tab container: it has a menu title but no PageTitle of its
    # own. Do not add a duplicate heading merely to make this set uniform.
    # Owner removed Counter's duplicate in-page heading in v0.76.0; its selected shell label stays.
    titleless = {"partners", "counter"}
    partners_text = partners_view.read_text(encoding="utf-8-sig")
    if "PageTitle" in partners_text or "<views:CustomersView" not in partners_text:
        fail("PartnersView is no longer the known titleless tab container; update the label route check")

    expected_live = set(catalog) - header_keys - titleless
    if headings != expected_live:
        fail("live application-label headings differ from the catalog; missing: " +
             (", ".join(sorted(expected_live - headings)) or "none") + "; unexpected: " +
             (", ".join(sorted(headings - expected_live)) or "none"))
    if 'ToolTip="{DynamicResource AppLabel.dashboard}"' not in shell_window.read_text(encoding="utf-8-sig"):
        fail("ShellWindow: the Dashboard door no longer uses the live application label")

    nav_key_list = re.findall(r'AddPage\(\s*"[^"]+",\s*"([^"]+)"', shell_text, re.S)
    duplicate_nav_keys = sorted({key for key in nav_key_list if nav_key_list.count(key) > 1})
    if duplicate_nav_keys:
        fail("navigation pages reuse a stable catalog key: " + ", ".join(duplicate_nav_keys))
    nav_keys = set(nav_key_list)
    unknown_keys = sorted(nav_keys - set(catalog))
    if unknown_keys:
        fail("navigation pages have no stable catalog key: " + ", ".join(unknown_keys))
    if "dashboard" in catalog:
        nav_keys.add("dashboard")  # The ungrouped home row is declared directly in BuildNavigation.
    expected_nav = set(catalog) - header_keys - {"alerts"}
    if nav_keys != expected_nav:
        fail("navigation coverage differs from the label catalog; missing: " +
             (", ".join(sorted(expected_nav - nav_keys)) or "none") + "; extra: " +
             (", ".join(sorted(nav_keys - expected_nav)) or "none"))

    if "SetProperty(ref _title, title, nameof(Title))" not in nav_item.read_text(encoding="utf-8-sig"):
        fail("NavItem.SetTitle must notify 'Title' explicitly; CallerMemberName would announce 'SetTitle' and leave the menu stale")

    for needle, reason in (
        ("LabelKey = labelKey", "AddPage no longer retains the stable key"),
        ("new NavItem(_services.AppLabels.Get(labelKey)", "AddPage no longer resolves visible text by its stable key"),
        ("NavItem.Header(_services.AppLabels.Get(key))", "group headers no longer resolve visible text by stable key"),
        ('new NavItem(_services.AppLabels.Get("dashboard")', "Dashboard construction duplicated its display title"),
        ('LabelKey = "dashboard"', "the ungrouped Dashboard row lost its stable key"),
        ("item.SetTitle(_services.AppLabels.Get(item.LabelKey))", "the live menu is not refreshed by key"),
        ('item.LabelKey == "shift"', "Shift navigation regressed to editable text"),
        ('item.LabelKey == "settings"', "Settings navigation regressed to editable text"),
        ("LabelResources.Apply(_services.AppLabels)", "saving does not refresh open dynamic resources"),
    ):
        if needle not in shell_text:
            fail("ShellViewModel: " + reason)

    if "LabelResources.Apply(_services.AppLabels)" not in app.read_text(encoding="utf-8-sig"):
        fail("App startup does not apply saved labels before the first window")

    vm_text = settings_vm.read_text(encoding="utf-8-sig")
    for needle in ("_services.AppLabels.Validate", "AppLabelService.SettingKey",
                   "_services.AppLabels.Reset(_user)", "_onSaved?.Invoke()"):
        if needle not in vm_text:
            fail(f"SettingsViewModel is missing the label save/reset route: {needle}")

    view_text = settings_view.read_text(encoding="utf-8-sig")
    for needle in ('Header="App words"', 'Header="{DynamicResource AppLabel.cloud}"',
                   'ItemsSource="{Binding LabelRows}"', 'Command="{Binding ResetLabelsCommand}"'):
        if needle not in view_text:
            fail(f"SettingsView is missing the application-word editor route: {needle}")

    if 'Text="{DynamicResource AppLabel.customFields}"' not in custom_fields_editor.read_text(encoding="utf-8-sig"):
        fail("CustomFieldsEditorView: the Own fields panel heading no longer follows its live application label")

    REPORT.append(
        f"App labels: {len(catalog)} stable keys and matching theme defaults; "
        f"{len(headings)} live page headings plus the bell, Cloud tab and Own fields panel, "
        "stable-key navigation and guarded Settings save/reset")


# ---------------------------------------------------------------------------
# 1h. x:Static members (the other XamlParseException class of error)
# ---------------------------------------------------------------------------
def check_x_static() -> None:
    """Every `{x:Static prefix:Type.Member}` must exist as a member of that type.

    Why this rule exists: XAML is parsed when the **window is created**, not when the project is
    built. A misspelt member (`BuildInfo.WindowTitel`) therefore builds cleanly and then throws a
    XamlParseException on the shop PC, and the program closes before the login window - the very
    failure that is hardest for the owner to report. `check_resources` covers {StaticResource};
    this covers {x:Static}, which until now was in no check at all.

    Resolution: the `xmlns:` map in the same file gives the namespace (and the assembly), the
    assembly gives the project folder, and the member must be `const` / `static` / `readonly` or a
    property (`Name =>` / `Name {`) inside the class that carries that name.
    """
    desktop = DOTNET / "Area11.Desktop"
    if not desktop.exists():
        return

    checked = 0

    for path in desktop.rglob("*.xaml"):
        text = path.read_text(encoding="utf-8-sig")

        prefixes = {}
        for match in re.finditer(
                r'xmlns:(\w+)="clr-namespace:([^";]+)(?:;assembly=([^"]+))?"', text):
            prefixes[match.group(1)] = (match.group(2), match.group(3) or "Area11.Desktop")

        for match in re.finditer(r"\{x:Static\s+(\w+):(\w+)\.(\w+)\s*\}", text):
            prefix, type_name, member = match.group(1), match.group(2), match.group(3)

            if prefix not in prefixes:
                continue

            namespace, assembly = prefixes[prefix]

            folder = {
                "Area11.Core": DOTNET / "Area11.Core",
                "Area11.Desktop": desktop
            }.get(assembly)

            if folder is None:
                continue                      # a framework assembly: not ours to check

            checked += 1
            line = text[:match.start()].count("\n") + 1
            found_type = False
            found_member = False

            for source in folder.rglob("*.cs"):
                body = source.read_text(encoding="utf-8-sig")

                if not re.search(r"\bnamespace\s+" + re.escape(namespace) + r"\b", body):
                    continue

                type_match = re.search(
                    r"\b(?:class|record|struct|enum)\s+" + re.escape(type_name) + r"\b", body)
                if type_match is None:
                    continue

                found_type = True
                rest = body[type_match.start():]

                if (re.search(r"\b(?:const|static|readonly)\b[^\n;{]*\b"
                              + re.escape(member) + r"\b", rest)
                        or re.search(r"\b" + re.escape(member) + r"\b\s*(?:=>|=|\{)", rest)):
                    found_member = True
                    break

            if not found_type:
                fail(f"{rel(path)}:{line}: {{x:Static {prefix}:{type_name}.{member}}} names "
                     f"`{type_name}`, which is not declared in `{namespace}` "
                     f"(XamlParseException when the window opens)")
            elif not found_member:
                fail(f"{rel(path)}:{line}: {{x:Static {prefix}:{type_name}.{member}}} names "
                     f"`{type_name}.{member}`, but `{type_name}` has no such const/static member "
                     f"(XamlParseException when the window opens)")

    REPORT.append(f"x:Static: {checked} member reference(s) resolve against the code")


# ---------------------------------------------------------------------------
# 1k. `_services.X` must be a member of AppServices (the CS1061 class, at the root)
# ---------------------------------------------------------------------------
def check_service_members() -> None:
    """Every `_services.X` in the window program must be a member of `AppServices`.

    Why: `check_service_methods` resolves `_services.Sales.Create(...)` - three parts - but the
    two-part form is used just as often (`_services.AuditHistory.CanRead(...)` starts here;
    `_services.Cloud` and `_services.Reports` are passed around as values). A misspelt
    two-part name is a **CS1061** in the one project that has never compiled on the shop PC, and
    nothing looked at it.

    What counts as a member: anything `AppServices` declares as public - a property, a method
    (`SignIn`, `SignOut` are methods, not properties, and LoginViewModel calls one). What does not
    count: a local, a nested type or a field the class keeps to itself, because those cannot be
    reached from a view model.
    """
    services_path = DOTNET / "Area11.Desktop" / "AppServices.cs"

    if not services_path.exists():
        return

    text = mask_csharp_noise(services_path.read_text(encoding="utf-8-sig"))

    members = set(re.findall(r"public\s+[\w<>?\[\], .]+?\s+(\w+)\s*(?:\{|=>)", text))
    members |= set(re.findall(r"public\s+[\w<>?\[\], .]+?\s+(\w+)\s*\(", text))
    members |= set(re.findall(r"public\s+[\w<>?\[\], .]+?\s+(\w+)\s*[;=]", text))

    checked = 0

    for path in (DOTNET / "Area11.Desktop").rglob("*.cs"):
        body = path.read_text(encoding="utf-8-sig")
        masked = mask_csharp_noise(body)

        for match in re.finditer(r"\b_services\.(\w+)", masked):
            name = match.group(1)
            checked += 1

            if name not in members:
                line = body[:match.start()].count("\n") + 1
                fail(f"{rel(path)}:{line}: `_services.{name}` is not a member of AppServices "
                     f"(CS1061 - the build stops here)")

    REPORT.append(f"App services: {checked} member reference(s) exist on AppServices")


# ---------------------------------------------------------------------------
# 1i. Service method names (the CS1061 class of error nothing else could see)
# ---------------------------------------------------------------------------
def check_service_methods() -> None:
    """`_services.Sales.Create(...)` and `_sales.Create(...)` must name real methods.

    Why: the Desktop project has not compiled on the shop PC since v0.22.0, and a wrong method
    name there is an ordinary compile error (CS1061) - yet no rule looked at method names. This
    round found the gap the hard way: `Backup.AutomaticIfDue()` was written where the method is
    `RunAutomaticIfDue`, and every check stayed green.

    Two shapes are checked, and only these two:
      * `_services.<Property>.<Method>(` - the holder must hold `AppServices`, the property gives
        the service class (`AppServices` declares them), then the method must exist on it;
      * `_<field>.<Method>(` - the field must be declared as a `...Service` type.
    Anything else (`_db.Query(...)`, a local, a LINQ call) is left alone, which is what keeps the
    rule quiet.
    """
    services_path = DOTNET / "Area11.Desktop" / "AppServices.cs"
    if not services_path.exists():
        return

    services_text = mask_csharp_noise(services_path.read_text(encoding="utf-8-sig"))

    # public SaleService Sales { get; }  ->  Sales -> SaleService
    members = {
        name: type_name
        for type_name, name in re.findall(
            r"public\s+(\w+(?:Service|Services))\s+(\w+)\s*\{\s*get;", services_text)
    }

    # Every service class, from its declaration to the end of its file.
    bodies: dict[str, str] = {}

    for source in DOTNET.rglob("*.cs"):
        text = mask_csharp_noise(source.read_text(encoding="utf-8-sig"))

        for match in re.finditer(r"\b(?:class|record)\s+(\w+(?:Service|Services))\b", text):
            bodies.setdefault(match.group(1), text[match.start():])

    def has_method(owner: str | None, method: str) -> bool:
        body = bodies.get(owner) if owner is not None else None

        return body is not None and re.search(
            r"\b" + re.escape(method) + r"\s*(?:<[^>]*>)?\s*\(", body) is not None

    checked = 0

    # Core as well as Desktop: a service calling another service has the same two shapes, and Core
    # is where most of the shop's rules live.
    for path in DOTNET.rglob("*.cs"):
        if "obj" in path.parts or "bin" in path.parts or "prototypes" in path.parts:
            continue

        text = path.read_text(encoding="utf-8-sig")
        masked = mask_csharp_noise(text)

        fields = {
            name: type_name
            for type_name, name in re.findall(
                r"(?:private|internal|public)\s+(?:readonly\s+)?(\w+(?:Service|Services))\s+(\w+)\s*[;=]",
                masked)
        }

        for match in re.finditer(r"\b(\w+)\.(\w+)\.(\w+)\s*\(", masked):
            holder, prop, method = match.group(1), match.group(2), match.group(3)

            if fields.get(holder) != "AppServices":
                continue

            owner = members.get(prop)
            checked += 1

            if not has_method(owner, method):
                line = text[:match.start()].count("\n") + 1
                fail(f"{rel(path)}:{line}: `{holder}.{prop}.{method}(...)` is not a method of "
                     f"`{owner or 'an unknown service'}` (CS1061 - the build stops here)")

        for match in re.finditer(r"\b(_\w+)\.(\w+)\s*(?:<[^>]*>)?\(", masked):
            holder, method = match.group(1), match.group(2)

            owner = fields.get(holder)

            if owner is None or owner == "AppServices":
                continue

            checked += 1

            if not has_method(owner, method):
                line = text[:match.start()].count("\n") + 1
                fail(f"{rel(path)}:{line}: `{holder}.{method}(...)` is not a method of "
                     f"`{owner}` (CS1061 - the build stops here)")

    REPORT.append(f"Service calls: {checked} method name(s) exist on their service")


# ---------------------------------------------------------------------------
# 2. XML (XAML, csproj, props)
# ---------------------------------------------------------------------------
def check_xml() -> None:
    targets = [p for p in DOTNET.rglob("*") if p.suffix in {".xaml", ".csproj", ".props"}]
    targets += list(DOTNET.glob("*.props"))
    broken = 0
    for path in sorted(set(targets)):
        try:
            root = ET.parse(path).getroot()
            if path.suffix == ".xaml":
                for element in root.iter():
                    if element.tag.rsplit("}", 1)[-1] == "DataGrid" and element.get("RowHeight", "").strip().lower() == "auto":
                        fail(f"{rel(path)}: DataGrid.RowHeight is a double; Auto is invalid. Use NaN for content sizing.")
                    if (element.tag.rsplit("}", 1)[-1] == "Setter"
                        and element.get("Property", "").split(".")[-1] == "RowHeight"
                        and element.get("Value", "").strip().lower() == "auto"):
                        fail(f"{rel(path)}: RowHeight setter cannot convert Auto to double; use NaN.")
        except ET.ParseError as error:
            broken += 1
            fail(f"XML error in {rel(path)}: {error}")
    REPORT.append(f"XML: {len(set(targets))} files, {broken} invalid")


# ---------------------------------------------------------------------------
# 3. XAML bindings
# ---------------------------------------------------------------------------
#: `public int Id { get; }`, `public string Name => ...` - the properties a binding may name.
_BINDABLE_RE = re.compile(r"public\s+[\w<>?\[\], .]+?\s+(\w+)\s*(?:\{|=>)")


def _bindings_in(text: str) -> list[tuple[int, str]]:
    """(offset, path) for every `{Binding path ...}` in a view, with the braces matched properly.

    Why not a regex: a binding may contain another markup extension - `{Binding DataContext.X,
    RelativeSource={RelativeSource AncestorType=ItemsControl}}` - and `[^}]+` stops at the FIRST
    `}`. The tail of that broken match then swallows the next binding, which is how `CanFoldMenu`
    was read as `xt.CanFoldMenu` and quietly skipped (`xt.*` is not a property of anything, so the
    old rule treated the name as a row binding and let it pass).
    """
    found = []
    position = 0

    while True:
        start = text.find("{Binding", position)
        if start < 0:
            break

        depth = 0
        index = start

        while index < len(text):
            if text[index] == "{":
                depth += 1
            elif text[index] == "}":
                depth -= 1
                if depth == 0:
                    break
            index += 1

        inner = text[start + len("{Binding"):index].strip()
        path = inner.split(",")[0].strip()
        found.append((start, path))
        position = index + 1

    return found


def _class_index() -> tuple[dict[str, set[str]], dict[str, str]]:
    """Every class in the repository: its public properties, and its base class.

    The whole tree, not one folder: a row class lives beside its view-model (`AlertRow`), beside
    the menu (`NavItem`) or in `Core/Models`, and the ItemsSource of a grid decides which one a
    column binds to.
    """
    properties: dict[str, set[str]] = {}
    bases: dict[str, str] = {}

    for path in DOTNET.rglob("*.cs"):
        if {"obj", "bin"} & set(path.parts):
            continue

        text = path.read_text(encoding="utf-8-sig")

        for match in re.finditer(r"\bclass\s+(\w+)", text):
            brace = text.find("{", match.end())
            if brace < 0:
                continue

            depth = 0
            end = len(text)

            for index in range(brace, len(text)):
                if text[index] == "{":
                    depth += 1
                elif text[index] == "}":
                    depth -= 1
                    if depth == 0:
                        end = index
                        break

            properties.setdefault(match.group(1), set()).update(
                _BINDABLE_RE.findall(text[brace:end]))

            header = text[match.end():brace]
            parent = re.search(r":\s*([\w.]+)", header)
            if parent and not parent.group(1).startswith("I"):
                bases.setdefault(match.group(1), parent.group(1).split(".")[-1])

    return properties, bases


def _element_type(view_model_text: str, collection: str) -> str:
    """`ObservableCollection<AlertRow> Alerts` + "Alerts" -> "AlertRow"."""
    for shape in (r"ObservableCollection<([\w.]+)>\s+" + re.escape(collection) + r"\b",
                  r"List<([\w.]+)>\s+" + re.escape(collection) + r"\b",
                  r"IReadOnlyList<([\w.]+)>\s+" + re.escape(collection) + r"\b"):
        match = re.search(shape, view_model_text)
        if match:
            return match.group(1).split(".")[-1]

    return ""


def check_bindings() -> None:
    """Every `{Binding X}` must name a property the DataContext really has.

    Why this rule exists at all: a wrong path does not throw. WPF writes a binding error to the
    debug output and leaves the box empty, so the shop shows a blank column and the owner reports
    "the page has no data" - the hardest kind of fault to trace back to one letter.

    What changed in v0.29.9 and why it mattered: the pool of legal names used to be EVERY public
    property of EVERY model and row class in the program, so `{Binding BalancePaisa}` in a grid of
    medicines passed the moment any unrelated model happened to own `BalancePaisa`. The pool is now
    built per page: the page's own view-model, the common `ItemProperties`, the Core models, and the
    element types of the collections THAT page actually binds an ItemsSource to. A name from
    somewhere else in the program is no longer a pass - proved by putting the customer property
    `BalancePaisa` into a grid of medicines: it fails now, and it passed before v0.29.9.

    The Core models are not added wholesale either: every one of the 1,278 bindings resolves
    through the page's own view-model or through a row type taken from its ItemsSource, and a
    pool that is merely "all models" is what hid the fault above.
    """
    properties, bases = _class_index()

    def with_bases(name: str, seen: tuple[str, ...] = ()) -> set[str]:
        if name in seen:
            return set()

        found = set(properties.get(name, set()))
        parent = bases.get(name)

        if parent:
            found |= with_bases(parent, seen + (name,))

        return found

    problems = 0
    checked = 0
    row_types = 0

    for view_name, vm_name in VIEW_MODEL_OF.items():
        view_path = DOTNET / "Area11.Desktop/Views" / view_name
        vm_path = DOTNET / "Area11.Desktop/ViewModels" / f"{vm_name}.cs"

        if not view_path.exists() or not vm_path.exists():
            fail(f"view or view-model missing for {view_name}")
            continue

        text = view_path.read_text(encoding="utf-8")
        vm_text = vm_path.read_text(encoding="utf-8")

        legal = set(_BINDABLE_RE.findall(vm_text)) | set(ITEM_PROPERTIES)
        legal |= set(re.findall(r"public\s+ICommand\s+(\w+)", vm_text))
        # These bindings deliberately target ShellWindow and its HeaderSectionItem wrappers,
        # not the ShellViewModel. Their exact implementation is guarded by check_phase_one_shell.
        if view_name == "ShellWindow.xaml":
            legal |= {"HeaderSections", "Tab"}

        for _, collection in _bindings_in(text):
            if not re.fullmatch(r"[A-Za-z_]\w*", collection):
                continue

            element = _element_type(vm_text, collection)

            if element and element in properties:
                row_types += 1
                legal |= with_bases(element)

        for _, path in _bindings_in(text):
            name = path.split(".")[0].strip()

            if not name or name.startswith(("Path=", "RelativeSource", "ElementName", "Source=")):
                continue

            # `{Binding DataContext.X, RelativeSource=...}` reaches the page's view-model through
            # an ancestor, so X is a page property - the same test applies.
            if name == "DataContext" and "." in path:
                name = path.split(".")[1].strip()

            if not re.fullmatch(r"[A-Za-z_]\w*", name):
                continue

            checked += 1

            if name in legal:
                continue

            problems += 1
            fail(f"{view_name}: binding {{Binding {name}}} is not a property of {vm_name} "
                 f"or of the rows this page lists (WPF leaves the box empty and says nothing)")

    REPORT.append(f"XAML bindings: {checked} checked, {problems} problems, "
                  f"{row_types} row type(s) resolved from ItemsSource")


# ---------------------------------------------------------------------------
# 3c. {x:Type} names, and the DataTemplate that decides which screen a page is
# ---------------------------------------------------------------------------
def _declared_types(folder_namespace: tuple[str, pathlib.Path]) -> dict[str, set[str]]:
    """namespace -> the class names declared in that namespace, inside one project folder."""
    namespace, folder = folder_namespace
    found: dict[str, set[str]] = {}

    for path in folder.rglob("*.cs"):
        if {"obj", "bin"} & set(path.parts):
            continue

        text = path.read_text(encoding="utf-8-sig")
        room = re.search(r"\bnamespace\s+([\w.]+)", text)

        if room is None or room.group(1) != namespace:
            continue

        found.setdefault(namespace, set()).update(
            re.findall(r"\b(?:class|record|struct|enum)\s+(\w+)", text))

    return found


def check_templates() -> None:
    """`{x:Type ns:SomeClass}` must name a real class, and a page must have its own template.

    Why: a `<DataTemplate DataType="{x:Type vm:PosViewModel}">` with a misspelt type is the SAME
    runtime failure as a misspelt `{x:Static}` - XAML is parsed when the window is created, so the
    program closes before the sign-in screen and a WinExe says nothing (F-01, F-16).

    Why the template body too: the shell builds a page by `new SomeViewModel(...)` and WPF picks the
    template whose DataType is that type. A view model with **no** template is not an error - it is
    a page that shows the object's own name in an empty frame, which the owner reads as "the page is
    broken". And a template that holds the WRONG view (`<views:PosView />` under
    `DashboardViewModel`) compiles, runs, and shows the counter where the dashboard should be.
    """
    desktop = DOTNET / "Area11.Desktop"
    shell = desktop / "Views" / "ShellWindow.xaml"

    if not shell.exists():
        return

    text = shell.read_text(encoding="utf-8-sig")

    prefixes = {}
    for match in re.finditer(
            r'xmlns:(\w+)="clr-namespace:([^";]+)(?:;assembly=([^"]+))?"', text):
        prefixes[match.group(1)] = (match.group(2), match.group(3) or "Area11.Desktop")

    known: dict[str, set[str]] = {}

    for prefix, (namespace, assembly) in prefixes.items():
        folder = {"Area11.Core": DOTNET / "Area11.Core", "Area11.Desktop": desktop}.get(assembly)

        if folder is None:
            continue                     # a framework namespace: not ours to check

        for room, names in _declared_types((namespace, folder)).items():
            known.setdefault(prefix, set()).update(names)

    # ---- 1. every `{x:Type prefix:Class}` names a class that exists
    typed = 0
    broken_type = False

    for path in desktop.rglob("*.xaml"):
        body = path.read_text(encoding="utf-8-sig")
        local = {}

        for match in re.finditer(
                r'xmlns:(\w+)="clr-namespace:([^";]+)(?:;assembly=([^"]+))?"', body):
            local[match.group(1)] = (match.group(2), match.group(3) or "Area11.Desktop")

        for match in re.finditer(r"\{x:Type\s+(\w+):(\w+)\s*\}", body):
            prefix, name = match.group(1), match.group(2)

            if prefix not in local:
                continue

            namespace, assembly = local[prefix]
            folder = {"Area11.Core": DOTNET / "Area11.Core", "Area11.Desktop": desktop}.get(assembly)

            if folder is None:
                continue

            typed += 1
            declared = _declared_types((namespace, folder)).get(namespace, set())

            if name not in declared:
                broken_type = True
                line = body[:match.start()].count("\n") + 1
                fail(f"{rel(path)}:{line}: {{x:Type {prefix}:{name}}} names `{name}`, which is not "
                     f"declared in `{namespace}` (XamlParseException when the window opens)")

    # ---- 2 and 3. the shell's own templates: one per page, and the right view inside each
    templates = {}

    for match in re.finditer(
            r'<DataTemplate\s+DataType="\{x:Type\s+\w+:(\w+)\}"\s*>(.*?)</DataTemplate>',
            text, re.S):
        templates[match.group(1)] = match.group(2)

    pages = set(re.findall(r"new\s+([A-Z]\w*ViewModel)\s*\(", (desktop / "ViewModels" / "ShellViewModel.cs").read_text(encoding="utf-8-sig")))
    views = {path.stem for path in (desktop / "Views").glob("*.xaml")}

    # One root cause, one message. A misspelt {x:Type} also leaves the page it belonged to without a
    # template, and reporting that second fact would send the reader looking for a missing template
    # that is in fact right there, misspelt. The cascade is suppressed; the first line is the truth.
    for page in ([] if broken_type else sorted(pages)):
        if page not in templates:
            fail(f"ShellWindow.xaml: the shell creates `new {page}(...)` but has no DataTemplate for "
                 f"it - the page opens as an empty frame (no exception, nothing said)")

    for page, body in sorted(templates.items()):
        inside = re.findall(r"<views:(\w+)\s*/>", body)
        wanted = page[:-len("ViewModel")] + "View" if page.endswith("ViewModel") else ""

        if wanted and wanted not in views:
            fail(f"ShellWindow.xaml: the template for `{page}` shows `{wanted}`, "
                 f"but Views/{wanted}.xaml does not exist")
        elif wanted and inside != [wanted]:
            shown = ", ".join(inside) if inside else "nothing"
            fail(f"ShellWindow.xaml: the template for `{page}` shows {shown}, and it should show "
                 f"`{wanted}` - the wrong screen would open under the right menu name")

    REPORT.append(f"Templates: {typed} {{x:Type}} name(s) resolve, "
                  f"{len(templates)} page template(s) match their view")


# ---------------------------------------------------------------------------
# 4. Migrations
# ---------------------------------------------------------------------------
def check_migrations() -> None:
    folder = DOTNET / "Area11.Core/Data/Migrations"
    files = sorted(folder.glob("*.sql"))
    if not files:
        fail("no migrations found")
        return

    numbers = [int(p.name.split("_")[0]) for p in files]
    expected = list(range(1, len(files) + 1))
    if numbers != expected:
        fail(f"migrations are not contiguous: {numbers}")

    sql = "\n".join(p.read_text(encoding="utf-8") for p in files)
    tables = set(re.findall(r"CREATE TABLE(?: IF NOT EXISTS)?\s+([a-z_]+)", sql, re.I))

    for required in ("products", "batches", "sales", "sale_items", "purchases", "purchase_items",
                     "customers", "suppliers", "payments", "sale_returns", "supplier_returns",
                     "provisional_returns", "audit_logs", "settings", "stock_movements", "photos",
                     "user_permissions", "alert_dismissals", "stock_takes", "shifts", "expenses",
                     "owner_drawings", "custom_fields"):
        if required not in tables:
            fail(f"migration set does not create table '{required}'")

    # quarantine (U-34) lives on sale_returns, not in a table of its own
    if "sale_returns" in tables:
        for column in ("qcode", "disposition"):
            if not re.search(rf"\b{column}\b", sql):
                fail(f"sale_returns is missing the quarantine column '{column}'")

    REPORT.append(f"Migrations: {len(files)} files, {len(tables)} tables")

    try:
        import sqlite3

        connection = sqlite3.connect(":memory:")
        for path in files:
            connection.executescript(path.read_text(encoding="utf-8"))
        actual = {row[0] for row in connection.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")}
        REPORT.append(f"Migrations executed on SQLite: {len(actual)} tables")
        if len(actual) < len(tables):
            fail(f"only {len(actual)} tables created, SQL declares {len(tables)}")
        connection.close()
    except Exception as error:  # noqa: BLE001
        warn(f"could not execute migrations in SQLite: {error}")


# ---------------------------------------------------------------------------
# 4b. SQL in the services must be valid against the real schema
# ---------------------------------------------------------------------------
#: A statement that ends on one of these is a piece of a statement pasted together in C#, not one.
FRAGMENT_END = re.compile(
    r"(?:[=,(]|\b(?:FROM|WHERE|AND|OR|JOIN|INTO|SET|VALUES|ORDER\s+BY|GROUP\s+BY|LIMIT|OFFSET|LIKE|IN))\s*$",
    re.I)


def check_sql() -> None:
    """Every SQL statement written in C# is prepared against the migrated schema.

    Why: a mistyped column name only shows up when the cashier presses the button. SQLite
    resolves names while preparing a statement, so preparing them here catches the whole
    class without running the app.
    """
    try:
        import sqlite3
    except Exception:  # noqa: BLE001
        warn("sqlite3 is not available - SQL statements were not checked")
        return

    connection = sqlite3.connect(":memory:")
    for path in sorted((DOTNET / "Area11.Core/Data/Migrations").glob("*.sql")):
        connection.executescript(path.read_text(encoding="utf-8"))

    # The ledger is created by MigrationRunner, not by a migration file; a test that counts it is
    # asking a true question, so the model of the schema has to carry it too.
    connection.execute("CREATE TABLE IF NOT EXISTS _migrations (id TEXT PRIMARY KEY, "
                       "applied_at TEXT NOT NULL DEFAULT (datetime('now','localtime')));")

    verbatim = re.compile(r'@"((?:[^"]|"")*)"', re.S)
    plain = re.compile(r'(?<![@$])"((?:[^"\\]|\\.)*)"', re.S)

    # Interpolated strings are not SQL that can be prepared - the {holes} are C# expressions - but they
    # must be BLANKED before the plain scan, not merely skipped: their closing quote otherwise pairs
    # with the next string's opening quote, and every statement after that point is read as prose and
    # silently ignored. That is exactly how `INSERT INTO companies(name, created_at)` - a column that
    # does not exist - reached the owner's PC in v0.29.5 while this rule reported the tree clean.
    interpolated = re.compile(r'\$@"(?:[^"]|"")*"|\$"(?:[^"\\]|\\.)*"', re.S)

    total = 0
    invalid = 0
    fragments = 0
    sources = sorted((DOTNET / "Area11.Core/Services").glob("*.cs"))
    sources += sorted((DOTNET / "Area11.Core/Data").glob("*.cs"))

    # The tests too. v0.29.5 shipped a test that typed `INSERT INTO companies(name, created_at)` -
    # a column that does not exist - and it reached the owner's PC because this rule only read the
    # services. A test that cannot run is worse than no test: it hides the feature it was written
    # to prove behind a red line the owner reads as a fault in the program.
    sources += sorted((DOTNET / "Area11.Tests").glob("*.cs"))

    for path in sources:
        text = path.read_text(encoding="utf-8-sig")

        # Both masks are for the PLAIN scan: a verbatim or interpolated literal ends in a quote that
        # would otherwise open the next plain string, and from there every statement in the file is
        # read off by one. Verbatim literals are still scanned for their own SQL - just from a copy
        # where the interpolated ones are already gone.
        without_interpolated = interpolated.sub('""', text)
        without_verbatim = verbatim.sub('""', without_interpolated)

        for pattern, subject in ((verbatim, without_interpolated), (plain, without_verbatim)):
            for match in pattern.finditer(subject):
                sql = match.group(1).replace('""', '"').strip()

                # The keywords are matched in UPPER CASE on purpose. Every statement in this repository
                # is written in upper case, so a lower-case "with" is prose, not SQL - and prose is what
                # a search term in a test looks like ("with, comma", 'with "quote"').
                if not re.match(r"^(SELECT|INSERT|UPDATE|DELETE|WITH)\b", sql):
                    continue

                # A statement built by pasting a variable is not a statement: "SELECT COUNT(1) FROM "
                # ends in a word that cannot end SQL. Preparing the half would report a fault in the
                # test rather than a fault in the shop, and a checker that cries wolf is turned off.
                if FRAGMENT_END.search(sql):
                    fragments += 1
                    continue

                total += 1
                parameters = {name: None for name in set(re.findall(r"@(\w+)", sql))}
                try:
                    connection.execute("EXPLAIN " + sql, parameters)
                except Exception as error:  # noqa: BLE001
                    invalid += 1
                    fail(f"invalid SQL in {rel(path)}: {error} :: "
                         + " ".join(sql.split())[:90])

    connection.close()
    REPORT.append(f"SQL: {total} statements prepared, {invalid} invalid"
                  + (f", {fragments} fragment(s) skipped" if fragments else ""))


# ---------------------------------------------------------------------------
# 4d. Every column Dapper is asked to read must have a home on the model
# ---------------------------------------------------------------------------
#: Words that follow AS inside a CAST(... AS INTEGER) and are not column aliases.
SQL_TYPE_WORDS = {"INTEGER", "REAL", "TEXT", "NUMERIC", "BLOB", "INT", "BOOLEAN"}

#: Columns selected on purpose with no property: they only make ORDER BY repeatable.
#: ExpenseService.List selects `sortKind` to keep expenses before drawings inside one second;
#: Dapper drops it, which is exactly what the comment there says.
ALIASES_WITHOUT_PROPERTY = {"sortKind"}


def _mask(sql: str):
    """A same-length copy with strings and comments blanked, plus the bracket depth of each char.

    Same length matters: every later position must still point at the same place in the original
    text, otherwise the projection slices drift.
    """
    chars = list(sql)
    depth = 0
    depths = [0] * len(sql)
    i = 0

    while i < len(sql):
        char = sql[i]

        if char == "'":
            end = sql.find("'", i + 1)
            end = len(sql) - 1 if end == -1 else end
            for k in range(i, end + 1):
                chars[k] = " "
            i = end + 1
            continue

        if sql.startswith("--", i):
            end = sql.find("\n", i)
            end = len(sql) if end == -1 else end
            for k in range(i, end):
                chars[k] = " "
            i = end
            continue

        if char == "(":
            depth += 1
        elif char == ")":
            depth = max(0, depth - 1)

        depths[i] = depth
        i += 1

    return chars, depths


def _top_level_spans(sql: str):
    """The SELECT ... FROM projections at bracket depth 0 - the ones Dapper maps."""
    chars, depths = _mask(sql)

    def _is_name_char(char: str) -> bool:
        """`_` belongs to a name: `opened_from_shift_id` is a column, not the FROM keyword.

        v0.30.6: this rule read `... s.opened_from_shift_id AS openedFromShiftId` as if the
        select list ended at `opened_`, so four healthy queries were reported as columns with no
        property. A gate that fails on good code teaches the owner to ignore the gate.
        """
        return char.isalnum() or char == "_"

    starts = []
    for i in range(len(sql) - 6):
        if depths[i] == 0 and "".join(chars[i:i + 6]).upper() == "SELECT" \
                and (i == 0 or not _is_name_char(chars[i - 1])):
            starts.append(i)

    spans = []
    for start in starts:
        for i in range(start + 6, len(sql) - 4):
            if depths[i] == 0 and "".join(chars[i:i + 4]).upper() == "FROM" \
                    and not _is_name_char(chars[i - 1]):
                spans.append(sql[start + 6:i])
                break

    return spans


def _alias_of(item: str) -> str | None:
    """The name Dapper will look for: the AS alias, or the last part of a plain column."""
    text = item.strip()
    chars, _ = _mask(text)

    if not text or not "".join(chars).strip():
        return None
    if text.startswith("*") or text.endswith("*") or text[0] in "?@'":
        return None
    if text[:1].isdigit():
        return None

    chars, depths = _mask(text)
    alias = None

    for i in range(len(text) - 2):
        if depths[i] == 0 and "".join(chars[i:i + 2]).upper() == "AS" \
                and (i == 0 or chars[i - 1].isspace()) and text[i + 2:i + 3] == " ":
            word = text[i + 3:].strip().split()[0].strip('"')
            alias = word

    if alias:
        if alias.startswith("'") or alias.upper() in SQL_TYPE_WORDS:
            return None
        return alias

    column = text.split()[-1].strip('"')
    return column.split(".")[-1] if column else None


PROPERTY_RE = re.compile(r"public\s+[\w<>?\[\], .]+?\s+(\w+)\s*(?:\{|=>)")


def _classes_in(text: str) -> dict:
    """class name -> its own property names, for ONE file.

    Per file matters: five files have a private `ItemRow` and three a `SaleRow`. Merging them by
    name would let one class lend a property to another and hide the very mistake this checks for.
    """
    headers = [(match.start(), match.group(1)) for match in re.finditer(r"class\s+(\w+)", text)]
    found = {}

    for index, (position, name) in enumerate(headers):
        end = headers[index + 1][0] if index + 1 < len(headers) else len(text)
        found.setdefault(name, set()).update(PROPERTY_RE.findall(text[position:end]))

    return found


def _base_of(text: str, name: str) -> str:
    match = re.search(rf"class\s+{re.escape(name)}\s*:\s*([\w.]+)", text)
    return (match.group(1) if match else "").split(".")[-1]


def _model_folder_properties() -> dict:
    """The shared models in Core/Models - the base classes a row class may inherit from."""
    found = {}

    for path in (DOTNET / "Area11.Core/Models").glob("*.cs"):
        for name, props in _classes_in(path.read_text(encoding="utf-8-sig")).items():
            found.setdefault(name, set()).update(props)

    return found


def _normalise(word: str) -> str:
    return word.replace("_", "").lower()


def _split_top(expr: str, separator: str):
    """Split an expression on a separator that sits outside brackets and strings."""
    chars, depths = _mask(expr)
    parts = []
    start = 0

    for index in range(len(expr)):
        if chars[index] == separator and depths[index] == 0:
            parts.append(expr[start:index])
            start = index + 1

    parts.append(expr[start:])
    return parts


def _const_sqls(text: str) -> dict:
    """name -> the SQL a `const string` holds, with `a + "suffix"` resolved.

    Why: a query written as a constant is otherwise invisible to this check - the report query
    that lost its profit column lived in a `const string`, and an inline-only scan would have
    declared the file clean.
    """
    raw = {}
    for match in re.finditer(r"const\s+string\s+(\w+)\s*=\s*(.+?);", text, re.S):
        raw[match.group(1)] = match.group(2).strip()

    def resolve(expression: str, seen: set) -> str:
        built = ""

        for part in _split_top(expression, "+"):
            piece = part.strip()

            if piece.startswith('@"') and piece.endswith('"'):
                built += piece[2:-1].replace('""', '"')
            elif piece.startswith('"') and piece.endswith('"'):
                built += piece[1:-1].replace('\\"', '"')
            elif piece in raw and piece not in seen:
                built += resolve(raw[piece], seen | {piece})
            elif piece in raw:
                built += " "

        return built

    return {name: resolve(expression, {name}) for name, expression in raw.items()}


def check_column_mapping() -> None:
    """A column with no property is DROPPED by Dapper, and the field silently stays 0.

    Why this exists: the reports page shipped a query that named its profit column `profit`
    while the row class called the property `ProfitPaisa`. Dapper matched nothing, wrote 0, and
    the page showed a profit of zero for a profitable day - no exception, no log line. The
    compiler cannot see it and SQLite accepts it, so it is checked here: every column of a
    top-level SELECT must resolve to a property of the class it is being read into.
    """
    models = _model_folder_properties()

    # Two shapes: SQL written inline, and SQL held in a `const string` (or a ternary of two of
    # them). Both are checked, because a query written as a constant is still a query.
    inline = re.compile(
        r"Query(?:Single)?<\s*([\w.]+)\s*>\(\s*\w+\s*,\s*@\"((?:[^\"]|\"\")*)\"", re.S)
    named = re.compile(
        r"Query(?:Single)?<\s*([\w.]+)\s*>\(\s*\w+\s*,\s*([^;]{1,200}?)\)\s*[;\n]", re.S)

    checked = 0
    problems = 0

    # The tests as well as the services. A test reads the shop's rows through its own little row
    # classes, and a column that does not reach one of them leaves a 0 that the assertion then
    # blames on the program: "expected 3, actual 0" is a sentence the owner cannot act on. The
    # services were the only folder checked until v0.29.7, which is the same half-a-gate that let
    # the SQL rule miss the tests.
    sources = sorted((DOTNET / "Area11.Core/Services").glob("*.cs"))
    sources += sorted((DOTNET / "Area11.Tests").glob("*.cs"))

    for path in sources:
        text = path.read_text(encoding="utf-8-sig")
        local = _classes_in(text)

        consts = _const_sqls(text)

        queries = [(match.group(1), match.group(2).replace('""', '"'), True)
                   for match in inline.finditer(text)]

        for match in named.finditer(text):
            argument = match.group(2)

            for name in re.findall(r"\b(\w+)\b", argument):
                if name in consts:
                    queries.append((match.group(1), consts[name], False))

        for model_name, sql, from_literal in queries:
            if not from_literal and not sql.strip():
                continue

            model = model_name.split(".")[-1]

            names = set(local.get(model, set()))
            base = _base_of(text, model)

            while base:
                names |= local.get(base, set())
                names |= models.get(base, set())
                base = _base_of(text, base) if base in local else ""

            if not names:
                names = set(models.get(model, set()))

            if not names:
                continue

            homes = {_normalise(name) for name in names}

            for span in _top_level_spans(sql):
                # Split on the mask, not on the raw text: a comma inside 'Returned, cash
                # received: ' is part of a word, not the end of a column.
                chars, depths = _mask(span)
                items = []
                start = 0

                for index in range(len(span)):
                    if chars[index] == "," and depths[index] == 0:
                        items.append(span[start:index])
                        start = index + 1

                items.append(span[start:])

                for one in items:
                    alias = _alias_of(one)
                    if alias is None or alias.startswith("@"):
                        continue
                    checked += 1
                    if _normalise(alias) in homes or alias in ALIASES_WITHOUT_PROPERTY:
                        continue
                    problems += 1
                    fail(f"{rel(path)}: column '{alias}' has no property on {model} "
                         f"(Dapper drops it and the field stays 0)")

    REPORT.append(f"Column mapping: {checked} selected column(s) checked against their model"
                  + (f", {len(sources)} file(s)" if len(sources) else ""))


# ---------------------------------------------------------------------------
# 4c. Package versions that were flagged by the first Windows build
# ---------------------------------------------------------------------------
def check_packages() -> None:
    """NU1903 on the owner's PC: SQLitePCLRaw.lib.e_sqlite3 2.1.11 (GHSA-2m69-gcr7-jv3q).

    The fix is an explicit pin, because the vulnerable version arrives transitively. This check
    keeps the pin from being "cleaned up" by a future tidy-up.
    """
    core = (DOTNET / "Area11.Core/Area11.Core.csproj").read_text(encoding="utf-8")

    needed = {
        "Microsoft.Data.Sqlite": "10.0.2",
        "SQLitePCLRaw.bundle_e_sqlite3": "2.1.12",
    }
    for package, version in needed.items():
        match = re.search(rf'Include="{re.escape(package)}"\s+Version="([^"]+)"', core)
        if match is None:
            fail(f"Area11.Core.csproj no longer references {package}")
        elif match.group(1) != version:
            fail(f"{package} is {match.group(1)} - the patched pin is {version}")

    REPORT.append("Packages: SQLite pinned to the patched 2.1.12")


# ---------------------------------------------------------------------------
# 4d. One version, one colour: BuildInfo == Theme.xaml == docs/VERSIONS.md
# ---------------------------------------------------------------------------
# `public int Id { get; }`, `public List<Row> Rows { get; } = new();` and the same with `init;` -
# only the first two are getter-only. The trailing `;?` matters: an initialised property ends in `;`,
# and without it the pattern silently matched nothing (which is how this check first missed its bug).
_READONLY_PROP_RE = re.compile(
    r"(?m)^[ \t]*(?:public|private|protected|internal)[^\n;{}]*?\b(\w+)\s*\{\s*get;\s*\}"
    r"\s*(?:=[^;\n]*)?;?$")

_INITIALIZER_RE = re.compile(r"\bnew\s+([\w.]+)\s*(?:\([^()]*\))?\s*\{")

_ASSIGN_RE = re.compile(r"(?<![\w.])(\w+)\s*=\s*(?!=)")


def check_readonly_members() -> None:
    """An object initializer may not assign a property that only has a getter (CS0200).

    Why: `SaleSample.Lines { get; } = new();` was assigned in `new SaleSample { Lines = lines }`.
    Nothing else in this checker could see it - the C# parser is happy, the SQL is right, the XAML
    binds - and it stopped the owner's first build of v0.26.0 on his own PC. A getter-only property
    is legal and often right; assigning it outside the constructor is the compile error.

    How: every type whose name is declared once in the repository gets the set of its getter-only
    properties; then every `new Type { ... }` block is read, nested braces are dropped, and each
    `Name =` inside is checked against that set. A type declared twice is skipped rather than guessed.
    """
    declarations: dict[str, set[str]] = {}
    duplicates: set[str] = set()
    sources: dict[pathlib.Path, str] = {}

    for path in sorted(DOTNET.rglob("*.cs")):
        if any(part in {"obj", "bin"} for part in path.parts):
            continue

        text = path.read_text(encoding="utf-8-sig")
        sources[path] = text

        for match in re.finditer(r"(?m)^[ \t]*(?:public|private|protected|internal|file)?[^\n;{}]*?"
                                 r"\b(?:class|record|struct)\s+(\w+)", text):
            name = match.group(1)
            block = text[match.start():match.start() + 20000]
            opened = block.find("{")
            if opened < 0:
                continue

            depth = 0
            end = len(block)
            for index in range(opened, len(block)):
                if block[index] == "{":
                    depth += 1
                elif block[index] == "}":
                    depth -= 1
                    if depth == 0:
                        end = index
                        break

            readonly = {prop.group(1) for prop in _READONLY_PROP_RE.finditer(block[opened:end])}

            if name in declarations:
                duplicates.add(name)
            declarations[name] = declarations.get(name, set()) | readonly

    checked = 0
    initializers = 0

    for name in duplicates:
        declarations.pop(name, None)

    for path, text in sources.items():
        for match in _INITIALIZER_RE.finditer(text):
            type_name = match.group(1).split(".")[-1].split("<")[0]
            readonly = declarations.get(type_name)

            if not readonly:
                continue

            start = match.end() - 1
            depth = 0
            end = None

            for index in range(start, min(len(text), start + 20000)):
                if text[index] == "{":
                    depth += 1
                elif text[index] == "}":
                    depth -= 1
                    if depth == 0:
                        end = index
                        break

            if end is None:
                continue

            initializers += 1

            # Only the assignments that belong to THIS initializer count. A nested `new Y { ... }`
            # has its own properties, so everything inside an inner brace pair - and inside a string -
            # is blanked out, character for character, to keep the line numbers exact.
            body = text[start:end]
            flat = []
            depth = 0
            index = 0

            while index < len(body):
                character = body[index]

                if character == '"':
                    flat.append(" ")
                    index += 1

                    while index < len(body):
                        if body[index] == "\\":
                            flat.append("  ")
                            index += 2
                            continue

                        if body[index] == '"':
                            flat.append(" ")
                            index += 1
                            break

                        flat.append(" ")
                        index += 1

                    continue

                if character == "{":
                    depth += 1
                    flat.append(" ")
                elif character == "}":
                    depth -= 1
                    flat.append(" ")
                else:
                    flat.append(character if depth <= 1 else " ")

                index += 1

            flat = "".join(flat)

            for assignment in _ASSIGN_RE.finditer(flat):
                prop = assignment.group(1)

                if prop not in readonly:
                    continue

                line = text[:start + assignment.start()].count("\n") + 1
                fail(f"{rel(path)}:{line}: '{prop}' of '{type_name}' is getter-only, so an object "
                     f"initializer cannot set it (CS0200) - give it `init;` or set it in the "
                     f"constructor")

            checked += 1

    REPORT.append(f"Read-only members: {checked} object initializers checked against "
                  f"{len(declarations)} types")


def check_version_colour() -> None:
    """The owner identifies a build by its colour, so the three places that describe it must agree.

    Why this is a hard failure: if the theme and the label disagree, the owner sees "purple" in the
    sidebar while the app is painted pink, and he cannot tell which copy of the folder is running.
    """
    build_info = (DOTNET / "Area11.Core/Utils/BuildInfo.cs").read_text(encoding="utf-8")
    theme = (DOTNET / "Area11.Desktop/Resources/Theme.xaml").read_text(encoding="utf-8")
    versions = (ROOT / "docs/VERSIONS.md").read_text(encoding="utf-8")

    version = re.search(r'Version\s*=\s*"([^"]+)"', build_info)
    code_name = re.search(r'CodeName\s*=\s*"([^"]+)"', build_info)
    brand = re.search(r'BrandHex\s*=\s*"([^"]+)"', build_info)
    dark = re.search(r'BrandDarkHex\s*=\s*"([^"]+)"', build_info)
    soft = re.search(r'BrandSoftHex\s*=\s*"([^"]+)"', build_info)

    for match, label in ((version, "Version"), (code_name, "CodeName"), (brand, "BrandHex"),
                         (dark, "BrandDarkHex"), (soft, "BrandSoftHex")):
        if match is None:
            fail(f"BuildInfo.cs is missing {label}")
            return

    theme_brand = re.search(r'x:Key="BrandBrush"\s+Color="([^"]+)"', theme)
    theme_dark = re.search(r'x:Key="BrandDarkBrush"\s+Color="([^"]+)"', theme)
    theme_soft = re.search(r'x:Key="BrandSoftBrush"\s+Color="([^"]+)"', theme)

    if theme_brand is None or theme_dark is None or theme_soft is None:
        fail("Theme.xaml is missing one of the Brand brushes")
        return

    pairs = [
        ("BrandBrush", brand.group(1), theme_brand.group(1)),
        ("BrandDarkBrush", dark.group(1), theme_dark.group(1)),
        ("BrandSoftBrush", soft.group(1), theme_soft.group(1)),
    ]
    for name, expected, actual in pairs:
        if expected.lower() != actual.lower():
            fail(f"{name}: Theme.xaml says {actual} but BuildInfo.cs says {expected}")

    # last row of the released-versions table
    version_lines = versions.splitlines()
    row_at = [index for index, line in enumerate(version_lines)
              if re.match(r"^\|\s*\d+\.\d+\.\d+\s*\|", line)]
    rows = [version_lines[index] for index in row_at]
    if not rows:
        fail("docs/VERSIONS.md has no released-version row")
        return

    # One table, not two. Rows that keep being added to the end of a file that has a "Notes" section
    # in the middle end up **after** the notes - and a table broken in the middle reads like a
    # complete history to anyone who stops scrolling at the last row before the heading. Every round
    # is required to add one row, so the break grows by one version each time and is never noticed.
    gaps = [first for first, second in zip(row_at, row_at[1:]) if second - first != 1]
    if gaps:
        fail("docs/VERSIONS.md: the released-version table is broken after "
             f"{version_lines[gaps[0]][:40]!r} - every row must sit in one table, with the notes "
             "below it")

    cells = [cell.strip() for cell in rows[-1].strip().strip("|").split("|")]
    if len(cells) < 5:
        fail(f"the last row of docs/VERSIONS.md is incomplete: {rows[-1]}")
        return

    released_version, _, released_name, released_hex = cells[0], cells[1], cells[2], cells[3]
    if released_version != version.group(1):
        fail(f"docs/VERSIONS.md ends at {released_version} but BuildInfo.Version is {version.group(1)}")
    if released_name.lower() != code_name.group(1).lower():
        fail(f"docs/VERSIONS.md calls this build '{released_name}' but BuildInfo.CodeName is '{code_name.group(1)}'")
    if released_hex.strip("`").lower() != brand.group(1).lower():
        fail(f"docs/VERSIONS.md colour is {released_hex} but BuildInfo.BrandHex is {brand.group(1)}")

    REPORT.append(f"Version: v{version.group(1)} ({code_name.group(1)}, {brand.group(1)}) - theme and VERSIONS.md agree")


# ---------------------------------------------------------------------------
# 5. Tests and settings
# ---------------------------------------------------------------------------
def check_tests() -> None:
    files = sorted((DOTNET / "Area11.Tests").glob("*Tests.cs"))
    facts = theories = inline = 0
    for path in files:
        text = path.read_text(encoding="utf-8")
        facts += len(re.findall(r"\[Fact\]", text))
        theories += len(re.findall(r"\[Theory\]", text))
        inline += len(re.findall(r"\[InlineData", text))

    cases = facts + inline
    REPORT.append(f"Tests: {facts} facts + {theories} theories ({inline} inline rows) "
                  f"= {cases} cases in {len(files)} files")

    # The double-click test runner prints "expect total: N" before it starts, because the owner has
    # twice sent a result from a tree that was not pulled (686 when 695 was current) and read it as a
    # fault in the shop. That number is typed by hand in the batch file, so it needs a keeper: here.
    script = ROOT / "tools" / "RUN-TESTS.cmd"

    if script.exists():
        runner = script.read_text(encoding="utf-8", errors="replace")
        wanted = re.search(r'set "EXPECTED=(\d+)"', runner)

        if wanted is None:
            fail("tools/RUN-TESTS.cmd no longer says how many cases to expect "
                 '(the line `set "EXPECTED=<number>"` is gone)')
        elif int(wanted.group(1)) != cases:
            fail(f"tools/RUN-TESTS.cmd expects {wanted.group(1)} cases but the suite has {cases} - "
                 "the owner would read a correct run as a stale pull")
        else:
            REPORT.append(f"Test runner: the printed expectation ({cases}) matches the suite")
    else:
        fail("tools/RUN-TESTS.cmd is missing - the owner cannot run the tests by double-click")

    # The three double-click scripts each print one line saying WHICH build is about to run. That line
    # is typed by hand, so it needs a keeper: v0.30.0 read the number out of the source with findstr
    # and printed "vunknown" on the owner's PC, which is worse than printing nothing - a banner that
    # says nothing looks like a broken tree. The scripts now hold a literal, and this rule compares it
    # with BuildInfo, so a version bump that forgets the scripts fails here instead of on his screen.
    build_info = (DOTNET / "Area11.Core/Utils/BuildInfo.cs").read_text(encoding="utf-8-sig")
    version = re.search(r'public const string Version = "([^"]+)"', build_info)
    code_name = re.search(r'public const string CodeName = "([^"]+)"', build_info)

    if version and code_name:
        wanted = f"v{version.group(1)} ({code_name.group(1)})"

        for name in SCRIPTS_THAT_NAME_THE_BUILD:
            script_path = ROOT / name

            if not script_path.exists():
                continue

            body = script_path.read_text(encoding="utf-8", errors="replace")
            printed = re.search(r'set "BUILD=([^"]*)"', body)

            if printed is None:
                fail(f"{name} no longer says which build it runs (the line `set \"BUILD=...\"` is gone)")
            elif printed.group(1) != wanted:
                fail(f"{name} will print '{printed.group(1)}' but this build is '{wanted}' - "
                     "the owner would read the wrong version on his own screen")

        REPORT.append(f"Script banners: {len(SCRIPTS_THAT_NAME_THE_BUILD)} file(s) print '{wanted}'")

    settings = (DOTNET / "Area11.Core/Services/SettingsService.cs").read_text(encoding="utf-8")
    # The lookbehind matters: "GetRaw(" ends in "Raw(", and without it every reader of a setting
    # looks like a second declaration of it.
    keys = re.findall(r'(?<![A-Za-z0-9_])(?:Text|Number|Flag|Raw)\("([a-zA-Z0-9_.]+)"', settings)

    # Application labels are deliberately generated from one Core catalog instead of repeating 32
    # literal Text(...) calls. Count those real defaults too, and refuse a catalog that is no longer
    # installed into the settings table.
    label_source = (DOTNET / "Area11.Core/Services/AppLabelService.cs")
    if label_source.exists():
        label_keys = re.findall(r'new AppLabelDefinition\("([A-Za-z0-9]+)"',
                                label_source.read_text(encoding="utf-8"))
        if label_keys and "foreach (var label in AppLabelService.Definitions)" not in settings:
            fail("AppLabelService has a catalog but SettingsService does not install its defaults")
        keys.extend("labels." + key for key in label_keys)

    if len(keys) != len(set(keys)):
        duplicated = sorted({k for k in keys if keys.count(k) > 1})
        fail(f"duplicate settings keys: {duplicated}")
    REPORT.append(f"Settings: {len(set(keys))} keys")


# ---------------------------------------------------------------------------
# 5b. The installer: two double-click files, and one set of names
# ---------------------------------------------------------------------------
def check_installer() -> None:
    """The installer is the one part of the program that runs before the program.

    Why this check exists: `InstallService.cs` is the rulebook and the two PowerShell scripts are the
    hands, and a folder name typed once in one place and once in the other is exactly the kind of
    mistake no test can catch - the test would pass and the shop PC would install to a folder the
    program never opens. So the constants are read out of the C# and searched for in the scripts.
    """
    service = (DOTNET / "Area11.Core/Services/InstallService.cs").read_text(encoding="utf-8")
    install = (ROOT / "tools/install/install.ps1").read_text(encoding="utf-8")
    uninstall = (ROOT / "tools/install/uninstall.ps1").read_text(encoding="utf-8")
    program = (DOTNET / "Area11.Desktop/Program.cs").read_text(encoding="utf-8")

    def constant(name: str) -> str:
        match = re.search(rf'const string {name}\s*=\s*(?:@"([^"]*)"|"([^"]*)")', service)
        if match is None:
            fail(f"InstallService.cs no longer declares {name}, but the installer needs it")
            return ""
        return match.group(1) if match.group(1) is not None else match.group(2)

    # The scripts must use the very same names the C# hands them.
    for name, script, where in (
        ("ExeName", install, "the installer"),
        ("ExeName", uninstall, "the uninstaller"),
        ("UninstallerName", install, "the installer"),
        ("UninstallKey", install, "the installer"),
        ("UninstallKey", uninstall, "the uninstaller"),
        ("PdfPrinterName", install, "the installer"),
        ("EveryoneSid", install, "the installer"),
        ("UninstallShortcutName", install, "the installer"),
        ("UninstallShortcutName", uninstall, "the uninstaller"),
    ):
        value = constant(name)
        if value and value not in script:
            fail(f"{where} script does not use InstallService.{name} ({value}) - the two have drifted")

    # The entry point the installer calls must still exist on both sides.
    if "--install-check" not in install:
        fail("tools/install/install.ps1 no longer asks Area11 whether the PC is ready")
    if "--install-check" not in program:
        fail("Program.cs no longer answers --install-check, which the installer relies on")

    # Two folders, never one: the scripts must name both environment folders.
    for required in ("$env:ProgramFiles", "$env:ProgramData", "/grant", "robocopy"):
        if required not in install:
            fail(f"tools/install/install.ps1 no longer contains {required}")

    # The uninstaller must never delete the shop. One line of `Remove-Item` on the data folder would
    # destroy every bill the shop ever made, so it is refused here rather than trusted.
    for line in uninstall.splitlines():
        if "Remove-Item" in line and ("ProgramData" in line or "$dataFolder" in line):
            fail("tools/install/uninstall.ps1 deletes the shop data - that folder is never removed")

    # The owner's rule: a double-click file waits for a keypress, so the window does not vanish
    # before the message is read.
    for name in ("INSTALL-AREA11.cmd", "UNINSTALL-AREA11.cmd", "MAKE-INSTALLER.cmd"):
        text = (ROOT / "tools" / name).read_text(encoding="utf-8")
        if "\npause" not in text:
            fail(f"tools/{name} never pauses - the owner cannot read its message")

    # The icon must exist, be a real icon, and be painted in THIS version's colour (D-24). The hex is
    # regenerated by tools/make_icon.py, so a forgotten regeneration cannot pass.
    icon = DOTNET / "Area11.Desktop/Resources/area11.ico"
    data = icon.read_bytes()
    if not data.startswith(b"\x00\x00\x01\x00"):
        fail("Resources/area11.ico is not an icon file (bad header)")

    build_info = (DOTNET / "Area11.Core/Utils/BuildInfo.cs").read_text(encoding="utf-8")
    brand = re.search(r'BrandHex\s*=\s*"#([0-9A-Fa-f]{6})"', build_info)
    if brand is not None:
        value = brand.group(1)
        red, green, blue = (int(value[i:i + 2], 16) for i in (0, 2, 4))
        if bytes((blue, green, red)) not in data:
            fail("Resources/area11.ico is not painted in BuildInfo.BrandHex - run tools/make_icon.py")

    # Windows shows the assembly version in Properties and the installer writes it to Add/Remove.
    props = (DOTNET / "Directory.Build.props").read_text(encoding="utf-8")
    assembly_version = re.search(r"<Version>([^<]+)</Version>", props)
    build_version = re.search(r'Version\s*=\s*"([^"]+)"', build_info)
    if assembly_version is None or build_version is None:
        fail("cannot read the version from Directory.Build.props or BuildInfo.cs")
    elif assembly_version.group(1).strip() != build_version.group(1).strip():
        fail(f"Directory.Build.props says {assembly_version.group(1)} but BuildInfo.Version is "
             f"{build_version.group(1)} - Windows would show the wrong number")

    # `dist` is a build output, never a commit.
    if "/dist/" not in (ROOT / ".gitignore").read_text(encoding="utf-8"):
        fail(".gitignore does not ignore /dist/ (the published program must never be committed)")

    REPORT.append("Installer: names agree with InstallService.cs, icon in the version colour, "
                  "uninstaller keeps the shop, dist ignored")


# ---------------------------------------------------------------------------
# 6. Documentation links and referenced files
# ---------------------------------------------------------------------------
# 7c. The test runner must be able to say "green"
# ---------------------------------------------------------------------------
def check_run_verdict() -> None:
    """A runner that cannot tell success from failure is a log, not a runner.

    Why this exists: `tools\\RUN-TESTS.cmd` printed

        "Not OK is not a disaster: send the lines above ..."

    on **every** run, including the owner's first fully green one (686 of 686, failed: 0,
    23 Oct 2026). The run was perfect and the tool told him it was not - at the exact moment
    he was supposed to trust it. That is worse than no message: the next time it really is
    red, he has already learned that the sentence means nothing.

    A batch file has no compiler, so this gate reads the script as text and asks four things:

      * there is a success branch at all (`ALL GREEN`), not only a failure message;
      * the failure advice lives **after** its `:testsnotok` label, so it can only print on
        failure - the literal sentence must not sit in the main flow;
      * the exit code of `dotnet test` is captured (`TESTEXIT`) instead of trusted to luck;
      * a green run of an OLD folder is caught (`LSS %EXPECTED%`), because a smaller total
        means the pull did not happen and the green proves nothing.

    It also checks that the build failure can name the project that broke - the one fact
    that matters when the window fails and the shop's own rules compiled fine.
    """
    runner = ROOT / "tools" / "RUN-TESTS.cmd"
    launcher = ROOT / "START-AREA11.bat"

    if not runner.exists():
        fail("tools\\RUN-TESTS.cmd is missing - the owner has no way to run the tests")
        return

    text = runner.read_text(encoding="utf-8", errors="replace")

    if "ALL GREEN" not in text:
        fail("tools\\RUN-TESTS.cmd never says a run was green - a passing run would read as a failure")

    if "TESTEXIT" not in text:
        fail("tools\\RUN-TESTS.cmd does not keep the exit code of `dotnet test` - "
             "a failing run could be reported as green")

    if "testsnotok" not in text:
        fail("tools\\RUN-TESTS.cmd has no separate failure branch (:testsnotok)")

    # The failure sentence must sit inside the failure branch, not in the main flow. Find the
    # label and the sentence; the sentence must come after it.
    label_at = text.find(":testsnotok")
    sentence_at = text.find("NOT OK - and that is fixable")

    if sentence_at < 0:
        fail("tools\\RUN-TESTS.cmd lost its failure message")

    if label_at < 0 or sentence_at < label_at:
        fail("tools\\RUN-TESTS.cmd prints the failure advice before it knows the result - "
             "a green run would end with 'not OK' (the v0.30.8 bug)")

    if not re.search(r"if\s+%TOTALN%\s+LSS\s+%EXPECTED%", text):
        fail("tools\\RUN-TESTS.cmd does not compare the total with EXPECTED - "
             "a green run of an old folder would be reported as a pass")

    if not launcher.exists():
        fail("START-AREA11.bat is missing")
        return

    launch = launcher.read_text(encoding="utf-8", errors="replace")

    if "The fault is in this project" not in launch:
        fail("START-AREA11.bat does not name the project that failed to build - "
             "the owner would have to read every error line to learn whether it was the shop's rules or the window")

    REPORT.append("Run verdict: the runner says ALL GREEN only on a full pass, counts the cases "
                  "against EXPECTED, and the launcher names the failing project")


# ---------------------------------------------------------------------------
def check_shift_report_gate() -> None:
    """Who was short is a management screen, and its two totals must stay two totals.

    Why this exists: v0.31.0 added the per-person shift report (F-56), and it has two ways to go
    quietly wrong - both of them silent, both of them expensive:

      * **the shop's short could be netted against its over.** The first version of `ByPerson`
        summed each person's net difference, so a shop short Rs 500 once and over Rs 200 once
        read as "Rs 300 short" - and both facts vanished. A real SQLite file caught it in one run
        (1500 short / 200 over came out 1300 / 0). The rule is now structural: the shop's two
        numbers come from a **second query over the shift rows**, and `ByPerson` never calls
        `.Sum(` over people. A gate cannot see a database, but it can see that the summing code
        is not there to be written again by accident.
      * **the card could be shown to the wrong person.** A cashier may open the till and still
        not read a colleague's differences. The view-model hides the table with `CanManage` and
        the service refuses the same person; if only one of the two changed, the shop would
        either show a page that then errors or hide a page the manager needs. Both must use the
        same rule and the same sentence, or the screen and the service drift apart and the drift
        is invisible until the shop is open.

    This is a static check - it reads the four files. The behaviour is proved by
    `ShiftSummaryTests` (9 cases) on the shop PC.
    """
    service = ROOT / "dotnet/Area11.Core/Services/ShiftService.cs"
    view_model = ROOT / "dotnet/Area11.Desktop/ViewModels/ShiftViewModel.cs"
    view = ROOT / "dotnet/Area11.Desktop/Views/ShiftView.xaml"

    for needed in (service, view_model, view):
        if not needed.exists():
            fail(f"the per-person shift report is half-built: {rel(needed)} is missing")
    if not all(path.exists() for path in (service, view_model, view)):
        return

    code = service.read_text(encoding="utf-8", errors="replace")

    marker = "public ServiceResult<ShiftPersonReport> ByPerson"

    if marker not in code:
        fail("ShiftService has no ByPerson - the per-person report has no service behind it")
        return

    start = code.index(marker)
    # Current role/module is resolved inside try, before the first money query.
    query_at = code.find("var people = _db.Query<ShiftPersonSummary>", start)
    guard = code[start:query_at if query_at > start else start + 2000]

    if "RequireCash(connection, user)" not in guard or "reader.CanManage" not in guard:
        fail("ByPerson does not ask for both the Cash permission and a manager - "
             "a cashier with the till permission could read a colleague's differences")

    if "Only the owner or a manager" not in guard:
        fail("ByPerson refuses without saying who may look - an unexplained refusal reads as a fault")

    if "QuerySingle<ShopTotals>" not in code[start:]:
        fail("ByPerson has no separate shop-totals query - the short and over would have to be "
             "summed from the people, and netting them hides both facts")

    if ".Sum(" in code:
        fail("ShiftService sums people - the shop's short and over must come from the shift rows, "
             "never from the per-person nets (a shop short 500 and over 200 is not '300 short')")

    if "private sealed class ShopTotals" not in code:
        fail("ShiftService lost its private ShopTotals row - the shop's own totals have no home")

    screen = view_model.read_text(encoding="utf-8", errors="replace")

    can_line = next((line for line in screen.splitlines() if "CanSeePeople =>" in line), "")

    if not can_line:
        fail("ShiftViewModel has no CanSeePeople - the card's visibility has no rule")

    if "_canSeePeople" not in can_line or "SetAccess(true, page.Value.CanSeePeople)" not in screen:
        fail("ShiftViewModel shows the per-person card to somebody who is not a manager - "
             "CanSeePeople must be the same rule the service refuses on")

    body = screen[screen.find("private void LoadPeopleSummary"):]
    body = body[:body.find("private void ") if body.find("private void ") > 0 else 1200]

    if "ByPerson(" not in body:
        fail("LoadPeopleSummary never calls ByPerson - the card would sit empty with no reason")

    if "Only the owner or a manager" not in body:
        fail("LoadPeopleSummary has no refusal sentence - an empty table reads as "
             "'nobody was ever short', which is the one wrong answer")

    xaml = view.read_text(encoding="utf-8", errors="replace")
    table = 'ItemsSource="{Binding PeopleSummary}"'
    count = xaml.count(table)

    if count == 0:
        fail("ShiftView has no table bound to PeopleSummary - the report has no screen")
    elif count > 1:
        fail("ShiftView binds PeopleSummary more than once - a second copy can escape the "
             "manager-only wrapper and show a cashier what the service refuses him")

    if count == 1:
        at = xaml.index(table)
        before = xaml[max(0, at - 900):at]

        if "Visibility=\"{Binding CanSeePeople" not in before or "BoolToVis" not in before:
            fail("the PeopleSummary table in ShiftView is not wrapped in the CanSeePeople "
                 "visibility - a cashier would see the table the service refuses to fill")

    REPORT.append("Shift report: manager-only on both sides, and the shop's short and over are "
                  "a second query - never the sum of the people's nets")


# ---------------------------------------------------------------------------
def _property_setters() -> dict[str, dict[str, bool]]:
    """class -> {property: True when WPF may only READ it}.

    Why this is parsed from C# and not assumed: a property is read-only to a binding only when
    it has no **public** setter. `{ get; private set; }` counts as read-only too, because the
    binding engine cannot call a private setter; `=>` members never have one at all.
    """
    found: dict[str, dict[str, bool]] = {}

    for path in DOTNET.rglob("*.cs"):
        if {"obj", "bin"} & set(path.parts):
            continue

        text = path.read_text(encoding="utf-8-sig", errors="replace")

        for match in re.finditer(r"\bclass\s+(\w+)", text):
            brace = text.find("{", match.end())

            if brace < 0:
                continue

            depth = 0
            end = len(text)

            for index in range(brace, len(text)):
                if text[index] == "{":
                    depth += 1
                elif text[index] == "}":
                    depth -= 1
                    if depth == 0:
                        end = index
                        break

            body = text[brace:end]
            room = found.setdefault(match.group(1), {})

            for prop in re.finditer(r"^\s*public\s+(?:static\s+)?(?:override\s+)?(?:virtual\s+)?"
                                    r"(?:sealed\s+)?[\w<>?,\[\]\. ]+?\s+(\w+)\s*(\{|=>)",
                                    body, re.M):
                name, shape = prop.group(1), prop.group(2)

                if shape == "=>":
                    room[name] = True
                    continue

                start = body.index("{", prop.end() - 1)
                level = 0
                stop = len(body)

                for index in range(start, len(body)):
                    if body[index] == "{":
                        level += 1
                    elif body[index] == "}":
                        level -= 1
                        if level == 0:
                            stop = index
                            break

                block = body[start:stop]
                setter = re.search(r"(?<!\w)(private|protected|internal)?\s*set\s*[;{]|set\s*=>", block)

                room[name] = not setter or bool(setter.group(1))

    return found


def check_stock_policy_gate() -> None:
    """D-93: every ordinary stock decrement has one guarded, transactional door.

    A source scan is useful here because a future bypass is otherwise just one harmless-looking
    SQL line. Additions to stock remain ordinary additions. StockTakeService is the one exception
    that assigns an absolute number: it is the owner-only, audited physical-count reconciliation,
    not a decrement API. Historical negative rows are never rewritten by this gate or a migration.
    """
    services = DOTNET / "Area11.Core/Services"
    policy = services / "StockPolicy.cs"
    guarded_callers = (
        "SaleService.cs",
        "StockService.cs",
        "ExpenseService.cs",
        "SupplierLedgerService.cs",
        "ProvisionalReturnService.cs",
    )

    if not policy.exists():
        fail("D-93 StockPolicy.cs is missing; ordinary decrements have no central guard")
        return

    for name in guarded_callers:
        path = services / name
        if not path.exists():
            fail(f"D-93 decrement service is missing: {rel(path)}")
            continue

        text = path.read_text(encoding="utf-8", errors="replace")
        if "StockPolicy.Decrease(" not in text:
            fail(f"{name} no longer calls StockPolicy.Decrease; a stock decrement can bypass D-93")

    policy_text = policy.read_text(encoding="utf-8", errors="replace")
    normalized_policy = re.sub(r"\s+", " ", policy_text)
    if not re.search(r"SET qty_base = qty_base - @quantity", normalized_policy, re.IGNORECASE):
        fail("StockPolicy lost its single guarded batch decrement")
    if not re.search(r"qty_base >= @quantity", normalized_policy, re.IGNORECASE):
        fail("StockPolicy's UPDATE no longer requires enough quantity in the selected batch")
    if "WHERE b.product_id = @productId AND b.active = 1) >= @quantity" not in normalized_policy:
        fail("StockPolicy's UPDATE no longer protects the active product total")
    if "internal static class StockPolicy" not in policy_text:
        fail("StockPolicy must stay an internal service boundary, not a public bypass API")

    update_count = 0
    for path in services.glob("*.cs"):
        text = path.read_text(encoding="utf-8", errors="replace")
        for match in re.finditer(r"UPDATE\s+batches\b", text, re.IGNORECASE):
            update_count += 1
            statement = re.sub(r"\s+", " ", text[match.start():match.start() + 700])

            if path.name == "StockPolicy.cs":
                continue  # the strict conditional decrement was checked above

            if path.name == "StockTakeService.cs":
                if not re.search(r"SET qty_base = @qty", statement, re.IGNORECASE):
                    fail("StockTakeService may assign only the audited physical count to a batch")
                continue

            if not re.search(r"SET qty_base = qty_base \+ @qty", statement, re.IGNORECASE):
                fail(f"{rel(path)} contains an unguarded batch decrement or absolute assignment; "
                     "use StockPolicy.Decrease (stock-take is the sole absolute-count exception)")

    stock_take = (services / "StockTakeService.cs").read_text(encoding="utf-8", errors="replace")
    if "!double.IsFinite(counted) || counted < 0" not in stock_take:
        fail("The physical-count exception no longer validates a finite, non-negative count")

    legacy_readers = []
    for tree in (DOTNET / "Area11.Core", DOTNET / "Area11.Desktop"):
        for path in tree.rglob("*.cs"):
            text = path.read_text(encoding="utf-8", errors="replace")
            if re.search(r"GetBool\s*\(\s*[\"']stock\.allowNegative[\"']", text):
                legacy_readers.append(rel(path))
    if legacy_readers:
        fail("the retired negative-stock bypass is still read by: " + ", ".join(legacy_readers))

    REPORT.append(
        f"Stock policy: {len(guarded_callers)} decrement services use the central guard, "
        f"{update_count} batch updates are guarded/additions/physical counts, "
        "and the legacy allow-negative setting has no production reader")


def check_binding_modes() -> None:
    """A binding that can write must never point at a property that cannot be written.

    Why this is a whole check and not a style opinion: WPF gives **`Run.Text`** and **every
    `DataGrid...Column.Binding`** a default mode of TwoWay (the framework registers
    `Run.Text` with `BindsTwoWayByDefault`). A computed label - `QtyLabel`, `RateLabel`,
    `TotalLabel` - has no setter, so the moment such a binding is attached WPF throws

        InvalidOperationException: A TwoWay or OneWayToSource binding cannot work on the
        read-only property 'QtyLabel' of type 'Area11.Core.Models.SaleHistoryLine'

    inside the layout pass, which surfaces as a `XamlParseException` and takes the window down
    with it. That is exactly what the owner's crash report of 22 Oct 2026 showed - the first
    time the WPF program ever ran a page with a grid on it. **123 bindings** in this repository
    were in that state, and nothing in the build could see it: XAML is not compiled by the C#
    compiler, and the fault only exists at the moment a template is created.

    The rule is deliberately narrow so it cannot cry wolf: a name counts as read-only only when
    **every** class that declares it has no public setter, so the writable `Code` of a bill row
    is never accused just because some other row class computes a `Code`.
    """
    targets = {
        "Run": {"Text"},
        "TextBox": {"Text"},
        "ComboBox": {"SelectedItem", "SelectedValue", "Text"},
        "CheckBox": {"IsChecked"},
        "RadioButton": {"IsChecked"},
        "ToggleButton": {"IsChecked"},
        "DatePicker": {"SelectedDate"},
        "Slider": {"Value"},
        "ListBox": {"SelectedItem"},
        "TabControl": {"SelectedItem"},
    }

    index = _property_setters()
    problems = 0
    checked = 0

    def only_readable(name: str, owner: str) -> bool:
        """True when the property has no public setter on the object the row really is.

        The owner matters: `StatusLabel` is computed on a drawer but a plain field on a sync
        row, so a name is never accused merely because some unrelated class computes it. When
        the row type cannot be worked out (an ItemsSource that is not a simple collection), the
        answer falls back to "read-only everywhere" - narrow, never a false accusation.
        """
        if owner and owner in index and name in index[owner]:
            return index[owner][name]

        owners = [room for room in index if name in index[room]]
        return bool(owners) and all(index[room][name] for room in owners)

    for view in sorted((DOTNET / "Area11.Desktop" / "Views").glob("*.xaml")):
        text = view.read_text(encoding="utf-8", errors="replace")
        vm_name = VIEW_MODEL_OF.get(view.name, "")
        vm_path = DOTNET / "Area11.Desktop" / "ViewModels" / f"{vm_name}.cs"
        vm_text = vm_path.read_text(encoding="utf-8", errors="replace") if vm_path.exists() else ""

        scopes: list[tuple[str, str]] = []

        for number, line in enumerate(text.splitlines(), 1):
            owner = scopes[-1][1] if scopes and scopes[-1][1] else vm_name

            if "{Binding" in line:
                tag_match = re.search(r"<([A-Za-z][\w.]*)", line)

                if tag_match:
                    tag = tag_match.group(1)

                    for attr in re.finditer(r'([A-Za-z][\w.]*)="(\{Binding[^"]*)"', line):
                        attribute, value = attr.group(1), attr.group(2)
                        inner = value[len("{Binding"):-1]
                        path = inner.split(",")[0].strip()
                        name = path.split(".")[0]

                        if not re.fullmatch(r"[A-Za-z_]\w*", name):
                            continue

                        two_way = (tag in targets and attribute in targets[tag]) or \
                                  (tag.endswith("Column") and attribute == "Binding")

                        if not two_way:
                            continue

                        checked += 1

                        if "Mode=OneWay" in inner or "Mode=OneTime" in inner:
                            continue

                        if only_readable(name, owner):
                            problems += 1
                            fail(f"{view.name}:{number}: {tag}.{attribute} binds the read-only "
                                 f"property '{name}' of {owner or 'the page'} without Mode=OneWay - "
                                 f"WPF throws at render time and the window closes "
                                 f"(this is the 22 Oct crash)")

            opened = re.search(r"<(DataGrid|ItemsControl|ListBox|ComboBox)\b", line)

            if opened and 'ItemsSource="{Binding ' in line:
                collection = re.search(r'ItemsSource="\{Binding\s+([\w.]+)', line)
                element = _element_type(vm_text, collection.group(1).split(".")[-1]) if collection else ""

                if "/>" not in line:
                    scopes.append((opened.group(1), element))

            closed = re.search(r"</(DataGrid|ItemsControl|ListBox|ComboBox)>", line)

            if closed and scopes and scopes[-1][0] == closed.group(1):
                scopes.pop()

    REPORT.append(f"Binding modes: {checked} TwoWay-by-default binding(s) checked against their "
                  f"source property, {problems} problem(s)")


# ---------------------------------------------------------------------------
def check_crash_report_honesty() -> None:
    """The failure screen may only print a crash report written by the run that just failed.

    Why: on 23 Oct 2026 the launcher stopped and printed

        Report: C:\ProgramData\Area11\logs\crash-2026-10-22_174207.txt
        Version  : 0.29.0 (Burgundy)

    - a report written the day before, by a different version, for a fault that had already
    been repaired. The owner was about to send a day-old file to explain a brand-new failure,
    which is worse than an empty screen: it aims the repair at the wrong fault and hides the
    real one. The report was the newest on the PC simply because **this run wrote none** (the
    program wrote it under `%TEMP%\Area11` when the shop folder was not writable).

    So the script must carry three things, and this check insists on all of them:
      * a marker written **before** the program starts, so "newer than the marker" means "from
        this run";
      * both places searched, because the crash writer falls back to `%TEMP%\Area11`;
      * an honest branch for "no new report", which names the older file but **never prints it**
        - no `type` of an old report anywhere in that path.
    """
    launcher = ROOT / "START-AREA11.bat"

    if not launcher.exists():
        fail("START-AREA11.bat is missing")
        return

    text = launcher.read_text(encoding="utf-8", errors="replace")

    marker_at = text.find('set "RUNMARKER=')
    exe_at = text.find("\"%APPEXE%\"")

    if marker_at < 0:
        fail("START-AREA11.bat has no run marker - it cannot tell this run's crash report from "
             "an old one")

    elif exe_at >= 0 and marker_at > exe_at:
        fail("START-AREA11.bat writes its run marker AFTER starting the program - a crash in the "
             "first second would look older than the marker and be hidden")

    core_crash = ROOT / "dotnet/Area11.Core/Utils/CrashLog.cs"

    if not core_crash.exists() or "latest-crash.txt" not in core_crash.read_text(encoding="utf-8"):
        fail("CrashLog no longer records where its newest report is - the launcher would have to "
             "guess the folder again (a shared folder for the second counter was the wrong guess)")

    if "latest-crash.txt" not in text:
        fail("START-AREA11.bat does not read the program's own pointer (latest-crash.txt) - it "
             "would guess the folder, and on 23 Oct 2026 it guessed wrong")

    if "%TEMP%\\Area11" not in text:
        fail("START-AREA11.bat only looks in the shop folder for a crash report - the program "
             "falls back to %TEMP%\\Area11 when it cannot write there, and that report would be "
             "invisible")

    if "LastWriteTimeUtc -gt $m" not in text:
        fail("START-AREA11.bat does not compare the report's time with the run marker - it would "
             "print any old report as if it were this run's")

    if "This run wrote NO crash report of its own" not in text:
        fail("START-AREA11.bat has no honest branch for a run that wrote no report - it would "
             "show an older file instead of saying so")

    older = text[text.find(":olderreport"):] if ":olderreport" in text else ""

    if ":olderreport" not in text:
        fail("START-AREA11.bat does not name the older report and its date when this run wrote none")
    elif re.search(r'type\s+"?%OLD', older):
        fail("START-AREA11.bat prints an OLD crash report - the owner would send a file from "
             "another day to explain today's fault")

    REPORT.append("Crash report: only a report written after this run's marker is printed, both "
                  "folders are searched, and an older report is named but never shown")


# ---------------------------------------------------------------------------
def check_report_tool() -> None:
    """The tool that collects the evidence must not become part of the fault.

    Why: on 23 Oct 2026 the owner ran `tools\\SEND-REPORT.cmd` and the console printed

        Test-Path : Cannot bind argument to parameter 'LiteralPath' because it is null.
        + if (Test-Path -LiteralPath $logsFolder)

    in the middle of a report that otherwise worked. v0.31.1 had moved the crash section and taken
    its local `$logsFolder` with it, so the log section further down asked about `$null`. Two
    lessons, both gates:

      * **a holder is computed in one place.** Every `$variable` handed to a path parameter must be
        assigned earlier in the same script - a name that is never assigned is `$null`, and
        PowerShell only finds out at the call;
      * **a fault in the tool is invisible in the file.** The report said nothing about its own
        error, so the owner would have sent a document with a silent gap. The script now writes
        every error it raised into the report itself, and this check insists that section exists.

    It also keeps the desktop's silent-binding watch honest: a listener must be attached **before**
    `InitializeComponent`, or the theme's own bindings are not covered, and the crash report must
    name the file, or the owner will never know a second file exists.
    """
    script = ROOT / "tools/support-report.ps1"

    if not script.exists():
        fail("tools/support-report.ps1 is missing - the one-file report does not exist")
        return

    text = script.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()
    problems = 0

    for number, line in enumerate(lines, 1):
        for name in re.findall(r"-(?:LiteralPath|Path)\s+\$(\w+)", line):
            if name in ("PSCommandPath", "PSScriptRoot", "env", "true", "false", "null"):
                continue

            assigned = False

            for earlier in lines[:number - 1]:
                if re.search(r"\$" + re.escape(name) + r"\s*(=|\+=)", earlier) or \
                   re.search(r"param\s*\([^)]*\$" + re.escape(name) + r"\b", earlier) or \
                   re.search(r"^\s*function\s+[\w-]+\s*\([^)]*\$" + re.escape(name) + r"\b",
                             earlier, re.I) or \
                   re.search(r"foreach\s*\(\s*\$" + re.escape(name) + r"\b", earlier) or \
                   re.search(r"\bfor\s*\([^)]*\$" + re.escape(name) + r"\s*=", earlier):
                    assigned = True
                    break

            if not assigned:
                problems += 1
                fail(f"tools/support-report.ps1:{number}: $${name} is used as a path but is never "
                     f"assigned before this line - PowerShell passes $null and the console shows "
                     f"a red 'Cannot bind argument' (the 23 Oct report fault)")

    if "ERRORS RAISED BY THIS REPORT ITSELF" not in text:
        fail("tools/support-report.ps1 does not write its own errors into the report - a gap in "
             "the tool would read like a healthy machine")

    if '"ERRORS RAISED BY THIS REPORT ITSELF"' not in text and "Section \"ERRORS RAISED BY THIS REPORT ITSELF\"" not in text:
        fail("the self-error section is not a section of the report")

    watch = DOTNET / "Area11.Core/Utils/BindingWatch.cs"
    program = DOTNET / "Area11.Desktop/Program.cs"

    if not watch.exists():
        fail("BindingWatch is missing - WPF's silent binding faults leave no trace for the owner")

    if not program.exists():
        fail("Area11.Desktop/Program.cs is missing")

    if watch.exists() and program.exists():
        program_text = program.read_text(encoding="utf-8", errors="replace")

        # Compare with the CALL that builds the window (`app.InitializeComponent()`), not the bare
        # name: the sentence explaining the rule mentions the method too, and a check that cannot
        # tell a comment from code fails on its own documentation.
        attach = program_text.find("BindingWatchInstall.Install();")
        draw = program_text.find("app.InitializeComponent();")

        if attach < 0:
            fail("Program.cs never installs the binding watch - a wrong property name on a page "
                 "would still leave an empty box and no evidence")

        elif draw >= 0 and attach > draw:
            fail("Program.cs installs the binding watch AFTER InitializeComponent - the theme's "
                 "own bindings would not be covered")

        crash = DOTNET / "Area11.Core/Utils/CrashLog.cs"

        if crash.exists() and "Bindings :" not in crash.read_text(encoding="utf-8", errors="replace"):
            fail("the crash report does not name the binding-errors file - the two are read "
                 "together and the owner would not know the second file exists")

    REPORT.append(f"Report tool: every $-path is assigned before use, the report carries its own "
                  f"errors ({problems} problem(s)), and the binding watch is installed before the "
                  f"first window")


# ---------------------------------------------------------------------------
def check_page_guard() -> None:
    """A page that fails must not take the till down with it (D-88).

    Why: every menu row builds its view-model from a constructor that reads the shop's database,
    inside a WPF property setter, on the UI thread. One failed read therefore used to land in the
    dispatcher while the shell was still being built - the same shape as the 22 Oct crash, but at
    sign-in, which would leave the shop with no counter at all. The fix is structural: every page is
    built through `PageGuard.Open`, which turns an exception into a card that names the page and
    says the rest of the shop still works.

    What this check insists on:
      * the unguarded call is gone (`= value?.Resolve()` is the exact line that used to run);
      * the guard is really used, for the menu page, Settings and the bell;
      * the card exists - the view, the view model, and the template the shell draws it through;
      * the card's own words promise that nothing else is affected and name the report file, because
        a failure message that does not say "the rest still works" makes the operator close the
        program and lose the day's billing.
    """
    shell = DOTNET / "Area11.Desktop/ViewModels/ShellViewModel.cs"
    guard = DOTNET / "Area11.Core/Utils/PageGuard.cs"
    card = DOTNET / "Area11.Desktop/ViewModels/PageFaultViewModel.cs"
    view = DOTNET / "Area11.Desktop/Views/PageFaultView.xaml"
    window = DOTNET / "Area11.Desktop/Views/ShellWindow.xaml"

    for needed in (shell, guard, card, view, window):
        if not needed.exists():
            fail(f"the page guard is half-built: {rel(needed)} is missing")
    if not all(path.exists() for path in (shell, guard, card, view, window)):
        return

    text = shell.read_text(encoding="utf-8", errors="replace")

    # `PageGuard.Open<SettingsViewModel>(...)` is the preferred form, because the compiler's own type
    # of `field ??= new(...)` is nullable and a nullable argument fails the method's `class`
    # constraint. The pattern therefore accepts an explicit type argument.
    guarded_page = re.compile(r"PageGuard\.Open(?:<[^>]+>)?\(")

    if not guarded_page.search(text):
        fail("ShellViewModel does not build pages through PageGuard - one failed page would still "
             "reach the dispatcher and close the window (the 22 Oct fault)")

    if re.search(r"=\s*value\??\.Resolve\(\)", text):
        fail("ShellViewModel calls Resolve() outside the guard - a page that throws while being "
             "built would escape into the UI thread")

    if len(guarded_page.findall(text)) < 3:
        fail("ShellViewModel guards fewer than three pages - the menu page, Settings and the bell "
             "are all built from the database and all able to fail")

    guard_text = guard.read_text(encoding="utf-8", errors="replace")

    if "catch (Exception exception)" not in guard_text:
        fail("PageGuard does not catch the exception it exists to catch")

    # The guard's own error path - the log line and the crash recorder - must not be narrowed to the
    # file errors somebody expected. A recorder that fails in an unpredicted way used to escape the
    # catch and become the second fault, which is the exact thing this class exists to prevent.
    if re.search(r"catch \(Exception (?:logFailure|recordFailure)\) when", guard_text):
        fail("PageGuard narrows its own error path to a list of file errors - a logger or recorder "
             "that fails in another way would then throw out of the guard and close the window")

    if "Nothing else is affected" not in guard_text:
        fail("PageGuard's message does not tell the operator the rest of the shop still works - "
             "he would close the program and lose the day's billing")

    if "SEND-REPORT" not in guard_text:
        fail("PageGuard's message does not say how to send the evidence")

    if "ReportPath" not in card.read_text(encoding="utf-8", errors="replace") and \
       "ReportLabel" not in card.read_text(encoding="utf-8", errors="replace"):
        fail("the failure card does not name the report file")

    window_text = window.read_text(encoding="utf-8", errors="replace")

    if 'DataType="{x:Type vm:PageFaultViewModel}"' not in window_text:
        fail("ShellWindow has no template for PageFaultViewModel - the card would open as an "
             "empty frame, which is exactly what it exists to prevent")

    tests = DOTNET / "Area11.Tests/PageGuardTests.cs"

    if not tests.exists():
        fail("PageGuard has no tests - the one method that must never throw is the one that needs "
             "them most")

    # ---- the other half: a page that was built and then failed while WPF drew it (D-89) ----
    app = DOTNET / "Area11.Desktop/App.xaml.cs"

    if not app.exists():
        fail("App.xaml.cs is missing - the drawing half of the page guard cannot be checked")

    if app.exists():
        app_text = app.read_text(encoding="utf-8", errors="replace")

        if "ShowDrawingFault(" not in app_text:
            fail("the dispatcher's handler never hands a drawing fault to the shell - the operator "
                 "would see a half-drawn page with no words on it, which is what D-89 exists to stop")

        # It has to be in the DISPATCHER handler, not the domain one: the domain exception cannot be
        # handled, so a card there would promise something the program cannot keep.
        dispatcher = re.search(r"void OnDispatcherException\(.*?\n    \}", app_text, re.S)

        if dispatcher is None or "ShowDrawingFault(" not in dispatcher.group(0):
            fail("ShowDrawingFault is not called from OnDispatcherException - a UI-thread fault "
                 "would keep its half-drawn page")

        if "public static PageFault Drawing(" not in guard_text:
            fail("PageGuard.Drawing is gone - nothing turns a fault raised while drawing into a card")

        if "ShowDrawingFault(" not in text:
            fail("ShellViewModel no longer offers ShowDrawingFault - the window frame is the only "
                 "thing that knows which page was on the screen")

        if "_currentPageTitle" not in text:
            fail("ShellViewModel does not remember which page is on the screen, so a drawing fault "
                 "could not name it")

        # The drawing fault must leave the same evidence a construction fault does: the log line
        # and the crash report. A card with no evidence behind it is a dead end for the engineer.
        drawing_call = re.search(r"PageGuard\.Drawing\((.*?)\);", text, re.S)

        if drawing_call is None:
            fail("ShellViewModel calls PageGuard.Drawing without the page's name, the log and the "
                 "crash recorder - the card would be the only trace of the fault")
        elif "_services.Log" not in drawing_call.group(1) or "RecordPageFault" not in drawing_call.group(1):
            fail("the drawing fault is not written to the shop's log and to the crash report - "
                 "a card with no evidence behind it cannot be sent to anybody")

        if "stopped while its screen was being drawn" not in guard_text:
            fail("the drawing fault does not say what happened - 'could not be opened' would send "
                 "the operator looking for a data fault that is not there")

    REPORT.append("Page guard: every page is built through the guard, and a failed page shows a "
                  "card that names it, keeps the shop usable and says where the report is - "
                  "including a page that failed while it was being drawn")


    # ------------------------------------------------------------------
def check_empty_page() -> None:
    """An empty page must say why it is empty, in the shop's own words (v0.33.0, D-89).

    Why: a blank grid is the most common thing a shopkeeper sees and the least useful. "Nothing
    here yet" and "nothing matches what you typed" call for two different actions, and one generic
    sentence for both is how a shopkeeper concludes the program lost his data.

    What this check insists on:
      * every page the shell can open says it is an `IEmptyPage`;
      * the page's hint is computed from its own list (`EmptyPage.When(...)`), never a stored flag;
      * the title in that call is the one the table answers for, so the card is never generic by
        accident;
      * a page with a search box passes `searched:`, because an empty search result is not an
        empty shop;
      * the frame draws the card, and both converters exist and treat "no hint" as "hide".
    """
    core = DOTNET / "Area11.Core/Utils/EmptyPage.cs"
    shell = DOTNET / "Area11.Desktop/ViewModels/ShellViewModel.cs"
    window = DOTNET / "Area11.Desktop/Views/ShellWindow.xaml"
    theme = DOTNET / "Area11.Desktop/Resources/Theme.xaml"
    converters = DOTNET / "Area11.Desktop/Converters/EmptyHintConverter.cs"
    tests = DOTNET / "Area11.Tests/EmptyPageTests.cs"

    for needed in (core, shell, window, theme, converters, tests):
        if not needed.exists():
            fail(f"the empty-page card is half-built: {rel(needed)} is missing")
    if not all(path.exists() for path in (core, shell, window, theme, converters, tests)):
        return

    core_text = core.read_text(encoding="utf-8", errors="replace")

    if "interface IEmptyPage" not in core_text or "EmptyHint? EmptyHint" not in core_text:
        fail("EmptyPage does not define IEmptyPage with an `EmptyHint? EmptyHint` property - the "
             "window would have nothing to bind to")

    # The table answers for the pages by the titles the shell writes on the menu rows.
    known = set(re.findall(r'\[\s*"([^"]+)"\s*\]\s*=', core_text))

    if len(known) < 25:
        fail(f"only {len(known)} page(s) have their own empty-page sentence - the rest would say "
             "something generic")

    generic = re.search(r'"No data[^"]*"', core_text)

    if generic:
        fail("the empty-page table carries a generic \"No data\" sentence, which is the sentence "
             "that makes the operator think the shop is empty")

    shell_text = shell.read_text(encoding="utf-8", errors="replace")

    # Every page the menu can open is built with `new SomethingViewModel(` inside the shell. Each one
    # must implement IEmptyPage, or its empty state is silent.
    built = set(re.findall(r"new ([A-Za-z]+ViewModel)\(", shell_text))

    # Pages built inside another page count too: Customers, Udhaar recovery and Suppliers are made
    # by the container in Partners, and an empty tab needs its sentence exactly as much as an empty
    # page does.
    for path in (DOTNET / "Area11.Desktop/ViewModels").glob("*ViewModel.cs"):
        built |= set(re.findall(r"new ([A-Za-z]+ViewModel)\(",
                                path.read_text(encoding="utf-8", errors="replace")))

    built = sorted(built)

    if len(built) < 20:
        fail(f"only {len(built)} page(s) found in the shell's menu - the check reads the wrong file?")

    missing = []

    for view_model in built:
        path = DOTNET / f"Area11.Desktop/ViewModels/{view_model}.cs"

        if not path.exists():
            fail(f"the shell builds {view_model}, which has no file - the menu names a page that "
                 "does not exist")

        text = path.read_text(encoding="utf-8", errors="replace")

        if "IEmptyPage" not in text:
            # A page may say, in writing, that it has no empty state to explain - a container whose
            # tabs carry their own sentences, or the fault card itself. The reason is mandatory and
            # has to be a real sentence: an exemption without a reason is how a page goes silent.
            said_none = re.search(r"// empty-page: (?:none|delegated) - (.{20,})", text)

            if said_none is None:
                missing.append(view_model)

    if missing:
        fail("these pages say nothing when they are empty: " + ", ".join(missing) +
             " - a blank grid with no sentence reads as lost data (or write "
             "`// empty-page: none - <why>` or `// empty-page: delegated - <why>` on the page)")

    problems = []

    for view_model in built:
        text = (DOTNET / f"Area11.Desktop/ViewModels/{view_model}.cs").read_text(
            encoding="utf-8", errors="replace")

        # The pages that wrote down why they have no empty state are already answered above.
        if re.search(r"// empty-page: (?:none|delegated) - (.{20,})", text):
            continue

        call = re.search(r"public EmptyHint\? EmptyHint =>\s*EmptyPage\.When\((.*?)\);", text, re.S)

        if call is None:
            problems.append(f"{view_model}: EmptyHint is not computed from EmptyPage.When(...)")
            continue

        arguments = call.group(1)

        title = re.search(r'"([^"]+)"', arguments)

        if title is None:
            problems.append(f"{view_model}: EmptyPage.When is called without a page title")
        elif title.group(1) not in known:
            problems.append(f"{view_model}: the title \"{title.group(1)}\" has no advice in "
                            f"EmptyPage.cs - the card would fall back to a generic sentence")

        # An empty search result is not an empty shop - but only where the search narrows THIS
        # page's own list. On the four pages below the visible list is the search's result, so an
        # empty one must say "nothing matches" rather than "nothing here yet". On the other pages
        # the box picks a record to act on (a bill to return, a medicine to write off), and saying
        # "nothing matches what you typed" about the page would be false - those pages do not pass
        # the flag, deliberately.
        search_narrows_the_page = {
            "PosViewModel",
            "ProductsViewModel",
            "SaleHistoryViewModel",
            "AuditViewModel",
        }

        if view_model in search_narrows_the_page and "searched:" not in arguments:
            problems.append(f"{view_model}: its search narrows the page's own list but it does not "
                            "pass `searched:` - an empty result would read as an empty shop")

        if "searched:" in arguments and re.search(r"searched:\s*(true|false)\b", arguments):
            problems.append(f"{view_model}: passes a literal to `searched:` - the flag has to come "
                            "from the page's own search box")

    if problems:
        fail("the empty-page sentences do not match the pages: " + "; ".join(problems))

    # And the other direction: a sentence nobody shows is a sentence nobody has read, and it will
    # describe a page that has since changed. Every line must be reachable from a page.
    used_titles = set()

    # Every view model, not only the ones the menu builds directly: Customers and Suppliers are
    # built inside the container page, and their sentences are just as real.
    for path in sorted((DOTNET / "Area11.Desktop/ViewModels").glob("*ViewModel.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        call = re.search(r"public EmptyHint\? EmptyHint =>\s*EmptyPage\.When\((.*?)\);", text, re.S)

        if call is not None:
            title = re.search(r'"([^"]+)"', call.group(1))

            if title is not None:
                used_titles.add(title.group(1))

    unused = sorted(known - used_titles)

    if unused:
        fail("these pages have an empty-page sentence that no page uses: " + ", ".join(unused) +
             " - a sentence nobody shows goes stale, and a page that is later given a list would "
             "fall back to the generic one")

    window_text = window.read_text(encoding="utf-8", errors="replace")

    for needle, reason in (
        ('Converter={StaticResource EmptyHintVis}', "the frame does not show or hide the card"),
        ('Converter={StaticResource EmptyHintText}', "the frame does not print the sentence"),
    ):
        if needle not in window_text:
            fail(f"ShellWindow: {reason} - {needle} is missing")

    theme_text = theme.read_text(encoding="utf-8", errors="replace")

    for key in ("EmptyHintConverter", "EmptyHintVisibilityConverter", "EmptyCard"):
        if key not in theme_text:
            fail(f"Theme.xaml no longer defines {key} - the card would not draw")

    converter_text = converters.read_text(encoding="utf-8", errors="replace")

    if "Visibility.Collapsed" not in converter_text:
        fail("the card's visibility converter never collapses - every page would show an empty "
             "banner")

    if "EmptyHint" not in shell_text or "IEmptyPage" not in shell_text:
        fail("the window frame does not ask the page why it is empty - the card would stay empty")

    REPORT.append("Empty pages: every page the menu can open says why it is empty in its own "
                  "words, a search that finds nothing says so, and the frame draws one card")


# ---------------------------------------------------------------------------
def check_audit_recovery() -> None:
    """Z-05 shows an earlier state without becoming a financial rollback engine.

    The black box may explain how to make a NEW audited correction, but a generic Undo on a completed
    sale/payment/return/PR would invent money or stock movements. Keep this page a reader: selected
    row -> complete snapshot -> existing correction screen. In particular, PR cancellation is not a
    customer repayment and must never grow a reversal-inflow button here.
    """
    model_path = DOTNET / "Area11.Core/Models/AuditModels.cs"
    vm_path = DOTNET / "Area11.Desktop/ViewModels/AuditViewModel.cs"
    view_path = DOTNET / "Area11.Desktop/Views/AuditView.xaml"

    missing = False

    for path in (model_path, vm_path, view_path):
        if not path.exists():
            fail(f"Z-05 audit recovery file is missing: {rel(path)}")
            missing = True

    if missing:
        return

    model = model_path.read_text(encoding="utf-8", errors="replace")
    vm = vm_path.read_text(encoding="utf-8", errors="replace")
    view = view_path.read_text(encoding="utf-8", errors="replace")

    snapshot = model.split("public sealed class AuditSnapshot", 1)[-1].split(
        "public sealed class AuditFilter", 1)[0]

    for needle in (
        "public static AuditSnapshot For(AuditRow row)",
        "EarlierRaw = row.OldValue",
        "RecordedRaw = row.NewValue",
        "CorrectionGuidance(row)",
        "never creates a payment",
        "does not reverse the original financial effect",
    ):
        if needle not in snapshot:
            fail(f"Z-05 AuditSnapshot lost its read-only evidence/safety rule: {needle}")

    for needle in ("Database", "SqliteConnection", "_db.", ".Execute(", ".Tx("):
        if needle in snapshot:
            fail(f"Z-05 AuditSnapshot must stay a pure reader, but contains {needle}")

    for needle in (
        "public AuditRow? Selected",
        "public AuditSnapshot? Snapshot",
        "_services.AuditHistory.ReadSnapshot(value.Id, _user)",
        "Selected = null;",
        "public bool HasSnapshot",
    ):
        if needle not in vm:
            fail(f"Z-05 selected-row snapshot is not safely wired: {needle} is missing")

    for command in ("Undo", "Restore", "Rollback", "Revert"):
        if re.search(rf"public\s+(?:RelayCommand|ICommand)\s+{command}Command\b", vm):
            fail(f"Z-05 Black box must not grow a universal {command}Command")
        if re.search(rf'Command="\{{Binding\s+{command}(?:Command)?[,\}}]', view):
            fail(f"Z-05 Black box view must not expose a {command} command")

    for needle in (
        'SelectedItem="{Binding Selected}"',
        'Visibility="{Binding HasSnapshot, Converter={StaticResource BoolToVis}}"',
        '{Binding Snapshot.EarlierText, Mode=OneWay}',
        '{Binding Snapshot.RecordedText, Mode=OneWay}',
        '{Binding Snapshot.DetailsText}',
        '{Binding Snapshot.CorrectionText}',
        "not a live query",
    ):
        if needle not in view:
            fail(f"Z-05 earlier-state view is incomplete: {needle} is missing")

    for line_number, line in enumerate(view.splitlines(), 1):
        if "Text=\"{Binding Snapshot." in line and "TextBox" in line and 'IsReadOnly="True"' not in line:
            fail(f"AuditView.xaml:{line_number}: snapshot text must be read-only")

    REPORT.append(
        "Audit recovery: selected row exposes complete read-only before/after values and safe "
        "correction guidance; no universal undo/restore command")


# ---------------------------------------------------------------------------
def check_report_chart() -> None:
    """T-05 is the existing native per-day chart, not a reason to add a web chart package.

    Recharts/TanStack belong to the reference web app. The desktop port uses WPF bars and grids.
    Keep the geometry separate from integer-paisa values, include quiet days, and never let the
    per-day profit value leak to a manager. This guard protects that existing implementation; the
    matrix must not claim that no chart exists merely because it does not use a JavaScript library.
    """
    model_path = DOTNET / "Area11.Core/Models/ReportModels.cs"
    service_path = DOTNET / "Area11.Core/Services/ReportService.cs"
    vm_path = DOTNET / "Area11.Desktop/ViewModels/ReportsViewModel.cs"
    view_path = DOTNET / "Area11.Desktop/Views/ReportsView.xaml"
    test_path = DOTNET / "Area11.Tests/ReportTests.cs"
    paths = (model_path, service_path, vm_path, view_path, test_path)
    missing = False

    for path in paths:
        if not path.exists():
            fail(f"T-05 report chart file is missing: {rel(path)}")
            missing = True

    if missing:
        return

    model = model_path.read_text(encoding="utf-8", errors="replace")
    service = service_path.read_text(encoding="utf-8", errors="replace")
    vm = vm_path.read_text(encoding="utf-8", errors="replace")
    view = view_path.read_text(encoding="utf-8", errors="replace")
    tests = test_path.read_text(encoding="utf-8", errors="replace")
    days = service.split("private List<ReportDayRow> ReadDays", 1)[-1].split(
        "private List<ReportItemRow> ReadTop", 1)[0]

    if "public double BarShare { get; set; }" not in model:
        fail("T-05 ReportDayRow no longer exposes the bar's 0..1 share")

    for needle in (
        "for (var day = first; day <= last; day = day.AddDays(1))",
        "var busiest = rows.Count == 0 ? 0 : rows.Max(row => row.SalesPaisa);",
        "row.BarShare = busiest <= 0 ? 0 : (double)row.SalesPaisa / busiest;",
        "ProfitPaisa = canSeeProfit ? salesPaisa - refundPaisa - (cost - restockCost) : 0",
    ):
        if needle not in days:
            fail(f"T-05 per-day chart lost a quiet-day/scaling/permission rule: {needle}")

    for needle in ("foreach (var day in value.Days)", "Days.Add(day);"):
        if needle not in vm:
            fail(f"T-05 report view-model no longer supplies the real day rows: {needle}")

    for needle in (
        '<ItemsControl ItemsSource="{Binding Days}">',
        "<ProgressBar",
        'Minimum="0" Maximum="1" Value="{Binding BarShare}"',
        'Text="{Binding DayLabel}"',
        'Text="{Binding SalesLabel}"',
        'Text="{Binding ProfitLabel}"',
        'ItemsSource="{Binding TopByQty}"',
        'ItemsSource="{Binding Categories}"',
    ):
        if needle not in view:
            fail(f"T-05 native chart/table wiring is missing: {needle}")

    for test_name in (
        "PerDay_HasEveryDayOfThePeriod_EvenTheQuietOnes",
        "PerDayProfit_AddsUpToTheTotalProfit",
        "PerDayChart_ScalesToTheBusiestDay_WithoutChangingTheMoney",
        "PerDayChart_AllQuietWeekKeepsSevenZeroBars",
        "PerDayChart_ManagerKeepsTheSalesBarButNotTheProfit",
    ):
        if test_name not in tests:
            fail(f"T-05 regression fact is missing: {test_name}")

    REPORT.append(
        "Report chart: native per-day bars keep quiet days, scale to the busiest day, preserve "
        "integer-paisa labels and hide profit without permission; named regression facts present")


# ---------------------------------------------------------------------------
def check_real_stock_projections() -> None:
    """An empty product and a stocked product must expose the same SQLite numeric type.

    IFNULL(SUM(REAL), 0) is integer zero when no batch exists and Double when a later product has
    stock. Dapper can cache the first row's shape, so the live product/reorder lists failed with a
    Double -> Int64 cast even though both C# properties were already double. Cast only the projected
    fallback to REAL: qty_base stays REAL, fractional quantities stay fractional, and money is not
    involved. The two facts keep the empty-first ordering that reproduces the owner's failure.
    """
    expression = (
        "CAST(IFNULL((SELECT SUM(b.qty_base) FROM batches b "
        "WHERE b.product_id = p.id AND b.active = 1), 0) AS REAL)"
    )
    targets = (
        (DOTNET / "Area11.Core/Services/ProductService.cs", "stock_base"),
        (DOTNET / "Area11.Core/Services/ReorderService.cs", "stockQty"),
    )

    for path, alias in targets:
        if not path.exists():
            fail(f"REAL stock projection file is missing: {rel(path)}")
            continue

        text = path.read_text(encoding="utf-8", errors="replace")
        if f"{expression} AS {alias}" not in text:
            fail(f"{rel(path)}: {alias} must CAST the empty/stocked SUM projection AS REAL")

    product = DOTNET / "Area11.Core/Models/Product.cs"
    if product.exists() and "public double StockBase { get; set; }" not in product.read_text(
            encoding="utf-8", errors="replace"):
        fail("Product.StockBase must remain double; do not repair SQLite materialization by truncation")

    reorder = targets[1][0]
    if reorder.exists() and "public double StockQty { get; set; }" not in reorder.read_text(
            encoding="utf-8", errors="replace"):
        fail("Reorder ProductRow.StockQty must remain double")

    for path, fact in (
        (DOTNET / "Area11.Tests/CatalogTests.cs", "ProductSearch_ReadsAnEmptyProductBeforeARealStockSum"),
        (DOTNET / "Area11.Tests/ReorderTests.cs", "ReorderReport_ReadsAnEmptyProductBeforeARealStockSum"),
    ):
        if not path.exists() or fact not in path.read_text(encoding="utf-8", errors="replace"):
            fail(f"Mixed empty/REAL stock regression fact is missing: {fact}")

    REPORT.append(
        "REAL stock projections: empty-first product and reorder reads stay double without casting "
        "qty_base or quantities to INTEGER; both runtime regression facts present")


# ---------------------------------------------------------------------------
def check_pr_reconciliation_parameters() -> None:
    """The PR allocation command must receive the exact names used in its SQL.

    Microsoft.Data.Sqlite rejected the owner's real run because anonymous-member shorthand exposed
    ProvisionalItemId/SaleItemId while the SQL requested @provisionalItemId/@saleItemId. Keep the
    bindings explicit; the existing service-level facts prove the resulting allocation behavior.
    """
    service_path = DOTNET / "Area11.Core/Services/ProvisionalReturnService.cs"
    test_paths = (
        DOTNET / "Area11.Tests/ProvisionalReconciliationTests.cs",
        DOTNET / "Area11.Tests/ProvisionalTests.cs",
    )

    if not service_path.exists() or any(not path.exists() for path in test_paths):
        fail("PR reconciliation parameter guard needs its service and behavioral test files")
        return

    service = service_path.read_text(encoding="utf-8", errors="replace")
    start = service.find("INSERT INTO provisional_return_allocations(")
    end = service.find("UPDATE provisional_returns", start + 1) if start >= 0 else -1

    if start < 0 or end < 0:
        fail("PR reconciliation allocation INSERT could not be isolated")
        return

    block = service[start:end]
    for needle in (
        "@provisionalItemId, @saleItemId, @qtyBase, @expected, @refund, @at",
        "provisionalItemId = line.ProvisionalItemId",
        "saleItemId = line.SaleItemId",
    ):
        if needle not in block:
            fail(f"PR allocation SQL and explicit parameter binding drifted apart: {needle}")

    if re.search(r"(?m)^\s*line\.(?:ProvisionalItemId|SaleItemId),\s*$", block):
        fail("PR allocation parameters must not use differently-cased anonymous-member shorthand")

    for path, fact in (
        (test_paths[0], "ExactLink_ConsumesQuantityAndValue_WithoutAnotherStockOrPaymentEvent"),
        (test_paths[1], "AttachingTheBill_DoesNotAddTheStockTwice"),
    ):
        if fact not in path.read_text(encoding="utf-8", errors="replace"):
            fail(f"PR allocation behavioral regression is missing: {fact}")

    REPORT.append(
        "PR reconciliation: allocation SQL receives exact provisional/sale-item parameter names; "
        "existing facts retain stock/payment behavior")


# ---------------------------------------------------------------------------
def check_cloud_sync_closure() -> None:
    """A 90-day cloud document must not cut off a recent row's financial parent.

    Recent payments may belong to older sales/purchases, and migration 024 lets them reference a
    provisional return. A no-bill reconciliation may in turn reference an old sale line. Keep those
    dependencies and every selected document's lines in one shared document reader; never drop a link merely
    to make the insert fit. Apply also needs its own switch/format/null-id guards because it is Core,
    not just a button. The named xUnit facts are the behavioral evidence; these are source guards.
    """
    service_path = DOTNET / "Area11.Core/Services/SyncService.cs"
    models_path = DOTNET / "Area11.Core/Models/SyncModels.cs"
    view_model_path = DOTNET / "Area11.Desktop/ViewModels/CloudSyncViewModel.cs"
    tests_path = DOTNET / "Area11.Tests/SyncTests.cs"
    paths = (service_path, models_path, view_model_path, tests_path)

    if any(not path.exists() for path in paths):
        for path in paths:
            if not path.exists():
                fail(f"Cloud sync dependency guard file is missing: {rel(path)}")
        return

    service = service_path.read_text(encoding="utf-8", errors="replace")
    models = models_path.read_text(encoding="utf-8", errors="replace")
    view_model = view_model_path.read_text(encoding="utf-8", errors="replace")
    tests = tests_path.read_text(encoding="utf-8", errors="replace")

    order_match = re.search(r"public static readonly string\[\] Order\s*=\s*\{(.*?)\};", models,
                            re.DOTALL)
    if order_match is None:
        fail("Cloud sync table order is missing")
    else:
        order = re.findall(r'"([^"]+)"', order_match.group(1))
        for parent, child in (
            ("sales", "sale_items"),
            ("purchases", "purchase_items"),
            ("provisional_returns", "provisional_items"),
            ("provisional_items", "provisional_return_allocations"),
            ("sale_items", "provisional_return_allocations"),
            ("provisional_returns", "payments"),
        ):
            if parent not in order or child not in order or order.index(parent) >= order.index(child):
                fail(f"Cloud sync must write {parent} before {child}")

    format_match = re.search(r"CurrentFormat\s*=\s*(\d+)\s*;", models)
    if format_match is None or int(format_match.group(1)) < 2:
        fail("Cloud sync format must be 2 or newer so older builds refuse dependency-complete documents")

    # Status, Build and Preview counts all use the same reader, not a second set of COUNT queries.
    if service.count("var envelope = ReadLocal();") < 3 or "CountRows(" in service:
        fail("Cloud sync page counts and push no longer share the dependency-complete document reader")

    for pattern in (
        r'payment,\s*"sale_id",\s*"sales"',
        r'payment,\s*"purchase_id",\s*"purchases"',
        r'payment,\s*\r?\n?\s*"provisional_return_id",\s*"provisional_returns"',
        r'provisional,\s*"linked_sale_id",\s*"sales"',
        r'allocation,\s*\r?\n?\s*"provisional_item_id",\s*"provisional_items"',
        r'allocation,\s*\r?\n?\s*"sale_item_id",\s*"sale_items"',
        r'line,\s*"sale_id",\s*"sales"',
    ):
        if not re.search(pattern, service):
            fail(f"Cloud sync dependency edge is missing: {pattern}")

    for query in (
        "SELECT * FROM sale_items WHERE sale_id IN (SELECT id FROM sales WHERE date >= @since)",
        "SELECT * FROM purchase_items WHERE purchase_id IN (SELECT id FROM purchases WHERE date >= @since)",
        "SELECT * FROM provisional_items WHERE provisional_id IN (SELECT id FROM provisional_returns WHERE date >= @since)",
        "item.provisional_id IN (SELECT id FROM provisional_returns WHERE date >= @since)",
        "SELECT * FROM sale_items WHERE sale_id = @parent ORDER BY id",
        "SELECT * FROM purchase_items WHERE purchase_id = @parent ORDER BY id",
        "SELECT * FROM provisional_items WHERE provisional_id = @parent ORDER BY id",
        "FROM provisional_return_allocations allocation",
        "WHERE item.provisional_id = @parent",
    ):
        if query not in service:
            fail(f"Cloud sync complete-child query is missing: {query}")

    for table in ("sales", "purchases", "provisional_returns"):
        needle = f'new HashSet<long>(ids["{table}"])'
        required = 2 if table == "provisional_returns" else 1  # lines and allocations
        if service.count(needle) < required:
            fail(f"Cloud sync must mark bulk-loaded {table} lines complete to avoid N+1 recent reads")

    apply_match = re.search(r"public ServiceResult<SyncApplyResult> Apply\(.*?\n    \}(?=\n\n    // =+ the write)",
                            service, re.DOTALL)
    if apply_match is None:
        fail("Cloud sync Apply boundary could not be isolated for its safety guards")
    else:
        apply = apply_match.group(0)
        backup = apply.find("_backup.Create(")
        enabled = apply.find("if (!Enabled)")
        future = apply.find("envelope.Format > SyncEnvelope.CurrentFormat")
        if backup < 0 or enabled < 0 or enabled > backup:
            fail("Cloud sync Apply must refuse a switched-off shop before taking a backup or writing")
        if backup < 0 or future < 0 or future > backup:
            fail("Cloud sync Apply must reject a future format before taking a backup or writing")

    if "id is null" not in service or "!allowed.Contains(\"id\")" not in service:
        fail("Cloud sync upsert must skip a missing/NULL id instead of letting SQLite auto-number it")

    refresh = re.search(r"private void Refresh\(\).*?\n    \}", view_model, re.DOTALL)
    push = re.search(r"private void Push\(\).*?\n    \}", view_model, re.DOTALL)
    if refresh is None or "ClearPreview();" not in refresh.group(0):
        fail("Cloud sync Refresh must invalidate an old applicable preview")
    if push is None or "ClearPreview();" not in push.group(0):
        fail("Cloud sync Push must invalidate the previously previewed cloud document")

    for fact in (
        "ARecentCollectionBringsItsOlderSaleAndAllTheSaleLines",
        "ARecentSupplierPaymentBringsItsOlderPurchaseAndLines",
        "ARecentProvisionalPaymentBringsTheWholeReconciliationChain",
        "SwitchingCloudOffAfterPreviewAlsoClosesTheCoreWriteDoor",
        "AFutureFormatCannotBypassPreviewAndReachTheWriter",
        "ANullCloudIdIsSkippedInsteadOfBecomingANewLocalRow",
        "AnOlderFormatOneDocumentCanStillBeReadAndApplied",
    ):
        if fact not in tests:
            fail(f"Cloud sync regression fact is missing: {fact}")

    REPORT.append(
        "Cloud sync: shared 90-day/dependency-complete document reader, parent-before-child writes, "
        "format-1 compatibility and future-format refusal, switch/null-id guards, stale-preview "
        "invalidation and seven named regression facts")


# ---------------------------------------------------------------------------
def check_native_workbook() -> None:
    """T-10 needs real Open XML and a safe bulk-update workflow, not a CSV renamed to .xlsx.

    Keep this dependency-free (D-53's build-risk concern), retain CSV for the workflows that use it,
    and make the all-data workbook safe: empty is no change, __CLEAR__ is explicit, stock/balances and
    secrets stay locked, Settings needs its own tick, and every old value is checked again before the
    single audited transaction. The tests exercise both our writer and ordinary Excel cell encodings.
    """
    codec_path = DOTNET / "Area11.Core/Models/XlsxWorkbook.cs"
    model_path = DOTNET / "Area11.Core/Models/WorkbookModels.cs"
    service_path = DOTNET / "Area11.Core/Services/WorkbookService.cs"
    import_path = DOTNET / "Area11.Core/Services/ImportService.cs"
    vm_path = DOTNET / "Area11.Desktop/ViewModels/ImportExportViewModel.cs"
    view_path = DOTNET / "Area11.Desktop/Views/ImportExportView.xaml"
    test_path = DOTNET / "Area11.Tests/WorkbookTests.cs"
    paths = (codec_path, model_path, service_path, import_path, vm_path, view_path, test_path)
    missing = False

    for path in paths:
        if not path.exists():
            fail(f"T-10 native workbook file is missing: {rel(path)}")
            missing = True

    if missing:
        return

    codec = codec_path.read_text(encoding="utf-8", errors="replace")
    service = service_path.read_text(encoding="utf-8", errors="replace")
    imports = import_path.read_text(encoding="utf-8", errors="replace")
    vm = vm_path.read_text(encoding="utf-8", errors="replace")
    view = view_path.read_text(encoding="utf-8", errors="replace")
    tests = test_path.read_text(encoding="utf-8", errors="replace")

    for needle in (
        "public static byte[] Write(XlsxWorkbook workbook)",
        "public static XlsxWorkbook Read(byte[] bytes)",
        'writer.WriteAttributeString("t", "inlineStr")',
        "DtdProcessing = DtdProcessing.Prohibit",
        "xl/sharedStrings.xml",
        "DateTime.FromOADate(serial)",
        "MaxTotalBytes = 64 * 1024 * 1024",
    ):
        if needle not in codec:
            fail(f"T-10 Open XML codec lost a format/safety rule: {needle}")

    for needle in (
        'private const string ClearToken = "__CLEAR__";',
        "if (text.Length == 0)",
        "if (text == ClearToken)",
        "VerifyCurrentRow(connection, record);",
        "BulkDataAccess.Require(_db, connection, _permissions, _settings, user, mixed: true);",
        "Snapshot = currentFingerprint",
        "ReadRows(connection, schema, record.KeyValue)",
        "Fingerprint(schema, current)",
        "FROM product_barcodes",
        "ValidateAcrossRows(connection, preview);",
        "_db.Tx(connection =>",
        "_audit.RecordChange(connection, user",
        "_settings.PrepareBulk",
        'Col("Quantity", "qty_base", CellKind.Quantity, editable: false',
        'Col("Stock on shelf", "stock_base", CellKind.Quantity, editable: false',
        'Col("Udhaar now", "balance_paisa", CellKind.Money, editable: false',
        'Col("Payable now", "balance_paisa", CellKind.Money, editable: false',
        'key.StartsWith("security.", StringComparison.Ordinal)',
        'key.StartsWith("bill.next", StringComparison.Ordinal)',
        'Name = "Products", Table = "products"',
        'Name = "Customers", Table = "customers"',
        'Name = "Suppliers", Table = "suppliers"',
        'Name = "Categories", Table = "categories"',
        'Name = "Companies", Table = "companies"',
        'Name = "Batches", Table = "batches"',
        'Name = "Settings"',
        'Name = "Instructions"',
    ):
        if needle not in service:
            fail(f"T-10 workbook preview/apply protection is missing: {needle}")

    if "PreviewWorkbook(User? user, string kind, byte[] bytes)" not in imports:
        fail("T-10 new-record import no longer accepts the matching native worksheet")

    for needle in (
        "_services.Import.PreviewWorkbook(_user, Kind, bytes)",
        "WorkbookService.Template()",
        "_services.Workbooks.ExportAll(_user)",
        "_services.Workbooks.PreviewBulk(_user, bytes, IncludeSettings)",
        "_services.Workbooks.ApplyBulk(_user, _bulkPreview)",
    ):
        if needle not in vm:
            fail(f"T-10 desktop workbook workflow is disconnected: {needle}")

    for needle in (
        'Command="{Binding DownloadAllCommand}"',
        'Command="{Binding ChooseBulkFileCommand}"',
        'Command="{Binding ApplyBulkCommand}"',
        'ItemsSource="{Binding BulkRows}"',
        'IsChecked="{Binding IncludeSettings, Mode=TwoWay}"',
    ):
        if needle not in view:
            fail(f"T-10 workbook page binding is missing: {needle}")

    for test_name in (
        "XlsxRoundTrip_KeepsUnicodeLeadingZerosAndFormulaText",
        "Reader_UnderstandsSharedStringsAndRealExcelDateCells",
        "Template_HasFourSheets_AndWorkbookImportUsesTheMatchingOne",
        "NativeListExport_IsRealXlsxWithTheSameRupeeValues",
        "AllDataDownload_HasEightSheetsWithoutPhotosSecretsOrCounters",
        "BulkApply_ShowsBeforeAfter_ClearsAndAuditsWithoutTouchingStock",
        "BulkUpdate_UploadingTheAppliedFileAgainMeansZeroChanges",
        "BulkUpdate_RefusesStockAndAccountBalanceChanges",
        "BulkUpdate_SettingsNeedTheTick_AndKeepStringNumberAndArrayTypes",
        "BulkUpdate_LockedSettingsCannotBeAddedByHand",
        "BulkUpdate_ImportAndSettingsAloneCannotExposeMixedFinancialWorkbook",
        "BulkApply_StaleLaterRowRollsBackTheEarlierValidRow",
        "BulkUpdate_BatchExpiryAlsoUpdatesItsMonth_NotItsQuantity",
        "BulkUpdate_CategoryCycleIsRefusedBeforeAnythingIsWritten",
    ):
        if test_name not in tests:
            fail(f"T-10 workbook regression fact is missing: {test_name}")

    reader_test = tests.split("Reader_UnderstandsSharedStringsAndRealExcelDateCells", 1)[-1].split(
        "    [Fact]", 1)[0]
    if "new MemoryStream(bytes);" in reader_test:
        fail("T-10 ZIP mutation fixture must not use a fixed-size MemoryStream(byte[])")
    for needle in (
        "new MemoryStream(bytes.Length + 4096)",
        "output.Write(bytes, 0, bytes.Length);",
        "output.Position = 0;",
    ):
        if needle not in reader_test:
            fail(f"T-10 expandable ZIP mutation fixture is incomplete: {needle}")

    protected_test = tests.split("BulkUpdate_RefusesStockAndAccountBalanceChanges", 1)[-1].split(
        "    [Fact]", 1)[0]
    if "+ productId + \", 'PACK-999', 'box', 1);\"" not in protected_test \
            or "+ otherId + \", 'PACK-999', 'box', 1);\"" in protected_test:
        fail("T-10 active-pack fixture must put the pack and edited base barcode on DIFFERENT products")
    if "Assert.Equal(0, preview.ChangeCount);" not in protected_test:
        fail("T-10 protected-field test must prove that no invalid edit became a ready change")

    for project in DOTNET.rglob("*.csproj"):
        packages = project.read_text(encoding="utf-8", errors="replace")
        if any(name in packages for name in ("ClosedXML", "EPPlus", "DocumentFormat.OpenXml")):
            fail(f"T-10 added a spreadsheet package despite the dependency-free codec: {rel(project)}")

    REPORT.append(
        "Native workbook: dependency-free Open XML read/write, eight-sheet data/template workflows, "
        "protected fields, Settings types/tick, stale-row checks and one audited transaction; 14 facts present")


# ---------------------------------------------------------------------------
def check_backup_restore_gate() -> None:
    """Keep the live SQLite family intact when a staged restore cannot replace it.

    The main database, -wal and -journal are one logical state. Deleting the companions before a
    successful same-volume swap can lose committed work even when the restore reports that nothing
    was replaced. The public-service facts also retain A/B/C recovery, damaged-input refusal and
    automatic rollback after a photo fault; the Windows fact supplies a real file-sharing lock.
    """
    service_path = DOTNET / "Area11.Core/Services/BackupService.cs"
    tests_path = DOTNET / "Area11.Tests/BackupTests.cs"

    if not service_path.exists() or not tests_path.exists():
        fail("Backup restore safety guard requires BackupService.cs and BackupTests.cs")
        return

    service = service_path.read_text(encoding="utf-8", errors="replace")
    tests = tests_path.read_text(encoding="utf-8", errors="replace")
    replacement = re.search(
        r"    private void ReplaceDatabase\(string sourcePath\)(.*?)(?=\n    private bool TryRollback)",
        service, re.DOTALL)

    if replacement is None:
        fail("Backup restore safety guard could not isolate ReplaceDatabase")
    else:
        method = replacement.group(0)
        move = method.find("File.Move(staged, _db.FilePath, overwrite: true);")
        sidecars = [match.start() for match in re.finditer(
            r"RemoveSidecars\(_db\.FilePath\);", method)]

        if move < 0 or "replaced = true;" not in method[move:]:
            fail("Backup database replacement must record success only after the same-volume move")
        if len(sidecars) != 1 or (sidecars and sidecars[0] < move):
            fail("Backup restore must not remove live SQLite sidecars before a successful replacement")
        if not re.search(
                r"finally\s*\{.*?TryDelete\(staged\);.*?if \(replaced\)\s*"
                r"\{\s*RemoveSidecars\(_db\.FilePath\);\s*\}", method, re.DOTALL):
            fail("Backup restore sidecar cleanup must be conditional on a successful replacement")

    restore = re.search(
        r"    public ServiceResult<RestoreResult> Restore\(.*?(?=\n    /// <summary>Same-volume)",
        service, re.DOTALL)
    if restore is None:
        fail("Backup restore safety guard could not isolate the public Restore method")
    else:
        body = restore.group(0)
        safety = body.find("var safety = CreateCore(user, includePhotos: true")
        replacement_call = body.find("ReplaceDatabase(unpacked);")
        rollback = body.find("TryRollback(safetyPath, databaseReplaced, photosTouched, previousPhotos, recoveryPassword, stage)")
        if safety < 0 or replacement_call < 0 or safety > replacement_call:
            fail("Backup restore must make a full safety copy before replacing the live database")
        if rollback < 0:
            fail("Backup restore failure path must retain the automatic recovery attempt")

    for fact in (
        "Restore_ABCSequence_NewBackupContainsCAndSafetyRetainsB",
        "Restore_DamagedZip_LeavesTheLiveShopAlone",
        "Restore_WhenPhotoCopyFails_RollsBackTheDatabaseAndPreviousPhotos",
        "Restore_WhenTheLiveDatabaseIsLocked_PreservesItsSidecarsAndData",
    ):
        if re.search(rf"public void {re.escape(fact)}\(\)", tests) is None:
            fail(f"Backup restore regression fact is missing: {fact}")

    REPORT.append(
        "Backup restore: full pre-swap recovery, sidecars preserved until successful replacement, "
        "rollback retained, and A/B/C + damaged-zip + photo-fault + Windows-lock facts present")


# ---------------------------------------------------------------------------
def check_shop_clock() -> None:
    """One time for the shop, a session that ends by itself, and no bill without a drawer (D-92).

    Why a check and not a comment: these three are the only rules of v0.35.0 that cost money. A
    `Clock` that quietly goes back to `DateTime.Now`, a 12-hour limit that becomes a setting, or a
    bill that slips through with the drawer closed would each look perfect on the screen and be
    wrong in the books - so the shape of the code is checked here, on every run, on every machine.
    """
    core = DOTNET / "Area11.Core/Utils"
    source = core / "TimeSource.cs"
    ntp = core / "NtpTime.cs"
    policy = core / "SessionPolicy.cs"
    watch = core / "SessionWatch.cs"
    handover = core / "SessionHandover.cs"
    paths = core / "AppPaths.cs"
    settings = DOTNET / "Area11.Core/Services/SettingsService.cs"
    sales = DOTNET / "Area11.Core/Services/SaleService.cs"
    pos = DOTNET / "Area11.Desktop/ViewModels/PosViewModel.cs"
    pos_view = DOTNET / "Area11.Desktop/Views/PosView.xaml"
    shell = DOTNET / "Area11.Desktop/ViewModels/ShellViewModel.cs"
    app = DOTNET / "Area11.Desktop/App.xaml.cs"
    session_tests = DOTNET / "Area11.Tests/SessionTests.cs"
    clock_tests = DOTNET / "Area11.Tests/TimeSourceTests.cs"

    needed = (source, ntp, policy, watch, handover, paths, settings, sales, pos, pos_view, shell, app,
              session_tests, clock_tests)

    for path in needed:
        if not path.exists():
            fail(f"the shop's clock is half-built: {rel(path)} is missing")

    if not all(path.exists() for path in needed):
        return

    paths_text = paths.read_text(encoding="utf-8", errors="replace")

    if "TimeSource.Now" not in paths_text:
        fail("Clock does not read TimeSource - a bill would be stamped with the computer clock even "
             "when a time server answered")

    # The four methods of Clock, and no fifth opinion about the time.
    clock_block = paths_text[paths_text.find("public static class Clock"):]
    if "DateTime.Now" in clock_block.split("DateDaysAgo")[-1] or "DateTime.Now" in clock_block.split("public static string Now")[0]:
        fail("Clock still contains DateTime.Now - one call site left behind is a stamp from the wrong clock")

    ntp_text = ntp.read_text(encoding="utf-8", errors="replace")

    if ": 123" not in ntp_text and ", 123" not in ntp_text:
        fail("NtpTime does not talk to port 123 - that is the only port a time server answers on")

    source_text = source.read_text(encoding="utf-8", errors="replace")

    if "LargestAcceptedOffset" not in source_text or "FromDays(30)" not in source_text:
        fail("TimeSource lost the thirty-day garbage bound - one wrong server would move every "
             "bill in the shop (the two-day refusal is gone on the owner's word, Nov 2026, but "
             "a garbage answer must still be refused)")

    if "IsPlausible" not in source_text:
        fail("TimeSource lost the pure plausibility question - the tests could no longer ask about "
             "a three-day drift without moving every stamp in the program")

    policy_text = policy.read_text(encoding="utf-8", errors="replace")

    if "HardCapHours = 12" not in policy_text:
        fail("SessionPolicy lost the 12-hour ceiling - the owner's rule is 'in every case', not a setting")

    if "HardCapHours = 12" in policy_text and "AllowedIdleHours" in policy_text:
        choice_line = [line for line in policy_text.splitlines() if "AllowedIdleHours" in line and "{" in line]
        if not choice_line:
            fail("SessionPolicy does not list the sign-out choices - the Settings page and the service "
                 "must agree about which numbers are allowed")

    settings_text = settings.read_text(encoding="utf-8", errors="replace")

    if '"session.idleHours"' not in settings_text:
        fail("the sign-out time is not a stored setting - it would reset on every restart")

    if "session.handoverAt" not in settings_text:
        fail("the handover note has no setting behind it - a forgotten drawer could not be remembered")

    sales_text = sales.read_text(encoding="utf-8", errors="replace")

    if "HasOpenDrawer()" not in sales_text:
        fail("SaleService does not ask whether a drawer is open - a bill could be saved with the "
             "galla closed")

    if "status = 'open'" not in sales_text:
        fail("SaleService does not read the open shift - the drawer gate cannot be answered")

    # The bill-time fix (v0.68.0, owner's word Nov 2026): the counter records the drift on every
    # bill, the owner fixes the wrongly-stamped ones together in one action, and the paper and the
    # black box keep both times. Each half is pinned - a fix that forgets the money rows, the audit
    # lines or the paper would look perfect on the screen and be wrong in the books.
    if "time_offset_seconds" not in sales_text or "TimeSource.Offset" not in sales_text:
        fail("SaleService no longer records the drift on the bill - the fix could not know which "
             "bills were stamped with the wrong clock")

    fix = DOTNET / "Area11.Core/Services/TimeCorrectionService.cs"
    receipts = DOTNET / "Area11.Core/Services/ReceiptService.cs"

    if not fix.exists():
        fail("TimeCorrectionService.cs is missing - the wrongly-stamped bills could not be fixed together")
    else:
        fix_text = fix.read_text(encoding="utf-8", errors="replace")

        if "OwnerAccess.Require(_db, connection, user);" not in fix_text or "Only the owner can fix the time on bills" not in fix_text:
            fail("TimeCorrectionService lost the owner gate - a cashier could move the day's حساب")

        if '"sale_time_fixed"' not in fix_text or "oldValue" not in fix_text or "newValue" not in fix_text:
            fail("TimeCorrectionService does not write both times into the black box - a moved bill "
                 "could never be traced back to its wrong stamp")

        if "UPDATE payments SET date" not in fix_text:
            fail("TimeCorrectionService leaves the money rows on the wrong day - the day's collection "
                 "and the day's sales would sit in different days")

        if "_db.Tx(" not in fix_text:
            fail("TimeCorrectionService does not fix the range in one transaction - a half-fixed range "
                 "would be worse than none")

    if receipts.exists():
        receipts_text = receipts.read_text(encoding="utf-8", errors="replace")

        if "First stamped" not in receipts_text or "Correct time" not in receipts_text:
            fail("ReceiptService no longer prints both times on a corrected bill - the wrong stamp "
                 "would be rewritten out of history")

        if "big: true" not in receipts_text and "doubleHeight: true" not in receipts_text:
            fail("ReceiptService no longer prints the correct time BIG - the customer would read the "
                 "small wrong stamp as the day of his bill")

    pos_text = pos.read_text(encoding="utf-8", errors="replace")

    if "IsBlocked" not in pos_text:
        fail("the counter does not know it is blocked - the card would never appear")

    if "OpenShiftCommand" not in pos_text:
        fail("the counter cannot walk to the shift page - the owner asked for the way out to be on "
             "that same screen")

    pos_view_text = pos_view.read_text(encoding="utf-8", errors="replace")

    if "IsBlocked" not in pos_view_text:
        fail("PosView does not show the drawer card - the rule would be invisible until the bill failed")

    shell_text = shell.read_text(encoding="utf-8", errors="replace")

    if "SessionWatch.Start()" not in shell_text:
        fail("the shell never starts the session clock - nobody would ever be signed out")

    if "SessionExpired" not in shell_text:
        fail("the shell cannot end a session - the idle limit would only draw a warning")

    app_text = app.read_text(encoding="utf-8", errors="replace")

    if "StartBackgroundSync" not in app_text:
        fail("the program never asks a time server - the shop's time would be the computer's alone")

    REPORT.append("Shop clock: online time with the computer as fallback, a 12-hour ceiling that is "
                  "not a setting, and no bill without an open drawer")


def check_checklist() -> None:
    """The owner's printed tick-list must say exactly what the board says (v0.34.1, D-91).

    Why: the owner asked for one page he can hold in his hand and tick with a pen. A list written by
    hand would be right on the day it was written and wrong from the next round on - and a wrong list
    is worse than no list, because he would stop asking and start trusting it. So the list is
    **generated** from `docs/PROGRESS.md` by `tools/make_checklist.py`, and this gate refuses a
    commit where feature membership disagrees. Legacy source-only ticks map to ongoing until current evidence exists.
    """
    board = ROOT / "docs" / "PROGRESS.md"
    markdown = ROOT / "docs" / "CHECKLIST-URDU.md"
    printable = ROOT / "docs" / "CHECKLIST-URDU.html"
    maker = ROOT / "tools" / "make_checklist.py"
    launcher = ROOT / "PRINT-CHECKLIST.cmd"

    for needed in (board, markdown, printable, maker, launcher):
        if not needed.exists():
            fail(f"the owner's tick-list is half-built: {rel(needed)} is missing")
    if not all(path.exists() for path in (board, markdown, printable, maker, launcher)):
        return

    def states(text: str, pattern: str) -> "dict[str, str]":
        found: dict[str, str] = {}
        for line in text.splitlines():
            match = re.search(pattern, line)
            if match is not None:
                found[match.group(1)] = match.group(2)
        return found

    wanted = states(board.read_text(encoding="utf-8"), r"^\| (F-\d+) \| .+? \| (✅|🟡|⬜) \|")
    listed = states(markdown.read_text(encoding="utf-8"),
                    r"^\| ☐ \| (F-\d+) \| .+ \| (✅|⏳|❌) ")

    if not wanted:
        fail("docs/PROGRESS.md has no feature rows - the tick-list has nothing to be built from")
        return

    missing = sorted(set(wanted) - set(listed))
    extra = sorted(set(listed) - set(wanted))

    if missing:
        fail(f"docs/CHECKLIST-URDU.md is missing {len(missing)} feature(s) the board has: "
             + ", ".join(missing[:6]))
    if extra:
        fail(f"docs/CHECKLIST-URDU.md lists {len(extra)} feature(s) the board does not have: "
             + ", ".join(extra[:6]))

    wrong = [feature for feature, mark in listed.items()
             if feature in wanted and mark != ("❌" if wanted[feature] == "⬜" else "⏳")]
    if wrong:
        fail("docs/CHECKLIST-URDU.md disagrees with the board about "
             + ", ".join(sorted(wrong)[:6]) + " - run: python3 tools/make_checklist.py")

    counts = {"✅": 0, "⏳": 0, "❌": 0}
    for mark in wanted.values():
        counts["❌" if mark == "⬜" else "⏳"] += 1
    total = sum(counts.values())

    page = printable.read_text(encoding="utf-8")

    for mark, word in (("✅", "مکمل"), ("⏳", "جاری"), ("❌", "شروع نہیں")):
        if f"<td>{mark} {word}</td><td>{counts[mark]}</td>" not in page:
            fail(f"docs/CHECKLIST-URDU.html does not print the {word} count ({counts[mark]}) - the "
                 "printable page must show the same totals as the board")

    absent = [feature for feature in wanted if f'class="id">{feature}<' not in page]
    if absent:
        fail(f"docs/CHECKLIST-URDU.html is missing {len(absent)} feature row(s), starting with "
             f"{absent[0]} - a printed list with holes is worse than no list")

    script = launcher.read_text(encoding="utf-8", errors="replace")

    if "CHECKLIST-URDU.html" not in script:
        fail("PRINT-CHECKLIST.cmd does not open docs\\CHECKLIST-URDU.html - the double-click would "
             "print nothing")

    if "pause" not in script.lower():
        fail("PRINT-CHECKLIST.cmd does not pause - a double-clicked file must leave its window open")

    REPORT.append(f"Checklist: {total} features ({counts['✅']} done, {counts['⏳']} in progress, "
                  f"{counts['❌']} not started) match the board, the printable page and "
                  "PRINT-CHECKLIST.cmd agree")


# ---------------------------------------------------------------------------
def check_run_trail() -> None:
    """A run must leave a heartbeat, and a failed button must stay on its page (v0.34.0, D-90).

    Why: on 23 Oct 2026 the owner was asked to look for `%TEMP%\Area11\binding-errors.txt`, found
    the folder missing, and that absence was read - by both of us - as if it proved something. It
    did not: a run with no silent binding fault writes no file, and a run that never started writes
    no file either. An absence is only evidence when its cause is knowable, so:

      * the program writes `%TEMP%\Area11\last-run.txt` **before its first window**, and adds a
        line for the database, the sign-in, every page and the exit;
      * the trail can never throw and never grows without a cap;
      * one failed button is caught inside the button, reported on one line above the page, and
        never reaches the dispatcher;
      * the report tool prints all of it, so the next report answers this question itself.
    """
    trail = DOTNET / "Area11.Core/Utils/RunTrail.cs"
    guard = DOTNET / "Area11.Core/Utils/CommandGuard.cs"
    program = DOTNET / "Area11.Desktop/Program.cs"
    relay = DOTNET / "Area11.Desktop/ViewModels/RelayCommand.cs"
    shell = DOTNET / "Area11.Desktop/ViewModels/ShellViewModel.cs"
    app = DOTNET / "Area11.Desktop/App.xaml.cs"
    window = DOTNET / "Area11.Desktop/Views/ShellWindow.xaml"
    report = ROOT / "tools/support-report.ps1"
    tests = DOTNET / "Area11.Tests/RunTrailTests.cs"
    guard_tests = DOTNET / "Area11.Tests/CommandGuardTests.cs"

    for needed in (trail, guard, program, relay, shell, app, window, report, tests, guard_tests):
        if not needed.exists():
            fail(f"the heartbeat is half-built: {rel(needed)} is missing")
    if not all(path.exists() for path in (trail, guard, program, relay, shell, app, window, report,
                                          tests, guard_tests)):
        return

    trail_text = trail.read_text(encoding="utf-8", errors="replace")

    if '"last-run.txt"' not in trail_text:
        fail("RunTrail does not write last-run.txt - the report tool and the owner are told to look "
             "for that exact name")

    # v0.35.1: the header line `Folder : ` called `Safe(AppContext.BaseDirectory)` while `Safe`
    # takes a `Func<string>` - CS1503, and the owner's build stopped there before the first window.
    # A static check cannot type-check, but it can insist that every `Safe(` call in this file is
    # handed a lambda, like its three neighbours. Proved by putting the fault back.
    safe_calls = trail_text.count("Safe(") - trail_text.count("string Safe(")
    safe_lambdas = trail_text.count("Safe(() =>")

    if safe_calls != safe_lambdas:
        fail(f"RunTrail calls Safe(...) {safe_calls} time(s) but hands it a lambda {safe_lambdas} "
             "time(s) - Safe takes a Func<string>, so a plain value is CS1503 and the build stops "
             "before the first window (that is exactly what v0.34.0 did)")

    # v0.35.2: `File.AppendAllText` writes exactly the characters it is given - no line end - so
    # every step after "started" was glued onto the previous one and the run read as a single line
    # (four tests failed on the owner's PC for exactly this). The file must be written by a call
    # that adds the terminator.
    if "File.AppendAllText(" in trail_text or ("File.AppendAllLines(" not in trail_text
                                              and "Environment.NewLine" not in trail_text):
        fail("RunTrail appends a step in a way that adds no line end - File.AppendAllText writes "
             "exactly the characters it is given, so every step glues onto the one before it and the "
             "trail becomes one unreadable line (v0.35.1 did exactly that on the owner's PC and four "
             "tests failed on it); one step must be written by AppendAllLines")

    if "Path.GetTempPath()" not in trail_text:
        fail("RunTrail does not use the temp folder - a diagnostics file must never be written into "
             "the shop folder, which can be the second counter's share")

    if "catch (Exception)" not in trail_text:
        fail("RunTrail does not catch everything - a heartbeat that throws would turn a healthy "
             "start into a crash report about a heartbeat")

    if "MaxLines" not in trail_text:
        fail("RunTrail has no size cap - a PC that is never switched off would fill the disk")

    program_text = program.read_text(encoding="utf-8", errors="replace")

    begin_at = program_text.find("RunTrail.Begin(")
    window_at = program_text.find("app.InitializeComponent();")

    if begin_at < 0:
        fail("Program.Main never starts the heartbeat - an absent file would go back to proving "
             "nothing")
    elif window_at >= 0 and begin_at > window_at:
        fail("the heartbeat is started after the first window - a start that dies in the theme "
             "(the 22 Oct fault) would leave no file at all")

    if "RunTrail.Step(" not in shell.read_text(encoding="utf-8", errors="replace"):
        fail("the window frame never adds a page to the trail - the file could not say how far the "
             "operator got")

    if "RunTrail.Step(" not in program_text:
        fail("the trail is never told that the program closed - a missing 'closed' line would then "
             "be read as a crash")

    guard_text = guard.read_text(encoding="utf-8", errors="replace")

    if "catch (Exception exception)" not in guard_text:
        fail("CommandGuard does not catch the exception it exists to catch")

    if "CrashLog.Write(" not in guard_text:
        fail("a failed button leaves no crash report - the owner would be asked to send a file that "
             "does not exist")

    if "Failed?.Invoke(" not in guard_text:
        fail("CommandGuard never tells the window - the operator would see the button do nothing")

    relay_text = relay.read_text(encoding="utf-8", errors="replace")

    if relay_text.count("CommandGuard.Run(") < 2:
        fail("not every RelayCommand runs through the guard - one of the two kinds still lets an "
             "exception reach the dispatcher, which replaces the whole page")

    shell_text = shell.read_text(encoding="utf-8", errors="replace")

    if "CommandGuard.Failed +=" not in shell_text or "CommandGuard.Failed -=" not in shell_text:
        fail("the frame does not subscribe AND unsubscribe to the command guard - a closed window "
             "would keep answering for a shell nobody is looking at")

    window_text = window.read_text(encoding="utf-8", errors="replace")

    if "{Binding FaultBanner" not in window_text or "HasFaultBanner" not in window_text:
        fail("ShellWindow does not draw the failed-button line - the fault would be silent again")

    app_text = app.read_text(encoding="utf-8", errors="replace")

    if "CommandGuard.Sink" not in app_text:
        fail("the command guard has no log sink - a failed button would write no line in the "
             "shop's own log")

    report_text = report.read_text(encoding="utf-8", errors="replace")

    for needle, reason in (
        ("last-run.txt", "the report tool does not print the heartbeat"),
        ("binding-errors.txt", "the report tool does not explain the binding watch"),
        ("HEALTHY answer", "the report tool does not say that an absent binding file is the healthy "
                           "answer - that is the exact confusion of 23 Oct 2026"),
    ):
        if needle not in report_text:
            fail(f"tools/support-report.ps1: {reason}")

    REPORT.append("Run trail: the program leaves a capped heartbeat before its first window and names "
                  "every page; one failed button is caught in its own button, reported on one line "
                  "and never reaches the dispatcher; the report tool prints both files and says what "
                  "an absent one means")


# ---------------------------------------------------------------------------
def check_phase_one_shell() -> None:
    """The expanded dark header must not silently fall back to the old sidebar or split filters.

    WPF cannot be launched in the Linux build area, so this gate checks the structural contracts
    whose failure previously produced a blank control: every header collection has a command and a
    view-model source, Settings has one (shell-owned) section row, and the category filter is not the
    product editor's SelectedCategory. Physical placement and keyboard walk-through remain owner
    evidence in PHYSICAL-TESTING.md rather than something this check claims to prove.
    """
    window_path = DOTNET / "Area11.Desktop/Views/ShellWindow.xaml"
    shell_path = DOTNET / "Area11.Desktop/ViewModels/ShellViewModel.cs"
    settings_path = DOTNET / "Area11.Desktop/Views/SettingsView.xaml"
    products_path = DOTNET / "Area11.Desktop/ViewModels/ProductsViewModel.cs"
    nav_path = DOTNET / "Area11.Desktop/ViewModels/NavItem.cs"
    header_items_path = DOTNET / "Area11.Desktop/ViewModels/HeaderMenuItem.cs"

    for path in (window_path, shell_path, settings_path, products_path, nav_path, header_items_path):
        if not path.exists():
            fail(f"Phase-1 shell is incomplete: {rel(path)} is missing")
    if not all(path.exists() for path in
               (window_path, shell_path, settings_path, products_path, nav_path, header_items_path)):
        return

    window = window_path.read_text(encoding="utf-8-sig")
    shell = shell_path.read_text(encoding="utf-8-sig")
    settings = settings_path.read_text(encoding="utf-8-sig")
    products = products_path.read_text(encoding="utf-8-sig")
    nav = nav_path.read_text(encoding="utf-8-sig")

    for needle, reason in (
        ('Text="Area11"', "the fixed Area11 wordmark is missing"),
        ('ItemsSource="{Binding ModuleItems}"', "top-row modules are not bound"),
        ('ItemsSource="{Binding SubNavItems}"', "contextual module pages are not bound"),
        ('ItemsSource="{Binding SettingsTabItems}"', "Settings sections are not in the shell"),
        ('ItemsSource="{Binding ProductCategories}"', "Product categories are not in the shell"),
        ('Command="{Binding GoHomeCommand}"', "the Area11 dashboard route is missing"),
        ('Width="44" Height="44"', "the alert target is no longer the approved 44x44 size"),
        ('Command="{Binding OpenAlertsCommand}"', "the alert bell lost its page route"),
        ('ContentControl Grid.Row="1" Content="{Binding CurrentViewModel}"',
         "the expanded header no longer leaves a content host"),
    ):
        if needle not in window:
            fail(f"Phase-1 ShellWindow: {reason} ({needle})")

    for forbidden, reason in (
        ('Width="176"', "the old fixed sidebar width has returned"),
        ('{Binding StoreName}', "the shop name is again a second application title"),
        ('{Binding AppName}', "the application title is editable again instead of fixed Area11"),
    ):
        if forbidden in window:
            fail(f"Phase-1 ShellWindow: {reason}")

    for needle, reason in (
        ("OpenModuleCommand = new RelayCommand<NavItem>(OpenModule)", "module clicks have no command"),
        ("OpenPageCommand = new RelayCommand<NavItem>(OpenContextPage)", "page clicks have no command"),
        ("OpenSettingsTabCommand = new RelayCommand<SettingsTabItem>(OpenSettingsTab)",
         "Settings section clicks have no command"),
        ("StartPagePolicy.Choose(", "startup does not select a permitted counter/fallback"),
        ("ModuleItems.Add(header)", "permission-filtered groups are absent from the top row"),
        ("UpdateHeaderNavigation();", "active module/page state is not synchronized"),
        ("products.ApplyFilter(categoryId: value.Id)", "the header category does not use the existing filter"),
        ("_services.Permissions.Can", "navigation no longer starts from permissions"),
    ):
        if needle not in shell:
            fail(f"Phase-1 ShellViewModel: {reason} ({needle})")

    if "ModuleItems.Add(dashboard)" in shell or "NavItems.Add(dashboard)" not in shell:
        fail("Dashboard must remain a wordmark route but not be a standalone module")
    bridge = DOTNET / "Area11.Desktop/Views/HeaderSectionTabs.cs"
    codebehind = DOTNET / "Area11.Desktop/Views/ShellWindow.xaml.cs"
    if not bridge.exists() or not codebehind.exists():
        fail("Header section-tab bridge is missing")
    else:
        bridge_text = bridge.read_text(encoding="utf-8-sig")
        code_text = codebehind.read_text(encoding="utf-8-sig")
        for needle in ("HeaderSectionItem", "tabs.Loaded += OnLoaded", "tabs.Unloaded += OnUnloaded"):
            if needle not in bridge_text:
                fail(f"Header section-tab bridge missing {needle}")
        for needle in ("ReferenceEquals(_headerTabs, tabs)", "item.Tab.Visibility == Visibility.Visible",
                       "item.Tab.IsEnabled", "_headerTabs.Items.Contains(item.Tab)"):
            if needle not in code_text:
                fail(f"Header section-tab selection guard missing {needle}")
    for name in ("PartnersView.xaml", "ReturnsView.xaml", "DoctorsView.xaml", "ImportExportView.xaml"):
        page = (DOTNET / "Area11.Desktop/Views" / name).read_text(encoding="utf-8-sig")
        if 'HeaderSectionTabs.InHeader="True"' not in page or 'ContentSource="SelectedContent"' not in page:
            fail(f"{name} must host its existing tab contents with shell header controls")
    for needle in ("HeaderSections, ElementName=ShellRoot", "Tab.Visibility", "Tab.IsEnabled",
                   "Tab.IsSelected", "OnHeaderSectionClick"):
        if needle not in window:
            fail(f"Shell header section binding missing {needle}")

    if shell.count("SettingsTabItems.Add(new SettingsTabItem(") != 8:
        fail("Settings shell must expose all eight contents, including identity and Fields")

    for obsolete in ("menu.collapsedGroups", "menu.pinnedOpen", "ToggleGroup", "SetCollapsed"):
        if obsolete in shell:
            fail(f"Phase-1 ShellViewModel still lets the retired sidebar fold control navigation: {obsolete}")

    for needle, reason in (
        ('ContentSource="SelectedContent"', "Settings would draw a second tab strip or lose its content"),
        ('SelectedIndex="{Binding SelectedTabIndex, Mode=TwoWay}"',
         "shell sections no longer select the existing Settings content"),
    ):
        if needle not in settings:
            fail(f"Phase-1 SettingsView: {reason} ({needle})")

    for obsolete in ("MenuPinnedOpen", "Application name (top of the window)"):
        if obsolete in settings:
            fail(f"Phase-1 SettingsView still shows a retired sidebar/application-title setting: {obsolete}")

    if "public int? CategoryFilterId => _categoryFilter;" not in products:
        fail("Products no longer exposes a read-only filter id for header synchronization")
    if "public bool IsCurrentModule" not in nav or "public bool IsCurrentPage" not in nav:
        fail("NavItem no longer exposes distinct active module and active page states")

    # Owner's 2026-11-11 crash (InvalidOperationException, "while a Window is closing"): setting
    # DialogResult on a modal dialog already closes it, so an explicit Close() on the next line
    # runs against a closing window and explodes. WPF cannot run here, so the ban is structural.
    dialog_result = re.compile(r"^\s*(\w+\.)?DialogResult\s*=\s*(true|false)\s*;\s*$")
    for behind in sorted((DOTNET / "Area11.Desktop").rglob("*.xaml.cs")):
        lines = behind.read_text(encoding="utf-8-sig").splitlines()
        for index, line in enumerate(lines[:-1]):
            if dialog_result.match(line) and lines[index + 1].strip() == "Close();":
                fail(f"{rel(behind)}:{index + 2} calls Close() right after DialogResult - "
                     "the dialog already closes itself and this crashes (Nov 2026)")

    # Kiosk login (owner's word, Nov 2026, v0.66.0): auto-start PCs sign in fullscreen with the
    # taskbar really hidden. WPF cannot run here, so the wiring is pinned structurally: the login
    # hides on kiosk entry (auto/protected only) and restores on close, the crash box parents to
    # the open window (an ownerless box would hide UNDER the topmost login), and the app heals a
    # kill-orphaned taskbar at startup and forces it back at exit (first copy only, both times).
    taskbar_path = DOTNET / "Area11.Desktop/TaskbarHider.cs"
    login_path = DOTNET / "Area11.Desktop/Views/LoginWindow.xaml.cs"
    crash_path = DOTNET / "Area11.Desktop/CrashReporter.cs"
    app_shell_path = DOTNET / "Area11.Desktop/App.xaml.cs"
    for path in (taskbar_path, login_path, crash_path, app_shell_path):
        if not path.exists():
            fail(f"Kiosk login is incomplete: {rel(path)} is missing")
    if all(path.exists() for path in (taskbar_path, login_path, crash_path, app_shell_path)):
        taskbar_text = taskbar_path.read_text(encoding="utf-8-sig")
        login_text = login_path.read_text(encoding="utf-8-sig")
        crash_text = crash_path.read_text(encoding="utf-8-sig")
        app_shell_text = app_shell_path.read_text(encoding="utf-8-sig")
        for needle, reason in (
            ("Shell_TrayWnd", "the taskbar window is not addressed"),
            ("Shell_SecondaryTrayWnd", "extra monitors keep their taskbar"),
        ):
            if needle not in taskbar_text:
                fail(f"Kiosk TaskbarHider: {reason}")
        for needle, reason in (
            ("WantsAutoStart", "fullscreen is not limited to auto-start PCs"),
            ("WindowStyle.None", "the kiosk login keeps its frame"),
            ("Topmost = true", "another window can sit above the kiosk login"),
            ("TaskbarHider.Hide()", "the kiosk login does not hide the taskbar"),
            ("TaskbarHider.Show()", "a closed login can orphan the taskbar hidden"),
        ):
            if needle not in login_text:
                fail(f"Kiosk LoginWindow: {reason} ({needle})")
        if "MessageBox.Show(owner" not in crash_text:
            fail("Kiosk CrashReporter: the crash box can open under the topmost login")
        if app_shell_text.count("TaskbarHider.EnsureVisible()") < 2:
            fail("Kiosk App: the startup heal and the exit restore are not both present")

    # Owner's 2026-11-11 stack trace (VerifyNotClosing <- InternalClose <- ConfirmShellClosing):
    # calling Close() re-entrantly from inside a Closing handler explodes, so the post-unlock
    # close must be deferred past the event (Dispatcher.BeginInvoke). Both guarded windows carry
    # the pattern, so both are pinned: no bare Close() line, and the deferred close must exist.
    for guarded in (DOTNET / "Area11.Desktop/Views/ShellWindow.xaml.cs",
                    DOTNET / "Area11.Desktop/Views/LoginWindow.xaml.cs"):
        guarded_lines = guarded.read_text(encoding="utf-8-sig").splitlines()
        if not any("BeginInvoke" in line and "Close" in line for line in guarded_lines):
            fail(f"{rel(guarded)} must defer its post-unlock Close past the Closing event (Nov 2026)")
        for index, line in enumerate(guarded_lines):
            if line.strip() == "Close();":
                fail(f"{rel(guarded)}:{index + 1} calls Close() directly - a guarded window must "
                     "defer it past Closing or VerifyNotClosing crashes (Nov 2026)")

    # Repair (owner's word, Nov 2026, v0.67.0): every Settings section opened its NEIGHBOUR's tab -
    # the headerless TabControl starts with "App name & icon" at 0, so Shop is 1, not 0. WPF cannot
    # run here, so each section number is pinned against its true tab, with "My settings" too.
    for needle, reason in (
        ('SettingsTabItems.Add(new SettingsTabItem(1, "Shop"))', "Shop section lost tab 1"),
        ('SettingsTabItems.Add(new SettingsTabItem(2, "Printing"))', "Printing lost tab 2"),
        ('SettingsTabItems.Add(new SettingsTabItem(3, "Sales rules"))', "Sales rules lost tab 3"),
        ('SettingsTabItem(4, _services.AppLabels.Get("cloud")', "Cloud section lost tab 4"),
        ('SettingsTabItems.Add(new SettingsTabItem(5, "Staff & permissions"))', "Staff lost tab 5"),
        ('SettingsTabItems.Add(new SettingsTabItem(6, "App words"))', "App words lost tab 6"),
        ('SettingsTabItems.Add(new SettingsTabItem(7, "Fields"))', "Fields section lost tab 7"),
        ("ShowSettings(5)", "My settings no longer opens the Staff tab"),
    ):
        if needle not in shell:
            fail(f"Settings sections: {reason} ({needle})")

    # Lockdown chrome (v0.67.0): no minimize, maximized, taskbar hidden while the guard is on; the
    # settings page raises the refresh, the shell re-raises it, the frame applies it. Structural.
    settings_vm_path = DOTNET / "Area11.Desktop/ViewModels/SettingsViewModel.cs"
    shell_behind_path = DOTNET / "Area11.Desktop/Views/ShellWindow.xaml.cs"
    for path in (settings_vm_path, shell_behind_path):
        if not path.exists():
            fail(f"Lockdown chrome is incomplete: {rel(path)} is missing")
    if all(path.exists() for path in (settings_vm_path, shell_behind_path)):
        settings_vm_text = settings_vm_path.read_text(encoding="utf-8-sig")
        shell_behind_text = shell_behind_path.read_text(encoding="utf-8-sig")
        if "LockdownChromeChanged" not in settings_vm_text:
            fail("Lockdown chrome: the settings page does not raise the refresh")
        if "ForwardChromeChanged" not in shell:
            fail("Lockdown chrome: the shell does not forward the refresh to the frame")
        for needle, reason in (
            ("ApplyLockdownChrome", "the frame never applies the guard chrome"),
            ("ResizeMode.NoResize", "the minimize box survives the guard"),
            ("TaskbarHider.Hide()", "the taskbar survives the guard"),
            ("TaskbarHider.Show()", "a closed/unlocked frame can orphan the taskbar hidden"),
            ("_chromeHidden", "repeated applies can stack taskbar hides"),
            ("OnShellStateChanged", "Win+Down minimizes past the guard"),
        ):
            if needle not in shell_behind_text:
                fail(f"Lockdown chrome: {reason} ({needle})")

    REPORT.append(
        "Shell: fixed Area11, 44px bell, permission-filtered modules, Counter-first startup, "
        "contextual pages, six shell-owned Settings sections and a separate Products category filter; "
        "no left-sidebar fold or editable application title")


# ---------------------------------------------------------------------------
def check_phase_two_fields() -> None:
    """Phase-2 field metadata must stay centralized and non-destructive."""
    policy_path = DOTNET / "Area11.Core/Services/FieldGovernance.cs"
    model_path = DOTNET / "Area11.Core/Models/CustomFieldModels.cs"
    service_path = DOTNET / "Area11.Core/Services/CustomFieldService.cs"
    migration_path = DOTNET / "Area11.Core/Data/Migrations/029_field_governance.sql"
    admin_path = DOTNET / "Area11.Desktop/Views/CustomFieldsView.xaml"
    editor_path = DOTNET / "Area11.Desktop/ViewModels/CustomFieldsEditorViewModel.cs"
    tests_path = DOTNET / "Area11.Tests/FieldGovernanceTests.cs"
    paths = (policy_path, model_path, service_path, migration_path, admin_path, editor_path, tests_path)
    if not all(path.exists() for path in paths):
        fail("Phase-2 field governance is incomplete: policy, migration, UI or regression file is missing")
        return

    policy = policy_path.read_text(encoding="utf-8-sig")
    model = model_path.read_text(encoding="utf-8-sig")
    service = service_path.read_text(encoding="utf-8-sig")
    migration = migration_path.read_text(encoding="utf-8-sig")
    admin = admin_path.read_text(encoding="utf-8-sig")
    editor = editor_path.read_text(encoding="utf-8-sig")
    tests = tests_path.read_text(encoding="utf-8-sig")

    for needle, reason in (
        ("public static string KeyFor", "stable field keys have no central generator"),
        ("public static string? DefinitionError", "definition validation is not centralized"),
        ("public static bool Normalize", "answer normalization is not centralized"),
        ("double.IsFinite", "non-finite custom numbers are not refused"),
        ("MaxHelpTextLength", "field guidance has no bound"),
    ):
        if needle not in policy:
            fail(f"Phase-2 FieldGovernance: {reason}")

    for needle, reason in (
        ("field_key AS fieldKey", "the service does not read stable keys"),
        ("help_text AS helpText", "the service does not read field guidance"),
        ("FieldGovernance.Normalize(field, raw, photoCount", "the old service bypasses central normalization"),
        ("FieldGovernance.KeyFor(key, id)", "new fields are not assigned stable keys"),
        ("help_text = @helpText", "field guidance is not saved"),
    ):
        if needle not in service:
            fail(f"Phase-2 CustomFieldService: {reason}")

    for needle, reason in (
        ("ADD COLUMN field_key", "the schema has no stable key column"),
        ("ADD COLUMN help_text", "the schema has no guidance column"),
        ("LOWER(TRIM(entity)) || '.custom.' || id", "legacy keys are not deterministically backfilled"),
        ("CREATE UNIQUE INDEX", "stable keys are not unique"),
    ):
        if needle not in migration:
            fail(f"Phase-2 migration: {reason}")
    if "DELETE FROM custom_values" in migration or "DROP TABLE custom_fields" in migration:
        fail("Phase-2 migration must not delete custom answers or rebuild their table")

    for needle, path, text in (
        ("SelectedFieldKey", admin_path, admin),
        ("HelpText", admin_path, admin),
        ("Field.HelpText", editor_path, editor),
        ("RenameKeepsTheKeyAndAnswer", tests_path, tests),
        ("FieldGovernanceMigration_BackfillsStableKeysAndKeepsAnswers", DOTNET / "Area11.Tests/MigrationTests.cs",
         (DOTNET / "Area11.Tests/MigrationTests.cs").read_text(encoding="utf-8-sig")),
    ):
        if needle not in text:
            fail(f"Phase-2 field governance evidence is missing: {rel(path)} ({needle})")

    if "FieldKey" not in model or "public string Key =>" not in model:
        fail("Phase-2 model does not expose a stable key with a legacy fallback")

    REPORT.append(
        "Phase 2 fields: central definition/value policy, stable rename-safe keys, bounded guidance, "
        "non-destructive migration 029 and focused regression coverage")


# ---------------------------------------------------------------------------
def check_phase_three_packaging() -> None:
    """Phase-3 pack labels must stay descriptive, editable and searchable on every agreed screen."""
    policy_path = DOTNET / "Area11.Core/Services/PackagingPolicy.cs"
    service_path = DOTNET / "Area11.Core/Services/ProductService.cs"
    model_path = DOTNET / "Area11.Core/Models/Product.cs"
    products_vm_path = DOTNET / "Area11.Desktop/ViewModels/ProductsViewModel.cs"
    products_view_path = DOTNET / "Area11.Desktop/Views/ProductsView.xaml"
    product_editor_path = DOTNET / "Area11.Desktop/Views/ProductEditorWindow.xaml"
    pos_vm_path = DOTNET / "Area11.Desktop/ViewModels/PosViewModel.cs"
    pos_view_path = DOTNET / "Area11.Desktop/Views/PosView.xaml"
    purchase_vm_path = DOTNET / "Area11.Desktop/ViewModels/PurchasesViewModel.cs"
    purchase_view_path = DOTNET / "Area11.Desktop/Views/PurchasesView.xaml"
    tests_path = DOTNET / "Area11.Tests/PackagingSearchTests.cs"
    paths = (policy_path, service_path, model_path, products_vm_path, products_view_path, product_editor_path,
             pos_vm_path, pos_view_path, purchase_vm_path, purchase_view_path, tests_path)
    if not all(path.exists() for path in paths):
        fail("Phase-3 packaging/search is incomplete: policy, product editor, screen or regression file is missing")
        return

    policy = policy_path.read_text(encoding="utf-8-sig")
    service = service_path.read_text(encoding="utf-8-sig")
    model = model_path.read_text(encoding="utf-8-sig")
    products_vm = products_vm_path.read_text(encoding="utf-8-sig")
    products_view = products_view_path.read_text(encoding="utf-8-sig")
    product_editor = product_editor_path.read_text(encoding="utf-8-sig")
    pos_vm = pos_vm_path.read_text(encoding="utf-8-sig")
    pos_view = pos_view_path.read_text(encoding="utf-8-sig")
    purchase_vm = purchase_vm_path.read_text(encoding="utf-8-sig")
    purchase_view = purchase_view_path.read_text(encoding="utf-8-sig")
    tests = tests_path.read_text(encoding="utf-8-sig")

    for needle, reason in (
        ("public static string CleanPackSize", "pack-label normalization is not centralized"),
        ("public static string? PackSizeError", "pack-label validation is not centralized"),
        ("public static string SearchKey", "spacing-insensitive pack search has no shared key"),
        ("public static bool Matches", "Desktop pages have no shared exact product matcher"),
        ("additionalBarcodes", "the shared matcher could drop pack-specific barcodes"),
    ):
        if needle not in policy:
            fail(f"Phase-3 PackagingPolicy: {reason}")

    for needle, reason in (
        ("IFNULL(p.pack_size_label, '') LIKE @like", "SQL product search omits the pack label"),
        ("keyLike", "SQL product search cannot match 50ml against 50 mL"),
        ("packSizeLabel = NullIfEmpty(packSizeLabel)", "create does not save the normalized label"),
        ("PackagingPolicy.CleanPackSize(patch.PackSizeLabel)", "update bypasses label normalization"),
    ):
        if needle not in service:
            fail(f"Phase-3 ProductService: {reason}")
    if service.count("PackagingPolicy.PackSizeError") < 2:
        fail("Phase-3 ProductService: create and update do not both validate the pack label")

    for needle, reason in (
        ("public string PackSizeLabel", "the product editor has no pack-label property"),
        ("PackSizeLabel = product.PackSizeLabel", "an existing label is not loaded into the editor"),
        ("PackSizeLabel = PackSizeLabel", "the editor does not submit the label"),
        ("PackSizeLabel = string.Empty", "a new product can inherit the previous label"),
    ):
        if needle not in products_vm:
            fail(f"Phase-3 ProductsViewModel: {reason}")

    for text, path, needles in (
        (product_editor, product_editor_path, ('{Binding PackSizeLabel, UpdateSourceTrigger=PropertyChanged}',)),
        (products_view, products_view_path, ('{Binding PackSizeLabel, Mode=OneWay}',)),
        (pos_view, pos_view_path, ('{Binding PackSizeLabel, Mode=OneWay}',)),
        # v0.71.0: the label moved into the photo ItemTemplate - DisplayMemberPath plus a
        # template is the pair that killed the page on open, so the pin follows the template.
        (purchase_view, purchase_view_path, ('{Binding ProductChoiceLabel}',)),
    ):
        for needle in needles:
            if needle not in text:
                fail(f"Phase-3 {rel(path)}: required pack/variant binding is missing ({needle})")

    if "PackagingPolicy.Matches(product, term, codes)" not in pos_vm:
        fail("Phase-3 Counter no longer uses the shared matcher with its extra barcode map")
    if "PackagingPolicy.Matches(product, term)" not in purchase_vm:
        fail("Phase-3 Purchase no longer uses the shared product matcher")
    if "public string ProductChoiceLabel" not in model or "public string VariantLabel" not in model:
        fail("Phase-3 Product model cannot distinguish variants in the purchase picker")

    for test_name in (
        "PackLabelIsEditableNormalized_AndPartialUpdatePreservesOtherFields",
        "PackLabelValidationRefusesMultilineAndOverlongTextWithoutWriting",
        "PackSearchWithFlexibleSpacingFindsOnlyTheIntendedSeparateVariant",
        "ProductSearchIncludesPackBrandRoomCategoryAndCompanyMetadata",
        "SharedDesktopSearchMatchesPackagingAndEveryExistingBarcode",
        "ProductChoiceLabelDistinguishesVariantsWithoutCreatingAnotherIdentity",
        "PackLabelEditDoesNotChangeSaleConversionPriceOrStock",
        "SmartSearchUsesExactPackHitBeforeFuzzy_AndBlankSearchRemainsSafe",
    ):
        if test_name not in tests:
            fail(f"Phase-3 packaging regression fact is missing: {test_name}")

    # Product workspace: an explicit New/Edit and real row double-click open the same modal form.
    editor_code = (DOTNET / "Area11.Desktop/Views/ProductEditorWindow.xaml.cs").read_text(encoding="utf-8-sig")
    view_code = (DOTNET / "Area11.Desktop/Views/ProductsView.xaml.cs").read_text(encoding="utf-8-sig")
    door_code = (DOTNET / "Area11.Desktop/ProductEditorDoor.cs").read_text(encoding="utf-8-sig")
    for needle in ('Command="{Binding NewCommand}"', 'Command="{Binding EditCommand}"',
                   'MouseDoubleClick="OnProductDoubleClick"'):
        if needle not in products_view:
            fail(f"Product workspace is missing {needle}")
    for needle in ("if (node is ButtonBase) return;", "DataGridRow { Item: Product product }",
                   "vm.EditCommand.Execute(null)"):
        if needle not in view_code:
            fail(f"Product double-click needs a real row and must not hijack row buttons: {needle}")
    for needle in ("window.ShowDialog()",):
        if needle not in door_code:
            fail(f"Product form must remain modal: {needle}")
    for needle in ("MessageBoxResult.No", "ConfirmEditorClosing", "_viewModel.EditorSaved -= OnEditorSaved"):
        if needle not in editor_code:
            fail(f"Product form is missing a safe close/unsubscribe guard: {needle}")
    for needle in ("ProductEditorDoor.Show(this)", "_editingId = created.Value", "Editor.Load(_editingId!.Value)",
                   "Product and extra fields saved.", "MessageBoxResult.No"):
        if needle not in products_vm:
            fail(f"Product editor lost a save, dialog or removal guard: {needle}")
    start_policy = (DOTNET / "Area11.Core/Utils/StartPagePolicy.cs").read_text(encoding="utf-8-sig")
    if 'keys.FirstOrDefault(key => key == "dashboard")' not in start_policy:
        fail("Startup without Counter must prefer the existing Dashboard route")

    # The list projection carries category display text, not the edit form's tag IDs.
    # Loading an edit directly from a list row silently wipes existing tags on Save.
    for needle in ("_services.Products.Get(_user, product.Id)", "LoadForm(full.Value)", "Editor.Load(full.Value.Id)"):
        if needle not in products_vm:
            fail(f"Product editing must load the complete record and category IDs: {needle}")
    for needle in ("primaryCategoryId is null && clean.Count > 0", "Choose a primary category before adding additional category tags."):
        if needle not in service:
            fail(f"Product additional tags must not silently acquire an implied primary: {needle}")
    if 'Binding="{Binding CategoryTags, Mode=OneWay}"' not in products_view:
        fail("Products list must show its additional category tags separately")
    if 'Text="Primary category"' not in product_editor or 'Choose a primary category before selecting additional tags.' not in product_editor:
        fail("Product editor must explain the primary versus filter-only category relationship")
    workbook = (DOTNET / "Area11.Core/Services/WorkbookService.cs").read_text(encoding="utf-8-sig")
    workbook_tests = (DOTNET / "Area11.Tests/WorkbookTests.cs").read_text(encoding="utf-8-sig")
    for needle in ("A product with additional category tags needs a primary category",
                   "new Dictionary<string, object?>(current, StringComparer.OrdinalIgnoreCase)",
                   "ValidateRecord(connection, schema, after, changed)"):
        if needle not in workbook:
            fail(f"Workbook category preview/apply must recheck tags with the real row key: {needle}")
    for fact in ("BulkCategoryChangeRefusesClearingOrPromotingAnExistingTag",
                 "BulkCategoryClearWithoutTagsSucceedsAndIsAudited", "BulkCategoryClearRechecksTagsAddedAfterPreview"):
        if fact not in workbook_tests:
            fail(f"Workbook category safety regression is missing: {fact}")
    # Owner-managed contact rules must be checked both when creating directly and again when
    # an import or workbook preview is applied; stale previews must not bypass a changed policy.
    partner = (DOTNET / "Area11.Core/Services/PartnerService.cs").read_text(encoding="utf-8-sig")
    imports = (DOTNET / "Area11.Core/Services/ImportService.cs").read_text(encoding="utf-8-sig")
    for field in ("customer.phone", "supplier.phone", "supplier.agency"):
        for source, name, minimum in ((partner, "partner saves", 1), (imports, "CSV preview/apply", 2),
                                      (workbook, "workbook preview/apply", 1)):
            if source.count('"' + field + '"') < minimum:
                fail(f"Missing {field} safety guard in {name}")
    custom_fields = (DOTNET / "Area11.Core/Services/CustomFieldService.cs").read_text(encoding="utf-8-sig")
    for signature in ("public ServiceResult<CustomField> Add(", "public ServiceResult Update(",
                      "public ServiceResult SetActive(", "public ServiceResult Move("):
        start = custom_fields.find(signature)
        if start < 0 or "user is null || !user.IsOwner" not in custom_fields[start:start + 600]:
            fail(f"Custom field definition management must be owner-only: {signature}")
    if "(manage || includeInactive) && (user is null || !user.IsOwner)" not in custom_fields:
        fail("Custom field management/inactive listing must be owner-only")
    custom_tests = (DOTNET / "Area11.Tests/CustomFieldTests.cs").read_text(encoding="utf-8-sig")
    if "OnlyOwnerMayManageDefinitionsEvenWhenManagerHasSettingsPermission" not in custom_tests:
        fail("Custom field owner-only management regression is missing")
    for entity in ("Customer", "Supplier"):
        if f"SaveNewRecordValuesInTransaction(connection, actor, CustomFieldEntity.{entity}, newId" not in partner:
            fail(f"{entity} and new custom answers must share a transaction")
    for fact in ("NewPartnerAndRequiredAnswerAreSavedAtomically", "NewPartnerAndAnswerRollBackWhenAuditFails",
                 "CustomerAndExtraAnswersUpdateTogetherOrRollback"):
        if fact not in custom_tests:
            fail(f"Partner custom answer transaction regression missing: {fact}")
    product_service = (DOTNET / "Area11.Core/Services/ProductService.cs").read_text(encoding="utf-8-sig")
    for needle in ("SaveNewRecordValuesInTransaction(connection, actor, CustomFieldEntity.Product, newId",
                   "SaveValuesInTransaction(connection, actor, CustomFieldEntity.Product, id, customValues)",
                   "if (!answers.Ok) throw new ValidationException"):
        if needle not in product_service:
            fail(f"Product and custom answers must share a transaction: {needle}")
    for fact in ("ProductAndRequiredAnswersAreCreatedTogetherOrNotAtAll",
                 "ProductAndAnswersUpdateTogetherAndRejectStaleOrInvalidAnswers",
                 "ProductAndAnswersRollBackWhenAnswerAuditFails"):
        if fact not in custom_tests:
            fail(f"Product custom answer transaction regression missing: {fact}")
    for field in ("product.generic", "product.brand", "product.rack", "product.pack"):
        for source, name, minimum in ((product_service, "product create/update", 2),
                                      (imports, "CSV preview/apply", 2), (workbook, "workbook revalidation", 1)):
            if source.count('"' + field + '"') < minimum:
                fail(f"Missing {field} safety guard in {name}")
    category_tests = (DOTNET / "Area11.Tests/CatalogUnitTests.cs").read_text(encoding="utf-8-sig")
    for fact in ("AdditionalTagsWithoutPrimaryAreRefusedAtomically_ButUncategorisedWithoutTagsIsAllowed",
                 "UpdatingTagsRequiresPrimary_AndFailedUpdatePreservesProductAndAudit",
                 "LegacyTagsWithoutPrimaryRemainReadableAndUnrelatedUpdatesPreserveThem",
                 "FullProductReadReturnsTagIdsForTheEditorAndSearchShowsTagLabels"):
        if fact not in category_tests:
            fail(f"Category safety regression is missing: {fact}")

    REPORT.append(
        "Phase 3 packaging: existing descriptive pack label is edited, normalized and searchable in "
        "Products/Counter/Purchase; variant picker text is unambiguous while IDs, units and prices stay intact")


# ---------------------------------------------------------------------------
def check_master_foundation() -> None:
    """Approved master Phase 1 adds contracts only, without weakening posting or financial rules.

    This guard keeps the schema-only boundary honest: native critical overrides need an acknowledged
    owner, drafts stay local and unposted, quantities remain REAL, preferred and historical suppliers
    share one directory, and the new catalog parents travel before a product that references them.
    """
    migration_path = DOTNET / "Area11.Core/Data/Migrations/030_master_foundation.sql"
    models_path = DOTNET / "Area11.Core/Models/MasterFoundationModels.cs"
    sync_models_path = DOTNET / "Area11.Core/Models/SyncModels.cs"
    sync_service_path = DOTNET / "Area11.Core/Services/SyncService.cs"
    settings_path = DOTNET / "Area11.Core/Services/SettingsService.cs"
    reset_path = DOTNET / "Area11.Core/Services/IsolatedTestCopyService.cs"
    app_services_path = DOTNET / "Area11.Desktop/AppServices.cs"
    tests_path = DOTNET / "Area11.Tests/MasterFoundationTests.cs"
    sync_tests_path = DOTNET / "Area11.Tests/SyncTests.cs"
    paths = (migration_path, models_path, sync_models_path, sync_service_path, settings_path,
             app_services_path, tests_path, sync_tests_path)
    if any(not path.exists() for path in paths):
        for path in paths:
            if not path.exists():
                fail(f"Master Phase-1 foundation file is missing: {rel(path)}")
        return

    migration = migration_path.read_text(encoding="utf-8-sig")
    models = models_path.read_text(encoding="utf-8-sig")
    sync_models = sync_models_path.read_text(encoding="utf-8-sig")
    sync_service = sync_service_path.read_text(encoding="utf-8-sig")
    settings = settings_path.read_text(encoding="utf-8-sig")
    app_services = app_services_path.read_text(encoding="utf-8-sig")
    tests = tests_path.read_text(encoding="utf-8-sig")
    sync_tests = sync_tests_path.read_text(encoding="utf-8-sig")

    # Owner Windows run exposed a provider-sensitive Dapper parameter case mismatch: keep the
    # constraint test reaching SQLite rather than passing on a missing-parameter exception.
    if 'new { id = draft.Id }' not in tests or 'error.SqliteErrorCode' not in tests:
        fail("Draft negative test must bind @id exactly and verify a SQLite constraint error")
    migration_tests = (DOTNET / "Area11.Tests/MigrationTests.cs").read_text(encoding="utf-8-sig")
    if ('Assert.Equal(2, runner.ApplyPending(TestDatabase.MigrationsFolder))' not in migration_tests
            or '030_master_foundation' not in migration_tests
            or '029_field_governance' not in migration_tests):
        fail("Pre-029 upgrade test must apply both 029 and 030 and verify the ledger")

    for table in ("form_field_definitions", "document_drafts", "generic_formulas", "brands",
                  "shift_discrepancies"):
        if not re.search(rf"CREATE TABLE IF NOT EXISTS {table}\s*\(", migration):
            fail(f"Master Phase-1 migration does not create {table}")

    for needle, reason in (
        ("critical = 0 OR safe_required = 0 OR required = 1",
         "a critical safe-required field can become optional without acknowledgement"),
        ("json_valid(payload_json) AND json_type(payload_json) = 'object'",
         "draft payloads are not constrained to valid JSON objects"),
        ("status <> 'posted' OR (posted_document_id IS NOT NULL AND posted_at IS NOT NULL)",
         "a draft can be marked posted without a resulting document"),
        ("CREATE VIEW product_supplier_directory", "preferred/historical supplier directory is missing"),
        ("difference_paisa = counted_paisa - expected_paisa",
         "drawer difference arithmetic is not guarded"),
        ("UNIQUE(shift_id, stage)", "one shift can have duplicate opening/closing discrepancy rows"),
        ("ALTER TABLE products ADD COLUMN preferred_supplier_id",
         "the preferred supplier has no nullable product link"),
    ):
        if needle not in migration:
            fail(f"Master Phase-1 migration: {reason}")

    for needle in ("public const string Quantity = \"quantity\"",
                   "public const string MonthCalendar = \"month_calendar\"",
                   "public bool RequiresOverrideAcknowledgement",
                   "public const string Opening = \"opening\"",
                   "public const string Closing = \"closing\"",
                   "public const string Draft = \"draft\""):
        if needle not in models:
            fail(f"Master Phase-1 typed contract is missing: {needle}")

    order_match = re.search(r"public static readonly string\[\] Order\s*=\s*\{(.*?)\};",
                            sync_models, re.DOTALL)
    if order_match is None:
        fail("Master Phase-1 cannot inspect cloud table order")
    else:
        order = re.findall(r'"([^"]+)"', order_match.group(1))
        for parent in ("companies", "generic_formulas", "brands", "suppliers"):
            if parent not in order or "products" not in order or order.index(parent) >= order.index("products"):
                fail(f"Cloud format 3 must write {parent} before products")
        if "document_drafts" in order:
            fail("Local unposted drafts must never enter the cloud document")

    format_match = re.search(r"CurrentFormat\s*=\s*(\d+)\s*;", sync_models)
    if format_match is None or int(format_match.group(1)) < 3:
        fail("Master catalog links need cloud format 3 so older builds refuse an incomplete apply")
    for table in ("units", "form_field_definitions", "generic_formulas", "brands"):
        if f'["{table}"] = "SELECT * FROM {table} ORDER BY id"' not in sync_service:
            fail(f"Cloud local reader has no query for {table}")

    for key in ("printer.defaultBulk", "printer.bulkFormat", "profit.showMargins",
                "whatsapp.preparePdf", "whatsapp.exportDirectory", "sync.automaticEnabled",
                "sync.automaticMinutes"):
        if f'"{key}"' not in settings:
            fail(f"Master Phase-1 setting contract is missing: {key}")

    # v0.65.0 (owner's word, Nov 2026): the isolated test-copy service was removed from the
    # shipped program - no screen used it, and the release must carry no dev/test machinery. Its
    # absence is pinned here instead of its shape: resurrecting the file fails the check until
    # this guard is consciously updated. Restorable from git if the owner's test results ask for it.
    if reset_path.exists():
        fail("IsolatedTestCopyService.cs was removed in v0.65.0 - bring it back only on the owner's word")
    if "IsolatedTestCopyService" in app_services:
        fail("The isolated test rebuild must not become a genuine-shop desktop service or demo mode")

    for fact in (
        "FreshSchema_ContainsMasterContracts_AndKeepsQuantitiesReal",
        "NativeFields_SupportMonthCalendarAndRealQuantityContracts",
        "CriticalFieldCannotBecomeOptionalWithoutNamedOwnerAcknowledgement",
        "DraftsValidateJsonAndNeverBecomeFinancialDocumentsByThemselves",
        "FormulaAndBrandMastersRejectBlankNamesAndDuplicateNormalizedKeys",
        "SupplierDirectoryContainsBothPreferredAndEveryHistoricalSupplier",
        "DrawerDiscrepanciesRequireOpeningClosingArithmeticAndARealReason",
        "MasterSettings_DefaultToSinglePrintManualSyncAndVisibleProfit",
        "MasterSettings_RefuseInvalidBulkFormatAndAutomaticInterval",
        "Migration030_PreservesLegacyProductTextAndAddsHistoryWithoutGuessingLinks",
    ):
        if fact not in tests:
            fail(f"Master Phase-1 regression fact is missing: {fact}")
    if "MasterCatalogAndPreferredSupplierTravelBeforeTheProduct" not in sync_tests:
        fail("Master Phase-1 cloud round-trip regression is missing")

    REPORT.append(
        "Master Phase 1: native override/draft/catalog/supplier/drawer contracts, REAL quantities, "
        "local-only drafts, format-3 parent ordering, isolated rebuild removed in v0.65.0 and "
        "eleven focused regression facts")


# ---------------------------------------------------------------------------
# De-workshop: the shop must never see the workshop (v0.69.0, owner's word)
# ---------------------------------------------------------------------------
def check_deworkshop() -> None:
    """No dev names, spec codes, vendor names or workshop wording on the shop's face.

    Why a check and not a cleanup: every one of these looks perfect to the person who typed it.
    A window titled "Photos - Area11" after the owner named his app, a subtitle ending in (F-52),
    a crash box asking him to "send it in the chat" - each reads as an unfinished program, and
    each would come back the next time someone copies an old line. So the face is checked here.
    """
    views = sorted((DOTNET / "Area11.Desktop/Views").glob("*.xaml"))
    attr_code = re.compile(
        r'(?:Text|Content|ToolTip|Title)="[^"]*\((?:F|D|U|E|Z|T|CAT|NAV|UNIT|PHOTO)-[0-9A-Z-]+')

    for path in views:
        text = path.read_text(encoding="utf-8-sig")

        if ' - Area11"' in text:
            fail(f"{rel(path)} still titles a window with the dev name 'Area11' - the shop named "
                 "its app, and every window carries that name (v0.69.0)")

        match = attr_code.search(text)
        if match:
            fail(f"{rel(path)} shows a spec code on the screen ({match.group(0)[:60]}...) - codes "
                 "live in comments, never in front of the shop")

        if "Supabase" in text:
            fail(f"{rel(path)} names the vendor 'Supabase' on the screen - the shop reads 'cloud' "
                 "(v0.69.0)")

    crash = (DOTNET / "Area11.Desktop/CrashReporter.cs").read_text(encoding="utf-8-sig")

    if "in the chat" in crash:
        fail("CrashReporter still asks the owner to 'send it in the chat' - the shop has a support "
             "person, not a dev chat")

    if "supports your shop" not in crash:
        fail("CrashReporter lost the support wording - a crash box that names no one leaves the "
             "owner with a file and no address")

    if "public static string AppName" not in crash:
        fail("CrashReporter lost the shop's app name - the crash box would call every shop 'Area11'")

    guard = (DOTNET / "Area11.Core/Utils/PageGuard.cs").read_text(encoding="utf-8-sig")
    button = (DOTNET / "Area11.Core/Utils/CommandGuard.cs").read_text(encoding="utf-8-sig")

    for name, text in (("PageGuard.cs", guard), ("CommandGuard.cs", button)):
        if "tools\\\\SEND-REPORT.cmd" in text:
            fail(f"{name} still tells the operator to run tools\\SEND-REPORT.cmd - the file is "
                 "named, and the support person is named; the command line is not (v0.69.0)")

    close_unlock = (DOTNET / "Area11.Desktop/Views/CloseUnlockWindow.xaml.cs").read_text(
        encoding="utf-8-sig")

    if '"Who is closing " +' not in close_unlock:
        fail("CloseUnlockWindow no longer asks with the shop's app name - 'Who is closing Area11?' "
             "is the workshop talking")

    shell_code = (DOTNET / "Area11.Desktop/Views/ShellWindow.xaml.cs").read_text(encoding="utf-8-sig")

    if '"Area11", MessageBoxButton.OK, MessageBoxImage.Warning' in shell_code:
        fail("the guard's 'not allowed' box is captioned 'Area11' - it carries the shop's app name "
             "(v0.69.0)")

    REPORT.append("De-workshop: every window carries the shop's app name, no spec code, vendor name "
                  "or workshop wording on the screen")


# ---------------------------------------------------------------------------
# Warn-once save: the Fields tab must actually warn (v0.70.0, owner's word)
# ---------------------------------------------------------------------------
def check_save_warning() -> None:
    """The product form warns about the owner's necessary fields - and still saves.

    The owner clicked "Mark required" and nothing happened: the policy only reached the file
    import, and the selling price was not even in the catalog. Now the catalog holds the price
    as necessary, the editor names the missing fields in a popup with a save-anyway choice,
    and the service itself never blocks (that is the owner's chosen warn-once).
    """
    policy = (DOTNET / "Area11.Core/Services/NativeFieldPolicyService.cs").read_text(encoding="utf-8-sig")

    if '"product.retail"' not in policy:
        fail("the built-in field catalog lost the selling price - a product without a price "
             "breaks the Counter total with no warning at save time")

    if 'item.Key == "product.retail"' not in policy:
        fail("the selling price no longer ships necessary - the owner's word is that a price "
             "is expected unless he relaxes it himself")

    products = (DOTNET / "Area11.Core/Services/ProductService.cs").read_text(encoding="utf-8-sig")

    if "MissingRequiredLabels" not in products:
        fail("ProductService lost MissingRequiredLabels - the editor popup would have nothing to name")

    editor = (DOTNET / "Area11.Desktop/ViewModels/ProductsViewModel.cs").read_text(encoding="utf-8-sig")

    if "MissingRequiredLabels(patch)" not in editor or "Save anyway?" not in editor:
        fail("the product editor no longer warns about the necessary fields with a save-anyway "
             "choice - the Fields tab would be a dead switch again")

    fields = (DOTNET / "Area11.Desktop/ViewModels/NativeFieldsViewModel.cs").read_text(encoding="utf-8-sig")

    if '"product.retail"' not in fields:
        fail("the Fields tab status no longer counts the selling price as connected - it would "
             "tell the owner his price rule is a mere preference")

    REPORT.append("Warn-once save: the selling price ships necessary, the product form names what "
                  "is missing with a save-anyway choice, and the service never blocks the save")


# ---------------------------------------------------------------------------
# Template conflict: DisplayMemberPath + ItemTemplate must never meet (v0.71.0)
# ---------------------------------------------------------------------------
def check_counter_search_trial() -> None:
    """Trial B: reserved results pane, unchanged search safety and fixed actions; final choice pending."""
    import xml.etree.ElementTree as ET
    root = ET.parse(DOTNET / "Area11.Desktop/Views/PosView.xaml").getroot()
    ns = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
    xname = "{http://schemas.microsoft.com/winfx/2006/xaml}Name"
    named = {node.get(xname): node for node in root.iter() if node.get(xname)}
    parents = {child: parent for parent in root.iter() for child in parent}
    results = named.get("SearchResultsPanel")
    workspace = named.get("CounterWorkspace")
    cart = named.get("CartPanel")
    actions = named.get("CounterActions")
    footer = named.get("CheckoutFooter")
    if results is None or workspace is None or cart is None:
        fail("Counter trial B: reserved search and cart panels must be named")
    else:
        if parents.get(results) is not workspace or parents.get(cart) is not workspace:
            fail("Counter trial B: results and cart must share the bounded workspace")
        if results.get("Grid.Column") != "0" or cart.get("Grid.Column") != "2":
            fail("Counter trial B: results must be beside the cart, never on top of it")
        if results.get("Visibility") is not None or results.get("Grid.ColumnSpan") is not None or results.get("Panel.ZIndex") is not None:
            fail("Counter trial B: the search panel must stay reserved, not conditional or overlaid")
        columns = workspace.find(ns + "Grid.ColumnDefinitions")
        if columns is None or len(columns) != 3:
            fail("Counter trial B: reserve narrow search / gap / wider cart columns")
        elif [node.get("Width") for node in columns] != ["1*", "8", "2.4*"] or columns[0].get("MaxWidth") != "340":
            fail("Counter trial B: search must remain narrower and capped, leaving most width to cart")
        grids = [node for node in root.iter(ns + "DataGrid") if node.get("ItemsSource") == "{Binding Products}"]
        if len(grids) != 1 or grids[0] not in list(results.iter()):
            fail("Counter trial B: the only products grid must live inside the reserved pane")
        elif grids[0].get("Visibility") != "{Binding IsSearchResultsOpen, Converter={StaticResource BoolToVis}}":
            fail("Counter trial B: blank search must leave the reserved pane empty of products")
        else:
            templates = grids[0].find(ns + "DataGrid.Columns")
            if templates is None or len(templates) != 1 or templates[0].tag != ns + "DataGridTemplateColumn":
                fail("Counter trial B: narrow results need a single stacked-card column, not a wide table")
        if named.get("SearchStandbyHint") is None:
            fail("Counter trial B: the empty pane needs a search instruction")
    if actions is None or footer is None or actions not in list(footer.iter()):
        fail("Counter trial B: compact actions must remain in the fixed footer")
    else:
        for command in ["HoldCommand", "SaveDraftCommand", "DraftsCommand", "ToggleParkedCartsCommand", "PrintLastCommand", "ToggleCheckoutDetailsCommand"]:
            if not any(node.get("Command") == "{Binding " + command + "}" for node in actions.iter()):
                fail("Counter trial B: missing fixed action " + command)
        node = actions
        while node in parents:
            node = parents[node]
            if node.tag == ns + "ScrollViewer": fail("Counter trial B: routine buttons must not scroll away")
    vm = (DOTNET / "Area11.Desktop/ViewModels/PosViewModel.cs").read_text(encoding="utf-8-sig")
    filter_code = vm.split("private void FilterProducts()", 1)[1].split("private void ScanBarcode()", 1)[0]
    for needle in ["if (term.Length == 0)", "SelectedProduct = null;", "IsSearchResultsOpen = false;", "return;", "IsSearchResultsOpen = true;", "PackagingPolicy.Matches(product, term, codes)", "Fuzzy.Rank"]:
        if needle not in filter_code: fail("Counter trial B: missing blank/exact/fuzzy search rule " + needle)
    if filter_code.index("if (term.Length == 0)") > filter_code.index("foreach (var product in _catalog)"):
        fail("Counter trial B: blank search must return before iterating the catalog")
    add_code = vm.split("private void AddToCart()", 1)[1].split("private void RemoveLast()", 1)[0]
    for needle in ['SearchText = string.Empty;', 'SelectedProduct = null;', 'IsSearchResultsOpen = false;']:
        if needle not in add_code: fail("Counter trial B: successful add must reset search: " + needle)
    if "if (IsSplit) ShowCheckoutDetails = true;" not in vm:
        fail("Counter trial B: split inputs must open when split is selected")
    REPORT.append("Counter trial B: blank search empty, reserved narrow results pane, compact fixed actions, optional side details")


def check_counter_keyboard_phase_one() -> None:
    """Source contracts only; Windows input routing and layout still need PT-20."""
    root = ET.parse(DOTNET / "Area11.Desktop/Views/PosView.xaml").getroot()
    ns = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
    xname = "{http://schemas.microsoft.com/winfx/2006/xaml}Name"
    xkey = "{http://schemas.microsoft.com/winfx/2006/xaml}Key"
    named = {node.get(xname): node for node in root.iter() if node.get(xname)}
    for name in ["SearchEntryPanel", "SearchResultsPanel", "CartPanel", "CheckoutDetailsPanel",
                 "ParkedCartsPanel", "CheckoutFields", "CheckoutMoney", "CounterActions",
                 "MedicineSearchBox", "UnitPicker", "AddQuantityBox", "CustomerPicker", "CashReceivedBox"]:
        if name not in named: fail("Counter phase 1: missing keyboard section/anchor " + name)
    if root.get("Loaded") != "OnCounterLoaded": fail("Counter phase 1: initial search focus must be wired")
    for node in root.iter(ns + "TextBlock"):
        if node.get("Text") == "{DynamicResource AppLabel.counter}" or "{Binding CashierName" in node.get("Text", ""):
            fail("Counter phase 1: duplicate Counter/cashier header must not return")
    matches = named.get("SearchMatchesGrid")
    if matches is None or matches.get("PreviewMouseDoubleClick") != "OnSearchMatchDoubleClick":
        fail("Counter phase 1: double-click Add must be wired to the results grid")
    search = named.get("MedicineSearchBox")
    if search is None or not any(node.get("Key") == "Enter" and node.get("Command") == "{Binding ScanCommand}" for node in search.iter()):
        fail("Counter phase 1: search Enter must remain exact-barcode scan")
    actions = named.get("CounterActions")
    if actions is None or not any(node.get("Command") == "{Binding RefreshCommand}" for node in actions.iter()):
        fail("Counter phase 1: Refresh must remain accessible without the old heading")
    elif not all(node.get("Style") == "{StaticResource CounterActionButton}" for node in actions.iter(ns + "Button")):
        fail("Counter phase 1: footer buttons need the quiet rounded action style")
    style = next((node for node in root.iter(ns + "Style") if node.get(xkey) == "CounterActionButton"), None)
    if style is None or not any(node.get("CornerRadius") == "9" for node in style.iter()):
        fail("Counter phase 1: missing softly rounded action template")
    elif not any(node.get("Property") == "IsKeyboardFocused" for node in style.iter()):
        fail("Counter phase 1: focused action needs a visible keyboard cue")
    view = (DOTNET / "Area11.Desktop/Views/PosView.xaml.cs").read_text(encoding="utf-8-sig")
    for needle in ["ModifierKeys.Shift", "MoveCounterSection(e.Key)", "CartGrid.CommitEdit", "IsDropDownOpen = false",
                   "grid.CurrentCell = new DataGridCellInfo", "FirstGridCell(row)?.Focus()", "if (!e.IsRepeat) AddFocusedSearchMatch();",
                   "section.IsAncestorOf(target)",
                   "FindParent<DataGridRow>(source)", "FindParent<ButtonBase>(source) is not null",
                   "ItemsControl.ItemsControlFromItemContainer(row), SearchMatchesGrid",
                   "viewModel.AddCommand.Execute(null)", "viewModel.SearchText.Length != 0",
                   "viewModel.IsBlocked", "Window.GetWindow(this)?.IsActive", "returnToSearch"]:
        if needle not in view: fail("Counter phase 1: missing input safety contract " + needle)
    REPORT.append("Counter phase 1: titleless Counter, quiet fixed actions, guarded initial/Add focus, section navigation and row-only Add gestures (Windows proof pending)")


def check_counter_pricing_phase_two() -> None:
    """Money regression tests are written in Core; pin WPF and alternate-write-path wiring too."""
    core = (DOTNET / "Area11.Core/Services/SaleService.cs").read_text(encoding="utf-8-sig")
    model = (DOTNET / "Area11.Core/Models/SaleModels.cs").read_text(encoding="utf-8-sig")
    policy = (DOTNET / "Area11.Core/Services/PricingAllowanceService.cs").read_text(encoding="utf-8-sig")
    vm = (DOTNET / "Area11.Desktop/ViewModels/PosViewModel.cs").read_text(encoding="utf-8-sig")
    settings = (DOTNET / "Area11.Core/Services/SettingsService.cs").read_text(encoding="utf-8-sig")
    workbook = (DOTNET / "Area11.Core/Services/WorkbookService.cs").read_text(encoding="utf-8-sig")
    view = (DOTNET / "Area11.Desktop/Views/PosView.xaml.cs").read_text(encoding="utf-8-sig")
    tests = (DOTNET / "Area11.Tests/PricingPhaseTwoTests.cs").read_text(encoding="utf-8-sig")
    for needle in ['new PricingAllowanceService(_db, _audit, _log).Read(connection, user)',
                   'billBasis != defaultBasis && !pricing.OverrideBasis',
                   'mode == "manual" ? 0 : input.DiscountPercent',
                   'PricingAllowanceService.CheckDiscount(pricing, lineBasis, linePercent)',
                   'pricing.ManualReductionPercent', 'const bool blockBelowCost = true;',
                   'line.GrossPaisa - line.LineCostPaisa', 'line.GrossPaisa - discount < line.LineCostPaisa',
                   'input.DiscountPercent > 5 && !input.RetailDiscountConfirmed',
                   'input.RetailDiscountConfirmed = false;', 'pricing_mode, discount_basis, discount_percent']:
        if needle not in core: fail("Counter phase 2: missing Core pricing guard " + needle)
    if 'user.Kind switch' in core:
        fail("Counter phase 2: pricing caps must be per person, not the retired role-wide switch")
    for needle in ['!actor.IsOwner', 'IsOwner: true, Active: true', 'ManualReductionPercent is < 0 or > 100',
                   'oldValue: JsonSerializer.Serialize(before)', 'user_pricing_allowances']:
        if needle not in policy: fail("Counter phase 2: missing owner-managed per-person policy " + needle)
    for needle in ['JsonIgnore', 'RetailDiscountConfirmed', 'LineDiscountPercent', 'PricingMode', 'InputIndex']:
        if needle not in model: fail("Counter phase 2: missing durable/ephemeral pricing distinction " + needle)
    if 'key is "discount.billBasis" or "discount.blockBelowCost"' not in settings or 'key is "discount.billBasis" or "discount.blockBelowCost"' not in workbook:
        fail("Counter phase 2: UI-only settings and workbook paths must not bypass pricing policy")
    if 'Text("discount.billBasis", "profit")' not in settings or 'Only the owner can change the default discount basis.' not in settings:
        fail("Counter phase 2: missing owner-only profit default")
    for needle in ['EditCartLine(PosCartLine line)', 'window.ShowDialog();', 'if (!editor.Accepted) return;',
                   'line.PricingMode == "normal"', 'ResetBillBasis();', 'RestoreBillBasis(input.DiscountBasis)',
                   'RestoreBillBasis(cart.DiscountBasis)', 'input.RetailDiscountConfirmed = true;']:
        if needle not in vm: fail("Counter phase 2: missing cart/editor/save state contract " + needle)
    if 'OnCartLineDoubleClick' not in view or 'cartModel.EditCartLine(line)' not in view:
        fail("Counter phase 2: cart double-click and selected-row Enter must reach the owned editor")
    root = ET.parse(DOTNET / "Area11.Desktop/Views/PosView.xaml").getroot()
    if not any(node.get("PreviewMouseDoubleClick") == "OnCartLineDoubleClick" for node in root.iter()):
        fail("Counter phase 2: wire the cart editor gesture")
    for needle in ['ExistingShopFutureBillsUseTheNewProfitDefault', 'ManualAndItemDiscountLinesAreExcludedFromBillDiscount',
                   'ManualRateCannotBypassTheTwentyPercentReductionCap', 'ProfitDiscountUsesBothFefoBatchCosts',
                   'ADiscountedPartialReturnUsesSavedNetNotTodaysBasis', 'ReopeningARetailDraftRequiresFreshSaveConfirmation']:
        if needle not in tests: fail("Counter phase 2: missing named money regression " + needle)
    REPORT.append("Counter phase 2: per-person owner-only limits, batch-profit/retail line and bill basis, manual exclusion, cost floor, ephemeral confirmation, owned editor and persisted hold/draft snapshots (Windows tests pending)")


def check_counter_extra_discount_phase_three() -> None:
    """Pin sale-only allowance, return policy B and display paths; NOT C#/WPF execution."""
    def read(path: str) -> str:
        return (DOTNET / path).read_text(encoding="utf-8-sig")
    core = read("Area11.Core/Services/SaleService.cs")
    vm = read("Area11.Desktop/ViewModels/PosViewModel.cs")
    returns = read("Area11.Core/Services/ReturnService.cs")
    policy = read("Area11.Core/Services/PricingAllowanceService.cs")
    receipt = read("Area11.Core/Services/ReceiptService.cs")
    book = read("Area11.Core/Services/BookCheckService.cs")
    report = read("Area11.Core/Services/ReportService.cs")
    migration = read("Area11.Core/Data/Migrations/036_sale_extra_discount.sql")
    tests = read("Area11.Tests/ExtraDiscountTests.cs")
    for needle in ['input.ExtraDiscountPaisa < 0', '!pricing.ExtraDiscount',
                   'input.ExtraDiscountPaisa > pricing.ExtraLimitPaisa',
                   'input.ExtraDiscountPaisa > Math.Max(0, preExtraTotal - costTotal)',
                   'totalPaisa = preExtraTotal - input.ExtraDiscountPaisa;',
                   'pre_extra_total_paisa, extra_discount_paisa', '"sale_extra_discount"']:
        if needle not in core: fail("Counter phase 3: missing sale-level financial guard " + needle)
    if core.index('var preExtraTotal = totalPaisa;') < core.index('Spread(lines, taxPaisa + roundPaisa - redeem.Paisa, totalPaisa);'):
        fail("Counter phase 3: Extra Discount must be AFTER normal line allocation")
    for call in re.findall(r'Spread\([^;]+;', core):
        if 'ExtraDiscount' in call: fail("Counter phase 3: never spread Extra Discount into products")
    if 'UPDATE sale_items' in migration or 'UPDATE sales' in migration:
        fail("Counter phase 3: additive migration must preserve historical records, not fabricate snapshots")
    for needle in ['extra_limit_paisa', 'extra_discount INTEGER NOT NULL DEFAULT 0', 'pre_extra_total_paisa INTEGER NULL']:
        if needle not in migration: fail("Counter phase 3: missing safe schema default " + needle)
    for needle in ['value.ExtraLimitPaisa < 0', 'extra_limit_paisa=excluded.extra_limit_paisa']:
        if needle not in policy: fail("Counter phase 3: owner allowance must validate and persist " + needle)
    for needle in ['input.ExtraDiscountPaisa = Money.ToPaisa(rupees)', '_appliedExtraDiscount = input.ExtraDiscountPaisa',
                   '_appliedExtraDiscount = input.ExtraDiscountPaisa;', '_appliedExtraDiscount = cart.ExtraDiscountPaisa;',
                   'Money.ToPaisa(extraEntry) != _appliedExtraDiscount', 'MessageBox.Show(owner,']:
        if needle not in vm: fail("Counter phase 3: missing absolute apply/restore/confirmation wiring " + needle)
    if '_appliedExtraDiscount +=' in vm: fail("Counter phase 3: Apply must replace, never accumulate")
    for needle in ['Before Extra Discount', 'Pair("Extra Discount",', 'sale.ExtraDiscountPaisa > 0']:
        if needle not in receipt: fail("Counter phase 3: receipt must expose separate final allowance " + needle)
    for needle in ['sale.TotalPaisa - sale.NormalRefundedPaisa - sale.ProvisionalRefundedPaisa',
                   'for (var index = lines.Count - 1;', 'Extra Discount and earlier refunds limit this bill',
                   'sale.PaidPaisa - sale.CashRefundedPaisa']:
        if needle not in returns: fail("Counter phase 3: missing approved policy-B refund/collection guard " + needle)
    if 'SUM(si.line_total_paisa), 0) - s.extra_discount_paisa' not in book:
        fail("Counter phase 3: BookCheck must reconcile unchanged items minus extra")
    if 'Bill Extra Discount (not assigned to products)' not in report:
        fail("Counter phase 3: category report must show separate unallocated allowance")
    for needle in ['ReturnPolicyBFirst560Then540AndNoThirdRefund', 'SaleItemRowsAreNotReducedByExtra',
                   'ExtraCannotTakeFinalBillBelowActualCostEvenForOwner', 'RevokedPermissionBetweenQuoteAndPostIsRechecked',
                   'TooLittleCashIsNotAutomaticallyForgiven', 'ReportsProfitAndCategoriesReconcileWithoutProductAllocation']:
        if needle not in tests: fail("Counter phase 3: missing named runtime regression " + needle)
    REPORT.append("Counter phase 3: separate absolute sale allowance after points/rounding, per-person owner-set cap, immutable product amounts, receipt/report/book reconciliation and policy-B refund guards (Windows tests pending)")


def check_counter_checkout() -> None:
    """Desktop has no test reference: pin the bounded layout, shortcut and post-commit modal."""
    import xml.etree.ElementTree as ET
    root = ET.parse(DOTNET / "Area11.Desktop/Views/PosView.xaml").getroot()
    ns = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
    xname = "{http://schemas.microsoft.com/winfx/2006/xaml}Name"
    parents = {child: parent for parent in root.iter() for child in parent}
    named = {node.get(xname): node for node in root.iter() if node.get(xname)}
    footer = named.get("CheckoutFooter")
    button = named.get("CompleteSaleButton")
    if footer is None or button is None:
        fail("Counter checkout: fixed footer and completion button must be named")
    else:
        ancestors = []
        node = button
        while node in parents:
            node = parents[node]
            ancestors.append(node)
        if footer not in ancestors or any(node.tag == ns + "ScrollViewer" for node in ancestors):
            fail("Counter checkout: Complete sale must remain in the footer, outside scrolling content")
        rows = parents[footer].find(ns + "Grid.RowDefinitions")
        if rows is None or [row.get("Height") for row in rows] != ["Auto", "Auto", "*", "Auto"]:
            fail("Counter checkout: the trial workspace must be a bounded star row, not overflowing Auto content")
        if footer.get("Grid.Row") != "3" or button.get("Command") != "{Binding CompleteCommand}":
            fail("Counter checkout: the fixed footer must invoke the existing sale command")
        for binding in ["{Binding TotalText}", "{Binding ChangeText}", "{Binding TenderedText, UpdateSourceTrigger=PropertyChanged}", "{Binding StatusMessage}"]:
            if not any(node.get("Text") == binding for node in footer.iter()):
                fail("Counter checkout: missing fixed-footer money/status field: " + binding)
    vm = (DOTNET / "Area11.Desktop/ViewModels/PosViewModel.cs").read_text(encoding="utf-8-sig")
    view = (DOTNET / "Area11.Desktop/Views/PosView.xaml.cs").read_text(encoding="utf-8-sig")
    if root.get("PreviewKeyDown") != "OnCounterPreviewKeyDown":
        fail("Counter checkout: connect the F4 preview handler")
    for needle in ["Key.F4", "e.IsRepeat", "e.Handled = true", "CartGrid.CommitEdit", "viewModel.CompleteCommand.Execute(null)"]:
        if needle not in view: fail("Counter checkout: missing F4 safeguard: " + needle)
    for needle in ["if (_isCompletingSale) return;", "finally", "_isCompletingSale = false;", 'GetBool("sale.successPopup", true)', "var printOk = !AutoPrint || Print(_lastSaleId);", "SaleSuccessDoor.Show(_services, sale, printStatus)"]:
        if needle not in vm: fail("Counter checkout: missing completion safeguard: " + needle)
    if vm.index("SaleSuccessDoor.Show") < vm.index("var sale = result.Value;"):
        fail("Counter checkout: success modal must follow a successful save")
    door = (DOTNET / "Area11.Desktop/SaleSuccessDoor.cs").read_text(encoding="utf-8-sig")
    if "window.Owner = owner" not in door or "window.ShowDialog();" not in door:
        fail("Counter checkout: success must be an owned modal window")
    popup = ET.parse(DOTNET / "Area11.Desktop/Views/SaleSuccessWindow.xaml").getroot()
    for binding in ["{Binding TotalText}", "{Binding CashReceivedText}", "{Binding ChangeText}"]:
        if not any(node.get("Text") == binding for node in popup.iter()):
            fail("Counter checkout: missing committed success amount: " + binding)
    REPORT.append("Counter checkout: fixed non-scrolling footer, bounded cart/options, F4 command, guarded post-save modal")


def check_counter_availability() -> None:
    """Counter popup wiring cannot be unit-tested by the Core-only test project."""
    vm = (DOTNET / "Area11.Desktop/ViewModels/PosViewModel.cs").read_text(encoding="utf-8-sig")
    core = (DOTNET / "Area11.Core/Services/SaleService.cs").read_text(encoding="utf-8-sig")
    for needle in ["_services.Sales.CounterWarnings(input, _user)", "&& !_availabilityWarningShown",
                   '_availabilityWarningShown = true;', '"Counter availability"',
                   'MessageBoxButton.OK, MessageBoxImage.Warning']:
        if needle not in vm:
            fail("Counter availability: missing popup-once wiring: " + needle)
    if vm.index('_availabilityWarningShown = true;') > vm.index('MessageBox.Show(string.Join'):
        fail("Counter availability: set the once flag before the modal opens")
    if len(re.findall(r'_availabilityWarningShown = false;\s*Cart.Clear\(\);', vm)) != vm.count('Cart.Clear();'):
        fail("Counter availability: each new/cleared/restored cart must reset the popup")
    if 'tierPrice <= 0 && lineInput.UnitPricePaisa <= 0' not in core:
        fail("Counter availability: an owner/manager rate must work without a catalog rate")
    for name in ['ReceiptService.cs', 'ReturnReceiptService.cs']:
        text = (DOTNET / ('Area11.Core/Services/' + name)).read_text(encoding="utf-8-sig")
        if 'CounterWarnings' in text or 'Counter availability' in text:
            fail("Counter availability: warnings must never enter receipts")
    REPORT.append("Counter availability: popup once per bill, reset on cart boundaries, no receipt wiring")


def check_template_conflict() -> None:
    """No list control sets both DisplayMemberPath and ItemTemplate.

    WPF refuses that combination with an InvalidOperationException while the page is being
    drawn, which the shop reads as "Purchase does not open". The Purchases picker died exactly
    this way (owner screenshot, 12 Nov 2026): the photo template was added without removing the
    old DisplayMemberPath. The template alone shows the label, so the path must stay away.
    """
    for path in sorted((DOTNET / "Area11.Desktop/Views").glob("*.xaml")):
        lines = path.read_text(encoding="utf-8-sig").split("\n")
        index = 0
        while index < len(lines):
            match = re.match(r"\s*<(ComboBox|ListBox|ListView|ItemsControl)\b", lines[index])
            if not match:
                index += 1
                continue
            tag = match.group(1)
            start = index
            chunk = []
            while index < len(lines):
                chunk.append(lines[index])
                if "/>" in lines[index]:
                    break
                if re.match(r"\s*</" + tag + r"\s*>", lines[index]):
                    break
                index += 1
            text = "\n".join(chunk)
            if "DisplayMemberPath" in text and "ItemTemplate" in text:
                fail(f"{rel(path)}:{start + 1}: a {tag} sets both DisplayMemberPath and "
                     "ItemTemplate - WPF refuses that pair and the page dies on open (v0.71.0)")
            index += 1

    REPORT.append("Template conflict: no list control sets DisplayMemberPath together with "
                  "ItemTemplate, so no page can die on open this way")


# ---------------------------------------------------------------------------
def check_docs() -> None:
    docs = [ROOT / "README.md", ROOT / "HANDOFF.md", ROOT / "AGENTS.md", ROOT / "dotnet/README.md"]
    docs += sorted((ROOT / "docs").glob("*.md"))

    broken = 0
    for doc in docs:
        if not doc.exists():
            fail(f"documentation file missing: {rel(doc)}")
            continue

        text = doc.read_text(encoding="utf-8")
        for target in re.findall(r"\[[^\]]+\]\(([^)]+)\)", text):
            if target.startswith(("http://", "https://", "#", "mailto:")):
                continue
            target = target.split("#")[0].strip()
            if not target:
                continue
            if not (doc.parent / target).exists():
                broken += 1
                fail(f"{rel(doc)} links to a missing file: {target}")

    REPORT.append(f"Docs: {len(docs)} files, {broken} broken links")


# ---------------------------------------------------------------------------
# 7. The double-click scripts have no compiler either
# ---------------------------------------------------------------------------
def check_scripts() -> None:
    """Every .bat / .cmd must be able to run: closed blocks, real labels, safe echos.

    Why: a batch file that is wrong does not fail to start - cmd.exe reads it line by line and
    does something else. A missing `)` swallows the rest of the file, `goto :typo` jumps into
    nothing, and an unescaped `)` inside `echo ...` **closes the block early**, so the shop owner
    double-clicks a launcher and sees a menu that stops halfway with a message nobody wrote.
    There is no compiler for these files, so this is their compiler. It is the same reason the
    error-line extraction lives in the launcher: the tooling is now the first thing he runs.

    It is deliberately conservative - it only counts parentheses that cmd itself would count:
    comments (`rem`) and echoed text are display, single-quoted `for` bodies are left alone, and
    `^( ... ^)` is the escape that means "print a bracket".
    """
    scripts = sorted(list(ROOT.glob("*.bat")) + list(ROOT.glob("*.cmd"))
                     + list((ROOT / "tools").glob("*.bat")) + list((ROOT / "tools").glob("*.cmd")))

    problems = 0

    for script in scripts:
        text = script.read_text(encoding="utf-8", errors="replace")
        depth = 0
        labels: dict[str, list[int]] = {}
        gotos: list[tuple[str, int]] = []

        for number, raw in enumerate(text.splitlines(), 1):
            line = raw.rstrip()
            stripped = line.strip()
            low = stripped.lower()

            if not stripped or low == "rem" or low.startswith("rem "):
                continue

            label = re.fullmatch(r":(\w+)", stripped)

            if label:
                labels.setdefault(label.group(1), []).append(number)
                continue

            for target in re.findall(r"\bgoto\s+:?(\w+)", low):
                if target not in ("eof", "eof ".strip()):
                    gotos.append((target, number))

            for target in re.findall(r"\bcall\s+:(\w+)", low):
                gotos.append((target, number))

            # Quoted text is data (a path, a message), never a bracket the parser counts.
            clean = re.sub(r'"[^"]*"', '""', line)
            clean = clean.replace("^(", "").replace("^)", "")

            if low.startswith("echo"):
                # An unescaped bracket here ends the enclosing block early - the classic
                # "the script printed half a menu and stopped" fault.
                if depth > 0 and re.search(r"(?<!\^)[()]", clean[4:]):
                    problems += 1
                    fail(f"{rel(script)}:{number}: an unescaped bracket inside `echo` inside a "
                         f"block - it closes the block early (write ^( and ^))")

                continue

            depth += clean.count("(") - clean.count(")")

            if depth < 0:
                problems += 1
                fail(f"{rel(script)}:{number}: a closing bracket with no opening - "
                     f"cmd.exe would stop reading the block here")
                depth = 0

        if depth != 0:
            problems += 1
            fail(f"{rel(script)}: {depth} opened block(s) never closed - cmd.exe reads the rest "
                 f"of the file as part of the block")

        for name, lines in labels.items():
            if len(lines) > 1:
                problems += 1
                fail(f"{rel(script)}: the label :{name} appears {len(lines)} times "
                     f"(lines {lines}) - a jump always lands on the first one")

        for name, number in gotos:
            if name not in labels:
                problems += 1
                fail(f"{rel(script)}:{number}: jumps to :{name}, which does not exist - "
                     f"the script dies with no message")

    REPORT.append(f"Scripts: {len(scripts)} .bat/.cmd files, blocks closed, every goto lands, "
                  f"{problems} problem(s)")


# ---------------------------------------------------------------------------
# 9. The C# the compiler will actually read: a real grammar, and the names it refers to
# ---------------------------------------------------------------------------
def _grammar():
    """The C# grammar, or None when the package is not installed in this environment.

    Why a real parser at all: `check_csharp` counts braces, and brace counting cannot see a
    misplaced `else`, a broken lambda or a `switch` arm without its arrow - the exact faults a
    first compile reports. tree-sitter-c-sharp is a full grammar, so an ERROR node means the file
    is not valid C#. It is still not a compiler: it knows nothing about types.
    """
    try:
        import tree_sitter_c_sharp  # noqa: F401  (imported for the language capsule)
        from tree_sitter import Language, Parser
    except Exception:
        return None

    return Parser(Language(tree_sitter_c_sharp.language()))


def _cs_files() -> list[pathlib.Path]:
    return [p for p in DOTNET.rglob("*.cs") if not ({"obj", "bin"} & set(p.parts))]


def _node_text(source: bytes, node) -> str:
    return source[node.start_byte:node.end_byte].decode("utf-8", errors="replace")


_TYPE_NODES = {"class_declaration", "struct_declaration", "interface_declaration",
               "record_declaration", "record_struct_declaration", "enum_declaration"}


def _member_name(source: bytes, node) -> str | None:
    """The identifier that NAMES a member - never the one that names its return type.

    `public static StoragePlan For(...)` has two identifiers; picking the first one records a
    method called "StoragePlan" and loses "For". That mistake was made once here and the check
    then reported real methods as missing, which is worse than no check at all.
    """
    kids = node.children

    for anchor in ("parameter_list", "accessor_list", "arrow_expression_clause", "initializer_expression", ";"):
        for index, child in enumerate(kids):
            if child.type != anchor:
                continue

            for back in reversed(kids[:index]):
                if back.type == "identifier":
                    return _node_text(source, back)

                if back.type in ("predefined_type", "generic_name", "qualified_name"):
                    return None

    return None


def _parameters(source: bytes, node) -> tuple[int, int, bool]:
    """(required, total, variadic) for a parameter list."""
    plist = next((c for c in node.children if c.type == "parameter_list"), None)

    if plist is None:
        return 0, 0, False

    required = 0
    total = 0
    variadic = False

    for parameter in plist.children:
        if parameter.type != "parameter":
            continue

        total += 1
        body = _node_text(source, parameter)

        # `params string[] warnings` may be called with nothing at all, and a parameter with a
        # default (`stepRupees = 10`) is optional. Both were counted as required in the first
        # draft of this rule, which then invented 580 faults on a healthy tree.
        if "params " in body:
            variadic = True
            continue

        if "=" in body:
            continue

        required += 1

    return required, total, variadic


def check_csharp_grammar() -> None:
    """Every C# file must parse, and every `Type.Member` must be a member that type declares.

    Why this check exists: the Desktop project has never been compiled, and this machine has no
    .NET. Two classes of first-build error are now caught here instead:

      * **a file that is not valid C#** - the grammar reports an ERROR node with a line number,
        where brace counting only noticed "the braces balance";
      * **a name that does not exist** - `Calc.Round(...)`, `Money.Format(...)`,
        `AppPaths.DatabaseFile`, `Modules.Pos`: the method is named on a type that this project
        declares, so the member can be looked up. Wrong name (CS0117/CS1061) and wrong argument
        count (CS7036) are both reported. Before this rule the program's own `Calc`,
        `Money`, `Clock`, `Expiry` and `AppPaths` calls were unchecked.

    What it deliberately does NOT do: it only looks at a bare identifier that names exactly ONE
    top-level type in this repository, and skips any file where that name is also declared as a
    property, field, parameter or local (a `Modules` collection shadows the `Modules` class).
    Instance calls (`db.Query<T>(...)`) are out of reach - knowing the type of `db` needs a real
    compiler, and guessing types is how a checker starts reporting faults that are not there.
    """
    parser = _grammar()

    if parser is None:
        warn("tree-sitter-c-sharp is not installed, so the real-grammar pass was skipped "
             "(pip install tree_sitter tree_sitter_c_sharp). The brace check still ran.")
        REPORT.append("C# grammar: not installed - skipped")

        # The static-member pass is a separate gate and it still has to be counted, even when it had
        # nothing to read: a check count that changes with what is installed on the machine is how a
        # future agent wastes an hour looking for a gate that "disappeared" (it happened on
        # 23 Oct 2026: the count read 40 instead of 41, and the missing number was this package).
        REPORT.append("Static members: skipped - the grammar needed to read them is not installed")
        return

    files = _cs_files()
    broken = 0

    for path in files:
        source = path.read_bytes()
        tree = parser.parse(source)
        node = tree.root_node

        if not node.has_error:
            continue

        # The FIRST error is the one to read; the rest are usually the grammar recovering.
        stack = [node]
        first = None

        while stack:
            current = stack.pop()

            if current.type == "ERROR" or current.is_missing:
                first = current
                break

            stack.extend(current.children)

        line = (first.start_point[0] + 1) if first is not None else 0
        broken += 1
        fail(f"{rel(path)}:{line}: not valid C# - the grammar stopped here")

    # ---- the type map: every top-level declaration, with its methods and members ----
    types: dict[str, dict] = {}

    def entry(name: str) -> dict:
        return types.setdefault(name, {"files": set(), "methods": {}, "members": set(),
                                       "bases": set(), "nested": False})

    for path in files:
        source = path.read_bytes()
        tree = parser.parse(source)
        stack: list[tuple[object, str | None]] = [(tree.root_node, None)]

        while stack:
            current, outer = stack.pop()

            for child in current.children:
                if child.type not in _TYPE_NODES:
                    stack.append((child, outer))
                    continue

                name_node = next((c for c in child.children if c.type == "identifier"), None)

                if name_node is None:
                    continue

                name = _node_text(source, name_node)
                info = entry(name)
                info["files"].add(str(path))

                if outer is not None:
                    info["nested"] = True

                if outer is None:
                    body = next((c for c in child.children
                                 if c.type in ("declaration_list", "enum_member_declaration_list")), None)

                    if body is not None:
                        for member in body.children:
                            if member.type == "method_declaration":
                                shorthand = _member_name(source, member)

                                if shorthand:
                                    info["methods"].setdefault(shorthand, set()).add(
                                        _parameters(source, member))
                            elif member.type in ("property_declaration", "event_declaration",
                                                 "indexer_declaration"):
                                shorthand = _member_name(source, member)

                                if shorthand:
                                    info["members"].add(shorthand)
                            elif member.type == "enum_member_declaration":
                                identifier = next((c for c in member.children
                                                   if c.type == "identifier"), None)

                                if identifier is not None:
                                    info["members"].add(_node_text(source, identifier))
                            elif member.type in ("field_declaration", "event_field_declaration",
                                                 "const_declaration"):
                                for part in member.named_children:
                                    holders = [part] if part.type == "variable_declarator" else [
                                        d for d in part.children if d.type == "variable_declarator"]

                                    for holder in holders:
                                        identifier = next((c for c in holder.children
                                                           if c.type == "identifier"), None)

                                        if identifier is not None:
                                            info["members"].add(_node_text(source, identifier))

                for part in child.children:
                    if part.type == "base_list":
                        for base in part.children:
                            if base.type in ("identifier", "qualified_name", "generic_name"):
                                info["bases"].add(
                                    _node_text(source, base).split("<")[0].split(".")[-1])

                stack.append((child, name))

    usable = {name for name, info in types.items()
              if not info["nested"] and len(info["files"]) == 1}

    def family(name: str) -> list[str]:
        """The type and its bases, inside this repository only."""
        found: list[str] = []
        todo = [name]

        while todo:
            current = todo.pop()

            if current in found or current not in types:
                continue

            found.append(current)
            todo.extend(types[current]["bases"])

        return found

    calls = 0
    names = 0
    problems = 0

    for path in files:
        source = path.read_bytes()
        tree = parser.parse(source)

        # A name that is also a property, field, parameter or local in THIS file is that thing,
        # not the type with the same name - `Category.Key` where `Category` is a property.
        shadow: set[str] = set()
        stack = [tree.root_node]

        while stack:
            current = stack.pop()

            if current.type == "property_declaration":
                shorthand = _member_name(source, current)

                if shorthand:
                    shadow.add(shorthand)
            elif current.type in ("variable_declarator", "parameter"):
                identifier = next((c for c in current.children if c.type == "identifier"), None)

                if identifier is not None:
                    shadow.add(_node_text(source, identifier))

            stack.extend(current.children)

        stack = [tree.root_node]

        while stack:
            node = stack.pop()

            if node.type == "invocation_expression":
                function = node.children[0] if node.children else None

                if (function is not None and function.type == "member_access_expression"
                        and function.children[0].type == "identifier"):
                    owner = _node_text(source, function.children[0])

                    if owner in usable and owner not in shadow and function.children[-1].type == "identifier":
                        member = _node_text(source, function.children[-1])
                        arguments = next((c for c in node.children if c.type == "argument_list"), None)
                        count = sum(1 for c in arguments.children if c.type == "argument") if arguments else 0
                        calls += 1

                        known = False
                        arity = False

                        for one in family(owner):
                            for required, total, variadic in types[one]["methods"].get(member, set()):
                                known = True
                                arity = arity or (count >= required if variadic else required <= count <= total)

                        if not known:
                            if member in {m for one in family(owner) for m in types[one]["members"]}:
                                continue

                            problems += 1
                            fail(f"{rel(path)}:{node.start_point[0] + 1}: {owner}.{member}() does not exist "
                                 f"on the type this project declares")
                        elif not arity:
                            problems += 1
                            fail(f"{rel(path)}:{node.start_point[0] + 1}: {owner}.{member}() is called with "
                                 f"{count} argument(s), which the declaration does not accept")
            elif node.type == "member_access_expression" and node.children[0].type == "identifier":
                owner = _node_text(source, node.children[0])

                if owner in usable and owner not in shadow and node.children[-1].type == "identifier":
                    member = _node_text(source, node.children[-1])
                    names += 1

                    if (member not in {m for one in family(owner) for m in types[one]["members"]}
                            and member not in {m for one in family(owner) for m in types[one]["methods"]}):
                        problems += 1
                        fail(f"{rel(path)}:{node.start_point[0] + 1}: {owner}.{member} does not exist "
                             f"on the type this project declares")

            stack.extend(node.children)

    if broken == 0:
        REPORT.append(f"C# grammar: {len(files)} file(s) parsed as real C#, no error nodes")

    REPORT.append(f"Static members: {calls} call(s) and {names} member reference(s) resolved, "
                  f"{problems} problem(s)")


# ---------------------------------------------------------------------------
def rel(path: pathlib.Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def check_owner_boundary_review():
    """Narrow source contracts, NOT executed authorization/security tests."""
    core = DOTNET / 'Area11.Core' / 'Services'
    def need(text, markers, label):
        for marker in markers:
            if marker not in text:
                FAILURES.append(f'Phase4 {label}: missing {marker!r}')
    owner = (core / 'OwnerAccess.cs').read_text()
    need(owner, ['SELECT id, name, role, active FROM users WHERE id = @id;',
                 'IsOwner: true, Active: true', 'throw new ValidationException'], 'live owner')
    auth = (core / 'AuthService.cs').read_text()
    permission = (core / 'PermissionService.cs').read_text()
    # Count the write region only: owner-only roster reads now use the same current-owner helper.
    auth_writes = auth.split('public ServiceResult<int> CreateStaff(', 1)[1]
    if auth_writes.count('OwnerAccess.Require(_db, connection, actor)') != 3:
        FAILURES.append('Phase4 Auth create/reset/activation must each validate owner in write transaction')
    if permission.count('OwnerAccess.Require(_db, connection, actor)') != 2:
        FAILURES.append('Phase4 Permission Set/Reset must validate current owner')
    need(auth, ['{otherColumn} = NULL', 'if (target is null)', 'target.IsOwner && !active',
                '_settings.SaveRecoveryHash(actor, stored)'], 'staff credentials')
    settings = (core / 'SettingsService.cs').read_text()
    need(settings, ['values.ContainsKey(AuthService.RecoveryWordKey)',
                    'key == AuthService.RecoveryWordKey', 'key == FocusGuardService.GuardKey', 'internal ServiceResult SaveRecoveryHash',
                    'if (ownerOnly) OwnerAccess.Require(_db, connection, actor);'], 'secret settings')
    recovery = settings.split('internal ServiceResult SaveRecoveryHash', 1)[-1].split('private bool IsCurrentOwner', 1)[0]
    need(recovery, ['OwnerAccess.Require', '_audit.Record(connection, owner, "Auth", "recovery_word_changed"',
                    '_cache[AuthService.RecoveryWordKey] = json'], 'atomic recovery')
    if recovery.find('_cache[') < recovery.find('_audit.Record('):
        FAILURES.append('Phase4 recovery cache must not precede transaction audit')
    photo = (core / 'PhotoService.cs').read_text()
    need(photo, ['Path.IsPathRooted(fileName)', 'fileName.Contains("..", StringComparison.Ordinal)',
                 'row.FullPath = CoverPath(row.FileName) ?? string.Empty;',
                 'CoverPath(selected.FileName) is null', 'var path = CoverPath(file);',
                 'var path = CoverPath(row.FileName);'], 'shared photo filename boundary')
    if 'Path.Combine(PhotosFolder, row.FileName)' in photo or 'Path.Combine(PhotosFolder, selected.FileName)' in photo:
        FAILURES.append('Phase4 stored gallery/cover names bypass safe resolver')
    boundary = (DOTNET / 'Area11.Tests' / 'OwnerBoundaryReviewTests.cs').read_text()
    need(boundary, ['RecoveryAuditFailureRollsBackHashAndCache', 'SecretResetAuditFailureRestoresOldCredential',
                    'PasswordResetInvalidatesAnOldPinToo', 'PinResetInvalidatesAnOldPasswordToo',
                    'ForgedOwnerCannotGrantAnyModule', 'ProtectedOwnerSettingsRejectForgedOwnerAtomically'], 'regressions')
    need((DOTNET / 'Area11.Tests' / 'PhotoTests.cs').read_text(),
         ['ThumbnailsNeverResolveAnExistingFileOutsidePhotoFolder', 'GalleryRowsCannotCarryAnOutsideFullPath',
          'BulkCoversIgnoreUnsafeStoredNames', 'ChoosingCoverCannotBlessAnOutsideFile',
          'StaffAvatarRejectsDriveRelativeAndNetworkNames'], 'photo regressions')
    doc = (ROOT / 'docs/POS-PHASE4-EVIDENCE.md').read_text()
    shell = (DOTNET / 'Area11.Desktop/ViewModels/ShellViewModel.cs').read_text()
    routes = set(re.findall(r'AddPage\("\w+",\s*"(\w+)"', shell)) | {'dashboard'}
    inventoried = set(re.findall(r'^\| ([a-zA-Z]+) \|', doc, re.M)) - {'Route', 'Lesson'}
    if routes != inventoried:
        FAILURES.append(f'Phase4 route evidence drift: missing {routes-inventoried}, extra {inventoried-routes}')
    lessons = set(re.findall(r'^\| (\d{2}) \|', doc, re.M))
    if lessons != {f'{n:02}' for n in range(1, 37)}:
        FAILURES.append('Phase4 evidence must map all36 guide lessons')
    # Top-level contents, not nested Built-in/Extra field TabItems.
    ns = {'w': 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'}
    view = ET.parse(DOTNET / 'Area11.Desktop/Views/SettingsView.xaml').getroot()
    tabs = view.findall('./w:Grid/w:TabControl/w:TabItem', ns)
    if len(tabs) != 8 or not all(f'| {n} ' in doc for n in range(8)):
        FAILURES.append('Phase4 Settings inventory drift: review all top-level contents')
    need(doc, ['IN PROGRESS', 'not a full audit PASS', 'filename containment', 'Windows'], 'evidence honesty')
    REPORT.append('Phase4 portion1: bounded live-owner/alternate-secret/recovery/photo contracts and 26-route/eight-Settings/36-lesson inventory present; full audit and Windows runtime pending')


def check_phase_four_access_projections():
    """Bounded source contracts only. In particular, these cannot prove live session authentication."""
    core = DOTNET / 'Area11.Core/Services'
    desktop = DOTNET / 'Area11.Desktop'
    shell = (desktop / 'ViewModels/ShellViewModel.cs').read_text()
    vm = (desktop / 'ViewModels/SettingsViewModel.cs').read_text()
    ns = {'w': 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'}
    tabs = ET.parse(desktop / 'Views/SettingsView.xaml').getroot().findall('./w:Grid/w:TabControl/w:TabItem', ns)
    indices = [int(n) for n in re.findall(r'SettingsTabItems.Add\(new SettingsTabItem\((\d+),', shell)]
    if indices != list(range(len(tabs))):
        FAILURES.append('Phase4 portion2: every Settings content needs its matching shell section, including identity0 and Fields7')
    clamp = re.search(r'Math.Clamp\(value, 0, (\d+)\)', vm)
    if clamp is None or int(clamp[1]) != len(tabs) - 1:
        FAILURES.append('Phase4 portion2: Settings selected-index clamp loses an existing tab')
    if 'SettingsTabItems.Add(new SettingsTabItem(0, "App name & icon"))' not in shell:
        FAILURES.append('Phase4 portion2: identity section0 label/address missing')
    native = (core / 'NativeFieldPolicyService.cs').read_text()
    if native.count('OwnerAccess.Require(_db, connection, user);') != 2:
        FAILURES.append('Phase4 native-field read AND write paths need current-owner validation')
    for token in ('OwnerAccess.Require(_db, connection, user);', 'by = overriding ? owner.Name.Trim() : null',
                  '_audit.Record(connection, owner, Modules.Settings, "native_field_required_changed"'):
        if token not in native:
            FAILURES.append(f'Phase4 native-field boundary missing {token}')
    write = native.split('public ServiceResult SetRequired', 1)[-1]
    if not (write.find('OwnerAccess.Require') >= 0 and write.find('OwnerAccess.Require') < write.find('if (field.Required == required)')):
        FAILURES.append('Phase4 native-field current owner check must precede no-op success inside transaction')
    reps = (core / 'RepService.cs').read_text()
    if 'MaskProfit(rows, ProfitPolicy.CanSee(_permissions, _settings, reader));' not in reps:
        FAILURES.append('Phase4 Doctors must respect the same global-plus-person profit policy')
    history = (core / 'ProductHistoryService.cs').read_text()
    if 'if (!history.CanSeeProfit)\n                foreach (var row in history.Sales) row.ProfitPaisa = 0;' not in history:
        FAILURES.append('Phase4 ProductHistory.Load must mask raw denied profit, not only UI/CSV')
    tests = (DOTNET / 'Area11.Tests/PhaseFourAccessProjectionTests.cs').read_text()
    for name in ('NativeFieldNoOpDoesNotBypassCurrentOwnerCheck', 'NativeFieldAcknowledgementUsesCurrentStoredOwnerName',
                 'DoctorProfitGrantDoesNotOverrideGlobalHideSwitch', 'ProductHistoryDoesNotReturnHiddenRawProfitToManager',
                 'ProductHistoryMasksOwnerRawProfitWhenGlobalOffAndRestoresFromSnapshots'):
        if name not in tests:
            FAILURES.append(f'Phase4 access projection regression missing {name}')
    REPORT.append('Phase4 portion2: all eight Settings indices/clamp, live-owner native fields, Doctors global profit switch and raw history-profit masking contracts; Windows/runtime/session and remaining access matrix pending')


def check_ledger_read_boundaries():
    """Narrow source contracts; not a compiler or authentication/transaction-revocation proof."""
    core = DOTNET / 'Area11.Core/Services'
    access = (core / 'LedgerReadAccess.cs').read_text()
    for token in ('actor is not { Active: true }', 'SELECT id, name, role, active FROM users WHERE id = @id;',
                  'current is not { Active: true }', 'current.Kind != actor.Kind',
                  'permissions.Can(current, module)'):
        if token not in access:
            FAILURES.append(f'Ledger reader current-account boundary missing {token}')
    for stem, module, direct in (
        ('CustomerLedgerService', 'Customers', ('List', 'Page', 'PaymentReceipt')),
        ('SupplierLedgerService', 'Suppliers', ('List', 'Page', 'ExpiredBatches')),
    ):
        text = (core / f'{stem}.cs').read_text()
        methods = re.findall(r'public ServiceResult<[^\n]+?>\s+(List|Page|Statement|BuildBulkStatements|ExpiredBatches)\(([^)]*)\)', text)
        for name, args in methods:
            if not args.startswith('User? user,'):
                FAILURES.append(f'{stem}.{name} retains an actorless ledger read overload')
        for name in direct:
            match = re.search(r'public ServiceResult<[^\n]+?>\s+' + name + r'\(', text)
            if match is None:
                FAILURES.append(f'{stem}.{name} missing')
                continue
            body = text[match.end():].split('\n    public ', 1)[0]
            token = f'LedgerReadAccess.Require(_db, connection, _permissions, user, Modules.{module});'
            if token not in body:
                FAILURES.append(f'{stem}.{name} bypasses live-reader authorization')
        for token in ('Page(user, id)', 'Statement(user, id)', f'Page(user, {"customerId" if module == "Customers" else "supplierId"})'):
            if token not in text:
                FAILURES.append(f'{stem} print/bulk must propagate caller: {token}')
    customer = (core / 'CustomerLedgerService.cs').read_text()
    for token in ('row.ProfitKnown = canSeeProfit;', 'if (!canSeeProfit) row.ProfitPaisa = 0;',
                  'summary.ProfitKnown = ProfitPolicy.CanSee(_permissions, _settings, reader);',
                  'if (!summary.ProfitKnown) summary.ProfitPaisa = 0;',
                  'Statement(User? user, int customerId) => Statement(user, customerId, null);'):
        if token not in customer:
            FAILURES.append(f'Customer ledger caller/profit contract missing {token}')
    for stem in ('Customers', 'Suppliers'):
        vm = (DOTNET / f'Area11.Desktop/ViewModels/{stem}ViewModel.cs').read_text()
        for method, first in re.findall(r'_services\.(?:Ledger|SupplierLedger)\.(List|Page|Statement|BuildBulkStatements|ExpiredBatches)\(\s*([^,)]+)', vm):
            if first.strip() != '_user':
                FAILURES.append(f'{stem} UI {method} does not pass actual _user')
    tests = (DOTNET / 'Area11.Tests/LedgerReadBoundaryTests.cs').read_text()
    for name in ('AnonymousCustomerReadsIncludingMonthlyAndBulkRefuse', 'AnonymousSupplierReadsIncludingExpiredAndBulkRefuse',
                 'ForgedOwnerRoleOnCashierIdCannotReadEitherLedger', 'GlobalSwitchMasksOwnerListAndPageThenRestoresWithoutReposting',
                 'PaymentReceiptChecksCurrentAccountBeforeReturningExistingPayment', 'SupplierGrantRevocationAffectsNextReadOnSameService'):
        if name not in tests:
            FAILURES.append(f'Ledger read regression missing {name}')
    REPORT.append('Phase4 portion3: required ledger reader on list/page/statement/month/bulk/expired paths, current active account/module gates, customer raw-profit masking and UI caller propagation; catalog/cost/session/full audit and C# runtime pending')


def check_product_read_boundaries():
    """Bounded source contracts, deliberately not authentication/runtime proof."""
    core = DOTNET / 'Area11.Core'
    access = (core / 'Services/ProductReadAccess.cs').read_text()
    for token in ('actor is not { Active: true }', 'current is not { Active: true }',
                  'current.Kind != actor.Kind', 'permissions.Can(current, module)',
                  'if (!product.CostKnown) product.CostPaisa = 0;',
                  'product.CostKnown = financialRow && canSeeCost;',
                  'product.MarginKnown = financialRow && canSeeProfit;'):
        if token not in access:
            FAILURES.append(f'Product read/projection boundary missing {token}')
    purposes = {'Catalog': 'Products', 'Counter': 'Pos', 'Purchase': 'Purchases',
                'SupplierReturn': 'Suppliers', 'WriteOff': 'Stock', 'Expense': 'Cash',
                'ProvisionalReturn': 'Returns'}
    for purpose, module in purposes.items():
        if f'ProductReadPurpose.{purpose} => Modules.{module}' not in access:
            FAILURES.append(f'Product workflow {purpose} lost module gate {module}')
    text = (core / 'Services/ProductService.cs').read_text()
    for name in ('Search', 'SearchSmart', 'Get'):
        signatures = re.findall(r'public ServiceResult<[^\n]+?>\s+' + name + r'\(\s*([^)]*)\)', text)
        if len(signatures) != 1 or not signatures[0].startswith('User? user,'):
            FAILURES.append(f'ProductService.{name} must require reader; no actorless overload')
        if name == 'SearchSmart':
            for token in ('Search(user, typed, categoryId, companyId, lowStockOnly, strength, supplierId, purpose)',
                          'Search(user, null, categoryId, companyId, lowStockOnly, strength, supplierId, purpose)'):
                if token not in text:
                    FAILURES.append(f'Product smart search lost projected path: {token}')
        else:
            match = re.search(r'public ServiceResult<[^\n]+?>\s+' + name + r'\(', text)
            body = text[match.end():].split('\n    public ', 1)[0] if match else ''
            for token in ('ProductReadAccess.Require(_db, connection, _permissions, user, purpose)',
                          'ProductReadAccess.Project(product, purpose, canSeeCost, canSeeProfit)'):
                if token not in body:
                    FAILURES.append(f'ProductService.{name} lost {token}')
    product = (core / 'Models/Product.cs').read_text()
    for token in ('CostKnown ? Money.Format(CostPaisa) : "hidden"', '!MarginKnown || RetailPaisa <= 0',
                  'return $"{MarginPercent:0.#}%";'):
        if token not in product:
            FAILURES.append(f'Product cost/margin formatter contract missing {token}')
    report = (core / 'Services/ReportService.cs').read_text()
    for token in ('CostPaisa = canSeeCost ? items.Cost : 0', 'RestockCostPaisa = canSeeCost ? returns.RestockCost : 0',
                  'PurchasesPaisa = canSeeCost ? outflow.Purchases : 0'):
        if token not in report:
            FAILURES.append(f'Report hidden raw cost contract missing {token}')
    vms = DOTNET / 'Area11.Desktop/ViewModels'
    for stem, purpose in (('Products', None), ('Pos', 'Counter'), ('Purchases', 'Purchase'),
                          ('Suppliers', 'SupplierReturn'), ('WriteOff', 'WriteOff'),
                          ('Expenses', 'Expense'), ('Provisional', 'ProvisionalReturn')):
        vm = (vms / f'{stem}ViewModel.cs').read_text()
        calls = re.findall(r'_services\.Products\.(?:Search|SearchSmart|Get)\(([^;]+?)\);', vm)
        if not calls:
            FAILURES.append(f'{stem} product caller missing')
        for args in calls:
            if not args.startswith('_user,') or (purpose and f'purpose: ProductReadPurpose.{purpose}' not in args):
                FAILURES.append(f'{stem} must pass current reader and correct product workflow')
    vm = (vms / 'ProductsViewModel.cs').read_text()
    if '!full.Value.CostKnown' not in vm:
        FAILURES.append('Product edit must not load masked zero cost')
    tests = (DOTNET / 'Area11.Tests/ProductReadBoundaryTests.cs').read_text()
    for name in ('AnonymousCatalogAndWorkflowReadsRefuse', 'ForgedOwnerRoleCannotReadProductsThroughAnotherAccount',
                 'ExactTokenAndFuzzyCounterRowsShareTheSameProjection', 'OperationalCostPreparationNeedsModuleAndSeparateCostGrant',
                 'PurposeGrantRevocationAffectsNextReadOnSameService', 'CostFreeCounterResultDoesNotZeroActualPostedBatchCost',
                 'CostFreeCatalogObjectCannotBypassCoreActualCostFloor', 'ManagerReportDoesNotReturnRawHiddenCostInputs'):
        if name not in tests:
            FAILURES.append(f'Product read regression missing {name}')
    REPORT.append('Phase4 portion4: current reader/module/purpose on product search/smart/get, cost-free Counter/PR, bounded operational cost and margin projection, report hidden raw-cost masking and caller propagation; session/full cost audit and C# runtime pending')


def check_snapshot_read_boundaries():
    """Focused snapshot/export contracts, not authentication or C# runtime proof."""
    core = DOTNET / 'Area11.Core'
    access = (core / 'Services/SnapshotReadAccess.cs').read_text()
    for token in ('actor is not { Active: true }', 'current is not { Active: true }',
                  'current.Kind != actor.Kind', 'permissions.Can(current, module)',
                  'alternateModule is null || !permissions.Can(current, alternateModule)'):
        if token not in access:
            FAILURES.append(f'Snapshot current-reader boundary missing {token}')
    for stem, method, modules in (
            ('ProductHistory', 'Load', 'Modules.Products'), ('Reorder', 'Report', 'Modules.Reorder'),
            ('Dashboard', 'GetSnapshot', 'Modules.Pos, Modules.Reports'),
            ('Report', 'Build', 'Modules.Reports'), ('Report', 'Days', 'Modules.Reports')):
        text = (core / f'Services/{stem}Service.cs').read_text()
        match = re.search(r'public ServiceResult<[^\n]+?>\s+' + method + r'\(', text)
        body = text[match.end():].split('\n    public ', 1)[0] if match else ''
        token = f'SnapshotReadAccess.Require(_db, connection, _permissions, user, {modules});'
        if token not in body:
            FAILURES.append(f'{stem}.{method} lost current-reader/module validation')
        if stem != 'Reorder' and 'ProfitPolicy.CanSee(_permissions, _settings, reader)' not in body:
            FAILURES.append(f'{stem}.{method} must use resolved reader for profit')
        if stem == 'Reorder' and not (0 <= body.find(token) < body.find('if (!_settings.GetBool("reorder.enabled"')):
            FAILURES.append('Disabled reorder must still validate current reader')
    history = (core / 'Services/ProductHistoryService.cs').read_text()
    for token in ('CanSeeCost = CostPolicy.CanSee(_permissions, _settings, reader)', 'row.CostKnown = history.CanSeeCost;',
                  'if (!row.CostKnown)', 'row.RatePaisa = 0;', 'row.LineTotalPaisa = 0;',
                  'history.BoughtQtyLabel + " / " + history.BoughtTotalLabel',
                  'row.CostKnown ? row.RatePaisa.ToString(CultureInfo.InvariantCulture) : "\\\"\\\""',
                  'row.CostKnown ? row.LineTotalPaisa.ToString(CultureInfo.InvariantCulture) : "\\\"\\\""'):
        if token not in history:
            FAILURES.append(f'History purchase-cost projection/export missing {token}')
    if history.count('var loaded = Load(user, productId);') != 2:
        FAILURES.append('History print/CSV preparation must both use current Load boundary')
    models = (core / 'Models/ProductHistoryModels.cs').read_text()
    for token in ('CostKnown ? Money.Format(RatePaisa) : "hidden"',
                  'CostKnown ? Money.Format(LineTotalPaisa) : "hidden"',
                  'CanSeeCost ? Money.Format(BoughtPaisa) : "hidden"'):
        if token not in models:
            FAILURES.append(f'History masked formatter missing {token}')
    reorder = (core / 'Services/ReorderService.cs').read_text()
    for token in ('row.LastCostKnown = canSeeCost;', 'row.LastCostPaisa = row.LastCostKnown ? supply.CostPaisa : 0;'):
        if token not in reorder:
            FAILURES.append(f'Reorder central cost rule missing {token}')
    report = (core / 'Services/ReportService.cs').read_text()
    if re.search(r'public List<ReportDayRow> Days\(', report) or 'Days(ReportRange range, User? user)' not in report:
        FAILURES.append('Public daily rows must require reader and return checked result')
    vm = (DOTNET / 'Area11.Desktop/ViewModels/ProductHistoryViewModel.cs').read_text()
    if 'history.BoughtTotalLabel' not in vm or 'Money.Format(history.BoughtPaisa)' in vm:
        FAILURES.append('History screen summary must distinguish hidden purchase money from zero')
    tests = (DOTNET / 'Area11.Tests/SnapshotReadBoundaryTests.cs').read_text()
    for name in ('AnonymousSnapshotAndHistoryExportReadsRefuse', 'InactiveDatabaseOwnerCannotUseSnapshotOrExportDefaults',
                 'StaffHistoryKeepsPurchaseQuantityButMasksBothCostInputsAndAggregate',
                 'ProfitGrantDoesNotOverrideIndependentHistoricalCostGrant',
                 'HistoryCsvLeavesBothDeniedPurchaseMoneyCellsEmptyButKeepsSales',
                 'HistoryDocumentMasksPurchaseRowsAndSummaryNotOnlyScreenColumns',
                 'DisabledReorderStillChecksCurrentReaderBeforeEarlySuccess',
                 'PublicDaysRequiresReportModuleAndRespectsGlobalProfitToggle',
                 'DashboardKeepsPosOrReportsRatherThanRequiringBoth'):
        if name not in tests:
            FAILURES.append(f'Snapshot boundary regression missing {name}')
    REPORT.append('Phase4 portion5: history/Reorder purchase-cost masks (D-110 now supplies independent grants), masked screen/print/CSV summaries, current reader/module for five snapshot paths, checked public daily rows; wider session/finance/export/Windows audit pending')


def check_definition_time_boundaries():
    """Owner definition writes and one-transaction clock correction; source contracts only."""
    core = DOTNET / 'Area11.Core/Services'
    fields = (core / 'CustomFieldService.cs').read_text()
    for name in ('Add', 'Update', 'SetActive', 'Move'):
        match = re.search(r'public ServiceResult(?:<CustomField>)?\s+' + name + r'\(', fields)
        body = fields[match.end():].split('\n    public ', 1)[0] if match else ''
        tx = body.find('return _db.Tx(connection =>')
        gate = body.find('var owner = OwnerAccess.Require(_db, connection, user);')
        first = min((pos for pos in (body.find('LabelExists(connection'), body.find('ReadField(connection')) if pos >= 0), default=-1)
        if not (0 <= tx < gate < first):
            FAILURES.append(f'CustomField.{name} must validate current owner inside transaction before duplicate/target/no-op checks')
        if '_audit.Record(connection, owner, Modules.Settings' not in body:
            FAILURES.append(f'CustomField.{name} must audit resolved owner')
    for name in ('List', 'Values', 'DescribeValues'):
        match = re.search(r'public ServiceResult<[^\n]+?>\s+' + name + r'\(', fields)
        body = fields[match.end():].split('\n    public ', 1)[0] if match else ''
        module = 'module' if name == 'List' else 'CustomFieldEntity.ModuleFor(key)'
        if f'SnapshotReadAccess.Require(_db, connection, _permissions, user, {module});' not in body:
            FAILURES.append(f'CustomField.{name} lost current reader/module gate')
        if name == 'List' and 'if (manage || includeInactive) OwnerAccess.Require(_db, connection, reader);' not in body:
            FAILURES.append('Custom field management/inactive listing must validate current owner')
    public_save = fields.split('public ServiceResult SaveValues(', 1)[-1].split('    // The creator', 1)[0]
    for token in ('return _db.Tx(connection =>',
                  'var reader = SnapshotReadAccess.Require(_db, connection, _permissions, user, CustomFieldEntity.ModuleFor(key));',
                  'return SaveValuesInTransaction(connection, reader, key, entityId, values);'):
        if token not in public_save:
            FAILURES.append(f'Public custom answer save lost checked transaction/reader: {token}')
    fix = (core / 'TimeCorrectionService.cs').read_text()
    if fix.count('OwnerAccess.Require(_db, connection, user);') != 3:
        FAILURES.append('Time eligibility, list and transactional apply all need current-owner gates')
    apply = fix.split('User? user, IReadOnlyList<int>? ids, TimeSpan currentOffset)', 1)[-1].split('    private static TimeSpan ShiftFor', 1)[0]
    stages = [apply.find(token) for token in ('return _db.Tx(connection =>',
              'var owner = OwnerAccess.Require(_db, connection, user);',
              'var row = _db.QuerySingle<BillRow>(connection', 'var planned = new List<',
              'UPDATE sales SET date = @date, time_offset_seconds = @offset',
              '_audit.Record(connection, owner, Modules.Sales, "sale_time_fixed"')]
    if any(pos < 0 for pos in stages) or stages != sorted(stages):
        FAILURES.append('Time correction must read/plan/write/audit after owner check in same transaction')
    if apply.count('_db.Tx(') != 1 or '_db.Open()' in apply:
        FAILURES.append('Time apply must not use detached read connection or nested write transaction')
    db = (DOTNET / 'Area11.Core/Data/Database.cs').read_text()
    if 'connection.BeginTransaction(deferred: false)' not in db:
        FAILURES.append('Time correction depends on Database.Tx immediate transaction')
    tests = (DOTNET / 'Area11.Tests/OwnerDefinitionTimeBoundaryTests.cs').read_text()
    for name in ('ForgedOwnerLabelCannotAdministerDefinitionsOrBillTimes',
                 'InactiveDatabaseOwnerCannotUseNoOpOrDefinitionAndTimeWrites',
                 'DefinitionAuditsUseCurrentStoredOwnerName', 'CashierKeepsAuthorizedCustomerAnswerWorkflow',
                 'SameFieldServiceChecksNextReadAndPublicSaveAfterModuleRevocation',
                 'AuthorizedTimeCorrectionKeepsCreatedAtAndAllMoneySnapshots',
                 'SecondApplyOfSameOffsetIsIdempotentAndStillRequiresOwner',
                 'BadPaymentDateRollsBackEarlierBillAndItsAuditInSameTransaction',
                 'TimeAuditFailureRollsBackBillPaymentAndOffset'):
        if name not in tests:
            FAILURES.append(f'Definition/time boundary regression missing {name}')
    REPORT.append('Phase4 portion6: current owner inside four custom-definition write transactions, public field read/save current account/module, resolved audits and immediate one-transaction owner/read/plan/write time correction; delegated parent workflows/full sessions/concurrency/Windows pending')


def check_photo_current_boundaries():
    """Bounded photo-source guards only, not file/SQLite/C#/WPF execution."""
    photo = (DOTNET / 'Area11.Core/Services/PhotoService.cs').read_text()
    vm = (DOTNET / 'Area11.Desktop/ViewModels/PhotoGalleryViewModel.cs').read_text()
    window = (DOTNET / 'Area11.Desktop/Views/PhotoGalleryWindow.xaml.cs').read_text()
    tests = (DOTNET / 'Area11.Tests/PhotoCurrentAccessTests.cs').read_text()
    for token in ('public ServiceResult CheckAccess(User? user, string entity, int entityId)',
                  'actor is not { Active: true } || actor.Id <= 0',
                  'current is not { Active: true } || current.Kind != actor.Kind',
                  '!PhotoEntity.IsKnown(entity) || entityId <= 0',
                  'SnapshotReadAccess.Require(_db, connection, _permissions, actor, PhotoEntity.ModuleFor(key))',
                  'if (key == PhotoEntity.Product) RequireActor(connection, user);',
                  'else SnapshotReadAccess.Require(_db, connection, _permissions, user, PhotoEntity.ModuleFor(key));',
                  'WHERE id=@id;', 'That photo record no longer exists.'):
        if token not in photo: FAILURES.append(f'Current photo boundary missing {token}')
    for start,end,token in (
        ('public ServiceResult<List<PhotoItem>> List(', 'public ServiceResult<PhotoItem> Add(', 'RequireGallery(connection, user, key, entityId);'),
        ('internal ServiceResult<PhotoItem> AddInTransaction(', 'internal void DiscardCopy(', 'var writer = RequireGallery(connection, user, key, entityId);'),
        ('public ServiceResult SetProductCover(', 'public ServiceResult Restore(', 'var writer = RequireGallery(connection, user, selected.Entity, selected.EntityId);'),
        ('private ServiceResult ChangeActive(', 'private ServiceResult<PhotoItem> Read(', 'var writer = RequireGallery(connection, user, row.Entity, row.EntityId);'),
        ('private ServiceResult<PhotoItem> Read(', 'public ServiceResult CheckAccess(', 'RequireGallery(connection, user, row.Entity, row.EntityId);'),
        ('public ServiceResult<string> OwnAvatar(', 'public ServiceResult<string> PathOf(', 'RequireActor(connection, user);')):
        body = photo.split(start,1)[-1].split(end,1)[0]
        if token not in body: FAILURES.append(f'Current photo check missing in {start}')
    add = photo.split('internal ServiceResult<PhotoItem> AddInTransaction(',1)[-1].split('internal void DiscardCopy(',1)[0]
    if not (add.find('RequireGallery(') < add.find('File.Copy(') < add.find('_audit.Record(connection, writer,')):
        FAILURES.append('Current photo writer must be checked before file copy and stored audit')
    if photo.count('_audit.Record(connection, writer,') != 3 or 'DiscardCopy(copiedPath);' not in photo:
        FAILURES.append('Photo add/cover/visibility audit needs stored current writer; failed new copy cleanup retained')
    for token in ('public bool RefreshAccess()', '_services.Photos.CheckAccess(_user, Entity, EntityId)',
                  'Selected = null;', 'Preview = null;', 'CountText = "Photos not loaded";',
                  'Title = "Photos";', 'HiddenText = string.Empty;',
                  'var path = _services.Photos.PathOf(_user, row.Item.Id);', 'Preview = Decode(path.Value, decodeWidth: null);',
                  'CanEdit = true;', '_lastLoadSucceeded = CanEdit;', 'public bool HasNoPhotos => CanEdit && Photos.Count == 0;',
                  'SAVED. The photo change was stored, but the gallery could not refresh.'):
        if token not in vm: FAILURES.append(f'Current gallery lifecycle missing {token}')
    if vm.count('SAVED. The photo change was stored, but the gallery could not refresh.') != 4:
        FAILURES.append('Each of four gallery write actions needs explicit postcommit SAVED refresh failure')
    if 'Preview = Decode(row.Item.FullPath' in vm: FAILURES.append('Gallery preview must not open cached path without current service read')
    for method in ('Add()', 'AddFile(string path)', 'TakePhoto()', 'SetCover()', 'Hide()', 'Restore()', 'OpenFile()'):
        body = vm.split('void ' + method,1)[-1].split('}',1)[0]
        if 'if (!RefreshAccess()) return;' not in body: FAILURES.append(f'Current gallery command missing check: {method}')
    for token in ('TimeSpan.FromSeconds(3)', 'viewModel.RefreshAccess()', 'accessTimer.Start()', 'Closed += (_, _) => accessTimer.Stop();'):
        if token not in window: FAILURES.append(f'Current gallery window lifecycle missing {token}')
    if 'Title = "Photos - " + viewModel.Title;' in window:
        FAILURES.append('Gallery OS title must not retain a denied record name')
    if tests.count('[Fact]') != 30:
        FAILURES.append('Current photo30 isolated regression facts missing')
    for name in ('RevokedCustomerGrantDeniesCachedGalleryActions','ProductCoversRemainAvailableToCurrentPosOnlyReader',
                 'CurrentWriterStoredNameIsUsedForPhotoAudit','StagedPhotoCurrentActorFailureRollsBackParentCreation',
                 'AddAuditFaultRemovesOnlyTheNewCopiedFileAndRollsBackRow','InactiveParentRetainsHistoricalReadAndPutAwayButCannotAdd'):
        if 'void ' + name + '(' not in tests: FAILURES.append(f'Current photo regression missing {name}')
    REPORT.append('Phase4 portion24: current gallery/own-avatar/cover readers and transactional photo writers with stored audits,known parent checks,stale gallery/preview/path/timer cleanup;30 facts written, native/custom/session/files/Windows/globalPhase4 OPEN')


def check_settings_current_boundaries():
    """Source contracts only; actual C#/WPF/provider checks remain required."""
    core = DOTNET / 'Area11.Core/Services'
    settings = (core / 'SettingsService.cs').read_text()
    startup = (core / 'StartupService.cs').read_text()
    workbook = (core / 'WorkbookService.cs').read_text()
    vm = (DOTNET / 'Area11.Desktop/ViewModels/SettingsViewModel.cs').read_text()
    view = (DOTNET / 'Area11.Desktop/Views/SettingsView.xaml').read_text()
    tests = (DOTNET / 'Area11.Tests/SettingsCurrentAccessTests.cs').read_text()
    runtime = settings.split('private static bool IsRuntimePreference', 1)[-1].split('/// <summary>', 1)[0]
    expected = {'session.lastUser','session.lastStart','session.lastSeen','session.cleanExit','session.handoverAt','session.handoverBy',
                'backup.lastRunAt','backup.lastFile','backup.lastRestoreSummary','sync.lastSyncAt','sync.status','sync.lastError',
                'hardware.testedAt','hardware.testedBy','hardware.note','hardware.testedWidth','hardware.passed'}
    if set(re.findall(r'"([^"\n]+)"', runtime)) != expected or 'if (!IsRuntimePreference(key))' not in settings:
        FAILURES.append('Current Settings quiet door must allow exactly17 runtime metadata keys')
    for token in ('public ServiceResult CheckAccess(User? actor)', 'public ServiceResult CheckCloudAccess(User? actor)',
                  'SnapshotReadAccess.Require(_db, connection, _permissions, actor, module, Modules.Settings)',
                  'if (_permissions is null) throw', 'keyList.All(IsCloudConfiguration)', 'keyList.All(IsBackupSchedule)',
                  'keyList.Length == 1 && keyList[0] == "printer.autoPrint"',
                  'keyList.Any(IsCloudConfiguration) && !_permissions.Can(current, Modules.Sync)',
                  'var ownerOnly = values.Keys.Any(RequiresOwner);',
                  'var writer = RequireWriter(connection, actor, values.Keys);',
                  '_audit.Record(connection, writer, "Settings", "settings_change"',
                  'var secret = pair.Key == SyncService.KeyKey;',
                  'secret ? "{\\"secret\\":\\"not disclosed\\"}" : old',
                  'secret ? "{\\"secret\\":\\"changed\\"}" : json',
                  'if (keyList.Any(RequiresOwner)) OwnerAccess.Require(_db, connection, actor);'):
        if token not in settings:
            FAILURES.append(f'Current Settings writer/secret contract missing {token}')
    owner = settings.split('private static bool RequiresOwner',1)[-1].split('// Workbook',1)[0]
    for token in ('"security."', '"startup.mode"', '"discount.maxPercentCashier"', '"discount.maxPercentManager"',
                  '"brand.appName"', '"appearance.iconFileName"', '"lockdown.focusGuard"', 'MasterFoundationSettings.ProfitShowMargins'):
        if token not in owner: FAILURES.append(f'Current Settings owner policy missing {token}')
    write = settings.split('public ServiceResult SetMany(', 1)[-1].split('internal ServiceResult SaveRecoveryHash',1)[0]
    if not (write.find('_db.Tx(connection =>') < write.find('RequireWriter(') < write.find('foreach (var pair in values)')):
        FAILURES.append('Current Settings writer must be checked inside Tx before first write')
    for token in ('_settings.SaveSystemStartupMode(automatic: true)', '_settings.SaveSystemStartupMode(automatic: false)',
                  'var access = _settings.CheckAccess(actor);', 'if (!saved.Ok) return saved;'):
        if token not in startup: FAILURES.append(f'Current startup settings boundary missing {token}')
    if '_settings.SetUiOnly(ModeKey,' in startup or 'actor: null' in startup:
        FAILURES.append('Startup must not use a general actorless configuration setter')
    if workbook.count('!_settings.CheckAccess(user).Ok') != 2:
        FAILURES.append('Workbook Settings preview/apply each need current Settings access')
    token = 'if (hasSettings) _settings.RequireBulkWriter(connection, user, preview.SettingValues.Keys);'
    if token not in workbook:
        FAILURES.append('Workbook Settings require current writer inside apply Tx')
    for token in ('public bool CanUseSettings => _settingsAccessAllowed;', '_services.Settings.CheckAccess(_user)',
                  '_services.Settings.CheckCloudAccess(_user).Ok', 'ClearCloudData(); ClearStaffData(); LabelRows.Clear();',
                  'SyncKey = SyncUrl = SyncTable = SyncShopId = SyncAutomaticMinutesText = string.Empty;',
                  'public bool CanChangeCloud => CanUseSettings && _cloudAccessAllowed;',
                  'if (CanChangeCloud && (!int.TryParse', 'Settings SAVED; startup mode was not saved.',
                  'Settings SAVED; screen lock was not saved.'):
        if token not in vm: FAILURES.append(f'Current Settings UI lifecycle missing {token}')
    if '_services.Cloud.KeyLabel' in vm or 'Visibility="{Binding CanUseSettings, Converter={StaticResource BoolToVis}}"' not in view:
        FAILURES.append('Settings must hide denied content and avoid showing key fragments')
    if tests.count('[Fact]') != 32:
        FAILURES.append('Current Settings32 isolated regression facts missing')
    for file, names in (
        ('StartupCloseTests.cs', ('InactiveStoredOwnerCannotTouchStartupStores','ForgedOwnerCannotTouchStartupStores')),
        ('WorkbookTests.cs', ('DelegatedWorkbookCannotSmuggleOwnerOnlySettingWithOrdinaryChange','WorkbookSettingsRevokedAfterPreviewDeniesWholeFile'))):
        body = (DOTNET / 'Area11.Tests' / file).read_text()
        for name in names:
            if 'public void ' + name + '(' not in body: FAILURES.append(f'Current Settings regression missing {name}')
    REPORT.append('Phase4 portion23: current transactional Settings/own-module exceptions and workbook settings writer,17-key quiet allowlist,future cloud-key audit redaction,fixed startup system paths and stale Settings/cloud UI cleanup;36 facts written, C#/Windows/session/record/globalPhase4 OPEN')


def check_transaction_permission_boundaries():
    """Source-only connection ownership/fail-closed contracts, not SQLite/.NET execution."""
    database = (DOTNET / 'Area11.Core/Data/Database.cs').read_text()
    permission = (DOTNET / 'Area11.Core/Services/PermissionService.cs').read_text()
    tests = (DOTNET / 'Area11.Tests/TransactionPermissionTests.cs').read_text()
    for token in ('private readonly object _transactionGate = new();',
                  'ReferenceEquals(_transaction?.Connection, connection) ? _transaction : null;',
                  'lock (_transactionGate) return command(TransactionFor(connection));'):
        if token not in database:
            FAILURES.append(f'Transaction/permission connection ownership missing {token}')
    for method in ('Execute', 'Query', 'QuerySingle', 'Scalar', 'NextId'):
        # All five wrappers must select by connection before invoking Dapper.
        declaration = re.search(r'public [^\n]+ ' + method + r'(?:<T>)?\(SqliteConnection connection[^\n]*\) =>\s*([^;]+);', database)
        if declaration is None or 'Command(connection, transaction =>' not in declaration[1]:
            FAILURES.append(f'Transaction/permission wrapper must bind its own connection: {method}')
    read = database.split('public T Read<T>', 1)[-1].split('/// <summary>All-or-nothing', 1)[0]
    for token in ('lock (_transactionGate)', 'return read(_transaction.Connection ?? throw',
                  'using var connection = Open();', 'return read(connection);'):
        if token not in read:
            FAILURES.append(f'Transaction/permission synchronous read contract missing {token}')
    tx = database.split('public T Tx<T>', 1)[-1].split('public void Tx(', 1)[0]
    for token in ('lock (_transactionGate)', 'if (_transaction is not null)', 'BeginTransaction(deferred: false)',
                  '_transaction = transaction;', 'transaction.Commit();', 'transaction.Rollback();', '_transaction = null;'):
        if token not in tx:
            FAILURES.append(f'Transaction/permission Tx cleanup/serialization missing {token}')
    ordinary = permission.split('public bool Can(', 1)[-1].split('public static bool IsFinancialVisibility', 1)[0]
    financial = permission.split('private bool CanSeeFinancial', 1)[-1].split('public IReadOnlyDictionary', 1)[0]
    for label, body in (('ordinary', ordinary), ('financial', financial)):
        if 'return _db.Read(connection =>' not in body or '_db.Open()' in body:
            FAILURES.append(f'Transaction/permission {label} grant must use synchronous current read')
        failure = body.split('catch (Exception exception)', 1)[-1]
        if 'return false;' not in failure or 'return RoleDefault(' in failure:
            FAILURES.append(f'Transaction/permission {label} failed grant read must deny')
    if 'return row is null ? RoleDefault(user.Kind, module) : row.Value != 0;' not in ordinary:
        FAILURES.append('Transaction/permission successful missing ordinary grant must retain role defaults')
    mapping = permission.split('public IReadOnlyDictionary<string, bool> MapFor', 1)[-1].split('public ServiceResult Set', 1)[0]
    if 'var rows = _db.Read(connection => _db.Query<PermissionRow>' not in mapping:
        FAILURES.append('Transaction/permission assignment map must use current read')
    for name in (
        'ExecuteDoesNotLendTransactionToForeignConnection', 'QueryDoesNotLendTransactionToForeignConnection',
        'QuerySingleDoesNotLendTransactionToForeignConnection', 'ScalarDoesNotLendTransactionToForeignConnection',
        'NextIdDoesNotLendTransactionToForeignConnection', 'ReadBorrowsCurrentConnectionWithoutDisposingIt',
        'OutsideReadOwnsAndDisposesItsConnection', 'ReadFailureDoesNotDisposeBorrowedTransaction',
        'SameConnectionCommandsStillRollBackTogetherAndNextTxWorks', 'NestedTxStillRejectedWithoutLosingOuterTransaction',
        'OtherThreadReadWaitsUntilCommitAndDoesNotBorrowTheWriterConnection', 'OtherThreadTxWaitsInsteadOfReceivingNestedTxError',
        'CommittedDelegatedCashIsAllowedInsideTx', 'CommittedRevokedCustomersCannotRegainCashierDefaultInsideTx',
        'PendingDelegatedGrantIsVisibleInsideTx', 'PendingRevocationIsVisibleAndRollbackRestoresRoleDefault',
        'RolledBackDelegationDoesNotLeakThroughPermissionCache', 'MissingGrantKeepsRoleDefaultWhenReadSucceeds',
        'OrdinaryPermissionReadFailureIsDeniedOutsideTx', 'OrdinaryPermissionReadFailureIsDeniedInsideTx',
        'OwnerOrdinaryBypassAndExplicitFinancialRevocationRemainSeparate', 'BothFinancialGrantsReadPendingChangesIndependently',
        'FinancialGrantReadsCurrentPendingAccountState', 'FinancialReadFailureStillFailsClosedForOwner',
        'AssignmentMapReadsPendingAssignmentsWithoutBecomingAuthorizationApi',
        'DelegatedCustomerCollectionStillPostsExactlyOnePayment', 'RevokedCustomerCollectionDoesNotPostOrAudit'):
        if 'public void ' + name + '(' not in tests:
            FAILURES.append(f'Transaction/permission regression missing {name}')
    REPORT.append('Phase4 portion22: connection-owned five command wrappers, synchronous current Read/Tx serialization, permission own-write visibility and failed grant read deny;27 facts written, C#/Windows/global concurrency/session/settings certification OPEN')


def check_financial_visibility_grants():
    """D-110 independent financial grants. Named paths only, not whole-application secrecy proof."""
    core = DOTNET / 'Area11.Core'
    permission = (core / 'Services/PermissionService.cs').read_text()
    for token in ('public const string PurchaseCost = "purchasecost";', 'PurchaseCost => "View Purchase Cost"',
                  'Profit => "View Profit"', 'Modules.Profit or Modules.PurchaseCost or Modules.Audit',
                  'if (IsFinancialVisibility(module)) return CanSeeFinancial(user, module);',
                  'current is not { Active: true } || current.Kind != actor.Kind',
                  'if (target.IsOwner && !IsFinancialVisibility(module))',
                  'user.IsOwner && !IsFinancialVisibility(module)'):
        if token not in permission:
            FAILURES.append(f'Central financial permission contract missing {token}')
    if not (permission.find('if (IsFinancialVisibility(module))') < permission.find('if (user.IsOwner)')):
        FAILURES.append('Owner module bypass must not override explicit financial checkbox')
    financial = permission.split('private bool CanSeeFinancial', 1)[-1].split('public IReadOnlyDictionary', 1)[0]
    if 'return false; // never fall back' not in financial:
        FAILURES.append('Financial DB failure must refuse visibility, not role-fallback')
    cost = (core / 'Services/CostPolicy.cs').read_text()
    for token in ('settings.GetBool(MasterFoundationSettings.ProfitShowMargins, true)',
                  'permissions.Can(user, Modules.PurchaseCost)'):
        if token not in cost:
            FAILURES.append(f'Cost policy must use main switch and independent cost grant: {token}')
    history = (core / 'Services/ProductHistoryService.cs').read_text()
    if 'CanSeeCost = CostPolicy.CanSee(_permissions, _settings, reader)' not in history or 'CanSeeCost = reader.CanManage' in history:
        FAILURES.append('History must supersede D-109 role-based cost rule with D-110 grant')
    report = (core / 'Services/ReportService.cs').read_text()
    for token in ('DeadStock = ReadDeadStock(connection, canSeeCost)', 'projected.CostKnown = canSeeCost;',
                  'if (!canSeeCost) projected.ValuePaisa = 0;', 'CostPaisa = canSeeCost ? items.Cost : 0',
                  'RestockCostPaisa = canSeeCost ? returns.RestockCost : 0',
                  'PurchasesPaisa = canSeeCost ? outflow.Purchases : 0'):
        if token not in report:
            FAILURES.append(f'Report independent purchase-cost projection missing {token}')
    if 'CostKnown ? Money.Format(ValuePaisa) : "hidden"' not in (core / 'Models/ReportModels.cs').read_text():
        FAILURES.append('Dead stock must distinguish hidden valuation from actual zero')
    products = (core / 'Services/ProductService.cs').read_text()
    if products.count('ProductReadAccess.RequirePreparationCost(reader, purpose, _permissions, settings);') != 2:
        FAILURES.append('Product search/get must refuse unauthorized preparation instead of feeding masked zero')
    project = (core / 'Services/ProductReadAccess.cs').read_text()
    for token in ('!CostPolicy.CanSee(permissions, settings, reader)',
                  'product.CostKnown = financialRow && canSeeCost;', 'product.MarginKnown = financialRow && canSeeProfit;',
                  '(product.RetailPaisa - product.CostPaisa) * 100m / product.RetailPaisa'):
        if token not in project:
            FAILURES.append(f'Independent product cost/profit projection missing {token}')
    quote = (core / 'Services/SaleService.cs').read_text()
    for token in ('var canSeeCost = CostPolicy.CanSee(_permissions, _settings, user);',
                  'CostKnown = canSeeCost', 'ProfitKnown = canSeeProfit', 'CostPaisa = canSeeCost',
                  'ProfitPaisa = canSeeProfit'):
        if token not in quote:
            FAILURES.append(f'Quote independent financial projection missing {token}')
    ui = DOTNET / 'Area11.Desktop'
    vm = (ui / 'ViewModels/SettingsViewModel.cs').read_text()
    if 'editable || (CanManageFinancialVisibility && PermissionService.IsFinancialVisibility(module))' not in vm:
        FAILURES.append('Owner must be able to edit own two financial visibility checkboxes')
    if 'if (_user.IsOwner) values[MasterFoundationSettings.ProfitShowMargins] = ShowMargins;' not in vm or '["profit.showMargins"] = ShowMargins,' in vm:
        FAILURES.append('Ordinary manager Settings save must omit the owner-only main financial switch')
    pricing = (ui / 'ViewModels/LinePricingViewModel.cs').read_text()
    for token in ('if (line.CostKnown)', 'if (line.ProfitKnown)', 'Money.Format(line.ProfitPaisa)'):
        if token not in pricing:
            FAILURES.append(f'Item editor must render independent Core quote flags: {token}')
    settings = (core / 'Services/SettingsService.cs').read_text()
    if 'if (key == MasterFoundationSettings.ProfitShowMargins)' not in settings or 'or MasterFoundationSettings.ProfitShowMargins;' not in settings:
        FAILURES.append('Main financial switch must be protected from UI-only/manager saves')
    if 'or MasterFoundationSettings.ProfitShowMargins ||' not in (core / 'Services/WorkbookService.cs').read_text():
        FAILURES.append('Workbook must not bypass owner-only main financial switch')
    tests = (DOTNET / 'Area11.Tests/FinancialVisibilityTests.cs').read_text()
    for name in ('StaffHasTwoDistinctFinancialCheckboxKeysAndLabels', 'CostOnlyGrantWorksAcrossNamedViewsWithoutGrantingProfit',
                 'ProfitOnlyGrantDoesNotReturnNamedPurchaseCostFields', 'MainSwitchOffMasksBothEvenForOwner',
                 'OwnerCostCheckboxMayBeRevokedWithoutLosingSettingsAccess',
                 'FinancialDatabaseReadFailureDoesNotFallBackToOwnerVisibility',
                 'QuoteProfitOnlyReturnsActualProfitButNoAllocatedCost',
                 'CostPreparationRequiresCostGrantRatherThanReturningFakeZeroInputs'):
        if name not in tests:
            FAILURES.append(f'Central financial visibility regression missing {name}')
    REPORT.append('Phase4 portion7/D-110: two independent financial checkboxes + common owner-only main switch, current-account fail-closed financial reads, named cost/profit projections and safe preparation refusal; remaining batch/invoice/ledger/export propagation and full runtime audit OPEN')


def check_windows_failure_regressions():
    """Contracts from the owner's actual v0.80.0 Windows failure excerpt."""
    service = (DOTNET / 'Area11.Core/Services/PricingAllowanceService.cs').read_text()
    for token in ('if (!user.Active || person is not { Active: true })', 'if (person.Kind != user.Kind)'):
        if token not in service:
            FAILURES.append(f'Pricing read must refuse inactive/mismatched actor snapshot: {token}')
    counter = (DOTNET / 'Area11.Tests/CounterAvailabilityTests.cs').read_text()
    if 'var cashier = TestData.Cashier(test);' not in counter or 'new User { Id = 1' in counter:
        FAILURES.append('Counter role tests must not relabel owner ID1 as staff')
    book = (DOTNET / 'Area11.Core/Services/BookCheckService.cs').read_text()
    snapshot = book.split('private BookCheckRule BillImpossibleMoney(', 1)[-1].split('private BookCheckRule ', 1)[0]
    for token in ('THEN s.total_paisa ELSE 0 END AS ExpectedPaisa',
                  'THEN s.pre_extra_total_paisa - s.extra_discount_paisa'):
        if token not in snapshot:
            FAILURES.append(f'Snapshot mismatch must report gap, not entire bill: {token}')
    tests = (DOTNET / 'Area11.Tests/BookCheckTests.cs').read_text()
    for token in ('Assert.Equal(4, report.Problems);', 'Assert.Equal(5, lines.Length);',
                  'SnapshotOnlyMismatchReportsTheGapNotTheWholeBill', 'LegacyNullSnapshotDoesNotInventAFourthFinding'):
        if token not in tests:
            FAILURES.append(f'BookCheck snapshot/legacy regression missing {token}')
    runner = (ROOT / 'tools/RUN-TESTS.cmd').read_text()
    if 'findstr /r /i /c:"^[ ]*Failed Area11.Tests[.]"' not in runner:
        FAILURES.append('Test runner must include indented VSTest failure names, not only final15 lines')
    if not (ROOT / 'tools/check_bookcheck_snapshot_sqlite.py').is_file():
        FAILURES.append('Reproducible isolated BookCheck snapshot SQL regression missing')
    REPORT.append('Windows v0.80 report: real counter-role fixtures, active/same-role pricing read, snapshot gap vs whole-bill correction, new-vs-legacy finding counts and indented failure-name extraction; all four old-run failures now identified and mapped; patched xUnit rerun still UNRUN')


def check_permission_inspector_ui():
    core = DOTNET / 'Area11.Core'
    inspector = (core / 'Services/PermissionInspectionService.cs').read_text()
    for token in ('actor is not { Active: true, IsOwner: true }',
                  'owner is not { Active: true, IsOwner: true }', 'Modules.All.Contains(module)',
                  'BeginTransaction(deferred: true)', 'p.module_key = @module',
                  'p.allowed AS savedGrant', 'JsonSerializer.Deserialize<bool>(json)',
                  'PermissionService.RoleDefault(role, module)', 'transaction.Commit()',
                  'Blocked: inactive account', 'Blocked: main switch OFF'):
        if token not in inspector:
            FAILURES.append(f'Owner-only live permission inspector missing {token}')
    for forbidden in ('SELECT *', 'pin_hash', 'password_hash', '_db.Tx(', 'INSERT INTO', 'UPDATE users', 'UPDATE user_permissions'):
        if forbidden in inspector:
            FAILURES.append(f'Permission inspector must be a secret-free read-only projection: {forbidden}')
    help_text = (core / 'Services/PermissionDocumentation.cs').read_text()
    for token in ('Modules.Profit', 'Modules.PurchaseCost', 'Modules.Suppliers', 'Modules.Customers',
                  'GLOBAL ROLLOUT OPEN', 'related totals', 'line total AND Bought aggregate'):
        if token not in help_text:
            FAILURES.append(f'Permission hover/scope must preserve independent grants and honest rollout: {token}')
    ui = DOTNET / 'Area11.Desktop'
    view = (ui / 'Views/SettingsView.xaml').read_text()
    for token in ('Sensitive financial &amp; ledger permissions', 'Binding PagePermissions',
                  'Binding FinancialPermissions', 'Binding Summary', 'PermissionMouseDown', 'PermissionDetailsClick'):
        if token not in view:
            FAILURES.append(f'Settings permission UI missing {token}')
    pointer = (ui / 'Views/SettingsView.xaml.cs').read_text()
    for token in ('TimeSpan.FromMilliseconds(GetDoubleClickTime() + 1)', 'e.Handled = true;', 'e.ClickCount >= 2',
                  'CancelPermissionClick();', '_pendingPermissionClick = () => viewModel.TogglePermissionFromPointer(toggle);',
                  'viewModel.OpenPermissionDetails(toggle);'):
        if token not in pointer:
            FAILURES.append(f'Double-click details must not accidentally persist a checkbox toggle: {token}')
    settings = (ui / 'ViewModels/SettingsViewModel.cs').read_text()
    if '!Permissions.Contains(toggle)' not in settings or 'PermissionInspector.Inspect(_user, toggle.Module)' not in settings:
        FAILURES.append('Pending old-selection permission toggles/details need a current object/owner check')
    details = (ui / 'ViewModels/PermissionDetailsViewModel.cs').read_text()
    if details.find('People.Clear();') > details.find('PermissionInspector.Inspect(') or 'People.Clear();' not in details:
        FAILURES.append('Permission inspector must clear stale names BEFORE any denied/failed refresh')
    window = (ui / 'Views/PermissionDetailsWindow.xaml.cs').read_text()
    for token in ('TimeSpan.FromSeconds(3)', '_refreshTimer.Stop();', '_refreshTimer.Tick -= RefreshAssignments;'):
        if token not in window:
            FAILURES.append(f'Live permission modal refresh/lifetime missing {token}')
    tests = (DOTNET / 'Area11.Tests/PermissionInspectionTests.cs').read_text()
    for name in ('EveryExistingCheckboxHasSummaryScopeAndPrerequisites',
                 'ManagerWithStaffAndSettingsGrantsStillCannotInspect',
                 'SameInspectorShowsCostGrantAndRevocationOnNextRead',
                 'MainOffShowsAssignedGrantSeparatelyFromBlockedAccess',
                 'DatabaseFailureDoesNotReturnAnOwnerDefaultOrCachedPeople',
                 'InspectionDoesNotWriteGrantsSettingsAuditsOrFinancialRows'):
        if name not in tests:
            FAILURES.append(f'Permission inspector regression missing {name}')
    REPORT.append('Phase4 permission UI: separate sensitive grants, shared hover/scope metadata, owner-only secret-free DB snapshot inspector, 3-second refresh with stale-name clearing, delayed single-click vs double-click details; Windows gestures and global D-110 route rollout OPEN')


def check_purchase_read_visibility():
    core = DOTNET / 'Area11.Core'
    purchase = (core / 'Services/PurchaseService.cs').read_text()
    invoice = purchase.split('public ServiceResult<ReceiptDocument> BuildInvoiceDocument(', 1)[-1].split('private ServiceResult<PurchaseResult> Save(', 1)[0]
    for token in ('int purchaseId, User? user',
                  'var reader = SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Purchases);',
                  'var canSeeCost = CostPolicy.CanSee(_permissions, _settings, reader);',
                  'canSeeCost ? Money.Format(line.LineTotalPaisa) : "hidden"',
                  'canSeeCost ? Money.Format(header.TotalPaisa) : "hidden"',
                  'if (!canSeeCost)', 'Pair("Paid", "hidden");', 'Pair("Due", "hidden");',
                  'else if (header.PaidPaisa > 0)', 'catch (ValidationException exception)'):
        if token not in invoice:
            FAILURES.append(f'Purchase invoice current-reader/independent rate-total mask missing {token}')
    if invoice.find('SnapshotReadAccess.Require') > invoice.find('_db.QuerySingle<PurchaseInvoiceHeader>'):
        FAILURES.append('Invoice authorization must precede header lookup, including missing records')
    if ' + exception.Message' in invoice:
        FAILURES.append('Invoice read failure must not return internal database values/messages')
    supplier = (core / 'Services/SupplierLedgerService.cs').read_text()
    expired = supplier.split('public ServiceResult<List<ExpiredBatchRow>> ExpiredBatches(', 1)[-1].split('// -------------------------------------------------------------------- paying', 1)[0]
    for token in ('var reader = LedgerReadAccess.Require(_db, connection, _permissions, user, Modules.Suppliers);',
                  'var canSeeCost = CostPolicy.CanSee(_permissions, _settings, reader);',
                  'row.CostKnown = canSeeCost;', 'if (!canSeeCost) row.CostPaisa = 0;'):
        if token not in expired:
            FAILURES.append(f'Expired supplier batches must independently project cost/valuation: {token}')
    model = (core / 'Models/SupplierLedgerModels.cs').read_text().split('public sealed class ExpiredBatchRow', 1)[-1]
    for token in ('public bool CostKnown', 'CostKnown ? Calc.Round(QtyBase * CostPaisa) : 0',
                  'CostKnown ? Money.Format(ValuePaisa) : "hidden"'):
        if token not in model:
            FAILURES.append(f'Expired valuation must distinguish hidden from known-zero: {token}')
    vm = (DOTNET / 'Area11.Desktop/ViewModels/SuppliersViewModel.cs').read_text()
    add = vm.split('private void AddExpired()', 1)[-1].split('private void RefreshTrayTotal()', 1)[0]
    for token in ('_services.SupplierLedger.ExpiredBatches(_user, 500)',
                  'current.Value?.FirstOrDefault(item => item.BatchId == selectedBatchId)',
                  'if (!current.Ok || row is null)', '_services.SupplierLedger.PrepareReturnLine(_user, row.ProductId, row.BatchId)',
                  'if (!prepared.Ok || prepared.Value is null)', 'CostKnown = fresh.CostKnown'):
        if token not in add:
            FAILURES.append(f'One-click expired preparation must re-read/refuse masked/stale inputs: {token}')
    if add.find('if (!prepared.Ok') > add.find('Tray.Remove('):
        FAILURES.append('Do not mutate supplier return tray before the current known-cost gate')
    tests = (DOTNET / 'Area11.Tests/PurchaseReadVisibilityTests.cs').read_text()
    for name in ('ProfitOnlyCannotRevealInvoiceLineTotalPaidOrDue', 'MainOffMasksOwnerInvoiceAndOnRestoresWithoutReposting',
                 'ManagerExpiredListRetainsQuantityButMasksRateAndValuation', 'AuthorizedActualZeroExpiredCostIsKnownNotHidden',
                 'FinancialGrantDatabaseFailureDoesNotFallBackToOwnerCost', 'MaskedReadsDoNotRewriteMoneyStockPaymentsOrAudits'):
        if name not in tests:
            FAILURES.append(f'Purchase/expired read regression missing {name}')
    REPORT.append('Phase4 portion9: current-reader purchase invoice masks line/Total/Paid/Due money, expired supplier batch raw cost/valuation + known flags, one-click current-row preparation uses D-114 quantity-only DTO, never masked0 as known; posting/supplier payable policy/global route rollout and Windows remain OPEN')


def check_batch_read_visibility():
    core = DOTNET / 'Area11.Core'
    stock = (core / 'Services/StockService.cs').read_text()
    read = stock.split('public ServiceResult<List<BatchRow>> Batches(', 1)[-1].split('public ServiceResult<BatchCostPreparation>', 1)[0]
    for token in ('User? user, int productId', 'RequireBatchPurpose(purpose);',
                  'var reader = ProductReadAccess.Require(_db, connection, _permissions, user, purpose);',
                  'var canSeeCost = CostPolicy.CanSee(_permissions, _settings, reader);',
                  'row.CostKnown = canSeeCost;', 'if (!canSeeCost) row.CostPaisa = 0;'):
        if token not in read:
            FAILURES.append(f'Batch reader/current workflow/independent raw mask missing: {token}')
    if read.find('ProductReadAccess.Require') > read.find('_db.Query<BatchRow>'):
        FAILURES.append('Batch reader must be resolved before batch lookup')
    prep = stock.split('public ServiceResult<BatchCostPreparation> PrepareBatchCost(', 1)[-1].split('/// <summary>Writes the goods off', 1)[0]
    for token in ('RequireBatchPurpose(purpose);',
                  'var reader = ProductReadAccess.Require(_db, connection, _permissions, user, purpose);',
                  'ProductReadAccess.RequirePreparationCost(reader, purpose, _permissions, _settings);',
                  'WHERE product_id = @productId AND active = 1 AND (@batchId IS NULL OR id = @batchId)',
                  'if (batchId is not null && batch is null)',
                  'if (batchId is null && (batch is not null || purpose == ProductReadPurpose.SupplierReturn))',
                  'CostPaisa = batch is not null && batch.CostPaisa > 0 ? batch.CostPaisa : product.CostPaisa',
                  'ProductReadPurpose.WriteOff or ProductReadPurpose.Expense or ProductReadPurpose.SupplierReturn'):
        if token not in prep:
            FAILURES.append(f'Fresh preparation must gate money/current product-batch before fallback: {token}')
    if prep.find('RequirePreparationCost') > prep.find('_db.QuerySingle<ProductRow>'):
        FAILURES.append('Batch cost authorization must precede cost lookup')
    if ' + exception.Message' in prep:
        FAILURES.append('Batch preparation must not disclose database exception details')
    model = (core / 'Models/StockModels.cs').read_text().split('public sealed class WriteOffInput', 1)[0]
    for token in ('public bool CostKnown', 'CostKnown ? Area11.Core.Utils.Calc.Round(QtyBase * CostPaisa) : 0',
                  'CostKnown ? Area11.Core.Utils.Money.Format(ValuePaisa) : "hidden"'):
        if token not in model:
            FAILURES.append(f'Batch valuation needs known flag/hidden label: {token}')
    for name, purpose in (('WriteOff', 'WriteOff'), ('Expenses', 'Expense'), ('Suppliers', 'SupplierReturn')):
        vm = (DOTNET / f'Area11.Desktop/ViewModels/{name}ViewModel.cs').read_text()
        if f'_services.Stock.Batches(_user, SelectedProduct.Id, ProductReadPurpose.{purpose})' not in vm:
            FAILURES.append(f'{name} batch picker must pass current actor and own workflow')
        part = vm.split('private void AddLine()' if name == 'Suppliers' else 'private void RefreshPreview()', 1)[-1]
        part = part.split('private void RemoveLine()' if name == 'Suppliers' else 'private void Save()', 1)[0]
        prep_tokens = ('_services.SupplierLedger.PrepareReturnLine(_user, SelectedProduct.Id, SelectedBatch.Id)',) if name == 'Suppliers' else (
            '_services.Stock.PrepareBatchCost(_user, SelectedProduct.Id, SelectedBatch', f'ProductReadPurpose.{purpose}')
        for token in (*prep_tokens, 'if (!prepared.Ok || prepared.Value is null)',
                      'var cost = fresh.CostPaisa;', 'Packs.PerUnit(fresh.BoxStrips, fresh.StripTablets, Unit)'):
            if token not in part:
                FAILURES.append(f'{name} preview/tray must use fresh authorized input: {token}')
        if name == 'Suppliers':
            if part.find('if (!prepared.Ok') > part.find('Tray.Remove('):
                FAILURES.append('Do not mutate manual SR tray before fresh preparation succeeds')
        elif ' = "hidden";' not in part:
            FAILURES.append(f'{name} denied preview must be hidden, not fake0')
        if 'SelectedProduct.CostPaisa' in part or 'SelectedBatch.CostPaisa' in part:
            FAILURES.append(f'{name} must not reuse stale/masked displayed cost')
    tests = (DOTNET / 'Area11.Tests/BatchReadVisibilityTests.cs').read_text()
    for name in ('EverySupportedWorkflowRequiresItsOwnModuleEvenWithCost', 'ProfitOnlyDoesNotRevealBatchCostInAnyWorkflow',
                 'HiddenBatchCostCannotBecomeZeroPreparation', 'PreparationReReadsCurrentCostQuantityAndPackInsteadOfDisplayedRow',
                 'MissingOrInactiveSelectedBatchNeverFallsBackToAnother', 'NullSelectionWithLiveBatchesMustNotSilentlyPickFirstBatch',
                 'ReadAndPreparationDoNotPostMoneyStockOrAudit'):
        if name not in tests:
            FAILURES.append(f'Batch read/preparation regression missing {name}')
    REPORT.append('Phase4 portion10: current-reader batch workflow gate, independent raw/aggregate masks and fresh cost/pack/quantity preparation for write-off/expense previews and manual SR tray; posting, frozen stock-take and histories/results/global rollout remain OPEN')


def check_stock_valuation_visibility():
    core = DOTNET / 'Area11.Core'
    stock = (core / 'Services/StockService.cs').read_text()
    take = (core / 'Services/StockTakeService.cs').read_text()
    for text, signature, end, module in (
        (stock, 'public List<WriteOffRow> History(', '/// <summary>Money written off', 'Stock'),
        (stock, 'public long? LossTodayPaisa(', '// ------------------------------------------------------------------- saving', 'Stock'),
        (take, 'public StockTakeSession? Current(', '/// <summary>Every session', 'StockTake'),
        (take, 'public List<StockTakeSession> History(', '/// <summary>The sheet', 'StockTake'),
        (take, 'public List<StockTakeLine> Sheet(', '/// <summary>\n    /// Portion 11', 'StockTake'),
        (take, 'public List<Product> NotOnSheet(', '// ------------------------------------------------------------------ helpers', 'StockTake'),
        (take, 'public ServiceResult<ReceiptDocument> BuildSheetDocument(', '/// <summary>Searches medicines', 'StockTake')):
        part = text.split(signature, 1)[-1].split(end, 1)[0]
        gate = f'SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.{module})'
        if 'User? user' not in part or gate not in part or 'CostPolicy.CanSee(_permissions, _settings, reader)' not in part:
            FAILURES.append(f'Stock valuation reader/module/cost gate missing: {signature}')
        lookup = min((part.find(x) for x in ('_db.Query', '_db.Scalar', 'ReadSheet(connection', 'LoadSession(connection') if part.find(x) >= 0), default=len(part))
        if part.find(gate) > lookup:
            FAILURES.append(f'Stock valuation gate must precede read: {signature}')
    for token in ('row.CostKnown = canSeeCost;', 'if (!canSeeCost) row.ValuePaisa = 0;',
                  'if (!CostPolicy.CanSee(_permissions, _settings, reader)) return null;',
                  'value.CostKnown = canSeeCost;', 'if (!canSeeCost) value.LossPaisa = 0;'):
        if token not in stock: FAILURES.append(f'Stock raw result/aggregate mask missing {token}')
    for token in ('ProjectSession(result.Value, canSeeCost);', 'ProjectSession(LoadSession(connection, openId), canSeeCost)',
                  'foreach (var row in rows) ProjectSession(row, canSeeCost);', 'ProjectLines(lines, canSeeCost);',
                  'if (!canSeeCost) { session.ShortPaisa = 0; session.ExtraPaisa = 0; }',
                  'if (!canSeeCost) { value.ShortPaisa = 0; value.ExtraPaisa = 0; }',
                  'if (!canSeeCost) line.ValuePaisa = null;', 'if (!canSeeCost) product.CostPaisa = 0;',
                  'Pair("Value", canSeeCost ? Money.Format(valueTotal) : "hidden", bold: true);'):
        if token not in take: FAILURES.append(f'Frozen stock-take projection missing {token}')
    for text, start, end, inner in (
        (stock, 'public ServiceResult<WriteOffResult> WriteOff(', '/// <summary>The write-off history', 'Save(connection, input, user)'),
        (take, 'public ServiceResult<StockTakeSession> Open(', 'private ServiceResult<StockTakeSession> OpenInside', 'OpenInside(connection, user!, room, categoryId, note)'),
        (take, 'public ServiceResult<StockTakeResult> Apply(', 'private ServiceResult<StockTakeResult> ApplyInside', 'ApplyInside(connection, user, sessionId, note)')):
        part = text.split(start, 1)[-1].split(end, 1)[0]
        if part.find('SnapshotReadAccess.Require') < 0 or part.find('CostPolicy.CanSee') < 0 or part.find('CostPolicy.CanSee') > part.find(inner):
            FAILURES.append(f'Posting output projection must resolve reader/cost before write in transaction: {start}')
    doc = take.split('public ServiceResult<ReceiptDocument> BuildSheetDocument(', 1)[-1].split('public List<Product> NotOnSheet', 1)[0]
    if 'var lines = ReadSheet(connection, sessionId, differencesOnly);' not in doc or ' + exception.Message' in doc:
        FAILURES.append('Document must use throwing same-connection raw sheet, not failure-as-empty public Sheet or DB error details')
    model = (core / 'Models/StockTakeModels.cs').read_text()
    for token in ('!CostKnown ? "hidden" : ValuePaisa is null ? "—"', 'CostKnown ? Money.Format(ShortPaisa) : "hidden"', 'CostKnown ? Money.Format(ExtraPaisa) : "hidden"'):
        if token not in model: FAILURES.append(f'Frozen value formatter needs hidden/zero/not-counted distinction: {token}')
    vm = (DOTNET / 'Area11.Desktop/ViewModels/StockTakeViewModel.cs').read_text()
    if '?? _session;' in vm or 'Lines.Clear();\n            Found.Clear();' not in vm:
        FAILURES.append('Stock-take failed refresh must not retain old session/rows')
    for token in ('applied.ShortLabel', 'applied.ExtraLabel', 'StockTake.Current(_user)', 'StockTake.Sheet(_user,'):
        if token not in vm: FAILURES.append(f'Stock-take UI must consume explicit masked read/result: {token}')
    vm = (DOTNET / 'Area11.Desktop/ViewModels/WriteOffViewModel.cs').read_text()
    if 'saved.LossLabel' not in vm or 'Stock.LossTodayPaisa(_user)' not in vm or 'Stock.History(_user)' not in vm:
        FAILURES.append('Write-off UI must consume masked history/today/result labels')
    tests = (DOTNET / 'Area11.Tests/StockValuationVisibilityTests.cs').read_text()
    for name in ('MaskedWriteOffResultStillPostsActualCostAndStock', 'MaskedOpenResultDoesNotZeroFrozenCostUsedByCountAndApply',
                 'LaterLiveCostChangeCannotRewriteFrozenApplyOrReadValues', 'NewDocumentHidesNetValueButPreservesBookAndCounted',
                 'MissingDocumentAndDatabaseFailureNeverProduceSuccessfulEmptySheet', 'KnownZeroTodayIsNotHiddenNull'):
        if name not in tests: FAILURES.append(f'Stock valuation regression missing {name}')
    REPORT.append('Phase4 portion11: current-reader stock history/daily/result and frozen stock-take current/history/sheet/found/document/Open/Apply money projections, no public actorless fallbacks; private valuation/posting retained; generic exports/expense/session/Windows still OPEN')


def check_expense_visibility():
    core = DOTNET / 'Area11.Core'
    text = (core / 'Services/ExpenseService.cs').read_text()
    for signature, end in (
        ('public List<CashOutRow> List(', '/// <summary>\n    /// Portion 11'),
        ('public CashOutToday Today(', '/// <summary>True while'),
        ('public ServiceResult<ReceiptDocument> BuildVoucherDocument(', '/// <summary>Today')):
        part = text.split(signature, 1)[-1].split(end, 1)[0]
        gate = 'SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Cash)'
        if 'User? user' not in part or gate not in part or 'CostPolicy.CanSee(_permissions, _settings, reader)' not in part:
            FAILURES.append(f'Cash output requires current Cash reader + independent cost: {signature}')
        query = min((part.find(x) for x in ('_db.Query', 'CashOutVoucher? voucher') if part.find(x) >= 0), default=len(part))
        if part.find(gate) > query:
            FAILURES.append(f'Cash reader must precede money lookup: {signature}')
    for token in ('row.AmountKnown = row.Kind != KindDrawingGoods || canSeeCost;',
                  'if (!row.AmountKnown) row.AmountPaisa = 0;', 'today.CashKnown = true;',
                  'today.GoodsCostKnown = canSeeCost;', 'if (!canSeeCost) today.DrawingGoodsCostPaisa = 0;',
                  'var showAmount = what != KindDrawingGoods || CostPolicy.CanSee(_permissions, _settings, reader);',
                  'AND d.type = @type;', 'Pair("Amount", showAmount ? Money.Format(voucher.AmountPaisa) : "hidden", bold: true);'):
        if token not in text: FAILURES.append(f'Goods-only cash output projection/type constraint missing {token}')
    for start,end in (('public ServiceResult<ExpenseResult> AddExpense(', '// ------------------------------------------------------------------ drawings'),
                      ('private ServiceResult<DrawingResult> AddCashDrawing(', '/// <summary>\n    /// Medicine taken')):
        part = text.split(start,1)[-1].split(end,1)[0]
        if 'SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Cash);' not in part or part.find('SnapshotReadAccess.Require') > part.find('INSERT INTO'):
            FAILURES.append(f'Cash write must resolve current reader before transaction write: {start}')
    goods = text.split('private ServiceResult<DrawingResult> AddGoodsDrawing(',1)[-1].split('private ServiceResult<DrawingResult> SaveGoods(',1)[0]
    for token in ('SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Cash);',
                  'var canSeeCost = CostPolicy.CanSee(_permissions, _settings, reader);',
                  'value.AmountKnown = canSeeCost;', 'if (!canSeeCost) value.AmountPaisa = 0;'):
        if token not in goods: FAILURES.append(f'Goods result projection inside transaction missing {token}')
    if goods.find('CostPolicy.CanSee') > goods.find('SaveGoods(connection, user, input)'):
        FAILURES.append('Resolve goods cost visibility before posting, not after commit')
    doc = text.split('public ServiceResult<ReceiptDocument> BuildVoucherDocument(',1)[-1].split('public CashOutToday Today(',1)[0]
    if ' + exception.Message' in doc:
        FAILURES.append('Voucher must not return database exception details')
    model = (core / 'Models/ExpenseModels.cs').read_text()
    for token in ('AmountKnown ? Money.Format(AmountPaisa) : "hidden"',
                  'CashKnown ? ExpensePaisa + DrawingCashPaisa : 0',
                  'CashKnown ? Money.Format(CashOutPaisa) : "hidden"',
                  'GoodsCostKnown ? Money.Format(DrawingGoodsCostPaisa) : "hidden"'):
        if token not in model: FAILURES.append(f'Cash/goods known-zero/hidden formatter missing {token}')
    vm = (DOTNET / 'Area11.Desktop/ViewModels/ExpensesViewModel.cs').read_text()
    for token in ('_services.CashOut.List(_user)', '_services.CashOut.Today(_user)', 'SelectedRow = null;', '+ saved.AmountLabel + ".";'):
        if token not in vm: FAILURES.append(f'Expense UI must consume explicit projected current reads/result: {token}')
    tests = (DOTNET / 'Area11.Tests/ExpenseVisibilityTests.cs').read_text()
    for name in ('MainOffMasksGoodsButKeepsActualCashExpenseAndDrawingAmounts', 'GoodsRowCannotBeReadAsCashKindToBypassCostMask',
                 'MainOffGoodsDrawingMasksReturnedAmountButPostsActualStockAndCost', 'GoodsDrawingStillDoesNotMoveDrawerCashWhenMainOff',
                 'StockCategoryIsOnlyACashExpenseLabelNotReceivingOrGoodsValuation', 'FinancialGrantDatabaseFailureMasksGoodsButNotOrdinaryCash'):
        if name not in tests: FAILURES.append(f'Expense goods/cash regression missing {name}')
    REPORT.append('Phase4 portion12: current Cash readers, goods-drawing raw/history/today/result/voucher independent cost masks, cash expense/drawing totals retained, current account before writes; purchase/supplier/generic exports/session/Windows OPEN')



def check_purchase_preparation():
    core = DOTNET / 'Area11.Core'
    text = (core / 'Services/PurchaseService.cs').read_text()
    create = text.split('public ServiceResult<PurchaseResult> Create(', 1)[-1].split('/// <summary>Fresh preparation gate', 1)[0]
    required = ('User? user', '_db.Tx(connection =>', 'RequireEntryReader(connection, user);',
                'var saved = Save(connection, input, user!);', 'value.CostKnown = true;')
    for token in required:
        if token not in create: FAILURES.append(f'Purchase create current preparation/result gate missing {token}')
    if not (create.find('_db.Tx(connection =>') < create.find('RequireEntryReader(connection, user);') < create.find('Save(connection, input, user!)')):
        FAILURES.append('Purchase gate must be inside transaction before unchanged Save')
    gate = text.split('internal User RequireEntryReader(', 1)[-1].split('/// <summary>', 1)[0]
    for token in ('SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Purchases)',
                  'ProductReadAccess.RequirePreparationCost(reader, ProductReadPurpose.Purchase, _permissions, _settings);'):
        if token not in gate: FAILURES.append(f'Purchase preparation must resolve current module and separate Cost/main: {token}')
    model = (core / 'Models/PurchaseModels.cs').read_text()
    if 'CostKnown ? Money.Format(TotalPaisa) : "hidden"' not in model:
        FAILURES.append('Purchase result must distinguish known0 from hidden')
    drafts = (core / 'Services/DraftService.cs').read_text()
    save = drafts.split('private ServiceResult<DocumentDraft> Save<TInput>(', 1)[-1].split('// ------------------------------------------------------------------ list & open', 1)[0]
    if save.count('var reader = RequireReader(connection, user, type, includePayload: true);') != 2:
        FAILURES.append('Purchase draft insert AND update transactions must recheck preparation')
    opened = drafts.split('private ServiceResult<TInput> Open<TInput>(', 1)[-1].split('// ------------------------------------------------------------------ discard', 1)[0]
    if 'RequireReader(connection, user, type, includePayload: true);' not in opened or opened.find('RequireReader') > opened.find('ReadRow(connection, draftId)'):
        FAILURES.append('Purchase draft open must gate before reading/deserializing raw payload')
    listed = drafts.split('public ServiceResult<List<DraftSummary>> List(', 1)[-1].split('public ServiceResult<SaleInput> OpenSale', 1)[0]
    if 'RequireReader(connection, user, type, includePayload: false);' not in listed or 'RequireEntryReader(' in listed:
        FAILURES.append('Purchase draft summaries need current module, not cost-bearing preparation')
    if 'if (includePayload && type == DraftType.Purchase)' not in drafts or 'return _purchases.RequireEntryReader(connection, user);' not in drafts:
        FAILURES.append('Shared draft reader must retain current purchase Cost/main payload gate')
    posted = drafts.split('public ServiceResult<PurchaseResult> PostPurchase(', 1)[-1].split('private ServiceResult<TResult> Post<', 1)[0]
    for token in ('_purchases.CheckEntryAccess(user)', 'payload => _purchases.Create(payload, user!)',
                  'connection => { _purchases.RequireEntryReader(connection, user); }'):
        if token not in posted: FAILURES.append(f'Purchase draft post must guard current payload and premark/Create: {token}')
    post = drafts.split('private ServiceResult<TResult> Post<', 1)[-1].split('private bool RestoreAfterFailure', 1)[0]
    if 'authorizeBeforeMark?.Invoke(connection);' not in post or post.find('authorizeBeforeMark?.Invoke(connection);') > post.find('var row = ReadRow(connection, draftId);'):
        FAILURES.append('Recheck purchase preparation before terminal draft marker')
    discard = drafts.split('public ServiceResult Discard(', 1)[-1].split('// ------------------------------------------------------------------ post', 1)[0]
    if 'RequireReader(connection, user, type, includePayload: false);' not in discard or 'row.DraftType != type' not in discard:
        FAILURES.append('Purchase draft discard needs current module/type recheck')
    vm = (DOTNET / 'Area11.Desktop/ViewModels/PurchasesViewModel.cs').read_text()
    for token in ('get => CanPrepare ? _costText : string.Empty;', 'get => CanPrepare ? _paidText : string.Empty;',
                  'foreach (var line in Lines) line.CostVisible = access.Ok;', 'public string TotalText => CanPrepare',
                  'public string CostText => !CostVisible ? "hidden"', '{purchase.TotalLabel}',
                  'purpose: ProductReadPurpose.Purchase'):
        if token not in vm: FAILURES.append(f'Purchase UI must mask but preserve unsaved values: {token}')
    for method in ('LoadLookups()', 'FilterProducts()', 'AddLine()', 'Save()', 'SavePurchaseDraft()', 'OpenPurchaseDraft(int draftId)'):
        prefix = 'public void ' if method.startswith('OpenPurchaseDraft') else 'private void '
        part = vm.split(prefix + method, 1)[-1]
        gate_prefix = '{\n        if (!RefreshEntryAccess()) return;'
        if method == 'LoadLookups()':
            gate_prefix = '{\n        _lastLookupsSucceeded = false;\n        if (!RefreshEntryAccess()) return;'
        elif method == 'Save()':
            gate_prefix = '{\n        if (_saveInProgress) return;\n        _saveInProgress = true;\n        try\n        {\n            if (!RefreshEntryAccess()) return;'
        if not part.lstrip().startswith(gate_prefix):
            FAILURES.append(f'Purchase interaction must check before using retained money: {method}')
    refresh = vm.split('public bool RefreshEntryAccess()', 1)[-1].split('private void LoadLookups()', 1)[0]
    if any(token in refresh for token in ('Lines.Clear(', '_costText =', '_paidText =', '.CostPaisa =')):
        FAILURES.append('Permission mask must not erase/zero unsaved purchase work')
    view = (DOTNET / 'Area11.Desktop/Views/PurchasesView.xaml').read_text()
    for cmd in ('SaveCommand', 'SaveDraftCommand'):
        if 'Command="{Binding ' + cmd + '}" IsEnabled="{Binding CanPrepare}"' not in view:
            FAILURES.append(f'Purchase save UI enabled state missing: {cmd}')
    timer = (DOTNET / 'Area11.Desktop/Views/PurchasesView.xaml.cs').read_text()
    for token in ('TimeSpan.FromSeconds(3)', 'Loaded += OnLoaded;', 'Unloaded += (_, _) => _accessTimer.Stop();',
                  'viewModel.RefreshEntryAccess();'):
        if token not in timer: FAILURES.append(f'Purchase page access timer/lifecycle missing {token}')
    tests = (DOTNET / 'Area11.Tests/PurchasePreparationTests.cs').read_text()
    for name in ('ProfitOnlyDoesNotAuthorizeCostBearingPreparation', 'MainOffBlocksEvenOwnerAndKeepsRawDraftIntact',
                 'ExistingDraftServiceCannotPostPreviouslyOpenedPayloadAfterRevocation',
                 'RestoringGrantOpensOriginalNotMaskedZeroDraft', 'SummaryListRemainsAvailableWithoutCostOrMainButReturnsNoPayload',
                 'PurchaseValidationFailureKeepsDraftRetryableWithoutPartialStock'):
        if name not in tests: FAILURES.append(f'Purchase preparation regression missing {name}')
    REPORT.append('Phase4 portion13: current Purchases+Cost/main preparation/create/draft open-save-post; module-only summaries/discard; result known flag; retained unsaved form masked/commands blocked with next-interaction and nominal3s timer checks. Supplier/export/session/Windows OPEN')


def check_bulk_audit_access():
    core = DOTNET / 'Area11.Core/Services'
    gate = (core / 'BulkDataAccess.cs').read_text()
    for token in ('SnapshotReadAccess.Require(db, connection, permissions, actor, Modules.Import)',
                  '(cost || mixed) && !CostPolicy.CanSee', 'mixed && !ProfitPolicy.CanSee'):
        if token not in gate: FAILURES.append(f'Bulk current-module/independent financial gate missing {token}')
    perms = (core / 'PermissionService.cs').read_text()
    manager = perms.split('UserRole.Manager => module is not (',1)[-1].split('),',1)[0]
    if 'Modules.Import' not in manager or 'Import => "Allow Excel/CSV Bulk Export & Import"' not in perms:
        FAILURES.append('Bulk checkbox must be explicit and default denied to manager/staff')
    imports = (core / 'ImportService.cs').read_text()
    if '_permissions.Can(user, Modules.Products)' in imports.split('private bool CanImport(',1)[-1]:
        FAILURES.append('Products module must not replace bulk-file grant')
    for method in ('PreviewProducts', 'PreviewCustomers', 'PreviewSuppliers', 'PreviewWorkbook'):
        part = imports.split('public ImportPreview '+method+'(',1)[-1].split('\n    }',1)[0]
        if 'User? user,' not in part or 'RequirePreviewAccess(user, cost:' not in part:
            FAILURES.append(f'Upload preview must gate current actor before parsing/lookup: {method}')
    for method in ('ApplyProducts','ApplyCustomers','ApplySuppliers','ApplyPrices'):
        part = imports.split(' '+method+'(',1)[-1].split('\n    }',1)[0]
        if 'BulkDataAccess.Require(_db, connection, _permissions, _settings, user, cost:' not in part:
            FAILURES.append(f'Apply transaction must recheck bulk reader: {method}')
    workbook = (core / 'WorkbookService.cs').read_text()
    if 'user.CanManage' in workbook or workbook.count('BulkDataAccess.Require(_db, connection, _permissions, _settings, user, mixed: true);') < 3:
        FAILURES.append('Mixed workbook requires explicit bulk+both financial, including inside Apply Tx; rank not substitute')
    audit = (core / 'AuditHistoryService.cs').read_text()
    for token in ('SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Audit)',
                  'CostPolicy.CanSee(_permissions, _settings, reader) && ProfitPolicy.CanSee(_permissions, _settings, reader)',
                  "CASE WHEN @detailsKnown THEN IFNULL(old_value, '') ELSE '' END AS oldValue",
                  "CASE WHEN @detailsKnown THEN IFNULL(new_value, '') ELSE '' END AS newValue",
                  "CASE WHEN @detailsKnown THEN IFNULL(details, '') ELSE 'hidden' END AS details",
                  'foreach (var row in rows) row.DetailKnown = detailsKnown;',
                  'public ServiceResult<AuditSnapshot> ReadSnapshot(int id, User? user)',
                  'public ServiceResult<string> BuildCsv(AuditFilter? filter, User? user)',
                  'BulkDataAccess.Require(_db, connection, _permissions, _settings, user);'):
        if token not in audit: FAILURES.append(f'Audit current metadata/detail/export boundary missing {token}')
    if audit.count("OR (@detailsKnown AND (IFNULL(details, '') LIKE @pattern") != 2:
        FAILURES.append('Both audit page/count must exclude hidden fields from text-search oracle')
    if '+ exception.Message' in audit: FAILURES.append('Audit errors must not return raw database details')
    models = (DOTNET / 'Area11.Core/Models/AuditModels.cs').read_text()
    if 'if (!row.DetailKnown)' not in models or 'EarlierText = "hidden", RecordedText = "hidden", DetailsText = "hidden"' not in models:
        FAILURES.append('Unknown audit snapshot must not expose old/new raw or details')
    for stem in ('ProductHistory','BookCheck'):
        part = (core / (stem+'Service.cs')).read_text().split('public ServiceResult<string> BuildCsv(',1)[-1]
        if 'BulkDataAccess.Require(' not in part: FAILURES.append(stem+' CSV missing dedicated bulk gate')
    vm = (DOTNET / 'Area11.Desktop/ViewModels/AuditViewModel.cs').read_text()
    if '_page.ToCsv()' in vm or '_services.AuditHistory.BuildCsv(_page.Filter, _user)' not in vm:
        FAILURES.append('Audit CSV must prepare fresh gated page, never old raw DTO')
    books = (DOTNET / 'Area11.Desktop/ViewModels/BookCheckViewModel.cs').read_text()
    if '_report.ToCsv()' in books or '_services.Books.BuildCsv(_user)' not in books:
        FAILURES.append('Books CSV must be rebuilt behind current gates')
    for stem in ('Audit','ImportExport'):
        view = (DOTNET / ('Area11.Desktop/Views/'+stem+'View.xaml.cs')).read_text()
        for token in ('TimeSpan.FromSeconds(3)', 'Unloaded += (_, _) => _accessTimer.Stop();', 'viewModel.RefreshAccess();'):
            if token not in view: FAILURES.append(f'{stem} active-view stale cleanup missing {token}')
    tests = (DOTNET / 'Area11.Tests/BulkAndAuditAccessTests.cs').read_text()
    for name in ('ProductsPageGrantIsNotABulkFileGrant', 'ExplicitStaffCanPreviewAndApplyAuthorizedProductImport',
                 'NoCostRefusesWholeEditableProductFileRatherThanBlankingItsPrice', 'MetadataSearchCannotMatchHiddenValueDetailsOrEntity',
                 'AuditExportRereadsAfterRevocationInsteadOfUsingEarlierRawPage', 'MetadataReadsNeverRewriteStoredAuditValues'):
        if name not in tests: FAILURES.append(f'Bulk/audit regression missing {name}')
    shell = (DOTNET / 'Area11.Desktop/ViewModels/ShellViewModel.cs').read_text()
    if 'var canAudit = _services.AuditHistory.CheckAccess(_user) is { Ok: true, Value: true };' not in shell:
        FAILURES.append('D-115 Black box menu must require successful current Audit access AND full financial visibility')
    if 'BlackBoxMenuEligibilityRequiresCurrentAuditBothFinancialAndMain' not in tests:
        FAILURES.append('D-115 Black box menu eligibility regression missing')
    REPORT.append('Phase4 portion14/D-113: owner-default explicit bulk gate, editable data refused not zeroed, actorful previews/current Tx checks; audit metadata/search boundary and fresh details/CSV; all four CSV doors gated; password backup was pending at portion14 (portion15 has its own gate), global/Windows OPEN')


def check_encrypted_backup_contract() -> None:
    """Structural regression guard only, NOT crypto/runtime/ACL certification."""
    core = DOTNET / 'Area11.Core/Services'
    service = (core / 'BackupService.cs').read_text()
    codec = (core / 'BackupPackage.cs').read_text()
    store = (DOTNET / 'Area11.Desktop/WindowsBackupPasswordStore.cs').read_text()
    vm = (DOTNET / 'Area11.Desktop/ViewModels/BackupViewModel.cs').read_text()
    dialog = (DOTNET / 'Area11.Desktop/Views/BackupPasswordDialog.cs').read_text()
    for token in ('Iterations = 600000', 'ChunkBytes = 1024 * 1024', 'MaxPayloadBytes = 32L',
                  'new AesGcm(key, 16)', 'Rfc2898DeriveBytes.Pbkdf2(',
                  'header.CopyTo(aad, 0)', 'WriteUInt32LittleEndian(aad.AsSpan(HeaderBytes), index)',
                  'source.Length - source.Position != encodedLength - HeaderBytes',
                  'CryptographicOperations.FixedTimeEquals(digest, plain.AsSpan(0, 32))',
                  'CryptographicOperations.ZeroMemory(key)', 'if (source.ReadByte() != -1)'):
        if token not in codec: fail(f'Encrypted backup codec contract missing {token}')
    for token in ('OwnerAccess.Require(_db, connection, user)',
                  'string.Equals(password, confirmation, StringComparison.Ordinal)',
                  '_passwordStore.Save(password)', 'var password = passwordOverride ?? StoredPassword();',
                  'private ServiceResult<BackupFile> CreateCore(',
                  'File.Move(pending, path);', 'FileMode.CreateNew', 'output.Flush(flushToDisk: true)',
                  'if (!CanUse(user)) throw new ValidationException(',
                  'private static void PreflightArchive(', 'MaxArchiveEntries = 60000',
                  'bytes > 128UL * 1024 * 1024', 'if (copied > expected)',
                  'entry.Length / Math.Max(1, entry.CompressedLength) > 10000',
                  '!names.Add(name)', 'if (!acknowledgeUnverified || user is not { Active: true, IsOwner: true })',
                  '.Where(file => file.IsAutomatic && file.IsEncrypted)',
                  'BackupPackage.Decrypt(input, output, recoveryPassword);'):
        if token not in service: fail(f'Encrypted backup service contract missing {token}')
    restore = service.split('public ServiceResult<RestoreResult> Restore(', 1)[-1].split('public ServiceResult<RestoreResult> RestoreLegacy(', 1)[0]
    points = [restore.find(token) for token in ('BackupPackage.Decrypt(', 'ValidateArchive(archive);', 'return RestoreArchive(')]
    if any(point < 0 for point in points) or points != sorted(points):
        fail('Encrypted restore must authenticate/checksum before ZIP/SQLite processing')
    archive = service.split('private static void ValidateArchive(', 1)[-1].split('private static int PhotosInside', 1)[0]
    if not 0 <= archive.find('PreflightArchive(path);') < archive.find('ZipFile.OpenRead(path)'):
        fail('Archive preflight bounds must precede central-directory allocation')
    public_create = service.split('public ServiceResult<BackupFile> Create(',1)[-1].split('private ServiceResult<BackupFile> CreateCore(',1)[0]
    if 'if (!CanUse(user))' not in public_create or 'if (!automatic' in public_create:
        fail('Public automatic flag must never bypass current actor permission')
    for token in ('CryptProtectData', 'CryptUnprotectData', 'SpecialFolder.LocalApplicationData',
                  'backup-credentials', 'File.Move(temporary, _path, overwrite: true)',
                  'CryptographicOperations.ZeroMemory(plain)'):
        if token not in store: fail(f'Windows protected backup storage contract missing {token}')
    if 'CRYPTPROTECT_LOCAL_MACHINE' in store or 'Settings.' in store:
        fail('Automatic backup credential must stay current-user DPAPI, outside Settings')
    for token in ('PasswordBox', 'dialog._password.Clear()', 'dialog._confirmation.Clear()'):
        if token not in dialog: fail(f'Unbound backup password dialog missing {token}')
    for token in ('BackupPasswordDialog.Prompt(setup: false)', 'RestoreLegacy(path, _user, acknowledgeUnverified: true)',
                  'new OpenFileDialog', 'UNVERIFIED legacy recovery'):
        if token not in vm: fail(f'Backup password / new-PC / legacy UI missing {token}')
    app = (DOTNET / 'Area11.Desktop/AppServices.cs').read_text()
    if 'new WindowsBackupPasswordStore(Database.FilePath)' not in app:
        fail('Desktop must inject Windows protected backup credential storage')
    shell = (DOTNET / 'Area11.Desktop/ViewModels/ShellViewModel.cs').read_text()
    if '"Automatic backup FAILED"' not in shell: fail('Automatic failure must be conspicuous at sign-in')
    tests = (DOTNET / 'Area11.Tests/BackupPackageTests.cs').read_text() + (DOTNET / 'Area11.Tests/EncryptedBackupTests.cs').read_text()
    for name in ('IndependentPythonVector_Decrypts', 'ReorderedFullChunks_AreRejected',
                 'MissingCredential_ManualAutomaticAndSafetyFailWithoutPlaintext',
                 'Rotation_OldFileNeedsOriginalPassword_NewSafetyUsesLocalPassword',
                 'Legacy_RequiresExplicitAcceptanceAndCurrentOwner',
                 'AuthenticatedButUnsafeArchive_IsRejectedBeforeSafetyOrLiveChanges',
                 'Password_IsAbsentFromSettingsAuditAndPackagePlaintext'):
        if name not in tests: fail(f'Encrypted backup regression missing {name}')
    REPORT.append('Phase4 portion15: bounded AES-GCM/password package, current-user DPAPI host store, no plaintext fallback, atomic manual/auto/safety, authenticated staged restore/rollback, explicit owner-only legacy recovery. Structural proof only; C#/Windows/DPAPI/fault/new-PC execution OPEN')


def check_stock_minus_visibility() -> None:
    """Structural D-110/D-113 regression guard; not authorization/runtime proof."""
    service = (DOTNET / 'Area11.Core/Services/StockMinusService.cs').read_text()
    models = (DOTNET / 'Area11.Core/Models/StockMinusModels.cs').read_text()
    vm = (DOTNET / 'Area11.Desktop/ViewModels/StockMinusViewModel.cs').read_text()
    view = (DOTNET / 'Area11.Desktop/Views/StockMinusView.xaml.cs').read_text()
    tests = (DOTNET / 'Area11.Tests/StockMinusVisibilityTests.cs').read_text()
    for token in ('var access = ReadAccess(connection, user);',
                  'SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Stock)',
                  'CostPolicy.CanSee(_permissions, _settings, reader)',
                  'ReviewNotesKnown = costKnown && ProfitPolicy.CanSee(_permissions, _settings, reader)',
                  'CostKnown = access.CostKnown',
                  'ValuePaisa = access.CostKnown ? product.ValuePaisa : 0',
                  'row.ReviewNote = access.ReviewNotesKnown ? review.Details : null;',
                  'public ServiceResult<StockMinusAccess> CheckReadAccess(User? user)',
                  '_audit.Record(connection, reader, Modules.Stock, "stock_minus_review"'):
        if token not in service: fail(f'StockMinus current reader/projection missing {token}')
    if service.count('CostKnown = access.CostKnown') != 2:
        fail('StockMinus report AND row must carry the actual CostKnown flag')
    report = service.split('public ServiceResult<StockMinusReport> Report(',1)[-1].split('public ServiceResult<StockMinusAccess>',1)[0]
    if not 0 <= report.find('ReadAccess(connection, user)') < report.find('_db.Query<ProductRow>'):
        fail('StockMinus must authorize current reader before registry query')
    review = service.split('public ServiceResult Review(',1)[-1].split('private static Dictionary',1)[0]
    tx = review.find('_db.Tx(connection =>'); reader = review.find('SnapshotReadAccess.Require('); lookup = review.find('_db.QuerySingle<ProductRow>')
    if not 0 <= tx < reader < lookup: fail('StockMinus Review must check current reader INSIDE transaction before lookup/audit')
    if '+ exception.Message' in service: fail('StockMinus errors must not echo raw DB details')
    for token in ('CostKnown ? Money.Format(ValuePaisa) : "hidden"',
                  'private bool TotalKnown => CostKnown && Rows.All(row => row.CostKnown);',
                  'TotalKnown ? Rows.Sum(row => row.ValuePaisa) : 0',
                  'TotalKnown ? Money.Format(TotalValuePaisa) : "hidden"',
                  '(ReviewNoteKnown ? ReviewNote! : "hidden")'):
        if token not in models: fail(f'StockMinus known/hidden model missing {token}')
    for token in ('Selected = null;', 'TotalText = "hidden";',
                  'public bool CanReview => CanRead &&',
                  'public void RefreshAccess()', '_services.StockMinus.CheckReadAccess(_user)',
                  '_lastCostKnown != access.Value.CostKnown',
                  '_lastReviewNotesKnown != access.Value.ReviewNotesKnown'):
        if token not in vm: fail(f'StockMinus selected/status/access cleanup missing {token}')
    for token in ('TimeSpan.FromSeconds(3)', 'Unloaded += (_, _) => _accessTimer.Stop();', 'viewModel.RefreshAccess();'):
        if token not in view: fail(f'StockMinus active-view cleanup missing {token}')
    for name in ('StockOnlyKeepsQuantityButMasksRawRowAggregateAndSummary',
                 'CostOnlySeesExactValuationButNotRawAuditDetails', 'BothFinancialGrantsRevealStoredReviewDetails',
                 'FinancialPermissionDatabaseFailureDoesNotFallBackToOwnerCost',
                 'ReviewRechecksRevokedStockBeforeAuditWrite', 'StockAuthorizedReviewDoesNotRequireCostOrProfit',
                 'MetadataReadNeverRewritesStoredNegativeValuationOrReview',
                 'AuditFailureRollsBackReviewWithoutChangingStockOrMovement'):
        if name not in tests: fail(f'StockMinus regression missing {name}')
    REPORT.append('Phase4 portion16: current Stock registry reader, independent Cost/main raw row+aggregate masking; raw audit notes BOTH-financial/main; current review writer inside transaction, stock/episode SQL unchanged; active-view stale selection/access cleanup. Windows/runtime/session/fullPhase4 OPEN')



def check_supplier_accounting_exception():
    """D-114 bounded source contracts, not session/Windows/runtime proof."""
    core = DOTNET / 'Area11.Core'
    source = (core / 'Services/SupplierLedgerService.cs').read_text()
    prep = source.split('public ServiceResult<SupplierReturnPreparation> PrepareReturnLine(', 1)[1].split('// -------------------------------------------------------------------- paying', 1)[0]
    required = ('LedgerReadAccess.Require(_db, connection, _permissions, user, Modules.Suppliers);',
                'FROM products WHERE id = @id AND active = 1;',
                'FROM batches WHERE id = @id AND product_id = @productId AND active = 1;',
                '!double.IsFinite(batch.QtyBase) || batch.QtyBase <= 0',
                'var costKnown = CostPolicy.CanSee(_permissions, _settings, reader);',
                'CostKnown = costKnown,',
                'CostPaisa = costKnown ? (batch.CostPaisa > 0 ? batch.CostPaisa : product.CostPaisa) : 0')
    for token in required:
        if prep.count(token) != 1:
            FAILURES.append(f'Supplier quantity preparation needs exactly one checked binding/mask: {token}')
    if prep.count('CostKnown =') != 1 or prep.count('CostPaisa =') != 1:
        FAILURES.append('Supplier preparation must not override checked cost visibility in its DTO')
    if prep.find('LedgerReadAccess.Require') > prep.find('_db.QuerySingle<ProductDbRow>'):
        FAILURES.append('Supplier preparation must authorize before product lookup')
    if 'RequirePreparationCost' in prep or ' + exception.Message' in prep:
        FAILURES.append('Supplier quantity preparation must not require cost or expose DB error')
    for start, end in (('public ServiceResult<SupplierPayment> Pay(', '// ------------------------------------------------------------------ returning'),
                       ('public ServiceResult<SupplierReturnResult> Return(', '// ------------------------------------------------------------------ statement')):
        part = source.split(start, 1)[1].split(end, 1)[0]
        gate = 'var reader = LedgerReadAccess.Require(_db, connection, _permissions, user, Modules.Suppliers);'
        if part.count(gate) != 1 or not (part.find('_db.Tx(connection =>') < part.find(gate) < min(
                [x for x in (part.find('_db.QuerySingle<'), part.find('SaveReturn(connection')) if x >= 0])):
            FAILURES.append('Supplier Pay/Return current-account gate must be inside Tx before data/write')
        if 'CostPolicy' in part or 'Modules.PurchaseCost' in part:
            FAILURES.append('Supplier accounting writes may not acquire a direct-cost prerequisite')
    if 'SaveReturn(connection, input, reader)' not in source or '_audit.Record(connection, reader, "Suppliers", "supplier_pay"' not in source:
        FAILURES.append('Supplier writes must use current stored actor in posting/audit')
    products = (core / 'Services/ProductService.cs').read_text()
    if products.count('if (purpose != ProductReadPurpose.SupplierReturn)\n                ProductReadAccess.RequirePreparationCost') != 2:
        FAILURES.append('Only SupplierReturn product selection may bypass cost preparation prerequisite')
    vm = (DOTNET / 'Area11.Desktop/ViewModels/SuppliersViewModel.cs').read_text()
    for token in ('public void RefreshAccess()', '_services.SupplierLedger.CheckReadAccess(_user)',
                  'ClearSensitiveData();', 'MaskDirectCosts();', 'row.CostKnown = false;', 'row.CostPaisa = 0;',
                  'public bool CanWork => _accessAllowed;', 'OnPropertyChanged(nameof(IsNotAllowed))',
                  'Tray.Any(row => !row.CostKnown)', 'CostKnown ? Calc.Round(QtyBase * CostPaisa) : 0',
                  'CostKnown ? Money.Format(ValuePaisa) : "hidden"'):
        if token not in vm:
            FAILURES.append(f'Supplier stale UI/known flag contract missing {token}')
    if vm.count('CostKnown = fresh.CostKnown') != 2 or 'if (!row.CostKnown)' in vm:
        FAILURES.append('Both Supplier tray paths must preserve fresh known flags without requiring cost')
    profile = vm.split('private void SendProfileOnWhatsApp()', 1)[1].split('private void SearchProducts()', 1)[0]
    for token in ('if (!UpdateAccess()) return;', '_services.SupplierLedger.Page(_user, Selected.Id)',
                  'var summary = page.Value.Summary;', 'BuildSupplierProfile(summary.Name, summary.Agency, summary.PayablePaisa)'):
        if token not in profile:
            FAILURES.append(f'Supplier profile sharing must rebuild current account before output: {token}')
    if 'BuildSupplierProfile(Selected.' in profile:
        FAILURES.append('Supplier profile must not share cached financial summary')
    view = (DOTNET / 'Area11.Desktop/Views/SuppliersView.xaml.cs').read_text()
    for token in ('TimeSpan.FromSeconds(3)', 'Loaded +=', 'Unloaded +=', '_accessTimer.Stop()', 'viewModel.RefreshAccess()'):
        if token not in view:
            FAILURES.append(f'Supplier active-view lifecycle check missing {token}')
    tests = (DOTNET / 'Area11.Tests/SupplierAccountingAccessTests.cs').read_text()
    if len(re.findall(r'\.Pay\([^(),]+, supplier, \d+, "cash", null\)', tests)) != 6:
        FAILURES.append('Supplier regressions must pass all five required Pay arguments')
    if tests.count('[Fact]') != 24:
        FAILURES.append('Supplier accounting exception requires all24 written regression facts')
    for name in ('SupplierOnlyCreditReturnUsesServerCostAndReconciles', 'MainOffMasksCostEvenForOwnerButReturnStillSettles',
                 'SupplierProductSearchAndGetReturnMaskedRowsWithoutCostGrant', 'RevocationAfterPreparationBlocksBothWrites',
                 'KnownGenuineZeroIsDifferentFromHiddenZero', 'CurrentStoredNameIsUsedForReturnAudit'):
        if name not in tests:
            FAILURES.append(f'Supplier accounting exception regression missing {name}')
    REPORT.append('Phase4 portion17: D-114 Supplier accounting exception, current transactional Pay/SR actor, masked quantity-only preparation and stale UI lifecycle;24 facts written, Windows/runtime/globalPhase4 OPEN')



def check_customer_current_boundaries():
    """Portion18 structural contracts only; no authentication/Windows guarantee."""
    core = DOTNET / 'Area11.Core'
    ledger = (core / 'Services/CustomerLedgerService.cs').read_text()
    collect = ledger.split('public ServiceResult<CollectionResult> Collect(', 1)[1].split('public ServiceResult<ReceiptDocument> Statement(', 1)[0]
    gate = 'var reader = LedgerReadAccess.Require(_db, connection, _permissions, user, Modules.Customers);'
    if collect.count(gate) != 1 or not (collect.find('_db.Tx(connection =>') < collect.find(gate) < collect.find('_db.QuerySingle<BalanceRow>')):
        FAILURES.append('Customer collection needs one current reader inside Tx before customer lookup')
    for token in ('userId = reader.Id,', '_audit.Record(connection, reader, "Customers", "customer_collect"',
                  'if (amountPaisa > customer.BalancePaisa)', 'var newBalance = customer.BalancePaisa - amountPaisa;'):
        if token not in collect: FAILURES.append(f'Collection current actor/cap/accounting missing {token}')
    if 'CostPolicy' in collect or 'ProfitPolicy' in collect:
        FAILURES.append('Customer collection must not acquire financial-visibility prerequisite')
    receipt = ledger.split('public ServiceResult<ReceiptDocument> PaymentReceipt(',1)[1].split('private long? BalanceAt',1)[0]
    for token in (gate, 'WHERE p.id = @id AND p.sale_id IS NULL AND p.purchase_id IS NULL',
                  'AND p.provisional_return_id IS NULL;', 'Add("Received by: " + reader.Name);'):
        if token not in receipt: FAILURES.append(f'Collection receipt current reader/typed ID missing {token}')
    if ' + exception.Message' in ledger:
        FAILURES.append('Customer ledger must not concatenate raw DB exception into returned error')
    reps = (core / 'Services/RepService.cs').read_text()
    methods = [('ListReps', 'SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Products);', '_db.Query<RepCompanyRow>'),
               ('SaveRep', 'var reader = SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Products);', '_db.QuerySingle<RepCompanyRow>'),
               ('ListDoctors', 'var reader = SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Customers);', '_db.Query<DoctorRow>'),
               ('ListCustomerProfit', 'var reader = SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Customers);', '_db.Query<CustomerProfitRow>')]
    for name, token, query in methods:
        part = reps.split(' '+name+'(',1)[1].split('\n    }',1)[0]
        if part.count(token) != 1 or part.find(token) > part.find(query):
            FAILURES.append(f'{name} needs exactly one current workflow reader before lookup')
        if name == 'SaveRep' and not part.find('_db.Tx(connection =>') < part.find(token):
            FAILURES.append('Rep update current reader must be inside Tx')
        if name == 'ListCustomerProfit' and not (0 <= part.find('if (!ProfitPolicy.CanSee(_permissions, _settings, reader))') < part.find(query)):
            FAILURES.append('Lifetime profit requires separate Profit/main before query')
    if '_audit.Record(connection, reader, Modules.Products, "company_rep_update"' not in reps or ' + exception.Message' in reps:
        FAILURES.append('Rep write must use current actor and safe errors')
    customer = (DOTNET / 'Area11.Desktop/ViewModels/CustomersViewModel.cs').read_text()
    for token in ('public void RefreshAccess()', '_services.Ledger.CheckAccess(_user)', 'row.ProfitKnown = false;',
                  'row.ProfitPaisa = 0;', 'ProfitText = "hidden";', 'ClearSensitiveData();', 'LastPaymentId = 0;',
                  'CanPrintReceipt = false;', 'OnPropertyChanged(nameof(IsNotAllowed))'):
        if token not in customer: FAILURES.append(f'Customer stale UI cleanup contract missing {token}')
    profile = customer.split('private void SendProfileOnWhatsApp()',1)[1].split('private void PrintMonthStatement()',1)[0]
    for token in ('if (!UpdateAccess()) return;', '_services.Ledger.Page(_user, Selected.Id)',
                  'summary.Name, summary.Phone, summary.BalancePaisa, summary.CreditLimitPaisa'):
        if token not in profile: FAILURES.append(f'Customer profile must re-read current account: {token}')
    doctors = (DOTNET / 'Area11.Desktop/ViewModels/DoctorsViewModel.cs').read_text()
    for token in ('public void RefreshAccess()', '_services.Reps.CheckAccess(_user)', 'SelectedCompany = null;',
                  'Doctors.Clear();', 'Customers.Clear();', 'previous.Profit != _access.Profit', 'if (!CanEditReps) return;'):
        if token not in doctors: FAILURES.append(f'Doctor/rep stale flags/clear contract missing {token}')
    for name in ('Customers','Doctors'):
        view = (DOTNET / f'Area11.Desktop/Views/{name}View.xaml.cs').read_text()
        for token in ('TimeSpan.FromSeconds(3)', 'Loaded +=', 'Unloaded +=', '_accessTimer.Stop()', 'viewModel.RefreshAccess()'):
            if token not in view: FAILURES.append(f'{name} active-view timer lifecycle missing {token}')
    tests = (DOTNET / 'Area11.Tests/CustomerCurrentAccessTests.cs').read_text()
    if tests.count('[Fact]') != 24: FAILURES.append('Portion18 requires all24 written regression facts')
    for name in ('CollectionDoesNotRequireCostOrProfitAndReconciles', 'AuditFailureRollsBackCollectionAndBalanceWithSafeError',
                 'RevocationAfterReadBlocksCollectionAndReprint', 'ProfitGrantWithoutCustomersCannotReadLifetimeProfit',
                 'ProductsOnlyCanReadAndEditRepButNotCustomerSummaries', 'RepAuditUsesCurrentStoredName'):
        if name not in tests: FAILURES.append(f'Customer/rep current-boundary regression missing {name}')
    REPORT.append('Phase4 portion18: current transactional customer collection/rep actor, current Customers doctor/profit and Products rep readers, safe ledger errors/fresh profile and stale UI timers;24 facts written, Windows/runtime/globalPhase4 OPEN')


def check_recovery_monthly_boundaries():
    """Portion19 structural guard, deliberately not a runtime or authentication claim."""
    recovery = (DOTNET / 'Area11.Core/Services/RecoveryService.cs').read_text()
    tax = (DOTNET / 'Area11.Core/Services/TaxService.cs').read_text()
    def need(source, tokens, label):
        for token in tokens:
            if token not in source: FAILURES.append(f'{label}: missing {token}')
    for source, method, gate, query in (
        (recovery, 'Report', 'LedgerReadAccess.Require(_db, connection, _permissions, user, Modules.Customers);', 'return ServiceResult<RecoveryReport>.Success(Build('),
        (recovery, 'Slip', 'LedgerReadAccess.Require(_db, connection, _permissions, user, Modules.Customers);', 'var payment = _db.QuerySingle<PaymentRow>'),
        (tax, 'Report', 'SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Reports);', 'var rows = _db.Query<TaxDayRow>')):
        part = source.split(' ' + method + '(', 1)[1].split('\n    }', 1)[0]
        if part.count(gate) != 1 or not 0 <= part.find(gate) < part.find(query):
            FAILURES.append(f'{method}: exactly one current own-module reader must precede data query')
        if 'CostPolicy' in part or 'ProfitPolicy' in part or '_permissions.Can(user' in part:
            FAILURES.append(f'{method}: ordinary account/monthly reads must use current own-module access without financial prerequisite')
    need(recovery, ['public ServiceResult CheckAccess(User? user)', 'CustomerSettlementLedger.Read(_db, connection, payment.CustomerId, payment.Id)',
                    'WHERE p.id = @id AND p.customer_id IS NOT NULL AND p.sale_id IS NULL AND p.purchase_id IS NULL',
                    'AND p.provisional_return_id IS NULL;', 'ReceivedBy = payment.UserName,'], 'Recovery typed historical slip')
    need(tax, ['public ServiceResult CheckAccess(User? user)', "WHERE s.status IN ('paid', 'partial', 'credit') AND s.date LIKE @like", 'TaxPaisa = rows.Sum(row => row.TaxPaisa)'], 'Stored monthly witness')
    if ' + exception.Message' in tax: FAILURES.append('Monthly report must not expose raw DB exception')
    vm = (DOTNET / 'Area11.Desktop/ViewModels/RecoveryViewModel.cs').read_text()
    need(vm, ['public bool CanRead => _accessAllowed;', 'public void RefreshAccess()', '_services.Recovery.CheckAccess(_user)',
              'private void ClearSensitiveData()', 'if (!_accessAllowed) return;', '_lastPaymentId = 0;', 'BooksMatch = false;',
              'OpeningText = AutoPrintHint = string.Empty;', 'TotalText = OverdueText = Bucket0Text = Bucket1Text = Bucket2Text = Bucket3Text = "hidden";',
              'if (!_reportLoaded)', 'SAVED, but the recovery list could not refresh. Do not receive again;', 'if (Selected is null) ClearSelection();'], 'Recovery stale-state/save-result')
    printing = vm.split('private (bool Ok, string? Error) PrintSlip(', 1)[1].split('public void RefreshAccess()', 1)[0]
    need(printing, ['_services.Recovery.SlipDocument(paymentId, _user)', 'if (!document.Ok || document.Value is null)', 'return (false, document.Error', '_services.Printer.Print(document.Value)'], 'Fail-closed actual slip')
    if 'new ReceiptDocument' in vm: FAILURES.append('Failed Recovery slip must not hand printer a fabricated empty document')
    tax_vm = (DOTNET / 'Area11.Desktop/ViewModels/TaxViewModel.cs').read_text()
    need(tax_vm, ['public void RefreshAccess()', '_services.Tax.CheckAccess(_user)', 'SettingLabel = string.Empty;', 'GrossText = RefundText = "hidden";'], 'Monthly stale-state')
    for method, end in [('Load', 'private void Previous'), ('Copy', 'public void RefreshAccess')]:
        part = tax_vm.split(' ' + method + '()', 1)[1].split(end, 1)[0]
        need(part, ['_services.Tax.Report(_user, _year, _month)', 'ClearReport();'], f'Monthly {method} fresh read and failure cleanup')
    for name in ('Recovery', 'Tax'):
        view = (DOTNET / f'Area11.Desktop/Views/{name}View.xaml.cs').read_text()
        need(view, ['TimeSpan.FromSeconds(3)', 'Loaded +=', 'Unloaded +=', '_accessTimer.Stop()', 'viewModel.RefreshAccess()'], f'{name} timer lifecycle')
    tests = (DOTNET / 'Area11.Tests/RecoveryReportAccessTests.cs').read_text()
    if tests.count('[Fact]') != 24: FAILURES.append('Portion19 needs 24 written regression facts')
    need(tests, ['RecoveryRevocationBlocksFreshReportAndPreparedSlipId', 'ReportRevocationBlocksFreshTextPreparationSource',
                 'MainOffAndFinancialDenialsDoNotBlockOrdinaryModuleReports', 'LaterCollectionDoesNotRewriteOldSlipBalance',
                 'MonthReadsStoredAmountsWithoutEnablingOrRecalculatingTax', 'FreshReportAndSlipReadsDoNotWriteFinancialOrAuditRows'], 'Portion19 regressions')
    REPORT.append('Phase4 portion19: current own-module Recovery/monthly report/slip, fresh copy and failed read/active-view stale cleanup, real-document-only print;24 facts written, C#/Windows/fullPhase4 OPEN')


def check_parent_roster_boundaries():
    """Portion20 source contracts only, not a compiler/session or live-view guarantee."""
    def need(source, tokens, label):
        for token in tokens:
            if token not in source: FAILURES.append(f'{label}: missing {token}')
    auth = (DOTNET / 'Area11.Core/Services/AuthService.cs').read_text()
    roster = auth.split('public ServiceResult<List<User>> ListUsers(User? actor)', 1)[1].split('public ServiceResult<List<User>> ListHandoverPeople', 1)[0]
    if not 0 <= roster.find('RequireStaffReader(connection, actor);') < roster.find('_db.Query<User>'):
        FAILURES.append('Administrative roster needs current owner before SELECT')
    need(auth, ['private User RequireStaffReader(SqliteConnection connection, User? actor)',
                'actor is not { Active: true, IsOwner: true } || actor.Id <= 0',
                'return OwnerAccess.Require(_db, connection, actor);', 'public ServiceResult CheckStaffAccess(User? actor)'], 'Current roster owner')
    picker = auth.split('public ServiceResult<List<User>> ListHandoverPeople(User? actor)', 1)[1].split('public ServiceResult CheckStaffAccess', 1)[0]
    gate = 'SnapshotReadAccess.Require(_db, connection, permissions, actor, Modules.Cash);'
    if not 0 <= picker.find(gate) < picker.find('_db.Query<User>'):
        FAILURES.append('Operational picker needs current Cash reader before SELECT')
    need(picker, ['SELECT id, name, role, active FROM users WHERE active = 1 AND id <> @id', 'new { id = reader.Id }'], 'Bound active other-person roster')
    if 'public ServiceResult<List<User>> ListUsers()' in auth:
        FAILURES.append('No actorless administrative roster API may remain')
    for part in (roster, picker):
        if 'password_hash' in part or 'pin_hash' in part or '_audit.Record(' in part or ' + exception.Message' in part:
            FAILURES.append('Roster reads must not query secrets/write audit/expose raw DB exceptions')
    service = (DOTNET / 'Area11.Core/Services/PartnerAccessService.cs').read_text()
    need(service, ['SnapshotReadAccess.Require(_db, connection, _permissions, user,', 'Modules.Customers, Modules.Suppliers);',
                   'Customers = _permissions.Can(reader, Modules.Customers)', 'Suppliers = _permissions.Can(reader, Modules.Suppliers)'], 'Current partner parent')
    if 'CostPolicy' in service or 'ProfitPolicy' in service:
        FAILURES.append('Partner navigation must not gain financial prerequisite')
    model = (DOTNET / 'Area11.Core/Models/PartnerAccess.cs').read_text()
    need(model, ['0 or 1 when Customers => requested', '2 when Suppliers => 2', '_ => Customers ? 0 : Suppliers ? 2 : -1'], 'Allowed physical tab index')
    parent = (DOTNET / 'Area11.Desktop/ViewModels/PartnersViewModel.cs').read_text()
    need(parent, ['public void RefreshAccess()', '_services.PartnerAccess.Read(_user)', 'result.Value : new PartnerAccess()',
                  'if (!next.Customers', 'if (!next.Suppliers', '_customers = null;', '_recovery = null;', '_suppliers = null;',
                  'Unwatch(_customers);', 'Unwatch(_recovery);', 'Unwatch(_suppliers);', '_access.SelectTab(value)',
                  '0 when HasCustomers', '1 when HasRecovery', '2 when HasSuppliers', 'if (_customers is null)', 'if (_recovery is null)', 'if (next.Suppliers && _suppliers is null)'], 'Parent detach/retain/fallback')
    settings = (DOTNET / 'Area11.Desktop/ViewModels/SettingsViewModel.cs').read_text()
    need(settings, ['public void RefreshStaffAccess()', '_services.Auth.CheckStaffAccess(_user)', '_services.Auth.ListUsers(_user)',
                    'public bool CanManageStaff => _staffAccessAllowed;', 'public bool CanManageFinancialVisibility => _staffAccessAllowed;',
                    'private void ClearStaffData()', '_selectedUser = null;', 'Users.Clear();', 'Permissions.Clear();',
                    'PagePermissions.Clear();', 'FinancialPermissions.Clear();', 'ClearPricing();',
                    'NewStaffName = NewStaffPin = ResetStaffSecret = NewRecoveryWord = string.Empty;',
                    '!Permissions.Contains(toggle)', '!Users.Contains(value)'], 'Owner staff UI cleanup')
    load = settings.split('public void LoadUsers()', 1)[1].split('public void RefreshStaffAccess()', 1)[0]
    need(load, ['if (!UpdateStaffAccess()) return;', 'ClearStaffData();'], 'Failed roster must clear stale data')
    for method in ('OpenStaffPhotos', 'AddStaff', 'ToggleActive', 'ResetSecret', 'SaveRecoveryWord', 'ResetPermissions'):
        part = settings.split('private void ' + method + '()', 1)[1].split('\n    }', 1)[0]
        need(part, ['if (!UpdateStaffAccess()) return;'], 'Fresh owner staff command ' + method)
    shift = (DOTNET / 'Area11.Desktop/ViewModels/ShiftViewModel.cs').read_text()
    need(shift, ['public void RefreshPeople()', '_services.Auth.ListHandoverPeople(_user)', 'NextPeople.Clear();'], 'Operational picker UI')
    if '_services.Auth.ListUsers()' in settings or '_services.Auth.ListUsers()' in shift:
        FAILURES.append('All administrative roster callers must explicitly supply current actor/purpose')
    for name, timer, method in [('Partners', '_accessTimer', 'RefreshAccess'), ('Settings', '_staffAccessTimer', 'RefreshStaffAccess'), ('Shift', '_accessTimer', 'RefreshPeople')]:
        view = (DOTNET / f'Area11.Desktop/Views/{name}View.xaml.cs').read_text()
        need(view, ['TimeSpan.FromSeconds(3)', 'Loaded +=', 'Unloaded +=', timer + '.Stop()', 'viewModel.' + method + '()'], name + ' timer lifecycle')
    tests = (DOTNET / 'Area11.Tests/ParentRosterAccessTests.cs').read_text()
    if tests.count('[Fact]') != 28: FAILURES.append('Portion20 requires 28 written regression facts')
    need(tests, ['ParentRechecksRevokeAndRestoreOnSameService', 'OwnerRosterIncludesInactiveAccountsWithoutSecretFields',
                 'CashDelegationAllowsOperationalPickerButNotAdministration', 'PickerRechecksCashRevocationAndRestore',
                 'ParentAndBothRosterReadsDoNotWriteAuditOrAccountData', 'SupplierOnlyAlwaysSelectsItsPhysicalIndexTwo'], 'Parent/roster regressions')
    REPORT.append('Phase4 portion20: current Customers/Suppliers parent tabs with detach/fallback, current-owner administrative roster vs current-Cash active-person picker, stale staff state cleanup/timers;28 facts written, fullSettings/galla/session/Windows/globalPhase4 OPEN')


def check_shift_current_boundaries():
    """Portion21 structural contracts, never C#/concurrency/Windows certification."""
    def need(source, tokens, label):
        for token in tokens:
            if token not in source: FAILURES.append(f'{label}: missing {token}')
    core = (DOTNET / 'Area11.Core/Services/ShiftService.cs').read_text()
    gate = 'var reader = RequireCash(connection, user);'
    for name in ('Open', 'Close', 'Handover'):
        part = core.split(' ' + name + '(', 1)[1].split('\n    }', 1)[0]
        if not 0 <= part.find('return _db.Tx(connection =>') < part.find(gate) < part.find('ReadOpen(connection)'):
            FAILURES.append(f'Shift {name}: current Cash actor must be inside Tx before drawer lookup')
        need(part, ['connection, reader, "Cash"'], 'Stored shift actor ' + name)
        if name == 'Open': need(part, ['userId = reader.Id', 'throw new ValidationException("The shift could not be opened.'], 'Opening rollback/actor')
        if name == 'Close': need(part, ['shift.UserId != reader.Id && !reader.CanManage', 'RecordDiscrepancy(connection'], 'Closing current senior/reason')
        if name == 'Handover':
            recipient = 'var recipient = RequireCash(connection, next);'
            if not 0 <= part.find(recipient) < part.find('ReadOpen(connection)'):
                FAILURES.append('Handover needs current same-role active Cash recipient inside transaction')
            need(part, ['GivenBy = reader.Name.Trim()', 'GivenTo = recipient.Name.Trim()', 'userId = recipient.Id',
                        'openingFloat = countedPaisa', '?? throw new ValidationException("The recorded handover',
                        'throw new ValidationException("The next shift could not be opened.'], 'Handover result/rollback')
        if 'CostPolicy' in part or 'ProfitPolicy' in part or '_permissions.Can(user' in part:
            FAILURES.append('Ordinary Cash operation must use current Cash only, not financial or cached-role shortcut')
    cash = core.split('private User RequireCash(', 1)[1].split('private static bool HasDifferenceReason', 1)[0]
    need(cash, ['actor is not { Active: true } || actor.Id <= 0', 'current.Kind != actor.Kind',
                'if (current.IsOwner) return current;', 'SELECT allowed FROM user_permissions WHERE user_id = @userId AND module_key = @module;',
                'new { userId = current.Id, module = Modules.Cash }', 'PermissionService.RoleDefault(current.Kind, Modules.Cash)'], 'Same-connection Cash reader')
    if '_db.Open(' in cash or '_permissions.Can(' in cash or 'catch (' in cash:
        FAILURES.append('Cash authorization must use provided connection and propagate grant query errors, not cross-connection/default fallback')
    need(core, ['shift.ExpectedPaisa = shift.OpeningFloatPaisa + shift.CashSalesPaisa + shift.CashCollectionsPaisa',
                'sql.Replace(">= @from", "> @from", StringComparison.Ordinal)'], 'Preserved drawer formula/boundary')
    page = core.split('public ServiceResult<ShiftPage> Page(', 1)[1].split('\n    }', 1)[0]
    if not 0 <= page.find(gate) < page.find('ReadOpen(connection)'):
        FAILURES.append('Drawer Page must current-check before any money lookup')
    need(page, ['Previous = ReadLastCounted(connection)', 'History = ReadHistory(connection, limit)',
                'Discrepancies = ReadDiscrepancies(connection, limit)', 'CanSeePeople = reader.CanManage', 'ServiceResult<ShiftPage>.Fail'], 'Known page vs failed read')
    for old in ('public ShiftMoney? Current()', 'public ShiftMoney? LastCounted()', 'public List<ShiftMoney> History(', 'public List<ShiftDiscrepancy> Discrepancies('):
        if old in core: FAILURES.append('Actorless financial shift read must not remain: ' + old)
    state = core.split('public ServiceResult<bool> DrawerState(', 1)[1].split('\n    }', 1)[0]
    need(state, ['SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Cash, Modules.Pos)',
                 'SELECT COUNT(1) FROM shifts WHERE status =', 'ServiceResult<bool>.Fail'], 'State-only Cash/POS read')
    if 'ReadOpen(' in state or 'FillDrawer(' in state: FAILURES.append('POS state check must not fetch drawer money')
    people = core.split('public ServiceResult<ShiftPersonReport> ByPerson(', 1)[1].split('\n    }', 1)[0]
    if not 0 <= people.find(gate) < people.find('if (!reader.CanManage)') < people.find('_db.Query<ShiftPersonSummary>'):
        FAILURES.append('ByPerson requires current Cash + stored senior role before monetary aggregates')
    vm = (DOTNET / 'Area11.Desktop/ViewModels/ShiftViewModel.cs').read_text()
    need(vm, ['_services.Shifts.Page(_user)', 'public bool CanWork => _accessAllowed;', 'public bool CanSeePeople => _canSeePeople;',
              'if (!UpdateAccess()) return;', '_services.Shifts.CheckAccess(_user)', 'private void ClearSensitiveData()',
              '_lastHandover = null;', 'History.Clear();', 'Discrepancies.Clear();', 'PeopleSummary.Clear();',
              'SelectedPerson = null;', 'OpenFloatText = ActualText = HandoverCashText = string.Empty;',
              '= ExpensesText = DrawingsText = ExpectedText = "hidden";', 'Handover SAVED,', 'Opening SAVED,', 'Closing SAVED,'], 'Shift stale data/output/save-result')
    for name in ('HandOver', 'PrintSlip', 'SendSlipOnWhatsApp', 'OpenShift', 'CloseShift'):
        part = vm.split('private void ' + name + '()', 1)[1].split('\n    }', 1)[0]
        need(part, ['if (!UpdateAccess()) return;'], 'Shift fresh action ' + name)
    pos = (DOTNET / 'Area11.Desktop/ViewModels/PosViewModel.cs').read_text()
    need(pos, ['_services.Shifts.DrawerState(_user)', 'ShiftClosed = !state.Ok || !state.Value;', 'BlockedText = !state.Ok ?'], 'Counter unknown status blocks, not closed claim')
    shell = (DOTNET / 'Area11.Desktop/ViewModels/ShellViewModel.cs').read_text()
    need(shell, ['_services.Shifts.DrawerState(_user)', 'if (!drawer.Ok || drawer.Value)', 'Drawer state was unknown during session expiry'], 'Session status-only known open')
    note = (DOTNET / 'Area11.Core/Utils/SessionHandover.cs').read_text()
    need(note, ['The drawer needs checking after ', 'signed out at '], 'Conservative session count note')
    tests = (DOTNET / 'Area11.Tests/ShiftCurrentAccessTests.cs').read_text()
    if tests.count('[Fact]') != 35: FAILURES.append('Portion21 requires35 written regression facts')
    need(tests, ['OpenAuditFaultRollsBackInsertedShift', 'CloseAuditFaultRestoresOpenShift',
                 'HandoverDiscrepancyFaultRollsBackUpdateInsertAndAudits', 'PosOnlyGetsStateButNotDrawerMoney',
                 'DatabaseFailureIsNotSuccessfulClosedOrEmpty', 'TwoDatabaseInstancesCannotBothOpenTheDrawer',
                 'HandoverPostwriteRereadFailureRollsBackBothHalves'], 'Shift regression suite')
    REPORT.append('Phase4 portion21: current Cash Page/ByPerson, POS/session state-only unknown-aware reads, transactional Open/Close and current actor+recipient Handover, stale UI/cached output cleanup;35 facts written, real concurrency/Windows/session/globalPhase4 OPEN')


def check_custom_value_current_boundaries():
    """Bounded custom-value identity/record/editor and native stale metadata source contracts."""
    core = DOTNET / 'Area11.Core/Services'
    fields = (core / 'CustomFieldService.cs').read_text()
    native = (core / 'NativeFieldPolicyService.cs').read_text()
    editor = (DOTNET / 'Area11.Desktop/ViewModels/CustomFieldsEditorViewModel.cs').read_text()
    policy_vm = (DOTNET / 'Area11.Desktop/ViewModels/NativeFieldsViewModel.cs').read_text()
    view = (DOTNET / 'Area11.Desktop/Views/CustomFieldsEditorView.xaml').read_text()
    tests = (DOTNET / 'Area11.Tests/CustomValueCurrentAccessTests.cs').read_text()
    native_tests = (DOTNET / 'Area11.Tests/NativeFieldCurrentAccessTests.cs').read_text()
    for name in ('Values', 'DescribeValues'):
        marker = re.search(r'public ServiceResult<[^\n]+?>\s+' + name + r'\(', fields)
        body = fields[marker.end():].split('\n    public ',1)[0] if marker else ''
        gate, query = body.find('RequireValueRecord(connection, key, entityId);'), body.find('var rows = _db.Query' if name == 'Values' else 'var lines = _db.Query')
        if not 0 <= gate < query: FAILURES.append(f'Custom {name} requires existing positive parent before answer query')
    internal = fields.split('internal ServiceResult SaveValuesInTransaction(',1)[-1].split('public ServiceResult CheckValueAccess(',1)[0]
    stages = [internal.find(t) for t in ('!CustomFieldEntity.IsKnown(key) || entityId <= 0',
              'key = CustomFieldEntity.Clean(key);', 'var writer = RequireValueActor(connection, user);',
              'if (!RecordExists(connection, key, entityId))', 'var fields = _db.Query', 'foreach (var (field, stored) in prepared)',
              '_audit.Record(connection, writer, CustomFieldEntity.ModuleFor(key), "custom_value_set",')]
    if any(x < 0 for x in stages) or stages != sorted(stages):
        FAILURES.append('Internal custom save must validate known positive record/current stored actor before reads/writes/stored audit')
    actor = fields.split('private User RequireValueActor(',1)[-1].split('private void RequireValueRecord(',1)[0]
    for token in ('actor is not { Active: true } || actor.Id <= 0',
                  'SELECT id, name, role, active FROM users WHERE id = @id;',
                  'current is not { Active: true } || current.Kind != actor.Kind', 'return current;'):
        if token not in actor: FAILURES.append(f'Current custom actor resolution missing {token}')
    if 'SnapshotReadAccess.Require' in internal or 'SnapshotReadAccess.Require' in actor or '_permissions.Can' in actor:
        FAILURES.append('Internal quick-create answer path must not invent a new module gate; caller owns workflow authorization')
    preflight = fields.split('public ServiceResult CheckValueAccess(',1)[-1].split('private User RequireValueActor(',1)[0]
    for token in ('!CustomFieldEntity.IsKnown(entity) || entityId < 0',
                  'SnapshotReadAccess.Require(_db, connection, _permissions, user, CustomFieldEntity.ModuleFor(key));',
                  'if (entityId > 0) RequireValueRecord(connection, key, entityId);'):
        if token not in preflight: FAILURES.append(f'Custom editor current access missing {token}')
    if '"The answers could not be loaded. " + exception.Message' in fields or '"The answers could not be saved. " + exception.Message' in fields:
        FAILURES.append('Custom answer infrastructure failures must not expose provider exception text')
    for token in ('_canEdit = false;', 'public bool ShowPanel => HasFields || HasStatus;',
                  'if (!RefreshAccess() || !LoadedSuccessfully) return values;',
                  'if (values is null || !RefreshAccess() || !LoadedSuccessfully) return;',
                  'RefreshAccess() && HasStagedPhoto', 'if (!RefreshAccess() || !LoadedSuccessfully) return;',
                  'SAVED. Extra answers were stored, but could not be reloaded.'):
        if token not in editor: FAILURES.append(f'Custom editor current output/save boundary missing {token}')
    cleanup = editor.split('public bool RefreshAccess()',1)[-1].split('private void SetStatus(',1)[0]
    for token in ('_services.CustomFields.CheckValueAccess(_user, _entity, _entityId)',
                  'CanEdit = false;', 'LoadedSuccessfully = false;', 'Rows.Clear();',
                  '_hasPhotoField = false;', 'ClearStagedPhoto();', 'RaiseListState();', 'RaiseStagedState();'):
        if token not in cleanup: FAILURES.append(f'Custom editor denial must drop stale data: {token}')
    if 'Visibility="{Binding ShowPanel, Converter={StaticResource BoolToVis}}"' not in view:
        FAILURES.append('Custom editor failure status must remain visible after rows cleared')
    for name, action in (('CustomFieldsEditorView','(DataContext as CustomFieldsEditorViewModel)?.RefreshAccess()'),
                         ('NativeFieldsView','(DataContext as NativeFieldsViewModel)?.RefreshAccess()')):
        lifecycle = (DOTNET / f'Area11.Desktop/Views/{name}.xaml.cs').read_text()
        for token in ('TimeSpan.FromSeconds(3)', action, 'accessTimer.Start()', 'Unloaded += (_, _) => accessTimer.Stop();'):
            if token not in lifecycle: FAILURES.append(f'{name} loaded access timer missing {token}')
    if native.count('!user.Active || user.Id <= 0') != 2:
        FAILURES.append('Native policy supplied active-positive owner preflight required on read/write')
    for token in ('Selected = null;', 'Fields.Clear();', 'if (!_lastLoadSucceeded) return;',
                  'SAVED. The preference was stored, but fields could not be reloaded.'):
        if token not in policy_vm: FAILURES.append(f'Native metadata stale/failure boundary missing {token}')
    for start, end in (('public void Refresh()', 'public bool RefreshAccess()'),
                       ('public bool RefreshAccess()', 'private void Change(bool required)')):
        body = policy_vm.split(start,1)[-1].split(end,1)[0]
        for token in ('Selected = null;', 'Fields.Clear();', '_lastLoadSucceeded = false;'):
            if token not in body: FAILURES.append(f'Native metadata failed {start} must clear stale data: {token}')
    allowed_tick = policy_vm.split('public bool RefreshAccess()',1)[-1].split('private void Change(bool required)',1)[0]
    if 'if (result.Ok && result.Value is not null) return true;' not in allowed_tick:
        FAILURES.append('Native allowed timer check must not churn its grid/selection')
    change = policy_vm.split('private void Change(bool required)',1)[-1]
    if not 0 <= change.find('Refresh();') < change.find('MessageBox.Show('):
        FAILURES.append('Native change must reload current metadata before risk prompt')
    if tests.count('[Fact]') != 26 or native_tests.count('[Fact]') != 6:
        FAILURES.append('Portion25 requires26custom+6native isolated regression facts')
    for name in ('InternalQuickCustomerCreationDoesNotAddCustomersGrantRequirement',
                 'InternalCreationChecksActorEvenWhenNoAnswersWereSupplied',
                 'MissingParentIsNotCertifiedEmptyOrOrphanAnswerOutput',
                 'AuditFaultRollsBackAllAnswersAndParentCreation',
                 'InternalCustomValueAuditUsesStoredCurrentActorName'):
        if 'void ' + name + '(' not in tests: FAILURES.append(f'Custom current regression missing {name}')
    REPORT.append('Phase4 portion25: current internal custom-answer actor/stored audit,existing-parent read checks,stale editor/output and native metadata cleanup;32 facts written/1977cases UNRUN,caller policy/session/records/Windows/fullPhase4 OPEN')

def check_custom_manager_current_boundaries():
    """Number8 bounded definition manager source contracts; not WPF or atomic concurrency proof."""
    core = DOTNET / 'Area11.Core'
    service = (core / 'Services/CustomFieldService.cs').read_text()
    state = (core / 'Models/CustomFieldDefinitionState.cs').read_text()
    vm = (DOTNET / 'Area11.Desktop/ViewModels/CustomFieldsViewModel.cs').read_text()
    view = (DOTNET / 'Area11.Desktop/Views/CustomFieldsView.xaml').read_text()
    window = (DOTNET / 'Area11.Desktop/Views/CustomFieldsView.xaml.cs').read_text()
    tests = (DOTNET / 'Area11.Tests/CustomDefinitionCurrentAccessTests.cs').read_text()
    if service.count('!user.Active || user.Id <= 0') != 4:
        FAILURES.append('All four definition writers require supplied active-positive Owner before existing stored-owner Tx checks')
    if '(manage || includeInactive) && (user is not { Active: true } || user.Id <= 0)' not in service:
        FAILURES.append('Management/inactive definition list requires supplied active-positive Owner')
    for text in ('The extra fields could not be loaded.', 'The field could not be added.',
                 'The field could not be changed.', 'The field could not be moved.'):
        if '"' + text + ' " + exception.Message' in service:
            FAILURES.append('Definition infrastructure error exposes provider details: ' + text)
    for field in ('Id', 'Entity', 'FieldKey', 'Label', 'Type', 'OptionsJson', 'HelpText', 'Required', 'Active', 'Sort', 'CreatedAt'):
        if not re.search(r'expected\.' + field + r'(?: == current\.' + field + r'|, current\.' + field + r', StringComparison.Ordinal)', state):
            FAILURES.append('Persisted manager snapshot comparison missing ' + field)
    if 'expected is not null && current is not null' not in state:
        FAILURES.append('Missing definitions must not compare as matching')
    if 'public bool CanManage => _user.IsOwner;' in vm:
        FAILURES.append('Definition manager must not enable commands from stale supplied Owner role')
    for token in ('public bool HasNoFields => _lastLoadSucceeded && CanManage && Fields.Count == 0;',
                  'public void Refresh() => Load();',
                  'if (_updatingList) return;', 'value.Field.Entity != SelectedTab.Entity',
                  'if (!RefreshAccess() || !Fields.Contains(value)) return;'):
        if token not in vm: FAILURES.append('Definition manager lifecycle/selection boundary missing ' + token)
    cleanup = vm.split('private void ClearManager(',1)[-1].split('private void ReplaceRows(',1)[0]
    for token in ('CanManage = false;', '_lastLoadSucceeded = false;',
                  'ReplaceRows(Array.Empty<CustomField>(), 0);', 'ResetEditor();', 'CountText = "Fields not loaded";', 'RaiseListState();'):
        if token not in cleanup: FAILURES.append('Definition denial must clear all stale state: ' + token)
    reset = vm.split('private void ResetEditor()',1)[-1].split('private void Edit(',1)[0]
    for token in ('_editingId = 0;', 'IsEditing = false;', '_selectedField = null;',
                  'LabelText = string.Empty;', 'OptionsText = string.Empty;', 'HelpText = string.Empty;',
                  'Required = false;', 'SelectedType = Types[0];', 'RaiseSelection();'):
        if token not in reset: FAILURES.append('Definition denial/editor reset missing ' + token)
    refresh = vm.split('public bool RefreshAccess()',1)[-1].split('private void ClearManager(',1)[0]
    for token in ('includeInactive: true, manage: true', 'ClearManager(',
                  'CustomFieldDefinitionState.Matches(_selectedField?.Field, current)',
                  'The selected field changed or disappeared.', 'ResetEditor();',
                  'Fields.Count != result.Value.Count || Fields.Where((row, index) =>',
                  'CustomFieldDefinitionState.Matches(row.Field, result.Value[index])'):
        if token not in refresh: FAILURES.append('Definition current snapshot conflict/stable-tick boundary missing ' + token)
    for method in ('BeginNew()', 'Save()', 'Move(int direction)', 'ToggleActive()', 'UseSuggestion(CustomFieldSuggestion? suggestion)'):
        body = vm.split('void ' + method,1)[-1].split('}',1)[0]
        if 'if (!RefreshAccess()) return;' not in body:
            FAILURES.append('Definition action requires current management/snapshot check: ' + method)
    if vm.count('FinishWrite(') != 4:
        FAILURES.append('Definition Add/Update,Move,Toggle must all use shared postcommit reload verdict')
    for token in ('if (!_lastLoadSucceeded || _selectedField?.Field.Id != savedId)',
                  'SAVED. The field change was stored, but could not be reloaded.',
                  'suggestion is null || !Types.Any(option => option.Key == suggestion.Type)'):
        if token not in vm: FAILURES.append('Definition postcommit/document suggestion guard missing ' + token)
    if 'Visibility="{Binding HasNoFields, Converter={StaticResource BoolToVis}}"' not in view:
        FAILURES.append('Definition failure must not show successful empty-list overlay')
    for token in ('TimeSpan.FromSeconds(3)', '(DataContext as CustomFieldsViewModel)?.RefreshAccess()',
                  'accessTimer.Start()', 'Unloaded += (_, _) => accessTimer.Stop();'):
        if token not in window: FAILURES.append('Definition manager loaded/unloaded lifecycle missing ' + token)
    if tests.count('[Fact]') != 32:
        FAILURES.append('Definition manager needs32 source regression facts (14 snapshot +18 real-schema Core)')
    for name in ('InactiveSuppliedOwnerCannotManageDefinitions','AllDefinitionWritesAuditStoredCurrentOwnerName',
                 'MoveAuditFailureRollsBackWholeOrdering','HideRestoreAndRenamePreserveStableKeyAndHistoricalAnswer',
                 'ChangedActiveFlagDoesNotMatch','ChangedOrderDoesNotMatch'):
        if 'void ' + name + '(' not in tests: FAILURES.append('Definition regression missing ' + name)
    tracker = (ROOT / 'docs/NUMBERED-WORK-STATUS-URDU.md').read_text()
    if [int(x) for x in re.findall(r'^\|(\d+)۔', tracker, re.M)] != list(range(1, 37)):
        FAILURES.append('Owner stable1–36 work IDs must remain complete and in order;not36Nexts')
    REPORT.append('Phase4 portion26/number8: current definition manager/stale selection and input cleanup,persisted target comparison,stable allowed ticks,SAVED postcommit reload verdict;32 facts/2009cases UNRUN,not atomic CAS/session/Windows/globalPhase4 certification')

def check_record_write_current_boundaries():
    """Stable5/6/7 source contracts, not executable authorization, rollback or WPF proof."""
    product = (DOTNET / 'Area11.Core/Services/ProductService.cs').read_text()
    partner = (DOTNET / 'Area11.Core/Services/PartnerService.cs').read_text()
    vm = (DOTNET / 'Area11.Desktop/ViewModels/ProductsViewModel.cs').read_text()
    customer = (DOTNET / 'Area11.Desktop/ViewModels/CustomersViewModel.cs').read_text()
    window = (DOTNET / 'Area11.Desktop/Views/ProductEditorWindow.xaml.cs').read_text()
    view = (DOTNET / 'Area11.Desktop/Views/ProductEditorWindow.xaml').read_text()
    tests = (DOTNET / 'Area11.Tests/RecordWriteCurrentAccessTests.cs').read_text()
    def need(text, tokens, label):
        for token in tokens:
            if token not in text: FAILURES.append('Record current boundary ' + label + ': ' + token)
    update = partner.split('public ServiceResult<CustomerEditResult> UpdateCustomer(', 1)[-1].split('private ServiceResult<CustomerEditResult> SaveCustomer(', 1)[0]
    gate = 'actor = SnapshotReadAccess.Require(_db, connection, new PermissionService(_db, _log, _audit), actor, Modules.Customers);'
    need(update, [gate, 'var result = SaveCustomer(connection, actor, input, name, phone, category);', 'if (!result.Ok || customValues is null) return result;'], 'customer null-map writer')
    if gate in update and update.index(gate) > update.index('var result = SaveCustomer('):
        FAILURES.append('Customer current writer must precede SaveCustomer even without optional answers')
    record_writes = product.split('public ServiceResult<List<Category>> GetCategories(', 1)[0]
    if record_writes.count('actor is not { Active: true } || actor.Id <= 0 || !actor.CanManage') != 3:
        FAILURES.append('All three reviewed product writers need active-positive manager preflight')
    if record_writes.count('actor = RequireProductWriter(connection, actor);') != 3:
        FAILURES.append('Create/Update/Deactivate must use stored actor for checks and audit')
    for start, end in (('public ServiceResult<int> Create(', 'public IReadOnlyList<string> MissingRequiredLabels('),
                       ('public ServiceResult Update(', 'public ServiceResult Deactivate('),
                       ('public ServiceResult Deactivate(', 'public ServiceResult CheckEditorAccess(')):
        body = product.split(start, 1)[-1].split(end, 1)[0]
        need(body, ['actor = RequireProductWriter(connection, actor);'], start)
        pos = body.find('actor = RequireProductWriter(connection, actor);')
        first_write = body.find('_db.Execute(connection,')
        if first_write >= 0 and pos > first_write: FAILURES.append('Product stored writer follows mutation: ' + start)
    writer = product.split('private User RequireProductWriter(', 1)[-1].split('private void RequireStoredCostEdit(', 1)[0]
    need(writer, ['SnapshotReadAccess.Require(_db, connection, _permissions, actor, Modules.Products)', 'if (!current.CanManage)'], 'shared product writer')
    need(product, ['SnapshotReadAccess.Require(_db, connection, _permissions, actor, Modules.Products)',
                   'if (!current.CanManage)', 'if (id <= 0) throw new ValidationException',
                   'if (patch.CostPaisa is not null) RequireStoredCostEdit(connection, actor);',
                   'if (active is null) throw new ValidationException',
                   'if (Convert.ToInt64(active) == 0) return;',
                   'SELECT value FROM settings WHERE key = @key;', 'MasterFoundationSettings.ProfitShowMargins',
                   'raw is null || (bool.TryParse(raw, out var flag) ? flag',
                   'System.Globalization.CultureInfo.InvariantCulture, out var number) && number != 0',
                   '!enabled || !_permissions.Can(writer, Modules.PurchaseCost)',
                   'var writer = RequireProductWriter(connection, actor);', 'RequireStoredCostEdit(connection, writer);',
                   'if (active is null || Convert.ToInt64(active) == 0)'], 'Core parent/cost/editor')
    need(customer, ['_editingCustomerId = Selected.Id;',
                    'if (!IsEditing || _editingCustomerId <= 0 || Selected?.Id != _editingCustomerId)',
                    'Id = _editingCustomerId,', 'SAVED. The customer change was stored, but the page could not refresh.'], 'pinned customer form')
    cleanup = customer.split('private void ClearEditForm()', 1)[-1].split('private void ClearSensitiveData()', 1)[0]
    need(cleanup, ['IsEditing = false;', '_editingCustomerId = 0;', 'EditName = EditPhone = EditLimitText = string.Empty;',
                   'EditCategory = "Normal";', 'EditEditor.Load(0);'], 'customer cleanup')
    need(customer.split('private void ClearSensitiveData()',1)[-1], ['ClearEditForm();', 'NewName = NewPhone = string.Empty;', 'NewEditor.Load(0);', 'Editor.Load(0);'], 'customer denied state')
    need(vm, ['_lastProductLoadSucceeded && Items.Count == 0,',
              '_services.Products.CheckEditorAccess(_user, product.Id)',
              'public bool CanSaveEditor => !_editorAccessLost;',
              'if (_editorAccessLost) return false;', '_services.Products.CheckEditorAccess(_user, _editingId)',
              'NewProduct(loadFields: false);', 'else Editor.Load(0);',
              'SAVED. Product and extra fields were stored, but could not refresh.',
              'SAVED. The product was put away, but the list could not refresh.',
              '_services.Log.Error("Product was saved but its editor could not refresh.", exception);',
              'InvalidateEditor(full.Error ??', 'InvalidateEditor("Extra fields could not be loaded.'], 'product stale/SAVED')
    for method in ('Save()', 'OpenBarcodes()', 'OpenHistory()', 'OpenPhotos()', 'DeactivateSelected()'):
        body = vm.split('void ' + method,1)[-1].split('}',1)[0]
        need(body, ['if (!RefreshEditorAccess()) return;'], method)
    if vm.count('if (!RefreshEditorAccess()) return;') < 8:
        FAILURES.append('Product modal needs fresh access after confirmations as well as action entry')
    cleanup = vm.split('private void InvalidateEditor(',1)[-1].split('public void ApplyFilter(',1)[0]
    need(cleanup, ['NewProduct(loadFields: false);', 'CostText = RetailText = VipText = DoctorText = string.Empty;',
                   'BaseUnit = StrengthUnit = string.Empty;', 'Items.Clear();', 'Categories.Clear();', 'Companies.Clear();',
                   'Formulas.Clear();', 'Brands.Clear();', 'Suppliers.Clear();', 'SupplierFilterOptions.Clear();',
                   'CategoryTagOptions.Clear();', 'BaseUnits.Clear();', '_lastProductLoadSucceeded = false;'], 'product cleanup')
    need(window, ['TimeSpan.FromSeconds(3)', '_viewModel.RefreshEditorAccess()', 'accessTimer.Start();', 'accessTimer.Stop();'], 'modal lifetime')
    need(view, ['IsEnabled="{Binding CanSaveEditor}"'], 'save button')
    if tests.count('[Fact]') != 36: FAILURES.append('Reviewed record writes need36 real-schema regression facts')
    need(tests, ['FreshMainReadDoesNotTrustInjectedSettingsCache', 'ReviewedWritesAuditStoredCurrentActorName',
                 'CustomerAuditFailureRollsBackNullMapEdit', 'ProductEditAuditFailureRollsBackTagsPriceAndName',
                 'ProductCreateAuditFailureRollsBackPhotoAndOnlyNewFile', 'ProductHideAuditFailureKeepsProductActive',
                 'RevokedCustomersGrantBlocksNullMapCustomerEdit', 'NonCostCorePatchRemainsAllowedWithoutCostGrant',
                 'NewProductTypedCostWorkflowDoesNotRequireStoredCostGrant'], 'regressions')
    REPORT.append('Phase4 portion27/stable5,6,7: current customer null-map writer,pinned editor;stored product writer/audit,parent checks,fresh stored-cost gate,stale modal cleanup/SAVED;36 facts/2045cases UNRUN,not authenticated session/CAS/WPF/globalPhase4 proof')

def check_catalog_current_boundaries():
    """Portion28 lexical contracts. Not C# transaction/session or WPF execution."""
    product = (DOTNET / 'Area11.Core/Services/ProductService.cs').read_text()
    methods = ('AddCategory', 'UpdateCategory', 'SetCategoryActive', 'AddCompany', 'UpdateCompany',
               'SetCompanyActive', 'AddFormula', 'AddBrand', 'UpdateFormula', 'UpdateBrand',
               'SetFormulaActive', 'SetBrandActive', 'SetBrandCompany', 'AddUnit', 'UpdateUnit', 'SetUnitActive')
    for method in methods:
        match = re.search(r'    public ServiceResult(?:<int>)? ' + method + r'\(', product)
        if not match:
            FAILURES.append('Catalog writer missing ' + method); continue
        body = product[match.start():].split('\n    public ', 1)[0]
        for token in ('actor is not { Active: true } || actor.Id <= 0 || !actor.CanManage',
                      'return _db.Tx(connection =>\n            {\n                actor = RequireProductWriter(connection, actor);',
                      'catch (ValidationException exception)'):
            if token not in body: FAILURES.append('Current catalog writer ' + method + ' missing ' + token)
        generic = body.split('catch (Exception exception)',1)[-1]
        if '+ exception.Message' in generic: FAILURES.append('Catalog provider details exposed: ' + method)
    capability = product.split('public ServiceResult<bool> CheckCatalogAccess(',1)[-1].split('public ServiceResult<List<Category>> GetCategories',1)[0]
    for token in ('SnapshotReadAccess.Require(_db, connection, _permissions, actor, Modules.Products)',
                  'ServiceResult<bool>.Success(reader.CanManage)', 'Catalog access could not be checked.'):
        if token not in capability: FAILURES.append('Catalog current reader/capability missing ' + token)
    if 'PurchaseCost' in capability or 'ProfitShowMargins' in capability:
        FAILURES.append('Catalog access must not gain financial requirements')
    for name, actions, cleanup in (
        ('Catalog', ('NewCategory','SaveCategory','ToggleCategory','NewCompany','SaveCompany','ToggleCompany',
                     'NewFormula','SaveFormula','ToggleFormula','NewBrand','SaveBrand','ToggleBrand','ClearBrandCompany'),
         ('SelectedCategory = null;', 'SelectedCompany = null;', 'SelectedFormula = null;', 'SelectedBrand = null;',
          'SelectedParent = null;', 'SelectedBrandCompany = null;', '_brandCompanyId = null;',
          'Categories.Clear(); ParentOptions.Clear(); Companies.Clear();', 'Formulas.Clear(); Brands.Clear(); ActiveCompanies.Clear();',
          'CategoryName = CompanyName = FormulaName = BrandName = string.Empty;',
          'CategoryOrderText = CompanyOrderText = FormulaOrderText = BrandOrderText = string.Empty;', 'Catalogs not loaded')),
        ('Unit', ('NewUnit','Save','Toggle'), ('SelectedUnit = null;', 'Units.Clear();', 'Name = OrderText = string.Empty;', 'WholeOnly = false;'))):
        vm = (DOTNET / ('Area11.Desktop/ViewModels/' + name + 'WindowViewModel.cs')).read_text()
        for token in ('CanEdit = user.CanManage;', 'public bool CanEdit { get; }'):
            if token in vm: FAILURES.append('Catalog stale supplied-role capability: ' + name)
        for method in actions:
            if 'void ' + method + '()\n    {\n        if (!RequireEdit()) return;' not in vm:
                FAILURES.append('Catalog current UI action missing: ' + name + '.' + method)
        for token in ('_services.Products.CheckCatalogAccess(_user)', 'CanEdit = _lastLoadSucceeded && access.Value;',
                      'if (!RefreshAccess()) return false;', '_lastLoadSucceeded &&', '!_lastLoadSucceeded || !',
                      'SAVED. The ' + ('catalog' if name == 'Catalog' else 'unit') + ' change was stored, but could not reload.',
                      'Do not repeat the action; refresh and review first.'):
            if token not in vm: FAILURES.append('Catalog current UI lifecycle ' + name + ': ' + token)
        names = ('Categories', 'Companies', 'Formulas', 'Brands') if name == 'Catalog' else ('Units',)
        for rows in names:
            if 'public bool HasNo' + rows + ' => _lastLoadSucceeded && ' + rows + '.Count == 0;' not in vm:
                FAILURES.append('Failed catalog read must not be certified empty: ' + rows)
        clear = vm.split('private void ClearManager(',1)[-1].split('private bool ReloadAfterWrite(',1)[0]
        for token in ('CanEdit = false;', '_lastLoadSucceeded = false;', '_loading = true;', '_loading = false;') + cleanup:
            if token not in clear: FAILURES.append('Catalog denial cleanup ' + name + ': ' + token)
        refresh = vm.split('public bool RefreshAccess()',1)[-1].split('private bool RequireEdit()',1)[0]
        if 'Load(' in refresh: FAILURES.append('Access-only timer must preserve allowed authored edits: ' + name)
        window = (DOTNET / ('Area11.Desktop/Views/' + name + 'Window.xaml.cs')).read_text()
        for token in ('TimeSpan.FromSeconds(3)', 'viewModel.RefreshAccess()', 'accessTimer.Start();', 'Closed += (_, _) => accessTimer.Stop();'):
            if token not in window: FAILURES.append('Catalog timer lifetime ' + name + ': ' + token)
    catalog = (DOTNET / 'Area11.Desktop/ViewModels/CatalogWindowViewModel.cs').read_text()
    unit = (DOTNET / 'Area11.Desktop/ViewModels/UnitWindowViewModel.cs').read_text()
    if catalog.count('if (ReloadAfterWrite(') != 9: FAILURES.append('All catalog save/toggle/partial-brand paths need committed-result reload verdict')
    if unit.count('if (!ReloadAfterWrite(id)) return;') != 2: FAILURES.append('Both unit write paths need committed-result verdict')
    for token in ('categoryId is null || SelectedCategory?.Id == categoryId', 'companyId is null || SelectedCompany?.Id == companyId',
                  'formulaId is null || SelectedFormula?.Id == formulaId', 'brandId is null || SelectedBrand?.Id == brandId',
                  'SAVED. Brand saved, but the company link failed:',
                  'the company link failed and the lists could not reload. Do not repeat the whole save;'):
        if token not in catalog: FAILURES.append('Catalog postcommit target/partial verdict missing ' + token)
    if 'SelectedUnit?.Id == id && RefreshAccess() && _lastLoadSucceeded' not in unit:
        FAILURES.append('Unit postcommit target/access must match')
    tests = (DOTNET / 'Area11.Tests/CatalogCurrentAccessTests.cs').read_text()
    if tests.count('[Fact]') != 9 or tests.count('[InlineData(') != 40:
        FAILURES.append('Catalog source regressions need9facts+40inlinecases=49')
    for name in ('AllSixteenWritersRejectInvalidCurrentAuthorityWithoutMutation',
                 'EachWriterAuditsCurrentStoredManagerNameWithoutFinancialGrants',
                 'EachWriterRollsBackOnAuditFaultWithoutProviderDisclosure',
                 'UnitRenameAuditFaultPreservesLinkedBaseAndStrengthLabelsAndBatchQuantity',
                 'BrandNameCommitAndCompanyLinkRemainSeparateTransactions'):
        if 'void ' + name + '(' not in tests: FAILURES.append('Catalog regression missing ' + name)
    REPORT.append('Phase4 portion28/stable5,6,7,24,25:16 current catalog/unit writers and stored audits;current reader/edit capability,stale forms/failed lists cleanup,access-only timers,explicit refresh,SAVED/partial-brand verdicts;49cases/2094total UNRUN,not session/CAS/WPF/runtime certification')

def check_ledger_posting_integrity():
    """Portion29 ledger-pair transaction/source contracts; not C#/WPF execution or idempotency proof."""
    for file, signature, end, table, action in (
        ('CustomerLedgerService.cs','public ServiceResult<CollectionResult> Collect(', 'public ServiceResult<ReceiptDocument> Statement(', 'customers','customer_collect'),
        ('SupplierLedgerService.cs','public ServiceResult<SupplierPayment> Pay(', 'public ServiceResult<SupplierReturnResult> Return(', 'suppliers','supplier_pay')):
        source = (DOTNET / ('Area11.Core/Services/' + file)).read_text()
        body = source.split(signature,1)[-1].split(end,1)[0]
        tokens = ('_db.Tx(connection =>','LedgerReadAccess.Require(', 'INSERT INTO payments(',
                  'UPDATE '+table+' SET balance_paisa = @balance WHERE id = @id;',
                  '_audit.Record(connection, reader,', 'return ServiceResult<')
        positions = [body.find(token) for token in tokens[:-1]]
        if -1 in positions or positions != sorted(positions): FAILURES.append('Ledger posting mutation order/Tx/authority changed: '+file)
        if '"'+action+'"' not in body: FAILURES.append('Ledger posting audit action missing: '+file)
        tx = body.split('_db.Tx(connection =>',1)[-1].split('catch (ValidationException',1)[0]
        if '.Fail(' in tx: FAILURES.append('Ledger Tx must not return failure after partial writes: '+file)
    for who, method, flag, note, reload in (
        ('Customers','Collect','_collectInProgress','Note','Load();'),
        ('Suppliers','Pay','_payInProgress','PayNote','ReloadKeeping(supplierId);')):
        vm = (DOTNET / ('Area11.Desktop/ViewModels/'+who+'ViewModel.cs')).read_text()
        body = vm.split('private void '+method+'()',1)[-1].split('\n    private ',1)[0]
        for token in ('if ('+flag+') return;', flag+' = true;', 'finally { '+flag+' = false; }',
                      'if (!UpdateAccess()) return;', 'AmountText = string.Empty;', note+' = string.Empty;',
                      reload, '_onChanged?.Invoke();', 'catch (Exception exception)',
                      'ClearSensitiveData();', 'Do not '+('receive' if who=='Customers' else 'pay')+' again;',
                      'SAVED. '+('Collection' if who=='Customers' else 'Supplier payment')+' was stored, but'):
            if token not in body: FAILURES.append('Ledger payment committed/reentry boundary '+who+': '+token)
        committed = body.split('var '+('collection' if who=='Customers' else 'payment')+' = result.Value!;',1)[-1]
        if not 0 <= committed.find('try') < committed.find('AmountText = string.Empty;') < committed.find(reload):
            FAILURES.append('Committed ledger cleanup must be inside postcommit try before UI reload: '+who)
        if '_onChanged?.Invoke();\n                if (!UpdateAccess())' not in committed:
            FAILURES.append('Ledger callback must be followed by current access check: '+who)
        clear = vm.split('private void ClearSensitiveData()',1)[-1]
        if 'AmountText = '+note+' = string.Empty;' not in clear: FAILURES.append('Revoked ledger payment input retained: '+who)
        success_flag = '_lastCustomerLoadSucceeded' if who=='Customers' else '_lastSupplierLoadSucceeded'
        if 'if (!'+success_flag+')' not in committed: FAILURES.append('Failed ledger reload overwritten by success: '+who)
    supplier = (DOTNET / 'Area11.Desktop/ViewModels/SuppliersViewModel.cs').read_text()
    reload = supplier.split('private void ReloadKeeping(int supplierId)',1)[-1].split('private void OpenPhotos()',1)[0]
    for token in ('_lastSupplierLoadSucceeded = false;', 'if (!UpdateAccess()) return;', 'if (!result.Ok || result.Value is null)',
                  'ClearSensitiveData();', '_lastSupplierLoadSucceeded = true;'):
        if token not in reload: FAILURES.append('Supplier payment reload must not certify unknown data: '+token)
    tests = (DOTNET / 'Area11.Tests/LedgerPostingIntegrityTests.cs').read_text()
    if tests.count('[Fact]') != 5 or tests.count('[InlineData(') != 27: FAILURES.append('Ledger pair needs32 real-schema source cases')
    for token in ('StageFaultRollsBackPaymentBalanceAuditAndDrawerThenOneRetrySucceeds',
                  'CompetingOverSettlementCannotCommitBothOnSameOrSeparateDatabaseInstances',
                  'FreshServiceAfterFullSettlementRejectsSameAmountWithoutSecondPayment',
                  'PartialRepeatsAreDistinctNewPaymentsNotGlobalIdempotency', 'FailedSecondActionDoesNotRollBackFirstCommittedAction',
                  'AccountingPostsKeepIndependentFinancialGrantsAndSupplierExceptionB', 'durable:false', 'new Barrier(2)',
                  'BEFORE INSERT ON payments','AFTER INSERT ON payments','BEFORE UPDATE OF balance_paisa',
                  'AFTER UPDATE OF balance_paisa','BEFORE INSERT ON audit_logs','AFTER INSERT ON audit_logs'):
        if token not in tests: FAILURES.append('Ledger integration regression missing '+token)
    REPORT.append('Phase4 portion29/stable23,24,25:ledger-pair stage-fault/retry/books/payments/drawer/concurrency regressions;Customer Collect/Supplier Pay reentry latch and committed reload/callback/receipt SAVED clarity;32cases/2126total UNRUN,not global idempotency/C#/WPF/session/commit-I/O certification')

def check_stock_posting_integrity():
    """Bounded portion30 stock posting source contracts; not provider/WPF/commit certification."""
    purchase = (DOTNET / 'Area11.Desktop/ViewModels/PurchasesViewModel.cs').read_text()
    supplier = (DOTNET / 'Area11.Desktop/ViewModels/SuppliersViewModel.cs').read_text()
    for source, method, flag, consumed, reload in (
        (purchase, 'Save', '_saveInProgress', 'Lines.Clear();', 'LoadLookups();'),
        (supplier, 'PostReturn', '_returnInProgress', 'ClearTray();', 'ReloadKeeping(supplierId);')):
        body = source.split('private void '+method+'()',1)[-1].split('\n    private ',1)[0]
        for token in ('if ('+flag+') return;', flag+' = true;', 'finally { '+flag+' = false; }',
                      'if (!result.Ok)', 'catch (Exception exception)', 'SAVED.', consumed, reload):
            if token not in body: FAILURES.append('Stock posting committed/reentry boundary '+method+': '+token)
        committed = body.split('var '+('purchase' if method=='Save' else 'saved')+' = result.Value',1)[-1]
        if not 0 <= committed.find('try') < committed.find(consumed) < committed.find(reload):
            FAILURES.append('Stock posting consumed input must clear inside postcommit try before reload: '+method)
        if method == 'Save':
            for token in ('_openDraftId = 0;', 'PaidText = string.Empty;', 'PaidMethod = "cash";',
                          'if (!_lastLookupsSucceeded || !DocEditor.LoadedSuccessfully || !RefreshEntryAccess())',
                          'Do not receive or post this delivery again;', 'PrintInvoice(purchase.PurchaseId, purchase.Code);'):
                if token not in committed: FAILURES.append('Committed purchase boundary missing '+token)
            if not 0 <= committed.find('Lines.Clear();') < committed.find('PrintInvoice('): FAILURES.append('Invoice precedes consumed purchase cleanup')
        else:
            for token in ('SrNote = string.Empty;', 'CashReceived = false;', 'if (!_lastSupplierLoadSucceeded)',
                          'if (!LoadExpired())', '_onChanged?.Invoke();\n                if (!UpdateAccess())',
                          'Do not return these goods again;', 'ClearSensitiveData();'):
                if token not in committed: FAILURES.append('Committed supplier return boundary missing '+token)
    lookups = purchase.split('private void LoadLookups()',1)[-1].split('private void FilterProducts()',1)[0]
    for token in ('_lastLookupsSucceeded = false;', 'if (!suppliers.Ok || suppliers.Value is null)',
                  'if (!products.Ok || products.Value is null)', '_lastLookupsSucceeded = CanPrepare;'):
        if token not in lookups: FAILURES.append('Purchase lookup success verdict missing '+token)
    expired = supplier.split('private bool LoadExpired()',1)[-1].split('private void ShowSupplier()',1)[0]
    if 'if (!result.Ok || result.Value is null) return false;' not in expired or 'return true;' not in expired:
        FAILURES.append('Supplier expired-stock refresh must expose failed read verdict')
    tests = (DOTNET / 'Area11.Tests/StockPostingIntegrityTests.cs').read_text()
    if tests.count('[InlineData(')!=47 or tests.count('[Fact]')!=3: FAILURES.append('Stock posting needs50 registered real-schema cases')
    for token in ('DeliveryStageFaultRestoresAllRowsAndNumberThenRetryReconciles',
                  'GoodsBackStageFaultPreservesCommittedDeliveryThenRetryReconciles',
                  'DeliveryPaymentMethodAndCashGoodsBackMatchSupplierBooksAndDrawer',
                  'SampleDeliveryStillAddsStockAtZeroCostWithoutInventingPayable',
                  'InvalidSecondDeliveryLineRollsBackAlreadyInsertedFirstLine',
                  'SecondGoodsBackItemFaultRollsBackFirstBatchDecreaseAndKeepsOriginalDelivery',
                  'Assert.Equal(before, f.Snapshot());', 'BooksMatch', 'SupplierCashInPaisa', 'SupplierCashOutPaisa',
                  '"custom_values"', '"sqlite_sequence"', 'nextSupplierReturnNo', 'nextPurchaseNo',
                  'BEFORE UPDATE OF value ON settings', 'BEFORE INSERT ON purchase_items', 'BEFORE INSERT ON supplier_return_items'):
        if token not in tests: FAILURES.append('Stock posting regression missing '+token)
    REPORT.append('Phase4 portion30/stable16,18,23,24,25:Purchase/SupplierReturn50sourcecases and acknowledged-commit UI reentry/consumed input/refresh/receipt clarity;2176suiteUNRUN,not global idempotency/draft atomicity/commit-I-O/WPF certification')

def check_counter_posting_integrity():
    """Portion31 acknowledged counter outcomes and closed/unknown drawer source contracts."""
    pos = (DOTNET / 'Area11.Desktop/ViewModels/PosViewModel.cs').read_text()
    returns = (DOTNET / 'Area11.Desktop/ViewModels/ReturnsViewModel.cs').read_text()
    sales = (DOTNET / 'Area11.Core/Services/SaleService.cs').read_text()
    create = sales.split('public ServiceResult<SaleResult> Create(',1)[-1].split('private bool HasOpenDrawer()',1)[0]
    for token in ('if (!HasOpenDrawer())', '_db.Tx(connection =>', 'if (!HasOpenDrawer(connection))', 'return Save(connection, input, reader);'):
        if token not in create: FAILURES.append('Sale drawer gate missing '+token)
    tx = create.split('_db.Tx(connection =>',1)[-1]
    if not 0 <= tx.find('if (!HasOpenDrawer(connection))') < tx.find('return Save(connection, input, reader);'):
        FAILURES.append('Sale transaction must check drawer before any Save writes')
    drawer = sales.split('private bool HasOpenDrawer()',1)[-1].split('private ServiceResult<SaleResult> Save(',1)[0]
    if 'return false;' not in drawer or 'return true;' in drawer or 'return HasOpenDrawer(connection);' not in drawer:
        FAILURES.append('Unknown drawer cannot authorize a sale')
    for source, method, consumed, last, action in (
        (pos, 'CompleteSaleOnce', 'Cart.Clear();', '_openDraftId = 0;', 'bill or collect this sale'),
        (returns, 'TakeReturn', 'ResetBill();', '_saleId = 0;', 'return or refund these goods')):
        body = source.split('private void '+method+'()',1)[-1].split('\n    private ',1)[0]
        for token in ('if (!result.Ok)', 'catch (Exception exception)', 'SAVED.', 'Do not '+action+' again;'):
            if token not in body: FAILURES.append('Counter acknowledged boundary '+method+': '+token)
        committed = body.split('var '+('sale' if method=='CompleteSaleOnce' else 'saved')+' = result.Value;',1)[-1]
        if not 0 <= committed.find(last) < committed.find('try') < committed.find(consumed) < committed.find('Print(' if method=='TakeReturn' else 'Print(_lastSaleId)'):
            FAILURES.append('Counter committed input/identity must be consumed before printing: '+method)
        if method == 'CompleteSaleOnce':
            for token in ('TenderedText = string.Empty;', 'SecondAmountText = string.Empty;', '_appliedExtraDiscount = 0;',
                          'ResetBillBasis();', 'if (!_lastLookupsSucceeded || !DocEditor.LoadedSuccessfully)', 'var printOk = !AutoPrint || Print(_lastSaleId);',
                          'SaleSuccessDoor.Show(_services, sale, printStatus)'):
                if token not in committed: FAILURES.append('Sale committed cleanup/result missing '+token)
        else:
            for token in ('if (_returnInProgress) return;', '_returnInProgress = true;', 'finally { _returnInProgress = false; }',
                          'if (!_lastQuarantineLoadSucceeded)', 'Note = string.Empty;'):
                if token not in body: FAILURES.append('Customer return committed cleanup/reentry missing '+token)
            if not committed.find('SetStatus(message') < committed.find('Print(saved.Code);'):
                FAILURES.append('Return print failure overwritten by success')
    for token in ('_lastLookupsSucceeded = false;', 'products.Ok && products.Value is not null',
                  '&& customers.Ok && customers.Value is not null && _lastBarcodeLoadSucceeded;',
                  '_lastBarcodeLoadSucceeded = false;', '_lastBarcodeLoadSucceeded = true;'):
        if token not in pos: FAILURES.append('Counter refresh verdict missing '+token)
    if pos.count('if (RefuseCommittedCart()) return;') != 3 or '_committedCartNeedsReset = true;' not in pos:
        FAILURES.append('Committed cart reset failure must block sale/draft/hold reuse')
    refuse = pos.split('private bool RefuseCommittedCart()',1)[-1].split('private bool TryGetDiscount(',1)[0]
    if 'if (!_committedCartNeedsReset) return false;' not in refuse or 'then Clear cart before starting a new bill.' not in refuse:
        FAILURES.append('Committed cart block needs explicit safe reset guidance')
    quarantine = returns.split('public void LoadQuarantine()',1)[-1].split('private void Decide(',1)[0]
    for token in ('_lastQuarantineLoadSucceeded = false;', 'if (!pending.Ok || pending.Value is null || !history.Ok || history.Value is null)',
                  'Quarantine was not loaded.', '_lastQuarantineLoadSucceeded = true;'):
        if token not in quarantine: FAILURES.append('Quarantine unknown read verdict missing '+token)
    tests = (DOTNET / 'Area11.Tests/CounterPostingIntegrityTests.cs').read_text()
    if tests.count('[InlineData(')!=53 or tests.count('[Fact]')!=2: FAILURES.append('Counter posting needs55 registered source cases')
    for token in ('SaleFaultRestoresStockMoneyPointsAuditAndNumberThenRetryReconciles',
                  'CustomerReturnFaultPreservesOriginalBillThenRetryReconciles', 'ClosedOrUnreadableDrawerCannotPostAnyRows',
                  'SecondItemAbortRestoresFirstItemAndKeepsEarlierCommittedBill', 'RedeemedPointsAndNegativeLoyaltyPaymentRollBackWithLateAuditFault',
                  'ExtraReturnBStillRefunds560Then540AfterLateFaultAndRetry', 'FullReturnUsesOriginalBillCashCreditAndDrawerAcrossMethods',
                  'Assert.Equal(before, f.Snapshot());', 'BooksMatch', 'CashRefundsPaisa', 'nextQuarantineNo',
                  '"sqlite_sequence"', '56000L', '54000L'):
        if token not in tests: FAILURES.append('Counter posting regression missing '+token)
    REPORT.append('Phase4 portion31/stable15,17,19,23–25:Sale/customerReturn55sourcecases,acknowledgedcommit form cleanup/SAVED/refresh/print clarity;Sale unknown-drawerfailclosed+transaction recheck;2231suiteUNRUN,not session/durableidentity/draft/COMMIT/WPF certification')

def check_counter_current_access():
    """Portion32 current local account/module; not session authentication or WPF runtime proof."""
    for stem, module, readers in (
        ('Sale', 'Pos', [('CounterWarnings(SaleInput input, User? user)', 'RequirePostingReader(connection, user);'),
                         ('Quote(SaleInput input, User user)', 'user = RequirePostingReader(connection, user);')]),
        ('Return', 'Returns', [('FindSale(string? code, User? user)', 'RequirePostingReader(connection, user);'),
                              ('Search(SaleLookupInput? input, User? user)', 'RequirePostingReader(connection, user);'),
                              ('Lines(int saleId, User? user)', 'RequirePostingReader(connection, user);'),
                              ('Quote(ReturnInput input, User user)', 'user = RequirePostingReader(connection, user);')])):
        source = (DOTNET / ('Area11.Core/Services/'+stem+'Service.cs')).read_text()
        for signature, gate in readers:
            body = source.split(signature,1)[-1].split('\n    public ',1)[0]
            if signature not in source or gate not in body: FAILURES.append('Counter current reader missing '+stem+':'+signature)
            if gate in body:
                before = body.split(gate,1)[0].split('try',1)[-1]
                if any(token in before for token in ('_db.Query', 'LoadSale(connection', 'Compute(connection')):
                    FAILURES.append('Counter reads financial payload before current gate: '+signature)
        for token in ('public ServiceResult CheckPostingAccess(User? user)', 'internal User RequirePostingReader(SqliteConnection connection, User? user)',
                      'SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.'+module+');'):
            if token not in source: FAILURES.append('Counter current helper missing '+stem+':'+token)
        create = source.split('public ServiceResult<'+stem+'Result> Create(',1)[-1].split('private bool HasOpenDrawer()',1)[0].split('// ------------------------------------------------------------------ saving',1)[0]
        tokens = ['_db.Tx(connection =>', 'var reader = RequirePostingReader(connection, user);', 'return Save(connection, input, reader);']
        positions = [create.find(t) for t in tokens]
        if -1 in positions or positions != sorted(positions): FAILURES.append('Current counter writer/actor must resolve inside Tx before Save: '+stem)
    draft = (DOTNET / 'Area11.Core/Services/DraftService.cs').read_text().split('public ServiceResult<SaleResult> PostSale(',1)[-1].split('public ServiceResult<PurchaseResult> PostPurchase(',1)[0]
    for token in ('_sales.CheckPostingAccess(user)', 'connection => { _sales.RequirePostingReader(connection, user); }',
                  'Finalize(connection, _sales.RequirePostingReader(connection, user), draftId, DraftType.Sale, id)'):
        if token not in draft: FAILURES.append('Current Sale draft premark/final actor missing '+token)
    vm = (DOTNET / 'Area11.Desktop/ViewModels/ReturnsViewModel.cs').read_text()
    refresh = vm.split('public bool RefreshPostingAccess()',1)[-1].split('private void Find()',1)[0]
    for token in ('_services.Returns.CheckPostingAccess(_user)', '_saleId = 0;', 'Clear();', 'Pending.Clear();', 'History.Clear();',
                  '_lastQuarantineLoadSucceeded = false;', 'old selections were cleared.'):
        if token not in refresh: FAILURES.append('Return stale cleanup missing '+token)
    for signature in ('private void Find()', 'private void SearchBills()', 'private void Recalculate()', 'private void TakeReturn()', 'public void LoadQuarantine()', 'private void Decide('):
        body = vm.split(signature,1)[-1].split('\n    private ',1)[0].split('\n    public ',1)[0]
        if 'if (!RefreshPostingAccess()) return;' not in body: FAILURES.append('Return next-action gate missing '+signature)
    for token in ('FindSale(BillCode, _user)', 'Lines(saleId, _user)', '}, _user);'):
        if token not in vm: FAILURES.append('Return actor propagation missing '+token)
    view = (DOTNET / 'Area11.Desktop/Views/ReturnsView.xaml.cs').read_text()
    for token in ('TimeSpan.FromSeconds(3)', '_accessTimer.Start();', 'Unloaded += (_, _) => _accessTimer.Stop();', 'model.RefreshPostingAccess();'):
        if token not in view: FAILURES.append('Return access timer lifecycle missing '+token)
    if 'IsEnabled="{Binding CanWork}"' not in (DOTNET/'Area11.Desktop/Views/ReturnsView.xaml').read_text(): FAILURES.append('Denied Return editing must disable')
    pos = (DOTNET/'Area11.Desktop/ViewModels/PosViewModel.cs').read_text()
    for token in ('_services.Sales.CounterWarnings(input, _user)', '_services.Sales.CheckPostingAccess(_user)',
                  'CashPartText = CreditPartText = "—";'):
        if token not in pos: FAILURES.append('Counter actor/output propagation missing '+token)
    tests = (DOTNET/'Area11.Tests/CounterCurrentAccessTests.cs').read_text()
    if tests.count('[InlineData(')!=69 or tests.count('[Fact]')!=5: FAILURES.append('Current counter requires74 registered source regressions')
    for token in ('CurrentChangeDeniesNextReadOrPostWithoutMutationAndRegrantUsesSameServices',
                  'InvalidSuppliedActorCannotReadOrPostEvenWithDefaultAllowedRole', 'DeniedSaleDraftPostCannotPremarkOrConsumeDraft',
                  'SuccessfulPostsUseStoredCurrentNameNotCallerAuthoredName', 'IndependentFinancialGrantsAndMainOffDoNotDisableOrdinarySaleOrRefund',
                  'CounterAndReturnsModuleGrantsRemainIndependent', 'DenialBeforeSaveDoesNotConsumeRetailDiscountConfirmation',
                  'Assert.Equal(before, f.Snapshot());', 'grant-read', 'forged-owner'):
        if token not in tests: FAILURES.append('Current counter regression missing '+token)
    REPORT.append('Phase4 portion32/stable5,6,7,15,17,23:current Sale/Return readers and transactional stored writer,required lookup actors,Sale draft premark/final actor,Return stale page cleanup/timer;74sourcecases/2305suiteUNRUN,not authenticatedsession/CAS/durableidentity/provider/WPF proof')

def check_receipt_current_access():
    """Lexical regression guard only; not execution or session certification."""
    for stem,module,signature,payload in (
        ('Receipt','Pos','Build(int saleId, User? user, ReceiptReadPurpose purpose = ReceiptReadPurpose.Counter)','var sale = _db.QuerySingle'),
        ('ReturnReceipt','Returns','Build(string? returnCode, User? user, ReceiptReadPurpose purpose = ReceiptReadPurpose.Counter)','var rows = _db.Query')):
        source=(DOTNET/('Area11.Core/Services/'+stem+'Service.cs')).read_text()
        body=source.split(signature,1)[-1].split('catch (ValidationException',1)[0]
        gate='SnapshotReadAccess.Require(_db, connection, _permissions, user, module);'
        for token in (signature,'ReceiptReadPurpose.Counter => Modules.'+module,'ReceiptReadPurpose.History => Modules.Sales',
                      '_ => throw new ValidationException',gate):
            if token not in source: FAILURES.append('Receipt current contract missing '+stem+':'+token)
        if gate not in body or payload not in body or body.find(gate)>body.find(payload): FAILURES.append('Receipt payload must follow current gate '+stem)
    source=(DOTNET/'Area11.Core/Services/SaleHistoryService.cs').read_text()
    for token in ('CanLook(User? user) => CheckReadAccess(user).Ok;',
                  'SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Sales)',
                  '_receipts.Build(saleId, user, ReceiptReadPurpose.History)',
                  '_returnReceipts.Build(code, user, ReceiptReadPurpose.History)',
                  '_receipts.Build(id, user, ReceiptReadPurpose.History)',
                  'RequireReader(receiptConnection, user);','A selected bill changed.',
                  'if (exists == 0) throw new ValidationException'):
        if token not in source: FAILURES.append('History current contract missing '+token)
    for signature in ('public ServiceResult<SaleHistoryPage> Search(', 'public ServiceResult<SaleHistoryDetail> Open(', 'public ServiceResult<BulkPrintJob> BuildBulkBills('):
        body=source.split(signature,1)[-1].split('\n    public ',1)[0]
        gate='RequireReader(connection, user);'
        if gate not in body or body.find(gate)>body.find('_db.Query'): FAILURES.append('History direct payload gate missing '+signature)
    for signature in ('public ServiceResult RecordReprint(', 'public ServiceResult RecordReturnReprint('):
        body=source.split(signature,1)[-1].split('\n    public ',1)[0]
        tokens=['_db.Tx(connection =>','var reader = RequireReader(connection, user);','_audit.Record(connection, reader, Modules.Sales']
        positions=[body.find(t) for t in tokens]
        if -1 in positions or positions!=sorted(positions) or 'RecordStandalone(' in body: FAILURES.append('History current transactional audit missing '+signature)
    vm=(DOTNET/'Area11.Desktop/ViewModels/SaleHistoryViewModel.cs').read_text()
    for token in ('_services.History.CheckReadAccess(_user)', '_checkingReadAccess = true;', 'finally { _checkingReadAccess = false; }',
                  '_printSelection = new List<SaleHistoryRow>();','CanPrintSelection = false;', 'Rows.Clear();', 'Selected = null;',
                  'CountText = "-";', 'TotalText = "-";', 'DueText = "-";', 'ClearDetail();',
                  'if (_checkingReadAccess || _clearingHistoryPayload) return false;',
                  'var selected = Selected;', 'RecordReprint(_user, selected.Id)'):
        if token not in vm: FAILURES.append('History stale cleanup missing '+token)
    for signature in ('public void Load()', 'public void SetPrintSelection(', 'private void Open()', 'private void PrintAgain()', 'private void PrintSelection()', 'private void Preview()', 'private void SendOnWhatsApp()', 'private void PrintSelectedReturn()'):
        body=vm.split(signature,1)[-1].split('\n    private ',1)[0].split('\n    public ',1)[0]
        if 'if (!RefreshReadAccess()) return;' not in body: FAILURES.append('History action current gate missing '+signature)
    for stem in ('Pos','Returns'):
        text=(DOTNET/('Area11.Desktop/ViewModels/'+stem+'ViewModel.cs')).read_text()
        token='_services.'+('Receipts.Build(saleId, _user)' if stem=='Pos' else 'ReturnReceipts.Build(returnCode, _user)')
        if token not in text: FAILURES.append('Receipt actor propagation missing '+stem)
    view=(DOTNET/'Area11.Desktop/Views/SaleHistoryView.xaml.cs').read_text()
    for token in ('TimeSpan.FromSeconds(3)', '_accessTimer.Start();', 'Unloaded += (_, _) => _accessTimer.Stop();', 'model.RefreshReadAccess();'):
        if token not in view: FAILURES.append('History timer lifecycle missing '+token)
    tests=(DOTNET/'Area11.Tests/ReceiptCurrentAccessTests.cs').read_text()
    if tests.count('[InlineData(')!=88 or tests.count('[Fact]')!=7: FAILURES.append('Receipt current requires95 registered source regressions')
    for token in ('Assert.Equal(before, f.Snapshot());','Assert.Equal(default(T), result.Value);','IndependentSalesPosAndReturnsGrantsCannotSubstituteForEachOther',
                  'BothReprintAuditsUseStoredCurrentNameNotCallerAuthoredName','AuditInsertFaultIsFailureAndDoesNotSilentlyClaimRecorded',
                  'MissingReturnCannotInventAReprintAudit','UnknownReceiptPurposeFailsClosedWithoutPayloadOrMutation',
                  'StoredOwnerMustStillBeActiveAndSameRoleBeforeOrdinaryBypass','BulkMissingBillReturnsNoPartialJobAndDoesNotWrite'):
        if token not in tests: FAILURES.append('Receipt regression missing '+token)
    REPORT.append('Phase4 portion33/stable5,6,7,15,17,21,23,24:current receipt/history actors,independent purpose/module gates,transactional stored reprint audit,history stale cleanup/timer;95sourcecases/2400suiteUNRUN;NOT C#/session/CAS/printer/prepared-output revocation proof')

def check_quarantine_current_access():
    """Bounded lexical source guard, not runtime authorization/stock/GUI evidence."""
    source=(DOTNET/'Area11.Core/Services/QuarantineService.cs').read_text()
    for signature,payload in (('Pending(User? user)','var rows = _db.Query'),('History(User? user)','var rows = _db.Query'),('PendingCount(User? user)','var count =')):
        body=source.split(signature,1)[-1].split('\n    public ',1)[0]
        gate='RequireReader(connection, user);'
        if signature not in source or gate not in body or body.find(gate)>body.find(payload): FAILURES.append('Quarantine current reader missing '+signature)
    for token in ('public ServiceResult<int> PendingCount(User? user)', 'ServiceResult<int>.Fail("The quarantine count could not be read.")',
                  'SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Returns);',
                  'public ServiceResult CheckDecisionAccess(User? user)', 'if (!reader.CanManage)',
                  'var reader = RequireReader(connection, user);'):
        if token not in source: FAILURES.append('Quarantine current contract missing '+token)
    dispose=source.split('public ServiceResult Dispose(User? user,',1)[-1].split('public ServiceResult CheckDecisionAccess(',1)[0]
    tokens=['_db.Tx(connection =>','var reader = RequireDecider(connection, user);','var row = _db.QuerySingle<PendingRow>',
            'UPDATE sale_returns','AddBack(connection, row, clean, stamp, reader);','_audit.Record(connection, reader,']
    positions=[dispose.find(t) for t in tokens]
    if -1 in positions or positions!=sorted(positions): FAILURES.append('Quarantine current transaction/actor order missing')
    for token in ('userId = reader.Id','That item already has a decision;', 'if (clean == Expired && cleanNote.Length < 3)',
                  'if (clean is Restocked or Sold)'):
        if token not in dispose: FAILURES.append('Quarantine decision guard/actor missing '+token)
    vm=(DOTNET/'Area11.Desktop/ViewModels/ReturnsViewModel.cs').read_text()
    for token in ('_services.Quarantine.Pending(_user)', '_services.Quarantine.History(_user)',
                  'access.Ok && _services.Quarantine.CheckDecisionAccess(_user).Ok',
                  'public bool CanDecideQuarantine => _postingAllowed && _decisionAllowed && _lastQuarantineLoadSucceeded;'):
        if token not in vm: FAILURES.append('Quarantine UI authority missing '+token)
    decide=vm.split('private void Decide(QuarantineRow row, string outcome)',1)[-1].split('private void SetStatus(',1)[0]
    for token in ('if (!RefreshPostingAccess()) return;', 'if (_busy) return;', 'if (!CanDecideQuarantine || !Pending.Contains(row))',
                  '_busy = true;', 'finally { _busy = false; }','var saved = false;', 'saved = true;', 'row.NoteText = string.Empty;',
                  'Pending.Remove(row);', 'Do not decide these goods again.', 'The decision outcome could not be confirmed.',
                  'catch (Exception exception)', '_lastQuarantineLoadSucceeded = false;', 'Pending.Clear();', 'History.Clear();'):
        if token not in decide: FAILURES.append('Quarantine decision UI integrity missing '+token)
    committed=decide.split('saved = true;',1)[-1]
    if not (committed.find('row.NoteText = string.Empty;')<committed.find('Pending.Remove(row);')<committed.find('LoadQuarantine();')):
        FAILURES.append('Committed quarantine row must be consumed before reload')
    xaml=(DOTNET/'Area11.Desktop/Views/ReturnsView.xaml').read_text()
    if xaml.count('IsEnabled="{Binding DataContext.CanDecideQuarantine, RelativeSource={RelativeSource AncestorType=UserControl}}"')!=2:
        FAILURES.append('Quarantine note and decisions require current decision capability')
    tests=(DOTNET/'Area11.Tests/QuarantineCurrentAccessTests.cs').read_text()
    if tests.count('[InlineData(')!=74 or tests.count('[Fact]')!=5: FAILURES.append('Quarantine needs79 registered source regressions')
    for token in ('Assert.Equal(before, f.Snapshot());','Assert.Equal(money, f.MoneySnapshot());','CurrentChangeDeniesWithoutMutationThenSameServiceRegrants',
                  'StatementFaultRollsBackThenOneRetryReconciles','DecisionsUseStoredNameAndIdForAuditAndStock',
                  'ACommittedDecisionCannotBeReplacedByAnotherOutcome','FailedCountIsNotCertifiedZeroAndUnreadableRowsHaveNoPayload',
                  'LegitimateReturnsReaderCanReadButCannotDecide','ValidOwnerBypassStillRequiresStoredActiveSameRoleAccount'):
        if token not in tests: FAILURES.append('Quarantine regression missing '+token)
    REPORT.append('Phase4 portion34/stable5,6,7,17,20,23,24,25:current quarantine readers/explicitcountverdict/Txstoreddecider,Returnsreadonlyvsmanage,detachedrowguard/commitcleanup/SAVED;79sourcecases/2479suiteUNRUN;not session/CAS/actualCOMMIT/WPF/stockcertification')

def check_draft_held_current_access():
    d=(DOTNET/'Area11.Core/Services/DraftService.cs').read_text()
    for token in ('type = DraftType.Clean(type);', 'if (!DraftType.IsKnown(type))',
                  'if (includePayload && type == DraftType.Purchase)', 'return _purchases.RequireEntryReader(connection, user);',
                  'SnapshotReadAccess.Require(_db, connection, _permissions, user, DraftType.ModuleFor(type!))',
                  'public ServiceResult CheckReadAccess(User? user, string draftType, bool includePayload = false)',
                  'byId = reader.Id,','by = reader.Name.Trim(),',
                  '_audit.Record(connection, reader, DraftType.ModuleFor(type), "draft_saved"',
                  '_audit.Record(connection, reader, DraftType.ModuleFor(type), "draft_updated"',
                  'Finalize(connection, _purchases.RequireEntryReader(connection, user), draftId, DraftType.Purchase, id)'):
        if token not in d: FAILURES.append('Draft current contract missing '+token)
    save=d.split('private ServiceResult<DocumentDraft> Save<TInput>(',1)[-1].split('// ------------------------------------------------------------------ list & open',1)[0]
    if save.count('var reader = RequireReader(connection, user, type, includePayload: true);')!=2: FAILURES.append('Both draft save Tx need current payload writer')
    for block in save.split('_db.Tx(connection =>')[1:]:
        gate = block.find('var reader = RequireReader(connection, user, type, includePayload: true);')
        payload = min((x for x in (block.find('ReadRow(connection, draftId)'), block.find('INSERT INTO document_drafts')) if x >= 0), default=-1)
        if gate < 0 or payload < 0 or gate > payload: FAILURES.append('Draft save must authorize inside Tx before payload/mutation')
    discard=d.split('public ServiceResult Discard(',1)[-1].split('// ------------------------------------------------------------------ final post',1)[0]
    tokens=['_db.Tx(connection =>','SELECT draft_type FROM document_drafts WHERE id = @id;',
            'var reader = RequireReader(connection, user, type, includePayload: false);','var row = ReadRow(connection, draftId);',
            '_audit.Record(connection, reader, DraftType.ModuleFor(row.DraftType), "draft_discarded"']
    positions=[discard.find(t) for t in tokens]
    if -1 in positions or positions!=sorted(positions) or 'probeConnection' in discard: FAILURES.append('Discard must classify type only,authorize,then read payload inside Tx')
    h=(DOTNET/'Area11.Core/Services/HoldCartService.cs').read_text()
    for signature,payload in (('List(User? user)','var rows ='),('Count(User? user)','var count =')):
        body=h.split(signature,1)[-1].split('\n    public ',1)[0]
        if 'RequireReader(connection, user);' not in body or body.find('RequireReader')>body.find(payload): FAILURES.append('Held current read missing '+signature)
    for signature,tx,first in (('Hold(SaleInput input, User? user, int sourceDraftId = 0)','var code = _db.Tx(connection =>','var number ='),
                               ('Resume(User? user, int cartId)','var cart = _db.Tx(connection =>','var row ='),
                               ('Discard(User? user, int cartId)','_db.Tx(connection =>','var code =')):
        body=h.split(signature,1)[-1].split('\n    public ',1)[0]
        tokens=[tx,'var reader = RequireReader(connection, user);',first]
        positions=[body.find(t) for t in tokens]
        if -1 in positions or positions!=sorted(positions) or '_audit.Record(connection, reader, "POS",' not in body: FAILURES.append('Held current Tx/audit missing '+signature)
    for token in ('public ServiceResult<int> Count(User? user)', 'ServiceResult<int>.Fail("The held carts could not be counted.")',
                  'SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Pos);', 'userId = reader.Id,'):
        if token not in h: FAILURES.append('Held contract missing '+token)
    vm=(DOTNET/'Area11.Desktop/ViewModels/DraftsViewModel.cs').read_text()
    for token in ('_services.Drafts.CheckReadAccess(_user, _type)', 'private void ClearDrafts()', '_listLoaded = false;',
                  'public bool HasNoDraft => _listLoaded && Drafts.Count == 0;', 'if (!_listLoaded || !Drafts.Contains(draft))',
                  'if (answer != MessageBoxResult.Yes || !CurrentSelection(draft, includePayload: true)) return;',
                  'if (answer != MessageBoxResult.Yes || !CurrentSelection(draft, includePayload: false)) return;',
                  'Do not post this draft again.', 'SAVED. Draft put away.', 'finally { _busy = false; }'):
        if token not in vm: FAILURES.append('Draft stale/modal/SAVED UI missing '+token)
    pos=(DOTNET/'Area11.Desktop/ViewModels/PosViewModel.cs').read_text()
    for token in ('public bool RefreshParkedAccess()', 'private bool _heldLoadSucceeded;', 'Parked carts are not loaded.',
                  'if (!_heldLoadSucceeded || !HeldCarts.Contains(row))'):
        if token not in pos: FAILURES.append('Parked stale UI missing '+token)
    for signature in ('private void SaveSaleDraft()', 'public void OpenSaleDraft(int draftId)', 'private void HoldCart()',
                      'private void LoadHeldCarts()', 'private void ResumeHeld(HeldCartRow row)', 'private void DiscardHeld(HeldCartRow row)'):
        body=pos.split(signature,1)[-1].split('\n    private ',1)[0].split('\n    public ',1)[0]
        if 'if (!RefreshParkedAccess()) return;' not in body: FAILURES.append('Parked/draft POS preflight missing '+signature)
    for name,stop,refresh in (('Drafts','Closed += (_, _) => _accessTimer.Stop();','viewModel.RefreshReadAccess();'),
                              ('Pos','Unloaded += (_, _) => _parkedAccessTimer.Stop();','model.RefreshParkedAccess();')):
        view=(DOTNET/('Area11.Desktop/Views/'+name+'View.xaml.cs')).read_text()
        for token in ('TimeSpan.FromSeconds(3)',stop,refresh):
            if token not in view: FAILURES.append('Draft/held timer lifecycle missing '+name+':'+token)
    tests=(DOTNET/'Area11.Tests/DraftHeldCurrentAccessTests.cs').read_text()
    if tests.count('[InlineData(')!=129 or tests.count('[Fact]')!=7: FAILURES.append('Draft/held requires136 registered source cases')
    for token in ('Assert.Equal(before, f.Snapshot());', 'Assert.Equal(money, f.MoneySnapshot());',
                  'WritersUseStoredActorNameNotCallerText','PurchaseMetadataAndDiscardRemainAllowedWithoutCostButPayloadDoesNot',
                  'CountReadFailureCannotClaimSuccessfulZero','WrongKindOrUnknownKindCannotReadPayloadOrOverwriteDraft',
                  'DraftsStaySharedAcrossAllowedPeopleAndCreatorRemainsHistorical'):
        if token not in tests: FAILURES.append('Draft/held regression missing '+token)
    REPORT.append('Phase4 portion35/stable5,6,7,15,16,23,25:currentdraft/heldreaders+writeraudit,metadata-vs-purchasepayloadpolicy,typeonlydiscardprobe,explicitheldcount,staledrawers/timers/modalrecheck;136sourcecases/2615suiteUNRUN;NOT session/CAS/whole-draftatomicity/heldreconstruction/COMMIT/WPFproof')

def check_draft_recovery_fault_boundaries():
    core=DOTNET/'Area11.Core'
    sources={name:(core/path).read_text() for name,path in {
        'db':'Data/Database.cs','outcome':'Data/TransactionOutcome.cs','draft':'Services/DraftService.cs',
        'hold':'Services/HoldCartService.cs','sale':'Services/SaleService.cs','purchase':'Services/PurchaseService.cs'}.items()}
    required={
        'outcome':['public bool CommitAttempted', '!WorkStarted || (!CommitAttempted && RollbackConfirmed)'],
        'db':['TransactionOutcome? outcome = null','outcome.WorkStarted = true;', 'outcome.CommitAttempted = true;', 'outcome.RollbackConfirmed = true;'],
        'draft':['if (!created.NoCommitConfirmed)', 'var restored = RestoreAfterFailure(draftId, type);',
                 'if (!marked.Ok) throw new ValidationException', 'created.Warnings.Concat(new[] { warning })',
                 'sale.Warnings.Add(warning);','purchase.Warnings.Add(warning);',
                 "WHERE id = @id AND draft_type = @type AND status = 'posted' AND posted_document_id = @marker;",
                 'return changed == 1 && row is { Status: DraftStatus.Draft, PostedDocumentId: null }',
                 'if (changed != 1) throw new ValidationException', 'Draft restoration was not confirmed'],
        'hold':['int sourceDraftId = 0','if (sourceDraftId > 0)','An open draft cannot be parked as another copy.',
                'var recoveryInput = new SaleInput','JsonSerializer.Serialize(recoveryInput, Json.WriterOptions)',
                'payload.Length > DraftService.MaxPayloadChars','INSERT INTO document_drafts(',
                'var recoveryId = (int)_db.NextId(connection);', 'Notes = row.Note,', 'recovery_draft_id = @draft',
                "WHERE id = @id AND status = 'held';",'if (consumed != 1)', 'RecoveryDraftId = recoveryId,',
                '"draft_saved", "document_drafts", recoveryId.ToString()', 'LineDiscountPercent = line.LineDiscountPercent'],
        'sale':['}, outcome);','NoCommitConfirmed = outcome.NoCommitConfirmed','NoCommitConfirmed = true'],
        'purchase':['}, outcome);','NoCommitConfirmed = outcome.NoCommitConfirmed','NoCommitConfirmed = true']}
    for name,tokens in required.items():
        for token in tokens:
            if token not in sources[name]: FAILURES.append('P36 '+name+' missing '+token)
    db=sources['db'];order=['outcome.WorkStarted = true;','var result = work(connection);','outcome.CommitAttempted = true;','transaction.Commit();']
    if [db.find(t) for t in order]!=sorted(db.find(t) for t in order): FAILURES.append('P36 transaction outcome ordering')
    d=sources['draft'];unknown=d.split('created = create(input);',1)[-1].split('if (!created.Ok',1)[0]
    if 'RestoreAfterFailure(' in unknown: FAILURES.append('P36 throwing creator must retain marker')
    failure=d.split('if (!created.Ok || created.Value is null)',1)[-1].split('var marked =',1)[0]
    if failure.find('if (!created.NoCommitConfirmed)')>failure.find('RestoreAfterFailure('): FAILURES.append('P36 unknown must stop before restore')
    h=sources['hold'].split('Resume(User? user, int cartId)',1)[-1].split('public ServiceResult Discard',1)[0]
    order=['var reader = RequireReader','var row =','var recoveryInput =','INSERT INTO document_drafts','var recoveryId =',"UPDATE held_carts SET status = 'resumed'",'"draft_saved"','"cart_resume"','return new HeldCart']
    positions=[h.find(t) for t in order]
    if -1 in positions or positions!=sorted(positions): FAILURES.append('P36 atomic linked recovery ordering')
    pos=(DOTNET/'Area11.Desktop/ViewModels/PosViewModel.cs').read_text()
    for token in ('private bool _draftReconstructionFailed;', 'Notes = _savedDraftNotes,', '_savedDraftNotes = cart.Notes;', '_services.Holds.Hold(BuildInput(), _user, _openDraftId)',
                  'if (_draftReconstructionFailed)', '_draftReconstructionFailed = skipped > 0;',
                  'Unsaved edits will be lost.', 'recover the saved work from Drafts.', 'Do not post this partial cart.'):
        if token not in pos: FAILURES.append('P36 Counter missing '+token)
    for start,link in [('private void ResumeHeld(', '_openDraftId = cart.RecoveryDraftId;'),('public void OpenSaleDraft(', '_openDraftId = draftId;')]:
        block=pos.split(start,1)[-1].split('\n    private ',1)[0]
        first=next((block.find(t) for t in ('SelectedCustomer =','Cart.Add(') if block.find(t)>=0),-1)
        if link not in block or block.find(link)>first or '_draftReconstructionFailed = true;' not in block or 'catch (Exception exception)' not in block: FAILURES.append('P36 link and guard before Counter callbacks '+start)
    vm=(DOTNET/'Area11.Desktop/ViewModels/DraftsViewModel.cs').read_text()
    if 'posted.Warning' not in vm or vm.count('string.Join(" ", result.Value.Warnings)')!=2: FAILURES.append('P36 draft drawer must surface saved warnings')
    migration=(core/'Data/Migrations/037_held_recovery.sql').read_text()
    for token in ('REFERENCES document_drafts(id)', 'CREATE UNIQUE INDEX idx_held_recovery_draft'):
        if token not in migration: FAILURES.append('P36 recovery migration missing '+token)
    tests=(DOTNET/'Area11.Tests/DraftRecoveryFaultTests.cs').read_text()
    if tests.count('[InlineData(')!=24 or tests.count('[Fact]')!=5: FAILURES.append('P36 requires29 registered fault/recovery source cases')
    for token in ('CreatorOutcomeControlsRestoreNotErrorWording','FinalizeAuditFaultReturnsSavedWarningAndNeverRecreatesDocument',
                  'ResumeFaultRollsBackDraftConsumeAndAuditThenOneRetryRecovers','TransactionObservationRequiresPreCommitRollbackAndIsNotACommitReceipt',
                  'Assert.Equal(before, f.Snapshot());','Assert.Equal(money, f.Money());'):
        if token not in tests: FAILURES.append('P36 regression missing '+token)
    REPORT.append('Phase4 portion36/stable15,23,24,25:atomiclinkedheldrecoverydraft+Counterguard;explicitpreCOMMITrollbackclassification,unknownmarker,restore/finalizewarnings;29C#sourcecases/2644suiteUNRUN;NOTdurableglobalidentity/providerCOMMIT/powercut/parallel/WPFcertification')

def check_automatic_shared_draft_posting():
    d=(DOTNET/'Area11.Core/Services/DraftService.cs').read_text()
    save=d.split('private ServiceResult<DocumentDraft> Save<TInput>',1)[-1].split('// ------------------------------------------------------------------ list & open',1)[0]
    for token in ('var outcome = new TransactionOutcome();','IsUniqueViolation(exception) && outcome.NoCommitConfirmed && attempt < 4',
                  'NoCommitConfirmed = outcome.NoCommitConfirmed','NoCommitDraftFailure(', 'new() { Error = error, NoCommitConfirmed = true };'):
        if token not in save: FAILURES.append('P37 draft save outcome missing '+token)
    if save.count('}, outcome);')!=2: FAILURES.append('P37 draft insert AND update need outcome observation')
    h=(DOTNET/'Area11.Core/Services/HoldCartService.cs').read_text().split('public ServiceResult<string> Hold(',1)[-1].split('/// <summary>Parked carts',1)[0]
    for token in ('if (sourceDraftId > 0)', 'An open draft cannot be parked as another copy.', '}, outcome);','NoCommitConfirmed = outcome.NoCommitConfirmed'):
        if token not in h: FAILURES.append('P37 Hold missing '+token)
    order=['var reader = RequireReader','if (sourceDraftId > 0)','var number =','INSERT INTO held_carts']
    ix=[h.find(t) for t in order]
    if -1 in ix or ix!=sorted(ix): FAILURES.append('P37 Hold must reject draft intent before numbering/rows')
    for page,kind,method,next_method in [('Pos','Sale','private void CompleteSaleOnce()','private bool RefuseCommittedCart()'),
                                        ('Purchases','Purchase','private void Save()','private void SendLastOnWhatsApp()')]:
        vm=(DOTNET/f'Area11.Desktop/ViewModels/{page}ViewModel.cs').read_text()
        body=vm.split(method,1)[-1].split(next_method,1)[0]
        expected=f'if (_openDraftId <= 0 && !Store{kind}Draft(input)) return;'
        post=f'_services.Drafts.Post{kind}(_user, _openDraftId, input)'
        if expected not in body or post not in body or body.find(expected)>body.find(post): FAILURES.append('P37 auto save-link-post order '+kind)
        if f'_services.{"Sales" if page=="Pos" else "Purchases"}.Create(' in vm: FAILURES.append('P37 direct UI creator bypass '+kind)
        helper=vm.split(f'private bool Store{kind}Draft(',1)[-1].split('private void OpenDrafts()',1)[0]
        for token in ('_draftSaveNeedsVerification = true;', '_draftSaveNeedsVerification = !saved.NoCommitConfirmed;',
                      '_openDraftId = saved.Value.Id;', '_draftSaveNeedsVerification = false;', 'OnPropertyChanged(nameof(HasOpenDraft));', 'catch (Exception exception)'):
            if token not in helper: FAILURES.append('P37 store helper missing '+kind+':'+token)
        if helper.find('_draftSaveNeedsVerification = true;')>helper.find(f'_services.Drafts.Save{kind}Draft') or helper.find('_openDraftId = saved.Value.Id;')>helper.find('OnPropertyChanged(nameof(HasOpenDraft));'): FAILURES.append('P37 latch/link must precede callbacks '+kind)
        if 'private bool _draftSaveNeedsVerification;' not in vm: FAILURES.append('P37 uncertain save guard '+kind)
    pos=(DOTNET/'Area11.Desktop/ViewModels/PosViewModel.cs').read_text()
    for token in ('if (_draftSaveNeedsVerification)','if (_parkedCartNeedsReset)','_parkedCartNeedsReset = !result.NoCommitConfirmed;',
                  '_holdOutcomeUnknown = _parkedCartNeedsReset;', '_parkedCartNeedsReset = true;',
                  'PARKED. The cart was stored, but the counter could not reset.',
                  'if (_openDraftId > 0)', 'Verify shared Drafts and parked carts before starting another copy.'):
        if token not in pos: FAILURES.append('P37 Counter copy guard missing '+token)
    purchase=(DOTNET/'Area11.Desktop/ViewModels/PurchasesViewModel.cs').read_text()
    for token in ('private bool RefuseUnsafeEntry()', 'if (RefuseUnsafeEntry()) return;',
                  'if (_saveInProgress || RefuseUnsafeEntry()) return;', '_committedEntryNeedsReset = true;',
                  '_committedEntryNeedsReset = false;', '_draftReconstructionFailed = skipped > 0;'):
        if token not in purchase: FAILURES.append('P37 Purchase safety missing '+token)
    opened=purchase.split('public void OpenPurchaseDraft(int draftId)',1)[-1].split('private void SetStatus',1)[0]
    ix=[opened.find(t) for t in ('_openDraftId = draftId;','_draftReconstructionFailed = true;','SelectedSupplier =','Lines.Add(')]
    if -1 in ix or ix!=sorted(ix) or 'catch (Exception exception)' not in opened: FAILURES.append('P37 Purchase reconstruction must link/block before callbacks')
    hold_ui=pos.split('private void HoldCart()',1)[-1].split('public bool RefreshParkedAccess()',1)[0]
    order=['_parkedCartNeedsReset = true;', '_holdOutcomeUnknown = true;', '_services.Holds.Hold(']
    ix=[hold_ui.find(t) for t in order]
    if -1 in ix or ix!=sorted(ix): FAILURES.append('P37 Hold must arm unknown before service/callbacks')
    maker=(ROOT/'tools/make_checklist.py').read_text()
    if 'HALF = "⏳"' not in maker or 'state = HALF if cells[2] in ("✅", "🟡") else TODO' not in maker:
        FAILURES.append('P37 report must not certify legacy source ticks as100%')
    tests=(DOTNET/'Area11.Tests/AutomaticDraftPostingTests.cs').read_text()
    if tests.count('[InlineData(')!=40 or tests.count('[Fact]')!=1: FAILURES.append('P37 requires41 source cases')
    for token in ('DraftSaveStatementFaultIsKnownNoCommitAndRetryUsesOneDraft','HoldStatementFaultRollsBackNumberRowsAndAuditThenOneRetryParks',
                  'AnySuppliedDraftIntentCannotBecomeAnotherParkedCopy','OrdinarySaveSequenceIsSharedRecoverableAndPostsTheSameDraftOnlyOnce',
                  'Assert.True(failed.NoCommitConfirmed);','Assert.Equal(before, f.Snapshot());'):
        if token not in tests: FAILURES.append('P37 regression missing '+token)
    REPORT.append('Phase4 portion37/stable15,16,23,24,25:auto shared draft before UI post,sameID/unknownsaveguard,all draft Hold refused,Holdoutcome/reset and Purchasecleanup/reconstructionguards;41C#sourcecases/2685suiteUNRUN;NOTWPF/providerCOMMIT/parallel/globalidentityproof')

def check_expense_stock_fault_boundaries():
    """Lexical contract only, never service execution or provider/WPF proof."""
    for name,start,end,count in [
        ('Expense','public ServiceResult<ExpenseResult> AddExpense','private ServiceResult<DrawingResult> SaveGoods',3),
        ('Stock','public ServiceResult<WriteOffResult> WriteOff','/// <summary>The write-off history',1),
        ('StockTake','public ServiceResult<StockTakeResult> Apply','private ServiceResult<StockTakeResult> ApplyInside',1)]:
        body=(DOTNET/f'Area11.Core/Services/{name}Service.cs').read_text().split(start,1)[-1].split(end,1)[0]
        if body.count('var outcome = new TransactionOutcome();')!=count or body.count('}, outcome);')!=count:
            FAILURES.append('P38 observed Tx wrappers missing '+name)
        if body.count('NoCommitConfirmed = outcome.NoCommitConfirmed')!=2*count: FAILURES.append('P38 failure evidence missing '+name)
        if 'could not be confirmed. Check' not in body or 'Nothing was written.' in body or 'Nothing was changed.' in body:
            FAILURES.append('P38 no blanket no-write claim '+name)
    guard=(DOTNET/'Area11.Core/Utils/PostingAttemptGuard.cs').read_text()
    for token in ('if (!Ready) return false;', 'WasAcknowledged = false;', 'if (failure.Ok) throw',
                  'failure.NoCommitConfirmed ? PostingAttemptState.Ready : PostingAttemptState.NeedsVerification',
                  'WasAcknowledged = true;', 'State = PostingAttemptState.CommittedNeedsReset;',
                  'Require(PostingAttemptState.CommittedNeedsReset);', 'if (InFlight) State = PostingAttemptState.NeedsVerification;'):
        if token not in guard: FAILURES.append('P38 local outcome guard missing '+token)
    for page,methods in [('Expenses',['SaveExpense','SaveCashDrawing','SaveGoodsDrawing']),('WriteOff',['Save']),('StockTake',['Apply'])]:
        vm=(DOTNET/f'Area11.Desktop/ViewModels/{page}ViewModel.cs').read_text()
        for token in ('private readonly PostingAttemptGuard _posting = new();','if (_postingInProgress) return;',
                      'if (!_posting.TryBegin())','_postingInProgress = true;', 'try { action(); }', '_posting.ObserveException();',
                      '_posting.WasAcknowledged', 'finally','if (_posting.InFlight) _posting.CancelBeforeWrite();','_postingInProgress = false;'):
            if token not in vm: FAILURES.append('P38 UI guard missing '+page+':'+token)
        for method in methods:
            if f'private void {method}() => RunPosting({method}Once);' not in vm: FAILURES.append('P38 unguarded UI '+page+method)
            body=vm.split(f'private void {method}Once()',1)[-1].split('\n    private ',1)[0]
            for token in ('_posting.ObserveFailure(result);','_posting.AcknowledgeCommit();','_posting.CompleteReset();','SAVED. '):
                if token not in body: FAILURES.append('P38 post cleanup missing '+page+method+':'+token)
            order=['_posting.AcknowledgeCommit();','if (result.Value is null)', '_posting.CompleteReset();']
            ix=[body.find(t) for t in order]
            if -1 in ix or ix!=sorted(ix): FAILURES.append('P38 acknowledged success precedes callbacks '+page+method)
        if page=='Expenses' and 'if (!DocEditor.LoadedSuccessfully) throw' not in vm: FAILURES.append('P38 editor reset result must be checked')
        if page=='StockTake':
            body=vm.split('private void ApplyOnce()',1)[-1].split('private void Cancel()',1)[0]
            order=['_posting.AcknowledgeCommit();','_session = null;','_selectedLine = null;','RaiseSession();','Load();','_posting.CompleteReset();']
            ix=[body.find(t) for t in order]
            if -1 in ix or ix!=sorted(ix): FAILURES.append('P38 stale applied session must be consumed before refresh')
            if vm.count('if (_postingInProgress || !_posting.Ready)')!=5: FAILURES.append('P38 pending Apply blocks count/open/cancel/found')
    tests=(DOTNET/'Area11.Tests/ExpenseStockPostingFaultTests.cs').read_text()
    for token in ('StatementFaultRestoresWholeSnapshotThenOneRetryReconciles','Assert.True(failure.NoCommitConfirmed);',
                  'Assert.Equal(before, f.Snapshot());','SecondStockTakeLineFaultRollsBackFirstThenApplyRemainsTerminal',
                  'ApplyStillUsesFrozenCostAndLeavesUncountedStockAlone','ExplicitFailureOutcomeNotWordingControlsLocalRetry',
                  'ExceptionDoesNotForgetAcknowledgedCommit','ResetCannotReleaseUnknownOrUnacknowledgedWork'):
        if token not in tests: FAILURES.append('P38 regression source missing '+token)
    if len(re.findall(r'\[(?:Fact|InlineData)(?:\(|\])',tests))!=59: FAILURES.append('P38 expected 59 regression cases')
    REPORT.append('P38 expense/stock observed failures + local acknowledgement/reset guard lexical contracts; C#/provider/WPF UNRUN')


def check_provisional_posting_fault_boundaries():
    """Lexical regression contract, not actual service/provider/WPF execution."""
    core=(DOTNET/'Area11.Core/Services/ProvisionalReturnService.cs').read_text()
    for start,end,tx in [('public ServiceResult<ProvisionalResult> Create(', '/// <summary>The blinking list', '_db.Tx(connection => Save(connection, input, user), outcome);'),
                         ('public ServiceResult Link(', 'public ServiceResult Cancel(', '}, outcome);'),
                         ('public ServiceResult Cancel(', 'public ServiceResult<List<BillSearchRow>> SearchBills(', '}, outcome);')]:
        body=core.split(start,1)[-1].split(end,1)[0]
        for token in ('var outcome = new TransactionOutcome();',tx,'outcome.NoCommitConfirmed ? exception.Message',
                      'NoCommitConfirmed = true','no commit was made.','could not be confirmed. Verify'):
            if token not in body: FAILURES.append('P39 PR outcome wrapper missing '+start+':'+token)
        if body.count('NoCommitConfirmed = outcome.NoCommitConfirmed')!=2: FAILURES.append('P39 PR both catches need observed failure '+start)
        caught=body.split('catch (ValidationException exception)',1)[-1]
        if 'Nothing was changed.' in caught or 'Technical detail:' in caught: FAILURES.append('P39 unknown must not promise rollback or expose raw technical detail')
    vm=(DOTNET/'Area11.Desktop/ViewModels/ProvisionalViewModel.cs').read_text()
    for name in ('Save','LinkBill','CancelEntry'):
        if f'private void {name}() => RunPosting({name}Once);' not in vm: FAILURES.append('P39 unguarded PR action '+name)
        body=vm.split(f'private void {name}Once()',1)[-1].split('\n    private ',1)[0]
        for token in ('_posting.ObserveFailure(result);','_posting.AcknowledgeCommit();','_posting.CompleteReset();','SAVED. ',
                      'BillCode = string.Empty;','CancelReason = string.Empty;','LookupText = string.Empty;','Lookup.Clear();'):
            if token not in body: FAILURES.append('P39 PR cleanup missing '+name+':'+token)
        order=['_posting.AcknowledgeCommit();','ForgetPostingTarget();','Load();','_posting.CompleteReset();']
        ix=[body.find(t) for t in order]
        if -1 in ix or ix!=sorted(ix) or body.count('ForgetPostingTarget();')!=2 or body.rfind('ForgetPostingTarget();')<body.find('Load();'):
            FAILURES.append('P39 PR consume selection before callbacks AND after Load '+name)
        if name=='Save':
            if body.find('_posting.AcknowledgeCommit();')>body.find('if (result.Value is null)') or body.find('Lines.Clear();')>body.find('Print(code)'):
                FAILURES.append('P39 Save acknowledgement/cleanup before result/print')
        else:
            if 'var entryId = SelectedEntry.Id;' not in body or 'var caption = SelectedEntry.Code;' not in body or body.find('var caption =')>body.find('_services.Provisional.'):
                FAILURES.append('P39 pin PR identity/caption before service '+name)
    for token in ('private readonly PostingAttemptGuard _posting = new();','if (RefusePendingPosting()) return;',
                  'if (!_posting.TryBegin()) return;', '_postingInProgress = true;', 'try { action(); }',
                  '_posting.ObserveException();','_posting.WasAcknowledged','finally',
                  'if (_posting.InFlight) _posting.CancelBeforeWrite();','_postingInProgress = false;'):
        if token not in vm: FAILURES.append('P39 PR shared guard missing '+token)
    for name in ('AddLine','RemoveLine'):
        body=vm.split(f'private void {name}()',1)[-1].split('\n    private ',1)[0]
        if 'if (RefusePendingPosting()) return;' not in body: FAILURES.append('P39 PR pending tray edit '+name)
    forget=vm.split('private void ForgetPostingTarget()',1)[-1].split('private bool RefusePendingPosting()',1)[0]
    order=['_selectedEntry = null;', '_selectedLookup = null;','OnPropertyChanged(nameof(SelectedEntry));']
    ix=[forget.find(t) for t in order]
    if -1 in ix or ix!=sorted(ix): FAILURES.append('P39 target backing fields before callbacks')
    tests=(DOTNET/'Area11.Tests/ProvisionalPostingFaultTests.cs').read_text()
    for token in ('StatementFaultRestoresSnapshotThenOneRetryReconciles','Assert.True(failed.NoCommitConfirmed);','Assert.Equal(before, f.Snapshot());',
                  'LegacyUnallocatedLinkKeepsItsSameBillAndNoNewMoney','ZeroRefundStillAddsGoodsButNoCashPaymentOrDrawerOutflow',
                  'KnownRefusalsPermitCorrectionWithoutMutation','Assert.Equal(financial, f.Snapshot(', 'Assert.Equal(stock, f.Snapshot('):
        if token not in tests: FAILURES.append('P39 PR regression source missing '+token)
    if len(re.findall(r'\[(?:Fact|InlineData)(?:\(|\])',tests))!=45: FAILURES.append('P39 expected45 PR source cases')
    REPORT.append('P39 PR Create/Link/Cancel observed failure + acknowledged cleanup/target/reentry lexical contracts;45 C# cases UNRUN')


def main() -> int:
    quiet = "--quiet" in sys.argv

    print("Area11 static check\n" + "=" * 60)
    check_csharp()
    check_usings()
    check_rounding()
    check_solutions()
    check_skeleton()
    check_internal_access()
    check_sealed_bases()
    check_shadowing()
    check_result_values()
    check_constructor_arity()
    check_readonly_members()
    check_resources()
    check_app_labels()
    check_x_static()
    check_service_methods()
    check_service_members()
    check_xml()
    check_bindings()
    check_templates()
    check_migrations()
    check_sql()
    check_column_mapping()
    check_packages()
    check_version_colour()
    check_tests()
    check_installer()
    check_docs()
    check_scripts()
    check_running_app()
    check_run_verdict()
    check_shift_report_gate()
    check_stock_policy_gate()
    check_binding_modes()
    check_crash_report_honesty()
    check_report_tool()
    check_page_guard()
    check_empty_page()
    check_run_trail()
    check_shop_clock()
    check_phase_one_shell()
    check_phase_two_fields()
    check_phase_three_packaging()
    check_master_foundation()
    check_deworkshop()
    check_save_warning()
    check_template_conflict()
    check_counter_availability()
    check_counter_checkout()
    check_counter_search_trial()
    check_counter_keyboard_phase_one()
    check_counter_pricing_phase_two()
    check_counter_extra_discount_phase_three()
    check_checklist()
    check_audit_recovery()
    check_report_chart()
    check_real_stock_projections()
    check_pr_reconciliation_parameters()
    check_cloud_sync_closure()
    check_native_workbook()
    check_backup_restore_gate()
    check_owner_boundary_review()
    check_phase_four_access_projections()
    check_ledger_read_boundaries()
    check_product_read_boundaries()
    check_snapshot_read_boundaries()
    check_definition_time_boundaries()
    check_financial_visibility_grants()
    check_windows_failure_regressions()
    check_permission_inspector_ui()
    check_purchase_read_visibility()
    check_batch_read_visibility()
    check_stock_valuation_visibility()
    check_expense_visibility()
    check_purchase_preparation()
    check_bulk_audit_access()
    check_encrypted_backup_contract()
    check_stock_minus_visibility()
    check_supplier_accounting_exception()
    check_customer_current_boundaries()
    check_recovery_monthly_boundaries()
    check_parent_roster_boundaries()
    check_shift_current_boundaries()
    check_transaction_permission_boundaries()
    check_settings_current_boundaries()
    check_photo_current_boundaries()
    check_custom_value_current_boundaries()
    check_custom_manager_current_boundaries()
    check_record_write_current_boundaries()
    check_catalog_current_boundaries()
    check_ledger_posting_integrity()
    check_stock_posting_integrity()
    check_counter_posting_integrity()
    check_counter_current_access()
    check_receipt_current_access()
    check_quarantine_current_access()
    check_draft_held_current_access()
    check_draft_recovery_fault_boundaries()
    check_automatic_shared_draft_posting()
    check_expense_stock_fault_boundaries()
    check_provisional_posting_fault_boundaries()
    check_csharp_grammar()

    if not quiet:
        for line in REPORT:
            print("  " + line)
        print("=" * 60)

    for message in WARNINGS:
        print("WARN " + message)
    for message in FAILURES:
        print("FAIL " + message)

    if FAILURES:
        print(f"\n{len(FAILURES)} problem(s). Fix them before committing.")
        return 1

    print(f"\nOK - {len(REPORT)} checks, no problems. "
          "This is a static check: run `dotnet build` and `dotnet test` on Windows.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
