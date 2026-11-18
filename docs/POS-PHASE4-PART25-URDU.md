# PT-45 — Custom answers اور Native fields کی موجودہ رسائی

**v0.103.0 Turnstone · `#705D4E` · source checkpoint؛ C#/Windows UNRUN**

مثال: کسی customer کی اضافی شناخت کا خانہ کھلا تھا، پھر ملازم کا account بند ہوگیا۔ پرانے account سے وہ جواب save نہ ہو اور اگلے check پر panel کا پرانا جواب/چنی ہوئی تصویر کا نام صاف ہو۔ جواب save ہوگیا مگر دوبارہ پڑھنا ناکام ہوا تو صاف **SAVED** لکھا جائے، تاکہ غلطی سے دوبارہ save نہ کریں۔

[تفصیلی trace اور حدود](POS-PHASE4-CUSTOM-VALUES-CURRENT-REVIEW.md) · [مکمل اردو کتاب](USER-GUIDE-URDU.md)

## حفاظت پہلے

- اصل دکان پر فرضی customer، bill، answer یا fault نہ ڈالیں۔ Verified backup اور واقعی الگ **DB/photos/config paths** ضروری ہیں؛ صرف دوسری exe folder کافی نہیں۔
- Synthetic نام/جواب/تصویر استعمال کریں۔ اصل CNIC، customer data، PIN/password،backup password A یا Cloud key chat میں نہ بھیجیں۔
- **32 نئے facts لکھے ہیں؛ کل 1977 C# cases ابھی UNRUN ہیں۔** یہاں SDK نہیں، اس لیے compilation،WPF،timers،Windows screenshots یا اصل hardware کا PASS نہیں۔ Windows میں تازہ copy پر `tools\RUN-TESTS.cmd` چلانا باقی ہے۔

## کیا درست کیا؟ کیا جان بوجھ کر نہیں بدلا؟

1. Parent transaction کے اندر extra answers لکھتے وقت current active/same-role account دوبارہ پڑھتا ہے؛custom-value audit میں اسی stored account کا نام آتا ہے۔ Null/empty answers کے ساتھ creation بھی یہ check کرتا ہے۔
2. Values/history reader موجود نہ ہونے والے record کو کامیاب خالی نتیجہ نہیں کہتا۔ موجود record کے واقعی خالی answers کامیاب رہتے ہیں؛inactive master کی historical reading برقرار،writes کی پرانی active شرط بھی برقرار۔
3. Editor کی رسائی ناکام ہو تو rows،staged photo کا path/name،edit/loaded حالت صاف؛failure message پھر بھی نظر آتا ہے۔ Nominal3second loaded timer اور Unloaded پر stop۔ اجازت بحال ہونے پر parent form کو دوبارہ load کریں؛timer آپ کے unsaved edits خود reload نہیں کرتا۔
4. Separate Save کے بعد reload fail ہو تو **SAVED — دوبارہ save نہ کریں؛refresh کریں**۔ Native policy کی failed read پر پرانی selection/metadata صاف،risk prompt سے پہلے تازہ definition پڑھی جاتی ہے۔
5. **Counter سے quick customer creation پر نئی Customers grant شرط نہیں لگائی۔** Public answer editor/read/save کی الگ module شرط برقرار؛internal writer کی workflow اجازت اب بھی parent caller کی ذمہ داری ہے۔ یہ تمام parent commands کی مکمل اجازت یا اصل signed-in session کا ثبوت نہیں۔

## صرف الگ Windows test copy میں مشق

