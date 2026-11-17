# فیز4 — حصہ4: Products کی read اجازت، cost اور Reports

**v0.82.0 Godwit (`#565F46`) · 2026-11-10**

**فیز4 ابھی جاری ہے؛ مکمل audit یا Windows PASS نہیں۔** اس حصے میں Products کی search/Get اور Reports کے مخصوص hidden cost fields درست کیے۔ [تکنیکی شواہد اور باقی حدود](POS-PHASE4-CATALOG-REVIEW.md) الگ درج ہیں۔

## پہلے آسان خلاصہ

- Products کی عام تلاش، کئی لفظ والی تلاش، غلط ہجے کی قریبی تجاویز اور مکمل چیز پڑھنے کے راستے اب Core میں بھی موجودہ active صارف اور متعلقہ اجازت چیک کرتے ہیں۔ صرف جعلی owner role، بند/غائب account یا بدل چکے role سے انکار۔
- Counter اور No-bill PR کو چیز کی catalog cost کی ضرورت نہیں؛ ان کے واپس آنے والے product data میں بھی cost hidden ہے، صرف کالم چھپانا نہیں۔ اصل sale کا batch cost Core خود پڑھتا ہے؛ صفر cost سمجھ کر فروخت نہیں کرتا۔
- Products میں editable cost مالک/مینیجر کے لیے؛ عام staff کو Products اور Profit دونوں اجازتیں دینے سے خودبخود catalog cost نہیں ملتی۔ منافع کا واضح فیصد الگ global switch اور Profit permission سے بھی مشروط ہے۔ **Margin (% retail)** یعنی فروخت کے ریٹ پر فیصد، خرید پر markup نہیں۔
- خرید، Supplier Return، Write-off اور Expenses کے پہلے سے مجاز کاموں میں ضروری cost برقرار؛ انہیں اندھا دھند صفر یا سب کو owner-only نہیں کیا۔ عام Products اجازت سے یہ چاروں الگ اجازتیں خود نہیں ملتیں۔
- Reports میں profit دیکھنے کی اجازت نہ ہو تو مخصوص raw sale/restock cost fields بھی صفر/masked؛ اصل محفوظ cost یا profit صفر نہیں کیے۔
- بل، sale-level Extra، اصل batch cost کی کم از کم حد، caps اور B کی560 پھر540 واپسی نہیں بدلی۔ installer نہیں بنایا۔

## روزمرہ مثال

دوا کی خرید200، فروخت300 ہے۔ Counter کو فروخت/stock ملتا ہے، catalog cost کا200 نہیں۔ پھر بھی bill بناتے وقت Core اصل batch cost200 استعمال کرتا ہے؛ اگر قیمت100 کرکے خرید سے نیچے لے جانا چاہیں تو مالک سمیت سب کے لیے انکار برقرار ہے۔

مالک/مینیجر Products میں جائز طریقے سے cost درست کرسکتا ہے۔ صرف اصل profit permission اور global switch ON ہونے پر margin **33.3% retail** ہوگا؛ یہ50% markup نہیں۔ global profit switch OFF ہونے سے مجاز manager کے editable cost کو مٹانا درست نہیں، کیونکہ receiving/catalog preparation میں اس کی ضرورت ہے۔

جس شخص کو مالک نے Purchases کی اجازت دی ہے وہ delivery کی cost تیار کرسکتا ہے؛ ہم نے اس پہلے سے موجود کام کو بند نہیں کیا۔ اس لیے مطلق راز داری یا کسی بھی مجاز قیمت سے cost اخذ نہ ہونے کا دعویٰ نہیں۔

## محفوظ PT-24 — صرف الگ test data پر

