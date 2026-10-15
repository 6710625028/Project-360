from pathlib import Path
import time
import cv2
from fastapi import FastAPI
from fastapi.responses import HTMLResponse, StreamingResponse

app = FastAPI(title="Pico 4 360 Live Server")

BASE_DIR = Path(__file__).resolve().parent
HTML_FILE = BASE_DIR / "web_app.html"
VIDEO_FALLBACK = BASE_DIR / "sample_360.mp4"

# =========================================================================
# การตั้งค่ากล้อง (CAMERA SETUP)
# 0 = Insta360 (จาก Device Manager)
# 1 = กล้อง Integrated Camera ของโน้ตบุ๊ก
# =========================================================================
CAMERA_INDEX = 0

def init_capture():
    """เปิดการเชื่อมต่อกล้อง Insta360 หรือใช้วิดีโอสำรองหากกล้องไม่พร้อม"""
    cap = cv2.VideoCapture(CAMERA_INDEX, cv2.CAP_DSHOW)
    
    # บังคับรูปแบบ MJPG เพื่อรองรับสัญญาณวิดีโอ 360° ความละเอียดสูงจาก Insta360
    cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*'MJPG'))
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1920)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 960)

    if not cap.isOpened():
        print(f"[คำเตือน] ไม่พบกล้องที่ Index {CAMERA_INDEX} -> สลับไปใช้ไฟล์สำรอง")
        if VIDEO_FALLBACK.exists():
            return cv2.VideoCapture(str(VIDEO_FALLBACK)), False
        else:
            return cv2.VideoCapture(1, cv2.CAP_DSHOW), True

    return cap, True

cap, is_live_camera = init_capture()

def generate_stream():
    global cap, is_live_camera
    while True:
        if not cap.isOpened():
            cap, is_live_camera = init_capture()
            time.sleep(0.5)
            continue

        success, frame = cap.read()

        if not success:
            if not is_live_camera:
                cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
                continue
            else:
                time.sleep(0.01)
                continue

        # บีบอัดเป็น JPEG ส่งไปยัง Web Client
        _, buffer = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 80])
        frame_bytes = buffer.tobytes()

        yield (b'--frame\r\n'
               b'Content-Type: image/jpeg\r\n\r\n' + frame_bytes + b'\r\n')

        time.sleep(0.016)

@app.get("/", response_class=HTMLResponse)
def index():
    if not HTML_FILE.exists():
        return HTMLResponse(
            f"<h3>ไม่พบไฟล์ web_app.html</h3><p>กรุณาวางไฟล์ไว้ที่: {HTML_FILE}</p>",
            status_code=404
        )
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