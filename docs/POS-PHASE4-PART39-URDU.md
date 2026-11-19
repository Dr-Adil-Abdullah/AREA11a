# ⏳ فیز4 — حصہ39: No-bill return (PR) میں fault،cleanup اور retry

**⏳ v0.117.0 Bracken · 2026-11-10 · PT-59 · مستقل17،19،23–25 — source موجود،Windows ثبوت باقی۔**

✅ علامتوں کا اصول:✅صرف متعلقہ کام100%مکمل؛⏳کچھ باقی؛❌شروع نہیں۔ Source/probe کا نتیجہ پوری feature کا runtime PASS نہیں۔

## ⏳ روزمرہ مثال اور source میں کیا بدلا؟

- ⏳ **17/25 — Save:** گاہک بغیر bill دو دوائیں واپس لایا؛PR محفوظ ہو گیا،stock واپس اور cash refund درج ہو گیا،مگر page صاف یا print نہ ہوا۔ کامیاب service جواب ملنے پر پہلے acknowledgement guard لگتا ہے،پھر tray/refund/customer/notes صاف ہوتے ہیں۔ بعد کا error **SAVED** بتاتا ہے؛دوسرا refund/PR نہیں کرنا۔
- ⏳ **17/25 — Attach bill:** Owner/Manager نے درست bill attach کیا؛allocations محفوظ،کوئی دوسرا stock/payment event نہیں۔ اب target ID/caption service سے پہلے pin؛کامیابی پر پرانا target callback سے پہلے چھوڑتے ہیں۔ Load خود اگلی pending entry منتخب کر دے تو اسے بھی چھوڑتے ہیں،تاکہ پرانا bill/reason غلط entry پر نہ لگے۔
- ⏳ **17/25 — Cancel:** کامیاب cancellation کے بعد پرانا target،bill lookup اور دونوں decision fields صاف؛refresh کی خرابی اسی entry کو دوبارہ cancel کرنے کی دعوت نہیں۔ **Cancellation پہلے سے دیا cash خودبخود واپس نہیں لاتی**؛پرانا cash/payment trail برقرار ہے۔ یہ نئی مالی policy نہیں۔
- ⏳ **23 — no-commit:** تینوں writers میں موجودہ TransactionOutcome استعمال۔ Preflight refusal یا COMMIT سے پہلے کامیاب rollback ہی `NoCommitConfirmed`۔ COMMIT-attempt/rollback-unconfirmed/throw میں “Nothing changed” نہیں؛پہلے history/stock/payment verify۔ Technical exception کا raw متن UI میں نہیں،اصل exception route/PR-ID کے ساتھ log میں رہتی ہے۔
- ⏳ **25 — local حفاظت:** ایک مشترک page guard Save/Link/Cancel کی reentry اور unknown/unfinished cleanup میں اگلا write روکتا ہے؛Add/Remove tray بھی رکتے ہیں۔ Clean reset کے بعد نیا جائز کام چل سکتا ہے۔ یہ restart،page reopen،دوسرے PC یا direct Core caller کی durable identity نہیں؛unknown کے بعد reopen کر کے اندھا Save اب بھی غلط ہے۔

## ✅ اس حصے میں کیا برقرار رکھا؟

- ✅ PR financial SQL literals،private Save/reconciliation/refund-allocation helpers اور Link/Cancel کے اندرونی transaction bodies پچھلے حصے جیسے ہیں۔ Migration یا historical data rewrite نہیں۔
- ✅ Create میں stock/payment صرف ایک event؛Link میں صرف reconciliation؛Cancel میں اسی batch سے stock واپس نکلتا ہے،نیا cash-in/payment نہیں۔ Zero refund،integer-paisa remainder،OPEN fallback batch،legacy unreconciled link کا same-bill rule برقرار۔
- ✅ Sale/Return/Extra/ReturnB/SupplierB،Blackbox/BookCheck،backup/cloud یا permission policy نہیں بدلی۔ PR کے لیے نیا automatic draft نہیں بنایا۔

## ✅ مقامی جانچ / ⏳ اصل execution

- ✅ نیا Python/SQLite probe:37 migrations،**36 BEFORE/AFTER statement fault rollbacks اور ایک retry**؛دو lines،OPEN batch،refund remainder،allocations،Link/Cancel repeat refusal،payment نہ بدلنا اور اصل drawer SELECT چیک۔ Transaction/plan/authority/terminal gates Python کے ہیں،C# allocation algorithm نہیں چلا۔
- ✅ نیا source-negative probe: **29 lexical mutations** رد،اصل bytes بحال۔ یہ source contract/order کی جانچ ہے،C# semantics/WPF proof نہیں۔
- ⏳ **45 نئے C# regression cases**:36faults،6refusals،2legacy-link،1zero-refund؛پوری suite **2789**۔ یہاں .NET SDK/Windows نہیں؛compile/run،fixture/trigger assertions اور actual provider verification باقی۔
- ✅ آخری مقامی run:106staticchecks،430C#grammarfiles،4765calls/9782refsمیں0staticissues؛تمام13source-negativescriptsکے378mutationsرد/restore؛تمام30SQLiteprobesPASS۔ Backupreference6roundtrips/24negatives،rulesmatrix/diffcheckPASS؛37migrations،پرانے139testpathsاورfinancialhelpersبرقرار۔
- ⏳ مکمل حدود [trace](POS-PHASE4-PROVISIONAL-POSTING-REVIEW.md) میں؛یہ checks C#/WPF/provider execution یا security certification نہیں۔

