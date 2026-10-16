$ErrorActionPreference = 'Stop'
$releaseMetadata = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'public-release.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$versionSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\VersionInfo.cs') -Raw -Encoding UTF8
if (-not $versionSource.Contains('[assembly: AssemblyInformationalVersion("' + $releaseMetadata.version + '")]')) { throw 'Executable version does not match public-release.json' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /target:exe /main:ExportBrand "/out:$PSScriptRoot\tests\ExportBrand.exe" /r:System.dll /r:System.Drawing.dll /r:System.Xaml.dll "/r:$framework\PresentationCore.dll" "/r:$framework\PresentationFramework.dll" "/r:$framework\WindowsBase.dll" "$PSScriptRoot\src\BrandDrawing.cs" "$PSScriptRoot\src\BrandIcon.cs" "$PSScriptRoot\tests\ExportBrand.cs"
if ($LASTEXITCODE -ne 0) { throw 'Brand build failed' }
& "$PSScriptRoot\tests\ExportBrand.exe" $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'Brand export failed' }
& $compiler /nologo /target:winexe /platform:x64 "/win32icon:$PSScriptRoot\SaveLecture.ico" /optimize+ "/out:$PSScriptRoot\SaveLecture.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll "/r:$framework\PresentationCore.dll" "/r:$framework\PresentationFramework.dll" "/r:$framework\WindowsFormsIntegration.dll" "/r:$framework\UIAutomationClient.dll" "/r:$framework\UIAutomationTypes.dll" "/r:$framework\WindowsBase.dll" $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
