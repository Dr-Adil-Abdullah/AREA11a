# فیز4 — حصہ5: History کی خرید cost، Reorder اور روزانہ رپورٹ

**v0.83.0 Snipe (`#5B5265`) · 2026-11-10**

**فیز4 جاری ہے؛ مکمل audit/security یا Windows PASS نہیں۔** [تکنیکی شواہد، method matrix اور باقی حدود](POS-PHASE4-SNAPSHOT-REVIEW.md) الگ ہیں۔

## پہلے خلاصہ — آپ کے منتخب اصول کے مطابق

آپ سے مثال کے ساتھ پوچھا تھا: خرید200، فروخت300؛ catalog cost چھپی ہو مگر History میں10 چیزوں کا خرید total2000 مل جائے تو cost200 پھر نکل آتی ہے۔ آپ نے **صرف مالک/مینیجر** منتخب کیا؛ اسی فیصلے D-109 کے مطابق:

- متعلقہ History/Reorder module کی اجازت بھی ضروری۔ صرف مالک/مینیجر کو **History کی خرید کے rate، line total اور خرید کا مالی مجموعہ** ملے گا۔ عام staff کے لیے تینوں mask؛ مقدار، تاریخ، supplier اور موجودہ مجاز sale معلومات برقرار۔
- History کی screen، print/WhatsApp/PDF preparation اور CSV میں یہی اصول۔ hidden خرید کو اصل صفر خرید نہ سمجھیں؛ CSV کے خرید rate/total خانے خالی، جبکہ اصل صفر والی مجاز خرید واقعی صفر دکھے گی۔
- Reorder کے returned data میں آخری خرید cost بھی management-only؛ موجودہ order message میں cost پہلے بھی شامل نہیں تھی، اب بھی نہیں۔ stock/minimum/suggested quantity/supplier کا حساب نہیں بدلا۔
- History/Reorder/Dashboard/Reports اور daily rows موجودہ active account، role اور module چیک کرتے ہیں۔ بند/غائب/بدلے role یا جعلی owner label سے انکار۔ Dashboard کی سابق Pos **یا** Reports اجازت برقرار، دونوں لازم نہیں کیں۔
- Reports کے public Days راستے میں پہلے user لیے بغیر profit نکلتا تھا؛ اب وہ بھی Reports اجازت اور global/Profit switch مانگتا ہے۔ method ہٹایا نہیں، محفوظ caller لازم کیا۔

**مجاز خرید/SR/Write-off/Expenses کی عملی cost تیاری کا سابق اصول نہیں بدلا۔** Product History کی خرید cost کے لیے اُن کی grant عام staff کو management نہیں بناتی۔ واضح sale profit کی اپنی پہلے سے موجود global/Profit اجازت الگ ہے؛ مجاز profit سے cost اخذ ہوسکتی ہے، مطلق راز داری کا دعویٰ نہیں۔

## مثال: خرید کی رقم اور profit الگ اجازتیں ہیں

مالک/مینیجر History میں خرید200، مقدار10، کل2000 دیکھ سکتا ہے۔ عام staff کے لیے rate/total اور Bought مالی خلاصہ **hidden** ہوگا؛ quantity10 برقرار۔ صرف rate چھپا کر total2000 چھوڑ دینا کافی نہیں تھا۔

global profit switch OFF کرنے سے مالک/مینیجر کی جائز خرید cost مٹانا درست نہیں؛ وہ cost برقرار، واضح sale profit hidden۔ ON اور متعلقہ Profit اجازت پر اصل محفوظ profit واپس آتا ہے؛ کوئی نئی sale یا پرانے record کی rewrite نہیں۔

## محفوظ PT-25 — Windows اور واقعی الگ test shop پر

