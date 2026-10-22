# The second counter over the LAN (F-45)

Two PCs, one shop. The **main counter** keeps the database and shares its folder; the **second
counter** opens the same file over the shop's own network. No internet, no server, no cloud.

---

## The one rule to read before starting

> **This is safe for two counters on a wired network.** The database is one SQLite file. Windows
> shares it, and SQLite's file locking keeps the two counters from writing over each other - every
> bill is one short transaction (Spec 5.1.4), so a lost cable refuses the bill instead of writing
> half of it.
>
> It is **not** safe to grow: three or more counters, a counter in another building on Wi-Fi, or a
> folder reached over a VPN. For that the specification already names the next step - a small
> `Area11.Server` on the main PC (Spec §34.9) - and it is a separate piece of work, not a setting.

The program does three things to keep this honest:

1. **WAL is switched off on a shared file.** WAL keeps a `-shm` file that two PCs cannot share, so a
   network database uses the old rollback journal with `synchronous=FULL` and a 15-second wait.
2. **A second counter never creates an empty shop.** If the main PC is off or the share is missing,
   Area11 stops with the full path in the message. It never starts a fresh database on the second PC.
3. **Only the owner may change it**, and the change takes effect at the next restart - never under
   an open bill.

---

## On the main counter (the PC that already has the data)

1. Update to the new version and start Area11 once. Everything stays exactly where it was.
2. **SYSTEM → Second counter (LAN)**.
3. Press **Prepare this PC as the main counter**, then press the same button again when it turns
   into a confirmation. The page now says the database uses the rollback journal, and `shop.ini`
   records the folder.
4. Restart Area11 on this PC.
5. Double-click **`tools\SHARE-AREA11.cmd`** (it asks for Administrator). It prints the address the
   second counter needs:

   ```
   \\THIS-PC\Area11
   ```

6. Write that address down, or leave the window open.

If the script reports a failure, the two usual reasons are: *File and Printer Sharing* is off for
this network, or Windows has marked the network as **Public**. Both are changed in
**Settings → Network & Internet → Network profile**.

---

## On the second counter

1. Install the same version (a copied folder is enough; see `START-AREA11.bat`).
2. Start it **once** on its own, so its data folder exists, then close it.
3. Start it again, sign in as the owner, open **SYSTEM → Second counter (LAN)**.
4. In *This PC uses the main counter's database* write the address from the main PC and press
   **Join the main counter** twice (the second press confirms).
   The program opens the file, checks it is an Area11 database, takes a real write lock and writes
   one line into the main counter's black box - this is the proof, not a promise.
5. Restart Area11. It now opens the **main PC's** database. The old, empty database this PC had is
   left untouched on its own disk; nothing is deleted.

The status card must say:

| What | The right answer |
|---|---|
| Mode | Second counter - uses the main PC's database |
| Database folder | `\\MAIN-PC\Area11` |
| Journal | `DELETE` (not `WAL`) |
| Reachable | reachable - *"The file is readable and writable. Shop: …"* |

---

## Day to day

- **Start the main PC first.** If the second counter starts while the main PC is off, it stops with
  a sentence naming the path - that is the guard working, not a fault.
- **Backups are taken on the main counter.** Its `backups` folder is local to that PC and the zip
  holds the whole shop (both counters' bills). A backup made on the second counter also reads the
  shared file, but the zip lands in **that PC's own** `backups` folder - use the main counter for the
  shop's real backup, so there is one shelf of copies and not two.
- **Photos travel with the database**: they live in `Area11\photos` *inside the shared folder*, so
  both counters show the same picture (U-19 stores only the file name).
- **The black box shows who joined**: `lan_prepare`, `lan_connect`, `lan_test` in SYSTEM → Black box.

---

## When something fails

| What you see | What it means |
|---|---|
| *"The shop's database was not found on the network at `\\…\area11.db`"* | The main PC is off, its folder is not shared, or the name changed. Nothing was created here. |
| *"The file is readable and writable, but it has no shop name"* | This is not the shop's database. Check the folder name. |
| *"could not be opened for writing"* | The share has Read-only permission. On the main PC: right-click the folder → Properties → Sharing → Advanced sharing → Permissions → **Change** → Allow. |
| Journal still says `WAL` | The main PC did not restart after *Prepare*, or another Area11 window was open during it. Close them, press *Prepare* again, restart. |
| Bills are slow on the second counter | Normal under load: the second counter waits up to 15 seconds for the other one's short transaction. A wired network makes this rare. |

Everything above is also recorded in `docs/OWNER-DECISIONS.md` (D-62) in Urdu.
