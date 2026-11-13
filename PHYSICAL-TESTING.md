# Area11 — Real-life Physical Testing Log

## PT-19 — POS trial B: reserved search pane vs A overlay (NOT physically passed)

Owner requested “tryal b”, not a final preference. B = v0.75.0 Kestrel (purple), A = v0.74.0 Marlin (blue, cfbc68d). No new screenshot/runtime/test log received for this trial. Only results layout and version/docs/checker pins changed; the A search/scan ViewModel and all financial services/models/migrations remain identical.

On test data or an UNPOSTED real cart cleared afterward (no fake sale):
1. Blank search: narrow pane remains at the side, with a prompt, no catalog/selected hidden medicine. Cart uses the wider adjoining area, not an obscured background. Typing results does not resize/cover Cart. After Add/scan, matches clear but the reserved pane does not disappear.
2. Search 2–3 matching names/strengths/packs. Cards show name, strength, pack, stock, retail, photo and salt/rack. Long names/pack text and medium/large photos wrap; scroll matches vertically, no wide nine-column table. Pick correct variant and Add. Test no matches, typo suggestions (explicit human Add), exact pack barcode, unknown barcode and Escape/Hide then edit query to reopen.
3. Add 5–10 cart lines, resize at 1366×768/1024×768 and 125%/150% scaling. Check fixed compact actions and total/cash/change/Complete sale/F4; only bounded lists scroll. Header/search/footer must not be pushed out by more products.
4. More/split, new customer and Parked carts open temporary side details over the cart region, not another permanent column; Close returns full Cart width. Split opens inputs; draft indicator remains in footer; required extras still apply. F4 edits, validation, exact barcode pack, reprint, auto-print on/off and modal snapshot retained.
5. Compare an A screenshot (same size/cart) to B; do not run two live instances or assume another program folder isolates shop data. Ask owner A / B / specific adjustment before calling either final. Source snapshots identify builds; no default-branch switch is needed.
6. Full Windows `ALL GREEN - 1240 of 1240` and Desktop build remain pending. Discount is STILL the former full-selling-total calculation: approved profit-default/retail warning is not implemented by either UI trial.

Result: pending owner comparison and Windows proof. No visual/production acceptance claimed.


## PT-18 — POS space comparison: trial A (Marlin), trial B queued, final choice pending

Owner's new spoken feedback: persistent products list and stacked optional actions take too much Cart space. Wants search-only products and compact actions. Chose to try transient results first, standing small search panel second on next, then decide. No new screenshot or runtime PASS supplied for this trial.

On v0.74.0, use test data or an UNPOSTED cart that is cleared afterward (do not create fake sales in the real shop):
1. Blank search: catalog absent, Cart has the main space, no hidden selected medicine. A shop with products must not show an empty-shop hint just because search is blank.
2. Type a genuine name/strength/pack: bounded results overlay, matching details/photos/stock/rate, correct variant selected. Add closes results, clears selected product/query and restores search focus. Add with blank search cannot silently repeat a hidden product. More matches scroll inside results. No match explains itself; fuzzy guesses are labelled and require Add, never name-Enter auto-sale.
3. Exact barcode of unit/strip/box adds one correct pack and clears search. Unknown barcode and closed-galla add retain refusal (not a false “added”). Escape/Close results hides the overlay; changing the query reopens it.
4. Add 5–10 lines. At short window/1024×768/1366×768 and 125%/150% DPI, check Cart, fixed total/cash/change/Complete sale/F4 and compact actions. Only the Cart/results/details themselves should scroll, not a long whole page hiding routine actions.
5. Park / Save draft / Open drafts / Parked carts / Reprint all use existing paths; More / split opens doctor/loyalty/extra fields without changing their values; choosing split opens its inputs; New customer opens its form. Parked list and details are not stacked permanently. Reprint is a copy, not another sale.
6. Regression: F4 and success modal, AutoPrint on/off, validation/stock guard and permissions unchanged. Discount still the former total-based calculation in THIS UI trial; profit-default/total-warning work is queued, not silently active.
7. Full Windows `ALL GREEN - 1240 of 1240` and Desktop build pending. Static structural gate is not visual proof. Save a screenshot/feedback of trial A before next B; final preference remains owner's decision. Do not run two live instances to compare (single-instance guard/data location are shared).

Owner result: pending. Next implementation: trial B only when owner says next, final selection later.


## PT-17 — Heather short-screen checkout/F4 failure; Laurel candidate repair (NOT physically passed)

Owner supplied sugession1.png (1366×728 image, v0.72.0 title) and sugession2.png (product editor), and reported the Complete sale button outside the screen plus F4 doing nothing. Screenshot shows Rs 300 subtotal, 10% / Rs 30 discount, Rs 270 total, Rs 500 tendered and Rs 230 change; this proves the on-screen quote, NOT a saved sale or printing. Source confirms Auto-height payment overflow and no F4 handler/binding anywhere in Desktop. Owner chose an owned modal completion popup. Packing explanation requested, not a change to real stock/product definitions.

Candidate v0.73.0: fixed non-scrolling checkout footer, bounded cart/options; actual F4 preview handler with edit commit/repeat suppression; post-commit modal and committed cash snapshot. Static checking is not visual proof.

