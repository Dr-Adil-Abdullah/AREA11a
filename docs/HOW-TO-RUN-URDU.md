# Area11 چلانے کا طریقہ (آسان، اردو میں)

> بس ایک فائل: ریپو کے فولڈر میں **`START-AREA11.bat`** پر ڈبل کلک کریں۔

## پہلی بار (صرف ایک بار)

1. **`https://dotnet.microsoft.com/download/dotnet/10.0`** کھولیں اور **.NET 10 SDK** انسٹال کریں۔
   (یہ مفت ہے، ایک بار انسٹال کرنا ہے)
2. ریپو کا فولڈر کھولیں — جیسے `D:\areaa`
3. اس میں **`START-AREA11.bat`** پر ڈبل کلک کریں۔

بس۔ باقی سب وہ خود کرتا ہے: کوڈ اپ ڈیٹ، بلڈ، اور ایپ چلانا۔

## ہر بار

`START-AREA11.bat` پر ڈبل کلک — کھڑکی کھل جائے گی، کچھ لمحے بلڈ ہوگا، پھر Area11 کھل جائے گا۔

## سائن اِن

| خانہ | کیا لکھیں |
|---|---|
| User name | **خالی چھوڑ دیں** (یہ مالک کا لاگ اِن ہے) |
| Password | `area11` |

## "D:\areaa\Area11.WPF\Area11.WPF.csproj not found" کیوں آ رہا تھا

