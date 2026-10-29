# Area11 — Real-life Physical Testing Log

یہ مالک کی عملی ٹیسٹنگ، تجاویز اور درخواست کردہ تبدیلیوں کی مستقل فائل ہے۔ ہر نتیجہ اور نئی بات اسی فائل میں شامل ہوگی تاکہ testing کے اختتام پر کوئی نکتہ نہ بھولے۔

## کام کا اصول

- Testing کے دوران پہلے واقعہ، توقع، حقیقی نتیجہ اور مطلوبہ تبدیلی **record** ہوگی۔
- مالک نے 27 اکتوبر کو **“fix the things”** کہہ کر درج شدہ fixes کی اجازت دی ہے؛ کوئی نکتہ خاموشی سے fixed یا rejected نہیں لکھا جائے گا۔
- Code fix، static check اور owner physical verification الگ الگ لکھے جائیں گے۔ مالک کے **next** پر اگلا physical-test portion بتایا جائے گا۔
- اصل دکان کے database میں صرف حقیقی لین دین؛ فرضی یا خطرناک مالیاتی cases کے لیے الگ test copy۔
- ہر item کی حالت: **Recorded → Planned → Fixed → Owner verified**۔
- پرانے owner notes نہیں مٹائے جائیں گے؛ نئی تصدیق کے ساتھ status اور تاریخ شامل ہوگی۔

## موجودہ build اور automated ثبوت

