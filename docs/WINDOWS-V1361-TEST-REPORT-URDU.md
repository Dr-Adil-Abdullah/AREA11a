# ⏳ اصل v0.136.1 Windows test run — دو ناکامیاں،محدود v0.136.2 اصلاح

## ملا ہوا اصل ثبوت

مالک کے Windows/.NET10 log میں **v0.136.1 Betony**،restore up to date،Core اورTests کی Debug DLLs بنیں اورپورے **3397 tests** چلے۔ اصل نتیجہ:

- **3395 پاس،2 ناکام،0 skipped؛کل3397؛5 منٹ59 سیکنڈ۔**
- `StockPostingIntegrityTests.DeliveryStageFaultRestoresAllRowsAndNumberThenRetryReconciles(stage: "audit", after: True)` — `Reconcile` سطر58،caller102؛supplier cash out میں Expected4000،Actual0۔
- `NativeFieldOutcomeTests.AnyPersistedMetadataChangeConflictsBeforePromptAndAtWrite(column: "required")` — setup سطر56؛SQLite Error19،critical/safe-required field کوacknowledgement کے بغیرoptional کرنا CHECK نے روکا۔

✅ پچھلا CS0619 compile blocker اس run میں دوبارہ نہیں آیا؛Core/Tests compile اورمکمل test discovery/execution کا اصل ثبوت مل گیا۔ ⏳یہ green run نہیں،نہDesktop/WPF/Release/installer کی نئی سند۔ v0.136.0 کی30 warnings کی صفائی اس log سے ثابت نہیں؛انہیں اس turn میں تبدیل نہیں کیا۔ ذاتی Windows path یہاں نقل نہیں کیا؛raw log مالک کے پیغام میں ہے۔

## 1 — Native-field fixture میں ناجائز setup

ٹیسٹ پہلے `sale.quantity` کی اصل required/critical/safe_required baseline لیتا تھا،پھرہرmetadata column کوخام SQL سے بدلتا تھا۔ `required=0` اکیلا لگانا schema کے اصل حفاظتی CHECK کے خلاف ہے؛اس لیےٹیسٹ اپنےمقصودstale-baseline assertions تک پہنچ ہی نہیں رہا تھا۔ CHECK درست طورپرکام کررہا تھا،اسےنرم کرناحل نہیں۔

v0.136.2 میںصرف`required` case کیلئےاصل `SetRequired(owner, id, false, confirmedRisk:true, originalBaseline)` سےجائز تبدیلی کروائی ہے۔ کامیابی،optional نتیجہ،اصل stored owner name اورacknowledgement timestamp بھیassert ہوتے ہیں۔ باقی4metadata edits کیSQL برقرار؛اصلپرانیbaseline ہیPrepare/write دونوںکو دی جاتی ہے۔ writer کےبعدaudit baseline لیاجاتاہےتاکہبعدکیrefusals کوئی نیاaudit نہ بنائیں۔ یوںrequired-column conflict کااصل مقصدبرقرارہے،constraint bypass نہیں۔ **21 native cases برقرار؛کوئیcaseحذف/skipنہیں۔**

## 2 — مشترکہ گھڑی کے parallel test اثرات

`Clock.Now()` process-wide `TimeSource.Now` پڑھتاہے۔ `TimeSourceTests` اور`TimeCorrectionTests` میںاصل مشترکہoffset کو+90seconds/+4minutes کیااورReset کیاجاتاہے۔ ان کی `[Collection("Time")]` ایک دوسرےکو serialize کرتی تھی،مگرکوئی`CollectionDefinition` نہ ہونے سےدوسری collections کےposting/drawer tests ساتھ چل سکتےتھے۔ ان تمامTimeSourcemutatorsکیsource تلاش کی گئی؛وہ انہی دوclassesمیںہیں۔

`ShiftService.Page` کی drawer query opening timeسےموجودہClock تکpayments لیتی ہے۔ اگرpayment +90seconds پرstamp ہوجائےاورread سےپہلےparallel test offsetResetکردےتو4000کیpayment databaseمیںموجودہونےکےباوجود`@to` سےآگےہوتی ہےاورquery0دیتی ہے؛اسیطرحshiftکھلنےکےبعدclockپیچھےجائےتوبھیحدمتاثرہوسکتی ہے۔ اس خطرےکوسourceمیںدیکھااوراصلdrawerSQLسےreference reproductionکیا۔ **مالککےاصلthread interleaving/offset values logمیںنہیںتھے؛اسیخاصrunکےscheduleکیقطعیruntimereproductionیہاںنہیںہوئی۔** نئےfull-suite runسےیہتصدیقابھیضروریہےکہاورکوئیوجہنہیں۔

