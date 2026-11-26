# ⏳ Native WPF رہنمائی — پہلا محدود حصہ، v0.137.0 Amaranth

## منظوری اور حد

مالک نے browser نمونہ دیکھنے کے بعد پہلے حصے کیلئے **A: native WPF** منتخب کیا۔ صرف چھ حصوں کی فہرست، پہلے سے موجود No Bill/Printing/Login کے تین مکمل نمونے اور باقی مخصوص عنوانات کی جگہ منظور ہے۔ اسے پوری نئی کتاب، پورے پروگرام کی certification یا باقی 5.1 کا اختتام نہ سمجھیں۔ کوئی نیا module/navigation tab یا Settings index نہیں بنایا؛ .NET10 برقرار، WebView2/runtime dependency نہیں۔ حصہ6،installer،مالی قواعد،کاروباری services،موجودہ tests یا37migrations نہیں بدلے۔

## ⏳ Source میں کیا شامل ہے

- Dashboard کا Guidelines بٹن: چھ حصوں والی پوری utility سطح،× سے بند۔ اصل CurrentViewModel/view وہیں attached رہتا ہے؛ کوئی Save/Cancel/Refresh یا کاروباری navigation نہیں چلتی۔ سطح کھلی ہو تو نیچے کا business surface عارضی disabled ہے،keyboard tab گائیڈ کے اندر رہتا ہے؛ بند ہونے پر اصل controls دوبارہ قابل استعمال ہوتے ہیں۔
- چھ اصل حصے: سیل،سٹاک،کسٹمرز،منی،سسٹم،شروعات اورعام باتیں۔ کوئی Emergency section یا فرضی Signup نہیں۔
- متعلقہ صفحے/heading/header button پر Right-click میں الگ **Help** اور **Docs**۔ Help میں «Help ابھی تیار نہیں»؛Docs اسی موضوع کی الگ،درمیانی،owned **modeless** window ہے۔ اصل فارم کو Docs کیلئے بند یا دوبارہ نہیں بنایا جاتا؛× اس Docs window کو بند کرتا ہے۔ ایک اصل window کی ایک Docs window دوبارہ استعمال ہوتی ہے۔
- TextBox/RichTextBox،PasswordBox،DataGrid،pickers اور موجودہ ContextMenu کو نہیں بدلا؛Copy/Paste اوراصل row actions کیلئے وہی native راستہ ہے۔ ان controls کے بجائے متعلقہ heading/background پرDocs استعمال کریں۔ Settings/Partners/Returns/Doctors/Import-Export/Catalog کے اصل tab labels بھی موجود ہیں؛جہاں الگ مضمون نہیں وہاں متعلقہ خاص placeholder ہے۔
- پہلے سے focus رکھنے والے control کا صرف weak حوالہ رکھا جاتا ہے؛وہ اب بھی اسی active owner میںvisible/enabled ہو تبھی واپسی کی کوشش۔ Window کو بلاوجہ activate نہیں کرتا۔ Session expiry،permissions،screen lock اوراصل app security کو bypass نہیں کرتا؛security کی جائز clearing کو Docs واپس نہیں لاتا۔ Focus/selection کی عملی جانچ ابھی ضروری ہے۔
- پہلے Guidelines/Docs request پر `Guide/guide.json` پڑھی جاتی ہے؛app startup پرGuideLibrary،guide index/tree یاviewer instantiate نہیں ہوتے۔ ایک کھلی view کیلئےایک bounded read،کوئی DB،AI یاHTML execution نہیں۔ عنوان/path/تین مضامین کے متن اورtable cells میں search؛نتیجہ خود نہیں کھلتا۔
- Native reader میں FlowDocument،paragraphs/headings،tables اورمتعلقہ-topic buttons ہیں۔ Font کیلئےWindows کی Segoe UI/fallback استعمال ہے؛اصل Urdu shaping/چھوٹی سکرین/print نتیجہ Windows پر دیکھنا ہے۔
- مواد غائب یا خراب ہو تو viewer کے اندر واضح پیغام؛اصل کاروباری فارم کی کارروائی نہیں چلتی۔ Unknown mapping پر چھ حصوں کی محفوظ فہرست،فرضی مضمون نہیں۔

## ✅ ایک ماخذ اور محدود آف لائن کتاب

`docs/guide-prototype/index.html` کا catalog اورتین article HTML اصل authored source ہیں۔ `tools/build_native_guide.py` انہی سے یہ دو artifacts بناتا ہے:

1. `dotnet/Area11.Desktop/Resources/Guide/guide.json` — Desktop output/publish میں `Guide/guide.json`؛Core tests بھی اصل packaged data کیcopy پڑھتے ہیں۔
2. `docs/guide-prototype/book.html` — خودکفیل offline **محدود نمونہ**؛چھ حصے اوروہی تین مضامین،باقی عنوانات placeholders۔ پورے پروگرام کی نئی مکمل کتاب نہیں۔ Ctrl+F تلاش،Ctrl+P browser print/PDF۔ Root کا `URDU-GUIDE.cmd` صرف یہ فائل کھولتا ہے،نہbuild نہupdate نہshop data۔ یہ command یہاں Windows پر نہیں چلائی گئی۔

Docs revision **R2** ہے؛reviewed source کی تاریخ **2026-11-10** برقرار ہے۔ R2 native/offline اشاعت اورحیثیت کے نوٹ کیتبدیلی ہے،نئی مکمل کتاب یاWindows review کیتاریخ نہیں۔ App کا الگ version **v0.137.0 Amaranth**،brand `#386C69`،dark `#274B49`،soft `#EDF5F4` ہے۔ Embedded licensed Noto Naskh Arabic font کےbytes browser source کےپچھلے published checkpoint جیسے ہیں؛native میںنیا font/runtime نصب نہیں ہوتا۔

