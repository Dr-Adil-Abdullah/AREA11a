#!/usr/bin/env python3
"""Build the owner's tick-list from the board - one file to print, one file to read (D-91).

Why this exists: the owner asked for a list he can hold in his hand, with a mark against every
feature, instead of reading the chat. A list written by hand would be out of date the next round,
so it is **generated** from `docs/PROGRESS.md` - the board is the only place a feature's state is
decided, and this script copies that state into two files:

    docs/CHECKLIST-URDU.md    - readable in the repo (and in Visual Studio)
    docs/CHECKLIST-URDU.html  - printable on A4, with a box to tick with a pen

Run it after any change to the board:

    python3 tools/make_checklist.py

The marks are the owner's own vocabulary:

    (tick)   every piece of work for the feature is finished and tested
    (half)   the code is finished, one action in the shop still has to prove it
    (cross)  not started

`tools/check_repo.py` fails if these files disagree with the board, so the printed list can never
claim a feature is done while the board says otherwise.
"""

from __future__ import annotations

import datetime
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
BOARD = ROOT / "docs" / "PROGRESS.md"
BUILD_INFO = ROOT / "dotnet" / "Area11.Core" / "Utils" / "BuildInfo.cs"
OUT_MD = ROOT / "docs" / "CHECKLIST-URDU.md"
OUT_HTML = ROOT / "docs" / "CHECKLIST-URDU.html"

DONE = "✅"
HALF = "🟡"
TODO = "❌"

DONE_WORD = "مکمل"
HALF_WORD = "جاری"
TODO_WORD = "شروع نہیں"

# What the owner should click to see each phase working, in his own words. Kept next to the phase
# heading so a future agent updates the same list the reader is looking at.
PHASES = (
    {
        "heading": "## Phase 1",
        "urdu": "فیز 1 — بنیاد، دوا اور بل",
        "why": "یہاں دکان کا روزمرہ کام ہے: دوا کی فہرست، خریداری، اور کاؤنٹر پر بل۔",
        "checks": (
            "`START-AREA11.bat` دو بار کلک کریں اور سائن اِن کریں (صارف نام خالی، پاس ورڈ `area11`)۔",
            "`Products` → **New medicine**: نام، پیک، ریٹ لکھ کر محفوظ کریں۔ فہرست میں دوا آ جائے۔",
            "`Counter (POS)` → اُس دوا کو تلاش کر کے کارٹ میں ڈالیں → **Save** → بل نمبر `INV-` سے شروع ہو اور رسید پرنٹ ہو۔",
            "`Purchases` → ایک خریداری `PINV-` لکھیں (سپلائر، بیچ، میعاد) → دونوں میں اسٹاک بڑھے۔",
        ),
    },
    {
        "heading": "## Phase 2",
        "urdu": "فیز 2 — واپسی، اسٹاک اور الرٹس",
        "why": "یہاں وہ کام ہیں جو بل کے بعد آتے ہیں: واپسی، خراب مال، گنتی، اور کھاتے۔",
        "checks": (
            "`Returns` → بل نمبر یا فون سے بل ڈھونڈیں → واپسی `RET-` بنے اور اسٹاک واپس آئے۔",
            "`Write-off` → میعاد ختم یا ٹوٹی دوا لکھیں → تاریخ اور وجہ کے ساتھ ریکارڈ ہو۔",
            "`Stock-take` → ایک گنتی `ST-` بنائیں → کتاب اور گنی ہوئی تعداد کا فرق نظر آئے۔",
            "اوپر کی گھنٹی (🔔) دبائیں → الرٹس اپنے الفاظ میں نظر آئیں۔",
        ),
    },
    {
        "heading": "## Phase 3",
        "urdu": "فیز 3 — کھاتے، رپورٹس، بیک اپ",
        "why": "یہاں دکان کا حساب ہے: گلہ، اخراجات، رپورٹس، ادھار کی وصولی، اور بیک اپ۔",
        "checks": (
            "`Shift (galla)` → گلہ کھولیں، رقم گنیں، بند کریں → فرق (کم/زیادہ) لکھا آئے۔",
            "`Expenses` → ایک خرچ (بجلی، چائے) لکھیں → رپورٹ میں نظر آئے۔",
            "`Reports` → آج / 7 دن / 30 دن کے نمبر دیکھیں؛ `Tax report` مہینے کا حساب دکھائے۔",
            "`Backup & restore` → **Back up now** → `C:\\ProgramData\\Area11\\backups` میں ZIP بنے۔",
        ),
    },
    {
        "heading": "## Phase 4",
        "urdu": "فیز 4 — باقی سب (دکان کے لیے اضافی سہولتیں)",
        "why": "یہاں وہ سہولتیں ہیں جو ہر دکان استعمال نہیں کرتی — لیکن لکھی اور آزمائی ہوئی ہیں۔",
        "checks": (
            "`Printer & scanner` → ٹیسٹ پرچی چھاپیں (58 یا 80 mm)؛ سکینر سے بارکوڈ آزمائیں۔",
            "`Own fields` → ایک نیا خانہ بنائیں → وہی خانہ گاہک یا دوا کے فارم پر فوراً نظر آئے۔",
            "`Second counter (LAN)` اور `Cloud sync` صرف اُس وقت آزمائیں جب آپ واقعی دوسرا کمپیوٹر یا کلاؤڈ استعمال کریں۔",
            "`Check the books` → «کتابیں ملتی ہیں؟» کا جواب دیکھیں (یہ صرف پڑھتا ہے، کچھ بدلتا نہیں)۔",
        ),
    },
)

