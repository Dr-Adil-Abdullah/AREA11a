# ⏳ 5.1 — Recovery word کا پرانا فارم اور محفوظ ہونے کا نتیجہ — v0.132.0 Chicory

تاریخ **2026-11-10**۔ تازہ next سے اسی5.1 میں recovery-word editor کا جائزہ؛نئی security/financial policy،CreateStaff،حصہ6 یا installer کی اجازت نہیں۔ **1تا5 کے باقی کام اور ضروری تصدیق سے پہلے6 نہیں۔**

## ⏳ مسئلہ اور محدود source اصلاح

پہلے `AuthService.SetRecoveryWord` → `SettingsService.SaveRecoveryHash` صرف موجودہ Owner کی اجازت چیک کرکے hash/audit لکھتا تھا۔ Loaded baseline نہیں تھی،COMMIT outcome نہیں دیکھا جاتا تھا؛بعد کے cache/UI کام کی ناکامی محفوظ تبدیلی کو بھی عام failure کہہ سکتی تھی۔

- نیا `AuthService.LoadRecoveryEditForm(actor)` اندرونی Settings reader سے **ایک authorized Snapshot** لیتا ہے۔ Current Owner/session اور persisted recovery setting اسی read میں؛app cache سے baseline نہیں۔ خراب JSON/read failure پر explicit failure،جعلی factory editor نہیں۔
- Immutable `RecoveryEditForm` میں صرف `UsesFactoryWord` اور JSON-ignored local `EditVersion`؛کوئی word/hash/raw JSON property نہیں۔ Result کا `RecoveryEdit` بھی JSON ignored۔ عمومی Settings editor/bulk/UI-only دروازے اس key کے لیے بدستور بند ہیں۔ پہلے سے موجود Core/settings internals کی ہر secret read کا عالمی audit اس checkpoint کا دعویٰ نہیں۔
- نئی `recovery_word` baseline میں **exact persisted key/value**؛missing row،JSON null اور explicit empty string الگ content states ہیں،چاہے factory behavior یکساں ہو۔ Timestamp،cache،unrelated settings baseline میں نہیں۔ Token Database instance/scope سے بندھا،global recovery target0؛Staff/Permissions token اسے نہیں کھولتا۔
- Explicit checked `SetRecoveryWord(actor,word,expected)` null کو بھی reject کرتا ہے،چاہے isolated sessions-disabled host ہو۔ Current Owner پھر write transaction کے اندر compare،**پہلے UPSERT سے پہلے**۔ Hash+nonsecret audit+exact post-write form/token ایک transaction میں۔
- پرانا دو-argument imperative API اور اندرونی hash writer الگ compatibility doors ہیں؛ان کے current Owner/session checks برقرار،مگر loaded baseline لازم نہیں۔ ان کو بھی transaction outcome ملتا ہے؛**تمام writers checked ہونے کا دعویٰ نہیں**۔
- Normalization،4–40 کی پرانی حد،PBKDF2،خالی لفظ سے factory word **area11** کی بحالی اور audit details کے دو پرانے nonsecret جملے برقرار۔ Recovery-word change password/PIN reset نہیں؛موجودہ session policy نہیں بدلی،نئی fresh-login شرط نہیں لگائی۔

## ⏳ Outcome،cache اور Desktop

- صرف `Tx` واپس آنے پر success handoff/cache update۔ Definite pre-COMMIT rollback پر `NoCommitConfirmed`؛COMMIT attempt/unknown cleanup پر **unconfirmed**،کوئی token/form/cache update نہیں۔
- Transaction کی تسلیم شدہ کامیابی کے بعد cache refresh خراب ہو تو saved form کے ساتھ **Ok + SAVED warning**؛محفوظ hash کو ناکام یا safe-to-retry نہیں کہتے۔ اصل provider/cache-fault execution ابھی باقی؛source contract اس کا runtime ثبوت نہیں۔ Recovery sign-in پہلے کی طرح direct DB پڑھتا ہے۔
- Staff refresh پر recovery form پہلے invalid،typed word صاف،پھر authorized load۔ یہ **پورے shop/Owner کا recovery word** ہے،منتخب staff member کا نہیں؛اسی لیے person بدلنے سے کوئی target-specific recovery form نہیں بنتا۔ Refresh/access denial اسے صاف کرتے ہیں۔
- Save کے وقت موجودہ Staff busy guard access/binding callbacks سے پہلے؛account/permission saves،selection/roster reload اور access timer متعلقہ reentry سے رکے رہتے ہیں۔ Loaded token کے بغیر Save نہیں۔
- Success کی acknowledgement پہلے،پھر result کا **عین نیا form/token**،typed word کا backing value صاف،پھر observable bindings/status۔ کوئی fresh read/rebase نہیں؛دوسرے writer کی بعد کی تبدیلی اگلے Save پر دیکھی جائے گی۔
- Known no-commit پر اصل loaded form/input برقرار؛یہ **تازہ DB state کی ضمانت نہیں**،conflict پر Refresh/review اور لفظ دوبارہ داخل کریں۔ Unknown پر form/input invalid؛پہلے verify،پھر explicit refresh/review۔ Ack کے بعد callback failure پر **SAVED**،نہ blind reset کی دعوت۔

### ⏳ Content CAS کی اہم حد

