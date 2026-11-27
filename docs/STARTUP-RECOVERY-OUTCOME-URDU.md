# ⏳ PT-86 — Shift-at-startup recovery کا صحیح نتیجہ

**v0.147.0 Cowslip · 2026-11-10 · منظورشدہ5.1 کا محدود recovery checkpoint۔**
LegacyStartup/recoveryکےجائزےمیںاسحصےکانفاذصرفحقیقیShift-recoveryراستےپرہے۔ پراناcompatibility `SetMode` اورinstaller `SetMachineAutoStart` **نہیںبدلے**؛انکومحفوظیاپوراStartupمکملنہیںکہا۔ حصہ6/installerشروعنہیں۔

## ⏳ پہلے مسئلہ کیا تھا؟

`ResetToManual()` پہلےدونوںWindowsentriesکوquietDisableکرتاتھا،پھرmanualmodeالگDBtransactionمیںاور`protection_reset`الگstandaloneauditمیں۔ Disableکیخرابیچھپتی؛standaloneauditخودexceptionدباسکتاتھا۔ چنانچہmanualبغیراuditمحفوظہوسکتاتھااورWindowsکیpartialcleanupکاواضحنتیجہنہیںملتا۔ DBکےunknownنتیجےپربھیreturnedfailureمیںno-commitثبوتیاحالیہserviceguardنہیںتھا۔ Appنےv0.146.0میںصرفhonest“requested/verify”لفظدرستکیےتھے،اصلنتیجہاباسحصےمیںدکھایاہے۔

اصلپالیسیبرقرار: **WindowsentryپھنسجائےتببھیShift-recoverymanualmodeمحفوظکرسکے۔** یہstartuprecoveryہے،FocusGuardOffیالاپتہpassword/PINکابائیپاسنہیں۔ پرانے `ResetToManual_SurvivesAStuckEntry` سمیتتمامپرانےC#testsکیassertionsبرقرارہیں۔ اب“Ok”صرفmanual+auditکیcommitہے،Windowscleanupکیguaranteeنہیں؛warningsساتھلازمپڑھیں۔

## ⏳ نیا محدود راستہ

1. اسیStartupServiceinstanceکا `_recoveryAttempt` guardشروع؛پچھلاunknown/partial/SAVEDhandoffہوتونئیwrite/captureبھیشروعنہیں۔
2. checkedmachineاورuserentriesکےrawvalue/kind/presenceالگcapture؛unreadableکومصنوعیabsenceنہیںبنایا۔ captureناکامہوبھیmanualrecoveryرکتی نہیں؛اسentryکیبعدکیcleanupskipاورwarningہوگی۔
3. **کسیWindowsmutationسےپہلے** `SettingsService.ResetStartupForRecovery()`ایکobservedimmediateDBtransactionمیںصرف `startup.mode="manual"`اوراسیواقعےکا `protection_reset`systemauditمحفوظکرتاہے۔ auditمیںاسیtransactionکیoldvalue/newmanual،systemactorاورWindowscleanupالگہونےکیوضاحتہے۔ auditfailہوتومodeبھیrollback۔
4. اسیtransactionسےصرفmodeکیimmutablepartialSettingsreceipt/baseline؛commitواپسآنےکےبعدصرفmodeکالocalcacheupdate۔ DB/savefailureپرWindowsکوہاتھنہیںلگتا۔ `NoCommitConfirmed`صرفTransactionOutcomeکےثبوتپر؛COMMIT/rollbackunknownکوretry-safeنہیںکہتے۔
5. کامیابیپر **acknowledgementپہلے**،پھرmanualreceiptکیلازمیت/جانچ۔ اسکےبعدmachineاورusercleanupالگالگکوشش؛ایکناکامہوتودوسریرکتی نہیں۔
6. ہرcleanupسےپہلےcurrentDBmodefingerprintکواسیreceiptسےملاتےہیں؛نیاmodewriterیاunreadableDBہوتواسstoreکیcleanupskip۔ checkedhostمیں`Replace(captured, Missing)`+readback؛valueبدلچکیہوتوoverwrite/restoreنہیں۔
7. دونوںcleanupکےبعددونوںentriesکیالگfinalabsenceverificationاورDBreceiptکاآخریمیل۔ مکملcheckedverificationپرguardReady؛کوئیwarning/legacyhost/تبدیلی/unreadableہوتوmanual **SAVED**،warningsاوراسیinstanceکاguardبند۔
8. App `Ok/Error/Warnings` کاحقیقیجوابایکباردکھاتیہے؛partial/unknownپرwarningicon،کوئیautomaticretryنہیں۔ نئےlogsمیںrawexceptionmessage/stackکیبجائےصرفtypenameہے۔ FocusGuardunchangedکیوضاحتبرقرار۔

