# PT-46 — نمبر8: Custom-field manager کی تازہ اجازت اور selection

**v0.104.0 Knot · `#5A627E` · source checkpoint؛ Windows/C# UNRUN**

[نمبر وار36کام کی موجودہ فہرست](NUMBERED-WORK-STATUS-URDU.md) · [تفصیلی trace/حدود](POS-PHASE4-CUSTOM-MANAGER-CURRENT-REVIEW.md)

## 1۔ کس نمبر پر کام ہوا؟

**نمبر8 — اپنے سوال بنانے/بدلنے والا manager۔** مثال: مالک نے “CNIC” کھولا،پھر account بند ہوگیا یا دوسری window نے اسی سوال کی type بدل دی۔ پرانی selection/text سے غیرارادی تبدیلی نہیں ہونی چاہیے؛failed reload کو بھی “سب ٹھیک save ہوگیا اور screen تازہ ہے” نہیں کہنا چاہیے۔

## 2۔ source میں کیا مکمل ہوا؟

1. Manager کو supplied Owner label سے نہیں،current management read کامیاب ہونے کے بعد enable کرتا ہے۔ Core کے چار definition writers اور management/inactive list میں supplied active/positive Owner check بھی شامل؛موجودہ stored Owner Tx check برقرار۔
2. Failed read/access پر list،selection،editing ID،label/options/help،required tick اورtype reset؛“Fields not loaded”،مصدقہ “no fields” نہیں۔ Empty overlay صرف واقعی کامیاب خالی list پر۔
3. Action سے پہلے current list اور selected definition کا تقابل؛ID،form،stable key،label،type،options،help،required/active/order/creation stamp میں فرق یا missing target ہو تو action رک جاتا ہے،editor صاف اور واضح review warning آتی ہے۔
4. New/Cancel،Save،Move،Hide/Restore اورSuggestion میں fresh check؛دوسرے tab کی پرانی row قبول نہیں۔ Document forms میں Photo suggestion نہیں۔
5. Add/Update/Move/Hide/Restore commit کے بعد reload fail یا saved target غائب ہو تو **SAVED — action دوبارہ نہ کریں؛refresh سے حالت دیکھیں**۔
6. Nominal3second loaded timer،Unloaded پر stop؛unchanged tick پر rows/selection/آپ کا typed text دوبارہ نہیں بنتا۔ اصل shell Refresh کے لیے public Refresh راستہ؛نیا اندرونی duplicate Refresh button نہیں۔

**واضح حد:** منتخب definition واقعی بدل چکی ہو تو unsaved edits صاف کیے جاتے ہیں اور پیغام بتاتا ہے؛انہیں خودکار merge/save نہیں کرتے۔ Manual Refresh/tab change یا Move/Toggle کا post-action reload بھی editor تازہ کرسکتا ہے۔ یہ atomic concurrency lock نہیں۔

## 3۔ الگ Windows test copy میں ایک ایک مشق

اصل دکان پر fake record/field/fault نہ بنائیں۔ Verified backup اور واقعی الگ **DB/photos/config paths** لازم؛صرف دوسری exe folder کافی نہیں۔ Synthetic labels/answers استعمال کریں؛CNIC/password/Cloud key/backupPasswordA chat میں نہ دیں۔

