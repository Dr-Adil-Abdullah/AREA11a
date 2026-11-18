# PT-42 — transaction کے اندر اجازت درست پڑھنا

**v0.100.0 Sanderling · `#576A45` · Source checkpoint؛ C#/Windows عملی جانچ UNRUN**

مثال: کیشیئر کو عام طور پر Customers دکھائی دیتے ہیں۔ مالک اس کی اجازت بند کرے تو وصولی کے دوران permission پڑھنے میں خرابی آنے پر پروگرام دوبارہ پرانی default اجازت نہ دے۔ دوسری طرف مالک Cash کی خاص اجازت دے تو transaction شروع ہونے سے وہ جائز اجازت غائب بھی نہ ہو۔ اس حصے میں یہی connection اور error کا فرق درست کیا گیا ہے؛ نیا حساب یا permission policy نہیں بنائی۔

[تفصیلی source trace اور حدود](POS-PHASE4-TRANSACTION-PERMISSION-REVIEW.md) · [مکمل اردو کتاب](USER-GUIDE-URDU.md)

## حفاظت پہلے

- اصل دکان میں فرضی بل، وصولی، permission fault یا DB تبدیلی **نہ کریں**۔ Verified backup اور واقعی الگ **DB،photos اور config paths** لازم؛ دوسری exe folder اکیلی isolation نہیں۔ شک ہو تو رک جائیں۔
- PIN،login password،backup password A،keys یا shop DB عام chat میں نہ دیں۔ چلتی دکان پر restore نہ کریں۔
- Developer fault/parallel exercises صرف خودکار isolated fixtures یا ماہر کے زیرِ نگرانی test copy پر ہیں؛ اصل DB میں table drop/rename ہرگز نہیں۔
- اس build کے `tools\RUN-TESTS.cmd` میں متوقع total **1879** ہے۔ **27 نئے facts لکھے ہیں، مگر یہاں C# compile/tests نہیں چلے۔** Desktop/WPF build الگ بھی ضروری ہے۔

## کیا بدلا؟

1. Database کی پانچ command helpers صرف **اپنی connection** کا transaction استعمال کرتی ہیں؛ دوسری connection کو پہلا transaction نہیں دیا جاتا۔
2. Permission read،اسی synchronous transaction کے اندر ہو تو اسی connection سے ہوتی ہے؛ اس transaction میں ابھی کی گئی grant/revoke بھی نظر آتی ہے۔ باہر اپنی connection کھول/بند ہوتی ہے۔
3. عام staff grant پڑھنے میں اصل خرابی ہو تو **اجازت نہیں**؛ کامیاب read میں grant row موجود نہ ہو تو پرانا role-default برقرار ہے۔ یہ دونوں الگ حالتیں ہیں۔
4. اسی Database instance کے synchronous Read/Tx/command callers باری لیتے ہیں؛ دوسرے thread کو پہلے writer کی connection نہیں دی جاتی۔ یہ پورے پروگرام کی concurrency یا session certification نہیں۔

## الگ Windows test copy میں جانچ