`ResetStartupForRecovery` fixedinternaloperationہے؛نہgeneralactorlesssetterنہنئیanonymousSettingspermission۔ پہلےسےموجودShiftstartupoperationloginسےپہلےہیچلتاہے۔ `MatchesStartupRecovery`verificationہے،نہfreshform/rebaseنہreceiptکوcurrentکہنےکابہانہ۔ genericSave/CAS/Owner/sessionاورمالیپالیسیبالکلبرقرار۔

## ⏳ نتیجہ کیسے سمجھیں؟

| جواب | محفوظ/باقی حالت | دوبارہ کوشش |
|---|---|---|
| واضحprimaryno-commit | manual+auditدونوںنہیں،Windowsmutationبھیشروعنہیں | خرابیصحیحکرکےنیاreviewedارادہ؛guardReady |
| primaryunknown/exception | DBنتیجہverifyلازم،Windowscleanupشروعنہیں | اسیserviceمیںبند؛اندھیretryنہیں |
| manual+auditSAVED،checkedentriesabsentاورmodeکاexactمیل | اسیوقتسبدستیابchecksدرست | نیاexplicitrequestممکن؛خودکارreplayنہیں |
| manual+auditSAVED،Windowsstuck/unreadable/changed/legacy | manualrecoveryمحفوظ؛Windowsکےساتھابھیایپخودشروعہوسکتیہے | واضحwarnings؛تصدیقکےبعدreopen،اسیinstanceمیںنہیں |
| manual+auditSAVED،بعدکاmodewriter/DBreadfailure | receiptاسپہلیcommitکیہے،ضرورینہیںابcurrentہو | باقیcleanupskipیاجزوی؛تصدیق،نہrestoreنہاندھیretry |
| serviceکامیابcommitکےبعدreceipt/cleanup/handoffexception | **SAVED**حیثیتبرقرار،باقیمرحلہادھورا | اسیinstanceمیںguardبند |

**Legacybooleanstoreکیخاصحد:** compatibilityhostکےلیےاصل `Disable()` best-effortاببھیچلتاہےتاکہپرانامعاہدہنہٹوٹے؛مگربولینکوexactabsence/readbackنہیںکہتے۔ ہمیشہwarning،کوئیEnable/compensationنہیں،guardنہیںکھلتا۔ اصلDesktopWindowsstorecheckedinterfaceدیتاہے۔

## ⏳ اہم حدود — یہ compound atomicity نہیں

- DBmanual+auditاباکٹھےہیں،**Windows+DBایکtransactionنہیں**۔ captureکےبعدیاDBcheckاورnativeReplaceکےبیچدوسراwriterآسکتاہے؛samevalue/ABAیاہرPCکیidentityیہاںحل نہیں۔ مثالکےٹیسٹمیںپہلیcleanupکےcomparecallbackکےدوراننیاmodeآتاہے؛پہلیentryکیcleanupہوسکتیہے،دوسریskipاورآخریwarningہوتی ہے۔ ایسیraceمیں“کچھنہیںبدلا”نہیںکہتے۔
- ManualcommitکےبعدentrycleanupسےپہلےcrashہوتوentryباقیرہسکتیہےاورWindowsپرایپکھلےگی؛manualmodeکیالگحیثیتہے۔ کوئیentryواپسلگانایاپراناexePathسےreconstructنہیںکیا۔
- DBنامعلومہوتوعملیfakefixtureمیںrollbackدکھنےکےباوجودserviceکوCOMMITunknownہیبتاناہے؛PythonSQLیافیککوprovider-successکیسندنہیںبناتے۔
- DB/cacheکیprimaryfailureپرWindowsکوغیرضروریcompensateنہیںکرتے۔ cachefailureبعدcommitمیںerror **SAVED**کےساتھآتاہے؛اصلWindowscleanupشروعنہیںہوئی۔ serviceguardlocalہے؛restart/newinstanceخودverificationنہیں،durableidentityباقیہے۔
- Recoveryaudit `protection_reset`صرفDBmanualcommitکاثبوتہے؛Windowsentryحذفہونےکاپراناauditclaimنہیں۔ partialWindowswarningsابھیdurableper-scopeauditreceiptنہیں،بعدکیseparateverificationضروریہے۔
- `StartupService.Mode`کیlegacycachedmanual/fail-openprojection،legacy `SetMode`کےboolsnapshots/unknowncompensation/“Nothingwaschanged”wordingاور`SetMachineAutoStart`کےپرانےراستےاسحصےمیںنہیںبدلے۔ اگلامحدودreviewانہیںمکملکہنےسےپہلےدرکار؛installerکیاجازتاسسےاخذنہکریں۔
- FocusGuardD-118/A،auth/CloseApp/VerifyClose،registryadapter/PInvoke/taskbar،sessiontelemetryبھیاسحصےمیںنہیںبدلے۔ NativeguideکےصرفstartupموضوعکیrecoveryوضاحتcentralR3sourceسےnative/offlinebookتکیکساںہے؛باقی95موضوعاتاورچھحصےبرقرار۔