- Build: **v0.38.0 Cobalt `#315C8C`** (پہلا Phase-3 packaging/variant/search slice؛ Windows build/xUnit اور PT-10 physical verification ابھی باقی ہے)
- Latest owner xUnit: **883 passed / 0 failed / 0 skipped / 883 total — ALL GREEN**، **Zircon v0.36.0**، 29 اکتوبر 2026، مدت **1 منٹ 10 سیکنڈ**۔ Cobalt کے 900-case run یا Aurora کے 892-case run کو اس نتیجے سے replace نہیں کیا جا سکتا۔
- موجودہ source: **900 written cases in 70 files**؛ **55/55 static checks PASS** (real C# grammar سمیت)۔ Latest gate: **291 C# files / 1,457 XAML bindings / 29 migrations / 37 business tables / 834 prepared SQL statements / 123 settings keys**۔ Phase-3 check pack editor، shared Products/Counter/Purchase search، extra barcodes اور variant picker کی حفاظت کرتا ہے؛ actual search SQL کا targeted **7/7 SQLite development probe** بھی کامیاب ہے۔ **Cobalt Windows xUnit اور PT-10 physical walk-through باقی ہیں**؛ Aurora PT-09 اور Zircon PT-08 الگ evidence ہیں۔
- Windows build report میں `AlertKindTests.cs(121,19)` کا `productId` scope compile error ملا؛ وہ `aedb65d` میں ٹھیک push ہو چکا ہے۔ بعد کا build xUnit تک پہنچا؛ حتمی 883/883 نتیجہ نیچے محفوظ ہے۔
- تازہ Windows build report میں `PosViewModel.cs(924,21)` کا **CS0103: `MessageBoxDefaultButton` does not exist** ملا۔ WPF کے صحیح overload کے لیے اسے **`MessageBoxResult.No`** سے بدلا گیا ہے، اس لیے default جواب No/normal retail رہتا ہے۔ Static gate دوبارہ PASS ہے؛ اگلا Windows build xUnit تک پہنچا، مگر اس وقت full green نہیں ہوا؛ حتمی نتیجہ نیچے محفوظ ہے۔
- تازہ Windows xUnit run: **879 passed / 4 failed / 883 total**۔ Reported HoldCart errors میں below-cost fixtures تھے: Brufen کی test retail cost سے کم تھی، اور ایک custom strip rate پورے strip کی cost سے کم تھا۔ ان غیرمتعلق HoldCart/Return fixtures کو درست rates دیے گئے ہیں؛ production below-cost lock اور اس کے dedicated refusal tests نہیں بدلے۔ اس وقت full run باقی تھا؛ حتمی 883/883 نتیجہ نیچے محفوظ ہے۔
- اگلا Windows xUnit run: **882 passed / 1 failed / 883 total**۔ واحد reported failure `CatalogUnitTests.AdditionalCategoryTagsFilterOnce_ButFinancialTotalsUsePrimaryOnly` تھا: expected 300، actual 200۔ Code review میں second product کا batch retail 300 مگر product retail 200 ملا؛ SaleService کے درست product-rate چارج کے مطابق fixture کو 300 پر align کیا گیا ہے۔ Report category rule نہیں بدلا؛ اس وقت full green run باقی تھا۔
- **تاریخی Windows xUnit verification — 27 اکتوبر 2026، Spruce source `94723e5`: 883 passed / 0 failed / 0 skipped / 883 total — ALL GREEN، مدت 1 منٹ 19 سیکنڈ۔** یہ Spruce کا Windows build/xUnit ثبوت ہے؛ latest Zircon 883/883 اوپر الگ درج ہے۔ WPF screens، genuine transactions، isolated restore اور real Excel/cloud checks کو ان نتائج سے automatically verified نہیں لکھا گیا۔
- 27 اکتوبر CAT-01/NAV-01/UNIT-01/PHOTO-01 code batch: primary+filter tags، managed units اور descriptive strength، product/counter thumbnails، اور dashboard/category/company drill-downs شامل ہیں۔ Migrations `027`–`028` اور **6 نئے regression cases** شامل ہیں؛ یہ code changes ہیں، owner physical verification نہیں۔
- Automated مدت: **1 منٹ 17 سیکنڈ** (پرانے tested Obsidian run کی)
- Physical testing شروع: **27 اکتوبر 2026**
- Cloud sync: اس physical-testing مرحلے میں فی الحال شامل نہیں۔

## Testing summary

| Test | حالت | مختصر نتیجہ |
|---|---|---|
| **PT-01 — Startup, backup اور UI smoke** | **PASS — improvements logged** | ایپ کھلی، photos کے ساتھ backup بنا، اور تمام مقررہ pages کھلے |
| **PT-02 — حقیقی medicine اور product details** | **PASS — owner confirmed 29 Oct 2026** | مالک نے “2 is ok” کی تصدیق کی؛ الگ logged feedback items اپنی اپنی حالت میں رہیں گے |
| **PT-03 — حقیقی purchase، batch، expiry اور payable** | **PARTIAL — owner feedback logged، report pending** | ابھی تفصیلی PINV/stock/payable evidence موصول نہیں ہوا؛ PASS نہیں لکھا گیا |
| **PT-04 — Sale، cash/khata، receipt اور drawer** | **INSTRUCTIONS READY** | حقیقی فروخت، price/cart، receipt اور completion feedback |
| **PT-05 — Returns، PR اور cancellation** | Pending | الگ test copy کے ساتھ محفوظ جانچ |
| **PT-06 — Shift، ledgers، permissions اور audit** | Pending | مالیاتی اور user workflows |
| **PT-07 — Excel، restore، printer اور scanner** | Pending | Hardware اور data workflows |
| **PT-08 — Redesign Phase 1: dark header اور navigation** | **883/883 ZIRCON xUNIT PASS — WPF verification pending** | Automated suite کامیاب؛ header، Settings sections، category filter، keyboard اور permissions کی physical جانچ باقی |
| **PT-09 — Redesign Phase 2: field governance اور schema** | **CODE READY — Aurora Windows/WPF verification pending** | Stable keys، central validation، guidance، migration 029 اور existing answers |
| **PT-10 — Redesign Phase 3: packaging، variants اور search** | **CODE READY — Cobalt Windows/WPF verification pending** | Existing pack label، Products/Counter/Purchase discovery، separate variant selection اور unchanged stock conversion |

---

# PT-08 — Redesign Phase 1: expanded dark header

**حالت:** Zircon Windows xUnit **883/883 PASS** (29 اکتوبر 2026، 1 منٹ 10 سیکنڈ) — مالک کی WPF/navigation verification pending

**اہمیت:** تمام modules کا مشترکہ راستہ؛ کوئی page یا permission نظرانداز نہیں ہونا چاہیے۔

**حدود:** صرف shell/navigation بدلا گیا ہے۔ Sale، purchase، stock, payment، return، printing، WhatsApp، schema اور cloud rules نہیں بدلے۔

## Automated ثبوت

- **53/53 static checks PASS**، real C# grammar سمیت۔
- **1,452 XAML bindings** resolve؛ Zircon پر **883/883 Windows xUnit PASS** (1 منٹ 10 سیکنڈ)۔ یہ WPF navigation یا PT-09/Aurora evidence نہیں۔
- Dedicated check retired 176px sidebar/editable shell title کو واپس آنے سے روکتا ہے اور header collections/commands/Settings/category separation کی حفاظت کرتا ہے۔

## مالک کی physical checklist

1. **Build اور automated suite:** `START-AREA11.bat` سے Zircon کھولیں، پھر `tools\RUN-TESTS.cmd` چلائیں۔ مطلوبہ: **`ALL GREEN - 883 of 883`**۔ Build یا test failure ہو تو وہی error/report بھیجیں؛ کسی نتیجے کو pass نہ لکھیں۔
2. **Header identity:** اوپر صرف **Area11** نظر آئے؛ shop name مرکز میں نہ ہو۔ **Zircon رنگ** نئی copy کی پہچان کرے۔
3. **Bell:** سب سے دائیں اوپر بڑا bell ہو۔ اس کے تینوں موجودہ colours/count کو expiry/low-stock یا کسی محفوظ genuine alert سے جانچیں؛ click سے Alerts page کھلے اور واپسی پر previous module دوبارہ منتخب ہو سکے۔
4. **Modules اور submodules:** Dashboard، SELL، STOCK، CUSTOMERS & SUPPLIERS، MONEY اور SYSTEM کلک کریں۔ ہر module کا پہلا page کھلے؛ واپس آنے پر اس module کا موجودہ page برقرار رہے۔ Active module اور active page واضح ہوں۔
5. **Settings sections:** SYSTEM → Settings کھولیں۔ Shop، Printing، Sales rules، Cloud sync، Staff & permissions، App words چھوں اوپر clickable ہوں، اندرونِ صفحہ دوسری tab strip نہ ہو۔ System کے عام pages بھی دستیاب رہیں تاکہ Settings سے سیدھا Backup/Audit یا دوسرا page کھولا جا سکے۔ ایک section میں غیر محفوظ text لکھ کر دوسرا section اور واپسی کریں؛ text ضائع نہ ہو۔ Save صرف ایک بار اور موجودہ rules کے مطابق ہو۔
6. **Products folder/category:** STOCK → Products میں header کا category dropdown کھولیں، keyboard سے نام لکھ کر category منتخب کریں، اور نتیجہ وہی products دکھائے۔ All categories پر واپس آئیں۔ Dashboard/category row سے آنے پر header میں وہی category منتخب نظر آئے۔
7. **Editor/filter separation:** کسی product کی editor category نوٹ کریں، پھر header سے دوسری category filter کریں؛ editor کا unsaved category field نہیں بدلنا چاہیے۔
8. **Put-away edge case:** الگ test copy میں filter کی گئی category کو Manage categories سے put away کریں؛ filter All categories پر صاف ہو، پرانا invisible filter باقی نہ رہے۔ اصل books میں destructive test نہیں۔
9. **Permissions:** Cashier/limited test user سے sign in کر کے confirm کریں کہ صرف permitted modules/pages دکھیں اور owner-only routes نہ کھلیں۔ کیونکہ header میں چھپانا security نہیں، existing service refusal بھی برقرار رہنا چاہیے۔
10. **Keyboard اور screen fit:** Tab/Enter سے modules، Settings اور bell تک پہنچیں؛ focus واضح ہو۔ 1240×760 اور اپنی screen resolution پر کوئی button clip/overlap نہ ہو؛ SYSTEM کے تمام pages قابلِ رسائی ہوں۔
11. **Existing safeguards:** Alerts، session warning، empty-page message اور کسی page کے failed-button/fault card کو موجودہ محفوظ طریقے سے جانچیں۔ Header کی وجہ سے page content یا half-typed bill ضائع نہیں ہونا چاہیے۔
12. **Version menu:** current-user menu سے My settings اور Sign out کھلیں، اور صحیح **v0.36.0 · Zircon** نظر آئے۔

**Owner result:** Pending — کوئی physical PASS ابھی record نہیں۔

---

# PT-09 — Redesign Phase 2: Central Field Governance & Schema

**حالت:** Code changed — Aurora کی Windows/xUnit اور owner physical verification pending

**مقصد:** Own fields کی identity، definition، validation اور guidance ایک central contract سے چلیں؛ پرانے جوابات محفوظ رہیں۔

**حدود:** یہ packaging/variants/purchasing/search یا financial test نہیں۔ Stock، prices، payments، historical documents اور permissions کے موجودہ قواعد نہیں بدلنے چاہئیں۔

## Automated ثبوت

- **54/54 static checks PASS**، real C# grammar سمیت۔
- **892 written cases in 69 files**، جن میں Phase-2 کے 9 regressions شامل ہیں؛ مطلوبہ Windows نتیجہ **`ALL GREEN - 892 of 892`** ہے۔
- Migration 029 fresh SQLite پر چلتی ہے اور upgrade regression ثابت کرتی ہے کہ legacy key backfill کے دوران answer نہیں بدلتا۔ یہ Aurora کے Windows run یا WPF proof کا بدل نہیں۔

## مالک کی physical checklist

1. **Build اور suite:** `START-AREA11.bat` سے **v0.37.0 · Aurora** کھولیں اور `tools\RUN-TESTS.cmd` چلائیں۔ Total **892**، failed **0** ہونا چاہیے۔ 883 والا پرانا Zircon نتیجہ اس release کے لیے کافی نہیں۔
2. **محفوظ backup:** اپنا موجودہ database کھولنے سے پہلے backup رکھیں۔ Migration کے بعد existing products، customers، suppliers اور custom answers موجود ہوں۔ Upgrade/fault test ہو تو isolated copy استعمال کریں۔
3. **نیا field اور stable key:** SYSTEM → **Own fields** → Medicines میں کوئی حقیقی optional text field بنائیں، مثلاً `Shelf note`، اور **Help on the record form** میں ایک سادہ یک سطری ہدایت لکھیں۔ Save کے بعد stable key `product.custom.<id>` نظر آئے؛ یہ key پڑھ کر لکھ لیں۔
4. **Form پر ظاہر ہونا:** کسی existing حقیقی product کو کھولیں۔ نیا field اور وہی guidance نظر آئے؛ کوئی rebuild/restart نہ چاہیے۔ جواب لکھ کر **Save extra fields** کریں، product بند کر کے دوبارہ کھولیں؛ جواب اور guidance برقرار رہیں۔
5. **Rename-safe identity:** Own fields میں اسی field کا نام بدلیں، مثلاً `Shelf note` سے `Rack note`۔ **Stable key بالکل نہیں بدلنی چاہیے**، پرانا جواب موجود رہے، اور Black box میں old → new definition نظر آئے۔
6. **Help edit:** صرف guidance بدل کر Save کریں۔ Record کے جواب میں کوئی تبدیلی نہ ہو؛ نئی guidance نظر آئے اور audit میں change ہو۔ اگر صرف نام/type بدلیں تو existing guidance اپنے آپ نہ مٹے۔
7. **Required اور atomic refusal:** الگ test copy میں required field بنائیں۔ خالی required answer کے ساتھ دوسرا optional answer بھر کر Save extra fields کریں؛ واضح field-named refusal آئے اور optional answer بھی partial save نہ ہو۔ بعد میں valid answer سے save ہو۔
8. **سات types:** isolated copy میں text، number، amount، date، list، yes/no اور photo کے safe examples جانچیں۔ Amount دوبارہ کھولنے پر درست rupees دکھے، date canonical رہے، list صرف defined choices قبول کرے، photo gallery کا existing rule رہے۔
9. **Invalid input:** number field میں `NaN` اور `Infinity`، name/help میں multiline/control text، اور list میں undefined choice آزمائیں۔ واضح refusal آئے، کوئی خاموش `0` یا غلط stored value نہ بنے۔
10. **Put away / restore:** field کو **Put this field away** کریں؛ form سے ہٹے مگر answer database میں رہے۔ **Show it on the form again** پر وہی key اور پرانا answer واپس آئے۔ Delete/undo جیسا کوئی خطرناک راستہ نہ ہو۔
11. **Permissions:** Cashier/limited login سے Own fields manage نہ ہو سکے۔ Existing record permission والے user کو صرف اجازت شدہ entity کے answers ملیں؛ owner/manager management برقرار رہے۔
12. **Legacy upgrade:** isolated pre-029 copy میں ایک existing custom field اور answer رکھ کر Aurora کھولیں۔ Migration صرف ایک بار لگے، key `entity.custom.id` بنے، answer unchanged رہے۔ دوبارہ startup پر کوئی duplicate migration/error نہ آئے۔
13. **No business side effect:** اس test کے دوران کوئی PINV/INV/payment یا stock quantity نہ بدلے۔ اگر کوئی financial یا stock فرق نظر آئے تو test روک کر report بھیجیں۔

## نتیجہ

```text
Build: v0.37.0 Aurora
PT-09: PASS / PARTIAL / FAIL
Windows xUnit: total / failed / skipped / duration:
Migration 029 first start / restart:
Entity + field id + stable key:
Help text before → after:
Rename before → after; key unchanged? :
Answer before → after:
Required/invalid refusal and atomicity:
Seven types checked:
Put away / restore:
Permissions:
Black box old → new:
Stock/payment/document side effect (must be none):
Screenshots / error / test.log:
```

**Owner result:** Pending — کوئی Aurora physical PASS ابھی record نہیں۔

---

# PT-10 — Redesign Phase 3: Packaging, Variants & Search

**حالت:** Code changed — Cobalt کی Windows/xUnit اور owner physical verification pending

**مقصد:** پہلے سے موجود pack/variant label کو edit اور search کے قابل بنانا، اور ایک جیسے نام والی separate products کو Counter/Purchase میں واضح منتخب کرنا۔

**حدود:** کوئی نیا variant/stock model یا migration نہیں۔ Base-unit stock، box/strip/base conversion، prices، payments، documents، historical rows، extra pack barcodes اور Phase-2 governance نہیں بدلنے چاہئیں۔ Phase 4 شامل نہیں۔

## Automated اور development ثبوت

- **55/55 static checks PASS**، real C# grammar سمیت۔
- **900 written cases in 70 files**، جن میں Phase-3 کے 8 regressions شامل ہیں؛ مطلوبہ Windows نتیجہ **`ALL GREEN - 900 of 900`** ہے۔
- Actual ProductService search SQL کو تمام 29 migrations والے SQLite database پر چلا کر **7/7 development search groups** کامیاب ہوئے: `50ml`، `100 ML`، brand، room، category، company اور base unit۔ یہ Windows xUnit یا WPF proof نہیں۔

## مالک کی physical checklist

1. **Build اور suite:** `START-AREA11.bat` سے **v0.38.0 · Cobalt** کھولیں اور `tools\\RUN-TESTS.cmd` چلائیں۔ Total **900**، failed **0** ہونا چاہیے۔ Zircon 883 یا Aurora 892 والا پرانا نتیجہ اس release کے لیے کافی نہیں۔
2. **محفوظ data boundary:** اصل books میں صرف genuine medicines/delivery استعمال کریں۔ Invalid-input، controlled stock conversion یا کوئی risky financial side-effect check الگ test copy میں کریں؛ پہلے backup لیں۔
3. **دو الگ variants بنائیں:** isolated copy میں ایک ہی نام کی دو medicines بنائیں، مثلاً strength `5 ml` + pack `50 mL bottle` اور strength `10 ml` + pack `100 mL bottle`۔ Base unit `injection` رکھیں۔ Save کے بعد دونوں کی الگ product rows/IDs رہیں؛ کوئی merge یا نیا stock unit نہ بنے۔
4. **Editor persistence:** پہلی product بند کر کے دوبارہ کھولیں۔ Pack size / variant label اور Products grid کا **Pack / variant** column درست دکھے۔ New product کھولنے پر پرانا label خود copy نہ ہو۔
5. **Normalization اور refusal:** isolated copy میں `  50   mL bottle  ` save کریں؛ reopen پر `50 mL bottle` ہو۔ Multiline/control label اور 100 characters سے لمبا label مسترد ہو، پیغام واضح ہو، اور name/rate/stock/formula میں کوئی partial change نہ ہو۔
6. **Products search:** `50ml` سے صرف 50 mL variant اور `100 ML` سے صرف 100 mL variant آئے۔ Brand، room، primary category اور company سے بھی intended product ملے۔ Exact pack hit پر fuzzy suggestion نہ آئے؛ unknown نام پر existing fuzzy note الگ واضح رہے۔
7. **Counter search:** وہی `50ml` / `100 ML` searches Counter میں کریں۔ Pack / variant column اور selected row intended variant کی ہو۔ Search صرف list filter کرے؛ کوئی medicine خود cart میں add نہ ہو۔
8. **Existing pack barcode:** کسی محفوظ existing/test product کے active strip اور box codes دوبارہ scan کریں۔ Enter پر پہلے کی طرح درست product اور درست pack unit add ہو؛ put-away code کام نہ کرے۔ نئے shared search نے اس behavior کو نہیں بدلنا چاہیے۔
9. **Purchase discovery اور picker:** Purchase میں pack label، strength، brand یا barcode سے product تلاش کریں۔ Dropdown میں **name — strength · pack** نظر آئے، ایک جیسے نام والی دونوں variants الگ ہوں، اور منتخب row وہی رہے جس کو search کیا گیا ہو۔
10. **Conversion safeguard:** isolated copy میں variant کا formula `1 box = 2 inner packs` اور `1 inner pack = 5 injections` رکھیں۔ ایک box receive/sale quote **10 base injections** رہے۔ صرف pack label بدلنے سے quantity، rate، batch cost/retail یا stock نہ بدلے۔
11. **No financial/history side effect:** pack label edit سے پہلے اور بعد میں existing PINV/INV، batch quantities، supplier/customer balances اور payments compare کریں۔ کوئی نیا payment، drawer inflow، invoice، reversal یا stock adjustment نہ بنے۔ صرف product edit کا expected Black-box entry ہو۔
12. **Keyboard اور screen fit:** Products editor، Counter grid اور Purchase dropdown کو Tab/Enter اور اپنی normal screen resolution پر دیکھیں؛ pack text کی وجہ سے Save، Add line یا کوئی control clip/overlap نہ ہو۔

## نتیجہ

```text
Build: v0.38.0 Cobalt
PT-10: PASS / PARTIAL / FAIL
Windows xUnit: total / failed / skipped / duration:
Two variant product IDs and stock units:
Pack label before → after reopen:
Normalization / multiline / over-100 refusal:
Products 50ml / 100 ML results:
Counter 50ml / 100 ML and pack-column results:
Strip / box extra-barcode scans:
Purchase search and displayed variant choice:
Box/strip/base conversion before → after label edit:
Rates / batches / payments / documents side effect (must be none):
Black-box product edit:
Keyboard / screen fit:
Screenshots / error / test.log:
```

**Owner result:** Pending — کوئی Cobalt physical PASS ابھی record نہیں۔

---

# PT-01 — Startup, backup اور UI smoke

## مالک کا نتیجہ — PASS

1. **Startup / sign-in:** ٹھیک رہا۔
2. **Backup with photos:** کامیاب؛ zip بن اور save ہوا۔
3. **Pages:** مقررہ تمام pages کامیابی سے کھلے۔
4. اس نتیجے کو PASS لکھا گیا ہے، مگر backup کے استعمال اور data-safety کے لیے درج ذیل بہترییں درکار ہیں۔

## PT01-BK-01 — Backup folder منتخب کرنا اور actual folder کھولنا

**حالت:** Code changed — Windows/Explorer اور USB verification pending
**اہمیت:** اہم؛ backup میسر ہے مگر اس تک رسائی اور جگہ کا انتخاب واضح ہونا چاہیے۔

### مالک کی بات

- مالک کو چاہیے کہ وہ خود منتخب کرے کہ backup کہاں بنے، مثلاً USB یا دوسرا folder۔
- اگر custom location کا option نہ ہو تو کم از کم **Open the folder** دبانے پر صرف folder کا نام نہیں بلکہ حقیقی Windows folder کھلنا چاہیے۔
- اگر custom location میں کوئی security خطرہ ہو تو موجودہ default قابل قبول ہے، مگر backup کی جگہ تک آسان رسائی ضروری ہے۔

### موجودہ code میں کیا ہے

- Backup default folder میں بنتا ہے؛ UI میں destination منتخب کرنے کا option نہیں۔
- صفحے پر پورا folder path لکھا ہوتا ہے۔
- **Open the folder** موجود ہے اور Windows shell کو folder کھولنے کو کہتا ہے؛ مالک کے تجربے میں مطلوبہ folder view واضح نہیں کھلا، اس لیے اسے reproduce کرنا ہے۔
- یہ owner-reported usability مسئلہ ہے؛ backup بننا ناکام نہیں ہوا۔

### قبولیت کا معیار

1. Owner ایک محفوظ folder picker سے backup destination منتخب کر سکے؛ default پہلے جیسا رہے۔
2. منتخب path مکمل اور پڑھنے کے قابل ہو؛ USB/removable drive ہو تو وہ واضح ہو۔
3. **Open the folder** حقیقی Windows Explorer میں folder کھولے۔ نیا backup بننے کے بعد **Show this backup** جیسا راستہ ہو تو نئی zip منتخب یا واضح ہو۔
4. Folder نہ ملنے، permission نہ ہونے، USB نکلنے یا disk full ہونے پر واضح error آئے؛ پرانا backup delete یا overwrite نہ ہو۔
5. Custom destination صرف backup zip کے لیے ہو؛ live `area11.db` کی جگہ نہیں بدلنی چاہیے۔
6. Physical test: default folder، دوسرا local folder اور USB پر backup بن کر کھولا جائے۔

### 27 اکتوبر code update

- **Back up as…** شامل کیا گیا: owner منتخب `.zip` path پر database + منتخب photos کا نیا backup بنا سکتا ہے؛ live database اپنی جگہ رہتا ہے۔
- ایک ہی destination دوبارہ منتخب ہو تو پرانی zip overwrite نہیں ہوتی، نیا suffix بنتا ہے۔ Backup کے بعد Explorer میں نئی zip منتخب کر کے کھولنے اور مکمل path دکھانے کا راستہ شامل ہے۔
- نیا regression `Create_BackUpAs_UsesTheChosenFolderWithoutMovingOrOverwritingAnything` لکھا گیا ہے۔ اصل Windows Explorer/USB test ابھی باقی ہے۔

## PT01-BK-02 — پرانا backup restore کرنے کے بعد نئے کام اور backups کی حفاظت

**حالت:** Code changed — A/B/C recovery physical proof pending
**اہمیت:** **انتہائی اہم / data safety**
**نوٹ:** مالک نے خدشہ اور required safeguard بیان کیا ہے؛ ایسا کوئی actual data-loss incident report نہیں ہوا۔

### مالک کا خدشہ

اگر 26 اکتوبر کا پرانا backup restore کر کے کام جاری رکھا جائے اور بعد میں 27 اکتوبر یا اس کے بعد نیا کام ہو تو:

- پرانا backup restore کرتے وقت اس کے بعد کا نیا کام ضائع یا الجھا ہوا محسوس نہ ہو؛
- restore کے بعد کیا گیا نیا کام آگے بننے والے backups میں شامل ہو؛
- غلطی سے پرانی حالت پر آنے کے بعد بھی تازہ ترین کام واپس پانے کا واضح راستہ رہے؛
- کوئی پرانا یا نیا backup خاموشی سے overwrite نہ ہو۔

### موجودہ code میں پہلے سے موجود safeguards

- Backup نام میں milliseconds کا وقت ہے؛ collision پر `UniquePath` نیا نام بناتا ہے، اس لیے مقصد overwrite سے بچنا ہے۔
- Manual backups کو automatic trimming میں نہیں ہٹایا جاتا۔
- Database کی consistent copy SQLite کے `VACUUM INTO` سے بنتی ہے۔
- Restore سے پہلے current database کی **safety copy** بنتی ہے۔
- Restore سے پہلے database کا `quick_check` ہوتا ہے۔
- Restore کے بعد app restart ہوتی ہے تاکہ pages پرانی memory values نہ رکھیں۔
- اس کے بعد بنا کوئی نیا backup اس وقت کے live database سے بنتا ہے؛ پرانی zip میں نیا کام لکھا نہیں جاتا۔

یہ code-review facts ہیں؛ انھیں physical test کے بغیر مکمل حل نہیں لکھا جائے گا۔

### قبولیت کا معیار

1. Restore confirmation پر backup کی **بننے کی تاریخ، version اور عمر** نمایاں ہو۔
2. اگر backup current data سے پرانا ہو تو واضح warning ہو: **“اس کے بعد کا نیا کام موجود ہو سکتا ہے؛ پہلے تازہ safety backup بنے گا.”**
3. Safety copy بننا ناکام ہو تو restore رک جائے؛ کوئی exception ہو تو بھی current data کے نقصان کا خطرہ نہ ہو۔
4. Restore کے بعد recovery information restart کے باوجود نظر آئے: کونسا backup restore ہوا، کب، اور تازہ safety copy کہاں ہے۔
5. Restore کے فوراً بعد نیا baseline backup بنانے کا واضح option ہو؛ اس کے بعد کے automatic/manual backups اسی current live database اور نئے کام پر مشتمل ہوں۔
6. کسی بھی backup کو overwrite نہ کیا جائے؛ safety copies بھی الگ اور قابلِ شناخت رہیں۔
7. Physical scenario:
   - **A:** 26 اکتوبر کا backup بنائیں۔
   - **B:** اس کے بعد ایک نیا recognizable record بنائیں۔
   - **A restore:** پرانا backup restore کریں۔
   - **C:** restore کے بعد ایک مختلف recognizable record بنائیں۔
   - نیا backup بنائیں، اسے restore کر کے **C** اور اس وقت کا پورا live data verify کریں۔
   - **B** والا data pre-restore safety copy سے recover ہو سکے۔
8. یہ scenario صرف الگ test copy پر ہوگا، اصل دکان کے books پر نہیں۔

### 27 اکتوبر code update

- Restore confirmation اب manifest کی date/version دکھاتا ہے اور audit میں backup کے بعد کا نیا کام ملنے پر واضح **Newer work** warning دیتا ہے۔
- Restore سے پہلے **پورا database + تمام موجود photos والا recovery zip** بننا لازمی ہے؛ اس کے ناکام ہونے پر restore رک جاتا ہے۔
- Recovery zip عام backup list میں **Before restore** کے نام سے دکھتا ہے، automatic trimming میں نہیں آتا، اور اسے دوبارہ restore کر کے pre-restore حالت پر واپس آنے کا automated round-trip لکھا گیا ہے۔
- Restore کے بعد recovery summary setting میں محفوظ ہوتی ہے، اس لیے restart کے بعد **Last restore and recovery** میں نظر آئے گی۔
- یہ بدلاو automated source/test coverage ہے؛ اصل A/B/C، damaged zip، locked file اور disk-full physical proof ابھی نہیں ملا۔

## PT01-BK-03 — متعلقہ code-review نکات: safety copy کی نمائش اور photos

**حالت:** Code changed — restore/fault verification pending
**ذریعہ:** Agent code review، owner-reported failure نہیں
**اہمیت:** BK-02 کے ساتھ حل کرنا ضروری ہے۔

### جو دیکھا گیا

- Restore سے پہلے بننے والی safety copy **صرف `.db`** ہے، جبکہ عام backup zip میں database اور photos دونوں ہوتے ہیں۔
- Safety copies عام backup list میں نہیں دکھتیں؛ recovery کے لیے owner کو file system پر انحصار کرنا پڑ سکتا ہے۔
- Photo restore existing same-name files کو overwrite کر سکتا ہے؛ اس سے پہلے photos کی safety zip نہیں بنتی۔
- Restore کے بعد status لکھا جاتا ہے مگر app restart ہوتی ہے، اس لیے recovery پیغام کو restart کے بعد بھی دکھانا بہتر ہوگا۔
- Live database کو staging کے بجائے براہِ راست overwrite کیا جاتا ہے؛ copy یا اس کے بعد کے photo step میں failure آئے تو partial restore اور غیر واضح حالت کا خطرہ ہے۔ Safety copy موجود ہے مگر recovery خودکار اور واضح ہونی چاہیے۔
- Hand-made zip کے photo paths کے لیے `..` اور sibling-prefix boundary cases کا explicit test درکار ہے تاکہ کوئی file photos folder سے باہر نہ لکھی جا سکے۔

### مطلوبہ حل کا دائرہ

1. Pre-restore safety کو پورے shop کے قابلِ بازیابی backup بنانا یا photos کے تحفظ کی حد واضح کرنا۔
2. Safety copies کو الگ **Recovery / safety copies** حصے میں دکھانا اور ان سے محفوظ restore ممکن بنانا۔
3. Current photos کو overwrite کرنے سے پہلے ان کا محفوظ راستہ فراہم کرنا۔
4. Recovery instructions کو restart کے بعد بھی دکھانا، صرف عارضی status پر اکتفا نہیں کرنا۔
5. Database کو staged و safe replacement کے ذریعے بدلنا؛ copy/photo failure پر واضح rollback یا recovery، اور partial success کو مکمل failure کہہ کر چھپانا نہیں۔
6. Locked file، disk full، damaged zip، `../` photo path اور restore کے دوران failure کے automated fault-injection tests۔

## Backup fixes کی تجویز کردہ ترتیب — testing کے بعد

1. **BK-02 + BK-03:** ڈیٹا، photos اور recovery کی حفاظت پہلے۔
2. **BK-01:** custom destination، Explorer اور backup discovery کا UI۔
3. ہر fix کے ساتھ automated regression، پھر اوپر والا physical A/B/C scenario۔

27 اکتوبر کو backup production code بدلا گیا ہے: database replacement same-volume staged file سے ہوتا ہے، photos پہلے staging میں validate ہوتی ہیں، اور بعد کی ناکامی پر safety zip سے database/photos rollback کی کوشش ہوتی ہے۔ Hand-made zip کا `../` photo path اب reject ہوتا ہے۔ تین نئے backup regressions سمیت written suite **863** ہے اور **52 static checks PASS** ہیں؛ locked-file/disk-full fault injection اور Windows A/B/C ابھی owner verification میں ہیں۔

---

# PT-02 — حقیقی medicine اور product details

**مقصد:** حقیقی product بنانا، اس کی details بچنا، zero-stock behaviour درست رہنا، اور audit record بننا۔
**اہم:** ایسی حقیقی دوا منتخب کریں جسے واقعی دکان میں رکھنا ہے۔ Fake medicine اصل books میں نہ بنائیں۔
**Batch / expiry / stock:** اس test میں نہیں؛ Products صفحے پر stock batches سے آتا ہے، اس لیے وہ **PT-03 purchase** میں آئے گا۔

## مراحل

1. **Products → New** کھولیں۔
2. پہلے خالی name کے ساتھ **Save product** دبائیں: refusal آنی چاہیے اور کوئی product نہیں بننا چاہیے۔
3. دوبارہ **New** کر کے حقیقی medicine کی تفصیل بھریں:
   - Name اور salt / formula
   - Rack / shelf، ضرورت ہو تو Room / almari
   - Category اور Company / brand owner
   - Pack formula: strips / box اور tablets / strip
   - درست base unit
   - Cost، Retail اور Reorder level
   - حقیقی barcode موجود ہو تو Barcode؛ VIP/Doctor rates صرف اگر واقعی استعمال ہوں
4. **Save product** دبائیں۔
5. Product کو name، barcode یا rack سے search کریں اور دوبارہ کھولیں۔
6. تمام values دوبارہ درست نظر آنی چاہئیں؛ Cost/Retail کے حساب سے Margin معقول ہو۔
7. Purchase سے پہلے **Stock = 0** ہونا چاہیے۔ Products اور Reorder pad دونوں کھلنے چاہئیں؛ کوئی `Double -> Int64` error نہیں آنا چاہیے۔
8. **Black box** میں اس product کی create/save entry دیکھیں: درست medicine، وقت، user اور نئی values واضح ہوں۔
9. UI کا معائنہ کریں: کوئی field یا button کٹا ہوا نہیں، pack formula سمجھ آتا ہے، اور save/search آسان ہیں۔

## PT-02 PASS کا معیار

- Blank name refused۔
- حقیقی product صرف ایک بار بنا۔
- Search اور reopen پر تمام details درست۔
- Purchase سے پہلے stock صفر؛ Products/Reorder بغیر error کھلے۔
- Margin درست اور Black box entry موجود۔
- کوئی UI یا usability مسئلہ ہو تو یہیں record ہوگا۔

## مالک کی تصدیق — 27 اکتوبر 2026

- **Save product:** حقیقی تبدیلیاں save ہوئیں۔
- **Black box:** product کی changes ریکارڈ ہوئیں — **PASS**۔
- PT-02 کو مکمل PASS نہیں لکھا گیا کیونکہ blank-name، search/reopen، zero-stock/Reorder اور Margin کی الگ تصدیق ابھی نہیں بھیجی گئی۔
- Categories، units، rates، photos، navigation، shift اور sale completion پر نئی درخواستیں ملی ہیں۔
- Product selection/cart کے ساتھ blank/dashes والا مسئلہ بھی report ہوا، مگر اس کا exact reproduction ابھی درکار ہے۔
- **PT-02: PARTIAL — تفصیلی items نیچے درج ہیں؛ production fixes کا انتظار مالک کے OK اور واضح choices پر ہے۔**
- **29 اکتوبر 2026 کی نئی owner confirmation:** مالک نے “2 is ok” لکھا ہے۔ اس تصدیق کی بنیاد پر PT-02 کا مجموعی physical نتیجہ **PASS / OWNER CONFIRMED** ہے۔ نیچے کے الگ logged items کو خودکار طور پر fixed یا rejected نہیں سمجھا جائے؛ جہاں الگ verification یا reproduction لکھا ہے وہ اپنی جگہ برقرار ہے۔

---

# PT-02 کے نئے مسائل اور تبدیلیوں کی درخواستیں

## PT02-CAT-01 — ایک medicine کے لیے ایک سے زیادہ categories یا subcategories

**حالت:** Code changed — Windows/Core verification pending
**اہمیت:** اہم catalog/data-model تبدیلی

- موجودہ product form میں ایک وقت میں ایک category منتخب ہوتی ہے۔
- مالک چاہتا ہے کہ ایک medicine ضرورت کے مطابق ایک سے زیادہ groupings میں آ سکے۔

### مالک کا فیصلہ — 27 اکتوبر 2026

**ایک primary category + اضافی tags۔** Financial totals primary category سے صرف ایک بار بنیں گے؛ tags search/filter کے لیے ہوں گے۔ کسی tag/filter سے sale، stock یا profit double-count نہیں ہوگا۔

**قبولیت کا معیار:** category/subcategory choices واضح ہوں، product پر تمام selected groups محفوظ ہوں، filters کام کریں، اور category reports کا میزان کبھی double-count نہ ہو۔

### 27 اکتوبر code update

- Migration `027_catalog_tags_units.sql` میں `product_category_tags` اور products پر descriptive `strength_value`/`strength_unit` شامل ہیں۔ Primary `products.category_id` financial reports کا واحد category رہتا ہے؛ tags صرف search/filter اور catalog counts کے لیے ہیں۔
- Product editor میں additional active-category checkboxes، tag-aware category/company filter، اور primary/tag conflict guard شامل ہیں۔ Category put-away primary links کے ساتھ tag links بھی صاف کرتا ہے؛ products/history delete نہیں ہوتے۔
- Regression source میں tagged product کو category filter میں ایک بار اور category financial total میں primary کے تحت ایک بار رکھنے کا case ہے۔ یہ ابھی Windows پر run نہیں ہوا۔

## PT02-NAV-01 — Company، Category اور Dashboard سے clickable filtered views

**حالت:** Code changed — Windows navigation verification pending
**اہمیت:** UI/navigation بہتری

- Company یا company-related view پر click کریں تو اسی company کی تمام medicines کا filtered product list کھلے۔
- Category پر click یا right-click → **Show all** سے اس category کی medicines کھلیں۔
- Dashboard کے cards/numbers متعلقہ detail page سے linked ہوں، صرف display نہ ہوں۔
- Product/catalog selection کے ہر ایسے مقام پر ایک مستقل اور predictable navigation راستہ ہو۔

**قبولیت کا معیار:** click کے بعد درست filter، records اور count نظر آئیں؛ واپسی پر قائمہ حالت محفوظ رہے؛ empty result کی واضح وجہ ہو۔

### 27 اکتوبر code update

- Products list میں category اور company clickable ہیں؛ وہی filtered product list کھلتا ہے، filter صاف لکھا آتا ہے اور Clear filter دستیاب ہے۔ Empty filtered result کو search/filter empty-state ملتا ہے۔
- Dashboard کے bills/sales/profit cards today کی تفصیل کھولتے ہیں؛ customer credit اور supplier payable متعلقہ owing tabs کھولتے ہیں؛ products، low-stock، stock-minus اور expiry cards متعلقہ detail pages سے linked ہیں۔
- Mouse/keyboard navigation، واپسی پر menu state اور ہر card کے actual records/counts کی Windows verification ابھی باقی ہے۔

## PT02-UNIT-01 — Editable units، pack contents اور strength/volume

**حالت:** Code changed — Windows/unit and genuine-stock verification pending
**اہمیت:** **بنیادی inventory model؛ احتیاط سے design کرنا ہے**

### مالک کی ضرورت

- Base unit کا fixed/محدود انتخاب سمجھ نہیں آ رہا؛ box / strip / tablets کی موجودہ زبان ہر medicine کے لیے موزوں نہیں۔
- Units کی ایک محفوظ managed list ہو جیسے categories/companies ہیں؛ owner نیا unit بنا اور edit کر سکے، مثلاً `mL`۔
- Unit کا نام ہر جگہ ایک جیسا رہے تاکہ `ml`، `ML` یا املا کی غلطی سے الگ الگ units نہ بنیں۔
- Product پر pack کی تفصیل ہو: بڑے ڈبے میں کتنے injections/عدد ہیں، اور ایک injection کتنے `mL` کا ہے۔
- `50 mL`، `100 mL`، `250 mL` جیسی strength/volume سے search یا filter ممکن ہو۔

### مالک کی مثال

**ایک box میں 10 injections، اور ہر injection 50 mL۔** اس سے physical pack ہے: `1 box = 10 injections`؛ strength ہے: `1 injection = 50 mL`؛ total content ہے: `500 mL / box`۔

### مالک کا فیصلہ — 27 اکتوبر 2026

- Injection کو **mL میں تقسیم کر کے نہیں بیچنا**؛ stock اور sale whole injections / whole sale units میں ہوں گے۔
- Box میں صرف یہ معلوم ہو کہ اس میں **کتنے injections / base units** ہیں۔
- `50 mL` ہر injection کی strength/quantity ہے، stock-count unit نہیں۔
- `10 mL`، `50 mL` اور `100 mL` والی variants الگ products ہوں گی؛ ایک variant ختم ہونے سے دوسری variant کا stock کم یا زیادہ نہیں ہوگا۔
- Managed editable units پھر بھی ضروری ہیں تاکہ `injection` جیسے base units ایک جیسے املے میں محفوظ ہوں، اور strength سے search/filter ہو سکے۔

**قبولیت کا معیار:** managed units، duplicate-name guard، pack conversion، strength filter، اور existing REAL/`double` quantities محفوظ ہوں۔ Used unit delete کرنے سے تاریخ یا stock نہ بگڑے؛ edit/inactivate کے اصول واضح ہوں۔

### 27 اکتوبر code update

- Products → **Manage units…** میں owner/manager کے لیے add، case-insensitive duplicate guard، rename، order، whole-only quantity rule، put-away/restore شامل ہیں۔ Used unit put-away refuse ہوتا ہے؛ rename current base/strength labels کو ایک transaction میں بدلتا ہے، quantities/history کو نہیں۔
- Product form میں managed base unit، optional strength value/unit اور pack formula ہے۔ `1 box = 1 inner pack × 10 injections` کی مثال میں stock 10 injection units ہے؛ `50 ml` صرف variant کی strength ہے۔ Search میں strength text اور Products میں exact `50 ml`/`50ml`/numeric/unit filter ہے۔
- Migration `028_unit_quantity_rules.sql` کے بعد whole-only rule managed unit row کے `whole_only` flag سے آتا ہے، editable نام `"injection"` سے نہیں؛ rename کے بعد بھی fractional sale/stock entry رکتی ہے۔ Sale، purchase، stock correction، physical count/apply، central stock decrement اور opening import پر guards ہیں۔ `qty_base` REAL اور `ItemRow.Qty` double پر کوئی INTEGER cast نہیں آیا۔
- CSV import اور native workbook دونوں active managed unit چیک کرتے ہیں، canonical spelling لکھتے ہیں اور apply کے وقت unit دوبارہ چیک کرتے ہیں۔ Regression source میں injection sale/fraction refusal، strength variants، rename/put-away، import اور workbook stale-unit cases ہیں؛ Windows/Core execution ابھی باقی ہے۔

## PT02-PRICE-01 — VIP/Doctor rate اور bill discount % کی ترتیب

**حالت:** Code changed — Windows pricing/WPF verification pending
**اہمیت:** **مالیاتی rule؛ owner decision کے مطابق code اور regression cases لکھے گئے ہیں**

### مالک کا فیصلہ — 27 اکتوبر 2026

1. اگر bill پر explicit **discount %** دیا جائے تو وہ **total retail price** پر لگے گا؛ VIP/Doctor rate اس percentage calculation میں شامل نہیں ہوگا، چاہے final rate special rate سے کم یا زیادہ ہو۔
2. اگر discount % خالی/صفر ہو اور متعلقہ special rate موجود ہو تو sale complete کرنے سے پہلے popup پوچھے گا: **“کیا VIP/Doctor rate لگانا ہے؟”**
3. **Yes / OK:** متعلقہ VIP/Doctor special rate لگے گا۔
4. **No:** special rate نہیں لگے گا؛ normal retail rate رہے گا۔
5. Special rate ایک default/رہنمائی قیمت ہے؛ مختلف doctors یا customers کو مقررہ rate سے مختلف حقیقی قیمت دینا ممکن رہے گا۔

**محفوظ شرط:** Cost/below-cost guard اور integer-paisa rounding برقرار رہیں؛ discount سے منفی total یا چھپا ہوا loss accept نہ ہو۔ Final selling rate اور margin واضح نظر آئیں۔

### 27 اکتوبر code update

- Explicit discount اب line کے **full retail/selling-price total** پر ہے؛ پرانا `discount.mode = margin` بھی نئے sale کا حساب نہیں بدل سکتا۔ Discount موجود ہو تو special rate کے ساتھ stacking نہیں ہوتی۔
- Discount صفر ہو اور VIP/Doctor rate واقعی مختلف ہو تو **Yes/No** سوال آتا ہے؛ default **No = normal retail** ہے۔ Yes پر وہی `SaleService` quote/commit کرتا ہے۔
- Cart میں selected unit کا actual rate نظر آتا ہے۔ Owner/Manager agreed custom rate لکھ سکتے ہیں؛ cashier custom rate سے discount limit bypass نہیں کر سکتا۔ Park/resume پر custom paisa rate محفوظ رہتا ہے۔
- Below-cost lock ON ہو تو pehle سے batch cost سے کم custom/special/retail line refuse ہوتی ہے؛ explicit discount کا موجودہ cost پر clamp اور integer-paisa line-total reconciliation برقرار ہے۔
- VIP، Doctor، legacy margin setting، below-cost، custom-rate permission اور normal-retail cases لکھے گئے ہیں۔ Windows پر popup کے Yes/No، held rate، actual receipt اور role checks کی physical verification باقی ہے۔

## PT02-PHOTO-01 — Products اور Counter میں medicine thumbnails

**حالت:** Code changed — Windows thumbnail/gallery verification pending
**اہمیت:** UI/recognition بہتری

- Product list اور Counter میں چھوٹی medicine photo نظر آئے تاکہ درست چیز پہچاننا آسان ہو۔
- Photo نہ ہو تو صاف placeholder ہو؛ موجودہ Photos editor اور put-away history قائم رہے۔

**قبولیت کا معیار:** thumbnail جلدی load ہو، row text پڑھنے میں رکاوٹ نہ بنے، click پر بڑی photo ملے، اور photo نہ ہونے سے sale/search متاثر نہ ہو۔

### 27 اکتوبر code update

- Products اور Counter دونوں کی medicine rows میں 44px thumbnail یا واضح **No photo** placeholder ہے۔ Thumbnail click موجودہ Photos gallery کھولتا ہے؛ put-away/active predicate وہی رہتا ہے جو migration 015 کے photos schema اور PhotoService استعمال کرتے ہیں۔
- Database میں صرف پہلے active gallery `file_name` کا selection پڑھا جاتا ہے؛ machine-specific path بنانا desktop کے `AppPaths.PhotosFolder` پر ہے اور missing file کو placeholder ملتا ہے۔ Search/sale میں کوئی photo-required شرط نہیں آئی۔
- Actual image loading، بند/missing file، gallery click اور sale/search unaffected ہونے کی Windows verification ابھی باقی ہے۔

## PT02-SHIFT-01 — Previous closing cash، اگلا opening float اور difference alerts

**حالت:** Code changed — Windows financial/alert verification pending
**اہمیت:** **انتہائی اہم مالیاتی/UI تصدیق**

### مالک کا فیصلہ — 27 اکتوبر 2026

- نیا galla/shift کھلنے پر پچھلی shift کا **counted closing cash خود opening cash** بنے؛ مثلاً `Rs 6,000`۔
- اگر opening رقم پچھلی رقم سے **ایک روپے بھی کم یا زیادہ** ہو تو اس کا **reason لکھنا لازم** ہوگا۔
- یہ فرق shift change کے کسی بھی وقت ہو، reason کے ساتھ record اور Alerts میں دکھایا جائے۔
- Closing کا short یا over، opening میں تبدیلی، یا اسی قسم کا مالی فرق **red alert** بنائے گا۔
- Alert محض اطلاع اور correction guidance دے؛ اس سے خودکار payment، drawer inflow یا financial adjustment نہیں بننا چاہیے۔

**قبولیت کا معیار:** opening cash واضح، short **اور** over دونوں کے alerts، ایک روپے کا فرق بھی capture، لازم reason، درست shift/person/amount، alert سے shift detail کا link اور مکمل Black-box record۔

### 27 اکتوبر code update

- Drawer بند ہو تو نیا opening field آخری **counted closing** سے خود بھر جاتا ہے؛ پہلی shift میں کوئی previous رقم نہ ہو تو واضح پیغام آتا ہے۔
- Opening previous counted سے ایک paisa بھی مختلف ہو، یا closing/handover short یا over ہو تو service میں reason لازم ہے۔ Reason کے بغیر کوئی shift close/open/handover mutation نہیں ہوتی۔
- دونوں الگ red alerts ہیں: **Opening cash difference** اور **Closing cash difference**۔ Detail میں shift/person، actual، baseline، فرق اور reason آتا ہے؛ Black box میں بھی opening baseline اور reason محفوظ ہوتا ہے۔
- نئے tests Rs 1 کم اور Rs 1 زیادہ دونوں جانچتے ہیں، no-reason refusal کے بعد unchanged shift بھی۔ کوئی automatic payment، drawer inflow یا adjustment نہیں بنتا۔
- Windows پر genuine shift کے ساتھ exact، Rs 1 short، Rs 1 over، handover اور اگلے opening کی physical verification ابھی باقی ہے۔

## PT02-ALERT-01 — Bell کے تین severity colors

**حالت:** Code changed — bell colour/severity physical verification pending

مالک bell icon پر تین رنگ چاہتا ہے تاکہ مسئلے کی نوعیت دور سے صاف ہو:

- **Red:** پیسوں کا فرق، مالی مسئلہ یا urgent financial discrepancy۔
- **Yellow:** کم شدت والی warning جس پر توجہ درکار ہے۔
- **Green:** عام معلومات/reminders، مثلاً expiry قریب آنا۔

Bell کی رنگت موجودہ سب سے سنگین active alert کے مطابق ہو؛ تفصیلی mapping کو implementation plan میں موجودہ alert اقسام کے ساتھ جوڑا جائے گا۔ کسی alert کی صرف رنگ بدلنے سے اس کا financial اثر نہیں بدلے گا۔

### 27 اکتوبر code update

- Bell icon اور badge ایک مرکزی severity mapping استعمال کرتے ہیں: **red = financial/urgent، yellow = medium warning، green = ordinary reminder**؛ سب سے سنگین active رنگ جیتتا ہے۔
- Shift differences red، low-stock/supplier-cutoff/quarantine yellow، drawer-open/bill-confirmation اور نیا **Nearing expiry** reminder green ہیں۔ Expired stock کو ordinary green نہیں کیا گیا۔
- Nearing expiry صرف Settings کے **nearest expiry band** میں آتا ہے؛ یہ light reminder ہے، کوئی stock یا financial transaction نہیں بناتا۔
- Alert board rows بھی وہی تین رنگ دکھاتے ہیں۔ Red discrepancy کو light dismiss سے ہٹایا نہیں جا سکتا؛ owner review/reason لازم رہتا ہے۔
- Written suite اب **872** اور static gate **52/52 PASS** ہے؛ WPF پر تینوں رنگوں کی visual اور dismissal verification باقی ہے۔

## PT02-POS-01 — Product selection/cart price اور blank/dashes کی رپورٹ

**حالت:** Needs reproduction — do not guess a fix
**اہمیت:** ممکنہ core POS مسئلہ؛ پہلے evidence

مالک نے product selection کے دوران price/cart behaviour میں تبدیلی اور کچھ حالات میں fields کے blank یا dashes ظاہر ہونے کی بات کی ہے۔ اصل جملے سے exact page، product state اور sequence واضح نہیں ہے، اس لیے کوئی diagnosis فرض نہیں کیا گیا۔

**درکار evidence:**

1. کونسے page سے شروع کیا: Products یا Counter؟
2. کونسی medicine select کی، اس کا stock اور batch کیا تھا؟
3. Select/Add to cart کے بعد کونسا field خالی یا `—` ہوا؟
4. پہلے کیا ہوتا تھا اور اب کیا ہونا چاہیے؟
5. اسکرین شاٹ یا مختصر video، اور error ہو تو `SEND-REPORT.cmd` کا report۔

## PT02-SALE-01 — Sale completion کا واضح، optional confirmation

**حالت:** Code changed — Windows confirmation/Settings verification pending
**اہمیت:** اہم daily workflow/UI

- کامیاب sale کے بعد چھوٹا واضح confirmation ہو: checkmark، **Done**، invoice number اور ضرورت ہو تو **اتنا cash واپس کریں**۔
- Owner Settings سے یہ popup بند کر سکے۔
- اگلا کام جلدی شروع کرنے کے لیے واضح **Continue / New sale / Back** جیسا راستہ ہو؛ keyboard سے بھی جلدی آگے بڑھا جا سکے۔
- Confirmation صرف successful committed sale کے بعد آئے؛ refused sale پر نہیں۔

**قبولیت کا معیار:** ایک sale = ایک invoice، صحیح change، ہلکا non-blocking indicator، Settings persistence، اور اگلی sale فوراً شروع کرنے کی صلاحیت۔

### 27 اکتوبر code update

- Successful commit کے بعد Counter پر checkmark والا non-blocking card آتا ہے: **Done — sale saved**، invoice/total، change یا due، اور **Done — ready for next sale**۔
- Card صرف موجودہ confirmation کو ہٹاتا ہے؛ sale دوبارہ save، print، undo یا payment نہیں کرتا۔ اگلا item scan/add کرنا بھی card ہٹا کر نئی sale جاری رکھتا ہے۔
- Settings → Sales rules میں **sale.successPopup** کا owner-controlled option ہے؛ OFF ہو تو موجودہ status/LastBill رہتا ہے۔ Auto-print الگ setting رہی۔
- Windows پر settings persistence، keyboard/scan continuation، exact change، refused-sale پر card نہ آنا، اور ایک click پر ایک ہی invoice کی verification باقی ہے۔

---

# PT-03 — حقیقی Purchase، Batch، Expiry اور Supplier Payable

**مقصد:** حقیقی delivery سے stock اور supplier حساب درست بننا۔
**شرط:** اصل دکان میں صرف حقیقی delivery درج کریں۔ آزمائشی purchase کے لیے الگ test copy استعمال کریں۔

**حالت — 29 اکتوبر 2026:** **PARTIAL** — مالک نے PT-03 کو PARTIAL بتایا ہے اور تفصیلی report بھیجنے کا کہا ہے۔ Report آنے تک PINV، stock conversion، batch/expiry، paid/payable اور Black box کے نتائج خالی رکھیں؛ کسی کو PASS یا fixed نہ لکھیں۔ PT-02 کی نئی owner confirmation الگ سے اوپر محفوظ ہے۔

## شروع کرنے سے پہلے لکھ لیں

- PT-02 medicine کا موجودہ stock: `______`
- Supplier کا موجودہ payable: `______`
- اصل delivery کی quantity، pack، batch اور expiry: `______`

## مراحل

1. **Purchases** کھولیں اور حقیقی Supplier منتخب کریں؛ نیا حقیقی supplier ہو تو **Add supplier** سے بنائیں۔
2. **Search product** میں PT-02 والی medicine منتخب کریں۔
3. درست **Unit** منتخب کریں: box / strip / base unit۔
4. حقیقی **Quantity** لکھیں اور اپنے pack formula سے expected base quantity حساب کر لیں۔
5. حقیقی **Batch number** اور **Expiry** `YYYY-MM` لکھیں۔
6. Cost/Retail درست ہیں تو آگے بڑھیں۔ **Sample / bonus line** صرف اس وقت tick کریں جب واقعی zero-cost sample ہو۔
7. **Add line** دبائیں؛ line کی quantity، batch، expiry اور cost دوبارہ پڑھیں۔
8. **Paid now** میں اصل paid amount لکھیں: full، partial یا `0` اگر پوری delivery udhaar ہے۔
9. **Save purchase (PINV)** ایک بار دبائیں۔

## PT-03 PASS کا معیار

- ایک حقیقی **PINV** number بنا؛ duplicate document نہیں۔
- Stock میں بالکل اتنا اضافہ ہوا جتنا `quantity × pack conversion` بنتا ہے۔
- Product/batch میں درست batch اور expiry نظر آئے۔
- Purchase total اور `supplier payable = total − paid now` درست ہیں۔
- Paid cash صرف اتنا ہی ledger/drawer میں آیا جتنا حقیقت میں ادا ہوا۔
- Products، Reorder، Purchases، Supplier ledger اور Black box میں درست تفصیل موجود ہے۔
- UI میں کوئی unclear conversion یا blank/dash ہو تو PT02-UNIT/POS items کے ساتھ record کریں۔

## نتیجہ

```text
PT-03: PASS / PARTIAL / FAIL
Supplier اور PINV:
Pehle stock → بعد کا stock:
Unit × quantity = expected base qty:
Batch / expiry:
Purchase total / paid / payable:
Black box:
کوئی مسئلہ یا UI تبدیلی:
```

---

# PT-04 — حقیقی Sale، Cart، Cash Change، Receipt اور Completion

**مقصد:** ایک حقیقی sale میں product، price، stock، cash اور receipt ایک جیسے رہیں۔
**شرط:** اصل books میں صرف حقیقی فروخت۔ Fake customer، fake payment یا صرف test کے لیے credit sale نہ بنائیں۔

## شروع کرنے سے پہلے

- PT-03 کے بعد product stock لکھیں: `______`
- Drawer/shift کھلا ہو؛ opening cash حقیقی counted رقم ہو اور وہ رقم PT02-SHIFT-01 کے ساتھ record کریں۔
- پہلے discount، VIP/Doctor اور loyalty کو غیر ضروری complication سے بچائیں؛ پہلی sale simple ہو۔

## مراحل

1. **Counter (POS)** کھولیں۔
2. Medicine کو name سے search کریں؛ حقیقی barcode ہو تو scan کر کے **Enter** بھی آزمائیں۔
3. درست unit اور quantity منتخب کر کے **Add to cart** دبائیں۔
4. Add کرنے سے پہلے اور بعد میں یہ خاص دیکھیں:
   - Retail / price واضح ہے یا blank/dash؟
   - Stock اور unit درست ہیں؟
   - Cart میں item، unit، qty، rate اور total درست ہیں؟
5. حقیقی customer ہو تو منتخب کریں؛ سادہ cash sale کے لیے customer لازم نہیں۔
6. Payment **Cash** رکھیں اور حقیقت میں received رقم **Cash received** میں لکھیں۔
7. **Change** صحیح حساب سے ظاہر ہو؛ received کم ہو تو sale کی رکاوٹ واضح ہو۔
8. حقیقی receipt چاہیے تو **Print the receipt automatically** tick رکھیں۔
9. **Complete sale (F4)** صرف ایک بار دبائیں۔
10. Sale کے بعد دیکھیں کہ confirmation کافی واضح ہے یا نہیں: Done/checkmark، invoice اور cash change۔ موجودہ کمی PT02-SALE-01 میں پہلے سے logged ہے۔

## PT-04 PASS کا معیار

- ایک **INV** بنا اور cart میں دکھائی گئی رقم ہی final bill بنی۔
- Stock صحیح unit/qty سے کم ہوا؛ batch FEFO قواعد کے مطابق رہا۔
- Cash received، change، total اور drawer کا فرق درست ہے۔
- پرنٹ شدہ receipt اور screen کے item/total ایک جیسے ہیں۔
- Sale completion کا انداز واضح ہے؛ اگر نہیں تو اسی item میں screenshot/note آئے۔
- Black box میں sale، payment اور print/reprint entries صحیح ہیں۔
- **Print last bill again** سے وہی bill دوبارہ print ہو مگر کوئی نئی sale/payment نہ بنے۔
- حقیقی khata sale ہو تو الگ سے customer balance قبل/بعد لکھ کر verify کریں؛ fake credit test نہ بنائیں۔

## نتیجہ

```text
PT-04: PASS / PARTIAL / FAIL
INV:
Cart price / stock / unit:
Total / received / change:
Pehle stock → بعد کا stock:
Receipt:
Sale completion ظاہر ہوا:
Black box:
کوئی blank/dash یا دوسرا مسئلہ:
```

---

# اگلے notes کے لیے سانچہ

```text
تاریخ:
Test ID:
Build:
نتیجہ: PASS / PARTIAL / FAIL
کیا کیا:
کیا توقع تھی:
حقیقت میں کیا ہوا:
مطلوبہ تبدیلی:
اسکرین شاٹ / error:
Agent investigation:
Fix status:
Owner verification:
```
