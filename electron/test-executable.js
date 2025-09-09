// 測試執行檔路徑的腳本
const path = require('path');
const fs = require('fs');
const os = require('os');

function getExecutablePath() {
  const platform = os.platform();
  const isDev = true; // 測試開發模式
  
  let toolsDir;
  if (isDev) {
    // 開發模式：使用當前目錄的 assets 資料夾
    toolsDir = path.join(__dirname, 'assets');
  } else {
    // 打包模式：使用 extraResources 中的 assets 資料夾
    toolsDir = path.join(process.resourcesPath, 'assets');
  }
  
  let executableName;
  if (platform === 'win32') {
    executableName = 'PixerUpload.exe';
  } else if (platform === 'darwin') {
    executableName = 'PixerUpload';
  } else {
    throw new Error(`不支援的作業系統: ${platform}`);
  }
  
  const executablePath = path.join(toolsDir, executableName);
  
  console.log('平台:', platform);
  console.log('工具目錄:', toolsDir);
  console.log('執行檔名稱:', executableName);
  console.log('完整路徑:', executablePath);
  console.log('檔案存在:', fs.existsSync(executablePath));
  
  if (fs.existsSync(executablePath)) {
    const stats = fs.statSync(executablePath);
    console.log('檔案大小:', stats.size, 'bytes');
    console.log('檔案權限:', stats.mode.toString(8));
    console.log('是否可執行:', !!(stats.mode & parseInt('111', 8)));
  }
  
  // 檢查執行檔是否存在
  if (!fs.existsSync(executablePath)) {
    // 如果在 assets 中找不到，嘗試在上層目錄的 tools 中尋找
    const fallbackToolsDir = path.join(__dirname, '..', 'tools');
    const fallbackPath = path.join(fallbackToolsDir, executableName);
    
    console.log('Fallback 目錄:', fallbackToolsDir);
    console.log('Fallback 路徑:', fallbackPath);
    console.log('Fallback 檔案存在:', fs.existsSync(fallbackPath));
    
    if (fs.existsSync(fallbackPath)) {
      console.log(`在 fallback 路徑找到執行檔: ${fallbackPath}`);
      return fallbackPath;
    }
    
    throw new Error(`找不到執行檔: ${executablePath} 或 ${fallbackPath}`);
  }
  
  return executablePath;
}

try {
  const execPath = getExecutablePath();
  console.log('✅ 成功找到執行檔:', execPath);
} catch (error) {
  console.error('❌ 錯誤:', error.message);
}
