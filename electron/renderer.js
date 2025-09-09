// DOM 元素
const selectImageBtn = document.getElementById('selectImageBtn');
const uploadBtn = document.getElementById('uploadBtn');
const checkDeviceBtn = document.getElementById('checkDeviceBtn');
const selectedImageDiv = document.getElementById('selectedImage');
const imagePreview = document.getElementById('imagePreview');
const imagePath = document.getElementById('imagePath');
const imageSize = document.getElementById('imageSize');
const uploadProgress = document.getElementById('uploadProgress');
const progressText = document.getElementById('progressText');
const uploadResult = document.getElementById('uploadResult');
const resultContent = document.getElementById('resultContent');
const deviceStatus = document.getElementById('deviceStatus');
const systemInfo = document.getElementById('systemInfo');
const debugToggle = document.getElementById('debugToggle');

// 全域變數
let selectedImagePath = null;
let debugEnabled = debugToggle?.checked || false;

// 初始化應用程式
async function initApp() {
    try {
        // 檢查 electronAPI 是否可用
        if (!window.electronAPI) {
            throw new Error('electronAPI 未定義。請確認 preload.js 已正確載入。');
        }
        
        console.log('electronAPI 可用:', Object.keys(window.electronAPI));
        
        // 載入系統資訊
        const sysInfo = await window.electronAPI.getSystemInfo();
        displaySystemInfo(sysInfo);
        
        // 設置事件監聽器
        setupEventListeners();
        
        console.log('應用程式初始化完成');
    } catch (error) {
        console.error('初始化失敗:', error);
        showError('應用程式初始化失敗: ' + error.message);
        
        // 顯示詳細的錯誤信息
        const errorInfo = `
            <div class="status-error">
                <h3>初始化錯誤</h3>
                <p><strong>錯誤訊息:</strong> ${error.message}</p>
                <p><strong>electronAPI 狀態:</strong> ${window.electronAPI ? '已載入' : '未載入'}</p>
                <p><strong>可用方法:</strong> ${window.electronAPI ? Object.keys(window.electronAPI).join(', ') : '無'}</p>
            </div>
        `;
        if (systemInfo) {
            systemInfo.innerHTML = errorInfo;
        }
    }
}

// 設置事件監聽器
function setupEventListeners() {
    // 選擇圖片按鈕
    if (selectImageBtn) selectImageBtn.addEventListener('click', selectImage);
    
    // 上傳按鈕
    if (uploadBtn) uploadBtn.addEventListener('click', uploadImage);
    
    // 檢查裝置按鈕
    if (checkDeviceBtn) checkDeviceBtn.addEventListener('click', checkDevice);

    // 監聽上傳進度
    if (window.electronAPI && window.electronAPI.onUploadProgress) {
        window.electronAPI.onUploadProgress((data) => {
            appendProgressText(data);
        });
    }

    // Debug 開關
    if (debugToggle) {
        debugToggle.addEventListener('change', () => {
            debugEnabled = debugToggle.checked;
        });
    }
}

// 顯示系統資訊
function displaySystemInfo(info) {
    const infoHtml = `
        <p><strong>作業系統:</strong> ${info.platform}</p>
        <p><strong>架構:</strong> ${info.arch}</p>
        <p><strong>Node.js 版本:</strong> ${info.nodeVersion}</p>
        <p><strong>Electron 版本:</strong> ${info.electronVersion}</p>
        ${info.executablePath ? `<p><strong>執行檔路徑:</strong> ${info.executablePath}</p>` : ''}
        ${info.error ? `<p class="status-error"><strong>錯誤:</strong> ${info.error}</p>` : ''}
    `;
    if (systemInfo) {
        systemInfo.innerHTML = infoHtml;
    }
}

// 選擇圖片
async function selectImage() {
    try {
        setButtonLoading(selectImageBtn, true);
        
        const imagePath = await window.electronAPI.selectImage();
        
        if (imagePath) {
            selectedImagePath = imagePath;
            await displaySelectedImage(imagePath);
            if (uploadBtn) uploadBtn.disabled = false;
            
            // 清除之前的結果
            hideElement(uploadResult);
            hideElement(uploadProgress);
        }
    } catch (error) {
        console.error('選擇圖片失敗:', error);
        showError('選擇圖片失敗: ' + error.message);
    } finally {
        setButtonLoading(selectImageBtn, false);
    }
}

