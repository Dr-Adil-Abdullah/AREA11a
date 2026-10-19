# `dotnet/` — the real Area11 desktop application

This folder is the **product**: a native Windows desktop rebuild of Area11 in C#/.NET.
It is created following the Master Specification §4.4 (`Area11.Core` / `Area11.Desktop` /
`Area11.Tests`) and §34.2 (build in a new folder, do not touch the Next.js reference app).

```
dotnet/
└── Area11.Core/
    └── Data/
        └── Migrations/        001_core … 014_user_permissions  (verbatim from the reference)
```

## Where things stand

| Item | State |
|---|---|
| Database migrations 001–013 | ✅ ported verbatim, executed in order, 31 tables verified |
| Migration 014 (per-user permissions) | ✅ added, upgrades an existing database safely |
| Column reference for every table | ✅ [`../docs/SCHEMA-REFERENCE.md`](../docs/SCHEMA-REFERENCE.md) |
| Core / Desktop / Tests projects (C#) | ⏳ stage 1 — starts after the owner confirms Q-01 |
| Owner decisions and phases | ✅ [`../docs/OWNER-DECISIONS.md`](../docs/OWNER-DECISIONS.md) |
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
