# PICO 4 360 Panorama Demo

โปรเจกต์นี้เป็น **mock 360 panorama viewer** สำหรับเปิดภาพ equirectangular บน PICO 4 ผ่าน Unity โดย Python/Flask ทำหน้าที่เป็น server ใน Mac เครื่องนี้

ตอนนี้ยังไม่ใช่ live stream จากกล้องจริง: server อ่านภาพ `server/assets/pano.jpg` แล้วเก็บไว้ในหน่วยความจำ เพื่อจำลอง API ที่จะใช้กับกล้องจริงในภายหลัง

## สิ่งที่เดโมนี้ครอบคลุมตามโจทย์

| สิ่งที่ต้องการ | ส่วนที่ใช้สาธิต |
| --- | --- |
| 360 live-stream server แบบจำลอง | `server/server.py` + `server/assets/pano.jpg` |
| ทดลอง Web request/API โดยส่งมุม | หน้าเว็บ `/` และ endpoint `/pano/view?yaw=…&pitch=…&fov=…&mode=…` |
| เลือก 360 หรือ 180 | radio button ในหน้าเว็บ หรือ `mode=360` / `mode=180` ใน API |
| แสดงผลใน PICO 4 | `unity/Scripts/Pano360SphereViewer.cs` บน Sphere รอบ XR Camera |
| ต่อกล้องเข้ากับ server | ตั้ง `PANO_SOURCE=rtsp` หรือ `PANO_SOURCE=usb` ตามหัวข้อ “ต่อกล้องจริง” |

## เริ่มเร็ว

บน Mac เครื่องนี้ port `5000` ถูกใช้งานโดย AirPlay Receiver จึงให้เริ่มที่ port `5050`:

```bash
cd "/Users/sitthipong.kam/CN360/360 live/server"
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r requirements.txt
PORT=5050 python server.py
```

เปิด `http://127.0.0.1:5050/pano/info` แล้วตั้ง `serverBaseUrl` ใน Unity เป็น `http://<Mac-IP>:5050`.

สำหรับเดโมผ่านเว็บ ให้เปิด `http://127.0.0.1:5050/` แล้วเลื่อน yaw/pitch/fov หรือสลับ 360/180 ได้ทันที

## โครงสร้างไฟล์

```text
360 live/
├── README.md                         # คู่มือหลัก
├── CHECKLIST.md                      # รายการตรวจเดโม
├── server/
│   ├── server.py                     # Flask API
│   ├── frame_source.py                # เลือก mock, RTSP หรือ USB camera
│   ├── make_fake_pano.py             # สร้างภาพ mock
│   ├── requirements.txt              # Python dependencies
│   ├── requirements-camera.txt       # OpenCV สำหรับกล้องจริง
│   └── assets/pano.jpg               # ภาพ equirectangular 2:1
└── unity/
    ├── Scripts/
    │   ├── Pano360SphereViewer.cs    # viewer หลักบน Sphere
    │   └── Pano360Viewer.cs          # API demo บน Quad
    └── Android/AndroidManifest.example.xml
```

### ไฟล์ที่ย้าย

หาก IDE ยังเปิดแท็บชื่อเก่า ให้เปิดไฟล์จากตำแหน่งใหม่ด้านล่างแทน:

| เดิม | ตำแหน่งปัจจุบัน |
| --- | --- |
| `sever.py` | `server/server.py` |
| `mockPano.py` | `server/make_fake_pano.py` |
| `pano.jpg` | `server/assets/pano.jpg` |
| `Pano360viewer.cs` | `unity/Scripts/Pano360Viewer.cs` |
| `Pano360Sphereviewer.cs` | `unity/Scripts/Pano360SphereViewer.cs` |

ข้อความแบบ `""" ... """` ที่เคยอยู่ต้นไฟล์ Python ถูกย้ายมาไว้ใน README นี้แล้ว; เหลือเฉพาะ docstring สั้น ๆ บนฟังก์ชันที่ช่วยอธิบายโค้ดเท่านั้น

## 1. รัน server บน Mac

