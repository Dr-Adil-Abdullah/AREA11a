# Installing Area11 on the shop PC (F-47, D-19)

> **اردو خلاصہ:** `tools\MAKE-INSTALLER.cmd` بلڈ کمپیوٹر پر ایک بار چلائیں۔ جو فولڈر `dist\Area11-setup`
> بنتا ہے وہ USB پر دکان کے کمپیوٹر پر لے جائیں اور اُس میں `INSTALL-AREA11.cmd` پر ڈبل کلک کریں۔ ونڈوز
> اجازت مانگے گی — **Yes** دبائیں۔ پروگرام `C:\Program Files\Area11` میں جاتا ہے، ڈیسک ٹاپ اور سٹارٹ مینو
> میں آئیکن بنتا ہے، اور "Add or remove programs" میں Area11 لکھا آتا ہے (اَن انسٹال سمیت)۔
> **دکان کا ڈیٹا `C:\ProgramData\Area11` میں رہتا ہے اور کبھی نہیں مٹایا جاتا** — نہ اپ ڈیٹ پر، نہ اَن انسٹال پر۔
> اپ ڈیٹ کا طریقہ یہی ہے: نیا انسٹالر بنا کر وہی فائل دوبارہ چلائیں۔

---

## The one rule

> **The program and the shop's data live in two different places, on purpose.**
>
> | What | Where | What happens on an update | What happens on an uninstall |
> |---|---|---|---|
> | The program | `C:\Program Files\Area11` | replaced | removed |
> | Every bill, khata, photo | `C:\ProgramData\Area11` | **not touched** | **not touched** |
>
> Installing again **is** the update. Uninstalling never deletes the shop. If the data were inside
> the program folder, one update would wipe the shop - so the installer refuses to run if the two
> folders ever point at the same place.

---

## On the build PC (once per release)

The build PC is the one with Visual Studio / the .NET SDK - the machine the code is pulled onto.

1. Pull the newest code (`START-AREA11.bat` does it, or GitHub Desktop → *Fetch origin* → *Pull origin*).
2. Double-click **`tools\MAKE-INSTALLER.cmd`**.
3. It builds a **self-contained** Area11: the .NET runtime and the SQLite engine travel inside the
   folder, so the shop PC needs **no .NET, no Python and no internet**. It takes about two minutes.
