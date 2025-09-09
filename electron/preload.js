const { contextBridge, ipcRenderer } = require('electron');

// 安全地暴露 API 給渲染進程
contextBridge.exposeInMainWorld('electronAPI', {
  // 選擇圖片檔案
  selectImage: () => ipcRenderer.invoke('select-image'),
  
  // 檢查裝置狀態
  checkDevice: () => ipcRenderer.invoke('check-device'),
  
  // 上傳圖片
  uploadImage: (imagePath) => ipcRenderer.invoke('upload-image', imagePath),
  
  // 獲取系統資訊
  getSystemInfo: () => ipcRenderer.invoke('get-system-info'),
  
  // 監聽上傳進度
  onUploadProgress: (callback) => {
    ipcRenderer.on('upload-progress', (event, data) => callback(data));
  },
  
  // 移除上傳進度監聽器
  removeUploadProgressListener: () => {
    ipcRenderer.removeAllListeners('upload-progress');
  }
});
