# PT-43 — Settings کی موجودہ اجازت اور حساس تبدیلیاں

**v0.101.0 Dunlin · `#6D5268` · Source checkpoint؛ C#/Windows جانچ UNRUN**

مثال: کیشیئر کو Settings کی اجازت ملی ہو تو وہ جائز عام setting محفوظ کر سکے۔ مالک اجازت واپس لے تو پرانے کھلے صفحے سے Save نہ ہو۔ Settings کی اجازت اکیلی Cloud key یا PIN policy بدلنے کا اختیار بھی نہ بنے۔ دوسری طرف Counter کا جائز auto-print toggle اور Backup والے شخص کا schedule بلاوجہ بند نہ ہوں۔

[تفصیلی trace اور حدود](POS-PHASE4-SETTINGS-CURRENT-REVIEW.md) · [مکمل اردو کتاب](USER-GUIDE-URDU.md)

## حفاظت پہلے

- اصل دکان پر فرضی وصولی/بل،غلط permission،fault injection یاDBتبدیلی **نہ کریں**۔ Verified backup اور واقعی الگ **DB،photos،config paths** لازم؛دوسریexe folder اکیلی isolation نہیں۔
- PIN/password/recovery word،backup password A،Cloud key یا اصلDB عامchat میں نہ دیں۔ چلتی دکان پرrestore نہ کریں۔
- اس build میں `tools\RUN-TESTS.cmd` کا متوقع total **1915** ہے۔ **36 نئے facts لکھے ہیں؛یہاں C# compile/tests نہیں چلے۔** Desktop/WPF build،اصلWindows/registry/hardware الگ جانچ ہیں۔

## کیا بدلا؟

- Settings کے عام Save میں بھی transaction کے اندر موجودہ active/same-role account اور متعلقہ grant چیک ہوتی ہے؛audit میں موجودہ stored نام آتا ہے۔
- Actor کے بغیر `SetUiOnly` اب صرف **17 مقررہ runtime metadata keys** کے لیے ہے؛shop/security/cloud configuration کا عام راستہ نہیں۔ Installer/Shift-at-startup کے موجودہ خاص راستے الگ fixed startup method سے برقرار ہیں۔
- آئندہ Cloud key کی تبدیلی کے audit میں **نہ پرانی نہ نئی key** لکھی جائے؛صرف تبدیلی کا نشان۔ **پرانے audit records نہیں بدلے**؛یہ Cloud key کی موجودہ storage کو encrypt کرنے یا پرانی copies صاف کرنے کا دعویٰ نہیں۔
- Settings کی اجازت ختم یاread خراب ہو تواگلےactive-viewcheck پرtabsچھپیں،Saveبند،typedCloudkey،PIN/recovery،staffselection/permissions/pricingاورlabelsصاف۔ صرفCloudgrant ختم ہو توCloudخانےصاف،جائزعامادھورےeditsبرقرار۔
- Workbook کےSettings والےحصےمیںبھیcurrentwriter/Ownerکیشرطاسیexistingtransactionمیں؛منعkeyکےساتھپورافائلapplyرد،جزویsaveنہیں۔

## الگ Windows test copy میں مشق

