# PT-41 — گلہ کے current reads اور محفوظ Open/Close/Handover

**v0.99.0 Curlew · `#526F85` · Source checkpoint؛Windows جانچ UNRUN**

مثال: گلہ میں Rs1000 متوقع ہوں،گنتی Rs999 ہو تو ایک روپیہ کم ہونے کی وجہ چاہیے؛اگلے شخص کا float **999** ہوگا،1000 نہیں۔ دوسری مثال: DB سے گلہ پڑھا نہ جا سکے تو پروگرام اسے “بند” یا “Rs0 تصدیق شدہ” نہ کہے۔ اس حصے میں یہی فرق،موجودہ اجازت اور action کے تمام writes کو اکٹھا رکھنے کی جانچ ہے۔

[تفصیلی trace اور حدود](POS-PHASE4-SHIFT-CURRENT-REVIEW.md) · [مکمل اردو کتاب](USER-GUIDE-URDU.md)

## پہلے حفاظت

- اصل دکان میں فرضی بل/گنتی/وصولی/handover **نہ کریں**۔ Verified backup اور واقعی الگ test DB **اور photos/config paths** ضروری؛صرف دوسری exe folder isolation نہیں۔ غیر یقینی ہو تو رک جائیں۔
- Backup password A،PIN،password،keys اور اصل DB عام chat میں نہ دیں۔ Shop activity چل رہی ہو تو restore نہ کریں۔
- `tools\RUN-TESTS.cmd` کا اس build میں متوقع total **1852** ہے۔ **35 نئے facts لکھے ہیں مگر C# build/tests یہاں نہیں چلے۔** Core/Tests کے ساتھ الگ Desktop/WPF build اور اصل Windows screenshots چاہییں۔

## صرف الگ test copy میں مشق

| # | عمل | متوقع نتیجہ — ابھی خود جانچنا ہے |
|---|---|---|
| 1 | Cash grant والا staff؛Cost/Profit دونوںOFF/mainOFF | موجودہ گلہ،history/discrepancy اور Open/Close جائز؛صرفمالیgrantsسےCashنہیںملتا۔ عامCashamountsموجودہمعنیسےبرقرار |
| 2 | POS-only،CashOFF | Counter کوصرفopen/closedstatus؛گلہکےtotals/Pageنہیں۔ Drawerstatefailتوbillblock،“stateunknown/refresh”؛غلط“بندہے”نہیں |
| 3 | گلہ کھلا،Cashrevoke یاaccountinactive/rolechange | اگلےcommand/nominal3scheckپرموجودہnumbers،previousclosing/history/discrepancies/personsummary/recipient/countinputs/lastslipصاف؛Open/Close/Handover/reprint/shareمنع |
| 4 | اجازتبحال اورcurrentactive/same-roleuser،پھرفreshread | گلہبحال؛مصدقہclosedصفحمیںCurrentnullاورحقیقیpreviouscount۔ Readfailureکوclosedنہسمجھیں |
| 5 | CashierاورManagerکاByPersoncompare | CashierordinaryCashpage/historyکیپرانیاجازتبرقرار؛**ByPersonaggregate**صرفcurrentCash+Owner/Manager۔ CachedManagerroleسےdemotionbypassنہیں |
| 6 | Recipientنامبدلا،Cashrevoke،inactive،rolechange | Handoverداخلtransactionدوبارہrecipientcheck؛منعrecipientکوفلوٹنہملے۔ جائزہوتوstoredcurrentname،پراناسپلائیکردہنامنہیں |
| 7 | Rs1000float،Rs999count؛reasonخالیپھرreasonکےساتھhandover | خالیreasonreject؛reasonسےoldshift-1،newfloat999،دونوںlinks/auditsاورایکdiscrepancy۔ Gapکوexpected1000بناکرنہچھپائیں |
| 8 | OwnshiftکسیpeerسےClose/Handover؛پھرcurrentOwner/Managerسے | Peerمنع؛currentCashseniorیااصلopenerکیexistingruleبرقرار |
| 9 | Developer-controlled isolatedfault: Openکےaudit/discrepancyپرخرابی | Shiftinsertاورمتعلقہwritesrollback؛آدھاکھلاگلہنہیں۔ SourceSQLprobeC#transactionruntimeثبوتنہیں |
| 10 | اسیطرحCloseاورHandoverکےaudit/discrepancyیاpostwritereadپرfault | Closeپراناopenstateبحال؛Handoverنہصرفoldcloseنہصرفnewopen،تمامگروپrollback۔ RawDBexceptionUIمیںنہیں |
| 11 | Actioncommitہوجائے،فوراًPage/printingfail | واضح**SAVED—دوبارہactionنہکریں،refreshکرکےدیکھیں**؛پرنٹکیخرابیسےsavedmoneyundo/retryنہیں۔ Failedpage/accessپرhandoverautoprintنہیں |
| 12 | کامیابhandoverکےبعدPrint/WhatsApp،پھرCashrevoke | OutputسےپہلےfreshCashcheck؛denialپرcachedslipصاف۔ جائزreprint**savedin-memorysnapshot**ہے،DBسےنیاimmutablehistoryreadنہیں؛پہلےسےPDF/paperواپسنہیںلیاجاتے |
| 13 | Sessionexpiryپرdrawerknownopen،knownclosedاورunknown | Open/unknownپر“گلہcheck/countکریں”نوٹ،بندش/فرضیگنتینہیں؛knownclosedپرنیاpendingnoteنہیں۔ Statusreadخرابہوتوبھیsign-outجاری۔ Noteکیbest-effortstoragefailureکیglobalجانچباقی |
| 14 | الگDBconnectionsسےایکساتھOpen،repeatedClose،same-secondhandoverpayment | صرفایکvalidopen؛دوبارہCloseنیاauditنہبنائے؛handoverboundaryپرpaymentدوبارہcountنہیں۔ C#parallelcaseلکھاUNRUN؛ایکsharedDatabaseکیعامthread-safetyثابتنہیں |
| 15 | Pagefault،grant-queryfault،viewکھول/بند،timer،عامNoteادھورا | Unknowncashhidden،Managerdefaultسےfailedgrantreadallowنہیں؛Unloadtimerstop،allowedtickپربلاوجہfullmoneyreload/Noteeraseنہیں؛realWPF/headers/physical58/80mmجانچضروری |

