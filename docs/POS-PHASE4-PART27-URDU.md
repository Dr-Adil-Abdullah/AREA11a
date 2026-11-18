# PT-47 — نمبر5/6/7: Customer اور Product کی موجودہ اجازت / پرانا editor

**v0.105.0 Harbor · `#466B65` · 2026-11-10 · source checkpoint؛ Windows/C# UNRUN**

[مستقل1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md) · [source trace / ثبوت کی حدود](POS-PHASE4-RECORD-WRITE-CURRENT-REVIEW.md)

## 1۔ اس حصے میں کون سے نمبر لیے گئے؟

1. **نمبر5 — موجودہ account/role:** customer edit اور product create/update/put-away کے مخصوص راستے؛یہ مکمل authenticated-session نظام نہیں۔
2. **نمبر6 — parent record:** optional custom answers نہ بھی دیے جائیں تب customer کی اصل تبدیلی سے پہلے اجازت؛product میں موجود/درست target،stored actor سے audit،stored cost کے لیے موجودہ Cost+main شرط۔
3. **نمبر7 — stale editor:** customer کی intended ID pin کرنا؛اجازت کھونے پر customer inputs/custom editor اور product cost/form/lookups صاف کرنا؛product modal میں nominal3second check اور explicit reopen۔

روزمرہ مثال: Manager نے دوا کی cost کھولی،مالک نے اس کی Cost اجازت بند کردی۔ کھلا ہوا form پرانی cost رکھ کر دوبارہ save نہ کرسکے۔ دوسری مثال: Cashier نے customer کا فون درست کیا مگر form نے custom answers نہیں بھیجے؛اس سے current-account check چھوٹنا نہیں چاہیے۔ **Cashier کی جائز phone/name correction برقرار ہے؛credit limit اب بھی Owner/Manager کے لیے ہے۔**

## 2۔ source میں کیا مکمل ہوا؟

1. **5/6 — Customer Update:** transaction میں موجودہ active/same-role account اور Customers module check اصل SaveCustomer سے پہلے؛null custom map bypass بند،audit میں current stored نام۔ POS quick-customer creation کی موجودہ policy تبدیل نہیں۔
2. **5/6 — Product writes:** Create/Update/Deactivate میں supplied active/positive manager preflight،پھر Tx میں current stored account،Products grant اور Owner/Manager شرط؛یہ checks no-op سے بھی پہلے۔ audit اسی stored actor سے۔
3. **6 — Target/cost:** Update/Deactivate کی nonpositive یا missing product ID رد؛already-hidden product کو authorized hide دوبارہ دینے پر نئی فرضی audit نہیں۔ Existing full editor کی Cost+main شرط stored-cost patch کے Core راستے پر بھی؛main setting براہِ راست DB سے پڑھی جاتی ہے،پرانے Settings cache سے نہیں۔ malformed main deny؛missing key کا shipped true default اور numeric0/1 compatibility برقرار۔
4. **6/13 کی حد:** نئی دوا کی خود typed cost لکھنے کی پہلے سے موجود workflow برقرار؛non-cost Core patch پر نئی Cost شرط نہیں لگائی۔ Profit grant،Cost کا بدل نہیں۔ UI کا موجودہ full edit اب بھی Cost+main مانگتا ہے۔
5. **7 — Customer form:** `_editingCustomerId` pin؛selection drift یا edit mode نہ ہو تو save نہیں۔ Cancel/selection/access loss پر نام،فون،category،limit اورedit custom answers صاف؛access loss پر new form کی authored fields/custom state بھی صاف۔
6. **7 — Product modal:** opening،save،history/barcodes/photos،put-away اورconfirmation کے بعد fresh preflight؛3second timer صرف کھلی window میں،Closed پر stop۔ denied/failed current read پر rates/form/target/custom state/lookups/list صاف،Save disabled؛اجازت واپس دینے سے وہ discarded form خود فعال نہیں ہوتا،واضح reopen درکار۔ failed full product read بھی پرانے form کو صاف کرتا ہے۔
7. **7/25 — committed نتیجہ:** Customer/Product save یاput-away ہوچکا ہو مگر refresh fail/throw ہو تو **SAVED — دوبارہ action نہ کریں**؛اسے DB save failure نہیں کہتے۔ Product list کی failed read کو کامیاب empty list نہیں دکھاتے۔
8. **2/3/24 — regressions:**36نئے real-schema C#facts لکھے؛پرانی CustomerEditTests میں fake role-only fixtures کو حقیقی persisted accounts کیا،assertions کمزور نہیں کیے۔ C# suite **2045 متوقع cases،سب یہاں UNRUN**۔ Python SQL/source checks کی الگ حدود trace میں۔

## 3۔ صرف الگ Windows test installation میں عملی جانچ — ابھی UNRUN

**پہلے verified encrypted backup،پھر واقعی الگ DB/photos/config؛صرف الگ exe folder کافی نہیں۔ اصل shop پر فرضی bill/customer/product/permission changes نہ کریں۔ کوئی password/PIN/اصل customer data chat میں نہ بھیجیں۔**

### 5.1۔ Customer کا null-custom-map راستہ / موجودہ account

1. الگ test customer اور persisted Cashier بنائیں؛Customers allowed،Cost/Profit بند اورmainOFF بھی ہوں تو جائز name/phone correction save ہونی چاہیے؛balance/ledger نہ بدلے۔ limit بدلنے پر Cashier کی پوری تبدیلی رد ہو؛Manager جائز limit درست کرسکے۔
2. customer edit کھلا رکھ کر test Owner سے account inactive/role changed یاCustomers deny کریں۔ اگلے access check/action پر edit نام/فون/limit/custom state صاف اورsave رد؛DB/customer/audit میں ممنوع تبدیلی نہ آئے۔
3. grant واپس دیں تو intended customer دوبارہ منتخب کرکے نیا edit کھولیں؛پرانا discarded متن خود واپس نہ آئے۔ account ID check کو login-session proof نہ لکھیں۔

