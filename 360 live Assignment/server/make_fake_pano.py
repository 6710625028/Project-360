from PIL import Image, ImageDraw
import os

WIDTH = 2048
HEIGHT = 1024  # equirectangular ต้องเป็นอัตราส่วน 2:1 เสมอ

ASSETS_DIR = os.path.join(os.path.dirname(__file__), "assets")
OUT_PATH = os.path.join(ASSETS_DIR, "pano.jpg")


def make_fake_panorama():
    os.makedirs(ASSETS_DIR, exist_ok=True)
    img = Image.new("RGB", (WIDTH, HEIGHT), "black")
    draw = ImageDraw.Draw(img)

    # แบ่งภาพเป็นแถบแนวตั้งตามทิศ (yaw 0-360) ระบายสีไล่เฉด
    # เพื่อให้เวลาหมุนหัวใน VR แล้วเห็นว่ามุมมองเปลี่ยนไปทิศไหนชัดเจน
    num_bands = 12
    band_width = WIDTH // num_bands
    for i in range(num_bands):
        # ภาพ equirectangular แบบมาตรฐานมี longitude 0° อยู่ตรงกึ่งกลางภาพ
        yaw_deg = int(-180 + i * 360 / num_bands)
        hue = int(255 * i / num_bands)
        color = (hue, 255 - hue, (hue * 2) % 255)
        x0 = i * band_width
        x1 = x0 + band_width
        draw.rectangle([x0, 0, x1, HEIGHT], fill=color)
        # ใส่ label องศาที่กึ่งกลางแถบ เพื่อเช็คด้วยตาว่า crop ถูกมุมไหม
        label = f"{yaw_deg:+d}°"
        text_x = x0 + band_width // 2 - 30
        text_y = HEIGHT // 2 - 15
        draw.text((text_x, text_y), label, fill="white")

    # เส้น horizon (pitch = 0) ให้เห็นชัดเจน
    draw.line([(0, HEIGHT // 2), (WIDTH, HEIGHT // 2)], fill="white", width=3)

    # เส้น "บน" (zenith) กับ "ล่าง" (nadir) แรเงาให้ดูออกว่าเป็นฟ้า/พื้น
    draw.rectangle([0, 0, WIDTH, HEIGHT // 6], fill=None, outline="white", width=2)
    draw.text((10, 10), "ZENITH (บนสุด)", fill="white")
    draw.text((10, HEIGHT - 30), "NADIR (ล่างสุด)", fill="white")

    img.save(OUT_PATH, quality=90)
    print(f"สร้างภาพ mock panorama แล้ว: {OUT_PATH} ({WIDTH}x{HEIGHT})")


if __name__ == "__main__":
    make_fake_panorama()
