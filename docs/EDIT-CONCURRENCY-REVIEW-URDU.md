# ⏳ حصہ5/14 — بیک وقت ترمیم: پہلا checkpoint — v0.123.0 Verbena

> ⏳ تازہ **5.1Settings** کا محدود checkpoint: [PT-67/باقیراستے](SETTINGS-EDIT-CONCURRENCY-REVIEW-URDU.md)؛3143suiteUNRUN۔ نیچےاسوقتکیحیثیتتاریخی؛5.1ابھیجاری،1تا5مکمل+تصدیقسےپہلے6نہیں۔


> ⏳ تازہv0.124.0میںsharedDraftکااگلامحدودcheckpointآگیا: [جائزہ/PT-66](DRAFT-EDIT-CONCURRENCY-REVIEW-URDU.md)؛3118suiteUNRUN۔ نیچےv0.123.0کیپہلیحیثیتتاریخیہے؛اسکےsharedDraftباقیکےحوالےاباسنئےcheckpointسےمحدودطورپربدلے۔ Settings/StockTake/remainingroutesاورWindowsاببھیباقی،حصہ5جاری۔


## ⏳ اجازت اور واضح حیثیت

مالک کے تازہ `Next` نے صرف **حصہ5** منظور کیا۔ یہ اس حصے کی **پہلی محدود source پیشرفت** ہے،پورے concurrency کام کی تکمیل نہیں۔ اگلی پیشرفت بھی اسی حصے کے باقی راستوں پر ہوگی؛حصہ6 کی durable identity/design پالیسی خود سے شروع نہیں کی۔ حصوں2،3،4 کی نئی C#/Windows آزمائش بھی باقی ہے۔

مثال:ایک ہی دوا کا فارم دو جگہ کھلا ہے۔ پہلے نےrack درست کیا؛دوسرااپناپرانافارمSaveکرےتواسکیپرانیrackیاpriceپہلیدرستیخاموشی سےمٹانہدے۔ نئےcheckedراستےمیںDBکیاصلحالتایکہیwrite transactionمیںملتیہے؛فرقہوتو **Nothing from this edit was saved**،پھرreopen/refreshاورreview۔ خودکارmerge،forceoverwriteیادونوںصارفینکیطرفسےجھوٹیsuccessنہیں۔ یہنئیمالییاbusinessپالیسی نہیں،اسیحصےکامقصدہے۔

## ⏳ اس checkpoint کا source دائرہ

### ایک وقت کی درست پڑھائی

`Database.Snapshot` میں synchronous read transaction،ایک ہی connection اور SQLite `query_only=ON`۔ Nested read اسی caller کی transaction borrow کرتی ہے؛دوسرے thread کو instance gate سے انتظار۔ root snapshot کے بعدquery-onlyOFF،transaction/flagcleanup؛snapshotکےاندرنیاTxمنع۔ پہلےسےجاریwriteTxمیںnestedSnapshotاسیwriteconnectionکیreadہے،اسےنئیreadonlyreservationنہکہیں۔ Callback/connection باہرنہلےجائیں،Task/await/cross-threadwait نہکریں۔

استعمال:Product.Search/Get،ProductاورCustomereditformکےparent+tags+extra definitions+answers،CustomFieldList/Values/EditorSnapshot،NativeFieldList،Category/Company/Formula/Brand/Unitlists،Customer/SupplierledgerPageاورSaleQuote۔ Parenteditorدوبارہالگanswer-readسےپرانی/نئیحالتنہیںملاتا؛Desktopکوpreparedsnapshotملتاہے۔ **تمامreports/receipts/exportsکوایکsnapshotمیںبدلنےکادعوینہیں۔**

### پرانی حالت سے لکھنے کی روک

`EditVersion` immutable،اسیDatabaseinstance/scope/recordIDسےbound،JSON/audit/export/draftserializationسےباہر۔ DBکےاصلcolumnvaluesکوJSONکےذریعےunambiguousطورپرserializeکرکےSHA256baseline؛display/maskedmodelیاصرفsecond-resolutionupdated_atپرtrustنہیں۔ یہauthenticationtokenنہdurablepostingidentityہے۔

اصلcomparison **BEGIN IMMEDIATE کےبعد،موجودہactorpermissionکےبعد،پہلےparent/child/auditwriteسےپہلے**۔ SQLiteکیwrite reservationموازنہاورUPDATEکےدرمیاندوسرےwriterکوجگہنہیںدیتی۔ الگconnection/Databasehandleسےآنےوالیتبدیلیبھیچیکہوتیہے۔ Missing/wrongcontext/wrongscope/wrongID/currentdifferenceپرfail؛write/auditنہیں۔ Product/Customer/catalogکےموجودہTransactionOutcomeاورunknown/SAVEDguardsبرقرار؛knownrollbackکوبلائنڈunknownنہیںبنایا۔

