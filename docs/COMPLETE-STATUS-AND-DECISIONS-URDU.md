# مکمل نمبر وار حالت،فیصلے،باقی کام اور اگلا قدم

> **⏳ PT-89 — StockTake جائزہ، اصلاح ابھی نہیں:** موجودہbuildv0.149.0/3748suiteبرقرار؛productioncode/tests/versionنہیںبدلے۔ StaleCount/ClearاوربعدکیstockchangeکےساتھApplyکےfrozenmovementفرقکی8syntheticSQLreproductionsملیں،یہfixیاC#PASSنہیں۔ [خطرہ،SettingsکیباقیحداورA/Bسوال](STOCKTAKE-CONCURRENCY-SCOPE-URDU.md)؛مالککاجوابباقی،کوئیD-120فرضنہیں۔ 3418PASSپچھلاثابتنتیجہ؛Windows/guideUI/باقی1تا5کھلے،6/installerنہیں۔


> **⏳ موجودہ5.1 — v0.149.0 Gentian:** sessionکےcurrentsharedmarkers،startupstamps+previousauditایکtransaction،heartbeat/exitکےatomicstampsاورcheckedنتائج؛unknownپرlocalpause،login/shellمیںstickywarning۔ ✅139static/46source-negative scripts/66نئیlexicalmutations/34PythonSQLplansصرفمحدودثبوت؛⏳31نئےC#cases/3748suiteاوراصلWindows/provider/WPFباقی۔ مالککا3418PASSثابت۔ [PT-88/حدود](SESSION-TELEMETRY-OUTCOME-URDU.md)؛یہkill/PC/userproofیاexactly-onceنہیں،StartupC/FocusAبرقرار؛StockTake/guideUIاور1تا5کیباقیتصدیقکھلی،6/installerنہیں۔ نیچےپرانےcountsتاریخیہیں۔


> **⏳ تازہ5.1 — v0.136.0 Comfrey:** CustomFieldOn/OffکاcheckedobservedTx،Owner/entityCASقبلno-op،exactdefensiveentityreceipt؛Update/OnOffکاوہیsharedpersistentguard،pinneddisplayedintent،ackقبلinputconsumption/callbacks،normalblankeditorشامل۔ [PT-78/حدود](CUSTOM-DEFINITION-ACTIVE-OUTCOME-URDU.md)؛[پچھلاUpdate/PT-77](CUSTOM-DEFINITION-UPDATE-OUTCOME-URDU.md)۔24نئےC#cases/3397suiteUNRUN؛128static/37نئیlexical/32PythonSQLplansمحدود،اصلWindows/provider/WPFنہیں۔ CustomAdd/Move،imperativeSettings/registry/StockTakeباقی؛v0.134.0اصلbuildFAIL/CS0411fixکاrerunاور3warningsابھیباقی؛1تا5بندہونےسےپہلے6/installerنہیں۔ نیچےپرانےcheckpointاعداد/pauseاپنےوقتکےتاریخیہیں۔

> **⏳ تازہ اصل build blocker — v0.134.1 Borage source fix:** مالک کےv0.134.0WindowsrunمیںCorecompileFAIL،1CS0411/3warnings،36.79s؛tests/appنہیںچلے۔ Product.UpdateکےobservedTxمیںexplicitobject?اورno-op/finalreturnnullکی3محدوداصلاحات؛[اصلثبوت/وجہ/حدود](WINDOWS-V1340-BUILD-FAILURE-URDU.md)۔126static/3نئیlexicalregressionsمحدود؛3347C#suiteاوراصلWindowsrerunابھیباقی۔ CustomFieldimplementationpaused؛پہلےیہblocker،پھر1تا5باقی؛6/installerنہیں۔ نیچےv0.134.0/docs-onlyاورپرانےنتائجتاریخیہیں۔

