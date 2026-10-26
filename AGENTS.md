# AGENTS.md - how to work in this repository

This file is for **any AI coding agent** that is given only the repository link (the owner's rule
U-13). Read it fully before touching code; it is short on purpose.

Owner: Dr. Adil Abdullah (does not program). Product: **Area11**, an offline pharmacy POS +
inventory for Windows. Language: talk to the owner in **Urdu or English matching his message -
never Roman Urdu**. Code, database and UI stay in **English**.

---

## 1. Start here (10 minutes)

0. **[`TAKEOVER.md`](TAKEOVER.md) - the complete brief for a new agent.** State, what is left, the
   non-negotiables, the eight-file release bump, the traps, and the first message to send the owner.
1. [`HANDOFF.md`](HANDOFF.md) - orientation, next tasks, traps.
2. [`docs/PROGRESS.md`](docs/PROGRESS.md) - every feature: ID, state, files, test, "done when".
3. [`docs/OWNER-DECISIONS.md`](docs/OWNER-DECISIONS.md) - the owner's decisions (Urdu, authoritative).
4. [`docs/RULES-MATRIX.md`](docs/RULES-MATRIX.md) - his 116 numbered rules and where each lives.
5. [`docs/AREA11-MASTER-SPEC.md`](docs/AREA11-MASTER-SPEC.md) - the blueprint. Sections 3 (rules),
   5 (data), 11 (pricing), 12 (billing), 14 (printing), 15 (returns), 34 (rebuild protocol).
6. [`docs/BUILD-ON-WINDOWS.md`](docs/BUILD-ON-WINDOWS.md) - how the owner builds it.

Then run the checkers (below) and pick the first ⬜ or 🟡 task from `HANDOFF.md` §4.

## 2. Hard rules

0. **Document every change, and change the colour with it.** Two standing orders from the owner:
   (a) anything a future session would need to know goes into this repository - `HANDOFF.md`,
   `docs/PROGRESS.md`, `docs/VERSIONS.md`, the doc comment above the code - so that the app can be
   dropped at any moment and any agent can continue from the link alone;
   (b) every code change ships with a **new brand colour** (`docs/VERSIONS.md`), because the owner
   tells builds apart by colour on his own screen.

These come from the spec (§3, §5) and from bugs already paid for. A change that breaks one of them is
rejected even if it "works".

1. **Money is `long` paisa**; quantities are **`double`** (the schema stores REAL). Never `decimal` -
   Microsoft.Data.Sqlite turns it into TEXT and `SUM()` breaks.
2. **Bill total = sum of line totals.** Round once (`Money.RoundDownWithCostGuard`), then spread the
   difference over the last allocation. Never re-round the total afterwards.
3. **Round down only, never below purchase cost** (Z-11).
4. **Negative stock is retired by D-93 (implemented in v0.35.9 Onyx), superseding U-25/U-30 for new transactions.**
   Every ordinary decrement must use `StockPolicy.Decrease` inside its immediate transaction; never add a raw
   subtraction, settings bypass or owner bypass. Sales reserve repeated products/shared FEFO batches
   cumulatively, exact zero is valid, and failures roll back completely. Historical negatives stay readable
   and are reconciled only through finite physical counts and audited corrections, never silent zeroing.
5. **Permissions are enforced in `Area11.Core`**, keyed on the signed-in `User`. Hiding a button is not
   a permission. Owner-only actions: dismissing alerts, applying stock-take, restoring backup.
6. **Never delete business rows.** Use `active = 0`. Audit rows and sales are permanent.
7. **Migrations are append-only**, run inside `BEGIN IMMEDIATE`, and re-check `_migrations` after the
   lock is taken. Add `027_*.sql`; never edit an existing file.
8. **Views contain no SQL and no business logic.** Services own every rule and every transaction; a
   service method takes `User` when the rule depends on permissions.
9. **Never modify the original Next.js app** (`Dr-Adil-Abdullah/Area11`) - it is only a behaviour
   reference.
10. **Everything editable** (E-01): no hard-coded shop name, discount limit, printer width, wording or
    colour. Settings come from the `settings` table via `SettingsService`.

## 3. Layout you must respect

```
dotnet/Area11.Core/      no UI references - Data, Models, Services, Utils
dotnet/Area11.Desktop/   WPF + MVVM only - Views, ViewModels, Resources, AppServices
dotnet/Area11.Tests/     xUnit; TestDatabase = in-memory SQLite + all migrations + seed
docs/                    spec, decisions, progress, rules matrix, schema, build guide
tools/                   check_repo.py, build_rules_matrix.py
```

