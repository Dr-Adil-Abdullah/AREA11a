# ⏳ حصہ3/14 — ریکارڈ اور فوری اندراج کے Save نتائج

## ⏳ موجودہ حیثیت — v0.121.0 Hawthorn

مالک کے نئے `next` سے صرف حصہ3 کی اجازت ملی۔ حصہ2 کی22 ناکامیوں کی fixture اصلاحات شائع ہیں،لیکن ان کا نیا Windows نتیجہ نہیں ملا؛انہیں کامیاب نہیں کہا گیا۔ موجودہ source میں72 الگ نئے C# cases ہیں،اس لیے اب متوقع suite **3035** ہے،2963 نہیں۔ حصہ4 شروع نہیں ہوا۔

یہ مستقل نمبر6،23،24،25 کی محدود پیش رفت ہے؛ان پورے موضوعات کی100% تکمیل نہیں۔ ❌ installer binary نہیں بنی؛⏳ FullPhase4 کھلا ہے۔

## ✅ محدود source کام

| راستہ | کیا بدلا؟ | نتیجہ کی حد |
|---|---|---|
| دوا کا نیا/ترمیمی modal | موجودہ `PostingAttemptGuard`؛تصدیقی dialog کے دوران بھی reentry بند؛عام prewrite validation/cancel سے دوبارہ کوشش؛Creator/Update سے explicit no-commit آئے تو retry؛success پہ acknowledgment پہلے،نیا ID pin،پھر editor/list reload | unknown یا کامیابی کے بعد cleanup failure پر اسی page instance میں Save/New/Edit دوبارہ نہیں؛صرف modal بند کرکے کھولنا اسی page کا guard نہیں بدلتا |
| Customers: نیا گاہک | success acknowledge پہلے،پھر form بند/name/phone/category/staged editor صاف؛فہرست میں **اسی ID** کا انتخاب اور checked reload؛callback کے بعد ہی guard ready | failure میں پرانا selected customer نئے کی کامیابی نہیں سمجھا جاتا؛Cancel،Refresh اور regrant unknown guard نہیں کھولتے |
| Customers: ترمیم | UpdateCustomer کا per-call transaction outcome؛UI guard؛target cleanup کے بعد checked reload/callback؛کامیابی کے بعد خرابی پر SAVED | Cashier کی phone correction اور Owner/Manager limit پالیسی برقرار؛پرانی نامکمل ترمیم خود سے دوبارہ نہیں چلتی |
| Counter: فوری گاہک | الگ creation guard؛ack کے بعد creation form صاف،اسی saved ID کا انتخاب،checked lookup/editor reload | بل کی cart،draft ID اور sale guard reset/clear نہیں کیے؛کامیابی کے بعد lookup خراب ہو تو customer verify/select کریں،گاہک دوبارہ نہ بنائیں |
| Purchases: فوری سپلائر | الگ creation guard؛ack کے بعد creation fields/staged editor صاف؛اسی saved ID اور lookup success کی تصدیق | Purchase lines/draft identity صاف نہیں؛lookup failure کو دوبارہ AddSupplier کا جواز نہیں بنایا |
| دوا کے اندر category/company/salt/brand فوری اندراج | چاروں ایک guarded helper سے؛success acknowledgment پہلے،پھر متعلقہ new-name صاف اورsaved ID منتخب؛selection نہ ملے تو SAVED/blocked | غیرتصدیق شدہ quick creation کے دوران parent Product Save/New/Edit بھی روکے؛Refresh guard نہیں کھولتا |
| Catalogs: category/company/salt/brand Save | ہر modal کا shared guard؛Add/Update کے explicit outcome؛reload میں محفوظ ID کی تصدیق کے بعد ہی دوبارہ edit کی اجازت | New/Toggle کے ذریعے unresolved Save کو bypass نہیں کیا جاسکتا؛Refresh/permission regrant guard نہیں بدلتا |
| Units: Add/Update Save | وہی observed-result/ack/reload guard؛canonical rename اور whole-unit پالیسی برقرار | units اور stock calculation نہیں بدلے |
| Brand name + company link | پرانا **دو الگ transaction** والا contract برقرار؛name save acknowledge،پھر link؛link failure میں نام محفوظ ہونے کا SAVED نتیجہ اور پوری Save دوبارہ کرنے پر روک | link کے confirmed rollback اور unknown outcome کی عبارت الگ؛یہ دونوں transactions کو atomic بنانے یا خودکار link retry کا دعویٰ نہیں |