نئی `TimeCollection.cs` میں `[CollectionDefinition("Time", DisableParallelization = true)]` شامل ہے۔ یہصرفclock-changing collection کوتمامدوسریcollectionsکےساتھoverlapسےروکتیہے؛باقیtestsآپس میںparallelرہتےہیں۔ اصلmutatorsکیconstructor/disposeResetبھیبرقرار۔ **StockPostingIntegrityTests byte-for-byte برقرار**:4000کیexpectation،496000متوقعdrawer،36000payable،40stock،rollback/retry/audit/number assertions کمزور نہیں کیے۔ کوئیsleep،retry-until-green،testskipیاwhole-suiteparallelizationoffنہیں۔

یہproductionclockpolicyکیاصلاح نہیں؛اصلappمیںonlinesync/clockجانےاورپرانےbill correctionکےالگruntimeجائزےباقیہیں۔ اسfixturefixسےوہعالمگیرخطرہحل یا ثابتنہیںقرارپاتا۔

## جانچ اور محفوظ حدود

✅ مقامی **129 static checks**؛نئےnegative script کی **13 lexical mutations** rejected اورexactbytesrestored؛نئےPython probe کے **37 migrations/8 WAL+DELETE reference cases** میںناجائزrequired-updateکاناکامہونا،اصلextractedUPDATEسےacknowledgedchange،اصلdrawerSQLمیںclock-jump0اورstableclock4000دکھائے۔ یہPython-ownedvalues/transactionsہیں،نہC#service،نہxUnit schedulerکاexecution۔

⏳ اصل .NET/Windows build/test rerunابھیباقیہے؛یہاںSDKموجودنہیں۔ متوقعsuite **3397** ہیہے؛نئیcollectiondefinitionخودtestcaseنہیں۔ پہلے3395PASSصرفv0.136.1کےہیں،patchedrunکےنہیں۔ مقامیbuildکوششexit127 (`dotnet: command not found`) پررکی؛compilerچلاہی نہیں۔

✅ تمام36source-negative scriptsپاس،ان میں13نئیمحدودmutationsشامل؛ہرscriptنےاصلbytesبحالکیے۔ پچھلےSQLprobesبھیپاس:stockposting44BEFORE/AFTERfault+rollback/retryplans،native30WAL/DELETEplans،parent15plans/30auditrollback+retryاورbrandtwo-phasecase۔ rules-matrixparity،`git diff --check`اورالگpreservationمقابلہپاس؛یہسبC#/provider/runtimeکیسندنہیں۔

Productionbusinesslogic،financialSQL،TimeSource/Clock/ShiftService،NativeFieldPolicyServiceاور37migrationsنہیںبدلے۔ لازمیversion/colour/icon/scriptbannersاورحالتکیdocsساتھتازہہوئیں۔ مکملrepoauditیاتمامممکنہbugsکیعدم موجودگی کا دعویٰ نہیں۔

## پہلے سے بدلی نظرآنے والی فائلوں کی حفاظت

شروع میںGitHEADپرانےbase805626eپرتھا،اس لیےبہتسیpublishedفائلیںmodified/untrackedنظرآئیں۔ اصلremote87f55c7کوفیچ کرکےاسکےہرtrackedblobکاworkspaceفائلوںسےhashمقابلہکیا: **0فرق**۔ اس کےبعدصرفاسیcurrentbranchکےHEAD/indexکوموجودہremoteتکmixedresetسےہم آہنگکیا؛hardreset،فائلحذف،checkout/branchswitchنہیںکیا۔ اس کےفوراًبعدworkingtreeصافتھی۔ پہلےstatus/patchکاعارضیbackupبھیلیاتھا۔ لہٰذاکسیغیرمتعلقاصلeditکوواپس نہیںکیااوراسfixکےcommitمیںپرانےہزاروںlinesدوبارہشاملنہیںکیے۔

## اگلاقدم

1. مالکپہلےپراناlogالگمحفوظکریں؛`START-AREA11.bat` سے **v0.136.2 Campion** کیbuildلیں۔
2. `tools/RUN-TESTS.cmd` سےپورا3397-case runکریں؛خاصطورپرانbothfailuresکیختمہونےکیfull-suiteتصدیقچاہیے۔ صرفfilteredgreenrunclock-raceکےلیےکافینہیں۔ failureہوتونیاپورابلاکبھیجیں؛متوقع4000کوصفرنہیںکریںگے۔
3. تجرباتیfault/postingصرفthrow-awaytestDB/photos/configیاپہلےمنظورشدہالگcopyمیں؛حقیقیshopمیںنہیں۔

⏳ ترتیب:پہلےیہruntimegate،پھرمنظورشدہ5.1میںCustomAdd/Move،باقیSettings/registryاورStockTake؛1تا5کےباقی gatesکھلےہیں۔ ❌ حصہ6/installerشروعنہیں۔ ہرنئےمسئلےکیحقیقیحدبتائیجائےگی؛اسturnسےFull5.1مکملنہیںکہاجائےگا۔
