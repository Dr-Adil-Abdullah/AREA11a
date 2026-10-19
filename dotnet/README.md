# `dotnet/` — the real Area11 desktop application

This folder is the **product**: a native Windows desktop rebuild of Area11 in C#/.NET.
It is created following the Master Specification §4.4 (`Area11.Core` / `Area11.Desktop` /
`Area11.Tests`) and §34.2 (build in a new folder, do not touch the Next.js reference app).

```
dotnet/
├── Area11.sln
├── Area11.Core/                 business logic, SQLite, no UI references
│   ├── Data/                    Database + migration runner
│   │   └── Migrations/          001_core … 015_photos
│   ├── Models/                  plain DTOs
│   ├── Services/                settings, audit, auth, permissions, numbering, products, dashboard
│   └── Utils/                   money, passwords (PBKDF2), paths, logging, audit diff
├── Area11.Desktop/              WPF + MVVM (login, shell, dashboard, products)
└── Area11.Tests/                xUnit (35 cases)
```

## Where things stand

| Item | State |
|---|---|
| Database migrations 001–013 | ✅ ported verbatim, executed in order |
| Migration 014 (per-user permissions) | ✅ added, upgrades an existing database safely |
| Migration 015 (multiple photos) | ✅ added for the owner's photo order |
| Core layer (Data, Models, Utils) | ✅ written |
| Services (settings, audit, auth, permissions, numbering, products, dashboard) | ✅ written |
| WPF shell (login, dashboard, products) | ✅ written |
| 35 xUnit tests | ✅ written — **must be run on Windows** |
| Compile on Windows | ⏳ first build pending (`docs/BUILD-ON-WINDOWS.md`) |
| Column reference for every table | ✅ [`../docs/SCHEMA-REFERENCE.md`](../docs/SCHEMA-REFERENCE.md) |
| Core / Desktop / Tests projects (C#) | ✅ three projects created and filled |
| Owner decisions and phases | ✅ [`../docs/OWNER-DECISIONS.md`](../docs/OWNER-DECISIONS.md) |
| How to build on Windows | ✅ [`../docs/BUILD-ON-WINDOWS.md`](../docs/BUILD-ON-WINDOWS.md) |
| Technical plan and port order | ✅ [`../docs/PORT-PLAN.md`](../docs/PORT-PLAN.md) |

## Rules that apply inside this folder

1. No SQL, no `SqliteConnection`, no file access in ViewModels or XAML code-behind (Spec §4.4.2).
2. Every service method takes/returns plain DTOs and the current user (§4.4.4).
3. Sale, stock deduction, return and shift close are single transactional calls (§4.4.5).
4. Migrations are **append-only**: new file at the end, old files never edited (§5.1.6).
5. Money is integer paisa; quantities are `decimal` (the original schema stores `qty_base` as REAL).

## Migration runner contract (implemented at stage 1)

```
BEGIN IMMEDIATE
  if id not in _migrations:
      execute the whole .sql file
      INSERT INTO _migrations(id)
COMMIT
```
The check is repeated **after** the lock is taken (Spec §5.1.6 — this is the fix for the original
`duplicate column name` crash when two processes opened a new database at the same time).