On disposable test data, after rebuild/reinstall:
1. At 1366×768 and 1024×768, plus 125%/150% scaling or a short window: cart rows, checkout button/total/cash/change/status stay reachable without scrolling to the bottom. Optional customer/doctor/points/split/custom fields/drafts/parked carts can scroll independently. Try many cart rows and many custom answers; no controls lost.
2. Click Complete sale AND separately press F4 from search, cash textbox, customer/payment ComboBox and a rate edit. Grid edit must be committed; Alt+F4 remains protected-close policy, not a sale. Hold F4: no duplicate sale. Failed stock/cost/short-cash/required-answer validation: visible error, no success modal or automatic print.
3. Rs 300 retail, cost at/below Rs 270, 10% discount, Rs 500 cash: exactly one saved INV, modal tick + bill Rs 270 + cash Rs 500 + LARGE give-back Rs 230. Done/Enter/× cannot save or print again. Cart is empty afterward; change snapshot is not overwritten by the cleared cart.
4. Test zero change, blank tender (explicit assumed-exact label), card, online, credit and mixed payments. Noncash received is Rs 0, credit due is not mistaken for cash; split cash/change matches the cash portion.
5. AutoPrint OFF: no printer call. ON: one automatic call after save. Printer unavailable: bill remains saved and confirmation says reprint, never enter the sale again. Popup OFF: no modal but fixed status/last invoice remain. Popup ON: owned, centered and Done reachable even with a short work area.
6. Check app title/icon and FocusGuard do not treat the owned modal as leaving the app. Confirm settings-popup wording and updated guide. Run full Windows suite: `ALL GREEN - 1240 of 1240` (not run in agent sandbox).

Owner result: pending. Never upgrade this record to PASS from static checks alone.


## PT-16 — Login/Counter XAML parse: owner screenshot, candidate repair pending Windows verification

**مالک کی تازہ اطلاع:** “سکرین جو ہے لاگ ان پہ اوپن ہو گئی ہے اور ایرر ختم ہو گیا ہے”۔ اسے login-related error کے خاتمے کی owner-reported مثبت اطلاع کے طور پر درج کیا گیا ہے۔ یہ نہیں بتایا گیا کہ Counter اور Products دونوں، medium/large thumbnails یا پورا app مکمل چیک ہوئے؛ ان کی الگ تصدیق باقی ہے۔ 973/973 Core/Tests کی اطلاع برقرار ہے؛ اس output سے exact commit خود ثابت نہیں ہوتا۔

مالک نے 973/973، 0 failed، 0 skipped، 1m14s والا Core/Tests output دوبارہ بھیجا ہے۔ یہ متن پہلے والے نتیجے سے یکساں ہے؛ اس میں commit، run date یا login کا نتیجہ نہیں، اس لیے اسے fix `9d02546` کے بعد کا نیا run یا Windows/WPF PASS فرض نہ کریں۔ Pull/commit اور login کا نتیجہ پوچھنا باقی ہے۔

مالک کی تصویر: `XamlParseException`, `TypeConverterMarkupExtension`, **line 72, position 31**؛ دکھائی گئی crash file `C:\ProgramData\Area11\logs\crash-2026-11-10_083528.txt` ہے۔ تصویر exception کی اندرونی وجہ یا متعلقہ XAML file نہیں دکھاتی، لہٰذا مکمل crash file ابھی بھی ضروری ہے۔ Counter `PosView.xaml` کی line 72 پر `DataGrid.RowHeight="Auto"` ملا؛ Products grid میں بھی یہی لکھا تھا۔ WPF source کے مطابق `DataGrid.RowHeight` ایک `double` ہے، اس کا auto-sizing default `double.NaN` ہے اور اس property پر Auto text converter موجود نہیں؛ .NET `DoubleConverter` عدد parse کرتا ہے۔ یہ owner error سے مطابقت رکھنے والا مضبوط ممکنہ سبب ہے، مگر screenshot اکیلا مکمل ثبوت نہیں۔

دونوں جگہ `RowHeight="NaN"` رکھا گیا ہے تاکہ theme کی 30px fixed row کو override کر کے thumbnail content کے مطابق height بنے؛ static checker میں DataGrid/Setter پر `RowHeight="Auto"` کا guard شامل کیا گیا ہے۔ مالک نے login screen کھلنے اور error ختم ہونے کی اطلاع دی؛ مکمل Counter اور Products rendering اور medium/large thumbnails کی تصدیق **ابھی باقی** ہے؛ اسے physical fix/pass مت لکھیں۔ Owner کا 973/973 Core/Tests run اس XAML کو verify نہیں کرتا۔ متعلقہ run کی پوری crash file بھیجیں، خاص طور پر inner exception اور stack trace، کوئی password/database نہ بھیجیں۔

## PT-15 — Login کے بعد ایپ بند ہونے کی اطلاع (تحقیق جاری)

مالک نے بتایا کہ password درج کرکے Enter دبانے پر app crash/بند ہو جاتی ہے۔ یہ مسئلہ **غیر حل شدہ، ابھی reproduce یا diagnose نہیں ہوا**؛ crash report، درست وقت، login کے بعد نظر آنے والا پیغام، run trail اور عین commit کی تصدیق درکار ہے۔ اسے test-suite pass یا پچھلے ورژن کی کامیابی سے رد نہ سمجھیں۔ Password، database یا حقیقی مریض/مالی معلومات نہ مانگیں۔

مالک نے الگ Windows log فراہم کیا: banner **v0.42.0 Kyanite**, expected 973, **973 passed، 0 failed، 0 skipped، 1m 14s**؛ log میں صرف Area11.Core اور Area11.Tests build/run ہیں۔ 973 کا runner موجودہ mixed WIP سے مطابقت رکھتا ہے، مگر banner دوبارہ استعمال ہوا ہے اور log خود exact Git commit ثابت نہیں کرتا۔ یہ Desktop/WPF build، login success، physical PASS یا crash fix ثابت نہیں کرتا۔ پچھلے Kyanite 929/929 کو الگ نتیجے کے طور پر برقرار رکھیں۔

اگلا قدم: اسی crash والے run کے فوراً بعد `tools\SEND-REPORT.cmd` سے report لیں، یا تازہ crash report اور `%TEMP%\Area11\last-run.txt` لیں؛ تاریخ/وقت ملا کر stale log استعمال نہ کریں۔ File share کرنے سے پہلے مالک حساس ذاتی تفصیل ہٹا سکتا ہے، مگر exception/stack trace، وقت اور build/commit باقی رکھے۔ App process واقعی بند ہوتی ہے یا window غائب/پیغام آتا ہے، اور Sign in button بمقابلہ Enter کا فرق پوچھیں۔ رپورٹ موجود نہ ہو تو یہ بات بھی درج کریں۔ وجہ ثابت ہونے تک کوئی guessed fix نہ کریں؛ حقیقی data reset نہ کروائیں۔