**دستاویزی جائزہ: 2026-11-10؛موجودہ application source v0.134.0 Eglantine،commit `fbe4218aa62bbeedca3fd706429a020d1d3bd6ea`۔** یہ نئی production release یا مکمل کامیاب جانچ کی سند نہیں۔ اس رپورٹ کی Git publication کی اصل تصدیق آخری گفتگو میں دی جاتی ہے؛پہلے سے کامیابی فرض نہیں۔

## 1۔ رپورٹ کیسے پڑھی جائے؟

### 1.1 — تین الگ حالتیں

- ✅ مکمل:صرف وہ خاص کام جس کا متعلقہ ثبوت موجود ہے،مثلاً source push یا محدود static check۔
- ⏳ جاری:کوڈ لکھا ہے مگر اصل compiler/runtime/UI/آلات یا دوسرے راستوں کی جانچ باقی ہے۔
- ❌ شروع نہیں:اس نئے14 حصوں کے منصوبے کا متعلقہ مرحلہ شروع نہیں ہوا۔ اس کا مطلب یہ نہیں کہ اس feature کا کوئی پرانا code موجود نہیں۔

### 1.2 — تین الگ گنتیاں

14 بڑے مرحلے،مستقل1–36 کاموں کی فہرست اور چھوٹے source checkpoints الگ چیزیں ہیں۔ 36 کاموں کا مطلب36 بار Next نہیں؛5.1 کے دس checkpoints کا مطلب دس نئے بڑے مراحل مکمل ہونا بھی نہیں۔ [14 حصے](REMAINING-14-PORTIONS-URDU.md)؛[مستقل1–36 تفصیل](NUMBERED-WORK-STATUS-URDU.md)۔

### 1.3 — پرانے اور موجودہ نوٹس

پرانے Windows نتائج،ریلیز کے وقت کی تعداد اور اس وقت کے pending نکات تاریخی ثبوت ہیں۔ انہیں آج کی کامیابی بنا کر نہیں لکھا گیا۔ آج کے باقی کام کے لیے اس رپورٹ اور14 حصوں کی موجودہ جدول کو پہلے پڑھیں؛ہر پرانی trace کا باقی کام لفظ بہ لفظ موجودہ inventory نہیں۔ User Guide کے شروع کے پرانے checkpoint نوٹس بھی اسی اصول سے پڑھیں۔

## 2۔ حقیقت میں کیا ثابت ہوا؟

### 2.1 — اصل Windows ثبوت

✅ مالک کے v0.120.1/.NET10 run میں Core اور Tests کا Debug compile ہوا،2963 tests چلے:2941 PASS،22 FAIL،0 skip۔ صرف متعلقہ پرانی compile خرابی دور ہونے کی تصدیق ہوئی؛اس run کو پوری suite green،Desktop Release یا installer PASS نہیں کہتے۔

### 2.2 — اس کے بعد کی اصلاح

⏳ 22 ناکامیوں کے متعلق14 test files کی fixtures/تیاری درست ہوئی؛اس checkpoint میں production مالی اصول یا2963 test identities تبدیل نہیں ہوئیں۔ نیا Windows rerun ابھی موصول نہیں۔ [اصل نتائج اور کیس وار وجہ](WINDOWS-V1201-TEST-REPORT-REVIEW.md)۔

### 2.3 — موجودہ مقامی ثبوت

✅ v0.134.0 کے125 repository static checks،32 lexical-negative scripts،ان میں native fields کی45 نئی mutations،اور اس کے30 isolated SQLite reference plans پاس۔ پہلے11 متعلقہ SQLite reference scripts بھی دوبارہ پاس ہوئے۔ یہ واقعی محدود source/Python نتائج ہیں،C# tests یا Windows app کے نتائج نہیں۔ [تازہ trace/PT-76](NATIVE-FIELD-OUTCOME-REVIEW-URDU.md)۔

### 2.4 — موجودہ غیرچلے tests

⏳ کل3347 متوقع C# cases؛موجودہ پوری suite UNRUN ہے۔ آخری اصل2963 والے run سے384 تحریری cases زیادہ ہیں؛یہ384 اضافی کامیاب نتائج نہیں۔ یہاں `dotnet` SDK موجود نہیں؛Release build کی کوشش exit127 تھی۔ Grammar/static/SQLite reference کو compiler،Microsoft SQLite provider،authenticated session یا WPF execution کا بدل نہیں کہا جا سکتا۔

