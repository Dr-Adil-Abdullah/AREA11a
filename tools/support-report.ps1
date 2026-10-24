# =============================================================================
#  Area11 - write ONE text file that says everything about this PC and this copy
#
#  Called by tools\SEND-REPORT.cmd (double-click), never by the program itself.
#
#  Why this exists: when the owner reports "it does not start", the answer depends
#  on six things that live in six different places - the .NET SDK, the branch, the
#  shop folder, the newest crash file, the build output and the test output. Asking
#  for them one at a time costs a day per round. This collects them in one file that
#  can be pasted into the chat complete.
#
#  It only READS. It does not build, does not delete, and does not open the shop
#  database (it prints the file's size and date, not a single row).
# =============================================================================

param(
    [string]$Build = "unknown"
)

$ErrorActionPreference = "Continue"

$root = Split-Path -Parent $PSScriptRoot
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$report = Join-Path $root ("report-" + $stamp + ".txt")
$lines = New-Object System.Collections.Generic.List[string]

function Section([string]$title) {
    $lines.Add("")
    $lines.Add("==================================================================")
    $lines.Add("  " + $title)
    $lines.Add("==================================================================")
}

# Everything an external command printed, one string per line - the way the console
# showed it. A command that cannot be run is written down as such: a report with a
# silent gap reads like a healthy machine.
function Capture([string]$exe, [string[]]$arguments) {
    try {
        $output = & $exe @arguments 2>&1

        foreach ($item in $output) {
            $lines.Add("" + $item)
        }
    }
    catch {
        $lines.Add("(could not run " + $exe + ": " + $_.Exception.Message + ")")
    }
}

# The first N lines of a text file, with a note when it was cut.
function Add-FileText([string]$path, [string]$label, [int]$maxLines) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path)) {
        $lines.Add($label + ": (not there)")
        return
    }

    try {
        $content = Get-Content -LiteralPath $path -ErrorAction Stop

        if ($null -eq $content) {
            $lines.Add($label + ": (empty)")
            return
        }

        $total = @($content).Count

        if ($total -gt $maxLines) {
            $lines.Add($label + ": (last " + $maxLines + " of " + $total + " lines)")
            $content = @($content)[($total - $maxLines)..($total - 1)]
        }

        foreach ($line in $content) {
            $lines.Add("" + $line)
        }
    }
    catch {
        $lines.Add($label + ": (could not be read: " + $_.Exception.Message + ")")
    }
}

# -----------------------------------------------------------------------------
Section "THIS COPY"

$lines.Add("Build on this PC : " + $Build)
$lines.Add("Report written   : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
$lines.Add("Computer         : " + $env:COMPUTERNAME)
$lines.Add("Windows user     : " + $env:USERNAME)
$lines.Add("Windows          : " + [System.Environment]::OSVersion.VersionString)
$lines.Add("64-bit process   : " + [System.Environment]::Is64BitOperatingSystem)
$lines.Add("PowerShell       : " + $PSVersionTable.PSVersion.ToString())
$lines.Add("Folder           : " + $root)

# -----------------------------------------------------------------------------
Section ".NET SDK (dotnet --info)"

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $lines.Add("dotnet WAS NOT FOUND on this PC. Area11 cannot be built without it.")
    $lines.Add("Install it from https://dotnet.microsoft.com/download/dotnet/10.0")
}
else {
    Capture "dotnet" @("--info")
}

# -----------------------------------------------------------------------------
Section "THIS COPY OF THE CODE (git)"

if ($null -eq (Get-Command git -ErrorAction SilentlyContinue)) {
    $lines.Add("git WAS NOT FOUND on this PC - is this folder a download (.zip) and not a clone?")
}
else {
    Capture "git" @("rev-parse", "--short", "HEAD")
    Capture "git" @("branch", "--show-current")
    Capture "git" @("log", "-1", "--format=%h %ad %s", "--date=iso")

    $lines.Add("")
    $lines.Add("--- changed files (empty = nothing to pull or push) ---")
    Capture "git" @("status", "--short")
}

