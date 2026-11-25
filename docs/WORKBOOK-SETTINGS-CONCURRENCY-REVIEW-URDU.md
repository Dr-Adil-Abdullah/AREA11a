# ⏳ 5.1 جاری — Workbook Settings preview/apply — v0.129.0 Mallow

> **⏳ checkpoint کی حد:** اس فائل کے نتائج/اعداد اسی درج شدہ checkpoint کے ہیں؛پرانے pending یا not-started جملوں کو موجودہ حالت نہ سمجھیں۔ [تازہ مجموعی رپورٹ](COMPLETE-STATUS-AND-DECISIONS-URDU.md) اور[فیصلوں کا index](DECISION-REGISTER-INDEX-URDU.md) پہلے دیکھیں؛نیا Windows PASS فرض نہیں۔

> ⏳ تازہv0.130.0میںاسی5.1کےStaffpermissioncheckbox/Resetکیboundedاصلاح: [PT-72/باقی5.1](PERMISSION-EDIT-CONCURRENCY-REVIEW-URDU.md)؛3248suiteUNRUN۔ نیچےپچھلےcheckpointکاثبوتتاریخی؛remainingStaffpermissionsکےحوالےنئےجائزےکیحدتکبدلے،credentials/toggle/imperativeAPIsاببھیالگباقی۔5.1جاری،1تا5مکمل+تصدیقسےپہلے6نہیں۔


## ⏳ اجازت اور دائرہ

تازہ `next` پر اسی **5.1 Settings** کا Workbook راستہ دیکھا۔ [پچھلا Icon/PT-70](ICON-EDIT-CONCURRENCY-REVIEW-URDU.md) اور Settings/Backup/Pricing/Hardware کے checkpoints اپنی حدود کے ساتھ برقرار ہیں۔ **5.1 ابھی جاری ہے؛حصوں1 تا5 کے باقی کام اور ضروری تصدیق مکمل کیے بغیر6 شروع نہیں کرنا۔** Installer نہیں بنایا۔

## ⏳ پہلے سے کیا درست تھا،کیا کمی ملی؟

Workbook پہلے ہی download کے Snapshot hashes،preview میں before→after،Apply کی transaction میں تمام pending records کی پرانی state اور atomic updates+audits رکھتا تھا۔ **Settings کے edited value پر پہلے سے stale refusal موجود تھا**؛اسے اس checkpoint کی نئی ایجاد نہیں کہتے۔ Locked keys،الگ Settings tick،موجودہ bulk/cost/profit gates اور cross-record guards برقرار ہیں۔

کمی یہ تھی:

- Export/Preview متعدد queries کو ایک coherent SQLite snapshot میں نہیں پڑھتے تھے۔
- Settings validation سے پہلے shared cache کاReload،پھرcache سے دوسرے متعلقہsettingکیvalue؛Applyکےwrite-lockسےپہلےpreparation۔ دوسرےcounterسےpairedvalueبدلنےپرreviewاورfinalvalidationایکحالتکیضمانت نہیںتھے۔
- صرفeditedsettingکیvalueمقابلہہوتی؛cashier/managerlimitsیاsupplieralert/cutoffکیuntouchedvalidationpartnerکیpreviewbaselineنہیںتھی۔
- ہرexceptionکو"every change rolled back"کہاجاتاتھا،بشمولنامعلومCOMMITیاواقعیcommitکےبعدcache-refreshfailure۔ UIکیApplyمیںack-before-clear/localunknownlatchنہیںتھا۔

## ⏳ Source اصلاح

### مربوط Export اور Preview

`ExportAll` اور`PreviewBulk` ابDatabaseکےexisting `Snapshot` میںauthorizedreadsاورتماممتعلقہqueriesچلاتےہیں۔ XLSXparserپہلےworkbookپڑھتاہے؛Snapshotمیںrecords/settings/cross-rowpreviewایکحالتکےہیں۔ ImmutableSettingsformاسیsnapshotکےاندرnestedreaderکےطورپربنتاہے؛نیاالگDBsnapshotنہیںکھلتا۔ Readsnapshotreservationنہیں؛Applyکاالگwrite-Txموازنہضروریہے۔

### مکمل متعلقہ Settings baseline

PreviewاپنےDatabasecontextاورSettingsEditformکورکھتاہے۔ Writtenkeysکےساتھانکےپرانےbusinessguardکیdependencyبھیشامل:

- `discount.maxPercentCashier` ↔ `discount.maxPercentManager`؛
- `supplier.returnCutoffDays` ↔ `supplier.returnAlertDays`۔

