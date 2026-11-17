# فیز4 حصہ10 — عام batch list اور تازہ cost تیاری

**v0.88.0 Skylark —2026-11-10۔ PT-30 Windows پر ابھی UNRUN۔** Docs/source checkpoint، نہ installer، نہ production منظوری۔

## مثال / کیا بدلا؟

تین tablets کی batch میں فی tablet خرید123.45 ہو تو پوری batch370.35 بنتی ہے۔ صرف rate چھپا کر پوری valuation چھوڑنا کافی نہیں۔ اب **Stock، Expenses اور Suppliers کے عام batch picker** میں الگ View Purchase Cost + main ON کے بغیر raw cost اور valuation دونوں mask، label **hidden**؛quantity،batch،expiry،supplier اور expiry-band برقرار۔ متعلقہ Stock/Cash/Suppliers page اجازت بھی لازم۔ Manager نام یا صرف Profit tick خرید کی اجازت نہیں۔

Write-off اور Expenses/goods کی valuation preview اب **تازہ مجاز cost/quantity/pack** پڑھتی ہے۔ Cost منع ہو تو hidden،جعلی0 یا پہلے کھلی row کا پرانا cost نہیں۔ Suppliers میں manual **Add this batch to the tray** بھی پہلے تازہ تیاری مانگتا ہے؛اجازت ختم،batch inactive/missing یا دوسرے product کی ہو تو tray کی پہلی row مٹانے سے پہلے انکار۔

اصل0 اور hidden الگ ہیں۔ Effective preview کا پرانا rule برقرار: batch کی positive cost،ورنہ product cost۔ مثال batch0/product456paisa ہو تو اسی پرانے fallback سے456؛یہ accounting formula بدلنے کی اجازت نہیں۔ واقعی کوئی active batch نہ ہو تو Stock/Expenses کی پرانی product-based preview ممکن،مگر اس سے invalid no-stock posting جائز نہیں ہوجاتی۔ Supplier tray کے لیے batch لازمی۔ اگر active batches موجود ہوں مگر selection خالی ہو تو خاموشی سے پہلی batch نہیں اٹھائی جاتی۔

## سب سے اہم حد

**پورے Stock/Expenses/Suppliers صفحے کو ابھی cost-safe نہ سمجھیں۔** انہی صفحات کے history،today totals،posting result/warnings اور دوسرے outputs ابھی الگ باقی ہیں۔ Stock-take کی frozen valuation/print/CSV بھی اگلا کام ہے۔ Supplier paid/payable/Pay/SR کی غیرطے شدہ پالیسی نہیں بدلی۔ موجودہ tray/پہلے تیار document واپس چھپنے کی ضمانت نہیں۔ یہ تازہ read/interaction پر جانچ ہے،instant session/concurrency protection نہیں۔

## محفوظ PT-30 — صرف الگ VM/test data

Verified backup اور واقعی الگ DB/PC ضروری؛صرف exe folder بدلنا isolation نہیں۔ اصل دکان میں fake purchase/write-off/return نہ کریں۔ پہلے سے تیار synthetic fixture استعمال کریں۔

1. `tools\RUN-TESTS.cmd` سے1521 موجودہ cases کا مکمل log،پھر Desktop سمیت `dotnet build Area11.sln -c Release`۔ یہاں یہ دونوں UNRUN ہیں؛پرانا v0.80 نتیجہ نئے source کا نتیجہ نہیں۔
2. Owner/main ON؛Stock/Cash/Suppliers+Cost،Profit نہیں۔ تینوں pickers میں اصل batch quantity/date/cost valuation درست؛owner explicit Cost denial/main OFF پر **نئی batch read** میں hidden۔ Profit-only/manager خودکار خرید نہ دیکھیں۔ اس مشق کو باقی history fields کا PASS نہ کہیں۔
3. ہر workflow کا اپنا page grant بند کرکے نیا batch read کریں؛دوسرے page grants یا Products+Cost اسے bypass نہ کریں۔ Missing/inactive/role-changed account service cases میں انکار۔
4. پہلے کھلا product/batch منتخب رکھ کر Cost بند کریں؛Write-off/Expenses میں quantity/unit بدلیں تاکہ preview تازہ ہو۔ پرانی money valuation کے بجائے hidden/error؛کوئی read خود stock/payment/audit post نہ کرے۔ یہ خودبخود timer-based clearing کی ضمانت نہیں۔
5. Manual supplier tray میں پہلے سے synthetic row رکھیں،پھر Cost/main/page منع کریں اور Add line آزمائیں۔ انکار پر tray کی rows/amounts unchanged۔ batch inactive/missing یا product mismatch ہو تو بھی پہلے والی row نہ مٹے؛اصل Post return نہ کریں۔
6. مجاز حالت میں fixture کی batch cost/quantity اور product pack الگ test connection سے بدلیں؛نئی preview/تیاری تازہ values لے۔ Service کا نتیجہ stock reserve یا بعد کی posting authorization نہیں۔
7. حقیقی مجاز0،product fallback،کوئی batch نہ ہونا،active batch ہوتے ہوئے null selection اور expired/no-date/zero-quantity metadata الگ ملائیں۔ No-selection حالت کسی دوسری batch میں خود نہ بدلے۔
8. DB permission-read failure کی service fixture میں owner کو rawcost نہ ملے؛تیاری ناکام ہو،جعلی0 نہیں۔ Read سے پہلے/بعد stock/expense/drawings/audit counts اور اصل amounts unchanged۔

## یہاں کے ثبوت

**77 static checks PASS**؛388 C# grammar files؛4034calls/7636references،0reportedissues۔10 غلط source mutations رد/بحال۔ نیا in-memory SQLite probe36اصل migrations/SELECTs،filter/ID/read-only/next-read/FK PASS؛دیگر پانچ probes بھی PASS۔ یہ C#/WPF/security PASS نہیں۔

**28 نئے facts،کل1521 C# cases UNRUN۔** Stock کا پورا write/save/history/today tail exact unchanged؛110 دوسرے Core service/data hashes unchanged؛تمام648 baseline files retained۔ اصل Windows build،UI،installer اور ہر راستے کی مکمل اجازتیں ابھی OPEN۔

[تفصیلی trace/حدود](POS-PHASE4-BATCH-READ-REVIEW.md) — [باقی کام اور test installer](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md)۔ ہر مرحلے میں docs+commit/push کی ہدایت برقرار؛push کی کامیابی اصل Git نتیجے سے بتائی جائے گی۔
