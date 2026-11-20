# Area11 — مکمل اردو استعمال نامہ

**کس کے لیے:** مالک، مینیجر اور کیشیئر۔ **جس کوڈ کے مطابق لکھا گیا:** v0.124.0 Sorrel۔ یہ استعمال کی کتاب ہے، Windows پر کامیاب جانچ یا اصل دکان میں منظوری کا سرٹیفکیٹ نہیں۔ اس ورژن کے Windows ٹیسٹ اور عملی تصدیق باقی ہیں۔

**⏳ مشترکہ Draft — تازہ حصہ5 checkpoint:** ابایکDraftدوجگہکھلاہواورتازہمحنتدوسرےنےSaveکیہوتوپراناSave/Post/Discardمنعہوگا۔ **changed after the form was opened**پراندھادوبارہSaveنہکریں؛موجودہDraftدوبارہکھولیں،reviewکریں،پھراپنیترمیم۔ اسconflictسےنہنیاbillبنانہپہلےوالےکیترمیممٹی۔ KnownvalidationfailureکےبعداسیDraftمیںcorrectionممکن؛**unconfirmed/verification marker/SAVED**کیہدایاتالگہیں،دوسریcopyبناکراندھیretryنہکریں۔ [⏳ PT-66/حدود](DRAFT-EDIT-CONCURRENCY-REVIEW-URDU.md)؛36نئےcases/3118suiteاوراصلWindowsجانچباقی۔ **Settings/StockTakeاورباقیراستےابھیحصہ5میںباقی**۔ نیچےپچھلےcheckpointکیsharedDraftباقیہدایاتتاریخیہیں۔

**⏳ حصہ5/14 — پرانا فارم نئی تبدیلی نہ مٹائے:** یہحصہابھیجاریہے۔ Product/Customer/extraanswers،definition/nativeاورCatalog/UnitکےمتعلقہSaveمیں **record or its fields changed** آئےتواسeditسےکچھمحفوظنہیںہوا؛موجودہrecordدوبارہکھولیں/Refresh،reviewکریں،پھرمطلوبہدرستی۔ ایکہیپراناSaveباربارنہدبائیں،خودکارmergeنہیں۔ Brandکےنامپر **SAVED** اورlinkconflictالگہوسکتاہے؛نامدوبارہبنانےکیمتکوششکریں۔ **Settings/sharedDrafts/StockTakeاورباقیراستےابھیاسیحصےمیںباقی**؛ہرSaveکومحفوظنہسمجھیں۔ [⏳ PT-65/حدود](EDIT-CONCURRENCY-REVIEW-URDU.md)؛25نئےfacts/3082suiteاوراصلWindowsجانچباقی۔

**⏳ حصہ4/14 — لاگ اِن اور پرانا فارم:** مالک نےpassword/PINبدلنےپرتازہloginمنتخبکیاہے۔ اگلاعملپرانےloginسےنہکریں؛محفوظریکارڈبرقرار،غیرمحفوظفارمدوبارہبھرناپڑسکتاہے۔ Counterکیاجازتواپسہوتوپورےoutputs/lookups/آخریreceiptاورunsavedextraanswersچھپ/صافہوتےہیں؛cartاورdraft/unknown/SAVEDguardsبرقرار۔ حقواپسآنےپرRefreshaccessسےfreshlists/fields/quote،پھربلreviewکریں؛discardedextraanswersخودواپسنہیںآتے۔ idle/12گھنٹےکیحدبرقرار۔ **اصلWindows/key/mouse/modalجانچباقی**؛[⏳ PT-64/حدود](AUTHENTICATED-SESSION-REVIEW-URDU.md)،3057suiteUNRUN۔ نیچےپرانےcheckpointsتاریخیہیں۔

**⏳ حصہ3/14 — ریکارڈ کے Save کے بعد خرابی:** نیاگاہک/سپلائر/دوا/کیٹلاگبنانےپر **SAVED** آئےتواسکامطلبریکارڈمحفوظہوگیا،چاہےفہرستتازہنہہوسکے۔ دوبارہوہیSave/Addنہدبائیں؛فہرست،تصاویر/auditمیںاصلریکارڈدیکھیں۔ **unconfirmed** میںبھییہفرضنہکریںکہکچھنہیںبنا؛پہلےتصدیق۔ Refresh،Cancelیااجازتواپسملنےسےاسیفارمکیretryروکنہیںکھلتی؛جانچکےبعدpage/windowدوبارہکھولیں۔ صرفproductmodalدوبارہکھولنااسیProductpageکوresetنہیںکرتا۔ CounterکیcartاورPurchaseکیlinesفوریاندراجسےصافنہیںکیجاتیں؛pageبدلنےسےپہلےانہیںمحفوظکریں۔ نئیpageیاری‌اسٹارٹخودکارverificationیاduplicateضمانتنہیں۔ [⏳ حدود/PT-63](PARENT-RECORD-SAVE-OUTCOME-REVIEW-URDU.md)؛72نئےcases/3035suiteکااصلWindowsنتیجہباقی۔

**⏳ حصہ2/14 — ٹیسٹ کی تیاری کی اصلاح:** v0.120.1کےاصلWindowsrunمیں2941کامیاب/22ناکام/2963کل؛کمپائلہوگیا۔ v0.120.2میںانناکامیوںکےمتعلقٹیسٹتیاریبدلی،دکانکےبٹن،مالیحسابیااجازتیںنہیں۔ نئےbuildپرWindowsدوبارہجانچباقی؛[منصوبہ](REMAINING-14-PORTIONS-URDU.md)اور[اصلنتائج](WINDOWS-V1201-TEST-REPORT-REVIEW.md)۔ یہپراناcheckpointہے؛بعدکےnextسےحصہ3کیsourceپیشرفتہوئی،حصہ4شروعنہیںہوا۔

**⏳ موجودہ محدود اصلاح — حصہ1/14:** v0.120.1میںصرفایکtestکیexpense-listcallمیںلازمیصارفجوڑا؛دکانکےبٹن،مالیحسابیااجازتوںمیںتبدیلینہیں۔ اسوقتاصلcompileتصدیقباقیتھی،بعدمیںv0.120.1Windowsrunسےملگئی؛[منصوبہ](REMAINING-14-PORTIONS-URDU.md)کےاگلےحصےپرنئیاجازتکےبغیرکامنہیںہوگا۔

**⏳ حصہ42 — Receive/Pay کے بعد دوبارہ رقم نہ لکھیں:** CustomersکیCollect،UdhaarRecoveryکیReceiveاورSuppliersکیPayمیں **unconfirmed** ہوتوپہلےpaymenthistory،کھاتہاورdrawerملائیں؛نہیہفرضکریںکہکچھنہیںبدلا،نہوہیرقمدوبارہلکھیں۔ **SAVED** کےبعدrefresh/callback/receiptخرابہوتواسیpageپرpostingبند؛Refreshیاpermissionregrantاسےنہیںکھولتے۔ جانچکےبعدpageدوبارہکھولیں؛reopenخودکارverificationنہیںاورCustomersسےRecoveryیادوسرےPCپرنیاpageالگguardہے۔ معمولکیsuccessکےبعدamount/noteخالی؛اگلیواقعیالگpartialpaymentتازہamountسےجائز،خودکارamount-baseddedupنہیں۔ Recoveryمیںنئےcustomerکیinitialselectionپرباقیرقمکیپرانیآسانیبرقرار،مگرsuccessfulReceiveکےبعدوہautofillصاف۔ [⏳ PT-62](POS-PHASE4-PART42-URDU.md)،[⏳ حدود](POS-PHASE4-LEDGER-OUTCOME-REVIEW.md)۔ ⏳47نئےcases/2963suiteUNRUN؛FullPhase4OPEN؛❌installerنہیںبنی۔

**⏳ حصہ41 — تصویر کی copy اور retry:** اگر Add کا نتیجہ **unconfirmed** ہو تو copy برقرار،دوبارہ Add سے پہلے record/gallery/photo folder verify کریں؛صرف rollback ثابت ہونے پر نئی owned copy ہٹ سکتی ہے۔ Collision پر پرانی تصویر نہیں بدلی جاتی؛نقل12MB تک محدود ہے۔ Gallery میں unknown یا**SAVED**کےبعدrefresh/previewخرابہوتواسیwindowمیںدوبارہAddبند؛Refreshیااجازتواپسآنااسےنہیںکھولتا۔ جانچکےبعدgalleryدوبارہکھولیں؛reopenخودکارverificationنہیں۔ Product/Customer/Suppliercreationکیstagedcopyبھیunknownپررہتیہے؛انformsمیںduplicate retryکیجامعحفاظتکادعوینہیں۔ [⏳ PT-61](POS-PHASE4-PART41-URDU.md)،[⏳ حدود](POS-PHASE4-PHOTO-FILE-FAULT-REVIEW.md)۔ ⏳28نئےcases/2916suiteUNRUN؛FullPhase4OPEN؛❌installerنہیںبنی۔

**⏳ حصہ40 — PRکیcurrentاجازت/مکملreceipt:** پراناaccount/codeکافینہیں؛موجودہactive/sameparsedrole/Returnsچیک،Attach/CancelcurrentOwner/Manager۔ اگلےaction/nominal3stickپرdeniedrows/tray/refund/customer/phone/notes/lookupصاف؛یہunsavedtypedworkمٹاتاہے،خودpostingنہیں۔ Stableallowedticktypingبرقرار؛regrantپرexplicitRefresh،پرانامتنبھالنہیں۔ Failedlistreadکوemptyنہیںکہا،decision/reprintبند؛line-readfailمیںheader-onlysuccessfulreceiptنہیں۔ [⏳ PT-60](POS-PHASE4-PART40-URDU.md)،[⏳ حدود](POS-PHASE4-PROVISIONAL-CURRENT-REVIEW.md)۔ ⏳99نئےcases/2888suiteUNRUN؛یہsession/CAS/instantrecallنہیں؛FullPhase4OPEN؛❌installerbinaryنہیںبنی۔

**⏳ حصہ39 — PRSave/Attach/Cancel:** کامیاب action کے بعد fields/selection/refresh/printخرابہوتو **SAVED**؛وہیrefund/attach/cancelدوبارہنہیں۔ Unknownمیںپہلےhistory/stock/paymentverify؛صرفexplicitno-commitپرlocalretry۔ کامیابیکےبعددونوںdecisionfields/lookupاورLoadکااگلاautomaticpendingselectionصاف،اگلاactionتازہexplicitselectionسے۔ Linkدوسراstock/paymentنہیں،Cancelcashrefundخودواپسنہیںلاتا۔ یہlocalguardہے،restart/globalidentityنہیں؛PRcurrentaccess/read/receiptجانچالگباقی۔ [⏳ PT-59](POS-PHASE4-PART39-URDU.md)،[⏳ حدود](POS-PHASE4-PROVISIONAL-POSTING-REVIEW.md)۔ ⏳45نئےcases/2789suiteUNRUN؛FullPhase4OPEN؛❌installerbinaryنہیںبنی۔

**⏳ حصہ38 — خرچ/مالک کی drawing/Write-off/Stock-take Apply:** کامیاب save کے بعد page/field/editor/refresh خراب ہو تو **SAVED**؛وہی خرچ/مال دوبارہ نہ لکھیں۔ ادھوری صفائی یا نامعلوم outcome میں اسی page پر posting بند؛پہلے history verify،پھر page دوبارہ کھولیں۔ صرف واضح no-commit پر retry۔ Unknown Apply میں count/clear/cancel/open/found بھی بند؛پراناappliedsessiononce-onlyبرقرار۔ یہlocalflagہے،restart/cross-PCduplicateprotectionنہیں؛expenseکےلیےautomaticdraftنہیںبنا۔ [⏳ PT-58](POS-PHASE4-PART38-URDU.md)،[⏳ حدود](POS-PHASE4-EXPENSE-STOCK-FAULT-REVIEW.md)۔ ⏳59نئےcases/2744C#suiteUNRUN؛FullPhase4OPEN؛❌installerbinaryنہیںبنی۔

**⏳ حصہ37 — عامSaveبھیsharedDraftسے:** عامCounterComplete/PurchaseSaveابپہلےخودکارsharedDraft،پھراسیIDسےpost؛مجازstaffبھیDraftsمیںمحفوظکامدیکھسکتاہے۔ Validationکےبعداسیdraftکودرستکریں؛unknownpostingmarkerمیںdocumentverifyکیےبغیرretryنہیں۔ ابتدائیdraftsavingہیunknownہوتوpostingشروعنہیں؛پہلےDraftscheck،ممکنہopenrowسےکامکریں۔ عامکھلےdraftکاParkبھیمنع؛Save/Completeاسیپر۔ HoldکےبعدcleanupخرابہوتوPARKED،دوسریsale/parkنہیں؛PurchasepartialcleanupمیںSAVED،historyverifyپھرصفحہدوبارہکھولیں۔ [⏳ PT-57](POS-PHASE4-PART37-URDU.md)،[⏳ حدود](POS-PHASE4-AUTOMATIC-DRAFT-REVIEW.md)۔ ⏳41نئےcases/2685C#suiteUNRUN؛FullPhase4OPEN؛❌installerbinaryنہیںبنی۔ ✅reportاصول:✅صرف100%مکمل؛⏳کچھباقی؛❌شروعنہیں۔

**حصہ36 — محفوظ Resume اور posting:** Bring back اسی transaction میں linked recovery Sale draft بناتا ہے؛restart پر Open drafts میں `held-…` سے saved کام واپس لیں۔ Counter اسی draft سے Complete کرتا ہے؛دوبارہ Park کرکے copy نہیں۔ Reconstruction یا missing-product error پر partial cart Save/Post نہیں؛Clear confirmation saved draft کو نہیں مٹاتی،unsaved edits ضائع ہوسکتے ہیں۔ Unknown posting میں marker رہے؛پہلے Bills/Purchases verify کریں۔ Confirmed no-commit پر ہی draft بحال؛finalization fault میں **SAVED،دوبارہ post نہیں** warning۔ [PT-56](POS-PHASE4-PART36-URDU.md)،[حدود](POS-PHASE4-RECOVERY-FAULT-REVIEW.md)۔29نئےcases/2644C#suite/WindowsUNRUN؛FullPhase4/installer OPEN۔

**حصہ35 / مستقل5/6/7/15/16/23/25 — Drafts/Parked cartsکی موجودہ اجازت:** Saledraft/held=POS؛Purchasedraftکیفہرست/discard=Purchases،لیکنpayloadکھولنا/بدلنا/saveکرناCost+mainبھیچاہتاہے۔ Currentaccountبند/roleبدل/grantواپسہوتوپراناdraft/cartIDکافینہیں۔ Drawersاگلےaction/nominal3secondtickپرdeniedlist/selectionصاف؛regrantپرتازہRefresh۔ Countfailureکوzeroنہیںکہا۔ ModalYesکےبعدپھراجازت/rowcheck؛draftpost/discardکےبعدreloadfailمیںSAVED۔ UnsavedPOScartنہیںمٹایا؛HeldResumeکےبعدUIreconstructionاورwhole-draftfaultsالگباقی۔ [PT-55](POS-PHASE4-PART35-URDU.md)،[trace](POS-PHASE4-DRAFT-HELD-CURRENT-REVIEW.md)۔ **136نئےC#cases/2615UNRUN؛Windows/fullPhase4/installerOPEN۔**

**حصہ34 / مستقل5/6/7/17/20/23/24/25 — Quarantine کی موجودہ اجازت:** Pending/History/CountموجودہReturnsaccountسے؛صرفجائزOwner/Managerفیصلہکرسکتاہے،عامReturnsreaderفہرستدیکھسکتاہے۔ Countreadناکامہو توzeroکہہکرچیزیںصافنہیںسمجھیں۔ پرانی/detachedrowپرactionنہیں؛SAVEDdecisionکےبعدrefreshناکامہوتودوبارہفیصلہنہکریں،تازہRefreshکریں۔ Restocked/Soldکیواپسی،Expiredمیںstockنہبڑھنااورrefundrulesنہیںبدلے۔ [PT-54](POS-PHASE4-PART34-URDU.md)،[trace](POS-PHASE4-QUARANTINE-CURRENT-REVIEW.md)۔ **79نئےC#cases/2479UNRUN؛Windows/fullPhase4/installerOPEN۔**

**حصہ33 / مستقل5/6/7/15/17/21/23/24 — receipt/Bills کی موجودہ اجازت:** accountبند،roleبدلا یاgrantواپسہوتوپراناbillIDreceiptنہیںدیتا۔ Counterreceipt=POS،Counterreturnreceipt=Returns،Billsسےدونوںreprints=Sales؛یہآزادgrantsہیں۔ Billsصفحہاگلےaction/nominal3secondtickپرrows/detail/preview/totals/tickedsetصافکرتاہے؛grantبحالپرRefresh،پراناselectionخودواپسنہیں۔ Stableallowedtickselectionبرقرار۔ Reprintauditلکھناناکامہوتوچھپیcopyکونہچھپنےکیغلطدعوتنہیں؛printed-but-unrecordedwarning۔ پہلےسےکھلاpreview/exportedfile/کاغذواپسنہیںلیاگیا۔ [PT-53](POS-PHASE4-PART33-URDU.md)،[trace](POS-PHASE4-RECEIPT-CURRENT-REVIEW.md)۔ **95نئےC#cases/2400UNRUN؛Windows/fullPhase4/installerOPEN۔**

**حصہ32 / مستقل5/6/7/15/17/23 — موجودہCounter/Returnsاجازت:** accountبند/roleبدل/modulegrantواپسہوتواگلیSalewarning/quote/postیاReturnlookup/quote/postمیںپراناUserobjectکافی نہیں؛Txمیںcurrentstoredactorچیک۔ Returnصفحہnominal3second/actionپرپراناbill/form/search/quarantinerowsصافاورeditingبند؛grantبحالکرتازہFind/Refresh،پرانامتنخودبحالنہیں۔ POSمیںnextaction/printchecksہیں،پورےcachedcartکیفوریtimerصفائییاسessiontokenکادعویٰنہیں۔ Cost/Profit/main،Extra/ReturnBunchanged۔ [PT-52](POS-PHASE4-PART32-URDU.md)،[trace](POS-PHASE4-COUNTER-CURRENT-ACCESS-REVIEW.md)۔ **74نئےC#cases/2305suiteUNRUN؛remainingcallers/session/CAS/Windows/fullPhase4/installerOPEN۔**

**حصہ31 / مستقل15/17/19/23–25 — billاورcustomerواپسی:** Saleکاکامیابsaveملےتوcart/paid/split/Extra/draftIDprint/modalسےپہلےconsume؛بعدکیreceipt/pageخرابیپر **SAVED،دوبارہbill/رقموصولنہیں،historyچیککریں**۔ صفائیادھوریہوتوlocalblockSale/draft/holdروکے؛historyکےبعدClearcart۔ ReturnsuccessپربillID/formپہلےصاف؛quarantinefailedreadمصدقہemptyنہیں،printwarningچھپتینہیں؛ **دوبارہgoods/refundنہیں**۔ CoreunknowndrawerپرابbilldenyاورSaleTxکےاندرپہلےrecheck۔ Pricing/Extra/ReturnBunchanged؛localflagrestart/crossPCexactlyonceنہیں۔ [PT-51](POS-PHASE4-PART31-URDU.md)،[trace](POS-PHASE4-COUNTER-POSTING-INTEGRITY-REVIEW.md)۔ **55نئےC#cases/2231suiteUNRUN؛session/COMMIT/Windows/fullPhase4/installerOPEN۔**

**حصہ30 / مستقل16/18/23–25 — deliveryاورsupplierreturn:** Purchaseکاکامیابsaveملنےپرpostedlines/paid/draftIDprintسےپہلےصاف؛receipt/pagefailتو **SAVED،دوبارہdeliveryنہوصولکریں،historyسےصرفreprint**۔ Supplierreturnمیںکامیابsaveکےبعدtrayصاف؛account/expiredlist/callbackfailتو **SAVED،دوبارہوہیgoodsواپسنہیں**۔ Creditnoteaccountکم،cashreturnaccountبرقرار/drawerin،اضافیpaymentنہیں؛samplezero-coststockاورSupplierBبرقرار۔ LocalUIlatchrestart/دوسرےPCکیexactlyonceنہیں؛DraftpremarkالگTx۔ [PT-50](POS-PHASE4-PART30-URDU.md)،[trace/حدود](POS-PHASE4-STOCK-POSTING-INTEGRITY-REVIEW.md)۔ **50نئےC#cases/2176suiteUNRUN؛Sale/customerReturn/Windows/fullPhase4/installerOPEN۔**

**حصہ29 / مستقل23/24/25 — moneyreceive/pay:** Customercollection/SupplierpaymentمیںCoreکاsuccessfulsaveملنےکےبعدpage/callback/receiptfailہوتو **SAVED،دوبارہرقموصول/ادانہکریں؛Refresh/historyچیککریں**۔ Amount/noteصاف،جاریاسیactionکیreentrantcallرکتیہے،finallyrelease؛یہrestart/دوسرےPCکیexactly-onceضمانتنہیں۔ Failedsupplierreloadمصدقہempty/successنہیں۔ Fullsettlementکےبعدsameamountردbalanceguardہے؛بعدمیںجائزpartialpaymentدوبارہہوسکتیہے،خودکارamount-baseddedupنہیں۔ [PT-49](POS-PHASE4-PART29-URDU.md)،[trace/حدود](POS-PHASE4-LEDGER-POSTING-INTEGRITY-REVIEW.md)،[1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md)۔ **32نئےC#cases/2126suiteUNRUN**؛Coreمالیcodeunchanged،COMMIT-I/Oambiguity/fullPhase4/installerOPEN۔

**حصہ28 / مستقل5/6/7 — Catalogs/Units:** تمام16catalogwriterscurrentProductsOwner/Managerسے؛جائزread-onlyProductsreaderبرقرار،Cost/Profit/mainکیcatalogپرنئیشرطنہیں۔ Account/module/readfailureپرrows/selection/نام/order/parent/company/WholeOnlyصاف،editingبند؛grantواپسآئےتوexplicitRefreshاورreview،پرانامتنبھالنےکادعوینہیں۔ Stableallowed3sticktypedinputsنہیںبدلتا؛windowبندہوتوtimerstop۔ Saveہوچکاہوreloadناکامتو **SAVED،دوبارہactionنہیں**۔ Brandnameاورcompanylinkالگtransactions؛partialsuccessواضح۔ [PT-48](POS-PHASE4-PART28-URDU.md)،[trace/حدود](POS-PHASE4-CATALOG-CURRENT-REVIEW.md)،[1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md)۔ **49نئےC#cases/2094suiteWindowsUNRUN**؛session/CAS/fullPhase4/installerOPEN۔

**حصہ27 / مستقل5/6/7 — customer/product edits:** Customer کاphone/name edit currentCustomersaccountسے؛Cashier کیجائزcorrectionبرقرار،limitOwner/Manager۔ FormکیintendedIDpin،selection/cancel/accesslossپرauthored/customstateصاف۔ Productcreate/update/put-awaycurrentProductsOwner/Managerسے؛existingstoredcost/full-editorCost+main،Profitاسکابدلنہیں۔ Authorednewproductcostاورnon-costCorepatchکیپرانیworkflowبرقرار۔ کھلےProducteditorکیاجازتبدلےتوnominal3second/actioncheckپرپراناcost/form/lookupsصاف،Saveبند،explicitclose/refresh/reopen؛grantواپسدینےسےdiscardedformخودبحالنہیں۔ Commitمگرrefreshfailتو **SAVED،دوبارہactionنہکریں**۔ [PT-47](POS-PHASE4-PART27-URDU.md)،[trace/حدود](POS-PHASE4-RECORD-WRITE-CURRENT-REVIEW.md)،[1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md)۔ **36نئےfacts/2045C#casesWindowsUNRUN**؛authenticatedsession/instantrecall/CAS/fullPhase4/installerکیضمانتنہیں۔ نیچےپرانےcheckpointsتاریخیہیں۔

