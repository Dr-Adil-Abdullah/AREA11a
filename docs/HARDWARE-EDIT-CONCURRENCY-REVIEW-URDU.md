# ⏳ 5.1 جاری — Hardware فارم اور محفوظ ریکارڈ — v0.127.0 Yarrow

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

> ⏳ تازہv0.128.0میںاسی5.1کےAppicon/fileکیsourceاصلاح: [PT-70/باقی5.1](ICON-EDIT-CONCURRENCY-REVIEW-URDU.md)؛3211suiteUNRUN۔ نیچےپچھلےcheckpointکاثبوتتاریخی؛iconباقیکےحوالےنئےجائزےکیحدتکبدلے۔ File+DBatomicity/Windowsاببھیسندنہیں؛5.1جاری،1تا5مکمل+تصدیقسےپہلے6نہیں۔


## ⏳ اس اجازت کا دائرہ

مالک کے `next`،پھر موجودہ تبدیلیاں برقرار رکھ کر `continue` پر اسی **5.1 Settings** کے Hardware راستے پر کام کیا۔ [پچھلا Backup/Pricing checkpoint](SCHEDULE-PRICING-CONCURRENCY-REVIEW-URDU.md) اپنی حدود کے ساتھ برقرار ہے۔ **5.1 ابھی مکمل نہیں؛حصوں1 تا5 کے باقی کام اور ضروری تصدیق مکمل کیے بغیر6 شروع نہیں ہوگا۔** Installer نہیں بنایا۔

ابتدا میں مقامی Git کا HEAD پرانے base805626e پر تھا،مگر پوری working tree پچھلے حقیقی remote70c2812 سے عین ملتی تھی۔ الگ temporary index سے دونوں کا tracked اورuntracked موازنہ کیا؛کوئی مختلف فائل نہیں تھی۔ Fetch کے بعد **mixed reset صرف اسی موجودہ برانچ کے موجودہ published remote پر** کیا؛فائلوں کا مواد نہیں بدلا،کسی کا کام حذف نہیں کیا۔ یہ hard reset یا دوسری برانچ نہیں تھی۔ اس کے بعد صرف اس checkpoint کی متعلقہ تبدیلیاں کی گئیں۔

## ⏳ Source میں کیا بدلا

### مربوط پڑھائی

`Hardware.Load` اب Settings کی مشترکہ cache سے الگ الگ values نہیں لیتا۔ `LoadEditForm` ایک مجاز database snapshot سے یہ نو keys دیتا ہے:

- ریکارڈ: `hardware.testedAt`،`hardware.testedBy`،`hardware.note`،`hardware.testedWidth`،`hardware.passed`۔
- متعلقہ printer setup: `printer.name`،`printer.width`،`printer.copies`،`printer.autoCut`۔

Width،copies،cut،چھ statements اور سابقہ answer اسی detached form سے بنتے ہیں۔ Report کا token JSON-ignored ہے۔ Read failure کے بعد Desktop کا token،rows اورNote صاف؛پرانے input کو تازہ baseline کے ساتھ نہیں ملاتا۔ Explicit Refresh اب Note بھی database کی موجودہ Note سے بدلتا ہے؛پہلے پرانی nonempty Note نئی baseline کے ساتھ رہ سکتی تھی۔

`BuildSlip` بھی انہی keys اورstore.name کو ایک snapshot سے پڑھ کر document بناتا ہے۔ **یہ Windows driver یا actual spool target کی end-to-end ضمانت نہیں**؛نیچے physical حدود دیکھیں۔

### ایک checked transaction اور ایک audit

پہلے Save پانچ الگ `SetUiOnly` transactions اور بعد میں ایک swallowing `RecordStandalone` کرتا تھا۔ پہلے سے موجود ریکارڈ میں درمیانی fault سے ملا جلا answer رہ سکتا تھا؛date آخر میں لکھنا اس کا مکمل حل نہیں تھا۔

اب fixed internal `Settings.SaveHardwareTest`:

