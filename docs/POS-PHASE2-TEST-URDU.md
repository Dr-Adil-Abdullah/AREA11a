# فیز2 — قیمت اور رعایت کیسے آزمائیں، اور پسند نہ آئے تو کیسے واپس ہوں؟

**ورژن: v0.77.0 Sandpiper، بھورا رنگ۔** کوڈ اور source checks موجود؛ یہاں .NET/Windows نہیں، اس لیے یہ عملی PASS کی رپورٹ نہیں۔ **1270 ٹیسٹ لکھے ہوئے ہیں (+30 نئے)، چلانا باقی ہے۔** Extra Discount ابھی نہیں؛ اگلے فیز3 میں آئے گا۔

## پہلے مختصر نقشہ

| کہاں جائیں؟ | کیا کریں؟ |
|---|---|
| Settings → Staff & permissions → شخص منتخب | This person's pricing limits میں اس شخص کی اجازتیں/حدیں |
| اسی پینل کا Save this person's pricing limits | صرف اسی شخص کی قیمت/رعایت والی اجازتیں محفوظ |
| Settings → Sales rules | بنیادی bill basis profit یا retail؛ اوپر/صفحے کا عام Save دبائیں |
| Counter → Cart کی قطار double-click یا منتخب row پر Enter | اسی item کا price/discount editor |
| Counter میں discount فیصد کے اوپر profit/retail | موجودہ bill کا basis؛ اجازت ہو تو popup کے بعد ایک bill کے لیے تبدیلی |
| ایڈیٹر کا Reset to normal | الگ price/discount ہٹائے اور line کو bill discount میں واپس شامل کرے |

**profit = حقیقی فروخت ہونے والے batch کے خرید خرچ اور فروخت کا فرق؛ کرایہ/بجلی نکالنے والا net profit نہیں۔ retail = فروخت کی پوری رقم۔**

## 1 — ڈیٹا محفوظ رکھیں

1. پہلے تازہ backup بنائیں اور اس کی صحیح جگہ/فائل کی موجودگی دیکھیں؛ restore کی مشق اصل دکان پر نہ کریں۔
2. **حقیقی دکان میں فرضی بل Complete sale/F4 سے محفوظ نہ کریں۔** ابتدائی preview آزمائش unposted cart میں کریں اور Clear کردیں۔
3. فرضی لاگت200/فروخت300 والی مثالیں صرف واقعی الگ test PC/VM یا تصدیق شدہ test data پر۔ ایک ہی PC پر دوسرا program folder عموماً وہی shop database کھول سکتا ہے؛ اسے محفوظ test copy نہ سمجھیں۔
4. اصل دوا کی لاگت یا ریٹ صرف اس مثال کو ملانے کے لیے تبدیل نہ کریں۔ schema update کے بعد پرانی exe یا پرانا backup اوپر ڈال کر واپسی نہ آزمائیں۔
5. Windows پر `tools\RUN-TESTS.cmd` کی مکمل رپورٹ میں **ALL GREEN - 1270 of 1270** مطلوب ہے۔ وہ Core tests ہیں؛ Desktop build، اصل keyboard/window اور printer کا الگ ثبوت بھی چاہیے۔

## 2 — مالک پہلے ہر شخص کو اجازت دے

مالک سے login → Settings → Staff & permissions → متعلقہ شخص منتخب کریں۔

- **May edit final item rate:** دستی final rate لکھنے کی اجازت۔
- **Maximum manual reduction from retail (%):** اصل منتخب-unit retail سے زیادہ سے زیادہ کمی۔ مثال20 یعنی1200 سے زیادہ سے زیادہ240 کم۔
- **May discount gross profit** اور نیچے حد: منافع پر کتنے فیصد تک۔
- **May discount selling total** اور نیچے حد: پوری فروخت پر کتنے فیصد تک۔
- **May override bill basis for one bill:** Counter سے اسی bill کے لیے profit/retail بدل سکے۔
- **Save this person's pricing limits** دبائیں؛ اوپر عام Save اس الگ panel کے Save کا بدل نہیں۔ پھر دوسرے شخص کو منتخب کریں۔

نئے اور موجودہ غیر مالک accounts کو نئی pricing row کے بغیر کوئی خودکار price/discount grant نہیں، manager role بھی نہیں۔ عام مکمل-retail بغیر رعایت فروخت کی pos permission الگ ہے۔ مالک کی ابتدائی missing-row allowances100% ہیں مگر cost floor اس پر بھی لازم؛ مالک اپنی limits بھی اسی panel سے محدود کرسکتا ہے۔ modules کی “Reset page permissions for this role (pricing unchanged)” pricing limits reset نہیں کرتی۔

