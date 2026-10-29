param(
 [Parameter(Mandatory=$true)][string]$PackageFolder,
 [Parameter(Mandatory=$true)][string]$MetadataFile,
 [switch]$Force
)
$ErrorActionPreference='Stop'
$packagePath=(Resolve-Path -LiteralPath $PackageFolder).Path
$inputPath=(Resolve-Path -LiteralPath $MetadataFile).Path
foreach($pathToCheck in @($packagePath,$inputPath)){
 $ancestor=$pathToCheck
 while($ancestor){
  if((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Symbolic links are not supported: $ancestor"}
  $ancestor=Split-Path -Parent $ancestor
 }
}
if((Get-Item -LiteralPath $inputPath).Length -gt 262144){throw 'Metadata exceeds 256 KiB.'}
$document=Get-Content -LiteralPath $inputPath -Raw -Encoding UTF8 | ConvertFrom-Json
if($document.schemaVersion -ne 1 -or @($document.mods).Count -eq 0 -or @($document.mods).Count -gt 128){throw 'Expected schemaVersion=1 and 1..128 mods.'}
$nativePath=Join-Path $packagePath 'unturned-manager.json'
$scopes=@{}
if(Test-Path -LiteralPath $nativePath){
 $native=Get-Content -LiteralPath $nativePath -Raw -Encoding UTF8 | ConvertFrom-Json
 $scopes[$native.id]=@{scope=$native.scopeId;kind=$native.kind}
}else{
 $dataPath=Join-Path $packagePath 'Data'
 if(-not(Test-Path -LiteralPath (Join-Path $dataPath 'version.json'))){$dataPath=$packagePath}
 $version=Get-Content -LiteralPath (Join-Path $dataPath 'version.json') -Raw -Encoding UTF8 | ConvertFrom-Json
 if($version.product -ne 'Unturned.AI.Tchinese'){throw 'Unsupported legacy package.'}
 $manifestPath=Join-Path $dataPath 'manifest.json'
 if((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -ne $version.manifest_sha256){throw 'Legacy manifest hash mismatch.'}
 $manifest=Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
 $identity='unturned.ai.tchinese'
 if($version.package_id -and $version.package_id -notin @('official','base','core')){$identity+='.'+$version.package_id}
 foreach($file in $manifest | Where-Object {$_.group -ne 'escalation'}){
  if($file.target -match '^Workshop/(\d{1,20})/'){$scopeId=$Matches[1];$scopes[($identity+'.workshop.'+$scopeId)]=@{scope=$scopeId;kind='localization'}}
  else{$scopes[$identity]=@{scope=$null;kind='localization'}}
 }
}
$seen=@{}
foreach($mod in $document.mods){
 if(-not $mod -or -not $scopes.ContainsKey($mod.modId) -or $seen.ContainsKey($mod.modId)){throw 'Unknown or duplicate modId.'}
 $seen[$mod.modId]=$true
 if($mod.workshopId -and $mod.workshopId -notmatch '^\d{1,20}$'){throw 'Invalid workshopId.'}
 $expected=$scopes[$mod.modId]
 if($expected.scope -and $mod.workshopId -cne $expected.scope){throw 'Workshop scope mismatch.'}
 if($mod.type -notin @('module','localization','map_translation') -or ($expected.kind -eq 'module') -ne ($mod.type -eq 'module')){throw 'Metadata type does not match installation kind.'}
 if($mod.type -eq 'map_translation' -and -not $mod.targetMap){throw 'Map translation requires targetMap.'}
 if($mod.targetMap){
  if([string]::IsNullOrWhiteSpace($mod.targetMap.name) -or $mod.targetMap.name.Length -gt 200 -or $mod.targetMap.name -match '\p{Cc}'){throw 'Invalid map name.'}
  if($mod.targetMap.workshopId -and $mod.targetMap.workshopId -notmatch '^\d{1,20}$'){throw 'Invalid target map ID.'}
 }
 $names=@($mod.displayNames.PSObject.Properties)
 if($names.Count -eq 0){throw 'displayNames is empty.'}
 foreach($name in $names){if($name.Name -notin @('zh-TW','zh-CN','en-US') -or [string]::IsNullOrWhiteSpace($name.Value) -or $name.Value.Length -gt 200 -or $name.Value -match '\p{Cc}'){throw 'Invalid display name or language code.'}}
}
$outputPath=Join-Path $packagePath 'unturned-manager.metadata.json'
if((Test-Path -LiteralPath $outputPath) -and -not $Force){throw 'Metadata exists. Review it first; use -Force to replace it.'}
if((Test-Path -LiteralPath $outputPath) -and ((Get-Item -LiteralPath $outputPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Output cannot be a symbolic link.'}
$json=$document | ConvertTo-Json -Depth 12
if([Text.Encoding]::UTF8.GetByteCount($json) -gt 262144){throw 'Output exceeds 256 KiB.'}
[IO.File]::WriteAllText($outputPath,$json,(New-Object Text.UTF8Encoding($false)))
Write-Output "Wrote display metadata only: $outputPath"
