# فیز4 حصہ12 — Expense/Drawings میں cash اور خرید کی valuation الگ

**v0.90.0 Dipper،2026-11-10۔ PT-32 حقیقی Windows پر UNRUN۔** Source checkpoint ہے،installer یا production منظوری نہیں۔

## آسان مثال

کرایہ250 روپے اور مالک کی نقد نکاسی70 روپے ہو تو گلے سے320 روپے نکلے۔ مالک نے3 tablets گھر لے لیں،فی tablet خرید123.45: مال کی valuation370.35 روپے،لیکن **گلے سے مزید370.35 نقد نہیں نکلے**۔

اب Expenses کی list،Today،goods-drawing کے نئے save result اور نئے voucher میں یہ خرید والی370.35 **الگ View Purchase Cost + main ON** سے ملے گی؛منع ہو تو raw amount mask اور label **hidden**۔ کرایہ/نقد drawing اور cash total320 متعلقہ Cash اجازت کے تحت برقرار۔ Manager یا صرف Profit tick خرید کا بدل نہیں؛main OFF اور owner کی اپنی cost denial بھی goods پر نافذ۔

Expense کا Stock goods category tag **صرف label** ہے:نہ stock آتا،نہ payable بنتا،نہ اس user-entered cash amount کو batch cost سمجھا گیا۔ واقعی متعلقہ رقم user اپنے note/title/extra answer میں لکھ دے تو خودکار طور پر scrub کرنے کا دعویٰ نہیں۔

## اصل accounting کی حفاظت

Goods save اندرونی اصل cost سے stock کم اور اصل amount/audit محفوظ کرتا ہے؛صرف باہر ملنے والا result mask ہوتا ہے۔ Cash drawings مالک ہی کی،عام expense Cash grantee کی؛نیا current-account/page check transaction میں write سے پہلے۔ Cost visibility سے نئی blanket posting پابندی نہیں بنائی۔ No-shift warning اور cash-vs-goods الگ حساب برقرار۔ کسی بعد کی permission query کو commit کے بعد ناکام کرکے دوبارہ Save کی ضرورت پیدا نہیں کی۔

Goods voucher کو cash kind کہہ کر اصل رقم نہیں نکالی جاسکتی؛query کا موجودہ type predicate برقرار۔ Read/query ناکام ہو تو آج کی رقم “معلوم0” نہیں بلکہ hidden؛voucher تیاری fail،اصل SQL details نہیں۔ Refresh پر پرانی selected history row صاف ہوتی ہے؛پہلے تیار file/status/object واپس recall کی ضمانت نہیں۔

## محفوظ PT-32 — صرف الگ synthetic data / VM

اصل shop پر fake expense/drawing مت کریں۔ Verified backup اور واقعی الگ DB/PC چاہیے؛الگ exe folder isolation نہیں۔ پہلے سے تیار test fixture استعمال کریں۔

1. `tools\RUN-TESTS.cmd` سے1579cases کا اصل log؛پھر `dotnet build Area11.sln -c Release` سے Desktop بھی۔ یہاں C#/Windows نہیں چلا۔
2. Cash grant+Cost/main ON،Profit OFF پر تین mixed entries اور voucher:expense25000paisa،cash7000،goods37035؛Today cash32000،goods الگ،entries3۔
3. Cost OFF/Profit ON،manager،main OFF،owner explicit denial پر نئی List/Today/goods voucher میں **hidden**؛expense/cash voucher اور cash32000 برقرار۔ اصل مجاز goods0،hidden0 سے الگ AmountKnown/GoodsCostKnown حالت میں ہو۔
4. Cash page grant بند/inactive/role-changed account سے نئی reads/voucher انکار؛Cost/Profit tick page کا بدل نہ بنے۔ Permission DB failure میں owner کو goods fallback نہ ملے،ordinary authorized cash پھر بھی اپنی Cash policy پر۔ یہ مکمل session authentication کا ثبوت نہیں۔
5. Test-service میں goods ID کو cash voucher kind سے مانگیں؛ناکام۔ حقیقی cash ID کو goods kind،unknown kind،missing ID بھی غلط document نہ دیں۔ Query failure پر successful empty voucher/SQL error leak نہ ہو۔
6. صرف الگ fixture میں main OFF پر owner goods save؛returned amount hidden،stored goods37035،stock10سے7،مگر drawer cash unchanged۔ No-cost owner cash drawing70 بدستور readable/correct؛Cash-grantee rent بھی صحیح محفوظ۔
7. Goods insert/audit کا injected failure fixture مکمل rollback کرے؛stock نہ گھٹے،fake masked success نہ آئے۔ Inactive/forged-owner snapshots سے cash/goods writes اور inactive expense writer انکار۔
8. Stock-tag والی cash expense پھر بھی label-only،نہ stock بڑھے نہ Purchase/payable بنے۔ Read/voucher سے پہلے/بعد DB counts/amounts unchanged۔ اصل Windows voucher/selection/labels کے screenshots/logs ہی ثبوت ہیں۔

## یہاں کیا چلا / باقی کیا ہے؟

**79 static checks PASS**؛390C#grammarfiles،4098calls/7898references،0reportedissues۔10 غلط source mutations رد/بحال۔ نیا isolated in-memory SQLite probe36اصل migrations/SELECTs،cash/goods الگ totals،typed/bound IDs،read-only/FK PASS؛سات پرانے probes بھی PASS۔ یہ C#/WPF/security PASS نہیں۔

**28 نئے facts،کل1579 C# cases UNRUN۔**12 SQL blocks اور اصل SaveGoods exact؛110دوسرے Core service/data hashes unchanged؛تمام656 baseline files retained۔ کوئی installer نہیں بنا۔

اگلا:purchase entry/drafts/results،پھر باقی generic exports/audit۔ Ordinary Supplier paid/payable/Pay/SR فیصلہ،StockMinus/session/fault/files/backup/cloud/Windows/fullPhase4 بھی باقی۔ Generic CSV/workbook کی حفاظت اس voucher fix سے خود مکمل نہیں ہوئی۔ Docs+commit/push ہر مرحلے کی ہدایت برقرار۔

[تفصیلی trace/حدود](POS-PHASE4-EXPENSE-VISIBILITY-REVIEW.md) — [باقی کام اور test installer](REMAINING-WORK-AND-TEST-INSTALLER-URDU.md)۔
