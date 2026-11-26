# ⏳ PT-82 — Settings سے startup بدلنے کا محدود checked راستہ

**v0.143.0 Tansy · 2026-11-10 · منظورشدہ5.1 جاری۔**
یہ اصل Windows جانچ،registry transaction،پوری Settings/حصہ5 یا installer کی تکمیل نہیں۔

## ⏳ پہلے کیا غلط ہوسکتا تھا؟

پرانا `IAutoStartStore.IsEnabled` صرف ہاں/نہیں بتاتا تھا؛پرانی Run entry کا command،arguments،value kind یا read failure محفوظ نہیں ہوتے تھے۔ DB refusal پر نئے exe path سے `RestoreQuietly` اصل پرانی entry واپس نہیں بنا سکتا تھا۔ `Ok=false` میں unknown/post-commit نتیجہ بھی شامل ہوسکتا ہے،اس لیے ہر failure پر registry واپس پلٹنا درست نہیں۔ Disable کی دبی ہوئی خرابی اور بے شرط fallback duplicate entries بھی چھوڑ سکتے تھے۔

## ⏳ اس checkpoint میں سورس کا نیا راستہ

1. **صرف Settings کی startup تبدیلی** اب `SetModeChecked` سے ہے۔ Owner/current DB/session اور loaded `startup.mode` baseline کا preflight،پھر اصل Settings transaction میں دوبارہ current actor/CAS۔ Main Save اپنی پہلے سے محفوظ دوسری settings اور بعد کے startup نتیجے کو الگ بتاتا ہے؛یہ سب ایک transaction نہیں۔
2. اضافی `ICheckedAutoStartStore` seam میں **Capture** اصل presence،raw value اور kind لیتا ہے؛read failure throw کرتا ہے،absence نہیں بناتا۔ Boolean-only host اس checked route میں mutation سے پہلے رک جاتا ہے۔
3. Immutable `AutoStartEntry` string/ExpandString،binary/None،DWORD/QWORD اورmulti-string کو قسم سمیت سنبھالتا ہے؛mutable arrays input/output پر copy ہوتے ہیں۔ پرانے command کے spaces/arguments/environment variables نہیں بدلے جاتے۔ نیا command وہی quoted trimmed exe path ہے؛خالی/quoted input پر auto/protected action نہیں لگتا۔ Manual removal کو exe path درکار نہیں۔
4. HKLM/HKCU adapters کا نیا مشترکہ helper صرف `Run\Area11` value پڑھتا/بدلتا ہے۔ `DoNotExpandEnvironmentNames`،exact kind،compare-before-write اور readback؛key/ACL نہیں بناتا/بدلتا،read/write faults نہیں چھپاتا۔ ایک process کا lock ہے،cross-process lock یا Windows atomic CAS نہیں۔
5. Auto/protected میں پہلے machine value،پھر user value صاف کی جاتی ہے۔ Access refusal پر user fallback **صرف تب** جب پرانی machine entry واقعی absent تھی اور دوبارہ بھی وہی absence پڑھی گئی۔ پرانی/خالی/نامعلوم machine value کے ساتھ duplicate fallback نہیں۔ Mutation کے بعد access exception آنا بھی خود no-change کا ثبوت نہیں۔ Manual میں دونوں removals کی تصدیق لازم؛Disable failure کامیابی نہیں۔
6. دونوں values target کے مطابق پڑھنے کے بعد checked DB save،پھر exact Settings receipt اور registry کا دوبارہ readback۔ کسی دوسرے Settings writer کی تبدیلی کو final DB check پھر روک سکتا ہے۔ کامیاب receipt ہی Desktop کی اگلی baseline ہے،اندھا reload نہیں۔

## ⏳ ناکامی اور بحالی

