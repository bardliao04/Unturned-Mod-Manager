# 底部操作列消失修正

日期：2026-10-30。從 a5bd9d1 建立 fix/persistent-command-bar 分支保留上一版；包含上一版的啟動溢位修正。

## 原因與修正

底部操作列從 ScrollSurface 捲動容器移到主版面後，仍保留該容器設定的 Control.Region。啟動時這個舊區域只剩 2 像素高；按鈕的 Visible 與 Bounds 正常，但實際 Windows 視窗被裁掉。先前只檢查屬性及 DrawToBitmap，無法排除此種原生裁切。

已在舊版重現：COMMAND REGION 為 {X=0,Y=0,Width=1250,Height=2}，而操作列 client 高度為 47。新增測試先因殘留 Region 失敗，再清除並釋放已不適用的裁切區，修正後 Region 為 none。

修正只在將底部操作列移出捲動容器時清除舊 Region。按鈕保留原有操作、忙碌時停用、窄視窗換行及全寬進度；主清單與其他捲動區的裁切仍保留。

## 驗證

- Windows build.ps1 -Test 成功，共 462 項：核心 73、版本／來源 23、主題／批次 92、回應速度 29、接續與輸入 64、三語介面 181。
- 新增 4 項：移出容器不保留 Region、Windows GetWindowRgn 確認操作列無原生裁切、1366×768 及 1000×680 縮放後仍無裁切。
- 三語、展開／收合導覽、底部按鈕換行、單項勾選、上一版原生訊息溢位、安裝／回滾／還原檢查持續通過。
- 新增證據：docs/command-bar-fix-results.txt。本機修正前與完整修正後輸出：TestResults/command-bar-before.log、TestResults/command-bar-final-validation.log。
- 實際桌面截圖工具在此執行環境無法啟動，因此未完成原生桌面截圖驗收；已改以 Windows 原生裁切區查詢驗證，沒有把 DrawToBitmap 當成實際畫面驗收。
- 未更動使用者實際遊戲、模組、安裝紀錄與備份。

## 修改檔案

- src/ReconciliationUi.cs：移除操作列的殘留 Region 並釋放舊區域。
- src/ReconciliationTests.cs：Region 與原生 GetWindowRgn 回歸檢查。
- build.ps1：建置附上本報告。
- 本報告及 docs/command-bar-fix-results.txt。

## 使用

完整解壓 Unturned-Mod-Manager-Command-Bar-Fix-preview.zip，執行其中的 Unturned模組管理器.exe。此包同時保留前一版啟動修正，無須清空設定或備份。
