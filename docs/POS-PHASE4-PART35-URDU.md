# Phase4 — حصہ35: Drafts / Held carts کی موجودہ اجازت؛ PT-55

**v0.113.0 Dune · مستقل5/6/7/15/16/23/25 · source checkpoint،Windows منظوری نہیں۔**

[مستقل1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md) · [فنی trace/حدود](POS-PHASE4-DRAFT-HELD-CURRENT-REVIEW.md) · [اردو استعمال نامہ](USER-GUIDE-URDU.md)

## 1۔ روزمرہ مثال اور برقرار پالیسی

Cashier نے کل کا Sale draft یا parked cart کھولا،پھر Owner نے POS اجازت واپس لے لی۔ پرانا User object/ID اب list،payload،save،resume یاdiscard کا اختیار نہیں۔ دوسرے جائز شخص کا draft اب بھی shared ہے؛نئی ownership پابندی نہیں لگائی۔

- **Sale drafts اور held carts:** موجودہ POS account۔
- **Purchase draft list/discard:** موجودہ Purchases اجازت؛metadata سے براہِ راست item costs نہیں ملتے۔
- **Purchase payload save/open/update/post:** Purchases کے علاوہ Cost + main کی پہلے سے موجود شرط۔ یہ مالی شرط metadata list/discard پر نہیں لگائی۔
- Count read failure کو successful zero نہ سمجھیں۔ Unsaved authored POS cart کو خاموشی سے نہیں مٹایا۔

## 2۔ source میں کیا مکمل ہوا؟

1. **5/6/23:** Draft save/update transactions،open/list اورdiscard میں current stored active/sameparsedrole/module reader۔ Purchase payload کا الگ Cost/main gate برقرار؛normalized type کے بغیر payload-gate bypass نہیں۔ Unknown type deny۔
2. **6/23:** Discard میں باہر پوراJSON پہلے نہیں پڑھا جاتا؛transaction میںصرفtype classify،پھرموجودہmoduleاجازت،پھرrow/payload۔ Stored creatorname اورsave/update/discard audits؛Purchasefinalizeauditبھیcurrentreaderسے۔ Existingcreatorhistoricalرہتاہے،updateپرنہیںبدلتا۔
3. **15/23:** HeldList/CountموجودہPOSreader؛Countابexplicitresult۔ Hold/Resume/Discardمیںtransactionکےاندرcurrentwriter،پھرnumber/target/mutation؛storedaudit۔ HoldکیQuote/pricingpolicyنہیںبدلی۔
4. **7/25:** Draftdrawerاورparkedlistکےnextaction/nominal3secondtimers؛denialپرlists/selectionclear،failedread!=certifiedempty،regrantپرتازہRefresh۔ Stableallowedticktypedcart/selectionنہیںمٹاتا۔ DraftmodalYesکےبعدcurrentrow/accessدوبارہ؛detachedcallbacksrefuse۔ Draftpost/discardackکےبعدrefreshfaultمیںSAVED۔
5. **2:**136نئےC#cases=60currentfault/regrant+60invalidactor+9storedwriter+7facts؛کل2615متوقع۔ **C#compile/runtimeسبUNRUN۔**

**Pricing/Extra/ReturnB/SupplierB/mainfinancialpolicy،Sale/Purchasepostingcalculations،draftpayloadformat/sharedpolicy/terminalrulesنہیںبدلے۔**

## 3۔ PT-55 — صرف isolated Windows copy؛تمام عملی steps UNRUN

**پہلے verified encrypted backup؛پھر واقعی الگ DB/photos/config۔ الگ exe folder کافی نہیں۔ اصل shop پر synthetic bills/drafts/permissionfaults نہیں۔ کوئیsecretیااصلDBchatمیںنہیں۔**

### 5.1 / 6.1 / 23.1 — current reader/writer