**حصہ26 / مستقل نمبر8 — سوال بنانے کا manager:** currentOwnerreadکےبعدcontrolsenable؛failedreadپرrows/selection/editID/label/options/help/required/typeصاف،Fieldsnotloaded۔ Selecteddefinitionبدلےتواگلاactionرکے،واضحdiscard/reviewwarning؛unchangedtickunsavedtextنہیںبدلتا۔ Save/Move/HideRestorecommitمگرreloadfailتو **SAVED،دوبارہactionنہکریں**۔ [PT-46](POS-PHASE4-PART26-URDU.md)،[trace](POS-PHASE4-CUSTOM-MANAGER-CURRENT-REVIEW.md)،[مکملنمبر1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md)۔ **32نئےfacts/2009C#casesWindowsUNRUN**؛نمبر8runtimeاورباقیrecord/session/files/Windows/installerکھلےہیں۔ SnapshotatomicCASنہیں؛نیچےپرانےcheckpointتاریخیہیں۔

**حصہ25 — Extra answers / Native fields:** current internal actor/stored custom audit،missing parent failure،editor کی پرانی rows/staged photo metadata صفائی اور fresh outputs،Save کے بعد failed reload پر **SAVED**؛Native failed selection صاف/current definition پہلے پڑھی جاتی ہے۔ POS quick customer create پر نئی Customers grant نہیں؛public answer panel کی الگ grant برقرار۔ [PT-45](POS-PHASE4-PART25-URDU.md)،[trace](POS-PHASE4-CUSTOM-VALUES-CURRENT-REVIEW.md)۔ **32 نئے facts/1977 C# cases Windows UNRUN**؛باقیcustom admin/parent records/session/files/Windows/installer کھلے ہیں۔ نیچےپرانےcheckpointتاریخیہیں۔

**حصہ24 — Photos:** موجودہgallery/module/actor،photoIDسےfreshpreviewpath،staleimage/selection/rows/title/countصفائی،windowaccess-timerاورpostcommit **SAVED**۔ Productcoversکیجائزcross-workflowdecorationاورownavatarبغیرStaffgrantبرقرار،galleryکیالگmoduleشرطبدستور؛historicalinactiveparentpolicyنہیںبدلی۔ [PT-44](POS-PHASE4-PART24-URDU.md)،[trace](POS-PHASE4-PHOTO-CURRENT-REVIEW.md)۔ **30نئےfacts/1945C#casesWindowsUNRUN۔** Native/custom/session/record/files/hardware/installerباقی؛حساب/Blackbox/BookCheck/backupunchanged۔ نیچےپرانےcheckpointتاریخیہیں۔

**حصہ23 — Settings کی موجودہ اجازت:** Save میں currentactive/same-rolemodulewriter،cloudکےلیےSync،owner-protectedkeys/workbookکےلیےcurrentOwner؛POSکااکیلاautoPrintاورBackupscheduleبرقرار۔ Quietsetterصرف17runtimekeys؛آئندہCloudkeyauditمیںاصلold/newkeyنہیں،پرانےauditنہیںبدلے۔ DeniedSettingsکےtabsچھپیں،typedcloud/PIN/recoveryاورstaffstateصاف؛cloud-onlylossپرعامادھورےeditsبرقرار۔ [PT-43](POS-PHASE4-PART23-URDU.md)،[trace](POS-PHASE4-SETTINGS-CURRENT-REVIEW.md)۔ **36نئےfacts/1915C#casesWindowsUNRUN۔** Native/custom/photo/session/recordاورfullPhase4/installerباقی؛حساب/Blackbox/BookCheck/backupunchanged۔ نیچےپرانےcheckpointتاریخیہیں۔

**حصہ22 — اجازت اور transaction:** جائز delegated grant اسیtransactionمیںقائم؛explicitrevokeیاgrant-readerrorسےپراناrole-defaultallowنہیں۔ کامیابreadمیںrowواقعیغائبہوتوdefaultبرقرار۔ اپنیconnectionکاہیtransaction،permissionکیpendingstateاسیconnectionسے؛per-instance synchronous serializationپورےsession/concurrencyکاثبوتنہیں۔ [PT-42](POS-PHASE4-PART22-URDU.md)،[traceاورحدود](POS-PHASE4-TRANSACTION-PERMISSION-REVIEW.md)۔ **27نئےfacts/1879C#casesWindowsUNRUN۔** حساب/Blackbox/BookCheck/backupunchanged؛Settings/session/record/fault/files/cloud/Windows/installerباقی۔ نیچےپرانےnumberedcheckpointتاریخیہیں۔

**حصہ21 — گلہ:** currentCashPage/ByPerson،OpenClosetransaction،Handoverمیںcurrentدینے/لینےوالے؛storedنام،countedfloatاورone-rupeereasonبرقرار۔ Failedreadکوclosed/Rs0نہیںکہتے؛POSصرفstatus،unknownپربلblock؛expiryopen/unknownپرcheck/countnote،فرضیcloseنہیں۔ Stalenumbers/history/recipient/lastslipصاف؛savedمگرrefreshfailتو**SAVED،actionدوبارہنہکریں**۔ [PT-41](POS-PHASE4-PART21-URDU.md)،[traceاورکیاباقیہے](POS-PHASE4-SHIFT-CURRENT-REVIEW.md)۔ **1852C#cases/WindowsUNRUN؛88staticPASS**۔ Othertransactionalservicesکےconnectionpattern،Settings/session/Windows/installer/fullPhase4باقی؛Blackbox/BookCheckunchanged۔

**حصہ20 — parent tabs اور staff list:** Customers/RecoveryاورSuppliersاپنےcurrentmodulegrantsسے؛منعbranchdetach،selectionجائزfallback،allowedchildformsہرtickدوبارہنہیںبنتے۔ FullstaffrostercurrentactiveOwner-only؛گلہhandoverکاالگcurrentCashactive-other-personpicker،Staffpanelنہیں۔ Denial/failedrosterپرstaffselection/permissions/pricing/typedsecretsصاف؛nominal3sviewchecks،فوریrecallنہیں۔ [PT-40](POS-PHASE4-PART20-URDU.md)،[حدود](POS-PHASE4-PARENT-ROSTER-REVIEW.md)۔ **1817C#cases/WindowsUNRUN؛87staticPASS**۔ FullSettings/gallareviewباقی؛Blackbox/BookCheck/حسابunchanged۔

**حصہ19 — Recovery اور ماہانہ report:** موجودہ Customers سے Recovery/collection-slip،موجودہ Reports سے محفوظ monthly report؛Cost/Profit/main کی نئی شرط نہیں،tax ON نہیں کیا۔ Failed report/copy پر stale rows/totals/setting label صاف؛Recovery میں selection/amount/phone/opening/books/last-slip صاف۔ ناکام slip کی جگہ خالی page نہیں چھپتا؛وصولی save مگر refresh fail تو **SAVED،دوبارہ وصول نہ کریں**۔ Nominal3s activeview checks فوریrecall نہیں۔ [PT-39](POS-PHASE4-PART19-URDU.md)،[حدود](POS-PHASE4-RECOVERY-MONTHLY-REVIEW.md)۔ **1789C#cases/WindowsUNRUN؛86staticPASS**۔ Blackbox/BookCheck/financialrulesunchanged۔

**حصہ18 — Customer/Doctor/rep کی موجودہ اجازت:** collection/repSaveکےtransactionمیںcurrentactive/same-roleactor؛DoctorاورlifetimeprofitcurrentCustomers+الگProfit/main،repProducts۔ Customerکھاتہ/وصولیCost/Profit/mainبندہونےسےنہیںرکتی۔ Freshprofile/receipt،staleaccount/selection/lastreceiptصفائیاورnominal3secondactiveviewchecks؛profitبدلےتوادھورےedit/repformخودsave/resetنہیں۔ [PT-38](POS-PHASE4-PART18-URDU.md)،[حدود](POS-PHASE4-CUSTOMER-CURRENT-REVIEW.md)۔ **1765C#cases/WindowsUNRUN؛85staticPASS**۔ Blackbox/BookCheckunchanged،D-115برقرار۔

**D-115 — صرف Black box menu کی نئی حد:** نئےsign-inپرموجودہ **Audit + View Purchase Cost + View Profit + main ON** ہوںتوBlackboxنظرآئےگا؛صرفAudittickکافینہیں۔ Permissions/mainبدلنےکےبعدsignout/signinکریں؛یہpatchفوریlive-menurebuildنہیںکرتی۔ Blackboxکےاندرونیrecords/metadata/detailbehaviorاورBookCheckunchanged۔ [مختصرscope/Windowsمشق](BLACK-BOX-MENU-VISIBILITY.md)۔ **1741C#cases/WindowsUNRUN؛84staticPASS**۔

**حصہ17 — Supplier کا منتخب B استثنا:** Supplier page grant سےaccount/paid/payable/statement/sharing/Pay/SRکےحسابCost/mainOFFپربھی؛paid500+due1500سےpurchase2000کاinferenceآپنےقبولکیا۔ Directitem/batch/traycostصرفCost+main؛manual/expiredSRquantity-onlyتیار،unknowncostکوknown0نہیںکہتے۔ CurrenttransactionalPay/SRactor،freshaccount/profile،اگلےcheck/nominal3secondپرstaleclear/mask؛فوریrecallنہیں۔ [PT-37](POS-PHASE4-PART17-URDU.md)،[حدود](POS-PHASE4-SUPPLIER-ACCOUNTING-REVIEW.md)۔ **1740C#cases/WindowsUNRUN؛84staticPASS**۔ پرانےnumberedcheckpointخلاصےتاریخیہیں؛Supplierunanswered/known-cost-onlytrayہدایتD-114سےبدلگئی۔

**حصہ16 — Stock Minus:** مقدار/episode/review metadata برقرار؛purchase valuation اورمجموعہ صرف Cost+main سے،raw Audit review note دونوں مالی grants/main سے۔ موجودہ Stock reader اورreview transaction میں current account چیک؛review stock نہیں بدلتا،جائز Stock delegation پرnote لکھنے کے لیے Cost/Profit کی نئی شرط نہیں۔ Active-view access change پر selected/details/totals refresh/clear؛فوری recall کی ضمانت نہیں۔ [PT-36](POS-PHASE4-PART16-URDU.md)،[حدود](POS-PHASE4-STOCK-MINUS-REVIEW.md)۔ **1716 C# cases/Windows UNRUN**؛83 static checks PASS۔

**حصہ15 — الگ password backup:** `.a11bak` کے لیے Owner کا Set / change backup password؛Automatic کے لیے اسی Windows account کی protected local storage،setup نہ ہو توFAIL/no plain fallback۔ Manual/auto/full safety/rollback ایک ہی encrypted format؛restore پہلے authentication/checksum،پھرZIP/DB validation،پھر encrypted safety اورlive swap۔ نئےPC/oldbackup کے لیےاصلpassword؛local safety password پہلےsetup،rotationپرoldpasswordبرقرار۔ Legacy ZIP/DB الگ Owner-only UNVERIFIED acceptance؛پرانےbackupsنہdeleteکریں۔ [PT-35](POS-PHASE4-PART15-URDU.md)،[format/حدود](POS-PHASE4-ENCRYPTED-BACKUP-REVIEW.md)۔ **1692C#cases/Windows/DPAPI/new-PC UNRUN**؛82static اورindependentPythonmodel PASS،certificationنہیں۔

**حصہ14 — Excel/CSV اور Audit:** موجودہ checkbox کا نام **Allow Excel/CSV Bulk Export & Import**؛default صرف Owner،Manager/Staff کو Owner کی explicit grant۔ Editable product file کے لیے Cost/main،mixed shop workbook دونوںfinancial/main؛انکار پرmasked0file نہیں۔ Audit کے rawold/new/details کے لیے دونوںfinancial/main،ورنہmetadata؛hiddenfieldsearch/count بھیبند۔ چارCSVdoorsپرbulkgrant؛freshexport،cachedrawpageنہیں۔ [PT-34](POS-PHASE4-PART14-URDU.md)،[trace](POS-PHASE4-BULK-AUDIT-REVIEW.md)۔ **اس حصے14 کے وقت encryption pending تھی؛نیا source حصہ15 میں،پرانے ZIP/DB اب بھی encrypted نہیں۔** [اصل contract/تازہ حیثیت](SECURE-BACKUP-PASSWORD-PLAN.md)۔1641C#cases UNRUN،81static PASS؛Windows/fullPhase4 باقی۔

**فیز4 حصہ13 — خرید کا editable hidden0 نہیں:** نئی خرید اور purchase draft کھولنے/محفوظ/update/post کے لیے موجودہ Purchases + الگ Cost + main ON؛صرف Profit/manager کافی نہیں۔ Summary/discard موجودہ page اجازت پر الگ۔ اجازت بند ہو تو unsaved لائنیں/اصل رقم برقرار مگر cost/paid/total/status اگلے check پر mask،Save بند؛اجازت واپس پر review،خودSave نہیں۔ Nominal3second timer کی فوری recall ضمانت نہیں۔ [PT-33](POS-PHASE4-PART13-URDU.md)،[trace/حدود](POS-PHASE4-PURCHASE-PREPARATION-REVIEW.md)۔1609 C#cases UNRUN،80static PASS؛Supplier/generic exports/session/Windows باقی۔

**فیز4 حصہ12 — expense میں cash/مال الگ:** Goods-drawing کی List/Today/new result/voucher valuation الگ Cost+main سے؛منع ہو تو raw money mask/hidden۔ کرایہ/نقدdrawing اور گلے کا cash total متعلقہ Cash grant پر برقرار؛Stock tag صرف label،receiving نہیں۔ Failed Today معلوم0 نہیں،hidden۔ اصل SaveGoods/SQL/accounting unchanged۔ [PT-32](POS-PHASE4-PART12-URDU.md)،[trace/حدود](POS-PHASE4-EXPENSE-VISIBILITY-REVIEW.md)۔1579C#cases UNRUN،79static PASS؛purchase/supplier/generic exports/session/Windows ابھی باقی۔

**فیز4 حصہ11 — stock/frozen valuation:** Write-off history/today/save result اور stock-take Current/History/sheet/found/new print/Open/Apply output الگ Cost+main کے تحت؛منع ہو تو raw money/labels mask،گنتی/فرق/status برقرار۔ اصل frozen cost اور Count/Apply حساب unchanged؛مجاز0،uncounted—،hidden الگ۔ نئے print میں query failure successful empty نہیں۔ [PT-31](POS-PHASE4-PART11-URDU.md)،[trace/حدود](POS-PHASE4-STOCK-VALUATION-REVIEW.md)۔1551C#cases UNRUN،78static PASS؛Expense goods outputs حصہ12 میں منتقل؛Windows/generic exports/purchase/supplier/session rollout باقی۔

**فیز4 حصہ10 — عام batch اور تازہ تیاری:** Stock/Expenses/Suppliers کے batch picker کی cost/valuation الگ Cost+main سے؛quantity/batch/date برقرار۔ Write-off/Expenses preview اور manual SR tray تازہ current cost/quantity/pack لیتے ہیں؛ناکامی پر hidden/انکار،نہ stale رقم نہ fake0۔ **Stock history/today/results اور frozen stock-take حصہ11 میں منتقل؛Expense goods outputs حصہ12 میں منتقل؛Supplier/generic exports ابھی باقی،global secrecy نہیں۔** [PT-30](POS-PHASE4-PART10-URDU.md)،[trace/حدود](POS-PHASE4-BATCH-READ-REVIEW.md)۔1521C#cases UNRUN،77static checks PASS؛Windows باقی۔

**فیز4 حصہ9 — خرید invoice / expired stock:** نئے purchase document میں cost منع ہو تو line amount،Total،Paid،Due سب hidden؛نام/quantity/batch/date برقرار۔ expired list کی raw cost/valuation بھی mask؛Add whole batch تازہ500حد والی row اور known cost دوبارہ چیک کرتا ہے،انکار پر tray untouched۔ supplier عام paid/payable/Pay/SR پالیسی اور posting unchanged،یہ global completion نہیں۔ [PT-29](POS-PHASE4-PART9-URDU.md)،[trace/حدود](POS-PHASE4-PURCHASE-READ-REVIEW.md)۔1493 C# cases UNRUN،76 static checks PASS؛Windows باقی۔

**موجودہ POS فیز3:** مستقل search pane منتخب ہے۔ v0.78.0 میں owner-only ہر شخص کی الگ قیمت/رعایت حدیں، Cart کا محفوظ item editor اور profit/retail حساب code میں شامل ہیں۔ **موجودہ دکان کے آئندہ bills کا ابتدائی default اب profit ہے**؛ مالک Settings سے retail واپس کرسکتا ہے۔ پرانے محفوظ bills unchanged۔ الگ ایڈٹ شدہ لائن پر bill% دوبارہ نہیں؛ خرید سے نیچے کسی کو اجازت نہیں۔ **الگ sale-level Extra Discount اب فیز3 کے code میں شامل؛ مالک کا return طریقہ B: پہلے560 پھر باقی540، اصل item rates نہیں بدلتے**، جامع page/15-point audit فیز4 میں۔ Windows tests/build/عملی ثبوت باقی۔ [فیز2 کی آسان مشق اور واپسی](POS-PHASE2-TEST-URDU.md) ، [فیز3 استعمال/آزمائش/بندش](POS-PHASE3-TEST-URDU.md) اور [مکمل منصوبہ](POS-NEXT-REVIEW-PLAN-URDU.md) الگ ہیں۔

**فیز4 حصہ8 — اجازتوں کی نئی مدد:** Staff & permissions میں الگ **Sensitive financial & ledger permissions** حصہ؛ View Profit اور View Purchase Cost الگ، Supplier/Customer ledger اپنے موجودہ page grants۔ ہر checkbox پر hover summary؛ double-click یا Details… سے scope اور owner-only موجودہ enabled/disabled assignment list۔ nominal3s refresh، active/main-switch blockers الگ؛ single mouse click double-click وقفے کے بعد save، Space native۔ یہ **global backend rollout مکمل نہیں کرتا**؛ [PT-28](POS-PHASE4-PART8-URDU.md)، [trace/حدود](POS-PHASE4-PERMISSION-INSPECTOR-REVIEW.md)۔1469 C# cases UNRUN،75 static checks PASS؛ حقیقی Windows modal/gestures اور مالی rollout باقی۔

**Windows کی موصولہ رپورٹ / تازہ patch:** آپ کے v0.80.0 میں1337 میں4 tests فیل،1333 پاس ہوئے؛ یہ اسی پرانے build کا اصل نتیجہ ہے۔ v0.85.1 میں cashier test کی حقیقی account fixture، pricing role-mismatch refusal، BookCheck snapshot کی اصل gap اور نئے/legacy finding counts درست کیے؛ ابھی Windows پر دوبارہ نہیں چلائے۔1449 cases لکھے،74 static checks PASS۔ چاروں failures اب اصل مکمل log سے شناخت ہوچکے؛updated Windows rerun باقی؛ [تفصیلی investigation](WINDOWS-V080-TEST-REPORT-REVIEW.md)۔ newer workspace ابھی push نہیں کیا، صرف Pull سے دستیابی کا دعویٰ نہیں۔

**فیز4 حصہ7 کی تازہ حیثیت — D-110:** آپ کے تازہ فیصلے کے مطابق **View Profit** اور **View Purchase Cost** الگ ticks؛ موجودہ main switch دونوں کو روکتا ہے، متعلقہ page اجازت بھی لازم۔ manager عہدہ کافی نہیں۔ catalog/History/Reorder/DeadStock/ReportTotals/item quote منتقل؛ preparation میں cost منع ہو تو fake0 کی بجائے انکار۔ **پورے پروگرام کی everywhere consistency ابھی مکمل نہیں**: نئے invoice/expired reads حصہ9 میں منتقل؛purchase entry/drafts/results،supplier-ledger/generic batch/stock-take/expense/workbook/audit/دوسرے exports باقی۔ [حصہ7 شواہد/باقی rollout](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md)، [محفوظ PT-27](POS-PHASE4-PART7-URDU.md)، [جامع register](POS-PHASE4-EVIDENCE.md)۔1445 C# cases لکھے مگر یہاں نہیں چلائے؛73 static checks PASS، Windows/full phase4 OPEN۔ actual batch cost/Extra/returnB/اصل snapshots unchanged۔

**پڑھنے کا طریقہ:** حصہ اوّل میں صرف صفحات/اختیارات کی مختصر فہرست ہے۔ حصہ دوم میں انہی کی الگ، قدم بہ قدم تفصیل ہے۔ پہلے پورا خلاصہ دیکھیں؛ پھر جس کام کی ضرورت ہو اسی نمبر کی تفصیل پڑھیں۔ HTML فائل براؤزر میں کھلتی ہے، انٹرنیٹ کے بغیر بھی؛ براؤزر کے Print سے کاغذ یا PDF بنا سکتے ہیں۔

**زبان:** سکرین کے اصل انگریزی نام ساتھ رکھے ہیں تاکہ بٹن پہچان سکیں۔ مالک App words سے نام بدلے تو سکرین پر مختلف لفظ آ سکتا ہے۔ ہر شخص کو صرف اجازت والے صفحات دکھتے ہیں؛ صفحہ نظر نہ آنا لازماً خرابی نہیں۔

## حصہ اوّل — مکمل نقشہ اور اختیارات کا خلاصہ

یہاں طریقۂ کار نہیں، صرف یہ بتایا گیا ہے کہ کون سا اختیار کیا کرتا ہے۔ تمام تفصیل اگلے حصے میں الگ ہے۔

