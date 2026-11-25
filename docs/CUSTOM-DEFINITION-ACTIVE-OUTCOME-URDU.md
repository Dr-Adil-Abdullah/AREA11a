# ⏳ حصہ5.1 — CustomField On/Off کا محفوظ نتیجہ — v0.136.0 Comfrey

**صرف موجودہ definition کی visibility assignment اوراس کا Desktop راستہ۔** نیا سوال Add،Move/order،باقی Settings/startup/StockTake اس میں مکمل نہیں ہوئے؛نہ نئی business/مالی policy،نہ durable retry identity،نہ حصہ6/installer۔

## 1.1 — اصل Windows شہادت ابھی کیا ہے؟

مالک کا تازہ actual v0.134.0 Windows build **FAIL:1 CS0411،3 warnings،36.79s**؛tests/app نہیں چلے۔ v0.134.1 کی Product.Update callback source fix برقرار مگراصل rerun نہیں ملا۔ v0.135.0 Update اوریہ v0.136.0 On/Off source checkpoints ہیں،compile certification نہیں۔ [اصل failure/حدود](WINDOWS-V1340-BUILD-FAILURE-URDU.md)۔ آخری actual test run تاریخی v0.120.1:2963total/2941PASS/22FAIL؛اسے نئےcodeکاPASSنہکہیں۔

## 1.2 — Core میں محدود تبدیلی

- `SetActive` کاپرانا signature اورsessions-disabled legacy null-token behavior برقرار؛نیا `SetActiveChecked` null baseline رد کرتا ہے۔ Owner-only/current active/session،stored audit name اورentity-wide CAS عینwriteTxمیںہیں۔
- **No-op سے پہلے بھی Owner اوراصل entity token compare لازم۔** کسی دوسرےwriterنےپہلےہیdesired active value کر دی ہو،تب بھیپراناtokenرکھنےوالاکامیابno-opنہیںبنتا۔
- `TransactionOutcome` اورexplicit `Tx<ServiceResult>`؛preflight/واقعیpre-COMMITrollbackپرہی `NoCommitConfirmed`،COMMIT/rollbackunknownمیںunconfirmed۔ ناکامیپرreceipt/tokenنہیں۔
- اصل `UPDATE custom_fields SET active...` اورon/off audit SQL/arguments/labels/تفصیل برقرار۔ کوئیanswer،stablekey،label/type/options/help/required/orderنہیںبدلتا؛no-opنیاauditنہیںبناتا۔ Missingtargetابthrowکےذریعےconfirmedrollbackrefusalہے۔
- Changed اورno-opدونوںاسیtransactionسےexisting `CaptureDefinitionEdit` اورdefensive `CustomDefinitionEditForm` لیتےہیں؛active+hidden پوریentity،ترتیب اورہرrowکااسیentitybaselineکاپناtoken۔ Helper/modelکواسcheckpointمیںنہیںبدلا۔

## 1.3 — Desktop میں کیا ہوا؟

- `ToggleActive` اصلselectedID/entity/token اورdisplayed `!Active` کوserviceسےپہلےpinکرتاہے؛actionپہلےRefreshAccessیاSaveکےبعدLoad/FinishWriteسےbaselineنہیںبدلتا۔ Corecurrentaccess/CASحتمیفیصلہکرتاہے۔
- **Update اورOn/Off وہی ایک persistent local guard استعمال کرتےہیں۔** کسیایککیunknown یاacknowledged-callbackfailureدوسرےcommand،Add/Move،Refresh/tab/New/Cancel/regrantسےguardreleaseنہیںہوتی۔ یہsharedboundaryہے،Add/Moveکےاپنےoutcomesکیfixنہیں۔
- Ackپہلے؛پھربیکنگselectedID،label/options/help/required/typeاورeditingstateconsume؛پھرexactentityreceiptکاID/entity/desiredActive/tokencheck،rows/notifications/status؛آخرمیںretainedfull-row/token/blankeditorverdictکےبعدnormalreset۔ Successپرselectedfieldخالی؛اگلےactionکےلیےنئیselection۔
- Unknown یاSAVED-callbackfailureپرموجودہ `InvalidateUpdate` backingstate/statusکوصاف/محفوظکرتاہےاورguardblockedرہتاہے۔ سابقUpdateimplementation،cleanuphelperاورinput/tab/timerguardsنہیںبدلے؛صرفوہیguardنئےOn/Offمیںبھیشاملہے۔
- On/Off unsavedlabel/typeکیترمیممحفوظنہیںکرتا؛پہلےبھییہصرفvisibilityactionتھااورreloadاسinputکوبدلتا تھا۔ ابکامیابیپرواضحblank/no-selectededitorہے؛نہترمیمخودsaveہوتیہےنہپرانیselectionکاالٹاtoggleدوبارہخودچلتاہے۔