## ✅ محدودثبوت / ⏳ اصلجانچباقی

- ✅ **137repositorysource/staticchecks**،C#grammar/memberresolutionسمیت؛.NETbuildنہیں۔
- ✅ **50نئیlexicalmutations** reject،exactsourcebytesبحال؛تمام **44source-negative scripts** پاس۔ پرانےgenericcheckمیںابموجودہrecoveryboundaryاورObserveFailureمانگتےہیں،اصلتحفظنہیںہٹایا۔
- ✅ **37migrations/8نئےPython-ownedextractedSQLplans**،WAL+DELETEمیںmanual+audit/focuspreservation،setting/auditrollbackاورreviewedretry؛zero financialposts۔ Pythontransactionچلاتاہے،C#guard/cache/actualprovider/registryنہیں۔ پرانے **24SettingsSQLplans** بھیپاس۔
- ✅ Nativeguide/offlinebookexactparity،3151static/dataاور309navigationchecks؛Windows/WPFیابراوزرexecutionنہیں۔
- ⏳ **27نئےpublicC#cases** `StartupRecoveryOutcomeTests.cs`:DB+auditقبلregistry،7rawkinds،stuck2/unreadable2/concurrententry2،setting/auditrollback2،deferredCOMMITunknown،entryreappear،latermodewriter،legacy2،Focusunchanged2،preloginoperation،missingmodeupsert،مکملhandoffکےبعدنیاexplicitrequestاورpartialreceiptکیحد۔ **یہاںSDKنہیں؛27نئےcasesاور3670کیمتوقعsuiteUNRUN۔**
- ✅ مالککااصلv0.138.0Windows/.NET10 **3418PASS/0FAIL/0skip،6m50s** ثابت؛pendingنہیں۔ بعدکے252casesیہاںUNRUN،نئیDesktopbuildکیسندنہیں۔
- ⏳ Windows/provider/WPFmatrix:Shiftصرفprotectedstartupمیں؛machine/userregistrypermissions/stuck/partiallywritten/readbackfailure؛کامیابmanualکےبعدcrash/notificationfailure؛DBaudit/COMMIT/cachefaults؛unknownاورpartialپرنوریٹری؛Focusunchanged،auth/login/close/expiry/shutdown؛دیگرPCwriter/DBbusy/LAN،اصلwarningswrap/DPI۔ live-shopیااصلregistryپرکوئیتجربہنہیں۔

## ⏳ باقی

پرانےcompatibilitySetModeاورstartupmodeکیruntimeprojectionکاتمامoutcomesپرالگboundedجائزہ؛telemetryکیatomicity/ignoredresults/shared-runidentity؛StockTake؛حصے1تا5کیضروریWindows/provider/WPFاورguideUIمالککیعملیرائے۔ ❌حصہ6/durableidentity/installerشروعنہیں۔ اگلاالگحصہنئیاجازتسے۔

✅ آخریbaselineaudit:پچھلے5b8db02کےتمام944trackedpathsبرقرار؛پرانےC#tests/financial/data/Tx/migrations/Counter/Focus/SettingsUI/nativeguidecodeنہیںبدلے۔ Settingsمیںصرفدوfixedrecoverymethodsاضافی؛StartupکےlegacySetMode/checkedSetMode/installer/VerifyClose/پرانےhelpersلفظبہلفظاصل۔ Appمیںصرفrecoveryresultblock؛guideکےباقی95موضوعات/R3metadataاورfeatureboardstatusesبرقرار۔ Checklistexplicit2026-11-10پرregenerate؛rulematrixمیںصرفdocumentationtrace،نئیcompletionنہیں۔
