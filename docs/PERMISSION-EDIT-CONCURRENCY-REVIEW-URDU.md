# ⏳ 5.1 جاری — Staff Permissions فارم،checkbox اورReset — v0.130.0 Hellebore

## ⏳ اجازت اور حد

تازہ `next` پر اسی **5.1 Settings** کا Staff permissions راستہ لیا۔ [پچھلا Workbook/PT-71](WORKBOOK-SETTINGS-CONCURRENCY-REVIEW-URDU.md) اور پہلے checkpoints اپنی حدود کے ساتھ برقرار ہیں۔ **5.1 ابھی مکمل نہیں؛حصوں1 تا5 کے باقی کام اور ضروری تصدیق کے بغیر6 شروع نہیں ہوگا۔** Installer نہیں بنایا۔

ابتدا میں مقامیHEADپرانےbase805626eپر،مگرtracked/untrackedفائلیںالگtemporaryindexسےactualremote08dcbd2کےعینبرابرملیں۔ FetchکےبعداسیبرانچکیGitنشان دہی **mixed reset** سےاسیpublishedcommitپرملائی؛فائلوںکاموادنہیںبدلا،دوسریبرانچنہیںلی،کسیغیرمتعلقکامکوحذفنہیںکیا۔

## ⏳ Source اصلاح

### مجاز مربوط permission editor

- نیا `PermissionService.LoadEditForm(actor,targetId)` ایک **Snapshot** میںcurrentOwner/session،موجودہtargetrole،تمامstoredrows،roledefaultsاورbaselineپڑھتاہے۔ Readfailureکاexplicitfailureہے؛UIاسےکامیابall-defaultیاall-denyeditorنہیںکہتی۔
- `PermissionEditForm` میںtargetid،storedparsedroleاورimmutablevaluesہیں۔ TokenJSONignoredہے؛resultکاPermissionEditبھیJSONignored۔
- صرفcallerکےpickerمیںلکھےroleسےOwnerdefaultsنہیںبنتے۔ UIکیroweditabilityبھیاسیloadedstoredroleسےہے؛oldpickerroleکوpermissiongrantنہیںکہتے۔ InactivepersonکوOwnerاپنےfreshformسےpermissionsدیکھ/بدلسکتاہے،جیساپہلےتھا؛نئیاactive-onlyپالیسی نہیں۔
- پراناactorful `MapFor` compatibilitydoorابexplicitformreaderکیvaluesیاfailureپرall-falseدیتاہے؛DesktopابdirectambiguousMapForکیبجائےexplicitresultاستعمالکرتاہے۔ Sessions-disabledactorlessMapForبھیstoredtarget/snapshotپڑھتاہے؛DBreadfailureپرroledefaultallowنہیںبناتا۔ **یہactorlesslegacyhostکومستندloginنہیںبناتا۔** عام`Can`authorizationراستہاسcheckpointمیںنہیںبدلا۔

### مکمل متعلقہ حالت،write-lock کے اندر

نئے `permissions` baselineمیں:

- targetکاexactpersisted `id,role,active`؛
- targetکیتمام `module_key,allowed` rows،keyکےمستقلorderمیں۔ Missingrowاورویسیہیexplicitdefaultrowالگstatesہیں۔ Unknown/futuremodulerowبھیشاملہے،کیونکہResetاسےبھیمٹاتاہے۔

`updated_at/updated_by` اسcontentbaselineمیںنہیں؛timestampکییکساںvalueیاclock پرانحصارنہیں۔ Role/activechangeیاکسیبھیmoduleکیconcurrentchangeپوریloadedlistکوstaleکرتیہے۔ یہخودکارmergeیاصرفclickedmoduleکیCASنہیں۔

CheckedSetاورcheckedResetدونوںcurrentOwnerاورtargetکےپرانےbusinessgatesکےبعد،**پہلےUPSERT/DELETEسےپہلے**baselineملاتےہیں۔ Grant/Resetاورمتعلقہauditایکtransactionمیں؛auditcurrentstoredOwnerسے۔ کسیبھیconflictپرپرانیlistسےنئیgrantنہپلٹے،نہResetاسےخاموشیسےdeleteکرے۔