## 3 — بنیادی طریقہ منتخب کریں

Settings → Sales rules → **Default bill discount basis (owner only)**:

- profit: 200 خرید،300 فروخت،10% → منافع100،رعایت10،بل290۔
- retail: وہی چیز10% → رعایت30،بل270۔

صفحے کا عام **Save** کریں۔ موجودہ دکان کے آئندہ bills کا ابتدائی default profit ہے؛ پرانے محفوظ bills کا حساب نہیں بدلے گا۔ manager/cashier خود default یا کسی کی limits نہیں بدل سکتا۔

**Purchase cost protection — always ON:** کوئی checkbox بند کرکے یا مالک کی خاص منظوری سے بھی خرید سے نیچے فروخت نہیں۔

## 4 — Counter پر کسی ایک item کی تبدیلی

1. درست دوا/طاقت/pack/Unit/Quantity Cart میں شامل کریں۔
2. Cart کی قطار پر double-click، یا منتخب row پر Enter دیں۔ **یہ window bill save نہیں کرتی۔**
3. نام، unit، quantity اور reference rate دیکھیں۔ `Pricing mode` منتخب کریں:
   - **normal:** اصل ریٹ، اس پر bill کی رعایت چلے۔
   - **manual:** اسی unit کا final rate خود لکھیں؛ کم یا زیادہ، دونوں صورتوں میں bill discount سے باہر۔
   - **discount:** اسی item کا فیصد لکھیں، اس کے اپنے basis میں profit یا retail چنیں؛ bill discount دوبارہ نہیں۔
4. نافذ ہونے سے پہلے quote/error پڑھیں۔ مجاز مالک/مینیجر کو permissions/Show profit اجازت کے مطابق actual batch cost، gross profit اور فروخت پر margin دکھے گا۔ یہ operating-expense net profit نہیں۔
5. **Apply:** تبدیلی موجودہ cart میں؛ **Cancel/×:** کچھ نافذ نہیں؛ **Reset to normal:** الگ تبدیلی ختم، موجودہ bill discount دوبارہ لاگو۔
6. Cart کی **Pricing** column بتائے گی عام bill discount یا الگ final rate/فیصد۔ عام rate cell read-only رہنا اب جان بوجھ کر ہے؛ اجازتوں والا editor اصل راستہ ہے۔
7. اسی product/unit کو دوبارہ عام Add کریں تو پہلے کی الگ adjusted line میں ضم نہیں ہوگا؛ نئی/موجودہ normal line پر bill discount آئے گا۔

### مثال A/B/C

تین items اصل100/100/100، ہر ایک کی cost50 فرض کریں:

- A manual95 →95۔
- B اپنی retail20% →80۔
- C normal، bill retail10% →90۔
- **ان لائنوں کا net مجموعہ265۔** اگر پرانا Rs10 round-down فعال ہے تو round-off−5 اور final260 ہوسکتا ہے؛ یہ double discount نہیں۔ پہلے سے موجود rounding/loyalty کی الگ سطر بھی دیکھیں۔ خودکار Rs10 rounding نئی Extra Discount سہولت نہیں۔
- A کو manual110 کریں تو مجموعہ280، A پر bill10% دوبارہ نہیں۔
- عام D/E شامل کریں تو ان پر bill10% چلے۔

## 5 — یہ حدود ضرور آزمائیں

| آزمائش | متوقع نتیجہ |
|---|---|
| reference1200، manual cap20%، cost500، دستی800 | انکار؛ max reduction240، minimum reference-based960 |
| اسی مثال میں960 | اجازت، دوسری کوئی پابندی/stock issue نہ ہو |
| reference1200، cap20% مگر actualcost1000، دستی960 | خرید سے نیچے ہونے کی وجہ سے انکار؛1000 یا اس سے اوپر رکھیں |
| rate-edit اجازت بند، دستی ریٹ | انکار، manager ہونے سے خودکار اجازت نہیں |
| profit cap30%، retail cap10%، requested20% | profit میں ممکن (cost/دیگر guards کے ساتھ)، retail میں انکار |
| ایک لائن پر manual rate اور الگ فیصد دونوں API سے | Core انکار؛ UI بھی ایک mode رکھتا ہے |
| retail reference موجود نہیں | فیصد کمی ناپی نہیں جاسکتی؛ explicit rate-edit اجازت والے کی مثبت قیمت، actualcost/stock guards پھر بھی لازم |
| cost guard کا پرانا storedfalse | پھر بھی کسی کو خرید سے نیچے فروخت نہیں |

