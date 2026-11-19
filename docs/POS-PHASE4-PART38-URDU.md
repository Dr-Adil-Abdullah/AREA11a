# ⏳ فیز4 — حصہ38: خرچ، مالک کی drawing اور stock posting کے بعد محفوظ retry

**⏳ v0.116.0 Tarn / PT-58 / مستقل20،23،24،25 — source review، اصل Windows تصدیق باقی۔**

✅ رپورٹ کا اصول: ✅ صرف متعلقہ کام100%مکمل؛ ⏳ کام شروع/کچھ باقی؛ ❌ بالکل شروع نہیں۔ اس حصے میں source تبدیلی اور مقامی checks کو پوری feature کی تکمیل نہیں کہا گیا۔

## ⏳ روزمرہ مثال اور اس حصے کی تبدیلی

- ⏳ **20 — خرچ:** آپ نے کرایہ Rs30 محفوظ کیا۔ DB نے کامیابی بتائی، مگر صفحہ صاف کرتے یا فہرست تازہ کرتے error آ گیا۔ اب **SAVED** کا مطلب پہلے خرچ درج ہے؛ اسی رقم کو دوبارہ Save نہیں کرنا۔ پہلے history دیکھیں۔ اگر صفائی ادھوری ہے تو اسی page پر دوسری posting بند رہتی ہے؛ تصدیق کے بعد page دوبارہ کھولیں۔
- ⏳ **20 — مالک کی cash/goods drawing اور Write-off:** cash روپے drawer سے نکلتے ہیں؛ goods drawing مال کو cost پر کم کرتی ہے، drawer سے رقم دوبارہ نہیں نکالتی۔ Write-off stock/loss کا پرانا حساب رکھتا ہے۔ کامیابی ملنے پر guard پہلے لگتا ہے، پھر quantity/amount/note صاف اور فہرست refresh ہوتی ہے۔ پرانی entry کی صفائی خراب ہو تو وہ entry دوبارہ نہیں جاتی۔
- ⏳ **20 — Stock-take Apply:** کامیابی ملنے کے بعد پرانا session/selected line پہلے چھوڑتے ہیں، پھر display refresh۔ پرانا once-only Core rule برقرار: applied session دوبارہ Apply نہیں ہوتا۔ Apply نامعلوم ہو تو اسی page کے Open/Count/Clear count/Cancel/Add found بھی رکتے ہیں، تاکہ پرانی session حالت بدل کر غلطی نہ ہو۔ ان دوسرے actions کا اپنا fault-retry redesign اس حصے میں نہیں ہوا۔
- ⏳ **23 — کیا لکھا ہی نہیں گیا؟** Service صرف واضح preflight refusal یا COMMIT سے پہلے کامیاب rollback پر `NoCommitConfirmed` دیتی ہے۔ عام exception کا مطلب خود بخود “Nothing written/changed” نہیں۔ COMMIT کی کوشش کے بعد error ہو، یا rollback ثابت نہ ہو، تو **outcome unconfirmed؛پہلے history verify**۔ Error کے الفاظ دیکھ کر retry نہیں کھلتا۔
- ⏳ **25 — یہ local حفاظت ہے:** ایک page کی synchronous/reentrant posting، unknown outcome اور acknowledged-but-not-reset حالت روکتی ہے۔ Page بند/restart یا دوسرے PC پر یہ guard محفوظ نہیں رہتا؛ durable posting ID، cross-PC exactly-once یا global duplicate prevention کا دعویٰ نہیں۔ Unknown صورت میں page دوبارہ کھول کر اندھا Save کرنا اب بھی غلط ہے۔

## ✅ اس bounded حصے میں جان بوجھ کر برقرار رکھا

- ✅ expense/drawing/write-off/Apply کے financial SQL string literals اور private financial helper tails پہلے جیسے ہیں؛ migration، قیمت، Extra/ReturnB، SupplierB، permission policy، Blackbox/BookCheck internals نہیں بدلے۔
- ✅ خرچ کی stock-category صرف label ہے؛ receiving یا Purchase نہیں بنی۔ expense کا کوئی نیا automatic Draft workflow نہیں بنایا۔
- ✅ frozen stock-take cost valuation، uncounted lines نہ چھیڑنا، GINTI found batch اور applied session کا once-only rule برقرار رکھا۔ یہ source preservation کا نتیجہ ہے، Windows runtime سند نہیں۔

## ✅ یہاں کیا چلا / ⏳ کیا نہیں چلا

- ✅ نیا disposable Python/SQLite probe: **37 migrations،34 BEFORE/AFTER statement fault rollbacks،ہر fault کے بعد ایک retry**؛ پورا SQL dump، stock/business/audit rows اور synthetic cash-out identities ملیں۔ اصل SQL source سے پڑھتا ہے، مگر plan/transaction/authority/terminal gate Python کے ہیں؛ C# service نہیں چلی۔
- ✅ نیا lexical negative probe: **33 source mutations رد،اصل bytes واپس**۔ Guard/observation/cleanup contract کے الفاظ/order چیک ہوتے ہیں؛ یہ C# semantics یا GUI execution کا بدل نہیں۔
- ⏳ **59 نئے C# regression cases**:34 statement faults،10 invalid/inactive refusals،2 second-line faults،1 frozen-cost case،12 local-state cases؛ suite **2744**۔ یہ source لکھا ہے؛ یہاں .NET SDK نہیں، اس لیے compile/run نہیں ہوا۔ Fixture/schema/trigger assumptions بھی اصل runner پر جانچنے ہیں۔
- ✅ آخری مقامی run:105staticchecks،429C#grammarfiles،4761calls/9765refsمیں0staticissues؛12source-negativescriptsکے349mutationsرد/restore؛تمام29SQLiteprobesPASS؛backupreference6roundtrips/24negatives،rulesmatrixاورdiffcheckPASS۔ GuideMD/HTML/36anchors،stable1–36،57boardrowsاور37migrationsبرقرار۔ یہ runtime سند نہیں۔
- ⏳ Windows/WPF/bindings/dialog callback/actual provider COMMIT/production journal/power-cut/parallel ثبوت باقی۔ مکمل مقامی حدود [⏳ trace](POS-PHASE4-EXPENSE-STOCK-FAULT-REVIEW.md) میں ہیں۔