یہ write-counter/attempt ledger نہیں۔ Factory→factory میں stored content یکساں رہتا ہے،لہٰذا پرانا token دوبارہ قابل قبول ہوسکتا ہے اور دوسری audit بن سکتی ہے۔ اسی normalized nonempty word کو دوبارہ save کرنے پر نیا random salt/hash content بدل دیتا ہے۔ A→B→عینA کی درمیانی history بھی baseline نہیں پکڑتی۔ یہ نہ durable retry identity،نہ cross-page/restart/cross-PC exactly-once ہے؛حصہ6 یہاں شروع نہیں کیا۔

## ✅ صرف محدود مقامی ثبوت

- **123 static checks**؛454 C# files grammar parsed،member heuristics میں کوئی reported مسئلہ نہیں؛یہ compiler/runtime/security certification نہیں۔
- **34 نئی lexical mutations** رد اور اصل bytes بحال؛تمام **30 source-negative scripts** پاس۔ پرانے Owner/generic-secret gates نہیں ہٹائے۔
- **37 migrations،37 Python-owned WAL/DELETE plans**؛actual recovery UPSERT/Audit SQL پر change/reset cross-conflicts،presence/exact JSON、metadata exclusion、audit/write rollback+retry、deferred COMMIT failure、exact handoff、write reservation、snapshot اور malformed read۔ الگ عارضی DB،financial posts صفر۔ Python اپنے hash/compare/transaction/outcome assumptions چلاتا ہے؛C# services/cache/auth/provider/WPF/LAN proof نہیں۔
- پچھلے Staff33、Permissions30、Workbook18、Hardware38、Pricing26、Settings24、Icon24、Draft40、Edit18 reference plans دوبارہ پاس؛وہ بھی اصل .NET/Windows ثبوت نہیں۔ پرانے tracked C# tests،37 migrations،Database/session/PasswordHasher اور financial services برقرار۔

## ⏳ 26 نئے C# cases — UNRUN

`RecoveryEditConcurrencyTests.cs`:14 Facts +12 theory rows۔ چار change/factory combinations،missing/default،uncached read،timestamp/unrelated setting،دوسرا handle/context/scope/null،input correction،exact handoff،normalization/hash/cache/audit،malformed read،immutable/no-secret JSON،audit rollback/retry،deferred COMMIT unknown،current Owner/session،same-word salt،repeated equivalent factory،demoted Owner اور imperative/generic-path compatibility۔

**3300 suite UNRUN**۔ Release build کوشش exit127: `dotnet: command not found`۔ آخری اصل Windows نتیجہ v0.120.1 کے **2963total/2941PASS/22FAIL** ہی ہے؛حصوں2تا5 کے بعد کا rerun ابھی باقی۔ Cache/observable fault کے live hooks نہیں بنائے،اور lexical checks کو ان کا executed test نہیں کہا۔

## ⏳ PT-74 — صرف الگ Windows ماحول میں

Verified encrypted backup **اور واقعی الگ DB/photos/config** پہلے؛اصل shop میں experimental posts/faults نہیں۔

1. **3300/0fail/0skip** کا اصل TRX/log،Core/Tests/Desktop Release؛پرانے AuthRecovery/OwnerBoundary/session/Settings tests بھی۔
2. دو loaded recovery editors میں custom→custom،custom→factory،factory reset کے بعد دوسری پرانی custom/reset intents؛پہلی مختلف content تبدیلی برقرار،دوسری atomic refuse،کوئی جزوی audit/hash/cache نہیں۔ Missing/empty/null、same timestamp、دوسرا handle、unrelated settings بھی۔
3. Wrong context/scope/null،current Owner deactivate/demote،expired/copy proof،malformed JSON/read failure؛failed read کو factory editor نہ کہیں۔ Generic Settings/workbook/UI-only پر key بند رہے۔ لفظ/hash نہ audit/result JSON میں آئے،نہ UI field میں واپس دکھے۔
4. Valid normalization/4–40،empty→area11،nonempty→factory word بند ہونے کی actual recovery sign-in جانچ؛throttle کے existing tests برقرار۔ Factory→factory content-equivalent save کی حد الگ درج کریں،اسے durable dedup نہ کہیں۔
5. Audit/write/provider COMMIT/rollback/dispose،postcommit cache failure اور post-ack binding/status exceptions۔ Known rollback پر جائز correction/retry؛unknown پر token/input invalid اور verify؛acknowledged success پر **SAVED**۔
6. Refresh/access denial،pointer/keyboard/doubleclick、account/permission-save reentry،success کے فوراً بعد دوسرے writer اور پھر اگلا Save۔ Exact handoff کو تازہ read سے نہ بدلیں؛Refresh typed word صاف کرے۔ Actual WPF/provider/cache execution ابھی باقی ہے۔

## ⏳ باقی 5.1

CreateStaff،remaining native/custom/imperative Settings writers کی inventory؛startup registry+SQLite race/partial restore،PT-67–74 کی اصل Windows/provider/UI/file/physical gates؛پھر اسی حصہ5 میں StockTake/toggle/bulk/snapshot کے باقی راستے۔ **Recovery editor کا source شامل ہونے کو پورا5.1 مکمل نہ کہیں۔ 1تا5 کے باقی کام+ضروری تصدیق سے پہلے6 نہیں؛❌installer نہیں بنایا۔**

## ⏳ بعد کا اسی5.1 کا CreateStaff checkpoint

v0.133.0 میں CreateStaff کا observed outcome/currentPIN/exactrow اور localguard source شامل؛[PT-75/موجودہحدود](STAFF-CREATE-OUTCOME-REVIEW-URDU.md)۔ اوپرکےنتائجاپنےcheckpointکےتاریخی؛remainingwriters/registryاوراصلWindows/provider/WPFابھیباقی،5.1مکملنہیں۔
