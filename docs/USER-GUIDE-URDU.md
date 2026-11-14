# Area11 — مکمل اردو استعمال نامہ

**کس کے لیے:** مالک، مینیجر اور کیشیئر۔ **جس کوڈ کے مطابق لکھا گیا:** v0.80.0 Ibis۔ یہ استعمال کی کتاب ہے، Windows پر کامیاب جانچ یا اصل دکان میں منظوری کا سرٹیفکیٹ نہیں۔ اس ورژن کے Windows ٹیسٹ اور عملی تصدیق باقی ہیں۔

**موجودہ POS فیز3:** مستقل search pane منتخب ہے۔ v0.78.0 میں owner-only ہر شخص کی الگ قیمت/رعایت حدیں، Cart کا محفوظ item editor اور profit/retail حساب code میں شامل ہیں۔ **موجودہ دکان کے آئندہ bills کا ابتدائی default اب profit ہے**؛ مالک Settings سے retail واپس کرسکتا ہے۔ پرانے محفوظ bills unchanged۔ الگ ایڈٹ شدہ لائن پر bill% دوبارہ نہیں؛ خرید سے نیچے کسی کو اجازت نہیں۔ **الگ sale-level Extra Discount اب فیز3 کے code میں شامل؛ مالک کا return طریقہ B: پہلے560 پھر باقی540، اصل item rates نہیں بدلتے**، جامع page/15-point audit فیز4 میں۔ Windows tests/build/عملی ثبوت باقی۔ [فیز2 کی آسان مشق اور واپسی](POS-PHASE2-TEST-URDU.md) ، [فیز3 استعمال/آزمائش/بندش](POS-PHASE3-TEST-URDU.md) اور [مکمل منصوبہ](POS-NEXT-REVIEW-PLAN-URDU.md) الگ ہیں۔

**فیز4 حصہ2 کی تازہ حیثیت:** v0.80.0 میں Settings کے App name & icon section اور Fields کی درست selection، Built-in fields کے موجودہ active owner، Doctors کے global profit switch اور Product history کے denied raw profit کی درستگی شامل۔ حصہ1 کے owner/reset/recovery/photo fixes برقرار۔ **جامع فیز4 ابھی جاری، مکمل PASS نہیں**؛26 routes/Settings کے8 contents/36 اسباق کا [register](POS-PHASE4-EVIDENCE.md)، [method-wise مزید شواہد/حدود](POS-PHASE4-ACCESS-REVIEW.md) اور [حصہ2 محفوظ آزمائش](POS-PHASE4-PART2-URDU.md) دیکھیں۔1337 C# cases لکھے ہیں، یہاں چلائے نہیں۔ مالی فیز2/3 کا منظورشدہ حساب برقرار ہے۔

**پڑھنے کا طریقہ:** حصہ اوّل میں صرف صفحات/اختیارات کی مختصر فہرست ہے۔ حصہ دوم میں انہی کی الگ، قدم بہ قدم تفصیل ہے۔ پہلے پورا خلاصہ دیکھیں؛ پھر جس کام کی ضرورت ہو اسی نمبر کی تفصیل پڑھیں۔ HTML فائل براؤزر میں کھلتی ہے، انٹرنیٹ کے بغیر بھی؛ براؤزر کے Print سے کاغذ یا PDF بنا سکتے ہیں۔

**زبان:** سکرین کے اصل انگریزی نام ساتھ رکھے ہیں تاکہ بٹن پہچان سکیں۔ مالک App words سے نام بدلے تو سکرین پر مختلف لفظ آ سکتا ہے۔ ہر شخص کو صرف اجازت والے صفحات دکھتے ہیں؛ صفحہ نظر نہ آنا لازماً خرابی نہیں۔

## حصہ اوّل — مکمل نقشہ اور اختیارات کا خلاصہ

یہاں طریقۂ کار نہیں، صرف یہ بتایا گیا ہے کہ کون سا اختیار کیا کرتا ہے۔ تمام تفصیل اگلے حصے میں الگ ہے۔

