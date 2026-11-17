# فیز4 حصہ9 — خرید invoice اور expired stock کی محدود cost حفاظت

**ماخذ:** v0.87.0 Waxwing،2026-11-10۔ **PT-29 ابھی Windows پر نہیں چلایا۔** یہ installer یا live shop میں استعمال کی منظوری نہیں۔

## روزمرہ مثال / کیا درست کیا

فرض کریں تین tablets کی خرید370.35 روپے ہے؛100 ادا کیے،270.35 باقی۔ صرف line کی رقم چھپا کر Paid/Due دکھائیں تو خرید370.35 پھر بھی معلوم ہوجاتی ہے۔ اب **نیا purchase invoice تیار ہوتے وقت** خرید دیکھنے کی اجازت نہ ہو تو line amount، Total،Paid،Due سب **hidden**۔ medicine،quantity،unit،batch،expiry،supplier اور invoice reference برقرار۔

Suppliers کی expired list میں quantity/batch/date دیکھ سکتے ہیں، مگر ممنوع cost اور valuation **hidden**؛ اندرونی read result بھی اصل cost نہیں بھیجتا۔ اجازت کے ساتھ اصل0، ممنوع خرید کے جعلی0 سے الگ پہچانی جاتی ہے۔ **Add whole batch** نئی موجودہ expired row دوبارہ پڑھتا ہے؛ اجازت ختم ہو یا row نہ ملے تو return tray چھیڑنے سے پہلے انکار۔ یہ زیادہ سے زیادہ500 والی موجودہ list ہے؛ اس سے باہر کی row بھی محفوظ انکار ہے۔

دو الگ **View Profit / View Purchase Cost** ticks برقرار؛ متعلقہ page بھی لازم، مشترک main switch دونوں کو روکتا ہے۔ manager نام خرید دیکھنے کی اجازت نہیں۔ main OFF یا owner کی اپنی cost tick منع ہو تو اس محدود read پر owner کو بھی رقم نہیں ملتی۔

## حدود پہلے سمجھیں

یہ **پورے supplier account کی paid/payable/Pay/SR پالیسی کا فیصلہ نہیں**۔ Supplier ledger totals،عام batch API،purchase entry/drafts/results،دوسرے stock/expense/export راستے ابھی الگ باقی ہیں۔ پہلے سے کھلا document،tray،کاغذ یا PDF واپس چھپنے کا دعویٰ نہیں؛ اس تبدیلی سے نیا document/read تیار ہونے پر جانچ ہوتی ہے۔ پہلے سے بنا tray اور براہ راست return posting APIs اس نئی cost gate کے مکمل دائرے میں نہیں۔ اصل Purchase save،stock،Supplier Pay/Return اور B560/540 حساب نہیں بدلا۔

## محفوظ PT-29 — صرف حقیقی الگ test data پر

پہلے verified backup **اور** واقعی الگ test database/profile؛ صرف executable folder بدلنا data isolation نہیں۔ اصل دکان میں fake purchase،payment یا return مت کریں۔ پہلے سے تیار synthetic fixture استعمال کریں؛ اسے live shop میں بنانے کی ہدایت نہیں۔ Windows source/build دستیاب ہونے کے بعد:

1. `RUN-TESTS.cmd` چلائیں؛1493 cases اور اصل مکمل result/log محفوظ کریں۔ ابھی ان کے PASS ہونے کا دعویٰ نہیں۔ v0.80 کے پرانے1333PASS/4FAIL نئے نتیجے نہیں۔
2. Owner/main ON؛Purchases+Suppliers+Cost دیں،Profit نہ دیں۔ الگ test fixture کے purchase invoice میں درست line/Total/Paid/Due؛expired list میں صحیح valuation ملائیں۔ حقیقی0 کا مطلب0 ہی رہے۔
3. صرف Cost بند،page grants برقرار؛ **نیا** invoice/read تیار کریں۔ رقم کی جگہ hidden؛Paid+Due سے total دوبارہ نہ نکلے؛نام/quantity/batch/date برقرار۔ Profit tick دینے سے خرید واپس نہ آئے۔ Main OFF پر Cost tick کے باوجود hidden؛manager پر بھی یہی۔
4. Owner کی explicit اپنی Cost tick بند کرکے نیا read بھی چیک کریں؛main ON واپس کرنا cost denial ختم نہ کرے۔ پھر اصل test permissions بحال کریں۔
5. Expired list کھلی رکھ کر دوسرے owner session سے Cost بند کریں؛پرانے selection پر Add whole batch آزمائیں۔ انکار اور **tray کی پہلے والی rows/quantity/cost unchanged**۔ اکاؤنٹ inactive،Suppliers grant بند،selected batch no longer returned یا500حد سے باہر ہو تو بھی انکار؛stale selection سے کوئی دوسری row نہ اٹھے۔
6. اجازت واپس،تازہ list/selection؛test fixture کے اصل zero-cost batch کی تیاری کو hidden-cost refusal نہ سمجھیں۔ اس مرحلے میں **Post return/Pay نہ کریں**؛عام ledger payment پالیسی ابھی الگ سوال ہے۔
7. Test-service cases میں null/missing/forged-role/inactive actor اور Purchases grant ختم ہونے پر invoice تیار نہ ہو؛Cost main flip/revocation کے بعد **اسی service سے نیا** document دوبارہ جانچا جائے۔ SQL failure پر raw database details screen/document میں نہ آئیں۔
8. Windows printer/PDF/text/WhatsApp preparation میں نئے masked document کی یکساں values چیک کریں؛اصل recipient کو test message نہ بھیجیں۔ پرانی PDF کو permissions بدلنے سے واپس محفوظ سمجھنے کا دعویٰ نہ کریں۔
9. Test DB کے purchase/payment/batch/supplier/audit counts/amounts read سے پہلے/بعد ملائیں؛کوئی save یا posted money mutation نہیں ہونی چاہیے۔ Screenshot صرف حقیقی Windows result کا؛نام/PIN/password recovery راز چھپائیں۔

## یہاں واقعی کیا چلا

-76 source/static checks PASS؛387 C# files grammar،3993 calls/7539 references،0reportedissues۔
-10 جان بوجھ کر غلط source mutations guard نے رد کیے،اصل فائلیں بحال۔
-نیا synthetic in-memory SQLite probe:36 اصل migrations اور اصل invoice/expired SELECTs،filter/boundID/read-only/FK PASS۔Python display model **C# permission یا receipt test نہیں**۔پرانی BookCheck/Extra/permission-inspector/time SQL probes بھی PASS۔
-24 نئے C# tests لکھے؛کل1493 **UNRUN**۔یہاں dotnet/Windows نہیں؛build،xUnit،WPF،printer/PDF/runtimesecurity PASS نہیں۔
-Purchase کے8/Supplier کے16 SQL blocks اور Purchase save/Supplier payment-return code baseline سے unchanged؛44 data/financial hashes unchanged،تمام643 baseline files retained۔No installer/commit/push۔

[تفصیلی trace/ثبوت/باقی خطرات](POS-PHASE4-PURCHASE-READ-REVIEW.md)،[جامع مالی rollout](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md)،[اجازتوں کی مدد PT-28](POS-PHASE4-PART8-URDU.md)۔**Phase4 اور ہر راستے پر مکمل مالی اجازتوں کا نفاذ ابھی OPEN ہے۔**
