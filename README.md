# Pixer Uploader (Modified Version)

This project is based on [kasperis7/pixer](https://github.com/kasperis7/pixer).  
The original functionality was to upload images to the G+ Pixer e-ink photo frame.  
This modified version keeps the image upload feature and adds **firmware update** capability.

---

## Features
- Upload images to the device  
- Supported formats: `.jpg`, `.png`  
- **New:** Firmware upgrade functionality (BLE/ITE/BSP)  

---

## Usage

### 1. Executable file (.exe)
```bash
upload.exe test.jpg
```

### 2. Python script
```bash
python upload.py test.jpg
```

---

## Requirements (for Python version)
- Python 3.8+
- Required package:
  ```bash
  pip install pillow
  ```

---

## Changes
- Added firmware update process: supports updating `ble.bin`, `pixer.bin`, etc.  
- Improved error messages for easier debugging  
- Preserved the original image upload workflow  

---

## Firmware Upgrade Rules (Simple Version)

The uploader can also update the device firmware when needed.  
Updates are done **automatically** based on these simple rules:

- **Battery check**  
  - If the battery is too low (15% or less), no update will run.

- **When updates happen**  
  - If the device firmware is out of date, the uploader will send the correct file:
    - **BLE update** → `ble.bin`
    - **ITE update** → `ite.bin`
    - **BSP update** → `pixer.bin`

That’s it — the tool will check and update when safe.  
Users only need to prepare the firmware files in the same folder as the uploader.

---

## Reference
- [kasperis7/pixer](https://github.com/kasperis7/pixer)

