# ⏳ PT-84 — Main Settings Save کا محفوظ نتیجہ اور handoff

**v0.145.0 Valerian · 2026-11-10 · اسی منظورشدہ5.1 کا محدود حصہ۔**
سورس کی پیشرفت ہے؛پوری Settings،Focus Guard نظام،Windows/WPF یا حصہ5 کی تکمیل نہیں۔

## ⏳ پہلے کا مسئلہ

Main Settings کی اصلdictionary checked/atomic Core Save سے جاتی تھی،لیکن Desktop میں اس Save کا مستقل outcome guard نہیں تھا۔ DB کامیاب ہونے کے بعد summary،credential box،label rows،shell callback یاstatus میں exception آئے تو اصل محفوظ نتیجہ اورصفحے کی خرابی الگ نہیں سنبھلتے تھے۔ بعد کا startup یاscreen-lock Save رک جائے تو پہلے محفوظmainvaluesبھیاندھیدوبارہکوششمیںچلےجاسکتےتھے۔ Screen-lockکےہر `Ok=false` کو “not saved” کہنا بھیunknown/post-commitنتیجےکےلیےدرستنہیںتھا۔

## ⏳ اب سورس میں کیا شامل ہے؟

- Main Save کا ایک retained `PostingAttemptGuard`؛existing synchronous `_savingSettings` کے ساتھ reentry بند۔ Guard صرف validation/اصلvaluesdictionaryتیارہونےکےبعد،serviceسےعینپہلےشروعہوتاہے۔ Interactive session کاpreflight اوراصلCoreکےcurrentwriter/CASchecksبرقرارہیں۔
- `NoCommitConfirmed=true` primary refusal پراصلکھلاformاورtypedinputsبرقرار؛وجہدرستکریںیاconflictکیصورتRefresh/reviewکریں۔ Unknownprimaryنتیجےپرforminvalidate،guardبنداورواضحverificationپیغام۔ صرف `Ok=false` سےrollbackنہیںاخذکیا۔
- Service کامیابی پر **acknowledgement پہلے**؛پھرنئیnon-nullreceipt،visiblewrittenkeysکیcompletenessاوراسیreturnedreceiptکیadoption۔ Coreکیnormalization/immutablepartialreceiptبدلییادوبارہimplementنہیںکی۔ تازہDB/cache readکامیابreceiptکیجگہنہیںلیتا۔
- Acknowledgement کےبعدtypedcloudkeyاورlabel-resetconfirmationکےbackingfields **کسیproperty/collectioncallbackسےپہلے** consumeہوتےہیں۔ پھرSyncKeynotification،summaries،receiptسےlabelrowsاوراصلshellcallback۔ Submitteddictionary/credentialکوstatusیااسنئےDesktoplogمیںشاملنہیںکیا۔
- Callbackکےبعدreceiptidentity،خالیconsumedkey/confirmationاورتمام32labelrowsکامیل؛آخرمیںبھییہverdictدرستہوتبھیguardکھلتاہے۔ Receiptغائب/نامکملہویاروزمرہnotificationthrowکرےتو **Main settings SAVED** اپنیجگہدرسترہتاہے۔
- Startupchoice،screen-lockchoiceاورانکاپہلےسےجائزUIaccess،callbacksسےپہلےpinnedہیں؛callbackکیبدلیہوئیselectionخودنیاSaveintentنہیںبنتی۔ اصلCoreدوبارہcurrentauthorizationچیککرتاہے۔
- آخریpaper/automatic-print/cloudon-offخلاصہmainreceiptسےبنتاہے،بعدمیںبدلیہوئیliveinputs/cacheسےنہیں۔ Cloudonہوناکامیابnetworkconnectionکادعویٰنہیں۔

## ⏳ ایک بٹن،مگر الگ commits

Main dictionary،startupاورscreen-lockابھیبھی **الگtransactions/resources** ہیں۔ انہیںایکatomicSaveنہیںبنایااورمحفوظmainvaluesواپسنہیںپلٹائے۔

| مرحلہ/نتیجہ | اسیصفحےکابرتاؤ |
|---|---|
| Primaryvalidation/preflight،serviceابھینہیںچلی | Mainpostingguardشروعنہیں؛کوئیSavecommitنہیں |
| Primaryواضحno-commit | guardReady؛اصلform/inputبرقرار؛درستکریں،conflictپرRefresh/review |
| Primaryunknown/serviceexception | guardبند،forminvalidate؛Settings/auditتصدیقکےبعدreopen،اندھیretryنہیں |
| Primaryکامیاب،بعدمیںUIcallbackخراب | **Main settings SAVED**؛guardبند؛مکملہوچکیwriteکوناکامنہیںبتاتے |
| Primaryکامیاب،startupرکگیا | mainSAVEDاوراصلstartuphintالگ؛guardبند؛دونوںحالتیںتصدیقکریں |
| Primaryکامیاب،screen-lockجوابناکام/unknown | mainSAVED،اصلscreen-lockerrorبشمولممکنہSAVEDمتنبرقرار؛guardبند |
| Screen-lockکامیاب،receipt/chromecallbackخراب | mainاورscreen-lockکیSAVEDحیثیتبرقرار؛verificationلازم |
| پوریbuttonsequenceاورhandoffکامیاب | آخریexactreceiptbaselineکےساتھguardReady؛اگلاSaveنیاارادہہے |

**جان بوجھ کر محتاط حد:** primarycommitکےبعدstartupیاسcreen-lockکی **معلومrefusalبھی** پوریbuttonsequenceکاguardنہیںکھولتی۔ پہلےمحفوظہوچکیdictionaryدوبارہaudit/writeکرنےکےبجائےنتائجتصدیقکریں،پھرSettingsreopen۔ یہavailabilityکیقربانیہے،cross-resource rollbackیادیرپاdedupنہیں۔

