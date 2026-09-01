import os
import threading

from PIL import Image


class FrameSourceError(RuntimeError):
    """Raised when a configured camera source cannot provide a frame."""


class MockPanoSource:
    def __init__(self, base_dir):
        self.path = os.environ.get("PANO_PATH", os.path.join(base_dir, "assets", "pano.jpg"))
        self._cached_image = None

    def get_image(self):
        if self._cached_image is None:
            if not os.path.exists(self.path):
                raise FileNotFoundError(
                    f"ไม่พบ {self.path} — รัน `python3 make_fake_pano.py` ก่อน"
                )
            with Image.open(self.path) as image:
                self._cached_image = image.convert("RGB")
        return self._cached_image

    def info(self):
        return {"source": "mock", "source_detail": self.path}


class OpenCvPanoSource:
    def __init__(self, mode):
        try:
            import cv2
        except ImportError as error:
            raise FrameSourceError(
                "โหมดกล้องต้องติดตั้ง OpenCV ก่อน: pip install -r requirements-camera.txt"
            ) from error

        raw_camera_url = os.environ.get("CAMERA_URL", "").strip()
        if not raw_camera_url:
            raise FrameSourceError(
                "กรุณาตั้ง CAMERA_URL เช่น rtsp://user:pass@camera/stream หรือ 0 สำหรับ USB camera"
            )

        self.cv2 = cv2
        self.mode = mode
        self.camera_url = int(raw_camera_url) if mode == "usb" and raw_camera_url.isdigit() else raw_camera_url
        self._capture = None
        self._lock = threading.Lock()

    def _open_capture(self):
        capture = self.cv2.VideoCapture(self.camera_url)
        if not capture.isOpened():
            capture.release()
            raise FrameSourceError(f"เปิดกล้องไม่ได้: {self.camera_url}")
        self._capture = capture

    def _read_frame(self):
        if self._capture is None:
            self._open_capture()

        ok, bgr_frame = self._capture.read()
        if not ok:
            self._capture.release()
            self._capture = None
            self._open_capture()
            ok, bgr_frame = self._capture.read()
            if not ok:
                raise FrameSourceError(f"อ่านเฟรมจากกล้องไม่ได้: {self.camera_url}")
        return bgr_frame

    def get_image(self):
        with self._lock:
            bgr_frame = self._read_frame()
        rgb_frame = self.cv2.cvtColor(bgr_frame, self.cv2.COLOR_BGR2RGB)
        return Image.fromarray(rgb_frame, mode="RGB")

    def info(self):
        return {"source": self.mode, "source_detail": str(self.camera_url)}


def create_frame_source(base_dir):
    source_mode = os.environ.get("PANO_SOURCE", "mock").lower()
    if source_mode == "mock":
        return MockPanoSource(base_dir)
    if source_mode in ("rtsp", "usb"):
        return OpenCvPanoSource(source_mode)
    raise FrameSourceError("PANO_SOURCE ต้องเป็น mock, rtsp หรือ usb")
