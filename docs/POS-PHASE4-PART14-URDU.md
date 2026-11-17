# حصہ14 — Excel/CSV کی الگ اجازت اور Audit میں محدود تفصیل

**v0.92.0 Bittern،2026-11-10۔ PT-34 / Windows UNRUN۔**

## کیا بدلا؟

Staff & Permissions میں موجود `import` checkbox کا واضح نام اب **Allow Excel/CSV Bulk Export & Import** ہے۔ Default صرف Owner؛Manager/Staff کو خودکار اجازت نہیں،Owner کی واضح tick چاہیے۔ پرانی explicit grants برقرار ہیں؛نیا duplicate checkbox یا auto-grant نہیں۔ Products page کی اجازت bulk export کی اجازت نہیں۔ صرف Owner ہی اجازت بدل سکتا ہے۔

**مثال:** خرید123.45 کو0/خالی کرکے editable فائل نہیں دی جاتی۔ Product CSV/XLSX کے لیے bulk tick + Cost + main ON؛پورے mixed shop workbook کے لیے دونوں Cost/Profit + main بھی لازم۔ اجازت کم ہو تو پوری file/preview/apply کا انکار۔ مجاز delegated staff بھی استعمال کرسکتا ہے؛manager عہدہ اجازت کا بدل نہیں۔ اصل import/write SQL،stock،balances اور audit محفوظ کرنے کا حساب unchanged ہے۔

Audit کے لیے آپ کا منتخب اصول:دونوں مالی grants/main کے بغیر **وقت،شخص،عمل،module اور row id**؛entity/old/new/details نہیں۔ اصل database audit مٹتا یا بدلتا نہیں۔ چھپی رقم کو search کرکے ملنے والی row/count سے بھی اندازہ نہ لگے:page اور count دونوں hidden fields نہیں ڈھونڈتے۔ Raw mixed detail کے لیے دونوں مالی grants ضروری ہیں،کیونکہ ایک متن میں مختلف حساس اعداد ہوسکتے ہیں۔

چار CSV راستے—list exports،Audit،Product History،Books check—اب dedicated bulk اجازت چیک کرتے ہیں۔ Audit/History کی CSV **report snapshot** ہے،دوبارہ bulk import کی template نہیں؛اس میں موجود approved redactions برقرار ہیں۔ Editable product/shop workbook مکمل اجازت کے بغیر نہیں بنتی۔ Export سے پہلے نئی service read ہوتی ہے،پرانے cached raw rows سے فائل نہیں بنتی۔

## کھلے صفحے میں اجازت بدل جائے؟

Import/Audit کے فعال view میں nominal3second timer،Loaded پر شروع اور Unloaded پر بند ہوتا ہے۔ متعلقہ اجازت ختم ہو تو stale previews صاف یا Audit metadata میں refresh؛Core Apply/save-file preparation پھر بھی دوبارہ چیک ہوتی ہے۔ Import previews اصل فائل سے دوبارہ بن سکتے ہیں،اس لیے stale derived plans ہٹتے ہیں؛یہ purchase فارم کی unsaved اصل لائنیں مٹانے کا اصول نہیں۔ Timer مؤخر ہوسکتا ہے؛Settings lifecycle/cache برقرار ہے،پہلے سے بنی file/clipboard واپس نہیں مٹ سکتی۔ مکمل session/security ضمانت نہیں۔

## Password backup کا صاف موجودہ حال

آپ نے **A: الگ backup password** منتخب کیا ہے؛اصل password chat میں نہیں دینا۔ [اگلے حصے کا منظورشدہ منصوبہ](SECURE-BACKUP-PASSWORD-PLAN.md) درج ہے۔ **اس حصہ14 میں BackupService نہیں بدلا؛موجودہ ZIP/DB backups encrypted نہیں ہیں۔** انہیں ابھی محفوظ encrypted package سمجھ کر نہ بھیجیں۔ اگلا مربوط کام password setup + protected automatic credentials + authenticated package + restore/safety/rollback ہے۔ Password گم ہو تو پرانا backup ناقابلِ recovery ہوسکتا ہے؛اسی لیے صرف ادھورا encryption نہیں لگایا گیا۔

## محفوظ PT-34 — الگ test data/VM پر

1. `tools\RUN-TESTS.cmd` کے1641cases کا اصل log،پھر Desktop سمیت Release build۔ یہاں Windows/.NET execution نہیں ہوا۔
2. Default manager/cashier سے editable export/upload کا انکار؛صرف Products tick کافی نہیں۔ Owner واضح bulk+Cost دے تو product12345paisa کی CSV/XLSX میں123.45 اصل رقم آئے؛مجاز staff کا import صحیح محفوظ ہو۔
3. Bulk ON مگر Cost OFF یا main OFF:Product file/preview/apply مکمل انکار،masked0 نہیں۔ Mixed shop workbook میں Cost-only بھی کافی نہیں،Profit الگ لازم ہے۔
4. پہلے preview بنائیں،پھر bulk/Cost بند کرکے Apply:کوئی stock/product/payment/monetary audit جزوی write نہ ہو۔ Inactive/role-changed/forged actor بھی انکار؛نئی upload bytes gate سے پہلے decode نہ ہوں۔
5. Audit-only reader کے raw fields blank/hidden؛دونوں grants/main پر اصل old/new/details۔ Cost-only/Profit-only/main OFF پر metadata؛Owner پر بھی یہی مالی شرط۔ Hidden old/new/details/entity کا search صفحے یا count میں جواب نہ دے؛شخص/action search برقرار رہے۔
6. کھلی raw Audit row کے بعد Cost بند کریں؛نیا snapshot/CSV/timer refresh حساس متن نہ دکھائے۔ Bulk نہ ہو تو Audit CSV بھی انکار۔ Import/Audit view بند ہوتے timer رکے۔
7. History/Books CSV بھی bulk grant مانگیں؛Books کی raw CSV دونوں مالی grants اور Reports-or-Audit کے بغیر نہ بنے۔ Export کے دوران permissions بدلیں تو دوبارہ fresh check؛پرانے cached report سے file نہ بنے۔
8. Audit read/export سے پہلے اور بعد اصل stored old/new/details اور business counts unchanged ہوں۔ اصل Windows labels/timers/Excel open-edit-reimport کے screenshots/logs ہی عملی ثبوت ہیں۔ Backup encryption کو ابھی PASS نہ لکھیں۔

## یہاں کی جانچ

**81 static checks PASS؛10 الگ synthetic SQLite probes PASS؛12 منفی source تبدیلیاں رد/بحال۔**393 C# grammar files،4191 calls/8125 references،0reportedissues۔ نیا SQLite probe اصل Audit page/count SQL پر metadata/raw/search/order/limit/read-only/FK جانچتا ہے،C# grants یا WPF نہیں۔

**32 نئے facts؛کل1641 C# cases UNRUN۔**13 Import /1 Workbook /11 BookCheck SQL blocks exact؛Audit writer اور BackupService unchanged؛105 دوسرے Core service/data hashes exact؛664 baseline files برقرار ہیں۔ Installer نہیں بنا۔

Docs + اسی branch پر commit/push لازم؛اصل Git result سے اشاعت ثابت ہوگی۔ باقی اندازہ **4–8 مربوط چکر**،نئی backup requirement اور Windows نتائج پر منحصر؛ضمانت یا upper limit نہیں۔

[تفصیلی trace/حدود](POS-PHASE4-BULK-AUDIT-REVIEW.md) — [باقی کام](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md)۔
