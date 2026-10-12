$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$root = Split-Path -Parent $PSScriptRoot
$sources = @(Get-ChildItem -LiteralPath "$root\src" -Filter '*.cs' | ForEach-Object FullName)
& $compiler /nologo /target:exe /main:UiAcceptanceTests "/out:$root\UiAcceptanceTests.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll "/r:$framework\PresentationCore.dll" "/r:$framework\PresentationFramework.dll" "/r:$framework\WindowsFormsIntegration.dll" "/r:$framework\UIAutomationClient.dll" "/r:$framework\UIAutomationTypes.dll" "/r:$framework\WindowsBase.dll" $sources "$PSScriptRoot\UiAcceptanceTests.cs" "$PSScriptRoot\DemoSlides.cs"
if ($LASTEXITCODE -ne 0) { throw 'UI test build failed' }
& "$root\UiAcceptanceTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'UI tests failed' }
& $compiler /nologo /target:winexe /main:UiAcceptanceTests "/out:$root\UiPreview.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll "/r:$framework\PresentationCore.dll" "/r:$framework\PresentationFramework.dll" "/r:$framework\WindowsFormsIntegration.dll" "/r:$framework\UIAutomationClient.dll" "/r:$framework\UIAutomationTypes.dll" "/r:$framework\WindowsBase.dll" $sources "$PSScriptRoot\UiAcceptanceTests.cs" "$PSScriptRoot\DemoSlides.cs"
if ($LASTEXITCODE -ne 0) { throw 'Preview build failed' }
