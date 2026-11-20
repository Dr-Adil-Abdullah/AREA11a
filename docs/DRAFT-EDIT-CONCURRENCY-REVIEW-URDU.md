# ⏳ حصہ5/14 — مشترکہ Draft کی پرانی ترمیم کی روک — v0.124.0 Sorrel

## ⏳ اجازت اور حد

تازہ `next` سے **اسی جاری حصہ5** کو آگے بڑھایا؛حصہ6 یا installer کی اجازت نہیں سمجھی۔ یہ دوسرا محدود checkpoint ہے۔ [پہلا Product/Customer/catalog checkpoint](EDIT-CONCURRENCY-REVIEW-URDU.md) برقرار۔ **Settings،StockTake اور باقی update/toggle/read→write راستوں کا source جائزہ ابھی باقی**؛صرف Windows کی جانچ باقی نہیں۔

مثال:دو لوگ ایک مشترکہ Draft کھولیں۔ پہلے نے مقدار2 محفوظ کی،دوسرے کے پرانے فارم میں1 تھی۔ پہلے پرانا Save اسے واپس1 کرسکتا تھا،یا Post پہلے نئی محفوظ محنت کوپرانےpayloadسےبدلتاپھراصلdocumentبناتا۔ ابایسیپرانیحالتعینwriteTxمیںrefuse؛نہpayloadبدلے،نہaudit،نہterminalmarker،نہcreatorشروعہو۔ موجودہrecordدوبارہکھولکرreviewلازم؛خودکارmerge/forceoverwriteنہیں۔

## ⏳ Source میں کیا بدلا

1. `EditConcurrency` کا نیا **draft** scope پوری `document_drafts` row کی اصل persisted حالت ملاتا ہے؛صرفtimestampیاlinecountنہیں۔ Databaseinstance/scope/IDchecksاورSessionsenabledمیںmissingtokenrefusalوہیجوپہلےcheckpointمیں۔ الگconnectionکیsame-secondpayloadتبدیلیبھیconflictہے۔ کوئیschema/migration/نیاdurableIDنہیں۔
2. **OpenSale/OpenPurchase اور List** coherent `Database.Snapshot` سےpayload،status،metadata،partnernameاورbaselineپڑھتےہیں۔ Openاپنے `ServiceResult.EditVersion` میںbaselineدیتاہے؛savedrowاورsummaryپرJSON-ignoredtoken۔ Purchaseکیlist/discardصرفموجودہpagepermissionسے،payloadopen/save/postاببھیCost/mainکےالگحقسے؛tokenاسحقکامتبادل نہیں۔
3. **SaveSaleDraft/SavePurchaseDraft** کےexisting-draftupdatesمیںموجودہreader/type/openstatusکےبعد،پہلےUPDATE/auditسےپہلےexpectedstateملتیہے۔ کامیابinsert/updateاپنیاسیtransactionکےاندرexactpost-writebaselineدیتاہے،commitکےبعدfreshreadنہیں۔ UIکیcleanupتککوئیاوردوسریتبدیلیکرےتواسکیطرفسےنیاtokenخاموشی سےنہیںلےلیا جاتا۔
4. **PostSale/PostPurchase** عینpre-marktransactionمیںموجودہpostingreaderکےبعدexpectedstateچیککرتےہیں؛پھرہیterminalmarkerاوربعدمیںاصلSale/Purchasecreator۔ DrawerکےYesسےپہلےنظرآنےوالیsummaryکاtokenجاتاہے؛modalکےدورانبدلاDraftنئےpayloadکیfreshOpenسےمنظورنہیںہوتا۔ `current:null` کےserviceراستےمیںاپنیfreshOpenکیحالتبعدکیmarkTxسےملتیہے؛اگرcallerنےsummarytokenدیاہوتویہاسکیجگہنہیںلیتا۔ premarkکےمعلومrollbackاورunknownCOMMITکیخبرالگ؛unknownپرcreatorنہیںچلتا،markerverifyکرنالازم۔
5. **Discard** عینtransactionمیںموجودہmodule/typeاجازتکےبعدپرانیsummaryملاتاہے؛دوسرےکیبدلیمحنتیاverificationmarkerپرانےYesسےconsumeنہیںہوتا۔ Postedfinalhistoryکاپرانالاکبرقرار۔ اسےاجتماعیbusinessdeleteنہیںبنایا۔
6. **معلوم ناکام creator کےبعدبحالی** میںصرف `created.NoCommitConfirmed` پرپرانامخصوصstatus/type/markerشرطوالاrestore۔ کامیابrestoreکاعینtokenاسیtransactionسےواپس؛Counter/Purchaseاسےاگلیاصلاحکےلیےرکھتےہیں۔ یہ `NoCommitConfirmed` اصلdocumentcreatorکےبارےمیںہے،یہنہیںکہdraftکیmarker/restoretransactionsبھیسرےسےنہیںہوئیں۔ restoreکےبعددوسراwriterبدلےتوواپسملنےوالاپراناtokenپھربھیrefuse۔ unknown/throw/restorefault/no-opپرنہنیاtokenنہجھوٹیretryاجازت؛finalizefaultپرSAVEDwarningبرقرار۔
7. **Held cart Resume** پہلےسےموجودایکtransactionمیںrecoveryDraftبناتا/heldcartconsumeکرتاہے؛اباسیوقتکاعینDrafttokenبھیCounterکودیتاہے۔ بعدمیںالگOpenکرکےدوسرےwriterکیحالتاپنیcartکیحالتنہیںبنائی۔ Counter/PurchaseمیںIDclear/consumptionپرtokenبھیصاف؛Refresh/regrantسےنئیbaselineنہیں۔ Existingunknown/SAVED/reconstructionguardsبرقرار۔

