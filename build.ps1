param([switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$outputDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler not found.' }
$sources = @('Core.cs','App.cs','Versions.cs','Updates.cs','Tests.cs') | ForEach-Object { Join-Path $projectRoot "src\$_" }
$exe = Join-Path $outputDir 'Unturned模組管理器.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $outputDir '使用說明.md') -Force
if ($Test) {
    $testExe = Join-Path $projectRoot 'TestResults\CoreTests.exe'
    New-Item -ItemType Directory -Path (Split-Path $testExe) -Force | Out-Null
    & $compiler /nologo /target:exe /platform:anycpu "/out:$testExe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & $testExe --self-test (Join-Path $projectRoot 'TestResults')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Write-Output "Built: $exe"
