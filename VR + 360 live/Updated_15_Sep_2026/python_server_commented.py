# ===== ส่วนที่ 1: นำเข้าไลบรารี =====
from pathlib import Path  # ใช้จัดการที่อยู่ไฟล์/โฟลเดอร์แบบ object ไม่ต้องกังวลเรื่อง \ กับ /
import threading  # ใช้สร้าง thread แยกสำหรับอ่านกล้องอยู่เบื้องหลัง
import time  # ใช้ sleep() เพื่อหน่วงเวลาในลูป
import cv2  # OpenCV: เปิดกล้อง อ่านเฟรม และบีบอัดเป็น JPEG
from fastapi import FastAPI  # เฟรมเวิร์กสร้างเว็บเซิร์ฟเวอร์
from fastapi.responses import HTMLResponse, StreamingResponse  # ตอบกลับเป็นหน้า HTML / เป็นสตรีมต่อเนื่อง

# ===== ส่วนที่ 2: สร้างแอปและตั้งค่าคงที่ =====
app = FastAPI(title="Pico 4 360 Live Server")  # สร้างตัวแอป (uvicorn เรียกใช้ผ่านชื่อ python_server:app)

BASE_DIR = Path(__file__).resolve().parent  # โฟลเดอร์ที่ไฟล์ .py นี้อยู่ (path เต็ม)
HTML_FILE = BASE_DIR / "web_app.html"  # ตำแหน่งไฟล์หน้าเว็บที่จะส่งให้แว่น
VIDEO_FALLBACK = BASE_DIR / "sample_360.mp4"  # ไฟล์วิดีโอสำรองไว้ใช้เมื่อไม่มีกล้อง

CAMERA_INDEX = 0  # เลขกล้องใน Windows: 0 = Insta360, 1 = กล้องหน้าโน้ตบุ๊ก
FRAME_WIDTH = 1920  # ความกว้างภาพที่ขอจากกล้อง (พิกเซล)
FRAME_HEIGHT = 960  # ความสูงภาพที่ขอจากกล้อง (กว้าง:สูง = 2:1 ตรงกับภาพ equirectangular 360°)
JPEG_QUALITY = 80  # คุณภาพ JPEG 0-100 (สูง = คมแต่ไฟล์ใหญ่ ใช้เน็ตมาก)


# ===== ส่วนที่ 3: ฟังก์ชันเปิดกล้อง =====
def open_capture():
    """เปิดกล้อง ถ้าไม่ได้ให้ใช้ไฟล์สำรอง คืนค่า (ตัวอ่านภาพ, เป็นกล้องสดหรือไม่)"""
    cap = cv2.VideoCapture(CAMERA_INDEX, cv2.CAP_DSHOW)  # เปิดกล้องผ่าน DirectShow (ระบบกล้องของ Windows)

    if cap.isOpened():  # ถ้าเปิดกล้องสำเร็จ
        cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))  # ขอให้กล้องส่งภาพแบบ MJPG (กินแบนด์วิดท์ USB น้อย)
        cap.set(cv2.CAP_PROP_FRAME_WIDTH, FRAME_WIDTH)  # ขอความกว้างภาพ (เป็นแค่คำขอ กล้องอาจไม่ทำตาม)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, FRAME_HEIGHT)  # ขอความสูงภาพ
        return cap, True  # คืนตัวอ่านภาพ และบอกว่าเป็นกล้องสด

    cap.release()  # เปิดไม่สำเร็จ: คืนทรัพยากรของตัวนี้ก่อน กันค้างในระบบ
    print(f"[คำเตือน] ไม่พบกล้องที่ Index {CAMERA_INDEX}")  # แจ้งเตือนในหน้าต่าง console

    if VIDEO_FALLBACK.exists():  # ถ้ามีไฟล์วิดีโอสำรองอยู่
        print("-> ใช้ไฟล์สำรอง sample_360.mp4")  # แจ้งว่ากำลังใช้ไฟล์แทน
        return cv2.VideoCapture(str(VIDEO_FALLBACK)), False  # เปิดไฟล์ (ต้องแปลง Path เป็น str) และบอกว่าไม่ใช่กล้องสด

    print("-> ลองกล้อง Index 1")  # ไม่มีไฟล์สำรอง จึงลองกล้องตัวถัดไป
    return cv2.VideoCapture(1, cv2.CAP_DSHOW), True  # เปิดกล้อง index 1 และถือว่าเป็นกล้องสด


