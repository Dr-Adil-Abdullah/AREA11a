# When GitHub Desktop cannot update the code

Two failures look alike and have nothing in common. Read the **first line** of the red box in
GitHub Desktop and pick the matching section. In both cases the shop data
(`C:\ProgramData\Area11`) is **not involved** and is never touched.

---

## A. `Failed to connect to github.com port 443 ... Could not connect to server`

The computer could not open a connection to GitHub at all. The code, the branch and the repository
are fine - the road is closed. Nothing inside Area11 can fix this; the network must be fixed.

### What to do, one step at a time

1. **Run `tools\CHECK-NETWORK.cmd`** (double-click). Step 2 of its report is the answer:
   *port 443 OPEN* = the network is fine and the problem is in git settings;
   *port 443 BLOCKED* = the provider / VPN / firewall is stopping GitHub.
2. **VPN:** turn it **off** and try again. If the provider blocks GitHub, do the opposite - turn a
   VPN **on**.
3. **Mobile hotspot:** connect the phone by cable or Wi-Fi and run the update again. This separates
   "the shop's line" from "the computer".
4. **DNS:** adapter settings → IPv4 → *Use the following DNS server addresses* → `1.1.1.1` and
   `8.8.8.8`. Then run `ipconfig /flushdns`.
5. **Proxy left behind:** `netsh winhttp show proxy` must say *Direct access*. If it names a proxy
   that no longer exists, clear it:
   `netsh winhttp reset proxy`, then
   `git config --global --unset http.proxy` and `git config --global --unset https.proxy`.

### Get the same code without git (always works)

The branch is downloadable as an ordinary file - the browser only needs to reach github.com once:

```
https://github.com/Dr-Adil-Abdullah/AREA11a/archive/refs/heads/main.zip
```

1. Open the link, let the ZIP download (about 0.5 MB).
2. Right-click the downloaded file → **Extract All** → choose `D:\` → Extract.
3. Open the new folder (`D:\AREA11a-main`) and double-click **`START-AREA11.bat`**.
   It notices there is no `.git` folder, skips the update, builds and runs the program.

The version is the same as the latest commit; the colour on the sign-in screen proves which build is
running. The clone in `Documents` can be repaired later - the ZIP copy is not a fork and holds no
data of its own.

---

## B. `update_ref failed for ref 'ORIG_HEAD'` · `Unable to create ...index.lock` · `File exists`

The previous update was interrupted (or a second program held the folder), so a `*.lock` file is
left inside `.git`. git refuses to write until it is gone. The network is fine.

**Run `tools\FIX-UPDATE.cmd`** (double-click). It removes only `*.lock` files inside `.git` and the
stale `ORIG_HEAD` bookmark, then prints the branch, the last commit and every file with local
changes. It never deletes a source file.

If Windows refuses to delete a lock file, restart the PC and run the tool again.

---

## C. `Your local changes would be overwritten`

A file in the folder was edited by hand. In GitHub Desktop: **Changes** tab → right-click →
**Discard all changes** (only if the edit was not wanted) → then **Pull origin**.

---

## Why this is documented here (D-25)

The owner works on one Windows PC with one ISP; a blocked port or a leftover lock must be solved
without a developer and without losing a single bill. Both faults have cost time already, so both
now have a double-click tool and this page.
