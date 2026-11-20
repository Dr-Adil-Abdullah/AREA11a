# نمبر وار کام اور باقی جانچ — مستقل1تا36فہرست

**⏳ موجودہ source: v0.121.0 Hawthorn؛نئے14حصوںکےمنصوبےکاصرفحصہ3منظورشدہپیشرفت۔** [⏳ منصوبہ](REMAINING-14-PORTIONS-URDU.md)؛[⏳ Saveجائزہ/PT-63](PARENT-RECORD-SAVE-OUTCOME-REVIEW-URDU.md)۔ مستقل1–36نمبرنہیںبدلے۔

✅ حصہ1کیCore/Testscompileتصدیقملی؛v0.120.1کے2963میں2941PASS/22FAIL۔ ⏳حصہ2کے22fixesاورحصہ3کے72newtestsکانیاruntimeباقی؛3035suiteیہاںUNRUN۔ حصہ4شروعنہیں؛❌installerنہیںبنی۔

| مستقل نمبر / دائرہ | کیا ہوگیا؟ | کیا باقی ہے؟ |
|---|---|---|
|1۔ ⏳ ورژن/GitHub | v0.121.0Hawthorn/source/docs/icon/scriptsaligned | samebranchcommit/push/actualremoteverificationآخریرپورٹمیں |
|2۔ ⏳ C#tests | 3035متوقعcases؛حصہ3میں72نئےinlinecases | اصلv0.120.1run2963/2941PASS/22FAIL؛v0.121.0کی3035suiteیہاںUNRUN |
|3۔ ⏳ Source/syntheticchecks | 112staticcontracts؛تازہ45Save-negativechecksاور30SQLfaults/1brandpartialprobe؛حتمینتائجtraceمیں | یہC#/WPF/provider/securitycertificationنہیں |
|4۔ ⏳ پرانےWindowsfailures | v0.120.1کے2941PASS/22FAILکیوجوہ/sourcefixtureاصلاح؛v0.80تاریخی | v0.121.0پراصلretestاورremainingfailuresکیfix؛پرانانتیجہنیاPASSنہیں |
|5۔ ⏳ Account/role/session | PRابcurrentstoredactive/sameparsedrole/Returns؛validOwnerبھیcurrentrow؛storedauditactor | authenticatedsessiontoken/same-role-IDimpersonation،remainingcallers؛یہfullsessionproofنہیں |
|6۔ ⏳ Parentrecordcreate/edit/read | Parent/quick-create/Catalog/Unit SaveپرobservedTx/unknownguard/ackقبلcleanup؛پرانےcurrentwrite/readchecksبرقرار | اصلparentWPF،باقیcallers/session،atomicCAS؛legacy/otherworkflowreview |
|7۔ ⏳ Revocation/staleoutputs | PRaction/nominal3stimerdenialپرفارم/rows/tray/lookupصاف؛stableinputsبرقرار،regrantfreshLoad؛detachedrowrefused | actualWPF،fullPOScart/otherpreparedoutputs/instantrecall/CASباقی |
|8۔ ⏳ Customdefinitionmanager | **حصہ26:currentownerUI،selection/inputcleanup،selectedsnapshotcomparison،stabletick،SAVED** | WPF/runtimeثبوت؛check/use/atomicversionCASاورothereditorssyncOPEN |
|9۔ ⏳ Customanswersservice | sharedinternalcurrentactor/storedaudit؛existingparentreads؛historicalinactiveپالیسیبرقرار | باقیcallers،اصلTxexecution،read/loadraces؛internalworkflowgatecaller-owned |
|10۔ ⏳ Customanswereditor | stalerecordrows/stagedpathcleanup،freshoutputs،SAVED،loadedtimer | اصلbindings/focus/regrant/parentdraftconsistency؛definitionversionsyncالگ |
|11۔ ⏳ Nativefields | currentOwner/suppliedactivepreflight،failedselectioncleanup،freshriskprompt،stableaccesschecks | اصلWindows/timer/confirmationاورformactionreconciliation |
|12۔ ⏳ Photos/gallery | exclusiveGUID/boundedownedcopy؛partialtracking؛unknownretain،galleryAddlocalguard؛currentaccessبرقرار | اصلC#/WPF/codec/camera؛providerCOMMIT/diskfull/ACL/powerloss؛othergalleryactions |
|13۔ ⏳ Cost/Profit/main | الگgrants/main/masks؛حصہ27stored-costpatchfreshDBmain+Cost؛authorednewcost/non-costCorepatchبرقرار | باقیroute/output/Windowscombinations؛free-textclassificationنہیں،preflightCASنہیں |
|14۔ ⏳ Pricing/discount/Extra | approvedbelow-actual-costfloor،sale-onlyExtra،limits/returnBکےsource/tests | تازہruntimeاورcross-page/retryreconciliation؛settledrulesدوبارہنہیںکھولنے |
|15۔ ⏳ Counter/bill/stock | عامCompleteسےپہلےsharedDraftsave/link؛sameIDpost؛all draftHoldrefused؛Holdknown/unknown/cleanupguards | اصلWPF/restart/powercut،fullPOS/session/scanner،ordinaryHolddurableintent |
|16۔ ⏳ Purchase/delivery | عامSaveسےپہلےsharedDraft؛Purchasepartialcleanup/reconstructionguards؛sameIDretry | اصلC#/WPF/mixedsupplier/providerCOMMIT؛دیگرCoreCreatecallers/globalidentity |
|17۔ ⏳ Customer/Recovery | CollectobservedTx/Customer+Recoverylocalguards/ackbeforecallbacks؛PRcurrent/ReturnBبرقرار | actualC#/WPF/provider؛recoveryread/slipunchanged؛cross-page/globalidentity/session/remainingcallers |
|18۔ ⏳ Supplier/Pay/SR | SupplierBبرقرار؛PayobservedTx/localunknown+ackcleanupguard؛SRپرانےtestsبرقرار | اصلC#/WPFstock/books/cash/results؛ambiguouscommit/durableidentity؛policyدوبارہنہیںپوچھنا |
|19۔ ⏳ Cashdrawer/handover | Collect/Pay12fault+6method/partialC#sourceودرجdrawerreconciliation؛PRCancelcash-inنہیں | اصلprovider/race/C#/WPF/handover/closing/print/cashreconciliation |
|20۔⏳ Expenses/stocktake/writeoff | expense/cash+goodsdrawing/write-off/Applylocalguard؛ackپہلے،cleanupبعد؛oldApplysessionconsume،financialSQL/helpersunchanged | actualC#/WPF،integratedreports/legacybatch/costfallback،Open/Count/Cancel/foundownfaultpaths |
|21۔ ⏳ Reports/files/exports | PRcompleteauthorizedheader+linesreceipt؛linefailureپرBuildfailure؛receiptComposeunchanged؛Historycurrentbulkپہلے | alreadyprepared/printed/exportedoutputsnotrecalled؛باقیExcel/CSV/PDF/print/Windowsroutes |
|22۔ ⏳ Blackbox/BookCheck | صرفapprovednavigationgateAudit+Cost+Profit+main؛حالیہinternalsunchanged | تازہWindowsnavigationproof؛بغیراجازتغیرمتعلقredesignنہیں |
|23۔ ⏳ DB/permissiontransaction | Collect/PayexistingTxobservedoutcomes؛storedactor/currentaccessاورfinancialcallbackbodiesبرقرار | session/same-role-ID/CAS/externalcoherentsnapshot/realCOMMIT/parallel/durableidentityباقی |
|24۔ ⏳ Fault/rollback | 72نئےparentSaveC#cases؛32auditfaults/16validation/16post-ack/8guards؛پرانےtestsبرقرار | تمام3035C#UNRUN؛providerCOMMIT/rollback/dispose/WPFcallbacks/remainingcallers/files |
|25۔ ⏳ Duplicate/retry | Parent/quick-create/Catalog/UnitSavelocalguards؛unknown/ackcleanupRefresh/regrantسےresetنہیں؛پرانےledger/draftguardsبرقرار | اصلparentWPFretry/othergalleryactions/cross-page/pageReopen/restart/globalidentity/parallel/providerCOMMITباقی؛localflagdurablekeyنہیں |
|26۔ ⏳ Encryptedbackup | passwordA .a11bakcode،manual/auto/safety/rollbackintegration،referencechecks | اصلC#service/DPAPI/ACL/passwordrotation/large-photo/interruptionverification |
|27۔ ⏳ Restore/newPC | stagedvalidation/safety/rollback/explicitlegacycode | نئےPCپرoriginalpassword،wrong/corrupt/interruptedrestore،پراناDB/photosبچنااورstartupثبوت |
|28۔ ⏳ Files/bounds | photoexclusive12MBstreambound/partialoutput/confirmedcleanup؛پرانےpath/archivelimits | malformed/largeimage/archive/import،codec/replacement/collision/cleanup/powerloss |
|29۔ ⏳ Cloud/credentials | بنیادیsync/settingsboundaries/newauditkeyredactionsource | الگtestremoteintegration،networkloss/retry/duplicates/isolation/credentials/restoreconfig |
|30۔ ⏳ Desktop/WPF | UI/source/bindingchecks | تازہReleasebuild،windows/dialogs/DPI/clipping/scroll/selection/screenshots/logs |
|31۔ ⏳ Keyboard/scanner | permanentsearch/approvednavigationcode | arrows/Shift-arrows/nativecaret/combo/grid/TabاوراصلUSBlookup/focus |
|32۔ ⏳ Printer/PDF/camera | receipt/print/PDFcode؛پراناownerprinter-slipproofمحفوظ | تازہreceipts/width/printer/no-printer/scanner/camera/viewerاورfiguresملاپ |
|33۔ ⏳ Startup/login/lock | approvedstartup/singleinstance/FocusGuard/recoverycode | حقیقیregistry/reboot/re-entry/escape/reset/recovery/shortcuts/taskbar/icon |
|34۔ ⏳ Guide/actionreconciliation | اردوMD/HTML،PT-62/trace/1–36trackerاور36lessonanchorsاپڈیٹ | اصلappتمامroutes/settings/actionsسےملاپ،Windows/PTsignoffsباقی |
|35۔ ⏳ Isolatedtestinstallation | verifiedbackup+separateDB/photos/configکیشرطdocumented | اصلVM/sparePC/storageproof،startup/migrationسےshopdataمحفوظ؛exefolderاکیلاکافینہیں |
|36۔ ❌ Singleinstaller | مالککیاجازت؛build/installerconfig/scriptsموجود | کامیابWindowsbuild/tests،exeبننا،cleaninstall/upgrade/uninstall/datapreservation/shortcuts/startup،پھرsendablefile؛**ابھینہیںبنا** |

