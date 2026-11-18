# نمبر وار کام اور باقی جانچ — مستقل1تا36فہرست

**موجودہ source: v0.108.0 Headland / حصہ30۔** یہ وہی36نمبر ہیں جن کی مالک نے تفصیلی وضاحت مانگی۔ اگلے حصوں میں نمبر تبدیل نہیں کریں؛done-source،یہاں کی جانچ،Windowsثبوت اورباقی کام الگ لکھیں۔ **36نمبر،36Next نہیں؛فیچرboardکی فیصد Phase4 safety فیصد نہیں۔**

تازہ محدود کام **16/18/23/24/25 Purchase/SupplierReturn**: [PT-50](POS-PHASE4-PART30-URDU.md)،[trace](POS-PHASE4-STOCK-POSTING-INTEGRITY-REVIEW.md)۔50نئےC#cases/acknowledgedcommitSAVED/consumedform/localreentry؛fullclosureنہیں۔ Coreمالیpolicyunchanged،Sale/customerReturn/draftfaults/actualcommitambiguity/requestidentity/Windows/installerOPEN۔

| مستقل نمبر / دائرہ | کیا ہوگیا؟ | کیا باقی ہے؟ |
|---|---|---|
|1۔ ورژن/GitHub | v0.108Headlandsource/docs/branding/scriptsaligned؛samebranchpublicationلازم | actualcommit/push/remoteverifyآخریرپورٹمیں؛Windowsbuild/installerالگ |
|2۔ C#tests | 2176متوقعcases؛حصہ30میں50نئے(3facts+47inlinecases) | تمامتازہC#compile/run/fix/retest؛یہاںUNRUN |
|3۔ Source/syntheticchecks | 97static/419grammar،21syntheticSQL،22نئے+23ledger+49catalog+30recordlexicalnegativesاورPythonbackupreferencePASS | traceمیںحدود؛PythonTx/guardsC#provider/WPFکابدلنہیں |
|4۔ پرانےWindowsfailures | v0.80کے1333PASS/4FAILکیچاروںوجوہ/sourcefixشناخت | تازہsuiteپرretestاورنئیfailuresکیfix؛پرانانتیجہنیاPASSنہیں |
|5۔ Account/role/session | حصہ28تمام16catalog/unitwriterscurrentstoredProductsmanager/audit؛reader capability | حقیقیauthenticatedsessionbinding،remainingcallers؛storedIDخودsessionproofنہیں |
|6۔ Parentrecordcreate/edit/read | حصہ27Customer/Product،حصہ28Catalog/Unitswritesقبلno-op/mutationguard؛oldSQL/rulesبرقرار | باقیparent/workflows/actorlesslookupcallers،versionCAS؛Brandname/linkالگTxہیں |
|7۔ Revocation/staleoutputs | حصہ30Purchaseexplicitlookupverdict،SRcallbackfreshaccess/failedexpiredlist/cleanup/SAVED | باقیdialogs/cache/quickadd،اصلWindowsfocus/timing/regrant؛instantrecallنہیں |
|8۔ Customdefinitionmanager | **حصہ26:currentownerUI،selection/inputcleanup،selectedsnapshotcomparison،stabletick،SAVED** | WPF/runtimeثبوت؛check/use/atomicversionCASاورothereditorssyncOPEN |
|9۔ Customanswersservice | sharedinternalcurrentactor/storedaudit؛existingparentreads؛historicalinactiveپالیسیبرقرار | باقیcallers،اصلTxexecution،read/loadraces؛internalworkflowgatecaller-owned |
|10۔ Customanswereditor | stalerecordrows/stagedpathcleanup،freshoutputs،SAVED،loadedtimer | اصلbindings/focus/regrant/parentdraftconsistency؛definitionversionsyncالگ |
|11۔ Nativefields | currentOwner/suppliedactivepreflight،failedselectioncleanup،freshriskprompt،stableaccesschecks | اصلWindows/timer/confirmationاورformactionreconciliation |
|12۔ Photos/gallery | currentreviewedreads/writes/parent/path؛stalegallerycleanup؛approvedcover/avatarexceptions | camera/codec/viewer،othercachedcovers/avatar،filefault/collision/cleanup/powerloss |
|13۔ Cost/Profit/main | الگgrants/main/masks؛حصہ27stored-costpatchfreshDBmain+Cost؛authorednewcost/non-costCorepatchبرقرار | باقیroute/output/Windowscombinations؛free-textclassificationنہیں،preflightCASنہیں |
|14۔ Pricing/discount/Extra | approvedbelow-actual-costfloor،sale-onlyExtra،limits/returnBکےsource/tests | تازہruntimeاورcross-page/retryreconciliation؛settledrulesدوبارہنہیںکھولنے |
|15۔ Counter/bill/stock | بنیادیbilling/stock/pricingاورreviewedguards | duplicatecompletion،stockrefusal/cart/numberpreservation،split/credit/change/loyalty/receiptثبوت |
|16۔ Purchase/delivery | حصہ30postedlines/paid/draftIDقبلprintconsume،localreentry/SAVED؛18stagefaults+retry/3methods/sample/late-lineC#source | C#execution،mixedsupplier/variants/receipt/permissionmatrix؛Draftpremark-finalize-restorefaultsالگ |
|17۔ Customer/Recovery | حصہ29CollectpostcommitSAVED/localreentry،ledger-pairfault/retry/books/drawerregressions | C#execution،دیگرcommands/receipts/recovery/returnsreconciliationاورdurableduplicateidentity |
|18۔ Supplier/Pay/SR | exceptionBبرقرار؛حصہ30SRlocalreentry/traycleanup/reloadverdict/callbackSAVED؛26stagefaults/cash-credit/second-itemC#source | اصلC#/WPFstock/books/cash/results؛ambiguouscommit/durableidentity؛policyدوبارہنہیںپوچھنا |
|19۔ Cashdrawer/handover | حصہ30purchasecash-card-online/SRcash-vs-creditderiveddrawer/booksregressions؛Shiftunchanged | actualC#/fault/reentry/print/handover/closingاورcross-servicecashreconciliation |
|20۔ Expenses/stocktake/writeoff | متعددcost/valuation/history/resultboundaries،frozenSQLprobes | integratedstock/expense/drawer/return/reportreconciliation،rollback،Windowsvouchers |
|21۔ Reports/files/exports | کئیcurrentoutputgates/masks؛editablebulkOwner-defaultwholefilegate | باقیoutputdoors،preparedfiles،realExcel/CSVroundtrip/bounds/formulas/print/PDFملاپ |
|22۔ Blackbox/BookCheck | صرفapprovednavigationgateAudit+Cost+Profit+main؛حالیہinternalsunchanged | تازہWindowsnavigationproof؛بغیراجازتغیرمتعلقredesignنہیں |
|23۔ DB/permissiontransaction | حصہ30number/stock/balance/payment/auditrollbackregressions؛CoreTxunchanged | provider/busy/parallel/productionjournal/crossprocess/PC/COMMIT-I/O؛DraftmarkerالگTx |
|24۔ Fault/rollback | حصہ30Purchase18+SR26stagefaults+retry؛2late-linefacts؛stock/books/drawer/snapshot | تمامC#UNRUN؛Sale/customerReturn/otherstock/expense/file/draft/realcommitfaultmatrix |
|25۔ Duplicate/retry | حصہ30localUIlatches/finally،acknowledgedcommitSAVED،postedformsقبلprint/reloadصاف | durableidentityنہیں؛ambiguouscommit/restart/timeout/parallel،Sale/customerReturnpostcommitflows |
|26۔ Encryptedbackup | passwordA .a11bakcode،manual/auto/safety/rollbackintegration،referencechecks | اصلC#service/DPAPI/ACL/passwordrotation/large-photo/interruptionverification |
|27۔ Restore/newPC | stagedvalidation/safety/rollback/explicitlegacycode | نئےPCپرoriginalpassword،wrong/corrupt/interruptedrestore،پراناDB/photosبچنااورstartupثبوت |
|28۔ Files/bounds | متعدددخل/path/size/extension/archivelimits | malformed/largeimage/archive/import،codec/replacement/collision/cleanup/powerloss |
|29۔ Cloud/credentials | بنیادیsync/settingsboundaries/newauditkeyredactionsource | الگtestremoteintegration،networkloss/retry/duplicates/isolation/credentials/restoreconfig |
|30۔ Desktop/WPF | UI/source/bindingchecks | تازہReleasebuild،windows/dialogs/DPI/clipping/scroll/selection/screenshots/logs |
|31۔ Keyboard/scanner | permanentsearch/approvednavigationcode | arrows/Shift-arrows/nativecaret/combo/grid/TabاوراصلUSBlookup/focus |
|32۔ Printer/PDF/camera | receipt/print/PDFcode؛پراناownerprinter-slipproofمحفوظ | تازہreceipts/width/printer/no-printer/scanner/camera/viewerاورfiguresملاپ |
|33۔ Startup/login/lock | approvedstartup/singleinstance/FocusGuard/recoverycode | حقیقیregistry/reboot/re-entry/escape/reset/recovery/shortcuts/taskbar/icon |
|34۔ Guide/actionreconciliation | اردوMD/HTML،PT-50/trace/1–36trackerاور36lessonanchorsاپڈیٹ | اصلappتمامroutes/settings/actionsسےملاپ،Windows/PTsignoffsباقی |
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
