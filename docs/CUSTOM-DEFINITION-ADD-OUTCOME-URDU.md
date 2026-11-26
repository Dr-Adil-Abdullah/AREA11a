# ⏳ حصہ 5.1 — نیا Custom Field شامل کرنے کا محفوظ نتیجہ

**v0.140.0 Agrimony · 2026-11-10 · رنگ `#6B5C42` · PT-79**

یہ صرف **Add** کا Core اور Desktop کام ہے۔ Move،باقی Settings writers،startup registry،StockTake،durable identity اور حصہ6/installer اس میں شامل نہیں۔ گائیڈ UI کی نئی شکل،ڈبل کلک اور focus کی Windows جانچ بھی الگ باقی ہے۔

## ✅ پہلے سے ثابت اصل نتیجہ

مالک کا v0.138.0 Windows/.NET10 Core+Tests run **3418PASS،0FAIL،0skip،6m50s** ثابت ہے؛اسے pending نہیں کہا جارہا۔ نئی3453 suite اور Add کی یہ تبدیلی اس پرانے run سے ثابت نہیں۔ یہاں Windows یا dotnet SDK نہیں۔

## 1 — Core میں محدود تبدیلی

`CustomFieldService.Add` کا public signature،owner-only اجازت،definition validation،trimmed/case-insensitive duplicate rule،document-photo restriction،اصل INSERT،stable key UPDATE اور audit arguments برقرار ہیں۔ مالی SQL،migration یا پرانے answers نہیں بدلے۔

- ہر call کا اپنا `TransactionOutcome` اور explicit `Tx<ServiceResult<CustomField>>` ہے۔ صرف preflight refusal یا واقعی pre-COMMIT rollback پر `NoCommitConfirmed=true` ملتا ہے۔ COMMIT/rollback/dispose کا نامعلوم نتیجہ **unconfirmed** ہے،دوبارہ Add کی کھلی اجازت نہیں۔ ناکامی پر Value،receipt یا EditVersion نہیں ملتے۔
- Transaction میں current stored Owner/session پہلے،duplicate check بعد میں ہے۔ Duplicate اب throw کے ذریعے rollback refusal ہے؛صرف failure result واپس کرکے transaction commit نہیں کرواتا۔
- `MAX(sort)+1` اسی write lock کے اندر،پھر اصل INSERT → stable key → audit چلتے ہیں۔ **یہ append ہے،پرانے row کی edit نہیں۔** دوسرے شخص کی جائز peer/order تبدیلی محفوظ رہتی ہے اور نیا سوال موجودہ locked entity کے آخر میں آتا ہے۔ نئے editor کیلئے کوئی فرضی existing-row CAS یا durable request token نہیں بنایا۔
- اسی transaction کے آخر میں موجودہ `CaptureDefinitionEdit` سے اصل stored row اور active+hidden پوری entity کی exact receipt بنتی ہے۔ ہر row کے پاس اسی entity baseline کا اپنا token ہے۔ `Value` اسی receipt کا الگ copy ہے۔ پہلے ہاتھ سے بنائی row اور دوسرا `Clock.Now()` ختم ہوئے؛اب created_at بھی اصل stored قدر ہے۔ Shared receipt/model/CAS engine نہیں بدلے۔
- Receipt میں مطلوبہ target غائب ہو تو pre-commit exception اور rollback ہوتے ہیں؛فرضی کامیابی نہیں۔ Result metadata پر پہلے سے موجود JsonIgnore برقرار ہے۔

## 2 — Desktop میں محدود تبدیلی

- `Save()` نئے سوال کیلئے `SaveNew()` اور موجودہ ID کیلئے پرانا `SaveExisting()` چلاتا ہے۔ Typed Add result کو plain Success/Fail میں بدل کر Value/outcome/receipt ضائع کرنے والا راستہ ختم ہوا۔
- `SaveNew` پہلے loaded،مجاز manager اور blank/no-selected editor کی حالت چیک کرتا ہے۔ Save کے آغاز میں `RefreshAccess` نہیں:وہ access restore یا list change پر لکھا ہوا editor بدل سکتا تھا۔ حتمی Owner/session/duplicate فیصلہ Core کی write transaction میں ہے؛stale CanManage اجازت نہیں بنتی۔ Timer اور explicit Refresh کا پرانا عمل نہیں بدلا۔
- Add،Update اور On/Off **وہی ایک persistent local guard** استعمال کرتے ہیں۔ TryBegin کے بعد reentry،input،tab،selection اور timer سے تبدیلی رکتی ہے۔ Known rollback پر input محفوظ اور explicit corrected retry ممکن ہے۔ Unknown پر guard بند رہتا ہے؛Refresh/New/Cancel/tab/regrant اسے نہیں کھولتے۔ Move پہلے RefreshAccess کرتا ہے،اس لئے uncertain Add کو bypass نہیں کرتا؛یہ Move کے اپنے outcomes کی fix نہیں۔
- **Ack پہلے،پھر input صاف،پھر callbacks:** منتخب ID،label/options/help/required/type اور editing کی backing state صاف ہوتی ہے۔ پھر created Value اور receipt کے ID/entity/row/token چیک،اسی receipt سے rows،notifications اور SAVED status آتے ہیں۔ آخر میں پوری list کے row/token اور blank editor کا retained verdict درست ہو تو guard reset ہوتا ہے۔
- کامیاب Add کے بعد نیا سوال خود edit کیلئے منتخب نہیں ہوتا۔ ترمیم کیلئے اسے دوبارہ چنیں؛دوسرے نئے سوال کیلئے نئی عبارت لکھیں۔ فوری دوبارہ کلک خالی label کو validation میں رد کرواتا ہے،پہلا نام خود replay نہیں کرتا۔
- Callback/collection/status کی خرابی محفوظ کام کو failed Add نہیں بناتی۔ Guard بند رہتا ہے اور موجودہ invalidation واضح SAVED یا verify-before-reopen پیغام دیتا ہے۔

