# Pixer Electron GUI

這是一個基於 Electron 的圖形化界面，用於操作 Pixer Upload 工具。它提供了友好的用戶界面來上傳圖片到 Pixer 裝置。

## 功能特色

- 🖼️ **圖片選擇**: 支援多種圖片格式（JPEG、PNG、BMP、GIF）
- 📤 **一鍵上傳**: 簡單直觀的上傳流程
- 🔍 **裝置檢查**: 檢查 Pixer 裝置連線狀態
- 🖥️ **跨平台**: 支援 Windows 和 macOS
- 📊 **即時進度**: 顯示上傳進度和詳細日誌
- ⚡ **自動檢測**: 自動選擇對應平台的執行檔

## 系統需求

- **Windows**: Windows 10 或更高版本
- **macOS**: macOS 10.14 或更高版本
- **網路**: 需要連接到 Pixer 裝置的 WiFi 熱點 (192.168.1.1)

## 安裝和使用

### 開發模式

1. 確保已安裝 Node.js (建議 16.x 或更高版本)
2. 安裝依賴：
   ```bash
   npm install
   ```
3. 啟動開發模式：
   ```bash
   npm run dev
   ```

### 建置應用程式

建置所有平台：
```bash
npm run build-all
```

建置 Windows 版本：
```bash
npm run build-win
```

建置 macOS 版本：
```bash
npm run build-mac
```

建置完成的應用程式會在 `dist` 目錄中。

## 使用說明

### 1. 檢查裝置
- 確保已連接到 Pixer 裝置的 WiFi 熱點
- 點擊「檢查裝置」按鈕確認連線狀態

### 2. 選擇圖片
- 點擊「選擇圖片」按鈕
- 選擇要上傳的圖片檔案
- 支援的格式：JPEG、PNG、BMP、GIF

### 3. 上傳圖片
- 選擇圖片後，「上傳圖片」按鈕會啟用
- 點擊按鈕開始上傳
- 可以在進度區域查看即時狀態

## 技術架構

### 主要組件

- **main.js**: Electron 主進程，處理系統級操作
- **preload.js**: 安全的 IPC 通信橋樑
- **renderer.js**: 渲染進程，處理用戶界面邏輯
- **index.html**: 主要用戶界面
- **style.css**: 界面樣式

### 跨平台支援

應用程式會自動檢測運行平台：
- **Windows**: 使用 `tools/PixerUpload.exe`
- **macOS**: 使用 `tools/PixerUpload`

### 安全性

- 使用 `contextIsolation` 和 `preload.js` 確保安全的 IPC 通信
- 禁用 `nodeIntegration` 防止安全漏洞
- 所有系統操作都在主進程中執行

## 疑難排解

### 常見問題

**Q: 找不到執行檔**
A: 確保 `tools` 目錄中包含對應平台的執行檔，並且在 macOS 上有執行權限。

**Q: 無法連接到裝置**
A: 
- 確保已連接到 Pixer 裝置的 WiFi 熱點 (192.168.1.1)
- 檢查防火牆設定是否阻擋 6000 埠
- 嘗試點擊「檢查裝置」確認連線狀態

**Q: 圖片上傳失敗**
A:
- 確保圖片格式受支援
- 檢查圖片檔案是否損壞
- 確認裝置有足夠的儲存空間

**Q: macOS 上提示「無法打開應用程式」**
A: 在系統偏好設定 > 安全性與隱私權中允許應用程式執行。

### 開發除錯

啟用開發者工具：
```bash
npm run dev
```

查看日誌：
- 主進程日誌會在終端中顯示
- 渲染進程日誌可在開發者工具的 Console 中查看

## 授權

與原始 Pixer 專案相同的授權條款。

## 貢獻

歡迎提交 Issue 和 Pull Request 來改善這個項目。

## 更新日誌

### v1.0.0
- 初始版本
- 支援圖片選擇和上傳
- 跨平台支援 (Windows/macOS)
- 裝置狀態檢查
- 即時進度顯示
