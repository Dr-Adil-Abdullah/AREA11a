# Area11 — مالک کی Physical Testing اور Manual Instructions Tracker

**Release:** v0.38.0 Cobalt (`#315C8C`)  
**یہ snapshot:** 29 اکتوبر 2026  
**ماخذ:** `PHYSICAL-TESTING.md`، `docs/PROGRESS.md`، `docs/OWNER-DECISIONS.md` اور محفوظ شدہ owner evidence  
**مقصد:** ایک پرنٹ ہونے والی فائل جس میں PT-01 تا PT-10، مکمل شدہ ثبوت، باقی checks اور manual instructions کا حساب ایک جگہ ہو۔

> یہ فائل نتائج لکھنے اور پرنٹ کرنے کے لیے ہے۔ مستند running feedback log `PHYSICAL-TESTING.md` ہی رہے گا۔ کسی phase کے مجموعی PASS سے اس کے نیچے الگ logged مسئلہ خودکار طور پر fixed، rejected یا closed نہیں ہوگا۔

## استعمال کا آسان طریقہ

1. ایک وقت میں ایک phase یا item آزمائیں؛ صرف وہ خانہ tick کریں جو واقعی آپ نے اپنے کمپیوٹر پر دیکھا ہو۔
2. ہر session میں تاریخ، build، PT/item ID اور نتیجہ لکھیں: **PASS / PARTIAL / FAIL**۔
3. ناکامی یا غیر واضح behaviour پر screenshot، error، اور ضرورت ہو تو `tools\SEND-REPORT.cmd` کا report منسلک کریں۔
4. اصل books میں صرف حقیقی medicine، delivery، sale، return، payment یا adjustment درج کریں۔ Fake financial transaction، restore، fault injection، controlled shortage یا risky scenario کے لیے پہلے backup اور **الگ test copy** استعمال کریں۔
5. Automated tests اور physical test الگ ثبوت ہیں۔ Automated PASS کو app کی ہر screen، printer، scanner، Excel یا genuine transaction کا PASS نہ لکھیں۔
6. `qty_base` **REAL** اور `ItemRow.Qty` **double** ہیں۔ Negative-stock retirement، D-93 اور PR cancellation کے financial rules اس testing میں نہیں بدلنے۔
7. Development فی الحال paused ہے؛ Phase 4 یا نیا feature اس tracker کی تکمیل سے خود authorize نہیں ہوتا۔

---

# 1 — ثبوت کی موجودہ حد: کیا ثابت ہے اور کیا نہیں

## Owner کے Windows پر حالیہ automated ثبوت

- [x] **29 اکتوبر 2026 — Cobalt:** `Area11.Core` / `Area11.Tests` کے **900 passed، 0 failed، 0 skipped**، مدت **1 منٹ 4 سیکنڈ**۔
- [x] Runner کا نتیجہ: **`ALL GREEN - 900 of 900`**۔
- [x] Development gate: **55/55 static checks PASS**؛ 900 written cases، 70 test files۔
- [x] Development SQLite search rehearsal: **7/7 groups PASS** — `50ml`، `100 ML`، brand، room، category، company اور base unit۔

## جو اس ثبوت سے ثابت نہیں ہوتا

- [ ] اوپر والا Cobalt log **Core/Tests** کا ہے؛ اس سے `Area11.Desktop` / WPF کا الگ build یا `START-AREA11.bat` سے current launch ثابت نہیں ہوتا۔
- [ ] PT-10 کے pack editor، Products/Counter/Purchase screens، barcode scanning، genuine stock conversion اور receipts کا physical PASS ابھی نہیں ملا۔
- [ ] PT-08 کی WPF/navigation verification اور PT-09 کی Aurora Windows/xUnit + physical verification الگ باقی ہیں۔
- [ ] Backup restore/fault injection، real Excel، USB scanner اور دیگر physical checks کا automated ہونا ان کا shop proof نہیں۔
- [ ] Cloud اصل PT-01 تا PT-10 کے موجودہ physical scope میں نہیں؛ اس کا real-cloud test الگ منظوری اور isolated test data چاہتا ہے۔

## پرانے نتائج

- **Zircon:** 883/883 Windows xUnit PASS، 29 اکتوبر 2026، 1 منٹ 10 سیکنڈ — یہ PT-08 کا automated ثبوت ہے، WPF navigation PASS نہیں۔
- **Aurora:** 892 written cases تھے؛ owner سے Aurora کا 892/892 Windows run یا physical PASS محفوظ نہیں۔
- Obsidian، Spruce اور دیگر پرانے totals historical evidence ہیں؛ انہیں Cobalt یا کسی نئے phase پر apply نہیں کرنا۔

---

# 2 — PT-01 تا PT-10 ایک نظر میں

| Phase | موجودہ نتیجہ | ثبوت اور باقی کام |
|---|---|---|
| **PT-01** Startup / Backup / UI | **PASS، improvements الگ open** | Startup/sign-in، photos سمیت backup zip اور pages owner نے دیکھے۔ Restore، recovery، Explorer/USB اور fault scenarios کا اپنا physical proof باقی۔ |
| **PT-02** Product / medicine | **PASS — owner confirmed 29 Oct** | Save اور Black box پہلے confirm ہوئے؛ 29 Oct کو overall “2 is ok” ملا۔ نیچے الگ CAT/NAV/UNIT/PRICE/PHOTO/SHIFT/ALERT/POS/SALE items اپنے status پر ہیں۔ |
| **PT-03** Purchase / Batch / Payable | **PARTIAL** | Owner نے PARTIAL بتایا؛ PINV، conversion، batch/expiry، paid/payable اور Black box کی detailed report ابھی درکار۔ |
| **PT-04** Sale / Cash / Receipt | **INSTRUCTIONS READY** | Genuine simple sale کا کوئی itemized owner result ابھی محفوظ نہیں۔ |
| **PT-05** Returns / PR / Q | **PENDING** | Genuine return اور isolated-copy PR/cancellation/negative-stock checks باقی۔ |
| **PT-06** Stock / Expiry / Alerts | **PENDING** | Stock-take، write-off، supplier return، expiry اور D-93 safeguards کے physical نتائج باقی۔ |
| **PT-07** Restore / Excel / Hardware | **PENDING / partial hardware evidence** | Printer-slip کا ایک حصہ 23 Oct کو confirm ہوا؛ USB scan/Black box، real Excel اور restore/fault proof باقی۔ |
| **PT-08** Zircon shell | **AUTOMATED PASS؛ WPF PENDING** | 883/883 محفوظ؛ modules، Settings، permissions، keyboard اور screen-fit owner checks باقی۔ |
| **PT-09** Aurora own fields | **CODE READY؛ VERIFICATION PENDING** | Stable key، guidance، migration، validations، permissions اور no-side-effect checks باقی۔ |
| **PT-10** Cobalt pack / variants / search | **AUTOMATED PASS؛ PHYSICAL PENDING** | 900/900 محفوظ؛ current WPF/genuine یا isolated-copy checklist باقی۔ |

## تجویز کردہ ترتیب — ایک مرحلہ مکمل کر کے اگلا

1. **PT-03 کی detailed report مکمل کریں**، پھر genuine simple sale کے ساتھ **PT-04**۔
2. Current release ہونے کی وجہ سے **PT-10** کی WPF/search/variant checklist کریں؛ automated 900/900 پہلے سے محفوظ ہے۔
3. Data-safety کے لیے isolated copy میں **PT01-BK-02/BK-03** کا A/B/C اور fault evidence دیں۔
4. Retained redesign/manual checks: **PT-09، PT-08، E-12/S-05، Z-05، T-10**۔
5. پھر **PT-05، PT-06، PT-07** کے باقی genuine یا isolated scenarios۔ ہر phase کے بعد نتیجہ بھیجیں؛ نئی development الگ owner approval سے ہوگی۔

---

# 3 — PT-01: Startup، Backup اور UI smoke

**مجموعی owner نتیجہ: PASS — مگر backup/recovery improvements الگ کھلی ہیں۔**

## جو owner نے پہلے ہی آزمایا

