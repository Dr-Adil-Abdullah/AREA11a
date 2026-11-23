# Area11 — مکمل فیچر ٹک لسٹ (پرنٹ کرنے کے لیے)

**ورژن:** v0.128.0 (Catkin `#6B6254`) · **تاریخ:** 2026-11-10

> یہ فائل **خودکار** بنتی ہے: `python3 tools/make_checklist.py`۔ ہاتھ سے نہ بدلیں۔
> اصلی فیصلہ `docs/PROGRESS.md` کے بورڈ میں ہوتا ہے، اور `tools/check_repo.py` اُس سے میل
> کھانے پر مجبور کرتا ہے۔ بورڈ کے پرانے source ticks مکمل عملی تصدیق نہیں؛نئی رپورٹ میں وہ جاری ہیں۔

## ایک نظر میں

| حالت | گنتی | فیصد |
|---|---|---|
| ✅ مکمل | 0 | 0% |
| ⏳ جاری — source موجود، تازہ جانچ/باقی کام درکار | 57 | 100.0% |
| ❌ شروع نہیں | 0 | 0% |
| **کل فیچر** | **57** | **100%** |

## نشانیوں کا مطلب

- ✅ = **مکمل**: صرف جب دائرے کا100% کام اورلازمی تازہ عملی ثبوت مکمل ہوں۔
- ⏳ = **جاری**: کام شروع ہے،مگرsource review یاC#/Windows/عملی ثبوت میں کچھ باقی ہے۔
- ❌ = **شروع نہیں**: ہاتھ نہیں لگا۔
- ☐ = آپ کا اپنا نشان: جب آپ خود دیکھ لیں تو قلم سے ٹک لگا دیں۔

## فیز 1 — بنیاد، دوا اور بل

**اس حصے میں 18 فیچر — 0 ✅ کامل · 18 ⏳ جاری · 0 ❌ شروع نہیں**

*یہاں دکان کا روزمرہ کام ہے: دوا کی فہرست، خریداری، اور کاؤنٹر پر بل۔*

| ☐ | نمبر | فیچر | حالت |
|---|---|---|---|
| ☐ | F-01 | Solution with three projects | ⏳ جاری |
| ☐ | F-02 | Exact database schema, migrations 001-030, 42 application tables + ledger and two v... | ⏳ جاری |
| ☐ | F-03 | Settings service, 130 keys, every owner-visible change audited, saved in one transa... | ⏳ جاری |
| ☐ | F-04 | Audit black box (old -> new values, never deleted) | ⏳ جاری |
| ☐ | F-05 | Sign-in: owner password, staff PIN, 5 tries -> 2 minute lock | ⏳ جاری |
| ☐ | F-06 | Per-person permissions stored in the database | ⏳ جاری |
| ☐ | F-07 | Permanent numbering `INV` / `PINV` / `RET` / `Q` / `SR` / `PR` / `ST` | ⏳ جاری |
| ☐ | F-08 | Products | ⏳ جاری |
| ☐ | F-09 | Purchases `PINV` | ⏳ جاری |
| ☐ | F-10 | Counter `INV` | ⏳ جاری |
| ☐ | F-11 | Customers: quick create at the counter | ⏳ جاری |
| ☐ | F-12 | Dashboard: today's bills, sales, profit by the report's own rule, khata, payable, l... | ⏳ جاری |
| ☐ | F-13 | Receipt render + instant ESC/POS print, 58 / 80 mm, auto or manual | ⏳ جاری |
| ☐ | F-14 | Hold cart / bring back a held cart | ⏳ جاری |
| ☐ | F-15 | Split payment (cash + online / card / khata in one bill) | ⏳ جاری |
| ☐ | F-16 | First real Windows build and test run | ⏳ جاری |
| ☐ | F-17 | Settings screen: shop info, printing, discount rules, and per-person permission ticks | ⏳ جاری |
| ☐ | F-18 | Tests for purchases, partners, numbering | ⏳ جاری |

**دکان میں کیسے دیکھیں (5 منٹ):**

1. ⏳ `START-AREA11.bat` دو بار کلک کریں اور سائن اِن کریں (صارف نام خالی، پاس ورڈ `area11`)۔
2. ⏳ `Products` → **New medicine**: نام، پیک، ریٹ لکھ کر محفوظ کریں۔ فہرست میں دوا آ جائے۔
3. ⏳ `Counter (POS)` → اُس دوا کو تلاش کر کے کارٹ میں ڈالیں → **Save** → بل نمبر `INV-` سے شروع ہو اور رسید پرنٹ ہو۔
4. ⏳ `Purchases` → ایک خریداری `PINV-` لکھیں (سپلائر، بیچ، میعاد) → دونوں میں اسٹاک بڑھے۔

