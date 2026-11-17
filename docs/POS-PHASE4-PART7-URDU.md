# فیز4 حصہ7 — دو الگ اجازتیں، مشترک main switch (PT-27)

**v0.85.0 Plover — code کا محدود checkpoint، پورے پروگرام کی منتقلی ابھی جاری۔** آپ کا جواب **“keep both seprat checkbox”** محفوظ ہے؛ دوبارہ یہ انتخاب نہیں پوچھنا۔ پرانا D-109 management-only اصول اب D-110 سے بدل گیا۔ یہ Windows آزمائش کی کامیابی کی رپورٹ نہیں۔

## کیا بدلا

Settings → Staff & permissions میں مالک ہر شخص کے لیے دو الگ ticks مقرر کرے گا:

1. **View Profit** — واضح منافع اور margin۔
2. **View Purchase Cost** — خرید cost، متعلقہ خرید rate/total/valuation۔

Sales rules کا موجودہ **main visibility switch** دونوں کے اوپر ہے۔ OFF پر دونوں بند، مالک کے لیے بھی۔ ON خود کسی شخص کو اجازت نہیں دیتا؛ متعلقہ صفحے کی اجازت اور اس شخص کی الگ tick لازم۔ Manager عہدہ اب خرید cost کی خودکار اجازت نہیں۔ مالک اپنی دونوں مالی ticks بھی بند/کھول سکتا ہے؛ اس سے مالک کا Settings کا دروازہ بند نہیں ہوگا۔ عام manager اپنی Settings محفوظ کرتے وقت یہ owner-only switch submit نہیں کرے گا۔

مثال: خرید200، فروخت300، تعداد10۔ صرف خرید rate چھپا کر total2000 دکھانا غلط؛ total سے200 پھر نکل آتے ہیں۔ اس لیے اس حصے کے History میں rate، line total اور Bought خلاصہ اکٹھے چھپتے ہیں۔ **Profit اجازت سے بھی cost حساب کرکے نکل سکتی ہے**؛ دو ticks کا مطلب ریاضی ناممکن کرنا نہیں، براہ راست کون سا field واپس ملے گا یہ الگ رکھنا ہے۔ پہلے نکلی PDF، پرنٹ یا screenshot واپس نہیں مٹتا۔

## اس checkpoint میں لگے راستے

- Products catalog: cost اور margin الگ؛ margin میں denominator retail۔ cost200/retail300 پر33.3%، نہ50%۔ Profit-only پر rawcost0 masked مگر درست projected margin؛ جعلی100% نہیں۔
- Product History: خرید rate + total + Bought aggregate؛ print/preview/CSV میں بھی متعلقہ masks۔ quantity/date/supplier اپنی page permission پر برقرار۔
- Reorder: last cost الگ cost tick کے مطابق؛ quantity/supplier/order message نہیں بدلتے۔
- Reports: Dead stock کی Money at cost/Value کی raw رقم اور label؛ totals میں sale/restock cost اور purchases aggregate۔ ممنوع رقم **hidden** ہے، اصل صفر نہیں۔
- Item-price quote: cost اور explicit profit کے fresh الگ flags؛ عام sale computation اور actual batch-cost floor برقرار۔
- Product-based purchase/SR/write-off/expense preparation: خرید cost کی اجازت نہ ہو تو واضح انکار، **جعلی صفر cost دے کر bill تیار نہیں کرنا**۔

## ابھی کیا باقی ہے — اہم

**پورے پروگرام میں everywhere consistency ابھی مکمل نہیں۔** Purchase invoice، supplier ledger/expired batches، stock-take/stock-minus/expense histories، workbook/CSV/audit اور دوسرے prints/exports کا باقی rollout لازم ہے۔ بعض راستے اب بھی اپنی پرانی module authorization پر ہیں۔ Main switch کو workbook میں بدلنے سے روکنا، workbook کے اندر تمام cost چھپانے کے برابر نہیں۔ پہلے سے تیار objects/drafts اور کھلی windows بھی الگ review مانگتی ہیں۔