1. ManagerfixtureمیںSale/Purchase draftsاورایکheldcart۔ Inactive/rolechange/POS-Purchasesrevoke/grantreadfaultکےبعدمتعلقہ15routesdeny،payload/draft/held/number/audit/money/stockunchanged۔ Same-rolegrantrestoreپراسیserviceسےfreshread/action؛changedroleکےلیےنئیsignin۔
2. Null/zero/inactivecaller/forgedOwnerاورstoredOwnerinactive/rolechangeddeny۔ ValidcurrentOwnerordinarybypassبرقرار۔ یہsessiontoken/same-role-IDimpersonationproofنہیں۔
3. POSOFF/PurchasesONمیںPurchaseroutesچلیں،Sale/helddeny؛الٹصورتالٹنتیجہ۔ Cost/mainOFFمیںPurchasemetadata/list/discardجائز،payloadsave/open/updatedeny؛Sale/heldپرنئیfinancialشرطنہیں۔ ` PURCHASE ` normalizedtypeبھیpayloadgateنہچھوڑے۔
4. Storednameبدلیں،callerobjectپراناہو؛نئےdraftcreator/auditsمیںstoredname۔ Existingdraftcreatorupdateسےنہبدلے۔ دوسریجائزpersonکےdraftکوکھولنا/updateاببھیجائز۔
5. Wrongkind/missing/unknown/terminaldraft/heldIDsمیںnooverwrite/noextraaudit۔ Heldcountreadfault=`Ok=false`،successfulzeroنہیں۔

### 7.1 / 15.1 — کھلے drawers اور callbacks

1. Draftdrawer/parkedlistکھلی،selectionموجود؛module/accountrevoke۔ اگلےaction/nominaltickپرrows/selectionصاف،regrantپراناselectionخودنہلائے۔ Failedlistکو"No open draft/No parked carts"کیصحیحخالیفہرستنہکہیں۔
2. StableallowedtickselectionاورauthoredunsavedPOScartنہیںمٹائے۔ DraftwindowClosedاورPOSviewUnloadedپرتعلقہtimerبند۔ اصلWPFtiming/focus/bindings/reentryیہاںUNRUN۔
3. DraftPost/Discardconfirmationکھلیہو؛Ownerاجازتبدلے۔ Yesکےبعدcurrentcheckrefuse۔ Refreshسےہٹیہوئیrowکاoldcallbackبھیrefuse۔ PurchasepayloadکیCostlossمیںmetadataفہرستجائزہوسکتیہےمگرOpen/Postنہیں۔
4. Draftpost/discardکامیاب،اگلیrefreshfault: **SAVED،دوبارہpostنہیں**؛unknownقبلackexceptionمیںاصلdocument/draftدیکھےبغیرretryنہیں۔

### 24.1 / 25.1 — واضح باقی حدود

1. `DraftHeldCurrentAccessTests`کے136اورپوری2615suiteکےاصلWindowslogsرکھیں۔ Denialکےبعدتمامsnapshotبرابر؛جائزdraft/holdعملسےsale/purchase/payment/stockنہیںبدلتے۔ Source/PythonchecksC#executionنہیں۔
2. **Draftpremark/Create/finalizeاببھیالگtransactionsہیں**؛restore/finalizefault،crash/parallel/ambiguousCOMMITاورdurableidentityکیالگجانچباقی۔ Interruptedmarkerکوdocumentverifyکیےبغیرنہیںdiscardکرنا۔
3. **HeldResume DBمیںconsumeکےبعدUIcartreconstructionکرتاہے۔** Reconstruction/refreshcrash،restartrecoveryاورعامHoldکےpostcommitUIfaultsاسحصےمیںحل/تصدیقنہیںہوئے۔ Holdمیںکھلےdraftکےساتھduplicate-intentpolicyبھیالگreviewہے؛یہglobalexactlyonceنہیں۔
4. FullPOScart/pickerprivacy/masking،quickadd/session/preparedoutputs،دیگرstock/expense/recoveryPR/filefaults،26–29restore/cloudاور2/4/30–35Windows/hardware/isolationباقی۔

**36۔ مجازsingle-doubleclicktestinstallerابھی نہیںبنا؛FullPhase4OPEN۔** ہرportiondocs+samebranchcommit/push+actualremoteverification۔36categories،36Nextنہیں؛3–6integratedroundsمشروطاندازہ،deadlineیاupperboundنہیں۔
