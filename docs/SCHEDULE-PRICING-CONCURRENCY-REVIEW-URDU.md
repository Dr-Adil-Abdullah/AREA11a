# ⏳ 5.1 جاری — Backup schedule اور Pricing limits — v0.126.0 Avens

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

> ⏳ تازہv0.128.0میںاسی5.1کےAppicon/fileکیsourceاصلاح: [PT-70/باقی5.1](ICON-EDIT-CONCURRENCY-REVIEW-URDU.md)؛3211suiteUNRUN۔ نیچےپچھلےcheckpointکاثبوتتاریخی؛iconباقیکےحوالےنئےجائزےکیحدتکبدلے۔ File+DBatomicity/Windowsاببھیسندنہیں؛5.1جاری،1تا5مکمل+تصدیقسےپہلے6نہیں۔


> ⏳ تازہ v0.127.0 میں اسی5.1 کے Hardware کی محدود source اصلاح شامل: [PT-69/باقی5.1](HARDWARE-EDIT-CONCURRENCY-REVIEW-URDU.md)؛3188suiteUNRUN۔ نیچے پچھلے checkpoint کا ثبوت تاریخی؛Hardware کے باقی source والے حوالے نئے جائزے کی حد تک بدلے۔5.1 ابھی جاری،1تا5مکمل+تصدیق سے پہلے6نہیں۔


## ⏳ اجازت اور ترتیب

تازہ `next` پر **اسی 5.1 Settings** کے دو مزید راستوں پر کام کیا۔ یہ نیا حصہ6 نہیں۔ مالک کی ہدایت برقرار: **حصوں1 تا5 کے باقی کام اور ضروری تصدیق مکمل کیے بغیر6 شروع نہیں کرنا۔** 5.1 بھی ابھی مکمل نہیں؛Hardware/icon/workbook،باقی Staff writes،startup registry اور اصل Windows جانچ نیچے درج ہیں۔

پہلے [مرکزی Settings فارم/PT-67](SETTINGS-EDIT-CONCURRENCY-REVIEW-URDU.md) اور اس سے پہلے Product/Customer/catalog اور مشترکہ Draft کے checkpoints اپنی محدود حیثیت کے ساتھ برقرار ہیں۔

## ⏳ Backup schedule میں source تبدیلی

- `BackupViewModel.LoadSchedule` اب `Settings.LoadEditForm` سے **backup.autoEnabled،backup.everyHours،backup.keep** ایک مجاز snapshot میں پڑھتا ہے۔ `BackupService` کی shared Settings cache سے تین الگ پرانی قدریں فارم نہیں بناتیں۔ Read failure پر token ختم،input خالی؛defaults کو کامیاب loaded form نہیں کہتے۔ دکھانے کی پرانی1–720گھنٹے/1–500فائل clamp برقرار۔
- `SaveSchedule` انہی تین keys کا loaded `SettingsEditForm` ساتھ بھیجتا ہے۔ موجودہ Backup یا Settings حق،validation،**BEGIN IMMEDIATE** کے اندر تمام submitted keys کا موازنہ اور ایک transaction میں settings+audit وہی مرکزی checked service کرتی ہے۔ ایک پرانی value ہو تو پورا schedule refuse؛کوئی key یا audit جزوی محفوظ نہیں۔
- کامیاب Save کا **عین اسی transaction والا** نیا form پہلے رکھا جاتا ہے،پھر bindings اور automatic-backup callback۔ بعد میں `Load()` کرکے کسی دوسرے writer کی state کو خاموشی سے اپنی baseline نہیں بناتا۔ اگلے Save تک دوسرا شخص بدلے تو وہ تبدیلی دوبارہ پکڑی جائے گی۔
- پہلے والا post-save `RunAutomaticIfDue` attempt برقرار،لیکن صرف acknowledged success کے بعد۔ یہ schedule transaction کا حصہ نہیں۔ اگر بعد کا callback/page cleanup throw ہو تو **SAVED**؛نامعلوم save پر form invalidate اور refresh/verify،نہ اندھی دوسری کوشش۔ Known no-commit پر اصل input/token تصحیح یا retry کے لیے برقرار۔ مقامی reentry guard callback کے دوران دوسری ScheduleSave روکتا ہے؛یہ durable identity نہیں۔
- `backup.lastRunAt/lastFile` جیسی telemetry ان تین keys کی baseline میں نہیں؛عام automatic-backup stamp سے بےوجہ schedule conflict نہیں۔ Backup-only grantee کے لیے الگ Settings permission لازمی نہیں بنائی؛پرانا موجودہ gate برقرار۔