## 3۔ حصے1 تا5 میں کیا کیا ہوا؟

### 3.1 — حصہ1:متعلقہ compile خرابی

✅ expense-list test میں لازمی actor کا argument شامل کیا گیا؛مالک کے اصل Windows run نے متعلقہ Core/Tests Debug compile کی تصدیق دی۔ پورے سافٹ ویئر کی تکمیل نہیں۔ Commit `952b6b7`۔

### 3.2 — حصہ2:22 failures کی تیاری

⏳ حقیقی stored user/Owner،واضح grants/denials،درست fault ترتیب،stored audit name اور schema-valid marker کے fixtures درست ہوئے۔ Commit `89f14d8`؛نیا Windows نتیجہ باقی۔

### 3.3 — حصہ3:ریکارڈ Save کا نتیجہ

⏳ Product/Customer create/edit،Counter quick customer،Purchase quick supplier،چار quick catalogs اور Catalog/Unit Save میں local retry guards اور acknowledged save کے بعد درست cleanup۔16 Core operations کے observed outcomes؛72 نئے تحریری cases۔ Commit `d67d63e`؛اصل WPF callbacks،provider faults اور نئی suite کا run باقی۔ [PT-63](PARENT-RECORD-SAVE-OUTCOME-REVIEW-URDU.md)۔

### 3.4 — حصہ4:login/session اور موجودہ اجازت

⏳ Database-bound session proof،password/PIN بدلنے پر اگلے action سے نیا login،موجودہ readers اور Counter access ختم ہونے پر stale outputs کی صفائی۔22 نئے تحریری cases۔ D-116 لاگو کرنے کا source،commit `13a6062`؛اصل Windows keyboard/modal/session teardown اور باقی routes کی جانچ باقی۔ [PT-64](AUTHENTICATED-SESSION-REVIEW-URDU.md)۔

### 3.5 — حصہ5:پرانا فارم نئی تبدیلی نہ مٹائے

⏳ Snapshot یعنی متعلقہ data ایک مربوط حالت میں پڑھنا،اور write transaction میں loaded baseline کا مقابلہ۔ Product/Customer/extra answers/definitions/catalogs کے پہلے محدود راستے،25 نئے cases۔ Commit `27983d2`،v0.123.0؛پورا concurrency جائزہ یا حقیقی LAN/provider test نہیں۔ [PT-65](EDIT-CONCURRENCY-REVIEW-URDU.md)۔

### 3.6 — مشترکہ Drafts

⏳ Save/premark/discard میں baseline،coherent Open/List اور exact restore/held handoff؛دوسرے شخص کا نیا کام پرانے فارم سے overwrite نہ ہو۔36 نئے cases،commit `fe069fa`،v0.124.0۔ مشترکہ draft کی پرانی اجازت برقرار؛creator-only lock یا durable retry identity نہیں۔ [PT-66](DRAFT-EDIT-CONCURRENCY-REVIEW-URDU.md)۔

## 4۔ 5.1 کے دس source checkpoints

**یہ سب source پیشرفت ہے؛ان میں سے کسی کو پوری Windows تصدیق کے بغیر feature کا100% مکمل ہونا نہ سمجھیں۔**

