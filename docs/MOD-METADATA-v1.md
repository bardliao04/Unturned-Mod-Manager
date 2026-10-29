# 模組名稱 Metadata v1 正式契約

檔名固定為 `unturned-manager.metadata.json`。此為作者提供的顯示資訊，不含任何安裝指令、路徑、程式或版本覆寫。

## 放置與結構

優先讀取安裝包根目錄，其次讀取 `Data/unturned-manager.metadata.json`；兩者同時存在時根目錄優先。原生 `unturned-manager.json` 與舊版漢化 `Data/version.json`、`manifest.json` 都不需要修改。ZIP 匯入會一併保留此檔。

外層固定為 `{ "schemaVersion": 1, "mods": [ ... ] }`。使用陣列是因為同一漢化包可能有本體、地圖及獨立資源多個安裝範圍。附件中單筆物件是格式提案；正式契約改用此陣列，避免遺漏 California 2 第二個範圍。

每筆欄位：

| 欄位 | 規則 |
| --- | --- |
| modId | 必須與管理器解析出的模組 ID 完全相同，區分大小寫，不可重複 |
| workshopId | `null` 或 1–20 位數字字串；有 scopeId 時必須等於該安裝範圍 ID |
| type | `module`、`localization` 或 `map_translation`；不得與既有安裝類型矛盾 |
| targetMap | 可選 `{ name, workshopId }`；map_translation 必填；name 為作者提供的地圖名稱 |
| displayNames | zh-TW、en-US、zh-CN 的名稱字典；至少一種，建議全部提供 |

名稱及地圖名稱須為 1–200 字元，不能只有空白或含控制字元。檔案最多 256 KiB、128 筆。未知語言代碼或 schemaVersion 不會被當成已支援資料。未知額外欄位不具執行效果。

同一文件可以涵蓋多個範圍。每個已掃描安裝範圍必須有匹配的記錄；缺少匹配、重複 ID、錯誤範圍、格式錯誤或符號連結會忽略該範圍的名稱 Metadata，保留有效安裝包並在問題中心提示。這是格式與身分核對，並非作者數位簽章認證。

## 識別與顯示

原生模組直接使用 manifest.id。舊版本體使用 `unturned.ai.tchinese`；非本體 package_id 使用 `unturned.ai.tchinese.<package_id>`，工作坊範圍再加 `.workshop.<scopeId>`。正式範例見 [California 2](metadata/california2.metadata.json)，包含地圖 3707778928 與資源 3711646503。

顯示回退：目前語言 → zh-TW → 原作者名稱 → modId。不根據資料夾名稱、Workshop ID 猜測地圖名稱，也不提供使用者改名功能。沒有可靠 Metadata 時保留原作者名稱，不能保證所有舊包都有三語名稱。

搜尋在記憶體內比對所有語言名稱、原名、模組 ID、workshopId、targetMap.name，以及既有版本與分類。切換語言不讀取模組目錄。Metadata 與包清單一起快取；重新掃描會依檔案大小／修改時間判斷失效，安裝前仍重新讀取及驗證實際內容。

名稱不會寫回 manifest、變更版本判斷、合併模組、修改登錄身分或改動安裝路徑。來源消失後若只剩舊安裝紀錄，會回退至紀錄中的原始名稱。

## 漢化模組專案整合

另一個專案應在打包完成、ZIP 壓縮前，提供經作者確認的完整三語名稱，使用本契約及本專案產生工具：

```powershell
./tools/Write-ModMetadata.ps1 -PackageFolder '<待發布安裝包根目錄>' -MetadataFile './docs/metadata/california2.metadata.json'
```

工具會核對該包解析出的 ID、範圍與類型，只新增名稱交換檔，不改原生資料或 manifest 雜湊。已存在時必須明確指定 `-Force`。這是作者／打包流程使用的工具；管理器不會自動替玩家改寫 Workshop 來源。

本次未自動覆寫其他專案的正式包、工作坊資料或已發布 ZIP。範例已在隔離複本驗證；將工具接入另一專案的發布腳本後，新包即可自動帶入相同格式。
