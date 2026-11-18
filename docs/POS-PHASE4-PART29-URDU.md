# PT-49 — نمبر23/24/25: Customer collection اورSupplier payment کیintegrated جانچ

**v0.107.0 Breakwater · `#535D7C` · 2026-11-10 · source checkpoint؛ C#/Windows UNRUN**

[مستقل1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md) · [تفصیلیtrace/حدود](POS-PHASE4-LEDGER-POSTING-INTEGRITY-REVIEW.md)

## 1۔ اس حصے میںکیا لیا؟

1. **23 — transaction/concurrency:** Customer Collect اورSupplier Pay کےموجودہtransaction میںpayment،balance،audit،books اورdrawer کاملاپ جانچنےکےregressions۔ اصلCore transaction/financial code نہیںبدلا۔
2. **24 — stage faults:** Payment insert سےپہلے/بعد،balance update سےپہلے/بعد،audit insert سےپہلے/بعد failure؛rollback کےبعدصرفایکsuccessfulretry۔
3. **25 — acknowledged save کےبعدUI failure:** رقمsave ہوگئی،مگرpage/callback/receipt نہیںچلا؛دوبارہرقم وصول/ادا کرنےکااشارہ نہیںآناچاہیے۔ Reentrant اسیbutton action کوجاریposting کےدورانروکنا۔

مثال: Cashier نے500روپے وصول کیے؛DBsave کامیاب ہوا مگرprinter نےexception دیا۔ درستنتیجہ **SAVED،دوبارہوصولنہکریں؛historyدیکھکرreceiptدوبارہپرنٹکریں** ہے،عام“try again” نہیں۔ Supplierpay کےبعدrefreshfail ہونےپربھییہیفرقضروریہے۔

## 2۔ source میںکیا مکمل ہوا؟

1. Customer Collect اورSupplier Pay میںlocal in-progress latch؛outerfinally اسےہمیشہrelease کرتاہے۔ یہاسیviewmodelکیreentrantexecutionروکتاہے،دوسرےPC/process/restartکیidempotency نہیں۔
2. Core سےsuccessfulcommit result ملنےکےبعدauthored amount/note فوراًصاف؛reload/callback/receipt سبpostcommittry/catchمیں۔ انکیfailureپر **SAVED / Do not receive/pay again**۔
3. Callback کےبعدcurrentaccess دوبارہچیک؛accessکھونےپرپراناname/balance کامیابfreshpageکےطورپرنہیںدکھاتے۔ Denial/failurecleanup میںpaymentamount/note بھیدور۔
4. Supplier ReloadKeeping میںfailed/null list کوکامیابempty page نہیںسمجھتے؛success flag اورcleanup۔ Pay اسflagکےبغیرsuccessstatus نہیںلکھتا۔ Customer کاexistingloadsuccessflag collectionمیںبھی استعمال۔
5. **32نئےC#cases**:12stagefault/retry،3payment-methodbooks/drawer،4same/separateDatabase competing over-settlement،2fresh-service full-settlementretry،2postcommitreadrevocation،2grantreadfailure،2inactiveparent،5distinctpartialrepeat/storedaudit/separatecommit/accountisolation/financialindependencefacts۔ **کل2126cases؛یہاںسبUNRUN۔**
6. تمامCore services/data/models بعینہٖبرقرار؛pricing/stock/SupplierB/backupA/BlackboxBookCheck میںنئیpolicyنہیں۔

## 3۔ صرفالگWindows testinstallation میںعملیجانچ — UNRUN

**پہلےverified encrypted backup،پھرواقعیالگDB/photos/config۔ الگexefolderکافی نہیں۔ اصلshopپرsyntheticpayment/customer/supplier/fault/permissionexercise نہیں۔ کوئیsecretیااصلcustomerdatachatمیںنہیں۔**

### 23.1 — Cash،card،online اوردونوںbooks

1. الگtestcustomer opening1000روپے،supplier payable1000روپے،drawerfloat5000روپےرکھیں۔ Customerسے300روپےوصول،supplierکو200روپےاداکریں۔
2. Cashمیںcustomer700،supplier800،drawer5100روپے؛paymentrowsاورauditملاکرCustomer receipt/دونوںstatementsدیکھیں۔ Card/onlineکےاسیexerciseمیںdrawer5000ہی؛accountbalancesضروربدلیں۔
3. Main/Cost/ProfitOFFکےساتھجائزCustomers/Suppliersaccountingwritesبرقرار؛Supplier exceptionB دوبارہفیصلہطلبنہیں۔ Stock/batchquantitiesنہیںبدلنیچاہئیں۔
4. C#concurrencycases میں1000owedکےخلافدو600کیcompetingposts؛sameDatabaseاورseparateDatabaseinstancesدونوںمیںایکہیcommit،400باقی،ایکpayment۔ یہtestsلکھےہیں؛اصلproviderexecutionباقی۔ PythonSQLiteنتیجہاسC#testکاPASSنہیں۔

