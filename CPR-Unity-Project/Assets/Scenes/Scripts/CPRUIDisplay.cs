using UnityEngine;
using TMPro;

/// <summary>
/// Listens to CPRLogicManager's events and updates the on-screen
/// ScoreText / BpmText TextMeshPro labels in real time.
/// Attach this to the same GameObject as CPRLogicManager (CPRManager),
/// or any GameObject, then drag the references in the Inspector.
/// </summary>
public class CPRUIDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the CPRManager GameObject's CPRLogicManager component here.")]
    public CPRLogicManager cprManager;

    [Tooltip("Drag the ScoreText TextMeshPro object here.")]
    public TextMeshProUGUI scoreText;

    [Tooltip("Drag the BpmText TextMeshPro object here.")]
    public TextMeshProUGUI bpmText;

    void OnEnable()
    {
        if (cprManager == null) return;

        cprManager.OnScoreUpdated += HandleScoreUpdated;
        cprManager.OnCompressionDetected += HandleCompressionDetected;
    }

    void OnDisable()
    {
        if (cprManager == null) return;

        cprManager.OnScoreUpdated -= HandleScoreUpdated;
        cprManager.OnCompressionDetected -= HandleCompressionDetected;
    }

    void Update()
    {
        // Also refresh every frame as a fallback, in case events haven't
        // fired yet (e.g. right when Play starts before first compression).
        if (cprManager == null) return;

        // Only show Score/BPM while actively in the Compressions step -
        // otherwise they overlap with InstructionText or the Summary screen.
        bool showDuringThisStep = cprManager.currentState == CPRLogicManager.ScenarioState.Compressions;

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(showDuringThisStep);
            if (showDuringThisStep)
                scoreText.text = $"Score: {cprManager.score:F1}";
        }

        if (bpmText != null)
        {
            bpmText.gameObject.SetActive(showDuringThisStep);
            if (showDuringThisStep)
                bpmText.text = $"BPM: {cprManager.CurrentBpm:F0}";
        }
    }

    private void HandleScoreUpdated(float newScore)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {newScore:F1}";
    }

    private void HandleCompressionDetected(int totalCompressions)
    {
        if (bpmText != null)
            bpmText.text = $"BPM: {cprManager.CurrentBpm:F0}";
    }
}