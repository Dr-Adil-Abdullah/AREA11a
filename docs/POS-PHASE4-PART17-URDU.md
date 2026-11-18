# حصہ17 — Supplier حساب، Pay اور SR

**v0.95.0 Avocet · 2026-11-10 · PT-37**

## آپ کے منتخب B کے مطابق

مثلاً خرید **2000**،ادا **500** اورباقی **1500** ہے۔ جس شخص کو **Suppliers page** کی اجازت دیں گے،وہ یہ حساب،statement،Pay اورSR کا حساب دیکھ/استعمال کرسکے گا،چاہے **View Purchase Cost یا main switch بند** ہو۔ ان رقموں،یاSR total/quantity سےخریدcost کااندازہ نکل سکتا ہے؛آپ نےیہاستثنا قبول کیا ہے۔ یہ فیصلہ دوبارہ نہیں پوچھنا۔

**براہِ راست دوا/batch کی cost اورtray کی cost-preview اب بھی الگ Cost + main ON سے۔** Manager یاProfit-only کافی نہیں۔ صرف Supplier اجازت والا شخص دوا/batch/مقدار سےreturn tray بناسکتا ہے؛قیمت hidden،کل میں quantity-only پیغام،اصلknown0 نہیں۔ Save پرservice اصل موجودہ batch/product cost سےcredit یاcash settlement بناتی ہے؛hidden0 کوقیمت بناکرstock/account نہیں بدلتی۔ بعدکاsettlement amount B کےتحت نظرآئےگا۔

- Pay/SR کےtransaction کےاندر موجودہ active account،وہیrole اورSuppliergrant دوبارہ چیک؛Audit میں موجودہstored نام۔ پراناcached Owner/manager label کافی نہیں۔
- Overpayment،zero سےکمstock کی ممانعت،cash کےبدلےcredit کااصول،numbering اوراصلposting calculation نہیں بدلے۔
- Account/statement/profile sharing تازہaccount سے؛deny پرپرانیlist/account/selection/tray صاف۔ Cost/main بدلنے پرpicker صاف،tray quantities برقرار مگرpreview masked؛اجازت واپس پرline دوبارہ add کرکےتازہpreview لیں۔خودSave یاrepricing نہیں۔
- Active Supplier view میں nominal3second check؛unload پرtimer بند۔ Dispatcher/Settings cache/پہلےبنائیfile یاcopy کی فوریواپسی کی ضمانت نہیں۔
- الگ Excel/CSV editable-file permission،Audit detail کےدونوںfinancialgrants اورpassword backup A تبدیل نہیں ہوئے۔

## واقعی کیا جانچا؟

| جانچ | نتیجہ |
|---|---|
| Static source/grammar/member/XAML checks | **84 PASS**؛compiler نہیں |
| جان بوجھ کرغلط source تبدیلیاں | **13 رد**؛اصلbytes بحال |
| Synthetic memory SQLite probes | **12 PASS**؛اصلSQL،نہC#service |
| الگ Python backup model | **6 round trips +24 negatives PASS**؛DPAPI نہیں |
| نئے C# tests | **24 facts لکھے**؛کل **1740 cases UNRUN** |
| Windows/WPF/DPAPI/new-PC restore/installer | **UNRUN؛installer نہیں بنا** |

684baselinefiles برقرار؛112دوسرےCore service/data hashes exact؛privateSaveReturn سےآخر،اورپرانے16SupplierSQLblocks unchanged۔ [تفصیلی حدود/trace](POS-PHASE4-SUPPLIER-ACCOUNTING-REVIEW.md)۔ یہglobal audit/securityPASS نہیں۔

## PT-37 — صرف واقعی الگ test ماحول میں

**Live shop پر فرضی خرید،Pay،SR،stockchange یاrestore نہ کریں۔** پہلےverifiedbackup اورواقعیالگVM/DB/photos؛دوسریexefolder الگdata نہیں۔ Windows میں `tools\RUN-TESTS.cmd` کااصلپوراlog محفوظ کریں؛متوقع **1740 cases**۔ WPF build/launch الگ بھی چیک کریں۔

