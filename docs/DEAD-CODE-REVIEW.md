# فالتو کوڈ کی فہرست — Phase 2 (صرف فہرست، کچھ ہٹایا نہیں گیا)

> اصول (مالک کا حکم): پہلے سب کچھ **ایک طرف لکھنا**، پھر **اکٹھے سوال** — مالک بتائے گا
> ہٹانا ہے یا رکھنا ہے۔ اس فائل کے لکھے جانے تک ایپ کے کوڈ میں **کوئی تبدیلی نہیں** ہوئی۔

## طریقہ

- Core+Desktop کے تمام public methods: باہر سے کوئی caller + کوئی ٹیسٹ نہ ہو تو امیدوار۔
- تمام ViewModels/Views: کوئی `new` / دروازہ / XAML حوالہ نہ ہو تو امیدوار (کوئی نہیں نکلا)۔
- تمام private methods: اپنی فائل میں بھی استعمال نہ ہو تو امیدوار؛ XAML handlers الگ سے
  XAML میں چیک کیے (سب جڑے ہوئے نکلے)۔
- Settings کی تمام seeded keys: seed کے باہر کوئی پڑھنے والا نہ ہو تو امیدوار۔
- تمام scripts/tools: کوئی حوالہ نہ ہو تو امیدوار (سب زندہ نکلے)۔

## A. مردہ methods (کوئی caller نہیں، کوئی ٹیسٹ نہیں)

| # | جگہ | کیا ہے | نوٹ |
|---|---|---|---|
| A1 | `AuthService.SetSecret` (~35 لائنیں) | مالک کسی کا PIN/پاس ورڈ بدلے | **خاص**: Staff ٹیب میں reset کا بٹن ہی نہیں — یہ ملبہ نہیں، ادھوری سہولت ہے |
| A2 | `PurchaseService.GetBatches` (~20 لائنیں) + `BatchView` ماڈل | دوا کی batches دکھانا | `StockService.Batches` وہی کام کرتا ہے اور وہی چلتا ہے — یہ پرانا راستہ ہے |
| A3 | `RecoveryService.NothingOwed` (~12 لائنیں) | "کوئی ادھار نہیں" کی جانچ | کوئی صفحہ اسے بلاتا نہیں |
| A4 | `Money.ParseOrZero` (1 لائن) | لفظ کو پیسہ بنانا، ورنہ صفر | کوئی caller نہیں |
| A5 | `SyncTables.IsRecent` (1 لائن) | کیا ٹیبل نئی فہرست میں ہے | فہرست خود ٹیسٹ میں بندھی ہے، صرف یہ مددگار لائن فالتو ہے |

## B. سوئی ہوئی settings keys (seed ہیں، کوئی پڑھتا نہیں)

| keys | قسم |
|---|---|
| `receipt.showSavings`، `receipt.showOriginalPrice`، `receipt.showCostColumns` | پرچی کی شکل (کوئی UI بھی نہیں) |
| `brand.primaryColor`، `brand.shortName`، `brand.logoDataUrl` | برانڈنگ کے بچے ہوئے لفظ |
| `owner.name` | مالک کا نام (کہیں پڑھا نہیں جاتا) |
| `discount.mode`، `payment.default` | رویے کی keys (صرف ٹیسٹ لکھتے ہیں، ایپ پڑھتی نہیں) |
| `returns.requireInvoice`، `returns.cashierRestockToShelf`، `security.requireLogin`، `menu.pinnedOpen` | رویے کی keys (مکمل سوئی ہوئی) |
| `stock.warnNegative` | پڑھا نہیں جاتا |
| `stock.allowNegative` | **فیصلہ شدہ**: D-93 کے تحت پرانی دکانوں کے لیے رکھا ہے — نہیں چھیڑنا |

## C. جانچ کر زندہ نکلے (نہیں چھیڑنا)

- تمام ViewModels اور Views جڑے ہوئے ہیں؛ تمام XAML handlers XAML میں بندھے ہیں۔
- `HeaderSectionTabs` کے Get/Set (WPF کا لازمی طریقہ)، `EscPosBuilder` (پرچی بناتا ہے)،
  `RelayCommand.CanExecute` / `ConvertBack` (انٹرفیس کے حصے)۔
- تمام `.bat`/`.cmd`/`.ps1` زندہ ہیں (`diagnose.bat` PowerShell-بند PC کا سہارا ہے)۔
- اندرونی-استعمال والے public methods (جیسے `OpenSaleDraft`) — صرف نظر کا معاملہ، خطرہ نہیں۔

## مالک کے جواب (v0.61.0 میں نافذ)

- [x] A1: **بٹن لگا + forgot-password بھی** — Staff ٹیب سے کسی کا بھی PIN/پاس ورڈ؛ لاگ اِن پر
  "forgot password" → "area11" لکھے تو مالک لاگ اِن (اپنا لفظ، پابندی، بلیک باکس سمیت)
- [x] A2: **ہٹا** (`GetBatches` + `BatchView`؛ `StockService.Batches` چلتا ہے)
- [x] A3+A4+A5: **ہٹیں** (چھوٹا ملبہ، میرے فیصلے پر)
- [x] B-شکل: **ہٹیں** (پرچی x3، برانڈ x3، مالک کا نام)
- [x] B-رویے: **ہٹیں** سوائے دستاویزی legacy کے — `discount.mode`، `stock.allowNegative`،
  `stock.warnNegative` پرانی دکانوں کے لیے رکھیں (فیصلہ کوڈ میں لکھا تھا)
- اضافی ملا اور ہٹا: `payment.methods`، `menu.collapsedGroups` (دونوں سوئی ہوئی نکلیں)؛
  `MenuStateTests` → `QuietSettingsTests` (زندہ keys پر، 6=6)