| نتیجہ | Windows values کا برتاؤ | Desktop کا اگلا قدم |
|---|---|---|
| Preflight/capture/input refusal | کوئی registry mutation شروع نہیں | Refresh کرکے موجودہ حالت دیکھیں |
| DB شروع ہونے سے پہلے write/cleanup fault | صرف اپنی intended value سے match ہو تو پرانا raw snapshot واپس؛دونوں stores کی readback | دونوں اصل values ثابت ہوں تو معلوم no-commit،ورنہ verification لازم |
| Settings کا واضح `NoCommitConfirmed` | وہی conditional restoration؛دونوں کوششیں الگ ہوتی ہیں،پہلی ناکام ہو تو دوسری نہیں چھوٹتی | صرف DB no-commit **اور** دونوں Windows values کی بحالی پر retry-safe جواب |
| DB outcome نامعلوم،یا post-commit failure | **کوئی compensation نہیں**؛target Windows values خودکار واپس نہیں پلٹتیں | اصل Settings اور دونوں Run entries تصدیق کریں؛تبدیلی نہ دہرائیں |
| DB کامیاب،لیکن بعد کی registry readback خراب | **SAVED** بتایا جاتا ہے؛compensation نہیں | دونوں resources تصدیق کے بعد reopen |
| Desktop acknowledgement کے بعد notification/receipt handoff خراب | startup **SAVED** محفوظ معنی رکھتا ہے؛صفحے کی baseline invalidate | guard بند،verified reopen سے پہلے replay نہیں |

یہاں raw value سے مراد .NET Registry API کی managed value ہے،environment expansion کے بغیر؛malformed native registry bytes/terminators کی byte-for-byte نقل کی ضمانت نہیں۔

Restoration صرف اسی captured value کے data/kind/presence کی ہے،پورے registry key،ACL،timestamps یا executable file کی نہیں۔ کسی concurrent value سے current state مختلف ہو تو اسے overwrite نہیں کیا جاتا۔ Compare/readback کے بعد بھی دوسرا process بدل سکتا ہے؛ABA،power loss،cross-PC یادو الگ resources کی atomicity حل نہیں ہوئی۔ **"Previous values verified" صرف اسی لمحے کا readback ہے،ہمیشہ درست رہنے کا وعدہ نہیں۔**

## ⏳ Settings UI کی حد

Main Save میں synchronous reentry guard؛اصل startup intent callbacks سے پہلے pinned ہے۔ Startup کا retained `PostingAttemptGuard` service سے پہلے،acknowledgement receipt/selection/status callbacks سے پہلے،اورreset صرف مکمل handoff/verdict کے بعد۔ Unknown یاSAVED-handoff failure پر Refresh/regrant main Save guard نہیں کھولتے؛دوسری fields پڑھنا verification نہیں۔ کسی retryable refusal کے بعد بھی `_editForm` ختم ہوتا ہے،اس لیے پہلے explicit Refresh/review چاہیے۔ الگ sticky startup hint عام status بدلنے کے بعد بھی بات دکھاتا ہے۔ Actual WPF reentrancy/focus/notification fault جانچ باقی ہے۔

Main Settings save اس سے **پہلے** اپنی الگ commit کرتا ہے؛اس حصے میں اس کی عمومی outcome handling،focus-guard writer،label reset یا دوسرے imperative writers کی تکمیل کا دعویٰ نہیں۔ Unknown startup کے بعد main Save رک جاتا ہے،مگر ہر غیرمتعلق Settings action کا نیا global guard نہیں بنایا۔

## ⏳ پرانے راستے دانستہ الگ

- پرانا public `SetMode` compatibility/boolean-only hosts اور موجودہ پرانے tests کے لیے برقرار ہے؛نئی Settings UI اسے نہیں بلاتی۔ اس پر exact restoration/unknown outcome کی نئی ضمانت لاگو **نہیں**۔
- `SetMachineAutoStart`،`ResetToManual`،legacy Enable/Disable/IsEnabled اور installer/emergency policy نہیں بدلے۔ ان کی پرانی best-effort/boolean compensation الگ باقی review ہے؛اسے اس checkpoint سے حل شدہ نہ سمجھیں۔
- `ScopeText` checked stores میں read failure/دو entries/malformed value کو absence یایقینی قابلِ استعمال command نہیں کہتا۔ یہ executable کے واقعی چلنے یا Windows sign-in کی کامیابی کا ٹیسٹ نہیں۔