## ⏳ PT-59 — الگ test ماحول میں عملی ثبوت

**⏳ پہلے verified encrypted backup اور واقعی الگ DB/photos/config؛صرف exe folder الگ کرنا کافی نہیں۔ اصل دکان پر synthetic PR،refund یا fault نہ چلائیں۔**

1. ⏳ **2/4:** تازہ version/branch پر `tools\RUN-TESTS.cmd`؛2789suite کے build/TRX/result logs،پھر failures کی fix/retest۔ Source count خود PASS نہیں۔
2. ⏳ **17/19/24:** دو synthetic medicines،ہر ایک10units،costRs10/retailRs15،drawerRs1000۔ Create میں دونوں کی1unit واپسی اورrefundRs30.01؛stock11+11،line refundsRs15.01/Rs15.00،ایکPR payment−3001paisa،drawerRs969.99۔ Zero refund میں goods آئیں،cash payment نہ بنے۔
3. ⏳ **17/19/24:** الگ case میں پہلے دونوں کی3units sale،پھر1+1 PR؛Attach سے stock/payment دوبارہ نہ بنے،allocations بنیں،اصل sale rows نہ بدلیں۔ Cancel الگ pending case میںstockکم کرےمگرcashrefundباہرہی رہے؛کوئی نیاcash-inنہبنائے۔ Actual sale paid کے مطابق drawer ملا کر دیکھیں۔
4. ⏳ **24:** Create header/batch/movement/item/refund-allocation/payment/audit،Link allocation/header/audit،Cancel batch/movement/header/audit پرBEFORE/AFTER faults۔ دوسری line/دوسریallocation پر fault پہلی کو بھی rollback کرے؛trigger ہٹاکر ایکretry۔ Snapshot/number/payment/stock/audit سب reconcile۔
5. ⏳ **17/25:** Service success کے بعد پہلی/درمیانی field clear،selection notification،Lines.Clear،Load/lookup/binding/print/status callback fault۔ **SAVED** رہے،دوسرا PR/refund/attach/cancel نہ ہو۔ Ok=true مگر result غائب ہو تو بھی acknowledged block۔
6. ⏳ **25:** Link/Cancel کے بعد Load اگلی pending row چنےتو کوئی target منتخب نہ رہ جائے؛BillCode/CancelReason دونوں خالی،lookup انتخاب بھی خالی۔ نئی entry پر action صرف نئی explicit selection اور input سے۔ Partial cleanup میں اسی page پر دوسرے writer/tray edit بھی بند۔
7. ⏳ **23/25:** failure کی wording ایک جیسی مگر explicit no-commit مختلف ہو؛صرف known حالت retry کھولے۔ اصل provider COMMIT/rollback-error injection الگ کریں؛statement trigger اسے ثابت نہیں کرتا۔ Unknown پرhistory/stock/paymentverify،reopenسےپہلےجائزہ؛restart/globalidentity الگ باقی۔
8. ⏳ **17:** پرانے linked-without-allocation record کو اسی bill سے reconcile؛دوسرا bill رد؛Link/Cancel کا دوبارہ terminal action کوئی نیا event نہ بنائے۔ پہلے سے موجود permission/reader/receipt routes کی current-account جانچ اس حصے سے certify نہیں ہوئی۔

## ⏳ باقی مستقل نمبر اور اگلا دائرہ

- ⏳ تفصیلی [مستقل1–36 فہرست](NUMBERED-WORK-STATUS-URDU.md) اپڈیٹ؛یہ حصہ17/19/23–25 تک محدود ہے۔
- ⏳ **5–7/17/21:** PR کے current stored actor،reads/search/receipt outputs،failed read≠empty اور stale selection/access boundaries الگ review مانگتے ہیں؛اس حصے میں پرانی authorization policy نہیں بدلی،اسے security completion نہ کہیں۔
- ⏳ **23–25/28:** provider COMMIT/rollbackfailure،parallel،durable identity اور باقی recovery/file fault paths؛**26–29**backup/restore/newPC/cloud؛**2/4/30–35**Windows/WPF/hardware/isolation باقی۔
- ❌ **36:** sendable single double-click installer binary نہیں بنی؛build/tests/package/cleaninstall/isolation/smoke gates باقی۔ ⏳FullPhase4 OPEN۔
- ⏳ ہرportiondocs+اسیactivebranchcommit/push+الگactualremotehashverification۔36categories≠36Nexts؛3–6roundsمشروطاندازہ،deadline/upperboundنہیں۔ نئیpolicyدرکارہو تو پہلے مثال/منظوری۔