## ✅ محدود مقامی ثبوت

```text
python3 tools/check_repo.py
python3 tools/check_custom_definition_add_source_negative.py
python3 tools/check_custom_definition_add_sqlite.py
python3 tools/check_custom_definition_update_source_negative.py
python3 tools/check_custom_definition_active_source_negative.py
python3 tools/check_custom_definition_update_sqlite.py
python3 tools/check_custom_definition_active_sqlite.py
```

- **130 repository static checks** پاس؛466 C# grammar files،4943 calls/10505 references،صفر مسئلہ۔ Typed nested generic Add Tx کو پرانی owner-order gate پہچانتی ہے؛اصل Owner-before-duplicate شرط برقرار ہے۔ پرانی lexical expectations کو Add/Update/OnOff کے تین جائز verdict resets اور صرف Move کے باقی reload کے مطابق بدلا،حفاظت نہیں ہٹائی۔
- **40 نئی lexical mutations** رد اور exact bytes بحال ہوئے۔ تمام **37 source-negative scripts** پاس؛ان میں پرانی Update کی46 اور On/Off کی37 mutations بھی ہیں۔ یہ compiler یا WPF جانچ نہیں۔
- **37 migrations/40 نئے isolated Python extracted-SQL plans**،WAL+DELETE:چھ entities،active/hidden duplicate،INSERT/key/audit سے پہلے اور بعد faults+retry،missing receipt،deferred COMMIT،current Owner refusal،دوسرے connection کی peer تبدیلی/live append،اصل stored timestamp اور later writer سے پرانا receipt نہ بدلنا۔ Answers اور unrelated entity محفوظ رہے۔ Python خود actor/Tx/outcome assumptions چلاتا ہے؛یہ C#/provider/session proof نہیں۔
- پرانے Update کے30 اور On/Off کے32 SQL reference plans دوبارہ پاس ہیں۔ ان کے اصل methods،Move،shared guard/model،Database/session/CAS implementation،تمام پرانے C# tests اور37 migrations برقرار ہیں۔
- گائیڈ R3 کا authored source/package/book اور v0.139 کی tree/card UI نہیں بدلے۔ ان کے runtime حدود والے نوٹس اس نئے Add source کی Windows سند نہیں۔

## ⏳ نئے C# cases اور PT-79

**35 additive C# cases** لکھے ہیں؛متوقع suite **3453** ہے۔ نئے cases یہاں **UNRUN** ہیں:

- چھ entities کی exact row/whole-entity tokens؛آٹھ validation refusals؛active/hidden duplicate؛تین actor preflight اور دو current Owner refusals۔
- چار statement/receipt faults کے rollback+retry؛deferred COMMIT unknown؛اصل stored timestamp؛interactive session اور stored audit name۔
- Defensive/nonserialized receipt؛later peer کے بعد stale token؛receipt سے اگلی جائز edit؛live append،پرانے answers اور unrelated entity کا تحفظ۔
- تین synthetic guard cases؛یہ اصل VM،timer یا typing نہیں چلاتے۔

اصل Windows پر صرف واقعی الگ DB/photos/config کے test ماحول میں:

1. v0.140.0 Agrimony شناخت کے ساتھ Core/Tests/Desktop build اور `tools\RUN-TESTS.cmd` چلائیں۔ متوقع3453 total/0FAIL/0skip کی اصل رپورٹ الگ ہے؛پرانا3418PASS ضائع یا pending نہیں ہوا۔
2. عام Add سے ایک نیا field/key/audit،اصل entity کے آخر میں،پھر blank editor/no-selection دیکھیں۔ صاف نئے ارادے سے دوسرا سوال بنا سکیں؛double-click پہلا نام خود نہ دہرائے۔
3. Duplicate/invalid list/document photo/revoked Owner پر نیا row/audit نہ بنے۔ Known rollback کے بعد input رہے اور صارف کی درست کی ہوئی explicit retry چلے۔
4. Isolated faults میں INSERT/key/audit/receipt failure اور COMMIT unknown الگ دیکھیں۔ Unknown/SAVED callback failure پر Update/Toggle/Add/Move/New/Refresh/tab/regrant سے blind write نہ کھلے۔ اصل records/audit دیکھ کر ہی reopen کریں؛نیا instance خود verification نہیں۔
5. دوسرے connection کی peer/order/hidden حالت نئی Add میں محفوظ رہے۔ Receipt کے بعد writer آئے تو اگلی existing edit پرانا token لے کر refuse ہو۔

## ⏳ باقی

اگلا محدود کام **CustomField Move** کا observed outcome،exact whole-entity receipt اور Desktop lifecycle ہے۔ پھر باقی Settings/startup registry/StockTake اور Windows/provider/WPF جانچ؛1تا5 کے باقی کام اور تصدیق سے پہلے6/installer نہیں۔ Local guard نہ global dedup ہے نہ restart/cross-PC exactly-once؛duplicate نام کا rule بھی durable request identity نہیں۔
