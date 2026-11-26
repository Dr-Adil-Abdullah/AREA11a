> **⏳ موجودہ5.1 — v0.142.0 Vervain:** Counter کاautoPrint ابDB-loaded baseline/currentPOS+session/checkedSave اور exactreceiptکےبعدlocalchoice؛معلومno-commitپرپچھلاانتخاب،unknownپرautomaticoff/retainedguard؛صرفprintchoiceپڑھنےکاالگبٹن،کارٹ/مالیحساب/دستیReprintبرقرار۔ ✅132static/39source-negative scripts/41نئیlexicalmutations محدود؛⏳24نئےC#cases/3516suiteاوراصلWindows/provider/WPFباقی۔ مالککاv0.138.0کا3418PASSثابت ہے،pendingنہیں۔ [PT-81/ہوااورباقی](COUNTER-AUTOPRINT-EDIT-URDU.md)؛startupregistryکےexactrestore/unknowncompensationمسائلصرفدرج،حلنہیں؛باقیSettings/StockTake/نئیguideUIعملیجانچکھلی،6/installerنہیں۔ نیچےپرانےcheckpointcountsتاریخیہیں۔

> **⏳ موجودہ Move checkpoint — v0.141.0:** [ترتیب بدلنے کا observed outcome،exact entity receipt اور Desktop guard/PT-80](CUSTOM-DEFINITION-MOVE-OUTCOME-URDU.md) سورس میں شامل اور محدود static/SQL جانچا گیا۔39نئےC#cases سمیت3492suite یہاںUNRUN؛پچھلاWindows3418PASS ثابت ہے۔ چاروں definition routes کی source پیشرفت پورے5.1 یاruntimeکی تکمیل نہیں؛باقیimperative Settings/startup registry/StockTake،نئیguideUIاورWindows/provider/WPFجانچ کھلی ہیں۔ نیچےMoveکا«باقی/شروع نہیں»متن تاریخی checkpoint ہے۔6/installerنہیں۔

> **⏳ موجودہ Add checkpoint — v0.140.0:** [نیا سوال شامل کرنے کا observed outcome،exact receipt اور Desktop guard/PT-79](CUSTOM-DEFINITION-ADD-OUTCOME-URDU.md) لکھا اور محدود static/SQL جانچا گیا؛35نئےC#cases/3453suite یہاںUNRUN۔ پچھلاv0.138.0Windows3418PASS ثابت ہے۔ **Move اگلا الگ باقی کام** ہے؛remaining Settings/startup/StockTake اورنئیguideUIکی عملی جانچ نہیں بند ہوئیں۔ نیچے Add کا «شروع نہیں/باقی» متن پچھلے checkpoint کا ہے،موجودہ پیشرفت نہیں۔ 6/installer نہیں۔

> **⏳ واپسی کا موجودہ جائزہ — v0.138.0:** گائیڈ R3 کے تحریری checkpoint کے بعد پہلے5.1کام کا source review دوبارہ کیا۔ `CustomFieldService.Add` ابھی plain `_db.Tx` سے ہاتھ سے بنا row دیتا ہے؛Desktop `Save` اسے plain `ServiceResult` میں بدلتا اور `FinishWrite→Load` چلاتا ہے۔ `Move` میں موجود Owner/entity CAS اور پوری list کی renumbering واقعی موجود ہیں،لیکن observed outcome/exact entity receipt اور اپنا acknowledged lifecycle ابھی نہیں۔ Update/OnOff کی موجودہ حفاظت کو Add/Move کی تکمیل نہ کہیں۔ **اگلا محدود implementation پہلے Add،پھر Move**؛stable key/duplicate validation/اصل INSERT+audit محفوظ،ack پہلےcallbacks،unknown پر persistent guard،exact created-row/entity handoff کے cases درکار ہیں۔ یہ صرف جائزہ ہے؛اس checkpoint میں ان services/VM کا code نہیں بدلا۔ [گائیڈ checkpoint/جانچ](GUIDE-R3-CONTENT-URDU.md)۔ مالک کاپچھلاv0.136.2Windows3397PASS ثابت ہے؛نئی3418suite/WPF الگUNRUN۔ نیچےپرانےbuildblocker/pause/counts تاریخی ہیں،موجودہ blocker کا دعویٰ نہیں۔

