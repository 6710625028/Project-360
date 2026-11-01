using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

public class ESP32Receiver : MonoBehaviour
{
    [Header("Core System References")]
    public CPRLogicManager cprManager;
    public CPRSensorSimulator simulator;

    [Header("3D Mannequin Visual")]
    public HumanChestCompressor chestCompressor;

    [Header("Main Board Display (Stationary)")]
    [Tooltip("ลาก GraphPanel บน Canvas บอร์ดใหญ่มาใส่")]
    public UIGraphRenderer mainGraphRenderer;

    [Header("Camera Mini-HUD (Player View)")]
    [Tooltip("ลาก GraphPanel บน Camera_HUD_Canvas มาใส่")]
    public UIGraphRenderer hudGraphRenderer;
    [Tooltip("ลาก DepthText บน Camera_HUD_Canvas มาใส่")]
    public TextMeshProUGUI hudDepthText;
    [Tooltip("ลาก BpmText บน Camera_HUD_Canvas มาใส่")]
    public TextMeshProUGUI hudBpmText;

    [Header("Network Settings")]
    public int listenPort = 8888;

    [Header("Telemetry Monitoring")]
    public int latestPacketId = 0;
    public int rawPressure = 0;
    public long boardTimestampMs = 0;
    public int lostPacketCount = 0;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = false;

    private float latestNormalizedDepth = 0f;
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
            CloseSocket();

            // เชื่อมต่อแบบ Raw Socket เพื่อป้องกัน Winsock Error 10013 บน Windows
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(IPAddress.Any, listenPort));

            udpClient = new UdpClient { Client = socket };

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
                if (udpClient == null) break;

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
                            hasNewPacket = true;
                        }
                    }
                }
            }
            catch (SocketException)
            {
                if (!isRunning) break;
            }
            catch (ObjectDisposedException)
            {
                if (!isRunning) break;
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    Debug.LogWarning($"[ESP32Receiver] Receive error: {ex.Message}");
                }
            }
        }
    }

    void Update()
    {
        float depth = 0f;
        int pId = 0;
        string rawText = "";
        bool newPacketThisFrame = false;

        lock (lockObject)
        {
            if (hasNewPacket)
            {
                hasNewPacket = false;
                newPacketThisFrame = true;

                depth = latestNormalizedDepth;
                pId = latestPacketId;
                rawText = rawPayloadString;
            }
        }

        if (newPacketThisFrame)
        {
            // 1. สั่งโมเดล 3D ยุบหน้าอก
            if (chestCompressor != null)
                chestCompressor.SetDepth(depth);

            // 2. ส่งข้อมูลเข้า Logic คำนวณ
            if (cprManager != null)
                cprManager.ProcessSensorSample(depth, Time.time);

            // 3. วาดกราฟลงบอร์ดเดิมในฉาก
            if (mainGraphRenderer != null)
                mainGraphRenderer.AddPoint(depth);

            // 4. วาดกราฟลง Mini-HUD ติดกล้อง
            if (hudGraphRenderer != null)
                hudGraphRenderer.AddPoint(depth);

            // 5. อัปเดตข้อมูลบนบอร์ดจำลองเดิม
            float depthCm = depth * 7.0f;
            string status = (depthCm >= 5.0f && depthCm <= 6.0f) ? "(Good Depth)" : (depthCm < 5.0f ? "(Too Shallow)" : "(Too Deep)");
            if (depthCm <= 0.5f) status = "(Released)";

            if (simulator != null)
            {
                if (simulator.depthSlider != null)
                {
                    simulator.depthSlider.interactable = false;
                    simulator.depthSlider.SetValueWithoutNotify(depth);
                }

                if (simulator.depthValueText != null)
                    simulator.depthValueText.text = $"Depth: {depthCm:F1} cm {status}";
            }

            // 6. อัปเดตตัวเลขบน Mini-HUD ของผู้เล่น
            if (hudDepthText != null)
            {
                hudDepthText.text = $"Depth: {depthCm:F1} cm\n<size=80%>{status}</size>";
            }

            if (hudBpmText != null && cprManager != null)
            {
                // ดึงค่า BPM ที่คำนวณได้มาแสดง
                hudBpmText.text = $"BPM: {cprManager.CurrentBpm:F0}";
            }

            // Debug Log ทุก 0.5 วินาที
            consoleLogTimer += Time.deltaTime;
            if (consoleLogTimer >= 0.5f)
            {
                consoleLogTimer = 0f;
                Debug.Log($"<color=lime>[ESP32 Live]</color> \"{rawText}\" | Depth: {depthCm:F1} cm | Packet: #{pId} | Loss: {lostPacketCount}");
            }
        }
    }

    private void CloseSocket()
    {
        isRunning = false;

        if (udpClient != null)
        {
            try { udpClient.Close(); } catch { }
            udpClient = null;
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            try { receiveThread.Abort(); } catch { }
            receiveThread = null;
        }
    }

    void OnDisable() => CloseSocket();
    void OnDestroy() => CloseSocket();
    void OnApplicationQuit() => CloseSocket();
}