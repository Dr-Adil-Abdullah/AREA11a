# حصہ18 — Customer کھاتہ،وصولی اور Doctor/rep کی موجودہ اجازت

**v0.96.0 Grebe · 2026-11-10 · PT-38**

## کیا بدلا؟

مثلاً Customer کا1000باقی ہے۔ Staffکےکھاتےکیاجازت بندکردی،مگرپرانافارمکھلاہے۔ اب **Receive کےtransactionکےاندرموجودہactiveaccount،وہیroleاورCustomersgrant** دوبارہچیکہوگا؛صرفپراناOwner/managerlabelکافینہیں۔ مجازوصولیپرreceipt/Auditکےلیےموجودہstoredreaderنام،اصلرقم/بقیہ/capunchanged۔

- **Customers اجازت سےعامکھاتہ،وصولی،statement/receipt؛CostیاProfitکیشرطنہیں۔** MainOFFپرعاموصولیبندنہیںکی۔ بقایاسےزیادہرقماببھیرد،creditکوcollectionmethodنہیںبنایا۔
- Doctorاورlifetimecustomerprofitکےلیےموجودہ **Customers + الگProfit + mainON**؛صرفCost،managerعہدہیاProfitبغیرCustomersکافینہیں۔ جائزdelegatedstaffProfitدیکھسکتاہے؛مجازProfitسےcostinferenceممکن،مطلقsecrecyنہیں۔ ProfitمنعہوتوDoctorکیsales/billsبرقرار،rawprofit0مگرknownfalseاورdash؛lifetimeprofittabرد/hidden۔
- Companyrepکیفہرست/ترمیمکےلیےموجودہProductsgrant؛Saveکےtransactionمیںدوبارہcheck،Auditمیںstoredنام۔ Repنام/phone/dayکےاصلvalidationsبرقرار۔
- CustomeraccountاورWhatsAppprofileتازہPageسے۔ Failedreadیاgrant/accountdenyپرجمعشدہlist/کھاتہ/selection/bulk/receiptصاف۔ CustomerبدلےتوپچھلےCustomerکاآخریreceiptbuttonساتھنہیںچلےگا۔
- Customer/Doctorsactiveviewsمیںnominal3secondLoaded/Unloadedcheck۔ Profitتبدیلیپرپرانےprofitاعدادصاف؛Customerکیصرفprofitبحالیپرتازہprofit،لکھاہواeditformخودreset/saveنہیں۔ Moduledenyپreditpanelبنداورcopiedlimitصاف۔ Doctorprofitتبدیلیrepکاادھورافارمنہیںمٹاتی۔
- رسیدکیپرانے **Received by** سطرابموجودہprintingreaderکاstoredناملیتیہے؛اصلhistoricalreceiverکیالگنئیlookupنہیںبنائی۔

**Blackbox،BookCheck،Shellmenu،Tax،backupA،اصلSQL/returns/loyalty/pricing/stockنہیںبدلے۔** [مکملtraceاورحدود](POS-PHASE4-CUSTOMER-CURRENT-REVIEW.md)۔

## واقعی کیا جانچا؟

| جانچ | نتیجہ |
|---|---|
| Static source/grammar/member/XAML | **85 PASS**؛compilerنہیں |
| غلطsourceتبدیلیاں | **13 رد**؛اصلbytesبحال |
| SyntheticSQLiteprobes | **13 PASS**؛C#serviceنہیں |
| الگPythonbackupmodel |6roundtrips+24negativePASS؛DPAPIنہیں |
| نئےC#tests |24factsلکھے؛کل **1765casesUNRUN** |
| Windows/WPF/DPAPI/restore/installer | **UNRUN؛installerنہیںبنا** |

689baselinefilesبرقرار؛112دوسرےCore service/datahashesexact؛Customerکے8اورRepکے5verbatimSQLblocksunchanged۔ یہfullPhase4/securityPASSنہیں۔

## PT-38 — صرف واقعی الگ test ماحول میں

**Live shop پر فرضیbill،collection،return،repchangeیاrestoreنہکریں۔** VerifiedbackupاورواقعیالگVM/DB/photosپہلے؛دوسریexefolderالگdataنہیں۔ Windowsپر `tools\RUN-TESTS.cmd`؛متوقع **1765cases**،پورااصلlogمحفوظکریں۔ WPFbuild/launchالگجانچیں۔