- [x] Startup اور sign-in ٹھیک رہا۔
- [x] Photos سمیت backup کامیابی سے zip بنا اور save ہوا۔
- [x] مقررہ pages کامیابی سے کھلے۔
- [x] PT-01 کو owner evidence پر PASS لکھا گیا؛ یہ restore/fault items کو close نہیں کرتا۔

## PT01-BK-01 — Back up as…، folder اور Explorer

**Code میں موجود:** owner منتخب `.zip` path بنا سکتا ہے؛ collision پر نیا suffix بنتا ہے؛ live database اپنی جگہ رہتا ہے؛ backup کے بعد Explorer میں نئی zip دکھانے کا راستہ موجود ہے۔

**Physical checks باقی:**

- [ ] Default folder میں نیا backup بنائیں؛ zip کھلتی ہے اور database + منتخب photos موجود ہیں۔
- [ ] **Back up as…** سے دوسرا local folder منتخب کریں؛ مکمل path واضح ہے اور zip وہیں بنتی ہے۔
- [ ] USB/removable drive پر backup بنائیں؛ USB نکالنے یا permission نہ ہونے پر واضح error آئے۔
- [ ] ایک ہی destination دوبارہ منتخب کریں؛ پرانی zip overwrite نہ ہو، نیا suffix بنے۔
- [ ] **Open the folder / Show this backup** سے حقیقی Windows Explorer کھلے اور نئی zip منتخب یا واضح ہو۔
- [ ] Disk full یا folder unavailable ہو تو پرانا backup محفوظ رہے۔
- [ ] تمام صورتوں میں live `area11.db` کی جگہ اور کام کا data نہیں بدلنا چاہیے۔

## PT01-BK-02 — A/B/C restore اور نئے کام کی حفاظت

**Code میں موجود:** restore confirmation میں date/version اور newer-work warning؛ restore سے پہلے database + photos کا لازمی recovery zip؛ **Before restore** فہرست؛ restart کے بعد recovery summary؛ recovery zip دوبارہ restore کرنے کا automated round-trip۔

**یہ پورا scenario صرف isolated test copy پر کریں:**

- [ ] **A:** نمایاں record کے ساتھ backup A بنائیں؛ تاریخ/وقت اور path لکھیں۔
- [ ] **B:** اس کے بعد ایک مختلف recognizable record بنائیں؛ یہ pre-restore نیا کام ہے۔
- [ ] A restore کریں؛ confirmation میں backup کی date/version/عمر اور newer-work warning پڑھیں۔
- [ ] Restore سے پہلے **Before restore / safety recovery zip** بننے کی تصدیق کریں؛ safety copy ناکام ہو تو restore رکنا چاہیے۔
- [ ] Restart کے بعد **Last restore and recovery** میں restored backup، وقت اور safety copy کا راستہ نظر آئے۔
- [ ] **C:** restore کے بعد B سے مختلف recognizable record بنائیں۔
- [ ] نیا backup بنائیں اور اسے restore کر کے C اور اس وقت کا پورا live data verify کریں۔
- [ ] Pre-restore recovery zip سے B والا data واپس لانے کا راستہ آزمائیں؛ A، B، C یا safety copies میں کوئی خاموش overwrite نہ ہو۔
- [ ] Restore کے فوراً بعد نیا baseline backup کا واضح option دیکھیں۔

**Fault checks — isolated copy:**

- [ ] Damaged zip reject ہو اور current data محفوظ رہے۔
- [ ] Locked file، disk full اور copy/photo step failure پر واضح error اور rollback/recovery ہو۔
- [ ] Hand-made zip میں `../` photo path reject ہو؛ کوئی file photos folder سے باہر نہ لکھی جائے۔

## PT01-BK-03 — Photos، safety copy اور partial restore

**Code میں موجود:** same-volume staged database replacement، photos کی پہلے validation، بعد کی ناکامی پر recovery zip سے rollback کی کوشش، path traversal refusal اور related regressions۔

**Physical ثبوت باقی:**

- [ ] Existing same-name photos والی copy میں restore کریں؛ pre-restore photos قابلِ بازیابی رہیں۔
- [ ] Recovery/safety zip backup list میں **Before restore** کے نام سے ملے اور restore ہو سکے۔
- [ ] Database یا photo step کی ناکامی کو مکمل کامیابی نہ دکھایا جائے؛ partial حالت واضح ہو۔
- [ ] Restore کے بعد app restart، memory refresh اور recovery instructions درست رہیں۔

### PT-01 / BK result

```text
تاریخ اور build:
Item: PT01-BK-01 / BK-02 / BK-03
نتیجہ: PASS / PARTIAL / FAIL
Test copy کا مقام:
A / B / C backup کے نام اور paths:
Default / local / USB / Explorer:
Damaged zip / locked / disk-full / rollback:
Restart کے بعد recovery information:
مسئلہ، screenshot یا report:
```

---

# 4 — PT-02: حقیقی Medicine اور Product Details

**مجموعی owner نتیجہ: PASS / OWNER CONFIRMED — 29 اکتوبر 2026۔**

- [x] 27 اکتوبر: حقیقی product changes save ہوئیں اور Black box میں product changes ریکارڈ ہوئیں۔
- [x] 29 اکتوبر: owner نے **“2 is ok”** لکھا؛ اسی بنیاد پر PT-02 overall PASS ہے۔
- [ ] کوئی نیا regression یا اختلاف ملے تو نیا item ID بنا کر لکھیں؛ پرانے PASS کو خاموشی سے نہ بدلیں۔

## Original PT-02 evidence map

ان میں overall owner confirmation موجود ہے، مگر ہر sub-step کی الگ رپورٹ نہیں بھیجی گئی۔ دوبارہ جانچنا ہو تو یہی checklist استعمال کریں:

- [ ] Blank name پر refusal اور کوئی product نہ بننا۔
- [ ] حقیقی medicine ایک بار بننا؛ name/salt، rack/room، primary category/company، pack formula، base unit، cost/retail/reorder اور barcode درست ہونا۔
- [ ] Name، barcode یا rack سے search اور reopen پر تمام values برقرار ہونا۔
- [ ] Purchase سے پہلے stock 0؛ Products اور Reorder pad بغیر `Double -> Int64` error کے کھلنا۔
- [ ] Margin معقول اور Black box میں درست medicine/user/time/new values ہونا۔
- [ ] Fields/buttons clip نہ ہوں اور pack formula سمجھ آئے۔

## الگ logged PT-02 items — overall PASS کے باوجود اپنا status رکھتے ہیں

| ID | Code/implementation | اب بھی درکار owner evidence |
|---|---|---|
| **PT02-CAT-01** | Primary category + filter-only tags؛ financial total صرف primary سے ایک بار۔ | Tags save/filter، primary/tag conflict، put-away اور no double-count۔ |
| **PT02-NAV-01** | Products category/company links، Dashboard cards اور Clear filter۔ | درست records/counts، back/menu state، keyboard اور empty result۔ |
| **PT02-UNIT-01** | Managed units، rename/put-away، strength search، whole-only guards؛ REAL/double برقرار۔ | `1 box = 10 injections`، `50 ml` strength، fractional refusal، variants اور import/workbook۔ |
| **PT02-PRICE-01** | Explicit discount full retail پر؛ zero discount پر opt-in VIP/Doctor؛ custom-rate role/cost guards۔ | Yes/No، No=retail، held rate، receipt، below-cost اور cashier refusal۔ |
| **PT02-PHOTO-01** | Products/Counter 44px thumbnail یا No photo؛ gallery click۔ | Real/missing/band file، speed، gallery اور sale/search unaffected۔ |
| **PT02-SHIFT-01** | Previous counted closing سے opening؛ Rs 0.01 فرق/reason؛ red alerts؛ کوئی auto payment نہیں۔ | Exact، Rs 1 short، Rs 1 over، handover، alert detail اور Black box۔ |
| **PT02-ALERT-01** | Red/yellow/green severity mapping اور alert-board colors۔ | حقیقی active alerts پر bell کا سب سے سنگین رنگ، expiry reminder اور owner-only red dismissal۔ |
| **PT02-POS-01** | **کوئی guessed fix نہیں؛ reproduction ہی باقی ہے۔** | Exact page/product/stock/sequence، blank/dash field، expected behaviour، screenshot/video/report۔ |
| **PT02-SALE-01** | Successful commit کے بعد Done/invoice/change card اور Settings option۔ | Settings persistence، keyboard/scan continuation، refused sale پر card نہ آنا، ایک click=ایک invoice۔ |

