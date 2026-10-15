using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CPR Logic, Signal Processing & Scoring
/// Handles: peak/recoil compression detection, rate (BPM) + pause detection,
/// scenario state machine, and score calculation.
/// Fully self-contained: works with real sensor data OR simulated mock data,
/// so it can be tested/demoed without any ESP32 / pressure sensor hardware.
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
    // COMPRESSION DETECTION (Peak & Recoil)
    // ---------------------------------------------------------------
    [Header("Compression Detection")]
    [Tooltip("Sensor value below baseline that counts as a compression peak (depth threshold).")]
    public float peakThreshold = 0.3f;
    [Tooltip("How close to baseline the signal must return to count as 'full recoil'.")]
    public float recoilThreshold = 0.05f;

    private float baseline = 0f;
    private bool isCompressing = false;   // currently going down toward a peak
    private bool waitingForRecoil = false; // past peak, waiting for release
    private float currentPeakDepth = 0f;

    // ---------------------------------------------------------------
    // RATE (BPM) & PAUSE DETECTION
    // ---------------------------------------------------------------
    [Header("Rate & Pause")]
    public float targetBpmMin = 100f;
    public float targetBpmMax = 120f;
    [Tooltip("Seconds of no compression before it counts as a 'pause'.")]
    public float pauseThresholdSeconds = 2f;
    [Tooltip("Score penalty applied per detected pause.")]
    public float pausePenalty = 5f;

    private List<float> compressionTimestamps = new List<float>();
    private float lastCompressionTime = -999f;
    public float CurrentBpm { get; private set; } = 0f;

    // ---------------------------------------------------------------
    // SCORING
    // ---------------------------------------------------------------
    [Header("Scoring")]
    public float score = 100f;
    private int totalCompressions = 0;
    private int goodDepthCompressions = 0;
    private int goodRateCompressions = 0;
    private int pauseCount = 0;

    // Depth is normalized (0-1 from sensor); adjust to your sensor's real range.
    [Tooltip("Minimum normalized depth considered a 'good' compression.")]
    public float goodDepthMin = 0.3f;
    [Tooltip("Maximum normalized depth considered a 'good' compression (avoid too deep).")]
    public float goodDepthMax = 0.6f;

    // ---------------------------------------------------------------
    // EVENTS (hook these up to UI / feedback / animation later)
    // ---------------------------------------------------------------
    public System.Action<int> OnCompressionDetected;   // passes total compression count
    public System.Action<float> OnPauseDetected;        // passes pause duration
    public System.Action<float> OnScoreUpdated;          // passes current score
    public System.Action<ScenarioState> OnStateChanged;

    void Update()
    {
        if (currentState != ScenarioState.Compressions) return;

        CheckForPause();
    }

    // =================================================================
    // PUBLIC ENTRY POINT — call this every frame/sample with the latest
    // pressure sensor reading (0 = baseline/no pressure, 1 = max pressure).
    // Works identically whether the value comes from the real ESP32
    // or from GenerateMockSample() below.
    // =================================================================
    public void ProcessSensorSample(float normalizedPressure, float timestamp)
    {
        if (currentState != ScenarioState.Compressions) return;

        float delta = normalizedPressure - baseline;

        if (!waitingForRecoil)
        {
            // Watching for a downward peak
            if (delta >= peakThreshold)
            {
                waitingForRecoil = true;
                currentPeakDepth = delta;
            }
        }
        else
        {
            // Track deepest point of this compression
            if (delta > currentPeakDepth)
                currentPeakDepth = delta;

            // Check for full recoil back to baseline
            if (delta <= recoilThreshold)
            {
                RegisterCompression(currentPeakDepth, timestamp);
                waitingForRecoil = false;
                currentPeakDepth = 0f;
            }
        }
    }

    private void RegisterCompression(float depth, float timestamp)
    {
        totalCompressions++;
        compressionTimestamps.Add(timestamp);
        lastCompressionTime = timestamp;

        // Keep only the last few timestamps for a rolling BPM calc
        if (compressionTimestamps.Count > 6)
            compressionTimestamps.RemoveAt(0);

        UpdateBpm();

        bool goodDepth = depth >= goodDepthMin && depth <= goodDepthMax;
        if (goodDepth) goodDepthCompressions++;

        bool goodRate = CurrentBpm >= targetBpmMin && CurrentBpm <= targetBpmMax;
        if (goodRate) goodRateCompressions++;

        OnCompressionDetected?.Invoke(totalCompressions);
        RecalculateScore();
    }

    private void UpdateBpm()
    {
        if (compressionTimestamps.Count < 2)
        {
            CurrentBpm = 0f;
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

            // Reset so we don't penalize every single frame during the pause
            lastCompressionTime = Time.time;
        }
    }

    private void RecalculateScore()
    {
        if (totalCompressions == 0) return;

        float depthRatio = (float)goodDepthCompressions / totalCompressions;
        float rateRatio = (float)goodRateCompressions / totalCompressions;

        // High-Quality CPR weighting: depth 50%, rate 50%, minus pause penalties already applied
        float qualityScore = ((depthRatio * 0.5f) + (rateRatio * 0.5f)) * 100f;
        score = Mathf.Clamp(qualityScore - (pauseCount * pausePenalty), 0f, 100f);

        OnScoreUpdated?.Invoke(score);
    }

    // ---------------------------------------------------------------
    // STATE MACHINE CONTROL
    // ---------------------------------------------------------------
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
        pauseCount = 0;
        compressionTimestamps.Clear();
        lastCompressionTime = -999f;
        CurrentBpm = 0f;
        score = 100f;
        waitingForRecoil = false;
        currentPeakDepth = 0f;
    }

    public ScoreSummary GetSummary()
    {
        return new ScoreSummary
        {
            totalCompressions = totalCompressions,
            goodDepthCompressions = goodDepthCompressions,
            goodRateCompressions = goodRateCompressions,
            pauseCount = pauseCount,
            finalScore = score
        };
    }

    [System.Serializable]
    public struct ScoreSummary
    {
        public int totalCompressions;
        public int goodDepthCompressions;
        public int goodRateCompressions;
        public int pauseCount;
        public float finalScore;
    }

    // =================================================================
    // MOCK DATA TESTING — no ESP32/sensor needed.
    // Attach this component in an empty test scene, enable useMockData,
    // press Play, and it will feed itself simulated compression cycles.
    // =================================================================
    [Header("Mock Data Testing")]
    public bool useMockData = false;
    [Tooltip("Simulated compressions per minute for the mock signal.")]
    public float mockTargetBpm = 110f;
    [Range(0f, 1f)] public float mockPeakDepth = 0.45f;

    private float mockCycleTimer = 0f;
    private float mockCycleDuration => 60f / Mathf.Max(1f, mockTargetBpm);

    void Start()
    {
        // NOTE: Mock data only ever samples while currentState == Compressions
        // (see LateUpdate below), so we no longer force the state here.
        // This lets an external controller (e.g. CPRScenarioController) own
        // the FindScene -> CheckBreathing -> Call1669 -> Compressions -> Summary
        // progression. Stats still reset whenever Compressions state begins
        // (see AdvanceState()).
    }

    void LateUpdate()
    {
        if (!useMockData || currentState != ScenarioState.Compressions) return;

        mockCycleTimer += Time.deltaTime;
        float phase = (mockCycleTimer % mockCycleDuration) / mockCycleDuration; // 0..1 within one compression cycle

        // Simple sine-based down/up motion: 0 = baseline, peak mid-cycle, back to 0
        float simulatedValue = Mathf.Sin(phase * Mathf.PI) * mockPeakDepth;
        ProcessSensorSample(simulatedValue, Time.time);
    }
}