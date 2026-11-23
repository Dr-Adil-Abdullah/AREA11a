# ⏳ باقی کام — منظورشدہ 14 حصوں کا منصوبہ

## ✅ کام اور گفتگو کے موجودہ اصول

- ✅ ہر جواب،وضاحت اور خلاصہ سادہ پاکستانی اردو میں ہوگا۔
- ✅ ایک وقت میں صرف ایک حصہ؛اگلا حصہ مالک کی نئی اجازت کے بعد۔
- ✅ ہر ٹاسک/حصے کے بعد صرف3–4 لائنوں کا خلاصہ؛تفصیلی رپورٹ ہر3 مکمل حصوں کے بعد یا مالک کے مطالبے پر۔
- ✅ نشان:✅صرف متعلقہ کام مکمل؛⏳کچھ باقی؛❌متعلقہ کام شروع نہیں ہوا۔ حقیقی کمپائل/عملی جانچ کو static checks نہیں کہا جائے گا۔
- ⏳ ہر حصے کی ضروری دستاویزات،اسی موجودہ برانچ پرcommit/push اور الگ actual remote تصدیق برقرار۔ نئی بڑی رکاوٹ یا پالیسی سامنے آئے تو پہلے منظوری؛خود سے اضافی حصہ نہیں۔

## ⏳ مالک کی تازہ ترتیب — پہلے1تا5 بند،پھر6

نامکملحصہ5کےاگلےکام5.1،5.2کیذیلیگنتیسے؛**1تا5کاباقیکاماورضروریتصدیقمکملکیےبغیر6شروعنہیںکرنا۔** اسوقتصرف1کیخاصcompileخرابیverified؛2rerun،3Save-WPF،4session-WPF،5source+runtimeباقی۔ تازہ5.1Settingsجاری؛[PT-71](WORKBOOK-SETTINGS-CONCURRENCY-REVIEW-URDU.md)،3224suiteUNRUN۔

## ⏳ مکمل تقسیم

یہ نئے منصوبے کے حصے ہیں؛پہلے کےحصہ42 تک کے شواہد تاریخی ہیں۔ مستقل1–36 کاموں کے نمبر نہیں بدلیں گے۔ نیچے ❌ کا مطلب اس **نئے منصوبے کے حصے** پر عمل ابھی شروع نہیں ہوا؛متعلقہ پرانا کوڈ یا پہلے کے ٹیسٹ موجود ہوسکتے ہیں۔14 حصے،14 جوابات یا لازمی کامیابی کی ضمانت نہیں۔

