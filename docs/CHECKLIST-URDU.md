# Area11 — مکمل فیچر ٹک لسٹ (پرنٹ کرنے کے لیے)

**ورژن:** v0.35.19 (Spruce `#175E54`) · **تاریخ:** 2026-10-27

> یہ فائل **خودکار** بنتی ہے: `python3 tools/make_checklist.py`۔ ہاتھ سے نہ بدلیں۔
> اصلی فیصلہ `docs/PROGRESS.md` کے بورڈ میں ہوتا ہے، اور `tools/check_repo.py` اُس سے میل
> کھانے پر مجبور کرتا ہے — سو یہ فہرست کبھی جھوٹ نہیں بول سکتی۔

## ایک نظر میں

| حالت | گنتی | فیصد |
|---|---|---|
| ✅ مکمل | 52 | 91.2% |
| 🟡 جاری — کوڈ مکمل، دکان میں ایک عمل باقی | 5 | 8.8% |
| ❌ شروع نہیں | 0 | 0% |
| **کل فیچر** | **57** | **100%** |

## نشانیوں کا مطلب

- ✅ = **مکمل**: کوڈ لکھا، ٹیسٹ لکھے، اور (جہاں ممکن تھا) آپ کے کمپیوٹر پر چل چکا۔
- 🟡 = **جاری**: کام مکمل ہے مگر ایک عمل دکان میں ہونا باقی ہے (نیچے ہر ایک کے لیے ایک سطر ہے)۔
- ❌ = **شروع نہیں**: ہاتھ نہیں لگا۔
- ☐ = آپ کا اپنا نشان: جب آپ خود دیکھ لیں تو قلم سے ٹک لگا دیں۔

## فیز 1 — بنیاد، دوا اور بل

**اس حصے میں 18 فیچر — 14 ✅ کامل · 4 🟡 جاری · 0 ❌ شروع نہیں**

*یہاں دکان کا روزمرہ کام ہے: دوا کی فہرست، خریداری، اور کاؤنٹر پر بل۔*

| ☐ | نمبر | فیچر | حالت |
|---|---|---|---|
| ☐ | F-01 | Solution with three projects | ✅ مکمل |
| ☐ | F-02 | Exact database schema, migrations 001-026, 35 business tables + provisional report... | ✅ مکمل |
| ☐ | F-03 | Settings service, 122 keys, every owner-visible change audited, saved in one transa... | ✅ مکمل |
| ☐ | F-04 | Audit black box (old -> new values, never deleted) | 🟡 جاری |
| ☐ | F-05 | Sign-in: owner password, staff PIN, 5 tries -> 2 minute lock | ✅ مکمل |
| ☐ | F-06 | Per-person permissions stored in the database | ✅ مکمل |
| ☐ | F-07 | Permanent numbering `INV` / `PINV` / `RET` / `Q` / `SR` / `PR` / `ST` | ✅ مکمل |
| ☐ | F-08 | Products | 🟡 جاری |
| ☐ | F-09 | Purchases `PINV`: supplier, batch, expiry, sample lines, payable | 🟡 جاری |
| ☐ | F-10 | Counter `INV` | 🟡 جاری |
| ☐ | F-11 | Customers: quick create at the counter | ✅ مکمل |
| ☐ | F-12 | Dashboard: today's bills, sales, profit by the report's own rule, khata, payable, l... | ✅ مکمل |
| ☐ | F-13 | Receipt render + instant ESC/POS print, 58 / 80 mm, auto or manual | ✅ مکمل |
| ☐ | F-14 | Hold cart / bring back a held cart | ✅ مکمل |
| ☐ | F-15 | Split payment (cash + online / card / khata in one bill) | ✅ مکمل |
| ☐ | F-16 | First real Windows build and test run | ✅ مکمل |
| ☐ | F-17 | Settings screen: shop info, printing, discount rules, and per-person permission ticks | ✅ مکمل |
| ☐ | F-18 | Tests for purchases, partners, numbering | ✅ مکمل |