| سبق | صفحہ یا اختیار | مختصر کام |
|---|---|---|
| [01](#lesson-01) | Sign in / Forgot password? | اپنے نام سے داخلہ؛ مالک کے لیے الگ recovery word |
| [02](#lesson-02) | اوپر کی navigation، نام کا مینو، Dashboard | صفحہ بدلنا؛ دکان کی حالت؛ اپنی نشست ختم کرنا |
| [03](#lesson-03) | Shift (galla) | گلا کھولنا، اصل نقدی گننا، بند کرنا یا اگلے شخص کو دینا |
| [04](#lesson-04) | Products | تلاش، فلٹر، New product، Edit product یا قطار پر ڈبل کلک |
| [05](#lesson-05) | Product editor | نام، نمک، برانڈ، کیٹیگری، سپلائر، طاقت، پیک، ریٹ، ریک، محفوظ کرنا |
| [06](#lesson-06) | Catalogs / Managed units | کیٹیگری، کمپنی، نمک، برانڈ اور یونٹ کی فہرستیں سنبھالنا |
| [07](#lesson-07) | Photos / Barcodes / Product history | تصویر، cover، pack کا کوڈ اور پرانی خریدوفروخت |
| [08](#lesson-08) | Purchases | اصل ڈیلیوری سے سٹاک وصول، بیچ/میعاد، سپلائر کا واجب الادا اور ادائیگی |
| [09](#lesson-09) | Counter (POS) | چیز ڈالنا، مقدار/یونٹ، کسٹمر، ادائیگی، رعایت، بل محفوظ کرنا |
| [10](#lesson-10) | Counter availability | فی بل ایک وارننگ؛ سٹاک نہ ہو تو کسی کا بل نہیں؛ بغیر ریٹ صرف مالک/مینیجر |
| [11](#lesson-11) | Park / Drafts | ادھورا کام محفوظ؛ بعد میں کھولنا، پوسٹ کرنا یا draft ترک کرنا |
| [12](#lesson-12) | Bills / Fix wrong time… | پرانا بل تلاش، دوبارہ پرنٹ، preview، WhatsApp؛ مالک کی وقت درستگی |
| [13](#lesson-13) | Returns / Quarantine | اصل بل سے واپسی؛ رقم/ادھار درست؛ مشکوک مال الگ رکھنا |
| [14](#lesson-14) | No bill (PR) | بل کے بغیر واپسی کا مستقل ریکارڈ؛ بعد میں اصل بل جوڑنا |
| [15](#lesson-15) | Customers / Khata | کسٹمر، کھاتہ، وصولی، حد، statement اور ماہانہ بیان |
| [16](#lesson-16) | Udhaar recovery | پرانا ادھار، عمر کی فہرست، وصولی اور رسید |
| [17](#lesson-17) | Suppliers / SR | واجب الادا، Pay، supplier return، credit note یا نقد واپسی |
| [18](#lesson-18) | Doctors & reps | نسخے کے ڈاکٹر، کمپنی کے نمائندے، آنے کا دن، lifetime profit |
| [19](#lesson-19) | Reorder pad | کم سٹاک، minimum، ایک سپلائر کا آرڈر، WhatsApp متن/پرنٹ |
| [20](#lesson-20) | Write-off / Stock minus | خراب/ضائع مال کا نقصان؛ پرانے منفی سٹاک کا جائزہ |
| [21](#lesson-21) | Stock-take | اصل گنتی، فرق، صرف مالک کی حتمی منظوری |
| [22](#lesson-22) | Expenses / Owner drawings | دکان کا خرچ الگ؛ مالک کی نقدی یا مال الگ |
| [23](#lesson-23) | Reports / Tax report | تاریخ وار کاروبار؛ منافع؛ ماہانہ ٹیکس کا موجودہ ریکارڈ |
| [24](#lesson-24) | Alerts | یاد دہانی اور مالی/سٹاک مسئلے الگ؛ مسئلہ صرف مالک وجہ سے ختم کرے |
| [25](#lesson-25) | Black box / Books check | کس نے کیا بدلا؛ پرانی حالت؛ حساب کی صرف پڑھنے والی جانچ |
| [26](#lesson-26) | Excel / CSV | نئی فہرست import، export، workbook bulk update، قیمتیں بدلنا |
| [27](#lesson-27) | Backup & restore | تصویروں سمیت ZIP؛ خودکار شیڈول؛ پرانی حالت بحال کرنے کا خطرہ |
| [28](#lesson-28) | Settings: App name & icon / Shop | ایپ کی شناخت الگ؛ رسید والی دکان کی تفصیل الگ |
| [29](#lesson-29) | Settings: Printing / Printer & scanner | کاغذ، copies، auto-print، bulk، PDF اور اصل ہارڈویئر کی جانچ |
| [30](#lesson-30) | Settings: Sales rules | رعایت کی حد، cost lock، credit lock، profit، points، stock/expiry |
| [31](#lesson-31) | Settings: Staff & permissions / App words | لوگ، PIN/password، recovery word، اجازتیں اور صفحات کے نام |
| [32](#lesson-32) | Settings: Fields / Own fields | موجودہ خانہ required/optional؛ اپنے سوال اور ان کی ترتیب |
| [33](#lesson-33) | Startup / Protected close / Screen lock | Windows کے ساتھ آغاز، بندش کی اجازت، کام چھوڑنے کا ریکارڈ |
| [34](#lesson-34) | Cloud sync / Second counter | اختیاری cloud mirror؛ دوسرا کاؤنٹر فی الحال ملتوی |
| [35](#lesson-35) | Preview / WhatsApp / کاغذ | پرنٹ سے پہلے دیکھنا؛ PDF خود attach کرنا؛ دوبارہ پرنٹ ≠ نیا بل |
| [36](#lesson-36) | روزانہ کام، محفوظ مشق اور مسئلہ حل | صبح سے بندش تک ترتیب؛ غلطی، dash، کریش یا پرنٹر کے بعد اگلا قدم |

### Settings کے ہر گروپ کا مختصر مطلب

| گروپ | اختیارات اور مطلب |
|---|---|
| App name & icon | App name: کھڑکی/login/taskbar کا نام؛ Choose app icon…: اپنی .ico؛ Use default icon: اصل آئیکن |
| Shop | thumbnail size: تصویر کتنی بڑی؛ shop name/phone/address/footer: رسید کے الفاظ؛ discount line: کاغذ پر رعایت؛ idle sign-out: بےکار نشست؛ clock: وقت کی حالت؛ startup/lock: سبق 33 |
| Printing | 58/80 mm: چوڑائی؛ printer: کون سا پرنٹر؛ auto-print: بل کے بعد خود پرنٹ؛ cutter؛ bulk print: منتخب قطاریں؛ separate/A4: الگ رسیدیں یا خلاصہ؛ PDF folder؛ copies |
| Sales rules | owner کا default profit/retail basis؛ خرید سے نیچے فروخت مستقل بند؛ khata-limit lock؛ profit visibility؛ Done popup؛ loyalty on/rate/VIP threshold؛ tax on/rate؛ expiry کی تین حدیں؛ supplier return کی reminder/cutoff |
| Cloud sync | on/off، address، table، shop id، محفوظ key، automatic push کا وقفہ، Remove the saved key |
| Staff & permissions | شخص منتخب؛ account on/off؛ module کی ticks؛ role page defaults؛ ہر شخص کے manual/profit/retail caps اور one-bill override کا الگ Save؛ Add this person؛ Change it for the selected person؛ Save this recovery word؛ Photos |
| App words | ہر page/group کا دکان والا نام؛ Restore original words |
| Fields: Built-in | Form، Field، type، input، Required، Critical؛ Mark required؛ Mark optional… |
| Fields: Extra / Own fields | فارم، field name، stable key، جواب کی قسم، help، choices، required، ترتیب، put away/bring back |

### وہ باتیں جو خلاصے ہی میں یاد رکھیں

- **سٹاک ختم ہونے کی اطلاع صرف Counter کے لیے ہے؛ رسید/چٹ پر ایسی کوئی لائن نہیں۔**
- **صفر/کم سٹاک والا بل مالک یا مینیجر بھی نہیں بنا سکتا۔** پرانے منفی سٹاک کو گنتی سے درست کریں۔
- **Expense سٹاک نہیں بڑھاتا۔** سٹاک وصول کرنے کا صفحہ Purchases ہے۔
- **Cloud backup کا متبادل نہیں۔** مکمل حفاظتی نقل میں ڈیٹابیس اور تصاویر دونوں رکھیں۔
- **کوئی بھی سبق اصل دکان پر فرضی رقم یا فرضی سٹاک ڈال کر نہ آزمائیں۔** حقیقی ریکارڈ پر مشق مستقل مالی اندراج بنا سکتی ہے۔

## حصہ دوم — الگ، قدم بہ قدم تفصیل

یہاں سے عملی طریقے شروع ہیں۔ اس حصے کو مختصر خلاصے کے ساتھ نہ ملائیں۔ مثالوں کے نام/رقم صرف سمجھانے کے لیے ہیں، حقیقی کاروبار میں اصل چیزیں درج کریں۔

<a id="lesson-01"></a>

## سبق 01 — داخلہ اور بھولا ہوا پاس ورڈ

1. نصب ایپ کا Area11 آئیکن کھولیں۔ جو شخص کام کرے وہ **اپنا** نام اور Password or PIN لکھے، پھر Sign in۔ دوسرے کا نام استعمال کریں گے تو کارروائی اسی دوسرے شخص کے نام پر لکھی جائے گی۔
2. خالی user name مالک/admin کی لاگ اِن شناخت کے لیے استعمال ہو سکتا ہے؛ اپنے عملے کے لیے ان کا اصل اکاؤنٹ چنیں/لکھیں۔ نئی، غیر تبدیل شدہ تنصیب کی ابتدائی شناخت admin اور password area11 ہے؛ پہلے سے چلتی دکان پر اسے اپنا موجودہ پاس ورڈ سمجھ کر بار بار نہ آزمائیں۔
3. عملے کا PIN بھولے تو مالک اپنے اکاؤنٹ سے Settings → Staff & permissions میں نیا PIN دے۔ پرانا PIN پوچھنے کی ضرورت نہیں۔
4. مالک password بھولے تو **Forgot password?** کھولے؛ اس الگ ڈبے کے **Recovery word** میں اپنا مقررہ لفظ دے اور Sign in as owner دبائے۔ جب تک تبدیل نہیں کیا، فیکٹری لفظ area11 ہے۔ اسے password والے خانے کا متبادل نہ سمجھیں۔
5. داخلے کے بعد مالک اپنا password اور recovery word بدل دے۔ recovery word chat، تصویر یا رپورٹ میں نہ بھیجیں۔ غلط کوششوں پر کچھ دیر پابندی لگ سکتی ہے؛ مسلسل اندازے نہ لگائیں۔

Recovery سے داخلہ پرانی فروخت یا کھاتے کو reset نہیں کرتا۔ دونوں راز بھول جائیں تو اصل ڈیٹابیس مٹانا حل نہیں؛ بیک اپ محفوظ کرکے مدد لیں۔

<a id="lesson-02"></a>

## سبق 02 — صفحے کھولنا اور Dashboard

1. اوپر پہلے کام کا گروپ منتخب کریں: SELL، STOCK، CUSTOMERS & SUPPLIERS، MONEY یا SYSTEM۔ نیچے اسی گروپ کے اجازت والے صفحات آتے ہیں۔
2. Counter کی اجازت ہو تو آغاز وہاں ہوتا ہے؛ اجازت نہ ہو تو Dashboard۔ Dashboard کی الگ عام tab کے بجائے اوپر ایپ کے نام/wordmark سے وہاں جا سکتے ہیں۔
3. Dashboard پر آج کے بل، فروخت، منافع، ادھار، مصنوعات، کم سٹاک، منفی سٹاک، میعاد اور گلے کے کارڈ دیکھیں۔ متعلقہ کارڈ متعلقہ صفحہ کھول سکتا ہے؛ اجازت یا profit visibility کے سبب کچھ چیزیں چھپی ہو سکتی ہیں۔
4. گھنٹی سے Alerts؛ اپنے نام کے مینو سے Settings یا Sign out۔ **Sign out ایپ بند کرنا نہیں**، صرف اس شخص کی نشست ختم کرنا ہے۔ گلا خود بخود بند نہیں ہوتا۔
5. Refresh تازہ فہرست لاتا ہے۔ خالی صفحے کا پیغام پڑھیں: ریکارڈ نہ ہونا، فلٹر تنگ ہونا اور اجازت نہ ہونا الگ باتیں ہیں۔

<a id="lesson-03"></a>

## سبق 03 — صبح گلا کھولنا، رات گننا

1. MONEY → Shift (galla) کھولیں۔ پہلے سے کھلا drawer ہو تو اسے دوبارہ نیا سمجھ کر نہ کھولیں؛ پچھلے شخص/مالک سے اصل نقدی گنوا کر بندش یا handover کریں۔
2. Cash you start with میں **واقعی موجود نقدی** لکھیں، فروخت کا ہدف نہیں۔ پچھلی گنتی سے فرق آئے تو صحیح وجہ لکھیں۔ Open the shift دبائیں۔
3. دن میں Cash bills، کھاتے کی نقد وصولی، refunds، supplier payments، expenses اور owner cash drawings سے Expected in the drawer بنتا ہے۔ Card/online رقم گلے میں نہیں پڑی، اسے نقدی میں شامل نہ گنیں۔
4. بندش پر نوٹ اور سکے گن کر Counted cash لکھیں۔ کم یا زیادہ ہو، چاہے ایک روپیہ، وجہ لکھیں؛ Close the shift۔ صرف حساب ملانے کے لیے فرضی Expense نہ ڈالیں۔
5. شخص بدل رہا ہو تو وصول کرنے والا staff چنیں، اصل counted cash دیں اور Hand over and print the slip۔ یہی رقم اگلے گلے کی opening float بنتی ہے؛ فرق پچھلی نشست پر محفوظ رہتا ہے۔ دونوں سے slip چیک/دستخط کروائیں۔
6. Slip دوبارہ Print یا WhatsApp کی جا سکتی ہے۔ History، Drawer differences اور عملے کے short/over الگ دیکھیں؛ زائد رقم کو کمی چھپانے کے لیے استعمال نہ کریں۔

گلا بند ہو تو بل نہیں بنے گا۔ Counter کے Open a galla now سے یہی صفحہ کھولیں؛ cash permission نہ ہو تو مجاز شخص کو بلائیں۔

<a id="lesson-04"></a>

## سبق 04 — Products میں صحیح دوا تلاش کرنا

1. STOCK → Products میں نام، نمک، طاقت، pack، barcode، brand، rack، category یا company لکھیں۔ کئی لفظ ہوں تو ہر لفظ ملنا ضروری ہے؛ بہت لمبی تلاش خالی آئے تو کم لفظ آزمائیں۔
2. طاقت کا الگ فلٹر مثلاً 50 ml، اور supplier کا فلٹر موجود ہے۔ Products والا supplier فلٹر preferred supplier یا اصل purchase history سے چیز دکھاتا ہے؛ اسے نئی خریداری یا payable نہ سمجھیں۔
3. primary category/company کی قطار والی جگہ سے بھی فلٹر لگ سکتا ہے۔ Clear filter سے اسے ہٹائیں۔ Stock بیچوں سے آتا ہے؛ اس grid میں عدد بدلنا سٹاک وصولی نہیں۔
4. New product سے الگ editor کھلے گا؛ پرانی چیز منتخب کرکے Edit product یا قطار پر ڈبل کلک۔ 50 ml اور 100 ml جیسے مختلف variants کو ایک ہی چیز سمجھ کر edit نہ کریں۔
5. پوری تصویر کے لیے Photo کھولیں؛ اصل معلومات کی تبدیلی editor میں کریں، پھر Products پر Refresh۔

<a id="lesson-05"></a>

## سبق 05 — نئی چیز اور اس کے پیک/ریٹ

1. Name واضح لکھیں۔ Salt / formula / generic اور Brand میں موجود نام کا link منتخب کریں یا اصل متن دیں۔ New salt/New brand سے نیا نام؛ **Unlink** صرف رابطہ ہٹاتا ہے، لکھا متن رکھنے کے لیے ہے۔ ایک چیز کے دو ہجے بنانے سے بچیں۔
2. Barcode، Rack / shelf اور Room / almari درست درج کریں۔ پہلی category حساب کی primary ہے؛ اضافی tags صرف تلاش کے لیے۔ اضافی tags منتخب کرنے سے پہلے primary منتخب کریں۔ category خالی اور tags بھی خالی رہ سکتے ہیں۔
3. Company اور اختیاری Preferred supplier منتخب کریں۔ Clear preferred رابطہ ہٹاتا ہے، پرانی خریداری نہیں۔ Manage catalogs… سے فہرستیں؛ Manage units… سے اصل stock units۔
4. **Strength / volume صرف وصف ہے، سٹاک تبدیل کرنے کا فارمولا نہیں۔** مثال: 50 ml کی 10 injections کے ڈبے میں stock کی اصل گنتی 10 injections ہے، 500 ml نہیں۔ ایسی مختلف طاقت/pack کی الگ مصنوعات رکھیں۔
5. Pack size / variant label بھی صرف پہچان/تلاش کا متن ہے۔ اصل فارمولا الگ ہے: 1 box = کتنے inner packs، 1 inner pack = کتنے base units۔ مثال 2 strips فی box اور 10 tablets فی strip تو ایک box = 20 tablets۔
6. Cost، Retail، VIP اور Doctor ریٹ ایک base unit کے روپے لکھیں؛ Amount میں روپے ہیں۔ Counter منتخب box/strip کے لیے pack formula سے ریٹ نکالتا ہے۔ Reorder level **base units** میں ہے۔ غلط pack formula سے مقدار اور منتخب unit کا ریٹ دونوں متاثر ہوں گے؛ پہلی رسید سے پہلے لازماً ملائیں۔
7. Save product دبائیں۔ Built-in required خانے خالی ہوں تو ناموں والا popup آ سکتا ہے؛ Save anyway کا مطلب جان بوجھ کر نامکمل چیز محفوظ کرنا ہے۔ اس سے Counter کی missing-price/stock پابندی ختم نہیں ہوتی۔
8. Cancel / close کے وقت unsaved changes کا سوال آئے تو اپنی ضرورت کے مطابق واپس جائیں یا چھوڑیں۔ Remove selected (history kept) چیز کو عام استعمال سے الگ کرتا ہے، پرانی فروخت نہیں مٹاتا۔

### چار مثالوں سے تین خانوں کا فرق

| چیز | Strength / volume | Pack label صرف پہچان | Pack formula اور stock |
|---|---|---|---|
| 500 mg گولی | 500 mg | 10 × 10 tablets | base tablet، 10 strips/box × 10 tablets/strip = 100 گولیاں |
| 5 gram انجیکشن | 5 gram | 10 injections | base injection، 10 × 1 = 10 انجیکشن؛ gram کو stock کی تقسیم نہ بنائیں |
| 100 ml شربت، پوری بوتل بیچنی ہے | 100 ml | 100 ml bottle | base bottle، 1 × 1 = 1 بوتل؛ 100 الگ ml فروخت فرض نہ ہوں |
| 5 gram ساشے | 5 gram | 20 sachets | base sachet، 20 × 1 = 20 ساشے |

موجودہ فارم پر غیر گولی مصنوعات کے لیے بھی strips / box اور tablets / strip کے عمومی labels آتے ہیں؛ ان کا حساب ضرب کا ہے، ہر انجیکشن کے واقعی پٹی میں ہونے کا دعویٰ نہیں۔ اصل pack دیکھ کر formula رکھیں۔ 1 × 1 اور base injection کا مطلب ایک box میں ایک injection؛ strength 5 gram ہونے سے اسے پانچ یا زیادہ injections نہیں بناتا۔ Pack label میں صرف 1 لکھنا پہچان واضح نہیں کرتا؛ label کو بہتر لکھنے سے اصل formula خود نہیں بدلتا۔ اس سبق کے لیے موجودہ کاروباری product کی تعریف/سٹاک خودکار تبدیل نہیں کیے گئے۔

**اہم:** product بنانے سے عام سٹاک خود نہیں آتا۔ اصل مال Purchases سے وصول کریں؛ حقیقی جسمانی فرق Stock-take سے درست کریں۔ Photo نہ ہونا چیز محفوظ کرنے کو لازمی طور پر نہیں روکتا۔

<a id="lesson-06"></a>

## سبق 06 — فہرستیں اور Managed units

1. Product editor میں Manage catalogs… کھولیں۔ Categories، Companies، Salts اور Brands کی اپنی tabs ہیں۔ New، Name، متعلقہ Parent/Company اور Order لکھ کر Save کریں۔ چھوٹا Order اوپر دکھاتا ہے۔
2. Category میں زیادہ سے زیادہ دو درجے ہیں؛ parent فلٹر اس کے children بھی دکھا سکتا ہے۔ primary category ہی مالی رپورٹ کا حصہ طے کرتی ہے، tags کو رقم دوبارہ جمع کرنے کا ذریعہ نہ بنائیں۔
3. نمک/برانڈ کا نام بدلیں تو جڑی ہوئی چیزوں کے labels بدلتے ہیں؛ جس product پر صرف آزاد typed text ہے وہ خود دوبارہ نام نہیں پاتا۔
4. Put away کسی catalog entry کو عام انتخاب سے ہٹاتا اور موجودہ links کو متعلقہ اصول کے مطابق الگ کرتا ہے؛ مصنوعات/بل/بیچ حذف نہیں ہوتے۔ Bring back پر پرانے links خود بخود دوبارہ نہیں جڑتے۔ بعد میں متعلقہ چیز کھول کر رابطہ دیکھیں۔
5. Manage units… میں canonical نام اور ترتیب دیں۔ Whole stock quantities only اصل گنتی کے لیے ہے، مثلاً injection ٹکڑوں میں نہیں بکتا۔ ml کی strength کو fraction of injection نہ سمجھیں۔
6. استعمال میں موجود unit کو put away نہیں کر سکتے۔ rename سے موجودہ labels بدل سکتے ہیں؛ صرف راستہ نکالنے کے لیے unit کا مطلب نہ بدلیں۔ catalog/unit میں تبدیلی مالک/مینیجر کرے۔

<a id="lesson-07"></a>

## سبق 07 — تصاویر، بارکوڈ اور چیز کی تاریخ

1. Photos of this medicine…، فہرست کی تصویر، یا کسٹمر/سپلائر/staff کے Photos… سے gallery کھولیں۔ Add a photo… اصل فائل کی نقل محفوظ کرتا ہے۔ Take a photo (camera) Windows Camera کھولتا ہے؛ تصویر محفوظ کرکے واپس Add a photo سے منتخب کریں۔
2. صحیح active تصویر منتخب کرکے Set as cover؛ Open full size سے بڑی دیکھیں۔ Put away صرف چھپاتا ہے، فائل/تاریخ مٹاتا نہیں؛ Bring back سے واپس۔ Missing file کا مطلب فہرست میں ریکارڈ ہے مگر تصویر کی فائل موجود نہیں۔
3. Barcodes of this medicine… میں code سکین کریں اور Printed on میں **صحیح pack** (box/strip/base وغیرہ) چنیں۔ Add this barcode۔ ایک box code کی beep ایک box ڈالے گی، ایک tablet نہیں۔
4. غلط code کو Put away، درست کو فعال کریں۔ صرف مالک/مینیجر یہ تعریفیں بدلیں۔ قیمت بدلنے کے لیے barcode دوبارہ بنانے کی ضرورت نہیں۔
5. History of this medicine… سے کب، کس سے خریدا، کس کو بیچا اور کیا ریٹ تھا دیکھیں۔ یہ دیکھنے کا صفحہ ہے؛ پرانی تجارت کو یہاں edit نہ سمجھیں۔
6. تصاویر سمیت نقل کے لیے Backup میں Include the photos رکھیں۔ Cloud اور عام Excel export تصاویر نہیں لے جاتے۔

**Product history میں منافع:** اجازت/global switch بند ہو تو explicit profit screen/export میں hidden اور Core کے returned sale-profit fields بھی mask؛ اصل sale/item snapshots unchanged۔ خرید کے الگ rows/cost-bearing دوسرے راستے ابھی مکمل privacy audit سے پاس نہیں؛ اسے ہر inference کے خلاف حفاظت نہ سمجھیں۔

<a id="lesson-08"></a>

## سبق 08 — Purchases میں حقیقی ڈیلیوری وصول کرنا

1. STOCK → Purchases میں supplier منتخب کریں؛ نہ ہو تو New supplier، اصل نام/فون/agency اور متعلقہ extra fields لکھ کر Add supplier۔
2. صحیح product variant تلاش کریں، Unit اور Quantity منتخب کریں۔ کاغذ والا Batch number درج کریں۔ اس فارم کے Cost اور Retail **ایک base unit** کے روپے ہیں، منتخب box/strip کے پورے pack کے نہیں؛ Quantity/Unit سے تعداد base units میں تبدیل ہو کر cost سے ضرب ہوتی ہے۔ مثال: 20 tablets کے ایک box کی کل خرید 200 روپے ہو تو Cost میں فی tablet 10 روپے، Quantity میں 1 اور Unit میں box ہوگا؛ پورے box کے 200 کو Cost میں ڈالیں گے تو کل 4000 بن سکتا ہے۔ لائن ڈالنے کے بعد total سے اصل invoice لازماً ملائیں۔
3. Expiry printed strip سے لکھیں: 2027-06، 06/27 یا 2027-06-30؛ month/year اور calendar بھی موجود ہیں۔ خالی میعاد کا مطلب no expiry ہے۔ **اس فارم میں غلط تاریخ لکھنے پر Add line رکتی ہے؛ پیغام پڑھ کر اصل printed تاریخ درست کریں۔** بغیر میعاد کی لائن کو خودکار طور پر درست تاریخ لگ جانے کے برابر نہ سمجھیں؛ save کے بعد status بھی پڑھیں۔
4. مفت bonus ہو تو Sample / bonus line منتخب کریں؛ تعداد الگ درج کریں، cost صفر ہوتا ہے۔ خریدی ہوئی اور bonus تعداد کو دو مرتبہ paid stock نہ لکھیں۔ Add line سے delivery lines میں ڈالیں؛ غلط آخری لائن Remove last۔
5. Purchase total supplier کے اصل bill سے ملائیں۔ Paid now صرف ابھی دی ہوئی رقم ہے؛ باقی payable بنے گا۔ Paid how میں cash، online یا card کا **حقیقی** طریقہ دیں؛ cash گلے سے نکلتا ہے۔
6. ایک ڈیلیوری میں دو طریقے ہوں تو اس فارم میں ایک ہی Paid how ہے: مثلاً 1000 کی خریداری، 300 cash اور 200 online ادا ہوئے—Paid now میں 300، cash سے purchase محفوظ کریں، پھر Suppliers میں اسی supplier کو Pay کے ذریعے 200 online الگ درج کریں۔ باقی 500 واجب الادا رہے گا۔ 500 کو cash لکھنا گلے کو غلط کرے گا۔
7. Save purchase (PINV) کے بعد stock، payable اور ادا شدہ رقم دیکھیں۔ Save as draft ابھی وصولی پوسٹ نہیں کرتا۔ WhatsApp last invoice… صرف ابھی محفوظ invoice کے لیے ہے۔

Supplier statement پر پہلے سے ادا شدہ رقم دوبارہ Pay نہ کریں۔ واقعی محفوظ ہوئی delivery کو refresh کے بعد دوبارہ نئے bill کے طور پر نہ لکھیں۔

<a id="lesson-09"></a>

## سبق 09 — Counter پر بل بنانا

1. گلا کھلا ہو؛ Counter کے اوپر دوا تلاش کریں یا barcode سکین کریں۔ خالی search پر پوری catalog فہرست نہیں آئے گی؛ نام لکھیں تو Cart کے ساتھ مستقل چھوٹے پینل میں نتیجوں کے cards آئیں گے؛ ہر card میں تصویر/نام، طاقت، pack، stock، retail اور salt/rack دیکھیں۔ صحیح طاقت/pack منتخب کرکے Unit، Quantity اور Add to cart؛ کامیاب add پر نتائج اور search صاف، scanner focus واپس، مگر پینل کی جگہ قائم رہے گی۔ Esc یا Hide صرف matches چھپاتا ہے؛ query بدلیں تو دوبارہ آئیں گے۔ name کے Enter سے guessed medicine خود نہیں ڈلتی، درست barcode Enter اپنی pack unit کے مطابق کام کرتا ہے۔
2. Cart میں Item، Unit، Qty اور Rate Rs دیکھیں۔ Rate اسی لائن کی unit کا روپے والا ریٹ ہے۔ غلط چیز Remove؛ پورا غیر محفوظ cart ختم کرنا ہو تو Clear۔
3. واک اِن فروخت کے لیے کھاتہ درکار نہ ہو تو customer ضروری نہیں؛ **credit کے لیے درست customer** منتخب کریں۔ New customer… سے نام/فون شامل کرنے پر cart اپنی جگہ رہتا ہے۔ نسخے کا Doctor اختیاری الگ انتخاب ہے۔
4. Cart row پر double-click یا منتخب row پر Enter سے **Item price / discount** کھولیں۔ mode **normal** (bill discount)، **manual** (آخری منتخب-unit ریٹ)، یا **discount** (اس item کا فیصد، profit/retail اپنی پسند)۔ اجازت ہر شخص کی مالک مقرر کرتا ہے؛ صرف manager role کافی نہیں۔ manual یا الگ discount والی line پر bill% دوبارہ نہیں، خواہ ریٹ بڑھایا ہو۔ Apply پہلے Core quote/حد/cost چیک کرتا ہے؛ Cancel نافذ نہیں؛ Reset to normal دوبارہ bill discount میں۔ Cart کی Pricing column حالت بتاتی ہے؛ grid کا عام rate cell جان بوجھ کر read-only، اصل editor یہ owned window ہے۔
5. **Bill Discount % کے اوپر profit/retail منتخب دکھتا ہے۔** profit میں actual FEFO batch cost اور فروخت کے فرق سے فیصد؛ retail میں پوری فروخت سے۔ مثال cost200/sale300/10%: profit290، retail270۔ normal lines ہی bill% میں؛ ہر لائن کے اپنے manual/profit/retail فیصلے الگ۔ نئی عام line موجودہ bill% لے گی۔ ہر شخص کے تین caps اور edit/override اجازتیں Settings → Staff میں مالک Save کرتا ہے۔ خرید سے نیچے final sale کسی role کو نہیں، زیادہ رعایت انکار (خاموش کم فیصد نہیں)۔ Loyalty کے points بعد میں اپنی caps/cost کے ساتھ؛ Rs10 rounding فعال ہو تو الگ round-off بھی دیکھیں۔ **Extra Discount اس پورے حساب کے بعد الگ؛ نیچے قدم دیکھیں**۔
6. Payment میں حقیقی طریقہ چنیں۔ Split میں دوسری method اور اس کا Amount، باقی cash؛ cash/credit حصے سکرین پر ملائیں۔ Cash received وہ نقدی ہے جو واقعی ہاتھ میں آئی؛ Change واپس کی جانے والی نقدی ہے، فروخت کا منافع نہیں۔
7. Subtotal، Discount، TOTAL، Cash part، On credit اور Change دیکھیں۔ v0.78.0 میں Customer، Payment، Discount %، Total، Cash received، Give back اور Complete sale مستقل نیچے ہیں۔ Cart مرکزی جگہ کا بڑا حصہ لیتا ہے؛ search pane اس کے ساتھ اپنی جگہ رکھتا ہے۔ Extra Discount/Doctor/points/extra answers کے لیے More / split، جبکہ نئی customer entry کے لیے New customer دبائیں۔ Split چنیں تو اس کی detail خود کھلے گی۔ Complete sale یا اصل F4 شارٹ کٹ استعمال کریں؛ rate edit ہو تو F4 پہلے اس edit کو مکمل کرتا ہے۔ error آئے تو بل محفوظ سمجھ کر رقم/مال دوبارہ نہ چلائیں؛ پہلے error اور Bills میں حالت دیکھیں۔
8. retail بنیاد پر bill% 5 سے زیادہ ہو تو **save سے پہلے ہر bill پر** تصدیق۔ No پر محفوظ نہیں، Yes سے user cap یا cost پابندی نہیں ٹوٹتی۔ کامیابی پر invoice/status ملے گا۔ Settings → Sales rules میں success popup آن ہو تو الگ درمیان کی کھڑکی کھلے گی: ✓، invoice، بل، Cash received اور بہت بڑا GIVE THIS CASH BACK۔ مثال: بل 270، نقد 500، واپس 230 روپے۔ Done / Enter / × صرف کھڑکی بند کرتے ہیں؛ دوبارہ فروخت یا پرنٹ نہیں کرتے۔ اس کے بعد اگلا customer شروع کریں۔ Auto-print الگ اختیار ہے؛ پرنٹر کو بھیجنا کاغذ واقعی نکلنے کا ثبوت نہیں۔ Print last bill again صرف نقل نکالتا ہے۔ Cash received خالی ہو تو پرانے exact-cash قاعدے کے مطابق رقم فرض ہوتی ہے اور popup صاف لکھتا ہے exact amount assumed؛ online/card/credit کے لیے cash صفر ہوتا ہے۔

**مثال:** 300 روپے کا cash bill، وصول 500 تو Change 200۔ credit کی رقم کو نقد وصولی میں گننے سے گلا غلط ہو گا۔

### فیز1 — ماؤس کے بغیر Counter کیسے چلائیں؟

**یہ keyboard سہولت فیز1 میں آئی؛ فیز2 میں item editor اور نیا حساب، فیز3 میں الگ Extra Discount اور منظورشدہ B واپسی شامل؛ Windows جانچ باقی ہے۔**

1. گلا کھلا ہو اور Counter کھولیں؛ سرچ میں کرسر آنا چاہیے۔ نام لکھیں؛ خالی سرچ پر تمام دوائیں نہیں آئیں گی۔ صرف نام لکھ کر Enter سے اندازے والی دوا Add نہیں ہوتی۔ اصل barcode لکھ/سکین کرکے سرچ کے اندر Enter دیں تو اسی code کا موجودہ scan راستہ چلتا ہے۔
2. نتیجے منتخب کرنے کے لیے سرچ میں **↓** دبائیں۔ فوکس نتیجے کے خانے پر آئے گا، تصویر کے بٹن پر نہیں۔ **↑↓** سے دوا/طاقت/pack چنیں؛ **Enter** سے وہی Add ہوگی۔ اسی row پر **double-click** بھی Add کرتا ہے۔ تصویر والا بٹن صرف gallery کھولتا ہے؛ خالی جگہ/heading پر double-click سے Add نہیں۔
3. کامیاب Add پر سرچ صاف اور فوکس واپس؛ اگلی دوا تلاش کریں۔ Quantity غلط ہو تو پیغام پڑھیں؛ انکار کو کامیاب Add نہ سمجھیں۔ scanner عام keyboard جیسا لکھتا ہے؛ کسی دوسرے خانے میں کرسر ہو تو پہلے سرچ میں واپس آئیں، ہر input سے scan خود پہچاننے کا وعدہ نہیں۔
4. **Tab** سے سرچ → Unit → Quantity → Add جیسے دستیاب controls میں آگے، **Shift+Tab** سے پیچھے۔ Unit خانے میں **↑↓** سے unit/strip/box منتخب کریں؛ متن یا رقم میں **←→** عام حروف کا کرسر چلائیں۔
5. **Shift+↓ یا Shift+→** اگلے حصے اور **Shift+↑ یا Shift+←** پچھلے حصے میں لے جاتے ہیں۔ ترتیب: **سرچ/Unit/Quantity → سرچ نتائج → Cart → کھلی More details یا Parked carts → Customer/Payment/Discount → Cash/Complete sale → نیچے کے actions**۔ بند حصے اور خالی/چھپے نتائج خود skip ہوتے ہیں۔ آخری حصے سے آگے دوبارہ شروع نہیں؛ وہیں رکیں گے۔ Shift+تیر اب section shortcut ہے؛ پورا متن منتخب کرنے کے لیے **Ctrl+A** استعمال کریں۔
6. حصے کے اندر عام تیر متعلقہ list/grid/خانے کا کام کرتے ہیں۔ نیچے action buttons پر تیر اسی حصے میں پڑوسی بٹن تک، **Enter** اس بٹن کا کام؛ کی بورڈ سے منتخب action نمایاں رنگ میں دکھتا ہے۔ Tab مکمل متبادل ہے۔ Print automatically checkbox پر Space یا Enter سے حالت بدلے؛ خود Complete sale نہیں۔
7. **Esc** کھلی Unit/Customer/Payment فہرست کو پہلے بند کرے۔ سرچ نتائج میں ہوں تو نتائج چھپنے پر فوکس سرچ میں واپس؛ query بدلیں تو دوبارہ matches۔ معمول کے دوسرے input میں رقم/کسٹمر لکھنے سے فوکس زبردستی سرچ میں واپس نہیں آئے گا۔
8. Refresh اب نیچے actions میں ہے۔ More / split یا Parked carts کھولیں تو Shift+تیر سے اس دکھائی دینے والے حصے تک جائیں یا Tab سے controls چنیں؛ بند کریں تو اضافی حصہ navigation میں شامل نہیں۔
9. **F4** اصل Complete sale ہی ہے اور بل محفوظ کر سکتا ہے؛ محض keyboard آزمانے کے لیے اسے نہ دبائیں۔ Cart پر Enter/double-click اب **Item price / discount** کھولتا ہے؛ Apply صرف cart بدلتا ہے، bill محفوظ نہیں کرتا۔

### فیز1 کی محفوظ مشق

بغیر بل محفوظ کیے 5–10 دوائیں Add کریں، Unit/Quantity/Cart/Customer/Cash/actions میں جائیں؛ رقم لکھ کر دیکھیں کہ فوکس نہیں چھنتا۔ Clear سے غیر محفوظ cart ختم کریں۔ حقیقی shop میں فرضی Complete sale/F4 نہ کریں۔ Windows پر چھوٹی سکرین، DPI، اصلی scanner اور keyboard کی تصدیق باقی؛ مسئلہ ہو تو version، بٹن، کیے گئے قدم اور تصویر دیں۔


### بل کے آخر میں الگ Extra Discount — پوری مثال

1. مالک پہلے Settings → Staff & permissions → شخص → **This person's pricing limits** میں **May give separate Extra Discount** اور روپے کی حد دے، پھر **Save this person's pricing limits**۔ خود مالک کے لیے بھی یہ اجازت مقرر کرنا ہوگی۔ عام profit/retail% caps الگ رہیں گی۔
2. پوری cart، rate/item discounts، bill%، points/rounding کا حساب پہلے ہونے دیں۔ فرض کریں **Before Extra Discount1120**۔ سب lines manual/fixed ہوں تب بھی الگ Extra چل سکتا ہے؛ یہ ان کے ریٹ بدلنے کا راستہ نہیں۔
3. **More / split** میں **Extra Discount (Rs for the whole bill)** میں20 لکھیں۔ صرف لکھنے سے لاگو نہیں ہوگا؛ **Apply Extra Discount** دبائیں۔
4. owned popup میں **1120، Extra20، final1100** دیکھ کر Yes؛ No سے سابقہ applied رقم برقرار۔ footer میں Extra−20 اور Total1100 دیکھیں۔ cap20 ہو تو30 سے انکار، خاموش20 نہیں۔ دوبارہ20 Apply کرنے سے20 ہی،40 نہیں؛ یہ پوری رقم replace ہے۔
5. **Cash received1100** دیں، صرف حقیقی فروخت پر Complete sale/F4۔ رسید، reprint اور Bills میں pre-extra1120/Extra20/final1100 الگ، due0۔ Extra کے بغیر کم cash لینے سے فرق خود سے معاف نہیں ہوگا؛ مناسب customer/credit منتخب کریں یا پوری نقدی وصول کریں۔
6. Apply/Remove سے product rate/quantity/پہلے کی line discount/bill% نہیں بدلتے۔ **Remove Extra Discount** صرف یہ رقم ہٹاتا ہے۔ اگر textbox کی نئی رقم Apply نہیں کی تو Complete sale/Park/Save draft روکے گا؛ پہلے Apply یا Remove۔ Clear/Post/Park سے اگلی live cartExtra0، اسی parked/draft کو کھولنے پر اس کی Extra بحال اور موجودہ cap دوبارہ چیک۔
7. final payable actual allocated خرید کی مجموعی لاگت سے کم نہیں ہوسکتا، مالک بھی نہیں؛ پہلے ہر line کا اپنا cost guard بھی برقرار۔ لہٰذا cap20 کا مطلب ہر bill پر لازماً20 دینے کی اجازت نہیں۔
8. بند کرنا ہو تو جاری cart میں Remove؛ مالک اس شخص کا Extra tick بند کرکے الگ Save کرے۔ محفوظ پرانے bills unchanged۔ پرانی exe/backup اوپر ڈالنا بندش کا طریقہ نہیں۔ محفوظ عملی مشق، errors اور B واپسی [فیز3 نامہ](POS-PHASE3-TEST-URDU.md) میں۔

<a id="lesson-10"></a>

## سبق 10 — Not in stock اور بغیر قیمت کی وارننگ

1. پہلی بار cart میں stock کم/ختم یا catalog retail missing ہونے پر Counter availability popup پڑھیں، OK کریں۔ **اسی live bill میں دوبارہ حساب/typing پر popup بار بار نہیں آئے گا۔** یہ وارننگ غائب ہونے سے رکاوٹ ختم نہیں ہوتی۔
2. stock کم ہو تو مقدار کم کریں، غلط unit ٹھیک کریں یا اصل stock وصول کریں۔ **مالک/مینیجر بھی صفر یا ناکافی stock کا بل نہیں بنا سکتے۔** ایک ہی product کی الگ لائنیں مجموعی stock استعمال کرتی ہیں؛ expired-only stock بھی sellable نہیں۔
3. stock موجود لیکن retail درج نہیں تو مالک کی rate-edit اجازت والا شخص item editor کے manual mode میں مثبت درست فروخت ریٹ دے۔ reference قیمت نہ ہو تو فیصد کمی نہیں ناپ سکتے، مگر actual batch cost/stock پابندی برقرار۔ اجازت نہ ہو تو خود ریٹ لکھ کر رکاوٹ عبور نہیں؛ مستقل حل product میں صحیح Retail محفوظ کرنا ہے۔
4. Clear، کامیاب sale، cart park یا دوسرے draft/cart کو load کرنے پر نئے live bill کی warning یاد دوبارہ شروع ہوتی ہے؛ آخری چیز ہٹانے سے cart خالی ہو تب بھی اگلی شروعات نئی ہے۔
5. **رسید/چٹ پر availability کا لفظ، اضافی سطر یا اوپر کا نشان نہیں آتا۔** قیمت کو popup میں دوبارہ نہیں دکھایا گیا؛ معمول کی Rate والی جگہ دیکھیں۔ سٹاک نہ ہونے کا ناکام bill کوئی فروخت نہیں۔

<a id="lesson-11"></a>

## سبق 11 — Park، مشترکہ drafts اور ادھورا کام

1. گاہک انتظار کر رہا ہو تو نیچے مختصر toolbar کا Park cart یا Save draft استعمال کریں۔ یہ پرانے Park this cart for later / Save as draft کے وہی commands ہیں؛ Open drafts اور Reprint بھی اسی مستقل پٹی میں ہیں۔ Parked carts کے بٹن سے فہرست کھولیں؛ فہرست ہر وقت Cart کی جگہ نہیں لیتی۔ اس وقت کوئی sale/stock deduction/رقم وصولی پوسٹ نہیں ہوتی۔
2. Parked carts کا Bring back یا Open drafts → Open on the page سے کام واپس لائیں۔ واپس لانے پر stock/ریٹ/قواعد دوبارہ چیک ہوں گے؛ park کرنے سے stock reserve ہونے کی ضمانت نہیں۔
3. purchase کا بھی Save as draft ہے؛ Open drafts سے اس کی delivery واپس لیں۔ draft مشترکہ ہے: متعلقہ اجازت والا شخص اسے کھول/پوسٹ کر سکتا ہے۔ اپنے ادھورے کام کو راز یا ذاتی lock نہ سمجھیں۔
4. Post now حتمی document بنانے کی کوشش ہے، صرف کھولنا نہیں۔ Discard/Throw away draft ترک کرتا ہے، پہلے سے posted document کو الٹتا نہیں۔
5. Post was interrupted جیسا پیغام ہو تو پہلے اصل Bills/purchase record/status چیک کریں؛ کہیں document بن چکا ہو تو دوبارہ post نہ کریں۔ draft history محفوظ ہونے کا مطلب یہ نہیں کہ editor میں ابھی کی ہر غیر محفوظ typing بھی محفوظ ہے۔

<a id="lesson-12"></a>

## سبق 12 — Bills، دوبارہ رسید اور غلط وقت

1. SELL → Bills میں Today، 7 days، 30 days یا From/To منتخب کریں۔ bill number، customer name/phone اور Paid by فلٹر؛ Search۔ Clear فلٹر صاف کرتا ہے۔
2. bill منتخب کریں؛ اشیا، مقدار، subtotal، discount، **Before Extra Discount، Extra Discount، final Total**، tax/round-off اگر ہوں، Paid، Due، returns اور Money that came in دیکھیں۔ انتخاب پڑھنے سے نیا bill نہیں بنتا۔
3. Print this bill again ایک نقل؛ Print selected… متعدد منتخب بل؛ Preview اصل کاغذ کی عبارت/چوڑائی دکھاتا ہے؛ WhatsApp… کا PDF خود attach کرنا ہوتا ہے۔ returns والے حصے سے اسی return کی رسید نکال سکتے ہیں۔
4. مالک کے لیے Fix wrong time… ہے۔ پہلے اصل Windows clock درست رکھیں اور time server کا جواب دستیاب ہو۔ From/To دے کر List bills؛ Wrong time اور Correct time کی پیش کش غور سے ملائیں۔
5. Fix every bill above فہرست والے bills اور متعلقہ زمانی ریکارڈ ایک ساتھ درست کرنے کی کارروائی ہے؛ انتخاب پڑھے بغیر نہ دبائیں۔ اصل اور درست وقت کا ثبوت ریکارڈ میں محفوظ رہتا ہے۔ وقت بدلا ہونے سے تاریخ وار reports میں bill دوسری تاریخ پر آ سکتا ہے، رقم کی نئی فروخت نہیں بنتی۔
6. انٹرنیٹ/time source نہ ملنے پر batch وقت درستگی دستیاب نہیں سمجھی جائے؛ اندازے سے تاریخ یا ڈیٹابیس کی فائل خود نہ بدلیں۔

<a id="lesson-13"></a>

## سبق 13 — اصل بل والی Returns اور Quarantine

1. SELL → Returns میں Bill number → Find bill۔ نمبر نہ ہو تو No bill number? Find it سے فون/نام/دوا/تاریخ تلاش کریں؛ صحیح bill کھولیں۔ یہ تلاش ہے، بل کے بغیر refund بنانے کا راستہ نہیں۔
2. Sold، Returned اور Can return دیکھیں۔ Coming back میں حقیقی واپس آئی مقدار لکھیں؛ فروخت سے زیادہ واپس نہیں ہو سکتی۔ Refund، Reason اور ضرورت کا Note پڑھیں/لکھیں۔
3. قابل فروخت صحیح مال Back on the shelf now؛ فیصلہ باقی یا مشکوک مال Quarantine۔ خراب/expired چیز فوراً shelf پر واپس نہ ڈالیں۔
4. Total refund، Cash out اور Khata reduced ملائیں؛ Take the return (RET)۔ credit والے bill میں پہلے ادھار کم ہو سکتا ہے؛ ہر refund لازماً پوری نقد ادائیگی نہیں۔ وصول شدہ رقم اور باقی مقدار کی حدیں لاگو ہیں۔
5. Quarantine tab میں مجاز شخص Q-number والی چیز چن کر Back in stock، Damaged / expired یا Sold on کا اصل نتیجہ درج کرے۔ ہر نتیجے کا وقت/شخص/نوٹ مستقل رہتا ہے۔
6. پہلے سے محفوظ return کو دوبارہ نیا return نہ لکھیں۔ رسید Bills کے returns حصے سے بھی مل سکتی ہے۔

### Extra والے بل کی جزوی واپسی — مالک کا منتخب طریقہ B

A560+B560=1120، Extra20، وصول1100۔ پہلے واپس آنے والی A کی اصل محفوظ رقم **560**؛ بعد میں B آئے تو صرف باقی **540**۔ یہ proportional550/550 نہیں، اور اصل sale-item rates/discounts نہیں بدلتے۔ Returns preview میں Extra اور باقی cap کی وجہ پڑھیں، گاہک کو پہلے سمجھائیں، پھر Take the return۔ دونوں ایک request میں ہوں تو بھی کل1100؛ cap کا فرق آخری درج return line سے کم ہوگا۔ واقعی وصول cash سے زائد نقد واپسی نہیں؛ credit کی صورت میں باقی کھاتہ کم ہوگا، فرضی cash نہیں۔ ایک بار واپس کی ہوئی quantity دوبارہ نہیں۔ الگ test data میں اس مثال کی مشق کریں؛ اصل دکان پر فرضی return نہ بنائیں۔

<a id="lesson-14"></a>

## سبق 14 — اصل بل ہاتھ میں نہ ہو: No bill (PR)

1. No bill (PR) عام refund کا آسان متبادل نہیں؛ اس میں اصل bill کی فوری تصدیق نہیں ہوتی۔ product، حقیقی واپس مقدار اور Unit ڈالیں؛ customer name/phone، واقعی دی گئی رقم، وجہ/نوٹ لکھیں۔
2. Save and give the slip سے PR نمبر بنتا ہے، stock/مال اور refund کا الگ ریکارڈ بنتا ہے۔ slip گاہک کو دیں اور بعد میں اصل bill لانے کا طریقہ واضح کریں۔ غیر تصدیق شدہ رقم میں خاص احتیاط کریں۔
3. Waiting for a bill میں PR منتخب کریں؛ Print this slip نقل ہے۔ مالک/مینیجر Attach the real bill میں INV نمبر دیں، یا فون/نام/دوا/تاریخ سے Find bills کرکے Use the selected bill؛ پھر Attach this bill۔
4. اصل bill جوڑنے سے وہی stock اور refund دوسری بار نہیں لگتے۔ alert کا رنگ/حالت بدل سکتی ہے مگر مالک کو وجہ لکھ کر معاملہ بند کرنا پڑتا ہے۔
5. واقعی duplicate/غلط PR ہو تو مالک/مینیجر وجہ دے کر Cancel this entry and take the stock back۔ اگر مطلوبہ stock باقی نہیں تو cancellation رک سکتی ہے؛ یہ نیا منفی stock بنانے کا راستہ نہیں۔
6. Linked/cancelled entries ہمیشہ تاریخ میں رہتی ہیں؛ slip پھاڑنے سے اندراج ختم نہیں ہوتا۔

<a id="lesson-15"></a>

## سبق 15 — Customers، کھاتہ اور وصولی

1. CUSTOMERS & SUPPLIERS → Customers میں نام/فون تلاش؛ Only with udhaar صرف مقروض۔ + New سے الگ کسٹمر بنائیں۔ ایک فون کو دو کھاتوں میں بانٹنے سے بچیں۔
2. Edit میں name، phone، category اور مجاز شخص کے لیے Credit limit۔ **Balance کو براہ راست نئی رقم بنا کر نہیں لکھتے**؛ bill، receipt اور return سے کھاتہ چلتا ہے۔ limit صفر کا مطلب کوئی مقررہ حد نہیں، صفر ادھار کی اجازت نہیں۔
3. کسٹمر منتخب کرکے Khata book کی Debit/Udhaar، Paid اور Balance قطاریں پڑھیں۔ Lifetime اور Profit اجازت/visibility کے تابع ہیں۔
4. پیسے ہاتھ/بینک میں آ چکے ہوں تو Money received میں رقم، How میں صحیح طریقہ اور Note؛ Receive۔ ادھار سے زیادہ وصولی رد ہو سکتی ہے؛ اضافی customer advance کا فرضی اندراج یہاں نہ کریں۔
5. Statement پورا بیان؛ Month میں YYYY-MM دے کر Month statement؛ Receipt (last collection) آخری وصولی کی نقل۔ متعدد منتخب کسٹمر کے Print selected، Preview، WhatsApp statement یا WhatsApp nudge دستیاب ہیں۔
6. Photos شناخت میں مدد کے لیے ہیں۔ دکھائی گئی ادھار کی رقم بدلنے کے بجائے غلطی کی اصل entry تلاش کریں اور مجاز correction کا طریقہ اپنائیں۔

<a id="lesson-16"></a>

## سبق 16 — Udhaar recovery میں پرانا ادھار

1. Customers & suppliers کے recovery حصے میں TOTAL UDHAAR اور 0–30، 31–60، 61–90، 90+ دن والے خانے پڑھیں۔ یہ رقم کی عمر ہے، جرمانہ یا خودکار سود نہیں۔
2. Only over two months اور Find سے فہرست محدود کریں۔ Carried in، Last paid اور Oldest سے سمجھیں کہ واجب الادا کہاں سے ہے؛ بہت پرانا opening balance بھی الگ ہو سکتا ہے۔
3. کسٹمر چنیں، اصل Amount received اور Method لکھیں؛ Receive and print سے PAY نمبر اور رسید بنتی ہے۔ ادائیگی پرانے بقایا کے اصول کے مطابق لگتی ہے۔
4. Print slip again نئی وصولی نہیں۔ رقم غلط لکھ دی ہو تو اسے چپکے سے edit/delete کرنے کا راستہ نہ ڈھونڈیں؛ مالک اصل ریکارڈ دیکھ کر درست corrective entry کروائے۔

<a id="lesson-17"></a>

## سبق 17 — Suppliers، ادائیگی اور SR

1. Suppliers میں supplier منتخب کریں؛ Only with money to pay فلٹر صرف واجب الادا دکھاتا ہے۔ Account، Purchases، Credit notes اور Money paid سے payable کا سبب ملائیں۔
2. اصل ادائیگی کے لیے رقم، How اور Note دیں، Pay۔ مالک/مینیجر ادائیگی یا supplier return کر سکتے ہیں۔ purchase کے Paid now کو دوبارہ supplier Pay میں نہ دہرائیں۔
3. Statement، Print selected، Preview، WhatsApp statement یا WhatsApp note سے حساب بھیجیں۔ bank سے دی ہوئی رقم کو cash کہنا گلے کو غلط کرتا ہے۔
4. Return goods (SR) میں اصل product/batch چنیں، Quantity اور Unit، Add this batch to the tray۔ expired batch کا متعلقہ quick button بھی موجود ہے؛ tray، Value اور اصل مال ملائیں۔
5. Reason لکھیں۔ عام SR سے stock نکلتا اور payable credit note سے کم ہوتا ہے۔ **The supplier gave the money in cash** صرف تب tick کریں جب واقعی نقد واپس لیا—یہ credit note کے ساتھ دگنی رقم لینے کے لیے نہیں۔
6. Save the return (SR) سے پہلے رقم اور tray دیکھیں؛ منفی stock کی اجازت نہیں۔ واپسی کی cutoff یاد دہانی supplier کے معاہدے کی جگہ خود فیصلہ نہیں کرتی۔

<a id="lesson-18"></a>

## سبق 18 — Doctors & reps

1. کسی doctor کو Customers میں Category = Doctor کے ساتھ رکھیں؛ اسی سے prescription doctor کی فہرست بنتی ہے۔ doctor کا customer کھاتہ اور bill پر نسخہ لکھنے والے doctor کا انتخاب الگ مقصد رکھ سکتے ہیں۔
2. Doctors & reps کے متعلقہ tabs/حصے کھولیں۔ Counter پر اصل prescription doctor منتخب کریں؛ بعد میں اس کے Bills، Sales، Profit اور Last bill دیکھیں۔
3. کمپنی منتخب کرکے rep کا نام، فون، Visits on لکھیں؛ مالک/مینیجر Save the rep کرے۔ company کی غلط entry پر کسی دوسری کمپنی کا نمائندہ نہ لگا دیں۔
4. Customer lifetime profit پوری تاریخ کے حساب کا اشارہ ہے، وصول شدہ نقدی نہیں۔ returns اور cost اثر ڈالتے ہیں، visibility بند ہو تو منافع چھپ سکتا ہے۔

**منافع کی موجودہ حد:** Doctors کا explicit profit اب global `profit.showMargins` اور اس شخص کی profit permission دونوں سے مشروط ہے۔ global OFF ہو تو مالک/الگ grantee کے لیے بھی profit hidden، اصل sales/counts اپنی permission کے مطابق برقرار؛ ON سے اصل محفوظ snapshot کا عدد واپس۔ یہ ہر cost-bearing راستے سے حساب اخذ کرنے کی ناممکن ہونے کی ضمانت نہیں؛ مکمل service access review ابھی جاری ہے۔

<a id="lesson-19"></a>

## سبق 19 — Reorder pad

1. STOCK → Reorder pad پر Refresh۔ صفحہ کہے switched off تو پہلے متعلقہ setting/مالک سے حالت چیک کریں۔ supplier اور Find سے چیزیں محدود کریں۔
2. Stock اور Minimum ملائیں۔ minimum اصل base units میں رکھیں؛ منتخب product کا Keep at least → Save minimum۔ یہ نہ purchase ہے، نہ stock correction۔
3. مطلوبہ چیزیں tick کریں؛ Order میں کتنی base units مانگنی ہیں لکھیں۔ Tick all/Untick all صرف فہرست کا انتخاب ہیں۔
4. **ایک پیغام میں ایک supplier۔** Products کے preferred-supplier فلٹر اور یہاں کے order supplier کو ایک ہی قاعدہ نہ سمجھیں: reorder فہرست supplier کو آخری حقیقی خریداری سے دکھاتی ہے۔ supplier غلط لگے تو اصل history دیکھیں؛ کسی دوسرے agent کو mixed order نہ بھیجیں۔
5. Supplier mobile اور Your note دیں؛ Copy text، Open WhatsApp یا Print order pad۔ پیغام بننے سے stock نہیں بڑھتا؛ delivery آنے پر Purchases کریں۔

<a id="lesson-20"></a>

## سبق 20 — Write-off اور Stock minus

1. Write-off میں صحیح medicine، واقعی موجود batch، quantity/unit اور Reason منتخب کریں؛ loss purchase cost پر شمار ہوتا ہے۔ Expired، broken، lost جیسے حقیقی اسباب لکھیں۔
2. Write this off سے پہلے quantity دوبارہ دیکھیں۔ Save کے بعد یہ عام editable note نہیں؛ غلطی کی correction الگ وجہ والی کارروائی سے ہو گی۔ نقصانی مال پھر سے موجود نہ سمجھیں۔
3. سٹاک سے زیادہ write-off رکے گا؛ owner bypass نہیں۔ supplier کو بھیجا مال SR سے جائے، اسی چیز پر دوبارہ write-off نہ کریں۔
4. Stock minus پر پرانے منفی balances، کتنے دن، cost value اور review دیکھیں۔ Monthly review میں جو پایا وہ لکھ کر Mark reviewed کریں۔ **Review سے quantity نہیں بدلتی۔**
5. حقیقی shelf count لے کر Stock-take میں مالک سے correction منظور کروائیں۔ محض فہرست صاف کرنے کے لیے فرضی purchase یا خود SQL سے صفر نہ کریں۔

<a id="lesson-21"></a>

## سبق 21 — Stock-take: اصل گنتی سے حساب ملانا

1. counting کے وقت کاروباری حرکت روک کر سب کو بتائیں۔ Stock-take میں Room خالی تو پوری دکان، ورنہ متعلقہ کمرہ؛ Open ST session۔ ایک جاری گنتی کے درمیان خریدوفروخت ہو تو جسمانی گنتی دوبارہ ملانا پڑ سکتی ہے۔
2. Print count sheet سے شیلف گنیں؛ ہر medicine/batch کی اصل تعداد درج کریں۔ Counted quantity میں درست Unit اور Note دے کر Save this count۔ Clear this count دوبارہ گننے کے لیے ہے، shelf سے چیز مٹانے کے لیے نہیں۔
3. Book، Counted، Diff اور Value دیکھیں؛ Only rows with a difference مدد دیتا ہے۔ کوئی چیز shelf پر ہے مگر book میں نہیں تو Found on the shelf… سے اصل product منتخب کریں؛ apply پر اس کے لیے GINTI batch بن سکتا ہے۔
4. صرف manager counting/cancel کی مجاز حدود میں کام کرے؛ **Apply this stock-take صرف مالک**۔ ہر row، shortage/excess اور cost اثر دیکھ کر ایک بار apply کریں۔ counted quantity کو حقیقی غیر منفی گنتی رکھیں۔
5. Apply کے بعد book count کے مطابق corrections اور audit بنتا ہے؛ دوبارہ apply نہیں۔ Cancel کے لیے وجہ لازم ہے۔ Earlier counts سے تاریخ ملتی ہے۔ غلط count کو صحیح دکھانے کے لیے بار بار sessions نہ بنائیں۔

<a id="lesson-22"></a>

## سبق 22 — Expenses اور مالک کی ذاتی نکاسی

1. Expenses میں Money the shop spent کے لیے Category، What was it، Amount اور Note لکھیں؛ Save the expense۔ cash/shift permission درکار ہے۔ **اس سے stock نہیں آتا، supplier account نہیں بدلتا۔**
2. Manage categories… سے مالک کی ترتیب کے مطابق running/stock labels سنبھالیں۔ نام میں stock لکھا ہونے سے یہ purchase نہیں بنتا۔ کرایہ، بجلی، چائے جیسے اصل اخراجات الگ عنوان سے رکھیں۔
3. Owner took money or goods میں Kind منتخب کریں۔ Cash taken اصل نقدی کم کرتا ہے؛ Goods میں medicine/batch، quantity/unit سے shelf کم اور purchase cost پر drawing شمار ہوتی ہے، cash نہیں نکلتا۔
4. ایک ہی نکاسی Expense اور Drawing دونوں میں مت لکھیں۔ goods drawing سٹاک سے زیادہ نہیں ہو سکتی۔
5. What went out میں entry منتخب کرکے Print voucher۔ محفوظ entry edit/delete نہیں؛ غلطی سامنے ہو تو مالک ریکارڈ دیکھ کر واضح corrective entry کرے۔

<a id="lesson-23"></a>

## سبق 23 — Reports اور Tax report

1. Reports میں Today، Last 7 days، Last 30 days، This month یا متعلقہ تاریخیں دیں؛ Refresh۔ Bills، Items sold، Sales، Discount، Average bill، Cash received، On khata اور Refunded کو الگ مطلب سے پڑھیں۔
2. profit میں sale کے وقت کا cost استعمال ہوتا ہے؛ موجودہ cost بدلنے سے ہر پرانا bill اسی نئے cost پر دوبارہ نہیں بنتا۔ cash in drawer، sales، profit اور net ایک ہی عدد نہیں۔
3. Day by day میں خاموش دن بھی آ سکتے ہیں؛ Category، Top 10 by quantity/profit، shelf/dead-stock متعلقہ فہرستیں کاروباری جائزے کے لیے ہیں۔ returns کے بعد net figures دیکھیں۔ Print report یا WhatsApp سے نقل لے سکتے ہیں۔
4. Tax report میں مہینہ Previous/Next/This month سے بدلیں؛ bill پر واقعی charged tax اور روزانہ کی جمع دکھتی ہے۔ یہ نیا tax لگانے والا بٹن نہیں۔ refund کے tax کی علیحدہ تقسیم خودکار نہیں؛ accountant کے لیے refunds بھی پڑھیں۔
5. مالک کی موجودہ ہدایت **sales tax نہیں لینا** ہے؛ Settings میں tax محض سبق دیکھنے کے لیے آن نہ کریں۔ Copy for the accountant متن کی نقل ہے، قانونی filing خودکار نہیں۔

**Extra Discount کی رپورٹ:** overall/day sales اور profit آخری payable سے؛ customer profit سے الگ Extra ایک بار کم۔ product history/top medicines کے item amounts/profit **bill Extra سے پہلے** دکھتے ہیں کیونکہ Extra کسی product کو تقسیم نہیں کی گئی۔ category report میں الگ منفی **Bill Extra Discount (not assigned to products)** row ہے؛ category مجموعہ final sales سے ملے گا۔ مثال1120−Extra20، cost800: overall profit300، نہ320۔ product figures کو final overall profit نہ سمجھیں۔

<a id="lesson-24"></a>

## سبق 24 — Alerts: یاد دہانی اور اصل مسئلہ

1. گھنٹی → Alerts، Refresh۔ رقم، stock، expiry، PR یا گلے کے فرق کا سبب پڑھیں؛ ہر سرخ چیز خرابی والا dialog نہیں۔
2. عام reminder الگ رکھنا مجاز عام شخص کر سکتا ہے؛ مالی/stock مسئلے کو ختم کرنا مالک کا کام اور وجہ ضروری ہے۔
3. گلے کی alert ہو تو Open the Shift page سے اصل counted cash/وجہ/شخص ملائیں۔ PR ہو تو پہلے bill link یا درست cancellation کا عمل مکمل کریں۔
4. مالک End an alert میں اصل وجہ لکھ کر End this alert۔ alert بند کرنا **رقم یا stock کی correction نہیں**۔ اصل مسئلہ واپس آئے یا نئی حرکت ہو تو alert دوبارہ آ سکتی ہے۔
5. سابق alerts/history سے پڑھے بغیر بار بار خاموش نہ کریں؛ یہ audit trail کا حصہ ہیں۔

<a id="lesson-25"></a>

## سبق 25 — Black box اور Books check

1. Black box میں Search، Module، Action، Lines اور When فلٹر؛ Today/7/30/All time۔ رقم یا چیز کا اختلاف ہو تو متعلقہ entry منتخب کریں۔
2. Earlier state — Before this line اور Recorded new state — After this line اس وقت ریکارڈ ہوئی عبارت ہیں، ضروری نہیں آج کا live balance ہوں۔ محفوظ correction والی ہدایت پڑھیں۔ Save CSV سے جائزے کی نقل۔
3. نئے Extra والے bill میں **unchanged item sum − الگ Extra = final total**؛ header میں points/rounding کے بعد بھی یہی رقم۔ Black box کی **sale_extra_discount** entry میں اصل/Extra/final دیکھیں۔ یہاں **عام Undo نہیں**؛ صرف پرانی حالت دکھنے سے transaction واپس نہیں ہوئی۔ بغیر سمجھے پوری shop restore کرنا ایک غلط رقم کا معمولی حل نہیں۔
4. Books check → Check again سے گیارہ حسابی identities کا صرف پڑھنے والا جائزہ۔ Says، Rows say اور Gap کو document کے ساتھ دیکھیں؛ Save this list سے ماہر کو ثبوت دیں۔
5. Books check خود repair نہیں کرتا۔ فرق آئے تو اصل bill/return/payment اور backup محفوظ کریں؛ DB فائل پر براہ راست رقم مت لکھیں۔

<a id="lesson-26"></a>

## سبق 26 — Excel / CSV: نئی فہرست اور bulk تبدیلی

1. import سے **پہلے photos-inclusive backup الگ جگہ رکھیں**۔ Excel / CSV → Import میں مطلوبہ entity منتخب، Save a blank template۔ اصل headings برقرار رکھ کر matching sheet بھریں؛ existing UTF-8 CSV بھی نئے records کے لیے قبول ہو سکتی ہے۔
2. Choose the file… کے بعد preview کی ہر error/skipped/new حالت پڑھیں۔ import کا حتمی بٹن صرف صحیح فائل/تعداد ملانے کے بعد دبائیں؛ Choose file بذات خود کامیاب import کا ثبوت نہیں۔ duplicate کو نیا product بنا کر پرانا ادھار تقسیم نہ کریں۔
3. opening stock/balances صرف متعلقہ import اصول کے مطابق ایک بار؛ پہلے سے وصول مال دوبارہ opening stock نہ بنائیں۔ موجودہ records بدلنے کے لیے Bulk update والا راستہ استعمال کریں۔
4. Export سے Medicines and stock، Customers and udhaar، Suppliers and payable کی .xlsx نقل نکلتی ہے۔ Excel کی تنصیب لازمی نہیں۔ تصاویر ان فائلوں میں نہیں ہوتیں۔
5. Bulk update → Download your data؛ اسی workbook میں Products، Customers، Suppliers، Categories، Companies، Batches، Settings اور Instructions sheets ہوتی ہیں۔ row IDs/headings برقرار رکھیں۔ **خالی cell = کوئی تبدیلی نہیں؛ `__CLEAR__` = صرف جہاں اجازت ہو وہاں صاف کرنا۔**
6. Stock، balances، secrets اور numbering counters محفوظ/protected ہیں؛ workbook کو منفی سٹاک یا PIN بدلنے کا راستہ نہ سمجھیں۔ Include Settings changes الگ confirmation مانگتا ہے، JSON value کی قسم برقرار رکھنی ہے۔
7. Choose edited workbook… کے بعد old/new preview دیکھیں؛ database بدل گئی ہو تو نیا download/preview چاہیے ہو سکتا ہے۔ Apply these changes حتمی مرحلہ ہے۔
8. Prices tab میں Percent یا Rupees، متعلقہ منتخب records اور Change the purchase cost too صرف حقیقی cost تبدیلی پر؛ Preview → پرانے/نئے ریٹ ملائیں → Apply these prices۔ below-cost سے رد قطار کو نظرانداز کرکے کامیاب نہ سمجھیں۔

<a id="lesson-27"></a>

## سبق 27 — Backup اور Restore: سب سے اہم حفاظت

1. Backup & restore → Include the photos رکھیں؛ Back up now یا Back up as… سے ZIP لیں۔ Open the folder/Refresh سے فائل دیکھیں۔ تازہ ZIP کو USB یا دوسرے محفوظ کمپیوٹر پر نقل کریں؛ اسی disk کی دوسری directory مکمل آفت سے حفاظت نہیں۔
2. Automatic backup میں on/off، Every (hours)، Keep this many؛ Save the schedule۔ صرف automatic فائلوں کی retention الگ ہے؛ دستی backup خود سنبھالیں۔ schedule ہونے سے پروگرام بند رہنے کے دوران ہر وقت backup بننے کی ضمانت نہیں۔
3. اصل data عموماً C:\ProgramData\Area11 میں رہتا ہے؛ اگر data location بدلی ہو تو موجودہ حالت/backup page دیکھیں۔ کھلی DB کی اکیلی .db فائل نقل کرنا مکمل ZIP backup کے برابر نہیں، متعلقہ journal/تصاویر رہ سکتی ہیں۔
4. Restore پورے موجودہ حساب کی جگہ **منتخب پرانی حالت** لاتا ہے۔ آج کے نئے بل/وصولیاں پرانے backup میں نہ ہوں تو restore کے بعد وہاں نہیں دکھیں گے۔ ایک غلط bill ٹھیک کرنے کے لیے یہ معمول کا Undo نہیں۔
5. صرف مالک، کام روک کر، درست تاریخ/فائل دیکھ کر Restore this backup… منتخب کرے، تنبیہ پڑھے، پھر Restore now۔ پروگرام پہلے موجودہ حالت کی recovery copy بنانے کی کوشش/جانچ کرتا ہے؛ ناکامی کا پیغام ہو تو آگے محفوظ سمجھ کر نہ بڑھیں۔
6. بحالی کے بعد records، bills، balances اور تصاویر ملائیں۔ Last restore and recovery میں پہلے کی حفاظتی ZIP محفوظ رکھیں۔ restore fail ہو تو فائلیں delete یا بار بار الگ backups نہ چلاتے رہیں؛ پیغام اور recovery location نوٹ کرکے مدد لیں۔
7. نئی مشین پر مکمل جانچ **الگ test environment** میں کریں۔ محض Area11 کی دوسری program folder بنا لینے سے الگ test data نہیں بنتا؛ دونوں ایک ہی shop data استعمال کر سکتی ہیں۔

<a id="lesson-28"></a>

## سبق 28 — ایپ کی شناخت اور Shop details الگ

**فیز4 حصہ2 کی source درستگی:** App name & icon اب shell کا اپنا section0، Shop1 سے Fields7 تک اصل contents وہی؛ Fields کی selection اب App words پر نہیں رکے گی۔ Windows پر تمام8 buttons/keyboard/چھوٹی screen کی عملی تصدیق باقی۔ section بدلنے سے half-typed values نہیں مٹنی چاہئیں؛ خرابی ہو تو [حصہ2 آزمائش](POS-PHASE4-PART2-URDU.md) کے مطابق بتائیں۔

1. Settings → App name & icon: App name ایک سے چالیس حرف؛ Choose app icon… میں .ico، زیادہ سے زیادہ 2 MB؛ Use default icon واپس اصل۔ پھر Save settings۔
2. یہ نام/icon window، login اور taskbar کے لیے ہیں۔ نصب executable اور پہلے سے بنے desktop shortcuts کا اپنا icon الگ رہ سکتا ہے۔ app icon بدلنے سے رسید پر لوگو نہیں لگتا۔
3. Shop tab: Product photos کا thumbnail size صرف Counter/Products کی دکھائی جانے والی تصویر کا حجم۔ Shop name، Phone، Address، Note at the bottom of the receipt **کاغذ والی دکان** ہیں۔
4. Show the discount line on the receipt صرف رسید کی line دکھانے/چھپانے کے لیے ہے؛ بل میں حقیقتاً دی گئی رعایت یا حساب خود ختم نہیں کرتا۔
5. idle sign-out منتخب کریں؛ Never والے انتخاب کے باوجود sign-in سے بارہ گھنٹے کی حد برقرار ہے۔ session ختم ہونے سے drawer خود نہیں گنا جاتا؛ اگلا شخص Shift page پر سنبھالے۔
6. Where the time comes from میں server/PC clock کی حالت پڑھیں۔ network نہ ملے تو PC clock استعمال ہو سکتی ہے؛ Windows کا وقت خود درست رکھیں۔ time settings بدلنے سے پہلے سبق 12 دیکھیں۔ عمومی تبدیلیوں کے بعد Save settings اور کامیابی کا status دیکھیں۔

<a id="lesson-29"></a>

## سبق 29 — Printing اور اصل پرنٹر/سکینر

1. Settings → Printing میں 58 mm (32 characters) یا 80 mm (48 characters) اپنے paper کے مطابق۔ Printer name خالی ہو تو Windows default؛ Copies ایک، دو یا تین۔ printer driver بھی Windows میں موجود ہو۔
2. Print the receipt automatically after each sale آن تو محفوظ بل کے بعد پرنٹ؛ بند ہو تو Counter کا Print last bill again۔ Cut the paper صرف cutter والے printer کے لیے۔ پرنٹ fail سے محفوظ sale خود cancel نہیں ہوتی۔
3. The Print button prints every ticked row اور bulk format سمجھ کر رکھیں: Separate receipts الگ slips؛ One A4 list صرف summary، ہر مکمل bill کی الگ رسید نہیں۔ Preview میں width دیکھیں۔
4. Prepare a PDF file with every WhatsApp send اور Folder for prepared PDFs منتخب کریں؛ blank folder سے عام exports جگہ استعمال ہو سکتی ہے۔ اس سے PDF خود WhatsApp میں attach نہیں ہوتی۔
5. SYSTEM → Printer & scanner میں پرنٹر چنیں؛ Open Windows printers سے driver/default درست؛ Print test slip۔ ruler، آخری حروف، paper width اور copies دیکھیں۔ یہ کوئی کاروباری bill نہیں۔
6. scanner والے خانے میں click کرکے حقیقی pack scan کریں؛ Check this code۔ اس کی unit بھی Counter کی محفوظ مشق میں چیک کریں، صرف digits آنا کافی نہیں۔
7. What you checked کی اصل ticks دیں؛ کوئی چیز نہ چلی ہو تو Note میں لکھیں، Save the result۔ جھوٹی tick hardware کو درست نہیں کرتی؛ note audit میں جاتا ہے، stock نہیں بدلتا۔

<a id="lesson-30"></a>

## سبق 30 — Sales rules: کیا بدلتا ہے، کیا نہیں

1. **Default bill discount basis (owner only)** میں profit یا retail؛ عام Save۔ پہلی update کے بعد آئندہ bills کا default profit ہے، retail پر واپس انتخاب ممکن۔ موجودہ user کو one-bill override کی اجازت ہو تو Counter پر صرف اس bill کے لیے popup کے بعد بدل سکے؛ نیا bill Settings default۔ محفوظ draft/held basis اپنی واپس آئے، موجودہ permissions دوبارہ چیک۔
2. **Purchase cost protection — always ON**: اصل batch خرید سے نیچے کسی کو، حتیٰ کہ مالک، اجازت نہیں۔ پرانا checkbox/lock-off اب راستہ نہیں۔ ہر شخص کی edit/manual reduction/profit%/retail%/override limits **Staff & permissions** کے الگ panel میں، یہاں role-wide cashier/manager caps نہیں۔
3. Stop a bill that crosses the customer's khata limit بھی قابل انتخاب ہے؛ customer کی limit صفر ہو تو مقررہ ceiling نہیں۔ مالک exempt ہے؛ manager override موجودہ manager-override configuration کے مطابق ہے—صرف ظاہری متن دیکھ کر ہر manager کو لازماً مجاز نہ سمجھیں۔
4. Show profit and margins بند ہو تو منافع مختلف صفحات سے چھپتا ہے، مالک سے بھی؛ کاروبار کے اصل اعداد نہیں مٹتے۔ Done / invoice / cash-change popup صرف کامیابی کے بعد کی اطلاع ہے، پرنٹ یا transaction کا متبادل نہیں۔
5. Give loyalty points آن/آف؛ Rupees spent for 1 point سے earning rate؛ Points that make a customer VIP سے threshold۔ points خرچ ہونے والے خانے کا 1 point = 1 روپیہ الگ اصول ہے۔ مالک نے موجودہ loyalty rate برقرار رکھا ہے۔
6. Tax آن/فیصد کا اختیار موجود ہے مگر **اس دکان کی ہدایت sales tax نہیں**۔ اسے آن کرنے سے آئندہ bills اور receipt پر اثر پڑ سکتا ہے؛ بلااجازت تبدیل نہ کریں۔
7. Stock: نئے کام میں negative stock کی checkbox/owner bypass نہیں۔ یہ sale، write-off، goods drawing، SR اور PR cancellation پر بھی لاگو ہے۔
8. Expiry Level 1 سب سے دور، Level 3 قریب کی warning؛ دن منطقی ترتیب میں رکھیں۔ یہ approaching-expiry رنگ بدلتے ہیں؛ گزر چکی expiry والی batch الگ expired ہے اور عام sellable batch نہیں۔
9. Supplier returns کے Start reminding اور Last return day cutoff میں مثلاً 180 اور 90 ہوں تو 120 دن باقی والی چیز یاد دہانی میں آتی ہے، واپسی آخری 90 دن سے پہلے مطلوب ہے۔ supplier کا اصل معاہدہ بھی ملائیں۔ Save settings کے بعد status دیکھیں۔

<a id="lesson-31"></a>

## سبق 31 — عملہ، اجازتیں، راز اور App words

**نیا الگ pricing panel (صرف مالک):** Settings → Staff & permissions میں شخص منتخب کریں، **This person's pricing limits** میں rate-edit اجازت، retail-reference سے manual reduction%، profit اجازت/حد، selling-total اجازت/حد اور one-bill override tick/حدیں دیں۔ الگ **May give separate Extra Discount** اور **Extra Discount limit (Rs per whole bill, not per click)** بھی یہیں ہیں؛ مالک سمیت اس نئی Extra اجازت کا کوئی خودکار grant نہیں، اپنی بھی واضح حد save کریں۔ **Save this person's pricing limits** اسی panel کا الگ Save ہے۔ non-owner کو بغیر محفوظ grant کے خودکار اختیار نہیں، manager بھی نہیں۔ module-role reset pricing panel کو reset نہیں کرتا۔ پہلے محفوظ bill دوبارہ حساب نہیں ہوگا۔ تفصیلی مثال/آزمائش/واپسی [فیز2 نامہ](POS-PHASE2-TEST-URDU.md) اور [فیز3 Extra Discount نامہ](POS-PHASE3-TEST-URDU.md) میں ہے۔

1. مالک Settings → Staff & permissions کھولے۔ Add a person میں Name، Role اور چار ہندسوں کا PIN؛ Add this person۔ مشترکہ PIN کے بجائے ہر شخص کا اپنا نام رکھیں۔
2. شخص منتخب کرکے account Switch on / off سے رسائی روک/بحال کریں۔ ماڈیول کی ticks مخصوص کام کی اجازت دیتی ہیں؛ checkbox بدلنے پر اس کا status دیکھیں۔ Reset page permissions for this role (pricing unchanged) صرف module exceptions ہٹاتا ہے؛ pricing limits کا الگ Save ہے۔ مالک کے module rights اس سے محدود نہیں ہوتے، pricing caps الگ ہیں۔
3. password/PIN بدلنے کے لیے شخص منتخب، New PIN or password میں نیا راز، Change it for the selected person۔ پرانا راز جاننا ضروری نہیں۔ غلط منتخب شخص کا password نہ بدلیں۔
4. Your own recovery word میں اپنا یاد رہنے والا محفوظ لفظ، Save this recovery word۔ خالی چھوڑ کر فیکٹری area11 پر واپس جانا کم محفوظ ہے؛ اسے عملے کے سامنے نہ دکھائیں۔
5. Photos… شناخت کی مدد ہے، login کا بدل نہیں۔ Close the program ایک خاص اجازت ہے؛ صرف عمومی manager نام رکھنے سے ہر بندش مجاز مت سمجھیں۔
6. App words میں اصل section کے مقابل اپنا نام لکھیں؛ Save settings سے navigation/headings بدلتے ہیں۔ Restore original words سب labels واپس کرتا ہے؛ پرانے بل/رسید کی عبارت یا تاریخی record دوبارہ نہیں لکھتا۔

**فیز4 حصہ1 — راز کی تبدیلی:** اب نیا password مقرر کرنے سے سابق PIN، اور نیا PIN مقرر کرنے سے سابق password صاف ہوتا ہے۔ صرف نیا مقرر کردہ راز استعمال کریں؛ اس سے تمام پہلے کھلے sessions کے فوراً ختم ہونے کا دعویٰ نہیں۔ owner-only متعلقہ writes میں database کا موجودہ active owner چیک؛ manager کو administration نہیں۔ recovery کا مخصوص owner راستہ استعمال کریں؛ hash اور غیرخفیہ audit ایک transaction، عام settings سے recovery hash لکھنا ممنوع۔ factory recovery کی موجودہ پالیسی برقرار۔

<a id="lesson-32"></a>

## سبق 32 — Fields: پہلے موجود خانے، پھر اپنے سوال




### Built-in fields کو استعمال کرنے کے قدم

1. مالک Settings → Fields → Built-in fields کھولے۔ Form کالم سے متعلقہ فارم پہچانے، پھر اصل Field کی row منتخب کرے۔ Data type اور Input پہچان کے لیے ہیں، SQL یا حساب کا فارمولا نہیں۔
2. Mark required سے کاروباری ضرورت فعال؛ Mark optional… سے نرم کرنا۔ Critical row نرم کرنے پر اضافی confirmation آ سکتا ہے؛ ہر جگہ ایک جیسے معنی نہ سمجھیں۔
3. Product کے خانے، خصوصاً Selling price، خالی ہوں تو Save product پر ان کے ناموں کا popup آتا ہے۔ **Save anyway موجود ہے**؛ No سے فارم واپس، Yes سے نامکمل چیز محفوظ۔ یہ ہر Save کوشش کی warning ہے، Counter والے فی بل popup سے الگ ہے۔
4. Customer/supplier phone اور agency کے built-in flags **file import** پر چیک ہوتے ہیں، عام ہر editor میں نیا تالا نہیں لگاتے۔ Sale/purchase rows ترجیحات ہیں؛ posting کے اپنے stock، amount، permission اور دوسری پابندیاں پھر بھی موجود ہیں۔
5. required بدلنے سے پرانے rows خود پورے یا حذف نہیں ہوتے۔ تصویر نہ ہونے کو product save کا لازمی دروازہ نہ بنائیں۔ setting کے بعد متعلقہ product کو test copy میں کھول کر اثر دیکھیں، صرف row کا رنگ نہ دیکھیں۔

### Extra fields / Own fields کے قدم

1. Settings → Fields → Extra fields یا Own fields میں Form منتخب کریں: Medicines، Customers، Suppliers، Bills، Purchases یا Expenses۔ صرف مالک field definitions سنبھالتا ہے؛ اجازت والا staff جواب دے سکتا ہے۔
2. New field، Name on the form، Kind of answer اور ضرورت کی Help لکھیں۔ Text عام عبارت، Number عدد، Amount (Rs) روپے، Date تاریخ، List (pick one) ایک انتخاب، Yes / no ہاں/نہیں، اور جائز فارموں پر Photo تصویر کے لیے ہیں۔ Quick add صرف شروعات کا نمونہ بھرتا ہے، حتمی سوال خود پڑھیں۔
3. choice والے سوال میں Choices commas سے الگ؛ Required سوچ کر رکھیں۔ دستاویزی Bills/Purchases/Expenses پر photo قسم دستیاب نہیں؛ دوسرے جائز photo fields بھی لازمی تصویر کا تالا نہیں بنتے۔
4. Save کے بعد متعلقہ فارم میں سوال دیکھیں۔ Stable key نام بدلنے کے باوجود شناخت قائم رکھتا ہے؛ اسے نئے سوال کے لیے پرانے جواب کا مطلب بدلنے میں استعمال نہ کریں۔
5. Move up/down ترتیب؛ Put away سوال چھپاتا ہے، پرانے جواب حذف نہیں؛ Bring back واپس دکھاتا ہے۔ document answers اسی bill/draft کے ہیں، اگلے customer کے bill میں جان بوجھ کر نقل نہ کریں۔

**Built-in fields — حصہ2:** مالک کے لیے metadata کھولتے/تبدیل کرتے وقت موجودہ active owner database سے چیک ہوتا ہے؛ critical field optional کرنے کا risk confirmation پہلے کی طرح لازم۔ audit/acknowledgement میں database کا موجودہ owner نام، صرف پرانا display label نہیں۔ یہ final post کی product/quantity validation ختم نہیں کرتا؛ مالک نہ ہوں یا account بدل/بند ہو تو تبدیلی نہ ہونے پر صحیح active owner سے دوبارہ sign in کریں۔ یہ تمام کھلے sessions کی فوری revocation کا دعویٰ نہیں۔

<a id="lesson-33"></a>

## سبق 33 — خودکار آغاز، محفوظ بندش اور Screen lock

1. Settings → Shop میں startup منتخب کریں: By hand/manual = ہاتھ سے آغاز؛ Start with Windows/auto = Windows sign-in پر آغاز؛ Protected = خود آغاز اور × پر password/PIN کی اجازت۔ auto-start خود login نہیں کرتا۔
2. تمام Windows اکاؤنٹس پر startup رکھنے کے لیے administrator اختیار چاہیے۔ نہ ہو تو موجودہ implementation صرف اس Windows account کی entry رکھ سکتی ہے؛ save کے بعد اصل ScopeText پڑھیں، ہر اکاؤنٹ پر لگ جانے کا اندازہ نہ کریں۔ دوسری account سے جانچ الگ ضروری ہے۔
3. خودکار startup کے login پر fullscreen/چھپی taskbar کی حالت ہو سکتی ہے؛ یہ عام دستی launch کے ہر صفحے کا مستقل نیا layout نہیں۔ دوسری copy کھولیں تو already-running پیغام کے ساتھ نئی copy رکتی ہے۔
4. Protected یا signed-in screen lock میں × پر اصل Close اجازت والا شخص نام اور PIN/password دے۔ غلط/نامکمل اجازت پر بندش نہیں۔ Sign out الگ کارروائی ہے، drawer بندش الگ۔
5. Lock the screen while signed in آن ہو تو maximize/no-minimize اور taskbar چھپانے کے ساتھ واپس جانے پر تنبیہ/واپسی، اور کس نے کب کتنی دیر چھوڑا اس کا record رکھنا مقصود ہے۔ Task Manager، Windows administrator، shortcut یا بجلی کے خلاف اسے مکمل Windows kiosk security نہ سمجھیں۔
6. Shift دبا کر startup پر manual/protection reset کا امدادی راستہ موجود ہے؛ یہ login password recovery نہیں، اور اس کا audit ہوتا ہے۔ صرف مجاز مالک اس کی ضرورت سمجھے؛ اسے عملے کی معمول کی بندش نہ بنائیں۔
7. غیر معمولی بندش کی رپورٹ اگلے آغاز پر آ سکتی ہے۔ ڈیٹابیس کی transaction حفاظت کے باوجود unsaved editor کی ہر typing محفوظ رہنے یا بجلی/disk کی ہر خرابی میں data کبھی نہ کھونے کی ضمانت نہیں—تازہ external backups لازماً رکھیں۔

<a id="lesson-34"></a>

## سبق 34 — Cloud اور دوسرا کاؤنٹر

1. Cloud اختیاری mirror ہے؛ offline Counter چل سکتا ہے۔ Settings → Cloud sync میں مجاز شخص on/off، address/table/shop id اور key دے۔ **key chat، screenshot یا report میں نہ بھیجیں۔** عام tutorial کے لیے کوئی connection درکار نہیں۔
2. Automatic push اور وقفہ (5 سے 1440 منٹ) صرف مقررہ push؛ failed push سے اگلی کوشش تک cloud پرانی ہو سکتی ہے۔ Remove the saved key کے بعد status ملائیں۔
3. SYSTEM → Cloud sync میں Push the shop's data to the cloud مقامی ریکارڈ کی نقل بھیجتا ہے؛ See what the cloud has (writes nothing) صرف preview ہے۔
4. Bring the cloud data here overwrite والی خطرناک کارروائی ہے۔ cloud میں آج کا bill نہ ہو سکتا ہے؛ counts/differences اور confirmation پڑھے بغیر نہ کریں۔ پہلے backup، پھر مجاز شخص کی دانستہ اجازت؛ اسے خودکار دوطرفہ merge نہ سمجھیں۔
5. catalog/master rows وسیع، transactions عموماً گزشتہ 90 دن اور ان کے ضروری متعلقہ records کے scope میں جاتے ہیں؛ **photos نہیں**۔ اس لیے مکمل، تصاویر والا ZIP backup ضروری ہے۔
6. Second counter (LAN) کا صفحہ موجود ہے مگر مالک نے دوسرا counter **ملتوی** رکھا ہے۔ ابھی data folder/share یا database mode نہ بدلیں۔ دو فولڈر بنا لینا دو الگ دکانیں نہیں۔ جب منظور ہو تو ماہر موجودہ location، journal، network/share permissions، backup اور اصل دونوں PC پر جانچ کے بعد ہی آگے بڑھے۔

<a id="lesson-35"></a>

## سبق 35 — Preview، WhatsApp اور پرنٹ کی مشترکہ سمجھ

1. Preview جہاں موجود ہو، اصل printer کو جانے والی عبارت دکھاتا ہے۔ آخری ہندسہ کٹ رہا ہو تو width/driver درست کریں، customer کو نامکمل رقم والی slip نہ دیں۔ Preview window کا Print وہی انتخاب چھاپتا ہے؛ Close صرف دیکھنا ختم کرتا ہے۔
2. Print selected… صرف متعلقہ منتخب قطاروں کے لیے؛ عام Print کی bulk setting بھی دیکھیں۔ A4 summary اور مکمل الگ bill slips ایک چیز نہیں۔
3. WhatsApp سے message/browser کھل سکتا ہے اور اگر on ہو تو PDF تیار ہوتا ہے۔ **PDF فائل کو خود attach اور send کریں**؛ تیار ہونے کا پیغام customer تک پہنچنے کا ثبوت نہیں۔ غلط فون پر کھاتہ نہ بھیجیں۔
4. Auto-print بند، paper ختم یا printer fail ہو تو پہلے Bills میں saved invoice دیکھیں، پھر اسی کی نقل نکالیں؛ دوبارہ Complete sale نئی sale بنا سکتی ہے۔ واپس مال/رقم دینا ہو تو Returns والا راستہ ہے، کاغذ نہ نکالنا cancellation نہیں۔
5. دستیابی کی اندرونی وارننگ رسید پر کبھی نہیں؛ app name/icon کو کاغذ کا shop name/logo نہ سمجھیں۔ Slip دینے سے پہلے نام، unit، quantity، rate، total، paid/due اور change ملانا کیشیئر کی ذمہ داری ہے۔

<a id="lesson-36"></a>

## سبق 36 — روزانہ ترتیب، محفوظ مشق اور خرابی کا اگلا قدم

### صبح سے بندش تک

1. اپنے نام سے Sign in؛ Windows وقت/تاریخ، printer/paper اور ضروری alerts دیکھیں۔
2. Shift میں اصل float سے گلا کھولیں؛ پہلے سے کھلا drawer صحیح گنتی/ہینڈ اوور سے سنبھالیں۔
3. حقیقی delivery Purchases، فروخت Counter، بل والی واپسی Returns، وصولی Customers/Recovery، supplier payment Suppliers؛ ایک ہی رقم دو صفحات پر دوبارہ نہ دیں۔
4. کرایہ وغیرہ Expenses؛ مالک کی ذاتی نقد/مال Drawings؛ ضائع مال Write-off؛ فرضی purchase سے فرق نہ چھپائیں۔
5. دن کے اختتام Reports، کھاتوں اور alerts کا جائزہ؛ اصل cash گن کر shift بند یا hand over؛ تازہ photos-inclusive ZIP الگ محفوظ جگہ نقل؛ Sign out۔

### پہلے صرف یہ محفوظ عملی مشق کریں

- الگ، تصدیق شدہ **test data environment** پر مالک اور ایک test cashier بنائیں۔ دوسری program folder کو الگ test database نہ سمجھیں؛ اصل shop کو fake entries سے نہ بھریں۔
- ایک test product، درست pack formula اور اصلی جیسی future expiry کے test batch سے purchase؛ ایک cash sale اور اس کی دوبارہ رسید؛ پھر اس bill کی return۔ ہر جگہ stock اور cash اثر الگ نوٹ کریں۔
- صفر-stock item کا bill مالک سے بھی رد ہو؛ missing retail لیکن موجود stock پر صرف مالک/مینیجر custom rate دے سکے؛ cashier کو اجازت نہ ہو۔ warning اسی live bill میں ایک بار ہو، Clear کے بعد پھر ممکن ہو، slip پر کوئی availability لائن نہ ہو۔
- Settings Fields میں product retail required کرکے product save warning دیکھیں؛ Save anyway ہو مگر Counter کی پابندیاں الگ رہیں۔
- printer test slip، gallery cover، draft save/open، backup ZIP اور صرف الگ ماحول میں restore آزمائیں۔ اصل stock-take apply، live cloud pull یا LAN configuration کو مشق کا پہلا قدم نہ بنائیں۔

### اگر کام رک جائے

| حالت | محفوظ اگلا قدم |
|---|---|
| Total پر dash/قیمت missing | cart کی Rate Rs، product retail، stock، unit اور نیچے message دیکھیں؛ سبق 10 |
| گلا بند | Shift میں مجاز شخص اصل float/پچھلی بندش ملائے؛ فرضی cash entry نہ بنائیں |
| سٹاک موجود لگتا ہے مگر sale رکے | unit/pack، cart کی مجموعی مقدار، active/unexpired batches اور پرانا minus ملائیں |
| ریٹ/رعایت رد | اس شخص کے محفوظ manual/profit/retail caps، درست basis، مثبت ریٹ، stock اور مستقل purchase-cost guard دیکھیں |
| credit bill رکے | customer، باقی limit، payment split اور اصل permissions چیک |
| صفحہ نہ دکھے | Staff permissions یا فلٹر/بدلا ہوا App words؛ فوراً reinstall/data reset نہ کریں |
| print نہ نکلے | پہلے saved invoice معلوم کریں؛ Windows printer/width/paper؛ اسی bill کی reprint |
| تصویر غائب | gallery میں missing file دیکھیں، photos-inclusive backup تلاش؛ record دوبارہ نہ بنائیں |
| کریش/صفحہ کھلنے کی خرابی | error کی پوری عبارت، version، صفحہ اور کیا دبایا نوٹ؛ غیر محفوظ کام کا حال دیکھیں؛ DB نہ مٹائیں |
| حساب میں فرق | اصل bill/payment/return اور Black box دیکھیں؛ Books check کی read-only فہرست محفوظ |
| restore/cloud کے بعد پرانا data | مزید posting روکیں، منتخب snapshot اور pre-operation recovery ZIP/پیغام دیکھیں؛ بار بار overwrite نہ کریں |

خرابی کی اطلاع میں **version، صفحے کا نام، کیا قدم اٹھایا، متوقع اور اصل نتیجہ** دیں۔ PIN/password، recovery word، cloud key یا پورا حقیقی customer database عام chat میں نہ بھیجیں۔ اگر developer جانچ مانگے تو tools\RUN-TESTS.cmd اور SEND-REPORT.cmd کی مکمل رپورٹ دیں؛ v0.78.0 کا متوقع test count 1300 ہے، یہ اس کتاب سے چل جانے کا دعویٰ نہیں۔

## نگہداشت کا نوٹ — کتاب کہاں سے تصدیق ہوئی

یہ متن v0.78.0 کی navigation اور Views/ViewModels کے مطابق لکھا ہے، تصاویر یا حقیقی Windows session کے مشاہدے کا دعویٰ نہیں۔ بنیادی نقشہ ShellViewModel اور AppLabelService؛ SettingsView، NativeFieldsView، CustomFieldsView؛ متعلقہ Products/Counter/Purchases/Returns/Partners/Shift/Reports/System views سے ملا۔ مالی/stock حدود SaleService، StockPolicy، PurchaseService، ReturnService، ProvisionalReturnService، SupplierLedgerService، AuthService اور StartupService سے ملائی گئیں۔

پرانے HOW-TO-RUN، سابق version rows یا پرانے handoff میں tutorial سے متصادم installation/receipt/negative-stock عبارت تاریخی ہو سکتی ہے۔ نئی receipt-unavailable لائن منسوخ، D-93 برقرار، app identity اور receipt shop details الگ ہیں۔ مالک نے مستقل search pane B منتخب کیا؛ فیز1 space/keyboard code موجود، Windows ثبوت باقی۔ فیز2 مالی editor/permissions code موجود، Windows/PT-21 باقی؛ فیز3 Extra Discount/طریقہB source موجود، Windows/PT-22 باقی؛ فیز4 مکمل page-by-page review ابھی باقی ہے۔ پروگرام کی نئی release میں بٹن/قاعدہ بدلے تو اس کتاب کو بھی نئے source کے ساتھ دوبارہ ملانا ضروری ہے۔
