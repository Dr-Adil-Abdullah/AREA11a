# فیز ۳ — الگ Extra Discount، استعمال، محفوظ آزمائش اور واپسی

**v0.78.0 Avocet، سلیٹی نیلا `#465D7A`۔** فیز۳ کا source code موجود؛ **1300 ٹیسٹ لکھے ہیں، جن میں 30 نئے ہیں، یہاں چلائے نہیں گئے۔** Windows build، اصل keyboard/چھوٹی screen/پرنٹر اور PT-22 باقی۔ اس نامے کو عملی PASS نہ سمجھیں۔ فیز۴ کا جامع جائزہ ابھی نہیں ہوا۔

## پہلے مختصر خلاصہ

| اختیار | مطلب |
|---|---|
| May give separate Extra Discount | مالک اسی منتخب شخص کو الگ آخری رعایت کی اجازت دیتا ہے |
| Extra Discount limit (Rs per whole bill, not per click) | پورے بل کی حد؛ 20/30/50 مثالیں، کوئی خودکار 50 نہیں |
| More / split → Extra Discount | روپے کی پوری مطلوبہ رعایت لکھیں؛ صرف لکھنے سے لاگو نہیں ہوتی |
| Apply Extra Discount | اصل رقم، الگ رعایت اور آخری payable کی تصدیق؛ دوبارہ Apply پچھلی رقم کی جگہ لیتا ہے |
| Remove Extra Discount | صرف یہ آخری رعایت ہٹتی ہے؛ آئٹم کے ریٹ اور پہلے کی رعایتیں نہیں |
| Returns: طریقہ B | پہلے واپس آنے والی چیز کی اصل محفوظ رقم، مگر پورے بل کی باقی قابلِ واپسی حد تک |

**مثال:** 1120 کا بل، الگ Extra Discount 20، final 1100، وصولی 1100 → بقایا صفر۔ Extra کے بغیر صرف 1100 لینا خود سے 20 معاف نہیں کرتا؛ موجودہ cash کم ہو تو save انکار کرسکتا ہے۔ ادھار منظور ہو تو customer اور credit/split باقاعدہ منتخب کریں، جعلی پوری وصولی درج نہ کریں۔

## 1 — حفاظت پہلے، خاص طور پر اصل دکان کا ڈیٹا

1. اصل shop کا تازہ **photos سمیت backup** لیں، ZIP اور database موجود ہونے کی تصدیق کریں؛ restore کی آزمائش الگ مشین/VM میں کریں۔
2. پروگرام کی دوسری exe یا دوسرا فولڈر **الگ database کا ثبوت نہیں**۔ ProgramData مشترک ہوسکتا ہے۔ posted sale، return، cash/shift آزمائش صرف الگ test data/Windows environment میں۔
3. اصل دکان پر صرف **غیر محفوظ cart کا preview** دیکھ سکتے ہیں؛ F4/Complete sale، Take the return یا دوسری posting نہ دبائیں۔ بعد میں Clear کریں۔
4. Migration036 میں پرانے sales/item amounts نہیں بدلتے؛ تاریخی pre-extra snapshot دستیاب نہیں تو NULL رہتا ہے، فرضی نئی رقم نہیں لکھی جاتی۔ پرانی الگ رعایت صفر ہے۔

## 2 — مالک ہر شخص کی اجازت اور روپے کی حد مقرر کرے

1. مالک کے اپنے نام سے داخل ہوں۔ **Settings → Staff & permissions** کھولیں۔
2. مطلوبہ شخص منتخب کریں۔ **This person's pricing limits** میں **May give separate Extra Discount** tick کریں۔
3. **Extra Discount limit (Rs per whole bill, not per click)** میں مثلاً **20** لکھیں۔ یہ فیصد نہیں، روپے ہیں۔
4. **Save this person's pricing limits** دبائیں؛ کامیابی کا پیغام دیکھیں۔ عام Settings Save اس الگ پینل کا متبادل نہیں۔
5. مالک خود Extra دینا چاہتا ہے تو **اپنی اجازت/حد بھی محفوظ کرے**۔ نئی Extra اجازت مالک سمیت کسی کو خود سے نہیں ملی۔ پہلے سے موجود profit/retail/manual حدیں الگ ہیں۔
6. مینیجر کسی کی Extra حد نہیں بدل سکتا۔ ایک ہی role کے دو لوگوں کی حدیں مختلف ہوسکتی ہیں۔ permission/cap بدلنے کے بعد اگلے quote/save پر نئی حد پڑھی جاتی ہے؛ پرانا کھلا draft اسے bypass نہیں کرتا۔

**واپسی:** اسی شخص کا tick ہٹا کر محفوظ کریں، یا cap کم کریں۔ محفوظ پرانے bills نہیں بدلیں گے؛ جاری cart میں پہلے سے Extra ہے تو Remove کریں یا مالک کی موجودہ اجازت کے مطابق درست Apply کریں۔

