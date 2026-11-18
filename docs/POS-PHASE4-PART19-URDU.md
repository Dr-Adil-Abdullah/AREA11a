# PT-39 — ادھار Recovery اور ماہانہ report کی موجودہ اجازت

**v0.97.0 Lapwing · `#527466` · SOURCE checkpoint · Windows جانچ ابھی UNRUN**

یہ حصہ وصولی کا حساب یا sales tax کا اصول نہیں بدلتا۔ مثال: جس ملازم کو Customers کی اجازت ہے وہ ادھار دیکھ/وصول کر سکتا ہے،چاہے Cost/Profit/main بند ہوں۔ صرف Profit دینے سے Customers نہیں مل جاتا۔ ماہانہ `Tax report` الگ Reports grant سے کھلتی ہے؛ یہ پہلے سے محفوظ بلوں کا حساب ہے،نیا tax لگانے کا حکم نہیں۔

[تفصیلی trace،ثبوت اور حدود](POS-PHASE4-RECOVERY-MONTHLY-REVIEW.md) · [مکمل اردو کتاب](USER-GUIDE-URDU.md)

## پہلے حفاظت

1. اصل دکان میں فرضی بل یا وصولی **نہ کریں**۔ صرف دوسری exe folder کافی نہیں: تصدیق شدہ backup کے ساتھ واقعی الگ test DB **اور photos/config paths** لازمی۔ اصل shop activity بند کیے بغیر restore نہ کریں۔ Isolation واضح نہ ہو تو رک جائیں۔
2. Backup password A الگ محفوظ رکھیں؛ chat میں password،اصل DB،keys یا customer records نہ دیں۔ اس حصے سے backup/restore کی Windows منظوری نہیں ملتی۔
3. `tools\RUN-TESTS.cmd` چلائیں: اس build کا متوقع total **1789** ہے۔ یہاں 24 نئے tests لکھے ہیں مگر **کسی C# test/build کو اس Linux میں چلانے کا دعویٰ نہیں**۔ مکمل build/test report اور اصل Windows screenshots چاہییں؛ نمبر/version درست ہوں،ناکامی نہ چھپائیں۔
4. Tax پہلے بند ہے تو بند ہی رہنے دیں؛ موجودہ bills کی tax amount دکھانا نئی tax policy نہیں۔

## الگ test copy میں مشق