1. پہلے verified backup؛ واقعی الگ database/photos/configuration کی تصدیق۔ صرف دوسری exe directory الگ دکان نہیں۔ اصل shop میں fake sale/return/payment نہ بنائیں۔
2. version **v0.82.0 Godwit** دیکھیں۔ Windows پر `tools\RUN-TESTS.cmd`: مطلوب **total1377، failed0**؛ اصل output محفوظ کریں۔ Desktop build/launch الگ جانچ ہے۔ یہاں یہ C# tests نہیں چلائے۔
3. عام cashier سے Counter میں صحیح نام، کئی الفاظ، barcode اور غلط ہجے کی تجاویز آزمائیں۔ stock/retail/Add پہلے کی طرح؛ permission denial پر bypass نہ کریں۔ Owner سے PR بھی آزمائیں؛ اس product read کو cost کی ضرورت نہیں۔
4. Products اجازت والے عام test staff سے Products تلاش کریں: Cost **hidden**، Margin **—**۔ نئی/edit چیز کا editor نہیں کھلنا چاہیے۔ مالک/مینیجر سے مجاز edit کھولیں: محفوظ اصل cost برقرار؛ hidden صفر form میں load نہ ہو۔
5. مالک کے مجاز account سے cost200/retail300 والی الگ sample دوا میں **Margin (% retail) =33.3%** ملائیں۔ global profit switch OFF/ON کرکے Refresh کریں: margin hidden/واپس، authorized editable cost اور محفوظ قیمتیں unchanged۔ manager کو Profit grant نہ ہو تو صرف management role سے margin نہ ملے۔
6. صرف پہلے سے مجاز test staff سے Purchases، Suppliers کی SR preparation، Write-off اور Expenses goods preparation میں cost درست ملائیں؛ **صرف تیاری دیکھیں، اصل دکان پر Save/Post نہیں**۔ ان modules کی اجازت نہ ہو تو product read انکار کرے۔ ضرورت کے بغیر سب permissions نہ دیں۔
7. test staff کی متعلقہ module grant بند کرکے اگلی refresh/read آزمائیں؛ پرانی کھلی/کاپی/پرنٹ معلومات فوری مٹنے کی ضمانت نہیں۔ role بدلے تو صحیح account سے دوبارہ sign in۔
8. الگ test database میں automated report cases sale/جزوی restock کے بعد manager کا raw cost/profit mask، مالک global OFF/ON اور اصل حساب کی واپسی جانچتے ہیں۔ اصل shop پر فرضی sale/return کے بجائے موجود اصل report محفوظ backup کے ساتھ ملائیں۔ Books/تاریخی totals خودبخود بدلنے نہیں چاہئیں۔
9. اصل Windows screenshot/error/output دیں۔ scanner، printer، DPI اور keyboard کی کامیابی یہاں source check سے ثابت نہیں ہوئی۔

## واقعی کیا جانچا، کیا باقی ہے؟

**واقعی چلایا:**70 static checks؛374 C# grammar files، member resolver میں0 reported problems؛9 منفی source تبدیلیاں پکڑی گئیں اور اصل source بحال۔ ProductService کے34 اور ReportService کے14 verbatim SQL blocks unchanged؛36 migrations اور SaleService/ReturnService unchanged؛ آغاز کی613 فائلیں موجود۔ الگ memory-only SQLite میں36 scripts/history/BookCheck/B560/540/FK کامیاب۔ اردو main MD/HTML یکساں،36 اسباق؛ diff check صاف۔

**نہیں چلایا:**20 نئے C# facts سمیت کل1377 cases لکھے ہیں؛ .NET SDK/Windows یہاں نہیں۔ یہ compile/xUnit/WPF یا shop/printer/security PASS نہیں۔

یہ اصل session authentication کی replacement نہیں: arbitrary اندرونی caller کو اصل owner ID دینے کی طاقت ہو تو ID کو password/session نہ سمجھیں۔ purpose enum راز یا نئی grant نہیں؛ ہر workflow اپنی موجودہ module اجازت مانگتا ہے۔ PermissionService کی پرانی error/default پالیسی نہیں بدلی۔ جاری کام/پرانے preview/export کی فوری revocation کا دعویٰ نہیں۔

Reorder کی LastCost، ProductHistory کی purchase rows، Dashboard/Reports/exports کے باقی costs، Custom fields، TimeCorrection، sessions، مکمل finance/stock/concurrency/duplicate/files/backup/cloud اور36 اسباق کے ہر عمل کی Windows جانچ باقی ہے۔ کسی پورے feature کو نہیں ہٹایا۔ نئی کاروباری پالیسی یا بڑی removal پہلے روزمرہ مثال اور آپ کے فیصلے کے بعد ہوگی۔

## خرابی ہو تو

درست active account/متعلقہ permission مقرر کریں؛ global profit switch اپنی مجاز ترتیب پر لائیں۔ hidden cost کو database میں صفر کرنا یا ہر staff کو receiving کی اجازت دینا حل نہیں۔ پرانی binary یا پرانا backup موجود نئی shop پر ڈالنا محفوظ rollback نہیں؛ اس سے نیا data ضائع ہوسکتا ہے۔ schema اس حصے میں نہیں بدلی، پھر بھی backup/restore الگ تصدیق مانگتے ہیں۔

**محفوظ رکھنے کی حیثیت:** سابق uncommitted کام برقرار؛ یہ حصہ بھی اسی branch کے workspace میں محفوظ۔ commit/push نہیں کیا۔ اگلا حصہ اسی فیز4 کا باقی review ہوگا، installer نہیں۔
