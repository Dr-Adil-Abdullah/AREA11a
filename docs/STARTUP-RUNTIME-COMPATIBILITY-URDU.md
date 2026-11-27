# ⏳ PT-87 — Startup runtime C اور compatibility SetMode کا نتیجہ

**v0.148.0 Harebell · 2026-11-10 · اسی منظورشدہ5.1 کا محدود checkpoint۔**
مالکنےMCQسے **C — read خراب ہو تو عارضی manual + warning** منتخبکیا؛[D-119](OWNER-DECISIONS.md#decision-d-119)۔ FocusGuardکاD-118/Aالگبرقرار۔ پوریSettings/Windows/حصے1تا5مکملنہیں؛6/installerشروعنہیں۔

## ⏳ Runtime میں کیا بدلا؟

پرانا `Mode` sharedSettingscacheپڑھتاتھا،unknownکوخاموشmanualبناتاتھا۔ اب `SettingsService.ReadCurrentStartupMode()`currentDBمیںصرفstartup.modeپڑھتاہے۔ JSONstringکےصرف `manual/auto/protected` معتبر؛missing،null،غلطtype،uppercase/اندرونیspacesوالاغلطmodeیاSQL/IOfailureunknownہیں۔ پرانیcacheقابلِتصدیقreadنہیں۔ کوئیcacheReload،rowrepair،Windowswriteیاunsavedformrebaseنہیں۔

`StartupService.ReadStatus()`اسیreadکاimmutable `StartupModeStatus(Mode, Verified, Notice)`دیتاہے۔ `Mode/IsProtected/WantsAutoStart`اباسیcurrentprojectionسےبنتےہیں؛`ModeLabel`unknownپر“unverified/manualtemporarily”کہتاہے۔ logصرفwarningtransitionپر،rawmodeیاrawexceptionنہیں۔

| DBread | runtimeرویہ |
|---|---|
| معتبرmanual | معمولکادستیlogin،startup-modeکیprotectedcloseOff |
| معتبرauto | kiosklogin،startup-modeکیprotectedcloseOff |
| معتبرprotected | kioskloginاورstartup-modeکیprotectedcloseOn |
| پہلییابعدکیunknownread | **عارضیmanual،Verified=falseاورwarning**؛آخریprotectedبھیretainنہیں |
| بعدکیصحیحread | اصلmode،warningصاف |

**یہمالککامنتخبsecurity/availabilitytrade-offہے:** readخرابہوتوstartup-modeکیcloseپابندیOffہوتیہے۔ اسسےمحفوظmodemanualنہیںہوتا؛Windowsentryاببھیایپخودکھولسکتیہے۔ **الگFocusGuard** D-118/AکےمطابقآخریverifiedOn/OffیابتدائیunknownOnرکھتاہے،اسلیےstartupfallbackmanualکےباوجودFocusGuardسے×پرprompt/شیللاکآسکتاہے۔ یہتضادنہیں،دوآزادpoliciesہیں۔

## ⏳ Login/shell/Shift کا اطلاق

- دونوںwindowsمیںالگ **Startup-mode verification warning**؛اسیstatusکیNoticeاورVerified،Focus/session/faultnoticeسےالگ۔ startupwarningنہعامstatusسےoverwriteہوتیہےنہکسیSaveguardکوریلیزکرتیہے۔
- Loginconstructorاورموجودہتقریباً5swindowtimercurrentstatusپڑھتےہیں۔ auto/protectedپرkiosk،manual/unknownپرصرف **اسیloginکیownedkioskحالت** واپسnormal؛Topmostہٹتا،borderواپس،اسکاtaskbarleaseہیreleaseہوتاہے۔ repeatedtickHideکونہیںبڑھاتا؛Closedپرصرفاپناleaseواپس۔ پہلےmanualLoginبھیunconditionalShowکرتاتھا؛ابکسیاورwindowکاحصہکم نہیںکرتا۔
- Loginاورshell×پرcurrentstatus+warning؛اسکےبعدالگFocusGuarddecisionاورپراناauthorizedCloseprompt۔ AllowClose/shutdown/deferredCloseبرقرار۔ LoginFocusrefreshمیںصرفstartuprefreshاضافی،Focuspolicyبودی/مختلفنہیںکی۔
- Shellload/activation/اسیwindowtimerپرstartupwarningتازہ؛shellکیmaximize/taskbarپالیسیابھیبھیالگFocusGuardکیہے۔ startupCاسکولاکآفکرنےکانیااختیارنہیں۔
- Shiftrecoveryصرف **Verified protected** modeپرچلتیہے۔ unknownmanualfallbackکومستقلmanualلکھنےکاارادہنہیںبناتے؛CخودrecoveryیاDBrepairنہیں۔ v0.147.0کیatomicmanual+audit/exactreceipt/cleanupwarnings/guardنہیںبدلے۔
- timerintervalسختrealtimeحدنہیں؛DBbusy/LANاورUImodal/انتظارمیںدیرہوسکتیہے۔ readsسynchronousہیں،non-blockingکیضمانتنہیں۔ اصلWindowslayout/Topmost/focus/DPI/taskbarjanchnہیںہوئی۔

## ⏳ Compatibility SetMode میں کیا بدلا؟

1. اصلOwner/session/currentwriter،validmodeاورexePathکیpreflight؛پراناformدیاتووہیbaseline،خاموشreloadنہیں۔ imperativecallمیںformنہہوتوصرفmodeکاauthorizedform **Windowsworkسےپہلے**لوڈکرتاہے۔
2. currentbaselinecheckکےبعدretained `_compatibilityAttempt`guard۔ دونوںstorescheckedہوںتوپرانےimperativeEnable/Disableکےبجائےوہی `SetModeChecked`؛loadedCAS،rawkind/value/presence،conditionalrestoreصرفknownDBrollbackاورunknownپرنوریسٹوربرقرار۔
3. Boolean-onlyhostکےلیےالگpresence-onlybridge:initialreadsکیexceptionکومصنوعیabsenceنہیںبنایا؛ordinarymanual/cleanupمیںDisableکیexceptionابquietsuccessنہیں۔ machineEnableکیaccessrefusalپرuserfallbackتبھیجبmachineپہلےبھیغائبہو **اوراببھیhostغائببتائے**۔ usercleanuprefusalmachine-enablefallbackنہیںبنتی۔
4. LegacybridgeمیںDBسےپہلےاوربعدpresencepostconditions؛SaveوہیcheckedSettingsformکےساتھ۔ پرانےboolsnapshotsاورنئےexePathسے **کبھیguessedrestoreنہیں**۔ registryattemptکےبعدDBknownrollbackبھیaggregateoperationکونوریٹریثبوتنہیںدیتا؛Windowschangesرہسکتیہیں،guardبنداورverifyپیغام۔
5. `NoCommitConfirmed=true`صرفbridgeمیںWindowsmutationشروعہی نہہونےیاcheckedpathکےواقعیverifiedrollbackپر۔ unknownCOMMIT/partialWindows/post-savepresencefailureپرfalse،بعضصورتوںمیں **SAVED** DBحیثیتواضح۔
6. کامیابیپرack،پھرلازمfreshmatchingDBreceipt،تبguardReady؛exceptionمیںacknowledgedSAVEDحیثیتبرقرار۔ دوبارہunknown/partialcallاسیserviceمیںرکتیہے؛modeکیread،Refresh،صلاحیتبدلنےیابعدکیصحیحreadسےguardنہیںکھلتا۔ یہAPI-localguardہے،cross-API/cross-PC/restartidentityنہیں۔

### “Nothing was changed” کا درست محدود مطلب

پرانےدوC#testsاسsubstringکودیکھتےہیں؛انکیاصلassertionsنہیںبدلیں۔ اباصلجوابہے:

> Nothing was changed **in startup settings. Windows entries may have changed**; verify both before reopening or retrying.

یہصرفاسbranchمیںہےجہاںDBsaveشروعہی نہیںہوئی۔ Windowsaccessrefusalpartialmutationکےبعدبھیآسکتیہے،اسلیےaggregate `NoCommitConfirmed=false` اورguardبند۔ نئےtestمیںuserentryواقعیبدلکرexceptionآتیہے؛DBmanualرہتی،Windowsentryموجود،نتیجہunknownاورrepeatبند۔ پرانےمختصرsubstringکومکملWindowsrollbackکیسندنہیںکہا۔

## ⏳ Legacy bridge کی نہ مٹنے والی حد

`IAutoStartStore`صرفboolpresenceاورvoidEnable/Disableدیتاہے۔ exactcommand/kind/ACL،hostکےاندرswallowedreadfailure،کسیاورprocessکیsame-value/ABAwriteکوثابتنہیںکرسکتا۔ legacyhostخودغلطabsenceبتائےتونیاbridgeاسکودریافتنہیںکرسکتا،duplicatefallbackکیمکملضمانتنہیں۔ عامsuccessمیںDBreceiptexactہے،**Windowsreceiptنہیں**؛machine-successکیپرانیمثالوںمیںWarningsخالیبرقرارہیں،اسےکاملWindowscertificationنہکہیں۔ `ScopeText`legacyhostکےreportsکوpresence-onlyکہتااورthrownreadfailureپرverifyمانگتاہے۔

اصلDesktopRegistrystoresدونوںcheckedinterfaceدیتےہیں؛compatwrapperانہیںcheckedrouteپرلےجاتاہے،SettingsUIپہلےہیاسcheckedrouteپرہے۔ installer `SetMachineAutoStart`،اسکےboolsnapshot/restorehelpersاورCLIنہیںبدلے؛انکےپرانےunknownoutcomeمسائلحلنہیںکہے۔ installerشروعکرنےکیاجازتنہیں۔

## ✅ محدودثبوت / ⏳ حقیقیجانچباقی

- ✅ **138repositorysource/staticchecks**،grammar/memberresolutionسمیت؛.NETbuildنہیں۔
- ✅ **65نئیlexicalmutations** reject،exactbytesبحال؛تمام **45source-negative scripts** پاس۔ پرانےSettingsorderinggateکوbaseline→guard→checked/legacydispatchاورlegacyread→mutation→checkedSaveکیموجودہترتیبپرمنتقلکیا؛پرانےSettingsnegativeprobeکےوہی29casesمیںbaseline/checkedSaveکےموجودہtokens،کمcasesنہیں؛Focussourcegateمیںصرفcurrentclosecallکاسمیٹکمتبادل،اسکے60mutationcasesبرقرار۔
- ✅ Nativeguide/offlinebookexactsourceparity/3152staticdata/309navigation؛صرفstartupموضوعمیںD-119/Cاورboundedcompatibilityوضاحت،باقی95موضوعات/R3edition/sixrootsبرقرار۔ یہbrowserیاWPFexecutionنہیں۔
- ✅ پرانے **37migrations/24SettingsSQLplansاور8recoverySQLplans** دوبارہPythonWAL+DELETEپرپاس؛یہنئیruntimepolicy/bridge/C#کوexecuteنہیںکرتے۔ اسحصےمیںنیاPythonpolicy-simulationcountایجادنہیںکیا۔
- ⏳ **47نئےC#cases:**23runtime(valid3/invalid8/previousmodes3/recovery3/currentwriter+staleform/missingrow/SQLfailure2/Focusindependence2)،24compatibility(legacy3/checked3/invalid3/partialrefusals/cleanup/fallback/non-swallowedmanual2/initialread2/knownrollback/unknownCOMMIT/staleform/ownerrevocation/postcommitfailure/checkedrollback+unknown)۔ **یہاں.NETSDKنہیں؛یہ47اور3717کیمتوقعپوریsuiteUNRUN۔**
- ✅ مالککااصلv0.138.0Windows/.NET10 **3418PASS/0FAIL/0skip،6m50s** ثابت،pendingنہیں۔ بعدکے299casesUNRUN؛نئےDesktopکاثبوتنہیں۔
- ⏳ اصلWindows/provider/WPFmatrix:protected→DBfailureپرCmanualwarning/kioskrelease؛FocusOnاورFocusOffکےساتھclose؛بعدکیصحیحreadپرواپسی؛invalidmode/missingrow/firstreadfailure؛Shiftunknownپرنوسسٹمwrite؛ownedtaskbarlease/timercleanup/Topmost/modal/DPI؛bridgepartialNativewrite/access/readback،unknownCOMMIT/cachefaults،SAVEDhandoff،staleOwner/loadedform؛realLAN/DBbusy/crossPCاورwarningsکیخواندگی۔ کوئیlive-shop/اصلregistryتجربہنہیں۔

## ⏳ باقی اور اگلا الگ کام

Windowsnative/registry/runtimepolicyکیعملیتصدیق؛legacyboolinterfaceکیexactnessکیحداورinstallerکےپرانےراستے؛sessiontelemetryکیignoredwrite-results/غیرatomicstamps/shared-runحیثیتکاالگboundedreview؛StockTake؛guideUIکےdouble-click/layout/focusاورمالککیپسند؛حصے1تا5کیضروریprovider/WPF/buildجانچ۔ ❌حصہ6/durableidentity/installerشروعنہیں۔ اگلاالگحصہنئیاجازتسے۔

✅ آخریbaselineaudit:پچھلے04ba126کےتمام948trackedpathsبرقرار؛پرانےC#tests/financial/data/Tx/migrations/Counter/Focus/SettingsVM/nativeguidecodeنہیںبدلے۔ SettingsServiceمیںصرفfreshstartupreadاضافی؛checkedSetMode/installer/Shiftrecovery/VerifyClose/پرانےhelpersلفظبہلفظاصل۔ Guideکےباقی95موضوعات/R3metadataاورfeatureboardstatusesبرقرار؛Checklist2026-11-10کیexplicitتاریخ/versionپرregenerate،rulematrixمیںصرفdocumentationtrace۔ D-01تاD-119کی119numberedindexrowsموجود۔
