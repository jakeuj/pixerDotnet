const { contextBridge, ipcRenderer } = require('electron');

console.log('Preload script loading...');

try {
  // 安全地暴露 API 給渲染進程
  contextBridge.exposeInMainWorld('electronAPI', {
    // 選擇圖片檔案
    selectImage: () => {
      console.log('selectImage called');
      return ipcRenderer.invoke('select-image');
    },
    
    // 檢查裝置狀態
    checkDevice: () => {
      console.log('checkDevice called');
      return ipcRenderer.invoke('check-device');
    },
    
    // 上傳圖片
    uploadImage: (imagePath) => {
      console.log('uploadImage called with:', imagePath);
      return ipcRenderer.invoke('upload-image', imagePath);
    },
    
    // 獲取系統資訊
    getSystemInfo: () => {
      console.log('getSystemInfo called');
      return ipcRenderer.invoke('get-system-info');
    },
    
    // 監聽上傳進度
    onUploadProgress: (callback) => {
      console.log('onUploadProgress listener added');
      ipcRenderer.on('upload-progress', (event, data) => callback(data));
    },
    
    // 移除上傳進度監聽器
    removeUploadProgressListener: () => {
      console.log('upload-progress listeners removed');
      ipcRenderer.removeAllListeners('upload-progress');
    }
  });

  console.log('Preload script loaded successfully');
} catch (error) {
  console.error('Error in preload script:', error);
}

// 添加一個全域變數來確認 preload 已載入
window.preloadLoaded = true;
console.log('Preload script execution completed');
