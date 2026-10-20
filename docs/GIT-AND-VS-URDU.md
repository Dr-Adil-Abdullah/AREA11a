# GitHub سے کوڈ لانا اور Visual Studio / VS Code میں چلانا

> آپ کی پہلی کامیاب بلڈ ہو گئی (Build succeeded)۔ یہ صفحہ بتاتا ہے کہ آگے کام کیسے کریں:
> کوڈ GitHub سے کیسے لائیں، Visual Studio یا VS Code میں کیسے کھولیں، اور تبدیلی کیسے بھیجیں۔

---

## 0. پہلے ایک وضاحت — فولڈر دوہرا ہے

آپ کی سکرین پر راستہ یہ تھا:

```
D:\AREA11a-arena-a8a3b2e0-area11a\AREA11a-arena-a8a3b2e0-area11a\dotnet\...
```

یعنی ZIP ایک ایسے فولڈر میں کھلا جس میں پہلے سے وہی نام کا فولڈر موجود تھا۔

**کوئی نقصان نہیں** — بس کام کرتے وقت **اندر والا** فولڈر کھولیں (جس میں `Area11.sln` ہے)۔
آگے سے صاف رکھنے کے لیے اندر والے فولڈر کا نام بدل کر `D:\Area11` رکھ لیں۔

---

## 1. راستہ A — ZIP (آسان، Git کی ضرورت نہیں)

یہ وہی طریقہ ہے جو آپ نے استعمال کیا۔ جب نیا کوڈ آئے تو:

1. نیا ZIP ڈاؤن لوڈ کریں:
   `https://github.com/Dr-Adil-Abdullah/AREA11a/archive/refs/heads/arena/a8a3b2e0-area11a.zip`
2. **نئے** فولڈر میں ایکسٹریکٹ کریں (پرانے پر نہ کھولیں)
3. `START-AREA11.bat` پر ڈبل کلک

> نقصان: ہر بار پورا ZIP آتا ہے، اور آپ کی تبدیلیاں واپس GitHub پر نہیں جاتیں۔

---

## 2. راستہ B — Git سے (سفارش)

ایک بار سیٹ کر لیں، پھر اپ ڈیٹ صرف چند سیکنڈ کا کام ہے۔

### قدم 1 — Git انسٹال کریں (ایک بار)

`https://git-scm.com/download/win` → ڈاؤن لوڈ → Next, Next, Install

تصدیق: نیا ٹرمینل کھول کر `git --version` لکھیں، ورژن دکھے تو ہو گیا۔

### قدم 2 — کوڈ لائیں (ایک بار)

ٹرمینل کھولیں (یا `D:\` میں رائٹ کلک → *Open in Terminal*) اور یہ ایک لائن:

```
git clone -b arena/a8a3b2e0-area11a https://github.com/Dr-Adil-Abdullah/AREA11a.git Area11
```

اب `D:\Area11` میں مکمل کوڈ، `.git` کے ساتھ (یعنی اپ ڈیٹ بھی خود ہو سکے گا)۔

### قدم 3 — اپ ڈیٹ (جب بھی کہا جائے)

`D:\Area11` میں `START-AREA11.bat` ڈبل کلک — وہ خود `git pull` کر کے بلڈ کرتا ہے۔
یا ٹرمینل میں:

```
cd D:\Area11
git pull
```

### قدم 4 — اپنی تبدیلی واپس بھیجیں (اختیاری)

اگر آپ خود کوڈ بدلیں اور GitHub پر محفوظ کرنا چاہیں:

```
cd D:\Area11
git add -A
git commit -m "my change"
git push
```

---

## 3. Visual Studio میں کھولنا

1. Visual Studio کھولیں
2. **File → Open → Project/Solution…**
3. جائیں: `D:\Area11` (یا جہاں آپ نے رکھا ہے) → **`Area11.sln`** منتخب کریں → Open
4. اوپر ٹول بار میں **Area11.Desktop** منتخب ہو (Startup project)
5. **F5** دبائیں (ڈیبگ کے ساتھ چلے گا) یا **Ctrl+F5** (بغیر ڈیبگ)

> ⚠️ `dotnet` فولڈر میں کوئی `.sln` **نہیں** ہے۔ سولوشن صرف روٹ میں ہے۔
> اگر Visual Studio پرانا `Area11.WPF.sln` کھولے تو وہ غلط ہے — بند کر کے `Area11.sln` کھولیں۔

### Visual Studio میں ڈیٹابیس کہاں بنتا ہے

`C:\ProgramData\Area11\area11.db` — یہی اصل ڈیٹا ہے، کبھی حذف نہ کریں۔

---

## 4. VS Code میں کھولنا

1. **Extensions** میں جائیں → **C# Dev Kit** (Microsoft) انسٹال کریں
2. **File → Open Folder…** → `D:\Area11`
3. `Area11.sln` پر کلک کریں (Solution Explorer کھل جائے گا)، یا ٹرمینل میں:

```
dotnet build
dotnet run --project dotnet\Area11.Desktop
```

4. ڈیبگ کے لیے **F5** → پہلی بار "C#" منتخب کریں → وہ خود `.vscode/launch.json` بنا دے گا

---

## 5. ٹیسٹ چلانا

`tools\RUN-TESTS.cmd` پر ڈبل کلک — یا:

```
dotnet test Area11.sln
```

توقع:

```
Passed!  - Failed: 0, Passed: 56, Skipped: 0, Total: 56
```

اگر کوئی ٹیسٹ فیل ہو تو وہ پوری سکرین بھیج دیں۔

---

## 6. رنگ بدلنا (آپ کی پہلی تبدیلی)

سارے رنگ **ایک ہی فائل** میں ہیں:

```
dotnet\Area11.Desktop\Resources\Theme.xaml
```

| نام | ابھی | کہاں استعمال ہوتا ہے |
|---|---|---|
| `BrandBrush` | `#15803D` (سبز) | بٹن، عنوان، ٹوٹل |
| `BrandDarkBrush` | `#166534` (گہرا سبز) | بٹن دبانے پر |
| `BrandSoftBrush` | `#F0FDF4` (ہلکا سبز) | پس منظر کی پٹی |

بدلنے کے بعد `START-AREA11.bat` دوبارہ چلائیں — رنگ فوراً بدل جائے گا۔

> یہ صرف آزمائش ہے۔ بعد میں یہ رنگ **Settings** سے بدلنے کی سہولت بھی آئے گی (اصول E-02:
> ہر چیز قابلِ تبدیل)۔

---

## 7. عام مسائل

| مسئلہ | حل |
|---|---|
| `MSB4025` — `Area11.WPF.csproj` نہیں ملا | `tools\CHECK-AREA11.cmd` چلائیں (پرانا سولوشن ہٹا دے گا) |
| `Area11.sln` موجود ہی نہیں | فولڈر ادھورا ہے — نیا ZIP ایکسٹریکٹ کریں |
| `NU1903` سیکیورٹی وارننگ | اب درست — `SQLitePCLRaw` کو 2.1.12 پر pin کیا گیا ہے |
| بلڈ ہو گیا مگر ایپ نہیں کھلی | `dotnet run --project dotnet\Area11.Desktop` کا آخری حصہ بھیجیں |
| سائن اِن نہیں ہو رہا | نام **خالی**، پاس ورڈ `area11` |
