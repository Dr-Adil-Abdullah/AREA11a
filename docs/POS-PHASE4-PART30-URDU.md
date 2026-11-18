# Phase4 — حصہ30: Purchase اور Supplier Return؛ PT-50

**v0.108.0 Headland · مستقل16/18/23/24/25 · source checkpoint، Windows منظوری نہیں۔**

[مکمل1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md) · [فنی trace/حدود](POS-PHASE4-STOCK-POSTING-INTEGRITY-REVIEW.md) · [اردو استعمال نامہ](USER-GUIDE-URDU.md)

## 1۔ روزمرہ مثال اور اس حصے کی حد

دو batches میں20+20گولیاں،فی گولی10روپے خریدیں: bill400،اسی وقت40اداکیے،supplierکو360دینا ہے۔ درمیان میںخرابی آئےتو ادھوراbatch،payment یا جلاہواbillnumber نہبچے۔ کامیابsaveکےبعدprinterخراب ہوتو **SAVED؛دوبارہیہdelivery وصول/post نہکریں،history دیکھ کرصرفinvoice دوبارہنکالیں۔**

اسیdelivery سے5+5گولیاں واپس:100روپےcredit note ہوتوpayable260؛supplierسے100cash ملےتوpayable360برقراراورdrawerمیں100واپس۔ Cash return کوpaymentsکیاضافیrow نہیںبناتے۔ اسsettledpolicy،samplestock،SupplierBاورCost+mainکیشرائط کونہیںبدلا۔

## 2۔ کیا source مکمل ہوا؟

1. **16/25 Purchase:** اسیviewmodelمیںجاریSaveکیreentrantcallرکتیہے؛finallylatchکھولتاہے۔ Coreکےacknowledgedsuccessکےبعدpostedlines،paidamount اورopen draftID printسےپہلےconsumeہوتےہیں۔ غیرمحفوظunsavedpurchaseکیpermissionmasking/preservationبرقرار۔
2. **16/7 Purchase:** lookupfailureمصدقہempty/successنہیں؛freshsupplierselection،explicitloadverdict،customeditor/accessrecheck۔ Receipt/settings/printerکیexceptionیاfailedresultپر **SAVED / Do not post again**،نہکہدوبارہمالوصولکریں۔
3. **18/25 Supplier Return:** الگlocalreentrylatch/finally؛successکےبعدtray/note/cashchoiceصاف؛failedsupplierreloadیاexpiredlistreadپرسuccessoverwriteنہیں۔ Callbackکےبعدfreshaccess؛postcommitexceptionپرstalesensitivepageصافاور **SAVED / Do not return these goods again**۔
4. **23/24/19:**50نئےreal-schemaC#casesلکھے:18Purchase BEFORE/AFTERfaults،26SRfaults(cash/credit)،3paymentmethods،1sample،2late-line/second-itemrollback۔ Snapshotمیںsettings/number،supplier،products،batches،headers/items،movements،payments،audit،custom_values،sqlite_sequence؛پھرretry/books/drawerملاپ۔ **کل2176cases؛یہاںC#compile/runسبUNRUN۔**
5. **1/3/34:** نیاsourceguard،22lexicalnegatives،actual-SQLPythonprobe،version/theme/icon/scripts،یہPT/trace،guideMD/HTMLاورمستقلtrackerاپڈیٹ۔ اصلچلائےہوئےنتائجtraceمیں؛static/PythonکونہC#providerنہWindowsPASSکہیں۔

## 3۔ الگ Windows test copy میں PT-50 — تمام عملی steps UNRUN

**پہلےverifiedencryptedbackup؛پھرحقیقتاًالگDB/photos/config۔ الگexe folderکافی نہیں۔ اصلshopپرسyntheticdelivery/return/fault/permissions نہچلائیں؛اصلdata/secretchatمیںنہیں۔**

### 16.1 — delivery اورpayment

1. صافtestsupplier،productکیopeningstock0،drawerfloat5000روپے۔ اوپرکی20+20گولیdelivery،cost10،paid40cash۔ stock40،purchase400،payable360،paymentsمیںایک40،drawer4960اورsupplierBooksMatchدیکھیں۔
2. Cardاورonlineکےلیےالگصافfixture: payable360مگردrawer5000۔ Receipt،batchاورauditسےملاپ۔
3. پہلی20sampleرکھیں: stock40،samplecost/line_total0،bill200،paid40،payable160۔ Samplepolicyبدلنےیاعامpaidclampکونئیغلطیسمجھنےکیضرورتنہیں۔