| نمبر | source میں کیا شامل ہوا؟ | شائع شدہ حوالہ | نئے C# cases/حدود |
|---|---|---|---|
|4.1| مرکزی Settings کا coherent form،checked multi-key save،exact partial handoff | v0.125.0 / `b65d0a9` / [PT-67](SETTINGS-EDIT-CONCURRENCY-REVIEW-URDU.md) |25؛Windows باقی |
|4.2| Backup schedule اور Staff Pricing کے loaded baselines/current writer | v0.126.0 / `70c2812` / [PT-68](SCHEDULE-PRICING-CONCURRENCY-REVIEW-URDU.md) |25؛اصل backup/قیمت کے تمام راستوں کی سند نہیں |
|4.3| Hardware form dependencies،ایک atomic record+audit،checked printer handoff | v0.127.0 / `4cf37f6` / [PT-69](HARDWARE-EDIT-CONCURRENCY-REVIEW-URDU.md) |20؛آلات/Windows باقی |
|4.4| Icon Choose/Reset،owned bounded copy،known rollback cleanup،unknown پر retention | v0.128.0 / `4cda066` / [PT-70](ICON-EDIT-CONCURRENCY-REVIEW-URDU.md) |23؛فائل+DB کو مکمل atomic نہیں کہتے |
|4.5| Workbook Settings کے preview/dependency baselines،observed Apply | v0.129.0 / `08dcbd2` / [PT-71](WORKBOOK-SETTINGS-CONCURRENCY-REVIEW-URDU.md) |13؛باقی bulk routes/Excel/Windows باقی |
|4.6| Staff permissions کی پوری متعلقہ list،checked Set/Reset،exact handoff | v0.130.0 / `9d68420` / [PT-72](PERMISSION-EDIT-CONCURRENCY-REVIEW-URDU.md) |24؛remaining imperative callers/Windows باقی |
|4.7| Staff On/Off اور password/PIN reset کا checked loaded account/outcome | v0.131.0 / `15f924f` / [PT-73](STAFF-EDIT-CONCURRENCY-REVIEW-URDU.md) |26؛اصل sessions/provider/WPF باقی |
|4.8| Recovery word کے authorized form،change/reset baseline اور exact outcome | v0.132.0 / `5ccdb94` / [PT-74](RECOVERY-EDIT-CONCURRENCY-REVIEW-URDU.md) |26؛cache/provider/Windows باقی |
|4.9| CreateStaff کا current PIN rule،observed transaction،exact created row اور persistent local guard | v0.133.0 / `b5024ec` / [PT-75](STAFF-CREATE-OUTCOME-REVIEW-URDU.md) |26؛cross-restart/cross-PC شناخت نہیں |
|4.10| Native Required/Optional کی preprompt baseline،postmodal recheck،observed exact row اور UI invalidation | v0.134.0 / `fbe4218` / [PT-76](NATIVE-FIELD-OUTCOME-REVIEW-URDU.md) |21؛provider/WPF/حقیقی Windows باقی |

### 4.11 — ان اصلاحات کا عملی مقصد

ایک مالک فارم کھولے،دوسرا پہلے نئی تبدیلی Save کرے،تو پہلا پرانی حالت سے نئی محنت نہ مٹائے۔ Save کے بعد UI refresh خراب ہو تو محفوظ کام کو ناکام سمجھ کر دوبارہ اندھی کوشش نہ ہو۔ نہ یہ ہر جگہ exactly-once ضمانت ہے،نہ اس سے مالی/اسٹاک ملاپ خود ثابت ہوجاتا ہے۔

## 5۔ موجودہ حصے میں بالکل کیا باقی ہے؟

### 5.1 — اپنے سوالات/Custom definitions

⏳ Add،Update،On/Off اور ترتیب بدلنے کے الگ راستے۔ Core میں موجودہ Owner/CAS checks پہلے سے ہیں،مگر reviewed methods میں observed transaction outcome/exact post-write entity handoff اور VM کے retained retry/ack guards مکمل نہیں۔ Access refresh کے دوران دوسری definition بدلنے سے پوری list اور token تازہ ہونے کا راستہ بھی ملا۔ اسے طے شدہ exploit یا actual runtime failure نہیں،source risk کہتے ہیں۔ [شروع شدہ اگلا جائزہ](CUSTOM-FIELD-NEXT-SCOPE-URDU.md)۔

### 5.2 — باقی براہ راست Settings writers