1. Customer-onlystaffسےsynthetic1000میں250وصول؛بقیہ750اورBooksmatch،Cost/Profit/mainOFFعاموصولینہروکیں۔ Cash/online/cardسباصلmethodکےساتھ۔ Purchase/SupplierیاجعلیPAYکواسCustomerreceiptکےبدلےنہچھاپے۔
2. بقایاسےزیادہ،zero،negativeیاcreditmethodرد؛payment/balance/Auditکیpartialentryنہہو۔ Inactive/missingcustomerسےوصولیرد۔
3. PageکھولنےکےبعدCustomersgrantrevoke،accountdeactivateیاstoredroleبدلیں؛وصولی/نئیreceipt/statement/profileرد،اگلےcheckپرپرانےاعداد/selection/receiptصاف۔ دوبارہgrantپرتازہload،خودpostنہیں۔
4. CustomerAکیوصولیکےبعدBچنیں؛AکاآخریreceiptbuttonBکےساتھفعالنہرہے۔ پہلےسےمحفوظPAYreceiptکےtypedIDاصولبرقرار۔
5. Doctorکے600کیsales،400کیcostfixtureمیںمجازprofit200؛Customer-onlyیاCost-onlyکوسales/billsمگرprofitdash/rawunknown۔ ProfitgrantبغیرCustomersپوریlifetimeprofitreadکیاجازتنہبنے۔
6. Customers+Profitوالےdelegatedstaffکوprofitملے،managerکوخودنہملے۔ MainOFFپرOwnerبھیprofitنہدیکھے؛عامCustomerکھاتہبرقرار۔ Grant/mainبحالیپرتازہprofit،پرانےmasked0کوknownprofitنہکہے۔
7. Customereditformمیںناملکھاہواہو؛صرفProfitgrantبدلےتواگلےcheckپرمالیdisplayماسک/تازہ،ادھورافارمخودsave/resetنہہو۔ Customergrantdenyپرpanelبند،پراناکھاتہاورcopiedlimitصاف۔
8. Products-onlyشخصrepدیکھ/ترمیمکرسکے،Customerprofitنہکھلے۔ Customers-onlyDoctorدیکھےمگرrepProductsاجازتکےبغیرنہیں۔ Profitبدلنےپرrepکالکھاہوافارمبرقرار؛Productsdenyپرselectedcompany/copiedformصاف۔
9. Storedنامبدلکرcollection/repSave؛Auditمیںتازہنام۔ Receiptمیںموجودہprintingreaderکاstoredنام،نہcachedDTOنام۔ Copyنامکیwidth/پرنٹرالگچیک۔
10. صرفisolatedDBمیںAuditfailureinject؛collectionpayment/balanceیاrepupdateسبrollback،screenپرrawSQL/حساسexceptionنہیں۔ اگلیکوششسےپہلےاصلresultملائیں۔
11. Unknown/forged/inactive/sameIDchangedrolefixturesسےreads/writesرد۔ SuppliedvalidaccountIDخودsessionauthenticationنہیں؛دوسرےaccountکیاصلIDدینےکوعالمگیرحفاظتکاثبوتنہکہیں۔
12. موجودہCustomerreturn/loyalty/monthstatement/repvalidationsکےپرانےtestsبھیچلائیں۔ [PT-35backup](POS-PHASE4-PART15-URDU.md)،[PT-36](POS-PHASE4-PART16-URDU.md)،[PT-37](POS-PHASE4-PART17-URDU.md)اور[D-115](BLACK-BOX-MENU-VISIBILITY.md)ابھیWindowsپرلازمہیں۔

Timerکی3secondsسختdeadlineنہیں؛Settingscache/dispatcherاورپہلےبنائیfile/preview/clipboardکیفوریrecallکادعویٰنہیں۔ باقیoutputs/Tax/session/record/fault/concurrency/files/cloudاورWindows/installerکھلےہیں؛BookCheckپرنیاکامآپکیD-115ہدایتکےمطابقنہیںکیا۔ اندازہ **3–6 مزیدمربوطreview/resultچکر**؛نتائجسےبڑھسکتےہیں،وعدہنہیں۔ Docsاوراسیbranchپرcommit/push/remoteverificationلازم۔