เปิด Terminal แล้วรันจากโฟลเดอร์นี้:

```bash
cd "/Users/sitthipong.kam/CN360/360 live/server"
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python make_fake_pano.py  # ไม่จำเป็นถ้าต้องการใช้ assets/pano.jpg ที่มีอยู่แล้ว
python server.py
```

server จะฟังที่พอร์ต `5000` และเปิดรับอุปกรณ์ใน Wi-Fi เดียวกัน (`0.0.0.0`) ตรวจจาก browser บน Mac ก่อน:

หาก macOS แจ้งว่า port `5000` ถูกใช้งานอยู่ (เช่น AirPlay Receiver) ให้เลือกพอร์ตว่างชั่วคราว เช่น `5050` และใช้เลขพอร์ตเดียวกันใน `serverBaseUrl`:

```bash
PORT=5050 python server.py
```

```text
http://127.0.0.1:5000/               # หน้าเว็บควบคุมเดโม
http://127.0.0.1:5000/pano/info
http://127.0.0.1:5000/pano/full
http://127.0.0.1:5000/pano/view?yaw=45&pitch=0&fov=90&mode=360
```

ผลของ `/pano/info` ต้องมี `"status":"ok"` และขนาดภาพ `2048 × 1024`.

### API

| Endpoint | ผลลัพธ์ |
| --- | --- |
| `GET /` | หน้าเว็บเดโม: ส่ง yaw/pitch/fov/mode ไปยัง API |
| `GET /pano/info` | สถานะ server และขนาดภาพต้นทาง |
| `GET /pano/full` | JPEG equirectangular เต็มใบ สำหรับ Sphere viewer |
| `GET /pano/view?yaw=0&pitch=0&fov=90&mode=360` | JPEG crop แบบ perspective ขนาด 800×800 |

`yaw` คือซ้าย-ขวา, `pitch` คือบน-ล่าง (`-90` ถึง `90`) และ `fov` ถูกจำกัดเป็น `10` ถึง `150` องศา. `mode=180` จำกัด `yaw` ไว้ที่ `-90` ถึง `90`; `mode=360` วนมุมมองที่รอยต่อ panorama ได้ถูกต้อง. ใน mock ปัจจุบัน 180 เป็นการจำลองครึ่งซีกหน้าจากภาพ 360 เดิม

## 2. ให้ PICO 4 ต่อถึง Mac

1. ให้ Mac และ PICO 4 อยู่ Wi-Fi เดียวกัน และไม่ใช้ guest network ที่ห้ามอุปกรณ์เห็นกัน
2. หา IPv4 ของ Wi-Fi บน Mac:

   ```bash
   ipconfig getifaddr en0
   ```

   หากคำสั่งไม่แสดงค่า ให้ดู **System Settings → Wi-Fi → Details → IP address** แทน
3. แทน `serverBaseUrl` ใน Unity ด้วย `http://<IPv4-ของ-Mac>:5000` เช่น `http://192.168.1.50:5000` ห้ามใช้ `localhost` หรือ `127.0.0.1` เพราะสองค่านั้นจะหมายถึงตัวแว่นเอง
4. ถ้า macOS ถามเรื่อง firewall ให้อนุญาต Python รับการเชื่อมต่อขาเข้าใน Private network แล้วลองเปิด `http://<IPv4-ของ-Mac>:5000/pano/info` จากมือถือใน Wi-Fi เดียวกันก่อน

## 3. เตรียม Unity สำหรับ PICO 4

