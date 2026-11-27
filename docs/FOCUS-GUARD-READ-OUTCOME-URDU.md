# ⏳ PT-85 — Focus Guard کی موجودہ read،A fallback اور محفوظ switch کا اطلاق

**v0.146.0 Meadowsweet · 2026-11-10 · منظورشدہ5.1 کا محدود checkpoint۔**
مالک نے MCQ میں **A — آخری تصدیق شدہ حالت** منتخب کی؛[D-118](OWNER-DECISIONS.md#decision-d-118)۔ یہ Windows security certification،پوریSettings/5.1 یا حصے1تا5 کی تکمیل نہیں۔

## ⏳ جائزے میں کیا ملا؟

1. `FocusGuardService.Enabled` عامSettingscacheکاboolپڑھتاتھا۔ دوسرےcontext/PCکیwriteخودنظرنہیںآتی؛missing/malformedvalueبھیOffبن سکتی تھی۔ catchمیںOffواپسآتا؛یہمالککیابمنتخبAپالیسیسےمختلفتھا۔
2. screen-lockSaveکامیابہونےکےبعدPropertyChangedاورآخریstatusسے **بعد** chromeeventتھا۔ پہلےcallbackمیںexceptionآئےتوswitchمحفوظ،مگرظاہریاطلاقکاeventرہجاتا۔ پچھلےmainSaveguardنےSAVEDtruthمحفوظکی،اسreviewمیںاطلاقکیترتیبدرستکی۔
3. focuswarningboxauditکامیابیثابتہونےسےپہلے“written”کہتاتھا؛اصلtelemetrybest-effortہے۔ UIکاپیابکوششاورحدبتاتیہے۔
4. Shift-at-startupاصلcodeمیںصرفstartupmode/Windowsentriesکیrecoveryہے؛FocusGuardOffنہیںکرتی۔ پرانا“protection off”پیغاماسفرقکونہیںبتاتاتھااورResetToManualکیواپسآنےوالیfailureکوبھیsuccessلگاسکتاتھا۔ ابپیغامصرف **reset requested** اورverifyکہتاہے؛legacyresetserviceاسحصےمیںنہیںبدلی۔

## ⏳ D-118/A کا محدود نفاذ

`SettingsService.ReadCurrentFocusGuard()` ایکcurrentDBreadہے،UIcacheنہیں۔ `true/false` اورlegacy `1/0`،اردگردwhitespaceکےساتھقبول؛غائبrow،quotedbool،null،2/-1/0.0،malformedیاSQLreadfailure **unknown** ہیں۔ shippeddefaultOffکوموجودہrowکیverificationنہیںبنایا۔ کوئیrepair/write/audit/cacheReloadنہیں۔ مالیvisibilityreader،genericGetBoolاورSave/CASنہیںبدلے۔

`FocusGuardService.ReadStatus()` ایکimmutable `FocusGuardStatus(Enabled, Verified, Notice)` دیتاہے۔ اسیAppServicesinstanceکیآخری **کامیابcurrentread** یادہے؛readfailہونےپرتصدیقشدہOffبھیOffرہتاہے۔ پہلیverificationسےپہلےunknownپرعارضیOn۔ بعدکیصحیحreadOnیاOffاپناتی،warningصافکرتی۔ DBreadstate-lockسےباہرہے؛fallbackdecisionایکlockمیںہے۔ warninglogمسلسلخرابreadsپرایکبار،صحیحreadکےبعدنئیخرابیپرپھر؛rawvalue/exception/credentiallogنہیںکیا۔

| آخری تصدیق | نئیread | عارضی runtime فیصلہ |
|---|---|---|
| کوئی نہیں | unknown | On + واضحwarning |
| On | unknown | On + آخریverifiedOnکیwarning |
| Off | unknown | Off + آخریverifiedOffکیwarning — مالکنےیہAمنتخبکیا |
| کوئی بھی/unknown | معتبرOn | On،warningصاف |
| کوئی بھی/unknown | معتبرOff | Off،warningصاف |

پہلیunknownپرOnہونےکیوجہسےmanualloginکا×بھیauthorizedCloseمانگسکتاہے؛DBخرابہوتوauthenticationبھیمنعہوسکتیہے۔ یہAکاواضحavailabilitytrade-offہے،اسےخاموشOffیاShiftسےنئےFocusbypassمیںنہیںبدلا۔ Windowsadministrator/shutdownکےموجودہراستےاسایپلاککےباہرتھےاورہیں۔

**نہdurableidentityنہمستقلsecuritypolicywrite:** restartیاالگserviceinstanceپچھلیmemoryکاثبوتنہیںلیتا۔ یہصرفscreen-lockswitchکاread-onlyruntimeprojectionہے؛اسےعمومیSettingsreaderنہیںبنایا۔ دستیابقرأتاسوقتکاثبوتہے،بعدکیwriteکیreservationنہیں۔ مصروفDBیاnetworkمیںreadانتظارکرسکتیہے؛UIlatencyکیاصلprovider/LANجانچابھیباقیہے،non-blockingکیضمانتنہیں۔

## ⏳ کون سا راستہ اب اسے استعمال کرتا ہے؟

| راستہ | سورس میں نتیجہ اور حد |
|---|---|
| LoginLoaded/تقریباً5sTimer | currentread،اسیstatusکیvisiblewarning؛FocusGuardسےloginخودfullscreenنہیںبنایا،پراناstartupkioskالگہے |
| Login× | startupProtected **یا** currentFocusAdecisionپرپراناauthorizedCloseprompt؛AllowClose/shutdownبائیپاسبرقرار |
| ShellLoaded/Activated/تقریباً5sTimer | currentreadکےمطابقchromeOn/Off؛اسیreadکیwarningصفحہبدلنےپرنہیںمٹتی |
| Shell× | currentFocusAdecisionاورstartupProtectedکیپرانیاجازت؛deferredClose/AllowCloseبرقرار |
| ShellDeactivated/queuedcheck | دونوںchecksپرcurrentdecision؛اپنےdialogs،cooldownاورone-dialogguardبرقرار |
| Shellminimize | currentdecisionپرپراناinstantrestore؛ابshutdown/AllowCloseسےلڑتانہیں |
| Settingsscreen-lockSave | primary/startupسےالگcheckedcommit؛receiptadoptکرکےchromeevent **property/statuscallbacksسےپہلے**؛وہیmainpersistentguardaftercommitfailureپرreplayروکتا ہے |
| Shell/LoginClosed | نیاtimerStop،Tickunsubscribe؛پراناTaskbarShowاورshellchromeeventunsubscribeبرقرار |
| Appstartup/exit | پچھلےrunکیtelemetryاورTaskbarEnsureVisibleبرقرار؛recoveryکیصرفhonestwordingبدلی |

تقریباً5secondsایکDispatcherTimerintervalہے،سختrealtimeحدنہیں۔ UIمصروفہو،modal/DBانتظارہوتودیرہوسکتیہے۔ immediateeventsاورtimerدونوںcurrentstateپڑھتےہیں؛بعدکاwriterجیتسکتاہے۔ کامیابSaveکیreceiptبتاتیہےکہ **وہSave** کیا تھا،runtimeprojectionنئیcurrentreadکےمطابقہوگا؛اسےپرانےUIchoiceپرمجبورنہیںکیا۔ MainSaveکےunknown/SAVEDguardکوtimer،readیاwarningصافہونانہیںکھولتا۔ unsavedSettingsinputs/baselinesنہیںبدلتے۔

Loginwarningکےلیےscrollablecardرکھا،تاکہwarningآنےپراصلsign-incontrolsکٹنہجائیں۔ Shellwarningعامdismissablefaultbannerاورsessionnoticeسےالگہے۔ Warningمیںکوئیshopsecretیاrawdatabaseerrorنہیں۔ Nativeguideکےصرفاسیstartup/focusموضوعکیوضاحتcentralR3sourceسےpackageddata/offlinebookتکیکساںاپڈیٹہے؛چھحصے/96موضوعاتاورباقیguidecodeبرقرار۔ R3موادکیeditionہے،یہموضوعv0.146.0کیاصلاحبتاتاہے؛نئیeditionیامکملreviewکادعویٰنہیں۔

## ⏳ کیا نہیں بدلا،کیا ابھی خطرہ/حد ہے؟

- Owner-onlySave/currentauthorization/checkedform/CASاورseparatecommitsکیپالیسیبرقرار؛fallbackکسیشخصکونئیSave/Closeاجازتنہیںدیتا۔ CloseUnlockکیVerifySecret/CloseApppermission/auditاورthrottleاصلراستہہے؛اسےنیاauthenticationdesignنہیںبنایا۔
- `TaskbarHider` اببھینativebest-effort/referencecountہے؛ShowWindowکاreturnvisibilityکیسندنہیں،Explorerrestart/secondarymonitors/third-partytaskbar/حقوقکایہاںruntimeproofنہیں۔ اسحصےمیںPInvokehelperنہیںبدلا۔ TaskManager،Windowsadmin،shutdown،powerlossیاOSshortcutsکےخلافیہWindowskiosksecurityنہیں۔
- `RecordAway/CheckPreviousRun/Heartbeat/MarkCleanExit` کےاصلbodiesنہیںبدلے۔ silentwritesکےServiceResultنظراندازہونا،متعددsessionstampwritesکیغیرatomicحیثیت،auditfailureاورsharedDBکےper-run/per-PCstampsالگtelemetryreviewمیںکھلےہیں۔ ابUI“ہرescapeضرورلکھاگیا”نہیںکہتی۔ Killروکنایاہرkillrecordکیguaranteeنہیں۔
- LegacyStartupSetMode/ResetToManual/installer/recoveryکیregistrycompensationاورunknownoutcomesپچھلیPT-82کیطرحباقی؛wordingfixکوانservicebugsکاحلنہکہیں۔ ShiftبغیرnewFocusOffwriteکےاسیپرانےطریقےسےچلتاہے۔
- Guide/nativeDocsکیاصلWindowslayout/focus/double-clickپسند،StockTakeاورحصے1تا5کیprovider/WPFجانچباقی۔ ❌حصہ6/durableidentity/installerشروعنہیں۔ اگلاالگحصہنئیاجازتسے۔

## ✅ محدودثبوت / ⏳ باقیاصلجانچ

- ✅ **136repositorysource/staticchecks**؛C#grammar/memberreferencecheckbuildنہیں۔
- ✅ **60نئیlexicalmutations** reject،اصلbytesبحال؛تمام **43source-negative scripts** پاس۔
- ✅ موجودہgeneralSettingsSQLprobeکے **37migrations/24Python-ownedWAL+DELETEplans** دوبارہپاس؛یہنئےC#ReadStatus،Windowsیاactualproviderfailureکونہیںچلاتا۔
- ✅ centralnativeguide/offlinebookexactparity،3150guide/staticdataاور309navigationchecks؛browserیاWPFexecutionنہیں۔
- ⏳ **31نئےpublicC#cases**:strictvalid6،invalid8،retain2،missingfirst1/missinglater2،initialrecovery2/oppositerecovery2،otherwriter1،openformCAS1،instancelocal1،realSQLreadfailure2/initialSQLfailure1،bookkeeping1،Ownerboundary1۔ **یہاں.NETSDKنہیں؛31اورمتوقع3643کیپوریsuiteUNRUN۔**
- ✅ مالککااصل **v0.138.0Windows/.NET10Core+Tests3418PASS/0FAIL/0skip،6m50s** ثابت؛اسےpendingنہیںکہا۔ بعدکے225نئےcasesUNRUN،یہنئےDesktopکاثبوتنہیں۔
- ⏳ Windowsعملیmatrix:validOn/Offبمقابلہmissing/malformed/readIOfailure؛پہلیunknownOn،knownOffretention/recovery؛دوسراPCcurrentwrite،busyDB/LAN/UIlatency؛warningwrap/scroll/tab/DPI؛singlemodal/cooldown/owneddialog/Win+Down؛savedfocusکےفوراًبعدproperty/statusexception؛nativechromeexceptionکےبعدSAVEDguard؛signout/expiry/shutdown/Closedtimercleanup؛ShiftمیںFocusunchanged،manual/auto/protectedاورlogin×؛actualtaskbar/Explorer/multimonitorاورtelemetryfailures۔ کوئیlive-shopیااصلregistryتجربہنہیںکیا۔

## گزشتہ تین محدود checkpoints کی مطلوبہ تفصیلی رپورٹ

| checkpoint | سورس پیشرفت | محدود نئی جانچ | کھلاکام |
|---|---|---|---|
| v0.144.0/PT-83 |32labelsکاcheckedreset،دوclickpinnedform،ackقبلcallbacks،unknown/SAVEDپرretainedguard |30C#casesUNRUN؛44lexicalmutations |actualWPFcallbacks/providerاورWindows |
| v0.145.0/PT-84 |MainSaveoutcomeguard،credential/confirmationconsume،primarySAVEDبمقابلہالگstartup/focuspartialstages |25C#casesUNRUN؛53lexicalmutations |actualsequencefaults،Windows/nativeapplication |
| v0.146.0/PT-85 |D-118/Acurrentreadfallback+warning،windowfreshness،chromeeventقبلnotifications،honestlimits |31C#casesUNRUN؛60lexicalmutations |اوپرکیWindows/native/telemetry/legacyStartupحدود |

انتینمیں **86نئےC#casesلکھے**،نہیںچلے؛3557سے3587،پھر3612اور3643expectedcountsہیں،actualPASS3418اپنیپرانیمصدقہbuildکاہے۔ تینوcheckpointملاکرنہcompoundatomicSaveنہdurableexactly-onceنہتمامSettingsمکمل۔ اگلےباقیسورسکاممیںlegacyStartup/recoveryoutcomes،telemetryکےالگboundedمسائلپھرStockTake؛اصلWindows/provider/WPFکےبغیرحصے1تا5بندنہیں۔

✅ آخریbaselineaudit:پچھلےa64352bکےتمام941trackedpathsبرقرار؛پرانےC#tests،financial/data/Tx/migrations/Counter/Startup/CloseUnlock/TaskbarHiderنہیںبدلے۔ SettingsServiceمیںصرفreadmethodاضافی،Focusکیtelemetrybodiesلفظبہلفظاصل،MainSaveمیںصرفchromeeventکیجگہ؛guideکےدوسرے95موضوعات/R3metadataاورfeatureboardکےتمامstatusesبرقرار۔ Checklistاسیboardسے2026-11-10کیexplicitتاریخ/versionپرregenerate؛rulematrixمیںصرفdocumentationtrace،نئیcompletionنہیں۔ D-01تاD-118کی118numberedindexrowsموجود۔
