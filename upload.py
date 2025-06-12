import threading
import time
import logging
import os, sys
import socket
from PIL import Image

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
            if img.size[1] > height: 
                img.thumbnail((width, height), Image.Resampling.LANCZOS)
            else: # img.thumbnail doesn't work
                img = img.resize((width, height),Image.Resampling.LANCZOS)
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

if __name__ == "__main__":
    activity = MainActivity()
    activity.check()
    if (len(sys.argv) > 1):
        data = ImgConverter(sys.argv[-1]).convert()
        activity.upload(data)
    # activity.reset()