| # | عمل | متوقع نتیجہ — ابھی خود جانچنا ہے |
|---|---|---|
| 1 | Customers-only staff؛Cost/Profit دونوں بند،main OFF | Recovery list/ordinary collection/PAY slip کام کریں؛Reports بند ہو تو ماہانہ report نہ ملے |
| 2 | Reports-only،Customers explicit OFF | ماہانہ report اور اس کا Copy جائز؛Recovery/collection-slip منع۔ Profit/Cost grant اکیلے دونوں modules کا بدل نہیں |
| 3 | Recovery پر گاہک منتخب،رقم بھری،ایک payment کی slip موجود؛پھر Owner Customers بند کرے | اگلے filter/receive/reprint یا nominal 3-second active-view check پر پرانی rows،amount/phone/balance/age،totals/opening/books-match،status اور last-slip ID صاف؛نئی وصولی/پرنٹ نہ ہو |
| 4 | ماہانہ report کھلی ہو؛Reports بند،پھر Copy/Refresh یا timer | نئی copy نہ بنے؛پرانے روزانہ rows/رقم/setting label/refund/round-off صاف؛نامعلوم کو مصدقہ Rs0 نہ کہیں۔ پہلے سے clipboard میں پڑا متن واپس نہیں لیا جا سکتا |
| 5 | account deactivate،stored role بدلنا،sign-out/sign-in؛پھر اجازت واپس | اگلی fresh service read منع؛grant واپس اور active/same-role ہو تو refresh/reopen سے کام بحال۔ یہ supplied-ID کو authenticated session بنانے کا ثبوت نہیں |
| 6 | صرف search/filter سے منتخب گاہک فہرست سے نکلے | پرانی selection،اس کا amount/phone/balance/age باقی نہ رہے؛غلط گاہک کو وصولی نہ لگے |
| 7 | Rs1000 carried opening،Rs250 وصول،پھر Rs100 اور وصول | پہلی PAY slip پہلے1000،وصول250،بعد750 ہی؛دوسری payment پہلی slip کا تاریخی balance نہ بدلے؛carried opening فرضی dated bucket نہ بنے |
| 8 | ایک bill کا قرض60دن،دوسرا61دن؛oldest-first collection،اسی bill کی return | پرانی عمر کی حد/collection اور source-bill return rule برقرار؛باقی رقم اصل account سے ملے،mismatch چھپے نہیں |
| 9 | مکمل Rs1000 وصولی،پھر slip دوبارہ | owing list سے گاہک نکلے،PAY slip پھر بھی موجود اور balance0؛overpayment اب بھی reject،clamp نہیں |
| 10 | Collector A وصول کرے،Reader B slip نکالے | Recovery slip میں **اصل payment کے collector** کا stored نام؛یہ Customer PaymentReceipt والے printing-reader label سے مختلف موجودہ معنی ہے |
| 11 | developer-controlled fault صرف isolated DB پر: list refresh،report query،slip read/printing failure | stale totals/labels ختم؛raw SQL/path exception UI/clipboard میں نہ آئے؛failed slip کی جگہ خالی/fabricated document پرنٹر کو نہ جائے |
| 12 | وصولی save ہو جائے مگر فوراً list refresh fail | صاف **SAVED / دوبارہ وصول نہ کریں / refresh کرکے account دیکھیں** message؛کوئی automatic repeat/reversal نہیں۔ Printer fail سے saved payment ختم نہ ہو |
| 13 | tax OFF رکھ کر محفوظ historical test bills کا مہینہ/دوسرا خالی مہینہ/void/hold دیکھیں | stored subtotal-discount/tax/gross،الگ refunds اورround-off؛settings rate سے پرانے bills دوبارہ calculate نہیں؛خالی جائز ماہ واقعی0،failed read hidden/error |
| 14 | views باربار کھول/بند،tab بدل،اجازت بدل،slow dispatcher | Loaded check اور nominal3s timer،Unloaded پرstop؛فوری3s guarantee نہیں،duplicate timers/data subscription نہیں؛authored Note/search/method بلاوجہ reset نہ ہوں |
| 15 | 58/80mm physical printer،Copy clipboard busy،valid reprint | اصل layout/width/footer/PAY code،generic clipboard failure،valid fresh document ہی پرنٹ؛output handoff race اور پہلے سے paper/file کو واپس لینے کی ضمانت نہیں |

## اس ماحول میں کیا چیک ہوا

- **86 static checks PASS**؛یہ compiler/WPF runtime نہیں۔
- **14 synthetic SQLite probes PASS**؛نیا probe اصل SELECTs،36 migrations،filters/typed payment/collector/stored sums/read-only/FK جانچتا ہے؛C# permission/ledger replay نہیں چلاتا۔
- **15 source-negative mutations** checker نے رد کیں،اصل bytes بحال؛یہ executable C# mutation testing نہیں۔
- **24 نئے facts / کل1789 C# cases UNRUN**۔ Windows،DPAPI،printer،installer اور مکمل Phase4 اب بھی کھلے ہیں۔
- حساب والے SQL،Recovery ageing/document builder،monthly text builder اور دوسرے113 Core service/data files نہیں بدلے۔ Black box/BookCheck،Supplier B،backup A،Extra/discount/stock rules برقرار۔

نتیجہ بھیجتے وقت version،قدم نمبر،متوقع/اصل نتیجہ،اصل Windows screenshot یا مکمل test log دیں۔ یہ checklist کامیاب runtime test یا installer نہیں ہے۔
