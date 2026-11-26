# ⏳ اردو گائیڈ R3 — تمام موضوعات کا تحریری ایڈیشن

**v0.138.0 Speedwell · 2026-11-10 · رنگ `#4B6285`**

مالک کی تازہ منظوری سے پہلے تین نمونوں کی حد ختم ہوئی: باقی گائیڈ کا مواد مکمل کرنا ہے، پھر پہلے جاری 5.1 کام پر واپس آنا ہے۔ پرانے محدود R2 checkpoint کو موجودہ scope نہ سمجھیں۔ تحریری مواد کی تکمیل، سورس/براؤزر جانچ اور اصل Windows/WPF کامیابی تین الگ باتیں ہیں۔

## ✅ مواد اور مشترکہ ماخذ

- چھ اصل حصوں کے تمام **96 موضوعات** میں اب مواد ہے؛ کوئی خالی placeholder نہیں۔ حصے وہی سیل، سٹاک، کسٹمرز، منی، سسٹم، شروعات اور عام باتیں ہیں۔
- ہر مضمون پہلے مقصد، پھر ممکن کام، گروپ کے لحاظ سے controls کی table، طریقہ اور احتیاط بتاتا ہے۔ اصل No Bill، Printing اور Login مضامین کا عملی مفہوم برقرار ہے؛ پرانے نمونے کے غیرضروری status جملے ہٹائے اور باقی مواد شامل کیا گیا ہے۔
- `docs/guide/guide-source.json` واحد موجودہ authored source ہے۔ اسی میں R3/date، ترتیب، عنوان/path، blocks، متعلقہ links اور implementation/manual references ہیں۔ Sources کا مطلب سورس سے تعلق ہے، ہر جملے کی الگ خودکار معنوی تصدیق نہیں۔
- `tools/build_native_guide.py` صرف اسی authored source سے Desktop کا `Resources/Guide/guide.json` اور `docs/guide-prototype/book.html` بناتا ہے۔ Packaged JSON میں صرف reader کے ضروری fields جاتے ہیں؛ source references build-time documentation ہیں۔
- پرانے `index.html`،`no-bill.html`،`printing.html`،`login.html` تاریخی R2 browser prototype ہیں، موجودہ کتاب کے authored source نہیں۔ پرانے index سے صرف offline CSS/font اور SIL OFL notice محفوظ طور پر لیا جاتا ہے؛ font bytes تبدیل نہیں ہوئے۔
- کتاب میں وہی 96 مضامین، چھ root links، متعلقہ offline anchors اور embedded Urdu font ہیں۔ اسے اکیلا نقل کرکے کھولا جاسکتا ہے؛ `URDU-GUIDE.cmd` بھی یہی کتاب کھولتا ہے۔ Ctrl+F تلاش اور Ctrl+P پرنٹ/PDF کیلئے ہیں۔ یہ launcher business data نہیں پڑھتا یا بدلتا۔

## ✅ محدود source تبدیلیاں

- Viewer کے پرانے «تین نمونے» status اب تمام مضامین کی تلاش بتاتے ہیں۔ Source lazy ہی ہے: صرف واضح Guidelines/Docs درخواست پر data/viewer کھلتا ہے۔ کوئی startup preload،WebView2،runtime AI،نیا network dependency یا .NET upgrade نہیں۔
- Barcode،Preview،Product history،Forgot password،Expense categories،Time correction،Sale success،Gallery اور فارم میں Extra fields کی متعلقہ headings/background کو Docs mapping دی گئی ہے۔ Shared Gallery کا مضمون Product کے ساتھ Customer/Supplier تصاویر کو بھی واضح کرتا ہے۔
- نیا navigation tab یا Settings index نہیں؛آٹھ Settings حصے اپنی پرانی ترتیب میں ہیں۔ Startup کی گائیڈ Shop کے موجود اختیارات کا مضمون ہے،نواں tab نہیں۔
- Dashboard overlay اور الگ owned modeless Docs کا پہلے منظور شدہ طریقہ برقرار ہے۔ Help کا الگ not-ready پیغام بھی مالک کے انتخاب کے مطابق برقرار ہے،اسے نئی نامکمل guide article نہ سمجھیں۔
- کوئی کاروباری service،ViewModel،مالی SQL،migration،حقیقی shop record یا پرانا غیرمتعلق test تبدیل نہیں ہوا۔ پچھلے 19 guide facts میں صرف نئی packaged revision/date کی توقع بدلی؛دو نئے facts تمام 96 مضامین اور نئے عملی مضامین کی search کیلئے شامل ہوئے ہیں۔

## ✅ دستیاب جانچ

