# AREA11 — MASTER SPECIFICATION (SINGLE-FILE REBUILD BLUEPRINT)

**Smart Pharmacy & Retail POS + Inventory Management System**

- Document version: 1.0
- Compiled: 11 October 2026
- Source repository: `github.com/Dr-Adil-Abdullah/Area11` (state: v0.6.2, checkpoint `stage-43`, 7 October 2026)
- Language of this file: English only (an Urdu translation can be produced afterwards, section by section)
- Purpose: if this single file is handed to a competent developer or AI coding agent, the application can be rebuilt completely, in the same way it behaved before, in any technology (the chosen target is a native Windows desktop application in C# / .NET, see Section 34).

---

## 1. HOW TO USE THIS DOCUMENT

### 1.1 Purpose

1.1.1 This file consolidates, in one place, every rule, decision, feature, setting, formula, workflow, acceptance test, known defect fix, and open question that was previously scattered over these source documents: the Master Specification (`complete_numbered_master_specs (1).md`), `PHASE-TWO-INFORMATION.md` (136 numbered rules), `INPUT-INFORMATION.md` (A-01…A-49 and Q-01…Q-19), `HANDOFF.md`, `AGENTS.md`, `README.md`, `docs/APP-TAFSEEL.md` and `docs/SUPABASE.md`.

1.1.2 The product it describes is a complete, professional, offline-first pharmacy point-of-sale and inventory system that already exists as a web application (Next.js + SQLite). The rebuild must reproduce all of its behavior.

1.1.3 The file is written so that the reader does not need to open any other document to understand what must be built. Where the source documents did not give a detail, this is stated openly in Section 37 (Known gaps) instead of being invented.

### 1.2 Authority and precedence

1.2.1 The Master Specification (reproduced in Appendix A) is the "Single Source of Truth" for scope and phase order.

1.2.2 Where the owner later gave a newer order (for example U-30 to U-36, 6 to 7 October 2026), the newer order overrides the older text.

1.2.3 Where the original implementation behaved differently from the written specification, both are stated, and the difference is listed in Section 35 (Open points) with a recommended default.

1.2.4 Section 3 (Standing rules) applies to every other section.

### 1.3 Conventions used in this file

1.3.1 `[U-17]`, `[R-03]`, `[E-02]`, `[S-01]`, `[P-01]`, `[B-01]`, `[Z-03]`, `[T-03]`, `[M-01]`, `[Q-08]` = the owner's numbered rules and decisions (Section 3).

1.3.2 `Spec §x.y.z` = the numbered clause of the Master Specification (Appendix A).

1.3.3 "ORIGINAL" = how the existing web application behaves today. "REQUIRED" = must be reproduced. "TARGET" = a choice made for the rebuild.

1.3.4 Money is always written in rupees in examples (Rs 545), but is **stored as integer paisa** (Rs 545 = 54 500 paisa). See 5.1.

1.3.5 Roman-Urdu words used by the original code and documents (udhaar, galla, ginti, almari, parchi, etc.) are explained in the glossary (Section 36).

---

## 2. PRODUCT IDENTITY AND CONTEXT

### 2.1 Identity

2.1.1 Product name (default, editable in Settings): **Area11**. Default store name: "Area11 Pharmacy" `[Q-01]`. Short name is also editable.

2.1.2 Product type: Smart Pharmacy & Retail POS + Inventory Management System.

2.1.3 Owner and primary stakeholder: Dr. Adil Abdullah (Urdu-speaking). The owner is not a programmer. The system must be understandable and operable by him and by shop staff.

2.1.4 The developer/agent must talk to the owner in Urdu; code, database and user interface are in English `[Q-07]`.

### 2.2 Business context

2.2.1 One pharmacy shop with **two counters plus the owner's mobile phone**, all on the shop's local network (LAN) `[Q-14] [Q-15] [Q-17]`. (Rebuild note: see 4.3 for the agreed staged approach — one computer first, second counter later.)

2.2.2 Must be **100 % usable without internet** `[Spec §1.2.1]`. The counter must never stop when the internet goes away.

2.2.3 Currency: Pakistani Rupee (PKR), stored as integer paisa. Dates and times: local time, Asia/Karachi.

2.2.4 Payments at the counter: cash and credit (udhaar) first `[Q-16]`; online/card exist as optional payment methods that can be switched on in Settings.

2.2.5 Hardware: thermal receipt printers (58 mm and 80 mm, model unknown) `[Q-10]`; USB barcode scanner (acts like a keyboard) `[R-09]`; shop PC/laptop (Windows preferred), Android tablet/phone later.

2.2.6 Tax is OFF by default (0 %) with a master switch `[Q-09]`.

2.2.7 Loyalty points are OFF by default, with settings ready `[Q-06]`.

2.2.8 Owner's intent for growth (informational): possible later expansion to several shops (`sync.shopId` already exists), and a possible Android companion app (see 34.8).

### 2.3 Roles of people

2.3.1 **Owner** — unlimited rights, real total profit and loss, user management, black-box audit log, alert dismissal, stock-take apply, backup/restore, bulk data update, cloud sync.

2.3.2 **Manager** — changes item prices, sets credit limits, clears pending bills, views operational reports, manages purchases/suppliers/stock write-offs. (Spec: net profit and the secret audit are hidden from the manager — see open point 35.4.)

2.3.3 **Cashier** — only creates bills, holds carts, gives discount within the allowed limit, handles customers and returns in a restricted way.

Full permission matrix: Section 7.4.

---

## 3. STANDING RULES (THE OWNER'S NUMBERED MANDATES)

> Every rule below is binding on the whole rebuild. They are reproduced with their original numbers so that nothing is lost.

### 3.1 User rules (U)

3.1.1 **[U-01]** Whatever technology is used, a **live preview** must be available.

3.1.2 **[U-02]** It must be possible to **go back to an earlier stage / earlier edit**.

3.1.3 **[U-03]** Everything is stored in the **Area11 repository**.

3.1.4 **[U-04]** First look at **all files**; if something is missing, ask; if complete, start work.

3.1.5 **[U-05]** First the necessary/basic work, after that the main work.

3.1.6 **[U-06]** This is a **business** — there must be **no recurring technical errors** (zero error).

3.1.7 **[U-07]** Whatever code, library, framework feature or repository already exists — **use that**.

3.1.8 **[U-08]** **Write as little code as possible.**

3.1.9 **[U-09]** Personal use — no license restriction (condition: mature code).

3.1.10 **[U-10]** **Everything must be editable** — app name, logo, categories… nothing is final (see E).

3.1.11 **[U-11]** **A change made on the front end must automatically reach the back end/data** (see S).

3.1.12 **[U-12]** Every point is written **numbered**, so nothing is missed.

3.1.13 **[U-13]** **One link is enough**: giving only the repository link must allow any new agent to understand what is done and what remains. `HANDOFF.md` and `AGENTS.md` are always kept current. (Order of 1 Oct 2026.)

3.1.14 **[U-14]** **Update documents after every agent session**: after every feature/decision update `HANDOFF.md`, the rule numbers file, and the README so the next agent starts exactly where the last stopped.

3.1.15 **[U-15]** **Bring the necessary forward**: a feature that belongs to a later phase but without which the shop cannot work (returns, credit collection, backup, day-end) is done earlier.

3.1.16 **[U-16]** The next work is given to a **new/full agent** with only the repository link; the questions (Q-01…Q-19) are already settled and must not be asked again.

3.1.17 **[U-17] Customization First.** On every screen the owner must be able to make his own **fields**, click any item to see its **full detail**, find **small clickable option blocks inside Settings** (each opening its own detail), and use **filter / sort / group** on every list. Nothing hard-coded. (5 Oct 2026)

3.1.18 **[U-18]** Category/company etc. are **created where they are chosen**: wherever a drop-down exists there is "＋ New" — no need to leave to another page. (5 Oct 2026)

3.1.19 **[U-19]** **Photos live in a folder** (`data/photos/`), the database stores only the file name — keeps the database light. (5 Oct 2026)

3.1.20 **[U-20]** **The basis of discount is chosen by the owner**: on profit or on retail — **both are built**, switchable in Settings. (5 Oct 2026)

3.1.21 **[U-21]** **Medicine room/almari + camera photo**: each medicine carries "where it is kept" and a photo — both. (5 Oct 2026)

3.1.22 **[U-22]** **Filter + sort in the three big lists**: medicines, customers, bills — and in the medicine list **grouping** (category/company/room) that can be expanded/collapsed. (5 Oct 2026)

3.1.23 **[U-23]** **Git updated with every change**: whenever the chat is closed, code + documents must both be on GitHub; "no work should be such that it cannot be updated". (5 Oct 2026)

3.1.24 **[U-24]** **Remaining work one by one**: finish the app first, after each item update git, and keep a readable done/remaining list inside the files. (5 Oct 2026)

3.1.25 **[U-25]** **Negative inventory**: the system never stops a sale even if stock goes below zero — but a big red alert appears at the very top of the screen. (6 Oct 2026, Spec 1)

3.1.26 **[U-26]** **Every stock movement is real-time**: every sale and every return changes stock at that moment — and if a batch is empty the record still continues (stock really goes negative). (6 Oct 2026, Spec 1.3)

3.1.27 **[U-27]** **A log in every module is mandatory**: every action (sale, return, stock, edit, delete, login) is stored permanently with **user name/id, time, action, old value and new value**. Unauthorized attempts are also recorded. (6 Oct 2026, Spec 2)

3.1.28 **[U-28]** **Strict return validation**: a return only against the **original bill number**; more than what was sold or more refund than what was collected is **impossible**; after approval stock and money records correct themselves. (6 Oct 2026, Spec 3)

3.1.29 **[U-29]** **Instant notice at the counter**: a stock problem or price change message appears on the billing screen **at that moment** (toast + top alert bar). (6 Oct 2026, Spec 4)

3.1.30 **[U-30]** **Negative stock must never stop any work**: supplier return, stock adjust, write-off — anywhere the quantity exceeds available stock, **no error, only an alert (warning)**; the work completes even if stock goes negative. (6 Oct 2026)

3.1.31 **[U-31]** **Every alert is dismissed only by the owner, by writing a reason**: any alert (negative stock, no-bill return, etc.) can be "ended" only by the **Owner** and a **reason is compulsory**; the reason is stored and logged. Until the owner dismisses it with a reason, the alert shows on every page. (6 Oct 2026)

3.1.32 **[U-32]** **Returned goods go straight into stock, no quarantine**: the moment goods are taken back from a customer (as soon as money goes out) they are **added to stock at once**. Damaged/expired goods are not taken back at all, so no separate quarantine is needed. (6 Oct 2026)

3.1.33 **[U-33]** **No-bill (rush) return: stock immediately + alert stays**: on a return without a bill the goods go into stock immediately; the bill may be attached later, but the alert **keeps showing until the owner writes a reason and dismisses it** (even after the bill is attached). (6 Oct 2026)

3.1.34 **[U-34]** **Quarantine stays, but with full accounting**: every item in quarantine has its **own permanent number which can never change**; the history (black box + quarantine list) must show clearly **which item went where** — back to stock, **expired/damaged** (write-off), or sold. From quarantine goods can be (a) returned to stock, (b) sent to damaged/expired, (c) **sold if wanted**; the negative-stock rule applies here too. **While goods are in quarantine the alert keeps going**, and the owner can end it by writing a reason (as U-31). (7 Oct 2026)

3.1.35 **[U-35]** **A printable receipt for every return**: containing the **permanent return number**, date/time, cashier, original bill number, medicine + quantity + refunded amount, reason, total refund — ready for printing (thermal/A4). (7 Oct 2026)

3.1.36 **[U-36]** **Work one by one** in the owner's order: (1) the **receipt**, (2) a **final review of the whole app + report**, (3) **cleaning of useless Settings lines**. One finished and uploaded, then the next. (7 Oct 2026)

### 3.2 Reuse rules (R) — "less code"

3.2.1 **[R-01]** Before every feature ask: "does a solution already exist?" Yes = take it.

3.2.2 **[R-02]** Six steps: **look → check → take → connect → try → save**.

3.2.3 **[R-03]** Matrix (how much new code): ready-made project ~5 % · built-in feature ~10 % · library ~20 % · module ~40 % · fully new code = last resort.

3.2.4 **[R-04]** Not only code — reuse **methods/designs** too.

3.2.5 **[R-05]** Condition: **mature, working** code — not experimental/broken.

3.2.6 **[R-06]** Review of pharmacy repositories (original study): pharma-suite (audit log/RBAC/invoice), Pharmtech-POS (shift/reorder), a Next.js dashboard (UI); the rest (PHP/desktop) light/incomplete.

3.2.7 **[R-07]** **Decision: do not fork a whole repository** — because the owner-specific parts exist in none (Box→Strip→Tablet, margin discount, downward round-off, owner drawings, black box, Urdu).

3.2.8 **[R-08]** Yes, **libraries are used** — 70–80 % of code saved.

3.2.9 **[R-09]** **USB barcode gun = zero code** (it types like a keyboard).

3.2.10 **[R-10]** Whatever is used is recorded in the rules file for reference.

### 3.3 Editability rules (E) — everything can be changed

3.3.1 **[E-01]** **Nothing is hard-coded** — everything comes from data/settings.

3.3.2 **[E-02]** **Branding**: app **name, logo, colour/theme** — changeable from Settings.

3.3.3 **[E-03]** **Shop information**: name, address, phone, terms — printed on the receipt.

3.3.4 **[E-04]** **Categories**: create, change, delete, order (unlimited + sub-categories).

3.3.5 **[E-05]** **Custom fields**: owner makes them — text / number / amount / photo (Spec §2.2).

3.3.6 **[E-06]** **Unit and packing**: per product `1 Box = X Strips = Y Tablets` — changeable later.

3.3.7 **[E-07]** **Expiry levels**: days + colour from settings (levels 1/2/3).

3.3.8 **[E-08]** **Rates and discount**: kinds of rate, method of discount (margin/retail), limit — all from settings.

3.3.9 **[E-09]** **Round-off, tax, invoice format, printer size (58/80 mm)** — from settings.

3.3.10 **[E-10]** **Receipt layout**: date top/bottom, show/hide columns — by toggle.

3.3.11 **[E-11]** **Roles and permissions**: Owner/Manager/Cashier powers — from settings.

3.3.12 **[E-12]** **Language/terms**: words can also be changed (custom labels).

3.3.13 **[E-13]** **Rule**: nothing is "final" — **every item has an edit option later**.

3.3.14 **[E-14]** Start from default values but all **changeable**; when a category is changed/removed **old records stay safe**.

### 3.4 Front-end → back-end automatic rules (S)

3.4.1 **[S-01]** **Create a category on the front end → it is created in the database automatically.**

3.4.2 **[S-02]** **No separate developer work** — the user can create/change himself.

3.4.3 **[S-03]** **UI and data are always consistent** (one source of truth).

3.4.4 **[S-04]** **A new custom field** → appears in the form **automatically**.

3.4.5 **[S-05]** **Change a name/term** → changes everywhere automatically.

3.4.6 **[S-06]** Database structure (schema) changes happen by **automatic migration** — no data lost.

3.4.7 **[S-07]** Settings are stored **in the database** (not only in the browser/local UI) — so identical on every counter/device.

3.4.8 **[S-08]** Every change has a **record** (who, when changed) — Spec §13.

3.4.9 **[S-09]** **Offline too** — a change made offline syncs when the net returns.

### 3.5 Live preview rules (P)

3.5.1 **[P-01]** On every change **automatic refresh** — the owner sees the app running.

3.5.2 **[P-02]** Server bound to `0.0.0.0` so the preview reaches the owner's browser. (Rebuild: equivalent — the owner can run and look at the app after every module.)

3.5.3 **[P-03]** After each stage the **owner tries it himself** (the agent says what to look at).

3.5.4 **[P-04]** Any error is **caught immediately** (hot reload + automatic checks).

3.5.5 **[P-05]** Preview is **not optional — it is a compulsory part** of the work.

### 3.6 Going back / checkpoint rules (B)

3.6.1 **[B-01]** Before every stage a save point: `bash scripts/ckpt.sh save "name"`.

3.6.2 **[B-02]** Return to any older stage: `ckpt.sh go <number>`.

3.6.3 **[B-03]** One step back: `ckpt.sh undo`.

3.6.4 **[B-04]** List: `ckpt.sh list` · difference: `ckpt.sh diff <n>` · status: `ckpt.sh status`.

3.6.5 **[B-05]** Even unsaved work is not lost — an automatic **safety snapshot** (tested).

3.6.6 **[B-06]** Nothing is **ever erased** — history stays linear.

3.6.7 **[B-07]** Safe across sessions: `ckpt.sh push` (GitHub).

3.6.8 **[B-08]** Helper scripts: `sync-spec.sh` (spec → Part 1 automatic copy), `rules-count.sh` (counting of the numbers; says `SAB THEEK` when no rule number is missing).

3.6.9 **[B-09]** The session branch is fixed, but **tags/commits** allow complete return. The earlier session branch's history and tags `stage-1…stage-6` were fast-forwarded into the current branch on 3 Oct 2026 — nothing lost.

3.6.10 **[B-10]** **Return is possible even after merges**: tags `stage-1…N` and every commit always remain. To look: `git checkout stage-N`, or `ckpt.sh go N`; to undo a merge: `git revert -m 1 <merge-commit>`. **Never delete tags/history; never force-push.**

### 3.7 Zero-error rules (Z) — compulsory for a business

3.7.1 **[Z-01]** Run every piece of work yourself and look, then save.

3.7.2 **[Z-02]** A new change must **not break old working parts** (test at every stage).

3.7.3 **[Z-03]** No mistakes in calculation/money (**round-off only downward** — 545→540).

3.7.4 **[Z-04]** Data is **always safe** (auto-save + transactions).

3.7.5 **[Z-05]** **Wrong entry reversible** (Undo / see the earlier state).

3.7.6 **[Z-06]** **Offline** too — no error (Spec §1.2.1).

3.7.7 **[Z-07]** If the printer/barcode device is absent the app **must not stop**.

3.7.8 **[Z-08]** Data is **one local file (SQLite)** — backup = copy of one file.

3.7.9 **[Z-09]** **Audit log** (unchangeable) — to reach the root of any mistake.

3.7.10 **[Z-10]** Each stage **passes through the owner's hands**, then move forward.

3.7.11 **[Z-11]** **Round-off + cost protection** (owner-confirmed): if rounding down to a multiple of 10 would take the amount **below the purchase cost**, the rounding is not applied (bill 545, cost 543 → collect 545; bill 545, cost 500 → 540). Original function: `applyRoundWithCostGuard`. A warning is shown when a bill is below cost.

### 3.8 Technology rules (T) — original choices (reference)

3.8.1 **[T-01]** TypeScript + Next.js — screen + server in one place (less code).

3.8.2 **[T-02]** UI: Tailwind + shadcn/ui + Lucide — ready tables/forms/dialogs.

3.8.3 **[T-03]** Data: **Node's own SQLite (`node:sqlite`) — zero dependency** (Prisma was planned first but its engine binaries could not be downloaded; the built-in SQLite is better: no extra package = fewer errors). One file, offline, fast, WAL mode.

3.8.4 **[T-04]** Offline: PWA + Dexie + Workbox (Spec §1.2).

3.8.5 **[T-05]** Charts: Recharts · tables: TanStack Table (virtual).

3.8.6 **[T-06]** Forms: react-hook-form + Zod (automatic validation).

3.8.7 **[T-07]** Search: Fuse.js (even a wrongly spelled medicine is found).

3.8.8 **[T-08]** Print: node-thermal-printer / ESC-POS (58/80 mm) + react-to-print (A4).

3.8.9 **[T-09]** WhatsApp: **free `wa.me` link** — no paid API (Spec §11.2.5).

3.8.10 **[T-10]** Excel: SheetJS (import old data / export reports).

3.8.11 **[T-11]** Live preview: dev server (`0.0.0.0`).

3.8.12 **[T-12]** Return/go-back: Git + tags (no new engine to write).

3.8.13 **[T-13]** No extra library for the schema: SQL schema + migrations written by hand (short, clean, understandable).

3.8.14 **[T-14]** **Two app lines**: (1) the **real product = root folder** (Next.js + node:sqlite, real database, offline); (2) `legacy-demo-vite/` = a Vite browser demo of another session (phases 1–3, sample data, 212 unit tests + 36 end-to-end specs, docs) kept for **reference/reuse** (logic for returns, cash drawer, supplier ledger in `src/domain/`: `returns.ts`, `cash.ts`, `accounts.ts`, `suppliers.ts`, `alerts.ts`, `arithmetic.ts`); it must not be merged into the root.

### 3.9 Milestone rules (M)

3.9.1 **[M-01]** Stone 0 — foundation + checkpoint + information files + numbered rules ✅.

3.9.2 **[M-02]** Stone 0.5 — Next.js + database + **live preview running** + **settings skeleton (E + S from day one)**.

3.9.3 **[M-03]** Phase 1 (Core Lifeline): auto PINV stock-in + batch + expiry + supplier account; packing formula + [Box][Strip][Tablet] buttons; counter billing with barcode/search, FIFO, auto INV, discount, downward round-off; thermal receipt 58/80 + auto print + hold/park cart + cash/credit; role switcher + sales history screen.

3.9.4 **[M-04]** Phase 2 — returns + smart bill lookup + provisional-return alert + float/shift/closing + owner drawing + expenses + below-cost lock.

3.9.5 **[M-05]** Phase 3 — supplier ledger + return credit note + 3-level expiry + 3-level reorder + WhatsApp web order + sample stock.

3.9.6 **[M-06]** Phase 4 — customer/credit limit/loyalty stars + multi-tier rates + split payment + custom fields/categories.

3.9.7 **[M-07]** Phase 5 — black-box audit + multi-dimensional search/rack number + breakage + analytics/P&L + online-offline sync + backup.

3.9.8 **[M-08]** **Rule:** the E and S principles apply **from day one** — not later.

### 3.10 Owner decisions (Q) — already answered, never ask again

| No. | Question | Decision |
|---|---|---|
| 3.10.1 **[Q-01]** | Shop name/address/phone | From app Settings (default "Area11 Pharmacy") |
| 3.10.2 **[Q-02]** | Cloud sync | **Supabase** (free account; Phase 5) |
| 3.10.3 **[Q-03]** | Numbering | `PINV-001` style for purchases, `INV-0001` for sales (implemented with 4-digit padding, e.g. `PINV-0001`) |
| 3.10.4 **[Q-04]** | Expiry alert | From settings; default 365/180/90 days (blue/yellow/red) |
| 3.10.5 **[Q-05]** | Reorder basis | Per-item minimum quantity |
| 3.10.6 **[Q-06]** | Loyalty stars | Not now — settings ready (can be switched on later) |
| 3.10.7 **[Q-07]** | Language | Everything English (app too) |
| 3.10.8 **[Q-08]** | Discount method | Default **on margin** (can be switched to total retail in settings) |
| 3.10.9 **[Q-09]** | Tax | **Off (0 %)** — switchable by master switch |
| 3.10.10 **[Q-10]** | Printer | **Both 58 mm and 80 mm** (model unknown) |
| 3.10.11 **[Q-11]** | Daily report | **WhatsApp (free)** — Phase 3 |
| 3.10.12 **[Q-12]** | Barcode label printing | Not at start — feature ready |
| 3.10.13 **[Q-13]** | Login | **Staff = PIN code; Owner = password** |
| 3.10.14 **[Q-14]** | Network | Shop **LAN** only |
| 3.10.15 **[Q-15]** | Shops/counters | **One shop, two counters + owner's mobile** (viewing from home) |
| 3.10.16 **[Q-16]** | Customer payments | **Cash + credit** — online/card later |
| 3.10.17 **[Q-17]** | Devices at one time | **Two counters + owner's mobile** |
| 3.10.18 **[Q-18]** | Roles first | **Only Owner first** — then Manager/Cashier added (later implemented fully) |
| 3.10.19 **[Q-19]** | Old data | **Import from Excel** (SheetJS) |

3.10.20 **Offline confirmation (A-02):** the owner said **100 % offline is compulsory** — the counter must not stop if the internet goes.

3.10.21 **Decisions taken during the build (chat Q&A, 5 Oct 2026 — do NOT re-ask):**

| Question | Owner's decision | What was built |
|---|---|---|
| What does "room" of a medicine mean? | **Both** — rack/room box **and** camera photo | migration `006_product_room` + photo upload (`U-21`) |
| On what is discount applied? | **Both** — owner chooses in Settings (profit **or** retail) | `discount.mode` = `margin` / `retail` (`U-20`) |
| Where do photos live? | **Files in a folder** (not in the database) | `data/photos/` + photo service (`U-19`) |
| Where are filters wanted? | **All three places** — medicines, customers, bills | filters + sorting in all three lists (`U-22`) |
| What does "enlarge/reduce folders" mean? | **Simple group** — by category/company/room, not a tree | collapsible groups in the product list (`U-22`) |
| What is the next big work? | **Black box (audit) + backup/restore** | audit page + backup service (stage-19) |

3.10.22 Order of work the owner preferred after that: **Phase 3** — supplier ledger + supplier return + WhatsApp order text.

### 3.11 Verification matrix of the numbered rules

3.11.1 The source rules file counts **136** numbered points: U (29 at the time of the matrix; 36 numbers exist U-01…U-36), R 10, E 14, S 9, P 5, B 10, Z 11, T 14, M 8, Q 19. (`rules-count.sh` reported `SAB THEEK`, i.e. no number missing.)

3.11.2 **Principle:** every new owner order → **new number** → added to the rules file (nothing stays verbal).

---

## 4. TECHNOLOGY AND ARCHITECTURE

### 4.1 ORIGINAL stack (reference only)

4.1.1 App: Next.js 15 (App Router) + TypeScript.

4.1.2 UI: Tailwind CSS + Lucide icons (shadcn/ui planned).

4.1.3 Database: SQLite through Node's built-in `node:sqlite` (Node ≥ 22.5; ExperimentalWarning is harmless), WAL mode, foreign keys on, helper functions `query/get/run/scalar/tx`. No ORM (Prisma was tried and rejected because its engine download was blocked). 19 tables initially (`001_core` 18 tables + `002_returns`), growing through migrations to `013`.

4.1.4 Money: integer paisa (`*_paisa` columns).

4.1.5 Auth: Node built-in scrypt for password/PIN hashes; HMAC-signed HttpOnly session cookie.

4.1.6 Excel: SheetJS `xlsx`. ZIP: a small hand-written ZIP engine (`src/lib/zip.ts`, no package).

4.1.7 Cloud: Supabase (mirror/backup only).

4.1.8 Tests: Node's built-in test runner with type stripping (`node --experimental-strip-types --test src/lib/*.test.ts`) — no extra library. 55 tests at stage-43.

4.1.9 Hosting reality: needs a Node ≥ 22.5 host with a persistent disk (the shop PC, or a Node host with a volume). Static/serverless hosts (Netlify, Vercel, GitHub Pages) cannot hold a SQLite file. The Vite legacy demo is what the public Netlify site `marea11.netlify.app` serves.

### 4.2 ORIGINAL runtime facts

4.2.1 Data file: `data/area11.db` (git-ignored; created and migrated automatically on first request; the owner account + 5 categories + settings are seeded by the bootstrap).

4.2.2 Photos: `data/photos/<entity>-<id>-<timestamp>.<ext>`. Backups: `data/backups/`.

4.2.3 First-run: default owner password **`area11`** (from `.env`/seed), default PIN **`1111`** — **must be changed on day one** (Settings → Staff or the reset script). Reset script: `node scripts/reset-login.mjs --list | --owner "new-password" | --user "Name" --pin 1234` (works while the app runs; writes an audit row).

4.2.4 Health check: `GET /api/health` → `{ok:true, database:"connected", version}`; the version comes from `package.json`.

4.2.5 Dev commands: `npm run dev` · `npm run build && npm start` · `npm test` · `npx tsc --noEmit` · `bash scripts/rules-count.sh` · `bash scripts/ckpt.sh save|list|go N|undo|push`.

4.2.6 Other devices on the shop Wi-Fi opened `http://<PC-IP>:3000`.

### 4.3 TARGET decisions for the rebuild (agreed in the planning conversation)

4.3.1 **Platform**: a full **native desktop application, not a browser app**. **Windows 10 preferred**; must also run on Windows 11; Linux acceptable but not required. Reason: the web version was slower than wanted and printing had extra delay.

4.3.2 **Technology**: **C# / .NET (current LTS, .NET 10)**, desktop UI with **WPF + MVVM (CommunityToolkit.Mvvm)** and a modern theme library (WPF-UI or MaterialDesignInXaml) so it does not look dated.

4.3.3 **Database**: **SQLite** with `Microsoft.Data.Sqlite` + **Dapper** (raw SQL ported almost line-for-line from the TypeScript; no EF Core). Keep the **same schema and migrations 001–013** so the owner's existing `area11.db` opens unchanged. New migrations only at the end; old ones never edited `[S-06]`.

4.3.4 **Authentication hashing**: PBKDF2 (built into .NET) — old scrypt hashes are not portable, so a first-run owner password reset is provided.

4.3.5 **Printing**: silent direct printing (no dialog) for 58/80 mm thermal printers, cash-drawer and barcode scanner support; **Urdu text on receipts is printed as rendered graphics** (thermal printers corrupt Urdu text).

4.3.6 **Excel**: ClosedXML. **Backup**: `VACUUM INTO` + `System.IO.Compression` zip including photos.

4.3.7 **Android**: **not now.** The owner wants to be able to add it later (possibly a separate Kotlin app). Keep the Core library UI-free and the data model sync-friendly (stable integer ids, `created_at`/`updated_at` on every table).

4.3.8 **Counters**: the shop starts with **one counter computer**; a **second counter sharing the same data is wanted later** (answered "now one, later two").

4.3.9 **UI language English; Urdu strings in a resource file** `[Q-07]`.

### 4.4 Architecture requirements for the rebuild

4.4.1 Solution layout: `Area11.Core` (class library, **no UI references**: business logic, SQLite, settings, auth, audit), `Area11.Desktop` (WPF), `Area11.Tests` (xUnit). Optional later: `Area11.Server` (ASP.NET Core minimal API, same Core + same SQLite file) for the second counter and Android.

4.4.2 **All data access goes only through Core service interfaces** (e.g. `IProductService`, `ISaleService`, `ICashService`). No SQL, no connection objects, no file access inside ViewModels or XAML code-behind.

4.4.3 Desktop depends only on those interfaces (dependency injection). Today the implementations call SQLite directly. Later a second implementation (HttpClient) will talk to `Area11.Server`. Swapping must need **zero UI changes**.

4.4.4 Every service method takes and returns plain DTOs (serializable; no DB rows, no UI types) and the current user/role.

4.4.5 Business-critical operations (sale, stock deduction, returns, shift close) are **single transactional service calls**, so two counters can never double-sell the same batch.

4.4.6 Stable integer ids and `created_at`/`updated_at` on every table; SQLite runs in WAL mode.

4.4.7 Do **not** build the server now. Prove the design with an **architecture test**: `Area11.Desktop` must not reference `Microsoft.Data.Sqlite`.

4.4.8 Role enforcement is in the Core services, not only in the UI (the original enforced roles on every API route and on every page).

4.4.9 Idle auto-lock, session lifetime and login throttling are application services (Section 7).

### 4.5 Quality gates for every module of the rebuild

4.5.1 Zero build warnings; all tests green; run the app and look; commit; tag `net-stage-N`; update `dotnet/HANDOFF.md` (done / remaining / gotchas) — see 34.4.
---

## 5. DATA MODEL AND GLOBAL CONVENTIONS

### 5.1 Global conventions (REQUIRED)

5.1.1 **Money = integer paisa.** Every money column is named `*_paisa` and holds an integer (Rs 1 = 100 paisa). Never floating point. Users type rupees; the application converts. Rates in the Excel template are typed in rupees.

5.1.2 **Quantities = base units.** Stock is always stored in the smallest unit (tablet/capsule/ml/piece) as `qty_base` `[Spec §4.1.1]`. Boxes and strips are only a way of typing/displaying quantity.

5.1.3 **Dates and times** are local shop time (Asia/Karachi), stored as local text (`datetime('now','localtime')` in SQLite). "Today" = `date(date) = date('now','localtime')`. Expiry is stored as year-month (`YYYY-MM`, e.g. `2027-03`).

5.1.4 **Transactions**: every multi-row write runs inside one transaction (`tx()`); all-or-nothing (also for Excel import and for sync apply).

5.1.5 **Never delete data**: use soft delete (`active = 0`) or reversing rows. Original bills are never edited; returns/voids create new records. The audit log is never deleted (no `DELETE FROM audit_logs` anywhere).

5.1.6 **Migrations are append-only.** New database changes = a new migration added at the end of the migration list; old migrations are never edited. Each migration runs inside `BEGIN IMMEDIATE` and the `_migrations` check is repeated after the lock is taken (an earlier build crashed with `duplicate column name` when several workers opened a new database at once).

5.1.7 **Database pragmas**: WAL mode, foreign keys ON.

5.1.8 **Ids**: integer primary keys; also `created_at`/`updated_at` (needed by sync and the future server).

5.1.9 **Payments sign convention**: in the `payments` table, rows that carry a `purchase_id` or `supplier_id` are money **out**; all others are money **in**; refunds are **negative** rows. The day summary relies on this.

5.1.10 **Supplier balance convention**: positive = the shop owes the supplier; zero = nothing owed; negative = the supplier owes the shop (credit note / advance).

5.1.11 **Customer balance** (khata): positive = the customer owes the shop (udhaar). It changes only through bills, payments and returns — never by hand.

5.1.12 **Bill numbers** come from a numbering service `takeNextCode(kind)` using a prefix setting + a counter setting + zero padding (default 4 digits). Kinds: sale `INV-`, purchase `PINV-`, return `RET-`, quarantine `Q-`, provisional return `PR-`, supplier return `SR-`, stock-take `ST-`. Counters are advanced by the app only; **never edit them by hand** (duplicates would result).

5.1.13 **Row objects** returned to the UI must be plain data (original bug: rows with a null prototype could not be passed to client components).

### 5.2 Migration ledger (known)

| Migration | Content |
|---|---|
| 001_core | 18 core tables |
| 002_returns | sale returns (19th table) |
| 003 | `sales.change_paisa` (cash-tendered change); `stock_adjustments` |
| 004_provisional_returns | rush-time (no-bill) returns |
| 005 | not described in the documents |
| 006_product_room | `room` (almari) on products |
| 007_supplier_returns | supplier returns |
| 008, 009 | not described individually (they carry, among others: shift handover columns `handed_to_user_id`, `handed_to_name`, `opened_from_shift_id`; custom fields; stock-take; split-payment support; loyalty) |
| 010_audit_old_new | `audit_logs.module`, `old_value`, `new_value` |
| 011_alert_dismissals | table `alert_dismissals` |
| 012_return_number | `sale_returns.code` + index |
| 013_quarantine_tracking | `sale_returns.qcode`, `disposition`, `disposed_at`, `disposed_by`, `disposed_note` + indexes |

5.2.1 The rebuilt schema must be a **superset** of all of the above so that an existing `area11.db` opens unchanged.

### 5.3 Logical entities and their meaning

> Names marked [orig] are known from the documents; other names are indicative. What matters is the stored information.

5.3.1 **settings** [orig] — key/value store (values are JSON, type preserved: array/object/number/boolean/string). Holds every editable option, numbering counters, and the hidden session secret.

5.3.2 **users** [orig concept] — name, role (owner/manager/cashier), password hash (owner) or PIN hash (staff), active flag. Default owner seeded at first start.

5.3.3 **categories** — name, optional parent (max 2 levels), active. Five categories are seeded at first start (the specification's examples: Medicine, Surgical, Cosmetics, General, Baby Care).

5.3.4 **companies** — name, active.

5.3.5 **products** — name, generic/salt/formula, brand, barcode, category, company, room, rack, reorder level (minimum quantity), photo file name, `boxStrips`, `stripTablets`, cost, retail, VIP and doctor rates, active, custom-field values.

5.3.6 **batches** — product, batch number, expiry (`YYYY-MM`), `qty_base`, cost, retail, `is_sample` flag, active. One product has many batches with different cost/retail/expiry/stock `[Spec §5.1]`.

5.3.7 **suppliers** — name, agency, phone, balance, notes, custom fields.

5.3.8 **purchases** + lines — code `PINV-nnnn`, supplier, date, total, lines (product, unit, qty, batch no, expiry, cost, retail, free flag), payment.

5.3.9 **customers** — name, phone, category (normal / vip / doctor), credit limit, balance, loyalty points, notes, photo, custom fields.

5.3.10 **sales** + lines — code `INV-nnnn`, customer, cashier, date, subtotal, discount, round-off, tax, total, paid, due, `tendered_paisa`, `change_paisa`, payment method (cash / credit / online / card / split / …), status (paid / partial / due / returned / void), lines (product, batch, unit, qty, unit price, line total, cost).

5.3.11 **payments** — sale/purchase/supplier/customer link, method, signed amount, note, date; includes `loyalty` method rows (negative) and cash-sale rows, customer collections, supplier payments, refunds.

5.3.12 **stock_movements** — product, batch, type (purchase, sale, return_in, supplier_return, writeoff, adjust, sample, import, …), signed `qty_base`, note, user, time. This is the full stock ledger of a product.

5.3.13 **stock_adjustments** — direction (in/out), type/reason (expired, damaged, lost, count correction, `count_short`, `count_extra`), note, value at cost.

5.3.14 **sale_returns** — `code` (`RET-nnnn`), sale, item, qty, refund, reason, `restock` flag, `qcode` (`Q-nnnn`, only for goods not restocked), `disposition` (`quarantine` | `restocked` | `expired` | `sold`), disposed time/by/note.

5.3.15 **provisional returns** — `PR-nnnn`, customer/phone, items, refund, status (`pending` | `linked` | cancelled), linked sale, cancel reason.

5.3.16 **supplier_returns** — `SR-nnnn`, supplier, lines (batch, qty), value, cash-received flag.

5.3.17 **shifts** — opening float, opened/closed time, expected cash, actual (counted) cash, difference, handed-to user id/name, `opened_from_shift_id`.

5.3.18 **expenses**, **owner drawings** — amount, category/note, date, user.

5.3.19 **stock-take sessions** + lines — `ST-nnnn`, status (open / applied / cancelled), scope (all or one room/category), per active batch: book qty, counted qty, difference, value.

5.3.20 **audit_logs** [orig] — time, user id + name, action, module, entity, entity id, `old_value`, `new_value`, details, IP address.

5.3.21 **alert_dismissals** [orig] — alert key, reason, who, when, active/restored.

5.3.22 **custom_fields** — entity (customer / product / supplier), label, type (text, number, date, list/select with options, yes/no), required, active, order; values stored per record.

5.3.23 **_migrations** [orig] — applied migration names.

5.3.24 **Hold carts** (parked bills) — originally kept in browser `localStorage`; in the rebuild they must be stored locally (database or local file) so they survive restarts.

---

## 6. SETTINGS CATALOGUE

6.1 Settings are stored in the database `[S-07]`, edited from the Settings screen (every section opens on click, `<details>`-style `[U-17]`), saved and audited (action `settings_change`). At stage-43 there were **70** setting keys and **every one is read by real code** (3 unused keys were removed in stage-42: `reorder.levels`, `ui.language`, `sync.provider`). A setting must never exist if no code reads it.

6.2 The documented keys are listed below with meaning. (Any key that a feature in this document needs but that is not listed must be created, with a default, in the same spirit — nothing hard-coded `[E-01]`.)

### 6.3 Store and branding

| Key | Meaning | Where it appears |
|---|---|---|
| `store.name` (default "Area11 Pharmacy") | Shop name | receipt, reports, WhatsApp text |
| `store.address`, `store.phone` | Address, phone | receipt header/footer |
| `store.footerNote` | Sentence at the end of the receipt | receipt |
| `store.terms` | Terms (warranty, return policy) | receipt |
| `brand.appName`, `brand.shortName` | App name / short name | title, mobile icon |
| `brand.logoDataUrl` | Logo image | login and receipt |
| `brand.primaryColor` | Main colour (default emerald `#047857` theme colour) | whole app |
| `owner.name` | Owner's name | receipts/reports |

### 6.4 Billing and numbering

| Key | Meaning |
|---|---|
| `bill.salePrefix` (`INV-`) | Sale bill prefix |
| `bill.purchasePrefix` (`PINV-`) | Purchase bill prefix |
| `bill.numberPadding` (4) | Digits in the number (`0001`) |
| `bill.roundMode` (`down10` / `none`) | Round bill down to a multiple of `roundTo`, or not at all |
| `bill.roundTo` (10) | Rounding unit in rupees |
| `bill.nextSaleNo`, `bill.nextPurchaseNo`, `bill.nextReturnNo`, `bill.nextQuarantineNo` (+ equivalent counters for provisional, supplier-return and stock-take codes) | Next numbers — the app advances them; **do not edit by hand**; locked against bulk Excel update |
| `bill.returnPrefix` (`RET-`) | Return code prefix |
| `bill.quarantinePrefix` (`Q-`) | Quarantine code prefix |
| `bill.provisionalPrefix` (`PR-`) | No-bill return prefix |
| `bill.supplierReturnPrefix` (`SR-`) | Supplier return prefix |
| `bill.stockTakePrefix` (`ST-`) | Stock-take prefix |

6.4.1 (Defect found in the original: the last three prefixes existed in the database but were invisible in Settings; they were made visible in the second Spec 1–4 round. They must be visible and editable.)

### 6.5 Tax

| Key | Meaning |
|---|---|
| `tax.enabled` (default **off**) | Master switch |
| `tax.percent` | Percentage (whatever is typed, e.g. 1 or 17) |
| `tax.label` | Name printed on the receipt (e.g. "Sales Tax") |

6.5.1 Effect when on: tax is added to every bill after the sub-total, printed as a separate line, and included in reports. There is **no separate monthly tax report** yet (see 33.4).

### 6.6 Discount

| Key | Meaning |
|---|---|
| `discount.enabled` | Discount on/off |
| `discount.mode` (`margin` default / `retail`) | The limit applies to profit margin or to the retail price `[U-20] [Q-08]` |
| `discount.maxPercentCashier` (5) | Cashier maximum % |
| `discount.maxPercentManager` (20) | Manager maximum % (owner unlimited) |
| `discount.blockBelowCost` (on) | Nobody may discount below purchase cost `[Spec §7.3.1]` |

### 6.7 Expiry, reorder, loyalty

| Key | Meaning |
|---|---|
| `expiry.levels` | 3 levels, each with **days, colour, name** (default 365 blue / 180 yellow / 90 red) |
| `reorder.enabled` | Low-stock warning on/off |
| `loyalty.enabled` (default off) | Points on/off |
| `loyalty.rupeesPerPoint` | Rupees of purchase per 1 point earned |
| `loyalty.vipThreshold` | Points at which a customer becomes VIP |

### 6.8 Printer and receipt

| Key | Meaning |
|---|---|
| `printer.width` (`58` / `80` / `both`) | Roll width |
| `printer.autoCut`, `printer.copies` | Auto cut; number of copies |
| `receipt.datePosition` (top/bottom) | Where date/time is printed |
| `receipt.showOriginalPrice`, `receipt.showDiscount`, `receipt.showSavings` | Show price before discount / the discount / total savings |
| `receipt.showCostColumns` | Cost columns — **owner only** |

### 6.9 Payments and credit

| Key | Meaning |
|---|---|
| `payment.methods` (array: cash / credit / online / card) | Which methods are offered (default cash + credit `[Q-16]`) |
| `payment.default` | Method pre-selected at the counter |
| `credit.blockOverLimit` | When ON, a credit sale over the customer's limit **cannot be made** (instead of only a warning) |
| `credit.managerCanOverride` | Manager may exceed the limit (owner always may) |

### 6.10 Security

| Key | Meaning |
|---|---|
| `security.pinLength` (4) | Staff PIN length |
| `security.autoLockMinutes` (15) | Auto-lock after idle |
| `security.sessionHours` (12) | Session lifetime |
| `security.requireLogin` | Login always required |
| `security.sessionSecret` | Hidden — never shown in Settings; auto-generated; may be overridden by environment `SESSION_SECRET`; locked against bulk update |

### 6.11 Stock and returns

| Key | Meaning |
|---|---|
| `stock.allowNegative` (default **ON**) | ON = sales continue even if stock is 0/low (only the big red alert); OFF = a sale that exceeds stock is refused with an Urdu/English message |
| `stock.warnNegative` | Show the big alert for negative stock |
| `returns.requireInvoice` (ON) | A return without the original bill number is not allowed |
| `returns.maxDays` (0 = no limit) | Return window in days (e.g. 7) |

### 6.12 Backup and sync

| Key | Meaning |
|---|---|
| `backup.autoEnabled`, `backup.everyHours` (24), `backup.keep` (7) | Automatic daily backup, interval, how many to keep (older ones are removed automatically) |
| `sync.enabled`, `sync.url`, `sync.key`, `sync.table` (`area11_sync`), `sync.shopId` (e.g. `area11-main`) | Supabase connection |
| `sync.status`, `sync.lastSyncAt` | Written by the app |

6.12.1 Security note from the original: `sync.key` is stored in plain text and shown in a password box; the database file must never be given to anybody; consider protecting it in the rebuild (e.g. Windows DPAPI).

### 6.13 Proposed additions for the rebuild (not in the original)

6.13.1 `returns.cashierRestockToShelf` (default ON, following U-32): when OFF, a return handled by a cashier is held in quarantine (the old stage-16 rule) until an owner/manager decides. See open point 35.1.

6.13.2 Permission toggles for Section 7.4 (so that `[E-11]` — roles and permissions editable from settings — is met).

---

## 7. USERS, AUTHENTICATION AND ROLES

### 7.1 Accounts

7.1.1 Roles: **Owner**, **Manager**, **Cashier**.

7.1.2 **Owner logs in with a password; staff log in with name + a 4-digit PIN** `[Q-13]` (length from `security.pinLength`). Staff accounts are created by the owner only (Settings → Staff).

7.1.3 Passwords/PINs are stored as salted hashes (original: scrypt; rebuild: PBKDF2). They are **never written to the audit log** — the log only says "changed: yes/no".

7.1.4 A first-run default owner exists (password `area11`, PIN `1111`); the owner must change it on day one. A reset tool must exist for a forgotten password and must itself be audited.

### 7.2 Login behavior

7.2.1 Login, failed login and logout are audited with IP (`login`, `login_failed`, `logout`).

7.2.2 Failed-attempt throttle: after 5 failed tries the same source is blocked for a short time (original: kept in memory, cleared when the server restarts).

7.2.3 Session: signed token (original: HMAC-signed HttpOnly cookie; secret from `.env` `SESSION_SECRET` or auto-generated in the database), lifetime `security.sessionHours` (12), **idle auto-lock** after `security.autoLockMinutes` (15). In the desktop rebuild: an in-memory session with a lock screen.

7.2.4 Session token behavior covered by tests: signing, verification, expiry, tampering rejected.

7.2.5 Role limits are checked **server-side/in the service layer on every operation** (`guard()` on every API route in the original) — hiding a menu item is never enough.

7.2.6 **URL-level page guard**: an owner/manager-only screen opened directly by a cashier is redirected to the counter (`/pos`); the matching API returns 403.

7.2.7 A visible quick role switcher for many people sharing one counter is a small nicety that was not built (users log out and in). A per-user "shift" report is also not built.

### 7.3 Phase-1 role switcher (Spec §12.2)

7.3.1 The specification asked for a temporary drop-down in Phase 1 to switch between Owner/Manager/Cashier for testing. It was **superseded by real login** (stage-9). Do not ship a switcher in production; a developer-only switch for testing is acceptable.

### 7.4 Permission matrix (as implemented; items marked ◇ differ from the specification — see Section 35)

| Capability | Owner | Manager | Cashier |
|---|---|---|---|
| Counter billing, hold cart, customers at counter | ✔ | ✔ | ✔ |
| Discount limit | unlimited | 20 % (setting) | 5 % (setting) |
| Sell below purchase cost | ✘ | ✘ | ✘ (`discount.blockBelowCost`) |
| Credit sale over the customer's limit | always allowed | allowed if `credit.managerCanOverride` | blocked when `credit.blockOverLimit` (cash bills still go through) |
| Sales history (search, view) | ✔ | ✔ | ✔ |
| Return (against original bill) | ✔ | ✔ | ✔ (restock rule: open point 35.1) |
| Create a no-bill (rush) return `PR-` | ✔ | ✔ | ✔ |
| Link / cancel a no-bill return to a bill | ✔ | ✔ | ✘ ("owner or manager only") |
| Put returned goods into stock / quarantine decisions | ✔ | ✔ | restricted (35.1) |
| Void a whole bill (only if it has no returns) | ✔ | ✔ (role not stated in docs) | not stated |
| Create/edit products, purchases, suppliers, supplier payments | ✔ | ✔ | ✘ (403) |
| Categories / companies edit & delete | ✔ | ✔ | ✘ (403) |
| Manage custom fields | ✔ | ✔ | ✘ (403) |
| Stock write-off / adjust (`/stock`) | ✔ | ✔ | ✘ |
| Supplier returns `SR-` | ✔ | ✔ | ✘ (403) |
| Stock-take: open/count | ✔ | ✔ | ✘ |
| Stock-take: **apply** | ✔ | ✘ | ✘ |
| Cash page, shifts open/close/handover, expenses, owner drawings | ✔ | ✔ | ✘ (page redirects to counter) |
| Reports | ✔ | ✔ | ✘ (page redirects, API 403) |
| Excel import/export/bulk update | ✔ | ✔ | ✘ (403) |
| Cloud sync page/API | ✔ | ✔ | ✘ |
| Audit / black-box page ◇ | ✔ | ✔ (original) | ✘ — Spec §13.3 says **owner only** |
| Backup download / restore | ✔ | ✘ | ✘ |
| Dismiss an alert (reason compulsory) | ✔ | ✘ | ✘ — others only see the alert |
| Receipt cost columns | ✔ | ✘ | ✘ |
| Settings | ✔ | partly (original redirected cashier only) | ✘ |
| Settings → Staff (PIN management) | ✔ | ✘ | ✘ |
| Real total profit & loss, user passwords ◇ | ✔ | Spec: hidden from manager | ✘ |

7.4.1 Manager powers named by the specification `[Spec §12.1.2]`: change item price, set credit limit, clear pending bills, view operational reports; net profit and the secret audit are hidden from him.

7.4.2 Cashier powers named by the specification `[Spec §12.1.3]`: only make bills, hold cart, discount within the fixed limit.

---

## 8. CATALOG: PRODUCTS, CATEGORIES, COMPANIES, CUSTOM FIELDS

### 8.1 Products (`/products`, `/products/[id]`)

8.1.1 **Product fields**: name (required), generic name / salt / formula, brand, barcode, category, company, **room / almari** `[U-21]`, **rack / shelf number** (e.g. "Rack B-12") `[Spec §11.1.4]`, **minimum (reorder) quantity**, photo (file or camera), pack formula, rates, custom fields `[U-17]`.

8.1.2 **Pack formula** `[Spec §4.2]`: configured once per product: `1 Box = X Strips = Y Tablets` (e.g. 1 Box = 2 Strips = 20 Tablets, or 1 Box = 10 Strips = 100 Tablets; stored as `boxStrips` and `stripTablets`). The base unit is always the smallest (tablet/capsule/ml/piece). Entering "5 Boxes" in a purchase automatically adds 100 tablets (when 1 Box = 20 tablets). Non-tablet products (syrups, creams) use the base unit as "piece/ml".

8.1.3 **Rates**: purchase cost; **normal retail**; **VIP rate**; **Doctor/institutional rate** `[Spec §7.1]`. Typed in rupees, stored as paisa. Rates are also kept per batch (see 10.1) so an old batch keeps its own cost.

8.1.4 **Create / edit**:
 - 8.1.4.1 A partial edit must **merge** with the existing record. (Original defect: a partial update zeroed purchase cost, box/strip formula, rack and reorder level when only the retail rate was sent. Required behavior: only the sent fields change.)
 - 8.1.4.2 Required custom fields are validated **before** the write.
 - 8.1.4.3 Price changes are audited (old → new) `[Spec §13.2]`.
 - 8.1.4.4 Delete = soft delete; history stays.

8.1.5 **Search** `[Spec §11.1] [T-07]`: one search box that finds by brand/name, **salt/formula (generic search)**, barcode, rack/room, category, company; tolerant of misspelling (Fuse.js in the original). Spec also asks for search by **pack size** (10 ml, 20 ml, 50 ml) — not evident in the original, required in the rebuild. A product may have **different barcodes for different pack sizes** `[Spec §8.4.2]` (the original stores one barcode field; the rebuild should allow several).

8.1.6 **Filters and sorting `[U-22]`**: category (including all its sub-categories), company, room, low stock, out of stock, expiring; six sort orders. **Groups**: by category / company / room — group headers can be clicked to fold/unfold.

8.1.7 **Product detail page** `[U-17]`: photo (file/camera), rates and margin, every batch with expiry and live stock, sales numbers, **price history**, and the **complete stock ledger** (what came in and went out, when, why) `[Spec §10.5]`.

8.1.8 **"＋ New" inside the form** `[U-18]`: a new category or company can be created without leaving the product form.

8.1.9 Product lifecycle history required by Spec §10.5: purchase history (when, from which supplier, which batch, which rate) and sales history (when, to which customer, at which rate).

### 8.2 Categories

8.2.1 Unlimited categories **and sub-categories** `[Spec §2.1]` — original supports two levels (e.g. `Medicines › Pain/Fever`). Choosing a parent in a filter includes all children.

8.2.2 Create, rename, delete; **deleting only unlinks products, never deletes them**; old records remain safe `[E-14]`.

8.2.3 Seeded at first start: five categories (Medicine, Surgical, Cosmetics, General, Baby Care).

### 8.3 Companies and representatives

8.3.1 Company create / rename / delete (delete unlinks only).

8.3.2 Clicking a company shows **all its medicines** `[Spec §10.3.1]`.

8.3.3 Spec §10.3.2 also asks for the **medical representative / salesman**: name, phone number, **visit day** — not evident in the original; required in the rebuild.

### 8.4 Custom fields (user-defined fields) `[Spec §2.2] [E-05] [S-04]`

8.4.1 The owner can add or remove a field on the **customer, product and supplier** forms (from Settings; quick-add suggestions such as CNIC, Shelf, Salt, Discount group).

8.4.2 Field types: original — text, number, date, list (select with options), yes/no. Specification — text, number, **fixed amount/currency**, **photo**. The rebuild supports all of these.

8.4.3 Each field can be **required or optional** and switched on/off; switching off keeps the stored values. The new field appears in the form automatically.

8.4.4 Required fields are validated server-side before saving. Only owner/manager may manage fields.

---

## 9. SUPPLIERS AND PURCHASES

### 9.1 Suppliers (`/suppliers`, `/suppliers/[id]`) `[Spec §10.2]`

9.1.1 Fields: name, agency, phone, outstanding balance, notes, custom fields; inline edit.

9.1.2 **Supplier ledger page**: every purchase bill, every payment and every return in one place; the **pay supplier** button is on the same screen.

9.1.3 Paying more than is owed is **allowed** (rule U-30: only an alert). The message reads, e.g.: "This supplier had no outstanding balance, yet Rs 2000 was paid — this is an **advance (credit)**." It is also written into the black box with `overpaid: true`.

9.1.4 **What was bought and at what rate** is visible per supplier and per purchase bill `[Spec §10.2.2]`.

### 9.2 Purchases / stock-in (`/purchases`) `[Spec §3]`

9.2.1 **Auto code**: every delivery gets the next purchase bill number `PINV-0001, PINV-0002…` `[Spec §3.1.1]`. From this number the delivery can later be reviewed: which products, which batch, which rate `[Spec §3.1.2 consignment tracking]`.

9.2.2 **Entry steps** `[Spec §3.2]`: choose the supplier from a drop-down or create a new one on the spot; search the product, or if it is new register it right on this screen; enter quantity (box / strip / base units), **batch number (compulsory)**, **expiry**, **purchase cost** and **retail price**. On finalizing, the amount goes to the supplier's account automatically.

9.2.3 Quantity entry in boxes/strips is multiplied by the pack formula into base units automatically (5 boxes → 100 tablets for 1 box = 20).

9.2.4 **Free / sample / bonus line** `[Spec §11.3]`: the goods enter stock at **cost Rs 0**, but are **not** added to the bill total or to what is owed to the supplier (the company's free medicine). Free stock has its own table on the alerts page.

9.2.5 Each purchase creates batches and stock movements (`purchase`), updates the supplier balance, and is audited.

9.2.6 A printable purchase receipt exists (`receipt/[id]?type=purchase`).

9.2.7 Only owner/manager can create purchases.

### 9.3 Supplier returns `SR-nnnn` `[Spec §10.2.3, §15.2.1]`

9.3.1 Reasons: expired, damaged, wrong goods. Each line names the batch (or falls back to FEFO). The stock leaves, and what the shop owes is reduced (or a **credit note** is created that is deducted from the next invoice).

9.3.2 **Rule U-30**: if the quantity returned exceeds the stock in the batch, the return **still completes**; stock goes negative and the response carries a warning ("stock MINUS (-10) gone"). (Original defect: "Batch has only N" stopped the work.)

9.3.3 Expired batches ticked on the alerts page can be **sent back to the supplier in one click**.

9.3.4 Optional cash received from the supplier is recorded.

9.3.5 Only owner/manager (cashier: 403).

9.3.6 Spec §15.2.1 "vendor return cutoff window" — an alert **3 to 6 months before expiry** that this is the last time to return the goods — is **not evident** in the original; required.

---

## 10. BATCHES, STOCK AND EXPIRY

### 10.1 Batches `[Spec §5.1]`

10.1.1 Batch number is compulsory on every purchase.

10.1.2 One product may have several batches at the same time with different purchase price, selling price, expiry and stock.

10.1.3 The live stock of each batch is visible separately.

### 10.2 FEFO / FIFO selection `[Spec §5.2]`

10.2.1 In search/billing the batch with the **nearest expiry is shown first and is auto-picked** (FEFO — first-expiry-first-out).

10.2.2 Typing a name shows a pop-up with all current batches, expiry, price and stock; the cashier can click a particular batch.

10.2.3 A sale line without an explicit batch automatically takes the nearest-expiry batch, so batch stock and the movement ledger cannot drift apart (defect fixed at stage-16).

10.2.4 **Expired batches cannot be sold** at the counter `[Spec §6.2.2]`; they are available only for supplier return or write-off (and, under U-34, from quarantine "if wanted").

10.2.5 If no batch has enough stock and `stock.allowNegative` is ON, the **last batch** is cut so that stock really goes negative `[U-26]`; book and reality never silently differ.

### 10.3 Stock movement ledger

10.3.1 Every change of stock writes a `stock_movements` row (signed quantity, batch, type, user, time, note). The ledger lets the product detail page show "what came and went".

10.3.2 Movement types in use: purchase, sale, return (restock), supplier return, write-off, adjust (stock-take / count correction), sample, import (opening stock), quarantine disposition.

### 10.4 Expiry management `[Spec §6]`

10.4.1 **Three levels** with owner-set **days and colour** `[Spec §6.1] [Q-04]` (default 365 blue, 180 yellow, 90 red; names editable) plus a **fourth box: already expired**.

10.4.2 Status labels per batch: OK / expiring soon (level) / expired / empty.

10.4.3 `/alerts` shows four sections: level 1, level 2, level 3, expired `[Spec §6.2.1]`. Expired value is shown at cost.

10.4.4 Expired goods are isolated from sale and offered only for supplier return / write-off `[Spec §6.2.2]`.

### 10.5 Reorder `[Spec §11.2]`

10.5.1 Each product has a **minimum quantity** `[Q-05]`. The reorder list shows products at or below it.

10.5.2 Specification asks for **three levels**: level 1 warning (running low), level 2 critical / order now, level 3 red alert / out of stock. The original implementation shows low-stock and out-of-stock lists (the `reorder.levels` multiplier setting was removed as unused at stage-42). The rebuild should show the three tiers with distinct colours.

10.5.3 The reorder list can be filtered **supplier-wise** `[Spec §11.2.4]` (not evident in the original) and becomes a WhatsApp **order pad** (see 20.3).

---

## 11. PRICING, DISCOUNT AND ROUNDING

### 11.1 Rate tiers `[Spec §7.1]`

11.1.1 Normal retail rate; **VIP rate**; **Doctor / institutional rate** (clinics, hospitals, doctors).

11.1.2 Choosing a customer applies the matching rate **automatically** (customer category → rate). A toast says "VIP/DOCTOR rate applied — Rs 20" `[U-29]`.

### 11.2 Discount engine `[Spec §7.2] [U-20]`

11.2.1 **Margin method**: the percentage applies **only to the profit margin**. Example: cost Rs 200, retail Rs 300 (margin 100); 10 % discount = Rs 10; final price Rs 290.

11.2.2 **Retail method**: the traditional discount on the retail price or the whole bill.

11.2.3 The owner chooses the method in Settings (`discount.mode`); both are built.

11.2.4 Discount may be typed in rupees or as a percentage.

11.2.5 **Role limits**: cashier 5 %, manager 20 %, owner unlimited (editable). **The limits are enforced on the server side** — no UI can exceed them. Percent limits measure against the margin or the retail value according to `discount.mode`.

11.2.6 **Hard lock**: no discount may take the price below purchase cost (`discount.blockBelowCost`) — checked per line and per bill `[Spec §7.3.1]`.

### 11.3 Downward round-off `[Spec §8.2] [Z-03] [Z-11]`

11.3.1 `bill.roundMode = down10`: the last 1–9 rupees of the bill are cut — always in the customer's favour, never up: 545 → 540; 556 → 550. `none` disables it. The unit is `bill.roundTo` (10).

11.3.2 **Cost guard** (function `applyRoundWithCostGuard`): if rounding down would take the bill amount below the purchase cost of its items, rounding is **not** applied. Bill 545 with cost 543 → collect 545. Bill 545 with cost 500 → 540. A warning is shown for a bill below cost.

### 11.4 Reconciling bill and item lines `[defect fix, stage-30s]`

11.4.1 Round-off and loyalty rebate must be spread over the item lines proportionally (the last line absorbs the remainder) so that **bill total = sum of item totals, always** — on the receipt and in every report. (Original defect: a typical bill showed a Rs 7.50 gap between items and total.) Tests: no-adjustment, loyalty only, round-off only, both, margin report.

### 11.5 Tax

11.5.1 Master switch `tax.enabled` (default off, 0 %); when on, `tax.percent` is applied after the sub-total and shown as a separate line labelled `tax.label`; included in reports `[Spec §8.5.3]`.

### 11.6 Entry of amounts

11.6.1 All amounts typed in rupees (decimals allowed), converted to paisa on input and formatted on output.

---

## 12. COUNTER BILLING AND SALES (`/pos`, `/sales`)

### 12.1 Counter screen `[Spec §8, Phase 1 items 4–7]`

12.1.1 **Search + barcode**: one search box; a USB barcode gun simply types into it `[R-09]`; the FEFO batch is on top.

12.1.2 **[Box] [Strip] [Tablet/Loose]** one-click quantity buttons on every line `[Spec §4.2.3]`.

12.1.3 **Cart** with per-line batch choice, quantity, unit price, line total; remove a line.

12.1.4 **Customer selection** (optional); selecting a customer applies the rate tier and credit checks.

12.1.5 **Hold / park cart** `[Spec §8.5.1]`: park an unfinished bill, serve another customer, come back. Keyboard shortcuts: `F2` focuses search, `F4` saves/holds.

12.1.6 **Payment area**: methods from settings (default cash / credit), **split pad** (see 13.3), **cash-tendered box with live change calculator** and quick buttons (Exact, 100, 500, 1000, 2000, 5000), change shown on screen and on the receipt.

12.1.7 **Shift reminder**: a small amber strip when no cash shift is open. **Offline banner**: amber strip "internet is off" (billing continues).

12.1.8 **Instant notices `[U-29]`**: toasts when a medicine is added with problems — "stock 0 — sale will go ahead but stock will go MINUS", "only 5 available — this quantity takes stock negative", "VIP/DOCTOR rate applied", discount/round-off applied; a **sticky red alert bar** at the top stays while the problem exists; the line itself shows "⚠ stock will go minus (−7)".

12.1.9 After saving, the server's warnings appear in the big alert at the top (not hidden in small text) and the receipt opens for printing (auto-print).

### 12.2 Sale creation algorithm (REQUIRED order)

12.2.1 Validate lines; find the batch for each line by FEFO unless one was chosen; expired batches refused.

12.2.2 Resolve the price tier from the customer category.

12.2.3 Apply discount and check role limit and below-cost lock (11.2).

12.2.4 Apply tax (if on), then round-off with cost guard (11.3), then reconcile item lines (11.4).

12.2.5 Check customer credit limit if any part is on credit (13.4).

12.2.6 Apply loyalty redemption if used (13.6).

12.2.7 Deduct stock in base units, write movements; if stock is insufficient and `stock.allowNegative` is ON, continue and cut the last batch (stock goes negative), record a `negative_sale` audit event with old → new quantity and return `negativeItems`; if OFF, refuse with a clear message "Settings › Stock to allow".

12.2.8 Create the sale (code `INV-nnnn`), lines, payments, change; update customer balance/points; earn loyalty points (if enabled).

12.2.9 Write audit rows; return the sale code, warnings and `negativeItems`.

12.2.10 All of this happens in **one transaction**.

### 12.3 Sales history (`/sales`) `[Spec §9.2.1]`

12.3.1 Search by **bill number, customer, phone, or medicine name** (also the "approximate date" lookup of the specification).

12.3.2 Filters: payment method, status, profit/loss bills, minimum amount, dates. Expand a bill to see items, payments and **return receipts** (reprint available).

12.3.3 Whole-bill void is allowed only if no returns exist (stock and money are reversed through new records; the original bill is never edited).

12.3.4 Each line of an old bill shows how many more can still be returned and limits the input accordingly.

---

## 13. PAYMENTS, CREDIT AND LOYALTY

### 13.1 Methods

13.1.1 Cash, credit (udhaar), online, card — enabled per `payment.methods`; default cash + credit; the pre-selected one is `payment.default`.

### 13.2 Cash tendered and change

13.2.1 The cashier enters the cash received; the system computes the change. `tendered_paisa` and `change_paisa` are stored on the sale; the receipt prints "Cash received" and "Change returned". The tendered amount is **not** revenue.

### 13.3 Split payments `[Spec §8.5.2]`

13.3.1 A bill can be paid part cash / part credit, or part cash / part online (any mix). One payment row is written per method; the credit part goes to the customer's khata.

13.3.2 If the parts do not add up, the difference is folded into cash. The sale's method is recorded as `split` and its status as partial when something remains due.

### 13.4 Credit (udhaar) and hard limit `[Spec §10.1.2]`

13.4.1 Each customer may have a **credit limit**. At the counter the cashier sees the old balance + this bill = new total.

13.4.2 With `credit.blockOverLimit` ON, a credit sale over the limit is **refused**; otherwise only a warning. Manager may exceed it if `credit.managerCanOverride`; owner always may. Cash bills are never blocked.

### 13.5 Receiving customer payments

13.5.1 A customer's payment lowers the balance and adds cash into the drawer (shift). Full history per customer: every bill, payment and return; searchable by phone. A receipt for collections and a printable monthly customer statement are not built yet (33.4).

### 13.6 Loyalty `[Spec §10.1.4] [Q-06]`

13.6.1 Default OFF. When on: points are **earned** (`loyalty.rupeesPerPoint` rupees per point; the specification wording is 1 point per Rs 100) and **spent** (1 point = Rs 1, capped at the customer's points; recorded as a negative `loyalty` payment row, e.g. 5 points = −500 paisa). Stars 1–5 on the customer page; at `loyalty.vipThreshold` points (spec: 5 stars) the customer becomes **VIP automatically**.

---

## 14. RECEIPTS AND PRINTING `[Spec §8.3, §8.4]`

### 14.1 Receipt types

14.1.1 Sale receipt (`/receipt/[id]`), purchase receipt (`?type=purchase`), return receipt (`?type=return&code=RET-nnnn`), no-bill return receipt (`?type=provisional`), shift handover slip. A sale receipt opens automatically after a sale; a return receipt opens automatically after a return.

### 14.2 Sale receipt content and layout

14.2.1 Shop name, address, phone (header); bill number; date/time (top or bottom per `receipt.datePosition`); cashier; customer; lines (name, unit, qty, price, total); optional columns: price before discount, discount, savings; sub-total, discount, round-off, tax (separate line), total; payment lines; **cash received and change**; footer note; custom terms (guarantee, return policy).

14.2.2 Toggles `[Spec §8.3.3]`: show original price / discounted price / only final total + name + quantity. Cost columns are shown **only to the owner**.

### 14.3 Return receipt `[U-35]` (`RET-nnnn`)

14.3.1 Contains: shop name/address, the **permanent return number**, date and time, **original bill number**, customer (name/phone), medicines + quantities + refunded amounts, **for each item "back in stock" or "in quarantine (Q-nnnn)"**, reason, **total refund**, receiver name and signature line.

14.3.2 Printed automatically right after a return; older return receipts can be reprinted from `/sales` inside each bill ("Return receipts"). One return = one number for all its lines.

14.3.3 A no-bill return has its own receipt (`PR-nnnn`) with a "Receipt" button on the provisional card in `/sales`.

### 14.4 Shift handover slip

14.4.1 Lists all lines, the difference, "from whom → to whom", and **both signatures**.

### 14.5 Printing technology

14.5.1 Roll widths **58 mm and 80 mm** (setting; both supported); copies; auto-cut `[Q-10]`.

14.5.2 ORIGINAL: browser print preview (printer driver not yet tested at the shop — **hardware test pending**). TARGET: direct silent printing for thermal printers (ESC/POS), A4 for formal documents; the printer or scanner being absent must not stop the app `[Z-07]`.

14.5.3 **Urdu on receipts** is printed as an image/graphic (thermal printers corrupt Urdu text).

14.5.4 Barcode label printing: not at the start `[Q-12]` — feature prepared only.

---

## 15. RETURNS, QUARANTINE AND NO-BILL RETURNS (`/sales`) `[Spec §9] [U-28] [U-32] [U-33] [U-34] [U-35]`

### 15.1 Validation (strict) — `[U-28]`, setting `returns.requireInvoice`

15.1.1 A return must be tied to an **original bill number** (no bill, or a wrong bill → refused).

15.1.2 An item that is not on that bill is refused: "Item #2 is not on this bill (INV-0001) at all."

15.1.3 **Never more than was sold**, counting returns already made: "this bill had 12 bike, 4 already returned — now only 8 can be returned."

15.1.4 **Never more refund than was actually collected** for that bill.

15.1.5 Optional **window**: `returns.maxDays` (0 = no limit; 7 = bills older than 7 days cannot be returned).

15.1.6 The original bill is never edited; a return is a new record.

### 15.2 Refund computation

15.2.1 Refund = the share of what the customer actually paid for the returned quantity (price after discount, round-off share, tax share). If the bill is on credit, the **credit (khata) is reduced first**; only the rest is paid back in cash. Full and partial returns are supported.

15.2.2 Money and stock correct themselves automatically `[Spec §9.1.1]`.

### 15.3 Return numbering and stock destination

15.3.1 Every return gets a **permanent number `RET-0001, RET-0002…`** (all lines of one return share it), written in the black box; prefix editable (`bill.returnPrefix`).

15.3.2 Return reasons `[Spec §9.3]`: defective, expired, customer changed mind, wrong medicine entered, plus a custom remark.

15.3.3 **Default: goods go into stock the moment money goes out** (`restock = 1`) `[U-32]`; the same specific batch is credited back `[Spec §9.1.1]`. This happens at once; nothing needs to be pressed.

15.3.4 The return stock destination can be set to "not back to stock" deliberately; then the goods go to quarantine (15.4).

15.3.5 The old role rule (stage-16: a cashier's return can never go back into stock; only owner/manager) conflicts with U-32 — see open point 35.1; the proposed switch is `returns.cashierRestockToShelf`.

### 15.4 Quarantine `[U-34]`

15.4.1 Every item that goes to quarantine receives a **permanent, never-changing number `Q-0001, Q-0002…`**.

15.4.2 Fields: `disposition` — `quarantine` (in quarantine) → `restocked` (back to stock) / `expired` (expired-damaged write-off) / `sold` (sold on); with time, note and user.

15.4.3 The owner/manager decides each item with three buttons: **back in stock / damaged-expired / sold**. Only "back in stock" increases stock. The negative-stock rule applies here too.

15.4.4 **As long as goods are in quarantine the alert keeps going** (and the owner can dismiss it with a reason, 16.4).

15.4.5 `/alerts` shows two cards: (1) "Currently in quarantine" with the three buttons and a note; (2) "**Full history of quarantine — which goods went where**" (number, medicine, quantity, outcome, time, note).

15.4.6 Ordinary returns that restock immediately do **not** create a Q-number. The Q-number is also printed on the return receipt.

### 15.5 Rush-time (no-bill) returns `PR-nnnn` `[Spec §9.2.2, §9.2.3] [U-33]`

15.5.1 If the paper bill is lost and the shop is busy, the cashier can refund the money at once and free the customer.

15.5.2 **Smart bill lookup** `[Spec §9.2.1]`: type a medicine name, a customer phone or an approximate date and the system lists all earlier bills.

15.5.3 The goods go **into stock immediately**. A red **pending badge keeps blinking** until the real bill is attached; owner/manager can then **link the real bill** (optionally restocking — it does **not** restock a second time, to avoid a double addition).

15.5.4 Even after the bill is linked **the alert stays** (turns amber: "bill (INV-…) linked, owner's confirmation pending") until the owner writes a reason and dismisses it.

15.5.5 Cancelling a provisional return needs a reason.

### 15.6 Profit effect of returns (defect fix)

15.6.1 The cost of returned goods flows back into profit **only when the goods really returned to stock (`restock = 1`)**; goods in quarantine or damaged are treated as a loss and their cost is not reduced. Reports and cash/day-end must use the same rule. Example: sold 6 for Rs 480 (cost Rs 300), 3 returned (Rs 240 refunded, cost Rs 150 back on shelf): profit **Rs 90** (37.5 % margin), not −60. Formula: `(480−240) − (300−150)`.

---

## 16. NEGATIVE STOCK AND THE ALERT SYSTEM `[U-25] [U-26] [U-30] [U-31] [U-32] [U-33] [U-34] [U-29]`

### 16.1 Principle

16.1.1 **Stock negativity never stops any work** — sale, supplier return, write-off, adjust all continue. Only a warning/alert is produced. (Two counters selling the last item together on a dead network also fall under this `[Spec §1.2.5]`: the system must not crash; it shows stock as minus and raises the alert.)

16.1.2 `stock.allowNegative` can be switched OFF; then a sale exceeding stock is refused with a clear message.

### 16.2 Where alerts are shown

16.2.1 **Global red alert bar at the very top of every page** — not only the counter. Example text: "⚠ Stock is MINUS — 3 medicines · (worst −9) · fix by count or purchase · See now". It can be hidden for the current session only.

16.2.2 `/alerts` has a top card "Stock MINUS" (how many medicines, how much).

16.2.3 `/stock` shows write-off warnings in a big red alert box.

16.2.4 Each cart line shows a red mark; the post-bill alert stays in the top bar.

### 16.3 What is recorded

16.3.1 Every negative sale is stored in the black box as `negative_sale` with "who · when · old value → new value".

### 16.4 Alert dismissal `[U-31]`

16.4.1 Only the **owner** can dismiss an alert; manager/cashier see "Only the owner can end this alert by writing a reason".

16.4.2 **A reason is compulsory** (at least 3 characters); without it the alert does not end.

16.4.3 The reason is stored permanently (table `alert_dismissals`: key, reason, who, when) and logged in the black box (`status: active → dismissed`, `reason: null → "…"`).

16.4.4 The owner may **restore** a dismissed alert ("bring back") — that is logged too. `/alerts` has a card "Alerts the owner ended with a reason" (reason + time + name).

16.4.5 The previous "✕" that hid an alert only for the session and let it come back after login has been replaced by this durable mechanism.

### 16.5 Alert sources (registry)

16.5.1 Negative stock per product; pending / linked no-bill returns (`PR-…`); items in quarantine (`Q-…`); overpayment to a supplier (informational, recorded in the black box). Each has a stable alert key used by the dismissal table.

16.5.2 During a stock-take apply, negative book stock is corrected automatically.

16.5.3 Cart-time toast rules: stock 0 / low / negative; VIP/Doctor rate applied; discount limit; round-off applied.

---

## 17. CUSTOMERS (`/customers`, `/customers/[id]`) `[Spec §10.1]`

17.1 **Fields**: name, phone, category (**Normal / VIP / Doctor** — drives the rate tier), credit limit, balance (khata), loyalty points/stars, notes, **photo (file or camera)**, custom fields (e.g. CNIC). Name and phone may be required or optional by custom-field style setting `[Spec §2.2.3]`.

17.2 **Hard credit limit** `[Spec §10.1.2]`: when the limit is reached credit bills are blocked if `credit.blockOverLimit` is ON (13.4). The cashier sees "old credit + this bill = total".

17.3 **Customer page (click a customer `[U-17]`)**: photo, khata, **every bill, every payment, every return** in full history; searchable by phone; edit inline; collect a payment (reduces udhaar, adds cash to the drawer).

17.4 **Lists**: filters (category, credit, etc.), sorting `[U-22]`.

17.5 **Lifetime profit per customer** `[Spec §10.1.3]` — the profit earned from this customer over all time. Not evident in the original (it shows purchases, collections, refunds, balance); required.

17.6 **Loyalty stars 1–5** `[Spec §10.1.4]`: per Rs 100 points; at 5 stars the customer automatically becomes VIP (built through `loyalty.vipThreshold`; default feature OFF).

17.7 **Prescriber / doctor tracking** `[Spec §10.4]`: record which doctor's prescription a medicine was sold on and how much profit it earned. Not evident in the original; required in the rebuild.

17.8 Monthly customer statement (print/WhatsApp) and a collection receipt: not built yet (33.4).

---

## 18. CASH, SHIFTS AND DAY-END (`/cash`) `[Spec §14]`

### 18.1 Cash shift ("galla") `[Spec §14.1]`

18.1.1 **Open** the drawer with an **opening float** (the small change in the drawer at morning). Only one shift can be open at a time. (Original defect fixed at stage-15: an empty shift was auto-created at first start — removed.)

18.1.2 While open the screen shows the **live Expected cash**: opening float + cash sales actually kept (net of change returned) + customer payments received − refunds paid − supplier payments − expenses − owner drawings, within the shift window.

18.1.3 **Close** by entering the **counted cash**; the system stores expected, counted and the **difference (short/excess)** in a shift history list `[Spec §14.1.3]`.

18.1.4 **No shift open** → a small amber reminder on the counter.

### 18.2 Shift handover `[Spec §14.1.2]` (stage-32)

18.2.1 When closing, state **who will take over**; the counted cash becomes the **next shift's opening float** automatically. The record "from whom → to whom" is kept (`handed_to_user_id`, `handed_to_name`, `opened_from_shift_id`).

18.2.2 A **handover slip** with all lines, the difference and both signatures can be printed.

18.2.3 Mid-shift handover on a change of employee is supported (immediate cash accounting).

### 18.3 Owner's drawing account `[Spec §14.2]`

18.3.1 Cash taken for personal expenses (e.g. bike tuning, household) is a **debit** to the owner's account.

18.3.2 Medicines taken by the owner for his own use also go into the same account.

18.3.3 When the owner puts money back into the counter it is a **credit**.

### 18.4 Shop expenses `[Spec §14.3]`

18.4.1 Daily (tea, cleaning) and monthly (rent, electricity, salaries) expenses are entered so that net profit can be computed.

### 18.5 Day-end summary

18.5.1 Shows sales, credit, returns, profit, cash in/out; profit uses the rule in 15.6 (returned cost counted only when restocked). Day-end figures for the rebuilt app must reconcile with `/reports` for the same day.

18.5.2 A reminder to close the drawer in the evening is a suggested improvement (33.5).

---

## 19. STOCK WRITE-OFF, ADJUSTMENT AND STOCK-TAKE

### 19.1 Write-off / adjust (`/stock`, owner/manager) `[Spec §15.2.2]`

19.1.1 One click to write off goods that are expired, broken/dropped, lost, or to correct a count. Value is computed at **purchase cost** (the real loss) and stored.

19.1.2 Removing more than the available stock is **allowed with a big red warning** `[U-30]`.

### 19.2 Stock-take / physical stock audit (`/stock-take`) `[Spec §15.2.4]` (stage-29)

19.2.1 Open a session (`ST-0001`): the **book stock is frozen**. Scope can be everything or a room/shelf.

19.2.2 For each medicine (each active batch) enter the count; the screen shows **book / counted / difference / value** side by side; "only differences" filter.

19.2.3 **Apply** (owner only): batches corrected, movements recorded (type adjust), shortage/excess valued at cost, and **negative book stock is corrected too**. A medicine found in the count but absent in the books gets a batch `GINTI-ST-xxxx` automatically.

19.2.4 A session can be applied **only once**.

---

## 20. ALERTS PAGE, REORDER AND WHATSAPP (`/alerts`) `[Spec §6.2, §11.2, §15.2]`

20.1 Cards on `/alerts` (top to bottom): negative stock; pending/linked rush returns; quarantine now; quarantine history; alerts dismissed by the owner with reason; expiry levels 1–3 and expired; low stock / reorder list; free/sample stock list.

20.2 **Send expired batches back to the supplier**: tick the batches and create a supplier return in one click.

20.3 **Reorder = WhatsApp order pad** `[Spec §11.2.5] [T-09] [Q-11]`: tick the medicines, set the quantities, type/choose the supplier's phone number and WhatsApp opens with a ready text; free `wa.me` links, no API key. A Pakistani number starting `03xx` is converted to `92 3xx`. With no number the link opens `wa.me/?text=` so the owner can choose a contact.

20.4 **Today's summary** message: one tap sends a WhatsApp summary (bills, sales, cash, credit, profit, expenses) of the day.

20.5 Spec §15.2.3 **Generic alternative suggestion** (when a medicine is out, show alternative brands of the same salt) — not evident in the original; required.

20.6 Spec §15.2.1 **Vendor return cutoff alert** (3–6 months before expiry) — required (see 9.3.6).

---

## 21. REPORTS AND ANALYTICS (`/reports`) `[Spec §15.1]` (owner/manager)

21.1 Period buttons: **Today / 7 days / 30 days / this month**.

21.2 KPI cards: bills, items, sales, discounts, **profit + margin %**, average bill, cash, credit, refunds, expenses, purchases.

21.3 Per-day bar chart; **Top-10 medicines** (by quantity and by profit `[Spec §15.1.1]`); **category split**; **Dead stock** (no sale for 60 days) `[Spec §15.1.2]`.

21.4 **Highest and lowest sales days of all time** `[Spec §15.1.3]` — the per-day chart exists; an explicit all-time best/worst day is not evident; required.

21.5 CSV download; WhatsApp summary; real P&L including expenses and owner drawings for the owner `[Spec §12.1.1]`.

21.6 **Profit formula** (shared module `report-math`): profit = (sales − refunds) − (cost of goods sold − cost of **restocked** returned goods); margin = profit / (sales − refunds) × 100 (Rs 90 of Rs 240 = 37.5 %). Applies to totals, per-day chart, top medicines, category split, and to cash/day-end. Five tests guard it.

21.7 Cashiers are redirected away from this page (API 403).

21.8 A separate monthly **tax report** is not built (33.4).

---

## 22. AUDIT LOG / BLACK BOX (`/audit`) `[Spec §13] [U-27]`

22.1 **No delete, no edit** of the log, for anyone — not even the owner. There is no `DELETE FROM audit_logs` anywhere in the code.

22.2 Every row has: who (name + id), when (date + time), action, entity + id, **module**, **old value → new value**, details, IP.

22.3 **Modules tagged**: Auth, POS, Sales, Returns, Inventory, Purchases, Customers, Suppliers, Stock, Settings, Users, Backup, Sync, Import.

22.4 **Only changed fields** are stored (not the whole record): **new record** → full new value; **delete** → full old value; **edit** → only the changed fields (old → new). (Original defect: new records saved only a name and the diff stored nothing when `before` was empty.)

22.5 **Security events**: login, failed login, logout, and **denied** (attempt without permission). **Failed operations** (e.g. "that much return is not possible") are also logged — to catch theft/mistakes.

22.6 **PIN and password never appear** (only "changed: yes/no"). Viewing a photo is deliberately not logged (noise); changing a photo is logged through product/customer old → new.

22.7 Spec §13.2 minimum events: login/logout, bill created, bill cancelled, return, stock entry, price change — with time stamp and user.

22.8 The viewer: columns incl. "Old → New" (old in red, new in green), filters (action, entity, date, words, **module**), **CSV export** (headers: Time, User, Action, Module, Entity, ID, Old, New, Details, IP — the original Urdu/Roman headers "Waqt, User, Action, Module, Cheez, ID, Purana, Naya, Tafseel, IP" may be translated).

22.9 Access: Spec — owner only; original — owner/manager (see 35.4).

22.10 Example actions logged: `negative_sale`, `return`, `dismiss_alert`, `restore_alert`, `settings_change`, `price_change`, `login`, `login_failed`, `logout`, `denied`, supplier payment with `overpaid`, backup/restore, sync, import.

---

## 23. BACKUP AND RESTORE `[Spec §1.3] [Z-08]`

23.1 **Manual backup in one click** (owner): a single `.zip` containing the **whole database + the `data/photos/` folder**; an option "database only". The old `.db`-only backup did not protect photos — the zip is the recommended backup.

23.2 **Restore** from a `.zip` or `.db`: the file is validated; the current database is saved first as a safety copy; the app switches to the new database **without a restart**.

23.3 **Automatic daily backup** into `data/backups/` (`backup.autoEnabled`, `backup.everyHours`, `backup.keep`: old ones trimmed automatically). Works while the app is running.

23.4 Before any cloud "pull" a copy `area11-before-sync-*.db` is made automatically.

23.5 Spec §1.3.2 weekly scheduled backup — covered by the configurable interval; Spec §1.3.3 **daily auto-send of the backup file/report by e-mail or WhatsApp** — only a manual WhatsApp summary exists; auto-send required; Spec §1.3.4 **archival of years-old data** so the system stays fast — not evident; required.

23.6 Backups are made while the app is running (SQLite `VACUUM INTO`/online copy); an ordinary file copy of `area11.db` is also a valid backup.

---

## 24. EXCEL IMPORT, EXPORT AND BULK UPDATE (`/import`) `[Q-19] [T-10]`

### 24.1 Importing old data (new records)

24.1.1 A **template** is offered (Products, Customers, Suppliers sheets + Instructions). The user fills it from the old list.

24.1.2 **Dry-run preview** first: line by line (OK / skipped / error), then **commit** in one transaction. Duplicate names/phones are skipped automatically ("already exists"). Rates are typed in rupees and stored as paisa.

24.1.3 **Opening stock arrives as a proper batch** with expiry (movement type import); customer **opening udhaar** and supplier **opening payable** are accepted.

### 24.2 Download all data and update everything from one file (bulk update, stage-30)

24.2.1 "Download your data" produces one `.xlsx`: medicines, customers, suppliers, categories, companies, batches, Settings, Instructions — **without photos**.

24.2.2 Edit anything, upload again → a full **before → after preview per row**, then apply with one click.

24.2.3 **Empty cell = no change** (nothing is erased by accident); to clear a value type `__CLEAR__`.

24.2.4 **Account balances and stock quantities are never changed from the file** (accounts must not break) — use payments/returns or stock-take instead.

24.2.5 Security settings (session secret, numbering counters) are locked; Settings are updated only if the user ticks a separate box; Settings types are preserved (arrays/objects/numbers stay what they were).

24.2.6 Uploading the same file twice yields 0 changes (idempotent).

24.2.7 Owner/manager only; cashier gets 403.

### 24.3 Other exports

24.3.1 CSV for reports; black-box CSV; the generic export endpoint returns xlsx (only Excel is used inside the app).

---

## 25. CLOUD SYNC — SUPABASE (`/sync`) `[Q-02] [Spec §1.2] [S-09]`

25.1 **Principle**: the local database is always the **truth**. Supabase is only a **mirror** so the owner can see numbers from home and an off-site copy exists. Without internet the app works exactly as before.

25.2 **Supabase setup (documented)**: create a project; take Project URL and API key (anon, or service-role if only for own use — keep secret); in the SQL editor create the mirror table:

```
create table if not exists public.area11_sync (
  shop_id   text primary key,
  shop_name text,
  sent_at   timestamptz,
  version   int,
  payload   jsonb,
  updated_at timestamptz default now()
);
-- trigger function area11_touch() sets updated_at := now() before each update
```

(Row-level security policies can be added for stricter protection.)

25.3 In Settings → Cloud sync enter URL, API key, **Shop id** (e.g. `area11-main`; use different ids for different branches) and table name `area11_sync`.

25.4 The `/sync` page shows status, counts (medicines/customers/suppliers/sales/batches) and three buttons: **push** (send to cloud), **see from cloud** (preview, changes nothing), **bring and apply here** (writes the cloud data onto this computer — an automatic backup is made first; it **overwrites** tables, so use deliberately).

25.5 **What is sent**: medicines, customers, suppliers, categories, companies, batches, and the last **90 days** of sales / purchases / payments / expenses. **Photos are not sent** (they travel in the zip backup).

25.6 Error messages (documented): URL/key missing; 401 key wrong or old; 404 table name wrong or table not created; network error; "no data for this shop in the cloud — push first".

25.7 Spec items not built: automatic background sync on reconnect (§1.2.2) and periodic sync every 15/30 minutes (§1.2.4); manual sync button exists (§1.2.3). Multi-branch is only prepared (`sync.shopId`).

25.8 Seeing the app from home (original advice): same Wi-Fi → `http://<PC-IP>:3000`; from the internet → host on a permanent Node host with HTTPS and a strong password. Never expose the app directly to the internet without HTTPS and a changed password.

25.9 TARGET (rebuild): no cloud in the first release; keep the data model sync-friendly (4.3.7); later options are the Supabase mirror, or a LAN/host server API for the second counter and Android.

---

## 26. PHOTOS AND CAMERA `[U-19] [U-21]`

26.1 Photos are **files in a folder** (`data/photos/`), the database stores only the file name; the folder is included in the zip backup.

26.2 Product and customer photos can be chosen from a file **or taken with the camera** ("Take with camera": live preview, switch front/back camera, the picture is reduced in size on the device before saving).

26.3 Very small images (under 100 bytes) are refused (protective rule; no practical effect).

26.4 Original limitation: the browser camera works only on `https://` or `localhost` (not on plain LAN `http://192.168…`) — a browser rule, not an app defect; the desktop rebuild has no such restriction.

---

## 27. OFFLINE AND PWA `[Spec §1.2.1] [Z-06]`

27.1 Billing, barcode scanning, printing and the cash register must work 100 % with no internet.

27.2 ORIGINAL: installable PWA ("Add to Home Screen"); a small amber bar when the internet is down; `/api` is never cached so billing data is always live; static UI files cached so the counter does not go blank; an offline page exists.

27.3 TARGET: a native desktop application is inherently offline; cloud/WhatsApp features degrade gracefully with a clear message.

---

## 28. SCREEN INVENTORY (original routes → rebuild screens)

| Route | Screen | Roles |
|---|---|---|
| `/` | Home dashboard | all |
| `/login` | Login | public |
| `/pos` | Counter billing | all |
| `/sales` | Sales history, returns, rush returns, return receipts | all |
| `/customers`, `/customers/[id]` | Customers and detail | all |
| `/products`, `/products/[id]` | Products and detail | owner/manager |
| `/purchases` | Purchases / stock-in | owner/manager |
| `/suppliers`, `/suppliers/[id]` | Suppliers and ledger | owner/manager |
| `/stock` | Write-off / adjust | owner/manager |
| `/stock-take` | Physical count | owner/manager (apply: owner) |
| `/cash` | Shifts, handover, expenses, drawings, day-end | owner/manager |
| `/alerts` | Expiry, reorder, negative stock, quarantine, dismissed alerts, WhatsApp pad | all view; owner dismisses |
| `/reports` | Reports | owner/manager |
| `/import` | Excel import/export/bulk update | owner/manager |
| `/audit` | Black box + backup/restore | owner (manager: view in original) |
| `/sync` | Cloud sync | owner/manager |
| `/settings` | Settings (14+ click-to-open sections, custom fields, staff) | owner/manager |
| `/receipt/[id]` | Receipts (sale, purchase, return, provisional) | all |
| `/offline` | Offline page | — |

28.1 The original had 21 pages and 69 routes (pages + API); all 21 pages were exercised without errors in the final review.

28.2 Global UI elements: top navigation, **global alert bar**, toasts, offline banner, auto-lock overlay.

---

## 29. SERVICE OPERATIONS INVENTORY (what the Core layer must offer)

29.1 Auth: login (password/PIN), logout, session check, lock/unlock, reset login, staff create/edit/deactivate.

29.2 Settings: read all, update (audited), defaults, custom-field CRUD.

29.3 Catalog: products CRUD + partial update + search + filter + group; categories/companies CRUD; custom values.

29.4 Suppliers: CRUD, ledger, payment (with overpaid warning), purchases, supplier returns.

29.5 Stock: batches by product (FEFO), movements ledger, write-off/adjust, stock-take session (open, count, apply), negative-stock registry.

29.6 Sales: create sale (12.2), sales search, void, receipts data, hold carts, change calculator data.

29.7 Customers: CRUD, ledger, payment receive, statement data.

29.8 Returns: validate, create return (RET), provisional return (PR) create/link/cancel, quarantine list + decide + history, return receipt data.

29.9 Alerts: active alerts, dismiss (owner + reason), restore, dismissed list.

29.10 Cash: shift open/close/handover, expenses, drawings, day summary.

29.11 Reports: summary by period, per-day, top medicines, category split, dead stock, CSV, WhatsApp text.

29.12 Audit: write (with old/new diff), query/filter, CSV export.

29.13 Backup: create zip, restore, auto-backup scheduler, trim old backups.

29.14 Import/export: template, preview, commit, bulk export, bulk update preview/apply.

29.15 Sync (later): push, preview, apply.

29.16 Numbering: `takeNextCode(kind)`.

29.17 Photos: save/read/delete files.

---

## 30. QUALITY, TESTING AND WORKFLOW

### 30.1 Quality gates (original; adapt names to the rebuild)

30.1.1 Type/build check clean (`npx tsc --noEmit` = 0 errors) → rebuild: **0 compiler warnings**.

30.1.2 Production build passes (69 routes) → rebuild: the application builds and starts.

30.1.3 Unit tests green (55/55 at stage-43; the legacy Vite demo had 212 unit tests + 36 end-to-end specs, all green).

30.1.4 `rules-count.sh` = `SAB THEEK` (136 numbered points, none missing) → rebuild: a **rules matrix** (rule → module → test); no numbered rule may be skipped.

30.1.5 Live smoke test with real data of every module before calling a stage done.

### 30.2 The 55 unit tests (areas that must be ported first, test-first)

30.2.1 Password/PIN hashing and verification; session token sign / verify / expiry / tamper.

30.2.2 Discount-limit math (role %, margin vs retail mode, below-cost lock).

30.2.3 Cash shift expected-cash and variance math.

30.2.4 Item-total reconciliation (plain bill, loyalty only, round-off only, both; line sums equal bill total) and round-off with cost guard.

30.2.5 Stock rules for negative stock (decisions for sale / supplier return / write-off; never blocks when allowed; blocks when `allowNegative` is OFF).

30.2.6 Audit diff (new record, deleted record, edited record = only changed fields, no-change = nothing).

30.2.7 Alert key, dismissed alerts hidden, **reason compulsory**, **owner only**, negative stock does not block work.

30.2.8 Report math (profit with restocked-return cost, margin; 5 tests).

30.2.9 Quarantine numbering and dispositions (4 tests).

30.2.10 ZIP engine (backup zip create/read, 5 tests).

30.3 The tests are the behavior specification; they are ported to xUnit before the code they cover.

### 30.4 Workflow and documentation duties (original)

30.4.1 Every piece of work ends with: build check, tests, save point `ckpt.sh save "…"` + `push`, update `HANDOFF.md` (§3 done list, §4 remaining), README status, and (when rules/specs change) the rules file `[U-14] [U-23]`.

30.4.2 Stages are tagged `stage-1…stage-43`; never delete a tag; never force-push.

30.4.3 New questions to the owner are asked in chat as multiple choice only — never in files.

30.4.4 Historic stage milestones (reference): Phase 1 complete at stage-14; Phase 2 essentials at stage-16; customization wave at stages 17–20 (custom fields, detail pages, click-to-open settings, room field, photos in folder, filters & groups); black box + backup/restore stage-19/20; supplier ledger + WhatsApp pad stages 21–22; split pay + loyalty + sub-categories stages 23–25; reports stages 26–34; Excel bulk update stage-30; backup with photos + daily auto backup stage-31; shift handover stage-32; camera photo stage-33; Supabase sync stage-34; Spec 1–4 stages 35 and 37; owner's four orders U-30…U-33 stage-38; return receipt stage-39; cost-of-returns accounting fix stage-40; quarantine numbers stage-41; Settings clean-up stage-42; second review round stage-43.

---

## 31. ACCEPTANCE SCENARIOS (verified live in the original — the rebuild must give the same results)

31.1 **Negative stock sale**: stock 5, sell 12 → sale succeeds, code `INV-0001`, warning "stock MINUS (−7) gone", `negativeItems = [{inStock 5, sold 12, after −7}]`. Turn `stock.allowNegative` OFF and sell 3 more → refused (400) "stock only −7 … negative stock is off". Turn ON again → sale goes through, stock −10.

31.2 **Audit old → new**: batch quantity `5 → −7` logged as `negative_sale`; a return logs `refunded_so_far 0 → 8000`, `cash_back 8000`; CSV exports Old/New.

31.3 **Return validation**: 12 bikes sold → returning 100 refused ("now only 12 can be returned"); returning an item of another bill refused ("Item #2 is not on this bill (INV-0001)"); returning 4 allowed (refund 8000 paisa); then returning 9 more (13 > 12) refused ("4 already returned — now only 8").

31.4 **Alert dismissal**: no reason → rejected; with reason → alert disappears, reason shown in `/alerts` and in the black box; a cashier attempting → "ending an alert is only the owner's job".

31.5 **Supplier return with negative stock**: batch at −4, return 6 → completes, stock −10, response `warnings: ["…stock MINUS (−10) gone"]`.

31.6 **No-bill return**: stock −7, 3 returned → stock **−4 immediately**; then link the bill → stock **stays −4** (no double add); the alert remains ("PR-0001 — bill (INV-0001) linked, owner's confirmation pending").

31.7 **Return numbers**: first return `RET-0001`, second `RET-0002`, third (plain restock) `RET-0003` with **no** Q-number; receipt pages `/receipt/1?type=return&code=RET-0001` and `?type=provisional` print correctly.

31.8 **Quarantine**: a return with `restock:false` → `Q-0001`, disposition "in quarantine", alert raised; decision "damaged" → disposition "expired-damaged", alert ends; another return → `Q-0002` → "back to stock" → batch `4 → 5`; history lists both; `Q-0001` printed on the receipt.

31.9 **Profit with returns**: sold 6 for Rs 480 (cost Rs 300), returned 3 (Rs 240 refunded, cost Rs 150 restocked) → profit **Rs 90**, margin **37.5 %**, returned count 3; cash/day-end profit also Rs 90.

31.10 **Udhaar**: credit sale Rs 250 → customer balance Rs 250; collect Rs 100 → balance Rs 150.

31.11 **Shift**: opening float Rs 200, cash sales Rs 140 → expected cash **Rs 340**, counted 340 → difference 0.

31.12 **Stock-take**: book 43, counted 48 → stock 48, difference +5 (movement type adjust); a session cannot be applied twice.

31.13 **Excel**: export gives a ~47 KB `.xlsx`; re-import recognizes duplicates and skips them ("already exists"); bulk update with the same file = 0 changes.

31.14 **Expiry alert**: a batch expiring in 45 days appears in the alerts page.

31.15 **Photos**: upload → stored in the photos folder, only the name in the database → readable again.

31.16 **Backup**: zip (≈360 KB in test) contains database + photos; restore switches without restart.

31.17 **Supplier overpayment**: payment when nothing is owed → payment accepted, alert "paid in advance", black-box row `overpaid: true`.

31.18 **Price examples**: margin discount cost 200 / retail 300 / 10 % → Rs 290; round-off 545 → 540 and 556 → 550; 545 with cost 543 → 545; 545 with cost 500 → 540; below-cost discount refused.

31.19 **Roles**: a cashier opening an owner/manager page is redirected to `/pos` (HTTP 307) and the API returns 403; a cashier cannot dismiss alerts, import data, run stock-take apply, or manage custom fields.

31.20 **Partial product edit**: product with cost 700, box 2, strip 10, rack A-1, reorder 20 — changing only retail `1000 → 1500` leaves everything else unchanged.

31.21 **Settings clean-up**: 70 of 70 keys read by code; after the clean-up `/settings`, `/alerts`, `/`, `/pos`, `/reports`, `/cash` return OK.

31.22 **Receipts**: after a sale the receipt opens automatically; after a return the return receipt opens automatically.

---

## 32. DEFECTS FOUND AND FIXED (regression guard — do not reintroduce)

32.1 Bill total ≠ sum of items because round-off and loyalty were not spread over lines (Rs 7.50 gap) → reconcile lines (11.4); 6 tests.

32.2 Return cost not flowing back into profit (profit shown Rs −60 instead of Rs 90) → 15.6 / 21.6; 5 tests.

32.3 New records stored without new value in the audit; empty `before` produced no diff → 22.4.

32.4 Partial product update zeroed other fields → 8.1.4.1.

32.5 Concurrent first-start migration crash (`duplicate column name`) → append-only migrations under `BEGIN IMMEDIATE` with a repeated check (5.1.6).

32.6 Rows with a null prototype could not be passed to UI → plain objects (5.1.13).

32.7 Sale lines without explicit batch let batch stock and ledger drift → FEFO auto-pick (10.2.3).

32.8 An empty shift auto-created at first start → removed (18.1.1).

32.9 Supplier return stopped with "Batch has only N" → warning only (9.3.2, U-30).

32.10 Hidden settings (`bill.stockTakePrefix`, `provisionalPrefix`, `supplierReturnPrefix`, `discount.blockBelowCost`, `owner.name`) → shown (6.4.1).

32.11 Alert "✕" hid an alert only for the session → durable owner dismissal with reason (16.4.5).

32.12 Prisma could not be installed (engine download blocked) → built-in SQLite, no ORM (T-03).

32.13 "Apply from cloud" overwrites tables → automatic backup first, explicit warning (25.4).

32.14 Camera unusable on plain LAN `http` → browser rule, documented (26.4); not an issue on desktop.

32.15 Accounting: tax/discount/loyalty/round-off total consistency verified by tests; profit is on **purchase cost stored with the batch** — if a batch has no cost entered, profit appears wrong (data entry rule: always enter cost).

---

## 33. STATUS AND COVERAGE

### 33.1 Phase status of the original (≈ 99 % complete at 7 Oct 2026)

| Phase / work | State |
|---|---|
| Stone 0 / 0.5 (foundation, 116 rules, settings) | ✅ |
| Phase 1 (Core lifeline: stock-in, packing, batches, counter, round-off, receipt, hold cart, login + roles, URL guard, offline shell, cash tendered, write-off, Excel import) | ✅ complete (stages 9–14) |
| Phase 2 (returns, cash shift, expenses, drawings, backup, lost-bill lookup, rush returns, handover) | ✅ complete |
| Discount guard (spec 7.3: role limits + below-cost) | ✅ |
| Phase 3 (supplier ledger, supplier returns, 3-level expiry, reorder, WhatsApp, sample stock) | ✅ (with the gaps listed in 33.2) |
| Phase 4 (customers, credit limit, loyalty, tiers, split pay, custom fields/categories) | ✅ (with gaps in 33.2) |
| Phase 5 (audit, search, breakage, stock-take, reports, backup with photos + daily auto, camera, Supabase sync) | ✅ — only hardware (thermal printer + USB scanner) test at the shop is pending |
| Owner's 4 orders (stage-38, U-30…U-33), Spec 1–4 (stages 35+37), receipts (39), cost fix (40), quarantine numbers (41), settings clean-up (42), review round 2 (43) | ✅ |

### 33.2 Coverage of the Master Specification (per the documents) — what the rebuild must still ensure

| Spec | Status in original | Rebuild action |
|---|---|---|
| §1.1 platform | web/PWA | native desktop (Windows) |
| §1.2.1 offline | ✅ | keep |
| §1.2.2 auto-sync on reconnect, §1.2.4 periodic sync | ❌ not built (manual push/pull only) | later, with sync/server |
| §1.2.3 manual sync | ✅ | later |
| §1.2.5 offline stock conflict | ✅ (negative stock + alert) | keep |
| §1.3.1 manual backup | ✅ | keep |
| §1.3.2 scheduled backup | ✅ (daily, configurable) | keep |
| §1.3.3 daily auto-send by e-mail/WhatsApp | ❌ (manual WhatsApp summary) | build |
| §1.3.4 archival of old data | ❌ not evident | build |
| §2.1 categories, §2.2 custom fields | ✅ (types differ, 8.4.2) | support all types |
| §3 purchases, §4 packing, §5 batches/FEFO | ✅ | keep |
| §6 expiry 3 + expired, isolation | ✅ | keep |
| §7 rates, margin/retail discount, below-cost lock, role limit | ✅ | keep |
| §8 invoice code, round-off, receipt settings, 58/80 mm, scanner, hold, split, tax switch | ✅ (hardware test pending) | keep + silent printing |
| §8.4.2 several barcodes per product (per pack size) | partial (single barcode field) | allow several |
| §9 returns, lost-bill lookup, provisional return, reasons | ✅ | keep |
| §10.1 customer/credit/stars | ✅; lifetime profit per customer ❌ | add |
| §10.2 supplier ledger, credit note | ✅ | keep |
| §10.3 company click → medicines ✅; medical rep / salesman info ❌ | | add |
| §10.4 prescriber/doctor tracking | ❌ not evident | build |
| §10.5 product lifecycle | ✅ | keep |
| §11.1 search brand/salt/rack ✅; pack-size search ❌ | | add |
| §11.2 3-level reorder (partial), supplier-wise filter ❌, WhatsApp ✅ | | complete |
| §11.3 sample/bonus | ✅ | keep |
| §12 roles, role switcher (superseded by login) | ✅ | keep |
| §13 black box | ✅ (viewer rights differ: 35.4) | keep |
| §14 financials | ✅ | keep |
| §15.1 top / dead stock ✅; all-time highest/lowest day ❌ | | add |
| §15.2.1 vendor return cutoff alert ❌; §15.2.2 write-off ✅; §15.2.3 alternative suggestion ❌; §15.2.4 stock-take ✅ | | build the ❌ ones |
| Owner orders U-25…U-36 | ✅ | keep |

### 33.3 Original remaining list (as of stage-43)

33.3.1 Merge PR #3 (`arena/01a10395-area11` → `main`); then close PR #2. (Repository housekeeping of the original web app.)

33.3.2 Netlify project `marea11`: set Base directory = `legacy-demo-vite` (demo site only).

33.3.3 Connect Supabase (create project → run SQL → enter URL + key → push).

33.3.4 **Hardware test at the shop**: thermal printer (58/80 mm) and USB barcode scanner.

33.3.5 Camera over plain-LAN `http` limitation (browser rule).

33.3.6 First-day security actions: change the owner password, take a `.zip` backup and keep it somewhere safe.

### 33.4 Features not built yet in the original

33.4.1 Monthly **tax report** (for tax returns) — tax can be applied but not reported separately.

33.4.2 Receipt/slip for **customer payment collection**.

33.4.3 **Customer monthly statement** (print/WhatsApp).

33.4.4 Photos inside the "Data (Excel)" export (by owner's order; photos travel only in the zip backup).

33.4.5 Full **multi-branch** feature (only `sync.shopId` exists).

### 33.5 Suggested improvements (from the review)

33.5.1 Show an expiry count on the home page.

33.5.2 Evening reminder to close the cash shift.

33.5.3 Protect the stored cloud key.

33.5.4 A per-user shift report; a quick user switcher.

### 33.6 Go-live risks to handle (from the review)

33.6.1 Default credentials (`area11` / `1111`) must be changed first.

33.6.2 `sync.key` stored in plain text; never hand the database file to anyone.

33.6.3 Never expose the app to the internet without HTTPS and a strong password.

33.6.4 Login throttling in the original is in memory only.

33.6.5 **Never delete the `data` folder** (the real data lives there); always take a `.zip` backup before updating the app.

33.6.6 Persistently negative stock on one medicine means theft, breakage or entry errors — review the "Stock MINUS" card monthly and run a stock-take.

33.6.7 Do not edit numbering counters by hand (duplicate bill numbers).

33.6.8 Profit is computed on the purchase cost stored with the batch; a missing cost makes profit wrong.

33.6.9 "Apply from cloud" overwrites the shop's data on this computer — understand it before pressing.

33.6.10 Minor facts: an image under 100 bytes is refused; the generic export endpoint returns xlsx even if csv is asked.

---

## 34. REBUILD PLAN AND AGENT PROTOCOL (TARGET: C# / .NET desktop)

34.1 **Goal**: reproduce every behavior in this file as a native Windows desktop application (Windows 10 preferred), fast, with direct printing, with a clean UI-free Core so the second counter and the Android app can be added later.

34.2 **Step 0 — read first, write no code**: read this entire file. If the repository is available, also read `HANDOFF.md`, `AGENTS.md`, `PHASE-TWO-INFORMATION.md`, `INPUT-INFORMATION.md`, `docs/APP-TAFSEEL.md`, `src/lib/*.ts` and `src/lib/*.test.ts` as the behavior reference (do **not** modify or delete the Next.js app; build in a new `/dotnet` folder). Then write `dotnet/PORT-PLAN.md` (modules, tables, rule numbers, port order) and **wait for the owner's approval**.

34.3 **Architecture reminders**: Section 4.3 and 4.4 (Core / Desktop / Tests; service interfaces; DTOs; Dapper + Microsoft.Data.Sqlite; same schema and migrations; PBKDF2; ClosedXML; zip; silent printing; Urdu as graphics; no EF Core; no SQL in view-models; architecture test).

34.4 **Workflow and gates (every module)**: zero build warnings; all tests green; run the app and look; commit; tag `net-stage-N`; update `dotnet/HANDOFF.md` (done / remaining / gotchas); keep a **rules matrix** (rule → module → test); no rule is skipped; ask the owner only in multiple-choice, all questions together; talk to the owner in Urdu, code and UI in English.

34.5 **Port order**: (1) schema + settings + audit; (2) auth + roles; (3) products / categories / companies / custom fields; (4) suppliers + purchases; (5) POS / sales / discount guard / round-off; (6) receipts + printing; (7) returns / provisional / quarantine / alerts; (8) customers / credit / split / loyalty; (9) cash / shifts / handover; (10) stock write-off / stock-take; (11) reports; (12) audit viewer / backup / restore; (13) Excel import / bulk update; (14) the spec items marked ❌ in 33.2.

34.6 **Test-first**: port the 55 existing tests to xUnit before the code they cover; add tests per module; add the acceptance scenarios of Section 31 as integration tests.

34.7 **Hardware last**: test the printer and the scanner at the real shop PC at the end; absence of devices must not stop the app.

34.8 **Android later**: the owner does not want Android sync now but wants to add it later — possibly a separate Kotlin app (viewing data first; selling later). Because of the interface/DTO design (4.4) it will connect to the same data through the future server API (or the Supabase mirror). Phone selling brings two-device stock conflicts — handled by negative-stock + alert rules (Section 16) and server-side transactions.

34.9 **Second counter later**: one computer now; later a main PC runs `Area11.Server` (ASP.NET Core minimal API, same Core + same SQLite file) and the second counter's Desktop uses the HTTP implementation of the service interfaces. WAL mode and single-transaction operations prevent double-selling.

34.10 **Ready agent prompt**: "You are rebuilding Area11 (offline pharmacy POS + inventory) as a native Windows desktop app in C#/.NET. Read AREA11-MASTER-SPEC.md completely first and write dotnet/PORT-PLAN.md without code; wait for my OK. Follow Sections 3, 4, 34. Port tests first; one module at a time; after each module: 0 warnings, all tests green, run, commit, tag net-stage-N, update dotnet/HANDOFF.md. If unsure ask me in multiple-choice, all questions together. Talk to me in Urdu; code and UI in English."

34.11 **Definition of done**: all numbered rules in Section 3 satisfied; all Section 31 scenarios pass; all Section 33.2 ❌ items either built or explicitly deferred by the owner; hardware tested at the shop; first-day security steps done (33.3.6).

---

## 35. OPEN POINTS FOR THE OWNER (conflicts and gaps found while consolidating) — recommended defaults given

35.1 **Cashier return → restock?** Stage-16 and the 6 Oct text of `APP-TAFSEEL` §2.5: only owner/manager can put returned goods back on the shelf; a cashier's return stays in quarantine. Stage-38 rule `U-32`: returned goods go straight into stock, no quarantine. The documents do not say the cashier gate was removed. **Recommended default**: setting `returns.cashierRestockToShelf` ON (follows U-32); OFF reproduces the older gate.

35.2 **Quarantine vs "no quarantine"**: `U-32` removes quarantine for ordinary returns; `U-34` keeps quarantine (with `Q-` numbers) for goods deliberately not restocked. **Default**: both as built — ordinary returns restock at once, quarantine only on deliberate "not back to stock".

35.3 **Role switcher** (Spec §12.2) vs real login: **Default**: real login only (no production switcher).

35.4 **Who sees the audit log and net profit?** Spec §12.1.2/§13.3: owner only (manager must not see net profit or the secret audit). Original: manager can open `/audit` and `/reports` (profit cards). **Default**: keep original behavior but make it a permission toggle (E-11); recommend switching audit + net-profit to owner-only.

35.5 **Loyalty arithmetic**: spec says 1 point per Rs 100 and 5 stars → VIP; original has `rupeesPerPoint` and `vipThreshold` and redeems 1 point = Rs 1. **Default**: keep the settings, set defaults to the spec (100 rupees per point, VIP at the 5-star threshold); loyalty stays OFF `[Q-06]`.

35.6 **Barcode per pack size** (Spec §8.4.2): original stores one barcode. **Default**: allow multiple barcodes per product, each mapped to a unit (box/strip/tablet).

35.7 **Void-bill roles**: not stated. **Default**: owner/manager only.

35.8 **Customer fields required** (name/phone optional or compulsory): controlled by custom-field style settings; **default**: name required, phone optional.

35.9 **Receipt printing language**: Urdu text as graphics; **default**: English receipt, Urdu optional lines rendered as images.

35.10 **Spec items not evident in the original** (33.2): owner to confirm priority; **default**: build them after the ported core is accepted.

---

## 36. GLOSSARY (Roman-Urdu terms used in the sources)

| Term | Meaning |
|---|---|
| udhaar / khata / baqaya | credit sale / customer account / outstanding balance |
| galla | cash drawer (the "shift") |
| ginti | counting (stock-take) |
| almari / kamra | cupboard / room (medicine location) |
| dawa / dawayen | medicine(s) |
| bikri | sale |
| wapsi / wapasi | return |
| raseed / parchi | receipt / slip |
| malik | owner |
| manfi | negative |
| mehfooz | safe/stored |
| purana → naya | old → new |
| wajah | reason |
| hadd | limit |
| kharaab | damaged |
| frookht | sold |
| quarantine (qarantina) | holding area for returned goods not restocked |
| SAB THEEK | "all fine" (output of the rule counter) |
| FEFO / FIFO | first-expiry-first-out / first-in-first-out |
| paisa | 1/100 rupee (storage unit) |
| PINV / INV / RET / PR / SR / ST / Q | codes: purchase / sale / return / rush (provisional) return / supplier return / stock-take / quarantine |
| black box | the permanent audit log |
| wa.me | free WhatsApp link format |

---

## 37. SOURCE INDEX AND KNOWN GAPS (honest statement)

37.1 **Sources consolidated here**: `complete_numbered_master_specs (1).md` (322 lines; 16 sections, 44 sub-sections, 102 micro-points, 5 phases — reproduced in Appendix A); `PHASE-TWO-INFORMATION.md` (U/R/E/S/P/B/Z/T/M/Q rules — Section 3); `INPUT-INFORMATION.md` (copy of the spec + A-01…A-49 which restate the spec, and Q-01…Q-19 — Section 3.10); `HANDOFF.md` (status, run, gotchas, hosting, rollback); `AGENTS.md` (rule sheet); `README.md` (status per stage, files map, commands); `docs/APP-TAFSEEL.md` (full Urdu feature description, Settings table, remaining work, risks); `docs/SUPABASE.md` (sync setup).

37.2 **Not available in the documents (therefore not invented here)**: the exact column list of every table and the names of migrations 005, 008, 009; the exact names and defaults of the remaining setting keys that bring the total to 70; the seed data details; the exact Excel template column headings; the exact label texts and screen layouts; the precise API route names; the exact text of WhatsApp messages and of Urdu strings; the contents of the legacy Vite demo's domain logic (`legacy-demo-vite/src/domain/*`) which the original reused as a design reference.

37.3 **How to close these gaps**: where the original repository is accessible to the developer/agent, read the files named in 34.2 (especially `src/lib/schema.ts`, `src/lib/settings.ts`, `src/lib/*.ts` and `src/lib/*.test.ts`) and copy the exact schema, setting keys and test cases; where it is not accessible, define the missing items in the spirit of this file (no hard-coding, everything editable) and record each choice in `dotnet/HANDOFF.md`.

37.4 This file does not repeat the Urdu originals; an Urdu translation of each section can be added below it in a later step without changing the English text.

---

## APPENDIX A — THE MASTER SPECIFICATION IN ENGLISH (original clause numbers kept)

> This is the owner's original "Master Blueprint: Smart Pharmacy & Retail POS and Inventory System — Master PRD & Agent Execution Plan", translated clause by clause. It is declared the **Single Source of Truth**. Agents must not build everything at once but follow the **5-phase roadmap of Section 16** strictly: first the most urgent part (Phase 1: Core Lifeline) so that the counter works; then Phases 2, 3, 4, 5 in order.

### SECTION 1: CORE SYSTEM, PLATFORM AND SYNC

**1.1 Platform and compatibility**
- 1.1.1 Web-based platform (Web / PWA): accessible on any device through a browser. (Rebuild: native desktop — see 4.3.)
- 1.1.2 Operating-system support: fully functional on Windows (PC/Laptop) and Android (Mobile/Tablet).
- 1.1.3 Cross-device operation: the same interface on laptop, tablet or mobile in the shop.

**1.2 Hybrid online/offline sync engine**
- 1.2.1 Full offline capability: when the internet is cut, billing, barcode scanning, printing and the cash register keep working 100 % without stopping.
- 1.2.2 Automatic sync: as soon as the internet is restored, all new data moves to the cloud/main server automatically in the background.
- 1.2.3 Manual sync button: the user can force syncing instantly with one click whenever he wants.
- 1.2.4 Periodic sync: a background sync attempt at set intervals (e.g. every 15 or 30 minutes).
- 1.2.5 Multi-counter offline conflict rule: if the internet is down and two counters sell the last item at the same time, the system will not crash but will show stock as minus and raise an alert.

**1.3 Data backup and old records**
- 1.3.1 Manual backup: full database backup at any time with one click.
- 1.3.2 Scheduled weekly backup: automatic safekeeping of all data every week.
- 1.3.3 Daily auto-send: every night the backup file or report goes automatically to e-mail or WhatsApp.
- 1.3.4 Protection of old data (archival): keep years-old rates and sales data separately so the system does not slow down.

### SECTION 2: CUSTOM CATEGORIES AND DYNAMIC FIELDS

**2.1 Self-governed categories**
- 2.1.1 Unlimited categories: the admin can create new categories and sub-categories at will (e.g. Medicine, Surgical, Cosmetics, General, Baby Care).
- 2.1.2 Edit and delete: full authority to edit or delete any category.

**2.2 Dynamic custom fields builder**
- 2.2.1 Add and remove fields: add a new field to the product or customer form, or remove an unnecessary field.
- 2.2.2 Field data types: (a) Text — name, formula, remarks, brand; (b) Number — milligrams (mg), pack size, count; (c) Fixed amount / currency — a specific fixed charge, distributor commission; (d) Photo / image — product, address or prescription picture.
- 2.2.3 Mandatory / optional toggle: whether the customer's name and mobile number are optional or compulsory.

### SECTION 3: PURCHASES, STOCK-IN AND AUTO PURCHASE-BILL CODE

**3.1 Auto sequential purchase invoice code**
- 3.1.1 Sequential auto code: whenever new goods arrive in the shop the system issues an auto purchase bill number in sequence (e.g. PINV-001, or 1, 2, 3).
- 3.1.2 Full view of the goods invoice (consignment tracking): from this bill number one can later see which products came in that particular delivery, in which batch and at which rate.

**3.2 Stock-entry procedure**
- 3.2.1 Choose the supplier: from a drop-down, or add a new supplier on the spot.
- 3.2.2 Instant product search or new entry: if the medicine already exists, search and select; if it is a new product, register the new item immediately on the same entry screen.
- 3.2.3 Quantity and cost: number of boxes, strips or tablets, batch number, expiry, purchase price and retail price.
- 3.2.4 Entry in the supplier's account: the moment the entry is finalized the amount moves to the supplier's account automatically.

### SECTION 4: PACKAGING, BASE UNIT AND CUTTING SYSTEM

**4.1 Base smallest unit (Tablet / ml / Piece)**
- 4.1.1 Calculation at tablet level: inventory is always based on the smallest unit (tablet, capsule or ml) so that even when loose medicine is sold the count stays 100 % correct.

**4.2 Automatic packing ratio conversion**
- 4.2.1 Formula configuration, set once when the product is added: 1 Box = X Strips = Y Tablets. Examples: 1 Box = 2 Strips = 20 Tablets; or 1 Box = 10 Strips = 100 Tablets.
- 4.2.2 Auto multiplication in purchase: the employee types only "5 Boxes"; the system counts and adds 100 tablets to the inventory.
- 4.2.3 One-click buttons at the counter: three quick selection buttons on the billing screen: **[Box]**, **[Strip]** and **[Tablet/Loose]**.

### SECTION 5: BATCH MANAGEMENT AND FIFO SEARCH

**5.1 Multi-level batches**
- 5.1.1 Batch number compulsory: every purchase carries a batch number.
- 5.1.2 Separate cost and sale: several batches of the same product at once, saved with different purchase price, sale price and expiry.
- 5.1.3 Live batch stock: how much stock of each batch is on the shelf is visible separately.

**5.2 Intelligent FIFO search and batch selection**
- 5.2.1 Nearest-expiry priority: when searching, the batch whose expiry is nearest appears at the top.
- 5.2.2 Batch pop-up and transparency: typing the name shows all current batches of that medicine, expiry, price and current stock; the cashier clicks the required batch.

### SECTION 6: EXPIRY MANAGEMENT AND 3-STEP ALERTS

**6.1 Adjustable expiry alert levels (thresholds and colours)**
- 6.1.1 Level 1 (far alert): days and colour set from settings (e.g. 1 year before, green/blue).
- 6.1.2 Level 2 (middle warning): set from settings (e.g. 6 months before, yellow).
- 6.1.3 Level 3 (nearest danger — Very Near): set from settings (e.g. 3 months before, orange/red).

**6.2 Dedicated expiry dashboard (menu tab)**
- 6.2.1 Four separate boxes: box 1 — goods at level-1 alert; box 2 — level-2; box 3 — level-3; box 4 — fully expired goods.
- 6.2.2 Separate isolation of expired goods: expired goods are blocked from counter sale and available only for "supplier return".

### SECTION 7: PRICES, PROFIT PROTECTION AND DISCOUNT ENGINE

**7.1 Multi-tier rates**
- 7.1.1 Normal retail rate: for an ordinary customer.
- 7.1.2 VIP rate: for regular or good buyers.
- 7.1.3 Doctor / institutional rate: special rate for clinics, hospitals or doctors.
- 7.1.4 Automatic selection: as soon as the customer is selected the matching rate applies automatically.

**7.2 Advanced dual-method discount engine**
- 7.2.1 Discount on profit margin: if purchase price is Rs 200 and sale price Rs 300 (profit = Rs 100); a 10 % discount applies only to Rs 100 (i.e. Rs 10 discount, final price Rs 290).
- 7.2.2 Discount on total retail price: the traditional discount applied to the product's retail price or the whole bill.
- 7.2.3 Selection from settings: the admin decides which method the shop uses.

**7.3 Discount safeguards**
- 7.3.1 Hard lock at purchase price (never below cost): the system will never allow a medicine to be sold below its purchase price.
- 7.3.2 Cashier's discount limit (max discount %): the owner or manager can fix a maximum discount for the cashier.

### SECTION 8: BILLING, RECEIPT AND HARDWARE CONTROL

**8.1 Auto sequential sales bill number**
- 8.1.1 Auto invoice code: as soon as a bill is made a unique sequential bill number is generated at the top (1, 2, 3 or INV-0001).

**8.2 Downward floor round-off to 10s**
- 8.2.1 In the customer's favour: the last 1 to 9 rupees of the bill are always cut off, the price is never increased. 545 → 540; 556 → 550.

**8.3 Bill and receipt settings (customizable receipt engine)**
- 8.3.1 Date and time position: top or bottom, chosen from settings.
- 8.3.2 Shop header and footer: shop name, address, phone numbers and custom terms (guarantee, return policy).
- 8.3.3 Price display options (columns toggle): show or hide the original price before discount; show the price after discount; or show only the final total, product name and quantity.

**8.4 Hardware support**
- 8.4.1 Thermal printer: adjustable format for both 58 mm and 80 mm roll sizes.
- 8.4.2 Barcode scanner gun: scanning with a handheld gun; support separate barcodes for different pack sizes of one product.

**8.5 Counter operations (cart controls)**
- 8.5.1 Hold and park cart: hold an incomplete bill, make another customer's bill, and come back to the first customer.
- 8.5.2 Split payments: half cash and half credit, or half cash and half online.
- 8.5.3 Master switch for sales tax: switch tax completely off (0 %) or on globally from settings.

### SECTION 9: SALES RETURN AND TRACKING

**9.1 Mandatory link with the original bill number**
- 9.1.1 Automatic stock and money return: when a return happens money goes minus and exactly that batch is added back into stock automatically.

**9.2 If the paper bill is lost**
- 9.2.1 Smart bill search: as soon as the medicine name, customer phone number or approximate date is typed the system shows all earlier related bills.
- 9.2.2 Provisional return in rush time: the cashier can refund the money immediately and free the customer.
- 9.2.3 Permanent pending-bill alert: for a provisional return a red notification keeps blinking in the system until the employee tags the original bill.

**9.3 Return reasons**
- 9.3.1 Classification of reasons: defective goods, expired medicine, customer change of mind, or wrong medicine entry, with custom remarks.

### SECTION 10: PROFILES, CONTACTS AND DEEP TRACKING

**10.1 Customer profile and loyalty stars**
- 10.1.1 Customer data: name, phone number, category (normal, VIP, doctor).
- 10.1.2 Hard credit limit: a credit limit is set; when it is exhausted credit bills are closed.
- 10.1.3 Customer profit tracking: lifetime profit earned from this customer.
- 10.1.4 Customer loyalty stars (1 to 5): points on every Rs 100; automatically becomes VIP at 5 stars.

**10.2 Supplier profile and ledger**
- 10.2.1 Supplier data: name, agency, phone, outstanding amounts.
- 10.2.2 Complete record of items bought from the supplier: when what goods came and at what rate.
- 10.2.3 Account for expired/damaged goods returned: on returning goods to the supplier a credit note goes into the account and the amount is deducted from the next invoice.

**10.3 Company and medical rep / salesman**
- 10.3.1 Company data: clicking a company gives the list of all its medicines.
- 10.3.2 Salesman information: the name, phone number and visit day of the relevant medical rep / salesman.

**10.4 Prescription and doctor tracking**
- 10.4.1 Entry of the doctor's prescription: on which doctor's prescription a medicine was sold and how much profit came from it.

**10.5 Product's complete lifetime history**
- 10.5.1 Purchase history: when goods came, from which supplier, which batch and rate.
- 10.5.2 Sales history: when sold to which customer, at what rate.

### SECTION 11: INVENTORY INTELLIGENCE AND SMART SEARCH

**11.1 Multi-dimensional search**
- 11.1.1 Search by brand name.
- 11.1.2 Search by pack size: 10 ml, 20 ml, 50 ml etc.
- 11.1.3 Salt / formula search (generic search): typing the formula shows all available medicines.
- 11.1.4 Rack and shelf number (shelf/rack locator): as soon as a medicine is searched its rack number (e.g. Rack B-12) is shown.

**11.2 3-level stock reorder alerts**
- 11.2.1 Alert level 1 (reorder warning): goods are nearly finished.
- 11.2.2 Alert level 2 (critical / order now): must be ordered urgently.
- 11.2.3 Alert level 3 (red alert / out of stock): completely finished.
- 11.2.4 Reorder menu page: a separate screen where all low-stock medicines are filtered supplier-wise.
- 11.2.5 Free WhatsApp Web purchase order: send a formatted list on WhatsApp Web without any paid API.

**11.3 Sample and bonus stock (at 0 cost)**
- 11.3.1 Entry of free goods: its purchase cost is saved as Rs 0 so the shop's expenses do not rise but the stock is added.

### SECTION 12: USER ROLES AND SECURITY

**12.1 Three-level roles**
- 12.1.1 Owner / Super Admin: unlimited powers; the shop's true total profit/loss (P&L), user passwords and audit log.
- 12.1.2 Manager: change item price, set credit limit, clear pending bills, view operational reports. (Net profit and the secret audit stay hidden from him.)
- 12.1.3 Cashier: only make bills, hold cart, and give discount within a fixed limit.

**12.2 Initial testing switch (Phase-1 role switcher)**
- A temporary drop-down in Phase 1 so one can easily switch between Owner, Manager and Cashier for testing. (Superseded by real login.)

### SECTION 13: IMMUTABLE BLACK-BOX AUDIT TRAIL

- 13.1 Complete ban on delete and edit: not even the admin or manager can delete or change a single character of this log.
- 13.2 Most important logs: system login/logout, bill made, bill cancelled, return, stock entry, price change — with time stamp and user.
- 13.3 Access: it will be visible only to the owner.

### SECTION 14: FINANCIAL MANAGEMENT AND CASH CLOSING

**14.1 Evening closing and shift handover (cash drawer reconciliation)**
- 14.1.1 Morning opening cash (float): entry of the small change in the drawer.
- 14.1.2 Mid-shift handover: instant cash accounting when an employee changes.
- 14.1.3 Final night closing: comparison of the actual drawer amount with the system's sales.

**14.2 Owner's personal drawing account**
- 14.2.1 Personal expense cash: cash taken for bike tuning or personal groceries is debited to the owner's account.
- 14.2.2 Owner's medicine use: medicines taken by the owner also go into the same account.
- 14.2.3 Return of money: when the owner deposits money back into the counter it is credited.

**14.3 Shop operational expenses**
- 14.3.1 Daily expenses: tea, cleaning.
- 14.3.2 Monthly expenses: shop rent, electricity, salaries — so that net profit can be computed.

### SECTION 15: ANALYTICS, REPORTS AND SOLUTIONS TO GROUND PROBLEMS

**15.1 Ranking of sales and profit**
- 15.1.1 Top items (by volume and by profit).
- 15.1.2 Dead stock / lowest-selling items.
- 15.1.3 All-time highest and lowest sales days.

**15.2 Automatic solutions to ground-level problems**
- 15.2.1 Vendor return cutoff window: alert 3 to 6 months before expiry.
- 15.2.2 Damage and breakage write-off: one-click write-off of destroyed goods.
- 15.2.3 Instant generic alternative suggestion: alternative brands at once when a medicine is out.
- 15.2.4 Physical stock check (cycle count / shelf audit): shelf-wise counting feature.

### SECTION 16: AGENT'S PHASE-WISE ROADMAP (5-PHASE EXECUTION PLAN)

**Phase 1: Core Lifeline MVP (the compulsory basics)**
1. Stock entry with automatic purchase bill number (Stock-In with auto PINV code).
2. Smallest unit with each product, packing formula (1 Box = X Strips = Y Tablets).
3. Entry of batch number and expiry.
4. Counter billing with auto invoice code, barcode and product search (nearest-expiry batch on top).
5. [Box], [Strip], [Tablet] counter selection buttons.
6. Downward round-off (floor rounding to 10s).
7. Instant thermal printing (58 mm/80 mm) and hold/park cart.
8. Initial role switcher (Owner / Manager / Cashier toggle).

**Phase 2: Security, returns and financial foundation**
1. Sales return with smart bill search.
2. Provisional return in rush time and permanent pending-bill notification alert.
3. Morning float, shift handover, and evening cash closing.
4. Owner's personal drawing ledger.
5. Entry of shop operational expenses.
6. Automatic lock when discount goes below cost.

**Phase 3: Smart inventory and supplier relationship**
1. Supplier profile and ledger.
2. Return of expired/damaged goods to the supplier and deduction of the amount from the account.
3. 3-step expiry alerts with custom colours and a fourth tab for expired goods.
4. 3-level stock reorder alerts with free WhatsApp Web purchase order.
5. Entry of sample/bonus stock (at 0 purchase cost).

**Phase 4: Customers, loyalty and advanced billing**
1. Customer profiles, hard credit limits and lifetime profit tracking.
2. Customer loyalty stars (1 to 5) and automatic VIP upgrade.
3. Multi-level prices (normal, VIP, doctor rate).
4. Split payments (half cash, half credit, or half online).
5. Admin control to create custom fields and custom categories.

**Phase 5: Intelligence, security and hybrid sync**
1. Immutable black-box audit log (for the owner).
2. Multi-dimensional search (salt search, pack size and rack number locator).
3. Breakage account (damage write-off) and physical stock audit.
4. Advanced analytics, dead stock and P&L reports.
5. Online + offline syncing and scheduled backups.

### MASTER VERIFICATION MATRIX (from the source)

- Main sections: 16 · Sub-sections: 44 · Detailed micro-specifications: 102 · Roadmap phases: 5.
- "Not one single point, rule or business logic is now outside this document."

---

## END OF DOCUMENT

*Area11 Master Specification v1.0 — compiled from the repository documents at stage-43 (7 October 2026). When the owner issues a new order it gets a new rule number in Section 3 and the relevant module section is updated; the open points of Section 35 are resolved by the owner.*
