# 啟動時 SmoothGrid 溢位修正

日期：2026-10-30。修正基準為 `bab3ac3`，另建 `fix/native-message-overflow` 分支保留原版本。

## 原因與修正

先前勾選手勢的鍵盤判斷先執行 `(int)m.WParam`，才判斷訊息是否為鍵盤事件。在 64 位元程序中，其他 Windows 訊息可以攜帶超出 Int32 範圍的參數；表格選中核取方塊欄位後，就可能在啟動或其他一般訊息處理時拋出 OverflowException。

已先用新的回歸測試在舊程式重現相同的 `SmoothGrid.WndProc` OverflowException，再修正為先確認鍵盤訊息種類、使用 `IntPtr.ToInt64()` 比較空白鍵。下拉控制項的同類鍵盤比較也統一使用指標寬度，不用捕捉並隱藏例外的方式略過問題。

這是管理器自身的缺陷，不需要啟用 JIT、修改系統設定或重新安裝 .NET。

## 驗證

- Windows `build.ps1 -Test` 成功，共 458 項：核心 73、版本／來源 23、主題／批次 92、回應速度 29、接續與輸入 60、三語介面 181。
- 新增 3 項回歸：64 位元非鍵盤參數不溢位且不誤勾選、負滾輪資料不經不安全的 IntPtr-to-Int32 轉換、空白鍵仍只切換一次。
- 原有單項滑鼠、版本群組、子版本、三語、常駐底部按鈕、縮小版面、備份與接續測試持續通過。
- 修正前重現與修正後完整輸出保留在本機 TestResults/native-overflow-before.log、TestResults/native-overflow-validation.log；本次 60 項輸入／接續證據在 docs/startup-fix-regression-results.txt。
- 未操作使用者實際遊戲、模組、安裝紀錄或備份。

## 修改檔案

- src/StyledControls.cs：限制訊息判斷範圍並保留指標寬度。
- src/ReconciliationTests.cs：原生訊息回歸探測及 3 項測試。
- build.ps1：建置時附上本報告。
- 本報告與 docs/startup-fix-regression-results.txt。

## 使用修正版

下載 Unturned-Mod-Manager-Startup-Fix-preview.zip，完整解壓後執行其中的 Unturned模組管理器.exe。既有設定與備份位置不變；無須刪除或清空原資料。

此修正針對已重現的溢位路徑。尚未在使用者同一個執行環境手動重播完整啟動過程，也沒有執行真正的遊戲安裝或遊戲內驗證。
