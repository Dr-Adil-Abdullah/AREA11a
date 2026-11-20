# ⏳ 5.1 — Settings: مرکزی فارم کی پرانی حالت سے Save کی روک — v0.125.0 Celandine

## ⏳ ترتیب کی نئی واضح ہدایت

مالک نے کہا:پہلے موجودہ حصہ مکمل،پھر اگلا؛نامکمل حصہ5 کی اگلی پیشرفت **5.1،5.2** جیسی ذیلی گنتی سے۔ **حصوں1تا5 کے باقی کام اور ضروری تصدیق مکمل کیے بغیر حصہ6 شروع نہیں کرنا۔** تازہNextسے5.1Settingsشروع؛یہاسکیپہلیمحدودپیشرفتہے،پورےSettingsکیمکملسندنہیں۔ اسبارStockTakeپرکوڈنہیںبدلا۔

موجودہحیثیت:1کیخاصCore/TestscompileخرابیآپکیWindowsرن سےتصدیقشدہ؛2کی22fixtureاصلاحاتکانیاrunباقی؛3کےSave/outcomeاور4کےlogin/permissionguardsکیاصلWindowsجانچباقی؛5کےپہلےProduct/Customer/catalogاورDraftcheckpointsکیرن بھیباقی۔ اب **3143متوقعC#cases،سبتازہsuiteیہاںUNRUN**؛پراناWindows2963total/2941PASS/22FAILاببھیآخریاصلlogہے۔

## ⏳ مسئلہ اور محدود source اصلاح

Settingsکامرکزیفارمsharedcacheسےبہتسیقدریںپڑھتاتھا۔ دوسرےSettingsinstanceیاCounterنےAutoPrintبدل دیا،مگرپرانافارمSaveہواتونئیvalueخاموشیسےپرانیہوسکتیتھی۔ اب:

- `LoadEditForm(actor,keys)` مجاز **Database.Snapshot** میںrequestedknownkeysکےrawpersistedvaluesاورbaselinesپڑھتاہے۔ sharedSettingscacheکوفارمکیbaselineنہیںسمجھتا،cacheکواپنیreadسےoverwriteبھی نہیںکرتا۔ MissingkeyپرموجودہdefaultدکھاتاہےمگربادکیحقیقیINSERTپکڑتاہے۔ Loadfailureپرeditablebaselineنہیں،defaultsکوجھوٹیloadedformنہیںکہتا۔
- `SettingsEditForm` immutable/detachedہے۔ publicgetterسےصرفloaded/non-secretقدریں؛recoveryhashloadممنوع۔ Cloudحقکےبغیرcloudkeysloadنہیںہوتیں؛حقہوتوبھیkeyکیrawvalueنہیں،صرفHasCloudKeyاورinternalhashedbaseline۔ Credentialsنہformserializationنہauditمیںصافtextجاتےہیں۔
- `SaveEditForm` کوloadedformلازم،Sessionsdisabledfixtureمیںبھیnullformrefuse۔ موجودہSettings/Sync/Ownerحقوقاورپرانےvalidationجوںکےتوں۔ `SetMany` کے **BEGIN IMMEDIATE** میںauthorizationکےبعد،تمامUPDATE/auditسےپہلے **تماممتعلقہkeys** کیexactpersistedJSONاورpresenceملتیہے۔ ایکبھیفرقہوتوپوراatomicbatchrefuse؛نہپہلیkeyجزویsaveنہaudit۔ غلطDatabaseinstanceیاایسیkeyجوformنےloadنہیںکیوہبھیrefuse۔
- Cloudconfigurationکواپنیmaskedkeyپرانحصارسمجھکرbaselineمیں **sync.key** بھیملاتاہے،چاہےkeyboxخالیہواورkeyreplaceنہہونیہو۔ اسدورانrotation/removalپرپراناformبغیرreviewنہیںچلتا۔
- کامیابSaveاسیtransactionمیں **صرفلکھیہوئیکیز** کیexactنئیbaselineواپسدیتاہے۔ Untouchedfieldکودوسرابدلچکاہوتواسکوخودکارfresh/rebaseنہیںکرتا۔ Reload/GetRaw/sharedcacheکیafter-commitتبدیلیکوخاموشی سےاپنیformکاحصہنہیںبناتا۔
- Timestamp-onlyچلناconflictنہیں،actualJSONdifferenceہے؛missingاورdefaultvalueکاحقیقیrowالگہیں۔ sessionheartbeat،numberingاورbackup/synctelemetrymainformkeysمیںنہیں،اسلیےہرheartbeatعامSaveکونہیںروکتا۔ JSONکیبےضررwhitespace/representationفرقپرconservativeconflictممکن؛semanticmergeنہیں۔
- مرکزیUIکےordinary/labels/cloud/expiryfieldsاسیpreparedsnapshotسے؛SaveکےوقتLoadکرسےbaselinefreshنہیںہوتی۔ Maintransactionکیsuccessکےفوراًبعدbaselineہینڈآف،بعدکےcallbackسےپہلے۔ LabelRestoreکیsecondconfirmationبھیloadedlabelsسےbound۔ Screen-lockکیالگمرحلہSaveاورstartupکیآخریSQLiteSetاسیbaselineسےchecked؛mainSAVEDاورsecondaryfailureکیپرانیمختلفخبرقائم۔
- StartupمیںknownstaleformWindowsentryچھونےسےپہلےrefuse؛عینfinalsettingswriteمیںدوبارہcheck۔ **WindowsregistryاورSQLiteایکatomicresourceنہیں**؛درمیانکےwriter/COMMIT/restorationکےاصلWindowsrace/faultsابھیOPEN۔ اسpreflightکواندھاعالمگیرatomiclockنہیںکہتے۔ Installer/emergencystartکےخصوصیراستےپرانےبرقرار۔
- Permissionrevocation/cloudrightsبدلنےپرفارمbaselineinvalidate؛نئیSaveسےپہلےexplicitRefresh/reviewلازم۔ عامtimer/unchangedaccessunsavedtextپرنئیbaselineنہیںچڑھاتا۔ Mainformمیںoldordinaryvaluesاورfreshlabel/cloudvaluesکےبےخبرملاپکیجگہcoherentread۔