⏳ تمام callers کی method-wise inventory،imperative write کے ارادے اور loaded-form write میں فرق،mixed settings/bulk/toggle actions کے outcomes۔ صرف مرکزی Settings form درست ہونے سے ہر caller درست ثابت نہیں ہوتا۔ نئی مکمل inventory کے بغیر ان کی حتمی تعداد یا پورے5.1 کی closure نہیں دی جا سکتی۔

### 5.3 — Windows startup registry اور SQLite

⏳ Registry پہلے،DB بعد میں لکھی جاتی ہے؛یہ دو الگ وسائل ہیں۔ DB refusal/unknown outcome کے بعد restore کی کوشش اصل registry کی کامیاب بحالی کا ثبوت نہیں۔ Current code میں best-effort restores اور بعض errors میں Nothing changed کا پیغام موجود ہے؛حقیقی Windows+provider faults،موجودہ raw registry state اور partial outcome کی handling کا محدود جائزہ باقی ہے۔

### 5.4 — StockTake اور باقی update/toggle/bulk/snapshot راستے

⏳ Open/Count/ClearCount/Found/Cancel،پرانے Apply guards کے ساتھ تعلق،stale session/row/order baseline،باقی readers/writers۔ یہ اسی حصہ5 کا کام ہے؛اسے حصہ6 میں دھکیل کر حصہ5 بند نہیں کریں گے۔

### 5.5 — موجودہ1تا5 کی ضروری تصدیق

⏳ تازہ Core/Tests build+3347-case run،Desktop Release،دو connections/LAN،current permission/session،provider COMMIT/rollback/dispose،WPF reentry/callbacks،فائل/registry/آلات کے متعلقہ PT-63–76 gates۔ ناکامی آئے تو اصل وجہ کی اصلاح اور rerun؛صرف نئے tests لکھ دینا closure نہیں۔

## 6۔ اس کے بعد کے9 بڑے مرحلے

| نمبر | نئے منصوبے کی حالت | باقی کام/بند ہونے کی شرط |
|---|---|---|
|6.1| ❌ اصل منصوبہ حصہ6 | جواب کھونے،page/process/restart/دوسرےPC کی retry identity؛نئی design/ضروری منظوری،پھر نفاذ اور faults |
|6.2| ❌ اصل حصہ7 | فروخت،خرید،returns،وصولی/ادائیگی،expense،drawing،writeoff،stocktake،گلے اور reports کا مالی/اسٹاک ملاپ |
|6.3| ❌ اصل حصہ8 | reports/Excel/CSV/PDF/receipt/print privacy؛Cost/Profit/main،Supplier B اور Blackbox/BookCheck کی منظورشدہ حدود |
|6.4| ❌ اصل حصہ9 | باقی photo/gallery actions،خراب/بڑی فائل،disk/ACL/partial copy/cleanup/power loss |
|6.5| ❌ اصل حصہ10 | encrypted manual/auto backup،password/rotation/DPAPI،wrong/corrupt/interrupted restore،نیاPC اور پرانا data محفوظ |
|6.6| ❌ اصل حصہ11 | الگ cloud environment،network loss/retry/duplicate/isolation/credentials/config؛اصل remote integration |
|6.7| ❌ اصل حصہ12 | Windows/DPI/dialog/keyboard/scanner/printer/camera،login/startup/lock/single instance/shortcuts |
|6.8| ❌ اصل حصہ13 | guide کے ہر action کا اصل app سے ملاپ،verified isolated DB/photos/config،پوری regression اور blocker closure |
|6.9| ❌ اصل حصہ14 | کامیاب Windows build/test/publish کے بعد test installer؛clean install/upgrade/uninstall/data preservation؛پھر واقعی فائل دینا |

یہاں6.1–6.9 صرف اس رپورٹ کی ذیلی numbering ہے؛اصل منصوبے کے حصہ6تا14 کو شروع کرنے کا دعویٰ یا نئی اجازت نہیں۔ پہلے1تا5 بند کرنے کی ہدایت برقرار۔

## 7۔ فیصلے:کیا محفوظ ہے،کس نے منتخب کیا؟

### 7.1 — مکمل تلاش کے قابل رجسٹر