# The five features whose code is finished and which need exactly one action in the shop. The words
# are the owner's, not a developer's: what he does, and what he should see.
HALF_ACTIONS = {
    "F-04": "`Black box` کھولیں: آج جو بھی کام کیا (دوا شامل، بل، واپسی)، اُس کی ایک سطر نظر آئے — کس نے، کب، کیا بدلا۔",
    "F-08": "`Products` → **New medicine** → ایک دوا **بیچ کے ساتھ** (بیچ نمبر، میعاد، تعداد) محفوظ کریں۔",
    "F-09": "`Purchases` → ایک اصل ڈیلیوری لکھیں (سپلائر، بیچ، میعاد، تعداد) → اسٹاک بڑھے اور واجب الادا بنے۔",
    "F-10": "`Counter (POS)` → ایک اصل بل بنائیں اور دکان کے پرنٹر پر نکالیں — رسید پوری اور صاف آئے۔",
    "F-46": "`Printer & scanner` → سکینر سے ایک بارکوڈ پڑھوائیں اور دیکھیں کہ دوا خود آ جائے (چھاپنے والا حصہ آپ نے 23 اکتوبر کو آزمایا تھا)۔",
}

# A short title is honest, but sometimes it is not the whole story. These lines are printed under the
# phase table so the printed page cannot mislead: a feature that was built and then removed is not
# the same thing as a feature the shop can open today.
NOTES = {
    "F-53": "نمونہ دکان (sample shop) بنا اور آزمایا گیا، پھر آپ کے کہنے پر ہٹا دیا گیا (D-77) — اِس لیے پروگرام میں یہ صفحہ نہیں ہے۔",
    "F-16": "پہلی اصل ونڈوز build اور ٹیسٹ — 23 اکتوبر 2026 کو آپ کے کمپیوٹر پر **0 warning / 0 error** اور **730 میں سے 730**۔",
}

# Not features: things that are written but have never been done on the shop PC. They are in the
# list because the owner asked "what is really left", and a list that hides them would be a lie.
OPEN_ITEMS = (
    ("انسٹالر (F-47)", "`MAKE-INSTALLER.cmd` چلائیں، پھر بنی ہوئی فائل دو بار کلک کریں — پروگرام `C:\\Program Files\\Area11` میں جائے اور آپ کا ڈیٹا `C:\\ProgramData\\Area11` میں جیسے تھا ویسے رہے۔ **یہ آج تک کبھی نہیں چلا۔**"),
    ("صفحات کا ہاتھ سے چلنا", "v0.34.0 کھول کر کم از کم پانچ صفحے دیکھیں (Products، Counter، Bills، Customers، Reports) اور `%TEMP%\\Area11\\last-run.txt` میں اُن کے نام دیکھیں۔"),
    ("دوسرا کاؤنٹر (F-45)", "کوئی شیئر فولڈر بنا کر `SHARE-AREA11.cmd` چلائیں — یہ صرف اُس وقت ضروری ہے جب آپ واقعی دوسرا کمپیوٹر لگائیں۔"),
)


