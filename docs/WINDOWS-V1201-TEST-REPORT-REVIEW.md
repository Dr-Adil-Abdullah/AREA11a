# ⏳ حصہ2/14 — اصل Windows کی22 ناکامیوں کا جائزہ

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

## ✅ موصول شدہ اصل نتیجہ،نئے نتیجے سے الگ

مالک کے v0.120.1 Alder Windows/.NET10 log میں Core اور Tests کمپائل ہوئے؛2963 کل،2941 کامیاب،22 ناکام،0 چھوڑے گئے؛وقت5 منٹ20 سیکنڈ۔ اس سے حصہ1 کی متعلقہ compile اصلاح ثابت ہے،Desktop/Release/installer یا نئی v0.120.2 suite کی کامیابی نہیں۔ مالک کے `next` سے صرف حصہ2 کی اجازت ملی؛حصہ3 شروع نہیں ہوا۔

## ✅ محدود source اصلاحات — v0.120.2 Willow

یہ14 test files کی تیاری اور پرانے پیغام/نام کی توقعات کی اصلاح ہے؛production services،مالی SQL،اجازتوں کی پالیسی اور37 migrations نہیں بدلیں۔ انکار،موجودہ شناخت اور unchanged-state assertions برقرار ہیں؛Settings کی تیاری اب کامیابی بھی جانچتی ہے۔

| ناکام کیس/گروپ | تعداد | وجہ اور محدود اصلاح |
|---|---:|---|
| Receipt: shop name،copies،80mm،hide discount |4| بغیر actor کی Settings تبدیلی رد تھی؛اب Owner سے تبدیلی اور `.Ok` لازماً جانچا۔ اسی فائل کی دوسری null-actor تیاری بھی درست تاکہ width/cut checks بےاثر نہ ہوں۔ |
| SplitPayment: credit part/Owner and Manager override |2| limit فعال کرنے کی actorless تبدیلی رد تھی؛checked Owner سے فعال۔ Manager کے stored account کے ساتھ override OFF پر انکار/کوئی اضافی sale نہیں،ON پر کامیابی کی جانچ بھی شامل۔ |
| CustomerCurrentAccess: profit-only/products-only |2| Cashier کو Customers پہلے سے ملتا ہے؛اب اس grant کو واضح false کرکے اصل denial assertions چلتے ہیں۔ |
| MasterFoundation: settings audit |1| موجودہ محفوظ Owner نام `admin` ہے؛`TestData.SeedOwnerName` سے موازنہ،بنائے ہوئے `Owner` نام سے نہیں۔ |
| Recovery: permission list |1| غیرموجود Manager9 کے بجائے database میں حقیقی Manager؛explicit denial اور دیگر assertions برقرار۔ |
| Partner: cashier goods return |1| Supplier module gate پہلے انکار کرتا ہے؛پرانی rank عبارت کی جگہ permission error؛stock unchanged بھی جانچا۔ |
| SupplierLedger: cashier Pay/Return |1| غیرموجود Cashier99 کے بجائے حقیقی stored cashier،Suppliers واضح false؛دونوں انکار،اصل balance/batch assertions کے علاوہ payment count،کل stock،audit اور payable بھی unchanged۔ |
| CounterPostingIntegrity: closed/unreadable drawer theory |2| fault کے بعد Sales helper drawer کھول دیتا/ناکام ہوتا تھا؛اب service پہلے تیار،پھر drawer fault،اسی service سے Create؛snapshot/reconciliation برقرار۔ |
| AutomaticDraftPosting: marker theory case |1| posted marker میں `posted_at` نہ ہونے سے schema CHECK پہلے روک رہا تھا؛اب درست marker fixture،اصل Hold/no-commit/snapshot assertions برقرار۔ |
| Stock: Manager write-off |1| غیرموجود Manager3 کی جگہ حقیقی stored Manager؛تاریخ اور مقدار کی جانچ برقرار۔ |
| Provisional: Manager attach/cancel |1| Cashier/Manager database میں واقعی بنائے؛Cashier انکار اور Manager link assertion برقرار۔ |
| PurchasePreparation: Owner inactive/role changed |2| account fault کے بعد Drafts factory drawer کھولنے کی کوشش کرتی تھی؛اب fixture کی پہلے سے تیار service استعمال،اصل access/create/open/post denial اور snapshot یکساں۔ |
| ShiftHandover: no permission/signatures |2| موجودہ drawer permission عبارت اور اصل stored Owner نام سے موازنہ؛refusal/current-open،دونوں signatures اور32-column assertions برقرار۔ |
| PricingPhaseTwo: role mismatch |1| Quote میں drawer gate پہلے account-changed کہتا ہے؛اسی درست انکار کا پیغام جانچا؛pricing throws،Quote/Create refusal اور صفر financial rows برقرار۔ |
| **کل** |**22**| **کوئی test skip/delete یا security-policy bypass شامل نہیں۔** |

## ✅ مقامی محدود جانچ

- `tools/check_repo.py --quiet`:111 static checks؛یہ C# compile یا test execution نہیں۔
- `tools/check_windows_v1201_source_negative.py`:22 lexical mutations رد،اصل bytes بحال؛یہ حقیقی permission enforcement کا runtime ثبوت نہیں۔
- `tools/check_windows_v1201_fixture_sqlite.py`:37 اصل migrations؛محفوظ Owner نام،غیرموجود/حقیقی Manager،explicit deny lookup؛پرانا ادھورا marker CHECK سے رد اور row unchanged،نیا اصل fixture marker قبول اور sales صفر۔ Python probe ہے،C# provider/transaction یا Windows test نہیں۔
- پہلے سے موجود14 test files کی test method/Fact/Theory/InlineData شناختیں baseline سے برابر؛2963 کی نئی discovery یہاں نہیں چلائی گئی۔

## ⏳ اصل دوبارہ جانچ — ابھی باقی

یہاں `dotnet build dotnet/Area11.Tests/Area11.Tests.csproj --configuration Release` exit127 سے رکا: `dotnet: command not found`۔ مقامی SDK نہیں؛اس لیے22 ناکامیاں عملی طور پر دور ہونے یا2963/2963 کامیاب ہونے کا دعویٰ نہیں۔

اسی برانچ کے v0.120.2 source پر Windows میں `tools\RUN-TESTS.cmd` چلائیں اور نیا مکمل test log دیں۔ متوقع دریافت2963 ہے؛0 failures مطلوب ہیں۔ پرانا v0.120.1 log نئی run کی جگہ نہیں۔ run الگ test data پر ہو؛اصل دکان پر فرضی posting نہیں۔ verified backup اور الگ DB/photos/config کی شرط برقرار۔ نئی ناکامی آئے تو اسی حصہ2 میں جائزہ،حصہ3 خود سے نہیں۔

⏳ FullPhase4 کھلا؛❌ installer binary نہیں بنی۔
