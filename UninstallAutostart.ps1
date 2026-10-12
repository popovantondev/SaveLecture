$ErrorActionPreference = 'Stop'
$linkPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'SaveLecture.lnk'
if (Test-Path -LiteralPath $linkPath) {
    $link = (New-Object -ComObject WScript.Shell).CreateShortcut($linkPath)
    if ($link.TargetPath -ne (Join-Path $PSScriptRoot 'SaveLecture.exe')) { throw 'The shortcut points to another copy and was preserved.' }
    Remove-Item -LiteralPath $linkPath
}
