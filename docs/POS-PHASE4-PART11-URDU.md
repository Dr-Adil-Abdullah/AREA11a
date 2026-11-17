# فیز4 حصہ11 — stock history اور frozen stock-take کی مالی حد

**v0.89.0 Redstart،2026-11-10۔ PT-31 حقیقی Windows پر UNRUN۔** یہ source checkpoint ہے،installer یا production منظوری نہیں۔

## آسان مثال / کیا مکمل کیا؟

Stock-take کھلتے وقت کتاب میں10 tablets اور frozen خرید123.45 روپے تھی۔ آپ نے7 گنیں؛3 کی کمی370.35 روپے۔ بعد میں نئی خرید999.99 ہونے سے **اس پرانی گنتی کا frozen حساب نہیں بدلنا چاہیے**۔

اب Cost permission + main ON نہ ہو تو:
- Write-off history کی رقم،آج کا loss total اور نئے save result کی رقم **hidden**؛quantity/reason/date برقرار۔ اصل مجاز0 کو denied رقم کاجعلی0 نہیں بنایا جاتا۔
- Stock-take Current/History summary کے Short/Extra،sheet کی valuation،نئے print document کا net Value اور Open/Apply result **hidden**۔ Book/count/difference اور progress برقرار۔
- Found-on-shelf search بھی raw خرید نہیں بھیجتی۔ متعلقہ Stock یا StockTake page grant لازم؛manager/Profit-only خرید کا بدل نہیں؛main OFF/owner کی اپنی Cost denial بھی نافذ۔

**اندرونی frozen cost اور اصل محفوظ حساب نہیں چھپایا/مٹایا۔** Count/Apply اصل database کے frozen اعداد لیتے ہیں،نہ screen کا masked0۔ جائز stock operation کے لیے Cost tick کو نئی لازمی posting اجازت نہیں بنایا؛صرف مالی output چھپا ہے۔ Open/Apply/WriteOff کے اندر موجودہ account/page پہلے چیک،projection اسی transaction میں،بعد میں الگ failure سے posted کام کو دوبارہ Save کرنے کا خطرہ نہیں بنایا۔

نئی sheet document کی query ناکام ہو تو successful empty sheet نہیں ملتی۔ Refresh ناکام ہو تو پچھلی کامیاب session/rows کو fallback رکھنا ختم؛لیکن پہلے کاغذ،PDF،status message یا object واپس recall ہونے کی ضمانت نہیں۔ یہ authenticated session/full concurrency تحفظ کا مکمل متبادل نہیں۔

## محفوظ PT-31 — صرف واقعی الگ test VM/DB

Verified backup + الگ test data ضروری؛الگ exe folder وہی ProgramData کھول سکتا ہے۔ اصل دکان میں فرضی write-off/count/Apply نہ کریں۔ پہلے سے تیار synthetic fixture یا الگ test suite استعمال کریں۔

1. `tools\RUN-TESTS.cmd`،متوقع1551cases؛پھر `dotnet build Area11.sln -c Release` سے Desktop بھی۔ مکمل اصل logs رکھیں۔ یہاں یہ دونوں نہیں چلے۔
2. متعلقہ page+Cost/main ON،Profit OFF پر stock history/today اور frozen sheet/summary/print کی اصل رقم ملائیں۔ Cost OFF/Profit ON،manager،main OFF،owner explicit denial پر نیا read/output hidden؛گنتی/فرق/تاریخ برقرار۔ عام Cash grant Stock/StockTake کا بدل نہ بنے۔
3. اصل0،uncounted line اور hidden الگ دیکھیں: مجاز uncounted **—**،مجاز گنی ہوئی zero valuation **0**،ممنوع **hidden**۔ Today بھی حقیقی مجاز0،hidden سے الگ۔ Differences-only count/quantity کے مطابق وہی rows رکھے۔
4. Test-service fixture میں main OFF پر WriteOff کریں؛returned loss hidden لیکن stored adjustment حقیقی cost،stock صرف ایک بار کم،audit موجود۔ خراب trigger/audit fault پر مکمل rollback،fake masked success نہیں۔ یہ صرف الگ synthetic fixture کی آزمائش ہے۔
5. Test fixture میں masked Open→Count→Apply چلائیں؛stored frozen cost12345paisa اور counted7/book10 سے shortage37035paisa برقرار۔ Live cost99999 کرنے پر بھی پرانا frozen حساب وہی۔ Apply result hidden ہو مگر DB totals اصلی؛دوبارہ Apply انکار۔
6. Account inactive/role changed یا page grant ختم کرکے نیا read/print اور تین named write wrappers آزمائیں؛انکار،no unauthorized write۔ Cost DB-check failure پر owner role کو مالی fallback نہ ملے۔ Local user ID کو authenticated session کا ثبوت نہ کہیں۔
7. کھلی screen سے Refresh/Count کے بعد history/session read ناکام ہو تو پرانی session/rows مالی اعداد نہ رکھیں۔ نئی document میں query error پر successful خالی slip نہ بنے،SQL details نہ آئیں۔ پہلے printed کاغذ واپس چھپنے کا دعویٰ نہیں۔
8. Read/print preparation سے stock/stock-take/audit records اور اصل amounts نہ بدلیں۔ حقیقی Windows screenshot/log ہی ثبوت؛scanner/printer/DPI الگ عملی جانچ۔

## صاف حدود / اب کیا باقی؟

الگ StockTake CSV builder source میں نہیں ملا؛اس لیے CSV complete کا دعویٰ نہیں۔ Generic workbook/CSV،audit،backup/cloud اور user notes میں محفوظ رقم کا الگ review باقی ہے۔ Expense/drawing history/results/vouchers،purchase entry/drafts/results،ordinary Supplier paid/payable/Pay/SR اور StockMinus بھی باقی۔ Supplier کی غیرطے شدہ پالیسی خود سے نہیں بدلی۔

## یہاں واقعی کیا چلا؟

**78 static checks PASS**؛389C#grammarfiles،4066calls/7764references،0reportedissues۔12غلط source mutations رد/بحال۔ نیا synthetic in-memory SQLite probe36اصل migrations/freeze/count/totals/read SQL سے frozen12345→short37035/extra400/net-36635 برقرار؛چھ پرانے probes بھی PASS۔ یہ C#/WPF/security PASS نہیں۔

**30 نئے facts؛کل1551 C# cases UNRUN۔** Stock کے9/StockTake کے18 SQL blocks unchanged؛اصل Save/freeze/Count/Apply اندرونی حساب exact،109دوسرے Core service/data hashes unchanged؛تمام652 baseline files retained۔ Installer نہیں بنا۔ Docs+commit/push ہر مرحلے کی ہدایت برقرار۔

[تفصیلی trace/حدود](POS-PHASE4-STOCK-VALUATION-REVIEW.md) — [باقی منصوبہ/محفوظ installer](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md)۔