| نمبر | عمل | متوقع نتیجہ — Windows تصدیق ابھی باقی |
|---|---|---|
| 8.1 | Field select کرکے label/options/help لکھیں؛account inactive/role بدلیں | اگلے command/تقریباً3second check پر rows/selection/typed inputs/required صاف،manager disabled؛failed read پرno-fields دعویٰ نہیں |
| 8.2 | Manager کو Settings grant دیں مگرOwner نہ بنائیں؛forged Owner/missing account بھی آزمائیں | Core management read/write منع؛بغیر تبدیلی/audit addition۔ عام staff کے جائز answer routes الگ ہیں |
| 8.3 | Current Owner بحال کریں | تازہ list؛پرانے discarded edits خود واپس/save نہ ہوں؛دوبارہ field منتخب کریں |
| 8.4 | پہلی window میں field edit؛دوسری سے اسی کا نام/type/options/help/required/active/order بدلیں | اگلےcheck/action پر mismatch warning،پرانا action رکے،selection/editor صاف؛تازہ field دیکھ کر دوبارہ فیصلہ کریں |
| 8.5 | Selected field غائب ہو یا دوسرے tab کی پرانی row آئے | پرانےID پر یا دوسرےform کےfield پر غیرارادیaction نہ ہو |
| 8.6 | صرف دوسری غیرمنتخب definition بدلے،یا کوئی تبدیلی نہ ہو | unchanged tick پر list/text churn نہیں؛دوسری row کی اصل تبدیلی آئے تو list تازہ ہوسکتی ہے مگر منتخب field کے unsaved text محفوظ رہیں |
| 8.7 | Add/Update،Move،Hide/Restore save کے بعد management reload جان بوجھ کر fail | واضح **SAVED،دوبارہactionنہکریں**؛failed repaint کو DBrollback نہ سمجھیں |
| 8.8 | Developer-controlled isolated audit fault میں چارwrites | Add/Update/Hide/Move کی DB changes rollback؛stable key/order/پرانےanswers محفوظ۔ اصل C# execution درکار |
| 8.9 | سوالHide،Restore،Rename کریں | سوال/answers delete نہیں؛وہیstablekey/history۔ Document Photo کی پرانی ممانعت برقرار |
| 8.10 | View باربار کھولیں/بند کریں،type کریں،selection/arrows/Tab استعمال کریں | Loaded timer/Unloadedstop،اصلWPFfocus/selection/typing نہ ٹوٹے؛صرفstaticcheckکافی نہیں |

## 4۔ کیا جانچا اور کیا نہیں؟

**32 نئے facts لکھے ہیں:14 persisted snapshot comparison +18 real-schema Core regression facts؛کل متوقع2009C#cases۔ سب یہاں UNRUN۔** Snapshot helper،currentOwner/no-op guards،stored audit name،audit rollback/history/document rules کے tests موجود؛یہ WPF tests نہیں۔ **یہاں کی اصل جانچ:**93staticchecksPASS؛415C#grammarfiles،4564calls،9248references،0structuralissues۔17موجودہsyntheticSQLiteprobesاور32source-negativechecksPASS؛اصلbytesبحال۔ Pythonbackupreferenceمیں6roundtrips/24negativesPASS۔ یہC#/Windowsexecutionنہیں۔721baselinepathsبرقرار،115دوسرےCoreServiceDataاورپرانےCoreModelsbyte-identical؛اصلSQL/value-writer/parentpolicyunchanged۔

## 5۔ نمبر8 میں کیا باقی ہے؟

اصل Windows/WPF manager،timers،selection/focus اورfault messages کا عملی ثبوت۔ دوسریwindow check کے بعد مگرwrite transaction سے پہلے پھر بدل سکتی ہے؛یہ atomic compare-and-swap/version token نہیں۔ اصل session،تمامparentcommands،دوسرےrecordeditorsکیdefinition-version sync اورglobal concurrency الگ باقی ہیں۔

## 6۔ اگلا نمبر اور installer

اگلی مربوط ترجیح **5،6،7**: باقی session/account/parent-record callers اورstaleoutputs۔ پھر **23–25** fault/rollback/retry/concurrency،**26–29** files/backup/restore/cloud،**2/4/30–35** Windows2009+tests/results/hardware/isolation؛آخر **36** واحدsendabledouble-clicktestinstaller۔ **ابھیinstallerنہیںبنا،Phase4کھلاہے۔**

یہ36الگNextنہیں۔3–6مزیدمربوطreview/resultچکروںکامشروطاندازہحدیاوعدہنہیں؛findingsسےبڑھسکتا۔ حساب،SupplierB،Cost/Profit/main،Extra/returnB،below-cost/overpayment،Photo/backupformatاورBlackbox/BookCheckنہیںبدلے۔ ہرحصےمیںdocs،sameactivebranchcommit/pushاورactualremoteverificationلازم۔