1. ใน Unity Hub ติดตั้ง **Android Build Support** พร้อม Android SDK, NDK และ OpenJDK สำหรับ Unity เวอร์ชันที่ใช้
2. สร้างหรือเปิดโปรเจกต์ Unity จากนั้นติดตั้ง **OpenXR Plugin** และ (ถ้าต้องการ controller/UI) **XR Interaction Toolkit** ผ่าน Package Manager
3. ดาวน์โหลดและ import **PICO Unity Integration SDK** เวอร์ชันที่เข้ากันได้กับ Unity ของคุณ แล้วเปิด Unity OpenXR/PICO capability ตามหน้าตั้งค่าของ SDK. PICO ระบุว่า Integration SDK ตั้งแต่ 3.3.0 ใช้งานร่วมกับ Unity OpenXR ได้
4. สลับ build target เป็น Android, เปิด XR สำหรับ Android และเลือก **ARM64**. PICO Integration SDK 3.1.0 ขึ้นไปสร้างแอป 64-bit เท่านั้น
5. ใน Player/Publishing Settings เปิด **Custom Main Manifest** เพื่อให้ Unity สร้าง `Assets/Plugins/Android/AndroidManifest.xml`; จากนั้นเพิ่ม permission, `usesCleartextTraffic` และ PICO metadata ตาม `unity/Android/AndroidManifest.example.xml` โดยเก็บ activity/configuration ที่ Unity สร้างไว้อยู่แล้ว
6. ตั้ง Package Name ของแอปใน **Project Settings → Player → Android → Package Name** เช่น `com.yourname.pano360demo`

manifest ตัวอย่างเพิ่ม `INTERNET`/`ACCESS_NETWORK_STATE` ตามข้อกำหนดของ PICO และเปิด clear-text HTTP สำหรับ server ใน LAN. ใช้ HTTP เฉพาะเครือข่ายที่เชื่อถือได้; หากจะ deploy นอก LAN ควรเปลี่ยน server เป็น HTTPS แล้วนำ `android:usesCleartextTraffic="true"` ออก