Coveredcheckedwriters:
- Product.Update:اصلproductrow،categorytags،extraanswerdefinitions+values۔ نئےProductCreateکیauthoredcostکیپرانیاجازتبرقرار۔
- Customer.UpdateCustomer:نام/phone/category/creditlimit/photo/activeاورextrafields/answers۔ balance/loyaltyکیعاممالیحرکتاسbaselineسےباہر؛محضنئیوصولیphonecorrectionکوبےوجہردنہیںکرتی۔ Cashierکےphoneحقاورnull-limitپالیسیبرقرار۔
- CustomField.Update/SetActive/Move:متعلقہentityکیپوریdefinition/orderحالت؛managerlistپریکہیمکملentityquery،ہرrowکےلیےدوبارہپوراتھیلاناسکیننہیں۔ Standaloneextra-answerSaveکوdefinitions+savedanswersbaseline۔
- NativeField.SetRequired:خطرےکیوضاحتیconfirmationسےپہلےپڑھیعینdefinitionدوبارہTxمیںملتیہے؛dialogکےدورانبدلےlabel/rulesپرپراناYesکافینہیں۔
- Category/Company/Formula/Brand/Unitکےrename/editاورBrandCompanylink۔ Brandnameاورcompanylinkاببھیدوtransactionsہیں؛پہلےمرحلےسےاسیوقتکانیاtokenواپس،دوسرےمرحلےکوسےپہلےblindfreshbaselineنہیںملتا۔ درمیانمیںدوسرےصارفکیترمیمہوتولنکrefuse،پہلانامSAVEDاورپراناpartial-successguardبرقرار۔

تمامDesktopcallersاصلloadedbaselineبھیجتےہیں۔ ProductionDesktopمیںSessionsenabledہونےکےباعثmissingbaselineمنع۔ **LegacyisolatedfixturesمیںSessionsdisabledاورexpectednullہوتوپراناAPIbehaviorبرقرار**؛tokenدیاہوتودونوںhostsمیںچیک۔ یہیوجہہےکہپرانےservicefixturesکواسcheckpointکیauthenticated/concurrencycertificationنہیںکہتے۔ کسیاضافیCorehostکوenableاوردرستcompositionلازم؛rawSQL/hostilein-processcodeکےخلافحفاظتنہیں۔

### دو مزید check/use اصلاحات

- CostPolicy/ProfitPolicyکاmainfinancialswitchاباسیcurrentDB/borrowedSnapshot/Txسے؛دوسرےSettingsinstanceکاپراناcachedONجوازنہیں۔ MissingکیپرانیشپڈONdefault،malformed/readfailureپرhide۔ UIکیunsavedSettingsinputکوخودسےreload/overwriteنہیںکیا؛grants/mainکیمنظورشدہپالیسیبرقرار۔ Blackbox/BookCheckکاالگredesignنہیں۔
- CreateCustomerکاشروعیphonepreflightصرفmessageکیسہولت؛عینINSERTسےپہلےBEGINIMMEDIATEمیںدوبارہactivephoneچیک۔ دوconnectionایکخالیpreflightکےبعددوسراعامduplicatecreateنہیںکرتے۔ پہلےسےموجودCustomereditمیںsharedphoneکیwarningپالیسیبدستور،اسلیےنئیuniqueDBconstraintنہیںلگائی۔

## ✅ صرف محدود مقامی نتائج

- **114staticchecks**؛C#grammar/type-referenceheuristics،Roslyncompilerنہیں۔
- **47نئیlexicalmutationsرد،عیناصلbytesبحال**؛تمام21source-negative scriptsدوبارہپاس۔ چندپرانےlexicalassumptionsکوpreparedsnapshot/currentID/Tx-method-scopingکےمطابقبدلا؛پرانیcoverageنہیںہٹائی۔
- **37migrations،18Python-ownedtwo-connectionSQLiteplans**:10edit-scopesکیstale/freshcomparison،balanceexclusion،tags/definitions/answers،WALsnapshotold/old،query-onlyrefusal،BEGINIMMEDIATEwriterreservationاورphonepreflightrace۔ Financialpostsصفر۔ Pythonخودfingerprint/comparison/control-flowچلاتاہے؛یہC#EditConcurrency/Database/provider/WPFexecutionنہیں۔
- پرانےparent-saveprobeدوبارہ:15SQLplans/30rollback+retryاور1brandpartialcase؛مالیrecords/stockبرقرار۔

## ⏳ C# اور اصل ثبوت

`EditConcurrencyTests.cs` میں **25نئےلکھےہوئےfacts**؛متوقعsuiteاب **3082**۔ دوDatabasehandles،same-timestampdifferentvalue،parent/extraanswerconflict،wrongrecord/scope/context،enabledmissingbaseline،Cashierphone،balanceexclusion،definitions/nativeconfirm/catalog/unit/brandphase2،read-only/nestedsnapshot،دوسرےconnectionکیcommitکےدورانold/oldread،staleSettingsmainاورserializationکےcases۔ **سبیہاںUNRUN**؛3082کیruntime discoveryبھیباقی۔ SDKنہیں:dotnetbuildReleaseکی کوششexit127،`dotnet: command not found`۔ اصلپراناWindowsv0.120.1نتیجہ2963total/2941PASS/22FAILہیبرقرار۔

