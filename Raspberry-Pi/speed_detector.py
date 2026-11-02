from gpiozero import LED, Button
from time import time

s1, s2 = Button(5), Button(6)
green, yellow, red = LED(17), LED(27), LED(22)
DIST = 20  # เมตร
count = 0

while True:
    s1.wait_for_press()
    t1 = time()
    s2.wait_for_press()
    t2 = time()
    s1.wait_for_release()
    s2.wait_for_release()

    dt = t2 - t1
    speed_kmh = (DIST / dt) * 3.6
    count += 1

    print(f"จำนวนรถทั้งหมด: {count} คัน")
    print(f"ความเร็วคันล่าสุด: {speed_kmh:.1f} km/h")

    green.off(); yellow.off(); red.off()
    if speed_kmh <= 60:
        green.on()
    elif speed_kmh <= 80:
        yellow.on()
    else:
        red.on()
