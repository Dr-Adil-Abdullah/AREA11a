# فیز4 — حصہ6: اپنے Fields کی اجازت اور bill کا وقت درست کرنا

**v0.84.0 Lapwing (`#4D6072`) · 2026-11-10**

**فیز4 ابھی جاری ہے؛ مکمل audit، security یا Windows PASS نہیں۔** [تکنیکی شواہد/حدود](POS-PHASE4-DEFINITION-TIME-REVIEW.md) الگ درج ہیں۔

## پہلے خلاصہ

- اپنے نئے Fields بنانا، definition بدلنا، on/off اور ترتیب بدلنا پہلے بھی **صرف مالک** کا اختیار تھا۔ اب Core ہر write transaction کے اندر database کا **موجودہ active مالک** چیک کرتا ہے؛ صرف پرانا owner label کافی نہیں۔ audit میں موجودہ محفوظ نام آتا ہے۔
- Field کا موجودہ tick دوبارہ محفوظ کرنا بھی permission bypass نہیں۔ management/inactive fields کی فہرست بھی live owner مانگتی ہے۔ عام field-list/answers پڑھنے اور public Save answers میں موجودہ account/module check ہے۔
- **عام staff کے جائز answers کا کام بند نہیں کیا۔** مثلاً Customers اجازت والا cashier گاہک کا جائز اضافی جواب لکھ سکتا ہے؛ وہ definition کا required tick خود نہیں بدل سکتا۔
- Bills کی **Fix wrong time** میں بھی موجودہ active مالک لازم۔ پہلے bill پڑھ کر planning اور بعد میں الگ transaction میں write ہوتا تھا؛ اب owner-check، موجودہ bills پڑھنا، planning، dates/payments اور audit ایک ہی فوری write transaction میں ہیں۔
- وقت کا فرق نکالنے کا حساب نہیں بدلا؛ اصل `created_at` اور مالی رقمیں unchanged۔ پرانا/نیا وقت audit میں برقرار؛ غلط payment date یا audit fault پر پوری correction واپس، آدھے bills درست نہیں چھوڑنے چاہئیں۔
- کوئی نیا field، کاروباری rule، schema یا installer نہیں بنایا۔ آپ کا History/Reorder management فیصلہ برقرار؛ Reports Dead stock valuation ابھی الگ باقی review ہے۔

## روزمرہ مثال

مالک نے گاہک کا «لائسنس» سوال required کیا۔ کسی سابق مالک account کا role بعد میں manager ہوگیا، مگر پرانی کھلی window اب بھی owner لکھتی ہے۔ اس پرانے label سے required بند نہیں ہونا چاہیے؛ اب definition لکھنے سے پہلے اصل active مالک کی دوبارہ DB جانچ ہوتی ہے۔ Customers کا مجاز cashier جواب لکھ سکتا ہے، مگر سوال کا اصول نہیں بدل سکتا۔

بل1 نومبر10بجے بنا مگر درست drift دو دن ہے؛ correction سے reporting date3نومبر10بجے، متعلقہ payments کا وقت بھی اسی سابق اصول سے منتقل۔ اصل `created_at`1نومبر محفوظ،300روپے300ہی؛ نیا bill نہیں بنتا۔ اسی دو دن کے offset کو دوبارہ Apply کرنے سے پھر دو دن مزید نہیں بڑھنے چاہئیں؛ پہلے درست bill skip ہوگا، لیکن active owner کی شرط پھر بھی لازم ہے۔

پہلے preview کھل جانا بعد کے Apply کی اجازت نہیں۔ account بند یا role بدل جائے تو نیا Apply انکار کرے۔ دوسرے کام کے درمیان bill بدل جائے تو پرانا preview اصل data نہیں؛ Apply موجودہ bill پھر پڑھتا ہے۔ transaction کے اندر دوسرے writer کو lock روکنے کا الگ SQLite ثبوت ہے، پورے Windows concurrency نظام کا PASS نہیں۔

## محفوظ PT-26 — صرف الگ test data پر