## ⏳ حصہ5 میں ابھی کیا باقی ہے — اگلا کام یہی ہے

1. **SharedDrafts کاstalepayloadSave/overwrite،StockTakeCount/Found/Cancel،باقیcatalog/photoorder/toggleactions،Settingsmulti-keyforms،bulk/workbook/remainingupdates** کاsame-timeintent/expected-stateجائزہ؛جہاںموجودہTxguardsکافینہیںوہاںمحدوداصلاح۔ انہیںاسcheckpointسےمحفوظنہسمجھیں۔
2. باقیmulti-queryreports/receipts/preparedfinancialoutputsاورread→writecallersکیcoherency/freshdecisionجانچ؛alreadyprinted/exporteddataواپسہٹانےکادعوینہیں۔
3. ActualC#/Microsoft.Data.Sqliteparallel/providerbusy/rollback/dispose/COMMITfaultsاورSQLiteDELETE/LANjournalمیںruntime؛WALPythonprobeدوسرےPCکیجانچنہیں۔ لمبےsnapshotکیwriterblocking/performanceبھیباقی۔
4. Concurrentauth/permission/mainchangesکےتمامwrite/quote/printراستے،modalcontinuationsاورDesktopbindingsتازہWindowsپرثابتکرنا۔ بعضlegacyreads/APIsابھیcaller-gatedہیں۔

Contentcomparisonآخریpersistedstateملاتاہے؛A→B→عینAواپسہوجائےتوہرintermediatehistoryگننےوالیrowrevisionنہیں۔ Schema/migrationsنہیںبدلے۔ Tokensin-memory/localہیں؛newpage/restart/cross-PCretrydedupنہیں۔ حصے6کےdurablerequestidentityپرنئیمنظوریابھیدرکارہے۔

## ⏳ PT-65 — الگ Windows میں پہلی عملی جانچ

پہلےverifiedencryptedbackup **اورواقعیالگDB/photos/config**؛صرفexeکیfolderکافینہیں۔ اصلshopپرتجرباتیposts/faultinjectionنہیں۔

1. تازہRUN-TESTSمیں**3082/0fail/0skip**اورDesktopReleasebuild؛نئیdiscovercount/TRX/logرکھیں۔ پراناPASSاسورژنکیسندنہیں۔
2. دوisolatedserviceconnectionsیاالگtestwindowcopiesمیںایکProduct/Customerکھولیں۔ پہلاphone/rack/price/tags/extraanswerمحفوظکرے،دوسراSaveکرے:دوسرےکابدلاؤ/auditنہیں،پہلاریکارڈبرقرار،واضحconflict؛reopen+reviewکےبعدجائزSave۔ sameupdated_atsecondمیںبھییہی۔
3. اضافیجواباورdefinitionبیکوقتبدلیں؛parentSaveانکونہپونچھے۔ Nativecriticalconfirmationکھلیہو،دوسراdefinitionبدلے،پراناYesrefuseہو۔ CustomMoveکےدورانorderبدلےتودونوںپرانےsortنہلکھیں۔
4. Catalog/Unitکےپرانےeditسےنیاname/whole-onlyruleنہبدلے۔ Brandrenameاورlinkکےدرمیاندوسراتبدیلیکرے:پہلاSAVED،linkconflict،duplicatebrandنہیں؛نامکیsuccessکوواپسنہکہیں۔
5. Customerکیعامcollectionکےبعدphoneeditجائز،مگرOwnercreditlimit/phone/answersبدلےتواُسکیپرانیformrefuse؛Cashierاپنامجازphoneکامکرسکے۔ نئیCreateCustomerایکphoneکےدوpreflightکےباوجودایکباربنے؛پرانےsharedphoneeditپرمنظورشدہwarningبرقرار۔
6. دوسرےSettingsinstanceسےmainOFF:پرانےcachedONکےباوجودCost/Profitoutputsdeny؛fields/UIکےunsavedtextخودsaveنہہوں۔ Snapshotمیںledgerheader/lines/booksایکوقتکےہوں،falseMISMATCHنہیں۔
7. Snapshotreadfault/busy/readonly/dispose،pooledconnectionreuse،COMMITfaultاورlongreadمیںcallback/transactionstateصاف؛unknown/SAVEDretryguardsقائم۔ اسکیاصلproviderجانچیہاںنہیںہوئی۔

⏳ **حصہ5جاریہے؛باقیsourceجائزہبھیباقی،صرفWindowsنہیں۔** FullPhase4OPEN؛❌حصہ6شروعنہیں،installerنہیںبنا۔
