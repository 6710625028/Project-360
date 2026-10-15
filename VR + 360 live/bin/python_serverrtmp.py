import os
import time
import threading
from pathlib import Path
import cv2
from fastapi import FastAPI
from fastapi.responses import HTMLResponse, StreamingResponse

# ปิด Buffer ของ FFmpeg ภายใน OpenCV เพื่อลดอาการภาพดีเลย์สำหรับสตรีมสด
os.environ["OPENCV_FFMPEG_CAPTURE_OPTIONS"] = "fflags;nobuffer|flags;low_delay"

app = FastAPI(title="Pico 4 360 RTMP Server")

BASE_DIR = Path(__file__).resolve().parent
HTML_FILE = BASE_DIR / "web_app.html"
VIDEO_FALLBACK = BASE_DIR / "sample_360.mp4"

# ช่องรับสัญญาณ RTMP จาก MediaMTX
RTMP_URL = "rtmp://localhost:1935/live"

class RTMPStreamManager:
    """คลาสจัดการดึงภาพแบบมัลติเธรด ดึงเฉพาะเฟรมล่าสุดเพื่อป้องกันภาพหน่วงสะสม"""
    def __init__(self, rtmp_url, fallback_path):
        self.rtmp_url = rtmp_url
        self.fallback_path = fallback_path
        self.cap = None
        self.is_rtmp = False
        self.latest_frame = None
        self.running = True
        self.lock = threading.Lock()
        
        self.thread = threading.Thread(target=self._capture_loop, daemon=True)
        self.thread.start()

    def _connect(self):
        if self.cap is not None:
            self.cap.release()

        # 1. พยายามเชื่อมต่อ RTMP สดจากกล้องก่อน
        cap = cv2.VideoCapture(self.rtmp_url)
        if cap.isOpened():
            ret, _ = cap.read()
            if ret:
                print("[INFO] เชื่อมต่อสัญญาณ RTMP 360° สำเร็จ!")
                self.cap = cap
                self.is_rtmp = True
                return

        # 2. หากยังไม่มีสตรีม ให้สลับไปใช้วิดีโอสำรองหรือกล้องคอม
        print("[WARN] ยังไม่มีสัญญาณ RTMP -> สลับไปใช้โหมดสำรองชั่วคราว")
        if self.fallback_path.exists():
            self.cap = cv2.VideoCapture(str(self.fallback_path))
            self.is_rtmp = False
        else:
            self.cap = cv2.VideoCapture(0, cv2.CAP_DSHOW)
            self.is_rtmp = False

    def _capture_loop(self):
        self._connect()
        while self.running:
            if self.cap is None or not self.cap.isOpened():
                time.sleep(1)
                self._connect()
                continue

            ret, frame = self.cap.read()
            if not ret:
                if not self.is_rtmp:
                    # วนลูปเล่นวิดีโอตัวอย่างซ้ำ
                    self.cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
                    continue
                else:
                    # กรณีสัญญาณสตรีมจากกล้องขาดหาย
                    print("[WARN] สัญญาณกล้องหลุด กำลังรอเชื่อมต่อใหม่...")
                    time.sleep(0.5)
                    self._connect()
                    continue

            with self.lock:
                self.latest_frame = frame

            # ถ้าเป็นวิดีโอสำรองให้หน่วงตาม Frame Rate ถ้าเป็น RTMP ให้อ่านเฟรมทิ้งทันทีเพื่อลดแล็ก
            if not self.is_rtmp:
                time.sleep(0.03)

    def get_frame_bytes(self):
        with self.lock:
            if self.latest_frame is None:
                return None
            frame = self.latest_frame.copy()

        # บีบอัดเฟรมเป็น JPEG (คุณภาพ 80 เหมาะกับ Wi-Fi)
        _, buffer = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 80])
        return buffer.tobytes()

stream_manager = RTMPStreamManager(RTMP_URL, VIDEO_FALLBACK)

def generate_stream():
    while True:
        frame_bytes = stream_manager.get_frame_bytes()
        if frame_bytes is None:
            time.sleep(0.01)
            continue

        yield (b'--frame\r\n'
               b'Content-Type: image/jpeg\r\n\r\n' + frame_bytes + b'\r\n')
        time.sleep(0.016)

@app.get("/", response_class=HTMLResponse)
def index():
    if not HTML_FILE.exists():
        return HTMLResponse("<h3>ไม่พบไฟล์ web_app.html</h3>", status_code=404)
    return HTML_FILE.read_text(encoding="utf-8")

@app.get("/video_feed")
def video_feed():
    return StreamingResponse(
        generate_stream(),
        media_type="multipart/x-mixed-replace; boundary=frame"
    )

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)