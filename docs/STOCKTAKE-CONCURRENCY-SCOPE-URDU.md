# PT-89 — Settings کی باقی حد اور StockTake کا ابتدائی جائزہ

**⏳ 2026-11-10؛ موجودہ ایپ v0.149.0 Gentian۔ یہ صرف جائزے/دستاویزات کا checkpoint ہے، نئی build نہیں۔** تازہ `next` سے باقی Settings راستوں کی حد اور StockTake کا محدود جائزہ شروع ہوا۔ اس مرحلے پر production code، مالی formula، migrations، اصل tests، version، رنگ یا runner نہیں بدلے۔ StockTake کی اصلاح ابھی شامل نہیں؛ Apply کے ایک اہم طریقۂ کار پر مالک کا جواب درکار ہے۔

## ⏳ سب سے اہم مسئلہ — گنتی کے دوران اصل اسٹاک بدلنا

StockTake کھولتے وقت book quantity اور cost محفوظ ہو جاتے ہیں۔ اس کا مطلب **دکان کے سارے لین دین پر lock لگ جانا نہیں**۔ دوسری فروخت، خرید یا adjustment بعد میں stock بدل سکتی ہے۔

موجودہ `StockTakeService.ApplyInside` میں:

- آخری batch quantity کو counted quantity بنا دیا جاتا ہے؛ اس سے پہلے موجودہ quantity اور frozen book کے فرق کی جانچ نہیں۔
- `adjust` movement کی quantity **counted − frozen book** ہے، موجودہ stock سے ہونے والی اصل correction نہیں۔ shortage/excess کی value بھی frozen cost سے بنتی ہے۔
- مثال: frozen book **100**، محفوظ گنتی **90**۔ درمیان میں stock **95** ہو گیا۔ Apply اسے **90** کرتا ہے، یعنی اصل تبدیلی **−5**، مگر movement **−10** بنتی ہے۔
- دوسری مثال: موجودہ stock **105** ہو تو Apply کی اصل correction **−15** ہے، مگر movement پھر بھی **−10**۔

**⏳ اس سے stock اور movement ledger کی مطابقت اور shortage/excess کی تعبیر متاثر ہو سکتی ہے۔** یہ حقیقی دکان پر آزمایا نہیں گیا؛ نیچے درج isolated SQL reproduction اور source review اس خطرے کا محدود ثبوت ہیں۔ اسے صرف cosmetic مسئلہ یا کامیاب concurrency handling نہیں کہیں گے۔

Spec §19.2 frozen book، owner-only Apply، frozen valuation، تاریخی minus کی اصلاح اور once-only Apply کہتا ہے؛ چلتی خرید/فروخت کے بعد stale count کے لیے ان دونوں طریقوں میں سے کوئی واضح نیا فیصلہ موجود نہیں ملا۔ Migration 008 اور service comments دکان بند کرکے گنتی کا طریقہ بیان کرتے ہیں، مگر source میں پورے کاروبار کو روکنے والا lock نہیں۔ اس لیے خود سے نیا stock/valuation اصول نہیں لگایا۔

### ⏳ مالک کا سوال — جواب ابھی نہیں ملا

**A — تجویز کردہ محتاط راستہ:** متعلقہ counted batch کا stock گنتی کے سیشن کے دوران بدل جائے تو پورا Apply stock لکھنے سے پہلے روکیں۔ پرانی sheet محفوظ رہے؛ وجہ بتا کر سیشن منسوخ کریں، نیا سیشن اور دوبارہ جسمانی گنتی/جائزہ کریں۔ پرانے count پر خودکار adjustment یا خاموش نئی baseline نہ لگے۔ اس میں دوبارہ گنتی کی محنت ہو سکتی ہے؛ دکان بند کرکے گنتی کرنا پھر بھی ضروری عملی احتیاط ہے۔

**B — آخری جسمانی گنتی حتمی:** تبدیلی کی واضح تنبیہ کے بعد مالک موجودہ جسمانی گنتی دوبارہ تصدیق کرے؛ اسی کو موجودہ batch کی آخری quantity مانا جائے۔ موجودہ stock سے اصل correction اور frozen book کا تقابلی فرق الگ، واضح طور پر دکھانا/درست درج کرنا ہوگا۔ محفوظ پرانی گنتی کو خودکار طور پر تازہ تصدیق نہیں مانیں گے۔ یہ موجودہ stock اور ledger/variance کے تعلق میں زیادہ وسیع تبدیلی ہوگی، صرف ایک warning کا اضافہ نہیں۔

دونوں راستوں میں stale sheet counts، fresh authorization، atomic failure اور unknown outcome کی حفاظت بھی ضروری رہے گی۔ **جواب آنے سے پہلے نہ A نافذ ہے، نہ B؛ کوئی D-120 فیصلہ فرض یا درج نہیں کیا گیا۔** پچھلے D-118/A Focus اور D-119/C Startup الگ فیصلے ہیں، اس سوال سے نہیں بدلتے۔

## ⏳ StockTake کے باقی مشاہدات اور اصلاح کی مجوزہ حد

