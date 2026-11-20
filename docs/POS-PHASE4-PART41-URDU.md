# ⏳ فیز4 — حصہ41: تصویر کی owned copy،file fault اور محتاط cleanup

**⏳ v0.119.0 Moraine · 2026-11-10 · PT-61 · مستقل12،23–25،28؛source موجود،اصل Windows/.NET ثبوت باقی۔**

✅ اصول:✅صرف متعلقہ کام100%مکمل؛⏳کچھ باقی؛❌بالکل شروع نہیں۔ Source check یا Python probe پوری feature کی عملی سند نہیں۔

## ⏳ روزمرہ مثال

⏳ دوا کی تصویر نقل ہوگئی،database نے شاید save بھی کردی،مگرجواب آتےوقت connection خراب ہوگیا۔ پرانا error handler ہر failure پرcopy حذف کر سکتا تھا؛اگرrowsaveتھی توgalleryمیںmissingphotoبنتی۔ ابunknownمیںcopyرہتیہے،log/پیغامverificationکہتےہیں۔ دوسریطرفcopyکےدرمیانread/sizefaultہوتوownedpartialpathفوراًمعلوم،confirmedrollbackپراسیفائلکاcleanupہوسکتاہے۔

## ⏳ کس نمبر پر کیا تبدیلی ہوئی؟

- ⏳ **12/28 — file ownership:** timestamp کے ساتھGUID،`CreateNew`؛collisionپرنہoverwriteنہپرانیفائلdelete۔ Outputبنتےہیownedpathassign،پھر64KiBstreamcopy؛12MBسےزیادہیاemptyہونےپرrefusal،successfulcopyپرflush۔ اصلsourceنہmoveنہdelete۔ یہextension/sizeحدہے،imagecodec/corruptimage/symlink/hostilefilesystemکیجامعsecurityسندنہیں۔
- ⏳ **23/24 — transaction evidence:** GalleryAddاورProduct/Customer/Suppliercreationاپنیexistingtransactionکاالگ`TransactionOutcome`مشاہدہکرتےہیں۔ صرفworkشروعنہہویاCOMMITattemptسےپہلےconfirmedrollbackہوتوownedcopydelete؛COMMITattemptیاunconfirmedrollbackمیںretain/log۔ ErrorhandlerنیاPhotoServiceبناکرفولڈربنانےکیکوششنہیںکرتا۔ DeleteخودI/O/permissionسےناکامہوسکتاہے؛logمیںmanualverification،خودکارorphanpurgeنہیں۔
- ⏳ **25 — galleryAddretry:** servicecallسےپہلےwindow-latch؛explicitno-commitہیfailureپرrelease۔ Successackکےبعدreload/previewخرابہوتو**SAVED**،وہیAddدوبارہنہیں۔ Refreshیاpermissionregrantلَیچنہیںکھولتے۔ معمولکاکامیابAddاورمکملrefreshاگلیتصویرکیاجازتدیتےہیں۔ Unknownمیںپہلےrecord/gallery/photosجانچپھرwindowreopen؛reopenخودکارverificationنہیں۔
- ⏳ **6/25 — محدودparentدائرہ:** تینcreatorsکیstagedcopycleanupدرستکی؛تمامparentformsکےretry/duplicateguardsنہیںبنے۔ انکاunknownپیغامبھیپہلےrecord/galleryverifyکہتاہے۔ GalleryHide/Restore/CoverکےCOMMIT/retrypathsاسحصےمیںنہیںبدلے۔ RecoveryServiceکےمالیrecoverypostingpathsاگلےreviewمیںباقی،اسحصےنےانہیںcertifyنہیںکیا۔

## ✅ محفوظ پرانی پالیسی

- ✅ تینوںcreatorsاورPhotoServiceکےSQLstrings،permission/currentactor/auditchecksبرقرار؛کوئیmigrationیامالیفارمولہنہیںبدلا۔ Pricing/Extra/ReturnB/SupplierB،sharedDraft/Park/heldResume،Blackbox/BookCheck،backup/cloudاورRecoveryServiceunchanged۔
- ✅ پرانےtestsکےassertionsنہیںبدلے؛نئےinternalhelpersکیلئےtestassemblyaccess،publicproductionAPIنہیںبڑھائی۔ 36guideanchorsاورمستقل1–36نمبرقائم۔

## ✅ یہاں کیا واقعی چلا؟ / ⏳ کیا نہیں چلا؟

