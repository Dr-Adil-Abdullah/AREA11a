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

Preview کے بعد **Refresh** یا کامیاب **Push** پرانی preview کو apply سے ہٹا دیتا ہے؛ نیا موازنہ پڑھنا ہوگا۔
Preview کے بعد Settings میں cloud بند کر دیں تو Core بھی apply رد کرتا ہے، بیک اپ سے پہلے۔ مستقبل کے
نامعلوم format اور `id` کے بغیر/NULL والی قطار کو محفوظ طریقے سے روکا جاتا ہے؛ NULL id سے نیا مقامی record نہیں بنتا۔

---

## 3. کیا جاتا ہے، کیا نہیں

| جاتا ہے | نہیں جاتا |
|---|---|
| دوائیں، بیچ، گاہک، سپلائر، کیٹگری، کمپنی — **پوری فہرست** | تصاویر (وہ زپ بیک اپ میں جاتی ہیں) |
| بل، خریداری، no-bill return، ادائیگی، خرچ — **آخری 90 دن** | 90 دن سے پرانے غیر منسلک documents |
| منتخب بل، خریداری اور no-bill return کی **تمام لائنیں** | صارفین کے پاس ورڈ/PIN، سیٹنگز، آڈٹ لاگ |
| حالیہ payment یا reconciliation کا پرانا parent اور اُس کی لائنیں — صرف ربط مکمل رکھنے کے لیے | تصاویر، shifts اور مکمل مشین کا backup |

**90 دن کا اصول ربط توڑنے کا جواز نہیں:** اگر آج کی ادائیگی پرانے بل یا خریداری سے ہے، یا no-bill return پرانے
بل کی لائن سے جُڑتی ہے، تو وہ parent اور اُس کے document کی تمام لائنیں بھی دستاویز میں جائیں گی۔ کسی غیر متعلقہ
پرانے بل کو صرف اس لیے نہیں بھیجا جاتا کہ وہ پرانا ہے۔ Payment کا `sale_id`، `purchase_id` یا
`provisional_return_id` خالی کر کے یا financial row چھپا کر خرابی نہیں چھپائی جاتی۔

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
| `version` | **دستاویز کی شکل** کا نمبر (آج `2`) — پروگرام کا ورژن نہیں |
| `payload` | پورا Area11 دستاویز، جس میں 15 ٹیبل اور `version` میں پروگرام کا ورژن (`0.35.16`) |

`payload` کے اندر جو `version` ہے وہ **پروگرام** کا ہے؛ باہر والا `version` صرف دستاویز کی شکل کا نمبر ہے۔
یہ فرق جان بوجھ کر ہے: `version` کالم `int` ہے، اور "0.35.16" جیسا متن اُس میں نہیں بیٹھ سکتا۔
نیا Area11 پرانا **format 1** پڑھ سکتا ہے۔ پرانا Area11 نیا **format 2** لکھنے کے بجائے اپڈیٹ مانگے گا، تاکہ
no-bill return اور financial links کے نئے قواعد کو نظر انداز کر کے ادھورا apply نہ کرے۔ اپڈیٹ کے بعد **Bring سے
پہلے نیا Push** کریں: پرانی format-1 copy میں ضروری parent پہلے سے missing ہو تو نیا reader اُسے ظاہر کر سکتا
ہے مگر missing row ایجاد نہیں کر سکتا؛ ایسی ادھوری copy apply نہیں ہوگی۔

---

## 4. خطرہ — یہ بات سمجھ لینا ضروری ہے

> **اس فیچر میں یہ نقصان ہے:** "Bring the cloud data here" کلاؤڈ کی **پرانی** قطاریں اِس کمپیوٹر کی قطاروں پر
> لکھ دیتا ہے۔ آج کی بلی ہمیشہ کلاؤڈ سے نئی ہوتی ہے۔ پروگرام **کوئی قطار مٹاتا نہیں** اور لکھنے سے پہلے
> خودکار بیک اپ لیتا ہے، پھر بھی یہ "اپ ڈیٹ" نہیں ہے — یہ واپسی (restore) کی طرح ہے۔
>
> **اصول:** روزمرہ کام میں صرف **Push**۔ "Bring" صرف جب آپ جان بوجھ کر یہی partial rows لانا چاہیں۔
> مکمل نئی مشین بحال کرنے کے لیے cloud نہیں، Area11 کا مکمل zip backup استعمال کریں۔

**یہ مکمل backup نہیں ہے۔** Cloud میں صرف اوپر بتائی گئی 15 tables اور 90 دن + ضروری linked parents ہیں۔
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

---

## 6. بلیک باکس میں کیا لکھا جاتا ہے

| عمل | لائن |
|---|---|
| push | `sync_push` — کتنی قطاریں، کتنی ٹیبل، کتنی کاپی |
| bring | `sync_apply` — کتنی قطاریں، بیک اپ کا نام |

Key کبھی آڈٹ میں نہیں لکھی جاتی؛ صفحہ بھی اُس کا صرف پہلا/آخری چار حرف دکھاتا ہے (`abcd****wxyz`)۔

---

## 7. اصل Supabase پر محتاط جانچ (v0.35.16)

پہلے Windows پر `tools\RUN-TESTS.cmd` چلائیں: مطلوبہ نتیجہ **`ALL GREEN - 860 of 860`** ہے۔
پھر اصل cloud جانچ کے لیے الگ **test Shop id / table** اور آزمائشی database نقل استعمال کریں۔ اصل دکان کا
ڈیٹا یا key چیٹ میں نہ بھیجیں؛ key صرف Settings کے خانے میں لکھیں۔

1. 90 دن سے پرانا credit بل بنائیں اور آج اُس کی payment کریں؛ ایسی ہی پرانی خریداری پر آج supplier payment کریں۔
2. پرانے بل سے منسلک no-bill return reconciliation رکھیں جس میں PR payment، PR line اور sale-line allocation موجود ہو۔
3. **Push** کے جواب میں format-2 کے منتخب rows اور کامیابی دیکھیں (خالی table شامل نہیں ہوتی)؛ **Preview** میں 15-table موازنہ دیکھیں۔
4. صرف آزمائشی نقل پر ٹک اور **Bring** کریں۔ پہلے backup بننا چاہیے؛ پرانے parents، ان کی تمام lines، PR allocations
   اور payments موجود ہوں، اور کوئی unrelated پرانا بل صرف dependency کے نام پر شامل نہ ہوا ہو۔
5. Preview کے بعد Refresh/Push پر پرانی copy apply نہ ہو؛ Settings میں switch بند کر کے بھی apply رد ہو۔
6. اصل نتیجہ، HTTP status اور پورا error متن محفوظ کریں۔ صرف fake-wire tests کو اصل Supabase کامیابی نہ لکھیں۔