**دکان میں کیسے دیکھیں (5 منٹ):**

1. `START-AREA11.bat` دو بار کلک کریں اور سائن اِن کریں (صارف نام خالی، پاس ورڈ `area11`)۔
2. `Products` → **New medicine**: نام، پیک، ریٹ لکھ کر محفوظ کریں۔ فہرست میں دوا آ جائے۔
3. `Counter (POS)` → اُس دوا کو تلاش کر کے کارٹ میں ڈالیں → **Save** → بل نمبر `INV-` سے شروع ہو اور رسید پرنٹ ہو۔
4. `Purchases` → ایک خریداری `PINV-` لکھیں (سپلائر، بیچ، میعاد) → دونوں میں اسٹاک بڑھے۔

**اِس حصے میں باقی:**

- 🟡 **F-04 — Audit black box (old -> new values, never deleted)**: `Black box` کھولیں: آج جو بھی کام کیا (دوا شامل، بل، واپسی)، اُس کی ایک سطر نظر آئے — کس نے، کب، کیا بدلا۔
- 🟡 **F-08 — Products**: `Products` → **New medicine** → ایک دوا **بیچ کے ساتھ** (بیچ نمبر، میعاد، تعداد) محفوظ کریں۔
- 🟡 **F-09 — Purchases `PINV`: supplier, batch, expiry, sample lines, payable**: `Purchases` → ایک اصل ڈیلیوری لکھیں (سپلائر، بیچ، میعاد، تعداد) → اسٹاک بڑھے اور واجب الادا بنے۔
- 🟡 **F-10 — Counter `INV`**: `Counter (POS)` → ایک اصل بل بنائیں اور دکان کے پرنٹر پر نکالیں — رسید پوری اور صاف آئے۔

**نوٹ:**

- F-16: پہلی اصل ونڈوز build اور ٹیسٹ — 23 اکتوبر 2026 کو آپ کے کمپیوٹر پر **0 warning / 0 error** اور **730 میں سے 730**۔

## فیز 2 — واپسی، اسٹاک اور الرٹس

**اس حصے میں 10 فیچر — 10 ✅ کامل · 0 🟡 جاری · 0 ❌ شروع نہیں**

*یہاں وہ کام ہیں جو بل کے بعد آتے ہیں: واپسی، خراب مال، گنتی، اور کھاتے۔*

| ☐ | نمبر | فیچر | حالت |
|---|---|---|---|
| ☐ | F-20 | Bill lookup for a return: by printed number and by phone / customer / medicine / date | ✅ مکمل |
| ☐ | F-21 | Return `RET` against the original bill, strict limits, printed return receipt | ✅ مکمل |
| ☐ | F-23 | Quarantine `Q` with permanent number + disposition by owner or manager | ✅ مکمل |
| ☐ | F-22 | Provisional (no-bill) return `PR-nnnn` | ✅ مکمل |
| ☐ | F-25 | Stock write-off / breakage | ✅ مکمل |
| ☐ | F-26 | Stock-take `ST-nnnn`: frozen book, count sheet | ✅ مکمل |
| ☐ | F-27 | Customer khata: list + page, money received | ✅ مکمل |
| ☐ | F-28 | Supplier ledger + supplier return `SR-nnnn` | ✅ مکمل |
| ☐ | F-29 | Expiry levels read from settings | ✅ مکمل |
| ☐ | F-30 | Return receipt (permanent number, original bill, reason) | ✅ مکمل |

**دکان میں کیسے دیکھیں (5 منٹ):**

1. `Returns` → بل نمبر یا فون سے بل ڈھونڈیں → واپسی `RET-` بنے اور اسٹاک واپس آئے۔
2. `Write-off` → میعاد ختم یا ٹوٹی دوا لکھیں → تاریخ اور وجہ کے ساتھ ریکارڈ ہو۔
3. `Stock-take` → ایک گنتی `ST-` بنائیں → کتاب اور گنی ہوئی تعداد کا فرق نظر آئے۔
4. اوپر کی گھنٹی (🔔) دبائیں → الرٹس اپنے الفاظ میں نظر آئیں۔

