from gpiozero import LED, Button
from time import sleep, time

car_g, car_y, car_r = LED(17), LED(27), LED(22)
ped_g, ped_r = LED(23), LED(24)
btn = Button(5)

def car_state(g, y, r):
    car_g.value, car_y.value, car_r.value = g, y, r

def ped_state(g, r):
    ped_g.value, ped_r.value = g, r

while True:
    # สถานะปกติ: รถเขียว คนแดง
    car_state(1, 0, 0)
    ped_state(0, 1)
    btn.wait_for_press()

    # รถเหลือง → แดง
    car_state(0, 1, 0)
    sleep(2)
    car_state(0, 0, 1)

    # คนข้ามเขียว (ขยายเวลาได้ถ้ากดซ้ำ)
    ped_state(1, 0)
    end = time() + 5
    while time() < end:
        if btn.is_pressed:
            end = time() + 5       # ขยายเวลา
            sleep(0.3)             # กันกดซ้ำรัว
        sleep(0.05)

    # ไฟคนข้ามกะพริบเตือน
    for _ in range(3):
        ped_g.off(); sleep(0.3)
        ped_g.on(); sleep(0.3)
