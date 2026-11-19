# Phase4 — حصہ32: Sale/Return کی موجودہ account/module اجازت؛ PT-52

**v0.110.0 Sandbar · مستقل5/6/7/15/17/23 · source checkpoint؛ Windows منظوری نہیں۔**

[مستقل1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md) · [فنی trace/حدود](POS-PHASE4-COUNTER-CURRENT-ACCESS-REVIEW.md) · [اردو استعمال نامہ](USER-GUIDE-URDU.md)

## 1۔ روزمرہ مثال

ManagerنےCounterکھولا،پھرOwnerنےاسaccountکوبندکردیایاPOSکیاجازتواپسلی۔ پراناUserobjectابھیwindowکےپاسہونےسےوہاگلاbillنہیںبناسکتا۔ اسیطرحReturnsکیاجازتختمہوتواگلیbillsearch،lines،quoteیاrefundصرفپرانےroleکیبنیادپرنہیںچلنیچاہیے۔ درستgrantواپسملےاورroleنہبدلاہوتواسیserviceسےتازہreadہوسکے؛roleبدلاہوتونئیsigninضروریہے۔

یہ **موجودہlocalaccount/modulevalidation** ہے؛کوئیcallerکسیحقیقیsame-roleIDکیجعلیشناختدےتواسکوlogin-sessiontokenسےauthenticateکرنےکاپوراانتظاماسحصےمیںنہیںبنایا۔

## 2۔ source میں کیا مکمل ہوا؟

1. **5/15/23 Sale:** stock/ratewarningsاورQuoteموجودہstoredactiveaccount،callerکیاسیparsedroleاورPOSgrantکےبعد۔ CreateکےTxکےاندرcurrentreader،پھرdrawerrecheck،پھرSave؛audit/financialcalculationکوموجودہstoredactorملتاہے۔ StaleOwnerobjectکیordinaryOwnerbypassبھیstoredaccountcheckنہیںچھوڑتی۔
2. **5/6/17 Return:** FindSale/Search/Linesابactorلازمیلیتےہیں؛پراناactorlessoverloadنہیں۔ Reads/QuoteموجودہReturnsgrantسے؛CreateاسیTxمیںreaderresolveکرکےSaveکودیتاہے۔ معلومdenialمیںbill/return/stock/payment/points/number/auditنہیںبدلتے۔
3. **6/25 Sale draft posting:** preflightکےعلاوہterminalpremarkTxمیںبھیcurrentPOSreader؛finalizeauditکےلیےبھیstoredcurrentactor۔ Draftpremark/Create/finalizeاببھیالگtransactionsہیں؛یہwhole-draftatomicityیاCOMMITambiguityکاحل نہیں۔ Save/Open/Holdکےتمامdraft/parkedroutesکومکملcurrent-sessionreviewنہیںکہا۔
4. **7/17 Returns UI:** actioncheckاورnominal3secondloadedtimer؛denial/readfailureپرbillID/lines/summary/searchmatches/notesاورquarantinerowsصاف،editingبند۔ Regrantپرپراناbill/formبحالنہیں،تازہFind/Refreshکریں۔ Unloadedپرtimerstop۔ Stableallowedtickunsavedquantitiesکوclearنہیںکرتا۔
5. **7/15 Counter UI:** Completeسےپہلےcurrentpreflight،warningsمیںactor،failedquoteپرپرانےcash/credit/pointsderivedoutputsصاف؛POSPrintاورReturnPrintسےپہلےcurrentpageaccess۔ ReturnکیpostcommitprintdenialمیںSAVEDبرقرار۔ POSکےتمامcachedcart/picker/receipt/historydoorsکیفوریtimer-basedصفائیاسحصےکادعویٰنہیں۔
6. **2/3:**74نئےC#cases؛کل2305متوقع۔32current-change/regrant،32invalidactors،3draftdenials،2storedOwnerchanges،5facts(storedname،financial/mainindependence،moduleindependence،confirmationpreservation،validOwnerbypass)۔ **C#compile/runtimeسبUNRUN۔** Python/sourcechecksکےاصلنتائجtraceمیںہیں۔

**Pricing،Extra،ReturnB،SupplierB،main/Cost/Profitکےطےشدہقواعدنہیںبدلے۔ مالیSale/ReturnSave/Compute/Plantailsبعینہٖبرقرار۔**

## 3۔ Windows isolated testcopy پر PT-52 — تمام عملی steps UNRUN

**پہلےverifiedencryptedbackup؛پھرواقعیالگDB/photos/config۔ الگexe folderکافی نہیں۔ اصلshopپرسyntheticbills/refunds/permissionfaultsنہیں۔ کوئیsecret/اصلDBchatمیںنہیں۔**

