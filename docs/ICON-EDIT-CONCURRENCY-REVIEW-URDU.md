# ⏳ 5.1 جاری — App icon فائل اور Settings اندراج — v0.128.0 Catkin

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

> ⏳ تازہv0.129.0میںاسی5.1کےWorkbookSettingsکیboundedاصلاح: [PT-71/باقی5.1](WORKBOOK-SETTINGS-CONCURRENCY-REVIEW-URDU.md)؛3224suiteUNRUN۔ نیچےپچھلےcheckpointکاثبوتتاریخی؛workbookباقیکےحوالےنئےجائزےکیحدتکبدلے۔5.1جاری،1تا5مکمل+تصدیقسےپہلے6نہیں۔


## ⏳ اجازت اور حد

تازہ `next` پر **اسی5.1 Settings** کا صرف App icon Choose/Reset راستہ لیا۔ [پچھلا Hardware checkpoint/PT-69](HARDWARE-EDIT-CONCURRENCY-REVIEW-URDU.md) اوراس سےپہلےSettings/Backup/Pricingکیحدودبرقرار۔ **5.1 ابھی مکمل نہیں؛حصوں1 تا5 کے باقی کام اور ضروری تصدیق کے بغیر6 شروع نہیں کرنا۔** Installer نہیں بنایا۔

## ⏳ پہلے کیا خطرہ تھا؟

1. Icon dialog کے بعد `Settings.Set` expected loaded state کے بغیر pointer بدلتا تھا؛پہلے سے کھلا فارم دوسرے شخص کا نیا icon بدل یا Reset کرسکتا تھا۔
2. Save کا صرف `Ok=false` دیکھ کر نئی فائل حذف ہوتی تھی۔ COMMIT کا جواب نامعلوم،یاDBcommitکےبعدcachecleanupناکامہوتوDBشایداسیفائلکیطرفاشارہکررہی ہو؛تبفائلحذفکرناغلطہے۔
3. `FileInfo.Length` کے بعد `ReadAllBytes` بڑھتی فائل کا اصل read محدود نہیں رکھتا تھا۔ Reset میںApplyکاthrowمحفوظکامکوغلطناکامیبتاسکتا تھا۔

## ⏳ Source اصلاح

### Loaded icon baseline اور موجودہ Owner

- MainSettings کے coherent `FormKeys` میں `appearance.iconFileName` شامل ہے۔ Choose اورReset **dialog سے پہلے والا form** ساتھ رکھتے ہیں؛dialog بند ہونے پر نیا snapshot لےکرپرانےinputکوصحیحقرارنہیںدیتے۔
- نئی Core `IconAssetService` دونوںoperationsپر `Settings.CheckIconEdit` preflight کرتی ہے:موجودہOwner/session،Settingsحق،nonnull/context/keybaseline۔ یہ preflight کوئیshopdataنہیںلکھتی۔ Knownstaleیاdeniedپرنئیdestinationنہیںبنتی۔
- آخری `Settings.Set` پھر اپنی **write transaction** میںcurrentauthاورexpectediconkeyملاتاہے۔ PreflightکےبعداورfilecopyکےدورانکوئیpointerبدلےتوبھیپراناSaverefuseہوگا۔ Auth/businessکےپرانےprotectedSettingsقواعدبرقرار۔
- Success کا **عین اسی transaction والا** `SettingsEdit` واپسMainformمیںجاتاہے۔ صرفiconkeyکیbaselineبدلتیہے؛اگرstore.name/labelsیاکوئیuntouchedfieldاسدورانبدلاہوتواگلاoldSaveاسےخاموشیسےنہیںمٹاسکتا۔ No post-save Load/rebase۔

### فائل کی ملکیت،حد اور درست cleanup

