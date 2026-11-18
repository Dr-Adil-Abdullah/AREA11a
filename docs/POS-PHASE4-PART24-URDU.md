# PT-44 — Photos کی موجودہ اجازت اور پرانی preview کی صفائی

**v0.102.0 Whimbrel · `#4D6872` · Source checkpoint؛ C#/Windows جانچ UNRUN**

مثال: Customer کی اجازت واپس لینے کے بعد اس کی پہلے سے کھلی gallery سے تصویر Add/Hide/Restore/Open نہ ہو اور اگلے check پر پرانی بڑی تصویر بھی صاف ہو۔ دوسری طرف POS-only کیشیئر کو Counter کی product thumbnail دکھانے کے لیے بلاوجہ Products کی پوری gallery اجازت نہ دینا پڑے۔

[تفصیلی trace اور حدود](POS-PHASE4-PHOTO-CURRENT-REVIEW.md) · [مکمل اردو کتاب](USER-GUIDE-URDU.md)

## حفاظت پہلے

- اصل دکان پر فرضی بل/وصولی/photo یاDBfault **نہ کریں**۔ Verified backup اور واقعی الگ **DB،photos،config paths** ضروری؛صرف دوسریexe folder isolation نہیں۔
- اصل customer/staff تصویر،PIN/password،backup password A،Cloud key یاDB عامchat میں نہ دیں۔ Synthetic test pictures استعمال کریں۔
- `tools\RUN-TESTS.cmd` کا متوقع total **1945** ہے۔ **30 نئے facts لکھے ہیں مگر یہاں C# compile/tests نہیں چلے۔** Desktop/WPF،اصلcamera/viewer/timers اورscreenshots الگ باقی ہیں۔

## اس حصے میں کیا بدلا؟

- Gallery/List/PathOf اورphoto writes میںموجودہactive/same-roleaccount،متعلقہmoduleاورصحیحparentrecordچیک؛auditمیںموجودہstoredنام۔ Addکےactiveparentکیپرانیشرطبرقرار۔
- Customer/Supplier/Staff کیbulkcoverreadsپرcurrentmodule؛productdecorationاورown-avatarپرموجودہaccount،بلاوجہنیاProducts/Staffgrantنہیں۔ یہactualsessionauthenticationنہیں۔
- Failed/deniedgalleryکیselection،بڑیpreview،thumbnailrows،recordtitle/count/hiddenlabelsصاف؛failureکو“مصدقہno photos”نہیںکہتے۔ Regrantپرتازہread۔
- Fullpreviewکےلیےcachedfilepathبراہراستنہیں؛photoIDسےfreshPathOf۔ Camera/filepicker/Hide/Restore/Cover/Openسےپہلےfreshaccesscheck؛windowمیںnominal3secondtimerاورCloseپرstop۔
- Photochangecommitہوگئی مگرgalleryrefreshfailتو **SAVED،دوبارہactionنہکریں،refreshکریں**۔ SQLiteکاکاماورباہرfile/viewerایکatomictransactionنہیں۔

## صرف الگ Windows test copy میں مشق