| سبق | صفحہ یا اختیار | مختصر کام |
|---|---|---|
| [01](#lesson-01) | Sign in / Forgot password? | اپنے نام سے داخلہ؛ مالک کے لیے الگ recovery word |
| [02](#lesson-02) | اوپر کی navigation، نام کا مینو، Dashboard | صفحہ بدلنا؛ دکان کی حالت؛ اپنی نشست ختم کرنا |
| [03](#lesson-03) | Shift (galla) | گلا کھولنا، اصل نقدی گننا، بند کرنا یا اگلے شخص کو دینا |
| [04](#lesson-04) | Products | تلاش، فلٹر، New product، Edit product یا قطار پر ڈبل کلک |
| [05](#lesson-05) | Product editor | نام، نمک، برانڈ، کیٹیگری، سپلائر، طاقت، پیک، ریٹ، ریک، محفوظ کرنا |
| [06](#lesson-06) | Catalogs / Managed units | کیٹیگری، کمپنی، نمک، برانڈ اور یونٹ کی فہرستیں سنبھالنا |
| [07](#lesson-07) | Photos / Barcodes / Product history | تصویر، cover، pack کا کوڈ اور پرانی خریدوفروخت |
| [08](#lesson-08) | Purchases | اصل ڈیلیوری سے سٹاک وصول، بیچ/میعاد، سپلائر کا واجب الادا اور ادائیگی |
| [09](#lesson-09) | Counter (POS) | چیز ڈالنا، مقدار/یونٹ، کسٹمر، ادائیگی، رعایت، بل محفوظ کرنا |
| [10](#lesson-10) | Counter availability | فی بل ایک وارننگ؛ سٹاک نہ ہو تو کسی کا بل نہیں؛ بغیر ریٹ صرف مالک/مینیجر |
| [11](#lesson-11) | Park / Drafts | ادھورا کام محفوظ؛ بعد میں کھولنا، پوسٹ کرنا یا draft ترک کرنا |
| [12](#lesson-12) | Bills / Fix wrong time… | پرانا بل تلاش، دوبارہ پرنٹ، preview، WhatsApp؛ مالک کی وقت درستگی |
| [13](#lesson-13) | Returns / Quarantine | اصل بل سے واپسی؛ رقم/ادھار درست؛ مشکوک مال الگ رکھنا |
| [14](#lesson-14) | No bill (PR) | بل کے بغیر واپسی کا مستقل ریکارڈ؛ بعد میں اصل بل جوڑنا |
| [15](#lesson-15) | Customers / Khata | کسٹمر، کھاتہ، وصولی، حد، statement اور ماہانہ بیان |
| [16](#lesson-16) | Udhaar recovery | پرانا ادھار، عمر کی فہرست، وصولی اور رسید |
| [17](#lesson-17) | Suppliers / SR | واجب الادا، Pay، supplier return، credit note یا نقد واپسی |
| [18](#lesson-18) | Doctors & reps | نسخے کے ڈاکٹر، کمپنی کے نمائندے، آنے کا دن، lifetime profit |
| [19](#lesson-19) | Reorder pad | کم سٹاک، minimum، ایک سپلائر کا آرڈر، WhatsApp متن/پرنٹ |
| [20](#lesson-20) | Write-off / Stock minus | خراب/ضائع مال کا نقصان؛ پرانے منفی سٹاک کا جائزہ |
| [21](#lesson-21) | Stock-take | اصل گنتی، فرق، صرف مالک کی حتمی منظوری |
| [22](#lesson-22) | Expenses / Owner drawings | دکان کا خرچ الگ؛ مالک کی نقدی یا مال الگ |
| [23](#lesson-23) | Reports / Tax report | تاریخ وار کاروبار؛ منافع؛ ماہانہ ٹیکس کا موجودہ ریکارڈ |
| [24](#lesson-24) | Alerts | یاد دہانی اور مالی/سٹاک مسئلے الگ؛ مسئلہ صرف مالک وجہ سے ختم کرے |
| [25](#lesson-25) | Black box / Books check | کس نے کیا بدلا؛ پرانی حالت؛ حساب کی صرف پڑھنے والی جانچ |
| [26](#lesson-26) | Excel / CSV | نئی فہرست import، export، workbook bulk update، قیمتیں بدلنا |
| [27](#lesson-27) | Backup & restore | تصویروں سمیت encrypted package؛ خودکار شیڈول؛ پرانی حالت بحال کرنے کا خطرہ |
| [28](#lesson-28) | Settings: App name & icon / Shop | ایپ کی شناخت الگ؛ رسید والی دکان کی تفصیل الگ |
| [29](#lesson-29) | Settings: Printing / Printer & scanner | کاغذ، copies، auto-print، bulk، PDF اور اصل ہارڈویئر کی جانچ |
| [30](#lesson-30) | Settings: Sales rules | رعایت کی حد، cost lock، credit lock، profit، points، stock/expiry |
| [31](#lesson-31) | Settings: Staff & permissions / App words | لوگ، PIN/password، recovery word، اجازتیں اور صفحات کے نام |
| [32](#lesson-32) | Settings: Fields / Own fields | موجودہ خانہ required/optional؛ اپنے سوال اور ان کی ترتیب |
| [33](#lesson-33) | Startup / Protected close / Screen lock | Windows کے ساتھ آغاز، بندش کی اجازت، کام چھوڑنے کا ریکارڈ |
| [34](#lesson-34) | Cloud sync / Second counter | اختیاری cloud mirror؛ دوسرا کاؤنٹر فی الحال ملتوی |
| [35](#lesson-35) | Preview / WhatsApp / کاغذ | پرنٹ سے پہلے دیکھنا؛ PDF خود attach کرنا؛ دوبارہ پرنٹ ≠ نیا بل |
| [36](#lesson-36) | روزانہ کام، محفوظ مشق اور مسئلہ حل | صبح سے بندش تک ترتیب؛ غلطی، dash، کریش یا پرنٹر کے بعد اگلا قدم |

### Settings کے ہر گروپ کا مختصر مطلب

| گروپ | اختیارات اور مطلب |
|---|---|
| App name & icon | App name: کھڑکی/login/taskbar کا نام؛ Choose app icon…: اپنی .ico؛ Use default icon: اصل آئیکن |
| Shop | thumbnail size: تصویر کتنی بڑی؛ shop name/phone/address/footer: رسید کے الفاظ؛ discount line: کاغذ پر رعایت؛ idle sign-out: بےکار نشست؛ clock: وقت کی حالت؛ startup/lock: سبق 33 |
| Printing | 58/80 mm: چوڑائی؛ printer: کون سا پرنٹر؛ auto-print: بل کے بعد خود پرنٹ؛ cutter؛ bulk print: منتخب قطاریں؛ separate/A4: الگ رسیدیں یا خلاصہ؛ PDF folder؛ copies |
| Sales rules | owner کا default profit/retail basis؛ خرید سے نیچے فروخت مستقل بند؛ khata-limit lock؛ مشترک profit/cost visibility main switch؛ Done popup؛ loyalty on/rate/VIP threshold؛ tax on/rate؛ expiry کی تین حدیں؛ supplier return کی reminder/cutoff |
| Cloud sync | on/off، address، table، shop id، محفوظ key، automatic push کا وقفہ، Remove the saved key |
| Staff & permissions | شخص منتخب؛ account on/off؛ module کی ticks؛ الگ View Profit / View Purchase Cost؛ role page defaults؛ ہر شخص کے manual/profit/retail caps اور one-bill override کا الگ Save؛ Add this person؛ Change it for the selected person؛ Save this recovery word؛ Photos |
| App words | ہر page/group کا دکان والا نام؛ Restore original words |
| Fields: Built-in | Form، Field، type، input، Required، Critical؛ Mark required؛ Mark optional… |
| Fields: Extra / Own fields | فارم، field name، stable key، جواب کی قسم، help، choices، required، ترتیب، put away/bring back |

### وہ باتیں جو خلاصے ہی میں یاد رکھیں

- **سٹاک ختم ہونے کی اطلاع صرف Counter کے لیے ہے؛ رسید/چٹ پر ایسی کوئی لائن نہیں۔**
- **صفر/کم سٹاک والا بل مالک یا مینیجر بھی نہیں بنا سکتا۔** پرانے منفی سٹاک کو گنتی سے درست کریں۔
- **Expense سٹاک نہیں بڑھاتا۔** سٹاک وصول کرنے کا صفحہ Purchases ہے۔
- **Cloud backup کا متبادل نہیں۔** مکمل حفاظتی نقل میں ڈیٹابیس اور تصاویر دونوں رکھیں۔
- **کوئی بھی سبق اصل دکان پر فرضی رقم یا فرضی سٹاک ڈال کر نہ آزمائیں۔** حقیقی ریکارڈ پر مشق مستقل مالی اندراج بنا سکتی ہے۔

## حصہ دوم — الگ، قدم بہ قدم تفصیل

یہاں سے عملی طریقے شروع ہیں۔ اس حصے کو مختصر خلاصے کے ساتھ نہ ملائیں۔ مثالوں کے نام/رقم صرف سمجھانے کے لیے ہیں، حقیقی کاروبار میں اصل چیزیں درج کریں۔

<a id="lesson-01"></a>

## سبق 01 — داخلہ اور بھولا ہوا پاس ورڈ

1. نصب ایپ کا Area11 آئیکن کھولیں۔ جو شخص کام کرے وہ **اپنا** نام اور Password or PIN لکھے، پھر Sign in۔ دوسرے کا نام استعمال کریں گے تو کارروائی اسی دوسرے شخص کے نام پر لکھی جائے گی۔
2. خالی user name مالک/admin کی لاگ اِن شناخت کے لیے استعمال ہو سکتا ہے؛ اپنے عملے کے لیے ان کا اصل اکاؤنٹ چنیں/لکھیں۔ نئی، غیر تبدیل شدہ تنصیب کی ابتدائی شناخت admin اور password area11 ہے؛ پہلے سے چلتی دکان پر اسے اپنا موجودہ پاس ورڈ سمجھ کر بار بار نہ آزمائیں۔
3. عملے کا PIN بھولے تو مالک اپنے اکاؤنٹ سے Settings → Staff & permissions میں نیا PIN دے۔ پرانا PIN پوچھنے کی ضرورت نہیں۔
4. مالک password بھولے تو **Forgot password?** کھولے؛ اس الگ ڈبے کے **Recovery word** میں اپنا مقررہ لفظ دے اور Sign in as owner دبائے۔ جب تک تبدیل نہیں کیا، فیکٹری لفظ area11 ہے۔ اسے password والے خانے کا متبادل نہ سمجھیں۔
5. داخلے کے بعد مالک اپنا password اور recovery word بدل دے۔ recovery word chat، تصویر یا رپورٹ میں نہ بھیجیں۔ غلط کوششوں پر کچھ دیر پابندی لگ سکتی ہے؛ مسلسل اندازے نہ لگائیں۔

Recovery سے داخلہ پرانی فروخت یا کھاتے کو reset نہیں کرتا۔ دونوں راز بھول جائیں تو اصل ڈیٹابیس مٹانا حل نہیں؛ بیک اپ محفوظ کرکے مدد لیں۔

<a id="lesson-02"></a>

## سبق 02 — صفحے کھولنا اور Dashboard

حصہ20: Customers & suppliersکےtabscurrentmodulegrantsسےبدلتےہیں؛CustomersکیاجازتسےCustomers/Recovery،SuppliersکیالگاجازتسےSupplierchapter۔ منعتابکاoldcontent/emptyhintنہیں؛جائزtabfallback،دونوںمنعہوںتوکوئیselectionنہیں۔ Nominal3secondactive-viewcheckفوریguaranteeنہیں۔

1. اوپر پہلے کام کا گروپ منتخب کریں: SELL، STOCK، CUSTOMERS & SUPPLIERS، MONEY یا SYSTEM۔ نیچے اسی گروپ کے اجازت والے صفحات آتے ہیں۔
2. Counter کی اجازت ہو تو آغاز وہاں ہوتا ہے؛ اجازت نہ ہو تو Dashboard۔ Dashboard کی الگ عام tab کے بجائے اوپر ایپ کے نام/wordmark سے وہاں جا سکتے ہیں۔
3. Dashboard پر آج کے بل، فروخت، منافع، ادھار، مصنوعات، کم سٹاک، منفی سٹاک، میعاد اور گلے کے کارڈ دیکھیں۔ متعلقہ کارڈ متعلقہ صفحہ کھول سکتا ہے؛ اجازت یا profit visibility کے سبب کچھ چیزیں چھپی ہو سکتی ہیں۔
4. گھنٹی سے Alerts؛ اپنے نام کے مینو سے Settings یا Sign out۔ **Sign out ایپ بند کرنا نہیں**، صرف اس شخص کی نشست ختم کرنا ہے۔ گلا خود بخود بند نہیں ہوتا۔
5. Refresh تازہ فہرست لاتا ہے۔ خالی صفحے کا پیغام پڑھیں: ریکارڈ نہ ہونا، فلٹر تنگ ہونا اور اجازت نہ ہونا الگ باتیں ہیں۔

**فیز4 حصہ5 — Dashboard کی read حد:** موجودہ active account اور Pos **یا** Reports اجازت درکار؛ دونوں لازم نہیں۔ explicit profit الگ global switch/Profit permission سے؛ account بند/role بدلے تو صحیح sign-in کریں۔ عام supplier payable/customer credit totals کی سابق policy نہیں بدلی؛ یہ مکمل session یا ہر مالی aggregate کی certification نہیں۔

<a id="lesson-03"></a>

## سبق 03 — صبح گلا کھولنا، رات گننا

حصہ21: currentCashreaderکےبغیرگلہکےnumbers/عملنہیں؛ByPersonaggregateکےلیےcurrentOwner/Managerبھی۔ Open/Close/Handoverکےwritesاکٹھےtransactionمیں؛readخرابہوتوunknownنہکہclosed۔ ActionSAVEDمگرrefresh/printerfailہوتودوبارہنہکریں،کھاتہ/گلہrefreshسےدیکھیں۔ Handoverreprint/shareسےپہلےfreshCash،slipآخریsavedin-memorysnapshotہے،freshDBreconstructionنہیں۔ [PT-41](POS-PHASE4-PART21-URDU.md)۔

حصہ20: handoverکاname-pickercurrentCashreaderکوصرفactiveدوسرےلوگدکھاتاہے،fullOwnerstaffdirectoryنہیں۔ نامچننےسےrecipientکیCashpostingاجازتثابتنہیں؛اصلHandovervalidationالگبرقرار۔ Pickerکیجانچسےگلہکےباقیfinancialreads/postingatomicitycertifiedنہیںہوتے۔

1. MONEY → Shift (galla) کھولیں۔ پہلے سے کھلا drawer ہو تو اسے دوبارہ نیا سمجھ کر نہ کھولیں؛ پچھلے شخص/مالک سے اصل نقدی گنوا کر بندش یا handover کریں۔
2. Cash you start with میں **واقعی موجود نقدی** لکھیں، فروخت کا ہدف نہیں۔ پچھلی گنتی سے فرق آئے تو صحیح وجہ لکھیں۔ Open the shift دبائیں۔
3. دن میں Cash bills، کھاتے کی نقد وصولی، refunds، supplier payments، expenses اور owner cash drawings سے Expected in the drawer بنتا ہے۔ Card/online رقم گلے میں نہیں پڑی، اسے نقدی میں شامل نہ گنیں۔
4. بندش پر نوٹ اور سکے گن کر Counted cash لکھیں۔ کم یا زیادہ ہو، چاہے ایک روپیہ، وجہ لکھیں؛ Close the shift۔ صرف حساب ملانے کے لیے فرضی Expense نہ ڈالیں۔
5. شخص بدل رہا ہو تو وصول کرنے والا staff چنیں، اصل counted cash دیں اور Hand over and print the slip۔ یہی رقم اگلے گلے کی opening float بنتی ہے؛ فرق پچھلی نشست پر محفوظ رہتا ہے۔ دونوں سے slip چیک/دستخط کروائیں۔
6. Slip دوبارہ Print یا WhatsApp کی جا سکتی ہے۔ History، Drawer differences اور عملے کے short/over الگ دیکھیں؛ زائد رقم کو کمی چھپانے کے لیے استعمال نہ کریں۔

گلا بند ہو تو بل نہیں بنے گا۔ Counter کے Open a galla now سے یہی صفحہ کھولیں؛ cash permission نہ ہو تو مجاز شخص کو بلائیں۔

<a id="lesson-04"></a>

## سبق 04 — Products میں صحیح دوا تلاش کرنا

1. STOCK → Products میں نام، نمک، طاقت، pack، barcode، brand، rack، category یا company لکھیں۔ کئی لفظ ہوں تو ہر لفظ ملنا ضروری ہے؛ بہت لمبی تلاش خالی آئے تو کم لفظ آزمائیں۔
2. طاقت کا الگ فلٹر مثلاً 50 ml، اور supplier کا فلٹر موجود ہے۔ Products والا supplier فلٹر preferred supplier یا اصل purchase history سے چیز دکھاتا ہے؛ اسے نئی خریداری یا payable نہ سمجھیں۔
3. primary category/company کی قطار والی جگہ سے بھی فلٹر لگ سکتا ہے۔ Clear filter سے اسے ہٹائیں۔ Stock بیچوں سے آتا ہے؛ اس grid میں عدد بدلنا سٹاک وصولی نہیں۔
4. New product سے الگ editor کھلے گا؛ پرانی چیز منتخب کرکے Edit product یا قطار پر ڈبل کلک۔ 50 ml اور 100 ml جیسے مختلف variants کو ایک ہی چیز سمجھ کر edit نہ کریں۔
5. پوری تصویر کے لیے Photo کھولیں؛ اصل معلومات کی تبدیلی editor میں کریں، پھر Products پر Refresh۔

**فیز4 حصہ7 — موجودہ cost/margin حد (D-110):** Products اجازت کے ساتھ **View Purchase Cost** اور main ON ہو تو catalog cost؛ manager عہدہ الگ شرط یا خودکار اجازت نہیں۔ margin کے لیے الگ **View Profit** اور main ON؛ **Margin (% retail)** میں خرید200/فروخت300 →33.3%، نہ50% markup۔ Profit-only پر rawcost hidden مگر درست projected margin؛ fake100% نہیں۔ cost hidden کو اصل صفر نہ سمجھیں؛ stored-cost editor نہیں کھلے گا۔ Counter/PR product reads cost-free، اصل sale floor Core اصل batch سے چیک کرتا ہے۔ product-based Purchase/SR/Write-off/Expense preparation cost اجازت/main ON کے بغیر انکار کرتی ہے؛ دیگر batch/direct routes کا rollout باقی۔ [PT-27](POS-PHASE4-PART7-URDU.md)۔

<a id="lesson-05"></a>

## سبق 05 — نئی چیز اور اس کے پیک/ریٹ

1. Name واضح لکھیں۔ Salt / formula / generic اور Brand میں موجود نام کا link منتخب کریں یا اصل متن دیں۔ New salt/New brand سے نیا نام؛ **Unlink** صرف رابطہ ہٹاتا ہے، لکھا متن رکھنے کے لیے ہے۔ ایک چیز کے دو ہجے بنانے سے بچیں۔
2. Barcode، Rack / shelf اور Room / almari درست درج کریں۔ پہلی category حساب کی primary ہے؛ اضافی tags صرف تلاش کے لیے۔ اضافی tags منتخب کرنے سے پہلے primary منتخب کریں۔ category خالی اور tags بھی خالی رہ سکتے ہیں۔
3. Company اور اختیاری Preferred supplier منتخب کریں۔ Clear preferred رابطہ ہٹاتا ہے، پرانی خریداری نہیں۔ Manage catalogs… سے فہرستیں؛ Manage units… سے اصل stock units۔
4. **Strength / volume صرف وصف ہے، سٹاک تبدیل کرنے کا فارمولا نہیں۔** مثال: 50 ml کی 10 injections کے ڈبے میں stock کی اصل گنتی 10 injections ہے، 500 ml نہیں۔ ایسی مختلف طاقت/pack کی الگ مصنوعات رکھیں۔
5. Pack size / variant label بھی صرف پہچان/تلاش کا متن ہے۔ اصل فارمولا الگ ہے: 1 box = کتنے inner packs، 1 inner pack = کتنے base units۔ مثال 2 strips فی box اور 10 tablets فی strip تو ایک box = 20 tablets۔
6. Cost، Retail، VIP اور Doctor ریٹ ایک base unit کے روپے لکھیں؛ Amount میں روپے ہیں۔ Counter منتخب box/strip کے لیے pack formula سے ریٹ نکالتا ہے۔ Reorder level **base units** میں ہے۔ غلط pack formula سے مقدار اور منتخب unit کا ریٹ دونوں متاثر ہوں گے؛ پہلی رسید سے پہلے لازماً ملائیں۔
7. Save product دبائیں۔ Built-in required خانے خالی ہوں تو ناموں والا popup آ سکتا ہے؛ Save anyway کا مطلب جان بوجھ کر نامکمل چیز محفوظ کرنا ہے۔ اس سے Counter کی missing-price/stock پابندی ختم نہیں ہوتی۔
8. Cancel / close کے وقت unsaved changes کا سوال آئے تو اپنی ضرورت کے مطابق واپس جائیں یا چھوڑیں۔ Remove selected (history kept) چیز کو عام استعمال سے الگ کرتا ہے، پرانی فروخت نہیں مٹاتا۔

### چار مثالوں سے تین خانوں کا فرق

| چیز | Strength / volume | Pack label صرف پہچان | Pack formula اور stock |
|---|---|---|---|
| 500 mg گولی | 500 mg | 10 × 10 tablets | base tablet، 10 strips/box × 10 tablets/strip = 100 گولیاں |
| 5 gram انجیکشن | 5 gram | 10 injections | base injection، 10 × 1 = 10 انجیکشن؛ gram کو stock کی تقسیم نہ بنائیں |
| 100 ml شربت، پوری بوتل بیچنی ہے | 100 ml | 100 ml bottle | base bottle، 1 × 1 = 1 بوتل؛ 100 الگ ml فروخت فرض نہ ہوں |
| 5 gram ساشے | 5 gram | 20 sachets | base sachet، 20 × 1 = 20 ساشے |

موجودہ فارم پر غیر گولی مصنوعات کے لیے بھی strips / box اور tablets / strip کے عمومی labels آتے ہیں؛ ان کا حساب ضرب کا ہے، ہر انجیکشن کے واقعی پٹی میں ہونے کا دعویٰ نہیں۔ اصل pack دیکھ کر formula رکھیں۔ 1 × 1 اور base injection کا مطلب ایک box میں ایک injection؛ strength 5 gram ہونے سے اسے پانچ یا زیادہ injections نہیں بناتا۔ Pack label میں صرف 1 لکھنا پہچان واضح نہیں کرتا؛ label کو بہتر لکھنے سے اصل formula خود نہیں بدلتا۔ اس سبق کے لیے موجودہ کاروباری product کی تعریف/سٹاک خودکار تبدیل نہیں کیے گئے۔

**اہم:** product بنانے سے عام سٹاک خود نہیں آتا۔ اصل مال Purchases سے وصول کریں؛ حقیقی جسمانی فرق Stock-take سے درست کریں۔ Photo نہ ہونا چیز محفوظ کرنے کو لازمی طور پر نہیں روکتا۔

<a id="lesson-06"></a>

## سبق 06 — فہرستیں اور Managed units

1. Product editor میں Manage catalogs… کھولیں۔ Categories، Companies، Salts اور Brands کی اپنی tabs ہیں۔ New، Name، متعلقہ Parent/Company اور Order لکھ کر Save کریں۔ چھوٹا Order اوپر دکھاتا ہے۔
2. Category میں زیادہ سے زیادہ دو درجے ہیں؛ parent فلٹر اس کے children بھی دکھا سکتا ہے۔ primary category ہی مالی رپورٹ کا حصہ طے کرتی ہے، tags کو رقم دوبارہ جمع کرنے کا ذریعہ نہ بنائیں۔
3. نمک/برانڈ کا نام بدلیں تو جڑی ہوئی چیزوں کے labels بدلتے ہیں؛ جس product پر صرف آزاد typed text ہے وہ خود دوبارہ نام نہیں پاتا۔
4. Put away کسی catalog entry کو عام انتخاب سے ہٹاتا اور موجودہ links کو متعلقہ اصول کے مطابق الگ کرتا ہے؛ مصنوعات/بل/بیچ حذف نہیں ہوتے۔ Bring back پر پرانے links خود بخود دوبارہ نہیں جڑتے۔ بعد میں متعلقہ چیز کھول کر رابطہ دیکھیں۔
5. Manage units… میں canonical نام اور ترتیب دیں۔ Whole stock quantities only اصل گنتی کے لیے ہے، مثلاً injection ٹکڑوں میں نہیں بکتا۔ ml کی strength کو fraction of injection نہ سمجھیں۔
6. استعمال میں موجود unit کو put away نہیں کر سکتے۔ rename سے موجودہ labels بدل سکتے ہیں؛ صرف راستہ نکالنے کے لیے unit کا مطلب نہ بدلیں۔ catalog/unit میں تبدیلی مالک/مینیجر کرے۔

<a id="lesson-07"></a>

## سبق 07 — تصاویر، بارکوڈ اور چیز کی تاریخ

1. Photos of this medicine…، فہرست کی تصویر، یا کسٹمر/سپلائر/staff کے Photos… سے gallery کھولیں۔ Add a photo… اصل فائل کی نقل محفوظ کرتا ہے۔ Take a photo (camera) Windows Camera کھولتا ہے؛ تصویر محفوظ کرکے واپس Add a photo سے منتخب کریں۔
⏳ **Add میں خرابی:** “unconfirmed” یا “SAVED … refresh failed” ہو تو اسی تصویر کو پھر نہ چنیں۔ پہلے اصل record،gallery اورphotos folder ملائیں؛unknown copy خود سے delete نہ کریں۔ Refresh سے Add guard نہیں کھلتا؛جانچ کے بعد window بند کرکے دوبارہ کھولیں۔ یہ صرف موجودہ window کا guard ہے،بجلی بند/restart/دوسرےPC کی ضمانت نہیں۔

2. صحیح active تصویر منتخب کرکے Set as cover؛ Open full size سے بڑی دیکھیں۔ Put away صرف چھپاتا ہے، فائل/تاریخ مٹاتا نہیں؛ Bring back سے واپس۔ Missing file کا مطلب فہرست میں ریکارڈ ہے مگر تصویر کی فائل موجود نہیں۔
3. Barcodes of this medicine… میں code سکین کریں اور Printed on میں **صحیح pack** (box/strip/base وغیرہ) چنیں۔ Add this barcode۔ ایک box code کی beep ایک box ڈالے گی، ایک tablet نہیں۔
4. غلط code کو Put away، درست کو فعال کریں۔ صرف مالک/مینیجر یہ تعریفیں بدلیں۔ قیمت بدلنے کے لیے barcode دوبارہ بنانے کی ضرورت نہیں۔
5. History of this medicine… سے کب، کس سے خریدا، کس کو بیچا اور کیا ریٹ تھا دیکھیں۔ یہ دیکھنے کا صفحہ ہے؛ پرانی تجارت کو یہاں edit نہ سمجھیں۔
6. تصاویر سمیت نقل کے لیے Backup میں Include the photos رکھیں۔ Cloud اور عام Excel export تصاویر نہیں لے جاتے۔

**Product history میں منافع:** اجازت/global switch بند ہو تو explicit profit screen/export میں hidden اور Core کے returned sale-profit fields بھی mask؛ اصل sale/item snapshots unchanged۔ خرید کے الگ rows/cost-bearing دوسرے راستے ابھی مکمل privacy audit سے پاس نہیں؛ اسے ہر inference کے خلاف حفاظت نہ سمجھیں۔

**فیز4 حصہ7 — History کی خرید رقم (D-110، پرانا management-only اصول بدل گیا):** متعلقہ Products/History page grant + **View Purchase Cost** + main ON پر خرید Rate، Total اور Bought مالی خلاصہ؛ کسی بھی role کے لیے۔ cost منع ہو تو تینوں raw/labels mask؛ quantity/date/supplier برقرار۔ print/WhatsApp/PDF preparation یہی labels، CSV دونوں purchase money cells خالی۔ صرف Profit/Purchases/Suppliers grant cost کا بدل نہیں۔ واضح sale-profit الگ View Profit/main policy پر؛ مجاز profit سے cost حساب کرکے نکل سکتی ہے، مطلق secrecy نہیں۔ [PT-27](POS-PHASE4-PART7-URDU.md)۔

**حصہ13 کی موجودہ شرط:** نئی خرید Save کرنے سے پہلے Core میں current stored Purchases+View Purchase Cost+main ON دوبارہ چیک ہوتا ہے۔ Cost/main منع ہو تو قابلِ Save masked0 فارم نہیں،انکار ہے۔ کھلے unsaved فارم کی line/cost/paid اصل حالت محفوظ رہتی ہے مگر اگلی action/timer check پر رقم hidden/blank،Save بند؛بحالی پر review کے بعد خود Save کریں۔ پہلے سے محفوظ invoice کی hidden-aware copy الگ ہے۔ [PT-33](POS-PHASE4-PART13-URDU.md)۔

**حصہ24 — galleryکیعملیمشق:** CustomerکیPhotosکھولیں،صرفالگtestcopyمیںgrantواپسلیں؛اگلےcommand/nominal3secondcheckپرpicture/selection/thumbnailrows/recordtitle/countصافاورactionsمنع۔ Regrantپرتازہread؛failedreadمصدقہ“no photos”نہیں۔ Open/fullpreviewسےپہلےphotoIDسےfreshPathOf،لیکنپہلےکھلاWindowsviewerواپسlockنہیںہوتا۔ Photochangeکےبعدrefreshfailتو **SAVED،actionدوبارہنہکریں**۔ ActiveparentکیپرانیAddشرط،inactiveparentکیhistoricalread/Hide/Restoreبرقرار۔ [PT-44](POS-PHASE4-PART24-URDU.md)۔ حقیقیcamera/codecs/Windowsجانچابھیباقی؛extension/sizecheckاصلimagevalidationنہیں۔

<a id="lesson-08"></a>

## سبق 08 — Purchases میں حقیقی ڈیلیوری وصول کرنا

1. STOCK → Purchases میں supplier منتخب کریں؛ نہ ہو تو New supplier، اصل نام/فون/agency اور متعلقہ extra fields لکھ کر Add supplier۔
2. صحیح product variant تلاش کریں، Unit اور Quantity منتخب کریں۔ کاغذ والا Batch number درج کریں۔ اس فارم کے Cost اور Retail **ایک base unit** کے روپے ہیں، منتخب box/strip کے پورے pack کے نہیں؛ Quantity/Unit سے تعداد base units میں تبدیل ہو کر cost سے ضرب ہوتی ہے۔ مثال: 20 tablets کے ایک box کی کل خرید 200 روپے ہو تو Cost میں فی tablet 10 روپے، Quantity میں 1 اور Unit میں box ہوگا؛ پورے box کے 200 کو Cost میں ڈالیں گے تو کل 4000 بن سکتا ہے۔ لائن ڈالنے کے بعد total سے اصل invoice لازماً ملائیں۔
3. Expiry printed strip سے لکھیں: 2027-06، 06/27 یا 2027-06-30؛ month/year اور calendar بھی موجود ہیں۔ خالی میعاد کا مطلب no expiry ہے۔ **اس فارم میں غلط تاریخ لکھنے پر Add line رکتی ہے؛ پیغام پڑھ کر اصل printed تاریخ درست کریں۔** بغیر میعاد کی لائن کو خودکار طور پر درست تاریخ لگ جانے کے برابر نہ سمجھیں؛ save کے بعد status بھی پڑھیں۔
4. مفت bonus ہو تو Sample / bonus line منتخب کریں؛ تعداد الگ درج کریں، cost صفر ہوتا ہے۔ خریدی ہوئی اور bonus تعداد کو دو مرتبہ paid stock نہ لکھیں۔ Add line سے delivery lines میں ڈالیں؛ غلط آخری لائن Remove last۔
5. Purchase total supplier کے اصل bill سے ملائیں۔ Paid now صرف ابھی دی ہوئی رقم ہے؛ باقی payable بنے گا۔ Paid how میں cash، online یا card کا **حقیقی** طریقہ دیں؛ cash گلے سے نکلتا ہے۔
6. ایک ڈیلیوری میں دو طریقے ہوں تو اس فارم میں ایک ہی Paid how ہے: مثلاً 1000 کی خریداری، 300 cash اور 200 online ادا ہوئے—Paid now میں 300، cash سے purchase محفوظ کریں، پھر Suppliers میں اسی supplier کو Pay کے ذریعے 200 online الگ درج کریں۔ باقی 500 واجب الادا رہے گا۔ 500 کو cash لکھنا گلے کو غلط کرے گا۔
7. Save purchase (PINV) کے بعد stock، payable اور ادا شدہ رقم دیکھیں۔ Save as draft ابھی وصولی پوسٹ نہیں کرتا۔ WhatsApp last invoice… صرف ابھی محفوظ invoice کے لیے ہے۔

Supplier statement پر پہلے سے ادا شدہ رقم دوبارہ Pay نہ کریں۔ واقعی محفوظ ہوئی delivery کو refresh کے بعد دوبارہ نئے bill کے طور پر نہ لکھیں۔

**حصہ9 — نئی purchase invoice preparation:** View Purchase Cost + main ON سے رقم؛صرف Purchases/Profit یا manager عہدہ کافی نہیں۔ منع ہو تو line amounts،Total،Paid،Due سب hidden،supplier/reference/quantity/batch/date برقرار۔ Print/WhatsApp کے لیے نیا document بنتے وقت موجودہ active account/Purchases grant چیک ہوتا ہے؛پرانا PDF واپس نہیں چھپتا۔ **Purchase entry/save result/draft ابھی اس مکمل cost rollout میں شامل نہیں۔** [محفوظ PT-29](POS-PHASE4-PART9-URDU.md)۔

<a id="lesson-09"></a>

## سبق 09 — Counter پر بل بنانا

حصہ21: Counterگلہکےmonetarytotalsنہیںپڑھتا،صرفcurrentCash/Posاجازتسےstatus۔ Stateunknownہوتو“refreshکریں”کےساتھبلblock،غلط“گلہبند”دعویٰنہیں۔ اصلSaleServiceکیopen-drawergateبرقرار۔

1. گلا کھلا ہو؛ Counter کے اوپر دوا تلاش کریں یا barcode سکین کریں۔ خالی search پر پوری catalog فہرست نہیں آئے گی؛ نام لکھیں تو Cart کے ساتھ مستقل چھوٹے پینل میں نتیجوں کے cards آئیں گے؛ ہر card میں تصویر/نام، طاقت، pack، stock، retail اور salt/rack دیکھیں۔ صحیح طاقت/pack منتخب کرکے Unit، Quantity اور Add to cart؛ کامیاب add پر نتائج اور search صاف، scanner focus واپس، مگر پینل کی جگہ قائم رہے گی۔ Esc یا Hide صرف matches چھپاتا ہے؛ query بدلیں تو دوبارہ آئیں گے۔ name کے Enter سے guessed medicine خود نہیں ڈلتی، درست barcode Enter اپنی pack unit کے مطابق کام کرتا ہے۔
2. Cart میں Item، Unit، Qty اور Rate Rs دیکھیں۔ Rate اسی لائن کی unit کا روپے والا ریٹ ہے۔ غلط چیز Remove؛ پورا غیر محفوظ cart ختم کرنا ہو تو Clear۔
3. واک اِن فروخت کے لیے کھاتہ درکار نہ ہو تو customer ضروری نہیں؛ **credit کے لیے درست customer** منتخب کریں۔ New customer… سے نام/فون شامل کرنے پر cart اپنی جگہ رہتا ہے۔ نسخے کا Doctor اختیاری الگ انتخاب ہے۔
4. Cart row پر double-click یا منتخب row پر Enter سے **Item price / discount** کھولیں۔ mode **normal** (bill discount)، **manual** (آخری منتخب-unit ریٹ)، یا **discount** (اس item کا فیصد، profit/retail اپنی پسند)۔ اجازت ہر شخص کی مالک مقرر کرتا ہے؛ صرف manager role کافی نہیں۔ manual یا الگ discount والی line پر bill% دوبارہ نہیں، خواہ ریٹ بڑھایا ہو۔ Apply پہلے Core quote/حد/cost چیک کرتا ہے؛ Cancel نافذ نہیں؛ Reset to normal دوبارہ bill discount میں۔ Cart کی Pricing column حالت بتاتی ہے؛ grid کا عام rate cell جان بوجھ کر read-only، اصل editor یہ owned window ہے۔
5. **Bill Discount % کے اوپر profit/retail منتخب دکھتا ہے۔** profit میں actual FEFO batch cost اور فروخت کے فرق سے فیصد؛ retail میں پوری فروخت سے۔ مثال cost200/sale300/10%: profit290، retail270۔ normal lines ہی bill% میں؛ ہر لائن کے اپنے manual/profit/retail فیصلے الگ۔ نئی عام line موجودہ bill% لے گی۔ ہر شخص کے تین caps اور edit/override اجازتیں Settings → Staff میں مالک Save کرتا ہے۔ خرید سے نیچے final sale کسی role کو نہیں، زیادہ رعایت انکار (خاموش کم فیصد نہیں)۔ Loyalty کے points بعد میں اپنی caps/cost کے ساتھ؛ Rs10 rounding فعال ہو تو الگ round-off بھی دیکھیں۔ **Extra Discount اس پورے حساب کے بعد الگ؛ نیچے قدم دیکھیں**۔
6. Payment میں حقیقی طریقہ چنیں۔ Split میں دوسری method اور اس کا Amount، باقی cash؛ cash/credit حصے سکرین پر ملائیں۔ Cash received وہ نقدی ہے جو واقعی ہاتھ میں آئی؛ Change واپس کی جانے والی نقدی ہے، فروخت کا منافع نہیں۔
7. Subtotal، Discount، TOTAL، Cash part، On credit اور Change دیکھیں۔ v0.78.0 میں Customer، Payment، Discount %، Total، Cash received، Give back اور Complete sale مستقل نیچے ہیں۔ Cart مرکزی جگہ کا بڑا حصہ لیتا ہے؛ search pane اس کے ساتھ اپنی جگہ رکھتا ہے۔ Extra Discount/Doctor/points/extra answers کے لیے More / split، جبکہ نئی customer entry کے لیے New customer دبائیں۔ Split چنیں تو اس کی detail خود کھلے گی۔ Complete sale یا اصل F4 شارٹ کٹ استعمال کریں؛ rate edit ہو تو F4 پہلے اس edit کو مکمل کرتا ہے۔ error آئے تو بل محفوظ سمجھ کر رقم/مال دوبارہ نہ چلائیں؛ پہلے error اور Bills میں حالت دیکھیں۔
8. retail بنیاد پر bill% 5 سے زیادہ ہو تو **save سے پہلے ہر bill پر** تصدیق۔ No پر محفوظ نہیں، Yes سے user cap یا cost پابندی نہیں ٹوٹتی۔ کامیابی پر invoice/status ملے گا۔ Settings → Sales rules میں success popup آن ہو تو الگ درمیان کی کھڑکی کھلے گی: ✓، invoice، بل، Cash received اور بہت بڑا GIVE THIS CASH BACK۔ مثال: بل 270، نقد 500، واپس 230 روپے۔ Done / Enter / × صرف کھڑکی بند کرتے ہیں؛ دوبارہ فروخت یا پرنٹ نہیں کرتے۔ اس کے بعد اگلا customer شروع کریں۔ Auto-print الگ اختیار ہے؛ پرنٹر کو بھیجنا کاغذ واقعی نکلنے کا ثبوت نہیں۔ Print last bill again صرف نقل نکالتا ہے۔ Cash received خالی ہو تو پرانے exact-cash قاعدے کے مطابق رقم فرض ہوتی ہے اور popup صاف لکھتا ہے exact amount assumed؛ online/card/credit کے لیے cash صفر ہوتا ہے۔

**مثال:** 300 روپے کا cash bill، وصول 500 تو Change 200۔ credit کی رقم کو نقد وصولی میں گننے سے گلا غلط ہو گا۔

### فیز1 — ماؤس کے بغیر Counter کیسے چلائیں؟

**یہ keyboard سہولت فیز1 میں آئی؛ فیز2 میں item editor اور نیا حساب، فیز3 میں الگ Extra Discount اور منظورشدہ B واپسی شامل؛ Windows جانچ باقی ہے۔**

1. گلا کھلا ہو اور Counter کھولیں؛ سرچ میں کرسر آنا چاہیے۔ نام لکھیں؛ خالی سرچ پر تمام دوائیں نہیں آئیں گی۔ صرف نام لکھ کر Enter سے اندازے والی دوا Add نہیں ہوتی۔ اصل barcode لکھ/سکین کرکے سرچ کے اندر Enter دیں تو اسی code کا موجودہ scan راستہ چلتا ہے۔
2. نتیجے منتخب کرنے کے لیے سرچ میں **↓** دبائیں۔ فوکس نتیجے کے خانے پر آئے گا، تصویر کے بٹن پر نہیں۔ **↑↓** سے دوا/طاقت/pack چنیں؛ **Enter** سے وہی Add ہوگی۔ اسی row پر **double-click** بھی Add کرتا ہے۔ تصویر والا بٹن صرف gallery کھولتا ہے؛ خالی جگہ/heading پر double-click سے Add نہیں۔
3. کامیاب Add پر سرچ صاف اور فوکس واپس؛ اگلی دوا تلاش کریں۔ Quantity غلط ہو تو پیغام پڑھیں؛ انکار کو کامیاب Add نہ سمجھیں۔ scanner عام keyboard جیسا لکھتا ہے؛ کسی دوسرے خانے میں کرسر ہو تو پہلے سرچ میں واپس آئیں، ہر input سے scan خود پہچاننے کا وعدہ نہیں۔
4. **Tab** سے سرچ → Unit → Quantity → Add جیسے دستیاب controls میں آگے، **Shift+Tab** سے پیچھے۔ Unit خانے میں **↑↓** سے unit/strip/box منتخب کریں؛ متن یا رقم میں **←→** عام حروف کا کرسر چلائیں۔
5. **Shift+↓ یا Shift+→** اگلے حصے اور **Shift+↑ یا Shift+←** پچھلے حصے میں لے جاتے ہیں۔ ترتیب: **سرچ/Unit/Quantity → سرچ نتائج → Cart → کھلی More details یا Parked carts → Customer/Payment/Discount → Cash/Complete sale → نیچے کے actions**۔ بند حصے اور خالی/چھپے نتائج خود skip ہوتے ہیں۔ آخری حصے سے آگے دوبارہ شروع نہیں؛ وہیں رکیں گے۔ Shift+تیر اب section shortcut ہے؛ پورا متن منتخب کرنے کے لیے **Ctrl+A** استعمال کریں۔
6. حصے کے اندر عام تیر متعلقہ list/grid/خانے کا کام کرتے ہیں۔ نیچے action buttons پر تیر اسی حصے میں پڑوسی بٹن تک، **Enter** اس بٹن کا کام؛ کی بورڈ سے منتخب action نمایاں رنگ میں دکھتا ہے۔ Tab مکمل متبادل ہے۔ Print automatically checkbox پر Space یا Enter سے حالت بدلے؛ خود Complete sale نہیں۔
7. **Esc** کھلی Unit/Customer/Payment فہرست کو پہلے بند کرے۔ سرچ نتائج میں ہوں تو نتائج چھپنے پر فوکس سرچ میں واپس؛ query بدلیں تو دوبارہ matches۔ معمول کے دوسرے input میں رقم/کسٹمر لکھنے سے فوکس زبردستی سرچ میں واپس نہیں آئے گا۔
8. Refresh اب نیچے actions میں ہے۔ More / split یا Parked carts کھولیں تو Shift+تیر سے اس دکھائی دینے والے حصے تک جائیں یا Tab سے controls چنیں؛ بند کریں تو اضافی حصہ navigation میں شامل نہیں۔
9. **F4** اصل Complete sale ہی ہے اور بل محفوظ کر سکتا ہے؛ محض keyboard آزمانے کے لیے اسے نہ دبائیں۔ Cart پر Enter/double-click اب **Item price / discount** کھولتا ہے؛ Apply صرف cart بدلتا ہے، bill محفوظ نہیں کرتا۔

### فیز1 کی محفوظ مشق

بغیر بل محفوظ کیے 5–10 دوائیں Add کریں، Unit/Quantity/Cart/Customer/Cash/actions میں جائیں؛ رقم لکھ کر دیکھیں کہ فوکس نہیں چھنتا۔ Clear سے غیر محفوظ cart ختم کریں۔ حقیقی shop میں فرضی Complete sale/F4 نہ کریں۔ Windows پر چھوٹی سکرین، DPI، اصلی scanner اور keyboard کی تصدیق باقی؛ مسئلہ ہو تو version، بٹن، کیے گئے قدم اور تصویر دیں۔


### بل کے آخر میں الگ Extra Discount — پوری مثال

1. مالک پہلے Settings → Staff & permissions → شخص → **This person's pricing limits** میں **May give separate Extra Discount** اور روپے کی حد دے، پھر **Save this person's pricing limits**۔ خود مالک کے لیے بھی یہ اجازت مقرر کرنا ہوگی۔ عام profit/retail% caps الگ رہیں گی۔
2. پوری cart، rate/item discounts، bill%، points/rounding کا حساب پہلے ہونے دیں۔ فرض کریں **Before Extra Discount1120**۔ سب lines manual/fixed ہوں تب بھی الگ Extra چل سکتا ہے؛ یہ ان کے ریٹ بدلنے کا راستہ نہیں۔
3. **More / split** میں **Extra Discount (Rs for the whole bill)** میں20 لکھیں۔ صرف لکھنے سے لاگو نہیں ہوگا؛ **Apply Extra Discount** دبائیں۔
4. owned popup میں **1120، Extra20، final1100** دیکھ کر Yes؛ No سے سابقہ applied رقم برقرار۔ footer میں Extra−20 اور Total1100 دیکھیں۔ cap20 ہو تو30 سے انکار، خاموش20 نہیں۔ دوبارہ20 Apply کرنے سے20 ہی،40 نہیں؛ یہ پوری رقم replace ہے۔
5. **Cash received1100** دیں، صرف حقیقی فروخت پر Complete sale/F4۔ رسید، reprint اور Bills میں pre-extra1120/Extra20/final1100 الگ، due0۔ Extra کے بغیر کم cash لینے سے فرق خود سے معاف نہیں ہوگا؛ مناسب customer/credit منتخب کریں یا پوری نقدی وصول کریں۔
6. Apply/Remove سے product rate/quantity/پہلے کی line discount/bill% نہیں بدلتے۔ **Remove Extra Discount** صرف یہ رقم ہٹاتا ہے۔ اگر textbox کی نئی رقم Apply نہیں کی تو Complete sale/Park/Save draft روکے گا؛ پہلے Apply یا Remove۔ Clear/Post/Park سے اگلی live cartExtra0، اسی parked/draft کو کھولنے پر اس کی Extra بحال اور موجودہ cap دوبارہ چیک۔
7. final payable actual allocated خرید کی مجموعی لاگت سے کم نہیں ہوسکتا، مالک بھی نہیں؛ پہلے ہر line کا اپنا cost guard بھی برقرار۔ لہٰذا cap20 کا مطلب ہر bill پر لازماً20 دینے کی اجازت نہیں۔
8. بند کرنا ہو تو جاری cart میں Remove؛ مالک اس شخص کا Extra tick بند کرکے الگ Save کرے۔ محفوظ پرانے bills unchanged۔ پرانی exe/backup اوپر ڈالنا بندش کا طریقہ نہیں۔ محفوظ عملی مشق، errors اور B واپسی [فیز3 نامہ](POS-PHASE3-TEST-URDU.md) میں۔

<a id="lesson-10"></a>

## سبق 10 — Not in stock اور بغیر قیمت کی وارننگ

1. پہلی بار cart میں stock کم/ختم یا catalog retail missing ہونے پر Counter availability popup پڑھیں، OK کریں۔ **اسی live bill میں دوبارہ حساب/typing پر popup بار بار نہیں آئے گا۔** یہ وارننگ غائب ہونے سے رکاوٹ ختم نہیں ہوتی۔
2. stock کم ہو تو مقدار کم کریں، غلط unit ٹھیک کریں یا اصل stock وصول کریں۔ **مالک/مینیجر بھی صفر یا ناکافی stock کا بل نہیں بنا سکتے۔** ایک ہی product کی الگ لائنیں مجموعی stock استعمال کرتی ہیں؛ expired-only stock بھی sellable نہیں۔
3. stock موجود لیکن retail درج نہیں تو مالک کی rate-edit اجازت والا شخص item editor کے manual mode میں مثبت درست فروخت ریٹ دے۔ reference قیمت نہ ہو تو فیصد کمی نہیں ناپ سکتے، مگر actual batch cost/stock پابندی برقرار۔ اجازت نہ ہو تو خود ریٹ لکھ کر رکاوٹ عبور نہیں؛ مستقل حل product میں صحیح Retail محفوظ کرنا ہے۔
4. Clear، کامیاب sale، cart park یا دوسرے draft/cart کو load کرنے پر نئے live bill کی warning یاد دوبارہ شروع ہوتی ہے؛ آخری چیز ہٹانے سے cart خالی ہو تب بھی اگلی شروعات نئی ہے۔
5. **رسید/چٹ پر availability کا لفظ، اضافی سطر یا اوپر کا نشان نہیں آتا۔** قیمت کو popup میں دوبارہ نہیں دکھایا گیا؛ معمول کی Rate والی جگہ دیکھیں۔ سٹاک نہ ہونے کا ناکام bill کوئی فروخت نہیں۔

**Purchase drafts کی تازہ حد:** Summary list/discard کے لیے current Purchases،لیکن raw draft Open/Save/update/Post کے لیے Cost+main بھی لازم؛یہ sale drafts کا نیا grant نہیں۔ Denied open میں editable صفر payload نہیں ملتا۔ اجازت واپس ہو تو اصل محفوظ رقم کھلتی ہے؛پرانے returned object/file کی recall نہیں۔ [PT-33](POS-PHASE4-PART13-URDU.md)۔

<a id="lesson-11"></a>

## سبق 11 — Park، مشترکہ drafts اور ادھورا کام

1. ⏳ گاہک انتظار کر رہا ہو تو نیچے مختصر toolbar کا Park cart یا Save draft استعمال کریں۔ یہ پرانے Park this cart for later / Save as draft کے وہی commands ہیں؛ Open drafts اور Reprint بھی اسی مستقل پٹی میں ہیں۔ Parked carts کے بٹن سے فہرست کھولیں؛ فہرست ہر وقت Cart کی جگہ نہیں لیتی۔ اس وقت کوئی sale/stock deduction/رقم وصولی پوسٹ نہیں ہوتی۔
2. ⏳ Parked carts کا Bring back یا Open drafts → Open on the page سے کام واپس لائیں۔ واپس لانے پر stock/ریٹ/قواعد دوبارہ چیک ہوں گے؛ park کرنے سے stock reserve ہونے کی ضمانت نہیں۔
3. ⏳ purchase کا بھی Save as draft ہے؛ Open drafts سے اس کی delivery واپس لیں۔ draft مشترکہ ہے: متعلقہ اجازت والا شخص اسے کھول/پوسٹ کر سکتا ہے۔ اپنے ادھورے کام کو راز یا ذاتی lock نہ سمجھیں۔
4. ⏳ Post now حتمی document بنانے کی کوشش ہے، صرف کھولنا نہیں۔ Discard/Throw away draft ترک کرتا ہے، پہلے سے posted document کو الٹتا نہیں۔
5. ⏳ Post was interrupted جیسا پیغام ہو تو پہلے اصل Bills/purchase record/status چیک کریں؛ کہیں document بن چکا ہو تو دوبارہ post نہ کریں۔ draft history محفوظ ہونے کا مطلب یہ نہیں کہ editor میں ابھی کی ہر غیر محفوظ typing بھی محفوظ ہے۔
6. ⏳ Bring back اب اسی transaction میں recovery draft بھی محفوظ کرتا ہے؛Counter اسی ID سے جڑا ہے۔ App بند ہوجائے تو Open drafts میں `held-…` کھولیں،نیا غیرمنسلک bill نہ بنائیں۔ Save اسی draft پر؛دوبارہ Park کا انکار جان بوجھ کر ہے۔
7. ⏳ Reconstruction/missing-product error آئے تو partial cart کو Save/Complete نہ کریں؛Clear کی confirmation saved draft باقی رکھتی ہے،صرف counter چھوڑتی ہے۔ اصل محفوظ کام Drafts میں،بعد کی unsaved typing نہیں۔
8. ⏳ صرف validation/confirmed no-commit پر draft دوبارہ کھلتا ہے۔ نامعلوم نتیجے پر verification marker رہتا ہے؛اگر **SAVED / finalization failed** ملے تو document بن چکا ہے،دوبارہ post نہ کریں۔ Marker کو verification کے بغیر discard کرکےنئیcopy نہ بنائیں۔

9. ⏳ عامCounterCompleteیاPurchaseSaveسےپہلےmanualSaveDraftضروری نہیں؛ابخودکارsharedDraftسےوہیکامpostہوتاہے۔ FailہوتوOpen draftsمیںsavedکام/markerچیککریں،دوسریcopyنہیں۔ پہلیdraftsavingہینامعلومہوتوdocumentpostشروعنہیںہوا؛openrowملسکتیہے،markerضروری نہیں۔
10. ⏳ عامکھلاdraftبھیParkنہیںہوسکتا؛اسیdraftکوSave/Completeکریں۔ HoldکاکامیابجوابملےمگرCounterصافنہہوتوPARKEDwarningکےبعددوسریsale/parkنہیں؛parkedفہرستverifyپھرصرفcounterClear۔

<a id="lesson-12"></a>

## سبق 12 — Bills، دوبارہ رسید اور غلط وقت

1. SELL → Bills میں Today، 7 days، 30 days یا From/To منتخب کریں۔ bill number، customer name/phone اور Paid by فلٹر؛ Search۔ Clear فلٹر صاف کرتا ہے۔
2. bill منتخب کریں؛ اشیا، مقدار، subtotal، discount، **Before Extra Discount، Extra Discount، final Total**، tax/round-off اگر ہوں، Paid، Due، returns اور Money that came in دیکھیں۔ انتخاب پڑھنے سے نیا bill نہیں بنتا۔
3. Print this bill again ایک نقل؛ Print selected… متعدد منتخب بل؛ Preview اصل کاغذ کی عبارت/چوڑائی دکھاتا ہے؛ WhatsApp… کا PDF خود attach کرنا ہوتا ہے۔ returns والے حصے سے اسی return کی رسید نکال سکتے ہیں۔
4. مالک کے لیے Fix wrong time… ہے۔ پہلے اصل Windows clock درست رکھیں اور time server کا جواب دستیاب ہو۔ From/To دے کر List bills؛ Wrong time اور Correct time کی پیش کش غور سے ملائیں۔
5. Fix every bill above فہرست والے bills اور متعلقہ زمانی ریکارڈ ایک ساتھ درست کرنے کی کارروائی ہے؛ انتخاب پڑھے بغیر نہ دبائیں۔ اصل اور درست وقت کا ثبوت ریکارڈ میں محفوظ رہتا ہے۔ وقت بدلا ہونے سے تاریخ وار reports میں bill دوسری تاریخ پر آ سکتا ہے، رقم کی نئی فروخت نہیں بنتی۔
6. انٹرنیٹ/time source نہ ملنے پر batch وقت درستگی دستیاب نہیں سمجھی جائے؛ اندازے سے تاریخ یا ڈیٹابیس کی فائل خود نہ بدلیں۔

**فیز4 حصہ6 — وقت کی correction کی حفاظتی حد:** صرف پرانا owner label کافی نہیں؛ preview اور Apply موجودہ active مالک مانگتے ہیں۔ Apply موجودہ bills دوبارہ پڑھ کر owner/read/plan/date/payment/audit ایک ہی فوری transaction میں کرتا ہے؛ پہلے سے تیار preview permission نہیں۔ اصل created_at/رقم unchanged، same offset دوبارہ Apply سے مزید shift نہیں؛ غلط payment date/audit fault پر پوری کارروائی واپس ہونی چاہیے۔ یہ Windows/.NET concurrency PASS نہیں؛ **اصل shop کے bills کی تاریخ محض test کے لیے نہ بدلیں**۔ [محفوظ PT-26](POS-PHASE4-PART6-URDU.md)۔

<a id="lesson-13"></a>

## سبق 13 — اصل بل والی Returns اور Quarantine

1. SELL → Returns میں Bill number → Find bill۔ نمبر نہ ہو تو No bill number? Find it سے فون/نام/دوا/تاریخ تلاش کریں؛ صحیح bill کھولیں۔ یہ تلاش ہے، بل کے بغیر refund بنانے کا راستہ نہیں۔
2. Sold، Returned اور Can return دیکھیں۔ Coming back میں حقیقی واپس آئی مقدار لکھیں؛ فروخت سے زیادہ واپس نہیں ہو سکتی۔ Refund، Reason اور ضرورت کا Note پڑھیں/لکھیں۔
3. قابل فروخت صحیح مال Back on the shelf now؛ فیصلہ باقی یا مشکوک مال Quarantine۔ خراب/expired چیز فوراً shelf پر واپس نہ ڈالیں۔
4. Total refund، Cash out اور Khata reduced ملائیں؛ Take the return (RET)۔ credit والے bill میں پہلے ادھار کم ہو سکتا ہے؛ ہر refund لازماً پوری نقد ادائیگی نہیں۔ وصول شدہ رقم اور باقی مقدار کی حدیں لاگو ہیں۔
5. Quarantine tab میں مجاز شخص Q-number والی چیز چن کر Back in stock، Damaged / expired یا Sold on کا اصل نتیجہ درج کرے۔ ہر نتیجے کا وقت/شخص/نوٹ مستقل رہتا ہے۔
6. پہلے سے محفوظ return کو دوبارہ نیا return نہ لکھیں۔ رسید Bills کے returns حصے سے بھی مل سکتی ہے۔

### Extra والے بل کی جزوی واپسی — مالک کا منتخب طریقہ B

A560+B560=1120، Extra20، وصول1100۔ پہلے واپس آنے والی A کی اصل محفوظ رقم **560**؛ بعد میں B آئے تو صرف باقی **540**۔ یہ proportional550/550 نہیں، اور اصل sale-item rates/discounts نہیں بدلتے۔ Returns preview میں Extra اور باقی cap کی وجہ پڑھیں، گاہک کو پہلے سمجھائیں، پھر Take the return۔ دونوں ایک request میں ہوں تو بھی کل1100؛ cap کا فرق آخری درج return line سے کم ہوگا۔ واقعی وصول cash سے زائد نقد واپسی نہیں؛ credit کی صورت میں باقی کھاتہ کم ہوگا، فرضی cash نہیں۔ ایک بار واپس کی ہوئی quantity دوبارہ نہیں۔ الگ test data میں اس مثال کی مشق کریں؛ اصل دکان پر فرضی return نہ بنائیں۔

<a id="lesson-14"></a>

## سبق 14 — اصل بل ہاتھ میں نہ ہو: No bill (PR)

⏳ حصہ40: PRکےلیےcurrentReturnsaccountلازم؛storedinactive/rolechange/revokeپرپراناPRcode/read/write/receiptنہیں۔ Timer/actionپرpageاورunsavedtray/formصاف،accessواپسآئےتوRefreshاورنیاانتخاب۔ Pagefail→notloaded،نہconfirmedempty؛receiptlinefail→Buildfailure،نہheader-onlyslip۔ عامجائزReturnsreaderکوread/receipt،فیصلہcurrentOwner/Manager؛receiptکےپرانےprint/exportواپسآنےکادعوینہیں۔ P39unknown/unfinishedpostingguardpermissionregrantسےresetنہیں۔ اصلWindowsجانچباقی۔

⏳ حصہ39: Save/Attach/Cancel کا کامیاب جواب مل چکا ہو تو page/print error کے بعد وہی action دوبارہ نہیں۔ **SAVED** warning میںhistoryverifyکریں؛ادھوریصفائیہوتوpageدوبارہکھولنےسےپہلےنتیجہضروردیکھیں۔ Unknownمیںدوسریcopy/refundنہیں؛localflagrestartپرنہیںرہتا۔ کامیابیپرپراناtargetاورLoadکاخودکاراگلاانتخابصاف،bill/reasonدوبارہخودنہیںلگتے۔ Linkسےرقم/stockدوبارہنہیں،Cancelسےcash-inخودنہیں۔ CurrentPRreader/permission/receiptاوراصلWindowsثبوتباقی۔

1. No bill (PR) عام refund کا آسان متبادل نہیں؛ اس میں اصل bill کی فوری تصدیق نہیں ہوتی۔ product، حقیقی واپس مقدار اور Unit ڈالیں؛ customer name/phone، واقعی دی گئی رقم، وجہ/نوٹ لکھیں۔
2. Save and give the slip سے PR نمبر بنتا ہے، stock/مال اور refund کا الگ ریکارڈ بنتا ہے۔ slip گاہک کو دیں اور بعد میں اصل bill لانے کا طریقہ واضح کریں۔ غیر تصدیق شدہ رقم میں خاص احتیاط کریں۔
3. Waiting for a bill میں PR منتخب کریں؛ Print this slip نقل ہے۔ مالک/مینیجر Attach the real bill میں INV نمبر دیں، یا فون/نام/دوا/تاریخ سے Find bills کرکے Use the selected bill؛ پھر Attach this bill۔
4. اصل bill جوڑنے سے وہی stock اور refund دوسری بار نہیں لگتے۔ alert کا رنگ/حالت بدل سکتی ہے مگر مالک کو وجہ لکھ کر معاملہ بند کرنا پڑتا ہے۔
5. واقعی duplicate/غلط PR ہو تو مالک/مینیجر وجہ دے کر Cancel this entry and take the stock back۔ اگر مطلوبہ stock باقی نہیں تو cancellation رک سکتی ہے؛ یہ نیا منفی stock بنانے کا راستہ نہیں۔
6. Linked/cancelled entries ہمیشہ تاریخ میں رہتی ہیں؛ slip پھاڑنے سے اندراج ختم نہیں ہوتا۔

<a id="lesson-15"></a>

⏳ **حصہ42 / رقم کی احتیاط:** Receive/Collect/Pay کا **unconfirmed** یا **SAVED مگر refresh ناکام** نتیجہ آئے تو دوبارہ رقم نہ لکھیں۔ پہلے payment history،کھاتہ اور drawer verify؛Refresh/regrant guard نہیں کھولتے۔ Page دوبارہ کھولنا خودکار verification یا global duplicate protection نہیں۔ [PT-62](POS-PHASE4-PART42-URDU.md)۔

## سبق 15 — Customers، کھاتہ اور وصولی

1. CUSTOMERS & SUPPLIERS → Customers میں نام/فون تلاش؛ Only with udhaar صرف مقروض۔ + New سے الگ کسٹمر بنائیں۔ ایک فون کو دو کھاتوں میں بانٹنے سے بچیں۔
2. Edit میں name، phone، category اور مجاز شخص کے لیے Credit limit۔ **Balance کو براہ راست نئی رقم بنا کر نہیں لکھتے**؛ bill، receipt اور return سے کھاتہ چلتا ہے۔ limit صفر کا مطلب کوئی مقررہ حد نہیں، صفر ادھار کی اجازت نہیں۔
3. کسٹمر منتخب کرکے Khata book کی Debit/Udhaar، Paid اور Balance قطاریں پڑھیں۔ Lifetime اور Profit اجازت/visibility کے تابع ہیں۔
4. پیسے ہاتھ/بینک میں آ چکے ہوں تو Money received میں رقم، How میں صحیح طریقہ اور Note؛ Receive۔ ادھار سے زیادہ وصولی رد ہو سکتی ہے؛ اضافی customer advance کا فرضی اندراج یہاں نہ کریں۔
5. Statement پورا بیان؛ Month میں YYYY-MM دے کر Month statement؛ Receipt (last collection) آخری وصولی کی نقل۔ متعدد منتخب کسٹمر کے Print selected، Preview، WhatsApp statement یا WhatsApp nudge دستیاب ہیں۔
6. Photos شناخت میں مدد کے لیے ہیں۔ دکھائی گئی ادھار کی رقم بدلنے کے بجائے غلطی کی اصل entry تلاش کریں اور مجاز correction کا طریقہ اپنائیں۔

**فیز4 حصہ3 — کھاتے کی read اجازت:** Customers کی فہرست/منتخب account/full یا monthly statement/bulk اور PAY receipt اب Core میں بھی موجودہ active reader اور Customers permission چیک کرتے ہیں۔ متعلقہ global switch یا profit permission بند ہو تو explicit customer profit screen کے ساتھ returned list/page میں بھی mask؛ balance/loyalty/اصل history unchanged۔ stale account/role ہو تو صحیح active account سے دوبارہ sign in؛ پہلے سے دکھائی/کاپی/پرنٹ معلومات مٹنے کی ضمانت نہیں۔ یہ RecoveryService کے ہر الگ راستے کی مکمل certification نہیں۔

**حصہ18کیcurrentحد:** وصولیtransactionمیںموجودہCustomersاجازت؛account/WhatsAppprofileتازہPageسے۔ User/role/grantdenyپراگلےcheckمیںپراناaccount/receiptselectionصاف۔ CustomerبدلنےپرپچھلےCustomerکاReceiptbuttonreset۔ صرفProfit/mainبدلےتوprofitmask/تازہread،عامکھاتہاورلکھاہواeditformبلاوجہresetنہیں۔ Nominal3secondtimerفوریguaranteeنہیں۔ [PT-38](POS-PHASE4-PART18-URDU.md)۔

<a id="lesson-16"></a>

⏳ **حصہ42 / رقم کی احتیاط:** Receive/Collect/Pay کا **unconfirmed** یا **SAVED مگر refresh ناکام** نتیجہ آئے تو دوبارہ رقم نہ لکھیں۔ پہلے payment history،کھاتہ اور drawer verify؛Refresh/regrant guard نہیں کھولتے۔ Page دوبارہ کھولنا خودکار verification یا global duplicate protection نہیں۔ [PT-62](POS-PHASE4-PART42-URDU.md)۔

## سبق 16 — Udhaar recovery میں پرانا ادھار

حصہ19: اس صفحے پر filter/receive/reprint fresh Customers check کرتے ہیں؛permission loss یا report fail پرپرانا account/amount/last-slip/totalsصاف۔ Note/search/methodبلاوجہresetنہیں۔ وصولیsavedمگرrefreshfailتو**دوبارہReceiveنہکریں**؛refreshکرکےaccountدیکھیں۔ Recovery slip میںاصلpaymentکےcollectorکاstoredنام،printingreaderکانام نہیں۔ [محفوظمشقPT-39](POS-PHASE4-PART19-URDU.md)۔

1. Customers & suppliers کے recovery حصے میں TOTAL UDHAAR اور 0–30، 31–60، 61–90، 90+ دن والے خانے پڑھیں۔ یہ رقم کی عمر ہے، جرمانہ یا خودکار سود نہیں۔
2. Only over two months اور Find سے فہرست محدود کریں۔ Carried in، Last paid اور Oldest سے سمجھیں کہ واجب الادا کہاں سے ہے؛ بہت پرانا opening balance بھی الگ ہو سکتا ہے۔
3. کسٹمر چنیں، اصل Amount received اور Method لکھیں؛ Receive and print سے PAY نمبر اور رسید بنتی ہے۔ ادائیگی پرانے بقایا کے اصول کے مطابق لگتی ہے۔
4. Print slip again نئی وصولی نہیں۔ رقم غلط لکھ دی ہو تو اسے چپکے سے edit/delete کرنے کا راستہ نہ ڈھونڈیں؛ مالک اصل ریکارڈ دیکھ کر درست corrective entry کروائے۔

<a id="lesson-17"></a>

⏳ **حصہ42 / رقم کی احتیاط:** Receive/Collect/Pay کا **unconfirmed** یا **SAVED مگر refresh ناکام** نتیجہ آئے تو دوبارہ رقم نہ لکھیں۔ پہلے payment history،کھاتہ اور drawer verify؛Refresh/regrant guard نہیں کھولتے۔ Page دوبارہ کھولنا خودکار verification یا global duplicate protection نہیں۔ [PT-62](POS-PHASE4-PART42-URDU.md)۔

## سبق 17 — Suppliers، ادائیگی اور SR

1. Suppliers میں supplier منتخب کریں؛ Only with money to pay فلٹر صرف واجب الادا دکھاتا ہے۔ Account، Purchases، Credit notes اور Money paid سے payable کا سبب ملائیں۔
2. اصل ادائیگی کے لیے رقم، How اور Note دیں، Pay۔ موجودہ active account اور Suppliers اجازت والا شخص ادائیگی یا supplier return کر سکتا ہے؛صرف عہدہ کافی نہیں۔ purchase کے Paid now کو دوبارہ supplier Pay میں نہ دہرائیں۔
3. Statement، Print selected، Preview، WhatsApp statement یا WhatsApp note سے حساب بھیجیں۔ bank سے دی ہوئی رقم کو cash کہنا گلے کو غلط کرتا ہے۔
4. Return goods (SR) میں اصل product/batch چنیں، Quantity اور Unit، Add this batch to the tray۔ expired batch کا متعلقہ quick button بھی موجود ہے؛ tray، Value اور اصل مال ملائیں۔
5. Reason لکھیں۔ عام SR سے stock نکلتا اور payable credit note سے کم ہوتا ہے۔ **The supplier gave the money in cash** صرف تب tick کریں جب واقعی نقد واپس لیا—یہ credit note کے ساتھ دگنی رقم لینے کے لیے نہیں۔
6. Save the return (SR) سے پہلے رقم اور tray دیکھیں؛ منفی stock کی اجازت نہیں۔ واپسی کی cutoff یاد دہانی supplier کے معاہدے کی جگہ خود فیصلہ نہیں کرتی۔

**فیز4 حصہ3 — supplier read حد:** Suppliers module کے بغیر list/account/statement/bulk/expired-batch reads سے انکار، بشمول PDF/WhatsApp/print کی نئی preparation۔ مالک کی واضح Suppliers grant والا سابق جائز workflow برقرار؛ نئی owner-only blanket پابندی نہیں۔ next read پر account/role/module check ہوتا ہے؛ پہلے سے جائز حالت میں تیار preview/file واپس مٹانے یا catalog/دوسرے cost-bearing راستوں کی مکمل secrecy کا دعویٰ نہیں۔ عام cashier کو خودبخود Suppliers اجازت نہیں۔ [PT-23](POS-PHASE4-PART3-URDU.md) صرف محفوظ الگ data پر۔

**حصہ17 / D-114 — موجودہ Supplier حد:** صرفSuppliersgrantبھیaccount/statement/Pay/SRکےلیےکافی،mainOFFپرمالیaccountingاستثنا؛براہِراستitem/batchcostکیاجازتاسسےنہیںملتی۔ Expiredlistاورbatchpickerکیcost/valuationhiddenہوسکتیہے،مگرmanual/expiredquick-addcurrentbatch/quantity/packکےساتھquantity-onlytrayبناسکتےہیں۔ Trayvalue/aggregateunknownہوتوhidden،نہfake0؛Saveاصلservercostسے۔ Batchcost0ہوتوپراناproductcostfallbackبرقرار۔ Known0مجازقیمتسےالگپہچان۔ Currentaccount/moduleدونوںwritesکےtransactionمیںدوبارہچیک؛costکیpreviewاجازتreturnpostingکیشرطنہیں۔ Cost/mainبدلنےپرtrayquantitiesبرقرارمگرpreviewmasked،واپسknownpreviewکےلیےlineدوبارہaddکریں؛moduledenyپرtrayصاف،خودpostنہیں۔ [PT-37](POS-PHASE4-PART17-URDU.md)،[trace](POS-PHASE4-SUPPLIER-ACCOUNTING-REVIEW.md)۔ حصہ9/10کیknown-cost-onlytrayہدایتاسسےبدلگئی۔

<a id="lesson-18"></a>

## سبق 18 — Doctors & reps

1. کسی doctor کو Customers میں Category = Doctor کے ساتھ رکھیں؛ اسی سے prescription doctor کی فہرست بنتی ہے۔ doctor کا customer کھاتہ اور bill پر نسخہ لکھنے والے doctor کا انتخاب الگ مقصد رکھ سکتے ہیں۔
2. Doctors & reps کے متعلقہ tabs/حصے کھولیں۔ Counter پر اصل prescription doctor منتخب کریں؛ بعد میں اس کے Bills، Sales، Profit اور Last bill دیکھیں۔
3. کمپنی منتخب کرکے rep کا نام، فون، Visits on لکھیں؛ موجودہProductsاجازتوالاشخص Save the rep کرے؛صرفعہدہکافینہیں۔ company کی غلط entry پر کسی دوسری کمپنی کا نمائندہ نہ لگا دیں۔
4. Customer lifetime profit پوری تاریخ کے حساب کا اشارہ ہے، وصول شدہ نقدی نہیں۔ returns اور cost اثر ڈالتے ہیں، visibility بند ہو تو منافع چھپ سکتا ہے۔

**حصہ18 — منافع کی موجودہ حد:** Doctorsاورcustomerlifetimeprofitدونوںسےپہلےcurrentactive/same-role **Customers** reader؛explicitprofitکےلیےالگ **View Profit + mainON**۔ صرفProfitبغیرCustomersنہیں،صرفCost/managerعہدہنہیں؛جائزdelegatedstaffprofitدیکھسکتاہے۔ MainOFFپرOwnerبھیprofitنہدیکھے،عامDoctorssales/countsبرقرار۔ RepکےلیےProductsاورSaveکےtransactionمیںcurrentactor،Auditمیںstoredنام۔ Timerپرrelevantflagsبدلیںتوصرفمتاثرہحصےreload/clear؛profitبدلنےسےrepکالکھاہوافارمنہیںمٹتا۔ مجازprofitسےcostاخذہوسکتیہے،مطلقsecrecyنہیں۔ [PT-38](POS-PHASE4-PART18-URDU.md)۔

<a id="lesson-19"></a>

## سبق 19 — Reorder pad

1. STOCK → Reorder pad پر Refresh۔ صفحہ کہے switched off تو پہلے متعلقہ setting/مالک سے حالت چیک کریں۔ supplier اور Find سے چیزیں محدود کریں۔
2. Stock اور Minimum ملائیں۔ minimum اصل base units میں رکھیں؛ منتخب product کا Keep at least → Save minimum۔ یہ نہ purchase ہے، نہ stock correction۔
3. مطلوبہ چیزیں tick کریں؛ Order میں کتنی base units مانگنی ہیں لکھیں۔ Tick all/Untick all صرف فہرست کا انتخاب ہیں۔
4. **ایک پیغام میں ایک supplier۔** Products کے preferred-supplier فلٹر اور یہاں کے order supplier کو ایک ہی قاعدہ نہ سمجھیں: reorder فہرست supplier کو آخری حقیقی خریداری سے دکھاتی ہے۔ supplier غلط لگے تو اصل history دیکھیں؛ کسی دوسرے agent کو mixed order نہ بھیجیں۔
5. Supplier mobile اور Your note دیں؛ Copy text، Open WhatsApp یا Print order pad۔ پیغام بننے سے stock نہیں بڑھتا؛ delivery آنے پر Purchases کریں۔

**فیز4 حصہ7 — Reorder کی cost حد:** page grant + View Purchase Cost + main ON پر آخری خرید cost؛ manager خودکار اجازت نہیں۔ denied rawcost mask/hidden؛ quantity/stock/minimum/supplier/order message unchanged۔ account active ہونا بھی لازم؛ minimum-save سمیت ہر write/session راستے کی مکمل جانچ ابھی باقی۔

<a id="lesson-20"></a>

## سبق 20 — Write-off اور Stock minus

⏳ حصہ38: Save کا کامیاب جواب مل جائے مگر quantity/notes/فہرست صاف نہ ہو تو SAVED؛وہی stock دوبارہ write-off نہ کریں۔ Unknown میں تاریخ دیکھے بغیر retry نہیں؛یہ local guard ہے،restart پر محفوظ identity نہیں۔

1. Write-off میں صحیح medicine، واقعی موجود batch، quantity/unit اور Reason منتخب کریں؛ loss purchase cost پر شمار ہوتا ہے۔ Expired، broken، lost جیسے حقیقی اسباب لکھیں۔
2. Write this off سے پہلے quantity دوبارہ دیکھیں۔ Save کے بعد یہ عام editable note نہیں؛ غلطی کی correction الگ وجہ والی کارروائی سے ہو گی۔ نقصانی مال پھر سے موجود نہ سمجھیں۔
3. سٹاک سے زیادہ write-off رکے گا؛ owner bypass نہیں۔ supplier کو بھیجا مال SR سے جائے، اسی چیز پر دوبارہ write-off نہ کریں۔
4. Stock minus پر پرانے منفی balances، کتنے دن، cost value اور review دیکھیں۔ Monthly review میں جو پایا وہ لکھ کر Mark reviewed کریں۔ **Review سے quantity نہیں بدلتی۔**
5. حقیقی shelf count لے کر Stock-take میں مالک سے correction منظور کروائیں۔ محض فہرست صاف کرنے کے لیے فرضی purchase یا خود SQL سے صفر نہ کریں۔

**حصہ10 — batch اور Write-off preview:** Stock grant کے ساتھ metadata،Cost+main سے خرید کی valuation؛preview ہر متعلقہ interaction پر تازہ cost/stock/pack لیتا ہے۔ ناکامی پر hidden/error،پرانے product کی رقم یا fake0 نہیں۔ **حصہ11 میں Write-off history/today totals اور save result بھی منتقل**؛StockMinus حصہ16 میں،audit حصہ14 میں منتقل؛generic export اور دوسرے نامکمل راستوں سے global secrecy نہ سمجھیں۔ اصل stock posting unchanged۔ [PT-30](POS-PHASE4-PART10-URDU.md)۔

**حصہ11 — Write-off کے باقی output:** history کی رقم،today total اور نئی save-result loss الگ Cost+main؛منع ہو تو hidden،مجاز اصل0 الگ۔ اصل adjustment/audit/stock حساب کو چھپی0 سے نہیں بدلتے۔ Account/page دوبارہ چیک؛StockMinus حصہ16 اورAudit حصہ14 میں منتقل؛generic exports/session الگ باقی۔ [PT-31](POS-PHASE4-PART11-URDU.md)۔

**حصہ16 — Stock Minus:** Stock page کی موجودہ اجازت پر دوا،منفی مقدار،کب سے/کیوں اورreview کی تاریخ/شخص/حالت نظر آتی ہے۔ خرید valuation،raw row value،کل اورsummary کے لیے الگ **View Purchase Cost + main switch**؛Manager یاProfit-only کافی نہیں۔ اصل0 مجاز ہو تو معلوم0،ورنہhidden۔ ReviewNote اصل Audit details ہے؛**دونوں مالی grants + main** کے بغیر raw note واپس نہیں آتا،stored note نہیں بدلتا۔ Note لکھنے کی موجودہ Stock delegation برقرار؛review stock نہیں بدلتا۔ Refresh/denial پر پرانی selection/total صاف،nominal3second active-view access check،مگرSettings cache/dispatcher/پہلےcopyکی گئی چیزوں کی فوری recall نہیں۔ [PT-36](POS-PHASE4-PART16-URDU.md)۔

<a id="lesson-21"></a>

## سبق 21 — Stock-take: اصل گنتی سے حساب ملانا

⏳ حصہ38: Apply کامیاب ہوتے ہی پرانا session/row چھوڑا جاتا ہے؛refresh ناکام ہو تو دوبارہ Apply نہیں۔ Unknown Apply کے بعد اسی page کے count/clear/cancel/open/found بند؛پہلے history verify۔ Frozen cost/uncounted/found/once-only حساب برقرار؛دوسرے actions کی اپنی fault review باقی۔

1. counting کے وقت کاروباری حرکت روک کر سب کو بتائیں۔ Stock-take میں Room خالی تو پوری دکان، ورنہ متعلقہ کمرہ؛ Open ST session۔ ایک جاری گنتی کے درمیان خریدوفروخت ہو تو جسمانی گنتی دوبارہ ملانا پڑ سکتی ہے۔
2. Print count sheet سے شیلف گنیں؛ ہر medicine/batch کی اصل تعداد درج کریں۔ Counted quantity میں درست Unit اور Note دے کر Save this count۔ Clear this count دوبارہ گننے کے لیے ہے، shelf سے چیز مٹانے کے لیے نہیں۔
3. Book، Counted، Diff اور Value دیکھیں؛ Only rows with a difference مدد دیتا ہے۔ کوئی چیز shelf پر ہے مگر book میں نہیں تو Found on the shelf… سے اصل product منتخب کریں؛ apply پر اس کے لیے GINTI batch بن سکتا ہے۔
4. صرف manager counting/cancel کی مجاز حدود میں کام کرے؛ **Apply this stock-take صرف مالک**۔ ہر row، shortage/excess اور cost اثر دیکھ کر ایک بار apply کریں۔ counted quantity کو حقیقی غیر منفی گنتی رکھیں۔
5. Apply کے بعد book count کے مطابق corrections اور audit بنتا ہے؛ دوبارہ apply نہیں۔ Cancel کے لیے وجہ لازم ہے۔ Earlier counts سے تاریخ ملتی ہے۔ غلط count کو صحیح دکھانے کے لیے بار بار sessions نہ بنائیں۔

**حصہ11 — محفوظ frozen cost،الگ visibility:** Current/history summary،sheet value،found search cost،نئے print کا net Value اور Open/Apply results Cost+main سے مشروط؛qty/count/difference باقی۔ اندرونی Count/Apply اصل frozen cost ہی لیتے ہیں۔ Hidden رقم کو دوبارہ0 input نہ بنائیں۔ مجاز uncounted—،حقیقی0 اور denied hidden الگ؛differences-only quantity-based۔ Query failure نئی document کو ناکام کرے،خالی successful sheet نہیں۔ الگ stock-take CSV builder موجود نہیں؛generic exports کی review ابھی باقی۔ [PT-31](POS-PHASE4-PART11-URDU.md)۔

<a id="lesson-22"></a>

## سبق 22 — Expenses اور مالک کی ذاتی نکاسی

⏳ حصہ38: خرچ/cash/goods drawing میں SAVED کے بعد field/editor/refresh failure نیا Save نہیں۔ Explicit no-commit پر ہی local retry؛unknown میں پہلے history check۔ Stock-category expense سے stock receive نہیں ہوتا،goods drawing سے cash دوبارہ کم نہیں ہوتا؛expense auto-draft نہیں بنا۔

1. Expenses میں Money the shop spent کے لیے Category، What was it، Amount اور Note لکھیں؛ Save the expense۔ cash/shift permission درکار ہے۔ **اس سے stock نہیں آتا، supplier account نہیں بدلتا۔**
2. Manage categories… سے مالک کی ترتیب کے مطابق running/stock labels سنبھالیں۔ نام میں stock لکھا ہونے سے یہ purchase نہیں بنتا۔ کرایہ، بجلی، چائے جیسے اصل اخراجات الگ عنوان سے رکھیں۔
3. Owner took money or goods میں Kind منتخب کریں۔ Cash taken اصل نقدی کم کرتا ہے؛ Goods میں medicine/batch، quantity/unit سے shelf کم اور purchase cost پر drawing شمار ہوتی ہے، cash نہیں نکلتا۔
4. ایک ہی نکاسی Expense اور Drawing دونوں میں مت لکھیں۔ goods drawing سٹاک سے زیادہ نہیں ہو سکتی۔
5. What went out میں entry منتخب کرکے Print voucher۔ محفوظ entry edit/delete نہیں؛ غلطی سامنے ہو تو مالک ریکارڈ دیکھ کر واضح corrective entry کرے۔

**حصہ10 — Goods drawing preview:** Cash workflow کی batch list اور نئی cost preview الگ Cost+main کے تحت؛ممنوع رقم hidden،missing/stale selection سے fake0 حساب نہیں۔ Pack/quantity تازہ read سے؛یہ stock reservation یا posting authorization نہیں۔ Frozen stock-take حصہ11 میں منتقل؛Expense/drawing goods history/results/voucher حصہ12 میں منتقل؛generic exports ابھی باقی؛عام expense cash rule نہیں بدلا۔ [PT-30](POS-PHASE4-PART10-URDU.md)۔

**حصہ12 — Goods valuation اور cash الگ:** List/Today/goods save result/new voucher میں Cost+main نہ ہو تو goods cost hidden،raw amount بھی mask۔ Expense/rent اور cash drawing اپنی Cash اجازت پر readable؛today cash sum میں goods کبھی شامل نہیں۔ Stock category tag label-only؛note/title/custom answers میں خود لکھی رقم خودکار scrub نہیں۔ Current account/page before read/write؛نیا voucher goods ID کو cash kind سے نہیں دکھاتا۔ مجاز0 اور failed/denied hidden الگ؛پہلے بنے file/status کی recall ضمانت نہیں۔ [PT-32](POS-PHASE4-PART12-URDU.md)۔

<a id="lesson-23"></a>

## سبق 23 — Reports اور Tax report

حصہ19: `Tax report` موجودہReportsgrantسے،Cost/Profit/mainکیاضافیشرطنہیں۔ Copyپرمہینےکاتازہread؛انکار/خرابیپرstalerows/رقم/settinglabel/refund/roundoffصاف،پراناclipboardمتنواپسنہیںلیاجاتا۔ Billsکیstoredtax/roundoffہی،نیاtaxONنہیں؛blankfailedreportکوknownzeroنہسمجھیں۔

1. Reports میں Today، Last 7 days، Last 30 days، This month یا متعلقہ تاریخیں دیں؛ Refresh۔ Bills، Items sold، Sales، Discount، Average bill، Cash received، On khata اور Refunded کو الگ مطلب سے پڑھیں۔
2. profit میں sale کے وقت کا cost استعمال ہوتا ہے؛ موجودہ cost بدلنے سے ہر پرانا bill اسی نئے cost پر دوبارہ نہیں بنتا۔ cash in drawer، sales، profit اور net ایک ہی عدد نہیں۔
3. Day by day میں خاموش دن بھی آ سکتے ہیں؛ Category، Top 10 by quantity/profit، shelf/dead-stock متعلقہ فہرستیں کاروباری جائزے کے لیے ہیں۔ returns کے بعد net figures دیکھیں۔ Print report یا WhatsApp سے نقل لے سکتے ہیں۔
4. Tax report میں مہینہ Previous/Next/This month سے بدلیں؛ bill پر واقعی charged tax اور روزانہ کی جمع دکھتی ہے۔ یہ نیا tax لگانے والا بٹن نہیں۔ refund کے tax کی علیحدہ تقسیم خودکار نہیں؛ accountant کے لیے refunds بھی پڑھیں۔
5. مالک کی موجودہ ہدایت **sales tax نہیں لینا** ہے؛ Settings میں tax محض سبق دیکھنے کے لیے آن نہ کریں۔ Copy for the accountant متن کی نقل ہے، قانونی filing خودکار نہیں۔

**Extra Discount کی رپورٹ:** overall/day sales اور profit آخری payable سے؛ customer profit سے الگ Extra ایک بار کم۔ product history/top medicines کے item amounts/profit **bill Extra سے پہلے** دکھتے ہیں کیونکہ Extra کسی product کو تقسیم نہیں کی گئی۔ category report میں الگ منفی **Bill Extra Discount (not assigned to products)** row ہے؛ category مجموعہ final sales سے ملے گا۔ مثال1120−Extra20، cost800: overall profit300، نہ320۔ product figures کو final overall profit نہ سمجھیں۔

**فیز4 حصہ7 — Reports کے دو الگ مالی gates:** Reports page + main ON کے بعد sale/restock cost اور Purchases aggregate کے لیے View Purchase Cost؛ explicit profit کے لیے الگ View Profit۔ denied cost/raw aggregate mask اور label hidden، actual0 سے الگ۔ underlying snapshots/SQL نہیں بدلتے۔ دیگر finance/payable/export inference مکمل audit سے پاس نہیں۔ [شواہد/باقی rollout](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md)۔

**فیز4 حصہ7 — Dead stock اور daily rows:** Dead stock کی **Money at cost/Value** اب Reports + View Purchase Cost + main ON سے؛ denied raw ValuePaisa0 masked اور label hidden، permitted genuine0 الگ معلوم0۔ daily/top explicit profit اپنی View Profit/main policy پر۔ cost-sort/top50 اپنی سابق ترتیب پر، اسے ہر inference یا دوسرے report/export کی مکمل confidentiality نہ سمجھیں۔ [PT-27](POS-PHASE4-PART7-URDU.md)۔

<a id="lesson-24"></a>

## سبق 24 — Alerts: یاد دہانی اور اصل مسئلہ

1. گھنٹی → Alerts، Refresh۔ رقم، stock، expiry، PR یا گلے کے فرق کا سبب پڑھیں؛ ہر سرخ چیز خرابی والا dialog نہیں۔
2. عام reminder الگ رکھنا مجاز عام شخص کر سکتا ہے؛ مالی/stock مسئلے کو ختم کرنا مالک کا کام اور وجہ ضروری ہے۔
3. گلے کی alert ہو تو Open the Shift page سے اصل counted cash/وجہ/شخص ملائیں۔ PR ہو تو پہلے bill link یا درست cancellation کا عمل مکمل کریں۔
4. مالک End an alert میں اصل وجہ لکھ کر End this alert۔ alert بند کرنا **رقم یا stock کی correction نہیں**۔ اصل مسئلہ واپس آئے یا نئی حرکت ہو تو alert دوبارہ آ سکتی ہے۔
5. سابق alerts/history سے پڑھے بغیر بار بار خاموش نہ کریں؛ یہ audit trail کا حصہ ہیں۔

**حصہ14 کی Audit حد:** Audit-only پر وقت،شخص،عمل اور module؛old/new/details/entity hidden۔ Raw mixed detail صرف Cost+Profit+main ON پر۔ Hidden field کو search کرکے row/count نہیں نکلتے۔ CSV کے لیے الگ bulk grant بھی؛وہی bounded filter تازہ پڑھا جاتا ہے،پرانا cached raw page نہیں۔ Books کی raw CSV کو bulk + دونوں مالی grants + Reports-or-Audit چاہیے؛Books کے پورے page کی باقی policy الگ OPEN ہے۔ [PT-34](POS-PHASE4-PART14-URDU.md)۔

<a id="lesson-25"></a>

## سبق 25 — Black box اور Books check

1. Black box میں Search، Module، Action، Lines اور When فلٹر؛ Today/7/30/All time۔ رقم یا چیز کا اختلاف ہو تو متعلقہ entry منتخب کریں۔
2. Earlier state — Before this line اور Recorded new state — After this line اس وقت ریکارڈ ہوئی عبارت ہیں، ضروری نہیں آج کا live balance ہوں۔ محفوظ correction والی ہدایت پڑھیں۔ Save CSV سے جائزے کی نقل۔
3. نئے Extra والے bill میں **unchanged item sum − الگ Extra = final total**؛ header میں points/rounding کے بعد بھی یہی رقم۔ Black box کی **sale_extra_discount** entry میں اصل/Extra/final دیکھیں۔ یہاں **عام Undo نہیں**؛ صرف پرانی حالت دکھنے سے transaction واپس نہیں ہوئی۔ بغیر سمجھے پوری shop restore کرنا ایک غلط رقم کا معمولی حل نہیں۔
4. Books check → Check again سے گیارہ حسابی identities کا صرف پڑھنے والا جائزہ۔ Says، Rows say اور Gap کو document کے ساتھ دیکھیں؛ Save this list سے ماہر کو ثبوت دیں۔
5. Books check خود repair نہیں کرتا۔ فرق آئے تو اصل bill/return/payment اور backup محفوظ کریں؛ DB فائل پر براہ راست رقم مت لکھیں۔

**موجودہ D-113 شرط:** Editable Excel/CSV default صرف Owner؛Manager/Staff کو **Allow Excel/CSV Bulk Export & Import** کی Owner grant چاہیے۔ Product cost file/preview/apply میں Cost/main؛mixed shop workbook میں دونوں مالی grants/main۔ Products page alone کافی نہیں؛partial/zero editable file نہیں بنتی۔ Upload preview اب current actor چیک کرتا ہے؛Apply transaction میں دوبارہ gate۔ Timer revocation پر derived preview ہٹاتا ہے،اصل input file نہیں۔ [PT-34](POS-PHASE4-PART14-URDU.md)۔

<a id="lesson-26"></a>

## سبق 26 — Excel / CSV: نئی فہرست اور bulk تبدیلی

1. import سے **پہلے photos-inclusive backup الگ جگہ رکھیں**۔ Excel / CSV → Import میں مطلوبہ entity منتخب، Save a blank template۔ اصل headings برقرار رکھ کر matching sheet بھریں؛ existing UTF-8 CSV بھی نئے records کے لیے قبول ہو سکتی ہے۔
2. Choose the file… کے بعد preview کی ہر error/skipped/new حالت پڑھیں۔ import کا حتمی بٹن صرف صحیح فائل/تعداد ملانے کے بعد دبائیں؛ Choose file بذات خود کامیاب import کا ثبوت نہیں۔ duplicate کو نیا product بنا کر پرانا ادھار تقسیم نہ کریں۔
3. opening stock/balances صرف متعلقہ import اصول کے مطابق ایک بار؛ پہلے سے وصول مال دوبارہ opening stock نہ بنائیں۔ موجودہ records بدلنے کے لیے Bulk update والا راستہ استعمال کریں۔
4. Export سے Medicines and stock، Customers and udhaar، Suppliers and payable کی .xlsx نقل نکلتی ہے۔ Excel کی تنصیب لازمی نہیں۔ تصاویر ان فائلوں میں نہیں ہوتیں۔
5. Bulk update → Download your data؛ اسی workbook میں Products، Customers، Suppliers، Categories، Companies، Batches، Settings اور Instructions sheets ہوتی ہیں۔ row IDs/headings برقرار رکھیں۔ **خالی cell = کوئی تبدیلی نہیں؛ `__CLEAR__` = صرف جہاں اجازت ہو وہاں صاف کرنا۔**
6. Stock، balances، secrets اور numbering counters محفوظ/protected ہیں؛ workbook کو منفی سٹاک یا PIN بدلنے کا راستہ نہ سمجھیں۔ Include Settings changes الگ confirmation مانگتا ہے، JSON value کی قسم برقرار رکھنی ہے۔
7. Choose edited workbook… کے بعد old/new preview دیکھیں؛ database بدل گئی ہو تو نیا download/preview چاہیے ہو سکتا ہے۔ Apply these changes حتمی مرحلہ ہے۔
8. Prices tab میں Percent یا Rupees، متعلقہ منتخب records اور Change the purchase cost too صرف حقیقی cost تبدیلی پر؛ Preview → پرانے/نئے ریٹ ملائیں → Apply these prices۔ below-cost سے رد قطار کو نظرانداز کرکے کامیاب نہ سمجھیں۔

**Backup کی اہم موجودہ حقیقت:** نئے v0.93 `.a11bak` manual/auto/safety encrypted source میں شامل؛پرانے ZIP/DB plain/unverified رہتے ہیں۔ Password الگ،صرفایپمیںsetup؛chatمیںنہدیں۔ Windows/PT-35 اورواقعیالگrestoreثبوتباقی؛پرانےbackupsمحفوظ۔ [حدود](POS-PHASE4-ENCRYPTED-BACKUP-REVIEW.md)۔

<a id="lesson-27"></a>

## سبق 27 — Backup اور Restore: سب سے اہم حفاظت

1. پہلے Owner → **Set / change backup password**؛ مضبوط الگ password اور confirmation دیں، 12–1024 characters۔ اصل password محفوظ جگہ سنبھالیں؛ ایپ کا login reset اس backup کو نہیں کھول سکتا۔ اسی Windows account اور database path کی protected local storage خودکار backup کو password دیتی ہے؛ نئے account یا path پر setup دوبارہ ضروری ہے۔
2. Include the photos رکھیں؛ Back up now یا Back up as… سے **`.a11bak` encrypted package** لیں۔ Open the folder/Refresh سے دیکھیں؛ تازہ copy USB یا الگ محفوظ computer پر رکھیں۔ Filename/date صرف unverified hint؛ پورا package password سے verify ہوگا۔ Back up as سے plain ZIP نہیں بنتی۔
3. Automatic backup میں on/off، Every(hours)، Keep this many؛ Save the schedule۔ صرف نئی managed encrypted automatic copies trim؛ manual، safety اور legacy خود محفوظ رکھیں۔ Setup/access ناکام ہو تو sign-in اور page پر تنبیہ، plaintext fallback نہیں۔ یہ ایپ بند ہونے پر چلنے والی Windows backup service نہیں؛ I/O میں وقت لگ سکتا ہے۔
4. Restore پورے موجودہ database کی جگہ پرانی حالت لاتا ہے؛ آج کے نئے bills پرانے backup میں نہ ہوں تو وہ وہاں نہیں دکھیں گے۔ **کام روکیں**؛ غلط bill کا معمول کا Undo نہیں۔ Restore this backup… کی red تنبیہ یا **Restore file / new PC / legacy** سے file چنیں، تصدیق پڑھیں۔
5. Encrypted restore میں اس file کا **اصل password** درج کریں۔ نئے PC پر پہلے local backup password setup کریں تاکہ موجودہ حالت کی encrypted full safety بن سکے۔ Source کا پرانا password اور نئی safety کا local password الگ ہو سکتے ہیں۔ Wrong password، tamper، unsupported format یا unsafe ZIP/DB پہلے رد؛ validation کے بعد safety، پھر replacement۔
6. Legacy ZIP/DB صرف موجودہ Owner کی الگ **UNVERIFIED legacy recovery** قبولیت سے؛ بغیر authentication فائل کو نئی hash سے معتبر نہیں بناتے۔ نئی safety پھر بھی encrypted۔ Password rotation سے پرانے packages کا password نہیں بدلتا؛ پرانے backups اس وقت تک نہ مٹائیں جب تک نیا encrypted backup اور واقعی الگ restore verify نہ ہو۔
7. بحالی کے بعد records، bills، balances، photos اور Last restore and recovery کا ملان کریں؛ named full safety رکھیں۔ Failure پر پیغام محفوظ، مزید posting روکیں؛ automatic rollback ناکام ہو تو صاف recovery required، جھوٹی success نہیں۔ Photo semantics merge ہیں: included فائلیں overwrite/add، غیرشامل تصاویر delete نہیں؛ DB-only copy موجودہ photos نہیں مٹاتی۔
8. واقعی الگ test VM/PC میں **[PT-35](POS-PHASE4-PART15-URDU.md)** کریں؛ دوسری exe folder کافی نہیں۔ Private current-user profile میں verification کے لیے عارضی plain staging بنتی ہے؛ cleanup best-effort، crash/lock پر رہ سکتی ہے، secure erase نہیں۔ Live DB/photos encrypt-at-rest نہیں کیے، photo+DB atomic snapshot نہیں۔ Windows، DPAPI، ACL، new-PC اور fault جانچ ابھی باقی ہے۔

<a id="lesson-28"></a>

## سبق 28 — ایپ کی شناخت اور Shop details الگ

**فیز4 حصہ2 کی source درستگی:** App name & icon اب shell کا اپنا section0، Shop1 سے Fields7 تک اصل contents وہی؛ Fields کی selection اب App words پر نہیں رکے گی۔ Windows پر تمام8 buttons/keyboard/چھوٹی screen کی عملی تصدیق باقی۔ section بدلنے سے half-typed values نہیں مٹنی چاہئیں؛ خرابی ہو تو [حصہ2 آزمائش](POS-PHASE4-PART2-URDU.md) کے مطابق بتائیں۔

1. Settings → App name & icon: App name ایک سے چالیس حرف؛ Choose app icon… میں .ico، زیادہ سے زیادہ 2 MB؛ Use default icon واپس اصل۔ پھر Save settings۔
2. یہ نام/icon window، login اور taskbar کے لیے ہیں۔ نصب executable اور پہلے سے بنے desktop shortcuts کا اپنا icon الگ رہ سکتا ہے۔ app icon بدلنے سے رسید پر لوگو نہیں لگتا۔
3. Shop tab: Product photos کا thumbnail size صرف Counter/Products کی دکھائی جانے والی تصویر کا حجم۔ Shop name، Phone، Address، Note at the bottom of the receipt **کاغذ والی دکان** ہیں۔
4. Show the discount line on the receipt صرف رسید کی line دکھانے/چھپانے کے لیے ہے؛ بل میں حقیقتاً دی گئی رعایت یا حساب خود ختم نہیں کرتا۔
5. idle sign-out منتخب کریں؛ Never والے انتخاب کے باوجود sign-in سے بارہ گھنٹے کی حد برقرار ہے۔ session ختم ہونے سے drawer خود نہیں گنا جاتا؛ اگلا شخص Shift page پر سنبھالے۔
6. Where the time comes from میں server/PC clock کی حالت پڑھیں۔ network نہ ملے تو PC clock استعمال ہو سکتی ہے؛ Windows کا وقت خود درست رکھیں۔ time settings بدلنے سے پہلے سبق 12 دیکھیں۔ عمومی تبدیلیوں کے بعد Save settings اور کامیابی کا status دیکھیں۔

<a id="lesson-29"></a>

## سبق 29 — Printing اور اصل پرنٹر/سکینر

1. Settings → Printing میں 58 mm (32 characters) یا 80 mm (48 characters) اپنے paper کے مطابق۔ Printer name خالی ہو تو Windows default؛ Copies ایک، دو یا تین۔ printer driver بھی Windows میں موجود ہو۔
2. Print the receipt automatically after each sale آن تو محفوظ بل کے بعد پرنٹ؛ بند ہو تو Counter کا Print last bill again۔ Cut the paper صرف cutter والے printer کے لیے۔ پرنٹ fail سے محفوظ sale خود cancel نہیں ہوتی۔
3. The Print button prints every ticked row اور bulk format سمجھ کر رکھیں: Separate receipts الگ slips؛ One A4 list صرف summary، ہر مکمل bill کی الگ رسید نہیں۔ Preview میں width دیکھیں۔
4. Prepare a PDF file with every WhatsApp send اور Folder for prepared PDFs منتخب کریں؛ blank folder سے عام exports جگہ استعمال ہو سکتی ہے۔ اس سے PDF خود WhatsApp میں attach نہیں ہوتی۔
5. SYSTEM → Printer & scanner میں پرنٹر چنیں؛ Open Windows printers سے driver/default درست؛ Print test slip۔ ruler، آخری حروف، paper width اور copies دیکھیں۔ یہ کوئی کاروباری bill نہیں۔
6. scanner والے خانے میں click کرکے حقیقی pack scan کریں؛ Check this code۔ اس کی unit بھی Counter کی محفوظ مشق میں چیک کریں، صرف digits آنا کافی نہیں۔
7. What you checked کی اصل ticks دیں؛ کوئی چیز نہ چلی ہو تو Note میں لکھیں، Save the result۔ جھوٹی tick hardware کو درست نہیں کرتی؛ note audit میں جاتا ہے، stock نہیں بدلتا۔

<a id="lesson-30"></a>

## سبق 30 — Sales rules: کیا بدلتا ہے، کیا نہیں

1. **Default bill discount basis (owner only)** میں profit یا retail؛ عام Save۔ پہلی update کے بعد آئندہ bills کا default profit ہے، retail پر واپس انتخاب ممکن۔ موجودہ user کو one-bill override کی اجازت ہو تو Counter پر صرف اس bill کے لیے popup کے بعد بدل سکے؛ نیا bill Settings default۔ محفوظ draft/held basis اپنی واپس آئے، موجودہ permissions دوبارہ چیک۔
2. **Purchase cost protection — always ON**: اصل batch خرید سے نیچے کسی کو، حتیٰ کہ مالک، اجازت نہیں۔ پرانا checkbox/lock-off اب راستہ نہیں۔ ہر شخص کی edit/manual reduction/profit%/retail%/override limits **Staff & permissions** کے الگ panel میں، یہاں role-wide cashier/manager caps نہیں۔
3. Stop a bill that crosses the customer's khata limit بھی قابل انتخاب ہے؛ customer کی limit صفر ہو تو مقررہ ceiling نہیں۔ مالک exempt ہے؛ manager override موجودہ manager-override configuration کے مطابق ہے—صرف ظاہری متن دیکھ کر ہر manager کو لازماً مجاز نہ سمجھیں۔
4. Show profit and margins بند ہو تو منافع مختلف صفحات سے چھپتا ہے، مالک سے بھی؛ کاروبار کے اصل اعداد نہیں مٹتے۔ Done / invoice / cash-change popup صرف کامیابی کے بعد کی اطلاع ہے، پرنٹ یا transaction کا متبادل نہیں۔
5. Give loyalty points آن/آف؛ Rupees spent for 1 point سے earning rate؛ Points that make a customer VIP سے threshold۔ points خرچ ہونے والے خانے کا 1 point = 1 روپیہ الگ اصول ہے۔ مالک نے موجودہ loyalty rate برقرار رکھا ہے۔
6. Tax آن/فیصد کا اختیار موجود ہے مگر **اس دکان کی ہدایت sales tax نہیں**۔ اسے آن کرنے سے آئندہ bills اور receipt پر اثر پڑ سکتا ہے؛ بلااجازت تبدیل نہ کریں۔
7. Stock: نئے کام میں negative stock کی checkbox/owner bypass نہیں۔ یہ sale، write-off، goods drawing، SR اور PR cancellation پر بھی لاگو ہے۔
8. Expiry Level 1 سب سے دور، Level 3 قریب کی warning؛ دن منطقی ترتیب میں رکھیں۔ یہ approaching-expiry رنگ بدلتے ہیں؛ گزر چکی expiry والی batch الگ expired ہے اور عام sellable batch نہیں۔
9. Supplier returns کے Start reminding اور Last return day cutoff میں مثلاً 180 اور 90 ہوں تو 120 دن باقی والی چیز یاد دہانی میں آتی ہے، واپسی آخری 90 دن سے پہلے مطلوب ہے۔ supplier کا اصل معاہدہ بھی ملائیں۔ Save settings کے بعد status دیکھیں۔

**Bulk checkbox کی تازہ تعریف:** موجودہ `import` key ہی **Allow Excel/CSV Bulk Export & Import** ہے،نیا duplicate grant نہیں؛default Owner-only،manager کی خودکار اجازت ختم،پرانی explicit grants برقرار ہیں۔ Owner ہی assign کرے؛مالی Cost/Profit checkboxes الگ اور main برقرار۔ Audit metadata اور raw detail کی حد بھی [PT-34](POS-PHASE4-PART14-URDU.md) میں ہے۔

<a id="lesson-31"></a>

## سبق 31 — عملہ، اجازتیں، راز اور App words

حصہ20: Staff/SettingsgrantیاManagerroleپوراstaffpanelنہیںکھولتا؛موجودہactiveOwnerہیstaffroster/permissions/pricingassignmentsدیکھتا/بدلتاہے۔ Owneraccessختمہوتواگلےcommand/checkپرnames/selection/permissions/pricingاورtypedstaffPIN/reset/recoveryمتنصاف؛removedtoggleکاpendingclickنہیںsaveہوناچاہیے۔ Ownerاپنےدوfinancialcheckboxesاببھیالگeditکرسکتاہے۔ عامSettingsformاورجائزchildformبلاوجہہر3secondresetنہیں۔ [PT-40](POS-PHASE4-PART20-URDU.md)۔

**نیا الگ pricing panel (صرف مالک):** Settings → Staff & permissions میں شخص منتخب کریں، **This person's pricing limits** میں rate-edit اجازت، retail-reference سے manual reduction%، profit اجازت/حد، selling-total اجازت/حد اور one-bill override tick/حدیں دیں۔ الگ **May give separate Extra Discount** اور **Extra Discount limit (Rs per whole bill, not per click)** بھی یہیں ہیں؛ مالک سمیت اس نئی Extra اجازت کا کوئی خودکار grant نہیں، اپنی بھی واضح حد save کریں۔ **Save this person's pricing limits** اسی panel کا الگ Save ہے۔ non-owner کو بغیر محفوظ grant کے خودکار اختیار نہیں، manager بھی نہیں۔ module-role reset pricing panel کو reset نہیں کرتا۔ پہلے محفوظ bill دوبارہ حساب نہیں ہوگا۔ تفصیلی مثال/آزمائش/واپسی [فیز2 نامہ](POS-PHASE2-TEST-URDU.md) اور [فیز3 Extra Discount نامہ](POS-PHASE3-TEST-URDU.md) میں ہے۔

1. مالک Settings → Staff & permissions کھولے۔ Add a person میں Name، Role اور چار ہندسوں کا PIN؛ Add this person۔ مشترکہ PIN کے بجائے ہر شخص کا اپنا نام رکھیں۔
2. شخص منتخب کرکے account Switch on / off سے رسائی روک/بحال کریں۔ ماڈیول کی ticks مخصوص کام کی اجازت دیتی ہیں؛ checkbox بدلنے پر اس کا status دیکھیں۔ Reset page permissions for this role (pricing unchanged) صرف module exceptions ہٹاتا ہے؛ pricing limits کا الگ Save ہے۔ مالک کے module rights اس سے محدود نہیں ہوتے، pricing caps الگ ہیں۔
3. password/PIN بدلنے کے لیے شخص منتخب، New PIN or password میں نیا راز، Change it for the selected person۔ پرانا راز جاننا ضروری نہیں۔ غلط منتخب شخص کا password نہ بدلیں۔
4. Your own recovery word میں اپنا یاد رہنے والا محفوظ لفظ، Save this recovery word۔ خالی چھوڑ کر فیکٹری area11 پر واپس جانا کم محفوظ ہے؛ اسے عملے کے سامنے نہ دکھائیں۔
5. Photos… شناخت کی مدد ہے، login کا بدل نہیں۔ Close the program ایک خاص اجازت ہے؛ صرف عمومی manager نام رکھنے سے ہر بندش مجاز مت سمجھیں۔
6. App words میں اصل section کے مقابل اپنا نام لکھیں؛ Save settings سے navigation/headings بدلتے ہیں۔ Restore original words سب labels واپس کرتا ہے؛ پرانے بل/رسید کی عبارت یا تاریخی record دوبارہ نہیں لکھتا۔

**فیز4 حصہ1 — راز کی تبدیلی:** اب نیا password مقرر کرنے سے سابق PIN، اور نیا PIN مقرر کرنے سے سابق password صاف ہوتا ہے۔ صرف نیا مقرر کردہ راز استعمال کریں۔ تازہحصہ4/D-116میںمتاثرہloginاگلےعملسےپہلےمسترداورfreshloginلازم؛unsavedفارمضائعہوسکتاہے۔ Sourceموجود،Windowsتصدیقباقی؛پہلےسےجاریtransactionکیatomicمنسوخیکادعوینہیں۔ owner-only متعلقہ writes میں database کا موجودہ active owner چیک؛ manager کو administration نہیں۔ recovery کا مخصوص owner راستہ استعمال کریں؛ hash اور غیرخفیہ audit ایک transaction، عام settings سے recovery hash لکھنا ممنوع۔ factory recovery کی موجودہ پالیسی برقرار۔

### حساس مالی اجازتوں کی نئی تفصیل (حصہ8)

شخص منتخب کریں؛ مالی group میں **View Profit**، **View Purchase Cost**، **Supplier ledger access (Suppliers)** اور **Customer ledger access (Customers)** ہیں۔ ہر tick الگ database key ہے؛ Ledger tick، cost/profit کی متبادل اجازت نہیں۔ **D-114 Supplier accounting exception:** ledger/paid/payable/statement/Pay/SR mainOFFپربھی،costinferenceقبول؛directitem/batchcostالگCost+main۔ Main profit/cost switch Sales rules میں ہے؛ غیر محفوظ Settings تبدیلی کو saved اجازت نہ سمجھیں۔

- Hover سے مختصر کام؛ **double-click یا Details…** سے scope اور current users کی enabled/disabled list۔ Disabled owner module کے لیے Details بٹن استعمال کریں۔
- List میں **Grant** اور **Access** الگ پڑھیں: enabled tick کے باوجود account inactive یا financial main OFF ہوسکتا ہے۔ Role default اور explicit override الگ دکھتے ہیں؛ relevant page grant اب بھی لازم۔
- تقریباً3s refresh یا Refresh now؛ owner grant/read ناکام ہو تو نام صاف، پرانی list کو تازہ حالت نہ سمجھیں۔ یہ instantaneous session revocation نہیں۔
- Single mouse click OS double-click وقفے کے بعد tick save کرتا ہے، تاکہ صرف تفصیل کھولنے سے grant نہ بدل جائے۔ Space عام checkbox کی طرح؛ per-person pricing limits کا الگ Save برقرار۔
- **Backend rollout OPEN:** حصہ9 میں نئے invoice/expired reads منتقل؛purchase entry/draft/result،عام supplier ledger،stock/expense history/results،frozen stock-take،audit/export کے باقی راستوں سے leakage بند ہونے کا دعویٰ نہیں۔ Supplier payable/payment کا مالی-policy سوال اس نئی UI سے خودکار طے نہیں ہوا۔ اصل cost/posting کبھی hidden0 سے نہ بنائیں۔ [محفوظ PT-28](POS-PHASE4-PART8-URDU.md)۔

**حصہ9 کی scope update:** permission Details میں نئے purchase invoice اور expired-list masks شامل؛line total کے ساتھPaid/Due بھی hidden۔ الگ ticks/main rule unchanged؛purchase entry/draft/result،عام supplier ledger/Pay/SR اور دوسرے نامکمل راستے واضح باقی۔ [trace](POS-PHASE4-PURCHASE-READ-REVIEW.md)۔

**حصہ10 کی scope update:** permission Details میں عام batch read masks اور تازہ Write-off/Expenses/manualSR preparation شامل۔ تاریخ/quantity برقرار؛حصہ11 میں stock history/totals/results اور frozen stock-take بھی منتقل،Expense goods outputs حصہ12 میں منتقل؛Supplier/generic exports ابھی باقی۔ [trace](POS-PHASE4-BATCH-READ-REVIEW.md)۔

**حصہ11 کی scope update:** Details میں stock history/today/result اور frozen stock-take line/session/document/Open/Apply money masks شامل؛backend frozen accounting برقرار۔ یہ expense/purchase/Supplier/generic exports اور session کی مکمل حفاظت کا سرٹیفکیٹ نہیں۔ [trace](POS-PHASE4-STOCK-VALUATION-REVIEW.md)۔

**حصہ12 کی scope update:** financial Details میں expense goods-drawing history/today/result/voucher masks شامل؛یہ ordinary Cash amount کو چھپانے کا نیا grant نہیں۔ Purchase/supplier/generic exports اور session rollout ابھی باقی۔ [trace](POS-PHASE4-EXPENSE-VISIBILITY-REVIEW.md)۔

**حصہ13 کی scope update:** Staff Details میں purchase preparation/Create/draft open-save-post کا Cost/main تعلق شامل۔ Form کی unsaved state next check پر mask،نہ0 نہ erase؛timer فوری session guarantee نہیں۔ عام Supplier paid/payable/PaySR اور generic exports ابھی باقی۔ [trace](POS-PHASE4-PURCHASE-PREPARATION-REVIEW.md)۔

**حصہ22 کی permission-read اصلاح:** کیشیئرکیCustomersاجازتuntickہوتووصولیtransactionمیںgrantپڑھنےکیخرابیسےدوبارہdefaultallowنہیں؛OwnerکیخاصCashgrantبھیtransactionشروعہونےسےغائبنہیں۔ حقیقیgrantrowنہہونااورDBreadfailالگہیں۔ OrdinaryOwnerbypassاورMapForassignment/defaultنقشہsigned-insessionکیضمانتنہیں؛Settingssecret/sessionکیباقیجانچکھلیہے۔ [PT-42](POS-PHASE4-PART22-URDU.md)۔

**حصہ23 — عملیSettingsمشق:** OwnerسےSettingsgrantدیں،عامfooterبدلیں؛grantواپسلےکرپراناصفحہSaveکریںتومنعاوراگلےcheckپرcontentچھپا/حساسinputsصافہو۔ SettingsاکیلےCloudconfigurationنہیں؛Syncکیالگgrantچاہیے۔ Cloudkeyکیصرفsaved/not-savedlabel،اصلkeyکےٹکڑےنہیں؛نئیauditentryمیںبھیاصلkeyنہیں۔ Security/startup/legacydiscountcapsاورمحفوظOwnerkeysکوworkbookسےبھیstaffنہبدلسکے؛پورافائلردہو،partialsaveنہیں۔ Saveکےبعدstartup/focusfailتوموجودہعامSettings **SAVED**ہیں؛refreshکرکےدیکھیں۔ [PT-43](POS-PHASE4-PART23-URDU.md)۔ Native/custom/photo/record/sessionکیباقیجانچکھلیہے۔

<a id="lesson-32"></a>

## سبق 32 — Fields: پہلے موجود خانے، پھر اپنے سوال




### Built-in fields کو استعمال کرنے کے قدم

1. مالک Settings → Fields → Built-in fields کھولے۔ Form کالم سے متعلقہ فارم پہچانے، پھر اصل Field کی row منتخب کرے۔ Data type اور Input پہچان کے لیے ہیں، SQL یا حساب کا فارمولا نہیں۔
2. Mark required سے کاروباری ضرورت فعال؛ Mark optional… سے نرم کرنا۔ Critical row نرم کرنے پر اضافی confirmation آ سکتا ہے؛ ہر جگہ ایک جیسے معنی نہ سمجھیں۔
3. Product کے خانے، خصوصاً Selling price، خالی ہوں تو Save product پر ان کے ناموں کا popup آتا ہے۔ **Save anyway موجود ہے**؛ No سے فارم واپس، Yes سے نامکمل چیز محفوظ۔ یہ ہر Save کوشش کی warning ہے، Counter والے فی بل popup سے الگ ہے۔
4. Customer/supplier phone اور agency کے built-in flags **file import** پر چیک ہوتے ہیں، عام ہر editor میں نیا تالا نہیں لگاتے۔ Sale/purchase rows ترجیحات ہیں؛ posting کے اپنے stock، amount، permission اور دوسری پابندیاں پھر بھی موجود ہیں۔
5. required بدلنے سے پرانے rows خود پورے یا حذف نہیں ہوتے۔ تصویر نہ ہونے کو product save کا لازمی دروازہ نہ بنائیں۔ setting کے بعد متعلقہ product کو test copy میں کھول کر اثر دیکھیں، صرف row کا رنگ نہ دیکھیں۔

### Extra fields / Own fields کے قدم

1. Settings → Fields → Extra fields یا Own fields میں Form منتخب کریں: Medicines، Customers، Suppliers، Bills، Purchases یا Expenses۔ صرف مالک field definitions سنبھالتا ہے؛ اجازت والا staff جواب دے سکتا ہے۔
2. New field، Name on the form، Kind of answer اور ضرورت کی Help لکھیں۔ Text عام عبارت، Number عدد، Amount (Rs) روپے، Date تاریخ، List (pick one) ایک انتخاب، Yes / no ہاں/نہیں، اور جائز فارموں پر Photo تصویر کے لیے ہیں۔ Quick add صرف شروعات کا نمونہ بھرتا ہے، حتمی سوال خود پڑھیں۔
3. choice والے سوال میں Choices commas سے الگ؛ Required سوچ کر رکھیں۔ دستاویزی Bills/Purchases/Expenses پر photo قسم دستیاب نہیں؛ دوسرے جائز photo fields بھی لازمی تصویر کا تالا نہیں بنتے۔
4. Save کے بعد متعلقہ فارم میں سوال دیکھیں۔ Stable key نام بدلنے کے باوجود شناخت قائم رکھتا ہے؛ اسے نئے سوال کے لیے پرانے جواب کا مطلب بدلنے میں استعمال نہ کریں۔
5. Move up/down ترتیب؛ Put away سوال چھپاتا ہے، پرانے جواب حذف نہیں؛ Bring back واپس دکھاتا ہے۔ document answers اسی bill/draft کے ہیں، اگلے customer کے bill میں جان بوجھ کر نقل نہ کریں۔

**Built-in fields — حصہ2:** مالک کے لیے metadata کھولتے/تبدیل کرتے وقت موجودہ active owner database سے چیک ہوتا ہے؛ critical field optional کرنے کا risk confirmation پہلے کی طرح لازم۔ audit/acknowledgement میں database کا موجودہ owner نام، صرف پرانا display label نہیں۔ یہ final post کی product/quantity validation ختم نہیں کرتا؛ مالک نہ ہوں یا account بدل/بند ہو تو تبدیلی نہ ہونے پر صحیح active owner سے دوبارہ sign in کریں۔ یہ تمام کھلے sessions کی فوری revocation کا دعویٰ نہیں۔

**فیز4 حصہ6 — custom definitions اور answers الگ اختیار:** سوال Add/Update/on-off/order اور inactive/admin list صرف database کے موجودہ active مالک کے لیے؛ same tick/no-op بھی پرانی owner snapshot سے اجازت نہیں۔ عام List/Values/Describe/public Save answers متعلقہ current module مانگتے ہیں؛ Customers مجاز cashier جواب دے سکتا ہے، definition کا اصول نہیں بدل سکتا۔ audit میں اس public save/definition write کا موجودہ محفوظ نام آتا ہے۔ parent record کے اندر atomic answers کی delegation اپنی سابق policy پر ہے، مکمل parent/session جانچ باقی۔ [PT-26/حدود](POS-PHASE4-PART6-URDU.md)۔

**حصہ25 کی محفوظ مشق:** صرف الگ test DB/photos/config میں customer کے Extra answers کھولیں،grant/account واپس لیں؛اگلےcommand/nominal3secondcheck پر rows اورstaged تصویر کا نام صاف،failure status نظر آئے۔ اجازت واپس آئے تو parent form دوبارہ load کریں؛timer unsaved edits خود overwrite نہیں کرتا۔ Missing record کو مصدقہ empty answers نہیں کہتے۔ Separate save commit مگر reload fail ہو تو **SAVED،دوبارہsave نہ کریں**۔ Native policy risk prompt سے پہلے تازہ definition پڑھی جاتی ہے؛critical optional کی confirmation برقرار۔ [PT-45](POS-PHASE4-PART25-URDU.md)۔ اصل session/تمام parent commands/definition-version synchronization کی blanket ضمانت نہیں۔

**حصہ26 — managerکیعملیمشق:** صرفالگtestcopyمیںdefinitioneditکھولیں،Ownerinactiveیااسیdefinitionکو دوسریwindowسےبدلیں؛اگلےcommand/nominal3secondcheckپرپراناactionرکےاورselection/typedinputsصاف،واضحwarningآئے۔ Unchangedtickپرآپکاتازہtypedtextبرقرار۔ Failedreadمصدقہemptylistنہیں۔ Save/Move/HideRestorecommitمگرreloadfailتو **SAVED،دوبارہactionنہکریں**۔ [PT-46](POS-PHASE4-PART26-URDU.md)۔ Selecteddefinitionبدلےتودوبارہselect/reviewاورضرورتپرمتنلکھیں؛یہautomaticmergeیاatomiclockنہیں۔ Stablekeys/oldanswersاورdocumentnoPhotoبرقرار؛Windowsfocus/timer/selectionثبوتباقی۔

**حصہ27کیالگtestcopyمیںمشق:** CustomerAکاphoneeditلکھیں،Bمنتخبکریں؛AکامتنBپرنہلگے۔ ProducteditکھولکرtestOwnerسےCost/main/Products/accountبدلیں؛اگلےaction/nominal3stickپرcost/formصاف،Saveبنداورreopenwarning۔ Regrantکےبعدeditorبند/refresh/reopen؛پراناdiscardedمتنواپس نہیں۔ Stored-costpatchCost+mainمانگتاہے؛newproductخودtypedcostکیworkflowنہیںبدلی۔ Saveہوچکاہومگرrefreshناکامتو **SAVED،دوبارہنہیں**۔ [PT-47](POS-PHASE4-PART27-URDU.md)۔ اصلshopپرکسیفرضیpost/permissionexerciseنہیں؛verifiedbackup+واقعیالگDB/photos/config،Windowsثبوتباقی۔

**حصہ28کیالگtestcopyمیںمشق:** Catalog/Uniteditorمیںunsavedname/order/link/WholeOnlyلکھیں؛testOwnerسےProductsgrantواپسلیں۔ اگلےaction/nominal3stickپرinputs/selection/rowsصاف،Saveبند؛regrantکےبعدRefreshاورrowreviewلازم۔ Stableticktextنہبدلے،failedlistمصدقہemptyنہہو۔ Savecommitمگرreloadfailتو **SAVED،دوبارہنہیں**؛Brandname/linkpartialsuccessملاکرfullatomicنہسمجھیں۔ [PT-48](POS-PHASE4-PART28-URDU.md)۔ اصلshopپرنہیں؛verifiedbackup+واقعیالگDB/photos/config،Windowsثبوتباقی۔

**حصہ29کیالگtestcopyمیںمشق:** Customerسے300اورsupplierکو200cashروپے؛دونوںbooks/paymentrowsملاکرdrawerکانتیجہ+100دیکھیں۔ Card/onlineمیںdrawerنہبدلے۔ CorepostsuccessکےبعدصرفUIcallback/receiptfailfixtureپر **SAVED،دوبارہreceive/payنہیں**؛historyسےentryچیک/reprintکریں۔ Localreentrylatchglobalrequesttokenنہیں۔ [PT-49](POS-PHASE4-PART29-URDU.md)۔ اصلshopپرنہیں؛verifiedbackup+الگDB/photos/config؛Windows2126tests/actualfault/timingproofباقی۔

**حصہ30کیالگtestcopyمیںمشق:**20+20گولیاںcost10،bill400،cashpaid40:stock40/payable360/drawer-40۔5+5supplierواپسcredit100:stock30/payable260؛الگfixtureمیںcash100واپس:payable360/drawer+100،returnکیpaymentrowنہیں۔ Printer/refreshfailبعدsuccessپر **SAVED؛دوبارہdelivery/returnنہیں**،historyچیک/reprint۔ [PT-50](POS-PHASE4-PART30-URDU.md)؛یہWindowsمشقUNRUNہے،اصلshopپرنہیں؛verifiedbackup+الگDB/photos/configلازم۔

**حصہ31کیالگtestcopyمیںمشق:**10+10گولیاںretail20،bill400،cash100/credit300:stock200سے180،balance300،drawer+100۔ پوریواپسی:credit300کم/cash100واپس،restockپرstock200یاquarantineپر180/Qcodes؛loyaltyfixture40سے44پھر40۔ Saveکےبعدreceiptfailپر **SAVED،دوبارہbill/refundنہیں**؛historyچیککریں۔ [PT-51](POS-PHASE4-PART31-URDU.md)؛یہWindowsمشقUNRUNہے،اصلshopپرنہیں؛verifiedbackup+الگDB/photos/configلازم۔

**حصہ32کیالگtestcopyمیںمشق:** managerکےکھلےReturnbillکیReturnsgrantواپس؛اگلےaction/nominaltickپربill/form/rowsصاف،editingبند۔ GrantبحالپرنیاFind/Refresh؛roleبدلاہوتونئیsignin۔ POSgrantاورReturnsgrantالگ؛مالیvisibilityگرantsاسکابدلنہیں۔ [PT-52](POS-PHASE4-PART32-URDU.md)؛WindowsمشقUNRUN،اصلshopپرنہیں؛verifiedbackup+الگDB/photos/configلازم۔

<a id="lesson-33"></a>

## سبق 33 — خودکار آغاز، محفوظ بندش اور Screen lock

حصہ21: Sessionexpiryمیںگلہopenیاstateunknownہوتوcheck/countnote؛knownclosedہوتونیاnoteنہیں۔ Programخودگنتیکرکےcloseنہیںکرتا؛noteکےالفاظunknownکوknownopenنہیںکہتے۔ Existingbest-effortnote-storageکیخرابی/مکملsessionauthenticationاورWindowsثبوتابھیباقی۔

1. Settings → Shop میں startup منتخب کریں: By hand/manual = ہاتھ سے آغاز؛ Start with Windows/auto = Windows sign-in پر آغاز؛ Protected = خود آغاز اور × پر password/PIN کی اجازت۔ auto-start خود login نہیں کرتا۔
2. تمام Windows اکاؤنٹس پر startup رکھنے کے لیے administrator اختیار چاہیے۔ نہ ہو تو موجودہ implementation صرف اس Windows account کی entry رکھ سکتی ہے؛ save کے بعد اصل ScopeText پڑھیں، ہر اکاؤنٹ پر لگ جانے کا اندازہ نہ کریں۔ دوسری account سے جانچ الگ ضروری ہے۔
3. خودکار startup کے login پر fullscreen/چھپی taskbar کی حالت ہو سکتی ہے؛ یہ عام دستی launch کے ہر صفحے کا مستقل نیا layout نہیں۔ دوسری copy کھولیں تو already-running پیغام کے ساتھ نئی copy رکتی ہے۔
4. Protected یا signed-in screen lock میں × پر اصل Close اجازت والا شخص نام اور PIN/password دے۔ غلط/نامکمل اجازت پر بندش نہیں۔ Sign out الگ کارروائی ہے، drawer بندش الگ۔
5. Lock the screen while signed in آن ہو تو maximize/no-minimize اور taskbar چھپانے کے ساتھ واپس جانے پر تنبیہ/واپسی، اور کس نے کب کتنی دیر چھوڑا اس کا record رکھنا مقصود ہے۔ Task Manager، Windows administrator، shortcut یا بجلی کے خلاف اسے مکمل Windows kiosk security نہ سمجھیں۔
6. Shift دبا کر startup پر manual/protection reset کا امدادی راستہ موجود ہے؛ یہ login password recovery نہیں، اور اس کا audit ہوتا ہے۔ صرف مجاز مالک اس کی ضرورت سمجھے؛ اسے عملے کی معمول کی بندش نہ بنائیں۔
7. غیر معمولی بندش کی رپورٹ اگلے آغاز پر آ سکتی ہے۔ ڈیٹابیس کی transaction حفاظت کے باوجود unsaved editor کی ہر typing محفوظ رہنے یا بجلی/disk کی ہر خرابی میں data کبھی نہ کھونے کی ضمانت نہیں—تازہ external backups لازماً رکھیں۔

<a id="lesson-34"></a>

## سبق 34 — Cloud اور دوسرا کاؤنٹر

1. Cloud اختیاری mirror ہے؛ offline Counter چل سکتا ہے۔ Settings → Cloud sync میں مجاز شخص on/off، address/table/shop id اور key دے۔ **key chat، screenshot یا report میں نہ بھیجیں۔** عام tutorial کے لیے کوئی connection درکار نہیں۔
2. Automatic push اور وقفہ (5 سے 1440 منٹ) صرف مقررہ push؛ failed push سے اگلی کوشش تک cloud پرانی ہو سکتی ہے۔ Remove the saved key کے بعد status ملائیں۔
3. SYSTEM → Cloud sync میں Push the shop's data to the cloud مقامی ریکارڈ کی نقل بھیجتا ہے؛ See what the cloud has (writes nothing) صرف preview ہے۔
4. Bring the cloud data here overwrite والی خطرناک کارروائی ہے۔ cloud میں آج کا bill نہ ہو سکتا ہے؛ counts/differences اور confirmation پڑھے بغیر نہ کریں۔ پہلے backup، پھر مجاز شخص کی دانستہ اجازت؛ اسے خودکار دوطرفہ merge نہ سمجھیں۔
5. catalog/master rows وسیع، transactions عموماً گزشتہ 90 دن اور ان کے ضروری متعلقہ records کے scope میں جاتے ہیں؛ **photos نہیں**۔ اس لیے مکمل، تصاویر والا encrypted backup ضروری ہے۔
6. Second counter (LAN) کا صفحہ موجود ہے مگر مالک نے دوسرا counter **ملتوی** رکھا ہے۔ ابھی data folder/share یا database mode نہ بدلیں۔ دو فولڈر بنا لینا دو الگ دکانیں نہیں۔ جب منظور ہو تو ماہر موجودہ location، journal، network/share permissions، backup اور اصل دونوں PC پر جانچ کے بعد ہی آگے بڑھے۔

<a id="lesson-35"></a>

## سبق 35 — Preview، WhatsApp اور پرنٹ کی مشترکہ سمجھ

گلہhandoverکیslipکےtext/PDF/printسےپہلےcurrentCashچیک؛پراناpermission/cachedsnapshotخودauthorizationنہیں۔ پہلےسےبنPaper/PDF/clipboardواپسنہیںلیاجاتے،accesscheckاورhandoffکےدرمیانraceابھیحدہے۔

Recoveryکیfailedslipپرپرنٹرکوخالیdocumentنہیںدیاجاتا۔ وصولیsaveہوجانےکےبعدprinterیاrefreshکیخرابیسےpaymentختمیادوبارہنہیںہوتی؛statusپڑھیںاورکھاتہcheckکریں۔

1. Preview جہاں موجود ہو، اصل printer کو جانے والی عبارت دکھاتا ہے۔ آخری ہندسہ کٹ رہا ہو تو width/driver درست کریں، customer کو نامکمل رقم والی slip نہ دیں۔ Preview window کا Print وہی انتخاب چھاپتا ہے؛ Close صرف دیکھنا ختم کرتا ہے۔
2. Print selected… صرف متعلقہ منتخب قطاروں کے لیے؛ عام Print کی bulk setting بھی دیکھیں۔ A4 summary اور مکمل الگ bill slips ایک چیز نہیں۔
3. WhatsApp سے message/browser کھل سکتا ہے اور اگر on ہو تو PDF تیار ہوتا ہے۔ **PDF فائل کو خود attach اور send کریں**؛ تیار ہونے کا پیغام customer تک پہنچنے کا ثبوت نہیں۔ غلط فون پر کھاتہ نہ بھیجیں۔
4. Auto-print بند، paper ختم یا printer fail ہو تو پہلے Bills میں saved invoice دیکھیں، پھر اسی کی نقل نکالیں؛ دوبارہ Complete sale نئی sale بنا سکتی ہے۔ واپس مال/رقم دینا ہو تو Returns والا راستہ ہے، کاغذ نہ نکالنا cancellation نہیں۔
5. دستیابی کی اندرونی وارننگ رسید پر کبھی نہیں؛ app name/icon کو کاغذ کا shop name/logo نہ سمجھیں۔ Slip دینے سے پہلے نام، unit، quantity، rate، total، paid/due اور change ملانا کیشیئر کی ذمہ داری ہے۔

**خرید invoice کی تازہ حد:** نئے purchase ReceiptDocument کی print/PDF/text preparation میں ایک ہی cost rule؛منع ہونے پرline amounts/Total/Paid/Due hidden۔ یہ نئے document کی جانچ ہے؛پہلے محفوظ PDF،کاغذ یا دوسرے ابھی غیرمنتقل exports خود واپس محفوظ نہیں ہوتے۔ [PT-29](POS-PHASE4-PART9-URDU.md)۔

**Stock-take نئی print حد:** نیا document authorized current account/page سے تیار؛Cost+main منع ہو تو net Value hidden،book/count/difference برقرار۔ Query failure میں print تیاری ناکام،اصل SQL error نہیں؛پرانے paper/PDF واپس recall نہیں ہوتے۔ کوئی الگ stock-take CSV route مکمل ہونے کا دعویٰ نہیں۔ [PT-31](POS-PHASE4-PART11-URDU.md)۔

**Cash-out voucher کی تازہ حد:** نیا goods voucher Cost+main سے amount،ورنہ hidden؛ordinary expense/cash-drawing voucher Cash module کے تحت اصل رقم۔ Voucher query fail ہو تو تیاری fail،SQL details نہیں۔ PDF/کاغذ پہلے بن چکے ہوں تو واپس مٹنے کی ضمانت نہیں؛generic exports الگ audit۔ [PT-32](POS-PHASE4-PART12-URDU.md)۔

<a id="lesson-36"></a>

## سبق 36 — روزانہ ترتیب، محفوظ مشق اور خرابی کا اگلا قدم

### صبح سے بندش تک

1. اپنے نام سے Sign in؛ Windows وقت/تاریخ، printer/paper اور ضروری alerts دیکھیں۔
2. Shift میں اصل float سے گلا کھولیں؛ پہلے سے کھلا drawer صحیح گنتی/ہینڈ اوور سے سنبھالیں۔
3. حقیقی delivery Purchases، فروخت Counter، بل والی واپسی Returns، وصولی Customers/Recovery، supplier payment Suppliers؛ ایک ہی رقم دو صفحات پر دوبارہ نہ دیں۔
4. کرایہ وغیرہ Expenses؛ مالک کی ذاتی نقد/مال Drawings؛ ضائع مال Write-off؛ فرضی purchase سے فرق نہ چھپائیں۔
5. دن کے اختتام Reports، کھاتوں اور alerts کا جائزہ؛ اصل cash گن کر shift بند یا hand over؛ تازہ photos-inclusive ZIP الگ محفوظ جگہ نقل؛ Sign out۔

### پہلے صرف یہ محفوظ عملی مشق کریں

- الگ، تصدیق شدہ **test data environment** پر مالک اور ایک test cashier بنائیں۔ دوسری program folder کو الگ test database نہ سمجھیں؛ اصل shop کو fake entries سے نہ بھریں۔
- ایک test product، درست pack formula اور اصلی جیسی future expiry کے test batch سے purchase؛ ایک cash sale اور اس کی دوبارہ رسید؛ پھر اس bill کی return۔ ہر جگہ stock اور cash اثر الگ نوٹ کریں۔
- صفر-stock item کا bill مالک سے بھی رد ہو؛ missing retail لیکن موجود stock پر صرف مالک/مینیجر custom rate دے سکے؛ cashier کو اجازت نہ ہو۔ warning اسی live bill میں ایک بار ہو، Clear کے بعد پھر ممکن ہو، slip پر کوئی availability لائن نہ ہو۔
- Settings Fields میں product retail required کرکے product save warning دیکھیں؛ Save anyway ہو مگر Counter کی پابندیاں الگ رہیں۔
- printer test slip، gallery cover، draft save/open، encrypted backup اور صرف الگ ماحول میں restore آزمائیں۔ اصل stock-take apply، live cloud pull یا LAN configuration کو مشق کا پہلا قدم نہ بنائیں۔

### اگر کام رک جائے

| حالت | محفوظ اگلا قدم |
|---|---|
| Total پر dash/قیمت missing | cart کی Rate Rs، product retail، stock، unit اور نیچے message دیکھیں؛ سبق 10 |
| گلا بند | Shift میں مجاز شخص اصل float/پچھلی بندش ملائے؛ فرضی cash entry نہ بنائیں |
| سٹاک موجود لگتا ہے مگر sale رکے | unit/pack، cart کی مجموعی مقدار، active/unexpired batches اور پرانا minus ملائیں |
| ریٹ/رعایت رد | اس شخص کے محفوظ manual/profit/retail caps، درست basis، مثبت ریٹ، stock اور مستقل purchase-cost guard دیکھیں |
| credit bill رکے | customer، باقی limit، payment split اور اصل permissions چیک |
| صفحہ نہ دکھے | Staff permissions یا فلٹر/بدلا ہوا App words؛ فوراً reinstall/data reset نہ کریں |
| print نہ نکلے | پہلے saved invoice معلوم کریں؛ Windows printer/width/paper؛ اسی bill کی reprint |
| تصویر غائب | gallery میں missing file دیکھیں، photos-inclusive backup تلاش؛ record دوبارہ نہ بنائیں |
| کریش/صفحہ کھلنے کی خرابی | error کی پوری عبارت، version، صفحہ اور کیا دبایا نوٹ؛ غیر محفوظ کام کا حال دیکھیں؛ DB نہ مٹائیں |
| حساب میں فرق | اصل bill/payment/return اور Black box دیکھیں؛ Books check کی read-only فہرست محفوظ |
| restore/cloud کے بعد پرانا data | مزید posting روکیں، منتخب snapshot اور pre-operation encrypted recovery package/پیغام دیکھیں؛ بار بار overwrite نہ کریں |

خرابی کی اطلاع میں **version، صفحے کا نام، کیا قدم اٹھایا، متوقع اور اصل نتیجہ** دیں۔ PIN/password، recovery word، cloud key یا پورا حقیقی customer database عام chat میں نہ بھیجیں۔ اگر developer جانچ مانگے تو tools\RUN-TESTS.cmd اور SEND-REPORT.cmd کی مکمل رپورٹ دیں؛ v0.124.0 کا متوقع test count 3118 ہے، یہ اس کتاب سے چل جانے کا دعویٰ نہیں۔

## نگہداشت کا نوٹ — کتاب کہاں سے تصدیق ہوئی

یہ متن v0.78.0 کی navigation اور Views/ViewModels کے مطابق لکھا ہے، تصاویر یا حقیقی Windows session کے مشاہدے کا دعویٰ نہیں۔ بنیادی نقشہ ShellViewModel اور AppLabelService؛ SettingsView، NativeFieldsView، CustomFieldsView؛ متعلقہ Products/Counter/Purchases/Returns/Partners/Shift/Reports/System views سے ملا۔ مالی/stock حدود SaleService، StockPolicy، PurchaseService، ReturnService، ProvisionalReturnService، SupplierLedgerService، AuthService اور StartupService سے ملائی گئیں۔

پرانے HOW-TO-RUN، سابق version rows یا پرانے handoff میں tutorial سے متصادم installation/receipt/negative-stock عبارت تاریخی ہو سکتی ہے۔ نئی receipt-unavailable لائن منسوخ، D-93 برقرار، app identity اور receipt shop details الگ ہیں۔ مالک نے مستقل search pane B منتخب کیا؛ فیز1 space/keyboard code موجود، Windows ثبوت باقی۔ فیز2 مالی editor/permissions code موجود، Windows/PT-21 باقی؛ فیز3 Extra Discount/طریقہB source موجود، Windows/PT-22 باقی؛ فیز4 مکمل page-by-page review ابھی باقی ہے۔ پروگرام کی نئی release میں بٹن/قاعدہ بدلے تو اس کتاب کو بھی نئے source کے ساتھ دوبارہ ملانا ضروری ہے۔