## PT-15 — Portion 17: فوٹوز اور app icon — کام شروع، جانچ pending

مالک نے products کی متعدد تصاویر میں سے نظر آنے والی اہم تصویر منتخب کرنے، customers، doctors اور staff کی تصاویر، top-right staff photo، Settings میں app icon، اور Sales/Stock میں قدرے بڑے rounded photos کے medium/large انتخاب کی درخواست کی ہے۔ اس کام کو اب ترجیح دینے کی اجازت دی گئی؛ Portion 4 نامکمل محفوظ ہے۔ Product cover منتخب کرنے کی ابتدائی code اور دو tests جاری worktree میں لکھے گئے ہیں؛ ابھی release یا Windows-tested نہیں۔ Customer/doctor/staff، icon، size settings، backup/sync اور Windows verification مکمل نہیں؛ کسی چیز کو physical PASS نہ سمجھا جائے۔

مالک کا مفصل Kyanite log موصول: **929 پاس، صفر فیل، صفر skipped، 1 منٹ 15 سیکنڈ**؛ یہ Portion 17 کا test نہیں۔ WPF اور practical tests الگ pending ہیں۔

## Launch اور tests تک آسان رسائی — Windows verification pending

مالک کی درخواست پر مرکزی folder میں `START-AREA11.bat` کے ساتھ `RUN-TESTS.bat` شامل کیا گیا ہے؛ دوسرا موجودہ `tools\RUN-TESTS.cmd` ہی چلاتا ہے اور `test.log` وہیں رہتا ہے۔ App شروع کرنے سے tests خود نہیں چلتے۔ پہلے start پر اسی folder میں Area11 logo والے `! START AREA11` اور `! RUN TESTS` shortcuts بنانے کی کوشش ہوتی ہے؛ failure سے app start نہیں رکتا اور موجودہ shortcuts overwrite نہیں ہوتے۔ File Explorer کی Large icons setting سے ان کا سائز بڑا کیا جا سکتا ہے۔ Folder منتقل ہو تو دونوں پرانے shortcuts حذف کر کے start دوبارہ کریں۔

**Pending Windows check:** نئی branch pull کریں؛ root سے دونوں BAT چلائیں؛ start پر دونوں logo shortcuts بنیں اور صحیح target چلائیں؛ folder میں space والے path پر چیک کریں؛ `test.log` اور result درست رہیں؛ shortcut generation ناکام ہو تو app پھر بھی چلے۔ یہ ابھی Windows پر تصدیق شدہ نہیں۔ Portion 4 کا جاری کام الگ ہے۔

## مالک کا Kyanite test نتیجہ اور آئندہ طریقۂ کار

مالک نے مکمل runner log فراہم کیا: **v0.42.0 Kyanite — 929 passed، 0 failed، 0 skipped، total 929، duration 1 منٹ 15 سیکنڈ؛ ALL GREEN - 929 of 929**۔ Log میں صرف Area11.Core اور Area11.Tests build/run ہیں؛ Desktop/WPF build/launch اور PT-14 physical walkthrough الگ pending ہیں۔ Siderite 922/922 پہلے سے OK ہے، مگر اس کا WPF/physical ثبوت بھی الگ pending ہے۔

ہر مکمل portion کے ساتھ اس کی تبدیلیاں، خصوصی test steps اور اگلا portion بتایا جائے گا۔ مالک کے NEXT پر اگلا portion شروع کیا جائے گا؛ دیر سے آنے والے results متعلقہ version/portion کے تحت درج ہوں گے، physical delay بذاتِ خود کام نہیں روکے گی۔ سنگین data، stock، money، security یا backup مسئلہ ترجیح سے دیکھا جائے گا۔

## مالک کا Siderite Windows test — Portion 2

مالک کے فراہم کردہ v0.41.0 Siderite output میں **922 passed، 0 failed، 0 skipped، 922 total، 1 منٹ 21 سیکنڈ؛ ALL GREEN - 922 of 922** ہے۔ Log صرف Area11.Core اور Area11.Tests دکھاتا ہے؛ Desktop/WPF build، launch اور PT-12/PT-13 physical walkthrough اس سے ثابت نہیں ہوتے۔ یہ نتیجہ Kyanite v0.42.0 کے 929 tests کا نتیجہ نہیں؛ Kyanite کا اپنا 929/929 Windows Core/Tests log بعد میں موصول ہوا ہے، PT-14 ابھی باقی ہے۔ مالک نے اگلا حصہ شروع کرنے کی ہدایت دی ہے۔

## PT-14 — Master Portion 3: primary category اور additional tags

**حالت: code/static تیار؛ v0.42.0 Kyanite Core/Tests کے 929/929 Windows tests مکمل log کے مطابق پاس؛ Desktop/WPF build اور owner physical verification باقی۔** Product list کے search rows tag names دکھاتے تھے مگر `CategoryTagIds` نہیں رکھتے تھے؛ اسی row سے editor لوڈ اور save پر tags ضائع ہو سکتے تھے۔ اب editor کھولنے سے پہلے مکمل record پڑھتا ہے۔ فہرست primary اور اضافی categories الگ دکھاتی ہے۔ نئی/بدلی گئی tags کے لیے explicit primary درکار ہے، مگر بغیر tags والا product uncategorised رہ سکتا ہے۔ کوئی tag خود primary نہیں بنتا۔