- ✅ نیاPythonreferenceprobe:17filecases(4size/buffer،3copyfault،collision،missingdirectory،8observedstates)،37migrations،اصلphotoINSERTکے3faultrollbackplans،1syntheticlost-ackretention۔ **Pythoncopy/outcome/transactionplanخودالگmodelہے؛C#helperیاactualproviderCOMMITfaultنہیں۔**
- ✅ نیاsource-negativeprobe:23lexicalmutationsرد،اصلbytesبحال۔ یہorder/guardtokensکیجانچ،semantics/runtimeکیضمانتنہیں۔
- ⏳ **28نئےC#cases؛پوریsuite2916UNRUN:**8outcomestates،4size/bufferboundaries،collision/missingdirectory،3partial/empty/growthfaults،2null/emptypaths،1cleanupI/Ofault،4routesکےauditrollback،4routesکےsuccess۔ SyntheticoutcomeflagsactualCOMMITfailureنہیں؛cleanupdirectoryfaultWindowsACLproofنہیں۔ یہاں.NETSDK/Windowsنہیں؛compile/run/fix/retestباقی۔
- ✅ آخریlocalrun:108staticchecks،433C#grammarfiles،4803calls/9854refsمیں0staticissues؛15source-negativeprobes/428mutationsرد/restore،31SQLiteprobesPASS؛backupreference6roundtrips/24negativesاورrulesmatrix/diffcheckPASS۔799پرانےpaths،141oldtestpaths،37migrations،156otherCorefilesاور52SQLblocksبرقرار۔ ⏳ تفصیلیحدود[trace](POS-PHASE4-PHOTO-FILE-FAULT-REVIEW.md)میںدرج؛وہبھیWindows/securitycertificationنہیں۔

## ⏳ PT-61 — اصل الگ test ماحول میں

**⏳ پہلےverifiedencryptedbackupاورواقعیالگDB/photos/config؛صرفexeکاالگfolderکافینہیں۔ اصلshopپرفرضیrecords/faultsنہیں۔**

1. ⏳ **2/4:** v0.119.0 `tools\RUN-TESTS.cmd`؛2916expectedcasesکاbuild/TRX/test.log،failuresکیfix/retest۔ پچھلاgreenاسbuildکاPASSنہیں۔
2. ⏳ **12/28:** filecollision،empty/growing/read-faultsource،64KiBboundary/12MBexact/overlimit؛outputownershipدرست،original/unrelatedpictureبرقرار۔ Copyکےدورانunplug/read-only/disk-full/flushfailureاورlockedfileکےاصلنتائج/logsمحفوظکریں۔
3. ⏳ **23/24:** Galleryاور3parentcreatorsمیںphotoINSERTیاauditfail؛confirmedrollbackمیںparent/photo/auditrowواپس،صرفنئیownedcopyہٹے۔ Faileddeleteمیںoperationکااصلنتیجہنہچھپے،copy/logرہیں؛cleanupconstructorکےبغیربھیچلے۔
4. ⏳ **23/25:** actualproviderCOMMITacklost/rollbackfailure/connectiondisposefailureاورpowercutصرفdisposableWindowsDBپر۔ Unknownمیںreferencedfileنہہٹے؛record/gallery/folderملاکردوبارہکامجاری۔ Syntheticflagsانfaultsکابدلیثبوتنہیں۔
5. ⏳ **25/30:** Addcallthrow،unknown،successfulcommitکےبعدreload/previewthrow؛اسیwindowمیںAddFileاورbuttonدونوںدوبارہAddنہکریں۔ Refresh/revoke/regrantلَیچنہکھولیں؛SAVEDواضح۔ Normalvalidationیاconfirmedrollbackکےبعدretryاورnormalfullsuccessکےبعداگلیتصویرممکن۔ اصلWPFscreenshots/bindings/focus/codec/cameraجانچیں۔
6. ⏳ **6/25:** Product/Customer/Supplierunknowncreationپرپہلےموجودrecord/galleryverify؛blindSaveنہیں۔ انformsکیglobalduplicate/restart/parallelحفاظتالگباقی۔ دوسرےgalleryactionsبھیالگreviewباقی۔

## ⏳ باقی منصوبہ

- ⏳ [مستقل1–36تفصیل](NUMBERED-WORK-STATUS-URDU.md)قائم؛اگلاboundedreviewremainingrecoveryfinancialfault/retryاورparentUIoutcomes،پھردurableidentity/session/CAS/provider؛نئیpolicyہوتوپہلےمثال/منظوری۔
- ⏳ 26–29restore/newPC/cloud/files؛2/4/30–35Windows/build/results/hardware/isolation۔ ❌36sendableinstallerbinaryنہیںبنی؛⏳FullPhase4OPEN۔
- ⏳ ہرportiondocs+اسیactivebranchcommit/push+الگactualremoteverification۔36categories،36Nextنہیں؛اندازہdeadline/upperboundنہیں۔
