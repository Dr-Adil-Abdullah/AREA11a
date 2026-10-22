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
}


def fail(message: str) -> None:
    FAILURES.append(message)


def warn(message: str) -> None:
    WARNINGS.append(message)


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

        for name in re.findall(r"\b(?:class|record|enum|interface|struct)\s+(\w+)", masked_text):
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

        for name, type_ns in declared_ns.items():
            if project_of.get(name) == project:
                continue

            if own_ns == type_ns or own_ns.startswith(type_ns + "."):
                continue

            if f"using {type_ns};" in text:
                continue

            mention = re.search(rf"(?<![\w.]){re.escape(name)}\s*[.(<]", masked_text)

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

        if ch in "([{<":
            depth += 1
        elif ch in ")]}>":
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

    for path in (DOTNET / "Area11.Core").rglob("*.cs"):
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
def check_bindings() -> None:
    # Resource keys are checked for every .xaml by check_resources(); this function only checks the
    # bindings of the pages that have a view-model.

    # Rows inside a DataGrid / ListBox bind to a *model*, not to the page's view-model. Reading the
    # model files makes those bindings checkable instead of ticking a hand-kept allow-list: a typo
    # in a column ("BalanceTxt") now fails here rather than printing an empty column in the shop.
    model_properties: set[str] = set()
    for model in sorted((DOTNET / "Area11.Core/Models").glob("*.cs")):
        model_properties |= set(re.findall(
            r"public\s+[\w<>?\[\], .]+\s+(\w+)\s*(?:\{|=>)", model.read_text(encoding="utf-8-sig")))

    # Row classes live in the view-model files as well (NavItem for the menu, AlertRow for the bell).
    # Their public properties belong in the same pool as the models above: a template's DataContext
    # is the row, so `{Binding IsHeader}` is legal even though ShellViewModel has no such property.
    # Without this the checker would demand a hand-kept name list - and a hand-kept list is exactly
    # what let a wrong column name through in v0.4.9.
    for vm_file in sorted((DOTNET / "Area11.Desktop/ViewModels").glob("*.cs")):
        vm_text = vm_file.read_text(encoding="utf-8-sig")

        declares_row = any(name.endswith("Row") or name == "NavItem"
                           for name in re.findall(r"public\s+(?:sealed\s+)?class\s+(\w+)", vm_text))
        if declares_row:
            model_properties |= set(re.findall(
                r"public\s+[\w<>?\[\], .]+\s+(\w+)\s*(?:\{|=>)", vm_text))

    problems = 0
    checked = 0
    for view_name, vm_name in VIEW_MODEL_OF.items():
        view_path = DOTNET / "Area11.Desktop/Views" / view_name
        vm_path = DOTNET / "Area11.Desktop/ViewModels" / f"{vm_name}.cs"
        if not view_path.exists() or not vm_path.exists():
            fail(f"view or view-model missing for {view_name}")
            continue

        text = view_path.read_text(encoding="utf-8")
        vm_text = vm_path.read_text(encoding="utf-8")
        properties = set(re.findall(r"public\s+[\w<>?\[\], .]+\s+(\w+)\s*(?:\{|=>)", vm_text))
        commands = set(re.findall(r"public\s+ICommand\s+(\w+)", vm_text))
        properties |= commands

        for raw in re.findall(r"\{Binding ([^}]+)\}", text):
            name = raw.split(",")[0].strip()
            # "Source=" binds to the object itself (e.g. a static label), not to a view-model property
            if not name or name.startswith(("Path=", "RelativeSource", "ElementName", "Source=")):
                continue

            # `{Binding DataContext.X, RelativeSource=...ItemsControl}` reaches the page's own
            # view-model through the ancestor's DataContext, so X is a page property, not a row one.
            if name.startswith("DataContext."):
                name = name[len("DataContext."):]
            checked += 1
            if name in ITEM_PROPERTIES or name in model_properties:
                continue
            if name not in properties:
                problems += 1
                fail(view_name + ": binding {" + "Binding " + name + "} not found in " + vm_name)


    REPORT.append(f"XAML bindings: {checked} checked, {problems} problems")


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

    verbatim = re.compile(r'@"((?:[^"]|"")*)"', re.S)
    plain = re.compile(r'(?<![@$])"((?:[^"\\]|\\.)*)"', re.S)

    total = 0
    invalid = 0
    sources = sorted((DOTNET / "Area11.Core/Services").glob("*.cs"))
    sources += sorted((DOTNET / "Area11.Core/Data").glob("*.cs"))

    for path in sources:
        text = path.read_text(encoding="utf-8-sig")
        for pattern in (verbatim, plain):
            for match in pattern.finditer(text):
                sql = match.group(1).replace('""', '"').strip()
                if not re.match(r"(?i)^(SELECT|INSERT|UPDATE|DELETE|WITH)\b", sql):
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
    REPORT.append(f"SQL: {total} statements prepared, {invalid} invalid")


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

    starts = []
    for i in range(len(sql) - 6):
        if depths[i] == 0 and "".join(chars[i:i + 6]).upper() == "SELECT" \
                and (i == 0 or not chars[i - 1].isalnum()):
            starts.append(i)

    spans = []
    for start in starts:
        for i in range(start + 6, len(sql) - 4):
            if depths[i] == 0 and "".join(chars[i:i + 4]).upper() == "FROM" \
                    and not chars[i - 1].isalnum():
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

    for path in sorted((DOTNET / "Area11.Core/Services").glob("*.cs")):
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

    REPORT.append(f"Column mapping: {checked} selected column(s) checked against their model")


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

    settings = (DOTNET / "Area11.Core/Services/SettingsService.cs").read_text(encoding="utf-8")
    # The lookbehind matters: "GetRaw(" ends in "Raw(", and without it every reader of a setting
    # looks like a second declaration of it.
    keys = re.findall(r'(?<![A-Za-z0-9_])(?:Text|Number|Flag|Raw)\("([a-zA-Z0-9_.]+)"', settings)
    if len(keys) != len(set(keys)):
        duplicated = sorted({k for k in keys if keys.count(k) > 1})
        fail(f"duplicate settings keys: {duplicated}")
    REPORT.append(f"Settings: {len(set(keys))} keys")


# ---------------------------------------------------------------------------
# 6. Documentation links and referenced files
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
    check_resources()
    check_xml()
    check_bindings()
    check_migrations()
    check_sql()
    check_column_mapping()
    check_packages()
    check_version_colour()
    check_tests()
    check_docs()

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