Raw SQLite probe سے ایک **ممکنہ** tag-only product کی primary label `Uncategorised` ہونا ثابت ہوا؛ یہ مالک کے اصل UI واقعہ کی مکمل reproduction نہیں۔ پرانے tag-only records خودکار rewrite نہیں ہوتے؛ غیر متعلقہ partial service updates انہیں برقرار رکھتے ہیں۔ Workbook category clear/tag-promotion اب preview اور apply دونوں پر check ہوتے ہیں؛ CSV import نئے products بناتا ہے اور tags import نہیں کرتا۔ Category put-away اب بھی primary unlink کر کے دوسرے tags چھوڑ سکتا ہے؛ اس رویے پر مالک کا جائزہ باقی ہے، اس لیے app-wide مکمل حل کا دعویٰ نہیں۔ مالی reports صرف primary category گنتی ہیں؛ پرانی financial history نہیں بدلی۔

**صرف isolated test copy میں چیک کریں:** (1) دو categories بنائیں؛ product پر ایک primary اور دوسری extra tag رکھ کر save؛ (2) editor دوبارہ کھولیں، tag checked ہو، صرف نام/ریٹ بدل کر save کریں، دوبارہ کھولنے پر tag برقرار؛ (3) دونوں category filters سے وہ product ایک مرتبہ ملے؛ (4) primary کے بغیر tag save پر واضح refusal اور کوئی جزوی mutation/audit نہ ہو؛ (5) primary اور tag دونوں نہ ہوں تو product allowed؛ (6) primary-only financial report میں sale دوبار شمار نہ ہو؛ fake sale صرف isolated copy میں؛ (7) پرانا tag-only record خود نہ بدلے؛ مالک primary منتخب کرے؛ (8) Core/Tests `ALL GREEN - 929 of 929` موصول؛ Desktop/WPF build اور launch الگ جانچیں؛ (9) Excel preview/apply میں tags والے product کی primary clear یا tag کو primary بنانے کی کوشش رد ہو، جبکہ بغیر tags کے clear کامیاب ہو؛ preview کے بعد tag شامل ہونے پر apply بھی رد ہو؛ (10) category put-away سے primary ہٹنے اور دیگر tags باقی رہنے کے رویے کا جائزہ لیں؛ (11) اگر اصل Uncategorized مسئلہ دوبارہ آئے تو exact product، categories، steps اور screenshot بھیجیں۔

PT-13 Siderite WPF/product-editor checks اور PT-12 navigation بھی الگ pending ہیں۔ PT02-POS-01 unreproduced ہے۔ Portion 4 کی اجازت مل گئی ہے؛ Portions 5–15 الگ approval کے منتظر ہیں۔


## PT-13 — Master Portion 2: Product workspace اور Dashboard fallback

**حالت: v0.41.0 Siderite Core/Tests Windows 922/922 PASS؛ Desktop/WPF build اور owner physical verification باقی۔** Baltic v0.40.0 پر مالک کے Core/Tests **921/921 PASS** الگ نتیجہ ہے۔ مالک کے فیصلے کے مطابق Counter permission نہ ہو تو Dashboard startup کا code لکھا گیا؛ کوئی نئی permission نہیں دی گئی۔

**Isolated test copy میں جانچیں:**
1. Owner یا Counter-allowed user login پر Counter دیکھے؛ Counter permission کے بغیر user پر Dashboard کھلے اور sensitive/profit data اپنی موجودہ permissions کے مطابق رہے۔
2. Products کی full-width list، چھوٹی image اور missing-photo placeholder دیکھیں۔ Top **New product** سے editor کھلے؛ ایک نئی test medicine محفوظ کریں، پھر دوبارہ Save سے دوسری product row نہ بنے۔
3. ایک row منتخب کر کے **Edit product**، پھر editor بند کر کے اسی row پر **double-click**: دونوں اسی medicine کے editor میں جائیں۔ خالی جگہ/header اور photo/category/company buttons double-click سے غلط product edit نہ ہو۔
4. Editor میں unsaved changes کے بعد Cancel/X پر default No warning: No سے کام قائم رہے؛ Yes سے صرف غیر محفوظ values ضائع ہوں۔ Save کے بعد editor کھلا رہے؛ extra fields الگ Save ہوتے ہیں اور product Save ان کے غیر محفوظ values نہ مٹائے۔ Photo/barcode اور catalog/units کی nested windows اور ان سے واپسی چیک کریں۔
5. Remove/put-away پر default No confirmation اور history protection چیک کریں؛ صرف isolated test data میں۔ Keyboard، چھوٹی screen، user permissions، validation errors اور نام/pack/category/strength/rates/edit کے مطابق service audit چیک کریں۔
6. Windows build اور `tools\RUN-TESTS.cmd` میں **ALL GREEN - 922 of 922** بھیجیں؛ failure پر اصل log بھیجیں۔ PT-12 header sections، bell اور fallback کا physical proof بھی الگ باقی ہے۔

ہر Save genuine database میں حقیقی record بدلتا ہے؛ fake medicine یا put-away صرف isolated copy میں۔ PT02-POS-01 unreproduced ہے؛ اسے اس کام سے fixed نہ لکھیں۔ Portions 3–15 الگ approval کے منتظر ہیں۔


## مالک کا مصدقہ Baltic Windows test اور Product Edit کا فیصلہ

مالک کی فراہم کردہ v0.40.0 Baltic Windows test output: **921 passed، 0 failed، 0 skipped، 921 total، 1 منٹ 14 سیکنڈ؛ `ALL GREEN - 921 of 921 cases passed, failed: 0`**۔ Log میں `Area11.Core` اور `Area11.Tests` build/run دکھتے ہیں؛ اس سے Desktop/WPF build، launch یا PT-12 physical walkthrough ثابت نہیں ہوتے۔ پہلے کی 918/918 کی اطلاع کے version کا ثبوت اب بھی الگ غیر مصدقہ ہے۔

مالک نے Product editing کے لیے **مخصوص Edit Product button اور product row پر double-click دونوں** منتخب کیے ہیں۔ Portion 2 ابھی implement یا approved-for-coding نہیں ہوا۔ Counter permission نہ ہونے پر Dashboard startup کا نیا فیصلہ بھی موجودہ Baltic code میں بدلنا باقی ہے۔

## PT-12 — مالک کی نئی ہدایت: Counter کی اجازت کے بغیر Dashboard

