using System.Collections;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// จำลองการยิง UDP จากบอร์ด ESP32
/// รองรับ 2 โหมด: Perfect CPR (เป๊ะตามเกณฑ์) และ Realistic Human (มีความผิดพลาดตามจริง)
/// </summary>
public class ESP32MockSender : MonoBehaviour
{
    public enum SimulationMode
    {
        PerfectCPR,      // โหมดเป๊ะ: 110 BPM, ลึก 5.5 cm, Full Recoil 100%
        RealisticHuman   // โหมดคนจริง: จังหวะแกว่ง, แรงตกเมื่อยล้า, ปล่อยมือไม่สุด, มีจังหวะหยุดพัก
    }

    [Header("Simulation Profile")]
    [Tooltip("เลือกสลับโหมดได้แบบ Real-Time จาก Dropdown ขณะรัน")]
    public SimulationMode simulationMode = SimulationMode.PerfectCPR;

    [Header("Target UDP Host")]
    public string targetIp = "127.0.0.1";
    public int targetPort = 8055;

    [Header("Simulation State")]
    public bool isSending = false;

    private UdpClient senderClient;
    private int packetCounter = 1000;
    private Coroutine sendCoroutine;

    // ตัวแปรสำหรับจำลองพฤติกรรมมนุษย์
    private float humanTimer = 0f;
    private float currentPaceBpm = 110f;
    private float targetDepthRatio = 0.785f; // ~5.5 cm
    private float baselineLean = 0f;          // จำลองการกดแช่ (Leaning)
    private bool isPausing = false;
    private float pauseTimer = 0f;

    void Start()
    {
        senderClient = new UdpClient();
        if (isSending) StartSendingMockESP32();
    }

    void Update()
    {
        if (isSending && sendCoroutine == null)
        {
            sendCoroutine = StartCoroutine(SendLoop());
        }
        else if (!isSending && sendCoroutine != null)
        {
            StopCoroutine(sendCoroutine);
            sendCoroutine = null;
        }
    }

    public void StartSendingMockESP32()
    {
        isSending = true;
        if (sendCoroutine == null)
            sendCoroutine = StartCoroutine(SendLoop());
    }

    public void StopSendingMockESP32()
    {
        isSending = false;
        if (sendCoroutine != null)
        {
            StopCoroutine(sendCoroutine);
            sendCoroutine = null;
        }
    }

    private IEnumerator SendLoop()
    {
        var wait = new WaitForSeconds(0.03f); // ความถี่ส่ง 30 ms (~33 Hz)
        float cycleTimer = 0f;

        while (isSending)
        {
            humanTimer += 0.03f;

            // ==============================================================
            // โหมดที่ 1: PERFECT CPR (จังหวะและระยะเป๊ะ 100%)
            // ==============================================================
            if (simulationMode == SimulationMode.PerfectCPR)
            {
                currentPaceBpm = 110f;
                targetDepthRatio = 5.5f / 7.0f; // 5.5 cm (~0.785)
                baselineLean = 0f;
                isPausing = false;
            }
            // ==============================================================
            // โหมดที่ 2: REALISTIC HUMAN (จำลองความผิดพลาดของมนุษย์จริง)
            // ==============================================================
            else
            {
                // 1. จำลองการหยุดชะงัก (Pause) ทุกๆ 18 วินาที เป็นเวลา 2.5 วินาที
                if (humanTimer > 15f && !isPausing && (Mathf.FloorToInt(humanTimer) % 18 == 0))
                {
                    isPausing = true;
                    pauseTimer = 2.5f;
                }

                if (isPausing)
                {
                    pauseTimer -= 0.03f;
                    if (pauseTimer <= 0f) isPausing = false;

                    // ขณะหยุดชะงัก ค่าแรงกดจะค้างนิ่งที่ระดับพื้น
                    SendPacket(0);
                    yield return wait;
                    continue;
                }

                // 2. จังหวะแกว่ง (Rate Variation): แกว่งระหว่าง 95 ถึง 130 BPM
                float paceNoise = Mathf.PerlinNoise(humanTimer * 0.2f, 0f);
                currentPaceBpm = Mathf.Lerp(95f, 130f, paceNoise);

                // 3. แรงตกเมื่อยล้า (Fatigue): ยิ่งกดนาน ความลึกยิ่งลดลง (จาก 5.5 cm เหลือ 3.8 cm)
                float fatigueProgress = Mathf.Clamp01(humanTimer / 45f);
                float currentCm = Mathf.Lerp(5.5f, 3.8f, fatigueProgress);
                targetDepthRatio = currentCm / 7.0f;

                // 4. การกดแช่/คืนตัวไม่สุด (Incomplete Recoil): เกิดขึ้นช่วงวินาทีที่ 10-18 และ 35-42
                if ((humanTimer >= 10f && humanTimer <= 16f) || (humanTimer >= 32f && humanTimer <= 38f))
                {
                    baselineLean = 0.18f; // หน้าอกคืนตัวค้างอยู่ที่ 1.2 ซม. ไม่แตะ 0 ซม.
                }
                else
                {
                    baselineLean = 0f;
                }
            }

            // คำนวณคลื่นความลึกตามวงรอบ
            float cycleDuration = 60f / Mathf.Max(30f, currentPaceBpm);
            cycleTimer += 0.03f;
            float phase = (cycleTimer % cycleDuration) / cycleDuration;

            // สูตรคลื่น Raised Cosine
            float smooth = 0.5f * (1f - Mathf.Cos(phase * 2f * Mathf.PI));
            float simulatedNormalized = baselineLean + (smooth * (targetDepthRatio - baselineLean));
            simulatedNormalized = Mathf.Clamp01(simulatedNormalized);

            int simulatedAdc = Mathf.RoundToInt(simulatedNormalized * 4095f);
            SendPacket(simulatedAdc);

            yield return wait;
        }
    }

    private void SendPacket(int adcValue)
    {
        packetCounter++;
        long boardMillis = (long)(Time.time * 1000f);
        string payload = $"{packetCounter},{adcValue},{boardMillis}";
        byte[] bytes = Encoding.UTF8.GetBytes(payload);

        try
        {
            senderClient.Send(bytes, bytes.Length, targetIp, targetPort);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ESP32MockSender] UDP Send Error: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        StopSendingMockESP32();
        if (senderClient != null)
        {
            senderClient.Close();
            senderClient = null;
        }
    }
}