| راستہ | موجودہ source میں کیا دیکھا | ابھی کیا باقی ہے |
|---|---|---|
| Open | transaction میں session/book snapshot؛ مگر ہر call نیا session کھول سکتی ہے۔ نتیجہ عام failure پر commit uncertainty الگ نہیں بتاتا۔ | Open کا observed outcome، exact created session، UI replay/cleanup guard؛ multiple-open policy خود سے نہ بدلیں۔ |
| Count | input finite/non-negative اور whole-unit checks؛ open status چیک ہوتا ہے۔ UPDATE صرف line ID سے، displayed count/pack baseline نہیں۔ permission کا ابتدائی check write transaction سے پہلے ہے۔ | current in-transaction actor/permission، loaded row/session/pack/unit baseline، stale refusal، exact count receipt اور observed result۔ stock/valuation formula نہیں بدلنی جب تک متعلقہ فیصلہ نہ ہو۔ |
| ClearCount | open sheet lookup، پھر row ID سے clear؛ دوسرے operator کی نئی count اور note مٹ سکتی ہے۔ | Count کے ساتھ مشترکہ expected-state اور retained UI guard؛ غلط row/پرانی count پر صاف انکار۔ |
| AddFound | open status، active product اور duplicate found-row check موجود؛ displayed sheet/product intent اور observed outcome الگ نہیں۔ | session/product baseline، current authorization، exact inserted row اور unknown replay روک۔ نئی GINTI policy نہیں۔ |
| Cancel | caller کے role اور reason کا ابتدائی check؛ open status transaction میں؛ مگر displayed sheet state نہیں ملتی۔ | current stored actor/permission، loaded sheet baseline، observed outcome؛ بعد کی counts کو لاعلمی میں cancel نہ کریں۔ |
| Apply | observed `TransactionOutcome` اور Desktop `PostingAttemptGuard` موجود؛ transaction میں current reader بھی؛ دوبارہ applied session رکتا ہے۔ | مالک کا اوپر والا فیصلہ، displayed sheet/session baseline، موجودہ Owner/current-reader checks برقرار رکھتے ہوئے exact handoff اور reset کی قابلِ اعتماد کامیابی۔ اسے ابھی مکمل محفوظ نہیں کہیں گے۔ |
| Current/History/Sheet/Print | current-reader/cost projections موجود؛ کئی queries الگ reads، typed failure کی جگہ بعض APIs null/empty list دیتی ہیں۔ | coherent authorized snapshot، read failure اور واقعی empty میں فرق، اصل WPF/readback جانچ۔ |

`StockTakeViewModel.Load/ReloadSheet` مختلف Current/History/Sheet calls جوڑتے ہیں۔ Filter/Refresh پر displayed rows اور نئے totals الگ وقت کے ہو سکتے ہیں؛ read failure خاموش empty بنا سکتی ہے۔ Count/Clear/AddFound/Cancel میں Apply جیسا مکمل operation guard نہیں۔ Apply کا reset `Load()` کے واپس آنے کو کامیاب reload مانتا ہے، جبکہ بعض service read APIs failure چھپا دیتی ہیں۔ اس لیے صرف service میں نیا token لگا کر پورا UI محفوظ ہونے کا دعویٰ نہیں کیا جا سکتا۔

پرانے `StockTakeTests` کی اصل assertions برقرار ہیں: frozen book/cost، negative correction، finite whole count، uncounted ≠ zero، owner-only/once-only Apply اور GINTI۔ نئی implementation کے لیے additive tests لازم؛ پرانے tests کو ہٹا کر یا کمزور کرکے policy تبدیل نہیں کرنی۔ موجودہ tests کا دوبارہ اصل Windows execution اس جائزے میں نہیں ہوا۔

## ✅ محدود SQL reproduction — خرابی کا ثبوت، اصلاح کا PASS نہیں

اصلsourcebaseline`4693dee77c2b11f67a907f8ac24ff8faaa8b279c`کی37migrationfilesسےدوdisposableSQLitefilesبنائیں؛ ایک WAL، ایک DELETE۔ کوئی حقیقی shop DB، user، bill، customer یا registry استعمال نہیں ہوا۔ synthetic product/batch/sheet میں book 100 اور frozen cost 20000 paisa رکھی۔ موجودہ `StockTakeService.cs` سے `Count` کا UPDATE، `ClearCount` کا UPDATE، Apply کا batch UPDATE اور movement INSERT نکالے؛ **Python نے اپنی transaction اور branch control flow چلائی، C# service نہیں۔**

ہر journal میں یہ چار صورتیں reproduce ہوئیں، مجموعی **8 defect scenarios**:

| صورت | موجودہ SQL کا نتیجہ |
|---|---|
| دونوں screens نے پرانی row دیکھی؛ پہلے نئی count 90، پھر پرانی screen سے 80 | آخری count 80؛ نئی count/note خاموش overwrite |
| نئی count 90 کے بعد پرانی screen سے Clear | count NULL؛ نئی count صاف |
| book100/count90؛ synthetic intervening stock95 | final90، اصل correction−5، movement−10 |
| book100/count90؛ synthetic intervening stock105 | final90، اصل correction−15، movement−10 |

