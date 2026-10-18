using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CPR Logic, International Metrics & Assessment Engine
/// Tracks: Peak/Recoil, BPM, 2-Minute Cycle Timer, CCF (Chest Compression Fraction),
/// Pause Penalties, and Percentage-based Performance Summary.
/// </summary>
public class CPRLogicManager : MonoBehaviour
{
    // ---------------------------------------------------------------
    // SCENARIO STATE MACHINE
    // ---------------------------------------------------------------
    public enum ScenarioState
    {
        FindScene,        // 1. พบเหตุ
        CheckBreathing,    // 2. ตรวจการหายใจ
        Call1669,          // 3. เรียก 1669
        Compressions,      // 4. เริ่มกดหน้าอก
        Summary            // 5. สรุปคะแนน
    }

    [Header("State")]
    public ScenarioState currentState = ScenarioState.FindScene;

    // ---------------------------------------------------------------
    // 2-MINUTE CYCLE & CCF (International Standard)
    // ---------------------------------------------------------------
    [Header("CPR Cycle & CCF Standards")]
    [Tooltip("Standard CPR cycle duration in seconds (Default 120s = 2 minutes).")]
    public float cprCycleDuration = 120f;
    public float TimeRemaining { get; private set; } = 120f;
    public float TotalSessionTime { get; private set; } = 0f;
    public float ActiveCompressionTime { get; private set; } = 0f;

    // CCF: Chest Compression Fraction (% of time compressions are actively performed)
    public float CurrentCCF => (TotalSessionTime > 0.1f) 
        ? Mathf.Clamp((ActiveCompressionTime / TotalSessionTime) * 100f, 0f, 100f) 
        : 0f;

    // ---------------------------------------------------------------
    // COMPRESSION DETECTION (Peak & Recoil)
    // ---------------------------------------------------------------
    [Header("Compression Detection")]
    public float peakThreshold = 0.35f;
    public float recoilThreshold = 0.08f;

    private float baseline = 0f;
    private bool waitingForRecoil = false;
    private float currentPeakDepth = 0f;
    private float currentSampleDepth = 0f;

    // ---------------------------------------------------------------
    // RATE (BPM) & PAUSE DETECTION
    // ---------------------------------------------------------------
    [Header("Rate & Pause")]
    public float targetBpmMin = 100f;
    public float targetBpmMax = 120f;
    public float pauseThresholdSeconds = 2f;
    public float pausePenalty = 5f;

    private List<float> compressionTimestamps = new List<float>();
    private float lastCompressionTime = -999f;
    public float CurrentBpm { get; private set; } = 0f;

    // ---------------------------------------------------------------
    // SCORING & METRICS (Standard 5.0 - 6.0 cm)
    // ---------------------------------------------------------------
    [Header("Scoring")]
    public float score = 100f;
    private int totalCompressions = 0;
    private int goodDepthCompressions = 0;
    private int goodRateCompressions = 0;
    private int fullRecoilCount = 0;
    private int pauseCount = 0;

    public float goodDepthMin = 0.71f; // 5.0 cm / 7.0 cm
    public float goodDepthMax = 0.86f; // 6.0 cm / 7.0 cm

    // ---------------------------------------------------------------
    // EVENTS
    // ---------------------------------------------------------------
    public System.Action<int> OnCompressionDetected;
    public System.Action<float> OnPauseDetected;
    public System.Action<float> OnScoreUpdated;
    public System.Action<ScenarioState> OnStateChanged;

    void Update()
    {
        if (currentState != ScenarioState.Compressions) return;

        UpdateCycleTimers();
        CheckForPause();
    }

    private void UpdateCycleTimers()
    {
        TotalSessionTime += Time.deltaTime;
        TimeRemaining = Mathf.Max(0f, cprCycleDuration - TotalSessionTime);

        // If sensor is depressed beyond recoil threshold, count as active compression time
        if (currentSampleDepth > recoilThreshold || waitingForRecoil)
        {
            ActiveCompressionTime += Time.deltaTime;
        }

        // Auto-advance to Summary when the 2-minute cycle completes
        if (TimeRemaining <= 0f)
        {
            AdvanceState();
        }
    }

    // ตัวแปรจำค่าต่ำสุดที่ผ่อนมือขึ้นมาระหว่างรอบ
    private float minDepthDuringRecoil = 999f;