یہ کوئی کوڈ کی خرابی نہیں تھی۔ آپ کے کمپیوٹر پر **پرانے پروٹوٹائپ کا سولوشن** (`Area11.WPF.sln`)
ابھی بھی موجود تھا اور Visual Studio وہی کھول رہا تھا۔ وہ پروٹوٹائپ اب `prototypes\` فولڈر میں
منتقل ہو چکا ہے، اس لیے پرانا راستہ نہیں مل رہا تھا۔

`START-AREA11.bat` اب پرانی `Area11.WPF.sln` خود ہٹا دیتا ہے۔

اگر پھر بھی وہی پیغام آئے تو:

1. Visual Studio **بند** کریں
2. `D:\areaa` میں سے `.vs` فولڈر اور `Area11.WPF.sln` فائل حذف کریں
3. اگر `D:\areaa\Area11.WPF` فولڈر اب بھی موجود ہے تو اسے بھی حذف کر دیں (وہ پرانا ہے)
4. `START-AREA11.bat` دوبارہ چلائیں

## Visual Studio سے چلانا ہو تو

1. Visual Studio بند کریں، پھر کھولیں
2. **`D:\areaa\Area11.sln`** کھولیں — یہی اصل سولوشن ہے (روٹ میں، `dotnet` فولڈر میں نہیں)
3. اوپر **Area11.Desktop** منتخب کریں اور **F5** دبائیں

> `Area11.sln` صرف ایک ہے۔ `prototypes\` والے فولڈر پرانا تجربہ ہیں — اُنہیں نہ کھولیں۔

## اگر بلڈ فیل ہو

کھڑکی بند نہ کریں۔ **سب سے اوپر والی پہلی error لائن** کاپی کر کے چیٹ میں بھیج دیں، جیسے:

```
SaleService.cs(337,22): error CS0121: The call is ambiguous between ...
```

اِسی طرح کی ایک لائن کافی ہے — باقی میں خود سمجھ جاؤں گا۔

## ڈیٹا کہاں ہے

`C:\ProgramData\Area11\area11.db` — یہ فائل کبھی حذف نہ کریں۔ اپ ڈیٹ سے اسے کوئی نقصان نہیں ہوتا۔

---

## اگر Visual Studio میں `Area11.WPF.csproj` نہ ملنے کا ایرر آئے (MSB4025)

یہ ایرر **کوڈ میں نہیں**، آپ کے کمپیوٹر پر Visual Studio کی یادداشت میں ہے۔ وہ پرانا سولوشن
(`Area11.WPF.sln`) کھولتا رہتا ہے جو اب `prototypes\` میں منتقل ہو چکا ہے۔

### سب سے آسان حل (ایک کمانڈ)

Visual Studio کا ٹرمینل کھولیں (**View → Terminal**) اور یہ چلائیں:

```powershell
powershell -ExecutionPolicy Bypass -File D:\areaa\tools\doctor.ps1
```

یہ خود بتا دے گا کہ کیا غلط ہے۔ اگر اس نے کہا کہ پرانی فائلیں ملی ہیں تو مرمت کے لیے:

```powershell
powershell -ExecutionPolicy Bypass -File D:\areaa\tools\doctor.ps1 -Fix
```

یہ پرانا `Area11.WPF.sln` ہٹاتا ہے، `.vs` فولڈر صاف کرتا ہے، اور پرانے `Area11.WPF` فولڈر کا
نام بدل دیتا ہے — **کچھ حذف نہیں ہوتا**۔ پھر Visual Studio بند کر کے **`D:\areaa\Area11.sln`**
کھولیں۔

### ایک ایک کر کے خود دیکھنا ہو تو (کمانڈز)

پہلے Visual Studio کا ٹرمینل کھولیں اور:

**قدم 1 — ہم کس فولڈر میں ہیں؟**

```powershell
cd D:\areaa
Get-Location
Get-ChildItem -Force | Select-Object Mode, Name
```

توقع: `Area11.sln`، `dotnet`، `prototypes`، `tools`، `docs`، `START-AREA11.bat`۔

**قدم 2 — کتنے سولوشن فائل ہیں؟**

```powershell
Get-ChildItem -Recurse -Filter *.sln | Select-Object -ExpandProperty FullName
```

توقع: صرف دو — `D:\areaa\Area11.sln` اور `D:\areaa\prototypes\prototypes.sln`۔
اگر `D:\areaa\Area11.WPF.sln` بھی دکھے تو **وہی پرانا مجرم ہے**۔

**قدم 3 — سولوشن کس پروجیکٹ کی طرف اشارہ کر رہا ہے؟**

```powershell
Select-String -Path .\Area11.sln -Pattern 'csproj'
```

توقع: تین لائنیں — `dotnet\Area11.Core\...`، `dotnet\Area11.Desktop\...`، `dotnet\Area11.Tests\...`۔
اگر `Area11.WPF\Area11.WPF.csproj` لکھا ہو تو آپ پرانا سولوشن کھول رہے ہیں۔

**قدم 4 — پروجیکٹ فائلیں کہاں ہیں؟**

```powershell
Get-ChildItem -Recurse -Filter *.csproj | Select-Object -ExpandProperty FullName
```

توقع: `dotnet\` کے تین، اور `prototypes\` کے دو۔

**قدم 5 — کوڈ کا ورژن کیا ہے؟**

```powershell
git branch --show-current
git log --oneline -1
git status --short
```

توقع: برانچ `arena/a8a3b2e0-area11a`، آخری commit `7a18b22` یا اس کے بعد کا۔
اگر `git status` بہت سی فائلیں دکھائے تو پہلے بتائیں، `git pull` نہ چلائیں۔

**قدم 6 — صفائی (صرف قدم 2 میں پرانی فائل ملنے پر)**

```powershell
Remove-Item .\Area11.WPF.sln -Force
Remove-Item .\.vs -Recurse -Force
```

پھر **Visual Studio بند کریں** اور `D:\areaa\Area11.sln` دوبارہ کھولیں۔

**قدم 7 — بلڈ**

```powershell
dotnet build .\Area11.sln -v minimal
```

### یاد رکھیں

- Visual Studio میں ہمیشہ **`D:\areaa\Area11.sln`** کھولیں — `dotnet` فولڈر والا نہیں، `prototypes`
  والا نہیں۔
- `C:\ProgramData\Area11` (ڈیٹا) کو کبھی نہ چھیڑیں۔