نئیlimitsیامالیپالیسی نہیں۔ صرفوہدونوںقدریںملائیتیںجنہیںموجودہguardپہلےہیایکدوسرےکےساتھصحیحکہتاتھا۔ Dependenciesکوpreviewکےوقتکیreviewedحالتکہتےہیں،downloadکےوقتکیہرunchangedcellکیhistoryنہیں۔ WrittenrowکےexportSnapshotکےپرانےقواعدبھیبرقرار۔ غیرمتعلقtelemetryہرSaveکوconflictنہیںبناتی۔

### Apply کی ایک transaction

Currentbulk/financialvisibilityاورSettings/Ownergatesکےبعد،کسیUPDATEسےپہلے:

1. SettingsplanکاDatabasecontextملتاہے۔ Wrongcontext/missingbaselineپرfreshpreviewلازم۔
2. Writtenkeysاورانکیrelevantpartnerkeysکیexactpresence/rawJSONbaselineملتیہے۔ صرفpartnerکیconcurrentchangeہوتببھیپراناSettingsplanrefuse؛mixedworkbookکاکوئیearlierrecordجزویapplyنہیں۔
3. `PrepareBulk(values,connection)` اسیtransactionکیcurrentDBvaluesسےguardچلاتاہے،sharedcacheسےنہیں۔ Previewبھییہیconnection-awareguardاستعمالکرتاہے۔ باقیimperativeSettingscallersکیcache-basedguardحالتاسcheckpointمیںجامعطورپرنہیںبدلی۔
4. پہلےسےموجود `VerifyPlan`،ہرrecordکا`VerifyCurrentRow`،activeunit/cross-recordvalidationاوراصلUPDATE/auditloopبرقرار۔ نہschema/migrationsبدلیں،نہprice/cost/stock/taxbusinessrules۔

CapturedSettingsformسےکوئیUIinputpost-saveدوبارہrebaseنہیںہوتا۔ یہbatchplanہے،عامeditableformکیpartialhandoffنہیں؛successپرپرانیpreviewconsumedہے۔

### Outcomes اور پرانی کوشش کا استعمال

- TransactionOutcomeکیobservedحیثیت:صرفknownno-commitپرrollbackکاصافدعویٰاوراسیreviewedplanکیretryاجازت۔ COMMITattempt/نامعلومrollbackپر **unconfirmed**؛"ہرچیزواپسہوگئی"کااندھادعویٰنہیں۔
- ایکpreviewپرApplyserializedہے؛successیاunknownکےبعدوہیplanدوبارہapplyنہیںہوتا۔ Knownrollbackپربلاککھلتاہے۔ SourcecontextguardSettingsوالےplansکےلیےہے؛اسےتمامdata-onlybulkfeaturesکیجامعcontextreviewنہکہیں۔
- COMMITواپسآنےکےبعدcache/logrefreshناکامہوتوresult **Ok+SAVED warning**؛کامیابbatchکوfailed-writeنہیںکہتے۔
- DesktopexistingPostingAttemptGuardاستعمالکرتاہے:successackپہلے،پھرpreviewconsume/rows/status،پھرہیfreshintentکےلیےrelease۔ UnknownیاackکےبعدcallbackfailureپرClear/permissionrefresh/newfileسےپچھلیguardخودصافنہیںہوتی۔ Pageکوتصدیقکےبعددوبارہکھولناعملیgateہے،خودکارverificationنہیں۔
- ServiceکاplanblockاورUIguard**localmemory**ہیں؛نہdurablerequestID،نہrestart/cross-PCexactly-once۔ نیاCorepreviewبننےکوپہلےنامعلومکامکیverifiedreconciliationنہیںکہتے۔ Data-onlyApplyبھیاسیمشترکہoutcome/UIwrapperسےگزرتاہے،مگراسcheckpointکیمکملsource/runtimeسندصرفboundedSettingsراستےتکہے۔

## ✅ صرف محدود مقامی نتائج

- **120 static checks**؛grammar/referenceheuristic،compilerنہیں۔
- **29 نئی lexical mutations**رد،اصلbytesبحال؛تمام **27 source-negative scripts**دوبارہپاس۔
- **37 migrations،18 Python-owned WAL/DELETE plans**:دونوںpairedgroupsکےدونوںdirectionsکیstalebaseline،syntheticmixed-master+Settings+actualAuditINSERTrollback/retry،deferredCOMMITfailure،writerreservationاورcoherentsnapshot۔ Financialpostsصفر۔
- Pythonاپناfingerprint/compare/transaction/outcomecontrolچلاتاہے؛dynamicWorkbookUPDATEکیPythonreferenceاورالگsyntheticmastertableہے،اصلC#schema-planexecutionنہیں۔ اسےWorkbookparser،C#services/auth/cache/TransactionOutcome/planlatch、Excel、WPF、providerCOMMITیاLANproofنہکہیں۔
- پچھلےHardware38،schedule/Pricing26،Settings24،Icon24،Draft40اورEdit18probesدوبارہپاس۔ پرانےC#testsاور37migrationsبرقرار؛financialpostingservicesنہیںبدلیں۔

