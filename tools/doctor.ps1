# Area11 - Visual Studio doctor
#
# Why this file exists: Visual Studio remembers the solution you had open. After the prototype was
# moved from `Area11.WPF\` to `prototypes\Area11.WPF\`, an old `Area11.WPF.sln` left in the repository
# folder still pointed at `Area11.WPF\Area11.WPF.csproj`, which no longer exists. Visual Studio then
# reports MSB4025 ("the project file could not be loaded") and, because no project loads, every type
# looks undefined (CS0103).
#
# Run it (read-only diagnosis):
#     powershell -ExecutionPolicy Bypass -File .\tools\doctor.ps1
#
# Run it to also repair what is safe to repair:
#     powershell -ExecutionPolicy Bypass -File .\tools\doctor.ps1 -Fix
#
# It never touches the shop database in C:\ProgramData\Area11 and never deletes project code:
# an old prototype folder is *renamed*, and a stale solution file is copied to *.bak first.

[CmdletBinding()]
param(
    [switch]$Fix
)

$ErrorActionPreference = 'Stop'

function Write-Head([string]$text) {
    Write-Host ''
    Write-Host $text -ForegroundColor Cyan
    Write-Host ('=' * $text.Length) -ForegroundColor DarkCyan
}

function Write-Ok([string]$text)   { Write-Host "  [ok]    $text" -ForegroundColor Green }
function Write-Warn2([string]$text) { Write-Host "  [warn]  $text" -ForegroundColor Yellow }
function Write-Bad([string]$text)  { Write-Host "  [ERROR] $text" -ForegroundColor Red }
function Write-Info([string]$text) { Write-Host "          $text" -ForegroundColor Gray }

# The doctor may be started from anywhere (Visual Studio keeps its own working directory),
# so the repository root is always the parent of this script's folder.
$root = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path
$problems = New-Object System.Collections.Generic.List[string]
$fixes = New-Object System.Collections.Generic.List[string]

Write-Host ''
Write-Host ' AREA11 - Visual Studio doctor' -ForegroundColor White
Write-Host " Repository: $root"

# ---------------------------------------------------------------- 1. tools ----
Write-Head '1. Tools'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    $version = (dotnet --version 2>$null)
    Write-Ok "dotnet SDK $version  ($($dotnet.Source))"
    if ($version -notmatch '^10\.') {
        Write-Warn2 "This project targets .NET 10 (see dotnet\global.json). Install the .NET 10 SDK."
    }
} else {
    Write-Bad 'dotnet was not found on PATH - install the .NET 10 SDK first.'
    $problems.Add('dotnet SDK missing')
}

$git = Get-Command git -ErrorAction SilentlyContinue
if ($git) { Write-Ok 'git found' } else { Write-Warn2 'git not found (only needed to update the code)' }

# ------------------------------------------------------------- 2. layout ----
Write-Head '2. Folders and solution files'

if (-not (Test-Path (Join-Path $root 'dotnet'))) {
    Write-Bad "This does not look like the Area11 folder: $root"
    Write-Info 'Expected a "dotnet" folder next to "tools". Run the script from the cloned repository.'
    Write-Info 'Example:  powershell -ExecutionPolicy Bypass -File D:\areaa\tools\doctor.ps1'
    exit 1
}

foreach ($expected in @('Area11.sln', 'dotnet', 'tools', 'docs')) {
    $path = Join-Path $root $expected
    if (Test-Path $path) { Write-Ok $expected } else { Write-Bad "missing: $expected"; $problems.Add("missing $expected") }
}

$solutions = @(Get-ChildItem -Path $root -Filter '*.sln' -File -ErrorAction SilentlyContinue)
$solutions += @(Get-ChildItem -Path (Join-Path $root 'prototypes') -Filter '*.sln' -File -ErrorAction SilentlyContinue)

if ($solutions.Count -eq 0) {
    Write-Bad 'No .sln file was found.'
    $problems.Add('no solution file')
}

