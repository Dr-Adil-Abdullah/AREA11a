# ⏳ فیز4 حصہ37 — عام Save بھی مشترکہ Draft سے؛Park کی دو copy نہیں

**PT-57 · v0.115.0 Cairn · 2026-11-10 · مستقل15/16/23/24/25**  
[⏳ تمام1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md) · [⏳ source/ثبوت/حدود](POS-PHASE4-AUTOMATIC-DRAFT-REVIEW.md)

## ✅ منظور شدہ فیصلے درج

- ✅ مالک نے منتخب کیا: عام Counter bill یاPurchase Save پہلے موجودہ shared Drafts میں خودکار محفوظ،پھر اسی ID سے posting؛الگ verification module نہیں۔ مجاز دوسرے عملے کو بھی اسی جگہ محفوظ کام نظر آسکتا ہے۔
- ✅ مالک نے منتخب کیا: کھلے عام draft کوPark کرکےنئیcopy نہ بنے؛اسی draft کوSave یاComplete کریں۔ Recovery draft پرپہلےسےموجودروک ابعامdraftپربھی۔
- ✅ نئیreportعلامتوں کااصول درج: ✅ صرف100%مکمل متعلقہ کام؛⏳ جاری/کچھ باقی؛❌ بالکل شروع نہیں۔ Source لکھنے کوWindows/productionمکمل نہیں کہیں گے۔ Generated checklist اب پرانےsource ticks کوعملی100%نہیںکہتی؛57started features جاری،اصلsourceboardمحفوظ۔

## ⏳ source میں شامل،اصل Windows ثبوت باقی

- ⏳ عام Save پرdraft محفوظ اوراس کاID Counter/Purchase سےجڑنے سےپہلےcreatorنہیںچلتا۔ عام کامیابsale/purchaseکےبعدdraftterminalہوجاتاہے،openفہرستمیںدوسریpostablecopyنہیںرہتی۔
- ⏳ Validation یاconfirmedno-commitپرcurrentworkاسیdraftمیںرہتاہے؛دوبارہSaveاسیIDسے۔ Unknownpostingپرپچھلےحصےکامارکررہتاہے؛documentverifyکےبغیردوبارہentryنہیں۔
- ⏳ پہلیdraftsavingہیunknownہوتو**documentpostingشروعنہیںہوتی**۔ SharedDraftsچیککیےبغیرنئیcopyنہیں؛localguardSave/Post/Parkروکتاہے۔ اسمرحلےمیںممکنہمحفوظrowopen draftہوگی،postingmarkerضروری نہیںکیونکہcreatorچلاہی نہیں۔
- ⏳ CounterClearکیconfirmationابsavedDraftیاparkedworkباقیرہنےاورunsavededitsضائعہونےکیوضاحتکرتیہے۔ Verificationکےبعدہیصرفcounterچھوڑیں۔
- ⏳ عامHoldکاکامیابجوابملےمگرUIcleanupخرابہوتو**PARKED،اسیcopyکودوبارہsell/parkنہیں**۔ UnknownHoldجوابپرparkedlistverifyکریں؛confirmedrollbackپرہیعامretry۔ یہlocalguardہے،cross-restartintentidentityنہیں۔
- ⏳ Purchaseکیacknowledgedsavingکےبعدpartialcleanupاورdraftopenکیpartialreconstruction/missingproductپرSave/SaveDraft/Openبند؛پہلےhistory/Draftsverify،پھرصفحہدوبارہکھولیں۔ Currentpayloadچھوڑنےسےپہلےمحفوظcopyکیجانچکریں؛خودکارunsavededitbackupکاوعدہنہیں۔
- ⏳ پہلےسےطےشدہactual-costfloor،pricing/discount/Extra،ReturnB،SupplierB،Cost/Profit/mainاورBlackbox/BookCheckinternalsunchanged۔

## ⏳ محفوظ عملی مشق — Windows قدم ابھی نہیں چلے

