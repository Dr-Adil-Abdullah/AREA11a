# ⏳ PT-83 — اصل application words کی checked بحالی

**v0.144.0 Primrose · 2026-11-10 · اسی منظورشدہ5.1 کا چھوٹا حصہ۔**
یہ main Settings Save،پوری Settings،Windows/WPF یا حصہ5 کی تکمیل نہیں۔

## ⏳ مسئلہ اور محدود اصلاح

پرانا **Restore original words** loaded form سے reset تو کرتا تھا،مگر null form کے خلاف الگ checked Core entry نہیں تھی۔ کامیابی کے بعد `LoadLabels`/shell callback/status میں خرابی پر اصل commit کا واضح acknowledgement/مستقل guard نہیں تھا؛کسی دوسری کوشش کو صرف `Ok=false` یاصفحہ تازہ ہونے سے محفوظ سمجھنا غلط ہوسکتا تھا۔

- نیا `AppLabelService.ResetChecked(actor, expected)` null form پر mutation سے پہلے definite refusal دیتا ہے؛ورنہ تمام32اصل label keys/defaults موجودہ `SettingsService.SaveEditForm` کو بھیجتا ہے۔ Current writer،ہر key کیpresence/raw-value baseline،atomic settings/audit transaction،observed outcome اورimmutable partial receipt وہی موجودہ Settings قواعد ہیں۔
- پرانے public `Save`/`Reset` compatibility APIs اور32ناموں کاcatalog نہیں بدلا۔ Desktop کا Restore اب پرانا optional-baseline راستہ نہیں بلاتا۔ یہ stable navigation/permission keys نہیں بدلتا،صرف دکھائی دینے والے section names بدلتا ہے؛رسید،پرانا business record اورaudit history نہیں لکھتا۔
- دو-click تصدیق برقرار؛اب پہلی click موجودہ form object کے ساتھ pinned ہے۔ Form بدل جائے،Refresh/label reload ہو،access نہ رہے یاformغائب ہو تو تصدیق دوبارہ چاہیے۔ پہلی click کوئی save نہیں کرتی؛پیغام بتاتا ہے کہ unsaved **لفظوں** کی ترمیم replaced ہوگی،دوسری Settings اس بٹن سے save نہیں ہوں گی۔
- Reset کی ابتدا سے آخر تک synchronous guard؛پہلی click کی notification یاSave کی callback دوبارہ Reset نہیں چلا سکتی۔ Main Save،Load اورicon action بھی اسی in-flight reset میں داخل نہیں ہوتے۔ Reset main Save/icon کے اندر نہیں چلتا۔
- Service کامیابی پر پہلے commit acknowledgement،پھر پوری receipt کے32defaults کی تصدیق،اسی receipt کو `_editForm` بنانا،تصدیق consume کرنا،صرف `LoadLabels`،پرانا shell callback اور **SAVED** status۔ آخر میںreceipt identity،confirmation اورہرkey کیایک exact default row برقرار ہو تبھی guard reset ہوتا ہے۔ کوئی fresh DB/cache reload کامیاب receipt کی جگہ نہیں لیتا۔

## ⏳ نتیجے کے مطابق اگلا قدم

| حالت | اسی صفحے میں برتاؤ | کیا کریں؟ |
|---|---|---|
| پہلی تصدیقی click | صرف confirmation،کوئی write نہیں | ارادہ ہو تو دوبارہ Restore؛unsaved label edits ختم ہوں گی |
| current access/form preflight refusal | کوئی reset شروع نہیں،confirmation ختم | access درست/Settings Refresh،پھر نیا فیصلہ |
| واضح `NoCommitConfirmed` failure | پرانے دکھائے ہوئے label rows رہتے ہیں؛کھلاform invalidate اورconfirmation ختم | پہلے Refresh/review،پھر دوبارہ دو-click تصدیق |
| نامعلوم service outcome/COMMIT یاwriteشروعہونےکےبعدexception | persistent guard بند،form invalidate؛main Save بھی bypass نہیں کرسکتا | اصلlabels/auditتصدیق کریں؛اندھی دوسری Restore نہیں |
| **SAVED** کے بعد receipt/row/shell/status handoff failure | محفوظ کام **SAVED** ہی رہتا ہے،guard بند | labels/auditتصدیق کے بعد Settings reopen؛دوبارہresetنہیں |
| کامیاب handoff | صرفlabelrowsاصلdefaults،دیگرکھلیinputsکوResetنےsave/reloadنہیںکیا | مزیدکارروائیایکنیاعملہے |

`Ok=false` یا error کے الفاظ سے no-commit اخذ نہیں ہوتا۔ Generic Settings کی بعض early permission refusals میں `NoCommitConfirmed=false` رہتا ہے؛checked reset یہ evidence خود نہیں گھڑتا۔ UI کا اپنا preflight service شروع ہونے سے پہلے روک سکتا ہے،لیکن بعد کی race میں conservative verification لازم ہوسکتی ہے۔ Service کے post-commit cache failure کا `SAVED` متن بھی hint میں محفوظ رہتا ہے۔ Notification خود ناکام ہو تو اصل failure متن/acknowledgement ضائع نہیں کیے جاتے۔

