# ⏳ 5.1 — نیا Staff account: محفوظ ہونے کا نتیجہ اور دوبارہ Add — v0.133.0 Scabious

تاریخ **2026-11-10**۔ تازہ next سے اسی5.1 میں CreateStaff کے outcome/loaded-UI راستے کا جائزہ؛نئی account/financial policy،durable identity،حصہ6 یا installer نہیں۔ **1تا5 کے باقی کام اور ضروری تصدیق سے پہلے6 نہیں۔**

## ⏳ مسئلہ اور source اصلاح

پہلے CreateStaff موجودہ Owner،duplicate name،INSERT اور audit کو ایک transaction میں رکھتا تھا،مگر COMMIT کی غیر یقینی صورت اور بعد کی UI خرابی میں عام failure دیتا تھا۔ Desktop پر local attempt guard نہیں تھا؛success کے بعد پورا `LoadUsers()` دوسرے کھلے Staff/permission/recovery فارم بھی تازہ کر دیتا تھا۔

- `AuthService.CreateStaff` اب اسی transaction کا **TransactionOutcome** لیتا ہے۔ Owner/name/input failure یا definite pre-COMMIT rollback پر ہی `NoCommitConfirmed`؛COMMIT attempt/unknown cleanup پر **unconfirmed**،کوئی success ID/row/token نہیں۔
- موجودہ Owner/session check،trimmed name،case-insensitive duplicate-name query،inactive duplicate کا انکار،پرانے role mappings،PIN-only creation،PBKDF2 اور nonsecret audit metadata برقرار۔ Owner-role creation کا پرانا imperative API بھی نہیں بدلا؛نئی promotion policy نہیں بنائی۔
- PIN کی مقررہ لمبائی اب **اسی write-lock/transaction میں موجودہ DB setting** سے پڑھی جاتی ہے،پرانے service cache سے نہیں۔ Integer parse/missing یا غیرقابلِ parse value پر سابقہ4 کا fallback برقرار؛settings key کی پہلے سے موجود3–8 validation policy نہیں بدلی۔ یہ config-corruption کی ہر ممکن حالت کا نیا audit نہیں۔ UI کا ہمیشہ4digits والا label اب **configured length** ہے۔
- INSERT،exact inserted ID،nonsecret audit اور `CaptureStaffEdit` کا post-write row/token ایک transaction میں۔ Success صرف transaction واپس آنے کے بعد؛پرانا `Value` ID برقرار،ساتھ existing JSON-ignored `StaffEdit`/`EditVersion` handoff۔ Hash،PIN یا target کی authenticated session proof جاری نہیں ہوتی۔
- یہ **نیا account بنانے کا imperative intent** ہے،پرانے target کی edit نہیں؛اس کے لیے roster کی پوری فہرست کو CAS baseline نہیں بنایا۔ Duplicate check current write-lock میں ہی ہے۔ Name uniqueness کو durable request identity نہیں کہتے۔

## ⏳ Desktop: مقامی guard اور عین کامیاب نتیجہ

- اسی existing `PostingAttemptGuard` کا الگ readonly `_staffCreate` instance؛TryBegin اور `_savingStaff` access/observable callbacks سے پہلے۔ متعلقہ account/permission/recovery writers اور selection/roster reload synchronous save کے دوران existing busy guard سے رکتے ہیں۔
- Service کو captured name/role/PIN؛known no-commit failure پر correction/retry جائز،اصل input برقرار۔ **Unknown/throw** پر guard verification مانگتا ہے؛input بدلنے،Refresh،access denial/regrant یا Staff data صاف ہونے سے یہ guard reset نہیں ہوتا۔ عام pre-service access denial پر `CancelBeforeWrite`؛exception پر conservative verification ہوسکتی ہے۔
- Success پہلے acknowledge،exact ID مقامی field میں،name/PIN کے backing values صاف؛**اس کے بعد** bindings/collection/status callbacks۔ Malformed/missing handoff،wrong ID یا already-present ID پر بھی **SAVED** اور block؛دوسرا Add نہیں۔
- صرف returned created row `Users.Add(saved)` سے list کے آخر میں لگتا ہے؛موجودہ selection،permission/Pricing/recovery forms کو fresh read سے rebase نہیں کرتے۔ پہلے سے کھلے کسی stale editor کو خاموش نئی baseline نہیں ملتی۔ Explicit Refresh بعد میں list کی معمول کی ترتیب بحال کرتا ہے؛نئے شخص کو منتخب کرنا معمول کے picker سے ہوگا۔
- صرف اصل input consumption،row display اور status کے تمام callbacks کامیاب ہوں **اور آخر میں name/PIN اب بھی خالی،عین ID والا returned row اب بھی list میں موجود ہو** تو `CompleteReset`؛پھر الگ نیا account بن سکتا ہے۔ Ack کے بعد failure پر **SAVED + ID**؛guard committed/blocked رہتا ہے،نہ failure wording سے دوبارہ Add کی دعوت۔
- یہ local form lifetime ہے،database attempt ledger نہیں۔ نیا ViewModel/process/cross-PC یہی guard نہیں رکھتا؛manual verification کے بعد واقعی نیا page instance کھولنے کی Windows boundary ابھی غیرمصدقہ۔ Refresh کو verified reopen نہیں بنایا؛حصہ6 کی durable identity شروع نہیں کی۔