> ان items کی تفصیلی manual-instructions حالت Section 13 میں بھی ہے۔ PT02-POS-01 کو موجودہ evidence سے reproduce یا fixed نہ لکھیں۔

---

# 5 — PT-03: حقیقی Purchase، Batch، Expiry اور Supplier Payable

**موجودہ نتیجہ: PARTIAL — detailed report pending۔ کسی sub-check کو خود PASS نہ لکھیں۔**

## شروع سے پہلے

- PT-02 medicine کا موجودہ stock: `____________________`
- Supplier کا موجودہ payable: `____________________`
- حقیقی delivery کی quantity / pack / batch / expiry: `____________________`

## Genuine delivery checklist

- [ ] **Purchases** میں حقیقی supplier منتخب یا **Add supplier** سے بنایا۔
- [ ] PT-02 والی intended medicine/variant منتخب کی۔
- [ ] درست unit اور حقیقی quantity لکھی؛ expected base qty پہلے حساب کی۔
- [ ] حقیقی batch number اور expiry `YYYY-MM` درج کی۔
- [ ] Cost/Retail درست تھے؛ sample/bonus صرف حقیقی zero-cost sample پر tick ہوا۔
- [ ] **Add line** کے بعد quantity، batch، expiry اور cost دوبارہ پڑھے۔
- [ ] **Paid now** میں اصل رقم لکھی: full، partial یا `0`۔
- [ ] **Save purchase (PINV)** صرف ایک بار دبایا۔

## PASS کے لیے نتائج

- [ ] ایک حقیقی PINV بنا؛ duplicate document نہیں۔
- [ ] Stock میں `quantity × pack conversion` کے برابر اضافہ ہوا۔
- [ ] Intended product/batch میں درست batch اور expiry نظر آئے۔
- [ ] `supplier payable = total − paid now` درست ہے۔
- [ ] Drawer/ledger میں صرف اصل paid cash آیا۔
- [ ] Products، Reorder، Purchases، Supplier ledger اور Black box کی تفصیل ایک جیسی ہے۔
- [ ] کوئی unclear conversion، blank/dash یا unintended stock/payment side effect نہیں۔

### PT-03 result — یہ فارم بھر کر بھیجیں

```text
PT-03: PASS / PARTIAL / FAIL
تاریخ اور build:
Supplier اور PINV:
پہلے stock -> بعد کا stock:
Unit × quantity = expected base qty:
Batch / expiry:
Purchase total / paid / payable:
Products / Reorder / Supplier ledger:
Black box:
کوئی مسئلہ، blank/dash یا UI تبدیلی:
Screenshot / error / report:
```

---

# 6 — PT-04: حقیقی Sale، Cart، Cash Change، Receipt اور Completion

**موجودہ نتیجہ: INSTRUCTIONS READY — itemized owner result pending۔**

**حد:** اصل books میں صرف حقیقی فروخت۔ پہلی sale simple cash ہو؛ fake customer/payment/credit test نہیں۔

## شروع سے پہلے

- PT-03 کے بعد product stock: `____________________`
- [ ] Drawer/shift کھلا ہے اور opening cash حقیقی counted رقم ہے۔
- [ ] Opening رقم PT02-SHIFT-01 کے ساتھ لکھی ہے۔
- [ ] پہلی simple sale میں discount، VIP/Doctor اور loyalty کو غیر ضروری complication نہیں بنایا۔

## Genuine sale checklist

- [ ] **Counter (POS)** میں medicine کو name سے search کیا۔
- [ ] حقیقی barcode ہو تو scan + Enter آزمایا؛ intended product/unit آیا۔
- [ ] درست unit اور quantity کے ساتھ **Add to cart** کیا۔
- [ ] Add سے پہلے/بعد Retail-price blank یا dash نہیں؛ stock/unit/cart item/qty/rate/total درست ہیں۔
- [ ] حقیقی customer ہو تو منتخب کیا؛ سادہ cash sale میں customer لازم نہیں رکھا۔
- [ ] Payment **Cash** اور حقیقی **Cash received** درج کی۔
- [ ] Change درست ہے؛ received کم ہونے پر واضح refusal آتی ہے۔
- [ ] حقیقی receipt چاہیے تو auto-print استعمال کیا۔
- [ ] **Complete sale (F4)** صرف ایک بار دبایا۔
- [ ] Done/checkmark، invoice، change/due اور next-sale راستہ واضح ہے۔

## PASS کے لیے نتائج

- [ ] ایک INV بنا اور cart کی رقم ہی final bill بنی۔
- [ ] Stock درست unit/qty سے کم ہوا؛ FEFO/batch behaviour درست رہا۔
- [ ] Total، received، change اور drawer فرق درست ہیں۔
- [ ] Printed receipt اور screen کے items/total ایک جیسے ہیں۔
- [ ] Black box میں sale، payment اور print/reprint entries درست ہیں۔
- [ ] **Print last bill again** سے وہی bill دوبارہ print ہوا؛ نئی sale/payment نہیں بنی۔
- [ ] حقیقی khata sale ہو تو customer balance قبل/بعد درست ہے؛ fake credit test نہیں بنایا۔

### PT-04 result

```text
PT-04: PASS / PARTIAL / FAIL
تاریخ اور build:
INV:
Cart price / stock / unit:
Total / received / change:
پہلے stock -> بعد کا stock:
Receipt اور reprint:
Sale completion ظاہر ہوا:
Black box:
کوئی blank/dash یا دوسرا مسئلہ:
Screenshot / error / report:
```

---
# 7 — PT-05: Returns، Provisional Return (PR) اور Quarantine (Q)

**موجودہ نتیجہ: PENDING۔**  
**محفوظ طریقہ:** حقیقی return اصل books میں ہو سکتی ہے؛ fake return، repeated/refund-limit، cancellation، insufficient-stock یا forced fault صرف isolated test copy میں۔

## A. Genuine bill return (RET)

- [ ] Returns میں اصل bill کو printed number سے تلاش کریں؛ ضرورت ہو تو phone/customer/medicine/date search بھی آزمائیں۔
- [ ] صرف اسی bill کی باقی return-able quantity/value منتخب کریں؛ guess یا unrelated bill نہ جوڑیں۔
- [ ] Return کی وجہ لکھیں اور RET محفوظ کریں؛ ایک permanent RET number بنے۔
- [ ] Stock صرف مقررہ quantity سے بڑھے، یا line مقررہ rule کے مطابق quarantine میں جائے۔
- [ ] Cash refund اور customer khata صرف اسی bill کے actual paid/due/refund rule کے مطابق بدلیں؛ دوسری bill کا balance نہ بدلے۔
- [ ] Repeated return پر cumulative quantity/value cap رکے؛ refusal کے بعد کوئی partial stock/payment row نہ بنے۔
- [ ] Return receipt پر RET، original INV، reason اور ہر line کا stock/quarantine واضح ہو۔
- [ ] Reprint پر وہی historical numbers رہیں؛ نئی payment یا stock movement نہ بنے۔
- [ ] Black box میں return، payment/refund اور print/reprint entries درست ہوں۔

## B. No-bill / Provisional Return (PR) — isolated copy