    // สถานะย่อยของวงรอบการกด: พัก (Idle) -> กำลังกดลง (Pressing) -> กำลังผ่อนมือขึ้น (Releasing)
    private enum CompressionCyclePhase { Idle, Pressing, Releasing }
    private CompressionCyclePhase cyclePhase = CompressionCyclePhase.Idle;
    private float lowestRecoilDepth = 999f;

    public void ProcessSensorSample(float normalizedPressure, float timestamp)
    {
        if (currentState != ScenarioState.Compressions) return;

        currentSampleDepth = normalizedPressure;
        float delta = normalizedPressure - baseline;

        switch (cyclePhase)
        {
            case CompressionCyclePhase.Idle:
                // ตรวจจับเมื่อเริ่มออกแรงกดข้ามเกณฑ์
                if (delta >= peakThreshold)
                {
                    cyclePhase = CompressionCyclePhase.Pressing;
                    currentPeakDepth = delta;
                }
                break;

            case CompressionCyclePhase.Pressing:
                // ติดตามหาจุดกดที่ลึกที่สุด (Peak) ในรอบนั้น
                if (delta > currentPeakDepth)
                {
                    currentPeakDepth = delta;
                }
                // เมื่อความลึกเริ่มลดลงจากจุดยอดเกิน 0.04 (เริ่มเข้าสู่ช่วงผ่อนมือ/คืนตัว)
                else if (delta < currentPeakDepth - 0.04f)
                {
                    cyclePhase = CompressionCyclePhase.Releasing;
                    lowestRecoilDepth = delta;
                }
                break;

            case CompressionCyclePhase.Releasing:
                // ติดตามหาจุดที่ผ่อนมือคืนตัวขึ้นมาได้ต่ำที่สุด
                if (delta < lowestRecoilDepth)
                {
                    lowestRecoilDepth = delta;
                }

                // กรณีที่ 1: ปล่อยมือคืนตัวสุดตามเกณฑ์ (Full Recoil สมบูรณ์)
                if (delta <= recoilThreshold)
                {
                    fullRecoilCount++;
                    RegisterCompression(currentPeakDepth, timestamp);
                    cyclePhase = CompressionCyclePhase.Idle;
                    currentPeakDepth = 0f;
                }
                // กรณีที่ 2: กดแช่ / คืนตัวไม่สุด แล้วออกแรงกดซ้ำลงไปใหม่
                // ตรวจจับเฉพาะเมื่ออยู่ในช่วงผ่อนมือ แล้วแรงกดเด้งกลับขึ้นมาเกิน 0.10 จากจุดต่ำสุด
                else if (delta - lowestRecoilDepth > 0.10f && currentPeakDepth >= peakThreshold)
                {
                    // นับ 1 ครั้งทันทีโดยไม่ให้แต้ม fullRecoilCount
                    RegisterCompression(currentPeakDepth, timestamp);
                    
                    // สลับเข้าสู่จังหวะกดลงรอบใหม่ต่อเนื่องทันที
                    cyclePhase = CompressionCyclePhase.Pressing;
                    currentPeakDepth = delta;
                }
                break;
        }
    }

    private void RegisterCompression(float depth, float timestamp)
    {
        totalCompressions++;
        compressionTimestamps.Add(timestamp);
        lastCompressionTime = timestamp;

        if (compressionTimestamps.Count > 6)
            compressionTimestamps.RemoveAt(0);

        UpdateBpm();

        bool goodDepth = depth >= goodDepthMin && depth <= goodDepthMax;
        if (goodDepth) goodDepthCompressions++;

        bool goodRate = (totalCompressions == 1) || (CurrentBpm >= targetBpmMin && CurrentBpm <= targetBpmMax);
        if (goodRate) goodRateCompressions++;

        OnCompressionDetected?.Invoke(totalCompressions);
        RecalculateScore();
    }

    private void UpdateBpm()
    {
        if (compressionTimestamps.Count < 2)
        {
            CurrentBpm = 110f;
            return;
        }

        float span = compressionTimestamps[compressionTimestamps.Count - 1] - compressionTimestamps[0];
        int intervals = compressionTimestamps.Count - 1;
        if (span <= 0f) return;

        float avgIntervalSeconds = span / intervals;
        CurrentBpm = 60f / avgIntervalSeconds;
    }

