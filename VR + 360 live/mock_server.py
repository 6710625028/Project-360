import cv2
import numpy as np
import time
import threading
import socket
from flask import Flask, Response, request, render_template_string

app = Flask(__name__)

VIDEO_PATH = "sample_360.mp4"
current_frame = None
frame_lock = threading.Lock()

def get_video_source():
    """ตรวจจับกล้องจริงก่อน หากไม่มีให้ใช้ไฟล์วิดีโอจำลอง"""
    cap = cv2.VideoCapture(0)  # ตรวจจับพอร์ตกล้องแรก
    if cap.isOpened():
        ret, frame = cap.read()
        if ret and frame is not None:
            print("[INFO] ตรวจพบกล้อง 360 จริง! เริ่มดึงภาพสด...")
            return cap
        cap.release()
    
    print("[INFO] ไม่พบกล้อง 360 สลับไปใช้ไฟล์วิดีโอจำลอง:", VIDEO_PATH)
    return cv2.VideoCapture(VIDEO_PATH)

def video_worker():
    global current_frame
    cap = get_video_source()

    while True:
        if cap.isOpened():
            success, frame = cap.read()
            if not success or frame is None:
                cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
                time.sleep(0.01)
                continue
            with frame_lock:
                current_frame = frame.copy()
            time.sleep(0.033)
        else:
            time.sleep(1)
            cap = get_video_source()

# เริ่ม Thread จัดการภาพ
threading.Thread(target=video_worker, daemon=True).start()

def udp_ip_broadcaster():
    """ยิงสัญญาณ IP ของคอมพิวเตอร์ออกเครือข่าย เพื่อให้แว่น Pico ตรวจจับได้อัตโนมัติ"""
    broadcast_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    broadcast_sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
    while True:
        try:
            # ยิงข้อความ PICO_SERVER_HERE ไปที่พอร์ต 5005 ทุกๆ 1 วินาที
            broadcast_sock.sendto(b"PICO_SERVER_HERE", ('<broadcast>', 5005))
        except Exception:
            pass
        time.sleep(1.0)

threading.Thread(target=udp_ip_broadcaster, daemon=True).start()

def get_cropped_frame(frame, mode="360", center_angle=0):
    if mode != "180":
        return frame
    h, w, _ = frame.shape
    doubled = np.hstack([frame, frame])
    fov_w = w // 2
    center_x = int(((center_angle % 360) / 360.0) * w)
    start_x = center_x - (fov_w // 2) + w
    return doubled[:, start_x : start_x + fov_w]

@app.route('/frame')
def get_single_frame():
    mode = request.args.get('mode', '360')
    angle = float(request.args.get('angle', 0))

    with frame_lock:
        if current_frame is None:
            return ("Server starting...", 503)
        frame = current_frame.copy()

    processed = get_cropped_frame(frame, mode=mode, center_angle=angle)
    # บีบอัดเป็น JPEG คุณภาพ 60 เพื่อเพิ่มเฟรมเรตและลดกระตุก
    _, buffer = cv2.imencode('.jpg', processed, [cv2.IMWRITE_JPEG_QUALITY, 60])
    return Response(buffer.tobytes(), mimetype='image/jpeg')

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, threaded=True)