**حد:** اس checkpoint میں actual encrypted package،password/restore،automatic scheduler کی تمام cached reads یا cross-PC backup coordination نہیں بدلے۔ BackupService کا automatic behavior اپنی سابقہ حدوں کے ساتھ ہے؛یہاں صرف loaded schedule edit کا راستہ checked ہے۔

## ⏳ Staff Pricing limits میں source تبدیلی

- Owner کا `ForUser(actor,target)` اب ایک **Snapshot** میں owner authorization،target کی موجودہ active/role/defaults،allowance row اور baseline دیتا ہے۔ Target کو خود لاگ اِن کرانا لازم نہیں؛مالک اپنے proof سے اس شخص کی settings دیکھتا ہے۔ عام signed-in `ForUser(user)` بھی snapshot میں مربوط پڑھائی کرتا ہے؛Sale کے اندر موجود `Read(connection,user)` اسی caller کی transaction استعمال کرتا ہے۔
- `pricing` baseline میں **target کا id/role/active** اور **user_pricing_allowances کی مکمل row یا اس کا نہ ہونا** شامل ہے۔ صرف نظر آنے والے defaults کافی نہیں؛missing row اور ویسی ہی explicit all-deny row بھی الگ persisted حالتیں ہیں۔ Target کے role سے owner/non-owner defaults بدل سکتے ہیں،اس لیے اس کا concurrent role change بھی پکڑتا ہے۔
- `Save` پہلے current authenticated/stored active Owner اور موجودہ target،پھر پرانے numeric validations چیک کرتا ہے۔ اس کے بعد **پہلے UPSERT/audit سے پہلے** expected state ملتی ہے۔ Wrong target/context/scope،stale grant،target role/active change اور enabled Desktop میں missing baseline refuse۔
- کامیاب Save اپنی اسی transaction کا exact نیا `EditVersion` واپس دیتا ہے؛Pricing editor اسی کو اگلے Save میں بھیجتا ہے۔ Target بدلنے،failed read یا staff denial پر پرانا token صاف؛unknown outcome پر refresh لازم۔ کسی دوسرے target کا token نئے selected target کے لیے کام نہیں کرتا۔
- Pricing token JSON-ignored؛allowance audit payload میں شامل نہیں۔ Audit کا نام اب caller کے supplied text کی بجائے اسی transaction میں پڑھے **stored Owner** سے۔ Supplied inactive Owner object بھی refuse؛legacy disabled fixtures کو حقیقی login proof نہیں کہتے۔

**منظورشدہ pricing policy نہیں بدلی:** percentage ranges،per-person grants،Extra Discount،rounding،actual cost floor،Sale allocation اورSupplierB جوں کے توں۔ 100% limits کی الگ پالیسی یا خودکار permissions merge نہیں۔ Sessions-disabled legacy host میں null expected کے پرانے compatibility behavior کو authenticated Desktop کا ثبوت نہیں کہتے؛Desktop loaded token لازماً بھیجتا ہے۔

## ✅ صرف محدود مقامی نتائج

- **117 static checks**؛444 C# files کی grammar/reference heuristics،Roslyn compiler نہیں۔
- **32 نئی lexical mutations رد،اصل bytes بحال**؛تمام24 source-negative scripts دوبارہ پاس۔ پہلے Settings/Draft/source contracts برقرار۔
- **37 migrations،26 Python-owned WAL/DELETE SQL plans**:تین schedule keys کی stale refusal،exact schedule handoff،telemetry exclusion؛Pricing row/default presence،concurrent grant،target role/active،actual allowance UPSERT+audit کی fault rollback/retry،write reservation اورcoherent snapshot۔ Financial posts صفر۔
- پچھلے Settings کے24 اور پہلے edit کے18 SQLite plans دوبارہ پاس۔ **Python اپنے fingerprint/compare/transaction/control flow چلاتا ہے**؛یہ C# services،auth،cache،pricing defaults کی runtime calculation،actual backup،provider COMMIT،WPF یاLAN ثبوت نہیں۔

## ⏳ C# اور Windows ثبوت باقی

`SettingsSchedulePricingConcurrencyTests.cs` میں **25 اضافی public-service cases**:10 schedule اور15 pricing۔ پچھلے C# tests نہیں بدلے۔ متوقع suite **3168**؛runtime discovery/compilation ابھی نہیں ہوئی۔ اصل .NET SDK موجود نہیں؛Release build کی کوشش exit127،`dotnet: command not found`۔