| # | عمل | متوقع نتیجہ — ابھی عملی تصدیق باقی |
|---|---|---|
| 1 | Customergrantوالاstaffgalleryکھولے؛پھرgrantrevoke | اگلےcommand/تقریباً3secondcheckپرselection/preview/rows/title/countصاف؛Add/Hide/Restore/Openمنع |
| 2 | Accountinactive،storedroleبدلا،missing/forgedOwner؛پھرvalidregrant | منعaccountسےکامنہہو؛صحیحaccount/اجازتبحالہوتوتازہgalleryواپس |
| 3 | DBسےphotosیاgrantreadخراب | failedread؛“Photos not loaded/access needs checking”،مصدقہخالیgalleryنہیں؛rawprovidererrorنہیں |
| 4 | Customer/Suppliergallery،Cost/Profit/mainOFF | اپنیجائزmodulephotoرسائیبرقرار؛نئیfinancialشرطنہیں |
| 5 | POS-onlyCounterproductcover،پھرProductsgallery | thumbnailجائز؛Productsgalleryکیالگgrantبدستورلازم۔ Anonymous/staleaccountکوbulkcoverنہیں |
| 6 | Staffgallerydelegateاورعامشخصکیown-avatar | StaffgalleryاپنیcurrentStaffgrantسے؛اپناavatarبغیرStaffgrantبرقرار۔ Oldrole/inactiveactorنہیں؛یہforgedIDکےخلافاصلsessionproofنہیں |
| 7 | Parentinactiveمگرموجود،پھرparentmissing/orphanphoto | inactiveکیپرانیhistoricalread/Hide/Restoreبرقرار،Addمنع؛missingparentgallery/PathOf/writeمنع۔ Bulkdecorativecoversکیper-IDpolicyنہیںبدلی |
| 8 | Hiddenphoto،coverselection،missing/unsafefilename | پرانیhidden/order/pathاورفائلنہحذفکرنےکیشرطیںبرقرار؛ایکcurrentcheckسےپرانالیاہواfilepathواپسنہیںہوتا |
| 9 | Developer-controlledisolatedauditfault: Add/Hide/Restore | DBwritesrollback؛نئیfailedcopyکیcleanup،اصل/source/پرانےgalleryfilesبرقرار۔ Powerloss/cleanupfailureکیglobalضمانتنہیں |
| 10 | Customercreationمیںstagedphotoاورinactiveactor | currentphotocheckکیexceptionسےparent/photo/auditrollback؛بغیرstagedphotoوالےcreatorsکیپوریاجازتاسحصےمیںcertifyنہیںہوئی |
| 11 | Add/Hide/Restore/Covercommitکےبعدrefreshfail | واضح **SAVED**؛پراناsaveundoنہسمجھیں،blindduplicateactionنہکریں |
| 12 | PhotoIDselect،permission/pathبدلے،Openexternalviewer،windowباربارopen/close | تازہPathOf،staleVMpreviewصفائی،timersCloseپرstop؛پہلےکھلاWindowsviewerواپسlockنہیںہوتا۔ اصلWindowsثبوتچاہیے |

## اس ماحول کا نتیجہ

- **91 static checks PASS**؛411 C# grammar files،4522 calls،9036 references،structural0issues؛compiler نہیں۔
- **16 موجودہ synthetic SQLite probes PASS**؛نئیC#photo/permission/file/transactionexecutionنہیں۔
- **21 source-negative تبدیلیاں رد** اوراصلbytesبحال؛executableC#mutationtestingنہیں۔
- Pythonbackupreferenceمیں6roundtrips/24negatives PASS؛اصلC#/Windows/DPAPI/restoreنہیں۔
-713baselinepathsبرقرار؛115دوسرےCore service/data filesunchanged۔ **30نئےfacts/کل1945C#casesUNRUN۔**

## حدود اور باقی کام

یہphotosکاlimitedsourceحصہہے،پورےNative/customfields/session/recordauthorizationکیcompletionنہیں۔ ابتدائیجائزےمیںانکےکئیرستوںپرcurrentchecksملے؛باقیvalues/editors/callers/staleoutputsابھیجانچنےہیں۔ دوسرےpagesکیcachedcovers،shellavatar،اصلsigned-insessionاورپراناکاغذ/PDF/clipboardبھیپورےحلکادعویٰنہیں۔

CoverPathfilesystemhelperauthorizationنہیں۔ Extension/12MBcheckاصلimagecodecvalidationنہیں؛symlink/reparsepoints،file-replacement/collision،cleanupfailure،powerlossاورcross-instanceconcurrencyکیحقیقیجانچباقی۔ TestsکیsyntheticbytesکوحقیقیWindowsتصویریاscreenshotنہسمجھیں۔

حساب،SupplierB،الگCost/Profit/main،Extra/below-cost/overpayment،Blackbox/BookCheck،storedauditاورbackuppasswordA/formatنہیںبدلے۔ آگےباقیfields/record/session،fault/retry/concurrency،files/cloud/restore/new-PC،Windows1945+WPF/hardware/screenshotsاورfixes؛پھرمجاز **واحدsendabledouble-clicktestinstaller**۔ **ابھیinstallerنہیںبنا۔** مزید **3–6مربوطreview/resultچکر** مشروطاندازہہے،حدیاوعدہنہیں؛نئیfindingسےبڑھسکتا۔
