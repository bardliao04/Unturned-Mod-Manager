# Unturned 模組管理器

協助玩家安裝、更新及移除相容的 Unturned 漢化與 mod。讀取原本的手動安裝包，不需另外下載管理器專用資源包。

## 下載

[下載最新版管理器](https://github.com/bardliao04/Unturned-Mod-Manager/releases/latest)

在下載頁的 **Assets** 點選 `Unturned模組管理器_v1.0.zip`。完整解壓縮後開啟 `Unturned模組管理器.exe`；Source code 並非管理器程式。

需要 Windows 10／11 與 .NET Framework 4.8。

## 開始使用

1. 選擇安裝目標：玩家遊戲或 Unturned 伺服器。
2. 自動尋找或手動選擇遊戲／伺服器資料夾。
3. 掃描已下載的工作坊內容、選擇本機 mod 來源，或匯入相容 ZIP。
4. 關閉遊戲／伺服器，勾選 mod，按「安裝／更新勾選mod」。
5. 依 mod 作者提供的使用說明啟動遊戲。

匯入 ZIP 只會加入清單，仍須執行安裝。滑鼠停留在按鈕上可查看提示。

## 支援內容

- 附有有效 `unturned-manager.json` 的 mod 安裝包。
- 含相容 `Data/version.json` 與 `Data/manifest.json` 的漢化安裝包。
- 適用伺服器的原生 Modules 模組。

其他作者使用相容格式的 mod 也可匯入。管理器不會猜測一般 DLL、地圖或物品 ZIP 的安裝方式。

## 備份與復原

安裝前驗證檔案，保存備份與安裝紀錄。支援解除安裝與復原未完成的操作。

資料位於 `%LOCALAPPDATA%\UnturnedModManager`，請保留以便還原。已安裝檔案遭其他程式修改時，更新或移除會停止並提示衝突。

## 使用範圍

管理器不附 mod 資源、不自動訂閱、不啟動遊戲、不更改 BattlEye 設定，也不執行安裝包中的 EXE 或腳本。

伺服器功能操作本機可存取的 Windows 檔案，不提供遠端上傳或啟停控制。各 mod 的遊戲模式與相容性，請依作者說明確認。

## 問題回報

請提供管理器版本、mod 名稱、玩家或伺服器目標、操作步驟，以及「操作紀錄」中的完整錯誤提示。
