# Continuous build

`build.yml` compiles the solution and runs the 131 test cases on a **Windows** runner, so the whole team
sees whether the code really builds without touching the shop computer.

It is not active yet because the agent's GitHub App token may not create workflow files.
To enable it: **GitHub → this repository → Add file → Create new file**, name it
`.github/workflows/build.yml`, paste the contents of `build.yml`, commit. It runs on every push.

Until then the gate is: `python3 tools/check_repo.py` here, and `dotnet build` + `dotnet test`
on the owner's Windows machine (`docs/BUILD-ON-WINDOWS.md`).