✅ پہلے سے موجود D-01 تا D-116 کے116 canonical entries ملے؛کوئی درمیانی عدد غائب نہیں۔ D-00،D-00a تاD-00d کےپانچ بنیادی اصول بھی الگ محفوظ ہیں۔ موجودہ واضح reporting/publication instruction کو D-117 میں شامل کیا گیا۔ [تمام117 numbered entries + پانچ بنیادی اصول کا index](DECISION-REGISTER-INDEX-URDU.md)؛[اصل الفاظ/تفصیل](OWNER-DECISIONS.md)۔ Index میں موجود ہونا implementation/test PASS نہیں۔

### 7.2 — خاص طور پر برقرار رہنے والے فیصلے

- D-100:مالک کا Required/Optional اختیار،staged photo،نام لازمی،shared drafts؛supplier edit کی نئی سہولت خود سے نہیں۔
- D-106–108:منظورشدہ Counter UI،فی شخص pricing limits،اصل allocated cost سے نیچے نہ بیچنا،sale-level Extra اور partial return طریقہ B۔
- D-110:Profit اور Purchase Cost کے الگ checkboxes،مشترک main switch،اپنی module permission؛D-109 کی پرانی management-only پسند superseded ہے۔
- D-111:حساس مالی permissions کا واضح group،hover/help/details/current list؛یہ مالی ticks ملانے کا حکم نہیں۔
- D-112:ہر portion docs+اسی branch پرcommit/push+اصل remote تصدیق؛installer صرف حقیقی build/pack کے بعد۔
- D-113:editable bulk export/import کی الگ اجازت،Audit metadata،password والا encrypted backup؛ان کے اصل Windows gates باقی ہیں۔
- D-114:Supplier accounting exception B؛paid/payable/ledger/SR سے مخصوص inference قبول،direct item/batch cost کا الگ gate برقرار۔
- D-115:Blackbox کی صرف منظورشدہ menu visibility؛internals کا خودساختہ redesign نہیں۔
- D-116:password/PIN reset پر اگلے action سے تازہ login؛محفوظ records برقرار،ممکنہ unsaved loss قبول۔
- D-117:موجودہ جامع1.1-style Urdu report،تمام دستیاب فیصلوں کی documentation/publication اور اسی5.1 میں اگلا محدود کام۔

### 7.3 — “ڈارک اسٹریم” کی نسبت

⏳ دستیاب repository میں Darkstream/ڈارک اسٹریم کے نام سے الگ فیصلہ نامہ یا واضح author mapping نہیں ملا۔ کسی فیصلے کو اندازے سے اس نام سے منسوب نہیں کیا گیا۔ جو فیصلے واقعی محفوظ ہیں وہ سب نئے index میں ہیں؛اس کے علاوہ کسی غیرموصولہ الگ chat/file کے فیصلوں کا دعویٰ نہیں۔ ایسی اصل گفتگو/فائل ملے تو اصل authority اور supersession کے ساتھ شامل ہوگی؛موجودہ کام اس وضاحت کے انتظار میں روکا نہیں گیا۔

## 8۔ اتنی دیر کیوں لگی؟

### 8.1 — کام صرف نئے بٹن بنانا نہیں رہا

ایک ہی بٹن کے پیچھے UI،موجودہ account/session،permission،DB transaction،audit،file/registry،reload اور retry الگ boundaries ہیں۔ عام کامیاب save کافی نہیں؛آدھی ناکامی اور دوسرے writer کے ساتھ بھی data درست رہنا چاہیے۔

### 8.2 — الگ writer کے الگ خطرات

Staff Add نیا account بناتا ہے؛Required/Optional موجودہ preference بدلتا ہے؛Workbook کئی keys بدلتا ہے؛Icon میں فائل بھی بنتی ہے؛Startup Windows کو بدلتا ہے۔ سب پر ایک عمومی guard لگا کر یکساں مکمل کہنا غلط ہوتا۔ اسی لیے5.1 دس source checkpoints تک گیا۔