## ✅ محدود مقامی نتائج

-128 repository static checks؛35negative scripts،انمیں37نئیvisibilitylexicalmutationsrejectاورexactbytesrestore۔ یہcompilerنہیں۔
-37migrations/32نئےPython-ownedextracted-SQLreferenceplans،WAL+DELETE:دونوںdirections/no-ops،5peerchanges،desired-no-op-stalerefusal،UPDATE/auditrollback+retry،exactreceipt/laterwriter、unrelatedentity、revokedowner-no-opاورdeferredCOMMIT۔ تمامplansمیںoldanswers/keys/دیگرdefinitionvaluesبرقرار۔ Pythonخودactor/CAS/Tx/outcomeassumptionsچلاتاہے؛C#implementation/provider/WPFproofنہیں۔
-Update/Add/Move/customanswers/Coreکےباقیserviceمتن،اصلvisibilityUPDATE/audit،sharedmodel/guard،Database/session/CASengine،financialservices،تمامپرانےC#testsاور37migrationsبرقرار۔ GuideMD/HTMLاورdiff/invariantsالگجانچے۔

## ⏳ C# اورbuild

24نئےisolatedC#cases؛کل3397suite **UNRUN**۔ دونوںdirections،no-op،peerchanges،staletargetalreadydesired،null/wrongtarget/wrongDBbaseline،currentOwner/session/storedname،oldanswers/keys،statementrollback/retry،deferredCOMMITunknown،defensive/laterreceipt、legacyAPI/deletedtargetاور3syntheticsharedguardcases۔ SyntheticguardcasesاصلVM/timer/typingنہیںچلاتے۔ یہاںSDKنہیں؛actualReleasebuildکی کوشش exit127؛اصلWindowsrerunاورتینwarningsابھیباقی۔

## ⏳ PT-78 — اصل الگ Windows پر

Encryptedbackupکی تصدیق اورواقعیالگDB/photos/configپہلے؛اصلshopپرexperimentalwrites/faultsنہیں۔

1. v0.136.0Comfreylabel،Core/Tests+DesktopReleasebuild؛CS0411fixکاactualنتیجہ،تینwarningsالگ؛3397expected/0fail/0skipاوراصلlogs۔
2. Fieldمنتخبکرکےدوسرےconnectionسےاسیrowیاpeerکیlabel/order/active/required/helpبدلیں۔ پراناOn/Offconflictدے؛دوسراwriterپہلےdesiredActiveکرچکاہوتببھیstale-no-opکامیابنہہو۔ Unrelatedentityکےwriterسےغلطconflictنہیں۔
3. NormalOff/On/no-opکےreceipt،exactactive+hiddenrows/rowtokens،no-inventedaudit،oldanswers/stablekeys/orderunchanged۔ Successپرblank/no-selectededitor؛نئیselectionسےہیالگجائزaction۔ Type/labelکیunsavedترمیمvisibilityکےساتھخودsaveنہہو۔
4. Doubleclick/Enter،selection/tab/typing/timerreentry؛ackکےبعدcollection/status/notificationfault۔ SAVEDواضح،backinginputconsume،guardblocked؛Refresh/New/Cancel/regrantیاUpdateبٹنreleaseنہکریں۔ الٹیسمتUpdateunknownکےبعدOn/Offبھیblockedہو۔
5. UPDATE/auditfaultپرknownrollbackاورجائزretry؛actualproviderCOMMIT/rollback/dispose/lostresponseپرunknown،کوئیreceiptنہیں۔ Pythondeferredconstraintاصلproviderresponseinjectionنہیں۔
6. CurrentOwner/sessiondenialاورno-op،missing/wrongtokens،legacycompatibility،verifiedreopenکیاصلVMlifetimeجانچیں؛نیاinstance/restartخودverificationیادurableidentityنہیں۔ PT-77Updateبھیregressionمیںبرقرار۔

## ⏳ باقی5.1

Custom AddاورMove/orderکےoutcomes/guards،remainingimperativeSettingsinventory،Windowsregistry+SQLitepartialrestore،StockTake/update/toggle/bulk/snapshotاورPT-63–78کیضروریactualجانچ۔ [جامعحالت](COMPLETE-STATUS-AND-DECISIONS-URDU.md)؛[منصوبہ](REMAINING-14-PORTIONS-URDU.md)۔ پوراCustommanagerیا5.1مکملنہیں؛1تا5کےباقیکام/تصدیقسےپہلے6نہیں؛❌installerنہیںبنا۔ Publication/actualremoteverificationآخریمختصرگفتگومیں۔