- ⏳ VerifiedbackupاورواقعیالگtestDB/photos/configلازم؛صرفexefolderالگکافینہیں۔ اصلshopپرsyntheticbill،SQLtriggerیاfaultمتدیں۔
- ⏳ **15/16:** نئیعامCountercartاورPurchase deliveryپربغیرmanualSaveDraftکےSave/Completeکریں۔ Developerdisposabletestمیںdraftsaveکےبعد،postسےپہلےرکواکراپبند/کھولے؛sharedDraftsمیںوہیpayloadملے،money/stockابھینہیںبدلے۔ پھرایکdocumentبنائیں،اسیdraftسےدوبارہنہیں۔
- ⏳ **15/16:** جائزvalidationfailureدیں؛درستگیبھیاسیdraftIDمیںSave/Postہو۔ عامcash/online/card،customanswers،doctor/customer/supplier،جائزpricing/Extraاورfreshretailconfirmationکیجانچکریں۔ Draftکےموجودہsize/currentwriter/Cost-maingatesعامSaveپربھیلگیںگے؛انکارسےdocumentنہیںبنےگا۔
- ⏳ **23/24:** draftinsert/updateاورauditکےBEFORE/AFTERfaults؛پورےsnapshotsبرابر،knownno-commit،faultہٹاکرایکdraftاورایکpost۔ C#testکےproviderسےحقیقیCOMMIT-I/Ofaultالگہے۔
- ⏳ **25:** documentcommitکےبعدreplyخراب/unknownکریں؛markerرہے،Post/Save/Hold-copyبند۔ پہلےاصلBills/Purchasesverify؛markerنہdocumentکےوجودکانہعدموجودکاثبوتہے۔
- ⏳ **25:** ابتدائیdraftsaveجوابunknown/throwکریں؛creatorcallzero،currentpageپرblindretryبند۔ RestartپرDraftscheck؛کوئیopenrowملےتواسیسےکامکریں،نئیcopyنہیں۔ Savedraftموجودنہہونےکایقینکیےبغیرresaveنہیں۔
- ⏳ **15:** عامopendraft،posted/verification/discarded/wrongkind/missingpositiveIDکےساتھHold؛کوئیheldrow/number/auditنہیں۔ عامnon-draftcartکاجائزHoldاببھیچلے۔
- ⏳ **15/25:** HoldکےبعدCart.Clear/discount/editor/refreshcallbackfault؛PARKEDwarning،Save/Post/Parkدوبارہبند۔ History/parkedlistکےبعدClearصرفcounter؛unknownparkingکوجھوٹاSAVEDنہیںکہنا۔
- ⏳ **16/25:** PurchaseLines.Clear/binding/editor/lookupfaultبعدack؛SAVED،دوبارہdeliveryنہیں۔ Draftloadکیپہلی/درمیانیlinefaultیاغائبproductپرpartialdeliveryoverwriteنہہو۔
- ⏳ **2/4:** نئے41C#casesاورپوری2685suiteکےWindowsbuild/TRXlogs،failuresکیfix/retestرکھیں۔ یہاںC#compile/runنہیںہوا۔

## ⏳ باقی دروازے

- ⏳ UIکیعامpostingابdraftکےذریعےہے؛براہراستCoreCreateکواستعمالکرنےوالےدوسرےcallersپرglobalidempotencyنہیںلگائی۔ Separatepremark/Create/finalize،durablereconciliation،parallel/session/CAS/providerCOMMIT/powercutاوردیگرstock/expense/recoveryPR/filefaultsباقی۔
- ⏳26–29restore/new-PC/cloud/filesاور2/4/30–35Windows/WPF/hardware/isolationباقی؛FullPhase4OPEN۔
- ❌36single-double-clicktestinstallerکیbinaryابھینہیںبنائی؛پہلےbuild/tests/package/isolation/smokeثبوتدرکار۔
- ⏳36categories≠36Nexts؛3–6مربوطroundsمشروطاندازہ،deadlineیاupperboundنہیں۔ ہرcompletedportionکےdocs+samebranchcommit/push+actualremoteverificationلازم۔
