<#
    Area11 - installer  (F-47, D-19)

    What this does, in order:
      1. checks that Windows is new enough and that there is a program to copy;
      2. asks Area11 itself (Area11.exe --install-check) whether this PC is ready;
      3. copies the program to  C:\Program Files\Area11  (replacing an older copy);
      4. gives every signed-in person write access to  C:\ProgramData\Area11  and never
         touches the database, the photos or a single bill in it;
      5. starts the print spooler and adds "Microsoft Print to PDF" when the PC has no printer;
      6. writes the desktop icon, the Start-menu entry and the uninstaller;
      7. lists Area11 in "Add or remove programs" with its version and its icon.

    The rules (which folder, which shortcut, what "ready" means) live in
    dotnet\Area11.Core\Services\InstallService.cs and are pinned by InstallTests.cs.
    This file is the hands; that file is the brain. Keep the names identical - the checker
    compares them.

    Run it through tools\INSTALL-AREA11.cmd, which asks Windows for Administrator rights.
#>

[CmdletBinding()]
param(
    [string]$Source = '',
    [switch]$Quiet,
    [switch]$Start
)

$ErrorActionPreference = 'Stop'

# --- names that must match InstallService.cs ---------------------------------
$installFolderName    = 'Area11'
$exeName              = 'Area11.exe'
$uninstallerName      = 'UNINSTALL-AREA11.cmd'
$uninstallKey         = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Area11'
$everyoneSid          = '*S-1-5-32-545'
$pdfPrinterName       = 'Microsoft Print to PDF'
$pdfPrinterFeature    = 'Printing-PrintToPDFServices-Features'
$displayName          = 'Area11 - pharmacy POS'
$publisher            = 'Area11'
$desktopShortcutName  = 'Area11'
$startMenuShortcutName = 'Area11'
$uninstallShortcutName = 'Uninstall Area11'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Administrator rights are needed. Close this window and double-click INSTALL-AREA11.cmd instead.'
    }
}

function Show-Headline([string]$text) {
    Write-Host ''
    Write-Host '  =========================================================='
    Write-Host ('    ' + $text)
    Write-Host '  =========================================================='
}

# The version Windows shows in "Add or remove programs". BuildInfo.cs is the source; a published
# folder carries the same number in the exe, so the installer works even without the source tree.
function Get-Area11Version([string]$exePath, [string]$rootPath) {
    $buildInfo = Join-Path $rootPath 'dotnet\Area11.Core\Utils\BuildInfo.cs'
    if (Test-Path -LiteralPath $buildInfo) {
        $text = Get-Content -LiteralPath $buildInfo -Raw
        $match = [regex]::Match($text, 'Version\s*=\s*"([0-9.]+)"')
        if ($match.Success) { return $match.Groups[1].Value }
    }

    if (Test-Path -LiteralPath $exePath) {
        $fileVersion = (Get-Item -LiteralPath $exePath).VersionInfo.FileVersion
        if (-not [string]::IsNullOrWhiteSpace($fileVersion)) { return $fileVersion }
    }

    return '0.0.0'
}

function Get-PrinterFacts {
    $facts = @{ Known = $false; Names = @() }

    try {
        $printers = @(Get-Printer -ErrorAction Stop)
        $facts.Names = @($printers | ForEach-Object { $_.Name })
        $facts.Known = $true
        return $facts
    } catch {
        # Windows 8 and newer always have Get-Printer; the CIM query is the fallback for a
        # stripped-down Windows where the PrintManagement module is missing.
    }

    try {
        $printers = @(Get-CimInstance -ClassName Win32_Printer -ErrorAction Stop)
        $facts.Names = @($printers | ForEach-Object { $_.Name })
        $facts.Known = $true
    } catch {
    }

    return $facts
}

function Get-SpoolerState {
    try {
        $service = Get-Service -Name 'Spooler' -ErrorAction Stop
        return $service.Status.ToString().ToLowerInvariant()
    } catch {
        return 'unknown'
    }
}