foreach ($solution in $solutions) {
    Write-Info ''
    Write-Info "solution: $($solution.FullName)"

    $text = Get-Content -LiteralPath $solution.FullName -Raw
    $projectEntries = [regex]::Matches($text, 'Project\("\{[^}]+\}"\)\s*=\s*"[^"]*",\s*"([^"]+)"')

    if ($projectEntries.Count -eq 0) {
        Write-Warn2 'the file lists no projects'
    }

    foreach ($match in $projectEntries) {
        $relative = $match.Groups[1].Value
        $full = [System.IO.Path]::GetFullPath((Join-Path $solution.DirectoryName ($relative -replace '\\', '/')))

        if (Test-Path -LiteralPath $full) {
            Write-Ok "  $relative"
        } else {
            Write-Bad "  $relative  ->  FILE DOES NOT EXIST"
            $problems.Add("$($solution.Name) points at a missing project: $relative")

            # This is the exact cause of MSB4025. A solution that points at a missing prototype
            # is obsolete; the real one is Area11.sln in the repository root.
            if ($solution.Name -eq 'Area11.sln') {
                Write-Info 'Area11.sln is the REAL solution and must not be missing files - run: git pull'
            } else {
                Write-Info 'This solution is obsolete (the prototype moved to prototypes\).'
                if ($Fix) {
                    $backup = "$($solution.FullName).bak"
                    Copy-Item -LiteralPath $solution.FullName -Destination $backup -Force
                    Remove-Item -LiteralPath $solution.FullName -Force
                    $fixes.Add("removed the obsolete $($solution.Name) (backup: $([System.IO.Path]::GetFileName($backup)))")
                }
            }
        }
    }
}

# --------------------------------------------------- 3. stale VS settings ----
Write-Head '3. Leftovers that confuse Visual Studio'

$vsFolder = Join-Path $root '.vs'
if (Test-Path $vsFolder) {
    Write-Warn2 '.vs folder present (Visual Studio remembers the last solution there)'
    if ($Fix) {
        Remove-Item -LiteralPath $vsFolder -Recurse -Force
        $fixes.Add('deleted the .vs folder (Visual Studio will forget the old solution)')
    } else {
        Write-Info 'add -Fix to delete it'
    }
} else {
    Write-Ok 'no .vs folder'
}

$oldPrototype = Join-Path $root 'Area11.WPF'
if (Test-Path $oldPrototype) {
    Write-Warn2 'a prototype folder Area11.WPF exists in the repository root'
    Write-Info 'the prototype now lives in prototypes\Area11.WPF - this old copy is what VS keeps trying to open'
    if ($Fix) {
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $renamed = Join-Path $root "Area11.WPF_old_$stamp"
        Rename-Item -LiteralPath $oldPrototype -NewName (Split-Path -Leaf $renamed)
        $fixes.Add("renamed Area11.WPF -> $(Split-Path -Leaf $renamed) (nothing deleted)")
    } else {
        Write-Info 'add -Fix to rename it (nothing is deleted)'
    }
} else {
    Write-Ok 'no stale Area11.WPF folder in the root'
}

foreach ($project in @(Get-ChildItem -Path $PSScriptRoot -Filter '*.csproj' -File -ErrorAction SilentlyContinue)) {
    Write-Warn2 "a stray project sits next to the tools: $($project.Name)"
}

# --------------------------------------------------------------- 4. git ----
Write-Head '4. Code version'

if (Test-Path (Join-Path $root '.git')) {
    Push-Location $root
    try {
        $branch = (git rev-parse --abbrev-ref HEAD 2>$null)
        $commit = (git log -1 --format='%h %s' 2>$null)
        Write-Ok "branch: $branch"
        Write-Info "last commit: $commit"

        $dirty = @(git status --porcelain 2>$null)
        if ($dirty.Count -gt 0) {
            Write-Warn2 "$($dirty.Count) file(s) have local changes - 'git pull' may refuse to run"
            $dirty | Select-Object -First 8 | ForEach-Object { Write-Info $_ }
        } else {
            Write-Ok 'working tree is clean'
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Warn2 'this folder is not a git clone - update manually or clone it again'
}

# ------------------------------------------------------------ 5. verdict ----
Write-Head '5. Verdict'

if ($fixes.Count -gt 0) {
    Write-Host '  Fixes applied:' -ForegroundColor Green
    $fixes | ForEach-Object { Write-Host "    - $_" -ForegroundColor Green }
    Write-Host ''
    Write-Host '  Now: close Visual Studio, then open the ROOT solution:' -ForegroundColor White
    Write-Host "      $root\Area11.sln" -ForegroundColor White
    Write-Host ''
    Write-Host '  Or simply double-click START-AREA11.bat.' -ForegroundColor White
}

if ($problems.Count -eq 0) {
    Write-Host '  No blocking problem found.' -ForegroundColor Green
    Write-Host '  Build with:' -ForegroundColor Gray
    Write-Host "      dotnet build `"$root\Area11.sln`" -v minimal" -ForegroundColor Gray
} else {
    Write-Host '  Problems found:' -ForegroundColor Red
    $problems | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
    Write-Host ''
    Write-Host '  Send this whole window to the agent.' -ForegroundColor Yellow
}

Write-Host ''
Write-Host '  Data folder (never delete): C:\ProgramData\Area11' -ForegroundColor DarkGray
Write-Host ''