def build_info() -> tuple[str, str, str]:
    """The version, its colour name and hex - read from BuildInfo.cs, one source of truth."""
    text = BUILD_INFO.read_text(encoding="utf-8")

    def take(name: str) -> str:
        match = re.search(name + r'\s*=\s*"([^"]+)"', text)
        return match.group(1) if match else "?"

    return take("Version"), take("CodeName"), take("BrandHex")


def short_title(title: str) -> str:
    """The first, readable half of a board title: what the feature IS, not its long history."""
    text = title.replace("**", "").strip()
    cuts = sorted({index for index in (text.find(": "), text.find(" - "), text.find(" ("))
                   if index >= 6})

    # Cut at the first natural break that still leaves a sentence worth printing: "Products" alone
    # tells the owner nothing, "Products: search, create, edit, box/strip/unit packing, prices, rack"
    # tells him what he is ticking.
    longer = [index for index in cuts if index >= 26]

    if longer:
        text = text[:longer[0]]
    elif len(text) > 86 and cuts:
        text = text[:cuts[-1]]

    text = text.strip(" .:;-")

    if len(text) > 86:
        text = text[:83].rstrip(" ,.;:-") + "..."

    return text


def read_board() -> tuple[list[dict], dict[str, int]]:
    """Every feature row, in board order, with the phase it sits under, plus the counts."""
    lines = BOARD.read_text(encoding="utf-8").splitlines()
    features: list[dict] = []
    counts = {DONE: 0, HALF: 0, TODO: 0}
    phase = ""

    for line in lines:
        if line.startswith("## Phase"):
            # "## Phase 1 - foundation and billing (...)" -> "## Phase 1", the key PHASES uses.
            phase = " ".join(line.split(" ")[:3])
            continue

        match = re.match(r"^\| (F-\d+) \| (.+?) \| (✅|🟡|⬜) \|", line)
        if match is None:
            continue

        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        state = DONE if cells[2] == DONE else (HALF if cells[2] == HALF else TODO)

        features.append({
            "id": match.group(1),
            "title": short_title(cells[1]),
            "state": state,
            "phase": phase,
            "done_when": cells[-1] if len(cells) > 3 else "",
        })
        counts[state] += 1

    return features, counts


def percent(part: int, whole: int) -> str:
    """A share, with "0%" written plainly - "0.0%" reads like a number that was almost something."""
    return "0%" if part == 0 else f"{part * 100 / whole:.1f}%"