الگ **LabelResetHint** عام status کے بعد بھی نتیجہ دکھاتا ہے۔ Unknown/SAVED-handoff guard Refresh،permission regrant،tab/input تبدیلی یاicon receipt سے نہیں کھلتا؛main Save کا entry guard بھی اسے چیک کرتا ہے۔ Refresh موجودہlabelsدکھاسکتا ہے،خود verification یاguard release نہیں۔ Reopen بھی خود verification نہیں،اوریہlocalguardcrash/restart/cross-PC durableidentityنہیں۔ بٹن click ہونے کے باوجودblockedحالت میںwriteنہیںچلتی،صرفverificationپیغامدکھتاہے۔

Reset کیreceiptصرفwrittenlabelkeysکیbaselineبدلتی ہے؛دوسری جگہبدلی ہوئی `store.name` جیسی untouchedkeyکوبے خبری میںنئیbaselineنہیںملتی۔ ایسےپرانےmainformکااگلاcheckedSaveابھیبھیconflictدےگا۔ Failedresetکوکسیزندہshopپرtryنہیںکیا۔

## ✅ محدود جانچ / ⏳ اصل جانچ باقی

- ✅ **134 repository static checks**،C#grammar/member-source resolution سمیت؛نہ .NET compilation نہWPFbindingexecution۔
- ✅ **44 نئی lexical mutations** reject اورoriginalsourcebytesبحال؛تمام **41 source-negative scripts** پاس۔ پچھلے Settings negative probe کاایکold-reset markerنئےcheckeddoorکےمطابقبدلا،جانچکاcaseحذفنہیںکیا۔
- ✅ موجودہgeneralSettingsSQLprobeدوبارہچلا:**37migrations/24Python-owned WAL+DELETE plans**۔ یہپرانےcompare/receipt/auditrollbackplansہیں،نئےC#ResetیاWPFcallbacksکاexecutionنہیں،کوئیfinancialpostنہیں۔
- ✅ Baseline audit:پچھلے935trackedfilesبرقرار؛پرانےC#tests،CoreSettings/Tx،financialservices/data/37migrations،Counter/startup/nativeguideاورAppLabelcatalog/legacyAPIsبرقرار۔ MainSaveCore/focus/staff/startupUIhelperbyte-identical؛صرفresetاوراسکےin-flightentrygates/confirmationwiringبدلے۔
- ⏳ **30نئےC#cases** `AppLabelResetOutcomeTests.cs`:Owner/delegatedSettingswriter،تمامdefaults/receipt/reuse،null/partial/foreignform،staleonekey،DBبمقابلہcache،unrelatedkey/partialreceipt،missingpresence،timestamp/rawwhitespace،currentpermission/actor/readfault،first/lastaudit/settingsfaultrollback،deferredCOMMITunknown،authenticatedsession،concurrentrename/exactreceipt،currentauditname،localguardاورmetadata۔ یہاںSDKنہیں؛یہ30casesاور **3587** کیمتوقعپوریsuite **UNRUN** ہیں۔
- ✅ مالککااصل **v0.138.0 Windows/.NET10 Core+Tests:3418PASS/0FAIL/0skip،6m50s** ثابتنتیجہہے،pendingنہیں؛یہنئے3587یاDesktopUIکیسندنہیں۔
- ⏳ Windows/WPF checklist:دو-click/Space/double-click/reentrantnotification؛پہلیclickکےبعدRefresh/formreplacement/accessrevoke؛دوسراwriter/conflict؛knownrollbackکےبعدtypedrowsبرقرار؛unknownیاSAVEDcallbackfaultکےبعدmainSave/Refresh/regrantسےguardنہکھلے؛receiptکےبعدshellresources/تمام32rows؛دیگرunsavedSettingsinputsبرقرار؛hint،focus/DPIاورnarrowlayout۔ اصلprovider/build/Releaseاورپرانےwarningsبھیالگباقی۔

## ⏳ کیا نہیں بدلا / باقی کام

Core `SettingsService`/transaction helper،مالی حساب/posting،37migrations،پرانےC#tests،Counter،startup/registrycheckedاورlegacy/installer/recoverymethodsاورnativeguideاسحصےمیںنہیںبدلے۔ GeneralmainSettingsSave/focuswriterکےاپنےoutcomes،باقیimperativeSettings/legacyStartup،StockTake،حصہ1تا5کیضروریWindows/provider/WPFتصدیقاورنئیguideUIکیعملیرائےکھلیہیں۔ ❌حصہ6/durableidentity/installerشروعنہیں۔ اگلاالگحصہنئیاجازتسے۔