Stock95/105 کو Python نے fixture میں براہ راست رکھا؛ اسے اصل Sale/Purchase service، authorization، C# guard، مالی posting یا Microsoft.Data.Sqlite provider کا execution نہ کہیں۔ Sales/Purchases tables میں حقیقی documents صفر رہے۔ یہ محفوظ tests کے کامیاب ہونے کی گنتی نہیں؛ معلوم خطرات کی reproduction ہے۔ temporary files خود ختم ہوئیں؛ production data محفوظ/بےتبدیلی۔

## ⏳ Settings inventory — کس چیز کو اب بھی بند نہیں کہنا

یہ production caller/source کی محدود فہرست ہے، تمام public APIs یا provider/Windows کی certification نہیں:

| گروہ | موجودہ checked راستہ / حد |
|---|---|
| Main Settings/Focus choice | `SettingsViewModel` سے `SaveEditForm`، پہلے کے main/secondary outcome guards؛ WPF/provider proof باقی۔ |
| Counter print choice، Backup schedule | `SaveEditForm` اور loaded baselines؛ پرانے checkpoints کا source، کوئی نئی runtime سند نہیں۔ |
| Labels | UI `ResetChecked`؛ عمومی `AppLabelService.Save/Reset` کے imperative APIs اب بھی موجود، ہر host کو checked نہیں کہیں گے۔ |
| Staff/Pricing/permissions/recovery | production UI سے متعلقہ versions/forms؛ CreateStaff کے لیے observed local guard۔ تمام legacy overloads یا callback faults کو اس review سے بند نہیں کیا۔ |
| Icon/Hardware | expected forms، checked saved-setting route؛ file/native device اور DB ایک atomic resource نہیں۔ |
| Workbook Settings | preview-owned Settings baseline/current transaction؛ باقی workbook/business routes کے لیے ان کی اپنی محدود reviews لاگو۔ |
| Startup | production checked registry/form route، bounded compatibility اور Shift recovery پہلے الگ کیے؛ legacy bool کی exactness اور `SetMachineAutoStart`/installer system path کی حدود برقرار۔ |
| Session telemetry | v0.149.0 کا atomic shared-marker/checked outcome؛ durable PC/run identity نہیں۔ |
| Backup/Sync runtime stamps | BackupService کے lastRunAt/lastFile اور SyncService کے lastSync/status/error میں actorless `SetUiOnly` calls اب بھی ہیں؛ بعض results نظرانداز ہوتے ہیں۔ یہ user form edits نہیں، مگر failure/atomicity/outcome کا باقی الگ review ہے۔ Backup restore-summary کا result الگ استعمال ہوتا ہے۔ |
| Numbering | caller-owned transaction میں counter؛ اسے عمومی cached UI preference نہ سمجھیں، مالی numbering کو یہاں نہیں بدلا۔ |

**⏳ نتیجہ:** مرکزی Settings کے پہلے fixes کی موجودگی کو تمام Settings writers کی تکمیل نہیں بنایا۔ backup/sync bookkeeping، legacy/system paths اور حقیقی Windows/provider/WPF جانچ کھلی ہیں۔ StockTake شروع کرنے کا مطلب انہیں خاموش DONE کرنا نہیں۔

## ✅ دستاویزات کی حد / ⏳ باقی ثبوت

- production/Core/Desktop،37 migrations،تمام اصل C# tests،version/theme/icon/runner اس checkpoint میں بےتبدیلی۔ build **v0.149.0** اور متوقع suite **3748** ہی ہیں؛ کوئی نیا C# case نہیں۔
- سابقہ **139 static / 46 source-negative scripts / 66 telemetry mutations / 34 telemetry SQL plans** صرف اپنے محدود checkpoint کے نتائج ہیں؛ آٹھ نئی reproductions ان میں اصلاح یا runtime PASS کا اضافہ نہیں۔
- مالک کا اصل **v0.138.0 Windows/.NET10:3418PASS/0FAIL/0skip،6m50s** ثابت ہے،pending نہیں۔ بعد کے 330 cases اور تازہ 3748 suite یہاں UNRUN؛ Desktop/30 پرانے warnings الگ باقی۔
- ✅ اس documentation checkpoint پر139repository/staticchecksپاس؛478C#filesکیgrammar،4999calls/10857memberreferencesکیstaticresolutionمیں0issues۔ feature-boardکے57partialstatuses/checklistبرقراراورrulesmatrixup-to-date؛یہ.NETbuildنہیں۔

## ⏳ اگلا قدم

پہلے اوپر **A/B** کا جواب؛ اسے اگلے حقیقی Owner decision کے طور پر درج کریں، پھر اسی محدود StockTake اصلاح کا واضح source/test/UI دائرہ بنائیں۔ جواب سے پہلے live stock arithmetic یا عالمی business lock نہ لگائیں۔ باقی1تا5 کی اصل ضروری تصدیق، guide UI اور Settings runtime bookkeeping ابھی کھلے ہیں۔ **❌ حصہ6/durable identity/installer شروع نہیں۔**