def markdown(features: list[dict], counts: dict[str, int], today: str,
             version: str, code_name: str, brand: str) -> str:
    """The readable copy: one table per phase and a page for what is left."""
    total = sum(counts.values())
    lines = [
        "# Area11 — مکمل فیچر ٹک لسٹ (پرنٹ کرنے کے لیے)",
        "",
        f"**ورژن:** v{version} ({code_name} `{brand}`) · **تاریخ:** {today}",
        "",
        "> یہ فائل **خودکار** بنتی ہے: `python3 tools/make_checklist.py`۔ ہاتھ سے نہ بدلیں۔",
        "> اصلی فیصلہ `docs/PROGRESS.md` کے بورڈ میں ہوتا ہے، اور `tools/check_repo.py` اُس سے میل",
        "> کھانے پر مجبور کرتا ہے — سو یہ فہرست کبھی جھوٹ نہیں بول سکتی۔",
        "",
        "## ایک نظر میں",
        "",
        "| حالت | گنتی | فیصد |",
        "|---|---|---|",
        f"| {DONE} مکمل | {counts[DONE]} | {percent(counts[DONE], total)} |",
        f"| {HALF} جاری — کوڈ مکمل، دکان میں ایک عمل باقی | {counts[HALF]} | {percent(counts[HALF], total)} |",
        f"| {TODO} شروع نہیں | {counts[TODO]} | {percent(counts[TODO], total)} |",
        f"| **کل فیچر** | **{total}** | **100%** |",
        "",
        "## نشانیوں کا مطلب",
        "",
        f"- {DONE} = **{DONE_WORD}**: کوڈ لکھا، ٹیسٹ لکھے، اور (جہاں ممکن تھا) آپ کے کمپیوٹر پر چل چکا۔",
        f"- {HALF} = **{HALF_WORD}**: کام مکمل ہے مگر ایک عمل دکان میں ہونا باقی ہے (نیچے ہر ایک کے لیے ایک سطر ہے)۔",
        f"- {TODO} = **{TODO_WORD}**: ہاتھ نہیں لگا۔",
        "- ☐ = آپ کا اپنا نشان: جب آپ خود دیکھ لیں تو قلم سے ٹک لگا دیں۔",
        "",
    ]

    for phase in PHASES:
        rows = [feature for feature in features if feature["phase"] == phase["heading"]]
        if not rows:
            continue

        done = sum(1 for feature in rows if feature["state"] == DONE)
        half = sum(1 for feature in rows if feature["state"] == HALF)
        todo = sum(1 for feature in rows if feature["state"] == TODO)

        lines += [
            f"## {phase['urdu']}",
            "",
            f"**اس حصے میں {len(rows)} فیچر — {done} {DONE} کامل · {half} {HALF} جاری · {todo} {TODO} شروع نہیں**",
            "",
            f"*{phase['why']}*",
            "",
            "| ☐ | نمبر | فیچر | حالت |",
            "|---|---|---|---|",
        ]

        for feature in rows:
            word = {DONE: DONE_WORD, HALF: HALF_WORD, TODO: TODO_WORD}[feature["state"]]
            lines.append(f"| ☐ | {feature['id']} | {feature['title']} | {feature['state']} {word} |")

        lines += ["", "**دکان میں کیسے دیکھیں (5 منٹ):**", ""]
        lines += [f"{number}. {check}" for number, check in enumerate(phase["checks"], start=1)]
        lines.append("")

        pending = [feature for feature in rows if feature["state"] == HALF]
        if pending:
            lines += ["**اِس حصے میں باقی:**", ""]
            for feature in pending:
                action = HALF_ACTIONS.get(feature["id"], feature["done_when"] or "دکان میں ایک عمل باقی ہے۔")
                lines.append(f"- {HALF} **{feature['id']} — {feature['title']}**: {action}")
            lines.append("")

        noted = [feature for feature in rows if feature["id"] in NOTES]
        if noted:
            lines += ["**نوٹ:**", ""]
            for feature in noted:
                lines.append(f"- {feature['id']}: {NOTES[feature['id']]}")
            lines.append("")

    lines += [
        "## جو فیچر نہیں، مگر ابھی آزمایا نہیں گیا",
        "",
        "| ☐ | کیا | کیسے |",
        "|---|---|---|",
    ]
    for name, how in OPEN_ITEMS:
        lines.append(f"| ☐ | {name} | {how} |")

    lines += [
        "",
        "## یاد رکھنے کی ایک بات",
        "",
        "**جتنے فیچر لکھنے تھے، سب لکھے جا چکے ہیں** — فہرست میں کوئی ❌ نہیں۔ باقی صرف **دکان میں ایک ایک عمل** ہے",
        "اور اُس کا پیمانہ اوپر لکھا ہے۔ جب آپ کوئی عمل کر لیں اور کہیں کہ «ہو گیا»، تو اُس فیچر کا نشان اگلی دوڑ میں",
        f"{DONE} ہو جائے گا — اِس فائل میں خود بخود۔",
        "",
        f"*بنایا گیا: {today} · `tools/make_checklist.py`*",
        "",
    ]

    return "\n".join(lines)