## اس ماحول کا نتیجہ

- **88 static checks PASS**؛C# grammar408files،structural0issues،compilerنہیں۔
- **16 synthetic SQLite probes PASS**؛نئےprobeمیں36migrations،13اصلShiftSQLblocks،cash/goods/boundary،6SQLfaultrollbackgroups/read-only/FK۔ Pythonorchestration،C#RequireCash/Database.Tx/WPF/concurrencyنہیں۔
- **19 source-negative mutations** رد/اصلbytesبحال؛executableC#mutationtestingنہیں۔
- **35 نئےfacts / کل1852C#casesUNRUN**؛parallel/faulttestsلکھےہوناruntimePASSنہیں۔
- تمام13ShiftaccountingSQLblocksاورFillDrawer/helperstail،دوسرے115Core service/data files،Blackbox/BookCheck/backup/migrationsunchanged۔

## اب کیا باقی ہے؟

1. **اگلیsourceترجیح:** باقیtransactionalservicesمیںDatabase/permissionکےconnectionبرتاؤکیجانچ؛اسیگلہpathکومحفوظکرنےسےپورےپروگرامکاtransactionpatternمنظورنہیںہوا۔
2. باقیSettings/native/custom/photo/secretfields،shell/sessionاورrecordauthorization۔
3. Cross-servicefaults،rollback/retry،concurrency،power-loss/duplicate-actionنتائج۔
4. Files/cloudاورbackup/restoreکیاصلintegration،نئےPCپرpasswordA/DPAPI،interruptedrestore/rollbackاورالگDB/photos۔
5. Windowsپر1852cases،Desktop/WPFbuild،keyboard/timers،اصلprinter/scannerاورscreenshots؛پھرنتائجکیfixes۔
6. اسکےبعدauthorizedواحدsendabledouble-clicktestinstaller،shortcuts/startup/upgrade/uninstall/new-PCverification۔ **ابھیinstallerنہیںبنا۔**

تقریباً **3–6 مزیدمربوطsource/resultچکر** مشروطاندازہہے؛runtime/connectionنتائجسےبڑھسکتا،deadlineیاceilingنہیں۔ Version0.99مطلبproductionreadyنہیں۔ Blackbox/BookCheckکیاضافیریڈیزائنD-115سےباہرنہیںکی۔

نتیجہبھیجیں: قدم نمبر،version،مکملWindowslogیااصلscreenshot،متوقعاوراصلرویہ۔
