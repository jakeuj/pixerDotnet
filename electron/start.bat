@echo off
chcp 65001 >nul

echo 🖼️  Pixer Electron GUI
echo =======================

REM 檢查 Node.js 是否安裝
node --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ 錯誤: 未找到 Node.js
    echo 請先安裝 Node.js: https://nodejs.org/
    pause
    exit /b 1
)

echo ✅ Node.js 版本:
node --version

REM 檢查是否已安裝依賴
if not exist "node_modules" (
    echo 📦 安裝依賴中...
    call npm install
    if %errorlevel% neq 0 (
        echo ❌ 依賴安裝失敗
        pause
        exit /b 1
    )
)

REM 檢查 assets 目錄
if not exist "assets" (
    echo ❌ 錯誤: 找不到 assets 目錄
    echo 請確保 assets 目錄包含 PixerUpload.exe 執行檔
    pause
    exit /b 1
)

REM 檢查執行檔
if not exist "assets\PixerUpload.exe" (
    echo ❌ 錯誤: 找不到 Windows 執行檔 (assets\PixerUpload.exe)
    pause
    exit /b 1
)

echo ✅ 找到 Windows 執行檔

echo 🚀 啟動 Electron 應用程式...
call npm start

pause
