# فیز4 — حصہ1: کیا درست ہوا، محفوظ جانچ اور اصل باقی کام

> یہ v0.79.0 حصہ1 کا تاریخی نامہ ہے۔ موجودہ v0.80.0 میں Settings identity0/Fields7، native-field owner اور profit projection کے مزید fixes؛ [حصہ2 کی آزمائش/باقی کام](POS-PHASE4-PART2-URDU.md) دیکھیں۔ موجودہ1337 cases لکھے ہیں، Windows چلنے باقی۔

**ورژن:** v0.79.0 Tern (`#6B455E`) · **تاریخ:** 2026-11-10

## پہلے مختصر نتیجہ

فیز4 **شروع ہوا ہے، مکمل نہیں**۔ اس حصے میں مالک کی اجازت والے writes، PIN/password reset، recovery محفوظ کرنے اور تصویر کے filename کی محدود خامیاں درست کی ہیں۔ بل، خرید کی لاگت کی پابندی، الگ Extra Discount اور منظورشدہ B واپسی (560 پھر540) کا حساب نہیں بدلا۔ factory recovery کا منظورشدہ طریقہ بھی نہیں بدلا۔

26 موجودہ routes، Settings کے8 contents اور Fields کے اندر2 حصے، اور36 اسباق کا source نقشہ بنایا ہے۔ ہر جگہ پوری جانچ کا دعویٰ نہیں: کہیں صرف View → ViewModel → service/data/permission راستہ ملا ہے، کہیں محدود مسئلہ تفصیل سے دیکھا ہے۔ [شواہد اور15 نکات کا باقی کام](POS-PHASE4-EVIDENCE.md) الگ درج ہے۔

## روزمرہ مثالوں سے fixes

1. **مالک کی پرانی شناخت:** فرض کریں کسی account کا role بدل یا account بند ہو چکا، مگر کھلی screen کے پاس پرانا owner label ہے۔ متعلقہ staff/permission/محفوظ settings تبدیلی سے پہلے اب اسی transaction میں database کا موجودہ active owner چیک ہوتا ہے۔ صرف role کا لفظ کافی نہیں۔ Screen lock کی owner-only تبدیلی بھی عام ownerless UI-preference راستے سے نہیں ہوسکتی۔ یہ پورے program کے session نظام کی مکمل security certification نہیں؛ اصل owner کا ID خود دینے والے arbitrary in-process caller کی authentication اس check سے ثابت نہیں ہوتی۔
2. **پرانا PIN باقی رہنا:** password reset کے بعد پرانا PIN بھی چل سکے تو reset نامکمل ہے۔ اب نیا password لگے تو پرانا PIN صاف، نیا PIN لگے تو پرانا password صاف؛ audit میں خود راز نہیں۔ متعلقہ شخص اب نئی مقرر کردہ credential استعمال کرے۔ reset سے ہر کھلی نشست فوراً ختم ہونے کا دعویٰ نہیں۔
3. **غائب account:** حذف/غائب target کا reset یا activation جھوٹی کامیابی نہیں دکھائے گا۔ کسی بھی owner account کو deactivate نہیں کیا جاسکتا؛ staff بند/دوبارہ کھولا جاسکتا ہے۔
4. **Recovery کی آدھی تبدیلی:** hash محفوظ ہو مگر audit ناکام ہو، تو اب دونوں واپس؛ cache بھی کامیاب transaction کے بعد بدلے گا۔ عام settings راستے سے recovery hash لکھنا منع ہے۔ recovery بدلنے کا مخصوص owner بٹن استعمال ہوگا؛ خالی recovery سے منظورشدہ factory طریقہ واپس کرنے کی موجودہ پالیسی برقرار ہے۔
5. **چھوٹی تصویر کا الگ راستہ:** بڑی تصویر کے لیے filename کی جانچ پہلے تھی، بعض thumbnails/gallery میں نہیں۔ اب thumbnail، bulk covers، gallery، بڑی تصویر، staff avatar اور cover selection سب ایک filename جانچ استعمال کرتے ہیں۔ imported/restored row میں `../`، drive-relative، network یا پورا باہر کا path ہو تو تصویر نہیں کھلے گی۔ خراب row/file حذف نہیں؛ gallery میں unavailable image کی طرح رہے گی۔ Windows junction/symlink سمیت ہر filesystem حملے سے حفاظت کا دعویٰ نہیں۔

## جانچ کی صحیح حیثیت

- **1325 C# test cases لکھے گئے**؛ اس حصے کے25 نئے:20 owner/secret/recovery اور5 photo path۔ یہاں **C# tests چلائے نہیں**؛ .NET SDK/Windows دستیاب نہیں۔
- اس حصے میں repository کے **67 static/source checks کامیاب**؛ owner شرط/پرانا راز/gallery path کی3 منفی source تبدیلیاں checker نے پکڑیں، پھر اصل source بحال کرکے جانچ کامیاب۔ یہ checks الگ ہیں؛ compilation، runtime، مکمل security audit یا printer PASS نہیں۔
- Python SQLite کی الگ proof **کامیاب**:36 actual migrations، history preservation، actual BookCheck SQL کی corruption detection اور B560/540؛ صرف مصنوعی in-memory data پر migration/history/BookCheck/Extra B جانچتی ہے؛ یہ C# services چلانے کے برابر نہیں۔
- اردو MD/HTML کا اصل main متن عین یکساں اور36 منفرد lesson anchors؛ rules matrix تازہ، diff whitespace check صاف۔ یہ ہر سبق کے عملی درست ہونے کا ثبوت نہیں۔
- کوئی installer نہیں بنایا؛ اصل دکان میں فرضی bill/return/payment نہیں ڈالی۔

