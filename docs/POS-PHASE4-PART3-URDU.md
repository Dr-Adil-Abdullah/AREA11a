# فیز4 — حصہ3: کھاتے کے read/statement راستوں کی اجازت

**v0.81.0 Curlew (`#4F566E`) · 2026-11-10**

**فیز4 ابھی جاری ہے، مکمل audit/security PASS نہیں۔** اس حصے میں customer/supplier کھاتے کے مخصوص read methods اور اُن کے callers درست کیے؛ catalog اور دوسرے cost-bearing راستوں کا مکمل جائزہ ابھی باقی ہے۔

## پہلے خلاصہ

- Customer/Supplier کی list، منتخب account، statement، customer کی monthly statement، bulk statements اور supplier کے expired batches اب Core میں بھی پڑھنے والے صارف کی شناخت لیتے ہیں۔ پرانا actorless public overload نہیں چھوڑا۔ shop کے بٹن/feature نہیں ہٹائے؛ اندرونی callers ساتھ بدلے ہیں۔
- پڑھنے کے وقت database کا موجودہ active account، اس کا role اور متعلقہ **Customers / Suppliers** permission چیک۔ stale/بدلا role، بند یا غائب account اور صرف جعلی owner label سے انکار۔
- Customer کا explicit profit صرف screen پر نہیں، returned list/account data میں بھی mask؛ global profit switch اور الگ profit permission دونوں ضروری۔ denied حالت میں flag=false، عدد صفر اور formatter “hidden”، اصل محفوظ منافع صفر نہیں کیا۔
- PDF/WhatsApp/print/month/bulk کی document preparation بھی اسی caller کو لے کر چلتی ہے۔ customer statement پہلے کی طرح خود مالک کے لیے بھی profit-free ہے۔
- bill، balance، payment، stock، Extra Discount، اصل cost floor یا560/540 واپسی کے حساب میں تبدیلی نہیں۔ installer نہیں بنایا۔

## روزمرہ مثال

کیشیئر کو Customers کھاتہ پڑھنے کی اجازت ہے؛ وہ معمول کا کھاتہ اور statement لے سکتا ہے۔ لیکن Suppliers کی اجازت نہیں تو صرف supplier کا ID دے کر اندرونی method سے اس کا account/statement/expired-cost list نہیں ملنی چاہیے۔ پہلے بعض methods صارف لیتے ہی نہیں تھے؛ اب متعلقہ اجازت خود check کرتے ہیں۔

مالک کسی شخص کو Suppliers کی واضح اجازت دے تو پہلے سے موجود جائز workflow برقرار ہے؛ ہر cashier کو supplier data نہیں دیا گیا۔ بعد میں اجازت بند کرے تو اگلی read request انکار کرے گی۔ پہلے سے کھلی/کاپی/پرنٹ/export ہو چکی معلومات مٹانے کا دعویٰ نہیں۔

فرض کریں customer کی فروخت300، cost200، profit100۔ profit permission نہ ہو یا global switch OFF ہو تو Customer list/account کے explicit profit field میں100 واپس نہ آئے؛ balance/فروخت/loyalty اپنی اجازت کے مطابق برقرار۔ ON اور جائز permission سے اصل100 پھر دکھے؛ کوئی نئی sale یا پرانے record کی rewrite نہیں۔

## اہم حدود

یہ authentication/session نظام کی مکمل replacement نہیں۔ کسی arbitrary in-process caller کے پاس اصل مالک کا ID خود دینے کی طاقت ہو تو یہ ID check اس کی authentication ثابت نہیں کرتا۔ ہر open session فوراً بند کرنے، جاری job کے درمیان فوری revocation یا ہر DB error کی fail-closed ضمانت نہیں۔ PermissionService کے سابق role defaults/DB-error policy اور factory recovery نہیں بدلے۔

یہ صرف explicit customer profit masking اور ان دو ledger services کا fix ہے۔ Catalog، Reorder کی last cost، Reports کے raw costs، Product history کی خرید والی rows، Dashboard اور دیگر exports الگ باقی review ہیں۔ قیمت/مجاز cost یا profit-based رعایت سے اخذ ہونے والی معلومات کے خلاف مطلق secrecy کا دعویٰ نہیں۔

## جانچ کی اصل حیثیت

**20 نئے C# facts؛ کل1357 لکھے ہیں، یہاں C# نہیں چلائے** کیونکہ .NET SDK/Windows موجود نہیں۔ source/static checks، Python SQLite اور اردو MD/HTML parity الگ ثبوت ہیں؛ ان کا کامیاب ہونا compile/xUnit/حقیقی shop/printer PASS نہیں۔ اصل executed نتائج [technical register](POS-PHASE4-LEDGER-REVIEW.md) میں الگ درج ہیں۔

**واقعی چلنے والے checks:**69 static checks کامیاب؛9 منفی source probes پکڑے گئے، اصل source بحال کرکے guard کامیاب۔ دونوں ledger services کے اصل8+16 verbatim SQL متن اور migrations unchanged؛ الگ SQLite proof میں36 actual scripts، history، BookCheck corruption detection اور B560/540 کامیاب۔ اردو MD/HTML main یکساں،36 منفرد lesson anchors، کوئی remote script/style/image asset نہیں؛ rules matrix تازہ، diff check صاف۔ یہ C# یا حقیقی hardware tests کا بدل نہیں۔