1. verified backup اور واقعی الگ database/photos/configuration پہلے ثابت کریں۔ دوسری exe directory کو خودبخود الگ shop نہ سمجھیں۔ اصل دکان کے bills کی تاریخیں محض test کے لیے نہ بدلیں، نہ fake financial posting کریں۔
2. version **v0.84.0 Lapwing** دیکھیں۔ Windows پر `tools\RUN-TESTS.cmd`: مطلوب **total1421، failed0**؛ اصل output محفوظ کریں۔ Desktop build/launch الگ مطلوب؛ یہاں C# نہیں چلایا۔
3. الگ test shop میں مالک سے Settings → Fields / Own fields کھولیں۔ optional sample سوال بنائیں، نام/ترتیب بدلیں، put away/واپس on کریں۔ saved answers حذف نہیں ہونے چاہئیں۔ required سوال آزمانا ہو تو صرف الگ sample record پر، اصل shop پر کاروبار بند کرنے والا tick نہ لگائیں۔
4. manager کو Settings اجازت ہونے کے باوجود definition administration کی اجازت نہ ملے۔ عام مجاز cashier سے Customer answer لکھ کر دیکھیں؛ جواب allowed، definition required/on-off/order بدلنا denied۔
5. الگ test account کی module permission بند کرنے کے بعد نئی list/answer read/save انکار کرے۔ بند/بدلے role والا owner snapshot definition کے same-tick/no-op میں بھی کامیاب نہ ہو؛ صحیح active account سے دوبارہ sign in کریں۔ یہ منفی cases خودکار suite میں بھی لکھے ہیں۔
6. Bills → Fix wrong time صرف مالک سے، **واقعی الگ test bills** پر کھولیں۔ preview کی Wrong/Proposed تاریخیں اور معلوم drift ملائیں۔ اصل PC clock، Windows یا live TimeSource محض مشق کے لیے نہ بدلیں؛ خودکار tests drift بطور argument دیتے ہیں۔
7. مجاز correction کے بعد corrected reporting date، متعلقہ payments، محفوظ اصل created_at، وہی مالی totals اور audit کے old/new وقت دیکھیں۔ اسی offset کی repeated Apply پر مزید وقت نہیں بڑھنا چاہیے۔ اصل shop میں حقیقی ضرورت ہو تو الگ verified backup/مالک کی تصدیق کے بغیر نہ چلائیں۔
8. preview کے بعد مالک account/role کی جائز تبدیلی ہو تو نیا Apply دوبارہ permission مانگے/انکار کرے۔ خراب payment date/audit fault کے rollback کو الگ خودکار tests پر چھوڑیں؛ اصل DB میں جان بوجھ کر corruption یا triggers مت ڈالیں۔
9. اصل Windows screenshot/error/test output دیں۔ native keyboard، confirmation window، printer/scanner، DPI اور hardware کی کامیابی یہاں static checks سے ثابت نہیں ہوئی۔

## واقعی چلنے والی جانچ

- **72 static checks کامیاب**؛377 C# grammar files، member resolver میں0 reported problems۔12 جان بوجھ کر source خرابیاں guard نے پکڑیں؛ اصل source بحال۔ یہ compiler یا runtime نہیں۔
- CustomField کے12 اور TimeCorrection کے4 verbatim SQL blocks unchanged؛36 migrations، Sale/Return/Purchase posting services اور Database helper کے hashes unchanged؛ آغاز کی622 فائلیں برقرار۔
- نیا `tools/check_time_correction_sqlite.py` واقعی الگ temporary synthetic SQLite پر36 scripts اور اصل source سے نکلی sale/payment/audit SQL چلاتا ہے: فوری transaction کے دوران دوسرا writer رکا؛ audit fault سے سب rollback؛ کامیابی پر dates درست، created_at/رقم محفوظ، old/new audit اور foreign keys درست۔ **یہ Python/SQLite ہے، C# service یا Windows/thread/session test نہیں۔**
- الگ Extra proof میں history/BookCheck، corruption detection، B560/540 اور foreign keys کامیاب۔ اردو main MD/HTML یکساں،36 اسباق؛ diff check صاف۔

**نہیں چلایا:**20 نئے C# facts سمیت1421 لکھے ہوئے cases؛ .NET SDK/Windows/xUnit/WPF دستیاب نہیں۔ حقیقی shop database کو ان proofs نے نہیں کھولا۔

## باقی حدود صاف طور پر

یہ session/password authentication کی replacement نہیں۔ arbitrary اندرونی code کو اصل مالک ID دینے کی طاقت ہو تو ID خود credential نہیں۔ پہلے سے تیار preview/file واپس مٹنے، ہر in-flight job فوراً بند ہونے یا ہر service کی فوری revocation کی ضمانت نہیں۔ PermissionService کی سابق error/default policy نہیں بدلی۔

Parent product/customer/supplier/sale/purchase/expense workflows کے اندر custom-answer helpers اب بھی parent کی اپنی authorization/transaction پر بھروسا کرتے ہیں؛ اُن کی مکمل الگ جانچ باقی ہے۔ انہیں چپکے سے نئی module شرط دے کر جائز receiving/creation بند نہیں کی۔ عمومی record IDs، session، finance/stock/concurrency/duplicates، export/files/backup/cloud اور36 اسباق کی ہر action ابھی مکمل PASS نہیں۔

Reports کی **Dead stock Value** ابھی سابق Reports اجازت پر خرید-cost valuation ہے؛ History/Reorder کا فیصلہ خودکار اس پر لاگو نہیں کیا۔ اگلے policy/review میں اسے واضح کرنا باقی ہے۔

## خرابی ہو تو

صحیح active مالک/account اور متعلقہ module permission دیکھیں۔ انکار دور کرنے کے لیے database یا code bypass نہیں۔ غلط required definition کو صرف مالک درست کرے؛ answers مٹانا حل نہیں۔ وقت کی correction کوئی عمومی undo بٹن نہیں؛ مختلف reporting دن پر اثر پہلے سمجھیں۔ پرانی binary/پرانا backup نئی shop پر ڈالنا محفوظ rollback نہیں، نئی entries ضائع ہوسکتی ہیں۔

**محفوظ رکھنے کی حیثیت:** اسی branch کے workspace میں سابق اور نئے changes محفوظ؛ reset/stash/checkout/commit/push نہیں کیا۔ installer یا live-shop fake posts نہیں۔ اصل مالی حساب، sale-level Extra، سب کے لیے actual-cost floor اور560 پھر540 unchanged؛ اگلا حصہ اسی جاری فیز4 کا ہوگا۔
