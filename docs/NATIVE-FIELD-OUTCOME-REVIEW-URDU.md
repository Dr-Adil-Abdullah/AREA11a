# ⏳ حصہ5.1 — Built-in fields کی Required/Optional ترجیح — v0.134.0 Eglantine

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

**یہ صرف native metadata writer کا محدود source checkpoint ہے؛5.1 یا حصے1تا5 مکمل نہیں۔** نیا کاروباری/مالی اصول،migration،durable retry identity یا installer شامل نہیں۔ پچھلے اصل Windows نتائج v0.120.1 کے2963total/2941PASS/22FAIL ہیں؛انہیں اس checkpoint کا PASS نہ سمجھیں۔

## ⏳ کیا درست کیا

پہلے Required/Optional دبانے پر VM پوری فہرست دوبارہ پڑھتا تھا؛منتخب row کا پرانا token خاموشی سے بدل جاتا،اور پھر نئی حالت کے خلاف write ہو جاتا تھا۔ Service کا CAS موجود تھا مگر اصل کھلے editor کی حالت کی حفاظت نہیں رہتی تھی۔ Save کے بعد بھی پوری فہرست تازہ ہوتی تھی؛دوسرے کھلے rows کا baseline بدل سکتا تھا۔ Generic failure سے COMMIT کی غیر یقینی حالت الگ ظاہر نہیں ہوتی تھی۔

- `PrepareRequiredEdit` اصل منتخب row/token کا مقابلہ موجودہ owner/session کے ساتھ write lock میں کرتا ہے؛definition/audit write نہیں۔ Risk prompt صرف اسی کامیاب تیاری کے بعد،محفوظ No-default سے آتا ہے۔ Modal کے دوران دوسرے writer کی تبدیلی آخری transaction دوبارہ پکڑتی ہے۔
- Explicit `SetRequiredChecked` null token قبول نہیں کرتا،چاہے legacy host میں sessions بند ہوں۔ پرانا `SetRequired` signature اور sessions-disabled legacy رویہ برقرار؛interactive owner proof،موجودہ role/active اور stored audit name اپنی جگہ۔ No-op سے پہلے بھی owner اور CAS لازم ہیں۔
- اصل UPDATE،risk شرط،audit اور final-post قواعد برقرار۔ Observed transaction میں exact saved/no-op row اور اسی row کا token لیا جاتا ہے؛ناکامی پر handoff نہیں۔ صرف ثابت pre-COMMIT rollback پر `NoCommitConfirmed`؛COMMIT/rollback نامعلوم ہو تو unconfirmed،نہ Nothing changed کا دعویٰ۔
- Desktop میں command/modal کے دوران reentry،selection اور timer refresh رکتے ہیں۔ Save اب خود Refresh نہیں کرتا؛کامیابی پہلے acknowledge،پھر صرف exact row replacement،آخر میں retained-selection/row/value کی پڑتال۔ دوسرے rows کے tokens تبدیل نہیں ہوتے۔ Callback failure پر SAVED اور explicit review؛unknown پر بھی data invalidate۔
- `Refresh Fields` واضح read/review کا راستہ ہے؛automatic access timer،Loaded lifecycle یا regrant نامعلوم write کے بعد list نہیں کھولتے۔ Explicit Refresh موجودہ DB state دکھاتا ہے،**وہ سابق write کی شناخت یا durable receipt نہیں**؛خود save یا replay نہیں کرتا۔ یہ idempotent metadata assignment ہے،CreateStaff کے additive account guard جیسی durable/ہر جگہ retry شناخت نہیں۔

## ✅ محدود مقامی ثبوت

