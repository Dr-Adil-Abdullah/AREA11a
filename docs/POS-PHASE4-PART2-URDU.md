# فیز4 — حصہ2: Settings کی رسائی اور اجازت/منافع کے محدود fixes

**v0.80.0 Ibis (`#3F625C`) · 2026-11-10**

**فیز4 ابھی جاری ہے؛ مکمل audit یا security PASS نہیں۔** اس حصے میں چار محدود خرابیوں کی درستگی اور مزید source شواہد ہیں۔ installer نہیں بنایا، اصل دکان میں فرضی مالی entry نہیں کی۔

## پہلے خلاصہ

| مسئلہ | اب کیا بدلا |
|---|---|
| App name & icon کا content تھا، section بٹن نہیں | Settings کے shell sections میں وہی موجودہ حصہ index0 پر شامل؛ دوسرے حصوں کی ترتیب نہیں بدلی |
| Fields کے لیے7 منتخب کریں مگر ViewModel زیادہ سے زیادہ6 لے | index کی حد7؛ Fields اب اپنے content سے منسلک، App words سے نہیں |
| Built-in fields میں صرف supplied owner label کافی | موجودہ active owner کا database record؛ تبدیلی والی transaction میں بھی، حتیٰ کہ بظاہر no-op ہو |
| Doctors میں منافع چھپانے کا global switch نظرانداز | عام profit permission **اور** global switch دونوں ضروری؛ profit عدد اور flag دونوں mask |
| Product history کی screen profit چھپائے مگر service result میں اصل عدد موجود | denied حالت میں returned sale rows/aggregate کا profit بھی صفر؛ محفوظ اصل snapshot نہیں بدلا |

App name/Fields کی یہ **source درستگی** ہے، Windows click/keyboard کی کامیاب عملی جانچ کا دعویٰ نہیں۔

## روزمرہ مثالیں

### 1. Settings میں غلط ہمسایہ حصہ

پہلے Fields بٹن tab7 مانگتا تھا مگر VM اسے6 کردیتا، اس لیے App words کھل سکتا تھا۔ اب تمام8 contents کے مطابق0–7 section اور selection حد ہیں۔ App name & icon، Shop، Printing، Sales rules، Cloud، Staff & permissions، App words، Fields اپنی جگہ۔ Fields کے اندر Built-in fields اور Extra fields وہی موجودہ controls ہیں۔

### 2. پرانی owner شناخت سے built-in fields بدلنا

account بند/role تبدیل ہو چکا ہو مگر کھلی object میں owner لکھا رہ جائے تو critical quantity optional کرنا نہیں چلنا چاہیے۔ اب native-field List/SetRequired میں موجودہ active owner چیک ہے۔ critical field optional کرنے کا الگ risk confirmation پھر بھی لازم؛ اس سے final bill کی ضروری quantity/product validation ختم نہیں ہوتی۔ audit اور risk acknowledgement میں database کا موجودہ owner نام ہوگا، پرانا supplied display name نہیں۔

یہ پورے session نظام کی replacement نہیں۔ کوئی arbitrary in-process caller اصل owner کا ID خود فراہم کرے تو اس check سے اس کی authentication ثابت نہیں ہوتی۔ تمام کھلی نشستوں کی فوری بندش بھی اس حصے میں نہیں۔

### 3. منافع کا global switch

فرض کریں prescription سے فروخت600، actual cost400، item profit200 ہے۔ global “منافع دکھائیں” switch بند ہو تو مالک یا الگ profit permission والا شخص بھی Doctors کے **explicit profit field** میں یہ200 نہ پائے؛ sales/count اپنی permission کے مطابق رہیں۔ switch دوبارہ ON ہو تو مجاز شخص کو200 اصل محفوظ حساب سے ملے، نئی sale نہ بنے۔

Product history میں بھی denied حالت میں raw sale profit/aggregate صفر ہوگا، UI میں hidden؛ stored bill/item/cost unchanged۔ item figure اب بھی **bill Extra سے پہلے** ہے؛ Extra کسی product پر تقسیم نہیں کی۔

**اہم حد:** یہ explicit profit fields کی درستگی ہے، لاگت یا منافع اخذ کرنے کے ہر ممکن راستے کی بندش نہیں۔ خرید کی تاریخ/دوسرے cost-bearing results اور profit-based quotes الگ مسئلہ ہیں؛ ان کی مکمل privacy certification نہیں۔

## جانچ — اصل حیثیت

12 نئے C# facts:7 native-field owner/identity/no-op/نام،3 Doctors global switch/grant/restore،2 ProductHistory raw-profit masking/restore۔ **کل1337 لکھے ہیں؛ یہاں C# چلائے نہیں** کیونکہ .NET SDK/Windows موجود نہیں۔

**اس حصے میں واقعی چلنے والی جانچ:**68 static checks کامیاب؛ Settings/native-owner/profit کے6 منفی source probes checker نے پکڑے، اصل source بحال کرکے guard کامیاب۔ اردو MD/HTML کا main متن یکساں،36 منفرد lesson anchors اور کوئی remote script/style/image asset نہیں۔ الگ SQLite proof میں36 actual migrations، history preservation، actual BookCheck corruption detection، B560/540 اور foreign keys کامیاب۔ rules matrix تازہ اور diff check صاف۔