# Ask Area11.exe itself. It writes the report and answers with an exit code:
#   0 = ready, 2 = a line says STOP, 3 = the report could not be written.
function Invoke-InstallCheck([string]$checkExe, [string]$reportPath, [string]$sourceFolder, [bool]$hasSdk, [string]$spooler, [hashtable]$printers) {
    $argumentLine = '--install-check "{0}" --source "{1}" --spooler {2} --sdk {3}' -f `
        $reportPath, $sourceFolder, $spooler, $(if ($hasSdk) { 'yes' } else { 'no' })

    if ($printers.Known) {
        $names = @($printers.Names | Where-Object { $_ -and ($_ -notmatch '"') })
        $argumentLine += ' --printers "{0}"' -f ($names -join '|')
    }

    # Start-Process with -Wait, because PowerShell does not wait for a windowed program (`&` would
    # return at once and leave $LASTEXITCODE from the previous command).
    $process = Start-Process -FilePath $checkExe -ArgumentList $argumentLine -Wait -PassThru -WindowStyle Hidden
    return $process.ExitCode
}

function Show-Reports([string]$reportPath) {
    if (Test-Path -LiteralPath $reportPath) {
        Get-Content -LiteralPath $reportPath | ForEach-Object { Write-Host ('  ' + $_) }
    }
}

function New-Area11Shortcut([object]$shell, [string]$shortcutPath, [string]$target, [string]$description) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $target
    $shortcut.WorkingDirectory = (Split-Path -LiteralPath $target -Parent)
    $shortcut.Description = $description
    $shortcut.IconLocation = $target + ',0'
    $shortcut.Save()
}

function ConvertTo-Kilobytes([string]$folder) {
    # Plain .NET (v0.35.2): the cmdlet version was one of the calls that could bind ambiguously on
    # the owner's PC, and this number is only the size Windows lists in "Add or remove programs".
    try {
        $sum = 0

        foreach ($file in [System.IO.Directory]::EnumerateFiles($folder, '*', 'AllDirectories')) {
            $sum += (New-Object System.IO.FileInfo $file).Length
        }

        return [int]($sum / 1KB)
    } catch {
        return 0
    }
}

function Install-Area11 {
    Assert-Administrator

    $root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
    $installFolder = Join-Path $env:ProgramFiles $installFolderName
    $dataFolder = Join-Path $env:ProgramData $installFolderName
    $installedExe = Join-Path $installFolder $exeName

    # A copy of this program into the shop's data folder (or the other way round) would be a
    # disaster on the next update: robocopy /MIR would delete the bills. Refuse before anything
    # else happens.
    $installFull = $installFolder.TrimEnd('\')
    $dataFull = $dataFolder.TrimEnd('\')
    if (($installFull -eq $dataFull) -or
        $installFull.StartsWith($dataFull + '\') -or
        $dataFull.StartsWith($installFull + '\')) {
        throw 'The program folder and the shop data folder point at the same place. Stop, and ask for help before going on.'
    }

    if (-not (Test-Path -LiteralPath $dataFolder)) {
        New-Item -ItemType Directory -Path $dataFolder -Force | Out-Null
    }

    $logsFolder = Join-Path $dataFolder 'logs'
    if (-not (Test-Path -LiteralPath $logsFolder)) {
        New-Item -ItemType Directory -Path $logsFolder -Force | Out-Null
    }

    $logPath = Join-Path $logsFolder ('install-' + (Get-Date -Format 'yyyy-MM-dd-HHmmss') + '.log')
    $reportPath = Join-Path $logsFolder 'install-report.txt'
    try { Start-Transcript -Path $logPath | Out-Null } catch { }

    Show-Headline 'AREA11 INSTALLER'
    Write-Host ('  This PC  : ' + $env:COMPUTERNAME)
    Write-Host ('  Program  : ' + $installFolder)
    Write-Host ('  Shop data: ' + $dataFolder + '   (kept - never deleted)')
    Write-Host ''

    # ---- 1. where do the program files come from? ---------------------------
    $publishedFolder = Join-Path $root 'dist\Area11'
    $setupParent = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
    $setupProgram = Join-Path $setupParent $installFolderName

    $sourceFolder = ''
    if (-not [string]::IsNullOrWhiteSpace($Source)) {
        $sourceFolder = (Resolve-Path -LiteralPath $Source).Path
    } elseif (Test-Path -LiteralPath (Join-Path $publishedFolder $exeName)) {
        $sourceFolder = $publishedFolder
    } elseif (Test-Path -LiteralPath (Join-Path $setupProgram $exeName)) {
        $sourceFolder = $setupProgram
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    $hasSdk = $null -ne $dotnet

    if ([string]::IsNullOrWhiteSpace($sourceFolder)) {
        if (-not $hasSdk) {
            throw 'There is no Area11 program to install and no .NET SDK on this PC. On the build PC run tools\MAKE-INSTALLER.cmd, copy the dist folder here, and start the installer from that folder.'
        }

        Write-Host '  No finished copy found - building a self-contained Area11.'
        Write-Host '  This happens once and takes about two minutes.'
        Write-Host ''
        & dotnet publish (Join-Path $root 'dotnet\Area11.Desktop\Area11.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $publishedFolder
        if ($LASTEXITCODE -ne 0) {
            throw 'The build failed. The first error line above says why; send it in the chat.'
        }

        $sourceFolder = $publishedFolder
    }

    if (-not (Test-Path -LiteralPath (Join-Path $sourceFolder $exeName))) {
        throw ('There is no ' + $exeName + ' in ' + $sourceFolder + '. That folder is incomplete.')
    }

    # ---- 2. ask Area11 whether this PC is ready -----------------------------
    $printers = Get-PrinterFacts
    $spooler = Get-SpoolerState

    $code = Invoke-InstallCheck -checkExe (Join-Path $sourceFolder $exeName) -reportPath $reportPath `
        -sourceFolder $sourceFolder -hasSdk $hasSdk -spooler $spooler -printers $printers

    Show-Reports $reportPath

    if ($code -eq 2) {
        throw 'The report says STOP. Nothing on this PC was changed - fix the line marked STOP and run the installer again.'
    }

    if ($code -eq 3) {
        throw ('Area11 could not write its report to ' + $reportPath + '. The shop folder may be read-only; the data is safe, nothing was changed.')
    }

    # ---- 3. the program itself ---------------------------------------------
    if (Get-Process -Name 'Area11' -ErrorAction SilentlyContinue) {
        if ($Quiet) { throw 'Area11 is open. Close it and run the installer again.' }

        Write-Host ''
        Write-Host '  Area11 is open in another window.'
        Read-Host '  Close it, then press Enter here to continue' | Out-Null

        if (Get-Process -Name 'Area11' -ErrorAction SilentlyContinue) {
            throw 'Area11 is still open. Close it and run the installer again.'
        }
    }

    if (-not (Test-Path -LiteralPath $installFolder)) {
        New-Item -ItemType Directory -Path $installFolder -Force | Out-Null
    }

    Write-Host ''
    Write-Host '  Copying the program files...'
    & robocopy $sourceFolder $installFolder /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:2 | Out-Null
    $copyCode = $LASTEXITCODE
    if ($copyCode -ge 8) {
        throw ('The copy failed (robocopy code ' + $copyCode + '). Close any window that has ' + $installFolder + ' open and run the installer again.')
    }

    # The uninstaller travels with the program: it is removed with the program, not with the data.
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination (Join-Path $installFolder 'uninstall.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $setupParent $uninstallerName) -Destination (Join-Path $installFolder $uninstallerName) -Force

    # The second-counter helper (F-45) lives in tools\ in the repository, which a shop PC that was
    # installed from a folder does not have. Carry it along, so the LAN page's instruction can be
    # followed on the shop PC itself.
    $shareScript = Join-Path $setupParent 'SHARE-AREA11.cmd'
    if (Test-Path -LiteralPath $shareScript) {
        Copy-Item -LiteralPath $shareScript -Destination (Join-Path $installFolder 'SHARE-AREA11.cmd') -Force
    }

    # ---- 4. the shop folder belongs to everyone who signs in ----------------
    Write-Host '  Giving every person who signs in on this PC write access to the shop folder...'
    & icacls $dataFolder /grant ($everyoneSid + ':(OI)(CI)M') /T /C /Q | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host '  Note: Windows reported a problem with the folder permissions.' -ForegroundColor Yellow
        Write-Host '        If a cashier cannot save a bill, run this installer again as Administrator.' -ForegroundColor Yellow
    }

    # ---- 5. printing -------------------------------------------------------
    try {
        Set-Service -Name 'Spooler' -StartupType Automatic -ErrorAction Stop
        if ((Get-Service -Name 'Spooler').Status -ne 'Running') {
            Start-Service -Name 'Spooler' -ErrorAction Stop
        }

        Write-Host '  Print spooler: running.'
    } catch {
        Write-Host '  Note: the print spooler could not be started. Area11 still works; printing may' -ForegroundColor Yellow
        Write-Host '        not work until Windows is restarted.' -ForegroundColor Yellow
    }

    if ($printers.Known -and $printers.Names.Count -eq 0) {
        try {
            $feature = Get-WindowsOptionalFeature -Online -FeatureName $pdfPrinterFeature -ErrorAction Stop
            if ($feature.State -eq 'Enabled') {
                Write-Host ('  No printer found yet; ' + $pdfPrinterName + ' is ready for receipts.')
            } else {
                Write-Host ('  No printer found - adding ' + $pdfPrinterName + '...')
                Enable-WindowsOptionalFeature -Online -FeatureName $pdfPrinterFeature -NoRestart -ErrorAction Stop | Out-Null
            }
        } catch {
            Write-Host ('  Note: ' + $pdfPrinterName + ' could not be added. Add the receipt printer in') -ForegroundColor Yellow
            Write-Host '        Windows; Area11 prints to whatever printer Windows has.' -ForegroundColor Yellow
        }
    }

    # ---- 6. the icons ------------------------------------------------------
    # v0.35.2: the program has already been copied, so an icon that cannot be written is a note,
    # not the end of the install. On the owner's PC the installer (v0.35.1) stopped in this region
    # with a cmdlet-binding message that named neither the line nor the value.
    $desktopFolder = [Environment]::GetFolderPath('CommonDesktopDirectory')
    $startFolder = [Environment]::GetFolderPath('CommonPrograms')

    try {
        $shell = New-Object -ComObject WScript.Shell

        New-Area11Shortcut -shell $shell -shortcutPath (Join-Path $desktopFolder ($desktopShortcutName + '.lnk')) `
            -target $installedExe -description 'Area11 - pharmacy POS'
        New-Area11Shortcut -shell $shell -shortcutPath (Join-Path $startFolder ($startMenuShortcutName + '.lnk')) `
            -target $installedExe -description 'Area11 - pharmacy POS'
        New-Area11Shortcut -shell $shell -shortcutPath (Join-Path $startFolder ($uninstallShortcutName + '.lnk')) `
            -target (Join-Path $installFolder $uninstallerName) -description 'Remove Area11 from this PC. The shop data stays.'

        Write-Host '  Desktop and Start menu icons: written.'
    } catch {
        Write-Host ('  Note: the icons could not be written (' + $_.Exception.Message + ').') -ForegroundColor Yellow
        Write-Host ('        Area11 is installed - start it from ' + $installedExe + '.') -ForegroundColor Yellow
    }

    # ---- 7. Add or remove programs ----------------------------------------
    $version = Get-Area11Version -exePath $installedExe -rootPath $root
    $uninstallerPath = Join-Path $installFolder $uninstallerName

    try {
        # .NET instead of New-Item / New-ItemProperty (v0.35.2): five plain calls cannot resolve to
        # the wrong "parameter set", and if Windows refuses (no elevation) the sentence below says
        # what was not written and what to do - the program is still installed either way.
        # One definition of the path: $uninstallKey is 'HKLM:\...' and Substring(6) drops 'HKLM:\'.
        $key = [Microsoft.Win32.Registry]::LocalMachine.CreateSubKey($uninstallKey.Substring(6), $true)

        if ($null -eq $key) {
            throw 'Windows refused to create the entry.'
        }

        $key.SetValue('DisplayName', $displayName)
        $key.SetValue('DisplayVersion', $version)
        $key.SetValue('Publisher', $publisher)
        $key.SetValue('InstallLocation', $installFolder)
        $key.SetValue('DisplayIcon', ($installedExe + ',0'))
        $key.SetValue('UninstallString', ('"' + $uninstallerPath + '"'))
        $key.SetValue('QuietUninstallString', ('"' + $uninstallerPath + '" -Quiet'))
        $key.SetValue('EstimatedSize', [int](ConvertTo-Kilobytes $installFolder),
            [Microsoft.Win32.RegistryValueKind]::DWord)
        $key.SetValue('NoModify', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
        $key.SetValue('NoRepair', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
        $key.Close()

        Write-Host '  Add or remove programs: Area11 listed.'
    } catch {
        Write-Host ('  Note: the "Add or remove programs" entry could not be written (' + $_.Exception.Message + ').') -ForegroundColor Yellow
        Write-Host ('        The program is installed in ' + $installFolder + '; the shop data is untouched.') -ForegroundColor Yellow
    }

    # ---- done -------------------------------------------------------------
    Show-Headline ('AREA11 ' + $version + ' IS INSTALLED')
    Write-Host ('  Desktop icon : ' + (Join-Path $desktopFolder ($desktopShortcutName + '.lnk')))
    Write-Host ('  Start menu   : ' + (Join-Path $startFolder ($startMenuShortcutName + '.lnk')))
    Write-Host ('  Uninstall    : "Add or remove programs" -> Area11, or ' + $uninstallerPath)
    Write-Host ('  Shop data    : ' + $dataFolder + '   (never deleted)')
    Write-Host ('  Log          : ' + $logPath)
    Write-Host ''
    Write-Host '  Sign in with the user name EMPTY and the password: area11'
    Write-Host ''

    # Start it through Explorer, never from this window: this window is elevated (Administrator),
    # and a shop program must NOT run as Administrator - it would create files the cashier's
    # account may then not be able to write. Explorer starts it as the person who is signed in.
    if ($Start) {
        Start-Process -FilePath 'explorer.exe' -ArgumentList ('"' + $installedExe + '"')
    } elseif (-not $Quiet) {
        $answer = Read-Host '  Start Area11 now? (Y/n)'
        if ($answer -notmatch '^(n|no)$') {
            Start-Process -FilePath 'explorer.exe' -ArgumentList ('"' + $installedExe + '"')
        }
    }
}

try {
    Install-Area11
    try { Stop-Transcript | Out-Null } catch { }
    exit 0
} catch {
    Write-Host ''
    Show-Headline 'THE INSTALLER STOPPED'
    Write-Host ('  ' + $_.Exception.Message) -ForegroundColor Red

    # Where it stopped (v0.35.2). "Parameter set cannot be resolved" named neither the line nor the
    # command, and finding it cost a round; the next stop prints both.
    try {
        $where = $_.InvocationInfo

        if ($where -and $where.ScriptLineNumber -gt 0) {
            Write-Host ('  Where : line ' + $where.ScriptLineNumber + ' - ' + $where.Line.Trim()) -ForegroundColor Yellow
        }
    } catch {
    }

    Write-Host ''
    Write-Host '  Nothing was deleted. The shop data in C:\ProgramData\Area11 is untouched.'
    Write-Host '  Copy everything above and send it in the chat.'
    Write-Host ''
    try { Stop-Transcript | Out-Null } catch { }
    exit 1
}
