param([string]$Root=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
$tables=@{}
foreach($code in @('zh-TW','en-US','zh-CN')){
 $tables[$code]=Get-Content -Raw -Encoding UTF8 (Join-Path $Root "src\Resources\$code.json") | ConvertFrom-Json
}
$baseNames=@($tables['zh-TW'].PSObject.Properties.Name | Sort-Object)
foreach($code in $tables.Keys){
 $names=@($tables[$code].PSObject.Properties.Name | Sort-Object)
 if(Compare-Object $baseNames $names){throw "Language key mismatch: $code"}
 foreach($key in $baseNames){
  $value=$tables[$code].$key
  if([string]::IsNullOrWhiteSpace($value)){throw "Empty translation: $code / $key"}
  $expected=@([regex]::Matches($tables['zh-TW'].$key,'\{\d+\}') | ForEach-Object {$_.Value} | Sort-Object -Unique)
  $actual=@([regex]::Matches($value,'\{\d+\}') | ForEach-Object {$_.Value} | Sort-Object -Unique)
  if(($expected -join '|') -ne ($actual -join '|')){throw "Translation placeholder mismatch: $code / $key"}
 }
}
foreach($source in Get-ChildItem -LiteralPath (Join-Path $Root 'src') -Filter *.cs | Where-Object { $_.Name -notlike '*Tests.cs' }){
 $content=Get-Content -Raw -Encoding UTF8 -LiteralPath $source.FullName
 foreach($match in [regex]::Matches($content,'L\.Get\("([^\"]+)"\)')){
  $key=$match.Groups[1].Value
  if($baseNames -notcontains $key){throw "Unknown resource key $key in $($source.Name)"}
 }
}
Write-Output "Localization verified: $($baseNames.Count) keys in each language; placeholders and literal references match."