**اِس حصے میں باقی:**

- ⏳ **F-01 — Solution with three projects**: **compiled on Windows**, 23 Oct 2026, v0.30.9: the launcher printed **`BUILD OK - the program was compiled`** for the first time - Core and Tests had compiled before, the WPF project had not. (The window then stopped on a binding fault of its own - see v0.31.1 and F-16.)
- ⏳ **F-02 — Exact database schema, migrations 001-030, 42 application tables + ledger and two v...**: an old `area11.db` opens unchanged; 024–029 retain their earlier purposes, and 030 adds native-field/draft/catalog/supplier/drawer contracts without guessing managed links or changing legacy text, quantities, money or history
- ⏳ **F-03 — Settings service, 130 keys, every owner-visible change audited, saved in one transa...**: bad values are refused before they reach the database; later-phase setting contracts are not presented as completed UI behavior
- ⏳ **F-04 — Audit black box (old -> new values, never deleted)**: `Black box` کھولیں: آج جو بھی کام کیا (دوا شامل، بل، واپسی)، اُس کی ایک سطر نظر آئے — کس نے، کب، کیا بدلا۔
- ⏳ **F-05 — Sign-in: owner password, staff PIN, 5 tries -> 2 minute lock**: owner signs in on Windows - **done 23 Oct 2026 on v0.33.0**: `app-2026-10-23.log` shows `13:53:31 Signed in: Owner`
- ⏳ **F-06 — Per-person permissions stored in the database**: the owner ticks/unticks in Settings; every change is audited
- ⏳ **F-07 — Permanent numbering `INV` / `PINV` / `RET` / `Q` / `SR` / `PR` / `ST`**: no duplicate numbers across independent settings instances, inside one transaction, or after rollback
- ⏳ **F-08 — Products**: `Products` → **New medicine** → ایک دوا **بیچ کے ساتھ** (بیچ نمبر، میعاد، تعداد) محفوظ کریں۔
- ⏳ **F-09 — Purchases `PINV`**: `Purchases` → ایک اصل ڈیلیوری لکھیں (سپلائر، بیچ، میعاد، تعداد) → اسٹاک بڑھے اور واجب الادا بنے۔
- ⏳ **F-10 — Counter `INV`**: `Counter (POS)` → ایک اصل بل بنائیں اور دکان کے پرنٹر پر نکالیں — رسید پوری اور صاف آئے۔
- ⏳ **F-11 — Customers: quick create at the counter**: a new customer is added at the counter **without losing the cart**, a credit bill lands on their account, and the owner corrects a name, phone, category and limit without touching the udhaar
- ⏳ **F-12 — Dashboard: today's bills, sales, profit by the report's own rule, khata, payable, l...**: every card equals a hand count, drill-downs open the correctly filtered page, the low-stock card equals the reorder pad's red rows, and today's card equals today's report
- ⏳ **F-13 — Receipt render + instant ESC/POS print, 58 / 80 mm, auto or manual**: his own test on the shop printer: *"I tested it on the printer and it is working perfectly"* (23 Oct 2026, v0.30.2). The slip prints; 58 and 80 mm both chosen from Settings
- ⏳ **F-14 — Hold cart / bring back a held cart**: a parked cart survives a restart and comes back line for line
- ⏳ **F-15 — Split payment (cash + online / card / khata in one bill)**: the credit part reaches the khata and never the drawer
- ⏳ **F-16 — First real Windows build and test run**: **both halves proved on the owner's PC:** the whole solution **compiled** (v0.30.9, `BUILD OK`) and `dotnet test` **ran and passed 686 of 686 in 53 s** (v0.30.9), after the earlier green 686/686 of v0.30.8. The first attempts are part of the record: 20 Oct v0.1.1 `Build succeeded`; 21 Oct Phase-2 builds with CS0509 `ReturnService.cs(504)` (v0.3.5), CS0136 `SaleService.cs(609)` (v0.3.6), CS0037/CS1061 `ReturnsViewModel.cs(152-167)` (v0.3.7) - all three now prevented by checker rules (sealed bases, shadowing, value-type results). **What the build could not prove** and the runtime had to: the window stopped on 22 Oct (a `Run.Text`/grid-column TwoWay binding to a read-only label, fixed in v0.31.1) and the launcher showed a day-old report for it (also fixed in v0.31.1). Building and testing are ✅; the window opening is the next run.
- ⏳ **F-17 — Settings screen: shop info, printing, discount rules, and per-person permission ticks**: owner edits everything without a developer
- ⏳ **F-18 — Tests for purchases, partners, numbering**: a box becomes twenty tablets and a bonus line never reaches the bill; a supplier is owed exactly what was not paid; a payment may not overshoot and a return may not burn its number; every paper keeps its own counter

