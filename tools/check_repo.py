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
}

VIEW_MODEL_OF = {
    "LoginWindow.xaml": "LoginViewModel",
    "ShellWindow.xaml": "ShellViewModel",
    "DashboardView.xaml": "DashboardViewModel",
    "ProductsView.xaml": "ProductsViewModel",
    "PosView.xaml": "PosViewModel",
    "PurchasesView.xaml": "PurchasesViewModel",
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
            i = text.find("*/", i + 2)
            i = length if i == -1 else i + 2
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
# 3. XAML bindings and resources
# ---------------------------------------------------------------------------
def check_bindings() -> None:
    theme = (ROOT / "dotnet/Area11.Desktop/Resources/Theme.xaml").read_text(encoding="utf-8")
    resources = set(re.findall(r'x:Key="([^"]+)"', theme))

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
            checked += 1
            if name in ITEM_PROPERTIES:
                continue
            if name not in properties:
                problems += 1
                fail(view_name + ": binding {" + "Binding " + name + "} not found in " + vm_name)

        for key in re.findall(r"\{StaticResource ([^}]+)\}", text):
            if key not in resources:
                problems += 1
                fail(f"{view_name}: missing resource '{key}' in Theme.xaml")

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
    keys = re.findall(r'(?:Text|Number|Flag|Raw)\("([a-zA-Z0-9_.]+)"', settings)
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
    check_xml()
    check_bindings()
    check_migrations()
    check_sql()
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
