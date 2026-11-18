# Black box menu — focused D-115 change

**v0.95.1 Plover · 2026-11-10 · source implemented; Windows UNRUN.**

## مالک کی ہدایت

Book Check کی نئی metadata/رقم masking تجویز پر کام آگے نہ بڑھائیں۔ صرف Black box اسی شخص کو menu میں دکھائیں جسے متعلقہ حساس معلومات کی مکمل اجازت ہو۔ اس کے لیے موجودہ **Audit + View Purchase Cost + View Profit + main ON** کا مشترک نتیجہ استعمال ہوا؛ہر غیرمتعلق shop module کی grant مراد نہیں۔ Supplier کا D-114 accounting exception بدستور الگ ہے۔

## Small implementation, not a new audit rollout

- `ShellViewModel.BuildNavigation` now sets `canAudit` from existing `AuditHistory.CheckAccess(_user) is { Ok: true, Value: true }`, instead of the Audit checkbox alone.
- Existing CheckAccess resolves current active/same-role account, Audit permission, Cost/main and Profit/main. Failure and successful-but-metadata-only (`Value=false`) both hide the Black box menu entry. This also respects an Owner financial denial/main OFF.
- Permission hover/help documents this menu prerequisite. Audit checkbox still stores its existing independent permission; it does not silently grant financial rights or other history modules.
- **No Audit service/SQL/writer/records, BookCheck, CSV, backup, internal layout or financial calculation changed.** Existing metadata/detail safety on a previously opened Audit page remains unchanged, as requested. This is menu visibility, not replacement authentication or a new all-route access guarantee.
- Navigation is built when the signed-in shell is created. **After changing permissions/main, sign out and sign in to rebuild the menu.** This patch does not add instant live-menu rebuilding or force-close an already-open Black box. Its existing active-view masking checks remain.

## Evidence

- **84 static checks PASS**:402 grammar files,4294 calls,8361 member references,0 problems; not a compiler.
- One new regression fact exercises all8 Audit/Cost/Profit grant combinations, main OFF including Owner, restoration and stored account deactivation via the exact existing eligibility result used by the menu. **1741 total C# cases UNRUN**.
- Three menu-gate source mutations rejected by the focused guard, original bytes restored.
- Existing synthetic Audit metadata and BookCheck snapshot SQLite probes PASS; SQL services unchanged. Not C#/WPF execution.
- Release label/theme/icon updated solely to identify this patch. No installer or fabricated screenshot.

## Windows check — still UNRUN

Use only verified backup and genuinely separate test DB/photos/VM, not another exe folder pointing at shop data. No fake live-shop postings.

1. Audit-only,Cost-only,Profit-only oronefinancialgrantmissing: new signed-in menu has no Black box.
2. All3grants + main ON: new signed-in menu includes Black box; existing viewer behaves as before.
3. Owner main OFF or explicit financial denial: no Black box after sign-in. Restore grants/main and sign in: visible again.
4. Other menus/BookCheck and Supplier B exception unchanged. Existing opened Audit masks still operate; do not assume instant removal of old menu/preview/files.
5. Run `tools\RUN-TESTS.cmd`:expected1741cases, preserve full real log; WPF build/launch separately required.

Full Phase4, BookCheck review, Windows/DPAPI/restore, remaining outputs/session/files/cloud/faults and installer remain open. This focused request does not close them.