# -----------------------------------------------------------------------------
Section "THE FILES THIS BUILD NEEDS"

$needed = @(
    "Area11.sln",
    "dotnet\Area11.Core\Area11.Core.csproj",
    "dotnet\Area11.Desktop\Area11.Desktop.csproj",
    "dotnet\Area11.Tests\Area11.Tests.csproj",
    "dotnet\Area11.Core\Utils\BuildInfo.cs",
    "START-AREA11.bat"
)

foreach ($name in $needed) {
    $path = Join-Path $root $name
    $state = if (Test-Path -LiteralPath $path) { "present" } else { "MISSING" }
    $lines.Add(("{0,-52} {1}" -f $name, $state))
}

$exe = Join-Path $root "dotnet\Area11.Desktop\bin\Debug\net10.0-windows\Area11.exe"

if (Test-Path -LiteralPath $exe) {
    $info = Get-Item -LiteralPath $exe
    $lines.Add("")
    $lines.Add("Area11.exe       : built " + $info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") +
        " (" + [math]::Round($info.Length / 1KB) + " KB)")
}
else {
    $lines.Add("")
    $lines.Add("Area11.exe       : NOT BUILT YET in this folder")
}

# -----------------------------------------------------------------------------
Section "THE SHOP FOLDER (read only - no rows are read)"

$data = $env:ProgramData

if ([string]::IsNullOrEmpty($data)) {
    $data = "C:\ProgramData"
}

$shop = Join-Path $data "Area11"
$db = Join-Path $shop "area11.db"

#  The logs folder is worked out ONCE, here, before anything uses it.
#  Why: v0.31.1 moved the crash section and took its local `$logsFolder` with it, so the log
#  section below asked Test-Path about $null - the owner's console showed a red
#  "Cannot bind argument to parameter 'LiteralPath' because it is null" on top of a report that
#  otherwise worked. A holder that is computed in two places will always end up computed in
#  neither, so it is computed in one.
$logsFolder = Join-Path $shop "logs"

