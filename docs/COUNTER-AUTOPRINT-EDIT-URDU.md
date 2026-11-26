# ⏳ PT-81 — Counter کا محفوظ پرنٹ انتخاب؛startup registry کا الگ کھلا مسئلہ

**نسخہ:** v0.142.0 Vervain · **تاریخ:** 2026-11-10 · **دائرہ:** اسی منظورشدہ5.1 کا چھوٹا حصہ۔
یہ پوری Settings،حصہ5،Windows یا installer کی تکمیل نہیں۔

## ⏳ مسئلہ اور سورس میں تبدیلی

- پرانا `AutoPrint` setter پہلے checkbox/local bool بدلتا،پھر imperative `Settings.Set` کرتا تھا۔ Save ناکام ہونے پر بھی Counter نیا انتخاب استعمال کرسکتا تھا؛کھلے ہوئے انتخاب کی baseline بھی نہیں چیک ہوتی تھی۔
- اب صرف `printer.autoPrint` کا authorized `LoadEditForm` ابتدا میں DB سے پڑھتا ہے،مشترکہ cache سے نہیں۔ `SettingsEditForm` اسی صفحے میں برقرار رہتا ہے؛تبدیلی `SaveEditForm` سے اسی baseline پر ہوتی ہے۔ دوسری جگہ key/value/presence بدل جائے تو خاموش overwrite نہیں۔
- Read اور Save دونوں interactive session اور current POS access چیک کرتے ہیں؛اصل Settings transaction بھی موجودہ actor/permission/CAS چیک کرتی ہے۔ Settings کا POS-only ایک-key استثنا برقرار؛اضافی keys کا دروازہ نہیں کھولا۔
- `AutoPrint` صرف کامیاب service جواب،commit acknowledgement اور واپس آنے والی non-null/raw-boolean receipt کی تصدیق کے بعد بدلتا ہے۔ یہی immutable receipt اگلے Save کی baseline ہے،بعد کا تازہ read اس کامیابی کی جگہ نہیں لیتا۔
- Callback/reentrant setter/read کو synchronous `_savingAutoPrint` اور مستقل `PostingAttemptGuard` روکتے ہیں۔ Acknowledgement پہلے،local handoff/notifications/status اور retained-value verdict بعد میں،صرف مکمل کامیابی پر guard کھلتا ہے۔

## ⏳ ناکامی میں صاف فرق

| نتیجہ | اسی Counter کا انتخاب | اگلا قدم |
|---|---|---|
| Save کامیاب اور handoff مکمل | receipt کا محفوظ bool؛اگلی تبدیلی جائز | عام کام جاری |
| Service کا واضح `NoCommitConfirmed` | پچھلا دکھایا ہوا/runtime انتخاب برقرار؛checkbox کی مزید ترمیم بند | **Read print choice** سے صرف یہ preference پڑھیں،موجودہ حالت دیکھ کر نیا فیصلہ کریں |
| Save نتیجہ نامعلوم،یا service exception | اسی Counter میں automatic printing بند؛baseline ختم،guard بند | تبدیلی نہ دہرائیں؛اصل Settings/audit کی تصدیق کریں،پھر Counter دوبارہ کھولیں |
| SAVED کے بعد receipt/notification/status خراب | **SAVED** الگ بتایا جاتا ہے؛local automatic printing بند،guard بند | محفوظ کام کو ناکامی نہ سمجھیں؛تصدیق کے بعد reopen |
| آغاز/explicit read ناکام | local automatic printing بند؛checkbox بند | access درست ہونے پر **Read print choice**؛یہ خود Save نہیں کرتا |

`Ok=false` خود no-commit کا ثبوت نہیں۔ Cache کی post-commit خرابی کا service متن `SAVED` ہو تو وہی اصل متن بھی hint میں رہتا ہے،نہ اسے rollback کہیں نہ خودکار retry کریں۔ Unknown/SAVED-handoff failure پر **Read print choice**،عام Refresh،timer اور permission regrant guard نہیں کھولتے۔ Reopen خود verification نہیں،اور یہ local guard crash-safe/durable identity نہیں۔

**واضح safety حد:** غیر یقینی یا ناقابلِ مطالعہ preference میں automatic print بند کرنا صرف اسی Counter کی مقامی حفاظتی حالت ہے،DB میں `false` دوبارہ لکھنا نہیں۔ پچھلا انتخاب no-commit پر برقرار رہ سکتا ہے؛یہ external writer کے بعد ہر وقت live preference ہونے کا وعدہ نہیں۔ دستی Reprint دستیاب رہتا ہے۔

## ⏳ کارٹ اور اصل فروخت الگ

