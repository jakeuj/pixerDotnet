import threading
import time
import logging
import os, sys
import socket
from PIL import Image
from typing import Optional  # ← 新增：為了 Optional[...]
import io

logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger(__name__)

class SocketClient:
    def __init__(self, host='192.168.1.1', port=6000):
        self.host = host
        self.port = port
        self.socket = None
        self.input = None
        self.output = None

    def connect(self):
        for i in range(10): 
            try:
                self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.socket.settimeout(2)  
                self.socket.connect((self.host, self.port))
                self.input = self.socket.makefile('rb')
                self.output = self.socket.makefile('wb')
                logger.debug("Connected successfully")
                return
            except Exception as e:
                logger.debug(f"Connection attempt {i+1} failed: {e}")
                time.sleep(2)
        logger.error("Failed to connect after 10 attempts")

    def send(self, data):
        try:
            self.output.write(data)
            self.output.flush()
            response = b''
            for _ in range(5):
                try:
                    chunk = self.socket.recv(64)
                    if chunk:
                        return chunk.decode()
                except socket.timeout:
                    logger.debug(f"Timeout, retrying... {_+1}")
            return None
        except Exception as e:
            logger.error(f"Error in send: {e}")
            return None
            
    def upload(self, data):
        try:
            self.socket.settimeout(10)
            chunk_size = 4096
            offset = 0
            while offset < len(data):
                chunk = data[offset: offset + chunk_size]
                self.socket.sendall(chunk)
                offset += len(chunk)
                progress = offset * 100 // len(data)
                sys.stdout.write(f"\rprogress: {progress}%")
                sys.stdout.flush()
            sys.stdout.write("\n")
            sys.stdout.flush()
            tail = "#MOVE#d"
            self.socket.sendall(tail.encode('utf-8')) 
        except Exception as e:
            logger.error(f"Error in upload: {e}")               

    def close(self):
        if self.socket:
            self.socket.close()

class ImgConverter:
    def __init__(self, source_path):
        self.source = source_path

    def convert(self, width=1872, height=1404):
        try:
            img = Image.open(self.source)

            if img.size[1] > img.size[0]: # height > width
                img = img.transpose(Image.Transpose.ROTATE_90)
            img_ratio = img.width / img.height
            target_ratio = width / height
            if img_ratio >= target_ratio:
                new_height = height
                new_width = int(img.width * (new_height / img.height))
            else:
                new_width = width
                new_height = int(img.height * (new_width / img.width))
            img = img.resize((new_width, new_height), Image.LANCZOS)
            left = (new_width - width) / 2
            top = (new_height - height) / 2
            right = left + width
            bottom = top + height
            img = img.crop((left, top, right, bottom))

            grayscale_img = img.convert('L')
            pixels = list(grayscale_img.getdata())
            packed_data = bytearray()

            for i in range(0, len(pixels), 2):
                pixel1 = pixels[i] >> 4
                if i + 1 < len(pixels):
                    pixel2 = pixels[i+1] >> 4
                else:
                    pixel2 = 0
                packed_byte = (pixel2 << 4) | pixel1
                packed_data.append(packed_byte)

            header_string = "#file#000801314144imagebin"
            hex_to_add = header_string.encode('utf-8').hex()
            combined_data = bytes.fromhex(hex_to_add) + packed_data
            logger.debug(f"successfully converted image '{self.source}'")
            return combined_data

        except FileNotFoundError:
            logger.error(f"file '{self.source}' not found")
            return None
        except Exception as e:
            logger.error(f"error handling the image: {e}")
            return None

class MainActivity:
    def __init__(self):
        self.pixerBattery = 0
        self.mValidUpdateBatteryLevel = 20

    def check(self):
        def task():
            client = SocketClient()
            try:
                client.connect()
                response = client.send(b"#TEST#")
                if response == "Hello PC!":
                    ble_version = client.send(b"bleVersion")
                    ite_version = client.send(b"iteVersion")
                    mcu_version = client.send(b"mcuVersion")
                    battery_level = client.send(b"batteryLevel")
                    self.pixerBattery = int(battery_level)
            except Exception as e:
                logger.error(f"Error in checkPixerBattery: {e}")
            finally:
                client.close()

            logger.debug(f"Battery={self.pixerBattery}")
            logger.debug(f"BLE Version={ble_version}")
            logger.debug(f"MCU Version={mcu_version}")
            logger.debug(f"ITE Version={ite_version}")

        threading.Thread(target=task).start()

    def reset(self):
        def task():
            client = SocketClient()
            
            try:
                client.connect()
                response = client.send(b"#TEST#")
                if response == "Hello PC!":
                    client.send(b"reset")
            except Exception as e:
                logger.error(f"Error in reset: {e}")
            finally:
                client.close()

            logger.debug(f"reset sent")

        threading.Thread(target=task).start()

    def upload(self, data):
        def task():
            client = SocketClient()
            try:
                client.connect()
                client.upload(data)
            except Exception as e:
                logger.error(f"Error in upload: {e}")
            finally:
                client.close()

            logger.debug(f"uploaded")

        threading.Thread(target=task).start()
        