### 8.3 — سب سے بڑی رکی ہوئی شہادت

یہ Linux workspace ہے،یہاں.NET SDK/حقیقی WPF/Windows registry/دکان کے آلات نہیں۔ مقامی source checks جاری رہے،مگر اصل Windows نتائج جمع نہیں ہوئے۔ اس لیے code progress ہوئی مگر verification کا قرض بڑھا۔ اس رکاوٹ کو مزید docs/tests لکھ کر PASS نہیں بنایا جا سکتا۔

### 8.4 — ہماری reporting کی کمی بھی رہی

چھوٹے checkpoints شائع ہوتے رہے مگر ان کا ایک صاف مجموعی current-vs-historical خلاصہ کافی نمایاں نہیں تھا۔ پرانے pending جملے بعض documents میں باقی رہے؛اس سے حقیقی تکمیل کا تاثر الجھ سکتا تھا۔ اس رپورٹ،decision index اور live entry-point updates کا مقصد یہی کمی درست کرنا ہے،صرف مزید صفحات بڑھانا نہیں۔

### 8.5 — آئندہ قابلِ پیمائش پیشرفت

ہر اگلے محدود writer پر:موجودہ خطرہ،چھوا گیا source،محفوظ policies،جانچے گئے limited results،UNRUN gates،باقی methods اور اصل Git SHA۔ نیا بڑا blocker/policy آئے تو منظوری؛پرانے settled فیصلے باربار نہ کھولیں۔

## 9۔ کتنی دفعہ Next میں مکمل ہوگا؟

### 9.1 — پکی موجودہ گنتی

14 اصل حصوں میں1 کا خاص compile مقصد verified،2تا5 کےچار حصے جزوی،6تا14 کےنو بڑے حصے اس نئے منصوبے میں شروع نہیں ہوئے۔ یہ13 remaining work areas ہیں،13 Next میں مکمل ہونے کا وعدہ نہیں۔

### 9.2 — عارضی planning budget،ڈیڈ لائن نہیں

موجودہ معلومات کے مطابق باقی حصہ5 کو تقریباً **5–8 محدود کاموں** میں دیکھنا مناسب ابتدائی تقسیم ہے:custom Update،Add،toggle/reorder کے exact entity effects،imperative Settings inventory/fixes،startup partial outcome،StockTake/remaining routes اور closure audit؛کچھ اکٹھے ہوسکتے ہیں،کچھ مزید تقسیم ہوں گے۔ اس کے بعد موجودہ1تا5 کا مشترکہ Windows verification/fix cycle اور بعد کے9 بڑے حصے الگ ہیں۔

اگر صرف منصوبہ بندی کا عدد چاہیے تو **15–25 مزید کام/جانچ کے checkpoints کا عارضی بجٹ** رکھیں،نہ15–25 چیٹ پیغامات اور نہ مکمل ہونے کی ضمانت۔ یہ measured duration یا deadline نہیں؛باقی route inventory،Windows failures،provider faults،cloud/آلات کی دستیابی یا نئی منظورشدہ ضرورت سے بڑھ سکتا ہے۔ اسے پرانے3–6 rounds کے وعدے کی توثیق نہ سمجھیں۔

### 9.3 — اندازہ کب بہتر ہوگا؟

باقی writer inventory اور تازہ اصل Windows run ملنے پر۔ اگر اصل failure نکلتا ہے تو پہلے اس کی وجہ/اصلاح/نتیجہ،پھر تعداد دوبارہ۔ ہر بار صرف Next کہنے سے بیرونی Windows/آلات کے ثبوت خود پیدا نہیں ہوں گے۔

## 10۔ اب کون سا اگلا کام شروع کیا؟

### 10.1 — محدود انتخاب

⏳ اسی5.1 میں CustomField **existing Update** کا source جائزہ اور acceptance/test plan شروع ہوا۔ Add کی نئی شناخت،Move کی پوری entity ordering،startup اور مالی rules اس محدود implementation کے ساتھ خاموشی سے نہیں ملائے جائیں گے۔ Source paths/خطرات/حدود [الگ اگلے-scope فائل](CUSTOM-FIELD-NEXT-SCOPE-URDU.md) میں ہیں۔

