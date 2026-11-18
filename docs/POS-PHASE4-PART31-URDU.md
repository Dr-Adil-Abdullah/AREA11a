# Phase4 — حصہ31: Sale اور customer Return؛ PT-51

**v0.109.0 Harbour · مستقل15/17/19/23/24/25 · source checkpoint؛ Windows منظوری نہیں۔**

[مستقل1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md) · [فنی trace/حدود](POS-PHASE4-COUNTER-POSTING-INTEGRITY-REVIEW.md) · [اردو استعمال نامہ](USER-GUIDE-URDU.md)

## 1۔ روزمرہ مثال اور محدود کام

دوproductsکی10+10گولیاں،فی گولی20روپے: bill400؛100cash،300khata۔ stock200سے180،customerbalance300،drawer+100۔ پوریواپسیپر300khataکم،100cashrefund؛restockہوتوstock200،quarantineہوتو180برقراراورالگQنمبر۔ Loyalty100روپےپرایکpointکےfixtureمیں40سے44،پوریواپسیپر40۔

درمیانمیںخرابیہوتوادھورابill/return،stock،payments،points،balance،numberیاauditنہبچے۔ **کامیابsaveکےبعدreceipt/pageخراب ہوتوSAVED؛دوبارہbill/وصولی/refundنہیں،پہلےhistoryچیککریں۔**

## 2۔ کیا source مکمل ہوا؟

1. **15/25 Sale:** پہلےسےموجودcompletionlatchبرقرار؛successfulCore resultکےبعدdraftIDinvalidate،cart/tendered/splitamount/Extra/discount/basisreset،پھرrefresh/print/modal۔ ابprint/settings/confirmationexceptionعامunsavedfailureنہیںبنتی۔ ExpectedprinterfailureبھیSAVEDکیوضاحتکےساتھ،popupOFFپربھی۔
2. **15/25 اضافیحفاظت:** اگرbinding/collectionexceptionکیوجہسےpostedcartکیصفائیپوری نہہوتوlocalcommitted-cartblock؛دوبارہSale،SaveDraftیاHoldنہیں۔ Historyچیککریں،پھرexplicitClearcart؛یہrestart/cross-PCdurableidentityنہیں۔
3. **17/25 customer Return:** localreentry/finally؛acknowledgedsuccessپرbillIDفوراً0،form/quantitiesprintسےپہلےصاف۔ Quarantineکیfailedpending/historyreadابمصدقہemptyنہیں؛SAVEDwarning۔ Printerکاfailedresultآخریsuccessmessageسےoverwriteنہیںہوتا۔
4. **15/19/23 drawer:** CoreSaleمیںپراناdrawer-readexceptionallowختم؛unknownپرrefusal۔ PreflightکےعلاوہاسیSaleTxconnectionپرکسیwriteسےپہلےopen-shiftrecheck۔ Closed/unreadableاپنیطرفسےopenنہیںفرضہوتا۔ Saleکیpricing/Compute/Saveمالیtail،Extra/ReturnBاورReturnServiceunchanged۔
5. **2/24:**55نئےC#cases؛کل2231متوقع۔18Salefaults،26restock/quarantineReturnfaults،5paymentmethods،2closed/unreadabledrawer،2seconditemfaults،1loyaltyredemptionrollback،1ExtraReturnB560پھر540withfault/retry۔ **سبC#compile/runtimeUNRUN۔**
6. **1/3/34:** sourceguard/lexicalnegative/actualSQLPythonprobe،version/theme/icon/scripts،PT-51/trace،guideMD/HTMLاورمستقلtrackerاپڈیٹ۔ اصلچلائےگئےchecksاورحدودtraceمیںہیں۔

## 3۔ Windows isolated testcopy کی عملی جانچ — تمامsteps UNRUN

**پہلےverifiedencryptedbackup؛پھرواقعیالگDB/photos/config۔ الگexe folderکافی نہیں۔ اصلshopپرسyntheticbill/refund/fault/permissionexerciseنہیں۔ Secrets/اصلcustomerDBchatمیںنہیں۔**

### 15.1 / 19.1 — sale،payments،points اورdrawer

1. الگtestcustomer،دوproducts100+100stock،cost10/retail20،drawerfloat5000روپے؛loyaltyON،100روپےپرایکpoint،openingpoints40۔10+10کا400bill،100cash/300credit۔ Stock180،balance300،points44،ایکcashpayment100،drawer5100،booksmatch۔
2. الگfreshfixturesمیںcash/card/online/credit/splitچلائیں۔ Card/onlinecashdrawerنہبڑھائیں؛creditکوpaymentrowنہبنائیں۔
3. Drawerclosedیاreadfailureکیtestfixtureپرbillrefuse؛کوئیfinancial/stock/numberchangeنہیں۔ Transactionrecheckکیsourcewiringاصلprovider/parallelruntimeکابدلنہیں؛race/lockingtestsالگباقی۔

