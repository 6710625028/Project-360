using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CPRScenarioController : MonoBehaviour
{
    [Header("Core References")]
    public CPRLogicManager cprManager;

    [Header("Instruction UI")]
    public TextMeshProUGUI instructionText;
    public Button nextButton;
    public TextMeshProUGUI nextButtonLabel;

    [Header("Scenario Illustration UI")]
    public Image scenarioImage;             // ลาก GameObject Image มาใส่
    public Sprite findSceneSprite;           // รูปที่ 1: มาถึงที่เกิดเหตุ
    public Sprite checkBreathingSprite;      // รูปที่ 2: ตรวจการตอบสนอง
    public Sprite call1669Sprite;            // รูปที่ 3: โทร 1669

    [Header("Summary UI")]
    public GameObject summaryPanel;
    public TextMeshProUGUI summaryText;

    [Header("Assessment Info Modal (i)")]
    public Button infoButton;
    public GameObject infoModalPanel;
    public Button closeInfoButton;
    public TextMeshProUGUI infoText;

    void Start()
    {
        if (cprManager == null)
        {
            Debug.LogWarning("CPRScenarioController: no CPRLogicManager assigned.");
            return;
        }

        cprManager.currentState = CPRLogicManager.ScenarioState.FindScene;
        cprManager.OnStateChanged += HandleStateChanged;

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextPressed);

        if (infoButton != null)
            infoButton.onClick.AddListener(OpenInfoModal);

        if (closeInfoButton != null)
            closeInfoButton.onClick.AddListener(CloseInfoModal);

        if (summaryPanel != null)
            summaryPanel.SetActive(false);

        if (infoModalPanel != null)
            infoModalPanel.SetActive(false);

        RefreshUI(cprManager.currentState);
    }

    void OnDestroy()
    {
        if (cprManager != null)
            cprManager.OnStateChanged -= HandleStateChanged;

        if (nextButton != null)
            nextButton.onClick.RemoveListener(OnNextPressed);

        if (infoButton != null)
            infoButton.onClick.RemoveListener(OpenInfoModal);

        if (closeInfoButton != null)
            closeInfoButton.onClick.RemoveListener(CloseInfoModal);
    }

    private void OnNextPressed()
    {
        if (cprManager.currentState == CPRLogicManager.ScenarioState.Summary)
        {
            cprManager.ResetCompressionStats();
            cprManager.currentState = CPRLogicManager.ScenarioState.FindScene;
            RefreshUI(cprManager.currentState);
            return;
        }

        cprManager.AdvanceState();
    }

    private void HandleStateChanged(CPRLogicManager.ScenarioState newState)
    {
        RefreshUI(newState);
    }

    private void RefreshUI(CPRLogicManager.ScenarioState state)
    {
        if (summaryPanel != null)
            summaryPanel.SetActive(state == CPRLogicManager.ScenarioState.Summary);

        if (instructionText != null)
            instructionText.text = GetInstructionFor(state);

        if (nextButtonLabel != null)
            nextButtonLabel.text = (state == CPRLogicManager.ScenarioState.Summary) ? "Restart" : "Next";

        // ควบคุมการแสดงผลรูปภาพประกอบตามแต่ละ State
        UpdateIllustration(state);

        if (state == CPRLogicManager.ScenarioState.Summary)
        {
            ShowSummary();
            if (infoModalPanel != null)
                infoModalPanel.SetActive(false);
        }
    }

    private void UpdateIllustration(CPRLogicManager.ScenarioState state)
    {
        if (scenarioImage == null) return;

        switch (state)
        {
            case CPRLogicManager.ScenarioState.FindScene:
                scenarioImage.gameObject.SetActive(true);
                scenarioImage.sprite = findSceneSprite;
                break;

            case CPRLogicManager.ScenarioState.CheckBreathing:
                scenarioImage.gameObject.SetActive(true);
                scenarioImage.sprite = checkBreathingSprite;
                break;

            case CPRLogicManager.ScenarioState.Call1669:
                scenarioImage.gameObject.SetActive(true);
                scenarioImage.sprite = call1669Sprite;
                break;

            case CPRLogicManager.ScenarioState.Compressions:
            case CPRLogicManager.ScenarioState.Summary:
            default:
                // เมื่อเข้าสู่โหมดกดหน้าอกจริง หรือหน้าสรุปผล ปิดรูปเพื่อแสดง Dashboard / Simulator
                scenarioImage.gameObject.SetActive(false);
                break;
        }
    }

    private string GetInstructionFor(CPRLogicManager.ScenarioState state)
    {
        switch (state)
        {
            case CPRLogicManager.ScenarioState.FindScene:
                return "You arrive at the scene. A person is lying unresponsive on the floor.";
            case CPRLogicManager.ScenarioState.CheckBreathing:
                return "Check for breathing and responsiveness. Tap/approach the person to check.";
            case CPRLogicManager.ScenarioState.Call1669:
                return "Call 1669 for emergency help before starting compressions.";
            case CPRLogicManager.ScenarioState.Compressions:
                return "Begin chest compressions. Aim for 100-120 BPM at 5-6 cm depth.";
            case CPRLogicManager.ScenarioState.Summary:
                return "Scenario complete. High-Quality CPR Performance Summary:";
            default:
                return "";
        }
    }

    private void ShowSummary()
    {
        if (summaryText == null) return;

        var summary = cprManager.GetSummary();
        int totalMinutes = Mathf.FloorToInt(summary.totalTimeSeconds / 60f);
        int totalSeconds = Mathf.FloorToInt(summary.totalTimeSeconds % 60f);

        summaryText.text =
            $"=== CPR ASSESSMENT REPORT ===\n" +
            $"Duration: {totalMinutes:00}:{totalSeconds:00} min\n" +
            $"Total Compressions: {summary.totalCompressions}\n\n" +
            $"Depth Accuracy: {summary.depthPercentage:F1}% ({summary.goodDepthCompressions}/{summary.totalCompressions})\n" +
            $"Rate Accuracy: {summary.ratePercentage:F1}% ({summary.goodRateCompressions}/{summary.totalCompressions})\n" +
            $"Full Recoil: {summary.recoilPercentage:F1}%\n" +
            $"CCF (Fraction): {summary.ccfPercentage:F1}% (Target > 60%)\n" +
            $"Pauses: {summary.pauseCount}\n\n" +
            $"OVERALL SCORE: {summary.finalScore:F1} / 100";
    }

    public void OpenInfoModal()
    {
        if (infoModalPanel == null) return;

        infoModalPanel.SetActive(true);

        if (infoText != null)
        {
            infoText.text =
                "<b>AHA CPR QUALITY CRITERIA GUIDE</b>\n\n" +
                "<b>1. Depth Accuracy (Target: 5.0 - 6.0 cm):</b>\n" +
                "Percentage of compressions meeting AHA adult depth guidelines.\n" +
                "Formula: (Good Depth Compressions / Total) * 100%\n\n" +
                "<b>2. Rate Accuracy (Target: 100 - 120 BPM):</b>\n" +
                "Percentage of compressions delivered at the optimal speed.\n" +
                "Formula: (Good Pace Compressions / Total) * 100%\n\n" +
                "<b>3. Full Recoil:</b>\n" +
                "Ensures the chest returns completely to baseline (depth <= 0.5 cm)\n" +
                "allowing complete cardiac refilling between pumps.\n\n" +
                "<b>4. CCF (Chest Compression Fraction):</b>\n" +
                "Proportion of total session time hands are actively compressing.\n" +
                "Formula: (Active Compressing Time / Total Time) * 100% (Target > 60%)\n\n" +
                "<b>5. Pauses & Score:</b>\n" +
                "Interruptions exceeding 2.0s cause blood pressure collapse.\n" +
                "Each detected pause deducts 5.0 points from overall score.";
        }
    }

    public void CloseInfoModal()
    {
        if (infoModalPanel != null)
            infoModalPanel.SetActive(false);
    }
}