حد سے زیادہ رعایت اب خاموشی سے کم کرکے bill save نہیں ہوگی؛ درست فیصد/قیمت دینے کا پیغام آئے گا۔ Allowances save کرنے کے بعد اگلا quote/post تازہ حد پڑھتا ہے۔

## 6 — صرف ایک بل کے لیے basis بدلنا

1. مالک Settings میں profit رکھے، متعلقہ user کو one-bill override اور retail فیصد کی اجازت دے۔
2. Counter کے discount فیصد کے اوپر basis کو retail کریں۔
3. popup واضح کرے گا: **صرف موجودہ bill**۔ No کریں تو پرانا basis رہنا چاہیے؛ Yes پر retail۔
4. retail basis میں bill discount **5% سے زیادہ** ہو تو Complete sale کے وقت ہر بار الگ تصدیق۔ **صرف test data میں save کرکے یہ قدم آزمائیں۔** No پر bill محفوظ نہیں۔
5. ایک save کی منظوری اگلے bill یا draft میں محفوظ نہیں رہتی؛ service بھی اسے استعمال کرکے ختم کرتی ہے۔
6. Clear، کامیاب post یا Park کے بعد نیا cart Settings default سے؛ مالک کا default retail ہو تو retail ہی، زبردستی profit نہیں۔ محفوظ Draft/Park اپنی basis/modes واپس لائے مگر موجودہ شخص کی تازہ اجازت/stock/cost دوبارہ چیک ہوں۔

## 7 — محفوظ بل، رسید اور واپسی

صرف الگ test data پر:

- profit10% والی300/200 مثال save:290؛ receipt/reprint/net sale/paid amount ایک جیسے۔
- پھر Settings retail کریں؛ پہلے محفوظ290 والا bill وہی رہے۔
- دو units profit10% پر580؛ ایک واپس تو محفوظ net کے مطابق290، دوسری بھی290، آج کی نئی Settings سے refund دوبارہ نہ بنے۔
- Draft/Park میں manual یا item-discount line محفوظ کرکے واپس لائیں؛ حالت/فیصد/basis برقرار۔ دوسرے user کو اجازت نہ ہو تو post انکار، پرانی اجازت draft میں چھپا کر استعمال نہ ہو۔
- Save/print failure کے بعد Bills میں حالت دیکھیں؛ دوبارہ posting یا fake practice نہ کریں۔

## 8 — پسند نہ آئے تو محفوظ واپسی

- صرف bill-discount طریقہ سابق والا کرنا ہو: مالک **Settings → Sales rules → retail → Save**۔ یہ آئندہ bills کا طریقہ بدلے گا، پرانے محفوظ bills نہیں۔ retail >5% confirmation برقرار۔
- کسی line کی تجرباتی تبدیلی: editor → **Reset to normal**؛ موجودہ bill discount پھر لگے گا۔
- کسی شخص کو آئندہ editing سے روکنا: اس کا pricing panel، اجازت بند، **Save this person's pricing limits**۔
- غیر محفوظ cart: **Clear**۔ محفوظ bill کو database سے delete/edit نہ کریں؛ واقعی واپسی ہو تو معمول کا Returns عمل۔
- پرانی binary/schema یا backup بحال کرنا اس Settings واپسی کا بدل نہیں؛ نئے bills ضائع ہونے کا خطرہ۔ نئی permission/cost پالیسی کو صرف retail منتخب کرکے پرانے role-based/lock-off نظام میں واپس نہیں لے جاتا۔

## ابھی کیا باقی ہے؟

- **فیز3:** الگ bill-level Extra Discount / روپے کی tolerance؛ ابھی نہ بٹن بنا ہے نہ یہ20/50 روپے والی رعایت لاگو ہوتی ہے۔
- **فیز4:** تمام pages کا جامع15نکاتی review، باقی bugs/obsolete decisions اور کتاب کی آخری matching۔
- موجودہ **1270 tests، Windows Desktop build، modal/cancel/keyboard/DPI، اصل printer/scanner proof باقی**۔65static checks ان کا بدل نہیں۔
- profit-based رعایت کے totals سے ریاضی کے ذریعے cost کا اندازہ ممکن ہے؛ cost/profit visibility براہ راست خانہ چھپاتی ہے، ناممکن mathematical secrecy نہیں۔ اسی لیے profit-discount permission سوچ کر دیں۔
- installer بنانے کی اجازت اس فیز میں شامل نہیں۔
