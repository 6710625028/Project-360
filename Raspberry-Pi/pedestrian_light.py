from gpiozero import LED, Button
from time import sleep, time

# ไฟรถ
car_g = LED(17)
car_y = LED(27)
car_r = LED(22)

# ไฟคนข้าม
ped_g = LED(23)
ped_y = LED(25)
ped_r = LED(24)

btn = Button(5)

CROSS_TIME = 5


def car_state(g, y, r):
    car_g.value, car_y.value, car_r.value = g, y, r


def ped_state(g, y, r):
    ped_g.value, ped_y.value, ped_r.value = g, y, r


while True:
    # ปกติ: รถเขียว คนแดง
    car_state(1, 0, 0)
    ped_state(0, 0, 1)
    btn.wait_for_press()

    # รถเหลือง แล้วแดง
    car_state(0, 1, 0)
    sleep(2)
    car_state(0, 0, 1)
    sleep(1)

    # คนข้ามเขียว (กดซ้ำ = ขยายเวลา)
    ped_state(1, 0, 0)
    end = time() + CROSS_TIME
    while time() < end:
        if btn.is_pressed:
            end = time() + CROSS_TIME
            sleep(0.3)
        sleep(0.05)

    # คนข้ามเหลืองกะพริบเตือน
    ped_state(0, 0, 0)
    for _ in range(3):
        ped_y.on()
        sleep(0.3)
        ped_y.off()
        sleep(0.3)

    # คนแดง รถเหลือง แล้ววนกลับไปรถเขียว
    ped_state(0, 0, 1)
    car_state(0, 1, 0)
    sleep(1)