ماخذ بدلنے کے بعد:

```text
python3 tools/build_native_guide.py
python3 tools/build_native_guide.py --check
python3 tools/check_native_guide.py
python3 tools/check_repo.py
```

## جانچ کی حیثیت

- ✅ Native guide کی **919 static/data assertions** پاس: الگ HTML text parser سےتینوں اصل مضامین کاہر visible text fragment،tables/related links،چھ roots،hierarchy،mapping IDs،offline anchors/font/license،package/source read-only/focus-containment boundaries۔ یہ source/data check ہے،WPF behavior کاtest نہیں۔
- ⏳ **19 نئے Core C# facts** لکھے ہیں:چھ roots/اصل packaged samples،metadata،children،unknown topic،article/table/title/Urdu-alias search،empty search،missing file،duplicate/null topic،missing parent/grandparent/link،cycle،unknown block،invalid date،seventh root۔ ابھی compile/run نہیں ہوئے؛کل suite **3416** متوقع ہے۔ `tools\RUN-TESTS.cmd` اسی count پرہے۔
- ✅ مالک کاپہلے **v0.136.2 Campion** کاWindows نتیجہ3397PASS/0FAIL/0skip،8m56s؛Debug build0errors/30warnings،1m1.82s الگ تاریخی ثبوت ہے۔ اسےpending نہیں کہا جارہا اوراسےاس نئے source کی3416/WPF کامیابی بھی نہیں کہا جاسکتا۔
- ⏳ یہاں `dotnet` SDK اورWindows نہیں؛نئی Core/Tests/Desktop build،3416xUnit،WPF bindings/ContextMenu/focus/selection/lock اوراصل native rendering کاکوئیruntime دعویٰ نہیں۔

- ✅ مرکزی repository کے **129 static checks** پاس؛465 C# grammar files،4938 calls/10492 references کیstatic resolution،صفر مسئلہ۔ یہ.NET compile نہیں۔
- ✅ browser prototype کے42assertions دوبارہHTTP/file:// پرپاس؛محدودoffline book کے12browser checks بھیپاس،جن میں96topic anchors،تینsample tables،embeddedfont،کوئیHTTPrequest/script نہیں اورnonemptybrowserprintPDF شامل ہیں۔ PDF جانچ کیعارضیfileہے،repo/deliverableمیںشامل نہیں؛یہ Windowsprinter یاnative WPF کیجانچ نہیں۔

## ⏳ Windows پر محفوظ جانچ — نئی اصلی shop entry بنائےبغیر

1. نئی v0.137.0/Amaranth شناخت دیکھیں؛Debug build اور`tools\RUN-TESTS.cmd` کا3416total/0failed/0skipped نتیجہ بھیجیں۔ موجودہ30warnings کوحل شدہ نہسمجھیں۔
2. Dashboard→Guidelines پہلی دفعہ کھولیں:چھ cards،کوئی مضمون خود نہکھلے۔ Search سےNo Bill/Printing/Login اوراندرکےمنفردالفاظ/table cells تلاش کریں۔ باقی عنوانات کاخاص placeholder اورrelated buttons دیکھیں؛× سےواپسDashboard۔ کوئی نیاNav/Settings tab نہہو۔
3. **صرف غیرمحفوظ تحریر**:کسیجائزform میںفرضی نام/نوٹ/quantity/amount اورselection رکھیں،کوئی Save/Post/Receive/Pay نہکریں۔ Heading/background→Right-click→Help پھرDocs؛اصلform modeless طورپرکھلارہے۔ Docs بندکرکےوہی تحریر/selection/focus دیکھیں؛keyboard navigation سےچھپےاصلform کاbutton نہچلے۔ غیرضروریEdit بھیآخرمیںاصلappکےمعمولکےمطابقخودختمکریں۔
4. Printing Settings header→Docs،Login heading→Docs اورNo Bill heading→Docs درستمضمون کھولیں۔ Partners/Settings کےnested tabs اورحقیقی header buttons کاخاصموضوع دیکھیں۔
5. TextBox میںCopy/Paste،grid میںاصلrow menu/selection،pickers کےاپنےmenus برقرارہوں۔ ContextMenu کوپہلیبارMouse اورkeyboard menu key/Shift+F10 سےبھیآزمائیں۔ باربارDocs کھولنےپرایکownerکیایکwindowہو؛دوسرےاصلwindowکیالگownership۔
6. Docs فعالہونے،Alt+Tab،عامappclose اورموجودہsecurity/idlepolicyکوسکون سےجانچیں؛session/logoutکیجائزformclearingکوrestoreنہکرے۔ Docsکھولنےکیلئےlock/permission بندنہکریں۔
7. الگtestcopyکےoutputمیںguide.jsonعارضیغائب/خرابکرکےپیغاماور×دیکھیں،پھراصلfileبحالکریں؛live shopکوہاتھ نہلگائیں۔ Publish/outputمیںGuide/guide.jsonضرورآئے۔
8. `URDU-GUIDE.cmd` اورbook.htmlکوofflinebrowserمیںکھولیں،چھحصوںکےlinks،تینمضامین،Urdufont،Ctrl+F/printpreviewدیکھیں؛حقیقیprinter/jobبھیجنااسحصےکیشرطیااجازت نہیں۔

اگلاcode/contentحصہصرفنئیمنظوری سے؛پوری کتاب،startupکیباقیاصلاحات،5.1کیباقیservicesاور6/installerاسcheckpointسےمنظورنہیںہوتے۔
