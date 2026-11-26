# ⏳ حصہ 5.1 — Custom Field کی ترتیب بدلنے کا محفوظ نتیجہ

**v0.141.0 Fritillary · 2026-11-10 · رنگ `#6A5268` · PT-80**

یہ صرف **Move up/down** کا Core اور Desktop راستہ ہے۔ باقی Settings writers،startup registry،StockTake،Windows/provider/WPF جانچ اور پورا حصہ5.1 اس سے مکمل نہیں ہوئے۔ حصہ6/durable identity/installer شروع نہیں کئے۔

## ✅ پہلے سے ثابت اصل نتیجہ

مالک کا v0.138.0 Windows/.NET10 Core+Tests نتیجہ **3418PASS،0FAIL،0skip،6m50s** ثابت ہے؛اسے pending نہیں کہیں۔ اس کے بعد Add کے35 اور اب Move کے39 نئے cases لکھے ہیں؛نئی متوقع suite **3492** ہے،اس کا اصل Windows run یہاں نہیں ہوا۔ نئی گائیڈ UI کی شکل،ڈبل کلک اور focus کی عملی جانچ بھی الگ باقی ہے۔

## 1 — Core میں محدود تبدیلی

- `Move` کا اصل public signature اور sessions-disabled legacy null-token راستہ برقرار ہے۔ Desktop کیلئے نیا `MoveChecked` missing baseline کو رد کرتا ہے،چاہے host میں sessions بند ہوں۔
- ہر call کا اپنا `TransactionOutcome` اور explicit `Tx<ServiceResult>` ہے۔ صرف preflight یا واقعی pre-COMMIT rollback پر `NoCommitConfirmed=true`؛COMMIT/rollback/dispose کا نامعلوم نتیجہ unconfirmed ہے۔ Failure پر receipt/token واپس نہیں ملتے۔
- **Current Owner/session اور پوری entity کا اصل CAS پہلے،bounds بعد میں** ہیں۔ پہلے/آخری سوال کی boundary refusal بھی stale baseline یا revoked Owner کو bypass نہیں کرتی۔ Missing field اور boundary اب transaction میں throw/rollback کے ذریعے واضح no-commit refusal ہیں؛bounds کو کامیاب no-op نہیں بنایا۔
- اصل ترتیب والا SELECT،پڑوسیوں کا swap،پوری list کی1..n renumbering،ہر row کا UPDATE اور audit arguments برقرار ہیں۔ Duplicate sort values میں صرف دو numbers بدل کر بےاثر swap نہیں ہوتا؛hidden سوال بھی پہلے کی طرح ترتیب کا حصہ ہیں۔ Answers،stable keys،label/type/options/help/required/active/created_at نہیں بدلے۔
- Audit کے بعد،اسی write transaction سے موجودہ `CaptureDefinitionEdit` پوری active+hidden entity کی exact defensive receipt دیتا ہے۔ ہر row کا اسی entity baseline کا اپنا token ہے؛بعد کی List/Load یا دوسرے writer کی state اس receipt کا بدل نہیں۔ Shared model،CAS engine اور مالی SQL نہیں بدلے۔

## 2 — Desktop میں محدود تبدیلی

- Move اب شروع میں `RefreshAccess` کرکے پرانا editor/token خاموشی سے تازہ نہیں کرتا۔ اصل selected ID،entity،direction اور دکھائی گئی فہرست سے مطلوبہ destination pin ہوتے ہیں؛حتمی Owner/session/CAS/bounds کا فیصلہ Core میں ہوتا ہے۔ Destination کا مقامی حساب صرف returned receipt کی جانچ ہے،نیا business rule نہیں۔
- Add،Update،On/Off اور Move **وہی ایک persistent local guard** استعمال کرتے ہیں۔ Reentry/input/tab/selection/timer guard کے اندر رک جاتے ہیں۔ صرف known no-commit failure explicit retry کی اجازت دیتا ہے؛unknown یا acknowledged callback failure کو Refresh/New/Cancel/tab/regrant یا دوسرے definition command سے نہیں کھولا جاتا۔
- **Ack → input صاف → callbacks → retained verdict → reset** کی ترتیب ہے۔ پہلے selected ID اور editor backing state صاف؛پھر receipt کے ID/entity/destination/token چیک؛اسی exact receipt سے پوری list،notifications اور SAVED status؛آخر میں row/token اور blank editor برقرار ہوں تو reset۔
- کامیاب Move کے بعد سوال خود منتخب نہیں رہتا۔ **مزید Move یا edit کیلئے سوال دوبارہ چنیں۔** اس سے پرانا arrow intent خود replay نہیں ہوتا۔ Move label/type کی ادھوری ترمیم Save نہیں کرتا؛پہلے post-write Load بھی وہ ادھوری ترمیم واپس stored حالت سے بدلتا تھا۔
- Unknown پر واضح verify-before-reopen،اور ack کے بعد callback خرابی پر واضح **SAVED** آتا ہے؛محفوظ ترتیب کو failed move نہیں کہا جاتا۔ Known rollback/boundary refusal پر اصل input محفوظ رہتا ہے۔
- پرانا `FinishWrite→Load` helper اب کسی definition write کو درکار نہیں تھا،اس لئے ہٹا دیا۔ Add/Update/OnOff کے اصل methods،shared guard اور invalidation helper نہیں بدلے۔