4. It writes:

   | Path | What it is |
   |---|---|
   | `dist\Area11-setup\` | the folder to copy on a USB stick |
   | `dist\Area11-setup.zip` | the same folder as one file (if Windows could zip it) |

   Inside that folder: `INSTALL-AREA11.cmd`, `UNINSTALL-AREA11.cmd`, `install\` (the two scripts),
   `Area11\` (the program), `SHARE-AREA11.cmd` (for the second counter) and `docs\INSTALL.md` (this page).

> `dist\` is ignored by git - it is a build output, never committed.

---

## On the shop PC

1. Copy `dist\Area11-setup` (or the zip) to the shop PC. If it is the zip, right-click → **Extract All**.
2. Open the folder and double-click **`INSTALL-AREA11.cmd`**.
3. Windows asks for permission → **Yes**.
4. Read the report it prints, then Area11 opens by itself.

**What the report says.** Every line is one of four words:

| Word | Meaning |
|---|---|
| `ready` | this PC already has what is needed |
| `note` | worth knowing; never a reason to stop |
| `does` | not right yet, and **the installer repairs it by itself** |
| `STOP` | the install cannot go on until a person does something |

The three things that can stop an install, and nothing else:

| STOP because | What to do |
|---|---|
| Windows older than 10 build 17763 (1809) | install Windows updates, or use a newer PC |
| less than about 600 MB free | free some space, run the installer again |
| no program to copy and no .NET SDK | run `MAKE-INSTALLER.cmd` on the build PC and copy `dist\Area11-setup` |

**What the installer does by itself** (the `does` lines):

- **Builds** the program when only the source and the .NET SDK are here.
- **Starts the print spooler** and sets it to start with Windows.
- **Adds `Microsoft Print to PDF`** when the PC has no printer at all, so a receipt can still be
  printed or saved as a PDF (Z-07: a missing printer never stops the shop).
- **Gives `Users` write access** to `C:\ProgramData\Area11`. Without this a cashier's own Windows
  account could not write the shop folder and Area11 would quietly fall back to that person's private
  `%LocalAppData%` - one shop with two databases. This is the most important line in the installer.

**What it cannot do:** a vendor's printer driver cannot be installed silently without the maker's own
package. Install the receipt printer in Windows as usual (or let Windows find it); Area11 prints to
whatever printer Windows has. A USB barcode scanner needs no driver at all - it types like a keyboard.

---

## Updating

1. On the build PC: pull the code and run `tools\MAKE-INSTALLER.cmd` again (the colour of the version
   changes, so the new build is recognisable).
2. Copy the folder to the shop PC and double-click `INSTALL-AREA11.cmd` again.
3. If Area11 is open, the installer asks you to close it first - it never kills a running till.
4. The program files are replaced, the version in *Add or remove programs* changes, and the shop data
   is exactly where it was. The first window you see is the sign-in window in the **new colour**.

---

## Uninstalling

Either:

- **Start → Settings → Apps → Installed apps → Area11 → Uninstall**, or
- Start menu → **Uninstall Area11**, or
- double-click `C:\Program Files\Area11\UNINSTALL-AREA11.cmd`.

Type **YES** when it asks. It removes the program folder, the three shortcuts and the
*Add or remove programs* entry, and then prints the shop folder's path.

> **`C:\ProgramData\Area11` stays.** Nothing in it is deleted - not the database, not the photos, not
> the backups. Installing Area11 again finds it and carries on where the shop left off. To move the
> shop to a new PC, copy that whole folder; it is the shop.

---

## Where everything is

| Item | Path |
|---|---|
| Program | `C:\Program Files\Area11\Area11.exe` |
| Shop data | `C:\ProgramData\Area11\area11.db` |
| Photos | `C:\ProgramData\Area11\photos` |
| Backups | `C:\ProgramData\Area11\backups` |
| Logs (and the installer's own log) | `C:\ProgramData\Area11\logs` |
| Desktop icon | `C:\Users\Public\Desktop\Area11.lnk` (everyone who signs in) |
| Start menu | `...\Start Menu\Programs\Area11.lnk` and `Uninstall Area11.lnk` |
| Add/remove entry | `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\Area11` |
| Second counter helper | `C:\Program Files\Area11\SHARE-AREA11.cmd` (see [SECOND-COUNTER.md](SECOND-COUNTER.md)) |

The installer writes its own log to `C:\ProgramData\Area11\logs\install-<date>.log`, and the report it
printed to `install-report.txt` beside it. Send that file when something looks wrong.

---

## Portable use is still there

`START-AREA11.bat` in the repository folder still works: it pulls the code, builds it and runs it
without installing anything. That is how the code is tested while it is being written. Both ways read
the same shop folder, so a portable copy and an installed copy see the same shop - but only one of
them should be open at a time on one PC.

---

## The rules behind it

- `dotnet\Area11.Core\Services\InstallService.cs` - which folders, which shortcuts, what "ready"
  means, and the three things that can stop an install. Pinned by `InstallTests.cs`.
- `dotnet\Area11.Core\Models\InstallModels.cs` - the report the owner reads.
- `tools\install\install.ps1` - the hands that do it (copy, permissions, icons, registry).
- `tools\install\uninstall.ps1` - the hands that undo it, and that never touch the data.
- `tools\check_repo.py` - fails the build if the script and the C# ever disagree about a folder name,
  and if the uninstaller ever grows a line that deletes the shop folder.

The same program answers the readiness question at install time:

```
Area11.exe --install-check report.txt --source dist\Area11 --printers "HP LaserJet 1020" --spooler running --sdk no
```

Exit code `0` = go, `2` = a line says STOP, `3` = the report could not be written.

---

## If something goes wrong

| What you see | What it means | What to do |
|---|---|---|
| Windows says *"Do you want to allow this app to make changes?"* | normal - the installer writes to Program Files | press **Yes** |
| nothing happens after **Yes** | the new window opened behind this one | look at the taskbar; the installer window is there |
| `install\install.ps1 is missing` | the folder was copied partially, or only the `.cmd` was taken | copy the **whole** `Area11-setup` folder again |
| `The report says STOP ...` | a STOP line is printed above | read that line; it names the fix |
| `Area11 is open` | a till window is still running | close Area11, run the installer again |
| `The copy failed (robocopy code N)` | a file in `C:\Program Files\Area11` is open | close Explorer windows on that folder and try again |
| cashier cannot save a bill | the folder permission line failed | run `INSTALL-AREA11.cmd` again as Administrator |
| the sign-in window is the **old** colour | Windows started the old copy | close every Area11 window; the desktop icon points at the installed program |