    private void CheckForPause()
    {
        float sinceLast = Time.time - lastCompressionTime;
        if (lastCompressionTime > 0 && sinceLast >= pauseThresholdSeconds)
        {
            pauseCount++;
            score = Mathf.Max(0f, score - pausePenalty);
            OnPauseDetected?.Invoke(sinceLast);
            OnScoreUpdated?.Invoke(score);

            lastCompressionTime = Time.time;
        }
    }

    private void RecalculateScore()
    {
        if (totalCompressions == 0) return;

        float depthRatio = (float)goodDepthCompressions / totalCompressions;
        float rateRatio = (float)goodRateCompressions / totalCompressions;
        float recoilRatio = (float)fullRecoilCount / totalCompressions;

        // AHA-aligned score weighting: Depth 40%, Rate 40%, Recoil 20%
        float qualityScore = ((depthRatio * 0.4f) + (rateRatio * 0.4f) + (recoilRatio * 0.2f)) * 100f;
        score = Mathf.Clamp(qualityScore - (pauseCount * pausePenalty), 0f, 100f);

        OnScoreUpdated?.Invoke(score);
    }

    public void AdvanceState()
    {
        if (currentState == ScenarioState.Summary) return;
        currentState++;
        if (currentState == ScenarioState.Compressions)
        {
            ResetCompressionStats();
        }
        OnStateChanged?.Invoke(currentState);
    }

    public void ResetCompressionStats()
    {
        totalCompressions = 0;
        goodDepthCompressions = 0;
        goodRateCompressions = 0;
        fullRecoilCount = 0;
        pauseCount = 0;
        TotalSessionTime = 0f;
        ActiveCompressionTime = 0f;
        TimeRemaining = cprCycleDuration;
        compressionTimestamps.Clear();
        lastCompressionTime = -999f;
        CurrentBpm = 0f;
        score = 100f;
        currentPeakDepth = 0f;
        currentSampleDepth = 0f;

        // รีเซ็ตสถานะวงรอบการกด
        cyclePhase = CompressionCyclePhase.Idle;
        lowestRecoilDepth = 999f;

        OnScoreUpdated?.Invoke(score);
    }

    public ScoreSummary GetSummary()
    {
        float depthPct = totalCompressions > 0 ? ((float)goodDepthCompressions / totalCompressions) * 100f : 0f;
        float ratePct = totalCompressions > 0 ? ((float)goodRateCompressions / totalCompressions) * 100f : 0f;
        float recoilPct = totalCompressions > 0 ? ((float)fullRecoilCount / totalCompressions) * 100f : 0f;

        return new ScoreSummary
        {
            totalCompressions = totalCompressions,
            goodDepthCompressions = goodDepthCompressions,
            goodRateCompressions = goodRateCompressions,
            fullRecoilCount = fullRecoilCount,
            pauseCount = pauseCount,
            depthPercentage = depthPct,
            ratePercentage = ratePct,
            recoilPercentage = recoilPct,
            ccfPercentage = CurrentCCF,
            totalTimeSeconds = TotalSessionTime,
            finalScore = score
        };
    }

    [System.Serializable]
    public struct ScoreSummary
    {
        public int totalCompressions;
        public int goodDepthCompressions;
        public int goodRateCompressions;
        public int fullRecoilCount;
        public int pauseCount;
        public float depthPercentage;
        public float ratePercentage;
        public float recoilPercentage;
        public float ccfPercentage;
        public float totalTimeSeconds;
        public float finalScore;
    }

    // ---------------------------------------------------------------
    // MOCK DATA TESTING
    // ---------------------------------------------------------------
    [Header("Mock Data Testing")]
    public bool useMockData = false;
    public float mockTargetBpm = 110f;
    [Range(0f, 1f)] public float mockPeakDepth = 0.785f; // 5.5 cm / 7.0 cm

    private float mockCycleTimer = 0f;
    private float mockCycleDuration => 60f / Mathf.Max(1f, mockTargetBpm);

    void LateUpdate()
    {
        if (!useMockData || currentState != ScenarioState.Compressions) return;

        mockCycleTimer += Time.deltaTime;
        float phase = (mockCycleTimer % mockCycleDuration) / mockCycleDuration;
        float simulatedValue = Mathf.Sin(phase * Mathf.PI) * mockPeakDepth;
        ProcessSensorSample(simulatedValue, Time.time);
    }
}