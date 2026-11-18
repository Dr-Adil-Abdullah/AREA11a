# PT-48 — نمبر5/6/7: Catalogs اور Units کی موجودہ اجازت

**v0.106.0 Estuary · `#586C48` · 2026-11-10 · source checkpoint؛ Windows/C# UNRUN**

[مستقل1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md) · [تفصیلیsource trace](POS-PHASE4-CATALOG-CURRENT-REVIEW.md)

## 1۔ دائرہ اورروزمرہ مثال

Manager نےcategory/unit editor کھولا؛مالک نےاس کاaccount یاProducts اجازت بندکردی۔ پراناform استعمال کرکےcategory کیproduct links ہٹانا،company/salt/brand بدلنا یاunit rename کرنا اب بھی ممکن نہیں ہونا چاہیے۔ اسی طرحsave ہوچکا ہو مگرreload ناکام ہو تو دوسریبارaction نہ کروایا جائے۔

یہ حصہ **نمبر5 account،6 parent/catalog writers،7 stale editors** اوران سےمتعلق **24 rollback/25 committed-result clarity** کی محدودsource اصلاح ہے۔ یہ مکملsession authentication،CAS یاپوریPhase4 نہیں۔ **Owner/Manager write policy،valid Products reader کیread-only access،Cost/Profit/main سےآزاد nonfinancial catalogs،category unlink/restore اورunit whole-quantity rules برقرار ہیں۔**

## 2۔ source میں کیا کیا ہوا؟

1. **5/6 —16 writers:** Category،Company،Salt،Brand اورUnit کےAdd/Update/Put-away-Restore،نیزBrand-company link: supplied active/positive manager check،پھرTx میںstored active/same-role Products Owner/Manager،اصلmutation/no-op سےپہلے۔ Audit بھیcurrent stored نام سے۔
2. **5/7 — capability:** Catalog/Units windows میںCanEdit ابsupplied role سےمستقلtrue نہیں۔ Current Products reader سےread/write capability؛جائزread-only reader برقرار،Cost/Profit/main کی نئیشرط نہیں۔ Actorless lookup APIs کےدیگرcallers نہیںبدلے؛یہAPIخودauthenticatedendpoint نہیں۔
3. **7 — cleanup:** account/module/read failure پرlists،selections،parent/company links،نام/order inputs اورunit WholeOnly tick صاف؛CanEdit=false۔ Failed read پرمصدقہ“No rows” overlay نہیں۔ Regrant پرخودauthor edits بحال نہیں؛واضحRefresh اورreview لازم۔
4. **7 — fresh actions:** New/Save/Put-away-Restore/Clear-brand-company actions fresh check کرتے ہیں؛detachedselection رد۔ دونوںwindows میںnominal3second access-only timer؛stableallowed tick authored text reload/overwrite نہیں کرتا،Closed پرtimer stop۔
5. **7/25 — SAVED:** Catalog اورUnit write commit مگرreload failure/exception یاsavedtarget نہملےتو **SAVED،دوبارہaction نہکریں؛Refresh/review**۔ Brand name اورcompany link پہلےکی طرحالگtransactions ہیں؛name save مگرlinkfail ہوتوpartial-success واضح۔ انہیںatomic نہیںکہاگیا۔ Product page سےmanager بندکرکےواپس آنےپرcurrentaccess/invalidatededitor چیک بھیشامل۔
6. **24 —49 C#cases:**8 invalid-authority cases ہرایک میںتمام16writers؛16 stored-audit/financial-independence cases؛16 audit-fault rollback/provider-error cases؛9 مزیدfacts: reader capability،regrant،no-op،unit linked-label rollback/usage rule،permission-read failure اورBrand partialcommit۔ **کلsuite2094cases؛یہاںسبUNRUN۔**

## 3۔ Windows پرالگtestinstallation میںعملیجانچ — ابھیUNRUN

**Verified encrypted backup،واقعیالگDB/photos/config لازم؛صرفexeکافولڈربدلناکافی نہیں۔ اصلshopپرsyntheticposts/permissionexercises نہیں؛secret یااصلcustomerdata chatمیںنہیں۔**

### 5.1۔ Current account/module اورread-only

1. الگtestManager سےCatalogs/Units کھولیں؛Products allowed،Cost/Profit/mainOFF بھی ہوں توcatalog manage ہوناچاہیے۔ Financial permission کوcatalog کی نئیشرط نہبنائیں۔
2. جائزProducts Salesman/other read-only reader سےlists دکھیں مگرwritecommands بندرہیں؛Products grant سےCashier کوManager نہیںبنناچاہیے۔
3. Manager کاaccountinactive،rolechanged یاProductsdeny کرکےہرAdd/Update/Put-away/Restore/brand-company action دوبارہآزمائیں؛mutation،unlink اورaudit نہہو۔ No-op بھیcurrentaccount چیک کرے۔
4. Grant واپس دیں؛پرانیdiscardedinputs خودواپس نہآئیں۔ پہلےRefresh،پھرrowselect/review،پھرنیاedit۔ AccountIDکومکملlogin-sessionproofنہکہیں۔