مالک نے واضح کیا کہ اگر login کرنے والے user کو Sales/Counter کی اجازت نہ ہو تو startup پر **Dashboard** کھلے۔ Baltic کا موجودہ fallback پہلا permitted ordinary page ہے، اس لیے یہ owner decision ابھی اس سے مختلف ہے اور **code adjustment باقی ہے**؛ اسے fixed یا verified نہ لکھیں۔ Counter اجازت اور موجودہ Dashboard کے data/profit permission guards برقرار رہیں۔ مالک نے Baltic کے 921/921 test result کی تصدیق بھیج دی ہے۔ Product editing کے لیے **مخصوص Edit Product button اور row پر double-click دونوں** منتخب کیے ہیں؛ Portion 2 کی coding ابھی باقی ہے۔

## PT-12 — Master checklist Portion 1: header/navigation and Counter startup

**حالت: Baltic Core/Tests Windows xUnit 921/921 PASS — Desktop/WPF اور owner physical verification باقی۔** مالک نے Portion 1 کے لیے NEXT کہا۔ v0.40.0 Baltic `#33566B` میں Area11 wordmark سے Dashboard برقرار مگر الگ Dashboard module ہٹایا گیا ہے؛ اجازت یافتہ Counter ابتدائی page، ورنہ پہلا permitted ordinary page، اور کوئی ordinary page نہ ہو تو Dashboard fallback ہے۔ مالک نے بعد میں Dashboard fallback منتخب کیا؛ اس کے مطابق code adjustment باقی ہے، اجازتیں نہیں بڑھائی جائیں گی۔ Returns، Customers/Suppliers، Doctors اور Import/Export کے موجودہ tab contents اصل controls میں ہیں اور ان کے section buttons dark header میں ہیں؛ Settings پہلے سے header میں تھا۔

**مالک کی الگ جانچ:** (1) Core/Tests `ALL GREEN - 921 of 921` پاس؛ Desktop/WPF build اور launch الگ باقی؛ (2) login پر Counter، پھر Area11 wordmark سے Dashboard، اور الگ Dashboard tab نہ ہو؛ (3) محدود users، خاص طور پر Counter کے بغیر، درست fallback اور hidden sections؛ (4) Returns، Partners، Doctors، Import/Export اور Settings کے ہر مجاز section کو header سے کھولیں، text/forms محفوظ رہیں؛ (5) keyboard، چھوٹی screen، 44px bell، alerts، category filter، session/fault cards؛ (6) PT-08 navigation اور آدھے درج bill کے سلسلے میں کسی data-loss observation کی الگ اطلاع دیں۔ Genuine books میں fake financial test نہ کریں۔ یہ checklist physical PASS نہیں۔

مالک نے 918/918 کی اطلاع دی، مگر اس run کا version/banner ابھی غیر مصدقہ ہے؛ سابق Tourmaline 916/918 evidence برقرار ہے۔ Parts 2–15 باقی ہیں اور ہر ایک کی الگ اجازت درکار ہے۔


## نئے 918/918 نتیجے کی اطلاع — version تصدیق باقی

مالک نے بتایا کہ تازہ run میں **918/918 tests پاس** ہوئے۔ اس اطلاع کو محفوظ رکھا گیا ہے؛ اس run کا مکمل runner output اور build/version ابھی فراہم نہیں ہوا، اس لیے اسے کسی خاص version، Desktop/WPF build یا physical verification سے منسوب نہیں کیا گیا۔ مالک سے banner اور نتیجے کی تصدیق درکار ہے۔ سابقہ Tourmaline کا 916/918 نتیجہ نیچے برقرار ہے۔ مالک کی مکمل 12 نکاتی master checklist اور مجوزہ 15 portions `docs/MASTER-CHECKLIST-ROADMAP.md` میں محفوظ ہیں؛ planning ہے، code approval نہیں۔

## تازہ owner evidence — Tourmaline test failures اور Admiralty repair

مالک نے v0.39.0 Tourmaline کا Windows Core/Tests run بھیجا: **916 passed، 2 failed، 0 skipped، 918 total، 1m 16s**۔ یہ green نہیں اور Desktop/WPF build یا physical PASS نہیں۔

1. `MigrationTests.FieldGovernanceMigration_BackfillsStableKeysAndKeepsAnswers`: pre-029 fixture اب 029 اور 030 دونوں چلاتی ہے، مگر پرانا assertion صرف 1 مانگتا تھا۔ v0.39.1 میں 2، دونوں ledger IDs، idempotent rerun اور legacy answer preservation چیک ہیں۔
2. `MasterFoundationTests.DraftsValidateJsonAndNeverBecomeFinancialDocumentsByThemselves`: `@id` کے لیے anonymous parameter `Id` کے بجائے exact `id` دے دیا گیا۔ اب SQLite constraint error code 19 اور draft کا unchanged/unposted رہنا بھی assert ہے۔ Schema یا posting guard نہیں بدلا۔

**حالت:** v0.39.1 Admiralty repair لکھی گئی، Windows retest pending؛ **ALL GREEN - 918 of 918** اور PT-11 اب بھی درکار ہیں۔ یہاں .NET/xUnit یا WPF execute نہیں ہوا۔ مالک نے late physical results کے باوجود اگلا approved portion جاری رکھنے کی ہدایت دی؛ بعد کا feedback محفوظ اور حل ہوگا، مگر سنگین data/financial risk کو فوری ترجیح دی جائے گی۔ Part 1 header/navigation کی تفصیلی master-checklist scope ابھی مالک سے واضح ہونی ہے؛ کوئی UI change اس repair میں شامل نہیں۔


یہ مالک کی عملی ٹیسٹنگ، تجاویز اور درخواست کردہ تبدیلیوں کی مستقل فائل ہے۔ ہر نتیجہ اور نئی بات اسی فائل میں شامل ہوگی تاکہ testing کے اختتام پر کوئی نکتہ نہ بھولے۔

