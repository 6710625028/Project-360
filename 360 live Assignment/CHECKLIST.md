# Demo Checklist — PICO 4 360 Live View

ใช้ [README.md](README.md) เป็นคู่มือหลัก; checklist นี้ใช้ซ้อมเดโมตามโจทย์อาจารย์

## 1. Mock 360 stream server

จากโฟลเดอร์ `server/`:

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r requirements.txt
PORT=5050 python server.py
```

- [ ] เปิด `http://127.0.0.1:5050/pano/info` และเห็น `"status":"ok"`
- [ ] เปิด `http://127.0.0.1:5050/pano/full` และเห็นภาพ panorama 2:1
- [ ] Mock image อยู่ที่ `server/assets/pano.jpg`

## 2. Web request / API

- [ ] เปิด `http://127.0.0.1:5050/`
- [ ] ปรับ `yaw`, `pitch` และ `fov` แล้วภาพ crop เปลี่ยนตาม
- [ ] สลับ `mode=360` และ `mode=180`
- [ ] แสดง API URL เช่น `/pano/view?yaw=45&pitch=0&fov=90&mode=360`

## 3. PICO 4 viewer

- [ ] Mac และ PICO 4 อยู่ Wi-Fi เดียวกัน
- [ ] ตั้ง `serverBaseUrl` ใน Unity เป็น `http://<Mac-IP>:5050` ไม่ใช่ `localhost`
- [ ] นำ `unity/Scripts/Pano360SphereViewer.cs` ไปใส่ Sphere ที่ครอบ XR Camera
- [ ] Sphere ใช้ Unlit/Texture และ Scale X ติดลบเพื่อมองจากด้านใน
- [ ] Android manifest มี INTERNET และ `usesCleartextTraffic="true"` ตาม `unity/Android/AndroidManifest.example.xml`
- [ ] Build And Run ลง PICO 4 แล้วหันศีรษะมองภาพรอบตัว

## 4. กล้อง 360 จริง (เมื่อมี)

- [ ] กล้องส่ง equirectangular panorama ที่ stitch แล้ว (อัตราส่วน 2:1)
- [ ] ติดตั้ง `python -m pip install -r requirements-camera.txt`
- [ ] RTSP: รัน `PANO_SOURCE=rtsp CAMERA_URL="rtsp://..." PORT=5050 python server.py`
- [ ] USB: รัน `PANO_SOURCE=usb CAMERA_URL=0 PORT=5050 python server.py`
- [ ] `/pano/info` แสดง `source` เป็น `rtsp` หรือ `usb`

## 5. ลำดับการนำเสนอ

1. เปิดหน้าเว็บและสาธิตการส่งมุมผ่าน API
2. สลับ 360/180
3. เปิดภาพ panorama ต้นทางเต็มใบ
4. สวม PICO 4 แล้วหันดูภาพ 360
5. หากมีกล้องจริง ให้สลับ source เป็น RTSP/USB เพื่อยืนยันว่า server ใช้เฟรมจากกล้อง
