# حصہ16 — Stock Minus کی مالی معلومات اور موجودہ اجازت

**v0.94.0 Cormorant · 2026-11-10 · PT-36**

## کیا بدلا؟

مثلاً پرانے ریکارڈ میں3 گولیاں کم ہیں،خرید cost12345 پیسے فی گولی،valuation-37035 پیسے ہے۔ جس شخص کو Stock page کی اجازت ہے مگر **View Purchase Cost + main switch** نہیں،اسے دوا،مقدار،کب سے کم ہے،وجہ اورreview کس نے کیا نظر آ سکتا ہے؛**خرید valuation اورکل مجموعہ hidden** ہوں گے۔ اندر لوٹنے والی raw رقم بھی masked ہے،صرف سکرین کا رنگ نہیں بدلا۔ مجاز حقیقی0 اورنامعلوم/ممنوع رقم الگ ہیں۔

- Manager عہدہ یا صرف Profit tick سے purchase cost نہیں ملتی۔ Owner کے لیے بھی اس کا الگ Cost tick اورmain switch لازم۔
- Review کا note اصل Audit details سے آتا ہے؛اس لیے **دونوں مالی اجازتیں + main** نہ ہوں تو raw note واپس نہیں آتا۔ Review کی تاریخ،شخص اورحالت metadata ہیں۔ اصل stored Audit note نہیں بدلا۔
- Report پڑھنے اورnote لکھنے میں موجودہ active account،role اورStock اجازت چیک۔ Review لکھنے کا check transaction کے اندر،اورAudit میں موجودہ stored نام۔
- **Review اب بھی stock نہیں بدلتا۔** پہلے سے مجاز Stock grantee note لکھ سکتا ہے؛Cost/Profit کوnote لکھنے کی نئی شرط نہیں بنایا۔ اصل correction Owner کے physical stock-take سے۔
- Refresh/denial پر پرانی selected row،رقم اورsummary صاف؛active view میں nominal3second access check۔ Dispatcher،Settings cache اورپہلے بنےobjects/files کی فوری recall کی ضمانت نہیں۔

**اصل valuation/negative-stock SQL،episode calculation،batch quantities،Audit writer،money posting اورpassword backup اس حصے میں نہیں بدلے۔** [مکمل trace/حدود](POS-PHASE4-STOCK-MINUS-REVIEW.md)۔

## واقعی کیا جانچا؟

| جانچ | نتیجہ |
|---|---|
| Static grammar/member/XAML/source checks | **83 PASS**؛compiler/runtime نہیں |
| Source guard کوجان بوجھ کرخراب کرکےدیکھنا | **12 mutations رد،اصل bytes بحال** |
| اصل SQL والے synthetic SQLite probes | **11 PASS**؛نیا StockMinus probe شامل |
| الگ Python backup format model | **6 round trips +24 negative PASS**؛C# نہیں |
| نئے C# tests | **24 facts لکھے**؛کل **1716 cases یہاں UNRUN** |
| Windows/WPF/DPAPI/حقیقی restore/installer | **UNRUN؛installer نہیں بنا** |

680 baseline files برقرار،114 دوسرے Core service/data hashes exact،4 StockMinus SQL blocks اورepisode/helper tail exact۔ یہ all-route security یاproduction approval نہیں۔

## PT-36 — صرف واقعی الگ test ماحول میں

Live shop پر فرضی sale،منفی stock،stock correction یاrestore نہ کریں۔ **دوسری exe folder الگ data نہیں بناتی۔** پہلے verified backup اورواقعی الگ VM/DB/photos؛negative stock صرف تیار کردہ historical synthetic fixture میں۔ Windows پر `tools\RUN-TESTS.cmd` چلائیں؛متوقع **1716 cases**،مکمل اصل log محفوظ کریں۔ WPF build/launch الگ جانچ ہے۔

1. Owner اورStock-only staff سے registry کھولیں؛دوا،quantity،episode اورreview metadata یکساں،staff کی row valuation،total،summary اورselected detail میں hidden۔
2. Manager اورProfit-only کوCost خود نہ ملے۔ Cost-only کوصحیح valuation،مگرraw Audit review note hidden؛دونوں مالی اجازتوں اورmain پرnote واپس۔
3. Main OFF پرOwner بھی valuation/note نہ دیکھے۔ Owner کا صرف Cost tick بند کریں؛admin access باقی،valuation hidden۔
4. Stock page grant بند ہو توCost tick اس کا متبادل نہ بنے؛report/access snapshot رد،selected details اورپرانے totals صاف،review button بند۔
5. کھلی view میںCost،Profit یاStock grant تبدیل کریں؛اگلے active check/interaction پرمناسب mask/clear۔ تین seconds سخت deadline نہیں؛unload پرtimer بند،واپسی پرچیک۔
6. Unknown،deactivated،changed-role یاforged Owner fixture سےread/review رد؛provided User object کوخودبخود authenticated session نہ سمجھیں۔
7. Actual cost0 والی historical minus fixture اورempty registry میںمجاز0،بغیر اجازت hidden؛query failure میںsuccessful empty0 نہیں۔
8. Stock-only مجاز شخص کا جائز review note قبول؛quantity/cost/movements unchanged۔ اس کا authored unsaved note خود auto-save نہ ہو۔
9. Review سے پہلے Stock grant revoke؛audit note نہ بنے۔ Stored name بدلے توAudit میںتازہ نام،نہ stale DTO name۔
10. Synthetic Audit failure پرtransaction ناکام،نہ note اورنہ stock movement؛UI میںraw SQL/exception کی حساس عبارت نہیں۔ اصل Audit record hidden read سے نہ بدلے۔
11. Minus→zero→دوبارہ minus کی پرانی نمائشی episode fixtures،latest review،age/order اورپرانے review کی حالت ملائیں؛اصلاح صرف اصل physical stock-take workflow سے۔
12. [PT-35 encrypted backup/restore](POS-PHASE4-PART15-URDU.md)،اصل password rotation،DPAPI/ACL اورواقعی الگ new-PC restore ابھی لازمی open gates ہیں۔ نئے source کوWindows proof کے بغیر production-safe نہ کہیں۔

## کیا باقی؟

Stock Minus کا یہ محدود source حصہ شامل ہے۔ باقی financial outputs،Supplier paid/payable/Pay/SR کا unanswered فیصلہ،session/record/stale access،concurrency/duplicates/faults،files/cloud اورWindows/hardware/installer باقی ہیں۔ اندازہ تقریباً **3–6 مزید مربوط review/result چکر**؛Windows نتائج سے بڑھ سکتا ہے،وعدہ نہیں۔ Docs اوراسی branch پرcommit/push لازم؛اصل publication صرف Git remote verification سے ثابت ہوگی۔