Settings کے تمام آٹھ XAML contents، shell indices اور VM کی حد کا source guard ہے؛ **یہ WPF runtime test نہیں**۔ source mutations بھی C# security tests چلنے کا بدل نہیں؛ SQLite proof بھی C# posting نہیں۔ [technical evidence register](POS-PHASE4-ACCESS-REVIEW.md) میں نتائج/حدود الگ درج ہیں۔

## Windows پر محفوظ مشق

1. پہلے verified backup؛ واقعی الگ test database/photos/configuration کی تصدیق۔ صرف exe کا دوسرا folder الگ data نہیں بناتا۔ اصل دکان میں فرضی bill/return/payment نہیں۔
2. version **v0.80.0 Ibis** دیکھیں؛ `tools\RUN-TESTS.cmd` میں **total1337، failed0** مطلوب۔ مکمل output محفوظ کریں۔ Desktop build/launch اور hardware proof الگ ہیں۔
3. مالک سے Settings کھولیں؛ App name & icon پھر Shop/Printing/Sales rules/Cloud/Staff/App words/Fields، ہر بٹن اپنے content پر جائے۔ Fields کے اندر دونوں ذیلی حصے کھلیں۔ موجودہ data/settings نہیں مٹنے چاہئیں۔
4. کسی textbox میں غیرمحفوظ draft text لکھیں؛ صرف section بدل کر واپس آئیں تو text برقرار رہے۔ Save/Cancel کے اصل قواعد برقرار۔ Staff shortcut اب بھی Staff کھولے؛ Fields نہیں۔
5. manager کو صرف section نظر آنے سے owner اختیار نہیں ملتا۔ App name/icon یا native field toggle مجاز owner ہی کرے۔ critical field optional کرنے میں No سے کوئی تبدیلی نہ ہو؛ Yes صرف الگ test shop پر، پھر لازماً سابق required حالت واپس کریں۔
6. isolated data میں owner کے لیے پہلے سے موجود nonzero-profit prescription/product history لیں۔ global margin switch OFF کرکے دونوں دوبارہ load کریں: explicit profit hidden/denied، sales/count برقرار؛ ON پر مجاز profit واپس۔ سابق bills/items/stock/ledger کوئی نئے مالی event کے بغیر وہی رہیں۔ C# direct-result assertions خودکار tests میں ہیں، صرف screen دیکھنا ان کا بدل نہیں۔
7. small screen اور اپنی DPI پر Settings section row keyboard اور mouse سے آزمائیں؛ کچھ چھپے یا overflow ہو تو تصویر/سکرین سائز بھیجیں۔ actual screenshot ہی معتبر، کوئی فرضی screenshot شامل نہیں۔

## واپس/مسئلہ کی صورت

global switch واپس ON صرف مجاز owner اپنی پالیسی کے مطابق کرسکتا ہے؛ profit permission پھر بھی لازم۔ Field کی سابق required حالت مخصوص owner control سے بحال کریں؛ ہر اہم input optional چھوڑنا rollback نہیں۔ App name/icon اپنے موجودہ controls سے واپس۔ پرانی binary نئی database پر ڈالنا یا پرانا backup overwrite کرنا اس fix کو بند کرنے کا محفوظ طریقہ نہیں۔

## باقی کام — صاف طور پر

[تفصیلی method-wise matrix](POS-PHASE4-ACCESS-REVIEW.md) اور [تمام صفحات/15 نکات کا register](POS-PHASE4-EVIDENCE.md) میں status درج ہے۔ اس حصے نے پوری service layer محفوظ قرار نہیں دی:

- Customer/Supplier ledger اور catalog کے بعض legacy read APIs actor لیتے ہی نہیں؛ customer raw profit، خرید/قیمت/statement کے direct-call راستے اگلے حصے کی **ترجیح** ہیں۔ سب callers/exports اور جائز receiving/pricing workflows ساتھ دیکھنا لازم ہے؛ محض screen کا column چھپانا کافی نہیں۔
- Report totals کے raw costs، Reorder کی last cost، Product history کی purchase rows، Dashboard aggregates کا مکمل role matrix باقی۔
- Custom fields اور bill time correction میں موجودہ owner/session/transaction کی مزید جانچ باقی۔
- مکمل finance/stock/concurrency/double-execution، imports/files/backup/cloud،36 اسباق کی ہر action اور اصلی Windows/printer/scanner جانچ باقی۔

بل/Extra/B واپسی/actual-cost floor نہیں بدلے۔ Permission DB-error defaults، HasOpenDrawer، factory recovery یا پوری compatibility feature خاموشی سے نہیں بدلی۔ نئی کاروباری پالیسی درکار ہوئی تو پہلے سادہ مثال اور آپ کا فیصلہ لیا جائے گا۔ اگلا حصہ **اسی فیز4** کا ہے۔