Successمیںاسیtransactionکی **exact نئی immutablemap/token** ملتےہیں۔ Setکےبعداگلاcheckboxاسیtokenسے؛Resetکےبعدroledefaultsاسیresultسےدکھتےہیں۔ Post-save `LoadPermissions()` یاfreshreadسےدوسرےwriterکاکاماپنیbaselineمیںنہیںملاتے۔

### پرانے imperative APIs جان بوجھ کر الگ

یہاںایکاہمcompatibilityحدہے:

- `Set(actor,id,module,allowed)` اور`ResetToRoleDefaults(actor,id)` **پرانےimperativeintent**ہیں؛currentOwner/businesschecksرکھتےہیں،مگرloadedstatebaselineنہیںمانگتے۔ Enabledاورdisabledدونوںhostsکےexistingcallers/testsکایہbehaviorبرقرار۔
- `Set(...,expected)` اور`ResetToRoleDefaults(...,expected)` **checkedoverloads**ہیں۔ Explicitnullبھیreject،چاہےSessionsdisabledہوں۔ Wrongscope/target/Databaseیاstaletokenreject۔
- Desktopکےloadedpermissioncheckbox/Resetابصرفcheckedoverloadsبلاتےہیں۔ **تمامwritersیاہرSetcallابprotectedہے،یہدعویٰنہیں۔** باقیimperativecallersاوردیگرStaffwritesکیinventoryابھیباقیہے۔

OwnerکےnonfinancialmodulesاببھیبنداورOwnerpermissionResetنہیںہوسکتا؛Ownerکے **View Profit/View Purchase Cost** independentcheckboxesبدلنےکیمنظورشدہپالیسیبرقرار۔ StaffmoduleکاگرانٹکسیغیرOwnerکوpermissionwriterنہیںبناتا۔ نئےdiscount/financialrulesنہیں۔

### UI اور outcome

- Localbusyguard،loadedtargetid،activecollectionmembershipاورtargetselectionguard؛پرانےqueued/detachedcheckboxکاSaveنہیں۔ Reset/selection/readfailure/denialکیbaselinecleanupواضح۔ Pricingformالگsnapshot/tokenہے؛دونوںکیjointatomicSaveکیسندنہیں۔
- Knownno-commitfailureپراصلloadedcheckboxvalueواپس؛یہ **تازہDBstateکیخبرنہیں**،conflictپرreload/reviewلازم۔ Unknownپرmap/tokenاورrowsہٹتےہیں؛اندھیاگلیSaveنہیں۔
- Ackbindings/statusسےپہلے؛Resetresultکوdisplayکرنےیابعدکےcallbackمیںthrowہوتو **SAVED**،نہ"کچھمحفوظنہیںہوا"۔ Currentaccessdenialکافارمصافکرناپرانےproofکوتازہنہیںکرتا۔
- ServiceکےTransactionOutcomeمیںصرفknownpre-COMMITrollbackپرNoCommitConfirmed؛COMMITattemptیاunknownrollbackپرverificationپیغام۔ Coreمیںdurableattemptledgerیاسارےcallersکےلیےunknownlatchنہیں؛Desktopکاتokeninvalidateہیlocalguardہے۔

## ✅ صرف محدود مقامی جانچ

- **121 static checks**؛grammar/referenceheuristics،compilerنہیں۔
- **29 نئی lexical mutations**رد،اصلbytesبحال؛تمام **28 source-negative scripts**دوبارہپاس۔ پرانےauth/current-map/financialguardsنئےexplicitreaderاورstoredtargetmapکےمطابقبدلے،authorizationcheckہٹایانہیں۔
- **37 migrations،30 Python-owned WAL/DELETE plans**:Set/exacthandoff،staleSet/Reset،missing/explicitdefault،other/unknownmodule،role/active،SetاورResetکےactualSQL+Auditfaultrollback،deferredCOMMITfailure،exactReset/write-reservation/coherentsnapshot۔ Financialpostsصفر۔
- Pythonاپنےfingerprint/compare/transaction/auth/defaultassumptions/outcomecontrolچلاتاہے؛یہC#PermissionService/EditConcurrency/Database/TransactionOutcome،اصلWPF/proof/UI/LANثبوتنہیں۔
- پرانےschedule/PricingprobeکیSQLextractionصرفpricingsectionتکمحدودکی؛نئےpermissionsectionمیںtargetqueryدوبارہہونےسےغلطduplicateSQLmatchنہہو۔ Assertionsنہیںبدلے۔ پچھلےWorkbook18،Hardware38،schedule/Pricing26،Settings24،Icon24،Draft40اورEdit18probesدوبارہپاس۔

