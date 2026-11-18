# نمبر وار کام اور باقی جانچ — مستقل1تا36فہرست

**موجودہ source: v0.104.0 Knot / حصہ26۔** یہ وہی36نمبر ہیں جن کی مالک نے تفصیلی وضاحت مانگی۔ اگلے حصوں میں نمبر تبدیل نہیں کریں؛done-source،یہاں کی جانچ،Windowsثبوت اورباقی کام الگ لکھیں۔ **36نمبر،36Next نہیں؛فیچرboardکی فیصد Phase4 safety فیصد نہیں۔**

تازہ محدود کام **نمبر8** پر ہے: [PT-46](POS-PHASE4-PART26-URDU.md)،[trace/ثبوت/حدود](POS-PHASE4-CUSTOM-MANAGER-CURRENT-REVIEW.md)۔ پوری Phase4/installer ابھی OPEN۔ “باقی” میں بعض معلوم gaps ہیں اوربعض صحیح ہونے کی تصدیق؛ہرrowکوثابت شدہbugنہسمجھیں۔

| مستقل نمبر / دائرہ | کیا ہوگیا؟ | کیا باقی ہے؟ |
|---|---|---|
|1۔ ورژن/GitHub | v0.104 source/docs/branding؛ہرحصے کی اسیbranch publication لازم | نئیactualcommit/push/remoteverification آخریرپورٹسے؛Windowsbuild/installer الگ |
|2۔ C#tests | 2009متوقعcasesلکھے؛حصہ26میں32نئے | تازہfullsuitecompile/run/fix/retest؛یہاںسبUNRUN |
|3۔ Source/syntheticchecks | 93static/17syntheticSQL/32source-negativechecksاورPythonbackupreferencePASS؛traceمیںحدود | compiler/provider/WPF/Windowsکابدلنہیں |
|4۔ پرانےWindowsfailures | v0.80کے1333PASS/4FAILکیچاروںوجوہ/sourcefixشناخت | تازہsuiteپرretestاورنئیfailuresکیfix؛پرانانتیجہنیاPASSنہیں |
|5۔ Account/role/session | کئیroutesمیںstoredactive/same-rolechecks | اصلauthenticatedsessionbinding،باقیcallers؛UserIDخودsessionproofنہیں |
|6۔ Parentrecordcreate/edit/read | مخصوصcreator/photo/customboundariesدرست | تمامباقیcommands؛custommapنہہوتوupdatewriterچھوڑسکتاہے؛target/workflowconsistency |
|7۔ Revocation/staleoutputs | کئیreviewedviewsمیںperiodiccheck/cleanup | باقیeditors/cache/dialogs؛Windowsثبوت؛باہرنکلاpaper/fileواپسنہیںمٹتا |
|8۔ Customdefinitionmanager | **حصہ26:currentownerUI،selection/inputcleanup،selectedsnapshotcomparison،stabletick،SAVED** | WPF/runtimeثبوت؛check/use/atomicversionCASاورothereditorssyncOPEN |
|9۔ Customanswersservice | sharedinternalcurrentactor/storedaudit؛existingparentreads؛historicalinactiveپالیسیبرقرار | باقیcallers،اصلTxexecution،read/loadraces؛internalworkflowgatecaller-owned |
|10۔ Customanswereditor | stalerecordrows/stagedpathcleanup،freshoutputs،SAVED،loadedtimer | اصلbindings/focus/regrant/parentdraftconsistency؛definitionversionsyncالگ |
|11۔ Nativefields | currentOwner/suppliedactivepreflight،failedselectioncleanup،freshriskprompt،stableaccesschecks | اصلWindows/timer/confirmationاورformactionreconciliation |
|12۔ Photos/gallery | currentreviewedreads/writes/parent/path؛stalegallerycleanup؛approvedcover/avatarexceptions | camera/codec/viewer،othercachedcovers/avatar،filefault/collision/cleanup/powerloss |
|13۔ Cost/Profit/main | الگgrants/mainاورمتعددraw/aggregate/outputmasks | باقیroute-to-outputreconciliation/Windowscombinations؛free-textکیخودکارclassificationنہیں |
|14۔ Pricing/discount/Extra | approvedbelow-actual-costfloor،sale-onlyExtra،limits/returnBکےsource/tests | تازہruntimeاورcross-page/retryreconciliation؛settledrulesدوبارہنہیںکھولنے |
|15۔ Counter/bill/stock | بنیادیbilling/stock/pricingاورreviewedguards | duplicatecompletion،stockrefusal/cart/numberpreservation،split/credit/change/loyalty/receiptثبوت |
|16۔ Purchase/delivery | بنیادیpurchase/batch/expiry/sampleاورcostvisibilitysource | صحیحvariant/stock/payment/supplierreconciliation،mixedsupplierworkflow،failedposting |
|17۔ Customer/Recovery | reviewedcurrentcollection/slip/read/stalecleanup | باقیcommands،duplicatereceipt،balance/drawer/reportملاپاورcorrection/retry |
|18۔ Supplier/Pay/SR | **exceptionBطے/implemented**؛accountingamountsجائز،directitemcostالگ؛currentreviewedwrites | payment/return/stock/cashreconciliation،duplicate/fault/retry/Windows؛فیصلہدوبارہنہیںپوچھنا |
|19۔ Cashdrawer/handover | reviewedcurrentactors/recipient،unknown-vs-closed،stalecleanup | parallel/fault/duplicate،count/reason/reportاورpostcommitprintfailureWindows |
|20۔ Expenses/stocktake/writeoff | متعددcost/valuation/history/resultboundaries،frozenSQLprobes | integratedstock/expense/drawer/return/reportreconciliation،rollback،Windowsvouchers |
|21۔ Reports/files/exports | کئیcurrentoutputgates/masks؛editablebulkOwner-defaultwholefilegate | باقیoutputdoors،preparedfiles،realExcel/CSVroundtrip/bounds/formulas/print/PDFملاپ |
|22۔ Blackbox/BookCheck | صرفapprovednavigationgateAudit+Cost+Profit+main؛حالیہinternalsunchanged | تازہWindowsnavigationproof؛بغیراجازتغیرمتعلقredesignنہیں |
|23۔ DB/permissiontransaction | connection-ownedTx،currentTxgrantread،failedgrantdeny | cross-instance/processlocking/parallel/busy/retry/concurrency |
|24۔ Fault/rollback | مخصوصregressionsاورsyntheticfaultproof | ہرcriticalstageپرparent/child/stock/payment/audit/file/commit-vs-UIfailurematrix |
|25۔ Duplicate/retry | بعضcommandguardsاورSAVEDmessages | repeatedsubmit/timeout/restart/parallelmonetaryactionsکیintegratedidempotency |
|26۔ Encryptedbackup | passwordA .a11bakcode،manual/auto/safety/rollbackintegration،referencechecks | اصلC#service/DPAPI/ACL/passwordrotation/large-photo/interruptionverification |
|27۔ Restore/newPC | stagedvalidation/safety/rollback/explicitlegacycode | نئےPCپرoriginalpassword،wrong/corrupt/interruptedrestore،پراناDB/photosبچنااورstartupثبوت |
|28۔ Files/bounds | متعدددخل/path/size/extension/archivelimits | malformed/largeimage/archive/import،codec/replacement/collision/cleanup/powerloss |
|29۔ Cloud/credentials | بنیادیsync/settingsboundaries/newauditkeyredactionsource | الگtestremoteintegration،networkloss/retry/duplicates/isolation/credentials/restoreconfig |
|30۔ Desktop/WPF | UI/source/bindingchecks | تازہReleasebuild،windows/dialogs/DPI/clipping/scroll/selection/screenshots/logs |
|31۔ Keyboard/scanner | permanentsearch/approvednavigationcode | arrows/Shift-arrows/nativecaret/combo/grid/TabاوراصلUSBlookup/focus |
|32۔ Printer/PDF/camera | receipt/print/PDFcode؛پراناownerprinter-slipproofمحفوظ | تازہreceipts/width/printer/no-printer/scanner/camera/viewerاورfiguresملاپ |
|33۔ Startup/login/lock | approvedstartup/singleinstance/FocusGuard/recoverycode | حقیقیregistry/reboot/re-entry/escape/reset/recovery/shortcuts/taskbar/icon |
|34۔ Guide/actionreconciliation | اردوMD/HTML/PT/traceاور36lessonanchors؛ابمستقلنمبرtracker | تمامroutes/settings/actionsکاحقیقیappسےملاپ؛historicalnotesکیواضححیثیت،signoffs |
|35۔ Isolatedtestinstallation | verifiedbackup+separateDB/photos/configکیشرطdocumented | اصلVM/sparePC/storageproof،startup/migrationسےshopdataمحفوظ؛exefolderاکیلاکافینہیں |
|36۔ Singleinstaller | مالککیاجازت؛build/installerconfig/scriptsموجود | کامیابWindowsbuild/tests،exeبننا،cleaninstall/upgrade/uninstall/datapreservation/shortcuts/startup،پھرsendablefile؛**ابھینہیںبنا** |

## اگلی رپورٹ کا لازمی انداز

1. انہی نمبروں میں سے کس پرکام ہوا؟
2. اسی نمبر میں sourceمیںکیاکیاہوگیا؟
3. کیاواقعیجانچا،کسماحولمیں،اورکیاصرفUNRUNtestsourceہے؟
4. اسی نمبرکےاندرکیاباقیہےاورکیوں؟
5. اگلےنمبروںکیترتیب،docs/GitHubکیاصلحیثیت،installerکےباقیدروازے۔

موجودہترتیب: **5/6/7** کےباقیcurrentrecord/sessioncallers،پھر **23–25** integratedfault/retry/concurrency،**26–29**files/restore/cloud،**2/4/30–35**Windows/results/hardware/isolation،آخر **36**۔ یہلچکدارترتیبہے؛dependencyیاحقیقیfindingسےتبدیلہوسکتیہے۔3–6مزیدمربوطreview/resultچکرمشروطاندازہ،نہdeadlineنہupperbound۔

اصلshopپرsyntheticpostsنہیں؛secret/datachatمیںنہیں؛ہرportiondocs+sameactivebranchcommit/push+actualremoteverification۔ Static/Python/featureboardکوproduction/runtime/securityPASSنہکہیں۔
