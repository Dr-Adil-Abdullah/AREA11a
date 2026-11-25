# ⏳ v0.136.0 Windows build — CS0619 کی محدود اصلاح

## اصل ثبوت

مالک کے بھیجے ہوئے Windows/.NET10 log میں موجودہ branch Already up to date تھی۔ تینوں projects restore ہوئے؛Core اور Desktop کی Debug DLLs بنیں،لیکن Tests project compile نہیں ہوا۔ آخری نتیجہ **Build FAILED:1 error،30 warnings،72.16 seconds** تھا:

```text
Area11.Tests/RecordSaveOutcomeTests.cs(169,9): error CS0619:
'Assert.Throws<T>(Func<Task>)' is obsolete:
'You must call Assert.ThrowsAsync<T> (and await the result) when testing async code.'
```

Launcher/full output میں ایک ہی error کی تکرار ہے،دو الگ errors نہیں۔ Tests چلے نہیں؛Desktop DLL بننا app کھولنے،WPF رویے،Release یا installer کی سند نہیں۔ پچھلا ProductService CS0411 اس run میں دوبارہ نہیں آیا اور Core compile ہوا؛اس سے اس compilation رکاوٹ کی نئی تصدیق ملی،مگر متعلقہ runtime رویے کی نہیں۔ اصل ذاتی Windows path نقل نہیں کیا گیا؛raw log مالک کے پیغام میں ہے۔

## وجہ اور v0.136.1 Betony کی اصلاح

`AcknowledgedSaveCannotBeReenteredAfterSimulatedRefreshFailure` ایک synchronous ٹیسٹ ہے۔ اس کی expression-bodied `() => throw ...` lambda کسی value کو واپس نہیں کرتی،مگر C# اسے Task-returning delegate میں بھی تبدیل کرسکتا ہے؛xUnit2.9.2 کا overload selection obsolete `Func<Task>` تک پہنچا اور CS0619 نے build روک دیا۔ پچھلے syntax/static checks overload resolution نہیں کرتے تھے،اس لیے اسے نہیں پکڑ سکے۔

اسی lambda کو واضح `(Action)(() => throw ...)` cast دیا ہے،تاکہ لازماً synchronous `Action` overload منتخب ہو۔ `ThrowsAsync` یا async test نہیں بنایا کیونکہ یہاں نہ Task ہے،نہ asynchronous کام۔ Exception type/message،حقیقی service save،acknowledgement،بعد کے retry-block/audit/state assertions اور تمام **72 parent-save cases** برقرار ہیں؛کل متوقع suite **3397** ہی ہے۔ یہ test-only fault simulation ہے،اصل WPF callback execution نہیں۔

`tools/check_repo.py` کے موجودہ parent-save contract میں اسی synchronous callback کا محدود regression شامل؛existing negative script میں expression-bodied واپسی،assertion ہٹانا اور async lambda کی3 نئی mutations شامل ہیں۔ یہ lexical probes ہیں،C# compiler کا بدل نہیں۔ Production service/UI logic،financial SQL،database/migrations،business policy اوردیگر C# tests نہیں بدلے؛صرف لازمی version/colour/icon/script banners اورحالت کی دستاویزات ساتھ بدلی ہیں۔

## جانچ اور حدود

✅128 static checks،موجودہ parent-save negative script کی48 lexical mutations (ان میں3 نئی)،exact source restoration،rules-matrix parity اور`git diff --check` پاس۔ Source preservation کے الگ مقابلے میں باقی تمام services/UI/migrations/tests برقرار؛اسی ایک test کی assertion اوروضاحتی comment بدلے،case count نہیں۔ ⏳اصل Windows rerun ابھی باقی ہے۔ مقامی `dotnet build Area11.sln -c Debug --no-restore` کوشش exit127 (`dotnet: command not found`) پر رکی؛نہ compiler چلا،نہ3397 C# tests پاس ہونے کا دعویٰ ہے۔

اصل run کی **30 warnings حل نہیں کی گئیں**۔ ان میں Core nullability،Tests nullability/xUnit2031/xUnit1031،Desktop میں inherited `CheckAccess` hiding اورPrintPreview/Purchases nullability شامل ہیں؛WPF temporary/main compilation میں کچھ warnings دہرائی گئیں۔ انہیں خاموش یا safe قرار نہیں دیا گیا۔

## مالک کا اگلا قدم

1. پرانا build log الگ محفوظ رکھیں،پھر معمول کے `START-AREA11.bat` کو چلائیں؛نیا banner **v0.136.1 (Betony)** ہونا چاہیے۔
2. Build کامیاب ہو تو الگ `tools/RUN-TESTS.cmd` چلائیں اور آخری summary یا مکمل failure blocks بھیجیں؛3397 متوقع cases ہیں،ابھی کامیاب نتیجہ نہیں۔ تجرباتی fault/posting صرف پہلے سے منظورشدہ الگ test copy میں،حقیقی shop data پر نہیں۔
3. Build دوبارہ ناکام ہو تو نئی پہلی error اور آخری counts بھیجیں۔ نئی error کو اسی پرانے blocker کا نام نہ دیں۔

⏳5.1 کے Custom Add/Move،باقی Settings/registry/StockTake اورحصوں1تا5 کے runtime gates باقی ہیں۔ ❌اس اصلاح میں حصہ6 یا installer شروع نہیں ہوا۔ پرانے checkpoints کی گنتیاں اپنے وقت کے تاریخی ثبوت ہیں۔

## بعد کا اصل نتیجہ — v0.136.1

✅ مالک کے اگلے Windows/.NET10runمیںCoreاورTestsDebugcompileہوئے؛CS0619دوبارہنہیںآیا،3397testsواقعیچلے۔ نتیجہ3395PASS/2FAIL/0skip،5m59s؛نہپوراgreenنہDesktop/WPFنئیتصدیق۔ ⏳[دونوںناکامیاںاورv0.136.2کیحدود](WINDOWS-V1361-TEST-REPORT-URDU.md)۔ اوپرrerunباقیوالےجملےاساصلاحکےوقتکیحالتہیں؛نیاpendingrerunابv0.136.2کاہے۔
