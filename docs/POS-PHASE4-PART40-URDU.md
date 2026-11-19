# ⏳ فیز4 — حصہ40: PR کی موجودہ اجازت،مکمل receipt اور honest read verdict

**⏳ v0.118.0 Sedge · 2026-11-10 · PT-60 · مستقل5–7،17،21،23 — source موجود،اصل Windows ثبوت باقی۔**

✅ علامتوں کا اصول:✅صرف متعلقہ کام100%مکمل؛⏳کچھ باقی؛❌بالکل شروع نہیں۔ Source/probe کی کامیابی پوری feature کی runtime/security سند نہیں۔

## ⏳ روزمرہ مثال اور اس حصے کی تبدیلی

- ⏳ **5/6/17 — پرانا User کافی نہیں:** Manager کے پاس PR کھلا تھا؛Owner نے account بند،role تبدیل یا Returns اجازت واپس کر دی۔ اب پرانی ID/code سے Page،Pending/History،Get،Lines،bill search یا receipt نہیں ملتی۔ Create/Attach/Cancel کے transaction کے اندر بھی current stored account دوبارہ check؛Attach/Cancel صرف current Returns والا Owner/Manager۔
- ⏳ **5/23 — audit:** جائز writer کا نام/ID موجودہ stored account سے؛supplied پرانا یا بناوٹی display name audit کا نام نہیں بنتا۔ یہ supplied ID کو authenticated session token نہیں بناتا؛same-role ID impersonation/session/CAS ابھی الگ باقی ہیں۔
- ⏳ **7 — کھلے page کی صفائی:** اگلے action یا nominal3second Loaded timer پر access denial/read-check failure ہو تو PR rows،selection،lookup،product results،tray،refund/customer/phone/notes اورdecision inputs صاف؛editing بند۔ یہ unsaved typed work ضائع کرتا ہے،کوئی مالی entry خود نہیں بناتا۔ Stable allowed tick عام typed inputs نہیں مٹاتا۔ Regrant پر پرانا data واپس نہیں؛تازہ Refresh/selection ضروری۔ View بند ہو تو timer رکنا چاہیے؛اصل WPF جانچ باقی۔
- ⏳ **17 — failed read،empty نہیں:** Page کا اب explicit success/failure ہے۔ Failed load پر lists/selection/decision fields صاف،caption کہتا ہے **not loaded**؛“Nothing waiting” یاempty hint صرف کامیاب load پر۔ Current Returns access برقرار ہو مگر page-data read ناکام ہو تو decisions/reprint نہیں؛نیا PR form مالی طور پر خود پوسٹ نہیں ہوتا۔
- ⏳ **21 — پوری receipt:** Build میں current Returns actor لازم؛authorized header اورتمام lines ایک read unit سے۔ Line query ناکام،missing PR یاincomplete lines ہوں تو failure؛صرف header والی کامیاب slip نہیں۔ Receipt Compose/رقم/layout نہیں بدلے۔ پہلے سے بنا document،printed paper یاexported file واپس نہیں لی گئی۔
- ⏳ **17/25 — P39 guards برقرار:** Detached پرانی row پر Attach/Cancel/Reprint نہیں؛decision کے لیے successful list load اورcurrent capability۔ Save ہوچکا مگر list دوبارہ نہ پڑھی جائے تو **SAVED** warning اورunfinished-reset block؛permission regrant سے unknown posting guard خود نہیں کھلتا۔

## ✅ پرانی مالی policy برقرار

- ✅ PR financial SQL،private Save/reconciliation/refund allocation functions اورreceipt Compose برقرار؛Link میں دوسراstock/payment نہیں،Cancel میں نیاcash-in نہیں۔ P39 fault tests/guards برقرار؛کوئی migration یاhistorical row rewrite نہیں۔
- ✅ جائز current Cashier/Manager/Owner Returns reader کے reads برقرار؛عام reader Attach/Cancel نہیں کر سکتا۔ Core bill lookup Returns reader کا راستہ ہے؛UI کاdecision lookup پہلے کی طرحOwner/Manager کےدائرےمیں۔ نئیSales/Cost/Profit شرط نہیں لگائی۔
- ✅ Pricing/Extra/ReturnB/SupplierB،Blackbox/BookCheck،backup/cloud اورautomatic-draft policy نہیں بدلی۔ دو پرانی PR test files میں صرف explicit Owner argument جوڑا؛assertions/financial expectations نہیں بدلیں۔

## ✅ یہاں کی جانچ / ⏳ اصل execution

