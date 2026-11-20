# Windows build / test / optional test-installer template

`build.yml` is a **template**, not an active GitHub Actions workflow. As of the
2026-11-10 check this checkout has no `.github/workflows` and the repository lists
zero Actions runs. The Actions permissions-settings API returned HTTP 403 for the
current integration; ordinary source Git access is separate. No installer was
built by writing this template.

To enable, an owner/integration with workflow-write permission must place the file
at `.github/workflows/build.yml`. Do not share credentials in chat. The workflow
builds the entire solution with .NET 10 on Windows and runs the current synthetic
C# suite (3057 written cases at v0.122.0;execution still required), retaining TRX evidence on failure.
A successful test job is not a real WPF/printer/install smoke test.

After enabling, a manual **Run workflow** with `make_installer=true` publishes
self-contained win-x64 only after earlier build/tests pass, packs with Inno Setup6
if available on the runner, and uploads `Area11-test-installer` (14-day retention)
with SHA256. It never installs or launches the shop app on a runner. An unsigned
installer may receive SmartScreen warnings; it is not production certification.
The workflow template and its PowerShell have not been executed here.

Local Windows fallback: run `tools\RUN-TESTS.cmd`, build `Area11.sln -c Release`,
then `tools\MAKE-SETUP.cmd` with .NET10 SDK and Inno Setup6 installed. The local
MAKE-SETUP script publishes/packs; it does **not** itself run the test suite.
Expected output: `dist\Area11-Setup-v0.92.0.exe`. Generated binaries/data must
stay out of Git. Use a separate VM/spare PC with no shop data for test installation:
changing the executable folder does not isolate `C:\ProgramData\Area11`.

See `docs/REMAINING-WORK-AND-TEST-INSTALLER-URDU.md` for remaining work, the
4–8-cycle estimate with caveats, and safe testing conditions.
