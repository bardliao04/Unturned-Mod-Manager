param(
    [Parameter(Mandatory=$true)][string]$Payload,
    [Parameter(Mandatory=$true)][string]$Id,
    [Parameter(Mandatory=$true)][string]$Name,
    [Parameter(Mandatory=$true)][version]$Version,
    [ValidateSet('module','localization')][string]$Kind = 'module',
    [string]$Description = '',
    [string]$AfterInstall = '',
    [string]$AfterInstallServer = '',
    [Parameter(Mandatory=$true)][string]$Output
)
$ErrorActionPreference = 'Stop'
if ($Id -notmatch '^[a-z0-9][a-z0-9.-]{0,79}$') { throw 'Invalid package id.' }
$payloadRoot = (Resolve-Path -LiteralPath $Payload).Path.TrimEnd('\')
$packageRoot = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $packageRoot) { throw 'Output already exists; use a new output folder.' }
if ($packageRoot.StartsWith($payloadRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must not be inside payload.' }
$payloadFiles = Get-ChildItem -LiteralPath $payloadRoot -Recurse -File
if ($payloadFiles.Count -eq 0) { throw 'Payload is empty.' }
if (Get-ChildItem -LiteralPath $payloadRoot -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Links are not supported.' }
New-Item -ItemType Directory -Path $packageRoot | Out-Null
Copy-Item -LiteralPath $payloadRoot -Destination (Join-Path $packageRoot 'payload') -Recurse
$files = @($payloadFiles | ForEach-Object {
    $relative = $_.FullName.Substring($payloadRoot.Length + 1).Replace('\','/')
    [ordered]@{source="payload/$relative"; target=$relative; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
$manifest = [ordered]@{schemaVersion=1;id=$Id;name=$Name;version=$Version.ToString();kind=$Kind;description=$Description;afterInstall=$AfterInstall;afterInstallServer=$AfterInstallServer;files=$files}
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $packageRoot 'unturned-manager.json'), ($manifest | ConvertTo-Json -Depth 6), $utf8)
@"
手動安裝：先關閉遊戲，將 payload 內的資料夾複製到包含 Unturned.exe 的遊戲目錄。
建議先備份同名檔案。手動安裝不會自動建立管理器的解除安裝紀錄。
管理器安裝：工作坊內容下載完成後，開啟管理器並重新掃描，勾選此 mod 後按安裝。請保留此安裝包的完整資料夾結構。
$AfterInstall
"@ | Set-Content -LiteralPath (Join-Path $packageRoot '手動安裝說明.txt') -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = $packageRoot + '.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $zipPath)
Write-Output $zipPath
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