| # | عمل | متوقع نتیجہ — ابھی عملی تصدیق باقی |
|---|---|---|
| 1 | Cashier کی Customers اجازت بند، پھر customer وصولی | وصولی منع؛ balance/payment/audit میں اس denied action کی تبدیلی نہیں |
| 2 | Customers جائز؛ Rs100 ادھار میں Rs10 وصول کریں | ایک payment؛ باقی Rs90؛ double posting نہیں۔ اصل دکان میں فرضی مثال نہ چلائیں |
| 3 | Cashier کو Owner خاص Cash grant دے؛ پھر transaction والا جائز Cash action | صرف transaction میں داخل ہونے سے grant غائب نہ ہو؛ باقی current-account/عمل کی شرطیں برقرار |
| 4 | Grant row واقعی غائب اور read کامیاب | پرانا role-default؛ Cashier Customers جائز،Cash خود بخود نہیں |
| 5 | Developer fixture میں grant SELECT خراب،اندر/باہر transaction | ordinary default-allow نہیں؛ financial read بھی fail-closed۔ عام Owner bypass الگ پرانی policy ہے |
| 6 | Fixture میں pending grant/revoke؛ transaction میں فوراً Can پڑھیں | اسی pending state کا نتیجہ؛ دوسری connection کا پرانا committed جواب نہیں |
| 7 | Pending change کے بعد مصنوعی exception/rollback | pending grant باہر leak نہیں،pending revoke rollback کے بعد باقی نہیں؛ اگلا transaction کام کرے |
| 8 | Profit اور Purchase Cost الگ ON/OFF؛Owner financial revoke بھی | دونوں الگ رہیں؛ordinary Owner access سےfinancial checkbox bypass نہیں؛main کی الگ شرط برقرار |
| 9 | پانچ wrapper tests دوسری connection پر query کریں | foreign-transaction exception نہیں؛دوسری connection کو pending writes خود بخود نہیں ملتیں |
| 10 | Own-connection insert/Query/QuerySingle/Scalar/NextId کے بعد fault | writes اکٹھے rollback؛ nested Tx اب بھی منع؛caught read fault سے outer connection بند نہیں |
| 11 | خودکار دو-thread Read/Tx fixtures | دوسرا caller پہلے Tx کے بعد الگ connection سے چلے؛اسے writer کیconnection نہ ملے۔ دوسرےDatabase instances/processes کی concurrency الگ باقی |
| 12 | پورے1879cases،Desktop/WPFbuild اورپچھلےSupplier/Customer/Expense/Shiftworkflows | shared Database کی تبدیلی کے بعد سابقہ tests بھی چلیں؛صرف27نئےtests کافی نہیں |

## کیا نہیں بدلا؟

حساب کے فارمولے/SQL،Supplier کا منتخب B استثنا،الگ Cost/Profit/main،below-cost/Extra/overpayment قواعد،Blackbox/BookCheck،backup password A/format اور stored audit نہیں بدلے۔ Settings cache/reload،عام Can کا Owner bypass اور MapFor کا assignment/default نقشہ مکمل authorization نہیں بنے۔ Session کی اصل شناخت،باہر کے check/query/output races،پرانا کاغذ/PDF/clipboard اورfile-sideeffects کی حدود باقی ہیں۔

Developer کے لیے: Read/Tx callback synchronous رکھیں؛connection باہر نہ رکھیں،بند نہ کریں،Task/reader واپس نہ دیں۔ Callback دوسرے thread کا اسی Database پر کام شروع کرکے اس کا انتظار کرے تو deadlock ہو سکتا ہے؛یہ استعمال مجاز نہیں۔ Returned `ServiceResult.Fail` خود rollback نہیں کرتا؛writes کے بعد rollback کے لیے exception درکار ہے۔

## نتیجہ اور باقی کام

- **89 static checks PASS**؛409 C# grammar files،4433 calls،8799 references،structural0issues۔ Compiler نہیں۔
- **16 موجودہ synthetic SQLite probes PASS**؛C# Database/Can/locking نہیں چلے۔
- **19 source-negative تبدیلیاں رد** اور اصل bytes بحال؛executable C# mutation tests نہیں۔
- Python backup reference میں6 round trips اور24 negatives PASS؛اصل C#/DPAPI/new-PCrestore نہیں۔
-707 baseline paths برقرار؛114 دوسرے Core service/data files،accounting/Shift/backup/Blackbox/BookCheckunchanged۔
- **27 نئے facts /1879 C# cases UNRUN**؛Windows/WPF/hardware/installer کی کامیابی کا دعویٰ نہیں۔

یہ **source اصلاح اور regression tests کی تحریر** ہے،Windows PASS نہیں۔ اس حصے کے static/source-negative اورموجودہ synthetic SQLite/Python backup checks کی تفصیل [trace](POS-PHASE4-TRANSACTION-PERMISSION-REVIEW.md) میں ہے؛یہ C#/Dapper/Windows execution کی جگہ نہیں لیتے۔

اگلے bounded حصے: باقی Settings/native/custom/photo/secret fields اورsession/record access؛cross-service faults/rollback/retry/locking؛files/cloud/اصل encrypted restore/new-PC؛پھرWindows1879cases،WPF/hardware/screenshots اورواپس آنے والی fixes۔ ان کے بعد authorized **واحد sendable double-click test installer**۔ ابھی installer نہیں بنایا۔ تقریباً **3–6 مزید مربوط review/result چکر** مشروط اندازہ ہے،حد یا وعدہ نہیں؛نئی خرابی سے بڑھ سکتا ہے۔
