$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'SaveLecture.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build SaveLecture first.' }
$startup = [Environment]::GetFolderPath('Startup')
$linkPath = Join-Path $startup 'SaveLecture.lnk'
if (Test-Path -LiteralPath $linkPath) {
    $existing = (New-Object -ComObject WScript.Shell).CreateShortcut($linkPath)
    if ($existing.TargetPath -ne $exe) { throw 'Existing SaveLecture startup shortcut points elsewhere; it was preserved.' }
}
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($linkPath)
$link.TargetPath = $exe
$link.WorkingDirectory = $PSScriptRoot
$link.Arguments = '--background'
$link.WindowStyle = 7
$link.Description = 'SaveLecture personal lecture recorder'
$link.Save()
Write-Output $linkPath
