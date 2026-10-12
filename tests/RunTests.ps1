$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$root = Split-Path -Parent $PSScriptRoot
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /target:exe /main:AcceptanceTests /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll "/r:$framework\PresentationCore.dll" "/r:$framework\PresentationFramework.dll" "/r:$framework\WindowsFormsIntegration.dll" "/r:$framework\UIAutomationClient.dll" "/r:$framework\UIAutomationTypes.dll" "/r:$framework\WindowsBase.dll" "/out:$PSScriptRoot\AcceptanceTests.exe" $sources (Join-Path $PSScriptRoot 'AcceptanceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
& (Join-Path $PSScriptRoot 'AcceptanceTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Acceptance tests failed' }
