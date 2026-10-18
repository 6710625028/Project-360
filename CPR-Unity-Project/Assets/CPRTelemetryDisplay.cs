using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CPRTelemetryDisplay : MonoBehaviour
{
    [Header("References")]
    public CPRLogicManager cprManager;
    public Slider depthSlider;

    [Header("UI Text Outputs")]
    public TextMeshProUGUI depthText;       // Current depth in cm
    public TextMeshProUGUI depthStatusText; // Status: Shallow / Good / Too Deep
    public TextMeshProUGUI recoilStatusText;// Status: Full Recoil / Release Chest
    public TextMeshProUGUI paceStatusText;  // Pace: Too Slow / Good / Too Fast
    public TextMeshProUGUI countText;       // Total compressions

    [Header("Color Indicators")]
    public Color normalColor = Color.white;
    public Color goodColor = Color.green;
    public Color warningColor = new Color(1f, 0.6f, 0f); // Orange / Yellow
    public Color dangerColor = Color.red;

    // Adult CPR standard: 5.0 - 6.0 cm
    private const float MAX_DEPTH_CM = 7.0f;
    private const float TARGET_DEPTH_MIN = 5.0f;
    private const float TARGET_DEPTH_MAX = 6.0f;

    void Update()
    {
        if (cprManager == null) return;

        float currentPressure = depthSlider != null ? depthSlider.value : 0f;
        float currentDepthCm = currentPressure * MAX_DEPTH_CM;

        // 1. Update depth and guidance feedback
        UpdateDepthFeedback(currentDepthCm, currentPressure);

        // 2. Update full recoil feedback
        UpdateRecoilFeedback(currentPressure);

        // 3. Update BPM pace feedback
        UpdatePaceFeedback(cprManager.CurrentBpm);

        // 4. Update compression count
        if (countText != null)
        {
            var summary = cprManager.GetSummary();
            countText.text = $"Count: {summary.totalCompressions}";
        }
    }

    private void UpdateDepthFeedback(float depthCm, float rawVal)
    {
        if (depthText != null)
            depthText.text = $"Depth: {depthCm:F1} cm";

        if (depthStatusText == null) return;

        if (rawVal < 0.1f)
        {
            depthStatusText.text = "Ready to Compress";
            depthStatusText.color = normalColor;
        }
        else if (depthCm < TARGET_DEPTH_MIN)
        {
            depthStatusText.text = "Too Shallow (Push Harder)";
            depthStatusText.color = warningColor;
        }
        else if (depthCm <= TARGET_DEPTH_MAX)
        {
            depthStatusText.text = "Good Depth (Correct)";
            depthStatusText.color = goodColor;
        }
        else
        {
            depthStatusText.text = "Too Deep (Release Pressure)";
            depthStatusText.color = dangerColor;
        }
    }

    private void UpdateRecoilFeedback(float rawVal)
    {
        if (recoilStatusText == null) return;

        if (rawVal <= cprManager.recoilThreshold)
        {
            recoilStatusText.text = "Recoil: Full Recoil (Good)";
            recoilStatusText.color = goodColor;
        }
        else if (rawVal > 0.2f)
        {
            recoilStatusText.text = "Recoil: Release Chest Fully";
            recoilStatusText.color = warningColor;
        }
    }

    private void UpdatePaceFeedback(float bpm)
    {
        if (paceStatusText == null) return;

        if (bpm <= 0)
        {
            paceStatusText.text = "Pace: Waiting to start";
            paceStatusText.color = normalColor;
        }
        else if (bpm < cprManager.targetBpmMin)
        {
            paceStatusText.text = $"Pace ({bpm:F0} BPM): Too Slow (Speed Up)";
            paceStatusText.color = warningColor;
        }
        else if (bpm <= cprManager.targetBpmMax)
        {
            paceStatusText.text = $"Pace ({bpm:F0} BPM): Good Pace";
            paceStatusText.color = goodColor;
        }
        else
        {
            paceStatusText.text = $"Pace ({bpm:F0} BPM): Too Fast (Slow Down)";
            paceStatusText.color = dangerColor;
        }
    }
}