# ⏳ اگلا محدود5.1 کام — Existing CustomField Update کا جائزہ شروع

> **⏳ تازہ5.1 — v0.136.0 Comfrey:** CustomFieldOn/OffکاcheckedobservedTx،Owner/entityCASقبلno-op،exactdefensiveentityreceipt؛Update/OnOffکاوہیsharedpersistentguard،pinneddisplayedintent،ackقبلinputconsumption/callbacks،normalblankeditorشامل۔ [PT-78/حدود](CUSTOM-DEFINITION-ACTIVE-OUTCOME-URDU.md)؛[پچھلاUpdate/PT-77](CUSTOM-DEFINITION-UPDATE-OUTCOME-URDU.md)۔24نئےC#cases/3397suiteUNRUN؛128static/37نئیlexical/32PythonSQLplansمحدود،اصلWindows/provider/WPFنہیں۔ CustomAdd/Move،imperativeSettings/registry/StockTakeباقی؛v0.134.0اصلbuildFAIL/CS0411fixکاrerunاور3warningsابھیباقی؛1تا5بندہونےسےپہلے6/installerنہیں۔ نیچےپرانےcheckpointاعداد/pauseاپنےوقتکےتاریخیہیں۔

> **⏳ تازہ اصل build blocker — v0.134.1 Borage source fix:** مالک کےv0.134.0WindowsrunمیںCorecompileFAIL،1CS0411/3warnings،36.79s؛tests/appنہیںچلے۔ Product.UpdateکےobservedTxمیںexplicitobject?اورno-op/finalreturnnullکی3محدوداصلاحات؛[اصلثبوت/وجہ/حدود](WINDOWS-V1340-BUILD-FAILURE-URDU.md)۔126static/3نئیlexicalregressionsمحدود؛3347C#suiteاوراصلWindowsrerunابھیباقی۔ CustomFieldimplementationpaused؛پہلےیہblocker،پھر1تا5باقی؛6/installerنہیں۔ نیچےv0.134.0/docs-onlyاورپرانےنتائجتاریخیہیں۔

**بنیاد: v0.134.0 / `fbe4218`؛یہ implementation یا runtime PASS نہیں۔** موجودہ مالک کی ہدایت D-117 پر source inventory/acceptance plan شروع؛اس turn میں production code نہیں بدلا۔ [مکمل current report](COMPLETE-STATUS-AND-DECISIONS-URDU.md)؛[14 حصے](REMAINING-14-PORTIONS-URDU.md)۔

## 1.1 — چار الگ write راستے ملے

| راستہ | اصل فائل/علامت | موجودہ حفاظت | ابھی source جائزے میں ملا خلا |
|---|---|---|---|
|نیا سوال|`Core/Services/CustomFieldService.cs` → `Add`|current Owner،definition validation،duplicate check،INSERT+stable key+audit transaction|observed outcome نہیں؛returned row ہاتھ سے بنتا،token نہیں؛VM اسے plain result میں بدل کر handoff گنواتا ہے؛Add identity الگ مسئلہ |
|موجودہ سوال کی ترمیم|اسی service کا `Update`|current Owner،entity-wide CAS،duplicate label،definition rules،old answers محفوظ،type-change warning|plain success/failure؛observed no-commit/unknown اور exact post-write entity/token handoff نہیں |
|سوال آن/آف|اسی service کا `SetActive`|current Owner،entity CAS،no-op،answers محفوظ،audit|plain result،unknown/no-op exact receipt الگ نہیں |
|ترتیب اوپر/نیچے|اسی service کا `Move`|current Owner،entity CAS،bounds،پوری list کی renumbering+audit|اثر صرف selected row نہیں،پوری entity list ہے؛اسی لیے native کا single-row handoff یہاں نقل کرنا کافی نہیں |

## 1.2 — Desktop کا اصل راستہ

`Desktop/ViewModels/CustomFieldsViewModel.cs`:

