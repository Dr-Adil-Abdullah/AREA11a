# VERSIONS - every build the owner received, and its colour

> **Why the colour changes.** The owner tests every build on his own computer, and several copies of
> the folder exist (downloads, extracts, clones). A colour on the sign-in screen answers one question
> instantly: *"am I looking at the new one?"* So **every code change ships with a new colour**.
>
> The colour lives in exactly two places, and `tools/check_repo.py` fails if they disagree with the
> last row of this table:
> - `dotnet/Area11.Core/Utils/BuildInfo.cs` - `Version`, `CodeName`, `BrandHex`
> - `dotnet/Area11.Desktop/Resources/Theme.xaml` - `BrandBrush`, `BrandDarkBrush`, `BrandSoftBrush`
>
> When the finished installer arrives (Phase 4), the colour stops being a version marker and becomes
> an ordinary setting the owner can pick (rule E-02).

## How to release a new version (for the agent)

1. Pick the next colour from the palette below (never reuse the previous one).
2. Set `Version`, `CodeName`, `BrandHex`, `BrandDarkHex`, `BrandSoftHex`, `ReleasedOn` in `BuildInfo.cs`.
3. Paint the same three colours on `BrandBrush`, `BrandDarkBrush`, `BrandSoftBrush` in `Theme.xaml`.
4. Add a row to the table at the bottom of this file - newest row **last**.
5. Run `python3 tools/check_repo.py`; it compares all three and refuses to pass on a mismatch.
6. Commit with the version in the message: `v0.2.0 - receipt and instant printing`.

White text sits on the brand colour (buttons, badges), so a colour must stay dark enough to read:
contrast **4.5:1 or better** against white.

## Colour palette (pick in order, then start again)

| Order | Name | Brand | Hover / dark | Soft tint |
|---|---|---|---|---|
| 1 | Teal | `#0E7490` | `#155E75` | `#ECFEFF` |
| 2 | Green | `#15803D` | `#166534` | `#F0FDF4` |
| 3 | Pink | `#DB2777` | `#BE185D` | `#FDF2F8` |
| 4 | Purple | `#6D28D9` | `#5B21B6` | `#EDE9FE` |
| 5 | Indigo | `#4338CA` | `#3730A3` | `#EEF2FF` |
| 6 | Blue | `#1D4ED8` | `#1E40AF` | `#EFF6FF` |
| 7 | Rose | `#E11D48` | `#BE123C` | `#FFF1F2` |
| 8 | ~~Amber~~ | `#B45309` | `#92400E` | `#FFFBEB` | *skipped - already the warning colour* |
| 9 | Slate | `#334155` | `#1E293B` | `#F1F5F9` |
| 10 | ~~Emerald~~ | `#047857` | `#065F46` | `#ECFDF5` | *skipped - already the success colour* |

## Released versions

| Version | Date | Colour | Hex | What changed |
|---|---|---|---|---|
| 0.1.0 | 2026-10-19 | Teal | `#0E7490` | First written code: 15 migrations, Core services, login, dashboard, products, POS, purchases, 56 tests |
| 0.1.1 | 2026-10-20 | Green | `#15803D` | **First successful Windows build** (Core, Desktop and Tests compiled); SQLite security pin; `tools/RUN-TESTS.cmd` |
| 0.1.2 | 2026-10-20 | Pink | `#DB2777` | Colour test confirmed by the owner; stale-solution repair tools (`CHECK-AREA11.cmd`, `doctor.ps1`) |
| 0.2.0 | 2026-10-20 | Purple | `#6D28D9` | Receipt on paper (58 / 80 mm), instant ESC/POS printing, reprint, printer settings (F-13); version badge on the sign-in window and in the sidebar |
| 0.2.1 | 2026-10-20 | Indigo | `#4338CA` | Owner decided printing is a **setting**: automatic after each sale or manual button. Both paper widths (58 / 80 mm) stay selectable. |
| 0.2.2 | 2026-10-20 | Blue | `#1D4ED8` | **Fix:** the receipt tests reached for an internal type (`CS0122`) and stopped the build. They now go through the public `ReceiptService.Build` against a real bill in the database; the checker refuses any test that names an internal type. |
| 0.2.3 | 2026-10-20 | Rose | `#E11D48` | Owner asked whether he must download a ZIP every time. `START-AREA11.bat` names the branch it is building and stops with instructions if the folder sits on `main`; the Urdu guide explains GitHub Desktop / Visual Studio / VS Code cloning; Windows line endings restored in four helper scripts. |
| 0.2.4 | 2026-10-20 | Slate | `#334155` | **Settings screen (F-17)**: shop details, printing (58/80 mm, automatic or manual), discount limits, tax, negative stock, staff list, one checkbox per permission, add-a-person form. Settings saves are now one transaction and are refused if a value would break the shop. |