Three projects only (owner's D-17). Do not add a fourth.

Wiring a new screen: service → property on `AppServices` → entry in `ShellViewModel.NavItems` guarded
by `_services.Permissions.Can(user, Modules.X)` → `DataTemplate` in `ShellWindow.xaml` → view + view-model.

## 4. Verify before you claim anything works

```bash
python3 tools/check_repo.py              # C# syntax, XAML/XML, bindings+resources, migrations, tests, docs links
python3 tools/build_rules_matrix.py --check
```

`check_repo.py` uses `tree-sitter` when available and falls back to brace balance when not. The agent
sandbox has **no .NET SDK**, so a WPF build can only be done by the owner; never say "it builds" unless
you actually ran `dotnet build` on a machine with the SDK.

## 5. Asking the owner

- Only when a decision truly blocks progress, and never twice about the same thing.
- Ask as **multiple choice**: `سوال / Question:` then `ممکنہ حل / Possible Options:` A/B/C/D, all
  questions in one message, each option a workable solution (not "whatever you prefer").
- Explain the concept in plain Urdu **before** asking; warn about money/accounting risk explicitly.
- Record every answer in `docs/OWNER-DECISIONS.md` as the next `D-xx`, then follow it.

## 6. Definition of done (spec §30.4.1, owner's U-14)

1. `python3 tools/check_repo.py` is clean (or its only complaints are pre-existing and explained).
2. A test covers the rule you touched, or `PROGRESS.md` says why it cannot.
3. `docs/PROGRESS.md` is updated (state, files, test).
4. `tools/build_rules_matrix.py` status map is updated and the matrix regenerated.
5. `HANDOFF.md` §4 still names the real next task.
6. Commit message names the feature: `F-13: receipt + instant ESC/POS print`.
7. Push. The owner's rule U-23: **every change reaches GitHub**, code and documents both.

Never commit `data/`, `*.db`, `*.log`, `bin/`, `obj/` (`.gitignore` already covers them) and never
commit a real shop database.

## 7. House style

- C# naming: `PascalCase` public members, `_camelCase` private fields, no abbreviations.
- Comments explain **what and why**, never how the code works line by line.
- One `try/catch` policy: services catch, log through `LogService`, and return
  `ServiceResult`/`ServiceResult<T>` with a message the cashier can act on. No empty catch, no
  exception text shown raw to the user.
- Prefer a method that already exists. Reuse beats elegance (owner's R-01…R-10).
- Keep UI text in English; keep it short enough for a 58 mm receipt.

## 9. Releasing a version (the colour rule)

The owner tests each build himself and keeps several copies of the folder, so every release gets a
new colour and a version number. Nothing may be shipped without it:

1. next colour from the palette in `docs/VERSIONS.md` (never repeat the previous one);
2. set `Version`, `CodeName`, `BrandHex`, `BrandDarkHex`, `BrandSoftHex`, `ReleasedOn` in
   `dotnet/Area11.Core/Utils/BuildInfo.cs`;
3. paint the same three colours on `BrandBrush` / `BrandDarkBrush` / `BrandSoftBrush` in
   `dotnet/Area11.Desktop/Resources/Theme.xaml`;
4. add the row to `docs/VERSIONS.md`;
5. `python3 tools/check_repo.py` - it fails if the three disagree;
6. commit with the version in the message;
7. **if you touched the feature board** (`docs/PROGRESS.md`), run `python3 tools/make_checklist.py` -
   it rebuilds the owner's printable tick-list (`docs/CHECKLIST-URDU.md` / `.html`) from the board,
   and gate #43 refuses a commit where the two disagree. The owner asked for that page by name
   (D-91): he ticks it with a pen, so a stale mark is worse than a missing one.

**Tests may only use public code.** `check_repo.py` fails if a test names an `internal` type: the
sandbox cannot compile, so a CS0122 would otherwise reach the owner's PC (it did, in v0.2.1). Write
tests through the public service methods and a real bill in `TestDatabase`.

## 8. Current state (update this line if you finish a phase)

> **v0.35.5 (Carmine `#A50021`) - the owner's three red cases are answered.**
>
> * **v0.35.5 repairs one policy mismatch and two stale test fixtures.** The owner's v0.35.4 run proved
>   the correct tree (**769 passed, 3 failed, 772 total**). A sign-in ahead of the PC clock now keeps
>   the session on the safe hard-cap answer rather than the shorter idle countdown. Two Shift tests
>   now open the drawer before billing instead of asking `TestData.Sales()` to open it and then opening
>   it again; the production second-open refusal is unchanged. No schema or test-count change (772).
>   Static checks pass, and the owner's Windows run then passed **772 of 772 in 56 s**.
>
> * **The old AlertService mapping report did not reproduce against current code.** `qty_base` is
>   SQLite `REAL`, `ProblemRow.Qty` is C# `double`, and all seven alert sources have passing tests in
>   that 772-case run (including low-stock REAL values 1.0 and 2.0). No cast or production file was
>   changed. Do not "fix" it to `long`; a fresh v0.35.5+ crash report is needed if it recurs.
>
> * **v0.35.4 fixes the delivery gate, not a shop rule.** `START-AREA11.bat` accepted only `main` and
>   a retired Arena branch, so it refused the correct current branch before building. The active
>   `WORK_BRANCH` is now declared once and accepted beside `main`; other branches remain refused.
>   No schema or test-count change (772 cases). It has not been compiled or executed here.
>
> * **v0.35.3 repairs cross-instance numbering and rollback.** `NumberingService` reads the counter on
>   the caller's connection; `WriteCounter` no longer mutates memory that SQLite cannot roll back.
>   Three public-service tests cover independent settings instances, rollback and repeated numbers in
>   one transaction (772 cases). The schema did not change. **Not compiled or executed here:** the
>   owner must run `tools\RUN-TESTS.cmd` and see `ALL GREEN - 772 of 772`.
>
> * **v0.35.2 fixed what the owner's run found.** (1) `RunTrail.Append` used `File.AppendAllText`, which
>   writes **no line end**, so every step glued onto the previous one and the trail was one enormous
>   line - four `RunTrailTests` failed on his PC and were right. It now uses `AppendAllLines`.
>   (2) The installer stopped after `Print spooler: running.` with *"Parameter set cannot be resolved
>   using the specified named parameters"*: the icon and **Add or remove programs** blocks are now
>   plain .NET (`Directory.EnumerateFiles`, `Registry.LocalMachine.CreateSubKey`), each in a `try/catch`
>   that says what it could not write and carries on, and the top-level catch prints **`Where : line N`**.
>   Both rules are in `check_repo.py`, each proved by putting the fault back.
> (3) **Tarragon is a fresh codename**: v0.35.1 repeated *Ultramarine*, which v0.5.0 had used in
>   2026 - a codename is a version marker, so re-using one makes two different builds look alike.
>
> * **v0.35.1 fixed the first line that did not compile.** `RunTrail.Begin` built its `Folder :` header
>   with `Safe(AppContext.BaseDirectory)` while `Safe` takes a `Func<string>` - CS1503, and the owner's
>   build stopped there. **Why it escaped:** v0.34.0 was never compiled here, and the static checker
>   resolves member *names*, not argument *types*. The checker now refuses any `Safe(` in `RunTrail`
>   that is not handed a lambda, and the fault was put back once to watch it fail. The lesson for every
>   later round: when a helper takes a delegate, call it with a lambda - and never assume a v-number
>   was compiled just because the checker is green.
>
> * **The time is taken, not assumed.** `TimeSource` asks a time server on a background thread
>   **before the first window** (1,500 ms, three servers), keeps one correction
>   (`online - computer`), and `Clock.Now()` applies it - so every bill, audit line and shift stamp
>   follows one answer, and no service had to learn about the internet. No answer means this PC's
>   clock, the question is repeated **every 20 minutes**, and an answer more than **two days** out is
>   refused as a wrong answer rather than drift.
> * **The 12-hour ceiling is not a setting.** `SessionPolicy` holds the owner's idle choice from
>   Settings *and* the cap; the frame checks both every 30 seconds; a click or key press postpones
>   the **idle** clock only; the last 15 minutes are named in the top bar **with the clock that will
>   fire**. A session that ends this way closes the frame and shows the sign-in window.
> * **No bill without a drawer.** `SaleService.Create` refuses without an open shift, so no page -
>   present or future - can save one. The counter shows a card **over** its page (the cart survives)
>   with **Open a galla now**. A drawer left open by a forgotten session is **never closed by the
>   program**: a note (`SessionHandover`) blocks the counter until a person counts it, because only a
>   counted amount may close a drawer.
> * **A test that bills opens the drawer.** `TestData.EnsureDrawerOpen` (called by `TestData.Sales`
>   and `SaleTests`) opens a zero-float shift, so every bill in the suite goes through the real door.
>
> **v0.34.0 (Smalt `#003399`) - a heartbeat, and a button that cannot take the counter down.**
>
> * **An absence is not evidence.** `%TEMP%\Area11\binding-errors.txt` is written only when WPF
>   raises a real binding complaint, so its absence means *no complaint* - and a run that never
>   reached the window leaves the identical empty folder. On 23 Oct 2026 that ambiguity cost the owner
>   a false alarm. An absence is only evidence when its cause is knowable, so the program now writes
>   **`%TEMP%\Area11\last-run.txt` on every start**, before the first window, and adds one line per
>   named step. *No file = it never started*; the last line says how far it got.
> * **Diagnostics never live in the shop folder.** The trail goes to the temp folder, because the shop
>   folder can be the second counter's SMB share and can be off. The same rule as `BindingWatch` and
>   the crash pointer (D-62, D-78).
> * **The heartbeat must not become the crash.** Every write is wrapped in one unfiltered catch and
>   the file is capped (`MaxLines = 140`, keep the header and the tail, print how many lines were
>   dropped). Nothing here may throw: a heartbeat that stops the till is worse than no heartbeat.
> * **A failed button belongs to the button.** `RelayCommand` used to let the exception reach the
>   dispatcher, which replaced the **whole page** - a failed Save took the cart with it. Both command
>   kinds now run through `CommandGuard.Run`, which catches, writes the crash report through the same
>   `CrashLog`, logs one line, and raises an outcome the frame turns into a banner **above** the page.
>   The page is not rebuilt.
> * **Never guess about the data.** The banner says which button failed, that the page is still open
>   and that the rest of the shop is untouched, and it names the report file. It must **not** say
>   "nothing was saved": the command may have written half its work before it failed, and a sentence
>   that guesses is worse than one that admits what it knows. The reason is written next to the
>   sentence in `CommandGuard`.
> * **A machine name is not a button name.** The compiler calls a lambda in a constructor
>   `<.ctor>b__12_0`; `NameOf` maps that to `"button"` and `HasName` lets the sentence say **"A button
>   on this page"** rather than repeating a name no shopkeeper has ever seen.
> * **The report tool answers the question itself.** `tools\support-report.ps1` prints the trail and
>   states, in words, that a missing binding file is the healthy answer - and that it means nothing
>   until the trail proves the program started.
> * **Gates.** **#42** checks the trail name, the temp folder, the unfiltered catch, the cap, the
>   heartbeat before `app.InitializeComponent()`, a step from the frame, the exit line, both command
>   kinds, the crash report, the subscribe *and* unsubscribe, the banner bindings, the log sink and the
>   report's three sentences; a rule inside the VERSIONS gate checks that the released table is **one**
>   table. Both were proved by **seven injections** (heartbeat deleted, heartbeat moved after the
>   window, one command kind unguarded, unsubscribe removed, the report's healthy-answer sentence
>   changed, a paragraph pushed into the version table, the notes section put back above the rows).
>   A check that only counts injections proves nothing; each one was expected to fail with its own
>   message.
>
> **v0.33.0 (Mauveine `#A050A0`) - an empty page must say why, and a page that dies while drawing must say so.**
>
> * **A blank grid is not an answer.** "Nothing here yet" and "nothing matches what you typed" call
>   for two different actions - add stock, or widen the search - and one generic sentence for both is
>   how a shopkeeper concludes the program lost his data. Every page the menu can open now implements
>   `IEmptyPage`, whose hint is **computed from the page's own list** (`EmptyPage.When(rows == 0,
>   "Products", searched: …)`), so no page carries a second flag that can drift from the list it
>   describes.
> * **The sentence lives with the page, the card lives with the frame.** `EmptyPage` holds one line
>   per page (what would *fill* it); the shell draws one card under the current page and re-asks on
>   every property change of that page. One card in `ShellWindow` instead of a copy in twenty-six views
>   is one chance to forget, not twenty-six. A container (the customers page) **delegates**: it asks
>   the open tab ('empty-page: delegated'), so a tab never inherits the container's words.
> * **A picker is not a search.** On the four pages where the search narrows the visible list (Counter,
>   Products, Bills, Black box) an empty result says "nothing matches"; on the pages where the box
>   picks a record to act on (a bill to return, a medicine to write off) the page deliberately does not
>   pass the flag - saying "nothing matches" there would be false. The gate encodes exactly those four.
> * **A page can refuse in writing.** Two pages have no empty state - the fault card, and the container
>   whose tabs carry their own sentences - and they must say so with `// empty-page: none - <why>`.
>   The reason is mandatory: an exemption without a reason is how a page goes silent.
> * **A page can die after it is built.** `PageGuard.Open` covers the constructor; a template, a
>   converter or a binding that throws arrives later, on the dispatcher, and used to leave a half-drawn
>   screen behind a crash file. `PageGuard.Drawing` turns that into the same card with a **different
>   sentence** - "stopped while its screen was being drawn" - because the data was read and the drawing
>   is what failed. The frame remembers which page is on the screen, which is the only place that knows.
> * **Gates #40 and #41** were proved by **nine injections**: a page's interface removed, a hint's
>   title changed, a `searched:` flag dropped, the card unbound in `ShellWindow`, a generic "No data"
>   sentence, the dispatcher's call removed, the drawing fault stripped of its log and report, an
>   exemption written without a real reason, and a sentence no page uses.
>
> **v0.32.0 (Amethyst `#69359C`) - a broken page must not take the till down.**
>
> * **A WPF property setter is the UI thread.** `SelectedNav = row` built the page's view-model
>   inside the setter; every one of those constructors reads the database. One failed read - a bad
>   row, a table missing from an old copy of the file - threw out of the setter into the dispatcher
>   while the shell was still being built, and the window died. That is the same shape as the 22 Oct
>   crash, but at sign-in: the shop would have lost the counter, not a screen. Every page is now
>   built through **`PageGuard.Open`**, which returns either the page or a `PageFault`.
> * **The guard must never be the second fault.** `Open` catches `Exception` (deliberately broad -
>   after an `OutOfMemoryException` the only useful thing left is a window that is still open), logs
>   it, offers it to the caller's recorder, and returns. The log call and the recorder are wrapped in
>   their **own** unfiltered catches - a recorder that fails in an unpredicted way used to escape and
>   become a second crash - and `Open` itself is a thin net around the whole body.
> * **Say that the rest of the shop still works.** The card's first line is *"Nothing else is
>   affected"*, because an operator who believes the till is broken closes the program and loses the
>   day's billing - the sentence is the feature, not the decoration. It also names the page, keeps
>   the exception's own words, and says to press another page and try again (a read that failed once
>   often works on the second try) and to run `tools\SEND-REPORT.cmd` if it fails twice.
> * **A path must be read, not typed.** The first draft of the card's last line escaped a Windows
>   path twice in the source, so the screen showed doubled separators. The wording now lives in Core
>   (`PageFault.ReportLabel`) where a test asserts no doubled backslash and that the tool's name is
>   printed - a path a shopkeeper cannot copy is a path he cannot use.
> * **Gate #40** fails if the unguarded `= value?.Resolve()` returns, if fewer than three pages are
>   guarded, if the log/recorder catches are narrowed to file errors, if the card or its
>   `DataTemplate` disappears, or if the reassuring sentence and the report path leave the card. It
>   was proved twice by breaking the program on purpose.
>
> **v0.31.2 (Bistre `#584204`) - the evidence tool broke, and the quiet bindings now leave a file.**
>
> * **A tool that fails while collecting evidence must say so IN the evidence.** `SEND-REPORT.cmd`
>   printed a red `Cannot bind argument to parameter 'LiteralPath' because it is null` and then wrote
>   a clean-looking report: the owner would have sent a document with a silent gap. Every error the
>   script raises is now a section of the report - a fault in the tool is not in the shop, but it is
>   in the file.
> * **A holder computed in two places ends up computed in neither.** v0.31.1 moved the crash
>   section and took its local `$logsFolder` with it; the log section 40 lines below still used it.
>   Path holders are now computed once, before anything uses them, and gate #39 fails if any
>   `$variable` reaches a path parameter without being assigned earlier.
> * **The quiet half of the same fault.** A binding to a property that does not exist never throws -
>   WPF writes one line to the debug output, which no shop PC shows, and leaves an empty box. The
>   loud half (TwoWay to a read-only label) killed the window on 22 Oct; the quiet half is what the
>   owner reports as "the page has no data". `BindingWatch` now keeps those lines, timestamped, in
>   `%TEMP%\Area11\binding-errors.txt` - never the shop folder, which can be the second counter's
>   share - clipped to 600 characters, rotated past 200 KB so the **first** complaint is never the
>   one lost, and it can never throw. The crash report names the file, because the two are read
>   together.
> * **Install a diagnostic before the thing it watches.** The listener is attached before
>   `InitializeComponent`, so Theme.xaml's own bindings are covered - gate #39 fails if the call
>   moves below it.
>
> **v0.31.1 (Boysenberry `#8A0A6C`) - the first time the window ran, and why 171 bindings could only crash.**
>
> * **`Run.Text` and every `DataGrid...Column.Binding` are TwoWay by default.** The framework
>   registers `Run.Text` with `BindsTwoWayByDefault`; a grid column generates its editing element
>   with the same binding. Point one at a **computed label** (`QtyLabel`, `StockLabel`,
>   `StatusLabel`) and WPF throws *"A TwoWay or OneWayToSource binding cannot work on the read-only
>   property"* inside the layout pass - which arrives as a `XamlParseException` and closes the
>   window. Nothing in a C# build can see it: the fault exists only when a template is created.
>   **171 bindings** here were in that state. Read-only display targets get `Mode=OneWay`, always.
> * **A gate that cannot know the row type guesses, and a guess cries wolf.** The first version of
>   #37 accused the writable `Code` of a bill row because an unrelated class computed a `Code`.
>   It now walks the XAML with a **scope stack** (DataGrid / ItemsControl / ListBox / ComboBox) and
>   resolves the property against the class the row really is; only when that cannot be worked out
>   does it fall back to "read-only everywhere".
> * **A failure screen that shows an old crash file is worse than one that shows nothing.** On
>   23 Oct the launcher printed a report from the day before, written by another version, for a
>   fault already repaired - and the real report (written to `%TEMP%\Area11` because the shop
>   folder was not writable) was never seen. The launcher now writes a **marker before starting**,
>   the program records the **path of its own newest report**, and only a report newer than the
>   marker is printed; otherwise the screen says so and names the old file *with its date*, without
>   printing it. **The tool must never tell a story the evidence does not support.**
>
> **v0.31.0 (Loden `#244506`) - who was short, and the total that must never be netted.**
>
> * **A spec line that says "not built" is a debt.** Spec 7.2.7 said *"a per-user shift report is
>   also not built"* and it stayed true for forty versions, because the feature board never had a row
>   for it. A gap that lives only in the specification has no owner - so when the board is green,
>   read the spec's own list of what is missing, not only the board.
> * **Netting two differences destroys both.** A shop short Rs 500 in one count and over Rs 200 in
>   another is **"500 short, 200 over"**, never "300 short". The first version of `ByPerson` summed
>   the per-person nets and produced exactly the wrong number; a SQLite file built from the 23
>   migrations showed it in one run (`1500 / 200` versus `1300 / 0`). Totals of **missing** and
>   **too much** come from the raw rows, and a person's own row keeps their net - *where this person
>   stands* and *how much the shop lost* are two questions with two answers.
> * **A window is a claim; say what it excludes.** An open drawer has no counted number, so it is
>   not in the report: calling it "exact" would be a lie in the shop's favour, which is the most
>   expensive kind.
> * **One sentence, three copies.** "exact / over by Rs 50 / short by Rs 120" is read on the shift
>   page, the handover slip and now this table. It lives in `ShiftWords` - three copies would drift,
>   and the copy that drifted would be the one the owner trusted.
> * **A hidden card and a refused call must agree.** The view-model hides the table for a cashier
>   and the service refuses the same person; both ask `CanManage`, so the screen can never show a
>   page that then errors, and the service can never be reached around the page.
>
> **v0.30.9 (Oxblood `#4A0E0E`) - 686 of 686 green, and the runner that could not say so.**
>
> * **The whole suite passes on the shop PC.** 686 of 686, failed: 0, 51 s - including the 26 tests
>   for the books check and the handover that had never been compiled before v0.30.6. That number is
>   now the floor: `EXPECTED` in `tools\RUN-TESTS.cmd` must equal it, and the checker compares them.
> * **The tool told the owner the run failed.** `RUN-TESTS.cmd` printed *"Not OK is not a disaster:
>   send the lines above"* after that perfect run, because the sentence was in the main flow and not
>   behind a branch. **A message that appears on both outcomes carries no information**, and the next
>   red run would have been read as noise. The verdict now comes from three things, in order: the
>   exit code, the word `Failed: 0`, and the total.
> * **Three outcomes, because there are three:** every case passed; every case passed but the folder
>   is old (`GREEN, BUT THIS IS AN OLD FOLDER`, exit 1 - a green run of yesterday's code proves
>   nothing); something failed. The middle one is the one a reader would otherwise trust by mistake.
> * **`set /p` on a CRLF file can leave a carriage return in the value**, and `686<CR>` makes
>   `if %N% LSS %EXPECTED%` a syntax error, not a comparison. The number is written with no trailing
>   line break and a numeric guard drops anything that is not a plain number.
> * **Name the project that broke.** MSBuild ends each error line with its project in brackets, so a
>   failed build prints *"The fault is in this project: Area11.Desktop"*. When the shop's rules compile
>   and only the window fails, that single line is the whole diagnosis.
> * **Both gates were proved by breaking them** (#34 the launcher's process name, #35 the runner's
>   verdict): the sentence was moved back above its label, the checker failed with the exact message,
>   and restoring the file turned the tree green.
>
> **v0.30.8 (Zaffre `#0818A8`) - the build that failed because the program was open.**
>
> * **MSB3027 is not a compiler error.** *"Failed. The file is locked by: Area11 (8460)"* means
>   Windows refused to overwrite a DLL that a running program had loaded - the shop screen from the
>   previous run, still open behind the launcher. No change to the code can fix it, and the same four
>   lines would greet the owner after **every** update he installs with the program still running.
> * **A launcher that cannot replace the program must first close it - and ask.** Step 5 of 6 reads
>   the process list, names the process, offers *Y/N*, ends it, and waits about two seconds before
>   building, because Windows releases handles a moment after the process dies. **N** stops cleanly:
>   killing a window with work in it is the owner's decision, never the script's.
> * **Say the true thing on the failure screen too.** A copy can be left open by another Windows user,
>   or a file held by something else, so the guard can lose. When it does, MSB3027/MSB3021 now print
>   *"the program is still open - this is not a code error"* and three steps, not forty lines of MSBuild.
> * **A guard looking for the wrong process is no guard.** Check #34 reads `<AssemblyName>` out of
>   `Area11.Desktop.csproj` and fails if the batch file looks for a different exe - the exact failure a
>   rename would cause, silently. It also fails if the script stops asking (`tasklist`), stops closing
>   (`taskkill`) or stops recognising either error code. Proved by pointing the script at
>   `Area11.Desktop.exe`: the checker failed by name, and the tree went green when it was restored.
> * **The failure lives between the compiler and the shop.** A gate that reads only C# can never see
>   it; this one reads the project file and the batch file together, which is why it can.
>
> **v0.30.7 (Bottle `#0A4626`) - the first real run, and what it says about a test written without a compiler.**
>
> * **A test you never ran is a guess, not a proof.** 26 tests for the books check and the handover were
>   written in this sandbox and marked done; on their first real run **six failed**. Three were the
>   *tests* (a purchase test built three units and expected two units' numbers; a CSV test wanted
>   `5 missing` where the screen says `Rs 5 missing`; a handover test asked for one `shift_open` row
>   when there are two). A red line does not mean the program is wrong - it means *something* is wrong,
>   and the first job is to find out which side.
> * **Three were the program, and they were one fault: the boundary second.** v0.30.6 gave the second
>   of a handover to the NEW shift. A bill rung in that second was then counted by the new shift even
>   though its cash was already inside the counted float - **Rs 300 invented** - and the same rows
>   reported 800 or 500 depending on whether a sale and a handover landed in the same second. A money
>   rule that depends on the clock is not a rule.
> * **The fix is decided by the row, not by the caller.** Only a shift that carries
>   `opened_from_shift_id` excludes its own opening second; every other window is untouched, so a
>   handover reports exactly what a plain close would for the same rows. A flag passed by the caller
>   would have left the page, the close button and the slip free to disagree about the same shift.
> * **A test must not be decided by the clock.** A suite runs in milliseconds, so every row lands in
>   the same second and a boundary rule proves nothing. One test now pins the stamps to -20/-15/-10/-5
>   seconds; another needs no pinning because the ordering it asserts is true whatever the seconds.
> * **Correct the record, do not bury it.** The v0.30.6 rows in `VERSIONS.md`, `PROGRESS.md` and D-81
>   described the wrong window. They now describe what the code actually does, with the correction
>   named, because the owner reads these files as the shop's memory.
>
> **v0.30.6 (Damson `#70266E`) - the drawer changes hands, and the board stops lying to itself.**
>
> * **A gap can live inside a ✅ row.** F-31 has been marked done since v0.4.0 and said, in the same
>   cell, *"handover still to come with a second counter"*. Twenty-six versions went by and nobody
>   read that clause again, because the tick answered the question before anyone asked it. **When a
>   row is marked done, its own text must not contain a promise.** The search for this release was
>   `still to come` / `not built` / `not yet` across the docs - and the only real hit was the one
>   feature the schema had reserved three columns for since migration 009.
> * **A handover is one transaction, not two.** Closing the drawer and opening the next one in
>   separate steps can leave the money closed and unwritten, or reopened with a typed float that
>   quietly erases the difference. Here the **counted figure is the next float**, written by the same
>   transaction that closes the first shift.
> * **The difference belongs to the shift that produced it.** If the first cashier was Rs 50 short,
>   that shortfall is booked against him, and the person taking over starts clean at the counted
>   figure. Opening the next shift at the *expected* figure would move the loss into the next
>   person's day - and it would never be seen again.
> * **One second can hold both a bill and a handover.** A SQLite stamp here has no fraction. v0.30.6
>   first gave that second to the new shift (`[opened, at)` / `[at, now]`) and v0.30.7 corrected it:
>   the closing shift keeps `[opened, at]` - the window a plain close uses - and a shift opened by a
>   handover counts `(at, now]`. A bill in that second belongs to the closing shift, and to nobody
>   twice. **See the v0.30.7 block: the first version of this bullet was wrong, and only a real run
>   could show it.**
> * **`_` is part of a name.** The checker split the select list at the word `FROM` and therefore
>   read the column `opened_from_shift_id` as the keyword `FROM`, then reported four healthy queries
>   as broken. A gate that fails on good code is worse than no gate: it teaches the owner that red
>   can be ignored.
>
> **v0.30.5 (Sable `#2E2118`) - read the C# with a real grammar, and resolve every static name.**
>
> * **Brace counting is not a syntax check.** It says the `{` and `}` match; it says nothing about a
>   misplaced `else`, a broken lambda or a `switch` arm without its arrow - which is exactly what a
>   first compile reports. `check_csharp_grammar` parses all 239 files with **tree-sitter-c-sharp**
>   and fails with a line number. It is still not a compiler: it knows nothing about types.
> * **`Type.Member` is checkable; `variable.Member` is not.** 2,021 calls and 3,325 references are
>   resolved against the declaring type for names that belong to exactly one top-level type here.
>   Guessing the type of a local (`db.Query<T>`) is the regex-inference dead end - refused again.
> * **Two bugs in the rule itself, and both would have cried wolf.** Picking the first identifier of
>   a method declaration records the RETURN type as the method name (`StoragePlan For(...)` became a
>   method called `StoragePlan`); a `params` or defaulted parameter was counted as required, which
>   invented 580 faults. Both were found by running the rule on the healthy tree before trusting it.
>   **A gate is finished only when it reports zero on code that is known good AND fails on code that
>   is known bad** - this one was checked in both directions.
> * **A name in the file beats a name in the project.** `Modules.Clear()` in a view model is a
>   collection property, not the static `Modules` class; the rule skips any type name that this file
>   also declares as a property, field, parameter or local.
> * **A rupee counted twice is a lie about money.** The books check counts **per document, not per
>   rule**: one broken bill is three findings. Totals are per document and "missing" and "too much"
>   stay apart, because they cancel each other out otherwise.
>
> **v0.30.4 (Mazarine `#1B2A66`) - the books can be checked without being touched (F-54).**
>
> * **A total that disagrees with its own rows is invisible from every other page.** The counter, the
>   report and the receipt all read the same number, so they agree with each other. `BookCheckService`
>   compares each document with the rows underneath it, and the page prints the document, what it
>   says, what its rows say, and the gap in words. **This is the class of fault the owner's "no error
>   may remain" order is really about**, and it is why the check exists at all.
> * **A rule must be an identity, not a second opinion.** Every rule is something this program
>   guarantees by construction (lines → total, header → total, paid + due → total, money rows → paid,
>   khata ≥ 0). Two rules were deliberately **not** written - stock vs batches, and supplier balances -
>   because the program legally leaves those open (negative shelves, cash-refunded supplier returns).
>   **A check that cries wolf teaches the shop to ignore the next alarm**, which is worse than silence.
> * **A read-only page needs no confirmation and no permission of its own** - it borrows the Reports /
>   Black-box permission, and `BookCheckService.CanRun` is the one place that decides, so a menu row
>   can never point at a page that refuses to open.
> * **A check that repairs is not a check.** The service has no UPDATE and no recalculation, one test
>   runs it twice to prove the fault is still there, and the page's last line says so. The owner
>   decides what a difference means; the black box keeps every attempt.
> * **Prove the gate on the page you just added.** The binding rule was shown failing on
>   `BookCheckView.xaml` (`HeadLine`) before it went green - a new page is exactly where a new gate is
>   worth nothing until it has been seen to fail.
>
> **v0.30.3 (Cocoa `#4B2E20`) - the first build must be readable in one paste.**
>
> * **A console that prints 300 lines is a measurement the owner cannot take.** The one line that
>   names a fault (`Something.cs(12,34): error CS1234`) scrolled past, and he was asked to find it by
>   eye. `START-AREA11.bat` now writes `build.log` and prints **only the error lines**;
>   `tools\RUN-TESTS.cmd` writes `test.log` and prints the result line, the failed test names and the
>   compile errors. **The thing being measured must be the thing on the screen.**
> * **`tools\SEND-REPORT.cmd` is the new first question.** One file: build, `dotnet --info`, git
>   state, project files, shop folder (names, sizes, dates - **no rows, never the database**), the
>   whole newest crash report, the log tail, the last build and test output. It only reads. When he
>   reports anything, ask for that file before asking for a screenshot; it is also why the launcher
>   and the runner keep their logs.
> * **A third script means a third banner to keep.** The version literal is now in three files and
>   `SCRIPTS_THAT_NAME_THE_BUILD` in `check_repo.py` is the list - the rule caught this version's own
>   half-finished bump, which is the only proof worth having that a checker works.
> * **The batch files got a compiler too (`check_scripts`, the 31st check).** They have none: cmd.exe
>   reads line by line, so a missing `)` swallows the rest of the file and an unescaped `)` inside an
>   `echo` inside a block ends the block early - the launcher prints half a menu and stops with a
>   message nobody wrote. A gate is only real once it has been seen failing: two faults were injected
>   into the new script and both were named before it went green again.
> * **`tools\diagnose.bat` stays** as the no-PowerShell fallback for a PC where script execution is
>   blocked; two tools for one question is a cost, and this is the reason it is paid.
>
> **v0.30.2 (Auburn `#8B3A3A`) - the demo is deleted, and deletion has companions.**
>
> * **The owner's word was the trigger D-76 was waiting for.** *"Delete whatever demo data there is -
>   now we are trying to make an app that will be completely real."* Nine files went, and the value of
>   a demo is exactly what it costs to remove: every switch that made it possible (a second data root,
>   a `shop.ini` exception, a title branch, a receipt watermark) had to be unpicked, not just the page.
> * **Deleting a view is four edits, not one.** The file, its `_VIEW_OF` entry, its `{x:Type}` template
>   and its `DataTemplate` - `check_repo.py` names whichever one was left behind. Same for a test file
>   and `RUN-TESTS.cmd`'s `EXPECTED`. **A deletion is not done until the checker is green.**
> * **A feature that the owner confirms by doing it is ✅.** F-13 (receipt + instant print) stood 🟡 for
>   forty versions; the owner printed on the shop printer and said *"it is working perfectly"*. Written
>   proof is his sentence, not a test. F-46 became half-done in the same message: the printer is real,
>   the **scanner** is still only a promise.
> * **A retired feature stays in the table.** F-53 keeps its row, its ❌-free history and its 🧪 column,
>   marked *(removed)*, so a later reader cannot mistake a deleted feature for one that was never built.
>
> **v0.30.1 (Citrine `#A98B18`) - 696/696 on the shop PC, and two faults the green run exposed.**
>
> * **A banner must not parse anything.** v0.30.0 read the version out of `BuildInfo.cs` with
>   `findstr` and a token split; on the owner's console it printed `vunknown (unknown)`. The three
>   double-click scripts now hold one literal `set "BUILD=..."`, and `check_repo.py` fails if it and
>   `BuildInfo.cs` disagree. If the version bumps, the checker is the thing that remembers.
> * **A rule the counter shows must be the rule the save applies.** `Quote` accepted a tender smaller
>   than the cash part and showed change 0; only `Create` refused it, after the button. The quote now
>   carries the same sentence as a **warning** while typing, and the total stays on screen.
> * **Run 22 (v0.30.0) was the first complete green run**: 696 total, 0 failed, 74.3 s. Core + Tests
>   build and pass on Windows; the Desktop project itself first compiled on 23 Oct 2026 (v0.30.9,
>   `BUILD OK`) and v0.31.1 repairs the binding fault that run exposed.
>
> **v0.30.0 (Mauve `#7A5C7B`) - the page map is a gate too: `{x:Type}`, the template, and the view in it.**
>
> * **A shell page is three names that must agree:** `new XViewModel(...)` in `ShellViewModel`, a
>   `<DataTemplate DataType="{x:Type vm:XViewModel}">` in `ShellWindow.xaml`, and `<views:XView />`
>   inside it. A typo in any of the three either closes the program before the sign-in window
>   (`{x:Type}`) or opens an empty frame, or - worst - opens the **wrong screen under the right menu
>   name** with no error at all. `check_templates` now holds all three.
> * **Suppress the cascade.** A misspelt type also makes its page "have no template"; printing both
>   sends the reader hunting for a template that is right there. One root cause, one line.
>
> **v0.29.9 (Quartz `#6E7B8B`) - a binding is checked against ITS page, not against the whole program.**
>
> * **A pool of "every property anywhere" is not a check.** `{Binding BalancePaisa}` in a grid of
>   medicines passed for two years of versions because a customer model owned that name. The legal
>   names are now the page's view-model plus the element types its `ItemsSource` collections carry
>   (81 row types resolved), and all 1,278 bindings still resolve - so the rule got stricter and
>   stayed silent, which is the only shape worth shipping.
> * **Match braces, not `[^}]+`.** A binding that contains another markup extension
>   (`RelativeSource={RelativeSource ...}`) was cut at the first `}`, and the tail swallowed the
>   next binding - `CanFoldMenu` was being read as `xt.CanFoldMenu` and skipped.
> * **A 🟡 row is not a missing feature.** All 51 are written; the nine that stay yellow are the ones
>   only the shop PC can prove. See "Where we stand" in `docs/PROGRESS.md` before starting new work.
>
> **v0.29.8 (Steel `#4A6FA5`) - the wiring gate, and a gate that cried wolf before it was allowed to stay.**
>
> * **`check_constructor_arity` now reads every project.** It collected declarations from
>   `Area11.Core` alone, so a Desktop row class (`new QuarantineRow(item, Decide)`) was scanned, found
>   no declaration, and passed in silence. 1,144 calls today.
> * **`_services.X` is checked too** (`check_service_members`): properties and methods of `AppServices`,
>   238 references. `SignIn`/`SignOut` are methods - a property-only rule would have refused them.
> * **A gate that cries wolf must be fixed the same hour.** The first run of the widened rule reported a
>   correct `new NavItem(title, subtitle, () => new View(a, b))` as four arguments, because the argument
>   splitter counted the `>` of `=>` as a closing angle bracket. It now treats `<` as a generic only
>   after a name and before a type.
> * **The double-click files print the build they are about to run**, read from `BuildInfo.cs`:
>   `RUN-TESTS.cmd` says *v0.29.8 (Steel) … expect total: 696*, and both launch scripts print
>   *Code on this PC*. A pull that failed is visible instead of silent - the owner has twice tested a
>   tree that was not pulled and read the result as a fault in the shop. `set "EXPECTED=…"` is kept
>   equal to the real case count by `check_repo.py`.
>
> **v0.29.7 (Coral `#C2554A`) - the column rule reads the tests, so a test's row class is guarded too.**
>
> * **851 columns became 871 over 101 files.** A test fills its own small row class from a query; a
>   column that reaches no property is dropped by Dapper and stays 0, and the assertion then blames the
>   program. That was the known blind spot of v0.29.6, now closed rather than described.
> * **A gate's reach should be printed, not implied.** The line now says how many files it read, because
>   "851 columns checked" was equally true when it read one folder.
>
> **v0.29.6 (Grape `#6D28D9`) - the SQL gate was reading half the SQL, and two of my tests were wrong.**
>
> * **A literal's closing quote is not the next literal's opening.** The plain-string scan treated the
>   closing `"` of a verbatim or interpolated string as the start of the next one, so from the first
>   `$@"..."` in a file onwards every following pair was read off by one and its SQL was silently
>   treated as prose. Nothing reported it; the count was simply low. Interpolated literals are now
>   blanked before the plain pass and verbatim ones before both - **327 statements became 711**.
> * **The tests are inside the SQL gate.** A test that throws in its first line reports a *program*
>   fault to the owner, and `INSERT INTO companies(name, created_at)` (no such column) sat in
>   `SampleShopTests` from v0.29.1 to v0.29.5 without ever running.
> * **A statement built by pasting a variable is a fragment, not a statement.** Ones ending on `FROM`,
>   `=`, `(` or a comma are counted and skipped instead of being prepared and reported as faults.
> * **The column-mapping rule reads the tests too as of v0.29.7** (871 columns, 101 files). What it still
>   cannot see: a row class created outside the folder it scans, and a query built at run time.
> * **When a page gains a line, look for the test that counts the lines.** `Assert.Equal(13,
>   report.Steps.Count)` was the only thing v0.29.5 broke by adding a step.
>
> **v0.29.5 (Sapphire `#1B4B8F`) - the demo shop keeps a black box too, and a count found it, not a page.**
>
> * **When a page looks empty in the demo, count the tables the sample writes.** 28 of 34 were written;
>   `audit_logs` was one of the six that were not, so the Black-box page held a single line in a demo that
>   exists to prove the pages work. The trail is now ~900 lines over ten modules.
> * **Copy the module and action names from the services that really write them.** The page's two
>   drop-downs are built from the values already in the table, so an invented vocabulary gives filters
>   that filter nothing - worse than an empty page, because it looks like it works.
> * **The SQL rule reads every string literal.** A details string beginning with the word *with* was
>   prepared as a `WITH` query and reported as *incomplete input*; the sentence was rewritten.
>
> **v0.29.4 (Saffron `#8A6508`) - a gate for method names on services, so CS1061 stops reaching the shop PC.**
>
> * **`_services.Prop.Method(` and `_field.Method(` are now resolved** against the real classes (rule 1i).
>   The holder `_services` is read **only** through `AppServices`'s own property declarations; every other
>   service field is read from its declaration. Wrong name, wrong line, wrong class: `check_repo.py`
>   says `file:line ... is not a method of `X` (CS1061 - the build stops here)`.
> * **Prove every new gate by breaking it on purpose.** The first two drafts of this rule were wrong -
>   one resolved 0 calls, the other flagged all 235 real ones. A gate nobody has seen fail is not a gate.
> * **Write the two shapes, not everything.** A pattern broad enough to catch `_db.Query(...)` catches the
>   whole tree and teaches people to ignore the output.
>
> **v0.29.3 (Cerulean `#14607A`) - a gate for XAML, and one limit of the checker written down.**
>
> * **`{x:Static}` is parsed at window creation, so it needs a gate like `{StaticResource}`.** A misspelt
>   member builds cleanly and throws before the sign-in window. `check_repo.py` now resolves prefix →
>   namespace → class → member against the source; prove any new gate by breaking it on purpose first.
> * **Method names on services are checked (v0.29.4).** `Backup.AutomaticIfDue()` (the real name is
>   `RunAutomaticIfDue`) is now a rule failure with its file and line, not something a reader has to spot.
>   What the checker still cannot see: a method name on a **non-service** type (`_db.Query`, a model), an
>   overload mismatch, and a `Type?` in a generic argument. For those, read the declaration.
> * **Failures before the first window must land in `crash-*.txt`.** Anything done in `Program.Main`
>   goes inside the try, including the demo switch, or the owner gets a window-less silent exit.
>
> **v0.29.2 (Petrol `#1F4E5A`) - the demo is a separate shop, and the tests learned three rules.**
> `DEMO-AREA11.cmd` starts the program with `--demo`: everything (database, photos, logs, backups)
> moves to `%ProgramData%\Area11\demo`, `shop.ini` is ignored, LAN and cloud sync are hidden, and the
> title says DEMO. Deleting the demo is deleting one folder - that is the only way a demo can be
> removed without touching a single real bill (D-76). Three more lessons from the owner's PC, all of
> them my own tests being wrong about the shop:
>
> * **`discount.mode` ships as `margin`** (Spec 11.2.1). 10 % off a 600 bill whose goods cost 400 is
>   20, not 60. Never write a discount expectation without naming the method and the margin.
> * **Sales stay gross; profit nets the returns** (21.2 vs 21.6). The Reports page keeps *sales* and
>   *refunds* as two cards. If the home screen netted them, one day would show two different numbers.
> * **`SetMany` must receive typed values.** For `expiry.levels` the caller passes `List<ExpiryBand>`;
>   a string is serialised into a *quoted string*, the parse fails, and the shop falls back to
>   365/180/90 in silence. The service now normalises that key, and a test proves both doorways.
>
> **v0.29.1 (Indigo `#3F3D8C`) - the sample shop never worked, and a "green" mirror hid it.**
> The owner ran the suite for the first time on a machine with a real SQLite engine: **666 cases, 28
> failed**, every one of the 28 downstream of a single sentence - `UNIQUE constraint failed:
> categories.name`. The lesson is not about categories; it is about **what a mirror can prove**:
>
> * **A brand-new database is not an empty one.** The first start writes five categories, and
>   `categories.name` is UNIQUE. Any generator that inserts "the usual" rows must read the table
>   first (`SeedService` already did; `SampleShopService.WriteCategories` did not). When you add a
>   write to a table with a UNIQUE index, ask: *does the app already put something there?*
> * **The mirror could not see it.** `sim10.py` rebuilt every sum over 74 days and reported 0
>   mismatches - because it started from an empty categories table. A mirror is only as good as the
>   state it starts in; **start it the way `TestDatabase` starts** (migrations + `Seed.Seed()`), and
>   list the UNIQUE constraints of every table you write to before believing it.
> * **A failure message must name the step.** "Could not be written" told the owner the table but not
>   which of forty writes had failed. `SampleShopService` now reports *while writing the shelves /
>   the medicines / two and a half months of days*.
> * **A test that asserts one machine is not a test.** *Printer: Windows default printer* is only
>   true with no printer installed; *omeprazol* is a prefix, so the exact search (correctly) found it
>   and the fuzzy test's premise was wrong. Both were repaired on the test side, and a new case pins
>   the behaviour that was actually wanted.
>
> **v0.29.0 (Burgundy `#6B1F3A`)** - all four phases are written: Phase 1-3 in full (billing, returns,
> purchase, khata, supplier ledger, shift/galla, expenses, reports, black box, backup & restore,
> Excel / CSV, reorder + WhatsApp, stock-minus registry), and Phase 4 has **all 12 of its features**:
> loyalty from the screen, photos for medicines / customers / suppliers (copied into the shop's
> folder, **put away instead of deleted**), the owner's **own fields**, **several barcodes per
> medicine** (migration 022), **medical reps + doctors + lifetime profit** (migration 023), the
> **second counter** over the shop's own share (D-62), the **tax report + `PAY-` slip + monthly
> statement** (D-63), the **installer** (F-47, D-64), the **printer & scanner test** (F-46, D-65),
> **cloud sync** (F-48, D-66) and the **bills page** (F-52, D-67) - 12 of 12, with the printer
> repair of v0.22.1 keeping the hardware test honest.
>
> **Superseded on 23 Oct 2026, and kept because the history matters:** the owner's own PC has since
> built the whole solution and run the suite green several times - `0 Warning(s), 0 Error(s)`, then
> **730 of 730 in 53 s** on v0.33.0, with the window open and `Signed in: Owner` in
> `app-2026-10-23.log`. What is still proved only by `tools/check_repo.py` is the **769 cases of
> v0.35.0**, which wait for his next `tools\\RUN-TESTS.cmd`.
>
> **The checker learned two things from this version.** `DashboardService` used `User` with no
> `using Area11.Core.Models;` and the gate stayed quiet: its mention patterns wanted `.`, `(` or
> `<` after the name, and a nullable parameter writes `User?`. Widening the pattern then produced
> six false failures, because `record` is contextual (`foreach (var record in rows)` looked like a
> declaration of a type called `in`) and because a private nested class was being registered as a
> namespace type. Both were fixed at the root and **the rule was proved by re-introducing the
> real bug** (checker fails) and taking it out again (checker passes).
>
> **v0.29.0 repairs the home screen and finishes F-11 at the counter (F-12, F-11, D-75).**
> Today's profit now follows the report's own four rows (bills - refunds - cost + restocked cost),
> the expiry cards count by **day** instead of by stored year-month, the profit card hides from
> anyone without the `profit` module (the service does not even read the number), and held/void
> bills no longer count as today's sales. The counter gains "New customer..." beside the
> customer box - the cart survives, and `PartnerService` keeps refusing a duplicate phone.
> 20 new cases in `DashboardTests`; none compiled here, `tools\RUN-TESTS.cmd` must end
> `Failed: 0` with **686** cases.
>
> **v0.28.0 turns the sample shop into a running shop (F-53, D-74).** SYSTEM -> *Sample shop* now
> writes **72 days** - 44 medicines over 17 categories, 20 customers, ~700 bills, deliveries six
> mornings a week, 16 returns, khata collections, rent/salary/chai/repairs, and 73 counted drawers
> with today's still open. The generator is deterministic (`Random(20261026)`), every closed drawer
> is rebuilt from its own payments exactly as `ShiftService` does it, and FEFO never sells a batch
> inside its last month. 12 new cases, none compiled or run here; `tools\RUN-TESTS.cmd` must end
> `Failed: 0` with **666** cases.
>
> **v0.27.0 adds the bill lookup for a return (F-20).** The Returns page takes the bill number, a phone with or
> without dashes, a customer name or a medicine, plus an optional date range, and answers with a **list** of
> bills - never a guess. *Open this bill* runs the same lookup a typed number runs, so there is one doorway.
> Searching writes nothing. 13 new cases, none of them compiled or run here; `tools\RUN-TESTS.cmd` must end
> `Failed: 0` with **654** cases.
>
> **v0.26.1 is a build fix (F-53).** The owner's first real build of the cloud round stopped in Core with **CS0200**:
> `new SaleSample { Lines = lines }` set a getter-only property; it is `init;` now. The lesson is a **24th checker
> rule**, `check_readonly_members`, which fails any object initializer that assigns a getter-only property - proved by
> reintroducing the line and watching the check fail. No behaviour changed, no test changed (**641 cases**).
>
> **v0.26.0 repairs the cloud push (F-48).** The cloud table's row is built from its own snake_case columns
> (`shop_id`, `shop_name`, `sent_at`, `version` as an int, `payload`), `sent_at` carries the shop's offset,
> `version` holds the document format while the program version stays in `payload`, and Settings has its
> fifth tab - **Cloud sync** - with the `sync.enabled` switch that was declared in Spec 6.12 and never read.
> The key is never shown again, never audited, and the tab is closed to anyone without cloud-sync permission.
> These 12 new tests have **not** been compiled or run here; `tools\RUN-TESTS.cmd` must end `Failed: 0`
> with **641** cases, and the first real Supabase push is still the owner's to make.
>
> **v0.25.0 adds the purchase, partner and numbering tests (F-18)** - the last untested corner of
> the code, proved through the public services only: a 2 x 10 box is 20 tablets, a bonus line never
> reaches the bill, a supplier payment may not overshoot, and every paper keeps its own counter.
> The 36 new cases have **not been compiled or run here** (this sandbox has no .NET SDK);
> `tools\RUN-TESTS.cmd` must end `Failed: 0` with **629** cases.
>
> **v0.24.0 adds the misspelt search (F-50).** A typo at the counter or on the Products page still
> finds the medicine - and the page says the list is a guess, never an answer.
>
> **v0.23.0 adds the sample shop (F-53).** SYSTEM -> *Sample shop* fills a new database with 20 medicines,
> 12 companies, 4 suppliers, 8 customers, 4 purchases, 12 bills, 2 returns, khata, expenses, a shift, a
> stock-take and a held cart in ONE transaction - refusing any shop that already has a bill.
>
> **v0.22.1 fixes the printer (code 6) and the two red tests.** SYSTEM -> *Printer & scanner* lists
> every printer Windows knows and saves the choice at once; an empty name now resolves the default
> printer by name instead of opening the print server, and each Windows code is explained.
>
> **v0.21.0 added cloud sync (F-48).** SYSTEM -> *Cloud sync* pushes the shop's own data to a Supabase
> mirror (lists whole, money for the last 90 days, no photos). *See what the cloud has* reads the
> copy and writes **nothing**; *Bring the cloud data here* needs a tick, takes a **backup**, writes
> every row in **one transaction**, never deletes, and ignores tables or columns this database does
> not have.
>
> **All three landed and are closed.** F-50 in v0.22.0, F-53 in v0.28.0 and *deleted* in v0.30.2
> (D-77), F-46's printer half confirmed by the owner on 23 Oct 2026. §8 at the top of this file is
> the only place that says what is current; the bullets below it are history.
> Details: `HANDOFF.md` §4.