نئے tests میں anonymous/denied/forged-role/inactive/missing/demoted readers، عام جائز cashier، owner grant/revoke، existing payment receipt، list/page profit masking، global switch، monthly/full/bulk documents اور unknown-ID/no-partial-job cases شامل۔ سابق financial tests کو وہی مجاز owner اب explicit argument کے طور پر دیا؛ اُن کے مالی expected نتائج نہیں بدلے۔

## Windows پر محفوظ مشق

1. پہلے verified backup اور واقعی الگ database/photos/configuration کی تصدیق۔ دوسرے exe folder کو خودبخود الگ shop نہ سمجھیں۔ اصل دکان پر fake bill/return/payment نہیں۔
2. version **v0.81.0 Curlew** دیکھیں؛ `tools\RUN-TESTS.cmd` میں **total1357، failed0** مطلوب۔ output محفوظ کریں۔ Desktop build/launch الگ verify ہو؛ static result اس کا بدل نہیں۔
3. الگ test shop میں عام cashier سے Customers کھولیں: search، account/Books match، statement preview، monthly statement اور bulk selection پہلے کی طرح چلیں۔ owner کے محفوظ sample account سے balances/movements ملائیں؛ اصل shop میں synthetic financial transaction نہ بنائیں۔
4. owner/manager سے جائز Suppliers کھولیں: list/account/expired batches، single/bulk statement اور PDF/WhatsApp کی preparation چلیں۔ صرف document تیار کریں؛ کسی حقیقی شخص کو test WhatsApp پیغام نہ بھیجیں۔
5. test staff کی Customers/Suppliers permission پہلے واضح grant پھر بند کرکے اگلی refresh/account/statement درخواست آزمائیں۔ menu چھپنا ایک چیز، Core negative cases خودکار tests میں الگ ہیں۔ اگر پہلے کھلی معلومات باقی نظر آئے تو اسے فوری session-revocation PASS نہ سمجھیں۔
6. global profit switch OFF کریں، مجاز test account دوبارہ load کریں؛ explicit customer profit hidden، sales/balance/loyalty/history unchanged۔ ON پر صرف profit-authorized شخص کو اصل عدد واپس۔ global switch عام customer statement میں profit شامل کرنے کا اختیار نہیں۔
7. permission denial کے بعد پرانے selected record کی print/PDF/WhatsApp preparation سے نیا unauthorized document نہیں بننا چاہیے۔ پہلے سے allowed حالت میں تیار preview/file کا خودبخود مٹ جانا اس fix کا حصہ نہیں۔
8. scanner/printer/paper، Windows DPI/keyboard، background session اور ہر export کے عملی ثبوت الگ باقی ہیں۔ حقیقی screenshot/error دیں؛ فرضی screenshot شامل نہیں۔

## مسئلہ یا واپس ترتیب

مالک صحیح account/permission مقرر کرے؛ stale role ہو تو صحیح active account سے دوبارہ sign in۔ global profit switch صرف مجاز طریقے سے بحال؛ باقی permission پھر بھی درکار۔ انکار دور کرنے کے لیے database/code bypass یا ہر شخص کو Suppliers دینا حل نہیں۔ پرانی binary نئی database پر چلانا یا پرانا backup اوپر ڈالنا محفوظ rollback نہیں۔ یہ نئی schema والی تبدیلی نہیں، پھر بھی پرانا backup ڈالنے سے نئی دکان کا data ضائع ہوسکتا ہے۔

## اگلا حقیقی باقی کام

- Product catalog کے safe/authorized-cost نتائج اور receiving/pricing/UI/export callers — اس حصے میں انہیں مکمل محفوظ قرار نہیں دیا۔
- Report/Reorder/ProductHistory/Dashboard کے باقی cost/profit projections، Custom fields، TimeCorrection اور عمومی session/record boundaries۔
- مکمل finance/stock/concurrency/double execution، inputs/files/SQL/HTML/archive، backup/cloud isolation،36 اسباق کی ہر action اور Windows/hardware جانچ۔

[تمام pages/15 نکات کا register](POS-PHASE4-EVIDENCE.md)، [پچھلا method matrix](POS-PHASE4-ACCESS-REVIEW.md)، [اس حصے کے شواہد](POS-PHASE4-LEDGER-REVIEW.md)۔ بڑی feature removal یا نئی کاروباری پالیسی پہلے مثال اور آپ کے فیصلے کے بغیر نہیں۔

**محفوظ رکھنے کی حیثیت:** آغاز میں سابق code بہت سی uncommitted تبدیلیوں کے ساتھ ملا۔ انہیں reset/delete نہیں کیا؛ اس حصے کی تبدیلیاں بھی اسی branch کے workspace میں محفوظ ہیں۔ اس حصے کے commit/push کا دعویٰ نہیں۔
