
import io
import math
import os

import numpy as np
from flask import Flask, jsonify, render_template, request, send_file
from PIL import Image

from frame_source import FrameSourceError, create_frame_source
 
app = Flask(__name__)

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
PANO_SOURCE = create_frame_source(BASE_DIR)
 
 
def get_pano_image():
    """คืนเฟรม equirectangular ล่าสุดจาก mock หรือกล้องจริง"""
    return PANO_SOURCE.get_image()


def source_error_response(error):
    status = 404 if isinstance(error, FileNotFoundError) else 503
    return jsonify({"error": str(error)}), status
 
 
def equirect_to_perspective(equirect_img, yaw_deg, pitch_deg, fov_deg, out_w=800, out_h=800):
    """
    Crop มุมมองแบบ perspective จากภาพ equirectangular
    ตามหลักการ standard equirectangular-to-perspective reprojection
    (คล้ายวิธีที่ Google Street View / VR viewer ใช้)
    """
    equirect = np.array(equirect_img)
    eh, ew, _ = equirect.shape
 
    yaw = math.radians(yaw_deg)
    pitch = math.radians(pitch_deg)
    fov = math.radians(fov_deg)
 
    # สร้าง grid พิกัดของภาพผลลัพธ์ในพิกัด camera space
    x = np.linspace(-math.tan(fov / 2), math.tan(fov / 2), out_w)
    y = np.linspace(-math.tan(fov / 2), math.tan(fov / 2), out_h)
    xv, yv = np.meshgrid(x, -y)  # กลับแกน y ให้บนเป็นบน
    zv = np.ones_like(xv)
 
    # normalize เป็นเวกเตอร์ทิศทาง
    norm = np.sqrt(xv**2 + yv**2 + zv**2)
    xv, yv, zv = xv / norm, yv / norm, zv / norm
 
    # หมุนตาม pitch (แกน x) แล้วตาม yaw (แกน y)
    # pitch rotation
    y2 = yv * math.cos(pitch) - zv * math.sin(pitch)
    z2 = yv * math.sin(pitch) + zv * math.cos(pitch)
    x2 = xv
 
    # yaw rotation
    x3 = x2 * math.cos(yaw) + z2 * math.sin(yaw)
    z3 = -x2 * math.sin(yaw) + z2 * math.cos(yaw)
    y3 = y2
 
    # แปลงเวกเตอร์ทิศทาง (x3,y3,z3) กลับเป็นพิกัด lon/lat บนภาพ equirectangular
    lon = np.arctan2(x3, z3)
    lat = np.arcsin(np.clip(y3, -1, 1))
 
    src_x = (lon / (2 * math.pi) + 0.5) * ew
    src_y = (0.5 - lat / math.pi) * eh
 
    # แนวนอนของ panorama เป็นวงกลม: pixel ที่เลยขอบต้องวนกลับไปอีกฝั่ง
    # ไม่ใช่ถูก clamp ที่ขอบ มิฉะนั้น crop ใกล้รอยต่อ 180° จะผิดรูป
    src_x = np.mod(np.rint(src_x).astype(np.intp), ew)
    src_y = np.clip(src_y, 0, eh - 1).astype(np.int32)
 
    out = equirect[src_y, src_x]
    return Image.fromarray(out)
 
 
@app.route("/", methods=["GET"])
def web_demo():
    """หน้าเว็บสำหรับสาธิต API ตาม yaw/pitch/fov/mode"""
    return render_template("index.html")


@app.route("/pano/full", methods=["GET"])
def pano_full():
    """คืนภาพ equirectangular เต็มใบ — ใช้แปะบน sphere ฝั่ง Unity"""
    try:
        img = get_pano_image()
    except (FileNotFoundError, FrameSourceError) as error:
        return source_error_response(error)
 
    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=85)
    buf.seek(0)
    return send_file(buf, mimetype="image/jpeg")
 
 
@app.route("/pano/view", methods=["GET"])
def pano_view():
    """คืนภาพ crop ตามองศา yaw/pitch/fov ที่ระบุ"""
    try:
        yaw = float(request.args.get("yaw", 0))
        pitch = float(request.args.get("pitch", 0))
        fov = float(request.args.get("fov", 90))
        mode = request.args.get("mode", "360").lower()
    except ValueError:
        return jsonify({"error": "yaw/pitch/fov ต้องเป็นตัวเลข"}), 400

    if not all(math.isfinite(value) for value in (yaw, pitch, fov)):
        return jsonify({"error": "yaw/pitch/fov ต้องเป็นจำนวนจริงที่มีขอบเขต"}), 400
 
    if mode not in ("360", "180"):
        return jsonify({"error": "mode ต้องเป็น 360 หรือ 180 เท่านั้น"}), 400
 
    # โหมด 180: จำลองกล้องแบบเห็นได้แค่ครึ่งซีกหน้า จำกัด yaw ไว้ -90..90
    if mode == "180":
        yaw = max(-90.0, min(90.0, yaw))
    else:
        yaw %= 360.0
 
    pitch = max(-90.0, min(90.0, pitch))
    fov = max(10.0, min(150.0, fov))
 
    try:
        equirect = get_pano_image()
    except (FileNotFoundError, FrameSourceError) as error:
        return source_error_response(error)
 
    cropped = equirect_to_perspective(equirect, yaw, pitch, fov)
 
    buf = io.BytesIO()
    cropped.save(buf, format="JPEG", quality=85)
    buf.seek(0)
    return send_file(buf, mimetype="image/jpeg")
 
 
@app.route("/pano/info", methods=["GET"])
def pano_info():
    """endpoint เสริม เอาไว้เช็คว่า server รันอยู่ + ดูขนาดภาพต้นทาง"""
    try:
        img = get_pano_image()
        return jsonify({
            "status": "ok",
            **PANO_SOURCE.info(),
            "width": img.width,
            "height": img.height,
        })
    except (FileNotFoundError, FrameSourceError) as error:
        status = 404 if isinstance(error, FileNotFoundError) else 503
        return jsonify({"status": "error", "message": str(error)}), status
 
 
if __name__ == "__main__":
    port = int(os.environ.get("PORT", "5000"))
    app.run(host="0.0.0.0", port=port, debug=False)
 