### 10.2 — کیا ابھی نہیں کیا؟

اس جامع documentation turn میں production C#،SQL،migrations یا tests نہیں بدلے؛نئی CustomField fix یا اس کی کامیاب runtime جانچ کا دعویٰ نہیں۔ Investigation شروع ہونا اور implementation مکمل ہونا الگ ہیں۔ اگلا code checkpoint اسی منتخب Update راستے کو آگے بڑھائے گا؛حصہ6 شروع نہیں ہوا۔

## 11۔ GitHub اور دستاویزات

### 11.1 — کہاں اپڈیٹ ہوگا؟

اسی موجودہ branch `arena/b4b7bf7b-area11a` پر؛`main` میں merge نہیں۔ آخری source checkpoint `fbe4218` پہلے سے push/remote verified تھا۔ یہ docs/index/next-scope changes الگ documentation commit میں push ہوں گی،اور remote SHA+اصل file content الگ پڑھ کر verify کیا جائے گا۔ نتیجہ آخری گفتگو میں؛یہ پیراگراف پہلے سے کامیاب upload کا دعویٰ نہیں۔

### 11.2 — کیا اپڈیٹ/محفوظ کیا؟

مرکزی رپورٹ،تمام درج شدہ owner decisions کا index/anchors،اگلے CustomField scope کی inventory،AGENTS/HANDOFF/TAKEOVER/README/PROGRESS/باقی منصوبے اور User Guide کے نمایاں current-status links۔ پرانی evidence traces اوراصل Windows failure history حذف نہیں؛موجودہ summaries اور تاریخی notes کا فرق نمایاں کیا گیا۔ پورے repo کی ہر تاریخی لائن کو آج کے اعداد سے replace کرنا معلومات کو درست نہیں،خراب کرتا۔

### 11.3 — تاریخ کا معلوم فرق

موجودہ v0.134.0 code کے `ReleasedOn` میں2026-11-25 لکھا ہے،جبکہ اس جائزے کی دستیاب موجودہ تاریخ2026-11-10 ہے۔ اس future label کو اصل publication date یا کسی نئے Windows run کی تاریخ نہ سمجھیں۔ Published code label اس docs-only turn میں خاموشی سے نہیں بدلا؛پرانی version rows اور tests کی تاریخیں بھی تاریخی evidence ہیں،نئے actual results نہیں۔

### 11.4 — کیا upload نہیں کرنا چاہیے؟

اصل shop DB،تصاویر،backup passwords/keys،credentials،local logs اور generated binaries کو source Git میں نہیں ڈالنا۔ `.gitignore` کی حفاظت برقرار؛“سب کچھ upload” سےمراد متعلقہ source/docs ہے،حساس حقیقی data نہیں۔ Installer بنا ہی نہیں،اس لیے downloadable exe کا وعدہ نہیں۔

## 12۔ مکمل کب کہیں گے؟

### 12.1 — code کی شرط

باقی agreed routes کے current access،stale edit،observed outcome،retry اور exact handoff rules نافذ؛غیرمتعلق مالی/business policy unchanged یا الگ واضح منظوری۔

### 12.2 — ثبوت کی شرط

تازہ successful Core/Tests اورDesktop Release،پوری اس وقت کی test count/0 failure/0 skip،متعلقہ actual provider/WPF/LAN/file/registry/آلات tests،الگ DB/photos/config اور backup/restore safety۔ صرف3347 کا عدد دکھانا بھی کافی نہیں؛اصل logs/verdict لازم ہیں۔

### 12.3 — فراہمی کی شرط

اصل guide کا app سے ملاپ،باقی blockers صاف،پھر installer publish/pack،clean install/upgrade/uninstall/data-preservation checks اور واقعی بھیجی جانے والی فائل۔ **اس وقت5.1 جاری؛FullPhase4 اور installer مکمل نہیں۔**