1. پہلے verified backup؛ الگ database/photos/configuration کی تصدیق۔ دوسری exe directory اکیلے الگ shop نہیں۔ اصل دکان پر fake sale/return/purchase/payment مت بنائیں۔
2. version **v0.83.0 Snipe** دیکھیں۔ `tools\RUN-TESTS.cmd` میں مطلوب **total1401، failed0**؛ اصل output محفوظ کریں۔ Desktop build/launch الگ لازم؛ یہاں C# نہیں چلایا۔
3. الگ test shop میں پہلے سے موجود حقیقی/محفوظ sample history لیں۔ Products اجازت والے عام staff سے History کھولیں: خرید کی مقدار/تاریخ/supplier برقرار، Rate/Total اور Bought مالی خلاصہ **hidden**۔ مجاز فروخت کے rates/totals اپنی سابق حالت میں۔
4. اسی staff سے Print/WhatsApp کی صرف preparation اور Save CSV آزمائیں۔ خرید کی row اور مجموعے میں cost واپس نہ آئے؛ CSV کے rate_paisa اور total_paisa خالی۔ test PDF کسی حقیقی شخص کو WhatsApp نہ بھیجیں۔
5. مالک/مینیجر سے متعلقہ module کی اجازت کے ساتھ وہی history کھولیں: اصل purchase rate/total درست۔ manager کو Profit grant نہ ہو تو management ہونے سے واضح sale profit خود نہ ملے۔ global OFF/ON سے purchase money unchanged، explicit profit اپنی اجازت کے مطابق hidden/واپس۔
6. Reorder کی اجازت والے عام test staff کو pad، stock/minimum/order quantity استعمال کرنے دیں۔ اس grant سے آخری خرید cost نہیں ملنی چاہیے۔ مالک/مینیجر کے مجاز Core test میں actual LastCostKnown=true؛ جس چیز کی خرید نہیں، unknown الگ۔ موجودہ screen/message میں نئی cost column شامل نہیں کی۔
7. module permission بند کرنے کے بعد **نئی** History print/CSV/report request انکار کرے۔ account بند یا role بدلے تو صحیح active account سے دوبارہ sign in۔ پہلے سے کھلی/کاپی/پرنٹ/export معلومات واپس مٹنے کی ضمانت نہیں۔
8. Dashboard پر عام cashier کے totals اپنی مجاز حالت میں؛ manager کو Profit grant/global switch کے مطابق profit۔ Reports کے daily/top اور نئی document preparation بھی یہی profit policy مانیں۔ اصل محفوظ report کو موجودہ اصل bills کے ساتھ ملائیں؛ نئی فرضی posting کی ضرورت نہیں۔
9. Reorder switched off ہونے سے inactive user کو read کی اجازت نہیں ملنی چاہیے؛ اس negative case کو automated suite میں بھی لکھا ہے۔ صرف menus چھپنا Core permission test کا بدل نہیں۔
10. اصل Windows screenshot/error/output محفوظ کریں۔ scanner، printer، keyboard، DPI اور دکان کی عملی کامیابی source check سے ثابت نہیں ہوئی۔

## اصل جانچ کی حیثیت

**واقعی چلایا:**71 static checks؛376 C# files real grammar سے parsed؛ member resolver میں0 reported problems۔12 منفی source mutations پکڑے گئے اور اصل code بحال۔ History/Reorder/Report/Dashboard کے2/3/14/10 verbatim SQL blocks unchanged۔36 migrations اور Sale/Return/Purchase posting services unchanged؛ آغاز کی618 فائلیں محفوظ۔ الگ memory-only SQLite میں36 scripts، history/BookCheck، deliberate corruption detection، B560/540 اور foreign keys کامیاب۔ اردو main MD/HTML یکساں،36 منفرد اسباق؛ diff check صاف۔

**یہاں نہیں چلایا:**24 نئے C# facts سمیت **1401 لکھے ہوئے cases**؛ .NET SDK/Windows/xUnit/WPF/hardware دستیاب نہیں۔ static/SQLite کامیابی کو runtime/security certification نہ سمجھیں۔

## صاف طور پر باقی خامی/فیصلہ

Reports کی **Dead stock کی Value** ابھی موجودہ Reports اجازت پر stock کی خرید-cost valuation دکھاتی ہے۔ یہ History/Reorder کے آپ کے اس فیصلے میں شامل نہیں تھی؛ اسے چپکے سے بند یا مکمل cost-secret قرار نہیں دیا۔ اس کی اصل ضرورت/اجازت اور دیگر report/export fields کا اگلا review باقی ہے۔ Dashboard کے supplier payable/customer credit اور Reports کے purchase totals بھی اپنی سابق policy پر ہیں۔

یہ supplied account ID کی authentication replacement نہیں۔ کسی arbitrary اندرونی caller کو اصل owner ID دینے کی طاقت ہو تو ID کو session credential نہ سمجھیں۔ PermissionService کی سابق DB-error/default policy نہیں بدلی؛ فوری session/job/file revocation کا دعویٰ نہیں۔ Reorder minimum کے write راستے، Custom fields، TimeCorrection، مکمل finance/stock/concurrency/duplicate/files/backup/cloud اور36 اسباق کے ہر عمل کی جانچ ابھی OPEN۔

## خرابی ہو تو

درست active account اور متعلقہ module permission چیک کریں۔ hidden رقم کو database میں صفر نہ لکھیں؛ عام staff کو محض cost دکھانے کے لیے غیرضروری permissions نہ دیں۔ global profit switch خرید کی الگ management شرط ختم نہیں کرتا۔ پرانی binary یا پرانا backup نئی اصل shop پر ڈال دینا محفوظ rollback نہیں؛ نئی entries ضائع ہوسکتی ہیں۔ schema اس حصے میں نہیں بدلی، پھر بھی verified backup/restore الگ احتیاط ہے۔

**محفوظ رکھنے کی حیثیت:** سابق uncommitted کام برقرار، موجودہ branch کے workspace میں تبدیلیاں محفوظ؛ commit/push نہیں کیا۔ مالی حساب، sale-level Extra، سب کے لیے actual cost floor اور560 پھر540 پالیسی unchanged۔ installer نہیں بنایا؛ اگلا حصہ اسی جاری فیز4 کا ہوگا۔