## 3 — 1120 میں سے 20 الگ چھوڑنے کے قدم

1. الگ test data میں دو چیزیں **560 + 560** کی cart میں رکھیں۔ خرید کی لاگت اتنی کم ہو کہ 20 چھوڑنے کی گنجائش ہو۔ صاف مثال کے لیے test copy میں rounding **none** رکھیں؛ اصل دکان کی settings بلاوجہ نہ بدلیں۔
2. آئٹم کے ریٹ، پہلے کی item رعایت اور bill% پہلے طے کریں۔ سب لائنیں manual/fixed ہوں تب بھی الگ Extra اجازت کے ساتھ چل سکتا ہے۔
3. **More / split** کھولیں۔ **Before Extra Discount** میں پچھلے مکمل حساب کی رقم دیکھیں۔ rounding/points بھی اسی سے پہلے ہوتے ہیں۔
4. **Extra Discount (Rs for the whole bill)** میں **20** لکھیں۔ ابھی یہ صرف entry ہے، لاگو نہیں۔
5. **Apply Extra Discount** دبائیں۔ popup میں **1120 / 20 / 1100** دیکھ کر Yes کریں؛ No سے سابقہ لاگو رقم برقرار۔ یہ confirmation فعال Counter window کی ملکیت میں ہے۔
6. footer میں **Extra: −Rs 20** اور **TOTAL Rs 1100** دیکھیں۔ More panel بند کرنے سے لاگو رعایت نہیں ہٹتی۔
7. **Cash received** میں 1100 درج کریں۔ صرف الگ test data میں Complete sale/F4 کریں۔ رسید اور Bills detail میں اصل/pre-extra1120، Extra20، final1100؛ cash1100، due0، change0۔ Reprint نیا بل نہیں بنائے گا۔

### دوبارہ Apply کا مطلب

- پہلے20 Apply، پھر20 Apply → **کل20**، کل40 نہیں۔
-20 کی جگہ30 Apply → نئی پوری رعایت30؛ شخص کی cap20 ہو تو انکار، چپکے20 نہیں لگائے گا۔ پرانی لاگو20 برقرار۔
- داخل کیا ہوا عدد لاگو رقم سے مختلف ہو تو Complete sale/Park/Save draft روکے گا: پہلے Apply یا Remove کریں۔ کسی failed Apply کے بعد پرانی رقم کو نئی سمجھ کر save نہ کریں۔
- Remove سے کل پھر1120؛ line rates/line discounts/bill% نہیں بدلتے۔ Clear/Park/کامیاب post کے بعد نئی live cart کا Extra صفر؛ Park/Draft دوبارہ کھولنے پر اسی cart کی محفوظ Extra واپس آتی ہے، موجودہ اجازت دوبارہ چیک ہوگی۔

## 4 — خرید کی لاگت کا لازمی تحفظ

- پہلے ہر اصل allocated batch/line پر خرید سے نیچے ریٹ/رعایت رکتی ہے؛ مالک بھی مستثنیٰ نہیں۔
- اس کے بعد مکمل بل سے الگ Extra کم ہوگا، اور **final bill اصل allocated خرید کی مجموعی لاگت سے کم نہیں ہوسکتا**۔ یہ check item rows بدل کر نہیں کیا جاتا۔
- مثال: pre-extra1120، actual cost1110؛ Extra20 سے final1100 بنتا ہے، اس لیے **انکار**۔ Extra10 تک ہو تو باقی اجازتیں درست ہونے پر1110 ہوسکتا ہے۔
- cap اجازت ہے، لازمی دینے کا وعدہ نہیں؛ cost، stock، active account، POS permission اور دوسری حقیقی پابندیاں برقرار ہیں۔

## 5 — مالک کا منظور کردہ واپسی طریقہ B

اصل: A560، B560، Extra20، **وصول1100**۔