## فیز 3 — کھاتے، رپورٹس، بیک اپ

**اس حصے میں 9 فیچر — 9 ✅ کامل · 0 🟡 جاری · 0 ❌ شروع نہیں**

*یہاں دکان کا حساب ہے: گلہ، اخراجات، رپورٹس، ادھار کی وصولی، اور بیک اپ۔*

| ☐ | نمبر | فیچر | حالت |
|---|---|---|---|
| ☐ | F-31 | Shift / galla: open, close, variance, and hand the drawer to the next person with a... | ✅ مکمل |
| ☐ | F-32 | Expenses and owner drawings | ✅ مکمل |
| ☐ | F-33 | Udhaar recovery: ageing | ✅ مکمل |
| ☐ | F-34 | Reports | ✅ مکمل |
| ☐ | F-35 | Audit viewer + CSV export (owner; manager hidden by default) | ✅ مکمل |
| ☐ | F-36 | Backup | ✅ مکمل |
| ☐ | F-37 | Import / export / bulk price change | ✅ مکمل |
| ☐ | F-38 | Reorder levels + WhatsApp order text | ✅ مکمل |
| ☐ | F-39 | Stock-minus registry: every shelf below zero, the movement that took it there, valu... | ✅ مکمل |

**دکان میں کیسے دیکھیں (5 منٹ):**

1. `Shift (galla)` → گلہ کھولیں، رقم گنیں، بند کریں → فرق (کم/زیادہ) لکھا آئے۔
2. `Expenses` → ایک خرچ (بجلی، چائے) لکھیں → رپورٹ میں نظر آئے۔
3. `Reports` → آج / 7 دن / 30 دن کے نمبر دیکھیں؛ `Tax report` مہینے کا حساب دکھائے۔
4. `Backup & restore` → **Back up now** → `C:\ProgramData\Area11\backups` میں ZIP بنے۔

## فیز 4 — باقی سب (دکان کے لیے اضافی سہولتیں)

**اس حصے میں 20 فیچر — 19 ✅ کامل · 1 🟡 جاری · 0 ❌ شروع نہیں**

*یہاں وہ سہولتیں ہیں جو ہر دکان استعمال نہیں کرتی — لیکن لکھی اور آزمائی ہوئی ہیں۔*

| ☐ | نمبر | فیچر | حالت |
|---|---|---|---|
| ☐ | F-40 | Loyalty: rupees spent -> points -> 1-5 stars -> a title. Points spent at the counter | ✅ مکمل |
| ☐ | F-41 | Photos: 2-3 per product, supplier and customer, camera capture | ✅ مکمل |
| ☐ | F-42 | Custom fields (text / number / amount / date / list / yes-no / photo) | ✅ مکمل |
| ☐ | F-43 | Several barcodes per product | ✅ مکمل |
| ☐ | F-44 | Medical rep / salesman info, doctor tracking, lifetime profit per customer | ✅ مکمل |
| ☐ | F-45 | Second counter over the LAN | ✅ مکمل |
| ☐ | F-46 | Printer & scanner test (Spec 33.3.4) | 🟡 جاری |
| ☐ | F-47 | Self-installing installer (D-19) | ✅ مکمل |
| ☐ | F-48 | Cloud sync with Supabase (Spec 25) | ✅ مکمل |
| ☐ | F-49 | Tax report month by month, the `PAY-` money-received slip, and the customer's month... | ✅ مکمل |
| ☐ | F-50 | Misspelt search (Spec 8.4, D-70) | ✅ مکمل |
| ☐ | F-51 | Supplier-return cutoff window | ✅ مکمل |
| ☐ | F-53 | Sample shop (D-69, D-74) - built, proved, then removed on the owner's word (D-77) | ✅ مکمل |
| ☐ | F-54 | Do the books add up? | ✅ مکمل |
| ☐ | F-55 | The drawer changes hands (Spec 14.4 / 18.2, migration 009) | ✅ مکمل |
| ☐ | F-56 | Who was short (Spec 7.2.7, the last line in the spec that still said *not built*) | ✅ مکمل |
| ☐ | F-57 | One time for the whole shop | ✅ مکمل |
| ☐ | F-58 | The session ends by itself | ✅ مکمل |
| ☐ | F-59 | No bill while the drawer is closed | ✅ مکمل |
| ☐ | F-52 | Bills page (Spec 12.3 `/sales`) | ✅ مکمل |

