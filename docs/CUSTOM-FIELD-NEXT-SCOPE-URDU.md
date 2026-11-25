# ⏳ اگلا محدود5.1 کام — Existing CustomField Update کا جائزہ شروع

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
