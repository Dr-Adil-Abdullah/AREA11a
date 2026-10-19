# AREA11a

Native Windows desktop rebuild of the **Area11 Smart Pharmacy & Retail POS + Inventory** system
in **C# / .NET 10 / WPF**, ported from the reference web app (`Dr-Adil-Abdullah/Area11`).

**Phase 1 is in progress:** the three-project solution, the exact database schema, sign-in with
per-person permissions, the dashboard and the products screen are written and waiting for their
first Windows build ([`docs/BUILD-ON-WINDOWS.md`](docs/BUILD-ON-WINDOWS.md)).

| Path | What it is |
|---|---|
| `dotnet/` | **The product** — `Area11.sln` (Core / Desktop / Tests), 15 migrations, 35 tests |
| `Area11.WPF/` | Working prototype from the first stage (proves the flows; replaced by `dotnet/`) |
| `WPFSample/` | The very first smoke-test window ("app is running") |
| `docs/` | Owner decisions, port plan, schema reference, status |

## Read these first

| Document | Contents |
|---|---|
| [`docs/OWNER-DECISIONS.md`](docs/OWNER-DECISIONS.md) | Every decision of the owner with numbers (D-01…), the 6-phase plan, open questions |
| [`docs/PORT-PLAN.md`](docs/PORT-PLAN.md) | Technical study: conflicts, module map, test map, port order `net-stage-1…14` |
| [`docs/SCHEMA-REFERENCE.md`](docs/SCHEMA-REFERENCE.md) | Column-by-column reference of all 31 tables (generated from the real schema) |
| [`docs/BUILD-ON-WINDOWS.md`](docs/BUILD-ON-WINDOWS.md) | How to build, run and test on Windows |
| [`docs/WPF-STATUS.md`](docs/WPF-STATUS.md) | Urdu status of the prototype stage |

## Database — already ported

`dotnet/Area11.Core/Data/Migrations/` holds the **original 13 migrations verbatim** plus
`014_user_permissions.sql` (per-person permissions) and `015_photos.sql` (2–3 photos per product,
supplier and customer). All 15 execute in order.

---|---|
| `Area11.WPF/` | **The product** — Windows POS + inventory application (.NET 8, WPF, MVVM, SQLite) |
| `WPFSample/` | The first smoke-test window ("app is running"), kept as-is from the initial upload |

---

## Run it

