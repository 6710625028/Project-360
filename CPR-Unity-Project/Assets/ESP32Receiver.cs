using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

public class ESP32Receiver : MonoBehaviour
{
    [Header("Core References")]
    public CPRLogicManager cprManager;
    public UIGraphRenderer graphRenderer;
    public CPRSensorSimulator simulator;
    [Header("3D Mannequin Visual")]
    public HumanChestCompressor chestCompressor;

    [Header("On-Screen Debug HUD (Optional)")]
    public TextMeshProUGUI debugLogText;

    [Header("Network Settings")]
    public int listenPort = 8055;

    [Header("Telemetry Monitoring")]
    public int latestPacketId = 0;
    public int rawPressure = 0;
    public long boardTimestampMs = 0;
    public int lostPacketCount = 0;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = false;

    private float latestNormalizedDepth = 0f;
    private float lastPacketReceivedTime = -999f;
    private int lastExpectedPacketId = -1;
    private string rawPayloadString = "";
    private bool hasNewPacket = false;
    private readonly object lockObject = new object();

    private float consoleLogTimer = 0f;

    void Start()
    {
        StartListening();
    }

    private void StartListening()
    {
        try
        {
            udpClient = new UdpClient(listenPort);
            isRunning = true;
            receiveThread = new Thread(ReceiveDataLoop)
            {
                IsBackground = true
            };
            receiveThread.Start();
            Debug.Log($"<color=cyan>[ESP32Receiver] UDP Socket opened on port {listenPort}</color>");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ESP32Receiver] Socket Error: {ex.Message}");
        }
    }

    private void ReceiveDataLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string text = Encoding.UTF8.GetString(data).Trim();

                string[] tokens = text.Split(',');
                if (tokens.Length >= 3)
                {
                    if (int.TryParse(tokens[0], out int pId) &&
                        int.TryParse(tokens[1], out int pressVal) &&
                        long.TryParse(tokens[2], out long tMs))
                    {
                        lock (lockObject)
                        {
                            if (lastExpectedPacketId != -1 && pId > lastExpectedPacketId + 1)
                            {
                                lostPacketCount += (pId - (lastExpectedPacketId + 1));
                            }
                            lastExpectedPacketId = pId;

                            latestPacketId = pId;
                            rawPressure = pressVal;
                            boardTimestampMs = tMs;
                            rawPayloadString = text;

                            latestNormalizedDepth = Mathf.Clamp01(pressVal / 4095f);
                            hasNewPacket = true; // แจ้ง Main Thread ว่ามีข้อมูลใหม่เข้ามา
                        }
                    }
                }
            }
            catch (SocketException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ESP32Receiver] Receive error: {ex.Message}");
            }
        }
    }

    void Update()
    {
        float depth = 0f;
        int pId = 0;
        int rawAdc = 0;
        string rawText = "";
        bool newPacketThisFrame = false;

        lock (lockObject)
        {
            // ตรวจสอบว่ามีข้อมูลแพ็กเก็ตใหม่เข้ามาจริงหรือไม่
            if (hasNewPacket)
            {
                lastPacketReceivedTime = Time.time;
                hasNewPacket = false;
                newPacketThisFrame = true;

                depth = latestNormalizedDepth;
                pId = latestPacketId;
                rawAdc = rawPressure;
                rawText = rawPayloadString;
            }
        }

        // สำคัญที่สุด: ประมวลผลเฉพาะเมื่อมี Packet ใหม่มาถึงจริง (ทุกๆ 30 ms ไม่ใช่ทุกเฟรมจอ)
        if (newPacketThisFrame)
        {
            // สั่งให้โมเดลคนไข้ยุบหน้าอกตามค่าแรงกดจริง
            if (chestCompressor != null)
                chestCompressor.SetDepth(depth);

            // 1. ส่งเข้า Logic คำนวณ
            if (cprManager != null)
                cprManager.ProcessSensorSample(depth, Time.time);

            // 2. เติมจุดลงกราฟ
            if (graphRenderer != null)
                graphRenderer.AddPoint(depth);

            // 3. อัปเดตตัวเลข Depth cm และเลื่อน Slider ตามค่าจริงจากเซนเซอร์
            if (simulator != null)
            {
                if (simulator.depthSlider != null)
                {
                    simulator.depthSlider.interactable = false;
                    simulator.depthSlider.SetValueWithoutNotify(depth); // ขยับก้าน Slider ตามคลื่น
                }

                if (simulator.depthValueText != null)
                {
                    float depthCm = depth * 7.0f;
                    string status = (depthCm >= 5.0f && depthCm <= 6.0f) ? "(Good Depth)" : (depthCm < 5.0f ? "(Too Shallow)" : "(Too Deep)");
                    if (depthCm <= 0.5f) status = "(Released)";

                    simulator.depthValueText.text = $"Depth: {depthCm:F1} cm {status}";
                }
            }

            // แสดง Log ใน Console ทุก 0.5 วินาที
            consoleLogTimer += Time.deltaTime;
            if (consoleLogTimer >= 0.5f)
            {
                consoleLogTimer = 0f;
                float depthCm = depth * 7.0f;
                Debug.Log($"<color=lime>[ESP32 Live]</color> \"{rawText}\" | Depth: {depthCm:F1} cm | Packet: #{pId} | Loss: {lostPacketCount}");
            }

            if (debugLogText != null)
            {
                float depthCm = depth * 7.0f;
                debugLogText.text = $"[ESP32] Packet: #{pId} | ADC: {rawAdc} | Depth: {depthCm:F1} cm";
            }
        }
    }
}