| # | عمل | متوقع نتیجہ — عملی تصدیق ابھی باقی |
|---|---|---|
| 1 | Customer extra answers کھولیں؛Customers grant واپس لیں | اگلے command/تقریباً3second access check پر panel کے پرانے answers اور staged تصویر کا نام صاف،Save/output denied؛failure message خالی panel کے ساتھ بھی نظر آئے |
| 2 | Account inactive یا stored role بدلیں؛پرانے actor سے parent creation آزمائیں | Extra-answer boundary پر انکار؛parent/value/audit writes rollback۔ Forged Owner/missing account بھی منع؛اصل session authentication پھر بھی الگ کام ہے |
| 3 | Permission بحال کریں | Parent form کو deliberate reload کریں تو تازہ answers آئیں؛timer discarded edits خاموشی سے واپس یا overwrite نہ کرے |
| 4 | Parent موجود مگر answers خالی،پھر parent غائب | موجود record کے empty answers جائز؛missing/invalid/nonpositive record failure،orphan values output نہیں۔ New-form preflight میں ID0 الگ جائز معنی رکھتا ہے |
| 5 | Parent inactive مگر موجود | Historical values/hidden-field labels برقرار؛نئے values لکھنے کی active-parent شرط برقرار |
| 6 | Current POS-only cashier؛Customers OFF؛quick customer create | نئی غیرمنظور Customers gate نہیں؛public Customer answer panel/read/save الگ grant سے۔ اس exception کو تمام Customers commands کی کھلی منظوری نہ سمجھیں |
| 7 | Owner نام بدلیں؛custom answers کے ساتھ نیا parent بنائیں | صرف `custom_value_set` audit میں موجودہ stored actor نام؛پورے parent audit کو اس حصے نے rewrite نہیں کیا |
| 8 | Developer-controlled isolated audit fault | Public custom save اور answers والے parent create کی DB تبدیلیاں rollback؛اصل persisted answer برقرار۔ File/power-loss کی global ضمانت نہیں |
| 9 | Separate answer Save commit ہو،پھر reload fail | واضح **SAVED،دوبارہ save نہ کریں،refresh کریں**؛failure کو commit undo نہ سمجھیں |
| 10 | Native fields میں selected critical field؛Owner inactive/role بدلیں | اگلے refresh/timer پر metadata/selection صاف؛stale risk prompt نہ ہو۔ Active owner بحال ہو تو تازہ list اور critical optional کی لازمی confirmation برقرار |
| 11 | Editor/Settings کو بار بار کھولیں/بند کریں | Loaded timer چلتا،Unloaded پر رکتا؛allowed custom edits ہر tick پر overwrite نہیں؛Native grid/selection بھی کامیاب tick پر دوبارہ نہیں بنتے۔ اصل focus/caret/dropdown/selection/dispatcher جانچ Windows میں کریں |
| 12 | Owner field definition بدل دے جبکہ دوسرا editor کھلا ہو | یہ timer definition-version sync نہیں؛fresh save validation پر required/unknown-field rules برقرار۔ مکمل stale definition/admin review باقی |

## اس ماحول کی جانچ

- **92 static checks PASS**؛413 C# grammar files،4542 calls،9157 references؛compiler نہیں۔
- **17 synthetic SQLite probes PASS**،جن میں source سے نکلی custom-answer/parent SQL کی نئی probe شامل ہے۔ یہ C# actor/grant/rollback execution نہیں۔
- **28 source-negative تبدیلیاں رد**؛اصل bytes بحال۔ Python backup reference کے6roundtrips/24negatives بھی PASS؛اصل C#/Windows restore نہیں۔
-716baseline paths برقرار؛114دوسرے Core service/data files byte-identical؛اصل SQL/normalization/active-parent write rule برقرار۔
- **32 نئے facts/کل1977C#cases ابھی UNRUN**؛Desktop/WPF/Windows/hardware/screenshots بھی باقی۔

## باقی کام

Custom-definition manager کی stale UI،باقی parent record create/update/read callers،اصل session اور دوسرے cached outputs؛پھر cross-service fault/retry/concurrency،files/cloud/encrypted restore/new-PC/DPAPI،Windows **1977+** tests اورDesktop/WPF/hardware/screenshots،نتائج کی fixes؛اس کے بعد مجاز **واحد sendable double-click test installer**۔ **ابھی installer نہیں بنا،Phase4 کھلا ہے۔**

Parent update اگر customValues نہ دے تو اندرونی writer تک آنا لازمی نہیں؛اسے اس حصے کی protection کا blanket دعویٰ نہ سمجھیں۔ Timer فوری screen recall نہیں؛پرانا returned dictionary،parent کے دوسرے text boxes،paper/PDF یاexternal window واپس نہیں مٹتے۔ Unknown free-text answer کو خودکار cost/profit classification نہیں دی۔

حساب،Supplier B،الگ Cost/Profit/main،below-cost/Extra/overpayment،Blackbox/BookCheck،backup password A/format اور stored audit history نہیں بدلے۔ مزید **3–6 مربوط review/result چکر** مشروط اندازہ ہے،حد یا وعدہ نہیں؛findings سے بڑھ سکتا ہے۔ ہر portion کے docs،same-branch commit/push اور actual remote verification لازم ہیں۔