Requirements: **Windows 10/11** and the **.NET 8 SDK** (https://dotnet.microsoft.com/download).

```bat
cd Area11.WPF
dotnet run
```

Or open `Area11.WPF.sln` in Visual Studio 2022 and press **F5**.

**First sign-in:** leave the user name empty and type the owner password **`area11`**.
Change it (and staff PINs) before real use — the reference app ships the same first-run default.

The database is created automatically on first launch:

```
Area11.WPF/bin/Debug/net8.0-windows/data/area11.db
```

`data/`, `bin/` and `obj/` are git-ignored — business data never goes to GitHub.

---

## Architecture

```
XAML View (UI only)
        │  state flows OUT, actions flow IN
        ▼
ViewModel (INotifyPropertyChanged + RelayCommand)
        │
        ▼
Service layer (business rules)  ── try/catch ──► LoggerService + friendly message
        │
        ▼
Data layer (Microsoft.Data.Sqlite, transactions, migrations)
        │
        ▼
Models + Utils (money = integer paisa, pure helpers)
```

- **Views** contain no business logic and no SQL.
- **Services** own every rule and transaction; they never throw at the UI (`OperationResult`).
- **ViewModels** hold screen state and commands only.
- **Data** applies ordered `.sql` migrations once and keeps the `_migrations` ledger.

### Folder structure

```
Area11.WPF/
├── App.xaml / App.xaml.cs          startup: config → database → login → shell
├── appsettings.json                DB path, log path, security, API/sync endpoints
├── Models/                         User, Product, Category, Company, ProductInput, DashboardSnapshot
├── Services/                       AppServices, AuthService, ProductService, ReportService,
│                                   SettingsService, AuditService, LoggerService, DatabaseSeed,
│                                   SettingsDefaults, OperationResult
├── ViewModels/                     Login, Shell, NavItem, Dashboard, Products, Settings (+ Base/)
├── Views/                          LoginView, ShellView, DashboardView, ProductsView, SettingsView
├── Utils/                          MoneyHelper, FormatHelper, PasswordHasher, AppConfig
├── Data/                           Database, DbReaderExtensions, Migrations/001_core.sql
└── Resources/                      Styles.xaml (single reusable design system)
```

---

## Implemented in this build

- **Sign-in + roles** — owner password, staff name + PIN; PBKDF2-SHA256 hashes; 5-attempt lockout;
  login events written to the black box. Owner / Manager / Cashier control visible navigation.
- **Product catalog** — search by name, barcode, salt, rack or room; category filter; low-stock filter;
  create and edit products; Box → Strips → Units pack formula; cost / retail / VIP / doctor prices;
  category and company can be created inside the product form (U-18).
- **Settings** — ~45 editable keys, all database-backed, grouped and searchable; each change audited.
- **Dashboard** — today's sales and bills, product count, low stock, expiring batches, stock value at cost.
- **Database** — full reference schema (products, batches, customers, suppliers, purchases, sales,
  payments, shifts, expenses, returns, quarantine, stock movements, stock-take, alerts, audit,
  custom fields) created by migration `001_core.sql`; WAL mode; transactional writes.
- **Audit black box** — module, action, entity, old value → new value, never deleted.
- **Purchases (PINV)** — supplier, batch number, expiry (month or full date), cost/retail,
  sample/bonus lines at zero cost, immediate stock-in, supplier payable and partial payment.
- **Counter / POS** — FEFO batch selection, Box/Strip/Unit selling, live totals computed by the
  same service that saves the bill, discount (margin/retail base, per-role ceiling, never below
  cost), round-down with cost guard, cash tendered → change, credit sales to a customer account,
  negative stock allowed with warnings only (owner rule U-30).
- **Numbering** — one counter table issues INV / PINV / RET / Q / SR / PR / ST numbers inside the
  same transaction as the document.
- **Local-first** — works with no internet; the SQLite file is the source of truth.

## Next modules (in the reference order)

1. Hold cart, split payment UI and the printed receipt
2. Returns: strict validation, `RET-0001`, provisional `PR-0001`, quarantine `Q-0001`
4. Stock write-off / stock-take (`ST-0001`)
5. Galla / shift handover
6. Reports (today / 7 / 30 days), alerts, backup, Excel, Supabase sync

## Reference mapping

| Reference (Next.js) | WPF |
|---|---|
| `src/lib/sales.ts` FEFO, discount, round-off | `Services/SaleService` (next) + `Utils/MoneyHelper` |
| `src/lib/returns.ts` | `Services/ReturnService` (next) |
| `src/lib/settings.ts` (70 keys) | `Services/SettingsDefaults` + `SettingsService` |
| `src/lib/audit.ts` (old → new) | `Services/AuditService` |
| `src/lib/db.ts` migrations, `tx()` | `Data/Database.cs` + `Data/Migrations` |
| `lib/auth` owner password / staff PIN | `Services/AuthService` + `Utils/PasswordHasher` |

## Conventions

- Money is **integer paisa** everywhere; formatting only at the edge (`FormatHelper`).
- Public members PascalCase, private fields camelCase, no abbreviations in names.
- No hard-coded secrets, URLs or paths — `appsettings.json` only.
- No empty `try/catch`; every failure is logged and returns a friendly message.
- Documentation explains **what** and **why**.

## Notes

- `Area11.WPF` targets `net8.0-windows`; it cannot be built on Linux, so the first compile should be
  done on Windows (or a Windows CI runner).
- Status, decisions and the remaining work are tracked in [`docs/WPF-STATUS.md`](docs/WPF-STATUS.md).