อ้างอิง PICO ทางการ: [Unity OpenXR SDK](https://developer.picoxr.com/document/unity-openxr/), [Android manifest และ permission](https://developer.picoxr.com/document/unity/android-manifest/), [ตัวอย่าง Unity/XR Interaction Toolkit](https://developer.picoxr.com/document/unity/create-an-xr-scene/)

## 4. จัด Scene

### ใช้งานจริงในแว่น: Sphere viewer

1. สร้าง `Sphere` ให้ครอบ XR Camera และตั้ง Scale ตัวอย่างเป็น `(-50, 50, 50)` เพื่อให้มอง texture จากด้านใน
2. ลบ Sphere Collider แล้วใส่ Material แบบ `Unlit/Texture`
3. นำ `unity/Scripts/Pano360SphereViewer.cs` เข้า Unity project แล้ว attach กับ Sphere
4. ลาก Sphere Renderer ไปยัง `targetRenderer`
5. ตั้ง `serverBaseUrl` เป็น IP ของ Mac ตามขั้นที่ 2. เริ่มจาก `refreshInterval = 1` วินาทีก่อน

สคริปต์จะทำลาย texture ที่ดาวน์โหลดรอบก่อนเมื่อโหลดรอบใหม่ เพื่อไม่ให้หน่วยความจำเพิ่มต่อเนื่องระหว่างใช้งาน

### เดโม API: Quad viewer

1. สร้าง Quad ไว้หน้ากล้อง พร้อม Material `Unlit/Texture`
2. นำ `unity/Scripts/Pano360Viewer.cs` เข้า Unity project แล้ว attach ที่ Main Camera/XR Camera
3. ลาก Quad Renderer ไปที่ `targetRenderer` และตั้ง `serverBaseUrl` เดียวกัน
4. กด Play แล้วหมุนกล้อง: script จะส่ง yaw/pitch ไป `/pano/view` อย่างน้อยทุก `0.2` วินาที

ตัวนี้เหมาะโชว์ API เท่านั้น; สำหรับการมอง 360 ใน PICO ให้ใช้ Sphere viewer เพราะ GPU บนแว่นจัดการการหมุนมุมมองได้ลื่นกว่า

## 5. เปิด Developer Mode และลงแอป

1. ในแว่นไปที่ **Settings → General → About** แล้วแตะ software version หลายครั้งจนมีเมนู **Developer**
2. เปิด **USB debugging** ในเมนู Developer แล้วต่อ USB-C เข้ากับ Mac และยืนยัน prompt บนแว่น
3. ติดตั้ง PICO Developer Center (PDC) รุ่นล่าสุดบน macOS เพื่อเช็คการเชื่อมต่อ/ADB หรือใช้ `adb devices` จาก Android SDK ของ Unity
4. ใน Unity เลือก Build And Run สำหรับ Android แล้วทดสอบในแว่น

PICO ระบุว่า PDC รองรับทั้ง macOS Intel และ Apple Silicon และ PICO 4; ดู [คู่มือ PICO Developer Center](https://developer.picoxr.com/zh/document/unity/pdc-basic-info/) สำหรับขั้นตอนตามเวอร์ชันปัจจุบัน

## ตรวจปัญหาที่พบบ่อย

| อาการ | ตรวจ/แก้ |
| --- | --- |
| `/pano/info` เป็น 404 | ในโฟลเดอร์ `server/` รัน `python make_fake_pano.py` เพื่อสร้าง `assets/pano.jpg` แล้ว restart server |
| Unity/PICO ต่อไม่ได้ | ยืนยัน IP ไม่ใช่ `localhost`, อยู่ Wi-Fi เดียวกัน และอนุญาต firewall บน Mac |
| Android แจ้ง clear-text HTTP ถูกบล็อก | ตรวจว่า manifest ถูกวางที่ `Assets/Plugins/Android/AndroidManifest.xml` และมี `usesCleartextTraffic="true"` |
| Sphere ดำหรือมองไม่เห็น | ตรวจ Material เป็น Unlit/Texture, ใส่ `targetRenderer`, และ Scale X เป็นค่าติดลบ |
| ภาพไม่อัปเดต | ตอนนี้ mock ใช้ไฟล์ภาพนิ่งเป็นปกติ; เปลี่ยนเป็นกล้องจริงตามหัวข้อถัดไป |
| Unity หา script ไม่เจอ | ตรวจว่าชื่อไฟล์ตรงกับชื่อ class: `Pano360Viewer.cs` และ `Pano360SphereViewer.cs` |

## 6. ต่อกล้อง 360 จริงเข้ากับ server

กล้องต้องส่งภาพที่ stitch แล้วในรูปแบบ **equirectangular 2:1** (เช่น 3840×1920) มาให้ server. หากกล้องส่งเป็นภาพเลนส์คู่/fisheye ต้องเปิด stream ที่ stitch แล้วจากแอปหรือ SDK ของผู้ผลิตก่อน เพราะ OpenCV ในโปรเจกต์นี้ไม่ได้ stitch ภาพให้เอง

ติดตั้ง dependency ของกล้องจากในโฟลเดอร์ `server/`:

```bash
python -m pip install -r requirements-camera.txt
```

### RTSP camera

```bash
PANO_SOURCE=rtsp CAMERA_URL="rtsp://camera-ip:554/stream" PORT=5050 python server.py
```

### USB camera

```bash
PANO_SOURCE=usb CAMERA_URL=0 PORT=5050 python server.py
```

เปิด `/pano/info` เพื่อตรวจว่า `source` เป็น `rtsp` หรือ `usb` จากนั้นทดสอบหน้าเว็บก่อน แล้วค่อยเปิด Unity/PICO. ฝั่ง PICO ใช้ URL เดิม เพียงแต่ Sphere จะได้รับเฟรมล่าสุดแทนภาพ mock

## 7. ลำดับเดโมให้อาจารย์

1. เปิด `http://127.0.0.1:5050/` และเลื่อน yaw/pitch เพื่อแสดง Web request/API
2. สลับ `mode=360` และ `mode=180` ให้เห็นข้อจำกัดของมุมมอง
3. เปิด `/pano/full` เพื่อแสดงภาพ panorama ต้นทาง 2:1
4. เปิดแอปบน PICO 4 แล้วหันศีรษะเพื่อมองภาพผ่าน Sphere 360 รอบตัว
5. หากมีกล้องจริง ให้สลับ `PANO_SOURCE` เป็น `rtsp` หรือ `usb` และเปิด auto-refresh ในหน้าเว็บเพื่อยืนยันว่าเฟรมอัปเดต