## ✅ محدود مقامی جانچ

```text
python3 tools/check_repo.py
python3 tools/check_custom_definition_move_source_negative.py
python3 tools/check_custom_definition_move_sqlite.py
python3 tools/check_native_guide.py
python3 tools/check_guide_navigation.py
python3 tools/build_rules_matrix.py --check
```

- **131 repository static checks**؛467 C# grammar files،4943 calls/10521 references،صفر مسئلہ۔ پرانی lexical expectations کو checked Move،چار جائز verdict resets اور reload helper کے خاتمے کے مطابق بدلا؛current Owner اور اصل entity token کی شرائط نہیں ہٹائیں۔
- **47 نئی lexical mutations** رد اور exact bytes بحال؛تمام **38 source-negative scripts** پاس۔ یہ compiler،UI behavior یا provider کی سند نہیں۔
- **37 migrations/56 نئے isolated Python extracted-SQL plans**،WAL+DELETE:دونوں directions،normal/tied/sparse sorts،hidden rows،bounds،چھ peer changes،stale boundary،ہر renumber مقام پر before/after faults،audit/receipt fault+retry،deferred COMMIT،revoked Owner boundary،unrelated entity اور later writer۔ پرانے answers اور non-sort values محفوظ رہے۔ Python خود actor/CAS/Tx/outcome assumptions چلاتا ہے؛یہ C#/session/provider/WPF execution نہیں۔
- پچھلے Add کے40،Update کے30 اورOn/Off کے32 SQL reference plans دوبارہ پاس۔ تمام پرانے C# test files،37 migrations،دوسری business services/ViewModels اور گائیڈ R3 source/package/book برقرار ہیں۔ Guide کی3149 اورnavigation کی309 static/data assertions بھی پاس ہیں۔

## ⏳ نئے C# cases اور PT-80

**39 additive source cases**:دونوں directions کے normal/tied sorts اورhidden rows؛bounds؛دونوں قسم کےhost میںnull baseline؛invalid direction/actor؛چھ peer changes؛stale boundary؛wrong-target/DB token؛unrelated entity؛missing target؛current session/Owner/stored audit name؛تین partial-renumber،audit اورmissing-receipt faults؛deferred COMMIT؛defensive/later receipt؛اگلا جائز row-specific Move؛legacy API؛old answers اورچار synthetic shared-guard cases۔ Synthetic guard cases اصل VM/timer/focus نہیں چلاتے۔

اصل Windows پر صرف واقعی الگ DB/photos/config میں:

1. v0.141.0 Fritillary شناخت کے ساتھ Core/Tests/Desktop build،پھر `tools\RUN-TESTS.cmd` کا3492total/0FAIL/0skip نتیجہ۔ پرانا3418PASS اپنی جگہ ثابت ہے،نئے74cases کا ثبوت نہیں۔
2. منتخب سوال اوپر/نیچے کریں،پوری entity کے درست ranks دیکھیں؛hidden/tied sort rows بھی شامل ہوں۔ کامیابی کے بعد blank/no-selected editor اور اگلے Move کیلئے نیا انتخاب دیکھیں؛double-click پر پرانا intent خود نہ دہرے۔
3. پہلے/آخری سوال پر boundary refusal سے کوئی UPDATE/audit نہ ہو۔ دوسرے connection کی peer/order تبدیلی کے بعد پرانا Move conflict دے؛Owner/session revoked ہو تو boundary بھی پہلے اسی اجازت کو چیک کرے۔
4. Isolated partial UPDATE/audit/receipt fault میں پوری ترتیب rollback اور explicit retry ممکن؛COMMIT unknown یا SAVED callback failure میں کوئی blind replay،refresh/regrant bypass یا نیا false failure نہ ہو۔ اصل definitions/audit دیکھ کر ہی reopen کریں؛نیا instance خود verification نہیں۔
5. Exact receipt کے بعد writer آئے تو پرانا snapshot اپنی جگہ رہے اور اگلی edit/Move پرانا token لے کر refuse ہو۔ Answers/keys اور unrelated entity کی حالت نہ بدلے۔

## ⏳ اگلا کام

Definition کے چار routes کی یہ source پیشرفت،پورے5.1 یا Windows runtime کی تکمیل نہیں۔ اگلا الگ جائزہ باقی **imperative Settings writers اور startup registry** کا ہے؛StockTake اور Windows/provider/WPF تصدیق بھی باقی ہیں۔ Local guard نہ restart/cross-PC dedup ہے نہ durable request identity۔1تا5 کے باقی کام اور تصدیق سے پہلے حصہ6/installer نہیں۔