1. `Save()` پہلے `RefreshAccess()`؛existing Update کو `_selectedField?.Field.EditVersion` دیتا ہے۔
2. `RefreshAccess()` selected persisted definition بدلے تو editor discard کرتا ہے؛یہ پہلے سے موجود حفاظت ہے،اسے غائب نہ بتائیں۔
3. مگر دوسری row بدلے تو `ReplaceRows(result.Value, _editingId)` پوری list اور selected wrapper/token بدلتا ہے۔ Entity-wide baseline خاموشی سے تازہ ہونے کا راستہ موجود ہے؛آخری in-Tx CAS اب بھی موجود،یہ "CAS سرے سے نہیں" والا دعویٰ نہیں۔
4. `FinishWrite()` نئی `Load()` کرتا ہے؛post-write saved snapshot کے بجائے بعد کے writer کی حالت adopt ہوسکتی ہے۔ Reload failure کا SAVED متن موجود ہے،مگر observed transaction outcome/persistent attempt lifecycle سے جڑا مکمل flow نہیں۔
5. `Save/Move/Toggle` کے گرد reviewed code میں persistent observed-outcome guard نہیں۔ Native-field کی تازہ `_saving/_needsReload` حفاظت اس الگ VM کو خود نہیں ملتی۔

## 1.3 — پہلے منتخب implementation کا دائرہ

**Existing `Update` اوراس کا Desktop Save branch۔** اصل Owner/Settings/current-session،entity/order baseline،definition validation،duplicate label،saved answers/type-change warning اور legacy API برقرار رکھنا۔

ممکنہ محفوظ contract:loaded entitybaseline کو action سے پہلے خاموشی سے adopt نہ کرنا؛اصل transaction میں compare،explicit no-commit/unknown classification،post-write exact entity state/token handoff؛acknowledgement کسی collection/status callback سے پہلے؛unknown/ack callback failure پر retained blocked/review state۔ یہ plan ہے،کوڈ لکھا نہیں۔ Entity-wide effect/current token کی وجہ سے پورے snapshot کی درست حد implementation میں طے کرنا ہوگی؛خاموش partial rebase نہیں۔

## 1.4 — اس محدود کام میں شامل نہیں

Add کی نئی شناخت/sequence،Move کی renumbering یاOn/Off کا مکمل outcome ابھی مکمل نہیں؛وہ inventory میں کھلے رہیں گے۔ Custom answers کی parent-save policy،startup registry،StockTake،مالی SQL،مigrations،durable request identity یاحصہ6/installer اس محدود Update میں شامل نہیں۔ کسی مشترکہ helper کی ضرورت سےscope بڑھے تو پہلے واضح درج کریں،سب کو100% مکمل نہ کہیں۔

## 1.5 — مجوزہ isolated acceptance cases

- loaded selected definition اور پڑوسی/order/active state میں دوسرے connection کی تبدیلی؛stale refusal before writes؛explicit refresh/review الگ۔
- wrong target/entity/database token،missing baseline،inactive/revoked Owner اورinteractive session؛no-op میں بھی current access/CAS۔
- same label/invalid type/options/help،document photo restriction،duplicate name؛پرانے answers unchanged؛type-change warning preserved۔
- UPDATE/audit fault پر known rollback؛deferred/provider COMMIT/rollback/dispose پر unknown،ناکامی پر exact receipt نہ نکلے۔
- transaction کے فوراً بعد دوسرا writer؛اصل exact result نئی state adopt نہ کرے؛اگلی write fresh conflict پکڑے۔
- Desktop double-click/reentry،typing/tab/selection/access timer،ack/status/collection callback failure؛unknown یا SAVED کو دوبارہblind write نہ بنایا جائے۔
- legacy tests unchanged؛isolated DB/photos/config؛پھر اصل Windows/.NET/WPF run۔ Python/reference plan اور lexical mutation کو runtime PASS نہ کہیں۔

## 1.6 — باقی inventory ساتھ بند نہیں ہوئی

StartupService میں raw registry vs boolean restore،Settings result کی unconfirmed حالت،best-effort restore اور Nothing changed متن الگ جائزہ چاہتے ہیں۔ باقی imperative Settings callers،Custom Add/OnOff/Move اورStockTake بھی کھلے ہیں۔ یہ تحقیق source risks دکھاتی ہے،اصل provider/Windows failure reproduce ہونے کا دعویٰ نہیں۔
