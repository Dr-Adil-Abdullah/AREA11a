# نمبر وار کام اور باقی جانچ — مستقل1تا36فہرست

**⏳ موجودہ source: v0.116.0 Tarn / حصہ38؛مکمل عملی تصدیق نہیں۔** یہ وہی36نمبر ہیں جن کی مالک نے تفصیلی وضاحت مانگی۔ اگلے حصوں میں نمبر تبدیل نہیں کریں؛done-source،یہاں کی جانچ،Windowsثبوت اورباقی کام الگ لکھیں۔ **36نمبر،36Next نہیں؛فیچرboardکی فیصد Phase4 safety فیصد نہیں۔**

✅ منظورشدہعلامتیں:✅صرف100%مکملمتعلقہکام؛⏳جاری/کچھباقی؛❌شروعنہیں۔ ⏳تازہ20/23–25:expense/drawing/write-off/Applyoutcomeاورlocalcleanup/retryguard؛59نئےcases/2744C#suiteUNRUN۔ ✅34SQLfaults+retry/33lexicalmutationsمقامیPASS،C#/WPFنہیں۔ [⏳ PT-58](POS-PHASE4-PART38-URDU.md)،[⏳ trace](POS-PHASE4-EXPENSE-STOCK-FAULT-REVIEW.md)۔ ⏳runtime/FullPhase4باقی؛❌installerbinaryنہیںبنی۔