| حصہ / اس نئے منصوبے کی حالت | کام |
|---|---|
|1۔ ✅ متعلقہ کمپائل کی خرابی | DocumentFieldTests کی اصلاح؛مالک کے v0.120.1 Windows run میں Core اور Tests Debug کمپائل ہونے سے تصدیق؛پوری Desktop/Release سند نہیں |
|2۔ ⏳ پوری ٹیسٹ سوئٹ | اجازت مل گئی؛اصل v0.120.1 run میں2941PASS/22FAIL/2963۔22 ناکامیوں سے متعلق fixture اصلاحات؛v0.120.2 کا اصل rerun باقی |
|3۔ ⏳ ریکارڈ بنانے والے فارم | نئےnextسےاجازت؛v0.121.0میںparent/quick-create/Catalog/Unit Saveکےlocaloutcome/ackguards،72نئےcases؛اصلWindows/WPF3035suiteاورPT-63باقی |
|4۔ ⏳ لاگ اِن اور اجازتیں | اجازتملی؛v0.122.0/D-116sessionproof،resetپرnext-actionfreshlogin،currentreaders/inputguards،Counterfull-outputdeny/freshrestore؛22نئےcases/3057suiteUNRUN،[PT-64](AUTHENTICATED-SESSION-REVIEW-URDU.md)/Windowsباقی |
|5۔ ⏳ بیک وقت کام — 5.1Settingsجاری | پچھلےSettings/Backup/Pricing/Hardware/Iconکےبعدv0.129.0WorkbookSettingscoherentsnapshot/pairedCAS/in-Txvalidation/observedApply+localguard؛13نئےcases/3224suiteUNRUN۔remainingStaff/imperativewriters/startupregistryاورتمامWindowsgatesابھی5.1میںباقی؛StockTake/remainingroutesبعدمیںاسی5میں؛[PT-71](WORKBOOK-SETTINGS-CONCURRENCY-REVIEW-URDU.md) |
|6۔ ❌ دوبارہ کوشش اور ری اسٹارٹ | جواب کھونا،بندش،دوسرے صفحے/کمپیوٹر کی retry؛دیرپا شناخت کی تجویز،ضروری منظوری،نفاذ |
|7۔ ❌ مالی اور اسٹاک ملاپ | فروخت/خرید/واپسی/وصولی/ادائیگی/خرچ/گنتی/گلا؛منظورشدہ رعایت اور اصل لاگت کی حد |
|8۔ ❌ رپورٹس اور رازداری | Excel/CSV/PDF/رسید/پرنٹ؛Cost/Profit اجازتیں؛Blackbox/BookCheck میں صرف منظورشدہ رسائی |
|9۔ ❌ تصاویر اور فائل خرابی | باقی gallery actions؛خراب/بڑی فائل،ادھوری نقل،ڈسک بھرنا،لاک اور cleanup failure |
|10۔ ❌ بیک اپ اور بحالی | خفیہ دستی/خودکار بیک اپ،پاس ورڈ،خراب/ادھوری restore،پرانا ڈیٹا محفوظ،نیا کمپیوٹر |
|11۔ ❌ کلاؤڈ | الگ ماحول،رابطہ ٹوٹنا،retry،اضافی ریکارڈ،رسائی کی معلومات اور shop سے علیحدگی |
|12۔ ❌ اصل Windows اور آلات | UI/DPI/ڈائیلاگ/کی بورڈ/اسکینر/پرنٹر/کیمرہ؛startup/login/lock/single instance/shortcuts |
|13۔ ❌ رہنما اور مشترکہ آخری جانچ | اصل بٹنوں سے اردو guide ملانا؛الگ DB/photos/config؛پوری دوبارہ جانچ اور باقی رکاوٹیں |
|14۔ ❌ قابل ارسال ٹیسٹ انسٹالر | ضروری build/tests کے بعد بنانا؛clean install/upgrade/uninstall/data preservation؛پھر فائل دینا |

## ✅ حصہ1 کی متعلقہ تصدیق — v0.120.1 Alder

- ✅ Source میں وجہ ملی: `ExpenseService.List(User? user, int limit = 200)` میں `user` لازمی ہے؛`DocumentFieldTests.TheExpenseList_CarriesTheAnswersLine` اسے `cashOut.List()` سے بلا رہا تھا۔ `User?` کاnullableہونا اسےoptionalنہیںبناتا۔
- ✅ محدود اصلاح: `cashOut.List(TestData.Owner)`؛اصل Single/ExtraLabel/HasExtra assertions برقرار۔ Production service،SQL،اجازتیں یا حساب نہیں بدلے۔
- ✅ نیا محدود source guard اور5 منفی تبدیلیوں کی جانچ؛actor ہٹانا/nullدینا،ضروری assertions ہٹانا اور API میں actor optionalکرنا رد؛اصل bytes بحال۔ یہ عمومی C# type-checker نہیں۔
- ✅ آخری مقامی جانچ:110 static checks؛434 C# فائلوں کی grammar،4810 calls/9871 references میں0 static issues؛5 منفی source تبدیلیاں رد اور اصل bytes بحال۔810 پہلے سے موجود paths برقرار؛version metadata کے سوا198 Core paths اور142 دوسری test paths نہیں بدلیں؛اس test میں صرفOwnerargumentکاایکسطریفرق،تمامassertionsبرقرار۔ یہRoslyn/Windowsbuildنہیں۔
- ⏳ `dotnet build dotnet/Area11.Tests/Area11.Tests.csproj --configuration Release` کی کوشش نہیں چل سکی: `dotnet: command not found`،exit127۔ اس ماحول میں.NETSDKنہیں۔ یہ اس sandbox کی حد تھی؛بعد میں مالک کے اصل Windows log سے Core/Tests Debug compile ثابت ہوگیا،Desktop/Release نہیں۔
- ✅ بعد میں مالک کے اصل Windows/.NET10 run میں2963 ٹیسٹ چلے:2941 کامیاب،22 ناکام،0 چھوڑے گئے،5 منٹ20 سیکنڈ۔ متعلقہ compile خرابی دور؛پوری suite green نہیں۔ حصہ2 کی الگ اجازت بھی مل گئی۔
- ⏳ FullPhase4کھلا؛❌installerbinaryنہیںبنی۔ اصلshopپرtestpostsنہیں؛verifiedbackupاورالگDB/photos/configکیشرطبرقرار۔