### 17.1 — پوریواپسی،stockیاquarantine

1. اوپرکےsplitbillکیپوریواپسی: refund400،khata300کم،cash100واپس؛points40،balance0،drawer5000۔ Restockپرstock200؛الگfixtureمیںquarantineپر180اوردوQنمبر۔
2. اصلsale/saleitems/paymentsنہیںبدلنےچاہییں؛returnاپنیrows،cashrefundاپنیsale_returnsfieldsمیں،نئیpaymentنہیں۔ Fullreturnکےبعداسیquantityکیواپسیرد؛یہbill/quantitycapہے،globalrequestdedupنہیں۔
3. موجودہpolicyمیںcard/onlineکےrefundبھیcashrefundہیں؛اسحصےمیںنیاchannel-selectionruleنہیںڈالا۔ Partialrecoveries/PRکےزیادہپیچیدہcasesکےپرانےtestsاورمزیدintegrationباقی۔

### 24.1 —55نئےC#cases اورپوری2231suite

1. `tools\RUN-TESTS.cmd`سے`CounterPostingIntegrityTests`اورپوریsuiteکےاصلlogs/failednamesمحفوظکریں؛یہاںrunنہیںہوا۔
2. BEFORE/AFTERnumber/header/item/batch/movement/payment/balance/loyalty/auditfaultپرتمامboundedtablesکیsnapshotبرابر۔ Faultہٹاکرایکصحیحretry،INV-0001/RET-0001،صحیحbooks/stock/points/drawer۔
3. Seconditemfaultمیںپہلیitemکیwritesبھیrollback۔ Returnfailureمیںپہلےسےcommittedbillمحفوظ۔ Quarantinecounterبھیrollback۔
4. 5redeempointsکےlateauditfaultپرnegative-loyaltypaymentاورpointsدونوںrollback؛retryپرموجودہقواعدسےصحیحbalance۔ Extraوالےfixtureمیںپہلاrefund560،دوسرےپرfault،retry540،تیسراrefundرد؛productlinepricesنہیںبدلتیں۔

### 25.1 — saveکےبعدUI/printخرابی

1. DedicatedWPFfixtureمیںsuccessfulCore resultکےبعدcartcollection/bindingthrow،lookup/customeditorfailure،receipt/settings/printerthrow،popupfailureاورReturnquarantinereadfailureالگinjectکریں؛کوئیproductionfaultswitchنہیںڈالی۔
2. SaleپرSAVED؛postedcartصافیاexplicitcommitted-resetblock۔ ResetناکامہوتوSale/draft/holdدوبارہنہیں؛historyدیکھکرClearcart۔ ReturnپرbillID0اورformصاف؛دوبارہgoods/refundنہیں۔
3. PopupOFFاورprinterfailedresultمیںبھیSAVEDواضحرہے؛Returnprintwarningبعدکیsuccessسےنہچھپے۔ اصلbinding/reentry/timingیہاںUNRUN۔

## 4۔ اسی نمبر کے اندر کیا باقی ہے؟

- **23:** حقیقیC#provider/busy/parallel/productionjournal/fsync/cross-processاورCOMMIT-I/Oambiguity۔ SourcegateاورPythonTxانکاثبوتنہیں۔ Sale/Returnکےexistingmodulechecksکومکملcurrent-sessionauthenticationنہیںکہہسکتے؛یہالگباقیboundaryہے۔
- **24:**55کیexecution؛دیگرstock/expense/files،draftpremark/finalize/restorefailures،recoveries/PR/partialreturnsاورpowerloss/commitmatrix۔
- **25:** durableoperationidentity،restart/timeout/crossPC/ambiguoussuccess؛localcartflag/latchexactlyonceنہیں۔ GenericCorefailuremessagesکاactualCOMMITambiguityوالاحصہنہیںبدلا۔
- **15/17/19:** تمامpayment/permission/receipt/handover/closing/scannerاوراصلWPFcallback/reentryflowsکیعملیجانچ۔ Pricing/Extra/ReturnB/SupplierBدوبارہطےنہیںکرنے۔
- **5/6/7/26–29:** حقیقیsession/CAS/remainingcallers/caches،files/encryptedbackup/restore/newPC/cloudintegration۔
- **2/4/30–35/36:** Windowsbuild+2231tests+اصلlogs/screens/hardware/results/fixesکےبعدمجازsingle-doubleclicktestinstaller؛**ابھینہیںبنا،FullPhase4OPEN۔**

ہرportiondocs+sameactivebranchcommit/push+اصلremoteverification۔36categories،36Nextنہیں؛3–6integratedroundsمشروطاندازہ،deadlineیاupperboundنہیں۔