## ⏳ PT-58 — صرف مکمل الگ test ماحول میں

**⏳ پہلے verified encrypted backup اور واقعی الگ DB/photos/config بنائیں۔ صرف exe کا دوسرا folder isolation نہیں۔ اصل دکان پر synthetic خرچ/stock faults نہ چلائیں۔**

1. ⏳ **2/4:** `tools\RUN-TESTS.cmd` چلائیں؛ نئے59 اور پوری2744suite کے build/TRX/result logs رکھیں۔ Failures ہوں تو پہلے fix/retest؛ source count کو PASS نہ سمجھیں۔
2. ⏳ **20/24:** الگ synthetic drawer Rs1000 سے کھولیں، rent Rs30 اور cash drawing Rs40 کے الگ cases؛ expected drawer بالترتیب Rs970/Rs960۔ Goods drawing2×Rs10 اور write-off2×Rs10 میں stock10→8،drawerRs1000ہی رہے۔ کوئی Purchase/payment کی اضافی row نہ ہو۔ ہر statement fault پر whole-state rollback، پھر ایک جائز retry، ایک business/audit/movement trail verify کریں۔
3. ⏳ **20/24:** Stock-take میں count10→8؛ دوسرے found medicine کی دوسری movement پر fault لگے تو پہلی stock تبدیلی بھی واپس۔ Trigger ہٹاکر Applyایکبار؛دوسراApplyنہیں۔ Countکےبعدcostبدلیں،valuation frozen cost سے؛uncounted medicine unchanged؛foundGINTIbatch۔
4. ⏳ **23/25:** Service failure کی دو یکساں wording مگر مختلف explicit no-commit حالتیں؛صرف confirmed حالت دوبارہ کوشش کھولے۔ Service throw/COMMIT-attempt/rollback-unconfirmed میں unknown verification warning، اسی page پر posting بند۔ اصل provider COMMIT injection الگ باقی test ہے، SQL statement trigger اس کا بدل نہیں۔
5. ⏳ **20/25:** ہر expense/cash/goods/write-off/Apply success کے بعد پہلی/درمیانی field clear،custom editor reset،binding،history/batch refresh اور status callback fault۔ کامیاب result کو **SAVED** کہیں؛اسی entryکیدوسریpostingنہہو۔ `Ok=true` مگر resultغائب ہو تو بھی acknowledged save سمجھ کر blocked رہیں،unknown/newcopyنہیں۔
6. ⏳ **25:** reentrant action دوران service/cleanup روکیں۔ Clean reset کے بعد واقعی نئی amount/quantity سے اگلی جائز entry چلے؛اس guard کو amount-baseddedupنہبنائیں۔ Callback fault صرف final status کے بعد ہو اور تمام inputs پہلے consumed ہوں تو اگلا خالی form محفوظ ہے؛WPFپرثبوتدرکار۔
7. ⏳ **23/25:** Unknown Apply کے بعد اسی page پر Count/Clear/Cancel/Open/Addfound بند؛history/refresh پڑھنا جائز۔ Acknowledged Apply میں پرانا session null ہونے کے بعد refresh failure سے وہ ID دوبارہ Apply نہ ہو۔ Reopen سے پہلے history verify؛restart/globalidentity الگ unresolved۔

## ⏳ مستقل1–36 اور باقی کام

- ⏳ پوری تفصیلی فہرست: [1–36](NUMBERED-WORK-STATUS-URDU.md)۔ اس حصے کا بنیادی اضافہ20/23–25 ہے؛source/tests/docs آئے، Windows/full-scope ثبوت باقی۔
- ⏳ اگلا bounded review: باقی recovery/PR/file fault paths اور durable identity/parallel/provider ambiguity؛ساتھ remaining caller/session/CAS review۔ نئی policy درکار ہوئی تو پہلے مثال اور منظوری، خود سے workflow نہیں بدلنا۔
- ⏳26–29 encrypted backup/restore/new-PC/cloud کی اصل execution؛2/4/30–35 Windows build/results/WPF/hardware/isolation ابھی باقی۔
- ❌36 single double-click test installer کی binary ابھی نہیں بنی۔ Build/tests/package/isolation/smoke gates باقی؛authorization readiness کا ثبوت نہیں۔
- ⏳ FullPhase4 OPEN؛36categories≠36Nexts۔3–6 مربوط rounds صرف مشروط اندازہ،deadline/upperbound نہیں۔ ہر حصے کے docs،اسی active branch پر commit/push اور الگ actual remote hash verification لازم۔
