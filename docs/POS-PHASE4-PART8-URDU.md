# فیز4 حصہ8 — Staff permissions کا الگ مالی حصہ اور تفصیل (PT-28)

**v0.86.0 Reedling — source میں شامل، Windows پر عملی آزمائش ابھی باقی۔** آپ کی نئی درخواست کے مطابق UI بنا ہے؛ اسے پورے پروگرام سے ہر leak بند ہونے کی تصدیق نہ سمجھیں۔

## کیا نیا ہے

Settings → Staff & permissions میں شخص منتخب کرنے کے بعد الگ **Sensitive financial & ledger permissions** حصہ ہے:

- **View Profit**
- **View Purchase Cost**
- **Supplier ledger access (Suppliers)**
- **Customer ledger access (Customers)**

پہلے منظورشدہ Profit اور Cost **الگ ہی ہیں**؛ نئی مثال کے مشترک لفظ کی وجہ سے انہیں ملایا نہیں۔ Ledger ticks موجودہ page/workflow permissions ہیں، نئی خفیہ cost override نہیں۔ باقی module ticks عام حصے میں ہیں؛ ہر tick صرف ایک جگہ۔ Main financial switch پہلے کی طرح Sales rules میں ہے، دونوں financial grants پر لازم۔

### مختصر اور تفصیلی مدد

1. کسی permission checkbox پر mouse رکھیں: مختصر tooltip بتائے گا کہ یہ کیا کرتی ہے۔
2. Checkbox یا اس کے label پر **double-click** کریں: تفصیلی dialog کھلے گا۔ **Details…** بٹن keyboard سے بھی دستیاب ہے؛ disabled box کے لیے بھی یہ بٹن استعمال کریں۔
3. Dialog میں scope، دوسرے ضروری grants/main switch اور rollout کی باقی حدود پڑھیں۔
4. نیچے موجودہ users کی فہرست ہے: نام، role، active/inactive، enabled/disabled grant، explicit یا role default، اور access کی باقی شرط۔
5. فہرست تقریباً ہر **3 سیکنڈ** بعد database سے دوبارہ پڑھی جاتی ہے؛ **Refresh now** بھی ہے۔ یہ instantaneous revocation نہیں۔

مثال: احمد کی Cost tick ON مگر main OFF ہو تو Grant **Enabled** مگر Access **Blocked: main switch OFF** ہوگا۔ Account بند ہو تو بھی پرانی tick کی حالت دکھے گی، مگر **Blocked: inactive account** صاف لکھا ہوگا۔ Enabled کا مطلب یہ نہیں کہ ہر page کی اجازت بھی مل گئی۔

### Double-click سے tick نہ بدل جائے

عام checkbox کا پہلا click فوراً save ہو تو دوسری click پر مدد کھولنے سے پہلے اجازت بدل سکتی ہے۔ اس لیے pointer single-click Windows کے مقرر double-click وقفے کے بعد نافذ ہوتا ہے؛ double-click pending toggle منسوخ کرکے صرف Details کھولتا ہے۔ Keyboard کا **Space** عام checkbox کی طرح کام کرتا ہے۔ Tick بدلنے پر موجودہ audited owner-only path سے فوراً save؛ Pricing limits کا الگ Save پہلے کی طرح الگ ہے۔

### فہرست کون دیکھ سکتا ہے؟

صرف موجودہ active مالک۔ ہر نئی read پر database میں بھی ownership/active account چیک ہے۔ Manager کو Staff/Settings tick دے دینا مالک نہیں بناتا۔ Owner بند/demote ہو یا read fail ہو تو نئی refresh پر پرانے staff names فہرست سے صاف؛ کامیاب پرانی list کو تازہ نتیجہ نہیں کہا جائے گا۔ Inspector میں PIN/password/hash/cloud key نہیں پڑھی جاتی۔

## ابھی کیا باقی ہے