- 125 repository static checks؛45 نئی lexical mutations reject اور exact source bytes بحال؛کل32 negative scripts پاس۔ یہ source contracts ہیں،compiler/runtime نہیں۔
- الگ temporary DBs پر37 migrations کے ساتھ30 extracted-SQL reference plans،WAL اور DELETE دونوں۔ Read/no-op،risk refusal،exact target،UPDATE/audit rollback،دوسرے connection کی metadata change،unrelated field،revoked owner اور deferred COMMIT fault شامل۔ Python خود actor/CAS/Tx/outcome assumptions نافذ کرتا ہے؛اسے C# implementation/provider/WPF کا ثبوت نہ کہیں۔
- گیارہ پچھلے SQLite reference scripts بھی پاس؛ان میں draft/Settings/Pricing/Hardware/Icon/Workbook/Permissions/Staff/Recovery/CreateStaff شامل،اصل .NET نہیں۔
- پہلے سے tracked C# tests اور37 migrations برقرار؛مالی services،Database،session اور CAS engine نہیں بدلے۔ Guide MD/HTML متن یکساں؛diff whitespace checks پاس۔ Same-branch publication اور الگ actual remote تصدیق کی حالت آخری مختصر رپورٹ میں ہے۔

## ⏳ C# اور Windows

21 نئے isolated C# cases لکھے؛کل3347 متوقع suite **UNRUN**۔ اصل Release build کی کوشش exit127: `dotnet: command not found`۔ یہاں SDK نہیں؛grammar/static/Python پاس کو build یا tests PASS نہیں کہا جا سکتا۔ Session denial،wrong target/database token،stale metadata،post-modal conflict/no-op،fresh risk،exact receipt،unrelated writer،statement rollback،deferred COMMIT،stored owner name کے tests تحریر ہوئے ہیں،چلے نہیں۔

## ⏳ PT-76 — اصل الگ Windows مشین پر

پہلے encrypted backup کی تصدیق اور واقعی الگ **DB/photos/config**؛اصل دکان پر experimental posts،accounts یا fault injection نہیں۔

1. تازہ source/clean build؛Core/Tests Debug،3347 total/0fail/0skip کے اصل نتائج،Desktop Release بھی الگ۔ پرانے22 fixes اور حصوں3–5 کے tests کا تازہ run محفوظ کریں۔
2. ایک field کھلا رکھ کر دوسرے connection سے Required/label/critical/safe-required/help بدلیں؛پہلے writer کا action stale refusal دے،نئی حالت کے خلاف خاموش overwrite نہ ہو۔ Explicit Refresh Fields کے بعد نئی حالت دیکھ کر نیا ارادہ کریں۔
3. Critical Optional پر No-default،Cancel اور Yes؛modal کھلا ہو تو دوسرے connection سے metadata/owner/session بدلیں۔ آخری write conflict/denial ہو؛prompt کی پرانی منظوری نئی حالت پر استعمال نہ ہو۔ No-op بھی access/CAS bypass نہ کرے۔
4. Normal save میں صرف exact target بدلے؛دوسرے rows پر دوسرے writer کی تبدیلی save کے بعد خود adopt نہ ہو۔ اگلے action پر وہ پرانا baseline conflict پکڑے۔ فون/agency کی import-only اور product warning کی اصل محدود حیثیت برقرار؛Sale/Purchase posting guard کمزور نہ ہو۔
5. Double click/Enter اور modal nested timer؛صرف ایک command،selection نہیں سرکے۔ Collection/selection/status callback failure پر SAVED واضح،پرانی row invalidate؛unknown provider COMMIT/rollback failure پر بھی auto timer/regrant سے save فعال نہ ہو۔
6. UPDATE/audit fault پر واقعی rollback اور مناسب explicit retry؛provider COMMIT/response/dispose fault اصل .NET میں الگ۔ Refresh Fields نئی موجودہ حالت دکھائے،خود replay یا سابق save کی verified identity کا دعویٰ نہ کرے۔

## ⏳ باقی5.1

Custom definition Add/Update/toggle/reorder،باقی imperative Settings writers،Startup کی Windows registry+SQLite partial restore،پرانے PT-67–75 اور موجودہ PT-76 کے provider/WPF/LAN/file/physical gates باقی۔ StockTake/toggle/bulk کے باقی راستے اسی حصہ5 میں بعد میں۔ **1تا5 کے باقی کام اور ضروری تصدیق سے پہلے6 نہیں؛❌installer نہیں بنایا۔**