## Windows پر محفوظ آزمائش

### 1. ماحول پہلے

تصدیق شدہ backup اور واقعی الگ test database/photo folder/environment لازم۔ صرف exe کو دوسرے فولڈر میں رکھنا isolation نہیں؛ ProgramData یا configured database پھر بھی مشترک ہوسکتی ہے۔ paths کی تصدیق کے بغیر synthetic معاملات post نہ کریں۔

### 2. Build اور خودکار tests

نیا code لینے کے بعد version **v0.79.0 Tern** دیکھیں۔ `tools\RUN-TESTS.cmd` چلائیں؛ **total1325، failed0** درکار۔ actual output محفوظ کریں۔ Desktop build اور اس کی warnings/errors بھی الگ دیکھیں؛ static checks کا سبز نتیجہ اس کی جگہ نہیں۔

### 3. Staff reset — صرف الگ test shop

- مالک سے test staff بنائیں، PIN سے login آزمائیں۔
- test automation دونوں سمتوں کا reset چیک کرتی ہے؛ UI میں جس قسم کا reset دستیاب ہے وہ استعمال کریں۔ نیا راز چلے، سابق راز نہ چلے۔ خودکار test failure کو manual success سے چھپائیں نہیں۔
- staff بند کریں: نیا login منع؛ دوبارہ active کرکے صحیح راز چلے۔ owner بند کرنے سے انکار ہو۔
- manager/cashier permissions یا کسی کا راز تبدیل نہ کرسکے۔ owner grant/reset اب بھی کام کرے۔
- کھلے session کی فوری revocation، forged ID یا transaction fault کو عام UI سے آزمانے کا دعویٰ نہ کریں؛ ان کے source tests/باقی audit الگ ہیں۔

### 4. Recovery

صرف isolated shop میں اپنی recovery word مقرر کریں؛ اگلے recovery login میں وہ چلے۔ Black box میں صرف تبدیلی کا واقعہ ہو، لفظ/hash نہ ہو۔ اصل owner کا معلوم راز یا verified backup ضائع نہ کریں۔ audit failure rollback کے لیے خودکار trigger test لکھی ہے، حقیقی دکان کے database میں trigger نہ لگائیں۔ factory recovery پالیسی اس حصے میں تبدیل نہیں ہوئی۔

### 5. تصاویر

عام محفوظ تصویر Add کریں؛ thumbnail، gallery، cover اور بڑی تصویر دکھیں۔ Hide/Restore سے file نہ مٹے۔ باہر کے filenames کے منفی cases خودکار tests میں ہیں؛ اصل database کو ہاتھ سے خراب نہ کریں۔ Windows پر profile avatar اور valid product cover بھی دیکھیں۔

### 6. پہلے کے منظورشدہ مالی اصول برقرار

الگ test data میں [فیز2](POS-PHASE2-TEST-URDU.md) اور [فیز3](POS-PHASE3-TEST-URDU.md) کے cost floor، pricing caps، الگ Extra اور560/540 واپسی کے tests دوبارہ چلائیں۔ اس حصے میں financial calculation code نہیں بدلا؛ اس کا مطلب ہر پرانی راہ خودبخود verified نہیں۔

## اس حصے کو بند/واپس کرنے کی سمجھ

یہ کوئی نیا optional financial mode نہیں۔ غلط permissions یا راز کی صورت میں active owner سے درست grant/reset کریں؛ filename غلط ہو تو معتبر تصویر دوبارہ Add/cover منتخب کریں، اصل row/file خودکار delete نہیں ہوگی۔ security checks بند کرنے کے لیے database یا code میں bypass نہ لگائیں۔ پرانی binary نئی database پر چلانا یا پرانا backup موجودہ دکان کے اوپر ڈالنا محفوظ rollback نہیں۔ مسئلہ آئے تو posting روکیں، محفوظ copy/logs رکھیں، build/test output بتائیں؛ restore صرف باقاعدہ تصدیق شدہ طریقے سے۔

## اسی فیز کا اگلا حقیقی باقی کام

- Settings index0 **App name & icon** content موجود ہے، مگر shell کی section list میں اس کا اندراج نہیں۔ headerless view میں عام click سے وہاں پہنچنے کی source discrepancy ملی ہے؛ اسے درست/verify کرنا باقی ہے۔ کتاب کا محض نام لکھنا اس کے accessible ہونے کا ثبوت نہیں۔
- تمام services کے direct-call permissions، stale sessions، record IDs اور cost/profit projections کا matrix۔ اس حصے کے checks کو ہر service کی حفاظت نہ سمجھیں۔
- خرید، کھاتے، stock/count/returns، mixed payments، loyalty، reports اور duplicate execution کے fault/concurrency cases۔
- imports، SQL/HTML/files، backup failure drill، cloud credentials/RLS/mixed-version حدود۔
-36 اسباق کی ہر action/label کی مکمل reconciliation؛ Windows DPI، native keys، scanner، dialogs اور اصلی printer کے شواہد۔
- deliberate DB-error policies، factory recovery یا پوری compatibility feature ہٹانے کا فیصلہ خاموشی سے نہیں ہوگا؛ ضرورت ہوئی تو پہلے سادہ مثال اور مالک کا فیصلہ۔

اگلا حصہ **اسی فیز4** کا ہے، کوئی نیا installer فیز نہیں۔