## ✅ محدود جانچ / ⏳ اصل جانچ

- ✅ **133 repository static checks**،C# grammar/member-source resolution سمیت؛یہ .NET build نہیں۔
- ✅ **71 نئی lexical mutations** reject اور original source bytes بحال؛تمام **40 source-negative scripts** پاس۔ Unknown compensation،blind helpers،snapshot copies،fallback اورUI handoff کے contracts بھی شامل؛یہ behavioral execution نہیں۔
- ✅ Baseline audit:پچھلے930trackedfilesبرقرار؛تمامپرانےtests/data/37migrations/مالیservices/nativeguide/Counterبرقرار۔ LegacySetMode،installer/recoveryاورEnable/Disableکےmethodsbyte-identical؛MainSaveکیاصلvalidation/valuesاورfocus/staff/cloudlogicبرقرار۔
- ⏳ **41 نئے C# cases** `StartupRegistryOutcomeTests.cs` میں public service/isolated حقیقی schema اور exact-value fake stores کے ساتھ لکھے:تین modes،access fallback دونوں exception shapes،existing-machine refusal،unreadable stores،missing/stale/foreign actor/input/legacy host،سات raw kinds کی known rollback،manual cleanup fault،deferred COMMIT unknown،post-commit readback،final actor/CAS،concurrent values،partial mutation،restore refusal اورdefensive copies۔ **یہاں .NET SDK نہیں؛نئی3557suite یا41cases نہیں چلے۔** Fake stores اصل registry نہیں۔
- ✅ مالک کا پچھلا **v0.138.0 Windows/.NET10 Core+Tests:3418PASS/0FAIL/0skip،6m50s** ثابت ہے،pending نہیں۔ یہ3557 یااس نئی Desktop تبدیلی کی سند نہیں۔
- ⏳ اصل Windows/WPF/provider checklist:isolated test hive پرسات kinds/presence/raw environment data؛admin/nonadmin،read/write/restore permissions،HKLMexisting/HKCUfallback/duplicate cleanup،concurrent external writes/readback،DB deferred-commit/notification faults،SAVED کے بعد selection/Refresh/regrant/reopen،narrow layout/DPI/focus۔ Live shop/اصلRun entries پر یہاں تجربہ نہیں کیا۔ Desktop/Release/buildwarnings الگ باقی۔

## ⏳ پچھلے تین checkpoints کی مختصر تفصیلی رپورٹ

| checkpoint | شامل محدود سورس | محدود ثابت جانچ | اصل باقی |
|---|---|---|---|
| v0.141.0/PT-80 | CustomField Move کا observed outcome،پوری ترتیب کی receipt اورshared guard |131static،38negative scripts،47mutations،56PythonSQLplans |39نئےC#cases اورWindows/WPF |
| v0.142.0/PT-81 | Counter autoPrint loaded checked save،truthful local choice،preference-only read |132static،39negative scripts،41mutations |24نئےC#cases اورWindows/WPF |
| v0.143.0/PT-82 | Settings کاchecked startup/typed values/conditional restoration/retained UI guard |133static،40negative scripts،71mutations |41نئےC#cases اورWindows/registry/WPF |

یہ104نئےcases پچھلے تین checkpoints میں لکھے گئے؛اس سےپہلے35Addcasesبھیowner-tested3418کےبعدہیں۔ کل متوقع3557 ہے،نہ اسےچلی ہوئیsuiteکہیں نہWindows3418کودوبارہpending۔ باقی imperative Settings/legacy startup routes،StockTake،حصہ1تا5 کی ضروری تصدیق اورنئی guide UI کی عملی رائے کھلی ہیں۔ ❌حصہ6/durable identity/installer شروع نہیں۔ اگلا الگ حصہ نئی اجازت سے۔
