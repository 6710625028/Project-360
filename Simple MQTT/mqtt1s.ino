#include <WiFi.h>
#include <PubSubClient.h>

// 1. ตั้งค่า Wi-Fi (ใส่ชื่อและรหัสผ่าน Wi-Fi 2.4GHz หรือ Hotspot จากมือถือ)
#define WIFI_STA_NAME "Xiaomi 14T"
#define WIFI_STA_PASS "noobshit"

// 2. ตั้งค่า MQTT Broker (ตามที่กำหนดในสไลด์)
#define MQTT_SERVER   "broker.mqttdashboard.com"
#define MQTT_PORT     1883
#define MQTT_USERNAME "test"
#define MQTT_PASSWORD "test"

// Client ID ต้องไม่ซ้ำกับคนอื่น เพื่อป้องกันไม่ให้ Broker ตัดการเชื่อมต่อ
#define MQTT_NAME     "esp32_tu_lab4_unique_01" 
#define MQTT_TOPIC    "PhisanTopic"

// 3. กำหนดขาพินตามที่ต่อวงจรไว้จริง
#define BUTTON_PIN 4  // ขา D4 บนบอร์ด ต่อเข้ากับปุ่มกด
#define LED_PIN    2  // ขา D2 บนบอร์ด ต่อเข้ากับตัวต้านทานและหลอด LED

WiFiClient client;
PubSubClient mqtt(client);

int lastButtonState = HIGH;

// ฟังก์ชันฝั่งรับ (Subscriber): ทำงานอัตโนมัติเมื่อมีข้อความส่งเข้ามาใน MQTT Topic
void callback(char* topic, byte* payload, unsigned int length) {
  Serial.print("Message arrived [");
  Serial.print(topic);
  Serial.println("] -> Turn ON LED for 1 second");

  // สั่งไฟติดค้างไว้ 1 วินาทีแล้วดับลงอัตโนมัติ (ตามโจทย์ Slide 23)
  digitalWrite(LED_PIN, HIGH);
  delay(1000);
  digitalWrite(LED_PIN, LOW);
}

// ฟังก์ชันเชื่อมต่อ MQTT Broker และเชื่อมต่อใหม่หากสัญญาณหลุด
void reconnect() {
  while (!mqtt.connected()) {
    Serial.print("Connecting to MQTT...");
    if (mqtt.connect(MQTT_NAME, MQTT_USERNAME, MQTT_PASSWORD)) {
      Serial.println(" Connected!");
      // ทำการ Subscribe หัวข้อรอรับคำสั่งทันทีที่ต่อสำเร็จ
      mqtt.subscribe(MQTT_TOPIC);
    } else {
      Serial.print(" Failed, state=");
      Serial.print(mqtt.state());
      Serial.println(" Retrying in 2 seconds...");
      delay(2000);
    }
  }
}

void setup() {
  Serial.begin(115200);

  // ตั้งค่าโหมดของขาพิน
  pinMode(BUTTON_PIN, INPUT_PULLUP); // เปิดใช้ตัวต้านทานดึงไฟขึ้นภายในชิป
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, LOW);        // ปิดไฟไว้ล่วงหน้าตอนเริ่มทำงาน

  // เริ่มเชื่อมต่อ Wi-Fi
  Serial.println("Connecting to WiFi...");
  WiFi.begin(WIFI_STA_NAME, WIFI_STA_PASS);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWiFi Connected!");

  // ตั้งค่าเซิร์ฟเวอร์ MQTT และผูกฟังก์ชันรับข้อมูล (callback)
  mqtt.setServer(MQTT_SERVER, MQTT_PORT);
  mqtt.setCallback(callback);
}

void loop() {
  // รักษาการเชื่อมต่อกับ Broker
  if (!mqtt.connected()) {
    reconnect();
  }
  mqtt.loop(); // ตรวจสอบและดักฟังข้อความขาเข้า

  // ส่วนฝั่งส่ง (Publisher): ตรวจจับจังหวะการกดปุ่ม
  int currentButtonState = digitalRead(BUTTON_PIN);

  // ตรวจจับเมื่อสถานะเปลี่ยนจากปล่อย (HIGH) มาเป็นกด (LOW)
  if (lastButtonState == HIGH && currentButtonState == LOW) {
    Serial.println("Button Pressed -> Publishing to MQTT...");
    mqtt.publish(MQTT_TOPIC, "PRESSED");
    delay(250); // ป้องกันสัญญาณเด้งซ้ำ (Debounce) จากหน้าสัมผัสของสวิตช์
  }

  lastButtonState = currentButtonState;
}