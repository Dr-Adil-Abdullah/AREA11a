# ⏳ حصہ4/14 — لاگ اِن،موجودہ اجازت اور Counter — v0.122.0 Larkspur

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

## ✅ منظورشدہ فیصلہ D-116

مالک کے `Next` نے صرف نئے منصوبے کا حصہ4 منظور کیا۔ وضاحت کے بعد مالک نے **A / require_new_login** منتخب کیا: کسی شخص کا password یا PIN بدلے تو اس کا موجودہ لاگ اِن **اگلے عمل سے پہلے** مسترد ہو؛تازہ لاگ اِن لازم۔ محفوظ ریکارڈ برقرار؛غیرمحفوظ فارم دوبارہ بھرنے کا ممکنہ نقصان مالک نے قبول کیا۔ پہلے سے طے شدہ idle اور12گھنٹے کی حد برقرار۔ یہ مالی حساب،Blackbox/BookCheck،SupplierB،backup password A یا durable posting identity کا نیا فیصلہ نہیں۔

## ⏳ Source میں کیا بدلا

- `Database.Sessions`: Desktop کی composition میں ایک بار `Enable()`؛بند کرنے/reset کا API نہیں۔ `ConditionalWeakTable<User,Lease>` میں اسی Database instance اور اصل login سے ملے User object کا غیرserializable ثبوت۔ اسی ID/role کی نقل،دوسرے Database کا object یا CloseApp کی تازہ تصدیق،عام لاگ اِن نہیں بن سکتے۔
- کامیاب Login اور Owner recovery interactive proof دیتے ہیں؛VerifySecret کا الگ2منٹ close-only proof صرف CloseApp کے لیے۔ عام ثبوت کی monotonic12گھنٹے کی حد؛Desktop کی موجودہ idle policy بھی اگلے عمل سے پہلے پڑھی جاتی ہے۔ یہ12گھنٹے کی حد کو بڑھاتا نہیں۔
- ہر `Require` پر account کی active حالت،parsed role اور موجودہ password/PIN hash stamp دوبارہ DB سے پڑھا جاتا ہے؛hash باہر User/UI میں نہیں آتا۔ Login میں verified hash دوبارہ ملائے بغیر proof نہیں بنتا۔ Logout پہلے proof revoke کرتا ہے،audit failure کے باوجود۔ تازہ current readers اصل lease کو Carry کرتے ہیں؛صرف ID سے نیا ثبوت نہیں بنتا۔
- مشترک Owner/ledger/product/snapshot/custom-answer/photo readers میں proof؛PermissionService اور pricing/settings/inspector/shift میں بھی موجودہ ثبوت۔ Staff permission/pricing **target** کو owner کے ثبوت سے پڑھا جاتا ہے؛target کا اپنا login لازم نہیں اوراس کے لیے proof جاری نہیں ہوتا۔ Cash handover recipient بھی target ہے؛اس کی موجودہ active/same-role/Cash شرط برقرار۔
- Desktop کے دونوں RelayCommand اصل بنانے والے login کو capture کرتے ہیں؛اگلے login میں پرانے command نہیں چلتے۔ WPF PreProcessInput keyboard/text/mouse-button/wheel کو routed events/bindings سے پہلے چیک کرتا ہے؛ہر mouse movement پر DB query نہیں۔ Shell activity پر expired idle کو پہلے Touch کرکے زندہ نہیں کیا جاتا۔ verification failure یا credential change پر dispatcher سے موجودہ ordered sign-out؛پرانے shell کا callback نئے login کو sign-out نہیں کرتا۔
- Counter کا3سیکنڈ loaded timer،اگلے input،براہ راست scan/add/recalculate/park/save اور modal continuations پر access checks۔ One-bill basis کی تصدیق اور Extra Discount dialog کے بعد موجودہ اجازت/quote دوبارہ۔
- Counter deny پر **پورا Counter** چھپتا ہے؛catalog/search/customer/doctor/parked lists،receipt preview/last-print ID،حساب اورunsaved extra-answer rows/staged photo صاف۔ authored cart،ادائیگی کی لکھی input اورposting/draft/unknown/SAVED guard state نہیں مٹتی۔ حق واپس آنے پر fresh lists/extra-field definitions،تازہ quote اورreview کا پیغام؛load failure پر جزوی outputs پھر صاف/چھپے رہتے ہیں۔ Refresh access اصل restoration path سے جاتا ہے۔
- Counter customer picker کے لیے current POS **یا** Customers؛supplier picker کے لیے current Products **یا** Purchases **یا** Suppliers،ساتھ اصل login۔ اس سے صرف picker کے لیے غیرمتعلق main tab لازم نہیں بنتا۔ موجودہ quick-create/edit کی الگ service checks برقرار؛Cashier phone correction،Owner/Manager product/catalog اورauthored new-cost rules نہیں بدلے۔

## ⏳ ثبوت اور اس کی حد

`AuthenticatedSessionTests.cs` میں **22 نئے لکھے ہوئے cases**: enabled/legacy فرق،same-object proof،same-file دوسری Database instance،forged owner denial،close-only promotion denial،logout،self password reset،staff PIN reset،دوسرے connection سے reset،deactivate/role changes،grant revoke/regrant،read failure،Owner recovery،owner/target فرق،parent pickers،fresh-reader Carry،custom-field/Counter refusal،saved customer preservation۔ پوری suite کی متوقع گنتی اب **3057**؛اس نئی گنتی کی runtime discovery یہاں نہیں ہوئی۔ پرانے tests حذف یا bypass نہیں کیے؛انہیں نئے authenticated-session ثبوت کا درجہ بھی نہیں دیا۔

