using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the 5-step CPR scenario flow end-to-end:
/// FindScene -> CheckBreathing -> Call1669 -> Compressions -> Summary
/// Shows instruction text per step, a "Next" button to advance,
/// and a final summary screen with the score breakdown.
/// Works together with CPRLogicManager (reads its state/events)
/// and CPRUIDisplay (which keeps showing live Score/BPM during Compressions).
/// </summary>
public class CPRScenarioController : MonoBehaviour
{
    [Header("References")]
    public CPRLogicManager cprManager;

    [Header("Instruction UI")]
    [Tooltip("Text showing the current step's instruction.")]
    public TextMeshProUGUI instructionText;
    [Tooltip("Button the user presses to move to the next step.")]
    public Button nextButton;
    [Tooltip("Text on the Next button (changes on the last step before scoring).")]
    public TextMeshProUGUI nextButtonLabel;

    [Header("Summary UI")]
    [Tooltip("Panel shown only during the Summary step.")]
    public GameObject summaryPanel;
    public TextMeshProUGUI summaryText;

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

        if (summaryPanel != null)
            summaryPanel.SetActive(false);

        RefreshUI(cprManager.currentState);
    }

    void OnDestroy()
    {
        if (cprManager != null)
            cprManager.OnStateChanged -= HandleStateChanged;

        if (nextButton != null)
            nextButton.onClick.RemoveListener(OnNextPressed);
    }

    private void OnNextPressed()
    {
        if (cprManager.currentState == CPRLogicManager.ScenarioState.Summary)
        {
            // Restart the whole scenario from the beginning.
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
            nextButtonLabel.text = (state == CPRLogicManager.ScenarioState.Summary) ? "Done" : "Next";

        if (state == CPRLogicManager.ScenarioState.Summary)
            ShowSummary();
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
                return "Begin chest compressions. Aim for 100-120 BPM at the correct depth.";
            case CPRLogicManager.ScenarioState.Summary:
                return "Scenario complete. Here is your performance summary.";
            default:
                return "";
        }
    }

    private void ShowSummary()
    {
        if (summaryText == null) return;

        var summary = cprManager.GetSummary();
        summaryText.text =
            $"Total Compressions: {summary.totalCompressions}\n" +
            $"Good Depth: {summary.goodDepthCompressions}\n" +
            $"Good Rate: {summary.goodRateCompressions}\n" +
            $"Pauses: {summary.pauseCount}\n" +
            $"Final Score: {summary.finalScore:F1}";
    }
}