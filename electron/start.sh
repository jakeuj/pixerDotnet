#!/bin/bash

# Pixer Electron 啟動腳本

echo "🖼️  Pixer Electron GUI"
echo "======================="

# 檢查 Node.js 是否安裝
if ! command -v node &> /dev/null; then
    echo "❌ 錯誤: 未找到 Node.js"
    echo "請先安裝 Node.js: https://nodejs.org/"
    exit 1
fi

echo "✅ Node.js 版本: $(node --version)"

# 檢查是否已安裝依賴
if [ ! -d "node_modules" ]; then
    echo "📦 安裝依賴中..."
    npm install
    if [ $? -ne 0 ]; then
        echo "❌ 依賴安裝失敗"
        exit 1
    fi
fi

# 檢查 assets 目錄
if [ ! -d "assets" ]; then
    echo "❌ 錯誤: 找不到 assets 目錄"
    echo "請確保 assets 目錄包含 PixerUpload 執行檔"
    exit 1
fi

# 檢查執行檔
if [[ "$OSTYPE" == "darwin"* ]]; then
    EXECUTABLE="assets/PixerUpload"
    if [ ! -f "$EXECUTABLE" ]; then
        echo "❌ 錯誤: 找不到 macOS 執行檔 ($EXECUTABLE)"
        exit 1
    fi
    # 確保有執行權限
    chmod +x "$EXECUTABLE"
    echo "✅ 找到 macOS 執行檔"
elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]]; then
    EXECUTABLE="assets/PixerUpload.exe"
    if [ ! -f "$EXECUTABLE" ]; then
        echo "❌ 錯誤: 找不到 Windows 執行檔 ($EXECUTABLE)"
        exit 1
    fi
    echo "✅ 找到 Windows 執行檔"
else
    echo "❌ 錯誤: 不支援的作業系統 ($OSTYPE)"
    exit 1
fi

echo "🚀 啟動 Electron 應用程式..."
npm start