- [ ] PR بنائیں، reason لکھیں اور status **pending / unchecked** رہنے دیں۔
- [ ] Pending PR کی refund/restock صرف ایک بار ہو؛ دوبارہ کھولنے یا save کرنے سے duplicate نہ بنے۔
- [ ] بعد میں PR کو اس کے حقیقی INV سے link کریں؛ linking پر **کوئی دوسری stock movement، payment یا khata event نہ بنے**۔
- [ ] Original bill کے sale lines کے ساتھ allocated quantities/refund درست ہوں؛ wrong product یا excessive quantity refuse ہو۔
- [ ] Split/repeated lines اور ایک سے زیادہ PRs کے بعد بھی RET caps اور over/under-refund واضح رہیں۔
- [ ] پرانا header-only link ہو تو صرف same-bill repair مانگے؛ unrelated bill سے repair نہ ہو۔
- [ ] PR reconcile/cancel کے بعد double return ممکن نہ ہو۔

## C. PR cancellation — financial boundary لازم

- [ ] Valid reason کے ساتھ PR cancel کریں۔
- [ ] Cancellation **صرف same batch کو stock-only rule سے بدلے**۔
- [ ] Cancellation سے customer cash repayment، نیا positive payment، drawer inflow یا original-financial reversal/report-reversal row **نہ بنے**۔
- [ ] Insufficient stock ہو تو cancellation atomically refuse ہو، PR pending رہے، cart/form محفوظ رہے اور payment نہ بنے۔
- [ ] Black box میں reason، batch، stock result اور refusal/cancellation واضح ہوں۔

## D. Quarantine (Q)

- [ ] Q کو permanent number ملے اور goods خود restock نہ ہوں۔
- [ ] Owner یا manager disposition کر سکے؛ صرف authorized role کو اجازت ہو۔
- [ ] Alert dismiss owner-only اور reason کے ساتھ ہو۔
- [ ] Disposition کے بعد history محفوظ رہے؛ کوئی silent delete نہ ہو۔

### PT-05 result

```text
PT-05: PASS / PARTIAL / FAIL
تاریخ اور build / real books یا isolated copy:
Original INV / RET / Q / PR numbers:
Quantity اور stock before -> after:
Cash / khata / payable before -> after:
PR link پر نیا stock/payment event ( ہونا چاہیے: نہیں ):
PR cancel پر payment/drawer inflow ( ہونا چاہیے: نہیں ):
Over-return / insufficient-stock refusal:
Receipt / Black box:
مسئلہ، screenshot یا report:
```

---

# 8 — PT-06: Stock-take، Write-off، Supplier Return، Expiry اور Alerts

**موجودہ نتیجہ: PENDING۔**  
Fake adjustment، controlled shortage، negative-stock یا destructive scenario کے لیے isolated test copy لازم ہے۔

## A. Stock-take (ST)

- [ ] نیا stock-take شروع کریں؛ frozen book quantities اور count sheet محفوظ ہوں۔
- [ ] Book / counted / difference / value columns درست دکھیں۔
- [ ] کچھ rows intentionally uncounted چھوڑیں؛ apply پر وہ zero نہ ہوں۔
- [ ] Genuine یا isolated zero-count اور non-zero-count دونوں درج کریں۔
- [ ] Owner کے علاوہ apply refused ہو؛ owner apply صرف ایک بار ہو۔
- [ ] Apply کے بعد audited adjustment بنے، frozen cost رہے اور ST دوبارہ apply نہ ہو۔
- [ ] Found stock ہو تو مقررہ `GINTI` batch/rule کے مطابق آئے۔
- [ ] Historical minus کو physical count سے correct کیا جا سکے، مگر کوئی خاموش auto-zero نہ ہو۔

## B. Write-off / breakage / goods drawing

- [ ] حقیقی breakage/expiry یا isolated example کی batch، quantity اور reason درج کریں۔
- [ ] Removal cost پر value ہو؛ sale price سے valuation نہ ہو۔
- [ ] Available سے زیادہ remove کرنے پر refusal میں available/requested واضح ہوں۔
- [ ] Refusal atomic ہو، typed form رہے، number ضائع نہ ہو اور کوئی partial stock row نہ بنے۔
- [ ] Stock، report اور Black box ایک جیسے نتیجہ دکھائیں۔

## C. Supplier return (SR)

- [ ] Genuine supplier return یا isolated test میں supplier/batch/quantity منتخب کریں۔
- [ ] Credit note یا cash mode حقیقت کے مطابق ہو۔
- [ ] Stock کم اور supplier payable/cash درست بدلے؛ unrelated customer یا sale نہ بدلے۔
- [ ] Repeated lines shared batch سے زیادہ stock return نہ کر سکیں؛ failed attempt پر SR number ضائع نہ ہو۔
- [ ] SR receipt/ledger/Black box درست ہوں۔

## D. D-93 negative-stock retirement — isolated copy

- [ ] Repeated cart lines کی combined quantity stock سے زیادہ ہو تو sale شروع ہونے سے پہلے refusal آئے۔
- [ ] Refusal available/requested بتائے، cart برقرار رہے اور اگلے valid bill میں number gap نہ آئے۔
- [ ] Exact zero تک sale کامیاب ہو؛ اگلی unit refused ہو۔
- [ ] Positive batch 5 اور legacy batch -3 کی fixture میں 3-unit sale refused ہو اور physical stock-take guidance ملے۔
- [ ] Ordinary shortage میں reduce/receive guidance رہے؛ wrong guidance نہ آئے۔
- [ ] Write-off، goods drawing، supplier return اور PR cancellation سب central stock guard سے رکیں۔
- [ ] Legacy negative rows تاریخ/reports/returns سے غائب نہ ہوں؛ صرف audited physical stock-take correction ہو۔
- [ ] Fractional quantity واقعی ضرورت ہو تو REAL/double behaviour درست رہے؛ کسی quantity کو INTEGER cast نہ کریں۔

## E. Expiry، low-stock اور alert board

- [ ] Settings کی expiry bands بدلنے پر colors/levels انھی settings سے بدلیں۔
- [ ] Nearing expiry nearest band میں light/green reminder ہو؛ expired stock کو ordinary green نہ لکھیں۔
- [ ] Low-stock، supplier-cutoff/quarantine warnings مقررہ severity اور detail دکھائیں۔
- [ ] Supplier return cutoff کی last acceptable date واضح ہو۔
- [ ] Stock-minus registry read-only رہے؛ monthly review صرف Black-box note ہو، stock change نہ ہو۔
- [ ] Dashboard/alert board low-stock کے ساتھ بغیر cast error کھلے۔

### PT-06 result

```text
PT-06: PASS / PARTIAL / FAIL
تاریخ اور build / real books یا isolated copy:
ST / write-off / SR numbers:
Book / counted / applied differences:
Zero اور non-zero counts:
Exact-zero / shortage / legacy-minus results:
Stock / payable / drawer before -> after:
Expiry / alert colours:
Black box:
مسئلہ، screenshot یا report:
```

---

# 9 — PT-07: Restore، Real Excel/CSV اور Printer/USB Scanner

**موجودہ نتیجہ: PENDING؛ printer-slip کا ایک حصہ پہلے confirm ہے، باقی نہیں۔**

## A. Backup اور clean-PC recovery

- [ ] PT01-BK-01 کا default/local/USB/Explorer checklist مکمل ہو۔
- [ ] PT01-BK-02 کا A/B/C اور PT01-BK-03 کا fault checklist isolated copy میں مکمل ہو۔
- [ ] ممکن ہو تو clean PC یا clean test profile پر database + photos restore کر کے product، bill اور photo کھولیں۔
- [ ] Restore کے بعد live data، version/migrations اور photos درست ہوں؛ پرانا backup overwrite نہ ہو۔

## B. Native `.xlsx` اور CSV — real Excel درکار