**نوٹ:**

- ⏳ F-16: پہلی اصل ونڈوز build اور ٹیسٹ — 23 اکتوبر 2026 کو آپ کے کمپیوٹر پر **0 warning / 0 error** اور **730 میں سے 730**۔

## فیز 2 — واپسی، اسٹاک اور الرٹس

**اس حصے میں 10 فیچر — 0 ✅ کامل · 10 ⏳ جاری · 0 ❌ شروع نہیں**

*یہاں وہ کام ہیں جو بل کے بعد آتے ہیں: واپسی، خراب مال، گنتی، اور کھاتے۔*

| ☐ | نمبر | فیچر | حالت |
|---|---|---|---|
| ☐ | F-20 | Bill lookup for a return: by printed number and by phone / customer / medicine / date | ⏳ جاری |
| ☐ | F-21 | Return `RET` against the original bill, strict limits, printed return receipt | ⏳ جاری |
| ☐ | F-23 | Quarantine `Q` with permanent number + disposition by owner or manager | ⏳ جاری |
| ☐ | F-22 | Provisional (no-bill) return `PR-nnnn` | ⏳ جاری |
| ☐ | F-25 | Stock write-off / breakage | ⏳ جاری |
| ☐ | F-26 | Stock-take `ST-nnnn`: frozen book, count sheet | ⏳ جاری |
| ☐ | F-27 | Customer khata: list + page, money received | ⏳ جاری |
| ☐ | F-28 | Supplier ledger + supplier return `SR-nnnn` | ⏳ جاری |
| ☐ | F-29 | Expiry levels read from settings | ⏳ جاری |
| ☐ | F-30 | Return receipt (permanent number, original bill, reason) | ⏳ جاری |

**دکان میں کیسے دیکھیں (5 منٹ):**

1. ⏳ `Returns` → بل نمبر یا فون سے بل ڈھونڈیں → واپسی `RET-` بنے اور اسٹاک واپس آئے۔
2. ⏳ `Write-off` → میعاد ختم یا ٹوٹی دوا لکھیں → تاریخ اور وجہ کے ساتھ ریکارڈ ہو۔
3. ⏳ `Stock-take` → ایک گنتی `ST-` بنائیں → کتاب اور گنی ہوئی تعداد کا فرق نظر آئے۔
4. ⏳ اوپر کی گھنٹی (🔔) دبائیں → الرٹس اپنے الفاظ میں نظر آئیں۔

**اِس حصے میں باقی:**

- ⏳ **F-20 — Bill lookup for a return: by printed number and by phone / customer / medicine / date**: the customer has no paper, and the right bill is found by any one thing they remember - as a list, never a guess
- ⏳ **F-21 — Return `RET` against the original bill, strict limits, printed return receipt**: cumulative quantity/value caps hold after later recoveries; only this bill's remaining khata is reduced and only collected value becomes cash (U-28, U-35) · v0.35.7
- ⏳ **F-23 — Quarantine `Q` with permanent number + disposition by owner or manager**: goods never restock without a decision; history kept forever
- ⏳ **F-22 — Provisional (no-bill) return `PR-nnnn`**: pending means unchecked; linking allocates real quantities/refund without another stock/payment event, repeated/split lines and cumulative RET caps agree, old links need same-bill repair, and refund differences remain visible (U-33) · v0.35.8; cancellation removes only the same batch, refuses insufficient stock atomically, and never creates a payment (D-93) · v0.35.9
- ⏳ **F-25 — Stock write-off / breakage**: removing more than the book says is refused atomically with available/requested detail; the legacy switch cannot bypass it and the typed form remains
- ⏳ **F-26 — Stock-take `ST-nnnn`: frozen book, count sheet**: the count is evidence; a finite non-negative physical count corrects a historical minus (including to zero) with an audited adjustment; uncounted rows are never zeroed (19.2, D-93)
- ⏳ **F-27 — Customer khata: list + page, money received**: balance always equals the sum of movements ("books match"), and money received is refused above the udhaar
- ⏳ **F-28 — Supplier ledger + supplier return `SR-nnnn`**: payable always equals opening + purchases - payments - credit notes ("books match"); repeated return lines cannot oversell a batch or burn an SR number
- ⏳ **F-29 — Expiry levels read from settings**: change the threshold, the colour changes
- ⏳ **F-30 — Return receipt (permanent number, original bill, reason)**: the paper says stock or quarantine for every line

