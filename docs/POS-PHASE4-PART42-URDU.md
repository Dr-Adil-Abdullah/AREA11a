# ⏳ فیز4 — حصہ42: وصولی/ادائیگی کا outcome اور محتاط retry

**⏳ v0.120.0 Shale · 2026-11-10 · PT-62 · مستقل17–19،23–25؛source موجود،اصل Windows/.NET ثبوت باقی۔**

✅ اصول:✅صرف متعلقہ کام100%مکمل؛⏳کچھ باقی؛❌بالکل شروع نہیں۔ Static/Python کاPASSپوریfeatureکیعملیسندنہیں۔

## ⏳ روزمرہ مثال

⏳ گاہکنے300روپےدیے،payment/balanceشایدمحفوظہوگئےمگرCOMMITکاجوابنہیںملا۔ پہلےgenericerrorمیں“Nothing was changed”آسکتاتھا؛پھرReceiveدبانےسےدوسریpartialpaymentبنسکتیہے۔ ابCoreunknownمیںایسادعویٰنہیںکرتا،اوراسیpageکاactionجانچتکرکتاہے۔ اگراسکےبرعکسrollbackواقعیواپسآیاتھاتومعلومno-commitپرlocalretryممکن۔

## ⏳ کس نمبر میں کیا بدلا؟

- ⏳ **17/18/23 — CoreCollect/Pay:** CustomerLedger.CollectاورSupplierLedger.PayاپنیexistingTxکاالگTransactionOutcomeمشاہدہکرتےہیں۔ Amount/methodpreflightدوواضحno-writefailures؛Validation/دیگرcatchمیںصرفمشاہدہشدہNoCommitConfirmed۔ UnknownCOMMITیاunconfirmedrollbackمیں**history،کھاتہ،drawerverify**؛blanketNothingchangedنہیں۔
- ⏳ **17/18/25 — تینUIactions:** CustomersCollect،RecoveryReceiveاورSuppliersPayپرموجودہPostingAttemptGuard؛serviceسےپہلےbegin،failureپرصرفexplicitno-commitrelease؛successackکسیUIcallback/printingسےپہلے۔ UnknownیاSAVEDکےبعدادھوریrefresh/cleanupپراسیpageمیںدوبارہpostingبند؛Refresh/regrantguardنہیںکھولتے۔
- ⏳ **17/25 — consumedinput:** Successکےبعدamount/noteصاف؛reload/selectionکیautomaticremaining-balancefillبھیconsume۔ Normalfullcompletionپراگلیواقعیالگpartialpaymentتازہamountسےجائز۔ Recoveryمیںنئےcustomerکیابتدائیselectionپرباقیرقمکیپرانیآسانیقائم؛successfulReceiveکےبعدصاف۔
- ⏳ **19/24 — accountingfaults:** نئےC#testsکےsourceمیںpayment/balance/auditکےBEFORE/AFTERfaults،unchangedbooks/drawerاورایکlocalretry۔ ActualproviderCOMMITfault،WPFاورpowercutابھیثابتنہیں۔ RecoveryServiceکیageing/report/slipcodeنہیںبدلی؛دونوںcollectionpagesایکexistingCollectserviceاستعمالکرتےہیں۔

## ✅ کیا نہیں بدلا؟

- ✅ دونوںCoretransactioncallbackbodiesاورتمامSQLstringsبرقرار؛settlement/overpayment/currentactor/stored-audit/financialpermissionsنہیںبدلے۔ SupplierBکےاجازتشدہwhole-accountPayپرنئیCost/mainشرطنہیں۔
- ✅ کوئیamount-baseddedup،نیاdraft،نئیfinancialpolicyیامالیformula/migrationنہیں۔ واقعیالگدوpartialpaymentsجائز؛balanceguardglobalidempotencyنہیں۔ Sale/Return/Extra،sharedDraft/Park/heldResume،Blackbox/BookCheck،P41photo/parentcleanupاورbackup/cloudunchanged۔
- ✅ پرانےtestsکےassertionsبرقرار؛نئے47casesالگ۔ اردوguideکے36lessonanchorsاورمستقل1–36نمبرقائم۔

## ✅ مقامی شواہد / ⏳ اصل execution