| # | عمل | متوقع نتیجہ — ابھی عملی تصدیق باقی |
|---|---|---|
| 1 | Settings grant والاactive staff،عامshopfooter/printing save | جائزsave؛Cost/Profit/main کی نئی شرط نہیں |
| 2 | Settings revoke،account inactiveیاstoredroleبدلیں؛پراناصفحہکھلارہے | اگلےcommand/nominal3scheckپرحساسinputsصاف،tabsچھپیں؛Saveمنع۔ مختلفWindowsuser/sessionکیاصلجانچالگباقی |
| 3 | Settings regrant،currentaccountvalid،Refresh | تازہجائزخانےواپس؛بلاککےدورانخودSaveنہیں |
| 4 | POS-onlyCounterپرauto-printtoggle؛پھرCorefixtureمیںاسیdictionaryمیںshopnameبھی | اکیلاtoggleجائز؛ملاہواSaveSettingsgrantبغیرپورارد |
| 5 | Backupgrantوالاشخصscheduleکےتینخانےبدلے؛ساتھprinterwidthشاملکرے | جائزscheduleبرقرار؛extraSettingskeyسےپوراملاہواSaveرد |
| 6 | SettingsgrantمگرSyncOFF؛پھرSyncgrantبھیON | SettingsاکیلےCloudconfigurationنہیں؛cloud-onlyکےلیےSync؛ordinarySettingsکےساتھCloudبدلنےکےلیےدونوں |
| 7 | Cloudخانےمیںصرفsynthetickeyٹائپ،پھرSyncrevoke | اگلےcheckپرtypedkey/URL/table/shop/interval/labelsصاف؛savedkeyکےprefix/suffixبھیSettingsمیںنہیںدکھتے؛عامادھوراformبلاوجہeraseنہیں |
| 8 | CloudOFF/بغیراجازتعامSettingssave | چھپےautomaticminutesکےخالیہونےسےجائزعامSaveنہرکے |
| 9 | ManagerکوSettings/Import/مالیgrantsسبدیں؛security/startup/discountcapsیاowner-protectedkeyبدلے | Owner-onlyکیشرطبرقرار؛bulkworkbookمیںعامsettingکےساتھملاکردیںتوبھیپوراapplyرد |
| 10 | Ownerسےجائزsecurity/PINpolicy،appname/icon/main/focus/billbasischange | موجودہOwnercheckاورپہلےکےvalidationsبرقرار؛PIN/recoveryکےاصلAuthrulesنہیںبدلے |
| 11 | خودکارfixtureمیںgrantqueryیاsettingsauditپرخرابی | deny؛نہنئیsetting،نہآدھیdictionary،نہcacheمیںنیاvalue۔ اصلDBمیںtrigger/tableتبدیلیہرگزنہیں |
| 12 | نئیCloudkeysaveکےبعدisolatedfixtureکاauditpayloadچیک | old/newکیاصلkeyنہیں،صرفchangedmarker؛اصلkey/chat/reportمیںنہلائیں۔ پرانےauditکیصفائیاسحصےمیںنہیں |
| 13 | Installerauto-startاورShift-at-startupmanualrecoveryکےموجودہtests | fixedsystempathبرقرار؛manualpersistfailتوسuccessنہیں۔ Stale/forgedOwnerSetModeسےregistrytouchنہیں۔ اصلWindowsregistry/CLItrustابھیجانچناہے |
| 14 | عامSettingssaveہوجائےمگربعدوالاstartup/focusstepfail | واضح **Settings SAVED**؛پہلےsaveکوrollbackنہسمجھیں،refreshکےبغیراندھادھنددوبارہنہکریں |
| 15 | تمام1915cases،Desktop/WPFbuild،آٹھtabs/timer/load-unload،keyboardاوراصلscreenshots | نئی36factsاکیلیکافینہیں؛اصلWindowsنتائجاورانکیfixesابھیباقی |

## اس ماحول کا نتیجہ

- **90 static checks PASS**؛410 C# grammar files،4494 calls،8921 references،structural0issues؛compiler نہیں۔
- **16 موجودہ synthetic SQLite probes PASS**؛C# Settings/permission/transaction execution نہیں۔
- **23 source-negative تبدیلیاں رد** اور اصل bytes بحال؛executable C# mutation testing نہیں۔
- Python backup reference میں6roundtrips/24negatives PASS؛اصلC#/Windows/DPAPI/new-PCrestoreنہیں۔
-710baselinepathsبرقرار،113دوسرےCore service/data filesunchanged۔ **36نئےfacts/کل1915C#casesUNRUN۔**

## حدود اور باقی کام

یہ **محدود source اصلاح** ہے،پورےSettings/sessionکیcertificationنہیں۔ Native/customfields،photos،اصلsession/recordauthorization،Settingscache/cross-instanceconcurrency،registry/filesکےساتھatomicity،cloud/اصلencryptedrestore/new-PC/DPAPIابھیباقی۔ پہلےسےکاغذ/PDF/clipboardمیںنکلیچیزواپسنہیںلیجاتی۔ Runtimehardware/sessionmetadataکاactorlessرہنااسکیاصلیتکاسرٹیفکیٹنہیں۔

حساب،SupplierB،الگCost/Profit/main،Extra/below-cost/overpayment،Blackbox/BookCheckاورbackuppasswordA/formatنہیںبدلے۔ Stored audit records حذف/تبدیل نہیں کیے۔

اسکےبعدباقیrecord/field/photo/sessionجائزہ،cross-servicefault/retry/concurrency،files/cloud/restore،Windows1915+WPF/hardware/screenshotsاورواپسآنےوالیfixes؛پھرمجاز **واحدsendabledouble-clicktestinstaller**۔ **ابھیinstallerنہیںبنا۔** تقریباً **3–6مزیدمربوطreview/resultچکر** مشروطاندازہہے،حدیاوعدہنہیں؛نئیfindingسےبڑھسکتا۔