## اگلی رپورٹ کا لازمی انداز

1. انہی نمبروں میں سے کس پرکام ہوا؟
2. اسی نمبر میں sourceمیںکیاکیاہوگیا؟
3. کیاواقعیجانچا،کسماحولمیں،اورکیاصرفUNRUNtestsourceہے؟
4. اسی نمبرکےاندرکیاباقیہےاورکیوں؟
5. اگلےنمبروںکیترتیب،docs/GitHubکیاصلحیثیت،installerکےباقیدروازے۔

موجودہترتیب: [⏳ نیا14حصوںکامنظورشدہمنصوبہ](REMAINING-14-PORTIONS-URDU.md)۔ حصہ1کیمتعلقہcompileتصدیقملی؛حصہ2کیاصلrerunباقی؛نئےnextسےصرفحصہ3کیاجازتملی،اسکیاصلWPF/.NETجانچبھیباقی؛حصہ4شروعنہیں۔ پرانا3–6چکروںکااندازہابکامشروعکرنےکیہدایتنہیں۔

اصلshopپرsyntheticpostsنہیں؛secret/datachatمیںنہیں؛ہرportiondocs+sameactivebranchcommit/push+actualremoteverification۔ Static/Python/featureboardکوproduction/runtime/securityPASSنہکہیں۔