## ✅ Core میں درست failure ثبوت

- Product/Customer/Supplier creation کی prewrite validation اب `NoCommitConfirmed=true` دیتی ہے؛صرف وہ راستے جہاں creator transaction ابھی نہیں چلا۔ پہلے سے موجود staged-photo cleanup/unknown retention برقرار۔
- Product.Update،Partner.UpdateCustomer اور Catalog کے Add/Update/SetBrandCompany کل13 مزید operations اب موجودہ `TransactionOutcome` کو اپنے **اسی** `Database.Tx` میں دیتے ہیں۔ تین creators سمیت16 observed operations ہیں۔
- Catalog کے transaction کے اندر پرانی validation-failure return اب اسی متن کی `ValidationException` ہے؛تبدیلی سے پہلے انکار rollback سے واپس آتا ہے،no-op COMMIT کی ناکامی کو retry-safe نہیں فرض کیا جاتا۔
- Customer کی private SaveCustomer helper کی returned refusals پہلی UPDATE سے پہلے ہیں؛ان میں creator نہ چلنے کا مثبت ثبوت ہے۔ باہر transaction/COMMIT exception آئے تو public method outcome کو دیکھتا ہے،اندرونی result کو کامیاب واپسی نہیں سمجھتا۔
- SQL-bearing string literals کی ترتیب baseline سے ایک جیسی جانچی؛financial services،37 migrations،model/schema،pricing/Extra/SupplierB/Cost+Profit قواعد میں کوئی تبدیلی نہیں۔ تمام پہلے سے tracked C# tests بغیر تبدیلی برقرار ہیں؛حصہ2 کی test اصلاحات بھی نہیں بدلی گئیں۔

## ✅ اصل مقامی نتائج — صرف محدود جانچ

- `tools/check_repo.py --quiet`:112 static contracts؛grammar/source/call checks،C# build نہیں۔
- `tools/check_parent_record_save_source_negative.py`:45 lexical mutations رد،اصل bytes بحال؛guard/ack/reset/unknown/ID-pin/financial-cart preservation کی source جانچ،WPF execution نہیں۔
- `tools/check_parent_record_save_sqlite.py`:37 اصل migrations،15 extracted-SQL plans کے30 BEFORE/AFTER audit-trigger rollback/retry cases،1 brand-name/link دو قدم case؛stock/financial rows یکساں۔ **Python plan/transaction/audit arguments** ہیں؛dynamic Product.Update statement،C# validation،Database.Tx اورprovider COMMIT چلانے کا ثبوت نہیں۔
- پچھلی جانچیں بھی دوبارہ:record-write30،catalog49،photo-file23 lexical mutations رد/bytes restored؛record/catalog کے SQL probes اورphoto reference17 file cases/3 SQL rollback plans/1 synthetic lost-ack retention کامیاب۔ یہ بھی .NET/WPF سند نہیں۔
- آخری مجموعی lexical run:19 source-negative scripts میں531 mutations رد،اصل bytes بحال۔435 C# files کی grammar،4772 calls/9877 references پر0 static issues؛Roslyn compilation نہیں۔ `build_rules_matrix.py --check` بھی کامیاب۔815 پہلے سے tracked paths اور36 guide anchors برقرار؛339 دوسری tracked Core/Tests files byte-identical،تمام پرانے tests محفوظ۔
- نیا `RecordSaveOutcomeTests.cs`:72 لکھے ہوئے cases —16 operations کے32 audit faults،16 validation/correction،16 real-service success + synthetic post-ack callback failure،8 guard observations۔ **تمام72 یہاں UNRUN**؛3035 suite کی نئی runtime discovery بھی نہیں ہوئی۔