# ======== Firmware Upgrade Addon (Python 3.11) ========
import io

def _fw_connect(host='192.168.1.1', port=6000, timeout=2):
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(timeout)
    s.connect((host, port))
    return s

def _fw_send(sock: socket.socket, data: bytes, recv_size: int = 64, retries: int = 5) -> str | None:
    try:
        sock.sendall(data)
        for _ in range(retries):
            try:
                chunk = sock.recv(recv_size)
                if chunk:
                    return chunk.decode(errors='ignore')
            except socket.timeout:
                pass
        return None
    except Exception as e:
        logger.error(f"[FW] send error: {e}")
        return None

def _fw_send_file(sock: socket.socket, path_on_device: str, stream: io.BufferedReader | io.BytesIO) -> bool:
    MAX_SIZE = 1024 * 1024     # 534KB
    CHUNK = 1024              # 與裝置端一致
    T_START = 5.0             # 握手/回覆等待（秒）
    T_XFER = 30.0             # 傳輸階段超時（秒/每次sendall）
    T_OK_S = 180              # 等待 "OK!" 總秒數（每秒輪詢一次）

    def _drain():
        """清空殘留回應，避免下一命令失步""" 
        try:
            old = sock.gettimeout()
            sock.settimeout(0.2)
            while True:
                try:
                    if not sock.recv(256):
                        break
                except socket.timeout:
                    break
        finally:
            try:
                sock.settimeout(old)
            except Exception:
                pass

    try:
        # 0) 計算大小並檢查上限
        if isinstance(stream, io.BytesIO):
            size = len(stream.getbuffer()); stream.seek(0)
        else:
            cur = stream.tell(); stream.seek(0, os.SEEK_END); size = stream.tell(); stream.seek(0)
        if size > MAX_SIZE:
            logger.error(f"[FW] file too large: {size} > {MAX_SIZE}")
            return False

        # 1) 啟動傳檔 + 路徑 + 檔長（握手階段用較短timeout）
        sock.settimeout(T_START)
        resp = _fw_send(sock, b"sendFile"); logger.debug(f"[FW] sendFile init resp: {resp}")
        if not resp: return False

        sock.sendall(path_on_device.encode('utf-8'))
        ack1 = _fw_send(sock, str(size).encode()); logger.debug(f"[FW] send size resp: {ack1}")
        if not ack1: return False

        # 2) 傳輸資料（傳輸階段用較長timeout）
        old_to = sock.gettimeout()
        sock.settimeout(T_XFER)
        sent = 0
        stream.seek(0)
        while True:
            chunk = stream.read(CHUNK)
            if not chunk: break
            sock.sendall(chunk)
            sent += len(chunk)
        logger.debug(f"[FW] sent bytes: {sent}")

        # 3) 等待最終 "OK!"（輪詢最多 T_OK_S 秒）
        sock.settimeout(5.0)
        for _ in range(T_OK_S):
            try:
                r = sock.recv(256)
                if r:
                    msg = r.decode(errors='ignore').strip()
                    logger.debug(f"[FW] last resp: {msg}")
                    if msg == "OK!":
                        return True
            except socket.timeout:
                pass
            time.sleep(1.0)
        logger.error("[FW] wait OK! timeout")
        return False

    except Exception as e:
        logger.error(f"[FW] send_file error: {e}")
        return False
    finally:
        _drain()                 # 清殘留，避免下一階段失步
        try: sock.settimeout(None)
        except Exception: pass
        time.sleep(0.5)          # 給裝置一點處理時間



