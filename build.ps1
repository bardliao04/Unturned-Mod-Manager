param([switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
& (Join-Path $projectRoot 'tools\verify-localization.ps1') -Root $projectRoot
$outputDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler not found.' }
$sources = @('Core.cs','App.cs','Versions.cs','Updates.cs','Tests.cs','Localization.cs','Experience.cs','ExperienceTests.cs','Dialogs.cs','PackageModels.cs','Scanning.cs','ModernWorkflow.cs','WorkflowTests.cs','Themes.cs','Metadata.cs','Dashboard.cs','Batch.cs','DashboardTests.cs','Responsive.cs','StyledControls.cs','ResponsiveTests.cs','Reconciliation.cs','ReconciliationUi.cs','ReconciliationTests.cs','ManualInstallations.cs') | ForEach-Object { Join-Path $projectRoot "src\$_" }
$resources = @('zh-TW','en-US','zh-CN') | ForEach-Object { '/resource:' + (Join-Path $projectRoot "src\Resources\$_.json") + ',UnturnedModManager.Resources.' + $_ + '.json' }
$manifest = "/win32manifest:" + (Join-Path $projectRoot "src\app.manifest")
$exe = Join-Path $outputDir 'Unturned模組管理器.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $sources $resources $manifest
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\app.config') -Destination "$exe.config" -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $outputDir '使用說明.md') -Force
if (Test-Path -LiteralPath (Join-Path $projectRoot 'UIUX-驗證報告.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'UIUX-驗證報告.md') -Destination $outputDir -Force }
if (Test-Path -LiteralPath (Join-Path $projectRoot 'LIGHT-WORKSHOP-驗證報告.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'LIGHT-WORKSHOP-驗證報告.md') -Destination $outputDir -Force }
if (Test-Path -LiteralPath (Join-Path $projectRoot 'DASHBOARD-驗證報告.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'DASHBOARD-驗證報告.md') -Destination $outputDir -Force }
if (Test-Path -LiteralPath (Join-Path $projectRoot 'GUIDE-PERFORMANCE-驗證報告.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'GUIDE-PERFORMANCE-驗證報告.md') -Destination $outputDir -Force }
if (Test-Path -LiteralPath (Join-Path $projectRoot 'RECONCILIATION-驗證報告.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'RECONCILIATION-驗證報告.md') -Destination $outputDir -Force }
if (Test-Path -LiteralPath (Join-Path $projectRoot 'STARTUP-FIX-驗證報告.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'STARTUP-FIX-驗證報告.md') -Destination $outputDir -Force }
if (Test-Path -LiteralPath (Join-Path $projectRoot 'COMMAND-BAR-FIX-驗證報告.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'COMMAND-BAR-FIX-驗證報告.md') -Destination $outputDir -Force }
if (Test-Path -LiteralPath (Join-Path $projectRoot 'VERIFIED-HANDOVER-驗證報告.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'VERIFIED-HANDOVER-驗證報告.md') -Destination $outputDir -Force }
if (Test-Path -LiteralPath (Join-Path $projectRoot 'AUTO-GUIDE-THEME-驗證報告.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'AUTO-GUIDE-THEME-驗證報告.md') -Destination $outputDir -Force }
if ($Test) {
    $testExe = Join-Path $projectRoot 'TestResults\CoreTests.exe'
    New-Item -ItemType Directory -Path (Split-Path $testExe) -Force | Out-Null
    & $compiler /nologo /target:exe /platform:anycpu "/out:$testExe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $sources $resources $manifest
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src\app.config') -Destination "$testExe.config" -Force
    & $testExe --self-test (Join-Path $projectRoot 'TestResults')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & $testExe --workflow-test (Join-Path $projectRoot 'TestResults')
    if ($LASTEXITCODE -ne 0) { throw 'Workflow tests failed.' }
    & $testExe --dashboard-test (Join-Path $projectRoot 'TestResults')
    if ($LASTEXITCODE -ne 0) { throw 'Dashboard tests failed.' }
    & $testExe --responsive-test (Join-Path $projectRoot 'TestResults')
    if ($LASTEXITCODE -ne 0) { throw 'Responsive tests failed.' }
    & $testExe --reconcile-test (Join-Path $projectRoot 'TestResults')
    if ($LASTEXITCODE -ne 0) { throw 'Reconciliation tests failed.' }
    & $testExe --ui-test (Join-Path $projectRoot 'TestResults')
    if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
}
Write-Output "Built: $exe"