## فیز 3 — کھاتے، رپورٹس، بیک اپ

**اس حصے میں 9 فیچر — 0 ✅ کامل · 9 ⏳ جاری · 0 ❌ شروع نہیں**

*یہاں دکان کا حساب ہے: گلہ، اخراجات، رپورٹس، ادھار کی وصولی، اور بیک اپ۔*

| ☐ | نمبر | فیچر | حالت |
|---|---|---|---|
| ☐ | F-31 | Shift / galla: open, close, variance, and hand the drawer to the next person with a... | ⏳ جاری |
| ☐ | F-32 | Expenses and owner drawings | ⏳ جاری |
| ☐ | F-33 | Udhaar recovery: ageing | ⏳ جاری |
| ☐ | F-34 | Reports | ⏳ جاری |
| ☐ | F-35 | Audit viewer + CSV export (owner; manager hidden by default) | ⏳ جاری |
| ☐ | F-36 | Backup | ⏳ جاری |
| ☐ | F-37 | Import / export / bulk price change | ⏳ جاری |
| ☐ | F-38 | Reorder levels + WhatsApp order text | ⏳ جاری |
| ☐ | F-39 | Stock-minus registry | ⏳ جاری |

**دکان میں کیسے دیکھیں (5 منٹ):**

1. ⏳ `Shift (galla)` → گلہ کھولیں، رقم گنیں، بند کریں → فرق (کم/زیادہ) لکھا آئے۔
2. ⏳ `Expenses` → ایک خرچ (بجلی، چائے) لکھیں → رپورٹ میں نظر آئے۔
3. ⏳ `Reports` → آج / 7 دن / 30 دن کے نمبر دیکھیں؛ `Tax report` مہینے کا حساب دکھائے۔
4. ⏳ `Backup & restore` → **Back up now** → `C:\ProgramData\Area11\backups` میں encrypted `.a11bak` بنے؛ پہلے Owner کا backup password setup۔

**اِس حصے میں باقی:**

- ⏳ **F-31 — Shift / galla: open, close, variance, and hand the drawer to the next person with a...**: expected cash = float + sales - payouts; the page says exact / over / short · v0.4.0
- ⏳ **F-32 — Expenses and owner drawings**: they reduce the drawer, not the profit · v0.4.4
- ⏳ **F-33 — Udhaar recovery: ageing**: the four columns plus remaining carried-in khata add up; a payment knocks the oldest existing bill off first; a later bill cannot rewrite a reprint · v0.35.7
- ⏳ **F-34 — Reports**: profit uses cost-at-sale, cost-at-return and the return rule of Spec 15.6 · v0.35.6
- ⏳ **F-35 — Audit viewer + CSV export (owner; manager hidden by default)**: search + module/action filters + date window, no edit or delete, CSV is exactly what is on screen · v0.5.0
- ⏳ **F-36 — Backup**: PT-35 plus current1716C#cases,DPAPI/ACL,new-PC original password,rotation,isolated full restore/fault matrix;no runtime certification
- ⏳ **F-37 — Import / export / bulk price change**: a 1,000-line file imports with a preview, and importing it twice adds nothing
- ⏳ **F-38 — Reorder levels + WhatsApp order text**: the pad names the last supplier, the message is one supplier, and a `03xx` mobile becomes a working `wa.me` link
- ⏳ **F-39 — Stock-minus registry**: baseline episode/review feature retained;new visibility/current-reader/timer extension requires PT-36;not runtime/security approval

## فیز 4 — باقی سب (دکان کے لیے اضافی سہولتیں)

**اس حصے میں 20 فیچر — 0 ✅ کامل · 20 ⏳ جاری · 0 ❌ شروع نہیں**

*یہاں وہ سہولتیں ہیں جو ہر دکان استعمال نہیں کرتی — لیکن لکھی اور آزمائی ہوئی ہیں۔*