### 6.1۔ Catalog/Unit rules اورaudit

1. پانچcatalog families میںجائزAdd/Rename/Order/Put-away/Restore،Brand-company set/clear کریں؛audit کاactorstoredcurrentنامہو۔16writer cases واقعیC#suiteمیںچلائیں۔
2. Category کیdirectlinks/filtertags ہٹیں،history/children باقی؛restoreپرپرانیlinks خودنہبنیں۔ Company/salt/brand unlink کےطےشدہrules اورtypedtext برقراررہیں۔
3. Unitrenameپرcurrentbase/strengthlabels اکٹھےبدلیں،batchquantity/prices/history نہبدلیں۔ Usedunit put-away اببھیرد؛whole-unit ruleصرفنام بدلنےسےختم نہہو۔ یہحسابیrules دوبارہنہیںلکھےگئے۔

### 7.1۔ Stale windows اورfailed reads

1. دونوںwindows میںunsavedname/order/parent/company/WholeOnly لکھیں؛stableallowedtick پرمتنبرقراررہے۔
2. Access revoke یاisolatedfailedreadکےبعداگلےaction/nominal3second tick پرrows/selection/inputs صاف،Save/Toggle بند،واضحdiscard/Refreshwarning؛failedreadکوsuccessfulempty نہدکھائے۔
3. دوسریwindow سےآیاdetachedoldrowobject editorمیںدوبارہقبول نہہو۔ یہpersisteddefinitioncomparison/CAS نہیں؛externalcontentchange بغیرaccesschange کےfullconflictprotocol ابھیباقی۔
4. Windowclose پرtimer بند؛Products editor کےاندرmanagerکھولتےوقتaccessکھوئیں توواپس آنےپرپراناproductcost/form بحال نہہو۔ Validread-only Products page پرmanagerبندکرناManager permission error نہبنائے۔ اصلbindings/focus/dispatcherثبوتدیں۔

### 24.1۔ Fault/rollback — C# میںابھیUNRUN

1. تمام16writers کےaudit-fault cases؛category/company/salt/brand links،unit/name/order/WholeOnly اورaudit پہلےجیسےہوں۔ Provider کاprivate markerusererrorمیںنہآئے۔
2. Unitrename کےlinkedbase+strengthlabel fault case میںproduct labelsاورbatchquantity بھیبرقرار۔
3. PythonSQLiteprobeصرفSQL/SQLite transaction semantics جانچتاہے،C#permissions/Database.Tx/audit execution نہیں۔

### 25.1۔ Commit بمقابلہreload / Brand partialsuccess

1. Catalog/Unit actioncommitکےبعدصرفreloadfail کریں؛واضح **SAVED،دوبارہactionنہیں** اورRefresh/review۔ Wrong/missingsavedtarget کودوسریrowکیsuccessنہسمجھیں۔
2. Brandname save ہونےکےبعدcompany-link مرحلہdeny/fail کریں؛نامsaveرہے،پراناlinkبرقراراورpartialmessageواضح۔ یہالگtransactionsہیں؛automaticfullretryنہکریں۔
3. Parallel/restart/timeout/idempotency مکملنہیںہوئے۔ Product formکےinlinequick-add کیpostcommitrefreshUX اوردیگرcallerpaths الگباقیہیں؛Corewritercheckانپرلاگوہے۔

## 4۔ ثبوت اورباقی نمبرز

1. **2/3:**49نئےC#cases/2094totalلکھے؛اصلCore/Tests/DesktopbuildاورtestsUNRUN۔ Static/Pythonresultsاورحدودtrace/آخریpublicationreportمیں۔
2. **5:** حقیقیauthenticatedsessionbinding اورباقیcallersکھلے۔
3. **6:** باقیparent workflows،actorlesslookupcallers،expected-version/CAS اورBrandtwo-stageatomicity کھلے؛نئیatomicitypolicyنہیںلگائی۔
4. **7:** اصلWindowstimers/focus/bindings/regrant،دوسرےdialogs/cache؛periodiccheckinstantrecallنہیں۔
5. **23–25:** اگلیintegratedpriorityfault/retry/duplicates/concurrency؛اسکےبعد26–29files/backup/restore/newPC/cloud۔
6. **4/30–35/36:** Windowsresults/fixes/hardware/isolation کےبعدمجازواحدdouble-clicktestinstaller؛**ابھینہیںبنا،Phase4OPEN۔**

ہرportiondocs+اسیbranchcommit/push+actualremoteverificationلازم۔36مستقلنمبر36Nextنہیں؛کوئیconditionalroundestimate deadlineیاupperboundنہیں۔