def html(features: list[dict], counts: dict[str, int], today: str,
         version: str, code_name: str, brand: str) -> str:
    """The printable copy: A4, big marks, one box per feature to tick with a pen."""
    total = sum(counts.values())

    def esc(text: str) -> str:
        return (text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))

    parts: list[str] = [
        "<!DOCTYPE html>",
        '<html lang="ur" dir="rtl">',
        "<head>",
        '<meta charset="utf-8" />',
        f"<title>Area11 - مکمل فیچر ٹک لسٹ - v{version} ({code_name})</title>",
        "<style>",
        "  @page { size: A4; margin: 12mm; }",
        "  * { box-sizing: border-box; }",
        '  body { font-family: "Segoe UI", "Noto Nastaliq Urdu", "Jameel Noori Nastaleeq", sans-serif;',
        "         color: #111827; font-size: 10.5pt; line-height: 1.75; margin: 0 auto; max-width: 190mm; }",
        f"  .brand {{ border-right: 8px solid {brand}; padding: 6px 12px; margin-bottom: 10px; }}",
        "  h1 { font-size: 17pt; margin: 0 0 2px 0; }",
        "  h2 { font-size: 13pt; margin: 16px 0 6px 0; border-bottom: 2px solid #e5e7eb; padding-bottom: 3px; }",
        "  .muted { color: #6b7280; font-size: 9.5pt; }",
        "  .en { direction: ltr; unicode-bidi: embed; font-family: Consolas, 'Segoe UI', monospace; font-size: 9.5pt; }",
        "  table { width: 100%; border-collapse: collapse; margin: 6px 0 10px 0; }",
        "  th, td { border: 1px solid #d1d5db; padding: 4px 6px; vertical-align: top; }",
        "  th { background: #f3f4f6; font-size: 9.5pt; }",
        "  td.mark { text-align: center; width: 16mm; font-size: 13pt; }",
        "  td.box { text-align: center; width: 12mm; font-size: 14pt; }",
        "  td.id { width: 18mm; direction: ltr; text-align: left; font-family: Consolas, monospace; font-size: 9pt; }",
        "  .summary td { font-size: 11pt; }",
        "  .half { background: #fffbeb; }",
        "  .todo { background: #fef2f2; }",
        "  .checks { background: #f9fafb; border: 1px dashed #9ca3af; padding: 6px 10px; margin: 4px 0 12px 0; }",
        "  .checks ol { margin: 4px 0 0 0; padding-right: 18px; }",
        "  .page-break { page-break-before: always; }",
        "  .legend span { display: inline-block; margin-left: 14px; }",
        "  .sign { margin-top: 14px; border-top: 1px solid #d1d5db; padding-top: 8px; }",
        "</style>",
        "</head>",
        "<body>",
        '<div class="brand">',
        "<h1>Area11 — مکمل فیچر ٹک لسٹ</h1>",
        f'<div class="muted">ورژن v{version} ({code_name} <span class="en">{brand}</span>) · {today} · '
        f'کل {total} فیچر</div>',
        "</div>",
        '<div class="legend">',
        f"<span>{DONE} {DONE_WORD}</span><span>{HALF} {HALF_WORD}</span><span>{TODO} {TODO_WORD}</span>"
        "<span>☐ = آپ کا اپنا نشان</span>",
        "</div>",
        "<h2>ایک نظر میں</h2>",
        '<table class="summary">',
        "<tr><th>حالت</th><th>گنتی</th><th>فیصد</th></tr>",
        f'<tr><td>{DONE} مکمل</td><td>{counts[DONE]}</td><td>{percent(counts[DONE], total)}</td></tr>',
        f'<tr class="half"><td>{HALF} جاری</td><td>{counts[HALF]}</td>'
        f"<td>{percent(counts[HALF], total)}</td></tr>",
        f'<tr class="todo"><td>{TODO} شروع نہیں</td><td>{counts[TODO]}</td>'
        f"<td>{percent(counts[TODO], total)}</td></tr>",
        f"<tr><th>کل</th><th>{total}</th><th>100%</th></tr>",
        "</table>",
        '<p class="muted">جاری کا مطلب: کوڈ مکمل ہے، دکان میں ایک عمل باقی ہے (ہر ایک کے لیے نیچے ایک سطر ہے)۔</p>',
        '<p class="muted">یہ فہرست خودکار بنتی ہے (<span class="en">tools/make_checklist.py</span>) اور '
        '<span class="en">docs/PROGRESS.md</span> کے بورڈ سے میل کھاتی ہے؛ چیکر اِسے جانچتا ہے۔</p>',
    ]

    for index, phase in enumerate(PHASES):
        rows = [feature for feature in features if feature["phase"] == phase["heading"]]
        if not rows:
            continue

        done = sum(1 for feature in rows if feature["state"] == DONE)
        half = sum(1 for feature in rows if feature["state"] == HALF)
        todo = sum(1 for feature in rows if feature["state"] == TODO)
        page_break = ' class="page-break"' if index else ""

        parts += [
            f"<h2{page_break}>{esc(phase['urdu'])}</h2>",
            f'<div class="muted">{len(rows)} فیچر — {done} {DONE} · {half} {HALF} · {todo} {TODO} — '
            f"{esc(phase['why'])}</div>",
            "<table>",
            "<tr><th>ٹک</th><th>نمبر</th><th>فیچر</th><th>حالت</th></tr>",
        ]

        for feature in rows:
            word = {DONE: DONE_WORD, HALF: HALF_WORD, TODO: TODO_WORD}[feature["state"]]
            css = "half" if feature["state"] == HALF else ("todo" if feature["state"] == TODO else "")
            css_attr = f' class="{css}"' if css else ""
            parts.append(
                f'<tr{css_attr}><td class="box">☐</td><td class="id">{esc(feature["id"])}</td>'
                f'<td><span class="en">{esc(feature["title"])}</span></td>'
                f'<td class="mark">{feature["state"]} {word}</td></tr>'
            )

        parts += ["</table>", '<div class="checks"><b>دکان میں کیسے دیکھیں:</b><ol>']
        parts += [f"<li>{esc(check)}</li>" for check in phase["checks"]]
        parts.append("</ol></div>")

        pending = [feature for feature in rows if feature["state"] == HALF]
        if pending:
            parts.append("<b>اِس حصے میں باقی:</b><ul>")
            for feature in pending:
                action = HALF_ACTIONS.get(feature["id"], feature["done_when"] or "دکان میں ایک عمل باقی ہے۔")
                parts.append(f'<li>{feature["state"]} <b>{esc(feature["id"])}</b> — '
                             f'<span class="en">{esc(feature["title"])}</span>: {esc(action)}</li>')
            parts.append("</ul>")

        noted = [feature for feature in rows if feature["id"] in NOTES]
        if noted:
            parts.append('<div class="muted"><b>نوٹ:</b><ul>')
            for feature in noted:
                parts.append(f'<li>{esc(feature["id"])}: {esc(NOTES[feature["id"]])}</li>')
            parts.append("</ul></div>")

    parts += [
        '<h2 class="page-break">جو فیچر نہیں، مگر ابھی آزمایا نہیں گیا</h2>',
        "<table>",
        "<tr><th>ٹک</th><th>کیا</th><th>کیسے</th></tr>",
    ]
    for name, how in OPEN_ITEMS:
        parts.append(f'<tr><td class="box">☐</td><td>{esc(name)}</td><td>{esc(how)}</td></tr>')

    parts += [
        "</table>",
        '<p class="muted">جتنے فیچر لکھنے تھے، سب لکھے جا چکے ہیں — فہرست میں کوئی ❌ نہیں۔ باقی صرف دکان میں '
        "ایک ایک عمل ہے۔ جب آپ کوئی عمل کر لیں، اگلی دوڑ میں اُس کا نشان خود بدل جائے گا۔</p>",
        '<div class="sign">',
        '<div>میرا دستخط: ______________________________ &nbsp;&nbsp; تاریخ: ____________</div>',
        '<div class="muted">یہ صفحہ پرنٹ کرنے کے لیے ہے: Ctrl+P دبائیں، کاغذ A4 رکھیں، اور ہر سطر کے '
        "خالی خانے میں قلم سے ٹک لگائیں۔</div>",
        "</div>",
        "</body>",
        "</html>",
        "",
    ]

    return "\n".join(parts)


def main() -> int:
    features, counts = read_board()
    version, code_name, brand = build_info()
    today = datetime.date.today().isoformat()

    OUT_MD.write_text(markdown(features, counts, today, version, code_name, brand), encoding="utf-8")
    OUT_HTML.write_text(html(features, counts, today, version, code_name, brand), encoding="utf-8")

    total = sum(counts.values())
    print(f"wrote {OUT_MD.relative_to(ROOT)} and {OUT_HTML.relative_to(ROOT)}: "
          f"{total} features - {counts[DONE]} done, {counts[HALF]} in progress, {counts[TODO]} not started")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
