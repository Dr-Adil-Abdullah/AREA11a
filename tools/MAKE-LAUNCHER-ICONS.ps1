# Convenience shortcuts in this checkout only. Never replace an existing file; if the folder
# moves, delete the old shortcuts and run START-AREA11.bat again to regenerate them.
$root = Split-Path -Parent $PSScriptRoot
$icon = Join-Path $root 'dotnet\Area11.Desktop\Resources\area11.ico'
if (-not (Test-Path -LiteralPath $icon -PathType Leaf)) { exit 1 }
try {
    $shell = New-Object -ComObject WScript.Shell -ErrorAction Stop
    foreach ($item in @(
        @{ Name = '! START AREA11.lnk'; Target = 'START-AREA11.bat'; Description = 'Start Area11' },
        @{ Name = '! RUN TESTS.lnk'; Target = 'RUN-TESTS.bat'; Description = 'Run the Area11 tests' }
    )) {
        $path = Join-Path $root $item.Name
        $target = Join-Path $root $item.Target
        if ((Test-Path -LiteralPath $path) -or -not (Test-Path -LiteralPath $target -PathType Leaf)) { continue }
        $shortcut = $shell.CreateShortcut($path)
        $shortcut.TargetPath = $target
        $shortcut.WorkingDirectory = $root
        $shortcut.IconLocation = "$icon,0"
        $shortcut.Description = $item.Description
        $shortcut.Save()
    }
} catch {
    Write-Error 'Could not create the optional Area11 shortcuts. The .bat files still work.'
    exit 1
}