# ===== ส่วนที่ 4: คลาสอ่านกล้องใน thread แยก =====
class CameraReader:
    """อ่านกล้องเพียงตัวเดียวเบื้องหลัง แล้วเก็บเฟรมล่าสุดให้ทุกผู้ชมมาหยิบไปใช้ร่วมกัน"""

    def __init__(self):  # ตัวสร้าง: ทำงานตอนสร้างออบเจ็กต์
        self.lock = threading.Lock()  # กุญแจกันสอง thread แก้ข้อมูลเฟรมพร้อมกัน
        self.latest_jpeg = None  # เก็บเฟรมล่าสุดที่บีบอัดเป็น JPEG แล้ว (เริ่มต้นยังไม่มี)
        self.frame_id = 0  # ตัวนับเฟรม ใช้บอกผู้ชมว่ามีเฟรมใหม่หรือยัง
        self.running = True  # ธงควบคุมลูป: False = สั่งให้ thread หยุด
        self.cap, self.is_live = open_capture()  # เปิดกล้องและจำไว้ว่าเป็นกล้องสดหรือไฟล์
        self.delay = 0.016  # เวลาพักต่อรอบ (~60 fps) ใช้ตอนอ่านกล้องสด
        self.thread = threading.Thread(target=self._loop, daemon=True)  # สร้าง thread; daemon=True คือปิดโปรแกรมแล้ว thread ดับตาม
        self.thread.start()  # เริ่มให้ thread ทำงานทันที

    def _loop(self):  # ลูปหลักที่รันใน thread เบื้องหลัง
        while self.running:  # วนต่อไปเรื่อยๆ จนกว่าจะสั่งหยุด
            if not self.cap.isOpened():  # ถ้ากล้องหลุด (เช่น ถอดสาย USB)
                self.cap.release()  # คืนของเก่าก่อน ไม่ให้เปิดซ้อนกัน
                time.sleep(0.5)  # รอครึ่งวินาทีก่อนลองใหม่ ไม่รัวจนหนักเครื่อง
                self.cap, self.is_live = open_capture()  # ลองเปิดกล้องใหม่
                continue  # กลับขึ้นไปต้นลูป

            success, frame = self.cap.read()  # อ่านเฟรม 1 ภาพ: success = อ่านได้ไหม, frame = ภาพ (NumPy array)

            if not success:  # ถ้าอ่านเฟรมไม่สำเร็จ
                if self.is_live:  # กรณีเป็นกล้องสด แปลว่าสะดุดชั่วคราว
                    time.sleep(0.01)  # รอ 10 มิลลิวินาทีแล้วลองใหม่
                else:  # กรณีเป็นไฟล์วิดีโอ แปลว่าเล่นจบแล้ว
                    self.cap.set(cv2.CAP_PROP_POS_FRAMES, 0)  # กรอกลับไปเฟรมแรกเพื่อเล่นวน
                continue  # กลับขึ้นไปต้นลูป

            ok, buffer = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, JPEG_QUALITY])  # บีบอัดเฟรมเป็น JPEG ในหน่วยความจำ
            if ok:  # ถ้าบีบอัดสำเร็จ
                with self.lock:  # ล็อกกุญแจก่อนแก้ข้อมูลที่ใช้ร่วมกัน
                    self.latest_jpeg = buffer.tobytes()  # เก็บเฟรมล่าสุดเป็นไบต์ธรรมดา
                    self.frame_id += 1  # เพิ่มตัวนับเพื่อบอกว่ามีเฟรมใหม่

            if not self.is_live:  # ถ้าเป็นไฟล์วิดีโอ
                fps = self.cap.get(cv2.CAP_PROP_FPS) or 30  # อ่าน fps จริงของไฟล์ ถ้าไม่ได้ค่าให้ใช้ 30
                time.sleep(1.0 / fps)  # พักตาม fps จริง เพื่อให้วิดีโอเล่นด้วยความเร็วปกติ
            else:  # ถ้าเป็นกล้องสด
                time.sleep(self.delay)  # พักเล็กน้อยกัน CPU พุ่ง (ความเร็วจริงถูกจำกัดด้วยกล้องอยู่แล้ว)

    def get_frame(self):  # ให้ผู้ชมเรียกเพื่อขอเฟรมล่าสุด
        with self.lock:  # ล็อกกุญแจขณะอ่าน กันอ่านระหว่างที่ thread กำลังเขียน
            return self.latest_jpeg, self.frame_id  # คืน (ไบต์ JPEG ล่าสุด, หมายเลขเฟรม)