// 顯示已選擇的圖片
async function displaySelectedImage(path) {
    try {
        // 設置圖片預覽
        if (imagePreview) imagePreview.src = `file://${path}`;
        if (imagePath) imagePath.textContent = `路徑: ${path}`;
        
        // 獲取檔案大小
        const stats = await getFileStats(path);
        if (imageSize) imageSize.textContent = `大小: ${formatFileSize(stats.size)}`;
        
        showElement(selectedImageDiv);
    } catch (error) {
        console.error('顯示圖片失敗:', error);
        showError('顯示圖片失敗: ' + error.message);
    }
}

// 獲取檔案統計資訊（模擬）
async function getFileStats(path) {
    // 這裡我們無法直接獲取檔案統計資訊，所以返回一個模擬值
    return { size: 0 };
}

// 格式化檔案大小
function formatFileSize(bytes) {
    if (bytes === 0) return '未知';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

// 檢查裝置
async function checkDevice() {
    try {
        setButtonLoading(checkDeviceBtn, true);
        if (deviceStatus) deviceStatus.innerHTML = '<p class="status-idle">檢查中...</p>';
        
        const result = await window.electronAPI.checkDevice(debugEnabled);
        
        if (deviceStatus) {
            if (result.success) {
                deviceStatus.innerHTML = `
                    <p class="status-success">✅ ${result.message}</p>
                    <pre>${result.output}</pre>
                `;
            } else {
                deviceStatus.innerHTML = `
                    <p class="status-error">❌ ${result.message}</p>
                    ${result.output ? `<pre>輸出: ${result.output}</pre>` : ''}
                    ${result.error ? `<pre>錯誤: ${result.error}</pre>` : ''}
                `;
            }
        }
    } catch (error) {
        console.error('檢查裝置失敗:', error);
        if (deviceStatus) deviceStatus.innerHTML = `<p class="status-error">❌ ${error.message}</p>`;
    } finally {
        setButtonLoading(checkDeviceBtn, false);
    }
}

// 上傳圖片
async function uploadImage() {
    if (!selectedImagePath) {
        showError('請先選擇一張圖片');
        return;
    }
    
    try {
        setButtonLoading(uploadBtn, true);
        
        // 顯示進度區域
        showElement(uploadProgress);
        if (progressText) progressText.textContent = '開始上傳...\n';
        hideElement(uploadResult);
        
        const result = await window.electronAPI.uploadImage(selectedImagePath, debugEnabled);
        
        // 顯示結果
        showElement(uploadResult);
        
        if (resultContent) {
            if (result.success) {
                resultContent.className = 'result-content success';
                resultContent.textContent = `✅ ${result.message}\n\n${result.output}`;
            } else {
                resultContent.className = 'result-content error';
                resultContent.textContent = `❌ ${result.message}\n\n輸出: ${result.output}\n\n錯誤: ${result.error}`;
            }
        }
        
    } catch (error) {
        console.error('上傳失敗:', error);
        showElement(uploadResult);
        if (resultContent) {
            resultContent.className = 'result-content error';
            resultContent.textContent = `❌ 上傳失敗: ${error.message}`;
        }
    } finally {
        setButtonLoading(uploadBtn, false);
    }
}

// 附加進度文字
function appendProgressText(text) {
    if (progressText) {
        progressText.textContent += text;
        progressText.scrollTop = progressText.scrollHeight;
    }
}

// 設置按鈕載入狀態
function setButtonLoading(button, loading) {
    if (!button) return;
    
    if (loading) {
        button.disabled = true;
        const originalText = button.innerHTML;
        button.dataset.originalText = originalText;
        button.innerHTML = '<span class="loading"></span> 處理中...';
    } else {
        button.disabled = false;
        if (button.dataset.originalText) {
            button.innerHTML = button.dataset.originalText;
            delete button.dataset.originalText;
        }
    }
}

// 顯示元素
function showElement(element) {
    if (element) element.style.display = 'block';
}

// 隱藏元素
function hideElement(element) {
    if (element) element.style.display = 'none';
}

// 顯示錯誤訊息
function showError(message) {
    alert('錯誤: ' + message);
}

// 當 DOM 載入完成時初始化應用程式
document.addEventListener('DOMContentLoaded', initApp);
