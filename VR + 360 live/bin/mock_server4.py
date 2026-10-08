import cv2
import numpy as np
import threading
import time
import socket
from flask import Flask, Response, request

app = Flask(__name__)

# ตัวแปรเก็บภาพในหน่วยความจำ (RAM)
latest_raw_frame = None
latest_360_jpeg = None
lock = threading.Lock()

def udp_broadcast_worker():
    """ยิงสัญญาณค้นหา IP ไปยังแว่น PICO 4 อัตโนมัติทุกๆ 1 วินาที"""
    broadcast_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    broadcast_sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
    while True:
        try:
            broadcast_sock.sendto(b"PICO_SERVER_HERE", ('<broadcast>', 5005))
        except Exception:
            pass
        time.sleep(1.0)

def camera_worker():
    """ดึงภาพจากกล้องแบบ Real-time ตัด Buffer Lag"""
    global latest_raw_frame, latest_360_jpeg
    
    cap = None
    # วนสแกนหากล้อง Insta360
    for idx in [ 1, 2]:
        temp_cap = cv2.VideoCapture(idx, cv2.CAP_DSHOW)
        if temp_cap.isOpened():
            ret, _ = temp_cap.read()
            if ret:
                cap = temp_cap
                print(f"[SUCCESS] เชื่อมต่อกล้อง 360 สำเร็จที่ Index {idx}")
                break
            temp_cap.release()

    use_mock = False
    if cap is None or not cap.isOpened():
        print("[INFO] ไม่พบกล้อง 360 สลับไปใช้ไฟล์จำลอง: sample_360.mp4")
        cap = cv2.VideoCapture("sample_360.mp4")
        use_mock = True

    # บังคับบัฟเฟอร์เหลือ 1 เฟรม
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)

    # พารามิเตอร์ Encode แบบเร็วพิเศษ ไม่เปลือง CPU
    encode_param = [
        int(cv2.IMWRITE_JPEG_QUALITY), 75,
        int(cv2.IMWRITE_JPEG_OPTIMIZE), 0
    ]

    while True:
        ret, frame = cap.read()
        if not ret:
            if use_mock:
                cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
                continue
            time.sleep(0.005)
            continue

        # บีบอัดภาพโหมด 360 เตรียมไว้ล่วงหน้า
        _, buffer = cv2.imencode('.jpg', frame, encode_param)
        jpeg_data = buffer.tobytes()

        with lock:
            latest_raw_frame = frame.copy()
            latest_360_jpeg = jpeg_data

@app.route('/frame')
def get_frame():
    mode = request.args.get('mode', '360')
    angle = float(request.args.get('angle', 0))

    with lock:
        if latest_360_jpeg is None or latest_raw_frame is None:
            return ("Waiting for camera...", 503)

        if mode == '360':
            resp = Response(latest_360_jpeg, mimetype='image/jpeg')
            resp.headers['Access-Control-Allow-Origin'] = '*'   # <-- เพิ่มบรรทัดนี้
            return resp

        img = latest_raw_frame.copy()

    h, w, _ = img.shape
    crop_w = w // 2
    start_x = int((angle / 360.0) * w) % w

    if start_x + crop_w <= w:
        cropped = img[:, start_x:start_x + crop_w]
    else:
        part1 = img[:, start_x:w]
        part2 = img[:, 0:crop_w - (w - start_x)]
        cropped = np.hstack((part1, part2))

    _, buffer = cv2.imencode('.jpg', cropped, [int(cv2.IMWRITE_JPEG_QUALITY), 75])
    resp = Response(buffer.tobytes(), mimetype='image/jpeg')
    resp.headers['Access-Control-Allow-Origin'] = '*'   # <-- เพิ่มบรรทัดนี้ด้วย
    return resp

if __name__ == '__main__':
    # รันเธรดยิง IP ค้นหาอัตโนมัติ
    t_udp = threading.Thread(target=udp_broadcast_worker, daemon=True)
    t_udp.start()

    # รันเธรดอ่านภาพจากกล้อง
    t_cam = threading.Thread(target=camera_worker, daemon=True)
    t_cam.start()

    print("[READY] เซิร์ฟเวอร์พร้อมทำงานที่ Port 5000 (รองรับ 360/180)")
    app.run(host='0.0.0.0', port=5000, threaded=True)