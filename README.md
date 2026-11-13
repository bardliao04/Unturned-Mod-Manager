# Unturned 模組管理器 v1.3

在 Windows 安裝、更新與移除相容的 Unturned 漢化及模組，支援玩家遊戲與本機可存取的專用伺服器。使用 OpenAI GPT 協助開發與撰寫說明。

## 下載與使用

在 [GitHub Releases](https://github.com/bardliao04/Unturned-Mod-Manager/releases/latest) 的 Assets 下載 **Unturned-Mod-Manager_v1.3.zip**。完整解壓縮，保留同名 .exe.config，再執行 Unturned模組管理器.exe。需要 Windows 10／11 與 .NET Framework 4.8。

1. 選擇玩家遊戲或 Unturned 伺服器，核對目標位置。
2. 等 Steam 完成下載後重新掃描，或匯入相容的 ZIP。
3. 關閉遊戲或伺服器，勾選模組並執行安裝、更新或移除。安裝漢化後，請依套件說明設定 Steam 啟動選項。

[完整玩家說明](docs/PLAYER-README.zh-TW.md)

## v1.3 更新

- 統一搜尋列、工具列與詳情按鈕的間距，修正側邊導覽展開和收合時的框線缺口。
- 閒置時底部只顯示狀態，執行時才展開進度條。點狀態可查看操作詳情。
- 保留安裝、更新、移除及必要的還原入口。改善批次作業的進度回饋與取消行為，安裝紀錄與備份仍供還原使用。

## 開發與驗證

使用 Windows PowerShell 在此資料夾執行 `.\build.ps1 -Test`。執行檔與設定檔輸出到 dist，測試資料與截圖存於 TestResults。建立本機玩家 ZIP 可使用 `tools/Publish-Manager.ps1`。

模組作者可參考 [模組名稱與識別資訊格式](docs/MOD-METADATA-v1.md)。歷史版本與下載檔案見 [發行紀錄](https://github.com/bardliao04/Unturned-Mod-Manager/releases)。
