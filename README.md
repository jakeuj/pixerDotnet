# Pixer Upload - C# .NET 9 版本
本專案是基於 [kasperis7/pixer](https://github.com/kasperis7/pixer) 的 Python 版本，將其完整轉換為 C# .NET 9 版本。

![image.png](image.png)

## 功能特色

- **完全相容**：與原始 Python 版本產生相同的二進位輸出
- **圖片轉換**：支援 1872x1404 解析度，4位元灰階封裝格式
- **Socket 通訊**：透過 TCP 在 192.168.1.1:6000 與 Pixer 裝置通訊
- **韌體升級**：支援 BLE、ITE、BSP 韌體升級功能
- **非同步處理**：使用現代 C# async/await 模式

## 系統需求

- .NET 9.0 或更高版本
- Windows、macOS 或 Linux

## 安裝和建置

1. 確保已安裝 .NET 9.0 SDK
2. 在專案目錄中執行：

```bash
dotnet restore
dotnet build
```

## 使用方式

### 基本圖片上傳

```bash
dotnet run <圖片路徑>
```

例如：
```bash
dotnet run image.jpg
dotnet run /path/to/your/image.png
```

### 僅檢查裝置狀態

```bash
dotnet run
```

## 專案結構

- `Program.cs` - 主程式進入點
- `ImgConverter.cs` - 圖片轉換類別
- `SocketClient.cs` - TCP Socket 通訊類別
- `MainActivity.cs` - 主要活動控制類別
- `FirmwareUpgrade.cs` - 韌體升級功能
- `PixerUpload.csproj` - 專案設定檔

## 核心類別說明

### ImgConverter
- 處理圖片縮放、裁切和灰階轉換
- 實作 4位元灰階封裝（每位元組包含2個像素）
- 產生協定標頭：`#file#000801314144imagebin`

### SocketClient
- 管理與 Pixer 裝置的 TCP 連線
- 支援命令傳送和檔案上傳
- 包含重試機制和錯誤處理

### MainActivity
- 提供裝置檢查、重設和上傳功能
- 非同步執行所有網路操作
- 記錄電池電量和版本資訊

### FirmwareUpgrade
- 自動檢查和升級 BLE、ITE、BSP 韌體
- 支援不同版本的升級策略
- 包含電池電量檢查機制

## 與 Python 版本的差異

1. **非同步模式**：使用 async/await 取代 Python 的 threading
2. **強型別**：利用 C# 的型別安全特性
3. **資源管理**：使用 `using` 語句自動釋放資源
4. **錯誤處理**：更詳細的例外處理機制

## 相容性保證

- 圖片轉換演算法與 Python PIL 完全相同
- 4位元封裝格式位元組順序一致
- 協定標頭格式完全相同
- Socket 通訊協定完全相容

## 疑難排解

### 連線問題
- 確保已連線到 Pixer 裝置的 WiFi 熱點 (192.168.1.1)
- 檢查防火牆設定是否阻擋 6000 埠

### 圖片轉換問題
- 支援的圖片格式：JPEG、PNG、BMP、GIF 等
- 確保圖片檔案存在且可讀取

### 韌體升級問題
- 確保韌體檔案 (ble.bin, ite.bin, pixer.bin) 存在於執行目錄
- 檢查裝置電池電量是否大於 15%

## 開發和測試

建議在開發過程中：

1. 先在有網際網路的環境下完成開發
2. 測試時切換到 Pixer 裝置的 WiFi 進行實際驗證
3. 比較 C# 和 Python 版本的輸出確保一致性

## 授權

與原始 Pixer 專案相同的授權條款。