## ⏳ 24 نئے C# cases — UNRUN

`PermissionEditConcurrencyTests.cs`میں **18Facts+6Theoryrows=24** public-servicecases:دوeditors/full-listconflict/staleReset،rowpresence،role/active、دوسراhandle/context/target/scope、currentOwner/proof、failure-read、exactSet/Reset、auditrollback/retry、deferredCOMMIToutcomes،ownerfinancialexceptions、freshinactive-target、immutablemap/JSONاورimperativecompatibility۔

متوقعsuite **3248 UNRUN**۔ `.NET SDK`نہیں؛Releasebuildکی کوشش`dotnet: command not found`۔ آخرکااصلWindowsنتیجہاببھیv0.120.1کے2963total/2941PASS/22FAIL؛حصوں2تا5کےبعدکاتازہrunباقی۔ پرانےC#testsاور37migrationsنہیںبدلے؛financialpostingservicesجوںکےتوں۔

## ⏳ PT-72 — صرف الگ Windows ماحول میں

Verifiedencryptedbackup **اورواقعیالگDB/photos/config** پہلے؛اصلshopمیںposts/faultsنہیں۔

1. **3248/0fail/0skip**،Core/Tests/DesktopReleaseاورTRX/log۔ دوStafflists:ایکgrantبدلے،دوسراoldcheckboxیاResetکرے؛پوراoldintentrefuse،پہلاgrant/auditبرقرار،نیاجزویrow/auditنہیں۔ کسیاورmoduleکیchangeبھیoldlistrefuse۔
2. Targetrole/active、missingvsdefaultrow、futuremodule、wrongtarget/context/scope、ownerdeactivation/expiredproof؛checkednullrejectاورimperativeAPIcompatibilityالگجانچیں۔ OwnerProfit/Costcheckboxesبدلیں،OwnerSettings/Resetممنوعرہیں؛freshinactive-targetکیپرانیاجازتبرقرار۔
3. Readfailureکوسuccessfuldefaultformنہکہاجائے؛selectionبدلنے/denial/queuedpointer/keyboardSpace/doubleclickمیںپراناcheckboxنئےpersonپرنہلگے۔ ResetکاexactresultبغیرfreshLoadدکھے؛کامیابیکےفوراًبعددوسراwriterہوتواگلاSaveاسےپکڑے۔
4. Set/Resetauditfaults،providerCOMMIT/rollback/dispose،ackکےبعدbinding/rowresetfailure۔ Knownrollbackپرجائزretry،unknownپرrows/tokeninvalidاورverify؛callbackfailureپر **SAVED**۔ ActualWPFreentry/selection/time-basedpermissionschecksباقی۔
5. موجودہauthorizationregressions،financialvisibility/defaults،pendingtransactionmapreadsاورتمامپرانےpermission/sessiontestsچلائیں۔ PythonlocalWAL/DELETEاصلLAN/parallel/restart/powerlossکاثبوتنہیں۔

## ⏳ 5.1 میں باقی

Staffcredential/recoveryreset、accounttoggle/remainingnative/custom/imperativeSettingswritersکیinventory؛startupregistry+SQLiteکیrace/partialrestore؛PT-67تا72کیاصلWindows/provider/UI/physical/filegates۔ اسکےبعداسیحصہ5میںStockTake/toggle/bulk/snapshotکےباقیراستے۔ **5.1/حصہ5ابھیجاری؛1تا5کےکام+ضروریتصدیقسےپہلے6نہیں۔** ❌6/installerشروعنہیں۔

Contentbaselineآخریpersistedstateملاتیہے؛A→B→عینAکیintermediatehistoryنہیں۔ یہdurableidentity،globalrevisionیافوریrevocation/physicaltestcertificationنہیں۔

## ⏳ بعد کا اسی5.1 کا checkpoint

v0.131.0 میں Staff account toggle/password/PIN کے checked forms/outcomes الگ کیے؛[PT-73/موجودہحدود](STAFF-EDIT-CONCURRENCY-REVIEW-URDU.md)۔ اوپرکےتاریخینتائجاپنےcheckpointکےہیں؛recoveryword/remainingwriters/registryاوراصلWindowsجانچابھیباقی،5.1مکملنہیں۔