- [ ] **Excel / CSV -> Import** سے blank template save کر کے real Excel میں کھولیں۔
- [ ] **Products، Customers، Suppliers، Instructions** چاروں readable sheets ہوں۔
- [ ] ایک حقیقی یا isolated medicine کو opening stock اور expiry کے ساتھ preview/import کریں؛ opening stock real batch بنے۔
- [ ] Opening udhaar import ہو تو پہلی khata line بنے؛ preview کے بغیر apply نہ ہو۔
- [ ] Import کے بعد وہی file دوبارہ منتخب کریں؛ duplicate skip ہو، stock/udhaar دوبارہ نہ بڑھے۔
- [ ] **Bulk update -> Download your data** میں آٹھ متوقع sheets ہوں؛ photos، secrets یا internal counters export نہ ہوں۔
- [ ] Medicine کا name/retail بدلیں؛ دوسری editable cell خالی چھوڑیں تو **empty = no change** رہے۔
- [ ] Allowed optional value کو `__CLEAR__` سے صاف کریں؛ apply سے پہلے ہر **before -> after** line پڑھیں۔
- [ ] `Stock on shelf`، `Udhaar now` یا `Payable now` بدلنے کی کوشش پر پورا workbook refuse ہو؛ کوئی partial change نہ ہو۔
- [ ] Settings صرف الگ tick پر بدلیں اور string/number/array types برقرار رہیں۔
- [ ] Apply ایک transaction میں ہو؛ Black box rows درست آئیں۔
- [ ] وہی workbook دوبارہ upload کریں تو **0 changes** آئے۔
- [ ] Workbook سے stock یا account balance خود نہیں بدلنا چاہیے۔
- [ ] Managed unit spelling، stale/put-away unit اور apply-time revalidation کی refusal واضح ہو۔

## C. Printer اور USB scanner — F-46

**محفوظ evidence:** printer-slip والا حصہ owner نے 23 اکتوبر کو shop printer پر دیکھا تھا۔ **USB scan سے medicine ملنا اور Black box record ابھی unconfirmed ہے۔**

- [ ] **SYSTEM -> Printer & scanner** میں Windows کے installed printers کی list سے درست printer منتخب کریں۔
- [ ] Test slip مقررہ 58 mm یا 80 mm paper پر print ہو۔
- [ ] Ruler کے تمام characters نظر آئیں: **58 mm = 32، 80 mm = 48**؛ آخری digit کٹے تو width setting دیکھیں۔
- [ ] Paper صاف cut/tear ہو۔
- [ ] USB scanner بغیر اضافی click کے box میں code type کر کے Enter دے۔
- [ ] Scan سے درست medicine اور pack ملے؛ unknown code ہو تو test record میں واضح distinguish ہو۔
- [ ] Counter سے ایک حقیقی bill print ہو؛ totals، khata line اور footer صاف ہوں۔
- [ ] چھ boxes tick کر کے **Save the result** کریں؛ missing tick ہو تو reason لکھیں۔
- [ ] Date، person، paper width اور `hardware_test` Black-box entry محفوظ ہوں۔
- [ ] Paper width بدلنے پر پرانے ticks stale ہوں اور نئی slip دوبارہ test ہو۔

### PT-07 result

```text
PT-07: PASS / PARTIAL / FAIL
تاریخ اور build:
Restore / A-B-C / fault result:
Excel version اور file type:
Four template sheets / eight export sheets:
Duplicate import / protected columns / repeated upload:
Printer model اور paper width:
Six checks: printed / fits / cut / scanner / found / bill:
USB scan سے medicine اور pack:
Black box hardware_test:
مسئلہ، screenshot یا report:
```

---
# 10 — PT-08: Redesign Phase 1 — Expanded Header / Navigation

**حالت:** Zircon کے **883/883 Windows xUnit** محفوظ ہیں؛ WPF/navigation owner verification pending۔  
**Current build پر نوٹ:** اب version Cobalt ہے۔ Current Cobalt پر یہ retained-shell checks کیے جا سکتے ہیں، مگر expected version **v0.38.0 Cobalt** ہوگی؛ 883 صرف historical Zircon total ہے۔ صرف Zircon reproduce کرنا ہو تو real books downgrade نہ کریں۔

## Automated ثبوت

- [x] Zircon: 883 passed / 0 failed / 0 skipped، 1 منٹ 10 سیکنڈ۔
- [x] اس release کے static checks اور XAML binding checks محفوظ ہیں۔
- [ ] یہ WPF navigation کا physical PASS نہیں؛ نیچے کے خانے صرف نظر آنے والے نتیجے پر tick کریں۔

## مالک کی physical checklist

- [ ] **Launch/build الگ لکھیں:** current copy `START-AREA11.bat` سے کھلی اور version/color درست تھی۔ کوئی error ہو تو build log/report منسلک کریں۔
- [ ] **Header identity:** اوپر صرف **Area11** ہے؛ shop name مرکز میں نہیں۔ Current copy کی پہچان Cobalt color/version سے ہو۔
- [ ] **Bell:** سب سے دائیں 44×44 bell، count اور severity color درست؛ click سے Alerts کھلیں اور واپسی پر previous module منتخب ہو سکے۔
- [ ] **Modules:** Dashboard، SELL، STOCK، CUSTOMERS & SUPPLIERS، MONEY، SYSTEM کھولیں؛ ہر module کا پہلا page کھلے۔
- [ ] Module بدل کر واپس آنے پر اس module کا موجودہ page برقرار رہے؛ active module/page واضح ہوں۔
- [ ] **Settings sections:** Shop، Printing، Sales rules، Cloud sync، Staff & permissions، App words اوپر clickable ہوں؛ اندرونِ صفحہ دوسری tab strip نہ ہو۔
- [ ] System کے عام pages دستیاب رہیں؛ Settings سے Backup/Audit یا دوسرا page کھولا جا سکے۔
- [ ] ایک Settings section میں غیر محفوظ text لکھ کر دوسرا section اور واپسی کریں؛ text ضائع نہ ہو؛ Save صرف ایک بار اور existing rules کے مطابق ہو۔
- [ ] **Products category:** header dropdown سے keyboard/type کر کے category منتخب کریں؛ وہی products اور count آئیں؛ All categories پر واپسی درست ہو۔
- [ ] Dashboard/category row سے آنے پر header میں وہی category منتخب نظر آئے۔
- [ ] **Editor/filter separation:** product editor کا unsaved category header filter بدلنے سے نہ بدلے۔
- [ ] **Put-away edge — isolated copy:** filtered category put away ہو تو filter All categories پر صاف ہو؛ invisible filter نہ رہے۔
- [ ] **Permissions:** cashier/limited user کو صرف permitted modules/pages دکھیں؛ owner-only route اور service-level refusal برقرار رہے۔
- [ ] **Keyboard:** Tab/Enter سے modules، Settings اور bell تک واضح focus کے ساتھ پہنچا جا سکے۔
- [ ] **Screen fit:** 1240×760 اور اپنی normal resolution پر button/text clip یا overlap نہ ہو؛ SYSTEM کے تمام pages قابلِ رسائی ہوں۔
- [ ] Alerts، session warning، empty-page message اور failed-button/fault card موجودہ محفوظ طریقے سے کام کریں۔
- [ ] Header بدلنے سے page content یا half-typed bill ضائع نہ ہو۔
- [ ] Current-user menu سے **My settings** اور **Sign out** کھلیں؛ version **v0.38.0 · Cobalt** ہو۔

### PT-08 result

```text
PT-08: PASS / PARTIAL / FAIL
تاریخ اور actual build/version:
Automated evidence الگ: Zircon 883/883 historical
Current launch/build log:
Modules / back-state / Settings sections:
Category filter / editor separation:
Permissions / keyboard / screen fit:
Bell / user menu / sign-out:
مسئلہ، screenshot یا report:
```

---

# 11 — PT-09: Redesign Phase 2 — Own Fields Governance

**حالت:** Code ready؛ Aurora کا 892/892 Windows run اور owner physical PASS محفوظ نہیں۔ Current Cobalt میں یہ features retained ہیں؛ 892 کو current runner کا expected total نہ بنائیں۔

## Automated/development ثبوت

- [x] Aurora کے 892 written cases اور migration 029 کے automated/development checks محفوظ ہیں۔
- [x] Current Cobalt کے 900/900 Core/Tests owner-verified ہیں، مگر یہ PT-09 WPF physical proof نہیں۔
- [ ] Aurora-specific Windows run اور نیچے کے physical نتائج الگ درکار ہیں۔

## محفوظ شروعات

- [ ] Existing database کھولنے سے پہلے backup لیا۔
- [ ] Migration کے بعد existing products، customers، suppliers اور custom answers موجود ہیں۔
- [ ] Required/invalid/legacy/fault scenarios کے لیے isolated test copy منتخب کی۔

