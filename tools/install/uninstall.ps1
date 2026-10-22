<#
    Area11 - uninstaller  (F-47, D-19)

    Removes Area11 from this PC: the program folder, the three icons and the entry in
    "Add or remove programs".

    It does NOT remove  C:\ProgramData\Area11  - that folder holds every bill, every khata
    and every photo the shop has ever made. The owner's standing rule is that this folder is
    never deleted; the last lines of this file print where it is so it can be kept (or copied
    to a new PC) deliberately.

    Run it through UNINSTALL-AREA11.cmd (in the repository's tools folder, or in
    C:\Program Files\Area11 after an install), which asks Windows for Administrator rights.
#>

[CmdletBinding()]
param(
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

$installFolderName     = 'Area11'
$exeName               = 'Area11.exe'
$uninstallKey          = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Area11'
$desktopShortcutName   = 'Area11'
$startMenuShortcutName = 'Area11'
$uninstallShortcutName = 'Uninstall Area11'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Administrator rights are needed. Close this window and double-click UNINSTALL-AREA11.cmd instead.'
    }
}

function Show-Headline([string]$text) {
    Write-Host ''
    Write-Host '  =========================================================='
    Write-Host ('    ' + $text)
    Write-Host '  =========================================================='
}

function Remove-Area11 {
    Assert-Administrator

    $installFolder = Join-Path $env:ProgramFiles $installFolderName
    $dataFolder = Join-Path $env:ProgramData $installFolderName

    # Where the installer put it, in case the program folder was moved by hand.
    if (Test-Path -LiteralPath $uninstallKey) {
        $stored = (Get-ItemProperty -Path $uninstallKey -Name 'InstallLocation' -ErrorAction SilentlyContinue).InstallLocation
        if (-not [string]::IsNullOrWhiteSpace($stored)) { $installFolder = $stored }
    }

    Show-Headline 'AREA11 UNINSTALLER'
    Write-Host ('  Program   : ' + $installFolder)
    Write-Host ('  Shop data : ' + $dataFolder)
    Write-Host ''
    Write-Host '  The program is removed. The shop data - every bill, every khata, every photo -'
    Write-Host '  STAYS on this PC. Nothing in that folder is touched.'
    Write-Host ''

    if (-not $Quiet) {
        $answer = Read-Host '  Type YES to remove Area11 from this PC'
        if ($answer -ne 'YES') {
            Write-Host ''
            Write-Host '  Nothing was changed.'
            Write-Host ''
            return $false
        }
    }

    if (Get-Process -Name 'Area11' -ErrorAction SilentlyContinue) {
        if ($Quiet) { throw 'Area11 is open. Close it and run the uninstaller again.' }

        Read-Host '  Area11 is open. Close it, then press Enter here to continue' | Out-Null

        if (Get-Process -Name 'Area11' -ErrorAction SilentlyContinue) {
            throw 'Area11 is still open. Close it and run the uninstaller again.'
        }
    }

    $desktopFolder = [Environment]::GetFolderPath('CommonDesktopDirectory')
    $startFolder = [Environment]::GetFolderPath('CommonPrograms')

    $shortcuts = @(
        (Join-Path $desktopFolder ($desktopShortcutName + '.lnk')),
        (Join-Path $startFolder ($startMenuShortcutName + '.lnk')),
        (Join-Path $startFolder ($uninstallShortcutName + '.lnk'))
    )

    foreach ($shortcutPath in $shortcuts) {
        if (Test-Path -LiteralPath $shortcutPath) {
            Remove-Item -LiteralPath $shortcutPath -Force
            Write-Host ('  Removed ' + $shortcutPath)
        }
    }

    if (Test-Path -LiteralPath $uninstallKey) {
        Remove-Item -Path $uninstallKey -Recurse -Force
        Write-Host '  Removed the "Add or remove programs" entry.'
    }

    # A guard worth one line: the program folder must never be the shop folder.
    if ($installFolder.TrimEnd('\') -eq $dataFolder.TrimEnd('\')) {
        throw 'The program folder and the shop folder are the same folder. Nothing was removed; ask for help.'
    }

    if (Test-Path -LiteralPath $installFolder) {
        Remove-Item -LiteralPath $installFolder -Recurse -Force -ErrorAction SilentlyContinue

        if (Test-Path -LiteralPath $installFolder) {
            # This window still holds two files (the .cmd and this script). A detached cmd removes
            # the rest a moment after the window closes, which is why it is not an error.
            Write-Host '  A few program files are still in use by this window - they disappear in a moment.'
            $command = '/c timeout /t 2 >nul & rd /s /q "' + $installFolder + '"'
            Start-Process -FilePath 'cmd.exe' -ArgumentList $command -WindowStyle Hidden
        }
    } else {
        Write-Host ('  Nothing to remove: there is no Area11 in ' + $installFolder + '.')
    }

    Show-Headline 'AREA11 HAS BEEN REMOVED'
    Write-Host ('  Your shop data is still here: ' + $dataFolder)
    Write-Host '  Nothing in that folder was deleted. Keep it: it holds every bill of the shop.'
    Write-Host '  To use Area11 again, run the installer; it will find this data and carry on.'
    Write-Host ''

    return $true
}

try {
    $removed = Remove-Area11
    if (-not $removed) { exit 0 }
    exit 0
} catch {
    Write-Host ''
    Show-Headline 'THE UNINSTALLER STOPPED'
    Write-Host ('  ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ''
    Write-Host '  Your shop data in C:\ProgramData\Area11 was not touched.'
    Write-Host '  Copy everything above and send it in the chat.'
    Write-Host ''
    exit 1
}
