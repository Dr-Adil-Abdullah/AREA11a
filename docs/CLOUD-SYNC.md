# کلاؤڈ سنک — Supabase (F-48)

> یہ صفحہ **اختیاری** ہے۔ Area11 بغیر انٹرنیٹ بھی پورا چلتا ہے؛ کلاؤڈ صرف ایک **نقل** ہے تاکہ آپ گھر بیٹھے
> دکان کے نمبر دیکھ سکیں، اور دکان کے باہر بھی ایک کاپی موجود رہے۔ **اصل ڈیٹا ہمیشہ اِس کمپیوٹر کا ہے۔**

---

## 1. ایک بار کا سیٹ اپ (تقریباً 10 منٹ)

1. [supabase.com](https://supabase.com) پر مفت اکاؤنٹ بنا کر **New project** دبائیں (خطہ: Singapore یا Mumbai)۔
2. پروجیکٹ کھلنے پر **SQL Editor** میں جائیں اور یہ پورا کوڈ چلا دیں:

```sql
create table if not exists public.area11_sync (
  shop_id    text primary key,
  shop_name  text,
  sent_at    timestamptz,
  version    int,
  payload    jsonb,
  updated_at timestamptz default now()
);
```

3. **Project Settings → API** سے دو چیزیں کاپی کریں:
   - **Project URL** (مثلاً `https://abcdxyz.supabase.co`)
   - **anon public** key (لمبا کوڈ)
4. Area11 میں **SYSTEM → Settings → Cloud sync** کھولیں، **ٹک لگائیں** اور یہ بھریں:

| خانہ | کیا لکھیں |
|---|---|
| Cloud sync is switched on | **ٹک لگائیں** — بغیر ٹک کے پروگرام کلاؤڈ سے بات ہی نہیں کرتا (خاموش، محفوظ) |
| Address (URL) | `https://abcdxyz.supabase.co` — آخر میں `/` نہ لگائیں |
| Key | anon key (یہ پاس ورڈ کی طرح ہے — کسی کو نہ دیں؛ محفوظ ہونے کے بعد دوبارہ سکرین پر نہیں آتا) |
| Table | `area11_sync` |
| Shop id | `shop-1` (ایک ہی دکان کے دونوں کاؤنٹر پر **ایک ہی**؛ دوسری برانچ ہو تو `shop-2`) |
| خودکار بھیج (automatic) | ٹک لگائیں تو sign-in کے دوران دکان خود ہر چند منٹ بعد بھیجتی ہے (v0.56.0) |
| ہر (منٹ) | 5 سے 1440 تک پورا عدد، مثلاً `15` |

پھر **Save settings** دبائیں۔ اگر ٹک لگی ہو اور کوئی خانہ خالی ہو تو صفحہ محفوظ نہیں کرتا اور صاف کہتا ہے کہ کون سا خانہ
باقی ہے۔ غلط خانہ بھی وہیں رد ہو جاتا ہے: ایڈریس `https://` سے شروع ہونا چاہیے، اور **shop id میں خالی جگہ نہیں** ہو سکتی۔

5. **SYSTEM → Cloud sync** کھولیں اور **Push the shop's data to the cloud** دبائیں۔

---

## 2. صفحے کے تین بٹن

| بٹن | کیا کرتا ہے | کیا لکھتا ہے |
|---|---|---|
| **Push the shop's data to the cloud** | دکان کا ڈیٹا ایک دستاویز میں بنا کر بھیجتا ہے | دکان کے records نہیں بدلتے؛ مقامی sync status اور ایک black-box سطر لکھی جاتی ہے |
| **See what the cloud has (writes nothing)** | کلاؤڈ کی کاپی پڑھ کر سامنے رکھتا ہے | **کچھ بھی نہیں** — نہ یہاں، نہ وہاں |
| **Bring the cloud data here** | ٹک لگانے کے بعد کلاؤڈ کی قطاریں یہاں لکھتا ہے | پہلے **بیک اپ**، پھر ایک ٹرانزیکشن میں |

تیسرا بٹن اُس وقت تک **بند** رہتا ہے جب تک آپ یہ ٹک نہ لگائیں:
*"I understand: the cloud rows replace the rows on this computer."*

## 2a. خودکار بھیج (automatic push، v0.56.0)

Settings میں **«Push automatically every few minutes»** پر ٹک اور منٹ (5 سے 1440) لکھیں تو sign-in کے دوران
دکان خود بھیجتی رہتی ہے۔ اصول وہی ہیں جو ہاتھ والے بٹن کے ہیں:

- ایک وقت میں **ایک ہی push**: ہاتھ والا بٹن چل رہا ہو تو خودکار باری خاموشی سے اگلی باری کا انتظار کرتی ہے،
  اور خودکار بھیج چل رہا ہو تو بٹن صاف کہتا ہے کہ پہلے وہ مکمل ہونے دیں۔
- ناکام باری **پورا وقفہ** انتظار کرتی ہے — انٹرنیٹ بند ہو تو ہر آدھے منٹ شور نہیں، اگلی باری پر خود کوشش ہوتی ہے۔
  آخری غلطی کا جملہ اسی صفحے کی «Automatic» والی سطر میں نظر آتا ہے؛ کامیاب بھیج (ہاتھ والا یا خودکار) اسے مٹا دیتا ہے۔
- خودکار بھیج **صرف بھیجتا** ہے — «Bring» ہمیشہ آپ کے اپنے دو کلک (دیکھیں، پھر ٹک) سے ہوتا ہے۔
- دو کاؤنٹر ایک ہی shop id پر بھیجیں تو **آخری بھیج جیتتا** ہے (نیا پرانے کی جگہ لکھا جاتا ہے)؛ کوئی جوڑ (merge) نہیں ہوتا۔
- خالی دکان («بھیجنے کو کچھ نہیں») بھی پورا وقفہ انتظار کرتی ہے، ہر ٹک پر دوبارہ تیاری نہیں ہوتی۔

Preview کے بعد **Refresh** یا کامیاب **Push** پرانی preview کو apply سے ہٹا دیتا ہے؛ نیا موازنہ پڑھنا ہوگا۔
Preview کے بعد Settings میں cloud بند کر دیں تو Core بھی apply رد کرتا ہے، بیک اپ سے پہلے۔ مستقبل کے
نامعلوم format اور `id` کے بغیر/NULL والی قطار کو محفوظ طریقے سے روکا جاتا ہے؛ NULL id سے نیا مقامی record نہیں بنتا۔

---

## 3. کیا جاتا ہے، کیا نہیں

| جاتا ہے | نہیں جاتا |
|---|---|
| دوائیں، بیچ، گاہک، سپلائر، کیٹگری، کمپنی — **پوری فہرست** | تصاویر (وہ زپ بیک اپ میں جاتی ہیں) |
| managed units، native field definitions، generic formulas اور brands — **پوری فہرست** | local-only document drafts؛ وہ cloud میں نہیں جاتے |
| بل، خریداری، no-bill return، ادائیگی، خرچ — **آخری 90 دن** | 90 دن سے پرانے غیر منسلک documents |
| منتخب بل، خریداری اور no-bill return کی **تمام لائنیں** | صارفین کے پاس ورڈ/PIN، سیٹنگز، آڈٹ لاگ |
| حالیہ payment یا reconciliation کا پرانا parent اور اُس کی لائنیں — صرف ربط مکمل رکھنے کے لیے | تصاویر، shifts اور مکمل مشین کا backup |

**90 دن کا اصول ربط توڑنے کا جواز نہیں:** اگر آج کی ادائیگی پرانے بل یا خریداری سے ہے، یا no-bill return پرانے
بل کی لائن سے جُڑتی ہے، تو وہ parent اور اُس کے document کی تمام لائنیں بھی دستاویز میں جائیں گی۔ کسی غیر متعلقہ
پرانے بل کو صرف اس لیے نہیں بھیجا جاتا کہ وہ پرانا ہے۔ Payment کا `sale_id`، `purchase_id` یا
`provisional_return_id` خالی کر کے یا financial row چھپا کر خرابی نہیں چھپائی جاتی۔

Format 3 میں companies، generic formulas، brands اور suppliers products سے پہلے لکھے جاتے ہیں تاکہ
managed links کا parent موجود ہو۔ v0.56.0 سے **automatic push** بھی ہے (اوپر 2a): ہاتھ والا Push / Preview /
Bring ویسے ہی ہیں، اور خودکار باری انہی اصولوں پر چلتی ہے — صرف بھیجنا، ایک وقت میں ایک، ناکامی پر اگلا وقفہ۔

---

## 3a. کلاؤڈ میں کیا لکھا جاتا ہے (ایک قطار)

Supabase کا ٹیبل `area11_sync` ایک ہی قطار رکھتا ہے، اور Area11 اُسے **ٹیبل کے اپنے کالم** سے بھرتا ہے
(یہی چیز v0.26.0 میں درست ہوئی — پہلے پورا دستاویز ہی بھیج دیا جاتا تھا اور Supabase اُسے `format` کالم
سمجھ کر انکار کر دیتا تھا):

| کالم | کیا جاتا ہے |
|---|---|
| `shop_id` | آپ کا shop id (`shop-1`) — اسی پر پرانی قطار بدلی جاتی ہے |
| `shop_name` | دکان کا نام (وہی جو رسید پر چھپتا ہے) |
| `sent_at` | بھیجنے کا وقت، دکان کی گھڑی اور اُس کا آفسیٹ (`+05:00`) |
| `version` | **دستاویز کی شکل** کا نمبر (آج `3`) — پروگرام کا ورژن نہیں |
| `payload` | پورا Area11 دستاویز، جس میں 19 ٹیبل اور `version` میں پروگرام کا ورژن (`0.39.0`) |

`payload` کے اندر جو `version` ہے وہ **پروگرام** کا ہے؛ باہر والا `version` صرف دستاویز کی شکل کا نمبر ہے۔
یہ فرق جان بوجھ کر ہے: `version` کالم `int` ہے، اور "0.39.0" جیسا متن اُس میں نہیں بیٹھ سکتا۔
نیا Area11 پرانا **format 1 اور format 2** پڑھ سکتا ہے۔ پرانا Area11 نیا **format 3** لکھنے کے بجائے اپڈیٹ
مانگے گا، تاکہ managed catalog/native-field parents کو نظر انداز کر کے ادھورا apply نہ کرے۔ اپڈیٹ کے بعد
**Bring سے پہلے نیا Push** کریں: پرانی copy میں ضروری parent پہلے سے missing ہو تو نیا reader اُسے ظاہر
کر سکتا ہے مگر missing row ایجاد نہیں کر سکتا؛ format 1/2 پڑھنا managed links خود بنانا نہیں ہے۔

---

## 4. خطرہ — یہ بات سمجھ لینا ضروری ہے

> **اس فیچر میں یہ نقصان ہے:** "Bring the cloud data here" کلاؤڈ کی **پرانی** قطاریں اِس کمپیوٹر کی قطاروں پر
> لکھ دیتا ہے۔ آج کی بلی ہمیشہ کلاؤڈ سے نئی ہوتی ہے۔ پروگرام **کوئی قطار مٹاتا نہیں** اور لکھنے سے پہلے
> خودکار بیک اپ لیتا ہے، پھر بھی یہ "اپ ڈیٹ" نہیں ہے — یہ واپسی (restore) کی طرح ہے۔
>
> **اصول:** روزمرہ کام میں صرف **Push**۔ "Bring" صرف جب آپ جان بوجھ کر یہی partial rows لانا چاہیں۔
> مکمل نئی مشین بحال کرنے کے لیے cloud نہیں، Area11 کا مکمل zip backup استعمال کریں۔

**یہ مکمل backup نہیں ہے۔** Cloud میں صرف اوپر بتائی گئی 19 tables اور 90 دن + ضروری linked parents ہیں۔
پوری مشین، تصاویر، settings، audit، users، shifts اور باقی history بحال کرنے کے لیے Area11 کا معمول کا
مکمل zip backup استعمال کریں۔ Cloud کی نقل کو مکمل restore سمجھ کر اصل دکان پر apply نہ کریں۔

---

## 5. اگر ایرر آئے

| پیغام | مطلب | حل |
|---|---|---|
| "Cloud sync is switched off…" | Settings میں ٹک نہیں لگا | Settings → Cloud sync میں ٹک لگا کر Save دبائیں |
| "Fill the cloud address, the key and the shop id…" | سیٹنگز خالی ہیں | Settings → Cloud sync بھریں |
| "The key was refused by Supabase (401)" | key غلط یا بدل دی گئی | Project Settings سے نئی anon key کاپی کریں |
| "The address or the table was not found (404)" | URL یا ٹیبل کا نام غلط | URL میں `/` نہ ہو؛ ٹیبل `area11_sync` |
| "No data for 'shop-1' in the cloud yet" | کبھی push نہیں ہوا | پہلے Push دبائیں |
| "The cloud could not be reached…" | انٹرنیٹ نہیں | انٹرنیٹ چیک کریں؛ دکان کا کام جاری رہے گا |
| "That copy belongs to 'shop-2'…" | غلط دکان کی کاپی | Shop id ٹھیک کریں؛ کچھ نہیں لکھا گیا |
| "different Area11 (format …)" | دستاویز نئے/نامعلوم قواعد کا ہے | Area11 اپڈیٹ کریں؛ بیک اپ یا apply شروع نہیں ہوتا |
| "references missing … Repair the local database" | مقامی record کا parent موجود نہیں | Push نہ دہرائیں؛ مقامی books/database کی مرمت اور مکمل log درکار ہے |
| "A push is already running…" | خودکار بھیج چل رہا ہے | چند سیکنڈ ٹھہریں اور Push دوبارہ دبائیں؛ دوہرا بھیج نہیں ہوتا |

---

## 6. بلیک باکس میں کیا لکھا جاتا ہے

| عمل | لائن |
|---|---|
| push | `sync_push` — کتنی قطاریں، کتنی ٹیبل، کتنی کاپی |
| bring | `sync_apply` — کتنی قطاریں، بیک اپ کا نام |

Key کبھی آڈٹ میں نہیں لکھی جاتی؛ صفحہ بھی اُس کا صرف پہلا/آخری چار حرف دکھاتا ہے (`abcd****wxyz`)۔

---

## 7. اصل Supabase پر محتاط جانچ (v0.39.0)

پہلے Windows پر `tools\RUN-TESTS.cmd` چلائیں: مطلوبہ نتیجہ **`ALL GREEN - 1144 of 1144`** ہے۔
پھر اصل cloud جانچ کے لیے الگ **test Shop id / table** اور آزمائشی database نقل استعمال کریں۔ اصل دکان کا
ڈیٹا یا key چیٹ میں نہ بھیجیں؛ key صرف Settings کے خانے میں لکھیں۔

1. 90 دن سے پرانا credit بل بنائیں اور آج اُس کی payment کریں؛ ایسی ہی پرانی خریداری پر آج supplier payment کریں۔
2. پرانے بل سے منسلک no-bill return reconciliation رکھیں جس میں PR payment، PR line اور sale-line allocation موجود ہو۔
3. Test copy میں company + generic formula + brand + supplier بنائیں اور product کے managed/preferred links دیں۔
   ایک local draft بھی رکھیں؛ وہ cloud document میں شامل نہیں ہونا چاہیے۔
4. **Push** کے جواب میں format-3 کے منتخب rows اور کامیابی دیکھیں (خالی table شامل نہیں ہوتی)؛ **Preview** میں 19-table موازنہ دیکھیں۔
5. صرف آزمائشی نقل پر ٹک اور **Bring** کریں۔ پہلے backup بننا چاہیے؛ managed parents product سے پہلے apply ہوں،
   پرانے financial parents/lines، PR allocations اور payments موجود ہوں، اور unrelated پرانا بل شامل نہ ہو۔
6. Preview کے بعد Refresh/Push پر پرانی copy apply نہ ہو؛ Settings میں switch بند کر کے بھی apply رد ہو۔
7. خودکار جانچ: test copy میں automatic پر ٹک اور 5 منٹ لکھیں؛ وقفے پر Push خود ہو اور «last sync» بدلے۔ پھر انٹرنیٹ
   بند کر کے وقفہ گزاریں: صفحے کی «Automatic» سطر میں ناکامی کا جملہ آئے اور دکان کا کام رُکے نہیں؛ انٹرنیٹ واپس آئے
   تو اگلی باری خود کامیاب ہو اور جملہ مٹ جائے۔
8. ترتیب: خودکار بھیج کے دوران Push دبائیں تو «already running» والا جواب آئے، دوہرا بھیج نہ ہو۔
9. علیحدگی: آزمائش ہمیشہ **test copy + test shop id + الگ table** پر کریں؛ بھیجنے سے پہلے صفحے پر SHOP ID اور TABLE
   خود پڑھیں۔ اصل shop id کے نیچے test copy کبھی نہ بھیجیں — وہ اصل cloud نقل پر لکھ دے گا۔
10. اصل نتیجہ، HTTP status اور پورا error متن محفوظ کریں۔ صرف fake-wire tests کو اصل Supabase کامیابی نہ لکھیں۔