## Notes for the next agent

- **Do not** edit an old row. The table is a record, like the audit log.
- **Amber `#B45309` and Emerald `#047857` are skipped** on purpose: they are already the colours of
  the warning and success messages, and a brand colour must never mean two different things.
- The version marker is temporary scaffolding for the test period; it must not become a second
  source of truth for the shop's branding. `store.name` / `brand.*` in Settings stay the real branding.
- If a build is released without a colour change, the owner cannot tell it apart from the previous
  one - treat that as a bug, not a shortcut.
| 0.2.5 | 2026-10-20 | Fuchsia | `#A21CAF` | **Hold cart (F-14)** - park a bill in the database, serve the next customer, bring it back; **split payment (F-15)** - cash + online/card/khata on one bill, with the credit part on the khata and never in the drawer. Receipt prints each part. |
| 0.3.0 | 2026-10-21 | Olive | `#4D7C0F` | **Phase 2 starts - returns.** Return against the original bill with strict limits (never more than sold, never more than collected), khata reduced before cash, goods back on the same batch or into **quarantine** with a permanent Q-number, the three quarantine decisions, and a printable `RET-nnnn` receipt. 122 tests. |
| 0.3.1 | 2026-10-21 | Bronze | `#92400E` | **Alert board (F-24).** A red bar on every page for minus stock and waiting quarantine; an Alerts page that lists every live alert; only the owner can end one, only with a written reason of 3+ characters; the reason is kept for ever and can be restored. An ended alert re-opens by itself when the medicine moves again. 131 tests. |
| 0.3.2 | 2026-10-21 | Plum | `#86198F` | **No-bill (rush) returns, `PR-nnnn` (F-22).** The customer has no paper bill: refund at once and put the goods on the shelf; the entry keeps a permanent number and blinks red until the real bill is attached (amber afterwards, until the owner writes a reason). Attaching a bill never adds stock twice; cancelling needs a reason and takes the same stock back out; the slip says on paper that the bill is missing, and the smart lookup finds a bill by medicine, phone, name or date. 145 tests. |
| 0.3.3 | 2026-10-21 | Navy | `#1E3A8A` | **Write-off & count correction (F-25).** Expired, broken, lost or a count that did not match - valued at **purchase cost**, recorded in the stock ledger and in its own history, entered in the black box. Writing off more than the book says is **allowed with a big red warning** (U-30) and still never blocks. A count that finds more adds the stock back. No edit and no delete: a mistake is corrected by a second entry with its own reason. 157 tests. |
| 0.3.4 | 2026-10-21 | Cyan | `#0891B2` | **Stock-take `ST-nnnn` (F-26).** Open a session and the book is **frozen**; count the shelves row by row with book / counted / difference / value side by side and an "only differences" pass; **Apply** (owner only, once) sets every counted batch to the count, writes `adjust` movements and values shortage/excess at the **frozen cost**. Negative book stock is corrected, medicine found on the shelf gets a `GINTI-ST-nnnn` batch, uncounted lines are never zeroed silently, and a cancelled session changes nothing. 173 tests. |
| 0.3.5 | 2026-10-21 | Ruby | `#9F1239` | **First Phase-2 Windows build - fix (CS0509).** The owner's build stopped in `Area11.Core` at `ReturnService.cs(504)`: `ItemRow` derived from the sealed `ReturnLineOption`. The type is no longer sealed (it is a transport model; the planner adds the batch id on top of it), and `tools/check_repo.py` gained a 17th check that refuses **any** class deriving from a sealed one, so this class of error cannot reach the shop PC again. No behaviour changed. 173 tests. |
| 0.3.6 | 2026-10-21 | Sky | `#0369A1` | **Build fix (CS0136) + a checker that can see it.** `SaleService.Plan` declared `var method` inside the payment loop while the same member declares `string method` further down; C# refuses that in either order. The loop variable is now `rowMethod`, and `tools/check_repo.py` gained an 18th check that walks every member, records which `{}` blocks enclose each local, and fails when two declarations have equal or nested scopes (CS0136/CS0128) - two local functions each with a `bold` parameter stay legal. No behaviour changed. 173 tests. |
