# ⏳ اصل v0.134.0 Windows build failure — CS0411 کی محدود source اصلاح

## 1.1 — مالک کے بھیجے ہوئے اصل نتائج

v0.134.0 Eglantine؛موجودہ branch Already up to date؛restore تینوں projects کے لیے کامیاب۔ Build **FAIL:1 error،3 warnings،36.79 seconds**۔ اصل error:

```text
ProductService.cs(570,17): error CS0411:
The type arguments for method 'Database.Tx<T>(Func<SqliteConnection, T>, TransactionOutcome?)'
cannot be inferred from the usage. Try specifying the type arguments explicitly.
```

Launcher میں یہی line دو بار چھپی ہے؛یہ دو الگ compiler errors نہیں۔ یہ actual Core compile failure ہے،runtime/test failure نہیں۔ اس run میں tests یا app نہیں چلے؛نہ اس سے پرانے22 failed tests کے حل کی تصدیق ملی۔

تین warnings الگ اورابھی unresolved:

- `SingleInstanceGuard.cs(68,13)` — CS8602،possibly null dereference۔
- `ImportService.cs(585,38)` — CS8602،possibly null dereference۔
- `TimeCorrectionService.cs(169,33)` — CS8601،possible null assignment۔

Warnings اس دکھائے گئے build کو روکنے والی1 error نہیں؛انہیں safe یا resolved نہیں کہا گیا۔ اصل raw log مالک کے پیغام میں ہے؛یہاں relative paths کے ساتھ ضروری معلومات محفوظ ہیں،ذاتی Windows folder/credentials نہیں۔

## 1.2 — اصل وجہ

`ProductService.Update` کی `_db.Tx(connection => { ... }, outcome)` call میں callback کا no-op راستہ `return;` تھا اورآخر میں value بھی نہیں لوٹتی تھی۔ Database میں `Tx(Action<SqliteConnection>)` صرف ایک argument لیتا ہے؛`outcome` والا overload `Tx<T>(Func<SqliteConnection,T>, TransactionOutcome?)` ہے۔ اس void callback سے `T` infer نہیں ہوسکتا۔ یہ پچھلے static/grammar checks سے نہیں پکڑا گیا؛ان کا PASS کبھی C# compiler کی سند نہیں تھا۔

## 1.3 — v0.134.1 Borage میں محدود source fix

1. صرف اسی method کی call کو `_db.Tx<object?>(connection =>` کیا۔
2. no-op کے `return;` کو `return null;` کیا۔
3. callback کے عام اختتام پر `return null;` شامل کیا۔

Null صرف generic transaction callback کی ignored return value ہے؛public `ServiceResult.Success()` اب بھی transaction واپس آنے کے بعد ہے۔ `outcome` اب بھی اسی Tx میں جاتا ہے؛Owner/current access،CAS،custom answers،audit،SQL،price/cost/stock/business rules یا transaction body کی ترتیب نہیں بدلی۔ Global Database overload میں تبدیلی نہیں کی۔ تمام پرانے C# tests/37 migrations برقرار؛نیا C# case نہیں،suite count3347 ہی۔

## 1.4 — محدود جانچ اور حدود

- ✅126 repository static checks؛نیا narrow source contract explicit generic اورno-op/final value-return محفوظ رکھتا ہے۔
- ✅3 نئی lexical regressions reject/exact bytes restored؛کل33 negative scripts پاس۔ یہ compiler نہیں۔
- ✅43 دوسرے untyped two-argument block-lambda Tx call sites کا grammar-based source scan؛اسی void-return pattern کا دوسرا suspect نہیں ملا۔ Expression lambdas/local functions الگ سمجھے؛اسےپورےrepo کیgeneric type-checking نہیں کہتے۔
- ✅ProductService کے باقی متن اوراس Update body کو انہی3 edits کوواپس کرکے پرانے source سے exact ملایا؛SQL/DB/session/financial services/old tests/migrations unchanged۔
- ⏳یہاں `dotnet` SDK نہیں؛اصل local Release build exit127۔ مالک کا تازہ Windows build rerun لازم؛نہfix compile-certified نہ3347suite PASS۔

## 1.5 — اب اگلا قدم

اسی branch پر update کے بعد launcher **v0.134.1 Borage** دکھائے؛پھر `START-AREA11.bat` سے build دوبارہ کریں۔ کامیاب build کے بعد الگ `RUN-TESTS.bat` سے تازہ3347 expected cases کی اصل summary درکار؛اگر اس سے پہلے دوسری compile error نکلے تو وہ اصل پہلی error حل کریں۔ Verified backup/واقعی الگ DB/photos/config کی شرط برقرار؛اصلshop پر experimental posts/faults نہیں۔

CustomField Update implementation فی الحال paused؛یہ اصل build blocker پہلے۔ حصے1تا5 کیباقیclosure،warnings review،Windows/provider/WPF/آلات اورحصہ6/installer اپنی جگہ کھلے ہیں۔ موجودہsource/docs/version/icon/scripts اسی branch پرpublish اورindependentremoteverify کی اصل حالت آخری مختصر رپورٹ میں ہے۔