| مستقل نمبر / دائرہ | کیا ہوگیا؟ | کیا باقی ہے؟ |
|---|---|---|
|1۔⏳ ورژن/GitHub | v0.116Tarn/source/docs/icon/scriptsaligned | samebranchcommit/push/actualremoteverificationآخریرپورٹمیں |
|2۔⏳ C#tests | 2744متوقعcases؛حصہ38میں59نئے(57inline+2fact) | تازہC#compile/run/fix/retestیہاںنہیںچلا |
|3۔⏳ Source/syntheticchecks | 105staticcontracts؛نیا34faultSQLprobe/33lexicalmutations؛حتمینتائجtraceمیں | یہC#/WPF/provider/securitycertificationنہیں |
|4۔ ⏳ پرانےWindowsfailures | v0.80کے1333PASS/4FAILکیچاروںوجوہ/sourcefixشناخت | تازہsuiteپرretestاورنئیfailuresکیfix؛پرانانتیجہنیاPASSنہیں |
|5۔ ⏳ Account/role/session | حصہ35draft/heldcurrentstoredactive/sameparsedrole/modulechecks؛validOwnercurrentcheck | authenticatedsessiontoken/same-roleIDimpersonation،remainingcallers؛یہfullsessionproofنہیں |
|6۔ ⏳ Parentrecordcreate/edit/read | حصہ35draftsave/update/open/list/discard،heldList/Count/writerscurrent؛discardقبلpayloadtypeonly | remainingquickadd/parent/sessioncallers،atomicCAS؛legacy/otherworkflowreviewباقی |
|7۔ ⏳ Revocation/staleoutputs | حصہ35draftdrawer/parkedlistaction+3stimerdeniedlists/selectionclear،failedlist!=empty،modalrecheck | fullPOScart/pickers،otherdialogs/preparedoutputs/instantrecallنہیں؛Windowsproofباقی |
|8۔ ⏳ Customdefinitionmanager | **حصہ26:currentownerUI،selection/inputcleanup،selectedsnapshotcomparison،stabletick،SAVED** | WPF/runtimeثبوت؛check/use/atomicversionCASاورothereditorssyncOPEN |
|9۔ ⏳ Customanswersservice | sharedinternalcurrentactor/storedaudit؛existingparentreads؛historicalinactiveپالیسیبرقرار | باقیcallers،اصلTxexecution،read/loadraces؛internalworkflowgatecaller-owned |
|10۔ ⏳ Customanswereditor | stalerecordrows/stagedpathcleanup،freshoutputs،SAVED،loadedtimer | اصلbindings/focus/regrant/parentdraftconsistency؛definitionversionsyncالگ |
|11۔ ⏳ Nativefields | currentOwner/suppliedactivepreflight،failedselectioncleanup،freshriskprompt،stableaccesschecks | اصلWindows/timer/confirmationاورformactionreconciliation |
|12۔ ⏳ Photos/gallery | currentreviewedreads/writes/parent/path؛stalegallerycleanup؛approvedcover/avatarexceptions | camera/codec/viewer،othercachedcovers/avatar،filefault/collision/cleanup/powerloss |
|13۔ ⏳ Cost/Profit/main | الگgrants/main/masks؛حصہ27stored-costpatchfreshDBmain+Cost؛authorednewcost/non-costCorepatchبرقرار | باقیroute/output/Windowscombinations؛free-textclassificationنہیں،preflightCASنہیں |
|14۔ ⏳ Pricing/discount/Extra | approvedbelow-actual-costfloor،sale-onlyExtra،limits/returnBکےsource/tests | تازہruntimeاورcross-page/retryreconciliation؛settledrulesدوبارہنہیںکھولنے |
|15۔ ⏳ Counter/bill/stock | عامCompleteسےپہلےsharedDraftsave/link؛sameIDpost؛all draftHoldrefused؛Holdknown/unknown/cleanupguards | اصلWPF/restart/powercut،fullPOS/session/scanner،ordinaryHolddurableintent |
|16۔ ⏳ Purchase/delivery | عامSaveسےپہلےsharedDraft؛Purchasepartialcleanup/reconstructionguards؛sameIDretry | اصلC#/WPF/mixedsupplier/providerCOMMIT؛دیگرCoreCreatecallers/globalidentity |
|17۔ ⏳ Customer/Recovery | حصہ34quarantinecurrentReturnsreader+storeddecider؛ReturnB/refundsunchanged | actualC#/WPF،recoveryPR/partialreturn/sessionintegration |
|18۔ ⏳ Supplier/Pay/SR | exceptionBبرقرار؛حصہ30SRlocalreentry/traycleanup/reloadverdict/callbackSAVED؛26stagefaults/cash-credit/second-itemC#source | اصلC#/WPFstock/books/cash/results؛ambiguouscommit/durableidentity؛policyدوبارہنہیںپوچھنا |
|19۔ ⏳ Cashdrawer/handover | حصہ31CoreSaleunknownreaddeny+sameTxopencheck،5methodSale/Returnderiveddrawerregressions | اصلprovider/race/C#/WPF/handover/closing/print/cashreconciliation |
|20۔⏳ Expenses/stocktake/writeoff | expense/cash+goodsdrawing/write-off/Applylocalguard؛ackپہلے،cleanupبعد؛oldApplysessionconsume،financialSQL/helpersunchanged | actualC#/WPF،integratedreports/legacybatch/costfallback،Open/Count/Cancel/foundownfaultpaths |
|21۔ ⏳ Reports/files/exports | حصہ33Historyreprint/preview/WhatsApp/bulkbuilderscurrentSales؛bulkwholejobrefusal | alreadyopenedpreview/preparedfilesnotrecalled،باقیoutputs،Excel/CSV/print/PDF/Windows |
|22۔ ⏳ Blackbox/BookCheck | صرفapprovednavigationgateAudit+Cost+Profit+main؛حالیہinternalsunchanged | تازہWindowsnavigationproof؛بغیراجازتغیرمتعلقredesignنہیں |
|23۔⏳ DB/permissiontransaction | انپانچpostingwrappersمیںexplicitTransactionOutcome؛preflight/confirmedrollbackہیsafe،COMMITattemptunknown | provider/rollbackfailure/parallel/session/CAS/durableidentity؛pre-servicethrowsconservativeunknown |
|24۔⏳ Fault/rollback | 34expense/stockstatementfaults+10refusals+2laterline+1cost+12guardکے59C#sourcecases؛نیاPythonprobe34PASS | تمام2744C#UNRUN؛باقیrecoveryPR/files/realCOMMITfaultsاورWPFcallbackinjection |
|25۔⏳ Duplicate/retry | localInFlight/NeedsVerification/CommittedNeedsResetguard؛unknownretryبند؛ackcleanupfailمیںSAVED؛Applyeditblock | pagereopen/restart/globalidentity/parallel/providerCOMMITباقی؛localguarddurablekeyنہیں |
|26۔ ⏳ Encryptedbackup | passwordA .a11bakcode،manual/auto/safety/rollbackintegration،referencechecks | اصلC#service/DPAPI/ACL/passwordrotation/large-photo/interruptionverification |
|27۔ ⏳ Restore/newPC | stagedvalidation/safety/rollback/explicitlegacycode | نئےPCپرoriginalpassword،wrong/corrupt/interruptedrestore،پراناDB/photosبچنااورstartupثبوت |
|28۔ ⏳ Files/bounds | متعدددخل/path/size/extension/archivelimits | malformed/largeimage/archive/import،codec/replacement/collision/cleanup/powerloss |
|29۔ ⏳ Cloud/credentials | بنیادیsync/settingsboundaries/newauditkeyredactionsource | الگtestremoteintegration،networkloss/retry/duplicates/isolation/credentials/restoreconfig |
|30۔ ⏳ Desktop/WPF | UI/source/bindingchecks | تازہReleasebuild،windows/dialogs/DPI/clipping/scroll/selection/screenshots/logs |
|31۔ ⏳ Keyboard/scanner | permanentsearch/approvednavigationcode | arrows/Shift-arrows/nativecaret/combo/grid/TabاوراصلUSBlookup/focus |
|32۔ ⏳ Printer/PDF/camera | receipt/print/PDFcode؛پراناownerprinter-slipproofمحفوظ | تازہreceipts/width/printer/no-printer/scanner/camera/viewerاورfiguresملاپ |
|33۔ ⏳ Startup/login/lock | approvedstartup/singleinstance/FocusGuard/recoverycode | حقیقیregistry/reboot/re-entry/escape/reset/recovery/shortcuts/taskbar/icon |
|34۔⏳ Guide/actionreconciliation | اردوMD/HTML،PT-58/trace/1–36trackerاور36lessonanchorsاپڈیٹ | اصلappتمامroutes/settings/actionsسےملاپ،Windows/PTsignoffsباقی |
|35۔ ⏳ Isolatedtestinstallation | verifiedbackup+separateDB/photos/configکیشرطdocumented | اصلVM/sparePC/storageproof،startup/migrationسےshopdataمحفوظ؛exefolderاکیلاکافینہیں |
|36۔ ❌ Singleinstaller | مالککیاجازت؛build/installerconfig/scriptsموجود | کامیابWindowsbuild/tests،exeبننا،cleaninstall/upgrade/uninstall/datapreservation/shortcuts/startup،پھرsendablefile؛**ابھینہیںبنا** |

## اگلی رپورٹ کا لازمی انداز

1. انہی نمبروں میں سے کس پرکام ہوا؟
2. اسی نمبر میں sourceمیںکیاکیاہوگیا؟
3. کیاواقعیجانچا،کسماحولمیں،اورکیاصرفUNRUNtestsourceہے؟
4. اسی نمبرکےاندرکیاباقیہےاورکیوں؟
5. اگلےنمبروںکیترتیب،docs/GitHubکیاصلحیثیت،installerکےباقیدروازے۔

موجودہترتیب: **23–25** integratedfault/retry/concurrency؛اسکےساتھ **5/6/7** remainingcaller/sessionreconciliation،**26–29**files/restore/cloud،**2/4/30–35**Windows/results/hardware/isolation،آخر **36**۔ یہلچکدارترتیبہے؛dependencyیاحقیقیfindingسےتبدیلہوسکتیہے۔3–6مزیدمربوطreview/resultچکرمشروطاندازہ،نہdeadlineنہupperbound۔

اصلshopپرsyntheticpostsنہیں؛secret/datachatمیںنہیں؛ہرportiondocs+sameactivebranchcommit/push+actualremoteverification۔ Static/Python/featureboardکوproduction/runtime/securityPASSنہکہیں۔
