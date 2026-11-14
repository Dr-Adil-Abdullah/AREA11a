# POS، رعایت اور مکمل جائزہ — مالک کی جانچ کے لیے یکجا منصوبہ

**حیثیت:** تازہ next سے فیز3 منظور، پھر مالک کے **b** سے جزوی return طریقہ B منظور: پہلے560 پھر باقی540، نہ کہ550/550۔ **v0.78.0 Avocet میں فیز1/2/3 کا source موجود؛ Windows build،1300-case run اور PT-20/21/22 عملی تصدیق باقی۔** فیز4 جامع review ابھی نہیں ہوا؛ اگلا next فیز4۔ یہ security certification یا runtime PASS نہیں۔ [فیز3 استعمال/آزمائش/بندش](POS-PHASE3-TEST-URDU.md)؛ [فیز2 نامہ](POS-PHASE2-TEST-URDU.md)۔

## مختصر نقشہ — پہلے یہ پڑھیں

| فیز | کام | تکمیل پر مالک کیا آزمائے گا؟ |
|---|---|---|
| 1 | مستقل سرچ پینل، زیادہ Cart جگہ، keyboard/focus، واضح ہلکے گول بٹن | بغیر بل محفوظ کیے تلاش، منتخب دوا Add، keyboard navigation، لمبا Cart |
| 2 | ہر شخص کی permissions، الگ item editor، profit/total حساب، دوہری رعایت کی روک تھام | مثالوں کے totals، per-user حدیں، انکار کے پیغامات، one-bill override/reset |
| 3 | بل بننے کے بعد الگ Extra Discount، رسید، وصولی، واپسی/رپورٹس | 1120 − 20 = 1100، صفر بقایا، cash drawer، cap، reprint/refund |
| 4 | 15 نکات کے تحت صفحہ وار code/guide review، bugs اور مکمل/باقی رپورٹ | اپ ڈیٹ اردو کتاب، page coverage، مسئلوں کی حیثیت، Windows/physical checklist |

مالک کا **next** فیز1 کی منظوری ہے؛ فیز1 v0.76.0، فیز2 v0.77.0 اور فیز3 کا code v0.78.0 میں کیا گیا۔ اس کے بعد ہر **next** پر اگلا فیز۔ کوئی فیز tests میں رکا ہو تو اسے مکمل کہہ کر آگے نہیں بڑھنا۔ ہر فیز کے متعلقہ tests، guide اور handoff اسی فیز کے ساتھ؛ آخری فیز تک documentation ملتوی نہیں۔

## فیز 1 — POS کی جگہ اور کی بورڈ

### منظور شدہ کام

- B طرز: ایک طرف مستقل مختصر search pane، ساتھ بڑا Cart۔ خالی search پر پوری catalog یا hidden medicine selection نہیں۔
- Counter کے اندر دہرایا POS/title/subtitle اور cashier/admin نام ہٹانا؛ اصل shell navigation/user identity برقرار۔ Refresh دستیاب۔
- Park cart، Save draft، Open drafts، Parked carts، Reprint وغیرہ مناسب padding، نرم گول کنارے، ہلکا highlight؛ بہت بڑے/شوخ نہیں۔
- Counter کھلے اور کامیاب Add ہو تو سرچ فوکس؛ quantity/rate/customer/cash لکھتے وقت زبردستی فوکس نہ چھیننا۔ Scanner کی keyboard-like input کی حقیقت بتانا؛ unrelated inputs سے ہر scan پکڑنے کا جھوٹا وعدہ نہیں۔
- سادہ تیر موجودہ حصے کے اندر؛ Shift+تیر سے حصے بدلنا۔ TextBox میں Left/Right text caret، ComboBox/list/grid میں native selection محفوظ۔ Tab/Shift+Tab متبادل راستہ۔ actual section map اور focus indication guide میں درج۔
- Search textbox Enter صرف exact barcode scan؛ fuzzy name خود Add نہیں۔ Results میں focused selected row Enter یا double-click سے ایک بار Add۔ Cart row Enter/double-click کا item editor فیز 2 میں؛ فیز 1 میں غیر موجود مالی سہولت کا دعویٰ نہیں۔
- F4، guarded Complete sale، owned success modal، independent AutoPrint، fixed footer اور bounded scrolling برقرار۔

### قبولیت/جانچ