- WPF منتخبsourceکواباصلreadکےدوران **زیادہ سے زیادہ2MB+1byte** تکپڑھتاہے؛حدسےبڑیفائلrefuse۔ `.ico` extension اورWPFIconBitmapDecoder/frameکیجانچdestination/DBwriteسےپہلے۔ Applyکیreadبھیboundedہے۔یہmaliciouscodecکیجامعsecuritycertificationنہیں۔
- Core bytes کیحداورICOheader/countبھیچیککرتی،اورcallerکیbytearrayکواپنیcopyمیںلیتیہے۔ Core کاheadercheckاکیلاباقیICOcodecکیجانچنہیں؛DesktopکاDecodeضروریہے۔
- نیاgeneratedbasename،photosfolder،`FileMode.CreateNew`،FileShare.None۔ Existingfileکونہoverwriteنہdelete؛ownershipصرفsuccessfulopenکےبعد،مگر**پہلیWrite/Flush/Disposeسےپہلے**درجہوتیہے۔ عامFileStreamکافیصلہکنflush`flushToDisk:true`ہے؛یہdirectoryentry/powerlossکیend-to-endضمانت نہیں۔
- DBwriterشروعہونےسےپہلےcopyfaultپراپنیبنائیcopyکاصرفbest-effortcleanup؛پہلےسےموجودcollisionیاoldiconکونہیںچھوتے۔
- `Settings.Set` کاواضح **NoCommitConfirmed** ہو تواسی کوششکیownedcopyصافہوسکتیہے۔ Unknown/throw/lostresponse/COMMITattemptپرcopy **برقراراورlog**؛اندھاحذفنہیں۔ CleanupخودناکامہوتوبھیDBکوکامیابنہیںکہتے؛residualfileکاlogرہتاہے۔
- Reset صرفcheckedpointerخالی کرتاہے؛پہلےاوربدلےہوئےiconsphotos/backupsمیںرہتےہیں۔ **File+SQLiteکوایکatomictransactionیاzero-orphanfeatureنہیںکہا۔** Processcrashیاunknownپرresidueرہسکتیہے؛کوئیخودکارgarbagecollectionنہیںبنائی۔
- `IconFileStore` صرفisolatedfaulttestsکاتنگStream/fileboundaryہے؛عامDesktopproductionہمیشہexclusiveFileStreamاستعمالکرتاہے،اسseamکاکوئیconfig/UIبائیپاسنہیں۔

### UI acknowledgement

- Choose/Reset/mainSave/Refreshکےدرمیانpage-localbusyguard؛modalکےدورانخوددوبارہiconwriteیاformrebaseنہیں۔ یہdurableidentity/cross-PCdedupنہیں۔
- DBsuccessکےبعدApplyناکامہوتوresultاببھی **Ok+exacttoken**،ساتھ **SAVED** warning؛دوبارہwriteکیغلطہدایتنہیں۔ MainVMبھیbindings/statusسےپہلےacknowledgeکرتاہے؛بعدکےthrowمیںSAVED،unknownمیںforminvalidateاورrefresh/verify۔
- پہلےسےموجودWindowsshortcutsاورexecutableکاآئیکناببھیانکےاپنےcompilediconsہیں؛صرفwindowresourceبدلتاہے۔ Missing/badcustomfileپراناdefaultfallbackرکھتاہے۔ Applyاپنیfallbackسےکامیابیپرآئےتواسےcustomcodec/physicalPASSنہسمجھیں۔

## ✅ صرف محدود مقامی جانچ

- **119 static checks**؛C#grammar/referenceheuristics،Roslyn/compilerنہیں۔
- **36 نئی lexical mutations** رد،اصلbytesبحال؛تمام **26 source-negative scripts** دوبارہپاس۔
- **37 migrations،24 Python-owned icon file/SQLite cases**:WALاورDELETEمیںcheckedpointer،preflight/finalconflict،actualSettingsUPSERT+AuditINSERTکیrollback،deferredFKCOMMITfailure،واقعیcommitکےبعدsyntheticlostack،stale/validResetاورwriterreservation؛الگownedcopywrite/flush/dispose/collision،unknownretentionاورsizeboundreference۔ Financialpostsصفر۔
- **Python اپنےfingerprint/compare/transactions/authassumptions/outcomeclassificationاورfilecontrolچلاتاہے**؛یہC#IconAssetService/SettingsService/TransactionOutcome،cache、WPFcodec،ACL、SMB、COMMITprovider、powercutیاcross-resourceatomicityکاثبوتنہیں۔
- پچھلےHardware38،schedule/Pricing26،Settings24،Draft40اورEdit18SQLplansدوبارہپاس؛rulesmatrixاورdiffcheckصاف۔ Financialservices/37migrationsاورپرانےC#testsنہیںبدلے۔

## ⏳ 23 نئے C# cases — ابھی UNRUN