## ⏳ حصہ2 — اجازت مل گئی،v0.120.2 کی دوبارہ جانچ باقی

✅ 22 ناکامیوں سے متعلق14 test files میں تیاری کی اصلاح:Settings کے لیے checked Owner،database میں حقیقی صارف،واضح ممنوعہ اجازت،محفوظ شدہ نام،fault لگانے سے پہلے service تیار،اورschemaکےمطابقdraftmarker۔ Productionحساب/permissions/SQLمیںتبدیلینہیں؛2963testidentitiesبرقرار۔

⏳ [اصل run،کیسوار وجہ،اصلاح اور حدود](WINDOWS-V1201-TEST-REPORT-REVIEW.md)۔ یہاں.NETSDKنہیں؛نئےbuildکاکامیابruntimeدعویٰنہیں۔ حصہ2کیاصلrerunتصدیقباقی؛بعدکےnextسےحصہ3کیاجازتملی،اسےاسرکاوٹکےساتھالگشروعکیا؛حصہ4شروعنہیں۔

## ⏳ حصہ3 — اجازت ملی،source guards تیار،اصل جانچ باقی

✅ محدود source میں parent Product/Customer Create+Edit،Counter quickcustomer/Purchase quicksupplier،چارquickcatalogsاورCatalog/UnitsکےSaveپرexistinglocalguard،ackقبلcleanup،checkedselectedIDreloadاورexplicitno-commitretry؛16CoreoperationsobservedTx؛مالیSQL/permissionsپالیسیبرقرار۔

⏳ [جائزہ،حدود اورPT-63](PARENT-RECORD-SAVE-OUTCOME-REVIEW-URDU.md)۔ 72نئےC#cases/3035suiteUNRUN؛یہاںSDKنہیں۔ حصہ2کی22ناکامیوںکےبعدکانیاlogبھیباقی؛نئےsourceپر3035کینئیWindowsrunدرکار۔ Localguarddurableidentityنہیں؛حصہ6کیdesignخودسےشروعنہیںکی۔ ❌حصہ4اورآگےشروعنہیں۔

## ⏳ حصہ4 کی تازہ پیشرفت

صرفحصہ4منظور؛D-116منتخب،source/22نئےcasesلکھے،3057suite/Windowsباقی۔ [⏳ مکملحدود/PT-64](AUTHENTICATED-SESSION-REVIEW-URDU.md)۔ اوپرپرانےحصہ4شروعنہیںکےجملےاسوقتکیحیثیتہیں؛اسنئیمنظوریسےبدلے۔ ❌حصہ5+شروعنہیں۔

## ⏳ حصہ5 جاری — اگلا Next بھی اسی باقی کام کے لیے

v0.123.0صرفپہلاcheckpoint؛نہپوراحصہ5مکملنہصرفWindowsباقی۔ [⏳ covered/remainingroutes/PT-65](EDIT-CONCURRENCY-REVIEW-URDU.md)۔ Settings/sharedDrafts/StockTake/باقیtoggle/update/snapshotراستوںکاجائزہاسیحصےمیں؛حصہ6کیdurableidentitydesignپرنئیمنظوریابھیلازم۔ اوپرپرانےحصہ5شروعنہیںکےجملےتاریخیہیں۔

## ⏳ حصہ5 کا دوسرا checkpoint — باقی کام اب Settings/StockTake

v0.124.0sharedDraftکےمتعلقہstaleeditراستوںکاسورسآگےبڑھا؛[⏳ PT-66/حدود](DRAFT-EDIT-CONCURRENCY-REVIEW-URDU.md)۔ **Settingsmulti-keyforms،StockTakeاورremainingupdates/toggles/snapshotsابھیاسیحصہ5میںباقی**؛پوراحصہ5یاWindowsمکملنہیں۔36new/3118suiteUNRUN؛حصہ6ابھیشروعنہیںکرنا۔
