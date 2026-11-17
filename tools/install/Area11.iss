; ============================================================================
;  Area11 - the single-file installer  (Phase 5, owner's word, Nov 2026)
;
;  One file, double-click, permission question, installed like a normal app.
;  Built by tools\MAKE-SETUP.cmd on the BUILD PC (needs the free Inno Setup 6
;  once: https://jrsoftware.org/isdl.php). Never edited for a version: the
;  version travels in as a /D define, so this script cannot disagree with
;  BuildInfo.cs - the file the sign-in screen reads.
;
;  What setup does, in order:
;    1. asks Windows for Administrator rights (PrivilegesRequired=admin);
;    2. copies the program to C:\Program Files\Area11 (an older copy replaced);
;    3. makes C:\ProgramData\Area11 writable for every signed-in person and NEVER
;       touches the database, the photos or a single bill in it - not on install,
;       not on upgrade, not on uninstall;
;    4. writes the desktop icon, the Start-menu entry and the uninstaller;
;    5. lists Area11 in "Add or remove programs" with its version and its icon;
;    6. starts the print spooler (result ignored - receipts need it, and a missing
;       printer never stops the shop);
;    7. runs the installed Area11.exe --enable-autostart, so Windows opens the
;       shop at every sign-in (the owner can switch it off in Settings, Shop).
;
;  The names (Area11, Area11.exe, the two folders) are the same words as
;  InstallService.cs - the brain - and tools\install\install.ps1 - the older
;  hands. That script stays as the backup path; this script is the single file.
;  The uninstall key is Inno's own (..._is1): the old script's key belongs to
;  the old path and is left alone, because no shop PC ever used that path.
; ============================================================================

#define MyAppName "Area11"
#define MyAppPublisher "Area11"
#define MyAppExeName "Area11.exe"
#define MyAppDataName "Area11"
; Passed by MAKE-SETUP.cmd from BuildInfo.cs; the fallback below only exists so
; opening this file in the Inno IDE still compiles for a look (never ship that).
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef MySourceDir
  #define MySourceDir "..\..\dist\Area11"
#endif

[Setup]
AppId={{925A79B7-F4CE-4C02-BB4F-63E4881C910B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
; admin: Program Files, the machine-wide start-up entry and the Add/Remove list
; all need it, and the shop PC runs this software for every Windows account.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
; The same floor as InstallService.MinimumWindowsBuild (Windows 10 1809): older than
; this, setup refuses with one sentence instead of installing a program that stutters.
MinVersion=10.0.17763
; An upgrade over a running shop asks to close it instead of failing on a lock.
CloseApplications=yes
; One file out: dist\Area11-Setup-v0.64.0.exe - the file the owner sends.
OutputDir=..\..\dist
OutputBaseFilename=Area11-Setup-v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Simple English (owner's word): short sentences, no jargon.
ShowLanguageDialog=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
; The shop's own folder: every signed-in person may write it (the cashier is not
; an administrator), and uninsneveruninstall keeps it through upgrades AND through
; uninstall - removing the program must never remove the shop's books.
Name: "{commonappdata}\{#MyAppDataName}"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Registry]
; A stale start-up entry would point at a deleted exe after uninstall, so setup
; owns this value's removal. Nothing is written here at install time - the
; program itself writes it (Area11.exe --enable-autostart, then Settings, Shop).
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Area11"; Flags: uninsdeletevalue

[Run]
; The first opening, offered with a tick (on by default): the shop sees at once
; that the install worked, and the sign-in screen is where the version is read.
Filename: "{app}\{#MyAppExeName}"; Description: "Open Area11 now"; Flags: nowait postinstall skipifsilent

[Code]
// Setup's last step, run from code (not [Run]) so a refusal warns instead of
// failing the install: exit 0 = on, 2 = refused with a sentence, 3 = unexpected.
// Either way the program itself is installed and works - auto-start can always
// be switched on later in Settings, Shop.
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  { The print spooler, like the older script's hands: receipts need it, and a PC with
    it stopped or disabled would fail every print. The result is ignored on purpose -
    already-running answers "no" to net start, and a missing printer never stops the
    shop (Z-07). Adding "Microsoft Print to PDF" itself stays a manual step - see the
    Urdu guide - because switching Windows features takes minutes, not seconds. }
  if CurStep = ssInstall then
  begin
    Exec('net.exe', 'start spooler', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;

  if CurStep = ssPostInstall then
  begin
    if Exec(ExpandConstant('{app}\{#MyAppExeName}'), '--enable-autostart', '',
           SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
    begin
      { Start-with-Windows is on; nothing to say. }
    end
    else
    begin
      MsgBox('Area11 is installed and works, but start-with-Windows could not be ' +
             'switched on. Open Settings, Shop and switch it on there.',
             mbInformation, MB_OK);
    end;
  end;
end;
