#include <WiFi.h>
#include <PubSubClient.h>

// 1. ตั้งค่า Wi-Fi
#define WIFI_STA_NAME "Xiaomi 14T"
#define WIFI_STA_PASS "noobshit"

// 2. ตั้งค่า MQTT Broker
#define MQTT_SERVER   "broker.mqttdashboard.com"
#define MQTT_PORT     1883
#define MQTT_USERNAME "test"
#define MQTT_PASSWORD "test"

#define MQTT_NAME     "esp32_tu_lab4_unique_01"
#define MQTT_TOPIC    "PhisanTopic"

// 3. กำหนดขาพิน
#define BUTTON_PIN 4  // ขา D4 ต่อสวิตช์ปุ่มกด
#define LED_PIN    2  // ขา D2 ต่อหลอด LED

WiFiClient client;
PubSubClient mqtt(client);

int lastButtonState = HIGH;
bool ledState = false; // ตัวแปรเก็บสถานะไฟ (false = ดับ, true = ติด)

// เมื่อมีข้อความเข้ามา -> สลับสถานะหลอดไฟ (Toggle)
void callback(char* topic, byte* payload, unsigned int length) {
  ledState = !ledState; // สลับสถานะ (ถ้าดับให้ติด ถ้าติดให้ดับ)
  digitalWrite(LED_PIN, ledState ? HIGH : LOW);

  Serial.print("Message arrived [");
  Serial.print(topic);
  Serial.print("] -> LED Toggled: ");
  Serial.println(ledState ? "ON" : "OFF");
}

void reconnect() {
  while (!mqtt.connected()) {
    Serial.print("Connecting to MQTT...");
    if (mqtt.connect(MQTT_NAME, MQTT_USERNAME, MQTT_PASSWORD)) {
      Serial.println(" Connected!");
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

  pinMode(BUTTON_PIN, INPUT_PULLUP);
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, LOW); // เริ่มต้นให้ไฟดับ

  Serial.println("Connecting to WiFi...");
  WiFi.begin(WIFI_STA_NAME, WIFI_STA_PASS);

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWiFi Connected!");

  mqtt.setServer(MQTT_SERVER, MQTT_PORT);
  mqtt.setCallback(callback);
}

void loop() {
  if (!mqtt.connected()) {
    reconnect();
  }
  mqtt.loop();

  // ตรวจจับการกดปุ่มบน Breadboard เพื่อสั่ง Toggle ด้วย
  int currentButtonState = digitalRead(BUTTON_PIN);
  if (lastButtonState == HIGH && currentButtonState == LOW) {
    Serial.println("Button Pressed -> Publishing TOGGLE...");
    mqtt.publish(MQTT_TOPIC, "TOGGLE");
    delay(250); // ป้องกันสัญญาณเด้ง (Debounce)
  }
  lastButtonState = currentButtonState;
}