### 24.1 — بارہfaultstages اورretry

1. الگautomatedfixtureمیںدونوںactionsپرpaymentBEFORE/AFTER،balanceBEFORE/AFTER،auditBEFORE/AFTERfault لگائیں۔ DBکوہاتھسےاصلshopمیںنہبدلیں۔
2. Failureکےبعدpayment/balance/audit/stock پہلےجیسے،دونوںbooksmatch،drawerunchanged۔ Providerprivatefaultmarkerusererrorمیںنہآئے۔
3. Triggerہٹاکرایکretry؛صرفایکpayment/auditmovementاورصحیحbalance/drawer۔ Failedsecondactionپہلےسےcommittedfirstactionواپس نہلے۔
4. یہstatement-triggerfaultsہیں؛actualCOMMIT I/O failure،powerloss،ambiguouscommit اورتمامcross-servicefaultsاسسےثابتنہیںہوتے۔

### 25.1 — Savedمگرrefresh/callback/receiptfailure

1. CorepostsuccessکےبعدصرفUIreloadfail/throw،callbackthrow اورCustomerreceipt/printerthrow الگdeveloper-controlledfixtureمیںinduceکریں۔ واضح **SAVED،دوبارہreceive/payنہیں**؛authoredamount/noteصاف،DBpaymentایک۔
2. Printdialog/callbackکےدورانreentrantاسیactionآئےتوsecondpostنہہو۔ actionfinish/failureکےبعدlatchreleaseہو؛جائزنیاamountبعدمیںلکھاجاسکے۔ اصلWPFnesteddispatchertimingثبوتدرکار۔
3. PostcommitcallbackgrantواپسلےتوfreshaccesscheckکےبعدgenericSAVEDwarning؛پراناauthorizedsummaryبحالنہہو۔ Regrant/refresh/historyسےاصلentryدیکھیں،بغیرrepostreceiptreprintکریں۔
4. Fullsettlementکےبعدfreshserviceسےوہیamountدوبارہرد؛یہbalanceguardہے،requesttokenنہیں۔ **Partialpaymentکوبعدمیںدوبارہدیناجائزnewpaymentہوسکتاہے**؛testsاسکوصافالگکرتےہیں۔ یکساںamount/noteکیبنیادپرخودکارduplicateclampنہیںلگایا۔
5. SupplierReturn،Sale/Purchase/Return اوردوسرےcommandsکےpostcommitpathsاسحصےمیںمکملنہیںہوئے۔ SuppliersharedReloadKeepingfailureصفائیبہتر،مگرSRکااپناresultflowابھیالگreviewمانگتاہے۔

## 4۔ Actual جانچ بمقابلہباقی کام

1. **2/3:**32نئےC#cases/2126suite source؛اصلCore/Tests/Desktopcompile/runUNRUN۔ Static،lexicalnegativeاورPythonSQLiteنتائجtrace/آخریرپورٹمیں،WindowsPASSنہیں۔
2. **23:** ledger-pairsourceguards/testmatrix؛اصلC#parallel/provider/busy/timing،cross-process/PCاورcommit-I/Oباقی۔
3. **24:** دونوںpostsکےstatementfaults/retrycases؛Sale/Purchase/Return/stock/payment/filefaultmatrixاورactualC#executionباقی۔
4. **25:** acknowledgedsuccessکےبعدSAVED/reentrysource؛durablerequesttoken،timeout/restart/ambiguouscommit اورglobalidempotencyباقی۔ UIlatchکوexactly-onceضمانتنہکہیں۔
5. **5/6/7:** حقیقیauthenticatedsession،باقیcallers/parentworkflows/cache/dialogs/CASاورWindowsregrant/focusproofباقی۔
6. **26–29/4/30–35/36:** files/encryptedbackup/restore/newPC/cloud،Windows2126+results/fixes/hardware/isolation،آخرمجازsingle-doubleclicktestinstaller؛**ابھینہیںبنا،Phase4OPEN۔**

ہرportiondocs+اسیbranchcommit/push+actualremoteverificationلازم۔36نمبر36Nextنہیں؛مشروطroundestimate deadlineنہیں۔
