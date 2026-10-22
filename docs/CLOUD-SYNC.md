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
4. Area11 میں **SYSTEM → Settings → Cloud sync** کھولیں اور یہ بھریں:

| خانہ | کیا لکھیں |
|---|---|
| Address (URL) | `https://abcdxyz.supabase.co` — آخر میں `/` نہ لگائیں |
| Key | anon key (یہ پاس ورڈ کی طرح ہے — کسی کو نہ دیں) |
| Table | `area11_sync` |
| Shop id | `shop-1` (ایک ہی دکان کے دونوں کاؤنٹر پر **ایک ہی**؛ دوسری برانچ ہو تو `shop-2`) |

5. **SYSTEM → Cloud sync** کھولیں اور **Push the shop's data to the cloud** دبائیں۔

---

## 2. صفحے کے تین بٹن

| بٹن | کیا کرتا ہے | کیا لکھتا ہے |
|---|---|---|
| **Push the shop's data to the cloud** | دکان کا ڈیٹا ایک دستاویز میں بنا کر بھیجتا ہے | صرف کلاؤڈ، اِس کمپیوٹر پر کچھ نہیں |
| **See what the cloud has (writes nothing)** | کلاؤڈ کی کاپی پڑھ کر سامنے رکھتا ہے | **کچھ بھی نہیں** — نہ یہاں، نہ وہاں |
| **Bring the cloud data here** | ٹک لگانے کے بعد کلاؤڈ کی قطاریں یہاں لکھتا ہے | پہلے **بیک اپ**، پھر ایک ٹرانزیکشن میں |

تیسرا بٹن اُس وقت تک **بند** رہتا ہے جب تک آپ یہ ٹک نہ لگائیں:
*"I understand: the cloud rows replace the rows on this computer."*

---

## 3. کیا جاتا ہے، کیا نہیں

| جاتا ہے | نہیں جاتا |
|---|---|
| دوائیں، بیچ، گاہک، سپلائر، کیٹگری، کمپنی — **پوری فہرست** | تصاویر (وہ زپ بیک اپ میں جاتی ہیں) |
| بل، خریداری، ادائیگی، خرچ — **آخری 90 دن** | پرانا ڈیٹا (90 دن سے پرانا) |
| بلوں اور خریداری کی لائنیں (اُنہی 90 دن کی) | صارفین کے پاس ورڈ/PIN، سیٹنگز، آڈٹ لاگ |

---

## 4. خطرہ — یہ بات سمجھ لینا ضروری ہے

> **اس فیچر میں یہ نقصان ہے:** "Bring the cloud data here" کلاؤڈ کی **پرانی** قطاریں اِس کمپیوٹر کی قطاروں پر
> لکھ دیتا ہے۔ آج کی بلی ہمیشہ کلاؤڈ سے نئی ہوتی ہے۔ پروگرام **کوئی قطار مٹاتا نہیں** اور لکھنے سے پہلے
> خودکار بیک اپ لیتا ہے، پھر بھی یہ "اپ ڈیٹ" نہیں ہے — یہ واپسی (restore) کی طرح ہے۔
>
> **اصول:** روزمرہ کام میں صرف **Push**۔ "Bring" صرف اُس دن جب نئی مشین پر پرانا ڈیٹا لانا ہو۔

---

## 5. اگر ایرر آئے

| پیغام | مطلب | حل |
|---|---|---|
| "Fill the cloud address, the key and the shop id…" | سیٹنگز خالی ہیں | Settings → Cloud sync بھریں |
| "The key was refused by Supabase (401)" | key غلط یا بدل دی گئی | Project Settings سے نئی anon key کاپی کریں |
| "The address or the table was not found (404)" | URL یا ٹیبل کا نام غلط | URL میں `/` نہ ہو؛ ٹیبل `area11_sync` |
| "No data for 'shop-1' in the cloud yet" | کبھی push نہیں ہوا | پہلے Push دبائیں |
| "The cloud could not be reached…" | انٹرنیٹ نہیں | انٹرنیٹ چیک کریں؛ دکان کا کام جاری رہے گا |
| "That copy belongs to 'shop-2'…" | غلط دکان کی کاپی | Shop id ٹھیک کریں؛ کچھ نہیں لکھا گیا |

---

## 6. بلیک باکس میں کیا لکھا جاتا ہے

| عمل | لائن |
|---|---|
| push | `sync_push` — کتنی قطاریں، کتنی ٹیبل، کتنی کاپی |
| bring | `sync_apply` — کتنی قطاریں، بیک اپ کا نام |

Key کبھی آڈٹ میں نہیں لکھی جاتی؛ صفحہ بھی اُس کا صرف پہلا/آخری چار حرف دکھاتا ہے (`abcd****wxyz`)۔