**ہر backend route پر نئی cost policy ابھی مکمل نہیں۔** Purchase invoice، supplier ledger/batches، stock-take/stock-minus/write-off/expense history، audit/workbook/دوسرے exports اور stale prepared data باقی ہیں۔ اسی لیے UI میں واضح rollout warning ہے۔ Supplier payable/paid کو دکھا کر purchase total اخذ ہونے اور ادائیگی/SR usability کا سابق سوال اس نئی UI درخواست سے خودکار طور پر حل نہیں سمجھا۔ پرانا combined checkbox سوال دوبارہ نہیں پوچھنا۔

یہ screen موجودہ permissions کو سمجھنے اور configure کرنے کا ذریعہ ہے، نئی encryption یا ہر مالی inference کے خلاف حفاظت نہیں۔ مجاز profit+sale سے cost اور cost+sale سے profit اخذ ہوسکتا ہے؛ پہلے کی PDF/print واپس نہیں مٹ سکتی۔

## محفوظ Windows جانچ — سب UNRUN

اصل shop data پر fake bill، payment یا SR نہ بنائیں۔ Verified backup اور واقعی الگ synthetic test data/process لازم؛ صرف executable کا فولڈر الگ ہونا کافی نہیں۔

| PT-28 | عمل | متوقع نتیجہ |
|---|---|---|
| 01 | مالک، Staff میں حقیقی الگ test account منتخب کرے | چار مالی/ledger ticks الگ group، باقی modules عام group، کوئی duplicate tick نہیں |
| 02 | ہر tick پر hover | مختصر متعلقہ tooltip؛ فوراً کوئی permission write نہیں |
| 03 | Profit/Cost پر double-click | scope + current assignment list؛ اصل grant نہ بدلے، audit میں اتفاقی permission_change نہ بنے |
| 04 | ایک single-click؛ پھر keyboard Space | pointer وقفے کے بعد صرف ایک change؛ Space native ایک change، غلط account کی grant نہیں |
| 05 | Details button Tab/Enter، Esc/Close | dialog کھلے/بند ہو؛ checkbox نہ بدلے؛ reopening میں duplicate timer نہ بنے |
| 06 | owner کی اپنی financial tick OFF پھر Details | own denial نظر آئے، مالک Settings/inspection تک پہنچ سکے |
| 07 | main OFF کے ساتھ enabled tick inspect | Grant Enabled مگر financial Access blocked؛ ledger module خود بند ظاہر نہ ہو |
| 08 | الگ test process سے grant/account بدلیں، refresh | موجودہ حالت اگلی read میں؛ inactive user فہرست میں مگر access blocked |
| 09 | inspector کھلا ہو، owner deactivate/demote یا DB fault | نئی refresh پر names صاف اور واضح انکار، پرانی کامیاب list باقی نہ رہے |
| 10 | پہلے click کے فوراً بعد selected person/Refresh بدلیں | pending پرانا checkbox نئے شخص پر save نہ ہو |
| 11 | click باہر drag کرکے release؛ slow configured double-click | accidental grant نہ ہو؛ OS timing کا احترام |
| 12 | چھوٹی screen/DPI/بہت سے staff | scroll/readability؛ ہر column سمجھ آئے؛ UI نہ کٹے |

## یہاں واقعی کیا چلا

**75 static checks PASS**؛386 grammar files،0 reported member problems؛10 negative source probes rejected/restored۔ **20 نئے /1469 کل C# cases لکھے مگر نہیں چلائے**؛ dotnet/Windows دستیاب نہیں۔ اصل source SELECTs الگ temporary SQLite پر coherent snapshot/current refresh/defaults/parameter/FK checks PASS؛ C#/WPF/gesture proof نہیں۔ پچھلا Windows v0.80.0 نتیجہ1333pass/4fail تھا، چاروں کی اصلاح source میں برقرار مگر updated Windows run نہیں ملا۔

کوئی installer، commit/push یا live-shop fake posting نہیں۔ Purchase/Supplier/Stock اور باقی110 پرانے Core service/data file hashes unchanged؛ اصل financial posting اس حصے میں نہیں بدلی۔ [تفصیلی trace/حدود](POS-PHASE4-PERMISSION-INSPECTOR-REVIEW.md)، [اصل مکمل استعمال نامہ](USER-GUIDE-URDU.html)۔