### 24.1 — posting fault/rollback اورایک retry

1. Dedicatedtestfixtureمیں`StockPostingIntegrityTests`کے50casesاورپوری2176suiteچلائیں۔ `tools\RUN-TESTS.cmd`کیlog/failednamesمحفوظکریں۔ یہاںیہrunنہیںہوا۔
2. Number/header/batch/item/movement/retail/balance/payment/auditپرBEFORE/AFTERabortکےبعدsnapshotمکملبرابر؛stock/payment/payable/drawerنہبدلیں۔ TriggerہٹاکرصرفایکretryپرPINV-0001یاSR-0001اورایکصحیحdocumentبنناچاہیے۔
3. دوسریPurchase lineinvalid،یاSRکیseconditemپرfault: پہلیlineکیجزویwritesبھیrollback؛پہلےسےcommitteddeliveryباقی۔ یہtest-onlyinjectionہے،production میںdebugdoorنہیںڈالی۔

### 18.1 — supplier return کےدونوںsettlements

1. اوپرکیcashpaid deliveryسے5+5واپس: stock30،creditnote100،payable260،cashdrawer4960۔
2. الگfreshfixtureمیںcashreceived: stock30،payable360،drawer5060؛اصلpurchasepaymentکیایکrowہی،returnکیالگpaymentنہیں۔ دونوںمیںbooks،returnitems،negative movementsاورauditملاپ۔
3. SupplierBکےمطابقCost/mainOFFپرaccount/returnکیجائزworkflowچلےمگردirectitem/batchcostچھپارے۔ Main/CostکےبغیرPurchasepreparationنہیں۔ اسحصےکے50casesنئیpermissionmatrixexecutionنہیںہیں؛پرانےtestsاورWindowschecksضروریہیں۔

### 25.1 — successکےبعدprinter/refresh/callbackخرابی

1. IsolatedWPFtestfixtureمیںCoreکےsuccessfulresultکےبعدinvoice/settings/printthrow،lookupfailedresult،supplier/expiredlistfailureاورcallbackthrow/revokeالگالگinjectکریں۔ اصلGUIیہاںنہیںچلایا؛نئیproductionfaultswitchنہیں۔
2. Purchaseکےpostedlines/paid/draftIDاورSRtrayصاف؛sameactionکیreentrantinvocationدوسراpostنہکرے؛**SAVED،دوبارہdelivery/returnنہیں**۔ Historyمیںاصلایکdocumentملے؛صرفreprintکریں۔
3. حقیقیunsavedPurchaseپراجازتجائےتومقدار/costنجیطورپرمحفوظاورmaskedرہیں؛grantبحالکرکےreview۔ Postedformکوunsavedسمجھکربحالنہکریں۔

## 4۔ اسی نمبر کے اندر کیا باقی ہے؟

- **23:** C#provider/locking/busy،productionjournal/fsync،cross-process/PCاوراصلCOMMIT-I/Oambiguity۔ SQLtriggerabortانسبکاثبوتنہیں۔
- **24:**50نئےtestsکیexecution؛Sale/customerReturn،دیگرstock/expense/files،draftpremark/finalize/restorefailures،powerloss/commitmatrix۔
- **25:** localUIlatchdurablerequestidentityنہیں؛restart/networktimeout/ambiguousresultکےبعدexactlyonceOPEN۔ Sale/customerReturnکےpostcommitflowsاگلےboundedreviewمیں؛یہاںمکملنہیںہوئے۔
- **16/18/19:** mixedsupplierdelivery،اصلscanner/print/returncash،WPFbinding/focus/reentryاورمکملaccount/drawerexecutionباقی۔ Drafts.PostPurchaseکاterminalpremarkالگcommittedTxہے؛تمامdraftstateatomicrollbackکادعویٰنہیں۔
- **5/6/7/26–29:** حقیقیsession/CAS/باقیcallers،files/encryptedbackup/restore/newPC/cloudintegrationباقی۔
- **2/4/30–35/36:** Windowsbuild+2176tests+اصلlogs/screens/hardware/results/fixesکےبعدمجازsingle-doubleclicktestinstaller۔ **ابھیinstallerنہیںبنا؛FullPhase4OPEN۔**

ہرportiondocs،اسیactivebranchپرcommit/pushاوراصلremotehashverification۔36مستقلcategories،36Nextنہیں؛3–6integratedroundsصرفمشروطاندازہ،deadlineیاupperboundنہیں۔
