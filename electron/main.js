const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const { spawn } = require('child_process');
const fs = require('fs');
const os = require('os');

let mainWindow;

// 獲取跨平台的執行檔路徑
function getExecutablePath() {
  const platform = os.platform();
  // 更準確的開發模式檢測
  const isDev = !app.isPackaged;
  
  console.log('平台:', platform);
  console.log('是否為開發模式:', isDev);
  console.log('__dirname:', __dirname);
  console.log('process.resourcesPath:', process.resourcesPath);
  
  let executableName;
  if (platform === 'win32') {
    executableName = 'PixerUpload.exe';
  } else if (platform === 'darwin') {
    executableName = 'PixerUpload';
  } else {
    throw new Error(`不支援的作業系統: ${platform}`);
  }
  
  // 嘗試多個可能的路徑
  const possiblePaths = [];
  
  if (isDev) {
    // 開發模式的可能路徑
    possiblePaths.push(path.join(__dirname, 'assets', executableName));
    possiblePaths.push(path.join(__dirname, '..', 'tools', executableName));
  } else {
    // 打包模式的可能路徑
    possiblePaths.push(path.join(process.resourcesPath, 'assets', executableName));
    possiblePaths.push(path.join(process.resourcesPath, 'tools', executableName));
  }
  
  // 尋找存在的執行檔
  for (const executablePath of possiblePaths) {
    console.log('檢查路徑:', executablePath);
    if (fs.existsSync(executablePath)) {
      console.log('✅ 找到執行檔:', executablePath);
      
      // 在 macOS 上確保執行檔有執行權限
      if (platform === 'darwin') {
        try {
          fs.chmodSync(executablePath, '755');
        } catch (error) {
          console.warn('無法設定執行權限:', error.message);
        }
      }
      
      return executablePath;
    }
  }
  
  throw new Error(`找不到執行檔，已檢查路徑: ${possiblePaths.join(', ')}`);
}

// 創建主視窗
function createWindow() {
  mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js')
    },
    icon: path.join(__dirname, 'assets', 'icon.png'),
    title: 'Pixer Upload Tool'
  });

  mainWindow.loadFile('index.html');

  // 開發模式下開啟開發者工具
  if (process.env.NODE_ENV === 'development' || process.argv.includes('--dev')) {
    mainWindow.webContents.openDevTools();
  }
}

// 應用程式準備就緒時創建視窗
app.whenReady().then(createWindow);

// 所有視窗關閉時退出應用程式（除了 macOS）
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

// macOS 上點擊 dock 圖示時重新創建視窗
app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow();
  }
});

// IPC 處理程序：選擇圖片檔案
ipcMain.handle('select-image', async () => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openFile'],
    filters: [
      { name: '圖片檔案', extensions: ['jpg', 'jpeg', 'png', 'bmp', 'gif'] },
      { name: '所有檔案', extensions: ['*'] }
    ]
  });
  
  if (!result.canceled && result.filePaths.length > 0) {
    return result.filePaths[0];
  }
  return null;
});

// IPC 處理程序：檢查裝置狀態
ipcMain.handle('check-device', async (event, debug) => {
  return new Promise((resolve, reject) => {
    try {
      const executablePath = getExecutablePath();
      const workingDir = path.dirname(executablePath);
      console.log('執行檔路徑:', executablePath);
      console.log('工作目錄:', workingDir);
      
      const args = [];
      if (debug) args.push('--debug');

      const child = spawn(executablePath, args, {
        cwd: workingDir
      });
      
      let output = '';
      let errorOutput = '';
      
      child.stdout.on('data', (data) => {
        output += data.toString();
      });
      
      child.stderr.on('data', (data) => {
        errorOutput += data.toString();
      });
      
      child.on('close', (code) => {
        if (code === 0) {
          resolve({
            success: true,
            output: output,
            message: '裝置檢查完成'
          });
        } else {
          resolve({
            success: false,
            output: output,
            error: errorOutput,
            message: `裝置檢查失敗 (退出碼: ${code})`
          });
        }
      });
      
      child.on('error', (error) => {
        reject({
          success: false,
          error: error.message,
          message: '無法啟動 PixerUpload 工具'
        });
      });
      
    } catch (error) {
      reject({
        success: false,
        error: error.message,
        message: '執行檔路徑錯誤'
      });
    }
  });
});

// IPC 處理程序：上傳圖片
ipcMain.handle('upload-image', async (event, imagePath, debug) => {
  return new Promise((resolve, reject) => {
    try {
      const executablePath = getExecutablePath();
      const workingDir = path.dirname(executablePath);
      console.log('上傳圖片 - 執行檔路徑:', executablePath);
      console.log('上傳圖片 - 工作目錄:', workingDir);
      console.log('上傳圖片 - 圖片路徑:', imagePath);

      const args = [];
      if (debug) args.push('--debug');
      args.push(imagePath);

      const child = spawn(executablePath, args, {
        cwd: workingDir
      });
      
      let output = '';
      let errorOutput = '';
      
      child.stdout.on('data', (data) => {
        const text = data.toString();
        output += text;
        // 即時發送進度更新到渲染進程
        mainWindow.webContents.send('upload-progress', text);
      });
      
      child.stderr.on('data', (data) => {
        const text = data.toString();
        errorOutput += text;
        mainWindow.webContents.send('upload-progress', text);
      });
      
      child.on('close', (code) => {
        if (code === 0) {
          resolve({
            success: true,
            output: output,
            message: '圖片上傳成功'
          });
        } else {
          resolve({
            success: false,
            output: output,
            error: errorOutput,
            message: `圖片上傳失敗 (退出碼: ${code})`
          });
        }
      });
      
      child.on('error', (error) => {
        reject({
          success: false,
          error: error.message,
          message: '無法啟動 PixerUpload 工具'
        });
      });
      
    } catch (error) {
      reject({
        success: false,
        error: error.message,
        message: '執行檔路徑錯誤'
      });
    }
  });
});

// IPC 處理程序：獲取系統資訊
ipcMain.handle('get-system-info', async () => {
  try {
    const executablePath = getExecutablePath();
    return {
      platform: os.platform(),
      arch: os.arch(),
      executablePath: executablePath,
      nodeVersion: process.version,
      electronVersion: process.versions.electron
    };
  } catch (error) {
    return {
      platform: os.platform(),
      arch: os.arch(),
      error: error.message
    };
  }
});
