using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CPRSensorSimulator : MonoBehaviour
{
    [Header("Core References")]
    public CPRLogicManager cprManager;
    public UIGraphRenderer graphRenderer;

    [Header("Main Simulator Panel")]
    public GameObject simulatorPanel;

    [Header("Controls")]
    public Button autoDemoButton;
    public TextMeshProUGUI autoDemoLabel;
    public Button manualSimButton;
    public TextMeshProUGUI manualSimLabel;
    public Slider depthSlider;
    public TextMeshProUGUI depthValueText;

    [Header("Sampling Configuration (Requirement: 20-50 ms)")]
    [Range(0.02f, 0.05f)]
    public float sampleInterval = 0.03f; // 30 ms (approx 33 Hz)

    [Header("Depth Calibration (Adult CPR Standard)")]
    [Tooltip("Maximum mechanical compression limit of the manikin (cm).")]
    public float maxDepthCm = 7.0f; // 1.0 on slider = 7.0 cm

    private enum SimMode { None, Auto, Manual }
    private SimMode currentMode = SimMode.None;
    private Coroutine samplingCoroutine;

    void Start()
    {
        if (autoDemoButton != null)
            autoDemoButton.onClick.AddListener(ToggleAutoDemo);

        if (manualSimButton != null)
            manualSimButton.onClick.AddListener(ToggleManualSim);

        if (depthSlider != null)
        {
            depthSlider.interactable = false;
            depthSlider.minValue = 0f;
            depthSlider.maxValue = 1f;
            depthSlider.value = 0f;
        }

        // Calibrate CPRLogicManager depth thresholds to match 5.0 - 6.0 cm
        CalibrateManagerThresholds();

        UpdateControlsUI();
    }

    void Update()
    {
        if (cprManager == null || simulatorPanel == null) return;

        // Show simulator UI only during Compressions step
        bool isCompressingStep = cprManager.currentState == CPRLogicManager.ScenarioState.Compressions;
        if (simulatorPanel.activeSelf != isCompressingStep)
        {
            simulatorPanel.SetActive(isCompressingStep);
            if (!isCompressingStep) StopAllSimulations();
        }
    }

    private void CalibrateManagerThresholds()
    {
        if (cprManager == null) return;

        // Standard adult CPR depth: 5.0 to 6.0 cm
        cprManager.goodDepthMin = 5.0f / maxDepthCm; // ~0.714
        cprManager.goodDepthMax = 6.0f / maxDepthCm; // ~0.857
        cprManager.peakThreshold = 0.35f;
        cprManager.recoilThreshold = 0.08f;
        //cprManager.useMockData = false;
    }

    // ==========================================
    // Mode 1: Auto Simulation (Gold Standard Demo)
    // ==========================================
    public void ToggleAutoDemo()
    {
        if (currentMode == SimMode.Auto)
        {
            StopAllSimulations();
        }
        else
        {
            StopAllSimulations();
            currentMode = SimMode.Auto;

            if (cprManager != null)
            {
                CalibrateManagerThresholds();
                //cprManager.ResetCompressionStats(); // Reset pause penalties and score to 100
            }

            if (depthSlider != null)
                depthSlider.interactable = false;

            samplingCoroutine = StartCoroutine(AutoSamplingLoop());
        }
        UpdateControlsUI();
    }

    private IEnumerator AutoSamplingLoop()
    {
        var wait = new WaitForSeconds(sampleInterval);
        float duration = 60f / 110f; // Target: 110 BPM (~0.545s per cycle)
        float simTimer = 0f;

        // Target depth: 5.5 cm (Ideal middle of 5.0 - 6.0 cm)
        float targetDepthNormalized = 5.5f / maxDepthCm;

        while (currentMode == SimMode.Auto)
        {
            simTimer += sampleInterval;
            float phase = (simTimer % duration) / duration; // 0.0 to 1.0 within cycle

            // --- โค้ดเดิม (ทำให้เกิดมุมหัก V) ---
            // float simulatedDepth = Mathf.Sin(phase * Mathf.PI) * targetDepthNormalized;

            // --- เปลี่ยนเป็นโค้ดนี้ (คลื่นโค้งมนนุ่มนวลตามหลักสากล) ---
            float smoothFactor = 0.5f * (1f - Mathf.Cos(phase * 2f * Mathf.PI));
            float simulatedDepth = smoothFactor * targetDepthNormalized;
            float depthCm = simulatedDepth * maxDepthCm;
            float currentTime = Time.time;

            // Animate slider to match
            if (depthSlider != null)
                depthSlider.value = simulatedDepth;

            // Feed to logic processor & graph
            if (cprManager != null)
                cprManager.ProcessSensorSample(simulatedDepth, currentTime);

            if (graphRenderer != null)
                graphRenderer.AddPoint(simulatedDepth);

            // Display feedback
            if (depthValueText != null)
                depthValueText.text = $"Depth: {depthCm:F1} cm (Good Depth)";

            yield return wait;
        }
    }

    // ==========================================
    // Mode 2: Manual Simulation (Slider Control)
    // ==========================================
    public void ToggleManualSim()
    {
        if (currentMode == SimMode.Manual)
        {
            StopAllSimulations();
        }
        else
        {
            StopAllSimulations();
            currentMode = SimMode.Manual;

            if (cprManager != null)
            {
                CalibrateManagerThresholds();
                cprManager.ResetCompressionStats();
            }

            if (depthSlider != null)
            {
                depthSlider.interactable = true;
                depthSlider.value = 0f;
            }

            samplingCoroutine = StartCoroutine(ManualSamplingLoop());
        }
        UpdateControlsUI();
    }

    private IEnumerator ManualSamplingLoop()
    {
        var wait = new WaitForSeconds(sampleInterval);
        while (currentMode == SimMode.Manual)
        {
            float sliderVal = depthSlider != null ? depthSlider.value : 0f;
            float depthCm = sliderVal * maxDepthCm;
            float currentTime = Time.time;

            if (cprManager != null)
                cprManager.ProcessSensorSample(sliderVal, currentTime);

            if (graphRenderer != null)
                graphRenderer.AddPoint(sliderVal);

            if (depthValueText != null)
            {
                if (sliderVal < 0.08f)
                    depthValueText.text = $"Depth: {depthCm:F1} cm (Released)";
                else if (depthCm < 5.0f)
                    depthValueText.text = $"Depth: {depthCm:F1} cm (Too Shallow)";
                else if (depthCm <= 6.0f)
                    depthValueText.text = $"Depth: {depthCm:F1} cm (Good Depth)";
                else
                    depthValueText.text = $"Depth: {depthCm:F1} cm (Too Deep)";
            }

            yield return wait;
        }
    }

    private void StopAllSimulations()
    {
        currentMode = SimMode.None;

        if (samplingCoroutine != null)
            StopCoroutine(samplingCoroutine);

        if (cprManager != null)
            //cprManager.useMockData = false;

        if (depthSlider != null)
        {
            depthSlider.value = 0f;
            depthSlider.interactable = false;
        }

        if (depthValueText != null)
            depthValueText.text = "Depth: 0.0 cm";

        UpdateControlsUI();
    }

    private void UpdateControlsUI()
    {
        if (autoDemoLabel != null)
            autoDemoLabel.text = (currentMode == SimMode.Auto) ? "Stop Auto" : "Auto Simulation";

        if (manualSimLabel != null)
            manualSimLabel.text = (currentMode == SimMode.Manual) ? "Stop Manual" : "Manual Simulation";
    }

    void OnDisable()
    {
        StopAllSimulations();
    }
}