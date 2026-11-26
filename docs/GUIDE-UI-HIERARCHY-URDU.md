# ⏳ گائیڈ کی نئی ترتیب اور شکل — v0.139.0 Anemone

**2026-11-10 · رنگ `#705E8B` · R3 کے وہی 96 موضوعات**

## ✅ مالک کا اصل Windows نتیجہ، v0.138.0

مالک کے بھیجے ہوئے `RUN-TESTS` output میں .NET10 پر Core اور Tests کی Debug assemblies بنیں اور **3418/3418 PASS،0FAIL،0skip،6m50s** آئے۔ Runner نے بھی ALL GREEN دکھایا۔ یہ کامیابی درج اور قبول ہے؛اسے pending نہ کہیں۔ اس output میں Desktop build،warnings کی تعداد یا نئی UI کی عملی منظوری کی رپورٹ نہیں،اس لئے ان کا دعویٰ نہیں۔

## مالک کی اصلاح

مالک کو موجودہ Docs کی شکل پسند نہیں تھی۔ صرف Open guide بٹن کے بجائے پورے Guidelines کارڈ پر double-click،نرم/خوبصورت styling،اور سیل جیسے حصے کے نیچے categories/subcategories کی واضح فہرست مطلوب تھی،تاکہ ترتیب سے آگے پڑھ سکیں۔ اس checkpoint میں صرف یہی UI کام ہے؛CustomField Add/Move implementation یا دوسرے کاروباری کام نہیں۔

## ✅ سورس میں تبدیلی

- **Dashboard کارڈ:** عنوان،تفصیل یا کارڈ کی خالی جگہ پر بائیں بٹن کا double-click اسی موجودہ lazy overlay کو کھولتا ہے۔ Normal single-click والا keyboard-accessible Open Urdu guide بٹن بھی برقرار ہے۔ غلط «تین نمونے» caption اب نہیں۔ Hover پر ہلکا brand tint ہے،نیا navigation tab نہیں۔
- **اصل nested TreeView:** ایک flat root list کے بجائے وہی source `Parent` graph پورا native درخت بناتا ہے۔ حصہ چنیں تو اس کا اگلا درجہ کھلتا ہے؛مثلاً سیل → Counter → تلاش/مقدار/ادائیگی۔ تیر سے کھولنا/بند کرنا اور native keyboard tree navigation برقرار ہیں؛اپنا کاروباری shortcut handler نہیں لگایا۔
- **دوطرفہ یکسانیت:** کارڈ،ذیلی موضوع،search result،breadcrumb یا contextual Docs سے کھولے ہوئے مضمون کے ancestors کھلتے اور اسی tree node کا انتخاب ہوتا ہے۔ Selection events کی recursion روکی گئی ہے۔ Home سے چھ حصوں کی ابتدائی فہرست واپس آتی ہے،خود کوئی مضمون نہیں کھلتا۔
- **واضح ذیلی فہرست:** منتخب حصے کے direct children مضمون سے **اوپر** اپنے cards/count کے ساتھ دکھتے ہیں؛جس میں مزید فہرست ہو اس پر ذیلی موضوعات کی تعداد ہے۔ یہ حصہ Expander سے عارضی بند بھی ہوسکتا ہے تاکہ چھوٹی window میں زیادہ متن پڑھ سکیں۔ اگلا مضمون کھولنے پر اس کی directory پھر ظاہر ہوتی ہے۔
- **راستہ اور متعلقہ links الگ:** اوپر قابلِ کلک breadcrumbs سے parent/home پر واپس آسکتے ہیں۔ نیچے «بھی دیکھیں» میں دوسرے متعلقہ مضامین ہیں؛direct children یا parent کو اس directory میں دوبارہ نہیں ملایا گیا۔
- **تلاش:** تمام96مضامین کا پہلے والا full-text/table search؛الگ result list،نتائج کی گنتی اور Clear بٹن۔ Result خود نہیں کھلتا۔ تلاش صاف کریں تو منتخب branch والا tree واپس ہے؛کوئی کاروباری صفحہ نہیں بدلتا۔
- **شکل:** نرم header،rounded سفید cards،کشادہ اردو متن،themed heading/table headers اور alternate rows؛reader کی غیرضروری toolbar ہٹائی ہے۔ Card keyboard focus کا واضح رنگ ہے۔ UI styles صرف GuideView کے اپنے resources میں ہیں؛باقی app کے buttons/forms کی styling نہیں بدلی۔