if (-not (Test-Path -LiteralPath $shop)) {
    $lines.Add("C:\ProgramData\Area11 does not exist yet - the program was never started on this PC.")
}
else {
    $lines.Add("Folder: " + $shop)
    $lines.Add("")

    Get-ChildItem -LiteralPath $shop -ErrorAction SilentlyContinue |
        Select-Object Name, Mode, LastWriteTime, Length |
        Format-Table -AutoSize |
        Out-String |
        ForEach-Object { $lines.Add($_) }

    if (Test-Path -LiteralPath $db) {
        $dbf = Get-Item -LiteralPath $db
        $lines.Add("")
        $lines.Add("area11.db        : " + [math]::Round($dbf.Length / 1MB, 2) + " MB, last written " +
            $dbf.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"))

        foreach ($extra in @("area11.db-wal", "area11.db-shm")) {
            $side = Join-Path $shop $extra

            if (Test-Path -LiteralPath $side) {
                $s = Get-Item -LiteralPath $side
                $lines.Add($extra.PadRight(16) + " : " + [math]::Round($s.Length / 1KB) + " KB")
            }
        }
    }
    else {
        $lines.Add("")
        $lines.Add("area11.db        : NOT THERE (a brand-new shop - it is created at the first sign-in)")
    }

    $logs = $logsFolder

    if (Test-Path -LiteralPath $logs) {
        $lines.Add("")
        $lines.Add("--- newest 8 files in logs ---")
        Get-ChildItem -LiteralPath $logs -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 8 Name, LastWriteTime, Length |
            Format-Table -AutoSize |
            Out-String |
            ForEach-Object { $lines.Add($_) }
    }
}

# -----------------------------------------------------------------------------
Section "THE CRASH REPORTS ON THIS PC (newest first - the DATE decides which one matters)"

#  Why a list and not one file: on 23 Oct 2026 the launcher printed a report from the
#  day before, written by a different version, for a fault that had already been fixed.
#  A report is evidence only for the run that wrote it, so every report is listed with
#  its date and version, and only the newest is quoted in full. Two folders are read,
#  because the program writes into %TEMP%\Area11 when the shop folder is not writable
#  (a second counter whose share is switched off).
$crashFolders = @($logsFolder, (Join-Path $env:TEMP "Area11"))
$crashes = @()

foreach ($folder in $crashFolders) {
    if (Test-Path -LiteralPath $folder) {
        $crashes += Get-ChildItem -LiteralPath $folder -Filter "crash-*.txt" -File -ErrorAction SilentlyContinue
    }
}

$crashes = @($crashes | Sort-Object LastWriteTime -Descending)

if ($crashes.Count -eq 0) {
    $lines.Add("No crash report exists - the program has not died with an exception on this PC.")
}
else {
    $lines.Add("Reports on this PC: " + $crashes.Count)
    $lines.Add("")

    foreach ($one in $crashes) {
        $lines.Add("  " + $one.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") + "   " +
                   $one.Length.ToString().PadLeft(6) + " bytes   " + $one.FullName)
    }

    $lines.Add("")
    $lines.Add("The newest one, quoted in full - it belongs to the run that wrote it, on the date above:")
    $lines.Add("")
    Add-FileText $crashes[0].FullName "crash report" 200
}

# -----------------------------------------------------------------------------
Section "THE LAST RUN (the program's own heartbeat)"

#  Why this section exists (v0.34.0, D-90): on 23 Oct 2026 the owner was asked to look for
#  %TEMP%\Area11\binding-errors.txt after a run, found the folder missing entirely, and that
#  absence was read as a fault - while it actually meant "nothing was noted", which is the healthy
#  answer. The two cases look identical from the outside, so the program now writes a small trail on
#  every start: no file = it never started; last line = the last thing it reached.
$trailPath = Join-Path (Join-Path $env:TEMP "Area11") "last-run.txt"

if (Test-Path -LiteralPath $trailPath) {
    $trail = Get-Content -LiteralPath $trailPath -ErrorAction SilentlyContinue
    $trailCount = @($trail).Count

    $lines.Add("A trail exists: " + $trailCount + " line(s), last written " +
               (Get-Item -LiteralPath $trailPath).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"))
    $lines.Add("")
    $lines.Add("Read it from the TOP: the header names the build, and the LAST line is the last")
    $lines.Add("thing the program reached. No 'closed' line means it was still open when this ran.")
    $lines.Add("The program caps this file at 140 lines, so the whole of it is printed below.")
    $lines.Add("")
    Add-FileText $trailPath "last-run" 200
}
else {
    $lines.Add("No trail file at all: " + $trailPath)
    $lines.Add("")
    $lines.Add("That means the program has NOT STARTED since v0.34.0 - so the absence of a")
    $lines.Add("binding-errors file below says nothing about the shop. Build and launch first")
    $lines.Add("with START-AREA11.bat, then run this report again.")
}

$lines.Add("")
$lines.Add("The binding watch (the folder is created only when WPF raises a real complaint):")

$watchPath = Join-Path (Join-Path $env:TEMP "Area11") "binding-errors.txt"

if (Test-Path -LiteralPath $watchPath) {
    Add-FileText $watchPath "binding-errors" 40
}
else {
    $lines.Add("binding-errors.txt: (not there) - this is the HEALTHY answer: a run with no")
    $lines.Add("silent binding fault writes no file at all. It only means something once the")
    $lines.Add("trail above proves the program started and pages were opened.")
}

# -----------------------------------------------------------------------------
Section "THE END OF THE NEWEST LOG"

$logFile = $null

if ((-not [string]::IsNullOrEmpty($logsFolder)) -and (Test-Path -LiteralPath $logsFolder)) {
    $logFile = Get-ChildItem -LiteralPath $logsFolder -Filter "*.log" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

if ($null -eq $logFile) {
    $lines.Add("No log file exists yet.")
}
else {
    $lines.Add("File: " + $logFile.FullName)
    $lines.Add("")
    Add-FileText $logFile.FullName "log" 60
}

# -----------------------------------------------------------------------------
Section "THE LAST BUILD (errors only, then the tail)"

$buildErrors = Join-Path $root "build-errors.txt"
$buildLog = Join-Path $root "build.log"

if ((-not (Test-Path -LiteralPath $buildErrors)) -and (-not (Test-Path -LiteralPath $buildLog))) {
    $lines.Add("No build.log in this folder - START-AREA11.bat was not run from here since v0.30.3.")
}
else {
    if (Test-Path -LiteralPath $buildErrors) {
        $lines.Add("--- build-errors.txt (the compiler's own lines) ---")
        Add-FileText $buildErrors "build-errors" 60
        $lines.Add("")
    }

    if (Test-Path -LiteralPath $buildLog) {
        $lines.Add("--- the last 40 lines of build.log ---")
        Add-FileText $buildLog "build.log" 40
    }
}

# -----------------------------------------------------------------------------
Section "THE LAST TEST RUN"

$testLog = Join-Path $root "test.log"

if (-not (Test-Path -LiteralPath $testLog)) {
    $lines.Add("No test.log in this folder - tools\RUN-TESTS.cmd was not run from here since v0.30.3.")
}
else {
    Add-FileText $testLog "test.log" 40
}

# -----------------------------------------------------------------------------
Section "ERRORS RAISED BY THIS REPORT ITSELF"

#  Why this section exists: on 23 Oct 2026 the report tool printed
#
#      Cannot bind argument to parameter 'LiteralPath' because it is null.
#
#  on the console while writing a perfectly readable file. A fault in the tool is not in the
#  report, so the owner cannot see it when he sends the file - and a report with a silent gap
#  reads like a healthy machine. Every error this script raises is now written into the file.

$selfErrors = @($Error | Where-Object {
        $null -ne $_ -and $null -ne $_.InvocationInfo -and
        $_.InvocationInfo.ScriptName -eq $PSCommandPath
    })

if ($selfErrors.Count -eq 0) {
    $lines.Add("None - every step of this report ran.")
}
else {
    $lines.Add("This report hit " + $selfErrors.Count + " problem(s) of its own (NOT the shop's):")
    $lines.Add("")

    $shown = 0

    foreach ($one in $selfErrors) {
        if ($shown -ge 5) {
            $lines.Add("  ... and " + ($selfErrors.Count - 5) + " more.")
            break
        }

        $lines.Add("  line " + $one.InvocationInfo.ScriptLineNumber + ": " + $one.Exception.Message)
        $shown++
    }
}

# -----------------------------------------------------------------------------
Section "WHAT TO DO WITH THIS FILE"

$lines.Add("1. Send the WHOLE of this file in the chat - it is plain text and it is")
$lines.Add("   the only thing needed to name the fault.")
$lines.Add("2. It can contain shop text that a crash printed (a name, a bill number,")
$lines.Add("   sometimes a phone number). Read it first if you wish; nothing here is")
$lines.Add("   sent anywhere by this script - it only writes the file.")
$lines.Add("3. Nothing in C:\ProgramData\Area11 was changed: every step above reads.")

# -----------------------------------------------------------------------------
try {
    $lines | Set-Content -LiteralPath $report -Encoding UTF8
    Write-Host ""
    Write-Host "  Report written:"
    Write-Host "      $report"
    Write-Host ""
    Write-Host "  Send that whole file in the chat."
}
catch {
    Write-Host ""
    Write-Host "  The report could not be written:"
    Write-Host "      $report"
    Write-Host "      $($_.Exception.Message)"
    exit 1
}