**مالک کا consolidated printable tracker:** [PDF](docs/AREA11-TESTING-TRACKER-URDU.pdf) · [RTL HTML](docs/AREA11-TESTING-TRACKER-URDU.html) · [editable Markdown](docs/AREA11-TESTING-TRACKER-URDU.md)۔ وہ snapshot PT-01 تا PT-10 اور manual instructions کے لیے ہے؛ running feedback کا ماخذ یہی فائل رہے گی۔

## کام کا اصول

- Testing کے دوران پہلے واقعہ، توقع، حقیقی نتیجہ اور مطلوبہ تبدیلی **record** ہوگی۔
- مالک نے 27 اکتوبر کو **“fix the things”** کہہ کر درج شدہ fixes کی اجازت دی ہے؛ کوئی نکتہ خاموشی سے fixed یا rejected نہیں لکھا جائے گا۔
- Code fix، static check اور owner physical verification الگ الگ لکھے جائیں گے۔ مالک کے **next** پر اگلا physical-test portion بتایا جائے گا۔
- اصل دکان کے database میں صرف حقیقی لین دین؛ فرضی یا خطرناک مالیاتی cases کے لیے الگ test copy۔
- ہر item کی حالت: **Recorded → Planned → Fixed → Owner verified**۔
- پرانے owner notes نہیں مٹائے جائیں گے؛ نئی تصدیق کے ساتھ status اور تاریخ شامل ہوگی۔

## موجودہ build اور automated ثبوت