def firmware_upgrade_before_check(
    host='192.168.1.1', port=6000,
    mBleVerNo=14, mIteVerNo=35, mBspVerNo=1702061,
    ble_bin_path='ble_new.bin', ite_bin_path='ite_new.bin', bsp_bin_path='pixer.bin'
):
    """
    依規則：
    - 若 battery <= 15 → 不升級，askCharge=True（此處僅記錄並返回）
    - BLE/ITE：版本低於門檻 → sendFile(ble_new.bin / ite_new.bin)
        * 若 BSP 新(>=1700000) → 立即以 bleversion/iteversion 查新值
        * 否則 → reboot=True, updateOta=True
    - BSP：版本低 → 先 mcuImage；回 '1' 用 /sys/mcuimg3.bin，否則 /sys/mcuimg2.bin，成功後 reboot=True
    - 若有 updateOta=True → 再送 ota_info.bin（內容 {1,0,0,0}）
    - 結束：
        * reboot 且 BSP 舊 → send "off"
        * reboot 且 BSP 新 → send "reset"
    """
    askCharge = False
    reboot = False
    updateOta = False

    try:
        sock = _fw_connect(host, port, timeout=2)
    except Exception as e:
        logger.error(f"[FW] connect fail: {e}")
        return

    try:
        # 啟始握手
        hello = _fw_send(sock, b"#TEST#")
        if hello and hello != "Hello PC!" and len(hello) > 9:
            # 與 App 行為一致：若回應夾雜，直接進行後續查詢
            pass

        # 讀版本與電量
        ble_version = _fw_send(sock, b"bleVersion") or "0.0.0"
        ite_version = _fw_send(sock, b"iteVersion") or "0.0.0"
        mcu_version = _fw_send(sock, b"mcuVersion") or "0_0000-00-00_0"
        battery_s = _fw_send(sock, b"batteryLevel") or "0"

        logger.debug(f"[FW] ble={ble_version}, ite={ite_version}, mcu={mcu_version}, battery={battery_s}")

        # 解析版本
        try:
            ble_no = int(ble_version.split(".")[2])
        except Exception:
            ble_no = 0
        try:
            ite_no = int(ite_version.split(".")[2])
        except Exception:
            ite_no = 0
        try:
            parts = mcu_version.split("_")
            bsp_no = int(parts[1].replace("-", "")) if len(parts) >= 2 else 0
        except Exception:
            bsp_no = 0

        try:
            battery = int(battery_s)
        except Exception:
            battery = 0

        # 電量門檻
        if battery <= 15:
            askCharge = True
            logger.info("[FW] battery <= 15 → skip upgrade (askCharge=True)")
            # 與 App 一致：此處不再送 off，僅先返回
            return

        # ITE 升級
        if ite_no < mIteVerNo and os.path.exists(ite_bin_path):
            logger.info(f"[FW] ITE update -> {ite_bin_path}")
            with open(ite_bin_path, "rb") as f:
                if _fw_send_file(sock, "ite_new.bin", f):
                    if bsp_no >= 1700000:
                        ite_version = _fw_send(sock, b"iteversion") or ite_version
                        logger.info(f"[FW] ITE new ver: {ite_version}")
                    else:
                        reboot = True
                        updateOta = True
                else:
                    logger.error("[FW] ITE update failed")

        # BLE 升級
        if ble_no < mBleVerNo and os.path.exists(ble_bin_path):
            logger.info(f"[FW] BLE update -> {ble_bin_path}")
            with open(ble_bin_path, "rb") as f:
                if _fw_send_file(sock, "ble_new.bin", f):
                    if bsp_no >= 1700000:
                        ble_version = _fw_send(sock, b"bleversion") or ble_version
                        logger.info(f"[FW] BLE new ver: {ble_version}")
                    else:
                        reboot = True
                        updateOta = True
                else:
                    logger.error("[FW] BLE update failed")

        # BSP 升級
        if bsp_no < mBspVerNo and os.path.exists(bsp_bin_path):
            logger.info(f"[FW] BSP update -> {bsp_bin_path}")
            mcu_img_resp = _fw_send(sock, b"mcuImage") or "0"
            target_path = "/sys/mcuimg3.bin" if (bsp_no > 1700000 and mcu_img_resp.strip() == "1") else "/sys/mcuimg2.bin"
            with open(bsp_bin_path, "rb") as f:
                if _fw_send_file(sock, target_path, f):
                    reboot = True
                else:
                    logger.error("[FW] BSP update failed")

        # 需要 OTA info
        if updateOta:
            logger.info("[FW] send ota_info.bin")
            ota = io.BytesIO(bytes([1, 0, 0, 0]))
            if not _fw_send_file(sock, "ota_info.bin", ota):
                logger.error("[FW] ota_info send failed")

        # 結束控制
        if reboot:
            if bsp_no < 1700000:
                _fw_send(sock, b"off")
                logger.debug("[FW] sent: off")
            else:
                _fw_send(sock, b"reset")
                logger.debug("[FW] sent: reset")

    except Exception as e:
        logger.error(f"[FW] upgrade exception: {e}")
    finally:
        try:
            sock.close()
        except Exception:
            pass
# ======== End of Firmware Upgrade Addon ========


if __name__ == "__main__":
    activity = MainActivity()
    # ★ 新增：先做升級檢查/執行（檔名可自行調整或放同目錄）
    firmware_upgrade_before_check(
        host='192.168.1.1', port=6000,
        mBleVerNo=14, mIteVerNo=35, mBspVerNo=1702061,
        ble_bin_path='ble.bin', ite_bin_path='ite.bin', bsp_bin_path='pixer.bin'
    )
    activity.check()
    if (len(sys.argv) > 1):
        data = ImgConverter(sys.argv[-1]).convert()
        activity.upload(data)
    # activity.reset()