نئے cases میں تین schedule conflicts،current permissions،telemetry،context/load/null،audit rollback؛Pricing کے missing/explicit row،دوDatabase handles،wrong target/context/scope،target role/active،Owner deactivation،enabled missing-baseline،exact handoff،serialization،audit fault،stored Owner name اورowner-proof target شامل ہیں۔ **یہ سب لکھے ہوئے مگر UNRUN ہیں۔**

پرانا اصل Windows log اب بھیv0.120.1:2963total/2941PASS/22FAIL۔ حصوں2/3/4 اورپہلے5checkpoints کے بعد کا تازہ3168-suite/Windows ثبوت باقی؛پرانا PASS نئے source کی سند نہیں۔

## ⏳ PT-68 — صرف الگ Windows ماحول میں

verified encrypted backup **اور واقعی الگDB/photos/config** پہلے؛اصل دکان میں تجرباتی posts/faults نہیں۔

1. تازہ **3168/0fail/0skip** اورDesktop Release؛TRX/log محفوظ۔ Backup page دو test sessions/connections میں کھولیں؛A schedule کی ایک key بدلے،B پرانا مکمل schedule Save کرے:پورا batch refuse،Aکی تینوں درست قدریں برقرار،Bکا audit نہیں۔ Refresh/review کے بعد جائز Save۔
2. صرفBackup حق والے test cashier سے loaded schedule پڑھیں/محفوظ کریں؛BackupاورSettings دونوں حقوق واپس لینے کے بعد پرانی form refuse۔ Telemetry change پر غلط conflict نہ ہو؛load failure پر default form سےSave نہ ہو۔
3. Schedule success کے بعد binding/automatic-backup callback failure:واضح **SAVED**،نہ "not saved"۔ Unknown Save سےautomatic backup شروع نہ ہو۔ دو بارSave/Refresh/callback اورinput edits کی actual WPF جانچ؛no-current-read-rebase بھی دیکھیں۔
4. ایک Staff target کے Pricing limits دو جگہ کھولیں۔ پہلاgrant محفوظ کرے،دوسراپراناSave:واضح conflict،پہلاgrant/audit محفوظ،نیا grant/audit نہیں۔ Fresh reload اوراگلے جائز Save میںself-conflict نہیں۔ Target بدلنے پرپچھلے شخص کی baseline یا input نئے شخص پرنہچڑھے۔
5. Target role/deactivate،Owner deactivate،copied/expiredlogin اورpermission changes کےبعد oldpricing Save refuse۔ Read-only lookups/Sale/Quote میںapproved actual pricing rules کا regressions run؛اس checkpoint کو مالی حساب کی مکمل سند نہ کہیں۔
6. Actual audit/COMMIT/rollback/dispose faults،same-Database serialization،WAL/DELETE،اصلLAN busy اورModal/reentry جانچ باقی؛Python probe اصلLAN یاWindows کی جگہ نہیں۔

## ⏳ 5.1 میں اگلا باقی کام

1. **Hardware.Load/Save/ChoosePrinter**:cached form اور پانچ الگ SetUiOnly writes کا coherent/atomic/current-owner record جائزہ؛یہاں ہاتھ نہیں لگایا۔
2. **App icon/file،workbook Settings preview/apply،باقی Staff permission/reset/toggle callbacks** کی expected-state/intent inventory اورجہاں ضرورت اصلاح۔
3. **Startup registry+SQLite** کےدرمیان race/partial restoration،باقی imperative Settings writers اور actual WPF/COMMIT/cache callbacks۔ MainSettings کی PT-67 gates بھی باقی۔
4. Backup automatic scheduling کےruntime cache/parallel behavior کی حد اصل backup حصے کے gates کےساتھ واضح رکھیں؛اسےschedule edit کیتکمیل میںچھپائیںنہیں۔

**5.1 ابھی جاری ہے۔** اس کے بعد ہی StockTake اور5کے باقی routes ترتیب سے۔ Local content-baseline آخری persisted حالت ملاتی ہے،ہرintermediate revision نہیں؛A→B→عینA کوhistory counter کی طرح نہیں پکڑتی۔ ❌1تا5کے باقی کام+تصدیق سے پہلے6شروع نہیں؛installer نہیں بنایا۔