- Build: **v0.39.0 Tourmaline `#7A1F5C`** (D-99 master checklist Phase 1 schema/data contracts؛ اس release کا Windows build/xUnit ابھی owner سے طلب ہے)
- Latest owner xUnit: **900 passed / 0 failed / 0 skipped / 900 total — ALL GREEN**، **Cobalt v0.38.0**، 29 اکتوبر 2026، مدت **1 منٹ 4 سیکنڈ**۔ Runner نے **`ALL GREEN - 900 of 900 cases passed, failed: 0`** دیا۔ یہ **previous-release Cobalt** کے `Area11.Core` / `Area11.Tests` Windows xUnit ثبوت ہے؛ Malachite، `Area11.Desktop` / WPF build یا launch اور PT-10 WPF/genuine physical evidence نہیں۔
- Previous owner xUnit: **883 passed / 0 failed / 0 skipped / 883 total — ALL GREEN**، **Zircon v0.36.0**، 29 اکتوبر 2026، مدت **1 منٹ 10 سیکنڈ**۔ یہ previous-release evidence ہے۔
- موجودہ source: **918 written cases in 71 files**؛ **57/57 static checks PASS** (real C# grammar اور static-member resolution سمیت)۔ Latest gate: **294 C# files / 1,457 XAML bindings / 30 migrations / 42 application tables + ledger / 2 views / 887 prepared SQL statements / 130 settings keys**۔ اس کے علاوہ **17/17 raw SQLite rehearsal groups** کامیاب ہیں: pre-030 upgrade، critical acknowledgement، draft/no-financials، catalog uniqueness، preferred+historical suppliers، drawer constraints، REAL quantities اور isolated snapshot/replacement۔ یہ raw SQL development evidence ہے، .NET/xUnit/WPF یا real-cloud execution نہیں۔ **Tourmaline Windows build اور `ALL GREEN - 918 of 918` باقی ہے؛ Cobalt 900/900 صرف پچھلا automated ثبوت ہے**۔ PT-11 کے ساتھ PT-01، PT-10، Aurora PT-09 اور Zircon PT-08 کے retained physical checks الگ باقی ہیں۔
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
| **PT-01 — Startup, backup اور UI smoke** | **PASS — improvements logged** | ایپ کھلی، photos کے ساتھ backup بنا، اور تمام مقررہ pages کھلے؛ Malachite restore hardening لکھا گیا، isolated A/B/C/fault proof باقی |
| **PT-02 — حقیقی medicine اور product details** | **PASS — owner confirmed 29 Oct 2026** | مالک نے “2 is ok” کی تصدیق کی؛ الگ logged feedback items اپنی اپنی حالت میں رہیں گے |
| **PT-03 — حقیقی purchase، batch، expiry اور payable** | **PARTIAL — owner feedback logged، report pending** | ابھی تفصیلی PINV/stock/payable evidence موصول نہیں ہوا؛ PASS نہیں لکھا گیا |
| **PT-04 — Sale، cash/khata، receipt اور drawer** | **INSTRUCTIONS READY** | حقیقی فروخت، price/cart، receipt اور completion feedback |
| **PT-05 — Returns، PR اور cancellation** | Pending | الگ test copy کے ساتھ محفوظ جانچ |
| **PT-06 — Shift، ledgers، permissions اور audit** | Pending | مالیاتی اور user workflows |
| **PT-07 — Excel، restore، printer اور scanner** | Pending | Hardware اور data workflows |
| **PT-08 — Redesign Phase 1: dark header اور navigation** | **883/883 ZIRCON xUNIT PASS — WPF verification pending** | Automated suite کامیاب؛ header، Settings sections، category filter، keyboard اور permissions کی physical جانچ باقی |
| **PT-09 — Redesign Phase 2: field governance اور schema** | **CODE READY — Aurora Windows/WPF verification pending** | Stable keys، central validation، guidance، migration 029 اور existing answers |
| **PT-10 — Redesign Phase 3: packaging، variants اور search** | **900/900 COBALT xUNIT PASS — WPF verification pending** | Windows suite کامیاب؛ pack editing، Products/Counter/Purchase discovery، separate variant selection اور unchanged stock conversion کی physical جانچ باقی |
| **PT-11 — Master Phase 1: schema، contracts اور isolated rebuild** | **CODE/STATIC/RAW-SQL READY — Windows xUnit/owner verification pending** | Migration 030، format 3، draft/catalog/supplier/drawer contracts اور technical-snapshot rebuild؛ کوئی later-phase UI یا physical PASS نہیں |

---

# PT-11 — Master checklist Phase 1: Fresh Schema & Data Contracts

**حالت:** Code/static/raw-SQLite evidence موجود — Tourmaline Windows build/xUnit اور owner verification pending

**مقصد:** نئے master plan کی صرف بنیاد محفوظ کرنا: native/custom field contracts، local drafts، managed
formulas/brands/suppliers، drawer discrepancies، sync dependencies اور isolated test rebuild۔

**حدود:** Q1=A، Q2=C، Q3=A، Q4=C اور صرف Phase 1 منظور ہے۔ Bulk printing، WhatsApp workflow، نیا profit
UI، automatic-sync scheduler اور Phases 2–7 اس release میں نہیں ہیں۔ D-77 کا fake demo shop واپس نہیں آیا۔
Genuine books میں صرف genuine transactions؛ destructive/fault checks صرف tests کی temporary isolated copies میں۔

## Automated اور development ثبوت

- **57/57 static checks PASS**، real C# grammar اور static members سمیت؛ **918 written cases in 71 files**۔
- **17/17 raw SQLite rehearsal groups PASS:** 30 migrations، 42 application tables + ledger، 2 views،
  `quick_check`/`foreign_key_check`، REAL quantities، negative constraints، upgrade preservation اور snapshot۔
- یہ دونوں development evidence ہیں۔ **Tourmaline کا .NET build، xUnit، WPF، real Supabase، printer یا
  physical PASS ابھی نہیں**؛ Cobalt 900/900 previous-release evidence ہے۔

## مالک کی verification checklist

1. **Backup پہلے:** Tourmaline کھولنے سے پہلے موجودہ genuine shop کا معمول کا مکمل ZIP backup لیں۔ Upgrade
   کے بعد کوئی صاف/reset action genuine database پر نہیں کرنا۔ Test-reset کی اجازت صرف isolated test data ہے۔
2. **Build اور full suite:** `START-AREA11.bat` سے **v0.39.0 · Tourmaline** build کریں، پھر
   `tools\RUN-TESTS.cmd` چلائیں۔ مطلوبہ **`ALL GREEN - 918 of 918`**، failed/skipped 0۔ Compile یا test
   failure ہو تو `test.log` اور `tools\SEND-REPORT.cmd` کا report بھیجیں؛ کسی نتیجے کو pass نہ لکھیں۔
3. **Named Phase-1 tests:** log میں `MasterFoundationTests` کے 13 facts اور
   `SyncTests.MasterCatalogAndPreferredSupplierTravelBeforeTheProduct` شامل ہوں۔ یہی 14 focused regressions
   upgrade، schema، settings audit، sync order اور isolated rebuild/fault paths cover کرتے ہیں۔
4. **Genuine upgrade کا read-only موازنہ:** backup کے بعد Tourmaline کھولیں۔ کسی existing product کا
   generic/brand text، کونٹ، پرانا PINV/INV، payment اور batch quantity پہلے جیسا ہو۔ Migration 030 کوئی
   managed formula/brand/preferred link تخمینے سے نہیں بناتی؛ nullable links خالی رہ سکتے ہیں۔
5. **Draft boundary:** automated test میں invalid JSON اور بغیر document/timestamp کے posted-draft refusal
   ہو، اور draft سے sale/purchase/payment/stock movement صفر رہے۔ فی الحال draft save/posting کی WPF screen
   نہیں ہے؛ اس غیر موجود UI کو PT-11 failure یا PASS نہ لکھیں، یہ later phase ہے۔
6. **Catalog/supplier/drawer contracts:** tests میں duplicate normalized key، blank name، wrong drawer
   arithmetic، zero discrepancy اور duplicate shift/stage reject ہوں۔ Preferred کے ساتھ historical supplier
   اور fractional `2.5` REAL quantity برقرار رہے۔ ان contracts کی management/detail UI ابھی نہیں بنی۔
7. **Critical Owner Override:** بغیر named acknowledgement کے critical safe-required field optional نہ ہو؛
   acknowledgement کے ساتھ contract قبول ہو۔ اصل risk-warning + explicit-confirmation screen Phase 3 میں ہے،
   اس لیے یہاں صرف schema/test evidence لیں، UI completion کا دعویٰ نہیں۔
8. **Guarded isolated rebuild:** تین rebuild facts میں پہلا empty schema، دوسرے rebuild سے پہلے persistent
   `.db` technical snapshot، نئے copy میں disposable row کا خاتمہ اور پرانے snapshot میں اس کا موجود رہنا،
   wrong-confirmation/unmarked refusal، اور backup-directory fault پر original isolated row کا تحفظ جانچا
   گیا ہے۔ Genuine `%ProgramData%\Area11` میں `area11-test.db` یا test marker نہیں بننا چاہیے، اور production
   menu/`--demo` route موجود نہیں۔ Genuine folder میں خود marker ہرگز نہ بنائیں۔
9. **Settings versus behavior:** 130 defaults میں نئی print/WhatsApp/profit/sync contract keys ہیں، مگر
   automatic sync OFF رہے اور کوئی background push نہ چلے۔ نیا bulk-print/WhatsApp/profit UI نہ ملنا اس
   Phase کی حد ہے؛ اسے implemented نہ لکھیں۔ Existing owner-visible settings کا old → new audit برقرار رہے۔
10. **Cloud حد:** format-3 round-trip fake transport پر ہے۔ Local drafts sync order میں نہیں، اور managed
    parents product سے پہلے ہیں۔ Real Supabase test نہ چلائیں جب تک الگ test Shop id/table، isolated database
    اور owner approval نہ ہو؛ fake-wire PASS کو real-cloud PASS نہیں لکھنا۔
11. **Financial safeguards:** upgrade/rebuild tests کے دوران genuine stock، sale، purchase، payment، balance،
    invoice number یا drawer میں کوئی automatic row نہ بنے۔ PR cancellation سے positive payment/drawer inflow
    بنانے کا قاعدہ نہیں بدلا؛ negative-stock retirement اور `qty_base REAL` بھی unchanged ہیں۔
12. **باقی retained checks:** PT-01 isolated restore/fault، PT-10 pack/search/variant، PT-09 governance اور
    PT-08 navigation کے اپنے evidence الگ مکمل کریں۔ PT02-POS-01 ابھی unreproduced ہے؛ بغیر exact evidence
    کے fixed/rejected نہ لکھیں۔

## نتیجہ

```text
Build: v0.39.0 Tourmaline
PT-11: PASS / PARTIAL / FAIL
Windows build errors/warnings:
Windows xUnit: total / failed / skipped / duration:
Runner line (expected ALL GREEN - 918 of 918):
13 MasterFoundationTests + 1 Sync master round-trip present:
Genuine backup filename before upgrade:
Existing generic/brand text and historical documents preserved:
Draft creates no financial/stock rows (test evidence):
Preferred + historical supplier and REAL quantity (test evidence):
Critical acknowledgement / drawer negative paths (test evidence):
Isolated snapshot-before-rebuild and backup-fault preservation (test evidence):
Genuine folder has no area11-test.db / marker / demo route:
No bulk-print/WhatsApp/profit UI or automatic scheduler claimed:
Stock/payment/document side effect on genuine upgrade (must be none):
Real Supabase run? (No, unless separately approved/isolated):
Screenshots / error / test.log / support report:
```

**Owner result:** Pending — کوئی Tourmaline Windows/xUnit یا PT-11 physical PASS ابھی record نہیں۔

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

**حالت:** Cobalt Windows xUnit **900/900 PASS** (29 اکتوبر 2026، 1 منٹ 4 سیکنڈ) — مالک کی PT-10 WPF/genuine physical verification pending

**مقصد:** پہلے سے موجود pack/variant label کو edit اور search کے قابل بنانا، اور ایک جیسے نام والی separate products کو Counter/Purchase میں واضح منتخب کرنا۔

**حدود:** کوئی نیا variant/stock model یا migration نہیں۔ Base-unit stock، box/strip/base conversion، prices، payments، documents، historical rows، extra pack barcodes اور Phase-2 governance نہیں بدلنے چاہئیں۔ Phase 4 شامل نہیں۔

## Automated اور development ثبوت

- **55/55 static checks PASS**، real C# grammar سمیت۔
- **900 written cases in 70 files**، جن میں Phase-3 کے 8 regressions شامل ہیں؛ مالک کے Windows پر **900 passed / 0 failed / 0 skipped**، مدت **1 منٹ 4 سیکنڈ**، اور runner کا **`ALL GREEN - 900 of 900`** موصول ہوا۔
- Actual ProductService search SQL کو تمام 29 migrations والے SQLite database پر چلا کر **7/7 development search groups** کامیاب ہوئے: `50ml`، `100 ML`، brand، room، category، company اور base unit۔ یہ Windows xUnit یا WPF proof نہیں۔

## مالک کی physical checklist

1. **Automated suite — 29 اکتوبر کو owner-verified:** `tools\\RUN-TESTS.cmd` سے **v0.38.0 · Cobalt** کے `Area11.Core` / `Area11.Tests` میں **900 total / 0 failed / 0 skipped**، مدت **1 منٹ 4 سیکنڈ** اور **`ALL GREEN - 900 of 900`** موصول ہوا۔ supplied log `Area11.Desktop` / WPF build یا `START-AREA11.bat` launch الگ ثابت نہیں کرتا؛ اسے PT-10 کا WPF physical PASS بھی نہ سمجھیں۔
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
Windows xUnit: 900 total / 0 failed / 0 skipped / 1 m 4 s — owner verified 29 Oct 2026
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

**Owner result:** Pending — Cobalt Windows xUnit **900/900 PASS** محفوظ ہے؛ کوئی PT-10 WPF/genuine physical PASS ابھی record نہیں۔

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

### 30 اکتوبر Malachite code update

- نیا public-service regression اب پورا **A → B → A restore → C → نیا backup → C restore** sequence photos کے ساتھ لکھتا ہے، پھر pre-A safety zip سے **B** recover کرتا ہے اور C backup کو دوبارہ restore کر کے ثابت کرتا ہے کہ B/C الجھتے نہیں۔
- Damaged zip کا regression live products/photo کو unchanged رہنے اور safety/replace step سے پہلے refusal لکھتا ہے۔
- یہ **written automated coverage** ہے، owner کے Windows پر چلنے یا isolated physical A/B/C PASS کا بدل نہیں۔ Malachite کے **904 written cases** چلانا اور نیچے دیا physical scenario isolated copy میں کرنا ابھی باقی ہے۔

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

30 اکتوبر **v0.38.1 Malachite** میں code review نے ایک محدود failure boundary ٹھیک کی ہے: پہلے copy یا locked destination پر replacement ناکام ہو تو بھی `finally` میں live `-wal` / `-shm` / `-journal` حذف ہو سکتے تھے۔ اب یہ companions صرف same-volume `File.Move` کے کامیاب ہونے کے بعد صاف ہوتے ہیں؛ ناکام replacement پر موجودہ database family نہیں چھڑی جاتی۔ یہ owner-reported data-loss incident نہیں، محدود code-path defect تھا۔

چار public-service regressions شامل ہیں: مکمل A/B/C + photos + B recovery، damaged zip refusal، photo-copy failure کے بعد database/previous-photo rollback، اور Windows میں Delete-sharing کے بغیر real open-handle lock پر live sidecars/data preservation۔ نیا static guard اس ordering اور rollback door کو بھی محفوظ رکھتا ہے۔ موجودہ inventory **904 written cases / 56 static checks** ہے، مگر Malachite کا Windows xUnit ابھی نہیں چلا۔ Locked-file regression **disk-full simulation نہیں**؛ real locked-file، committed-WAL، disk-full، restart اور A/B/C physical checks PT01-BK-02/BK-03 میں کھلے رہیں گے۔

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