**اہم حد:** باقیimperative `Set/SetMany` commandsکوخودسےexpectedformلازمنہیںکیا؛وہکسیloadedformکیخودکارتصدیقنہیں۔ CheckedراستہنیاSaveEditFormاورصراحتاًexpectedبھیجنےوالےcallersہیں۔ اسلیےتمامSettingswritersمحفوظہونےکادعوینہیں۔ کوئیschema/migrationیاfinancialcallback/hisabنہیںبدلا؛D-100/D-116،discount/costfloor/SupplierBبرقرار۔ یہlocalcontentbaselineہے،durableidentityنہیں؛A→B→عینAکوتاریخکیطرحگنتانہیں۔

## ✅ صرف محدود مقامی نتائج

- **116staticchecks**؛443C#filesکیgrammar/referenceheuristics،Roslyncompilerنہیں۔
- **29نئیlexicalmutationsرد،exactbytesبحال**؛تمام23source-negative scriptsPASS۔ پراناlabelresetneedleصرفنئےexpectedargumentکےمطابقبدلا،authorizedatomicrouteکیcoverageبرقرار۔
- **37migrations،24Python-ownedSettingsSQLiteplans**:WAL+DELETEدونوںمیںstalebulkrefusal،untouchedkeyکونہrebaseکرنا،own-savehandoff،postcommitwriter،missinginsert،timestamp-only،heartbeat/counterchurn،maskedkeydependency،actualsettings+auditSQLfaultrollback/retry،write-reservationاورreadsnapshot۔ Financialpostsصفر۔ Pythonاپناfingerprint/compare/Txچلاتاہے،نہC#SettingsService/auth/cache/registry/providerCOMMITfault/WPF/LANproof۔

## ⏳ C# کی اصل جانچ باقی

`SettingsEditConcurrencyTests.cs` کے **25نئےpublic-servicefacts**؛پرانےtestsبرقرار۔ الگDatabasewriter/cache،atomicnoauditconflict،exacthandoff/untouchedbaseline،runtime/counterchurn،presence/default،same-timestamp،wrongcontext/unloadedkey/null،recovery/secretredaction/clouddependency،currentpermissions/owner،auditfault+retry،enabledlogin،labelresetاورstartupfake-entrypreflight۔ **UNRUN/uncompiled**؛3143countstaticexpectedہے،runtime-discoveryکیسندنہیں۔