Blank/name/typo/exact barcode/unknown scan/no stock refusal، Add کے بعد reset، 5–10 cart lines، short window/DPI، no double-add، rate/cash typing محفوظ۔ اس فیز میں مالی calculation/default نہیں بدلیں گے۔ Windows/visual proof الگ pending اگر یہاں دستیاب نہ ہو۔

## فیز 2 — اجازتیں اور اصل رعایت کا حساب

### صرف مالک کا per-person panel

صرف مالک ہر شخص کی اجازتیں/حدیں بدلے؛ manager یا cashier اپنی/دوسروں کی limits نہیں بدل سکتا۔ ہر شخص کے لیے الگ:

1. دستی قیمت ایڈٹ کی اجازت اور زیادہ سے زیادہ فیصد کمی۔
2. فروخت کی رقم پر رعایت کی اجازت/زیادہ سے زیادہ فیصد۔
3. منافع پر رعایت کی اجازت/زیادہ سے زیادہ فیصد۔
4. ایک بل کے لیے bill basis بدلنے کی اجازت۔
5. فیز 3 میں الگ Extra Discount کی اجازت اور روپے فی بل کی حد۔

نئے اختیارات سب کو خودکار نہ دیں۔ موجودہ owner recovery/manage ability، user migration اور safe initialization code میں inspect کرکے documented کریں؛ کسی پرانے role cap کو نئی per-person اجازت سمجھ کر چپکے سے bypass نہ کریں۔

### ہر item کی چار حالتیں

- **Normal:** bill discount میں شامل۔
- **Manual final rate:** قیمت کم یا زیادہ خود لکھی؛ bill discount سے باہر۔
- **Line discount on profit:** اسی item کے منافع سے فیصد؛ bill discount سے باہر۔
- **Line discount on selling total:** اسی item کی فروخت سے فیصد؛ bill discount سے باہر۔

ایک وقت میں ایک حالت، دوبارہ stacked line/manual discount نہیں۔ Reset to normal سے دوبارہ bill discount میں شامل۔ بعد کی normal D/E lines پر موجودہ bill discount خود لاگو۔ الگ adjustments والی ایک product/unit کی lines غلط merge نہ ہوں۔ item editor میں unit/strength/pack، reference rate، final rate، واضح basis؛ authorized cost/profit visibility کے تحت actual batch cost، gross profit اور denominator کے نام سمیت margin۔

**مثال:** A=100 کو manual95، B=100 کو selling-total20% یعنی80، C=100 normal؛ bill selling-total10% صرف C پر90؛ total265۔ A کو110 کیا تو110 ہی، اس پر bill discount نہیں۔

### bill basis اور safeguards

- ابتدائی default profit، موجودہ shop کے مستقبل کے bills بھی؛ مالک Settings میں profit/total بدل سکتا ہے۔ محفوظ پرانے bills/history unchanged۔
- اجازت یافتہ user صرف موجودہ bill کا basis بدل سکے؛ popup خصوصاً profit→total پر واضح۔ اگلا نیا bill Settings default سے شروع، hardcoded profit نہیں اگر owner نے default total کیا ہو۔
- total bill basis >5% پر ہر save سے پہلے confirmation، typing popup نہیں؛ confirmation per-person cap توڑنے کا اختیار نہیں۔
- دستی کمی reference selling rate کے مقابلے میں cap۔ 1200 پر20%=240، minimum960؛ 980 نہیں۔ cost1000 ہو تو minimum1000۔ تجاوز پر maximum reduction/minimum allowed واضح؛ خاموش clamp نہیں۔
- خرید سے نیچے final sale کسی role حتیٰ کہ owner کو بھی نہیں؛ actual allocated batch cost، no per-sale bypass۔ line/bill discount کے بعد check، پھر فیز3 Extra Discount کے بعد bill-level cost safety بھی۔ ایک profitable line سے دوسری loss-making line کو چھپانا نہیں۔
- permissions/caps/cost صرف UI نہیں، quote/create service پر بھی۔ rounding integer paisa، packs/batches، split payments، loyalty/special-rate interaction، stock/credit locks محفوظ۔
- preview/quote/saved sale/receipt/draft/park/return ایک ہی محفوظ حساب پر۔ data/schema change درکار ہو تو non-destructive migration، defaults اور old-bill compatibility tests۔

### قبولیت/جانچ

Cost200/sale300/10%: profit290، total270؛ mixed normal/manual/line bases، role-independent per-user caps، unauthorized override، limit-bypass attempts، zero/negative profit، pack/fraction/rounding، draft resume اور repeated save۔ test data میں ہی committed sale/return tests۔