1. Write transaction میں موجودہ user/session/Settings حق resolve کرتا ہے۔ صرف Owner بننے کی نئی شرط نہیں؛مجاز Settings grantee کا سابقہ حق برقرار۔
2. **کسی write سے پہلے تمام نو keys کی loaded presence+exact persisted JSON baseline ملاتا ہے۔** Printer کی name/width/copies/cut میں بھی concurrent فرق ہو تو پورا پرانا answer refuse۔
3. پانچ record values **اور صرف ایک `hardware_test` audit** اسی transaction میں لکھتا ہے۔ Audit میں موجودہ stored actor استعمال ہوتا ہے۔ `CheckedBy` کا سابقہ displayed signed-in name behavior برقرار ہے؛اس text کو الگ authentication proof نہ سمجھیں۔
4. صرف اپنی پانچ written keys کی exact نئی baseline transaction کے اندر بناتا ہے؛printer dependencies کو نئی read سے rebase نہیں کرتا۔ Commit کے بعد cache میں صرف انہی پانچ values کو تازہ کرتا ہے۔
5. صرف معلوم pre-COMMIT rollback پر `NoCommitConfirmed`؛COMMIT/cleanup نامعلوم ہو تو refresh/verify،اندھا retry نہیں۔ Commit واپس آنے کے بعد cache failure ہو تو واضح **SAVED** error۔ اصل provider faults پھر بھی runtime gate ہیں۔

تمام چھ ticks کی تعریف،partial result کے لیے reason،unknown tick refusal،عرض کی58/80normalization،copy clamp اوررپورٹ کے پہلے الفاظ/معنی برقرار۔ Financial rules،stock،discount،موجودہ migrations اورپرانے C# tests نہیں بدلے۔

### Desktop exact handoff اور printer انتخاب

- Hardware Save اپنی loaded form لازماً بھیجتا ہے۔ کامیابی کی acknowledgment **bindings/status سے پہلے**؛واپس اسی save کا exact form،کوئی `Load()` نہیں۔ بعد کےcallback fault میں **SAVED**،نہ "کچھ محفوظ نہیں ہوا"۔ Known no-commit پرinput/token باقی؛unknown یاexception پرtoken ختم،دوبارہreviewلازم۔
- Record Save،Printer selection اورRefresh کو ایک page-local busy guard محفوظ رکھتا ہے۔ یہ durable retry identity،cross-PC dedup یاreopen verification نہیں۔
- `ChoosePrinter` loaded `printer.name` کے ساتھ checked Set کرتا ہے؛enabled Desktop میں null baseline refuse۔ صرف printer.name کی exact handoff ملتی ہے،باقی پرانی keys کو صحیح نہیں مان لیا جاتا۔ کسی دوسرے شخص نے اسی دورانhardware.note بدلی ہو تو کامیاب printer selection کے بعد بھی پرانا Hardware Save conflict دیتا ہے۔
- Failed selection پر displayed choice اصل محفوظ selection کی طرف واپسnotify؛unknown پرtoken ختم۔ کامیابselectionکیackبھیUIcallbacksسےپہلےاورcallbackfailureمیںSAVED۔
- Sessions-disabled legacy callers کے پرانے no-token API کو compatibility کے لیے رکھا ہے؛Record Save پہلے ایک تازہ مجاز form لیتا ہے۔ یہ حقیقی Desktop authentication یا loaded old form کی حفاظت کا دعویٰ نہیں۔

## ✅ صرف محدود مقامی جانچ

- **118 static checks**؛C# grammar اورreferences کی heuristic جانچ،compiler نہیں۔
- **31 نئی lexical negative mutations** رد؛اصل bytes بحال۔ تمام **25 source-negative scripts** دوبارہ پاس۔
- **37 migrations،38 Python-owned WAL/DELETE Hardware plans**:نوrecord/printer dependency conflicts،ہرپانچkeyپرsyntheticfault،auditfault،پوراrollback/retry،ایکaudit/exacthandoff،untouchedstalefield،writerreservation،coherentsnapshot۔ Financial posts صفر۔
- Python probe actual Hardware-record UPSERT اورAudit INSERT کا متن لیتا ہے،مگر fingerprint،compare،transaction اورfault-control **خود Python** چلاتا ہے؛یہ C# `SaveHardwareTest`،`TransactionOutcome`،current auth،cache،COMMITfault،WPF،driver،default-printer یاLAN ثبوت نہیں۔
- پچھلے Settings/schedulePricing probes کی SQL extraction صرف SetMany تک محدود کی،تاکہ نئے الگ Hardware helper کا دوسرا UPSERT غلطی سے شامل نہ ہو؛assertions نہیں بدلے۔ پچھلے Settings24،schedule/Pricing26،Draft40 اورEdit18 SQL plans بھی دوبارہ چلانے کی verification record checkpoint کی final جانچ میں شامل ہے۔

## ⏳ C# execution باقی

