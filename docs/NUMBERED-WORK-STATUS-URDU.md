# نمبر وار کام اور باقی جانچ — مستقل1تا36فہرست

**موجودہ source: v0.107.0 Breakwater / حصہ29۔** یہ وہی36نمبر ہیں جن کی مالک نے تفصیلی وضاحت مانگی۔ اگلے حصوں میں نمبر تبدیل نہیں کریں؛done-source،یہاں کی جانچ،Windowsثبوت اورباقی کام الگ لکھیں۔ **36نمبر،36Next نہیں؛فیچرboardکی فیصد Phase4 safety فیصد نہیں۔**

تازہ محدود کام **23/24/25 CustomerCollect/SupplierPay ledger-pair**: [PT-49](POS-PHASE4-PART29-URDU.md)،[trace](POS-PHASE4-LEDGER-POSTING-INTEGRITY-REVIEW.md)۔32integratedC#cases/acknowledgedcommitUIguards؛categoriesکیfullclosureنہیں۔ Coreمالیcodeunchanged،actualcommitambiguity/requestidentity/othermonetaryroutes/Windows/installerOPEN۔

| مستقل نمبر / دائرہ | کیا ہوگیا؟ | کیا باقی ہے؟ |
|---|---|---|
|1۔ ورژن/GitHub | v0.107Breakwatersource/docs/branding/scriptsaligned؛اسیbranchpublicationلازم | actualcommit/push/remoteverifyآخریرپورٹمیں؛Windowsbuild/installerالگ |
|2۔ C#tests | 2126متوقعcases؛حصہ29میں32نئے(5facts+27inlinecases) | تازہcompile/run/fix/retest؛یہاںسبC#UNRUN |
|3۔ Source/syntheticchecks | 96static/20syntheticSQL/23نئے+49catalog+30recordlexicalnegativeاورPythonbackupreferencePASS | traceمیںحدود؛PythonTx/threadsC#provider/WPFکابدلنہیں |
|4۔ پرانےWindowsfailures | v0.80کے1333PASS/4FAILکیچاروںوجوہ/sourcefixشناخت | تازہsuiteپرretestاورنئیfailuresکیfix؛پرانانتیجہنیاPASSنہیں |
|5۔ Account/role/session | حصہ28تمام16catalog/unitwriterscurrentstoredProductsmanager/audit؛reader capability | حقیقیauthenticatedsessionbinding،remainingcallers؛storedIDخودsessionproofنہیں |
|6۔ Parentrecordcreate/edit/read | حصہ27Customer/Product،حصہ28Catalog/Unitswritesقبلno-op/mutationguard؛oldSQL/rulesبرقرار | باقیparent/workflows/actorlesslookupcallers،versionCAS؛Brandname/linkالگTxہیں |
|7۔ Revocation/staleoutputs | حصہ29Collect/Paycallbackکےبعدfreshaccess،failedreload/postcommitexceptionپرcleanup/SAVED،paymentinputsصاف | دیگرdialogs/cache/inlinequickaddrefreshUX،اصلWindowsfocus/timing/regrant؛instantrecallنہیں |
|8۔ Customdefinitionmanager | **حصہ26:currentownerUI،selection/inputcleanup،selectedsnapshotcomparison،stabletick،SAVED** | WPF/runtimeثبوت؛check/use/atomicversionCASاورothereditorssyncOPEN |
|9۔ Customanswersservice | sharedinternalcurrentactor/storedaudit؛existingparentreads؛historicalinactiveپالیسیبرقرار | باقیcallers،اصلTxexecution،read/loadraces؛internalworkflowgatecaller-owned |
|10۔ Customanswereditor | stalerecordrows/stagedpathcleanup،freshoutputs،SAVED،loadedtimer | اصلbindings/focus/regrant/parentdraftconsistency؛definitionversionsyncالگ |
|11۔ Nativefields | currentOwner/suppliedactivepreflight،failedselectioncleanup،freshriskprompt،stableaccesschecks | اصلWindows/timer/confirmationاورformactionreconciliation |
|12۔ Photos/gallery | currentreviewedreads/writes/parent/path؛stalegallerycleanup؛approvedcover/avatarexceptions | camera/codec/viewer،othercachedcovers/avatar،filefault/collision/cleanup/powerloss |
|13۔ Cost/Profit/main | الگgrants/main/masks؛حصہ27stored-costpatchfreshDBmain+Cost؛authorednewcost/non-costCorepatchبرقرار | باقیroute/output/Windowscombinations؛free-textclassificationنہیں،preflightCASنہیں |
|14۔ Pricing/discount/Extra | approvedbelow-actual-costfloor،sale-onlyExtra،limits/returnBکےsource/tests | تازہruntimeاورcross-page/retryreconciliation؛settledrulesدوبارہنہیںکھولنے |
|15۔ Counter/bill/stock | بنیادیbilling/stock/pricingاورreviewedguards | duplicatecompletion،stockrefusal/cart/numberpreservation،split/credit/change/loyalty/receiptثبوت |
|16۔ Purchase/delivery | بنیادیpurchase/batch/expiry/sampleاورcostvisibilitysource | صحیحvariant/stock/payment/supplierreconciliation،mixedsupplierworkflow،failedposting |
|17۔ Customer/Recovery | حصہ29CollectpostcommitSAVED/localreentry،ledger-pairfault/retry/books/drawerregressions | C#execution،دیگرcommands/receipts/recovery/returnsreconciliationاورdurableduplicateidentity |
|18۔ Supplier/Pay/SR | exceptionBطے؛حصہ29PaypostcommitSAVED/localreentry،fault/retry/books/drawerregressions | C#execution،SRکامکملpostcommitflow/stock/cash/faultreconciliation؛policyدوبارہنہیںپوچھنا |
|19۔ Cashdrawer/handover | حصہ29Collect/Paycash-vs-card-onlineکےderiveddrawerregressions؛Shiftcodeunchanged | actualC#/parallel/fault/duplicate/printresults،handover/closingاورcross-servicecashreconciliation |
|20۔ Expenses/stocktake/writeoff | متعددcost/valuation/history/resultboundaries،frozenSQLprobes | integratedstock/expense/drawer/return/reportreconciliation،rollback،Windowsvouchers |
|21۔ Reports/files/exports | کئیcurrentoutputgates/masks؛editablebulkOwner-defaultwholefilegate | باقیoutputdoors،preparedfiles،realExcel/CSVroundtrip/bounds/formulas/print/PDFملاپ |
|22۔ Blackbox/BookCheck | صرفapprovednavigationgateAudit+Cost+Profit+main؛حالیہinternalsunchanged | تازہWindowsnavigationproof؛بغیراجازتغیرمتعلقredesignنہیں |
|23۔ DB/permissiontransaction | حصہ29ledger-pair32casesمیںsame/separateDatabasecompetingpostsover-settlement؛Coretransactionunchanged | C#provider/busy/parallel،productionjournal/cross-process/PC/COMMIT-I/Oambiguityباقی |
|24۔ Fault/rollback | حصہ29Collect/Pay12BEFORE/AFTERpayment-balance-auditfaultcases+retry،books/payment/drawer ملاپ | C#سبUNRUN؛باقیSale/Purchase/Return/stock/file/commitfaultmatrix |
|25۔ Duplicate/retry | حصہ29localUIreentryfinally،acknowledgedcommitSAVED،fullsettlementretryrefusalvsdistinctpartialrepeatregressions | globalrequesttoken/idempotencyنہیں؛ambiguouscommit/timeout/restart/parallel اوردیگرpostcommitpaths |
|26۔ Encryptedbackup | passwordA .a11bakcode،manual/auto/safety/rollbackintegration،referencechecks | اصلC#service/DPAPI/ACL/passwordrotation/large-photo/interruptionverification |
|27۔ Restore/newPC | stagedvalidation/safety/rollback/explicitlegacycode | نئےPCپرoriginalpassword،wrong/corrupt/interruptedrestore،پراناDB/photosبچنااورstartupثبوت |
|28۔ Files/bounds | متعدددخل/path/size/extension/archivelimits | malformed/largeimage/archive/import،codec/replacement/collision/cleanup/powerloss |
|29۔ Cloud/credentials | بنیادیsync/settingsboundaries/newauditkeyredactionsource | الگtestremoteintegration،networkloss/retry/duplicates/isolation/credentials/restoreconfig |
|30۔ Desktop/WPF | UI/source/bindingchecks | تازہReleasebuild،windows/dialogs/DPI/clipping/scroll/selection/screenshots/logs |
|31۔ Keyboard/scanner | permanentsearch/approvednavigationcode | arrows/Shift-arrows/nativecaret/combo/grid/TabاوراصلUSBlookup/focus |
|32۔ Printer/PDF/camera | receipt/print/PDFcode؛پراناownerprinter-slipproofمحفوظ | تازہreceipts/width/printer/no-printer/scanner/camera/viewerاورfiguresملاپ |
|33۔ Startup/login/lock | approvedstartup/singleinstance/FocusGuard/recoverycode | حقیقیregistry/reboot/re-entry/escape/reset/recovery/shortcuts/taskbar/icon |
|34۔ Guide/actionreconciliation | اردوMD/HTML،PT-49/trace/1–36trackerاور36lessonanchorsاپڈیٹ | اصلappکےتمامroutes/settings/actionsسےملاپ،Windows/PTsignoffsباقی |
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
