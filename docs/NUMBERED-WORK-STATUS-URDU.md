# نمبر وار کام اور باقی جانچ — مستقل1تا36فہرست

**موجودہ source: v0.111.0 Inlet / حصہ33۔** یہ وہی36نمبر ہیں جن کی مالک نے تفصیلی وضاحت مانگی۔ اگلے حصوں میں نمبر تبدیل نہیں کریں؛done-source،یہاں کی جانچ،Windowsثبوت اورباقی کام الگ لکھیں۔ **36نمبر،36Next نہیں؛فیچرboardکی فیصد Phase4 safety فیصد نہیں۔**

تازہ محدود کام **5/6/7/15/17/21/23/24 receipt/history**: [PT-53](POS-PHASE4-PART33-URDU.md)،[trace](POS-PHASE4-RECEIPT-CURRENT-REVIEW.md)۔95نئےC#cases/2400UNRUN؛requiredactor/purposemodulegates،currentHistory،Txstoredreprintaudit،stalepayload/timercleanup۔ session/CAS/fullPOS/remainingcallers/preparedoutputs/draft/file/COMMIT/Windows/fullPhase4/installerOPEN۔

| مستقل نمبر / دائرہ | کیا ہوگیا؟ | کیا باقی ہے؟ |
|---|---|---|
|1۔ ورژن/GitHub | v0.111Inletsource/docs/branding/scriptsaligned؛samebranchpublicationلازم | actualcommit/push/remoteverifyآخریرپورٹمیں؛Windowsbuild/installerالگ |
|2۔ C#tests | 2400متوقعcases؛حصہ33میں95نئے(7facts+88inlinecases) | تازہC#compile/run/fix/retestیہاںسبUNRUN |
|3۔ Source/syntheticchecks | 100static/422grammar،24syntheticSQL،29نئے+177پرانےlexicalnegatives/exactrestore،backup6roundtrips/24negativePASS | traceمیںحدود؛PythonpolicyC#authorization/session/WPFنہیں |
|4۔ پرانےWindowsfailures | v0.80کے1333PASS/4FAILکیچاروںوجوہ/sourcefixشناخت | تازہsuiteپرretestاورنئیfailuresکیfix؛پرانانتیجہنیاPASSنہیں |
|5۔ Account/role/session | حصہ33receipt/historystoredactive/sameparsedrole/modulechecks؛Ownercurrentcheck | authenticatedsessiontoken/same-roleIDimpersonation،remainingcallers؛یہfullsessionproofنہیں |
|6۔ Parentrecordcreate/edit/read | حصہ33actorlessreceiptreadہٹا،Historydirectreadگates،missingreturnauditsrefuse | quarantine/draftsaveopen/held/quickadd/parentworkflows،atomicCAS |
|7۔ Revocation/staleoutputs | حصہ33Historyaction/3stimerdeniedrows/detail/preview/totals/multiselectclear؛stableallowedpreserved/regrantfreshRefresh | fullPOS،otherdialogs،alreadyopenedpreview/files/paperrecallنہیں؛Windowsproofباقی |
|8۔ Customdefinitionmanager | **حصہ26:currentownerUI،selection/inputcleanup،selectedsnapshotcomparison،stabletick،SAVED** | WPF/runtimeثبوت؛check/use/atomicversionCASاورothereditorssyncOPEN |
|9۔ Customanswersservice | sharedinternalcurrentactor/storedaudit؛existingparentreads؛historicalinactiveپالیسیبرقرار | باقیcallers،اصلTxexecution،read/loadraces؛internalworkflowgatecaller-owned |
|10۔ Customanswereditor | stalerecordrows/stagedpathcleanup،freshoutputs،SAVED،loadedtimer | اصلbindings/focus/regrant/parentdraftconsistency؛definitionversionsyncالگ |
|11۔ Nativefields | currentOwner/suppliedactivepreflight،failedselectioncleanup،freshriskprompt،stableaccesschecks | اصلWindows/timer/confirmationاورformactionreconciliation |
|12۔ Photos/gallery | currentreviewedreads/writes/parent/path؛stalegallerycleanup؛approvedcover/avatarexceptions | camera/codec/viewer،othercachedcovers/avatar،filefault/collision/cleanup/powerloss |
|13۔ Cost/Profit/main | الگgrants/main/masks؛حصہ27stored-costpatchfreshDBmain+Cost؛authorednewcost/non-costCorepatchبرقرار | باقیroute/output/Windowscombinations؛free-textclassificationنہیں،preflightCASنہیں |
|14۔ Pricing/discount/Extra | approvedbelow-actual-costfloor،sale-onlyExtra،limits/returnBکےsource/tests | تازہruntimeاورcross-page/retryreconciliation؛settledrulesدوبارہنہیںکھولنے |
|15۔ Counter/bill/stock | حصہ33CounterreceiptcurrentPOS،حصہ32currentposting/حصہ31SAVEDبرقرار | fullPOScachedUI،quickadd/held/sessioncallers،اصلC#/WPF/scanner/receipt/durableidentity |
|16۔ Purchase/delivery | حصہ30postedlines/paid/draftIDقبلprintconsume،localreentry/SAVED؛18stagefaults+retry/3methods/sample/late-lineC#source | C#execution،mixedsupplier/variants/receipt/permissionmatrix؛Draftpremark-finalize-restorefaultsالگ |
|17۔ Customer/Recovery | حصہ33returnreceiptcurrentReturns،HistoryreturnusesSales؛ReturnBunchanged | actualC#/WPF،quarantine/recoveryPR/partialreturn/sessionintegration |
|18۔ Supplier/Pay/SR | exceptionBبرقرار؛حصہ30SRlocalreentry/traycleanup/reloadverdict/callbackSAVED؛26stagefaults/cash-credit/second-itemC#source | اصلC#/WPFstock/books/cash/results؛ambiguouscommit/durableidentity؛policyدوبارہنہیںپوچھنا |
|19۔ Cashdrawer/handover | حصہ31CoreSaleunknownreaddeny+sameTxopencheck،5methodSale/Returnderiveddrawerregressions | اصلprovider/race/C#/WPF/handover/closing/print/cashreconciliation |
|20۔ Expenses/stocktake/writeoff | متعددcost/valuation/history/resultboundaries،frozenSQLprobes | integratedstock/expense/drawer/return/reportreconciliation،rollback،Windowsvouchers |
|21۔ Reports/files/exports | حصہ33Historyreprint/preview/WhatsApp/bulkbuilderscurrentSales؛bulkwholejobrefusal | alreadyopenedpreview/preparedfilesnotrecalled،باقیoutputs،Excel/CSV/print/PDF/Windows |
|22۔ Blackbox/BookCheck | صرفapprovednavigationgateAudit+Cost+Profit+main؛حالیہinternalsunchanged | تازہWindowsnavigationproof؛بغیراجازتغیرمتعلقredesignنہیں |
|23۔ DB/permissiontransaction | حصہ33reprintauditcurrentSales+storedactor+writeایکTx؛failurepropagates | sessionbinding/CAS/provider/busy/parallel/productionjournal/actualCOMMIT-I/O،remainingcallers |
|24۔ Fault/rollback | حصہ33denialread/auditsnapshots/regrant،audittriggerfailure/missingreturn/invalidpurpose/bulkpartialregressions | تمامC#UNRUN؛دیگرstock/expense/recoveryPR/file/draft/actualcommitfaults |
|25۔ Duplicate/retry | حصہ32SaleDraftpremarkقبلcurrentgate؛پہلےSAVED/localblocksبرقرار | whole-draftatomicityنہیں؛durableidentity/restart/timeout/parallel/ambiguouscommit/actualWPFباقی |
|26۔ Encryptedbackup | passwordA .a11bakcode،manual/auto/safety/rollbackintegration،referencechecks | اصلC#service/DPAPI/ACL/passwordrotation/large-photo/interruptionverification |
|27۔ Restore/newPC | stagedvalidation/safety/rollback/explicitlegacycode | نئےPCپرoriginalpassword،wrong/corrupt/interruptedrestore،پراناDB/photosبچنااورstartupثبوت |
|28۔ Files/bounds | متعدددخل/path/size/extension/archivelimits | malformed/largeimage/archive/import،codec/replacement/collision/cleanup/powerloss |
|29۔ Cloud/credentials | بنیادیsync/settingsboundaries/newauditkeyredactionsource | الگtestremoteintegration،networkloss/retry/duplicates/isolation/credentials/restoreconfig |
|30۔ Desktop/WPF | UI/source/bindingchecks | تازہReleasebuild،windows/dialogs/DPI/clipping/scroll/selection/screenshots/logs |
|31۔ Keyboard/scanner | permanentsearch/approvednavigationcode | arrows/Shift-arrows/nativecaret/combo/grid/TabاوراصلUSBlookup/focus |
|32۔ Printer/PDF/camera | receipt/print/PDFcode؛پراناownerprinter-slipproofمحفوظ | تازہreceipts/width/printer/no-printer/scanner/camera/viewerاورfiguresملاپ |
|33۔ Startup/login/lock | approvedstartup/singleinstance/FocusGuard/recoverycode | حقیقیregistry/reboot/re-entry/escape/reset/recovery/shortcuts/taskbar/icon |
|34۔ Guide/actionreconciliation | اردوMD/HTML،PT-53/trace/1–36trackerاور36lessonanchorsاپڈیٹ | اصلappتمامroutes/settings/actionsسےملاپ،Windows/PTsignoffsباقی |
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
