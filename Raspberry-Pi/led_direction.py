from gpiozero import LED, Button
from time import sleep

leds = [LED(17), LED(27), LED(22)]   # ซ้าย → ขวา
sw1 = Button(5)
sw2 = Button(6)

pos = 1
direction = 1   # 0 = หยุด, 1 = ไปทางซ้าย, 2 = ไปทางขวา

while True:
    # step 1: อ่านค่าจาก switch
    if sw1.is_pressed and not sw2.is_pressed:
        direction = 2
    elif not sw1.is_pressed and sw2.is_pressed:
        direction = 1
    elif sw1.is_pressed and sw2.is_pressed:
        direction = 0

    # step 2: แสดงผล LED
    for l in leds:
        l.off()
    leds[pos - 1].on()

    # step 3: ขยับตำแหน่ง
    if direction == 1:
        pos -= 1
        if pos < 1:
            pos = 3
    elif direction == 2:
        pos += 1
        if pos > 3:
            pos = 1

    sleep(0.3)