1. Returns میں اصل bill کھولیں۔ A کی quantity واپس لکھیں۔ preview **560**؛ وضاحت بتائے گی کہ اصل item value پہلے، پھر bill کی باقی حد۔
2. الگ test data میں **Take the return** کریں۔ اصل product/sale-item قیمتیں ابھی بھی560 ہیں؛ رسید/stock/audit واپسی کا الگ record ہے۔
3. بعد میں B واپس آئے تو **صرف540** باقی قابلِ واپسی ہے۔ preview540 اور cap کی وجہ واضح ہونی چاہیے۔ گاہک کو پہلے بتائیں؛ دونوں کی درج قیمت برابر ہونے کے باوجود بعد کی واپسی کم ہے۔
4. دونوں ایک ہی return میں واپس ہوں تو بھی کل **1100**؛ آخری درج return line سے cap کا فرق کم ہوگا۔ یہ اصل فروخت کی لائنوں کا rewrite نہیں۔
5. واپس کی ہوئی quantity دوبارہ return نہیں ہوسکتی۔ کچھ bill value پہلے ہی ختم ہوچکی ہو تو باقی جائز quantity صفر refund کے ساتھ آسکتی ہے؛ screen کی وجہ پڑھیں۔
6. credit bill میں پہلے اصل باقی کھاتہ کم ہوتا ہے، cash صرف واقعی وصول اور ابھی واپس نہ ہوئی قابلِ واپسی رقم تک۔ Recovery کے بعد بھی یہی ledger حد ہے؛ pre-extra1120 کا جعلی cash refund نہیں۔
7. **No bill (PR)** الگ پہلے سے ادا شدہ provisional workflow ہے، اسے اصل invoice والی نئی واپسی نہ سمجھیں۔ بعد میں link کرنے سے پرانا دیا ہوا cash نہیں بدلتا/دوبارہ ادا نہیں ہوتا؛ Extra والے bill پر warning آتی ہے اور بعد کی invoice returns باقی final bill value تک محدود رہتی ہیں۔ بغیر bill نقد دینے سے پہلے اصل رسید تلاش کریں۔

## 6 — رسید، رپورٹ، منافع اور audit کیسے ملائیں؟

- Receipt/reprint میں **Before Extra Discount، Extra Discount، TOTAL** الگ؛ عام Discount receipt line چھپانے سے Extra کی لائن نہیں چھپتی۔
- Bills detail میں بھی یہی اصل/Extra/final amounts۔ محفوظ bill آج کی catalog قیمت یا discount default بدلنے سے دوبارہ نہیں بنتا۔
- overall Reports/day totals/profit/cash اصل **final1100** سے چلتے ہیں۔ مثال cost800 ہو تو اس bill کا gross profit300، 320 نہیں۔ یہ operating expenses کے بعد net profit کی تعریف نہیں۔
- چونکہ Extra کسی product کو نہیں دی، **per-medicine/top-product figures اور Product history، bill Extra سے پہلے** ہیں؛ انہیں final shop profit نہ کہیں۔ Reports category میں الگ منفی **Bill Extra Discount (not assigned to products)** row ہے تاکہ category مجموعہ final overall sales سے ملے۔ یہ کسی آئٹم کے ریٹ میں allocation نہیں۔
- customer profit aggregate سے متعلقہ bills کی الگ Extra ایک بار کم ہوتی ہے، ہر line پر دوبارہ نہیں۔
- **Books check**: unchanged item sum − الگ Extra = final total؛ header میں points/rounding سمیت یہی نتیجہ۔ Black box میں **sale_extra_discount** entry اصل/extra/final محفوظ کرتی ہے؛ عام Undo نہیں۔

## 7 — Windows قبولیت کی مختصر فہرست

- Desktop build صاف؛ **tools\RUN-TESTS.cmd → ALL GREEN —1300 of1300** کی اصل log، یہاں اس کا PASS دعویٰ نہیں۔
- اجازت بند، cap0،20/30 الگ افراد، cap سے ایک پیسہ زائد، منفی/خراب entry، بار بار Apply/Cancel/Remove اور pending entry پر F4۔
-1120−20، cash1100 اور1200→change100؛ split/credit/due/recovery، actual FEFO cost، points/rounding کے بعد Extra۔
- manual/item-discount lines unchanged، no merge/stack regression؛ no-stock ہر role block۔
- Park/Draft/restart/current user cap revocation؛ Clear/Post/Park کی نئی cart صفر۔
- B کی560 پھر540، دونوں اکٹھے1100، repeated quantity اور duplicate refusal، credit/full/partial، quarantine/restock اور linked PR history۔
- Receipt58/80mm، reprint، Bills، drawer/shift totals، report/day/profit/category، Books check اور audit ایک ہی paisa تک ملیں۔
- چھوٹی screen/DPI، search/scanner focus، arrows/Shift+arrows، F4 re-entry اور owned popup؛ حقیقی Windows تصاویر ہی ثبوت ہیں۔

## 8 — پسند نہ آئے تو محفوظ طور پر بند کیسے کریں؟

1. جاری cart میں **Remove Extra Discount** دبائیں؛ نئی payable دوبارہ دیکھیں۔
2. مالک متعلقہ شخص کی Extra اجازت بند کرکے **Save this person's pricing limits** کرے۔ محفوظ پرانے bills/returns کی تاریخ برقرار۔
3. پرانی exe چلانا یا پرانا backup اوپر ڈالنا feature بند کرنے کا طریقہ نہیں؛ اس سے نئی فروخت کا ڈیٹا ضائع ہوسکتا ہے۔ Restore صرف اصل recovery ضرورت، تصدیق شدہ backup اور واضح cutoff کے ساتھ۔
4. **Installer نہیں بنایا۔** اگلا next فیز۴ جامع review کے لیے ہے، Windows کی باقی آزمائش خود سے PASS نہیں ہوجاتی۔