| ☐ | نمبر | فیچر | حالت |
|---|---|---|---|
| ☐ | F-40 | Loyalty: rupees spent -> points -> 1-5 stars -> a title. Points spent at the counter | ⏳ جاری |
| ☐ | F-41 | Photos: 2-3 per product, supplier and customer, camera capture | ⏳ جاری |
| ☐ | F-42 | Custom fields (text / number / amount / date / list / yes-no / photo) | ⏳ جاری |
| ☐ | F-43 | Several barcodes per product | ⏳ جاری |
| ☐ | F-44 | Medical rep / salesman info, doctor tracking, lifetime profit per customer | ⏳ جاری |
| ☐ | F-45 | Second counter over the LAN | ⏳ جاری |
| ☐ | F-46 | Printer & scanner test (Spec 33.3.4) | ⏳ جاری |
| ☐ | F-47 | Self-installing installer (D-19) | ⏳ جاری |
| ☐ | F-48 | Cloud sync with Supabase (Spec 25) | ⏳ جاری |
| ☐ | F-49 | Tax report month by month, the `PAY-` money-received slip, and the customer's month... | ⏳ جاری |
| ☐ | F-50 | Misspelt search (Spec 8.4, D-70) | ⏳ جاری |
| ☐ | F-51 | Supplier-return cutoff window | ⏳ جاری |
| ☐ | F-53 | Sample shop (D-69, D-74) - built, proved, then removed on the owner's word (D-77) | ⏳ جاری |
| ☐ | F-54 | Do the books add up? | ⏳ جاری |
| ☐ | F-55 | The drawer changes hands (Spec 14.4 / 18.2, migration 009) | ⏳ جاری |
| ☐ | F-56 | Who was short (Spec 7.2.7, the last line in the spec that still said *not built*) | ⏳ جاری |
| ☐ | F-57 | One time for the whole shop | ⏳ جاری |
| ☐ | F-58 | The session ends by itself | ⏳ جاری |
| ☐ | F-59 | No bill while the drawer is closed | ⏳ جاری |
| ☐ | F-52 | Bills page (Spec 12.3 `/sales`) | ⏳ جاری |

**دکان میں کیسے دیکھیں (5 منٹ):**

1. ⏳ `Printer & scanner` → ٹیسٹ پرچی چھاپیں (58 یا 80 mm)؛ سکینر سے بارکوڈ آزمائیں۔
2. ⏳ `Own fields` → ایک نیا خانہ بنائیں → وہی خانہ گاہک یا دوا کے فارم پر فوراً نظر آئے۔
3. ⏳ `Second counter (LAN)` اور `Cloud sync` صرف اُس وقت آزمائیں جب آپ واقعی دوسرا کمپیوٹر یا کلاؤڈ استعمال کریں۔
4. ⏳ `Check the books` → «کتابیں ملتی ہیں؟» کا جواب دیکھیں (یہ صرف پڑھتا ہے، کچھ بدلتا نہیں)۔

**اِس حصے میں باقی:**