- ✅ نیا disposable SQLite probe:37 migrations،35 Python current-account/grant denial صورتیں،unchanged dumps،regrant،3جائزreaders،اصلPRpage/header/lines/search SQL،bound IDs،stored names اورline-read fault۔ Authority/routing Python کے ہیں؛C# service نہیں چلی۔
- ✅ نیا source-negative probe:27lexicalmutationsرد،اصلbytesبحال۔ Source gates/order کی جانچ ہے،C#/WPF semantics کا بدل نہیں۔
- ⏳ **99 نئے C# cases؛پوری suite2888UNRUN:**49readerdenials،21writerdenials،21validreads،3storedaudit،2reader-vs-decider،3failure/compatibilitycases۔ یہاں .NET SDK/Windows نہیں؛compile/run اورfixture assumptions باقی۔
- ✅ آخری مقامیrun:107staticchecks،431C#grammarfiles،4783calls/9828refsمیں0staticissues؛تمام14source-negativescriptsکے405mutationsرد/restore؛تمام31SQLiteprobesPASS۔ Backupreference6roundtrips/24negatives،rulesmatrix/diffcheckPASS؛37migrations/financialhelpers/Composeاور36guideanchorsبرقرار۔
- ⏳ مکمل حدود [trace](POS-PHASE4-PROVISIONAL-CURRENT-REVIEW.md) میں؛static/SQLite کو Windows یاsecurity certification نہ سمجھیں۔

## ⏳ PT-60 — واقعی الگ test ماحول میں

**⏳ پہلے verified encrypted backup اورواقعی الگ DB/photos/config؛صرفexeکاالگfolderکافی نہیں۔ اصل دکان پرsyntheticPR/refund/faultنہیں۔**

1. ⏳ **2/4:** v0.118.0 پر `tools\RUN-TESTS.cmd`؛2888suite کےbuild/TRX/resultlogs،پھرfailuresکیfix/retest۔ پرانا green نتیجہ نیاPASSنہیں۔
2. ⏳ **5/6/17:** PR کھول کر accountinactive،storedrolechange،Returns revoke،missing actor/null/suppliedinactive cases؛Page/Get/Lines/Search/Receipt اورCreate/Link/Cancel سب مناسب denial،کوئیfinancial/auditmutationنہیں۔ validOwnerبھیstoredinactiveہوتوبند۔
3. ⏳ **17:** جائزcurrentCashier/Manager/OwnerReturnsreadsبرقرار؛CashierکوAttach/Cancelنہیں۔ جائزManagerکامکملعملچلےاورauditstorednameسے؛suppliedforgednameنہیں۔ دوسریpermissionsکیبلاوجہشرطنہآئے۔
4. ⏳ **7/30:** دونوں lists،selection،lookup،product search،tray/refund/name/phone/notes بھر کر revoke؛nextaction/nominaltick پر سبصاف/editingبند۔ Stableallowedtickinputsبرقرار؛regrantپرexplicitRefresh،پرانےinputsخودنہیں۔ Navigateawayپرتimerstop؛DPI/binding/focus اورscreenshots اصلWindowsسے۔
5. ⏳ **17/21:** Page query faultکوemptyنہکہے؛headerqueryچلےمگرlinequeryfailہوتوreceiptBuildfailure،header-onlysuccessfulslipنہیں۔ Missing/incomplete PR بھیرد؛manualاورautosaveprintدونوںcurrentactorسے۔ پہلےسےprinted/exportedcopyواپسآنےکادعوینہیں۔
6. ⏳ **17/25:** successfulSave/Attach/Cancelکےبعدlistreloadfail→SAVED،نہunsavedfailureنہدوسراrefund/action۔ P39unknown/unfinishedguardaccessregrantسےresetنہیں۔ Detachedentry/lookupobjectپرactionنہیں؛تازہloadedrowہیمنتخبہو۔
7. ⏳ **23:** اصلprovider/parallel/CAS/sessiontestsالگ؛`Database.Read`localgateہے،externalconnectionsکےلیےmulti-querysnapshotیاinstantpermissionrecallکیضمانت نہیں۔ WriteTxکیactualexecutionبھیrunnerپرثابتکریں۔

## ⏳ باقی دروازے

- ⏳ مکمل [مستقل1–36](NUMBERED-WORK-STATUS-URDU.md) اپڈیٹ۔ اس حصے نےPRcurrent-boundarysourceبڑھایا؛remainingcallers/fullPOS/session/CAS اورحقیقیruntimeباقی۔
- ⏳ اگلاboundedreview:باقیrecovery/filefaultpaths اور durable identity/providerambiguity؛نئیpolicyہو تو پہلے مثال/منظوری۔ Legacy list/null APIsfailclosedہیں مگرerrorshapeمحدود؛productionUIابexplicitPage/ReadReceiptاستعمالکرتاہے۔
- ⏳26–29backup/restore/newPC/cloud،2/4/30–35Windows/build/results/hardware/isolationباقی۔ ❌36sendabletestinstallerbinaryنہیںبنی؛⏳FullPhase4OPEN۔
- ⏳ ہرportiondocs+اسیactivebranchcommit/push+الگactualremoteverification۔36categories≠36Nexts؛3–6roundsمشروطاندازہ،deadline/upperboundنہیں۔