نیا **Read print choice** بٹن صرف preference پڑھتا ہے:نہ customer منتخب/بدلتا،نہ cart/amount/payment/draft چھوتا،نہ quote بناتا،نہ Sale Save/Print چلاتا ہے۔ اصل **Refresh** کا موجودہ رویہ برقرار ہے؛وہ print baseline تازہ نہیں کرتا۔ Sale completion،financial calculations،posting،`!AutoPrint || Print(_lastSaleId)` اور manual Reprint کا پرانا کوڈ نہیں بدلا۔ Status بعد کے کام سے بدل سکتا ہے،اس لیے الگ persistent hint/disabled-checkbox tooltip بھی ہے۔ چھوٹی سکرین پر layout اور binding rollback کی اصل WPF جانچ باقی ہے۔

## ⏳ Startup registry — صرف دریافت،حل نہیں

`StartupService`/`IAutoStartStore`/HKLM/HKCU stores اس checkpoint میں نہیں بدلے:

1. Boolean `IsEnabled` snapshot پرانا command/path،value kind یا unreadable state محفوظ نہیں کرتا۔ `RestoreQuietly` نئے exe path سے Enable کرکے اصل پرانی Run entry کی exact بحالی ثابت نہیں کرسکتا۔
2. Settings کا `!Ok` نتیجہ unknown یا post-commit بھی ہوسکتا ہے؛اس پر registry compensation کو لازماً درست کہنا محفوظ نہیں۔ دو الگ resources crash-atomic نہیں۔
3. `DisableQuietly` کی swallowed errors اور HKLM→HKCU fallback duplicate entry چھوڑ سکتے ہیں؛read failure کو false سمجھنا absence کا ثبوت نہیں۔ قطعی “Nothing changed” پیغامات بھی الگ review مانگتے ہیں۔
4. اگلے الگ منظورشدہ حصے میں exact snapshot/read-failure/compensation policy،checked outcomes اور fake-store/public-service tests کا جائزہ چاہیے؛اس حصہ میں Windows registry یا live-shop experiment نہیں کیا۔

## ✅ محدود مکمل جانچ / ⏳ اصل جانچ باقی

- ✅ `tools/check_repo.py`: **132 static checks**؛C# grammar/member source resolution بھی شامل،نہ .NET compilation۔
- ✅ `tools/check_counter_autoprint_source_negative.py`: **41 lexical mutations** reject اور source کے اصل bytes بحال؛تمام **39 source-negative scripts** کی جانچ بھی الگ چلائی گئی۔ یہ behavioral/WPF execution نہیں۔
- ✅ Baseline audit:پچھلے927trackedfilesبرقرار؛تمامپرانےC#tests،Core services/models/data/37migrations،nativeguideبرقرار۔ `PosViewModel` میں `TenderedText` سے آخر تک اصل sale math/posting/manual print/access/timer کوڈ byte-identical؛اس سے پہلے صرفprintlifecycleکیwiringبدلی۔
- ⏳ `CounterAutoPrintEditTests.cs`: **24 نئے public-service/guard cases** لکھے؛POS-only receipt/reuse،دو writers،stale same-desired،cache سے الگ read،missing/foreign form،smuggling،revoked/inactive/deleted/changed-role actor،missing default/presence،timestamp-only،audit/settings fault rollback،exact retained receipt،unrelated key،permission-read fault،authenticated session اور unknown/ack guard۔ یہاں .NET SDK نہیں؛نئے cases یا **3516** کی متوقع پوری suite نہیں چلائی۔
- ✅ مالک کا پچھلا **v0.138.0 Windows/.NET10 Core+Tests:3418 PASS/0 FAIL/0 skip،6m50s** ثابت نتیجہ ہے،pending نہیں؛یہ نئی Desktop تبدیلی یا3516 suite کی سند نہیں۔
- ⏳ اصل Windows checklist:POS-only checkbox دونوں طرف Save؛دو کھلے editors کا conflict؛revocation/fresh login؛failed setter کے بعد checkbox واپس؛notifications کی reentrancy/throw؛unknown اور SAVED-handoff کے بعد read/Refresh/timer/regrant سے بند guard؛manual Reprint؛Read print choice سے typed cart/customer/payment/draft برقرار؛narrow layout/focus/DPI۔ مکمل build/provider/Release اور موجودہ warnings بھی الگ باقی۔

## ⏳ آگے کا باقی کام

باقی imperative Settings writers/startup registry کی الگ اصلاح،StockTake،حصہ1تا5 کی ضروری Windows/provider/WPF تصدیق اور نئی guide UI کی عملی رائے کھلی ہیں۔ Guide کا authored content/native hierarchy یہاں نہیں بدلا۔ ❌ حصہ6،durable identity اور installer شروع نہیں کیے۔ اگلا حصہ نئی اجازت سے۔