## ⏳ کیا ثابت نہیں ہوا؟

`dotnet build dotnet/Area11.Tests/Area11.Tests.csproj --configuration Release` یہاں exit127،`dotnet: command not found` سے نہیں چلا۔ نئی Core/Tests/ Desktop compilation،72 tests اورپوری3035 suite،WPF bindings/dialog/property callback execution،اصل COMMIT/lost-ack/rollback/dispose faults،parallel/restart/cross-PC scenarios ابھی باقی ہیں۔

Guard صرف اسی form/page instance کا ہے۔ نئی page/window،دوسرے صفحے یا پروگرام restart پر **اپنے آپ پچھلا کام verify نہیں ہوتا**۔ پہلے records/gallery/audit میں جائزہ،پھر page دوبارہ کھولیں۔ Counter/Purchases کی محفوظ نہ ہوئی cart/lines کو page بدلنے سے پہلے سنبھالیں؛صرف guard ہٹانے کے لیے app بند نہ کریں۔ durable operation identity/new retry protocol حصہ6 کی تجویز اور الگ منظوری کے بغیر شامل نہیں کیا گیا۔

Non-Save actions (مثلاً الگ Deactivate/Put-away،gallery Remove/primary،unit/catalog toggle کے اپنے unknown outcomes)،باقی program-wide Save callers،session/current permission rollout،CAS/concurrency/backup/cloud/hardware اپنے متعلقہ اگلے حصوں میں کھلے ہیں؛یہ پورے پروگرام کے ہر بٹن کی certification نہیں۔ موجودہ Catalog/Unit toggles unresolved **Save** کے دوران guard سے رک جاتے ہیں،مگر ان toggles کی الگ outcome design اس حصے میں نہیں بدلی۔

## ⏳ PT-63 — اصل Windows پر مطلوبہ جانچ

**صرف verified backup کے بعد،واقعی الگ DB/photos/config اور disposable test installation پر؛اصل دکان پر فرضی posting یا fault injection نہیں۔**

1. `tools\RUN-TESTS.cmd` کا نیا v0.121.0 log:3035 total،0 failed،0 skipped مطلوب؛پرانے v0.120.1 کے2941/22 کو نیا PASS نہ کہیں۔ پھر Desktop Release build الگ جانچیں۔
2. چار parent routes اورCatalog/Units میں عام invalid name/duplicate phone/catalog name کے بعد correction/retry؛نہ form بلاوجہ مستقل بند ہو،نہ غیرمطلوب ریکارڈ بنے۔ Cashier phone correction بھی برقرار ہو۔
3. disposable ماحول میں audit/extra-field failure سے parent/custom/photo row rollback؛پھر ایک درست retry سے صرف ایک parent اورمتعلقہaudit۔ SQL/file reference کو اسی test کا متبادل نہ سمجھیں۔
4. اصل UI میں success کے بعد reload،selection،editor.LoadNew،property notification یا callback خراب ہونے کی جانچ: **SAVED** رہے،وہی action دوبارہ نہ چلے؛Refresh/Cancel/permissions regrant نئی copy نہ بنائیں۔
5. Counter cart اورPurchase lines کے ساتھ quick-create success/failure؛موجودہ financial intent صاف نہ ہو،نیا مطلوب customer/supplier ID صحیح منتخب ہو،غلط پرانا انتخاب کامیابی نہ کہلائے۔
6. Product confirm dialog کے دوران repeated Enter/Save،catalog modal reentry،brand کے پہلے save کے بعد link failure؛safe refusal یاsingle save،partial-success کی واضح عبارت۔
7. الگ provider harness میں lost-ack/COMMIT/rollback/disposal failure؛result unknown اوراسیinstance پر retry بند۔ نئےpage/restart پر کوئیexactly-once ضمانت نہیں؛verify-first ہدایت جانچیں۔

⏳ حصہ2/3 کے اصل نتائج باقی؛حصہ4 کی نئی اجازت سے پہلے اسے خود سے شروع نہیں کرنا۔
