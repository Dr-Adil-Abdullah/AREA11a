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
    "LoginWindow.xaml": "LoginViewModel",
    "ShellWindow.xaml": "ShellViewModel",
    "DashboardView.xaml": "DashboardViewModel",
    "ProductsView.xaml": "ProductsViewModel",
    "PosView.xaml": "PosViewModel",
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
    "DoctorsView.xaml": "DoctorsViewModel",
    "LanView.xaml": "LanViewModel",
    "TaxView.xaml": "TaxViewModel",
    "HardwareView.xaml": "HardwareViewModel",
    "CloudSyncView.xaml": "CloudSyncViewModel",
    "SaleHistoryView.xaml": "SaleHistoryViewModel",
    "BookCheckView.xaml": "BookCheckViewModel",
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

        if parser is not None:
            tree = parser.parse(source)
            if tree.root_node.has_error:
                broken += 1
                line = first_error_line(tree.root_node, source)
                fail(f"C# syntax error: {rel(path)}:{line}")
            continue

        text = source.decode("utf-8", errors="replace")
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
            ET.parse(path)
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
    rows = [line for line in versions.splitlines()
            if re.match(r"^\|\s*\d+\.\d+\.\d+\s*\|", line)]
    if not rows:
        fail("docs/VERSIONS.md has no released-version row")
        return

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
    try_at = code.find("try", start)
    guard = code[start:try_at if try_at > start else start + 1500]

    if "Modules.Cash" not in guard or "CanManage" not in guard:
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

    if "CanManage" not in can_line:
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
    built = sorted(set(re.findall(r"new ([A-Za-z]+ViewModel)\(", shell_text)))

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
            said_none = re.search(r"// empty-page: none - (.{20,})", text)

            if said_none is None:
                missing.append(view_model)

    if missing:
        fail("these pages say nothing when they are empty: " + ", ".join(missing) +
             " - a blank grid with no sentence reads as lost data (or write "
             "`// empty-page: none - <why>` on the page)")

    problems = []

    for view_model in built:
        text = (DOTNET / f"Area11.Desktop/ViewModels/{view_model}.cs").read_text(
            encoding="utf-8", errors="replace")

        # The pages that wrote down why they have no empty state are already answered above.
        if re.search(r"// empty-page: none - (.{20,})", text):
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
    check_binding_modes()
    check_crash_report_honesty()
    check_report_tool()
    check_page_guard()
    check_empty_page()
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