D-100کیsharedپالیسیبرقرار:صرفcreatorکالاکنہیں؛کوئیبھیاسkindکامجازشخصfreshbaselineسےکامکرسکتاہے۔ نئےordinarycartکاexistingautomatic-save→sameDraftPostبھیtokenلےجاتاہے۔ قیمت،رعایت،حقیقیcostfloor،SupplierB،StockPolicy،numberingاورSale/Purchaseکےاصلfinancialcallbackbodiesنہیںبدلے۔ Blackbox/BookCheckکاغیرمتعلقredesignنہیں۔

## ✅ صرف محدود مقامی جانچ

- **115 static checks**؛441C#filesکیgrammarاورreferenceheuristics،compilerنہیں۔
- **41نئیlexicalmutationsرد،عیناصلbytesبحال**؛تمام22source-negative scriptsدوبارہPASS۔ پرانےcurrent-reader/automaticdraftguardsکےneedlesمیںصرفنئےcallback/CASargumentاورnullableRestoresignatureکیadaptation،coverageبرقرار۔
- **37migrations،40Python-ownedtwo-connectionSQLiteplans**؛sale/purchaseدونوں،WALاورDELETEjournalدونوںمیںstaleSave/mark/discardrefusal،same-timestampdifferentpayload،exactown-save/restorehandoff،postcommitwriter،markerchange،write-reservationاورreadsnapshot۔ DELETEمیںreadsnapshotwriterCOMMITروکتا،WALمیںدوسریcommitکےبعدold/oldread۔ Financial/auditpostsصفر؛Pythonنےcreatorنہیںچلایا۔ **Pythonاپناfingerprint/comparison/controlflowچلاتاہے؛C#serviceیاauth/actualproviderکیسندنہیں۔** LAN/SMBکیجانچبھی نہیں۔
- پرانےautomaticDraftSQLکے22faultrollback/retryplans،draft/heldکے120current-actor/moduledenialsاورrecoveryINSERT/consume/2auditsکے4faults+restoreABORT/IGNORE/finalize/constraintsدوبارہPASS؛انکیپرانیمحدودنوعیتبرقرار۔

## ⏳ لکھے ہوئے،مگر چلائے نہیں گئے

`DraftEditConcurrencyTests.cs` میں **36نئےC#cases** (16theories×2اور4facts)،پرانےtestsبرقرار۔ staleSave/editedPost/drawermodal/discard،freshlinkedSave+Post،دوDatabasehandles،wrongrecord/context/scope،enabledmissingbaseline،knowncreatorfailureکیexactrestore/correction،laterwriter،changedmarker،currentactordenial،restorefault،sharedallowedperson،purchasepage-onlydiscard،heldresumeاورserialization۔ Suite **3118متوقعcases**؛یہactualruntime-discoverycountنہیں۔