`dotnet build ...Area11.Tests.csproj --configuration Release` کیکوشش:SDKغائب،exit127،`dotnet: command not found`۔ WindowsDesktopRelease/3143suite/COMMIT/providerfailuresاوراصلUIcallbacksباقی۔ نہپراناPASSنہنئےstaticchecksانکابدل۔

## ⏳ PT-67 — صرف الگ Windows ماحول میں

verifiedencryptedbackup **اورواقعیالگDB/photos/config**پہلے؛اصلshopپرتجرباتیposts/faultsنہیں۔

1. تازہ **3143/0fail/0skip**اورDesktopRelease،TRX/log؛حصوں2/3/4اورپہلےحصہ5کیرناببھیباقی۔
2. دوSettingsformsکھولیں۔ Astore/printing/label/cloudvalueSaveکرے؛BoldSave:واضحchanged/refuse،پورابatchبےتبدیلی،کوئیاضافیauditنہیں۔ CounterسےAutoPrintبدلےتوبھیoldSettingsاسےنہپلٹے۔ Refresh/reviewکےبعدجائزSave۔
3. mainformکھلیہو،heartbeat/numberingبدلے؛ordinarySaveجائز۔ MissingrowکیبعدمیںdefaultvalueINSERTپرoldformrefuse؛timestamp-onlyچلنےپرمحضاسوجہسےrefuseنہہو۔
4. Cloudkeyخالیboxسےکبھیظاہر نہ ہو۔ دوسریconnectionkeyrotate/removeکرے؛پراناformcloudsettingsSaveسےپہلےreviewمانگے۔ Rightsrevoke/regrantمیںhiddenvalues/oldbaselineبحالنہہو،explicitRefreshلازم۔
5. MainSAVEDپھرstartup/focusconflict/failure:mainکوواپسنہکہیں؛مناسبSAVED+secondaryfailure۔ StalestartupknownpreflightWindowsentryنہچھوئے؛اسکےبعددوسرےwriter/registryfailure/COMMIT/restoreکےاصلWindowsfaultsکیجانچالگاورابھیباقی۔
6. labelRestoreکےدوpressکےدرمیانlabelبدلےتومنع۔ One-savehandoffکےبعدuntouchedfieldدوسرابدلےتووہخودکارfreshنہہو؛اگلیwriteپکڑے۔ UIcallbacks/selection/reentrancy/busy/pooling/logsتازہWindowsپر۔

## ⏳ 5.1 کے باقی راستے — اگلا کام ابھی Settings ہی

1. **BackupViewModelکاscheduleSave**ابھیimperativeSetMany؛دوکھلےفارمکاباقیcheckedloader/save۔
2. **SettingsکےStaff/Pricingallowances،Appicon/fileاورHardwareکےsettingwriters،workbookSettingspreview/apply**کیloaded-intent/coherencyinventoryاورجہاںضرورتاصلاح۔ حصوں4/9/12کےمتعلقہاصلsecurity/file/hardwaregatesاپنیجگہ۔
3. **Startupregistry+DB**کیcross-resourceconcurrency/failureحداوراصلWindowsثبوت؛یہاںصرفknownstalepreflight/finalDBcompare۔ باقیshort-livedcommandcallersکومحضنئےAPIکیوجہسےcheckedنہکہیں۔
4. MainformکیاصلWPFcallbacks،COMMIT/dispose/cacheaftercommitfaultsاورperformanceکیجانچ۔

5.1کوابھیمکملنہیںکہنا؛اسکےبعدہی5کیباقیStockTake/toggle/bulk/snapshotinventoryکیطرفترتیبسےجائیں۔ **حصوں1تا5کےباقیکاماورضروریتصدیقپہلے؛حصہ6بعدمیںاورنئیمنظوریسے۔** FullPhase4OPEN؛❌حصہ6شروعنہیں،installerنہیںبنا۔
