## Reverse Engineering the G+ Pixer E-Ink Photo Frame

### Usage

Connect to the device's wifi hotspot, then

`$ python upload.py <image>`

### Hardware

7.8 in carta e-ink screen, resolution 1872x1404

IT8951E e-ink controller chip

CC3xxx Ti WiFi-On-Chip MCU

### Protocol

The reset hole wakes the device, and the device will then create a wifi hotspot. 

To communicate with the device, transfer cmd to `192.168.1.1:6000` through tcp.

| cmd            | description                            | example response    |
| -------------- | -------------------------------------- | ------------------- |
| `#TEST#`       |                                        | `Hello PC!`         |
| `bleVersion`   | get ble firmware version               | `GBT_BLE_v.A.14`    |
| `mcuVersion`   | get mcu firmware version               | `GBT_170206-1_7.8A` |
| `iteVersion`   | get screen controller firmware version | `GBT_v.A.35`        |
| `batteryLevel` | get battery level                      | `88`                |

To upload an image, you need to create a binary payload. The header is `#file#000801314144demo.jpg`, which is followed by the image data. The image size must be 1872x1404, each pixel is represented as a 4-bit grayscale value. For each 8-bit value , you should first pack the 4-bit value of the 2nd pixel, then the value for the 1st pixel. After sending the payload, when the response is arrived, send `#MOVE#d`

### License

Public Domain