محدود مقامی نتائج: **113 static checks،52 نئی lexical mutations rejected/exact bytes restored**۔ تمام20 source-negative scripts بھی دوبارہ پاس۔ پرانےSQLprobesدوبارہ:37migrations،parentSaveکے15plans/30rollback+retryاور1brandpartialcase،حصہ2fixturesprobeپاس؛یہsessionsecurityیاC#runtimeثبوتنہیں۔ یہ C# test execution،authentication penetration test،WPF binding/input/dialog یا provider concurrency سند نہیں۔ `dotnet build ... --configuration Release` کی کوشش `dotnet: command not found` سے نہیں چلی؛SDK موجود نہیں۔ حصہ2 کے22fixture fixes اورحصہ3 کے72 tests کا نیاWindows نتیجہ بھی باقی ہے۔ اصل پرانا v0.120.1 نتیجہ2963total/2941PASS/22FAIL ہی ہے۔

### ⏳ جان بوجھ کر کھلی حدود

- Legacy isolated Core test hosts میں Sessions default disabled ہے تاکہ پرانے service fixtures اپنی الگ business-policy جانچ جاری رکھیں۔ **Desktop لازماً enable کرتا ہے**؛کوئی نیا Core/API host استعمال ہو تو login سے پہلے خود enable کرنا لازم۔ لہٰذا ہر ممکن host کے لیے authentication/security PASS کا دعویٰ نہیں۔ raw SQL یا hostile in-process code کے خلاف sandbox/security boundary نہیں۔
- Password reset DB میں committed ہونے کے بعد **اگلے نئے check/action** پر refusal؛پہلے سے جاری transaction اور دوسرے connection کی تبدیلی کا atomic CAS اس حصے کا دعویٰ نہیں۔ حصہ5 الگ منظوری مانگتا ہے۔ Multi-query coherent snapshots/global/durable identity اورrestart/cross-PC exactly-once نہیں بنائے؛حصہ6 کی design/policy الگ منظوری سے۔
- Refresh/regrant پر unsaved extra answers خود واپس نہیں آتے؛fresh fields دوبارہ بھرنا ہوں گے۔ cart محفوظ رہنے کا مطلب ہر price خود جائز نہیں؛fresh quote/Save پھر موجودہ permission،limits اورallocated actual-cost floor جانچتے ہیں۔
- Loaded timer فوری screenshot recall نہیں؛پہلے کھلے native/modal windows کی اصل teardown،key/text/mouse event order،binding reentry،Closing/expiry اورfresh login reuse ابھی Windows پر ثابت کرنا ہے۔ ان کے بغیر حصہ4 مکمل نہیں۔

## ⏳ PT-64 — لازمی الگ Windows آزمائش

پہلے **verified encrypted backup اور واقعی الگ DB/photos/config**؛صرفexe کی الگfolder کافی نہیں۔ اصل دکان پر test bills/fault injection نہیں۔ ہر قدم کاversion،اصل نتیجہ اورضروری screenshot/log رکھیں؛secrets نہیں۔

1. تازہ `tools\RUN-TESTS.cmd`: **3057 total،0 failed،0 skipped** مطلوب؛پھرDesktop Release الگbuild۔ مختلف count کوپرانا PASS نہ سمجھیں۔
2. اصل Owner/Cashier/Manager login اورOwner recovery؛غلط secret/throttle/CloseApp حسبِ سابق۔ VerifyClose سے عام session نہ بنے؛نئےlogin پر پرانیpage/command نہچلے۔
3. Counter کھلا،cart+cash input+extra answer+staged photo،customer/doctor search،held tray اورlast receipt۔ دوسرے مجازconnection/window سے **اسی user کاPIN/password reset**؛اگلاmouse/key/scanner/Save/TextBox source update پرانیsession سے نہچلے،تازہlogin لازم۔ unsaved loss منظور؛پہلےمحفوظcustomer/bill/draft موجودرہیں۔
4. یہیreset ProductEditor/Catalog/CustomField/Photo modal،retail/basis/Extra confirmation اورClosing prompt کھلےہوئے۔ کوئیزومبیwindow،duplicate save،old-user write یاclosing loop نہہو۔ نئیlogin کےبعدپراناqueuedcallback نئیsession بندنہکرے۔
5. Password بدلے بغیر POS grant revoke: مکملCounter/lookup/extraanswers/lastreceipt/totals hide،cart اورposting/draft guards برقرار؛keyboard/direct input بھیروکے۔ regrant+Refresh میںfresh lists/fields/quote؛عینreloadfailure پرنہجزویoutput نہblindpost۔ unknown/SAVEDguardRefreshسےreleaseنہہو۔
6. CustomersOFF/POSONمیںpickerاورCounterrestore؛CustomersONپرمنظورشدہquickcustomer؛SuppliersOFFاورProducts/PurchasesONمیںsupplierpicker؛Cashier phone correction اورOwner/Managercatalog/newcost جاری۔ تمامparentgrantsOFF پرpickerdeny۔ owner target permission/pricing editorاورCashhandoverrecipient کامکریں؛target login لازم نہہو۔
7. Customfield/Photos permissions revoke/role downgrade/deactivate:کھلےeditorsصاف/deny،تازہregrantload؛staffکوOwner controlsنہملیں۔ actualCostfloor/discountcaps/Extraاورfinancialvisibilityپرانیpolicyکےمطابق۔
8. Idle مدت اور12گھنٹے کی حد:اگلاTouch پہلےexpireکرے؛keyboardسےexpiredsession زندہنہہو۔ Logout/audit-readfailure میںfail-closed؛freshlogin تکnormalactionsنہیں۔

⏳ FullPhase4،حصہ2/3 reruns،C#/Windows security/runtime اورinstaller gates کھلے۔ ❌ Installer نہیں بنا؛حصہ5 شروع نہیں کیا۔