## فیز 3 — الگ Extra Discount / Cashier Tolerance

### مالک کی آخری وضاحت — یہی نافذ ہوگی

**بل مکمل بننے کے بعد روپے کی اضافی رعایت۔ کسی product کا rate، quantity، پہلے کی الگ رعایت یا bill-percent دوبارہ نہیں بدلنا۔** یہ item adjustment نہیں اور normal/manual line eligibility سے بند نہیں؛ سب items پہلے adjusted ہوں تب بھی owner کی دی ہوئی Extra Discount اجازت کے اندر چل سکے۔

مثال:

| حساب | روپے |
|---|---:|
| سابق تمام حساب کے بعد بل | 1120 |
| Extra Discount | −20 |
| آخری قابل ادا | 1100 |
| وصولی | 1100 |
| بقایا | 0 |

- الگ field/button سے Extra Discount یا مطلوبہ کم final total داخل کرنا؛ قبل از apply دونوں values واضح۔ صرف کمی، قیمت بڑھانے والی rounding اس درخواست کا حصہ نہیں۔
- مالک ہر شخص کے لیے اجازت اور روپے فی bill cap مقرر کرے؛ 20/30/50 مثالیں ہیں، سب کو automatic50 نہیں۔ بار بار apply سے cap reset یا accumulate bypass نہیں۔
- عام فیصد کی رعایت سے الگ مقرر روپے والی اجازت؛ اس میں line rates/discounts untouched۔ below-cost safeguard اور zero/negative total validation پھر بھی۔
- Receipt/reprint میں اصل calculated total، Extra Discount اور final payable الگ؛ صرف1120 printable payable اور اندر1100 رکھنا نہیں۔ tax نہیں۔
- Discount، cash received، change، due/credit، shift drawer، reports، profit اور audit سب reconcile۔ کم وصولی خود سے Extra Discount نہ بنے؛ معافی نہ ہو تو due۔
- Sale-level original/extra/final values، actor/time محفوظ؛ Park/Draft اور validation میں صحیح بحالی، sale posting transaction میں atomic record۔
- **Product lines rewrite یا Extra Discount کو item price/line discount بنا کر محفوظ نہیں کرنا۔** Returns میں bill-level رعایت کا settlement لازمی دیکھنا تاکہ original paid amount سے زیادہ refund نہ ہو؛ مالک نے مثال کے بعد **B** منتخب کیا: اصل item amount پہلے، پھر final bill کی باقی قابلِ واپسی حد؛560 پھر540، دونوں اکٹھے1100۔ cap آخری درج return line سے کم، وجہ واضح؛ أصل فروخت کی لائنیں نہ بدلیں۔

### قبولیت/جانچ

Allowed1120→1100، denied user، cap exceeded، repeated apply/edit/reset، below-cost refusal، full/partial return consistency، receipt/reprint، partial cash/credit، shift totals اور app reload۔ محفوظ test data، حقیقی shop میں fake bill نہیں۔

## فیز 4 — 15 نکاتی صفحہ وار جائزہ اور مکمل کتاب

ہر موجودہ navigation page/settings کا inventory، متعلقہ View/ViewModel/service/data/permission path کا review، guide lesson سے mapping۔ review evidence/status لکھنا؛ صرف فہرست ہونا PASS نہیں۔ اتنا بڑا review ایک turn میں verify نہ ہوسکے تو اسی فیز کی حقیقی باقی checklist بتانا، final کا جھوٹا دعویٰ نہیں۔