- ✅ نیاSQLiteprobe:37migrations،اصلunchangedledgerpayment/balance/auditSQLکے12faultrollback/retryplans؛6syntheticlost-ack/refresh/normal/reopenboundarycases۔ Pythonخودplan/transaction/guardچلاتاہے؛C#service/NoCommitConfirmed/WPFیاactualCOMMITfaultنہیں۔
- ✅ نیاsource-negativeprobe:31lexicalmutationsرد،اصلbytesبحال۔ Token/orderproofہے،semanticruntimeکیضمانتنہیں۔
- ⏳ **47نئےC#cases؛پوری2963suiteUNRUN:**12stagefault/retry،14definiterefusals،6cash/card/onlinelegitimatepartialrepeat،15syntheticguardobservations۔15guardrowsمیںpageکےنامتینconsumerlabelہیں،تیناصلWPFpagesنہیںچلے۔ یہاں.NETSDK/Windowsنہیں؛compile/run/fix/retestباقی۔
- ✅ آخریlocalrun:109staticchecks،434C#grammarfiles،4810calls/9870refsمیں0staticissues؛16source-negativescripts/459mutationsرد/restore،32SQLiteprobesPASS۔ P41filereference،backupreference6roundtrips/24negatives،rulesmatrix/diffcheckPASS۔805پرانےpaths،142oldtestpaths،37migrations،158otherCorefiles،دونوںTxcallbackbodiesاور32SQLliteralsبرقرار۔ ⏳ حدود[trace](POS-PHASE4-LEDGER-OUTCOME-REVIEW.md)میں؛یہبھیprovider/securitycertificationنہیں۔

## ⏳ PT-62 — واقعی الگ Windows test ماحول میں

**⏳ پہلےverifiedencryptedbackupاورواقعیالگDB/photos/config؛صرفexeکاالگfolderکافینہیں۔ اصلshopپرفرضیpayment/faultنہیں۔**

1. ⏳ **2/4:** v0.120.0 `tools\RUN-TESTS.cmd`؛2963expectedcasesکےbuild/TRX/test.log،failuresکیfix/retest۔ پراناgreenنیاPASSنہیں۔
2. ⏳ **17/18/19/24:** دونوںsidesپرpaymentINSERT،balanceUPDATE،auditINSERTکےBEFORE/AFTERfaults؛confirmedrollbackمیںno-committrue،balance/payment/audit/stock/drawerبرقرار؛faultہٹانےپرایکretryاورbooks/drawerملیں۔ Methodcash/online/cardکےاثراتملائیں۔
3. ⏳ **23/25:** actualproviderCOMMIT-acklost،rollback/disposefailureصرفdisposableDBپرinject؛unknownنتیجےمیںNoCommitfalse،“Nothingchanged”نہیں۔ History/account/drawerکوactualpersistentstateسےملائیں۔ Syntheticflagsانfaultsکابدلیثبوتنہیں۔
4. ⏳ **17/18/25/30:** تینوںactionsمیںnestedcallback/reentry،servicecallthrow،successfulcommitکےبعدreload/callback/receiptthrow؛ackسےپہلےunknown،ackکےبعدSAVED؛اسیpageمیںدوبارہpostingبند۔ Refresh،selectionchange،permissionrevoke/regrantguardنہکھولیں۔ اصلbindings/focus/screenshots/logsلازم۔
5. ⏳ **17/25:** Recoverysuccessکےبعدremainingbalanceautofillصاف،amount/noteخالی؛نئےcustomerکیinitialselectionپرباقیرقمfillبرقرار۔ Knownvalidation/confirmedrollbackپردرستretry؛normalfullsuccessکےبعدواقعیالگpartialpaymentنئیamountسےچلے۔ PrinterreturnedfailureپرنئیcollectionکےبجائےاسیPAYکیreprint۔
6. ⏳ **18/13:** SupplierBکےمطابقCost/mainOFFمیںجائزPay،currentSuppliersgrantلازم؛کوئینئیcostشرطنہیں۔ Customergrants/storedactor/overpaymentقواعدبرقرار۔
7. ⏳ **25:** CustomersسےRecovery،نیاpage،restartیادوسراPCاپناlocalguardرکھتےہیں؛unknownمیںانراستوںسےدوبارہpaymentنہکریں۔ پہلےجانچپھرreopen؛یہخودکارverificationیاdurablerequestkeyنہیں۔ Parentcreationformsاورباقیgalleryactionsالگباقی۔

## ⏳ باقی ترتیب

- ⏳ [مستقل1–36تفصیل](NUMBERED-WORK-STATUS-URDU.md)قائم۔ اگلاboundedreviewparentcreationUIoutcomes/remainingfinancialcallers؛session/CAS/durableidentity/provider/parallelاور26–29restore/cloud/filesباقی۔ نئیpolicyہوتوپہلےمثال/منظوری۔
- ⏳ 2/4/30–35Windows/build/results/hardware/isolation؛FullPhase4OPEN۔ ❌36sendableinstallerbinaryنہیںبنی۔
- ⏳ ہرportiondocs+اسیactivebranchcommit/push+الگactualremoteverification۔36categories≠36Nexts؛اندازہdeadline/upperboundنہیں۔