## Physical checklist

- [ ] **نیا field:** SYSTEM -> Own fields -> Medicines میں حقیقی optional text field بنائیں، مثلاً `Shelf note`۔
- [ ] **Help on the record form** میں سادہ یک سطری guidance لکھیں؛ save کے بعد stable key `product.custom.<id>` لکھ لیں۔
- [ ] Existing product کھولیں؛ نیا field اور guidance فوری نظر آئیں، rebuild/restart نہ چاہیے۔
- [ ] جواب لکھ کر **Save extra fields** کریں؛ product بند/دوبارہ کھولنے پر جواب اور guidance برقرار رہیں۔
- [ ] **Rename-safe identity:** نام `Rack note` بدلیں؛ stable key نہ بدلے، پرانا جواب رہے، Black box میں old -> new definition آئے۔
- [ ] صرف guidance بدلیں؛ answer نہ بدلے، نئی guidance آئے، audit لکھے۔
- [ ] صرف نام/type بدلنے پر existing guidance خود نہ مٹی ہو۔
- [ ] **Required atomic refusal — isolated copy:** خالی required answer کے ساتھ optional answer بھر کر save کریں؛ field-named refusal آئے اور optional answer بھی partial save نہ ہو۔
- [ ] Valid answer دینے پر save کامیاب ہو۔
- [ ] **سات types — isolated copy:** text، number، amount، date، list، yes/no، photo کے safe examples reopen کے بعد درست ہوں۔
- [ ] Amount درست rupees، date canonical، list صرف defined choices، photo existing gallery rule پر ہو۔
- [ ] Number میں `NaN` / `Infinity`، name/help میں multiline/control text، list میں undefined choice refuse ہوں؛ خاموش `0` یا غلط stored value نہ بنے۔
- [ ] **Put away / restore:** field form سے ہٹے مگر answer database میں رہے؛ Show it on the form again پر وہی key اور پرانا answer واپس آئے۔
- [ ] Delete/undo جیسا خطرناک راستہ نہ ہو۔
- [ ] **Permissions:** cashier/limited login Own fields manage نہ کر سکے؛ record permission کے مطابق ہی answers ملیں؛ owner/manager management برقرار رہے۔
- [ ] **Legacy upgrade — isolated pre-029 copy:** existing field/answer کے ساتھ migration ایک بار لگے، key `entity.custom.id` بنے، answer unchanged رہے؛ restart پر duplicate migration/error نہ ہو۔
- [ ] **No business side effect:** اس testing میں کوئی PINV/INV/payment یا stock quantity نہ بدلے۔ فرق نظر آئے تو test روک کر report بھیجیں۔

### PT-09 result

```text
PT-09: PASS / PARTIAL / FAIL
تاریخ اور actual build/version:
Windows xUnit (الگ automated evidence): total / failed / skipped / duration
Migration 029 first start / restart:
Entity + field id + stable key:
Help text before -> after:
Rename before -> after; key unchanged؟
Answer before -> after:
Required/invalid refusal and atomicity:
Seven types checked:
Put away / restore:
Permissions:
Black box old -> new:
Stock/payment/document side effect ( ہونا چاہیے: نہیں ):
Screenshots / error / test.log:
```

---

# 12 — PT-10: Redesign Phase 3 — Packaging، Variants اور Search

**حالت:** Cobalt Core/Tests **900/900 PASS**؛ PT-10 WPF/genuine physical verification pending۔ Phase 4 اس میں شامل نہیں۔

## Automated اور development ثبوت

- [x] 29 اکتوبر: **900 total / 900 passed / 0 failed / 0 skipped / 1 m 4 s**۔
- [x] `ALL GREEN - 900 of 900` موصول ہوا۔
- [x] 55/55 static checks اور 7/7 actual ProductService SQLite search groups کامیاب۔
- [ ] `Area11.Desktop` / WPF build اور `START-AREA11.bat` launch کو الگ evidence چاہیے؛ Core/Tests log کو اس کا ثبوت نہ لکھیں۔
- [ ] نیچے کے pack/search/barcode/conversion checks کا physical result ابھی pending ہے۔

## Data boundary

- [ ] اصل books میں صرف genuine medicines/delivery استعمال کی۔
- [ ] Fake variants، invalid input، controlled conversion یا risky financial side-effect check کے لیے backup کے ساتھ isolated copy استعمال کی۔

## Physical checklist

- [ ] **Current launch:** actual app کھلی، **v0.38.0 · Cobalt** نظر آیا؛ build/launch result الگ لکھا۔
- [ ] **دو الگ variants — isolated copy:** ایک ہی نام کی دو medicines، مثلاً strength `5 ml` + pack `50 mL bottle` اور strength `10 ml` + pack `100 mL bottle`؛ base unit `injection`۔
- [ ] Save کے بعد الگ product rows/IDs رہیں؛ کوئی merge یا نیا stock unit نہ بنے۔
- [ ] **Editor persistence:** پہلی product بند/دوبارہ کھولنے پر pack/variant label اور Products grid کا **Pack / variant** column درست ہو۔
- [ ] New product کھولنے پر پرانا label خود copy نہ ہو۔
- [ ] **Normalization:** `  50   mL bottle  ` save کریں؛ reopen پر `50 mL bottle` ہو۔
- [ ] Multiline/control label اور 100 characters سے لمبا label refuse ہو؛ پیغام واضح ہو۔
- [ ] Refusal پر name/rate/stock/formula میں کوئی partial change نہ ہو۔
- [ ] **Products search:** `50ml` سے صرف 50 mL variant اور `100 ML` سے صرف 100 mL variant آئے۔
- [ ] Brand، room، primary category اور company سے بھی intended product ملے۔
- [ ] Exact pack hit پر fuzzy suggestion نہ آئے؛ unknown نام پر existing fuzzy note الگ واضح رہے۔
- [ ] **Counter search:** وہی `50ml` / `100 ML` searches میں Pack / variant column اور selected row intended variant کی ہو۔
- [ ] Search صرف list filter کرے؛ کوئی medicine خود cart میں add نہ ہو۔
- [ ] **Existing pack barcode:** active strip اور box codes scan + Enter پر پہلے کی طرح درست product/pack unit add کریں؛ put-away code کام نہ کرے۔
- [ ] **Purchase discovery:** pack label، strength، brand یا barcode سے intended product ملے۔
- [ ] Dropdown میں **name — strength · pack** نظر آئے؛ ایک جیسے نام والی variants الگ ہوں؛ selected row وہی رہے۔
- [ ] **Conversion — isolated copy:** `1 box = 2 inner packs` اور `1 inner pack = 5 injections` پر ایک box کا quote **10 base injections** رہے۔
- [ ] صرف pack label بدلنے سے quantity، rate، batch cost/retail یا stock نہ بدلے۔
- [ ] **No financial/history side effect:** label edit سے پہلے/بعد existing PINV/INV، batches، balances اور payments یکساں رہیں۔
- [ ] کوئی نیا payment، drawer inflow، invoice، reversal یا stock adjustment نہ بنے؛ صرف expected product-edit Black-box entry ہو۔
- [ ] **Keyboard/screen:** Products editor، Counter grid اور Purchase dropdown میں Tab/Enter کام کرے؛ pack text سے Save، Add line یا control clip/overlap نہ ہو۔

### PT-10 result

```text
Build: v0.38.0 Cobalt
PT-10: PASS / PARTIAL / FAIL
تاریخ:
Windows Core/Tests: 900 total / 0 failed / 0 skipped / 1 m 4 s — owner verified 29 Oct 2026
Desktop/WPF build اور START-AREA11.bat launch (الگ):
Two variant product IDs and stock units:
Pack label before -> after reopen:
Normalization / multiline / over-100 refusal:
Products 50ml / 100 ML results:
Counter 50ml / 100 ML and pack-column results:
Strip / box extra-barcode scans:
Purchase search and displayed variant choice:
Box/strip/base conversion before -> after label edit:
Rates / batches / payments / documents side effect ( ہونا چاہیے: نہیں ):
Black-box product edit:
Keyboard / screen fit:
Screenshots / error / test.log:
```