- ⏳ **F-40 — Loyalty: rupees spent -> points -> 1-5 stars -> a title. Points spent at the counter**: off by default; when on, a bill earns points, a customer spends them without the lines losing a rupee, and 5 stars make him VIP
- ⏳ **F-41 — Photos: 2-3 per product, supplier and customer, camera capture**: a picture is copied into `data/photos`, the database keeps only its name (U-19), a wrong one is **put away and never deleted**, thumbnails fall back safely, and the camera takes it
- ⏳ **F-42 — Custom fields (text / number / amount / date / list / yes-no / photo)**: a field the owner adds asks its question on that form, a required answer is checked before the write, and nothing is ever deleted
- ⏳ **F-43 — Several barcodes per product**: a medicine carries one code per pack, a scan on Enter adds one of the pack the code belongs to, the old single column still scans, and a code is never deleted
- ⏳ **F-44 — Medical rep / salesman info, doctor tracking, lifetime profit per customer**: a company keeps its rep and visit day, a bill can name a doctor, and each doctor and customer shows what he has earned the shop - never to a manager
- ⏳ **F-45 — Second counter over the LAN**: two PCs open one database; the second PC can never invent an empty shop, and the black box shows it joined
- ⏳ **F-46 — Printer & scanner test (Spec 33.3.4)**: `Printer & scanner` → سکینر سے ایک بارکوڈ پڑھوائیں اور دیکھیں کہ دوا خود آ جائے (چھاپنے والا حصہ آپ نے 23 اکتوبر کو آزمایا تھا)۔
- ⏳ **F-47 — Self-installing installer (D-19)**: run it once: it installs itself, the icon is on the desktop, and an uninstall leaves every bill in place
- ⏳ **F-48 — Cloud sync with Supabase (Spec 25)**: a real push lands in Supabase; preview writes nothing; pull refuses without the tick, keeps today's bills and preserves managed/old-sale/old-purchase/PR dependencies
- ⏳ **F-49 — Tax report month by month, the `PAY-` money-received slip, and the customer's month...**: the month's tax is read from the bills (never recalculated), a reprint keeps its own day's balance, and a month's statement starts where the last one ended
- ⏳ **F-50 — Misspelt search (Spec 8.4, D-70)**: typing `panadl` finds Panadol and says so; `zink` finds nothing rather than guessing
- ⏳ **F-51 — Supplier-return cutoff window**: the board says the last day the supplier accepts the goods
- ⏳ **F-53 — Sample shop (D-69, D-74) - built, proved, then removed on the owner's word (D-77)**: **retired 23 Oct 2026** (v0.30.2): the owner asked for the demo to be deleted once the product was ready - the script, the page, the service, the models and 37 tests are gone, and no flag or button can create invented rows any more
- ⏳ **F-54 — Do the books add up?**: a clean shop has zero faults, a hand-written fault is caught, and a no-bill refund payment is not called an orphan
- ⏳ **F-55 — The drawer changes hands (Spec 14.4 / 18.2, migration 009)**: nothing on the shop PC to prove - the proof is the tests plus the SQL run on a real SQLite file built from all 23 migrations: exact handover, short-by-50, a bill pinned to the boundary second (300 once, never 600) and a two-handover chain (300 / 500 / 600)
- ⏳ **F-56 — Who was short (Spec 7.2.7, the last line in the spec that still said *not built*)**: nothing on the shop PC to prove - proved by the tests, and the SQL was run on a SQLite file built from all 23 migrations (7 drawers, 5 inside the window: 1500 short / 200 over, not 1300 / 0)
- ⏳ **F-57 — One time for the whole shop**: the NTP client itself cannot be proven here (no network in this build area) - his PC with the cable in, and again with it out, is the proof
- ⏳ **F-58 — The session ends by itself**: the clock is proven by arithmetic, not by waiting twelve hours; his PC will show the warning line and the sign-out
- ⏳ **F-59 — No bill while the drawer is closed**: his PC: one bill with the drawer closed must show the card, then open the galla and bill
- ⏳ **F-52 — Bills page (Spec 12.3 `/sales`)**: a bill from last month is found by its number and printed again with the same numbers

**نوٹ:**

- ⏳ F-53: نمونہ دکان (sample shop) بنا اور آزمایا گیا، پھر آپ کے کہنے پر ہٹا دیا گیا (D-77) — اِس لیے پروگرام میں یہ صفحہ نہیں ہے۔

## جو فیچر نہیں، مگر ابھی آزمایا نہیں گیا

| ☐ | کیا | کیسے |
|---|---|---|
| ☐ | ❌ انسٹالر binary (F-47) | ابھی نہیں بنی۔ پہلےتازہbuild/tests/packageاورواقعیالگtestDB/photos/configکیisolation/smokeجانچ؛اصلshopپراندھادھندinstallerمشقنہیں۔ |
| ☐ | ⏳ صفحات کا ہاتھ سے چلنا | تازہ Windows build،متعلقہpages/dialogs/DPI/focus/receiptsکےlogsاورحقیقیscreenshotsدرکار؛صرفstaticchecksکافی نہیں۔ |
| ☐ | ⏳ دوسرا کاؤنٹر (F-45) | بنیادیsourceموجود،عملیsecond-counterdeploymentمالککےانتخابسےمؤخر؛یہاسحصےکیقبولیتکیشرطنہیں۔ |

## یاد رکھنے کی ایک بات

⏳ یہ source board سے بنا کام کا نقشہ ہے،مکمل safety/runtime منظوری نہیں۔ ہرstarted فیچر کے ضروری تازہ ثبوت باقی ہیں۔
✅ صرف مکمل دائرہ اورلازمی ثبوت کے بعد؛ایک زبانی tick یاپرانی کامیابی سےخودکار مکمل نہیں۔ ❌ صرفبالکل شروع نہ ہونے والا کام۔

*بنایا گیا: 2026-11-10 · `tools/make_checklist.py`*