## ⏳ 13 نئے C# cases — UNRUN

`WorkbookSettingsConcurrencyTests.cs`میں **9Facts+4Theoryrows=13** نئےpublic-servicecases:چارvalidationpartnerconflicts،mixedproduct/settingsatomicrefusal،uncachedDBpreviewguard،validpair+sameplanreuse،wrongDatabase、telemetry、auditrollback/retry、deferredCOMMITunknown/blockedreuse、Settings-tickاورcurrentOwnerdeactivation۔ اصلXLSXbytesparse/preview/applyکےpublicdoorsاستعمالکرتےہیں،لیکن**یہاںنہیںچلے**۔

متوقعsuite **3224 UNRUN**۔ Releasebuildکی کوشش:SDKغائب،`dotnet: command not found`۔ LastactualWindowsنتیجہاببھیv0.120.1:2963total/2941PASS/22FAIL؛حصوں2تا5کےبعدکاتازہrunباقی۔ Static/sourcePASSاسکیجگہنہیں۔

## ⏳ PT-71 — صرف الگ Windows/DB ماحول میں

Verifiedencryptedbackup **اورواقعیالگDB/photos/config** پہلے؛اصلshopپرposts/faultsنہیں۔

1. **3224/0fail/0skip**،Core/Tests/DesktopReleaseاورTRX/log۔ Export→edit→previewکےبعددوسرےconnectionسےeditedsettingیاانچارpairedkeysکیpartnerبدلیں؛پوراApplyrefuse،mixedproductrowاورکوئیauditجزویapplyنہیں۔ Freshpreview/reviewکےبعدجائزbatch۔
2. Discount/supplierguardکیDBvaluesبدلیںمگرsharedcacheپرانیہو؛previewاورwrite-TxcurrentDBسےvalidationکریں۔ Samevalues/replay،unchangedrow،missingkey/presenceاورwrongDatabasecontextجانچیں۔ Owner/bulk/Settings/cost/profitgatesاورlockedsecrets/counters/iconkeysبرقرار۔
3. متعددsheetqueriesکےدرمیاندوسراwriter؛Export/Previewایکcoherentreadدیں،Applyپھرcurrentstateملائے۔ ActualWAL/DELETE/LAN/busyاوربڑےworkbookکیlatency/snapshotمدةکیWindowsجانچباقی۔
4. Settings/dataUPDATE،audit،COMMIT/rollback/disposefaults؛knownrollbackپراتنیہیreviewedplanکیretry،unknownپربغیرتصدیقدوسریکوششنہیں۔ ActualcommitکےبعدcacheRefreshاورUIbindingthrowپر **SAVED**،نہجھوٹاrollback۔
5. Applydoubleclick/reentry،IncludeSettingschange،permissionrefresh،newfileاورClearکےدورانunknown/ack-failureguardقائم؛successکےبعدپراناپلانمصرف،freshexplicitworkbookجائز۔ Guardکوverifiedreopenیادurableidentityنہکہیں۔
6. اصلExcel،roundtrip、UIreadfailure、financialprojections/cross-recordrulesاورتمامپرانےWorkbooktestsچلائیں۔ Data-onlybulk/CSV/import/Pricesکےباقیدروازوںکوچونکہمشترکہwrapperچھواہے،انکارگرشنبھیلازم؛اسحصےکومکملbulkcertificationنہکہیں۔

## ⏳ 5.1 میں باقی

باقیStaffpermission/reset/toggle、native/customاورimperativeSettingswritersکیinventory؛startupregistry+SQLiterace/partialrestore؛PT-67تا71کیاصلWindows/provider/UI/physical/filegates۔ WorkbookSettingsکییہsourceاصلاحپورے5.1یا5کیcompletionنہیں۔ بعدمیںاسیحصہ5کےStockTake/toggle/bulk/snapshotباقیراستے؛**1تا5کےکام+ضروریتصدیقسےپہلے6نہیں**۔ ❌6/installerشروعنہیں۔

Contentbaselineآخریpersistedstateملاتیہے؛A→B→عینAکیintermediatehistoryنہیں۔ کوئیschemarevision/durablejournalنہیںبنایا۔