---
# 13 — Manual Instructions: کیا implement ہوا اور کیا باقی ہے

**پڑھنے کا اصول:** “Code میں موجود” کا مطلب source اور متعلقہ automated coverage موجود ہے؛ اس کا مطلب owner physical PASS نہیں۔ جہاں owner confirmation ملی ہے وہ الگ لکھی ہے۔

## 13.1 Master implementation tracker

| Item | Code / implemented حالت | باقی یا independent owner action |
|---|---|---|
| **PT01-BK-01** | Back up as…، chosen zip، unique suffix، live DB unchanged، Explorer/show-backup path۔ | Default/local/USB، Explorer selection، permission/disk-full اور no-overwrite physical proof۔ |
| **PT01-BK-02 / BK-03** | Newer-work warning، full recovery zip، Before restore list، restart summary، staged replacement، photo validation/rollback اور traversal refusal۔ | Isolated A/B/C، B recovery، damaged/locked/disk-full/photo faults اور restart recovery proof۔ |
| **PT02-CAT-01** | Primary category + additional filter-only tags؛ financial totals primary سے ایک بار۔ | Tag save/search/filter، conflict guard، put-away اور report no-double-count۔ |
| **PT02-NAV-01** | Clickable category/company، Dashboard detail cards، Clear/empty filter states۔ | Actual records/counts، mouse/keyboard، back/menu state اور empty result۔ |
| **PT02-UNIT-01** | Managed units، canonical spelling، rename/order/put-away، strength، whole-only flag، import guards؛ REAL/double محفوظ۔ | Whole injection، 50 ml variant، fractional refusal، rename/put-away، genuine stock اور real workbook checks۔ |
| **PT02-PRICE-01** | Full-retail explicit discount، opt-in special rate، default No=retail، Owner/Manager custom rate، cost/role/paisa guards۔ | Yes/No popup، held custom rate، receipt/margin، below-cost اور cashier refusal۔ |
| **PT02-PHOTO-01** | Products/Counter thumbnails، No photo fallback، gallery click؛ photo sale کی شرط نہیں۔ | Real/missing/closed file، loading، gallery اور sale/search unaffected۔ |
| **PT02-SHIFT-01** | Previous counted opening، exact/paisa difference reason، opening/closing red alerts، audit؛ auto payment نہیں۔ | Exact، Rs 1 short/over، handover، next opening، detail link اور no automatic financial row۔ |
| **PT02-ALERT-01** | Central red/yellow/green mapping، highest severity wins، alert-board colors، nearing-expiry reminder۔ | حقیقی bell colors، expiry band، red owner review/reason اور dismissal behaviour۔ |
| **PT02-POS-01** | **Needs reproduction؛ کوئی guessed diagnosis/fix نہیں۔** | Exact page، product/stock/batch، sequence، blank/dash field، expected/actual، screenshot/video اور report۔ |
| **PT02-SALE-01** | Committed sale کے بعد Done/invoice/change/due card؛ setting OFF؛ next scan/card dismiss؛ auto-print الگ۔ | Persistence، keyboard/scan، exact change، refused sale پر no card، one click=one invoice۔ |
| **D-96 / PT-08** | Expanded dark header، fixed Area11، modules/context pages، Settings sections، 44×44 bell۔ | Retained WPF navigation، permissions، unsaved state، keyboard اور screen fit۔ |
| **D-97 / PT-09** | Central field contract، stable keys، guidance، validation، migration 029۔ | Aurora evidence الگ؛ current WPF fields/rename/help/types/permissions/legacy/no-side-effect proof۔ |
| **D-98 / PT-10** | Editable normalized pack label، shared Products/Counter/Purchase search، separate variants؛ 8 focused regressions۔ | Core/Tests 900/900 کے علاوہ Desktop launch، WPF selection، barcodes، conversion اور no-side-effect proof۔ |
| **E-04** | Category/company create/rename/order/put-away/restore، two-level guard، rep/visit fields، permissions۔ | Live parent/child، third-level refusal، linked put-away/restore، audit اور cashier refusal۔ |
| **E-12 / S-05** | 32 App words، stable navigation keys، live menu/page headings، Unicode/atomic audit/reset۔ | Rename/restart/reset، current heading، Cloud/Own fields labels، blank refusal، old bill unchanged۔ |
| **Z-05** | Read-only Black-box earlier/recorded-new reader، Unicode/details/guidance اور module correction route۔ | Actual selected rows، PR guidance، filter clearing، copy-only behaviour، no business/audit mutation۔ |
| **T-05** | Native per-day chart پہلے سے موجود؛ matrix میں **DONE**؛ duplicate engine نہیں بنایا۔ | صرف retained visual check تجویز شدہ؛ اسے نیا unfinished feature نہ سمجھیں۔ |
| **T-10** | Native `.xlsx` codec، four-sheet template، eight-sheet export، preview/atomic bulk update/protected balances۔ | Real Excel، duplicate import، `__CLEAR__`، protected refusal، types، audit اور repeated 0 changes۔ |
| **D-93** | Central StockPolicy، repeated-line/shared-batch reservation، exact zero، atomic refusals، historical-minus preservation۔ | Isolated physical shortage scenarios، stock-take correction، write-off/drawing/SR/PR cancel guards۔ |
| **Lapis PR reconciliation** | PR lines original sale lines سے allocate؛ link پر no new stock/payment؛ RET caps/over-under warnings۔ | Actual PR/INV link، split/repeated lines، same-bill repair، no double return اور cancellation boundary۔ |
| **Paprika / Quasar repairs** | REAL stock projections، Unicode settings audit، REAL PR/RET reads اور detailed link errors۔ | Isolated `A empty`/`B stocked` 3.5، Urdu audit، PR attach/no duplicate event؛ error ہو تو تفصیل بھیجیں۔ |
| **Merlot follow-up** | Low-stock alert read اور PR reconciliation read repairs۔ | Dashboard low-stock fold/no cast error اور pending PR link without another stock/payment event۔ |
| **F-46 hardware** | Printer list/selection، test slip/ruler، six checks، scanner lookup، saved audit record۔ | Slip half 23 Oct confirmed؛ USB scan -> medicine، real bill، saved Black box اور width-change retest باقی۔ |
| **Cloud sync** | Format-2 push/preview/backup-first pull code موجود؛ partial mirror ہے۔ | موجودہ PT scope سے باہر؛ real Supabase test صرف الگ منظوری، test Shop id/table اور isolated copy پر۔ |

## 13.2 E-12 / S-05 — App words کا retained physical walk-through

- [ ] Settings -> **App words** میں Counter (POS) کو `ریکاؤنٹر` اور Settings کو `Choices` لکھ کر Save کریں۔
- [ ] Save کے فوراً بعد menu rows اور موجودہ Settings heading بدلیں۔
- [ ] Counter کھول کر اس کی heading دیکھیں۔
- [ ] Dashboard کو `Home` کریں؛ menu row اور page heading دونوں بدلیں۔
- [ ] Cloud sync کا نام بدلیں تو اس کا Settings tab بدلے۔
- [ ] Own fields والے record پر panel heading نئے Own fields label سے آئے۔
- [ ] App restart کے بعد custom words برقرار رہیں۔
- [ ] Blank label کے ساتھ Save کریں؛ پوری save refuse ہو اور دوسری settings نہ بدلیں۔
- [ ] **Restore original words** کو دو بار استعمال کریں؛ تمام 32 defaults واپس آئیں۔
- [ ] Black box میں `labels.*` کے old/new rows اور Urdu Unicode صاف پڑھے جا سکیں۔
- [ ] پرانا bill reprint کریں؛ INV number اور historical wording rewrite نہ ہو۔
- [ ] Custom labels سے routes، permissions، Shift/Settings shortcuts یا page resolution نہ ٹوٹیں۔

## 13.3 Z-05 — Black box earlier-state reader

