param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(!$SkipBuild){& (Join-Path $root 'build.ps1')}
$binaryVersion=[version][Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root "dist\Unturned模組管理器.exe")).FileVersion
if($binaryVersion.Build -gt 0 -or $binaryVersion.Revision -gt 0){throw '公開版號只允許主版號與次版號，請先修正程式版本。'}
$version=$binaryVersion.ToString(2)
$folder=Join-Path (Split-Path -Parent $root) ("模組管理器_v"+$version)
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$files=@('Unturned模組管理器.exe','Unturned模組管理器.exe.config','使用說明.md')
foreach($name in $files){Copy-Item -LiteralPath (Join-Path $root "dist\$name") -Destination (Join-Path $folder $name) -Force}
$archive=Join-Path (Split-Path -Parent $root) ("Unturned-Mod-Manager_v"+$version+".zip")
Compress-Archive -LiteralPath @($files | ForEach-Object {Join-Path $folder $_}) -DestinationPath $archive -Force
Get-Item -LiteralPath $archive | Select-Object FullName,Length