## ✅ جو برقرار ہے

چھ اصل حصے،96مضامین،R3/date،packaged JSON اور self-contained offline book **byte-identical** ہیں۔ نیا authored مضمون یا نئی source revision نہیں۔ اصل .NET10،read-only lazy load،Dashboard overlay،Help not-ready،owned modeless Docs،×،Copy/Paste/picker/row-menu exclusions اورsecurity boundaries برقرار ہیں۔ GuideView پھر بھی صرف واضح request پر بنتی ہے؛tree کا بننا بھی اسی کے بعد ہے،startup پر نہیں۔

Core services/data،Desktop business ViewModels،مالی حساب،37migrations اور **تمام C# test files** نہیں بدلے۔ Suite count **3418** ہی ہے؛نئی release میں صرف BuildInfo/theme/icon،تین build labels اور assembly version کو0.139.0سے ہم آہنگ کیا گیا ہے۔

## ✅ محدود جانچ

```text
python3 tools/build_native_guide.py --check
python3 tools/check_native_guide.py
python3 tools/check_guide_navigation.py
python3 tools/check_repo.py
python3 tools/build_rules_matrix.py --check
```

- **3149 guide static/data assertions:** اصل source/package/book parity اورپہلے read-only/mapping/font/license checks پاس۔
- **309 نئی navigation source/data assertions:** native TreeView،recursive child population،ancestor selection guard،اوپر child directory،الگ related area،کارڈ event/normal button،96موضوعات کےparent paths؛چار in-memory lexical mutation probes بھی رد ہوئے۔ یہ C#/WPF UI execution نہیں،نہ کسی تصویر کا ثبوت ہے۔
- **129 repository static checks:** grammar/XML/resources/bindings/docs؛یہ .NET build نہیں۔ Rules matrix تازہ اورdiff whitespace check صاف۔

## ⏳ اصل Windows/Desktop پر مختصر جانچ

یہاں Windows/.NET SDK نہیں؛نئے UI کا screenshot،WPF rendering،mouse double-click یا focus کا runtime ثبوت نہیں بنایا گیا۔ پچھلا3418PASS اپنی جگہ ثابت ہے؛یہ نئیDesktop تبدیلیوں کی سند نہیں۔

1. نئی **v0.139.0 Anemone** شناخت کے ساتھ Desktop Debug build کریں۔ Regression suite چلائیں تومتوقع count3418ہی ہے؛پچھلا3418PASS ضائع یاpending نہیں ہوا۔
2. Dashboard کے Guidelines عنوان،اردو تفصیل اورخالی جگہ پر الگ الگ double-click کریں؛ہرمرتبہ وہی guide کھلے۔ Button کاsingle-click/keyboard بھی چلے،کارڈکےعام single-click سے guide خود نہ کھلے۔ × سے اصل Dashboard واپس ہو۔
3. سیل منتخب کریں:فوری طورپر اس کے children نظر آئیں؛Counter چنیں،پھر quantity/payment کا leaf۔ Tree،اوپر راستہ اورمضمون ایک ہی موضوع دکھائیں۔ تیر سے collapse/expand اورkeyboard navigation دیکھیں۔
4. Search سے `Save minimum` تلاش کرکےReorder کھولیں؛تلاش صاف کرنے پرمتعلقہbranch نمایاں ہو۔ Contextual Docs سےPrinting/Extra fields کاگہرا راستہ بھی کھلے۔
5. چھوٹی/درمیانی window،DPI/zoom،طویل اردو عنوان،tree scroll،child-directory collapse اورreader/table scroll دیکھیں۔ اصل شکل پسند آنے کی مالک کی منظوری ابھی باقی ہے۔
6. کسیجائزفارم میںصرفغیرمحفوظ متن/amount/quantity/selection رکھیں؛Docs کھول/بندکرکےوہی رہیں۔ کوئی حقیقی Save/Post/Receive/Pay نہ کریں؛native Copy/Paste/row menus/security clearing پرانی شرطوں کے مطابق رہیں۔

## اگلا کام

یہ مالک کے مانگے ہوئے guide UI revision کا checkpoint ہے۔ نئی شکل کی عملی رائے/ضروری correction کو پہلے دیکھیں؛پھر پہلے منظورشدہ5.1CustomField Add/Move کا کام وہیں سے آگے،نہ اس UI تبدیلی سے اسےمکمل سمجھیں۔ حصہ6/durable identity/installer شروع نہیں۔
