param(
 [Parameter(Mandatory=$true)][string]$Source,
 [string]$Output
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$exe=Join-Path $root 'dist\Unturned模組管理器.exe'
if(!(Test-Path -LiteralPath $exe)){throw '請先執行 build.ps1。'}
if(!$Output){$Output=([IO.Path]::GetFullPath($Source).TrimEnd('\')+'-管理器精簡包.zip')}
$process=Start-Process -FilePath $exe -ArgumentList @('--export-compact',('"'+$Source+'"'),('"'+$Output+'"')) -Wait -PassThru -WindowStyle Hidden
if($process.ExitCode -ne 0){throw '無法匯出：請檢查來源清單、檔案雜湊及輸出檔是否已存在。'}
Get-Item -LiteralPath $Output | Select-Object FullName,Length