### 5.1 / 23.1 — staleaccount اورmodule

1. الگmanagerfixtureمیںکچھstockاورکھلاdrawer؛اسیservice/windowکےساتھallowedQuote/lookupکاbaseline۔ پھرOwnerسےaccountinactiveیاrolechangeکریں۔ اگلےwarning/quote/create/Returnfind/search/lines/quote/createمیںdenial،کوئیpostingنہیں۔
2. Same-roleaccountپرصرفPOSrevokeکریں:POSdeny،Returnsکیجائزاجازتچلے۔ پھرPOSبحالاورReturnsrevoke:الٹنتیجہ۔ Cost/ProfitgrantsیاmainOFFکواِنpagegrantsکابدلنہیںبنایا۔
3. Syntheticfixtureمیںpermissiontableunreadable:nonOwnerکواسکےroledefaultکیبنیادپرallowنہیں۔ ValidcurrentOwnerکیordinarymodulebypassبرقرار؛لیکنstoredOwnerinactive/rolechangedہوتوبھیdeny۔
4. Same-rolegrantبحالکریں؛freshreadاسیserviceسےکامیاب۔ Changedroleکےلیےنئیsignin؛صرفپراناobjectکافی نہیں۔ یہcross-processsessionauthentication/CASکاثبوتنہیں۔

### 6.1 / 24.1 — مالی state اورdraft

1. `CounterCurrentAccessTests`کے74اورپوری2305suiteکےاصلWindowslogsمحفوظکریں؛یہاںrunنہیںہوا۔ Denialسےپہلےاوربعدfinancial/draft/number/auditsnapshotبرابر۔
2. DeniedSaleDraftPostterminalmarker/payloadکوconsumeنہکرے؛grantبحالکرتودوبارہصحیحpostہوسکے۔ PremarkکےبعدCreate/finalizefaultsکےالگtestsابھیباقی۔
3. Storeduserنامبدلیںمگرcallerobjectپراناہو؛نئےSale/Returnauditsمیںstoredنام۔ ZeroID/null/inactivecaller/جعلیOwnerroleسےdata/postنہیں۔
4. MainOFFاورCost/ProfitOFFکےباوجودجائزعامSale/Returnچلیں؛quoteکےdisallowedcost/profitrawnumbers0/hidden۔ Actualbatchcostfloor،ExtraاورReturnBکےپرانےtestsبھیچلائیں۔

### 7.1 — کھلا Returns صفحہ

1. Billloaded،refundquantities،searchmatchesاورquarantinelistکھلیہوں؛OwnerسےReturnsrevokeیاaccountinactive۔ اگلےaction/nominaltickپرصفائیاورeditingdisabled۔ Failedaccessreadکومصدقہemptyhistoryنہسمجھیں۔
2. Grantواپس:تازہFind/Refresh؛پراناbillID/quantitiesواپسخودنہآئیں۔ Unchangedallowedtickپرtypedquantitiesقائم۔ Window/pageUnloadedپرtimerstop۔ اصلtiming/bindingsیہاںUNRUN۔
3. CoreکےsuccessfulsaveکےبعدPrintaccessdenyہوتو **SAVED،دوبارہbill/refundنہیں؛historyچیککریں**۔ سابقہportion31کےpostcommit/latchtestsبھیبرقرار۔

## 4۔ اسی نمبر میں باقی کام اور وجہ

- **5/6/23:** login-sessiontoken/identitybinding،same-roleIDimpersonation،remainingSalehistory/receipt/quarantine/draft-save-open/hold/quickaddcallersاورcheck/use/CAS۔ CurrentstoredIDvalidationانسبکیضمانتنہیں۔
- **7/15/17:** پوریPOScachedcart/picker/outputکیrevocation/timer/maskingpolicy،تمامremainingdialogs،اصلWPFfocus/reentry/regrant۔ اسحصےمیںPOScartکوخاموشی سےنہیںمٹایا؛مصنفکےunsavedکامکیحفاظتکیحدواضحرکھی۔
- **2/4/24/25:** تمام74اور2305کیexecution،دیگرstock/expense/partialrecovery/PR/files/draft/realCOMMITfaultmatrix،productionjournal/busy/parallel/cross-processاورdurablerequestidentity۔
- **26–29/30–35:** files/encryptedbackup/restore/newPC/cloud،Windowsbuild/logs/screens/hardware/isolation/results/fixes۔
- **36:** gatesکےبعدمجازsingle-doubleclicktestinstaller؛**ابھیinstallerنہیںبنا،FullPhase4OPEN۔**

ہرportiondocs+sameactivebranchcommit/push+actualremoteverification۔36categories،36Nextنہیں؛3–6integratedroundsمشروطاندازہ،deadlineیاupperboundنہیں۔