اسbuttonمیںscreen-lockکاپراناغلط“not saved”پیغامہٹایا،receiptکیلازمیتاورpinnedchoiceپرhandoffجانچا؛لیکنFocusGuardکےپورےstartup/login/lockdown/recoveryاوراصلWindowssecuritybehaviorکومکملreviewشدہنہیںکہا۔ وہالگباقیہیں۔ نیاcheckedstartupserviceاوراسکاپچھلاguardنہیںبدلا۔

## ⏳ guard کو کیا نہیں کھولتا؟

Refresh/regrant/tab/inputتبدیلی،iconکیpartialreceiptیانئےloadedformسےunknown/SAVED-handoffguardresetنہیںہوتا۔ UnknownmainSaveکےبعد **Restore original words** بھیاسیguardکومانتاہے،کیونکہmainSaveمیںوہیlabelkeysشاملتھیں۔ In-flightmainSaveمیںiconactionکوداخلنہیںہونےدیتے۔ الگ **MainSettingsHint** پورےSettingsheaderمیںرہتاہے،چاہےعامstatusبعدمیںبدلجائے۔

Refreshموجودہvaluesدکھاسکتاہے،خودverificationنہیں۔ Reopenبھیخودverificationنہیں؛یہlocalguardcrash/restart/دوسرےصفحےیاPCکےپارidentityنہیں۔ ServiceشروعہونےسےپہلےvalidationUIکیہرexceptionکےلیےنیاعمومیexceptionframeworkنہیںبنایا۔

## ✅ محدود جانچ / ⏳ اصل جانچ باقی

- ✅ **135repository static checks**،C#grammar/member-source resolutionسمیت؛.NETbuildنہیں۔
- ✅ **53نئیlexicalmutations** reject،اصلbytesبحال؛تمام **42source-negative scripts** پاس۔ پرانےcurrentSettingsmarkerکوغلط“screen lock was not saved”کےبجائےنئےtruthfulresultmarkerسےبدلا؛پرانیC#assertionsنہیںہٹائیں۔
- ✅ موجودہgeneralSettingsSQLprobe:**37migrations/24Python-owned WAL+DELETE plans** دوبارہپاس؛یہنئےDesktopSaveCoreیاactualprovider/WPFcallbacksکانفاذنہیںچلاتا۔
- ⏳ **25نئےC#cases** `MainSettingsSaveOutcomeTests.cs`:Owner/Managerکیmixedlabels+mainvaluesreceipt/audit/reuse،Counterprint/labelconflict،untouchedstartup/focusbaseline،heartbeat،null/foreignform،early/lateauditrollback،deferredCOMMITunknown،currentauthorization،maskedcloudkeyrotation،cloudsmuggling،exactreceiptبمقابلہبعدکاwriter،primarycommitکےبعدfollowuprefusal/مصنوعیclientcallback،guardevidence/completehandoff،session/currentauditname/serialization۔ یہpublicCore+guardtestsہیں،DesktopSaveCoreکانقلیٹیسٹactualWPFنہیںکہاگیا۔ **یہاںSDKنہیں؛25casesاور3612کیپوریsuiteUNRUNہیں۔**
- ✅ مالککااصل **v0.138.0Windows/.NET10Core+Tests:3418PASS/0FAIL/0skip،6m50s** ثابتہے،pendingنہیں۔ یہنئے3612یاDesktopUIکیسندنہیں۔
- ⏳ اصلWindows/WPF/providerجانچ:primarysaveکےفوراًبعدsummary/secret/rows/shell/statusexceptions؛receiptconsumptionاورreentrantSave/Reset/icon؛callbackکےدورانstartup/focus/permissionsتبدیلی؛primarySAVEDکےبعدہرpartialstagefailure؛chromeeventthrow؛Refresh/regrantسےguardنہکھلے؛typedwork/secretmasking/hintwrap/focus/DPI؛پوریDebug/Releasebuild،suiteاورwarnings۔ Live-shopیااصلregistryپرکوئیتجربہنہیں۔

## ⏳ باقی کام

CoreSettings/transaction/authorization/CAS/financialpolicy،37migrations،پرانےC#tests،Counter،AppLabelResetاورchecked/legacyStartupservices/nativeguideنہیںبدلے۔ FocusGuardکاالگتفصیلیreview،باقیimperative/legacyStartupراستے،StockTake،حصہ1تا5کیضروریWindows/provider/WPFتصدیقاورنئیguideUIکیعملیرائےکھلیہیں۔ ❌حصہ6/durableidentity/installerشروعنہیں۔ اگلاالگحصہنئیاجازتسے۔

✅ آخری baseline audit:پچھلےcheckpointکےتمام938trackedpathsبرقرار؛پرانےC#testsاورCoreservices/models/Tx/data/migrations/Counter/startup/nativeguideنہیںبدلے۔ MainSaveکیپرانیکاروباریvalidationاورارسالہونےوالیdictionaryلفظبہلفظبرقرار؛صرفsessionpreflight/pinnedintent/outcomehandoffشامل۔ LabelResetمیںصرفmain-guardentry،iconمیںصرفin-flightentry؛startupUIhelper/cloud/staffبرقرار۔ نئےMainSavecatchlogsمیںrawexceptionmessage/stackکےبجائےصرفexceptiontypename؛callbackسےcredentialوالیexceptionبھییہاںechoنہیںہوتی۔