- [ ] Black box میں ایسی settings change منتخب کریں جس کے old/new JSON ہوں۔
- [ ] Complete earlier value، recorded-new value، Unicode، details اور guidance پڑھیں۔
- [ ] Text copy ہو سکے مگر edit/Undo/Restore command موجود نہ ہو۔
- [ ] PR cancellation row پر guidance ہو: same-batch stock-only، no payment، no original-financial reversal۔
- [ ] Search/filter بدلنے پر پرانا selected snapshot غائب ہو۔
- [ ] Reader کھولنے/copy کرنے سے کوئی business یا audit row نہ بدلے۔
- [ ] Recorded-new کو غلطی سے “اب کی live value” نہ سمجھا جائے۔

## 13.4 E-04 — Category / Company management

- [ ] Parent اور child category بنائیں؛ rename/reorder محفوظ ہو۔
- [ ] Third level refused ہو۔
- [ ] Linked category/company put away کریں؛ product اور old records باقی رہیں، صرف current link clear ہو۔
- [ ] Put-away audit میں affected count درست ہو۔
- [ ] Restore پر پرانا link خود invent نہ ہو۔
- [ ] Company rep، phone، notes اور visit-day محفوظ رہیں۔
- [ ] Cashier write refused ہو۔
- [ ] Primary/tag filter اور financial count double-count نہ کرے۔

## 13.5 Paprika / Quasar / Merlot retained checks — isolated copy

- [ ] Never-received `A empty` اور 3.5 base units والی `B stocked` بنائیں۔
- [ ] Products اور Reorder pad دونوں کھلیں؛ بالترتیب 0 اور 3.5 نظر آئیں، `Double -> Int64` error نہ آئے۔
- [ ] Urdu app label save کر کے Black box میں readable Unicode دیکھیں۔
- [ ] Pending PR کو INV سے attach کریں؛ کوئی second stock/payment event یا double return نہ ہو۔
- [ ] Link fail ہو تو error میں technical detail آئے؛ screenshot/report بھیجیں۔
- [ ] ایک medicine reorder level پر یا نیچے رکھ کر Dashboard کھولیں؛ alert board low-stock row fold کرے، cast error نہ آئے۔

## 13.6 T-05 — پہلے سے موجود chart کا optional retained check

یہ matrix میں DONE ہے؛ نیچے کا visual check regression روکنے کے لیے ہے، نیا feature یا reopened item نہیں۔

- [ ] Isolated copy میں Last 7 days کے مختلف دنوں کی Rs 160 / Rs 320 / Rs 480 sales کے bars تقریباً one-third / two-thirds / full ہوں۔
- [ ] Quiet days نظر آئیں اور exact money labels unchanged رہیں۔
- [ ] Manager کو profit نہ دکھے۔

## 13.7 T-10 / F-46 / D-93 / PR کی تفصیل کہاں ہے

- **Real Excel:** PT-07 Section 9-B۔
- **Printer اور USB scanner:** PT-07 Section 9-C۔
- **D-93 stock safeguards:** PT-06 Section 8-D۔
- **PR linking/cancellation:** PT-05 Section 7-B اور 7-C۔

---

# 14 — Feature board کے پانچ owner-action rows

**Board:** 52/57 done، 5 in progress، 0 not started۔ یہ پانچ rows صرف owner evidence کی وجہ سے partial ہیں؛ اس فائل میں ان کا راستہ یہ ہے:

| Feature | کیوں ابھی partial | کونسا evidence مکمل کرے گا |
|---|---|---|
| **F-04 Audit black box** | “Every module writes” کے لیے running physical evidence مکمل کرنا ہے۔ | PT-01 تا PT-10 کے Black-box checks + Z-05 reader walk-through۔ |
| **F-08 Products** | Catalog/pack/search code کے باوجود genuine/physical proof چاہیے۔ | PT-02 + PT-03 + PT-10، tags/units/thumbnails اور no cast error۔ |
| **F-09 Purchases** | ایک genuine intended-variant delivery کا detailed result چاہیے۔ | PT-03 PINV/conversion/batch/payable + PT-10 Purchase picker۔ |
| **F-10 Counter** | Genuine bill اور pricing/completion/stock safeguards کا physical result چاہیے۔ | PT-04 + PRICE/SALE + D-93 isolated checks + POS reproduction اگر مسئلہ رہے۔ |
| **F-46 Printer & scanner** | Slip half confirmed؛ scan half اور saved record unconfirmed۔ | PT-07 چھ checks، USB scan -> medicine، real bill اور `hardware_test` Black box۔ |

> کسی row کو صرف اس لیے DONE نہ لکھیں کہ current automated suite green ہے۔ Owner confirmation آنے پر running log اور board الگ update ہوں گے۔

---

# 15 — مالک کے نتائج بھیجنے کے سانچے

## 15.1 ہر test کے لیے مختصر جواب

```text
تاریخ:
Build/version:
PT یا item ID:
نتیجہ: PASS / PARTIAL / FAIL
اصل books یا isolated test copy:
کیا آزمایا:
کیا توقع تھی:
حقیقت میں کیا ہوا:
باقی مسئلہ یا مطلوبہ تبدیلی:
Screenshot / error / report file:
```

## 15.2 Automated test run کا الگ جواب

```text
تاریخ اور build:
Command: tools\RUN-TESTS.cmd
Project(s):
Total / passed / failed / skipped:
Duration:
Runner کی آخری ALL GREEN یا error line:
کیا Area11.Desktop/WPF الگ build اور launch ہوا؟ ہاں / نہیں
اگر ہاں تو version اور launch result:
test.log / build.log / error:
```

## 15.3 PT02-POS-01 reproduction — guessed fix سے پہلے لازم

```text
کونسے page سے شروع کیا: Products / Counter / دوسرا:
Medicine کا نام اور product ID:
اس کا stock / batch / unit:
Select یا Add to cart سے پہلے کے exact steps:
کونسا field blank یا — ہوا:
پہلے کیا ہوتا تھا:
اب کیا ہونا چاہیے:
کیا مسئلہ ہر بار آتا ہے؟ ہاں / نہیں / کبھی کبھار
Screenshot یا مختصر video:
SEND-REPORT.cmd report / error:
```

## 15.4 Phase sign-off — پرنٹ پر لکھنے کے لیے

| Phase | تاریخ | PASS / PARTIAL / FAIL | Owner initials / مختصر نوٹ |
|---|---|---|---|
| PT-01 | __________ | __________ | ______________________________ |
| PT-02 | 29 Oct 2026 | PASS — owner confirmed | الگ items اپنی حالت پر |
| PT-03 | __________ | PARTIAL — report pending | ______________________________ |
| PT-04 | __________ | __________ | ______________________________ |
| PT-05 | __________ | __________ | ______________________________ |
| PT-06 | __________ | __________ | ______________________________ |
| PT-07 | __________ | __________ | ______________________________ |
| PT-08 | __________ | __________ | ______________________________ |
| PT-09 | __________ | __________ | ______________________________ |
| PT-10 | __________ | __________ | Automated 900/900؛ physical الگ |

## حتمی بندش کا اصول

- کسی PT کا overall PASS الگ logged feedback item کو silently close نہیں کرے گا۔
- Automated evidence، physical evidence اور owner decision الگ لکھے جائیں گے۔
- PT02-POS-01 exact reproduction کے بغیر fixed نہیں لکھا جائے گا۔
- Negative-stock retirement، REAL/double quantities، D-93 اور PR cancellation کے no-repayment/no-drawer-inflow rules نہیں بدلیں گے۔
- Phase 4+ یا کوئی نئی development صرف owner کی الگ منظوری پر شروع ہوگی۔
- تمام مطلوبہ phases اور separately logged items کا نتیجہ آنے کے بعد بھی حتمی completion owner کی واضح تصدیق سے لکھی جائے گی۔

---

**اگلا مختصر قدم:** PT-03 کا detailed result بھیجیں؛ اگر اس کے بجائے current Cobalt UI آزما رہے ہیں تو PT-10 کا پہلا launch خانہ اور pack/search checks بھریں۔ نتیجہ اوپر کے مختصر سانچے میں بھیج دیں۔