`HardwareEditConcurrencyTests.cs` میں **20 نئے isolated public-service cases**:11Facts اور9Theory rows۔ دوeditors،نوdependencies،settings/auditfault rollback،exacthandoff/singleaudit،cachedreadavoidance،permissionrevoke،wrongDatabase،telemetry،enabledmissingbaseline اورpartialprinterhandoff شامل۔

متوقع suite **3188**؛یہ **UNRUN** ہے۔ اس ماحول میں .NET SDK نہیں؛Release build کی کوشش `dotnet: command not found`۔ Grammar/referencePASS کوcompileیاruntimeنہیںکہتے۔ پچھلااصلWindowsنتیجہ اب بھیv0.120.1کے2963total/2941PASS/22FAIL؛حصوں2 تا5کےبعدکاactualrunباقی۔

## ⏳ PT-69 — اصل Windows جانچ،صرف الگ محفوظ ماحول میں

Verified encrypted backup **اورواقعی الگDB/photos/config** پہلے؛اصلshopمیںfaults/تجرباتیpostsنہیں۔

1. تازہ **3188/0fail/0skip**،Core/Tests/Desktop Release اورTRX/log محفوظ۔ دوHardwareformsکھولیں؛Aاپناanswerمحفوظکرے،BپراناSaveکرے:واضحconflict،Aکیپانچvaluesاورایکauditبرقرار،Bکیجزویvalue/auditنہیں۔ ExplicitRefresh/reviewکےبعدجائزSave۔
2. Settingsکےدوسرےconnectionسےprinter.name/width/copies/cutبدلیں؛پرانےHardwareanswerپرSaveپوراrefuse۔ Singleprinterselectionکےبعدکسیuntouchedstalehardwarefieldکوخاموشیسےصحیحنہمانے۔ WrongDatabase/incompletebaselineبھیrefuse۔
3. پانچوںwritesاورauditپرالگfault؛اصلproviderمیںrollbackاورretryدیکھیں۔ COMMIT/rollback/disposefaultپرunknownoutcomeکیسچیخبر؛عملیUIمیںدوہراSaveاورstaleRefreshسےخوددوسریwriteنہہو۔
4. Readfailure،explicitRefreshکیNote،revokedSettingsgrantee،deactivated/expiredsession،selectioncallbackاورackکےبعدbindingthrow؛**SAVED**اورunknownکیخبروںکوآپسمیںنہملائیں۔ Modal/reentryاورbusyguardاصلWPFمیںجانچیں۔
5. **58mmاور80mmکےاصلprinter،USBscanner،copies/cut،defaultprinterchangeاورPrintServiceکےactualtarget**ابھیالگphysicalgateہیں۔ Windowsdefault،driverیاpaperاننوSQLitekeysکےبغیربدلےتوtokenاسexternalchangeکونہیںپکڑتا۔ Storedtickصرفانسانکاanswerہے؛successfulDBSaveکوprinter/scannerPASSنہکہیں۔
6. BuildSlipsnapshotاورPrintServiceکےoutputکےدرمیانsettings/cache/Windowsdefaultchangesکیend-to-endجانچباقی۔ یہcheckpointاصلprintingکوجامعatomicoperationنہیںبناتا۔ اصلLAN/busy/power-lossاورbackup/restoreکےبعدformsبھیباقی۔

## ⏳ ابھی5.1میںباقی

- **App icon/file + database** کےدرمیانoutcome؛workbookSettingspreview/applyکیloadedbaseline۔
- باقیStaffpermissions/reset/toggle،native/custom/imperativeSettingswritersکیinventory؛Hardwareکیruntime`SetUiOnly`allowlistابھیبرقرار،ہرwritercheckedہونےکادعویٰنہیں۔
- Startupregistry+SQLiteکیrace/partialrestoreاورمرکزیSettings/PT-67،Backup/Pricing/PT-68کیWindowsgates۔
- HardwareکےاوپروالےاصلWindows/provider/UI/physicalgates۔ **Hardwarefeatureمکملنہیںکہا۔**
- اسکےبعداسیحصہ5کےStockTake/remainingtoggle/bulk/snapshotroutes؛پھرہی1تا5کےضروریثبوتپورےکرکے6کےلیےنئیمنظوری۔ ❌6/installerشروعنہیں۔

Content baselineآخریpersistedحالتملاتیہے،revisionhistoryنہیں؛A→B→عینAکیintermediatehistoryنہیںپکڑتی۔ اسcheckpointکوCASکیحدودسےبڑھکرsecurity/physical/durableidentitycertificationنہکہیں۔