## ✅ صرف محدود مقامی ثبوت

- **124 static checks**؛C# grammar/member heuristics compiler/runtime نہیں۔
- **29 نئی lexical mutations** رد،اصل bytes بحال؛تمام **31 source-negative scripts** پاس۔ نئے guards کے لیے پرانے Owner/current roster/permission/recovery contracts نہیں ہٹائے۔
- **37 migrations،34 Python-owned WAL/DELETE plans**؛actual CreateStaff INSERT/duplicate/current-PIN/Audit SQL،چار roles،inactive/case duplicate،INSERT/audit rollback+retry/sequence،deferred COMMIT failure،exact row baseline،synthetic lost-ack control اور write reservation۔ الگ temporary DB،financial posts صفر۔
- Python اپنی validation/hash/transaction/outcome/guard assumptions چلاتا ہے؛یہ **AuthService/Database/TransactionOutcome/PostingAttemptGuard/auth/provider/WPF/LAN** کا executed ثبوت نہیں۔ Lost-ack plan میں Python COMMIT کے بعد اپنی variable بدلی،provider کے اصل dropped response کا fault injection نہیں۔
- Recovery37、Staff33、Permissions30、Workbook18、Hardware38、Pricing26、Settings24、Icon24、Draft40、Edit18 پچھلے محدود reference plans دوبارہ پاس۔ پرانے tracked C# tests،37 migrations،Database/session/PasswordHasher/financial services اور باقی Auth methods برقرار۔

## ⏳ 26 نئے C# cases — UNRUN

`StaffCreateOutcomeTests.cs`:5 Facts +21 theory rows۔ چار roles/exact ID/nonsecret row،پانچ invalid inputs،active/inactive duplicate،تین stale-cache/current PIN lengths،missing/malformed fallback،INSERT/audit failure/sequence/retry،deferred COMMIT unknown/no ID،current Owner/session/no target login،بعد کا writer/exact stale token اور پانچ synthetic guard observations۔ Guard کے Refresh/regrant/input scenarios **WPF execution نہیں**۔

متوقع **3326 suite UNRUN**۔ Release build کوشش exit127: `dotnet: command not found`۔ آخری اصل Windows evidence v0.120.1 کے **2963total/2941PASS/22FAIL** ہی ہیں؛حصوں2تا5 کے بعد کا runtime/Release/Desktop run باقی۔

## ⏳ PT-75 — صرف الگ Windows ماحول میں

پہلے verified encrypted backup **اور واقعی الگ DB/photos/config**؛اصل shop میں experimental accounts/posts/faults نہیں۔

1. **3326/0fail/0skip** کے اصل TRX/log،Core/Tests/Desktop Release؛پرانے Auth/OwnerBoundary/session/Staff/Recovery tests بھی۔
2. Account create میں ہر جائز role،trimmed/case/inactive duplicate،default/current PIN length،سروس کے stale cache کے باوجود دوسرے connection کی3/6/8 length change؛name/PIN کے غلط input پر کوئی account/audit/sequence change نہ ہو۔ Owner-only/current proof اور target کو نیا login proof نہ ملنا جانچیں۔
3. INSERT/audit fault،provider COMMIT/rollback/dispose،DB/settings read failure؛known rollback پر جائز correction/retry،unknown پر ID/row/token نہ آئے اور اسی page پر Add رکا رہے۔ Duplicate name کو lost commit کی verified شناخت نہ کہیں۔
4. Ack کے بعد name/PIN bindings、collection/status callback failures؛**SAVED + exact ID**،backing inputs consumed،guard blocked۔ Double click/Enter/reentry میں دوسرا account نہ بنے؛malformed success handoff بھی re-add نہ کھولے۔
5. Unknown/SAVED-refreshfailure کے بعد Refresh،typed name/role/PIN تبدیلی،revoke/regrant اور page/tab navigation؛guard کی اصل lifetime معلوم کریں۔ صرف plain refresh یا input clear اسے reset نہ کرے؛نئے instance/process کی حد کو durable dedup نہ کہیں۔
6. Normal success پر صرف نیا row آئے،پرانی selection/permission/Pricing/recovery inputs/baselines تازہ نہ ہوں؛بعد کے writer کو اگلا checked Save پکڑے۔ Row آخر میں دکھے،explicit Refresh سے ترتیب بحال؛الگ نیا legitimate account صرف مکمل successful cleanup کے بعد بنے۔

## ⏳ باقی 5.1

Remaining native/custom/imperative Settings writers کی inventory،startup registry+SQLite race/partial restore اور PT-67–75 کی اصل Windows/provider/UI/file/physical gates؛پھر اسی حصہ5 میں StockTake/toggle/bulk/snapshot کے باقی راستے۔ **CreateStaff outcome source شامل ہے،پورا5.1 مکمل نہیں۔ 1تا5 کے باقی کام+ضروری تصدیق سے پہلے6 نہیں؛❌installer نہیں بنایا۔**