**دکان میں کیسے دیکھیں (5 منٹ):**

1. `Printer & scanner` → ٹیسٹ پرچی چھاپیں (58 یا 80 mm)؛ سکینر سے بارکوڈ آزمائیں۔
2. `Own fields` → ایک نیا خانہ بنائیں → وہی خانہ گاہک یا دوا کے فارم پر فوراً نظر آئے۔
3. `Second counter (LAN)` اور `Cloud sync` صرف اُس وقت آزمائیں جب آپ واقعی دوسرا کمپیوٹر یا کلاؤڈ استعمال کریں۔
4. `Check the books` → «کتابیں ملتی ہیں؟» کا جواب دیکھیں (یہ صرف پڑھتا ہے، کچھ بدلتا نہیں)۔

**اِس حصے میں باقی:**

- 🟡 **F-46 — Printer & scanner test (Spec 33.3.4)**: `Printer & scanner` → سکینر سے ایک بارکوڈ پڑھوائیں اور دیکھیں کہ دوا خود آ جائے (چھاپنے والا حصہ آپ نے 23 اکتوبر کو آزمایا تھا)۔

**نوٹ:**

- F-53: نمونہ دکان (sample shop) بنا اور آزمایا گیا، پھر آپ کے کہنے پر ہٹا دیا گیا (D-77) — اِس لیے پروگرام میں یہ صفحہ نہیں ہے۔

## جو فیچر نہیں، مگر ابھی آزمایا نہیں گیا

| ☐ | کیا | کیسے |
|---|---|---|
| ☐ | انسٹالر (F-47) | `MAKE-INSTALLER.cmd` چلائیں، پھر بنی ہوئی فائل دو بار کلک کریں — پروگرام `C:\Program Files\Area11` میں جائے اور آپ کا ڈیٹا `C:\ProgramData\Area11` میں جیسے تھا ویسے رہے۔ **یہ آج تک کبھی نہیں چلا۔** |
| ☐ | صفحات کا ہاتھ سے چلنا | تازہ ترین build کھول کر کم از کم پانچ صفحے دیکھیں (Products، Counter، Bills، Customers، Reports) اور `%TEMP%\Area11\last-run.txt` میں اُن کے نام دیکھیں۔ ساتھ ہی کاؤنٹر پر **گلا بند** کا کارڈ اور **Settings → Shop** میں گھڑی کی سطر دیکھیں۔ |
| ☐ | دوسرا کاؤنٹر (F-45) | کوئی شیئر فولڈر بنا کر `SHARE-AREA11.cmd` چلائیں — یہ صرف اُس وقت ضروری ہے جب آپ واقعی دوسرا کمپیوٹر لگائیں۔ |

## یاد رکھنے کی ایک بات

**جتنے فیچر لکھنے تھے، سب لکھے جا چکے ہیں** — فہرست میں کوئی ❌ نہیں۔ باقی صرف **دکان میں ایک ایک عمل** ہے
اور اُس کا پیمانہ اوپر لکھا ہے۔ جب آپ کوئی عمل کر لیں اور کہیں کہ «ہو گیا»، تو اُس فیچر کا نشان اگلی دوڑ میں
✅ ہو جائے گا — اِس فائل میں خود بخود۔

*بنایا گیا: 2026-10-27 · `tools/make_checklist.py`*
