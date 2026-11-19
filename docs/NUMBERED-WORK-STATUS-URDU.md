# نمبر وار کام اور باقی جانچ — مستقل1تا36فہرست

**موجودہ source: v0.112.0 Shore / حصہ34۔** یہ وہی36نمبر ہیں جن کی مالک نے تفصیلی وضاحت مانگی۔ اگلے حصوں میں نمبر تبدیل نہیں کریں؛done-source،یہاں کی جانچ،Windowsثبوت اورباقی کام الگ لکھیں۔ **36نمبر،36Next نہیں؛فیچرboardکی فیصد Phase4 safety فیصد نہیں۔**

تازہ محدود کام **5/6/7/17/20/23/24/25 quarantine**: [PT-54](POS-PHASE4-PART34-URDU.md)،[trace](POS-PHASE4-QUARANTINE-CURRENT-REVIEW.md)۔79نئےC#cases/2479UNRUN؛currentReturnsread/count،TxstoredOwner/Managerdecider،UIcapability/detachedrowguard/consume-before-reload/SAVED۔ session/CAS/fullPOS/draftheldquickadd/preparedoutputs/file/COMMIT/Windows/fullPhase4/installerOPEN۔

| مستقل نمبر / دائرہ | کیا ہوگیا؟ | کیا باقی ہے؟ |
|---|---|---|
|1۔ ورژن/GitHub | v0.112Shoresource/docs/branding/scriptsaligned؛samebranchpublicationلازم | actualcommit/push/remoteverifyآخریرپورٹمیں؛Windowsbuild/installerالگ |
|2۔ C#tests | 2479متوقعcases؛حصہ34میں79نئے(5facts+74inlinecases) | تازہC#compile/run/fix/retestیہاںسبUNRUN |
|3۔ Source/syntheticchecks | 101static/423grammar،25syntheticSQL،29نئے+206پرانےlexicalnegatives/exactrestore،backup6roundtrips/24negativePASS | traceمیںحدود؛PythonpolicyC#authorization/session/WPFنہیں |
|4۔ پرانےWindowsfailures | v0.80کے1333PASS/4FAILکیچاروںوجوہ/sourcefixشناخت | تازہsuiteپرretestاورنئیfailuresکیfix؛پرانانتیجہنیاPASSنہیں |
|5۔ Account/role/session | حصہ34quarantinecurrentstoredactive/sameparsedrole/Returnschecks؛validOwnerbypassبعدcurrentcheck | authenticatedsessiontoken/same-roleIDimpersonation،remainingcallers؛یہfullsessionproofنہیں |
|6۔ Parentrecordcreate/edit/read | حصہ34requiredactorPending/History/Count؛DisposeTxمیںcurrentOwner/Manager | draftsaveopen/held/quickadd/remainingparentworkflows،atomicCAS؛Alertscannerالگunchanged |
|7۔ Revocation/staleoutputs | حصہ34decisioncapability/loadverdict/readonlyUI،detachedrowguard؛حصہ32/33timercleanupبرقرار | fullPOS،otherdialogs/preparedoutputs/instantrecallنہیں؛Windowsbindings/timingباقی |
|8۔ Customdefinitionmanager | **حصہ26:currentownerUI،selection/inputcleanup،selectedsnapshotcomparison،stabletick،SAVED** | WPF/runtimeثبوت؛check/use/atomicversionCASاورothereditorssyncOPEN |
|9۔ Customanswersservice | sharedinternalcurrentactor/storedaudit؛existingparentreads؛historicalinactiveپالیسیبرقرار | باقیcallers،اصلTxexecution،read/loadraces؛internalworkflowgatecaller-owned |
|10۔ Customanswereditor | stalerecordrows/stagedpathcleanup،freshoutputs،SAVED،loadedtimer | اصلbindings/focus/regrant/parentdraftconsistency؛definitionversionsyncالگ |
|11۔ Nativefields | currentOwner/suppliedactivepreflight،failedselectioncleanup،freshriskprompt،stableaccesschecks | اصلWindows/timer/confirmationاورformactionreconciliation |
|12۔ Photos/gallery | currentreviewedreads/writes/parent/path؛stalegallerycleanup؛approvedcover/avatarexceptions | camera/codec/viewer،othercachedcovers/avatar،filefault/collision/cleanup/powerloss |
|13۔ Cost/Profit/main | الگgrants/main/masks؛حصہ27stored-costpatchfreshDBmain+Cost؛authorednewcost/non-costCorepatchبرقرار | باقیroute/output/Windowscombinations؛free-textclassificationنہیں،preflightCASنہیں |
|14۔ Pricing/discount/Extra | approvedbelow-actual-costfloor،sale-onlyExtra،limits/returnBکےsource/tests | تازہruntimeاورcross-page/retryreconciliation؛settledrulesدوبارہنہیںکھولنے |
|15۔ Counter/bill/stock | حصہ33CounterreceiptcurrentPOS،حصہ32currentposting/حصہ31SAVEDبرقرار | fullPOScachedUI،quickadd/held/sessioncallers،اصلC#/WPF/scanner/receipt/durableidentity |
|16۔ Purchase/delivery | حصہ30postedlines/paid/draftIDقبلprintconsume،localreentry/SAVED؛18stagefaults+retry/3methods/sample/late-lineC#source | C#execution،mixedsupplier/variants/receipt/permissionmatrix؛Draftpremark-finalize-restorefaultsالگ |
|17۔ Customer/Recovery | حصہ34quarantinecurrentReturnsreader+storeddecider؛ReturnB/refundsunchanged | actualC#/WPF،recoveryPR/partialreturn/sessionintegration |
|18۔ Supplier/Pay/SR | exceptionBبرقرار؛حصہ30SRlocalreentry/traycleanup/reloadverdict/callbackSAVED؛26stagefaults/cash-credit/second-itemC#source | اصلC#/WPFstock/books/cash/results؛ambiguouscommit/durableidentity؛policyدوبارہنہیںپوچھنا |
|19۔ Cashdrawer/handover | حصہ31CoreSaleunknownreaddeny+sameTxopencheck،5methodSale/Returnderiveddrawerregressions | اصلprovider/race/C#/WPF/handover/closing/print/cashreconciliation |
|20۔ Expenses/stocktake/writeoff | حصہ34quarantinestockstatementfaults+3outcomes/retrysource؛AddBack/SQLunchanged | integratedstock/expense/drawer/return/reportreconciliation،legacybatch/fallbackedges،Windowsvouchers |
|21۔ Reports/files/exports | حصہ33Historyreprint/preview/WhatsApp/bulkbuilderscurrentSales؛bulkwholejobrefusal | alreadyopenedpreview/preparedfilesnotrecalled،باقیoutputs،Excel/CSV/print/PDF/Windows |
|22۔ Blackbox/BookCheck | صرفapprovednavigationgateAudit+Cost+Profit+main؛حالیہinternalsunchanged | تازہWindowsnavigationproof؛بغیراجازتغیرمتعلقredesignنہیں |
|23۔ DB/permissiontransaction | حصہ34quarantinecurrentReturnsOwner/Manager+target+mutation/auditایکTx؛storedactor | sessionbinding/CAS/provider/busy/parallel/productionjournal/actualCOMMIT-I-O،remainingcallers |
|24۔ Fault/rollback | حصہ34denialsnapshots/regrant،20BEFORE/AFTERfaults+retry،countfailure/3outcomes/storedname | تمامC#UNRUN؛دیگرstock/expense/recoveryPR/file/draft/actualcommitfaults |
|25۔ Duplicate/retry | حصہ34alreadydecidedCoreguardبرقرار،UIcommittedrowconsume-before-reload/SAVED/detachedrowrefusal | whole-draftatomicityنہیں؛durableidentity/restart/timeout/parallel/ambiguouscommit/actualWPFباقی |
|26۔ Encryptedbackup | passwordA .a11bakcode،manual/auto/safety/rollbackintegration،referencechecks | اصلC#service/DPAPI/ACL/passwordrotation/large-photo/interruptionverification |
|27۔ Restore/newPC | stagedvalidation/safety/rollback/explicitlegacycode | نئےPCپرoriginalpassword،wrong/corrupt/interruptedrestore،پراناDB/photosبچنااورstartupثبوت |
|28۔ Files/bounds | متعدددخل/path/size/extension/archivelimits | malformed/largeimage/archive/import،codec/replacement/collision/cleanup/powerloss |
|29۔ Cloud/credentials | بنیادیsync/settingsboundaries/newauditkeyredactionsource | الگtestremoteintegration،networkloss/retry/duplicates/isolation/credentials/restoreconfig |
|30۔ Desktop/WPF | UI/source/bindingchecks | تازہReleasebuild،windows/dialogs/DPI/clipping/scroll/selection/screenshots/logs |
|31۔ Keyboard/scanner | permanentsearch/approvednavigationcode | arrows/Shift-arrows/nativecaret/combo/grid/TabاوراصلUSBlookup/focus |
|32۔ Printer/PDF/camera | receipt/print/PDFcode؛پراناownerprinter-slipproofمحفوظ | تازہreceipts/width/printer/no-printer/scanner/camera/viewerاورfiguresملاپ |
|33۔ Startup/login/lock | approvedstartup/singleinstance/FocusGuard/recoverycode | حقیقیregistry/reboot/re-entry/escape/reset/recovery/shortcuts/taskbar/icon |
|34۔ Guide/actionreconciliation | اردوMD/HTML،PT-54/trace/1–36trackerاور36lessonanchorsاپڈیٹ | اصلappتمامroutes/settings/actionsسےملاپ،Windows/PTsignoffsباقی |
|35۔ Isolatedtestinstallation | verifiedbackup+separateDB/photos/configکیشرطdocumented | اصلVM/sparePC/storageproof،startup/migrationسےshopdataمحفوظ؛exefolderاکیلاکافینہیں |
|36۔ Singleinstaller | مالککیاجازت؛build/installerconfig/scriptsموجود | کامیابWindowsbuild/tests،exeبننا،cleaninstall/upgrade/uninstall/datapreservation/shortcuts/startup،پھرsendablefile؛**ابھینہیںبنا** |

## اگلی رپورٹ کا لازمی انداز

1. انہی نمبروں میں سے کس پرکام ہوا؟
2. اسی نمبر میں sourceمیںکیاکیاہوگیا؟
3. کیاواقعیجانچا،کسماحولمیں،اورکیاصرفUNRUNtestsourceہے؟
4. اسی نمبرکےاندرکیاباقیہےاورکیوں؟
5. اگلےنمبروںکیترتیب،docs/GitHubکیاصلحیثیت،installerکےباقیدروازے۔

موجودہترتیب: **23–25** integratedfault/retry/concurrency؛اسکےساتھ **5/6/7** remainingcaller/sessionreconciliation،**26–29**files/restore/cloud،**2/4/30–35**Windows/results/hardware/isolation،آخر **36**۔ یہلچکدارترتیبہے؛dependencyیاحقیقیfindingسےتبدیلہوسکتیہے۔3–6مزیدمربوطreview/resultچکرمشروطاندازہ،نہdeadlineنہupperbound۔

اصلshopپرsyntheticpostsنہیں؛secret/datachatمیںنہیں؛ہرportiondocs+sameactivebranchcommit/push+actualremoteverification۔ Static/Python/featureboardکوproduction/runtime/securityPASSنہکہیں۔