`dotnet build dotnet/Area11.Tests/Area11.Tests.csproj --configuration Release` پھرنہیںچل سکا: **SDKغائب،exit127**۔ لہٰذاC#compile،Microsoft.Data.Sqliteexecution،WPFاور3118suiteسبUNRUN۔ پرانااصلWindowsنتیجہv0.120.1:2963total/2941PASS/22FAIL،نئیاصلاحاتکےبعدکانیاlogابھیباقی۔

## ⏳ PT-66 — اصل Windows پر الگ ماحول میں

پہلےverifiedencryptedbackup **اورواقعیالگDB/photos/config**؛اصلshopپرمصنوعیposts/faultsنہیں۔

1. تازہRUN-TESTSمیں**3118/0fail/0skip**اورDesktopReleasebuild؛TRX/logمحفوظ۔ حصوں2/3/4اورپہلےحصہ5کیرناببھیباقی۔
2. دوtestpages/مجازtestaccountsسےایکsaleاورpurchaseDraftکھولیں۔ Aمقدار/partner/extraanswerمحفوظکرے؛BپراناSaveاورPostدونوں:واضحconflict،AکاDraftمحفوظ،Bکاکوئیaudit/marker/documentنہیں۔ reopen/reviewکےبعدجائزSave/Postایکبار۔
3. DrawerمیںPostیاDiscardکاYesکھلاہو،دوسریconnectionDraftبدلے؛پراناYesrefuse۔ MarkerکیحالتبدلےتوبھیپراناDiscardrefuse۔ freshListاورreviewکےبعدصرفمجازعمل۔
4. نئیordinarysale/purchaseکاautosave→post،دوsuccessiveSaveDraft،پھرPost؛اپنےپہلےSaveسےself-conflictنہیں۔ SharedDraftکیcreated-byhistoryبرقرار،دوسراauthorizedpersonfreshحالتمیںکرسکے۔ PurchaseCostنہہوتوسummary/discardممکن،payload/postمنع۔
5. Missingproductسےcreatorمعلومvalidationfailureکرے؛کامDraftمیںبحال،correctionپراسیIDسےpost۔ restoreکےبعددوسراتبدیلکرےتوoldrestorationtokenنہچلے۔ realproviderrestore/COMMITfailure،throw/unknownاورfinalizefaultمیںپرانےverification/SAVEDguardsنہٹوٹیں۔
6. HeldcartresumeسےrecoveryDraftلِنک،پھرSave/Post؛دوسراeditorrecoveryDraftبدلےتوپراناCounterrefuse۔ Resumefailure/partialreconstructionپرپراناguardقائم۔ Refresh/regrantنہtokenبدلےنہunknownچھوڑے۔
7. WAL،DELETEاوراصلدوcounter/LANمیںbusy/longread/dialog/performanceاورpooledconnectioncleanupچیک؛PythonDELETEprobeکوLANproofنہسمجھیں۔

## ⏳ اگلا کام اسی حصہ5 میں

- **Settingsmulti-keyforms/cache→save** اور **StockTakeCount/Clear/Found/Cancel/Applyکےintent/state** کا sourceجائزہاورجہاںضرورتboundedاصلاح۔ اسcheckpointنےانہیںنہیںبدلا۔
- باقیcatalog/producttoggles/photoorder/bulk/workbookwrites،multi-queryreports/receiptsاورread→writecallersکیcoherency۔ پہلےسےprinted/exporteddataواپسہٹانےکادعوینہیں۔
- اصلparallel/providerfailures/Windowsmodalbindingsاورحصہ5کیremainingcoverageinventory۔

**حصہ5جاری،FullPhase4OPEN**۔ Localopaquecontenttokenصرفآخریpersistedحالتملاتاہے،ہرintermediaterevisionگننےوالیrowversionنہیں؛A→B→عینAکوتاریخکیطرحtrackنہیںکرتا۔ Terminaldraftmarkerکیپرانیتینtransactionsاببھیاپنیجگہ؛پوراdraftatomicنہیںبنایا۔ نہنئیglobal/durableexactly-onceشناخت،نہrestart/cross-PCretryحل۔ ❌حصہ6شروعنہیں،installerنہیںبنا۔