لہٰذا اس checkpoint کو cost-confidential production release نہ سمجھیں۔ اگلا کام انہی باقی راستوں پر یہی منظور شدہ پالیسی لگانا ہے، نہ الگ ticks کا سوال دہرانا۔ مکمل فیز4 بھی OPEN ہے۔

## محفوظ Windows مشق — ابھی چلائی نہیں گئی

اصل shop database پر فرضی purchase/sale/return/stock apply **نہیں**۔ verified backup اور واقعی الگ synthetic data directory/process درکار؛ صرف exe کا فولڈر الگ ہونا کافی نہیں۔ Cloud/LAN/auto-sync بھی اصل دکان کی طرف نہ ہوں۔ اس checklist کے لیے فرضی data الگ ماحول میں مجاز tester تیار کرے، live shop میں نہیں۔

| PT-27 | الگ ماحول میں عمل | متوقع نتیجہ | اصل نتیجہ |
|---|---|---|---|
| 01 | مالک Staff میں شخص منتخب کرے | View Profit اور View Purchase Cost الگ ticks؛ عام manager edit نہیں کرسکے | UNRUN |
| 02 | page grant، دونوں ticks OFF، main ON | named cost/profit fields hidden؛ quantity/name برقرار | UNRUN |
| 03 | صرف Profit ON | profit/margin درست، History purchase rate/total/Bought اور Reorder/DeadStock cost hidden | UNRUN |
| 04 | صرف Cost ON | خرید fields درست، explicit profit/margin hidden؛ role manager ہونا شرط نہیں | UNRUN |
| 05 | دونوں ON پھر main OFF | دونوں named financial projections چھپیں، مالک بھی bypass نہ کرے | UNRUN |
| 06 | main دوبارہ ON | اصل محفوظ اعداد واپس، کوئی نئی posting/zero rewrite نہیں | UNRUN |
| 07 | cost tick revoke کرکے نئی History/CSV/Report/quote preparation | rate اور متعلقہ totals raw/label mask، CSV purchase money cells خالی؛ پرانی فائل واپس مٹنے کی توقع نہیں | UNRUN |
| 08 | مالک اپنی Cost tick OFF | اپنی cost hidden، Settings/Staff میں دوبارہ ON کرسکے | UNRUN |
| 09 | cost OFF پر product edit / receiving preparation | واضح انکار؛ hidden0 کو اصل cost بنا کر save نہ کرے | UNRUN |
| 10 | cost/profit hidden کے ساتھ below-actual-cost sale | ہر role پر انکار؛ visibility cost floor نہیں بدلتی | UNRUN |
| 11 | manager ordinary Settings Save؛ Core/UI-only/workbook سے main switch بدلنے کی کوشش | عام مجاز settings save برقرار؛ owner-only switch bypass نہیں | UNRUN |
| 12 | حقیقی zero-cost synthetic item، اجازت ON پھر OFF | پہلے معلوم0، بعد hidden؛ دونوں حالتوں میں stored value unchanged | UNRUN |

## یہاں کیا واقعی چلا

**73 static checks PASS**؛379 C# grammar files،0 reported member problems؛12 source mutation probes پکڑے اور originals واپس۔ **24 نئے،1445 کل C# cases لکھے مگر نہیں چلائے**؛ dotnet/Windows دستیاب نہیں۔ الگ temporary SQLite میں36 migrations، پرانے financial snapshot/BookCheck/return B560540 اور time transaction lock/rollback checks PASS؛ یہ C# یا Windows security PASS نہیں۔

کوئی installer، live shop کی fake posting، commit یا push نہیں کیا۔ اصل حساب، sale-level Extra، partial return B اور سب پر actual batch cost کی پابندی unchanged۔ [تفصیلی trace، شواہد اور باقی راستے](POS-PHASE4-FINANCIAL-VISIBILITY-REVIEW.md)؛ [مکمل استعمال نامہ](USER-GUIDE-URDU.html)۔