### 6.1۔ Product current writer،no-op،target اورaudit

1. Owner/Manager کی جائز Create/Update/Put-away چلائیں؛stored current نام audit میں دیکھیں۔Products deny یاaccount inactive/role changed ہونے کے بعد تینوں actions اورempty patch بھی رد ہوں؛کوئی جزوی rename/rate/tag/audit نہ ہو۔
2. isolated automated tests سے missing/nonpositive product ID اورmissing customer؛already-hidden product پر دوسری authorized hide سے اضافی audit نہ بنے۔ hidden product کو full editor میں نہ کھولیں۔
3. Cost grant OFF،mainOFF،Profit-only،Cost-only کے الگ combinations کریں۔ stored cost patch تبھی جبCost+main؛rename-only Core patch پر نئیCost پابندی نہیں۔ UI full-editor پہلے سےcost دکھاتا ہے،اس لیےوہCost+main مانگتا رہے۔
4. نئی دوا میں خود typed cost والی موجودہ workflow چیک کریں۔ پہلی save کے بعد اگر existing-editor Cost access نہیں تو **SAVED + close/reopen warning** درست ہے؛دوسری نئی دوا نہ بنائیں۔ malformed setting صرف automated isolated fixture میں؛اصل DB ہاتھ سے نہ بدلیں۔

### 7.1۔ Selection،revocation،timer اورfresh failure

1. CustomerA کا edit کھولیں،متن لکھیں،CustomerB منتخب کریں؛A کا متن B پر نہ لگے،edit reset ہو۔ Cancel پر authored edit state صاف؛stable allowed access tick اپنی طرف سے typed text نہ مٹائے۔
2. Product edit کھولیں،cost/name/custom fields لکھیں۔ دوسری test window سےCost/Products/account/main بدلیں؛اگلےcommand یاnominal3second tick پر پراناcost/form/lookups صاف،Save disabled،واضح discarded/reopen warning۔ UI فوراً recall یا3seconds کی قطعی ضمانت نہ کہیں۔
3. Products grant/main/Cost واپس دیں؛کھلا invalidated editor خودsaveable نہ ہو۔ اسےبندکرکےlist refresh اورfresh edit کھولیں۔ Product کو دوسری window سےput-away کریں توpreflight editor بند/صاف کرے۔
4. failedfullGet/failedlist/failedextra-fieldread کی isolated fault exercise: پرانیcost باقی نہیں؛failedlist پرمصدقہempty overlay نہیں۔ خود نئی workingcopy میںDB بند/rename مت کریں اگر اس سےtest installation خراب ہو؛developer-controlledfaultfixture استعمال کریں۔
5. History/barcodes/photos کھولنے،ضروری-fields confirmation اورput-away confirmation کے دورانrevocation کے بعداگلاactionرد۔ پہلے سےکھلی دوسریdialogs،copiedfiles/paper یاcachedobjectواپس مٹنےکادعویٰ نہیں۔ timer modalclose پرstop؛focus/bindings/confirmationWindowsمیںدیکھیں۔

### 24.1 / 25.1۔ Rollback اورcommitted-but-refresh-failed

1. نئی36facts میں چارaudit fault tests: Customer update،Product edit+tags+cost،Product create+stagedphoto،Product put-away۔ Release suite میں واقعی execute کریں؛failed action پرDB/audit/file preservation ثابت ہو۔ Python probe ان C#tests کا متبادل نہیں۔
2. Commit کے بعد صرف reload/callback failure الگ induce کریں۔ **SAVED / دوبارہ نہ کریں**،stored record ایک دفعہ؛بعد میںrefresh سےپڑھیں۔ یہ messages universal idempotency/retry/concurrency solution نہیں۔
3. Windows پر `tools\RUN-TESTS.cmd` سے **2045 یااس سےزیادہ** cases،Core/Tests/Desktopbuild،اصلlogs اورضروریredactedscreens دیں۔ staleoldfolderکاPASS قبول نہیں۔ تمامنئےfactsیہاںUNRUNہیں۔

## 4۔ ان ہی نمبروں میں کیا باقی ہے،اورکیوں؟

1. **5:** حقیقی authenticated session binding،دوسرےcaller paths؛موجودہstored ID/role login ownership ثابت نہیں کرتا۔
2. **6:** باقیparent/workflow routes،inactive/history policies اورcross-service reconciliation؛اس حصے نے Customer Update اور تین Product writes لیے،تمامcatalog/othercommands نہیں۔ snapshot/version CAS نہیں۔
3. **7:** Windows timers/focus/bindings،otherdialogs/cache/closedview cleanup اورread-to-output races؛periodiccheck instantrecall نہیں۔ arbitrary free-text کو cost کے طورپر خود classify نہیں کرتے۔
4. **2/4/23–25:** اصل2045tests/build،پرانےWindowsfailuresretest،parallel/busy/retry/idempotency/powerloss اورintegratedfaultmatrix؛یہاں .NET/Windows دستیاب نہیں۔
5. **26–29/30–35/36:** files/backup/restore/newPC/cloud،حقیقیWindows/hardware/isolation،پھرمجازایکsendabledouble-clickinstaller۔ **Installer ابھینہیںبنا؛Phase4 مکمل نہیں۔**

ہرportionکےdocs،اسیbranchcommit/pushاورactualremoteverification لازم؛اس حصے کیاصلpublication آخریرپورٹمیں۔36مستقلنمبر،36Nextنہیں۔مشروط3–6rounds کوdeadline/upperboundنہسمجھیں۔