1. **رقم اور rounding:** paisa، packs، quantity، preview/save/receipt، split/cash/change، invalid input۔
2. **رعایت کی ترتیب:** line vs bill exclusivity، extra separate، loyalty/special rate، caps، no hidden double discount۔
3. **Actual cost:** batches/expiry/units، تاریخی لاگت، zero/negative margin، consistent allocation۔
4. **Bills/returns:** محفوظ snapshots، partial/full refund، paid-vs-refunded limits، history unchanged۔
5. **Stock/atomicity:** تمام roles پر sufficient stock، expired/repeated product، transaction rollback، concurrent sale۔
6. **Double execution:** double-click/Enter/F4 repeat، modal/print failure سے duplicate sale/stock/payment نہیں۔
7. **Permissions:** owner-only admin، UI+service enforcement، cost/profit secrecy، direct-call bypass۔
8. **Record access:** customer/user/partner IDs سے unauthorized record/report/attachment، موجودہ cloud/shop boundaries؛ hypothetical multi-tenancy کو ثابت شدہ feature نہ سمجھنا۔
9. **Login/secrets:** PIN/password/reset/session، logs/exports، owner recovery، lockdown escape record۔
10. **Inputs/files:** SQL parameters، imports، photos/paths، zip traversal، malformed files، receipt/HTML escaping۔
11. **Backup/cloud:** isolation جہاں موجود، credentials، sync/export، restore failure/rollback؛ دوسرا program folder الگ database کی ضمانت نہیں۔
12. **Other finance:** customer/supplier credit، mixed supplier payment، expenses، loyalty، reports reconciliation؛ stock-tag label-only۔
13. **Draft/Park:** rates/bases/adjustments restore، stale permissions/stock revalidation، no hidden selected medicine۔
14. **UI/keyboard:** small screen/DPI، scanner focus، accessible controls، fixed checkout، optional panels اور section shortcuts۔
15. **Obsolete/pending/guide:** duplicate feature evidence، whole-feature removal صرف مالک کی batch approval، tests/static/physical statuses الگ۔

واضح محدود bug ملے تو درست کرکے کیا/کیوں/test بتانا؛ policy decision یا بڑی feature removal پر پہلے وضاحت پھر MCQ۔ security/financial correctness کی مطلق ضمانت یا ہر code path run ہونے کا دعویٰ نہیں۔

## گائیڈ، screenshots اور test/rollback

- پڑھنے/print کے لیے: `docs/USER-GUIDE-URDU.html`، offline browser۔
- قابل ترمیم source: `docs/USER-GUIDE-URDU.md`۔
- یہ مکمل منصوبہ: `docs/POS-NEXT-REVIEW-PLAN-URDU.md`۔
- کتاب میں پہلے مختصر option summary، پھر الگ نمبر وار detailed lessons، button کے اصل نام، مثال، expected result، error/واپسی کے قدم۔ ہر page/settings کو source سے ملانا۔
- اصل Windows screenshots نہ ہوں تو fake screenshot نہیں؛ owner images ملیں تو متعلقہ جگہ، ورنہ ترتیب وار واضح متن/tables۔
- verified backup اور واقعی الگ test data/environment کی تصدیق؛ پہلے unposted cart سے preview، save/return tests test data پر۔
- Settings سے bill basis بدل کر future bills کا سابق حساب منتخب کیا جاسکے؛ line reset/Extra Discount remove الگ actions، اجازتوں کے تحت۔ یہ نئی schema والی app سے پرانی binary چلانے یا پرانا backup overwrite کرنے کے برابر نہیں؛ ایسا rollback data loss دے سکتا ہے۔
- ہر فیز کی completion میں: version/files، changes، fixed bugs، tests واقعی executed یا صرف written، static results، Windows/physical pending، owner test steps اور باقی کام۔ installer صرف الگ واضح اجازت پر؛ اس منصوبے سے installer authorized نہیں۔

## ابھی کی آخری حیثیت

بنیادی UI/discount/permission/Extra Discount فیصلے واضح؛ مزید وہی سوال دوبارہ درکار نہیں۔ جزوی return کا سوال پوچھا اور B جواب ملا؛ یہی نافذ ہے، دوبارہ یہی سوال نہیں۔ نئی حقیقی ambiguity پر مثال کے ساتھ پوچھیں۔ v0.78.0 میں فیز1/2/3 source موجود؛ Windows/runtime proof باقی۔ اگلے next پر فیز4 review؛ اسے پہلے سے مکمل نہ سمجھیں۔

### فیز4 کی تازہ پیش رفت — v0.79.0، حصہ1

فیز4 شروع ہے، مکمل نہیں۔ 26 routes، Settings کے8 contents اور36 lessons کا evidence register [یہاں](POS-PHASE4-EVIDENCE.md)؛ مالک کی موجودہ شناخت، راز reset، recovery atomicity اور تصاویر کے محفوظ filenames کے محدود fixes شامل۔ 1325 C# cases صرف لکھے گئے، Windows پر چلنے باقی۔ اگلا حصہ اسی فیز کا direct-service/finance/files/cloud/UI اور مکمل کتاب reconciliation ہے؛ Settings index0 کی shell reachability بھی باقی۔ [اس حصے کی محفوظ آزمائش](POS-PHASE4-TEST-URDU.md)۔