`IconAssetConcurrencyTests.cs` میں **17Facts+6Theoryrows=23cases**:دوloadedforms،stalereset،exacthandoff/reset/oldfilepreservation،missing/wrongcontext/wrongkeybaseline،Ownerdenial/deactivation/enabledproof،bounds/header،write/flush/dispose/collision、auditrollback/retry、concurrentcopychange、cleanupfailure、untouchedstalefieldاورdeferredCOMMITfailureپرretention۔

سباپنیtemporaryDB/photosمیں؛کوئیrealshop،WPF یااصلiconcodecنہیں۔ Testsکےheaderbytesجان بوجھکرسyntheticہیں؛انکاپاسہونابھیWindowsDecodeکاثبوتنہیںہوگا۔ DeferredconstraintcaseactualC#providerکےCOMMITattemptclassificationکوجانچےگا،مگریہاں **نہیںچلا**۔

متوقعsuite **3211 UNRUN**۔ Releasebuildکی کوشش: `.NET SDK`نہیں،`dotnet: command not found`۔ اصلآخریWindowsنتیجہاببھیv0.120.1کے2963total/2941PASS/22FAIL؛حصوں2تا5کےبعدکاتازہrunباقی۔

## ⏳ PT-70 — صرف الگ Windows ماحول میں

Verifiedencryptedbackup **اورواقعیالگDB/photos/config** پہلے؛اصلshopمیںتجرباتیposts/faultsنہیں۔

1. **3211/0fail/0skip**،Core/Tests/DesktopReleaseاورTRX/log۔ دوSettingsformsکھولیں؛AiconChooseکرے،BپراناChooseیاReset:conflict،Aکاpointer/file/auditبرقرار،Bکاجزویpointer/auditنہیں۔ FreshreviewکےبعدجائزChoose/ResetاوراگلیSaveمیںself-conflictنہیں۔
2. Filedialogکھلاہو،دوسرےconnectionسےiconبدلےیاOwner/session/Settingsحقبدلے؛dialogکےبعدپرانافارمانےسےپہلیknownstalepreflightیاfinalCASrefuse۔ Filecopyکےدرمیانchangeکےبعدfinalrefusalاورصرفownedcopycleanup۔
3. StoreName/labelsمیںدوسرےwriterکیuntouchedchange،پھرiconChoose:iconکاجائزSaveہوسکتاہےمگرmainformکااگلاoldSaveدوسرےfieldکوoverwriteنہکرے۔ ModalکےدورانRefresh/mainSave/reentry،cancelپرکوئیwriteنہیں۔
4. اصلWindowsfilefaults:collision、readlimit、truncated/invalidICO、write/flush/close、readonly/diskfull、cleanupfail۔ Source/previousiconکبھیdelete/overwriteنہہو؛actualACL/codec/nativeimageقواعدجانچیں۔
5. Audit/COMMIT/rollback/disposefaultsاورlostack؛Unknownمیںنئیcopyبرقرار،کوئیautomaticretryنہیں۔ واضحrollbackپرصرفاسیcopyکاصفایا۔ ActualcommitکےبعدApply/bindingthrowپر **SAVED** اورtoken/refreshصحیح؛defaultfallbackکوصافبیانکریں۔
6. Windowswindowresourceبدلے،existingshortcut/exeاپناiconرکھیں؛restart、missingfile、restoreاورphotosbackupمیںcustomicon۔ File+DBکےدرمیانprocesskill/powerloss،SMB/LANاورrestoreکےبعدdanglingpointer/residueکیعملیجانچباقی؛durablejournal/dedupنہیںبنایا۔

## ⏳ 5.1 میں باقی

- WorkbookSettingspreview/applyکیloadedbaselineاورatomicapply؛باقیStaffpermission/reset/toggle、native/customاورimperativewritersکیinventory۔
- Startupregistry+SQLiteکیrace/partialrestore؛PT-67/68/69/70کےاصلWindows/provider/UI/physicalgates۔ Iconکیsourceاصلاحکوپورے5.1کیcompletionنہیںکہا۔
- اسکےبعداسیحصہ5میںStockTake/toggle/bulk/snapshotroutes؛1تا5کےباقیکاماورضروریتصدیقمکملکرکےہی6کےلیےنئیمنظوری۔ ❌6/installerشروعنہیں۔

Content-baselineآخریpersistedvalueملاتیہے؛A→B→عینAکیintermediatehistoryنہیںپکڑتی۔ Backup/registry/fileکیاصلcross-resourceatomicity،verifiedreopenاورdurablerequestidentityاببھیاسcheckpointسےباہرجداgatesہیں۔