1. Synthetic خرید2000،paid500،due1500 بنائیں؛Supplier-only staff کےaccount/statement/bulkstatement/profileمیںیہحساب آئے،Cost/mainOFF کےباوجود۔ خریدpaidکوstandalonepayment سمجھ کردوبارہجمع نہ کرے۔
2. اسیstaff سےmedicine search،batch اورexpiredquick-add؛quantity/pack درست،directcost/rowvalue/کلpreviewhidden۔ tray میںunknownاورknowncostملےتوpartialtotalکوصحیحgrandtotalنہکہے۔
3. Cost grant+mainON پرتازہbatchpreview؛Profit-only/Manager سےdirectcostخودنہکھلے۔ Owner کاCost بند،یاmainOFF پرOwnerکےdirectfieldsبھیhidden،مگرSupplieraccount/Pay/SRجائز۔
4. حقیقیcost0 fixture میںمجازknown0،بغیراجازتhidden۔ Batchcost0/productcostمثلاً7654ہوتوسرورکااصلfallback7654برقرار؛hidden0کونئیقیمتنہبنائے۔
5. Tray بنانےکےبعدbatchcostبدلیں؛Save موجودہservercostسےsettleکرے،پرانیpreviewسےنہیں۔ CreditSRسےpayableکم،cashSRسےpayableبرقراراورcashwarning؛cashrefundکوstandalonepaymentsمیںنہڈالے۔
6. موجودہdebtسےزیادہPayاورavailablebatch/productstockسےزیادہSRرد؛نہpartialheader/stock/payment،نہburnednumber۔ Failedsaveکوبغیرنتیجہملائےدوبارہنہچلائیں۔
7. TrayکےبعدSuppliergrantrevoke،accountdeactivateیاstoredroleبدلیں؛اگلےcheck/interactionپرlist/account/movements/expired/search/tray/bulkselectionصاف،Pay/SRرد۔ Grantواپسپرloadہو،پرانیtrayخودpostنہہو۔
8. صرفCost/mainبدلیں؛trayquantity/IDsبرقرارمگرdirectpreviewsصاف۔ Pickers/expiredselectionتازہہوں؛lineدوبارہaddسےنیاknownpreview۔یہ3secondسختdeadlineنہیں۔
9. UnknownID،inactivecaller،forgedOwnerrole،missing/wrong/inactivebatch/productاورemptybatchfixturesرد؛دوسرےاصلaccountIDکاobjectخودauthenticationنہیں۔
10. StoredنامبدلکرPay/SRکریں؛Auditمیںتازہstoredنام۔ اسیisolatedDBمیںauditfailureinjectکریں؛payment/SR/stock/payable/numberسبrollback،screenپرrawSQL/حساسexceptionنہیں۔
11. Statement/preview/WhatsApp/profileبنانےسےپہلےgrantrevokeکریں؛نئیoutputرد۔ پہلےسےبنیfile/clipboard/کھلاpreviewخودواپسdeleteہونےکادعویٰنہیں۔
12. Currentaccountکیquery/DBfailureپرsuccessful0یاپرانےحسابکوتازہنہکہے؛desktopپرpermissionhelp/hoverمیںBاستثناواضحہو۔ [PT-35 backup](POS-PHASE4-PART15-URDU.md) اور[PT-36 StockMinus](POS-PHASE4-PART16-URDU.md) بھیابھیلازمopengatesہیں۔

## کیا باقی ہے؟

یہSupplierکامsourceمیںشاملہے؛**مکملPhase4اورinstallerابھیباقی**۔ باقیoutputs/BookCheck،session/record/staleaccess،concurrency/duplicates/faults،files/cloudاوراصلWindows/DPAPI/restoreنتائجدرکارہیں۔ تقریباً **3–6 مزید مربوطreview/resultچکر**؛نئےنتائجسےبڑھسکتےہیں،وعدہنہیں۔ ہرportionکےساتھdocsاوراسیbranchپرcommit/pushلازم؛remoteverificationکااصلنتیجہہیpublicationکاثبوتہے۔
