# فیز4 حصہ13 — خرید کا فارم، drafts اور اجازت بدلنے پر ادھورا کام

**v0.91.0 Kingfisher،2026-11-10۔ PT-33 Windows پر UNRUN۔** یہ source checkpoint ہے،نہ installer نہ production منظوری۔

## آسان مثال: hidden کو مفت خرید نہ بنائیں

3 tablets،فی tablet خرید123.45 روپے،کل370.35۔ اگر Cost اجازت بند ہو تو draft میں123.45 کو0 کرکے قابلِ Save فارم دینا خطرناک ہے:اصل خرید مفت محفوظ ہوسکتی ہے۔ اب نئی خرید اور purchase draft **کھولنا/محفوظ کرنا/update/post کرنا** موجودہ Purchases اجازت + الگ **View Purchase Cost** + مشترک main ON کے بغیر انکار کرتے ہیں۔ صرف Profit،manager عہدہ،یا پرانی کھلی ہوئی payload اجازت کا بدل نہیں۔ Owner کی اپنی Cost denial/main OFF بھی مؤثر؛حقیقی zero/sample کی preparation بھی اسی شرط سے۔

**Summary فہرست اور discard الگ:**موجودہ Purchases والا شخص Cost کے بغیر metadata/count/supplier دیکھ اور draft ترک کرسکتا ہے؛اسے raw payload/قیمت نہیں ملتی۔ پہلے سے محفوظ خرید کا نیا invoice حصہ9 کی طرح line/Total/Paid/Due hidden دکھا سکتا ہے۔ Supplier کے عام paid/payable/Pay/SR کا کھلا فیصلہ خود سے نہیں کیا گیا۔

## کھلے فارم میں اجازت بند ہو تو کیا ہوگا؟

- اگلی متعلقہ action/refresh یا view کے nominal3second timer check پر cost/paid editors blank،لائن کی رقم اور total **hidden**،Save/Save draft/entry بند،پرانا مالی status بدل جائے گا۔
- **نامکمل لائنیں اور اصل رقم ضائع یا0 نہیں کی جاتیں۔** اضافی document answers بھی denied حالت میں چھپتے ہیں۔ اجازت واپس ہو تو کام دوبارہ نظر آئے گا؛پیغام کہتا ہے پہلے review کریں۔ خودکار Save/post/reprice نہیں،اور review کی الگ لازمی confirmation ابھی نہیں۔
- Product selection کی تازہ read ناکام ہو تو پرانی دوسری دوا کا rate آگے نہیں چلتا۔ AddLine تازہ pack metadata دیکھتا ہے؛پہلے کی unsaved lines خودکار repricing نہیں۔
- Timer Windows dispatcher سے مؤخر ہوسکتا ہے،Settings کا موجودہ cache/lifecycle برقرار؛بیرونی DB edit فوراً پکڑنے کی ضمانت نہیں۔ یہ memory erasure/مکمل session security نہیں۔ پہلے بنی PDF/کاغذ/clipboard یا returned object واپس مٹ نہیں جاتا۔

## محفوظ PT-33 — صرف الگ test VM/data

اصل دکان پر fake خرید/draft/post نہ کریں۔ Verified backup **اور واقعی الگ DB/PC** لازم؛صرف الگ exe folder کافی نہیں،ProgramData مشترک ہوسکتا ہے۔

1. `tools\RUN-TESTS.cmd`:1609cases کا اصل log،پھر `dotnet build Area11.sln -c Release`۔ یہاں C#/Desktop/Windows نہیں چلا۔
2. الگ fixture میں Purchases+Cost ON،Profit OFF،main ON:3×12345paisa draft کھولیں؛Paid10000 برقرار۔ مجاز direct/post میں Total37035،supplier due27035،stock+3،online payment10000؛اصل invoice اور record ملائیں۔ یہ عامSupplier visibility پالیسی کا نیا فیصلہ نہیں۔
3. Cost OFF/Profit-only/manager بغیرCost/main OFF/owner explicit denial:نیاCreate،Open،Save/update اورPost انکار؛کوئیpurchase/payment/stock/monetary audit تبدیلی نہیں۔ دستی current payload یا پرانا opened input بھی bypass نہ کرے۔
4. Purchases ON مگرCost/main OFF:summary list دستیاب،رقم/payload نہیں؛discard ممکن اورhistory برقرار۔ Purchases OFF/inactive/role-changed/forged actor پرsummary/discard بھی انکار۔ Financial ticks page اجازت کا بدل نہ بنیں۔
5. مجازفارم میں unsaved lines+paid رقم رکھیں؛دوسرے مجاز control سے Cost بند کریں،پھر action/timer۔ تمام cost labels/status/paid blank یاhidden،Save بند،لائنیں نہ مٹیں۔ دوبارہCost/main ON:اصل12345/10000 واپس،review پیغام،خودpost نہیں۔ اصل Windows binding/timer کے screenshots/logs چاہیے؛یہاں source check ہی ہوا۔
6. پہلے سے opened draft کیCost بند کرکےupdate/postکریں:انکار،اصلpayload/status برقرار۔ واپس اجازت دینے پرoriginal amount کھلے،masked0 نہ بنے۔ نیا product lookup fail ہو تو پچھلی دوا کاcost باقی نہ رہے۔
7. حقیقی0 اورsample مجازSave میں CostKnowntrue/اصل0؛defaultunknown labelhidden۔ Successful resultTotalLabel صحیح؛saved invoice نئیpermission کے مطابق الگ تیارہو۔
8. صرفtestfixture:درمیانیproduct invalid ہونے پرpartialstock/purchase نہ بچے،draftretry ممکن؛ایکdraftدو بارpostنہ ہو۔ Permission/read storage failure safe denial،SQLdetailsنہیں۔ موجودہ3transactionmarker/crashmodelکو مکملconcurrencyproof نہ سمجھیں۔

## یہاں کی جانچ

**80 static checks PASS**؛ 391 C# grammar files،4135 calls/7986 references،0 reported issues۔ **12 منفی source تبدیلیاں رد اور بحال** ہوئیں۔ نیا memory SQLite probe:36 migrations اور8 اصل draft SQL statements؛payload،marker،restore،finalize،discard،read-only اور FK جانچ PASS؛8 پرانے probes بھی PASS۔ یہ C# authorization/WPF/runtime/security PASS نہیں۔

**30 نئے facts،کل1609 C# cases UNRUN۔** Purchase کے8 اور Draft کے8 verbatim SQL blocks exact؛Purchase کا اصل private Save/helpers exact؛109 دوسرے Core service/data hashes unchanged؛660 baseline files برقرار۔ Installer نہیں بنا؛Windows/.NET/Inno ابھی یہاں نہیں۔

اگلا مربوط حصہ:generic audit/workbook/CSV اور باقی مالی outputs۔ Supplier2000/paid500/payable1500/PaySR کا policy فیصلہ،StockMinus/session/fault/files/backup/cloud/Windows اور مکمل Phase4 باقی۔ باقی اندازہ **3–7 مزید چکر**،Windows کے نئے مسائل پر زیادہ ہوسکتے ہیں؛ضمانت نہیں۔ ہر مرحلے میں docs + اسی branch پر commit/push لازم۔

[تفصیلی trace/حدود](POS-PHASE4-PURCHASE-PREPARATION-REVIEW.md) — [باقی کام/installer](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md)۔
