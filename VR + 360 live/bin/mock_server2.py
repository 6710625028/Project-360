import cv2
import threading
import time
from flask import Flask, Response

app = Flask(__name__)

# เก็บภาพที่บีบอัดเป็น Byte ไว้ล่วงหน้าใน RAM
latest_jpeg_bytes = None
lock = threading.Lock()

def camera_worker():
    global latest_jpeg_bytes
    
    cap = None
    # ค้นหากล้อง Insta360
    for idx in [0, 1, 2]:
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
        print("[INFO] ไม่พบกล้อง 360 สลับไปใช้ไฟล์ sample_360.mp4")
        cap = cv2.VideoCapture("sample_360.mp4")
        use_mock = True

    # 1. ล้างคิวบัฟเฟอร์ให้เหลือ 1 เฟรม ป้องกันดีเลย์สะสม
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)

    # 2. ตั้งค่า Encode ให้ประมวลผลเร็วที่สุดสำหรับภาพขนาดใหญ่
    # ปิด JPEG_OPTIMIZE (0) เพื่อลดภาระ CPU และตั้ง Quality ที่ 75 (ภาพยังคมชัดระดับเดิม)
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

        # ไม่มีการ resize พิกเซล คง Resolution ดั้งเดิมของกล้องไว้ 100%
        _, buffer = cv2.imencode('.jpg', frame, encode_param)
        jpeg_data = buffer.tobytes()

        with lock:
            latest_jpeg_bytes = jpeg_data

@app.route('/frame')
def get_frame():
    with lock:
        if latest_jpeg_bytes is None:
            return ("No frame available", 503)
        # ส่งข้อมูลภาพจาก RAM กลับไปทันทีโดยไม่ต้องรอ Encode ใหม่
        return Response(latest_jpeg_bytes, mimetype='image/jpeg')

if __name__ == '__main__':
    # รันลูปกล้องแยกเป็นเบื้องหลัง
    t = threading.Thread(target=camera_worker, daemon=True)
    t.start()

    print("[READY] เซิร์ฟเวอร์ Full-Resolution เริ่มทำงานบน Port 5000...")
    app.run(host='0.0.0.0', port=5000, threaded=True)