reader = CameraReader()  # สร้างตัวอ่านกล้อง 1 ตัวตอนเซิร์ฟเวอร์เริ่ม ทุกผู้ชมใช้ร่วมกัน


# ===== ส่วนที่ 5: ตัวสร้างสตรีม MJPEG สำหรับผู้ชมแต่ละคน =====
def generate_stream():
    """generator ที่ส่งเฟรมให้ผู้ชม 1 คน (ผู้ชมแต่ละคนมี generator ของตัวเอง)"""
    last_id = -1  # จำหมายเลขเฟรมที่ส่งไปแล้ว เริ่มที่ -1 เพื่อให้เฟรมแรกถูกส่งแน่นอน
    while True:  # วนส่งไปเรื่อยๆ จนผู้ชมปิดหน้า (FastAPI จะหยุด generator ให้เอง)
        frame_bytes, frame_id = reader.get_frame()  # ขอเฟรมล่าสุดจากตัวอ่านกล้อง

        if frame_bytes is None or frame_id == last_id:  # ถ้ายังไม่มีเฟรม หรือยังเป็นเฟรมเดิมที่เคยส่งแล้ว
            time.sleep(0.005)  # รอ 5 มิลลิวินาทีให้เฟรมใหม่มา (กันส่งภาพซ้ำเปลืองเน็ต)
            continue  # กลับไปขอใหม่

        last_id = frame_id  # จำว่าเฟรมนี้ส่งแล้ว

        yield (  # ส่งข้อมูลออกไป 1 ก้อน แล้วหยุดรอรอบถัดไป
            b"--frame\r\n"  # ตัวคั่น (boundary) บอกเบราว์เซอร์ว่าเริ่มภาพใหม่
            b"Content-Type: image/jpeg\r\n\r\n"  # header บอกว่าข้อมูลถัดไปคือภาพ JPEG (\r\n\r\n = บรรทัดว่างคั่น)
            + frame_bytes  # ไบต์ของภาพ JPEG จริงๆ
            + b"\r\n"  # ปิดท้ายส่วนนี้ด้วยขึ้นบรรทัดใหม่
        )


# ===== ส่วนที่ 6: เส้นทาง (Routes) ของเว็บ =====
@app.get("/", response_class=HTMLResponse)  # เมื่อเปิดที่อยู่หลัก "/" ให้เรียกฟังก์ชันนี้ และตอบเป็น HTML
def index():
    if not HTML_FILE.exists():  # ถ้าไม่พบไฟล์ web_app.html
        return HTMLResponse(  # ตอบกลับหน้าแจ้งข้อผิดพลาดแทน
            f"<h3>ไม่พบไฟล์ web_app.html</h3><p>กรุณาวางไฟล์ไว้ที่: {HTML_FILE}</p>",  # ข้อความบอกตำแหน่งที่ต้องวางไฟล์
            status_code=404,  # รหัส 404 = Not Found
        )
    return HTML_FILE.read_text(encoding="utf-8")  # อ่านไฟล์เป็น UTF-8 (รองรับภาษาไทย) แล้วส่งให้เบราว์เซอร์


@app.get("/video_feed")  # เมื่อเปิดที่อยู่ "/video_feed" ให้เรียกฟังก์ชันนี้
def video_feed():
    return StreamingResponse(  # ตอบกลับแบบสตรีมที่ไม่ปิดการเชื่อมต่อ
        generate_stream(),  # แหล่งข้อมูล: generator ที่ผลิตเฟรมไม่รู้จบ
        media_type="multipart/x-mixed-replace; boundary=frame",  # บอกเบราว์เซอร์ว่าแต่ละส่วนใหม่ "แทนที่" ส่วนเก่า; boundary ต้องตรงกับ --frame
    )


# ===== ส่วนที่ 7: รันโดยตรง =====
if __name__ == "__main__":  # ทำงานเฉพาะเมื่อสั่ง python python_server_commented.py ตรงๆ
    import uvicorn  # นำเข้าตัวรันเซิร์ฟเวอร์ตรงนี้ เพราะใช้เฉพาะกรณีนี้
    uvicorn.run(app, host="0.0.0.0", port=8000)  # รับการเชื่อมต่อจากทุกอุปกรณ์ ที่พอร์ต 8000 (ตรงกับ ngrok)