```text
python3 tools/build_native_guide.py --check
python3 tools/check_native_guide.py
python3 tools/check_repo.py
python3 tools/build_rules_matrix.py --check
```

- **3,149 static/data assertions**: source/package/book parity،metadata،96 مضامین،purpose-first،control tables،steps،source files،tree/links،mapped IDs،licensed font bytes،read-only اور focus-containment source boundaries۔ یہ خودکار اردو معنوی proof یا WPF runtime نہیں۔
- **129 repository static checks** پاس؛465 C# grammar files،4940 calls/10494 references کی static resolution،صفر مسئلہ۔ Rules matrix بھی تازہ ہے؛یہ .NET compile نہیں۔
- **205 browser assertions**: تمام96 مضامین کے purpose/table،چھ root links،R3/date،تمام anchors،کوئی placeholder/script/network request نہیں،embedded font،mobile horizontal overflow نہیں،اور nonempty browser print PDF۔ Desktop screenshot بھی دیکھا گیا۔ PDF/screenshot صرف عارضی جانچ کی files ہیں،repository میں شامل نہیں۔
- اختیاری browser check: `node tools/check_guide_book.cjs`؛اسے الگ development ماحول میں Playwright اور Chromium چاہیے،اصل AREA11 runtime کو نہیں۔ متبادل Chromium کیلئے `CHROMIUM_PATH`،موجودہ sandbox package کیلئے `AREA11_SERVERLESS_CHROMIUM=1` استعمال ہوسکتا ہے۔

## ⏳ Windows/WPF پر ابھی ضروری

یہاں `dotnet` SDK یا Windows نہیں۔ نئی Core/Desktop/Tests compilation اور **3418** متوقع C# cases **UNRUN** ہیں؛نئے21guide facts بھی اس گنتی میں ہیں۔ پچھلے v0.136.2 کے مالک کے **3397PASS،0FAIL،0skip** اور Debug **0errors/30warnings** ثابت تاریخی نتائج ہیں،نہ pending ہیں نہ نئی build کی کامیابی۔

1. نئی Speedwell شناخت کے ساتھ Debug build اور `tools\RUN-TESTS.cmd` چلائیں؛3418total/0failed/0skipped کی اصل رپورٹ الگ دیکھیں۔ موجودہ warnings حل شدہ نہیں کہی گئیں۔
2. Dashboard→Guidelines پر صرف چھ cards آئیں،خود کوئی مضمون نہ کھلے؛نئے مضامین مثلاً Reorder/Quarantine/Backup کے اندرونی الفاظ اور control table تلاش کریں۔
3. کسی غیرمحفوظ فارم کی تحریر،quantity،amount،selection محفوظ رکھتے ہوئے heading/background سے Help/Docs کھولیں؛کوئی Save/Post/Receive/Pay نہ کریں۔ × بند کرنے پر اصل form/focus/selection دیکھیں؛اصل typing/Copy/Paste/row menu/picker باقی ہوں۔
4. پہلی right-click اور keyboard Menu/Shift+F10،nested tabs،نئی mapped windows،ایکowner کی ایک modeless Docs window،Alt+Tab اور اصل security/idle/session clearing کی جانچ کریں۔ Docs کو جائز security clearing واپس نہیں لانی چاہیے۔
5. چھوٹی screen/DPI،Urdu shaping،طویل articles/tables،packaged/published guide.json اور isolated test output میں missing/corrupt file کا پیغام دیکھیں۔ اصل shop data یا printer job پر تجربہ نہیں۔
6. `URDU-GUIDE.cmd` کو Windows پر offline کھول کر links،font،Ctrl+F اور print preview دیکھیں؛browser proof کو native/printer proof نہ سمجھیں۔

## ⏳ پہلے کام پر واپسی

گائیڈ کا تحریری/source checkpoint اب R3 ہے؛عملی تصدیق اوپر الگ باقی ہے۔ اگلا پہلے سے منظورشدہ کام **5.1 CustomField Add/Move** کا باقی outcome/concurrency جائزہ ہے؛پھر remaining imperative Settings writers،startup registry اور StockTake کا tracker دیکھنا ہے۔ [واپسی کے source review](CUSTOM-FIELD-NEXT-SCOPE-URDU.md) میں Add کے plain Tx/ہاتھ سے row،VM کے receipt ضائع کرنے اور Move کے existing entity-CAS مگر باقی outcome/receipt فرق دوبارہ درج کئے ہیں؛اس checkpoint میں ان کا implementation نہیں بدلا۔ گائیڈ ان routes کی hardening مکمل نہیں کرتی۔ **1 تا 5 کے باقی کام/تصدیق سے پہلے حصہ6،durable identity یا installer شروع نہیں۔**
